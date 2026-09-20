# F-010 — Monthly Budgets per Category

> **Phase:** 10 — Monthly Budgets per Category
> **Status:** ✅ Archived (2026-09-20)
> **SDD:** `.atl/sdd/f-010p10-monthly-budgets/`

## Goal

Let the user set a monthly spending budget per category, compare it against
the real spend of that month, and surface the result on the Dashboard as a
progress bar with a clear `Ok` / `Near` / `Exceeded` status — without any
manual arithmetic and with foreign-currency expenses correctly counted.
Actual-spend aggregation reuses the existing Phase 8 SQL query unchanged;
currency normalization reuses the existing Phase 8 `ConvertAsync` pattern.

### Confirmed product decisions

- **Near-limit threshold: 80%.** A budgeted category becomes `Near` at ≥80%
  consumed, `Exceeded` at ≥100%. Implemented as a single constant in
  `BudgetEvaluator`.
- **Rollover: none.** Each month's budget is fully independent; no
  cross-period carry logic.

## Requirements

### R-1 — Budget CRUD per category per month

The system MUST provide a dedicated management screen (`BudgetsView` /
`BudgetsViewModel`, reachable via `RouteNames.Budgets`) where the user can
create, edit, and delete a budget amount for a given category and month.
Deletion MUST remove the budget row entirely (no soft-delete/archival
concept is introduced).

### R-2 — Upsert semantics (no duplicates)

Setting a budget for a category+month combination that already has one MUST
update the existing budget's amount rather than create a second row. This
MUST be enforced at two independent layers: the `CreateOrUpdateBudget`
use case (query-then-update-or-insert on `Year+Month+CategoryId`) AND a
unique database index on `(Year, Month, CategoryId)` in
`BudgetConfiguration`, so the invariant holds even if a future caller
bypasses the use case.

### R-3 — Valid construction only (domain invariants)

`Budget.Create` MUST reject an `Amount` ≤ 0 and MUST reject an invalid
`Year`/`Month` (`Month` outside 1–12). `Budget` MUST be a class with
private setters and a static `Create` factory — matching `Debt`, `Expense`,
and `RecurringRule` — so that constructing an invalid `Budget` from any
layer outside the domain (application, infrastructure, UI) is impossible.
This replaces the existing public-`init` record stub, which currently
allows exactly that.

### R-4 — Actual spend reuses existing aggregation

Actual spend per category for a month MUST be computed via the existing
`ExpenseRepository.GetCategoryTotalsAsync` query. The system MUST NOT
introduce a new aggregation query, and MUST NOT sum expenses in memory from
paginated/loaded transaction lists.

### R-5 — DOP normalization on both sides, missing rate is indeterminate

Both the budget amount and the actual spend total MUST be normalized to DOP
via `ICurrencyConverter.ConvertAsync` before being compared — `DisplayAsync`
MUST NOT be used for this comparison, since it is a presentation-only
helper not suited for a value driving a status computation. If the
conversion of either side fails or a rate is missing, that category's
budget status MUST surface as an explicit indeterminate state to the
caller (mirroring the `hasMissingRates` flag pattern from
`GetCategoryBreakdownQuery`) and MUST NOT silently render as 0% or 100%
consumed.

### R-6 — Pure-domain status evaluation

`BudgetStatus` (`Ok` at <80% consumed, `Near` at 80%–<100%, `Exceeded` at
≥100%) MUST be computed by a pure domain service, `BudgetEvaluator`, that
takes a budget amount and an actual-spend amount already normalized to the
same currency by the caller, and performs no I/O, no DI-resolved
dependencies, and no currency conversion of its own. The 80%/100%
thresholds MUST be expressed as constants inside `BudgetEvaluator`.

### R-7 — Dashboard budget-progress section

The Dashboard MUST show a budget-progress section with one progress bar per
budgeted category for the currently navigated month, color-coded by
`BudgetStatus`. This section MUST reuse the Dashboard's existing
month-navigation state (the same anchor month already driving the
category-breakdown chart) — it MUST NOT introduce a second, independent
month selector. Categories with no budget set for the navigated month MUST
NOT render a progress bar.

### R-8 — Explicit scope exclusions

The following are explicit non-requirements for this phase, not omissions:

- **Global (non-per-category) budgets.** `CategoryId` remains nullable in
  the schema for future-proofing, but the use-case layer MUST reject
  `CategoryId == null` for v1 (this also sidesteps the SQLite unique-index
  NULL-distinctness gap for the out-of-scope case).
- **Rollover across months.** Every month's budget evaluation is
  independent of prior months' unspent or overspent amounts.
- **Notifications/alerts.** No push, toast, or out-of-app notification is
  introduced; the in-dashboard color-coded progress bar is the only status
  surface for this phase.

### R-9 — Localization

Any month label rendered by the budget management screen or the Dashboard
budget-progress section MUST use `CultureInfo.CurrentCulture`, consistent
with the existing pattern in `DashboardViewModel.FormatMonthLabel` and the
Phase 9 calendar weekday/month labels.

**Reconciliation note (2026-09-20):** static UI copy (labels, button text,
sidebar entry) on the new `BudgetsView` and the Dashboard budget-progress
section MUST be added as `Strings.resx`/`Strings.es-DO.resx` entries, not
hard-coded inline XAML strings. `DebtsView`/`RecurringRulesView` hard-code
Spanish text directly and are the exception, not the rule — the two most
recent phases (8 Dashboard, 9 Calendar) both route static copy through
`Strings.Designer.cs`, and Phase 10 follows that more recent precedent.

## Scenarios

### S-1 — Create, edit, and delete a budget from the management screen

- GIVEN the `BudgetsView` open with no existing budget for "Comida" in the
  current month
- WHEN the user sets an amount and saves, then later edits the amount, then
  later deletes it
- THEN each action is reflected immediately in the persisted budget list:
  a row is created, then updated, then removed

### S-2 — Setting a budget twice for the same category+month updates, not duplicates

- GIVEN a budget of 10,000 DOP already set for "Comida" in September 2026
- WHEN the user sets a budget of 15,000 DOP for "Comida" in September 2026
  again
- THEN exactly one `Budget` row exists for that category+month, now with
  amount 15,000, both immediately after the use case runs and after a
  direct query against the unique index

### S-3 — `Budget.Create` rejects invalid amount and month

- GIVEN an attempt to construct a `Budget` with `Amount == 0` (or negative),
  or with `Month == 13` (or `Month == 0`)
- WHEN `Budget.Create` is called with those inputs
- THEN construction fails with a validation error, and no code path outside
  `Budget` can produce an instance bypassing this check

### S-4 — Actual spend comes from the existing aggregation query, unmodified

- GIVEN a month with several expenses recorded across multiple categories
- WHEN the budget-vs-actual evaluation runs for that month
- THEN the actual-spend figures are sourced from
  `ExpenseRepository.GetCategoryTotalsAsync` with no new SQL query
  introduced and no in-memory summation of expense lists

### S-5 — Foreign-currency expense counts against a DOP budget

- GIVEN a budget of 15,000 DOP for "Comida" in the current month, and
  expenses in that category totaling a mix of DOP and USD amounts
- WHEN the budget-vs-actual evaluation runs
- THEN the USD portion is converted to DOP via `ConvertAsync` before being
  summed against the DOP portion, and the resulting percent-consumed
  reflects the correctly normalized total

### S-6 — Missing conversion rate surfaces as indeterminate, never 0% or 100%

- GIVEN a budgeted category with an expense in a currency for which
  `ICurrencyConverter.ConvertAsync` has no rate available
- WHEN the budget-vs-actual evaluation runs
- THEN that category's status is surfaced as indeterminate (a missing-rate
  flag propagated to the caller), and the UI does not render it as `Ok`,
  `Near`, or `Exceeded`

### S-7 — `BudgetEvaluator` threshold boundaries

- GIVEN a normalized budget amount and normalized actual-spend amounts of
  exactly 79.99%, exactly 80%, exactly 99.99%, and exactly 100% of the
  budget
- WHEN each amount is passed to `BudgetEvaluator`
- THEN the returned statuses are `Ok`, `Near`, `Near`, and `Exceeded`
  respectively

### S-8 — Dashboard budget section follows the Dashboard's own month navigation

- GIVEN the Dashboard open on the current month with at least one budgeted
  category
- WHEN the user navigates the Dashboard to the previous month using the
  existing month-navigation control
- THEN the budget-progress section recalculates for that previous month
  using the same anchor month as the category-breakdown chart, without any
  separate month selector being shown or used

### S-9 — Global (category-less) budgets are rejected in v1

- GIVEN a request to create or update a budget with `CategoryId == null`
- WHEN `CreateOrUpdateBudget` is invoked with that input
- THEN the use case rejects it with a validation error, even though the
  underlying schema column remains nullable

### S-10 — Month labels respect the current culture

- GIVEN the application running under a non-default `CultureInfo`
- WHEN the budget management screen and the Dashboard budget-progress
  section render their month label
- THEN both labels are formatted using `CultureInfo.CurrentCulture`,
  consistent with `DashboardViewModel.FormatMonthLabel`

## Out of Scope

Global (non-per-category) budgets, rollover of unspent budget across
months, notifications/alerts beyond the in-dashboard visual status (owned
by a later phase per the roadmap), budget overlays on the calendar view,
goals, and drill-down navigation from a progress bar into filtered
expenses (not built unless explicitly requested later).

## Acceptance Criteria

- [x] `Budget.Create` rejects amount ≤ 0 and invalid year/month; an invalid
  budget cannot be constructed from outside the domain. Confirmed by direct
  read of `Domain/Budgets/Budget.cs` (private setters, private ctor, `Create`
  factory validates `Year`/`Month`/`CategoryId`/`Amount`) plus passing unit
  tests.
- [x] A budget can be created, edited, and deleted per category for a given
  month from `BudgetsView`. `BudgetsViewModel.SaveCommand`/`DeleteCommand`/
  `EditCommand` wired to `CreateOrUpdateBudgetUseCase`/`DeleteBudgetUseCase`.
- [x] Setting a budget twice for the same category+month updates rather
  than duplicating it, enforced both at the use-case layer
  (`CreateOrUpdateBudgetUseCase`'s `GetByCategoryAndMonthAsync` check) and via
  a DB unique index on `(Year, Month, CategoryId)`
  (`BudgetConfiguration.HasIndex(...).IsUnique()`).
- [x] The Dashboard shows percent consumed and `Ok`/`Near`/`Exceeded` per
  budgeted category, driven by the Dashboard's existing month-navigation
  state. Verified running the app: "Progreso de presupuestos" card renders
  below "Próximos pagos", recalculating on `PreviousMonth`/`NextMonth`.
- [x] A foreign-currency expense counts against a DOP budget at its
  `ConvertAsync`-converted amount. Confirmed by direct read of
  `GetBudgetVsActualQuery.cs` — `ConvertAsync` used on both sides, never
  `DisplayAsync`.
- [x] A failed or missing conversion rate surfaces as an indeterminate
  status, never as a silently wrong percentage.
  `BudgetProgressItem.IsIndeterminate => Status is null`, confirmed directly.
- [x] `ExpenseRepository.GetCategoryTotalsAsync` is unchanged; aggregation
  still happens in SQL, not in memory. Confirmed: no diff to
  `ExpenseRepository.cs` across all 4 Phase 10 commits.
- [x] `CategoryId == null` budgets are rejected at the use-case layer in v1.
  `CreateOrUpdateBudgetValidator` rejects `CategoryId == null` with a
  dedicated global-budget message.
- [x] Month labels on both the management screen and the Dashboard section
  use `CultureInfo.CurrentCulture`, matching `DashboardViewModel.FormatMonthLabel`.
- [x] `AppDbContextModelSnapshot.cs` matches the `AddBudgets` migration —
  scaffolded via `dotnet ef migrations add AddBudgets`, confirmed to touch
  only the `Budgets` table.
- [x] Full test suite green, no regressions: 861/861 (703 unit + 158
  integration), re-verified independently during `sdd-verify`.
