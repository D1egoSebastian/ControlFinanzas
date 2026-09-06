# Specification — F-005: Application: DTOs + `GetTransactionsQuery` (paginado, read side)

> **Fase:** 3 — Slice vertical: registrar y listar gastos
> **Feature:** F-005 (local a esta fase) · **Estado:** Pendiente · **Spec ID:** SPEC-F005P3
>
> **Nota de numeración:** este `F-005` es el de la **Fase 3**
> (`spec/phases/phase-03/features.md`). No debe confundirse con el
> `F-005` de Fase 1 (`Result`/`PagedResult<T>`). Los IDs son locales
> a cada fase; el folder los desambigua.
>
> Fuentes de verdad: `docs/vision.md` (§4.1, §4.3), `docs/architecture.md`
> (§7), `spec/phases/phase-03/features.md` §F-005,
> `spec/features/phase-03/F-001-expense-domain-entity.done.md`
> (INV-1..INV-8),
> `spec/features/phase-03/F-003-expense-repository.done.md`
> (`GetByFilterAsync → Task<PagedResult<Expense>>` pelado, DP-3;
> record `ExpenseFilter`),
> `spec/features/phase-03/F-004-register-expense-use-case.done.md`
> (`ExpenseDto` + `ExpenseExtensions.ToDto` **reutilizados**;
> `Microsoft.Extensions.Logging` 8.0.1 ya en CPM; ~194 tests
> baseline),
> `spec/features/phase-01/F-005-comuntypes-result-pagedresult.done.md`
> (`Result` non-generic con `Value: object?`; `PagedResult<T>`
> constructor 4-arg, **sin** factory `Empty`; **no se modifica**).

---

## 1. Resumen

F-005 introduce el **primer query de lectura del proyecto**
(`GetTransactionsQuery`), el patrón read-side que **todos** los
queries próximos replicarán: `Request` inmutable con defaults,
**validación manual** (sin FluentValidation — divergencia
justificada de F-004 por la trivialidad de 2 reglas), construcción
de `ExpenseFilter` (reutilizado de F-003, filtros en `null` para
esta fase), invocación a `IExpenseRepository.GetByFilterAsync`
(que devuelve `PagedResult<Expense>` pelado, **no** `Result`),
mapeo `Expense → ExpenseDto` reutilizando
`ExpenseExtensions.ToDto` de F-004 (sin duplicar), captura
defensiva de excepciones del repo con `ILogger.LogError` + rethrow
de `OperationCanceledException`, y retorno `Task<Result>` (eco
non-generic de Phase 1 F-005) envolviendo `PagedResult<ExpenseDto>`
en éxito. Cierra el lado de lectura del slice vertical de Phase 3
y deja el contrato Application → UI listo para que F-007
(`ExpensesViewModel`) sólo bindee la página de DTOs.

Al final de la feature:

- 2 tipos nuevos en `Application/UseCases/Expenses/GetTransactions/`:
  `GetTransactionsQueryRequest`, `GetTransactionsQuery`.
- `Bootstrap.ConfigureServices` registra el query como `Transient`.
- `FakeExpenseRepository` extendido con `SetFilterResult(PagedResult<Expense>)`
  (hoy `GetByFilterAsync` está hardcoded a vacío).
- `GetTransactionsQueryTests.cs` con ≥ 4 tests pasa con **pure xUnit
  `Assert.*`** (NO FluentAssertions, NO librería de mocking).
- `dotnet build -c Release`: 0 warnings, 0 errors.
- `dotnet test`: ~194 (baseline F-004) + ~5 ≈ **199 verdes**.
- NetArchTest R-1 a R-5 siguen pasando (no introduce referencias
  prohibidas).

## 2. Objetivo

1. **Introducir 2 tipos nuevos** en `Application` sin tocar `Domain`,
   `Infrastructure` (salvo DI), `Result`, `PagedResult<T>`,
   `IExpenseRepository`, `ExpenseFilter`, `ExpenseDto`,
   `ExpenseExtensions`, `IClock`.
2. **Establecer el patrón read-side**: request inmutable con
   defaults; validación manual de `Page`/`PageSize` (sin
   FluentValidation — DP-2); `ExpenseFilter` con filtros en `null`
   (Phase 4 los enchufa sin tocar el query); invocación a
   `GetByFilterAsync` (`PagedResult<Expense>` pelado, DP-3 de F-003);
   mapeo vía `ExpenseExtensions.ToDto` (F-004, reutilizado); captura
   de excepciones con `ILogger.LogError` + rethrow de
   `OperationCanceledException`.
3. **Reutilizar el contrato Application → UI de F-004**: `ExpenseDto`
   y `ExpenseExtensions.ToDto` son los mismos tipos; F-005 no
   duplica el mapper ni toca el DTO.
4. **Cubrir con ≥ 4 tests** el query usando pure xUnit `Assert.*` y
   el `FakeExpenseRepository` extendido con `SetFilterResult`.

## 3. Descripción breve

| Tipo | Archivo | Responsabilidad |
| --- | --- | --- |
| `GetTransactionsQueryRequest` (sealed record) | `Application/UseCases/Expenses/GetTransactions/GetTransactionsQueryRequest.cs` | `int Page = 1, int PageSize = 50`. Sin filtros (CategoryId/DateFrom/DateTo llegan en Phase 4). Request inmutable Application ← UI. |
| `GetTransactionsQuery` (sealed class) | `Application/UseCases/Expenses/GetTransactions/GetTransactionsQuery.cs` | Inyecta `IExpenseRepository` + `ILogger<GetTransactionsQuery>`. `ExecuteAsync(request, ct) → Task<Result>` con `Value = PagedResult<ExpenseDto>` en éxito. Validación manual + catch de excepciones del repo. |
| `Bootstrap.ConfigureServices` | `UI/Composition/Bootstrap.cs` | 1 registro nuevo: `AddTransient<GetTransactionsQuery>()`. No se toca `AddPersistence`. |
| `FakeExpenseRepository.SetFilterResult` | `Tests.Unit/TestDoubles/FakeExpenseRepository.cs` | `SetFilterResult(PagedResult<Expense>)` para configurar `GetByFilterAsync`. Default sigue siendo vacío (no rompe tests de F-004). |
| `GetTransactionsQueryTests.cs` | `Tests.Unit/Application/Expenses/` | ≥ 4 tests + recomendados adicionales. |

## 4. Dependencias

| Dependencia | Estado | Uso en F-005 |
| --- | --- | --- |
| F-001 de Fase 3 (archivada) | ✅ | Entidad `Expense` (INV-1..INV-8). Mapea vía `ExpenseExtensions.ToDto` (lee `e.Amount.Amount`, `e.Amount.Currency`, etc.). **No se modifica.** |
| F-003 de Fase 3 (archivada) | ✅ | `IExpenseRepository.GetByFilterAsync(ExpenseFilter, ct) → Task<PagedResult<Expense>>` — `PagedResult<Expense>` **pelado**, no `Result` (DP-3 de F-003 honrado). Record `ExpenseFilter` con `Page`, `PageSize`, `CategoryId?`, `DateFrom?`, `DateTo?` (init-only, sin auto-validación). **No se modifica.** |
| F-004 de Fase 3 (archivada) | ✅ | `ExpenseDto` (9 campos) + `ExpenseExtensions.ToDto(this Expense)` **reutilizados, no duplicados**. Patrón `Bootstrap` con `AddTransient`. `Microsoft.Extensions.Logging` 8.0.1 ya referenciado. ~194 tests baseline. |
| F-005 de Fase 1 (archivada) | ✅ | `Result` non-generic (`Value: object?`); `Success(object)` / `Failure(code, message)`. `PagedResult<T>` constructor 4-arg `(IReadOnlyList<T>, int Total, int Page, int PageSize)` con `TotalPages` calculado — **sin** factory `Empty`. **No se modifica.** Caller hace `(PagedResult<ExpenseDto>)result.Value!` tras `IsSuccess`. |
| `Microsoft.Extensions.Logging.Abstractions` 8.0.1 | ✅ pre-wired | Ya en CPM desde F-004. Provee `NullLogger<T>` para tests. **No se añade el paquete.** |

**Bloqueos:** Ninguno.

## 5. Fuera de alcance

- **UI / ViewModel** (`ExpensesViewModel`, `ExpensesView`, navegación,
  DataTemplates) — F-006, F-007.
- **Filtros `CategoryId` / `DateFrom` / `DateTo`** expuestos al
  usuario — Phase 4 los añade al `Request`; la firma `ExpenseFilter`
  ya los acepta en `null`.
- **Modificar** `Result`, `PagedResult<T>`, `IExpenseRepository`,
  `ExpenseFilter`, `ExpenseDto`, `ExpenseExtensions`, `IClock`,
  `Expense.cs`, `AppDbContext`, `ExpenseConfiguration`,
  `ExpenseRepository` o generar migraciones. F-005 sólo añade tipos
  a `Application`, 1 registro a `Bootstrap` y 1 método al fake.
- **FluentValidation** para el `Request` (DP-2 — validación manual,
  divergencia explícita y justificada de F-004 por la trivialidad
  de 2 reglas).
- **FluentAssertions** o **librería de mocking** (Moq/NSubstitute).
  Tests con pure xUnit `Assert.*` y fakes hand-rolled (cache
  `sdd/controldefinanzasia/testing-capabilities`).
- **Factory `static PagedResult<T>.Empty(int, int)`** — fuera de
  scope; se usa el constructor 4-arg. Mejora post-Phase 3
  (follow-up), **no** se introduce en F-005 (mantener diff mínimo).
- Paginación de los otros 14 repositorios, queries avanzados
  (FTS5, texto libre), soft-delete, multi-moneda.

## 6. Decisiones de diseño (fijadas por la propuesta — no re-abrir)

| # | Decisión | Rationale |
| --- | --- | --- |
| DP-1 | El query retorna `Task<Result>` **non-generic** con `Value = PagedResult<ExpenseDto>` en éxito. El caller (F-007) hace `(PagedResult<ExpenseDto>)result.Value!` tras `IsSuccess`. | Honra F-003 DP-3 (`GetByFilterAsync` devuelve `PagedResult<Expense>` pelado, no `Result`) y el contrato de `Result` non-generic de Phase 1 F-005. Eco de F-004 (`Task<Result>`). |
| DP-2 | **Validación manual** en el query, **sin** FluentValidation: `Page < 1` → `"InvalidFilter"`; `PageSize <= 0` → `"InvalidFilter"`; `PageSize > 200` → `"InvalidFilter"` (mensajes es-DO en §8 AC-3). | Sólo 2 reglas triviales sobre enteros. FluentValidation (patrón de F-004 para comandos con 6 reglas + trim + enum) es desproporcionado: requeriría `AbstractValidator`, registro DI y tests del validador. Divergencia **documentada** (no se interprete como omisión). |
| DP-3 | El query **catch** `Exception ex` `when (ex is not OperationCanceledException)`: `_logger.LogError(ex, "...")` (preserva stack) → `Result.Failure("RepositoryError", "Ocurrió un error al obtener los gastos.")`. `OperationCanceledException` se **rethrow**. | `GetByFilterAsync` no devuelve `Result` para lecturas — el único canal de fallo es excepciones. El filtro `when (ex is not OperationCanceledException)` respeta la cancelación cooperativa (CT propagado a `CountAsync`/`ToListAsync`). El log preserva el stack; el `Result.Failure` traduce a error de negocio claro. |

> **DP-4** (page-beyond-end / empty repo): el query usa el
> constructor 4-arg de `PagedResult<T>` — **no** introduce factory
> `Empty` (ver AC-7, AC-8, §5).

## 7. Cambios al alcance

**Se añade** (sólo `Application` y `Tests.Unit`): 2 archivos en
`Application/UseCases/Expenses/GetTransactions/`, 1 archivo de
tests en `Tests.Unit/Application/Expenses/`, 1 método
(`SetFilterResult`) en `Tests.Unit/TestDoubles/FakeExpenseRepository.cs`.

**Se modifica** (sólo `Bootstrap.ConfigureServices` en `UI`): 1
línea `AddTransient<GetTransactionsQuery>()`. **No** se toca
`AddPersistence` (F-003 ya registró `IExpenseRepository`).

**NO se modifica:** `Expense.cs`/`Money`/`Currency` (Domain);
`ExpenseConfiguration`/`AppDbContext`/`ExpenseRepository`/migraciones
(Infrastructure); `Result`/`PagedResult<T>`/`IExpenseRepository`/
`ExpenseFilter`/`IClock`/`IPathResolver` (Application abstracciones);
`ExpenseDto`/`ExpenseExtensions` (F-004 — **reutilizados, no
duplicados**); `Directory.Packages.props`/`Application.csproj`
(`Microsoft.Extensions.Logging` ya referenciado desde F-004);
NetArchTest R-1..R-5.

## 8. Acceptance Criteria

La feature se considera completada cuando **todos** los siguientes
ACs se cumplen, verificables mediante tests automatizados y revisión
de código.

### AC-1 — `GetTransactionsQueryRequest`
- [ ] `src/ControlDeFinanzasIA.Application/UseCases/Expenses/GetTransactions/GetTransactionsQueryRequest.cs`
      existe como `public sealed record GetTransactionsQueryRequest(int Page = 1, int PageSize = 50)`.
- [ ] File-scoped namespace, XML doc, `sealed`. **Sin** filtros
      (`CategoryId`, `DateFrom`, `DateTo`) — Phase 4 los añade.

### AC-2 — `GetTransactionsQuery`
- [ ] `public sealed class GetTransactionsQuery` con constructor que
      inyecta `IExpenseRepository` y `ILogger<GetTransactionsQuery>`.
- [ ] `public async Task<Result> ExecuteAsync(GetTransactionsQueryRequest request, CancellationToken ct = default)`.
- [ ] Retorna `Task<Result>` non-generic (DP-1); `Value =
      PagedResult<ExpenseDto>` en éxito, `null` en fallo.

### AC-3 — Validación manual (sin FluentValidation)
- [ ] **Sin** `AbstractValidator<GetTransactionsQueryRequest>` (DP-2).
- [ ] `request.Page < 1` → `Result.Failure("InvalidFilter", "Page debe ser mayor o igual a 1.")`.
- [ ] `request.PageSize <= 0` → `Result.Failure("InvalidFilter", "PageSize debe ser mayor a 0.")`.
- [ ] `request.PageSize > 200` → `Result.Failure("InvalidFilter", "PageSize debe ser menor o igual a 200.")`.
- [ ] Validación ocurre **antes** de invocar al repo (verificable
      por tests que no llegan a llamar `GetByFilterAsync`).

### AC-4 — Construye `ExpenseFilter` con filtros en `null`
- [ ] En request válido, construye `new ExpenseFilter { Page =
      request.Page, PageSize = request.PageSize, CategoryId = null,
      DateFrom = null, DateTo = null }`.
- [ ] Phase 4 podrá añadir filtros a `GetTransactionsQueryRequest`
      y propagarlos al `ExpenseFilter` sin tocar la firma del query
      ni del repo.

### AC-5 — Invoca `repo.GetByFilterAsync` y mapea con `ToDto` (reutilizado)
- [ ] Llama `await repo.GetByFilterAsync(filter, ct)` y recibe
      `PagedResult<Expense>` (no `Result` — DP-3 de F-003).
- [ ] Mapea cada `Expense` → `ExpenseDto` vía `ExpenseExtensions.ToDto`
      (F-004) — **sin** duplicar el mapper inline ni introducir
      otro método de extensión.
- [ ] Propaga `ct` a `GetByFilterAsync` (async extremo a extremo).

### AC-6 — Devuelve `Result.Success(PagedResult<ExpenseDto>)`
- [ ] `return Result.Success(new PagedResult<ExpenseDto>(items, page.Total, filter.Page, filter.PageSize));`.
- [ ] El caller hace `(PagedResult<ExpenseDto>)result.Value!` tras
      `IsSuccess` (eco de F-004; documentado en el XML doc de
      `ExecuteAsync`).
- [ ] Firma del constructor 4-arg de `PagedResult<T>` respetada
      (`(IReadOnlyList<T> Items, int Total, int Page, int PageSize)`,
      verificada en `Common/PagedResult.cs`).

### AC-7 — Repo vacío (decisión DP-4 / no `Empty` factory)
- [ ] Si `page.Items` está vacío y `page.Total == 0`, devuelve
      `Result.Success(new PagedResult<ExpenseDto>(Array.Empty<ExpenseDto>(), 0, filter.Page, filter.PageSize))`.
- [ ] **No existe** `PagedResult<T>.Empty` (verificado en
      `Common/PagedResult.cs`); se usa el constructor 4-arg.
- [ ] **No se introduce** factory `static Empty(int, int)` en
      F-005 (es mejora post-Phase 3; fuera de scope — §5).

### AC-8 — Página fuera de rango (decisión DP-4 / no es error)
- [ ] Si `page.Items` es vacío pero `page.Total > 0` (p. ej. Page=999
      con 3 páginas reales), devuelve
      `Result.Success(new PagedResult<ExpenseDto>(Array.Empty<ExpenseDto>(), realTotal, requestedPage, requestedPageSize))`.
- [ ] **No es error.** El `Result.IsSuccess` es `true`; la UI decide
      cómo presentar una página vacía.

### AC-9 — Excepción del repo (decisión DP-3)
- [ ] `catch (Exception ex) when (ex is not OperationCanceledException)`.
- [ ] `_logger.LogError(ex, "Error al obtener gastos (Page={Page}, PageSize={PageSize})", filter.Page, filter.PageSize);`
      (preserva el stack trace en el log; usa `_logger` inyectado).
- [ ] `return Result.Failure("RepositoryError", "Ocurrió un error al obtener los gastos.");`.
- [ ] `OperationCanceledException` se **rethrow** (no se captura);
      la cancelación cooperativa sube al caller. Verificable con
      un test de `CancellationToken` cancelado.

### AC-10 — `Bootstrap` registra transient
- [ ] `Bootstrap.ConfigureServices` (en
      `UI/Composition/Bootstrap.cs`) añade 1 `AddTransient`:
      `GetTransactionsQuery`.
- [ ] No se modifica `AddPersistence` ni otros registros existentes.

### AC-11 — `FakeExpenseRepository` extendido
- [ ] `tests/ControlDeFinanzasIA.Tests.Unit/TestDoubles/FakeExpenseRepository.cs`
      expone `public void SetFilterResult(PagedResult<Expense> result)`.
- [ ] `GetByFilterAsync` retorna el `PagedResult<Expense>` configurado
      (default sigue siendo `new PagedResult<Expense>(Array.Empty<Expense>(), 0, filter.Page, filter.PageSize)`
      para no romper tests existentes de F-004).

### AC-12 — Tests del query (≥ 4)
- [ ] `tests/ControlDeFinanzasIA.Tests.Unit/Application/Expenses/GetTransactionsQueryTests.cs`
      existe con estos métodos (mínimo):
  - `ExecuteAsync_EmptyRepo_ReturnsSuccessWithEmptyPagedResult`
  - `ExecuteAsync_WithItems_ReturnsMappedDtosPreservingOrder`
  - `ExecuteAsync_PageBeyondEnd_ReturnsEmptyItemsWithRealTotal`
  - `ExecuteAsync_RepositoryThrows_ReturnsRepositoryErrorFailure`
- [ ] Recomendados adicionales:
  - `ExecuteAsync_PageLessThanOne_ReturnsInvalidFilter`
  - `ExecuteAsync_PageSizeZeroOrNegative_ReturnsInvalidFilter`
  - `ExecuteAsync_PageSizeOver200_ReturnsInvalidFilter`
  - `ExecuteAsync_CancelledToken_RethrowsOperationCanceledException`
- [ ] Usan `FakeExpenseRepository.SetFilterResult` y `NullLogger<GetTransactionsQuery>`
      (de `Microsoft.Extensions.Logging.Abstractions`, ya en CPM).
      Pure xUnit `Assert.*`. **No** FluentAssertions, **no**
      librería de mocking.

### AC-13 — Build limpio, tests verdes, NetArchTest, format
- [ ] `dotnet build -c Release`: 0 warnings, 0 errors.
- [ ] `dotnet test -c Release --no-build`: ~194 (baseline F-004) +
      ~5 nuevos ≈ **199 verdes**, 0 failures.
- [ ] NetArchTest R-1..R-5 verdes sin cambios (F-005 sólo añade
      tipos a `Application` + 1 registro DI en composition root
      `UI`; `Application` sigue dependiendo sólo de `Domain` + BCL
      + `FluentValidation` + `Microsoft.Extensions.Logging.Abstractions`,
      todos ya permitidos).
- [ ] `dotnet format style --verify-no-changes`: exit 0.

### AC-14 — Sin scope creep
- [ ] NO UI, NO filtros expuestos (`CategoryId`/`DateFrom`/`DateTo`
      en el `Request`), NO modificaciones a `Result` /
      `PagedResult<T>` / `IExpenseRepository` / `ExpenseFilter` /
      `ExpenseDto` / `ExpenseExtensions` / `IClock` / `Expense.cs`.
- [ ] NO FluentValidation (validación manual, DP-2), NO
      FluentAssertions, NO librería de mocking, NO factory
      `PagedResult<T>.Empty`, NO migraciones, NO nuevos paquetes
      NuGet.

## 9. Test cases

### 9.1 Query — `GetTransactionsQueryTests.cs` (≥ 4)

| # | Método | Escenario |
| --- | --- | --- |
| T-Q1 | `ExecuteAsync_EmptyRepo_ReturnsSuccessWithEmptyPagedResult` | `SetFilterResult(new PagedResult<Expense>([], 0, 1, 50))`. → `IsSuccess` true; `Value` castea a `PagedResult<ExpenseDto>`; `Items.Count == 0`; `Total == 0`; `Page == 1`; `PageSize == 50`; `TotalPages == 0`. |
| T-Q2 | `ExecuteAsync_WithItems_ReturnsMappedDtosPreservingOrder` | `SetFilterResult` con 3 `Expense` ya ordenados por `Date` desc (el repo garantiza el orden). → `IsSuccess` true; `Items.Count == 3`; cada DTO refleja `ExpenseExtensions.ToDto(entity)`; el orden se preserva (el query **no** reordena). |
| T-Q3 | `ExecuteAsync_PageBeyondEnd_ReturnsEmptyItemsWithRealTotal` | `SetFilterResult(new PagedResult<Expense>([], 30, 999, 50))` (simula 3 páginas reales, Page=999). → `IsSuccess` true; `Items.Count == 0`; `Total == 30`; `Page == 999`; `PageSize == 50`. **No es error** (DP-4 / AC-8). |
| T-Q4 | `ExecuteAsync_RepositoryThrows_ReturnsRepositoryErrorFailure` | Fake configurado para lanzar `InvalidOperationException("boom")` desde `GetByFilterAsync`. → `IsSuccess` false; `Error.Code == "RepositoryError"`; `Error.Message == "Ocurrió un error al obtener los gastos."`; `Value == null`. El log se invoca (no se aserta el contenido — `NullLogger<T>` descarta). |
| T-Q5 (rec.) | `ExecuteAsync_PageLessThanOne_ReturnsInvalidFilter` | `request = new(0, 50)` (o `(-1, 50)`). → `IsSuccess` false; `Error.Code == "InvalidFilter"`; **no** se invoca `GetByFilterAsync` (verificable: el fake no recibe la llamada). |
| T-Q6 (rec.) | `ExecuteAsync_PageSizeZeroOrNegative_ReturnsInvalidFilter` | `[Theory]` `[InlineData(0)]` `[InlineData(-1)]` para `PageSize`. → `IsSuccess` false; `Error.Code == "InvalidFilter"`. |
| T-Q7 (rec.) | `ExecuteAsync_PageSizeOver200_ReturnsInvalidFilter` | `request = new(1, 201)`. → `IsSuccess` false; `Error.Code == "InvalidFilter"`. |
| T-Q8 (rec.) | `ExecuteAsync_CancelledToken_RethrowsOperationCanceledException` | Fake lanza `OperationCanceledException` desde `GetByFilterAsync`. → el query **rethrow** (no captura); `await Assert.ThrowsAsync<OperationCanceledException>(...)`. **No** se devuelve `Result.Failure("RepositoryError", ...)` (acuña el código DP-3). |

### 9.2 Fake — `FakeExpenseRepository` extensión

| # | Método | Escenario |
| --- | --- | --- |
| T-F1 | `SetFilterResult_PersistsAndReturnedByGetByFilterAsync` | Tras `SetFilterResult(paged)`, `GetByFilterAsync(filter)` devuelve `paged` (mismo `Items`, `Total`, `Page`, `PageSize`). Las llamadas subsiguientes siguen devolviendo `paged` hasta que se llame de nuevo a `SetFilterResult`. |
| T-F2 | `GetByFilterAsync_Default_ReturnsEmptyWithoutSetFilterResult` | Sin llamar `SetFilterResult`, `GetByFilterAsync` devuelve vacío con `Total == 0` (patrón de F-004 sin romperse). |

## 10. Estructura de archivos esperada

```
src/
├── ControlDeFinanzasIA.Application/
│   ├── Abstractions/                             (sin cambios de F-005)
│   │   ├── IExpenseRepository.cs                 (Phase 1 F-006 + F-003)
│   │   └── ExpenseFilter.cs                      (F-003 — reutilizado, NO se toca)
│   ├── Common/                                   (sin cambios)
│   │   ├── Result.cs                             (Phase 1 F-005 — NO se toca)
│   │   └── PagedResult.cs                        (Phase 1 F-005 — NO se toca)
│   ├── DTOs/                                     (F-004 — reutilizado, NO se toca)
│   │   ├── ExpenseDto.cs
│   │   └── ExpenseExtensions.cs                  ← ToDto reutilizado
│   └── UseCases/Expenses/
│       ├── RegisterExpense/                      (F-004 — sin cambios)
│       └── GetTransactions/                      ← NUEVO (carpeta y 2 tipos)
│           ├── GetTransactionsQueryRequest.cs    ← nuevo
│           └── GetTransactionsQuery.cs           ← nuevo
├── ControlDeFinanzasIA.UI/Composition/
│   └── Bootstrap.cs                              ← MODIFICAR (1 AddTransient)
└── (Domain e Infrastructure sin cambios)

tests/
└── ControlDeFinanzasIA.Tests.Unit/
    ├── Application/Expenses/                      (carpeta ya existe por F-004)
    │   ├── RegisterExpenseUseCaseTests.cs        (F-004 — sin cambios)
    │   ├── RegisterExpenseValidatorTests.cs      (F-004 — sin cambios)
    │   └── GetTransactionsQueryTests.cs          ← nuevo
    └── TestDoubles/
        ├── FakeClock.cs                           (Phase 1 F-004 — sin cambios)
        └── FakeExpenseRepository.cs              ← MODIFICAR (añadir SetFilterResult)
```

## 11. Convenciones

- **Tests:** pure xUnit `[Fact]` / `[Theory]` + `[InlineData]`,
  aserciones `Assert.*` (`True`/`False`/`Equal`/`NotNull`/`Contains`/
  `ThrowsAsync`). **NO FluentAssertions** (cache
  `sdd/controldefinanzasia/testing-capabilities`). Naming
  `Method_Scenario_ExpectedResult`. **NO librería de mocking**;
  fakes hand-rolled (`FakeExpenseRepository`).
- **`ILogger` en tests:** `NullLogger<GetTransactionsQuery>` de
  `Microsoft.Extensions.Logging.Abstractions` (ya en CPM desde
  F-004). El test **no aserta el contenido del log**; sólo verifica
  `Result.Failure("RepositoryError", ...)` (no acopla al mensaje
  de log). Un `TestLogger<T>` hand-rolled que captura entradas es
  **opcional**, no requerido por F-005.
- **Códigos de error** en inglés (`"InvalidFilter"`,
  `"RepositoryError"`); **mensajes** en español (es-DO):
  `"Page debe ser mayor o igual a 1."`, `"PageSize debe ser mayor a 0."`,
  `"PageSize debe ser menor o igual a 200."`,
  `"Ocurrió un error al obtener los gastos."`. Eco de F-003/F-004 y
  `CategoryRepository` (Fase 2).
- **Validación:** **manual** (DP-2), no FluentValidation. Divergencia
  **explícita y justificada** de F-004: 2 reglas triviales sobre
  `int` no justifican `AbstractValidator` + registro DI + tests
  del validador. FluentValidation se reserva para comandos
  complejos (escritura, multi-campo, reglas cross-field). La
  ausencia **no** es omisión.
- **Código:** file-scoped namespace, `using` al tope, XML doc en
  cada clase y miembro público, `sealed` en los 2 tipos nuevos.
  Async/await extremo a extremo con `CancellationToken`.
- **`Result.Success(value)`** non-generic; el caller hace
  `(PagedResult<ExpenseDto>)result.Value!` tras `IsSuccess`.
  Documentado en el XML doc de `ExecuteAsync`.

## 12. Riesgos

| Riesgo | Prob. | Mitigación |
| --- | --- | --- |
| `PagedResult<T>` no tiene factory `Empty` — alguien lo invoca. | Media | Verificado en `Common/PagedResult.cs`: **no existe**. F-005 usa el constructor 4-arg (AC-7, AC-8). La fábrica es follow-up post-Phase 3, **no** se introduce aquí (§5). |
| `FakeExpenseRepository.GetByFilterAsync` hardcoded a vacío — tests de items / page-beyond-end no funcionan. | Alta | AC-11 exige `SetFilterResult(PagedResult<Expense>)`. Default vacío para no romper tests de F-004. |
| `ILogger.LogError` no se puede asertar con `NullLogger<T>`. | Baja | F-005 no aserta el log; sólo verifica `Result.Failure("RepositoryError", ...)`. `TestLogger<T>` hand-rolled es opcional (§11). |
| Confusión con `Result` non-generic: caller castea sin `IsSuccess` check → `InvalidCastException`/`NullReferenceException`. | Media | XML doc de `ExecuteAsync` documenta el patrón; tests cubren ambos paths (T-Q1 éxito, T-Q4 fallo). F-007 hace el cast tras `IsSuccess`. |
| `OperationCanceledException` atrapado por error genuino del repo (`TaskCanceledException` hereda de él) y enmascarado. | Baja | El filtro `when (ex is not OCE)` rethrow correctamente. T-Q8 cubre cancelación: el query **rethrow**, no devuelve `Result.Failure`. |
| `GetTransactionsQueryRequest` sin filtros — alguien añade `CategoryId` "sólo para pasar por el `ExpenseFilter`". | Media | Out of scope en §5. `ExpenseFilter` ya acepta los filtros en `null`; Phase 4 los añade al `Request` sin tocar este query ni el repo. |
| Validación manual diverge del patrón FluentValidation de F-004 — se interpreta como inconsistencia. | Media | DP-2 y §11 documentan la divergencia **explícitamente**. El patrón read-side es validación manual. |
| Scope creep en `Bootstrap.cs` (otros use cases). | Baja | Sólo 1 registro nuevo. F-005 no toca `AddPersistence`. |

## 13. Criterios para considerarla completada

- [ ] `dotnet build -c Release` desde la raíz: 0 warnings, 0 errors.
- [ ] `dotnet test -c Release --no-build`: ~194 (baseline F-004) +
      ~5 ≈ **199 tests verdes**, 0 failures.
- [ ] NetArchTest R-1..R-5 verdes (F-005 sólo añade tipos a
      `Application` + 1 registro DI en composition root `UI`).
- [ ] `dotnet format style --verify-no-changes`: exit 0.
- [ ] F-005 marcada como ✅ COMPLETADA en
      `spec/phases/phase-03/features.md` tras apply + verify; la
      spec se renombra a `F-005-get-transactions-query.done.md`
      tras sdd-archive.

## 14. Referencias

- `spec/features/phase-03/F-001-expense-domain-entity.done.md` —
  entidad `Expense`, invariantes INV-1..INV-8 (consumidos por `ToDto`).
- `spec/features/phase-03/F-003-expense-repository.done.md` —
  `GetByFilterAsync → Task<PagedResult<Expense>>` pelado (DP-3);
  record `ExpenseFilter` (init-only, sin auto-validación — F-005
  valida).
- `spec/features/phase-03/F-004-register-expense-use-case.done.md`
  — `ExpenseDto` + `ExpenseExtensions.ToDto` **reutilizados**;
  patrón `Bootstrap` `AddTransient`; `Microsoft.Extensions.Logging`
  8.0.1 ya en CPM; baseline ~194 tests.
- `spec/features/phase-01/F-005-comuntypes-result-pagedresult.done.md`
  — `Result` non-generic (`Value: object?`); `PagedResult<T>`
  constructor 4-arg, **sin** factory `Empty`; **no se modifica**.
- `spec/phases/phase-03/features.md` — F-005 en el desglose de
  Phase 3.
- `docs/architecture.md` §7 (queries devuelven `Result`/`PagedResult<T>`).
- `docs/vision.md` §4.1 (gastos), §4.3 (listado básico).
- `docs/roadmap.md` §Fase 3 (entregable: query de lectura paginado).