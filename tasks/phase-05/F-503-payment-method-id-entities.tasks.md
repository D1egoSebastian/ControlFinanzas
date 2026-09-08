# Tareas — F-503: PaymentMethodId en Expense + Income

> **Spec:** `spec/features/phase-05/F-503-payment-method-id-entities.spec.md`
> **Fuentes de verdad:** la spec, `spec/phases/phase-05/features.md`
> (§RF-1, §RF-2, §9 feature 5-001), `docs/vision.md` §4.2,
> entidades existentes `Expense.cs` e `Income.cs`, tests existentes
> `ExpenseTests.cs` e `IncomeTests.cs`.
>
> Cada tarea es implementable en menos de 2 horas. El proyecto
> debe seguir compilando al finalizar cada una. La numeración es
> local a esta feature.

---

## Resumen de tareas

| ID | Tarea | Estimación | Depende de |
| --- | --- | --- | --- |
| T-001 | Verificar call sites + plan de compatibilidad | XS | — |
| T-002 | Modificar `Expense` (property + Create + Update + ValidateAndNormalize) | S | T-001 |
| T-003 | Modificar `Income` (property + Create + Update + ValidateAndNormalize) | S | T-001 |
| T-004 | Actualizar test helpers + agregar ~12 tests | M | T-002, T-003 |
| T-005 | Verificación final: build, tests, format, arquitectónica, cierre | S | T-004 |

**Total estimado:** ~2-3 horas. T-002/T-003 son mecánicos
(espejo del otro). T-004 es la más larga.

---

## T-001 — Verificar call sites + plan de compatibilidad

**Objetivo**

Confirmar que el blast radius de modificar `Create` / `Update`
es manejable antes de tocar las entidades.

**Descripción**

### Búsqueda de call sites

Ejecutar (mentalmente o con grep):

```bash
grep -rn "Expense\.Create(" src tests
grep -rn "Expense\.Update(" src tests
grep -rn "Income\.Create(" src tests
grep -rn "Income\.Update(" src tests
```

**Resultado esperado** (de la spec §12):
- `Expense.Create`: solo en `RegisterExpenseUseCase.cs:87`.
- `Expense.Update`: cero call sites (todavía — los use cases de
  update se añadirán en F-004).
- `Income.Create`: solo en `RegisterIncomeUseCase.cs:86`.
- `Income.Update`: cero call sites.

### Plan

- `Create(...)` con parámetro default `null` → todos los call
  sites existentes siguen compilando.
- `Update(...)` con parámetro obligatorio → si F-004 añade
  call sites, esos usarán el valor explícito. Como NO hay
  call sites actuales, agregar el parámetro sin default es
  seguro.

### Verificación de compilación pre-cambio

```bash
dotnet build -c Debug
```

Debe pasar clean antes de T-002 (baseline).

**Dependencias**

- Ninguna.

**Archivos / proyectos afectados**

- Solo se ejecuta verificación. Ningún archivo modificado.

**Criterios de finalización**

- Búsqueda de call sites realizada.
- `dotnet build` baseline clean.

---

## T-002 — Modificar `Expense` (property + Create + Update + ValidateAndNormalize)

**Objetivo**

Agregar `PaymentMethodId` a `Expense` con INV-N aplicada.

**Descripción**

Modificar `src/ControlDeFinanzasIA.Domain/Transactions/Expense.cs`:

### 1. Agregar propiedad

Después de `UpdatedAt` y antes de `Tags`:

```csharp
/// <summary>
/// Identificador opcional del metodo de pago utilizado
/// (etiqueta: efectivo, debito, credito, transferencia, otro).
/// Es solo una etiqueta, no se llevan balances por metodo.
/// Puede ser <see langword="null"/> cuando el usuario decide no
/// asignar uno. Si esta presente, debe ser distinto de
/// <see cref="Guid.Empty"/> (validado en
/// <see cref="Create"/> y <see cref="Update"/>).
/// </summary>
public Guid? PaymentMethodId { get; private set; }
```

### 2. Modificar `Create` — añadir parámetro al final

**Firma actual:**
```csharp
public static Expense Create(
    Guid categoryId,
    DateOnly date,
    Money amount,
    string description,
    string? note,
    DateTime createdAt)
```

**Firma nueva:**
```csharp
public static Expense Create(
    Guid categoryId,
    DateOnly date,
    Money amount,
    string description,
    string? note,
    DateTime createdAt,
    Guid? paymentMethodId = null)
```

Actualizar el XML doc del método para documentar el nuevo
parámetro y la invariante INV-N.

### 3. Modificar `Update` — añadir parámetro al final (sin default)

**Firma actual:**
```csharp
public void Update(
    Guid categoryId,
    DateOnly date,
    Money amount,
    string description,
    string? note,
    DateTime updatedAt)
```

**Firma nueva:**
```csharp
public void Update(
    Guid categoryId,
    DateOnly date,
    Money amount,
    string description,
    string? note,
    DateTime updatedAt,
    Guid? paymentMethodId)
```

Actualizar el XML doc.

### 4. Modificar helper `ValidateAndNormalize`

**Firma actual:**
```csharp
private static (string Description, string? Note) ValidateAndNormalize(
    Money amount,
    DateOnly date,
    string description,
    string? note,
    Guid categoryId)
```

**Firma nueva:**
```csharp
private static (string Description, string? Note) ValidateAndNormalize(
    Money amount,
    DateOnly date,
    string description,
    string? note,
    Guid categoryId,
    Guid? paymentMethodId)
```

Agregar al inicio (después de los chequeos existentes de amount,
date, categoryId, description, note):

```csharp
if (paymentMethodId == Guid.Empty)
{
    throw new ArgumentException(
        "PaymentMethodId must be null or a non-empty Guid.",
        nameof(paymentMethodId));
}
```

### 5. Pasar `paymentMethodId` al constructor interno y `Update`

En `Create`, después de las validaciones:
```csharp
return new Expense(
    Guid.NewGuid(),
    categoryId,
    date,
    amount,
    normalizedDescription,
    normalizedNote,
    createdAt,
    createdAt,
    paymentMethodId);   // <-- NEW
```

En `Update`, reemplazar la asignación de propiedades para
incluir `PaymentMethodId`:
```csharp
CategoryId = categoryId;
Date = date;
Amount = amount;
Description = normalizedDescription;
Note = normalizedNote;
PaymentMethodId = paymentMethodId;   // <-- NEW
UpdatedAt = updatedAt;
```

### 6. Modificar el constructor interno privado

**Firma actual:**
```csharp
private Expense(
    Guid id,
    Guid categoryId,
    DateOnly date,
    Money amount,
    string description,
    string? note,
    DateTime createdAt,
    DateTime updatedAt)
```

**Firma nueva:**
```csharp
private Expense(
    Guid id,
    Guid categoryId,
    DateOnly date,
    Money amount,
    string description,
    string? note,
    DateTime createdAt,
    DateTime updatedAt,
    Guid? paymentMethodId)
```

Agregar `PaymentMethodId = paymentMethodId;` en el cuerpo del
constructor.

### Verificación

```bash
dotnet build src/ControlDeFinanzasIA.Domain -c Debug
dotnet build -c Debug   # full solution
```

El full build debe pasar **sin cambios** en otros proyectos:
los call sites existentes (`RegisterExpenseUseCase.Create(...)`
sin paymentMethodId) deben seguir compilando gracias al
default `null` del nuevo parámetro.

**Dependencias**

- T-001.

**Archivos / proyectos afectados**

- `src/ControlDeFinanzasIA.Domain/Transactions/Expense.cs`
  (modificado).

**Criterios de finalización**

- `dotnet build`: 0 warnings, 0 errors.
- Tests existentes del proyecto (si los hay para Expense) deben
  seguir pasando (la nueva propiedad aparece como `null` en sus
  assertions; agregar una assertion si era débil).

---

## T-003 — Modificar `Income` (property + Create + Update + ValidateAndNormalize)

**Objetivo**

Espejo de T-002 para `Income`.

**Descripción**

Modificar `src/ControlDeFinanzasIA.Domain/Transactions/Income.cs`
con los mismos 6 pasos que T-002 pero adaptados:

1. Agregar `PaymentMethodId` después de `Source`.
2. `Create(..., Guid? paymentMethodId = null)`.
3. `Update(..., Guid? paymentMethodId)` (sin default).
4. `ValidateAndNormalize` acepta `Guid? paymentMethodId`.
5. Pasar `paymentMethodId` al constructor interno + `Update`.
6. Constructor interno acepta `Guid? paymentMethodId`.

XML docs idénticos (con texto equivalente para `Income`).

**Dependencias**

- T-001.

**Archivos / proyectos afectados**

- `src/ControlDeFinanzasIA.Domain/Transactions/Income.cs`
  (modificado).

**Criterios de finalización**

- `dotnet build`: 0 warnings, 0 errors.

---

## T-004 — Actualizar test helpers + agregar ~12 tests

**Objetivo**

Cubrir el nuevo invariante y comportamiento con tests
unitarios.

**Descripción**

### 1. `tests/ControlDeFinanzasIA.Tests.Unit/Domain/ExpenseTests.cs`

#### Actualizar helper `CreateValidExpense`

```csharp
private static Expense CreateValidExpense(
    Guid? categoryId = null,
    DateOnly? date = null,
    Money? amount = null,
    string? description = null,
    string? note = null,
    DateTime? createdAt = null,
    Guid? paymentMethodId = null)
{
    return Expense.Create(
        categoryId ?? _defaultCategoryId,
        date ?? _defaultDate,
        amount ?? _defaultAmount,
        description ?? _defaultDescription,
        note,
        createdAt ?? _defaultCreatedAt,
        paymentMethodId);
}
```

#### Agregar tests nuevos

```csharp
// === PaymentMethodId (F-503) ===

[Fact]
public void Create_WithPaymentMethodId_ReturnsExpenseWithThatId()
{
    Guid paymentMethodId = Guid.NewGuid();

    Expense expense = CreateValidExpense(paymentMethodId: paymentMethodId);

    Assert.Equal(paymentMethodId, expense.PaymentMethodId);
}

[Fact]
public void Create_WithoutPaymentMethodId_ReturnsExpenseWithNull()
{
    Expense expense = CreateValidExpense();

    Assert.Null(expense.PaymentMethodId);
}

[Fact]
public void Create_WithEmptyPaymentMethodId_ThrowsArgumentException()
{
    ArgumentException ex = Assert.Throws<ArgumentException>(
        () => Expense.Create(
            _defaultCategoryId, _defaultDate, _defaultAmount,
            _defaultDescription, null,
            _defaultCreatedAt,
            paymentMethodId: Guid.Empty));

    Assert.Equal("paymentMethodId", ex.ParamName);
    Assert.Contains("PaymentMethodId", ex.Message);
}

[Fact]
public void Create_WithPaymentMethodId_AssignsPaymentMethodId()
{
    // Covers RF-1: property assignment + visibility.
    Guid paymentMethodId = Guid.NewGuid();
    Expense expense = CreateValidExpense(paymentMethodId: paymentMethodId);

    Assert.Equal(paymentMethodId, expense.PaymentMethodId);
}
```

Si ya hay tests de `Update`, agregar:

```csharp
[Fact]
public void Update_WithPaymentMethodId_AssignsNewPaymentMethodId()
{
    Expense expense = CreateValidExpense();
    DateTime updatedAt = _defaultCreatedAt.AddMinutes(1);
    Guid newPaymentMethodId = Guid.NewGuid();

    expense.Update(
        _defaultCategoryId, _defaultDate, _defaultAmount,
        _defaultDescription, null,
        updatedAt,
        newPaymentMethodId);

    Assert.Equal(newPaymentMethodId, expense.PaymentMethodId);
    Assert.Equal(updatedAt, expense.UpdatedAt);
}

[Fact]
public void Update_WithNullPaymentMethodId_DoesNotThrow()
{
    Expense expense = CreateValidExpense();
    DateTime updatedAt = _defaultCreatedAt.AddMinutes(1);

    // Passing null should be allowed (user chose to remove method).
    expense.Update(
        _defaultCategoryId, _defaultDate, _defaultAmount,
        _defaultDescription, null,
        updatedAt,
        paymentMethodId: null);

    Assert.Null(expense.PaymentMethodId);
}

[Fact]
public void Update_WithEmptyPaymentMethodId_ThrowsArgumentException()
{
    Expense expense = CreateValidExpense();
    DateTime updatedAt = _defaultCreatedAt.AddMinutes(1);

    Assert.Throws<ArgumentException>(
        () => expense.Update(
            _defaultCategoryId, _defaultDate, _defaultAmount,
            _defaultDescription, null,
            updatedAt,
            paymentMethodId: Guid.Empty));
}
```

### 2. `tests/ControlDeFinanzasIA.Tests.Unit/Domain/IncomeTests.cs`

Espejo: actualizar `CreateValidIncome` con parámetro
`Guid? paymentMethodId = null`, agregar los mismos tests
adaptados para `Income`.

**Dependencias**

- T-002, T-003.

**Archivos / proyectos afectados**

- `tests/ControlDeFinanzasIA.Tests.Unit/Domain/ExpenseTests.cs`
  (modificado).
- `tests/ControlDeFinanzasIA.Tests.Unit/Domain/IncomeTests.cs`
  (modificado).

**Criterios de finalización**

- ~6 tests nuevos en `ExpenseTests.cs`.
- ~6 tests nuevos en `IncomeTests.cs`.
- `dotnet test --filter "FullyQualifiedName~ExpenseTests"`:
  todos verdes.
- `dotnet test --filter "FullyQualifiedName~IncomeTests"`:
  todos verdes.

---

## T-005 — Verificación final y cierre

**Objetivo**

Validar que F-503 cumple los criterios de aceptación y dejar el
repo en estado cerrado.

**Descripción**

### Pasos de verificación

1. **Compilación completa:**
   ```bash
   dotnet build -c Debug
   dotnet build -c Release
   ```
   Ambos con 0 warnings, 0 errors.

2. **Format check:**
   ```bash
   dotnet format style --verify-no-changes
   ```
   Exit 0.

3. **Tests completos:**
   ```bash
   dotnet test
   ```
   510 anteriores + ~12 nuevos = ~522 verdes, 0 failed.

4. **Compatibilidad backward (AC-3):** verificar que los call sites
   actuales siguen compilando sin cambios:
   - `RegisterExpenseUseCase.cs` debe seguir usando
     `Expense.Create(...)` sin paymentMethodId.
   - `RegisterIncomeUseCase.cs` análogo.

5. **Modificaciones controladas (AC-8):**
   ```bash
   git diff --name-only
   ```
   Debe mostrar **EXACTAMENTE** estos 4 archivos:
   - `src/ControlDeFinanzasIA.Domain/Transactions/Expense.cs`
   - `src/ControlDeFinanzasIA.Domain/Transactions/Income.cs`
   - `tests/ControlDeFinanzasIA.Tests.Unit/Domain/ExpenseTests.cs`
   - `tests/ControlDeFinanzasIA.Tests.Unit/Domain/IncomeTests.cs`

   Si aparece cualquier otro archivo, el slice falló AC-8.

### Pasos de limpieza

6. **Marcar el spec como completada:** actualizar
   `spec/features/phase-05/F-503-payment-method-id-entities.spec.md`
   línea de estado a "✅ COMPLETADA (fecha)".
7. **Renombrar la spec** a `.done.md`.

### Resumen de AC

| AC | Criterio | Estado |
| --- | --- | --- |
| AC-1 | Propiedades agregadas | `PaymentMethodId` en Expense + Income |
| AC-2 | Create extendido, backward compatible | `dotnet build` clean |
| AC-3 | Update extendido | Zero call sites rotos |
| AC-4 | INV-N aplicada | Guid.Empty lanza ArgumentException |
| AC-5 | Tests verdes | ~12 nuevos, todos verdes |
| AC-6 | Convenciones | XML doc + file-scoped namespace |
| AC-7 | Verificación final | Build + format + tests clean |
| AC-8 | Modificaciones controladas | `git diff` solo muestra 4 archivos |

**Dependencias**

- T-004.

**Archivos / proyectos afectados**

- `spec/features/phase-05/F-503-payment-method-id-entities.spec.md`
  (modificado + renombrado).

**Criterios de finalización**

- `dotnet build -c Debug && -c Release`: clean.
- `dotnet format style --verify-no-changes`: exit 0.
- `dotnet test`: ~522 verde.
- `git diff --name-only` muestra solo los 4 archivos esperados.
- Spec marcada como completada y renombrada.

---

## Orden recomendado

**Orden secuencial estricto:** T-001 → T-002 → T-003 → T-004 → T-005.

T-002 y T-003 se podrían hacer en paralelo (espejo uno del otro),
pero al ser cambios mecánicos es más rápido hacerlos
secuencialmente.

---

## Riesgos de la fase de tareas

| Riesgo | Probabilidad | Impacto | Mitigación |
| --- | --- | --- | --- |
| Call site de `Update` no previsto rompe build | Baja | Alto | T-001 verifica con grep antes |
| Test helper `CreateValidExpense` ignora `paymentMethodId` y rompe coverage | Baja | Bajo | T-004 lo actualiza explícitamente |
| El `ValidateAndNormalize` ya tiene muchos argumentos (6+) y se vuelve ilegible | Media | Bajo | Aceptable; el spec lo justifica |
| Modificación accidental a `ExpenseConfiguration` o repo | Baja | Alto | AC-8 lo verifica con `git diff` |
| Olvidar agregar `paymentMethodId` al constructor interno | Media | Bajo | T-002 paso 6 lo cubre explícitamente |
| Olvidar agregar `PaymentMethodId = paymentMethodId` en `Update` | Media | Bajo | T-002 paso 5 lo cubre explícitamente |

---

## Referencias

- `spec/features/phase-05/F-503-payment-method-id-entities.spec.md`
  — spec completa.
- `spec/phases/phase-05/features.md` §RF-1, §RF-2, §9 (5-001).
- `docs/vision.md` §4.2.
- `src/.../Domain/Transactions/Expense.cs` — patrón de entidad
  rica en invariantes.
- `src/.../Domain/Transactions/Income.cs` — análogo.
- `tests/.../Unit/Domain/ExpenseTests.cs` — patrón de tests.
- `src/.../Application/UseCases/Expenses/RegisterExpense/RegisterExpenseUseCase.cs`
  — call site actual de `Expense.Create`.
