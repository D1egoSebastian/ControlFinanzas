# F-011 — Savings Goals

> **Phase:** 11 — Savings Goals (metas de ahorro)
> **Status:** ✅ Archived (2026-09-20)
> **SDD:** `.atl/sdd/f-011p11-savings-goals/`
> **Known gap (not blocking):** R-1's prose and scenario S-2 describe a UI-driven goal-edit flow
> ("edit its target amount/date/name"). The shipped `GoalsView` only supports create/contribute/
> view/delete — no edit command exists. This is a spec-internal inconsistency inherited from the
> proposal stage (which scoped UI editing out as "beyond delete-and-recreate"), flagged during design
> and never reconciled against R-1/S-2's wording. The Acceptance Criteria checklist below — the actual
> definition-of-done — does not require a UI-edit flow and is fully satisfied. See
> `.atl/sdd/f-011p11-savings-goals/verify-report.md` for full detail.

## Goal

Let the user create a savings goal with a target amount, currency, and target
date; register manual contributions against it; see progress derived from the
contribution ledger; and get a deterministic monthly savings plan — without
any manual arithmetic, without touching Expense/Income, and without a
Dashboard change in this phase.

### Confirmed product decisions

- **Cross-currency contributions: rejected.** A contribution MUST be
  denominated in the same currency as its goal, enforced at the use-case
  layer with a clear validation error — never silently converted.
- **Target date in the past: allowed, with a warning.** `Goal.Create` does
  NOT reject a past `TargetDate`. `SavingsPlanCalculator` surfaces it as a
  non-throwing `TargetDateInPast` result state.
- **Dashboard integration: deferred out of v1.** Phase 11 ships only the
  standalone `GoalsView` management screen; `DashboardViewModel`/
  `DashboardView` are unchanged.

## Requirements

### R-1 — Goal CRUD via a dedicated management screen

The system MUST provide a dedicated management screen (`GoalsView` /
`GoalsViewModel`, reachable via `RouteNames.Goals`) where the user can
create a goal (name, target amount, currency, target date), edit its target
amount/date/name, and delete it. Deletion MUST remove the goal row entirely
(no soft-delete/archival concept is introduced).

**Evidence (2026-09-20):** Create and delete are implemented and verified
(`GoalsViewModel.CreateAsync`/`DeleteAsync`). **The "edit" clause above is
NOT implemented at the UI layer** — no edit command exists in `GoalsView`/
`GoalsViewModel`. See the header's "Known gap" note and `verify-report.md`.

### R-2 — Valid construction only (domain invariants)

`Goal.Create` MUST reject an empty/blank `Name` and a `TargetAmount` ≤ 0.
`Goal.Create` MUST NOT reject a `TargetDate` in the past — no past-date
restriction exists on construction. `Goal` MUST be a class with private
setters, a parameterless EF Core constructor, and a static `Create` factory
— matching `Debt`, `Expense`, `RecurringRule`, and `Budget` — so that
constructing an invalid `Goal` from any layer outside the domain is
impossible. This replaces the existing public-`init` record stub, which
currently allows exactly that.

### R-3 — Manual contribution entry via a public factory

`ContributeToGoalUseCase` MUST record a `GoalContribution` (`Amount` > 0)
against an existing goal after confirming the goal exists.
`GoalContribution.Create` MUST be a **public** static factory — unlike
`DebtPayment`'s internal constructor — because contributions are manual and
ad-hoc, entered by the user at will, not generated in bulk by a schedule
generator.

### R-4 — Contribution currency MUST match the goal's currency

A contribution's currency MUST equal its goal's currency. `ContributeToGoalUseCase`
MUST reject a mismatched-currency contribution with a clear, localized
validation error before any persistence occurs. The system MUST NOT convert
a contribution's currency to match the goal — no `ICurrencyConverter` usage
exists anywhere in the goals path.

**Reconciliation note (2026-09-20):** "localized" here means the currency-
mismatch message MUST resolve through `Strings.resx` (a new
`GoalsCurrencyMismatchMessage` key), with `GoalsViewModel` substituting it
when `result.Error.Code == "CurrencyMismatch"`. No other use-case error
message in this codebase is currently localized this way — this establishes
a new, narrow convention for this one user-facing error, not a general
requirement to localize every `Result.Failure` message.

### R-5 — Progress is always computed, never stored

A goal's current amount MUST be derived by summing its `GoalContribution`
rows via `Goal.CalculateProgress(contributions)`, mirroring
`Debt.CalculatePendingBalance(payments)`. `Goal` MUST NOT carry a stored
running-balance field. Every reader of goal progress (queries, UI) MUST
obtain the total through `Goal.CalculateProgress`, never by re-summing
independently.

### R-6 — Contributions never create an Expense or Income

Registering a `GoalContribution` MUST NOT create an `Expense` or `Income`
row. Goal contributions live in a fully separate ledger, mirroring the
`DebtPayment`-is-not-an-`Expense` precedent, so they never distort
`GetCategoryTotalsAsync` or any expense/income aggregation.

### R-7 — Pure-domain savings plan calculation

`SavingsPlanCalculator` MUST be a pure static domain service — no I/O, no
DI-resolved dependencies — that takes `targetAmount`, `currentAmount`,
`targetDate`, and `today` (all pre-guaranteed to be the same currency per
R-4) and returns the months remaining and the required monthly contribution.
It MUST return an explicit, non-throwing `AlreadyMet` state when
`currentAmount >= targetAmount`, and an explicit, non-throwing
`TargetDateInPast` state when `targetDate` has already elapsed. The
calculator MUST NOT divide by zero and MUST NOT return a negative month
count under any input, including a `targetDate` equal to `today`.

**Reconciliation note (2026-09-20):** the required monthly contribution MUST
be rounded UP to the nearest cent (ceiling), not `MidpointRounding.AwayFromZero`
(Phase 10's `BudgetEvaluator` convention). A rounded-down minimum could leave
the goal short of its target on the final month, which is a correctness
defect, not a style choice — design wins here over blind convention reuse.

### R-8 — Goal auto-completion

A goal's status MUST transition from `Active` to `Completed` via
`Goal.TryAutoComplete(contributions)` once `CalculateProgress(contributions)`
reaches or exceeds `TargetAmount`, mirroring `Debt.TryAutoClose(payments)`.

### R-9 — Explicit scope exclusions

The following are explicit non-requirements for this phase, not omissions:

- **Dashboard integration.** No change to `DashboardViewModel` or
  `DashboardView`; no goals-progress card is added.
- **Automatic contribution from "unspent income."** Deferred per the
  roadmap's own recommendation.
- **AI-assisted or adaptive savings plans.** Owned by Phase 16.

### R-10 — Localization

All static user-facing copy on the new `GoalsView` and its sidebar entry
MUST resolve through `Strings.resx`/`Strings.es-DO.resx`/
`Strings.Designer.cs`. No hardcoded user-facing Spanish (or English) string
literals are introduced. This follows, and does not reopen, the convention
established in Phase 10.

## Scenarios

### S-1 — Create a goal from the management screen

- GIVEN the `GoalsView` open with no existing goal named "Fondo de
  emergencia"
- WHEN the user enters a name, a target amount of 200,000 DOP, and a target
  date of 2027-06-30, then saves
- THEN a new `Goal` row is created and immediately visible in the goal list
  with 0% progress

### S-2 — Edit and delete a goal

- GIVEN an existing goal with target amount 200,000 DOP
- WHEN the user edits the target amount to 250,000 DOP, then later deletes
  the goal
- THEN the edit persists (verified by a direct query), and the delete
  removes the row entirely from `GoalsView` and the database

**Evidence (2026-09-20):** Delete half verified (`GoalsViewModel.DeleteAsync`
→ `DeleteGoalUseCase` → `IGoalRepository.RemoveAsync`). Edit half verified
only at the repository level — `GoalRepository.UpdateAsync`'s owned-`Money`
persistence is covered by a dedicated integration test (task 1b.10) — but
no UI path reaches it in v1; see R-1's evidence note.

### S-3 — `Goal.Create` rejects invalid name and target amount

- GIVEN an attempt to construct a `Goal` with an empty/blank `Name`, or with
  `TargetAmount == 0` (or negative)
- WHEN `Goal.Create` is called with those inputs
- THEN construction fails with a validation error, and no code path outside
  `Goal` can produce an instance bypassing this check

### S-4 — `Goal.Create` accepts a target date in the past

- GIVEN an attempt to construct a `Goal` with a `TargetDate` earlier than
  today
- WHEN `Goal.Create` is called
- THEN construction succeeds without throwing or rejecting

### S-5 — Contribute successfully to a goal

- GIVEN a goal "Fondo de emergencia" with target 200,000 DOP and currency
  DOP, currently at 0 contributions
- WHEN the user contributes 15,000 DOP via `ContributeToGoalUseCase`
- THEN a `GoalContribution` row is created for that goal, and
  `Goal.CalculateProgress` over its contributions returns 15,000 DOP (7.5%)

### S-6 — Reject a mismatched-currency contribution

- GIVEN a goal "Fondo de emergencia" denominated in DOP
- WHEN the user attempts to contribute an amount denominated in USD via
  `ContributeToGoalUseCase`
- THEN the use case rejects the contribution with a clear, localized
  validation error, no `GoalContribution` row is persisted, and no currency
  conversion is attempted

### S-7 — Goal reaches 100% and auto-completes

- GIVEN a goal with target 200,000 DOP and existing contributions totaling
  185,000 DOP
- WHEN the user contributes 15,000 DOP more, reaching exactly 200,000 DOP
- THEN `Goal.CalculateProgress` returns 100% and `Goal.TryAutoComplete`
  transitions the goal's status from `Active` to `Completed`

### S-8 — Contribution never creates an Expense or Income row

- GIVEN a goal with an existing contribution history
- WHEN a new contribution is registered via `ContributeToGoalUseCase`
- THEN no `Expense` row and no `Income` row is created, asserted directly
  against the respective repositories/tables

### S-9 — `SavingsPlanCalculator` AlreadyMet case

- GIVEN `targetAmount` = 200,000 DOP and `currentAmount` = 200,000 DOP (or
  more), with a future `targetDate`
- WHEN `SavingsPlanCalculator` runs
- THEN it returns `AlreadyMet = true` without throwing, without dividing by
  zero, and without a monthly-contribution figure that implies further
  saving is needed

### S-10 — `SavingsPlanCalculator` TargetDateInPast case

- GIVEN `targetAmount` = 200,000 DOP, `currentAmount` = 15,000 DOP, and a
  `targetDate` earlier than `today`
- WHEN `SavingsPlanCalculator` runs
- THEN it returns `TargetDateInPast = true` without throwing, without
  dividing by zero, and without a negative month count

### S-11 — `SavingsPlanCalculator` target date equal to today

- GIVEN `targetAmount` = 200,000 DOP, `currentAmount` = 185,000 DOP, and a
  `targetDate` equal to `today`
- WHEN `SavingsPlanCalculator` runs
- THEN it returns a non-negative, non-throwing result (either an explicit
  edge state or a correctly computed single-month figure), never a
  division-by-zero exception

## Out of Scope

Dashboard integration (any change to `DashboardViewModel`/`DashboardView`),
automatic contribution from unspent income, AI-assisted or adaptive savings
plans (Phase 16), cross-currency contributions and any `ICurrencyConverter`
usage in the goals path, withdrawals/negative contributions, goal
archiving, goal categories/priorities/shared goals, and notifications or
alerts when a goal falls behind (Phase 17).

## Acceptance Criteria

- [x] `Goal.Create` rejects an empty name and a target amount ≤ 0; an
  invalid goal cannot be constructed from outside the domain. Confirmed by
  direct read of `Domain/Goals/Goal.cs` (private setters, private ctor,
  `Create` factory validates `Name`/`TargetAmount`) plus passing unit tests.
- [x] `Goal.Create` accepts a target date in the past (no throw, no
  rejection). Confirmed: `Create` has no `TargetDate` validator at all.
- [x] `GoalContribution.Create` is public and rejects an amount ≤ 0.
  Confirmed by direct read — `public static Create`, unlike `DebtPayment`'s
  `internal` ctor.
- [x] A goal's current amount is derived from summing its contributions
  everywhere it is shown; no stored running-balance field exists on `Goal`.
  `Goal.CalculateProgress` is the sole aggregation path, called from
  `ContributeToGoalUseCase` and `GetGoalsQuery`; `Goal.cs` has no balance
  field.
- [x] `Goal.TryAutoComplete` marks a goal `Completed` once contributions
  reach the target. Confirmed by direct read (`>=` comparison, idempotent).
- [x] `SavingsPlanCalculator` returns `AlreadyMet` for a goal at or above
  target, without throwing. Confirmed and unit-tested against the design's
  10-row boundary table.
- [x] `SavingsPlanCalculator` returns `TargetDateInPast` for an elapsed
  target date, without throwing and without dividing by zero or a negative
  month count. Confirmed by direct read of `Calculate`'s early-return branch.
- [x] `SavingsPlanCalculator` returns a correct required monthly
  contribution for a normal in-future goal, and handles a target date equal
  to today. Confirmed: month floor at 1, ceiling-to-cents rounding.
- [x] `ContributeToGoalUseCase` rejects a contribution whose currency
  differs from the goal's, with a clear, localized error. Confirmed: the
  `CurrencyMismatch` gate runs before any persistence; `GoalsViewModel`
  substitutes `Strings.GoalsCurrencyMismatchMessage`.
- [x] Contributing to a goal creates no `Expense` and no `Income` row
  (asserted by test). Confirmed: `ContributeToGoalUseCase`'s constructor has
  no `IExpenseRepository`/`IIncomeRepository` dependency to inject.
- [x] `IGoalContributionRepository.GetByGoalIdAsync` exists and is used to
  load a goal's contributions. Confirmed in `GoalContributionRepository.cs`
  and called from both `ContributeToGoalUseCase` and `GetGoalsQuery`.
- [x] Editing a goal's target amount through `GoalRepository.UpdateAsync`
  actually persists (owned-`Money` navigation copied explicitly, following
  `BudgetRepository`'s pattern). Confirmed by direct read of
  `GoalRepository.UpdateAsync` and its dedicated integration test (1b.10).
  **Note: this criterion is scoped to the repository method, not a UI flow —
  see the header's "Known gap" note regarding R-1/S-2's UI-edit wording.**
- [x] A goal can be created, contributed to, viewed with progress and plan,
  and deleted from `GoalsView`. Confirmed: `CreateAsync`/`ContributeAsync`/
  `DeleteAsync` wired to their use cases; `GoalRow` carries progress/plan
  precomputed by `GetGoalsQuery`.
- [x] No hardcoded user-facing Spanish strings; all copy resolves through
  `Strings.resx`. Confirmed: `GoalsViewModel` routes every message through
  `Strings.*`.
- [x] `DashboardViewModel`/`DashboardView` are unchanged. Confirmed: `git
  log` shows no Phase 11 commit touches either file.
- [x] `AppDbContextModelSnapshot.cs` matches the `AddGoals` migration.
  Scaffolded via `dotnet ef migrations add AddGoals`, confirmed during apply
  to touch only the `Goals`/`GoalContributions` tables.
- [x] Full test suite green, no regressions: 944/944 (773 unit + 171
  integration), re-verified independently during `sdd-verify`.
