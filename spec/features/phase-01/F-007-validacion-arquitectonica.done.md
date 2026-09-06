# Specification — F-007: Validación arquitectónica con NetArchTest

> **Fase:** 1 — Esqueleto de arquitectura y contratos base
> **Feature:** F-007
> **Estado:** Pendiente de implementación
> **Spec ID:** SPEC-F007
>
> Fuentes de verdad: `docs/vision.md`, `docs/architecture.md`,
> `docs/roadmap.md`, `spec/constitution/tech-stack.md`,
> `spec/constitution/mission.md`,
> `spec/phases/phase-01/features.md` (F-007 dentro del desglose de
> la fase).

---

## 1. Resumen

F-007 introduce **validación arquitectónica automática** mediante
`NetArchTest.Rules`. Crea un conjunto de tests xUnit que verifican
el grafo de dependencias entre las 4 capas (`Domain`,
`Application`, `Infrastructure`, `UI`) y que **fallan en CI** si
alguien introduce una violación.

El objetivo es convertir la Clean Architecture definida en
`docs/architecture.md` §4 en **documentación ejecutable**: las
reglas de dependencia dejan de ser "lo que dice el doc" y pasan a
ser "lo que el build verifica en cada commit".

Al final de la feature, `dotnet test` debe ejecutar los nuevos tests
arquitectónicos, deben pasar sobre el estado actual del repo, y una
violación intencional debe hacer fallar el test correspondiente.

---

## 2. Contexto

El proyecto define en `docs/architecture.md` §4 un grafo de
dependencias estricto:

```
UI (Avalonia) → Infrastructure → Application → Domain
```

Más las reglas de la §4:
- `Domain` no referencia a nadie (excepto BCL puro).
- `Application` solo referencia a `Domain` y BCL/FluentValidation.
- `Infrastructure` referencia a `Application` y `Domain`, y a
  paquetes externos (EF Core, SQLite, HttpClient).
- `UI` referencia a `Application` y `Domain`; el composition root
  referencia a `Infrastructure` como **excepción explícita**.
- `Tests.*` pueden referenciar a cualquiera de los 4 proyectos.

Sin F-007, estas reglas existen solo en documentos. Una violación
(alguien añade `using Microsoft.EntityFrameworkCore;` en `Domain`
"porque lo necesito aquí") se descubre en code review (tarde) o
en producción (tardísimo). Con NetArchTest, la violación se detecta
en el commit, antes del merge.

F-007 es la **red de seguridad del proyecto entero** (cita literal
de `spec/phases/phase-01/features.md` §F-007 — "Justificación").

---

## 3. Objetivo

- Añadir el paquete `NetArchTest.Rules` (vía CPM, sin `Version`
  hardcodeado en `.csproj`) si no está ya.
- Crear un conjunto de tests xUnit en `Tests.Unit` que cubran las
  **5 reglas arquitectónicas** definidas en `architecture.md` §4.
- Hacer que `dotnet test` ejecute estos tests junto con los demás.
- Confirmar que una **violación intencional** (por ejemplo,
  añadir `using ControlDeFinanzasIA.Infrastructure;` en un archivo
  de `Domain`) **hace fallar el test correspondiente** (ver AC-3 de
  §14).
- Mantener la regla "Domain no debe tener `PackageReference`s"
  (cubierta por F-002 / F-006, ahora también enforceable).

---

## 4. Alcance

### 4.1 En alcance

- Tests xUnit en `tests/ControlDeFinanzasIA.Tests.Unit/`
  (carpeta `Architecture/`) que validan el grafo de dependencias.
- Las **5 reglas** de `architecture.md` §4, con sus excepciones
  explícitas.
- Configuración necesaria en `Tests.Unit.csproj` (referencias a
  `Mono.Cecil` si la versión de NetArchTest la requiere).
- Comentarios XML doc en cada test explicando la regla que valida
  y su justificación (referencia a `architecture.md` §4).
- Verificación de que la regla "composition root puede referenciar
  a Infrastructure" se modela con la API de NetArchTest adecuada
  (ver §10 — Excepciones explícitas).

### 4.2 Fuera de alcance

- **Reescritura de ninguna capa.** F-007 NO toca `Domain`,
  `Application`, `Infrastructure`, `UI`, ni sus `.csproj`. Solo
  añade tests.
- **Modificación de project references.** Las `<ProjectReference>`
  actuales son correctas (verificadas en F-001 y F-006). F-007 NO
  las cambia.
- **Style/format checks.** Eso lo cubre `dotnet format` en CI
  (workflow existente). F-007 no duplica eso.
- **Reglas no arquitectónicas.** F-007 valida el **grafo de
  dependencias**, no reglas de estilo, naming, longitud de
  métodos, complejidad ciclomática, etc. Esas quedan para code
  review y analyzers específicos.
- **Análisis estático del contenido de los archivos.** NetArchTest
  inspecciona referencias a nivel de **assembly**, no lee `.cs`
  línea por línea. Reglas como "no usar `string` para enums" no
  entran en F-007.
- **Pruebas unitarias adicionales** de los value objects o use
  cases. Esas viven en sus features respectivas (F-002, F-003,
  F-005 ya las tienen).
- **Composition root.** F-008 implementa la registración de DI
  en `UI/Program.cs`. F-007 NO implementa composition root; solo
  deja lista la **mecánica de excepción** que F-008 necesitará.
- **Reglas para `Tests.Integration`.** F-007 añade tests solo en
  `Tests.Unit`. Los integration tests no necesitan reglas
  arquitectónicas adicionales (puede tocar todo lo de `src/`,
  eso es por diseño).
- **Performance budgets de los tests.** F-007 no exige un tiempo
  máximo para correr los tests arquitectónicos. Si en el futuro
  se vuelve lento, se aborda en una feature de pulido.

---

## 5. Reglas arquitectónicas a validar

Las 5 reglas (numeradas R-1 a R-5) que F-007 convierte en tests
ejecutables. Cada una referencia explícita a `architecture.md` §4
para que un futuro lector pueda trazarla al documento fundacional.

### R-1 — `Domain` no referencia a nadie del proyecto

- **Documento fuente:** `docs/architecture.md` §4 — "Domain no
  referencia a nadie (excepto BCL puro)".
- **Aserción:** ningún tipo del assembly `ControlDeFinanzasIA.Domain`
  debe tener referencia (`HaveDependencyOn`) a los assemblies
  `ControlDeFinanzasIA.Application`,
  `ControlDeFinanzasIA.Infrastructure`,
  `ControlDeFinanzasIA.UI`,
  `ControlDeFinanzasIA.Tests.Unit`,
  `ControlDeFinanzasIA.Tests.Integration`.
- **Aserción adicional (paquetes prohibidos):** ningún tipo del
  assembly `ControlDeFinanzasIA.Domain` debe depender de los
  assemblies de los siguientes paquetes externos:
  - `Microsoft.EntityFrameworkCore` (cualquier variante)
  - `Avalonia.*` (cualquier variante)
  - `System.Net.Http` (HttpClient)
- **Test:** 2 tests separados (uno para referencias a proyectos,
  uno para paquetes prohibidos) o 1 test con 2 asserts.

### R-2 — `Application` solo referencia a `Domain` y BCL/FluentValidation

- **Documento fuente:** `docs/architecture.md` §4 — "Application
  solo referencia a Domain y a BCL/paquetes neutros
  (FluentValidation, etc.)".
- **Aserción:** ningún tipo del assembly
  `ControlDeFinanzasIA.Application` debe tener referencia a
  `ControlDeFinanzasIA.Infrastructure`,
  `ControlDeFinanzasIA.UI`, `Tests.*`.
- **Aserción adicional (paquetes prohibidos):** Application NO
  debe depender de EF Core, SQLite, Avalonia, ni HttpClient.
  (FluentValidation es la única excepción documentada.)
- **Test:** 1 test que valida las dos asserciones (referencias a
  proyectos + paquetes prohibidos).

### R-3 — `Infrastructure` puede referenciar a `Application` y `Domain`, y a EF Core / SQLite / HttpClient

- **Documento fuente:** `docs/architecture.md` §4 — "Infrastructure
  referencia Application y Domain, más paquetes externos".
- **Aserción (positiva):** el assembly `ControlDeFinanzasIA.Infrastructure`
  DEBE depender de `ControlDeFinanzasIA.Application` y
  `ControlDeFinanzasIA.Domain`. Sin esta dependencia, no podría
  implementar los repos.
- **Aserción (negativa):** Infrastructure NO debe depender de
  `ControlDeFinanzasIA.UI` ni de `Tests.*`. (Application y
  Domain sí; UI y Tests no.)
- **Aserción (positiva paquetes):** Infrastructure PUEDE (y debe,
  cuando los use) depender de EF Core, SQLite, HttpClient. Esta
  parte se valida implícitamente con la regla negativa "no
  depender de UI" — si el día de mañana alguien añade UI como
  dependencia, falla.
- **Test:** 1 test combinado.

### R-4 — `UI` referencia a `Application` y `Domain`; **NO** a `Infrastructure` (salvo composition root)

- **Documento fuente:** `docs/architecture.md` §4 — "UI referencia
  Application y Domain, no Infrastructure. El composition root
  (en `App.axaml.cs` / `Program.cs`) registra implementaciones
  y es la única excepción documentada".
- **Aserción:** el assembly `ControlDeFinanzasIA.UI` debe tener
  referencias a `Application` y `Domain`. NO debe tener
  referencias a `Infrastructure` desde ningún tipo, **excepto** los
  tipos marcados como "composition root" (ver §10).
- **Aserción adicional (paquetes prohibidos):** UI NO debe
  depender de EF Core directamente (el acceso a DB va por la
  interfaz, no por el paquete).
- **Test:** 1 test combinado con la exclusión explícita del
  composition root.

### R-5 — `Tests.*` pueden referenciar a cualquiera de los 4 proyectos principales

- **Documento fuente:** `docs/architecture.md` §4 — "Tests pueden
  referenciar cualquier proyecto, pero nadie los referencia a
  ellos".
- **Aserción:** los assemblies `ControlDeFinanzasIA.Tests.Unit` y
  `ControlDeFinanzasIA.Tests.Integration` PUEDEN depender de
  cualquiera de los 4 proyectos de `src/`. No es una violación
  arquitectónica.
- **Aserción (negativa):** los assemblies de `src/` NO deben
  depender de los assemblies de `Tests.*`. (Esto es la otra
  mitad de la regla: "nadie los referencia a ellos".)
- **Test:** 2 tests — uno que confirma que los tests pueden
  referenciar a los 4 proyectos (assertion positiva, redundante
  con R-1 a R-4, pero la incluimos por simetría y para que
  cualquier cambio que rompa esto falle en su test
  correspondiente), y otro que confirma que `src/` no depende
  de `Tests.*` (assertion negativa, la que captura la violación
  típica: "alguien importó un test helper en Domain").

---

## 6. Casos de uso

Cada test arquitectónico es, en sí, un caso de uso: el sistema
detecta una violación y falla el build.

### CU-1 — Detectar acoplamiento accidental de `Domain` a `Application`

- **Actor:** agente o developer que añade un `using
  ControlDeFinanzasIA.Application;` en un archivo de `Domain`
  (por error o descuido).
- **Trigger:** commit / PR con la violación.
- **Precondición:** los tests arquitectónicos están en CI.
- **Postcondición:** el test R-1 falla, el commit no se puede
  mergear (CI rojo).

### CU-2 — Detectar EF Core en `Domain`

- **Actor:** agente o developer que añade un `using
  Microsoft.EntityFrameworkCore;` en un archivo de `Domain`
  porque le conviene para definir una entidad o un value object
  con un atributo de EF.
- **Trigger:** commit / PR con la violación.
- **Precondición:** los tests arquitectónicos están en CI.
- **Postcondición:** el test R-1 (paquetes prohibidos) falla.

### CU-3 — Detectar acoplamiento de `Application` a `Infrastructure`

- **Actor:** developer que añade un `using
  ControlDeFinanzasIA.Infrastructure;` en un use case o
  validador de `Application` (rompiendo la inversión de
  dependencias).
- **Trigger:** commit / PR.
- **Precondición:** los tests arquitectónicos están en CI.
- **Postcondición:** el test R-2 falla.

### CU-4 — Detectar acoplamiento de `UI` a `Infrastructure` (fuera del composition root)

- **Actor:** developer que añade un `using
  ControlDeFinanzasIA.Infrastructure;` en un ViewModel o un code
  behind de una View, para usar un servicio concreto directamente.
- **Trigger:** commit / PR.
- **Precondición:** los tests arquitectónicos están en CI.
- **Postcondición:** el test R-4 falla, indicando exactamente qué
  tipo tiene la dependencia prohibida.

### CU-5 — Detectar que un proyecto de `src/` importa un helper de tests

- **Actor:** developer que importa un test helper (e.g.,
  `FakeClock`) en código de producción.
- **Trigger:** commit / PR.
- **Precondición:** los tests arquitectónicos están en CI.
- **Postcondición:** el test R-5 (assertion negativa) falla.

### CU-6 — Confirmar que el composition root puede referenciar a `Infrastructure`

- **Actor:** developer implementando F-008 que necesita registrar
  `AppDbContext` y los repos en el `Host` de .NET.
- **Trigger:** implementa el composition root.
- **Precondición:** la regla R-4 está modelada con la excepción
  adecuada (ver §10).
- **Postcondición:** el test R-4 pasa, el build es verde, el
  composition root compila y arranca.

---

## 7. Requisitos funcionales

### RF-1 — Tests por regla

- Existe al menos un test xUnit por cada una de las 5 reglas
  (R-1 a R-5). Mínimo 5 tests, máximo 10 (si se separan
  referencias a proyectos y paquetes prohibidos en tests
  distintos).
- Cada test es `[Fact]` (no `[Theory]`) porque cada regla tiene
  una única forma canónica; no hay parametrización útil.

### RF-2 — Convención de nombres

- La clase que agrupa los tests arquitectónicos se llama
  `ArchitectureTests` (siguiendo el patrón de los proyectos .NET
  OSS).
- Ubicación: `tests/ControlDeFinanzasIA.Tests.Unit/Architecture/ArchitectureTests.cs`.
- Si el archivo crece, se subdivide en uno por capa (e.g.,
  `DomainArchitectureTests.cs`,
  `ApplicationArchitectureTests.cs`, etc.) — decisión a
  confirmar en §16.
- Cada método de test tiene un nombre descriptivo siguiendo
  `Rule_<ID>_<Description>` o
  `R<N>_<Capa>_No_Depende_De_<Objetivo>`, según el estilo que
  el equipo prefiera (ver §16).

### RF-3 — Mensaje de fallo útil

- Cuando un test falla, el mensaje de xUnit debe ser lo
  suficientemente claro como para que un developer sepa:
  1. Qué regla se violó.
  2. Qué tipo / archivo tiene la dependencia prohibida.
  3. Dónde está documentada la regla (referencia a
     `architecture.md` §4).
- NetArchTest produce mensajes decentes por defecto. La spec
  no exige personalizar el mensaje, pero el test debe incluir un
  comentario XML doc que cite la regla (`R-N` y el párrafo de
  `architecture.md` §4).

### RF-4 — Verificación de packages prohibidos (no solo project references)

- R-1 (Domain) y R-2 (Application) verifican también que no haya
  dependencias a assemblies de paquetes prohibidos. Esto se hace
  con la API de NetArchTest sobre el nombre del assembly (e.g.,
  `Microsoft.EntityFrameworkCore`, `Avalonia.Controls`,
  `System.Net.Http`).
- No se valida la presencia/versión de paquetes a nivel de
  `.csproj` directamente; NetArchTest inspecciona las
  referencias a assemblies en el binario compilado, que es lo
  que importa en tiempo de ejecución.

### RF-5 — Excepción explícita del composition root

- La regla R-4 (UI no debe depender de Infrastructure) tiene una
  **excepción documentada** para los tipos del composition
  root. El modelo concreto (qué tipo / namespace / atributo se
  excluye) se decide en §10.

### RF-6 — Cobertura de las 4 capas

- Las 5 reglas cubren los 4 proyectos principales de `src/`:
  - R-1 cubre `Domain`.
  - R-2 cubre `Application`.
  - R-3 cubre `Infrastructure`.
  - R-4 cubre `UI`.
  - R-5 cubre la simetría y el sentido "ningún proyecto de
    `src/` debe depender de los tests".
- Las reglas son **exhaustivas** sobre las 4 capas: cualquier
  combinación prohibida de `src/X → src/Y` está cubierta por
  alguna R-N.

### RF-7 — Corren en `dotnet test` normal

- Los tests no requieren configuración especial, base de datos,
  ni servicios externos. Corren junto con los demás tests del
  proyecto.
- Tiempo de ejecución esperado: < 2 segundos para los 5–10
  tests arquitectónicos.

### RF-8 — Compilan sin warnings

- Los tests siguen las convenciones del repo: file-scoped
  namespace, XML doc, no warnings.

---

## 8. Requisitos no funcionales

### RNF-1 — Determinismo

- Los tests son deterministas. No dependen del reloj, del SO, del
  orden de ejecución ni de variables de entorno. Múltiples
  ejecuciones dan el mismo resultado.

### RNF-2 — Velocidad

- La suite de tests arquitectónicos corre en < 2 segundos en
  hardware típico. Esto es para que no se note en el feedback
  loop del developer.

### RNF-3 — Sin red, sin filesystem, sin DB

- Los tests no tocan la red, ni el filesystem, ni bases de
  datos. Son 100% estáticos (inspección de assemblies en
  memoria).

### RNF-4 — Compatibilidad con CI existente

- Los tests corren en el workflow `.github/workflows/build.yml`
  que ya ejecuta `dotnet test`. No hace falta cambiar el CI.

### RNF-5 — Mensajes de error en inglés

- Los mensajes de los tests y los comentarios XML doc están en
  inglés (convención de la comunidad .NET). Si el developer
  necesita mensaje en español, lo añade como texto adicional
  en el `Assert.True(...)` o en un comentario.

### RNF-6 — Cobertura de las reglas = 100%

- Las 5 reglas (R-1 a R-5) están cubiertas. F-007 no deja
  ninguna regla del spec de phase-01 sin test.

### RNF-7 — Sin dependencias circulares a través de tests

- Los tests NO deben introducir dependencias nuevas entre
  proyectos. Solo leen assemblies ya referenciados.

---

## 9. Configuración técnica

### 9.1 Paquete `NetArchTest.Rules`

- **NuGet:** `NetArchTest.Rules`.
- **Versión:** la última estable compatible con .NET 8 al
  momento de implementar. Fijada en
  `Directory.Packages.props` (CPM), sin `Version` en el
  `.csproj`.
- **Pre-transitivo:** `NetArchTest.Rules` arrastra `Mono.Cecil`
  (para inspección de assemblies) y `System.Reflection.Metadata`.
  Estas dependencias son esperadas y no requieren acción.

### 9.2 Proyecto de tests

- Los tests arquitectónicos van en `Tests.Unit`, NO en
  `Tests.Integration`. Razón: son tests rápidos, sin
  dependencias externas, y deben correr en el feedback loop
  corto del developer. `Tests.Integration` se reserva para
  escenarios que necesitan DB, red, filesystem.
- Si en el futuro los tests arquitectónicos necesitan
  infraestructura (por ejemplo, validar el schema de la DB
  migrada), se mueven a `Tests.Integration` como issue aparte.

### 9.3 Ubicación de los archivos

```
tests/
└── ControlDeFinanzasIA.Tests.Unit/
    ├── Architecture/
    │   ├── ArchitectureTests.cs          (clase principal)
    │   ├── DomainArchitectureTests.cs    (opcional, ver §16)
    │   ├── ApplicationArchitectureTests.cs
    │   ├── InfrastructureArchitectureTests.cs
    │   └── UIArchitectureTests.cs
    ├── Common/                            (F-005, ya existe)
    ├── TestDoubles/                       (F-004, ya existe)
    ├── ValueObjects/                      (F-002, F-003, ya existe)
    └── ControlDeFinanzasIA.Tests.Unit.csproj
```

### 9.4 Convención de `using` y namespace

- File-scoped namespace:
  `namespace ControlDeFinanzasIA.Tests.Unit.Architecture;`
- `using` directives al tope (NUNCA después del namespace —
  esto fue un bug de Fase 0 ya corregido, ver memoria
  del proyecto).
- `using NetArchTest.Rules;` y `using Xunit;` para los tipos
  comunes de los tests.
- `using System.Reflection;` para `Assembly.LoadFrom` o
  `Assembly.GetAssembly` si la API lo requiere.

### 9.5 Convención de nombres de tests

- Estilo elegido (ver §16): `R<N>_<Capa>_<Verbo>` siguiendo
  la convención de F-001, F-002 y F-006 que usan nombres
  descriptivos en español en specs.
- Ejemplos:
  - `R1_Domain_No_Depende_De_Application_Infra_UI_Tests`
  - `R2_Application_Solo_Depende_De_Domain_Y_FluentValidation`
  - `R4_UI_No_Depende_De_Infrastructure_Salvo_CompositionRoot`

---

## 10. Excepciones explícitas

### 10.1 — Composition root (regla R-4)

`architecture.md` §4 dice que UI no debe depender de
Infrastructure, "salvo el composition root, que se modela como
excepción explícita". F-007 NO implementa el composition root
(es F-008), pero **deja lista la mecánica** para que cuando
F-008 lo implemente, no rompa los tests.

**Mecánica propuesta:**

Cuando F-008 implemente el composition root, lo hará en una
clase dedicada. La convención es:

- El composition root vive en una clase llamada `Bootstrap`
  (o `CompositionRoot`) en el namespace
  `ControlDeFinanzasIA.UI`.
- Esa clase se anota con un atributo marker:
  `[CompositionRoot]` (atributo definido en
  `ControlDeFinanzasIA.UI`, no en Domain, para evitar
  acoplamiento inverso).
- El test R-4 excluye cualquier tipo con `[CompositionRoot]`
  al verificar la regla.

**API de NetArchTest para excluir tipos:**

`Types.InAssembly(uiAssembly)
    .That()
    .DoNotHaveDependencyOn(typeof(CompositionRootAttribute).Assembly)
    .ShouldNot()
    .HaveDependencyOn(infrastructureAssembly)`

(El `.DoNotHaveDependencyOn(...)` con un marker propio
es el patrón documentado de NetArchTest. Si la API exacta
cambia entre versiones, se documenta en §16.)

**Decisión de diseño:** la convención de "atributo
`[CompositionRoot]`" se introduce como parte de F-007 **en
la spec de F-008**, no se implementa el atributo ni la
clase en F-007. F-007 solo documenta el mecanismo. Razón:
mantener F-007 dentro de scope (solo tests, no
modificación de UI).

### 10.2 — Tests pueden referenciar a `src/` (regla R-5)

- No es una excepción, es la **regla**. Los `Tests.*` PUEDEN
  referenciar a cualquier proyecto de `src/`. La regla
  R-5 incluye un test que verifica que efectivamente pueden
  hacerlo (assertion positiva redundante con R-1 a R-4 pero
  que captura violaciones del tipo "alguien borró la
  `<ProjectReference>` por error").
- La única regla **negativa** de R-5 es: `src/` NO debe
  depender de `Tests.*`. Esto se modela como "Domain,
  Application, Infrastructure y UI no deben tener
  `HaveDependencyOn` a los assemblies de `Tests.Unit` o
  `Tests.Integration`".

---

## 11. Casos borde

### EC-1 — `using` indirecto a través de re-exports

- Si `Application` re-exporta un tipo de `Infrastructure` (a
  través de un `using static` o re-exponiendo en un partial
  class), NetArchTest detecta la dependencia transitiva. La
  regla falla correctamente.

### EC-2 — Referencias circulares

- Si por error alguien añade `<ProjectReference>` mutuo
  entre `Domain` y `Application`, NetArchTest lo detecta y
  el test R-1 falla. El fix es romper la referencia.

### EC-3 — Código generado por la plantilla

- Avalonia genera `App.axaml.g.cs` y otros archivos. Estos
  NO son tipos del usuario; NetArchTest los inspecciona pero
  sus dependencias no cuentan como violaciones (viven en el
  assembly `UI`, no en `Domain`).
- El `.gitignore` excluye estos archivos del repo. En
  runtime, NetArchTest los ve en `bin/` y los inspecciona
  junto con el resto del assembly. No hace falta excluirlos
  explícitamente en los tests.

### EC-4 — Assembly no encontrado

- Si un assembly referenciado no se puede cargar (por
  ejemplo, se borró el binario), NetArchTest lanza
  excepción. El test falla con un mensaje claro. Es un
  caso esperado solo en configuración rota; no requiere
  manejo especial.

### EC-5 — Tests vacíos

- Si `Architecture/` queda vacía (e.g., F-007 se
  reverte), xUnit no detecta error. La spec asume que
  esto no ocurre (F-007 deja al menos 5 tests en su
  cierre). En la práctica, dejar tests arquitectónicos
  vacíos es un **error de developer**, no un caso borde
  a manejar.

### EC-6 — NetArchTest no soporta el assembly

- En raras ocasiones, NetArchTest no puede inspeccionar un
  assembly (e.g., es de un formato no estándar). El test
  falla con un mensaje que indica qué assembly. No es
  un caso esperado en este proyecto (todos los assemblies
  son C# .NET 8 estándar).

### EC-7 — Falsa violación por convención de nombres

- Si un tipo en `Domain` se llama `Application` (e.g.,
  `namespace Domain.Applications` por error), el test R-1
  puede dar falsos positivos. Mitigación: el filtro de
  NetArchTest es por **assembly**, no por nombre de tipo,
  así que este caso no es un problema en la práctica.

### EC-8 — Atributo `[CompositionRoot]` ausente en F-007

- Mientras F-008 no exista, no hay ninguna clase con
  `[CompositionRoot]`. El test R-4 **sí** corre y
  **pasa** porque UI no tiene `<ProjectReference>` a
  Infrastructure (verificado en F-001). El marker no
  afecta el resultado mientras no haya composition root.
- Cuando F-008 añada el composition root con el atributo,
  el test R-4 sigue pasando gracias a la exclusión.

---

## 12. Dependencias

### 12.1 Prerrequisitos de implementación

- **F-001 completada** (T-001 a T-014, T-010a): los 6
  proyectos existen con las referencias correctas.
- **F-006 completada** (T-001 a T-003): las 15 interfaces
  existen en `Application/Abstractions/`. Esto es
  importante porque las reglas R-1 a R-4 son significativas
  cuando hay contratos reales que cruzar; sin interfaces,
  la violación "Application import Infrastructure" es
  trivial (no hay Infrastructure que importar). Con las
  interfaces, la violación típica es "Application importa
  un repo concreto de Infrastructure" — la regla la
  captura.
- **F-004 completada** (recomendado): IClock e IPathResolver
  viven en `Application/Abstractions/`, y su ausencia sería
  un agujero en R-2 (Application puede importar más de lo
  que debería si no hay abstractions reales que limiten).

### 12.2 Dependencias con otras features

| Feature | Relación |
| --- | --- |
| F-001 | Prove los 6 proyectos. Las reglas validan exactamente esos assemblies. |
| F-006 | Prove las 15 interfaces en `Application/Abstractions/`. Hace que R-2 sea significativa (sin ellas, R-2 validaría un assembly vacío). |
| F-008 (futuro) | Implementa el composition root. Usa la mecánica de excepción de F-007. La existencia de F-008 NO es prerrequisito de F-007. |
| F-002 de Fase 2 (futuro) | Cuando implemente los repos en `Infrastructure`, R-3 ya estará validando que Infrastructure depende correctamente de Application. |

### 12.3 Bloqueos

- **Ninguno.** F-007 puede implementarse ahora mismo.
  Los prerrequisitos (F-001, F-006) están cumplidos.

---

## 13. Fuera de alcance (detallado)

Repetición con énfasis para evitar ambigüedad. Estos puntos
**NO** se implementan en F-007:

- **Modificación de Domain, Application, Infrastructure o UI.**
  F-007 solo añade tests. Las capas quedan intactas.
- **Modificación de `.csproj` de `src/`.** Las project
  references actuales son la verdad; F-007 no las cambia.
- **Composition root.** F-008 implementa la registración
  de DI. F-007 NO toca `UI/Program.cs` ni `App.axaml.cs`.
- **Atributo `[CompositionRoot]`.** Se documenta en la
  spec pero NO se implementa en F-007 (sería código de
  UI, fuera de scope). Se implementa en F-008 cuando
  exista el composition root.
- **Reglas de style, naming, complejidad.** Esas son
  trabajo de `.editorconfig`, StyleCop, code review y
  analyzers específicos. NetArchTest no las cubre y
  F-007 no intenta añadirlas.
- **Análisis del contenido de los archivos.** NetArchTest
  trabaja a nivel de **assembly**. No lee `.cs` línea por
  línea, no valida convenciones de comentarios, no
  detecta código duplicado. Esas son otras herramientas
  (SonarQube, Roslyn analyzers, etc.).
- **Performance budgets de los tests.** F-007 no exige un
  tiempo máximo. Si los tests se vuelven lentos, se
  aborda en una feature de pulido (Fase 21, "Empaquetado,
  pulido, accesibilidad").
- **Tests para `Tests.Integration`.** F-007 añade tests
  solo en `Tests.Unit`. `Tests.Integration` se mantiene
  como está.
- **Modificación del workflow de CI.** El workflow
  existente en `.github/workflows/build.yml` ya ejecuta
  `dotnet test`. F-007 no requiere cambios allí.

---

## 14. Criterios de aceptación

La feature se considera completada cuando **todos** los
siguientes puntos se cumplen.

### AC-1 — Tests implementados

- [ ] Existen los archivos de tests arquitectónicos en
      `tests/ControlDeFinanzasIA.Tests.Unit/Architecture/`
      (al menos `ArchitectureTests.cs`).
- [ ] Cubren las 5 reglas (R-1 a R-5) de §5.
- [ ] Mínimo 5 tests, máximo 10 (ver §16).
- [ ] Cada test es `[Fact]` con un nombre descriptivo.

### AC-2 — Compilación y ejecución

- [ ] `dotnet build` desde la raíz: 0 warnings, 0 errors.
- [ ] `dotnet test` desde la raíz: todos los tests
      existentes siguen pasando (100/100 en `Tests.Unit`).
- [ ] Los nuevos tests arquitectónicos pasan sobre el
      estado actual del repo (las reglas se satisfacen).
- [ ] Tiempo de ejecución de los tests arquitectónicos:
      < 2 segundos (medible con `dotnet test` verbose).

### AC-3 — Violación intencional detectada

- [ ] Añadir un `using
      ControlDeFinanzasIA.Infrastructure;` en un archivo
      de `Domain` (e.g., `Money.cs`) **hace fallar el
      test R-1**.
- [ ] Añadir un `using
      ControlDeFinanzasIA.Application;` en un archivo
      de `Domain` (e.g., `Money.cs`) **hace fallar el
      test R-1** (proyectos prohibidos).
- [ ] Revertir la violación intencional **hace pasar el
      test de nuevo**.
- [ ] El mensaje de fallo indica qué tipo o assembly
      tiene la dependencia prohibida.

### AC-4 — Paquetes prohibidos detectados

- [ ] Añadir un `<PackageReference
      Include="Microsoft.EntityFrameworkCore" />` en
      `Domain.csproj` y un `using` correspondiente en
      un archivo de `Domain` **hace fallar el test R-1**
      (paquetes prohibidos).
- [ ] Revertir **hace pasar el test de nuevo**.

### AC-5 — Compatibilidad arquitectónica

- [ ] `Application` sigue sin `ProjectReference` a
      `Infrastructure` (verificable con
      `dotnet list src/.../Application reference`).
- [ ] `UI` sigue sin `ProjectReference` a
      `Infrastructure` (verificable con
      `dotnet list src/.../UI reference`).
- [ ] `Domain` sigue sin `PackageReference`s
      (verificable con `dotnet list package`).
- [ ] `dotnet list src/.../Application package` sigue
      mostrando solo `FluentValidation`.

### AC-6 — Convenciones del repo

- [ ] Los tests siguen el `.editorconfig` del proyecto
      (file-scoped namespace, no `using` redundantes,
      naming consistente).
- [ ] XML doc en cada test, citando la regla (`R-N`) y
      el párrafo de `architecture.md` §4 que la motiva.
- [ ] Sin placeholders en los nombres de los tests (no
      `Test1`, `Test2`, etc.).

### AC-7 — CI integration

- [ ] El workflow `.github/workflows/build.yml` ejecuta
      `dotnet test` y por tanto ejecuta los nuevos tests
      arquitectónicos.
- [ ] No se requieren cambios al workflow.

### AC-8 — Documentación y trazabilidad

- [ ] Cada test referencia en su XML doc a la regla
      correspondiente (`R-N`) y a `architecture.md` §4.
- [ ] La spec de F-007 está marcada como completada en
      `spec/phases/phase-01/features.md` (de "Pendiente" a
      "✅ COMPLETADA" en la fila 007 de la tabla resumen y
      en el header de la sección F-007).
- [ ] La spec se renombra a
      `spec/features/phase-01/F-007-validacion-arquitectonica.done.md`.

---

## 15. Estructura de archivos esperada

```
tests/
└── ControlDeFinanzasIA.Tests.Unit/
    ├── Architecture/                                 ← nuevo (F-007)
    │   ├── ArchitectureTests.cs                      ← principal
    │   ├── DomainArchitectureTests.cs                (opcional, ver §16)
    │   ├── ApplicationArchitectureTests.cs           (opcional)
    │   ├── InfrastructureArchitectureTests.cs        (opcional)
    │   └── UIArchitectureTests.cs                    (opcional)
    ├── Common/                                       (F-005, ya existe)
    │   ├── PagedResultTests.cs
    │   └── ResultTests.cs
    ├── TestDoubles/                                  (F-004, ya existe)
    │   ├── FakeClock.cs
    │   ├── FakeClockTests.cs
    │   ├── FakePathResolver.cs
    │   └── FakePathResolverTests.cs
    ├── ValueObjects/                                 (F-002, F-003, ya existe)
    │   ├── DateRangeTests.cs
    │   └── MoneyTests.cs
    ├── F001PlaceholderTests.cs                      (F-001, ya existe)
    └── ControlDeFinanzasIA.Tests.Unit.csproj
```

**Modificaciones al `.csproj` de `Tests.Unit`:** verificar
que el paquete `NetArchTest.Rules` está referenciado (vía
CPM, sin `Version`). Si F-001 ya lo añadió (la spec de
F-001 §5 lo lista), no se requiere cambio. Si no está,
añadirlo en esta feature.

**No** se añade `NetArchTest.Rules` a
`Tests.Integration` (esos tests cubren otros escenarios,
no necesitan validación arquitectónica).

---

## 16. Decisiones de diseño explícitas

### 16.1 — Un archivo por capa vs un archivo monolítico

**Decisión recomendada:** un archivo `ArchitectureTests.cs`
monolítico en F-007, con clases anidadas o regiones
(`#region`) por capa. Razón: 5 tests (o 10) caben en un
archivo de < 200 líneas, y mantener un solo archivo hace
que la revisión de las reglas sea trivial ("abre un archivo,
lee todas las reglas").

**Cuándo subdividir:** si la suite crece a > 20 tests, o
si las reglas se vuelven lo suficientemente complejas
como para necesitar contexto por archivo (e.g., exclusiones
extensas), se subdivide en `DomainArchitectureTests.cs`,
`ApplicationArchitectureTests.cs`, etc.

### 16.2 — Convención de nombres de tests

**Decisión recomendada:** `R<N>_<Capa>_<Verbo>` siguiendo
la convención de specs en español del proyecto (las specs
están en español, los comentarios también). El nombre es
explícito y se lee como una frase.

Ejemplos:

- `R1_Domain_No_Depende_De_Application_Infra_UI_o_Tests`
- `R2_Application_Solo_Depende_De_Domain_Y_FluentValidation`
- `R3_Infrastructure_Depende_De_Application_Y_Domain_Pero_No_De_UI_o_Tests`
- `R4_UI_No_Depende_De_Infrastructure_Salvo_CompositionRoot`
- `R5_Ningun_Proyecto_De_src_Depende_De_Tests`

Nombres en C# no admiten guiones ni espacios; se usa
underscore. Es feo pero es la convención del proyecto.

**Alternativa inglesa (no recomendada):**
`R1_Domain_ShouldNotDependOn_ApplicationInfraUITests`.

### 16.3 — Versión de NetArchTest

**Decisión recomendada:** la última estable compatible
con .NET 8 al momento de implementar, fijada en CPM
(consultar la documentación oficial vía Context7 al
implementar). NetArchTest suele ser backwards-compatible
dentro de la línea 1.x, así que la elección es estable.

**Riesgo:** si la versión cambia mucho entre releases,
puede haber cambios de API. Mitigación: el spec está
escrito a nivel de reglas (qué se valida), no de API
exacta (cómo se escribe el test). El implementador
traduce las reglas a la API de la versión elegida.

### 16.4 — Tests.Unit vs Tests.Integration para los tests

**Decisión:** `Tests.Unit`. Ver §9.2. Razón: tests
rápidos, sin infraestructura, deben correr en el feedback
loop del developer.

### 16.5 — API de NetArchTest para el composition root

**Decisión propuesta:** marker attribute + filtro. Ver
§10.1. La API exacta de NetArchTest para excluir tipos
con un atributo concreto puede variar entre versiones;
el implementador confirma la API en
[documentación oficial de NetArchTest](https://github.com/BenMorris/NetArchTest).

Si la API no soporta filtros por atributo custom, la
alternativa es:

- Excluir por namespace (e.g.,
  `Types.InAssembly(ui).That().ResideInNamespace("ControlDeFinanzasIA.UI.Bootstrap")`).
- Excluir por nombre de tipo (e.g.,
  `Types.InAssembly(ui).That().DoNotHaveName("Bootstrap")`).

El implementador documenta la elección final en el
resumen de implementación.

### 16.6 — Mensajes de fallo

**Decisión:** usar los mensajes por defecto de
NetArchTest, complementados con un comentario XML doc en
cada test que cite la regla (`R-N`) y la sección de
`architecture.md` §4. No se personaliza el mensaje de
`Assert.True(...)` a menos que el de NetArchTest sea
claramente insuficiente.

### 16.7 — Naming de los archivos

**Decisión:** `ArchitectureTests.cs` (PascalCase + sufijo
`Tests`, convención xUnit). Si se subdivide por capa, los
archivos se llaman `DomainArchitectureTests.cs`,
`ApplicationArchitectureTests.cs`, etc.

---

## 17. Riesgos y mitigaciones

| Riesgo | Probabilidad | Impacto | Mitigación |
| --- | --- | --- | --- |
| NetArchTest mal configurado y no detecta violaciones reales. | Media | Alto | AC-3 y AC-4 exigen verificar que una violación intencional **rompe** el test. Si no rompe, F-007 no se considera completada. |
| Falsos positivos por código generado (Avalonia, XAML g.cs). | Baja | Medio | NetArchTest inspecciona assemblies, no archivos individuales. El código generado vive en el assembly `UI`, no afecta las reglas sobre `Domain`. Si aparece un falso positivo, se documenta y se excluye con `Types.That(...).ShouldNot()...`. |
| API de NetArchTest cambia entre versiones y rompe los tests. | Baja | Medio | Pinning exacto de versión en `Directory.Packages.props`. Si la API cambia, se actualizan los tests en una PR con su propia revisión. |
| Tiempo de ejecución de los tests se degrada con el crecimiento del proyecto. | Baja | Bajo | RNF-2 fija < 2 s. Si se acerca al límite, se evalúa mover a `Tests.Integration` o reducir el scope. |
| Falsa sensación de seguridad: tests pasan pero la arquitectura está mal. | Media | Alto | Las 5 reglas son **completas** sobre las 4 capas (RF-6). Cualquier combinación prohibida está cubierta. AC-3 y AC-4 verifican que las reglas detectan violaciones. |
| Atributo `[CompositionRoot]` no se implementa en F-008 y deja la excepción sin efecto. | Baja | Bajo | Cuando F-008 implemente el composition root, define el atributo y la convención. F-007 solo documenta el mecanismo. |
| Conflictos con `.editorconfig` (e.g., longitud de línea). | Baja | Bajo | Si las líneas de los tests son muy largas, usar `dotnet format` para ajustar. No relajar la regla global. |
| Tests arquitectónicos frágiles ante refactors del código. | Media | Bajo | Las reglas son a nivel de **assembly reference**, no a nivel de tipo. Refactors internos (renombrar clases, mover archivos) no afectan las reglas. Solo añadir/quitar `<ProjectReference>` o `<PackageReference>` afecta. |

---

## 18. Referencias

- `docs/architecture.md` §4 (grafo de dependencias) — fuente
  de verdad de las 5 reglas.
- `docs/architecture.md` §15 (testing) — menciona
  NetArchTest como opción para validar el grafo.
- `docs/roadmap.md` §Fase 1, F-007 dentro del desglose.
- `docs/roadmap.md` §6 (Trabajo en paralelo posible) —
  F-007 se puede hacer en paralelo con otras features de la
  fase 1 que no compartan código.
- `spec/constitution/tech-stack.md` — convenciones de
  paquetes, CPM.
- `spec/constitution/mission.md` — principios (Clean
  Architecture, separación de capas).
- `spec/phases/phase-01/features.md` — F-007 dentro del
  desglose de la fase 1.
- `spec/features/phase-01/F-001-skeleton-solution.done.md`
  — provee los 6 proyectos y la configuración de CPM.
- `spec/features/phase-01/F-006-contrato-interfaces-repo.done.md`
  — provee las 15 interfaces en `Application/Abstractions/`.
- `spec/features/phase-01/F-004-Abstracciones-clock-pathresolver.done.md`
  — convención de ubicación de interfaces (usada por R-2).
- `spec/features/phase-01/F-005-comuntypes-result-pagedresult.done.md`
  — patrón de `Result` non-generic (relevante para R-2 al
  verificar que Application no importa EF Core).
- `spec/README.md` — convención del directorio `spec/`.
- [NetArchTest — documentación oficial](https://github.com/BenMorris/NetArchTest)
  (a consultar vía Context7 al implementar, para confirmar
  la API exacta de la versión elegida).
- [NetArchTest.Rules en NuGet](https://www.nuget.org/packages/NetArchTest.Rules/).
