# Specification — F-005: `UpdateExpenseUseCase` + `UpdateIncomeUseCase` + `DeleteTransactionUseCase`

> **Fase:** 4 — Cerrar el CRUD de transacciones (Update + Delete)
> **Feature:** F-005 (local a esta fase)
> **Spec ID:** SPEC-F005P4
> **change_id:** `f-005p4-update-delete-use-cases`
> **Modo:** Standard (SDD: propose → spec → tasks → apply → verify → archive)
> **Estado:** Pendiente de implementación (renombrar a `.done.md` tras apply + verify)
>
> Fuentes de verdad: `docs/vision.md`, `docs/architecture.md`,
> `docs/roadmap.md` (§Fase 4), `spec/constitution/tech-stack.md`,
> `spec/constitution/mission.md`,
> `spec/phases/phase-04/features.md` (§RF-7 — líneas 170-175,
> §RF-8 — líneas 177-182),
> `.atl/sdd/f-005p4-update-delete-use-cases/proposal.md` (PRD de
> origen — 219 líneas),
> `spec/features/phase-04/F-003-register-income-use-case.done.md`
> (patrón espejo de use case de escritura),
> `spec/features/phase-03/F-004-register-expense-use-case.done.md`
> (patrón espejo de `RegisterExpenseUseCase`),
> `spec/features/phase-04/F-002-income-repository.done.md`
> (`IIncomeRepository` 6 métodos),
> `spec/features/phase-01/F-005-comuntypes-result-pagedresult.done.md`
> (`Result` non-generic — **no se modifica**),
> `spec/features/phase-01/F-004-Abstracciones-clock-pathresolver.done.md`
> (`IClock`).

---

## 1. Metadata

| Campo | Valor |
| --- | --- |
| **change_id** | `f-005p4-update-delete-use-cases` |
| **spec_id** | `SPEC-F005P4` |
| **modo** | Standard (SDD: propose → spec → tasks → apply → verify → archive) |
| **fase** | 4 |
| **feature** | F-005 (local a Fase 4) — `4-005` en el plan de fase |
| **dependencies (build)** | F-001 Fase 3 (`Expense` + `Expense.Update` 6-param) ✅ archivada; F-001 Fase 4 (`Income` + `Income.Update` 7-param) ✅ archivada; F-003 Fase 3 / F-002 Fase 4 (`IExpenseRepository`/`IIncomeRepository` con `UpdateAsync`/`RemoveAsync`/`GetByIdAsync`) ✅ archivadas; F-004 Fase 3 / F-003 Fase 4 (`RegisterExpenseUseCase`/`RegisterIncomeUseCase` — patrones espejo) ✅ archivadas |
| **dependencies (contrato)** | F-005 Fase 1 (`Result` non-generic, `Value: object?`) ✅; F-004 Fase 1 (`IClock`, `FakeClock`) ✅; F-006 Fase 1 (convención commands `Task<Result>`) ✅ |
| **dependencies (paquetes)** | FluentValidation 11.10.0 (ya referenciado en `Application.csproj` vía CPM) ✅ — F-005 **no añade paquetes** |
| **unblocks** | 4-008 (UI edición/borrado + `IncomeDto` + `ExpenseDto` desde Update) |
| **artifact_store** | Engram (objetivo: `sdd/f-005p4-update-delete-use-cases/spec`) + filesystem (este archivo) |
| **status** | Pendiente de implementación |
| **línea base de tests (pre-F-005)** | Aproximada, post F-004 Fase 4 (Tags) — el implementador reporta la cuenta exacta |

> **Nota Engram:** `mem_save` no está disponible en el toolset del
> sub-agente de spec en esta sesión (verificado: sin funciones `mem_*`
> — mismo precedente que F-002/F-003 P4, ver R-8 de la spec F-003).
> Respaldo en filesystem: proposal en
> `.atl/sdd/f-005p4-update-delete-use-cases/proposal.md` y esta spec en
> `spec/features/phase-04/F-005-update-delete-use-cases.spec.md`. El
> orquestador backfillará a Engram con los topic keys
> `sdd/f-005p4-update-delete-use-cases/{proposal,spec}` (precedente
> `f-002p4`/`f-003p4`). El sub-agente **no fabrica observation IDs**.

---

## 2. Contexto y objetivo

Phase 3 y las features 4-001..4-004 entregaron el **Create + Read** del
slice vertical de transacciones: `RegisterExpenseUseCase` (Fase 3
F-004), `RegisterIncomeUseCase` (Fase 4 F-003), `GetTransactionsQuery`
(Fase 3 F-005), y los repos con `AddAsync`/`GetByIdAsync`/
`GetByFilterAsync`/`UpdateAsync`/`RemoveAsync` (6 métodos cada uno,
completos desde F-002/F-003). La entidad `Tag` y el M:N `ExpenseTags`
(Fase 4 F-004) también están listos. Lo que falta para cerrar el CRUD
es el **Update** (editar gasto e ingreso) y el **Delete** (borrado
genérico por tipo de transacción). Sin estos, el usuario no puede
corregir un registro erróneo ni eliminarlo: el ciclo de vida de una
transacción queda truncado en Create/Read, y la UI de edición/borrado
(4-008) no tiene backend que invocar.

F-005 es un **espejo estricto** de los use cases de registro
(`RegisterExpenseUseCase`/`RegisterIncomeUseCase`), reutilizando los
`Update(...)` de dominio **ya existentes** (Fase 3 F-001 para `Expense`,
Fase 4 F-001 para `Income`) y los `UpdateAsync`/`RemoveAsync`/
`GetByIdAsync` de los repos **ya existentes** (Fase 3 F-003 / Fase 4
F-002). **No se modifican entidades, repos, configuraciones ni
migraciones.** La novedad es pura orquestación de Application: validar
el comando → buscar el agregado por Id → invocar `entity.Update(...)`
(revalida invariantes) → persistir, propagando los `Result` del repo. El
Delete añade un discriminador `TransactionType` (`Domain.Enums`) y un
dispatch por `switch` al repo correspondiente.

**Diferencias semánticas clave (verificadas en código, ver §3):**
- `Expense.Update` es **6 params** (sin `source` — `Expense` no tiene
  propiedad `Source`); `UpdateExpenseCommand.CategoryId` es **`Guid`**
  (requerido — un gasto siempre tiene categoría).
- `Income.Update` es **7 params** (con `source` — `Income` sí tiene
  `Source`); `UpdateIncomeCommand.CategoryId` es **`Guid?`** (nullable
  — un ingreso sin categoría es válido). `UpdateIncomeUseCase` pasa
  `null` a `source` (reset — ver Q4).
- Update retorna **`Result.Success(null!)`** sin payload ni warnings
  (sin DTO — `IncomeDto`/`ExpenseDto` desde Update son scope 4-008).
- Delete no usa FluentValidation: guard inline `Id != Guid.Empty`.

**Objetivos concretos:**

1. **Crear 8 tipos nuevos de producción** + 1 enum de dominio:
   `TransactionType` (enum), `UpdateExpenseCommand`/`Validator`/`UseCase`,
   `UpdateIncomeCommand`/`Validator`/`UseCase`,
   `DeleteTransactionCommand`/`UseCase`.
2. **Establecer los patrones de Update y Delete** para transacciones
   (espejo de Register con lookup por Id + `entity.Update`), reutilizando
   `IClock`, `IExpenseRepository`/`IIncomeRepository`, `Result`
   non-generic y FluentValidation ya referenciados.
3. **Registrar DI** en `Bootstrap.ConfigureServices` (UI composition
   root — 3 use cases + 2 validators Transient).
4. **Extender los fakes** `FakeExpenseRepository`/`FakeIncomeRepository`
   con `SetUpdateResult`/`SetRemoveResult`/`SetGetByIdResult` (test
   doubles — necesarios para tests de NotFound/propagación).
5. **Cubrir con ≥10 tests unitarios** (≥3 UpdateExpense, ≥3 UpdateIncome,
   ≥4 DeleteTransaction) usando los fakes extendidos, `FakeClock`,
   `NullLogger<>` y validadores reales; pure xUnit `Assert.*`.

Al final de la feature: `dotnet build` limpio (Debug+Release); `dotnet
test` suite previa + ~12-15 nuevos verdes; NetArchTest R-1..R-5 verdes;
`dotnet format style --verify-no-changes` exit 0.

---

## 3. Decisiones (locked-in — no re-abrir)

### C-1 — `Expense.Update` tiene **6 params**, NO 7; `Expense` NO tiene `Source`

`src/.../Domain/Transactions/Expense.cs` (líneas 161-167, verificado):

```csharp
public void Update(Guid categoryId, DateOnly date, Money amount,
                   string description, string? note, DateTime updatedAt)
```

- **No existe** parámetro `source`. `Expense` no tiene propiedad
  `Source` (sólo `Income` la tiene). Una llamada con 7 args (un `null`
  extra como source) **no compila**.
- **Llamada correcta (locked):** `existing.Update(command.CategoryId,
  command.Date, money, command.Description, command.Note, audit)` — 6
  args posicionales, sin `source`.

### C-2 — `UpdateExpenseCommand.CategoryId` es `Guid` (requerido), NO `Guid?`

- `Expense.CategoryId` es `Guid` (required); `Expense.Update` toma `Guid
  categoryId` y lanza `ArgumentException` en `Guid.Empty` (líneas
  258-263); `RegisterExpenseCommand.CategoryId` es `Guid` (required).
- El phase plan RF-7 (línea 171) dice `Guid? CategoryId` para
  `UpdateExpenseCommand` — es un **copy-paste** de la variante `Income`
  (donde `Guid?` es correcto). Es semánticamente inconsistente con el
  invariante de `Expense` (un gasto **requiere** categoría).
- **Decisión (locked):** `UpdateExpenseCommand.CategoryId` es `Guid`
  (required), espejo exacto de `RegisterExpenseCommand` + `Id`
  prependido. `UpdateIncomeCommand.CategoryId` SÍ es `Guid?` (nullable,
  espejo de `RegisterIncomeCommand`).

### C-3 — Los tests siguen la convención `Application/<Aggregate>/<UseCase>/` (sin segmento `UseCases`)

- El contrato de lanzamiento lista los tests en
  `tests/.../Application/UseCases/Expenses/UpdateExpense/...` — **no
  coincide** con la convención real del repo. Verificado: los tests
  existentes viven en `Application/<Aggregate>/<UseCase>/` **sin** el
  segmento `UseCases`:
  - `tests/.../Application/Incomes/RegisterIncome/RegisterIncomeUseCaseTests.cs`
  - `tests/.../Application/Expenses/RegisterExpense/RegisterExpenseUseCaseTests.cs`
  - `tests/.../Application/Expenses/RegisterExpenseValidatorTests.cs`
  - `tests/.../Application/Expenses/GetTransactionsQueryTests.cs`
- **Decisión (locked):** los 3 archivos de tests nuevos se ubican en
  `tests/ControlDeFinanzasIA.Tests.Unit/Application/Expenses/UpdateExpense/`,
  `.../Application/Incomes/UpdateIncome/`,
  `.../Application/Transactions/DeleteTransaction/` con namespaces
  `ControlDeFinanzasIA.Tests.Unit.Application.Expenses.UpdateExpense`
  (etc.). La estructura de **producción** sí conserva el segmento
  `UseCases/` (`src/.../Application/UseCases/Expenses/UpdateExpense/`).

### D-1 — `Result` es non-generic; NO existe `Result<T>`

`src/.../Application/Common/Result.cs` declara `public sealed record
Result` con `Value: object?` y fábricas `Success(object)` /
`Failure(Error)` / `Failure(code, message)` / `Failure(code, message,
details)`. **No hay `Result<T>`.**

- `ExecuteAsync` retorna **`Task<Result>`** (non-generic) en los 3 use
  cases.
- Update éxito → `Result.Success(null!)` (sin payload — `Success(object
  value)` acepta `null!`, línea 38). El caller sólo inspecciona
  `IsSuccess`.
- Delete éxito → propaga el `Result` del repo (que es
  `Result.Success(null!)` por convención del repo real).
- Fallo de repo (Update/Delete) → `return repoResult;` (propaga el
  `Result` non-generic tal cual, con su `Error` — p. ej. `"NotFound"`).

### D-2 — Validación sync `_validator.Validate(command)`, NO `ValidateAsync`

Espejo de F-004 P3 / F-003 P4 D-4: todas las reglas son sync
(`RuleFor`, `NotEmpty`, `GreaterThan`, `MaximumLength`, `Must`,
`IsInEnum`, `When`, `NotEqual`). El use case invoca
`_validator.Validate(command)`. Sin `MustAsync`/`CustomAsync` → evita
`AsyncValidatorInvokedSynchronouslyException` (breaking change de
FluentValidation 11.0). Delete **no** usa FluentValidation (guard
inline — ver D-7).

### D-3 — Fallo de validación con `details` por campo (no un `string.Join` plano)

```csharp
IReadOnlyDictionary<string, string[]> details = validation.Errors
    .GroupBy(e => e.PropertyName)
    .ToDictionary(
        g => g.Key,
        g => g.Select(e => e.ErrorMessage).ToArray(),
        StringComparer.Ordinal);

return Result.Failure(
    "Validation",
    "El comando para actualizar el gasto no es válido.",  // o "el ingreso"
    details);
```

Preserva la estructura por campo que la UI (4-008) necesita para mapear
errores a inputs. Espejo de `RegisterExpenseUseCase` líneas 63-73.

### D-4 — `try/catch(ArgumentException)` alrededor de `Money.Create`/`entity.Update` → `"DomainInvariant"`

`Expense.Update`/`Income.Update`/`Money.Create` lanzan
`ArgumentException`/`ArgumentNullException` en invariantes no cubiertas
por el validador (p. ej. `date < 1900-01-01`, `Guid.Empty` si se escapa
del validador, `updatedAt < CreatedAt`). Sin el catch, una excepción de
dominio escapa del use case. **Decisión:** envolver `Money.Create` +
`entity.Update` en `try { ... } catch (ArgumentException ex) {
_logger.LogWarning(ex, "Domain invariant violation while updating
{entity}."); return Result.Failure("DomainInvariant", ex.Message); }`.
`ArgumentNullException` deriva de `ArgumentException` → cubierta. Espejo
de Register D-6.

### D-5 — `Description`/`Note` se pasan **sin trim** a `entity.Update`

La entidad normaliza (trima `Description`, normaliza `Note`/`Source` a
`null` si whitespace — `Expense.cs`/`Income.cs` `ValidateAndNormalize`).
Un `.Trim()` manual en el use case sería redundante (doble-trim). El
validador **sólo chequea** `Must(d => d.Trim().Length > 0)` (no muta).
Espejo de F-004 P3 DP-3/DP-4 y F-003 D-7.

### D-6 — `UpdateIncomeUseCase` pasa `null` a `source` (reset — Q4)

`Income.Update` recibe `source` (7º param). El use case lo pasa `null`:
editar cualquier campo **resetea** `Source` a `null` si estaba seteado.
Esto es una **limitación conocida y aceptada** (Q4 resuelta): preservar
el `Source` existente requeriría leer `income.Source` de la entidad
cargada vía `GetByIdAsync` y pasarlo — trivial pero **fuera del
contrato cached**. El seteo deliberado de `Source` vía Update se
diferiere (ver §7 y R-3). `null` es un estado válido y soportado por
`Income` (INV-6: `Source` se normaliza a `null` si whitespace).

### D-7 — Delete sin FluentValidation: guard inline `Id != Guid.Empty` (Q5)

El contrato AC-10 decía "3 validators" pero sólo hay **2** FluentValidation
(UpdateExpense/UpdateIncome); Delete usa un guard inline. **Decisión
(Q5 resuelta):** `DeleteTransactionUseCase` hace `if (command.Id ==
Guid.Empty) return Result.Failure("Validation", "Id cannot be empty.");`
antes del `switch`. No se crea un 3er `AbstractValidator`. Razón: la
única validación de `DeleteTransactionCommand` es `Id != Guid.Empty`
(el `Type` es un enum válido por construcción; el rango inválido se
cubrió con `default` del switch — ver D-8). Un validator de 1 regla
sería sobrecarga; el inline es consistente con "errors as values" y
más simple. Se documenta en el XML doc del use case.

### D-8 — `switch` con `default` defensivo para `TransactionType` inválido

`TransactionType` es un enum (`Expense`, `Income`). Un valor fuera de
rango (p. ej. `(TransactionType)999`, posible por casting) cae en el
`default` del `switch` → `Result.Failure("Validation",
$"Unknown transaction type: {command.Type}.")`. Es defensivo (con un
enum real nunca debería ocurrir), pero cubre futuros valores del enum
añadidos sin actualizar el switch. Test cubre la rama (T-D5).

### D-9 — Update no detecta ni surfacea `FutureDateWarning` (Q3)

Register detecta fecha futura y la devuelve como warning en el result.
Update **no** lo hace: retorna `Result.Success(null!)` sin payload ni
warnings. Razón (Q3 resuelta): no hay `UpdateResult`/DTO (eso es scope
4-008), y añadir un `UpdateResult(Warnings)` mínimo ahora sería scope
creep de DTOs. **Inconsistencia menor aceptada:** editar un gasto/ingreso
con fecha futura se acepta silenciosamente (no hay warning). Diferido a
4-008 cuando la UI de edición construya el result con DTO + warnings.
Documentado en R-2.

### D-ML — Idioma de mensajes (decisión de consistencia)

La convención del codebase (verificada en `RegisterExpenseValidator`,
`RegisterIncomeValidator`, `RegisterIncomeUseCaseTests`) es: **códigos
de error en inglés** (`"Validation"`, `"NotFound"`, `"DomainInvariant"`,
`"CategoryNotFound"`) + **mensajes humanos en español** (validadores,
resúmenes de use case, mensajes de repo). Las invariantes de dominio
que propagan vía `ex.Message` son **inglés** (el dominio lanza en
inglés, p. ej. `"Amount must be strictly positive."`). F-005 sigue esa
misma convención:

| Mensaje | Idioma | Origen |
| --- | --- | --- |
| FluentValidation por-campo (CategoryId, Amount, Description, Note, Currency) | **Español** | espejo de `RegisterExpenseValidator`/`RegisterIncomeValidator` (instrucción locked "mirror Register…Validator") |
| FluentValidation regla `Id` (nueva) | **Español** | "El identificador es obligatorio." (paridad con "La categoría es obligatoria.") |
| Resumen de fallo de validación (use case) | **Español** | "El comando para actualizar el gasto/ingreso no es válido." (espejo de Register) |
| `NotFound` (use case Update) | **Inglés** | `"Expense {Id} does not exist."` / `"Income {Id} does not exist."` (contrato AC-4 explícito; sin precedente en Register) |
| Guards de `DeleteTransactionUseCase` | **Inglés** | `"Id cannot be empty."`, `"Unknown transaction type: {Type}."` (contrato AC-9 explícito) |
| `DomainInvariant` | **Inglés** | `ex.Message` (del dominio) |
| Códigos de error | **Inglés** | convención del codebase |

> **Nota:** el contrato AC-3 bosquejó los mensajes por-campo del
> validador en inglés, pero la instrucción locked dice "mirror
> `RegisterExpenseValidator`". Estos dos signals conflictúan; la
> decisión es **espejar los mensajes en español** de los validadores
> Register (consistencia dentro de la familia de validadores) + un
> mensaje español nuevo para la regla `Id`. Los mensajes en inglés del
> bosquejo AC-3 se consideran ilustrativos. Si el usuario prefiere
> inglés para los nuevos, se corrige en revisión. Ver R-4.

### Otras decisiones de forma

- **`TransactionType` vive en `ControlDeFinanzasIA.Domain.Enums`** (mismo
  namespace que `Currency`, verificado) — sin valores ordinales
  explícitos (`{ Expense, Income }`, ordinales implícitos 0/1), XML doc
  en el tipo + cada valor (espejo del estilo de `Currency`).
- **`UpdateExpenseUseCase`/`UpdateIncomeUseCase`** referencian
  `Application.Abstractions`, `Application.Common`, `Domain.Transactions`,
  `Domain.ValueObjects`, `FluentValidation`, `FluentValidation.Results`,
  `Microsoft.Extensions.Logging` (sin `Application.DTOs` — no hay DTO).
- **`DeleteTransactionUseCase`** referencia `Application.Abstractions`,
  `Application.Common`, `Microsoft.Extensions.Logging` (sin
  `FluentValidation` — guard inline, D-7; sin `Domain.Transactions` —
  no toca entidades).
- **Ningún paquete NuGet nuevo.** FluentValidation ya referenciado en
  `Application.csproj` vía CPM. Sin `.csproj` modificado.

### Respuestas a las 5 preguntas del proposal (locked)

| # | Pregunta | Respuesta locked |
| --- | --- | --- |
| Q1 | `UpdateExpenseCommand.CategoryId` `Guid` vs `Guid?` | **`Guid`** (required) — C-2 |
| Q2 | `Expense.Update` 6-param vs 7-param | **6 args** (sin source) — C-1 |
| Q3 | Update sin payload/warnings vs `UpdateResult(Warnings)` mínimo | **`Result.Success(null!)`** sin payload/warnings; inconsistencia menor con Register aceptada hasta 4-008 — D-9 |
| Q4 | `UpdateIncomeUseCase` resetea `Source` a `null` vs preserva existente | **`null`** (reset) — D-6 |
| Q5 | "3 validators": 2 FluentValidation + guard inline vs 3 FluentValidation | **2 FluentValidation + 1 guard inline** en `DeleteTransactionUseCase` — D-7 |

---

## 4. Criterios de aceptación

La feature se considera completada cuando **todos** los ACs siguientes
se cumplen, verificables mediante tests automatizados y revisión de
código. Cada AC incluye escenarios Given/When/Then. **Las formas
corregidas (C-1, C-2, C-3, D-1..D-9, D-ML) son las que rigen — no el
bosquejo original del contrato.**

### AC-1 — `TransactionType` enum

- [ ] `src/ControlDeFinanzasIA.Domain/Enums/TransactionType.cs` existe
      como `public enum TransactionType { Expense, Income }` (sin
      valores ordinales explícitos).
- [ ] File-scoped namespace `ControlDeFinanzasIA.Domain.Enums`; XML doc
      en el tipo + cada valor (`Expense`, `Income`).
- [ ] Usado por `DeleteTransactionCommand.Type` (AC-8).

```csharp
namespace ControlDeFinanzasIA.Domain.Enums;

/// <summary>
/// Discrimina el tipo de transacción para el dispatch de borrado
/// en <c>DeleteTransactionUseCase</c>.
/// </summary>
public enum TransactionType
{
    /// <summary>Gasto (agregado <c>Expense</c>).</summary>
    Expense,

    /// <summary>Ingreso (agregado <c>Income</c>).</summary>
    Income
}
```

- **GIVEN** el enum declarado en `Domain.Enums`.
- **WHEN** `DeleteTransactionCommand` referencia `TransactionType`.
- **THEN** compila; `TransactionType.Expense` y `TransactionType.Income`
  son los únicos valores válidos; `(TransactionType)999` es legal en
  compilación pero cae al `default` del switch (D-8).

### AC-2 — `UpdateExpenseCommand` record

- [ ] `src/ControlDeFinanzasIA.Application/UseCases/Expenses/UpdateExpense/UpdateExpenseCommand.cs`
      existe como `public sealed record UpdateExpenseCommand(
      Guid Id, Guid CategoryId, DateOnly Date, decimal Amount,
      Currency Currency, string Description, string? Note)` — 7 props.
- [ ] **`Guid CategoryId` es REQUIRED (no `Guid?`)** per C-2.
- [ ] File-scoped namespace
      `ControlDeFinanzasIA.Application.UseCases.Expenses.UpdateExpense`;
      `using ControlDeFinanzasIA.Domain.Enums;`; XML doc (documenta que
      `CategoryId` es required y que `Source` no aplica a Expense).

```csharp
using ControlDeFinanzasIA.Domain.Enums;

namespace ControlDeFinanzasIA.Application.UseCases.Expenses.UpdateExpense;

/// <summary>
/// Immutable command to update an existing expense. Carries the
/// aggregate identity (<see cref="Id"/>) plus the mutable fields.
/// <see cref="CategoryId"/> is required (<c>Guid</c>, not nullable):
/// an expense always belongs to a category (mirrors
/// <c>RegisterExpenseCommand</c>). <c>Source</c> is not modeled —
/// <c>Expense</c> has no <c>Source</c> property.
/// </summary>
public sealed record UpdateExpenseCommand(
    Guid Id,
    Guid CategoryId,
    DateOnly Date,
    decimal Amount,
    Currency Currency,
    string Description,
    string? Note);
```

- **GIVEN** el proyecto compila con este archivo presente.
- **WHEN** un caller construye `new UpdateExpenseCommand(id, catId, date, 100m, Currency.DOP, "Almuerzo", null)`.
- **THEN** las 7 props se asignan; `CategoryId` es `Guid` (no nullable).

### AC-3 — `UpdateExpenseValidator` (6 reglas sync)

- [ ] `src/.../UseCases/Expenses/UpdateExpense/UpdateExpenseValidator.cs`
      es `public sealed class UpdateExpenseValidator : AbstractValidator<UpdateExpenseCommand>`.
- [ ] File-scoped namespace; `using FluentValidation;`
      `using ControlDeFinanzasIA.Domain.Enums;`; XML doc en la clase.
- [ ] **6 reglas sync** (D-2): `Id`, `CategoryId` (**incondicional** —
      C-2, `Guid` required), `Amount`, `Description`, `Note`, `Currency`.
      Sin `MustAsync`/`CustomAsync`. Mensajes por-campo en **español**
      (D-ML): espejo de `RegisterExpenseValidator` + regla `Id` nueva.

```csharp
using ControlDeFinanzasIA.Domain.Enums;
using FluentValidation;

namespace ControlDeFinanzasIA.Application.UseCases.Expenses.UpdateExpense;

/// <summary>
/// Validates a <see cref="UpdateExpenseCommand"/> using synchronous
/// FluentValidation rules. Mirrors <c>RegisterExpenseValidator</c> and
/// adds an <c>Id != Guid.Empty</c> rule. Async validators are
/// intentionally avoided to prevent
/// <c>AsyncValidatorInvokedSynchronouslyException</c>.
/// </summary>
public sealed class UpdateExpenseValidator : AbstractValidator<UpdateExpenseCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateExpenseValidator"/> class.
    /// </summary>
    public UpdateExpenseValidator()
    {
        RuleFor(c => c.Id)
            .NotEqual(Guid.Empty).WithMessage("El identificador es obligatorio.");

        RuleFor(c => c.CategoryId)
            .NotEqual(Guid.Empty).WithMessage("La categoría es obligatoria.");

        RuleFor(c => c.Amount)
            .GreaterThan(0).WithMessage("El monto debe ser mayor que 0.");

        RuleFor(c => c.Description)
            .NotEmpty().WithMessage("La descripción es obligatoria.")
            .MaximumLength(200).WithMessage("La descripción no puede superar 200 caracteres.")
            .Must(d => d.Trim().Length > 0).WithMessage("La descripción no puede contener solo espacios en blanco.");

        RuleFor(c => c.Note)
            .MaximumLength(500).When(c => c.Note is not null).WithMessage("La nota no puede superar 500 caracteres.");

        RuleFor(c => c.Currency)
            .IsInEnum().WithMessage("La moneda no es válida.");
    }
}
```

- **GIVEN** un comando con `Id = Guid.Empty`.
- **WHEN** `_validator.Validate(command)`.
- **THEN** `IsValid == false` y `Errors` contiene
  `"El identificador es obligatorio."` en `PropertyName == "Id"`.
- **GIVEN** un comando con `CategoryId = Guid.Empty`.
- **WHEN** `_validator.Validate(command)`.
- **THEN** `IsValid == false` (regla incondicional — C-2).

### AC-4 — `UpdateExpenseUseCase` sealed class + `ExecuteAsync` (6 args, C-1)

- [ ] `src/.../UseCases/Expenses/UpdateExpense/UpdateExpenseUseCase.cs`
      es `public sealed class UpdateExpenseUseCase`.
- [ ] Campos privados readonly: `IExpenseRepository _expenseRepository`,
      `IClock _clock`, `IValidator<UpdateExpenseCommand> _validator`,
      `ILogger<UpdateExpenseUseCase> _logger`.
- [ ] Constructor `public UpdateExpenseUseCase(IExpenseRepository
      expenseRepository, IClock clock, IValidator<UpdateExpenseCommand>
      validator, ILogger<UpdateExpenseUseCase> logger)`.
- [ ] `public async Task<Result> ExecuteAsync(UpdateExpenseCommand
      command, CancellationToken ct = default)` — non-generic (D-1).
- [ ] Flujo (D-1..D-5): `ArgumentNullException.ThrowIfNull(command)` →
      `_validator.Validate(command)` sync → si inválido
      `Result.Failure("Validation", "El comando para actualizar el gasto
      no es válido.", details)` (D-3) → `GetByIdAsync(command.Id, ct)` →
      si `null` `Result.Failure("NotFound", $"Expense {command.Id} does
      not exist.")` → `try { money = Money.Create(...);
      existing.Update(command.CategoryId, command.Date, money,
      command.Description, command.Note, _clock.UtcNow); }` (**6 args**
      — C-1, sin source) `catch (ArgumentException)` →
      `Result.Failure("DomainInvariant", ex.Message)` (D-4) →
      `UpdateAsync(existing, ct)` → propagar `Result` →
      `Result.Success(null!)` (D-9).
- [ ] File-scoped namespace; `using` al tope; XML doc en la clase
      (documenta `Result.Success(null!)` sin payload).

```csharp
public async Task<Result> ExecuteAsync(UpdateExpenseCommand command, CancellationToken ct = default)
{
    ArgumentNullException.ThrowIfNull(command);

    ValidationResult validation = _validator.Validate(command);   // sync (D-2)
    if (!validation.IsValid)
    {
        IReadOnlyDictionary<string, string[]> details = validation.Errors   // D-3
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray(),
                StringComparer.Ordinal);

        return Result.Failure(
            "Validation",
            "El comando para actualizar el gasto no es válido.",
            details);
    }

    Expense? existing = await _expenseRepository.GetByIdAsync(command.Id, ct);
    if (existing is null)
    {
        return Result.Failure("NotFound", $"Expense {command.Id} does not exist.");
    }

    DateTime audit = _clock.UtcNow;                                 // ADR-007; se lee una vez
    try
    {
        var money = Money.Create(command.Amount, command.Currency);
        existing.Update(
            command.CategoryId,    // Guid (C-2) — 6 args (C-1), SIN source
            command.Date,
            money,
            command.Description,   // as-is (D-5)
            command.Note,          // as-is (D-5)
            audit);
    }
    catch (ArgumentException ex)    // D-4; cubre ArgumentNullException
    {
        _logger.LogWarning(ex, "Domain invariant violation while updating expense.");
        return Result.Failure("DomainInvariant", ex.Message);
    }

    Result updateResult = await _expenseRepository.UpdateAsync(existing, ct);
    if (!updateResult.IsSuccess)
    {
        return updateResult;        // propaga "NotFound"/"CategoryNotFound" del repo
    }

    return Result.Success(null!);   // sin payload, sin warnings (D-9)
}
```

- **GIVEN** un comando válido + un `Expense` sembrado en el fake (`SetGetByIdResult`).
- **WHEN** `ExecuteAsync(command)` con `command.Id == seeded.Id`.
- **THEN** `IsSuccess == true`, `Value == null`; el `Expense` sembrado
  fue mutado in-place (`CategoryId`/`Description`/`UpdatedAt` == command/clock).
- **GIVEN** `SetGetByIdResult(null)` (gasto inexistente).
- **WHEN** `ExecuteAsync(command)`.
- **THEN** `IsSuccess == false`, `Error.Code == "NotFound"`.
- **GIVEN** un comando con `Amount = 0`.
- **WHEN** `ExecuteAsync(command)`.
- **THEN** `IsSuccess == false`, `Error.Code == "Validation"`,
  `Error.Details` contiene `"Amount"`; el repo **no** fue invocado.

### AC-5 — `UpdateIncomeCommand` record

- [ ] `src/.../UseCases/Incomes/UpdateIncome/UpdateIncomeCommand.cs`
      existe como `public sealed record UpdateIncomeCommand(
      Guid Id, Guid? CategoryId, DateOnly Date, decimal Amount,
      Currency Currency, string Description, string? Note)` — 7 props.
- [ ] **`Guid? CategoryId` es nullable** (matches `Income` +
      `RegisterIncomeCommand`). Sin `Source` en el comando — el use
      case lo resetea a `null` (D-6, Q4).
- [ ] File-scoped namespace
      `ControlDeFinanzasIA.Application.UseCases.Incomes.UpdateIncome`;
      `using ControlDeFinanzasIA.Domain.Enums;`; XML doc (documenta
      `CategoryId` nullable + que `Source` se resetea, diferido a 4-008).

```csharp
using ControlDeFinanzasIA.Domain.Enums;

namespace ControlDeFinanzasIA.Application.UseCases.Incomes.UpdateIncome;

/// <summary>
/// Immutable command to update an existing income. Carries the
/// aggregate identity (<see cref="Id"/>) plus the mutable fields.
/// <see cref="CategoryId"/> may be null (an income without a category
/// is valid — mirrors <c>RegisterIncomeCommand</c>). <c>Source</c> is
/// not modeled here: <c>UpdateIncomeUseCase</c> resets it to null
/// (setting the source is deferred — see spec §7).
/// </summary>
public sealed record UpdateIncomeCommand(
    Guid Id,
    Guid? CategoryId,
    DateOnly Date,
    decimal Amount,
    Currency Currency,
    string Description,
    string? Note);
```

- **GIVEN** el proyecto compila con este archivo presente.
- **WHEN** un caller construye `new UpdateIncomeCommand(id, null, date, 100m, Currency.DOP, "Sueldo", null)`.
- **THEN** las 7 props se asignan; `CategoryId == null` es válido.

### AC-6 — `UpdateIncomeValidator` (6 reglas sync, `CategoryId` condicional)

- [ ] `src/.../UseCases/Incomes/UpdateIncome/UpdateIncomeValidator.cs`
      es `public sealed class UpdateIncomeValidator : AbstractValidator<UpdateIncomeCommand>`.
- [ ] File-scoped namespace; `using FluentValidation;`
      `using ControlDeFinanzasIA.Domain.Enums;`; XML doc en la clase.
- [ ] **6 reglas sync**: `Id`, `CategoryId` (**condicional**
      `.When(c => c.CategoryId is not null)` — espejo de la 5ª regla de
      `RegisterIncomeValidator`), `Amount`, `Description`, `Note`,
      `Currency`. Mensajes por-campo en **español** (D-ML).

```csharp
using ControlDeFinanzasIA.Domain.Enums;
using FluentValidation;

namespace ControlDeFinanzasIA.Application.UseCases.Incomes.UpdateIncome;

/// <summary>
/// Validates a <see cref="UpdateIncomeCommand"/> using synchronous
/// FluentValidation rules. Mirrors <c>RegisterIncomeValidator</c> and
/// adds an <c>Id != Guid.Empty</c> rule. Async validators are
/// intentionally avoided to prevent
/// <c>AsyncValidatorInvokedSynchronouslyException</c>.
/// </summary>
/// <remarks>
/// <see cref="UpdateIncomeCommand.CategoryId"/> is nullable: the
/// <c>NotEqual(Guid.Empty)</c> rule only applies when a category is
/// supplied (<c>.When(c => c.CategoryId is not null)</c>). A null
/// category is valid (an income may have no category).
/// </remarks>
public sealed class UpdateIncomeValidator : AbstractValidator<UpdateIncomeCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateIncomeValidator"/> class.
    /// </summary>
    public UpdateIncomeValidator()
    {
        RuleFor(c => c.Id)
            .NotEqual(Guid.Empty).WithMessage("El identificador es obligatorio.");

        RuleFor(c => c.CategoryId)
            .NotEqual(Guid.Empty)
            .When(c => c.CategoryId is not null)
            .WithMessage("La categoría no es válida.");

        RuleFor(c => c.Amount)
            .GreaterThan(0).WithMessage("El monto debe ser mayor que 0.");

        RuleFor(c => c.Description)
            .NotEmpty().WithMessage("La descripción es obligatoria.")
            .MaximumLength(200).WithMessage("La descripción no puede superar 200 caracteres.")
            .Must(d => d.Trim().Length > 0).WithMessage("La descripción no puede contener solo espacios en blanco.");

        RuleFor(c => c.Note)
            .MaximumLength(500).When(c => c.Note is not null).WithMessage("La nota no puede superar 500 caracteres.");

        RuleFor(c => c.Currency)
            .IsInEnum().WithMessage("La moneda no es válida.");
    }
}
```

- **GIVEN** un comando con `CategoryId = null`.
- **WHEN** `_validator.Validate(command)`.
- **THEN** `IsValid == true` (la regla `CategoryId` se omite por `.When`).
- **GIVEN** un comando con `Id = Guid.Empty`.
- **WHEN** `_validator.Validate(command)`.
- **THEN** `IsValid == false`; `Errors` contiene `"El identificador es obligatorio."`.

### AC-7 — `UpdateIncomeUseCase` sealed class + `ExecuteAsync` (7 args, `null` source — D-6)

- [ ] `src/.../UseCases/Incomes/UpdateIncome/UpdateIncomeUseCase.cs`
      es `public sealed class UpdateIncomeUseCase`.
- [ ] Campos privados readonly: `IIncomeRepository _incomeRepository`,
      `IClock _clock`, `IValidator<UpdateIncomeCommand> _validator`,
      `ILogger<UpdateIncomeUseCase> _logger`.
- [ ] Constructor `public UpdateIncomeUseCase(IIncomeRepository
      incomeRepository, IClock clock, IValidator<UpdateIncomeCommand>
      validator, ILogger<UpdateIncomeUseCase> logger)`.
- [ ] `public async Task<Result> ExecuteAsync(UpdateIncomeCommand
      command, CancellationToken ct = default)` — non-generic (D-1).
- [ ] **Espejo de AC-4** con `IIncomeRepository` y la llamada
      `existing.Update(command.CategoryId, command.Date, money,
      command.Description, command.Note, null, audit)` — **7 args**
      (`null` para `source` — D-6/Q4). `NotFound` message: `$"Income
      {command.Id} does not exist."`. Resumen de validación: "El comando
      para actualizar el ingreso no es válido."

```csharp
public async Task<Result> ExecuteAsync(UpdateIncomeCommand command, CancellationToken ct = default)
{
    ArgumentNullException.ThrowIfNull(command);

    ValidationResult validation = _validator.Validate(command);
    if (!validation.IsValid)
    {
        IReadOnlyDictionary<string, string[]> details = validation.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray(),
                StringComparer.Ordinal);

        return Result.Failure(
            "Validation",
            "El comando para actualizar el ingreso no es válido.",
            details);
    }

    Income? existing = await _incomeRepository.GetByIdAsync(command.Id, ct);
    if (existing is null)
    {
        return Result.Failure("NotFound", $"Income {command.Id} does not exist.");
    }

    DateTime audit = _clock.UtcNow;
    try
    {
        var money = Money.Create(command.Amount, command.Currency);
        existing.Update(
            command.CategoryId,    // Guid? (nullable)
            command.Date,
            money,
            command.Description,
            command.Note,
            null,                  // source — reset a null (D-6, Q4)
            audit);                // 7 args
    }
    catch (ArgumentException ex)
    {
        _logger.LogWarning(ex, "Domain invariant violation while updating income.");
        return Result.Failure("DomainInvariant", ex.Message);
    }

    Result updateResult = await _incomeRepository.UpdateAsync(existing, ct);
    if (!updateResult.IsSuccess)
    {
        return updateResult;
    }

    return Result.Success(null!);
}
```

- **GIVEN** un comando válido + un `Income` sembrado (con `Source` seteado).
- **WHEN** `ExecuteAsync(command)`.
- **THEN** `IsSuccess == true`; el `Income` sembrado fue mutado:
  `Source == null` (reset — D-6), `Description`/`UpdatedAt` actualizados.
- **GIVEN** `SetGetByIdResult(null)`.
- **WHEN** `ExecuteAsync(command)`.
- **THEN** `IsSuccess == false`, `Error.Code == "NotFound"`.

### AC-8 — `DeleteTransactionCommand` record

- [ ] `src/.../UseCases/Transactions/DeleteTransaction/DeleteTransactionCommand.cs`
      existe como `public sealed record DeleteTransactionCommand(
      Guid Id, TransactionType Type)` — 2 props.
- [ ] File-scoped namespace
      `ControlDeFinanzasIA.Application.UseCases.Transactions.DeleteTransaction`;
      `using ControlDeFinanzasIA.Domain.Enums;`; XML doc (documenta
      que `Type` dispatcha el borrado).

```csharp
using ControlDeFinanzasIA.Domain.Enums;

namespace ControlDeFinanzasIA.Application.UseCases.Transactions.DeleteTransaction;

/// <summary>
/// Immutable command to delete a transaction by identity and type.
/// <see cref="Type"/> dispatches the hard delete to the matching
/// repository (<c>IExpenseRepository</c> or <c>IIncomeRepository</c>).
/// </summary>
public sealed record DeleteTransactionCommand(
    Guid Id,
    TransactionType Type);
```

- **GIVEN** el comando declarado.
- **WHEN** `new DeleteTransactionCommand(id, TransactionType.Expense)`.
- **THEN** `Id` y `Type` se asignan; `Type` es `TransactionType.Expense`.

### AC-9 — `DeleteTransactionUseCase` sealed class + `ExecuteAsync` (dispatch + guard inline)

- [ ] `src/.../UseCases/Transactions/DeleteTransaction/DeleteTransactionUseCase.cs`
      es `public sealed class DeleteTransactionUseCase`.
- [ ] Campos privados readonly: `IExpenseRepository _expenseRepository`,
      `IIncomeRepository _incomeRepository`,
      `ILogger<DeleteTransactionUseCase> _logger` — **3 deps** (sin
      `IClock` — el borrado no muta auditoría; sin `IValidator<>` —
      guard inline, D-7).
- [ ] Constructor `public DeleteTransactionUseCase(IExpenseRepository
      expenseRepository, IIncomeRepository incomeRepository,
      ILogger<DeleteTransactionUseCase> logger)`.
- [ ] `public async Task<Result> ExecuteAsync(DeleteTransactionCommand
      command, CancellationToken ct = default)` — non-generic (D-1).
- [ ] Flujo (D-7, D-8): `ArgumentNullException.ThrowIfNull(command)` →
      `if (command.Id == Guid.Empty) return Result.Failure("Validation",
      "Id cannot be empty.");` (guard inline) → `switch (command.Type)`:
      `TransactionType.Expense` → `_expenseRepository.RemoveAsync(
      command.Id, ct)` → `return` ese `Result`; `TransactionType.Income`
      → `_incomeRepository.RemoveAsync(command.Id, ct)` → `return` ese
      `Result`; `default` → `Result.Failure("Validation",
      $"Unknown transaction type: {command.Type}.")`.

```csharp
public async Task<Result> ExecuteAsync(DeleteTransactionCommand command, CancellationToken ct = default)
{
    ArgumentNullException.ThrowIfNull(command);

    if (command.Id == Guid.Empty)                                   // D-7: guard inline
    {
        return Result.Failure("Validation", "Id cannot be empty.");
    }

    switch (command.Type)
    {
        case TransactionType.Expense:
            Result expenseResult = await _expenseRepository.RemoveAsync(command.Id, ct);
            return expenseResult;        // propaga "NotFound" del repo

        case TransactionType.Income:
            Result incomeResult = await _incomeRepository.RemoveAsync(command.Id, ct);
            return incomeResult;         // propaga "NotFound" del repo

        default:                         // D-8: defensivo (enum fuera de rango)
            return Result.Failure("Validation", $"Unknown transaction type: {command.Type}.");
    }
}
```

- **GIVEN** `command = (validId, TransactionType.Expense)`.
- **WHEN** `ExecuteAsync(command)`.
- **THEN** se invoca `_expenseRepository.RemoveAsync(validId, ct)` y su
  `Result` se retorna verbatim (no re-envuelto).
- **GIVEN** `command.Id == Guid.Empty`.
- **WHEN** `ExecuteAsync(command)`.
- **THEN** `IsSuccess == false`, `Error.Code == "Validation"`,
  `Error.Message == "Id cannot be empty."`; **ningún** repo fue invocado.
- **GIVEN** `command.Type == (TransactionType)999`.
- **WHEN** `ExecuteAsync(command)`.
- **THEN** `IsSuccess == false`, `Error.Code == "Validation"`,
  `Error.Message` empieza con `"Unknown transaction type:"`.

### AC-10 — DI en `Bootstrap.ConfigureServices`

- [ ] `src/.../UI/Composition/Bootstrap.cs` (modificado) añade, junto a
      los registros existentes (líneas 62-66):
      - `services.AddTransient<UpdateExpenseUseCase>();`
      - `services.AddTransient<UpdateIncomeUseCase>();`
      - `services.AddTransient<DeleteTransactionUseCase>();`
      - `services.AddTransient<IValidator<UpdateExpenseCommand>, UpdateExpenseValidator>();`
      - `services.AddTransient<IValidator<UpdateIncomeCommand>, UpdateIncomeValidator>();`
- [ ] Añade 3 `using` al tope:
      `using ControlDeFinanzasIA.Application.UseCases.Expenses.UpdateExpense;`
      `using ControlDeFinanzasIA.Application.UseCases.Incomes.UpdateIncome;`
      `using ControlDeFinanzasIA.Application.UseCases.Transactions.DeleteTransaction;`
- [ ] **No** se registra `IValidator<DeleteTransactionCommand>` (guard
      inline, D-7). **No** se modifica `AddPersistence`
      (`ServiceCollectionExtensions` — los repos ya están registrados
      desde F-002/F-003).
- [ ] El XML doc de `ConfigureServices` se actualiza para mencionar los
      **5 use cases** (Register Expense/Income, Update Expense/Income,
      Delete Transaction) + sus 4 validators FluentValidation.

- **GIVEN** `Bootstrap.ConfigureServices` registra `RegisterExpenseUseCase` + `RegisterIncomeUseCase` + `GetTransactionsQuery` + 2 validators hoy.
- **WHEN** se aplican los 5 `AddTransient` + los 3 `using` + el XML doc.
- **THEN** el contenedor resuelve los 3 use cases nuevos como
  `Transient`; `IValidator<UpdateExpenseCommand>` →
  `UpdateExpenseValidator` e `IValidator<UpdateIncomeCommand>` →
  `UpdateIncomeValidator` como `Transient`; `AddPersistence` intacto.

### AC-11 — Extensión de `FakeExpenseRepository` (test double)

- [ ] `tests/.../TestDoubles/FakeExpenseRepository.cs` (modificado)
      añade 3 campos privados + 3 setters + modifica
      `UpdateAsync`/`RemoveAsync`/`GetByIdAsync` para retornar los
      valores configurables (en vez de los defaults hardcodeados
      actuales — líneas 72-81).
- [ ] **Sólo test double** (no producción). Mantiene `AddedExpenses` +
      `SetAddResult` (de F-004 P3) intactos.

```csharp
// nuevos campos (junto a los existentes):
private Result _updateResult = Result.Success(null!);
private Result _removeResult = Result.Success(null!);
private Expense? _getByIdResult;

// nuevos setters:
/// <summary>Configures the <see cref="Result"/> returned by <see cref="UpdateAsync"/>.</summary>
public void SetUpdateResult(Result result) => _updateResult = result;

/// <summary>Configures the <see cref="Result"/> returned by <see cref="RemoveAsync"/>.</summary>
public void SetRemoveResult(Result result) => _removeResult = result;

/// <summary>Configures the <see cref="Expense"/> returned by <see cref="GetByIdAsync"/>.</summary>
public void SetGetByIdResult(Expense? expense) => _getByIdResult = expense;

// métodos modificados:
/// <inheritdoc />
public Task<Result> UpdateAsync(Expense expense, CancellationToken ct = default)
    => Task.FromResult(_updateResult);

/// <inheritdoc />
public Task<Result> RemoveAsync(Guid id, CancellationToken ct = default)
    => Task.FromResult(_removeResult);

/// <inheritdoc />
public Task<Expense?> GetByIdAsync(Guid id, CancellationToken ct = default)
    => Task.FromResult(_getByIdResult);
```

> **Nota:** no se añaden listas de captura (`UpdatedExpenses`/
> `RemovedIds`/`GetByIdCallCount`). Para los tests de Update, la
> verificación de "se llamó Update y mutó la entidad" se hace
> **asertando la mutación in-place** del `Expense` sembrado (el use case
> muta la misma referencia que el fake retorna). Para los tests de
> Delete, la verificación de "se llamó el repo correcto" se hace
> **configurando un `Result` distintivo** en cada repo (técnica de
> disambiguación — ver §6).

- **GIVEN** `_repository.SetGetByIdResult(seededExpense)`.
- **WHEN** el use case llama `GetByIdAsync(anyId, ct)`.
- **THEN** retorna `seededExpense` (ignora el `id` — el fake no es un
  store real; el test controla el `command.Id` para que coincida).
- **GIVEN** `_repository.SetUpdateResult(Result.Failure("NotFound", "x"))`.
- **WHEN** `UpdateAsync(expense, ct)`.
- **THEN** retorna ese `Result` (configurable, no hardcoded Success).

### AC-12 — Extensión de `FakeIncomeRepository` (test double)

- [ ] `tests/.../TestDoubles/FakeIncomeRepository.cs` (modificado) —
      **igual que AC-11** con `Income` en vez de `Expense`. Añade
      `SetUpdateResult(Result)` / `SetRemoveResult(Result)` /
      `SetGetByIdResult(Income?)` + modifica `UpdateAsync`/`RemoveAsync`/
      `GetByIdAsync` (líneas 37-46 actuales). Mantiene `AddedIncomes` +
      `SetAddResult` intactos.

### AC-13 — Tests unitarios (≥10; ~14-15 planeados)

- [ ] `tests/ControlDeFinanzasIA.Tests.Unit/Application/Expenses/UpdateExpense/UpdateExpenseUseCaseTests.cs`
      existe con ≥3 tests (§6), pure xUnit `Assert.*`.
- [ ] `tests/ControlDeFinanzasIA.Tests.Unit/Application/Incomes/UpdateIncome/UpdateIncomeUseCaseTests.cs`
      existe con ≥3 tests (§6).
- [ ] `tests/ControlDeFinanzasIA.Tests.Unit/Application/Transactions/DeleteTransaction/DeleteTransactionUseCaseTests.cs`
      existe con ≥4 tests (§6).
- [ ] **Ubicación correcta per C-3**: `Application/<Aggregate>/<UseCase>/`
      (sin segmento `UseCases`).
- [ ] Fakes: `FakeExpenseRepository`/`FakeIncomeRepository` (extendidos,
      AC-11/AC-12), `FakeClock` (existente),
      `NullLogger<>.Instance` (de `Microsoft.Extensions.Logging.Abstractions`),
      validadores **reales** (no fake). Sin FluentAssertions, sin
      mocking library.

- **GIVEN** los ~14-15 tests implementados según §6.
- **WHEN** `dotnet test -c Debug --no-build --filter "FullyQualifiedName~UpdateExpenseUseCase|FullyQualifiedName~UpdateIncomeUseCase|FullyQualifiedName~DeleteTransactionUseCase"`.
- **THEN** ≥10 (todos los planeados) pasan en verde.

### AC-14 — Gates de verificación

- [ ] `dotnet build -c Release --nologo` → 0 warnings, 0 errors.
- [ ] `dotnet build -c Debug --nologo` → 0 warnings, 0 errors.
- [ ] `dotnet test -c Debug --no-build --nologo --filter "FullyQualifiedName~UpdateExpenseUseCase|FullyQualifiedName~UpdateIncomeUseCase|FullyQualifiedName~DeleteTransactionUseCase"` → todos pasan.
- [ ] `dotnet test -c Debug --no-build --nologo --filter "FullyQualifiedName~ArchitectureTests"` → 11/11 (R-1..R-5).
- [ ] `dotnet test -c Debug --no-build --nologo` (suite completa) →
      suite previa verde + ~14-15 nuevos unit verdes (nota: puede haber
      un fallo pre-existente por regresión ambiental WDAC — el
      implementador reporta la cuenta exacta observada).
- [ ] `dotnet format style --verify-no-changes` → exit 0.

> Las cuentas de tests son **aproximadas**; el gate real es "0 failures
> + suite previa sigue verde + ~14-15 nuevos unit verdes". El
> implementador (sdd-apply) reporta las cuentas exactas observadas.

### AC-15 — Sin scope creep

- [ ] **NO** `Expense.cs`/`Income.cs` (los `Update(...)` ya existen —
      6-param y 7-param respectivamente, verificados).
- [ ] **NO** `IExpenseRepository.cs`/`IIncomeRepository.cs` (6 métodos
      completos desde F-002/F-003).
- [ ] **NO** `ExpenseRepository.cs`/`IncomeRepository.cs`/
      `ExpenseConfiguration.cs`/`IncomeConfiguration.cs`/
      `ExpenseFilter.cs`/`IncomeFilter.cs`.
- [ ] **NO** `Expense.AddTag`/`RemoveTag`/`Source` (Fase 4 F-004 los
      provee / N/A).
- [ ] **NO** nuevas migraciones EF Core.
- [ ] **NO** `Money`/`Currency`/`Result`/`Error`/`PagedResult` modificados.
- [ ] **NO** `Tag.cs`/`TagConfiguration`/`TagRepository`/`ITagRepository`.
- [ ] **NO** `RegisterExpenseUseCase*`/`RegisterIncomeUseCase*` modificados.
- [ ] **NO** UI (Views, XAML, ViewModels) — **salvo `Bootstrap.cs`**.
- [ ] **NO** `ServiceCollectionExtensions.cs` (DI de use cases vive en
      `Bootstrap.cs` — D-3 de F-003).
- [ ] **NO** FluentAssertions. **NO** mocking library. **NO** paquetes
      NuGet nuevos. **NO** `.csproj` modificados.

- **GIVEN** el diff de F-005.
- **WHEN** se inspecciona contra la lista de archivos in-scope (§5).
- **THEN** no hay archivos fuera de la lista modificados; no hay
  referencias a 4-008 (UI/DTOs) en el código de producción.

---

## 5. Áreas afectadas

| Archivo | Acción | Descripción |
| --- | --- | --- |
| `src/ControlDeFinanzasIA.Domain/Enums/TransactionType.cs` | **New** | `enum TransactionType { Expense, Income }`, file-scoped ns, XML doc. Sin ordinales explícitos. |
| `src/ControlDeFinanzasIA.Application/UseCases/Expenses/UpdateExpense/UpdateExpenseCommand.cs` | **New** | `sealed record`, 7 props (`Guid Id` + 6 de `RegisterExpenseCommand`); **`Guid CategoryId` required** (C-2). Sin `Source`. |
| `src/ControlDeFinanzasIA.Application/UseCases/Expenses/UpdateExpense/UpdateExpenseValidator.cs` | **New** | `sealed class : AbstractValidator<UpdateExpenseCommand>`, 6 reglas sync (`Id` + 5 de `RegisterExpenseValidator`). Mensajes español (D-ML). |
| `src/ControlDeFinanzasIA.Application/UseCases/Expenses/UpdateExpense/UpdateExpenseUseCase.cs` | **New** | `sealed class`, 4 deps, `ExecuteAsync → Task<Result>` (non-generic, D-1). Llama `Expense.Update` **6 args** (C-1). `Result.Success(null!)`. |
| `src/ControlDeFinanzasIA.Application/UseCases/Incomes/UpdateIncome/UpdateIncomeCommand.cs` | **New** | `sealed record`, 7 props; **`Guid? CategoryId` nullable**. Sin `Source` en comando (use case resetea). |
| `src/ControlDeFinanzasIA.Application/UseCases/Incomes/UpdateIncome/UpdateIncomeValidator.cs` | **New** | `sealed class`, 6 reglas sync (`Id` + 5 de `RegisterIncomeValidator`, `CategoryId` condicional). Mensajes español. |
| `src/ControlDeFinanzasIA.Application/UseCases/Incomes/UpdateIncome/UpdateIncomeUseCase.cs` | **New** | `sealed class`, 4 deps, `ExecuteAsync → Task<Result>`. Llama `Income.Update` **7 args** (`null` source — D-6). `Result.Success(null!)`. |
| `src/ControlDeFinanzasIA.Application/UseCases/Transactions/DeleteTransaction/DeleteTransactionCommand.cs` | **New** | `sealed record (Guid Id, TransactionType Type)` — 2 props. |
| `src/ControlDeFinanzasIA.Application/UseCases/Transactions/DeleteTransaction/DeleteTransactionUseCase.cs` | **New** | `sealed class`, **3 deps** (sin `IClock`, sin `IValidator`), guard inline `Id != Guid.Empty` (D-7), `switch` dispatch (D-8). |
| `src/ControlDeFinanzasIA.UI/Composition/Bootstrap.cs` | **Modified** | +3 `AddTransient<UseCase>` + 2 `AddTransient<IValidator<>,...>` + 3 `using` + XML doc (5 use cases). **NO** `ServiceCollectionExtensions`. |
| `tests/ControlDeFinanzasIA.Tests.Unit/TestDoubles/FakeExpenseRepository.cs` | **Modified** | +`SetUpdateResult`/`SetRemoveResult`/`SetGetByIdResult` + `UpdateAsync`/`RemoveAsync`/`GetByIdAsync` configurables (AC-11). |
| `tests/ControlDeFinanzasIA.Tests.Unit/TestDoubles/FakeIncomeRepository.cs` | **Modified** | Igual que AC-11 con `Income` (AC-12). |
| `tests/ControlDeFinanzasIA.Tests.Unit/Application/Expenses/UpdateExpense/UpdateExpenseUseCaseTests.cs` | **New** | ≥3 tests (§6), pure xUnit `Assert.*`. |
| `tests/ControlDeFinanzasIA.Tests.Unit/Application/Incomes/UpdateIncome/UpdateIncomeUseCaseTests.cs` | **New** | ≥3 tests (§6). |
| `tests/ControlDeFinanzasIA.Tests.Unit/Application/Transactions/DeleteTransaction/DeleteTransactionUseCaseTests.cs` | **New** | ≥4 tests (§6). |

**Archivos NO modificados (verificados presentes y correctos):**

`Expense.cs`, `Income.cs`, `ExpenseConfiguration.cs`,
`IncomeConfiguration.cs`, `ExpenseRepository.cs`, `IncomeRepository.cs`,
`IExpenseRepository.cs` (6 métodos), `IIncomeRepository.cs` (6 métodos),
`ExpenseFilter.cs`, `IncomeFilter.cs`, `Money.cs`, `Currency.cs`,
`Result.cs`, `Error`, `PagedResult.cs`, `Tag.cs`, `TagConfiguration.cs`,
`TagRepository.cs`, `ITagRepository.cs`, `RegisterExpenseCommand.cs`,
`RegisterExpenseValidator.cs`, `RegisterExpenseUseCase.cs`,
`RegisterExpenseResult.cs`, `ExpenseDto.cs`, `ExpenseExtensions.cs`,
`RegisterIncomeCommand.cs`, `RegisterIncomeValidator.cs`,
`RegisterIncomeUseCase.cs`, `RegisterIncomeResult.cs`, `IClock.cs`,
`ServiceCollectionExtensions.cs`, `AppDbContext.cs`, cualquier
migración, cualquier archivo de UI (salvo `Bootstrap.cs`),
`Directory.Build.props`, `Directory.Packages.props`, cualquier
`.csproj`.

**Modificaciones a `.csproj`:** ninguna (sin paquetes nuevos, sin
referencias de proyecto nuevas — FluentValidation ya referenciado en
`Application.csproj` vía CPM; `Microsoft.Extensions.Logging.Abstractions`
ya referenciado en `Tests.Unit.csproj`).

---

## 6. Plan de tests unitarios

Espejo estructural de `RegisterIncomeUseCaseTests.cs` (239 líneas) /
`RegisterExpenseUseCaseTests.cs`, con las adaptaciones de Update
(lookup por Id + `SetGetByIdResult`) y Delete (dispatch + guard).
Patrón de fixture verificado:

```csharp
private static readonly Guid _validCategoryId = new("11111111-1111-1111-1111-000000000001");

private readonly FakeClock _clock;
private readonly FakeExpenseRepository _repository;   // o FakeIncomeRepository
private readonly UpdateExpenseValidator _validator;   // real, no fake
private readonly UpdateExpenseUseCase _useCase;

public UpdateExpenseUseCaseTests()
{
    _clock = new FakeClock { UtcNow = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc) };
    _repository = new FakeExpenseRepository();
    _validator = new UpdateExpenseValidator();
    _useCase = new UpdateExpenseUseCase(_repository, _clock, _validator, NullLogger<UpdateExpenseUseCase>.Instance);
}

private UpdateExpenseCommand CreateValidCommand(Guid id)
    => new(id, _validCategoryId, DateOnly.FromDateTime(_clock.UtcNow), 100m, Currency.DOP, "Almuerzo", null);

private Expense SeedExpense()
    => Expense.Create(_validCategoryId, new DateOnly(2026, 1, 10), Money.Create(50m, Currency.DOP), "Original", null, _clock.UtcNow.AddDays(-5));
```

> **Por qué `SeedExpense()` y `command.Id = seeded.Id`:** `Expense.Create`
> genera el `Id` internamente vía `Guid.NewGuid()` (no es seteable
> públicamente). El test crea el agregado sembrado, lo registra en el
> fake vía `SetGetByIdResult(seeded)`, y construye el comando con
> `seeded.Id` para que el lookup coincida. El use case muta `seeded`
> **in-place** (`Expense.Update` muta la misma referencia), así el test
> aserta la mutación directamente sobre `seeded` sin lista de captura.

### 6.1 `UpdateExpenseUseCaseTests` (≥3 requeridos; ~6 planeados)

| # | Método | GIVEN / WHEN / THEN |
| --- | --- | --- |
| T-E1 | `ExecuteAsync_WithValidCommand_ReturnsSuccess` | **GIVEN** `var seeded = SeedExpense(); _repository.SetGetByIdResult(seeded);` comando con `Id = seeded.Id`. **WHEN** `ExecuteAsync(command)`. **THEN** `IsSuccess` true; `Value == null`; `seeded.CategoryId == _validCategoryId`; `seeded.Description == "Almuerzo"`; `seeded.UpdatedAt == _clock.UtcNow`. |
| T-E2 | `ExecuteAsync_WithMissingExpense_ReturnsNotFound` | **GIVEN** `_repository.SetGetByIdResult(null)` (default). **WHEN** `ExecuteAsync(command)` con cualquier `Id`. **THEN** `IsSuccess` false; `Error.Code == "NotFound"`; `Error.Message` contiene el `Id`. |
| T-E3 | `ExecuteAsync_WithInvalidCommand_ReturnsValidationFailure` | **GIVEN** `command = CreateValidCommand(anyId) with { Amount = 0 }`. **WHEN** `ExecuteAsync`. **THEN** `IsSuccess` false; `Error.Code == "Validation"`; `Error.Details` contiene `"Amount"`. (Repo `UpdateAsync` no invocado — no hay forma de asertarlo sin call count, pero el guard de validación short-circuita antes del repo.) |
| T-E4 | `ExecuteAsync_WithEmptyId_ReturnsValidationFailure` | **GIVEN** `command = CreateValidCommand(Guid.Empty)`. **WHEN** `ExecuteAsync`. **THEN** `IsSuccess` false; `Error.Code == "Validation"`; details contiene `"Id"`. |
| T-E5 | `ExecuteAsync_WithEmptyCategoryId_ReturnsValidationFailure` | **GIVEN** `command = CreateValidCommand(anyId) with { CategoryId = Guid.Empty }` (C-2 — required). **WHEN** `ExecuteAsync`. **THEN** `IsSuccess` false; `Error.Code == "Validation"`; details contiene `"CategoryId"`. |
| T-E6 | `ExecuteAsync_PropagatesRepositoryFailure` | **GIVEN** `var seeded = SeedExpense(); _repository.SetGetByIdResult(seeded); _repository.SetUpdateResult(Result.Failure("NotFound", "Expense gone."));` comando con `Id = seeded.Id`. **WHEN** `ExecuteAsync`. **THEN** `IsSuccess` false; `Error.Code == "NotFound"`; `Error.Message == "Expense gone."` (propagado verbatim, sin re-envoltorio). |
| T-E7 | `ExecuteAsync_WithDateBefore1900_ReturnsDomainInvariantFailure` | **GIVEN** `var seeded = SeedExpense(); _repository.SetGetByIdResult(seeded);` comando con `Date = new DateOnly(1899, 12, 31)` (pasa el validador — no hay regla de floor; `Expense.Update` lanza). **WHEN** `ExecuteAsync`. **THEN** `IsSuccess` false; `Error.Code == "DomainInvariant"`; `Error.Message` es el del dominio (inglés). |

### 6.2 `UpdateIncomeUseCaseTests` (≥3 requeridos; ~6 planeados)

| # | Método | GIVEN / WHEN / THEN |
| --- | --- | --- |
| T-I1 | `ExecuteAsync_WithValidCommand_ReturnsSuccess` | **GIVEN** `var seeded = SeedIncome(); _repository.SetGetByIdResult(seeded);` comando con `Id = seeded.Id`. **WHEN** `ExecuteAsync`. **THEN** `IsSuccess` true; `Value == null`; `seeded.Description == "Sueldo"`; `seeded.UpdatedAt == _clock.UtcNow`. |
| T-I2 | `ExecuteAsync_WithMissingIncome_ReturnsNotFound` | **GIVEN** `_repository.SetGetByIdResult(null)`. **WHEN** `ExecuteAsync`. **THEN** `IsSuccess` false; `Error.Code == "NotFound"`. |
| T-I3 | `ExecuteAsync_WithInvalidCommand_ReturnsValidationFailure` | **GIVEN** `command with { Amount = 0 }`. **WHEN** `ExecuteAsync`. **THEN** `IsSuccess` false; `Error.Code == "Validation"`; details contiene `"Amount"`. |
| T-I4 | `ExecuteAsync_WithNullCategory_ReturnsSuccess` | **GIVEN** `var seeded = SeedIncome(); _repository.SetGetByIdResult(seeded);` `command with { CategoryId = null }`. **WHEN** `ExecuteAsync`. **THEN** `IsSuccess` true; `seeded.CategoryId == null` (nullable válido). |
| T-I5 | `ExecuteAsync_ResetsSourceToNull` | **GIVEN** `var seeded = SeedIncome(); seeded` con `Source == "Freelance"` (sembrado vía `Income.Create(..., "Freelance", ...)`); `_repository.SetGetByIdResult(seeded);` comando válido. **WHEN** `ExecuteAsync`. **THEN** `IsSuccess` true; `seeded.Source == null` (reset — D-6/Q4). |
| T-I6 | `ExecuteAsync_PropagatesRepositoryFailure` | **GIVEN** `var seeded = SeedIncome(); _repository.SetGetByIdResult(seeded); _repository.SetUpdateResult(Result.Failure("NotFound", "Income gone."));`. **WHEN** `ExecuteAsync`. **THEN** `IsSuccess` false; `Error.Code == "NotFound"` (propagado verbatim). |

### 6.3 `DeleteTransactionUseCaseTests` (≥4 requeridos; ~5 planeados)

Fixture: **2 fakes** (`FakeExpenseRepository` + `FakeIncomeRepository`),
`NullLogger<DeleteTransactionUseCase>.Instance`, sin validador, sin
`FakeClock` (el borrado no usa `IClock`).

**Técnica de disambiguación (sin listas de captura):** para probar que
el dispatch invoca el repo **correcto**, se configura un `Result`
distintivo (p. ej. un `Failure`) en el repo que **no** debe invocarse, y
se aserta el `Result` del repo que **sí** debe invocarse. Si el dispatch
estuviera roto (rama equivocada), el resultado sería el del repo
configurado con failure, no el esperado.

| # | Método | GIVEN / WHEN / THEN |
| --- | --- | --- |
| T-D1 | `ExecuteAsync_WithExpenseType_CallsExpenseRepository` | **GIVEN** `_incomeRepository.SetRemoveResult(Result.Failure("NotFound", "income"));` (trampa — si se llamara al repo de ingresos, fallaría); `command = (validId, TransactionType.Expense)`; el repo de gastos usa el default `Success`. **WHEN** `ExecuteAsync`. **THEN** `IsSuccess` true (probó que la rama `Expense` corrió, no `Income`). |
| T-D2 | `ExecuteAsync_WithIncomeType_CallsIncomeRepository` | **GIVEN** `_expenseRepository.SetRemoveResult(Result.Failure("NotFound", "expense"));` (trampa); `command = (validId, TransactionType.Income)`. **WHEN** `ExecuteAsync`. **THEN** `IsSuccess` true (probó que la rama `Income` corrió, no `Expense`). |
| T-D3 | `ExecuteAsync_WithEmptyId_ReturnsValidationFailure` | **GIVEN** ambos repos con `SetRemoveResult(Failure(...))` (trampas); `command = (Guid.Empty, TransactionType.Expense)`. **WHEN** `ExecuteAsync`. **THEN** `IsSuccess` false; `Error.Code == "Validation"`; `Error.Message == "Id cannot be empty."` (el guard short-circuita antes del switch — ningún repo invocado; si se invocara alguno, el `Error.Code` sería `"NotFound"`, no `"Validation"`). |
| T-D4 | `ExecuteAsync_WithNotFound_PropagatesResult` | **GIVEN** `_expenseRepository.SetRemoveResult(Result.Failure("NotFound", "Expense 000... not found."));` `command = (validId, TransactionType.Expense)`. **WHEN** `ExecuteAsync`. **THEN** `IsSuccess` false; `Error.Code == "NotFound"`; `Error.Message == "Expense 000... not found."` (propagado verbatim, sin re-envoltorio). |
| T-D5 | `ExecuteAsync_WithUnknownTransactionType_ReturnsValidationFailure` | **GIVEN** `command = (validId, (TransactionType)999)`. **WHEN** `ExecuteAsync`. **THEN** `IsSuccess` false; `Error.Code == "Validation"`; `Error.Message` empieza con `"Unknown transaction type:"` (default del switch — D-8). |

**Marco y paquetes:** xUnit (ya en `Tests.Unit.csproj`).
`Microsoft.Extensions.Logging.Abstractions` ya referenciado (lo usan los
tests de Register). **Sin paquetes nuevos**; **sin FluentAssertions**;
**sin mocking**. Aserciones con `Assert.*` (`Assert.True`, `Assert.False`,
`Assert.Equal`, `Assert.NotEqual`, `Assert.Null`, `Assert.Contains`,
`Assert.StartsWith`, `Assert.NotNull`).

**Cobertura:** happy paths (T-E1, T-I1, T-D1, T-D2), edge cases
(T-I4 categoría null, T-I5 reset source, T-D5 enum inválido), error
states (T-E2/T-I2 NotFound, T-E3/T-E4/T-E5/T-I3 validación, T-E6/T-I6/
T-D4 propagación de repo, T-E7 DomainInvariant, T-D3 guard inline).

---

## 7. Fuera de alcance

F-005 **no** hace lo siguiente (cada ítem vive en otra feature):

- **UI de edición/borrado** (4-008): `EditExpenseViewModel`,
  `EditIncomeViewModel`, botones "Editar"/"Eliminar" en `ExpensesView`/
  `IncomeView`, confirmación de borrado, refresco de lista.
- **`IncomeDto`/`ExpenseDto` desde Update** (4-008): Update retorna
  `Result.Success(null!)` sin payload; el DTO de la entidad editada es
  scope de 4-008 (la UI hará re-fetch o construirá el result con DTO).
- **`FutureDateWarning` en Update** (4-008): Update no surfacea warnings
  (D-9). La detección de fecha futura al editar se difiere a 4-008.
- **Seteo deliberado de `Source` en `Income` vía Update** (4-008+):
  `UpdateIncomeUseCase` pasa `null` (reset — D-6). Preservar el
  `Source` existente o editarlo es un campo del form de edición (4-008)
  — requeriría añadir `Source` a `UpdateIncomeCommand` + regla de
  validación (max 50) + pasarlo al `Income.Update`. Fuera del contrato
  cached.
- **Manejo de `Tags` vía Update** (4-008+): `UpdateExpenseCommand` no
  lleva `TagIds`. Editar tags es `Expense.AddTag`/`RemoveTag` (Fase 4
  F-004) orquestado desde la UI de edición, no desde este use case.
- **`UpdateResult` type** (4-008): no se crea un `UpdateResult(Warnings)`
  ahora (evita scope creep de DTOs — D-9).
- **Soft-delete / `IsDeleted`**: borrado es hard delete (alineado con
  `ExpenseRepository`/`IncomeRepository`).
- **Modificaciones a entidades/repos/configs** (ver AC-15).
- **Nuevas migraciones** EF Core.
- **`ServiceCollectionExtensions.cs`** (DI de use cases vive en
  `Bootstrap.cs`).
- **Tests aislados de los validadores** (`UpdateExpenseValidatorTests.cs`/
  `UpdateIncomeValidatorTests.cs`): excluidos del contrato (espejo de
  F-003 P4 que tampoco los tuvo); los validadores se ejercitan
  indirectamente vía los tests del use case.
- **CI workflow**, **`Directory.Packages.props`**,
  **`Directory.Build.props`**: sin cambios.
- **Paquetes NuGet nuevos** (en particular, no FluentAssertions, no
  mocking).

---

## 8. Plan de rollback

Cambio **aditivo** en Application + UI composition root + Tests.Unit
(sin tocar Domain entidades/repos, Infrastructure, migraciones, `Result`,
`Money`, `Currency`, `IClock`, interfaces de repo). Revertir en orden:

1. **Eliminar** las 3 carpetas nuevas de use cases
   (`UseCases/Expenses/UpdateExpense/`, `UseCases/Incomes/UpdateIncome/`,
   `UseCases/Transactions/DeleteTransaction/` — 8 archivos), el enum
   `Domain/Enums/TransactionType.cs`, y los 3 archivos de tests
   (`Application/Expenses/UpdateExpense/`,
   `Application/Incomes/UpdateIncome/`,
   `Application/Transactions/DeleteTransaction/`).
2. **Revertir** la extensión de `FakeExpenseRepository.cs`/
   `FakeIncomeRepository.cs`: quitar `SetUpdateResult`/
   `SetRemoveResult`/`SetGetByIdResult` + los 3 campos, y restaurar
   `UpdateAsync`/`RemoveAsync`/`GetByIdAsync` a los defaults
   hardcodeados (`Result.Success(null!)` / `null`).
3. **Revertir** `Bootstrap.cs`: quitar los 5 `AddTransient` (3 use cases
   + 2 validators) + los 3 `using` de los nuevos namespaces; restaurar
   el XML doc previo de `ConfigureServices` (mención de sólo Register +
   GetTransactions).
4. **Verificar** `dotnet build` limpio y `dotnet test` suite previa en
   verde.
5. No hay migración EF ni datos de usuario en juego. NetArchTest
   R-1..R-5 no se afectan: los tipos nuevos viven en `Application`
   (sólo `Domain` + `FluentValidation`, ya referenciados — R-2 ok), el
   enum en `Domain.Enums` (sin deps — R-1 ok), y los registros DI en el
   composition root de `UI` (R-4 ok); ningún tipo nuevo en
   `Infrastructure`, ningún cambio en `Domain` entidades.

---

## 9. Riesgos y mitigaciones

| # | Riesgo | Prob. | Impacto | Mitigación |
| --- | --- | --- | --- | --- |
| R-1 | **C-1/C-2: el contrato cached no compila contra el código real** (`Expense.Update` 6-param, `CategoryId` required `Guid`). | Alta (verificado) | Alto | Correcciones C-1/C-2 aplicadas en el proposal y ratificadas en esta spec (§3). El implementador (sdd-apply) debe usar las formas corregidas de AC-4/AC-7 (6 args Expense, 7 args Income con `null` source; `Guid CategoryId` en `UpdateExpenseCommand`), **no** el bosquejo original del contrato. |
| R-2 | **Update no surfacea `FutureDateWarning`** (Register sí). Editar con fecha futura se acepta silenciosamente. | Media (decisión) | Bajo | D-9: inconsistencia menor **aceptada** hasta 4-008 (cuando la UI de edición construya el result con DTO + warnings). No se crea `UpdateResult` ahora (evita scope creep). Documentado en XML doc del use case. |
| R-3 | **`UpdateIncomeUseCase` resetea `Source` a `null`**: editar cualquier campo borra el `Source` previo. | Media (decisión) | Bajo | D-6/Q4: `null` es un estado válido y soportado por `Income` (INV-6). Limitación conocida y aceptada; el seteo deliberado de `Source` es scope 4-008+ (requeriría `Source` en `UpdateIncomeCommand`). Test T-I5 (`ExecuteAsync_ResetsSourceToNull`) blinda el comportamiento. |
| R-4 | **Conflicto de idioma en mensajes por-campo del validador**: el bosquejo AC-3 del contrato los escribió en inglés, pero la instrucción locked dice "mirror `RegisterExpenseValidator`" (español). | Media (decisión) | Bajo | D-ML: se espejan los mensajes en **español** de los validadores Register (consistencia dentro de la familia) + mensaje español nuevo para la regla `Id` (`"El identificador es obligatorio."`). Los mensajes en inglés del bosquejo AC-3 se consideran ilustrativos. Si el usuario prefiere inglés, se corrige en revisión. Mensajes `NotFound`/guards de Delete sí en inglés (contrato explícito, sin precedente). |
| R-5 | **Fakes actuales tienen `UpdateAsync`/`RemoveAsync`/`GetByIdAsync` hardcoded** (no configurables) → tests NotFound/propagación no se pueden escribir. | Alta (verificado) | Bajo | AC-11/AC-12: extender `FakeExpenseRepository`/`FakeIncomeRepository` con `SetUpdateResult`/`SetRemoveResult`/`SetGetByIdResult` (test doubles — no en lista "no modificar"). |
| R-6 | **C-3: las rutas de test del contrato (`Application/UseCases/...`) no coinciden con la convención del repo** (`Application/<Aggregate>/<UseCase>/` sin `UseCases`). | Alta (verificado) | Bajo | C-3: se usan las rutas correctas del repo (`Application/Expenses/UpdateExpense/` etc.) con namespaces `ControlDeFinanzasIA.Tests.Unit.Application.Expenses.UpdateExpense`. La estructura de producción conserva `UseCases/`. |
| R-7 | **`Money.Create`/`entity.Update` lanzan `ArgumentException`** que escapa del use case (sin el catch). | Media | Medio | D-4 + AC-4/AC-7 envuelven ambas factories en `try/catch (ArgumentException ex)` → `"DomainInvariant"`. `ArgumentNullException` deriva de `ArgumentException` → cubierta. Test T-E7 blinda la rama (fecha < 1900). |
| R-8 | **Engram `mem_save` no disponible en el toolset del sub-agente** (verificado: sin funciones `mem_*`). | Alta (verificado) | Bajo | Respaldo en filesystem (proposal `.atl/sdd/f-005p4-…/proposal.md` + esta spec). El orquestador backfillará a Engram con los topic keys `sdd/f-005p4-update-delete-use-cases/{proposal,spec}` (precedente `f-002p4`/`f-003p4`). El sub-agente **no fabrica observation IDs**. |
| R-9 | **FluentValidation 11 lanza `AsyncValidatorInvokedSynchronouslyException`**. | Baja | Medio | D-2: todas las reglas sync; el use case invoca `Validate` no `ValidateAsync`. Sin `MustAsync`/`CustomAsync`. |
| R-10 | **`TransactionType` enum inválido** (p. ej. `(TransactionType)999`) cae en `default` del switch sin handling. | Baja | Bajo | D-8: `default` branch retorna `Result.Failure("Validation", $"Unknown transaction type: {command.Type}.")`. Test T-D5 cubre la rama. |
| R-11 | **Scope creep hacia 4-008** (UI/DTOs/`Source` en command/`FutureDateWarning` en Update) o tests aislados de validadores. | Media | Alto | AC-15 lo prohíbe explícitamente; §7 lista el out-of-scope. D-6/D-9 fijan `null` source y sin warnings. Verificar en sdd-verify. |

---

## 10. Referencias

- **Propuesta (filesystem):**
  `.atl/sdd/f-005p4-update-delete-use-cases/proposal.md` (219 líneas)
  — PRD de origen. Sus correcciones C-1/C-2 y las decisiones Q1..Q5
  (resueltas: 6 args Expense, `Guid CategoryId`, `Result.Success(null!)`
  sin payload, `null` source, 2 validators + guard inline) son la base
  de esta spec. **Las formas corregidas rigen sobre el bosquejo original
  del contrato de lanzamiento.**
- **Propuesta (Engram objetivo):** topic key
  `sdd/f-005p4-update-delete-use-cases/proposal` (pendiente de
  persistencia por el orquestador — ver R-8).
- **Spec (Engram objetivo):** topic key
  `sdd/f-005p4-update-delete-use-cases/spec` (pendiente de
  persistencia por el orquestador — ver R-8).
- **F-003 Fase 4 (archivada — patrón espejo de use case de escritura):**
  `spec/features/phase-04/F-003-register-income-use-case.done.md`
  (860 líneas) — `RegisterIncomeUseCase`/`Command`/`Validator`/`Result`
  que F-005 replica para Update, con las adaptaciones (lookup por Id +
  `entity.Update` + `Result.Success(null!)`).
- **F-002 Fase 4 (archivada — dependencia de repo):**
  `spec/features/phase-04/F-002-income-repository.done.md` —
  `IIncomeRepository` 6 métodos (`UpdateAsync`/`RemoveAsync`/
  `GetByIdAsync` consumidos sin modificar la interface).
- **F-004 Fase 3 (archivada — patrón espejo de `RegisterExpenseUseCase`):**
  `spec/features/phase-03/F-004-register-expense-use-case.done.md` —
  flujo `ExecuteAsync` que `UpdateExpenseUseCase` replica.
- **Phase plan:** `spec/phases/phase-04/features.md` §RF-7 (líneas
  170-175 — `UpdateExpenseUseCase`/`UpdateIncomeUseCase`) + §RF-8
  (líneas 177-182 — `TransactionType` + `DeleteTransactionUseCase`).
- **Convención de contrato:** `spec/features/phase-01/F-006-contrato-interfaces-repo.done.md`
  (commands `Task<Result>` non-generic).
- **Tipos comunes:** `spec/features/phase-01/F-005-comuntypes-result-pagedresult.done.md`
  (`Result` non-generic + `Value: object?` + `Error(code, message, details?)`).
- **Abstracciones:** `spec/features/phase-01/F-004-Abstracciones-clock-pathresolver.done.md`
  (`IClock.UtcNow`; `FakeClock` en `Tests.Unit/TestDoubles/`).
- **Arquitectura:** `docs/architecture.md` §4 (R-1..R-5), §7 (use cases
  devuelven `Result`), §19 (ADR-007 `IClock` inyectado para auditoría).
- **Constitución:** `spec/constitution/mission.md`,
  `spec/constitution/tech-stack.md` (.NET 8 LTS, FluentValidation
  11.10.0, async/await extremo a extremo, errors-as-values, MVVM
  estricto, localización es-DO/en).
- **Código verificado (fuentes de la spec):**
  - `src/.../Domain/Transactions/Expense.cs` (290 líneas — `Update`
    6-param líneas 161-167, `CategoryId` `Guid` línea 23, lanza en
    `Guid.Empty`/`date<1900`/`updatedAt<CreatedAt`).
  - `src/.../Domain/Transactions/Income.cs` (268 líneas — `Update`
    7-param líneas 168-175 con `source` 6º, `CategoryId` `Guid?` línea
    24, `Source` propiedad línea 54).
  - `src/.../Application/UseCases/Expenses/RegisterExpense/RegisterExpenseCommand.cs`
    (15 líneas — `Guid CategoryId` required; espejo del comando Update).
  - `src/.../Application/UseCases/Expenses/RegisterExpense/RegisterExpenseValidator.cs`
    (35 líneas — 5 reglas sync, mensajes español per-rule `.WithMessage()`).
  - `src/.../Application/UseCases/Expenses/RegisterExpense/RegisterExpenseUseCase.cs`
    (110 líneas — flujo `ExecuteAsync`: `audit` local,
    `DateOnly.FromDateTime(audit)`, try/catch `ArgumentException` →
    `"DomainInvariant"`, propaga `addResult`, `Result.Success(result)`).
  - `src/.../Application/UseCases/Incomes/RegisterIncome/RegisterIncomeCommand.cs`
    (17 líneas — `Guid? CategoryId` nullable).
  - `src/.../Application/UseCases/Incomes/RegisterIncome/RegisterIncomeValidator.cs`
    (45 líneas — 5 reglas, `CategoryId` condicional `.When(not null)`).
  - `src/.../Application/UseCases/Incomes/RegisterIncome/RegisterIncomeUseCase.cs`
    (109 líneas — `Income.Create(..., null, audit)` pasa `null` source;
    `Result.Success(new RegisterIncomeResult(...))`).
  - `src/.../Application/Common/Result.cs` (47 líneas — `Result`
    non-generic; `Success(object value)` línea 38 acepta `null!`;
    `Failure(code, message, details)`).
  - `src/.../Application/Abstractions/IExpenseRepository.cs` (71 líneas
    — 6 métodos: `AddAsync`/`UpdateAsync`/`RemoveAsync` → `Task<Result>`,
    `GetByIdAsync` → `Task<Expense?>`).
  - `src/.../Domain/Enums/Currency.cs` (17 líneas — enum en
    `Domain.Enums`, XML doc por valor; `TransactionType` va aquí).
  - `src/.../UI/Composition/Bootstrap.cs` (123 líneas — DI de use cases
    líneas 62-66; **no** `ServiceCollectionExtensions`).
  - `tests/.../Application/Incomes/RegisterIncome/RegisterIncomeUseCaseTests.cs`
    (239 líneas — espejo de tests: fixture + `NullLogger<>.Instance` +
    `Assert.*`; ubicación `Application/Incomes/RegisterIncome/` sin
    segmento `UseCases` → confirma C-3).
  - `tests/.../TestDoubles/FakeExpenseRepository.cs` (99 líneas —
    `SetAddResult` existe; `UpdateAsync`/`RemoveAsync`/`GetByIdAsync`
    hardcoded líneas 72-81 → a extender AC-11).
  - `tests/.../TestDoubles/FakeIncomeRepository.cs` (55 líneas —
    `SetAddResult` existe; `UpdateAsync`/`RemoveAsync`/`GetByIdAsync`
    hardcoded líneas 37-46 → a extender AC-12).
  - `tests/.../TestDoubles/FakeClock.cs` (21 líneas — `UtcNow` seteable).
- **FluentValidation 11.x — Sync vs async validators:**
  [docs.fluentvalidation.net/async](https://docs.fluentvalidation.net/en/latest/async.html)
  (reglas sync; `AsyncValidatorInvokedSynchronouslyException`).
