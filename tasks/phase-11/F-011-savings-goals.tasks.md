# Tasks — F-011 Savings Goals

## Review Workload Forecast

| Field | Value |
|---|---|
| Estimated changed lines | 1700–2100 total across all slices; slice 1 (Domain+Infra combined) alone would land 800–1000, which is why it is split |
| 400-line budget risk | High overall; Medium per sub-slice once 1 is split into 1a/1b |
| Chained PRs recommended | Yes |
| Suggested split | PR 1a (Domain) → PR 1b (Infrastructure + migration) → PR 2 (Application use cases) → PR 3 (Management UI) |
| Delivery strategy | ask-on-risk |
| Chain strategy | stacked-to-main |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: High

Design's "Recommended Slicing" section proposes 3 slices but explicitly names the escape hatch:
slice 1 rewrites 2 domain aggregates, adds a calculator with a 10-row boundary-table test matrix, 2 EF
configs, 2 repositories (each needing the owned-`Money` `UpdateAsync` fix), a scaffolded migration, and
the snapshot regeneration checklist — combined that is comparable to Phase 10's single highest-risk
slice on its own. This plan takes the escape hatch: **1a = pure Domain** (zero EF Core dependency,
compiles and tests standalone), **1b = Infrastructure** (configs, repositories, migration, snapshot).
No Dashboard slice exists (Q3/R-9 deferral) — Phase 11 modifies zero existing view-model, so there is
no ctor-arity blast radius to repair, unlike Phase 10's slice 4.

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|---|---|---|---|
| 1a | Domain (`Goal`, `GoalContribution`, `GoalStatus`, `SavingsPlanCalculator`) | PR 1a | Base = main. Zero consumers today — safe to merge alone. |
| 1b | Infrastructure + migration | PR 1b | Base = PR 1a's merged main. Depends on 1a's `Goal`/`GoalContribution` shapes. |
| 2 | Application use cases | PR 2 | Base = PR 1b's merged main. Depends on `IGoalRepository`/`IGoalContributionRepository`. |
| 3 | Management UI (`GoalsView`) | PR 3 | Base = PR 2's merged main. Depends on the 4 use cases/queries. |

## 1a. Domain

Zero consumers exist yet — safe to merge in isolation. No EF Core / DI dependency.

- [x] 1a.1 Rewrite `Domain/Goals/Goal.cs`: record → sealed class, private setters, `MaxNameLength`/`MaxNoteLength` constants, private ctor + EF ctor, static `Create(string name, Money targetAmount, DateOnly targetDate, string? note = null)` factory rejecting empty/blank name and `TargetAmount <= 0`, **no** `TargetDate` validator (past dates legal). `Currency` derived from `targetAmount.Currency`. Drop `CreatedAt`. (R-2, S-3, S-4, D-1)
- [x] 1a.2 Create `Domain/Goals/GoalStatus.cs`: `enum GoalStatus { Active = 0, Completed = 1 }`. (D-3)
- [x] 1a.3 Add `GoalProgress` record struct + `Goal.CalculateProgress(IReadOnlyList<GoalContribution>)` to `Goal.cs`: clamps `Remaining` at `Money.Zero` instead of an unguarded `Money.Subtract`; guards mixed-currency contributions up front and throws `ArgumentException` naming the offending contribution; `PercentComplete` uses `BudgetEvaluator`'s rounding and is never clamped. (R-5, D-3)
- [x] 1a.4 Add `Goal.TryAutoComplete(IReadOnlyList<GoalContribution>)` to `Goal.cs`: transitions `Active` → `Completed` once `CalculateProgress` reaches target; returns `false` and is idempotent once already `Completed`. (R-8, S-7, D-3)
- [x] 1a.5 Rewrite `Domain/Goals/GoalContribution.cs`: record → sealed class, private setters, private ctor + EF ctor, **public** static `Create(Guid goalId, Money amount, DateOnly date)` rejecting `Guid.Empty` and `Amount <= 0`. Drop `Source` and `CreatedAt`. (R-3, D-2)
- [x] 1a.6 Create `Domain/Goals/SavingsPlanCalculator.cs`: static pure class, `SavingsPlan` readonly record struct (`MonthsRemaining`, `MonthlyContributionRequired`, `AlreadyMet`, `TargetDateInPast`), `MinMonthsRemaining = 1`, `CeilingToDecimals` private helper (ceiling-to-cents per R-7's reconciliation note — NOT `MidpointRounding.AwayFromZero`), throws `ArgumentOutOfRangeException` on `targetAmount <= 0` / `currentAmount < 0`, floors months at 1 for any live goal, returns `0/0m` for both flag states without dividing. (R-7, D-4)
- [x] 1a.7 Unit tests for `Goal.Create`: rejects empty/whitespace name, name > `MaxNameLength`, `TargetAmount` 0/negative, note > `MaxNoteLength`; accepts a past `TargetDate` without throwing; `Currency` derived from `TargetAmount.Currency`; no public ctor exists outside `Create`. (S-3, S-4)
- [x] 1a.8 Unit tests for `GoalContribution.Create`: callable from the test assembly (asserts public, not internal, unlike `DebtPayment`); rejects amount 0/negative and `Guid.Empty` goal id.
- [x] 1a.9 Unit tests for `Goal.CalculateProgress`: zero contributions, partial, exactly-met, over-contributed (`Remaining = 0`, `Percent > 100`, no throw), mixed-currency throws `ArgumentException` naming the contribution, null list throws. (R-5)
- [x] 1a.10 Unit tests for `Goal.TryAutoComplete`: `false` below target; `true` + `Completed` at exactly target; `true` above target; idempotent `false` on a second call; `false` on an empty list. (S-7)
- [x] 1a.11 Unit tests for `SavingsPlanCalculator`: the full D-4 boundary table (all 10 rows) — already-met, over-funded, past date (`0/0`, no division), date == today, date later this month, both-flags-true, the `100/3 → 33.34` ceiling-pinning row, `targetAmount <= 0` throws. (R-7, S-9, S-10, S-11)

## 1b. Infrastructure + migration

Depends on 1a (`Goal`/`GoalContribution` shapes must exist before configs/repositories compile).

- [x] 1b.1 Modify `Application/Abstractions/IGoalContributionRepository.cs`: add `Task<IReadOnlyList<GoalContribution>> GetByGoalIdAsync(Guid goalId, CancellationToken ct = default)`. (D-9)
- [x] 1b.2 Create `Infrastructure/Persistence/Configurations/GoalConfiguration.cs`: `ToTable("Goals")`, `OwnsOne(TargetAmount)` (`HasPrecision(18,4)`), `Currency`/`Status` `HasConversion<int>()`, index on `Status`. No unique index. (D-9)
- [x] 1b.3 Create `Infrastructure/Persistence/Configurations/GoalContributionConfiguration.cs`: `ToTable("GoalContributions")`, `OwnsOne(Amount)`, FK on `GoalId` with `OnDelete(DeleteBehavior.Cascade)` (load-bearing for 1b.7's delete-wrapper approach), index on `GoalId`, no navigation property to `Goal`. (D-8, D-9)
- [x] 1b.4 Create `Infrastructure/Persistence/Repositories/GoalRepository.cs`: `BudgetRepository` template for `AddAsync`/`RemoveAsync`/`GetByIdAsync`(`AsNoTracking`)/`GetAllAsync`(`OrderBy(TargetDate).ThenBy(Name)`). `UpdateAsync` MUST explicitly copy the owned `TargetAmount` entry after `CurrentValues.SetValues` (`BudgetRepository`'s fixed pattern, never `DebtRepository`'s). This method is load-bearing, not dead code: `ContributeToGoalUseCase`'s `TryAutoComplete` path (slice 2) calls it even though no v1 UI flow edits `TargetAmount` directly. (D-9)
- [x] 1b.5 Create `Infrastructure/Persistence/Repositories/GoalContributionRepository.cs`: `DebtPaymentRepository` template with `GetByGoalIdAsync` (`.Where(GoalId == goalId).OrderBy(Date)`) and the same explicit owned-`Amount` copy in `UpdateAsync`. Do NOT fix `DebtPaymentRepository.UpdateAsync`'s equivalent gap here — out of scope for this phase; flag it in the PR description for a future `fix/owned-money-update` change covering both `DebtRepository` and `DebtPaymentRepository`. (D-9, Open Question a)
- [x] 1b.6 Modify `Infrastructure/Persistence/AppDbContext.cs`: add `DbSet<Goal> Goals` and `DbSet<GoalContribution> GoalContributions`.
- [x] 1b.7 Modify `Infrastructure/Persistence/ServiceCollectionExtensions.cs`: register `AddScoped<IGoalRepository, GoalRepository>()` and `AddScoped<IGoalContributionRepository, GoalContributionRepository>()` in `AddPersistence` — not `Bootstrap.cs`.
- [x] 1b.8 Scaffold migration with `dotnet ef migrations add AddGoals` (do not hand-author). Confirm `Up` contains only `CreateTable("Goals")`, `CreateTable("GoalContributions")`, `CreateIndex("IX_Goals_Status")`, `CreateIndex("IX_GoalContributions_GoalId")`, and touches no other table; confirm `Down` drops `GoalContributions` then `Goals` (child first). Any `Expenses`/`Debts`/`Budgets` DDL means the snapshot was stale — stop and fix it first.
- [x] 1b.9 Confirm `Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs` now contains `Goal` and `GoalContribution` with their owned `Money` and the cascading FK. Do not touch this file from slice 2 or 3. Snapshot verified line-by-line against the migration (see apply-progress). NOTE: `Bootstrap.ApplyMigrations` against a real dev/fresh DB via the running desktop app was NOT exercised in this batch — deferred to the phase-wide `4.2` manual smoke-test task; `context.Database.MigrateAsync()` was exercised end-to-end by every integration test in this batch instead.
- [x] 1b.10 Integration tests for `GoalRepository` (SQLite harness, `DashboardAggregateQueryTests` pattern): `TargetAmount`/`TargetAmountCurrency` round-trip; regression — change a target amount, `UpdateAsync`, re-read from a fresh context, assert the new value persisted; `Status` transition persists.
- [x] 1b.11 Integration tests for `GoalContributionRepository`: `GetByGoalIdAsync` returns only that goal's rows ordered by `Date`; owned `Amount` round-trips; deleting a `Goal` cascades its `GoalContribution` rows away; the same owned-`Money` `UpdateAsync` regression.

## 2. Application use cases

Depends on 1b (`IGoalRepository`/`IGoalContributionRepository`/`GoalRepository`/`GoalContributionRepository`).

- [x] 2.1 Create `Application/UseCases/Goals/CreateGoal/CreateGoalCommand.cs`: `record(string Name, decimal TargetAmount, Currency Currency, DateOnly TargetDate, string? Note)`. (D-5)
- [x] 2.2 Create `Application/UseCases/Goals/CreateGoal/CreateGoalValidator.cs`: `Name` `NotEmpty().MaximumLength(Goal.MaxNameLength)`, `TargetAmount` `GreaterThan(0m)`, `Note` `MaximumLength(Goal.MaxNoteLength)`, **no** `TargetDate` rule. (D-5)
- [x] 2.3 Create `Application/UseCases/Goals/CreateGoal/CreateGoalUseCase.cs`: validate → `Money.Create` + `Goal.Create` + `AddAsync` inside `try/catch (ArgumentException)` → `Result.Failure("DomainInvariant", ex.Message)` on catch → `Result.Success(goal)`. (D-5)
- [x] 2.4 Create `Application/UseCases/Goals/ContributeToGoal/ContributeToGoalCommand.cs`: `record(Guid GoalId, decimal Amount, Currency Currency, DateOnly Date)`. (D-6)
- [x] 2.5 Create `Application/UseCases/Goals/ContributeToGoal/ContributeToGoalValidator.cs`: `GoalId` `NotEqual(Guid.Empty)`, `Amount` `GreaterThan(0m)`. (D-6)
- [x] 2.6 Create `Application/UseCases/Goals/ContributeToGoal/ContributeToGoalUseCase.cs`: ctor takes ONLY `IGoalRepository`, `IGoalContributionRepository`, validator, logger — no `IExpenseRepository`/`IIncomeRepository`/`ICurrencyConverter` (R-6, R-4). Ordered body: validate → load goal (`GoalNotFound` if absent) → **currency gate** `command.Currency != goal.Currency` → `Result.Failure("CurrencyMismatch", ...)` BEFORE any `Money`/`GoalContribution` construction → `Money.Create` + `GoalContribution.Create` + `AddAsync` inside `try/catch (ArgumentException)` → reload contributions + `Goal.TryAutoComplete` + `IGoalRepository.UpdateAsync` only if it transitions → `Result.Success(contribution)`. (R-4, R-6, D-6)
- [x] 2.7 Create `Application/UseCases/Goals/GetGoals/GoalPlanItem.cs`: `record(Guid GoalId, string Name, Currency Currency, decimal TargetAmount, decimal ContributedAmount, decimal RemainingAmount, decimal PercentComplete, DateOnly TargetDate, GoalStatus Status, int MonthsRemaining, decimal MonthlyContributionRequired, bool AlreadyMet, bool TargetDateInPast)`. (D-7)
- [x] 2.8 Create `Application/UseCases/Goals/GetGoals/GetGoalsQuery.cs`: ctor `IGoalRepository`, `IGoalContributionRepository`, `IClock`. Reads both repositories directly (no composition into another query, per D-7's `GetBudgetVsActualQuery` precedent). Empty repo → empty list, no contribution round-trip. Per goal: `GetByGoalIdAsync` → `Goal.CalculateProgress` (the only sum) → `SavingsPlanCalculator.Calculate` (the only plan) → project into `GoalPlanItem`. Order by `Status`, then `TargetDate`, then `Name` (ordinal). Returns a bare `IReadOnlyList<GoalPlanItem>`, no wrapper record. (R-5, D-7)
- [x] 2.9 Create `Application/UseCases/Goals/DeleteGoal/DeleteGoalUseCase.cs`: thin wrapper over `IGoalRepository.RemoveAsync(Guid goalId, CancellationToken)`. (R-1, D-8)
- [x] 2.10 Modify `UI/Composition/Bootstrap.cs`: register `AddTransient<CreateGoalUseCase>()`, `AddTransient<ContributeToGoalUseCase>()`, `AddTransient<GetGoalsQuery>()`, `AddTransient<DeleteGoalUseCase>()`, `AddTransient<IValidator<CreateGoalCommand>, CreateGoalValidator>()`, `AddTransient<IValidator<ContributeToGoalCommand>, ContributeToGoalValidator>()`. (D-11)
- [x] 2.11 Unit tests for `CreateGoalUseCase`: valid command → `AddAsync` once + `Result.Success`; empty name / amount 0 → `Validation` with details dictionary; a past `TargetDate` succeeds (no validator rule, S-4 at the use-case layer); a `Goal.Create` throw surfaces as `DomainInvariant`.
- [x] 2.12 Unit tests for `ContributeToGoalUseCase`: currency mismatch → `Result.Failure("CurrencyMismatch", ...)` and `AddAsync` is NEVER called (order assertion); unknown goal id → `GoalNotFound`; matching currency → contribution added; reaching target → `TryAutoComplete` fires + `UpdateAsync` called; not reaching it → `UpdateAsync` NOT called; assert the constructor has no `IExpenseRepository`/`IIncomeRepository`, and no `Expense`/`Income` row is created. (R-4, R-6, S-5, S-6, S-8)
- [x] 2.13 Unit tests for `GetGoalsQuery`: fake `IClock`; empty repo → empty list, no contribution round-trip; progress matches `Goal.CalculateProgress` for the same inputs; plan matches `SavingsPlanCalculator.Calculate` for the same inputs; `GetByGoalIdAsync` called once per goal; deterministic ordering (Active before Completed, then target date, then ordinal name tie).
- [x] 2.14 Unit tests for `DeleteGoalUseCase`: delegates to `RemoveAsync`; unknown id propagates `NotFound`.

## 3. Management UI

Depends on 2 (the 4 use cases/queries).

- [ ] 3.1 Modify `UI/Navigation/RouteNames.cs`: add `public const string Goals = "Goals";` — literal value matters for `ShellViewModel`'s sidebar-highlight match. (D-11)
- [ ] 3.2 Modify `UI/Navigation/NavigationService.cs`: add `{ RouteNames.Goals, typeof(GoalsViewModel) }` to the ctor `_routes` dictionary.
- [ ] 3.3 Modify `UI/ViewModels/ShellViewModel.cs`: add the `Goals` `RouteMenuItem` using `_localizer["SidebarGoals"]` — not a hardcoded string.
- [ ] 3.4 Create `UI/ViewModels/GoalRow.cs`: row projection with `ProgressValue` (clamped at 100 for the bar), `IsCompleted`, and the three mutually-exclusive presentation-precedence flags `ShowAlreadyMetMessage`/`ShowPastDateWarning`/`ShowPlan` resolved here, not in XAML (`AlreadyMet` and `TargetDateInPast` can both be true). (D-10)
- [ ] 3.5 Create `UI/ViewModels/GoalsViewModel.cs`: `BudgetsViewModel` template. Ctor: `GetGoalsQuery`, `CreateGoalUseCase`, `ContributeToGoalUseCase`, `DeleteGoalUseCase`, `IClock`, logger; fire-and-forget `LoadAsync`. Create-form fields + contribution sub-flow fields (`SelectedGoal`, `ContributionAmount`, `ContributionDate`) visible only when a goal row is selected. `ContributionCurrency` is read-only, derived from `SelectedGoal.Currency` — never a user picker (Q1 enforcement stays reachable only via a non-UI caller). `ContributeAsync` builds the command from `SelectedGoal.Currency`. On a `CurrencyMismatch`-coded failure, substitute `Strings.GoalsCurrencyMismatchMessage` into `FormError` instead of the raw use-case message (R-4 reconciliation note). `Select`/`CreateAsync`/`ContributeAsync`/`DeleteAsync` `[RelayCommand]`s, `FormError`/`StatusMessage`/`IsLoading` chrome identical to `BudgetsViewModel`. (R-1, R-4, D-10)
- [ ] 3.6 Create `UI/Views/GoalsView.axaml` (+ `.axaml.cs`): two-column `Grid`, `x:Name="Root"` for the delete-command binding pattern. Left: create form + contribution panel (`IsVisible` bound to `SelectedGoal != null`, read-only currency `TextBlock`). Right: `ItemsControl` over `Goals` with `ProgressBar` (`Value` = `ProgressValue`), contributed/target amounts, the three mutually-exclusive message `TextBlock`s, Select/Delete buttons, empty-state message. All static copy from `Strings.resx` — no hardcoded literals. (R-1, R-10, D-10)
- [ ] 3.7 Modify `UI/Views/ViewLocator.cs`: add `GoalsViewModel` in BOTH `Build` and `Match` — the omission Phase 9 caught for `DebtsViewModel`.
- [ ] 3.8 Modify `UI/Composition/Bootstrap.cs`: add `AddTransient<GoalsViewModel>()`.
- [ ] 3.9 Modify `UI/Resources/Strings.resx`, `Strings.es-DO.resx`, `Strings.Designer.cs`: add the ~26 `GoalsView`/sidebar keys from design D-12, plus `GoalsCurrencyMismatchMessage` (new key per spec R-4's reconciliation note — not in D-12's original table). Manual `Strings.Designer.cs` accessors — the build does not auto-regenerate it. (R-4, R-10, D-12)
- [ ] 3.10 Unit tests for `GoalsViewModel`: load populates `Goals` with progress and plan; create calls the use case with form values and resets the form on success; failure lands in `FormError` without resetting the form; selecting a row exposes `ContributionCurrency` from that goal; `ContributeAsync` sends the goal's currency, never a user-picked one; a `CurrencyMismatch` failure surfaces the localized `Strings.GoalsCurrencyMismatchMessage`, not the raw use-case message; `GoalRow.ProgressValue` clamps at 100 while `PercentComplete` stays > 100; the three `Show*` flags are mutually exclusive including when both underlying flags are true. (R-4, S-1, S-2)

## 4. Cierre

- [ ] 4.1 Run build, `dotnet test` (unit + integration), `dotnet format style --verify-no-changes`, and `ArchitectureTests`/NetArchTest. All green, no regressions.
- [ ] 4.2 Run the app (`dotnet run`) against an existing dev DB and a fresh DB; visually confirm `GoalsView` create/contribute/edit/delete, progress bar, plan message, already-met message, and past-date warning all render correctly.
- [ ] 4.3 Update `docs/architecture.md` with a Phase 11 entry, following the pattern used for Phases 9/10.
- [ ] 4.4 Create `spec/phases/phase-11/features.md` following the pattern of `spec/phases/phase-09/` and `phase-10/`.
- [ ] 4.5 Archive the spec (`F-011-savings-goals.spec.md` → `.done.md`) and write `verify-report.md`/`archive-report.md` in `.atl/sdd/f-011p11-savings-goals/`, re-verifying gates independently before archiving.
- [ ] 4.6 In the PR description for slice 1b, note the flagged (not fixed) `DebtPaymentRepository.UpdateAsync` owned-`Money` gap as a follow-up candidate for a future `fix/owned-money-update` change.
