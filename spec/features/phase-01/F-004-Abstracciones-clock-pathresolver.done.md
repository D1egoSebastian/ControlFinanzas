# Specification — F-004: Abstracciones IClock e IPathResolver

> **Fase:** 1 — Esqueleto de arquitectura y contratos base
> **Feature:** F-004 (Phase 1, local a esta fase)
> **Estado:** Pendiente de implementación
> **Spec ID:** SPEC-F004
>
> **Nota de numeración:** este `F-004` es el de la **Fase 1**
> (`spec/phases/phase-01/features.md`). Los IDs de feature son
> locales a cada fase; el folder los desambigua. Ver `spec/README.md`
> para la convención completa.
>
> Fuentes de verdad: `docs/vision.md`, `docs/architecture.md`,
> `docs/roadmap.md`, `spec/constitution/*`,
> `spec/phases/phase-01/features.md` (F-004 dentro del desglose de
> la fase 1).

---

## 1. Resumen

F-004 introduce dos **interfaces de "seam"** que toda la aplicación
usa para acceder a la hora actual y a la ruta del archivo de
datos, sin acoplarse a `DateTime.Now` ni a `Environment`. Introduce
también **implementaciones de prueba** (`FakeClock`,
`FakePathResolver`) que permiten a los tests controlar tiempo y
rutas de forma explícita.

| Miembro | Tipo | Carpeta |
| --- | --- | --- |
| `IClock` | interface | `src/ControlDeFinanzasIA.Application/Abstractions/IClock.cs` |
| `IPathResolver` | interface | `src/ControlDeFinanzasIA.Application/Abstractions/IPathResolver.cs` |
| `FakeClock` | class (test) | `tests/ControlDeFinanzasIA.Tests.Unit/TestDoubles/FakeClock.cs` |
| `FakePathResolver` | class (test) | `tests/ControlDeFinanzasIA.Tests.Unit/TestDoubles/FakePathResolver.cs` |

---

## 2. Contexto

F-004 desbloquea **todo test que dependa de tiempo o de rutas**. Sin
estas abstracciones, cada test termina mockeando `DateTime.Now`
(que **no se puede mockear directamente** en .NET — `DateTime.Now`
es un static getter no virtual) o copiando archivos a directorios
temporales. Definir las interfaces ahora y bloquear su uso con
tests es la única forma de que las features siguientes (F-005 en
adelante) no vuelvan a `DateTime.Now` por costumbre.

> **Decisión de ubicación documentada:** las interfaces viven en
> `Application/Abstractions/`, NO en `Domain`. El roadmap y el
> desglose de fase las listan en `Domain`, pero `architecture.md` §7
> las coloca en `Application/Abstractions/`. La arquitectura es la
> fuente de verdad para decisiones técnicas. Si más adelante se
> decide mover a `Domain`, basta con cambiar el namespace y ajustar
> los `using` en Infrastructure y en los tests.

---

## 3. Objetivo

Establecer los contratos `IClock` e `IPathResolver` como la única
forma en que `Application` (y todo el código que la use) accede a
la hora actual y a las rutas de archivos, junto con sus
implementaciones de prueba `FakeClock` y `FakePathResolver` que
permiten a los tests inyectar valores controlados.

---

## 4. Alcance

### 4.1 En alcance

- `interface IClock` en `Application/Abstractions/IClock.cs` con
  dos miembros:
  - `DateTime UtcNow { get; }` — la hora actual en UTC.
  - `DateOnly Today { get; }` — la fecha actual (interpretada por
    la implementación; ver §6 RN-2).
- `interface IPathResolver` en
  `Application/Abstractions/IPathResolver.cs` con dos miembros:
  - `string DataDirectory { get; }` — la ruta **del directorio**
    donde la aplicación persiste datos. No incluye el nombre del
    archivo.
  - `string DatabaseFilePath { get; }` — la ruta **completa** al
    archivo de base de datos. Derivable de `DataDirectory` +
    nombre de archivo.
- Implementaciones de prueba en `Tests.Unit/TestDoubles/`:
  - `FakeClock` — `class` que implementa `IClock` con propiedades
    auto-implementadas con `set;` para que los tests puedan
    setear la hora y la fecha directamente.
  - `FakePathResolver` — `class` que implementa `IPathResolver`
    con propiedades auto-implementadas con `set;` para rutas.
- Tests de los fakes en `Tests.Unit/TestDoubles/` que verifican:
  - `FakeClock`: setear `UtcNow` y verificar que `UtcNow` lo
    retorna; setear `Today` y verificar que `Today` lo retorna;
    setear `DateTime.MinValue`, `DateTime.MaxValue`, `DateOnly`
    extremos.
  - `FakePathResolver`: setear `DataDirectory` y `DatabaseFilePath`
    y verificar; setear rutas vacías, con espacios, con caracteres
    especiales.
- Verificación de **no-acoplamiento**: el proyecto `Domain` no
  referencia `IClock` ni `IPathResolver` (preparación para F-007
  NetArchTest).

### 4.2 Fuera de alcance

- **Implementaciones reales** (`SystemClock`, `SystemPathResolver`).
  Estas se añadirán cuando se haga la composition root (F-008).
- **Registro en DI** de las implementaciones reales. También
  F-008.
- **Lógica de zona horaria** más allá de UTC. `Today` es la fecha
  en la zona horaria local; no se discute aquí cómo se determina
  esa zona (lo decide la implementación real).
- **Validación de rutas** (canonicalización, existencia, permisos).
  La interfaz solo expone strings; la implementación real validará.
- **Localización** de mensajes de error. La interfaz es agnóstica
  al idioma.
- **Mocks auto-generados** (Moq, NSubstitute, etc.). Los fakes son
  implementaciones manuales, explícitas y mínimas. Esto evita
  añadir una dependencia solo para los fakes.

---

## 5. Casos de uso

### CU-1 — Use case obtiene la hora actual
- **Actor:** use case de la capa Application (ej.
  `CreateExpenseUseCase`).
- **Trigger:** el use case necesita timestamp al registrar un
  gasto. `var now = _clock.UtcNow;`
- **Precondición:** `IClock` está inyectado en el constructor del
  use case.
- **Postcondición:** el use case usa `now.UtcNow` para poblar
  `Expense.CreatedAt`.

### CU-2 — Repository abre el archivo de base de datos
- **Actor:** repository de la capa Application o Infrastructure.
- **Trigger:** el repository necesita la ruta del `.db` para
  abrir conexión. `var path = _pathResolver.DatabaseFilePath;`
- **Precondición:** `IPathResolver` está inyectado.
- **Postcondición:** el repository abre el archivo en la ruta
  resuelta por la implementación real de `IPathResolver`.

### CU-3 — Test simula la hora actual
- **Actor:** test de un use case que depende de tiempo.
- **Trigger:** el test quiere verificar que `CreateExpense` se
  ejecuta con la hora esperada. `var fakeClock = new FakeClock {
  UtcNow = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc) };`
  Inyectar `fakeClock` en el use case. Ejecutar. Verificar.
- **Precondición:** el use case recibe `IClock` por DI.
- **Postcondición:** el test verifica el comportamiento esperado
  sin depender del reloj del sistema.

### CU-4 — Test simula la ruta de la base de datos
- **Actor:** test de un repository que abre el `.db`.
- **Trigger:** el test quiere verificar que el repository abre el
  archivo en la ruta correcta. `var fakeResolver = new
  FakePathResolver { DatabaseFilePath = "/tmp/test.db" };`
  Inyectar en el repository. Ejecutar. Verificar.
- **Precondición:** el repository recibe `IPathResolver` por DI.
- **Postcondición:** el test verifica el comportamiento esperado
  sin depender del sistema de archivos.

---

## 6. Requisitos funcionales

### RF-1 — `IClock` interface
- `public interface IClock` en
  `src/ControlDeFinanzasIA.Application/Abstractions/IClock.cs`.
- Miembros:
  - `DateTime UtcNow { get; }` — retorna la hora actual **en
    UTC**. La implementación real es responsable de garantizar
    `Kind == DateTimeKind.Utc`.
  - `DateOnly Today { get; }` — retorna la fecha actual. La
    interpretación de "actual" (zona local, UTC, etc.) es decisión
    de la implementación real; ver §7 RN-2.

### RF-2 — `IPathResolver` interface
- `public interface IPathResolver` en
  `src/ControlDeFinanzasIA.Application/Abstractions/IPathResolver.cs`.
- Miembros:
  - `string DataDirectory { get; }` — ruta **del directorio** de
    datos. No incluye el nombre del archivo. La implementación
    real decide el directorio según el SO (ver
    `tech-stack.md`); esta interfaz es agnóstica al SO.
  - `string DatabaseFilePath { get; }` — ruta **completa** al
    archivo de base de datos (incluye el nombre del archivo).
    Derivable de `DataDirectory` + nombre de archivo, pero se
    expone para que los callers no tengan que conocer la
    convención del nombre.

### RF-3 — `FakeClock`
- `public class FakeClock : IClock` en
  `tests/ControlDeFinanzasIA.Tests.Unit/TestDoubles/FakeClock.cs`.
- Implementa `IClock` con propiedades auto-implementadas con
  `get;` y `set;`:
  - `public DateTime UtcNow { get; set; }`
  - `public DateOnly Today { get; set; }`
- No realiza conversiones ni interpretaciones. Almacena
  exactamente lo que se le setea.
- Inicialización por defecto: `UtcNow = default` (que es
  `DateTime.MinValue` con `Kind = Unspecified`) y
  `Today = default` (que es `DateOnly.MinValue`). El test que
  necesita un valor específico debe setearlo explícitamente.

### RF-4 — `FakePathResolver`
- `public class FakePathResolver : IPathResolver` en
  `tests/ControlDeFinanzasIA.Tests.Unit/TestDoubles/FakePathResolver.cs`.
- Implementa `IPathResolver` con propiedades auto-implementadas
  con `get;` y `set;`:
  - `public string DataDirectory { get; set; } = string.Empty;`
  - `public string DatabaseFilePath { get; set; } = string.Empty;`
- No realiza validación ni canonicalización. Almacena
  exactamente lo que se le setea.
- Inicialización por defecto: ambas propiedades son `string.Empty`.
  El test que necesita una ruta específica debe setearla
  explícitamente.

### RF-5 — Tests de los fakes
- Tests en `Tests.Unit/TestDoubles/FakeClockTests.cs`:
  - `FakeClock_DefaultConstruction_ReturnsDefaultValues` — al
    construir sin parámetros, `UtcNow` es `default(DateTime)` y
    `Today` es `default(DateOnly)`.
  - `FakeClock_SetUtcNow_ReturnsSetValue` — setear un valor
    cualquiera, verificar que `UtcNow` lo retorna.
  - `FakeClock_SetToday_ReturnsSetValue` — setear un valor,
    verificar que `Today` lo retorna.
  - `FakeClock_SetDateTimeMinValue_ReturnsMinValue` — boundary
    con `DateTime.MinValue`.
  - `FakeClock_SetDateTimeMaxValue_ReturnsMaxValue` — boundary
    con `DateTime.MaxValue`.
  - `FakeClock_SetDateOnlyMinAndMax_ReturnsCorrectValues` — boundary
    con `DateOnly.MinValue` y `DateOnly.MaxValue`.
  - `FakeClock_SetKindToUtc_PreservesUtcKind` — si se setea un
    `DateTime` con `Kind = Utc`, `UtcNow` retorna ese `Kind`.
- Tests en `Tests.Unit/TestDoubles/FakePathResolverTests.cs`:
  - `FakePathResolver_DefaultConstruction_ReturnsEmptyStrings` — al
    construir sin parámetros, ambas propiedades son `string.Empty`.
  - `FakePathResolver_SetDataDirectory_ReturnsSetValue` — setear una
    ruta, verificar.
  - `FakePathResolver_SetDatabaseFilePath_ReturnsSetValue` — setear
    una ruta completa, verificar.
  - `FakePathResolver_SetEmptyStrings_AcceptsEmptyValues` — el fake
    no valida, acepta strings vacíos.
  - `FakePathResolver_SetPathWithSpaces_PreservesValue` — rutas con
    espacios.
  - `FakePathResolver_SetPathWithSpecialCharacters_PreservesValue`
    — rutas con caracteres especiales (`/`, `\`, `..`).

---

## 7. Requisitos no funcionales

### RNF-1 — Pureza de Application
- `IClock` e `IPathResolver` solo dependen de BCL (`System`).
- Los fakes solo dependen de BCL + de las interfaces.
- Esto se valida con NetArchTest en F-007: `Application` no debe
  tener `PackageReference`s, y `Domain` no debe referenciar
  `Application/Abstractions/`.

### RNF-2 — No threading implícito
- Los fakes **no son thread-safe** (propiedades auto-implementadas
  sin sincronización). Esto es aceptable: los tests son single-thread
  por diseño.
- Las interfaces reales (`SystemClock`, `SystemPathResolver`) sí
  serán thread-safe (F-008), pero eso es responsabilidad de la
  implementación, no de la interfaz.

### RNF-3 — Cobertura de tests >= 95%
- Cobertura de las interfaces: medida indirectamente por el uso en
  use cases reales (F-005 en adelante). Las interfaces no tienen
  lógica propia.
- Cobertura de los fakes: medida por los tests de fakes (RF-5).
  Sin `coverlet.collector`, verificación manual: cada línea de
  `FakeClock` y `FakePathResolver` está cubierta por al menos un
  test.

### RNF-4 — Mensajes de error en inglés (en código)
- Si en el futuro se añaden excepciones a las interfaces, los
  mensajes serán en inglés. Por ahora las interfaces no lanzan
  excepciones.

### RNF-5 — Sin estado mutable global
- Los fakes son instancias. No hay singletons, no hay static
  state. Cada test crea su propio `FakeClock` / `FakePathResolver`.

---

## 8. Reglas de negocio

### RN-1 — `IClock.UtcNow` siempre retorna UTC
- La implementación real (`SystemClock` en F-008) debe garantizar
  que `DateTime.Kind == DateTimeKind.Utc`. Los callers pueden
  confiar en que el `DateTime` retornado es UTC.
- El fake **no** fuerza `Kind = Utc`. Si el test setea un
  `DateTime` con `Kind = Local`, el fake lo retorna con `Kind =
  Local`. Esto es intencional: el test verifica el comportamiento
  del fake, no la semántica de UTC (que es responsabilidad de la
  implementación real).

### RN-2 — `IClock.Today` es la fecha "actual" en la zona que determine la implementación
- La interfaz no especifica zona. La implementación real decide
  (probablemente zona local). El fake retorna exactamente lo que
  se setea.
- Documentación: el XML doc del miembro explica que "actual" es
  decisión de la implementación.

### RN-3 — `IPathResolver.DataDirectory` y `DatabaseFilePath` son responsabilidades distintas
- `DataDirectory` es solo la carpeta. El nombre del archivo NO
  debe incluirse en este string.
- `DatabaseFilePath` incluye el nombre del archivo. Es derivable
  de `DataDirectory` + nombre, pero se expone para no obligar a
  los callers a conocer la convención.

### RN-4 — `IPathResolver` no garantiza que las rutas existan
- La interfaz solo retorna strings. La implementación real (en
  F-008) puede crear directorios si no existen, pero la interfaz
  es agnóstica a esa lógica.

### RN-5 — Los fakes no validan entrada
- Los fakes almacenan exactamente lo que se les setea. Si el test
  setea una ruta vacía o nula, el fake la retorna. Esto es
  intencional: el fake es un *test double*, no una validación de
  negocio.

---

## 9. Modelo de dominio

```
Application/Abstractions/
  │
  ├── <<interface>> IClock
  │     ├── + DateTime UtcNow { get; }
  │     └── + DateOnly Today { get; }
  │
  └── <<interface>> IPathResolver
        ├── + string DataDirectory { get; }
        └── + string DatabaseFilePath { get; }

Tests.Unit/TestDoubles/
  │
  ├── class FakeClock : IClock
  │     ├── + DateTime UtcNow { get; set; }
  │     └── + DateOnly Today { get; set; }
  │
  └── class FakePathResolver : IPathResolver
        ├── + string DataDirectory { get; set; } = ""
        └── + string DatabaseFilePath { get; set; } = ""
```

- `IClock` e `IPathResolver` son **interfaces** (contratos sin
  lógica).
- `FakeClock` e `FakePathResolver` son **implementaciones
  manuales** para tests. No usan una librería de mocking.
- `FakeClock` y `FakePathResolver` viven en el **proyecto de
  tests**, NO en `Application`. Son `internal` (visibles solo al
  test project).

---

## 10. Validaciones

Las interfaces en sí no validan nada (son contratos). Los fakes
tampoco validan (almacenan lo que se les setea).

| # | Validación | Cuándo se aplica | Comportamiento esperado |
| --- | --- | --- | --- |
| V-1 | `IClock.UtcNow.Kind` | En implementación real (F-008) | Debe ser `DateTimeKind.Utc` |
| V-2 | `IClock.Today` zona | En implementación real (F-008) | Debe ser coherente con la zona definida |
| V-3 | `IPathResolver.DataDirectory` no vacío | En implementación real (F-008) | Debe apuntar a un directorio válido |
| V-4 | `IPathResolver.DatabaseFilePath` incluye el archivo | En implementación real (F-008) | Debe terminar con el nombre del archivo |
| V-5 | `FakeClock` setter | N/A (test double) | Acepta cualquier `DateTime` y `DateOnly` |
| V-6 | `FakePathResolver` setter | N/A (test double) | Acepta cualquier string, incluido `string.Empty` |

---

## 11. Casos borde

### EC-1 — `IClock.UtcNow` retorna `DateTime.MinValue`
- Válido. La implementación real (F-008) usa `DateTime.UtcNow`, que
  nunca retorna `MinValue` en la práctica. El fake puede
  setearlo para testear boundary.

### EC-2 — `IClock.UtcNow` retorna `DateTime.MaxValue`
- Válido. Mismo razonamiento.

### EC-3 — `IClock.Today` retorna `DateOnly.MinValue` (año 1) o `MaxValue` (año 9999)
- Válido. El fake acepta ambos. La implementación real usa
  `DateOnly.FromDateTime(DateTime.Today)` que retorna la fecha
  actual del sistema (siempre entre 1885 y 9999 aproximadamente).

### EC-4 — `IPathResolver.DataDirectory` es `string.Empty`
- Válido en la interfaz. En la implementación real, no debería
  pasar, pero la interfaz no lo prohíbe.

### EC-5 — `IPathResolver.DatabaseFilePath` no tiene extensión
- Válido. La interfaz es agnóstica al formato del archivo (es un
  `.db` en SQLite, pero la interfaz no lo sabe).

### EC-6 — `IPathResolver.DataDirectory` no termina con separador
- Válido. La interfaz no garantiza el formato. La implementación
  real puede usar `Path.Combine(DataDirectory, "app.db")` que es
  robusto a ambos casos.

### EC-7 — `FakeClock` setea `DateTime` con `Kind = Local` o `Unspecified`
- Válido. El fake no fuerza `Kind = Utc`. El test verifica el
  contrato del fake (almacena y retorna), no la semántica de UTC
  (que es de la implementación real).

### EC-8 — `FakePathResolver` setea rutas con separadores de Linux en Windows
- Válido. El fake no valida. La implementación real usa
  `Path.Combine` que es robusto. En tests, se puede usar
  cualquier string.

---

## 12. Dependencias

### 12.1 Prerrequisitos de implementación

- **F-001 completada** (T-001 a T-014, T-010a): la solución, los
  proyectos y la configuración global están listos. El proyecto
  `ControlDeFinanzasIA.Application` existe.

### 12.2 Dependencias con otras features

| Feature | Relación |
| --- | --- |
| F-002 (Money) | Independiente. |
| F-003 (DateRange) | Independiente. |
| F-005 (Result<T>, PagedResult<T>) | Independiente. F-004 se usa en use cases que F-005 retorna. |
| F-006 (Interfaces de repositorio) | Consumidora. Los repositorios futuros podrían recibir `IPathResolver` para abrir el `.db`. |
| F-007 (NetArchTest) | Verificación. F-007 valida que `Application/Abstractions/` existe, que `Domain` no la referencia, etc. |
| F-008 (Composition root) | Consumidora. F-008 registra `SystemClock : IClock` y `SystemPathResolver : IPathResolver` en el contenedor de DI. |

### 12.3 Bloqueos

- **Ninguno.** F-004 puede implementarse inmediatamente después de
  F-003.

---

## 13. Fuera de alcance (detallado)

Repetición con énfasis para evitar ambigüedad. Estos puntos **NO**
se implementan en F-004:

- **Implementaciones reales** (`SystemClock`, `SystemPathResolver`).
  Son F-008. Aquí NO.
- **Registro en DI** de las implementaciones reales. F-008. Aquí
  NO.
- **Lógica de zona horaria** más allá de UTC. `IClock.UtcNow`
  retorna UTC; `IClock.Today` es decisión de la implementación.
- **Validación de rutas** (canonicalización, permisos, existencia).
  La interfaz es agnóstica.
- **Localización** de mensajes. Las interfaces no lanzan.
- **Mocks auto-generados** (Moq, NSubstitute). Los fakes son
  clases manuales. Aquí NO.
- **Lógica de thread-safety** en los fakes. Los fakes no son
  thread-safe; las implementaciones reales sí lo serán (F-008).
- **Cache de fecha** (`Today` puede ser costoso si se llama
  frecuentemente; podría tener cache interno). Decisión de la
  implementación real, no de la interfaz.
- **Localización del directorio de datos** dentro del
  `DataDirectory` (subcarpetas por usuario, por año, etc.).
  Decisión de la implementación real.

---

## 14. Criterios de aceptación

La feature se considera completada cuando **todos** los siguientes
puntos se cumplen, verificables mediante tests automatizados y
revisión de código.

### AC-1 — Interfaces compilan sin warnings
- [ ] `IClock` existe en
      `src/ControlDeFinanzasIA.Application/Abstractions/IClock.cs`,
      es `public interface`, tiene `UtcNow` y `Today` con `get;` y
      los tipos correctos (`DateTime` y `DateOnly`).
- [ ] `IPathResolver` existe en
      `src/ControlDeFinanzasIA.Application/Abstractions/IPathResolver.cs`,
      es `public interface`, tiene `DataDirectory` y
      `DatabaseFilePath` con `get;` y tipo `string`.
- [ ] `dotnet build`: 0 warnings, 0 errors.

### AC-2 — Fakes existen y permiten control
- [ ] `FakeClock` existe en
      `tests/.../TestDoubles/FakeClock.cs`, implementa `IClock`,
      tiene `UtcNow` y `Today` con `get; set;`.
- [ ] `FakePathResolver` existe en
      `tests/.../TestDoubles/FakePathResolver.cs`, implementa
      `IPathResolver`, tiene `DataDirectory` y `DatabaseFilePath`
      con `get; set;`.

### AC-3 — Tests de los fakes verifican comportamiento
- [ ] `FakeClock_DefaultConstruction_ReturnsDefaultValues` pasa.
- [ ] `FakeClock_SetUtcNow_ReturnsSetValue` pasa.
- [ ] `FakeClock_SetToday_ReturnsSetValue` pasa.
- [ ] `FakeClock_SetDateTimeMinValue_ReturnsMinValue` pasa.
- [ ] `FakeClock_SetDateTimeMaxValue_ReturnsMaxValue` pasa.
- [ ] `FakeClock_SetDateOnlyMinAndMax_ReturnsCorrectValues` pasa.
- [ ] `FakeClock_SetKindToUtc_PreservesUtcKind` pasa.
- [ ] `FakePathResolver_DefaultConstruction_ReturnsEmptyStrings` pasa.
- [ ] `FakePathResolver_SetDataDirectory_ReturnsSetValue` pasa.
- [ ] `FakePathResolver_SetDatabaseFilePath_ReturnsSetValue` pasa.
- [ ] `FakePathResolver_SetEmptyStrings_AcceptsEmptyValues` pasa.
- [ ] `FakePathResolver_SetPathWithSpaces_PreservesValue` pasa.
- [ ] `FakePathResolver_SetPathWithSpecialCharacters_PreservesValue` pasa.

### AC-4 — Aislamiento de Domain
- [ ] `dotnet list src/.../Domain package` sigue mostrando "No
      packages were found".
- [ ] `Domain` no referencia `IClock` ni `IPathResolver`
      (verificable por `grep` o por NetArchTest en F-007).

### AC-5 — Cobertura y calidad
- [ ] `dotnet format style --verify-no-changes`: exit 0.
- [ ] `dotnet test --project tests/.../Tests.Unit` pasa todos los
      tests nuevos.
- [ ] Cobertura de los fakes: cada setter probado con al menos un
      test (verificación manual sin coverlet).

### AC-6 — Convenciones del repo
- [ ] Interfaces siguen el `.editorconfig` (file-scoped namespace,
      sin `using` redundantes, sin XML doc obligatorio en
      interfaces públicas — es opcional).
- [ ] Fakes siguen el `.editorconfig`.
- [ ] No hay archivos `IClock1.cs`, `FakeClock1.cs`, ni
      placeholders similares.

---

## 15. Estructura de archivos esperada

```
src/
└── ControlDeFinanzasIA.Application/
    ├── ControlDeFinanzasIA.Application.csproj
    ├── Abstractions/                       ← NUEVO
    │   ├── IClock.cs                       ← nuevo (F-004)
    │   └── IPathResolver.cs                ← nuevo (F-004)
    ├── Common/                             (vacío por ahora; F-005)
    ├── UseCases/                           (vacío; llega en F-005+)
    └── ...

tests/
└── ControlDeFinanzasIA.Tests.Unit/
    ├── ValueObjects/                       (F-002, F-003)
    │   ├── MoneyTests.cs
    │   └── DateRangeTests.cs
    └── TestDoubles/                        ← NUEVO
        ├── FakeClock.cs                    ← nuevo (F-004)
        ├── FakeClockTests.cs               ← nuevo (F-004)
        ├── FakePathResolver.cs             ← nuevo (F-004)
        └── FakePathResolverTests.cs        ← nuevo (F-004)
```

---

## 16. Decisiones de diseño explícitas

### 16.1 — Ubicación en `Application/Abstractions/`, no en `Domain`

El roadmap y el desglose de fase listan F-004 en `Domain`. La
arquitectura (§7) las coloca en `Application/Abstractions/`.
**La arquitectura gana** porque es la fuente de verdad para
decisiones técnicas. Razones:
- `Domain` debe ser puro (sin dependencias). Las interfaces de
  "seam" son contratos para que `Application` se comunique con
  infraestructura, lo cual es una preocupación de Application.
- Si en el futuro se decide mover a `Domain`, basta con cambiar el
  namespace y los `using`. No hay acoplamiento de comportamiento,
  solo de tipo.

### 16.2 — `IClock.UtcNow` retorna `DateTime`, no `DateTimeOffset`

- `DateTimeOffset` es técnicamente más preciso (incluye offset
  respecto a UTC). Pero el desglose de fase y el roadmap dicen
  `DateTime`. La spec sigue lo que pide el task.
- La implementación real debe garantizar `Kind == Utc`. Documentado
  en RN-1.
- Si en el futuro se quiere `DateTimeOffset`, se puede añadir como
  miembro adicional (`IClock.UtcNowOffset` o migración
  completa). Por ahora, `DateTime` con `Kind = Utc`.

### 16.3 — Fakes manuales, no Moq/NSubstitute

Los fakes son clases manuales con propiedades auto-implementadas.
Razones:
- **Cero dependencias** en el test project. Moq y NSubstitute
  añaden peso.
- **Explicito y minimalista**: el código del fake es la
  especificación. Con Moq, la configuración del mock está en los
  tests; con clases manuales, está en el fake.
- **Rendimiento**: las llamadas a los setters son O(1), sin
  overhead de reflexión o generación de proxies.

### 16.4 — Fakes con `get; set;` (no `init`)

Los fakes son **mutables** por diseño. El test setea, luego el SUT
lee. Si usáramos `init`, el test tendría que construir un nuevo
fake cada vez que cambia el valor, lo cual es menos idiomático en
este contexto.

---

## 17. Riesgos y mitigaciones

| Riesgo | Probabilidad | Impacto | Mitigación |
| --- | --- | --- | --- |
| Las interfaces se vuelven demasiado amplias (más miembros de los necesarios) | Baja | Medio | Empezar con lo mínimo (UtcNow, Today, DataDirectory, DatabaseFilePath). Añadir solo si es necesario. |
| Los fakes tienen divergencia con la implementación real | Media | Alto | Los fakes son explícitos (no se generan). Si cambia la interfaz, el compilador obliga a actualizar el fake. |
| Confusión entre `DataDirectory` y `DatabaseFilePath` | Baja | Bajo | Nombres distintos, XML doc explícito, tests diferenciados. |
| Zona horaria ambigua en `Today` | Baja | Medio | El XML doc de `Today` aclara que es decisión de la implementación. La implementación real (F-008) documenta la zona usada. |
| Mocking con Moq o similar se prefiere en el equipo | Baja | Bajo | Los fakes son intercambiables por Moq en el futuro si el equipo lo decide. No hay lock-in. |
| `Domain` referencia `IClock` o `IPathResolver` accidentalmente | Baja | Alto | NetArchTest en F-007 verifica que `Domain` no tiene `PackageReference`s. Pero las interfaces viven en `Application`, no en `Domain`, así que un `using` accidental en `Domain` fallaría por compilación. |

---

## 18. Referencias

- `docs/vision.md` — describe el producto y los casos de uso que
  dependen de "fecha actual" y "ruta del archivo".
- `docs/architecture.md` §7 (Composition root) y §3 (Value
  objects en Domain) — define el patrón de interfaces de "seam"
  en `Application/Abstractions/`.
- `docs/roadmap.md` § Fase 1, F-004 dentro del desglose.
- `spec/constitution/tech-stack.md` — define las rutas de los
  archivos de datos por SO (relevante para `IPathResolver`
  cuando se implemente la versión real en F-008).
- `spec/phases/phase-01/features.md` — F-004 dentro del
  desglose de features de la fase 1.
- `spec/features/phase-01/F-001-skeleton-solution.done.md` —
  provee la base (proyecto `Application` existe con
  configuración limpia).
- `spec/features/phase-01/F-002-money-value-object.done.md` y
  `F-003-datarange-value-object.done.md` — siguen el mismo
  patrón de value object.
- `spec/README.md` — convención completa del directorio `spec/`.
- **Documentación de `DateTime.UtcNow`:**
  [learn.microsoft.com/dotnet/api/system.datetime.utcnow](https://learn.microsoft.com/dotnet/api/system.datetime.utcnow)
- **Documentación de `DateOnly`:**
  [learn.microsoft.com/dotnet/api/system.dateonly](https://learn.microsoft.com/dotnet/api/system.dateonly)
- **Documentación de `Environment.SpecialFolder` y paths
  cross-platform:**
  [learn.microsoft.com/dotnet/api/system.environment.specialfolder](https://learn.microsoft.com/dotnet/api/system.environment.specialfolder)
