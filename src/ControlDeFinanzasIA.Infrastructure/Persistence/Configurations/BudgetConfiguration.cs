using ControlDeFinanzasIA.Domain.Budgets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControlDeFinanzasIA.Infrastructure.Persistence.Configurations;

public sealed class BudgetConfiguration : IEntityTypeConfiguration<Budget>
{
    public void Configure(EntityTypeBuilder<Budget> builder)
    {
        builder.ToTable("Budgets");
        builder.HasKey(budget => budget.Id);
        builder.Property(budget => budget.Id).ValueGeneratedNever();
        builder.Property(budget => budget.Year).IsRequired();
        builder.Property(budget => budget.Month).IsRequired();
        builder.Property(budget => budget.CategoryId);
        builder.Property(budget => budget.Currency).IsRequired().HasConversion<int>();

        builder.OwnsOne(budget => budget.Amount, money =>
        {
            money.Property(value => value.Amount)
                .HasColumnName("Amount")
                .HasPrecision(18, 4)
                .IsRequired();
            money.Property(value => value.Currency)
                .HasColumnName("AmountCurrency")
                .HasConversion<int>()
                .IsRequired();
        });

        builder.HasIndex(budget => new { budget.Year, budget.Month, budget.CategoryId })
            .IsUnique();
    }
}
