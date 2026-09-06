# Specification — F-003: `RegisterIncomeUseCase` + Command + Validator + Result + DI

> **Fase:** 4 — Slice vertical: registrar, actualizar y eliminar ingresos
> **Feature:** F-003 (local a esta fase)
> **Spec ID:** SPEC-F003P4
> **change_id:** `f-003p4-register-income-use-case`
> **Modo:** Standard (SDD: propose → spec → tasks → apply → verify → archive)
> **Estado:** Pendiente de implementación (renombrar a `.done.md` tras apply + verify)
>
> **Nota de numeración:** este `F-003` es el de la **Fase 4**
> (`spec/phases/phase-04/features.md`). No debe confundirse con el
> `F-003` de Fase 3 (`ExpenseRepository`). Los IDs son locales a cada
> fase; el folder los desambigua. Ver `spec/README.md`.
>
> Fuentes de verdad: `docs/vision.md`, `docs/architecture.md`,
> `docs/roadmap.md` (§Fase 4), `spec/constitution/tech-stack.md`,
> `spec/constitution/mission.md`,
> `spec/phases/phase-04/features.md` (§RF-6 — líneas 162-168),
> `spec/features/phase-04/F-001-income-entity-config-migration.done.md`
> (entidad `Income` + `IncomeConfiguration` + migración `AddIncomesTable`),
> `spec/features/phase-04/F-002-income-repository.done.md`
> (`IncomeRepository` 6 métodos + `IncomeFilter` + `IIncomeRepository.GetByFilterAsync`),
> `spec/features/phase-03/F-004-register-expense-use-case.done.md`
> (patrón espejo: `RegisterExpenseUseCase`),
> `spec/features/phase-01/F-005-comuntypes-result-pagedresult.done.md`
> (`Result` non-generic — **no se modifica**),
> `spec/features/phase-01/F-004-Abstracciones-clock-pathresolver.done.md`
> (`IClock`).

---

## 1. Metadata

| Campo | Valor |
| --- | --- |
| **change_id** | `f-003p4-register-income-use-case` |
| **spec_id** | `SPEC-F003P4` |
| **modo** | Standard (SDD: propose → spec → tasks → apply → verify → archive) |
| **fase** | 4 |
| **feature** | F-003 (local a Fase 4) |
| **dependencies (build)** | F-001 Fase 4 (`Income`, `IncomeConfiguration`, migración `AddIncomesTable`, `DbSet<Income>`) ✅ archivada; F-002 Fase 4 (`IncomeRepository` 6 métodos, `IncomeFilter`, `IIncomeRepository.GetByFilterAsync`, registro en `AddPersistence`) ✅ archivada |
| **dependencies (contrato)** | F-005 Fase 1 (`Result` non-generic, `Value: object?`) ✅; F-004 Fase 1 (`IClock`, `FakeClock`) ✅; F-004 Fase 3 (`RegisterExpenseUseCase` — patrón espejo) ✅ |
| **dependencies (paquetes)** | FluentValidation 11.10.0 (ya referenciado en `Application.csproj` vía CPM) ✅ — F-003 **no añade paquetes** |
| **unblocks** | F-005 Fase 4 (`UpdateIncomeUseCase` / `DeleteTransactionUseCase`), F-008 Fase 4 (`GetIncomesQuery` / UI `IncomeView`) |
| **artifact_store** | Engram (objetivo: `sdd/f-003p4-register-income-use-case/spec`) + filesystem (`spec/features/phase-04/F-003-register-income-use-case.spec.md`) |
| **status** | Pendiente de implementación |
| **línea base de tests (pre-F-003)** | ~263 (aproximada, post F-002 Fase 4) |

> **Nota Engram:** `mem_save` no está disponible en el toolset del
> sub-agente de spec en esta sesión (igual que F-002 P4 — verificado:
> sin funciones `mem_*`). Respaldo en filesystem: proposal en
> `.atl/sdd/f-003p4-register-income-use-case/proposal.md` y esta spec
> en `spec/features/phase-04/F-003-register-income-use-case.spec.md`.
> El orquestador backfillará a Engram con los topic keys
> `sdd/f-003p4-register-income-use-case/{proposal,spec}` (precedente
> `f-002p4`). El sub-agente **no fabrica observation IDs**.

---

## 2. Contexto y objetivo

F-001 de Fase 4 entregó la base de `Income`: la entidad de dominio
blindada con invariantes (incluyendo `CategoryId` **nullable**, a
diferencia de `Expense`), `IncomeConfiguration` con `OwnsOne` de
`Money` y FK **`SetNull`** a `Categories`, la migración
`AddIncomesTable`, el `DbSet<Income> Incomes` en `AppDbContext`, y los
`IncomeSchemaTests`. F-002 cerró el lado de **persistencia**:
`IncomeRepository` (6 métodos), `IncomeFilter`, la extensión de
`IIncomeRepository` con `GetByFilterAsync`, y su registro scoped en
`AddPersistence`. Pero una entidad con repo y sin use case es
almacenamiento accesible sólo desde Infrastructure: ningún flujo de
aplicación puede orquestar validación → creación de dominio →
persistencia hasta que exista el use case de escritura.

F-003 cierra el **lado de escritura** del slice vertical de ingresos.
Es un **espejo estricto** de F-004 de Fase 3 (`RegisterExpenseUseCase`),
adaptando las dos diferencias semánticas de `Income`: (1) `CategoryId`
es **nullable** — el validador sólo rechaza `Guid.Empty` **cuando se
provee** una categoría, y `null` pasa (un ingreso sin categoría es
válido); (2) `Income.Create` recibe un **7º parámetro `source`** que
`Expense` no tiene — el use case lo pasa `null` (el seteo del origen se
difiere a F-005 `UpdateIncomeUseCase`). El use case orquesta: validación
FluentValidation **sync**, detección de warnings (fecha futura),
creación de la entidad vía sus factories (`Money.Create` +
`Income.Create`), persistencia delegada en `IIncomeRepository.AddAsync`,
auditoría sourced desde `IClock.UtcNow` (ADR-007), y retorno
`Task<Result>` **non-generic** envolviendo `RegisterIncomeResult(IncomeId, Warnings)`.

**Por qué `RegisterIncomeResult` lleva `IncomeId` y no un DTO:** `IncomeDto`
es scope de F-008 (UI / `GetIncomesQuery`). F-003 devuelve sólo el
`IncomeId` generado; la UI (F-008) hará re-fetch o extenderá el result
entonces. Esto se documenta en el XML doc del result.

**Objetivos concretos:**

1. **Crear 4 tipos nuevos** en `Application/UseCases/Incomes/RegisterIncome/`:
   `RegisterIncomeCommand`, `RegisterIncomeValidator`,
   `RegisterIncomeUseCase`, `RegisterIncomeResult`.
2. **Establecer el patrón de use case de escritura para `Income`**
   (espejo de F-004 P3), reutilizando `IClock`, `IIncomeRepository`,
   `Result` non-generic y FluentValidation ya referenciados.
3. **Registrar DI** en `Bootstrap.ConfigureServices` (UI composition
   root, **no** en `ServiceCollectionExtensions` — D-3).
4. **Cubrir con ≥10 tests unitarios** (12 planeados) usando
   `FakeIncomeRepository` (nuevo), `FakeClock` (existente),
   `NullLogger<>` y el validador real; pure xUnit `Assert.*`.

Al final de la feature: `dotnet build` limpio; `dotnet test` suite
previa (~263) + ~12 nuevos ≈ **~275 verdes**; NetArchTest R-1..R-5
verdes; `dotnet format style --verify-no-changes` exit 0.

---

## 3. Decisiones (locked-in — no re-abrir)

### D-1 — `Income.Create` tiene firma de 7 parámetros (corrección crítica)

`src/.../Domain/Transactions/Income.cs` (líneas 125-132) declara:

```csharp
public static Income Create(
    Guid? categoryId,
    DateOnly date,
    Money amount,          // value object único, NO (decimal, Currency) separados
    string description,
    string? note,
    string? source,        // 7º param — se pasa null en F-003
    DateTime createdAt);   // OBLIGATORIO, sourced desde IClock
```

- **NO** recibe `Guid id` (lo genera internamente vía `Guid.NewGuid()`).
  Pasar `id:` es un **error de compilación** (arg extra).
- `amount` es un **`Money`** único; el use case llama
  `Money.Create(command.Amount, command.Currency)` primero (espejo de
  `RegisterExpenseUseCase` líneas 86-93).
- Requiere `DateTime createdAt` (7º param). Omitirlo es error de
  compilación.
- Orden posicional: `categoryId, date, money, description, note, source, createdAt`.

**Llamada correcta (locked):** `Money.Create` → `Income.Create(categoryId, date, money, description, note, null, audit)`.

### D-2 — `Result` es non-generic; NO existe `Result<T>` (corrección crítica)

`src/.../Application/Common/Result.cs` declara `public sealed record Result`
con `Value: object?` y fábricas `Success(object)` / `Failure(Error)` /
`Failure(code, message)` / `Failure(code, message, details)`. **No hay
`Result<T>`**, ni `Result.Success<T>`, ni `Result.Failure<T>`.

- `ExecuteAsync` retorna **`Task<Result>`** (non-generic).
- Éxito → `Result.Success(new RegisterIncomeResult(income.Id, warnings))`.
- Fallo de repo → `return addResult;` (propaga el `Result` non-generic
  tal cual, con su `Error`).
- El caller hace `(RegisterIncomeResult)result.Value!` tras verificar `IsSuccess`.

### D-3 — El registro DI vive en `Bootstrap.cs` (UI), NO en `ServiceCollectionExtensions.cs` (Infrastructure)

`src/.../UI/Composition/Bootstrap.cs` (líneas 60-62) registra use cases +
validators. `ServiceCollectionExtensions.AddPersistence` (Infrastructure)
**sólo** registra repositorios. Registrar use cases en Infrastructure
rompería R-3/R-4 (Infrastructure no orquesta use cases; UI es el
composition root). **Decisión:** modificar `Bootstrap.ConfigureServices`
añadiendo 2 `AddTransient` + `using ControlDeFinanzasIA.Application.UseCases.Incomes.RegisterIncome;`.
`ServiceCollectionExtensions.cs` **no se toca** (F-002 ya registró `IIncomeRepository`).

### D-4 — Validación sync `_validator.Validate(command)`, NO `ValidateAsync`

Espejo de F-004 P3 DP-6: todas las reglas son sync (`RuleFor`, `NotEmpty`,
`GreaterThan`, `MaximumLength`, `Must`, `IsInEnum`, `When`). El use case
invoca `_validator.Validate(command)`. Sin `MustAsync`/`CustomAsync` →
evita `AsyncValidatorInvokedSynchronouslyException` (breaking change de
FluentValidation 11.0). RF-6 dice "ValidateAsync" como wording suelto;
se satisface en espíritu (cero reglas async).

### D-5 — Fallo de validación con `details` por campo (no un `string.Join` plano)

```csharp
IReadOnlyDictionary<string, string[]> details = validation.Errors
    .GroupBy(e => e.PropertyName)
    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray(), StringComparer.Ordinal);
return Result.Failure("Validation", "El comando para registrar el ingreso no es válido.", details);
```

Eso preserva la estructura por campo que la UI (F-008) necesita para
mapear errores a inputs. Espejo de `RegisterExpenseUseCase` líneas 63-73.

### D-6 — `try/catch(ArgumentException)` alrededor de `Money.Create`/`Income.Create` → `"DomainInvariant"`

`Income.Create`/`Money.Create` lanzan `ArgumentException`/
`ArgumentNullException` en invariantes no cubiertas por el validador
(p. ej. `date < 1900-01-01`, `source > 50`, `Guid.Empty` si se escapa
del validador). Sin el catch, una excepción de dominio escapa del use
case. **Decisión:** envolver ambas factories en
`try { ... } catch (ArgumentException ex) { _logger.LogWarning(ex, "Domain invariant violation while registering income."); return Result.Failure("DomainInvariant", ex.Message); }`.
`ArgumentNullException` deriva de `ArgumentException` → queda cubierta.

### D-7 — `Description`/`Note` se pasan **sin trim** a `Income.Create`

La entidad normaliza (trima `Description`, normaliza `Note`/`Source` a
`null` si whitespace — `Income.cs` INV-4/INV-5/INV-6, líneas 235-264).
Un `.Trim()` manual en el use case sería redundante (doble-trim). El
validador **sólo chequea** `Must(d => d.Trim().Length > 0)` (no muta).
Espejo de F-004 P3 DP-3/DP-4.

### D-8 — `DateOnly.FromDateTime(_clock.UtcNow)` para el umbral de fecha futura

`_clock.UtcNow.DateOnly` no existe (no hay prop `DateOnly` en `DateTime`).
Se usa `DateOnly.FromDateTime(audit)` (API real de .NET). Espejo de
`RegisterExpenseUseCase` línea 78.

### OPEN-1 (resuelto) — 5ª regla `CategoryId` del validador INCLUIDA

El draft de AC-3 listaba 4 reglas y omitía `CategoryId`. **Decisión
locked:** incluir la regla adaptada a la nullability de `Income`:

```csharp
RuleFor(c => c.CategoryId)
    .NotEqual(Guid.Empty)
    .When(c => c.CategoryId is not null)
    .WithMessage("La categoría no es válida.");
```

Justificación: paridad con F-004 (que tiene la regla incondicional) +
surfacer `Guid.Empty` como `"Validation"` (limpio, por campo) en vez de
`"DomainInvariant"` (vía catch). Si se omite, el catch de D-6 lo atrapa
(funcionalmente seguro, menos ergonómico) — pero la decisión es
**incluir las 5 reglas**.

### OPEN-2 (resuelto) — `source = null` al registrar

`source` se pasa `null` a `Income.Create`. Su seteo (p. ej. default
`"manual"`) se difiere a F-005 (`UpdateIncomeUseCase`). No se inventa
un default ahora para no dejar el campo con un valor arbitrario en la
BD hasta F-005; `null` es un estado válido y soportado.

### OPEN-3 (resuelto) — Token plano `"FutureDateWarning"`

El warning es el token plano `"FutureDateWarning"` (paridad con F-004
P3 DP-1), no un formato estructurado con fechas embebidas. Cambiarlo
requeriría backport a F-004; el formato estructurado queda como mejora
transversal futura.

### Otras decisiones de forma

- **`Currency` vive en `ControlDeFinanzasIA.Domain.Enums`** (verificado en
  `RegisterExpenseCommand.cs` `using`) — el comando y validador importan
  ese namespace. `Money` vive en `ControlDeFinanzasIA.Domain.ValueObjects`.
- **`RegisterIncomeResult` no referencia DTOs** (sin `using Application.DTOs`).
  Lleva `Guid IncomeId` + `IReadOnlyList<string> Warnings`.
- **`RegisterIncomeUseCase` no referencia `Application.DTOs`** (a diferencia
  de F-004 que sí, porque mapea `ExpenseDto`). Sí referencia
  `Application.Abstractions`, `Application.Common`, `Domain.Transactions`,
  `Domain.ValueObjects`, `FluentValidation`, `FluentValidation.Results`,
  `Microsoft.Extensions.Logging`.

---

## 4. Criterios de aceptación

La feature se considera completada cuando **todos** los ACs siguientes
se cumplen, verificables mediante tests automatizados y revisión de
código. Cada AC incluye escenarios Given/When/Then. **Las formas
corregidas (D-1..D-8, OPEN-1/2/3) son las que rigen — no el sketch
original de AC-6.**

### AC-1 — `RegisterIncomeCommand` record

- [ ] `src/ControlDeFinanzasIA.Application/UseCases/Incomes/RegisterIncome/RegisterIncomeCommand.cs`
      existe como `public sealed record RegisterIncomeCommand(
      Guid? CategoryId, DateOnly Date, decimal Amount, Currency Currency,
      string Description, string? Note)`.
- [ ] File-scoped namespace
      `ControlDeFinanzasIA.Application.UseCases.Incomes.RegisterIncome`;
      `using ControlDeFinanzasIA.Domain.Enums;`; XML doc en el tipo.
- [ ] **`CategoryId` es `Guid?`** (nullable — diferencia vs
      `RegisterExpenseCommand` que es `Guid` required). Sin `Id`, sin
      `Source`, sin audit.

```csharp
using ControlDeFinanzasIA.Domain.Enums;

namespace ControlDeFinanzasIA.Application.UseCases.Incomes.RegisterIncome;

/// <summary>
/// Immutable command to register a new income. The identity, audit
/// timestamps and source are generated by the domain entity / use case,
/// not supplied by the caller. <see cref="CategoryId"/> may be null
/// (an income without a category is valid).
/// </summary>
public sealed record RegisterIncomeCommand(
    Guid? CategoryId,
    DateOnly Date,
    decimal Amount,
    Currency Currency,
    string Description,
    string? Note);
```

- **GIVEN** el proyecto compila con este archivo presente.
- **WHEN** un caller construye `new RegisterIncomeCommand(null, date, 100m, Currency.DOP, "Sueldo", null)`.
- **THEN** las 6 props se asignan; `CategoryId == null` es válido.

### AC-2 — `RegisterIncomeResult` record

- [ ] `src/.../UseCases/Incomes/RegisterIncome/RegisterIncomeResult.cs`
      existe como `public sealed record RegisterIncomeResult(
      Guid IncomeId, IReadOnlyList<string> Warnings)`.
- [ ] File-scoped namespace; XML doc. `Warnings` nunca null (lista
      vacía si no hay warnings). Sin `using` nuevo (`Guid` e
      `IReadOnlyList<>` son implícitos con ImplicitUsings).

```csharp
namespace ControlDeFinanzasIA.Application.UseCases.Incomes.RegisterIncome;

/// <summary>
/// Wrapper returned on a successful
/// <see cref="RegisterIncomeUseCase.ExecuteAsync"/> call. It carries the
/// generated income id (not a full DTO — <c>IncomeDto</c> is scope of
/// F-008) and an immutable list of warnings.
/// </summary>
/// <remarks>
/// <see cref="Warnings"/> is never null: it is an empty list when no
/// warnings apply. A future date produces a <c>"FutureDateWarning"</c>
/// entry; the income is still accepted.
/// </remarks>
public sealed record RegisterIncomeResult(
    Guid IncomeId,
    IReadOnlyList<string> Warnings);
```

- **GIVEN** el use case retorna `Result.Success(new RegisterIncomeResult(id, new List<string>()))`.
- **WHEN** el caller hace `(RegisterIncomeResult)result.Value!`.
- **THEN** `IncomeId` coincide con el generado y `Warnings` es una lista vacía (no null).

### AC-3 — `RegisterIncomeValidator` (5 reglas sync, incluye `CategoryId`)

- [ ] `src/.../UseCases/Incomes/RegisterIncome/RegisterIncomeValidator.cs`
      es `public sealed class RegisterIncomeValidator : AbstractValidator<RegisterIncomeCommand>`.
- [ ] File-scoped namespace; `using FluentValidation;`
      `using ControlDeFinanzasIA.Domain.Enums;`; XML doc en la clase.
- [ ] **5 reglas sync** (OPEN-1 resuelto): `CategoryId` (condicional a
      not-null), `Amount`, `Description`, `Note`, `Currency`. **Sin**
      regla de `Date` futura (es warning, no fallo). Sin
      `MustAsync`/`CustomAsync` (D-4).

```csharp
using ControlDeFinanzasIA.Domain.Enums;
using FluentValidation;

namespace ControlDeFinanzasIA.Application.UseCases.Incomes.RegisterIncome;

/// <summary>
/// Validates a <see cref="RegisterIncomeCommand"/> using synchronous
/// FluentValidation rules. Async validators are intentionally avoided
/// to prevent <c>AsyncValidatorInvokedSynchronouslyException</c>.
/// </summary>
/// <remarks>
/// <see cref="RegisterIncomeCommand.CategoryId"/> is nullable: the
/// <c>NotEqual(Guid.Empty)</c> rule only applies when a category is
/// supplied (<c>.When(c => c.CategoryId is not null)</c>). A null
/// category is valid (an income may have no category).
/// </remarks>
public sealed class RegisterIncomeValidator : AbstractValidator<RegisterIncomeCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RegisterIncomeValidator"/> class.
    /// </summary>
    public RegisterIncomeValidator()
    {
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
            .MaximumLength(500)
            .When(c => c.Note is not null)
            .WithMessage("La nota no puede superar 500 caracteres.");

        RuleFor(c => c.Currency)
            .IsInEnum().WithMessage("La moneda no es válida.");
    }
}
```

> **Nota de estilo:** se usa el patrón **per-rule `.WithMessage()`**
> (espejo de `RegisterExpenseValidator`), no un `.WithMessage` único al
> final de la cadena — así cada fallo recibe su mensaje relevante
> (obligatoria vs. máximo vs. whitespace-only).

- **GIVEN** un comando con `Description = "   "`.
- **WHEN** `_validator.Validate(command)`.
- **THEN** `IsValid == false` y `Errors` contiene
  `"La descripción no puede contener solo espacios en blanco."`.
- **GIVEN** un comando con `CategoryId = null`.
- **WHEN** `_validator.Validate(command)`.
- **THEN** `IsValid == true` (la regla `CategoryId` se omite por `.When`).

### AC-4 — `RegisterIncomeUseCase` sealed class

- [ ] `src/.../UseCases/Incomes/RegisterIncome/RegisterIncomeUseCase.cs`
      es `public sealed class RegisterIncomeUseCase`.
- [ ] Campos privados readonly: `IIncomeRepository _incomeRepository`,
      `IClock _clock`, `IValidator<RegisterIncomeCommand> _validator`,
      `ILogger<RegisterIncomeUseCase> _logger`.
- [ ] Constructor `public RegisterIncomeUseCase(IIncomeRepository incomeRepository, IClock clock, IValidator<RegisterIncomeCommand> validator, ILogger<RegisterIncomeUseCase> logger)`.
- [ ] File-scoped namespace; `using` al tope; XML doc summary en la clase
      (documenta el cast `(RegisterIncomeResult)result.Value!`).

- **GIVEN** la clase declarada con las 4 dependencias inyectadas.
- **WHEN** el contenedor DI (o un test) la instancia con `FakeIncomeRepository`, `FakeClock`, `RegisterIncomeValidator`, `NullLogger<>`.
- **THEN** compila como `sealed` y las 4 dependencias quedan asignadas.

### AC-5 — Firma `ExecuteAsync` (non-generic)

- [ ] `public async Task<Result> ExecuteAsync(RegisterIncomeCommand command, CancellationToken ct = default)`.
- [ ] Retorna **`Task<Result>`** non-generic (D-2), no `Task<Result<RegisterIncomeResult>>`.
- [ ] XML doc en `ExecuteAsync` documenta que el caller hace
      `(RegisterIncomeResult)result.Value!` tras `IsSuccess`.

- **GIVEN** la firma declarada.
- **WHEN** se compila contra `Result` non-generic (F-005 Fase 1).
- **THEN** compila sin referenciar ningún `Result<T>` (no existe).

### AC-6 — Lógica de `ExecuteAsync` (formas corregidas D-1..D-8)

- [ ] El cuerpo implementa el flujo de 6 pasos siguiente (espejo de
      `RegisterExpenseUseCase` con las 2 adaptaciones de `Income`):
      `CategoryId` nullable + 7º param `source = null`.

```csharp
public async Task<Result> ExecuteAsync(RegisterIncomeCommand command, CancellationToken ct = default)
{
    ArgumentNullException.ThrowIfNull(command);

    ValidationResult validation = _validator.Validate(command);   // sync (D-4)
    if (!validation.IsValid)
    {
        IReadOnlyDictionary<string, string[]> details = validation.Errors   // D-5
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray(),
                StringComparer.Ordinal);

        return Result.Failure(
            "Validation",
            "El comando para registrar el ingreso no es válido.",
            details);
    }

    DateTime audit = _clock.UtcNow;                                 // ADR-007; se lee una vez
    var warnings = new List<string>();
    if (command.Date > DateOnly.FromDateTime(audit))                // D-8
    {
        warnings.Add("FutureDateWarning");                          // OPEN-3
    }

    Income income;
    try
    {
        var money = Money.Create(command.Amount, command.Currency);
        income = Income.Create(
            command.CategoryId,    // Guid? (D-1)
            command.Date,
            money,                 // Money (D-1)
            command.Description,   // as-is (D-7)
            command.Note,          // as-is (D-7)
            null,                  // source — diferido a F-005 (OPEN-2)
            audit);                // createdAt (D-1, 7º param)
    }
    catch (ArgumentException ex)    // D-6; cubre ArgumentNullException
    {
        _logger.LogWarning(ex, "Domain invariant violation while registering income.");
        return Result.Failure("DomainInvariant", ex.Message);
    }

    Result addResult = await _incomeRepository.AddAsync(income, ct);
    if (!addResult.IsSuccess)
    {
        return addResult;           // propaga "CategoryNotFound" del repo F-002 (D-2)
    }

    return Result.Success(new RegisterIncomeResult(income.Id, warnings));
}
```

- **GIVEN** un comando válido con `CategoryId` sembrado.
- **WHEN** `ExecuteAsync(command)`.
- **THEN** `IsSuccess == true`, `Value` castea a `RegisterIncomeResult`,
  `IncomeId != Guid.Empty`, `Warnings` vacío (fecha no futura).
- **GIVEN** un comando con `Amount = 0`.
- **WHEN** `ExecuteAsync(command)`.
- **THEN** `IsSuccess == false`, `Error.Code == "Validation"`,
  `Error.Details` contiene `"Amount"`, el repo **no** fue invocado.

### AC-7 — Registro DI en `Bootstrap.ConfigureServices`

- [ ] `src/.../UI/Composition/Bootstrap.cs` (modificado) añade, tras
      los registros existentes de `RegisterExpenseUseCase` y
      `IValidator<RegisterExpenseCommand>` (líneas 60-62):
      `services.AddTransient<RegisterIncomeUseCase>();` y
      `services.AddTransient<IValidator<RegisterIncomeCommand>, RegisterIncomeValidator>();`.
- [ ] Añade `using ControlDeFinanzasIA.Application.UseCases.Incomes.RegisterIncome;`
      al tope (junto al `using ...Expenses.RegisterExpense;` existente).
- [ ] El XML doc de `ConfigureServices` menciona tanto el use case de
      gastos como el de ingresos. **No** se modifica `AddPersistence`
      (F-002 ya registró `IIncomeRepository`).

- **GIVEN** `Bootstrap.ConfigureServices` registra `RegisterExpenseUseCase` + su validator hoy.
- **WHEN** se aplican los 2 `AddTransient` + el `using`.
- **THEN** el contenedor resuelve `RegisterIncomeUseCase` e
  `IValidator<RegisterIncomeCommand>` → `RegisterIncomeValidator`
  como `Transient`; `AddPersistence` intacto.

### AC-8 — Tests unitarios (≥10; 12 planeados)

- [ ] `tests/ControlDeFinanzasIA.Tests.Unit/Application/Incomes/RegisterIncome/RegisterIncomeUseCaseTests.cs`
      existe con 12 tests (§6), pure xUnit `Assert.*` (sin
      FluentAssertions, sin mocking library).
- [ ] Fakes: `FakeIncomeRepository` (nuevo en `TestDoubles/`, espejo de
      `FakeExpenseRepository` — 6 métodos, `AddedIncomes` +
      `SetAddResult(Result)`), `FakeClock` (existente en `TestDoubles/`),
      `NullLogger<RegisterIncomeUseCase>.Instance` (de
      `Microsoft.Extensions.Logging.Abstractions`), y el validador **real**
      `RegisterIncomeValidator` (no fake).
- [ ] Helper `CreateValidCommand()` construye un comando canónico
      (`CategoryId = _validCategoryId`, `Date = today`, `Amount = 100m`,
      `Currency = DOP`, `Description = "Sueldo"`, `Note = null`).

- **GIVEN** los 12 tests implementados según §6.
- **WHEN** `dotnet test -c Debug --no-build --filter "FullyQualifiedName~RegisterIncomeUseCaseTests"`.
- **THEN** ≥10 (12) pasan en verde.

### AC-9 — Gates de verificación

- [ ] `dotnet build -c Release --nologo` → 0 warnings, 0 errors.
- [ ] `dotnet build -c Debug --nologo` → 0 warnings, 0 errors.
- [ ] `dotnet test -c Debug --no-build --nologo --filter "FullyQualifiedName~RegisterIncomeUseCaseTests"` → ≥10 (12) passed.
- [ ] `dotnet test -c Debug --no-build --nologo --filter "FullyQualifiedName~ArchitectureTests"` → 11/11 (R-1..R-5).
- [ ] `dotnet test -c Debug --no-build --nologo` (suite completa) → ~263 baseline + ~12 nuevos ≈ **~275 verdes** (+ 1 fallo pre-existente si lo hay).
- [ ] `dotnet format style --verify-no-changes` → exit 0.

> Las cuentas de tests son **aproximadas** (línea base según contrato
> de lanzamiento); el gate real es "0 failures + suite previa sigue
> verde + ~12 nuevos unit verdes". El implementador (sdd-apply) debe
> reportar las cuentas exactas observadas.

### AC-10 — Sin scope creep

- [ ] **NO** `UpdateExpenseUseCase`/`UpdateIncomeUseCase`/`DeleteTransactionUseCase` (4-005).
- [ ] **NO** UI `IncomeView`/`RegisterIncomeFormViewModel`/`GetIncomesQuery`/`IncomeDto`/`IncomeExtensions.ToDto` (4-008).
- [ ] **NO** modificaciones a `IIncomeRepository.cs`/`IncomeRepository.cs`/`IncomeFilter.cs` (completos desde F-002).
- [ ] **NO** modificaciones a `Income.cs`/`IncomeConfiguration.cs`/`AppDbContext.cs`/`Money.cs`/`Currency.cs`/`Result.cs`/`Error`/`PagedResult.cs`.
- [ ] **NO** modificaciones a `Expense*` (`Expense.cs`, `ExpenseConfiguration`, `ExpenseRepository`, `IExpenseRepository`, `ExpenseFilter`, DTOs/Extensions de Expense).
- [ ] **NO** nuevas migraciones EF Core. **NO** `ServiceCollectionExtensions.cs`.
- [ ] **NO** FluentAssertions. **NO** mocking library. **NO** paquetes NuGet nuevos.

- **GIVEN** el diff de F-003.
- **WHEN** se inspecciona contra la lista de archivos in-scope (§5).
- **THEN** no hay archivos fuera de la lista modificados; no hay
  referencias a 4-005/4-008 en el código de producción.

### AC-11 — Fecha futura es WARNING, no fallo de validación

- [ ] El XML doc de `RegisterIncomeResult.Warnings` (y/o
      `RegisterIncomeCommand.Date`) documenta: "Las fechas futuras
      producen un `FutureDateWarning` en el resultado; el comando se
      acepta de todas formas." (espejo F-004 P3 DP-1).
- [ ] El validador **no** tiene regla de techo superior para `Date`.
      `Income.Create` sólo floor `1900-01-01` (INV-3), no ceiling.

- **GIVEN** un comando con `Date = 2099-12-31` y `FakeClock.UtcNow = 2026-01-15`.
- **WHEN** `ExecuteAsync(command)`.
- **THEN** `IsSuccess == true` y `Warnings.Contains("FutureDateWarning")`.

### AC-12 — Propagación de `"CategoryNotFound"` del repositorio

- [ ] Cuando `command.CategoryId != null` Y el repo devuelve
      `Result.Failure("CategoryNotFound", ...)` desde `AddAsync`, el use
      case retorna **ese exacto** `Result` (sin envolver ni renombra).
      La validación de FK está delegada en el repo F-002 (D-5 de F-002).

- **GIVEN** `FakeIncomeRepository.SetAddResult(Result.Failure("CategoryNotFound", "La categoría no existe."))`.
- **WHEN** `ExecuteAsync(command)` con `CategoryId` válido por shape.
- **THEN** `IsSuccess == false`, `Error.Code == "CategoryNotFound"`,
  `Error.Message` es el del repo (sin re-envoltorio).

---

## 5. Áreas afectadas

| Archivo | Acción | Descripción |
| --- | --- | --- |
| `src/ControlDeFinanzasIA.Application/UseCases/Incomes/RegisterIncome/RegisterIncomeCommand.cs` | **New** | `sealed record`, 6 props (`CategoryId` nullable `Guid?`), file-scoped ns, XML doc. Sin `Source`, sin `Id`, sin audit. |
| `src/ControlDeFinanzasIA.Application/UseCases/Incomes/RegisterIncome/RegisterIncomeValidator.cs` | **New** | `sealed class : AbstractValidator<RegisterIncomeCommand>`, 5 reglas sync (OPEN-1: `CategoryId` condicional). |
| `src/ControlDeFinanzasIA.Application/UseCases/Incomes/RegisterIncome/RegisterIncomeResult.cs` | **New** | `sealed record (Guid IncomeId, IReadOnlyList<string> Warnings)`. Sin DTO (F-008). |
| `src/ControlDeFinanzasIA.Application/UseCases/Incomes/RegisterIncome/RegisterIncomeUseCase.cs` | **New** | `sealed class`, 4 deps, `ExecuteAsync → Task<Result>` (non-generic, D-2). Flujo D-1..D-8. |
| `src/ControlDeFinanzasIA.UI/Composition/Bootstrap.cs` | **Modified** | 2 `AddTransient` (use case + `IValidator<RegisterIncomeCommand>`) + `using ...Incomes.RegisterIncome;` + XML doc. **NO** `ServiceCollectionExtensions.cs` (D-3). |
| `tests/ControlDeFinanzasIA.Tests.Unit/TestDoubles/FakeIncomeRepository.cs` | **New** | Fake manual `IIncomeRepository` (espejo de `FakeExpenseRepository`): 6 métodos, `AddedIncomes` + `SetAddResult(Result)`. Los 5 métodos no-Add retornan defaults. |
| `tests/ControlDeFinanzasIA.Tests.Unit/Application/Incomes/RegisterIncome/RegisterIncomeUseCaseTests.cs` | **New** | 12 tests, pure xUnit `Assert.*` (§6). |

**Archivos NO modificados (verificados presentes y correctos):**

`Income.cs`, `IncomeConfiguration.cs`, `AppDbContext.cs` (`DbSet<Income>
Incomes` ya existe), `Money.cs`, `Currency.cs`, `Result.cs`, `Error`
record, `PagedResult.cs`, `IIncomeRepository.cs` (6 métodos desde
F-002), `IncomeRepository.cs`, `IncomeFilter.cs`, `Expense.cs`,
`ExpenseConfiguration.cs`, `ExpenseRepository.cs`, `IExpenseRepository.cs`,
`ExpenseFilter.cs`, `ExpenseDto.cs`, `ExpenseExtensions.cs`,
`RegisterExpense*`, `IClock.cs`, `ServiceCollectionExtensions.cs`,
cualquier migración, cualquier archivo de UI (salvo `Bootstrap.cs`),
`Directory.Build.props`, `Directory.Packages.props`, cualquier `.csproj`.

**Modificaciones a `.csproj`:** ninguna (sin paquetes nuevos, sin
referencias de proyecto nuevas — FluentValidation ya referenciado en
`Application.csproj` vía CPM).

---

## 6. Plan de tests unitarios

Ubicación:
`tests/ControlDeFinanzasIA.Tests.Unit/Application/Incomes/RegisterIncome/RegisterIncomeUseCaseTests.cs`.

Espejo estructural de `RegisterExpenseUseCaseTests.cs` (162 líneas),
con las adaptaciones de `Income` (`CategoryId` nullable en el helper de
comando; result lleva `IncomeId` no DTO; repo fake es
`FakeIncomeRepository`). Patrón de fixture verificado:

```csharp
private static readonly Guid _validCategoryId = new("11111111-1111-1111-1111-000000000001");

private readonly FakeClock _clock;
private readonly FakeIncomeRepository _repository;
private readonly RegisterIncomeValidator _validator;
private readonly RegisterIncomeUseCase _useCase;

public RegisterIncomeUseCaseTests()
{
    _clock = new FakeClock { UtcNow = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc) };
    _repository = new FakeIncomeRepository();
    _validator = new RegisterIncomeValidator();
    _useCase = new RegisterIncomeUseCase(_repository, _clock, _validator, NullLogger<RegisterIncomeUseCase>.Instance);
}

private RegisterIncomeCommand CreateValidCommand()
    => new(_validCategoryId, DateOnly.FromDateTime(_clock.UtcNow), 100m, Currency.DOP, "Sueldo", null);
```

### `FakeIncomeRepository` (espejo de `FakeExpenseRepository`)

```csharp
public sealed class FakeIncomeRepository : IIncomeRepository
{
    private readonly List<Income> _addedIncomes = new();
    private Result _addResult = Result.Success(null!);

    public IReadOnlyList<Income> AddedIncomes => _addedIncomes;
    public void SetAddResult(Result result) => _addResult = result;

    public Task<Result> AddAsync(Income income, CancellationToken ct = default)
    {
        _addedIncomes.Add(income);
        return Task.FromResult(_addResult);
    }

    public Task<Result> UpdateAsync(Income income, CancellationToken ct = default)
        => Task.FromResult(Result.Success(null!));

    public Task<Result> RemoveAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Result.Success(null!));

    public Task<Income?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult<Income?>(null);

    public Task<IReadOnlyList<Income>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Income>>(Array.Empty<Income>());

    public Task<PagedResult<Income>> GetByFilterAsync(IncomeFilter filter, CancellationToken ct = default)
        => Task.FromResult(new PagedResult<Income>(new List<Income>(), 0, 1, 50));
}
```

> El fake se coloca en `TestDoubles/FakeIncomeRepository.cs` (namespace
> `ControlDeFinanzasIA.Tests.Unit.TestDoubles`) para reuso futuro por
> F-005/F-008. Los 5 métodos no-Add retornan **defaults** (espejo de
> `FakeExpenseRepository`), no `NotImplementedException` — más robusto
> y consistente con el patrón establecido.

### Tests (12 planeados)

| # | Método | GIVEN / WHEN / THEN |
| --- | --- | --- |
| T-1 | `ExecuteAsync_WithValidCommand_ReturnsSuccessWithIncomeId` | **GIVEN** comando canónico (`CategoryId` válido, `Date` = hoy del reloj). **WHEN** `ExecuteAsync(command)`. **THEN** `IsSuccess` true; `Value` es `RegisterIncomeResult`; `IncomeId != Guid.Empty`; `Warnings` vacío. |
| T-2 | `ExecuteAsync_WithZeroAmount_ReturnsValidationFailure` | **GIVEN** `Amount = 0`. **WHEN** `ExecuteAsync`. **THEN** `IsSuccess` false; `Error.Code == "Validation"`; `Error.Details` contiene `"Amount"`; `_repository.AddedIncomes` vacío (repo no invocado). |
| T-3 | `ExecuteAsync_WithNegativeAmount_ReturnsValidationFailure` | **GIVEN** `Amount = -1` (`[Theory]` con T-2 o `[InlineData]`). **WHEN** `ExecuteAsync`. **THEN** `IsSuccess` false; `Error.Code == "Validation"`; details contiene `"Amount"`. |
| T-4 | `ExecuteAsync_WithEmptyDescription_ReturnsValidationFailure` | **GIVEN** `Description = ""`, `"   "`, `"\t"` (`[Theory]`/`[InlineData]`). **WHEN** `ExecuteAsync`. **THEN** `IsSuccess` false; `Error.Code == "Validation"`; details contiene `"Description"`; repo no invocado. |
| T-5 | `ExecuteAsync_WithDescriptionOver200Chars_ReturnsValidationFailure` | **GIVEN** `Description` de 201 chars. **WHEN** `ExecuteAsync`. **THEN** `IsSuccess` false; `Error.Code == "Validation"`; details contiene `"Description"`. |
| T-6 | `ExecuteAsync_WithNoteOver500Chars_ReturnsValidationFailure` | **GIVEN** `Note` de 501 chars. **WHEN** `ExecuteAsync`. **THEN** `IsSuccess` false; `Error.Code == "Validation"`; details contiene `"Note"`. |
| T-7 | `ExecuteAsync_WithInvalidCurrencyEnum_ReturnsValidationFailure` | **GIVEN** `Currency = (Currency)999`. **WHEN** `ExecuteAsync`. **THEN** `IsSuccess` false; `Error.Code == "Validation"`; details contiene `"Currency"`. |
| T-8 | `ExecuteAsync_WithFutureDate_ReturnsSuccessWithFutureDateWarning` | **GIVEN** `_clock.UtcNow = 2026-01-15`; `command.Date = 2026-01-20`. **WHEN** `ExecuteAsync`. **THEN** `IsSuccess` true; `Warnings.Contains("FutureDateWarning")`; `AddedIncomes.Count == 1`. |
| T-9 | `ExecuteAsync_WithValidCategory_PersistsAndReturnsSuccess` | **GIVEN** comando con `CategoryId` válido. **WHEN** `ExecuteAsync`. **THEN** `IsSuccess` true; `AddedIncomes.Count == 1`; `result.IncomeId == AddedIncomes[0].Id`. |
| T-10 | `ExecuteAsync_WithUnknownCategory_ReturnsCategoryNotFoundFailure` | **GIVEN** `_repository.SetAddResult(Result.Failure("CategoryNotFound", "La categoría no existe."))`. **WHEN** `ExecuteAsync(command)` con `CategoryId` válido por shape. **THEN** `IsSuccess` false; `Error.Code == "CategoryNotFound"` (propagado sin re-envoltorio); `Error.Message` es el del repo. |
| T-11 | `ExecuteAsync_WithNullCategory_PersistsWithoutCategory` | **GIVEN** `command.CategoryId = null`. **WHEN** `ExecuteAsync`. **THEN** `IsSuccess` true; `AddedIncomes.Count == 1`; `AddedIncomes[0].CategoryId == null` (ingreso sin categoría es válido — FK `SetNull` de F-002). |
| T-12 | `ExecuteAsync_AddAsyncSucceeds_AuditDatesFromClock` | **GIVEN** `_clock.UtcNow = 2026-03-10 14:30 UTC`. **WHEN** `ExecuteAsync`. **THEN** `IsSuccess` true; `AddedIncomes[0].CreatedAt == _clock.UtcNow` y `AddedIncomes[0].UpdatedAt == CreatedAt` (sourcing desde `IClock`, no `DateTime.UtcNow`). |

**Marco y paquetes:** xUnit (ya en `Tests.Unit.csproj`).
`Microsoft.Extensions.Logging.Abstractions` ya referenciado (lo usa
`RegisterExpenseUseCaseTests`). **Sin paquetes nuevos**; **sin
FluentAssertions**; **sin mocking**. Aserciones con `Assert.*`
(`Assert.True`, `Assert.False`, `Assert.Equal`, `Assert.NotEqual`,
`Assert.NotNull`, `Assert.IsType`, `Assert.Contains`, `Assert.Empty`,
`Assert.Single`).

**Cobertura:** happy paths (T-1, T-9, T-12), edge cases (T-8 fecha
futura, T-11 categoría null), error states (T-2..T-7 validación, T-10
`CategoryNotFound` propagado).

---

## 7. Fuera de alcance

F-003 **no** hace lo siguiente (cada ítem vive en otra feature):

- **`UpdateIncomeUseCase`** + `UpdateIncomeCommand` + `UpdateIncomeValidator` (4-005).
- **`UpdateExpenseUseCase`** + **`DeleteTransactionUseCase`** (4-005).
- **UI `IncomeView`** + `RegisterIncomeFormViewModel` (4-008).
- **`GetIncomesQuery`** + `GetIncomesQueryRequest` (4-008).
- **`IncomeDto`** + `IncomeExtensions.ToDto` (4-008) — **por eso el
  result lleva `IncomeId`, no un DTO.**
- **Seteo del campo `source`** (diferido a F-005 `UpdateIncomeUseCase`).
  F-003 lo pasa `null` (OPEN-2).
- **Tests aislados del validador** (`RegisterIncomeValidatorTests.cs`).
  El contrato los excluye explícitamente; el validador se ejercita
  indirectamente vía los tests del use case (a diferencia de F-004 P3,
  que sí los tenía).
- **Lógica de `TagIds`** en filtros (4-007) — irrelevante para F-003 (escritura).
- **Soft-delete** / `IsDeleted`.
- **Multi-moneda** real / `PaymentMethod` (Phase 5).
- **Modificaciones a `Expense*`**.
- **Modificaciones a `Income.cs` / `IncomeConfiguration.cs` / `AppDbContext.cs`** (F-001 los provee).
- **Modificaciones a `IIncomeRepository.cs` / `IncomeRepository.cs` / `IncomeFilter.cs`** (F-002 los completó).
- **Nuevas migraciones** EF Core (F-001 creó `AddIncomesTable`).
- **Paquetes NuGet nuevos** (en particular, no FluentAssertions, no mocking).
- **Modificaciones a `ServiceCollectionExtensions.cs`** (D-3: la DI de
  use cases vive en `Bootstrap.cs`, no en Infrastructure).
- **CI workflow**, **`Directory.Packages.props`**, **`Directory.Build.props`**: sin cambios.

---

## 8. Plan de rollback

Cambio **aditivo** en Application + UI composition root + Tests.Unit
(sin tocar Domain, Infrastructure, migraciones, `Result`,
`IIncomeRepository`, `IClock`). Revertir en orden:

1. **Eliminar** la carpeta `UseCases/Incomes/RegisterIncome/` (4
   archivos nuevos), `TestDoubles/FakeIncomeRepository.cs` y
   `Application/Incomes/RegisterIncome/RegisterIncomeUseCaseTests.cs`.
2. **Revertir** `Bootstrap.cs`: quitar los 2 `AddTransient`
   (`RegisterIncomeUseCase` + `IValidator<RegisterIncomeCommand>`) y el
   `using ControlDeFinanzasIA.Application.UseCases.Incomes.RegisterIncome;`;
   restaurar el XML doc previo de `ConfigureServices`.
3. **Verificar** `dotnet build` limpio y `dotnet test` suite previa en
   verde.
4. No hay migración EF ni datos de usuario en juego (la tabla `Incomes`
   la creó F-001 y se conserva). NetArchTest R-1..R-5 no se afectan: los
   tipos nuevos viven en `Application` (sólo `Domain` + `FluentValidation`,
   ya referenciados — R-2 ok) y los registros DI en el composition root
   de `UI` (R-4 ok); ningún tipo nuevo en `Domain`/`Infrastructure`.

---

## 9. Riesgos y mitigaciones

| # | Riesgo | Prob. | Impacto | Mitigación |
| --- | --- | --- | --- | --- |
| R-1 | **D-1/D-2/D-3: el contrato de lanzamiento "committed" no compila contra el código real** (`Income.Create` 7-param, `Result` non-generic, DI en `Bootstrap`). | Alta (verificado) | Alto | Correcciones D-1..D-3 aplicadas en el proposal y ratificadas en esta spec. El implementador (sdd-apply) debe usar las formas corregidas de AC-1..AC-7, **no** el sketch original de AC-6 del contrato. |
| R-2 | Omitir la 5ª regla `CategoryId` del validador → `Guid.Empty` escapa como `"DomainInvariant"` (vía catch) en vez de `"Validation"` (por campo). | Media | Bajo | **OPEN-1 resuelto = incluir la regla** (AC-3). Si aun así se omite, el catch de D-6 lo atrapa (funcionalmente seguro, menos ergonómico). Test T-10 + la regla blindan ambas rutas. |
| R-3 | `IncomeRepository.AddAsync` con `CategoryId == null` → el repo skip-ea FK (F-002 D-5); el use case añade guard defensivo incorrecto. | Baja | Medio | Test T-11 (`ExecuteAsync_WithNullCategory_PersistsWithoutCategory`) blinda la rama `null`. Confiar en F-002; **no** añadir guard extra en el use case. |
| R-4 | El implementador pasa `command.Description.Trim()` a `Income.Create` (doble-trim) o setea `source = "manual"` (scope creep F-005). | Media | Bajo | D-7 + AC-6 fijan `Description`/`Note` as-is y `source = null`. Verificar en sdd-verify. |
| R-5 | El implementador registra la DI en `ServiceCollectionExtensions.cs` (Infrastructure) rompiendo R-3/R-4. | Media | Alto | D-3 + AC-7 fijan `Bootstrap.cs` (UI composition root). `ServiceCollectionExtensions.cs` en la lista de NO-modificar. |
| R-6 | `Money.Create`/`Income.Create` lanzan `ArgumentException` que escapa del use case (sin el catch de D-6). | Media | Medio | D-6 + AC-6 envuelven ambas factories en `try/catch (ArgumentException ex)` → `"DomainInvariant"`. `ArgumentNullException` deriva de `ArgumentException` → cubierta. |
| R-7 | El implementador construye `RegisterIncomeResult` desde `addResult.Value` (el repo F-002 devuelve `Result.Success(null!)` sin Value) en vez de `income.Id` local. | Media | Alto | AC-6 fija `Result.Success(new RegisterIncomeResult(income.Id, warnings))` desde la instancia local. Test T-9 verifica `IncomeId == AddedIncomes[0].Id`. |
| R-8 | Engram `mem_save` no disponible en el toolset del sub-agente (verificado: sin funciones `mem_*`). | Alta (verificado) | Bajo | Respaldo en filesystem (proposal `.atl/sdd/f-003p4-…/proposal.md` + esta spec). El orquestador backfillará a Engram con los topic keys `sdd/f-003p4-register-income-use-case/{proposal,spec}` (precedente `f-002p4`). El sub-agente **no fabrica observation IDs**. |
| R-9 | FluentValidation 11 lanza `AsyncValidatorInvokedSynchronouslyException`. | Baja | Medio | D-4: todas las reglas sync; el use case invoca `Validate` no `ValidateAsync`. Sin `MustAsync`/`CustomAsync`. |
| R-10 | Scope creep hacia F-005 (Update) / F-008 (UI/`IncomeDto`) / tests aislados del validador. | Media | Alto | AC-10 lo prohíbe explícitamente; §7 lista el out-of-scope. Verificar en sdd-verify. |

---

## 10. Referencias

- **Propuesta (filesystem):**
  `.atl/sdd/f-003p4-register-income-use-case/proposal.md` (251 líneas)
  — PRD de origen. Sus correcciones D-1..D-8 y las decisiones OPEN
  #1/#2/#3 (resueltas: 5ª regla incluida, `source = null`, token plano)
  son la base de esta spec. **Las formas corregidas rigen sobre el
  sketch original de AC-6 del contrato de lanzamiento.**
- **Propuesta (Engram objetivo):** topic key
  `sdd/f-003p4-register-income-use-case/proposal` (pendiente de
  persistencia por el orquestador — ver R-8).
- **Spec (Engram objetivo):** topic key
  `sdd/f-003p4-register-income-use-case/spec` (pendiente de
  persistencia por el orquestador — ver R-8).
- **F-001 Fase 4 (archivada):**
  `spec/features/phase-04/F-001-income-entity-config-migration.done.md`
  — entidad `Income` (invariantes INV-1..INV-7, `CategoryId` nullable,
  `Source`, `CreatedAt`/`UpdatedAt`), `IncomeConfiguration` (FK
  `SetNull`, `OwnsOne` de `Money` `HasPrecision(18,4)`), migración
  `AddIncomesTable`, `DbSet<Income> Incomes`.
- **F-002 Fase 4 (archivada — dependencia directa):**
  `spec/features/phase-04/F-002-income-repository.done.md` (728 líneas)
  — `IncomeRepository` (6 métodos, `AddAsync` con validación FK
  condicional D-5: skip si `CategoryId == null`, `"CategoryNotFound"`
  si no existe), `IncomeFilter` (6 props con `TagIds` placeholder),
  `IIncomeRepository.GetByFilterAsync`, registro scoped en
  `AddPersistence`. **F-003 consume `IIncomeRepository.AddAsync` sin
  modificar la interface.**
- **F-004 Fase 3 (archivada — patrón espejo):**
  `spec/features/phase-03/F-004-register-expense-use-case.done.md`
  (401 líneas) — `RegisterExpenseUseCase`/`Command`/`Validator`/`Result`
  que F-003 replica con las 2 adaptaciones de `Income`.
- **Phase plan:** `spec/phases/phase-04/features.md` §RF-6 (líneas
  162-168 — `RegisterIncomeUseCase` + `RegisterIncomeCommand` +
  `RegisterIncomeValidator` + `RegisterIncomeResult`).
- **Convención de contrato:** `spec/features/phase-01/F-006-contrato-interfaces-repo.done.md`
  (commands `Task<Result>` vs queries `Task<T>`).
- **Tipos comunes:** `spec/features/phase-01/F-005-comuntypes-result-pagedresult.done.md`
  (`Result` non-generic + `Value: object?` + `Error(code, message, details?)`).
- **Abstracciones:** `spec/features/phase-01/F-004-Abstracciones-clock-pathresolver.done.md`
  (`IClock.UtcNow`; `FakeClock` en `Tests.Unit/TestDoubles/`).
- **Arquitectura:** `docs/architecture.md` §4 (R-1..R-5), §7 (use cases
  devuelven `Result`), §19 (ADR-007 `IClock` inyectado para auditoría).
- **Constitución:** `spec/constitution/mission.md`,
  `spec/constitution/tech-stack.md` (.NET 8 LTS, FluentValidation
  11.10.0, async/await extremo a extremo, errors-as-values, MVVM estricto).
- **Código verificado (fuentes de la spec):**
  - `src/.../Application/UseCases/Expenses/RegisterExpense/RegisterExpenseCommand.cs` (15 líneas — espejo del comando; `CategoryId` es `Guid` required allí, `Guid?` aquí).
  - `src/.../Application/UseCases/Expenses/RegisterExpense/RegisterExpenseValidator.cs` (35 líneas — espejo del validador per-rule `.WithMessage()`).
  - `src/.../Application/UseCases/Expenses/RegisterExpense/RegisterExpenseResult.cs` (11 líneas — lleva `ExpenseDto`; aquí lleva `Guid IncomeId`).
  - `src/.../Application/UseCases/Expenses/RegisterExpense/RegisterExpenseUseCase.cs` (110 líneas — espejo del flujo `ExecuteAsync`: `audit` local, `DateOnly.FromDateTime(audit)`, try/catch `ArgumentException` → `"DomainInvariant"`, propaga `addResult`).
  - `src/.../Application/Common/Result.cs` (47 líneas — `Result` non-generic, `Error(code, message, details?)`).
  - `src/.../Application/Abstractions/IIncomeRepository.cs` (71 líneas — 6 métodos desde F-002; `AddAsync → Task<Result>`).
  - `src/.../Application/Abstractions/IClock.cs` (43 líneas — `DateTime UtcNow { get; }`).
  - `src/.../Domain/Transactions/Income.cs` (268 líneas — `Create` 7 params líneas 125-132, `Guid.NewGuid()` interno, INV-2..INV-7, normaliza trim/null).
  - `src/.../UI/Composition/Bootstrap.cs` (119 líneas — DI de use cases en líneas 60-62; **no** `ServiceCollectionExtensions`).
  - `tests/.../Application/Expenses/RegisterExpenseUseCaseTests.cs` (162 líneas — espejo de tests; patrón fixture + `NullLogger<>.Instance`).
  - `tests/.../TestDoubles/FakeExpenseRepository.cs` (99 líneas — espejo de `FakeIncomeRepository`).
  - `tests/.../TestDoubles/FakeClock.cs` (21 líneas — `public DateTime UtcNow { get; set; }`, ya existe).
- **FluentValidation 11.x — Sync vs async validators:**
  [docs.fluentvalidation.net/async](https://docs.fluentvalidation.net/en/latest/async.html)
  (reglas sync; `AsyncValidatorInvokedSynchronouslyException`).
