using System.Collections.Concurrent;
using System.Text;
using ControlDeFinanzasIA.Application.Abstractions.Llm;
using ControlDeFinanzasIA.Application.Settings;

namespace ControlDeFinanzasIA.Infrastructure.Llm.Prompts;

/// <summary>
/// Implementación de <see cref="IPromptProvider"/> que lee las plantillas
/// de prompt desde recursos embebidos en este ensamblado (F-013, D-4).
/// Registrada Singleton: las plantillas son inmutables durante la vida
/// del proceso, así que se resuelven una vez y se cachean.
/// </summary>
public sealed class EmbeddedPromptProvider : IPromptProvider
{
    private const string _resourceNamespace = "ControlDeFinanzasIA.Infrastructure.Llm.Prompts";

    private readonly ConcurrentDictionary<(string Name, string Locale), string> _cache = new();

    /// <inheritdoc />
    public string GetTemplate(string name, string locale)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(locale);

        return _cache.GetOrAdd((name, locale), static key => LoadTemplate(key.Name, key.Locale));
    }

    private static string LoadTemplate(string name, string locale)
    {
        string? template = ReadResource(name, locale);

        if (template is not null)
        {
            return template;
        }

        if (!string.Equals(locale, SupportedLocales.Default, StringComparison.Ordinal))
        {
            template = ReadResource(name, SupportedLocales.Default);
        }

        // A missing resource for a known prompt name is a packaging bug,
        // not a runtime condition: it must surface in CI, not at a user's
        // keystroke (D-4).
        return template
            ?? throw new InvalidOperationException(
                $"No prompt resource found for name '{name}' "
                    + $"(locale '{locale}' or default locale '{SupportedLocales.Default}').");
    }

    private static string? ReadResource(string name, string locale)
    {
        string resourceName = $"{_resourceNamespace}.{name}.{locale}.txt";

        using Stream? stream = typeof(EmbeddedPromptProvider).Assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
