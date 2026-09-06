# Specification — F-003: DateRange Value Object

> **Fase:** 1 — Esqueleto de arquitectura y contratos base
> **Feature:** F-003 (Phase 1, local a esta fase)
> **Estado:** Pendiente de implementación
> **Spec ID:** SPEC-F003
>
> **Nota de numeración:** este `F-003` es el de la **Fase 1**
> (`spec/phases/phase-01/features.md`). Los IDs de feature son
> locales a cada fase; el folder los desambigua. Ver `spec/README.md`
> para la convención completa.
>
> Fuentes de verdad: `docs/vision.md`, `docs/architecture.md`,
> `docs/roadmap.md`, `spec/constitution/*`,
> `spec/phases/phase-01/features.md` (F-003 dentro del desglose de
> la fase 1).

---

## 1. Objetivo

Implementar el value object `DateRange` en el proyecto
`ControlDeFinanzasIA.Domain`, con semántica de **rango
inclusivo** en ambos extremos y operaciones de contención,
solapamiento, duración e intersección.

`DateRange` es el value object de fechas más usado del proyecto:
se aplica a periodos de gastos, presupuestos, suscripciones, metas
con fecha objetivo, deudas con plan de cuotas, y a cualquier filtro
temporal de las features futuras (F-005 en adelante). Definirlo
bien y probarlo ahora evita arrastrar errores de diseño a todas
las features que filtran por fecha.

---

## 2. Alcance

### 2.1 En alcance

- `record DateRange` con propiedades `Start` y `End` de tipo
  `DateOnly` (BCL .NET 6+, sin componente de hora ni zona
  horaria).
- Factory estático `Create(DateOnly start, DateOnly end)` que
  valida `start <= end`.
- Operación `Contains(DateOnly date) -> bool` con semántica
  **inclusiva** en ambos extremos (`start <= date <= end`).
- Operación `Overlaps(DateRange other) -> bool` que retorna
  `true` si dos rangos comparten al menos un día (inclusivo).
- Operación `DurationInDays() -> int` con conteo **inclusivo**
  (un rango de un solo día retorna `1`).
- Operación `Intersect(DateRange other) -> DateRange?` que retorna
  el rango intersección o `null` si no se solapan.
- Equality por valor (la provee `record` automáticamente).
- Inmutabilidad: propiedades `readonly`, no hay constructores
  públicos. El factory es el único camino de construcción.
- Tests unitarios en `Tests.Unit` con cobertura >= 95%.

### 2.2 Fuera de alcance

- **Rangos abiertos o semi-abiertos** (por ejemplo, `[start,
  end)` o `(start, end]`). La spec es exclusivamente inclusiva en
  ambos extremos.
- **Recurrencia** (rangos que se repiten, p. ej. "todos los
  lunes"). Esto es F-006 en el roadmap.
- **Duración en otras unidades** (semanas, meses, años). Solo se
  cubre días. Si en el futuro hace falta meses, se añade como
  overload o como método separado.
- **Zona horaria o instante de tiempo**. `DateOnly` no tiene ni
  zona ni hora; el dominio opera a nivel de "día calendario".
- **Validación con respecto a "hoy"** (por ejemplo, "Start no
  puede ser en el pasado"). Esto es lógica de aplicación, no de
  dominio; un rango histórico es válido.
- **Serialización / parseo** desde string. La persistencia la
  cubre F-002 de Fase 2; el parseo no se hace en `Domain`.

---

## 3. Casos de uso

### CU-1 — Verificar pertenencia de una fecha
- **Actor:** use case de la capa Application (ej.
  `GetExpensesInRangeQuery`).
- **Trigger:** "¿está esta fecha en el periodo del presupuesto?".
  `budget.Period.Contains(transaction.Date)`.
- **Precondición:** `DateRange` está construido con
  `Start <= End`.
- **Postcondición:** `true` si la fecha está dentro del rango
  (inclusivo), `false` en caso contrario.
- **Variantes:**
  - `range.Contains(range.Start)` retorna `true`.
  - `range.Contains(range.End)` retorna `true`.
  - `range.Contains(date_in_middle)` retorna `true`.
  - `range.Contains(date_before_start)` retorna `false`.
  - `range.Contains(date_after_end)` retorna `false`.

### CU-2 — Detectar solapamiento entre dos rangos
- **Actor:** use case de la capa Application.
- **Trigger:** "¿estas dos suscripciones se solapan en algún
  momento?". `sub1.Period.Overlaps(sub2.Period)`.
- **Precondición:** ambos `DateRange` son válidos.
- **Postcondición:** `true` si comparten al menos un día.
- **Variantes:**
  - Rangos disjuntos: `false`.
  - Rangos adyacentes (uno termina el día que el otro empieza):
    `true` (comparten ese día).
  - Un rango contiene al otro: `true`.
  - Rangos idénticos: `true`.

### CU-3 — Calcular la duración de un rango
- **Actor:** use case de la capa Application (ej.
  `GetBudgetSummaryUseCase`).
- **Trigger:** "¿cuántos días tiene este periodo?".
  `range.DurationInDays()`.
- **Precondición:** `DateRange` válido.
- **Postcondición:** número de días **inclusivo** (un rango de un
  solo día retorna `1`).
- **Variantes:**
  - `DateRange.Create(1-ene, 1-ene).DurationInDays()` → `1`.
  - `DateRange.Create(1-ene, 2-ene).DurationInDays()` → `2`.
  - `DateRange.Create(1-ene, 31-ene).DurationInDays()` → `31`.

### CU-4 — Calcular la intersección de dos rangos
- **Actor:** use case de la capa Application (ej.
  `GetOverlappingSubscriptionsQuery`).
- **Trigger:** "¿cuántos días se solapan estas dos suscripciones
  y cuáles?". `sub1.Period.Intersect(sub2.Period)`.
- **Precondición:** ambos `DateRange` válidos.
- **Postcondición:**
  - Si los rangos se solapan, retorna un `DateRange` con la
    intersección.
  - Si no se solapan, retorna `null`.
- **Variantes:**
  - `r1 = [1, 10]`, `r2 = [5, 15]` → `[5, 10]`.
  - `r1 = [1, 5]`, `r2 = [10, 15]` → `null`.
  - `r1 = r2` → mismo rango.

### CU-5 — Intento de mezcla de rangos inválidos (error de programador)
- **Actor:** programador (error de código; la UI no debería
  permitir esto).
- **Trigger:** `DateRange.Create(10-ene, 1-ene)` (start > end).
- **Precondición:** input inválido.
- **Postcondición:** la app lanza `ArgumentException` con
  mensaje claro. No se crea un `DateRange` con Start > End.

---

## 4. Requisitos funcionales

### RF-1 — `record DateRange` con `DateOnly`
- `record DateRange` con propiedades `Start` y `End`, ambas de
  tipo `DateOnly` (namespace `System`).
- `DateOnly` es el tipo apropiado: representa un día sin hora ni
  zona horaria. Disponible desde .NET 6.

### RF-2 — `DateRange` es un value object inmutable
- `record class` con propiedades `public DateOnly Start { get; }`
  y `public DateOnly End { get; }`.
- Las propiedades son **readonly** (no `init`), para bloquear la
  expresión `with` de C# 10+ que permitiría saltarse la
  validación. Misma decisión que F-002 con `Money`; documentada
  en la spec.
- Constructor **privado** (no público). El factory es el único
  camino de construcción.

### RF-3 — Validación de `Start <= End`
- En `DateRange.Create(DateOnly start, DateOnly end)`, si
  `start > end`, lanza `ArgumentException` con mensaje:
  `"Start must be less than or equal to End. Received: start={start:o}, end={end:o}."`.
- `start == end` es válido (rango de un solo día).
- La validación se aplica una sola vez (en el factory). Las
  operaciones aritméticas no revalidan.

### RF-4 — `Contains(DateOnly date) -> bool`
- Retorna `true` si `date >= Start && date <= End` (inclusivo en
  ambos extremos).
- Semántica inclusiva: tanto `Start` como `End` están en su
  propio rango.

### RF-5 — `Overlaps(DateRange other) -> bool`
- Retorna `true` si los dos rangos comparten al menos un día
  (inclusivo).
- Fórmula: `Start <= other.End && other.Start <= End`.
- Si `other` es `null`, lanza `ArgumentNullException(nameof(other))`.
- Rangos adyacentes (uno termina el día que el otro empieza)
  retornan `true` (comparten ese día).
- Rangos idénticos retornan `true`.
- Rangos disjuntos (no comparten ningún día) retornan `false`.

### RF-6 — `DurationInDays() -> int`
- Retorna el número de días **inclusivo** del rango.
- Fórmula: `End.DayNumber - Start.DayNumber + 1`.
- `Start == End` → `1` (un solo día).
- `End < Start` no puede ocurrir por la invariante (el factory
  rechaza esa entrada). Si el contrato se rompe por reflexión
  u otro mecanismo, `End.DayNumber - Start.DayNumber + 1` puede
  dar un valor <= 0; no se valida porque se asume la invariante.

### RF-7 — `Intersect(DateRange other) -> DateRange?`
- Retorna el rango intersección si los rangos se solapan.
- Retorna `null` si no se solapan.
- Fórmula: si `Start > other.End || other.Start > End` → `null`.
  En otro caso: `Create(Max(Start, other.Start), Min(End,
  other.End))`.
- Si `other` es `null`, lanza `ArgumentNullException(nameof(other))`.
- El resultado es siempre un `DateRange` válido (la intersección
  de dos rangos válidos con `Start <= End` es siempre un rango
  con `Start <= End`).
- Caso especial: si los rangos son idénticos, retorna un rango
  con los mismos `Start` y `End` (no la misma instancia, pero
  son iguales por valor).

### RF-8 — Equality por valor
- `record` provee `Equals`, `GetHashCode`, `==`, `!=`
  automáticamente, comparando `Start` y `End`.
- Dos `DateRange` con mismos `Start` y `End` son iguales aunque
  sean instancias diferentes.

### RF-9 — Inmutabilidad
- Las propiedades son `readonly` (no `init`, no `set`).
- Todas las operaciones devuelven **nuevas** instancias (`Overlaps`
  retorna `bool` así que no aplica; `Intersect` retorna
  `DateRange?`).
- No hay forma de modificar un `DateRange` después de creado.

---

## 5. Requisitos no funcionales

### RNF-1 — Pureza de Dominio
- `DateRange` no depende de EF Core, Avalonia, ni de ningún
  paquete externo. Solo BCL (`System` para `DateOnly`).
- Esto se valida con la regla de NetArchTest en F-007: `Domain`
  no puede tener `PackageReference`s.

### RNF-2 — Inmutabilidad thread-safe
- `record` con propiedades `readonly` garantiza que las
  instancias no se modifican tras creación. `DateRange` es
  thread-safe por construcción.

### RNF-3 — Cobertura de tests >= 95%
- Cobertura medida manualmente (sin `coverlet.collector` en esta
  fase, que se añadirá en F-007+). Todas las branches de los
  métodos públicos deben tener al menos un test.

### RNF-4 — Sin estado mutable global
- No hay cache, no hay singletons, no hay static state. Cada
  operación es determinista y pura.

### RNF-5 — Mensajes de error en inglés (en código)
- Consistente con RNF-5 de F-002: mensajes de excepción en inglés
  en el código. La UI los traduce si es necesario (F-012).

---

## 6. Reglas de negocio

### RN-1 — Rango inclusivo en ambos extremos
- `range.Contains(range.Start) == true`
- `range.Contains(range.End) == true`
- Rango de un solo día (`Start == End`) es válido y contiene ese
  día.

### RN-2 — Duración mínima es 1 día
- `DateRange.Create(start, start).DurationInDays()` retorna `1`,
  no `0`. Un rango de un solo día **es** un día, no cero días.
- Consecuencia: `DurationInDays() == (End.DayNumber - Start.DayNumber) + 1`.

### RN-3 — Intersección nula cuando no hay solapamiento
- Si dos rangos no comparten ningún día, `Intersect` retorna
  `null`. No retorna un `DateRange` "vacío".
- Los callers deben manejar el `null` explícitamente (con `?.`,
  `Match`, o `if`).

### RN-4 — Mezcla de rangos inválidos es error de programador
- Si el factory recibe `Start > End`, lanza `ArgumentException`.
  No es un caso de uso válido de la API. La UI no debería
  permitir al usuario crear rangos inválidos.

### RN-5 — Adyacencia cuenta como solapamiento
- Dos rangos adyacentes (uno termina el día `X`, el otro empieza
  el día `X`) **se solapan** (comparten el día `X`).
- Esto es consecuencia de la semántica inclusiva. Si en el futuro
  se necesita semántica exclusiva, se añadirá una variante
  explícita (`OverlapsExclusive`); no se cambia el default.

---

## 7. Modelo de dominio

```
┌─────────────────────────────────────┐
│  record DateRange                    │
│  ──────────────────                 │
│  + Start: DateOnly                   │
│  + End: DateOnly                     │
│  ──────────────────                 │
│  + Create(start, end) : DateRange    │
│  + Contains(date) : bool             │
│  + Overlaps(other) : bool            │
│  + DurationInDays() : int            │
│  + Intersect(other) : DateRange?     │
│  + Equals(other) : bool (record)     │
│  + GetHashCode() : int (record)      │
└─────────────────────────────────────┘
```

- `DateRange` es un value object: igualdad por valor, sin
  identidad, inmutable.
- `Start` y `End` son `DateOnly` (día calendario, sin zona
  horaria).
- `Intersect` retorna `DateRange?` (nullable): `null` indica no
  solapamiento.
- `DateOnly` es un struct (value type). `DateRange` envolviendo
  dos `DateOnly` es un `record class` (reference type), lo que
  permite el `Intersect` que retorna `null` sin boxing.

---

## 8. Validaciones

| # | Validación | Cuándo se aplica | Excepción |
| --- | --- | --- | --- |
| V-1 | `Start <= End` | En `DateRange.Create` | `ArgumentException` |
| V-2 | `other` no es `null` (en `Overlaps`) | En `Overlaps` | `ArgumentNullException(nameof(other))` |
| V-3 | `other` no es `null` (en `Intersect`) | En `Intersect` | `ArgumentNullException(nameof(other))` |

---

## 9. Casos borde

### EC-1 — `Start == End` (rango de un solo día)
- Válido (rango de un solo día).
- `Contains(Start) == true`, `Contains(End) == true` (mismo día).
- `DurationInDays() == 1`.
- `Overlaps(rango_que_comparte_ese_día) == true`.
- `Overlaps(rango_que_no_comparte) == false`.
- `Intersect(otro_rango_que_comparte) == mismo_rango_de_un_día`.

### EC-2 — Rangos adyacentes (uno termina el día que el otro empieza)
- `range1 = [1-ene, 5-ene]`, `range2 = [5-ene, 10-ene]`.
- Comparten el día 5-ene. `Overlaps(range1, range2) == true`.
- `Intersect(range1, range2) == [5-ene, 5-ene]` (rango de un día).

### EC-3 — Un rango contiene al otro
- `range1 = [1-ene, 31-ene]`, `range2 = [10-ene, 20-ene]`.
- `range2` está contenido en `range1`. `Overlaps == true`.
- `Intersect(range1, range2) == [10-ene, 20-ene]` (devuelve el
  rango interior).

### EC-4 — Rangos idénticos
- `range1 = range2 = [1-ene, 10-ene]`.
- `Overlaps == true`. `Intersect == [1-ene, 10-ene]`. Equality
  retorna `true` (mismo Start, mismo End).

### EC-5 — Intersección nula
- `range1 = [1-ene, 5-ene]`, `range2 = [10-ene, 15-ene]`.
- No comparten ningún día. `Overlaps == false`.
- `Intersect == null`. El caller debe manejar el `null`.

### EC-6 — DateOnly min/max values
- `Start = DateOnly.MinValue` (1/1/0001), `End = DateOnly.MaxValue`
  (12/31/9999). El rango más grande posible.
- `DurationInDays() ≈ 3 millones` (valor grande pero válido).
- `Contains(MinValue) == true`, `Contains(MaxValue) == true`.
- Operaciones de comparación (`<`, `>`, `==`) sobre `DateOnly`
  funcionan con estos valores extremos.

### EC-7 — Intersección en un solo punto
- `range1 = [1-ene, 5-ene]`, `range2 = [5-ene, 5-ene]`.
- Comparten el día 5-ene. `Overlaps == true`.
- `Intersect == [5-ene, 5-ene]`. Rango de un solo día resultante.

### EC-8 — Intersección con un rango de un día dentro de otro
- `range1 = [1-ene, 31-ene]`, `range2 = [15-ene, 15-ene]`.
- `Intersect == [15-ene, 15-ene]` (un solo día, el día 15).

---

## 10. Dependencias

### 10.1 Prerrequisitos de implementación

- **F-001 completada** (T-001 a T-014, T-010a): la solución, los
  proyectos y la configuración global están listos. El proyecto
  `ControlDeFinanzasIA.Domain` existe.
- **F-002 completada** (Money value object): el patrón de value
  object con factory y readonly está establecido. F-003 lo sigue
  consistentemente.

### 10.2 Dependencias con otras features

| Feature | Relación |
| --- | --- |
| F-002 (Money) | Independiente. F-003 no usa Money directamente. Ambos coexisten como value objects de `Domain`. |
| F-004 (IClock, IPathResolver) | Independiente. |
| F-005 (Result<T>, PagedResult<T>) | Independiente. F-003 lanza excepción; usar `Result<DateRange?>` en su lugar es decisión de F-005+. |
| F-006 (Interfaces de repositorio) | Independiente. Los repositorios futuros referenciarán `DateRange` como tipo de propiedad, pero `DateRange` no depende de ellos. |
| F-007 (NetArchTest) | Verificación. F-007 valida que `DateRange` esté en `Domain` y que `Domain` no tenga `PackageReference`s. |
| F-002 Fase 2 (Persistencia EF Core) | Consumidora. Cómo persistir `DateRange` en EF Core se cubre en la spec de Fase 2. |

### 10.3 Bloqueos

- **Ninguno.** F-003 puede implementarse inmediatamente después de
  F-002.

---

## 11. Fuera de alcance (detallado)

Repetición con énfasis para evitar ambigüedad. Estos puntos **NO**
se implementan en F-003:

- **Rangos abiertos o semi-abiertos.** La spec es exclusivamente
  inclusiva. Si en el futuro se necesita `[start, end)`, se
  añadirá como un tipo separado (`HalfOpenDateRange`) o como un
  flag en `DateRange`. Aquí NO.
- **Recurrencia** (rangos que se repiten). Esto es F-006. Aquí
  NO.
- **Duración en otras unidades** (semanas, meses, años). Solo días.
  Si hace falta, se añade como overload. Aquí NO.
- **Zona horaria o instante de tiempo.** `DateOnly` no tiene ni
  zona ni hora; el dominio opera a nivel de día. Aquí NO.
- **Validación con respecto a "hoy".** Un rango histórico es
  válido. Aquí NO.
- **Serialización / parseo** desde string. La persistencia la
  cubre F-002 de Fase 2. Aquí NO.
- **Validación de "fecha en el pasado" o "fecha en el futuro".**
  Esto es lógica de aplicación, no de dominio. Aquí NO.
- **Operaciones de conjunto avanzadas** (unión, diferencia
  simétrica). `Intersect` cubre lo básico; operaciones más
  complejas se añaden bajo demanda.

---

## 12. Criterios de aceptación

La feature se considera completada cuando **todos** los siguientes
puntos se cumplen, verificables mediante tests automatizados y
revisión de código.

### AC-1 — Factory y validación
- [ ] `DateRange.Create(DateOnly(2026, 1, 1), DateOnly(2026, 1, 10))`
      devuelve un `DateRange` con `Start=2026-01-01`, `End=2026-01-10`.
- [ ] `DateRange.Create(DateOnly(2026, 1, 1), DateOnly(2026, 1, 1))`
      devuelve un rango válido (un solo día).
- [ ] `DateRange.Create(DateOnly(2026, 1, 10), DateOnly(2026, 1, 1))`
      lanza `ArgumentException` con mensaje que contiene
      `"Start"` y `"End"`.

### AC-2 — `Contains`
- [ ] `DateRange.Create(5-ene, 10-ene).Contains(7-ene) == true`
      (fecha dentro).
- [ ] `DateRange.Create(5-ene, 10-ene).Contains(5-ene) == true`
      (fecha en el extremo Start, inclusivo).
- [ ] `DateRange.Create(5-ene, 10-ene).Contains(10-ene) == true`
      (fecha en el extremo End, inclusivo).
- [ ] `DateRange.Create(5-ene, 10-ene).Contains(4-ene) == false`
      (fecha antes).
- [ ] `DateRange.Create(5-ene, 10-ene).Contains(11-ene) == false`
      (fecha después).
- [ ] `DateRange.Create(7-ene, 7-ene).Contains(7-ene) == true`
      (rango de un solo día contiene ese día).

### AC-3 — `Overlaps`
- [ ] `DateRange.Create(1-ene, 10-ene).Overlaps(DateRange.Create(5-ene, 15-ene))`
      == `true` (solapan en 5-ene a 10-ene).
- [ ] `DateRange.Create(1-ene, 5-ene).Overlaps(DateRange.Create(10-ene, 15-ene))`
      == `false` (disjuntos).
- [ ] `DateRange.Create(1-ene, 5-ene).Overlaps(DateRange.Create(5-ene, 10-ene))`
      == `true` (adyacentes, comparten el día 5-ene).
- [ ] `DateRange.Create(1-ene, 31-ene).Overlaps(DateRange.Create(10-ene, 20-ene))`
      == `true` (uno contiene al otro).
- [ ] `DateRange.Create(1-ene, 10-ene).Overlaps(DateRange.Create(1-ene, 10-ene))`
      == `true` (idénticos).
- [ ] `range.Overlaps(null!)` lanza `ArgumentNullException`.

### AC-4 — `DurationInDays`
- [ ] `DateRange.Create(1-ene, 1-ene).DurationInDays() == 1`
      (un solo día).
- [ ] `DateRange.Create(1-ene, 2-ene).DurationInDays() == 2`
      (dos días).
- [ ] `DateRange.Create(1-ene, 31-ene).DurationInDays() == 31`
      (enero completo).
- [ ] `DateRange.Create(31-ene, 1-ene).DurationInDays()` — caso
      imposible por la invariante; si se prueba por reflexión o
      estado corrupto, retorna un valor no positivo (no se
      valida).

### AC-5 — `Intersect`
- [ ] `DateRange.Create(1-ene, 10-ene).Intersect(DateRange.Create(5-ene, 15-ene))`
      == `DateRange.Create(5-ene, 10-ene)`.
- [ ] `DateRange.Create(1-ene, 5-ene).Intersect(DateRange.Create(10-ene, 15-ene))`
      == `null` (no solapan).
- [ ] `DateRange.Create(1-ene, 10-ene).Intersect(DateRange.Create(3-ene, 7-ene))`
      == `DateRange.Create(3-ene, 7-ene)` (uno contiene al otro,
      retorna el interior).
- [ ] `range1.Intersect(range1) == range1` (mismos Start y End,
      instancias distintas pero iguales por valor).
- [ ] `range.Intersect(null!)` lanza `ArgumentNullException`.

### AC-6 — Equality
- [ ] `DateRange.Create(1-ene, 10-ene) == DateRange.Create(1-ene, 10-ene)`
      (record equality).
- [ ] `DateRange.Create(1-ene, 10-ene) != DateRange.Create(1-ene, 11-ene)`
      (distinto End).
- [ ] `DateRange.Create(1-ene, 10-ene) != DateRange.Create(2-ene, 10-ene)`
      (distinto Start).
- [ ] `range.GetHashCode() == range.GetHashCode()` (consistente).

### AC-7 — Inmutabilidad
- [ ] `DateRange.Start` y `DateRange.End` son `readonly` (verificable
      intentando asignar: no compila).
- [ ] `range.Overlaps(other)` no muta `range` ni `other`.
- [ ] `range.Intersect(other)` devuelve una **nueva** instancia
      (no muta `range` ni `other`).

### AC-8 — Cobertura y calidad
- [ ] Cobertura de `DateRange` >= 95% (medida manualmente sin
      `coverlet.collector`; cuando se añada en F-007+, verificación
      automática).
- [ ] `dotnet build` desde la raíz: 0 warnings, 0 errors.
- [ ] `dotnet format style --verify-no-changes`: exit 0.
- [ ] `dotnet test` (proyecto `Tests.Unit`): todos los tests
      pasan.
- [ ] NetArchTest (F-007) confirma que `DateRange` está en
      `Domain` y que `Domain` no tiene `PackageReference`s.

### AC-9 — Convenciones del repo
- [ ] `DateRange` sigue el `.editorconfig` del proyecto
      (file-scoped namespace, no `using` redundantes, naming
      PascalCase público, métodos `private` con prefijo `_camelCase`
      si los hay).
- [ ] `DateRange` es público (no `internal` ni `private`),
      porque las features que filtren por fecha lo consumirán.
- [ ] XML doc exhaustivo en el record, las propiedades, y los
      métodos públicos (consistente con F-002).
- [ ] No hay archivos `DateRange1.cs` ni placeholders similares.

---

## 13. Estructura de archivos esperada

```
src/
└── ControlDeFinanzasIA.Domain/
    ├── ControlDeFinanzasIA.Domain.csproj
    ├── Entities/
    │   └── (vacío; las entidades llegan en features posteriores)
    ├── ValueObjects/
    │   ├── Money.cs                      (F-002, ya existe)
    │   └── DateRange.cs                  ← nuevo (F-003)
    ├── Enums/
    │   └── Currency.cs                   (F-001, ya existe)
    ├── DomainServices/
    │   └── (vacío; los servicios llegan con features posteriores)
    ├── Interfaces/
    │   └── (vacío; las interfaces llegan con F-004+)
    └── ...

tests/
└── ControlDeFinanzasIA.Tests.Unit/
    ├── (sin placeholder; el de F-001 se eliminó en T-007)
    ├── ValueObjects/
    │   ├── MoneyTests.cs                 (F-002, ya existe)
    │   └── DateRangeTests.cs             ← nuevo (F-003)
    └── ...
```

---

## 14. Riesgos y mitigaciones

| Riesgo | Probabilidad | Impacto | Mitigación |
| --- | --- | --- | --- |
| Confusión entre `DateOnly` y `DateTime` | Media | Bajo | El spec es explícita: `DateOnly` siempre. Tests con `DateOnly` en lugar de `DateTime`. |
| Decisión "inclusivo" vs "exclusivo" en `Contains` / `Overlaps` | Baja | Medio | La spec documenta inclusivo en ambos extremos. Si en el futuro se necesita exclusivo, se añade un método separado, no se cambia el default. |
| `DateOnly` no disponible en .NET < 6 | Baja | Alto | El proyecto targeta .NET 8. `DateOnly` está disponible desde .NET 6. No es un riesgo en este target. |
| `with` expression saltando la validación | Baja | Alto | Usar propiedades `readonly` (no `init`) bloquea `with`. Misma decisión que F-002 con `Money`. |
| `Intersect` retornando `null` causa `NullReferenceException` en callers | Media | Medio | Documentado en RN-3 y AC-5. Los callers deben usar `?.`, `Match`, o `if`. No se "arregla" retornando un `DateRange` vacío (rompería la invariante). |
| Rendimiento: `Intersect` crea instancias | Baja | Bajo | `DateRange` es un POCO pequeño (2 `DateOnly`). Crear instancias es barato. Si el profiling muestra problemas, se introduce un pool o struct. |
| Tests con `DateOnly` hardcoded en strings | Baja | Bajo | Usar `new DateOnly(2026, 1, 1)` directamente; el tipado fuerte previene errores de formato. |

---

## 15. Referencias

- `docs/vision.md` §4.1 (transacciones) y §4.4 (metas con fecha
  objetivo).
- `docs/architecture.md` §6 (Dominio: entidades, value objects,
  servicios de dominio puros).
- `docs/roadmap.md` § Fase 1, F-003 dentro del desglose.
- `spec/constitution/tech-stack.md` — convenciones de código y
  reglas de paquetes.
- `spec/constitution/mission.md` — propósito del producto.
- `spec/phases/phase-01/features.md` — F-003 dentro del
  desglose de features de la fase 1.
- `spec/features/phase-01/F-001-skeleton-solution.done.md` —
  provee la base de proyectos y configuración.
- `spec/features/phase-01/F-002-money-value-object.done.md` —
  provee el patrón de value object que F-003 sigue.
- `spec/README.md` — convención completa del directorio `spec/`.
- **Documentación de `DateOnly`:** [learn.microsoft.com/dotnet/api/system.dateonly](https://learn.microsoft.com/dotnet/api/system.dateonly)
