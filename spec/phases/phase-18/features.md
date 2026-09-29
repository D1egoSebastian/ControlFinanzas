# Phase 18 — Exportación a PDF y Excel

> **Phase:** 18 — Exportación (PDF / Excel)
> **Status:** ✅ Completed (2026-09-29) — see `spec/features/phase-18/F-018-export.done.md`
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

Chained slices, each ≤ ~400 changed lines. The shipped split is S1a/S1b/S2/S3/S4/S5/S6 plus one fix commit; the original S1/S6 (Debts stretch) plan was revised during apply.

| ID | Slice | Commit | Status |
| --- | --- | --- | --- |
| S1a | Ports (`IReportWriter`, `IFileSaveDialog`, `SaveTarget`), `ReportFormat`, `ReportLabels`, month DTOs, `ReportPaging`, test doubles | `36c9463` | ✅ Completed |
| S1b | `ReportLineBuilder`, `ReportOutput`, `ExportMonthSummaryReportUseCase` + unit tests with fake writer (DOP totals, `HasMissingRates`, labels pass-through) | `5a8f2fe` | ✅ Completed |
| S2 | Package pins + license banner, `QuestPdfReportWriter` (month summary), `AddReportExporters()`, integration tests (`%PDF-`, non-empty, es-DO + en) | `b734fbd` | ✅ Completed |
| S3 | `ClosedXmlReportWriter` (two sheets, month summary and expense list) + XLSX round-trip tests | `825568d` | ✅ Completed |
| S4 | `AvaloniaFileSaveDialog`, Dashboard commands, resx (es-DO + en + Designer), fixture updates | `61dee0e` | ✅ Completed |
| S5 | `ExportExpenseListReportUseCase` (paged at 200), `ExpensesViewModel` button (PDF list writer was still a stub here) | `e741559` | ✅ Completed |
| S6 | Docs reconciliation (`docs/architecture.md`), Debts export deferred to a follow-up | `c01a732` | ✅ Completed |
| Fix | `QuestPdfReportWriter.WriteExpenseListAsync` implemented (found by the first independent verification) | `3515cb9` | ✅ Completed |
| Close | `ReportExportEndToEndTests` (verification warning W1), final docs, archive | `docs(phase-18): verify and close SDD cycle for export` | ✅ Completed |

## 3. Phase completion criteria

- **(roadmap)** Exporting the current month to PDF and Excel produces valid files with correct data.
- Filtered Expenses export contains every matching row across all pages.
- Cancelling the save dialog writes nothing and shows no error; missing rates appear in the footer; a failed
  render leaves no partial file and surfaces a localized status.
- Reports render in es-DO and en.
- Build clean, full suite green, `dotnet format style --verify-no-changes` exit 0, NetArchTest R-1..R-5 green
  (no QuestPDF/ClosedXML/Avalonia type in Application).
- Manual verification of the real save dialog and of opening both files externally recorded (cannot be
  automated; no headless Avalonia harness exists). **NOT run by hand** — recorded as an open, non-blocking
  item in section 6 and in the tasks file (8.2/8.3).

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
- `spec/features/phase-18/F-018-export.done.md` — formal spec
- `.atl/sdd/f-018p18-export/{exploration,proposal}.md` — traceability
- `docs/architecture.md` §11e — reconciled in S6 (`CsvExporter` / `ExportView` mentions removed)

## 6. Follow-ups (not implemented)

- **Manual checks 8.2/8.3:** real save dialog on Windows, opening the PDF and XLSX in external viewers, and a
  visual check in es-DO and en were not run by hand.
- Report writers swallow the inner exception (only the error code is logged); Expenses export failure text
  is shown in `FormError` far from the export buttons (view-only follow-up).
- **Debts export:** recorded as a follow-up (spec R-16); predicted ~250 lines, above the ~150-line stretch limit.
- **Expenses payment-method filter gap:** pre-existing (the visible list ignores `PaymentMethodIds`), so the
  export ignores it too; out of scope.
- **CSV output:** excluded from v1 despite the roadmap's "Excel/CSV" wording.
- `spec/phases/phase-17/features.md` — format precedent

## 7. Verification outcome

- Verify #1: FAIL (PDF expense-list writer was a stub) → fixed in `3515cb9`.
- Verify #2 (exhaustive): PASS WITH WARNINGS, 0 critical. W1 closed by `ReportExportEndToEndTests`; W2 PDF content
  only asserted as `%PDF-`/size/differs (spec R-17 forbids a second PDF library); W3 stale test count fixed.
- Gates at close: build 0/0, 1908/1908 tests (1550 unit + 358 integration), `dotnet format style` exit 0.
