using System.Text.Json;
using ControlDeFinanzasIA.Domain.Recurring;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControlDeFinanzasIA.Infrastructure.Persistence.Configurations;

public sealed class RecurringRuleConfiguration : IEntityTypeConfiguration<RecurringRule>
{
    public void Configure(EntityTypeBuilder<RecurringRule> builder)
    {
        builder.ToTable("RecurringRules");
        builder.HasKey(rule => rule.Id);
        builder.Property(rule => rule.Id).ValueGeneratedNever();
        builder.Property(rule => rule.Name).IsRequired().HasMaxLength(RecurringRule.MaxNameLength);
        builder.Property(rule => rule.Currency).IsRequired().HasConversion<int>();
        builder.Property(rule => rule.Frequency).IsRequired().HasMaxLength(20);
        builder.Property(rule => rule.StartDate).IsRequired();
        builder.Property(rule => rule.EndDate);
        builder.Property(rule => rule.CategoryId).IsRequired();
        builder.Property(rule => rule.PaymentMethodId).IsRequired();
        builder.Property(rule => rule.Active).IsRequired();
        builder.Property(rule => rule.NextGenerationDate).IsRequired();
        builder.Property(rule => rule.TagIds)
            .HasConversion(
                tags => JsonSerializer.Serialize(tags, (JsonSerializerOptions?)null),
                json => JsonSerializer.Deserialize<List<Guid>>(json, (JsonSerializerOptions?)null) ?? new List<Guid>(),
                new ValueComparer<IReadOnlyList<Guid>>(
                    (left, right) => left!.SequenceEqual(right!),
                    value => value!.Aggregate(0, (hash, tag) => HashCode.Combine(hash, tag)),
                    value => value!.ToList()))
            .IsRequired();

        builder.HasDiscriminator<string>("RuleType")
            .HasValue<Subscription>("Subscription")
            .HasValue<FixedExpense>("FixedExpense");

        builder.OwnsOne(rule => rule.Amount, money =>
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

        builder.HasIndex(rule => rule.NextGenerationDate);
        builder.HasIndex(rule => rule.Active);
    }
}
