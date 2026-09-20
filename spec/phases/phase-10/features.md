# Phase 10 — Monthly Budgets per Category

> **Phase:** 10 — Monthly Budgets per Category
> **Status:** ✅ Completed (2026-09-20) — see `spec/features/phase-10/F-010-monthly-budgets.done.md`
> **Spec ID:** F-010
> **Dependencies:** Phase 8 ✅ (reuses `ExpenseRepository.GetCategoryTotalsAsync` unchanged, the
> `ConvertAsync`-to-DOP normalization pattern, `MonthPeriod`), Phase 9 ✅ (`CultureInfo.CurrentCulture`
> month-label convention)
> **Roadmap:** `docs/roadmap.md` §Fase 10
> **Stack:** .NET 8 · Avalonia 11.2.7 (MVVM strict) · EF Core 8.0.11 + SQLite ·
> xUnit + FluentAssertions

## 1. Summary

Phase 10 lets the user set a monthly spending budget per category, compares it against the real
spend of that month, and surfaces the result on the Dashboard as a progress bar with a clear
`Ok`/`Near`/`Exceeded` status. Actual-spend aggregation reuses the existing Phase 8 SQL query
unchanged; currency normalization reuses the existing Phase 8 `ConvertAsync` pattern applied on
**both** sides of the comparison (budget and actual). Confirmed product decisions: near-limit
threshold is 80% (`Near`), exceeded at 100%; no rollover — every month's budget is fully
independent.

## 2. Model

```
Domain/Budgets/
  Budget                     — rewritten stub → invariant-carrying class, static Create factory
  BudgetStatus                — enum { Ok, Near, Exceeded }
  BudgetEvaluator              — static pure domain service, 80%/100% thresholds as constants

Application/UseCases/Budgets/
  CreateOrUpdateBudget/       — upsert command + validator + use case
  DeleteBudget/                — thin RemoveAsync wrapper
  GetBudgetsForMonth/          — GetDebtsQuery-shaped query
  GetBudgetVsActual/           — clones GetCategoryBreakdownQuery's DOP-normalization body,
                                  applied to both sides; per-line indeterminacy on conversion failure

Infrastructure/Persistence/
  BudgetConfiguration          — OwnsOne(Amount), unique index (Year, Month, CategoryId)
  BudgetRepository              — DebtRepository template + explicit owned-Amount copy in UpdateAsync

UI/ViewModels/
  BudgetsViewModel/BudgetRow    — management screen (DebtsViewModel template)
  BudgetProgressItem            — Dashboard card row projection (clamped bar value, true percent text)
```

## 3. Slice plan

| ID | Slice | Estimate | Status |
| --- | --- | --- | --- |
| 1 | Domain + Infrastructure + migration (`Budget` rewrite, `BudgetEvaluator`, `BudgetRepository`, `AddBudgets` migration) | L | ✅ Completed |
| 2 | Application use cases (upsert/delete/query use cases, DOP normalization on both sides) | M | ✅ Completed |
| 3 | Management UI (`BudgetsView`/`BudgetsViewModel`, route, sidebar entry) | M | ✅ Completed |
| 4 | Dashboard progress addon (`BudgetProgressItem`, `DashboardViewModel` 7th ctor param, new card) | M | ✅ Completed |

Applied in 4 chained commits (`94dbc3e`, `1daf049`, `7fd44c2`, `4d4cd40`) following `sdd-tasks`'
recommendation — each slice is a complete, independently-mergeable unit, sequenced because slice 2
depends on slice 1's repository, slice 3 on slice 2's use cases, and slice 4 on slice 2's query while
also touching Phase 8's `DashboardViewModel` constructor (the design's flagged ctor-arity blast
radius, which materialized exactly as predicted — 3 call sites repaired). The orchestrator
independently re-verified all 4 core gates after every single slice, not just at the end.

## 4. Phase completion criteria

- Budget CRUD per category per month, upsert-safe at both the use-case layer and a DB unique index. ✅
- `Budget.Create` rejects invalid amount/year/month; invalid budgets unconstructible outside the domain. ✅
- Actual spend reuses `ExpenseRepository.GetCategoryTotalsAsync` unchanged, no new SQL, no in-memory summation. ✅
- Both budget and actual amounts normalized to DOP via `ConvertAsync` (never `DisplayAsync`); missing rate surfaces as indeterminate, never a fabricated 0%/100%. ✅
- `BudgetEvaluator` is a pure domain service with 80%/100% thresholds as named constants, no I/O, no DI. ✅
- Dashboard budget-progress card follows the Dashboard's own month-navigation state, no second selector. ✅
- Global (`CategoryId == null`) budgets rejected at the use-case layer in v1; no rollover; no notifications. ✅
- Month labels use `CultureInfo.CurrentCulture` on both the management screen and the Dashboard. ✅
- Full test suite green, no regressions (861/861: 703 unit + 158 integration). ✅

## 5. Findings carried forward, not fixed in this phase

- **`DebtRepository.UpdateAsync` has the same owned-type (`OwnsOne`) copy gap** that this phase found
  and fixed for `BudgetRepository.UpdateAsync` (D-7). `Debt`'s `OriginalAmount`/`InstallmentAmount`
  amounts are similarly not copied by `CurrentValues.SetValues` alone. Out of Phase 10's scope (the
  spec does not touch Debts); confirmed still present during `sdd-verify`. Tracked as a follow-up for
  a future phase, in the same spirit as Phase 9's `ViewLocator`/`DebtsViewModel` flag (which was
  fixed immediately because it was in scope; this one is deliberately deferred).

## 6. References

- `docs/roadmap.md` §Fase 10 — original plan, now marked complete
- `spec/features/phase-10/F-010-monthly-budgets.done.md` — archived formal spec, evidence per criterion
- `.atl/sdd/f-010p10-monthly-budgets/{exploration,proposal,design,verify-report,archive-report}.md` — full traceability
- `spec/features/phase-09/F-009-calendar-view.done.md` — precedent for the month-navigation/`CultureInfo` convention this phase extends
