# Tareas — F-501: Multi-moneda — Provider stub (ExchangeRate + IExchangeRateProvider + StubExchangeRateProvider)

> **Spec:** `spec/features/phase-05/F-501-exchange-rate-stub.spec.md`
> **Fuentes de verdad:** la spec, `spec/phases/phase-05/features.md`
> (§RF-9, §RF-10, §9 features 5-007 + 5-008), `docs/vision.md` §8,
> `docs/roadmap.md` §Fase 5, `spec/constitution/tech-stack.md`.
>
> Cada tarea es implementable en menos de 2 horas. El proyecto debe
> seguir compilando al finalizar cada una. La numeración (T-001...) es
> local a esta feature.

---

## Resumen de tareas

| ID | Tarea | Estimación | Depende de |
| --- | --- | --- | --- |
| T-001 | Crear `ExchangeRate` VO + `CurrencyPair` record | XS | — |
| T-002 | Tests del VO (`ExchangeRateTests`) | S | T-001 |
| T-003 | Crear `IExchangeRateProvider` interface | XS | T-001 |
| T-004 | Crear `StubExchangeRateProvider` + `AddExchangeRates` extension | M | T-003 |
| T-005 | Tests del stub (`StubExchangeRateProviderTests`) + thread-safety | S | T-004 |
| T-006 | Verificación final: build, tests, format, arquitectónica, cierre | S | T-002, T-005 |

**Total estimado:** ~3-4 horas. T-004 es la más larga (lógica de
cache inmutable + configuración + coherencia inversa). T-001 y T-003
son triviales (1 archivo cada una, sin lógica). T-006 es solo
verificación.

---

## T-001 — Crear `ExchangeRate` VO + `CurrencyPair` record

**Objetivo**

Establecer el value object `ExchangeRate` (con invariantes duras y
operación `Convert`) y el record `CurrencyPair` (clave de
dictionary).

**Descripción**

### 1. `src/ControlDeFinanzasIA.Domain/ValueObjects/ExchangeRate.cs`

```csharp
using ControlDeFinanzasIA.Domain.Enums;

namespace ControlDeFinanzasIA.Domain.ValueObjects;

/// <summary>
/// Value object inmutable que representa una tasa de cambio entre
/// dos monedas en un instante de tiempo. Dos <see cref="ExchangeRate"/>
/// son iguales si tienen el mismo <see cref="From"/>, <see cref="To"/>,
/// <see cref="Rate"/>, <see cref="AsOf"/> y <see cref="Source"/>.
/// </summary>
/// <remarks>
/// <para>
/// No se puede construir directamente con `new`: el único camino
/// válido es a través de <see cref="Create"/>, que valida todas
/// las invariantes (RN-1, RN-2, RN-3).
/// </para>
/// <para>
/// La conversion entre monedas NO es responsabilidad de
/// <see cref="ExchangeRate"/> directamente (eso es del
/// <c>ICurrencyConverter</c> en feature 5-009). Aqui solo
/// proveemos <see cref="Convert"/> como primitiva aritmética.
/// </para>
/// </remarks>
public sealed record ExchangeRate
{
    public Currency From { get; }
    public Currency To { get; }
    public decimal Rate { get; }
    public DateTime AsOf { get; }
    public string Source { get; }

    private ExchangeRate(
        Currency from,
        Currency to,
        decimal rate,
        DateTime asOf,
        string source)
    {
        From = from;
        To = to;
        Rate = rate;
        AsOf = asOf;
        Source = source;
    }

    /// <summary>
    /// Crea un <see cref="ExchangeRate"/> validando las invariantes:
    /// <list type="bullet">
    ///   <item><description><see cref="From"/> != <see cref="To"/> (RN-3).</description></item>
    ///   <item><description><see cref="Rate"/> > 0 (RN-2).</description></item>
    ///   <item><description><see cref="AsOf"/>.Kind == Utc.</description></item>
    ///   <item><description><see cref="Source"/> no vacío ni whitespace.</description></item>
    /// </list>
    /// </summary>
    /// <param name="from">Moneda origen.</param>
    /// <param name="to">Moneda destino.</param>
    /// <param name="rate">Tasa de conversion (> 0).</param>
    /// <param name="asOf">Instante de la tasa (Utc).</param>
    /// <param name="source">Identificador del origen de la tasa.</param>
    /// <returns>Una instancia valida de <see cref="ExchangeRate"/>.</returns>
    /// <exception cref="ArgumentException">
    /// Si <paramref name="from"/> == <paramref name="to"/>,
    /// <paramref name="asOf"/>.Kind != Utc, o
    /// <paramref name="source"/> es vacio/whitespace.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Si <paramref name="rate"/> <= 0.
    /// </exception>
    public static ExchangeRate Create(
        Currency from,
        Currency to,
        decimal rate,
        DateTime asOf,
        string source)
    {
        if (from == to)
        {
            throw new ArgumentException(
                $"From and To must differ. Received: from={from}, to={to}.",
                nameof(from));
        }

        if (rate <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rate), rate, "Rate must be strictly positive.");
        }

        if (asOf.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                $"AsOf must be UTC. Received Kind={asOf.Kind}.",
                nameof(asOf));
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentException(
                "Source must be a non-empty string.",
                nameof(source));
        }

        return new ExchangeRate(from, to, rate, asOf, source);
    }

    /// <summary>
    /// Convierte <paramref name="amount"/> (asumido en
    /// <see cref="From"/>) a su equivalente en <see cref="To"/>,
    /// aplicando <see cref="Rate"/> con redondeo bancario a 4
    /// decimales (alineado con <c>Money.HasPrecision(18, 4)</c>).
    /// </summary>
    /// <param name="amount">Monto en <see cref="From"/>.</param>
    /// <returns>
    /// Monto equivalente en <see cref="To"/>, redondeado a 4
    /// decimales.
    /// </returns>
    /// <exception cref="OverflowException">
    /// Si <paramref name="amount"/> * <see cref="Rate"/> excede
    /// <see cref="decimal.MaxValue"/>.
    /// </exception>
    /// <remarks>
    /// No valida <paramref name="amount"/> (delega al caller; mismo
    /// patron que <c>Money.Multiply</c>).
    /// </remarks>
    public decimal Convert(decimal amount)
        => Math.Round(amount * Rate, 4, MidpointRounding.ToEven);
}
```

### 2. `src/ControlDeFinanzasIA.Application/Common/CurrencyPair.cs`

```csharp
using ControlDeFinanzasIA.Domain.Enums;

namespace ControlDeFinanzasIA.Application.Common;

/// <summary>
/// Par ordenado de monedas usado como clave en los diccionarios
/// de tasas del <c>IExchangeRateProvider</c>. Equality estructural
/// por valor (provisto por <c>record</c>).
/// </summary>
/// <param name="From">Moneda origen.</param>
/// <param name="To">Moneda destino.</param>
public sealed record CurrencyPair(Currency From, Currency To)
{
    /// <summary>
    /// Representacion textual para debugging y logs:
    /// <c>"USD->DOP"</c>.
    /// </summary>
    public override string ToString() => $"{From}->{To}";
}
```

**Dependencias**

- Phase 1 ✅: `Currency` enum (F-002), convención de VOs (F-002, F-003).

**Archivos / proyectos afectados**

- `src/ControlDeFinanzasIA.Domain/ValueObjects/ExchangeRate.cs` (nuevo).
- `src/ControlDeFinanzasIA.Application/Common/CurrencyPair.cs` (nuevo).

**Criterios de finalización**

- Ambos archivos existen.
- `dotnet build`: 0 warnings, 0 errors.
- XML doc en cada miembro público.
- File-scoped namespace.

---

## T-002 — Tests del VO (`ExchangeRateTests`)

**Objetivo**

Cubrir las invariantes de `ExchangeRate.Create` y el redondeo de
`Convert` con tests unitarios xUnit + FluentAssertions, siguiendo
el patrón de `MoneyTests` y `DateRangeTests`.

**Descripción**

Crear `tests/ControlDeFinanzasIA.Tests.Unit/ValueObjects/ExchangeRateTests.cs`
con los siguientes tests (mínimo):

| Test | Caso |
| --- | --- |
| `Create_WithValidInputs_ReturnsExchangeRateWithThoseValues` | Happy path |
| `Create_WithFromEqualsTo_ThrowsArgumentException` | INV: From != To |
| `Create_WithZeroRate_ThrowsArgumentOutOfRangeException` | INV: Rate > 0 |
| `Create_WithNegativeRate_ThrowsArgumentOutOfRangeException` | INV: Rate > 0 |
| `Create_WithLocalAsOf_ThrowsArgumentException` | INV: AsOf.Kind == Utc |
| `Create_WithUnspecifiedAsOf_ThrowsArgumentException` | INV: AsOf.Kind == Utc |
| `Create_WithEmptySource_ThrowsArgumentException` | INV: Source non-empty |
| `Create_WithWhitespaceSource_ThrowsArgumentException` | INV: Source non-empty |
| `Convert_WithSimpleAmount_ReturnsAmountTimesRate` | Happy path |
| `Convert_WithRoundingEdgeCase_BankersRoundToEven` | MidpointRounding.ToEven |
| `Convert_WithLargeAmount_ThrowsOverflowException` | decimal.MaxValue |

Estructura sugerida (alineada con `MoneyTests.cs`):

```csharp
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.ValueObjects;

public class ExchangeRateTests
{
    // === Create ===
    [Fact]
    public void Create_WithValidInputs_ReturnsExchangeRateWithThoseValues() { ... }

    [Fact]
    public void Create_WithFromEqualsTo_ThrowsArgumentException() { ... }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.01)]
    public void Create_WithNonPositiveRate_ThrowsArgumentOutOfRangeException(decimal rate) { ... }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void Create_WithNonUtcAsOf_ThrowsArgumentException(DateTimeKind kind) { ... }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithInvalidSource_ThrowsArgumentException(string? source) { ... }

    // === Convert ===
    [Fact]
    public void Convert_WithSimpleAmount_ReturnsAmountTimesRate() { ... }

    [Theory]
    [InlineData(100m, 60m, 6000.0000)]
    [InlineData(1.005m, 60m, 60.3000)]   // banker's: 1.005 * 60 = 60.3 exacto
    [InlineData(1.015m, 60m, 60.9000)]   // banker's: 60.9 exacto
    public void Convert_WithVariousAmounts_ReturnsExpected(decimal amount, decimal rate, decimal expected) { ... }

    [Fact]
    public void Convert_WithMaxAmount_ThrowsOverflowException() { ... }
}
```

**Dependencias**

- T-001.

**Archivos / proyectos afectados**

- `tests/.../Unit/ValueObjects/ExchangeRateTests.cs` (nuevo).

**Criterios de finalización**

- Archivo existe con ≥ 11 tests.
- `dotnet test --filter "FullyQualifiedName~ExchangeRateTests"`: todos
  verdes.
- Sin `Skip`, sin `[Ignore]`.

---

## T-003 — Crear `IExchangeRateProvider` interface

**Objetivo**

Definir el contrato de abstracción entre la capa de aplicación y la
fuente de tasas de cambio.

**Descripción**

Crear `src/ControlDeFinanzasIA.Application/Abstractions/IExchangeRateProvider.cs`:

```csharp
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Application.Abstractions;

/// <summary>
/// Abstraccion sobre una fuente de tasas de cambio. La
/// implementacion por defecto en Fase 5 es
/// <c>StubExchangeRateProvider</c> (tasas fijas locales). Las
/// features 5-009+ consumiran este contrato sin acoplarse a una
/// API externa.
/// </summary>
/// <remarks>
/// <para>
/// El adapter real (OpenExchangeRates, exchangerate.host, banco
/// central RD) se difiere a Fase 6+. Cuando llegue, se enchufa
/// implementando esta misma interface.
/// </para>
/// <para>
/// <b>Thread-safety:</b> las implementaciones deben ser
/// thread-safe (se asume llamada concurrente desde la UI).
/// </para>
/// </remarks>
public interface IExchangeRateProvider
{
    /// <summary>
    /// Nombre del proveedor, util para logging y debugging
    /// (ej. <c>"stub"</c>, <c>"openexchangerates"</c>).
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Obtiene la tasa vigente de <paramref name="from"/> a
    /// <paramref name="to"/>.
    /// </summary>
    /// <param name="from">Moneda origen.</param>
    /// <param name="to">Moneda destino.</param>
    /// <param name="ct">Token de cancelacion opcional.</param>
    /// <returns>
    /// <see cref="ExchangeRate"/> vigente. Si <paramref name="from"/>
    /// == <paramref name="to"/>, retorna una tasa identidad
    /// (<c>Rate = 1m</c>, <c>Source = "identity"</c>) sin
    /// consultar la fuente.
    /// </returns>
    Task<ExchangeRate> GetRateAsync(
        Currency from,
        Currency to,
        CancellationToken ct = default);

    /// <summary>
    /// Obtiene todas las tasas disponibles en un solo batch.
    /// Util para popular dropdowns o validar conversiones
    /// multiples en una sola llamada a la fuente.
    /// </summary>
    /// <param name="ct">Token de cancelacion opcional.</param>
    /// <returns>
    /// Dictionary inmutable con todas las tasas vigentes. En Fase
    /// 5 son 2 pares (USD<->DOP en ambas direcciones).
    /// </returns>
    Task<IReadOnlyDictionary<CurrencyPair, decimal>> GetLatestRatesAsync(
        CancellationToken ct = default);
}
```

**Dependencias**

- T-001 (`ExchangeRate`, `CurrencyPair`).

**Archivos / proyectos afectados**

- `src/ControlDeFinanzasIA.Application/Abstractions/IExchangeRateProvider.cs`
  (nuevo).

**Criterios de finalización**

- Interface existe.
- `dotnet build`: 0 warnings, 0 errors.
- XML doc en la interface y en cada miembro público.
- File-scoped namespace.
- Convenciones de F-006 (interfaces en `Abstractions/`).

---

## T-004 — Crear `StubExchangeRateProvider` + `AddExchangeRates` extension

**Objetivo**

Primera implementación de `IExchangeRateProvider`: tasas fijas
configurables, thread-safe vía cache inmutable en constructor.

**Descripción**

### 1. `src/ControlDeFinanzasIA.Infrastructure/ExchangeRates/StubExchangeRateProvider.cs`

```csharp
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Application.Abstractions;   // IClock
using Microsoft.Extensions.Configuration;

namespace ControlDeFinanzasIA.Infrastructure.ExchangeRates;

/// <summary>
/// Implementacion stub de <see cref="IExchangeRateProvider"/>:
/// devuelve tasas fijas configurables (default
/// <c>1 USD = 60 DOP</c>). Pensada para development offline y
/// para mantener la app operativa mientras se decide el provider
/// real (Fase 6+).
/// </summary>
/// <remarks>
/// <para>
/// <b>No es una fuente de verdad.</b> Las tasas hardcodeadas son
/// un placeholder. Cuando se enchufe el provider real, este stub
/// queda relegado a tests.
/// </para>
/// <para>
/// <b>Thread-safety:</b> el constructor precomputa un dictionary
/// inmutable de tasas; los metodos de instancia solo leen de ese
/// dictionary (sellado). No hay estado mutable despues del
/// constructor, por lo que el objeto es intrinsecamente
/// thread-safe.
/// </para>
/// <para>
/// <b>Configuracion:</b> lee <c>ExchangeRates:UsdToDop</c> de
/// <see cref="IConfiguration"/>. Default: <c>60.0m</c>. Si la
/// config es invalida (negativa o cero), cae al default sin
/// throw para no bloquear el arranque.
/// </para>
/// </remarks>
public sealed class StubExchangeRateProvider : IExchangeRateProvider
{
    private const decimal DefaultUsdToDop = 60.0m;
    private const string Source = "stub-fixed";
    private const string IdentitySource = "identity";

    private readonly IClock _clock;
    private readonly IReadOnlyDictionary<CurrencyPair, ExchangeRate> _rates;

    /// <inheritdoc />
    public string ProviderName => "stub";

    public StubExchangeRateProvider(IConfiguration configuration, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;

        decimal usdToDop = configuration.GetValue<decimal?>("ExchangeRates:UsdToDop")
            ?? DefaultUsdToDop;

        if (usdToDop <= 0)
        {
            // Config invalida -> default + no throw (RN-4 del slice).
            usdToDop = DefaultUsdToDop;
        }

        decimal dopToUsd = Math.Round(
            1m / usdToDop, 4, MidpointRounding.ToEven);

        DateTime asOf = _clock.UtcNow;

        _rates = new Dictionary<CurrencyPair, ExchangeRate>
        {
            [new CurrencyPair(Currency.USD, Currency.DOP)] =
                ExchangeRate.Create(Currency.USD, Currency.DOP, usdToDop, asOf, Source),
            [new CurrencyPair(Currency.DOP, Currency.USD)] =
                ExchangeRate.Create(Currency.DOP, Currency.USD, dopToUsd, asOf, Source),
        };
    }

    /// <inheritdoc />
    public Task<ExchangeRate> GetRateAsync(
        Currency from,
        Currency to,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (from == to)
        {
            return Task.FromResult(ExchangeRate.Create(
                from, to, 1m, _clock.UtcNow, IdentitySource));
        }

        CurrencyPair key = new(from, to);
        if (!_rates.TryGetValue(key, out ExchangeRate? rate))
        {
            throw new InvalidOperationException(
                $"No rate available for {key}. Supported pairs: USD<->DOP.");
        }

        return Task.FromResult(rate);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<CurrencyPair, decimal>> GetLatestRatesAsync(
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        IReadOnlyDictionary<CurrencyPair, decimal> result = _rates
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Rate);

        return Task.FromResult(result);
    }
}
```

### 2. `src/ControlDeFinanzasIA.Infrastructure/ExchangeRates/ExchangeRateServiceCollectionExtensions.cs`

```csharp
using ControlDeFinanzasIA.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ControlDeFinanzasIA.Infrastructure.ExchangeRates;

/// <summary>
/// Extension methods para registrar los servicios de tasas de
/// cambio en el contenedor de DI.
/// </summary>
public static class ExchangeRateServiceCollectionExtensions
{
    /// <summary>
    /// Registra <see cref="IExchangeRateProvider"/> como
    /// <see cref="StubExchangeRateProvider"/> (Singleton).
    /// </summary>
    /// <param name="services">Coleccion de servicios de DI.</param>
    /// <param name="configuration">
    /// Configuracion desde la que el stub lee las tasas. Si es
    /// <see langword="null"/>, el stub usa defaults.
    /// </param>
    /// <returns>La misma <see cref="IServiceCollection"/> para encadenar.</returns>
    public static IServiceCollection AddExchangeRates(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configuration is not null)
        {
            services.AddSingleton<IConfiguration>(configuration);
        }

        services.AddSingleton<IExchangeRateProvider, StubExchangeRateProvider>();

        return services;
    }
}
```

**Dependencias**

- T-003 (`IExchangeRateProvider`).
- Phase 1 ✅: `IClock`, `IConfiguration`.

**Archivos / proyectos afectados**

- `src/.../Infrastructure/ExchangeRates/StubExchangeRateProvider.cs` (nuevo).
- `src/.../Infrastructure/ExchangeRates/ExchangeRateServiceCollectionExtensions.cs`
  (nuevo).

**Criterios de finalización**

- Ambos archivos existen.
- `dotnet build`: 0 warnings, 0 errors.
- XML doc completo.
- File-scoped namespace.
- `AddExchangeRates` registra como Singleton.
- `StubExchangeRateProvider` es `sealed`.
- Thread-safe verificado por inspección (estado inmutable tras constructor).

---

## T-005 — Tests del stub + thread-safety

**Objetivo**

Cubrir el comportamiento del `StubExchangeRateProvider` con tests
unitarios, incluyendo un test concurrente que verifica
thread-safety.

**Descripción**

Crear `tests/ControlDeFinanzasIA.Tests.Unit/Infrastructure/ExchangeRates/StubExchangeRateProviderTests.cs`:

```csharp
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Infrastructure.ExchangeRates;

public class StubExchangeRateProviderTests
{
    private static IConfiguration BuildConfig(decimal? usdToDop = null)
    {
        var dict = new Dictionary<string, string?>();
        if (usdToDop.HasValue)
        {
            dict["ExchangeRates:UsdToDop"] = usdToDop.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    private static StubExchangeRateProvider BuildProvider(decimal? usdToDop = null)
        => new(BuildConfig(usdToDop), new FakeClock(new DateTime(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc)));

    // === ProviderName ===
    [Fact]
    public void ProviderName_ReturnsStub() { ... }

    // === Default config ===
    [Fact]
    public async Task GetRateAsync_UsdToDop_WithoutConfig_Returns60() { ... }

    [Fact]
    public async Task GetRateAsync_DopToUsd_WithoutConfig_ReturnsApproximately1Over60() { ... }

    // === Custom config ===
    [Fact]
    public async Task GetRateAsync_UsdToDop_WithCustomConfig_ReturnsConfigured() { ... }

    // === Inverse coherence ===
    [Fact]
    public async Task GetRateAsync_InverseCoherence_ProductIsApproximately1() { ... }

    // === Identity ===
    [Fact]
    public async Task GetRateAsync_SameCurrency_ReturnsRate1() { ... }

    // === GetLatestRates ===
    [Fact]
    public async Task GetLatestRatesAsync_ReturnsBothPairs() { ... }

    // === Thread-safety ===
    [Fact]
    public async Task GetRateAsync_Concurrent_ReturnsSameRate()
    {
        var provider = BuildProvider(usdToDop: 60m);
        var tasks = Enumerable.Range(0, 100)
            .Select(_ => provider.GetRateAsync(Currency.USD, Currency.DOP))
            .ToArray();
        var results = await Task.WhenAll(tasks);
        results.Should().AllSatisfy(r => r.Rate.Should().Be(60m));
    }

    // === Invalid config ===
    [Fact]
    public async Task GetRateAsync_WithNegativeConfig_FallsBackToDefault() { ... }

    [Fact]
    public async Task GetRateAsync_WithZeroConfig_FallsBackToDefault() { ... }
}
```

Nota: `FakeClock` ya existe en
`tests/.../Unit/TestDoubles/FakeClock.cs` (usado por tests de
`Expense`).

**Dependencias**

- T-004.

**Archivos / proyectos afectados**

- `tests/.../Unit/Infrastructure/ExchangeRates/StubExchangeRateProviderTests.cs`
  (nuevo).

**Criterios de finalización**

- Archivo existe con ≥ 10 tests.
- `dotnet test --filter "FullyQualifiedName~StubExchangeRateProviderTests"`:
  todos verdes.
- Test concurrente pasa consistentemente (correr 3 veces, todas
  verdes).

---

## T-006 — Verificación final y cierre

**Objetivo**

Validar que F-501 cumple los criterios de aceptación de la spec
(sección 14), verificar la compatibilidad arquitectónica, y dejar
el repo en estado de "F-501 cerrada".

**Descripción**

Esta tarea **no introduce código nuevo**. Ejecuta los pasos de
verificación y las acciones de cierre.

### Pasos de verificación

1. **Compilación:** `dotnet build -c Debug && dotnet build -c Release` —
   ambos con 0 warnings, 0 errors.
2. **Format check:** `dotnet format style --verify-no-changes` —
   exit 0.
3. **Tests completos:** `dotnet test` — 470 anteriores + ~21 nuevos
   = ~491 verdes, 0 failed.
4. **Compatibilidad arquitectónica:**
   - `grep -r "using ControlDeFinanzasIA.Infrastructure" src/.../Application`
     sigue retornando 0 líneas.
   - `grep -r "using ControlDeFinanzasIA.Application.Abstractions"
     src/.../Domain` sigue retornando 0 líneas.
   - `grep -r "using ControlDeFinanzasIA.Infrastructure" src/.../Domain`
     sigue retornando 0 líneas.
   - `dotnet list src/.../Application package` no añade EF Core.
   - ArchitectureTests (F-007 R-1..R-5) verdes.
5. **Cero impacto en código existente:** `git diff` solo muestra los 7
   archivos nuevos del slice.

### Pasos de limpieza

6. **Marcar el spec como completada:** actualizar
   `spec/features/phase-05/F-501-exchange-rate-stub.spec.md` línea
   de estado a "✅ COMPLETADA (fecha)".
7. **Renombrar la spec:** mover a `.done.md`.

### Resumen de verificación de AC

| AC | Criterio | Estado |
| --- | --- | --- |
| AC-1 | Archivos nuevos compilan | 5 archivos, build 0 errors |
| AC-2 | Tests verdes | ~21 nuevos, todos verdes |
| AC-3 | Convenciones del repo | File-scoped namespace, XML doc, naming |
| AC-4 | Compatibilidad arquitectónica | grep clean, NetArchTest verde |
| AC-5 | Cero impacto en existente | git diff solo muestra archivos nuevos |
| AC-6 | Verificación final | Build + format + tests + cierre |

**Dependencias**

- T-002, T-005.

**Archivos / proyectos afectados**

- `spec/features/phase-05/F-501-exchange-rate-stub.spec.md`
  (modificado: línea de estado).
- `spec/features/phase-05/F-501-exchange-rate-stub.spec.md`
  (renombrado a `.done.md`).

**Criterios de finalización**

- `dotnet build -c Debug && -c Release`: clean.
- `dotnet format style --verify-no-changes`: exit 0.
- `dotnet test`: todos verdes (~491).
- `git diff` solo muestra archivos nuevos.
- `grep` arquitectónicos: 0 matches.
- Spec marcada como completada y renombrada.

---

## Orden recomendado y trabajo en paralelo

**Orden secuencial estricto:** T-001 → T-002 → T-003 → T-004 → T-005 → T-006.

- T-001 es trivial (2 archivos cortos).
- T-002 depende de T-001 (necesita el VO para testearlo).
- T-003 depende de T-001 (usa `ExchangeRate` y `CurrencyPair`).
- T-004 depende de T-003 (implementa la interface).
- T-005 depende de T-004 (testea el stub).
- T-006 depende de T-002 y T-005 (verificación global).

Si hay 2 personas, T-002 y T-003 se pueden hacer en paralelo
después de T-001 (tests del VO vs. interface). Pero el cuello de
botella es T-005 (test concurrente del stub), no las otras.

---

## Riesgos de la fase de tareas

| Riesgo | Probabilidad | Impacto | Mitigación |
| --- | --- | --- | --- |
| `decimal.IsFinite` no existe en .NET 8 BCL | Alta | Bajo | Documentar en XML doc; verificar manualmente que ningún decimal válido es NaN/Infinity |
| Test concurrente flaky por timing | Baja | Bajo | Usar `Task.WhenAll` con N fijo; verificar valor, no timing |
| Modificación accidental a archivos existentes | Baja | Alto | AC-5 lo verifica con `git diff` |
| Tasas inversas incoherentes | Baja | Medio | Test dedicado `InverseCoherence_ProductIsApproximately1` |
| Configuración inválida bloquea arranque | Baja | Alto | Stub cae a default + no throw |
| El usuario quiere cambiar el alcance del slice | Baja | Bajo | El spec ya está firmado por el usuario vía question; cambios requieren nuevo spec |

---

## Referencias

- `spec/features/phase-05/F-501-exchange-rate-stub.spec.md` — spec completa.
- `spec/phases/phase-05/features.md` §RF-9 (ExchangeRate), §RF-10 (Stub),
  §9 (5-007 + 5-008).
- `docs/vision.md` §8 (display-time conversion).
- `spec/features/phase-01/F-002-money-value-object.done.md` — patrón VO.
- `spec/features/phase-01/F-004-Abstracciones-clock-pathresolver.done.md` — IClock.
- `spec/features/phase-01/F-006-contrato-interfaces-repo.done.md` — convención interfaces.
