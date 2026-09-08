# Tareas — F-502: CurrencyConverter (envoltorio de IExchangeRateProvider con cache TTL)

> **Spec:** `spec/features/phase-05/F-502-currency-converter.spec.md`
> **Fuentes de verdad:** la spec, `spec/phases/phase-05/features.md`
> (§RF-11, §CU-5, §9 feature 5-009), F-501 spec (`IExchangeRateProvider`),
> F-005 spec (`Result` non-generic), F-006 spec (`Task<Result>` para
> commands).
>
> Cada tarea es implementable en menos de 2 horas. El proyecto debe
> seguir compilando al finalizar cada una. La numeración (T-001...) es
> local a esta feature.

---

## Resumen de tareas

| ID | Tarea | Estimación | Depende de |
| --- | --- | --- | --- |
| T-001 | Agregar `Microsoft.Extensions.Caching.Memory` (CPM + csproj) | XS | — |
| T-002 | Crear `MoneyDisplay` record + `ICurrencyConverter` interface | XS | — |
| T-003 | Crear `CurrencyConverter` implementación | M | T-001, T-002 |
| T-004 | Actualizar `AddExchangeRates` para registrar converter + memory cache | XS | T-003 |
| T-005 | Tests unitarios (`CurrencyConverterTests` + `FakeExchangeRateProvider`) | M | T-003, T-004 |
| T-006 | Verificación final: build, tests, format, arquitectónica, cierre | S | T-005 |

**Total estimado:** ~3-4 horas. T-003 es la más larga (lógica de
cache + manejo de errores + DI). T-005 es media (mocking del
provider con FakeExchangeRateProvider).

---

## T-001 — Agregar `Microsoft.Extensions.Caching.Memory` (CPM + csproj)

**Objetivo**

Agregar la dependencia de `IMemoryCache` al proyecto `Application`
vía Central Package Management (CPM), consistente con el resto del
repo.

**Descripción**

### 1. `Directory.Packages.props`

Agregar (o verificar que ya está) la versión del package:

```xml
<PackageVersion Include="Microsoft.Extensions.Caching.Memory" Version="8.0.1" />
```

Verificar que es coherente con la versión de
`Microsoft.Extensions.Logging` (8.0.1) ya presente en el archivo.

### 2. `src/ControlDeFinanzasIA.Application/ControlDeFinanzasIA.Application.csproj`

Agregar:

```xml
<PackageReference Include="Microsoft.Extensions.Caching.Memory" />
```

(Sin `Version`, porque CPM resuelve la versión).

### Verificación

- `dotnet restore src/ControlDeFinanzasIA.Application` debe
  descargar el package.
- `dotnet list src/ControlDeFinanzasIA.Application package` debe
  mostrar la línea nueva.

**Dependencias**

- Ninguna.

**Archivos / proyectos afectados**

- `Directory.Packages.props` (modificado).
- `src/ControlDeFinanzasIA.Application/ControlDeFinanzasIA.Application.csproj`
  (modificado).

**Criterios de finalización**

- Package agregado al CPM con versión correcta.
- Project reference agregado.
- `dotnet restore` exit 0.
- `dotnet build` exit 0 (sin uso aún, pero la resolución debe
  funcionar).

---

## T-002 — Crear `MoneyDisplay` record + `ICurrencyConverter` interface

**Objetivo**

Establecer las abstracciones de la capa de aplicación.

**Descripción**

### 1. `src/ControlDeFinanzasIA.Application/Services/MoneyDisplay.cs`

```csharp
using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Application.Services;

/// <summary>
/// Representacion de un monto para mostrar en la UI: incluye el
/// monto original y, opcionalmente, su equivalente convertido a la
/// otra moneda disponible.
/// </summary>
/// <remarks>
/// <para>
/// Se usa para que la UI renderice listas de transacciones con una
/// segunda linea mostrando el equivalente en la otra moneda, sin
/// recalcular en cada bind.
/// </para>
/// <para>
/// <see cref="Converted"/> es <see langword="null"/> cuando la
/// conversion fallo (provider no disponible). La UI debe ocultar la
/// segunda linea en ese caso y mostrar un statusMessage global.
/// </para>
/// </remarks>
/// <param name="Original">Monto original (siempre presente).</param>
/// <param name="Converted">
/// Equivalente en la otra moneda, o <see langword="null"/> si la
/// conversion fallo.
/// </param>
/// <param name="ProviderName">
/// Nombre del proveedor de tasas (ej. <c>"stub"</c>,
/// <c>"openexchangerates"</c>). Util para tooltip de auditoria.
/// Puede ser <see langword="null"/> si no se llego a consultar al
/// provider (ej. <c>source.Currency == target</c>).
/// </param>
public sealed record MoneyDisplay(
    Money Original,
    Money? Converted,
    string? ProviderName);
```

### 2. `src/ControlDeFinanzasIA.Application/Services/ICurrencyConverter.cs`

```csharp
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Application.Services;

/// <summary>
/// Servicio de aplicacion que envuelve <see cref="IExchangeRateProvider"/>
/// con cache TTL y manejo defensivo de errores. Es la capa que la
/// UI y los use cases consumen para obtener montos convertidos.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por que en Application (no Infrastructure):</b> el converter
/// es logica de aplicacion pura (orquestacion de cache + provider +
/// logging). No accede a BD, archivos, ni red. Sigue el patron
/// "use case como orquestador" del Clean Architecture del repo.
/// </para>
/// <para>
/// <b>Errores como valores:</b> <see cref="ConvertAsync"/> retorna
/// <see cref="Result"/> con codigo <c>"ExchangeRateUnavailable"</c>
/// cuando el provider falla. <see cref="DisplayAsync"/> nunca falla
/// estructuralmente: retorna <see cref="MoneyDisplay"/> con
/// <c>Converted: null</c> en caso de fallo.
/// </para>
/// </remarks>
public interface ICurrencyConverter
{
    /// <summary>
    /// Convierte <paramref name="source"/> a la moneda
    /// <paramref name="target"/>.
    /// </summary>
    /// <param name="source">Monto a convertir.</param>
    /// <param name="target">Moneda destino.</param>
    /// <param name="ct">Token de cancelacion opcional.</param>
    /// <returns>
    /// <see cref="Result"/> con <c>IsSuccess = true</c> y
    /// <c>Value = Money</c> convertido en exito; o
    /// <c>Error.Code = "ExchangeRateUnavailable"</c> si el provider
    /// fallo. Si <paramref name="source"/>.Currency ==
    /// <paramref name="target"/>, retorna <paramref name="source"/>
    /// sin consultar cache ni provider.
    /// </returns>
    Task<Result> ConvertAsync(
        Money source,
        Currency target,
        CancellationToken ct = default);

    /// <summary>
    /// Obtiene la representacion para mostrar en la UI: monto
    /// original + equivalente en la otra moneda disponible.
    /// </summary>
    /// <param name="source">Monto original.</param>
    /// <param name="ct">Token de cancelacion opcional.</param>
    /// <returns>
    /// <see cref="MoneyDisplay"/> con <c>Converted</c> poblado en
    /// exito, o <see langword="null"/> si la conversion fallo. La
    /// direccion de la conversion es implicita: si source es USD,
    /// convierte a DOP; si source es DOP, convierte a USD.
    /// </returns>
    Task<MoneyDisplay> DisplayAsync(
        Money source,
        CancellationToken ct = default);
}
```

**Dependencias**

- F-501 ✅: `Money` (F-002), `Currency` enum (F-002), `Result`
  (F-005), `IExchangeRateProvider` (F-501).

**Archivos / proyectos afectados**

- `src/ControlDeFinanzasIA.Application/Services/MoneyDisplay.cs`
  (nuevo).
- `src/ControlDeFinanzasIA.Application/Services/ICurrencyConverter.cs`
  (nuevo).

**Criterios de finalización**

- Ambos archivos existen.
- `dotnet build`: 0 warnings, 0 errors.
- XML doc completo.
- File-scoped namespace.

---

## T-003 — Crear `CurrencyConverter` implementación

**Objetivo**

Implementar la lógica de cache + provider + manejo de errores.

**Descripción**

Crear `src/ControlDeFinanzasIA.Application/Services/CurrencyConverter.cs`:

```csharp
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Application.Services;

/// <inheritdoc cref="ICurrencyConverter"/>
public sealed class CurrencyConverter : ICurrencyConverter
{
    private const int DefaultCacheTtlMinutes = 60;
    private const string CacheKeyPrefix = "exchange-rate";
    private const string ErrorCodeUnavailable = "ExchangeRateUnavailable";
    private const string ErrorCodeInvalidRate = "InvalidRate";

    private readonly IExchangeRateProvider _provider;
    private readonly IMemoryCache _cache;
    private readonly IClock _clock;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CurrencyConverter> _logger;
    private readonly TimeSpan _cacheTtl;

    public CurrencyConverter(
        IExchangeRateProvider provider,
        IMemoryCache cache,
        IClock clock,
        IConfiguration configuration,
        ILogger<CurrencyConverter> logger)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);

        _provider = provider;
        _cache = cache;
        _clock = clock;
        _configuration = configuration;
        _logger = logger;

        int ttlMinutes = configuration.GetValue<int?>(
            "ExchangeRates:CacheTtlMinutes") ?? DefaultCacheTtlMinutes;
        if (ttlMinutes <= 0)
        {
            _logger.LogInformation(
                "Invalid ExchangeRates:CacheTtlMinutes ({Ttl}); using default {Default}.",
                ttlMinutes, DefaultCacheTtlMinutes);
            ttlMinutes = DefaultCacheTtlMinutes;
        }
        _cacheTtl = TimeSpan.FromMinutes(ttlMinutes);
    }

    /// <inheritdoc />
    public async Task<Result> ConvertAsync(
        Money source, Currency target, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Currency == target)
        {
            return Result.Success(source);
        }

        ExchangeRate rate;
        string cacheKey = BuildCacheKey(source.Currency, target);

        if (_cache.TryGetValue(cacheKey, out ExchangeRate? cached) && cached is not null)
        {
            rate = cached;
        }
        else
        {
            try
            {
                rate = await _provider.GetRateAsync(source.Currency, target, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Exchange rate provider {Provider} failed for {From}->{To}.",
                    _provider.ProviderName, source.Currency, target);
                return Result.Failure(ErrorCodeUnavailable,
                    $"Provider threw: {ex.GetType().Name}: {ex.Message}");
            }

            if (rate.Rate <= 0)
            {
                return Result.Failure(ErrorCodeInvalidRate,
                    $"Provider returned non-positive rate: {rate.Rate}.");
            }

            _cache.Set(cacheKey, rate, _cacheTtl);
        }

        decimal convertedAmount = rate.Convert(source.Amount);
        Money converted = Money.Create(convertedAmount, target);
        return Result.Success(converted);
    }

    /// <inheritdoc />
    public async Task<MoneyDisplay> DisplayAsync(
        Money source, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        Currency target = source.Currency == Currency.USD
            ? Currency.DOP
            : Currency.USD;

        Result result = await ConvertAsync(source, target, ct).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            Money converted = (Money)result.Value!;
            return new MoneyDisplay(source, converted, _provider.ProviderName);
        }

        return new MoneyDisplay(source, Converted: null, _provider.ProviderName);
    }

    private static string BuildCacheKey(Currency from, Currency to)
        => $"{CacheKeyPrefix}:{from}:{to}";
}
```

**Dependencias**

- T-001 (`IMemoryCache`), T-002 (`ICurrencyConverter`, `MoneyDisplay`),
  F-501 (`IExchangeRateProvider`), F-002 (`Money.Create`).

**Archivos / proyectos afectados**

- `src/ControlDeFinanzasIA.Application/Services/CurrencyConverter.cs`
  (nuevo).

**Criterios de finalización**

- Archivo existe.
- `dotnet build`: 0 warnings, 0 errors.
- XML doc en clase + métodos públicos (via `<inheritdoc />`).
- Constructor defensivo (null checks).
- `ConvertAsync` retorna `Task<Result>` (no `Task<Money>`).
- Same-currency no-op antes de cache lookup.
- Provider exception → `Result.Failure("ExchangeRateUnavailable", ...)`.

---

## T-004 — Actualizar `AddExchangeRates` para registrar converter + memory cache

**Objetivo**

Hacer que `ICurrencyConverter` se resuelva por DI.

**Descripción**

Modificar `src/ControlDeFinanzasIA.Infrastructure/ExchangeRates/ExchangeRateServiceCollectionExtensions.cs`:

```csharp
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Services;
using Microsoft.Extensions.Caching.Memory;
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
    /// <see cref="StubExchangeRateProvider"/> (Singleton) e
    /// <see cref="ICurrencyConverter"/> como
    /// <see cref="CurrencyConverter"/> (Singleton).
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

        services.AddMemoryCache();
        services.AddSingleton<IExchangeRateProvider, StubExchangeRateProvider>();
        services.AddSingleton<ICurrencyConverter, CurrencyConverter>();

        return services;
    }
}
```

**Dependencias**

- T-003 (`CurrencyConverter`).

**Archivos / proyectos afectados**

- `src/ControlDeFinanzasIA.Infrastructure/ExchangeRates/ExchangeRateServiceCollectionExtensions.cs`
  (modificado: agregar `using`, agregar 2 líneas en el método).

**Criterios de finalización**

- `dotnet build`: 0 warnings, 0 errors.
- `services.AddMemoryCache()` antes de
  `services.AddSingleton<ICurrencyConverter, CurrencyConverter>()`.

---

## T-005 — Tests unitarios (`CurrencyConverterTests` + `FakeExchangeRateProvider`)

**Objetivo**

Cubrir los caminos críticos del converter.

**Descripción**

### 1. `tests/ControlDeFinanzasIA.Tests.Unit/Application/Services/TestDoubles/FakeExchangeRateProvider.cs`

```csharp
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Tests.Unit.Application.Services.TestDoubles;

/// <summary>
/// Test double de <see cref="IExchangeRateProvider"/> que permite
/// inyectar tasas controladas o simular errores del provider.
/// </summary>
public sealed class FakeExchangeRateProvider : IExchangeRateProvider
{
    private readonly Func<Currency, Currency, ExchangeRate>? _onGetRate;
    private readonly Exception? _throwOnGetRate;
    public int CallCount { get; private set; }
    public string ProviderName { get; }

    public FakeExchangeRateProvider(
        Func<Currency, Currency, ExchangeRate> onGetRate,
        string providerName = "fake")
    {
        _onGetRate = onGetRate;
        ProviderName = providerName;
    }

    public FakeExchangeRateProvider(
        Exception throwOnGetRate,
        string providerName = "fake-throwing")
    {
        _throwOnGetRate = throwOnGetRate;
        ProviderName = providerName;
    }

    public Task<ExchangeRate> GetRateAsync(
        Currency from, Currency to, CancellationToken ct = default)
    {
        CallCount++;
        ct.ThrowIfCancellationRequested();
        if (_throwOnGetRate is not null)
        {
            throw _throwOnGetRate;
        }
        return Task.FromResult(_onGetRate!(from, to));
    }

    public Task<IReadOnlyDictionary<CurrencyPair, decimal>> GetLatestRatesAsync(
        CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyDictionary<CurrencyPair, decimal>>(
            new Dictionary<CurrencyPair, decimal>());
    }
}
```

### 2. `tests/ControlDeFinanzasIA.Tests.Unit/Application/Services/CurrencyConverterTests.cs`

```csharp
using ControlDeFinanzasIA.Application.Services;
using ControlDeFinanzasIA.Application.Services.TestDoubles;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.Services;

public class CurrencyConverterTests
{
    private static IMemoryCache BuildCache()
        => new MemoryCache(new MemoryCacheOptions());

    private static IConfiguration BuildConfig(int? ttlMinutes = null)
    {
        var dict = new Dictionary<string, string?>();
        if (ttlMinutes.HasValue)
        {
            dict["ExchangeRates:CacheTtlMinutes"] = ttlMinutes.Value.ToString();
        }
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    private static CurrencyConverter BuildConverter(
        IExchangeRateProvider provider,
        int? ttlMinutes = null)
        => new(provider, BuildCache(), new FakeClock(), BuildConfig(ttlMinutes), NullLogger<CurrencyConverter>.Instance);

    [Fact]
    public async Task ConvertAsync_SameCurrency_ReturnsSameMoneyWithoutConsultingProvider()
    {
        var provider = new FakeExchangeRateProvider((f, t) =>
            throw new InvalidOperationException("Provider should not be called."));
        var converter = BuildConverter(provider);
        Money source = Money.Create(100m, Currency.DOP);

        Result result = await converter.ConvertAsync(source, Currency.DOP);

        Assert.True(result.IsSuccess);
        Assert.Equal(source, result.Value);
        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public async Task ConvertAsync_DifferentCurrencies_ReturnsConvertedMoney()
    {
        var provider = new FakeExchangeRateProvider((f, t) =>
            ExchangeRate.Create(f, t, 60m,
                new DateTime(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc), "fake"));
        var converter = BuildConverter(provider);
        Money source = Money.Create(100m, Currency.USD);

        Result result = await converter.ConvertAsync(source, Currency.DOP);

        Assert.True(result.IsSuccess);
        Money converted = (Money)result.Value!;
        Assert.Equal(6000m, converted.Amount);
        Assert.Equal(Currency.DOP, converted.Currency);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public async Task ConvertAsync_ProviderThrows_ReturnsFailureWithExchangeRateUnavailableCode()
    {
        var provider = new FakeExchangeRateProvider(new HttpRequestException("network down"));
        var converter = BuildConverter(provider);
        Money source = Money.Create(100m, Currency.USD);

        Result result = await converter.ConvertAsync(source, Currency.DOP);

        Assert.False(result.IsSuccess);
        Assert.Equal("ExchangeRateUnavailable", result.Error.Code);
    }

    [Fact]
    public async Task ConvertAsync_CachedRate_ReturnsCachedValueWithoutHittingProvider()
    {
        var provider = new FakeExchangeRateProvider((f, t) =>
            ExchangeRate.Create(f, t, 60m, DateTime.UtcNow, "fake"));
        var converter = BuildConverter(provider);
        Money source = Money.Create(100m, Currency.USD);

        await converter.ConvertAsync(source, Currency.DOP);  // cache miss
        Result result2 = await converter.ConvertAsync(source, Currency.DOP);  // cache hit

        Assert.True(result2.IsSuccess);
        Assert.Equal(1, provider.CallCount);  // solo se llamo una vez
    }

    [Fact]
    public async Task DisplayAsync_UsdSource_ReturnsDisplayWithDopConverted()
    {
        var provider = new FakeExchangeRateProvider((f, t) =>
            ExchangeRate.Create(f, t, 60m, DateTime.UtcNow, "fake"));
        var converter = BuildConverter(provider);
        Money source = Money.Create(25m, Currency.USD);

        MoneyDisplay display = await converter.DisplayAsync(source);

        Assert.Equal(source, display.Original);
        Assert.NotNull(display.Converted);
        Assert.Equal(Currency.DOP, display.Converted!.Currency);
        Assert.Equal(1500m, display.Converted.Amount);
        Assert.Equal("fake", display.ProviderName);
    }

    [Fact]
    public async Task DisplayAsync_DopSource_ReturnsDisplayWithUsdConverted()
    {
        var provider = new FakeExchangeRateProvider((f, t) =>
            ExchangeRate.Create(f, t, 0.0167m, DateTime.UtcNow, "fake"));
        var converter = BuildConverter(provider);
        Money source = Money.Create(60m, Currency.DOP);

        MoneyDisplay display = await converter.DisplayAsync(source);

        Assert.NotNull(display.Converted);
        Assert.Equal(Currency.USD, display.Converted!.Currency);
        Assert.Equal(1.002m, display.Converted.Amount);  // 60 * 0.0167 = 1.002
    }

    [Fact]
    public async Task DisplayAsync_ProviderUnavailable_ReturnsDisplayWithNullConverted()
    {
        var provider = new FakeExchangeRateProvider(new HttpRequestException("net down"));
        var converter = BuildConverter(provider);
        Money source = Money.Create(100m, Currency.USD);

        MoneyDisplay display = await converter.DisplayAsync(source);

        Assert.Equal(source, display.Original);
        Assert.Null(display.Converted);
        Assert.Equal("fake-throwing", display.ProviderName);
    }

    [Fact]
    public async Task DisplayAsync_SameCurrency_ReturnsOriginalWithoutConversion()
    {
        var provider = new FakeExchangeRateProvider((f, t) =>
            throw new InvalidOperationException("Provider should not be called."));
        var converter = BuildConverter(provider);
        Money source = Money.Create(100m, Currency.DOP);

        MoneyDisplay display = await converter.DisplayAsync(source);

        Assert.Equal(source, display.Original);
        Assert.Null(display.Converted);
        Assert.Null(display.ProviderName);  // no provider consulted
    }
}
```

**Dependencias**

- T-003, T-004.

**Archivos / proyectos afectados**

- `tests/.../Unit/Application/Services/TestDoubles/FakeExchangeRateProvider.cs`
  (nuevo).
- `tests/.../Unit/Application/Services/CurrencyConverterTests.cs`
  (nuevo).

**Criterios de finalización**

- 8 tests, todos verdes.
- `dotnet test --filter "FullyQualifiedName~CurrencyConverterTests"`:
  todos verdes.
- Sin `Skip`, sin `[Ignore]`.

---

## T-006 — Verificación final y cierre

**Objetivo**

Validar que F-502 cumple los criterios de aceptación de la spec
(sección 14), verificar la compatibilidad arquitectónica, y dejar
el repo en estado de "F-502 cerrada".

**Descripción**

Esta tarea **no introduce código nuevo**. Ejecuta los pasos de
verificación y las acciones de cierre.

### Pasos de verificación

1. **Compilación:** `dotnet build -c Debug && dotnet build -c Release` —
   ambos con 0 warnings, 0 errors.
2. **Format check:** `dotnet format style --verify-no-changes` —
   exit 0.
3. **Tests completos:** `dotnet test` — 502 anteriores + ~8 nuevos
   = ~510 verdes, 0 failed.
4. **Compatibilidad arquitectónica:**
   - `grep -r "using ControlDeFinanzasIA.Infrastructure" src/.../Application`
     sigue retornando 0 líneas.
   - `grep -r "using Microsoft.EntityFrameworkCore" src/.../Application`
     sigue retornando 0 líneas.
   - `dotnet list src/.../Application package` muestra el nuevo
     `Microsoft.Extensions.Caching.Memory` y los pre-existentes.
   - ArchitectureTests (F-007 R-1..R-5) verdes.
5. **Modificaciones controladas (AC-5):**
   - `git diff --name-only` muestra los 4 archivos nuevos del
     slice + los 3 archivos modificados (csproj, Directory.Packages.props,
     ExchangeRateServiceCollectionExtensions.cs).
   - Ningún otro archivo pre-existente modificado.

### Pasos de limpieza

6. **Marcar el spec como completada:** actualizar
   `spec/features/phase-05/F-502-currency-converter.spec.md` línea
   de estado a "✅ COMPLETADA (fecha)".
7. **Renombrar la spec:** mover a `.done.md`.

### Resumen de verificación de AC

| AC | Criterio | Estado |
| --- | --- | --- |
| AC-1 | Archivos nuevos compilan | 3 archivos, build 0 errors |
| AC-2 | Tests verdes | ~8 nuevos, todos verdes |
| AC-3 | Convenciones del repo | File-scoped namespace, XML doc, naming |
| AC-4 | Compatibilidad arquitectónica | grep clean, NetArchTest verde |
| AC-5 | Modificaciones controladas | 4 nuevos + 3 modificados (csproj + DI + CPM), nada más |
| AC-6 | Verificación final | Build + format + tests + cierre |

**Dependencias**

- T-005.

**Archivos / proyectos afectados**

- `spec/features/phase-05/F-502-currency-converter.spec.md`
  (modificado: línea de estado).
- `spec/features/phase-05/F-502-currency-converter.spec.md`
  (renombrado a `.done.md`).

**Criterios de finalización**

- `dotnet build -c Debug && -c Release`: clean.
- `dotnet format style --verify-no-changes`: exit 0.
- `dotnet test`: todos verdes (~510).
- `git diff` muestra solo los archivos esperados.
- `grep` arquitectónicos: 0 matches.
- Spec marcada como completada y renombrada.

---

## Orden recomendado y trabajo en paralelo

**Orden secuencial estricto:** T-001 → T-002 → T-003 → T-004 → T-005 → T-006.

- T-001 (package) es trivial y se puede hacer en cualquier
  momento antes de T-003.
- T-002 (interfaces) es trivial y se puede hacer en paralelo con
  T-001.
- T-003 (implementación) requiere T-001 y T-002.
- T-004 (DI) requiere T-003.
- T-005 (tests) requiere T-003 y T-004.

---

## Riesgos de la fase de tareas

| Riesgo | Probabilidad | Impacto | Mitigación |
| --- | --- | --- | --- |
| `IMemoryCache.GetOrCreate` no es atómico bajo concurrencia | Media | Bajo | Usar `TryGetValue` + `Set` separados (más explícito); documentar en XML doc |
| Versión de `Microsoft.Extensions.Caching.Memory` no compatible con .NET 8 | Muy baja | Alto | Usar `8.0.1` (alineado con `Microsoft.Extensions.Logging`) |
| `Directory.Packages.props` ya tiene la entry duplicada | Baja | Bajo | Verificar antes de agregar |
| El usuario quiere cambiar el alcance del slice | Baja | Bajo | El spec ya está firmado por el usuario vía question |
| Tests flaky por timing del TTL | Baja | Bajo | No testear TTL expirado directamente (requeriría esperar minutos); documentar como limitación |

---

## Referencias

- `spec/features/phase-05/F-502-currency-converter.spec.md` — spec completa.
- `spec/phases/phase-05/features.md` §RF-11, §CU-5, §9 (5-009).
- `docs/vision.md` §8 (display-time conversion).
- `spec/features/phase-05/F-501-exchange-rate-stub.done.md` —
  `IExchangeRateProvider` (consumido por el converter).
- `spec/features/phase-01/F-005-comuntypes-result-pagedresult.done.md` —
  patrón `Result` non-generic.
- `spec/features/phase-01/F-006-contrato-interfaces-repo.done.md` —
  convención `Task<Result>` para commands.
- `tests/.../Unit/TestDoubles/FakeClock.cs` — patrón de test doubles.
- `tests/.../Unit/TestDoubles/FakeExpenseRepository.cs` — patrón de
  test doubles con state.
