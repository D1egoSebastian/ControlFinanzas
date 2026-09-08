# Specification — F-503: PaymentMethodId en Expense + Income (entidades)

> **Fase:** 5 — Método de pago + multi-moneda DOP/USD
> **Feature:** F-503 (Phase 5, local a esta fase)
> **Estado:** ✅ COMPLETADA (2026-09-07)
> **Spec ID:** SPEC-F503
> **Cubre:** feature 5-001 (`Expense.PaymentMethodId` + `Income.PaymentMethodId`)
>
> **Dependencias:**
> - Phase 1 ✅: `Money`, `Currency`, `DateRange`, `Guid` para IDs.
> - Domain `Catalog/PaymentMethod` ✅: la entidad referenciada
>   existe desde Phase 1 F-006 (como placeholder de dominio).
>
> **Nota de numeración:** este `F-503` es el tercer slice de
> Fase 5. Modifica las entidades `Expense` e `Income` (NO crea
> archivos nuevos). NO toca configs EF Core, migraciones,
> comandos, validadores, DTOs, filtros, repositorios ni UI — esos
> son features 5-002..5-006, 5-010..5-012.

---

## 1. Resumen

Este slice agrega la propiedad `PaymentMethodId` (nullable `Guid`)
a las entidades `Expense` e `Income`, con la invariante INV-N nueva
("si `PaymentMethodId.HasValue`, debe ser distinto de `Guid.Empty`").

Modifica los factories `Create` y `Update` para aceptar el nuevo
parámetro con `default null`, preservando compatibilidad con los
call sites actuales (que serán actualizados en F-004 cuando los
commands cambien su firma).

| Miembro | Cambio |
| --- | --- |
| `Expense.PaymentMethodId` | Nueva propiedad `Guid?` |
| `Income.PaymentMethodId` | Nueva propiedad `Guid?` |
| `Expense.Create(...)` | Parámetro `Guid? paymentMethodId = null` al final |
| `Expense.Update(...)` | Parámetro `Guid? paymentMethodId` (sin default — Update siempre se llama explícitamente) |
| `Income.Create(...)` | Parámetro `Guid? paymentMethodId = null` al final |
| `Income.Update(...)` | Parámetro `Guid? paymentMethodId` (sin default) |
| INV-N (nueva) | Si `PaymentMethodId.HasValue`, debe ser != `Guid.Empty` |

---

## 2. Contexto

`docs/vision.md` §4.2 define que cada movimiento lleva:

> *"Fecha, monto y moneda original (DOP o USD), categoría, tags
> libres opcionales, **método de pago** (etiqueta: efectivo,
> débito, crédito, transferencia, otro). Es solo una etiqueta, no
> se llevan balances por método."*

Phase 4 construyó el CRUD de `Expense` + `Income` con moneda
persistente, tags, category, y descripción. Pero el **método de
pago no está modelado en las entidades** (a pesar de que la tabla
`PaymentMethods` y su repo existen desde Phase 1 F-006).

Este slice cierra esa brecha solo en la capa de dominio. Las
features siguientes (5-002 a 5-006) cablean la columna SQL, los
commands, los DTOs y los filtros.

---

## 3. Objetivo

- Agregar `PaymentMethodId` (Guid?) a `Expense` e `Income`.
- Validar la invariante INV-N en `Create` y `Update`.
- Mantener compatibilidad con los call sites actuales vía
  parámetro default `null`.
- Cubrir la invariante con tests unitarios.

---

## 4. Alcance

### 4.1 En alcance

- **`Expense.PaymentMethodId`**: nueva propiedad pública
  `Guid? PaymentMethodId { get; private set; }`.
- **`Income.PaymentMethodId`**: análogo.
- **`Expense.Create(...)`**: nuevo último parámetro
  `Guid? paymentMethodId = null`. Validación: si `.HasValue`,
  debe ser != `Guid.Empty`.
- **`Expense.Update(...)`**: nuevo parámetro `Guid? paymentMethodId`
  (sin default — Update se llama explícitamente, el caller decide
  pasar `null` o un valor). Misma validación.
- **`Income.Create(...)`**: análogo.
- **`Income.Update(...)`**: análogo.
- **Tests unitarios** en
  `tests/.../Unit/Domain/ExpenseTests.cs` y
  `tests/.../Unit/Domain/IncomeTests.cs`:
  - `Create_WithPaymentMethodId_ReturnsExpenseWithIt`.
  - `Create_WithoutPaymentMethodId_ReturnsExpenseWithNull`.
  - `Create_WithEmptyPaymentMethodId_ThrowsArgumentException`.
  - `Update_WithPaymentMethodId_PreservesIt` (solo si ya hay
    tests de `Update`; si no, agregar el coverage básico).
  - `Update_WithEmptyPaymentMethodId_ThrowsArgumentException`.

### 4.2 Fuera de alcance (este slice)

- **Modificar `ExpenseConfiguration.cs` / `IncomeConfiguration.cs`**
  (F-002 / 5-002: agregar FK + índice).
- **Crear la migración `AddPaymentMethodIdToTransactions`** (5-002).
- **Modificar comandos** (`RegisterExpenseCommand`, etc.) — los
  call sites actuales siguen funcionando con `default null` y se
  actualizarán en F-004.
- **Modificar validadores** (`RegisterExpenseValidator`, etc.) — F-004.
- **Modificar DTOs** (`ExpenseDto`, `IncomeDto`) — F-005.
- **Modificar repos** (`ExpenseRepository.GetByFilterAsync`) — F-006.
- **Modificar filtros** (`ExpenseFilter.PaymentMethodIds`) — F-006.
- **UI** (combobox de método, segunda línea de conversión) —
  5-010, 5-011, 5-012.
- **`Bootstrap.cs` update** — 5-013.

---

## 5. Casos de uso

### CU-1 — Crear gasto con método de pago

```csharp
var expense = Expense.Create(
    categoryId, date, amount,
    "Almuerzo NYC", null,
    createdAt,
    paymentMethodId: tarjetaCreditoId);

// expense.PaymentMethodId == tarjetaCreditoId
```

### CU-2 — Crear gasto sin método de pago

```csharp
var expense = Expense.Create(
    categoryId, date, amount,
    "Café", null, createdAt);
// paymentMethodId defaults to null

// expense.PaymentMethodId == null
```

### CU-3 — Intentar crear con Guid.Empty (debe fallar)

```csharp
Expense.Create(
    categoryId, date, amount,
    "Café", null, createdAt,
    paymentMethodId: Guid.Empty);
// throws ArgumentException("PaymentMethodId must be null or non-empty Guid.")
```

### CU-4 — Update con método nuevo

```csharp
expense.Update(
    categoryId, date, amount, "Café", null,
    updatedAt,
    paymentMethodId: tarjetaDebitoId);

// expense.PaymentMethodId == tarjetaDebitoId
```

---

## 6. Requisitos funcionales

### RF-1 — Propiedad `PaymentMethodId`

Ambas entidades exponen:

```csharp
/// <summary>
/// Identificador del metodo de pago utilizado (etiqueta: efectivo,
/// debito, credito, transferencia, otro). Es opcional: <c>null</c>
/// cuando el usuario decide no asignar uno. No se llevan balances
/// por metodo.
/// </summary>
public Guid? PaymentMethodId { get; private set; }
```

- `private set` porque la mutación solo ocurre vía `Create` /
  `Update` (entidad rica en invariantes).
- Nullable: el dominio no fuerza la asignación.

### RF-2 — INV-N (`PaymentMethodId` invariante)

Si `paymentMethodId.HasValue`, debe ser distinto de `Guid.Empty`.

```csharp
if (paymentMethodId == Guid.Empty)
{
    throw new ArgumentException(
        "PaymentMethodId must be null or a non-empty Guid.",
        nameof(paymentMethodId));
}
```

Mensaje y `paramName` consistentes con INV-2 (CategoryId)

### RF-3 — `Create` con parámetro default

```csharp
public static Expense Create(
    Guid categoryId,
    DateOnly date,
    Money amount,
    string description,
    string? note,
    DateTime createdAt,
    Guid? paymentMethodId = null)   // <-- NEW (con default null)
```

- Default `null` mantiene los call sites actuales (solo
  `RegisterExpenseUseCase.RegisterExpenseAsync`) compilando.
- En F-004, ese call site será actualizado para pasar el
  paymentMethodId explícitamente.

### RF-4 — `Update` con parámetro requerido

```csharp
public void Update(
    Guid categoryId,
    DateOnly date,
    Money amount,
    string description,
    string? note,
    DateTime updatedAt,
    Guid? paymentMethodId)   // <-- NEW (sin default)
```

- Sin default porque `Update` siempre se llama explícitamente y
  debe forzar al caller a decidir (incluso si decide `null`).
- Test coverage: `Update_WithNullPaymentMethodId_DoesNotThrow`
  (es válido pasar null).

### RF-5 — Validación centralizada

Para evitar duplicar la validación de `PaymentMethodId` en
`Create` y `Update`, agregar a la helper privada
`ValidateAndNormalize(...)` ya existente:

```csharp
// en Expense.ValidateAndNormalize (y analogamente en Income):
if (paymentMethodId == Guid.Empty)
{
    throw new ArgumentException(
        "PaymentMethodId must be null or a non-empty Guid.",
        nameof(paymentMethodId));
}
```

La firma de `ValidateAndNormalize` se extiende para aceptar el
nuevo parámetro.

---

## 7. Requisitos no funcionales

### RNF-1 — Build limpio

- `dotnet build -c Debug && -c Release`: 0 warnings, 0 errors.
- `dotnet format style --verify-no-changes`: exit 0.

### RNF-2 — Cobertura de tests

- ~6 tests nuevos en `ExpenseTests.cs` + 6 en `IncomeTests.cs`.
- Total estimado: 510 anteriores + ~10 = **~520 tests verdes**.

### RNF-3 — Compatibilidad backward

- Los call sites actuales de `Expense.Create` y `Income.Create`
  (solo `RegisterExpenseUseCase` y `RegisterIncomeUseCase`)
  siguen compilando sin cambios gracias al parámetro default.
- Los call sites de `Update` (si existen) actualmente pasan
  menos argumentos → con el nuevo parámetro obligatorio
  podrían romperse. **Verificación previa:** ejecutar build antes
  de cambiar `Update`; si hay call sites que rompen, ajustar el
  default a `= null` también en `Update` o actualizar esos
  call sites.

### RNF-4 — Cero impacto adicional

- **Modificación permitida (AC-5):** solo `Expense.cs` y `Income.cs`
  + sus archivos de tests. Cero cambios en configs EF Core,
  migraciones, comandos, validadores, DTOs, filtros, repos, UI.

---

## 8. Reglas de negocio

### RN-1 — `PaymentMethodId` es opcional

Vision §4.2: *"el método de pago (etiqueta: efectivo, débito,
crédito, transferencia, otro). Es solo una etiqueta, no se
llevan balances por método."*

Por tanto, el default es `null`. El caller (use case, F-004)
decide si asignar un método o no. La entidad no fuerza la
asignación.

### RN-2 — `Guid.Empty` no es válido

Si alguien intenta pasar `Guid.Empty` como `PaymentMethodId`,
la entidad rechaza con `ArgumentException`. Esto evita bugs en
los callers (e.g.,忘记 inicializar un `Guid newGuid = default(Guid)`).

### RN-3 — PaymentMethodId no se valida contra la tabla

Esta entidad (dominio) **no** verifica que el `PaymentMethodId`
exista en `PaymentMethods`. Esa validación ocurre en la capa
de aplicación o infraestructura (F-005 / F-006, vía
`PaymentMethodRepository.GetByIdAsync(...)` o unique constraint en
la migración). Mantener el dominio libre de dependencias.

---

## 9. Modelo de dominio (diff)

```diff
  public sealed class Expense
  {
      public Guid Id { get; private set; }
      public Guid CategoryId { get; private set; }
      public DateOnly Date { get; private set; }
      public Money Amount { get; private set; } = null!;
      public string Description { get; private set; } = string.Empty;
      public string? Note { get; private set; }
      public DateTime CreatedAt { get; private set; }
      public DateTime UpdatedAt { get; private set; }
+     public Guid? PaymentMethodId { get; private set; }
      public ICollection<Tag> Tags { get; private set; } = new List<Tag>();

      ...

-     public static Expense Create(Guid categoryId, DateOnly date, Money amount, string description, string? note, DateTime createdAt)
+     public static Expense Create(Guid categoryId, DateOnly date, Money amount, string description, string? note, DateTime createdAt, Guid? paymentMethodId = null)

-     public void Update(Guid categoryId, DateOnly date, Money amount, string description, string? note, DateTime updatedAt)
+     public void Update(Guid categoryId, DateOnly date, Money amount, string description, string? note, DateTime updatedAt, Guid? paymentMethodId)
```

Idéntico para `Income` (más `string? source`).

---

## 10. Validaciones

| # | Validación | Cuándo | Comportamiento |
| --- | --- | --- | --- |
| V-1 | `paymentMethodId == Guid.Empty` | `ValidateAndNormalize` (Create + Update) | `ArgumentException` con mensaje claro |
| V-2 | `paymentMethodId == null` | siempre | Permitido (default) |

No se revalidan INV-1 a INV-6 (Amount, Date, CategoryId,
Description, Note) — ya están en `ValidateAndNormalize`.

---

## 11. Casos borde

### EC-1 — Default `null` en Create

Llamadas existentes (sin pasar el parámetro) siguen
funcionando. `expense.PaymentMethodId == null`.

### EC-2 — Update con `null` (borrar método)

Si un usuario registra un gasto con método "Efectivo" y luego
lo actualiza sin método, debe poder pasar `paymentMethodId: null`
explícitamente. Cubierto por test.

### EC-3 — El PaymentMethodId no apunta a un PaymentMethod existente

Esto NO se valida aquí. Si el caller pasa un `Guid` que no
existe en `PaymentMethods`, el INSERT fallará con FK violation
en la BD. Cubierto por F-005 (`PaymentMethodNotFound` error code
en el repository).

### EC-4 — Reordenar de placeholders a instancias reales

En Phase 1 F-006, los repos eran placeholders (entidades con
solo `Id`). Cuando F-005 los implemente con EF Core, los
`PaymentMethod` reales existirán en la BD. Este slice no
necesita coordinarse con esa implementación — la entidad
acepta cualquier `Guid` no-vacío.

---

## 12. Dependencias

### 12.1 Prerrequisitos

- Phase 1 ✅: `Money`, `Currency`, `Guid`.
- Phase 1 F-006 ✅: `PaymentMethod` existe como placeholder
  (no se valida aquí, pero documenta que la entidad target
  existe).
- F-501 + F-502 ✅: no son prerequisito de este slice, pero
  demuestra que la multi-moneda rama no depende de PaymentMethod
  (ortogonales).

### 12.2 Consumidores futuros

- F-004: commands pasan `paymentMethodId` explícitamente
  (`RegisterExpenseCommand` añade `Guid? PaymentMethodId`).
- F-005: repos validan FK a `PaymentMethods`.
- F-006: filtros aceptan `PaymentMethodIds`.
- F-010: UI muestra picker de método.

### 12.3 Bloqueos

- **Verificación pre-Create:** ejecutar `dotnet build` antes de
  tocar `Update` — si el nuevo parámetro rompe algún call site
  no previsto, ajustar.
- **Ninguno para Create** (default null = backward compatible).

---

## 13. Fuera de alcance (detallado)

- **Modificaciones a `ExpenseConfiguration.cs` /
  `IncomeConfiguration.cs`** (5-002). El EF Core mapping es F-002.
- **Migración** (5-002).
- **`RegisterExpenseCommand` / `UpdateExpenseCommand`** (5-004).
  Esos cambios vienen cuando 5-004 actualice esos commands y el
  caller (`RegisterExpenseUseCase`) empiece a pasar el
  paymentMethodId.
- **DTOs `ExpenseDto.PaymentMethodId`** (5-005).
- **Repos `GetByFilterAsync(PaymentMethodIds)`** (5-006).
- **UI** (5-010+).
- **Bootstrap / i18n** (5-012, 5-013).

---

## 14. Criterios de aceptación

### AC-1 — Propiedades agregadas

- [ ] `Expense.PaymentMethodId` existe como propiedad pública
      `Guid?` con `private set`.
- [ ] `Income.PaymentMethodId` existe como propiedad pública
      `Guid?` con `private set`.

### AC-2 — Create extendido

- [ ] `Expense.Create(..., Guid? paymentMethodId = null)` compila.
- [ ] `Income.Create(..., Guid? paymentMethodId = null)` compila.
- [ ] Los call sites actuales de `Create` siguen compilando
      (validar con `dotnet build`).

### AC-3 — Update extendido

- [ ] `Expense.Update(..., Guid? paymentMethodId)` compila.
- [ ] `Income.Update(..., Guid? paymentMethodId)` compila.
- [ ] Si hay call sites de `Update` que rompen por el nuevo
      parámetro, actualizarlos (con `null` explícito o el valor
      correspondiente) o agregar `= null` default si la
      búsqueda de call sites revela que la mayoría no tiene
      `PaymentMethodId` aún.

### AC-4 — INV-N aplicada

- [ ] `Expense.Create` con `paymentMethodId: Guid.Empty` lanza
      `ArgumentException`.
- [ ] `Expense.Update` con `paymentMethodId: Guid.Empty` lanza
      `ArgumentException`.
- [ ] Análogos para `Income`.

### AC-5 — Tests verdes

- [ ] `ExpenseTests`: ~6 tests nuevos del nuevo invariante +
      comportamiento (Create con/sin paymentMethodId, Create
      con Guid.Empty, Update con/sin/nul empty).
- [ ] `IncomeTests`: análogo.
- [ ] `dotnet test`: 510 anteriores + ~10 = ~520 verdes,
      0 failed.

### AC-6 — Convenciones

- [ ] XML doc en la nueva propiedad.
- [ ] File-scoped namespace preservado.
- [ ] Mensaje de error de la invariante es claro y menciona
      `nameof(paymentMethodId)`.

### AC-7 — Verificación final

- [ ] `dotnet build -c Debug && -c Release`: clean.
- [ ] `dotnet format style --verify-no-changes`: exit 0.
- [ ] `dotnet test`: ~520 verde.
- [ ] Spec renombrada a `.done.md` (en T-005 de las tasks).

### AC-8 — Modificaciones controladas

- [ ] `git diff --name-only` muestra SOLO 4 archivos
      modificados: `Expense.cs`, `Income.cs`, `ExpenseTests.cs`,
      `IncomeTests.cs`. Cero archivos nuevos. Cero
      modificaciones adicionales.

---

## 15. Estructura de archivos esperada

```
src/ControlDeFinanzasIA.Domain/Transactions/
  Expense.cs         ← modificado (+1 property, +param en Create/Update)
  Income.cs          ← modificado (+1 property, +param en Create/Update)

tests/ControlDeFinanzasIA.Tests.Unit/Domain/
  ExpenseTests.cs    ← modificado (+~6 tests)
  IncomeTests.cs     ← modificado (+~6 tests)
```

Archivos **NO modificados** (AC-8):
- `ExpenseConfiguration.cs`, `IncomeConfiguration.cs`,
  `AppDbContext.cs`, ningún `Configuration`, ninguna
  migration, ningún comando, ningún validador, ningún DTO,
  ningún repo, ningún ViewModel, ninguna `.axaml`, ningún
  `.resx`, `Bootstrap.cs`, los specs/tasks previos.

---

## 16. Decisiones de diseño explícitas

### 16.1 — Parámetro default `null` en `Create`

`Expense.Create(..., Guid? paymentMethodId = null)` mantiene la
compatibilidad con el call site actual en
`RegisterExpenseUseCase.RegisterExpenseAsync` (que no pasa
`paymentMethodId`). El default se omite cuando F-004 actualice
esos use cases para pasar el método explícitamente.

### 16.2 — Parámetro sin default en `Update`

`Update` siempre se llama explícitamente (no es API de creación
del sistema, sino de reasignación). El caller debe decidir
explícitamente si el método cambia o no. Si en F-004 se
descubre que hay call sites que rompen, se agregará `= null`
default por compatibilidad — pero el ideal es forzarlo.

### 16.3 — Validación en `ValidateAndNormalize` (no duplicar)

`Expense.ValidateAndNormalize` ya valida INV-1 a INV-6. Agregamos
la validación de `PaymentMethodId` ahí para mantener un solo
punto de validación. Mismo cambio en `Income.ValidateAndNormalize`.

### 16.4 — No validar FK en el dominio

El dominio no llama a `IPaymentMethodRepository`. Esa validación
vive en F-005 (repo layer) y F-004 (validador). El dominio
acepta cualquier `Guid` no-vacío — confía en que el caller
(use case) pasa un ID válido.

### 16.5 — Sin FK en `OnDelete` aquí

El comportamiento de borrado (`SetNull` vs `Restrict` vs
`Cascade`) se configura en F-002 / F-006 cuando se cablea la
configuración EF Core. Este slice es solo de dominio, no toca
la BD.

### 16.6 — `[NotMapped]` como medida temporal post-implementación

Durante la implementación se descubrió que EF Core mapea
`PaymentMethodId` por convención (es una propiedad pública con
getter/setter), lo cual rompía los integration tests (43/106)
porque la columna SQL no existe aún. La solución pragmática:
agregar `[System.ComponentModel.DataAnnotations.Schema.NotMapped]`
a la propiedad. Esto:

- **Mantiene AC-8**: solo se modifican los 4 archivos planeados
  (Expense.cs, Income.cs y sus tests).
- **Es honesto arquitectónicamente**: el Domain sabe que esta
  propiedad no es persistente todavía (vs. ocultar el problema
  con un `.Ignore(...)` en el config que el Domain no controla).
- **Es transitorio**: F-002 (migración `AddPaymentMethodIdToTransactions`)
  agregará la columna y removerá el `[NotMapped]` en el mismo
  PR/commit.

`[NotMapped]` viene de `System.ComponentModel.DataAnnotations.Schema`
(BCL), NO de EF Core, así que no introduce dependencia de
Infrastructure en Domain.

---

## 17. Riesgos y mitigaciones

| Riesgo | Probabilidad | Impacto | Mitigación |
| --- | --- | --- | --- |
| `Update` con parámetro obligatorio rompe call sites inesperados | Media | Medio | Búsqueda previa con `grep "Expense\.Update\|Income\.Update"` en todo el repo; si encuentra call sites, ajustar con `= null` default |
| Test helpers (`CreateValidExpense`) no aceptan el nuevo parámetro | Alta | Bajo | Actualizar helpers para aceptar `Guid? paymentMethodId = null` |
| El caller (`RegisterExpenseUseCase`) ignora el parámetro nuevo y nunca pasa el método | Media | Bajo | Aceptable para v1; F-004 lo arregla cuando actualice los commands |
| Modificación accidental a `ExpenseConfiguration` | Baja | Alto | AC-8 lo verifica con `git diff` |
| `Guid.Empty == null` comparación vs `Guid.Empty.Equals(null)` | Baja | Bajo | Usar `paymentMethodId == Guid.Empty` que es válido para ambos casos (`null == Guid.Empty` es `false`, `Guid.Empty == Guid.Empty` es `true`) |

---

## 18. Referencias

- `spec/phases/phase-05/features.md` §RF-1 (Expense.PaymentMethodId),
  §RF-2 (Income.PaymentMethodId), §9 (feature ID 5-001).
- `docs/vision.md` §4.2 (método de pago opcional).
- `spec/constitution/tech-stack.md` — convenciones de código.
- `src/.../Domain/Transactions/Expense.cs` — patrón de entidad
  rica en invariantes (`Create`, `Update`, `ValidateAndNormalize`).
- `src/.../Domain/Transactions/Income.cs` — análogo.
- `tests/.../Unit/Domain/ExpenseTests.cs` — patrón de tests
  para invariantes de entidad.
- `tests/.../Unit/Domain/IncomeTests.cs` — análogo.
- `src/.../Application/UseCases/Expenses/RegisterExpense/RegisterExpenseUseCase.cs`
  — único call site actual de `Expense.Create`.
- `src/.../Application/UseCases/Incomes/RegisterIncome/RegisterIncomeUseCase.cs`
  — único call site actual de `Income.Create`.
