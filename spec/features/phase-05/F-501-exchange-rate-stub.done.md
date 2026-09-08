# Specification — F-501: Multi-moneda — Provider stub (ExchangeRate + IExchangeRateProvider + StubExchangeRateProvider)

> **Fase:** 5 — Método de pago + multi-moneda DOP/USD
> **Feature:** F-501 (Phase 5, local a esta fase)
> **Estado:** ✅ COMPLETADA (2026-09-07)
> **Spec ID:** SPEC-F501
> **Cubre:** features 5-007 (ExchangeRate VO) + 5-008 (IExchangeRateProvider + StubExchangeRateProvider)
>
> **Nota de numeración:** este `F-501` es el primero del **slice stub**
> de Fase 5. Cubre solo el contrato de conversión y el stub local; no
> toca entidades, UI ni migraciones. Las features 5-001..5-006 (rama
> PaymentMethod), 5-009 (CurrencyConverter), 5-010..5-013 (UI/i18n/DI)
> se entregan en specs posteriores. Ver
> `spec/phases/phase-05/features.md` §9 para la descomposición completa.

---

## 1. Resumen

Este slice establece la **base de la conversión multi-moneda DOP/USD**
sin tocar nada del modelo de dominio existente ni de la UI. Entrega:

1. El value object `ExchangeRate` (con invariantes duras y una
   operación `Convert` consistente con `Money.HasPrecision(18, 4)`).
2. La abstracción `IExchangeRateProvider` (en `Application/`) que
   cualquier adaptador (stub, OpenExchangeRates, banco central RD,
   etc.) puede implementar.
3. Un `StubExchangeRateProvider` en `Infrastructure/` que devuelve
   tasas fijas configurables (default `1 USD = 60 DOP`), suficiente
   para el resto de Fase 5 y para development offline.

| Miembro | Tipo | Carpeta |
| --- | --- | --- |
| `ExchangeRate` | `sealed record` (VO) | `src/.../Domain/ValueObjects/` |
| `CurrencyPair` | `record` (clave de dictionary) | `src/.../Application/Common/` |
| `IExchangeRateProvider` | `interface` | `src/.../Application/Abstractions/` |
| `StubExchangeRateProvider` | `class` | `src/.../Infrastructure/ExchangeRates/` |
| `AddExchangeRates` | extension method | `src/.../Infrastructure/ExchangeRates/` |

> **Importante:** NO incluye `ICurrencyConverter` (feature 5-009).
> El converter es una capa encima del provider y entra en su propio
> slice.

---

## 2. Contexto

`docs/vision.md` §8 dice:

> *"Conversión de moneda se aplica en tiempo de display, no al
> guardar. El movimiento se almacena en su moneda original; los
> reportes convierten usando una tasa actualizada periódicamente
> desde una API."*

Fase 5 cierra esa brecha. Antes de poder mostrar la conversión, hace
falta un **contrato estable** entre la capa de aplicación y la fuente
de tasas. Sin ese contrato, las features 5-009 (converter), 5-011
(UI con segunda línea de conversión) y futuras (reportes Phase 8,
exportación Phase 18) acoplarían la UI a una API externa concreta.

Este slice entrega **solo el contrato + el stub** (cero dependencias
externas, todo local), de forma que cuando llegue el provider real
(Phase 6+), se enchufe implementando `IExchangeRateProvider` sin
tocar la UI ni los use cases.

---

## 3. Objetivo

Establecer la infraestructura base de conversión multi-moneda:
- `ExchangeRate` como VO reusable y testeable.
- `IExchangeRateProvider` como abstracción pura en Application.
- `StubExchangeRateProvider` como primera implementación, con tasas
  configurables y thread-safe.
- Tests unitarios que aseguren invariantes y coherencia inversa.

---

## 4. Alcance

### 4.1 En alcance

- **`ExchangeRate`** en `Domain/ValueObjects/ExchangeRate.cs`:
  `public sealed record ExchangeRate(Currency From, Currency To, decimal Rate, DateTime AsOf, string Source)`.
  - Factory `Create` que valida invariantes.
  - Método `Convert(decimal amount)` con redondeo bancario a 4
    decimales (`MidpointRounding.ToEven`).
- **`CurrencyPair`** en `Application/Common/CurrencyPair.cs`:
  `public sealed record CurrencyPair(Currency From, Currency To)` con
  igualdad estructural (para usarlo como key de `IReadOnlyDictionary`).
- **`IExchangeRateProvider`** en
  `Application/Abstractions/IExchangeRateProvider.cs`:
  - `string ProviderName { get; }`
  - `Task<ExchangeRate> GetRateAsync(Currency from, Currency to, CancellationToken ct = default)`
  - `Task<IReadOnlyDictionary<CurrencyPair, decimal>> GetLatestRatesAsync(CancellationToken ct = default)`
- **`StubExchangeRateProvider`** en
  `Infrastructure/ExchangeRates/StubExchangeRateProvider.cs`:
  - Lee `IConfiguration["ExchangeRates:UsdToDop"]` (default `60.0m`).
  - Deriva `DopToUsd = 1 / UsdToDop` con redondeo a 4 decimales
    (alineado con `Money.HasPrecision(18, 4)`).
  - `ProviderName = "stub"`.
  - `GetRateAsync` construye `ExchangeRate.Create(..., AsOf = _clock.UtcNow, Source = "stub-fixed")`.
  - `GetLatestRatesAsync` devuelve las 2 tasas (USD↔DOP, ambas
    direcciones) en un dictionary inmutable.
  - Thread-safe vía `IMemoryCache` (TTL largo, ej. `TimeSpan.FromHours(1)`)
    **o** un campo `IReadOnlyDictionary<CurrencyPair, ExchangeRate>`
    inmutable cacheado en el constructor; el spec deja la decisión al
    implementador pero **debe ser thread-safe** (test concurrente
    dedicado).
- **`AddExchangeRates`** extension method en
  `Infrastructure/ExchangeRates/ExchangeRateServiceCollectionExtensions.cs`:
  - Registra `IExchangeRateProvider` → `StubExchangeRateProvider` como
    **Singleton**.
  - Acepta opcionalmente un `IConfiguration` para que las tasas sean
    configurables vía `appsettings.json`.
- **Tests unitarios** (xUnit + FluentAssertions):
  - `ExchangeRateTests` en `tests/.../Unit/ValueObjects/` — INV-1 a
    INV-4 + `Convert` + casos borde.
  - `StubExchangeRateProviderTests` en `tests/.../Unit/Infrastructure/ExchangeRates/`
    — tasas por defecto, configuración custom, coherencia inversa,
    `ProviderName`, concurrencia (test que lanza N tasks y verifica
    consistencia).

### 4.2 Fuera de alcance (este slice)

- **`ICurrencyConverter` + `CurrencyConverter`** (feature 5-009).
  Viene después, como capa encima del provider.
- **Modificaciones a `Expense`, `Income`, `ExpenseConfiguration`,
  `IncomeConfiguration`, comandos, DTOs, filtros, validadores**
  (features 5-001 a 5-006). Cero impacto en este slice.
- **Migración EF Core `AddPaymentMethodIdToTransactions`** (feature
  5-002).
- **UI** (features 5-010, 5-011, 5-012).
- **DI registration en `Bootstrap.cs`** (feature 5-013). Aquí solo
  se crea la extension method `AddExchangeRates`; el wiring en
  Bootstrap es 5-013.
- **Provider real** llamando a una API externa.
- **Histórico de tasas por fecha** (vision §8). El stub siempre
  devuelve "tasa vigente" con `AsOf = clock.UtcNow`.

---

## 5. Casos de uso

### CU-1 — Crear un ExchangeRate válido

```csharp
var rate = ExchangeRate.Create(
    from: Currency.USD,
    to: Currency.DOP,
    rate: 60m,
    asOf: DateTime.UtcNow,
    source: "stub-fixed");

Assert.Equal(Currency.USD, rate.From);
Assert.Equal(60m, rate.Rate);
```

### CU-2 — Convertir un monto

```csharp
var rate = ExchangeRate.Create(Currency.USD, Currency.DOP, 60m, ..., "stub");
var converted = rate.Convert(100m);  // 6000.0000 (DOP)
Assert.Equal(6000.0000m, converted);
```

### CU-3 — Coherencia inversa del stub

```csharp
var stub = new StubExchangeRateProvider(usdToDop: 60m, clock: ..., config: ...);
var usdToDop = await stub.GetRateAsync(Currency.USD, Currency.DOP, ct);
var dopToUsd = await stub.GetRateAsync(Currency.DOP, Currency.USD, ct);

Assert.Equal(60m, usdToDop.Rate);
Assert.Equal(0.0167m, dopToUsd.Rate);   // 1/60 redondeado a 4 decimales
Assert.True(Math.Abs(usdToDop.Rate * dopToUsd.Rate - 1m) < 0.0001m);
```

### CU-4 — Configuración desde appsettings

```json
{
  "ExchangeRates": {
    "UsdToDop": 58.5
  }
}
```

→ `StubExchangeRateProvider` usa `58.5` como base, deriva
`1/58.5 = 0.0171` para DOP→USD.

### CU-5 — Thread-safety

`StubExchangeRateProvider.GetRateAsync` se llama desde N tasks
concurrentes (ej. UI renderizando 100 filas con conversión). El
provider debe devolver siempre la misma tasa coherente sin race
conditions.

---

## 6. Requisitos funcionales

### RF-1 — `ExchangeRate.Create` invariantes

- `From != To` (si son iguales, lanza `ArgumentException`).
- `Rate > 0` (lanza `ArgumentOutOfRangeException` si es ≤ 0).
- `Rate` es finito (validar con `decimal.IsFinite` si está
  disponible; si no, documentar el workaround — `.NET 8 BCL` no
  expone NaN/Infinity para `decimal`, ver F-002 §remarks de
  `Money.Create` para el patrón exacto).
- `AsOf.Kind == DateTimeKind.Utc` (lanza `ArgumentException` si es
  `Unspecified` o `Local`).
- `Source` no vacío ni whitespace (lanza `ArgumentException`).

### RF-2 — `ExchangeRate.Convert`

- `public decimal Convert(decimal amount) => Math.Round(amount * Rate, 4, MidpointRounding.ToEven);`
- No valida `amount` (delegado al caller — mismo patrón que
  `Money.Multiply`).
- Si `amount * Rate` excede `decimal.MaxValue`, lanza
  `OverflowException` (consistente con `Money.Add`).

### RF-3 — `CurrencyPair` equality

- `public sealed record CurrencyPair(Currency From, Currency To)`
- Equality estructural por valor (los `record` lo proveen gratis).
- ToString legible: `"USD→DOP"` para debugging.

### RF-4 — `IExchangeRateProvider` contrato

```csharp
public interface IExchangeRateProvider
{
    string ProviderName { get; }

    Task<ExchangeRate> GetRateAsync(
        Currency from,
        Currency to,
        CancellationToken ct = default);

    Task<IReadOnlyDictionary<CurrencyPair, decimal>> GetLatestRatesAsync(
        CancellationToken ct = default);
}
```

- `GetRateAsync(from, to)` con `from == to` retorna
  `ExchangeRate.Create(from, to, 1m, AsOf, source)` con `Source =
  "identity"` (sin consultar ninguna fuente).
- `GetLatestRatesAsync` retorna todas las tasas disponibles (en
  Phase 5, USD↔DOP en ambas direcciones = 2 pares).
- `CancellationToken` por defecto (alineado con F-006 §RF-20).

### RF-5 — `StubExchangeRateProvider` configuración

- Constructor:
  ```csharp
  public StubExchangeRateProvider(
      IConfiguration configuration,
      IClock clock)
  ```
- Lee `configuration["ExchangeRates:UsdToDop"]` (default `60.0m`).
- Cachea las 4 tasas derivadas (USD→DOP, DOP→USD) como
  `IReadOnlyDictionary<CurrencyPair, ExchangeRate>` en un campo
  inmutable, construido una sola vez en el constructor (thread-safe
  sin locks).
- `ProviderName => "stub"`.
- `GetRateAsync` lookup en el dictionary cacheado + sella `AsOf`
  con `_clock.UtcNow` y `Source = "stub-fixed"`.
- `GetLatestRatesAsync` proyecta el dictionary a
  `IReadOnlyDictionary<CurrencyPair, decimal>` con solo los Rates
  (no los ExchangeRate completos — el caller no necesita AsOf para
  el batch).

### RF-6 — `StubExchangeRateProvider` coherencia inversa

- `DopToUsd = Math.Round(1m / UsdToDop, 4, MidpointRounding.ToEven)`.
- Si `UsdToDop = 60`, `DopToUsd = 0.0167`.
- Test dedicado verifica que `UsdToDop * DopToUsd ≈ 1` dentro de
  tolerancia `0.0001`.

### RF-7 — `AddExchangeRates` extension

```csharp
public static IServiceCollection AddExchangeRates(
    this IServiceCollection services,
    IConfiguration? configuration = null)
{
    ArgumentNullException.ThrowIfNull(services);
    services.AddSingleton<IExchangeRateProvider, StubExchangeRateProvider>();
    return services;
}
```

- `StubExchangeRateProvider` resuelve `IConfiguration` y `IClock`
  por DI (no se pasan explícitamente).

### RF-8 — Thread-safety del stub

- `StubExchangeRateProvider` no mantiene estado mutable después del
  constructor.
- Test concurrente: 100 tasks llamando `GetRateAsync` en paralelo
  → todas retornan la misma `ExchangeRate` (mismo `Rate`).
- Si el implementador elige `IMemoryCache`, debe usar
  `MemoryCache` con `ConcurrentDictionary` internamente (built-in
  thread-safe).

---

## 7. Requisitos no funcionales

### RNF-1 — Build limpio

- `dotnet build -c Debug && -c Release`: 0 warnings, 0 errors.
- `dotnet format style --verify-no-changes`: exit 0.

### RNF-2 — Cobertura de tests

- `ExchangeRate.Create`: ≥ 8 tests (positivos + cada invariante +
  null/whitespace).
- `ExchangeRate.Convert`: ≥ 3 tests (happy path, redondeo,
  overflow).
- `StubExchangeRateProvider`: ≥ 6 tests (default, custom config,
  coherencia inversa, ProviderName, GetLatestRatesAsync shape,
  thread-safety).
- Total estimado: **+17 tests** sobre los 470 actuales = ~487.

### RNF-3 — Arquitectura R-1..R-5

- `Application` sigue sin ProjectReference a `Infrastructure`.
- `ExchangeRate` vive en `Domain` (sin dependencias hacia fuera).
- `IExchangeRateProvider` vive en `Application` (sin EF Core ni
  Infrastructure).
- `StubExchangeRateProvider` vive en `Infrastructure/ExchangeRates/`
  (nueva carpeta, no `Persistence/`).
- ArchitectureTests (F-007) sigue verde.

### RNF-4 — Compatibilidad backward

- Cero cambios en código existente (entidades, configs, repos,
  UI, Bootstrap). Si el build falla por cambios accidentales, el
  slice se considera fallido.

---

## 8. Reglas de negocio

### RN-1 — Redondeo bancario a 4 decimales

`ExchangeRate.Convert` usa `MidpointRounding.ToEven` con 4
decimales, alineado con `Money.HasPrecision(18, 4)`. Esto evita
sesgos sistemáticos por redondeo al alza o a la baja.

### RN-2 — Tasas siempre positivas

`Rate > 0` es invariante dura. Una tasa de 0 o negativa no tiene
sentido económico y siempre es bug o error de configuración.

### RN-3 — `From != To`

No se permite crear una `ExchangeRate` de una moneda a sí misma
vía `Create`. El caller que quiera la "tasa identidad" usa la
rama explícita en `IExchangeRateProvider.GetRateAsync` (retorna
`Rate = 1m`).

### RN-4 — Coherencia inversa del stub

El stub **debe** producir tasas inversas coherentes. Si el usuario
configura `UsdToDop = 60`, el stub **debe** devolver `DopToUsd ≈
0.0167`. No se permite que las dos direcciones sean
independientes (eso daría pie a arbitraje sin riesgo).

### RN-5 — Stub no es fuente de verdad

El stub es solo para development y para mantener la app
offline-capable. Cuando se enchufe el provider real (Phase 6+),
el stub queda relegado a tests o detrás de un feature flag. Esta
RN se documenta en el XML doc del `StubExchangeRateProvider`.

---

## 9. Modelo de dominio

```
Domain/ValueObjects/
  ExchangeRate
    + From: Currency
    + To: Currency
    + Rate: decimal
    + AsOf: DateTime
    + Source: string
    + Convert(amount): decimal
    + Create(from, to, rate, asOf, source): ExchangeRate

Application/Common/
  CurrencyPair
    + From: Currency
    + To: Currency

Application/Abstractions/
  IExchangeRateProvider
    + ProviderName: string
    + GetRateAsync(from, to, ct): Task<ExchangeRate>
    + GetLatestRatesAsync(ct): Task<IReadOnlyDictionary<CurrencyPair, decimal>>

Infrastructure/ExchangeRates/
  StubExchangeRateProvider : IExchangeRateProvider
    - _configuration: IConfiguration
    - _clock: IClock
    - _rates: IReadOnlyDictionary<CurrencyPair, ExchangeRate>  (cache inmutable)
    + ProviderName => "stub"
    + GetRateAsync(...)
    + GetLatestRatesAsync(...)
```

---

## 10. Validaciones

| # | Validación | Cuándo | Comportamiento |
| --- | --- | --- | --- |
| V-1 | `From != To` | `ExchangeRate.Create` | `ArgumentException` con detalle |
| V-2 | `Rate > 0` | `ExchangeRate.Create` | `ArgumentOutOfRangeException` |
| V-3 | `Rate` finito | `ExchangeRate.Create` | Documentar BCL .NET 8 (NaN/Infinity no aplican); si BCL cambia, reintroducir check |
| V-4 | `AsOf.Kind == Utc` | `ExchangeRate.Create` | `ArgumentException` |
| V-5 | `Source` no vacío | `ExchangeRate.Create` | `ArgumentException` |
| V-6 | `UsdToDop > 0` desde config | Constructor del stub | Si config inválido, default `60.0m` + log warning (NO throw — la app debe arrancar) |

---

## 11. Casos borde

### EC-1 — `GetRateAsync(from, to)` con `from == to`

Retorna `ExchangeRate.Create(from, to, 1m, AsOf, "identity")` sin
consultar config. Test dedicado.

### EC-2 — `UsdToDop` mal configurado (negativo o cero)

El stub cae al default `60.0m`. No lanza para no bloquear el
arranque de la app.

### EC-3 — Concurrencia: 100 tasks simultáneos

Todas retornan la misma `ExchangeRate` (cache inmutable).

### EC-4 — `AsOf` muy antiguo (cached o clock atrasado)

Aceptamos. `AsOf` es informativo; la lógica de "tasa muy vieja,
refresh" es responsabilidad del converter (5-009) o del caller.

### EC-5 — `Rate * amount` excede `decimal.MaxValue`

`ExchangeRate.Convert` lanza `OverflowException`. El caller lo
atrapa (patrón consistente con `Money.Add`).

---

## 12. Dependencias

### 12.1 Prerrequisitos (ya completados)

- Phase 1 ✅: `IClock` (F-004), `Money` value object (F-002),
  `IConfiguration` (F-001 vía Microsoft.Extensions.DependencyInjection).
- Phase 2 ✅: `IExpenseRepository`, `IIncomeRepository`, etc. (F-006).
- Phase 3 + 4 ✅: slice vertical de Expense + UI.

### 12.2 Consumidores futuros (NO en este slice)

- 5-009 (`CurrencyConverter`) usa `IExchangeRateProvider`.
- 5-011 (UI) usa `IExchangeRateProvider` indirectamente vía
  `ICurrencyConverter`.
- Phase 8 (dashboard) usa `ICurrencyConverter`.
- Phase 18 (export) usa `ICurrencyConverter`.

### 12.3 Bloqueos

- **Ninguno.** El slice es aditivo: solo crea archivos nuevos.

---

## 13. Fuera de alcance (detallado)

- **`ICurrencyConverter`** (5-009). El converter es la capa que
  cachea, falla con `Result.Failure("ExchangeRateUnavailable", ...)`
  y maneja misma-moneda. NO se incluye aquí.
- **Provider real** (cualquier API). NO se incluye.
- **Modificaciones al modelo de dominio existente.** Cero impacto
  en `Expense`, `Income`, `ExpenseConfiguration`, `IncomeConfiguration`.
- **`Bootstrap.cs` update.** Eso es 5-013.
- **UI, i18n, commands, validadores.** NO en este slice.
- **Tests de integración.** Solo unit tests.

---

## 14. Criterios de aceptación

### AC-1 — Archivos creados y compilando

- [ ] `Domain/ValueObjects/ExchangeRate.cs` existe y compila.
- [ ] `Application/Common/CurrencyPair.cs` existe y compila.
- [ ] `Application/Abstractions/IExchangeRateProvider.cs` existe y
      compila.
- [ ] `Infrastructure/ExchangeRates/StubExchangeRateProvider.cs`
      existe y compila.
- [ ] `Infrastructure/ExchangeRates/ExchangeRateServiceCollectionExtensions.cs`
      existe y compila.
- [ ] `dotnet build -c Debug && -c Release`: 0 warnings, 0 errors.

### AC-2 — Tests verdes

- [ ] `ExchangeRateTests` existe con ≥ 11 tests, todos verdes.
- [ ] `StubExchangeRateProviderTests` existe con ≥ 6 tests, todos
      verdes.
- [ ] `dotnet test`: 470 actuales + nuevos ≥ **487 verdes**,
      0 failed.

### AC-3 — Convenciones del repo

- [ ] File-scoped namespace en todos los archivos nuevos.
- [ ] XML doc en cada tipo público y cada miembro público.
- [ ] `var` cuando el tipo es aparente, explícito cuando no (alineado
      con `.editorconfig` y el estilo del repo).
- [ ] Sin `Co-Authored-By` ni trailers de IA en commits.

### AC-4 — Compatibilidad arquitectónica

- [ ] `grep -r "using ControlDeFinanzasIA.Infrastructure" src/.../Application`
      sigue retornando 0 líneas.
- [ ] `grep -r "using ControlDeFinanzasIA.Application.Abstractions"
      src/.../Domain` sigue retornando 0 líneas.
- [ ] `Application` no tiene nuevos `PackageReference`s.
- [ ] ArchitectureTests (F-007 R-1..R-5) verdes.

### AC-5 — Cero impacto en código existente

- [ ] `git diff` muestra **solo archivos nuevos** en este slice:
      `Domain/ValueObjects/ExchangeRate.cs`,
      `Application/Common/CurrencyPair.cs`,
      `Application/Abstractions/IExchangeRateProvider.cs`,
      `Infrastructure/ExchangeRates/StubExchangeRateProvider.cs`,
      `Infrastructure/ExchangeRates/ExchangeRateServiceCollectionExtensions.cs`,
      `tests/.../Unit/ValueObjects/ExchangeRateTests.cs`,
      `tests/.../Unit/Infrastructure/ExchangeRates/StubExchangeRateProviderTests.cs`.
- [ ] Ningún archivo pre-existente modificado.

### AC-6 — Verificación final

- [ ] `dotnet build -c Debug && -c Release`: clean.
- [ ] `dotnet format style --verify-no-changes`: exit 0.
- [ ] `dotnet test`: todos verdes.
- [ ] Spec renombrada a `.done.md` (en T-003 de las tasks).

---

## 15. Estructura de archivos esperada

```
src/
├── ControlDeFinanzasIA.Domain/
│   └── ValueObjects/
│       └── ExchangeRate.cs                          ← nuevo (F-501)
├── ControlDeFinanzasIA.Application/
│   ├── Abstractions/
│   │   └── IExchangeRateProvider.cs                 ← nuevo (F-501)
│   └── Common/
│       └── CurrencyPair.cs                          ← nuevo (F-501)
└── ControlDeFinanzasIA.Infrastructure/
    └── ExchangeRates/                               ← nueva carpeta
        ├── StubExchangeRateProvider.cs              ← nuevo (F-501)
        └── ExchangeRateServiceCollectionExtensions.cs ← nuevo (F-501)

tests/
└── ControlDeFinanzasIA.Tests.Unit/
    ├── ValueObjects/
    │   └── ExchangeRateTests.cs                     ← nuevo (F-501)
    └── Infrastructure/
        └── ExchangeRates/
            └── StubExchangeRateProviderTests.cs     ← nuevo (F-501)
```

Archivos **NO modificados** (AC-5):
- `Expense.cs`, `Income.cs`, `ExpenseConfiguration.cs`,
  `IncomeConfiguration.cs`, `AppDbContext.cs`, `Bootstrap.cs`,
  ningún DTO, ningún comando, ningún validador, ningún ViewModel,
  ninguna `.axaml`, ningún `.resx`.

---

## 16. Decisiones de diseño explícitas

### 16.1 — `ExchangeRate` con constructor público + factory

El record tiene un constructor público (necesario para que sea
`record` con `init`) **y** una factory `Create` que valida. Esto
se alinea con `Money.Create` y `DateRange.Create` (F-002, F-003).
El record se sella con `sealed` para evitar herencia no
controlada.

### 16.2 — `CurrencyPair` como `record` (no struct)

Es pequeño (2 enums = 8 bytes), pero se usa como key de
dictionary y se beneficia de `Equals`/`GetHashCode` autogenerados
del record. Struct requeriría implementación manual.

### 16.3 — `StubExchangeRateProvider` con cache inmutable en constructor

El constructor lee config, calcula las 4 tasas, y guarda un
`IReadOnlyDictionary<CurrencyPair, ExchangeRate>` **sellado**
como campo. Después del constructor, el objeto es funcionalmente
inmutable → thread-safe sin locks. Esto evita la complejidad de
`IMemoryCache` o `ConcurrentDictionary` para una cache que no
cambia en runtime.

**Trade-off:** si la config cambia en runtime (improbable, pero
posible en tests con `IConfiguration` reemplazable), el stub no
lo refleja hasta ser reconstruido. Esto es aceptable para un
stub y consistente con la decisión de registrarlo como
**Singleton** en DI.

### 16.4 — `GetLatestRatesAsync` retorna solo Rates, no ExchangeRates

El batch de tasas se usa típicamente para popular dropdowns o
validar conversiones múltiples. El caller no necesita el `AsOf`
por par (todos comparten el mismo `AsOf = clock.UtcNow` en el
momento del batch). Devolver solo `decimal` simplifica la API y
reduce alocaciones.

### 16.5 — Cero impacto en código existente

Este slice es puramente aditivo. Cualquier diff en archivos
pre-existentes indica error de implementación. AC-5 lo verifica
explícitamente.

---

## 17. Riesgos y mitigaciones

| Riesgo | Probabilidad | Impacto | Mitigación |
| --- | --- | --- | --- |
| `decimal.IsFinite` no existe en .NET 8 BCL | Alta | Bajo | Documentar en XML doc el workaround; verificar manualmente que ningún decimal válido es NaN/Infinity (mismo patrón que F-002 §remarks de Money) |
| Thread-safety rota si el implementador usa `Dictionary` mutable | Baja | Alto | Test concurrente dedicado; preferir cache inmutable en constructor |
| Tasas inversas incoherentes por bug de redondeo | Baja | Medio | Test dedicado que verifica `UsdToDop * DopToUsd ≈ 1` dentro de `0.0001` |
| `IConfiguration` mal configurado bloquea arranque | Baja | Alto | Default a `60.0m` + log warning (no throw) |
| Modificación accidental a archivos existentes | Baja | Alto | AC-5 lo verifica con `git diff` |
| El test concurrente es flaky por timing | Baja | Bajo | Usar `Task.WhenAll` con N fijo (100 tasks) y verificar igualdad de referencia o valor, no timing |

---

## 18. Referencias

- `spec/phases/phase-05/features.md` §RF-9 (ExchangeRate),
  §RF-10 (StubExchangeRateProvider), §9 (feature IDs 5-007, 5-008).
- `docs/vision.md` §8 (display-time conversion).
- `docs/roadmap.md` §Fase 5 (líneas 270-310).
- `spec/constitution/tech-stack.md` — convenciones de código.
- `spec/features/phase-01/F-002-money-value-object.done.md` —
  patrón de VO (`Money.Create`, redondeo bancario).
- `spec/features/phase-01/F-003-datarange-value-object.done.md` —
  patrón de VO.
- `spec/features/phase-01/F-004-Abstracciones-clock-pathresolver.done.md` —
  `IClock` (inyección en stub).
- `spec/features/phase-01/F-006-contrato-interfaces-repo.done.md` —
  convención de interfaces en `Application/Abstractions/`.
- `spec/features/phase-04/F-006-f-006-...` (Phase 4 features) —
  precedente de cómo se introdujo el `Money.Owned` mapping.
