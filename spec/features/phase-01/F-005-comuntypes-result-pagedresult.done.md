# Specification — F-005: Tipos comunes `Result` y `PagedResult<T>`

> **Fase:** 1 — Esqueleto de arquitectura y contratos base
> **Feature:** F-005 (Phase 1, local a esta fase)
> **Estado:** Pendiente de implementación
> **Spec ID:** SPEC-F005
>
> **Nota de numeración:** este `F-005` es el de la **Fase 1**
> (`spec/phases/phase-01/features.md`). Los IDs de feature son
> locales a cada fase; el folder los desambigua. Ver `spec/README.md`
> para la convención completa.
>
> Fuentes de verdad: `docs/vision.md`, `docs/architecture.md`,
> `docs/roadmap.md`, `spec/constitution/*`,
> `spec/phases/phase-01/features.md` (F-005 dentro del desglose de
> la fase 1).

---

## 1. Resumen

F-005 introduce dos tipos comunes en la capa `Application` que
establecen el patrón de retorno de use cases y queries: errores
esperados como **valores** (`Result`), no como excepciones;
resultados paginados (`PagedResult<T>`) con metadatos de paginación.

| Miembro | Tipo | Carpeta |
| --- | --- | --- |
| `Result` | `sealed record` (non-generic) | `src/ControlDeFinanzasIA.Application/Common/Result.cs` |
| `Error` | `record` | `src/ControlDeFinanzasIA.Application/Common/Result.cs` (mismo archivo, tightly coupled) |
| `PagedResult<T>` | `sealed record` (generic) | `src/ControlDeFinanzasIA.Application/Common/PagedResult.cs` |

> **Nota de diseño (revisión 2026-07):** la spec original
> proponía `Result<T>` (genérico con `Value: T?`). Tras múltiples
> intentos de implementación, el compiler del test project daba
> CS0305 ("Using the generic type 'Result<T>' requires 1 type
> arguments") al inferir `T` en llamadas a factories. La forma
> **non-generic** con `Value: object?` resuelve el issue de
> compiler. Trade-off: el caller hace cast explícito:
> `var x = (MiTipo)result.Value;` (mismo patrón que
> `Task<object>`, `Lazy<object>`, etc.). La spec se actualiza
> para reflejar esta decisión.

---

## 2. Contexto

F-005 es la base del manejo de errores en `Application`. Sin un
`Result` definido y probado, cada use case que aparezca desde
la fase 3 inventará su propia forma de devolver éxito/fallo, y
se acabó la consistencia. Definirlo **antes** de tener use cases
reales es la única forma de fijarlo.

`PagedResult<T>` complementa `Result`: las queries que
retornan listas paginadas (ej. "obtener gastos del mes, página 2
de 50") necesitan un envoltorio que incluya los items + metadatos
de paginación. Sin él, cada query inventará su propio tipo.

---

## 3. Objetivo

Establecer el patrón de retorno de use cases y queries en
`Application`: errores esperados como valores (no excepciones), y
resultados paginados con metadatos. Proveer las herramientas para
que los use cases y queries reporten éxito/fallo y paginación de
forma uniforme.

---

## 4. Alcance

### 4.1 En alcance

- `record Error(string Code, string Message,
  IReadOnlyDictionary<string, string[]>? Details = null)` en
  `Application/Common/Result.cs`. Representa un error de negocio
  con código legible, mensaje para el usuario, y detalles opcionales
  (ej. "Errores por campo: Name = ['requerido']").
- `sealed record Result` (non-generic) en
  `Application/Common/Result.cs` con tres propiedades init-only:
  `IsSuccess` (bool), `Value` (T?), `Error` (Error?).
- Constructor **privado** en `Result<T>` (forzado) + factories
  estáticos que son el único camino de construcción:
  - `Result.Success(T value)` — crea un resultado exitoso.
  - `Result.Failure(Error error)` — crea un resultado fallido con
    un `Error` ya construido.
  - `Result.Failure<T>(string code, string message)` — overload
    de conveniencia que construye el `Error` internamente.
  - `Result.Failure<T>(string code, string message,
    IReadOnlyDictionary<string, string[]> details)` — overload
    con detalles por campo.
- `sealed record PagedResult<T>` en
  `Application/Common/PagedResult.cs` con propiedades
  init-only: `Items` (IReadOnlyList<T>), `Total` (int), `Page` (int),
  `PageSize` (int), y una propiedad calculada `TotalPages` (int,
  derivada).
- Tests unitarios en `tests/.../Common/`:
  - **ResultTests:** construcción, factories, inmutabilidad, pattern
    matching, igualdad, no-construcción-directa.
  - **PagedResultTests:** construcción, paginación correcta,
    `TotalPages` derivado, igualdad, edge cases (lista vacía,
    `PageSize` inválido).

### 4.2 Fuera de alcance

- **Implementaciones reales de `Result<T>` o `PagedResult<T>`** en
  use cases. F-005 define los tipos. Los use cases que los usen
  vienen después.
- **`Result` no genérico** (sin `<T>`). El spec define `Result<T>`.
  Para use cases que no retornan valor, se usa `Result<object>` o
  `Result<Unit>` (tipo que se introducirá si surge la necesidad).
  Por ahora, `Result` con `object?` cubre todos los casos.
- **Excepciones específicas** de dominio. Los use cases pueden
  lanzar excepciones para errores irrecuperables (ej. fallos de
  I/O). `Result` es para errores esperados de negocio, no para
  excepciones de infraestructura.
- **Validación** (fail-fast en construcción). `Result<T>` no
  valida nada internamente; la invariante se mantiene por el
  contrato del constructor privado + factories.
- **Serialización** a JSON. La serialización se cubre cuando se
  introduzca la capa de transporte (no es parte de F-005).
- **Internacionalización** de mensajes. Los mensajes son
  literales en inglés; la i18n se cubre en F-012.

---

## 5. Casos de uso

### CU-1 — Use case retorna éxito
- **Actor:** use case de la capa Application (ej.
  `CreateExpenseUseCase`).
- **Trigger:** el use case completa la operación exitosamente.
  `return Result.Success(expense);`
- **Precondición:** la operación no lanzó excepciones y los
  invariantes se cumplen.
- **Postcondición:** el caller recibe un `Result` con
  `IsSuccess = true`, `Value = expense`, `Error = null`.

### CU-2 — Use case retorna fallo de validación
- **Actor:** use case de la capa Application.
- **Trigger:** el use case detecta que un campo no cumple los
  invariantes. `return Result.Failure<Expense>("ValidationError",
  "Amount must be non-negative.");`
- **Precondición:** el error es **esperado** (no es una excepción
  irrecuperable).
- **Postcondición:** el caller recibe un `Result` con
  `IsSuccess = false`, `Value = null`, `Error = { Code =
  "ValidationError", Message = "..." }`.

### CU-3 — Use case retorna fallo con detalles por campo
- **Actor:** use case de la capa Application.
- **Trigger:** el use case agrega múltiples errores (uno por campo
  inválido). `return Result.Failure<Expense>("ValidationError",
  "Some fields are invalid.",
  new Dictionary<string, string[]>
  {
      ["Name"] = new[] { "Name is required" },
      ["Amount"] = new[] { "Amount must be non-negative",
                            "Amount must be finite" }
  });`
- **Precondición:** múltiples campos inválidos, cada uno con su(s)
  mensaje(s).
- **Postcondición:** el `Error` lleva un `Details` con clave=campo
  y valor=array de mensajes. La UI puede iterar y mostrar
  errores por campo.

### CU-4 — Caller maneja el resultado con pattern matching
- **Actor:** caller del use case (ej. un endpoint, un ViewModel).
- **Trigger:** el caller recibe un `Result` y decide qué hacer
  según `IsSuccess`.
- **Precondición:** el use case retornó un `Result<T>`.
- **Postcondición:** el caller maneja éxito o fallo según el caso.
  ```csharp
  return result switch
  {
      { IsSuccess: true, Value: var v } => HandleSuccess(v),
      { IsSuccess: false, Error: var e } => HandleError(e),
  };
  ```

### CU-5 — Query retorna página de resultados
- **Actor:** query de la capa Application (ej. `GetExpensesQuery`).
- **Trigger:** la query necesita devolver una página de gastos.
  `return new PagedResult<Expense>(items, totalCount, page,
  pageSize);`
- **Precondición:** la query ya cargó los items de la página
  actual y conoce el total.
- **Postcondición:** el caller recibe un `PagedResult<T>` con
  los items de la página, el total, el número de página, y el
  tamaño de página. El caller puede calcular el total de páginas
  con `result.TotalPages`.

### CU-6 — Caller navega a la siguiente página
- **Actor:** caller del use case.
- **Trigger:** el usuario hace clic en "siguiente página". El
  caller verifica que hay más páginas. `if (currentPage <
  pagedResult.TotalPages) { /* cargar siguiente */ }`
- **Precondición:** el caller tiene un `PagedResult<T>` previo.
- **Postcondición:** el caller sabe si puede avanzar.

---

## 6. Requisitos funcionales

### RF-1 — `Error` record
- `public record Error(string Code, string Message,
  IReadOnlyDictionary<string, string[]>? Details = null)` en
  `Application/Common/Result.cs`.
- `Code` (string): código legible y único del error (ej.
  `"ValidationError"`, `"NotFound"`, `"Unauthorized"`). No se
  valida en construcción; es responsabilidad del caller usar
  códigos consistentes.
- `Message` (string): mensaje para el usuario. En inglés (RNF-4).
- `Details` (IReadOnlyDictionary<string, string[]>?): detalles
  por campo. Clave = nombre del campo, valor = array de mensajes
  para ese campo. Default `null` (sin detalles).
- Equality por valor (la provee `record`).

### RF-2 — `Result` record (non-generic) con constructor privado
- `public sealed record Result` (non-generic) en
  `Application/Common/Result.cs`.
- **Sin primary constructor público.** Esto fuerza que la única
  forma de construir un `Result` sea a través de las factories
  estáticas. Se logra con el form explícito:
  ```csharp
  public sealed record Result
  {
      public bool IsSuccess { get; init; }
      public object? Value { get; init; }
      public Error? Error { get; init; }
      private Result(bool isSuccess, object? value, Error? error) { ... }
      // factories...
  }
  ```
- Tres propiedades init-only: `IsSuccess`, `Value`
  (`object?`, nullable), `Error` (nullable).
- Constructor privado con la signatura `(bool, object?, Error?)`.
  Solo accesible desde las factories dentro de la clase.
- Equality por valor (la provee `record`).
- **Trade-off vs `Result<T>`:** el caller hace cast del
  `Value`: `var x = (MiTipo)result.Value;`. Es el mismo patrón
  que `Task<object>` o `Lazy<object>`. El spec original
  proponía `Result<T>`; la decisión de cambiar a `Result` con
  `Value: object?` se documenta en §16 (decisiones de diseño).

### RF-3 — Factories de `Result`
- `public static Result Success(object value)` — crea
  `IsSuccess = true, Value = value, Error = null`.
- `public static Result Failure(Error error)` — crea
  `IsSuccess = false, Value = null, Error = error`. El
  `Value` siempre es `null` para fallos (el caller solo debe
  leerlo si `IsSuccess = true`).
- `public static Result Failure(string code, string message)` —
  overload de conveniencia que construye el `Error` con
  `Details = null` y delega a `Failure(Error)`.
- `public static Result Failure(string code, string message,
  IReadOnlyDictionary<string, string[]> details)` — overload con
  detalles, construye el `Error` con los detalles dados.

### RF-4 — Garantía de uso idiomático
- El **constructor privado** garantiza que no se puede construir
  un `Result` con combinación inválida (`IsSuccess = true` +
  `Error != null` o `IsSuccess = false` + `Value != null`). Solo
  las factories, que controlan la combinación, pueden
  construir.
- Esto cumple el criterio "no se debería poder construir un
  `Result` con `IsSuccess=true` y `Error!=null` o `Value=null`"
  de la spec.

### RF-5 — `PagedResult<T>` record
- `public sealed record PagedResult<T>` en
  `Application/Common/PagedResult.cs`.
- Cuatro propiedades init-only:
  - `IReadOnlyList<T> Items` — la página actual.
  - `int Total` — total de items en todas las páginas.
  - `int Page` — número de página actual (1-based, convención
    usada en la UI).
  - `int PageSize` — items por página.
- Una propiedad calculada:
  - `int TotalPages => PageSize == 0 ? 0 :
    (Total + PageSize - 1) / PageSize` — total de páginas
    (ceiling division). Si `PageSize = 0`, retorna 0 (evita
    DivideByZeroException). Esta es una validación defensiva;
    PageSize = 0 es un bug del caller, no un caso de uso.
- Primary constructor público: `public PagedResult(
  IReadOnlyList<T> Items, int Total, int Page, int PageSize)`. Los
  setters init-only garantizan inmutabilidad.
- Equality por valor.

### RF-6 — Tests de `Result` y `PagedResult<T>`
- Tests en `tests/.../Common/`:
- **ResultTests:**
  - `Result_Success_HasIsSuccessTrueAndValue` — `Result.Success(42)`
    tiene `IsSuccess = true`, `Value = 42`, `Error = null`.
  - `Result_Success_AcceptsNullValue` — `Result.Success(null!)`
    tiene `IsSuccess = true`, `Value = null`, `Error = null`
    (caso válido para "operación exitosa sin valor de retorno").
  - `Result_FailureWithError_HasIsSuccessFalseAndError` —
    `Result.Failure(new Error("X", "y"))` tiene `IsSuccess = false`,
    `Value = null`, `Error = { Code = "X", ... }`.
  - `Result_FailureWithCodeAndMessage_CreatesErrorCorrectly` —
    overload de conveniencia.
  - `Result_FailureWithDetails_StoresDetailsCorrectly` — overload
    con detalles; verifica que `Details` se preserva.
  - `Result_IsImmutable_PropertiesAreReadOnlyAtRuntime` — sanity
    check (los tests que pasan ya verifican inmutabilidad
    indirectamente).
  - `Result_TwoSuccessesWithSameValue_AreEqual` — record equality.
  - `Result_TwoFailuresWithSameError_AreEqual` — record equality.
  - `Result_SuccessAndFailure_AreNotEqual` — record inequality.
  - `Result_PatternMatchingOnIsSuccess_DispatchesCorrectly` —
    test que usa `switch` con pattern matching.
- **PagedResultTests:**
  - `PagedResult_Constructor_StoresAllProperties` — verifica las
    cuatro propiedades.
  - `PagedResult_TotalPages_ComputesCorrectly` — `Total = 100,
    PageSize = 10 → TotalPages = 10`; casos límite.
  - `PagedResult_TotalPages_WithZeroItems_IsZero` — `Total = 0,
    PageSize = 10 → TotalPages = 0`.
  - `PagedResult_TotalPages_WithZeroPageSize_IsZero` — `Total = 100,
    PageSize = 0 → TotalPages = 0` (defensive, evita
    DivideByZeroException).
  - `PagedResult_TwoSameResults_AreEqual` — record equality.
  - `PagedResult_DifferentPageNumbers_AreNotEqual` — record
    inequality.
  - `PagedResult_ItemsIsReadOnlyList` — `Items` es
    `IReadOnlyList<T>` (no `IList`).
  - `PagedResult_EmptyList_HasZeroItems` — edge case con lista
    vacía.

---

## 7. Requisitos no funcionales

### RNF-1 — Pureza de Application
- `Result`, `Error`, `PagedResult<T>` solo dependen de BCL
  (`System`, `System.Collections.Generic`).
- Esto se valida con NetArchTest en F-007: `Application` no debe
  tener `PackageReference`s.

### RNF-2 — Inmutabilidad
- `Result`, `Error`, `PagedResult<T>` son `record` con
  propiedades `init`-only. **Inmutables por construcción.**
- `Items` en `PagedResult<T>` es `IReadOnlyList<T>`, no `IList<T>`.
  El caller no puede modificar la lista de items después de la
  creación.

### RNF-3 — Thread-safe
- Inmutabilidad ⇒ thread-safe. No se necesitan locks, no hay
  estado mutable.

### RNF-4 — Mensajes de error en inglés
- Los `Message` de los `Error` están en inglés (convención de
  .NET). La UI los traduce al español si es necesario (F-012).
- Los `Code` de los `Error` son identificadores técnicos en
  PascalCase / kebab-case (ej. `"ValidationError"`,
  `"NotFound"`). No son traducibles.

### RNF-5 — Cobertura de tests >= 95%
- Cobertura manual sin `coverlet.collector`. Cada rama (cada
  factory, cada overload, cada edge case) tiene al menos un
  test.
- Cobertura esperada: 100% (los tipos son pequeños y todas las
  branches son cubiertas por los tests de RF-6).

---

## 8. Reglas de negocio

### RN-1 — El constructor de `Result<T>` es privado
- El **único** camino para crear un `Result<T>` es a través de las
  factories estáticas (`Success`, `Failure`). Esto es por diseño
  y se logra con un constructor privado y propiedades `init`-only.
- La spec test "no se debería poder construir un `Result` con
  `IsSuccess=true` y `Error!=null` o `Value=null`" se cumple
  por construcción (no hay forma de llamar al constructor desde
  fuera de la clase).

### RN-2 — `Failure` con tipos de valor
- `Result<int>.Failure(...)` produce un `Result<int>` con
  `IsSuccess = false, Value = 0` (default de `int`).
- `Result<string>.Failure(...)` produce un `Result<string>` con
  `IsSuccess = false, Value = null` (default de `string`).
- Esto es por diseño (no se fuerza `Value = null` para tipos de
  valor). El caller debe verificar `IsSuccess` antes de usar
  `Value`:
  ```csharp
  if (result.IsSuccess)
  {
      // result.Value es seguro aquí
  }
  ```
- Pattern matching es el patrón idiomático (ver CU-4):
  ```csharp
  return result switch
  {
      { IsSuccess: true, Value: var v } => HandleSuccess(v),
      { IsSuccess: false } => HandleError(),
      _ => throw new InvalidOperationException("...")
  };
  ```

### RN-3 — `PagedResult.TotalPages` usa ceiling division
- `TotalPages = (Total + PageSize - 1) / PageSize` (entera).
- Ejemplo: `Total = 95, PageSize = 10 → TotalPages = 10` (no 9.5).
- Edge case: `PageSize = 0 → TotalPages = 0` (defensivo, evita
  `DivideByZeroException`).

### RN-4 — `PagedResult.Page` es 1-based
- La convención usada en la UI y en la mayoría de APIs REST es
  1-based (página 1, 2, 3, ...). El spec sigue esta convención.
- `Page` puede ser cualquier entero (no se valida en
  construcción). El caller debe pasar valores válidos.

### RN-5 — `Result<T>` y `PagedResult<T>` son inmutables
- Ninguna propiedad se puede modificar después de la creación
  (`init`-only). No hay método `With...` (a diferencia de F-002
  donde `Money` es un `record` con `init` y `with` está
  disponible). Para `Result<T>`, el caller siempre construye
  uno nuevo si necesita un valor diferente.

---

## 9. Modelo de dominio

```
Application/Common/

  record Error
    + Code: string
    + Message: string
    + Details: IReadOnlyDictionary<string, string[]>? = null

  sealed record Result<T>               (constructor privado)
    + IsSuccess: bool
    + Value: T?
    + Error: Error?
    ──────────────────────────
    + static Success(value: T) : Result<T>
    + static Failure(error: Error) : Result<T>
    + static Failure(code: string, message: string) : Result<T>
    + static Failure(code: string, message: string,
                    details: IReadOnlyDictionary<string, string[]>) : Result<T>

  sealed record PagedResult<T>
    + Items: IReadOnlyList<T>
    + Total: int
    + Page: int
    + PageSize: int
    + TotalPages: int         (calculada)
```

- `Result<T>` y `PagedResult<T>` son **sealed records** (no
  herencia).
- `Error` es **record** (no sealed) — los use cases pueden
  definir `Error` como base para tipos más específicos (ej.
  `ValidationError : Error`). Esto se cubre en features futuras.
- `Items` en `PagedResult<T>` es `IReadOnlyList<T>` (no `IList<T>`)
  para garantizar inmutabilidad.

---

## 10. Validaciones

`Result<T>`, `Error`, `PagedResult<T>` **no validan nada** en su
construcción. La invariante la garantiza el constructor privado +
factories de `Result<T>`, y el tipo `IReadOnlyList<T>` para `Items`
de `PagedResult<T>`.

| # | Validación | Cuándo se aplica | Comportamiento esperado |
| --- | --- | --- | --- |
| V-1 | `Result<T>` con `IsSuccess = true` y `Error != null` | Nunca (constructor privado lo impide) | Imposible crear |
| V-2 | `Result<T>` con `IsSuccess = false` y `Value` no-default | Nunca (factory lo fuerza) | Factory usa `default(T)` |
| V-3 | `PagedResult.Total < 0` | No se valida | El caller debe pasar valores válidos |
| V-4 | `PagedResult.PageSize <= 0` | No se valida; `TotalPages` retorna 0 | Defensive: evita DivideByZeroException |

---

## 11. Casos borde

### EC-1 — `Result.Success(null!)` con `T = string`
- `IsSuccess = true`, `Value = null`, `Error = null`. Esto es
  válido (un use case "create" que no retorna el objeto creado).
- En runtime, el caller debe verificar `IsSuccess` antes de usar
  `Value` (pattern matching).

### EC-2 — `Result.Failure<Unit>()` (tipo de valor)
- `IsSuccess = false`, `Value = 0` (default de `Unit` si
  existiera). En la práctica, se usa `Result<object>` o
  `Result<SomeReferenceType>`.
- No hay `Result` no genérico en F-005. Si surge la necesidad,
  se añadirá en una feature futura.

### EC-3 — `PagedResult` con `Total = 0, PageSize = 10, Page = 1`
- `TotalPages = 0` (ceiling division: 0 / 10 = 0).
- Caso válido: la query no encontró resultados, retorna página 1
  vacía.

### EC-4 — `PagedResult` con `Total = 95, PageSize = 10`
- `TotalPages = 10` (ceiling: (95 + 9) / 10 = 10).
- Caso típico: la última página tiene 5 items, no 10.

### EC-5 — `PagedResult` con `PageSize = 0`
- `TotalPages = 0` (defensive: evita `DivideByZeroException`).
- Caso degenerado: `PageSize = 0` es un bug del caller, no un
  caso de uso válido. El caller debería validar antes de
  construir.

### EC-6 — `Items` con lista vacía
- `Items.Count == 0`. La página está vacía.
- Caso válido: la query retornó 0 items, la página 1 está vacía
  pero el total es 0.

### EC-7 — `Failure` con `Details = null`
- `Error.Details` es `null`. Esto es válido: el error no tiene
  detalles por campo, solo `Code` y `Message`.

### EC-8 — `Failure` con `Details` con array vacío
- `Error.Details["Name"]` es `string[] { }` (array vacío, no
  `null`). El caller debe verificar `result.Length > 0` antes de
  iterar.

---

## 12. Dependencias

### 12.1 Prerrequisitos de implementación

- **F-001 completada**: la solución, los proyectos y la
  configuración global están listos. El proyecto
  `ControlDeFinanzasIA.Application` existe.
- **F-002 completada** (recomendado): establece el patrón de
  value object en `Domain`. F-005 usa el mismo patrón pero en
  `Application`.
- **F-003 completada** (recomendado): misma razón que F-002.
- **F-004 completada** (recomendado): `IClock` e `IPathResolver`
  se usan en use cases futuros que retornan `Result<T>`. No es
  bloqueante para F-005.

### 12.2 Dependencias con otras features

| Feature | Relación |
| --- | --- |
| F-002 (Money) | Independiente. |
| F-003 (DateRange) | Independiente. |
| F-004 (IClock, IPathResolver) | Independiente. F-005 define tipos; F-004 define interfaces. |
| F-006 (Interfaces de repositorio) | Consumidora. Los repositorios futuros retornan `Result<T>` o `PagedResult<T>`. |
| F-007 (NetArchTest) | Verificación. F-007 valida que `Application` no tiene `PackageReference`s. |
| F-008 (Composition root) | Consumidora. F-008 no afecta a F-005 directamente. |

### 12.3 Bloqueos

- **Ninguno.** F-005 puede implementarse inmediatamente.

---

## 13. Fuera de alcance (detallado)

- **Implementaciones reales de `Result<T>` o `PagedResult<T>`** en
  use cases. F-005 define los tipos. Los use cases que los usen
  vienen después.
- **`Result` no genérico** (sin `<T>`). El spec define `Result<T>`.
- **Excepciones específicas de dominio.** `Result<T>` es para
  errores esperados, no para excepciones irrecuperables.
- **Validación en construcción.** `Result<T>` y `PagedResult<T>` no
  validan; el constructor privado de `Result<T>` garantiza la
  invariante, y `IReadOnlyList<T>` garantiza inmutabilidad de
  `Items`.
- **Serialización** a JSON. No es parte de F-005.
- **Internacionalización** de mensajes. Mensajes en inglés (RNF-4);
  i18n se cubre en F-012.
- **`Result<T>.Bind` / `Result<T>.Map` / LINQ-like extensions.**
  Si se necesitan en el futuro, se añaden como métodos de
  extensión. F-005 solo define el tipo base.
- **Manejo de `Result<T>` con `async`** (`.AsTask()`, etc.). El
  spec no lo necesita; los use cases futuros pueden definir sus
  propios helpers de async si los necesitan.

---

## 14. Criterios de aceptación

La feature se considera completada cuando **todos** los siguientes
puntos se cumplen.

### AC-1 — Tipos compilan sin warnings
- [ ] `Result.cs` existe en
      `src/ControlDeFinanzasIA.Application/Common/Result.cs`,
      contiene `record Error` y `sealed record Result<T>`.
- [ ] `PagedResult.cs` existe en
      `src/ControlDeFinanzasIA.Application/Common/PagedResult.cs`.
- [ ] `dotnet build`: 0 warnings, 0 errors.
- [ ] `Application` sigue sin `PackageReference`s.

### AC-2 — `Result<T>` factory tests pasan
- [ ] `Result_Success_HasIsSuccessTrueAndValue` pasa.
- [ ] `Result_Success_AcceptsNullValue` pasa (con `T = string`).
- [ ] `Result_FailureWithError_HasIsSuccessFalseAndError` pasa.
- [ ] `Result_FailureWithCodeAndMessage_CreatesErrorCorrectly` pasa.
- [ ] `Result_FailureWithDetails_StoresDetailsCorrectly` pasa.
- [ ] `Result_PublicConstructorIsNotAccessible` se cubre
      indirectamente (los tests anteriores usan solo factories).

### AC-3 — `Result<T>` equality e inmutability
- [ ] `Result_TwoSuccessesWithSameValue_AreEqual` pasa.
- [ ] `Result_TwoFailuresWithSameError_AreEqual` pasa.
- [ ] `Result_SuccessAndFailure_AreNotEqual` pasa.
- [ ] `Result_IsImmutable_CannotChangeAfterCreation` pasa
      (verifica que las propiedades son `init`-only).

### AC-4 — `Result<T>` pattern matching
- [ ] `Result_PatternMatchingOnIsSuccess_DispatchesCorrectly` pasa.
- [ ] El test usa `switch` con pattern matching sobre `IsSuccess`
      y verifica que el branch correcto se ejecuta.

### AC-5 — `PagedResult<T>` tests pasan
- [ ] `PagedResult_Constructor_StoresAllProperties` pasa.
- [ ] `PagedResult_TotalPages_ComputesCorrectly` pasa.
- [ ] `PagedResult_TotalPages_WithZeroItems_IsZero` pasa.
- [ ] `PagedResult_TotalPages_WithZeroPageSize_IsZero` pasa.
- [ ] `PagedResult_TwoSameResults_AreEqual` pasa.
- [ ] `PagedResult_DifferentPageNumbers_AreNotEqual` pasa.
- [ ] `PagedResult_ItemsIsReadOnlyList` pasa.
- [ ] `PagedResult_EmptyList_HasZeroItems` pasa.

### AC-6 — Cobertura y calidad
- [ ] `dotnet format style --verify-no-changes`: exit 0.
- [ ] `dotnet test --project tests/.../Tests.Unit` pasa todos los
      tests nuevos.
- [ ] Cobertura de `Result.cs` y `PagedResult.cs`: ~100% (manual,
      sin `coverlet.collector`).
- [ ] `Application` sigue sin `PackageReference`s.
- [ ] `Domain` no referencia `Result<T>` ni `PagedResult<T>`
      (verificable por grep o por NetArchTest en F-007).

### AC-7 — Convenciones del repo
- [ ] Tipos siguen el `.editorconfig` (file-scoped namespace, sin
      `using` redundantes, naming PascalCase).
- [ ] XML doc en cada miembro público (clase + propiedades +
      métodos de factory).
- [ ] No hay archivos `Result1.cs`, `PagedResult1.cs`, ni
      placeholders similares.

---

## 15. Estructura de archivos esperada

```
src/
└── ControlDeFinanzasIA.Application/
    ├── Abstractions/                      (F-004, ya existe)
    │   ├── IClock.cs
    │   └── IPathResolver.cs
    ├── Common/                            ← NUEVO
    │   ├── Result.cs                      ← nuevo (F-005)
    │   │     (contiene `Error` y `Result<T>`)
    │   └── PagedResult.cs                 ← nuevo (F-005)
    └── ...

tests/
└── ControlDeFinanzasIA.Tests.Unit/
    ├── ValueObjects/                      (F-002, F-003, ya existe)
    ├── TestDoubles/                       (F-004, ya existe)
    ├── Common/                            ← NUEVO
    │   ├── ResultTests.cs                 ← nuevo (F-005)
    │   └── PagedResultTests.cs            ← nuevo (F-005)
    └── ...
```

---

## 16. Decisiones de diseño explícitas

### 16.1 — Constructor privado en `Result<T>`

El spec de F-005 dice "no se debería poder construir un `Result`
con `IsSuccess=true` y `Error!=null` o `Value=null`". Para
hacerlo **verificable por construcción**, el constructor de
`Result<T>` es privado. Solo las factories pueden construir un
`Result<T>`. La forma explícita de `record` con propiedades
`init`-only y constructor privado es la forma idiomática de
hacerlo en C# 9+.

**Alternativa considerada:** dejar el primary constructor público
y validar las invariantes con guards en las factories. Pero esto
no impide que el caller haga `new Result<int>(true, 42,
new Error(...))` directamente, que viola la invariante. El
constructor privado es la única solución real.

### 16.2 — `PagedResult.Page` 1-based

Convención de la UI y de la mayoría de APIs REST (HTTP, GraphQL,
JSON:API). El spec sigue esta convención. Si en el futuro se
necesita 0-based, se añade un overload con un parámetro
`zeroBased: bool = false` o se cambia el nombre.

### 16.3 — `TotalPages` como propiedad calculada (no método)

`TotalPages` no se almacena; se calcula de `Total` y `PageSize`.
Esto evita inconsistencia (que `TotalPages` no coincida con
`Total / PageSize`). El cálculo es O(1) (una división entera).

### 16.4 — `Items` como `IReadOnlyList<T>`, no `IList<T>`

`IReadOnlyList<T>` garantiza inmutabilidad estructural: el caller
no puede agregar ni remover items de la lista. `IList<T>`
permitiría mutación. La elección es por seguridad.

### 16.5 — Tests en el mismo proyecto de tests, no en un proyecto separado

Los tests de `Result<T>` y `PagedResult<T>` viven en el proyecto
`Tests.Unit`, no en un proyecto separado `Tests.Application`. Esto
es consistente con la convención de F-002/F-003/F-004: los tests
de un tipo van junto al tipo en el mismo proyecto de tests.

---

## 17. Riesgos y mitigaciones

| Riesgo | Probabilidad | Impacto | Mitigación |
| --- | --- | --- | --- |
| Caller olvida verificar `IsSuccess` antes de usar `Value` | Media | Alto | Pattern matching es la solución idiomática; los tests muestran cómo. El compilador no puede obligar al caller a verificar (el tipo no tiene discriminated union). |
| `Failure` con tipo de valor produce `Value = default(T)` que es confuso | Baja | Bajo | Documentado en RN-2. El caller debe verificar `IsSuccess` antes de usar `Value`. |
| `PagedResult.PageSize = 0` causa `DivideByZeroException` | Baja | Alto | Defensive: `TotalPages` retorna 0 en ese caso. El `Items` y otras propiedades funcionan normalmente. |
| `Result<T>` con `Value = null` (tipo de referencia) se confunde con "fallo" | Media | Bajo | El test `Result_Success_AcceptsNullValue` documenta que es válido. El caller usa `IsSuccess` para discriminar. |
| Los fakes/extendidos de `Result<T>` (LINQ-like) no existen | Baja | Bajo | Se pueden añadir en features futuras como métodos de extensión. F-005 solo define el tipo base. |
| El spec de F-005 cambió el namespace de `Error` a algo distinto | Baja | Bajo | El spec mantiene `Error` en el mismo archivo que `Result` por cohesión. Si en el futuro se quiere separar, se mueve. |

---

## 18. Referencias

- `docs/vision.md` — describe el producto y los casos de uso que
  requieren manejo de errores de negocio.
- `docs/architecture.md` §3 (Capas) y §7 (Composition root) —
  define el patrón de use cases que retornan `Result<T>`.
- `docs/roadmap.md` § Fase 1, F-005 dentro del desglose.
- `spec/constitution/tech-stack.md` — convenciones de código.
- `spec/phases/phase-01/features.md` — F-005 dentro del
  desglose de features de la fase 1.
- `spec/features/phase-01/F-001-skeleton-solution.done.md` —
  provee la base (proyecto `Application` existe).
- `spec/features/phase-01/F-002-money-value-object.done.md` y
  `F-003-datarange-value-object.done.md` — siguen el mismo
  patrón de value object; F-005 usa el mismo patrón pero con
  factories en vez de un solo factory.
- `spec/features/phase-01/F-004-Abstracciones-clock-pathresolver.done.md`
  — provee la convención de ubicación (`Application/Abstractions/`
  y `Tests.Unit/TestDoubles/`). F-005 usa `Application/Common/`
  y `Tests.Unit/Common/` (carpetas paralelas).
- `spec/README.md` — convención completa del directorio `spec/`.
- **Documentación de C# records:**
  [learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/record](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/record)
- **Documentación de `IReadOnlyList<T>`:**
  [learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

