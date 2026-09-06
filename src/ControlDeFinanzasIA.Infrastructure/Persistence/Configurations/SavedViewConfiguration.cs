using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControlDeFinanzasIA.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configura el mapeo de <see cref="SavedView" /> a la tabla SQLite
/// <c>SavedViews</c> siguiendo las convenciones de
/// <c>docs/architecture.md</c> §8 (mapeo Fluent API en Infrastructure).
/// </summary>
public sealed class SavedViewConfiguration : IEntityTypeConfiguration<SavedView>
{
    /// <summary>
    /// Configura la tabla, clave primaria, longitudes máximas,
    /// columnas requeridas, valores por defecto e índices sobre
    /// <see cref="SavedView.Name"/> y <see cref="SavedView.EntityType"/>.
    /// </summary>
    /// <param name="b">Constructor de la entidad <see cref="SavedView" />.</param>
    public void Configure(EntityTypeBuilder<SavedView> b)
    {
        b.ToTable("SavedViews");
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).ValueGeneratedNever();
        b.Property(s => s.Name).IsRequired().HasMaxLength(100);
        b.HasIndex(s => s.Name).IsUnique();
        b.Property(s => s.EntityType).IsRequired().HasConversion<int>();
        b.HasIndex(s => s.EntityType).HasDatabaseName("IX_SavedViews_EntityType");
        b.Property(s => s.FilterJson).IsRequired().HasColumnType("TEXT");
        b.Property(s => s.SchemaVersion).IsRequired().HasDefaultValue(1);
        b.Property(s => s.CreatedAt).IsRequired();
        b.Property(s => s.UpdatedAt).IsRequired();
    }
}
