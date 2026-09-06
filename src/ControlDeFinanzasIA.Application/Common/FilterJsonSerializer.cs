using System.Text.Json;

namespace ControlDeFinanzasIA.Application.Common;

/// <summary>
/// Serializador de filtros a JSON versionado mediante un envelope
/// <c>{ schemaVersion, type, data }</c>. Usa <see cref="System.Text.Json"/>
/// con naming camelCase y sin indentación.
/// </summary>
public static class FilterJsonSerializer
{
    /// <summary>
    /// Versión actual del esquema de serialización de filtros.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    /// <summary>
    /// Serializa un filter a JSON con envelope versionado.
    /// </summary>
    /// <typeparam name="T">Tipo del filter a serializar.</typeparam>
    /// <param name="filter">Instancia del filter.</param>
    /// <returns>JSON con el envelope <c>{ schemaVersion, type, data }</c>.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="filter"/> es <c>null</c>.</exception>
    public static string Serialize<T>(T filter)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(filter);
        return Serialize(filter, typeof(T));
    }

    /// <summary>
    /// Serializa un filter a JSON con envelope versionado usando su tipo en runtime.
    /// </summary>
    /// <param name="filter">Instancia del filter.</param>
    /// <param name="type">Tipo del filter.</param>
    /// <returns>JSON con el envelope <c>{ schemaVersion, type, data }</c>.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="filter"/> o <paramref name="type"/> son <c>null</c>.</exception>
    public static string Serialize(object filter, Type type)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(type);

        var envelope = new
        {
            schemaVersion = CurrentSchemaVersion,
            type = type.Name,
            data = filter,
        };

        return JsonSerializer.Serialize(envelope, _jsonOptions);
    }

    /// <summary>
    /// Deserializa un JSON envelope a un filter del tipo <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">Tipo esperado del filter.</typeparam>
    /// <param name="filterJson">JSON con el envelope.</param>
    /// <returns>Instancia deserializada del filter.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="filterJson"/> es <c>null</c>.</exception>
    /// <exception cref="JsonException">Si el JSON está malformado, falta <c>type</c> o <c>data</c>, o el <c>schemaVersion</c> es menor que 1.</exception>
    /// <exception cref="InvalidOperationException">Si el <c>schemaVersion</c> es mayor que <see cref="CurrentSchemaVersion"/> o hay mismatch de <c>type</c>.</exception>
    public static T Deserialize<T>(string filterJson)
        where T : class
        => (T)Deserialize(filterJson, typeof(T));

    /// <summary>
    /// Deserializa un JSON envelope a un filter del tipo <paramref name="targetType"/> (polimórfico).
    /// </summary>
    /// <param name="filterJson">JSON con el envelope.</param>
    /// <param name="targetType">Tipo esperado del filter.</param>
    /// <returns>Instancia deserializada del filter como <see cref="object"/>.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="filterJson"/> o <paramref name="targetType"/> son <c>null</c>.</exception>
    /// <exception cref="JsonException">Si el JSON está malformado, falta <c>type</c> o <c>data</c>, o el <c>schemaVersion</c> es menor que 1.</exception>
    /// <exception cref="InvalidOperationException">Si el <c>schemaVersion</c> es mayor que <see cref="CurrentSchemaVersion"/> o hay mismatch de <c>type</c>.</exception>
    public static object Deserialize(string filterJson, Type targetType)
    {
        ArgumentNullException.ThrowIfNull(filterJson);
        ArgumentNullException.ThrowIfNull(targetType);

        using var doc = JsonDocument.Parse(filterJson);
        JsonElement root = doc.RootElement;

        int schemaVersion = root.TryGetProperty("schemaVersion", out JsonElement schemaVersionElement)
            ? schemaVersionElement.GetInt32()
            : 1;

        if (schemaVersion > CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Schema version {schemaVersion} is not supported. Current version: {CurrentSchemaVersion}.");
        }

        if (schemaVersion < 1)
        {
            throw new JsonException(
                $"Envelope 'schemaVersion' must be at least 1. Received: {schemaVersion}.");
        }

        string envelopeType = root.GetProperty("type").GetString()
            ?? throw new InvalidOperationException("Missing 'type'.");

        if (!string.Equals(envelopeType, targetType.Name, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Type mismatch: envelope '{envelopeType}' vs expected '{targetType.Name}'.");
        }

        JsonElement dataElement = root.GetProperty("data");

        object? result = JsonSerializer.Deserialize(
            dataElement.GetRawText(),
            targetType,
            _jsonOptions);

        if (result is null)
        {
            throw new InvalidOperationException("Deserialized filter is null.");
        }

        return result;
    }
}
