using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ControlDeFinanzasIA.Infrastructure.Persistence;

/// <summary>
/// Fábrica de diseño para <see cref="AppDbContext" />.
/// Permite que <c>dotnet ef migrations add</c> use el proyecto
/// Infrastructure como startup project sin depender del composition
/// root de la UI (que se conecta en F-005).
/// </summary>
/// <remarks>
/// El connection string de diseño apunta a una base de datos
/// transitoria en el directorio temporal; solo se usa para
/// scaffolding de migraciones, nunca en runtime.
/// </remarks>
public sealed class DesignTimeAppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    /// <summary>
    /// Crea un <see cref="AppDbContext" /> configurado con SQLite
    /// para el tooling de diseño de EF Core.
    /// </summary>
    /// <param name="args">Argumentos pasados por el tooling de EF Core.</param>
    /// <returns>Una nueva instancia de <see cref="AppDbContext" />.</returns>
    public AppDbContext CreateDbContext(string[] args)
    {
        string designTimeDbPath = Path.Combine(Path.GetTempPath(), $"ControlDeFinanzasIA_DesignTime_{Guid.NewGuid():N}.db");
        DbContextOptionsBuilder<AppDbContext> optionsBuilder = new();
        optionsBuilder.UseSqlite($"Data Source={designTimeDbPath}");
        return new AppDbContext(optionsBuilder.Options);
    }
}
