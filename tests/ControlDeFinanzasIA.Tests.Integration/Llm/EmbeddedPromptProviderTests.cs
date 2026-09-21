using ControlDeFinanzasIA.Application.Llm;
using ControlDeFinanzasIA.Application.Settings;
using ControlDeFinanzasIA.Infrastructure.Llm.Prompts;

namespace ControlDeFinanzasIA.Tests.Integration.Llm;

/// <summary>
/// Tests de integración para <see cref="EmbeddedPromptProvider"/> (F-013,
/// slice 2, D-4). No usa <c>FakePromptProvider</c> a propósito: este es el
/// test que prueba que los recursos embebidos reales existen y se
/// resuelven.
/// </summary>
public class EmbeddedPromptProviderTests
{
    private static readonly string[] _allPromptNames =
    [
        PromptNames.CategorizeExpenseSystem,
        PromptNames.CategorizeExpenseUser,
    ];

    [Theory]
    [InlineData(SupportedLocales.SpanishDominican)]
    [InlineData(SupportedLocales.English)]
    public void GetTemplate_EveryKnownPromptName_ResolvesForEverySupportedLocale(string locale)
    {
        var provider = new EmbeddedPromptProvider();

        foreach (string name in _allPromptNames)
        {
            string template = provider.GetTemplate(name, locale);

            Assert.False(string.IsNullOrWhiteSpace(template));
        }
    }

    [Fact]
    public void GetTemplate_UnknownLocale_FallsBackToDefaultLocale()
    {
        var provider = new EmbeddedPromptProvider();

        string fallback = provider.GetTemplate(PromptNames.CategorizeExpenseSystem, "fr-FR");
        string defaultTemplate = provider.GetTemplate(
            PromptNames.CategorizeExpenseSystem,
            SupportedLocales.Default);

        Assert.Equal(defaultTemplate, fallback);
    }

    [Theory]
    [InlineData(SupportedLocales.SpanishDominican)]
    [InlineData(SupportedLocales.English)]
    public void GetTemplate_SystemPrompt_ContainsCategoriesPlaceholderAndCoreInstruction(string locale)
    {
        var provider = new EmbeddedPromptProvider();

        string template = provider.GetTemplate(PromptNames.CategorizeExpenseSystem, locale);

        Assert.Contains("{{categories}}", template, StringComparison.Ordinal);
        Assert.Contains(
            "Respond with exactly one category name copied verbatim from the list, or null if none fits.",
            template,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(SupportedLocales.SpanishDominican)]
    [InlineData(SupportedLocales.English)]
    public void GetTemplate_UserPrompt_ContainsDescriptionAndCategoriesPlaceholders(string locale)
    {
        var provider = new EmbeddedPromptProvider();

        string template = provider.GetTemplate(PromptNames.CategorizeExpenseUser, locale);

        Assert.Contains("{{description}}", template, StringComparison.Ordinal);
        Assert.Contains("{{categories}}", template, StringComparison.Ordinal);
    }

    [Fact]
    public void GetTemplate_UnknownName_Throws()
    {
        var provider = new EmbeddedPromptProvider();

        Assert.Throws<InvalidOperationException>(
            () => provider.GetTemplate("not-a-real-prompt-name", SupportedLocales.Default));
    }
}
