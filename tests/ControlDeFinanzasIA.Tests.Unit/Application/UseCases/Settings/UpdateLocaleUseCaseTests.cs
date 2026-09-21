using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Settings;
using ControlDeFinanzasIA.Application.UseCases.Settings.UpdateLocale;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.UseCases.Settings;

/// <summary>
/// Tests for <see cref="UpdateLocaleUseCase"/> (F-012, R-4, S-3, D-4, D-9).
/// </summary>
public sealed class UpdateLocaleUseCaseTests
{
    private static UpdateLocaleUseCase CreateUseCase(FakeSettingsStore store) =>
        new(store, NullLogger<UpdateLocaleUseCase>.Instance);

    [Fact]
    public async Task ExecuteAsync_UnsupportedLocale_ReturnsUnsupportedLocaleFailureWithoutWriting()
    {
        var store = new FakeSettingsStore();
        UpdateLocaleUseCase useCase = CreateUseCase(store);

        Result result = await useCase.ExecuteAsync("fr-FR");

        Assert.False(result.IsSuccess);
        Assert.Equal("UnsupportedLocale", result.Error!.Code);
        Assert.Empty(store.SetCalls);
    }

    [Theory]
    [InlineData(SupportedLocales.SpanishDominican)]
    [InlineData(SupportedLocales.English)]
    public async Task ExecuteAsync_SupportedLocale_Persists(string locale)
    {
        var store = new FakeSettingsStore();
        UpdateLocaleUseCase useCase = CreateUseCase(store);

        Result result = await useCase.ExecuteAsync(locale);

        Assert.True(result.IsSuccess);
        (string Key, string Value) call = Assert.Single(store.SetCalls);
        Assert.Equal(SettingKeys.Locale, call.Key);
        Assert.Equal(locale, call.Value);
    }
}
