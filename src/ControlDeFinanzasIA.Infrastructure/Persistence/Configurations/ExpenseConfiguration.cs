using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControlDeFinanzasIA.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configura el mapeo de <see cref="Expense" /> a la tabla SQLite
/// <c>Expenses</c> siguiendo las convenciones de
/// <c>docs/architecture.md</c> §8 (mapeo Fluent API en Infrastructure)
/// y la especificación de F-001 de la Fase 3.
/// </summary>
public sealed class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    /// <summary>
    /// Configura la tabla, clave primaria, columnas requeridas,
    /// longitudes máximas, foreign key a <see cref="Category" />,
    /// índices y la descomposición de <see cref="Money" /> vía
    /// <c>OwnsOne</c>.
    /// </summary>
    /// <param name="b">Constructor de la entidad <see cref="Expense" />.</param>
    public void Configure(EntityTypeBuilder<Expense> b)
    {
        b.ToTable("Expenses");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedNever();

        b.Property(e => e.CategoryId).IsRequired();
        b.Property(e => e.Date).IsRequired();
        b.Property(e => e.Description).IsRequired().HasMaxLength(200);
        b.Property(e => e.Note).HasMaxLength(500);
        b.Property(e => e.PaymentMethodId);
        b.Property(e => e.CreatedAt).IsRequired();
        b.Property(e => e.UpdatedAt).IsRequired();

        b.HasOne<Category>()
            .WithMany()
            .HasForeignKey(e => e.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

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

        // Relación many-to-many entre gastos y tags mediante la tabla
        // join ExpenseTags. La PK es compuesta (ExpenseId, TagId) y ambas
        // FKs tienen Cascade para mantener la integridad al borrar un
        // gasto o un tag.
        b.HasMany(e => e.Tags)
            .WithMany()
            .UsingEntity<Dictionary<string, object>>(
                "ExpenseTags",
                right => right.HasOne<Tag>()
                    .WithMany()
                    .HasForeignKey("TagId")
                    .OnDelete(DeleteBehavior.Cascade),
                left => left.HasOne<Expense>()
                    .WithMany()
                    .HasForeignKey("ExpenseId")
                    .OnDelete(DeleteBehavior.Cascade),
                join =>
                {
                    join.HasKey("ExpenseId", "TagId");
                    join.HasIndex("TagId");
                    join.ToTable("ExpenseTags");
                });
    }
}
