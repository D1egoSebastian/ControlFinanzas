using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Settings;
using ControlDeFinanzasIA.Domain.Settings;
using ControlDeFinanzasIA.Infrastructure.Persistence.Repositories;
using ControlDeFinanzasIA.Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Integration.Persistence;

/// <summary>
/// Tests de integracion de round-trip para <see cref="SettingRepository"/>
/// y <see cref="SettingsStore"/> contra SQLite real (no in-memory provider),
/// mismo patron que <c>BudgetRepositoryTests</c> (F-012, R-7, S-9, S-11).
/// </summary>
public sealed class SettingRepositoryTests
{
    [Fact]
    public async Task AddAsync_ThenGetById_RoundTripsKeyAndValue()
    {
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        var repository = new SettingRepository(context);
        var setting = new Setting { Id = Guid.NewGuid(), Key = SettingKeys.Theme, Value = "Dark" };

        Result addResult = await repository.AddAsync(setting);
        Setting? loaded = await repository.GetByIdAsync(setting.Id);

        Assert.True(addResult.IsSuccess);
        Assert.NotNull(loaded);
        Assert.Equal(SettingKeys.Theme, loaded!.Key);
        Assert.Equal("Dark", loaded.Value);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsRowsOrderedByKey()
    {
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        var repository = new SettingRepository(context);
        await repository.AddAsync(new Setting { Id = Guid.NewGuid(), Key = "theme", Value = "Dark" });
        await repository.AddAsync(new Setting { Id = Guid.NewGuid(), Key = "ai.provider", Value = "OpenAI" });
        await repository.AddAsync(new Setting { Id = Guid.NewGuid(), Key = "locale", Value = "es-DO" });

        IReadOnlyList<Setting> all = await repository.GetAllAsync();

        Assert.Equal(["ai.provider", "locale", "theme"], all.Select(s => s.Key));
    }

    [Fact]
    public async Task UniqueIndex_DuplicateKey_ThrowsOnSave()
    {
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        var repository = new SettingRepository(context);
        await repository.AddAsync(new Setting { Id = Guid.NewGuid(), Key = SettingKeys.Theme, Value = "Dark" });

        var duplicate = new Setting { Id = Guid.NewGuid(), Key = SettingKeys.Theme, Value = "Light" };

        await Assert.ThrowsAsync<DbUpdateException>(() => repository.AddAsync(duplicate));
    }

    [Fact]
    public async Task UpdateAsync_UnknownId_ReturnsNotFound()
    {
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        var repository = new SettingRepository(context);
        var setting = new Setting { Id = Guid.NewGuid(), Key = SettingKeys.Theme, Value = "Dark" };

        Result updateResult = await repository.UpdateAsync(setting);

        Assert.False(updateResult.IsSuccess);
        Assert.Equal("NotFound", updateResult.Error!.Code);
    }

    [Fact]
    public async Task SettingsStore_SetAsync_SameKeyTwice_UpsertsToExactlyOneRow()
    {
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        var repository = new SettingRepository(context);
        var store = new SettingsStore(repository, NullLogger<SettingsStore>.Instance);

        await store.SetAsync(SettingKeys.Theme, AppTheme.Light);
        await store.SetAsync(SettingKeys.Theme, AppTheme.Dark);

        IReadOnlyList<Setting> rows = await repository.GetAllAsync();

        Assert.Single(rows);
        Assert.Equal("Dark", rows[0].Value);
    }

    [Fact]
    public async Task SettingsStore_GetAppSettingsAsync_EmptyTable_ReturnsDefaultsNeverThrows()
    {
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        var repository = new SettingRepository(context);
        var store = new SettingsStore(repository, NullLogger<SettingsStore>.Instance);

        AppSettings settings = await store.GetAppSettingsAsync();

        Assert.Equal(AppSettings.Defaults, settings);
    }

    [Fact]
    public async Task SettingsStore_AllSettings_SurviveARestart_ThroughAFreshContextAndStore()
    {
        // R-7/S-9: verified via a direct ISettingRepository/ISettingsStore round-trip
        // using a fresh DbContext/service instance, not an in-memory cache.
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext writeContext = factory.CreateContext();
        await writeContext.Database.MigrateAsync();

        var writeRepository = new SettingRepository(writeContext);
        var writeStore = new SettingsStore(writeRepository, NullLogger<SettingsStore>.Instance);

        await writeStore.SetAsync(SettingKeys.Theme, AppTheme.Dark);
        await writeStore.SetAsync(SettingKeys.Locale, "en");
        await writeStore.SetAsync(SettingKeys.AiProvider, AiProvider.Anthropic);
        await writeStore.SetAsync(SettingKeys.AiApiKey, "sk-test-key");
        await writeStore.SetAsync(SettingKeys.AiModel, "claude-test");
        await writeStore.SetAsync(SettingKeys.AiBaseUrl, "https://example.test/v1");
        await writeStore.SetAsync(SettingKeys.AiTemperature, 1.2d);
        await writeStore.SetAsync(SettingKeys.AiMaxTokensPerRequest, 4096);

        await using Infrastructure.Persistence.AppDbContext freshContext = factory.CreateContext();
        var freshRepository = new SettingRepository(freshContext);
        var freshStore = new SettingsStore(freshRepository, NullLogger<SettingsStore>.Instance);

        AppSettings settings = await freshStore.GetAppSettingsAsync();

        Assert.Equal(AppTheme.Dark, settings.Theme);
        Assert.Equal("en", settings.Locale);
        Assert.Equal(AiProvider.Anthropic, settings.AiProvider);
        Assert.Equal("sk-test-key", settings.AiApiKey);
        Assert.Equal("claude-test", settings.AiModel);
        Assert.Equal("https://example.test/v1", settings.AiBaseUrl);
        Assert.Equal(1.2d, settings.AiTemperature);
        Assert.Equal(4096, settings.AiMaxTokensPerRequest);
    }

    [Fact]
    public async Task SettingsStore_EnumValues_ArePersistedByNameNotOrdinal()
    {
        using var factory = new TestAppDbContextFactory();
        await using Infrastructure.Persistence.AppDbContext context = factory.CreateContext();
        await context.Database.MigrateAsync();

        var repository = new SettingRepository(context);
        var store = new SettingsStore(repository, NullLogger<SettingsStore>.Instance);

        await store.SetAsync(SettingKeys.Theme, AppTheme.Light);

        Setting? raw = (await repository.GetAllAsync()).SingleOrDefault(s => s.Key == SettingKeys.Theme);

        Assert.NotNull(raw);
        Assert.Equal("Light", raw!.Value);
        Assert.NotEqual("1", raw.Value);
    }
}
