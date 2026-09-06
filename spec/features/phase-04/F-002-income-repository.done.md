# Specification — F-002: `IncomeRepository` + `IncomeFilter` + tests de integración

> **Fase:** 4 — Slice vertical: registrar, actualizar y eliminar ingresos
> **Feature:** F-002 (local a esta fase)
> **Spec ID:** SPEC-F002P4
> **change_id:** `f-002p4-income-repository`
> **Modo:** Standard
> **Estado:** Pendiente de implementación
>
> **Nota de numeración:** este `F-002` es el de la **Fase 4**
> (`spec/phases/phase-04/features.md`). No debe confundirse con el
> `F-002` de Fase 3 (`ExpenseConfiguration` + migración). Los IDs son
> locales a cada fase; el folder los desambigua. Ver `spec/README.md`.
>
> Fuentes de verdad: `docs/vision.md`, `docs/architecture.md`,
> `docs/roadmap.md` (§Fase 4), `spec/constitution/tech-stack.md`,
> `spec/constitution/mission.md`,
> `spec/phases/phase-04/features.md` (§RF-3, RF-4, RF-5 — líneas
> 142-160),
> `spec/features/phase-04/F-001-income-entity-config-migration.done.md`
> (la entidad `Income` + `IncomeConfiguration` + migración
> `AddIncomesTable` — contrato de entrada),
> `spec/features/phase-03/F-003-expense-repository.done.md` (patrón
> espejo estructural: `ExpenseRepository` / `ExpenseFilter` /
> `ExpenseRepositoryTests`),
> `spec/features/phase-01/F-006-contrato-interfaces-repo.done.md`
> (convención commands `Task<Result>` vs queries `Task<T>`),
> `spec/features/phase-01/F-005-comuntypes-result-pagedresult.done.md`
> (`Result` non-generic + `PagedResult<T>` 4-arg),
> `spec/features/phase-02/F-006-integration-tests.done.md`
> (`TestAppDbContextFactory`).

---

## 1. Metadata

| Campo | Valor |
| --- | --- |
| **change_id** | `f-002p4-income-repository` |
| **spec_id** | `SPEC-F002P4` |
| **modo** | Standard (SDD: propose → spec → tasks → apply → verify → archive) |
| **fase** | 4 |
| **feature** | F-002 (local a Fase 4) |
| **dependencies (build)** | F-001 Fase 4 (`Income`, `IncomeConfiguration`, migración `AddIncomesTable`, `DbSet<Income>` en `AppDbContext`) ✅ archivada |
| **dependencies (test infra)** | F-006 Fase 2 (`TestAppDbContextFactory`, SQLite in-memory) ✅; F-003 Fase 2 (seed de 9 categorías con GUIDs conocidos) ✅ |
| **dependencies (contrato)** | F-006 Fase 1 (`IIncomeRepository` 5 métodos base) ✅; F-005 Fase 1 (`Result`, `PagedResult<T>`) ✅; F-003 Fase 3 (`ExpenseRepository` — patrón espejo) ✅ |
| **unblocks** | F-003 Fase 4 (`RegisterIncomeUseCase`), F-005 Fase 4 (`UpdateIncomeUseCase` / `DeleteTransactionUseCase`), F-008 Fase 4 (`GetIncomesQuery` / UI) |
| **artifact_store** | Engram (objetivo: `sdd/f-002p4-income-repository/spec`) + filesystem (`spec/features/phase-04/F-002-income-repository.spec.md`) |
| **status** | Pendiente de implementación (renombrar a `.done.md` tras apply + verify) |
| **línea base de tests (pre-F-002)** | ~204 unit + ~45 integration ≈ 249 (aproximada, según contrato de lanzamiento) |

---

## 2. Contexto y objetivo

F-001 de Fase 4 entregó la base de `Income`: la entidad de dominio
blindada con invariantes (incluyendo `CategoryId` **nullable**, a
diferencia de `Expense`), `IncomeConfiguration` con `OwnsOne` de
`Money` y FK **`SetNull`** a `Categories`, la migración
`AddIncomesTable`, el `DbSet<Income> Incomes` en `AppDbContext`, y los
`IncomeSchemaTests` que validan la forma del schema. Pero una tabla sin
repositorio es almacenamiento inaccesible: ningún use case de escritura
ni query de lectura puede materializar un ingreso en disco hasta que
exista un adaptador que traduzca las operaciones de la interface
`IIncomeRepository` a EF Core.

F-002 cierra el lado de persistencia del slice vertical de ingresos. Es
un **espejo estricto** de F-003 de Fase 3 (`ExpenseRepository` +
`ExpenseFilter` + `ExpenseRepositoryTests`), adaptando las dos
diferencias semánticas de `Income`: (1) `CategoryId` es **nullable**, lo
que cambia la validación de FK en `AddAsync` — el chequeo se omite
cuando `CategoryId == null` (un ingreso sin categoría es válido); (2)
`Income.Create` añade un **7º parámetro `source`** (origen del ingreso)
que `Expense` no tiene, lo que se refleja en el helper de test
`CreateIncome`.

**Discrepancia resuelta — `IIncomeRepository` tenía 5 métodos, no 6.**
La instrucción de lanzamiento original afirmaba que `IIncomeRepository`
"ya existe con firma de 6 métodos". La verificación del código real
(`src/.../Application/Abstractions/IIncomeRepository.cs`, 57 líneas)
desmiente esa premisa: la interface declara **sólo 5 métodos**
(`AddAsync`, `UpdateAsync`, `RemoveAsync`, `GetByIdAsync`,
`GetAllAsync`) — **falta `GetByFilterAsync`**, y tampoco referencia
`IncomeFilter`. El phase plan RF-3 (línea 148) lo lista explícitamente
como parte del contrato. **Decisión (locked): MODIFICAR
`IIncomeRepository`** para añadir `GetByFilterAsync(IncomeFilter, ct) →
Task<PagedResult<Income>>` con el mismo shape que
`IExpenseRepository.GetByFilterAsync` (líneas 58-70). Sin esta
modificación, `GetByFilterAsync` no quedaría en el contrato de la
abstracción y se rompe la simetría con `Expense`. Ver §3 D-1 y §9
Riesgo R-1.

**Objetivos concretos:**

1. **Crear `IncomeFilter`** (record "tonto", 6 props incluyendo
   `TagIds` por estabilidad de firma; `TagIds` ignorado hasta 4-007).
2. **Extender `IIncomeRepository`** con `GetByFilterAsync`.
3. **Implementar `IncomeRepository`** (`sealed class : IIncomeRepository`,
   6 métodos, espejo de `ExpenseRepository`).
4. **Registrar `IIncomeRepository → IncomeRepository`** scoped en
   `AddPersistence`.
5. **Cubrir el repo con ≥12 tests de integración** (14 planeados)
   reutilizando `TestAppDbContextFactory`, pure xUnit `Assert.*`.
6. **Mantener NetArchTest R-1..R-5 verdes**; `dotnet build` 0 warnings;
   `dotnet format style --verify-no-changes` exit 0.

---

## 3. Decisiones (locked-in — no re-abrir)

### D-1 — MODIFICAR `IIncomeRepository` (resuelve la discrepancia crítica)

**Decisión:** `IIncomeRepository.cs` se modifica para añadir el 6º
método `GetByFilterAsync(IncomeFilter, ct) → Task<PagedResult<Income>>`
+ XML doc (espejo de `IExpenseRepository` líneas 58-70). Los 5 métodos
originales quedan intactos.

**Justificación:** la interface real tiene 5 métodos (verificado). El
phase plan RF-3 exige 6. Sin el método en la abstracción, los use cases
(F-003/F-005/F-008) no podrían invocarlo vía `IIncomeRepository`
inyectado, y se rompe la simetría con `IExpenseRepository`. La
instrucción "DO NOT MODIFY IIncomeRepository" partía de una premisa
falsa; la modificación es arquitectónicamente correcta y es **in-scope
para F-002**. No se requiere `using` nuevo: `IncomeFilter` vivirá en el
mismo namespace `Application.Abstractions`, y `PagedResult<T>` ya está
cubierto por el `using ControlDeFinanzasIA.Application.Common;`
existente (línea 1 del archivo).

### D-2 — `IncomeFilter` con 6 props INCLUYENDO `TagIds` (override del draft de propuesta)

**Decisión:** `IncomeFilter` tiene 6 propiedades: `Page` (default 1),
`PageSize` (default 50), `CategoryId?`, `DateFrom?`, `DateTo?`, y
**`IReadOnlyList<Guid>? TagIds`**. Esto **contradice** el draft de
propuesta (que recomendaba 5 props sin `TagIds`) pero coincide
**verbatim** con el phase plan RF-4 (línea 151).

**Justificación:** `TagIds` se incluye por **estabilidad de firma**: la
propiedad existe ahora para que 4-007 (filtros por tags) pueda añadir la
lógica sin romper la firma de `GetByFilterAsync` ni los callers.
`GetByFilterAsync` **IGNORA** `TagIds` en F-002 (no aplica el filtro);
la propiedad es un placeholder de transporte. El test
`GetByFilterAsync_IgnoresTagIds_ForNow` (T-12) blinda este contrato
diferido.

### D-3 — Forma `init` + XML doc por propiedad (no la notación posicional)

**Decisión:** `IncomeFilter` se implementa con propiedades `init`
(mirando `ExpenseFilter.cs`), **no** con la notación posicional
`record IncomeFilter(int Page = 1, ...)` del shorthand de RF-4.

**Justificación:** AC-1 exige "XML doc summary on the type + each
property (mirror `ExpenseFilter`)". Una record posicional no permite
XML doc por propiedad generada. El propio proposal aclaró: "La notación
posicional del committed decision es shorthand del mismo shape; se
privilegia el espejo de `ExpenseFilter` para satisfacer AC-1". Las
defaults son idénticas a las posicionales. Semántica equivalente;
elección de forma.

### D-4 — `GetByFilterAsync` retorna `Task<PagedResult<Income>>` (no envuelto en `Result`)

**Decisión:** el query devuelve `PagedResult<Income>` directo, no
`Result<PagedResult<Income>>`.

**Justificación:** convención de queries de F-006 de Fase 1: los
queries devuelven `Task<T>` (el caller verifica); los commands
(`AddAsync`/`UpdateAsync`/`RemoveAsync`) devuelven `Task<Result>` para
reportar errores de negocio. Espejo exacto de
`ExpenseRepository.GetByFilterAsync`. La validación de los parámetros
del filtro (`Page ≥ 1`, `PageSize ≥ 1`, `DateFrom ≤ DateTo`) vive en el
caller (F-003/F-008), no en el repo (DP-3).

### D-5 — `AddAsync` valida FK sólo si `CategoryId != null` (diferencia clave vs `Expense`)

**Decisión:** si `income.CategoryId is { } catId`, el repo hace
`_db.Categories.FindAsync(new object?[] { catId }, ct)`; si la
categoría no existe → `Result.Failure("CategoryNotFound", $"La
categoría {catId} no existe.")`. Si `CategoryId == null`, **se omite el
chequeo** y se persiste directamente (ingreso sin categoría es válido).

**Justificación:** `Income.CategoryId` es `Guid?` (nullable), a
diferencia de `Expense.CategoryId` que es `Guid` (required). Además,
`IncomeConfiguration` usa **`OnDelete(DeleteBehavior.SetNull)`** (línea
40), no `Restrict` como `Expense`: null `CategoryId` es un estado
válido y soportado por el schema. `Income.Create` ya rechaza
`Guid.Empty` para `CategoryId` (invariante de dominio, `Income.cs`
línea 228), así que el repo no añade guard extra de `Guid.Empty` para la
FK. El test `AddAsync_WithNullCategoryId_Persists` (T-3) blinda esta
rama.

### D-6 — `GetAllAsync` y `GetByFilterAsync` ordenan `Date desc, CreatedAt desc`

**Decisión:** ambos métodos aplican
`OrderByDescending(e => e.Date).ThenByDescending(e => e.CreatedAt)`.

**Justificación:** consistencia de desempate con `ExpenseRepository`
(DP-5 de F-003). El tie-break por `CreatedAt` garantiza un orden
determinista cuando varios ingresos comparten la misma `Date` (mismo
día), evitando que un item aparezca en dos páginas o en ninguna al
navegar. `CreatedAt` ya existe en la entidad (F-001) y en la tabla
(`IncomeConfiguration` línea 34) — no se requiere cambio a la entidad ni
migración. A diferencia de `ExpenseRepository.GetAllAsync` (que en F-003
sólo ordenaba por `Date`), aquí `GetAllAsync` **también** aplica el
tie-break `CreatedAt desc` (espejo del phase plan RF-5 línea 157, que lo
exige explícitamente).

### D-7 — Hard delete en `RemoveAsync` (no soft-delete)

**Decisión:** `RemoveAsync` elimina la fila físicamente. No hay
`IsDeleted`, no hay query filter.

**Justificación:** eco de DP-7 de F-003. `vision.md` no menciona
papelera de ingresos. La app es single-user local; el backup manual
(Fase 19) es la red de seguridad.

### D-8 — Validación del filtro vive en el use case (F-003/F-008), no en el repo

**Decisión:** `IncomeRepository.GetByFilterAsync` **no** valida
`filter.Page`, `filter.PageSize`, ni el orden `DateFrom`/`DateTo`.

**Justificación:** el repo es una capa de persistencia, no de validación
de negocio. El caller (use case que devuelve `Result`) valida antes de
llamar al repo (DP-3). Si un caller bypassa la validación, el
comportamiento es indefinido (EF Core puede lanzar en `Skip` negativo).

---

## 4. Criterios de aceptación

La feature se considera completada cuando **todos** los ACs siguientes
se cumplen, verificables mediante tests automatizados y revisión de
código. Cada AC incluye escenarios Given/When/Then.

### AC-1 — Record `IncomeFilter`

- [ ] `src/ControlDeFinanzasIA.Application/Abstractions/IncomeFilter.cs`
      existe como `public sealed record IncomeFilter` con 6 props
      (`Page`, `PageSize`, `CategoryId?`, `DateFrom?`, `DateTo?`,
      `TagIds`) en forma `init` (D-3), file-scoped namespace
      `ControlDeFinanzasIA.Application.Abstractions`, XML doc en el
      tipo + cada propiedad.
- [ ] Defaults: `Page = 1`, `PageSize = 50`, resto `null`.
- [ ] Sin auto-validación (D-8).

```csharp
namespace ControlDeFinanzasIA.Application.Abstractions;

/// <summary>Contrato de transporte para filtrar y paginar ingresos
/// en <see cref="IIncomeRepository.GetByFilterAsync"/>.</summary>
/// <remarks>Record "tonto": no valida. La validación es del caller.
/// <see cref="TagIds"/> existe por estabilidad de firma y es ignorado
/// por el repositorio hasta 4-007.</remarks>
public sealed record IncomeFilter
{
    /// <summary>Número de página (1-based). Default: 1.</summary>
    public int Page { get; init; } = 1;
    /// <summary>Items por página. Default: 50.</summary>
    public int PageSize { get; init; } = 50;
    /// <summary>Categoría para filtrar; null = sin filtro.</summary>
    public Guid? CategoryId { get; init; }
    /// <summary>Cota inferior inclusive; null = sin cota.</summary>
    public DateOnly? DateFrom { get; init; }
    /// <summary>Cota superior inclusive; null = sin cota.</summary>
    public DateOnly? DateTo { get; init; }
    /// <summary>Tags para filtrar. Ignorado por el repo hasta 4-007.</summary>
    public IReadOnlyList<Guid>? TagIds { get; init; }
}
```

- **GIVEN** el proyecto compila con `IncomeFilter.cs` presente.
- **WHEN** un caller construye `new IncomeFilter()` o
  `new IncomeFilter { Page = 2, CategoryId = guid }`.
- **THEN** las 6 props tienen los defaults correctos
  (`Page=1`, `PageSize=50`, `CategoryId=null`, `DateFrom=null`,
  `DateTo=null`, `TagIds=null`) salvo las sobreescritas; el record es
  inmutable (`init`).

### AC-2 — `IIncomeRepository` extendida

- [ ] `IIncomeRepository.cs` expone 6 métodos; se añade
      `Task<PagedResult<Income>> GetByFilterAsync(IncomeFilter filter, CancellationToken ct = default);`
      con XML doc (espejo de `IExpenseRepository` líneas 58-70).
- [ ] Los 5 métodos originales se mantienen con las firmas exactas.
- [ ] No se añade `using` nuevo (`IncomeFilter` en el mismo namespace;
      `PagedResult<T>` ya cubierto por `using ...Application.Common;`).

- **GIVEN** la interface tiene 5 métodos hoy (verificado).
- **WHEN** se aplica el añadido del 6º método.
- **THEN** la interface tiene 6 métodos y `IncomeRepository :
  IIncomeRepository` compila sin añadir `using`.

### AC-3 — `IncomeRepository` sealed class

- [ ] `src/.../Infrastructure/Persistence/Repositories/IncomeRepository.cs`
      existe como `public sealed class IncomeRepository : IIncomeRepository`.
- [ ] Constructor `public IncomeRepository(AppDbContext db)`; campo
      `private readonly AppDbContext _db;`.
- [ ] File-scoped namespace
      `ControlDeFinanzasIA.Infrastructure.Persistence.Repositories`;
      `using` al tope; XML doc en la clase y cada método público.

- **GIVEN** `IIncomeRepository` define 6 métodos (tras AC-2).
- **WHEN** se crea `IncomeRepository` implementando la interface.
- **THEN** los 6 métodos están implementados y la clase compila como
  `sealed`.

### AC-4 — `AddAsync` (validación FK condicional)

- [ ] Firma: `public async Task<Result> AddAsync(Income income, CancellationToken ct = default)`.
- [ ] Lógica: `ArgumentNullException.ThrowIfNull(income)`; si
      `income.CategoryId is { } catId` → `_db.Categories.FindAsync(new object?[] { catId }, ct)`;
      si `null` → `Result.Failure("CategoryNotFound", $"La categoría {catId} no existe.")`;
      si `CategoryId == null` → **skip** del chequeo; luego
      `_db.Incomes.Add(income)` + `await _db.SaveChangesAsync(ct)` +
      `return Result.Success(null!);`.

- **GIVEN** un `Income` con `CategoryId` válido (sembrado).
- **WHEN** `repo.AddAsync(income)`.
- **THEN** `Result.IsSuccess == true` y `GetByIdAsync` lo recupera con
  `Amount`/`Currency`/`Date`/`Source` preservados (round-trip de
  `OwnsOne` de `Money`).

- **GIVEN** un `Income` con `CategoryId = Guid.NewGuid()` (no existe).
- **WHEN** `repo.AddAsync(income)`.
- **THEN** `Result.IsSuccess == false`, `Error.Code == "CategoryNotFound"`.

- **GIVEN** un `Income` con `CategoryId = null`.
- **WHEN** `repo.AddAsync(income)`.
- **THEN** `Result.IsSuccess == true` y se persiste con `CategoryId`
  null (D-5; soportado por FK `SetNull`).

### AC-5 — `UpdateAsync`

- [ ] Firma: `public async Task<Result> UpdateAsync(Income income, CancellationToken ct = default)`.
- [ ] Lógica: `ArgumentNullException.ThrowIfNull(income)`;
      `Income? existing = await _db.Incomes.FindAsync(new object?[] { income.Id }, ct)`;
      si `null` → `Result.Failure("NotFound", $"Ingreso {income.Id} no encontrado.")`;
      `_db.Entry(existing).CurrentValues.SetValues(income)` +
      `await _db.SaveChangesAsync(ct)` + `return Result.Success(null!);`.
- [ ] No re-valida la FK (responsabilidad del caller).

- **GIVEN** un `Income` existente en el store.
- **WHEN** `repo.UpdateAsync(income)` con `Description`/`Amount`/`Source` nuevos.
- **THEN** `Result.IsSuccess == true` y `GetByIdAsync` refleja los
  cambios; `Id` y `CreatedAt` no cambian.

- **GIVEN** un `Income` cuyo `Id` no existe.
- **WHEN** `repo.UpdateAsync(income)`.
- **THEN** `Result.IsSuccess == false`, `Error.Code == "NotFound"`.

### AC-6 — `RemoveAsync` (hard delete)

- [ ] Firma: `public async Task<Result> RemoveAsync(Guid id, CancellationToken ct = default)`.
- [ ] Lógica: si `id == Guid.Empty` → `Result.Failure("NotFound", $"Ingreso {id} no encontrado.")`;
      `Income? existing = await _db.Incomes.FindAsync(new object?[] { id }, ct)`;
      si `null` → `Result.Failure("NotFound", $"Ingreso {id} no encontrado.")`;
      `_db.Incomes.Remove(existing)` + `await _db.SaveChangesAsync(ct)` +
      `return Result.Success(null!);`.

- **GIVEN** un `Income` existente.
- **WHEN** `repo.RemoveAsync(id)`.
- **THEN** `Result.IsSuccess == true` y `GetByIdAsync(id)` devuelve
  `null` (fila eliminada físicamente).

- **GIVEN** `id == Guid.Empty` o un `id` inexistente.
- **WHEN** `repo.RemoveAsync(id)`.
- **THEN** `Result.IsSuccess == false`, `Error.Code == "NotFound"`.

### AC-7 — `GetByIdAsync`

- [ ] Firma: `public async Task<Income?> GetByIdAsync(Guid id, CancellationToken ct = default)`.
- [ ] Lógica: si `id == Guid.Empty` → `return null;`; else
      `return await _db.Incomes.FindAsync(new object?[] { id }, ct);`.

- **GIVEN** un `id` existente.
- **WHEN** `repo.GetByIdAsync(id)`.
- **THEN** devuelve el `Income` con todas sus propiedades.

- **GIVEN** un `id` inexistente o `Guid.Empty`.
- **WHEN** `repo.GetByIdAsync(id)`.
- **THEN** devuelve `null` (no excepción).

### AC-8 — `GetAllAsync`

- [ ] Firma: `public async Task<IReadOnlyList<Income>> GetAllAsync(CancellationToken ct = default)`.
- [ ] Lógica: `await _db.Incomes.AsNoTracking().OrderByDescending(e => e.Date).ThenByDescending(e => e.CreatedAt).ToListAsync(ct);`.

- **GIVEN** 3 ingresos con `Date` distintas (y `CreatedAt` controlado).
- **WHEN** `repo.GetAllAsync()`.
- **THEN** devuelve los 3 ordenados por `Date` descendente (más
  recientes primero); `AsNoTracking` (solo lectura).

### AC-9 — `GetByFilterAsync`

- [ ] Firma: `public async Task<PagedResult<Income>> GetByFilterAsync(IncomeFilter filter, CancellationToken ct = default)`.
- [ ] Lógica: `ArgumentNullException.ThrowIfNull(filter)`;
      `IQueryable<Income> query = _db.Incomes.AsNoTracking();`;
      si `filter.CategoryId is not null` → `Where(e => e.CategoryId == filter.CategoryId)`;
      si `filter.DateFrom is not null` → `Where(e => e.Date >= filter.DateFrom)`;
      si `filter.DateTo is not null` → `Where(e => e.Date <= filter.DateTo)`;
      **`filter.TagIds` se IGNORA** (comentario explícito en el código);
      orden `Date desc, CreatedAt desc`; `int total = await query.CountAsync(ct)`;
      `int skip = (filter.Page - 1) * filter.PageSize`;
      `List<Income> items = await query.Skip(skip).Take(filter.PageSize).ToListAsync(ct)`;
      `return new PagedResult<Income>(items, total, filter.Page, filter.PageSize);`.

- **GIVEN** ingresos en 2 categorías y un rango de fechas mixto.
- **WHEN** `repo.GetByFilterAsync(filter)` con `CategoryId` y/o
  `DateFrom`/`DateTo` y/o `Page`/`PageSize`.
- **THEN** `PagedResult<Income>` con `Items` = página que cumple los
  filtros (excluyendo `TagIds`), `Total` = conteo que cumple los
  filtros, `Page`/`PageSize` = los solicitados, `TotalPages` calculado.

- **GIVEN** `filter.TagIds = [Guid.NewGuid()]` y 3 ingresos persistidos.
- **WHEN** `repo.GetByFilterAsync(filter)` (sin otros filtros).
- **THEN** los 3 ingresos se devuelven (`TagIds` ignorado; contrato
  diferido 4-007).

### AC-10 — Registro DI en `AddPersistence`

- [ ] `ServiceCollectionExtensions.AddPersistence` añade
      `services.AddScoped<IIncomeRepository, IncomeRepository>();`
      tras la línea existente `services.AddScoped<IExpenseRepository, ExpenseRepository>();`.
- [ ] El XML doc del método lista `IIncomeRepository` (summary + un
      `<para>` referenciando F-002 Fase 4).
- [ ] `using ControlDeFinanzasIA.Infrastructure.Persistence.Repositories;`
      ya presente (línea 2 del archivo) — cubre `IncomeRepository`.

- **GIVEN** `AddPersistence` registra 3 repos hoy.
- **WHEN** se aplica el añadido.
- **THEN** registra 4 repos; `IIncomeRepository` se resuelve a
  `IncomeRepository` (scoped) desde el contenedor.

### AC-11 — Tests de integración (≥12; 14 planeados)

- [ ] `tests/.../Integration/Persistence/IncomeRepositoryTests.cs`
      existe con 14 tests (T-1..T-14 de §6), pure xUnit `Assert.*`
      (sin FluentAssertions, sin mocking).
- [ ] Patrón verificado de fixture:
      `using var factory = new TestAppDbContextFactory();` →
      `using AppDbContext context = factory.CreateContext();` →
      `await context.Database.MigrateAsync();` →
      `var repo = new IncomeRepository(context);`.
- [ ] Helper `CreateIncome(Guid? categoryId, DateOnly date, decimal
      amount, string description, DateTime? createdAt = null, string?
      note = null, string? source = null)` — primer arg **`Guid?`**
      (nullable, diferencia vs `CreateExpense`), 7º arg `source`
      (espejo del 7º param de `Income.Create`).
- [ ] Categoría sembrada reutilizada:
      `_comidaCategoryId = new("11111111-1111-1111-1111-000000000001")`
      (mismo GUID que `ExpenseRepositoryTests`).
- [ ] `_fixedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)`
      para determinismo de `CreatedAt`.

> **Nota de discrepancia resuelta (verificación de código):** la
> instrucción de lanzamiento sugería
> `await using var ctx = await TestAppDbContextFactory.CreateAsync();`.
> La API real de `TestAppDbContextFactory` (F-006 Fase 2, 49 líneas) es
> `sealed class : IDisposable` con constructor + `CreateContext()`
> (sync) + `Dispose()` (sync); **no existe `CreateAsync()`** ni
> `IAsyncDisposable`. El patrón canónico es el que usa
> `ExpenseRepositoryTests.cs` (líneas 28-31), reflejado arriba. La spec
> usa el patrón **verificado**, no el shorthand de la instrucción.

- **GIVEN** los 14 tests implementados según §6.
- **WHEN** `dotnet test -c Debug --no-build` (filtro
  `FullyQualifiedName~IncomeRepositoryTests`).
- **THEN** los 14 pasan en verde.

### AC-12 — Gates de verificación

- [ ] `dotnet build -c Release --nologo` → 0 warnings, 0 errors.
- [ ] `dotnet build -c Debug --nologo` → 0 warnings, 0 errors.
- [ ] `dotnet test -c Debug --no-build --nologo` → suite completa
      verde: ~204 unit (sin nuevos unit — todos los tests de F-002 son
      integración) + ~59 integration (~45 baseline + ~14 nuevos) ≈ 263.
- [ ] `dotnet test -c Debug --no-build --nologo --filter "FullyQualifiedName~ArchitectureTests"`
      → 11/11 (R-1..R-5).
- [ ] `dotnet format style --verify-no-changes` → exit 0.
- [ ] `dotnet list src/ControlDeFinanzasIA.Domain package` sigue vacío (R-1).

> Las cuentas de tests son **aproximadas** (línea base según contrato de
> lanzamiento); el gate real es "0 failures + suite previa sigue verde
> + ~14 nuevos integration verdes". El implementador (sdd-apply) debe
> reportar las cuentas exactas observadas.

### AC-13 — Sin scope creep

- [ ] NO use cases (4-003), ni update/delete use cases (4-005), ni UI
      (4-008), ni lógica de `TagIds` (4-007).
- [ ] NO modificaciones a `IExpenseRepository`/`ExpenseRepository`/
      `ExpenseFilter`/`Expense.cs`/`ExpenseConfiguration.cs`.
- [ ] NO modificaciones a `Income.cs`/`IncomeConfiguration.cs`/
      `AppDbContext.cs`/`Money.cs`/`Currency.cs`/`Result.cs`/
      `PagedResult.cs`.
- [ ] NO nuevas migraciones EF Core (F-001 ya creó `AddIncomesTable`).
- [ ] NO FluentAssertions, NO mocking library, NO paquetes NuGet nuevos.

- **GIVEN** el diff de F-002.
- **WHEN** se inspecciona contra la lista de archivos in-scope (§5).
- **THEN** no hay archivos fuera de la lista modificados; no hay
  referencias a 4-003/4-005/4-007/4-008 en el código de producción.

---

## 5. Áreas afectadas

| Archivo | Acción | Descripción |
| --- | --- | --- |
| `src/ControlDeFinanzasIA.Application/Abstractions/IncomeFilter.cs` | **New** | `sealed record` transporte de filtro/paginación (6 props, `TagIds` placeholder ignorado hasta 4-007). Forma `init` + XML doc por propiedad (D-3). |
| `src/ControlDeFinanzasIA.Application/Abstractions/IIncomeRepository.cs` | **Modified** | Añadir 6º método `GetByFilterAsync(IncomeFilter, ct) → Task<PagedResult<Income>>` + XML doc (D-1). Los 5 métodos originales intactos. Sin `using` nuevo. |
| `src/ControlDeFinanzasIA.Infrastructure/Persistence/Repositories/IncomeRepository.cs` | **New** | `sealed class : IIncomeRepository`, ctor `IncomeRepository(AppDbContext db)`, 6 métodos. Espejo de `ExpenseRepository`; `AddAsync` con validación FK condicional (D-5). |
| `src/ControlDeFinanzasIA.Infrastructure/Persistence/ServiceCollectionExtensions.cs` | **Modified** | `services.AddScoped<IIncomeRepository, IncomeRepository>();` + XML doc actualizado (AC-10). |
| `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/IncomeRepositoryTests.cs` | **New** | 14 tests CRUD + filtros + paginación + `TagIds`-ignorado, pure xUnit `Assert.*` (§6). |

**Archivos NO modificados (verificados presentes y correctos):**
`Income.cs`, `IncomeConfiguration.cs`, `AppDbContext.cs` (`DbSet<Income>
Incomes` línea 60 ya existe), `Money.cs`, `Currency.cs`, `Result.cs`,
`PagedResult.cs`, `Expense.cs`, `ExpenseConfiguration.cs`,
`ExpenseRepository.cs`, `IExpenseRepository.cs`, `ExpenseFilter.cs`,
`ExpenseRepositoryTests.cs`, `TestAppDbContextFactory.cs`,
`IncomeSchemaTests.cs`, cualquier migración, cualquier archivo de UI.

**Modificaciones a `.csproj`:** ninguna (sin paquetes nuevos, sin
referencias de proyecto nuevas).

---

## 6. Plan de tests de integración

Ubicación:
`tests/ControlDeFinanzasIA.Tests.Integration/Persistence/IncomeRepositoryTests.cs`.

Espejo estructural de `ExpenseRepositoryTests.cs` (314 líneas), con las
adaptaciones de `Income` (helper `CreateIncome` con `categoryId` nullable
+ 7º arg `source`; `income.Update(...)` con `source`). Patrón de fixture
verificado (no el shorthand incorrecto de la instrucción):

```csharp
using var factory = new TestAppDbContextFactory();
using AppDbContext context = factory.CreateContext();
await context.Database.MigrateAsync();
var repo = new IncomeRepository(context);
```

Helper de fábrica (diferencias vs `CreateExpense` marcadas):

```csharp
private static readonly DateTime _fixedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
private static readonly Guid _comidaCategoryId = new("11111111-1111-1111-1111-000000000001");

private static Income CreateIncome(
    Guid? categoryId,                 // nullable (vs Guid en CreateExpense)
    DateOnly date,
    decimal amount,
    string description,
    DateTime? createdAt = null,
    string? note = null,
    string? source = null)            // 7º arg (espejo de Income.Create)
    => Income.Create(
        categoryId,
        date,
        Money.Create(amount, Currency.DOP),
        description,
        note,
        source,
        createdAt ?? _fixedTimestamp);
```

| # | Test | GIVEN / WHEN / THEN |
| --- | --- | --- |
| T-1 | `AddAsync_WithValidIncome_Persists` | **GIVEN** un `Income` con `CategoryId` sembrado (`_comidaCategoryId`), `Amount` 1500.50m, `Source="Transferencia"`. **WHEN** `repo.AddAsync(income)`. **THEN** `Result.IsSuccess` true; `GetByIdAsync` recupera con `Amount.Amount`==1500.50m, `Currency`==DOP, `Date`, `Description`, `Source` preservados (valida `OwnsOne` round-trip). |
| T-2 | `AddAsync_WithUnknownCategoryId_ReturnsCategoryNotFound` | **GIVEN** un `Income` con `CategoryId = Guid.NewGuid()`. **WHEN** `repo.AddAsync(income)`. **THEN** `Result.IsSuccess` false; `Error.Code == "CategoryNotFound"`; `Error.Message` no vacío. |
| T-3 | `AddAsync_WithNullCategoryId_Persists` | **GIVEN** un `Income` con `CategoryId = null`. **WHEN** `repo.AddAsync(income)`. **THEN** `Result.IsSuccess` true; `GetByIdAsync` recupera con `CategoryId == null` (D-5; soportado por FK `SetNull`). |
| T-4 | `GetByIdAsync_Existing_ReturnsIncome` | **GIVEN** un `Income` persistido vía `AddAsync`. **WHEN** `repo.GetByIdAsync(income.Id)`. **THEN** devuelve la entidad con `Id` y `Description` correctos. |
| T-5 | `GetByIdAsync_Missing_ReturnsNull` | **GIVEN** ningún ingreso con ese `Id`. **WHEN** `repo.GetByIdAsync(Guid.NewGuid())`. **THEN** devuelve `null`. |
| T-6 | `GetByIdAsync_WithEmptyGuid_ReturnsNull` | **GIVEN** `id == Guid.Empty`. **WHEN** `repo.GetByIdAsync(Guid.Empty)`. **THEN** devuelve `null` (guard, sin tocar la DB). |
| T-7 | `GetAllAsync_ReturnsAllOrderedByDateDesc` | **GIVEN** 3 ingresos con `Date` 2026-01-01, 2026-01-03, 2026-01-02. **WHEN** `repo.GetAllAsync()`. **THEN** `Count == 3` y el orden de `Date` es `[2026-01-03, 2026-01-02, 2026-01-01]` (desc). |
| T-8 | `GetByFilterAsync_NoFilter_ReturnsFirstPageOrdered` | **GIVEN** 3 ingresos (fechas 01-01, 01-03, 01-02). **WHEN** `repo.GetByFilterAsync(new IncomeFilter { Page = 1, PageSize = 50 })`. **THEN** `Total == 3`, `Page == 1`, `PageSize == 50`, `TotalPages == 1`, `Items.Count == 3`, orden `Date` desc. |
| T-9 | `GetByFilterAsync_WithCategoryId_FiltersCorrectly` | **GIVEN** 2 ingresos en `_comidaCategoryId` y 1 en otra categoría sembrada (p. ej. `11111111-1111-1111-1111-000000000002`). **WHEN** `repo.GetByFilterAsync(new IncomeFilter { CategoryId = _comidaCategoryId })`. **THEN** `Total == 2` y todos los `Items` tienen ese `CategoryId`. |
| T-10 | `GetByFilterAsync_WithDateRange_FiltersCorrectly` | **GIVEN** ingresos en 2026-01-01, 2026-01-15, 2026-01-31. **WHEN** `repo.GetByFilterAsync(new IncomeFilter { DateFrom = 2026-01-10, DateTo = 2026-01-20 })`. **THEN** `Total == 1`, `Items.Single().Date == 2026-01-15`. |
| T-11 | `GetByFilterAsync_Paginates_CorrectTotalAndItems` | **GIVEN** 25 ingresos persistidos. **WHEN** `repo.GetByFilterAsync(new IncomeFilter { Page = 2, PageSize = 10 })`. **THEN** `Total == 25`, `Items.Count == 10`, `TotalPages == 3`, `Page == 2`. (Página 3 → 5 items; página 1 → 10 items.) |
| T-12 | `GetByFilterAsync_IgnoresTagIds_ForNow` | **GIVEN** 3 ingresos persistidos; `filter = new IncomeFilter { TagIds = new[] { Guid.NewGuid() } }` (sin otros filtros). **WHEN** `repo.GetByFilterAsync(filter)`. **THEN** `Total == 3` y `Items.Count == 3` (`TagIds` ignorado; contrato diferido 4-007). |
| T-13 | `UpdateAsync_Existing_UpdatesFields` | **GIVEN** un `Income` persistido (`Amount` 1000m, `Description="Original"`, `Source=null`). **WHEN** `income.Update(_comidaCategoryId, 2026-01-11, Money.Create(2000m, DOP), "Actualizado", "Nota", "Sueldo", _fixedTimestamp.AddDays(1))` y `repo.UpdateAsync(income)`. **THEN** `Result.IsSuccess` true; `GetByIdAsync` refleja `Amount.Amount`==2000m, `Description=="Actualizado"`, `Source=="Sueldo"`, `Note=="Nota"`; `CreatedAt` inmutable; `UpdatedAt == _fixedTimestamp.AddDays(1)`. |
| T-14 | `RemoveAsync_Existing_DeletesIncome` | **GIVEN** un `Income` persistido. **WHEN** `repo.RemoveAsync(income.Id)`. **THEN** `Result.IsSuccess` true; `GetByIdAsync(income.Id)` devuelve `null` (hard delete). |

**Marco y paquetes:** xUnit (ya en `Tests.Integration.csproj` desde
Fase 2). `Microsoft.Data.Sqlite` ya presente. **Sin paquetes nuevos**;
**sin FluentAssertions**; **sin mocking**. Aserciones con `Assert.*`
(`Assert.True`, `Assert.False`, `Assert.Equal`, `Assert.Null`,
`Assert.NotNull`, `Assert.Single`, `Assert.Contains`).

**Cobertura:** happy paths (T-1, T-4, T-7, T-8, T-13, T-14), edge cases
(T-3 null category, T-6 empty guid, T-11 paginación, T-12 TagIds
ignorado), error states (T-2 CategoryNotFound, T-5 missing→null).

---

## 7. Fuera de alcance

F-002 **no** hace lo siguiente (cada ítem vive en otra feature):

- **`RegisterIncomeUseCase`** + `RegisterIncomeCommand` +
  `RegisterIncomeValidator` + `RegisterIncomeResult` (4-003).
- **`UpdateIncomeUseCase`** + **`DeleteTransactionUseCase`** (4-005).
- **UI `IncomeView`** + `GetIncomesQuery` + DTOs (4-008).
- **Lógica de `TagIds`** en `GetByFilterAsync` (4-007). La propiedad
  existe en `IncomeFilter` (D-2) pero el repo la ignora.
- **Validación de `IncomeFilter`** (`Page ≥ 1`, `PageSize ≥ 1`,
  `DateFrom ≤ DateTo`) — responsabilidad del caller (D-8).
- **Soft-delete** / `IsDeleted` / query filters (D-7).
- **Multi-moneda** / filtro por `Currency`.
- **Modificaciones a `Expense*`** (`Expense.cs`, `ExpenseConfiguration.cs`,
  `ExpenseRepository.cs`, `IExpenseRepository.cs`, `ExpenseFilter.cs`).
- **Modificaciones a `Income.cs` / `IncomeConfiguration.cs` /
  `AppDbContext.cs`** (F-001 los provee; `DbSet<Income>` ya existe).
- **Nuevas migraciones** EF Core (F-001 creó `AddIncomesTable`).
- **Paquetes NuGet nuevos** (en particular, no FluentAssertions).
- **Modificaciones a `Bootstrap.ConfigureServices`** (F-002 sólo
  extiende `AddPersistence`).
- **CI workflow**, **`Directory.Packages.props`**,
  **`Directory.Build.props`**: sin cambios.

---

## 8. Plan de rollback

Cambio **aditivo** en Application/Infrastructure (sin tocar Domain,
migraciones ni UI). Revertir en orden:

1. **Eliminar** `IncomeFilter.cs`, `IncomeRepository.cs`,
   `IncomeRepositoryTests.cs` (los 3 archivos nuevos).
2. **Revertir** `IIncomeRepository.cs` a 5 métodos (quitar
   `GetByFilterAsync` + su XML doc).
3. **Revertir** `ServiceCollectionExtensions.cs` (quitar la línea
   `AddScoped<IIncomeRepository, IncomeRepository>()` y restaurar el
   XML doc previo).
4. **Verificar** `dotnet build` limpio y `dotnet test` suite previa en
   verde. No hay migración EF ni datos de usuario en juego (la tabla
   `Incomes` la creó F-001 y se conserva). NetArchTest R-1..R-5 no se
   afectan: `IncomeRepository` está en Infrastructure (depende de
   Application+Domain, permitido por R-3); `IncomeFilter`/
   `IIncomeRepository` en Application (sólo abstracciones + Domain,
   permitido por R-2).

---

## 9. Riesgos y mitigaciones

| # | Riesgo | Prob. | Impacto | Mitigación |
| --- | --- | --- | --- | --- |
| R-1 | `IIncomeRepository` tenía 5 métodos, no 6 — la premisa "DO NOT MODIFY" era falsa (verificado). | Alta (verificado) | Medio | **Resuelto en D-1**: modificar la interface para añadir `GetByFilterAsync` (espejo de `IExpenseRepository` + phase plan RF-3). La modificación es in-scope para F-002. |
| R-2 | `AddAsync` con `CategoryId == null` rompe simetría con `Expense` y se olvida el skip del chequeo FK. | Media | Medio | D-5 + test T-3 (`AddAsync_WithNullCategoryId_Persists`) blindan la rama `null`. Guard `if (income.CategoryId is { } catId)`. |
| R-3 | `CurrentValues.SetValues` no copia `Money` embebida (`OwnsOne`) en `UpdateAsync`. | Media | Alto | Test T-13 verifica que `Amount.Amount`/`Amount.Currency` persisten tras `UpdateAsync`. Mismo riesgo que `ExpenseRepository` (ya probado verde en F-003 T-9); `IncomeSchemaTests.ST-3` ya valida round-trip de `OwnsOne` para `Income`. Si fallara, re-mapear `OwnsOne` explícitamente. |
| R-4 | Engram `mem_save` no disponible en el toolset del sub-agente (verificado: `list_mcp_resources` → vacío; sin funciones `mem_*`). | Alta (verificado) | Bajo | Respaldo en filesystem (`.atl/sdd/f-002p4-income-repository/proposal.md` ya existe; spec en `spec/features/phase-04/F-002-income-repository.spec.md`). El orquestador persiste a Engram con los topic keys `sdd/f-002p4-income-repository/{proposal,spec}` (precedente `f-006p3` + proposal Risk #4). El sub-agente NO fabrica observation IDs. |
| R-5 | El implementador usa el shorthand incorrecto `await TestAppDbContextFactory.CreateAsync()` (no existe). | Media | Bajo | §6 + AC-11 documentan el patrón verificado (`new TestAppDbContextFactory()` + `CreateContext()` + `MigrateAsync()`), espejo de `ExpenseRepositoryTests`. |
| R-6 | El implementador implementa `IncomeFilter` como record posicional y pierde el XML doc por propiedad. | Media | Bajo | D-3 + AC-1 fijan la forma `init` con XML doc por propiedad (espejo de `ExpenseFilter`). |
| R-7 | FK `SetNull` de `Income` (vs `Restrict` de `Expense`) sorprende al implementador y añade validación defensiva incorrecta. | Baja | Medio | D-5 documenta la diferencia semántica; `IncomeConfiguration.cs` línea 40 es la fuente. El repo NO debe validar `Guid.Empty` para `CategoryId` (`Income.Create` ya lo rechaza). |
| R-8 | `Skip` negativo (`Page = 0`) lanza en EF Core. | Baja | Bajo | D-8: el repo no valida; el caller valida `Page ≥ 1`. No añadir validación defensiva en el repo. |

---

## 10. Referencias

- **Propuesta (filesystem):**
  `.atl/sdd/f-002p4-income-repository/proposal.md` (146 líneas) — PRD
  de origen. Notar que su draft "5 props sin `TagIds`" fue sobreescrito
  por D-2 (phase plan RF-4); su "Discrepancia crítica" fue resuelta por
  D-1.
- **Propuesta (Engram objetivo):** topic key
  `sdd/f-002p4-income-repository/proposal` (pendiente de persistencia
  por el orquestador — ver R-4).
- **Spec (Engram objetivo):** topic key
  `sdd/f-002p4-income-repository/spec` (pendiente de persistencia por
  el orquestador — ver R-4).
- **F-001 Fase 4 (archivada):**
  `spec/features/phase-04/F-001-income-entity-config-migration.done.md`
  — entidad `Income` (invariantes, `CategoryId` nullable, `Source`,
  `CreatedAt`/`UpdatedAt`), `IncomeConfiguration` (FK `SetNull`,
  `OwnsOne` de `Money` `HasPrecision(18,4)`), migración
  `AddIncomesTable`, `DbSet<Income> Incomes`.
- **F-003 Fase 3 (archivada — patrón espejo):**
  `spec/features/phase-03/F-003-expense-repository.done.md` (1363
  líneas) — `ExpenseRepository` / `ExpenseFilter` /
  `ExpenseRepositoryTests` que F-002 replica con las adaptaciones de
  `Income`.
- **Phase plan:** `spec/phases/phase-04/features.md` §RF-3 (línea 142,
  6 métodos incl. `GetByFilterAsync`), §RF-4 (línea 150, `IncomeFilter`
  con `TagIds`), §RF-5 (línea 154, `IncomeRepository` implementation).
- **Convención de contrato:** `spec/features/phase-01/F-006-contrato-interfaces-repo.done.md`
  (commands `Task<Result>` vs queries `Task<T>`).
- **Tipos comunes:** `spec/features/phase-01/F-005-comuntypes-result-pagedresult.done.md`
  (`Result` non-generic + `PagedResult<T>` 4-arg, `TotalPages` calculado).
- **Test infra:** `spec/features/phase-02/F-006-integration-tests.done.md`
  (`TestAppDbContextFactory`, SQLite in-memory compartida).
- **Patrón repo:** `spec/features/phase-02/F-004-category-paymentmethod-repositories.done.md`
  (`sealed`, `_db`, `FindAsync`, `CurrentValues.SetValues`,
  `AsNoTracking`, códigos en inglés + mensajes en español,
  `Result.Success(null!)`).
- **Arquitectura:** `docs/architecture.md` §4 (R-1..R-5), §7
  (`Result<T>`/`PagedResult<T>`), §8 (repositorios en Infrastructure),
  §12 (single-user single-process — justifica no soft-delete).
- **Constitución:** `spec/constitution/mission.md`,
  `spec/constitution/tech-stack.md` (async/await extremo a extremo,
  errores como valores, MVVM estricto).
- **Código verificado (fuentes de la spec):**
  `src/.../Abstractions/IExpenseRepository.cs` (71 líneas — espejo de
  la interface extendida),
  `src/.../Abstractions/ExpenseFilter.cs` (42 líneas — espejo del
  record `init`),
  `src/.../Repositories/ExpenseRepository.cs` (212 líneas — espejo de
  la implementación),
  `src/.../Persistence/ServiceCollectionExtensions.cs` (67 líneas —
  registro DI),
  `src/.../Common/PagedResult.cs` (39 líneas — constructor 4-arg),
  `src/.../Abstractions/IIncomeRepository.cs` (57 líneas — estado
  actual: 5 métodos, confirma D-1),
  `src/.../Domain/Transactions/Income.cs` (268 líneas — entidad,
  `Create` 7 params, `CategoryId` nullable, invariante `Guid.Empty`),
  `src/.../Configurations/IncomeConfiguration.cs` (57 líneas — FK
  `SetNull` línea 40),
  `src/.../Persistence/AppDbContext.cs` (72 líneas — `DbSet<Income>
  Incomes` línea 60),
  `tests/.../Persistence/ExpenseRepositoryTests.cs` (314 líneas —
  espejo de tests),
  `tests/.../Persistence/TestAppDbContextFactory.cs` (49 líneas — API
  real: `CreateContext()`, no `CreateAsync()`),
  `tests/.../Persistence/IncomeSchemaTests.cs` (151 líneas — schema
  ya validado por F-001).
