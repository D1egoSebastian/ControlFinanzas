# Tareas — F-003: DateRange Value Object

> **Spec:** `spec/features/phase-01/F-003-datarange-value-object.md`
> **Fuentes de verdad:** la spec, `docs/architecture.md`,
> `docs/vision.md`, `docs/roadmap.md`, `spec/constitution/*`,
> `spec/features/phase-01/F-001-skeleton-solution.done.md` (F-001
> completada, provee la base de proyectos y configuración),
> `spec/features/phase-01/F-002-money-value-object.done.md` (F-002
> completada, provee el patrón de value object que F-003 sigue).
>
> Cada tarea es implementable en menos de 2 horas. El proyecto
> debe seguir compilando al finalizar cada una. La numeración
> (T-001...) es local a esta feature.

---

## Resumen de tareas

| ID | Tarea | Estimación | Depende de |
| --- | --- | --- | --- |
| T-001 | Crear el value object `DateRange` con factory y validación | M | — |
| T-002 | Añadir la operación `Contains` + tests (semántica inclusiva) | S | T-001 |
| T-003 | Añadir la operación `Overlaps` + tests (con null check) | S | T-001 |
| T-004 | Añadir la operación `DurationInDays` + tests (conteo inclusivo) | S | T-001 |
| T-005 | Añadir la operación `Intersect` + tests (con null check) | S | T-001 |
| T-006 | Verificación final: tests, format check, build, cobertura, cierre | M | todas |

**Total estimado:** 1 jornada o menos. T-001 es la más larga
(la primera tarea "real" de código, introduce el patrón de value
object para fechas); el resto son operaciones independientes y
tests asociados.

---

## T-001 — Crear el value object `DateRange` con factory y validación

**Objetivo**
Establecer `DateRange` como value object inmutable con la API
mínima: propiedades `Start` y `End` (solo lectura), factory
estático `Create` que valida `Start <= End`, e igualdad por valor.
Esta tarea es el cimiento; las operaciones (`Contains`, `Overlaps`,
`DurationInDays`, `Intersect`) llegan en T-002 a T-005.

**Descripción**
Crear el archivo
`src/ControlDeFinanzasIA.Domain/ValueObjects/DateRange.cs` con:

- `public sealed record DateRange` con propiedades públicas de solo
  lectura: `Start` (`DateOnly`) y `End` (`DateOnly`).
- Constructor **privado** (NUNCA público). El factory es el único
  camino de construcción.
- `public static DateRange Create(DateOnly start, DateOnly end)`
  con validación:
  - Si `start > end`, lanza `ArgumentException` con mensaje:
    `"Start must be less than or equal to End. Received: start={start:o}, end={end:o}."`
  - Si pasa, devuelve `new DateRange(start, end)` (constructor
    privado invocado aquí; la validación previa garantiza que el
    resultado es válido).
- Equality por valor (la provee `record` automáticamente).
- Documentar con comentarios XML públicos la intención de cada
  miembro (regla de `architecture.md` §6).

Crear el archivo de tests
`tests/ControlDeFinanzasIA.Tests.Unit/ValueObjects/DateRangeTests.cs`
con tests que cubran AC-1 (factory) y AC-6 (equality):

- **AC-1:**
  - `Create_WithStartLessThanEnd_ReturnsValidRange`
  - `Create_WithStartEqualToEnd_ReturnsSingleDayRange`
  - `Create_WithStartGreaterThanEnd_ThrowsArgumentException` (con
    mensaje que contiene "Start" y "End")
- **AC-6:**
  - `Equality_SameStartSameEnd_AreEqual`
  - `Equality_DifferentStartSameEnd_AreNotEqual`
  - `Equality_SameStartDifferentEnd_AreNotEqual`
  - `GetHashCode_SameStartSameEnd_ReturnsSameHash`

> **No es necesario añadir tests para `DateOnly` en sí mismo** (es
> un tipo BCL probado por Microsoft). `DateRange` se valida
> **implícitamente** a través de los tests de las operaciones
> que usan `DateRange` con fechas concretas.

**Por qué se hace primero:** `Contains`, `Overlaps`, `DurationInDays`
e `Intersect` referencian `Start` y `End` y la invariante. Si
`DateRange` no existe con el factory, ninguna operación compila.
Es un prerequisito absoluto de T-002 a T-005.

**Dependencias**
- F-001 (proyecto `Domain` existe con la configuración global).
- F-002 (patrón de value object establecido: `Money` usa el
  mismo `record + factory + readonly properties`).

**Archivos / proyectos afectados**
- `src/ControlDeFinanzasIA.Domain/ValueObjects/DateRange.cs`
  (nuevo).
- `tests/ControlDeFinanzasIA.Tests.Unit/ValueObjects/DateRangeTests.cs`
  (nuevo).

**Criterios de finalización**
- `DateRange.cs` existe en `ValueObjects/`.
- `DateRange` es `public sealed record`.
- Constructor privado (verificable intentando `new DateRange(...)`
  desde un test; debe fallar compilación).
- `DateRange.Create(start, end)` con `start < end` retorna una
  instancia válida.
- `DateRange.Create(start, start)` con `start == end` retorna
  instancia válida (un solo día).
- `DateRange.Create(end, start)` con `start > end` lanza
  `ArgumentException` con mensaje que contiene "Start" y "End".
- `DateRange.Create(5, 10) == DateRange.Create(5, 10)` (record
  equality).
- `DateRange.Create(5, 10) != DateRange.Create(5, 11)`.
- `DateRange.Create(5, 10) != DateRange.Create(6, 10)`.
- `dotnet build`: 0 warnings, 0 errors.
- `dotnet test --project tests/ControlDeFinanzasIA.Tests.Unit`
  pasa todos los tests nuevos.
- `dotnet format style --verify-no-changes`: pasa.
- `Domain` sigue sin `PackageReference`s.

---

## T-002 — Añadir la operación `Contains` con semántica inclusiva

**Objetivo**
Implementar `Contains(DateOnly)` que indica si una fecha está
dentro del rango, con **semántica inclusiva en ambos extremos**.

**Descripción**
Añadir a `src/ControlDeFinanzasIA.Domain/ValueObjects/DateRange.cs`:

- `public bool Contains(DateOnly date)`:
  - Retorna `true` si `date >= Start && date <= End`.
  - No valida nada (la invariante `Start <= End` ya está garantizada
    por el factory).

Añadir a `tests/.../DateRangeTests.cs` los tests para AC-2
(Contains):

- `Contains_DateInsideRange_ReturnsTrue`
- `Contains_DateEqualToStart_ReturnsTrue` (inclusivo en Start)
- `Contains_DateEqualToEnd_ReturnsTrue` (inclusivo en End)
- `Contains_DateBeforeStart_ReturnsFalse`
- `Contains_DateAfterEnd_ReturnsFalse`
- `Contains_SingleDayRange_ContainsOnlyThatDay` (Start == End, solo
  ese día retorna `true`)

**Dependencias**
- T-001 (`DateRange` debe existir con factory).

**Archivos / proyectos afectados**
- `src/ControlDeFinanzasIA.Domain/ValueObjects/DateRange.cs`
  (modificado: añadir `Contains`).
- `tests/ControlDeFinanzasIA.Tests.Unit/ValueObjects/DateRangeTests.cs`
  (modificado).

**Criterios de finalización**
- `DateRange.Contains(DateOnly)` existe y es `public`.
- Retorna `true` para fecha en `Start`, fecha en `End`, fecha
  intermedia.
- Retorna `false` para fecha anterior a `Start` y posterior a
  `End`.
- Para rango de un solo día, solo `Start` (que es igual a `End`)
  retorna `true`.
- Build, tests y format check: limpios.

---

## T-003 — Añadir la operación `Overlaps` con null check

**Objetivo**
Implementar `Overlaps(DateRange)` que detecta si dos rangos
comparten al menos un día, con null check.

**Descripción**
Añadir a `src/ControlDeFinanzasIA.Domain/ValueObjects/DateRange.cs`:

- `public bool Overlaps(DateRange other)`:
  - Si `other` es `null`, lanza `ArgumentNullException(nameof(other))`.
  - Retorna `this.Start <= other.End && other.Start <= this.End`.
  - Semántica inclusiva: rangos adyacentes (uno termina el día que
    el otro empieza) retornan `true` (comparten ese día).

Añadir a `tests/.../DateRangeTests.cs` los tests para AC-3
(Overlaps):

- `Overlaps_TwoOverlappingRanges_ReturnsTrue`
- `Overlaps_TwoDisjointRanges_ReturnsFalse`
- `Overlaps_AdjacentRanges_ReturnsTrue` (RN-5: comparten el día de
  la adyacencia)
- `Overlaps_OneContainsTheOther_ReturnsTrue`
- `Overlaps_IdenticalRanges_ReturnsTrue`
- `Overlaps_Null_ThrowsArgumentNullException`

**Dependencias**
- T-001.

**Archivos / proyectos afectados**
- `src/ControlDeFinanzasIA.Domain/ValueObjects/DateRange.cs`
  (modificado).
- `tests/ControlDeFinanzasIA.Tests.Unit/ValueObjects/DateRangeTests.cs`
  (modificado).

**Criterios de finalización**
- `DateRange.Overlaps(DateRange)` existe y es `public`.
- Retorna `true` para rangos que comparten al menos un día.
- Retorna `true` para rangos adyacentes (RN-5).
- Retorna `false` para rangos disjuntos.
- Lanza `ArgumentNullException` si `other` es `null`.
- Build, tests y format check: limpios.

---

## T-004 — Añadir la operación `DurationInDays` con conteo inclusivo

**Objetivo**
Implementar `DurationInDays()` que retorna el número de días del
rango, con conteo **inclusivo** (un solo día retorna 1).

**Descripción**
Añadir a `src/ControlDeFinanzasIA.Domain/ValueObjects/DateRange.cs`:

- `public int DurationInDays()`:
  - Retorna `this.End.DayNumber - this.Start.DayNumber + 1`.
  - No valida nada. Se asume la invariante `Start <= End` (el
    factory la garantiza).
  - Para rango de un solo día, retorna 1 (no 0). Consecuencia de
    la fórmula `End.DayNumber - Start.DayNumber + 1` con
    `Start == End`: `0 + 1 = 1`.
  - Documentar en `<remarks>` que la fórmula asume la invariante;
    si la invariante se rompe externamente (por reflexión), el
    resultado puede ser <= 0, pero no se valida porque romper la
    invariante es error de programador, no caso de uso.

Añadir a `tests/.../DateRangeTests.cs` los tests para AC-4
(DurationInDays):

- `DurationInDays_SingleDayRange_ReturnsOne` (Start == End → 1)
- `DurationInDays_TwoDayRange_ReturnsTwo`
- `DurationInDays_FullJanuary_ReturnsThirtyOne` (1-ene al 31-ene → 31)
- `DurationInDays_LongRange_ReturnsLargeNumber` (1 año completo)
- `DurationInDays_DoesNotMutate` (defensa: inmutabilidad)

**Dependencias**
- T-001.

**Archivos / proyectos afectados**
- `src/ControlDeFinanzasIA.Domain/ValueObjects/DateRange.cs`
  (modificado).
- `tests/ControlDeFinanzasIA.Tests.Unit/ValueObjects/DateRangeTests.cs`
  (modificado).

**Criterios de finalización**
- `DateRange.DurationInDays()` existe y es `public`.
- Retorna 1 para rango de un solo día.
- Retorna 2 para rango de 2 días.
- Retorna 31 para enero completo.
- No muta el `DateRange` (test de defensa).
- Build, tests y format check: limpios.

---

## T-005 — Añadir la operación `Intersect` con null check

**Objetivo**
Implementar `Intersect(DateRange)` que retorna el rango
intersección o `null` si no se solapan.

**Descripción**
Añadir a `src/ControlDeFinanzasIA.Domain/ValueObjects/DateRange.cs`:

- `public DateRange? Intersect(DateRange other)`:
  - Si `other` es `null`, lanza `ArgumentNullException(nameof(other))`.
  - Si los rangos no se solapan (`Start > other.End || other.Start >
    End`), retorna `null`.
  - Si se solapan, retorna `Create(Max(Start, other.Start),
    Min(End, other.End))`. La intersección de dos rangos válidos
    con `Start <= End` es siempre un rango válido.

Añadir a `tests/.../DateRangeTests.cs` los tests para AC-5
(Intersect):

- `Intersect_TwoOverlappingRanges_ReturnsIntersection`
  (r1=[1,10], r2=[5,15] → [5,10])
- `Intersect_DisjointRanges_ReturnsNull` (r1=[1,5], r2=[10,15] →
  null)
- `Intersect_OneContainsTheOther_ReturnsInner`
  (r1=[1,31], r2=[10,20] → [10,20])
- `Intersect_IdenticalRanges_ReturnsSameRange` (r1==r2 → mismo
  Start/End, instancia distinta pero igual por valor)
- `Intersect_AdjacentRanges_ReturnsSingleDay` (r1=[1,5], r2=[5,10]
  → [5,5] rango de un día)
- `Intersect_Null_ThrowsArgumentNullException`

**Dependencias**
- T-001.

**Archivos / proyectos afectados**
- `src/ControlDeFinanzasIA.Domain/ValueObjects/DateRange.cs`
  (modificado).
- `tests/ControlDeFinanzasIA.Tests.Unit/ValueObjects/DateRangeTests.cs`
  (modificado).

**Criterios de finalización**
- `DateRange.Intersect(DateRange)` existe y es `public`.
- Retorna `DateRange` con la intersección para rangos solapados.
- Retorna `null` para rangos disjuntos.
- Lanza `ArgumentNullException` si `other` es `null`.
- La intersección de dos rangos válidos es siempre válida (Start
  <= End en el resultado).
- Build, tests y format check: limpios.

---

## T-006 — Verificación final, cobertura y cierre

**Objetivo**
Validar que F-003 cumple los criterios de aceptación de la spec
(sección 12), y dejar el repo en estado de "F-003 terminada".

**Descripción**
Esta tarea **no introduce código nuevo**. Consiste en ejecutar
los pasos de verificación y ejecutar las acciones de cierre.

### Pasos de verificación

1. **Compilación:** `dotnet build -c Debug` y `dotnet build -c
   Release` — ambos con 0 warnings, 0 errors.
2. **Tests:** `dotnet test` — todos los tests pasan.
3. **Format check:** `dotnet format style --verify-no-changes` —
   exit 0.
4. **Cobertura:** verificar que `DateRange.cs` tiene cobertura
   >= 95%. Sin `coverlet.collector` configurado, **verificación
   manual**: contar que los tests cubren todas las branches de
   los métodos públicos. Cobertura esperada: 100% (todas las
   branches tienen tests).
5. **Aislamiento de Domain:** `dotnet list src/.../Domain package`
   debe seguir mostrando "No packages were found for this
   framework." (preparación para F-007).
6. **Layout:** `DateRange.cs` está en
   `src/ControlDeFinanzasIA.Domain/ValueObjects/DateRange.cs`.
   `DateRangeTests.cs` está en
   `tests/.../ValueObjects/DateRangeTests.cs`.

### Pasos de limpieza

7. **Verificar que no hay placeholders que limpiar.** F-002 ya
   eliminó el `F001PlaceholderTests.cs` de `Tests.Unit` en su
   T-007. No hay nada que limpiar para F-003 (los tests reales
   son la realidad de la feature).
8. **Actualizar el desglose de fase:** en
   `spec/phases/phase-01/features.md`, cambiar el estado de F-003
   de "Pendiente" a "✅ COMPLETADA" (mismo formato que F-001 y
   F-002).
9. **Renombrar la spec:** mover
   `spec/features/phase-01/F-003-datarange-value-object.md` a
   `spec/features/phase-01/F-003-datarange-value-object.done.md`
   (convención del proyecto, ver F-001 y F-002).

### Resumen de cobertura manual (criterio AC-8)

Verificar que cada miembro público de `DateRange` está cubierto por
al menos un test:

| Miembro público | Tests |
| --- | --- |
| Constructor (privado, invocado por Create/operaciones) | T-001, T-002, T-003, T-004, T-005 |
| `Start` getter | T-001, T-002, T-003, T-004, T-005 |
| `End` getter | T-001, T-002, T-003, T-004, T-005 |
| `Create(DateOnly, DateOnly)` | T-001 (válido, single-day, Start>End) |
| `Contains(DateOnly)` | T-002 (6 tests) |
| `Overlaps(DateRange)` | T-003 (6 tests) |
| `DurationInDays()` | T-004 (5 tests) |
| `Intersect(DateRange)` | T-005 (6 tests) |
| `Equals`, `GetHashCode` (de record) | T-001 (3 tests) |

**Cobertura esperada: 100% en líneas de `DateRange`.**

**Dependencias**
- T-001 a T-005.

**Archivos / proyectos afectados**
- `spec/phases/phase-01/features.md` (modificado: marcar F-003
  como completada).
- `spec/features/phase-01/F-003-datarange-value-object.md`
  (renombrado a `.done.md`).

**Criterios de finalización**
- `dotnet build` (Debug y Release): 0 warnings, 0 errors.
- `dotnet test`: todos los tests pasan.
- `dotnet format style --verify-no-changes`: exit 0.
- `DateRange.cs` está en la ruta correcta bajo
  `src/ControlDeFinanzasIA.Domain/ValueObjects/`.
- `Domain` sigue sin `PackageReference`s.
- `spec/phases/phase-01/features.md` actualizado con F-003
  marcada como completada.
- Spec renombrada a `.done.md`.
- **Confirmación punto por punto de los 9 grupos de criterios de
  aceptación de la spec (AC-1 a AC-9)**, presentada al cerrar
  la feature.

---

## Orden recomendado y trabajo en paralelo

**Orden secuencial estricto:** T-001 → (T-002, T-003, T-004, T-005 en
cualquier orden) → T-006.

**No se recomienda paralelizar** T-002, T-003, T-004, T-005 entre
sí porque todas modifican el mismo archivo `DateRange.cs`. Con 2
personas se podría paralelizar tests (cada persona su propio archivo
de tests), pero la definición de los métodos en `DateRange.cs`
debe ser secuencial.

**T-001 es la tarea más delicada.** Define el patrón de value
object para fechas. Si se hace bien, T-002 a T-005 son mecánicas. Si
se hace con prisa (p. ej. exponiendo el constructor público para
"acabar rápido"), hay que corregirlo en T-001 antes de seguir.

---

## Riesgos de la fase de tareas

| Riesgo | Probabilidad | Impacto | Mitigación |
| --- | --- | --- | --- |
| Decisión "inclusivo" vs "exclusivo" en Contains/Overlaps | Baja | Medio | La spec documenta inclusivo en ambos extremos. Si en el futuro se necesita exclusivo, se añade un método separado, no se cambia el default. |
| `with` expression saltando validación | Baja | Alto | Propiedades `readonly` (no `init`) bloquean `with`. Misma decisión que F-002 con `Money`. |
| `Intersect` retornando `null` causa `NullReferenceException` en callers | Media | Medio | Documentado en AC-5 y RN-3. Los callers deben usar `?.`, `Match`, o `if`. No se "arregla" retornando un `DateRange` vacío. |
| Confusión entre `DateOnly` y `DateTime` | Baja | Bajo | Usar `DateOnly` consistentemente. Los tests usan `new DateOnly(2026, 1, 1)` para tipado fuerte. |
| Cambio de `DateOnly` a `DateTime` si la spec del proyecto evoluciona | Baja | Alto | `DateOnly` es el tipo correcto (sin zona horaria, sin hora). Si se necesita hora, se añade como tipo separado, no se cambia `DateRange`. |
| Tests descubiertos como flaky por temas de zona horaria | Baja | Bajo | `DateOnly` no tiene zona horaria, así que no hay riesgo. |

---

## Referencias

- `spec/features/phase-01/F-003-datarange-value-object.md` — la
  spec completa de F-003. Este tasks file es su descomposición
  técnica.
- `spec/features/phase-01/F-001-skeleton-solution.done.md` — F-001
  completada, provee la base: proyecto `Domain` existe,
  `Nullable` y `ImplicitUsings` activos, sin `PackageReference`s.
- `spec/features/phase-01/F-002-money-value-object.done.md` — F-002
  completada, provee el patrón de value object que F-003 sigue
  (record + factory + readonly + XML doc + formato de tests).
- `docs/architecture.md` §6 (Dominio: entidades, value objects,
  servicios de dominio puros).
- `docs/vision.md` §4.1 (transacciones) y §4.4 (metas con fecha
  objetivo).
- `docs/roadmap.md` § Fase 1, F-003 dentro del desglose.
- `spec/constitution/tech-stack.md` — convenciones de código y
  reglas de paquetes.
- `spec/phases/phase-01/features.md` — F-003 dentro del
  desglose de features de la fase 1.
- `spec/features/phase-02/F-002-persistence-efcore-sqlite.md` —
  spec consumidora de F-003 (Fase 2 persistirá `DateRange`).
- `spec/README.md` — convención completa del directorio `spec/`.
- **Documentación de `DateOnly`:** [learn.microsoft.com/dotnet/api/system.dateonly](https://learn.microsoft.com/dotnet/api/system.dateonly)
