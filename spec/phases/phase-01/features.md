# Fase 1 — Features: Esqueleto de arquitectura y contratos base

> Descomposición de la **Fase 1** del `docs/roadmap.md` en features
> independientes, cada una candidata a una Specification propia.
>
> **Ubicación de este documento:** `spec/phases/phase-01/features.md`.
> Las specs de cada feature viven en `spec/features/phase-01/`.
> Ver `spec/README.md` para la convención completa del repo.
>
> Fuente de verdad: `docs/vision.md`, `docs/architecture.md`,
> `docs/roadmap.md`, `spec/constitution/*`.
>
> **Reglas de la fase:**
> - Cada feature deja la solución compilando.
> - Cada feature es una capacidad única.
> - Las dependencias entre features se indican explícitamente.
> - Una feature completada = sus criterios de cierre cumplidos.
>
> **Nota sobre ubicación de `IClock` e `IPathResolver`:** el roadmap los
> lista en `Domain`, mientras que `architecture.md` §7 los ubica en
> `Application/Abstractions/`. Esta feature los coloca en
> `Application/Abstractions/`, alineándose con la arquitectura. La
> decisión es reversible (moverlos a `Domain` no rompe contratos
> siempre que se mantenga el namespace usado por Infrastructure).

---

## Resumen

| ID | Nombre | Prioridad | Estimación | Depende de |
| --- | --- | --- | --- | --- |
| 001 | Esqueleto de solución y proyectos por capa | Crítica | M | — | ✅ COMPLETADA |
| 002 | Money value object con enum Currency | Alta | M | 001 | ✅ COMPLETADA |
| 003 | DateRange value object | Alta | S | 001 | ✅ COMPLETADA |
| 004 | Abstracciones IClock e IPathResolver | Alta | S | 001 | ✅ COMPLETADA |
| 005 | Tipos comunes Result\<T\> y PagedResult\<T\> | Alta | S | 001 | ✅ COMPLETADA |
| 006 | Contratos de interfaces de repositorio | Alta | M | 001 | ✅ COMPLETADA |
| 007 | Validación arquitectónica con NetArchTest | Alta | M | 001 | ✅ COMPLETADA |
| 008 | Composition root con host de DI y logging | Crítica | M | 001, 004 | ✅ COMPLETADA |

**Tamaño total estimado:** ~3–5 días de trabajo para una persona.
**MVP de la fase:** 001 + 008 (compilando y arrancando). El resto
completa el "esqueleto validado".

---

## F-001 — Esqueleto de solución y proyectos por capa — ✅ COMPLETADA

**Objetivo**
Crear la solución con los 6 proyectos según `architecture.md` §3, con
las referencias de `architecture.md` §4, settings globales de
compilación, y la app Avalonia arrancando con una ventana vacía.

**Descripción breve**
- Crear `ControlDeFinanzasIA.sln`.
- Crear los 6 proyectos:
  - `ControlDeFinanzasIA.Domain` (class library, `net8.0`).
  - `ControlDeFinanzasIA.Application` (class library, `net8.0`).
  - `ControlDeFinanzasIA.Infrastructure` (class library, `net8.0`).
  - `ControlDeFinanzasIA.UI` (executable, `net8.0`, Avalonia).
  - `ControlDeFinanzasIA.Tests.Unit` (xUnit).
  - `ControlDeFinanzasIA.Tests.Integration` (xUnit).
- Configurar referencias entre proyectos según `architecture.md` §4
  (regla: cada proyecto solo referencia los permitidos).
- Settings globales en `Directory.Build.props` o en cada `.csproj`:
  `Nullable=enable`, `LangVersion=latest`, `TreatWarningsAsErrors=true`,
  `ImplicitUsings=enable`.
- `.editorconfig` raíz con reglas de formato y naming.
- StyleCop configurado (sin rechazos a warning).
- `App.axaml` / `App.axaml.cs` mínimos y `Program.cs` que arranca
  Avalonia con una `MainWindow` vacía.

**Justificación**
Sin el esqueleto correcto, las demás features de la fase y del
proyecto se acumulan sobre una base débil. Esta feature fija el
"contrato de capas" a nivel de solución, que es lo que el resto del
proyecto asume. Hacerla explícita desde el día uno evita la
reorganización típica del "ahora muevo 200 archivos a otro proyecto".

**Dependencias**
- Ninguna (es la primera).

**Prioridad**
- Crítica. Bloquea todas las demás features de la fase y del proyecto.

**Estimación**
- M (medio día a un día).

**Criterios para considerarla completada**
- `dotnet build` pasa sin warnings en los 6 proyectos.
- `dotnet test` corre (aunque no haya tests).
- La app UI arranca y muestra una ventana vacía en Windows, macOS y
  Linux.
- El grafo de referencias entre proyectos coincide exactamente con
  `architecture.md` §4 (verificable manualmente o con un check de
  NetArchTest temporal).
- `.editorconfig` y StyleCop activos, sin supresiones masivas.
- `Nullable=enable` y `TreatWarningsAsErrors=true` activos en los 6
  proyectos.

---

## F-002 — Money value object con enum Currency — ✅ COMPLETADA

**Objetivo**
Implementar el value object `Money` y el enum `Currency` en `Domain`,
con la aritmética segura y el formato coherente con la cultura
es-DO.

**Descripción breve**
- `enum Currency { DOP, USD }` en `Domain/Enums/Currency.cs`.
- `record Money(decimal Amount, Currency Currency)` en
  `Domain/ValueObjects/Money.cs`, **no** struct mutable.
- Operaciones:
  - Suma, resta, multiplicación por escalar.
  - Comparación por igualdad (record ya lo da).
  - `ToString(CultureInfo)` con formato `#,##0.00` y símbolo/código
    `DOP` o `USD`.
- **Reglas:**
  - Operaciones entre monedas distintas lanzan `InvalidOperationException`
    (la conversión entre monedas no es de dominio; la hace otra capa).
  - `Amount` validado: nunca negativo, no NaN, no Infinity. Se valida en
    el factory/constructor.
- Tests unitarios:
  - Suma, resta, multiplicación.
  - Rechazo de mezcla de monedas.
  - Formato en `es-DO` y `en-US`.
  - Rechazo de `Amount` inválido.

**Justificación**
`Money` es el value object más usado del proyecto: está en Expense,
Income, Debt, Goal, Budget, RecurringRule. Definirlo bien y probarlo
ahora evita arrastrar errores a todas las features. Es la primera
prueba de que el proyecto `Domain` funciona de forma aislada (sin
EF Core, sin Avalonia).

**Dependencias**
- F-001 (proyecto `Domain` debe existir).

**Prioridad**
- Alta. Otras features de fases futuras dependen de `Money`.

**Estimación**
- M (medio día).

**Criterios para considerarla completada**
- `Money` y `Currency` compilan en `Domain` sin warnings.
- Tests unitarios en `Tests.Unit` cubren aritmética, mezcla de
  monedas, formato y validación de `Amount`.
- Cobertura del tipo ≥ 95%.
- Cero referencias a EF Core, Avalonia o cualquier paquete externo
  (verificable con `NetArchTest` o manualmente).
- Ejemplo: `new Money(100m, Currency.DOP).ToString(new
  CultureInfo("es-DO"))` produce `"100.00 DOP"`.

---

## F-003 — DateRange value object — ✅ COMPLETADA

**Objetivo**
Implementar `DateRange` en `Domain` con semántica de período cerrado
por ambos extremos y operaciones comunes (intersección, contención,
duración).

**Descripción breve**
- `record DateRange(DateOnly Start, DateOnly End)` en
  `Domain/ValueObjects/DateRange.cs`.
- Validación en el factory: `Start <= End`.
- Operaciones:
  - `Contains(DateOnly)`.
  - `Overlaps(DateRange)`.
  - `DurationInDays()` (entero).
  - `Intersect(DateRange) → DateRange?`.
  - `Equals` por valor (record).
- Tests unitarios para cada operación, incluido el caso
  `Start == End` (rango de un día).

**Justificación**
`DateRange` se usará masivamente en queries (filtros por periodo),
reportes (semana, mes, año) y presupuestos. Como `Money`, es un
candidato natural para entrar en la primera fase: sin él, las
features siguientes no pueden expresar períodos de forma tipada y
segura.

**Dependencias**
- F-001.

**Prioridad**
- Alta.

**Estimación**
- S (1–2 horas).

**Criterios para considerarla completada**
- `DateRange` compila en `Domain` sin warnings.
- Tests unitarios cubren todas las operaciones y casos borde (rango
  vacío, intersección nula, etc.).
- Sin dependencias externas (puro BCL).
- Cobertura del tipo ≥ 95%.

---

## F-004 — Abstracciones IClock e IPathResolver — ✅ COMPLETADA

**Objetivo**
Definir las dos interfaces de "seam" que toda la app usa para
obtener la hora actual y la ruta del archivo de datos, sin acoplarse
a `DateTime.Now` ni a `Environment`.

**Descripción breve**
- `interface IClock` en `Application/Abstractions/IClock.cs` con:
  - `DateTime UtcNow { get; }`
  - `DateOnly Today { get; }`
- `interface IPathResolver` en
  `Application/Abstractions/IPathResolver.cs` con:
  - `string DataDirectory { get; }` (devuelve la ruta **del directorio**
    de datos, no del archivo; el nombre del archivo es interno a
    Infrastructure).
  - `string DatabaseFilePath { get; }` (devuelve la ruta completa al
    `.db`; se puede derivar de `DataDirectory`).
- Implementaciones de prueba (`FakeClock`, `FakePathResolver`) en
  `Tests.Unit/TestDoubles/` para que los tests no necesiten las
  implementaciones reales.
- Tests:
  - `FakeClock` permite setear hora arbitraria.
  - `FakePathResolver` permite setear rutas arbitrarias.

**Justificación**
`IClock` y `IPathResolver` son los contratos que permiten **testear
fácilmente** todo lo que dependa de tiempo o de rutas. Sin estas
abstracciones, cada test termina mockeando `DateTime.Now` (imposible
directamente) o copiando archivos. Definirlas ahora y bloquear su uso
con tests es la única forma de que las features siguientes no vuelvan
a `DateTime.Now` por costumbre.

> **Decisión de ubicación:** siguen `architecture.md` §7
> (`Application/Abstractions/`). El roadmap las lista en `Domain`,
> pero la arquitectura es la fuente de verdad para decisiones
> técnicas. Si más adelante se decide mover a `Domain`, basta con
> cambiar el namespace y ajustar los `using` en Infrastructure y en
> los tests.

**Dependencias**
- F-001.

**Prioridad**
- Alta. Es la base de todo test que involucre tiempo o rutas.

**Estimación**
- S (1–2 horas).

**Criterios para considerarla completada**
- `IClock` y `IPathResolver` compilan en `Application/Abstractions/`
  sin warnings.
- `FakeClock` y `FakePathResolver` permiten controlar tiempo y rutas
  en tests.
- Tests de los fakes en `Tests.Unit` verifican su comportamiento.
- `Domain` no las referencia (son de Application).

---

## F-005 — Tipos comunes `Result` y `PagedResult<T>` — ✅ COMPLETADA

**Objetivo**
Establecer el patrón de retorno de use cases y queries: errores
esperados como valores, no como excepciones.

**Descripción breve**
- `record Result<T>(bool IsSuccess, T? Value, Error? Error)` en
  `Application/Common/Result.cs`.
- `record Error(string Code, string Message, IReadOnlyDictionary<string,
  string[]>? Details = null)`.
- Factory helpers: `Result.Success(T)`, `Result.Failure<T>(Error)`.
- Helper `Result.Failure<T>(string code, string message, ...)`.
- `record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page,
  int PageSize)` en `Application/Common/PagedResult.cs`.
- Tests:
  - Construcción success/failure.
  - Uso idiomático (no se debería poder construir un `Result` con
    `IsSuccess=true` y `Error!=null` o `Value=null`).
  - `PagedResult` con paginación correcta.

**Justificación**
Es la base del manejo de errores en `Application`. Sin un `Result<T>`
definido y probado, cada use case que aparezca desde la fase 3
inventará su propia forma de devolver éxito/fallo, y se acabó la
consistencia. Definirlo **antes** de tener use cases reales es la
única forma de fijarlo.

**Dependencias**
- F-001.

**Prioridad**
- Alta.

**Estimación**
- S (2–3 horas, hay que decidir bien la API del tipo).

**Criterios para considerarla completada**
- `Result<T>` y `PagedResult<T>` compilan en `Application/Common/` sin
  warnings.
- Tests unitarios cubren construcción, factories, inmutabilidad.
- API documentada en XML comments.
- Cero dependencias externas.

---

## F-006 — Contratos de interfaces de repositorio — ✅ COMPLETADA

**Objetivo**
Definir todos los contratos de persistencia de la fase 1, como
firmas vacías, para que `Application` no dependa de `Infrastructure`
y para que las features futuras solo añadan métodos, no nuevas
interfaces.

**Descripción breve**
- En `Application/Abstractions/`, una interfaz por agregado, con
  métodos **firmados pero sin implementación**:
  - `IExpenseRepository` (Add, Update, Remove, GetById, queries
    básicas — no queries complejas todavía).
  - `IIncomeRepository`.
  - `IDebtRepository`.
  - `IDebtPaymentRepository`.
  - `IRecurringRuleRepository`.
  - `IGoalRepository`.
  - `IGoalContributionRepository`.
  - `IBudgetRepository`.
  - `ICategoryRepository`.
  - `ITagRepository`.
  - `IPaymentMethodRepository`.
  - `ISavedViewRepository`.
  - `IAnomalyAlertRepository`.
  - `ISettingRepository`.
  - `IUnitOfWork` con `Task<int> SaveChangesAsync(CancellationToken)`.
- Cada método devuelve `Task<T>` o `Task<Result<T>>` cuando aplique
  (decisión: para la fase 1, queries devuelven `Task<T>`; commands
  devuelven `Task<Result<T>>` para fijar el patrón).
- Sin tests todavía (no hay implementación que probar). Los tests
  entran en la fase 2 cuando se creen los repos en `Infrastructure`.

**Justificación**
Aunque las features que usen estos repos llegan en fases 3–11,
**definir las interfaces ahora** bloquea la tentación de añadir EF
Core o tipos de Infrastructure a `Application`. Una vez que las
interfaces están y compilan, no hay excusa para meter
`Microsoft.EntityFrameworkCore` en `Application`. Esto cumple la
ADR-001 (Clean Architecture) y es lo que valida `NetArchTest`.

**Dependencias**
- F-001.
- F-005 (los commands devuelven `Result<T>`).

**Prioridad**
- Alta. Es la línea defensiva contra acoplamiento accidental a EF
  Core.

**Estimación**
- M (medio día — son muchas interfaces, pero cada una es trivial).

**Criterios para considerarla completada**
- Las 15 interfaces compilan en `Application/Abstractions/` sin
  warnings.
- `Application` sigue sin referenciar a `Infrastructure`.
- Cada interfaz tiene XML doc comments.
- No hay implementación alguna en `Application` (solo contratos).

---

## F-007 — Validación arquitectónica con NetArchTest — ✅ COMPLETADA

**Objetivo**
Proteger automáticamente el grafo de dependencias entre capas con
tests que fallen en CI si alguien rompe la arquitectura.

**Descripción breve**
- Agregar `NetArchTest.Rules` y `xunit` (si no están) a
  `Tests.Unit`.
- Crear `ArchitectureTests.cs` (o carpeta `Architecture/`) con reglas:
  - `Domain` no debe referenciar a `Application`, `Infrastructure`,
    `UI`, `Tests.*` ni a `Microsoft.EntityFrameworkCore*`,
    `Avalonia.*`, `HttpClient`.
  - `Application` solo puede referenciar a `Domain` y a BCL/FluentValidation.
  - `Infrastructure` puede referenciar a `Application` y `Domain`, y a
    EF Core, SQLite, HttpClient.
  - `UI` puede referenciar a `Application` y `Domain`, **no** a
    `Infrastructure` (salvo el composition root, que se modela como
    excepción explícita).
  - `Tests.*` pueden referenciar a cualquiera de los 4 proyectos
    principales.
- Los tests deben correr como parte de `dotnet test`.

**Justificación**
Las reglas arquitectónicas son **documentación ejecutable**. Sin un
test que falle cuando alguien mete EF Core en `Domain`, las
violaciones se descubren en code review (tarde) o en producción
(tardísimo). Con NetArchTest, la violación se detecta en el commit.

**Dependencias**
- F-001 (proyectos deben existir).
- Idealmente después de F-006 (interfaces creadas), porque las
  reglas se vuelven más significativas con contratos reales. Pero se
  puede hacer con proyectos vacíos.

**Prioridad**
- Alta. Es la red de seguridad del proyecto entero.

**Estimación**
- M (medio día — escribir las reglas y aprender la API de
  NetArchTest).

**Criterios para considerarla completada**
- `dotnet test` ejecuta los tests de arquitectura y pasan.
- Una violación intencional (ej. agregar `using Domain;` en `UI` con
  un `using Infrastructure;` mal puesto) hace fallar el test
  correspondiente.
- Las reglas están documentadas (qué se valida y por qué) en
  comentarios y/o en un `architecture-rules.md` corto.

---

## F-008 — Composition root con host de DI y logging — ✅ COMPLETADA

**Objetivo**
Dejar el `Program.cs` de `UI` configurando un `Host` de .NET con
`Microsoft.Extensions.DependencyInjection` y
`Microsoft.Extensions.Logging`, registrando las abstracciones
existentes (sin implementaciones reales todavía), para que la
arquitectura esté **eléctricamente conectada** y no solo
declarada en archivos.

**Descripción breve**
- En `UI/Program.cs`:
  - Crear un `Host` genérico con `Host.CreateApplicationBuilder` (o
    `Host.CreateDefaultBuilder` para una app de escritorio).
  - Registrar `ILogger` con `Microsoft.Extensions.Logging` (consola en
    Debug; un sink simple en producción se difiere a una fase de
    pulido).
  - Registrar `IClock` con una implementación por defecto `SystemClock`
    (en `Infrastructure` o en un subdirectorio `UI/System` — decidir
    en la Specification; **no** en `Application`, que es donde vive
    la interfaz).
  - Registrar `IPathResolver` con `SystemPathResolver` (lee variables
    de entorno y aplica las reglas de `tech-stack.md`).
  - Inyectar el `IServiceProvider` en el arranque de Avalonia.
- En `App.axaml.cs`:
  - Cargar el `IServiceProvider` desde el host.
  - Resolver la `MainWindow` (por ahora vacía).
- Agregar el mensaje de log "App starting" al arranque y "App
  stopping" al cierre.

**Justificación**
Tener las interfaces en `Application` sin nadie que las inyecte no
prueba nada. El composition root es la **única** pieza de la
arquitectura que toca todas las capas, y configurarla ahora
(valiéndose de F-004) permite que las features futuras lleguen,
registren sus servicios y arranquen sin reorganizar nada.

> **Decisión de ubicación de las implementaciones:** `SystemClock` y
> `SystemPathResolver` son implementaciones triviales que se
> considerarán **infraestructura de runtime**, no de `Application`.
> Se crean en `Infrastructure/System/` (o `UI/System/` si se prefiere
> evitar que `UI` dependa de `Infrastructure` solo para esto; en
> cualquier caso, **no** en `Application`).

**Dependencias**
- F-001.
- F-004 (`IClock`, `IPathResolver` deben existir).

**Prioridad**
- Crítica. Es lo que cierra la fase.

**Estimación**
- M (medio día, hay que decidir bien la estructura del host en una
  app de escritorio Avalonia).

**Criterios para considerarla completada**
- La app arranca y en el log aparece "App starting" y al cerrarla
  "App stopping".
- `IClock` y `IPathResolver` se pueden resolver desde `App.axaml.cs`
  y devuelven valores correctos (la hora actual, la ruta esperada
  por SO).
- Ningún `new ConcreteClass()` aparece en `App.axaml.cs`: todo se
  resuelve desde el `IServiceProvider`.
- `NetArchTest` sigue pasando (no se rompió la arquitectura al meter
  el composition root).

---

## Orden recomendado de implementación

1. **F-001** — sin esto no hay nada.
2. **F-004** — desbloquea F-008 y de paso fija las dos interfaces más
   importantes para testeabilidad.
3. **F-005** — fija el patrón de error/result.
4. **F-006** — fija los contratos de persistencia.
5. **F-002** y **F-003** — value objects. Se pueden hacer en paralelo
   o en cualquier orden; juntas cubren todo el modelo de datos
   abstracto.
6. **F-007** — NetArchTest. Idealmente antes de F-008, para que
   cualquier violación posterior se detecte.
7. **F-008** — composition root. Cierra la fase.

## Trabajo en paralelo posible

Con dos personas:
- Persona A: F-002 + F-003 (value objects de Domain).
- Persona B: F-004 + F-005 + F-006 + F-007 (abstracciones de
  Application y reglas arquitectónicas).
- Luego ambos: F-008.

## Riesgos de la fase

- **Conflicto roadmap vs arquitectura** sobre ubicación de `IClock` /
  `IPathResolver` (resuelto en F-004 a favor de `Application`).
- **NetArchTest mal configurado** y que no detecte violaciones
  reales: mitigado con el test de "violación intencional" en los
  criterios de F-007.
- **Estilo de Avalonia + Generic Host:** la integración entre el
  ciclo de vida de Avalonia y un `IHost` de .NET no es trivial y
  puede requerir un bootstrapper intermedio. F-008 lo resuelve y
  debe documentar el patrón para no repetirlo.
- **Pinning de versiones de paquetes:** StyleCop, NetArchTest,
  Avalonia y .NET 8 tienen versiones que avanzan. Fijar versiones
  exactas en `Directory.Packages.props` para evitar drift.

## Referencias

- `docs/roadmap.md` § Fase 1 — Entregables esperados.
- `docs/architecture.md` §3 (solución y proyectos), §4 (grafo de
  dependencias), §6 (Dominio), §7 (Aplicación — abstracciones).
- `spec/constitution/tech-stack.md` — convenciones y reglas de
  paquetes.
- `spec/constitution/mission.md` — principios (Clean Architecture,
  determinismo testeable).
