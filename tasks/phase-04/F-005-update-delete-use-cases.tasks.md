# Tareas — F-005 (Phase 4): `UpdateExpenseUseCase` + `UpdateIncomeUseCase` + `DeleteTransactionUseCase`

> **Spec:** `spec/features/phase-04/F-005-update-delete-use-cases.spec.md`
> **change_id:** `f-005p4-update-delete-use-cases`
> **Estado:** ✅ Implementado y verificado
> **Fecha:** 2026-08-27
> **Fuentes de verdad:** spec F-005 P4, proposal `.atl/sdd/f-005p4-update-delete-use-cases/proposal.md`,
> `tasks/phase-04/F-003-register-income-use-case.tasks.md` (espejo de use case de escritura),
> `tasks/phase-04/F-004-tag-entity-and-many-to-many.tasks.md` (precedente de chained PRs).
> **Dependencias:** F-001 Phase 3 ✅ (`Expense` + `Expense.Update` 6-param), F-001 Phase 4 ✅ (`Income` + `Income.Update` 7-param),
> F-002 Phase 4 ✅ (`IIncomeRepository`), F-003 Phase 4 ✅ (`RegisterIncomeUseCase` patrón espejo).

---

## Pronóstico de carga de review

| Field | Value |
|-------|-------|
| Estimated changed lines | ~790 |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1: T-001..T-005 (tipos + use cases de producción) ~480 líneas; PR 2: T-006..T-008 (DI + fakes + tests + gates) ~310 líneas |
| Delivery strategy | auto-forecast |
| Chain strategy | stacked-to-main |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: High

### Unidades de trabajo sugeridas

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Crear `TransactionType`, comandos, validadores y use cases de Update/Delete | PR 1 | Base = `main`; ~480 líneas; no tests |
| 2 | Registrar DI, extender fakes, crear tests unitarios y ejecutar gates | PR 2 | Base = PR 1; depende de los tipos de producción |

---

## Resumen de tareas

| ID | Tarea | Estimación diff | Depende de | Verificación | Estado |
| --- | --- | --- | --- | --- | --- |
| T-001 | Crear `TransactionType` enum + `UpdateExpenseCommand` + `UpdateExpenseValidator` | +80 / -0 | ninguna | `dotnet build` exit 0 | [x] |
| T-002 | Crear `UpdateExpenseUseCase` (6 args, `Result.Success(null!)`) | +120 / -0 | T-001 | `dotnet build` exit 0 | [x] |
| T-003 | Crear `UpdateIncomeCommand` + `UpdateIncomeValidator` | +80 / -0 | ninguna | `dotnet build` exit 0 | [x] |
| T-004 | Crear `UpdateIncomeUseCase` (7 args, source `null`) | +120 / -0 | T-003 | `dotnet build` exit 0 | [x] |
| T-005 | Crear `DeleteTransactionCommand` + `DeleteTransactionUseCase` | +80 / -0 | ninguna | `dotnet build` exit 0 | [x] |
| T-006 | Registrar DI en `Bootstrap.cs` + extender `FakeExpenseRepository` + `FakeIncomeRepository` | +60 / -0 | T-002, T-004, T-005 | `dotnet build` exit 0 | [x] |
| T-007 | Crear 3 archivos de tests unitarios (UpdateExpense, UpdateIncome, DeleteTransaction) | +250 / -0 | T-006 | ≥10 tests pasan | [x] |
| T-008 | Verificación final, formato y cierre | 0 | T-001..T-007 | Todos los gates verdes | [x] |

**Total estimado:** ~790 líneas cambiadas.

---

## T-001 — Crear `TransactionType` enum + `UpdateExpenseCommand` + `UpdateExpenseValidator`

**AC refs:** AC-1, AC-2, AC-3

**Pre-condiciones**
- `Currency.cs` existe en `src/.../Domain/Enums/` como patrón de enum con XML doc.
- `RegisterExpenseCommand.cs`/`RegisterExpenseValidator.cs` verificados como espejo.

**Pasos**
1. Crear `src/ControlDeFinanzasIA.Domain/Enums/TransactionType.cs`:
   - File-scoped namespace `ControlDeFinanzasIA.Domain.Enums`.
   - `public enum TransactionType { Expense, Income }` sin ordinales explícitos.
   - XML doc en tipo + cada valor.
2. Crear `src/ControlDeFinanzasIA.Application/UseCases/Expenses/UpdateExpense/UpdateExpenseCommand.cs`:
   - `public sealed record UpdateExpenseCommand(Guid Id, Guid CategoryId, DateOnly Date, decimal Amount, Currency Currency, string Description, string? Note)`.
   - **`CategoryId` es `Guid` (required, NO `Guid?`)** per C-2.
   - `using ControlDeFinanzasIA.Domain.Enums;` y XML doc.
3. Crear `src/ControlDeFinanzasIA.Application/UseCases/Expenses/UpdateExpense/UpdateExpenseValidator.cs`:
   - `public sealed class UpdateExpenseValidator : AbstractValidator<UpdateExpenseCommand>`.
   - 6 reglas sync: `Id`, `CategoryId` (incondicional), `Amount`, `Description`, `Note`, `Currency`.
   - Mensajes en español (D-ML), espejo de `RegisterExpenseValidator` + `"El identificador es obligatorio."`.
4. Ejecutar `dotnet build -c Debug --nologo`.

**Criterios de verificación**
- Build exit 0, 0 warnings, 0 errors.
- `CategoryId` es `Guid` no nullable.
- 6 reglas sync; sin `MustAsync`/`CustomAsync`.

**Notas**
- C-2 locked: `UpdateExpenseCommand.CategoryId` es required; no seguir el `Guid?` del phase plan RF-7.
- El validador no necesita regla de `Date` (igual que Register).

---

## T-002 — Crear `UpdateExpenseUseCase`

**AC refs:** AC-4

**Pre-condiciones**
- T-001 completada (`UpdateExpenseCommand` + `UpdateExpenseValidator` existen).
- `Expense.Update` verificado con 6 params: `(Guid categoryId, DateOnly date, Money amount, string description, string? note, DateTime updatedAt)`.
- `Result` non-generic verificado.

**Pasos**
1. Crear `src/ControlDeFinanzasIA.Application/UseCases/Expenses/UpdateExpense/UpdateExpenseUseCase.cs`.
2. File-scoped namespace; `using` al tope:
   - `ControlDeFinanzasIA.Application.Abstractions`
   - `ControlDeFinanzasIA.Application.Common`
   - `ControlDeFinanzasIA.Domain.Transactions`
   - `ControlDeFinanzasIA.Domain.ValueObjects`
   - `FluentValidation`
   - `FluentValidation.Results`
   - `Microsoft.Extensions.Logging`
3. Declarar `public sealed class UpdateExpenseUseCase`.
4. Campos readonly: `_expenseRepository`, `_clock`, `_validator`, `_logger`.
5. Constructor con 4 parámetros inyectados.
6. Implementar `public async Task<Result> ExecuteAsync(UpdateExpenseCommand command, CancellationToken ct = default)`:
   - `ArgumentNullException.ThrowIfNull(command);`
   - `_validator.Validate(command)` sync → si inválido, agrupar errores por `PropertyName` → `Result.Failure("Validation", "El comando para actualizar el gasto no es válido.", details)` (D-3).
   - `existing = await _expenseRepository.GetByIdAsync(command.Id, ct)` → si `null`, `Result.Failure("NotFound", $"Expense {command.Id} does not exist.");`
   - `DateTime audit = _clock.UtcNow;`
   - `try { var money = Money.Create(command.Amount, command.Currency); existing.Update(command.CategoryId, command.Date, money, command.Description, command.Note, audit); }` (**6 args, sin source** — C-1).
   - `catch (ArgumentException ex) { _logger.LogWarning(ex, "Domain invariant violation while updating expense."); return Result.Failure("DomainInvariant", ex.Message); }` (D-4).
   - `Result updateResult = await _expenseRepository.UpdateAsync(existing, ct);` → si falla, propagar.
   - `return Result.Success(null!);` (D-9).
7. Ejecutar `dotnet build -c Debug --nologo`.

**Criterios de verificación**
- Build exit 0, 0 warnings, 0 errors.
- Firma `Task<Result>` non-generic.
- Llamada a `existing.Update` con exactamente 6 args posicionales.

**Notas**
- **CRÍTICO:** no pasar un 7º arg `null` a `Expense.Update` — no compila (C-1).
- **CRÍTICO:** pasar `Description`/`Note` as-is, sin `.Trim()` (D-5).
- `audit` se lee una sola vez.

---

## T-003 — Crear `UpdateIncomeCommand` + `UpdateIncomeValidator`

**AC refs:** AC-5, AC-6

**Pre-condiciones**
- `RegisterIncomeCommand.cs`/`RegisterIncomeValidator.cs` verificados como espejo.

**Pasos**
1. Crear `src/ControlDeFinanzasIA.Application/UseCases/Incomes/UpdateIncome/UpdateIncomeCommand.cs`:
   - `public sealed record UpdateIncomeCommand(Guid Id, Guid? CategoryId, DateOnly Date, decimal Amount, Currency Currency, string Description, string? Note)`.
   - **`CategoryId` es `Guid?` nullable**.
   - `using ControlDeFinanzasIA.Domain.Enums;` y XML doc (documenta reset de `Source`).
2. Crear `src/ControlDeFinanzasIA.Application/UseCases/Incomes/UpdateIncome/UpdateIncomeValidator.cs`:
   - `public sealed class UpdateIncomeValidator : AbstractValidator<UpdateIncomeCommand>`.
   - 6 reglas sync: `Id`, `CategoryId` (condicional `.When(c => c.CategoryId is not null)`), `Amount`, `Description`, `Note`, `Currency`.
   - Mensajes en español, espejo de `RegisterIncomeValidator`.
3. Ejecutar `dotnet build -c Debug --nologo`.

**Criterios de verificación**
- Build exit 0, 0 warnings, 0 errors.
- `CategoryId` es `Guid?` nullable.
- Regla `CategoryId` solo aplica cuando no es null.

**Notas**
- La 5ª regla `CategoryId` condicional es el espejo exacto de `RegisterIncomeValidator`.

---

## T-004 — Crear `UpdateIncomeUseCase`

**AC refs:** AC-7

**Pre-condiciones**
- T-003 completada.
- `Income.Update` verificado con 7 params: `(Guid? categoryId, DateOnly date, Money amount, string description, string? note, string? source, DateTime updatedAt)`.

**Pasos**
1. Crear `src/ControlDeFinanzasIA.Application/UseCases/Incomes/UpdateIncome/UpdateIncomeUseCase.cs`.
2. File-scoped namespace; mismos `using` que T-002.
3. Declarar `public sealed class UpdateIncomeUseCase` con 4 deps readonly.
4. Implementar `ExecuteAsync` espejo de AC-4:
   - Validación → `Result.Failure("Validation", "El comando para actualizar el ingreso no es válido.", details)`.
   - `GetByIdAsync` → `Result.Failure("NotFound", $"Income {command.Id} does not exist.")`.
   - `try { var money = Money.Create(...); existing.Update(command.CategoryId, command.Date, money, command.Description, command.Note, null, audit); }` (**7 args, `null` para `source`** — D-6/Q4).
   - `catch (ArgumentException ex)` → `"DomainInvariant"`.
   - Propagar `UpdateAsync`; retornar `Result.Success(null!)`.
5. Ejecutar `dotnet build -c Debug --nologo`.

**Criterios de verificación**
- Build exit 0, 0 warnings, 0 errors.
- Llamada a `existing.Update` con exactamente 7 args; 6º arg es `null`.

**Notas**
- **CRÍTICO:** `source` es `null` (reset) — no leer `existing.Source` (D-6/Q4).
- `Income.Update` acepta `Guid? categoryId`; el comando nullable es compatible.

---

## T-005 — Crear `DeleteTransactionCommand` + `DeleteTransactionUseCase`

**AC refs:** AC-8, AC-9

**Pre-condiciones**
- T-001 completada (`TransactionType` existe).

**Pasos**
1. Crear `src/ControlDeFinanzasIA.Application/UseCases/Transactions/DeleteTransaction/DeleteTransactionCommand.cs`:
   - `public sealed record DeleteTransactionCommand(Guid Id, TransactionType Type)`.
   - `using ControlDeFinanzasIA.Domain.Enums;` y XML doc.
2. Crear `src/ControlDeFinanzasIA.Application/UseCases/Transactions/DeleteTransaction/DeleteTransactionUseCase.cs`:
   - `public sealed class DeleteTransactionUseCase` con 3 deps: `_expenseRepository`, `_incomeRepository`, `_logger`.
   - Constructor inyecta `IExpenseRepository`, `IIncomeRepository`, `ILogger<DeleteTransactionUseCase>`.
   - `public async Task<Result> ExecuteAsync(DeleteTransactionCommand command, CancellationToken ct = default)`:
     - `ArgumentNullException.ThrowIfNull(command);`
     - `if (command.Id == Guid.Empty) return Result.Failure("Validation", "Id cannot be empty.");` (D-7).
     - `switch (command.Type)`:
       - `TransactionType.Expense` → `return await _expenseRepository.RemoveAsync(command.Id, ct);`
       - `TransactionType.Income` → `return await _incomeRepository.RemoveAsync(command.Id, ct);`
       - `default` → `return Result.Failure("Validation", $"Unknown transaction type: {command.Type}.");` (D-8).
3. Ejecutar `dotnet build -c Debug --nologo`.

**Criterios de verificación**
- Build exit 0, 0 warnings, 0 errors.
- Sin `IValidator<DeleteTransactionCommand>`; guard inline.
- `switch` cubre ambos valores de enum + `default`.

**Notas**
- No usar FluentValidation para Delete (D-7).
- Propagar el `Result` del repo verbatim (sin re-envoltorio).

---

## T-006 — Registrar DI + extender fakes

**AC refs:** AC-10, AC-11, AC-12

**Pre-condiciones**
- T-002, T-004, T-005 completadas.
- `Bootstrap.cs` verificado con registros de `RegisterExpenseUseCase`, `RegisterIncomeUseCase`, `GetTransactionsQuery` y 2 validators.

**Pasos**
1. Abrir `src/ControlDeFinanzasIA.UI/Composition/Bootstrap.cs`:
   - Añadir 3 `using`:
     - `ControlDeFinanzasIA.Application.UseCases.Expenses.UpdateExpense`
     - `ControlDeFinanzasIA.Application.UseCases.Incomes.UpdateIncome`
     - `ControlDeFinanzasIA.Application.UseCases.Transactions.DeleteTransaction`
   - Añadir 3 registros de use case: `services.AddTransient<UpdateExpenseUseCase>();`, `UpdateIncomeUseCase`, `DeleteTransactionUseCase`.
   - Añadir 2 registros de validators: `services.AddTransient<IValidator<UpdateExpenseCommand>, UpdateExpenseValidator>();`, `IValidator<UpdateIncomeCommand>`.
   - Actualizar XML doc de `ConfigureServices` para mencionar 5 use cases + 4 validators.
2. Abrir `tests/.../TestDoubles/FakeExpenseRepository.cs`:
   - Añadir campos privados: `_updateResult`, `_removeResult`, `_getByIdResult`.
   - Añadir setters: `SetUpdateResult(Result)`, `SetRemoveResult(Result)`, `SetGetByIdResult(Expense?)`.
   - Modificar `UpdateAsync`, `RemoveAsync`, `GetByIdAsync` para retornar los valores configurables.
3. Abrir `tests/.../TestDoubles/FakeIncomeRepository.cs`:
   - Añadir mismos campos/setters con `Income`.
   - Modificar `UpdateAsync`, `RemoveAsync`, `GetByIdAsync`.
4. Ejecutar `dotnet build -c Debug --nologo`.

**Criterios de verificación**
- Build exit 0, 0 warnings, 0 errors.
- Contenedor resuelve los 3 use cases nuevos.
- Fakes compilan; `AddedExpenses`/`AddedIncomes` se mantienen intactos.

**Notas**
- No registrar `IValidator<DeleteTransactionCommand>` (D-7).
- No modificar `ServiceCollectionExtensions.cs` (DI de repos ya registrado).
- Los fakes son test doubles; no añadir listas de captura innecesarias.

---

## T-007 — Crear tests unitarios

**AC refs:** AC-13

**Pre-condiciones**
- T-006 completada (fakes extendidos, DI registrado).
- `FakeClock.cs` y `NullLogger<>.Instance` disponibles.

**Pasos**
1. Crear `tests/ControlDeFinanzasIA.Tests.Unit/Application/Expenses/UpdateExpense/UpdateExpenseUseCaseTests.cs`:
   - Namespace `ControlDeFinanzasIA.Tests.Unit.Application.Expenses.UpdateExpense` (C-3).
   - Fixture con `FakeClock`, `FakeExpenseRepository`, `UpdateExpenseValidator`, `UpdateExpenseUseCase`, `NullLogger<>`.
   - Helper `SeedExpense()` y `CreateValidCommand(Guid id)`.
   - ≥3 tests; ~6 planeados (T-E1..T-E7 del §6 de la spec).
2. Crear `tests/ControlDeFinanzasIA.Tests.Unit/Application/Incomes/UpdateIncome/UpdateIncomeUseCaseTests.cs`:
   - Namespace `ControlDeFinanzasIA.Tests.Unit.Application.Incomes.UpdateIncome`.
   - Fixture con `FakeIncomeRepository`, `UpdateIncomeValidator`, `UpdateIncomeUseCase`.
   - ≥3 tests; ~6 planeados (T-I1..T-I6).
3. Crear `tests/ControlDeFinanzasIA.Tests.Unit/Application/Transactions/DeleteTransaction/DeleteTransactionUseCaseTests.cs`:
   - Namespace `ControlDeFinanzasIA.Tests.Unit.Application.Transactions.DeleteTransaction`.
   - Fixture con ambos fakes + `NullLogger<DeleteTransactionUseCase>`.
   - ≥4 tests; ~5 planeados (T-D1..T-D5).
4. Ejecutar:
   ```powershell
   dotnet test -c Debug --no-build --nologo --filter "FullyQualifiedName~UpdateExpenseUseCase|FullyQualifiedName~UpdateIncomeUseCase|FullyQualifiedName~DeleteTransactionUseCase"
   ```

**Criterios de verificación**
- Build exit 0, 0 warnings, 0 errors.
- ≥10 tests pasan, 0 fallos.
- Aserciones con `Assert.True`, `Assert.False`, `Assert.Equal`, `Assert.Null`, `Assert.Contains`, `Assert.StartsWith`.

**Notas**
- Pure xUnit `Assert.*`; sin FluentAssertions; sin mocking library.
- Para Delete, usar técnica de disambiguación: configurar `Result` distintivo en el repo que no debe invocarse.
- Para Update, asertar mutación in-place de la entidad sembrada.

---

## T-008 — Verificación final, formato y cierre

**AC refs:** AC-14, AC-15

**Pre-condiciones**
- T-001..T-007 completadas.

**Pasos**
1. `dotnet build -c Release --nologo` → 0 warnings, 0 errors.
2. `dotnet build -c Debug --nologo` → 0 warnings, 0 errors.
3. `dotnet test -c Debug --no-build --nologo --filter "FullyQualifiedName~UpdateExpenseUseCase|FullyQualifiedName~UpdateIncomeUseCase|FullyQualifiedName~DeleteTransactionUseCase"` → todos pasan.
4. `dotnet test -c Debug --no-build --nologo --filter "FullyQualifiedName~ArchitectureTests"` → 11/11.
5. `dotnet test -c Debug --no-build --nologo` → suite completa verde (baseline + ~14-15 nuevos; reportar cuenta exacta).
6. `dotnet format style --verify-no-changes` → exit 0.
7. Verificar scope (AC-15):
   - No se modificó `Expense.cs`, `Income.cs`, `IExpenseRepository.cs`, `IIncomeRepository.cs`.
   - No nuevas migraciones.
   - No UI salvo `Bootstrap.cs`.
   - No `.csproj`, `Directory.*.props`.
8. Marcar todas las tareas `[x]`.

**Criterios de verificación**
- Todos los gates anteriores exitosos.
- Sin archivos fuera de scope en el diff.

**Notas**
- Reportar conteos exactos observados de tests.
- No renombrar spec a `.done.md`; eso corresponde a `sdd-archive`.

---

## Orden de ejecución recomendado

**Secuencial estricto con dependencias:**

```text
T-001 → T-002 ─┐
T-003 → T-004 ─┼─ T-006 ── T-007 ── T-008
T-005 ─────────┘
```

T-001/T-003/T-005 son independientes entre sí. T-002 depende de T-001; T-004 depende de T-003. T-006 depende de T-002, T-004 y T-005. T-007 depende de T-006. T-008 cierra.

---

## Riesgos y mitigaciones

| # | Riesgo | Prob. | Impacto | Mitigación |
| --- | --- | --- | --- | --- |
| R-1 | **C-1/C-2:** usar formas del contrato original (`Expense.Update` 7 args, `UpdateExpenseCommand.CategoryId` `Guid?`) que no compilan. | Alta (resuelta) | Alto | Spec/proposal fijan las correcciones; T-002/T-004 incluyen las llamadas exactas. |
| R-2 | **Update no surfacea `FutureDateWarning`** (Register sí). | Media (decisión) | Bajo | D-9: aceptado hasta 4-008; `Result.Success(null!)` sin payload; documentar en XML doc. |
| R-3 | **`UpdateIncomeUseCase` resetea `Source` a `null`**. | Media (decisión) | Bajo | D-6/Q4: pasar `null`; test T-I5 blinda el comportamiento. |
| R-4 | **Conflicto de idioma en mensajes de validador**: bosquejo AC-3 en inglés vs espejo Register en español. | Media (decisión) | Bajo | D-ML: usar español en validadores; inglés en `NotFound`/guards de Delete. |
| R-5 | **Fakes actuales retornan defaults hardcodeados** → tests NotFound/propagación no se pueden escribir sin extender. | Alta (verificado) | Bajo | AC-11/AC-12: extender fakes con `SetUpdateResult`/`SetRemoveResult`/`SetGetByIdResult` (T-006). |
| R-6 | **C-3:** rutas de test del contrato (`Application/UseCases/...`) no coinciden con convención del repo. | Alta (verificado) | Bajo | Usar `Application/<Aggregate>/<UseCase>/` (sin `UseCases`) en T-007. |
| R-7 | **`Money.Create`/`entity.Update` lanzan `ArgumentException`** que escapa sin `try/catch`. | Media | Medio | D-4 + T-002/T-004 envuelven en `try/catch (ArgumentException)` → `"DomainInvariant"`. |
| R-8 | **FluentValidation 11 lanza `AsyncValidatorInvokedSynchronouslyException`**. | Baja | Medio | D-2: reglas sync y `_validator.Validate(command)`. |
| R-9 | **`TransactionType` fuera de rango** cae en `default` sin handling. | Baja | Bajo | D-8: `default` retorna `"Validation"`; test T-D5 cubre. |
| R-10 | **Scope creep hacia 4-008** (DTOs, `Source` en command, UI, `FutureDateWarning`). | Media | Alto | AC-15/§7 lo prohíben; verificar en T-008. |

---

## Definition of Done (DoD)

- [x] AC-1: `TransactionType` enum creado en `Domain.Enums`. (T-001)
- [x] AC-2: `UpdateExpenseCommand` creado con `Guid CategoryId` required. (T-001)
- [x] AC-3: `UpdateExpenseValidator` con 6 reglas sync y mensajes en español. (T-001)
- [x] AC-4: `UpdateExpenseUseCase` llama `expense.Update` con 6 args. (T-002)
- [x] AC-5: `UpdateIncomeCommand` creado con `Guid? CategoryId`. (T-003)
- [x] AC-6: `UpdateIncomeValidator` con 6 reglas sync, `CategoryId` condicional. (T-003)
- [x] AC-7: `UpdateIncomeUseCase` llama `income.Update` con 7 args y `source: null`. (T-004)
- [x] AC-8: `DeleteTransactionCommand` creado con `Id` + `Type`. (T-005)
- [x] AC-9: `DeleteTransactionUseCase` con guard inline + dispatch + `default`. (T-005)
- [x] AC-10: DI registrado en `Bootstrap.cs` (3 use cases + 2 validators). (T-006)
- [x] AC-11: `FakeExpenseRepository` extendido con setters configurables. (T-006)
- [x] AC-12: `FakeIncomeRepository` extendido con setters configurables. (T-006)
- [x] AC-13: ≥10 tests unitarios verdes. (T-007)
- [x] AC-14: Build, tests, NetArchTest y format exit 0. (T-008)
- [x] AC-15: Sin scope creep. (T-008)

---

## Referencias

- `spec/features/phase-04/F-005-update-delete-use-cases.spec.md`
- `.atl/sdd/f-005p4-update-delete-use-cases/proposal.md`
- `tasks/phase-04/F-003-register-income-use-case.tasks.md`
- `tasks/phase-04/F-004-tag-entity-and-many-to-many.tasks.md`
- `spec/phases/phase-04/features.md` §RF-7, §RF-8
- `src/.../Domain/Transactions/Expense.cs` (líneas 161-167 — `Update` 6-param)
- `src/.../Domain/Transactions/Income.cs` (líneas 168-175 — `Update` 7-param)
- `src/.../UI/Composition/Bootstrap.cs`
- `tests/.../TestDoubles/FakeExpenseRepository.cs`
- `tests/.../TestDoubles/FakeIncomeRepository.cs`
- `AGENTS.md` — convenciones de arquitectura, NetArchTest R-1..R-5 y formato.
