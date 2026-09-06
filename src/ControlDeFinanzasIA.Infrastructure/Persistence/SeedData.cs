using ControlDeFinanzasIA.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace ControlDeFinanzasIA.Infrastructure.Persistence;

/// <summary>
/// Sembrado de datos del sistema para EF Core.
/// </summary>
/// <remarks>
/// Contiene las 9 categorías y 5 métodos de pago del sistema,
/// con IDs deterministas e <c>IsSystem = true</c>. El seed se aplica
/// en <see cref="AppDbContext.OnModelCreating(ModelBuilder)"/> mediante
/// <c>modelBuilder.Entity&lt;Category&gt;().HasData(...)</c> y
/// <c>modelBuilder.Entity&lt;PaymentMethod&gt;().HasData(...)</c>, y se
/// materializa en la migración <c>SeedSystemData</c>.
/// </remarks>
public static class SeedData
{
    /// <summary>
    /// Aplica los datos iniciales del sistema al modelo de EF Core.
    /// </summary>
    /// <param name="modelBuilder">Constructor del modelo de EF Core.</param>
    /// <remarks>
    /// Siembra 14 entidades del sistema:
    /// <list type="bullet">
    ///   <item>
    ///     <description>9 categorías con IDs deterministas del rango 11111111-1111-1111-1111-00000000000X.</description>
    ///   </item>
    ///   <item>
    ///     <description>5 métodos de pago con IDs deterministas del rango 22222222-2222-2222-2222-00000000000X.</description>
    ///   </item>
    ///   <item>
    ///     <description>Todas las entidades tienen <c>IsSystem = true</c> y <c>CreatedAt</c>/<c>UpdatedAt</c> fijados en <c>2026-01-01 00:00:00 UTC</c>.</description>
    ///   </item>
    /// </list>
    /// Este seed se materializa en la migración <c>SeedSystemData</c>.
    /// </remarks>
    public static void Apply(ModelBuilder modelBuilder)
    {
        var fixedTimestamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<Category>().HasData(
            new Category { Id = new Guid("11111111-1111-1111-1111-000000000001"), Name = "Comida", Color = "#E67E22", Icon = "🍔", IsSystem = true, CreatedAt = fixedTimestamp, UpdatedAt = fixedTimestamp },
            new Category { Id = new Guid("11111111-1111-1111-1111-000000000002"), Name = "Transporte", Color = "#3498DB", Icon = "🚗", IsSystem = true, CreatedAt = fixedTimestamp, UpdatedAt = fixedTimestamp },
            new Category { Id = new Guid("11111111-1111-1111-1111-000000000003"), Name = "Hogar", Color = "#9B59B6", Icon = "🏠", IsSystem = true, CreatedAt = fixedTimestamp, UpdatedAt = fixedTimestamp },
            new Category { Id = new Guid("11111111-1111-1111-1111-000000000004"), Name = "Salud", Color = "#1ABC9C", Icon = "💊", IsSystem = true, CreatedAt = fixedTimestamp, UpdatedAt = fixedTimestamp },
            new Category { Id = new Guid("11111111-1111-1111-1111-000000000005"), Name = "Ocio", Color = "#E74C3C", Icon = "🎮", IsSystem = true, CreatedAt = fixedTimestamp, UpdatedAt = fixedTimestamp },
            new Category { Id = new Guid("11111111-1111-1111-1111-000000000006"), Name = "Educación", Color = "#F39C12", Icon = "📚", IsSystem = true, CreatedAt = fixedTimestamp, UpdatedAt = fixedTimestamp },
            new Category { Id = new Guid("11111111-1111-1111-1111-000000000007"), Name = "Ropa", Color = "#16A085", Icon = "👕", IsSystem = true, CreatedAt = fixedTimestamp, UpdatedAt = fixedTimestamp },
            new Category { Id = new Guid("11111111-1111-1111-1111-000000000008"), Name = "Servicios", Color = "#34495E", Icon = "💡", IsSystem = true, CreatedAt = fixedTimestamp, UpdatedAt = fixedTimestamp },
            new Category { Id = new Guid("11111111-1111-1111-1111-000000000009"), Name = "Otros", Color = "#95A5A6", Icon = null, IsSystem = true, CreatedAt = fixedTimestamp, UpdatedAt = fixedTimestamp });

        modelBuilder.Entity<PaymentMethod>().HasData(
            new PaymentMethod { Id = new Guid("22222222-2222-2222-2222-000000000001"), Name = "Efectivo", IsSystem = true, CreatedAt = fixedTimestamp, UpdatedAt = fixedTimestamp },
            new PaymentMethod { Id = new Guid("22222222-2222-2222-2222-000000000002"), Name = "Tarjeta de débito", IsSystem = true, CreatedAt = fixedTimestamp, UpdatedAt = fixedTimestamp },
            new PaymentMethod { Id = new Guid("22222222-2222-2222-2222-000000000003"), Name = "Tarjeta de crédito", IsSystem = true, CreatedAt = fixedTimestamp, UpdatedAt = fixedTimestamp },
            new PaymentMethod { Id = new Guid("22222222-2222-2222-2222-000000000004"), Name = "Transferencia", IsSystem = true, CreatedAt = fixedTimestamp, UpdatedAt = fixedTimestamp },
            new PaymentMethod { Id = new Guid("22222222-2222-2222-2222-000000000005"), Name = "Otro", IsSystem = true, CreatedAt = fixedTimestamp, UpdatedAt = fixedTimestamp });
    }
}
