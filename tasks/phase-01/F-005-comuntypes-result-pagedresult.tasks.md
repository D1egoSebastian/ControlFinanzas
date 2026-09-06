# Tareas — F-005: Tipos comunes `Result<T>` y `PagedResult<T>`

> **Spec:** `spec/features/phase-01/F-005-comuntypes-result-pagedresult.md`
> **Fuentes de verdad:** la spec, `docs/architecture.md`,
> `docs/vision.md`, `docs/roadmap.md`, `spec/constitution/*`,
> `spec/features/phase-01/F-001-skeleton-solution.done.md` (F-001
> completada, provee la base: proyecto `Application` existe),
> `spec/features/phase-01/F-002-money-value-object.done.md` y
> `F-003-datarange-value-object.done.md` (features de value object
> que siguen el mismo patrón).
>
> Cada tarea es implementable en menos de 2 horas. El proyecto
> debe seguir compilando al finalizar cada una. La numeración
> (T-001...) es local a esta feature.

---

## Resumen de tareas

| ID | Tarea | Estimación | Depende de |
| --- | --- | --- | --- |
| T-001 | Crear `Error` + `Result<T>` (record con constructor privado) + tests | M | — |
| T-002 | Crear `PagedResult<T>` (record con `TotalPages` calculado) + tests | S | — |
| T-003 | Verificación final: tests, format check, build, cobertura, cierre | S | T-001, T-002 |

**Total estimado:** 1 jornada o menos. T-001 es la más larga
(introduce el patrón de constructor privado en un record, que es
delicado); T-002 es más mecánica; T-003 es solo verificación.

---

## T-001 — Crear `Error` + `Result<T>` con constructor privado + tests

**Objetivo**
Establecer el patrón de retorno de use cases y queries:
errores esperados como **valores** (no excepciones). El tipo
`Result<T>` permite que un use case reporte éxito (con un valor) o
fallo (con un `Error`) de forma uniforme, verificable por el
compilador.

**Descripción**
Crear el archivo
`src/ControlDeFinanzasIA.Application/Common/Result.cs` con **dos
tipos** (tightly coupled, mismo archivo por cohesión):

### Tipo 1: `Error` record

```csharp
public record Error(
    string Code,
    string Message,
    IReadOnlyDictionary<string, string[]>? Details = null);
```

- `Code` (string): código legible del error (ej. "ValidationError",
  "NotFound"). No se valida en construcción; es responsabilidad del
  caller usar códigos consistentes.
- `Message` (string): mensaje para el usuario. En inglés (convención
  de la spec, RNF-4).
- `Details` (IReadOnlyDictionary<string, string[]>?): detalles por
  campo. Clave = nombre del campo, valor = array de mensajes.
  Default `null` (sin detalles).
- Equality por valor (auto-generada por `record`).
- File-scoped namespace: `namespace
  ControlDeFinanzasIA.Application.Common;`.
- XML doc en el record y en cada parámetro del primary
  constructor.

### Tipo 2: `Result<T>` record con constructor **privado**

```csharp
public sealed record Result<T>
{
    public bool IsSuccess { get; init; }
    public T? Value { get; init; }
    public Error? Error { get; init; }

    private Result(bool isSuccess, T? value, Error? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    // Factories: único camino público de construcción
    public static Result<T> Success(T value) =>
        new(true, value, null);

    public static Result<T> Failure(Error error) =>
        new(false, default, error);

    public static Result<T> Failure(string code, string message) =>
        Failure(new Error(code, message));

    public static Result<T> Failure(
        string code,
        string message,
        IReadOnlyDictionary<string, string[]> details) =>
        Failure(new Error(code, message, details));
}
```

**Puntos críticos:**

- `Result<T>` es `sealed` (no herencia; use cases no deben poder
  crear subtipos).
- **Sin primary constructor público.** El `record` se define con
  propiedades `init`-only explícitas y un constructor privado.
  Esto garantiza que `new Result<T>(...)` no compile desde fuera
  de la clase.
- **Cuatro factories estáticos** son el único camino de
  construcción:
  - `Success(T value)` — crea `IsSuccess = true, Value = value,
    Error = null`.
  - `Failure(Error error)` — crea `IsSuccess = false,
    Value = default, Error = error`.
  - `Failure(string code, string message)` — overload de
    conveniencia.
  - `Failure(string code, string message, IReadOnlyDictionary<...>
    details)` — overload con detalles.
- `T?` en `Value` permite tipos de referencia (ej. `string`) y
  tipos de valor (ej. `int` con valor por defecto `0`).
- File-scoped namespace: `namespace
  ControlDeFinanzasIA.Application.Common;`.
- XML doc en el record, las propiedades, y cada factory,
  explicando:
  - Por qué el constructor es privado (cumple la invariante).
  - Que el caller debe verificar `IsSuccess` antes de usar `Value`.

### Tests

Crear el archivo
`tests/ControlDeFinanzasIA.Tests.Unit/Common/ResultTests.cs` con:

```csharp
using ControlDeFinanzasIA.Application.Common;

namespace ControlDeFinanzasIA.Tests.Unit.Common;

public class ResultTests
{
    // === AC-2: factory tests ===

    [Fact]
    public void Result_Success_HasIsSuccessTrueAndValue()
    {
        var result = Result.Success(42);
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Result_Success_AcceptsNullValue()
    {
        string? nothing = null;
        var result = Result.Success(nothing!);
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Result_FailureWithError_HasIsSuccessFalseAndError()
    {
        var error = new Error("NotFound", "Item not found");
        var result = Result.Failure<string>(error);
        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void Result_FailureWithCodeAndMessage_CreatesErrorCorrectly()
    {
        var result = Result.Failure<int>("ValidationError", "Amount must be non-negative.");
        Assert.False(result.IsSuccess);
        Assert.Equal("ValidationError", result.Error!.Code);
        Assert.Equal("Amount must be non-negative.", result.Error.Message);
        Assert.Null(result.Error.Details);
    }

    [Fact]
    public void Result_FailureWithDetails_StoresDetailsCorrectly()
    {
        var details = new Dictionary<string, string[]>
        {
            ["Name"] = new[] { "Name is required" },
            ["Amount"] = new[] { "Amount must be non-negative",
                                  "Amount must be finite" }
        };
        var result = Result.Failure<decimal>("ValidationError", "Some fields are invalid.", details);
        Assert.Equal(2, result.Error!.Details!.Count);
        Assert.Equal(new[] { "Name is required" }, result.Error.Details["Name"]);
        Assert.Equal(2, result.Error.Details["Amount"].Length);
    }

    // === AC-3: equality e inmutability ===

    [Fact]
    public void Result_TwoSuccessesWithSameValue_AreEqual()
    {
        Assert.Equal(Result.Success(42), Result.Success(42));
    }

    [Fact]
    public void Result_TwoFailuresWithSameError_AreEqual()
    {
        var error = new Error("X", "y");
        Assert.Equal(Result.Failure<int>(error), Result.Failure<int>(error));
    }

    [Fact]
    public void Result_SuccessAndFailure_AreNotEqual()
    {
        Assert.NotEqual(Result.Success(42), Result.Failure<int>("X", "y"));
    }

    [Fact]
    public void Result_IsImmutable_PropertiesAreInitOnly()
    {
        // Verificable por compilación: intentar asignar result.IsSuccess
        // = true después de la creación no compila (init-only).
        // Este test documenta la expectativa sin verificar
        // dinámicamente (no se puede en runtime con init).
        var result = Result.Success(42);
        Assert.Equal(42, result.Value);
    }

    // === AC-4: pattern matching ===

    [Fact]
    public void Result_PatternMatchingOnIsSuccess_DispatchesCorrectly()
    {
        var successResult = Result.Success(42);
        var failureResult = Result.Failure<int>("X", "y");

        var successOutcome = successResult switch
        {
            { IsSuccess: true, Value: var v } => $"OK: {v}",
            { IsSuccess: false, Error: var e } => $"ERR: {e.Code}",
            _ => "unknown"
        };
        var failureOutcome = failureResult switch
        {
            { IsSuccess: true, Value: var v } => $"OK: {v}",
            { IsSuccess: false, Error: var e } => $"ERR: {e.Code}",
            _ => "unknown"
        };

        Assert.Equal("OK: 42", successOutcome);
        Assert.Equal("ERR: X", failureOutcome);
    }
}
```

**11 tests** que cubren los criterios AC-2, AC-3, y AC-4 de la spec.

**Dependencias**
- F-001 (proyecto `Application` existe).

**Archivos / proyectos afectados**
- `src/ControlDeFinanzasIA.Application/Common/Result.cs` (nuevo).
- `tests/ControlDeFinanzasIA.Tests.Unit/Common/ResultTests.cs`
  (nuevo).

**Criterios de finalización**
- `Result.cs` existe en `src/.../Application/Common/`.
- `Error` y `Result<T>` están definidos en el archivo.
- `Result<T>` tiene constructor privado (verificable intentando
  `new Result<int>(...)` desde un test: debe fallar compilación).
- Las cuatro factories (`Success`, `Failure` × 3) están definidas.
- `ResultTests.cs` existe con los 11 tests.
- Todos los 11 tests pasan.
- `dotnet build`: 0 warnings, 0 errors.
- `dotnet test`: los 11 tests pasan + los anteriores.
- `dotnet format style --verify-no-changes`: pasa.
- `Application` sigue sin `PackageReference`s.

---

## T-002 — Crear `PagedResult<T>` con `TotalPages` calculado + tests

**Objetivo**
Establecer el envoltorio de resultados paginados para queries.
Permite a las queries retornar los items de la página actual
junto con metadatos de paginación (total, página actual, tamaño de
página, total de páginas).

**Descripción**
Crear el archivo
`src/ControlDeFinanzasIA.Application/Common/PagedResult.cs` con:

```csharp
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Total,
    int Page,
    int PageSize)
{
    /// <summary>
    /// Total de páginas. Calculado como ceiling division:
    /// (Total + PageSize - 1) / PageSize. Si PageSize = 0, retorna 0
    /// (defensive, evita DivideByZeroException).
    /// </summary>
    public int TotalPages => PageSize == 0
        ? 0
        : (Total + PageSize - 1) / PageSize;
}
```

- Primary constructor **público** (a diferencia de `Result<T>`, aquí
  no hay invariante que proteger; es solo un contenedor de datos).
- `Items` es `IReadOnlyList<T>` (no `IList<T>`) para garantizar
  inmutabilidad estructural.
- `Page` es 1-based (convención de la UI).
- `TotalPages` es una **propiedad calculada** (no se almacena; se
  calcula de `Total` y `PageSize`).
- File-scoped namespace: `namespace
  ControlDeFinanzasIA.Application.Common;`.
- XML doc en el record, los parámetros del primary constructor, y
  la propiedad calculada.

### Tests

Crear el archivo
`tests/ControlDeFinanzasIA.Tests.Unit/Common/PagedResultTests.cs`
con:

```csharp
using ControlDeFinanzasIA.Application.Common;

namespace ControlDeFinanzasIA.Tests.Unit.Common;

public class PagedResultTests
{
    [Fact]
    public void PagedResult_Constructor_StoresAllProperties()
    {
        var items = new[] { 1, 2, 3 };
        var result = new PagedResult<int>(items, 10, 1, 3);

        Assert.Equal(items, result.Items);
        Assert.Equal(10, result.Total);
        Assert.Equal(1, result.Page);
        Assert.Equal(3, result.PageSize);
    }

    [Fact]
    public void PagedResult_TotalPages_ComputesCorrectly()
    {
        // Caso típico: 100 items, 10 por página = 10 páginas
        var r1 = new PagedResult<int>(Array.Empty<int>(), 100, 1, 10);
        Assert.Equal(10, r1.TotalPages);

        // Caso con remainder: 95 items, 10 por página = 10 páginas (no 9.5)
        var r2 = new PagedResult<int>(Array.Empty<int>(), 95, 1, 10);
        Assert.Equal(10, r2.TotalPages);
    }

    [Fact]
    public void PagedResult_TotalPages_WithZeroItems_IsZero()
    {
        var result = new PagedResult<int>(Array.Empty<int>(), 0, 1, 10);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public void PagedResult_TotalPages_WithZeroPageSize_IsZero()
    {
        // Defensive: PageSize = 0 retorna TotalPages = 0 sin
        // DivideByZeroException. PageSize = 0 es un bug del
        // caller, no un caso de uso.
        var result = new PagedResult<int>(Array.Empty<int>(), 100, 1, 0);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public void PagedResult_TwoSameResults_AreEqual()
    {
        var r1 = new PagedResult<int>(new[] { 1, 2 }, 10, 1, 2);
        var r2 = new PagedResult<int>(new[] { 1, 2 }, 10, 1, 2);
        Assert.Equal(r1, r2);
    }

    [Fact]
    public void PagedResult_DifferentPageNumbers_AreNotEqual()
    {
        var r1 = new PagedResult<int>(new[] { 1, 2 }, 10, 1, 2);
        var r2 = new PagedResult<int>(new[] { 1, 2 }, 10, 2, 2);
        Assert.NotEqual(r1, r2);
    }

    [Fact]
    public void PagedResult_ItemsIsReadOnlyList()
    {
        var result = new PagedResult<int>(new[] { 1, 2 }, 10, 1, 2);
        // Verificable en runtime: el tipo concreto es IReadOnlyList<T>
        Assert.IsAssignableFrom<IReadOnlyList<int>>(result.Items);
    }

    [Fact]
    public void PagedResult_EmptyList_HasZeroItems()
    {
        var result = new PagedResult<int>(Array.Empty<int>(), 0, 1, 10);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalPages);
    }
}
```

**8 tests** que cubren el criterio AC-5 de la spec.

**Dependencias**
- F-001 (proyecto `Application` existe).

**Archivos / proyectos afectados**
- `src/ControlDeFinanzasIA.Application/Common/PagedResult.cs`
  (nuevo).
- `tests/ControlDeFinanzasIA.Tests.Unit/Common/PagedResultTests.cs`
  (nuevo).

**Criterios de finalización**
- `PagedResult.cs` existe en `src/.../Application/Common/`.
- `PagedResult<T>` es `sealed record` con primary constructor
  público.
- Las cuatro propiedades (`Items`, `Total`, `Page`, `PageSize`)
  son init-only.
- `TotalPages` es propiedad calculada (no almacenada).
- `PagedResultTests.cs` existe con los 8 tests.
- Todos los 8 tests pasan.
- `dotnet build`: 0 warnings, 0 errors.
- `dotnet test`: los 8 tests pasan + los anteriores.
- `dotnet format style --verify-no-changes`: pasa.

---

## T-003 — Verificación final, cobertura y cierre

**Objetivo**
Validar que F-005 cumple los criterios de aceptación de la spec
(sección 14), y dejar el repo en estado de "F-005 terminada".

**Descripción**
Esta tarea **no introduce código nuevo**. Consiste en ejecutar
los pasos de verificación y ejecutar las acciones de cierre.

### Pasos de verificación

1. **Compilación:** `dotnet build -c Debug` y `dotnet build -c
   Release` — ambos con 0 warnings, 0 errors.
2. **Tests:** `dotnet test` — todos los tests pasan.
3. **Format check:** `dotnet format style --verify-no-changes` —
   exit 0.
4. **Cobertura:** verificar que `Result.cs` y `PagedResult.cs`
   tienen cobertura ~100%. Sin `coverlet.collector` configurado,
   **verificación manual** (ver tabla abajo).
5. **Aislamiento de Application:** `dotnet list src/.../Application
   package` debe seguir mostrando "No packages were found for this
   framework." (preparación para F-007).
6. **Aislamiento de Domain:** `Domain` no debe referenciar
   `Result<T>` ni `PagedResult<T>` (verificable por `grep` o por
   NetArchTest en F-007).
7. **Layout:** `Result.cs` y `PagedResult.cs` están en
   `src/ControlDeFinanzasIA.Application/Common/`. `ResultTests.cs`
   y `PagedResultTests.cs` están en
   `tests/.../Common/`.

### Pasos de limpieza

8. **Verificar que no hay placeholders que limpiar.** F-002 ya
   eliminó el `F001PlaceholderTests.cs` de `Tests.Unit` en su
   T-007. No hay nada que limpiar para F-005.
9. **Actualizar el desglose de fase:** en
   `spec/phases/phase-01/features.md`, cambiar el estado de F-005
   de "Pendiente" a "✅ COMPLETADA" (mismo formato que F-001,
   F-002, F-003, F-004).
10. **Renombrar la spec:** mover
    `spec/features/phase-01/F-005-comuntypes-result-pagedresult.md`
    a
    `spec/features/phase-01/F-005-comuntypes-result-pagedresult.done.md`
    (convención del proyecto, ver F-001, F-002, F-003, F-004).

### Resumen de cobertura manual (criterio AC-6)

Verificar que cada línea pública de `Result.cs` y `PagedResult.cs`
está cubierta por al menos un test:

| Miembro público | Test |
| --- | --- |
| `Error` constructor | Cobertura implícita (todos los tests de Failure crean Errors) |
| `Error.Code` getter | `Result_FailureWithCodeAndMessage_CreatesErrorCorrectly` |
| `Error.Message` getter | `Result_FailureWithCodeAndMessage_CreatesErrorCorrectly` |
| `Error.Details` getter | `Result_FailureWithDetails_StoresDetailsCorrectly` |
| `Error` equality | Cobertura implícita (record auto-genera) |
| `Result<T>` constructor (privado) | Cobertura implícita (todas las factories lo invocan) |
| `Result<T>.IsSuccess` getter | Todos los tests de Result lo verifican |
| `Result<T>.Value` getter | Tests de Success y equality |
| `Result<T>.Error` getter | Tests de Failure |
| `Result<T>.Success(T)` factory | `Result_Success_HasIsSuccessTrueAndValue` y `Result_Success_AcceptsNullValue` |
| `Result<T>.Failure(Error)` factory | `Result_FailureWithError_HasIsSuccessFalseAndError` |
| `Result<T>.Failure(string, string)` factory | `Result_FailureWithCodeAndMessage_CreatesErrorCorrectly` |
| `Result<T>.Failure(string, string, dict)` factory | `Result_FailureWithDetails_StoresDetailsCorrectly` |
| `Result<T>` equality | `Result_TwoSuccessesWithSameValue_AreEqual`, `Result_TwoFailuresWithSameError_AreEqual`, `Result_SuccessAndFailure_AreNotEqual` |
| `PagedResult<T>` constructor | `PagedResult_Constructor_StoresAllProperties` |
| `PagedResult<T>.Items` getter | `PagedResult_Constructor_StoresAllProperties` |
| `PagedResult<T>.Total` getter | `PagedResult_Constructor_StoresAllProperties` |
| `PagedResult<T>.Page` getter | `PagedResult_Constructor_StoresAllProperties` |
| `PagedResult<T>.PageSize` getter | `PagedResult_Constructor_StoresAllProperties` |
| `PagedResult<T>.TotalPages` getter | `PagedResult_TotalPages_ComputesCorrectly`, `PagedResult_TotalPages_WithZeroItems_IsZero`, `PagedResult_TotalPages_WithZeroPageSize_IsZero`, `PagedResult_EmptyList_HasZeroItems` |
| `PagedResult<T>` equality | `PagedResult_TwoSameResults_AreEqual`, `PagedResult_DifferentPageNumbers_AreNotEqual` |

**Cobertura esperada: ~100%.**

**Dependencias**
- T-001, T-002.

**Archivos / proyectos afectados**
- `spec/phases/phase-01/features.md` (modificado: marcar F-005
  como completada).
- `spec/features/phase-01/F-005-comuntypes-result-pagedresult.md`
  (renombrado a `.done.md`).

**Criterios de finalización**
- `dotnet build` (Debug y Release): 0 warnings, 0 errors.
- `dotnet test`: todos los tests pasan.
- `dotnet format style --verify-no-changes`: exit 0.
- `Result.cs` y `PagedResult.cs` en
  `src/ControlDeFinanzasIA.Application/Common/`.
- `ResultTests.cs` y `PagedResultTests.cs` en `tests/.../Common/`.
- `Application` sigue sin `PackageReference`s.
- `Domain` no referencia `Result<T>` ni `PagedResult<T>`.
- `spec/phases/phase-01/features.md` actualizado con F-005 marcada
  como completada.
- Spec renombrada a `.done.md`.
- **Confirmación punto por punto de los 7 grupos de criterios de
  aceptación de la spec (AC-1 a AC-7)**, presentada al cerrar
  la feature.

---

## Orden recomendado y trabajo en paralelo

**Orden secuencial estricto:** T-001 → T-002 → T-003.

**T-001 y T-002 se pueden hacer en paralelo** (archivos distintos:
`Result.cs` vs `PagedResult.cs`). Con 1 persona, en orden.

**T-001 es la tarea más delicada.** Introduce el patrón de
constructor privado en un `record` en C#. Si se hace bien (el
form explícito con propiedades `init`-only + constructor privado),
T-002 y T-003 son mecánicas. Si se hace con prisa (ej. exponiendo
el primary constructor público, que violaría la invariante), hay
que corregirlo antes de seguir.

---

## Riesgos de la fase de tareas

| Riesgo | Probabilidad | Impacto | Mitigación |
| --- | --- | --- | --- |
| El constructor privado de `Result<T>` no se aplica correctamente (primary constructor público accidental) | Baja | Alto | El spec da el código exacto; revisar con `new Result<int>(...)` desde un test para confirmar que falla compilación. |
| Los tests de pattern matching no cubren todos los casos | Baja | Medio | El test `Result_PatternMatchingOnIsSuccess_DispatchesCorrectly` cubre éxito y fallo. |
| `PagedResult.PageSize = 0` no se maneja defensivamente | Baja | Alto | El test `PagedResult_TotalPages_WithZeroPageSize_IsZero` verifica que retorna 0. |
| `Domain` referencia `Result<T>` o `PagedResult<T>` accidentalmente | Baja | Alto | `Domain` no debería necesitar estos tipos (es lógica de Application). La compilación falla si intenta importarlos. |
| `Application` recibe `PackageReference`s accidentales | Baja | Alto | El spec verifica explícitamente que `dotnet list src/.../Application package` muestra "No packages were found". |
| El `Record<T>` con primary constructor público tiene auto-generado `==` que puede no ser lo esperado | Baja | Bajo | El spec usa la forma explícita (sin primary constructor) para tener control total sobre la API. |

---

## Referencias

- `spec/features/phase-01/F-005-comuntypes-result-pagedresult.md`
  — la spec completa de F-005. Este tasks file es su
  descomposición técnica.
- `spec/features/phase-01/F-001-skeleton-solution.done.md` —
  F-001 completada, provee la base: proyecto `Application`
  existe.
- `spec/features/phase-01/F-002-money-value-object.done.md` —
  provee el patrón de value object con readonly properties; F-005
  usa la misma técnica para `Result<T>`.
- `spec/features/phase-01/F-003-datarange-value-object.done.md` —
  mismo patrón de value object.
- `spec/features/phase-01/F-004-Abstracciones-clock-pathresolver.done.md`
  — sigue el patrón de organización de carpetas
  (`Application/Abstractions/`, `Tests.Unit/TestDoubles/`); F-005 usa
  `Application/Common/`, `Tests.Unit/Common/`.
- `docs/architecture.md` §3 (Capas) y §7 (Composition root) —
  define el patrón de use cases que retornan `Result<T>`.
- `docs/roadmap.md` § Fase 1, F-005 dentro del desglose.
- `spec/constitution/tech-stack.md` — convenciones de código.
- `spec/phases/phase-01/features.md` — F-005 dentro del
  desglose de features de la fase 1.
- `spec/README.md` — convención completa del directorio `spec/`.
- **Documentación de C# records:**
  [learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/record](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/record)
- **Documentación de `IReadOnlyList<T>`:**
  [learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)
