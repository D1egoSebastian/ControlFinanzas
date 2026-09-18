using ControlDeFinanzasIA.Domain.Debts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControlDeFinanzasIA.Infrastructure.Persistence.Configurations;

public sealed class DebtPaymentConfiguration : IEntityTypeConfiguration<DebtPayment>
{
    public void Configure(EntityTypeBuilder<DebtPayment> builder)
    {
        builder.ToTable("DebtPayments");
        builder.HasKey(payment => payment.Id);
        builder.Property(payment => payment.Id).ValueGeneratedNever();
        builder.Property(payment => payment.DebtId).IsRequired();
        builder.Property(payment => payment.InstallmentNumber).IsRequired();
        builder.Property(payment => payment.ScheduledDate).IsRequired();
        builder.Property(payment => payment.PaidDate);
        builder.Property(payment => payment.Status).IsRequired().HasConversion<int>();

        builder.OwnsOne(payment => payment.Amount, money =>
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

        builder.HasOne<Debt>()
            .WithMany()
            .HasForeignKey(payment => payment.DebtId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(payment => payment.DebtId);
        builder.HasIndex(payment => payment.Status);
        builder.HasIndex(payment => new { payment.DebtId, payment.InstallmentNumber }).IsUnique();
    }
}
