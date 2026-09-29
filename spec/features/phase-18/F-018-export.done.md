# F-018 — PDF and Excel Export

> **Phase:** 18 — Exportación a PDF y Excel
> **Status:** ✅ Completed and archived (2026-09-29) — verify PASS WITH WARNINGS, 0 critical
> **SDD:** `.atl/sdd/f-018p18-export/`
> **Input:** `.atl/sdd/f-018p18-export/proposal.md`, `.atl/sdd/f-018p18-export/exploration.md`
> **Depends on:** Phase 4 (expense queries / filters), Phase 5 (`ICurrencyConverter`), Phase 8 (Dashboard),
> Phase 10 (budgets), Phase 17 (archived; no code coupling).
> **Blocks:** Phase 20 (import reads the "Data" sheet contract defined here). No overlap with Phase 19
> (backup zips the raw `data.db`; different artifact).

## Goal

Every figure the user may need to share or archive (month totals, category split, budget status, the expense
list) exists only on screen. Phase 18 adds "Exportar" actions that write the displayed month summary to PDF
and XLSX, and then the currently filtered Expenses list, using the same deterministic numbers the Dashboard
shows. No new business rule is introduced: reports are composed from existing queries and `ICurrencyConverter`.

### Confirmed product decisions

Binding (D1-D14 from the proposal), not reopened here:

- **Libraries (D1):** QuestPDF (Community) for PDF and ClosedXML (MIT) for XLSX, Infrastructure-only, exact
  version pins with a license banner in `Directory.Packages.props`.
- **Currency (D2):** line items show original amount + currency + DOP equivalent; subtotals/totals are
  DOP-normalized via `ICurrencyConverter.ConvertAsync(..., Currency.DOP)`; missing rates flagged in a footer.
- **Scope (D3):** month summary in both formats first, then the filtered Expenses list; Debts is a stretch.
- **Excel shape (D4/D8/D9):** sheet "Summary" (styled) + sheet "Data" (flat, one row per transaction,
  incomes and expenses, invariant English headers).
- **Ports (D5/D6):** one `IReportWriter` (per-format implementations, one method per report) and one
  `IFileSaveDialog` returning `SaveTarget?` (`null` = cancelled).
- **Localization (D7):** the ViewModel builds `ReportLabels` from resx; writers are locale-agnostic.
- **Paging (D10):** the expense-list use case pages the repository at 200 per page until `Total` is reached.
- **Atomicity (D11):** render into a `MemoryStream`, copy to the target only on success.
- **PDF (D12):** text and tables only, no charts.
- **Wiring (D13):** `DashboardViewModel` gains exactly two constructor parameters.
- **Debts (D14):** conditional stretch, dropped to a follow-up note above ~150 changed lines.

## Requirements

### R-1 — Ports and value types live in Application, library-free

The system MUST define in `Application/Abstractions/`: `IReportWriter` (with `ReportFormat Format { get; }`,
`WriteMonthSummaryAsync`, `WriteExpenseListAsync`, each taking the report DTO, `ReportLabels`, and a `Stream`
and returning `Result`), `IFileSaveDialog` (`PickAsync(suggestedName, ReportFormat, ct)` returning
`SaveTarget?` whose `OpenWriteAsync()` yields the destination stream), `ReportFormat` (`Pdf`, `Xlsx`) and
`ReportLabels`. No type from QuestPDF, ClosedXML or Avalonia MAY appear in Application. Two writers MUST be
registered as `IEnumerable<IReportWriter>` and the use case MUST select the writer whose `Format` matches
the request; an unregistered format MUST return `Result.Failure`, not throw.

### R-2 — Month-summary report content

`ExportMonthSummaryReportUseCase` MUST build a report DTO for the requested month containing, all sourced
from existing queries with no re-implemented arithmetic:

- Totals: expenses, incomes and net balance in DOP (`GetMonthSummaryQuery`), plus the previous-month
  comparison figures that query already returns.
- Category breakdown: every category with spend in the month, UNFOLDED (no "Otras" bucket, no top-N cap),
  each with its DOP amount, and a total that reconciles with the sum of the rows
  (`GetCategoryBreakdownQuery` called with a `topCount` large enough to disable folding).
- Budget vs actual: one line per budget of the month with budget DOP, actual DOP, percent consumed and
  status (`GetBudgetVsActualQuery`); indeterminate lines (null status) MUST be rendered as an explicit
  "not available" marker and MUST NOT be shown as 0% or 100%.
- Generation date and the `HasMissingRates` flag (see R-4).
- The month (`MonthAnchor`) and the month title MUST match the Dashboard's displayed month.

An empty month (no transactions, no budgets) MUST still produce a valid report with zero totals and empty
sections, not a failure.

### R-3 — Line items keep original amount and currency; totals are DOP

Every transaction line in a report MUST show its original amount and currency code and, when a rate exists,
its DOP equivalent obtained from `ICurrencyConverter.ConvertAsync(amount, currency, Currency.DOP, date)`.
Subtotals and grand totals MUST be DOP-normalized. A DOP transaction's DOP equivalent MUST equal its original
amount. When the converter cannot convert a line, its DOP equivalent MUST be left empty (never 0), the line
MUST still be written with its original amount and currency, and the report MUST be flagged per R-4.

### R-4 — Missing rates are flagged in the footer

If any underlying query reports `HasMissingRates`, or any line-item conversion fails, the report DTO MUST
carry `HasMissingRates = true` and both formats MUST render a localized footer note stating that some
amounts were excluded from DOP totals for lack of an exchange rate. When no rate is missing, the note MUST
NOT appear. The PDF footer MUST also include the generation date.

### R-5 — PDF validity

`QuestPdfReportWriter` MUST write a non-empty stream that begins with the bytes `%PDF-`, for both the month
summary and the expense list, in `es-DO` and `en` labels/cultures, without throwing. The PDF MUST be text and
tables only (D12). Rendering exceptions MUST be converted to `Result.Failure` (see R-9, R-14).

### R-6 — XLSX shape: two sheets

`ClosedXmlReportWriter` MUST write a workbook readable by `XLWorkbook` (round-trip from the output stream)
with exactly two worksheets, in this order:

1. **"Summary"** — styled: title, month, totals, category breakdown, budget vs actual, footer note per R-4.
   Sheet names are fixed English strings (not localized); label text inside the sheet comes from
   `ReportLabels`.
2. **"Data"** — flat, one header row then one row per transaction, expenses AND incomes of the month, with
   invariant English headers in this exact order: `Date`, `Type`, `Description`, `Category`, `Amount`,
   `Currency`, `AmountDOP`. `Date` MUST be a real date cell, `Amount` and `AmountDOP` real numeric cells
   (not text), `Type` is `Expense` or `Income`, `Currency` is the ISO code. `AmountDOP` MUST be an empty cell
   when the rate is missing (R-3). Headers MUST NOT change with the UI language (D8).

For the expense-list report the workbook MUST have the same two sheets, with "Data" limited to the exported
expenses (`Type = Expense`) and "Summary" carrying the filter description and DOP total.

### R-7 — Filtered Expenses export honors the filter across all pages

`ExportExpenseListReportUseCase` MUST receive the ViewModel's current filter (category, date range, tags) and
MUST read `IExpenseRepository.GetByFilterAsync` page by page with page size 200 until every matching row has
been collected, ignoring the ViewModel's current page (D10). The report MUST contain every matching row and
no non-matching row. A filter matching zero rows MUST produce a valid empty report, not a failure. DOP total
and `HasMissingRates` follow R-3 and R-4.

### R-8 — Save dialog: cancel is not an error

The export flow MUST obtain the destination through `IFileSaveDialog` before building or writing anything
that reaches disk. When `PickAsync` returns `null`, the flow MUST write no file, MUST open no destination
stream, MUST surface no error and MUST leave the ViewModel in its idle state (busy flag cleared). The
suggested file name MUST include the report kind and the month (e.g. `resumen-2026-09.pdf`, culture-neutral
`yyyy-MM`) and the correct extension for the format.

### R-9 — No partial file on failure

Writers MUST render into a `MemoryStream` first; the use case MUST copy to the target stream only after a
successful render. If rendering or any data read fails, no bytes MUST be written to the target stream and
`OpenWriteAsync` SHOULD NOT have been called. If the copy itself fails after the target is open, the failure
MUST be reported as in R-14 (the OS-level partial file cannot be guaranteed away, and the spec does not
require deletion).

### R-10 — Labels supplied by the ViewModel

`ReportLabels` MUST carry every user-visible string the writers emit (titles, section headings, column
captions on the "Summary" sheet and PDF, budget status names, missing-rates note, footer text) plus the
`CultureInfo` used to format amounts and dates. The ViewModel MUST build it from `Strings` resx for the
current UI culture and pass it through the use case unchanged. Writers MUST NOT reference resx or
`CultureInfo.CurrentUICulture`. A test MUST show that changing the `ReportLabels` culture/strings changes the
output text (locale-agnostic writers).

### R-11 — QuestPDF license and package pins

`Directory.Packages.props` MUST pin QuestPDF and ClosedXML to exact versions with a comment banner that
records the Community-license eligibility reasoning (individual/no revenue; revenue-gated; re-verify on every
bump). `QuestPDF.Settings.License = LicenseType.Community` MUST be set exactly once, inside
`ReportServiceCollectionExtensions.AddReportExporters()` in Infrastructure, and nowhere else. The writers MUST
work when `AddReportExporters()` has been called and the container built; no other global initialization is
required from callers.

### R-12 — Architecture rules

NetArchTest R-1..R-5 MUST stay green. QuestPDF and ClosedXML types MUST be referenced only from
Infrastructure; Avalonia types only from UI (`FileSaveDialog`, over `TopLevel.StorageProvider`). Application
use cases depend only on the ports of R-1. Use case names MUST use "Report" (e.g.
`ExportMonthSummaryReportUseCase`) and MUST NOT be called `ExportDataUseCase` (reserved for Phase 19).

### R-13 — Localization of new UI strings

All new UI strings (Dashboard and Expenses export buttons, save-dialog title and file-type names, busy and
success/failure status text, report labels) MUST be added to `Strings.resx` (en) and `Strings.es-DO.resx`,
and exposed through manually maintained `Strings.Designer.cs` accessors. No new hardcoded UI literal is
allowed in AXAML or ViewModels. Both resx files MUST define the same set of keys.

### R-14 — Errors surface as user-facing status, never exceptions

`ExportMonthSummaryReportUseCase` and `ExportExpenseListReportUseCase` MUST return `Result` and MUST NOT let
an exception escape (query failure, converter failure other than a missing rate, writer failure, stream
failure). Each failure MUST be logged and MUST produce a localized status message in the ViewModel
(`ExportStatus` or equivalent) without crashing the app or leaving the command permanently disabled. Success
MUST also set a localized status message. Cancellation of the `CancellationToken` MUST NOT be reported as a
failure message.

### R-15 — Dashboard and Expenses wiring

- `DashboardViewModel` MUST gain exactly two constructor parameters (`ExportMonthSummaryReportUseCase`,
  `IFileSaveDialog`) and expose `ExportPdfCommand` and `ExportExcelCommand` operating on the month currently
  displayed. While an export runs, both commands MUST be non-executable (no double execution); the flag MUST
  clear on success, failure and cancel.
- `ExpensesViewModel` MUST expose an export command (both formats) that passes its current filter and ignores
  its current page (R-7).
- Existing test fixtures constructing these ViewModels (`DashboardViewModelTests`, `NavigationServiceTests`,
  `ShellViewModelTests`, and the Expenses fixtures) MUST be updated in the same slice so the suite stays
  green.
- The buttons MUST follow the `frontend-design` skill and existing design tokens; no logic in code-behind.

### R-16 — Debts export (conditional stretch)

IF, after S5, a Debts report can be added in at most ~150 changed lines reusing the same ports (a third
`IReportWriter` method plus a Debts-view button), it MAY be delivered as slice S6 with the same R-3..R-14
guarantees. OTHERWISE it MUST be recorded as a follow-up in `spec/phases/phase-18/features.md` and MUST NOT
block phase completion.

### R-17 — Explicit scope exclusions

The system MUST NOT implement: backup/restore of `data.db` (Phase 19), import (Phase 20), CSV output, charts
inside the PDF, cloud or email delivery, a second PDF library for tests, export of incomes/goals/budget views,
any schema change, migration or new setting, or any change to the behavior of existing queries or of
`ICurrencyConverter`. No remote telemetry MAY be introduced (QuestPDF Community requires none).

## Scenarios

### S-1 — Roadmap: current month exports to PDF with correct data

- GIVEN a month with expenses in DOP and USD, one income, two budgets, and a rate for USD
- WHEN the user runs Export PDF from the Dashboard and picks a destination
- THEN the file starts with `%PDF-`, is non-empty, and the DTO handed to the writer holds the same DOP
  totals, category rows and budget lines the Dashboard queries return for that month

### S-2 — Roadmap: current month exports to XLSX with two sheets

- GIVEN the same month
- WHEN Export Excel completes
- THEN reading the file back shows sheets "Summary" and "Data"; "Data" has the seven invariant headers and
  one row per expense and per income; `Amount` cells are numeric and `AmountDOP` matches the converter

### S-3 — Original amount and DOP equivalent per line

- GIVEN a USD 100 expense with rate 60.00 on its date
- WHEN the report is built
- THEN its line shows `100` and `USD` and a DOP equivalent of `6000.00`, and the DOP total includes 6000.00

### S-4 — Missing rate is flagged, not zeroed

- GIVEN a EUR expense with no available rate
- WHEN the month report is built
- THEN the EUR line is kept with its original amount and currency, its DOP equivalent is empty, the EUR
  amount is excluded from DOP totals, `HasMissingRates` is true and the footer note is rendered in both
  formats; a month with all rates present renders no note

### S-5 — Category breakdown is unfolded

- GIVEN eight categories with spend in the month (the Dashboard shows top-5 plus "Otras")
- WHEN the month report is built
- THEN all eight categories appear individually, no "Otras" row exists, and their sum equals the breakdown
  total

### S-6 — Indeterminate budget line is not shown as a percentage

- GIVEN a budget whose conversion failed (null status/percent)
- WHEN the report is rendered
- THEN that line shows the "not available" marker, never 0% or 100%

### S-7 — Empty month still produces a valid file

- GIVEN a month with no transactions and no budgets
- WHEN Export PDF or Excel runs
- THEN a valid `%PDF-` file / a two-sheet workbook is written, totals are zero, no failure is reported

### S-8 — PDF renders in both locales

- GIVEN `ReportLabels` built for `es-DO` and for `en`
- WHEN each is written by `QuestPdfReportWriter`
- THEN both outputs begin with `%PDF-`, are non-empty and no exception is thrown; the `es-DO` and `en`
  outputs differ (labels are honored)

### S-9 — Filtered Expenses export includes every page

- GIVEN 450 expenses match the filter (category X, September) and the grid shows page 2 of 50 per page
- WHEN the user exports the Expenses list
- THEN the repository is read in 3 pages of 200 (200, 200, 50), the report holds all 450 rows and none from
  other categories or dates

### S-10 — Cancelling the save dialog

- GIVEN the user clicks Export PDF
- WHEN the dialog returns `null` (cancel)
- THEN no writer output reaches disk, no stream is opened, no error is shown, and both export buttons are
  enabled again

### S-11 — Render failure leaves no partial file

- GIVEN a writer that throws midway (fake) after the user picked a destination
- WHEN the use case runs
- THEN the target stream receives zero bytes, a localized failure status is shown, the app does not crash,
  and the commands are enabled again

### S-12 — Double click does not double-export

- GIVEN an export is in progress
- WHEN the user clicks the same or the other export button
- THEN the click is ignored (command not executable); afterwards a new export can start

### S-13 — Labels come from the ViewModel

- GIVEN a fake writer capturing its arguments
- WHEN the Dashboard ViewModel exports under UI culture `es-DO`
- THEN the captured `ReportLabels` equal the `es-DO` resx strings and carry the `es-DO` culture; under `en`
  they equal the English strings

### S-14 — Library confinement and license

- GIVEN the built solution
- WHEN NetArchTest and a source scan run
- THEN no Application/Domain/UI type references QuestPDF or ClosedXML, no Application type references
  Avalonia, and `QuestPDF.Settings.License` is assigned in exactly one place, `AddReportExporters()`

### S-15 — Localized resources are complete

- GIVEN the new export keys
- WHEN resx files and Designer accessors are compared
- THEN every new key exists in `Strings.resx`, `Strings.es-DO.resx` and `Strings.Designer.cs`

## Out of Scope

Backup/restore (Phase 19), import (Phase 20), CSV output, charts in the PDF, cloud/email delivery, a second
PDF library for tests, export of income/goals/budget views, any change to existing query behavior or to
`ICurrencyConverter`, schema/migration/settings changes. Debts export is conditional (R-16).

## Acceptance Criteria

Verified 2026-09-29 against source and tests (second independent verification, then the closing gate run:
build 0/0, 1550 unit + 358 integration = 1908 green, `dotnet format style --verify-no-changes` exit 0).
Test names are relative to `tests/ControlDeFinanzasIA.Tests.Unit` (U) or `.Tests.Integration` (I).

- [x] **(roadmap)** Exporting the current month to PDF and Excel produces valid files with correct data
  (S-1, S-2, S-3, S-5, S-7). Evidence: U `Application/Exports/ExportMonthSummaryReportUseCaseTests`
  (`ExecuteAsync_SummaryMatchesGetMonthSummaryQuery`, `ExecuteAsync_MoreThanFiveCategories_StayUnfoldedWithoutOtras`,
  `ExecuteAsync_EmptyMonth_ProducesValidEmptyReport`); I `Reports/ReportExportEndToEndTests`
  (`MonthSummary_RealUseCaseAndWriter_ProducesAValidFile`, real use case + real writers, Pdf/Xlsx x es-DO/en,
  temp SQLite file: PDF starts with `%PDF-` and is > 1 KB; XLSX reopens with `XLWorkbook`, sheets `Summary`
  and `Data`, 5 data rows = 3 expenses + 2 incomes).
- [x] Every report line keeps original amount + currency; totals are DOP; missing rates are flagged in the
  footer and never zeroed (S-3, S-4, S-6). Evidence: U `ExecuteAsync_UsdLineAtRate60_ConvertsToDop_DopLineEqualsAmount`,
  `ExecuteAsync_LineWithoutRate_KeepsAmountAndFlagsMissingRates`,
  `ExecuteAsync_IndeterminateBudgetLines_PassThroughWithNullStatus`; I `ClosedXmlReportWriterTests` (blank
  `AmountDOP` on a missing rate, indeterminate budget marker), `QuestPdfReportWriterTests`
  (`WriteMonthSummary_MissingRates_ProducesValidPdfDifferentFromComplete`,
  `WriteMonthSummary_IndeterminateBudgetLines_ProducesValidPdf`); `ReportExportEndToEndTests` (a USD line with no
  rate yields a blank `AmountDOP` cell and the export still succeeds).
- [x] PDF is valid (`%PDF-`, non-empty) in es-DO and en without exceptions (S-8). Evidence: I
  `QuestPdfReportWriterTests` (`WriteMonthSummary_EsDo_ProducesValidPdf`, `WriteMonthSummary_En_ProducesValidPdf`,
  `WriteMonthSummary_LabelsAreHonored_LocalesDiffer`, `WriteExpenseList_EsDo_ProducesValidPdf`,
  `WriteExpenseList_En_ProducesValidPdf`) and `ReportExportEndToEndTests`. Limit: PDF content is only asserted
  as prefix/size/differs (R-17 forbids a second PDF library).
- [x] XLSX has exactly two sheets, "Summary" and "Data", with the invariant seven headers and numeric/date
  cells (S-2). Evidence: I `ClosedXmlReportWriterTests` (`MonthSummary_HasExactlyTwoInvariantSheetNames`, the
  Data-headers test over both locales, formula-injection cases) and `ReportExportEndToEndTests`.
- [x] Filtered Expenses export contains every matching row across all pages (S-9). Evidence: U
  `ExportExpenseListReportUseCaseTests.ExecuteAsync_450MatchingRows_AreReadAsThreePagesOf200And200And50`,
  `ExecuteAsync_IgnoresIncomingPage_AndOverwritesPageSizeTo200`; I `ReportExportEndToEndTests.ExpenseList_...`
  (list `Data` sheet holds only the 3 expenses, no incomes).
- [x] Cancelling the dialog writes nothing and shows no error (S-10). Evidence: U
  `UI/DashboardViewModelExportTests.ExportPdf_Cancel_NeverCallsUseCase_ShowsNoMessage_AndClearsFlag`,
  `UI/ExpensesViewModelTests.Export_Cancel_WritesNothingNoMessageAndNoUseCaseCall`.
- [x] A failure leaves no partial file and is shown as a localized status, never an exception (S-11).
  Evidence: U `ExecuteAsync_WriterThrows_ReportRenderFailedAndTargetNeverOpened`,
  `ExecuteAsync_WriterReturnsFailure_ReportRenderFailedAndTargetNeverOpened`,
  `ExecuteAsync_RepositoryThrows_ReturnsFailureInsteadOfThrowing`, `DashboardViewModelExportTests`
  (`Export_WriterFailure_ShowsRenderFailedMessage_AndCommandIsUsableAgain`,
  `Export_UnexpectedException_IsSwallowedAndShowsGenericMessage`),
  `ExpensesViewModelTests.Export_RenderFailure_SetsFormErrorAndNoStatusMessage`.
- [x] Commands cannot double-execute (S-12); labels originate in the ViewModel (S-13). Evidence: U
  `DashboardViewModelExportTests.Export_TwoConcurrentInvocations_OpenExactlyOneDialog_ThenAllowsAnother`,
  `ExpensesViewModelTests.Export_WhileExporting_SecondInvocationIsIgnored`,
  `DashboardViewModelExportTests.Export_LabelsComeFromResxAndCurrentCulture`.
- [x] QuestPDF license set once in `AddReportExporters()`, pins + banner in `Directory.Packages.props`;
  libraries Infrastructure-only; NetArchTest R-1..R-5 green (S-14). Evidence: U
  `Architecture/ArchitectureTests.F018_QuestPdf_License_Se_Asigna_En_Un_Unico_Lugar` plus the forbidden-namespace
  arrays for QuestPDF/ClosedXML/DocumentFormat.OpenXml; banner in `Directory.Packages.props` (QuestPDF 2026.9.1,
  ClosedXML 0.105.1); I `ReportServiceCollectionExtensionsTests` (writer registration, idempotence, resolved PDF
  writer renders). Note: the license is assigned in `QuestPdfConfiguration.Apply()`, reached from
  `AddReportExporters()` and also from the writer constructor (see Post-apply reconciliation).
- [x] New UI strings exist in resx, es-DO and Designer (S-15). Evidence: U
  `UI/ExportResourcesTests` (`ExportKeys_CoverTheDesignedSet`, `Key_IsDefinedAndNonEmpty_InBothLocales`).
- [ ] Manual verification recorded: real save dialog on Windows, opening the PDF and XLSX in external
  viewers (the dialog cannot be automated). NOT DONE: nobody drove the native dialog or opened the files in a
  PDF viewer / Excel / LibreOffice, and the UI was never eyeballed in es-DO and en (tasks 8.2 and 8.3 stay
  unchecked). Only launch-only smoke runs exist (S4, S5). Left open and non-blocking; to be run by the user.
- [x] Build clean, full suite green, `dotnet format style --verify-no-changes` exit 0,
  `docs/architecture.md` reconciled (`CsvExporter`/`ExportView` mentions), `AGENTS.md` updated, spec renamed
  to `.done.md` after archive. Evidence: closing gate run (build 0 warnings/0 errors; 1550 + 358 = 1908
  green; format exit 0); `docs/architecture.md` §11e; `AGENTS.md` status line; this file's name.

## Reconciliation with design (2026-09-29, orchestrator)

All 12 points of `.atl/sdd/f-018p18-export/design.md` "Spec Reconciliation" are accepted; where the spec wording differs, the design governs:

1. Report DTOs live in `Application/DTOs/Reports/`.
2. Buffering (render to memory, then copy to target) happens once in the use case (`ReportOutput`); writers are pure DTO -> Stream. The guarantee that a failed render never creates or truncates the target is unchanged.
3. The ViewModel owns the save dialog and passes a `SaveTarget` to the use case; cancel is tested at ViewModel level.
4. "Filtered Expenses list" means the same filter fields as the visible list (category, date range, tags), all pages. Payment-method filtering is NOT promised (the visible list already ignores it; pre-existing gap, out of scope).
5. Sheet names `Summary`/`Data` and Data headers are invariant English in both locales.
6. The month-summary PDF has no transaction list (totals, categories, budgets, footer); only the XLSX `Data` sheet carries per-transaction rows. The expense-list PDF carries rows.
7. Summary totals (from the Dashboard queries) are authoritative; `AmountDOP` is a per-line conversion.
8. A missing rate yields a blank `AmountDOP` cell in XLSX and an em dash in the PDF, never 0.
9. Page size Letter; generation date is `IClock.Today` (local date).
10. Cancel shows no message; success shows the chosen file name; Expenses reuses `StatusMessage`/`FormError`.
11. Only the 3 Dashboard test fixtures change; Expenses constructor params are optional.
12. Debts export is a documented follow-up, not an acceptance criterion.

Open item carried into apply: slice S2 must verify exact QuestPDF/ClosedXML versions and nuspec dependencies (and SkiaSharp compatibility with LiveCharts' 2.88.9) before pinning.

## Post-apply reconciliation (2026-09-29, phase closer)

Divergences between the design/spec and the shipped code that were not documented while applying. None changes
a requirement; they are recorded so a future reader does not "fix" them.

1. **Extra error code `ReportDataFailed`.** R-14 lists no code for a failure while reading the data; both use
   cases return `ReportDataFailed` (defined in `ReportErrorCodes`) when the queries or repositories throw or a
   query returns a failure. The other codes are `ReportWriterUnavailable`, `ReportRenderFailed`,
   `ReportSaveFailed`, plus `InvalidFilter` for the list.
2. **`WriteExpenseListAsync` landed in two steps.** The XLSX implementation shipped in S3 (`825568d`); the PDF
   implementation was a stub through S5/S6 and was only implemented in the fix commit `3515cb9`, after the first
   independent verification FAILED on it. Task 6.5 was ticked while the PDF half did not exist.
3. **QuestPDF setting renamed.** The design called it `CheckIfAllTextGlyphsAreAvailable`; in QuestPDF 2026.9.1 it
   is `ThrowOnMissingTextGlyphs`, set to `false` in `QuestPdfConfiguration` (comment in code records the old name).
4. **`ExportErrorMessage` helper.** `DashboardViewModel.ExportErrorMessage(string? code)` maps failure codes to
   localized messages (unknown or missing code falls back to a generic message);
   `ExpensesViewModel` has the equivalent `ExportErrorKey` mapping. Tested by
   `ExportErrorMessage_KnownCodes_MapToSpecificMessages` and `ExportErrorKey_MapsCodesToResxKeys`.
5. **Expenses failure text lives in `FormError`.** Reconciliation #10 said Expenses reuses
   `StatusMessage`/`FormError`; in practice success goes to `StatusMessage` and failures go to `FormError`,
   which is rendered far from the export buttons (view-only follow-up).
6. **`QuestPdfConfiguration.Apply()` also runs from the writer constructor.** R-11 says the license is set only
   in `AddReportExporters()`; it is assigned in exactly one place (`QuestPdfConfiguration`), reached from
   `AddReportExporters()` and from the `QuestPdfReportWriter` constructor so a writer built directly still
   works. The scan test `F018_QuestPdf_License_Se_Asigna_En_Un_Unico_Lugar` passes.
7. **`ReportFilterSummary`** (date range, category name, tag count) is an extra DTO in
   `Application/DTOs/Reports/`; it was not named in R-1/R-7 and is now listed in `docs/architecture.md` §11e.
8. **Strict TDD was not literal in S1b and S2.** Tests were written together with the implementation in those
   slices rather than strictly failing-first.
9. **End-to-end coverage added at close.** `ReportExportEndToEndTests` (verification warning W1) was added in
   the closing commit; before it, no test ran the real use cases with the real writers together.

Not a divergence for this phase: `Tests.Integration.csproj` referencing the UI project dates from Phase 17
(S5a), and no Phase 18 change was needed there.
