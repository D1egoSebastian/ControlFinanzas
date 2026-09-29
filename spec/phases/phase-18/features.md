# Phase 18 — Exportación a PDF y Excel

> **Phase:** 18 — Exportación (PDF / Excel)
> **Status:** 📝 Spec written (2026-09-29) — design pending. See `spec/features/phase-18/F-018-export.spec.md`
> **Spec ID:** F-018
> **Dependencies:** Phase 4 ✅ (expense queries), Phase 5 ✅ (`ICurrencyConverter`), Phase 8 ✅ (Dashboard),
> Phase 10 ✅ (budgets). Phase 17 ✅ archived (no coupling).
> **Blocks:** Phase 20 (import consumes the "Data" sheet contract). Distinct from Phase 19 (backup).
> **Roadmap:** `docs/roadmap.md` §Fase 18
> **Stack:** .NET 8 · Avalonia 11.2.7 (MVVM strict) · QuestPDF (Community) + ClosedXML (MIT), Infrastructure
> only · no schema change, no migration, no new settings.

## 1. Summary

Phase 18 adds "Exportar" actions that write the displayed month summary to PDF and XLSX (totals, unfolded
category breakdown, budget vs actual, DOP-normalized figures with original amount + currency on line items,
missing-rates footer) and, later in the phase, the currently filtered Expenses list (all pages, not just the
visible one). Reports reuse the existing Dashboard/budget/expense queries and `ICurrencyConverter`; no new
business rule is added.

Structure: two format-agnostic use cases in `Application/UseCases/Exports/` build report DTOs and select an
`IReportWriter` by `ReportFormat`; `QuestPdfReportWriter` and `ClosedXmlReportWriter` live in
`Infrastructure/Reports/` (registered by `AddReportExporters()`, which sets the QuestPDF license once);
`IFileSaveDialog` (Application port) is implemented in UI over Avalonia `TopLevel.StorageProvider`. Writers
render into memory first (no partial files) and take all text from a ViewModel-supplied `ReportLabels`.
The XLSX has a styled "Summary" sheet and a flat "Data" sheet with invariant English headers.

## 2. Slice plan

Chained slices, each ≤ ~400 changed lines.

| ID | Slice | Commit | Status |
| --- | --- | --- | --- |
| S1 | Ports (`IReportWriter`, `IFileSaveDialog`), `ReportFormat`, `ReportLabels`, DTOs, `ExportMonthSummaryReportUseCase` + unit tests with fake writer (DOP totals, `HasMissingRates`, labels pass-through) | — | ⏳ Pending |
| S2 | Package pins + license banner, `QuestPdfReportWriter`, `AddReportExporters()`, integration tests (`%PDF-`, non-empty, es-DO + en) | — | ⏳ Pending |
| S3 | `ClosedXmlReportWriter` month summary (two sheets) + XLSX round-trip tests | — | ⏳ Pending |
| S4 | `FileSaveDialog` UI impl, Dashboard commands, resx (es-DO + en + Designer), fixture updates (`DashboardViewModelTests`, `NavigationServiceTests`, `ShellViewModelTests`), `frontend-design` pass | — | ⏳ Pending |
| S5 | `ExportExpenseListReportUseCase` (paged at 200), list method in both writers, `ExpensesViewModel` button | — | ⏳ Pending |
| S6 | Debts export (stretch, ≤ ~150 lines) or follow-up note; docs reconciliation (`docs/architecture.md`) + `AGENTS.md` | — | ⏳ Pending |

## 3. Phase completion criteria

- **(roadmap)** Exporting the current month to PDF and Excel produces valid files with correct data.
- Filtered Expenses export contains every matching row across all pages.
- Cancelling the save dialog writes nothing and shows no error; missing rates appear in the footer; a failed
  render leaves no partial file and surfaces a localized status.
- Reports render in es-DO and en.
- Build clean, full suite green, `dotnet format style --verify-no-changes` exit 0, NetArchTest R-1..R-5 green
  (no QuestPDF/ClosedXML/Avalonia type in Application).
- Manual verification of the real save dialog and of opening both files externally recorded (cannot be
  automated; no headless Avalonia harness exists).

## 4. Risks and notes

- QuestPDF Community license is revenue-gated and version-specific: exact pin, banner, re-verify on bumps.
- QuestPDF native engine on Windows / SkiaSharp version compatibility with
  `LiveChartsCore.SkiaSharpView.Avalonia` 2.0.5 — check and smoke-export in S2.
- `IFileSaveDialog` is the first UI port over a `TopLevel` capability; VM tests use a fake dialog.
- `DashboardViewModel` constructor growth breaks three fixtures (certain); limited to two parameters.
- Debts export is conditional (spec R-16); if it exceeds ~150 lines it is recorded here as a follow-up.
- No CSV output in v1 despite the roadmap's "Excel/CSV" wording (proposal assumption 4).

## 5. References

- `docs/roadmap.md` §Fase 18
- `spec/features/phase-18/F-018-export.spec.md` — formal spec
- `.atl/sdd/f-018p18-export/{exploration,proposal}.md` — traceability
- `docs/architecture.md` §11e — reconciled in S6 (`CsvExporter` / `ExportView` mentions removed)

## 6. Follow-ups (not implemented)

- **Debts export:** recorded as a follow-up (spec R-16); predicted ~250 lines, above the ~150-line stretch limit.
- **Expenses payment-method filter gap:** pre-existing (the visible list ignores `PaymentMethodIds`), so the
  export ignores it too; out of scope.
- **CSV output:** excluded from v1 despite the roadmap's "Excel/CSV" wording.
- `spec/phases/phase-17/features.md` — format precedent
