using ControlDeFinanzasIA.Domain.Goals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControlDeFinanzasIA.Infrastructure.Persistence.Configurations;

public sealed class GoalConfiguration : IEntityTypeConfiguration<Goal>
{
    public void Configure(EntityTypeBuilder<Goal> builder)
    {
        builder.ToTable("Goals");
        builder.HasKey(goal => goal.Id);
        builder.Property(goal => goal.Id).ValueGeneratedNever();
        builder.Property(goal => goal.Name).IsRequired().HasMaxLength(Goal.MaxNameLength);
        builder.Property(goal => goal.TargetDate).IsRequired();
        builder.Property(goal => goal.Note).HasMaxLength(Goal.MaxNoteLength);
        builder.Property(goal => goal.Currency).IsRequired().HasConversion<int>();
        builder.Property(goal => goal.Status).IsRequired().HasConversion<int>();

        builder.OwnsOne(goal => goal.TargetAmount, money =>
        {
            money.Property(value => value.Amount)
                .HasColumnName("TargetAmount")
                .HasPrecision(18, 4)
                .IsRequired();
            money.Property(value => value.Currency)
                .HasColumnName("TargetAmountCurrency")
                .HasConversion<int>()
                .IsRequired();
        });

        builder.HasIndex(goal => goal.Status);
    }
}
