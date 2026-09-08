using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControlDeFinanzasIA.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configura el mapeo de <see cref="Income" /> a la tabla SQLite
/// <c>Incomes</c> siguiendo las convenciones de
/// <c>docs/architecture.md</c> §8 (mapeo Fluent API en Infrastructure)
/// y la especificación de F-001 de la Fase 4.
/// </summary>
public sealed class IncomeConfiguration : IEntityTypeConfiguration<Income>
{
    /// <summary>
    /// Configura la tabla, clave primaria, columnas requeridas,
    /// longitudes máximas, foreign key a <see cref="Category" />,
    /// índices y la descomposición de <see cref="Money" /> vía
    /// <c>OwnsOne</c>.
    /// </summary>
    /// <param name="b">Constructor de la entidad <see cref="Income" />.</param>
    public void Configure(EntityTypeBuilder<Income> b)
    {
        b.ToTable("Incomes");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedNever();

        b.Property(e => e.CategoryId);
        b.Property(e => e.Date).IsRequired();
        b.Property(e => e.Description).IsRequired().HasMaxLength(200);
        b.Property(e => e.Note).HasMaxLength(500);
        b.Property(e => e.Source).HasMaxLength(50);
        b.Property(e => e.PaymentMethodId);
        b.Property(e => e.CreatedAt).IsRequired();
        b.Property(e => e.UpdatedAt).IsRequired();

        b.HasOne<Category>()
            .WithMany()
            .HasForeignKey(e => e.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasOne<PaymentMethod>()
            .WithMany()
            .HasForeignKey(e => e.PaymentMethodId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(e => e.Date);
        b.HasIndex(e => e.CategoryId);
        b.HasIndex(e => e.PaymentMethodId);

        b.OwnsOne(e => e.Amount, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("Amount")
                .IsRequired()
                .HasPrecision(18, 4);

            money.Property(m => m.Currency)
                .HasColumnName("Currency")
                .IsRequired();
        });

        // Relación many-to-many entre ingresos y tags mediante la tabla
        // join IncomeTags. La PK es compuesta (IncomeId, TagId) y ambas
        // FKs tienen Cascade para mantener la integridad al borrar un
        // ingreso o un tag.
        b.HasMany(e => e.Tags)
            .WithMany()
            .UsingEntity<Dictionary<string, object>>(
                "IncomeTags",
                right => right.HasOne<Tag>()
                    .WithMany()
                    .HasForeignKey("TagId")
                    .OnDelete(DeleteBehavior.Cascade),
                left => left.HasOne<Income>()
                    .WithMany()
                    .HasForeignKey("IncomeId")
                    .OnDelete(DeleteBehavior.Cascade),
                join =>
                {
                    join.HasKey("IncomeId", "TagId");
                    join.HasIndex("TagId");
                    join.ToTable("IncomeTags");
                });
    }
}
