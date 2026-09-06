# Tareas — F-005: Application: DTOs + `GetTransactionsQuery` (paginado, read side)

> **Spec:** `spec/features/phase-03/F-005-get-transactions-query.spec.md`
> **Fuentes de verdad:** la spec, `docs/vision.md` §4.1/§4.3,
> `docs/architecture.md` §7,
> `spec/features/phase-03/F-001-expense-domain-entity.done.md`
> (INV-1..INV-8),
> `spec/features/phase-03/F-003-expense-repository.done.md`
> (`GetByFilterAsync → Task<PagedResult<Expense>>` pelado; `ExpenseFilter`),
> `spec/features/phase-03/F-004-register-expense-use-case.done.md`
> (`ExpenseDto` + `ExpenseExtensions.ToDto` reutilizados; patrón `Bootstrap`
> `AddTransient`; `Microsoft.Extensions.Logging` 8.0.1 ya en CPM;
> ~194 tests baseline),
> `spec/features/phase-01/F-005-comuntypes-result-pagedresult.done.md`
> (`Result` non-generic; `PagedResult<T>` constructor 4-arg, **sin** factory
> `Empty`).
>
> Cada tarea es implementable en menos de 2 horas. F-005 **sólo** añade tipos
> a `Application`, 1 registro a la composition root de `UI`, 1 método al fake
> de tests y tests unitarios. NO modifica `Domain`, `Infrastructure` (salvo DI),
> `Result`, `PagedResult<T>`, `IExpenseRepository`, `ExpenseFilter`,
> `ExpenseDto`, `ExpenseExtensions`, `IClock` ni `Expense.cs`.

---

## Pronóstico de carga de review

| Field | Value |
|-------|-------|
| Estimated changed lines | ~200–230 |
| 400-line budget risk | Low |
| Chained PRs recommended | No |
| Suggested split | Single PR |
| Delivery strategy | auto-forecast |
| Chain strategy | stacked-to-main |

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: stacked-to-main
400-line budget risk: Low

### Unidades de trabajo sugeridas

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Crear `GetTransactionsQueryRequest`, `GetTransactionsQuery`, registrar en `Bootstrap`, extender `FakeExpenseRepository`, crear `GetTransactionsQueryTests` y verificar | PR 1 | Base = `main`; single slice; ~200 líneas |

---

## Resumen

F-005 introduce el **primer query de lectura del proyecto**
(`GetTransactionsQuery`) y el patrón read-side que replicarán los queries
próximos: `Request` inmutable con defaults, **validación manual** (sin
FluentValidation — divergencia justificada de F-004 por la trivialidad de 2
reglas), construcción de `ExpenseFilter` (reutilizado de F-003, filtros en
`null` para esta fase), invocación a `IExpenseRepository.GetByFilterAsync`
(`PagedResult<Expense>` pelado, DP-3 de F-003), mapeo `Expense → ExpenseDto`
reutilizando `ExpenseExtensions.ToDto` de F-004 (sin duplicar), captura
defensiva de excepciones del repo con `ILogger.LogError` + rethrow de
`OperationCanceledException`, y retorno `Task<Result>` non-genérico envolviendo
`PagedResult<ExpenseDto>` en éxito. Cierra el lado de lectura del slice vertical
de Phase 3.

Entregables:

- `src/ControlDeFinanzasIA.Application/UseCases/Expenses/GetTransactions/GetTransactionsQueryRequest.cs` — `sealed record`, 2 campos con defaults.
- `src/ControlDeFinanzasIA.Application/UseCases/Expenses/GetTransactions/GetTransactionsQuery.cs` — `sealed class`, inyecta `IExpenseRepository` + `ILogger<GetTransactionsQuery>`.
- `src/ControlDeFinanzasIA.UI/Composition/Bootstrap.cs` — 1 registro `Transient`.
- `tests/ControlDeFinanzasIA.Tests.Unit/TestDoubles/FakeExpenseRepository.cs` — `SetFilterResult(PagedResult<Expense>)`.
- `tests/ControlDeFinanzasIA.Tests.Unit/Application/Expenses/GetTransactionsQueryTests.cs` — ≥ 4 tests con pure xUnit `Assert.*`.
- `dotnet build` limpio; `dotnet test` ≈ **199 verdes**; NetArchTest R-1..R-5 verdes.

---

## Resumen de tareas

| ID | Tarea | Estimación diff | Depende de | Verificación |
| --- | --- | --- | --- | --- |
| [x] T-001 | Crear `GetTransactionsQueryRequest.cs` | +12 / -0 | ninguna | `dotnet build` exit 0 |
| [x] T-002 | Implementar `GetTransactionsQuery.cs` | +55 / -0 | T-001 | `dotnet build` exit 0 |
| [x] T-003 | Registrar query en `Bootstrap.ConfigureServices` | +1 / -0 | T-002 | `dotnet build` exit 0 |
| [x] T-004 | Extender `FakeExpenseRepository` con `SetFilterResult` | +15 / -0 | ninguna | `dotnet test` F-004 aún pasa |
| [x] T-005 | Crear `GetTransactionsQueryTests.cs` (≥ 4 tests) | +110 / -0 | T-002, T-004 | `dotnet test --filter "FullyQualifiedName~GetTransactionsQueryTests"` ≥ 4 passed |
| [x] T-006 | Verificación final, formato y cierre | +2 / -2 | T-001..T-005 | `dotnet test` 199 passed; `dotnet format style --verify-no-changes` exit 0 |

**Total estimado:** ~5–7 horas. T-002 y T-005 son las más densas; las demás
son mecánicas o verificación.

---

## T-001 — Crear `GetTransactionsQueryRequest.cs`

**Objetivo**
Crear el request inmutable del query como `public sealed record` con defaults.

**Pasos**

1. Crear `src/ControlDeFinanzasIA.Application/UseCases/Expenses/GetTransactions/GetTransactionsQueryRequest.cs`.
2. Crear la carpeta si no existe.
3. File-scoped namespace `ControlDeFinanzasIA.Application.UseCases.Expenses.GetTransactions`.
4. Declarar:
   ```csharp
   public sealed record GetTransactionsQueryRequest(int Page = 1, int PageSize = 50);
   ```
5. XML doc en el record explicando que `Page` y `PageSize` son los únicos
   parámetros de esta fase; filtros llegan en Phase 4.
6. Ejecutar `dotnet build`.

**Verificación**

- `dotnet build "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --nologo`:
  exit 0, 0 warnings, 0 errors.
- Inspección: `sealed`, file-scoped namespace, 2 campos con defaults, XML doc.
- **No** incluye `CategoryId`, `DateFrom`, `DateTo`.

**Dependencias:** ninguna.
**Estimación:** S (< 1 h).
**Trazabilidad:** AC-1, §3, §8 AC-1, §10.

---

## T-002 — Implementar `GetTransactionsQuery.cs`

**Objetivo**
Crear el query que valida, filtra, invoca al repo, mapea y devuelve `Result`.

**Pasos**

1. Crear `src/ControlDeFinanzasIA.Application/UseCases/Expenses/GetTransactions/GetTransactionsQuery.cs`.
2. File-scoped namespace `ControlDeFinanzasIA.Application.UseCases.Expenses.GetTransactions`.
3. `using` al tope:
   - `ControlDeFinanzasIA.Application.Abstractions`
   - `ControlDeFinanzasIA.Application.Common`
   - `ControlDeFinanzasIA.Application.DTOs`
   - `Microsoft.Extensions.Logging`
4. Declarar `public sealed class GetTransactionsQuery`.
5. Constructor que inyecte `IExpenseRepository repository` y
   `ILogger<GetTransactionsQuery> logger`.
6. Implementar `ExecuteAsync(GetTransactionsQueryRequest request, CancellationToken ct = default)`:
   - `ArgumentNullException.ThrowIfNull(request);`
   - Validación manual **antes** de tocar el repo:
     - `request.Page < 1` → `Result.Failure("InvalidFilter", "Page debe ser mayor o igual a 1.")`.
     - `request.PageSize <= 0` → `Result.Failure("InvalidFilter", "PageSize debe ser mayor a 0.")`.
     - `request.PageSize > 200` → `Result.Failure("InvalidFilter", "PageSize debe ser menor o igual a 200.")`.
   - Construir `ExpenseFilter`:
     ```csharp
     var filter = new ExpenseFilter
     {
         Page = request.Page,
         PageSize = request.PageSize,
         CategoryId = null,
         DateFrom = null,
         DateTo = null,
     };
     ```
   - `try`:
     - `var page = await _repository.GetByFilterAsync(filter, ct);`
     - Mapear: `var items = page.Items.Select(e => e.ToDto()).ToList();`
     - Devolver `Result.Success(new PagedResult<ExpenseDto>(items, page.Total, filter.Page, filter.PageSize));`
   - `catch (Exception ex) when (ex is not OperationCanceledException)`:
     - `_logger.LogError(ex, "Error al obtener gastos (Page={Page}, PageSize={PageSize})", filter.Page, filter.PageSize);`
     - `return Result.Failure("RepositoryError", "Ocurrió un error al obtener los gastos.");`
   - `OperationCanceledException` se propaga sola (rethrow implícito por no
     capturarla).
7. XML doc en clase y `ExecuteAsync` documentando que el caller debe hacer
   `(PagedResult<ExpenseDto>)result.Value!` tras `IsSuccess`.
8. Ejecutar `dotnet build`.

**Verificación**

- `dotnet build ... -c Release --nologo`: exit 0, 0 warnings, 0 errors.
- Inspección: `sealed`; constructor con `IExpenseRepository` + `ILogger`;
  validación manual antes del repo; `ExpenseFilter` con filtros `null`;
  `page.Items.Select(e => e.ToDto())`; `catch` con filtro `is not OCE`;
  `LogError` con parámetros; `Result.Failure("RepositoryError", ...)`.

**Dependencias:** T-001, F-003, F-004.
**Estimación:** M (1–2 h).
**Trazabilidad:** AC-2..AC-9, §6 DP-1..DP-3, §8, §9.1.

---

## T-003 — Registrar query en `Bootstrap`

**Objetivo**
Añadir 1 registro `Transient` en `Bootstrap.ConfigureServices`.

**Pasos**

1. Abrir `src/ControlDeFinanzasIA.UI/Composition/Bootstrap.cs`.
2. Localizar `ConfigureServices`.
3. Añadir:
   ```csharp
   services.AddTransient<GetTransactionsQuery>();
   ```
   junto al registro existente de `RegisterExpenseUseCase`.
4. Asegurar `using ControlDeFinanzasIA.Application.UseCases.Expenses.GetTransactions;`.
5. **No** modificar `AddPersistence`.
6. Ejecutar `dotnet build`.

**Verificación**

- `dotnet build ... -c Release --nologo`: exit 0, 0 warnings, 0 errors.
- Inspección: 1 `AddTransient<GetTransactionsQuery>()`; `AddPersistence` intacto.

**Dependencias:** T-002.
**Estimación:** XS (< 30 min).
**Trazabilidad:** AC-10, §8 AC-10, §10.

---

## T-004 — Extender `FakeExpenseRepository` con `SetFilterResult`

**Objetivo**
Permitir configurar el resultado de `GetByFilterAsync` para los tests de
F-005 sin romper los tests existentes de F-004.

**Pasos**

1. Abrir `tests/ControlDeFinanzasIA.Tests.Unit/TestDoubles/FakeExpenseRepository.cs`.
2. Añadir campo privado:
   ```csharp
   private PagedResult<Expense>? _filterResult;
   ```
3. Añadir método público:
   ```csharp
   public void SetFilterResult(PagedResult<Expense> result) => _filterResult = result;
   ```
4. Modificar `GetByFilterAsync(ExpenseFilter filter, CancellationToken ct)`
   para retornar:
   ```csharp
   return Task.FromResult(_filterResult ?? new PagedResult<Expense>(
       Array.Empty<Expense>(),
       0,
       filter.Page,
       filter.PageSize));
   ```
5. Ejecutar `dotnet test --filter "FullyQualifiedName~RegisterExpenseUseCaseTests"`
   para confirmar que F-004 sigue verde.

**Verificación**

- `dotnet build ... -c Release --nologo`: exit 0, 0 warnings, 0 errors.
- `dotnet test --filter "FullyQualifiedName~RegisterExpenseUseCaseTests"`: todos passed.
- Inspección: `SetFilterResult` existe; default vacío preservado.

**Dependencias:** ninguna.
**Estimación:** S (< 1 h).
**Trazabilidad:** AC-11, §8 AC-11, §9.2.

---

## T-005 — Crear `GetTransactionsQueryTests.cs`

**Objetivo**
Tests unitarios del query con pure xUnit `Assert.*`, `FakeExpenseRepository`
extendido y `NullLogger<GetTransactionsQuery>`.

**Pasos**

1. Crear `tests/ControlDeFinanzasIA.Tests.Unit/Application/Expenses/GetTransactionsQueryTests.cs`.
2. Crear la carpeta si no existe.
3. File-scoped namespace `ControlDeFinanzasIA.Tests.Unit.Application.Expenses`.
4. `using` al tope:
   - `ControlDeFinanzasIA.Application.Common`
   - `ControlDeFinanzasIA.Application.DTOs`
   - `ControlDeFinanzasIA.Application.UseCases.Expenses.GetTransactions`
   - `ControlDeFinanzasIA.Domain.Transactions`
   - `ControlDeFinanzasIA.Domain.ValueObjects`
   - `ControlDeFinanzasIA.Tests.Unit.TestDoubles`
   - `Microsoft.Extensions.Logging.Abstractions`
   - `Xunit`
5. Implementar al menos 4 tests obligatorios (§9.1 T-Q1..T-Q4):
   - `ExecuteAsync_EmptyRepo_ReturnsSuccessWithEmptyPagedResult`
   - `ExecuteAsync_WithItems_ReturnsMappedDtosPreservingOrder`
   - `ExecuteAsync_PageBeyondEnd_ReturnsEmptyItemsWithRealTotal`
   - `ExecuteAsync_RepositoryThrows_ReturnsRepositoryErrorFailure`
6. Implementar tests recomendados adicionales:
   - `ExecuteAsync_PageLessThanOne_ReturnsInvalidFilter`
   - `ExecuteAsync_PageSizeZeroOrNegative_ReturnsInvalidFilter` (`[Theory]`)
   - `ExecuteAsync_PageSizeOver200_ReturnsInvalidFilter`
   - `ExecuteAsync_CancelledToken_RethrowsOperationCanceledException`
7. En cada test:
   - `var fake = new FakeExpenseRepository();`
   - `var query = new GetTransactionsQuery(fake, NullLogger<GetTransactionsQuery>.Instance);`
   - Configurar `fake.SetFilterResult(...)` según el escenario.
   - Usar `Assert.True`, `Assert.False`, `Assert.Equal`, `Assert.NotNull`,
     `Assert.ThrowsAsync<OperationCanceledException>`, etc.
8. Para el test de página fuera de rango, usar
   `new PagedResult<Expense>([], 30, 999, 50)` y verificar que
   `result.IsSuccess` es `true`, `Items.Count == 0`, `Total == 30`,
   `Page == 999`, `PageSize == 50`.
9. Para el test de error del repo, hacer que el fake lance
   `InvalidOperationException("boom")` y verificar
   `Error.Code == "RepositoryError"` y
   `Error.Message == "Ocurrió un error al obtener los gastos."`.
10. Para validación, verificar que `Error.Code == "InvalidFilter"` y que el
    fake **no** recibió la llamada a `GetByFilterAsync` (no asertable
    directamente; el test pasa porque el fake no se configuró para fallar y
    el resultado `IsSuccess == false` demuestra que no llegó al repo).
11. Ejecutar `dotnet test --filter "FullyQualifiedName~GetTransactionsQueryTests"`.

**Verificación**

- `dotnet build ... -c Release --nologo`: exit 0, 0 warnings, 0 errors.
- `dotnet test ... --filter "FullyQualifiedName~GetTransactionsQueryTests"`: ≥ 4 passed, 0 failed.
- Inspección: file-scoped namespace, `Assert.*`, `NullLogger<T>`,
  `FakeExpenseRepository.SetFilterResult`, **no** FluentAssertions.

**Dependencias:** T-002, T-004, F-001, F-003, F-004.
**Estimación:** M (2–3 h).
**Trazabilidad:** AC-12, §9.1 T-Q1..T-Q8, §9.2 T-F1..T-F2.

---

## T-006 — Verificación final, formato y cierre

**Objetivo**
Ejecutar la batería de verificación final y cerrar la feature.

**Pasos**

1. `dotnet build -c Release` y `dotnet build -c Debug`: 0 warnings, 0 errors.
2. `dotnet test -c Release --no-build` completo: ~194 (baseline F-004) +
   ~5 ≈ **199 passed**, 0 failed.
3. `dotnet test --filter "FullyQualifiedName~ArchitectureTests"`: todos passed.
4. `dotnet list src/ControlDeFinanzasIA.Domain package`: vacío.
5. `dotnet list src/ControlDeFinanzasIA.Application package`: sólo los ya
   permitidos (`FluentValidation`, `Microsoft.Extensions.Logging.Abstractions`).
6. `dotnet list src/ControlDeFinanzasIA.UI reference`: sin referencia a
   `Infrastructure`.
7. `dotnet format style --verify-no-changes`: exit 0.
8. Verificar convenciones en archivos nuevos: file-scoped namespace, `using` al
   tope, XML doc, `sealed`, códigos en inglés, mensajes en español (es-DO).
9. Confirmar AC-14: sin UI, sin filtros expuestos, sin modificar `Result` /
   `PagedResult<T>` / `IExpenseRepository` / `ExpenseFilter` / `ExpenseDto` /
   `ExpenseExtensions` / `IClock` / `Expense.cs`, sin FluentValidation, sin
   FluentAssertions, sin librería de mocking, sin factory `PagedResult<T>.Empty`,
   sin migraciones, sin nuevos paquetes NuGet.
10. Actualizar `spec/phases/phase-03/features.md`: F-005 a "✅ COMPLETADA"
    (lo hará sdd-archive tras verify; en tasks de apply dejar marcado como
    pendiente hasta verify).
11. `dotnet build -c Release` final.

**Verificación**

- `dotnet build -c Release`: exit 0.
- `dotnet test -c Release --no-build`: ~199 passed, 0 failed.
- NetArchTest R-1..R-5: passed.
- `dotnet format style --verify-no-changes`: exit 0.

**Dependencias:** T-001..T-005.
**Estimación:** S (1 h).
**Trazabilidad:** AC-13, AC-14, §13.

---

## Orden de ejecución recomendado

**Secuencial con paralelismo limitado:**

1. **T-001** (request) puede ejecutarse primera; no depende de nadie.
2. **T-004** (fake) puede ejecutarse en paralelo con T-001.
3. **T-002** (query) depende de T-001.
4. **T-003** (Bootstrap) depende de T-002.
5. **T-005** (tests) depende de T-002 y T-004.
6. **T-006** cierra la feature.

```text
T-001 ───→ T-002 ───→ T-003 ──┐
                              ├→ T-005 ──→ T-006
T-004 ────────────────────────┘
```

---

## Tareas bloqueantes / habilitadoras

| Tarea | Habilita | Razón |
| --- | --- | --- |
| T-001 | T-002 | El query recibe `GetTransactionsQueryRequest`. |
| T-002 | T-003, T-005 | Bootstrap registra el query; tests lo instancian. |
| T-004 | T-005 | `GetTransactionsQueryTests` necesita `SetFilterResult`. |
| T-003 | T-006 | Registro DI verificado por NetArchTest R-4 y build. |
| T-005 | T-006 | Todos los tests deben pasar antes del cierre. |

---

## Estimación total

| Tarea | Dificultad | Horas estimadas |
| --- | --- | --- |
| T-001 | S | 0.5 |
| T-002 | M | 1–2 |
| T-003 | XS | 0.25 |
| T-004 | S | 0.5 |
| T-005 | M | 2–3 |
| T-006 | S | 1 |
| **Total** | | **~5–7 horas (~1 jornada)** |

---

## Riesgos y mitigaciones

| Riesgo | Prob. | Impacto | Mitigación |
| --- | --- | --- | --- |
| `PagedResult<T>` no tiene factory `Empty` — alguien lo invoca. | Media | Medio | Verificado en `Common/PagedResult.cs`: **no existe**. F-005 usa el constructor 4-arg (AC-7, AC-8). La fábrica es follow-up post-Phase 3, **no** se introduce aquí (§5). |
| `FakeExpenseRepository.GetByFilterAsync` hardcoded a vacío — tests de items / page-beyond-end no funcionan. | Alta | Alto | AC-11 exige `SetFilterResult(PagedResult<Expense>)`. Default vacío para no romper tests de F-004. |
| `ILogger.LogError` no se puede asertar con `NullLogger<T>`. | Baja | Bajo | F-005 no aserta el log; sólo verifica `Result.Failure("RepositoryError", ...)`. `TestLogger<T>` hand-rolled es opcional (§11). |
| Confusión con `Result` non-generic: caller castea sin `IsSuccess` check → `InvalidCastException`/`NullReferenceException`. | Media | Medio | XML doc de `ExecuteAsync` documenta el patrón; tests cubren ambos paths (T-Q1 éxito, T-Q4 fallo). F-007 hace el cast tras `IsSuccess`. |
| `OperationCanceledException` atrapado por error genuino del repo (`TaskCanceledException` hereda de él) y enmascarado. | Baja | Bajo | El filtro `when (ex is not OCE)` rethrow correctamente. T-Q8 cubre cancelación: el query **rethrow**, no devuelve `Result.Failure`. |
| `GetTransactionsQueryRequest` sin filtros — alguien añade `CategoryId` "sólo para pasar por el `ExpenseFilter`". | Media | Medio | Out of scope en §5. `ExpenseFilter` ya acepta los filtros en `null`; Phase 4 los añade al `Request` sin tocar este query ni el repo. |
| Validación manual diverge del patrón FluentValidation de F-004 — se interpreta como inconsistencia. | Media | Bajo | DP-2 y §11 documentan la divergencia **explícitamente**. El patrón read-side es validación manual. |
| Scope creep en `Bootstrap.cs` (otros use cases). | Baja | Bajo | Sólo 1 registro nuevo. F-005 no toca `AddPersistence`. |

---

## Notas para apply

- **No tocar `Domain/Transactions/Expense.cs`.** F-005 sólo lee la entidad a
  través de `ExpenseExtensions.ToDto`; no hay migración.
- **No tocar `Result` ni `PagedResult<T>`.** F-005 Phase 1 definió `Result`
  non-generic con `Value: object?`; `PagedResult<T>` usa el constructor 4-arg
  y **no** tiene factory `Empty`. Query retorna
  `Result.Success(new PagedResult<ExpenseDto>(...))`; caller hace
  `(PagedResult<ExpenseDto>)result.Value!` tras `IsSuccess`.
- **No tocar `ExpenseDto` / `ExpenseExtensions`.** Reutilizar `ToDto` de F-004;
  no duplicar el mapper.
- **Validación manual (DP-2).** Sin `AbstractValidator`, sin registro DI de
  validador. Sólo 2 reglas triviales sobre `int`.
- **Catch defensivo con rethrow de OCE.** Usar
  `catch (Exception ex) when (ex is not OperationCanceledException)`. El log
  preserva el stack trace; el `Result.Failure` traduce a error de negocio.
- **Códigos de error en inglés, mensajes en español (es-DO):**
  - `"InvalidFilter"` / `"Page debe ser mayor o igual a 1."`
  - `"InvalidFilter"` / `"PageSize debe ser mayor a 0."`
  - `"InvalidFilter"` / `"PageSize debe ser menor o igual a 200."`
  - `"RepositoryError"` / `"Ocurrió un error al obtener los gastos."`
- **`ILogger` en tests.** Usar `NullLogger<GetTransactionsQuery>.Instance` de
  `Microsoft.Extensions.Logging.Abstractions` (ya en CPM). No asertar el log.
  `TestLogger<T>` hand-rolled es opcional, no requerido.
- **`FakeExpenseRepository` extendido con cuidado.** `SetFilterResult` debe
  persistir el `PagedResult<Expense>` configurado; `GetByFilterAsync` lo
  devuelve si existe. El default vacío preserva compatibilidad con F-004.
- **No modificar `AddPersistence`.** F-003 ya registró `IExpenseRepository`.
- **Tests pure xUnit.** `Assert.True`, `Assert.False`, `Assert.Equal`,
  `Assert.NotNull`, `Assert.ThrowsAsync`. **NO FluentAssertions, NO Moq/NSubstitute.**
- **`ExpenseFilter` con filtros en `null`.** Aunque `ExpenseFilter` acepta
  `CategoryId?`, `DateFrom?`, `DateTo?`, F-005 los deja en `null`. Phase 4
  los propagará sin tocar el query.

---

## Definition of Done (DoD)

- [x] AC-1: `GetTransactionsQueryRequest` `sealed record`, `Page = 1`, `PageSize = 50`, XML doc. (T-001)
- [x] AC-2: `GetTransactionsQuery` `sealed class`, ctor con `IExpenseRepository` + `ILogger`, `ExecuteAsync → Task<Result>`. (T-002)
- [x] AC-3: Validación manual sin FluentValidation; 3 reglas `InvalidFilter`. (T-002)
- [x] AC-4: `ExpenseFilter` construido con `Page`, `PageSize`, filtros `null`. (T-002)
- [x] AC-5: Invoca `repo.GetByFilterAsync(filter, ct)` y mapea con `ToDto`. (T-002)
- [x] AC-6: Devuelve `Result.Success(new PagedResult<ExpenseDto>(...))`. (T-002)
- [x] AC-7: Repo vacío devuelve `PagedResult<ExpenseDto>` vacío vía constructor 4-arg; no factory `Empty`. (T-002)
- [x] AC-8: Página fuera de rango devuelve éxito con `Items=[]` y `Total` real. (T-002)
- [x] AC-9: Excepción del repo → `LogError` + `Result.Failure("RepositoryError", ...)`. `OperationCanceledException` rethrow. (T-002)
- [x] AC-10: `Bootstrap` registra `GetTransactionsQuery` `Transient`. (T-003)
- [x] AC-11: `FakeExpenseRepository` tiene `SetFilterResult`; default vacío. (T-004)
- [x] AC-12: `GetTransactionsQueryTests` ≥ 4 tests verdes con pure xUnit. (T-005)
- [x] AC-13: Build limpio, ~199 tests verdes, NetArchTest R-1..R-5 verdes, format exit 0. (T-006)
- [x] AC-14: Sin scope creep. (T-006)

---

## Referencias

- `spec/features/phase-03/F-005-get-transactions-query.spec.md`
- `spec/phases/phase-03/features.md`
- `spec/features/phase-03/F-001-expense-domain-entity.done.md`
- `spec/features/phase-03/F-003-expense-repository.done.md`
- `spec/features/phase-03/F-004-register-expense-use-case.done.md`
- `spec/features/phase-01/F-005-comuntypes-result-pagedresult.done.md`
- `tasks/phase-03/F-004-register-expense-use-case.tasks.md`
- Engram `sdd/controldefinanzasia/testing-capabilities`
- Engram `sdd/f-005p3-get-transactions-query/proposal`
- Engram `sdd/f-005p3-get-transactions-query/spec`
