using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Settings;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>
/// Hand-rolled fake for <see cref="ISettingsStore"/> used in unit tests
/// (F-012, slice 2). Records every <see cref="SetAsync{T}"/> call (key +
/// serialized value) so use case tests can assert exactly which writes
/// happened, and exposes a configurable <see cref="AppSettings"/> for
/// <see cref="GetAppSettingsAsync"/>.
/// </summary>
public sealed class FakeSettingsStore : ISettingsStore
{
    private readonly List<(string Key, string Value)> _setCalls = new();

    /// <summary>Gets the (key, serialized value) pairs passed to <see cref="SetAsync{T}"/>, in call order.</summary>
    public IReadOnlyList<(string Key, string Value)> SetCalls => _setCalls;

    /// <summary>Gets or sets the <see cref="AppSettings"/> returned by <see cref="GetAppSettingsAsync"/>.</summary>
    public AppSettings AppSettings { get; set; } = AppSettings.Defaults;

    /// <summary>Gets or sets the <see cref="Result"/> returned by <see cref="SetAsync{T}"/>. Defaults to success.</summary>
    public Result SetResult { get; set; } = Result.Success(null!);

    /// <summary>
    /// When set, <see cref="SetAsync{T}"/> returns this failure result once
    /// the given key is written, instead of <see cref="SetResult"/>. Lets
    /// tests simulate a mid-sequence write failure.
    /// </summary>
    public string? FailOnKey { get; set; }

    /// <inheritdoc />
    public Task<string?> GetStringAsync(string key, string? defaultValue = null, CancellationToken ct = default)
        => Task.FromResult(defaultValue);

    /// <inheritdoc />
    public Task<T> GetEnumAsync<T>(string key, T defaultValue, CancellationToken ct = default) where T : struct, Enum
        => Task.FromResult(defaultValue);

    /// <inheritdoc />
    public Task<Result> SetAsync<T>(string key, T value, CancellationToken ct = default)
    {
        string serialized = value switch
        {
            null => string.Empty,
            string s => s,
            Enum e => e.ToString(),
            IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };

        _setCalls.Add((key, serialized));

        if (FailOnKey is not null && string.Equals(key, FailOnKey, StringComparison.Ordinal))
        {
            return Task.FromResult(Result.Failure("RepositoryError", "boom"));
        }

        return Task.FromResult(SetResult);
    }

    /// <inheritdoc />
    public Task<AppSettings> GetAppSettingsAsync(CancellationToken ct = default)
        => Task.FromResult(AppSettings);
}
