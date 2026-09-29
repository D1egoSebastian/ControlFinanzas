using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.DTOs.Reports;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>
/// Configurable <see cref="IReportWriter"/> double (F-018). Captures the DTO, the labels
/// instance and the bytes it wrote, and can be told to fail or throw.
/// </summary>
public sealed class FakeReportWriter : IReportWriter
{
    public FakeReportWriter(ReportFormat format = ReportFormat.Pdf)
    {
        Format = format;
    }

    public ReportFormat Format { get; }

    /// <summary>Bytes written to the destination on success.</summary>
    public byte[] Payload { get; set; } = new byte[] { 1, 2, 3 };

    /// <summary>When set, writes return this failure without touching the stream.</summary>
    public Error? FailWith { get; set; }

    /// <summary>When set, writes throw this exception.</summary>
    public Exception? ThrowWith { get; set; }

    public int CallCount { get; private set; }

    public MonthSummaryReport? CapturedMonthSummary { get; private set; }

    public ExpenseListReport? CapturedExpenseList { get; private set; }

    public ReportLabels? CapturedLabels { get; private set; }

    public Task<Result> WriteMonthSummaryAsync(
        MonthSummaryReport report,
        ReportLabels labels,
        Stream destination,
        CancellationToken ct = default)
    {
        CapturedMonthSummary = report;
        return WriteAsync(labels, destination, ct);
    }

    public Task<Result> WriteExpenseListAsync(
        ExpenseListReport report,
        ReportLabels labels,
        Stream destination,
        CancellationToken ct = default)
    {
        CapturedExpenseList = report;
        return WriteAsync(labels, destination, ct);
    }

    private async Task<Result> WriteAsync(ReportLabels labels, Stream destination, CancellationToken ct)
    {
        CallCount++;
        CapturedLabels = labels;
        if (ThrowWith is not null)
        {
            throw ThrowWith;
        }

        if (FailWith is not null)
        {
            return Result.Failure(FailWith);
        }

        await destination.WriteAsync(Payload, ct);
        return Result.Success(Payload.Length);
    }
}
