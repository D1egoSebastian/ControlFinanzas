using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.Settings;
using Microsoft.EntityFrameworkCore;

namespace ControlDeFinanzasIA.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementacion CRUD cruda de <see cref="ISettingRepository"/>, plantilla
/// de <c>BudgetRepository</c> sin la copia de navegacion owned de
/// <c>Money</c>: <see cref="Setting"/> no tiene propiedades owned, asi que
/// la clase de bug de Fase 10/11 (D-7 de <c>BudgetRepository</c>) no
/// aplica aqui (F-012 D-6).
/// </summary>
public sealed class SettingRepository : ISettingRepository
{
    private readonly AppDbContext _db;

    public SettingRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Result> AddAsync(Setting setting, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(setting);
        _db.Settings.Add(setting);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return Result.Success(null!);
    }

    public async Task<Result> UpdateAsync(Setting setting, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(setting);
        Setting? existing = await _db.Settings.FindAsync([setting.Id], ct).ConfigureAwait(false);
        if (existing is null)
        {
            return Result.Failure("NotFound", $"Setting {setting.Id} was not found.");
        }

        // NO Reference(...).TargetEntry copy aqui, a proposito: Setting no tiene
        // navegacion OwnsOne. La linea extra en BudgetRepository/GoalRepository
        // existe solo para su Money owned. No copiarla aqui buscando simetria.
        _db.Entry(existing).CurrentValues.SetValues(setting);

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return Result.Success(null!);
    }

    public async Task<Result> RemoveAsync(Guid id, CancellationToken ct = default)
    {
        Setting? existing = await _db.Settings.FindAsync([id], ct).ConfigureAwait(false);
        if (existing is null)
        {
            return Result.Failure("NotFound", $"Setting {id} was not found.");
        }

        _db.Settings.Remove(existing);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return Result.Success(null!);
    }

    public Task<Setting?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Settings.AsNoTracking().FirstOrDefaultAsync(setting => setting.Id == id, ct);

    public async Task<IReadOnlyList<Setting>> GetAllAsync(CancellationToken ct = default) =>
        await _db.Settings.AsNoTracking()
            .OrderBy(setting => setting.Key)
            .ToListAsync(ct).ConfigureAwait(false);
}
