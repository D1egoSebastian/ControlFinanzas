using ControlDeFinanzasIA.Domain.Alerts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControlDeFinanzasIA.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuracion EF Core de <see cref="AnomalyAlert"/> (F-014, D-4).
/// </summary>
/// <remarks>
/// <see cref="AnomalyAlert.Severity"/> se persiste como texto
/// (<c>HasConversion&lt;string&gt;()</c>), una desviacion deliberada de la
/// convencion int-enum usada en las otras 8 configuraciones del proyecto:
/// esta tabla es el contrato de la Fase 17 y un string sobrevive a un
/// reordenamiento del enum y es legible en una inspeccion cruda de la BD.
/// No existe ningun owned value object en esta entidad, por lo que el fix
/// de <c>BudgetRepository.UpdateAsync</c> para navegaciones OwnsOne no
/// aplica aqui.
/// </remarks>
public sealed class AnomalyAlertConfiguration : IEntityTypeConfiguration<AnomalyAlert>
{
    public void Configure(EntityTypeBuilder<AnomalyAlert> builder)
    {
        builder.ToTable("AnomalyAlerts");
        builder.HasKey(alert => alert.Id);
        builder.Property(alert => alert.Id).ValueGeneratedNever();
        builder.Property(alert => alert.TransactionId).IsRequired();
        builder.Property(alert => alert.CategoryId).IsRequired();
        builder.Property(alert => alert.Date).IsRequired();
        builder.Property(alert => alert.Severity).IsRequired().HasConversion<string>().HasMaxLength(16);
        builder.Property(alert => alert.Message).IsRequired().HasMaxLength(AnomalyAlert.MaxMessageLength);
        builder.Property(alert => alert.DetectedAt).IsRequired();
        builder.Ignore(alert => alert.IsAcknowledged);

        // Idempotency key (D-8): DetectAnomaliesUseCase relies on this unique index to
        // never create a second alert for the same expense.
        builder.HasIndex(alert => alert.TransactionId).IsUnique();

        // Month-scoped reconcile query (GetByMonthAsync).
        builder.HasIndex(alert => alert.Date);

        // Deliberately NO HasOne<Expense>() -- D-8: the alert must survive the
        // deletion of the Expense that originated it until the next reconcile.
    }
}
