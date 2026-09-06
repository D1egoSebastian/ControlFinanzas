using ControlDeFinanzasIA.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControlDeFinanzasIA.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configura el mapeo de <see cref="Tag" /> a la tabla SQLite
/// <c>Tags</c> siguiendo las convenciones de
/// <c>docs/architecture.md</c> §8 (mapeo Fluent API en Infrastructure).
/// </summary>
public sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    /// <summary>
    /// Configura la tabla, clave primaria, longitudes máximas,
    /// columnas requeridas e índice único sobre <see cref="Tag.Name"/>.
    /// </summary>
    /// <param name="b">Constructor de la entidad <see cref="Tag" />.</param>
    public void Configure(EntityTypeBuilder<Tag> b)
    {
        b.ToTable("Tags");
        b.HasKey(t => t.Id);
        b.Property(t => t.Id).ValueGeneratedNever();
        b.Property(t => t.Name).IsRequired().HasMaxLength(50);
        b.HasIndex(t => t.Name).IsUnique();
        b.Property(t => t.Color).HasMaxLength(9);
        b.Property(t => t.CreatedAt).IsRequired();
    }
}
