using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Settings;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>
/// Hand-rolled fake for <see cref="ISettingRepository"/> used in unit
/// tests (F-012, slice 1). Stores settings in memory and exposes call
/// counters so tests can assert whether <see cref="AddAsync"/> or
/// <see cref="UpdateAsync"/> was invoked (upsert-by-key semantics).
/// </summary>
public sealed class FakeSettingRepository : ISettingRepository
{
    private readonly List<Setting> _settings = new();

    /// <summary>Gets the settings currently stored.</summary>
    public IReadOnlyList<Setting> Settings => _settings;

    /// <summary>Gets or sets the result returned by <see cref="AddAsync"/>.</summary>
    public Result AddResult { get; set; } = Result.Success(null!);

    /// <summary>Gets or sets the result returned by <see cref="UpdateAsync"/>.</summary>
    public Result UpdateResult { get; set; } = Result.Success(null!);

    /// <summary>When set, <see cref="GetAllAsync"/> throws this instead of returning.</summary>
    public Exception? GetAllException { get; set; }

    /// <summary>Number of times <see cref="AddAsync"/> was invoked.</summary>
    public int AddAsyncCallCount { get; private set; }

    /// <summary>Number of times <see cref="UpdateAsync"/> was invoked.</summary>
    public int UpdateAsyncCallCount { get; private set; }

    public void Seed(Setting setting) => _settings.Add(setting);

    /// <inheritdoc />
    public Task<Result> AddAsync(Setting setting, CancellationToken ct = default)
    {
        AddAsyncCallCount++;
        if (AddResult.IsSuccess)
        {
            _settings.Add(setting);
        }

        return Task.FromResult(AddResult);
    }

    /// <inheritdoc />
    public Task<Result> UpdateAsync(Setting setting, CancellationToken ct = default)
    {
        UpdateAsyncCallCount++;
        if (UpdateResult.IsSuccess)
        {
            int index = _settings.FindIndex(s => s.Id == setting.Id);
            if (index >= 0)
            {
                _settings[index] = setting;
            }
        }

        return Task.FromResult(UpdateResult);
    }

    /// <inheritdoc />
    public Task<Result> RemoveAsync(Guid id, CancellationToken ct = default)
    {
        _settings.RemoveAll(s => s.Id == id);
        return Task.FromResult(Result.Success(null!));
    }

    /// <inheritdoc />
    public Task<Setting?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_settings.FirstOrDefault(s => s.Id == id));

    /// <inheritdoc />
    public Task<IReadOnlyList<Setting>> GetAllAsync(CancellationToken ct = default)
    {
        if (GetAllException is not null)
        {
            throw GetAllException;
        }

        return Task.FromResult<IReadOnlyList<Setting>>(_settings.OrderBy(s => s.Key, StringComparer.Ordinal).ToList());
    }
}
