using System.Text.Json;
using ControlDeFinanzasIA.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControlDeFinanzasIA.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuracion EF Core de <see cref="Notification"/> (F-017, D-1, D-8).
/// </summary>
/// <remarks>
/// <see cref="Notification.Kind"/>, <see cref="Notification.Tier"/> y
/// <see cref="Notification.DigestFrequency"/> se persisten como texto
/// (<c>HasConversion&lt;string&gt;()</c>), siguiendo el precedente de
/// <c>AnomalyAlertConfiguration.Severity</c>. No existe ningún owned value
/// object en esta entidad, por lo que el fix de
/// <c>BudgetRepository.UpdateAsync</c> para navegaciones OwnsOne no aplica
/// aquí.
/// </remarks>
public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(notification => notification.Id);
        builder.Property(notification => notification.Id).ValueGeneratedNever();
        builder.Property(notification => notification.Kind).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(notification => notification.DedupKey).IsRequired().HasMaxLength(128);
        builder.Property(notification => notification.SourceId);
        builder.Property(notification => notification.ReferenceDate).IsRequired();
        builder.Property(notification => notification.SubjectLabel)
            .IsRequired()
            .HasMaxLength(Notification.MaxLabelLength);
        builder.Property(notification => notification.Amount).HasPrecision(18, 4);
        builder.Property(notification => notification.Currency).HasConversion<int>();
        builder.Property(notification => notification.Percent).HasPrecision(18, 4);
        builder.Property(notification => notification.Tier).HasConversion<string>().HasMaxLength(16);
        builder.Property(notification => notification.DigestFrequency).HasConversion<string>().HasMaxLength(16);
        builder.Property(notification => notification.DigestPaymentCount);
        builder.Property(notification => notification.DigestBudgetCount);
        builder.Property(notification => notification.DigestAnomalyCount);

        // DigestTotals has no setter (only a private backing field), so field access is
        // required for EF to materialize/write it -- there is no other collection fact
        // column shaped like this in the project. Converter + comparer copy the
        // RecurringRuleConfiguration.TagIds JSON precedent.
        builder.Property(notification => notification.DigestTotals)
            .HasField("_digestTotals")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasConversion(
                totals => JsonSerializer.Serialize(totals, (JsonSerializerOptions?)null),
                json => JsonSerializer.Deserialize<List<NotificationCurrencyTotal>>(json, (JsonSerializerOptions?)null)
                    ?? new List<NotificationCurrencyTotal>(),
                new ValueComparer<IReadOnlyList<NotificationCurrencyTotal>>(
                    (left, right) => left!.SequenceEqual(right!),
                    value => value!.Aggregate(0, (hash, total) => HashCode.Combine(hash, total)),
                    value => value!.ToList()))
            .IsRequired();

        builder.Property(notification => notification.CreatedAt).IsRequired();
        builder.Property(notification => notification.ReadAt);
        builder.Property(notification => notification.DismissedAt);

        builder.Ignore(notification => notification.IsRead);
        builder.Ignore(notification => notification.IsDismissed);

        // Idempotency key (D-2): the reconciler relies on this unique index to never
        // create a second row for the same DedupKey.
        builder.HasIndex(notification => notification.DedupKey).IsUnique();

        // Purge (R-9) and flyout ordering (R-10).
        builder.HasIndex(notification => notification.ReferenceDate);

        // GetUndismissedAsync filter.
        builder.HasIndex(notification => notification.DismissedAt);

        // Deliberately NO HasOne<...>() to any source table -- D-8: a row must survive
        // the deletion/change of its source (payment, rule, budget, alert) until the
        // next reconcile.
    }
}
