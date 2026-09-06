# Specification — F-003: `ExpenseRepository` + query paginado `GetByFilterAsync`

> **Fase:** 3 — Slice vertical: registrar y listar gastos
> **Feature:** F-003 (local a esta fase)
> **Estado:** Pendiente de implementación
> **Spec ID:** SPEC-F003P3
>
> **Nota de numeración:** este `F-003` es el de la **Fase 3**
> (`spec/phases/phase-03/features.md`). No debe confundirse con el
> `F-003` de Fase 1 (`DateRange` value object) ni con el `F-003` de
> Fase 2 (`SeedData`). Los IDs son locales a cada fase; el folder
> los desambigua. Ver `spec/README.md` para la convención.
>
> Fuentes de verdad: `docs/vision.md`, `docs/architecture.md`,
> `docs/roadmap.md` (§Fase 3), `spec/constitution/tech-stack.md`,
> `spec/constitution/mission.md`,
> `spec/phases/phase-03/features.md` (F-003 dentro del desglose de
> la fase 3),
> `spec/features/phase-03/F-001-expense-domain-entity.done.md`
> (la entidad `Expense` con invariantes INV-1 a INV-8 — contrato de
> entrada),
> `spec/features/phase-03/F-002-expense-configuration-migration.done.md`
> (la tabla `Expenses` + FK a `Categories` — schema de entrada),
> `spec/features/phase-01/F-006-contrato-interfaces-repo.done.md`
> (la interface `IExpenseRepository` con 5 métodos que F-003
> implementa y **extiende**),
> `spec/features/phase-01/F-005-comuntypes-result-pagedresult.done.md`
> (`Result` y `PagedResult<T>` que `GetByFilterAsync` consume),
> `spec/features/phase-02/F-004-category-paymentmethod-repositories.done.md`
> (patrón de repositorio con `Result.Failure` que F-003 replica).

---

## 1. Resumen

F-003 implementa el **repositorio concreto `ExpenseRepository`** que
satisface la interface `IExpenseRepository` (definida en F-006 de
Fase 1 con 5 métodos estándar), **extiende** esa interface con un
sexto método `GetByFilterAsync` para queries paginadas (que
`GetTransactionsQuery` en F-005 consumirá), introduce el **record
`ExpenseFilter`** como contrato de filtros de lectura, registra el
repo en el composition root de persistencia (`AddPersistence`), y
deja tests de integración con SQLite in-memory que validan CRUD,
validación de FK a `Categories`, filtros por rango de fecha y por
categoría, y paginación correcta.

Al final de la feature:

- `src/ControlDeFinanzasIA.Application/Abstractions/IExpenseRepository.cs`
  queda **extendido** con
  `Task<PagedResult<Expense>> GetByFilterAsync(ExpenseFilter filter, CancellationToken ct = default);`
  (los 5 métodos originales de F-006 de Fase 1 se mantienen intactos).
- `src/ControlDeFinanzasIA.Application/Abstractions/ExpenseFilter.cs`
  existe como `public sealed record ExpenseFilter` con
  `Page`, `PageSize`, `CategoryId?`, `DateFrom?`, `DateTo?` (record
  "tonto", sin auto-validación — la validación vive en F-005).
- `src/ControlDeFinanzasIA.Infrastructure/Persistence/Repositories/ExpenseRepository.cs`
  existe como `public sealed class ExpenseRepository :
  IExpenseRepository` implementando los 6 métodos.
- `AddPersistence` (en
  `Infrastructure/Persistence/ServiceCollectionExtensions.cs`)
  registra `IExpenseRepository → ExpenseRepository` como `Scoped`.
- `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/ExpenseRepositoryTests.cs`
  existe con al menos 10 tests cubriendo CRUD, FK validation,
  paginación y filtros; todos pasan con SQLite in-memory vía
  `TestAppDbContextFactory` (F-006 de Fase 2).
- `dotnet build` desde la raíz: 0 warnings, 0 errors.
- `dotnet test`: ~140 tests existentes (heredados de Fases 1, 2 y
  F-001/F-002 de Fase 3) + ~10 nuevos de `ExpenseRepository` ≈ 150
  verdes, 0 failures.
- NetArchTest R-1 a R-5 (F-007 de Fase 1) siguen pasando: la
  extensión de la interface en `Application` no rompe reglas; el
  repo en `Infrastructure` implementa la interface de
  `Application` (patrón ya permitido por R-3).

---

## 2. Contexto

F-001 de Fase 3 entregó la entidad `Expense` blindada con 8
invariantes en el dominio puro. F-002 de Fase 3 mapeó esa entidad a
la tabla `Expenses` (con `OwnsOne` de `Money`, FK `Restrict` a
`Categories`, índices en `Date` y `CategoryId`) y produjo la
migración `AddExpenseTable`. Pero una tabla sin repositorio es
almacenamiento inaccesible: ningún use case de escritura (F-004) ni
query de lectura (F-005) puede materializar un gasto en disco hasta
que exista un adaptador que traduzca las operaciones de la interface
`IExpenseRepository` a EF Core.

F-003 cierra esa brecha. Conecta la interface `IExpenseRepository`
(definida en F-006 de Fase 1 con 5 métodos: `AddAsync`,
`UpdateAsync`, `RemoveAsync`, `GetByIdAsync`, `GetAllAsync`) con la
tabla `Expenses` producida por F-002, replicando el patrón de
repositorio que F-004 de Fase 2 fijó para `CategoryRepository` y
`PaymentMethodRepository` (clase `sealed`, `_db` inyectado,
`FindAsync`, `CurrentValues.SetValues`, `AsNoTracking`,
`Result.Failure` con códigos en inglés y mensajes en español).

**Extensión de la interface — la novedad de F-003.** La Fase 1
definió 15 interfaces de repositorio con una firma **uniforme** de
5 métodos (sin queries por filtro). F-003 **rompe** esa
uniformidad añadiendo `GetByFilterAsync` **sólo** a
`IExpenseRepository`. La justificación es la paginación de
transacciones: `GetTransactionsQuery` (F-005) necesita una página de
gastos ordenados por fecha descendente con metadatos de paginación
(`Total`, `Page`, `PageSize`, `TotalPages`), y ninguno de los 5
métodos existentes cubre ese caso (`GetAllAsync` no pagina ni
filtra). El método es **específico de `Expense`** por la paginación
de transacciones en Phase 3; los otros 14 repositorios evaluarán
añadirlo caso a caso cuando lo necesiten. **No** se introduce un
`IQueryRepository<T>` genérico en esta fase (ver §17 y el riesgo
"IExpenseRepository extensión" del phase plan).

**Trazabilidad.** `vision.md` §4.1 (gastos del día a día) y §4.3
(listado básico — subconjunto "listar", sin filtros UI todavía).
`architecture.md` §7 (use cases / queries devuelven `Result<T>` o
`PagedResult<T>`) y §8 (repositorios en `Infrastructure`
implementan interfaces de `Application`). `roadmap.md` §Fase 3
(entregable: repositorio de `Expense`).

---

## 3. Objetivo

1. **Extender `IExpenseRepository`** con el método
   `GetByFilterAsync(ExpenseFilter, CancellationToken)` que
   devuelve `Task<PagedResult<Expense>>` (no envuelto en `Result`).
2. **Crear el record `ExpenseFilter`** en
   `Application/Abstractions/ExpenseFilter.cs` con `Page` (1-based,
   default 1), `PageSize` (default 50), `CategoryId?`, `DateFrom?`,
   `DateTo?`. Sin auto-validación (DP-2): el record confía en el
   caller; la validación de `Page`/`PageSize` vive en F-005.
3. **Implementar `ExpenseRepository`** con los 6 métodos (los 5
   originales con la firma exacta de F-006 de Fase 1 + el nuevo
   `GetByFilterAsync`), validando la FK a `Categories` en
   `AddAsync` (devuelve `Result.Failure("CategoryNotFound", ...)`
   si el `CategoryId` no existe).
4. **Registrar `IExpenseRepository → ExpenseRepository`** como
   `Scoped` en `AddPersistence` (F-001 de Fase 2 / F-004 de Fase 2).
5. **Cubrir el repo con al menos 10 tests de integración** en
   `Tests.Integration/Persistence/ExpenseRepositoryTests.cs`
   reutilizando `TestAppDbContextFactory` (F-006 de Fase 2).
6. **Mantener NetArchTest R-1 a R-5 en verde.** F-003 sólo añade
   código a `Application/Abstractions/` (la extensión de la
   interface + el record) y a `Infrastructure/Persistence/` (el
   repo + el registro DI); no añade paquetes a `Domain` ni
   introduce referencias prohibidas.

---

## 4. Alcance

### 4.1 En alcance

- Extensión de
  `src/ControlDeFinanzasIA.Application/Abstractions/IExpenseRepository.cs`
  con `GetByFilterAsync`.
- Creación de
  `src/ControlDeFinanzasIA.Application/Abstractions/ExpenseFilter.cs`.
- Creación de
  `src/ControlDeFinanzasIA.Infrastructure/Persistence/Repositories/ExpenseRepository.cs`.
- Modificación de
  `src/ControlDeFinanzasIA.Infrastructure/Persistence/ServiceCollectionExtensions.cs`
  para registrar `IExpenseRepository` scoped en `AddPersistence`.
- Creación de
  `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/ExpenseRepositoryTests.cs`
  con al menos 10 tests.
- Verificación de que `dotnet build` y los tests existentes siguen
  pasando, más los nuevos.
- Verificación de que NetArchTest R-1 a R-5 siguen pasando.

### 4.2 Fuera de alcance

- **UI** (`ExpensesViewModel`, `ExpensesView`, navegación,
  DataTemplates). Son F-006 y F-007 de Fase 3.
- **Use cases** (`RegisterExpenseUseCase`,
  `RegisterExpenseValidator`, `GetTransactionsQuery`, DTOs,
  `ExpenseExtensions.ToDto`). Son F-004 y F-005. **La validación
  de `ExpenseFilter` (Page ≥ 1, PageSize ≥ 1) es de F-005**, no
  del repo (DP-3).
- **Multi-moneda / filtro por `Currency`.** Phase 3 asume
  single-currency (DP-6): `ExpenseFilter` no expone `Currency?`.
  Phase 5 (multi-moneda real) podrá añadirlo sin romper la firma.
- **Soft-delete** (`IsDeleted`, query filters). Rechazado para v1
  (DP-7): `RemoveAsync` hace **hard delete**.
- **Queries avanzadas** (búsqueda por texto libre en `Description`,
  FTS5, filtros por tag/método de pago). Son Fase 4+ (tags) y
  Fase 8+ (búsqueda).
- **Patrón genérico `IQueryRepository<T>`.** No se introduce; el
  método es específico de `IExpenseRepository` (ver §17).
- **Paginación de los otros 14 repositorios.** Cada repo evaluará
  añadir `GetByFilterAsync` cuando lo necesite.
- **Modificaciones a `Domain`.** F-003 no toca `Expense.cs` ni
  ninguna entidad (ver §4.3).
- **Migraciones nuevas.** F-003 usa el schema existente de F-002
  (`AddExpenseTable`); no genera migraciones.
- **Seed data de `Expense`.** `Expense` es dato de usuario; el seed
  de sistema (9 categorías + 5 métodos) es de F-003 de Fase 2.
- **Conversión de value objects.** Ya resuelta en F-002 (`OwnsOne`
  de `Money`). F-003 sólo consume el `AppDbContext` configurado.

### 4.3 Verificación: `CreatedAt` ya existe en F-001 (sin cambio a la entidad)

> **Corrección al alcance inicial.** La fase de propuesta del
> ciclo SDD de F-003 consideró brevemente que `Expense` carecía de
> un campo `CreatedAt` y que F-003 tendría que añadirlo para
> soportar el orden secundario (tie-break) de `GetByFilterAsync`.
> La verificación contra las specs archivadas **desmintió** esa
> suposición:
>
> - **F-001 de Fase 3, invariante INV-7** (`spec/features/phase-03/F-001-expense-domain-entity.done.md` §8):
>   `CreatedAt: DateTime` y `UpdatedAt: DateTime` son propiedades
>   públicas con `private set` de la entidad `Expense`, seteadas
>   en el factory `Create` (`UpdatedAt = createdAt`) y reasignadas
>   en `Update`. El caller (F-004) pasa `createdAt` sourced desde
>   `IClock.UtcNow`. La entidad es pura y no llama a `DateTime.UtcNow`.
> - **F-002 de Fase 3** (`spec/features/phase-03/F-002-expense-configuration-migration.done.md` §6 RF-1, §9):
>   `ExpenseConfiguration` mapea `b.Property(e => e.CreatedAt).IsRequired()`
>   y `b.Property(e => e.UpdatedAt).IsRequired()` a columnas
>   `TEXT` en la tabla `Expenses`. La migración `AddExpenseTable`
>   ya las crea.
> - **F-001 de Fase 3, tests T-1 y T-12**: cubren `CreatedAt` /
>   `UpdatedAt` (happy path `Create` verifica `UpdatedAt ==
>   CreatedAt`; `Update` verifica inmutabilidad de `Id` y
>   `CreatedAt`).
>
> **Consecuencia:** F-003 **no modifica `Domain/Transactions/Expense.cs`**
> ni genera ninguna migración. El orden secundario por `CreatedAt
> desc` (DP-5) se soporta con la columna que ya existe. Cualquier
> referencia en esta spec a `CreatedAt` asume que la columna ya
> está persistida y poblada por el factory de la entidad.

---

## 5. Casos de uso

### CU-1 — Persistir un gasto válido

- **Actor:** use case `RegisterExpenseUseCase` (F-004, futuro).
- **Trigger:** el use case construye un `Expense` válido vía
  `Expense.Create(...)` y llama `repo.AddAsync(expense, ct)`.
- **Precondición:** `expense` cumple INV-1 a INV-8 de F-001;
  `expense.CategoryId` referencia una `Category` existente
  (sembrada o custom).
- **Postcondición:** el repo valida la FK contra `Categories` vía
  `FindAsync`; si existe, `_db.Expenses.Add` +
  `SaveChangesAsync` + `Result.Success(null!)`. Si no existe,
  `Result.Failure("CategoryNotFound", ...)`.

### CU-2 — Rechazar un gasto con categoría inexistente

- **Actor:** use case o test.
- **Trigger:** `repo.AddAsync(expense)` con
  `expense.CategoryId = Guid.NewGuid()` (no sembrada).
- **Postcondición:** `Result.Failure("CategoryNotFound", ...)`.
  No se persiste nada. La FK `Restrict` de F-002 es la red de
  seguridad estructural; la validación previa del repo produce un
  error de negocio claro antes de llegar a la DB.

### CU-3 — Actualizar un gasto existente

- **Actor:** use case `UpdateExpenseUseCase` (Phase 4, futuro) o
  test.
- **Trigger:** `repo.UpdateAsync(expense)` sobre un `Expense` con
  `Id` existente.
- **Postcondición:** `FindAsync` localiza la entidad trackeada;
  `CurrentValues.SetValues(expense)` + `SaveChangesAsync` +
  `Result.Success(null!)`. Si el `Id` no existe,
  `Result.Failure("NotFound", ...)`.

### CU-4 — Eliminar un gasto (hard delete)

- **Actor:** use case o test.
- **Trigger:** `repo.RemoveAsync(id)`.
- **Postcondición:** `FindAsync` localiza la entidad; `_db.Remove`
  + `SaveChangesAsync` + `Result.Success(null!)`. Si no existe,
  `Result.Failure("NotFound", ...)`. **No** hay soft-delete
  (DP-7): la fila se borra físicamente.

### CU-5 — Listar todos los gastos ordenados por fecha

- **Actor:** use case o test (escenario sin paginar).
- **Trigger:** `repo.GetAllAsync(ct)`.
- **Postcondición:** `IReadOnlyList<Expense>` con todos los
  gastos, ordenados por `Date` descendente (más recientes
  primero), `AsNoTracking`.

### CU-6 — Consultar una página de gastos con filtros

- **Actor:** `GetTransactionsQuery` (F-005, futuro).
- **Trigger:** `repo.GetByFilterAsync(filter, ct)` con
  `filter = new ExpenseFilter { Page = 2, PageSize = 50,
  DateFrom = new DateOnly(2026,1,1), DateTo = new DateOnly(2026,1,31) }`.
- **Precondición:** `filter` ya fue validado por F-005 (Page ≥ 1,
  PageSize ≥ 1, DateFrom ≤ DateTo). El repo **no** re-valida
  (DP-3).
- **Postcondición:** `PagedResult<Expense>` con los items de la
  página 2 que caen en el rango de fechas, ordenados por `Date`
  descendente (tie-break `CreatedAt` descendente, DP-5), con
  `Total` = conteo total que cumple los filtros, `Page = 2`,
  `PageSize = 50`, y `TotalPages` calculado.

### CU-7 — Página fuera de rango

- **Actor:** `GetTransactionsQuery` o test.
- **Trigger:** `repo.GetByFilterAsync(filter)` con `filter.Page`
  mayor que `TotalPages` real.
- **Postcondición:** `PagedResult<Expense>` con `Items` vacío,
  `Total` = conteo real, `Page` = el solicitado, `PageSize` = el
  solicitado, `TotalPages` = el real. El repo **no** lanza ni
  devuelve error; el caller decide cómo presentar una página
  vacía.

---

## 6. Requisitos funcionales

### RF-1 — Extensión de `IExpenseRepository`

- Editar
  `src/ControlDeFinanzasIA.Application/Abstractions/IExpenseRepository.cs`.
- Añadir, **tras** los 5 métodos existentes (que se mantienen
  idénticos a F-006 de Fase 1), el método:
  `Task<PagedResult<Expense>> GetByFilterAsync(ExpenseFilter filter, CancellationToken ct = default);`
- Añadir `using ControlDeFinanzasIA.Application.Abstractions;` **no
  es necesario** (`ExpenseFilter` vive en el mismo namespace
  `ControlDeFinanzasIA.Application.Abstractions`). El `using
  ControlDeFinanzasIA.Application.Common;` ya presente (para
  `Result`) cubre `PagedResult<T>`.
- XML doc en el nuevo método describiendo que devuelve una página
  paginada de gastos que cumplen `filter`, ordenados por `Date`
  descendente.

### RF-2 — Record `ExpenseFilter`

- `public sealed record ExpenseFilter` en
  `src/ControlDeFinanzasIA.Application/Abstractions/ExpenseFilter.cs`.
- Propiedades (init-only, con defaults):
  - `int Page` — 1-based, default `1`.
  - `int PageSize` — default `50`.
  - `Guid? CategoryId` — nullable; `null` = sin filtro de
    categoría.
  - `DateOnly? DateFrom` — nullable; `null` = sin cota inferior.
  - `DateOnly? DateTo` — nullable; `null` = sin cota superior.
- **Sin auto-validación** (DP-2): el record no valida `Page ≥ 1`
  ni `PageSize ≥ 1` ni `DateFrom ≤ DateTo`. Esa validación es de
  F-005 (`GetTransactionsQuery`). El repo confía en el caller.
- `sealed` (no se hereda). Namespace
  `ControlDeFinanzasIA.Application.Abstractions`. File-scoped
  namespace, `using` al tope, XML doc exhaustivo.
- **No** se expone `Currency?` (DP-6: single-currency en Phase 3).

### RF-3 — `ExpenseRepository` con 6 métodos

- `public sealed class ExpenseRepository : IExpenseRepository` en
  `src/ControlDeFinanzasIA.Infrastructure/Persistence/Repositories/ExpenseRepository.cs`.
- Constructor: `public ExpenseRepository(AppDbContext db) => _db = db;`
  (campo privado `private readonly AppDbContext _db;`). Patrón
  idéntico a `CategoryRepository` (F-004 de Fase 2).
- Implementa los 6 métodos de `IExpenseRepository` con las firmas
  exactas (los 5 de F-006 de Fase 1 + `GetByFilterAsync`).
- Cada método de comando valida su input defensivo
  (`ArgumentNullException.ThrowIfNull(expense)` en `AddAsync` /
  `UpdateAsync`; `id == Guid.Empty` → `Result.Failure("NotFound",
  ...)` en `RemoveAsync`).

### RF-4 — `AddAsync` (con validación de FK)

1. `ArgumentNullException.ThrowIfNull(expense)`.
2. Validar la FK: `Category? category = await _db.Categories.FindAsync(new object?[] { expense.CategoryId }, ct);`.
3. Si `category is null`, devolver `Result.Failure("CategoryNotFound", $"La categoría {expense.CategoryId} no existe.")`.
4. `_db.Expenses.Add(expense)` + `await _db.SaveChangesAsync(ct)`.
5. Devolver `Result.Success(null!)`.
- La FK `Restrict` de F-002 es la red de seguridad estructural; la
  validación previa produce un error de negocio claro (DP-4:
  código `"CategoryNotFound"` en inglés, mensaje en español).

### RF-5 — `UpdateAsync`

1. `ArgumentNullException.ThrowIfNull(expense)`.
2. `Expense? existing = await _db.Expenses.FindAsync(new object?[] { expense.Id }, ct);`.
3. Si `existing is null`, devolver `Result.Failure("NotFound", $"Gasto {expense.Id} no encontrado.")`.
4. `_db.Entry(existing).CurrentValues.SetValues(expense)` +
   `await _db.SaveChangesAsync(ct)`.
5. Devolver `Result.Success(null!)`.
- **No** se re-valida la FK a `Categories` en `UpdateAsync`: el
  `expense` ya pasó por la entidad (`Expense.Update` valida
  `CategoryId != Guid.Empty`, INV-6) y el use case (F-004/Phase 4)
  es responsable de la coherencia cross-aggregate. Si el
  `CategoryId` nuevo no existe, la FK `Restrict` de la DB lanza
  `DbUpdateException` en `SaveChangesAsync`; el repo **no** la
  captura (no es un error esperado de negocio, es un bug del
  caller — ver §12 EC-3).

### RF-6 — `RemoveAsync` (hard delete)

1. Si `id == Guid.Empty`, devolver `Result.Failure("NotFound", $"Gasto {id} no encontrado.")`.
2. `Expense? existing = await _db.Expenses.FindAsync(new object?[] { id }, ct);`.
3. Si `existing is null`, devolver `Result.Failure("NotFound", $"Gasto {id} no encontrado.")`.
4. `_db.Expenses.Remove(existing)` + `await _db.SaveChangesAsync(ct)`.
5. Devolver `Result.Success(null!)`.
- **Hard delete** (DP-7): no hay `IsDeleted`, no hay query filter.
  La fila se elimina físicamente de `Expenses`.

### RF-7 — `GetByIdAsync`

- Si `id == Guid.Empty`, devolver `null`.
- `return await _db.Expenses.FindAsync(new object?[] { id }, ct);`
  (devuelve `Expense?`; `null` si no existe).

### RF-8 — `GetAllAsync`

- `return await _db.Expenses.AsNoTracking()
  .OrderByDescending(e => e.Date)
  .ToListAsync(ct);`
- Orden: `Date` descendente (los más recientes primero). Patrón
  replicable a otras transacciones. `AsNoTracking` (query de solo
  lectura, eco de F-004 de Fase 2 §17.4).

### RF-9 — `GetByFilterAsync` (el método nuevo)

- Firma: `public async Task<PagedResult<Expense>> GetByFilterAsync(ExpenseFilter filter, CancellationToken ct = default)`.
- `ArgumentNullException.ThrowIfNull(filter)`.
- Construir el `IQueryable<Expense>` base:
  `IQueryable<Expense> query = _db.Expenses.AsNoTracking();`
- Aplicar filtros opcionales (sólo si no-`null`):
  - `if (filter.CategoryId is not null) query = query.Where(e => e.CategoryId == filter.CategoryId);`
  - `if (filter.DateFrom is not null) query = query.Where(e => e.Date >= filter.DateFrom);`
  - `if (filter.DateTo is not null) query = query.Where(e => e.Date <= filter.DateTo);`
- Orden: `query = query.OrderByDescending(e => e.Date).ThenByDescending(e => e.CreatedAt);`
  (DP-5: `Date` desc, tie-break `CreatedAt` desc — soportado por
  INV-7 de F-001 y la columna de F-002, ver §4.3).
- Calcular total: `int total = await query.CountAsync(ct);`
- Aplicar paginación: `int skip = (filter.Page - 1) * filter.PageSize;`
  `List<Expense> items = await query.Skip(skip).Take(filter.PageSize).ToListAsync(ct);`
- Devolver: `return new PagedResult<Expense>(items, total, filter.Page, filter.PageSize);`
- **El repo NO valida** `filter.Page` ni `filter.PageSize` (DP-3):
  si el caller pasa `Page = 0`, `skip = -PageSize` y EF Core lanza
  en `Skip` (no es un caso esperado — F-005 valida antes). Si
  pasa `Page` más allá del final, `items` es vacío y `total`
  refleja el conteo real (CU-7).
- El `CancellationToken` se propaga a `CountAsync` y
  `ToListAsync` (RNF-3).

### RF-10 — Registro en `AddPersistence`

- Editar
  `src/ControlDeFinanzasIA.Infrastructure/Persistence/ServiceCollectionExtensions.cs`.
- En `AddPersistence`, **tras** las líneas existentes de
  `ICategoryRepository` e `IPaymentMethodRepository`, añadir:
  `services.AddScoped<IExpenseRepository, ExpenseRepository>();`
- Actualizar el XML doc del método `AddPersistence` para listar
  `IExpenseRepository` entre los repos registrados (coherencia
  con el doc existente que ya menciona `ICategoryRepository` e
  `IPaymentMethodRepository`).
- `using ControlDeFinanzasIA.Infrastructure.Persistence.Repositories;`
  ya presente (registra `CategoryRepository` /
  `PaymentMethodRepository`); cubre `ExpenseRepository`.

### RF-11 — Compilación y tests

- `dotnet build` desde la raíz: 0 warnings, 0 errors (Debug y
  Release).
- `dotnet test`: ~140 tests heredados + ~10 nuevos ≈ 150 verdes,
  0 failures.
- 11 tests de NetArchTest (R-1 a R-5) verdes.
- `dotnet format style --verify-no-changes`: exit 0.

---

## 7. Requisitos no funcionales

### RNF-1 — Compatibilidad con la arquitectura (R-1 a R-5 de F-007 de Fase 1)

- **R-1 (Domain sin paquetes externos):** F-003 no toca `Domain`.
  `Expense` sigue siendo POCO puro. Verificable:
  `dotnet list src/ControlDeFinanzasIA.Domain package` sigue vacío.
- **R-2 (Application sólo Domain + BCL + FluentValidation):**
  F-003 añade a `Application` el record `ExpenseFilter` (sólo
  BCL: `int`, `Guid?`, `DateOnly?`) y un método a
  `IExpenseRepository` (referencia `PagedResult<Expense>` de
  `Application.Common` y `Expense` de `Domain.Transactions` —
  ambos ya permitidos). No añade `PackageReference`s a
  `Application`.
- **R-3 (Infrastructure → Application + Domain):** F-003 añade
  `ExpenseRepository` en `Infrastructure` que implementa
  `IExpenseRepository` de `Application` y referencia `Expense` de
  `Domain` — patrón ya permitido (igual que
  `CategoryRepository`).
- **R-4 (UI no → Infrastructure salvo composition root):** F-003
  no toca `UI`.
- **R-5 (src/ no → Tests):** F-003 no toca referencias de
  proyecto.

### RNF-2 — Sin paquetes NuGet nuevos

- F-003 no añade nada a `Directory.Packages.props`. EF Core 8 +
  `Microsoft.Data.Sqlite` ya están (Fase 2). xUnit ya está (Fase
  1). No se añade FluentAssertions (ver §10.4).

### RNF-3 — Async con `CancellationToken` extremo a extremo

- Todos los métodos públicos de `ExpenseRepository` aceptan
  `CancellationToken ct = default` y lo propagan a las llamadas
  async de EF Core (`FindAsync`, `SaveChangesAsync`,
  `CountAsync`, `ToListAsync`, `AnyAsync`). Eco de F-004 de Fase
  2 RNF-3 y `architecture.md` ("async/await extremo a extremo").

### RNF-4 — Códigos de error en inglés, mensajes en español

- Códigos (`Result.Error.Code`): en inglés — `"NotFound"`,
  `"CategoryNotFound"` (DP-4). Consistente con F-005 de Fase 1
  RNF-4 (códigos son identificadores técnicos) y con los códigos
  ya usados por `CategoryRepository` (`"NotFound"`,
  `"DuplicateName"`, `"SystemCategoryImmutable"`).
- Mensajes (`Result.Error.Message`): en español (es-DO),
  consistentes con el `CategoryRepository` **implementado** en
  F-004 de Fase 2 (p. ej. `$"Categoría {id} no encontrada."`,
  `$"La categoría {categoryId} no existe."`). Eco de F-004 de
  Fase 2 RNF-4. La UI puede traducirlos (Fase 12) si añade i18n.

### RNF-5 — Sin scope creep

- F-003 no implementa use cases (F-004, F-005), ni UI (F-006,
  F-007), ni validación de `ExpenseFilter` (F-005), ni
  multi-moneda, ni soft-delete, ni `IQueryRepository<T>` genérico,
  ni migraciones, ni seed de `Expense`, ni repositorios de las
  otras 13 entidades.

### RNF-6 — Determinismo en tests

- Los tests no asumen `Id`s específicos: leen `expense.Id` de la
  instancia creada (`var id = expense.Id;`) para operaciones
  subsiguientes. `CreatedAt`/`UpdatedAt` provienen de un
  `_fixedTimestamp` estático (patrón de
  `CategoryRepositoryTests`, F-006 de Fase 2). `Expense.Create`
  no llama a `DateTime.UtcNow` (F-001 INV-7).

---

## 8. Reglas de negocio

### RN-1 — `AddAsync` valida la FK a `Categories` antes de persistir

- El repo consulta `_db.Categories.FindAsync(expense.CategoryId)`
  **antes** de `_db.Expenses.Add` + `SaveChangesAsync`. Si la
  categoría no existe, devuelve
  `Result.Failure("CategoryNotFound", ...)`. Eco del principio
  "validación cross-aggregate en el repositorio" de F-001 §17.6.
- La FK `Restrict` de F-002 es la red de seguridad estructural si
  la validación previa se bypassa (p. ej., race condition).
- **Limitación:** la consulta y la inserción no están en una
  transacción explícita. Si la categoría se borra entre la
  validación y el `SaveChangesAsync`, la DB lanza
  `DbUpdateException` (FK violation). El repo **no** captura esa
  excepción (no es un error esperado; es un race condition
  extremo en single-user local — ver §12 EC-4).

### RN-2 — `UpdateAsync` / `RemoveAsync` con `Id` inexistente → `NotFound`

- `FindAsync` devuelve `null` →
  `Result.Failure("NotFound", ...)`. Eco de F-006 de Fase 1 RN-3
  y F-004 de Fase 2 RN-3.

### RN-3 — `GetByIdAsync` devuelve `null` para no-encontrado

- `FindAsync` devuelve `null` → el método devuelve `null` (no
  excepción, no `Result.Failure`). El caller verifica. Eco de
  F-006 de Fase 1 RN-4.

### RN-4 — `GetAllAsync` ordena por `Date` descendente

- Los gastos más recientes primero. Decisión replicable a otras
  transacciones (`Income` en Phase 4). `AsNoTracking` (solo
  lectura).

### RN-5 — `GetByFilterAsync` no valida el filtro

- El repo confía en que `filter.Page ≥ 1`, `filter.PageSize ≥ 1`
  y `filter.DateFrom ≤ filter.DateTo`. La validación es de F-005
  (`GetTransactionsQuery`), que devuelve
  `Result<PagedResult<ExpenseDto>>` y valida antes de llamar al
  repo (DP-3). Si el caller bypassa F-005 y pasa un filtro
  inválido, el comportamiento es indefinido (EF Core puede lanzar
  en `Skip` con valor negativo, o devolver resultados vacíos).

### RN-6 — Orden de `GetByFilterAsync`: `Date` desc, tie-break `CreatedAt` desc

- `OrderByDescending(e => e.Date).ThenByDescending(e => e.CreatedAt)`.
- El tie-break por `CreatedAt` (DP-5) garantiza un orden
  determinista cuando varios gastos comparten la misma `Date`
  (p. ej., varios gastos del mismo día): el creado más tarde
  aparece primero. `CreatedAt` ya existe en la entidad (F-001
  INV-7) y en la tabla (F-002) — ver §4.3.

### RN-7 — Hard delete, no soft-delete

- `RemoveAsync` elimina la fila físicamente. No hay `IsDeleted`,
  no hay query filter global. Decisión v1 (DP-7) eco de F-001
  §17.7.

### RN-8 — Códigos de error canónicos

- `"NotFound"` — el `Id` no existe (en `UpdateAsync`,
  `RemoveAsync`).
- `"CategoryNotFound"` — el `CategoryId` no referencia una
  `Category` existente (en `AddAsync`).
- Códigos como `string` (no enums), eco de F-005 de Fase 1 y
  F-004 de Fase 2 RN-5. Case-sensitive.

---

## 9. API surface

### 9.1 `IExpenseRepository` (extendida)

La interface gana un sexto método. Los 5 originales (F-006 de Fase
1) se mantienen idénticos:

| Método | Retorno | Notas |
| --- | --- | --- |
| `AddAsync(Expense, ct)` | `Task<Result>` | + validación FK (RF-4). |
| `UpdateAsync(Expense, ct)` | `Task<Result>` | RF-5. |
| `RemoveAsync(Guid, ct)` | `Task<Result>` | hard delete (RF-6). |
| `GetByIdAsync(Guid, ct)` | `Task<Expense?>` | RF-7. |
| `GetAllAsync(ct)` | `Task<IReadOnlyList<Expense>>` | orden `Date` desc (RF-8). |
| `GetByFilterAsync(ExpenseFilter, ct)` | `Task<PagedResult<Expense>>` | **nuevo** (RF-9). |

### 9.2 `ExpenseFilter` (nuevo record)

`public sealed record ExpenseFilter(int Page = 1, int PageSize = 50, Guid? CategoryId = null, DateOnly? DateFrom = null, DateOnly? DateTo = null);`

- Record "tonto" (DP-2): sin validación. Inmutable (record).
- `Page` 1-based (convención de `PagedResult<T>`, F-005 de Fase 1
  RN-4).
- Sin `Currency?` (DP-6).

### 9.3 `ExpenseRepository` (nuevo)

`public sealed class ExpenseRepository : IExpenseRepository` con
campo `_db`, constructor que inyecta `AppDbContext`, y los 6
métodos (RF-3 a RF-9). `sealed` (no se hereda; eco de
`CategoryRepository`).

---

## 10. Configuración técnica

### 10.1 — Ubicación de archivos

| Archivo | Acción | Justificación |
| --- | --- | --- |
| `src/ControlDeFinanzasIA.Application/Abstractions/IExpenseRepository.cs` | Editar | Añadir `GetByFilterAsync` (RF-1). Los 5 métodos originales intactos. |
| `src/ControlDeFinanzasIA.Application/Abstractions/ExpenseFilter.cs` | Crear | Record de filtros. Carpeta `Abstractions/` (flat — ver §17.9). |
| `src/ControlDeFinanzasIA.Infrastructure/Persistence/Repositories/ExpenseRepository.cs` | Crear | Repo concreto (RF-3). Carpeta `Repositories/` ya existe (F-004 de Fase 2). |
| `src/ControlDeFinanzasIA.Infrastructure/Persistence/ServiceCollectionExtensions.cs` | Editar | Registrar `IExpenseRepository` scoped (RF-10). |
| `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/ExpenseRepositoryTests.cs` | Crear | 10+ tests (§11). Carpeta `Persistence/` ya existe (F-006 de Fase 2). |

### 10.2 — Inyección de dependencias

- `ExpenseRepository` se registra como `Scoped` (un `AppDbContext`
  por scope; el repo comparte el scope). Patrón idéntico a
  `CategoryRepository` / `PaymentMethodRepository`.
- El registro va en `AddPersistence`, **tras** los repos de
  catálogo existentes:
  `services.AddScoped<IExpenseRepository, ExpenseRepository>();`
- **No** se modifica `Bootstrap.ConfigureServices` en F-003 (eso
  es F-005 de Fase 2 / Fase 3 use cases). `AddPersistence` ya
  está enchufado en `Bootstrap` desde F-005 de Fase 2.

### 10.3 — Sin `dotnet-ef`, sin migraciones

- F-003 no genera migraciones. Usa el schema de F-002
  (`AddExpenseTable`). Los tests aplican `MigrateAsync()` sobre
  SQLite in-memory (patrón de `CategoryRepositoryTests`), lo que
  ejecuta `InitialSchema` + `SeedSystemData` + `AddExpenseTable`.

### 10.4 — Convenciones (tests y código)

> **Descubrimiento importante:** las specs archivadas de F-001 y
> F-002 de Fase 3 afirman que los tests usan "xUnit +
> FluentAssertions" (convención heredada de Fase 1 §15). La
> **verificación del código real** (`CategoryRepositoryTests.cs`
> de F-006 de Fase 2) y el cache de testing-capabilities de Engram
> (`sdd/controldefinanzasia/testing-capabilities`) **desmienten**
> eso: los tests usan **pure xUnit `Assert.*`** (`Assert.True`,
> `Assert.Equal`, `Assert.NotNull`, `Assert.False`,
> `Assert.ThrowsAsync`, `Assert.Contains`). **FluentAssertions
> NO está wired** (no hay `Should()` en ningún test existente, y
> `FluentAssertions` no está en `Directory.Packages.props`).
> F-003 sigue la convención **implementada** (pure xUnit
> `Assert.*`), no la documentación aspiracional.

Convenciones de F-003:

- **Tests:** pure xUnit `[Fact]` / `[Theory]` + `[InlineData]`,
  aserciones con `Assert.*`. **No FluentAssertions.** Naming
  `Method_Scenario_ExpectedResult` (eco de
  `CategoryRepositoryTests`). Un concepto de aserción por test;
  multi-step usa múltiples `Assert.*`.
- **Códigos de error:** en inglés (`"NotFound"`,
  `"CategoryNotFound"`). **Mensajes de error:** en español
  (es-DO), consistentes con `CategoryRepository` implementado.
- **UI strings:** F-003 no produce UI (sin scope); las strings
  de UI son F-006/F-007.
- **Código:** file-scoped namespace, `using` al tope, XML doc en
  cada clase y método público, `sealed` en `ExpenseRepository` y
  `ExpenseFilter`, nombres en inglés en código, prosa de la spec
  en español neutro profesional. Eco de F-002 de Fase 3 §10.4.
- **Async/await extremo a extremo** con `CancellationToken`
  (RNF-3).
- **`Result.Success(null!)`** para comandos exitosos sin valor
  (patrón de `CategoryRepository`, no `Result.Success()`).

### 10.5 — Manejo de excepciones

- `AddAsync`: captura sólo el caso de "categoría no encontrada"
  vía validación previa (`FindAsync`). **No** captura
  `DbUpdateException` (a diferencia de `CategoryRepository` que
  captura `DuplicateName` por race condition en índice único):
  `Expense` no tiene índice único de negocio (el `Id` es
  generado por `Expense.Create`, no por el caller), y la FK
  violation es un bug del caller, no un error esperado.
- `UpdateAsync` / `RemoveAsync`: no capturan excepciones de EF
  Core; se dejan subir. La capa superior (use case / UI) las
  traduce. Eco de F-004 de Fase 2 §10.3.
- `GetByFilterAsync`: si `filter.Page ≤ 0`, `Skip` con valor
  negativo lanza `InvalidOperationException` en EF Core. El repo
  **no** captura (no es un caso esperado — F-005 valida). Ver
  §12 EC-2.

---

## 11. Validaciones — tests de integración

F-003 añade tests de integración que validan el **comportamiento**
del repositorio (CRUD, FK, filtros, paginación). Se diferencian de
los tests de **forma del schema** de F-002 (que validan columnas,
índices, FK estructural): éstos validan que el repo traduce
correctamente las operaciones de la interface a EF Core. Reutilizan
`TestAppDbContextFactory` de F-006 de Fase 2 (SQLite in-memory
compartida); **no** se inventa una factoría nueva.

Ubicación:
`tests/ControlDeFinanzasIA.Tests.Integration/Persistence/ExpenseRepositoryTests.cs`.

Patrón de cada test (espejo de `CategoryRepositoryTests.cs`):
`using var factory = new TestAppDbContextFactory();` →
`using AppDbContext context = factory.CreateContext();` →
`await context.Database.MigrateAsync();` →
`var repo = new ExpenseRepository(context);`. Los seed data (9
categorías con GUIDs conocidos como
`11111111-1111-1111-1111-000000000001` = "Comida") están
disponibles tras `MigrateAsync`. **Pure xUnit `Assert.*`** (sin
FluentAssertions, ver §10.4).

| # | Test | Verifica |
| --- | --- | --- |
| T-1 | `AddAsync_WithValidExpense_PersistsAndReturnsSuccess` | Un `Expense` con `CategoryId` sembrado se persiste; `Result.IsSuccess` es true; `GetByIdAsync` lo recupera con `Amount`/`Currency`/`Date` preservados (valida `OwnsOne` de F-002 round-trip). |
| T-2 | `AddAsync_WithUnknownCategoryId_ReturnsCategoryNotFound` | `Expense` con `CategoryId = Guid.NewGuid()` (no sembrado) → `Result.IsSuccess` false, `Error.Code == "CategoryNotFound"`. |
| T-3 | `GetByIdAsync_WithExistingId_ReturnsExpense` | Tras `AddAsync`, `GetByIdAsync(id)` devuelve la entidad con todas las propiedades. |
| T-4 | `GetByIdAsync_WithMissingId_ReturnsNull` | `GetByIdAsync(Guid.NewGuid())` devuelve `null`. |
| T-5 | `GetAllAsync_ReturnsAllOrderedByDateDesc` | Tras insertar 3 gastos con `Date` distintas, `GetAllAsync` los devuelve ordenados por `Date` descendente. |
| T-6 | `GetByFilterAsync_WithNoFilter_ReturnsFirstPageOrderedByDateDesc` | Sin filtros (`Page=1`, `PageSize=50`), devuelve la primera página ordenada por `Date` desc; `Total` coincide con el conteo real. |
| T-7 | `GetByFilterAsync_WithDateRange_FiltersCorrectly` | `DateFrom`/`DateTo` acotan los items a los que caen en el rango; `Total` refleja sólo los del rango. |
| T-8 | `GetByFilterAsync_PaginatesWithCorrectTotalAndItems` | Con `PageSize=2` y 5 gastos, página 1 devuelve 2 items, `Total=5`, `TotalPages=3`; página 3 devuelve 1 item. |
| T-9 | `UpdateAsync_WithExistingExpense_UpdatesFields` | Tras `AddAsync`, `UpdateAsync` con `Description`/`Amount` nuevos; `GetByIdAsync` refleja los cambios; `Id` y `CreatedAt` no cambian. |
| T-10 | `RemoveAsync_WithExistingExpense_DeletesAndReturnsSuccess` | Tras `AddAsync`, `RemoveAsync(id)` devuelve `Result.IsSuccess` true; `GetByIdAsync(id)` devuelve `null` (hard delete). |

**Tests recomendados adicionales (opcionales, cubren error states
y edge cases):**

- `GetByFilterAsync_WithCategoryId_FiltersCorrectly` — filtra por
  una categoría sembrada; `Total` = gastos de esa categoría.
- `GetByFilterAsync_WithPageBeyondEnd_ReturnsEmptyItemsAndCorrectTotal`
  — `Page = 999` → `Items` vacío, `Total` = real, `TotalPages` =
  real (CU-7).
- `UpdateAsync_WithMissingExpense_ReturnsNotFound` —
  `UpdateAsync` con `Id` inexistente → `Error.Code == "NotFound"`.
- `RemoveAsync_WithMissingExpense_ReturnsNotFound` —
  `RemoveAsync(Guid.NewGuid())` → `Error.Code == "NotFound"`.
- `AddAsync_ThenGetByFilterAsync_PreservesMoneyPrecision` —
  valida round-trip de `Money` con `HasPrecision(18,4)` (monto
  `1234.5678` DOP) a través del repo (eco del riesgo de F-002 de
  Fase 3 sobre conversión de value objects).

**Marco y paquetes:** xUnit v2.9.0 (ya en
`Tests.Integration.csproj` desde Fase 2). `Microsoft.Data.Sqlite`
ya presente. F-003 **no** añade paquetes. **No FluentAssertions**
(§10.4).

---

## 12. Casos borde

### EC-1 — `AddAsync` con `Expense` cuyo `CategoryId` es `Guid.Empty`

- La entidad `Expense.Create` ya rechaza `CategoryId ==
  Guid.Empty` (F-001 INV-6) lanzando `ArgumentException`. El
  repo nunca recibe un `Expense` con `CategoryId` vacío si el
  caller usa `Expense.Create`. Si el caller bypassa el factory
  (imposible: constructor privado), el repo hace `FindAsync` con
  `Guid.Empty` → `null` → `Result.Failure("CategoryNotFound",
  ...)`.

### EC-2 — `GetByFilterAsync` con `Page = 0` o negativo

- El repo **no** valida (DP-3). `skip = (0 - 1) * PageSize` =
  negativo; EF Core lanza `InvalidOperationException` en
  `Skip`. Esto **no** es un caso esperado: F-005 valida
  `Page ≥ 1` antes de llamar al repo. Si ocurre, es un bug del
  caller. El repo no captura (fail-fast). Documentado para que
  el implementador no añada validación defensiva en el repo
  (DP-3 la prohíbe para mantener la separación de
  responsabilidades).

### EC-3 — `UpdateAsync` con `CategoryId` que no existe

- `CurrentValues.SetValues` no valida la FK; la validación
  ocurre en `SaveChangesAsync`. Si el `CategoryId` nuevo no
  existe en `Categories`, la FK `Restrict` de F-002 lanza
  `DbUpdateException`. El repo **no** la captura (no es un error
  esperado de negocio; es un bug del caller que pasó una
  categoría inexistente). La capa superior (use case Phase 4)
  la traduce. Ver RF-5 y RN-1 limitación.

### EC-4 — Race condition: categoría borrada entre validación y `SaveChanges`

- `AddAsync` valida la FK con `FindAsync` **antes** de
  `SaveChangesAsync`. Si la categoría se borra entre ambos
  (otro proceso/thread), `SaveChangesAsync` lanza
  `DbUpdateException` (FK violation). El repo no captura. En
  single-user single-process (F-001 §17.8, `architecture.md`
  §12), este race es prácticamente imposible; la FK `Restrict`
  es la red de seguridad.

### EC-5 — `GetByFilterAsync` con `DateFrom > DateTo`

- El repo aplica ambos `Where` (`Date >= DateFrom` AND `Date <=
  DateTo`); si `DateFrom > DateTo`, el resultado es vacío
  (`Total = 0`, `Items` vacío). **No** lanza. El repo no valida
  el orden (DP-3); F-005 valida `DateFrom ≤ DateTo`.

### EC-6 — `GetByFilterAsync` con `PageSize = 0`

- `skip = (Page - 1) * 0 = 0`; `Take(0)` devuelve lista vacía;
  `Total` = conteo real; `PagedResult.TotalPages` = 0 (defensivo
  de `PagedResult<T>`, F-005 de Fase 1). No lanza. Caso
  degenerado; F-005 valida `PageSize ≥ 1`.

### EC-7 — `GetAllAsync` con tabla vacía

- Devuelve `IReadOnlyList<Expense>` vacío (no `null`). El
  caller verifica `Count == 0`. Eco de F-006 de Fase 1 EC-3.

### EC-8 — Dos gastos con la misma `Date` (tie-break por `CreatedAt`)

- `GetByFilterAsync` y `GetAllAsync` ordenan por `Date` desc;
  `GetByFilterAsync` añade tie-break `CreatedAt` desc (DP-5,
  RN-6). El gasto creado más tarde (mayor `CreatedAt`) aparece
  primero dentro del mismo día. `GetAllAsync` (RF-8) **no**
  añade tie-break explícito (sólo `OrderByDescending(e =>
  e.Date)`); el orden dentro de la misma `Date` es no
  determinista a nivel de DB. Esto es aceptable para
  `GetAllAsync` (sin paginar, escenario de bajo volumen); la
  paginación determinista es responsabilidad de
  `GetByFilterAsync`.

### EC-9 — `RemoveAsync` con `Guid.Empty`

- Devuelve `Result.Failure("NotFound", ...)` (RF-6 paso 1). No
  llega a `FindAsync`.

---

## 13. Dependencias

### 13.1 Prerrequisitos de implementación

- **F-001 de Fase 3 (archivada):** provee la entidad `Expense`
  en `Domain/Transactions/Expense.cs` con 8 propiedades
  (`Id`, `CategoryId`, `Date`, `Amount`, `Description`, `Note`,
  `CreatedAt`, `UpdatedAt`), factory `Create`, método `Update`,
  constructor privado parameterless para EF Core, e invariantes
  INV-1 a INV-8. **`CreatedAt` ya existe** (INV-7) — ver §4.3.
- **F-002 de Fase 3 (archivada):** provee `ExpenseConfiguration`
  con `OwnsOne` de `Money`, FK `Restrict` a `Categories`, índices
  en `Date` y `CategoryId`, y la migración `AddExpenseTable`
  (tabla `Expenses` con `CreatedAt`/`UpdatedAt` mapeadas). Es el
  schema de entrada.
- **F-006 de Fase 1 (archivada):** define `IExpenseRepository`
  con los 5 métodos estándar que F-003 implementa y extiende.
- **F-005 de Fase 1 (archivada):** provee `Result` (non-generic)
  y `PagedResult<T>` que `GetByFilterAsync` consume.
- **F-004 de Fase 2 (archivada):** provee `CategoryRepository` y
  el patrón de repositorio (`sealed`, `_db`, `FindAsync`,
  `CurrentValues.SetValues`, `AsNoTracking`, `Result.Failure`
  con códigos en inglés y mensajes en español,
  `Result.Success(null!)`) que F-003 replica. También registra
  los repos en `AddPersistence` (patrón a extender).
- **F-006 de Fase 2 (archivada):** provee `TestAppDbContextFactory`
  (SQLite in-memory) que los tests de F-003 reutilizan.
- **F-003 de Fase 2 (archivada):** sembró 9 categorías + 5
  métodos de pago con `IsSystem=true` y GUIDs conocidos. Los
  tests de `AddAsync` validan la FK contra estas categorías.
- **F-007 de Fase 1 (archivada):** los tests NetArchTest R-1 a
  R-5 que F-003 no debe romper.

### 13.2 Dependencias con otras features (Fase 3)

| Feature | Relación | Notas |
| --- | --- | --- |
| F-001 (Fase 3) | Consumidora-inversa | Provee `Expense`. F-003 no la modifica (§4.3). |
| F-002 (Fase 3) | Consumidora-inversa | Provee la tabla `Expenses`. F-003 no genera migraciones. |
| F-004 (Fase 3) | Consumidora | `RegisterExpenseUseCase` invoca `repo.AddAsync`. Mockea `IExpenseRepository` en tests unitarios (no necesita F-003 implementado, sólo la firma). |
| F-005 (Fase 3) | Consumidora | `GetTransactionsQuery` invoca `repo.GetByFilterAsync` y **valida** `ExpenseFilter` antes (DP-3). Mockea `IExpenseRepository.GetByFilterAsync` en tests unitarios. |
| F-006 (Fase 3) | Independiente | UI shell no toca persistencia. |
| F-007 (Fase 3) | Consumidora (vía use cases) | La vista consume `ExpenseDto` vía F-004/F-005, no el repo directamente. |

### 13.3 Bloqueos

- **Ninguno.** F-003 puede implementarse inmediatamente. F-001 y
  F-002 de Fase 3 están archivadas; F-006/F-005 de Fase 1,
  F-004/F-006 de Fase 2 están completas.

---

## 14. Fuera de alcance (detallado)

Repetición con énfasis. F-003 **no** hace lo siguiente:

- Implementar use cases, validadores, DTOs, `ExpenseExtensions`
  (F-004, F-005).
- Implementar UI, ViewModels, vistas, navegación, DataTemplates
  (F-006, F-007).
- Validar `ExpenseFilter` (`Page ≥ 1`, `PageSize ≥ 1`,
  `DateFrom ≤ DateTo`) — es de F-005 (DP-3).
- Añadir filtro por `Currency` (DP-6: single-currency en Phase 3).
- Añadir soft-delete / `IsDeleted` / query filters (DP-7).
- Introducir `IQueryRepository<T>` genérico (ver §17).
- Implementar `GetByFilterAsync` en los otros 14 repositorios.
- Modificar `Domain/Transactions/Expense.cs` (§4.3 — `CreatedAt`
  ya existe).
- Generar migraciones nuevas (usa el schema de F-002).
- Añadir seed data de `Expense` (dato de usuario).
- Modificar `AppDbContext.cs` (el `DbSet<Expense>` ya está desde
  F-002).
- Modificar `ExpenseConfiguration.cs` (ya está desde F-002).
- Añadir paquetes NuGet a ningún proyecto (todo lo necesario está
  desde Fases 1 y 2; en particular, **no** FluentAssertions).
- Modificar `Bootstrap.ConfigureServices` (F-003 sólo extiende
  `AddPersistence`).
- Modificar el CI workflow.

---

## 15. Criterios de aceptación

La feature se considera completada cuando **todos** los siguientes
puntos se cumplen, verificables mediante tests automatizados y
revisión de código.

### AC-1 — `IExpenseRepository` extendida

- [ ] `src/ControlDeFinanzasIA.Application/Abstractions/IExpenseRepository.cs`
      expone `Task<PagedResult<Expense>> GetByFilterAsync(ExpenseFilter filter, CancellationToken ct = default);`.
- [ ] Los 5 métodos originales (`AddAsync`, `UpdateAsync`,
      `RemoveAsync`, `GetByIdAsync`, `GetAllAsync`) se mantienen
      con las firmas exactas de F-006 de Fase 1.
- [ ] XML doc en el nuevo método.

### AC-2 — `ExpenseFilter` creado

- [ ] `src/ControlDeFinanzasIA.Application/Abstractions/ExpenseFilter.cs`
      existe como `public sealed record ExpenseFilter`.
- [ ] Propiedades: `Page` (default 1), `PageSize` (default 50),
      `CategoryId?`, `DateFrom?`, `DateTo?`.
- [ ] **Sin** auto-validación (DP-2).
- [ ] **Sin** propiedad `Currency?` (DP-6).
- [ ] File-scoped namespace, `sealed`, XML doc.

### AC-3 — `ExpenseRepository` creado

- [ ] `src/ControlDeFinanzasIA.Infrastructure/Persistence/Repositories/ExpenseRepository.cs`
      existe como `public sealed class ExpenseRepository : IExpenseRepository`.
- [ ] Constructor público que recibe `AppDbContext`.
- [ ] Implementa los 6 métodos con las firmas exactas.
- [ ] `AddAsync` valida la FK a `Categories` y devuelve
      `Result.Failure("CategoryNotFound", ...)` si no existe.
- [ ] `UpdateAsync` / `RemoveAsync` devuelven
      `Result.Failure("NotFound", ...)` si el `Id` no existe.
- [ ] `GetAllAsync` ordena por `Date` descendente, `AsNoTracking`.
- [ ] `GetByFilterAsync` aplica filtros opcionales, ordena por
      `Date` desc + tie-break `CreatedAt` desc, calcula `Total`
      con `CountAsync`, aplica `Skip`/`Take`, devuelve
      `PagedResult<Expense>`.
- [ ] `RemoveAsync` hace hard delete (no soft-delete).
- [ ] File-scoped namespace, `sealed`, XML doc, `using` al tope.

### AC-4 — `AddPersistence` registra el repo

- [ ] `ServiceCollectionExtensions.AddPersistence` registra
      `IExpenseRepository → ExpenseRepository` como `Scoped`,
      tras `ICategoryRepository` e `IPaymentMethodRepository`.
- [ ] El XML doc de `AddPersistence` lista `IExpenseRepository`.

### AC-5 — Tests de integración

- [ ] `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/ExpenseRepositoryTests.cs`
      existe.
- [ ] Al menos 10 tests (T-1 a T-10 de §11) pasan cubriendo CRUD,
      FK validation (`CategoryNotFound`), `NotFound` implícito,
      paginación y filtros por rango de fecha.
- [ ] Los tests reutilizan `TestAppDbContextFactory` (F-006 de
      Fase 2); no se inventa una factoría nueva.
- [ ] Los tests usan **pure xUnit `Assert.*`** (sin
      FluentAssertions, §10.4).
- [ ] Los tests aplican `MigrateAsync()` (traen seed de
      categorías para validar la FK).

### AC-6 — Compilación y tests

- [ ] `dotnet build -c Release` desde la raíz: 0 warnings, 0
      errors.
- [ ] `dotnet test`: ~140 tests heredados + ~10 nuevos ≈ 150
      verdes, 0 failures.
- [ ] Los 11 tests de NetArchTest (R-1 a R-5) pasan sin
      modificaciones.
- [ ] `dotnet list src/ControlDeFinanzasIA.Domain package` sigue
      vacío (R-1).
- [ ] `dotnet format style --verify-no-changes`: exit 0.

### AC-7 — Sin scope creep

- [ ] NO se ha implementado `RegisterExpenseUseCase` /
      `GetTransactionsQuery` / DTOs (F-004, F-005).
- [ ] NO se ha añadido UI (F-006, F-007).
- [ ] NO se ha añadido validación de `ExpenseFilter` en el repo
      (DP-3 — es de F-005).
- [ ] NO se ha añadido filtro por `Currency` (DP-6).
- [ ] NO se ha añadido soft-delete (DP-7).
- [ ] NO se ha introducido `IQueryRepository<T>` genérico.
- [ ] NO se ha modificado `Domain/Transactions/Expense.cs` (§4.3).
- [ ] NO se ha generado ninguna migración nueva.
- [ ] NO se ha modificado `AppDbContext.cs` ni
      `ExpenseConfiguration.cs` (F-002 los provee).
- [ ] NO se ha añadido FluentAssertions (§10.4).
- [ ] NO se ha modificado `Bootstrap.ConfigureServices`.

### AC-8 — Convenciones

- [ ] File-scoped namespace en `ExpenseRepository.cs`,
      `ExpenseFilter.cs`, y `ExpenseRepositoryTests.cs`.
- [ ] `using` directives al tope.
- [ ] XML doc en cada clase y método público.
- [ ] `sealed` en `ExpenseRepository` y `ExpenseFilter`.
- [ ] Códigos de error en inglés (`"NotFound"`,
      `"CategoryNotFound"`); mensajes en español.
- [ ] Tests con `Assert.*` (no `Should`).

### AC-9 — Documentación y trazabilidad

- [ ] `spec/phases/phase-03/features.md` actualizado: F-003
      marcada como ✅ COMPLETADA en la tabla resumen y en el
      header de la sección F-003 (tras apply + verify).
- [ ] La spec se renombra a
      `spec/features/phase-03/F-003-expense-repository.done.md`
      tras apply + verify.

### AC-10 — Criterios de cierre de fase (criterios para considerarla completada)

- [ ] `dotnet build` desde la raíz: 0 warnings, 0 errors (Debug
      y Release).
- [ ] `dotnet test` desde la raíz: ~140 + ~10 ≈ **150 verdes**,
      0 failures.
- [ ] NetArchTest R-1 a R-5 verdes (la extensión de la interface
      en `Application` no rompe reglas; el repo en
      `Infrastructure` implementa la interface de `Application`).
- [ ] `dotnet format style --verify-no-changes`: exit 0.

---

## 16. Estructura de archivos esperada

```
src/
└── (sin cambios en Domain)

src/ControlDeFinanzasIA.Application/
└── Abstractions/                                   (existente, flat)
    ├── IExpenseRepository.cs                       ← F-003 (EDITAR: añadir GetByFilterAsync)
    ├── ExpenseFilter.cs                            ← F-003 (NUEVO)
    └── (IClock, IPathResolver, IUnitOfWork, los 14 I*Repository — sin cambios)

src/ControlDeFinanzasIA.Infrastructure/
└── Persistence/                                    (existente)
    ├── AppDbContext.cs                             (F-002, sin cambios — DbSet<Expense> ya existe)
    ├── ServiceCollectionExtensions.cs              ← F-003 (EDITAR: AddScoped<IExpenseRepository, ExpenseRepository>)
    ├── Configurations/ExpenseConfiguration.cs      (F-002, sin cambios)
    ├── Migrations/                                  (F-002, sin cambios)
    └── Repositories/                               (existente)
        ├── CategoryRepository.cs                   (F-004 Fase 2, sin cambios)
        ├── PaymentMethodRepository.cs              (F-004 Fase 2, sin cambios)
        └── ExpenseRepository.cs                    ← F-003 (NUEVO)

tests/ControlDeFinanzasIA.Tests.Integration/
└── Persistence/                                    (existente)
    ├── TestAppDbContextFactory.cs                  (F-006 Fase 2, reutilizado)
    ├── CategoryRepositoryTests.cs                  (F-006 Fase 2, sin cambios)
    ├── PaymentMethodRepositoryTests.cs             (F-006 Fase 2, sin cambios)
    ├── ExpenseSchemaTests.cs                       (F-002 Fase 3, sin cambios)
    └── ExpenseRepositoryTests.cs                   ← F-003 (NUEVO)
```

**Modificaciones a `.csproj`:** ninguna. F-003 no añade paquetes
(en particular, no FluentAssertions) ni referencias de proyecto.

**Modificaciones al CI workflow:** ninguna.

**Modificaciones a `Domain`, `UI`, `Tests.Unit`:** ninguna.

---

## 17. Decisiones de diseño explícitas

Las decisiones DP-1 a DP-8 están **comprometidas** desde la fase
de propuesta del ciclo SDD de F-003; no se reabren. DP-9 (ubicación
de `ExpenseFilter`) se resuelve aquí.

### 17.1 — `GetByFilterAsync` devuelve `Task<PagedResult<Expense>>` (no envuelto en `Result`) — DP-1

**Decisión:** el método devuelve `Task<PagedResult<Expense>>`,
**no** `Task<Result<PagedResult<Expense>>>`.

**Justificación:** la convención de queries de F-006 de Fase 1
establece que los queries devuelven `Task<T>` directo (sin
envolver en `Result`), porque el caller (un use case o query)
verifica el resultado por su cuenta. Los **commands** (`AddAsync`,
`UpdateAsync`, `RemoveAsync`) devuelven `Task<Result>` para
reportar errores esperados de negocio (`NotFound`,
`CategoryNotFound`). Los **queries** (`GetByIdAsync`,
`GetAllAsync`, `GetByFilterAsync`) devuelven el valor directo
(`Task<Expense?>`, `Task<IReadOnlyList<Expense>>`,
`Task<PagedResult<Expense>>`). La validación de los parámetros del
filtro (`Page`, `PageSize`) se delega a F-005, que **sí** devuelve
`Result<PagedResult<ExpenseDto>>` y valida antes de llamar al repo
(DP-3). Eco de F-006 de Fase 1 §1 nota de diseño y F-005 de Fase 1.

### 17.2 — `ExpenseFilter` es un record "tonto" sin auto-validación — DP-2

**Decisión:** `ExpenseFilter` no valida `Page ≥ 1`, `PageSize ≥
1`, ni `DateFrom ≤ DateTo` en construcción.

**Justificación:** separación de responsabilidades. El record es
un contrato de transporte de parámetros de lectura, no un value
object de dominio. La validación pertenece al use case que **ya**
devuelve `Result` (F-005 `GetTransactionsQuery`). Duplicar la
validación en el record + en el use case + en el repo añade
complejidad sin valor. El repo confía en el caller (DP-3). Si una
feature futura necesita reusar `ExpenseFilter` con validación, se
introduce un `ValidatedExpenseFilter` o se añade validación en
ese momento (YAGNI ahora).

### 17.3 — La validación del filtro vive en F-005, no en el repo — DP-3

**Decisión:** `ExpenseRepository.GetByFilterAsync` **no** valida
`filter.Page`, `filter.PageSize`, ni el orden
`DateFrom`/`DateTo`. `GetTransactionsQuery` (F-005) valida antes
de llamar al repo.

**Justificación:** el repo es una capa de persistencia, no de
validación de negocio (eco de F-006 de Fase 1 RN-1). El use case
F-005 devuelve `Result<PagedResult<ExpenseDto>>` y es el lugar
natural para validar los parámetros del query y mapear los
errores a códigos (`"InvalidFilter"`, DP-4). Mantener la
validación fuera del repo permite que el repo sea un adaptador
delgado y testable sin lógica de negocio. Riesgo: si un caller
bypassa F-005 y llama al repo con un filtro inválido, el
comportamiento es indefinido (EF Core puede lanzar en `Skip`
negativo). Mitigación: el repo es `internal`-ish (sólo se
consume vía la interface desde use cases que validan); ver §12
EC-2.

### 17.4 — Códigos de error en inglés — DP-4

**Decisión:** los códigos (`Result.Error.Code`) son `"NotFound"`,
`"CategoryNotFound"`, y (en F-005) `"InvalidFilter"`. En inglés.

**Justificación:** los códigos son identificadores técnicos
consumidos por la UI en un `switch` para mostrar mensajes
localizados (F-005 de Fase 1 RNF-4, F-004 de Fase 2 RF-8).
Consistente con los códigos ya usados por `CategoryRepository`
(`"NotFound"`, `"DuplicateName"`, `"SystemCategoryImmutable"`).
Los **mensajes** (`Result.Error.Message`) van en español (es-DO),
eco del `CategoryRepository` implementado (F-004 de Fase 2 RNF-4).

### 17.5 — Orden secundario (tie-break) por `CreatedAt` desc — DP-5

**Decisión:** `GetByFilterAsync` ordena por `Date` descendente
con tie-break `CreatedAt` descendente:
`OrderByDescending(e => e.Date).ThenByDescending(e => e.CreatedAt)`.

**Justificación:** cuando varios gastos comparten la misma
`Date` (mismo día), el tie-break por `CreatedAt` produce un
orden determinista (el creado más tarde aparece primero) y
estable dentro de una página. Sin tie-break, el orden de la DB
dentro de la misma `Date` es no determinista, lo que rompe la
paginación (un item podría aparecer en dos páginas o en ninguna
al navegar). `CreatedAt` ya existe en la entidad (F-001 INV-7)
y en la tabla (F-002) — **no se requiere cambio a la entidad ni
migración** (ver §4.3). `GetAllAsync` (RF-8) no añade tie-break
explícito (escenario sin paginar, bajo volumen); la paginación
determinista es responsabilidad de `GetByFilterAsync` (§12
EC-8).

### 17.6 — Single-currency en Phase 3, sin filtro `Currency?` — DP-6

**Decisión:** `ExpenseFilter` no expone `Currency?`.

**Justificación:** Phase 3 asume single-currency (el phase plan
F-001: "en Phase 3 sólo se persistirá `Currency.DOP` por
defecto"). Filtrar por moneda no tiene sentido con una sola
moneda activa. Phase 5 (multi-moneda real en `Transaction`)
podrá añadir `Currency?` a `ExpenseFilter` **sin romper la
firma** de `GetByFilterAsync` (añadir una propiedad nullable a
un record es backward-compatible para callers existentes que no
la seteen). YAGNI ahora.

### 17.7 — Hard delete, no soft-delete — DP-7

**Decisión:** `RemoveAsync` elimina la fila físicamente. No hay
`IsDeleted`, no hay query filter global.

**Justificación:** eco de F-001 §17.7. `vision.md` no menciona
recuperación de gastos borrados ni papelera. La app es
single-user local; el backup manual (Fase 19) es la red de
seguridad. Añadir soft-delete ahora especula un requisito que no
existe y complica cada query (`IsDeleted == false` en
`GetAllAsync`/`GetByFilterAsync`). Phase 4+ puede revisitar si el
usuario pide "deshacer borrado".

### 17.8 — `CreatedAt` ya existe en `Expense` (sin cambio a F-001) — DP-8

**Decisión:** F-003 **no** modifica `Domain/Transactions/Expense.cs`.

**Justificación:** la fase de propuesta consideró brevemente que
`Expense` carecía de `CreatedAt` (necesario para el tie-break de
DP-5). La verificación contra specs archivadas desmintió eso:
F-001 de Fase 3 invariante **INV-7** define `CreatedAt: DateTime`
y `UpdatedAt: DateTime` como propiedades de la entidad, seteadas
en `Create` y `Update`. F-002 de Fase 3 ya las mapea a columnas
`TEXT` `IsRequired` en la tabla `Expenses`. Los tests T-1 y T-12
de F-001 cubren `CreatedAt`/`UpdatedAt`. **No se añade
`CreatedAt` de nuevo, no se modifica la entidad, no se genera
migración.** Ver §4.3 para el detalle completo de la
verificación. Esta decisión corrige el error inicial del
orchestrator que asumía un cambio de entidad.

### 17.9 — `ExpenseFilter` en `Abstractions/` (flat), no en `Abstractions/Queries/` ni en `Common/`

**Decisión:** `ExpenseFilter.cs` vive directamente en
`src/ControlDeFinanzasIA.Application/Abstractions/` (carpeta
flat existente), **no** en un nuevo subfolder
`Abstractions/Queries/` ni en `Application/Common/`.

**Justificación:** la carpeta `Abstractions/` es actualmente
flat (los 15 `I*Repository` + `IClock` + `IPathResolver` +
`IUnitOfWork` están directamente en ella; verificado vía `glob`).
El phase plan ofrecía "Abstractions/Queries/ o Application/Common/";
como `Queries/` no existe, la instrucción de la fase de spec
indica usar `Abstractions/` (flat) como fallback. `ExpenseFilter`
está estrechamente acoplado a `IExpenseRepository.GetByFilterAsync`
(vive en el mismo namespace `ControlDeFinanzasIA.Application.
Abstractions`, sin `using` extra). Crear un subfolder
`Queries/` para un solo archivo es premature abstraction
(YAGNI); cuando Phase 4+ añada más filtros/queries, se podrá
extraer `Queries/` con un refactor trivial. `Common/` se
descarta porque ahí viven tipos de retorno (`Result`,
`PagedResult`), no contratos de entrada de queries.

**Alternativa descartada:** `Application/Common/ExpenseFilter.cs`
— semánticamente `Common` es para tipos de retorno compartidos
(`Result`, `PagedResult`), no para DTOs de entrada de queries.

---

## 18. Riesgos y mitigaciones

| Riesgo | Probabilidad | Impacto | Mitigación |
| --- | --- | --- | --- |
| `GetByFilterAsync` no se traduce bien a SQL en SQLite (`ThenByDescending` sobre `CreatedAt` con `OwnsOne` de `Money`). | Baja | Medio | El `OrderByDescending(e => e.Date).ThenByDescending(e => e.CreatedAt)` opera sobre columnas primitivas (`Date` `TEXT`, `CreatedAt` `TEXT`), no sobre `Money`. Verificar vía Context7 al implementar; el test T-6/T-8 valida el orden en SQLite in-memory. |
| `Skip` con valor negativo (`Page = 0`) lanza en EF Core. | Baja | Bajo | DP-3: el repo no valida; F-005 valida `Page ≥ 1`. EC-2 documenta. No añadir validación defensiva en el repo (rompe DP-3). |
| Paginación no determinista sin tie-break (item en dos páginas). | Media | Medio | DP-5: tie-break `CreatedAt` desc en `GetByFilterAsync`. Test T-8 valida. `GetAllAsync` no pagina (bajo volumen). |
| `AddAsync` no detecta `CategoryNotFound` por race (categoría borrada entre validación y `SaveChanges`). | Muy baja (single-user) | Bajo | RN-1 limitación + EC-4. La FK `Restrict` de F-002 es la red de seguridad (`DbUpdateException`). |
| `UpdateAsync` con `CategoryId` inexistente lanza `DbUpdateException` no capturado. | Baja | Medio | RF-5 + EC-3: el repo no captura (no es error esperado). El use case (Phase 4) valida `CategoryId` antes de `Update`. |
| Round-trip de `Money` con `HasPrecision(18,4)` pierde decimales. | Baja | Alto | F-002 ya fijó `OwnsOne` con `HasPrecision(18,4)`. Test T-1 (y el opcional `PreservesMoneyPrecision`) validan round-trip con `1234.5678` DOP. |
| El developer añade FluentAssertions "porque la doc de F-001/F-002 lo dice". | Media | Bajo | §10.4 documenta la discrepancia: la doc aspiracional dice FluentAssertions, el código real usa pure xUnit `Assert.*`. AC-5 y AC-8 verifican `Assert.*`. |
| El developer valida `ExpenseFilter` en el repo (rompe DP-3). | Media | Bajo | DP-3 + RN-5 + AC-7 lo prohíben. La validación es de F-005. |
| `IExpenseRepository` pierde uniformidad con los otros 14 repos (riesgo del phase plan). | Aceptado | Bajo | §17 + phase plan "IExpenseRepository extensión": el método es específico de `Expense` por la paginación de transacciones. No se introduce `IQueryRepository<T>` genérico. Cada repo evalúa añadirlo caso a caso. |
| `dotnet format style` propone cambios inesperados. | Baja | Bajo | Aplicar los cambios. No relajar reglas globales. |
| R-2 falla porque `Application` adquiere una dependencia nueva. | Muy baja | Alto | `ExpenseFilter` sólo usa BCL (`int`, `Guid?`, `DateOnly?`). `IExpenseRepository` ya referencia `PagedResult` (`Application.Common`) y `Expense` (`Domain`). No hay `PackageReference` nueva. NetArchTest R-2 verifica. |

---

## 19. Referencias

- `docs/roadmap.md` §Fase 3 (Entregables esperados: repositorio
  de `Expense`; Riesgos: extensión de `IExpenseRepository`).
- `docs/architecture.md` §3 (solución y proyectos), §4 (grafo
  de dependencias, R-1 a R-5), §7 (use cases / queries devuelven
  `Result<T>` o `PagedResult<T>`), §8 (Infrastructure:
  repositorios implementan interfaces de `Application`,
  `IEntityTypeConfiguration<T>`, PascalCase), §12 (single-user
  single-process — justifica no soft-delete / no concurrencia),
  §19 (ADR-007 `IClock`).
- `docs/vision.md` §4.1 (gastos del día a día), §4.3 (listado
  básico — subconjunto "listar", sin filtros UI todavía).
- `spec/constitution/mission.md` — Clean Architecture,
  "Evitar duplicación de lógica" (justifica que el repo no
  re-validue invariantes de `Expense` — F-001 las blinda).
- `spec/constitution/tech-stack.md` — async/await extremo a
  extremo, errores como valores, MVVM estricto (no aplica a
  F-003, pero fija el tono).
- `spec/phases/phase-03/features.md` — F-003 dentro del
  desglose de la fase 3 (objetivo, descripción breve con los 6
  métodos + `ExpenseFilter` + `AddPersistence` + 10 tests,
  criterios de cierre).
- `spec/features/phase-03/F-001-expense-domain-entity.done.md`
  — la entidad `Expense` con invariantes INV-1 a INV-8
  (**INV-7 = `CreatedAt`/`UpdatedAt` ya existen**, ver §4.3),
  factory `Create`, `Update`, constructor privado parameterless
  para EF Core.
- `spec/features/phase-03/F-002-expense-configuration-migration.done.md`
  — la tabla `Expenses` con `OwnsOne` de `Money`, FK `Restrict`
  a `Categories`, índices en `Date` y `CategoryId`, columnas
  `CreatedAt`/`UpdatedAt` mapeadas — schema de entrada de F-003.
- `spec/features/phase-01/F-006-contrato-interfaces-repo.done.md`
  — `IExpenseRepository` con los 5 métodos estándar que F-003
  implementa y **extiende**; convención de queries (`Task<T>`
  directo) vs commands (`Task<Result>`).
- `spec/features/phase-01/F-005-comuntypes-result-pagedresult.done.md`
  — `Result` (non-generic) para commands y `PagedResult<T>`
  (`Items`, `Total`, `Page`, `PageSize`, `TotalPages` calculado)
  que `GetByFilterAsync` consume.
- `spec/features/phase-02/F-004-category-paymentmethod-repositories.done.md`
  — patrón de repositorio (`sealed`, `_db`, `FindAsync`,
  `CurrentValues.SetValues`, `AsNoTracking`, `Result.Failure`
  con códigos en inglés + mensajes en español,
  `Result.Success(null!)`, registro en `AddPersistence`) que
  F-003 replica.
- `spec/features/phase-02/F-006-integration-tests.done.md` —
  `TestAppDbContextFactory` (SQLite in-memory) que los tests de
  F-003 reutilizan.
- `spec/README.md` — convención del directorio `spec`
  (numeración local por fase, sufijos `.spec.md` → `.done.md`).
- Engram `sdd/controldefinanzasia/testing-capabilities` —
  confirma pure xUnit `Assert.*` (FluentAssertions **no**
  wired), comando `dotnet test -c Release --no-build`, patrón
  `TestAppDbContextFactory` fixture.
- EF Core 8 documentation oficial (a consultar vía Context7 al
  implementar: `ThenByDescending` sobre `DateTime` en SQLite,
  `Skip`/`Take` traducción, `CountAsync` + `ToListAsync` doble
  query).
