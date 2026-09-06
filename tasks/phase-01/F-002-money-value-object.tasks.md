# Tareas — F-002: Money Value Object con enum Currency

> **Spec:** `spec/features/phase-01/F-002-money-value-object.md`
> **Fuentes de verdad:** la spec, `docs/architecture.md`,
> `docs/vision.md`, `docs/roadmap.md`, `spec/constitution/*`,
> `spec/features/phase-01/F-001-skeleton-solution.done.md` (F-001
> completada, provee la base de proyectos y configuración).
>
> Cada tarea es implementable en menos de 2 horas. El proyecto debe
> seguir compilando al finalizar cada una. La numeración (T-001...)
> es local a esta feature; las tareas de F-001 viven en
> `tasks/phase-01/F-001-skeleton-solution.tasks.md`.

---

## Resumen de tareas

| ID | Tarea | Estimación | Depende de |
| --- | --- | --- | --- |
| T-001 | Crear el enum `Currency` con DOP y USD | XS | — |
| T-002 | Crear el value object `Money` (data + factory + Zero) + tests de creación y validación | M | T-001 |
| T-003 | Añadir la operación `Add` + tests (válida y mezcla de monedas) | S | T-002 |
| T-004 | Añadir la operación `Subtract` + tests (válida, mezcla, resultado negativo) | S | T-002 |
| T-005 | Añadir la operación `Multiply` + tests (válida, escalar negativo, escalar cero) | S | T-002 |
| T-006 | Añadir `ToString(CultureInfo)` y `ToString()` + tests (es-DO, en-US, null, default) | S | T-002 |
| T-007 | Verificación final: tests, format check, build, cobertura, limpieza y cierre | M | todas |

**Total estimado:** 1 jornada o menos. T-002 es la más larga (la
primera tarea "real" de código, introduce el patrón de value
object); el resto son operaciones independientes y tests asociados.

---

## T-001 — Crear el enum `Currency` con DOP y USD

**Objetivo**
Establecer el enum `Currency` en `Domain`, con los dos valores
iniciales (`DOP` y `USD`). Es la base sobre la que se construye
`Money`.

**Descripción**
Crear el archivo
`src/ControlDeFinanzasIA.Domain/Enums/Currency.cs` con un enum
público `Currency` y exactamente dos miembros: `DOP` (valor 0) y
`USD` (valor 1).

**Por qué se hace primero:** `Money` referencia `Currency`. Si
`Currency` no existe, `Money` no compila. Es un prerequisito
absoluto de T-002.

**Por qué no se añaden tests específicos para `Currency`:** los
dos valores del enum son tan triviales que un test dedicado
("`Currency.DOP == 0`") no aporta. `Currency` se valida
**implícitamente** a través de los tests de `Money` (T-002 en
adelante), que usan `Currency.DOP` y `Currency.USD` en todas las
operaciones.

**Dependencias**
- Ninguna (F-001 ya creó el proyecto `Domain`).

**Archivos / proyectos afectados**
- `src/ControlDeFinanzasIA.Domain/Enums/Currency.cs` (nuevo).

**Criterios de finalización**
- El archivo existe en la ruta exacta.
- El enum es `public`.
- Tiene exactamente dos miembros: `DOP` y `USD`.
- El proyecto compila sin warnings.
- `dotnet list src/ControlDeFinanzasIA.Domain package` sigue
  mostrando que `Domain` no tiene `PackageReference`s (preparación
  para F-007).
- `dotnet format style --verify-no-changes` pasa.

---

## T-002 — Crear el value object `Money` con factory y `Zero`

**Objetivo**
Tener `Money` definido como value object inmutable con la API
mínima: propiedades `Amount` y `Currency` (solo lectura), factory
estático `Create` que valida, y helper `Zero`. Esta tarea es el
cimiento; las operaciones (`Add`, `Subtract`, `Multiply`,
`ToString`) llegan en T-003 a T-006.

**Descripción**
Crear el archivo
`src/ControlDeFinanzasIA.Domain/ValueObjects/Money.cs` con:

- `public sealed record Money` con propiedades públicas de solo
  lectura: `Amount` (decimal) y `Currency` (`Currency` enum de
  T-001).
- Constructor **privado o interno** (NUNCA público). El factory
  es el único camino de construcción.
- `public static Money Create(decimal amount, Currency currency)`
  con validación:
  - `amount >= 0`, si no → `ArgumentOutOfRangeException`.
  - `amount` finito (rechaza `NaN`, `PositiveInfinity`,
    `NegativeInfinity`), si no → `ArgumentException`.
  - `currency` no es `null` (no aplica en enum, pero documentado
    para futuras extensiones).
- `public static Money Zero(Currency currency)` que devuelve
  `Money.Create(0m, currency)`.
- Equality por valor (la provee `record` automáticamente).
- Documentar con comentarios XML públicos la intención de cada
  método (regla de `architecture.md` §6).

Crear el archivo de tests
`tests/ControlDeFinanzasIA.Tests.Unit/ValueObjects/MoneyTests.cs`
con tests que cubran los criterios AC-1 y AC-6 de la spec:

- AC-1 (factory y validación): `Create` con valores válidos
  (positivos, cero), con valores inválidos (negativo, NaN, ±Infinity),
  `Zero` con cada Currency.
- AC-6 (equality): `==` y `!=` entre Money con mismo/diferente
  Amount, mismo/diferente Currency; `Equals`; `GetHashCode`.

Mantener `F001PlaceholderTests.cs` en `Tests.Unit` (sigue siendo
un test skipped válido; se eliminará en T-007 cuando haya tests
reales).

**Dependencias**
- T-001 (`Currency` debe existir).

**Archivos / proyectos afectados**
- `src/ControlDeFinanzasIA.Domain/ValueObjects/Money.cs` (nuevo).
- `tests/ControlDeFinanzasIA.Tests.Unit/ValueObjects/MoneyTests.cs`
  (nuevo).
- `tests/ControlDeFinanzasIA.Tests.Unit/F001PlaceholderTests.cs`
  (sin cambios; coexiste con los nuevos tests).

**Criterios de finalización**
- `Money.cs` existe en `ValueObjects/`.
- `Money` es `public sealed record`.
- El constructor no es público (verificable intentando `new
  Money(...)` desde un test; debe fallar compilación o lanzar
  error de accesibilidad).
- `Money.Create(100m, Currency.DOP)` devuelve una instancia
  válida.
- `Money.Create(-1m, Currency.DOP)` lanza
  `ArgumentOutOfRangeException`.
- `Money.Create(decimal.NaN, Currency.DOP)` lanza
  `ArgumentException`.
- `Money.Create(decimal.PositiveInfinity, Currency.DOP)` lanza
  `ArgumentException`.
- `Money.Create(decimal.NegativeInfinity, Currency.DOP)` lanza
  `ArgumentException`.
- `Money.Zero(Currency.USD)` devuelve `Amount=0`, `Currency=USD`.
- `Money.Create(100, DOP) == Money.Create(100, DOP)` (record
  equality).
- `Money.Create(100, DOP) != Money.Create(100, USD)`.
- `Money.Create(100, DOP) != Money.Create(200, DOP)`.
- `dotnet build` desde la raíz: 0 warnings, 0 errors.
- `dotnet test --project tests/ControlDeFinanzasIA.Tests.Unit`
  pasa todos los tests nuevos; los placeholders siguen
  `Skipped`.
- `dotnet format style --verify-no-changes` pasa.
- `Domain` sigue sin `PackageReference`s.

---

## T-003 — Añadir la operación `Add` con validación de mezcla de monedas

**Objetivo**
Implementar la suma de dos `Money` del mismo `Currency`, con
rechazo explícito (excepción) cuando las monedas no coinciden.

**Descripción**
Añadir a `src/ControlDeFinanzasIA.Domain/ValueObjects/Money.cs`:

- `public Money Add(Money other) -> Money`:
  - Si `other` es `null`, lanza `ArgumentNullException` (defensa
    de API; no debería pasar en código de uso normal).
  - Si `other.Currency != this.Currency`, lanza
    `InvalidOperationException` con mensaje: `"Cannot operate on
    Money with different currencies: {A} vs {B}."` (A y B son los
    nombres de los enums).
  - Si pasa las validaciones, devuelve `new Money(this.Amount +
    other.Amount, this.Currency)`. (El constructor privado se
    invoca aquí; la validación previa garantiza que el resultado
    es válido.)

Añadir a `tests/ControlDeFinanzasIA.Tests.Unit/ValueObjects/MoneyTests.cs`
los tests para AC-2 (aritmética válida) y AC-3 (rechazo de
mezcla):

- `Money.Create(100, DOP).Add(Money.Create(50, DOP))` ==
  `Money.Create(150, DOP)`.
- `Money.Create(0, DOP).Add(Money.Create(0, DOP))` ==
  `Money.Zero(DOP)`.
- `Money.Zero(DOP).Add(Money.Create(50, DOP))` ==
  `Money.Create(50, DOP)`.
- `Money.Create(100, DOP).Add(Money.Create(50, USD))` lanza
  `InvalidOperationException` con mensaje que contiene `"DOP"`
  y `"USD"`.
- `Add(null)` lanza `ArgumentNullException`.

**Dependencias**
- T-002 (`Money` debe existir con factory).

**Archivos / proyectos afectados**
- `src/ControlDeFinanzasIA.Domain/ValueObjects/Money.cs`
  (modificado: añadir `Add`).
- `tests/ControlDeFinanzasIA.Tests.Unit/ValueObjects/MoneyTests.cs`
  (modificado: añadir tests).

**Criterios de finalización**
- `Money.Add(Money)` existe y es `public`.
- Devuelve una **nueva** instancia de `Money` (no muta `this`).
- El resultado tiene `Currency == this.Currency` y `Amount` igual a
  la suma.
- Lanza `InvalidOperationException` si las monedas difieren.
- Lanza `ArgumentNullException` si `other` es `null`.
- Los tests anteriores pasan.
- `dotnet build`: 0 warnings, 0 errors.
- `dotnet test`: pasan los nuevos tests + los de T-002.
- `dotnet format style`: pasa.

---

## T-004 — Añadir la operación `Subtract` con validación de resultado no negativo

**Objetivo**
Implementar la resta de dos `Money` del mismo `Currency`, con dos
validaciones: misma moneda (como en `Add`) y resultado no
negativo.

**Descripción**
Añadir a `src/ControlDeFinanzasIA.Domain/ValueObjects/Money.cs`:

- `public Money Subtract(Money other) -> Money`:
  - Si `other` es `null`, lanza `ArgumentNullException`.
  - Si `other.Currency != this.Currency`, lanza
    `InvalidOperationException` con el mismo mensaje que `Add`.
  - Calcula `this.Amount - other.Amount`. Si el resultado es `< 0`,
    lanza `InvalidOperationException` con mensaje: `"Subtraction
    would result in negative Money: {a} - {b} = {diff} {c}."`.
  - Si pasa, devuelve `new Money(result, this.Currency)`.

Añadir los tests para AC-2, AC-3 y el caso borde EC-5:

- `Money.Create(100, DOP).Subtract(Money.Create(30, DOP))` ==
  `Money.Create(70, DOP)`.
- `Money.Create(100, DOP).Subtract(Money.Create(100, DOP))` ==
  `Money.Zero(DOP)`.
- `Money.Zero(DOP).Subtract(Money.Zero(DOP))` == `Money.Zero(DOP)`.
- `Money.Create(50, DOP).Subtract(Money.Create(100, DOP))` lanza
  `InvalidOperationException` con mensaje que contiene `"-50"`.
- `Money.Create(100, DOP).Subtract(Money.Create(50, USD))` lanza
  `InvalidOperationException` (mezcla de monedas).
- `Subtract(null)` lanza `ArgumentNullException`.

**Dependencias**
- T-002.

**Archivos / proyectos afectados**
- `src/ControlDeFinanzasIA.Domain/ValueObjects/Money.cs`
  (modificado).
- `tests/ControlDeFinanzasIA.Tests.Unit/ValueObjects/MoneyTests.cs`
  (modificado).

**Criterios de finalización**
- `Money.Subtract(Money)` existe y es `public`.
- Devuelve una nueva instancia.
- El resultado tiene `Currency == this.Currency`.
- Lanza `InvalidOperationException` si las monedas difieren.
- Lanza `InvalidOperationException` si el resultado sería negativo.
- Lanza `ArgumentNullException` si `other` es `null`.
- Build, tests y format check: limpios.

---

## T-005 — Añadir la operación `Multiply` con validación del escalar

**Objetivo**
Implementar la multiplicación de `Money` por un escalar decimal,
con validación de que el escalar no es negativo.

**Descripción**
Añadir a `src/ControlDeFinanzasIA.Domain/ValueObjects/Money.cs`:

- `public Money Multiply(decimal scalar) -> Money`:
  - Si `scalar < 0`, lanza `ArgumentOutOfRangeException` con
    mensaje: `"Scalar must be non-negative: {scalar}."`.
  - Si pasa, devuelve `new Money(this.Amount * scalar,
    this.Currency)`. (El `Amount` resultante es `>= 0` porque
    tanto `this.Amount` como `scalar` son `>= 0`.)

**Por qué `Multiply` no valida mezcla de monedas:** la
multiplicación es `Money × decimal`, no `Money × Money`. No hay
otra moneda involucrada.

Añadir los tests para AC-2, AC-4 y los casos borde EC-6 y EC-7:

- `Money.Create(100, DOP).Multiply(2m)` == `Money.Create(200, DOP)`.
- `Money.Create(100, DOP).Multiply(0m)` == `Money.Zero(DOP)`.
- `Money.Create(100, DOP).Multiply(0.5m)` ==
  `Money.Create(50, DOP)`.
- `Money.Create(100, DOP).Multiply(-1m)` lanza
  `ArgumentOutOfRangeException` con mensaje que contiene
  `"-1"`.

**Dependencias**
- T-002.

**Archivos / proyectos afectados**
- `src/ControlDeFinanzasIA.Domain/ValueObjects/Money.cs`
  (modificado).
- `tests/ControlDeFinanzasIA.Tests.Unit/ValueObjects/MoneyTests.cs`
  (modificado).

**Criterios de finalización**
- `Money.Multiply(decimal)` existe y es `public`.
- Devuelve una nueva instancia.
- Lanza `ArgumentOutOfRangeException` si `scalar < 0`.
- Acepta `scalar = 0` (devuelve `Money.Zero`).
- Build, tests y format check: limpios.

---

## T-006 — Añadir `ToString(CultureInfo)` y `ToString()`

**Objetivo**
Implementar el formato de `Money` para mostrar al usuario,
respetando la cultura y usando el código ISO de la moneda.

**Descripción**
Añadir a `src/ControlDeFinanzasIA.Domain/ValueObjects/Money.cs`:

- `public string ToString(CultureInfo culture)`:
  - Si `culture` es `null`, lanza `ArgumentNullException`.
  - Formatea `Amount` con el formato `"N2"` aplicado a la cultura
    (esto da `"1,234.56"` en `es-DO` y `en-US`, o `"1 234,56"` en
    `fr-FR`).
  - Devuelve la concatenación: `<formattedAmount> <Currency>`.
    Ejemplos:
    - es-DO: `"1,234.56 DOP"`
    - en-US: `"1,234.56 USD"`
    - fr-FR: `"1 234,56 DOP"`
- `public override string ToString()`: delega a
  `this.ToString(CultureInfo.CurrentCulture)`.

Añadir los tests para AC-5:

- `Money.Create(1234.56m, DOP).ToString(CultureInfo.GetCultureInfo("es-DO"))`
  == `"1,234.56 DOP"`.
- `Money.Create(1234.56m, USD).ToString(CultureInfo.GetCultureInfo("en-US"))`
  == `"1,234.56 USD"`.
- `Money.Create(0m, DOP).ToString(CultureInfo.GetCultureInfo("es-DO"))`
  == `"0.00 DOP"`.
- `money.ToString(null as CultureInfo)` lanza
  `ArgumentNullException`.
- `Money.Create(1234.56m, DOP).ToString()` (sin parámetros)
  produce el mismo resultado que
  `money.ToString(CultureInfo.CurrentCulture)` (verificable
  usando `CurrentCulture` neutral o una cultura específica).
- `Money.Create(1234.56m, DOP).ToString(CultureInfo.GetCultureInfo("fr-FR"))`
  usa separadores franceses (contiene `" "` o `","` en lugar
  de `","` o `"."` respectivamente; verificación flexible).

**Dependencias**
- T-002.

**Archivos / proyectos afectados**
- `src/ControlDeFinanzasIA.Domain/ValueObjects/Money.cs`
  (modificado: añadir dos overloads de `ToString`).
- `tests/ControlDeFinanzasIA.Tests.Unit/ValueObjects/MoneyTests.cs`
  (modificado).

**Criterios de finalización**
- `ToString(CultureInfo)` es `public` y lanza
  `ArgumentNullException` si `culture` es `null`.
- `ToString()` (sin parámetros) es `public override` y delega a
  `CurrentCulture`.
- El formato es `<N2 de la cultura> <Currency>`, sin
  transformaciones adicionales.
- Build, tests y format check: limpios.

---

## T-007 — Verificación final, cobertura, limpieza y cierre

**Objetivo**
Validar que F-002 cumple los criterios de aceptación de la spec
(sección 12), limpiar artefactos temporales, y dejar el repo en
estado de "F-002 terminada".

**Descripción**
Esta tarea **no introduce código nuevo**. Consiste en ejecutar
los pasos de verificación y ejecutar las acciones de cierre.

### Pasos de verificación

1. **Compilación:** `dotnet build -c Debug` y `dotnet build -c
   Release` — ambos con 0 warnings, 0 errors.
2. **Tests:** `dotnet test` — todos los tests pasan (los reales
   de F-002 + los placeholders de F-001 que aún siguen `Skipped`).
3. **Format check:** `dotnet format style --verify-no-changes` —
   exit 0.
4. **Cobertura:** verificar que `Money.cs` y `Currency.cs` tienen
   cobertura >= 95%. La spec de F-002 menciona coverlet, pero
   coverlet aún no está añadido a CPM (F-001 lo removió). Sin
   coverlet configurado, **verificación manual**: contar que
   los tests cubren todas las branches de los métodos públicos.
   Cobertura esperada: 100% en `Money` (todas las branches
   tienen tests) y 100% trivial en `Currency` (enum sin lógica).
5. **Aislamiento de Domain:** `dotnet list src/ControlDeFinanzasIA.Domain package`
   debe seguir mostrando "No packages were found for this
   framework." (preparación para F-007).
6. **CI:** la pipeline en `.github/workflows/build.yml` pasa
   localmente los 3 steps (`dotnet build`, `dotnet test`, `dotnet
   format style --verify-no-changes`). No requiere ejecutarse en
   GitHub Actions para validar.

### Pasos de limpieza

7. **Eliminar el placeholder:** una vez que hay tests reales,
   `tests/ControlDeFinanzasIA.Tests.Unit/F001PlaceholderTests.cs`
   ya no aporta (los nuevos tests son la realidad de F-002).
   Eliminarlo. Si el equipo prefiere mantenerlo, dóblalo en una
   decisión explícita en lugar de dejarlo por inercia.
8. **Actualizar el desglose de fase:** en
   `spec/phases/phase-01/features.md`, cambiar el estado de F-002
   de "Pendiente" a "✅ COMPLETADA" (mismo formato que F-001).
9. **Renombrar la spec:** mover
   `spec/features/phase-01/F-002-money-value-object.md` a
   `spec/features/phase-01/F-002-money-value-object.done.md`
   (convención del proyecto, ver F-001).

### Resumen de cobertura manual (criterio AC-7)

Verificar que cada línea pública de `Money` está cubierta por al
menos un test:

| Miembro público | Tests |
| --- | --- |
| Constructor (privado, invocado por Create/Zero/operaciones) | T-002, T-003, T-004, T-005 |
| `Amount` getter | T-002, T-003, T-004, T-005, T-006 |
| `Currency` getter | T-002, T-003, T-004, T-005, T-006 |
| `Create(decimal, Currency)` | T-002 (válido, negativo, NaN, ±Inf) |
| `Zero(Currency)` | T-002 |
| `Add(Money)` | T-003 (válido, mezcla, null) |
| `Subtract(Money)` | T-004 (válido, mezcla, resultado negativo, null) |
| `Multiply(decimal)` | T-005 (válido, cero, negativo) |
| `ToString(CultureInfo)` | T-006 (es-DO, en-US, fr-FR, null) |
| `ToString()` | T-006 (default usa CurrentCulture) |
| `Equals`, `GetHashCode` (de record) | T-002 |

**Cobertura esperada: 100% en líneas de `Money`, 100% trivial en
`Currency`.**

**Dependencias**
- T-001 a T-006.

**Archivos / proyectos afectados**
- `tests/ControlDeFinanzasIA.Tests.Unit/F001PlaceholderTests.cs`
  (eliminado).
- `spec/phases/phase-01/features.md` (modificado: marcar F-002
  como completada).
- `spec/features/phase-01/F-002-money-value-object.md`
  (renombrado a `.done.md`).

**Criterios de finalización**
- `dotnet build` (Debug y Release): 0 warnings, 0 errors.
- `dotnet test`: todos los tests pasan.
- `dotnet format style --verify-no-changes`: exit 0.
- `Money.cs` y `Currency.cs` están en las rutas correctas bajo
  `src/ControlDeFinanzasIA.Domain/`.
- `Domain` sigue sin `PackageReference`s.
- `F001PlaceholderTests.cs` eliminado (o decisión documentada
  de conservarlo).
- `spec/phases/phase-01/features.md` actualizado con F-002
  marcada como completada.
- Spec renombrada a `.done.md`.
- **Confirmación punto por punto de los 8 grupos de criterios de
  aceptación de la spec (AC-1 a AC-8)**, presentada en la PR o
  en la conversación al cerrar la feature.

---

## Orden recomendado y trabajo en paralelo

**Orden secuencial estricto:** T-001 → T-002 → (T-003, T-004, T-005,
T-006 en orden, sin dependencias cruzadas) → T-007.

**No se recomienda paralelizar** T-003, T-004, T-005, T-006 entre sí
porque todas modifican el mismo archivo `Money.cs`. Con 2 personas
se podría paralelizar tests (cada persona su propio archivo de
tests), pero la definición del método en `Money.cs` debe ser
secuencial.

**T-002 es la tarea más delicada.** Define el patrón de value
object y el factory. Si se hace bien, T-003 a T-006 son
mecánicas. Si se hace con prisa (p. ej. exponiendo el constructor
público para "acabar rápido"), hay que corregirlo en T-002 o en
T-003 antes de seguir.

---

## Riesgos de la fase de tareas

| Riesgo | Probabilidad | Impacto | Mitigación |
| --- | --- | --- | --- |
| `decimal` overflow en operaciones extremas | Baja | Bajo | `decimal` puede lanzar `OverflowException`; documentado en el spec. No es responsabilidad de `Money` validarlo. |
| Mezcla accidental de monedas en código de uso | Media | Alto | Tests + excepción ruidosa; mitigado por el spec. |
| Cobertura "manual" sin coverlet (mientras no se añada en F-007) | Media | Bajo | La tabla de T-007 enumera todos los miembros públicos; cualquier branch no testeada se detecta manualmente. |
| Cambios en la API pública de `Money` rompen Fase 2 (Persistencia) | Baja | Alto | El header de la spec de F-002 Phase 2 referencia esta spec; cualquier cambio debe reflejarse allá. El contrato de `Money` debe ser estable. |
| `ToString` formato cambia entre culturas (`fr-FR` no usa `,` sino ` `) | Media | Bajo | Tests parametrizados con `[Theory]` + `[InlineData]` para múltiples culturas; documentar el formato esperado. |
| Tests no descubiertos por xUnit si la convención cambia | Baja | Bajo | Convenciones fijas: `[Fact]` y `[Theory]` con `[InlineData]`. Verificado en T-013 de F-001 que el descubrimiento funciona. |

---

## Referencias

- `spec/features/phase-01/F-002-money-value-object.md` — la spec
  completa de F-002. Este tasks file es su descomposición técnica.
- `spec/features/phase-01/F-001-skeleton-solution.done.md` — F-001
  completada, provee la base: proyecto `Domain` existe, `Nullable`
  y `ImplicitUsings` activos, sin `PackageReference`s.
- `docs/architecture.md` §6 (Dominio: entidades, value objects,
  servicios de dominio puros).
- `docs/vision.md` §4.1 (transacciones) y §4.9 (IA, mención de
  multi-moneda).
- `docs/roadmap.md` § Fase 1, F-002 dentro del desglose.
- `spec/constitution/tech-stack.md` — convenciones de código y
  reglas de paquetes.
- `spec/phases/phase-01/features.md` — F-002 dentro del
  desglose de features de la fase 1.
- `spec/features/phase-02/F-002-persistence-efcore-sqlite.md` —
  spec consumidora de F-002 (Fase 2).
