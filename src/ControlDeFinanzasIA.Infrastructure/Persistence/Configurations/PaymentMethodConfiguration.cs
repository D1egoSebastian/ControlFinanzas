using ControlDeFinanzasIA.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControlDeFinanzasIA.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configura el mapeo de <see cref="PaymentMethod" /> a la tabla SQLite
/// <c>PaymentMethods</c> siguiendo las convenciones de
/// <c>docs/architecture.md</c> §8 (mapeo Fluent API en Infrastructure).
/// <see cref="PaymentMethod" /> no tiene representación visual, por lo que
/// no incluye <c>Color</c> ni <c>Icon</c>.
/// </summary>
public sealed class PaymentMethodConfiguration : IEntityTypeConfiguration<PaymentMethod>
{
    /// <summary>
    /// Configura la tabla, clave primaria, longitudes máximas,
    /// columnas requeridas e índice único con collation <c>NOCASE</c>.
    /// </summary>
    /// <param name="b">Constructor de la entidad <see cref="PaymentMethod" />.</param>
    public void Configure(EntityTypeBuilder<PaymentMethod> b)
    {
        b.ToTable("PaymentMethods");
        b.HasKey(p => p.Id);
        b.Property(p => p.Id).ValueGeneratedNever();
        b.Property(p => p.Name).IsRequired().HasMaxLength(100).UseCollation("NOCASE");
        b.HasIndex(p => p.Name).IsUnique();
        b.Property(p => p.IsSystem).IsRequired();
        b.Property(p => p.CreatedAt).IsRequired();
        b.Property(p => p.UpdatedAt).IsRequired();
    }
}
