using ControlDeFinanzasIA.Domain.Alerts;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Domain.Alerts;

/// <summary>
/// Tests para <see cref="AnomalyDetector"/> (F-014, R-2, D-1): elegibilidad
/// por historial, la tabla resuelta del diseno (median 100 / MAD 5), el
/// fallback por razon cuando <c>MAD = 0</c>, unilateralidad, el umbral
/// exacto sobre el score redondeado, el orden de salida y las guardas de
/// division por cero.
/// </summary>
public sealed class AnomalyDetectorTests
{
    private static readonly Guid _categoryId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid _candidateExpenseId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly DateOnly _candidateDate = new(2026, 4, 10);

    // Design D-1 worked example: [100,100,110,90,100,120,80,100] -> median 100, MAD 5.
    private static readonly (DateOnly Date, decimal Amount)[] _workedExampleRows =
    [
        (new DateOnly(2026, 1, 5), 100m),
        (new DateOnly(2026, 1, 20), 100m),
        (new DateOnly(2026, 2, 5), 110m),
        (new DateOnly(2026, 2, 20), 90m),
        (new DateOnly(2026, 3, 5), 100m),
        (new DateOnly(2026, 3, 6), 120m),
        (new DateOnly(2026, 3, 7), 80m),
        (new DateOnly(2026, 3, 8), 100m)
    ];

    // Design D-1 MAD=0 table: eight x 500 -> median 500, MAD 0.
    private static readonly (DateOnly Date, decimal Amount)[] _madZeroRows =
    [
        (new DateOnly(2026, 1, 10), 500m),
        (new DateOnly(2026, 1, 20), 500m),
        (new DateOnly(2026, 2, 10), 500m),
        (new DateOnly(2026, 2, 20), 500m),
        (new DateOnly(2026, 3, 10), 500m),
        (new DateOnly(2026, 3, 15), 500m),
        (new DateOnly(2026, 3, 20), 500m),
        (new DateOnly(2026, 3, 25), 500m)
    ];

    // Same shape as the worked example scaled x4 around the same median: [100,100,140,60,100,180,20,100]
    // -> median 100, MAD 20. Built so a candidate exists whose ROUNDED score lands exactly on
    // AnomalyDetector.FlagThreshold (3.50).
    private static readonly (DateOnly Date, decimal Amount)[] _boundaryRows =
    [
        (new DateOnly(2026, 1, 10), 100m),
        (new DateOnly(2026, 1, 20), 100m),
        (new DateOnly(2026, 2, 10), 140m),
        (new DateOnly(2026, 2, 20), 60m),
        (new DateOnly(2026, 3, 10), 100m),
        (new DateOnly(2026, 3, 15), 180m),
        (new DateOnly(2026, 3, 20), 20m),
        (new DateOnly(2026, 3, 25), 100m)
    ];

    private static List<AnomalySample> BuildSamples(Guid categoryId, IEnumerable<(DateOnly Date, decimal Amount)> rows) =>
        [.. rows.Select(r => new AnomalySample(categoryId, r.Date, r.Amount))];

    private static AnomalyCandidate Candidate(decimal amount, DateOnly? date = null, Guid? expenseId = null) =>
        new(expenseId ?? _candidateExpenseId, _categoryId, date ?? _candidateDate, amount);

    // --- Eligibility (S-1: thin history never produces an anomaly, regardless of deviation) ---

    [Fact]
    public void Detect_FewerThan8Expenses_ProducesNoAnomalyRegardlessOfDeviation()
    {
        // 7 samples across 3 distinct months (count < MinHistoryCount).
        List<AnomalySample> history = BuildSamples(_categoryId, _workedExampleRows[..7]);

        IReadOnlyList<AnomalyFinding> findings = AnomalyDetector.Detect(history, [Candidate(100000m)]);

        Assert.Empty(findings);
    }

    [Fact]
    public void Detect_FewerThan3DistinctMonths_ProducesNoAnomalyRegardlessOfDeviation()
    {
        // 8 samples but only 2 distinct months (distinctMonths < MinHistoryMonths).
        (DateOnly Date, decimal Amount)[] rows =
        [
            (new DateOnly(2026, 1, 1), 100m),
            (new DateOnly(2026, 1, 5), 100m),
            (new DateOnly(2026, 1, 10), 110m),
            (new DateOnly(2026, 1, 15), 90m),
            (new DateOnly(2026, 2, 1), 100m),
            (new DateOnly(2026, 2, 5), 120m),
            (new DateOnly(2026, 2, 10), 80m),
            (new DateOnly(2026, 2, 15), 100m)
        ];
        List<AnomalySample> history = BuildSamples(_categoryId, rows);

        IReadOnlyList<AnomalyFinding> findings = AnomalyDetector.Detect(history, [Candidate(100000m)]);

        Assert.Empty(findings);
    }

    [Fact]
    public void Detect_MedianZero_ProducesNoAnomalyWithoutDividingByZero()
    {
        // count >= 8 and distinctMonths >= 3, but median == 0 -> ineligible (median > 0 required).
        // Also proves the MAD=0 ratio branch never runs against a zero median (would divide by zero).
        (DateOnly Date, decimal Amount)[] rows =
        [
            (new DateOnly(2026, 1, 10), 0m),
            (new DateOnly(2026, 1, 20), 0m),
            (new DateOnly(2026, 2, 10), 0m),
            (new DateOnly(2026, 2, 20), 0m),
            (new DateOnly(2026, 3, 10), 0m),
            (new DateOnly(2026, 3, 15), 0m),
            (new DateOnly(2026, 3, 20), 0m),
            (new DateOnly(2026, 3, 25), 0m)
        ];
        List<AnomalySample> history = BuildSamples(_categoryId, rows);

        IReadOnlyList<AnomalyFinding> findings = AnomalyDetector.Detect(history, [Candidate(999999m)]);

        Assert.Empty(findings);
    }

    // --- Worked example (design D-1): median 100, MAD 5 ---

    [Fact]
    public void Detect_WorkedExample_BelowThreshold_ProducesNoAnomaly()
    {
        List<AnomalySample> history = BuildSamples(_categoryId, _workedExampleRows);

        // z = 0.6745 * (125 - 100) / 5 = 3.3725 -> rounds to 3.37, below FlagThreshold.
        IReadOnlyList<AnomalyFinding> findings = AnomalyDetector.Detect(history, [Candidate(125m)]);

        Assert.Empty(findings);
    }

    [Fact]
    public void Detect_WorkedExample_InfoBand_FlagsWithRoundedScore()
    {
        List<AnomalySample> history = BuildSamples(_categoryId, _workedExampleRows);

        // z = 0.6745 * (130 - 100) / 5 = 4.047 -> rounds to 4.05, band [3.5,5).
        AnomalyFinding finding = Assert.Single(AnomalyDetector.Detect(history, [Candidate(130m)]));

        Assert.Equal(AnomalySeverity.Info, finding.Severity);
        Assert.Equal(AnomalyRule.RobustZScore, finding.Rule);
        Assert.Equal(4.05m, finding.RobustZScore);
        Assert.Equal(100m, finding.MedianDop);
        Assert.Equal(1.30m, finding.RatioToMedian);
    }

    [Fact]
    public void Detect_WorkedExample_WarningBand_FlagsWithRoundedScore()
    {
        List<AnomalySample> history = BuildSamples(_categoryId, _workedExampleRows);

        // z = 0.6745 * (145 - 100) / 5 = 6.0705 -> rounds to 6.07, band [5,8).
        AnomalyFinding finding = Assert.Single(AnomalyDetector.Detect(history, [Candidate(145m)]));

        Assert.Equal(AnomalySeverity.Warning, finding.Severity);
        Assert.Equal(AnomalyRule.RobustZScore, finding.Rule);
        Assert.Equal(6.07m, finding.RobustZScore);
    }

    [Fact]
    public void Detect_WorkedExample_CriticalBand_FlagsWithRoundedScore()
    {
        List<AnomalySample> history = BuildSamples(_categoryId, _workedExampleRows);

        // z = 0.6745 * (160 - 100) / 5 = 8.094 -> rounds to 8.09, band [8,inf).
        AnomalyFinding finding = Assert.Single(AnomalyDetector.Detect(history, [Candidate(160m)]));

        Assert.Equal(AnomalySeverity.Critical, finding.Severity);
        Assert.Equal(AnomalyRule.RobustZScore, finding.Rule);
        Assert.Equal(8.09m, finding.RobustZScore);
    }

    [Fact]
    public void Detect_NegativeZ_OneSidedNeverFlags()
    {
        List<AnomalySample> history = BuildSamples(_categoryId, _workedExampleRows);

        // z = 0.6745 * (20 - 100) / 5 = -10.792: a large NEGATIVE score (a cheap expense) must
        // never flag, regardless of magnitude (D-1 point 5).
        IReadOnlyList<AnomalyFinding> findings = AnomalyDetector.Detect(history, [Candidate(20m)]);

        Assert.Empty(findings);
    }

    [Fact]
    public void Detect_RoundedScoreExactlyAtFlagThreshold_Flags()
    {
        // History has median 100, MAD 20. Candidate 203.70 -> raw z = 3.4972825, which rounds
        // (AwayFromZero, 2 decimals) to EXACTLY 3.50 -- the threshold is applied on the rounded
        // value and is inclusive, so this must flag as Info, not fall just short.
        List<AnomalySample> history = BuildSamples(_categoryId, _boundaryRows);

        AnomalyFinding finding = Assert.Single(AnomalyDetector.Detect(history, [Candidate(203.70m)]));

        Assert.Equal(3.50m, finding.RobustZScore);
        Assert.Equal(AnomalySeverity.Info, finding.Severity);
        Assert.Equal(AnomalyRule.RobustZScore, finding.Rule);
    }

    // --- MAD = 0 fallback (design D-1, spec S-11) ---

    [Fact]
    public void Detect_MadZero_BelowRatioThreshold_ProducesNoAnomaly()
    {
        List<AnomalySample> history = BuildSamples(_categoryId, _madZeroRows);

        // 1499 / 500 = 2.998, below MedianRatioThreshold (3x).
        IReadOnlyList<AnomalyFinding> findings = AnomalyDetector.Detect(history, [Candidate(1499m)]);

        Assert.Empty(findings);
    }

    [Fact]
    public void Detect_MadZero_AtRatioThreshold_FlagsWarningWithoutDividingByZero()
    {
        List<AnomalySample> history = BuildSamples(_categoryId, _madZeroRows);

        // 1500 / 500 = 3.00 exactly -> flags via the ratio fallback, never divides by MAD (== 0).
        AnomalyFinding finding = Assert.Single(AnomalyDetector.Detect(history, [Candidate(1500m)]));

        Assert.Equal(AnomalySeverity.Warning, finding.Severity);
        Assert.Equal(AnomalyRule.MedianRatio, finding.Rule);
        Assert.Null(finding.RobustZScore);
        Assert.Equal(500m, finding.MedianDop);
        Assert.Equal(3.00m, finding.RatioToMedian);
    }

    // --- Output ordering (design D-1 point 7) ---

    [Fact]
    public void Detect_MultipleFindings_OrderedBySeverityThenRatioThenDateThenExpenseId()
    {
        List<AnomalySample> history = BuildSamples(_categoryId, _workedExampleRows);

        var idC = Guid.Parse("00000000-0000-0000-0000-000000000003"); // Critical, ratio 2.00
        var idD = Guid.Parse("00000000-0000-0000-0000-000000000004"); // Critical, ratio 1.60, later date, higher id
        var idE = Guid.Parse("00000000-0000-0000-0000-000000000001"); // Critical, ratio 1.60, later date, lower id
        var idA = Guid.Parse("00000000-0000-0000-0000-000000000002"); // Critical, ratio 1.60, earlier date
        var idB = Guid.Parse("00000000-0000-0000-0000-000000000005"); // Warning, ratio 1.45

        List<AnomalyCandidate> candidates =
        [
            Candidate(200m, new DateOnly(2026, 4, 3), idC),
            Candidate(160m, new DateOnly(2026, 4, 5), idD),
            Candidate(160m, new DateOnly(2026, 4, 5), idE),
            Candidate(160m, new DateOnly(2026, 4, 1), idA),
            Candidate(145m, new DateOnly(2026, 4, 2), idB)
        ];

        IReadOnlyList<AnomalyFinding> findings = AnomalyDetector.Detect(history, candidates);

        Assert.Equal([idC, idE, idD, idA, idB], [.. findings.Select(f => f.ExpenseId)]);
    }

    // --- Guards ---

    [Fact]
    public void Detect_NullHistory_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => AnomalyDetector.Detect(null!, []));

    [Fact]
    public void Detect_NullCandidates_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => AnomalyDetector.Detect([], null!));

    [Fact]
    public void Detect_NegativeHistoryAmount_ThrowsArgumentOutOfRangeException()
    {
        List<AnomalySample> history = [new AnomalySample(_categoryId, _candidateDate, -1m)];

        Assert.Throws<ArgumentOutOfRangeException>(() => AnomalyDetector.Detect(history, []));
    }

    [Fact]
    public void Detect_NegativeCandidateAmount_ThrowsArgumentOutOfRangeException() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => AnomalyDetector.Detect([], [Candidate(-1m)]));
}
