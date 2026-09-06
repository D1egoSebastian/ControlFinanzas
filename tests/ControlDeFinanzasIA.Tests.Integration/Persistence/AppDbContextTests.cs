using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ControlDeFinanzasIA.Tests.Integration.Persistence;

/// <summary>
/// Tests de integración para <see cref="AppDbContext"/>.
/// </summary>
/// <remarks>
/// Verifica que las migraciones crean el schema esperado y que el seed
/// del sistema inserta exactamente 9 categorías y 5 métodos de pago de
/// forma idempotente.
/// </remarks>
public class AppDbContextTests
{
    /// <summary>
    /// Aplica las migraciones y verifica que las tablas
    /// <c>Categories</c> y <c>PaymentMethods</c> existen.
    /// </summary>
    [Fact]
    public async Task Migrate_CreatesSchema()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();

        await context.Database.MigrateAsync();

        List<string> tables = await context.Database
            .SqlQueryRaw<string>("SELECT name FROM sqlite_master WHERE type='table'")
            .ToListAsync();

        Assert.Contains("Categories", tables);
        Assert.Contains("PaymentMethods", tables);
    }

    /// <summary>
    /// Verifica que el seed inserta exactamente 9 categorías del sistema.
    /// </summary>
    [Fact]
    public async Task SeedData_InsertsSystemCategories()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();

        await context.Database.MigrateAsync();

        List<Category> categories = await context.Set<Category>().ToListAsync();

        Assert.Equal(9, categories.Count);
        Assert.All(categories, c => Assert.True(c.IsSystem));
    }

    /// <summary>
    /// Verifica que el seed inserta exactamente 5 métodos de pago del sistema.
    /// </summary>
    [Fact]
    public async Task SeedData_InsertsSystemPaymentMethods()
    {
        using var factory = new TestAppDbContextFactory();
        using AppDbContext context = factory.CreateContext();

        await context.Database.MigrateAsync();

        List<PaymentMethod> paymentMethods = await context.Set<PaymentMethod>().ToListAsync();

        Assert.Equal(5, paymentMethods.Count);
        Assert.All(paymentMethods, p => Assert.True(p.IsSystem));
    }

    /// <summary>
    /// Verifica que aplicar las migraciones dos veces sobre la misma
    /// conexión no duplica los registros del seed.
    /// </summary>
    [Fact]
    public async Task SeedData_IsIdempotent()
    {
        using var factory = new TestAppDbContextFactory();

        using (AppDbContext context1 = factory.CreateContext())
        {
            await context1.Database.MigrateAsync();
        }

        using (AppDbContext context2 = factory.CreateContext())
        {
            await context2.Database.MigrateAsync();
        }

        using AppDbContext context3 = factory.CreateContext();
        List<Category> categories = await context3.Set<Category>().ToListAsync();
        List<PaymentMethod> paymentMethods = await context3.Set<PaymentMethod>().ToListAsync();

        Assert.Equal(9, categories.Count);
        Assert.Equal(5, paymentMethods.Count);
    }
}
