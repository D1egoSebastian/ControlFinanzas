using ControlDeFinanzasIA.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ControlDeFinanzasIA.Tests.Integration.Persistence;

/// <summary>
/// Factoría de <see cref="AppDbContext"/> para tests de integración.
/// </summary>
/// <remarks>
/// Usa una <see cref="SqliteConnection"/> en memoria compartida entre
/// todos los <see cref="AppDbContext"/> creados durante la vida de un test.
/// La conexión se mantiene abierta para que SQLite preserve la base de
/// datos en memoria; al cerrarse (disposición de la factoría), la BD se
/// destruye.
/// </remarks>
public sealed class TestAppDbContextFactory : IDisposable
{
    private readonly SqliteConnection _connection;

    /// <summary>
    /// Inicializa la factoría abriendo una conexión SQLite en memoria.
    /// </summary>
    public TestAppDbContextFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    /// <summary>
    /// Crea un <see cref="AppDbContext"/> configurado para usar la
    /// conexión SQLite compartida.
    /// </summary>
    /// <returns>
    /// Un <see cref="AppDbContext"/> listo para usar en el test.
    /// </returns>
    public AppDbContext CreateContext()
    {
        DbContextOptionsBuilder<AppDbContext> optionsBuilder = new();
        optionsBuilder.UseSqlite(_connection);
        return new AppDbContext(optionsBuilder.Options);
    }

    /// <summary>
    /// Cierra y dispone la conexión SQLite, destruyendo la base de
    /// datos en memoria.
    /// </summary>
    public void Dispose() => _connection.Dispose();
}
