using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Settings;
using ControlDeFinanzasIA.Application.UseCases.Settings.UpdateTheme;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.UseCases.Settings;

/// <summary>
/// Tests for <see cref="UpdateThemeUseCase"/> (F-012, R-3, S-1, D-9).
/// </summary>
public sealed class UpdateThemeUseCaseTests
{
    private static UpdateThemeUseCase CreateUseCase(FakeSettingsStore store) =>
        new(store, NullLogger<UpdateThemeUseCase>.Instance);

    [Fact]
    public async Task ExecuteAsync_InvalidEnumValue_ReturnsValidationFailureWithoutWriting()
    {
        var store = new FakeSettingsStore();
        UpdateThemeUseCase useCase = CreateUseCase(store);

        Result result = await useCase.ExecuteAsync((AppTheme)99);

        Assert.False(result.IsSuccess);
        Assert.Equal("Validation", result.Error!.Code);
        Assert.Empty(store.SetCalls);
    }

    [Fact]
    public async Task ExecuteAsync_Dark_PersistsByName()
    {
        var store = new FakeSettingsStore();
        UpdateThemeUseCase useCase = CreateUseCase(store);

        Result result = await useCase.ExecuteAsync(AppTheme.Dark);

        Assert.True(result.IsSuccess);
        (string Key, string Value) call = Assert.Single(store.SetCalls);
        Assert.Equal(SettingKeys.Theme, call.Key);
        Assert.Equal("Dark", call.Value);
    }

    [Fact]
    public async Task ExecuteAsync_Light_IsPersistedEvenThoughNotRendered()
    {
        // Q1: Light is a legal, persisted choice even though it never applies.
        var store = new FakeSettingsStore();
        UpdateThemeUseCase useCase = CreateUseCase(store);

        Result result = await useCase.ExecuteAsync(AppTheme.Light);

        Assert.True(result.IsSuccess);
        (string Key, string Value) call = Assert.Single(store.SetCalls);
        Assert.Equal(SettingKeys.Theme, call.Key);
        Assert.Equal("Light", call.Value);
        Assert.NotEqual("1", call.Value);
    }
}
