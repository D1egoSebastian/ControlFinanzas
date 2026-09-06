using ControlDeFinanzasIA.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControlDeFinanzasIA.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configura el mapeo de <see cref="Category" /> a la tabla SQLite
/// <c>Categories</c> siguiendo las convenciones de
/// <c>docs/architecture.md</c> §8 (mapeo Fluent API en Infrastructure).
/// </summary>
public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    /// <summary>
    /// Configura la tabla, clave primaria, longitudes máximas,
    /// columnas requeridas e índice único con collation <c>NOCASE</c>.
    /// </summary>
    /// <param name="b">Constructor de la entidad <see cref="Category" />.</param>
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.ToTable("Categories");
        b.HasKey(c => c.Id);
        b.Property(c => c.Id).ValueGeneratedNever();
        b.Property(c => c.Name).IsRequired().HasMaxLength(100).UseCollation("NOCASE");
        b.HasIndex(c => c.Name).IsUnique();
        b.Property(c => c.IsSystem).IsRequired();
        b.Property(c => c.CreatedAt).IsRequired();
        b.Property(c => c.UpdatedAt).IsRequired();
        b.Property(c => c.Color).HasMaxLength(9);
        b.Property(c => c.Icon).HasMaxLength(50);
    }
}
