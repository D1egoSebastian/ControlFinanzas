# Phase 4 — Transacciones: ingresos, edición, borrado, tags, filtros, vistas guardadas

> **Fase:** 4 — Extensión del slice vertical de Phase 3
> **Estado:** ✅ COMPLETADA (Phase 4 cerrado — F-001..F-008 archivadas)
> **Spec ID:** PHASE-04-PLAN
> **Dependencias:** Phase 3 (F-001..F-007 ✅ archivadas)
> **Roadmap:** `docs/roadmap.md` §Fase 4 (líneas 232-266)

## 1. Resumen

Phase 3 cerró el slice vertical de `Expense` (registrar + listar + paginar). Phase 4 lo **extiende** en cuatro ejes:

1. **Ingresos** — el lado simétrico de gastos. Misma forma, distinto significado contable.
2. **Edición y borrado** — completar el CRUD (Phase 3 solo implementó Create + Read).
3. **Tags** — categorización many-to-many libre (vs. Category que es jerárquica).
4. **Filtros y vistas guardadas** — la base sobre la que se construyen presupuestos, recurrentes, deudas y visualizaciones.

Phase 4 no innova arquitectura: **replica los patrones de Phase 3** (Domain entity + Configuration + Repository + UseCase + Validator + UI View+ViewModel). El leverage es alto porque los precedentes están frescos.

## 2. Contexto

`docs/vision.md` define los movimientos como "transacciones" (`Expense` o `Income`). El usuario quiere poder:
- Registrar ingresos manuales (sueldo, freelance, transferencias).
- Editar o borrar gastos e ingresos que registró mal.
- Filtrar la lista por categoría, tags y rango de fecha.
- Guardar filtros recurrentes ("mis gastos de transporte del último mes") como vistas nombradas.

Phase 4 es prerrequisito de Phase 5 (multi-moneda, método de pago) y Phase 6 (recurrentes). Sin CRUD completo + filtros, no se puede iterar sobre los demás features.

## 3. Objetivo

> Cerrar el CRUD de transacciones y sentar las bases de filtrado que usarán las fases 5-9.

## 4. Alcance

### 4.1 In scope
- Entidad `Income` con invariantes, mapping EF Core, migración `AddIncomesTable`.
- `IncomeRepository` con los 6 métodos estándar + `GetByFilterAsync`.
- `RegisterIncomeUseCase` + `RegisterIncomeCommand` + `RegisterIncomeValidator` + `RegisterIncomeResult` (con warnings).
- `UpdateExpenseUseCase` + `UpdateExpenseCommand` + `UpdateExpenseValidator`.
- `UpdateIncomeUseCase` (mismo patrón).
- `DeleteTransactionUseCase` (genérico sobre Expense o Income via discriminador `TransactionType`).
- Entidad `Tag` con invariantes + `ITagRepository` (ya existe interface de Phase 1) + `TagRepository` + `Expense.AddTag(tag)` / `Expense.RemoveTag(tag)`.
- Tabla join `ExpenseTags` (M:N Expense ↔ Tag).
- Extender `ExpenseFilter` con `TagIds` (IReadOnlyList<Guid>?).
- Extender `IncomeFilter` con `TagIds`.
- Actualizar `ExpenseRepository.GetByFilterAsync` para aplicar todos los filtros (CategoryId, TagIds, DateFrom, DateTo).
- `IncomeRepository.GetByFilterAsync` con los mismos filtros.
- Entidad `SavedView` con `Name`, `EntityType` (Expense/Income), `FilterJson` (System.Text.Json con versionado).
- `ISavedViewRepository` (existe de Phase 1) + `SavedViewRepository`.
- `SaveCurrentFilterUseCase` + `ApplySavedViewUseCase` (parsea JSON → filter concreto).
- UI: `IncomeView` (sidebar item "Ingresos" + ViewModel + tests).
- UI: extender `ExpensesView` con inputs de filtro (Category, Tags, Date range) + combobox de vistas guardadas.
- Tests unitarios + integration para todo lo anterior.

### 4.2 Out of scope (Phase 5+)
- Multi-moneda DOP/USD + `PaymentMethod` real (Phase 5).
- `IExchangeRateProvider` real con API externa (Phase 5).
- Recurrentes / suscripciones (Phase 6).
- Deudas + plan de cuotas (Phase 7).
- Presupuestos (Phase 8).
- Anomalías (Phase 9).
- Visualizaciones / reportes (Phase 10).
- Migración del modelo `Transaction` polimórfico (Phase 5+ lo hará).

### 4.3 Cambios al alcance
- **Modifica:** `Expense.cs` (agrega colección `Tags` y métodos `AddTag`/`RemoveTag`), `ExpenseConfiguration.cs` (mapping de M:N), `ExpenseRepository.cs` (`GetByFilterAsync` con TagIds), `GetTransactionsQuery.cs` (request con TagIds), `ExpensesViewModel.cs` + `ExpensesView.axaml` (filter UI), `App.axaml` (DataTemplate para `IncomeView`), `RouteNames.cs` (+ `Incomes`), `NavigationService.cs` (+ `Incomes → IncomeViewModel`), `MainWindow.axaml` (sidebar item), `Bootstrap.cs` (registros).
- **NO modifica:** `Money` value object, `Currency` enum, `Result`/`PagedResult`, `Result` non-genérico convention.

## 5. Casos de uso

### CU-1 — Registrar ingreso
1. Usuario navega a "Ingresos" desde el sidebar.
2. Llena form (monto, moneda, fecha, descripción, nota opcional).
3. Click "Registrar".
4. Validator de F-004 (FluentValidation) valida.
5. Use case crea `Income` vía `Money.Create(...)`, persiste vía `IncomeRepository.AddAsync`.
6. Lista se refresca.

### CU-2 — Editar gasto
1. Usuario hace click en "Editar" en una fila de la lista de gastos.
2. Se abre form modal (o vista de edición) pre-poblado con los datos del gasto.
3. Usuario modifica campos.
4. Click "Guardar" → `UpdateExpenseUseCase`.
5. Validator valida; use case persiste cambios vía `ExpenseRepository.UpdateAsync`.
6. Lista se refresca con los datos actualizados.

### CU-3 — Borrar gasto
1. Usuario hace click en "Eliminar" en una fila.
2. Confirmación ("¿Eliminar este gasto?").
3. `DeleteTransactionUseCase` con tipo `TransactionType.Expense`.
4. `ExpenseRepository.RemoveAsync` hace hard delete.
5. Lista se refresca.

### CU-4 — Taggear gasto
1. En el form de edición, combobox multi-select de tags existentes.
2. Usuario selecciona 1-N tags → `Expense.AddTag(tag)` (o `AssignTags(IEnumerable<Tag>)`).
3. Al guardar, `ExpenseRepository.UpdateAsync` persiste la nueva lista de tags vía EF Core M:N.

### CU-5 — Filtrar gastos
1. En `ExpensesView`, inputs de filtro (Category picker, Tags picker, Date range).
2. Usuario ajusta filtros → `ExpensesViewModel.Filter` se actualiza → `RefreshAsync` consume los filtros.
3. `ExpenseRepository.GetByFilterAsync` con el filter completo (CategoryId?, TagIds?, DateFrom?, DateTo?).

### CU-6 — Guardar vista
1. Usuario ajusta filtros en `ExpensesView`.
2. Click "Guardar como vista" → prompt para nombre → `SaveCurrentFilterUseCase`.
3. View serializa el filter a JSON (versionado) → `SavedViewRepository.AddAsync`.
4. Vista aparece en el combobox de vistas guardadas.

### CU-7 — Aplicar vista guardada
1. Usuario selecciona una vista del combobox.
2. `ApplySavedViewUseCase` deserializa JSON → filter concreto.
3. `ExpensesViewModel.Filter` se actualiza → `RefreshAsync` aplica.

## 6. Requisitos funcionales

### RF-1 — Income entity
- `public sealed class Income : Entity<Guid>` (paralelo a `Expense`)
- Propiedades: `Id`, `CategoryId` (nullable, ingresos sin categoría son válidos), `Date`, `Amount` (Money), `Description`, `Note?`, `CreatedAt`, `UpdatedAt`.
- Invariantes (mínimo 5):
  - INV-1: `Amount.Amount > 0`.
  - INV-2: `Description` no vacío (post-trim).
  - INV-3: `Description` longitud ≤ 200.
  - INV-4: `Note` longitud ≤ 500 si no null.
  - INV-5: `CreatedAt` UTC.
  - INV-6 (opcional): `UpdatedAt >= CreatedAt`.
- Factory `Create(...)` con validación (mismo patrón que `Expense.Create`).

### RF-2 — Income configuration + migration
- `IncomeConfiguration : IEntityTypeConfiguration<Income>`:
  - `b.ToTable("Incomes")` (PascalCase).
  - `Id`: PK, `ValueGeneratedNever`.
  - `CategoryId`: nullable FK a `Categories.Id` (ingresos sin categoría OK).
  - `Date`: `IsRequired` (`DateOnly`).
  - `Description`: `IsRequired`, max 200.
  - `Note`: max 500, nullable.
  - `CreatedAt`, `UpdatedAt`: `IsRequired`.
  - `OwnsOne(e => e.Amount)` con columnas `Amount` (decimal, `HasPrecision(18,4)`) + `Currency` (int).
- Migración `AddIncomesTable` generada vía `dotnet ef migrations add AddIncomesTable`.

### RF-3 — IIncomeRepository (ya existe de Phase 1 F-006)
- `Task<Result> AddAsync(Income income, CancellationToken ct = default)`
- `Task<Result> UpdateAsync(Income income, CancellationToken ct = default)`
- `Task<Result> RemoveAsync(Guid id, CancellationToken ct = default)` — hard delete.
- `Task<Income?> GetByIdAsync(Guid id, CancellationToken ct = default)`
- `Task<IReadOnlyList<Income>> GetAllAsync(CancellationToken ct = default)`
- `Task<PagedResult<Income>> GetByFilterAsync(IncomeFilter filter, CancellationToken ct = default)`

### RF-4 — IncomeFilter
- `public sealed record IncomeFilter(int Page = 1, int PageSize = 50, Guid? CategoryId = null, DateOnly? DateFrom = null, DateOnly? DateTo = null, IReadOnlyList<Guid>? TagIds = null)`
- Mismo formato que `ExpenseFilter` + `TagIds`.

### RF-5 — IncomeRepository implementation
- `public sealed class IncomeRepository : IIncomeRepository`
- 6 métodos con contratos idénticos al `ExpenseRepository` (Phase 3 F-003).
- `GetAllAsync`: `OrderByDescending(e => e.Date).ThenByDescending(e => e.CreatedAt).AsNoTracking().ToListAsync()`.
- `GetByFilterAsync`: aplica filtros opcionales + `Skip/Take`.
- Validación FK de `CategoryId` en `AddAsync` (similar a `ExpenseRepository`): `Result.Failure("CategoryNotFound", "...")` si la categoría no existe.
- Hard delete en `RemoveAsync` (no soft-delete).

### RF-6 — RegisterIncomeUseCase
- `RegisterIncomeCommand(Guid? CategoryId, DateOnly Date, decimal Amount, Currency Currency, string Description, string? Note)`
- `RegisterIncomeValidator : AbstractValidator<RegisterIncomeCommand>` con 6 reglas sync:
  - `Amount > 0`, `Description` not empty (post-trim) + max 200, `Note` max 500, `Currency` `IsInEnum`, `Date` no futura (decisión de Phase 4 — **abierto a producto**).
- `RegisterIncomeUseCase` inyecta `IIncomeRepository`, `IClock`, `IValidator<RegisterIncomeCommand>`, `ILogger<>`.
- `RegisterIncomeResult` con warnings (paralelo a F-004).
- Validación manual + `ValidateAsync`. Si falla, `Result.Failure("Validation", ...)`.

### RF-7 — UpdateExpenseUseCase + UpdateIncomeUseCase
- `UpdateExpenseCommand(Guid Id, Guid? CategoryId, DateOnly Date, decimal Amount, Currency Currency, string Description, string? Note)`
- `UpdateExpenseValidator` (mismas reglas que `RegisterExpenseValidator` + invariante `Id != Guid.Empty`).
- `UpdateExpenseUseCase`: valida, busca, valida invariantes, `CurrentValues.SetValues` + `SaveChangesAsync`.
- `UpdateIncomeUseCase` simétrico.
- Errors propagados del repo: `"NotFound"`, `"CategoryNotFound"`.

### RF-8 — DeleteTransactionUseCase
- `enum TransactionType { Expense, Income }`
- `DeleteTransactionCommand(Guid Id, TransactionType Type)`
- `DeleteTransactionUseCase` inyecta `IExpenseRepository` + `IIncomeRepository`, dispatch por tipo.
- `RemoveAsync` en el repo correspondiente.
- Errors: `"NotFound"`.

### RF-9 — Tag entity
- `public sealed class Tag : Entity<Guid>` (paralelo a Category)
- Propiedades: `Id`, `Name`, `Color?` (hex string opcional).
- Invariantes: `Name` no vacío + max 50, `Color` max 9 chars (hex like `#FF00FF`).
- Factory `Create(...)`.

### RF-10 — Many-to-many Expense ↔ Tag
- EF Core: skip navigations o join table explícita (`ExpenseTags` con `ExpenseId`, `TagId`).
- `Expense.AddTag(Tag)` y `Expense.RemoveTag(Tag)` — métodos de dominio que mutan la colección interna.
- Migración: `AddTagsTable` + `AddExpenseTagsJoinTable`.

### RF-11 — ITagRepository (ya existe de Phase 1) + TagRepository
- `Task<Result> AddAsync(Tag tag, ct)`
- `Task<Result> UpdateAsync(Tag tag, ct)`
- `Task<Result> RemoveAsync(Guid id, ct)`
- `Task<Tag?> GetByIdAsync(Guid id, ct)`
- `Task<IReadOnlyList<Tag>> GetAllAsync(ct)`

### RF-12 — SavedView entity
- `public sealed class SavedView : Entity<Guid>`
- Propiedades: `Id`, `Name`, `EntityType` (`TransactionType` o un enum propio `SavedViewEntityType { Expense, Income }`), `FilterJson` (string), `CreatedAt`, `UpdatedAt`.
- Invariantes: `Name` no vacío + max 100, `FilterJson` no vacío.

### RF-13 — SavedView serialization (versionado)
- `record ExpenseFilterDto(...)` y `record IncomeFilterDto(...)` — DTOs serializables que representan el filter.
- `record SavedViewPayloadV1(int Version, string EntityType, ExpenseFilterDto? ExpenseFilter, IncomeFilterDto? IncomeFilter)` — versionado con campo `Version` (int, empezar en 1).
- `SaveCurrentFilterUseCase`: toma filter concreto → DTO → `JsonSerializer.Serialize(payload)` → guarda en `FilterJson`.
- `ApplySavedViewUseCase`: lee `FilterJson` → deserializa → valida `Version` → mapea a filter concreto (o rechaza con `Result.Failure("IncompatibleVersion", "...")` si la versión es mayor a la soportada).
- `System.Text.Json` con `JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false }`.

### RF-14 — ISavedViewRepository (existe de Phase 1) + SavedViewRepository
- 6 métodos estándar (AddAsync, UpdateAsync, RemoveAsync, GetByIdAsync, GetAllAsync, `GetByEntityTypeAsync(SavedViewEntityType type, ct)`).

### RF-15 — SaveCurrentFilterUseCase + ApplySavedViewUseCase
- `SaveCurrentFilterUseCase` inyecta `ISavedViewRepository`, `ILogger<>`.
- `ApplySavedViewUseCase` inyecta `ISavedViewRepository`, `ILogger<>`.
- Retornan `Result<SavedView>` y `Result<Filter>` (concretos) respectivamente.

### RF-16 — IncomeView (UI)
- `IncomeViewModel` (sealed partial) análogo a `ExpensesViewModel` pero con `Income` en lugar de `Expense`.
- `RegisterIncomeCommand` se construye desde form fields.
- Llama `RegisterIncomeUseCase`.
- `RefreshAsync` llama `GetIncomesQuery` (nuevo, paralelo a `GetTransactionsQuery`).
- `IncomeView.axaml` con form + lista 4-col + paginación (idéntico layout a `ExpensesView`).

### RF-17 — `GetIncomesQuery`
- Paralelo a `GetTransactionsQuery` de Phase 3 F-005.
- `GetIncomesQueryRequest(int Page = 1, int PageSize = 50)` con filtros.
- `GetIncomesQuery` con `ExecuteAsync → Task<Result>` (non-genérico, `Value = PagedResult<IncomeDto>`).
- Validación manual de `Page`/`PageSize`.
- Tests con `FakeIncomeRepository`.

### RF-18 — Extender filtros en `ExpenseRepository.GetByFilterAsync`
- Aplicar `CategoryId?` (existe), `TagIds?` (nuevo), `DateFrom?` (existe), `DateTo?` (existe).
- Orden: `Date desc, CreatedAt desc`.
- Paginación sin cambios.

### RF-19 — UI: filter inputs en ExpensesView
- `ExpensesView.axaml`: agregar `ComboBox` (Category), `ComboBox` multi-select (Tags), `DatePicker` desde, `DatePicker` hasta.
- "Limpiar filtros" button.
- "Guardar como vista" button + prompt para nombre.
- Combobox de vistas guardadas (dropdown) + "Aplicar" + "Eliminar vista" buttons.
- `ExpensesViewModel`: nueva `Filter` property (tipo `ExpenseFilter`), `FilterChangedCommand` que llama `RefreshAsync`, `SaveCurrentFilterCommand`, `ApplySavedViewCommand`.

### RF-20 — Tests
- Unit tests para `Income.Create` (invariantes), `RegisterIncomeValidator`, `RegisterIncomeUseCase`, `UpdateExpenseUseCase`, `UpdateIncomeUseCase`, `DeleteTransactionUseCase`, `SaveCurrentFilterUseCase`, `ApplySavedViewUseCase`, `Tag.Create`.
- Integration tests para `IncomeRepository` (6 métodos + queries con TagIds).
- Integration tests para `SavedViewRepository` (round-trip JSON).
- VM tests para `IncomeViewModel`, `ExpensesViewModel` extendido (filter changes, save/apply saved view).
- Tests de serialización versionada (`SavedViewPayloadV1` con `Version = 1`).

## 7. Requisitos no funcionales

- `dotnet build`: 0 warnings, 0 errors (Release + Debug).
- `dotnet test`: 222 actuales + ~30 nuevos ≈ **252 verdes**.
- NetArchTest R-1..R-5 verdes (Application no depende de Infrastructure; UI referencia Application + Domain).
- `dotnet format style --verify-no-changes` exit 0.
- `dotnet run --project src/ControlDeFinanzasIA.UI` arranca + navega a "Ingresos" + muestra el form + lista + paginación + filtros.
- **Pure xUnit `Assert.*` en tests. No FluentAssertions. No mocking library (hand-rolled fakes).**
- JSON payload de `SavedView` debe ser **round-trip safe** (mismo input → mismo output) con versionado.

## 8. Reglas de negocio

- **Edición no permite cambiar tipo de transacción** (Expense → Income o viceversa requiere delete + create).
- **Borrado es hard delete** (sin soft-delete, alineado con `ExpenseRepository`).
- **Tags son globales** (no por categoría) — un Tag puede aplicarse a cualquier Expense.
- **SavedView es global** por usuario (no por "proyecto" o "carpeta") — todas las vistas en una sola tabla.
- **`Result.Failure` con códigos en inglés** (`"Validation"`, `"CategoryNotFound"`, `"NotFound"`, `"IncompatibleVersion"`, etc.).
- **`ApplySavedView` rechaza versiones futuras** con `"IncompatibleVersion"` pero acepta versiones iguales o anteriores (forward-compatible para futuras migraciones).

## 9. Descomposición en features

| ID | Feature | Criticidad | Estimación | Estado |
|----|---------|------------|------------|--------|
| 4-001 | `Income` entity + `IncomeConfiguration` + migración `AddIncomesTable` | Crítica | S | ✅ COMPLETADA |
| 4-002 | `IncomeRepository` + `IncomeFilter` + integration tests | Crítica | M | ✅ COMPLETADA |
| 4-003 | `RegisterIncomeUseCase` + `RegisterIncomeCommand` + `RegisterIncomeValidator` + `RegisterIncomeResult` + DI | Crítica | M | ✅ COMPLETADA |
| 4-004 | `Tag` entity + `TagRepository` + M:N join + migration | Alta | M | ✅ COMPLETADA |
| 4-005 | `UpdateExpenseUseCase` + `UpdateIncomeUseCase` + `DeleteTransactionUseCase` + tests | Crítica | M | ✅ COMPLETADA |
| 4-006 | `SavedView` entity + JSON versionado + `SaveCurrentFilterUseCase` + `ApplySavedViewUseCase` + `SavedViewRepository` | Alta | L | ✅ COMPLETADA |
| 4-007 | Extender filtros en `ExpenseRepository.GetByFilterAsync` (TagIds) + extender `ExpenseFilter` + tests de queries complejas | Alta | M | ✅ COMPLETADA |
| 4-008 | UI: `IncomeView` + `GetIncomesQuery` + extender `ExpensesView` con filter inputs + saved views UI + tests | Crítica | L | ✅ COMPLETADA |

> **Nota sobre IDs:** los IDs `4-001` a `4-008` son locales a Phase 4. Los `F-XXX` también se usan (siguen siendo locales al plan, ver convención en `spec/README.md`).

### Dependencias entre features
- 4-001 → 4-002 → 4-003 (Income: entity → repo → use case)
- 4-004 (Tag) es independiente
- 4-005 (Update/Delete) depende de F-003/F-004 de Phase 3 + 4-002 (repo de Income)
- 4-006 (SavedView) depende de 4-007 (filtros)
- 4-007 (filtros extendidos) depende de 4-004 (Tag) + Phase 3 F-003 (repo)
- 4-008 (UI) depende de todo lo anterior

### Orden recomendado
```
4-001 → 4-002 → 4-003 → 4-004 → 4-005 → 4-007 → 4-006 → 4-008
```

Tres bifurcaciones paralelas posibles:
- **Rama Ingresos:** 4-001 → 4-002 → 4-003 (backend completo de Income).
- **Rama Tags:** 4-004 (independiente).
- **Rama Update/Delete:** 4-005 (depende de 4-002).
- **Rama Filtros + Vistas:** 4-007 → 4-006 → 4-008 (encadenada).

### Trabajo en paralelo (si el equipo lo permite)
- Persona A (backend Income): 4-001 → 4-002 → 4-003.
- Persona B (Tags + Update/Delete): 4-004 → 4-005 (4-005 depende de 4-002, pero B puede empezar 4-004 + preparar 4-005 mientras A cierra 4-002).
- Persona C (UI): espera a que A cierre 4-003 y B cierre 4-007.

## 10. Estimación

- **L (1-2 semanas)** para Phase 4 completa con un equipo pequeño.
- **M (3-5 días)** para el MVP (4-001 + 4-002 + 4-003 + 4-005 + 4-008 básico).
- **XS (1 día)** para solo 4-004 (Tags) si se hace aislado.

## 11. Riesgos principales

| Riesgo | Probabilidad | Mitigación |
|--------|--------------|------------|
| Mapeo de filtros (Category + Tags + Date range) a EF Core — errores de traducción LINQ | Media | Tests de integration exhaustivos con combinaciones. Usar `IQueryable` building, no expressions dinámicas. |
| Versionado de JSON de `SavedView` — migrar `Version = 1` a `Version = 2` sin romper vistas guardadas | Media | Versionado explícito en `SavedViewPayloadV1`. `ApplySavedView` rechaza solo versiones mayores a la soportada. |
| Rendimiento de queries con muchos joins (Category + Tags + Date) | Baja | Índices en `ExpenseTags(TagId)` y `Expenses(Date)`. EF Core genera los joins; monitorear con `dotnet ef dbcontext info` después de las migraciones. |
| `Income` sin categoría (nullable) — UI debe permitirlo | Baja | Form con combobox Category **incluyendo opción "Sin categoría"** (`SelectedItem = null`). |
| `Update` parcial — campos no enviados | Baja | Command pattern: todos los campos son obligatorios (Id + todos los editables). No hay PATCH-style. |
| Tag duplicado (mismo nombre) | Baja | Validación en `TagRepository.AddAsync` por nombre único (case-insensitive). |

## 12. Criterios para considerarla completada

- ✅ `Income` entity archivada + repositorio + use cases + UI.
- ✅ CRUD completo de gastos e ingresos (Create, Read, Update, Delete).
- ✅ Tags funcionales con M:N.
- ✅ Filtros (Category, Tags, Date range) operativos en `ExpensesView` y `IncomeView`.
- ✅ `SavedView` con JSON versionado + guardar/aplicar/eliminar vista.
- ✅ Tests de integration cubren las queries más usadas (≥ 5 combinaciones de filtros).
- ✅ 222 actuales + ~30 nuevos ≈ 252 tests verdes.
- ✅ NetArchTest R-1..R-5 verdes.
- ✅ `dotnet run` arranca + navega a "Ingresos" + "Gastos" + aplica filtros + guarda vista + aplica vista guardada.

## 13. Out of scope (detallado)

- **Phase 5 — Método de pago + multi-moneda DOP/USD.**
- **Phase 6 — Recurrentes: suscripciones y gastos fijos.**
- **Phase 7 — Deudas con plan de cuotas.**
- **Phase 8 — Presupuestos.**
- **Phase 9 — Anomalías.**
- **Phase 10 — Visualizaciones / reportes.**
- **Migración polimórfica del modelo `Transaction`** (Phase 5+ puede hacerlo; Phase 4 mantiene `Expense` e `Income` como entidades separadas).
- **`IExchangeRateProvider` real con API de tipos de cambio** (Phase 5+).
- **Background jobs** (Phase 6+).

## 14. Referencias

- `docs/vision.md` §4.1 (Gastos), §4.2 (Ingresos), §4.3 (Listado + filtros), §6 (Localización).
- `docs/roadmap.md` §Fase 4 (líneas 232-266).
- `docs/architecture.md` §7 (Use cases), §9 (UI), §10 (Navegación).
- `spec/constitution/tech-stack.md` — MVVM, async/await, errors-as-values.
- `spec/constitution/mission.md` — Clean Architecture.
- `spec/features/phase-03/F-00{1..7}-*.done.md` — precedentes de Phase 3 que se replican.
- `spec/phases/phase-03/features.md` — formato y estructura del phase plan.
