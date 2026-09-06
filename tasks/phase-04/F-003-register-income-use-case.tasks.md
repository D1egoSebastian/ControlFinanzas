# Tareas — F-003 (Phase 4): `RegisterIncomeUseCase` + Command + Validator + Result + DI

> **Spec:** `spec/features/phase-04/F-003-register-income-use-case.spec.md`
> **change_id:** `f-003p4-register-income-use-case`
> **Estado:** 🟢 PR-1 COMPLETO — PR-2 COMPLETO
> **Fecha:** 2026-08-27
> **Fuentes de verdad:** spec F-003 P4, proposal `.atl/sdd/f-003p4-register-income-use-case/proposal.md`,
> `tasks/phase-03/F-004-register-expense-use-case.tasks.md` (espejo),
> `tasks/phase-04/F-002-income-repository.tasks.md` (precedente).
> **Dependencias:** F-001 Phase 4 ✅ (`Income`, `IncomeConfiguration`, migración), F-002 Phase 4 ✅ (`IIncomeRepository`, `IncomeRepository`).

---

## Pronóstico de carga de review

| Field | Value |
|-------|-------|
| Estimated changed lines | ~505 |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1: T-001..T-005 (producción + DI) ~245 líneas; PR 2: T-006..T-007 (fakes + tests) ~260 líneas |
| Delivery strategy | auto-forecast |
| Chain strategy | stacked-to-main |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: High

### Unidades de trabajo sugeridas

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Crear `RegisterIncomeCommand`, `RegisterIncomeResult`, `RegisterIncomeValidator`, `RegisterIncomeUseCase` y registrar DI en `Bootstrap.cs` | PR 1 | Base = `main`; ~245 líneas; no tests |
| 2 | Crear `FakeIncomeRepository` + `RegisterIncomeUseCaseTests` (12 tests) | PR 2 | Base = PR 1; `FakeClock` ya existe |

---

## Resumen de tareas

| ID | Tarea | Estimación diff | Depende de | Verificación | Estado |
| --- | --- | --- | --- | --- | --- |
| T-001 | Crear `RegisterIncomeCommand.cs` | +30 / -0 | ninguna | `dotnet build` exit 0 | [x] |
| T-002 | Crear `RegisterIncomeResult.cs` | +20 / -0 | ninguna | `dotnet build` exit 0 | [x] |
| T-003 | Crear `RegisterIncomeValidator.cs` (5 reglas sync) | +50 / -0 | T-001 | `dotnet build` exit 0 | [x] |
| T-004 | Crear `RegisterIncomeUseCase.cs` (D-1..D-8) | +140 / -0 | T-001..T-003 | `dotnet build` exit 0 | [x] |
| T-005 | Registrar use case + validator en `Bootstrap.cs` | +5 / -0 | T-003, T-004 | `dotnet build` exit 0; NetArchTest R-3/R-4 | [x] |
| T-006 | Crear `FakeIncomeRepository.cs` | +80 / -0 | ninguna | `dotnet build` exit 0 | [x] |
| T-007 | Crear `RegisterIncomeUseCaseTests.cs` (12 tests) | +180 / -0 | T-004, T-006 | 12 passed | [x] |
| T-008 | Verificación final, formato y cierre | 0 | T-001..T-007 | Todos los gates verdes | [x] |

**Total estimado:** ~505 líneas cambiadas.

---

## T-001 — Crear `RegisterIncomeCommand.cs`

**AC refs:** AC-1

**Pre-condiciones**
- F-001 Phase 4 archivada (`Income` existe con `Create` de 7 params).
- F-002 Phase 4 archivada (`IIncomeRepository` inyectable).

**Pasos**
1. Crear `src/ControlDeFinanzasIA.Application/UseCases/Incomes/RegisterIncome/RegisterIncomeCommand.cs`.
2. File-scoped namespace `ControlDeFinanzasIA.Application.UseCases.Incomes.RegisterIncome`.
3. Añadir `using ControlDeFinanzasIA.Domain.Enums;`.
4. Declarar:
   ```csharp
   public sealed record RegisterIncomeCommand(
       Guid? CategoryId,
       DateOnly Date,
       decimal Amount,
       Currency Currency,
       string Description,
       string? Note);
   ```
5. XML doc en el tipo documentando `CategoryId` nullable y campos omitidos (`Id`, `Source`, audit).
6. Ejecutar `dotnet build -c Debug --nologo`.

**Criterios de verificación**
- Build exit 0, 0 warnings, 0 errors.
- `sealed record`, 6 props, `CategoryId` es `Guid?`, sin `Id`, sin `Source`.

**Notas**
- `CategoryId` nullable es la diferencia clave vs `RegisterExpenseCommand`.
- `Currency` vive en `ControlDeFinanzasIA.Domain.Enums`.

---

## T-002 — Crear `RegisterIncomeResult.cs`

**AC refs:** AC-2

**Pre-condiciones**
- Ninguna (no depende de otros archivos nuevos).

**Pasos**
1. Crear `src/ControlDeFinanzasIA.Application/UseCases/Incomes/RegisterIncome/RegisterIncomeResult.cs`.
2. File-scoped namespace `ControlDeFinanzasIA.Application.UseCases.Incomes.RegisterIncome`.
3. Declarar:
   ```csharp
   public sealed record RegisterIncomeResult(
       Guid IncomeId,
       IReadOnlyList<string> Warnings);
   ```
4. XML doc indicando que `Warnings` nunca es null y que `IncomeDto` es scope de F-008.
5. Ejecutar `dotnet build -c Debug --nologo`.

**Criterios de verificación**
- Build exit 0, 0 warnings, 0 errors.
- `sealed record` con `Guid IncomeId` e `IReadOnlyList<string> Warnings`.

**Notas**
- No referenciar `Application.DTOs`; F-003 devuelve sólo el `IncomeId`.

---

## T-003 — Crear `RegisterIncomeValidator.cs`

**AC refs:** AC-3

**Pre-condiciones**
- T-001 completada (`RegisterIncomeCommand` existe).

**Pasos**
1. Crear `src/ControlDeFinanzasIA.Application/UseCases/Incomes/RegisterIncome/RegisterIncomeValidator.cs`.
2. File-scoped namespace; `using FluentValidation;` y `using ControlDeFinanzasIA.Domain.Enums;`.
3. Declarar `public sealed class RegisterIncomeValidator : AbstractValidator<RegisterIncomeCommand>`.
4. Definir 5 reglas sync con `.WithMessage(...)` por regla:
   - `CategoryId`: `NotEqual(Guid.Empty).When(c => c.CategoryId is not null)`.
   - `Amount`: `GreaterThan(0)`.
   - `Description`: `NotEmpty()`, `MaximumLength(200)`, `Must(d => d.Trim().Length > 0)`.
   - `Note`: `MaximumLength(500).When(c => c.Note is not null)`.
   - `Currency`: `IsInEnum()`.
5. XML doc en la clase documentando reglas sync y `CategoryId` nullable.
6. Ejecutar `dotnet build -c Debug --nologo`.

**Criterios de verificación**
- Build exit 0, 0 warnings, 0 errors.
- 5 reglas presentes; sin `MustAsync`/`CustomAsync`; sin techo para `Date`.

**Notas**
- OPEN-1 resuelto: incluir la 5ª regla `CategoryId` condicional.
- Patrón per-rule `.WithMessage()` espejo de `RegisterExpenseValidator`.

---

## T-004 — Crear `RegisterIncomeUseCase.cs`

**AC refs:** AC-4, AC-5, AC-6, AC-11, AC-12

**Pre-condiciones**
- T-001, T-002, T-003 completadas.
- `Income.Create` verificado con 7 params: `(Guid? categoryId, DateOnly date, Money amount, string description, string? note, string? source, DateTime createdAt)`.
- `Result` non-generic verificado.

**Pasos**
1. Crear `src/ControlDeFinanzasIA.Application/UseCases/Incomes/RegisterIncome/RegisterIncomeUseCase.cs`.
2. File-scoped namespace; `using` al tope:
   - `ControlDeFinanzasIA.Application.Abstractions`
   - `ControlDeFinanzasIA.Application.Common`
   - `ControlDeFinanzasIA.Domain.Transactions`
   - `ControlDeFinanzasIA.Domain.ValueObjects`
   - `FluentValidation`
   - `FluentValidation.Results`
   - `Microsoft.Extensions.Logging`
3. Declarar `public sealed class RegisterIncomeUseCase`.
4. Campos readonly: `_incomeRepository`, `_clock`, `_validator`, `_logger`.
5. Constructor con 4 parámetros inyectados.
6. Implementar `public async Task<Result> ExecuteAsync(RegisterIncomeCommand command, CancellationToken ct = default)`:
   - `ArgumentNullException.ThrowIfNull(command);`
   - `ValidationResult validation = _validator.Validate(command);` (sync, D-4).
   - Si inválido, agrupar errores por `PropertyName` → `Result.Failure("Validation", "El comando para registrar el ingreso no es válido.", details)` (D-5).
   - `DateTime audit = _clock.UtcNow;` (leer una vez).
   - `var warnings = new List<string>();` + `"FutureDateWarning"` si `command.Date > DateOnly.FromDateTime(audit)` (D-8, OPEN-3).
   - `try { var money = Money.Create(command.Amount, command.Currency); income = Income.Create(command.CategoryId, command.Date, money, command.Description, command.Note, null, audit); } catch (ArgumentException ex) { _logger.LogWarning(ex, "Domain invariant violation while registering income."); return Result.Failure("DomainInvariant", ex.Message); }` (D-1, D-6, D-7, OPEN-2).
   - `Result addResult = await _incomeRepository.AddAsync(income, ct);`.
   - Si `!addResult.IsSuccess`, retornar `addResult` (propaga `"CategoryNotFound"`, AC-12).
   - `return Result.Success(new RegisterIncomeResult(income.Id, warnings));` (D-2, R-7).
7. XML doc en clase y `ExecuteAsync` documentando el cast `(RegisterIncomeResult)result.Value!`.
8. Ejecutar `dotnet build -c Debug --nologo`.

**Criterios de verificación**
- Build exit 0, 0 warnings, 0 errors.
- Firma `Task<Result>` non-generic; no referencia `Result<T>`.
- `Income.Create` recibe `Money`, `source: null`, `createdAt: audit`.

**Notas**
- **CRÍTICO:** usar `income.Id` local para el result, NO `addResult.Value` (R-7).
- **CRÍTICO:** pasar `Description`/`Note` as-is, sin `.Trim()` (D-7).
- `DateOnly.FromDateTime(audit)` es la API real; `_clock.UtcNow.DateOnly` no existe (D-8).

---

## T-005 — Registrar use case + validator en `Bootstrap.cs`

**AC refs:** AC-7

**Pre-condiciones**
- T-003 (`RegisterIncomeValidator`) y T-004 (`RegisterIncomeUseCase`) completadas.
- `Bootstrap.cs` verificado con registros de `RegisterExpenseUseCase` y `IValidator<RegisterExpenseCommand>`.

**Pasos**
1. Abrir `src/ControlDeFinanzasIA.UI/Composition/Bootstrap.cs`.
2. Añadir `using ControlDeFinanzasIA.Application.UseCases.Incomes.RegisterIncome;`.
3. Tras `services.AddTransient<RegisterExpenseUseCase>();`, añadir `services.AddTransient<RegisterIncomeUseCase>();`.
4. Tras `services.AddTransient<IValidator<RegisterExpenseCommand>, RegisterExpenseValidator>();`, añadir `services.AddTransient<IValidator<RegisterIncomeCommand>, RegisterIncomeValidator>();`.
5. Actualizar XML doc de `ConfigureServices` para mencionar use case de ingresos.
6. Ejecutar `dotnet build -c Debug --nologo`.

**Criterios de verificación**
- Build exit 0, 0 warnings, 0 errors.
- NetArchTest R-3/R-4 siguen verdes.
- `ServiceCollectionExtensions.cs` no se modificó.

**Notas**
- D-3: DI de use cases vive en `Bootstrap.cs` (UI), no en Infrastructure.

---

## T-006 — Crear `FakeIncomeRepository.cs`

**AC refs:** AC-8 (infraestructura de tests)

**Pre-condiciones**
- `FakeClock.cs` ya existe en `tests/ControlDeFinanzasIA.Tests.Unit/TestDoubles/`.
- `IIncomeRepository` expone 6 métodos (F-002).

**Pasos**
1. Crear `tests/ControlDeFinanzasIA.Tests.Unit/TestDoubles/FakeIncomeRepository.cs`.
2. File-scoped namespace `ControlDeFinanzasIA.Tests.Unit.TestDoubles`.
3. Declarar `public sealed class FakeIncomeRepository : IIncomeRepository`.
4. Implementar:
   - `private readonly List<Income> _addedIncomes = new();`
   - `private Result _addResult = Result.Success(null!);`
   - `public IReadOnlyList<Income> AddedIncomes => _addedIncomes;`
   - `public void SetAddResult(Result result) => _addResult = result;`
   - `AddAsync`: añade a `_addedIncomes` y retorna `_addResult`.
   - `UpdateAsync`, `RemoveAsync`, `GetByIdAsync`, `GetAllAsync`, `GetByFilterAsync` retornan defaults (espejo `FakeExpenseRepository`).
5. Ejecutar `dotnet build -c Debug --nologo`.

**Criterios de verificación**
- Build exit 0, 0 warnings, 0 errors.
- 6 métodos implementados; `AddedIncomes` captura entidades; `SetAddResult` permite simular fallos.

**Notas**
- No crear `FakeClock`; ya existe.
- Los 5 métodos no-Add retornan defaults (no `NotImplementedException`), consistentes con `FakeExpenseRepository`.

---

## T-007 — Crear `RegisterIncomeUseCaseTests.cs`

**AC refs:** AC-8

**Pre-condiciones**
- T-004 (`RegisterIncomeUseCase`) y T-006 (`FakeIncomeRepository`) completadas.
- `FakeClock` ya existe.

**Pasos**
1. Crear `tests/ControlDeFinanzasIA.Tests.Unit/Application/UseCases/Incomes/RegisterIncome/RegisterIncomeUseCaseTests.cs`.
2. File-scoped namespace `ControlDeFinanzasIA.Tests.Unit.Application.UseCases.Incomes.RegisterIncome`.
3. `using` al tope: `ControlDeFinanzasIA.Application.Abstractions`, `ControlDeFinanzasIA.Application.Common`, `ControlDeFinanzasIA.Application.UseCases.Incomes.RegisterIncome`, `ControlDeFinanzasIA.Domain.Enums`, `ControlDeFinanzasIA.Domain.Transactions`, `ControlDeFinanzasIA.Tests.Unit.TestDoubles`, `Microsoft.Extensions.Logging.Abstractions`, `Xunit`.
4. Fixture:
   - `_validCategoryId = new Guid("11111111-1111-1111-1111-000000000001")`.
   - `_clock = new FakeClock { UtcNow = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc) };`
   - `_repository = new FakeIncomeRepository();`
   - `_validator = new RegisterIncomeValidator();`
   - `_useCase = new RegisterIncomeUseCase(_repository, _clock, _validator, NullLogger<RegisterIncomeUseCase>.Instance);`
5. Helper `CreateValidCommand()` → `new(_validCategoryId, DateOnly.FromDateTime(_clock.UtcNow), 100m, Currency.DOP, "Sueldo", null)`.
6. Implementar 12 tests (pure xUnit `Assert.*`, sin FluentAssertions):
   - `ExecuteAsync_WithValidCommand_ReturnsSuccessWithIncomeId`
   - `ExecuteAsync_WithZeroAmount_ReturnsValidationFailure`
   - `ExecuteAsync_WithNegativeAmount_ReturnsValidationFailure`
   - `ExecuteAsync_WithEmptyDescription_ReturnsValidationFailure` (Theory: `""`, `"   "`, `"\t"`)
   - `ExecuteAsync_WithDescriptionOver200Chars_ReturnsValidationFailure`
   - `ExecuteAsync_WithNoteOver500Chars_ReturnsValidationFailure`
   - `ExecuteAsync_WithInvalidCurrencyEnum_ReturnsValidationFailure`
   - `ExecuteAsync_WithFutureDate_ReturnsSuccessWithFutureDateWarning`
   - `ExecuteAsync_WithValidCategory_PersistsAndReturnsSuccess`
   - `ExecuteAsync_WithUnknownCategory_ReturnsCategoryNotFoundFailure`
   - `ExecuteAsync_WithNullCategory_PersistsWithoutCategory`
   - `ExecuteAsync_AddAsyncSucceeds_AuditDatesFromClock`
7. Ejecutar `dotnet test -c Debug --no-build --nologo --filter "FullyQualifiedName~RegisterIncomeUseCase"`.

**Criterios de verificación**
- Build exit 0, 0 warnings, 0 errors.
- 12 tests pasan, 0 fallos.
- Aserciones con `Assert.True`, `Assert.False`, `Assert.Equal`, `Assert.NotEqual`, `Assert.NotNull`, `Assert.IsType`, `Assert.Contains`, `Assert.Empty`, `Assert.Single`.

**Notas**
- Verificar que `AddedIncomes[0].CategoryId == null` en T-11.
- Verificar que `AddedIncomes[0].CreatedAt == _clock.UtcNow` en T-12.

---

## T-008 — Verificación final, formato y cierre

**AC refs:** AC-9, AC-10

**Pre-condiciones**
- T-001..T-007 completadas.

**Pasos**
1. `dotnet build -c Release --nologo` → 0 warnings, 0 errors.
2. `dotnet build -c Debug --nologo` → 0 warnings, 0 errors.
3. `dotnet test -c Debug --no-build --nologo --filter "FullyQualifiedName~RegisterIncomeUseCase"` → 12 passed.
4. `dotnet test -c Debug --no-build --nologo --filter "FullyQualifiedName~ArchitectureTests"` → 11/11 passed.
5. `dotnet test -c Debug --no-build --nologo` → suite completa verde (~275 passed; 1 fallo pre-existente sin cambios).
6. `dotnet format style --verify-no-changes` → exit 0.
7. Verificar scope: no se modificaron `Income.cs`, `Money.cs`, `Currency.cs`, `Result.cs`, `IIncomeRepository.cs`, `IncomeRepository.cs`, `Expense*`, migraciones, `ServiceCollectionExtensions.cs`, UI salvo `Bootstrap.cs`.
8. Marcar todas las tareas `[x]`.

**Criterios de verificación**
- Todos los gates anteriores exitosos.
- Sin archivos fuera de scope en el diff.

**Notas**
- Reportar conteos exactos observados de tests.
- No renombrar spec a `.done.md`; eso corresponde a `sdd-archive`.

---

## Orden de ejecución recomendado

**Secuencial estricto:**

```text
T-001 → T-002 → T-003 → T-004 → T-005 → T-006 → T-007 → T-008
```

T-001/T-002/T-003 son independientes entre sí, pero T-004 depende de todos; T-005 depende de T-003 y T-004; T-006 puede crearse en paralelo con T-004; T-007 necesita T-004 y T-006; T-008 cierra.

---

## Riesgos y mitigaciones

| # | Riesgo | Prob. | Impacto | Mitigación |
| --- | --- | --- | --- | --- |
| R-1 | Usar formas del contrato original en vez de D-1..D-8 (`Income.Create` 7 params, `Result` non-generic). | Alta (resuelta) | Alto | La spec/proposal fijan las formas corregidas; T-004 incluye el cuerpo exacto. |
| R-2 | Omitir la 5ª regla `CategoryId` → `Guid.Empty` escapa como `"DomainInvariant"`. | Media | Bajo | OPEN-1 resuelto; T-003 incluye la regla condicional. |
| R-3 | `IncomeRepository.AddAsync` con `CategoryId == null` y se añade guard defensivo incorrecto. | Baja | Medio | T-11 verifica rama null; confiar en F-002. |
| R-4 | Pasar `Description.Trim()` o setear `source = "manual"` (scope creep F-005). | Media | Bajo | D-7/OPEN-2 fijan as-is y `null`; T-004 lo documenta. |
| R-5 | Registrar DI en `ServiceCollectionExtensions.cs` rompiendo R-3/R-4. | Media | Alto | D-3 + T-005 fijan `Bootstrap.cs`; `ServiceCollectionExtensions.cs` en lista de no tocar. |
| R-6 | Excepción de dominio escapa por falta de `try/catch(ArgumentException)`. | Media | Medio | D-6 + T-004 envuelven `Money.Create`/`Income.Create`. |
| R-7 | Construir `RegisterIncomeResult` desde `addResult.Value` (repo devuelve `null!`). | Media | Alto | AC-6/T-004 usan `income.Id` local; T-9 lo verifica. |
| R-8 | `mem_save` no disponible para persistir en Engram. | Alta (verificado) | Bajo | Respaldo en filesystem; el orquestador backfillará a Engram. |
| R-9 | `AsyncValidatorInvokedSynchronouslyException` por regla async. | Baja | Medio | D-4: reglas sync y `_validator.Validate(command)`. |
| R-10 | Scope creep hacia F-005/F-008/tests aislados del validador. | Media | Alto | AC-10/§7 lo prohíben; verificar en T-008. |

---

## Definition of Done (DoD)

- [x] AC-1: `RegisterIncomeCommand` creado con 6 props y `Guid? CategoryId`. (T-001)
- [x] AC-2: `RegisterIncomeResult` creado con `IncomeId` + `Warnings`. (T-002)
- [x] AC-3: `RegisterIncomeValidator` con 5 reglas sync incluyendo `CategoryId`. (T-003)
- [x] AC-4: `RegisterIncomeUseCase` es `sealed` con 4 deps. (T-004)
- [x] AC-5: `ExecuteAsync` retorna `Task<Result>` non-generic. (T-004)
- [x] AC-6: Lógica D-1..D-8 correcta; propaga `"CategoryNotFound"`. (T-004)
- [x] AC-7: DI registrado en `Bootstrap.cs`. (T-005)
- [x] AC-8: `FakeIncomeRepository` + 12 tests unitarios verdes. (T-006, T-007)
- [x] AC-9: Build, tests, NetArchTest y format exit 0. (T-008)
- [x] AC-10: Sin scope creep. (T-008)
- [x] AC-11: Fecha futura es warning, no fallo. (T-004)
- [x] AC-12: Propagación de `"CategoryNotFound"` sin re-envoltorio. (T-007 T-10)

---

## Referencias

- `spec/features/phase-04/F-003-register-income-use-case.spec.md`
- `.atl/sdd/f-003p4-register-income-use-case/proposal.md`
- `tasks/phase-03/F-004-register-expense-use-case.tasks.md`
- `tasks/phase-04/F-002-income-repository.tasks.md`
- `spec/phases/phase-04/features.md` §RF-6
