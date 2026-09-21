using ControlDeFinanzasIA.Domain.Goals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControlDeFinanzasIA.Infrastructure.Persistence.Configurations;

public sealed class GoalContributionConfiguration : IEntityTypeConfiguration<GoalContribution>
{
    public void Configure(EntityTypeBuilder<GoalContribution> builder)
    {
        builder.ToTable("GoalContributions");
        builder.HasKey(contribution => contribution.Id);
        builder.Property(contribution => contribution.Id).ValueGeneratedNever();
        builder.Property(contribution => contribution.GoalId).IsRequired();
        builder.Property(contribution => contribution.Date).IsRequired();

        builder.OwnsOne(contribution => contribution.Amount, money =>
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

        // A contribution cannot outlive its goal (D-8: DeleteGoalUseCase stays a thin
        // wrapper only because this cascade exists).
        builder.HasOne<Goal>()
            .WithMany()
            .HasForeignKey(contribution => contribution.GoalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(contribution => contribution.GoalId);
    }
}
