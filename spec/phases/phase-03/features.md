# Fase 3 — Features: Slice vertical — registrar y listar gastos

> Descomposición de la **Fase 3** del `docs/roadmap.md` en features
> independientes, cada una candidata a una Specification propia.
>
> **Ubicación de este documento:** `spec/phases/phase-03/features.md`.
> Las specs de cada feature vivirán en `spec/features/phase-03/`.
> Ver `spec/README.md` para la convención completa del repo.
>
> Fuente de verdad: `docs/vision.md`, `docs/architecture.md`,
> `docs/roadmap.md` (§Fase 3), `spec/constitution/tech-stack.md`,
> `spec/constitution/mission.md`,
> `spec/phases/phase-02/features.md` (formato de referencia),
> `spec/phases/phase-01/features.md`.
>
> **Reglas de la fase:**
> - Cada feature deja la solución compilando (`dotnet build` 0 warnings,
>   `dotnet test` verde).
> - Cada feature es una capacidad única.
> - Las dependencias entre features (intra-fase y extra-fase) se
>   indican explícitamente.
> - Una feature completada = sus criterios de cierre cumplidos.
> - Trazabilidad: cada feature traza a `docs/vision.md` §4.1
>   (Transacciones) o §4.3 (Búsqueda y vistas — sólo el subconjunto
>   "listar") y a `docs/roadmap.md` §Fase 3 (Entregables esperados).
>
> **Heredado de Phase 1 (ya ✅ COMPLETADA):**
> - `Money` + `Currency` (`Money` value object con validación de
>   redondeo y operaciones; `Currency` enum `DOP`/`USD`).
> - `DateRange` value object.
> - `IClock` (`SystemClock` implementado) e `IPathResolver`
>   (`SystemPathResolver` implementado) en `Application/Abstractions/`.
> - `Result` **no-genérico** (con `Value: object?`, factories
>   `Success(object)` / `Failure(string, string)`) y
>   `PagedResult<T>` con constructor 4-arg
>   (`Common/Result.cs` y `Common/PagedResult.cs`). **El caller
>   hace cast** del `Value` al tipo esperado — no se usa
>   `Result<T>` en el proyecto.
> - Las **15 interfaces de repositorio** de F-006, entre ellas
>   `IExpenseRepository` con la firma estándar de 5 métodos:
>   `AddAsync`, `UpdateAsync`, `RemoveAsync`, `GetByIdAsync`,
>   `GetAllAsync`. **El método de query por filtro NO está en la
>   interfaz** — Phase 3 lo introduce (ver F-003).
> - NetArchTest R-1 a R-5 (grafo de dependencias validado).
> - `Bootstrap.ConfigureServices` (composition root con host de DI
>   y logging).
>
> **Heredado de Phase 2 (ya ✅ COMPLETADA):**
> - `AppDbContext` con `DbSet<Category>` y `DbSet<PaymentMethod>`.
> - Configuraciones Fluent API en
>   `Infrastructure/Persistence/Configurations/` (patrón a replicar
>   para `Expense` en F-002).
> - Migración `InitialSchema` (no incluye `Expense` — F-002 añade una
>   migración nueva `AddExpenseTable`).
> - `SeedData` con 9 categorías y 5 métodos de pago del sistema
>   (`IsSystem=true`); `IExpenseRepository.AddAsync` podrá validar la
>   FK contra `Categories` vía el `AppDbContext` ya configurado.
> - `CategoryRepository`, `PaymentMethodRepository` implementados.
> - `EfUnitOfWork` y `IUnitOfWork` registrados.
> - `AddPersistence` extension method + `App.axaml.cs` aplica
>   migraciones al arranque.
> - `TestAppDbContextFactory` reutilizable (SQLite in-memory
>   compartida) — F-008/I tests de `ExpenseRepository` lo reusarán.

---

## Resumen

| ID | Nombre | Prioridad | Estimación | Depende de | Estado |
| --- | --- | --- | --- | --- | --- |
| 001 | Entidad `Expense` en Domain con invariantes | Crítica | M | Phase 1 F-002 (Money) | ✅ COMPLETADA |
| 002 | `ExpenseConfiguration` + migración `AddExpenseTable` | Crítica | S | 001 | ✅ COMPLETADA |
| 003 | `ExpenseRepository` + método de query paginado en `IExpenseRepository` | Crítica | M | 002 | ✅ COMPLETADA |
| 004 | Application: DTOs + `RegisterExpenseValidator` + `RegisterExpenseUseCase` | Crítica | M | 001 | ✅ COMPLETADA |
| 005 | Application: DTOs + `GetTransactionsQuery` (paginado, sin filtros) | Alta | M | 003 | ✅ COMPLETADA |
| 006 | UI: shell, `ShellViewModel`, navegación por contenido, DataTemplates | Alta | L | — | ✅ COMPLETADA |
| 007 | UI: `ExpensesViewModel` + `ExpensesView` + ViewModel tests | Crítica | L | 004, 005, 006 | ✅ COMPLETADA |

**Tamaño total estimado:** 1–2 semanas de trabajo para una persona.
**MVP de la fase:** la fase **toda** es el MVP — es un slice vertical
sin el cual no hay valor de usuario end-to-end. La feature que "cierra"
el slice y produce valor visible es **F-007**; sin las 6 anteriores no
hay slice. Por eso casi todas son Críticas. F-006 (shell +
navegación) se marca como Alta porque su entrega aislada no muestra
un gasto al usuario, pero es prerrequisito técnico de F-007.

> **Nota sobre asignación de IDs:** los IDs `F-001` a `F-007` están
> **scoped a Phase 3**. Reaprovechar los IDs de Phase 1 y Phase 2
> (que también usan `F-001`, `F-002`, etc.) es intencional: el
> "**namespace**" de un ID es la fase, no el repo. Esta convención
> coincide con la que ya utilizan `spec/phases/phase-01/features.md`
> y `spec/phases/phase-02/features.md`.

---

## F-001 — Entidad `Expense` en Domain con invariantes — ✅ COMPLETADA

**Objetivo**
Introducir la entidad `Expense` como la primera transacción del
modelo de dominio, con sus campos mínimos (`Id`, `CategoryId`,
`Date`, `Amount`, `Description`, `Note?`, `CreatedAt`, `UpdatedAt`),
los invariantes de negocio (monto positivo, fecha válida,
descripción requerida, categoría asignada) y los unit tests que los
blinden. Sin persistencia, sin UI.

**Descripción breve**
- Crear `src/ControlDeFinanzasIA.Domain/Entities/Expense.cs`:
  - `public sealed class Expense`.
  - Propiedades: `Id` (`Guid`), `CategoryId` (`Guid`), `Date`
    (`DateOnly`), `Amount` (`Money` — Phase 1 F-002), `Description`
    (`string`), `Note` (`string?`), `CreatedAt` (`DateTime` UTC),
    `UpdatedAt` (`DateTime` UTC).
  - Factory method estático `public static Expense Create(...)`
    que valida invariantes y devuelve una `Expense` ya válida o
    lanza `ArgumentException` con mensaje descriptivo (Domain usa
    excepciones para invariantes que violan contrato; `Result<T>`
    se reserva para errores esperados en Application).
  - Método `Update(...)` que reasigna `Description`, `Note`,
    `CategoryId`, `Amount`, `Date` y refresca `UpdatedAt`.
  - Constructor sin parámetros `private` para EF Core (uso
    exclusivo de la infraestructura de persistencia; `internal` o
    `private` según convención de F-002 de Phase 2 — replicar).
- **Invariantes** (validados en `Create` y `Update`):
  - `Amount.Amount > 0` (un gasto de monto cero o negativo no es
    gasto; decisión a confirmar en la Specification de esta
    feature — ver "Open questions").
  - `Amount.Currency` es un valor válido del enum `Currency`
    (garantizado por el value object `Money`; no redundante en
    `Expense`).
  - `Description` no vacío ni sólo espacios; `HasMaxLength(200)`
    (decisión de longitud en F-002).
  - `Note`, si presente, `HasMaxLength(500)` (decisión de longitud
    en F-002).
  - `CategoryId != Guid.Empty`.
  - `Date` no futura (decisión abierta — aceptar fechas futuras
    planeadas? Vision §4.1 no prohíbe gastos planificados. Ver
    "Open questions" más abajo).
- NO se incluye `PaymentMethodId`, `TagIds`, `SourceRuleId`,
  ni propiedad `Currency` separada — esos campos llegan en
  Phase 4 (tags) y Phase 5 (método de pago y multi-moneda real
  en `Transaction`). `Amount` ya lleva `Currency` embebido, pero
  en Phase 3 sólo se persistirá `Currency.DOP` por defecto
  (ver F-004).
- Tests unitarios en
  `tests/ControlDeFinanzasIA.Tests.Unit/Domain/ExpenseTests.cs`:
  - `Create_WithValidArgs_ReturnsExpenseWithAudit`.
  - `Create_WithNegativeAmount_Throws`.
  - `Create_WithZeroAmount_Throws`.
  - `Create_WithEmptyDescription_Throws`.
  - `Create_WithWhitespaceDescription_Throws`.
  - `Create_WithEmptyCategoryId_Throws`.
  - `Create_WithEmptyNote_AllowsNull`.
  - `Update_WithValidArgs_RefreshesUpdatedAt`.
  - `Update_WithInvalidArgs_Throws`.
- No se introduce la base abstracta `Transaction` (ver
  "Decisiones de diseño" al final y "Riesgos de la fase").

**Justificación**
La entidad es el contrato que todas las capas posteriores
(Application, Infrastructure, UI) consumen. Definirla primero
con invariantes blindados evita que reglas de negocio se
  filtren al repositorio o al ViewModel. Trazabilidad: `docs/vision.md`
§4.1 (Gastos del día a día) y §4.2 (organización de un movimiento
— subconjunto mínimo: fecha, monto+moneda, categoría, nota
opcional; sin tags ni método todavía). `docs/roadmap.md` §Fase 3
lista explícitamente los campos `CategoryId`, `Date`, `Amount`,
`Description`, `Note?`.

**Dependencias**
- Phase 1 F-002 (`Money` value object y `Currency` enum).

**Prioridad**
- Crítica. Bloquea F-002 (configuración), F-003 (repo) y F-004
  (use case de escritura). Sin la entidad no hay nodo inicial
  del slice vertical.

**Estimación**
- M (medio día). La entidad es pequeña pero los invariantes y
  tests blindados consumen tiempo de diseño.

**Criterios para considerarla completada**
- `src/ControlDeFinanzasIA.Domain/Entities/Expense.cs` existe,
  compila y la entidad está blindada por invariants (`Create`
  lanza ante argumentos inválidos).
- `ExpenseTests.cs` existe con al menos los 9 casos listados y
  pasan en `Tests.Unit`.
- `dotnet build` desde la raíz: 0 warnings, 0 errors.
- `dotnet test`: 131 tests existentes verdes + ~9 nuevos de
  `Expense` ≈ 140 verdes.
- NetArchTest R-1 a R-5 siguen pasando: `Domain` sigue sin
  referenciar a nadie (la nueva entidad no introduce depedencias).
- La entidad no referencia EF Core ni Avalonia (verificación de
  NetArchTest o inspección de `Domain.csproj`).

---

## F-002 — `ExpenseConfiguration` + migración `AddExpenseTable` — ✅ COMPLETADA

**Objetivo**
Mapear la entidad `Expense` a SQLite siguiendo el patrón Fluent
API establecido en F-002 de Phase 2, introducir el **primer
`ValueConverter` de value object** en el proyecto (`Money` → dos
columnas primitivas `Amount` decimal + `Currency` int), y producir
una nueva migración versionada `AddExpenseTable` que añada la tabla
sin destruir el schema existente.

**Descripción breve**
- Crear
  `src/ControlDeFinanzasIA.Infrastructure/Persistence/Configurations/ExpenseConfiguration.cs`:
  - `public sealed class ExpenseConfiguration : IEntityTypeConfiguration<Expense>`.
  - `b.ToTable("Expenses")`.
  - `b.HasKey(e => e.Id)`; `b.Property(e => e.Id).ValueGeneratedNever()`
    (Guid generado en Domain).
  - `b.Property(e => e.CategoryId).IsRequired()`.
  - `b.Property(e => e.Date).IsRequired()` (`DateOnly` — EF Core 8
    lo mapea nativamente a `TEXT` en ISO-8601; verificación vía
    Context7 si surgen edge cases).
  - **`Money` como `OwnsOne(e => e.Amount)` en dos columnas
    primitivas** (decisión resuelta en la Specification de esta
    feature — **Opción A adoptada**, ver
    `spec/features/phase-03/F-002-expense-configuration-migration.spec.md`
    §17.1; Opción B descartada por indexabilidad y replicabilidad):
    - Opción A (recomendada): `b.Property(e => e.Amount.Amount)`
      no es accesible directamente porque `Money.Amount` no es
      propiedad navegable. En su lugar, usar *shadow / owned
      entity*: `b.OwnsOne(e => e.Amount, money => { money.Property(m
      => m.Amount).HasColumnName("Amount").IsRequired(); money.Property(m
      => m.Currency).HasColumnName("Currency").IsRequired(); })`.
    - Opción B: `ValueConverter<Money, string>` serializando
      `"1200.50|DOP"` en una sola columna `AmountJson`. **No
      recomendable**: impediría indexar y consultar por monto.
  - `b.Property(e => e.Description).IsRequired().HasMaxLength(200)`.
  - `b.Property(e => e.Note).HasMaxLength(500)` (nullable).
  - `b.Property(e => e.CreatedAt).IsRequired()`.
  - `b.Property(e => e.UpdatedAt).IsRequired()`.
  - `b.HasIndex(e => new { e.Date })` (queries por fecha — Phase 4
    podrá añadir índice compuesto con `CategoryId`).
  - **FK a `Categories`**: `b.HasOne<Category>().WithMany()
    .HasForeignKey(e => e.CategoryId).OnDelete(ReferentialAction.Restrict)`.
    `Restrict` para que borrar una categoría con gastos pueda
    bloquearse en la capa de Application (visión §4.2: categorías
    planas configurables; Phase 4 define el comportamiento de
    borrado).
- `AppDbContext.OnModelCreating` ya llama a
  `ApplyConfigurationsFromAssembly` — la nueva configuración se
  carga automáticamente; **no se edita**`OnModelCreating`.
- Generar la migración:
  `dotnet ef migrations add AddExpenseTable --project src/ControlDeFinanzasIA.Infrastructure --startup-project src/ControlDeFinanzasIA.Infrastructure`.
- Los 3 archivos resultantes en
  `src/ControlDeFinanzasIA.Infrastructure/Persistence/Migrations/`
  se commitean; el `AppDbContextModelSnapshot.cs` se actualiza.
- La migración NO toca `Categories` ni `PaymentMethods` (es
  aditiva: crea `Expenses` + índice + FK).

**Justificación**
Sin mapeo y migración no hay persistencia de gastos. Esta feature
materializa el riesgo diferido de Phase 2 (Fase 2 Risk #1):
**primera conversión real de un value object (`Money`) a columnas
primitivas en EF Core**. El patrón que se fija aquí será replicado
en `RecurringRule`, `Debt`, `Goal`, `Budget` (todas con `Money`).
Hacerlo bien ahorra retrofit cuando ya haya 6 entidades con `Money`.

**Dependencias**
- F-001 de Phase 3 (existe `Expense`).
- F-002 de Phase 2 (patrón de configuraciones, convención
  PascalCase para `ToTable`, decisión `ValueGeneratedNever`).
- `SeedData` de Phase 2 (la FK a `Categories` requiere que la
  tabla `Categories` exista — garantizado por la migración
  `InitialSchema`).

**Prioridad**
- Crítica. Bloquea F-003 (`ExpenseRepository`).

**Estimación**
- S (1–2 horas). El patrón está fijado en F-002 de Phase 2; el
  trabajo nuevo es el `OwnsOne` de `Money` y la FK.

**Criterios para considerarla completada**
- `ExpenseConfiguration.cs` existe y compila.
- `dotnet ef migrations add AddExpenseTable` se genera sin error
  y los 3 archivos resultantes están commiteados.
- El método `Up` de `AddExpenseTable` crea la tabla `Expenses`
  con columnas `Id`, `CategoryId`, `Date`, `Amount`, `Currency`,
  `Description`, `Note`, `CreatedAt`, `UpdatedAt`, el índice
  `IX_Expenses_Date`, y la FK a `Categories`.
- `dotnet build`: 0 warnings, 0 errors.
- `dotnet test`: 164 existentes (los tests de F-001 siguen
  pasando) + 3 nuevos de F-002 = **~167 verdes**. F-002 sí
  añade tests: 3 schema tests de integración en
  `ExpenseSchemaTests.cs` (validan columnas, índices, FK y
  `OwnsOne(Money)`) per el spec aprobado en
  `spec/features/phase-03/F-002-expense-configuration-migration.spec.md`
  §11. Los tests CRUD del repositorio siguen quedando en
  F-003 según lo planificado originalmente.
- `dotnet run --project src/ControlDeFinanzasIA.UI` arranca,
  aplica la migración nueva en un equipo/DB con `InitialSchema`
  ya aplicada y deja `Expenses` creada **sin** perder `Categories`
  ni `PaymentMethods` (verificable: las 9 categorías sembradas
  siguen tras el arranque).
- NetArchTest R-1 a R-5 siguen pasando.

---

## F-003 — `ExpenseRepository` + método de query paginado en `IExpenseRepository` — ✅ COMPLETADA

**Objetivo**
Implementar el repositorio concreto `ExpenseRepository` que
satisface la interface `IExpenseRepository` definida en F-006 de
Phase 1 con los 5 métodos estándar, **extender** la interface con
un método `GetByFilterAsync` para queries paginadas (que
`GetTransactionsQuery` en F-005 consumirá), y dejar tests de
integración con SQLite in-memory que validen CRUD y queries.

**Descripción breve**
- **Modificar** `src/ControlDeFinanzasIA.Application/Abstractions/IExpenseRepository.cs`
  (firma casi completa listada en F-006 de Phase 1 — los 5
  métodos ya están; aquí se **añade** el sexto):
  - `Task<PagedResult<Expense>> GetByFilterAsync(ExpenseFilter filter, CancellationToken ct = default);`
  - Crear `ExpenseFilter` record en
    `Application/Abstractions/Queries/ExpenseFilter.cs` (o en
    `Application/Common/`): propiedades `Page` (`int`, 1-based),
    `PageSize` (`int`, default 50), `CategoryId?` (`Guid?`),
    `DateFrom?` (`DateOnly?`), `DateTo?` (`DateOnly?`). En
    Phase 3 los filtros `CategoryId`/`DateFrom`/`DateTo` se
    definen pero la UI no los expone todavía (Phase 4 los
    enchufa); así `GetTransactionsQuery` de F-005 puede quedar
    paginado ahora y extender filtros en Phase 4 sin romper la
    firma.
- Crear
  `src/ControlDeFinanzasIA.Infrastructure/Persistence/Repositories/ExpenseRepository.cs`:
  - `public sealed class ExpenseRepository : IExpenseRepository`.
  - Constructor que recibe `AppDbContext`.
  - `AddAsync`: chequea que `CategoryId` exista en `Categories`
    (vía `_db.Categories.FindAsync`); si no, devuelve
    `Result.Failure("CategoryNotFound", "...")`. Luego `Add` +
    `SaveChangesAsync`.
  - `UpdateAsync`: busca; si no existe
    `Result.Failure("NotFound", "...")`; actualiza
    `CurrentValues.SetValues` + `SaveChangesAsync`.
  - `RemoveAsync`: busca; si no existe
    `Result.Failure("NotFound")`; `Remove` + `SaveChangesAsync`.
  - `GetByIdAsync`: `FindAsync`.
  - `GetAllAsync`: `AsNoTracking().OrderByDescending(e =>
    e.Date).ToListAsync()` (orden: defecto descendente por fecha
    — los más recientes primero; decisión replicable a otras
    transacciones).
  - `GetByFilterAsync`: construye el query con `IQueryable`
    aplicando los filtros opcionales, ordena por `Date`
    descendente, calcula `totalCount` con `CountAsync`, aplica
    `Skip`/`Take` según `Page` y `PageSize`, devuelve
    `PagedResult<Expense>` (Phase 1 F-005 ya provee el tipo).
- Registra `IExpenseRepository` → `ExpenseRepository` en el
  composition root: extender `AddPersistence` en
  `Infrastructure/Persistence/ServiceCollectionExtensions.cs`
  con `services.AddScoped<IExpenseRepository, ExpenseRepository>();`.
- Tests de integración en
  `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/ExpenseRepositoryTests.cs`
  usando `TestAppDbContextFactory` (de Phase 2 F-006):
  - `AddAsync_WithValidExpense_Persists`.
  - `AddAsync_WithUnknownCategoryId_ReturnsCategoryNotFound`.
  - `GetByIdAsync_Existing_ReturnsExpense`.
  - `GetByIdAsync_Missing_ReturnsNull`.
  - `GetAllAsync_ReturnsAllOrderedByDateDesc`.
  - `GetByFilterAsync_NoFilter_ReturnsFirstPageOrdered`.
  - `GetByFilterAsync_WithDateRange_FiltersCorrectly`.
  - `GetByFilterAsync_Paginates_CorrectTotalAndItems`.
  - `UpdateAsync_Existing_UpdatesFields`.
  - `RemoveAsync_Existing_DeletesExpense`.

**Justificación**
El repositorio es el adaptador de persistencia que la capa de
Application invoca. Esta feature cristaliza el patrón repo-con-FK
+ query-por-filtro que Phase 4 heredará para `Income`,
`UpdateTransaction`, filtros avanzados y vistas guardadas.

**Dependencias**
- F-002 de Phase 3 (tabla `Expenses` existe; FK a `Categories`
  aplicada).
- F-006 de Phase 1 (`IExpenseRepository` interface a implementar
  y extender).
- F-005 de Phase 1 (`PagedResult<T>` para `GetByFilterAsync`).
- Phase 2 F-006 (`TestAppDbContextFactory` para los tests).

**Prioridad**
- Crítica. Bloquea F-005 (`GetTransactionsQuery` consume el
  método nuevo `GetByFilterAsync`). Sin repositorio, los use
  cases de Application son ideas sin persistencia.

**Estimación**
- M (medio día a un día). El mapeo y los 5 métodos básicos son
  triviales (patrón Phase 2 F-004); el método de query paginado
  y los ~10 tests de integración llevan tiempo.

**Criterios para considerarla completada**
- `IExpenseRepository.cs` extendido con `GetByFilterAsync`;
  `ExpenseFilter` record existe en `Application`.
- `ExpenseRepository.cs` existe e implementa los 6 métodos (los
  5 originales con la firma exacta de F-006 de Phase 1 + el
  nuevo `GetByFilterAsync`).
- `AddPersistence` registra `IExpenseRepository` scoped.
- `ExpenseRepositoryTests.cs` existe con al menos 10 tests
  cubriendo CRUD, FK validation, paginación y filtros por
  rango de fecha; todos pasan en `Tests.Integration` con
  SQLite in-memory.
- `dotnet build`: 0 warnings, 0 errors.
- `dotnet test`: ~140 + ~10 ≈ 150 verdes.
- NetArchTest R-1 a R-5 siguen pasando (la extensión de la
  interface en `Application` no rompe reglas; el repo en
  `Infrastructure` implementa la interface de `Application`,
  patrón ya permitido).

---

## F-004 — Application: DTOs + `RegisterExpenseValidator` + `RegisterExpenseUseCase` — ✅ COMPLETADA

**Objetivo**
Implementar el primer use case de escritura: validar el comando,
mapearlo a la entidad `Expense`, persistir vía `IExpenseRepository.AddAsync`,
devolver un `Result<ExpenseDto>`. Introducir el patrón FluentValidation
+ DTO + mapeo manual que todas las features de Application posteriores
replicarán.

**Descripción breve**
- Crear `src/ControlDeFinanzasIA.Application/DTOs/ExpenseDto.cs`:
  - `public sealed record ExpenseDto(Guid Id, Guid CategoryId,
    DateOnly Date, decimal Amount, Currency Currency, string
    Description, string? Note, DateTime CreatedAt, DateTime UpdatedAt);`
  - Método de extensión `ToDto()` en
    `Application/DTOs/ExpenseExtensions.cs`: `public static ExpenseDto
    ToDto(this Expense e) => new(...)`.
- Crear
  `src/ControlDeFinanzasIA.Application/UseCases/Expenses/RegisterExpense/RegisterExpenseCommand.cs`:
  - `public sealed record RegisterExpenseCommand(Guid CategoryId,
    DateOnly Date, decimal Amount, Currency Currency, string Description,
    string? Note);` (sin `Id` ni audit — los genera la entidad).
- Crear
  `src/ControlDeFinanzasIA.Application/UseCases/Expenses/RegisterExpense/RegisterExpenseValidator.cs`:
  - `public sealed class RegisterExpenseValidator :
    AbstractValidator<RegisterExpenseCommand>`.
  - Reglas: `CategoryId` no `Guid.Empty`; `Date` no futura (decisión
    en la Specification — ver "Open questions"); `Amount > 0`;
    `Description` no vacío, longitud ≤ 200; `Note` longitud ≤ 500
    si no null; `Currency` enum válido (`IsInEnum()`).
- Crear
  `src/ControlDeFinanzasIA.Application/UseCases/Expenses/RegisterExpense/RegisterExpenseUseCase.cs`:
  - `public sealed class RegisterExpenseUseCase`.
  - Constructor inyecta `IExpenseRepository`, `IClock`, el validador
    y un `ILogger<RegisterExpenseUseCase>`.
  - `public async Task<Result<ExpenseDto>> ExecuteAsync(
    RegisterExpenseCommand command, CancellationToken ct = default)`.
  - Flujo: validar → si invalid, devolver `Result.Failure("Validation",
    errors)`; construir `Expense.Create(...)` (la propia entidad puede
    lanzar por invariantes — capturar y mapear a `Result.Failure` si
    aún quedan casos no cubiertos por el validador); llamar
    `repo.AddAsync`; mapear a `ExpenseDto` y devolver
    `Result.Success(dto)`.
  - `CreatedAt` y `UpdatedAt` provienen de `IClock.UtcNow` (no de
    `DateTime.UtcNow` directo — principio ADR-007).
- Registar `RegisterExpenseUseCase` (y el validador) en
  `Bootstrap.ConfigureServices` (transient).
- Tests unitarios en
  `tests/ControlDeFinanzasIA.Tests.Unit/Application/Expenses/RegisterExpenseUseCaseTests.cs`:
  - `ExecuteAsync_ValidCommand_ReturnsSuccessWithDto`.
  - `ExecuteAsync_InvalidAmount_ReturnsValidationFailure`.
  - `ExecuteAsync_EmptyDescription_ReturnsValidationFailure`.
  - `ExecuteAsync_FutureDate_ReturnsValidationFailure` (si se
    decide rechazar fechas futuras).
  - `ExecuteAsync_UnknownCategoryId_ReturnsCategoryNotFound`
    (mockeando `IExpenseRepository.AddAsync` para devolver
    `Result.Failure("CategoryNotFound")`).
  - `ExecuteAsync_AddAsyncSucceeds_AuditDatesFromClock` (verifica
    que se usó `IClock.UtcNow`, no `DateTime.UtcNow`).
- Tests del validador (FluentValidation) en
  `RegisterExpenseValidatorTests.cs` con casos válidos e inválidos
  parametrizados.

**Justificación**
F-004 es la **primera implementación del patrón use case** del
proyecto: validación previa con FluentValidation, mapeo manual,
`Result<T>` de retorno, `IClock` inyectado. Si este patrón encaja,
**todos los use cases futuros son iteración del mismo molde**.
Trazabilidad: `vision.md` §4.1 (Gastos del día a día) y
`architecture.md` §7 (use cases devuelven `Result<T>`).

**Dependencias**
- F-001 de Phase 3 (entidad `Expense`).
- F-006 de Phase 1 (`IExpenseRepository` interface con `AddAsync`).
- F-005 de Phase 1 (`Result` no-genérico con `Value: object?`).
  **No** se usa `Result<T>` en el proyecto — el caller hace cast
  del `Value` al tipo esperado.
- F-004 de Phase 1 (`IClock`).
- FluentValidation (paquete a añadir a
  `Directory.Packages.props` vía CPM; Phase 1 no lo añadió — el
  primer use case lo introduce).

**Prioridad**
- Crítica. Bloquea F-007 (el ViewModel invoca el use case para
  registrar). Sin F-004 no hay comando de escritura en UI.

**Estimación**
- M (medio día a un día). Aunque pequeño, es el primer caso del
  patrón y merece tiempo de diseño.

**Criterios para considerarla completada**
- `ExpenseDto`, `ExpenseExtensions.ToDto`, `RegisterExpenseCommand`,
  `RegisterExpenseValidator`, `RegisterExpenseUseCase` existen y
  compilan en `ControlDeFinanzasIA.Application`.
- `FluentValidation` añadido a `Directory.Packages.props` con
  versión exacta (CPM); `Application.csproj` lo referencia sin
  `Version`.
- `Bootstrap.ConfigureServices` registra el use case y el
  validador (transient).
- `dotnet build`: 0 warnings, 0 errors.
- `dotnet test`: ~150 + ~8 nuevos ≈ 158 verdes.
- NetArchTest R-1 a R-5 siguen pasando: `Application` referencia
  sólo a `Domain` + BCL + `FluentValidation` (paquete neutro,
  permitido por la regla R-1 "Application no referencia
  Infrastructure ni UI").
- El test `AddAsyncSucceeds_AuditDatesFromClock` verifica que
  **no** se usa `DateTime.UtcNow` (revisión manual de diff o
  aserción sobre el `CreatedAt` con un `IClock` mockeado que
  devuelve una fecha fija conocida).

---

## F-005 — Application: DTOs + `GetTransactionsQuery` (paginado, sin filtros UI) — ✅ COMPLETADA

**Objetivo**
Implementar el lado de lectura del slice: el query que devuelve
una página de gastos ordenados por fecha descendente, paginado
vía `PagedResult<ExpenseDto>`. Sin filtros expuestos en la UI
todavía (Phase 4 los introduce), pero la firma ya acepta
`ExpenseFilter` para que Phase 4 sólo enchufe UI.

**Descripción breve**
- Crear
  `src/ControlDeFinanzasIA.Application/UseCases/Expenses/GetTransactions/GetTransactionsQueryRequest.cs`:
  - `public sealed record GetTransactionsQueryRequest(int Page = 1,
    int PageSize = 50);`.
- Crear
  `src/ControlDeFinanzasIA.Application/UseCases/Expenses/GetTransactions/GetTransactionsQuery.cs`:
  - `public sealed class GetTransactionsQuery`.
  - Constructor inyecta `IExpenseRepository` y
    `ILogger<GetTransactionsQuery>`.
  - `public async Task<Result> ExecuteAsync(
    GetTransactionsQueryRequest request,
    CancellationToken ct = default)`. **`Result` es no-genérico**
    (Phase 1 F-005: `Value: object?`); el caller hace
    `(PagedResult<ExpenseDto>)result.Value!` tras verificar
    `IsSuccess`.
  - Construye `ExpenseFilter` desde `request` (sin `CategoryId`
    ni rango de fecha — Phase 4 los añade).
  - Llama `repo.GetByFilterAsync(filter, ct)`; mapea cada
    `Expense` a `ExpenseDto` (reutiliza `ExpenseExtensions.ToDto`
    de F-004); devuelve `Result.Success(pagedDto)`.
  - Maneja el caso "página vacía" y "página fuera de rango"
    con el constructor 4-arg de `PagedResult<ExpenseDto>`:
    `new PagedResult<ExpenseDto>(Array.Empty<ExpenseDto>(),
    paged.TotalCount, requestedPage, requestedPageSize)`. **No
    existe** factory `PagedResult.Empty` en el proyecto.
- Registrar el query en `Bootstrap` (transient).
- Tests unitarios en
  `tests/ControlDeFinanzasIA.Tests.Unit/Application/Expenses/GetTransactionsQueryTests.cs`
  usando un **fake hand-rolled** `FakeExpenseRepository`
  extendido (de F-004) con `SetFilterResult(...)` y
  `SetThrowOnGetFilter(...)`. El proyecto no usa mocking
  library; los fakes viven en
  `tests/.../TestDoubles/`. Para el `ILogger<>` se usa
  `NullLogger<T>` de `Microsoft.Extensions.Logging.Abstractions`:
  - `ExecuteAsync_EmptyRepo_ReturnsEmptyPagedResult`.
  - `ExecuteAsync_WithItems_ReturnsMappedDtosOrderedDesc`
    (verifica que los DTOs preservan el orden del repo).
  - `ExecuteAsync_PageBeyondEnd_ReturnsEmptyItems_CorrectTotal`
    (el totalCount refleja el total real, los items están
    vacíos en una página fuera de rango; **no** es un error).
  - `ExecuteAsync_PropagatesRepositoryFailure` (si el repo
    lanza una excepción distinta de `OperationCanceledException`,
    el query la captura, loguea con `ILogger.LogError(ex, ...)`,
    y devuelve `Result.Failure("RepositoryError", "...")`. El
    `OperationCanceledException` se **rethrowa** — la cancelación
    no es un error).

**Justificación**
Cierra el lado lectura del slice vertical. Sin F-005, el
`ExpensesViewModel` de F-007 registra pero no ve nada. La firma
`ExpenseFilter` ya definida en F-003 garantiza que Phase 4
introducirá filtros sin tocar este use case.

**Dependencias**
- F-003 de Phase 3 (método `GetByFilterAsync` en
  `IExpenseRepository`).
- F-004 de Phase 3 (`ExpenseDto` y `ExpenseExtensions.ToDto`).
- F-005 de Phase 1 (`PagedResult<T>`).

**Prioridad**
- Alta. Es la rama de lectura del slice. Podría diferirse si
  F-007 se pusheara con un listing vacío, pero no aporta valor
  visible sin él.

**Estimación**
- M (medio día). La query itself es simple; el peso está en los
  tests de borde de paginación.

**Criterios para considerarla completada**
- `GetTransactionsQueryRequest`, `GetTransactionsQuery` existen
  y compilan en `Application`.
- `Bootstrap.ConfigureServices` registra el query (transient).
- `GetTransactionsQueryTests.cs` existe con al menos 4 tests
  cubriendo empty / con items / paginación fuera de rango /
  propagación de error; todos pasan en `Tests.Unit`.
- `dotnet build`: 0 warnings, 0 errors.
- `dotnet test`: 177 heredados (post F-001..F-004) + 8 nuevos tests
  del query (10 cases con `[Theory]`/`[InlineData]`) ≈ 204 verdes.
- NetArchTest R-1 a R-5 siguen pasando.

---

## F-006 — UI: shell, `ShellViewModel`, navegación por contenido, DataTemplates — ✅ COMPLETADA

**Objetivo**
Establecer el esqueleto de UI de la app: `ShellViewModel` con
`CurrentViewModel`, `INavigationService` con pila de historial in
memory, `RouteNames`, layout del `MainWindow` con sidebar + content
area, y el registro de `DataTemplate`s en `App.axaml` que asocia
`ViewModel → View`. Sin `ExpensesView` todavía — esta feature
introduce la **infraestructura** que F-007 enchufará con la feature
de gastos.

**Descripción breve**
- Crear
  `src/ControlDeFinanzasIA.UI/Navigation/RouteNames.cs`:
  - `public static class RouteNames { public const string Dashboard
    = "Dashboard"; public const string Expenses = "Expenses"; }`
    (Phase 3 sólo necesita los dos; fases futuras añaden más).
- Crear
  `src/ControlDeFinanzasIA.UI/Navigation/INavigationService.cs`:
  - `public interface INavigationService { ViewModelBase CurrentViewModel
    { get; } void NavigateTo(string routeName); void GoBack();
    bool CanGoBack { get; } event EventHandler? CurrentChanged; }`
  - `ViewModelBase` es una clase base abstracta extendiendo
    `ObservableObject` que todos los ViewModels concretos heredan.
- Crear
  `src/ControlDeFinanzasIA.UI/Navigation/NavigationService.cs`:
  - `public sealed class NavigationService : ObservableObject,
    INavigationService`.
  - Resuelve ViewModels por `routeName` desde un `IServiceProvider`
    (o un `IDictionary<string, Func<ViewModelBase>>`).
  - Mantiene `Stack<ViewModelBase> _history`.
  - `NavigateTo(routeName)`: resuelve el VM desde DI, push sobre
    la pila, asigna `CurrentViewModel`, dispara
    `OnPropertyChanged(nameof(CurrentViewModel))` y `CurrentChanged`.
  - `GoBack()`: pop si `CanGoBack`, actualiza `CurrentViewModel`.
- Crear
  `src/ControlDeFinanzasIA.UI/ViewModels/ShellViewModel.cs`:
  - `public sealed class ShellViewModel : ObservableObject`.
  - `INavigationsService` inyectado (o bien el `ShellViewModel`
    **es** el navigation service: decisión menor — preferimos
    separar primero por testabilidad, fusionar más adelante si
    es muy pequeño). Decisión: en Phase 3 **separar**; facilita
    mockear en tests.
  - `[ObservableProperty] private ViewModelBase? current;`
    bindeado a `INavigationService.CurrentViewModel`.
  - Comandos `[RelayCommand] NavigateTo(route)` y
    `[RelayCommand] GoBack` delegan al servicio.
- Modificar
  `src/ControlDeFinanzasIA.UI/Views/MainWindow.axaml`:
  - Layout `DockPanel` con: sidebar a la izquierda (`ListBox` de
    rutas), `ContentControl` centrado bindeado a
    `ShellViewModel.Current`, statusbar abajo (placeholder).
- Modificar `src/ControlDeFinanzasIA.UI/App.axaml`:
  - Registrar `DataTemplate`s: por ahora un placeholder para
    `ShellViewModel` y un `DashboardViewModel` vacío que
    muestra "Dashboard placeholder". F-007 añade
    `ExpensesViewModel → ExpensesView`.
- Modificar `src/ControlDeFinanzasIA.UI/App.axaml.cs`:
  - Crear `ShellViewModel` desde el contenedor, asignarlo como
    `DataContext` de `MainWindow`. La navegación inicial apunta
    a `RouteNames.Dashboard`.
- Crear
  `src/ControlDeFinanzasIA.UI/ViewModels/DashboardViewModel.cs`
  + `src/ControlDeFinanzasIA.UI/Views/DashboardView.axaml`
  (vacío) — sólo existe para que la app arranque con un content
  visible y la navegación tenga a dónde navegar.
- Registrar `ShellViewModel`, `INavigationService → NavigationService`,
  `DashboardViewModel` en `Bootstrap.ConfigureServices`.
- Tests de ViewModel en
  `tests/ControlDeFinanzasIA.Tests.Unit/UI/NavigationServiceTests.cs`:
  - `NavigateTo_UnknownRoute_Throws` (o devuelve failure — decisión
    de Specification).
  - `NavigateTo_Dashboard_SetsCurrentAndPushesHistory`.
  - `NavigateTo_SecondRoute_ThenGoBack_RestoresPreviousCurrent`.
  - `GoBack_WithoutHistory_ThrowsOrNoop`.

**Justificación**
Esta feature entrega el **contrato de navegación** que todas las
features de producto siguientes consumen (`Phase 4` para Ingresos,
`Phase 6` para Suscripciones, etc.). Separarla de `ExpensesView`
(F-007) sigue el principio "una sola capacidad por feature":
el **layout de shell** es una capacidad distinta del **formulario
de gastos**. Si se mezclan, la Specification será demasiado larga
y la mitad del código no tendrá trazabilidad al roadmap de gastos
de Phase 3. Trazabilidad: `architecture.md` §9 (capa de UI) y
§10 (Navegación).

**Dependencias**
- F-008 de Phase 1 (`Bootstrap.ConfigureServices`).
- F-005 de Phase 1 (no se requiere; `PagedResult` ya está pero no
  es necesario en esta feature).
- **No depende de F-001 a F-005 de Phase 3** — la navegación
  como infraestructura no necesita que `Expense` exista.

**Prioridad**
- Alta. Sin F-006, F-007 no tiene dónde vivir. No es Crítica
  individualmente porque no entrega valor visible al usuario (la
  ventana sin gastos sigue siendo un placeholder), pero bloquea
  F-007 que sí lo es.

**Estimación**
- L (1–2 días). La navegación + shell layout con Avalonia es
  exploratorio; la primera vez requiere calibrar `DataTemplate`s,
  bindings, lifecycle y tests con `Avalonia.Headless` (si se
  incluye en esta fase) o con simulación del `INavigationService`.

**Criterios para considerarla completada**
- `RouteNames`, `INavigationService`, `NavigationService`,
  `ShellViewModel`, `NavigationServiceTests`, `DashboardViewModel`,
  `DashboardView.axaml` existen y compilan.
- `MainWindow.axaml` tiene un sidebar bindeado a las rutas
  disponibles y un `ContentControl` que muestra
  `ShellViewModel.Current`.
- `App.axaml` registra `DataTemplate` para `DashboardViewModel`
  (placeholder visible, sin `ExpensesViewModel` todavía).
- `App.axaml.cs` instancia `ShellViewModel`, navega a
  `RouteNames.Dashboard` al inicializar, y su `DataContext` está
  bindeado.
- `dotnet run --project src/ControlDeFinanzasIA.UI` arranca,
  muestra la ventana con sidebar, "Dashboard" seleccionado, y
  el placeholder "Dashboard" visible en el content area.
- `dotnet build`: 0 warnings, 0 errors.
- `NavigationServiceTests` pasa con al menos 4 casos.
- `dotnet test`: ~163 + ~4 nuevos ≈ 167 verdes.
- NetArchTest R-1 a R-5 siguen pasando; la dependencia UI →
  Infrastructure sigue canalizada por `Bootstrap` (R-4 sigue
  excluyendo `Bootstrap`).

---

## F-007 — UI: `ExpensesViewModel` + `ExpensesView` + ViewModel tests — ✅ COMPLETADA

**Objetivo**
Cerrar el slice vertical: una vista `ExpensesView` con un
formulario simple para registrar un gasto (fecha, monto, moneda,
categoría, descripción, nota opcional) y una lista que muestra
los gastos registrados paginados. Navegación desde el shell.
ViewModel tests blindando estados, comandos y mapeo de errores.

**Descripción breve**
- Crear
  `src/ControlDeFinanzasIA.UI/ViewModels/ExpensesViewModel.cs`:
  - `public sealed partial class ExpensesViewModel : ViewModelBase`
    (hereda la base de F-006).
  - `[ObservableProperty]` para los campos del formulario:
    `Date` (`DateOnly`), `Amount` (`decimal`), `Currency`
    (`Currency`, default `DOP`), `SelectedCategoryId` (`Guid?`),
    `Description` (`string`), `Note` (`string?`).
  - `[ObservableProperty]` para la lista: `Expenses`
    (`ObservableCollection<ExpenseDto>`) y metadatos de
    paginación (`Page`, `TotalPages`).
  - `[ObservableProperty]` para `Categories`
    (`IReadOnlyList<CategoryDto>`) — cargadas al inicializar
    desde `ICategoryRepository` (Phase 2 ya lo implementó).
  - `[RelayCommand] RegisterExpenseAsync`:
    - Construye un `RegisterExpenseCommand` desde los campos del
      formulario.
    - Llama `RegisterExpenseUseCase.ExecuteAsync`.
    - Si `Result.Success`: limpia el formulario, recarga la
      lista (`RefreshAsync`).
    - Si `Result.Failure`: mapea los errores del `Result` a
      `ValidationErrors` (`ObservableProperty` `Dictionary<string,
      string>`) para bindings de error por campo en la vista.
  - `[RelayCommand] RefreshAsync`: llama `GetTransactionsQuery`
    con la `Page` actual; actualiza `Expenses` y `TotalPages`.
  - `[RelayCommand] NextPage` / `[RelayCommand] PreviousPage`
    invocan `RefreshAsync` tras mutar `Page`.
  - Constructor inyecta `RegisterExpenseUseCase`,
    `GetTransactionsQuery`, `ICategoryRepository`, `IClock`
    (para defaultear `Date = clock.Today`) y
    `ILogger<ExpensesViewModel>`.
- Crear
  `src/ControlDeFinanzasIA.UI/Views/ExpensesView.axaml`:
  - Formulario: `DatePicker` para `Date`, `TextBox` numérico para
    `Amount` (formato según locale — Phase 3 default es-DO),
    `ComboBox` para `Currency` (DOP/USD), `ComboBox` de categorías
    bindeado a `Categories`, `TextBox` para `Description`,
    `TextBox` multilinea para `Note`, `Button` "Registrar"
    bindeado a `RegisterExpenseCommand`.
  - Lista: `ListBox` o `DataGrid` bindeado a `Expenses`
    (decisiones de UX abiertas para la Specification — ver "Open
    questions").
  - Controles de paginación: botones "Anterior"/"Siguiente" +
    texto "Página X de Y".
  - Errores de validación: `TextBlock` por campo bindeado a
    `ValidationErrors[fieldName]`.
- Modificar `RouteNames` (F-006 ya incluyó "Expenses") y añadir
  sidebar item para gastos en `MainWindow.axaml`.
- Modificar `App.axaml`: registrar `DataTemplate`
  `ExpensesViewModel → ExpensesView` con `DataType`
  (`x:DataType="vm:ExpensesViewModel"` para CompiledBindings).
- Registrar `ExpensesViewModel` en `Bootstrap` (transient o
  scoped — decisión menor, transient es seguro porque cada
  navegación crea uno nuevo).
- Tests ViewModel en
  `tests/ControlDeFinanzasIA.Tests.Unit/UI/ExpensesViewModelTests.cs`
  con use cases mockeados:
  - `Constructor_LoadsCategories_SetsDefaultDateFromClock`.
  - `RegisterExpense_ValidForm_CallsUseCase_AndRefreshesList`.
  - `RegisterExpense_UseCaseFails_UpdatesValidationErrors`.
  - `RegisterExpense_ValidationFailsBeforeUseCase_ShowsFieldErrors`.
  - `Refresh_PopulatesExpensesAndPageMetadata`.
  - `NextPage_IncrementsPage_AndRefreshes`.
  - `PreviousPage_AtFirstPage_DoesNotDecrement`.

**Justificación**
F-007 es la **prueba de fuego** que cierra Phase 3: si aquí todo
fluye (Domain → Application → Infrastructure → UI con
navegación, MVVM, DI, `Result`, validación, tests unit + integration
+ ViewModel), el resto del proyecto es iteración del mismo patrón
(`docs/roadmap.md` §Fase 3, Justificación). Trazabilidad:
`vision.md` §4.1 (registro de gastos), §4.3 (listado básico — sin
filtros todavía); `architecture.md` §9 (UI), §10 (navegación).

**Dependencias**
- F-004 de Phase 3 (`RegisterExpenseUseCase`).
- F-005 de Phase 3 (`GetTransactionsQuery`).
- F-006 de Phase 3 (`ShellViewModel`, `INavigationService`,
  `ViewModelBase`, `RouteNames`, infra de DataTemplate).
- Phase 2 F-004 (`ICategoryRepository` implementado, para listar
  categorías en el `ComboBox`).
- Phase 1 F-004 (`IClock` para defaultear `Date`).

**Prioridad**
- Crítica. Es la feature que entrega valor visible y cierra la
fase. Sin F-007, F-001 a F-006 son código sin usuario.

**Estimación**
- L (1–2 días). MVVM en Avalonia con bindings, validación en
  vivo, paginación y tests del ViewModel es el grueso de la fase.

**Criterios para considerarla completada**
- `ExpensesViewModel.cs`, `ExpensesView.axaml` existen,
  compilan y se resuelven vía `DataTemplate` en `App.axaml`.
- `dotnet run --project src/ControlDeFinanzasIA.UI` arranca,
  navega a "Gastos" desde el sidebar, muestra el formulario y la
  lista.
- El usuario puede: seleccionar categoría (de las 9 sembradas),
  ingresar monto (`1500.50` o `1,500.50` según locale),
  descripción, fecha default hoy (de `IClock.Today`), pulsar
  "Registrar" y **ver el gasto en la lista inmediatamente**.
- Cierra la app, la vuelve a abrir, **el gasto sigue ahí**
  (criterio explícito de `roadmap.md` §Fase 3).
- Si se introduce un monto inválido (vacío, negativo, no
  numérico) o descripción vacía, el botón "Registrar" desactiva
  o muestra errores por campo sin llamar al use case.
- Si la categoría no existe (escenario inducido), la UI muestra
  el error mapeado desde `Result.Failure`.
- `ExpensesViewModelTests` pasa con al menos 7 tests; cobertura
  del ViewModel ≥ 70% (umbral de `architecture.md` §15).
- `dotnet build`: 0 warnings, 0 errors.
- `dotnet test`: ~167 + ~7 nuevos + ~2 ajustes ≈ 176 verdes.
- Cobertura `Domain` y `Application` (incluyendo F-001 a F-005
  de Phase 3) ≥ 80% (`architecture.md` §15).
- NetArchTest R-1 a R-5 siguen pasando: `ExpensesViewModel`
  consume `RegisterExpenseUseCase` y `GetTransactionsQuery`
  desde `Application` (interfaces ya registradas en
  `Bootstrap`); no referencia `Infrastructure` directamente.
- El flujo manual "registrar gasto → verlo → cerrar app →
  reabrir → sigue visible" se documenta como smoke test de
  aceptación en la Specification de F-007.

---

## Orden recomendado y trabajo en paralelo

**Orden secuencial estricto** (una persona sola):

F-001 → F-002 → F-003 → F-004 → F-005 → F-006 → F-007

**Detalle de las dependencias:**

1. **F-001** (entidad) es el nodo inicial. Todo lo demás necesita
   `Expense` para existir (en firma o en cuerpo).
2. Tras F-001, se bifurcan **tres ramas independientes** que
   pueden avanzar en paralelo:
   - **Rama de persistencia:** F-002 → F-003.
   - **Rama de escritura:** F-004 (depende sólo de F-001 + Phase 1
     interfaces existentes; en particular `IExpenseRepository.AddAsync`
     ya está firme desde Phase 1 F-006, así que F-004 puede
     mockearlo sin esperar a F-003).
   - **Rama de UI infra:** F-006 (depende sólo de Phase 1 F-008,
     no toca nada de `Expense`).
3. F-005 (lectura) **depende de F-003** porque consume
   `GetByFilterAsync` (método nuevo introducido en esa feature).
   Nota: si F-003 y F-005 los hace la misma persona,
   secuencial `F-003 → F-005`. Si los hace gente distinta, F-005
   puede empezar sólo con la **firma** de `GetByFilterAsync`
   acordada (trabajo con `IExpenseRepository` mockeado); el
   acoplamiento adelante se cierra cuando F-003 mergea.
4. F-007 (vista) **depende de F-004 + F-005 + F-006** y los
   consume por interface. Es el punto de convergencia.

**Trabajo en paralelo posible (entre features intra-fase):**

- **F-002** y **F-004** son totalmente independientes:
  - F-002 modifica `Infrastructure/Persistence` (no toca
    `Application`).
  - F-004 modifica `Application` y `Tests.Unit` (no toca
    `Infrastructure/Persistence/Configurations` salvo el
    registro en `Bootstrap` — **cuidado aquí**: ambos tocan
    `Bootstrap.ConfigureServices`. Si se parallelizan, coordinar
    conflictos en ese archivo; ambos añaden registros distintos,
    merge trivial).
- **F-006** es la más independiente de todas: no toca `Domain`,
  `Application` ni `Infrastructure/Persistence`. Sólo `UI` y un
  par de líneas en `Bootstrap`. Se puede hacer **en paralelo con
  F-001 a F-005** sin conflictos estructurales.
- **F-003** y **F-005** pueden parallelizar si la firma de
  `GetByFilterAsync` se acuerda primero (ver #3 arriba).

**No se puede parallelizar**:

- F-007 es estrictamente final.
- F-003 no puede empezar antes que F-002 (la migración debe
  existir para que los tests de integración del repo corran).
- F-005 no puede empezar antes que F-003 sin acuerdo previo de
  firma.

## Trabajo en paralelo posible

**Con 2 personas:**

- Persona A (backend completo): F-001 → F-002 → F-003 → F-005.
  Tras F-001, en paralelo con Persona B (F-004).
- Persona B (UI + escritura): empieza F-004 al terminar A su
  F-001; luego F-006; finalmente F-007 cuando A mergeue F-005
  y B haya mergeado F-006.
- Cronograma aproximado:
  - Día 1–2: A hace F-001 + F-002; B hace F-004 + comienza F-006.
  - Día 3–4: A hace F-003 + F-005; B termina F-006.
  - Día 5–6: B hace F-007, con A disponible para desbloquear
    integración.

**Con 3 personas:**

- Persona A: F-001 → F-002 → F-003 → F-005.
- Persona B: arranca F-004 al terminar A su F-001; luego ayuda
  a C con F-007.
- Persona C: F-006 al inicio (independiente); luego F-007.
- Aceleración real: 4–5 días vs 7 secuenciales.

## Riesgos de la fase

- **Conversión del value object `Money` en EF Core (F-002).**
  Phase 2 lo diferrió. F-002 de Phase 3 es la primera feature
  que realmente materializa el patrón. Si se opta por `OwnsOne`
  (recomendado) y no por `ValueConverter` serializando a JSON,
  futuras entidades (`RecurringRule`, `Debt`, `Goal`, `Budget`)
  replicarán el patrón; cualquier error aquí se propaga. **Mitigación:**
  test de integración `ExpenseRepositoryTests.AddAsync_ThenGetByIdAsync_PreservesMoneyPrecision`
  con montos de varias magnitudes y DOP + USD; cobertura de
  redondeo y de preservación deCurrency. Comparar contra
  `architecture.md` §8: "Conversores de value objects
  registrados en el OnModelCreating para mapear a columnas
  primitivas."

- **Hilo de UI + persistencia asíncrona (F-007).** Primera vez
  que un ViewModel llama a un use case async (`await`) y
  actualiza una `ObservableCollection`. Si el comando no
  respeta el `SynchronizationContext` (o si el use case hace
  `.ConfigureAwait(false)` y actualiza UI desde hilo de
  fondo), se rompe en runtime. **Mitigación:** patrón
  estándar en Avalonia: el `[RelayCommand]` se ejecuta en UI
  thread; el `await` del use case retoma en el mismo contexto
  por defecto. Asegurar que los tests del ViewModel no asumen
  threading; usar `Avalonia.Headless` sólo para smoke tests de
  aceptación. Documentar en la Specification de F-007.

- **`Date` futura permitida o no (decisiones de invariantes en
  F-001 y F-004).** `vision.md` §4 no prohíbe ni obliga. La
  decisión afecta el `Expense.Create`, el `RegisterExpenseValidator`
  y el `DatePicker` de la vista. **Mitigación:** surface esto a
  la Specification de F-001 y al usuario antes de implementar
  (ver "Open questions" en el retorno al orchestrator). Una
  decisión consistente evita bifurcar el `Validator` (F-004)
  y el `Expense.Create` (F-001).

- **Monto positivo obligatorio (idem F-001).** `Money` en Phase
  1 F-002 ya valida `Amount` con redondeo pero **no** impone
  signo. El `Expense` de Phase 3 decide si `Amount.Amount > 0`.
  Si se rechazan negativos aquí, los ingresos (`Income` en
  Phase 4) deberán tomar la decisión contrapuesta (montos
  positivos también, no negativos). **Mitigación:** en `Money`,
  el signo se permite; cada entidad decide (`Expense` y
  `Income` exigen `Amount > 0`). Documentar como invariant en
  el test de F-001.

- **Refactor futuro a `Transaction` abstracta (F-001).** Phase
  3 introduce `Expense` como clase sellada sin base abstracta
  `Transaction`. Phase 4 (Ingresos, edición, borrado) introducirá
  `Income` y muy probablemente extraerá `Transaction` como
  abstract base común para evitar duplicar `Id`, `Date`,
  `Amount`, `CreatedAt`, `UpdatedAt`, `CategoryId`. **Mitigación:**
  mantener `Expense` minimalista en campos compartidos con
  `Income` (no acoplar nada específico de "gasto" en el
  constructor; el futuro `Transaction` podrá encapsularlos
  sin tocar nada específico de `Expense`). Costo: refactor de ~1h en
  Phase 4, aceptable vs especular la abstracción antes de
  tener un segundo subtipo.

- **`IExpenseRepository` extensión (F-003).** Phase 1 definía
  5 métodos (las mismas firmas fijas para las 15
  interfaces). F-003 de Phase 3 **rompe** esa uniformidad
  añadiendo `GetByFilterAsync` sólo a `IExpenseRepository`.
  Esto puede generar dudas sobre si los otros 14 repositorios
  también deberían tener un `GetByFilterAsync` genérico.
  **Mitigación:** declarar explícitamente en la spec de F-003
  que el método es **específico de `Expense`** por la paginación
  de transacciones (un rasgo específico de esta entidad en
  Phase 3). Otros repos evaluarán añadirlo cuando lo necesiten,
  caso a caso. No introducir `IQueryRepository<T>` genérico en
  esta fase.

- **CompiledBindings mal configurados y DataTemplate no
  resuelve (F-006, F-007).** Riesgos explícitos del
  `roadmap.md` §Fase 3. Si el `x:DataType` no coincide con el
  namespace del ViewModel, o si el `DataTemplate` no se aplica
  porque el `ContentControl` no encuentra el tipo, aparece una
  vista vacía o un `TextBox`. **Mitigación:** smoke test manual
  documentado en F-007 AC; `dotnet build` con CompiledBindings
  activado (warnings de bindings en tiempo de compilación). Si
  surge un fallback oscuro, describir el síntoma en la
  Specification de F-006 para que el implementador lo
  reconozca.

- **Navegación con dependencia circular (F-006).** Si
  `INavigationService` se define con referencias bidireccionales
  entre el `ShellViewModel` y los ViewModels de feature
  (inyectarse reciprocamente), se forma un ciclo en el
  contenedor DI. **Mitigación:** separar `INavigationService`
  de `ShellViewModel`; los ViewModels de feature reciben
  `INavigationService` nunca `ShellViewModel` ni hermanos. La
  navegación "de feature a feature" será evaluable en Phase 4.

- **`Bootstrap.ConfigureServices` como archienemigo del
  parallelismo.** F-003, F-004, F-005, F-006 y F-007
  **modifican** `Bootstrap.ConfigureServices` (cada uno añade
  sus registros). Trabajo en paralelo entre features produce
  conflictos de merge en ese archivo. **Mitigación:** adoptar
  ese archivo como "critical section" en los PRs; merges
  triviales (cada uno añade líneas distintas). Para evitar
  cuello, F-006 y los use cases de Application pueden
  registrarse en extension methods aparte
  (`AddNavigation`, `AddExpensesUseCases`) y `Bootstrap` sólo
  los invoca — patrón que históricamente evita este problema
  en fases más grandes.

## Referencias

- `docs/roadmap.md` — §Fase 3 "Slice vertical: registrar y listar
  gastos" (Entregables esperados, Riesgos principales, Criterios
  de fase completada).
- `docs/architecture.md` — §3 (solución y proyectos), §4 (grafo
  de dependencias, reglas R-1 a R-5 invalidadas por F-007 de
  Phase 1 / F-006 de Phase 2), §6 (entidad `Expense` y `Money`),
  §7 (use cases, validación, `Result<T>`, FluentValidation), §8
  (persistencia, configuraciones Fluent API, value converters),
  §9 (UI: ViewModels, Views, MVVM, DataTemplates), §10
  (Navegación), §15 (Testing).
- `docs/vision.md` — §4.1 (Gastos del día a día), §4.2 (campos
  mínimos de un movimiento — transitional subset for Phase 3:
  fecha, monto+moneda, categoría, nota; sin tags ni método),
  §4.3 (Búsqueda y vistas, subconjunto "listar" — los filtros
  se difieren a Phase 4).
- `spec/constitution/tech-stack.md` — MVVM estricto en Avalonia,
  async/await extremo a extremo, errores como valores.
- `spec/constitution/mission.md` — Clean Architecture, MVVM,
  testabilidad.
- `spec/phases/phase-01/features.md` — F-002 (`Money`/`Currency`),
  F-005 (`Result` no-genérico con `Value: object?`,
  `PagedResult<T>` con constructor 4-arg, sin factory `Empty`),
  F-006 (interfaces de repositorio, incluida `IExpenseRepository`),
  F-007 (NetArchTest R-1..R-5), F-008 (`Bootstrap.ConfigureServices`).
- `spec/phases/phase-02/features.md` — **formato de referencia**
  para este documento. F-002 (patrón de configuraciones Fluent
  API y migraciones — replicado en F-002 de Phase 3), F-004
  (patrón de repositorio con `Result.Failure` para `NotFound`,
  `SystemCategoryImmutable`), F-006 (`TestAppDbContextFactory`
  reutilizado en F-003 de Phase 3).
- `spec/features/phase-01/F-002-money-value-object.done.md` —
  `Money` y `Currency` que F-001 de Phase 3 consume.
- `spec/features/phase-01/F-006-contrato-interfaces-repo.done.md`
  — `IExpenseRepository` (5 métodos) que F-003 de Phase 3
  implementa y extiende con `GetByFilterAsync`.
- `spec/features/phase-01/F-008-composition-root.done.md` —
  `Bootstrap.ConfigureServices` que F-003, F-004, F-005, F-006
  y F-007 de Phase 3 extienden.
- `spec/features/phase-02/F-004-category-paymentmethod-repositories.done.md`
  — `CategoryRepository` que F-007 de Phase 3 usa vía
  `ICategoryRepository` para llenar el ComboBox de categorías.
- `spec/features/phase-02/F-005-composition-root-persistence.done.md`
  — `AddPersistence` y la aplicación de migraciones al arranque;
  F-002 de Phase 3 añade una migración nueva que ese mismo
  arranque aplica automáticamente.
- EF Core 8 documentation oficial (a consultar vía Context7
  cuando se implemente, en especial `OwnsOne` para value objects
  y migraciones aditivas).
- Avalonia UI documentation (a consultar vía Context7 para
  `DataTemplate`, `CompiledBindings`, `INavigationService`
  patterns, y `Avalonia.Headless` en tests).