using ControlDeFinanzasIA.Domain.Debts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControlDeFinanzasIA.Infrastructure.Persistence.Configurations;

public sealed class DebtConfiguration : IEntityTypeConfiguration<Debt>
{
    public void Configure(EntityTypeBuilder<Debt> builder)
    {
        builder.ToTable("Debts");
        builder.HasKey(debt => debt.Id);
        builder.Property(debt => debt.Id).ValueGeneratedNever();
        builder.Property(debt => debt.Description).IsRequired().HasMaxLength(Debt.MaxDescriptionLength);
        builder.Property(debt => debt.Currency).IsRequired().HasConversion<int>();
        builder.Property(debt => debt.TotalInstallments).IsRequired();
        builder.Property(debt => debt.Frequency).IsRequired().HasMaxLength(20);
        builder.Property(debt => debt.StartDate).IsRequired();
        builder.Property(debt => debt.CounterpartyName).HasMaxLength(Debt.MaxCounterpartyNameLength);
        builder.Property(debt => debt.Note).HasMaxLength(Debt.MaxNoteLength);
        builder.Property(debt => debt.Status).IsRequired().HasConversion<int>();

        builder.OwnsOne(debt => debt.OriginalAmount, money =>
        {
            money.Property(value => value.Amount)
                .HasColumnName("OriginalAmount")
                .HasPrecision(18, 4)
                .IsRequired();
            money.Property(value => value.Currency)
                .HasColumnName("OriginalAmountCurrency")
                .HasConversion<int>()
                .IsRequired();
        });

        builder.OwnsOne(debt => debt.InstallmentAmount, money =>
        {
            money.Property(value => value.Amount)
                .HasColumnName("InstallmentAmount")
                .HasPrecision(18, 4)
                .IsRequired();
            money.Property(value => value.Currency)
                .HasColumnName("InstallmentAmountCurrency")
                .HasConversion<int>()
                .IsRequired();
        });

        builder.HasIndex(debt => debt.Status);
    }
}
