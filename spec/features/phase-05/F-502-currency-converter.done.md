# Specification — F-502: CurrencyConverter (envoltorio de IExchangeRateProvider con cache TTL)

> **Fase:** 5 — Método de pago + multi-moneda DOP/USD
> **Feature:** F-502 (Phase 5, local a esta fase)
> **Estado:** ✅ COMPLETADA (2026-09-07)
> **Spec ID:** SPEC-F502
> **Cubre:** feature 5-009 (`ICurrencyConverter` + `CurrencyConverter`)
>
> **Dependencias:**
> - F-501 ✅ (5-007 `ExchangeRate` + 5-008 `IExchangeRateProvider` +
>   `StubExchangeRateProvider`).
>
> **Nota de numeración:** este `F-502` es el segundo slice de
> Fase 5. Continúa el contrato de conversión; agrega la capa de
> aplicación que el resto de Fase 5 (UI, commands, DTOs) consumirá.
> NO toca entidades, UI, ni migraciones.

---

## 1. Resumen

Este slice agrega el **servicio de aplicación** que envuelve
`IExchangeRateProvider` (F-501) con:

1. Cache en memoria con TTL configurable (default 1 hora) para no
   martillar al provider en cada render de lista.
2. Manejo defensivo de fallos del provider vía `Result.Failure` con
   código de error semántico (`ExchangeRateUnavailable`).
3. **Same-currency no-op**: convertir un `Money` a su propia moneda
   retorna el mismo `Money` sin consultar al provider.
4. API batch `DisplayAsync` para que la UI muestre "monto original +
   equivalente en la otra moneda" en una sola llamada.

| Miembro | Tipo | Carpeta |
| --- | --- | --- |
| `ICurrencyConverter` | `interface` | `src/.../Application/Services/` |
| `CurrencyConverter` | `sealed class` | `src/.../Application/Services/` |
| `MoneyDisplay` | `sealed record` | `src/.../Application/Services/` |

> **Nota arquitectónica:** `ICurrencyConverter` vive en `Application`,
> pero `CurrencyConverter` (la implementación) también vive en
> `Application` — no en `Infrastructure`. Razón: el converter es
> lógica de aplicación pura (orquestación + cache), sin acceso a
> infraestructura. Sigue el patrón "use case como orquestador" del
> Clean Architecture del repo.

---

## 2. Contexto

F-501 estableció el contrato `IExchangeRateProvider`. F-502 lo
envuelve con la lógica que el resto de Fase 5 necesita:

- **Cache TTL**: la UI de Phase 5+ puede llamar al converter en
  cada `RefreshAsync` (típicamente cada vez que cambia un filtro o
  se navega a una vista). Sin cache, cada llamada golpea al
  provider aunque la tasa no haya cambiado. `IMemoryCache` con TTL
  evita eso.
- **Errores como valores**: si el provider real falla (red caída,
  timeout, rate limit), el converter NO lanza excepción — devuelve
  `Result.Failure("ExchangeRateUnavailable", ...)`. Esto se alinea
  con F-005 (Result non-generic) y F-006 (commands devuelven
  `Task<Result>`).
- **Same-currency**: el caso "convertir DOP a DOP" debe ser trivial
  (no-op), sin consultar cache ni provider.
- **Display batch**: la UI lista transacciones y para cada una
  quiere mostrar "100.00 USD ≈ 6,000.00 DOP". Llamar a
  `ConvertAsync` por fila es ineficiente; `DisplayAsync` recibe un
  solo `Money` y devuelve su `MoneyDisplay` (original + convertido
  en la otra moneda + nombre del provider).

---

## 3. Objetivo

- Proveer `ICurrencyConverter` como abstracción de aplicación.
- Implementar `CurrencyConverter` con cache TTL + fallback
  defensivo + same-currency no-op.
- Exponer `MoneyDisplay` para que la UI muestre monto original +
  equivalente.
- Mantener `Application` libre de dependencias hacia `Infrastructure`
  (el converter solo conoce `IExchangeRateProvider`, una abstracción
  de Application).

---

## 4. Alcance

### 4.1 En alcance

- **`ICurrencyConverter`** en
  `src/.../Application/Services/ICurrencyConverter.cs`:
  ```csharp
  public interface ICurrencyConverter
  {
      Task<Result> ConvertAsync(
          Money source,
          Currency target,
          CancellationToken ct = default);

      Task<MoneyDisplay> DisplayAsync(
          Money source,
          CancellationToken ct = default);
  }
  ```
  - `ConvertAsync` retorna `Task<Result>` con `Value` = `Money`
    convertido (en éxito) o `Error` con código
    `"ExchangeRateUnavailable"` (en fallo). Patrón F-005/F-006.
  - `DisplayAsync` retorna `Task<MoneyDisplay>` directamente (no
    Result envuelto) — `MoneyDisplay.Converted` es nullable, lo
    que indica éxito/fallo de forma estructural sin necesidad de
    Result.

- **`MoneyDisplay`** en `src/.../Application/Services/MoneyDisplay.cs`:
  ```csharp
  public sealed record MoneyDisplay(
      Money Original,
      Money? Converted,
      string? ProviderName);
  ```
  - `Converted == null` ⇒ no se pudo convertir (provider no
    disponible). La UI oculta la segunda línea en ese caso.
  - `ProviderName` permite a la UI mostrar un tooltip con la fuente
    de la tasa (ej. `"stub"`, `"openexchangerates"`).

- **`CurrencyConverter`** en
  `src/.../Application/Services/CurrencyConverter.cs`:
  - Recibe `IExchangeRateProvider`, `IMemoryCache`, `IClock`,
    `IConfiguration`, `ILogger<CurrencyConverter>` por DI.
  - Cache key: `($"exchange-rate:{from}:{to}", asOfBucket)` donde
    `asOfBucket` se redondea al TTL (ej. `floor(now / ttl) * ttl`)
    para que expirar sea natural con TTL eviction de
    `IMemoryCache`.
  - TTL configurable:
    `IConfiguration["ExchangeRates:CacheTtlMinutes"]` (default
    `60` minutos). Documentar que valores `<= 0` se tratan como
    default.
  - Si `source.Currency == target`, retorna
    `Result.Success(source)` sin consultar cache ni provider.
  - En error del provider: log warning + retorna
    `Result.Failure("ExchangeRateUnavailable", "...")`. NO lanza.
  - `DisplayAsync`: si la moneda origen es USD, convierte a DOP;
    si es DOP, convierte a USD. Si la conversión falla, devuelve
    `MoneyDisplay(Original, Converted: null, ProviderName: provider.ProviderName)`.

- **DI registration** — actualizar
  `Infrastructure/ExchangeRates/ExchangeRateServiceCollectionExtensions.cs`:
  - Agregar `services.AddMemoryCache()` (si no está).
  - Agregar
    `services.AddSingleton<ICurrencyConverter, CurrencyConverter>()`.

- **Package addition** — agregar
  `Microsoft.Extensions.Caching.Memory` a `Application.csproj`
  (vía `Directory.Packages.props` con CPM, consistente con el
  resto del repo).

- **Tests unitarios** (xUnit + FluentAssertions **NO**, el proyecto
  usa `Assert` de xUnit — ver nota en §6):
  - `ConvertAsync_SameCurrency_ReturnsSameMoneyWithoutConsultingProvider`.
  - `ConvertAsync_DifferentCurrencies_ReturnsConvertedMoney`.
  - `ConvertAsync_ProviderThrows_ReturnsFailureWithExchangeRateUnavailableCode`.
  - `ConvertAsync_CachedRate_ReturnsCachedValueWithoutHittingProvider`.
  - `ConvertAsync_AfterTtl_ConsultsProviderAgain`.
  - `DisplayAsync_UsdSource_ReturnsDisplayWithDopConverted`.
  - `DisplayAsync_DopSource_ReturnsDisplayWithUsdConverted`.
  - `DisplayAsync_ProviderUnavailable_ReturnsDisplayWithNullConverted`.
  - Total: ~8 tests.

### 4.2 Fuera de alcance (este slice)

- **Modificaciones a `Expense`, `Income`, configs, comandos,
  validadores, DTOs, filtros, repositorios** (features 5-001 a
  5-006, 5-010, 5-011). Cero impacto.
- **Migración EF Core** (5-002).
- **UI, i18n, ViewModels, `.axaml`** (5-010, 5-011, 5-012).
- **`Bootstrap.cs` update** (5-013) — el converter se registra vía
  la extension `AddExchangeRates` ya existente (modificada en este
  slice), pero Bootstrap debe llamar a `AddExchangeRates` para que
  el wiring tome efecto. Ese wiring es 5-013.
- **Provider real** (Phase 6+).
- **Histórico de tasas por fecha** (vision §8). El cache TTL es
  por-tiempo-real, no por-fecha-de-transacción.

---

## 5. Casos de uso

### CU-1 — Convertir 100 USD a DOP (camino feliz)

1. UI llama `await converter.ConvertAsync(Money.Create(100m, Currency.USD), Currency.DOP, ct)`.
2. Converter detecta `source.Currency != target`.
3. Cache miss (primera llamada).
4. Llama `provider.GetRateAsync(USD, DOP, ct)` → `ExchangeRate { Rate = 60m }`.
5. Aplica `Money.Create(60 * 100m, DOP) = Money.Create(6000m, DOP)`.
6. Cachea la `ExchangeRate` en `IMemoryCache` con TTL.
7. Retorna `Result.Success(Money.Create(6000m, DOP))`.

### CU-2 — Convertir 100 USD a DOP (cache hit)

1. UI llama `ConvertAsync(...)` (segunda vez, dentro del TTL).
2. Cache hit.
3. NO consulta al provider.
4. Aplica la tasa cacheada.
5. Retorna `Result.Success(...)`.

### CU-3 — Convertir 100 USD a USD (same currency)

1. UI llama `ConvertAsync(Money.Create(100m, USD), USD, ct)`.
2. Converter detecta `source.Currency == target`.
3. Retorna `Result.Success(source)` sin consultar nada.

### CU-4 — Provider lanza (red caída)

1. UI llama `ConvertAsync(...)`.
2. Converter consulta provider.
3. Provider lanza `HttpRequestException` (o cualquier excepción).
4. Converter log warning + retorna
   `Result.Failure("ExchangeRateUnavailable", "Provider threw: ...")`
   sin rethrow.

### CU-5 — DisplayAsync para fila de lista

1. UI renderiza fila: `Money = Money.Create(25m, USD)`.
2. ViewModel llama `await converter.DisplayAsync(money, ct)`.
3. Converter consulta provider (USD→DOP).
4. Retorna
   `MoneyDisplay(Original: Money.Create(25m, USD), Converted: Money.Create(1500m, DOP), ProviderName: "stub")`.
5. UI muestra `"25.00 USD ≈ 1,500.00 DOP"`.

### CU-6 — DisplayAsync cuando provider falla

1. UI llama `DisplayAsync(money, ct)`.
2. Provider lanza.
3. Converter retorna
   `MoneyDisplay(Original: money, Converted: null, ProviderName: "stub")`.
4. UI oculta la segunda línea y muestra el statusMessage global
   `"Conversión no disponible temporalmente"` (esa clave i18n es
   5-012, fuera de scope).

---

## 6. Requisitos funcionales

### RF-1 — `ICurrencyConverter` contrato

```csharp
public interface ICurrencyConverter
{
    Task<Result> ConvertAsync(
        Money source,
        Currency target,
        CancellationToken ct = default);

    Task<MoneyDisplay> DisplayAsync(
        Money source,
        CancellationToken ct = default);
}
```

- **Decisión de diseño:** `ConvertAsync` retorna `Task<Result>`
  (no `Task<Money>` como sugería el draft original RF-11 de
  features.md). Razón: alineado con F-005 (`Result` non-generic con
  `Value: object?`) y F-006 (`Task<Result>` para commands). El
  caller hace `(Money)result.Value!` cuando `IsSuccess`.
- `DisplayAsync` retorna `Task<MoneyDisplay>` directamente
  (sin Result envuelto) porque `MoneyDisplay.Converted == null`
  ya expresa el fallo.

### RF-2 — `MoneyDisplay` shape

```csharp
public sealed record MoneyDisplay(
    Money Original,
    Money? Converted,
    string? ProviderName);
```

- Constructor compacto (record provee `public MoneyDisplay(...)`).
- `Converted` nullable: `null` cuando la conversión falló.
- `ProviderName` nullable: `null` cuando no se llegó a consultar
  al provider (ej. `source.Currency == target`).

### RF-3 — `CurrencyConverter.ConvertAsync` same-currency no-op

```csharp
public async Task<Result> ConvertAsync(
    Money source, Currency target, CancellationToken ct = default)
{
    ArgumentNullException.ThrowIfNull(source);

    if (source.Currency == target)
    {
        return Result.Success(source);
    }

    // ... resto: cache lookup -> provider call -> cache store
}
```

### RF-4 — Cache con TTL via `IMemoryCache`

- Cache key: `string` formado por `$"exchange-rate:{from}:{to}"`.
  El TTL de `IMemoryCache` se encarga de expirar; no se necesita
  bucket manual. La invalidación por cambio de provider es por
  TTL, no por evento.
- TTL configurable:
  ```csharp
  int ttlMinutes = configuration.GetValue<int?>(
      "ExchangeRates:CacheTtlMinutes") ?? 60;
  if (ttlMinutes <= 0) ttlMinutes = 60;
  var ttl = TimeSpan.FromMinutes(ttlMinutes);
  ```
- Cache value: el objeto `ExchangeRate` completo (incluye
  `AsOf`).
- Cache miss path:
  ```csharp
  var rate = await provider.GetRateAsync(from, to, ct);
  _cache.Set(cacheKey, rate, ttl);
  ```
- Cache hit path:
  ```csharp
  if (_cache.TryGetValue(cacheKey, out ExchangeRate? cached) && cached is not null)
  {
      rate = cached;
  }
  ```

### RF-5 — Manejo defensivo del provider

```csharp
ExchangeRate rate;
try
{
    rate = await provider.GetRateAsync(from, to, ct);
}
catch (Exception ex) when (ex is not OperationCanceledException)
{
    _logger.LogWarning(ex,
        "Exchange rate provider {Provider} failed for {From}->{To}",
        provider.ProviderName, from, to);
    return Result.Failure(
        "ExchangeRateUnavailable",
        $"Provider threw: {ex.GetType().Name}: {ex.Message}");
}
```

- `OperationCanceledException` se rethrow (el caller sabe que
  canceló).
- Cualquier otra excepción se captura y se reporta como
  `Result.Failure`.
- Log warning (NO error — es un fallo esperado, no un bug).

### RF-6 — `DisplayAsync` dirección implícita

```csharp
public async Task<MoneyDisplay> DisplayAsync(
    Money source, CancellationToken ct = default)
{
    ArgumentNullException.ThrowIfNull(source);

    Currency target = source.Currency == Currency.USD
        ? Currency.DOP
        : Currency.USD;

    Result result = await ConvertAsync(source, target, ct);
    if (result.IsSuccess)
    {
        var converted = (Money)result.Value!;
        return new MoneyDisplay(source, converted, _provider.ProviderName);
    }

    return new MoneyDisplay(source, Converted: null, ProviderName: _provider.ProviderName);
}
```

- Si la moneda origen NO es USD ni DOP (futuro EUR, etc.), cae al
  default `Currency.USD` como destino. Documentado en XML doc.

### RF-7 — DI registration

Actualizar `ExchangeRateServiceCollectionExtensions.AddExchangeRates`:

```csharp
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
```

- `CurrencyConverter` registrado como Singleton (el cache es
  por-instancia; si fuera Scoped, el cache sería por-request —
  ineficiente).
- `IMemoryCache` registrado como Singleton (built-in de
  `Microsoft.Extensions.DependencyInjection`).

### RF-8 — Logging

- `CurrencyConverter` recibe `ILogger<CurrencyConverter>` por DI.
- Log **Information** en cache miss (con provider name y pair).
- Log **Warning** en error del provider (con excepción).
- Log **Debug** en same-currency no-op (opcional, para diagnóstico).

---

## 7. Requisitos no funcionales

### RNF-1 — Build limpio

- `dotnet build -c Debug && -c Release`: 0 warnings, 0 errors.
- `dotnet format style --verify-no-changes`: exit 0.

### RNF-2 — Cobertura de tests

- ~8 tests nuevos en `CurrencyConverterTests`.
- Total estimado: **502 actuales + ~8 = ~510 tests verdes**.

### RNF-3 — Arquitectura R-1..R-5

- `Application` sigue sin ProjectReference a `Infrastructure`.
- `ICurrencyConverter` y `CurrencyConverter` viven en `Application`
  (no en `Infrastructure`).
- `IMemoryCache` viene de `Microsoft.Extensions.Caching.Memory`
  (Abstractions de Microsoft.Extensions, **no** de Infrastructure).
- ArchitectureTests (F-007) verdes.

### RNF-4 — Compatibilidad backward

- **Modificación permitida** (no viola AC-5 de F-501):
  - `Infrastructure/ExchangeRates/ExchangeRateServiceCollectionExtensions.cs`
    (agregar 2 líneas de DI).
- **Modificación permitida**:
  - `Application/ControlDeFinanzasIA.Application.csproj`
    (agregar `Microsoft.Extensions.Caching.Memory` package
    reference).
  - `Directory.Packages.props` (agregar la versión del package
    en CPM).
- **Sin modificación** a: entidades, configs EF Core,
  repositorios, comandos, validadores, DTOs, filtros, UI,
  migrations, Bootstrap.

---

## 8. Reglas de negocio

### RN-1 — Errores de provider son `Result.Failure`, no excepciones

El converter NUNCA re-lanza excepciones del provider (excepto
`OperationCanceledException`). El caller (UI, use case) debe
poder manejar el fallo del provider como un valor.

### RN-2 — Same-currency es siempre éxito trivial

`source.Currency == target` ⇒ retorno inmediato sin tocar cache ni
provider. La UI nunca debe ver una `Result.Failure` para este caso.

### RN-3 — Cache TTL configurable, default 60 minutos

Default conservador. Si la app está abierta 8 horas, se consultan
las tasas ~8 veces (vs. cada render). Para development, valores
más bajos (ej. `5` minutos) son aceptables.

### RN-4 — `DisplayAsync` siempre retorna `MoneyDisplay`, nunca falla

`DisplayAsync` no puede retornar `Result.Failure` — su contrato es
devolver un `MoneyDisplay` con `Converted: null` en caso de fallo.
Esto simplifica el binding de la UI (no necesita manejar Result).

### RN-5 — `ProviderName` se expone para auditoría

La UI puede mostrar un tooltip `"Source: stub"` o `"Source:
openexchangerates"`. Esto es transparencia para el usuario, no un
debug log interno.

---

## 9. Modelo de dominio

```
Application/Services/
  ICurrencyConverter
    + ConvertAsync(source, target, ct): Task<Result>
    + DisplayAsync(source, ct): Task<MoneyDisplay>

  MoneyDisplay (record)
    + Original: Money
    + Converted: Money?
    + ProviderName: string?

  CurrencyConverter : ICurrencyConverter
    - _provider: IExchangeRateProvider
    - _cache: IMemoryCache
    - _clock: IClock
    - _configuration: IConfiguration
    - _logger: ILogger<CurrencyConverter>
    + ConvertAsync(...)
    + DisplayAsync(...)
```

Dependencias de `CurrencyConverter`:
- `ControlDeFinanzasIA.Application.Abstractions.IExchangeRateProvider`
  (F-501).
- `ControlDeFinanzasIA.Application.Abstractions.IClock` (Phase 1).
- `Microsoft.Extensions.Caching.Memory.IMemoryCache` (nuevo en F-502).
- `Microsoft.Extensions.Configuration.IConfiguration` (Phase 1).
- `Microsoft.Extensions.Logging.ILogger<CurrencyConverter>` (Phase 1).

---

## 10. Validaciones

| # | Validación | Cuándo | Comportamiento |
| --- | --- | --- | --- |
| V-1 | `source != null` | `ConvertAsync`, `DisplayAsync` | `ArgumentNullException` |
| V-2 | `source.Currency == target` | `ConvertAsync` | No-op, retorna `source` |
| V-3 | Provider lanza | `ConvertAsync` | `Result.Failure("ExchangeRateUnavailable", ...)` |
| V-4 | `OperationCanceledException` | `ConvertAsync` | Rethrow (caller sabe) |
| V-5 | `CacheTtlMinutes <= 0` en config | Constructor | Default 60 + log info |
| V-6 | Provider retorna `ExchangeRate` con `Rate <= 0` | `ConvertAsync` | Esto NO debería pasar (invariant del VO); defensivo: `Result.Failure("InvalidRate", ...)` |

---

## 11. Casos borde

### EC-1 — `source.Currency` no es USD ni DOP (futuro EUR)

`DisplayAsync` cae a `Currency.USD` como destino. Test con un enum
"fake" no es posible (Currency es sealed en Fase 5); documentar
en XML doc.

### EC-2 — TTL de cache = 0 (config inválida)

Cae al default 60. Cache sigue funcionando.

### EC-3 — Provider tarda mucho (no cancela)

`ConvertAsync` espera. El `CancellationToken` del caller puede
abortar. Si el provider no respeta el CT, la llamada puede
bloquear; documentar como limitación.

### EC-4 — Cache miss concurrente (100 tasks, mismo par)

`IMemoryCache` no es atómico en `GetOrCreate`. La implementación
puede consultar al provider N veces en la primera ráfaga, pero
después del TTL las llamadas siguientes son cache hits. Aceptable
para v1 (la ráfaga inicial solo ocurre tras arrancar la app).

### EC-5 — `MoneyDisplay.Converted` con moneda distinta a la del `Original`

Garantía: si `Converted != null`, entonces
`Converted.Currency == (Original.Currency == USD ? DOP : USD)`.
Test dedicado.

### EC-6 — `provider.GetRateAsync` retorna misma moneda (identity)

El stub retorna `Rate = 1m` cuando `from == to`. `ConvertAsync`
debe retornar `source` (same-currency no-op tiene prioridad y se
ejecuta ANTES de consultar al provider).

---

## 12. Dependencias

### 12.1 Prerrequisitos (completados)

- F-501 ✅: `ExchangeRate`, `IExchangeRateProvider`,
  `StubExchangeRateProvider`, `AddExchangeRates` extension.
- Phase 1 ✅: `IClock`, `IConfiguration`, `ILogger<T>`.

### 12.2 Nuevas dependencias (este slice)

- `Microsoft.Extensions.Caching.Memory` (latest stable para
  .NET 8, agregar a `Directory.Packages.props` vía CPM).

### 12.3 Consumidores futuros (NO en este slice)

- 5-010, 5-011: UI de Fase 5 consume `ICurrencyConverter`.
- Phase 8 (dashboard), Phase 18 (export): consumen
  `ICurrencyConverter`.

### 12.4 Bloqueos

- **Ninguno.** El slice es aditivo: 4 archivos nuevos + 2 archivos
  pre-existentes modificados (csproj + DI extension).

---

## 13. Fuera de alcance (detallado)

- **Provider real** (Phase 6+).
- **`Bootstrap.cs` update** (5-013). El converter queda
  registrado en la extension `AddExchangeRates`, pero Bootstrap
  debe llamar a esa extension. Ese wiring es 5-013.
- **Modificaciones a entidades, configs EF Core, comandos,
  validadores, DTOs, filtros, repositorios, UI, i18n, migraciones**.
  Cero impacto.
- **Tests de integración.** Solo unit tests (con `FakeExchangeRateProvider`
  para inyectar errores controlados).

---

## 14. Criterios de aceptación

### AC-1 — Archivos creados/compilando

- [ ] `Application/Services/ICurrencyConverter.cs` existe y compila.
- [ ] `Application/Services/MoneyDisplay.cs` existe y compila.
- [ ] `Application/Services/CurrencyConverter.cs` existe y compila.
- [ ] `dotnet build -c Debug && -c Release`: 0 warnings, 0 errors.

### AC-2 — Tests verdes

- [ ] `CurrencyConverterTests` existe con ≥ 8 tests, todos verdes.
- [ ] `dotnet test`: 502 actuales + nuevos ≥ **510 verdes**,
      0 failed.

### AC-3 — Convenciones del repo

- [ ] File-scoped namespace en todos los archivos nuevos.
- [ ] XML doc completo en cada tipo y miembro público.
- [ ] `var` cuando el tipo es aparente, explícito cuando no.
- [ ] Sin `Co-Authored-By` ni trailers de IA.

### AC-4 — Compatibilidad arquitectónica

- [ ] `grep -r "using ControlDeFinanzasIA.Infrastructure" src/.../Application`
      sigue retornando 0 líneas.
- [ ] `grep -r "using Microsoft.EntityFrameworkCore" src/.../Application`
      sigue retornando 0 líneas.
- [ ] `Application` no tiene nuevos `ProjectReference`s.
- [ ] `Application` añade solo `Microsoft.Extensions.Caching.Memory`
      como nuevo `PackageReference`.
- [ ] ArchitectureTests (F-007 R-1..R-5) verdes.

### AC-5 — Modificaciones controladas

- [ ] `Infrastructure/ExchangeRates/ExchangeRateServiceCollectionExtensions.cs`
      modificado: agrega `AddMemoryCache()` + `AddSingleton<ICurrencyConverter, CurrencyConverter>()`.
- [ ] `Directory.Packages.props` modificado: agrega versión de
      `Microsoft.Extensions.Caching.Memory` (vía CPM).
- [ ] `Application.csproj` modificado: agrega
      `Microsoft.Extensions.Caching.Memory` como
      `PackageReference` (referenciando el CPM).
- [ ] **Ningún otro archivo pre-existente modificado.**

### AC-6 — Verificación final

- [ ] `dotnet build -c Debug && -c Release`: clean.
- [ ] `dotnet format style --verify-no-changes`: exit 0.
- [ ] `dotnet test`: todos verdes (~510).
- [ ] Spec renombrada a `.done.md` (en T-005 de las tasks).

---

## 15. Estructura de archivos esperada

```
src/
├── ControlDeFinanzasIA.Application/
│   ├── Services/                                       ← nueva carpeta
│   │   ├── ICurrencyConverter.cs                       ← nuevo (F-502)
│   │   ├── MoneyDisplay.cs                             ← nuevo (F-502)
│   │   └── CurrencyConverter.cs                        ← nuevo (F-502)
│   └── ControlDeFinanzasIA.Application.csproj          ← modificado (+ 1 PackageRef)

Directory.Packages.props                                 ← modificado (+ 1 package version)

src/ControlDeFinanzasIA.Infrastructure/
└── ExchangeRates/
    └── ExchangeRateServiceCollectionExtensions.cs       ← modificado (+ 2 lineas DI)

tests/ControlDeFinanzasIA.Tests.Unit/
└── Application/
    └── Services/
        ├── CurrencyConverterTests.cs                   ← nuevo (F-502)
        └── TestDoubles/
            └── FakeExchangeRateProvider.cs             ← nuevo (test double)
```

Archivos **NO modificados** (AC-5):
- `Expense.cs`, `Income.cs`, `ExpenseConfiguration.cs`,
  `IncomeConfiguration.cs`, `AppDbContext.cs`, `Bootstrap.cs`,
  ningún DTO, ningún comando, ningún validador, ningún ViewModel,
  ninguna `.axaml`, ningún `.resx`, ninguna migration,
  `ExchangeRate.cs`, `IExchangeRateProvider.cs`,
  `StubExchangeRateProvider.cs`, `CurrencyPair.cs`.

---

## 16. Decisiones de diseño explícitas

### 16.1 — `ConvertAsync` retorna `Task<Result>`, no `Task<Money>`

El draft original de features.md RF-11 decía `Task<Money>`. Esta
spec lo cambia a `Task<Result>` (con `Value` = `Money` convertido)
para alinearse con F-005 (`Result` non-generic) y F-006
(`Task<Result>` para commands). Es una mejora, no una
incompatibilidad, porque todavía no hay consumidores de
`ConvertAsync` en F-502.

### 16.2 — `CurrencyConverter` en `Application`, no en `Infrastructure`

El converter es lógica de aplicación pura (orquestación de cache
+ provider + logging). No accede a BD, archivos, ni red. Vive en
`Application/Services/`. Esto sigue el principio
"use cases como orquestadores" del Clean Architecture del repo
(ver `docs/architecture.md` §4).

### 16.3 — `IMemoryCache` en lugar de `ConcurrentDictionary` propio

`IMemoryCache` provee TTL eviction, thread-safety, y es el patrón
estándar de Microsoft.Extensions. Agregar el package es
aceptable; reimplementar TTL eviction a mano sería más código
y más bugs. Si en el futuro se quiere evitar el package, se
puede refactorizar a `ConcurrentDictionary` + `Timer`.

### 16.4 — `DisplayAsync` no retorna Result, retorna `MoneyDisplay` con `Converted: null`

`MoneyDisplay` ya tiene el campo `Converted: Money?` que expresa
el éxito/fallo estructuralmente. Agregar un `Result` envuelto
sería redundante y obligaría a la UI a hacer dos niveles de
unwrapping.

### 16.5 — Same-currency tiene prioridad sobre cache y provider

`if (source.Currency == target) return Result.Success(source);`
se ejecuta **antes** del lookup de cache. Esto evita un cache
hit inútil (y un log de "cache hit" ruidoso) para el caso más
frecuente en producción (gasto en DOP, display en DOP).

### 16.6 — Test doubles en `tests/.../Unit/Application/Services/TestDoubles/`

`FakeExchangeRateProvider` permite inyectar tasas controladas o
forzar errores del provider. Sigue el patrón existente de
`FakeExpenseRepository`, `FakeTagRepository`, etc.

---

## 17. Riesgos y mitigaciones

| Riesgo | Probabilidad | Impacto | Mitigación |
| --- | --- | --- | --- |
| Cache miss concurrente golpea provider N veces | Media | Bajo | Aceptable para v1; documentar como limitación; en F-509+ se puede agregar `LazyCache` o `GetOrCreate` |
| Provider real tarda mucho y bloquea UI | Baja | Medio | `CancellationToken` se pasa al provider; UI puede abortar; documentar que el provider debe respetar CT |
| `ConvertAsync` retorna `Task<Result>` pero la UI espera `Money` directo | Baja | Alto | Documentar en XML doc + tests; el caller hace `(Money)result.Value!` |
| `DisplayAsync` con moneda futura (ej. EUR) no tiene destino obvio | Baja | Bajo | Documentar en XML doc; cae a `Currency.USD` como destino |
| `Microsoft.Extensions.Caching.Memory` introduce breaking change en API futura | Muy baja | Medio | API estable desde .NET Core 2.0; riesgo aceptable |
| `IMemoryCache` no es atómico en `GetOrCreate` (concurrencia) | Media | Bajo | Documentar; usar pattern `TryGetValue` + `Set` separados (más explícito) |

---

## 18. Referencias

- `spec/phases/phase-05/features.md` §RF-11 (`CurrencyConverter`),
  §CU-5 (provider failure), §9 (feature ID 5-009).
- `docs/vision.md` §8 (display-time conversion).
- `docs/roadmap.md` §Fase 5 (líneas 270-310).
- `spec/constitution/tech-stack.md` — convenciones.
- `spec/features/phase-05/F-501-exchange-rate-stub.done.md` —
  prerrequisito (`IExchangeRateProvider`, `StubExchangeRateProvider`).
- `spec/features/phase-01/F-005-comuntypes-result-pagedresult.done.md` —
  patrón `Result` non-generic.
- `spec/features/phase-01/F-006-contrato-interfaces-repo.done.md` —
  convención `Task<Result>` para commands.
- `spec/features/phase-01/F-004-Abstracciones-clock-pathresolver.done.md` —
  `IClock` (inyección en converter).
- `tests/.../Unit/TestDoubles/FakeExpenseRepository.cs` — patrón
  de test doubles.
