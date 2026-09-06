# Tareas — F-007: Validación arquitectónica con NetArchTest

> **Spec:** `spec/features/phase-01/F-007-validacion-arquitectonica.md`
> **Fuentes de verdad:** la spec, `docs/architecture.md` §4 (grafo
> de dependencias), `docs/architecture.md` §15 (testing —
> NetArchTest), `docs/roadmap.md` §Fase 1 (F-007 dentro del
> desglose), `spec/constitution/tech-stack.md` (CPM, paquetes),
> `spec/constitution/mission.md` (Clean Architecture),
> `spec/phases/phase-01/features.md` (F-007 dentro del
> desglose de la fase 1),
> `spec/features/phase-01/F-001-skeleton-solution.done.md`
> (provee los 6 proyectos y la configuración de CPM),
> `spec/features/phase-01/F-006-contrato-interfaces-repo.done.md`
> (provee las 15 interfaces en `Application/Abstractions/`).
>
> Cada tarea es implementable en menos de 2 horas. El proyecto
> debe seguir compilando al finalizar cada una. La numeración
> (T-001...) es local a esta feature. No se escribe código de
> aplicación en esta feature; solo se añade el paquete
> `NetArchTest.Rules` (si falta) y tests en `Tests.Unit`.

---

## Resumen de tareas

| ID | Tarea | Estimación | Depende de |
| --- | --- | --- | --- |
| T-001 | Verificar / añadir `NetArchTest.Rules` en CPM y `Tests.Unit.csproj` | XS | — |
| T-002 | Crear esqueleto de `ArchitectureTests.cs` (carpeta, clase, usings, namespace) | XS | T-001 |
| T-003 | Implementar R-1 — `Domain` no referencia a nadie (proyectos ni paquetes prohibidos) | S | T-002 |
| T-004 | Implementar R-2 — `Application` solo referencia a `Domain` y `FluentValidation` | S | T-002 |
| T-005 | Implementar R-3 — `Infrastructure` depende de `Application` y `Domain`; no de `UI` ni `Tests` | S | T-002 |
| T-006 | Implementar R-4 — `UI` depende de `Application` y `Domain`; no de `Infrastructure` (con mecánica de excepción para composition root) | S | T-002 |
| T-007 | Implementar R-5 — ningún proyecto de `src/` depende de `Tests.*`; `Tests.*` puede depender de los 4 proyectos | S | T-002 |
| T-008 | Verificación con violación intencional (AC-3 y AC-4 de la spec) | M | T-003 a T-007 |
| T-009 | Verificación final, format check y cierre de la feature | S | T-001 a T-008 |

**Total estimado:** 1–2 jornadas. T-008 es la más larga porque
requiere tocar archivos temporalmente, ejecutar tests, revertir
y documentar. T-001 y T-002 son triviales. T-003 a T-007 son
mecánicas (cada una sigue un patrón similar con la API de
NetArchTest).

---

## T-001 — Verificar / añadir `NetArchTest.Rules` en CPM y `Tests.Unit.csproj`

**Objetivo**
Confirmar que el paquete `NetArchTest.Rules` está disponible
para `Tests.Unit` y, si no, añadirlo correctamente vía CPM
(Central Package Management) para que los tests
arquitectónicos compilen.

**Descripción**
La spec de F-001 §5 lista `NetArchTest.Rules` como
pre-instalado en `Tests.Unit`. Verificarlo:

1. Abrir `Directory.Packages.props` y comprobar que hay un
   `<PackageVersion Include="NetArchTest.Rules" Version="..." />`.
   Si la versión no está fijada, añadirla (consultar la
   documentación oficial vía Context7 para confirmar la
   última estable compatible con .NET 8 al momento de
   implementar).
2. Abrir `tests/ControlDeFinanzasIA.Tests.Unit/ControlDeFinanzasIA.Tests.Unit.csproj`
   y comprobar que hay un
   `<PackageReference Include="NetArchTest.Rules" />` (sin
   `Version`, tomado de CPM).
3. Si falta cualquiera de los dos, añadirlo siguiendo las
   convenciones de CPM del proyecto (sin `Version` en el
   `.csproj`).
4. Ejecutar `dotnet restore` desde la raíz y verificar que
   el paquete se restaura sin errores.

**Dependencias**
- F-001 completada (la solución, los proyectos y la
  configuración de CPM ya están listos).
- F-006 completada (no es prerrequisito estricto para T-001,
  pero F-007 se implementa en el contexto de F-006 ya
  terminado).

**Archivos / proyectos afectados**
- `Directory.Packages.props` (modificado solo si falta el
  `PackageVersion`).
- `tests/ControlDeFinanzasIA.Tests.Unit/ControlDeFinanzasIA.Tests.Unit.csproj`
  (modificado solo si falta el `PackageReference`).
- `tests/ControlDeFinanzasIA.Tests.Unit/obj/project.assets.json`
  (regenerado por `dotnet restore`).

**Criterios de finalización**
- `Directory.Packages.props` contiene un
  `<PackageVersion Include="NetArchTest.Rules" Version="X.Y.Z" />`
  con versión exacta (sin rangos).
- `Tests.Unit.csproj` contiene un
  `<PackageReference Include="NetArchTest.Rules" />` sin
  atributo `Version`.
- `dotnet restore` desde la raíz pasa sin errores.
- `dotnet list tests/ControlDeFinanzasIA.Tests.Unit package --include-transitive`
  muestra `NetArchTest.Rules` con la versión esperada y sus
  dependencias transitivas (`Mono.Cecil`,
  `System.Reflection.Metadata`, etc.).
- Si ya estaba todo, no se hace ningún cambio (no-op).

---

## T-002 — Crear esqueleto de `ArchitectureTests.cs`

**Objetivo**
Crear la carpeta `Architecture/` y el archivo
`ArchitectureTests.cs` con la estructura básica (clase,
`using` directives, namespace, comentarios XML doc) pero
sin tests implementados. Sirve de "punto de partida" para
las tareas T-003 a T-007.

**Descripción**
Crear:

- `tests/ControlDeFinanzasIA.Tests.Unit/Architecture/` —
  carpeta nueva (vacía, los archivos vienen a continuación).
- `tests/ControlDeFinanzasIA.Tests.Unit/Architecture/ArchitectureTests.cs`
  con la siguiente estructura (NO incluir los tests todavía,
  solo la clase y los comentarios que motivan el archivo):

  ```text
  using NetArchTest.Rules;
  using Xunit;

  namespace ControlDeFinanzasIA.Tests.Unit.Architecture;

  /// <summary>
  /// Tests arquitectónicos que validan el grafo de dependencias
  /// entre las 4 capas del proyecto. Las reglas verificadas aquí
  /// están definidas en <c>docs/architecture.md</c> §4 y
  /// especificadas en
  /// <c>spec/features/phase-01/F-007-validacion-arquitectonica.md</c>.
  /// </summary>
  /// <remarks>
  /// Cualquier violación debe detectarse en este test antes de
  /// llegar a code review o a producción. Si una regla no se
  /// valida aquí, es un agujero en la red de seguridad del
  /// proyecto.
  /// </remarks>
  public sealed class ArchitectureTests
  {
      // Los tests se añaden en T-003 a T-007.
  }
  ```

  Notas:
  - File-scoped namespace.
  - `using` directives al tope (NUNCA después del namespace).
  - Clase `sealed` (no se hereda de esta; es el último nivel
    de la jerarquía de tests).
  - XML doc en la clase citando las dos referencias clave
    (spec y `architecture.md` §4).
  - Comentario `// Los tests se añaden en T-003 a T-007.`
    para que un futuro lector sepa que la clase está
    intencionalmente vacía al final de T-002.
  - **NO** añadir tests todavía. Solo el esqueleto.

**Dependencias**
- T-001 (el paquete `NetArchTest.Rules` debe estar
  disponible para que el `using NetArchTest.Rules;`
  compile).

**Archivos / proyectos afectados**
- `tests/ControlDeFinanzasIA.Tests.Unit/Architecture/` (nueva
  carpeta).
- `tests/ControlDeFinanzasIA.Tests.Unit/Architecture/ArchitectureTests.cs`
  (nuevo).

**Criterios de finalización**
- La carpeta `tests/ControlDeFinanzasIA.Tests.Unit/Architecture/`
  existe.
- El archivo `ArchitectureTests.cs` existe y compila.
- `dotnet build` desde la raíz pasa con 0 warnings, 0
  errors.
- `dotnet test` desde la raíz corre (los tests existentes
  siguen pasando; la nueva clase no tiene tests todavía y
  xUnit no reporta error por eso).
- `ArchitectureTests` tiene la firma `public sealed class
  ArchitectureTests` en el namespace
  `ControlDeFinanzasIA.Tests.Unit.Architecture`.
- Los `using` están al tope del archivo.
- El XML doc de la clase cita
  `docs/architecture.md §4` y
  `spec/features/phase-01/F-007-validacion-arquitectonica.md`.

---

## T-003 — Implementar R-1: `Domain` no referencia a nadie

**Objetivo**
Cubrir la regla R-1 de la spec (§5 y §7 RF-1): ningún tipo del
assembly `ControlDeFinanzasIA.Domain` debe tener referencias a
los assemblies de los otros proyectos del repo, ni a los
paquetes prohibidos (`Microsoft.EntityFrameworkCore`,
`Avalonia.*`, `System.Net.Http`).

**Descripción**
Añadir dos tests `[Fact]` a la clase `ArchitectureTests`
(creada en T-002). Los nombres siguen la convención
`R<N>_<Capa>_<Verbo>` (§16.2 de la spec) en español:

- `R1_Domain_No_Referencia_A_Application_Infra_UI_o_Tests`.
- `R1_Domain_No_Depende_De_Paquetes_Prohibidos_EfCore_Avalonia_HttpClient`.

Contenido esperado (descripción de la lógica, no se incluye
código completo — la implementación se hace en la fase de
aplicación):

- Cargar los assemblies de los 5 proyectos objetivo
  (`Domain`, `Application`, `Infrastructure`, `UI`,
  `Tests.Unit`, `Tests.Integration`).
- Para el primer test:
  `Types.InAssembly(domainAssembly).ShouldNot().HaveDependencyOnAny(applicationAssembly, infrastructureAssembly, uiAssembly, testsUnitAssembly, testsIntegrationAssembly).`
- Para el segundo test:
  `Types.InAssembly(domainAssembly).ShouldNot().HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Avalonia.*", "System.Net.Http").`
  (la API exacta de NetArchTest para filtrar por nombre de
  assembly se confirma en su documentación oficial al
  implementar; el spec describe la **regla**, no la API
  exacta).
- Cada test incluye un comentario XML doc que cita la regla
  R-1 de la spec y el párrafo de `architecture.md` §4 que
  la motiva.

**Dependencias**
- T-002 (la clase `ArchitectureTests` debe existir).

**Archivos / proyectos afectados**
- `tests/ControlDeFinanzasIA.Tests.Unit/Architecture/ArchitectureTests.cs`
  (modificado: se añaden los 2 tests).

**Criterios de finalización**
- Los 2 tests existen en la clase `ArchitectureTests`.
- Cada test es `[Fact]` con un nombre siguiendo la
  convención `R1_...`.
- Cada test tiene un comentario XML doc citando R-1 y
  `architecture.md` §4.
- `dotnet build` desde la raíz pasa con 0 warnings, 0
  errors.
- `dotnet test` desde la raíz: los 2 tests nuevos pasan
  sobre el estado actual del repo (las reglas se
  satisfacen: `Domain` no tiene esas dependencias).
- `Domain` no tiene `<PackageReference>` ni
  `<ProjectReference>` a los assemblies prohibidos
  (verificable con `dotnet list src/ControlDeFinanzasIA.Domain reference`
  y `dotnet list package`, que deben estar vacíos o solo
  listar lo que el `<ImplicitUsings>` de .NET 8 añada).

---

## T-004 — Implementar R-2: `Application` solo referencia a `Domain` y `FluentValidation`

**Objetivo**
Cubrir la regla R-2 de la spec: `Application` no debe
referenciar a `Infrastructure`, `UI`, ni a `Tests.*`. Y no
debe depender de los paquetes prohibidos (EF Core, Avalonia,
HttpClient). `Domain` y `FluentValidation` son las únicas
dependencias permitidas.

**Descripción**
Añadir dos tests `[Fact]` a `ArchitectureTests`:

- `R2_Application_No_Referencia_A_Infra_UI_o_Tests` (regla
  sobre project references).
- `R2_Application_No_Depende_De_Paquetes_Prohibidos_EfCore_Avalonia_HttpClient`
  (regla sobre packages).

Contenido esperado:

- Para el primer test:
  `Types.InAssembly(applicationAssembly).ShouldNot().HaveDependencyOnAny(infrastructureAssembly, uiAssembly, testsUnitAssembly, testsIntegrationAssembly).`
- Para el segundo test:
  `Types.InAssembly(applicationAssembly).ShouldNot().HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Avalonia.*", "System.Net.Http").`
- Cada test incluye XML doc citando R-2 y `architecture.md`
  §4.

**Dependencias**
- T-002 (la clase debe existir).
- F-006 completada (las 15 interfaces en
  `Application/Abstractions/` ya están; sin ellas, R-2
  validaría un assembly casi vacío y la regla no sería
  significativa).

**Archivos / proyectos afectados**
- `tests/ControlDeFinanzasIA.Tests.Unit/Architecture/ArchitectureTests.cs`
  (modificado: se añaden los 2 tests).

**Criterios de finalización**
- Los 2 tests existen en `ArchitectureTests`.
- Cada test es `[Fact]` con nombre siguiendo la convención
  `R2_...`.
- XML doc cita R-2 y `architecture.md` §4.
- `dotnet build`: 0 warnings, 0 errors.
- `dotnet test`: los 2 tests pasan sobre el estado actual.
- `Application` no tiene `<ProjectReference>` a
  `Infrastructure`, `UI`, ni `Tests.*` (verificable con
  `dotnet list src/ControlDeFinanzasIA.Application reference`).
- `Application` no tiene `<PackageReference>` a EF Core,
  Avalonia, ni HttpClient (verificable con `dotnet list
  src/ControlDeFinanzasIA.Application package`, que debe
  seguir mostrando solo `FluentValidation`).

---

## T-005 — Implementar R-3: `Infrastructure` depende de `Application` y `Domain`; no de `UI` ni `Tests`

**Objetivo**
Cubrir la regla R-3: `Infrastructure` debe referenciar a
`Application` y `Domain` (asserción positiva, porque sin
esas dependencias no podría implementar las interfaces de
los repos). Y NO debe referenciar a `UI` ni a `Tests.*`.

**Descripción**
Añadir dos tests `[Fact]` a `ArchitectureTests`:

- `R3_Infrastructure_Depende_De_Application_Y_Domain`
  (asserción positiva).
- `R3_Infrastructure_No_Referencia_A_UI_o_Tests` (asserción
  negativa).

Contenido esperado:

- Para el primer test:
  `Types.InAssembly(infrastructureAssembly).Should().HaveDependencyOn(applicationAssembly).And().HaveDependencyOn(domainAssembly)`
  (o la API equivalente que verifique las dos
  dependencias a la vez).
- Para el segundo test:
  `Types.InAssembly(infrastructureAssembly).ShouldNot().HaveDependencyOnAny(uiAssembly, testsUnitAssembly, testsIntegrationAssembly).`
- Cada test incluye XML doc citando R-3 y `architecture.md`
  §4.

**Dependencias**
- T-002 (la clase debe existir).

**Archivos / proyectos afectados**
- `tests/ControlDeFinanzasIA.Tests.Unit/Architecture/ArchitectureTests.cs`
  (modificado: se añaden los 2 tests).

**Criterios de finalización**
- Los 2 tests existen en `ArchitectureTests`.
- Cada test es `[Fact]` con nombre siguiendo la convención
  `R3_...`.
- XML doc cita R-3 y `architecture.md` §4.
- `dotnet build`: 0 warnings, 0 errors.
- `dotnet test`: los 2 tests pasan sobre el estado actual.
- `Infrastructure` tiene `<ProjectReference>` a
  `Application` y `Domain` (verificable con `dotnet list
  src/ControlDeFinanzasIA.Infrastructure reference`, que
  debe mostrar `Application` y, transitivamente, `Domain`).
- `Infrastructure` NO tiene `<ProjectReference>` a `UI` ni
  a `Tests.*` (la verificación anterior cubre `UI`; añadir
  `grep` o `dotnet list` para confirmar).

---

## T-006 — Implementar R-4: `UI` depende de `Application` y `Domain`; no de `Infrastructure` (con mecánica de excepción para composition root)

**Objetivo**
Cubrir la regla R-4: `UI` debe referenciar a `Application` y
`Domain`. NO debe referenciar a `Infrastructure`, con la
**excepción documentada** del composition root (que se
implementará en F-008). F-007 modela la mecánica de la
excepción con un atributo marker, pero **NO** implementa
el atributo ni la clase del composition root (esos llegan
en F-008).

**Descripción**
Añadir tres tests `[Fact]` a `ArchitectureTests`:

- `R4_UI_Depende_De_Application_Y_Domain` (asserción
  positiva).
- `R4_UI_No_Referencia_A_Infrastructure_Salvo_CompositionRoot`
  (asserción negativa con la excepción).
- `R4_UI_No_Depende_De_Paquetes_Prohibidos_EfCore_Avalonia_HttpClient`
  (asserción negativa sobre packages; UI no debe importar
  EF Core directamente, el acceso a DB va por la interfaz
  en `Application`).

Contenido esperado:

- Para el primer test:
  `Types.InAssembly(uiAssembly).Should().HaveDependencyOn(applicationAssembly).And().HaveDependencyOn(domainAssembly).`
- Para el segundo test:
  `Types.InAssembly(uiAssembly).That().DoNotHaveName("Bootstrap").ShouldNot().HaveDependencyOn(infrastructureAssembly).`
  (o la API equivalente que excluya un tipo con nombre
  `Bootstrap` — convención propuesta en §10.1 y §16.5 de
  la spec). Como F-008 no existe, el tipo `Bootstrap` no
  existe todavía; el test pasa porque UI no tiene
  `<ProjectReference>` a `Infrastructure` (verificado en
  F-001). La exclusión se incluye en el test aunque hoy
  sea "vacía" para que cuando F-008 añada el composition
  root, el test siga pasando.
- Para el tercer test:
  `Types.InAssembly(uiAssembly).ShouldNot().HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Avalonia.*", "System.Net.Http").`
- Cada test incluye XML doc citando R-4, el párrafo de
  `architecture.md` §4 que la motiva, y (en el segundo
  test) una nota sobre la mecánica de excepción para que
  un futuro implementador de F-008 entienda por qué el
  filtro existe.

**Dependencias**
- T-002 (la clase debe existir).

**Archivos / proyectos afectados**
- `tests/ControlDeFinanzasIA.Tests.Unit/Architecture/ArchitectureTests.cs`
  (modificado: se añaden los 3 tests).

**Criterios de finalización**
- Los 3 tests existen en `ArchitectureTests`.
- Cada test es `[Fact]` con nombre siguiendo la convención
  `R4_...`.
- XML doc cita R-4 y `architecture.md` §4. El segundo
  test además comenta la mecánica de la excepción
  (composition root).
- `dotnet build`: 0 warnings, 0 errors.
- `dotnet test`: los 3 tests pasan sobre el estado actual.
- `UI` tiene `<ProjectReference>` a `Application` y
  `Domain`, NO a `Infrastructure` (verificable con
  `dotnet list src/ControlDeFinanzasIA.UI reference`).
- `UI` no tiene `<PackageReference>` a EF Core, Avalonia
  (Avalonia sí está como dependencia del UI, ver nota
  abajo), ni HttpClient.

**Nota sobre Avalonia como package de UI:** Avalonia está
referenciada por UI (es el framework de UI). La regla
R-4 sobre packages prohibidos aplica a las **dependencias
del proyecto de UI hacia otros proyectos**, no a las
dependencias del propio UI hacia su framework. Por tanto,
la asserción sobre Avalonia en R-4 es trivialmente cierta
porque Avalonia es la dependencia legítima de UI. Si la
implementación de NetArchTest causa problemas al excluir
el package Avalonia de R-4, ajustar la asserción a
"`UI` no debe importar EF Core ni HttpClient
directamente" (que es la intención de la regla). Esta
decisión se documenta en el comentario del test.

---

## T-007 — Implementar R-5: ningún proyecto de `src/` depende de `Tests.*`; `Tests.*` puede depender de los 4 proyectos

**Objetivo**
Cubrir la regla R-5: la dirección de las dependencias es
`Tests → src/`, nunca `src/ → Tests`. Los `Tests.*` PUEDEN
referenciar a cualquier proyecto de `src/` (asserción
positiva, redundante con R-1 a R-4 pero incluida por
simetría y para capturar violaciones del tipo "alguien
borró una `<ProjectReference>` por error"). Los proyectos
de `src/` NO deben referenciar a `Tests.*` (asserción
negativa, la que captura la violación típica: "alguien
importó un test helper en Domain").

**Descripción**
Añadir dos tests `[Fact]` a `ArchitectureTests`:

- `R5_Ningun_Proyecto_De_src_Depende_De_Tests` (asserción
  negativa — la que captura la violación típica).
- `R5_Tests_Pueden_Depender_De_Todos_Los_Proyectos_De_src`
  (asserción positiva por simetría).

Contenido esperado:

- Para el primer test (aplica a cada uno de los 4
  proyectos de `src/`):
  ```
  Types.InAssembly(domainAssembly).ShouldNot().HaveDependencyOn(testsUnitAssembly);
  Types.InAssembly(applicationAssembly).ShouldNot().HaveDependencyOn(testsUnitAssembly);
  Types.InAssembly(infrastructureAssembly).ShouldNot().HaveDependencyOn(testsUnitAssembly);
  Types.InAssembly(uiAssembly).ShouldNot().HaveDependencyOn(testsUnitAssembly);
  // (y lo mismo para testsIntegrationAssembly)
  ```
  Esto se puede expresar en un único test que ejecuta las
  8 asserciones (4 proyectos × 2 assemblies de tests), o
  en un test parametrizado con `[Theory]` y
  `[InlineData]`. La spec §7 RF-1 dice "[Fact] (no
  [Theory])" para mantener 1 método por regla, pero
  admite hasta 10 tests; usar `[Theory]` aquí es
  razonable por la simetría de las 8 asserciones. Si se
  elige `[Theory]`, documentar la decisión en el
  comentario del test.
- Para el segundo test (asserción positiva):
  ```
  Types.InAssembly(testsUnitAssembly).Should().HaveDependencyOn(domainAssembly)
      .And().HaveDependencyOn(applicationAssembly);
  // (Tests.Integration puede depender de los 4
  // proyectos, así que se verifica también.)
  ```
- Cada test incluye XML doc citando R-5 y `architecture.md`
  §4.

**Dependencias**
- T-002 (la clase debe existir).

**Archivos / proyectos afectados**
- `tests/ControlDeFinanzasIA.Tests.Unit/Architecture/ArchitectureTests.cs`
  (modificado: se añaden los 2 tests).

**Criterios de finalización**
- Los 2 tests existen en `ArchitectureTests`.
- Cada test es `[Fact]` (preferido) o `[Theory]` con
  `[InlineData]` (si se eligió parametrizar).
- XML doc cita R-5 y `architecture.md` §4.
- `dotnet build`: 0 warnings, 0 errors.
- `dotnet test`: los 2 tests pasan sobre el estado actual.
- `Domain`, `Application`, `Infrastructure` y `UI` no
  tienen `<ProjectReference>` a `Tests.Unit` ni
  `Tests.Integration` (verificable con `dotnet list
  reference` en cada uno; debe estar vacío).
- `Tests.Unit` y `Tests.Integration` tienen
  `<ProjectReference>` a los proyectos de `src/` que
  necesitan (verificable con `dotnet list reference`).

---

## T-008 — Verificación con violación intencional (AC-3 y AC-4 de la spec)

**Objetivo**
Demostrar empíricamente que las 5 reglas detectan
violaciones reales. Sin esta verificación, los tests
podrían estar mal escritos y dar falsos positivos
(creen detectar violaciones cuando no lo hacen, o no
detecten violaciones que sí ocurren). AC-3 y AC-4 de la
spec son obligatorios: añadir violaciones temporales,
confirmar que los tests fallan, revertir y confirmar que
vuelven a pasar.

**Descripción**
Esta tarea es meta-verificación. NO introduce código
nuevo; **toca temporalmente** archivos del proyecto
solo para forzar violaciones, ejecuta los tests, y
revierte.

Sub-tareas (cada una es un ciclo violación → test falla →
revert → test pasa):

### T-008.1 — Violación de project reference de `Domain`

1. En `src/ControlDeFinanzasIA.Domain/ValueObjects/Money.cs`
   (o cualquier archivo de `Domain`), añadir temporalmente
   una línea `using ControlDeFinanzasIA.Application;` al
   tope (después de los `using` existentes).
2. Ejecutar `dotnet test --filter "FullyQualifiedName~R1_Domain_No_Referencia"`.
3. Confirmar que el test R-1 falla y que el mensaje de
   fallo indica el tipo o la dependencia prohibida.
4. Revertir el cambio (`git checkout -- Money.cs` o quitar
   la línea manualmente).
5. Volver a ejecutar el test y confirmar que pasa.
6. Documentar el resultado en un comentario del test
   R-1 o en el commit / PR que cierre F-007.

### T-008.2 — Violación de package reference de `Domain`

1. En `src/ControlDeFinanzasIA.Domain/ControlDeFinanzasIA.Domain.csproj`,
   añadir temporalmente
   `<PackageReference Include="Microsoft.EntityFrameworkCore" />`
   (sin versión, tomado de CPM).
2. En un archivo de `Domain` (e.g., `Money.cs`), añadir
   `using Microsoft.EntityFrameworkCore;`.
3. Ejecutar `dotnet test --filter "FullyQualifiedName~R1_Domain_No_Depende_De_Paquetes"`.
4. Confirmar que el test R-1 (paquetes prohibidos) falla.
5. Revertir ambos cambios (`.csproj` y `.cs`).
6. Volver a ejecutar y confirmar que pasa.
7. Documentar el resultado.

### T-008.3 — Violación de `Application` → `Infrastructure`

1. Crear un archivo temporal
   `src/ControlDeFinanzasIA.Application/ArchitectureProbe.cs`
   con un `using ControlDeFinanzasIA.Infrastructure;` y
   una referencia simbólica a un tipo de `Infrastructure`
   (e.g., una clase pública que ya exista o un comentario
   para forzar la dependencia).
2. Ejecutar
   `dotnet test --filter "FullyQualifiedName~R2_Application_No_Referencia"`.
3. Confirmar que el test R-2 falla.
4. Revertir (borrar `ArchitectureProbe.cs`).
5. Volver a ejecutar y confirmar que pasa.
6. Documentar el resultado.

### T-008.4 — Violación de `UI` → `Infrastructure`

1. En `src/ControlDeFinanzasIA.UI/App.axaml.cs` (o
   `Program.cs`), añadir temporalmente
   `using ControlDeFinanzasIA.Infrastructure;` y
   `<PackageReference Include="ControlDeFinanzasIA.Infrastructure" />`
   en el `.csproj` de UI.
2. Ejecutar
   `dotnet test --filter "FullyQualifiedName~R4_UI_No_Referencia"`.
3. Confirmar que el test R-4 falla y que el mensaje
   identifica la dependencia.
4. Revertir ambos cambios.
5. Confirmar que el test pasa.
6. Documentar el resultado.

### T-008.5 — Resumen

Al final de T-008, el resumen de verificación (en el
comentario del test, en el commit, o en la PR) incluye:

- Las 4 violaciones probadas.
- El test que detectó cada una.
- El mensaje de fallo (capturado en cada corrida).
- La confirmación de que revertir hace pasar el test.

**Dependencias**
- T-003 a T-007 (los 5 reglas deben estar implementadas
  antes de poder probarlas).

**Archivos / proyectos afectados**
- `src/ControlDeFinanzasIA.Domain/ValueObjects/Money.cs`
  (tocado y revertido en T-008.1 y T-008.2).
- `src/ControlDeFinanzasIA.Domain/ControlDeFinanzasIA.Domain.csproj`
  (tocado y revertido en T-008.2).
- `src/ControlDeFinanzasIA.Application/ArchitectureProbe.cs`
  (creado y borrado en T-008.3).
- `src/ControlDeFinanzasIA.UI/App.axaml.cs` (tocado y
  revertido en T-008.4).
- `src/ControlDeFinanzasIA.UI/ControlDeFinanzasIA.UI.csproj`
  (tocado y revertido en T-008.4).

**Criterios de finalización**
- T-008.1 ejecutado: el test R-1 (project references) falló
  con la violación y volvió a pasar al revertir.
- T-008.2 ejecutado: el test R-1 (packages prohibidos)
  falló con la violación y volvió a pasar al revertir.
- T-008.3 ejecutado: el test R-2 falló con la violación y
  volvió a pasar al revertir.
- T-008.4 ejecutado: el test R-4 falló con la violación y
  volvió a pasar al revertir.
- El estado del repo al final de T-008 es **idéntico** al
  estado de entrada (todos los cambios revertidos;
  `git status` limpio o solo con los archivos legítimos de
  T-001 a T-007).
- `dotnet build` y `dotnet test` pasan en limpio.
- Resumen documentado (en comentario de test, commit o
  PR) con los resultados de las 4 violaciones.

---

## T-009 — Verificación final, format check y cierre de la feature

**Objetivo**
Ejecutar la lista de comprobaciones finales de la spec
(§14 AC-1 a AC-8), validar que F-007 cumple todos los
criterios de aceptación, dejar el repo en estado de
"F-007 terminada" y marcar la feature como completada en
el desglose de fase.

**Descripción**
Esta tarea **no introduce código nuevo** (los tests
están en T-003 a T-007 y la verificación con violación
está en T-008). Consiste en ejecutar los pasos de
verificación y ejecutar las acciones de cierre.

### Pasos de verificación

1. **Compilación:**
   `dotnet build "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Debug`
   y `-c Release` — ambos con 0 warnings, 0 errors.

2. **Format check:**
   `dotnet format style --verify-no-changes` — exit 0. Si
   propone cambios, aplicarlos con `dotnet format style`
   y volver a verificar.

3. **Tests completos:**
   `dotnet test` desde la raíz. Resultado esperado:
   - `Tests.Unit`: 100 + N tests pasando (100 originales
     + N nuevos de F-007, donde N está entre 5 y 10 según
     las decisiones de §16.1 y §7 RF-1).
   - `Tests.Integration`: 1 skipped (placeholder).
   - 0 failures, 0 errors.

4. **Compatibilidad arquitectónica** (los tests ya lo
   validan, pero se verifica a nivel de `.csproj` para
   tener doble seguridad):
   - `dotnet list src/.../Domain reference` — debe estar
     vacío.
   - `dotnet list src/.../Application reference` — debe
     mostrar solo `Domain`.
   - `dotnet list src/.../Infrastructure reference` —
     debe mostrar `Application` (y, transitivamente,
     `Domain`).
   - `dotnet list src/.../UI reference` — debe mostrar
     `Application` y `Domain`; **NO** debe mostrar
     `Infrastructure`.
   - `dotnet list src/.../Domain package` — debe estar
     vacío (o solo listar transitivos de .NET 8).
   - `dotnet list src/.../Application package` — debe
     mostrar solo `FluentValidation`.

5. **Layout:** los archivos de F-007 están en
   `tests/ControlDeFinanzasIA.Tests.Unit/Architecture/`
   (al menos `ArchitectureTests.cs`). Verificable con
   `Get-ChildItem` o `ls`.

6. **CI workflow:** el archivo
   `.github/workflows/build.yml` ya ejecuta
   `dotnet test` (verificado en F-001). Confirmar que
   no requiere cambios y que los nuevos tests
   arquitectónicos se ejecutan en CI.

### Pasos de limpieza

7. **Verificar que no hay placeholders o archivos
   temporales:** los archivos creados en T-008
   (`ArchitectureProbe.cs`, modificaciones a `Money.cs`,
   `App.axaml.cs`, etc.) están todos revertidos.
   `git status` (o equivalente) muestra solo los archivos
   legítimos de T-001 a T-007.

8. **Actualizar el desglose de fase:** en
   `spec/phases/phase-01/features.md`:
   - Cambiar la fila 007 de la tabla resumen de
     "Pendiente" a "✅ COMPLETADA" (mismo formato que
     F-001, F-002, F-003, F-004, F-005, F-006).
   - Cambiar el header de la sección F-007 de
     `## F-007 — Validación arquitectónica con NetArchTest`
     a
     `## F-007 — Validación arquitectónica con NetArchTest — ✅ COMPLETADA`.

9. **Renombrar la spec:** mover
   `spec/features/phase-01/F-007-validacion-arquitectonica.md`
   a
   `spec/features/phase-01/F-007-validacion-arquitectonica.done.md`
   (convención del proyecto, ver F-001, F-002, F-003,
   F-004, F-005, F-006).

### Resumen de verificación de AC

| AC | Criterio | Estado |
| --- | --- | --- |
| AC-1 | Tests implementados (5 reglas, 5-10 tests) | T-003 a T-007 |
| AC-2 | Compilación y ejecución (0 warnings, tests pasan, < 2s) | Paso 1, 3 |
| AC-3 | Violación intencional detectada (project references) | T-008.1, T-008.3, T-008.4 |
| AC-4 | Violación intencional detectada (packages prohibidos) | T-008.2 |
| AC-5 | Compatibilidad arquitectónica (project references, packages) | Paso 4 |
| AC-6 | Convenciones del repo (file-scoped namespace, XML doc) | T-002 a T-007 |
| AC-7 | CI integration (workflow existente ejecuta los tests) | Paso 6 |
| AC-8 | Documentación y trazabilidad (features.md, rename) | Pasos 8, 9 |

**Dependencias**
- T-001 a T-008 (todas las anteriores).

**Archivos / proyectos afectados**
- `spec/phases/phase-01/features.md` (modificado: F-007
  marcada como completada).
- `spec/features/phase-01/F-007-validacion-arquitectonica.md`
  (renombrado a `.done.md`).

**Criterios de finalización**
- `dotnet build -c Debug` y `-c Release`: 0 warnings, 0
  errors.
- `dotnet format style --verify-no-changes`: exit 0.
- `dotnet test`: todos los tests pasan (100 originales
  de Tests.Unit + N nuevos de F-007, 0 failures).
- Tiempo de ejecución de los tests arquitectónicos:
  < 2 segundos (verificable con `dotnet test --logger
  "console;verbosity=normal"` y observando la salida
  por test).
- Layout OK: `tests/.../Architecture/ArchitectureTests.cs`
  existe; no hay archivos temporales o placeholders.
- `git status` muestra solo los archivos legítimos de
  T-001 a T-007.
- `spec/phases/phase-01/features.md` actualizado con
  F-007 marcada como completada (tabla resumen y
  header de sección).
- Spec renombrada a `.done.md`.

---

## Orden recomendado y trabajo en paralelo

**Orden secuencial estricto:** T-001 → T-002 → (T-003, T-004,
T-005, T-006, T-007 en paralelo posible) → T-008 → T-009.

**T-003 a T-007 son independientes entre sí** porque cada
una añade métodos distintos a la misma clase
`ArchitectureTests` sin solaparse. Si hay 2 personas,
pueden repartirse las 5 reglas (e.g., persona A hace
R-1, R-2, R-3; persona B hace R-4, R-5). El merge es
mecánico porque cada regla es un test separado.

**T-008 es secuencial a T-003 a T-007** porque verifica
que los tests de T-003 a T-007 funcionan. T-008 NO
debe paralelizarse con T-003 a T-007.

**T-009 es secuencial a T-008** porque necesita el
estado limpio para verificar.

## Riesgos de la fase de tareas

| Riesgo | Probabilidad | Impacto | Mitigación |
| --- | --- | --- | --- |
| NetArchTest mal configurado y que no detecte violaciones reales. | Media | Alto | T-008 (verificación con violación intencional) es la red de seguridad. Si los tests no fallan con la violación, F-007 no se cierra. |
| Falsos positivos por código generado (Avalonia, XAML g.cs). | Baja | Medio | NetArchTest inspecciona assemblies, no archivos individuales. Si aparece un falso positivo, se documenta y se excluye con `Types.That(...).ShouldNot()...`. |
| API de NetArchTest cambia entre versiones y rompe los tests. | Baja | Medio | Pinning exacto de versión en `Directory.Packages.props`. Si la API cambia, se actualizan los tests en una PR con su propia revisión. |
| `Tests.Unit` no compila por las referencias a assemblies o por el uso de `Mono.Cecil` indirecto. | Baja | Alto | T-001 verifica que el paquete y sus dependencias están bien. Si la compilación falla, ajustar las versiones en CPM. |
| La asserción de R-4 sobre Avalonia da falso positivo (Avalonia es dependencia legítima de UI). | Media | Bajo | T-006 nota explícita: la asserción se ajusta a "UI no debe importar EF Core ni HttpClient directamente" (Avalonia es legítima). Se documenta en el comentario del test. |
| El composition root (F-008) implementa un nombre distinto a `Bootstrap`, rompiendo la convención. | Baja | Bajo | La convención `Bootstrap` se documenta en §10.1 de la spec. Si F-008 usa otro nombre, se ajusta el filtro del test R-4 (un solo cambio en una línea). |
| Los tests arquitectónicos son lentos porque inspeccionan assemblies grandes. | Baja | Bajo | RNF-2 de la spec fija < 2 segundos. Si se acerca al límite, se evalúa mover a `Tests.Integration`. |
| Conflictos en merge cuando T-003 a T-007 se hacen en paralelo (varias personas modificando el mismo archivo). | Baja | Bajo | El merge es trivial: cada test es un método separado. No hay solapamiento de líneas. |

## Referencias

- `spec/features/phase-01/F-007-validacion-arquitectonica.md`
  — la spec completa de F-007. Este tasks file es su
  descomposición técnica.
- `docs/architecture.md` §4 (grafo de dependencias) —
  fuente de verdad de las 5 reglas.
- `docs/architecture.md` §15 (testing) — menciona
  NetArchTest como opción.
- `docs/roadmap.md` §Fase 1, F-007 dentro del desglose.
- `docs/roadmap.md` §6 (Trabajo en paralelo posible) —
  F-007 se puede hacer en paralelo con otras features de
  la fase 1.
- `spec/constitution/tech-stack.md` — CPM, convenciones de
  paquetes.
- `spec/constitution/mission.md` — principios (Clean
  Architecture, separación de capas).
- `spec/phases/phase-01/features.md` — F-007 dentro del
  desglose de la fase 1.
- `spec/features/phase-01/F-001-skeleton-solution.done.md`
  — provee los 6 proyectos y la configuración de CPM.
- `spec/features/phase-01/F-006-contrato-interfaces-repo.done.md`
  — provee las 15 interfaces en `Application/Abstractions/`.
- `spec/features/phase-01/F-004-Abstracciones-clock-pathresolver.done.md`
  — convención de ubicación de interfaces.
- `spec/features/phase-01/F-005-comuntypes-result-pagedresult.done.md`
  — patrón de `Result` non-generic (relevante para R-2).
- `spec/README.md` — convención del directorio `spec/`.
- [NetArchTest — documentación oficial](https://github.com/BenMorris/NetArchTest)
  (a consultar vía Context7 al implementar, para
  confirmar la API exacta de la versión elegida).
- [NetArchTest.Rules en NuGet](https://www.nuget.org/packages/NetArchTest.Rules/).
