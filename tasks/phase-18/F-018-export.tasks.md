# Tasks — F-018 PDF and Excel Export

> Governing inputs: `spec/features/phase-18/F-018-export.spec.md` (its final "Reconciliation with design"
> section governs where wording differs) and `.atl/sdd/f-018p18-export/design.md` (per-slice sections binding).
> Requirement ids (R-n) and scenario ids (S-n) below refer to the spec. Design decisions are cited as D-n.
> Strict TDD: within each slice, failing tests come first, then implementation.
>
> **Shared-file guard:** every touch on a shared file (`Bootstrap.cs`, `Directory.Packages.props`,
> `Strings.resx`, `Strings.es-DO.resx`, `Strings.Designer.cs`, `ArchitectureTests.cs`, `AGENTS.md`) MUST be an
> appended block. Nothing existing may be reordered, renamed or restructured.

## Review Workload Forecast

| Field | Value |
|---|---|
| Estimated changed lines | ≈2,900 total across 7 chained slices (design estimates, production + tests: S1a ~330, S1b ~330, S2 ~350, S3 ~360, S4 ~480, S5 ~480, S6 docs ~120) |
| 400-line budget risk | High — S4 and S5 exceed the budget; S1 is split into S1a/S1b to stay near it; S2 and S3 sit just under |
| Chained PRs recommended | Yes |
| Suggested split | PR S1a → PR S1b → PR S2 → PR S3 → PR S4 → PR S5 → PR S6 |
| Delivery strategy | auto-chain |
| Chain strategy | stacked-to-main |

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: High

The design's slice order is authoritative and strictly chained. S1 is pure Application (no DI graph change).
S2 is the package-risk slice (new NuGet dependencies, SkiaSharp compatibility) with a mandatory smoke export.
S3 adds the XLSX writer only. S4 is the first UI slice (constructor change on `DashboardViewModel`, three test
fixtures, new UI port adapter, resx): mandatory smoke-launch. S5 reuses S1-S4 for the Expenses list and touches
`ExpensesViewModel`/`ExpensesView`: mandatory smoke-launch. S6 is documentation only (Debts export is a
documented follow-up, not an acceptance criterion; Reconciliation #12, R-16).

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|---|---|---|---|
| S1a | Ports and value types | PR S1a | Base = main. `ReportFormat`, `IReportWriter`, `IFileSaveDialog`, `SaveTarget`, `ReportLabels`, month DTOs, `ReportPaging`, `FakeReportWriter`/`SaveTargets`. |
| S1b | Month-summary use case | PR S1b | Base = main (S1a merged). `ReportLineBuilder`, `ReportOutput`, `ExportMonthSummaryReportUseCase`, page-aware repo fakes, use case tests. |
| S2 | PDF writer + packages | PR S2 | Base = main (S1b merged). Version verification and pins, banner, `QuestPdfReportWriter`, `AddReportExporters()`, architecture-test forbidden arrays. **Smoke export required.** |
| S3 | XLSX writer | PR S3 | Base = main (S2 merged). `ClosedXmlReportWriter` month summary (two sheets), round-trip tests. |
| S4 | Dashboard export UI | PR S4 | Base = main (S3 merged). `AvaloniaFileSaveDialog`, `ReportLabelsFactory`, Dashboard commands/buttons, Bootstrap block, resx, 3 fixtures. **Smoke-launch required.** |
| S5 | Expenses list export | PR S5 | Base = main (S4 merged). List DTOs, `ExportExpenseListReportUseCase`, both writers' list method, `ExpensesViewModel` command/buttons, resx. **Smoke-launch required.** |
| S6 | Docs reconciliation | PR S6 | Base = main (S5 merged). `docs/architecture.md`, `spec/phases/phase-18/features.md` (Debts follow-up), `AGENTS.md`. |

## 1. Ports, DTOs and paging (S1a)

Depends on nothing (Phases 4, 5, 8, 10, 17 archived). Goal: library-free ports and value types live in
Application, with the paging helper and test doubles ready for the use case in S1b.

- [x] 1.1 Write failing `tests/…Unit/Application/Exports/ReportPagingTests.cs`: page 1 fixes the expected
  total; the loop continues while collected < total; 450 rows read as 200/200/50; an empty page breaks the loop
  (rows deleted mid-export); a hard cap of `ceil(total/200)` pages; zero rows yields an empty list. (R-7, S-9,
  D-5)
- [x] 1.2 Write failing `tests/…Unit/Application/Exports/SaveTargetTests.cs`: `OpenWriteAsync` delegates to the
  supplied function; `Dispose` disposes the owner exactly once; `OpenWriteAsync` after `Dispose` throws
  `ObjectDisposedException`. (R-1, R-8, D-1)
- [x] 1.3 Create `src/…Application/Abstractions/{ReportFormat,IReportWriter,IFileSaveDialog,SaveTarget,
  ReportLabels}.cs` exactly as in D-1: `ReportLabels` is a `sealed record` of `required init` strings plus a
  `CultureInfo Culture` (all keys from D-7); no QuestPDF, ClosedXML or Avalonia type. Run 1.2 green.
  (R-1, R-10, R-12)
- [x] 1.4 Create `src/…Application/DTOs/Reports/{ReportTransactionKind,ReportTransactionLine,MonthSummaryReport,
  ExportReportResult}.cs` per D-2, reusing `MonthSummaryResult`, `CategorySlice` and `BudgetVsActualResult`
  as-is. (R-2, R-3, R-4, Reconciliation #1)
- [x] 1.5 Create `src/…Application/UseCases/Exports/Common/ReportPaging.cs` (internal static `ReadAllAsync`,
  page size 200). Run 1.1 green. (R-7, D-5)
- [x] 1.6 Create test doubles `tests/…Unit/TestDoubles/{FakeReportWriter,SaveTargets}.cs`:
  `FakeReportWriter` captures DTO, labels and bytes and supports `FailWith`/`ThrowWith`;
  `SaveTargets.Recording()` counts opens and keeps written bytes. (R-9, R-10)
- [x] 1.7 Run all four gates: `dotnet build -c Release`, `dotnet test -c Release --no-build`,
  `dotnet format style --verify-no-changes`, NetArchTest filter (R-1..R-5; confirm Application has no
  QuestPDF/ClosedXML/Avalonia reference). All green before opening PR S1a. (R-12)

## 2. Month-summary use case (S1b)

Depends on 1. Goal: `ExportMonthSummaryReportUseCase` builds the fully computed DTO from existing queries,
renders through a picked writer into memory, and copies to the target only on success.

- [x] 2.1 Write failing `tests/…Unit/Application/Exports/ExportMonthSummaryReportUseCaseTests.cs` with
  `FakeReportWriter`, `SaveTargets.Recording()` and `FakeCurrencyConverter(onConvert)`:
  (a) DTO summary equals `GetMonthSummaryQuery`'s DOP totals and previous-month figures (R-2, S-1);
  (b) more than 5 categories stay unfolded, no "Otras", rows sum to `CategoriesTotalDop` (R-2, S-5);
  (c) a USD 100 line at rate 60 yields `AmountDop = 6000.00`; a DOP line equals its original amount (R-3, S-3);
  (d) a EUR line with no rate keeps amount and currency, `AmountDop == null`, `HasMissingRates == true`, and
  is excluded from totals (R-3, R-4, S-4);
  (e) incomes and expenses both present, ordered by date then Expense before Income (R-2, D-3);
  (f) indeterminate budget lines pass through with null status (R-2, S-6);
  (g) 450 rows across 3 pages are all included using the page-aware fake (R-7, S-9);
  (h) `Labels` reaches the writer by reference, unchanged (R-10);
  (i) empty month produces a valid DTO with zero totals and empty sections, no failure (R-2, S-7);
  (j) `GeneratedOn == IClock.Today` (Reconciliation #9).
- [x] 2.2 Add to the same test file the failure and safety cases: writer returns failure or throws gives
  `ReportRenderFailed` with 0 opens and 0 bytes on the target (R-9, R-14, S-11); `IOException` on open gives
  `ReportSaveFailed` (R-9, R-14); no writer for the requested format gives `ReportWriterUnavailable` and no
  repository call (R-1); no exception ever escapes and a cancelled token is not reported as failure (R-14);
  writing over a longer existing buffer is truncated via `SetLength` (R-9, D-3).
- [x] 2.3 Additive page-aware mode for `tests/…Unit/TestDoubles/FakeExpenseRepository.cs` and
  `FakeIncomeRepository.cs` (currently the fake returns the same page on every call); existing behavior and
  tests stay unchanged. (R-7, D-5)
- [x] 2.4 Create `src/…Application/UseCases/Exports/Common/ReportLineBuilder.cs`: maps expense/income to
  `ReportTransactionLine`, converting with `ICurrencyConverter.ConvertAsync(Money, Currency.DOP)` (never
  `DisplayAsync`); failure sets `AmountDop = null` and reports a missing rate. (R-3, R-4, D-3)
- [x] 2.5 Create `src/…Application/UseCases/Exports/Common/ReportOutput.cs`: pick the writer by `Format` from
  `IEnumerable<IReportWriter>`, render into a `MemoryStream`, open the target and copy only on success,
  `SetLength(Position)` when seekable, map failures to codes. (R-1, R-9, R-14, Reconciliation #2)
- [x] 2.6 Create `src/…Application/UseCases/Exports/ExportMonthSummaryReport/
  {ExportMonthSummaryReportCommand,ExportMonthSummaryReportUseCase}.cs` per D-3: pick writer first; run the
  three queries sequentially (`GetCategoryBreakdownQuery` with `ChatContextLimits.UnfoldedTopCount`); page
  expenses/incomes with `ReportPaging` over `MonthPeriod.Range(anchor)`; resolve category names with one
  `ICategoryRepository.GetAllAsync`; catch everything except `OperationCanceledException`, log and map to a
  code. Run 2.1 and 2.2 green. The name uses "Report", not `ExportDataUseCase`. (R-2, R-3, R-4, R-7, R-9,
  R-12, R-14)
- [x] 2.7 Run all four gates. All green before opening PR S1b. (R-12)

## 3. PDF writer, packages and DI (S2)

Depends on 2. Goal: `QuestPdfReportWriter` writes valid PDFs in both locales, packages are pinned with the
license banner, and the QuestPDF license is set once inside `AddReportExporters()`.

- [x] 3.1 **Version verification and pin task (first task of the slice).** Run
  `dotnet package search QuestPDF --exact-match` and `dotnet package search ClosedXML --exact-match` (or check
  nuget.org) and record the newest stable versions. Inspect each nuspec: QuestPDF must be >= 2024.3.0 with no
  `SkiaSharp` package dependency (own native Skia build); ClosedXML must not pull SixLabors.Fonts >= 2 (Six
  Labors Split License) and must be MIT with MIT/Apache-2.0 transitives. Re-read the QuestPDF Community license
  terms for the chosen version and confirm eligibility (personal, zero revenue). Record the findings (versions,
  nuspec dependencies, license reading) in the PR description. (R-11, D-9)
- [x] 3.2 Append the banner `ItemGroup` block to `Directory.Packages.props` (text from D-9: Infrastructure-only,
  Community-license eligibility reasoning, revenue-gated and version-specific, re-verify on every bump,
  rejected alternatives) with exact `PackageVersion` pins for QuestPDF and ClosedXML from 3.1. Add the two
  `PackageReference` entries (no versions) to `ControlDeFinanzasIA.Infrastructure.csproj` only. (R-11, R-12)
- [x] 3.3 SkiaSharp compatibility gate: `dotnet build -c Release` shows no NU1605/NU1608; `dotnet list
  src/ControlDeFinanzasIA.UI package --include-transitive` shows exactly one SkiaSharp version (2.88.9, from
  LiveChartsCore.SkiaSharpView.Avalonia 2.0.5). If QuestPDF depends on SkiaSharp, apply mitigation (a) pick a
  QuestPDF version that does not; only if impossible mitigation (b) transitive pin, then re-check the Dashboard
  charts. Record the output. (R-11, D-9)
- [x] 3.4 Write failing `tests/…Unit/Architecture/ArchitectureTests.cs` additions (appended): add `"QuestPDF"`,
  `"ClosedXML"` and `"DocumentFormat.OpenXml"` to both forbidden-namespace arrays (Application/Domain and UI
  sides) so the rules fail if any of those types leak; plus a source-scan test asserting
  `QuestPDF.Settings.License` is assigned in exactly one place, `QuestPdfConfiguration`/`AddReportExporters()`.
  (R-11, R-12, S-14)
- [x] 3.5 Write failing `tests/…Integration/Reports/QuestPdfReportWriterTests.cs`: month-summary output is
  non-empty, longer than 1 KB and starts with `%PDF-` under es-DO and en labels/cultures (S-8); es-DO and en
  outputs differ (labels honored, R-10); does not throw for an empty month (S-7), missing rates (footer note
  present, S-4), indeterminate budget lines (marker, never 0%/100%, S-6) and a description containing
  `ñ á €` plus an emoji (glyph check off, D-10); a failing render is returned as `Result.Failure`, not thrown
  (R-5, R-14); when no rate is missing the footer note is absent (R-4). (R-4, R-5, R-10)
- [x] 3.6 Write failing `tests/…Integration/Reports/ReportServiceCollectionExtensionsTests.cs`: after
  `AddReportExporters()` the container resolves two `IReportWriter` with distinct formats; calling it twice is
  idempotent (`TryAddEnumerable`); a PDF renders without any other global initialization. (R-1, R-11)
- [x] 3.7 Create `src/…Infrastructure/Reports/QuestPdfConfiguration.cs` (idempotent `Apply()`: License =
  Community, `CheckIfAllTextGlyphsAreAvailable = false`) and
  `ReportServiceCollectionExtensions.cs` (`AddReportExporters()` per D-10, the only place the license is set
  from DI; the writer constructor calls the same idempotent `Apply()`). (R-11, D-10)
- [x] 3.8 Create `src/…Infrastructure/Reports/QuestPdfReportWriter.cs`: `WriteMonthSummaryAsync` per D-6
  (Letter, 36pt margin, base size 10; summary table, unfolded category table with share and total row, budget
  table with `—` + indeterminate label, `ReportNoData` for empty sections, footer with generation date,
  missing-rates note only when flagged, page label); text and tables only; all text and formatting from
  `labels`/`labels.Culture`, never resx or `CurrentUICulture`; work wrapped in `Task.Run(..., ct)`; library
  exceptions converted to `Result.Failure("ReportRenderFailed")`. `WriteExpenseListAsync` returns a
  not-yet-implemented failure until S5. Run 3.5 and 3.6 green. (R-4, R-5, R-10, R-14, D-12)
- [x] 3.9 Smoke export on Windows: render one month-summary PDF to a temp path through a throwaway test or
  script and confirm it opens in a viewer; confirm no runtime SkiaSharp native conflict with the Dashboard
  charts by launching the app (`dotnet run --project src/ControlDeFinanzasIA.UI`) and viewing the Dashboard.
  Screenshot only, no simulated clicks. (R-5, D-9)
- [x] 3.10 Run all four gates. All green before opening PR S2. (R-11, R-12)

## 4. XLSX writer (S3)

Depends on 3. Goal: `ClosedXmlReportWriter` writes a two-sheet workbook ("Summary" styled, "Data" flat) whose
"Data" contract is stable for Phase 20.

- [x] 4.1 Write failing `tests/…Integration/Reports/ClosedXmlReportWriterTests.cs` (round-trip via
  `new XLWorkbook(ms)`): sheet names are exactly `["Summary","Data"]` in that order (R-6, S-2); the Data header
  row equals `Date, Type, Description, Category, Amount, Currency, AmountDOP` under es-DO labels and under en
  labels (invariant, D8, Reconciliation #5); one Data row per expense and per income (R-6, S-2); `Date` is a
  real date cell, `Amount` and `AmountDOP` are numeric (asserted with `Math.Round(..., 2)`), `Type` is
  `Expense`/`Income`, `Currency` is the ISO code (R-6); USD 100 at 60.00 gives `AmountDOP` 6000.00 (R-3, S-3);
  a missing rate gives a blank `AmountDOP` cell, never 0 (R-3, S-4, Reconciliation #8); Summary sheet holds the
  DOP totals, unfolded categories, budget lines with the "not available" marker for indeterminate lines, and
  labels from `ReportLabels` (R-2, S-5, S-6, R-10); missing-rates note appears only when flagged (R-4); an
  empty month still yields two sheets, Data with headers only (R-6, S-7); a description `=1+1` or
  `=HYPERLINK(...)` is stored as text with `HasFormula == false` (D-6); changing the `ReportLabels` strings
  changes Summary text (R-10). Failure from the library returns `Result.Failure`, not an exception (R-14).
- [x] 4.2 Create `src/…Infrastructure/Reports/ClosedXmlReportWriter.cs`: `WriteMonthSummaryAsync` per D-6
  (fixed invariant sheet-name constants; explicit column widths, never `AdjustToContents()`; bold header row,
  frozen panes; numeric cells formatted `#,##0.00`; Data date format `yyyy-mm-dd`; all text assigned as string
  values, never `FormulaA1`); `Task.Run(..., ct)`; exceptions to `Result.Failure("ReportRenderFailed")`.
  `WriteExpenseListAsync` returns a not-yet-implemented failure until S5. Run 4.1 green. (R-4, R-6, R-10,
  R-14)
- [x] 4.3 Register `ClosedXmlReportWriter` in `AddReportExporters()` (already declared in D-10); extend
  `ReportServiceCollectionExtensionsTests` to assert both formats resolve and are distinct. (R-1)
- [x] 4.4 Run all four gates (NetArchTest confirms ClosedXML/DocumentFormat.OpenXml types are Infrastructure-only,
  S-14). All green before opening PR S3. (R-12)

## 5. Dashboard export UI (S4)

Depends on 4. Goal: the Dashboard shows "Exportar PDF"/"Exportar Excel" for the displayed month, through a
real Avalonia save dialog, with labels built by the ViewModel and no double execution. Invoke the
`.claude/skills/frontend-design` skill for the buttons and status text.

- [ ] 5.1 Resx first (S-15, R-13): append to `src/…UI/Resources/Strings.resx` (en) and `Strings.es-DO.resx` the
  same key set: all report labels from D-7 (`ReportMonthSummaryTitle`, `ReportExpenseListTitle`,
  `ReportSummarySection`, `ReportExpensesLabel`, `ReportIncomesLabel`, `ReportNetBalanceLabel`,
  `ReportDeltaLabel`, `ReportCategoriesSection`, `ReportBudgetsSection`, `ReportCategoryColumn`,
  `ReportAmountDopColumn`, `ReportShareColumn`, `ReportBudgetColumn`, `ReportActualColumn`,
  `ReportPercentColumn`, `ReportStatusColumn`, `ReportDateColumn`, `ReportDescriptionColumn`,
  `ReportAmountColumn`, `ReportCurrencyColumn`, `ReportTotalLabel`, `ReportStatusOk`, `ReportStatusNear`,
  `ReportStatusExceeded`, `ReportStatusIndeterminate`, `ReportNoData`, `ReportFiltersLabel`,
  `ReportFilterAll`, `ReportFilterPeriodFormat`, `ReportFilterCategoryLabel`, `ReportFilterTagsFormat`,
  `ReportCountLabel`, `ReportGeneratedOnFormat`, `ReportMissingRatesNote`, `ReportPageLabel`) and UI keys
  (`ExportPdfButton`, `ExportExcelButton`, `ExportDialogTitle`, `ExportPdfFileTypeName`,
  `ExportExcelFileTypeName`, `ExportSucceededMessage`, `ExportFailedMessage`, `ExportMonthSummaryFileName`,
  `ExportExpenseListFileName`). Neutral, professional Spanish for es-DO. (R-13)
- [ ] 5.2 Append MANUAL accessors for every new key to `src/…UI/Resources/Strings.Designer.cs` (the Designer is
  hand-maintained; do not regenerate). (R-13)
- [ ] 5.3 Write failing resx-completeness test (`tests/…Unit/UI/ExportResourcesTests.cs`): every new key exists
  in `Strings.resx`, `Strings.es-DO.resx` and `Strings.Designer.cs`, both resx files define the same key set,
  and no value is empty. (R-13, S-15)
- [ ] 5.4 Write failing `tests/…Unit/UI/ReportLabelsFactoryTests.cs`: en vs es-DO titles differ; `Culture`
  equals the current culture; every `ReportLabels` string is non-empty in both locales. Test under both UI
  cultures, restoring the culture afterwards. (R-10, S-13)
- [ ] 5.5 Test doubles: `tests/…Unit/TestDoubles/FakeFileSaveDialog.cs` (returns null or a target; counts
  calls; can block on a `TaskCompletionSource`) and `ExportFixtures` builder producing the two new Dashboard
  constructor arguments. (R-15)
- [ ] 5.6 Write failing Dashboard tests in `tests/…Unit/UI/DashboardViewModelTests.cs`: cancel (dialog returns
  null) means the writer is never called, no stream opened, empty `ExportStatusMessage`, `IsExporting`
  cleared (R-8, S-10); PDF/XLSX success reaches the writer with the matching `Format` and the captured
  `SelectedMonth` and sets the success message with the file name (R-15, S-1, S-2); use case failure sets
  `IsExportError` and the localized failure message, command usable again (R-14, S-11); a thrown exception is
  swallowed and logged (R-14); two concurrent `ExecuteAsync` calls (dialog blocked on a TCS) produce exactly 1
  dialog call, `CanExecute` false while exporting, a new export can start afterwards (R-15, S-12);
  `CancellationToken` cancellation shows no failure message (R-14); suggested file name has kind + `yyyy-MM`
  and the right extension (R-8); captured `ReportLabels` equal the es-DO resx strings and es-DO culture under
  es-DO and the English strings under en (R-10, S-13).
- [ ] 5.7 Create `src/…UI/Formatting/ReportLabelsFactory.cs` (internal static `Create()` from the static
  `Strings` class plus `CultureInfo.CurrentCulture`). Run 5.4 green. (R-10, D-7)
- [ ] 5.8 Create `src/…UI/Services/AvaloniaFileSaveDialog.cs` copying the `AvaloniaClipboardService` pattern:
  resolve `IClassicDesktopStyleApplicationLifetime.MainWindow` → `TopLevel.GetTopLevel` on every call, log a
  Warning and return `null` when there is no `TopLevel`, `SaveFilePickerAsync` with title, suggested name,
  default extension, `ShowOverwritePrompt = true` and one `FilePickerFileType` (`*.pdf`/`application/pdf` or
  `*.xlsx`/`application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`), return
  `new SaveTarget(file.Name, _ => file.OpenWriteAsync(), owner: file)`. Only UI references Avalonia. No
  automated test (no `TopLevel` in tests); covered by manual check in section 8. (R-1, R-8, R-12, D-8)
- [ ] 5.9 Modify `src/…UI/ViewModels/DashboardViewModel.cs`: append exactly two constructor parameters after
  `logger` (`ExportMonthSummaryReportUseCase`, `IFileSaveDialog`), null-checked; add `IsExporting`,
  `ExportStatusMessage`, `IsExportError`, `ExportPdfCommand`/`ExportExcelCommand`
  (`AllowConcurrentExecutions = true`, `CanExecute = !IsExporting`) with the body guard over `IsExporting`, the
  month captured before the dialog, cancel returning with no message, and `finally` clearing the flag. Run 5.6
  green. (R-8, R-14, R-15, D-11)
- [ ] 5.10 Update the three existing fixtures to pass the two new arguments through `ExportFixtures`:
  `DashboardViewModelTests.CreateViewModel`, `NavigationServiceTests.CreateDashboardViewModel`,
  `ShellViewModelTests.CreateDashboardViewModel`. Existing assertions unchanged. (R-15, Reconciliation #11)
- [ ] 5.11 Modify `src/…UI/Views/DashboardView.axaml`: two `ledger-btn` buttons in the period-navigation grid
  (`ColumnDefinitions` grows by two) bound to the commands, plus a status `TextBlock` under `LoadError` bound
  to `ExportStatusMessage` with the Terracotta error style when `IsExportError`; all text via resx bindings,
  no code-behind logic; follow existing design tokens and the `frontend-design` skill. (R-13, R-15)
- [ ] 5.12 Append the Fase 18 block to `src/…UI/Composition/Bootstrap.cs` after the Phase 17 block:
  `services.AddReportExporters();`, `AddTransient<ExportMonthSummaryReportUseCase>()`,
  `AddSingleton<IFileSaveDialog, AvaloniaFileSaveDialog>()`. Write and pass `ExportCompositionTests` (real
  `Bootstrap`): both writers resolve as `IEnumerable<IReportWriter>`, the use case resolves, the dialog is
  `AvaloniaFileSaveDialog`, the Dashboard VM resolves. (R-1, R-11, R-15, D-10)
- [ ] 5.13 Smoke-launch (`dotnet run --project src/ControlDeFinanzasIA.UI`): confirm startup without DI
  failure, the Dashboard shows both buttons and the status area in es-DO and en. Screenshot only, no simulated
  clicks. (R-15)
- [ ] 5.14 Run all four gates. All green before opening PR S4. (R-12)

## 6. Expenses list export (S5)

Depends on 5. Goal: the Expenses screen exports its current filter across all pages in both formats, reusing
the ports, use-case plumbing and resx from earlier slices.

- [ ] 6.1 Write failing `tests/…Unit/Application/Exports/ExportExpenseListReportUseCaseTests.cs`: 450 matching
  expenses read as pages of 200/200/50 and all 450 rows in the report, none from other categories/dates,
  ignoring the incoming `Page` and overwriting `PageSize` to 200 (R-7, S-9); a zero-match filter yields a valid
  empty report, not a failure (R-7); `DateFrom > DateTo` or `Guid.Empty` in `TagIds` returns `InvalidFilter`
  and no repository call (D-4); `ReportFilterSummary` carries date range, category name (same dictionary as
  the lines) and tag count (D-4); `TotalDop` sums only non-null `AmountDop`, missing rate sets
  `HasMissingRates` and leaves the DOP cell empty (R-3, R-4); writer failure gives `ReportRenderFailed` with 0
  opens (R-9, R-14, S-11); `IOException` gives `ReportSaveFailed` (R-14); no writer gives
  `ReportWriterUnavailable` (R-1).
- [ ] 6.2 Create `src/…Application/DTOs/Reports/{ExpenseListReport,ReportFilterSummary}.cs` and
  `src/…Application/UseCases/Exports/ExportExpenseListReport/{ExportExpenseListReportCommand,
  ExportExpenseListReportUseCase}.cs` per D-4, reusing `ReportPaging`, `ReportLineBuilder` and `ReportOutput`.
  Run 6.1 green. (R-7, R-12, R-14)
- [ ] 6.3 Extend `tests/…Integration/Reports/QuestPdfReportWriterTests.cs`: `WriteExpenseListAsync` starts with
  `%PDF-`, non-empty, in es-DO and en; empty list, missing-rate rows (em dash, footer note) and long lists
  spanning multiple pages do not throw; header row repeats (structural check acceptable). (R-5, R-7, R-10,
  S-8)
- [ ] 6.4 Extend `tests/…Integration/Reports/ClosedXmlReportWriterTests.cs`: expense-list workbook has exactly
  `["Summary","Data"]`; Data holds only `Type = Expense` rows with the 7 invariant headers, numeric/date cells
  and blank `AmountDOP` on missing rate; Summary carries filter description, count and DOP total. (R-6, R-7)
- [ ] 6.5 Implement `WriteExpenseListAsync` in `QuestPdfReportWriter` (same frame as the summary, filter line,
  table Date | Description | Category | Amount | Currency | Amount (DOP), repeating header, total row, footer)
  and in `ClosedXmlReportWriter` (two-sheet shape per D-6); replace the not-yet-implemented failures. Run 6.3
  and 6.4 green. (R-4, R-5, R-6, R-7)
- [ ] 6.6 Resx: confirm the S4 keys cover the list (`ReportExpenseListTitle`, filter and count labels,
  `ExportExpenseListFileName`); append any Expenses-specific keys to `Strings.resx`, `Strings.es-DO.resx` and
  MANUAL accessors in `Strings.Designer.cs`; re-run the resx-completeness test. (R-13, S-15)
- [ ] 6.7 Write failing Expenses ViewModel tests in `tests/…Unit/UI/ExpensesViewModelTests.cs`: export passes
  `CurrentFilter with { PaymentMethodIds = null }` and ignores the current page (R-7,
  Reconciliation #4); cancel writes nothing and shows no message (R-8, S-10); success sets `StatusMessage`
  with the file name, failure sets `FormError` (Reconciliation #10, R-14); double-click is ignored while
  exporting (R-15, S-12); with null optional dependencies `CanExport` is false; labels come from
  `ReportLabelsFactory` (R-10). Confirm no existing Expenses fixture needs changes (optional parameters,
  Reconciliation #11).
- [ ] 6.8 Modify `src/…UI/ViewModels/ExpensesViewModel.cs`: append two optional constructor parameters after
  `categorizeExpenseUseCase` (`ExportExpenseListReportUseCase? exportExpenseList = null, IFileSaveDialog?
  fileSaveDialog = null`); export commands for both formats with the same body guard and status handling as
  the Dashboard, using `_localizer[...]`. Run 6.7 green. (R-15, D-11)
- [ ] 6.9 Modify `src/…UI/Views/ExpensesView.axaml`: new header row beside the "Movimientos" title with the
  two export buttons (design tokens, `frontend-design` skill, no code-behind logic, resx-bound text).
  (R-13, R-15)
- [ ] 6.10 Append to the Fase 18 `Bootstrap.cs` block: `services.AddTransient<ExportExpenseListReportUseCase>();`
  and extend `ExportCompositionTests` to resolve `ExpensesViewModel` with the use case and dialog injected.
  (R-1, D-10)
- [ ] 6.11 Smoke-launch: startup without DI failure, the Expenses screen shows the export buttons in both
  locales, and navigating Dashboard ↔ Expenses still works. Screenshot only, no simulated clicks. (R-15)
- [ ] 6.12 Run all four gates. All green before opening PR S5. (R-12)

## 7. Docs reconciliation (S6)

Depends on 6. Goal: docs and phase notes match the shipped state; Debts export recorded as a follow-up (R-16,
Reconciliation #12), not implemented.

- [ ] 7.1 Reconcile `docs/architecture.md`: replace or correct the `CsvExporter`/`ExportView` mentions with the
  shipped design (`IReportWriter`, `IFileSaveDialog`, `QuestPdfReportWriter`, `ClosedXmlReportWriter`,
  `AvaloniaFileSaveDialog`, DTOs in `Application/DTOs/Reports/`, no CSV in this phase). (R-12, R-17)
- [ ] 7.2 Create `spec/phases/phase-18/features.md` following the pattern of `spec/phases/phase-10/` through
  `phase-17/`, recording the Debts export as a follow-up with the predicted ~250-line estimate, the
  Expenses payment-method-filter gap (pre-existing, out of scope), and the CSV exclusion. (R-16, R-17)
- [ ] 7.3 Confirm no new setting, migration or schema change, and zero diff to existing queries or
  `ICurrencyConverter` (R-17): `git diff --name-only <phase base>^ HEAD` filtered by `Migrations/`,
  `SettingKeys`, `AppSettings`, `GetMonthSummaryQuery`, `GetCategoryBreakdownQuery`,
  `GetBudgetVsActualQuery`, `GetTransactionsQuery`, `ICurrencyConverter`, `CurrencyConverter` is empty.
- [ ] 7.4 Run all four gates. All green before opening PR S6.

## 8. Cierre

- [ ] 8.1 Run the full gate suite across the whole phase: `dotnet build -c Release` (0 warnings/errors),
  `dotnet test -c Release --no-build` (unit + integration), `dotnet format style --verify-no-changes` exit 0,
  NetArchTest R-1..R-5, source scan that `QuestPDF.Settings.License` appears once, resx completeness. All
  green. (R-11, R-12, R-13, S-14, S-15)
- [ ] 8.2 Manual check on Windows, real save dialog, both locales (the dialog cannot be automated): Dashboard
  "Exportar PDF" and "Exportar Excel" open the native dialog with the suggested name (kind + `yyyy-MM`) and
  the right file-type filter; cancel writes nothing and shows no message and both buttons re-enable (R-8,
  S-10); overwriting an existing larger file produces a valid file (D-3); Expenses export after a calendar
  day drill-down contains only that day (R-7). Record results with screenshots.
- [ ] 8.3 Manual check in external viewers: open the exported PDF in a PDF viewer (footer with generation date,
  unfolded categories, budget table, missing-rate note when applicable, es-DO and en) and the XLSX in
  Excel or LibreOffice (exactly two sheets "Summary" and "Data", seven invariant headers, numeric and date
  cells, blank `AmountDOP` on a missing rate, no formula execution from a `=...` description). (R-4, R-5,
  R-6, S-1, S-2, S-4)
- [ ] 8.4 Update `docs/roadmap.md` §Fase 18: mark the phase's completion criteria complete, with the Debts
  export and payment-method-filter gap recorded as follow-ups.
- [ ] 8.5 Update `docs/AI_HANDOFF.md` with the Phase 18 summary, including: buffering happens once in the use
  case (`ReportOutput`), writers are pure DTO → Stream; sheet names and Data headers are invariant English
  (Phase 20 contract); Summary totals are authoritative over the per-line `AmountDOP`; `QuestPdfConfiguration`
  glyph check is off deliberately; `IAsyncRelayCommand.ExecuteAsync` bypasses `CanExecute`, hence the body
  guard. (Reconciliation #2, #5, #7)
- [ ] 8.6 Update `AGENTS.md` project status: Phase 18 shipped (PDF and XLSX export of the month summary and
  the filtered Expenses list, libraries Infrastructure-only, test counts) and Phase 19 as next; remove the
  "Phase 18 is next" wording. Appended/edited in place, no restructuring. (R-12)
- [ ] 8.7 Write `verify-report.md` in `.atl/sdd/f-018p18-export/`, independently re-verifying every
  Acceptance Criteria checklist line against source and tests before archiving, including all 12
  Reconciliation points and the manual checks 8.2/8.3 (mark honestly if any was not run by hand).
- [ ] 8.8 Archive the spec (`F-018-export.spec.md` → `.done.md` with `git mv`, acceptance criteria checked with
  evidence) and write `archive-report.md` in `.atl/sdd/f-018p18-export/`. (Acceptance Criteria)
