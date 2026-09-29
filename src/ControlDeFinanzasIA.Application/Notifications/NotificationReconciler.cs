using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Domain.Notifications;

namespace ControlDeFinanzasIA.Application.Notifications;

/// <summary>
/// Reconcilia las filas <see cref="Notification"/> existentes contra los
/// candidatos producidos por la corrida de detección actual (F-017, R-1,
/// R-5, R-7, R-9, D-4). Puro: no hace I/O, no tiene DI, no lanza
/// excepciones propias — el único punto de mutación es
/// <see cref="Notification.RefreshFacts"/> sobre las filas que van a
/// <see cref="NotificationReconciliationPlan.ToUpdate"/>.
/// </summary>
public static class NotificationReconciler
{
    /// <summary>
    /// Días de retención (R-9): toda fila cuyo <see cref="Notification.ReferenceDate"/>
    /// sea anterior a <c>today - RetentionDays</c> se purga al inicio de
    /// cada corrida, esté o no descartada. Una fila a exactamente
    /// <see cref="RetentionDays"/> días se conserva.
    /// </summary>
    public const int RetentionDays = 60;

    /// <summary>
    /// Calcula el plan de reconciliación (D-4), en este orden:
    /// <list type="number">
    /// <item>Purga las filas existentes cuyo <see cref="Notification.ReferenceDate"/>
    /// sea anterior a <paramref name="today"/> menos <see cref="RetentionDays"/> días
    /// (descartadas o no).</item>
    /// <item>Para cada candidato: una fila descartada con la misma
    /// <see cref="Notification.DedupKey"/> se ignora (nunca se recrea ni
    /// se modifica); una fila no descartada con hechos distintos se
    /// refresca (<see cref="Notification.RefreshFacts"/>) preservando
    /// <c>Id</c>/<c>CreatedAt</c>/<c>ReadAt</c>; una fila no descartada con
    /// los mismos hechos no genera cambios; si no existe fila, el
    /// candidato se agrega.</item>
    /// <item>Toda fila sobreviviente no descartada cuya clave no sea un
    /// candidato de esta corrida, no esté en <paramref name="protectedKeys"/>,
    /// y cuyo <see cref="Notification.Kind"/> esté en
    /// <paramref name="evaluatedKinds"/>, se elimina. Un tipo desactivado se
    /// evalúa con cero candidatos, así que todas sus filas no descartadas
    /// se eliminan (S-7).</item>
    /// </list>
    /// </summary>
    /// <param name="existing">Filas <see cref="Notification"/> existentes (incluye descartadas).</param>
    /// <param name="candidates">Candidatos producidos por las fuentes evaluadas en esta corrida.</param>
    /// <param name="evaluatedKinds">
    /// Tipos evaluados en esta corrida (habilitados o no — un tipo desactivado igual se evalúa,
    /// con cero candidatos, para que sus filas huérfanas se eliminen).
    /// </param>
    /// <param name="protectedKeys">
    /// Claves que se conservan aunque no tengan candidato en esta corrida (p. ej. presupuestos
    /// indeterminados por falta de tipo de cambio: "no se puede evaluar" no es "ya no califica").
    /// </param>
    /// <param name="today">Fecha de referencia de la corrida, para calcular la ventana de purga.</param>
    /// <param name="digestDue">
    /// Indica que esta corrida genera el resumen periódico (D-12, R-13). Cuando es
    /// <see langword="true"/> y hay un candidato <see cref="NotificationKind.Digest"/>, toda otra fila
    /// de resumen no descartada se elimina aunque <see cref="NotificationKind.Digest"/> no figure en <paramref name="evaluatedKinds"/>.
    /// </param>
    /// <returns>El <see cref="NotificationReconciliationPlan"/> a aplicar en una única <c>SaveChangesAsync</c>.</returns>
    public static NotificationReconciliationPlan Plan(
        IReadOnlyList<Notification> existing,
        IReadOnlyList<Notification> candidates,
        IReadOnlySet<NotificationKind> evaluatedKinds,
        IReadOnlySet<string> protectedKeys,
        DateOnly today,
        bool digestDue)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(evaluatedKinds);
        ArgumentNullException.ThrowIfNull(protectedKeys);

        var toAdd = new List<Notification>();
        var toUpdate = new List<Notification>();
        var toRemove = new List<Guid>();

        DateOnly purgeCutoff = today.AddDays(-RetentionDays);
        var survivors = new Dictionary<string, Notification>(StringComparer.Ordinal);

        foreach (Notification row in existing)
        {
            if (row.ReferenceDate < purgeCutoff)
            {
                toRemove.Add(row.Id);
                continue;
            }

            survivors[row.DedupKey] = row;
        }

        var candidateKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (Notification candidate in candidates)
        {
            candidateKeys.Add(candidate.DedupKey);

            if (!survivors.TryGetValue(candidate.DedupKey, out Notification? survivor))
            {
                toAdd.Add(candidate);
                continue;
            }

            if (survivor.IsDismissed)
            {
                continue;
            }

            if (!survivor.HasSameFacts(candidate))
            {
                survivor.RefreshFacts(candidate);
                toUpdate.Add(survivor);
            }
        }

        // A newly generated digest supersedes every other undismissed digest (R-13, D-4).
        bool digestSupersedes = digestDue && candidates.Any(c => c.Kind == NotificationKind.Digest);

        foreach (Notification row in survivors.Values)
        {
            if (row.IsDismissed || candidateKeys.Contains(row.DedupKey))
            {
                continue;
            }

            bool supersededDigest = digestSupersedes && row.Kind == NotificationKind.Digest;
            if (!supersededDigest && !evaluatedKinds.Contains(row.Kind))
            {
                continue;
            }

            if (protectedKeys.Contains(row.DedupKey))
            {
                continue;
            }

            toRemove.Add(row.Id);
        }

        return new NotificationReconciliationPlan(toAdd, toUpdate, toRemove);
    }
}
