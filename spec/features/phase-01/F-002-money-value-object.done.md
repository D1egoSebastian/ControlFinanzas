# Specification — F-002: Money Value Object con enum Currency

> **Fase:** 1 — Esqueleto de arquitectura y contratos base
> **Feature:** F-002 (Phase 1, local a esta fase)
> **Estado:** Pendiente de implementación
> **Spec ID:** SPEC-F002
>
> **Nota de numeración:** este `F-002` es el de la **Fase 1**
> (`spec/phases/phase-01/features.md`). No debe confundirse con el
> `F-002` de la Fase 2 (Persistencia con EF Core + SQLite) en
> `spec/features/phase-02/F-002-persistence-efcore-sqlite.md`. La
> numeración de features es local a cada fase; el folder los
> desambigua. Ver `spec/README.md` para la convención.
>
> Fuentes de verdad: `docs/vision.md`, `docs/architecture.md`,
> `docs/roadmap.md`, `spec/constitution/*`,
> `spec/phases/phase-01/features.md` (F-002 dentro del desglose de
> la fase).

---

## 1. Objetivo

Implementar el value object `Money` y el enum `Currency` en el
proyecto `ControlDeFinanzasIA.Domain`, con aritmética segura entre
valores de la misma moneda, formato coherente con la cultura local
del usuario (es-DO), y validación estricta de los datos de entrada.

`Money` es el value object más usado del proyecto: aparece como
propiedad de `Expense`, `Income`, `Debt`, `Goal`, `Budget`,
`RecurringRule` (definidos en features posteriores). Definirlo bien
y probarlo ahora evita arrastrar errores de diseño a todas las
features que lo consumirán.

---

## 2. Alcance

### 2.1 En alcance

- `enum Currency` con valores `DOP` y `USD` (extensible a más
  monedas en el futuro sin romper API).
- `record Money` inmutable con `Amount` (decimal) y `Currency`.
- Factory estático `Money.Create(decimal, Currency)` que valida
  `Amount`.
- Helper `Money.Zero(Currency)` como valor inicial para agregaciones.
- Operaciones aritméticas: `Add`, `Subtract`, `Multiply(escalar)`.
- Validación de `Amount`: `>= 0`. La validación de finitud
  (rechazo de `NaN`, `PositiveInfinity`, `NegativeInfinity`) es
  **aspiracional** en la spec — la BCL de .NET 8.0.28 no expone
  esos valores, por lo que el chequeo es vacuo. Ver sección 13
  (Notas sobre la implementación) y V-2 en la tabla de
  validaciones.
- Rechazo de operaciones entre monedas distintas con excepción
  tipada y mensaje claro.
- `ToString(CultureInfo)` y `ToString()` (usa `CurrentCulture`).
- Tests unitarios en `Tests.Unit` con cobertura >= 95%.

### 2.2 Fuera de alcance

- **Conversión entre monedas** (DOP ↔ USD). Es responsabilidad de
  la capa de presentación, que consulta `IExchangeRateProvider` y
  muestra el equivalente. La conversión NO es lógica de dominio
  (requiere una tasa de cambio, dato externo).
- **Redondeo al display.** El redondeo se aplica al formatear para
  mostrar al usuario, no al almacenar ni al operar. `decimal` mantiene
  precisión exacta en operaciones.
- **Display / formato visual** (color, íconos, alineación). La
  presentación es responsabilidad de Avalonia.
- **Persistencia.** Cómo se guarda `Money` en EF Core se cubre en
  `spec/features/phase-02/F-002-persistence-efcore-sqlite.md` (Fase
  2). El value object es agnóstico al almacenamiento.
- **Localización exhaustiva.** La spec cubre `es-DO` y `en-US`
  explícitamente. La API es genérica para cualquier `CultureInfo`,
  pero otras culturas no se prueban en v1.
- **Operaciones avanzadas** (división, módulo, comparación
  mayor/menor). Si se necesitan en features futuras, se añaden
  como overloads.

---

## 3. Casos de uso

### CU-1 — Registrar un gasto en una moneda
- **Actor:** usuario (a través de la UI).
- **Trigger:** la UI captura un monto y una moneda, llama al factory
  de Money, asigna el resultado a un `Expense`.
- **Precondición:** el usuario ha seleccionado una moneda (DOP por
  defecto en es-DO; USD si el gasto fue en otra moneda).
- **Postcondición:** existe un `Money` válido con `Amount >= 0` y
  `Currency` no nulo. El registro se persiste (responsabilidad de
  Fase 2, no de F-002).
- **Variante negativa:** si el usuario introduce un monto negativo
  o NaN, la UI muestra un error de validación y NO llama al
  factory. F-002 lanza la excepción solo si la UI no lo evita.

### CU-2 — Sumar varios Money del mismo Currency
- **Actor:** use case de la capa Application (ej.
  `GetMonthSummaryUseCase`).
- **Trigger:** agregar todos los gastos de un periodo: `total =
  expenses.Aggregate(Money.Zero(Currency.DOP), (acc, e) =>
  acc.Add(e.Amount))`.
- **Precondición:** todos los `Money` involucrados tienen el mismo
  `Currency`.
- **Postcondición:** el `Money` resultante tiene ese `Currency` y
  `Amount = sum(Amount)`.

### CU-3 — Mostrar Money formateado en la UI
- **Actor:** binding de Avalonia (`TextBlock.Text`).
- **Trigger:** la UI llama a `money.ToString(CultureInfo.CurrentCulture)`
  o usa el `ToString()` sin parámetros (que delega a
  `CurrentCulture`).
- **Precondición:** el usuario ha configurado su cultura (es-DO por
  defecto; en-US si el sistema está en inglés).
- **Postcondición:** el `TextBlock` muestra `"1,234.56 DOP"` o
  `"1,234.56 USD"`, con separadores de la cultura.

### CU-4 — Intento de mezclar monedas (caso negativo, error de programador)
- **Actor:** programador (error de código; la UI no debería permitir
  esto).
- **Trigger:** `dopMoney.Add(usdMoney)` ejecuta.
- **Precondición:** los dos `Money` tienen `Currency` distintos.
- **Postcondición:** se lanza `InvalidOperationException` con
  mensaje `"Cannot operate on Money with different currencies:
  DOP vs USD."` La operación NO devuelve un valor incorrecto.

### CU-5 — Validación de Amount inválido
- **Actor:** programador o UI.
- **Trigger:** `Money.Create(-50m, Currency.DOP)` o
  `Money.Create(decimal.NaN, Currency.DOP)` ejecuta.
- **Precondición:** el dato de entrada es inválido.
- **Postcondición:** se lanza `ArgumentOutOfRangeException` o
  `ArgumentException` con mensaje que identifica el valor inválido.

---

## 4. Requisitos funcionales

### RF-1 — Enum `Currency`
- `enum Currency { DOP, USD }` en `src/ControlDeFinanzasIA.Domain/Enums/Currency.cs`.
- Es extensible: añadir un valor (ej. `EUR`) es un cambio aislado
  que no rompe la API pública de `Money` (los callers usan el enum
  por nombre o por valor, no por ordinal magic).

### RF-2 — `Money` es un value object inmutable
- `record Money` con `Amount` (decimal) y `Currency`.
- Las propiedades son inmutables (`init`-only o readonly).
- No hay forma de construir un `Money` con valores inválidos: el
  único camino es el factory estático `Money.Create`, que valida.

### RF-3 — Validación de `Amount`
- `Amount >= 0` (no se permiten montos negativos).
- `Amount` es finito (rechazo de `decimal.NaN`,
  `decimal.PositiveInfinity`, `decimal.Decimal.NegativeInfinity`).
  **Nota de implementación en .NET 8:** la BCL 8.0.28 no expone
  las constantes `decimal.NaN`/`+Inf`/`-Inf` ni los métodos
  `decimal.IsFinite`/`IsNaN`/`IsInfinity`; el constructor
  `new decimal(int, int, int, bool, byte)` rechaza explícitamente
  `scale > 28`. Por tanto, en .NET 8 ningún `decimal` puede tener
  un valor no-finito a través de la BCL, y este chequeo es vacuo.
  La implementación actual omite este chequeo (con un `<remarks>`
  en `Money.Create` que documenta la situación). Cuando la BCL
  añada estos valores (.NET 9+), se reintroducirá la validación.
- Validación se aplica **una sola vez** (en el factory). Las
  operaciones aritméticas NO revalidan; confían en que las
  instancias vienen del factory.

### RF-4 — Operaciones aritméticas
- `Add(Money other) -> Money`: suma, requiere mismo `Currency`.
- `Subtract(Money other) -> Money`: resta, requiere mismo `Currency`.
  El resultado se valida: si `< 0`, lanza excepción.
- `Multiply(decimal scalar) -> Money`: multiplica `Amount` por
  escalar. El escalar debe ser `>= 0`; si es negativo, lanza
  excepción.

### RF-5 — Rechazo de mezcla de monedas
- Cualquier operación aritmética entre `Money` con distinto
  `Currency` lanza `InvalidOperationException` con mensaje:
  `"Cannot operate on Money with different currencies: {A} vs {B}."`

### RF-6 — `ToString` con cultura
- `ToString(CultureInfo culture)`: formatea `Amount` con el formato
  numérico `N2` de la cultura, seguido de un espacio y el código
  ISO de la moneda. Ejemplos:
  - es-DO: `"1,234.56 DOP"`
  - en-US: `"1,234.56 USD"`
- `ToString()` (sin parámetros): usa `CultureInfo.CurrentCulture`.
- Si `culture` es `null`, lanza `ArgumentNullException`.

### RF-7 — `Money.Zero`
- `static Money Zero(Currency currency)`: factory de `Money` con
  `Amount = 0` y el `Currency` dado. Útil como valor inicial de
  `Aggregate` (ver CU-2).

### RF-8 — Equality por valor
- `record` provee `Equals`, `GetHashCode`, `==` y `!=` por valor
  automáticamente.
- Dos `Money` con mismo `Amount` y mismo `Currency` son iguales
  aunque sean instancias diferentes.

---

## 5. Requisitos no funcionales

### RNF-1 — Pureza de Dominio
- `Money` y `Currency` no dependen de EF Core, Avalonia, ni de
  ningún paquete externo. Solo BCL (`System`, `System.Globalization`).
- Esto se valida con la regla de NetArchTest en F-007: `Domain` no
  puede tener `PackageReference`s.

### RNF-2 — Inmutabilidad thread-safe
- `record` con propiedades `init`-only garantiza que las instancias
  no se modifican tras creación. `Money` es thread-safe por
  construcción; no hay locks, no hay synchronized.

### RNF-3 — Cobertura de tests >= 95%
- Cobertura medida con `coverlet.collector` (configurado en
  `Directory.Packages.props` cuando se active en F-007+).
- Cubre: factory, validación, aritmética válida, mezcla de monedas,
  formato en múltiples culturas, equality.

### RNF-4 — Sin estado mutable global
- No hay cache, no hay singletons, no hay static state. Cada
  operación es determinista y pura.

### RNF-5 — Mensajes de error localizables (en código, no en runtime)
- Los mensajes de excepción están en **inglés** en el código (es
  convención de la comunidad .NET y de las librerías de Microsoft).
  La UI los traduce al español si lo necesita (F-012, i18n).

### RNF-6 — Cero alocaciones en operaciones aritméticas
- Las operaciones aritméticas (`Add`, `Subtract`, `Multiply`)
  devuelven una **nueva** instancia (semántica de value object).
  No hay boxing, no hay reflection, no hay alocaciones
  innecesarias. `decimal` es un struct, así que las copias son
  baratas.

---

## 6. Reglas de negocio

### RN-1 — `Amount` nunca es negativo
- En este modelo, los gastos, ingresos, deudas, presupuestos y
  metas se expresan como `Amount >= 0`. La representación de
  "devoluciones" o "ajustes negativos" es una entidad separada
  (ej. `Refund` o `Adjustment`) que se implementará en features
  futuras si el usuario lo pide.

### RN-2 — `Money.Zero` es un valor válido
- `Amount = 0` representa "este mes no gasté nada" o "este
  presupuesto está agotado" o "este es el valor inicial de una
  suma". Las operaciones aritméticas con cero funcionan
  normalmente.

### RN-3 — Mezcla de monedas es error de programador, no de usuario
- Si llega a una operación aritmética un `Money` con `Currency`
  distinto al otro, NO es un error de usuario sino de programador
  (la UI nunca debería permitir mezclar monedas en una suma). Por
  eso se lanza excepción tipada con mensaje claro, no se devuelve
  un error suave.

### RN-4 — La conversión entre monedas NO es dominio
- `Money` no sabe convertir entre DOP y USD. Eso requiere una
  tasa de cambio, que es dato externo, y pertenece a otra capa
  (Application o Infrastructure, con `IExchangeRateProvider`).
- `Money` es un tipo "tonto": solo sabe aritmética del mismo
  Currency.

### RN-5 — Redondeo al display, no al almacenar
- Las operaciones aritméticas devuelven `Amount` con la precisión
  completa de `decimal` (sin redondeo). El redondeo se aplica solo
  al formatear para mostrar al usuario, si el redondeo a 2
  decimales no coincide con la precisión de `decimal`.

---

## 7. Modelo de dominio

```
┌──────────────────────────┐
│  enum Currency           │
│  ──────────────          │
│  DOP = 0                │
│  USD = 1                │
└──────────────────────────┘
        ▲
        │ (referenced by)
        │
┌──────────────────────────────────────┐
│  record Money                         │
│  ──────────────────                   │
│  + Amount: decimal                    │
│  + Currency: Currency                 │
│  ──────────────────                   │
│  + Create(amount: decimal,            │
│           currency: Currency) : Money │
│  + Zero(currency: Currency) : Money   │
│  + Add(other: Money) : Money          │
│  + Subtract(other: Money) : Money     │
│  + Multiply(scalar: decimal) : Money  │
│  + ToString(culture: CultureInfo)     │
│  + ToString() : string                │
│  + Equals(other) : bool (record)      │
│  + GetHashCode() : int (record)       │
└──────────────────────────────────────┘
```

- `Money` es un value object: igualdad por valor (dos `Money` con
  mismo `Amount` y `Currency` son iguales aunque sean instancias
  distintas).
- `Money` no tiene identidad; es inmutable.
- `Currency` es un enum simple, comparable por valor.
- `Money` no se persiste directamente: las entidades (Expense, etc.)
  tendrán `Amount: Money` y la persistencia se cubre en Fase 2.

---

## 8. Validaciones

| # | Validación | Cuándo se aplica | Excepción |
| --- | --- | --- | --- |
| V-1 | `Amount >= 0` | En `Money.Create` | `ArgumentOutOfRangeException` |
| V-2 | `Amount` finito (no `NaN`, no `±Infinity`) | Aspiracional. En .NET 8.0.28 BCL es **vacuo** (ningún `decimal` puede tener esos valores). | `ArgumentException` (cuando la BCL lo permita) |
| V-3 | `other.Currency == this.Currency` | En `Add`, `Subtract` | `InvalidOperationException` |
| V-4 | `scalar >= 0` | En `Multiply` | `ArgumentOutOfRangeException` |
| V-5 | `result.Amount >= 0` (post-resta) | En `Subtract` (resultado) | `InvalidOperationException` |
| V-6 | `culture != null` | En `ToString(CultureInfo)` | `ArgumentNullException` |

---

## 9. Casos borde

### EC-1 — `Amount = decimal.MaxValue`
- Válido, `Money` lo acepta. La aritmética puede causar
  `OverflowException` si la suma excede el límite. Se documenta que
  el caller debe validar límites superiores si su dominio lo
  requiere (no es responsabilidad de `Money`).

### EC-2 — Suma que excede `decimal.MaxValue`
- `decimal` puede lanzar `OverflowException`. Aceptable: el caller
  lo trata. (No usamos `double` para evitar esto.)

### EC-3 — Suma con cero
- `Money.Zero(c).Add(Money.Zero(c))` devuelve `Money.Zero(c)`.
  Válido y útil como caso base de `Aggregate`.

### EC-4 — Resta que da cero
- `Money.Create(100, DOP).Subtract(Money.Create(100, DOP))` devuelve
  `Money.Zero(DOP)`. Válido.

### EC-5 — Resta que da negativo
- `Money.Create(50, DOP).Subtract(Money.Create(100, DOP))` lanza
  `InvalidOperationException` con mensaje: `"Subtraction would
  result in negative Money: 50 - 100 = -50 DOP."`. NO devuelve
  `Money` con `Amount` negativo.

### EC-6 — Multiplicación por cero
- `Money.Create(100, DOP).Multiply(0m)` devuelve `Money.Zero(DOP)`.
  Válido.

### EC-7 — Multiplicación por escalar negativo
- `Money.Create(100, DOP).Multiply(-1m)` lanza
  `ArgumentOutOfRangeException` con mensaje: `"Scalar must be
  non-negative: -1."`.

### EC-8 — Cultura nula
- `money.ToString(null as CultureInfo)` lanza
  `ArgumentNullException`.

### EC-9 — Cultura con separadores distintos
- `money.ToString(CultureInfo.GetCultureInfo("fr-FR"))` formatea
  con separadores franceses: `"1 234,56 DOP"`. La spec no exige
  validar cultura; cualquier `CultureInfo` válida es aceptada.

### EC-10 — `Currency` inválido (cast fuera del rango)
- `(Currency)999` no es un valor definido. La spec no exige
  validar (es defensa del caller), pero documenta que el enum es
  finito y un cast inválido es responsabilidad del caller.

### EC-11 — `Amount` muy pequeño
- `Money.Create(0.01m, DOP)` es válido. `decimal` mantiene
  precisión exacta incluso con muchos decimales (hasta 28).

### EC-12 — Resta con cero
- `Money.Create(0, DOP).Subtract(Money.Create(0, DOP))` devuelve
  `Money.Zero(DOP)`. Válido.

---

## 10. Dependencias

### 10.1 Prerrequisitos de implementación

- **F-001 completada** (T-001 a T-014, T-010a): la solución, los
  proyectos y la configuración global están listos. El proyecto
  `ControlDeFinanzasIA.Domain` existe y compila.

### 10.2 Dependencias con otras features

| Feature | Relación | Notas |
| --- | --- | --- |
| F-003 (DateRange) | Independiente | F-002 puede implementarse sin F-003. Pero F-002 es prerequisito lógico para los queries que filtran por DateRange y suman gastos. |
| F-004 (IClock, IPathResolver) | Independiente | F-002 no los necesita. |
| F-005 (Result<T>, PagedResult<T>) | Independiente | F-002 lanza excepción; usar `Result<Money>` en su lugar es decisión de F-005+. |
| F-006 (Interfaces de repositorio) | Independiente | Los repositorios futuros referenciarán `Money`, pero `Money` no depende de ellos. |
| F-007 (NetArchTest) | Verificación | F-007 valida que `Money` y `Currency` estén en `Domain` y que `Domain` no tenga `PackageReference`s. |
| F-002 Fase 2 (Persistencia) | Consumidora | Cómo persistir `Money` en EF Core se cubre en `spec/features/phase-02/F-002-persistence-efcore-sqlite.md`. |
| F-005 Fase 1 (Phase 1 F-005) | Verificación | F-002 se beneficia de F-005 (`Result<T>`) para reportar errores de validación sin excepciones, pero no la requiere. |

### 10.3 Bloqueos

- **Ninguno.** F-002 puede implementarse inmediatamente después de
  F-001.

---

## 11. Fuera de alcance (detallado)

Repetición con énfasis para evitar ambigüedad. Estos puntos NO se
implementan en F-002; se documentan para que cualquier reviewer o
implementador no los agregue por error.

- **Conversión entre monedas** (DOP ↔ USD, ni cualquier otra). Es
  una operación de **otra capa** que requiere una tasa de cambio
  (dato externo). Aquí NO.
- **Redondeo al display.** `decimal` mantiene precisión completa;
  el redondeo se aplica al formatear, no al operar. Aquí NO.
- **Display / formato visual** (colores, íconos, alineación).
  Responsabilidad de Avalonia. Aquí NO.
- **Persistencia.** Cómo guardar `Money` en SQLite/EF Core se cubre
  en F-002 de Fase 2. Aquí NO.
- **Localización exhaustiva.** `es-DO` y `en-US` están cubiertas.
  Otras culturas funcionarán con la API genérica pero no se
  prueban en esta feature. Aquí NO.
- **Operaciones avanzadas** (división, módulo, comparación
  `> <` ≥ ≤). Si se necesitan, se añaden como overloads en features
  futuras. Aquí NO.
- **Money como propiedad de cualquier entidad.** Las entidades
  (`Expense`, `Income`, etc.) se definen en features posteriores.
  Aquí NO.
- **Histórico de tasas de cambio.** Solo el modelo de datos del
  value object; las tasas en sí se obtienen de un proveedor
  externo. Aquí NO.

---

## 12. Criterios de aceptación

La feature se considera completada cuando **todos** los siguientes
puntos se cumplen, verificables mediante tests automatizados y
revisión de código.

### AC-1 — Factory y validación
- [ ] `Money.Create(100m, Currency.DOP)` devuelve un `Money` con
      `Amount=100`, `Currency=DOP`.
- [ ] `Money.Create(0m, Currency.DOP)` devuelve un `Money` con
      `Amount=0`, `Currency=DOP`. (Válido: representa cero.)
- [ ] `Money.Create(-1m, Currency.DOP)` lanza
      `ArgumentOutOfRangeException`.
- [ ] `Money.Create(decimal.NaN, Currency.DOP)` lanza
      `ArgumentException`. **(No testeable en .NET 8 — `decimal.NaN`
      no existe en la BCL 8.0.28. El test se reintroducirá cuando
      la BCL exponga el valor.)**
- [ ] `Money.Create(decimal.PositiveInfinity, Currency.DOP)` lanza
      `ArgumentException`. **(No testeable en .NET 8 — idem.)**
- [ ] `Money.Create(decimal.NegativeInfinity, Currency.DOP)` lanza
      `ArgumentException`. **(No testeable en .NET 8 — idem.)**
- [ ] `Money.Zero(Currency.USD)` devuelve un `Money` con
      `Amount=0`, `Currency=USD`.

### AC-2 — Aritmética válida
- [ ] `Money.Create(100, DOP).Add(Money.Create(50, DOP))` ==
      `Money(150, DOP)`.
- [ ] `Money.Create(100, DOP).Subtract(Money.Create(30, DOP))` ==
      `Money(70, DOP)`.
- [ ] `Money.Create(100, DOP).Subtract(Money.Create(100, DOP))` ==
      `Money.Zero(DOP)`.
- [ ] `Money.Create(100, DOP).Multiply(2m)` == `Money(200, DOP)`.
- [ ] `Money.Create(100, DOP).Multiply(0m)` == `Money.Zero(DOP)`.
- [ ] `Money.Zero(DOP).Add(Money.Zero(DOP))` == `Money.Zero(DOP)`.

### AC-3 — Rechazo de mezcla de monedas
- [ ] `Money.Create(100, DOP).Add(Money.Create(50, USD))` lanza
      `InvalidOperationException` con mensaje que menciona
      `"DOP"` y `"USD"`.
- [ ] `Money.Create(100, DOP).Subtract(Money.Create(50, USD))`
      lanza `InvalidOperationException`.
- [ ] `Money.Create(100, DOP).Multiply(2m)` no se ve afectado por
      mezcla (no requiere mismo Currency).

### AC-4 — Validación de escalares y resultados
- [ ] `Money.Create(100, DOP).Multiply(-1m)` lanza
      `ArgumentOutOfRangeException`.
- [ ] `Money.Create(50, DOP).Subtract(Money.Create(100, DOP))`
      lanza `InvalidOperationException` (resultado negativo).

### AC-5 — Formato
- [ ] `Money.Create(1234.56m, DOP).ToString(CultureInfo.GetCultureInfo("es-DO"))`
      produce `"1,234.56 DOP"`.
- [ ] `Money.Create(1234.56m, USD).ToString(CultureInfo.GetCultureInfo("en-US"))`
      produce `"1,234.56 USD"`.
- [ ] `Money.Create(0m, DOP).ToString(CultureInfo.GetCultureInfo("es-DO"))`
      produce `"0.00 DOP"`.
- [ ] `money.ToString(null as CultureInfo)` lanza
      `ArgumentNullException`.
- [ ] `money.ToString()` (sin parámetros) produce el mismo resultado
      que `money.ToString(CultureInfo.CurrentCulture)`.
- [ ] `Money.Create(1234.56m, DOP).ToString(CultureInfo.GetCultureInfo("fr-FR"))`
      produce formato con separadores franceses (ej. `"1 234,56 DOP"`)
      — solo verifica que NO usa separadores en-DO.

### AC-6 — Equality
- [ ] `Money.Create(100, DOP) == Money.Create(100, DOP)` (record
      equality).
- [ ] `Money.Create(100, DOP) != Money.Create(100, USD)`.
- [ ] `Money.Create(100, DOP) != Money.Create(200, DOP)`.
- [ ] `Money.Create(100, DOP).Equals(Money.Create(100, DOP))` es
      `true`.
- [ ] `Money.Create(100, DOP).GetHashCode() ==
      Money.Create(100, DOP).GetHashCode()`.

### AC-7 — Cobertura y calidad
- [ ] Cobertura de `Money` y `Currency` >= 95%.
- [ ] `dotnet build` desde la raíz: 0 warnings, 0 errors.
- [ ] `dotnet format style --verify-no-changes`: exit 0.
- [ ] `dotnet test` (proyecto `Tests.Unit`): todos los tests
      pasan, incluyendo los placeholders skipped.
- [ ] `Money.cs` y `Currency.cs` están en `src/ControlDeFinanzasIA.Domain/`.
- [ ] `Domain` sigue sin `PackageReference`s (verificable con
      `dotnet list package`).

### AC-8 — Convenciones del repo
- [ ] `Money` y `Currency` siguen el `.editorconfig` del proyecto
      (file-scoped namespaces, no `using` redundantes, naming
      PascalCase público, métodos `private` con prefijo `_camelCase`
      si los hay).
- [ ] `Money` y `Currency` son públicos (no internal ni private),
      porque la UI los va a consumir.
- [ ] No hay archivos `Class1.cs`, `Money1.cs`, ni placeholders
      similares.

---

## 13. Estructura de archivos esperada

```
src/
└── ControlDeFinanzasIA.Domain/
    ├── ControlDeFinanzasIA.Domain.csproj
    ├── Entities/
    │   └── (vacío; las entidades llegan en features posteriores)
    ├── ValueObjects/
    │   └── Money.cs                    ← nuevo
    ├── Enums/
    │   └── Currency.cs                 ← nuevo
    ├── DomainServices/
    │   └── (vacío; los servicios llegan con features posteriores)
    ├── Interfaces/
    │   └── (vacío; las interfaces llegan con F-004+)
    └── ...

tests/
└── ControlDeFinanzasIA.Tests.Unit/
    ├── F001PlaceholderTests.cs        ← se mantiene hasta que F-002 esté
    │                                     terminada (luego se elimina en T-013
    │                                     de la siguiente ronda, o aquí mismo
    │                                     si se decide)
    ├── ValueObjects/
    │   └── MoneyTests.cs               ← nuevo
    └── ...
```

---

## 14. Riesgos y mitigaciones

| Riesgo | Probabilidad | Impacto | Mitigación |
| --- | --- | --- | --- |
| Decisión `record` vs `readonly struct` | Baja | Bajo | La spec deja libertad; `record` (con primary constructor) es la elección recomendada por defecto por ergonomía y comparabilidad nativa. |
| Overflow en operaciones con `decimal.MaxValue` | Baja | Bajo | `decimal` puede lanzar `OverflowException`; se documenta que el caller lo trata si su dominio lo requiere. |
| `decimal` y cultura: coma vs punto decimal | Media | Bajo | Especificar formato con `CultureInfo` explícito; usar `CultureInfo.InvariantCulture` cuando se necesite determinismo (p. ej. en logs). |
| Mezcla accidental de monedas en código de aplicación | Media | Alto | Regla arquitectónica + tests de aceptación; la excepción es ruidosa y detectable. |
| `F-002 Phase 2` (Persistencia) redefine el value object | Baja | Alto | El header de la spec Phase 2 referencia esta spec; cualquier cambio aquí debe reflejarse allá. El contrato público de `Money` debe permanecer estable. |
| Tests no descubiertos porque la convención xunit requiere `[Fact]` y no `[Theory]` para casos parametrizados | Baja | Bajo | Usar `[Theory]` + `[InlineData]` para los casos parametrizados (es-DO, en-US, etc.) y `[Fact]` para los casos únicos. |

---

## 15. Notas sobre la implementación

Esta sección documenta **desviaciones conocidas** entre la spec
(aspiracional) y la implementación actual (limitada por la BCL
disponible). Se mantiene como referencia para cuando la BCL cambie.

### 15.1 Validación de finitud: aspiracional en la spec, vacua en la BCL

**Lo que dice la spec:** `Money.Create` debe rechazar `decimal.NaN`,
`decimal.PositiveInfinity` y `decimal.NegativeInfinity` con
`ArgumentException` (ver RF-3, V-2, AC-1).

**Lo que pasa en .NET 8.0.28 BCL:** estos valores no existen
construibles. Concretamente:

- `decimal.IsFinite` / `IsNaN` / `IsInfinity` — no existen como
  métodos públicos de la BCL 8.0.28.
- `decimal.NaN` / `PositiveInfinity` / `NegativeInfinity` — no
  existen como campos públicos estáticos de la BCL 8.0.28.
- `new decimal(int, int, int, bool, byte)` — el constructor
  rechaza explícitamente `scale > 28` con
  `ArgumentOutOfRangeException`, impidiendo construir un valor
  con los bits de "special value" que la representación interna
  reserva.

**Conclusión:** en .NET 8, ningún `decimal` puede ser
`NaN`/`+Inf`/`-Inf` a través de ninguna API pública. El chequeo
de finitud es **vacuo** (siempre retorna "es finito").

**Implementación actual (T-002):** el chequeo de finitud está
**omitido**. Solo se valida `< 0`. Un `<remarks>` en el XML doc
de `Money.Create` documenta la situación para futuros lectores.

**Cuando reintroducir el chequeo:** cuando la BCL exponga los
valores (esperado en .NET 9, donde la API ya está presente en las
versiones preview). En ese momento:
1. Reemplazar la omisión por `decimal.IsFinite(amount)` (o
   combinación de `IsNaN` / `IsInfinity`).
2. Añadir los 3 tests skipped en `MoneyTests.cs`:
   - `Create_WithNaN_ThrowsArgumentException`
   - `Create_WithPositiveInfinity_ThrowsArgumentException`
   - `Create_WithNegativeInfinity_ThrowsArgumentException`
3. Reactivar las 3 casillas marcadas como "No testeable" en AC-1.

**Si se quiere ser defensivo ya:** se puede añadir un chequeo
vía `decimal.GetBits(amount)[3]` que mire los bits reservados
del flag. En .NET 8 nunca disparará (los bits nunca se setean),
pero serviría como red de seguridad en .NET 9+ sin tener que
recordar actualizar la implementación. Trade-off: añade código
"muerto" hasta que .NET 9 sea el target.

### 15.2 Cómo verificar que la spec sigue vigente

Si en algún momento .NET 9+ pasa a ser el `TargetFramework` del
proyecto:
- Re-evaluar si el chequeo de finitud se reintroduce automáticamente
  (cuando .NET 9 sea el target, los métodos/constantes existen y el
  chequeo natural es viable).
- Actualizar este sección 15 para reflejar que la desviación ya
  no aplica.
- Actualizar la tabla de validaciones (V-2) para quitar la nota
  "aspiracional".

---

## 16. Referencias

- `docs/vision.md` §4.1 (transacciones) y §4.9 (IA, mención de
  multi-moneda).
- `docs/architecture.md` §6 (Dominio: entidades, value objects,
  servicios de dominio puros).
- `docs/roadmap.md` § Fase 1, F-002 dentro del desglose.
- `spec/constitution/tech-stack.md` — convenciones de código y
  reglas de paquetes.
- `spec/constitution/mission.md` — propósito del producto.
- `spec/phases/phase-01/features.md` — F-002 dentro del desglose
  de features de la fase 1.
- `spec/features/phase-01/F-001-skeleton-solution.done.md` — F-001
  completada, provee la base (proyecto `Domain` ya existe).
- `spec/features/phase-02/F-002-persistence-efcore-sqlite.md` —
  F-002 de **Fase 2** (persistencia), NO el mismo feature.
- `spec/README.md` — convención completa del directorio `spec/`.
