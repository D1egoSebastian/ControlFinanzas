# Tareas — F-004: Application: DTOs + `RegisterExpenseValidator` + `RegisterExpenseUseCase`

> **Spec:** `spec/features/phase-03/F-004-register-expense-use-case.spec.md`
> **Fuentes de verdad:** la spec, `docs/vision.md` §4.1,
> `docs/architecture.md` §7, §19 (ADR-007 `IClock`),
> `docs/roadmap.md` §Fase 3,
> `spec/constitution/tech-stack.md` (.NET 8, FluentValidation 11.10.0),
> `spec/constitution/mission.md`,
> `spec/phases/phase-03/features.md`,
> `spec/features/phase-03/F-001-expense-domain-entity.done.md`,
> `spec/features/phase-03/F-003-expense-repository.done.md`,
> `spec/features/phase-01/F-005-comuntypes-result-pagedresult.done.md`
> (`Result` no-genérico),
> `spec/features/phase-01/F-006-contrato-interfaces-repo.done.md`,
> `spec/features/phase-01/F-004-Abstracciones-clock-pathresolver.done.md`
> (`IClock`; `FakeClock` en `Tests.Unit/TestDoubles/`).
>
> Cada tarea es implementable en menos de 2 horas. F-004 **sólo** añade tipos
> a `Application`, 2 registros a la composition root de `UI` y tests unitarios.
> NO modifica `Domain`, `Infrastructure` (salvo DI), `Result`,
> `IExpenseRepository`, `IClock` ni `Expense.cs`.

---

## Pronóstico de carga de review

| Field | Value |
|-------|-------|
| Estimated changed lines | ~380–420 |
| 400-line budget risk | Medium |
| Chained PRs recommended | Yes |
| Suggested split | PR 1: DTOs + command + validator + result + use case + DI (~220 líneas); PR 2: tests unitarios + verificación (~180 líneas) |
| Delivery strategy | auto-forecast |
| Chain strategy | stacked-to-main |

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: Medium

### Unidades de trabajo sugeridas

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Crear `ExpenseDto`, `ExpenseExtensions`, `RegisterExpenseCommand`, `RegisterExpenseValidator`, `RegisterExpenseResult`, `RegisterExpenseUseCase` y registrarlo en `Bootstrap` | PR 1 | Base = `main`; ~6 archivos nuevos + 1 modificado |
| 2 | Crear `RegisterExpenseUseCaseTests` y `RegisterExpenseValidatorTests`; ejecutar verificación final | PR 2 | Base = PR 1; pure xUnit `Assert.*`; ≥10 tests nuevos |

---

## Resumen

F-004 introduce el **primer use case del proyecto** (`RegisterExpenseUseCase`)
y el patrón que replicarán los siguientes: `Command` inmutable, validador
FluentValidation 11.10.0 **sync**, mapeo manual a `Expense` vía
`Money.Create` + `Expense.Create`, persistencia delegada en
`IExpenseRepository.AddAsync` (F-003), auditoría desde `IClock.UtcNow`
(ADR-007) y retorno `Task<Result>` no-genérico envolviendo
`RegisterExpenseResult` (DTO + warnings).

Entregables:

- `src/ControlDeFinanzasIA.Application/DTOs/ExpenseDto.cs` — `sealed record`, 9 campos.
- `src/ControlDeFinanzasIA.Application/DTOs/ExpenseExtensions.cs` — `ToDto(this Expense)`.
- `src/ControlDeFinanzasIA.Application/UseCases/Expenses/RegisterExpense/` —
  `RegisterExpenseCommand`, `RegisterExpenseValidator`,
  `RegisterExpenseUseCase`, `RegisterExpenseResult`.
- `src/ControlDeFinanzasIA.UI/Composition/Bootstrap.cs` — 2 registros `Transient`.
- 2 archivos de tests unitarios con pure xUnit `Assert.*`.
- `dotnet build` limpio; `dotnet test` ≈ **187 verdes**; NetArchTest R-1..R-5 verdes.

---

## Resumen de tareas

| ID | Tarea | Estimación diff | Depende de | Verificación |
| --- | --- | --- | --- | --- |
| T-001 | Crear `ExpenseDto.cs` | +25 / -0 | ninguna | `dotnet build` exit 0 | [x] |
| T-002 | Crear `ExpenseExtensions.cs` con `ToDto` | +20 / -0 | T-001 | `dotnet build` exit 0 | [x] |
| T-003 | Crear `RegisterExpenseCommand.cs` | +20 / -0 | ninguna | `dotnet build` exit 0 | [x] |
| T-004 | Crear `RegisterExpenseValidator.cs` con 6 reglas sync | +45 / -0 | T-003 | `dotnet build` exit 0 | [x] |
| T-005 | Crear `RegisterExpenseResult.cs` | +15 / -0 | T-001 | `dotnet build` exit 0 | [x] |
| T-006 | Implementar `RegisterExpenseUseCase.cs` | +90 / -0 | T-001..T-005 | `dotnet build` exit 0 | [x] |
| T-007 | Registrar use case + validator en `Bootstrap` | +2 / -0 | T-004, T-006 | `dotnet build` exit 0 | [x] |
| T-008 | Crear `RegisterExpenseUseCaseTests.cs` (≥ 6 tests) | +120 / -0 | T-006 | `dotnet test --filter "FullyQualifiedName~RegisterExpenseUseCaseTests"` ≥ 6 passed | [x] |
| T-009 | Crear `RegisterExpenseValidatorTests.cs` (≥ 4 tests) | +60 / -0 | T-004 | `dotnet test --filter "FullyQualifiedName~RegisterExpenseValidatorTests"` ≥ 4 passed | [x] |
| T-010 | Verificación final, formato y cierre | +2 / -2 | T-001..T-009 | `dotnet test` 187 passed; `dotnet format style --verify-no-changes` exit 0 | [x] |

**Total estimado:** ~9–11 horas. T-006 y T-008 son las más densas; las demás
son mecánicas o verificación.

---

## T-001 — Crear `ExpenseDto.cs`

**Objetivo**
Crear el DTO de salida del use case como `public sealed record` con 9 campos.

**Pasos**

1. Crear `src/ControlDeFinanzasIA.Application/DTOs/ExpenseDto.cs`.
2. File-scoped namespace `ControlDeFinanzasIA.Application.DTOs`.
3. Añadir `using ControlDeFinanzasIA.Domain.ValueObjects;`.
4. Declarar:
   ```csharp
   public sealed record ExpenseDto(
       Guid Id,
       Guid CategoryId,
       DateOnly Date,
       decimal Amount,
       Currency Currency,
       string Description,
       string? Note,
       DateTime CreatedAt,
       DateTime UpdatedAt);
   ```
5. XML doc en el record.
6. Ejecutar `dotnet build`.

**Verificación**

- `dotnet build "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --nologo`:
  exit 0, 0 warnings, 0 errors.
- Inspección: `sealed`, file-scoped namespace, 9 campos en orden, XML doc.

**Dependencias:** ninguna.
**Estimación:** S (< 1 h).
**Trazabilidad:** AC-1, §3, §8 AC-1, §10.

---

## T-002 — Crear `ExpenseExtensions.cs`

**Objetivo**
Crear la extensión de mapeo manual `Expense → ExpenseDto`.

**Pasos**

1. Crear `src/ControlDeFinanzasIA.Application/DTOs/ExpenseExtensions.cs`.
2. File-scoped namespace `ControlDeFinanzasIA.Application.DTOs`.
3. Añadir `using ControlDeFinanzasIA.Domain.Transactions;`.
4. Declarar `public static class ExpenseExtensions`.
5. Implementar:
   ```csharp
   public static ExpenseDto ToDto(this Expense expense)
       => new(
           expense.Id,
           expense.CategoryId,
           expense.Date,
           expense.Amount.Amount,
           expense.Amount.Currency,
           expense.Description,
           expense.Note,
           expense.CreatedAt,
           expense.UpdatedAt);
   ```
6. XML doc en clase y método.
7. Ejecutar `dotnet build`.

**Verificación**

- `dotnet build ... -c Release --nologo`: exit 0, 0 warnings, 0 errors.
- Inspección: mapea 9 campos incluyendo `Amount.Amount` y `Amount.Currency`.

**Dependencias:** T-001, F-001 de Fase 3.
**Estimación:** S (< 1 h).
**Trazabilidad:** AC-2, §3, §8 AC-2, §10.

---

## T-003 — Crear `RegisterExpenseCommand.cs`

**Objetivo**
Crear el comando inmutable de entrada, `sealed record` con 6 campos.

**Pasos**

1. Crear `src/ControlDeFinanzasIA.Application/UseCases/Expenses/RegisterExpense/RegisterExpenseCommand.cs`.
2. Crear la carpeta si no existe.
3. File-scoped namespace `ControlDeFinanzasIA.Application.UseCases.Expenses.RegisterExpense`.
4. Añadir `using ControlDeFinanzasIA.Domain.ValueObjects;`.
5. Declarar:
   ```csharp
   public sealed record RegisterExpenseCommand(
       Guid CategoryId,
       DateOnly Date,
       decimal Amount,
       Currency Currency,
       string Description,
       string? Note);
   ```
6. XML doc en el record.
7. Ejecutar `dotnet build`.

**Verificación**

- `dotnet build ... -c Release --nologo`: exit 0, 0 warnings, 0 errors.
- Inspección: `sealed record`, 6 campos exactos, sin `Id` ni audit.

**Dependencias:** ninguna.
**Estimación:** S (< 1 h).
**Trazabilidad:** AC-3, §6 DP-2, §8 AC-3.

---

## T-004 — Crear `RegisterExpenseValidator.cs`

**Objetivo**
Crear el validador FluentValidation 11.10.0 **sync** con las 6 reglas fijadas.

**Pasos**

1. Crear `src/ControlDeFinanzasIA.Application/UseCases/Expenses/RegisterExpense/RegisterExpenseValidator.cs`.
2. File-scoped namespace `ControlDeFinanzasIA.Application.UseCases.Expenses.RegisterExpense`.
3. Añadir `using FluentValidation;`.
4. Declarar `public sealed class RegisterExpenseValidator : AbstractValidator<RegisterExpenseCommand>`.
5. En el constructor definir:
   ```csharp
   RuleFor(c => c.CategoryId).NotEqual(Guid.Empty);
   RuleFor(c => c.Amount).GreaterThan(0);
   RuleFor(c => c.Description)
       .NotEmpty()
       .MaximumLength(200)
       .Must(d => d.Trim().Length > 0);
   RuleFor(c => c.Note)
       .MaximumLength(500)
       .When(n => n is not null);
   RuleFor(c => c.Currency).IsInEnum();
   // Date sin techo superior: fechas futuras permitidas (DP-1).
   ```
6. **No** usar `MustAsync`, `CustomAsync` ni `ValidateAsync` (DP-6).
7. XML doc en la clase.
8. Ejecutar `dotnet build`.

**Verificación**

- `dotnet build ... -c Release --nologo`: exit 0, 0 warnings, 0 errors.
- Inspección: 6 reglas presentes; `Description` rechaza whitespace-only;
  `Note` usa `.When(n => n is not null)`; `Currency` usa `IsInEnum()`;
  `Date` no tiene techo superior.

**Dependencias:** T-003, FluentValidation ya referenciado.
**Estimación:** S (< 1 h).
**Trazabilidad:** AC-4, §6 DP-1..DP-6, §8 AC-4, §9.2.

---

## T-005 — Crear `RegisterExpenseResult.cs`

**Objetivo**
Crear el envoltorio de éxito del use case.

**Pasos**

1. Crear `src/ControlDeFinanzasIA.Application/UseCases/Expenses/RegisterExpense/RegisterExpenseResult.cs`.
2. File-scoped namespace `ControlDeFinanzasIA.Application.UseCases.Expenses.RegisterExpense`.
3. Añadir `using ControlDeFinanzasIA.Application.DTOs;`.
4. Declarar:
   ```csharp
   public sealed record RegisterExpenseResult(
       ExpenseDto Expense,
       IReadOnlyList<string> Warnings);
   ```
5. XML doc explicando que `Warnings` es inmutable y vacío si no hay advertencias.
6. Ejecutar `dotnet build`.

**Verificación**

- `dotnet build ... -c Release --nologo`: exit 0, 0 warnings, 0 errors.
- Inspección: `sealed record`, `Warnings` es `IReadOnlyList<string>`, XML doc.

**Dependencias:** T-001.
**Estimación:** S (< 1 h).
**Trazabilidad:** AC-6, §6 DP-5, §8 AC-6.

---

## T-006 — Implementar `RegisterExpenseUseCase.cs`

**Objetivo**
Crear el use case que orquesta validación, warnings, creación de entidad,
persistencia y mapeo a DTO.

**Pasos**

1. Crear `src/ControlDeFinanzasIA.Application/UseCases/Expenses/RegisterExpense/RegisterExpenseUseCase.cs`.
2. File-scoped namespace `ControlDeFinanzasIA.Application.UseCases.Expenses.RegisterExpense`.
3. `using` al tope:
   - `ControlDeFinanzasIA.Application.Abstractions`
   - `ControlDeFinanzasIA.Application.Common`
   - `ControlDeFinanzasIA.Application.DTOs`
   - `ControlDeFinanzasIA.Domain.Transactions`
   - `ControlDeFinanzasIA.Domain.ValueObjects`
   - `FluentValidation`
   - `Microsoft.Extensions.Logging`
4. Declarar `public sealed class RegisterExpenseUseCase`.
5. Constructor que inyecte `IExpenseRepository`, `IClock`,
   `IValidator<RegisterExpenseCommand>`, `ILogger<RegisterExpenseUseCase>`.
6. Implementar `ExecuteAsync(RegisterExpenseCommand command, CancellationToken ct = default)`:
   - `ArgumentNullException.ThrowIfNull(command);`
   - `var vr = _validator.Validate(command);`
   - Si inválido: `Result.Failure("Validation", errorSnapshot)`.
   - `var warnings = new List<string>();` Añadir `"FutureDateWarning"` si
     `command.Date > _clock.UtcNow.Date`.
   - En try/catch (`ArgumentException` → `"DomainInvariant"`):
     - `Money money = Money.Create(...);`
     - `Expense expense = Expense.Create(categoryId, date, money, description, note, _clock.UtcNow);`
     - `Result addResult = await _repository.AddAsync(expense, ct);`
     - Si `!addResult.IsSuccess`, propagar `addResult`.
     - `ExpenseDto dto = expense.ToDto();`
     - `return Result.Success(new RegisterExpenseResult(dto, warnings));`
   - Loggear excepción y devolver `Result.Failure("DomainInvariant", ex.Message)`.
7. **Importante:** el repo F-003 devuelve `Result.Success(null!)`; construir
   el DTO desde la instancia local `expense`, no desde `addResult.Value`.
8. XML doc en clase y `ExecuteAsync` documentando el cast
   `(RegisterExpenseResult)result.Value!`.
9. Ejecutar `dotnet build`.

**Verificación**

- `dotnet build ... -c Release --nologo`: exit 0, 0 warnings, 0 errors.
- Inspección: `sealed`; constructor con 4 dependencias;
  `_validator.Validate(command)` (no `ValidateAsync`);
  `Money.Create` + `Expense.Create` con `_clock.UtcNow`;
  propaga `"CategoryNotFound"`; mapea DTO desde `expense` local.

**Dependencias:** T-001..T-005, F-001, F-003, Phase 1 F-004/F-005.
**Estimación:** M (2–3 h).
**Trazabilidad:** AC-5, §6 DP-1..DP-6, §8 AC-5, §9.1.

---

## T-007 — Registrar use case + validator en `Bootstrap`

**Objetivo**
Añadir 2 registros `Transient` en `Bootstrap.ConfigureServices`.

**Pasos**

1. Abrir `src/ControlDeFinanzasIA.UI/Composition/Bootstrap.cs`.
2. Localizar `ConfigureServices`.
3. Añadir:
   ```csharp
   services.AddTransient<RegisterExpenseUseCase>();
   services.AddTransient<IValidator<RegisterExpenseCommand>, RegisterExpenseValidator>();
   ```
4. Asegurar `using`:
   - `ControlDeFinanzasIA.Application.UseCases.Expenses.RegisterExpense`
   - `FluentValidation`
5. **No** modificar `AddPersistence`.
6. Ejecutar `dotnet build`.

**Verificación**

- `dotnet build ... -c Release --nologo`: exit 0, 0 warnings, 0 errors.
- Inspección: ambos registros `Transient`; path correcto; `AddPersistence` intacto.

**Dependencias:** T-004, T-006.
**Estimación:** S (< 1 h).
**Trazabilidad:** AC-7, §8 AC-7, §10.

---

## T-008 — Crear `RegisterExpenseUseCaseTests.cs`

**Objetivo**
Tests unitarios del use case con `FakeClock` y fake manual de repo, pure xUnit.

**Pasos**

1. Crear `tests/ControlDeFinanzasIA.Tests.Unit/Application/Expenses/RegisterExpenseUseCaseTests.cs`.
2. Crear la carpeta si no existe.
3. File-scoped namespace `ControlDeFinanzasIA.Tests.Unit.Application.Expenses`.
4. `using` al tope:
   - `ControlDeFinanzasIA.Application.Abstractions`
   - `ControlDeFinanzasIA.Application.Common`
   - `ControlDeFinanzasIA.Application.DTOs`
   - `ControlDeFinanzasIA.Application.UseCases.Expenses.RegisterExpense`
   - `ControlDeFinanzasIA.Domain.ValueObjects`
   - `ControlDeFinanzasIA.Tests.Unit.TestDoubles`
   - `FluentValidation`
   - `Microsoft.Extensions.Logging.Abstractions`
   - `Xunit`
5. Crear fake manual de `IExpenseRepository` (local o en `TestDoubles/`):
   - Devolver `Result.Success(null!)` por defecto.
   - Permitir configurar `Result.Failure` (ej. `"CategoryNotFound"`).
   - Capturar la entidad `Expense` recibida.
6. Implementar al menos 6 tests (§9.1):
   - `ExecuteAsync_ValidCommand_ReturnsSuccessWithDto`
   - `ExecuteAsync_InvalidAmount_ReturnsValidationFailure`
   - `ExecuteAsync_EmptyDescription_ReturnsValidationFailure`
   - `ExecuteAsync_FutureDate_AddsFutureDateWarning`
   - `ExecuteAsync_UnknownCategoryId_PropagatesCategoryNotFound`
   - `ExecuteAsync_AddAsyncSucceeds_AuditDatesFromClock`
7. Aserciones `Assert.*` — **no** `Should()`.
8. Ejecutar `dotnet test --filter "FullyQualifiedName~RegisterExpenseUseCaseTests"`.

**Verificación**

- `dotnet build ... -c Release --nologo`: exit 0, 0 warnings, 0 errors.
- `dotnet test ... --filter "FullyQualifiedName~RegisterExpenseUseCaseTests"`: ≥6 passed, 0 failed.
- Inspección: file-scoped namespace, `Assert.*`, `FakeClock`, fake manual.

**Dependencias:** T-006, Phase 1 F-004 (`FakeClock`), F-003.
**Estimación:** M (2–3 h).
**Trazabilidad:** AC-9, §9.1 T-U1..T-U6.

---

## T-009 — Crear `RegisterExpenseValidatorTests.cs`

**Objetivo**
Tests unitarios del validador con `[Theory]`/`[InlineData]` y pure xUnit.

**Pasos**

1. Crear `tests/ControlDeFinanzasIA.Tests.Unit/Application/Expenses/RegisterExpenseValidatorTests.cs`.
2. File-scoped namespace `ControlDeFinanzasIA.Tests.Unit.Application.Expenses`.
3. `using` al tope:
   - `ControlDeFinanzasIA.Application.UseCases.Expenses.RegisterExpense`
   - `ControlDeFinanzasIA.Domain.ValueObjects`
   - `FluentValidation.Results`
   - `Xunit`
4. Implementar al menos 4 tests (§9.2):
   - `Validate_ValidCommand_ReturnsSuccess`
   - `Validate_ZeroOrNegativeAmount_ReturnsFailure` (`[Theory]` con `0` y `-1`)
   - `Validate_WhitespaceOnlyDescription_ReturnsFailure`
   - `Validate_OverMaxLengthDescription_ReturnsFailure`
   - (Opcional) `Validate_FutureDate_IsAllowed`
5. En cada test: `new RegisterExpenseValidator()`, `validator.Validate(command)`,
   `Assert.True/False(result.IsValid)`.
6. Ejecutar `dotnet test --filter "FullyQualifiedName~RegisterExpenseValidatorTests"`.

**Verificación**

- `dotnet build ... -c Release --nologo`: exit 0, 0 warnings, 0 errors.
- `dotnet test ... --filter "FullyQualifiedName~RegisterExpenseValidatorTests"`: ≥4 passed, 0 failed.
- Inspección: `[Theory]` en monto, aserciones `Assert.*`.

**Dependencias:** T-004.
**Estimación:** S (1 h).
**Trazabilidad:** AC-10, §9.2 T-V1..T-V5.

---

## T-010 — Verificación final, formato y cierre

**Objetivo**
Ejecutar la batería de verificación final y cerrar la feature.

**Pasos**

1. `dotnet build -c Release` y `dotnet build -c Debug`: 0 warnings, 0 errors.
2. `dotnet test` completo: ~187 passed, 0 failed.
3. `dotnet test --filter "FullyQualifiedName~ArchitectureTests"`: todos passed.
4. `dotnet list src/ControlDeFinanzasIA.Domain package`: vacío.
5. `dotnet list src/ControlDeFinanzasIA.Application package`: sólo `FluentValidation`.
6. `dotnet list src/ControlDeFinanzasIA.UI reference`: sin referencia a `Infrastructure`.
7. `dotnet format style --verify-no-changes`: exit 0.
8. Verificar convenciones en archivos nuevos: file-scoped namespace, `using` al
tope, XML doc, `sealed`, códigos en inglés.
9. Confirmar AC-15: sin UI, `GetTransactionsQuery`, `UpdateExpenseUseCase`,
   migraciones, modificaciones a `Expense.cs`/`Result`/`IExpenseRepository`/`IClock`,
   ni FluentAssertions.
10. Actualizar `spec/phases/phase-03/features.md`: F-004 a "✅ COMPLETADA".
11. Renombrar spec a `.done.md`.
12. `dotnet build -c Release` final.

**Verificación**

- `dotnet build -c Release`: exit 0.
- `dotnet test -c Release --no-build`: ~187 passed, 0 failed.
- NetArchTest R-1..R-5: passed.
- `dotnet format style --verify-no-changes`: exit 0.
- `features.md` actualizado; spec renombrada.

**Dependencias:** T-001..T-009.
**Estimación:** M (1–2 h).
**Trazabilidad:** AC-11..AC-15.

---

## Orden de ejecución recomendado

**Secuencial con paralelismo inicial:**

1. **T-001**, **T-002**, **T-003** y **T-005** pueden ejecutarse en paralelo.
2. **T-004** depende de T-003.
3. **T-006** depende de T-001..T-005.
4. **T-007** depende de T-004 y T-006.
5. **T-008** depende de T-006; **T-009** de T-004. Pueden correr juntas.
6. **T-010** cierra la feature.

```text
T-001 ──┐
T-002 ──┤
T-003 ──┼→ T-004 ──┐
T-005 ──┘          ├→ T-006 → T-007 ─┐
                     │                ├→ T-010
                     └→ T-009 ────────┘
                                      ↑
                                   T-008 ┘
```

---

## Tareas bloqueantes / habilitadoras

| Tarea | Habilita | Razón |
| --- | --- | --- |
| T-001 | T-005, T-006 | Necesitan `ExpenseDto`. |
| T-002 | T-006 | El use case invoca `expense.ToDto()`. |
| T-003 | T-004, T-006 | Validador y use case operan sobre `RegisterExpenseCommand`. |
| T-004 | T-006, T-007, T-009 | Inyección de `IValidator<RegisterExpenseCommand>` y tests del validador. |
| T-005 | T-006 | Use case retorna `RegisterExpenseResult`. |
| T-006 | T-007, T-008 | Bootstrap registra el use case; tests lo instancian. |
| T-007 | T-010 | Registro DI verificado por NetArchTest R-4 y build. |
| T-008, T-009 | T-010 | Todos los tests deben pasar antes del cierre. |

---

## Estimación total

| Tarea | Dificultad | Horas estimadas |
| --- | --- | --- |
| T-001 | S | 0.5 |
| T-002 | S | 0.5 |
| T-003 | S | 0.5 |
| T-004 | S | 0.5 |
| T-005 | S | 0.5 |
| T-006 | M | 2 |
| T-007 | S | 0.5 |
| T-008 | M | 2–3 |
| T-009 | S | 1 |
| T-010 | M | 1–2 |
| **Total** | | **~9–11 horas (~1.5 jornadas)** |

---

## Riesgos y mitigaciones

| Riesgo | Prob. | Impacto | Mitigación |
| --- | --- | --- | --- |
| FluentValidation 11 lanza `AsyncValidatorInvokedSynchronouslyException`. | Baja | Medio | Reglas sync (DP-6); use case llama `Validate`, no `ValidateAsync`. Ver T-004/T-006. |
| Confusión `Result` genérico vs non-genérico. | Media | Medio | Use case retorna `Task<Result>`; caller hace cast. Documentado en XML doc. |
| `Expense.Create` lanza `ArgumentException`. | Baja | Medio | Try/catch defensivo → `"DomainInvariant"`. Ver T-006. |
| Repo devuelve `Result.Success(null!)`; se construye DTO desde `result.Value`. | Media | Alto | Nota explícita en T-006: construir DTO desde `expense` local. |
| `FutureDateWarning` sin presentación definida. | Media | Bajo | F-006/F-007 lo resuelven; F-004 sólo transporta `IReadOnlyList<string>`. |
| `CategoryNotFound` propagado tal cual. | Baja | Bajo | ViewModel (F-007) decide presentación; F-004 no renombra. |
| Bootstrap en `Composition/` (path no raíz UI). | — | Bajo | Ruta confirmada; ver T-007. |
| Se añade FluentAssertions. | Media | Bajo | §11 y AC-9/AC-10 exigen `Assert.*`. Revisar en PR. |
| Scope creep hacia UI / query / update. | Media | Alto | AC-15 prohíbe; verificar en T-010. |
| `dotnet format style` propone cambios. | Baja | Bajo | Aplicar; no relajar reglas. |
| R-2 falla por nueva dependencia en `Application`. | Muy baja | Alto | Sólo `FluentValidation` (ya referenciado) + referencias a `Domain`. |

---

## Notas para apply

- **No tocar `Domain/Transactions/Expense.cs`.** `Expense.Create` ya normaliza
  trim/null (F-001 INV-4/INV-5). No hay migración.
- **No tocar `Result`.** F-005 Phase 1 lo definió non-generic con `Value: object?`.
  Use case retorna `Result.Success(new RegisterExpenseResult(...))`; caller hace
  `(RegisterExpenseResult)result.Value!`.
- **Repo devuelve `Result.Success(null!)` (F-003).** Construir DTO desde la
  instancia local `expense`, no desde `addResult.Value`. EF Core ya asignó `Id`
  tras `SaveChangesAsync` dentro de `AddAsync`.
- **FluentValidation 11.10.0 sync.** Reglas sync (`RuleFor`, `NotEmpty`,
  `GreaterThan`, `MaximumLength`, `Must`, `IsInEnum`). Use case invoca
  `_validator.Validate(command)`. Sin `MustAsync`/`CustomAsync`.
- **Códigos en inglés:** `"Validation"`, `"CategoryNotFound"`, `"DomainInvariant"`,
  `"FutureDateWarning"`.
- **Audit dates desde `IClock`.** Pasar `_clock.UtcNow` a `Expense.Create`;
  tests usar `FakeClock`.
- **Warnings como `IReadOnlyList<string>`.** Inicializar vacío; añadir
  `"FutureDateWarning"` si `command.Date > _clock.UtcNow.Date`.
- **No modificar `AddPersistence`.** F-003 ya registró `IExpenseRepository`.
- **Tests pure xUnit.** `Assert.True`, `Assert.False`, `Assert.Equal`,
  `Assert.NotNull`, `Assert.Contains`. **NO FluentAssertions**.
- **Fake de repo manual.** Puede vivir en `TestDoubles/FakeExpenseRepository.cs`
  o ser nested class. Debe configurar el `Result` de retorno y capturar la
  entidad recibida.
- **Validador testeado directamente.** `new RegisterExpenseValidator()` no
  requiere DI.

---

## Definition of Done (DoD)

- [x] AC-1: `ExpenseDto` con 9 campos, `sealed record`, XML doc. (T-001)
- [x] AC-2: `ExpenseExtensions.ToDto` mapea todos los campos. (T-002)
- [x] AC-3: `RegisterExpenseCommand` `sealed record` con 6 campos, sin `Id`
      ni audit. (T-003)
- [x] AC-4: `RegisterExpenseValidator` 6 reglas sync, rechaza whitespace-only. (T-004)
- [x] AC-5: `RegisterExpenseUseCase` inyecta 4 deps, flujo completo, propaga
      `"CategoryNotFound"`. (T-006)
- [x] AC-6: `RegisterExpenseResult` `sealed record` con DTO + warnings. (T-005)
- [x] AC-7: `Bootstrap` registra use case y validator `Transient`. (T-007)
- [x] AC-8: `Application.csproj` ya tiene `FluentValidation`; sólo verificar. (T-006/T-010)
- [x] AC-9: Tests use case ≥ 6 verdes con `FakeClock` y fake repo. (T-008)
- [x] AC-10: Tests validador ≥ 4 verdes con `[Theory]`. (T-009)
- [x] AC-11: `dotnet build -c Release` limpio. (T-010)
- [x] AC-12: `dotnet test` ≈187 verdes. (T-010)
- [x] AC-13: NetArchTest R-1..R-5 verdes. (T-010)
- [x] AC-14: `dotnet format style --verify-no-changes` exit 0. (T-010)
- [x] AC-15: Sin scope creep. (T-010)

---

## Referencias

- `spec/features/phase-03/F-004-register-expense-use-case.spec.md`
- `spec/phases/phase-03/features.md`
- `spec/features/phase-03/F-001-expense-domain-entity.done.md`
- `spec/features/phase-03/F-003-expense-repository.done.md`
- `spec/features/phase-01/F-005-comuntypes-result-pagedresult.done.md`
- `spec/features/phase-01/F-006-contrato-interfaces-repo.done.md`
- `spec/features/phase-01/F-004-Abstracciones-clock-pathresolver.done.md`
- `tasks/phase-03/F-003-expense-repository.tasks.md`
- Engram `sdd/controldefinanzasia/testing-capabilities`
- Engram `sdd/f-004p3-register-expense-use-case/proposal`
- Engram `sdd/f-004p3-register-expense-use-case/spec`
