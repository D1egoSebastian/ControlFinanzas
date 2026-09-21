using System.Globalization;
using System.Net.Http;
using System.Reflection;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Settings;
using ControlDeFinanzasIA.Domain.Settings;
using ControlDeFinanzasIA.Infrastructure.Settings;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Infrastructure.Settings;

/// <summary>
/// Tests para <see cref="SettingsStore"/> con un
/// <see cref="FakeSettingRepository"/> (F-012, D-5, S-10, S-11).
/// </summary>
public sealed class SettingsStoreTests
{
    private static SettingsStore CreateStore(FakeSettingRepository repository) =>
        new(repository, NullLogger<SettingsStore>.Instance);

    [Fact]
    public async Task GetAppSettingsAsync_EmptyTable_ReturnsDefaults()
    {
        var repository = new FakeSettingRepository();
        SettingsStore store = CreateStore(repository);

        AppSettings result = await store.GetAppSettingsAsync();

        Assert.NotNull(result);
        Assert.Equal(AppSettings.Defaults, result);
    }

    [Fact]
    public async Task GetAppSettingsAsync_ThrowingRepository_ReturnsDefaultsAndLogsWarning()
    {
        var repository = new FakeSettingRepository { GetAllException = new InvalidOperationException("boom") };
        var logger = new RecordingLogger<SettingsStore>();
        var store = new SettingsStore(repository, logger);

        AppSettings result = await store.GetAppSettingsAsync();

        Assert.Equal(AppSettings.Defaults, result);
        Assert.True(logger.HasWarning);
    }

    [Fact]
    public async Task SetAsync_AbsentKey_CallsAddOnceAndNeverUpdate()
    {
        var repository = new FakeSettingRepository();
        SettingsStore store = CreateStore(repository);

        Result result = await store.SetAsync(SettingKeys.Theme, AppTheme.Dark);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, repository.AddAsyncCallCount);
        Assert.Equal(0, repository.UpdateAsyncCallCount);
        Assert.Single(repository.Settings);
    }

    [Fact]
    public async Task SetAsync_PresentKey_CallsUpdateOnceWithSameIdAndKey()
    {
        var repository = new FakeSettingRepository();
        var id = Guid.NewGuid();
        repository.Seed(new Setting { Id = id, Key = SettingKeys.Theme, Value = "Dark" });
        SettingsStore store = CreateStore(repository);

        Result result = await store.SetAsync(SettingKeys.Theme, AppTheme.Light);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, repository.AddAsyncCallCount);
        Assert.Equal(1, repository.UpdateAsyncCallCount);
        Assert.Single(repository.Settings);
        Assert.Equal(id, repository.Settings[0].Id);
        Assert.Equal(SettingKeys.Theme, repository.Settings[0].Key);
        Assert.Equal("Light", repository.Settings[0].Value);
    }

    [Fact]
    public async Task SetAsync_SameKeyTwice_UpsertsInsteadOfDuplicating()
    {
        var repository = new FakeSettingRepository();
        SettingsStore store = CreateStore(repository);

        await store.SetAsync(SettingKeys.Theme, AppTheme.Light);
        await store.SetAsync(SettingKeys.Theme, AppTheme.Dark);

        Assert.Single(repository.Settings);
        Assert.Equal("Dark", repository.Settings[0].Value);
    }

    [Fact]
    public async Task GetAppSettingsAsync_UnsupportedStoredLocale_FallsBackToDefault()
    {
        var repository = new FakeSettingRepository();
        repository.Seed(new Setting { Id = Guid.NewGuid(), Key = SettingKeys.Locale, Value = "fr-FR" });
        SettingsStore store = CreateStore(repository);

        AppSettings result = await store.GetAppSettingsAsync();

        Assert.Equal(SupportedLocales.Default, result.Locale);
    }

    [Fact]
    public async Task GetAppSettingsAsync_UnparseableEnum_FallsBackToDefault()
    {
        var repository = new FakeSettingRepository();
        repository.Seed(new Setting { Id = Guid.NewGuid(), Key = SettingKeys.Theme, Value = "NotAnEnumValue" });
        SettingsStore store = CreateStore(repository);

        AppSettings result = await store.GetAppSettingsAsync();

        Assert.Equal(AppSettings.DefaultTheme, result.Theme);
    }

    [Fact]
    public async Task GetAppSettingsAsync_UnparseableDouble_FallsBackToDefault()
    {
        var repository = new FakeSettingRepository();
        repository.Seed(new Setting { Id = Guid.NewGuid(), Key = SettingKeys.AiTemperature, Value = "not-a-number" });
        SettingsStore store = CreateStore(repository);

        AppSettings result = await store.GetAppSettingsAsync();

        Assert.Equal(AppSettings.DefaultAiTemperature, result.AiTemperature);
    }

    [Fact]
    public async Task GetAppSettingsAsync_UnparseableInt_FallsBackToDefault()
    {
        var repository = new FakeSettingRepository();
        repository.Seed(new Setting { Id = Guid.NewGuid(), Key = SettingKeys.AiMaxTokensPerRequest, Value = "not-a-number" });
        SettingsStore store = CreateStore(repository);

        AppSettings result = await store.GetAppSettingsAsync();

        Assert.Equal(AppSettings.DefaultAiMaxTokensPerRequest, result.AiMaxTokensPerRequest);
    }

    [Fact]
    public async Task EnumValue_IsPersistedByNameNotOrdinal()
    {
        // Load-bearing per D-5: "0"/"1" surviving an enum reorder would silently
        // repoint every user's configuration. The raw stored string must be the
        // enum's NAME.
        var repository = new FakeSettingRepository();
        SettingsStore store = CreateStore(repository);

        await store.SetAsync(SettingKeys.Theme, AppTheme.Light);
        await store.SetAsync(SettingKeys.AiProvider, AiProvider.OpenAI);

        Assert.Equal("Light", repository.Settings.Single(s => s.Key == SettingKeys.Theme).Value);
        Assert.NotEqual("1", repository.Settings.Single(s => s.Key == SettingKeys.Theme).Value);
        Assert.Equal("OpenAI", repository.Settings.Single(s => s.Key == SettingKeys.AiProvider).Value);
        Assert.NotEqual("0", repository.Settings.Single(s => s.Key == SettingKeys.AiProvider).Value);
    }

    [Fact]
    public async Task AiTemperature_InvariantCultureRoundTrip_WriteEsDoReadEnUs_StaysZeroPointSeven()
    {
        // D-5's single most load-bearing line: under es-DO, 0.7 formats as "0,7";
        // under en-US, double.Parse("0,7") yields 7.0 if culture isn't pinned.
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            var repository = new FakeSettingRepository();
            SettingsStore store = CreateStore(repository);

            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("es-DO");
            await store.SetAsync(SettingKeys.AiTemperature, 0.7d);

            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            AppSettings settings = await store.GetAppSettingsAsync();

            Assert.Equal(0.7d, settings.AiTemperature);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public async Task AiTemperature_InvariantCultureRoundTrip_WriteEnUsReadEsDo_StaysZeroPointSeven()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            var repository = new FakeSettingRepository();
            SettingsStore store = CreateStore(repository);

            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            await store.SetAsync(SettingKeys.AiTemperature, 0.7d);

            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("es-DO");
            AppSettings settings = await store.GetAppSettingsAsync();

            Assert.Equal(0.7d, settings.AiTemperature);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Constructor_HasNoHttpClientOrLLMClientDependency()
    {
        // Reflection assertion (R-5, S-6): the ctor signature IS the "zero AI
        // calls" guarantee for this phase.
        //
        // F-013 slice 1 note: this originally also asserted no type named
        // "*LLMClient*" existed anywhere in the Application/Infrastructure
        // assemblies, as a trip wire against Phase 13 starting early. F-013
        // slice 1 has now legitimately introduced `ILLMClientFactory` (a
        // zero-HTTP-surface abstraction — see
        // Tests.Unit/Llm/LlmContractPurityTests), so that
        // assembly-wide scan is retired. The guarantee that actually matters
        // here — SettingsStore's own constructor never takes an HTTP or LLM
        // client dependency — is unchanged and still asserted below.
        ConstructorInfo ctor = typeof(SettingsStore).GetConstructors().Single();
        ParameterInfo[] parameters = ctor.GetParameters();

        Assert.DoesNotContain(parameters, p => p.ParameterType.Namespace == typeof(HttpClient).Namespace);
        Assert.DoesNotContain(parameters, p => p.ParameterType.Name.Contains("LLMClient", StringComparison.Ordinal));
    }
}
