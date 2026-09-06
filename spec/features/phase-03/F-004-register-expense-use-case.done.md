# Specification — F-004: Application: DTOs + `RegisterExpenseValidator` + `RegisterExpenseUseCase`

> **Fase:** 3 — Slice vertical: registrar y listar gastos
> **Feature:** F-004 (local a esta fase)
> **Estado:** Pendiente de implementación
> **Spec ID:** SPEC-F004P3
>
> **Nota de numeración:** este `F-004` es el de la **Fase 3**
> (`spec/phases/phase-03/features.md`). No debe confundirse con el
> `F-004` de Fase 1 (`IClock` / `IPathResolver`). Los IDs son locales
> a cada fase; el folder los desambigua. Ver `spec/README.md` para
> la convención.
>
> Fuentes de verdad: `docs/vision.md`, `docs/architecture.md`,
> `docs/roadmap.md` (§Fase 3), `spec/constitution/tech-stack.md`,
> `spec/constitution/mission.md`,
> `spec/phases/phase-03/features.md` (F-004 dentro del desglose),
> `spec/features/phase-03/F-001-expense-domain-entity.done.md`
> (entidad `Expense` con invariantes INV-1..INV-8 y factory `Create`),
> `spec/features/phase-03/F-003-expense-repository.done.md`
> (`IExpenseRepository.AddAsync` devuelve `Result` no-genérico; códigos
> `"CategoryNotFound"` y `"NotFound"`; 177 tests verdes actuales),
> `spec/features/phase-01/F-005-comuntypes-result-pagedresult.done.md`
> (contrato de `Result` — **no se modifica**),
> `spec/features/phase-01/F-006-contrato-interfaces-repo.done.md`
> (interface `IExpenseRepository`),
> `spec/features/phase-01/F-004-Abstracciones-clock-pathresolver.done.md`
> (interface `IClock`).

---

## 1. Resumen

F-004 introduce el **primer use case del proyecto** (`RegisterExpenseUseCase`),
el patrón que **todos** los use cases futuros replicarán: un
`Command` inmutable, un validador FluentValidation 11.10.0 **sync**, un
mapeo manual a la entidad `Expense` vía sus factories (`Money.Create` +
`Expense.Create`), persistencia delegada en `IExpenseRepository.AddAsync`，
auditoría de timestamp sourced desde `IClock.UtcNow`, y un retorno
`Task<Result>` (no-genérico, eco de F-005 de Fase 1) envolviendo un
nuevo `RegisterExpenseResult` (DTO + lista de warnings). Cerrar el
lado de escritura del slice vertical de Phase 3 deja el contrato
Application → UI listo para que F-007 (ViewModel) sólo bindee el DTO.

Al final de la feature:

- `src/ControlDeFinanzasIA.Application/DTOs/ExpenseDto.cs` existe como
  `public sealed record` con 9 campos.
- `src/ControlDeFinanzasIA.Application/DTOs/ExpenseExtensions.cs`
  expone `ToDto(this Expense)`.
- `src/ControlDeFinanzasIA.Application/UseCases/Expenses/RegisterExpense/`
  contiene los 4 tipos nuevos: `RegisterExpenseCommand`,
  `RegisterExpenseValidator`, `RegisterExpenseUseCase`,
  `RegisterExpenseResult`.
- `src/ControlDeFinanzasIA.UI/Composition/Bootstrap.cs` registra el
  use case y el `IValidator<RegisterExpenseCommand>` como `Transient`.
- Los 2 archivos de tests unitarios
  (`RegisterExpenseUseCaseTests.cs` ≥ 6 + `RegisterExpenseValidatorTests.cs`
  ≥ 4) pasan con **pure xUnit `Assert.*`** (NO FluentAssertions).
- `dotnet build -c Release`: 0 warnings, 0 errors.
- `dotnet test`: ~177 verdes + ~10 nuevos ≈ **187 verdes**.
- NetArchTest R-1 a R-5 siguen pasando (F-004 sólo añade tipos a
  `Application` y 2 registros a la composition root de `UI`; no
  introduce referencias prohibidas).

## 2. Objetivo

1. **Introducir 6 tipos nuevos** en `Application` sin tocar `Domain`,
   `Infrastructure` (salvo DI) ni `Result`/`IExpenseRepository`/`IClock`:
   `ExpenseDto`, `ExpenseExtensions.ToDto`, `RegisterExpenseCommand`,
   `RegisterExpenseValidator`, `RegisterExpenseUseCase`,
   `RegisterExpenseResult`.
2. **Establecer el patrón de use case**: validador FluentValidation
   inyectado (`IValidator<RegisterExpenseCommand>`) resuelto por la DI
   de FluentValidation; comando inmutable; mapeo manual a la entidad;
   `createdAt` sourced desde `IClock.UtcNow` (ADR-007).
3. **Modelar los warnings como un tipo dedicado** (`RegisterExpenseResult`)
   para no acoplar la lista de adverntencias a `Result` (Phase 1 F-005
   se mantiene intacto).
4. **Cubrir con ≥ 10 tests** el use case y el validador usando pure
   xUnit `Assert.*` y fakes manuales para el repo y el reloj.

## 3. Descripción breve

| Tipo | Archivo | Responsabilidad |
| --- | --- | --- |
| `ExpenseDto` (sealed record) | `Application/DTOs/ExpenseDto.cs` | 9 campos (`Id`, `CategoryId`, `Date`, `Amount` decimal, `Currency` enum, `Description`, `Note?`, `CreatedAt`, `UpdatedAt`). Contrato Application → UI. |
| `ExpenseExtensions.ToDto` | `Application/DTOs/ExpenseExtensions.cs` | Mapeo manual `Expense → ExpenseDto` (extrae `e.Amount.Amount` y `e.Amount.Currency`). |
| `RegisterExpenseCommand` (sealed record) | `Application/UseCases/Expenses/RegisterExpense/` | 6 campos: `CategoryId`, `Date`, `Amount` decimal, `Currency`, `Description`, `Note?`. Sin `Id` ni audit. |
| `RegisterExpenseValidator : AbstractValidator<RegisterExpenseCommand>` | idem | 6 reglas **sync** (`NotEqual(Guid.Empty)`, `GreaterThan(0)`, `NotEmpty().MaximumLength(200).Must(trim)`, `MaximumLength(500).When`, `IsInEnum()`). No rechaza fechas futuras. |
| `RegisterExpenseUseCase` | idem | Inyecta `IExpenseRepository`, `IClock`, `IValidator<RegisterExpenseCommand>`, `ILogger<…>`. Método `ExecuteAsync(cmd, ct) → Task<Result>`. |
| `RegisterExpenseResult` (sealed record) | idem | `(ExpenseDto Expense, IReadOnlyList<string> Warnings)`. Envoltorio de éxito. |
| `Bootstrap.ConfigureServices` | `UI/Composition/Bootstrap.cs` | 2 registros nuevos: `AddTransient<RegisterExpenseUseCase>()` + `AddTransient<IValidator<RegisterExpenseCommand>, RegisterExpenseValidator>()`. No se toca `AddPersistence`. |
| `RegisterExpenseUseCaseTests.cs` | `Tests.Unit/Application/Expenses/` | ≥ 6 tests. |
| `RegisterExpenseValidatorTests.cs` | idem | ≥ 4 tests (`[Theory]`/`[InlineData]`). |

## 4. Dependencias

| Dependencia | Estado | Uso en F-004 |
| --- | --- | --- |
| F-001 de Fase 3 (archivada) | ✅ | Entidad `Expense`, factory `Create(categoryId, date, money, description, note, createdAt)` con INV-1..INV-8. **F-004 NO la modifica.** |
| F-003 de Fase 3 (archivada) | ✅ | `IExpenseRepository.AddAsync(Expense, ct) → Task<Result>` con códigos `"CategoryNotFound"` / `"NotFound"`. 177 tests verdes actuales. |
| F-005 de Fase 1 (archivada) | ✅ | `Result` **non-generic** (`Value: object?`); factories `Success(object)` / `Failure(code, message)` / `Failure(code, message, details)`. **F-004 NO lo modifica.** El caller hace `(RegisterExpenseResult)result.Value!`. |
| F-006 de Fase 1 (archivada) | ✅ | Interface `IExpenseRepository` firmada (6 métodos tras F-003). |
| F-004 de Fase 1 (archivada) | ✅ | `IClock.UtcNow` en `Application/Abstractions/`; `FakeClock` en `Tests.Unit/TestDoubles/`. |
| FluentValidation 11.10.0 | ✅ pre-wired | Ya en `Directory.Packages.props` y ya `PackageReference` en `Application.csproj`. **F-004 no añade el paquete; sólo lo consume.** |

**Bloqueos:** Ninguno. F-001 y F-003 (Fase 3) están archivadas; Phase 1
abstracciones completas; FluentValidation ya referenciado.

## 5. Fuera de alcance

- **UI / ViewModel** (`ExpensesViewModel`, `ExpensesView`, navegación,
  DataTemplates). Son F-006 y F-007 de Fase 3.
- **`GetTransactionsQuery`** (lado de lectura del slice). Es F-005.
- **Modificar `Result`** (Phase 1 F-005). Los warnings viven en
  `RegisterExpenseResult`, no en `Result`.
- **Modificar `Expense.cs`**, `ExpenseConfiguration`, `AppDbContext`,
  `ExpenseRepository`, o generar migraciones. F-004 sólo añade tipos
  nuevos a `Application` y 2 registros a `Bootstrap`.
- **Soft-delete**, UI multi-moneda, tags, métodos de pago.
- **`UpdateExpenseUseCase`** (Phase 4).
- **Async validators** (`MustAsync`). v1 usa sólo reglas sync.

## 6. Decisiones de diseño (fijadas por la propuesta — no re-abrir)

| # | Decisión | Rationale |
| --- | --- | --- |
| DP-1 | Fechas futuras permitidas; el validador **no** las rechaza; el use case añade `"FutureDateWarning"` a `Warnings` si `command.Date > _clock.UtcNow.Date`. | `vision.md` §4 no prohíbe gastos planificados; la invariante INV-3 de F-001 sólo floor `1900-01-01`. |
| DP-2 | `Currency` viaja **en el comando**; el validador usa `IsInEnum()`. Multi-monedaadelante desde el día 1 en el contrato. | Single-currency por ahora, pero el contrato Application → UI no se rompe cuando entren más monedadas. |
| DP-3 | `Note` vacío → `null` se normaliza **en la entidad** (F-001 INV-5 ya lo hace). El validador sólo chequea `length ≤ 500 if not null`. | Evita duplicar trim/canonización; un solo dueño de la normalización. |
| DP-4 | `Description` whitespace-only = inválido. El validador usa `Must(d => d.Trim().Length > 0)`. | " pizza " se trim por la entidad; "   " se rechaza por el validador antes de llegar al factory. Doble-trim kosher: el validador **sólo chequea**, no muta. |
| DP-5 | Nuevo tipo `RegisterExpenseResult = sealed record(ExpenseDto Expense, IReadOnlyList<string> Warnings)`. El use case retorna `Task<Result>` y el caller hace `(RegisterExpenseResult)result.Value!`. | No acoplar warnings a `Result` (que es de Phase 1 y no se modifica). El envoltorio de éxito es específico del use case. |
| DP-6 | FluentValidation 11.x: **todas** las reglas son `sync` (`RuleFor`/`NotEmpty`/`IsInEnum`/`GreaterThan`/`MaximumLength`/`Must`). El use case invoca `_validator.Validate(command)` (no `ValidateAsync`). | El breaking change 11.0 lanza `AsyncValidatorInvokedSynchronouslyException` sólo para validadores async. Sin `MustAsync`/`CustomAsync` → no hay trampa. |

## 7. Cambios al alcance (qué se toca y qué no)

**Se añade (sólo en `Application` y `Tests.Unit`):**

- 2 archivos en `Application/DTOs/`.
- 4 archivos en `Application/UseCases/Expenses/RegisterExpense/`.
- 2 archivos en `Tests.Unit/Application/Expenses/`.

**Se modifica (sólo `Bootstrap.ConfigureServices` en `UI`):**

- 2 líneas `AddTransient`: el use case y `IValidator<RegisterExpenseCommand>`.
- No se toca `AddPersistence` (F-003 ya registró `IExpenseRepository`).
- El validador es stateless, pero `Transient` es la opción idiomática
  y segura (prepara el patrón para validators stateful futuros).

**NO se modifica:**

- `Expense.cs`, `Money.cs`, `Currency.cs` (Domain).
- `ExpenseConfiguration.cs`, `AppDbContext.cs`, `ExpenseRepository.cs`,
  migraciones (Infrastructure).
- `Result.cs`, `PagedResult.cs`, `IExpenseRepository.cs`, `IClock.cs`,
  `IPathResolver.cs` (Application abstracciones — Phase 1 intactas).
- `Directory.Packages.props`, `Application.csproj` (FluentValidation
  **ya referenciado**; F-004 no añade paquetes).
- Tests de arquitectura NetArchTest (R-1..R-5 siguen pasando sin cambios).

## 8. Acceptance Criteria

La feature se considera completada cuando **todos** los siguientes ACs
se cumplen, verificables mediante tests automatizados y revisión de
código.

### AC-1 — `ExpenseDto`
- [ ] `src/ControlDeFinanzasIA.Application/DTOs/ExpenseDto.cs` existe
      como `public sealed record ExpenseDto(Guid Id, Guid CategoryId,
      DateOnly Date, decimal Amount, Currency Currency, string Description,
      string? Note, DateTime CreatedAt, DateTime UpdatedAt)`.
- [ ] File-scoped namespace, XML doc, `sealed`.

### AC-2 — `ExpenseExtensions.ToDto`
- [ ] `src/ControlDeFinanzasIA.Application/DTOs/ExpenseExtensions.cs`
      expone `public static ExpenseDto ToDto(this Expense e)`.
- [ ] Mapea `e.Amount.Amount → decimal`, `e.Amount.Currency → Currency`,
      y los 7 campos escalares restantes.

### AC-3 — `RegisterExpenseCommand`
- [ ] `src/ControlDeFinanzasIA.Application/UseCases/Expenses/RegisterExpense/RegisterExpenseCommand.cs`
      es `public sealed record` con 6 campos: `CategoryId`, `Date`,
      `Amount` (decimal), `Currency`, `Description`, `Note?`.
- [ ] **Sin** `Id`, **sin** audit. Sealed record, XML doc.

### AC-4 — `RegisterExpenseValidator`
- [ ] `public sealed class RegisterExpenseValidator : AbstractValidator<RegisterExpenseCommand>`.
- [ ] 6 reglas **sync**: `CategoryId` `NotEqual(Guid.Empty)`;
      `Amount` `GreaterThan(0)`; `Description`
      `NotEmpty().MaximumLength(200).Must(d => d.Trim().Length > 0)`
      (DP-4); `Note` `MaximumLength(500).When(n => n is not null)`
      (DP-3); `Currency` `IsInEnum()` (DP-2); `Date` **sin techo**
      superior (DP-1 — fechas futuras permitidas).
- [ ] No usa `MustAsync`/`CustomAsync`/`ValidateAsync` (DP-6).

### AC-5 — `RegisterExpenseUseCase`
- [ ] `public sealed class RegisterExpenseUseCase` con constructor que
      inyecta `IExpenseRepository`, `IClock`,
      `IValidator<RegisterExpenseCommand>`,
      `ILogger<RegisterExpenseUseCase>`.
- [ ] `public async Task<Result> ExecuteAsync(RegisterExpenseCommand command, CancellationToken ct = default)`.
- [ ] Flujo: (1) `_validator.Validate(command)` → inválido →
      `Result.Failure("Validation", errorSnapshot)` (con `details` por
      campo si aplica); (2) `warnings = new List<string>()` y si
      `command.Date > _clock.UtcNow.Date` añade `"FutureDateWarning"`
      (DP-1); (3) `Money.Create` + `Expense.Create(categoryId, date,
      money, description, note, _clock.UtcNow)` — la entidad normaliza
      trim/null INV-4/INV-5; (4) `await _repo.AddAsync(expense, ct)` →
      si `!IsSuccess`, **propaga** el `Result.Failure` tal cual (código
      `"CategoryNotFound"` del repo F-003); (5) `expense.ToDto()` →
      `Result.Success(new RegisterExpenseResult(dto, warnings))`.

### AC-6 — `RegisterExpenseResult`
- [ ] `public sealed record RegisterExpenseResult(ExpenseDto Expense, IReadOnlyList<string> Warnings)`.
- [ ] `Warnings` es `IReadOnlyList<string>` (inmutable; vacío si no
      hay warnings).

### AC-7 — `Bootstrap` registra transient
- [ ] `Bootstrap.ConfigureServices` (en
      `UI/Composition/Bootstrap.cs`) añade 2 `AddTransient`:
      `RegisterExpenseUseCase` y
      `IValidator<RegisterExpenseCommand> → RegisterExpenseValidator`.
- [ ] No se modifica `AddPersistence` ni otros registros existentes.

### AC-8 — FluentValidation ya referenciado
- [ ] `Application.csproj` ya tiene `<PackageReference Include="FluentValidation" />`
      resuelto a 11.10.0 desde el CPM. F-004 **sólo verifica** que
      compila; no edita el `.csproj` ni el `Directory.Packages.props`.

### AC-9 — Tests del use case (≥ 6)
- [ ] `tests/ControlDeFinanzasIA.Tests.Unit/Application/Expenses/RegisterExpenseUseCaseTests.cs`
      existe con estos métodos (mínimo):
  - `ExecuteAsync_ValidCommand_ReturnsSuccessWithDto`
  - `ExecuteAsync_InvalidAmount_ReturnsValidationFailure`
  - `ExecuteAsync_EmptyDescription_ReturnsValidationFailure`
  - `ExecuteAsync_FutureDate_AddsFutureDateWarning`
  - `ExecuteAsync_UnknownCategoryId_PropagatesCategoryNotFound`
  - `ExecuteAsync_AddAsyncSucceeds_AuditDatesFromClock`
- [ ] Usan `FakeClock` (F-004 de Fase 1) y un fake `IExpenseRepository`
      manual (en `TestDoubles/` o local al archivo). Pure xUnit `Assert.*`.

### AC-10 — Tests del validador (≥ 4)
- [ ] `RegisterExpenseValidatorTests.cs` existe con estos métodos
      (mínimo, `[Theory]`/`[InlineData]` recomendados):
  - `Validate_ValidCommand_ReturnsSuccess`
  - `Validate_ZeroOrNegativeAmount_ReturnsFailure`
  - `Validate_WhitespaceOnlyDescription_ReturnsFailure`
  - `Validate_OverMaxLengthDescription_ReturnsFailure`
  - (opcional) `Validate_UnknownCurrencyValue_ReturnsFailure` /
    `Validate_FutureDate_IsAllowed`.

### AC-11 — Build limpio
- [ ] `dotnet build -c Release`: 0 warnings, 0 errors.

### AC-12 — Tests verdes
- [ ] `dotnet test -c Release --no-build`: 177 + ~10 ≈ **187 verdes**,
      0 failures (134 unit + 43 integration heredados + ~10 nuevos).

### AC-13 — NetArchTest
- [ ] R-1 (Domain puro), R-2 (Application sólo `Domain` +
      `FluentValidation`), R-3 (Infrastructure → Application + Domain),
      R-4 (UI → Application + Domain; sin Infrastructure desde
      Views), R-5 (src/ no → Tests) siguen pasando sin cambios.
- [ ] Verificable leyendo `Application.csproj`: sólo `Domain`
      `ProjectReference` + `FluentValidation` `PackageReference`.

### AC-14 — Format
- [ ] `dotnet format style --verify-no-changes`: exit 0.

### AC-15 — Sin scope creep
- [ ] NO UI, NO `GetTransactionsQuery`, NO `Expense.Update` use case,
      NO migraciones, NO modificaciones a `Expense.cs` / `Result` /
      `IExpenseRepository` / `IClock`, NO FluentAssertions.

## 9. Test cases

### 9.1 Use case — `RegisterExpenseUseCaseTests.cs` (≥ 6)

| # | Método | Escenario |
| --- | --- | --- |
| T-U1 | `ExecuteAsync_ValidCommand_ReturnsSuccessWithDto` | Comando válido + repo OK → `IsSuccess` true; `Value` castea a `RegisterExpenseResult`; `Warnings` vacío; `Expense` con todos los campos. |
| T-U2 | `ExecuteAsync_InvalidAmount_ReturnsValidationFailure` | `Amount = 0` o negativo → `IsSuccess` false, `Error.Code == "Validation"`, `Value == null`; **no** se invoca al repo. |
| T-U3 | `ExecuteAsync_EmptyDescription_ReturnsValidationFailure` | `Description = ""` o `"   "` (DP-4) → `Validation` sin tocar repo. |
| T-U4 | `ExecuteAsync_FutureDate_AddsFutureDateWarning` | `command.Date > FakeClock.UtcNow.Date` → `IsSuccess` true, `Warnings.Contains("FutureDateWarning")`. |
| T-U5 | `ExecuteAsync_UnknownCategoryId_PropagatesCategoryNotFound` | Fake repo devuelve `Result.Failure("CategoryNotFound", …)` → el use case lo propaga (mismo `Error.Code`); no se pierde. |
| T-U6 | `ExecuteAsync_AddAsyncSucceeds_AuditDatesFromClock` | El DTO retorna `CreatedAt == FakeClock.UtcNow` y `UpdatedAt == CreatedAt`; verifica que el use case sourcing desde `IClock`, no `DateTime.UtcNow`. |

### 9.2 Validador — `RegisterExpenseValidatorTests.cs` (≥ 4)

| # | Método | Escenario |
| --- | --- | --- |
| T-V1 | `Validate_ValidCommand_ReturnsSuccess` | Comando canónico → `IsValid` true. |
| T-V2 | `Validate_ZeroOrNegativeAmount_ReturnsFailure` | `[Theory]` `[InlineData(0)]` `[InlineData(-1)]` → `IsValid` false, error en `Amount`. |
| T-V3 | `Validate_WhitespaceOnlyDescription_ReturnsFailure` | `Description = "   "` o `"\t"` → `IsValid` false (DP-4). |
| T-V4 | `Validate_OverMaxLengthDescription_ReturnsFailure` | 201 caracteres → `IsValid` false. |
| T-V5 (opc.) | `Validate_FutureDate_IsAllowed` | `Date = DateOnly(2099,12,31)` → `IsValid` true (DP-1 — el validator no rechaza). |

## 10. Estructura de archivos esperada

```
src/
├── ControlDeFinanzasIA.Application/
│   ├── Abstractions/                             (sin cambios de F-004)
│   │   ├── IClock.cs                             (Phase 1 F-004)
│   │   ├── IExpenseRepository.cs                 (Phase 1 F-006 + F-003)
│   │   └── ExpenseFilter.cs                      (F-003)
│   ├── Common/                                   (sin cambios)
│   │   ├── Result.cs                             (Phase 1 F-005 — NO se toca)
│   │   └── PagedResult.cs                        (Phase 1 F-005 — NO se toca)
│   ├── DTOs/                                     ← NUEVO
│   │   ├── ExpenseDto.cs                         ← nuevo
│   │   └── ExpenseExtensions.cs                  ← nuevo
│   └── UseCases/Expenses/RegisterExpense/        ← NUEVO (carpeta y tipo)
│       ├── RegisterExpenseCommand.cs             ← nuevo
│       ├── RegisterExpenseValidator.cs           ← nuevo
│       ├── RegisterExpenseUseCase.cs             ← nuevo
│       └── RegisterExpenseResult.cs               ← nuevo
├── ControlDeFinanzasIA.UI/Composition/
│   └── Bootstrap.cs                              ← MODIFICAR (2 AddTransient)
└── (Domain e Infrastructure sin cambios)

tests/
└── ControlDeFinanzasIA.Tests.Unit/
    ├── Application/Expenses/                      ← NUEVO (carpeta)
    │   ├── RegisterExpenseUseCaseTests.cs        ← nuevo
    │   └── RegisterExpenseValidatorTests.cs      ← nuevo
    └── TestDoubles/                              (FakeClock ya existe)
```

## 11. Convenciones

- **Tests:** pure xUnit `[Fact]` / `[Theory]` + `[InlineData]`,
  aserciones `Assert.True` / `Assert.Equal` / `Assert.False` /
  `Assert.NotNull` / `Assert.Contains`. **NO FluentAssertions**
  (confirma el cache `sdd/controldefinanzasia/testing-capabilities` —
  la lib de aserción del proyecto es xUnit puro, distinta de
  FluentValidation que es para validación de input). Naming
  `Method_Scenario_ExpectedResult`.
- **Códigos de error:** en inglés (`"Validation"`, `"CategoryNotFound"`,
  `"NotFound"`, `"FutureDateWarning"`). **Mensajes de error** en español
  (es-DO), eco de `CategoryRepository` (F-004 de Fase 2). **Strings de
  UI:** F-004 no produce UI; las traduce F-006/F-007 con `IStringLocalizer`.
- **FluentValidation 11.x**: reglas `sync` solamente (DP-6); el use
  case llama `_validator.Validate(command)` no `ValidateAsync`. Sin
  `MustAsync`/`CustomAsync` en v1 — evita
  `AsyncValidatorInvokedSynchronouslyException`.
- **Código:** file-scoped namespace, `using` al tope, XML doc en cada
  clase y miembro público, `sealed` en `ExpenseDto`,
  `RegisterExpenseCommand`, `RegisterExpenseValidator`,
  `RegisterExpenseUseCase`, `RegisterExpenseResult`. Async/await
  extremo a extremo con `CancellationToken`.
- **`Result.Success(value)`** (non-generic, eco de F-005 de Fase 1);
  el caller hace `(RegisterExpenseResult)result.Value!` tras verificar
  `IsSuccess`.

## 12. Riesgos

| Riesgo | Probabilidad | Mitigación |
| --- | --- | --- |
| FluentValidation 11 lanza `AsyncValidatorInvokedSynchronouslyException` si un validator async se invoca sync. | Baja | Todas las reglas son sync (DP-6); el use case llama `Validate` no `ValidateAsync`. |
| El warning de fecha futura (`FutureDateWarning`) llega a la UI pero el contract actual no define cómo presentarlo. | Media | F-006/F-007 lo resuelven; F-004 sólo acarrea el `IReadOnlyList<string>` Warnings. Documentar en el XML doc del DTO. |
| `CategoryNotFound` se prop tal cual desde el repo (F-003); el use case no lo renombra. | Baja | El ViewModel (F-007) decide cómo presentarlo; F-004 no inventa un nuevo código. Confirma trazabilidad con F-003 §8 RN-1. |
| `Expense.Create` lanza `ArgumentException` si bypass el validador (edge cases INV no cubiertipp). F-001.GetPropertyCapturaDefensiva. | Baja | LANZAMIENTO → conversión a `Result.Failure("DomainInvariant", ex.Message)` en un try/catch alrededor de `Money.Create`/`Expense.Create`. Decisión a confirmar en design. |
| Confusión `Result` non-generic (F-005 Phase 1 ya cambió `Value: object?`). Callers temptados a usar genérico. | Media | El use case retorna `Result`; caller hace cast. El XML doc de `ExecuteAsync` documenta el patrón. |
| Bootstrap está en `Composition/`, no en raíz UI (path distinto al esperable). | — | Ruta confirmada: `src/ControlDeFinanzasIA.UI/Composition/Bootstrap.cs`. |

## 13. Criterios para considerarla completada

- [ ] `dotnet build -c Release` desde la raíz: 0 warnings, 0 errors.
- [ ] `dotnet test -c Release --no-build`: ~177 + ~10 ≈ **187 tests
      verdes**, 0 failures (134 unit + 43 integration heredados + ~10
      nuevos de Application/Expenses).
- [ ] NetArchTest R-1..R-5 verdes (F-004 sólo añade tipos a
      `Application` + 2 registros DI en composition root `UI`; `Application`
      sigue dependiendo sólo de `Domain` + `FluentValidation`).
- [ ] `dotnet format style --verify-no-changes`: exit 0.
- [ ] F-004 marcada como ✅ COMPLETADA en `spec/phases/phase-03/features.md`
      tras apply + verify; la spec se renombra a
      `F-004-register-expense-use-case.done.md` tras sdd-archive.

## 14. Referencias

- `spec/features/phase-03/F-001-expense-domain-entity.done.md` — entidad
  `Expense`, INV-1..INV-8, factory `Create`.
- `spec/features/phase-03/F-003-expense-repository.done.md` —
  `IExpenseRepository.AddAsync`, códigos `"CategoryNotFound"` /
  `"NotFound"`.
- `spec/features/phase-01/F-005-comuntypes-result-pagedresult.done.md` —
  contrato `Result` (no-genérico, `Value: object?`).
- `spec/features/phase-01/F-006-contrato-interfaces-repo.done.md` —
  interface `IExpenseRepository`.
- `spec/features/phase-01/F-004-Abstracciones-clock-pathresolver.done.md` —
  interface `IClock`.
- `spec/phases/phase-03/features.md` — F-004 en el desglose de Phase 3.
- `docs/architecture.md` §7 (use cases devuelven `Result`),
  §19 (ADR-007 `IClock` inyectado).
- `docs/vision.md` §4.1 (gastos del día a día).
- **FluentValidation 11.x — Sync vs async validators:**
  [docs.fluentvalidation.net/async](https://docs.fluentvalidation.net/en/latest/async.html)
  (reglas sync; `AsyncValidatorInvokedSynchronouslyException`).