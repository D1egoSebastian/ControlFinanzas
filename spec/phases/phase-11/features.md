# Phase 11 — Savings Goals (metas de ahorro)

> **Phase:** 11 — Savings Goals
> **Status:** ✅ Completed (2026-09-20) — see `spec/features/phase-11/F-011-savings-goals.done.md`
> **Spec ID:** F-011
> **Dependencies:** Phase 4 ✅ (data), Phase 10 ✅ (`Budget`/`BudgetRepository`'s owned-`Money`
> `UpdateAsync` pattern, reused verbatim for `Goal`/`GoalContribution`)
> **Roadmap:** `docs/roadmap.md` §Fase 11
> **Stack:** .NET 8 · Avalonia 11.2.7 (MVVM strict) · EF Core 8.0.11 + SQLite ·
> xUnit + FluentAssertions

## 1. Summary

Phase 11 lets the user create a savings goal with a target amount, currency, and target date, register
manual contributions against it, see progress derived from the contribution ledger, and get a
deterministic monthly savings plan. Confirmed product decisions: cross-currency contributions are
rejected outright (no silent conversion); a target date in the past is allowed, surfaced as a warning
via `SavingsPlanCalculator`'s `TargetDateInPast` flag; Dashboard integration is deferred out of v1 —
`GoalsView` is a standalone management screen.

## 2. Model

```
Domain/Goals/
  Goal                        — rewritten stub → invariant-carrying class, static Create factory
  GoalStatus                  — enum { Active, Completed }
  GoalContribution             — rewritten stub → invariant-carrying class, PUBLIC static Create
                                  (unlike DebtPayment's internal ctor — contributions are manual/ad-hoc)
  SavingsPlanCalculator        — static pure domain service, ceiling-rounded monthly contribution

Application/UseCases/Goals/
  CreateGoal/                  — command + validator + use case
  ContributeToGoal/            — currency-match gate (Q1/R-4), auto-complete on target reached
  GetGoals/                    — reads both repositories directly, delegates all arithmetic to the domain
  DeleteGoal/                  — thin RemoveAsync wrapper (FK cascades contributions)

Infrastructure/Persistence/
  GoalConfiguration             — OwnsOne(TargetAmount), index on Status, no unique index
  GoalContributionConfiguration — OwnsOne(Amount), cascading FK on GoalId
  GoalRepository/
  GoalContributionRepository    — BudgetRepository/DebtPaymentRepository templates + explicit
                                   owned-Money copy in UpdateAsync

UI/ViewModels/
  GoalsViewModel/GoalRow        — management screen (BudgetsViewModel template); contribution entry
                                   is an inline panel revealed on row selection, not a modal/second route
```

## 3. Slice plan

| ID | Slice | Estimate | Status |
| --- | --- | --- | --- |
| 1a | Domain (`Goal`, `GoalContribution`, `GoalStatus`, `SavingsPlanCalculator`) | M | ✅ Completed |
| 1b | Infrastructure + migration (configs, repositories, `AddGoals` migration) | L | ✅ Completed |
| 2 | Application use cases (create/contribute/query/delete) | M | ✅ Completed |
| 3 | Management UI (`GoalsView`/`GoalsViewModel`, route, sidebar entry) | M | ✅ Completed |

Applied in 4 chained commits (`10dc303` domain, `5870967` infrastructure+migration, `98b634f` use
cases, `f3d1400` management UI) per `sdd-tasks`' chained-PR recommendation, taking the escape hatch of
splitting slice 1 into 1a (pure domain, zero EF Core dependency) and 1b (infrastructure) because
combined they were comparable to Phase 10's single highest-risk slice. No Dashboard slice exists
(Q3/R-9 deferral) — Phase 11 modifies zero existing view-model, so unlike Phase 10's slice 4 there was
no ctor-arity blast radius to repair. The orchestrator independently re-verified all 4 core gates after
every single slice, not just at the end.

## 4. Phase completion criteria

- Goal create/contribute/view/delete from a dedicated `GoalsView` management screen. ✅ (UI-driven
  *editing* is not implemented — see §5 below; not a phase-completion criterion per the spec's own
  Acceptance Criteria checklist)
- `Goal.Create` rejects invalid name/target amount; accepts a past target date without rejection. ✅
- `GoalContribution.Create` is a public factory (contrast with `DebtPayment`'s internal ctor); rejects
  amount ≤ 0. ✅
- Progress is always derived via `Goal.CalculateProgress`, never stored as a running balance. ✅
- `ContributeToGoalUseCase` rejects a currency-mismatched contribution before any persistence, with a
  localized error; never resolves `ICurrencyConverter`. ✅
- Contributions never create an `Expense`/`Income` row (constructor has no such dependency to inject). ✅
- `SavingsPlanCalculator` is a pure domain service: `AlreadyMet`/`TargetDateInPast` non-throwing states,
  ceiling-rounded monthly contribution, never divides by zero, never returns a negative month count. ✅
- `Goal.TryAutoComplete` transitions `Active` → `Completed` once contributions reach the target. ✅
- All static UI copy resolves through `Strings.resx`, including the new `GoalsCurrencyMismatchMessage`
  key. ✅
- Dashboard is unchanged (Q3/R-9 — confirmed via `git log` on `DashboardViewModel.cs`). ✅
- Full test suite green, no regressions (944/944: 773 unit + 171 integration). ✅

## 5. Findings carried forward, not fixed in this phase

- **`DebtPaymentRepository.UpdateAsync` has the same owned-type (`OwnsOne`) copy gap** that Phase 10
  found and fixed for `BudgetRepository.UpdateAsync`, and that this phase correctly avoided in both
  `GoalRepository.UpdateAsync` and `GoalContributionRepository.UpdateAsync`. New finding from this
  phase's design (not previously named) — `DebtRepository.UpdateAsync`'s twin gap was already tracked
  at Phase 10's close; the two are now explicitly paired as a single future `fix/owned-money-update`
  follow-up covering both `DebtRepository` and `DebtPaymentRepository`.
- **Spec-internal inconsistency: R-1/S-2's UI-edit wording vs. the Acceptance Criteria checklist.**
  R-1's prose ("edit its target amount/date/name") and scenario S-2 describe a UI-driven goal-edit
  flow that was never implemented — `GoalsView`/`GoalsViewModel` supports create/contribute/view/delete
  only. This gap was independently flagged during design (the proposal stage had scoped UI editing out
  as "beyond delete-and-recreate") but never reconciled against R-1/S-2's literal text, unlike the
  design's other 7 spec-reconciliation points which were explicitly resolved. The spec's own
  Acceptance Criteria checklist — the actual definition-of-done — does not require UI editing and is
  fully satisfied (the one editing-related criterion is scoped to `GoalRepository.UpdateAsync`
  persisting correctly, which is tested and confirmed). Not blocking; flagged for a future phase to
  either add a goal-edit UI flow or formally narrow R-1/S-2's wording.

## 6. References

- `docs/roadmap.md` §Fase 11 — original plan, now marked complete
- `spec/features/phase-11/F-011-savings-goals.done.md` — archived formal spec, evidence per criterion,
  including the flagged R-1/S-2 gap
- `.atl/sdd/f-011p11-savings-goals/{exploration,proposal,design,verify-report,archive-report}.md` —
  full traceability
- `spec/features/phase-10/F-010-monthly-budgets.done.md` — precedent for the owned-`Money`
  `UpdateAsync` pattern this phase's repositories reuse
