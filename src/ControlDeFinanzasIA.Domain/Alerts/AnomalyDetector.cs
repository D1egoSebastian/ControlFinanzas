namespace ControlDeFinanzasIA.Domain.Alerts;

/// <summary>
/// Un gasto historico ya usado para calcular la mediana/MAD de una
/// categoria. El monto DEBE venir ya normalizado a DOP por el caller
/// (D7); <see cref="AnomalyDetector"/> no realiza conversion de moneda.
/// </summary>
public readonly record struct AnomalySample(Guid CategoryId, DateOnly Date, decimal AmountDop);

/// <summary>
/// Un gasto candidato a evaluar contra el historial de su categoria.
/// El monto DEBE venir ya normalizado a DOP por el caller (D7).
/// </summary>
public readonly record struct AnomalyCandidate(Guid ExpenseId, Guid CategoryId, DateOnly Date, decimal AmountDop);

/// <summary>Regla que produjo un <see cref="AnomalyFinding"/>.</summary>
public enum AnomalyRule
{
    /// <summary>Z-score robusto (<c>0.6745·(x − median)/MAD</c>), <c>MAD &gt; 0</c>.</summary>
    RobustZScore = 0,

    /// <summary>Fallback por razon a la mediana, usado unicamente cuando <c>MAD = 0</c>.</summary>
    MedianRatio = 1
}

/// <summary>
/// Anomalia detectada para un gasto candidato (F-014, R-2).
/// </summary>
/// <param name="RatioToMedian">
/// <c>round(x / median, 2)</c>. Definida para AMBAS reglas — a
/// diferencia de <paramref name="RobustZScore"/>, que es <see langword="null"/>
/// bajo la regla de razon — por lo que es la clave de orden usada por
/// <see cref="AnomalyDetector.Detect"/> (D-1 punto 7).
/// </param>
/// <param name="RobustZScore">
/// Score robusto ya redondeado a <see cref="AnomalyDetector.ScoreDecimals"/>
/// decimales; <see langword="null"/> cuando <see cref="Rule"/> es
/// <see cref="AnomalyRule.MedianRatio"/>.
/// </param>
public readonly record struct AnomalyFinding(
    Guid ExpenseId,
    Guid CategoryId,
    DateOnly Date,
    decimal AmountDop,
    decimal MedianDop,
    decimal RatioToMedian,
    decimal? RobustZScore,
    AnomalyRule Rule,
    AnomalySeverity Severity);

/// <summary>
/// Servicio de dominio puro (F-014, R-2) que detecta gastos atipicos por
/// categoria mediante el z-score robusto (mediana/MAD). No realiza I/O,
/// no resuelve dependencias via DI y no realiza conversion de moneda:
/// recibe montos ya normalizados a DOP por el caller (D7), exactamente
/// como <c>Domain.Budgets.BudgetEvaluator</c>.
/// </summary>
/// <remarks>
/// Clase estatica, mismo patron que <c>BudgetEvaluator</c> /
/// <c>SavingsPlanCalculator</c>. No conoce ventanas de fechas: el
/// caller ya restringe <paramref name="history"/> a los 6 meses
/// completos previos al mes seleccionado (D-1); eso mantiene la tabla
/// de tests libre de razonamiento sobre fechas relativas a "hoy".
/// </remarks>
public static class AnomalyDetector
{
    /// <summary>Constante de consistencia de Iglewicz-Hoaglin para el z-score robusto.</summary>
    public const decimal ConsistencyConstant = 0.6745m;

    /// <summary>Umbral minimo (inclusive) del score redondeado para marcar una anomalia.</summary>
    public const decimal FlagThreshold = 3.5m;

    /// <summary>Umbral (inclusive) a partir del cual la severidad pasa a <see cref="AnomalySeverity.Warning"/>.</summary>
    public const decimal WarningThreshold = 5m;

    /// <summary>Umbral (inclusive) a partir del cual la severidad pasa a <see cref="AnomalySeverity.Critical"/>.</summary>
    public const decimal CriticalThreshold = 8m;

    /// <summary>
    /// Multiplo de la mediana (inclusive) que marca una anomalia bajo la
    /// regla de razon, usada unicamente cuando <c>MAD = 0</c>.
    /// </summary>
    public const decimal MedianRatioThreshold = 3m;

    /// <summary>Minimo de meses distintos con historial para que una categoria sea elegible.</summary>
    public const int MinHistoryMonths = 3;

    /// <summary>Minimo de gastos historicos para que una categoria sea elegible.</summary>
    public const int MinHistoryCount = 8;

    /// <summary>
    /// Ventana de meses completos previos usada por el caller para
    /// construir <c>history</c>. El detector mismo no aplica ninguna
    /// ventana de fechas; esta constante documenta el contrato.
    /// </summary>
    public const int HistoryWindowMonths = 6;

    /// <summary>Decimales usados al redondear tanto el score robusto como la razon a la mediana.</summary>
    public const int ScoreDecimals = 2;

    /// <summary>
    /// Detecta anomalias por categoria. Una categoria es elegible solo
    /// con <c>count &gt;= 8 &amp;&amp; distinctMonths &gt;= 3 &amp;&amp; median &gt; 0</c>
    /// (D-1 punto 3); los candidatos de una categoria no elegible (o sin
    /// historial alguno) no producen ninguna entrada. El umbral se
    /// aplica sobre el score YA redondeado, nunca sobre el valor crudo
    /// (mismo criterio que <c>BudgetEvaluator</c>). La deteccion es
    /// unilateral: un score negativo (un gasto barato) nunca se marca.
    /// </summary>
    /// <param name="history">
    /// Gastos historicos ya restringidos por el caller a los 6 meses
    /// completos previos al mes evaluado, ya normalizados a DOP.
    /// </param>
    /// <param name="candidates">Gastos candidatos del mes evaluado, ya normalizados a DOP.</param>
    /// <returns>
    /// Las anomalias marcadas, ordenadas por <c>Severity</c> descendente,
    /// luego <c>RatioToMedian</c> descendente, luego <c>Date</c>
    /// descendente, luego <c>ExpenseId</c> ascendente (D-1 punto 7).
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Se lanza si <paramref name="history"/> o <paramref name="candidates"/> son <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Se lanza si algun <c>AmountDop</c> de <paramref name="history"/> o
    /// <paramref name="candidates"/> es negativo.
    /// </exception>
    public static IReadOnlyList<AnomalyFinding> Detect(
        IReadOnlyList<AnomalySample> history, IReadOnlyList<AnomalyCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(candidates);

        foreach (AnomalySample sample in history)
        {
            if (sample.AmountDop < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(history), sample.AmountDop, "AmountDop must be non-negative.");
            }
        }

        foreach (AnomalyCandidate candidate in candidates)
        {
            if (candidate.AmountDop < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(candidates), candidate.AmountDop, "AmountDop must be non-negative.");
            }
        }

        Dictionary<Guid, CategoryStats> statsByCategory = BuildCategoryStats(history);
        List<AnomalyFinding> findings = [];

        foreach (AnomalyCandidate candidate in candidates)
        {
            if (!statsByCategory.TryGetValue(candidate.CategoryId, out CategoryStats? stats) || !stats.IsEligible)
            {
                continue;
            }

            decimal ratio = Math.Round(
                candidate.AmountDop / stats.Median, ScoreDecimals, MidpointRounding.AwayFromZero);

            if (stats.Mad > 0)
            {
                decimal rawZ = ConsistencyConstant * (candidate.AmountDop - stats.Median) / stats.Mad;
                decimal z = Math.Round(rawZ, ScoreDecimals, MidpointRounding.AwayFromZero);

                // One-sided (D-1 point 5): a negative z is always < FlagThreshold, so this
                // single comparison also excludes cheap-side outliers without a separate check.
                if (z < FlagThreshold)
                {
                    continue;
                }

                AnomalySeverity severity =
                    z >= CriticalThreshold ? AnomalySeverity.Critical :
                    z >= WarningThreshold ? AnomalySeverity.Warning :
                    AnomalySeverity.Info;

                findings.Add(new AnomalyFinding(
                    candidate.ExpenseId, candidate.CategoryId, candidate.Date, candidate.AmountDop,
                    stats.Median, ratio, z, AnomalyRule.RobustZScore, severity));
            }
            else
            {
                // MAD = 0 fallback (D-1 point 4, spec S-11): never divides by mad.
                if (candidate.AmountDop < stats.Median * MedianRatioThreshold)
                {
                    continue;
                }

                findings.Add(new AnomalyFinding(
                    candidate.ExpenseId, candidate.CategoryId, candidate.Date, candidate.AmountDop,
                    stats.Median, ratio, null, AnomalyRule.MedianRatio, AnomalySeverity.Warning));
            }
        }

        return [.. findings
            .OrderByDescending(f => f.Severity)
            .ThenByDescending(f => f.RatioToMedian)
            .ThenByDescending(f => f.Date)
            .ThenBy(f => f.ExpenseId)];
    }

    private static Dictionary<Guid, CategoryStats> BuildCategoryStats(IReadOnlyList<AnomalySample> history)
    {
        Dictionary<Guid, CategoryStats> result = [];

        foreach (IGrouping<Guid, AnomalySample> group in history.GroupBy(s => s.CategoryId))
        {
            List<decimal> amounts = [.. group.Select(s => s.AmountDop)];
            int distinctMonths = group.Select(s => (s.Date.Year, s.Date.Month)).Distinct().Count();
            decimal median = Median(amounts);
            List<decimal> deviations = [.. amounts.Select(a => Math.Abs(a - median))];
            decimal mad = Median(deviations);

            result[group.Key] = new CategoryStats(amounts.Count, distinctMonths, median, mad);
        }

        return result;
    }

    /// <summary>
    /// Mediana de una lista: para conteo impar, el elemento central;
    /// para conteo par, el promedio de los dos elementos centrales.
    /// </summary>
    private static decimal Median(List<decimal> values)
    {
        List<decimal> sorted = [.. values.OrderBy(v => v)];
        int count = sorted.Count;

        return count % 2 == 1
            ? sorted[count / 2]
            : (sorted[(count / 2) - 1] + sorted[count / 2]) / 2m;
    }

    private sealed record CategoryStats(int Count, int DistinctMonths, decimal Median, decimal Mad)
    {
        public bool IsEligible => Count >= MinHistoryCount && DistinctMonths >= MinHistoryMonths && Median > 0;
    }
}
