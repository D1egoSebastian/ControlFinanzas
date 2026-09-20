# Tasks — F-010 Monthly Budgets per Category

## Review Workload Forecast

| Field | Value |
|---|---|
| Estimated changed lines | 2000–2400 total across all slices; each slice individually lands in the 500–800 range |
| 400-line budget risk | High (overall and per-slice — every slice exceeds the 400-line budget on its own) |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 (Domain + Infrastructure + migration) → PR 2 (Application use cases) → PR 3 (Management UI) → PR 4 (Dashboard addon) |
| Delivery strategy | ask-on-risk |
| Chain strategy | stacked-to-main |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: High

This follows the design's own "Recommended Slicing" section exactly: 4 strictly chained slices,
each one a complete, mergeable unit. Chaining here is driven by both size (every slice individually
exceeds 400 lines once domain/infra/tests are counted) and dependency order — slice 2 needs slice 1's
repository, slice 3 needs slice 2's use cases, and slice 4 needs slice 2's query while also touching
Phase 8's `DashboardViewModel` constructor (the design's flagged blast-radius risk). `Bootstrap.cs` is
the only file both slice 2 and slice 3 touch, which is exactly why they must stay sequential rather
than parallel — see the design's "Chaining vs. parallel" subsection. If a reviewer wants strictly
sub-400-line PRs, slice 1 (domain+infra+migration+tests) and slice 4 (Dashboard ctor-arity repair
touches many existing test files) are the two most likely candidates to split further; this was left
as a single unit per slice to match the design's exact recommendation, not pre-split speculatively.

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|---|---|---|---|
| 1 | Domain + Infrastructure + migration | PR 1 | Base = main. No UI, no consumers yet — safe to merge alone. |
| 2 | Application use cases | PR 2 | Base = PR 1's merged main. Depends on `IBudgetRepository`/`BudgetRepository`. |
| 3 | Management UI (`BudgetsView`) | PR 3 | Base = PR 2's merged main. Depends on the 4 use cases/queries. |
| 4 | Dashboard progress addon | PR 4 | Base = PR 3's merged main. Modifies Phase 8's `DashboardViewModel` ctor — repairs every existing Dashboard test. |

## 1. Domain + Infrastructure + migration

No UI, no consumers of `Budget` exist yet — safe to merge in isolation.

- [x] 1.1 Rewrite `Domain/Budgets/Budget.cs`: record → sealed class with private setters, `MinYear`/`MaxYear` constants, private ctor + EF ctor, static `Create(int year, int month, Guid categoryId, Money amount)` factory validating year range, month 1–12, `categoryId != Guid.Empty`, `amount.Amount > 0` (strict), and a `ChangeAmount(Money amount)` mutator for the upsert path. Drop `CreatedAt`. (R-3, D-1)
- [x] 1.2 Create `Domain/Budgets/BudgetStatus.cs`: `enum BudgetStatus { Ok, Near, Exceeded }`. (R-6)
- [x] 1.3 Create `Domain/Budgets/BudgetEvaluator.cs`: static pure class, `NearThresholdPercent = 80m`, `ExceededThresholdPercent = 100m`, `PercentDecimals = 2` constants, `BudgetEvaluation Evaluate(decimal budgetAmount, decimal actualSpend)` — thresholds evaluated on the *rounded* percent, throws `ArgumentOutOfRangeException` on `budgetAmount <= 0` or `actualSpend < 0`, percent never clamped. (R-6, D-2)
- [x] 1.4 Modify `Application/Abstractions/IBudgetRepository.cs`: add `GetByMonthAsync(int year, int month, CancellationToken)` and `GetByCategoryAndMonthAsync(Guid categoryId, int year, int month, CancellationToken)`; keep the 5 existing stub methods verbatim, add XML docs. (D-3)
- [x] 1.5 Create `Infrastructure/Persistence/Configurations/BudgetConfiguration.cs`: `ToTable("Budgets")`, `OwnsOne(Amount)` with `Amount`/`AmountCurrency` columns (`HasPrecision(18,4)`), `Currency` scalar with `HasConversion<int>()`, unique index on `(Year, Month, CategoryId)`, no FK navigation to `Category`. (R-2, D-4)
- [x] 1.6 Create `Infrastructure/Persistence/Repositories/BudgetRepository.cs`: `DebtRepository` template for `AddAsync`/`RemoveAsync`/`GetByIdAsync`/`GetAllAsync`, plus `GetByMonthAsync`/`GetByCategoryAndMonthAsync`. `UpdateAsync` MUST explicitly copy the owned `Amount` entry after `CurrentValues.SetValues` — `_db.Entry(existing).Reference(b => b.Amount).TargetEntry!.CurrentValues.SetValues(budget.Amount)` — because `SetValues` does not copy `OwnsOne` navigations and a plain `DebtRepository`-style copy would silently discard every amount edit. (R-2, D-7)
- [x] 1.7 Modify `Infrastructure/Persistence/AppDbContext.cs`: add `public DbSet<Budget> Budgets => Set<Budget>();`.
- [x] 1.8 Modify `Infrastructure/Persistence/ServiceCollectionExtensions.cs`: add `services.AddScoped<IBudgetRepository, BudgetRepository>();` in `AddPersistence` (not `Bootstrap.cs`). (D-12)
- [x] 1.9 Scaffold migration with `dotnet ef migrations add AddBudgets` (do not hand-author). Confirm the generated `Up` contains only `CreateTable("Budgets")` + the unique index and touches no other table; confirm `Down` drops only `Budgets`. If any `Expenses`/`Debts` DDL appears, the snapshot was stale — stop and fix the snapshot before continuing.
- [x] 1.10 Confirm `Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs` now contains the `Budget` entity with the owned `Money` and the unique index — do not touch it from any later slice. Run `Bootstrap.ApplyMigrations` against an existing dev DB and a fresh one.
- [x] 1.11 Unit tests for `Budget.Create`/`ChangeAmount`: rejects amount 0 and negative, `Month` 0/13, `Year` out of range, `Guid.Empty` category; accepts a valid budget; `Currency` denormalized from `Amount.Currency`; no public ctor exists outside `Create`. (S-3)
- [x] 1.12 Unit tests for `BudgetEvaluator`: the full boundary table — 0 spend → `0.00/Ok`; exactly 79.99% → `Ok`; exactly 80% → `Near`; exactly 99.99% → `Near`; exactly 100% → `Exceeded`; >100% not clamped (e.g. 133.33% → `Exceeded`); `budgetAmount <= 0` throws. (S-7)
- [x] 1.13 Integration tests for `BudgetRepository` against the SQLite harness (`DashboardAggregateQueryTests` pattern): `Amount`/`AmountCurrency` round-trip through `OwnsOne`; upsert regression — set 15000, update to 9000, re-read from a fresh context, assert `Amount.Amount == 9000m` (D-7 guard); unique index rejects a duplicate `(Year, Month, CategoryId)`; `GetByMonthAsync` excludes other months; `GetByCategoryAndMonthAsync` returns `null` when absent. (S-2)

## 2. Application use cases

Depends on 1 (`IBudgetRepository`/`BudgetRepository`).

- [ ] 2.1 Create `Application/UseCases/Budgets/CreateOrUpdateBudget/CreateOrUpdateBudgetCommand.cs`: `record(int Year, int Month, Guid? CategoryId, decimal Amount, Currency Currency)`. (D-6)
- [ ] 2.2 Create `Application/UseCases/Budgets/CreateOrUpdateBudget/CreateOrUpdateBudgetValidator.cs`: `Year` in `Budget.MinYear..MaxYear`, `Month` 1–12, `CategoryId` not null / not `Guid.Empty` (global-budget rejection message per R-8), `Amount` > 0. (R-8, D-6)
- [ ] 2.3 Create `Application/UseCases/Budgets/CreateOrUpdateBudget/CreateOrUpdateBudgetUseCase.cs`: validate → `ICategoryRepository.GetByIdAsync` (null ⇒ `CategoryNotFound`) → `IBudgetRepository.GetByCategoryAndMonthAsync` → `try/catch(ArgumentException)` wrapping `Money.Create` + either `Budget.Create`+`AddAsync` or `existing.ChangeAmount`+`UpdateAsync` → propagate repository `Result` or `Result.Success`. (R-1, R-2, D-6)
- [ ] 2.4 Create `Application/UseCases/Budgets/DeleteBudget/DeleteBudgetUseCase.cs`: thin wrapper over `IBudgetRepository.RemoveAsync(Guid budgetId, CancellationToken)`. (R-1)
- [ ] 2.5 Create `Application/UseCases/Budgets/GetBudgetsForMonth/GetBudgetsForMonthQuery.cs`: `ExecuteAsync(int year, int month, CancellationToken)` → `Result.Success(await _repository.GetByMonthAsync(...))`, `GetDebtsQuery` shape. Reads the repository directly — does NOT compose into `GetBudgetVsActualQuery` (D-5).
- [ ] 2.6 Create `Application/UseCases/Budgets/GetBudgetVsActual/BudgetVsActualResult.cs`: `BudgetVsActualLine` (nullable `BudgetDop`/`ActualDop`/`PercentConsumed`/`Status`, `IsIndeterminate`) and `BudgetVsActualResult` (`MonthAnchor`, `Lines`, `TotalBudgetDop`, `TotalActualDop`, `HasMissingRates`). (R-5, D-8)
- [ ] 2.7 Create `Application/UseCases/Budgets/GetBudgetVsActual/GetBudgetVsActualQuery.cs`: early-return empty result when no budgets exist for the month; call `IExpenseRepository.GetCategoryTotalsAsync` unchanged; filter to budgeted categories BEFORE converting (a non-budgeted category's conversion failure must not raise `HasMissingRates`); convert both actual and budget sides via `ICurrencyConverter.ConvertAsync` (never `DisplayAsync`); a partial actual-side failure poisons the whole category; resolve category names via `ICategoryRepository.GetAllAsync()` so zero-spend budgeted categories still render a name; order by `PercentConsumed` descending (indeterminate last) then `CategoryName` ordinal. (R-4, R-5, D-8)
- [ ] 2.8 Modify `UI/Composition/Bootstrap.cs`: register `AddTransient<CreateOrUpdateBudgetUseCase>()`, `AddTransient<DeleteBudgetUseCase>()`, `AddTransient<GetBudgetsForMonthQuery>()`, `AddTransient<GetBudgetVsActualQuery>()`, `AddTransient<IValidator<CreateOrUpdateBudgetCommand>, CreateOrUpdateBudgetValidator>()`. (D-12)
- [ ] 2.9 Unit tests for `CreateOrUpdateBudgetUseCase`: new category+month → `AddAsync`; same category+month again → `UpdateAsync`, not a second `AddAsync` (S-2); `CategoryId == null` → `Validation` failure with the global-budget message (S-9); unknown category → `CategoryNotFound`; amount 0 → `Validation`; a `Budget.Create` throw surfaces as `DomainInvariant`, never as an unhandled exception. (S-2, S-9)
- [ ] 2.10 Unit tests for `GetBudgetVsActualQuery` with fakes for all 4 dependencies: USD expense counts against a DOP budget at the converted amount (S-5); budget with zero spend → `0.00/Ok` with name resolved via `ICategoryRepository`; budget-side conversion failure → indeterminate line + `HasMissingRates`, never 0%/100% (S-6); actual-side partial failure poisons that whole category; a failure in a non-budgeted category leaves `HasMissingRates` false; no budgets → empty result and `GetCategoryTotalsAsync` NOT called (S-4); deterministic ordering on a percent tie. (S-4, S-5, S-6)

## 3. Management UI

Depends on 2 (the 4 use cases/queries).

- [ ] 3.1 Modify `UI/Navigation/RouteNames.cs`: add `public const string Budgets = "Budgets";` — literal `"Budgets"` matters for `ShellViewModel`'s sidebar-highlight match. (D-12)
- [ ] 3.2 Modify `UI/Navigation/NavigationService.cs`: add `{ RouteNames.Budgets, typeof(BudgetsViewModel) }` to the ctor `_routes` dictionary.
- [ ] 3.3 Create `UI/ViewModels/BudgetRow.cs`: `record(Guid Id, Guid CategoryId, string CategoryName, decimal Amount, Currency Currency)`. (D-10)
- [ ] 3.4 Create `UI/ViewModels/BudgetsViewModel.cs`: `DebtsViewModel` template — ctor (`GetBudgetsForMonthQuery`, `CreateOrUpdateBudgetUseCase`, `DeleteBudgetUseCase`, `ICategoryRepository`, `IClock`, `ILogger`) fire-and-forget `LoadAsync`; `SelectedMonth`/`SelectedMonthLabel` (`CultureInfo.CurrentCulture`, `DashboardViewModel.FormatMonthLabel` pattern); `SelectedCategoryId`/`Amount`/`Currency`/`FormError`/`StatusMessage`/`IsLoading`/`SelectedBudget`; `Budgets` observable collection; `Categories`/`Currencies`; `OnSelectedBudgetChanged` hydrates the form; `SaveAsync`/`DeleteAsync`/`PreviousMonthAsync`/`NextMonthAsync` commands. `SaveAsync` never throws — `Result.Failure` surfaces into `FormError`. (R-1, R-9, D-10)
- [ ] 3.5 Create `UI/Views/BudgetsView.axaml(.cs)`: two-column `Grid` layout, `x:Name="Root"` for the delete-command binding pattern (`DebtsView.axaml` precedent). Left column: month header + prev/next commands, category `ComboBox` (`ExpensesView.axaml` line-20 pattern), amount `TextBox`, currency `ComboBox`, `FormError`/`StatusMessage`, save button. Right column: `ItemsControl` over `Budgets` with category name, amount, delete button, and an empty-state message. All static copy (labels, buttons, empty state, month header) sourced from `Strings.resx`/`Strings.es-DO.resx` — resolved per R-9's broad reading, NOT hardcoded Spanish. (R-1, R-9)
- [ ] 3.6 Modify `UI/Resources/Strings.resx`, `Strings.es-DO.resx`, `Strings.Designer.cs`: add the ~12 `BudgetsView` keys (title, month-nav labels, category/amount/currency field labels, save/delete button text, default form-error/status messages, empty-state message) plus the sidebar `"Presupuestos"` label key. `Strings.Designer.cs` accessors must be added by hand — the build does not auto-regenerate it (known Phase 8 finding). (R-9)
- [ ] 3.7 Modify `UI/ViewModels/ShellViewModel.cs`: add the `Budgets` `RouteMenuItem` using the new resx-backed sidebar label (per R-9's resolution — do not hardcode `"Presupuestos"` inline).
- [ ] 3.8 Modify `UI/Views/ViewLocator.cs`: add `BudgetsViewModel` in BOTH `Build` and `Match` — the omission Phase 9 caught for `DebtsViewModel`.
- [ ] 3.9 Modify `UI/Composition/Bootstrap.cs`: add `AddTransient<BudgetsViewModel>()`.
- [ ] 3.10 Unit tests for `BudgetsViewModel`: load populates `Budgets` and `Categories`; `SaveAsync` calls the upsert use case with the current form values; a failure result lands in `FormError` without clearing the form; selecting a `BudgetRow` hydrates the form fields; `PreviousMonthAsync`/`NextMonthAsync` reload for the new month. (S-1, S-10)

## 4. Dashboard progress addon

Depends on 2 (`GetBudgetVsActualQuery`). Modifies Phase 8's `DashboardViewModel` — repairs every existing constructor-based Dashboard test.

- [ ] 4.1 Create `UI/ViewModels/BudgetProgressItem.cs`: `record(Guid CategoryId, string CategoryName, string AmountsDisplay, string PercentDisplay, double ProgressValue, BudgetStatus? Status)` with `IsIndeterminate`/`IsNear`/`IsExceeded` computed properties. `ProgressValue` clamped at 100 for the bar; `PercentDisplay` keeps the true unclamped percent. (R-7, D-11)
- [ ] 4.2 Modify `UI/ViewModels/DashboardViewModel.cs`: add a 7th ctor parameter `GetBudgetVsActualQuery getBudgetVsActualQuery`; add `BudgetProgress` observable collection; add `ApplyBudgetVsActual(Result result)` ending with `HasMissingRates |= budgetVsActual.HasMissingRates`; join the query into `LoadAsync`'s existing `Task.WhenAll` as a 4th concurrent, month-dependent task (rebuilt on `PreviousMonth`/`NextMonth`) — do NOT follow the load-once `LoadUpcomingPaymentsAsync` pattern, since the budget section is anchored to the navigated month, not to today. (R-7, D-11)
- [ ] 4.3 Modify `UI/Views/DashboardView.axaml`: add a new budget-progress `Border` card after the "Próximos pagos" card, following that card's structure (title `TextBlock`, `ItemsControl`/`DataTemplate` over `BudgetProgress`, empty-state `TextBlock`); progress bar bound to `ProgressValue`/`IsIndeterminate` with `near`/`exceeded` style classes. (R-7, D-11)
- [ ] 4.4 Modify `UI/Styles/LedgerStyles.axaml`: add `ProgressBar.budget-progress` (`SageBrush`, default/`Ok`), `.near` (`AmberBrush`), `.exceeded` (`TerracottaBrush`) selectors — no new color tokens needed.
- [ ] 4.5 Modify `UI/Resources/Strings.resx`, `Strings.es-DO.resx`, `Strings.Designer.cs`: add `DashboardBudgetProgressTitle`, `DashboardEmptyBudgetProgressMessage`, `DashboardBudgetIndeterminateLabel` keys, with manual `Strings.Designer.cs` accessors.
- [ ] 4.6 Repair every existing Phase 8 `DashboardViewModel` unit test broken by the new 7th constructor parameter — add a fake/mock `GetBudgetVsActualQuery` to each existing test fixture's construction call. This is mechanical but must be budgeted explicitly (flagged in the design's Open Questions as the single largest non-obvious cost in Phase 10).
- [ ] 4.7 New `DashboardViewModel` unit tests: `BudgetProgress` populated from the query result; `ProgressValue` clamped at 100 while `PercentDisplay` keeps e.g. `133.3%`; `IsNear`/`IsExceeded` map correctly from `Status`; an indeterminate line renders `IsIndeterminate == true` and sets `HasMissingRates`; the budget section recalculates on `PreviousMonth`/`NextMonth` using the same anchor as the category-breakdown chart, without a separate month selector. (R-7, S-8)

## 5. Cierre

- [ ] 5.1 Run build, `dotnet test` (unit + integration), `dotnet format style --verify-no-changes`, and `ArchitectureTests`/NetArchTest. All green, no regressions.
- [ ] 5.2 Run the app (`dotnet run`) against an existing dev DB and a fresh DB; visually confirm `BudgetsView` CRUD and the Dashboard budget-progress card render correctly, including the indeterminate/missing-rate state.
- [ ] 5.3 Update `docs/architecture.md` if it needs a Phase 10 entry, following the pattern used for Phases 8/9.
- [ ] 5.4 Create `spec/phases/phase-10/features.md` following the pattern of `spec/phases/phase-08/` and `phase-09/`.
- [ ] 5.5 Archive the spec (`F-010-monthly-budgets.spec.md` → `.done.md`) and write `verify-report.md`/`archive-report.md` in `.atl/sdd/f-010p10-monthly-budgets/`, re-verifying gates independently before archiving.
