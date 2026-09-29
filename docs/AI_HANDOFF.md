# AI_HANDOFF.md — ControlDeFinanzasIA

> Self-contained handoff document for any AI agent picking up the project tomorrow.
> Verified against the filesystem on **2026-09-28**. All claims are sourced from
> `AGENTS.md`, `spec/`, `docs/`, or the repo's actual state.

---

## 1. TL;DR

- **What it is:** `ControlDeFinanzasIA` — a cross-platform (.NET 8 + Avalonia MVVM) desktop app for personal-finance control with AI assist. 100% local data. DOP + USD.
- **Current phase:** **Phase 17 archived** (in-app notifications). Phases 0–17 fully archived. Next per the roadmap: Phase 18 (PDF/Excel export) — check `.atl/sdd/f-018p18-export/` and `git log --oneline` for any in-flight work before starting.
- **Test count:** 1700/1700 passing (1405 unit + 295 integration) as of 2026-09-28, Phase 17 close (up from 1522 at Phase 16 close). Build 0 warnings/0 errors, `dotnet format style --verify-no-changes` exit 0, NetArchTest 11/11, `ModelSnapshotConsistencyTests` 3/3. The one-off `DashboardViewModelTests` anomaly recorded at Phase 16 close (gotcha #26) did not recur in this close's gate run.
- **Phase 17 shipped in-app notifications: a bell + unread badge + popup in a new header row of the shell, fed by a deterministic detection run.** `DetectNotificationsUseCase` reads four existing sources sequentially (debt installments in `[today-7, today+3]` with overdue derived at display time, recurring charges in `[today, today+3]`, current-month budget Near/Exceeded — highest tier only —, unacknowledged `AnomalyAlert` rows) plus a periodic digest (`Off`/`Daily`/`Weekly`, default Weekly), and hands candidates to `NotificationReconciler`, which persists them in a `Notification` table keyed by a single unique `DedupKey` string (idempotent; a dismissed key is never recreated; an undismissed row whose source stops qualifying is removed; facts on undismissed rows are refreshed without touching `Id`/`CreatedAt`/`ReadAt`; 60-day purge; one `SaveChangesAsync` per run). `ScopedNotificationCenter` gives every run its own DI scope, guards against concurrent runs (manual refresh is a no-op while one is in flight; startup/settings runs are queued), and swallows and logs every failure. Runs happen at app start (`mainWindow.Opened`), after saving the Settings "Notificaciones" section (4 per-kind toggles, default on, plus the digest frequency picker, via new `SettingKeys`/`ISettingsStore` — `AppSettings` untouched), and via the popup's refresh button. The badge shows the full unread count (`9+` above 9), the popup lists at most 20 rows unread-first and marks them read on open, per-row dismiss is permanent, and there is no click-through. `Notification` stores facts only; `NotificationTextFormatter` renders text at display time from resx. See gotchas #28-#30 below.
- **Phase 16 shipped a "Pedir plan con IA" button on every goal row that asks the LLM for a short, motivational explanation of the exact deterministic figure `SavingsPlanCalculator` (Phase 11, unmodified) already computes.** A new `GenerateSavingsPlanUseCase` takes only `GoalId`, reloads the goal and its contributions server-side, recomputes via `Goal.CalculateProgress` + `SavingsPlanCalculator.Calculate`, and classifies the plan into exactly one state (`Live`/`AlreadyMet`/`TargetDateInPast`, `AlreadyMet` wins when both apply) so the prompt only ever receives that state's figures. A pure `SavingsPlanNarrationGuard` extracts every digit run from the model's cleaned output and drops the narration to `Unavailable` if any run isn't among the figures — plus the sanitized goal name — actually sent, closing the loop against invented numbers. `GoalRow` converted from an immutable record to an `ObservableObject` (keeping its exact 13-parameter PascalCase constructor via a scoped `IDE1006` suppression) so a per-row `IsGeneratingPlan` flag plus `[RelayCommand(AllowConcurrentExecutions = true)]` let one row's request run without disabling any other row's button. Not configured, a provider failure, or a guard rejection all leave the card's number untouched, showing only a localized fallback caption. See gotcha #26 below for the transient Dashboard test-run anomaly and gotcha #27 for the guard's documented membership-not-position limitation.
- **Phase 15 shipped a chat screen that answers plain-language finance questions grounded in local data, with every figure computed deterministically before the model ever sees it.** `BuildChatContextQuery` composes 6 already-existing queries (month summary, category breakdown — fetched UNFOLDED so no category is ever hidden past the dashboard's top-5 default —, budget vs. actual, goals, debts + schedule, upcoming payments), each section degrading independently on failure. `ChatContextRenderer`, a pure static class, formats that snapshot into a culture-correct, name-sanitized prompt block capped at 6,000 characters by construction, dropping whole sections in a fixed order when needed. `AskFinanceChatUseCase` copies `CategorizeExpenseUseCase`'s guard skeleton and is success-shaped for every outcome (rejected, not configured, any AI failure — never `Result.Failure` or an uncaught exception). The conversation lives in memory only, cleared via a brand-new opt-in `INavigationAware` hook because `NavigationService.GoBack` reactivates the same `ChatViewModel` instance instead of resolving a new one. See gotcha #24 below for the `AsyncRelayCommand` CanExecute-not-self-checked footgun found in slice 5 and gotcha #25 for the `InternalsVisibleTo` test-only seam introduced in slice 4.
- **Phase 14 shipped three AI-facing dashboard sections while keeping every number deterministic**: a month-end forecast, persisted anomaly alerts, and an AI-written insight summary. Two new pure Domain services — `AnomalyDetector` (robust median/MAD z-score, `MAD=0` ratio fallback, never divides by zero) and `MonthEndForecaster` (run-rate/history blend with explicit `InsufficientData`/`ClosedMonth`/`FutureMonth` states) — compute every figure with **zero** `ILLMClient` dependency. The LLM only narrates figures it is handed, via two success-shaped use cases (`GetInsightsUseCase`, `GetAnomalyNarrativeUseCase`) that copy Phase 13's guard skeleton but never fail — not-configured, offline, and every AI error all resolve to a fallback payload plus a correlation-id Warning log, never an exception or `Result.Failure`. One new composite query (`GetDashboardInsightsQuery`) splits a deterministic step (forecast + reconciled anomalies, sequential, zero LLM) from a non-blocking narration step (insights + one batched anomaly-narration call, `WhenAll`, at most 2 concurrent calls) — the dashboard's numeric sections render immediately regardless of AI state. See gotcha #23 below for the `AnomalySeverity`-as-string persistence deviation and gotcha #22 for Phase 13's flaky-test lesson this phase's own timing-sensitive tests followed.
- **Phase 13 shipped a real AI feature end to end**: typing an expense description (4+ chars, 600ms debounce) suggests an existing category via a dismissable chip; accepting the chip is the *only* code path in the entire feature that writes `SelectedCategoryId` from AI output. Every failure mode (no key, no network, bad key, timeout, unparseable response, no matching category) leaves the form fully usable with an honest `AiStatusMessage`, never an exception or a dialog. `ILLMClient` + two provider adapters (`OpenAICompatibleClient` for OpenAI/OpenRouter/Custom, `AnthropicClient`) + `Microsoft.Extensions.Http.Resilience`-backed pipeline (15s total timeout, 1 retry, circuit breaker) ship in `Application`/`Infrastructure`, with zero `System.Net.*` leak into `Application`/`UI` enforced by a reflection test stricter than the existing NetArchTest rules. See gotcha #22 below for a real flaky-test root cause found and fixed during this phase.
- **The app was actually launched and screenshotted for the first time this project** at Phase 9 close — confirmed real rendering (LiveCharts2, all 6 sidebar routes) and caught two real runtime-only bugs no test suite could catch: `ViewLocator` missing the `DebtsViewModel` case (Debts route rendered blank since Phase 7) and the entire "Libro Mayor" stylesheet never merged into `App.axaml` (every `ledger-*` class had been inert since Phase 4). Both fixed — see gotchas below. Phase 10 continued the same discipline: `dotnet run` mid-slice-4 confirmed the new Dashboard "Progreso de presupuestos" card renders correctly, including its empty-state message. Phase 11 confirmed the same mid-slice-3: the Dashboard renders with the new "Metas" sidebar entry, no startup crash. Phase 13's slices 6 and 7 both smoke-launched the app with no DI startup failure — the first phase to register `AddHttpClient` calls anywhere in the solution.
- **Phase 12's smoke test found and fixed a significant cross-phase regression, unrelated to Settings itself:** `MainWindow.axaml`'s `ContentControl` resolves navigation content through `CurrentViewConverter.cs`, NOT `ViewLocator.cs`. `CurrentViewConverter` was never updated past Phase 6 (`RecurringRulesViewModel`) — every route added since (Debts, Calendar, Budgets, Goals, and Settings itself) rendered a **solid black, completely empty content pane** in the real app, despite 1033 passing tests and a `ViewLocator` that was diligently kept current every phase. No exception was ever thrown, so it went undetected across 5 phases. Fixed in commit `4f8e423` by making `CurrentViewConverter` delegate to `ViewLocator` instead of keeping a second parallel map. See gotcha #21 below.
- **Phase 11 shipped with one documented, non-blocking spec gap:** the spec's R-1 prose and scenario S-2 describe a UI-driven goal-edit flow that was never built — `GoalsView` only supports create/contribute/view/delete. This is a spec-internal inconsistency inherited from the proposal stage (which scoped UI editing out), independently flagged during design but never reconciled against R-1/S-2's literal wording. The spec's own Acceptance Criteria checklist — the actual definition-of-done — does not require UI editing and is fully satisfied. See `spec/features/phase-11/F-011-savings-goals.done.md` and `.atl/sdd/f-011p11-savings-goals/verify-report.md`.
- **Key constraint:** Filesystem-based SDD specs in `spec/features/phase-NN/` (NOT OpenSpec). `.spec.md` = active, `.done.md` = archived. Do not create `openspec/`.
- **Architecture:** Clean Architecture (Domain / Application / Infrastructure / UI). NetArchTest rules R-1..R-5 enforced in `Tests.Unit`.

---

## 2. Project at a glance

| Field | Value |
| --- | --- |
| Name | `ControlDeFinanzasIA` |
| Stack | C# on **.NET 8 (LTS)**, **Avalonia 11.2.7** UI, **EF Core 8.0.11 + SQLite** (Code First), `Microsoft.Extensions.DependencyInjection`, **xUnit + FluentAssertions 11.10.0** |
| Solution | `ControlDeFinanzasIA.sln` at repo root |
| Type | Cross-platform desktop (Windows, macOS, Linux), single-user, local data only |
| UI pattern | MVVM strict with **CommunityToolkit.Mvvm** (`[ObservableProperty]`, `[RelayCommand]`) |
| Localization | `es-DO` default + `en` via `IStringLocalizer` / resx |
| Status | Phases 0–17 ✅ archived; Phase 18 (PDF/Excel export) next |

---

## 3. Current state — phase-by-phase

Counts derived by globbing `spec/features/phase-NN/*.md` and splitting `.done.md` vs `.spec.md`.

| # | Phase | Total features | Done (`.done.md`) | Active (`.spec.md`) | % complete | Phase plan |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Bootstrap | (predates feature spec convention) | — | — | 100% | `docs/roadmap.md` §Fase 0 |
| 1 | Architecture & contracts | 8 | 8 | 0 | 100% | `spec/phases/phase-01/features.md` |
| 2 | Persistence (EF Core + SQLite) | 6 | 6 | 0 | 100% | `spec/phases/phase-02/features.md` |
| 3 | Slice vertical: register/list expenses | 7 | 7 | 0 | 100% | `spec/phases/phase-03/features.md` |
| 4 | Income + edit/delete + tags + filters + saved views | 8 | 8 | 0 | 100% | `spec/phases/phase-04/features.md` |
| 5 | Payment method + multi-moneda DOP/USD | 3 (`F-501..F-503`; plan says 13 sub-features 5-001..5-013 grouped) | 3 | 0 | 100% | `spec/phases/phase-05/features.md` |
| 6 | Recurring rules (subscriptions + fixed expenses) | 1 umbrella `F-001` covering 8 sub-features (6-001..6-008) | 1 | 0 | 100% | `spec/phases/phase-06/features.md` + `spec/features/phase-06/F-001-recurring-rules.done.md` |
| 7 | Debts with installment plan | 1 umbrella `F-001` covering 8 slices (7-001..7-008) | 1 | 0 | 100% | `spec/phases/phase-07/features.md` + `spec/features/phase-07/F-001-debts-installment-plan.done.md` |
| 8 | Dashboard + period views/visualizations | 1 umbrella `F-008` covering 3 slices (aggregation/UI/upcoming-payments) | 1 | 0 | 100% | `spec/phases/phase-08/features.md` + `spec/features/phase-08/F-008-dashboard-visualizations.done.md` |
| 9 | Calendar view + parameterized navigation | 1 umbrella `F-009` covering 3 slices (data/navigation/UI) | 1 | 0 | 100% | `spec/phases/phase-09/features.md` + `spec/features/phase-09/F-009-calendar-view.done.md` |
| 10 | Monthly budgets per category | 1 umbrella `F-010` covering 4 slices (domain+infra/use cases/management UI/dashboard addon) | 1 | 0 | 100% | `spec/phases/phase-10/features.md` + `spec/features/phase-10/F-010-monthly-budgets.done.md` |
| 11 | Savings goals with manual contributions | 1 umbrella `F-011` covering 4 slices (domain/infra+migration/use cases/management UI) | 1 | 0 | 100%* | `spec/phases/phase-11/features.md` + `spec/features/phase-11/F-011-savings-goals.done.md` |
| 12 | Settings: theme, language, AI config (persistence only) | 1 umbrella `F-012` covering 5 slices (domain+infra+migration/use cases/startup culture read/settings UI 4a/settings UI 4b) | 1 | 0 | 100% | `spec/phases/phase-12/features.md` + `spec/features/phase-12/F-012-settings-i18n-ai-config.done.md` |
| 13 | AI pipeline + automatic expense categorization | 1 umbrella `F-013` covering 8 work units (contracts/OpenAI adapter+prompts/Anthropic adapter/factory+DI+resilience/parser/categorization use case/expense-entry UI/Probar conexión) | 1 | 0 | 100% | `spec/phases/phase-13/features.md` + `spec/features/phase-13/F-013-ai-pipeline-categorization.done.md` |
| 14 | AI insights, month-end forecast, anomaly detection | 1 umbrella `F-014` covering 10 work units (domain math/alert entity+persistence/forecast use case/detection+acknowledge/AI plumbing/AI use cases/composite+DI/VM deterministic pass/VM narration pass/view) | 1 | 0 | 100% | `spec/phases/phase-14/features.md` + `spec/features/phase-14/F-014-ai-insights-forecast-anomalies.done.md` |
| 15 | Chat con IA sobre tus finanzas | 1 umbrella `F-015` covering 6 strictly-chained slices (context builder/renderer/use case+prompts/navigation hook+clipboard port/view model/view+routing) | 1 | 0 | 100% | `spec/phases/phase-15/features.md` + `spec/features/phase-15/F-015-ai-finance-chat.done.md` |
| 16 | Plan de ahorro con IA para metas | 1 umbrella `F-016` covering 4 strictly-chained slices (guard+DTO+prompts/use case/row+ViewModel/view+docs) | 1 | 0 | 100% | `spec/phases/phase-16/features.md` + `spec/features/phase-16/F-016-ai-savings-plan.done.md` |
| 17 | Notificaciones in-app | 1 umbrella `F-017` covering 9 strictly-chained slices (domain/persistence+migration/settings+reconciler/debt+recurring detection/budget+anomaly detection+read/dismiss/UI service+list VM/shell bell+startup trigger/settings section/digest) | 1 | 0 | 100% | `spec/phases/phase-17/features.md` + `spec/features/phase-17/F-017-notifications.done.md` |

\* Phase 11's Acceptance Criteria checklist is 100% satisfied; one documented, non-blocking gap exists
between the spec's R-1/S-2 prose (UI goal editing) and the shipped UI (create/contribute/view/delete
only) — see the TL;DR above.

### Per-phase detail

**Phase 1** — `spec/features/phase-01/` (8 done)
- `F-001..F-008`: solution skeleton, Money VO, DateRange VO, IClock/IPathResolver abstractions, Result/PagedResult, repository contracts, NetArchTest, composition root with DI host.

**Phase 2** — `spec/features/phase-02/` (6 done)
- `F-001..F-006`: AppDbContext, EF Core configurations + initial migration, seed data (9 categories + 5 payment methods), `CategoryRepository` + `PaymentMethodRepository`, composition root persistence wiring, integration tests with SQLite in-memory.

**Phase 3** — `spec/features/phase-03/` (7 done)
- `F-001..F-007`: Expense entity, configuration + migration, repository with `GetByFilterAsync`, `RegisterExpenseUseCase` + DTOs, `GetTransactionsQuery`, shell + navigation infrastructure, `ExpensesViewModel` + `ExpensesView` (Phase 3 capstone).

**Phase 4** — `spec/features/phase-04/` (8 done)
- `F-001..F-008`: Income entity, repository, register use case, Tag entity + M:N, update/delete use cases, SavedView with JSON versionado, filter extension with TagIds, `IncomeView` + filter inputs + saved views UI (Phase 4 capstone "Libro Mayor").

**Phase 5** — `spec/features/phase-05/` (3 done)
- `F-501-exchange-rate-stub`, `F-502-currency-converter`, `F-503-payment-method-id-entities`. Plan-level IDs 5-001..5-013 grouped under these three specs. Payment method + multi-moneda wired end-to-end. `559/559 tests verde` at close (2026-09-08).

**Phase 6** — `spec/features/phase-06/` (1 done, archived 2026-09-17)
- `F-001-recurring-rules.done.md` (umbrella). All 8 slices (6-001..6-008) complete:
  calculator, `RecurringRule`/`Subscription`/`FixedExpense`, repository + migration,
  full CRUD for both rule types, `GenerateDueRecurringUseCase` with idempotent
  preview/confirm, unified `RecurringRulesView` UI including the "Generar del mes" section.

**Phase 7** — `spec/features/phase-07/` (1 done, archived 2026-09-18)
- `F-001-debts-installment-plan.done.md` (umbrella). All 8 slices (7-001..7-008) complete:
  `Debt`/`DebtPayment` aggregate + `DebtScheduleGenerator`, repository + migration,
  `CreateDebtWithScheduleUseCase`/`RegisterDebtPaymentUseCase` (auto-close on last
  installment)/`SkipDebtPaymentUseCase`/queries, standalone `DebtsView` UI.

**Phase 13** — `spec/features/phase-13/` (1 done, archived 2026-09-23)
- `F-013-ai-pipeline-categorization.done.md` (umbrella). All 8 work units complete: `ILLMClient`
  contracts (zero HTTP surface), `OpenAICompatibleClient` + `AnthropicClient` adapters,
  `LLMClientFactory` + resilience pipeline, tolerant JSON parser, `CategorizeExpenseUseCase`,
  expense-entry suggestion chip, `TestAiConnectionUseCase` + "Probar conexión" button.

**Phase 14** — `spec/features/phase-14/` (1 done, archived 2026-09-23)
- `F-014-ai-insights-forecast-anomalies.done.md` (umbrella). All 10 work units complete:
  `AnomalyDetector`/`MonthEndForecaster` pure Domain services, hardened `AnomalyAlert` +
  idempotent reconciliation, `GetMonthEndForecastUseCase`/`DetectAnomaliesUseCase`/
  `AcknowledgeAnomalyUseCase` (zero LLM), `GetInsightsUseCase`/`GetAnomalyNarrativeUseCase`
  (success-shaped for every AI failure), `AnomalyNarrationParser` (tolerant batch parsing),
  `GetDashboardInsightsQuery` composite, non-blocking `DashboardViewModel` narration pass, three
  new `DashboardView.axaml` sections.

**Phase 15** — `spec/features/phase-15/` (1 done, archived 2026-09-23)
- `F-015-ai-finance-chat.done.md` (umbrella). All 6 strictly-chained slices complete:
  `BuildChatContextQuery` composite over 6 existing queries (unfolded category fetch, per-section
  degrade), pure static `ChatContextRenderer` (culture-correct, ≤6,000 chars by construction, fixed
  section-trim order), `AskFinanceChatUseCase` (success-shaped for every outcome, zero HTTP when not
  configured), grounding-rule prompt pair, new `INavigationAware` hook + `IClipboardService` port,
  `ChatViewModel`/`ChatView` with a full routing checklist (sidebar, `RouteNames`, both `ViewLocator`
  arms).

**Phase 16** — `spec/features/phase-16/` (1 done, archived 2026-09-26)
- `F-016-ai-savings-plan.done.md` (umbrella). All 4 strictly-chained slices complete:
  `GenerateSavingsPlanUseCase` (server-side recompute from `GoalId` only, never trusts the VM,
  `GoalNotFound` checked before the settings read), `SavingsPlanNarrationGuard` (pure static digit-run
  membership check, goal name included in the allowed set, leading zeros normalized, non-ASCII digits
  rejected, whitespace-aware 600-char cap), `GoalRow` converted from a record to an `ObservableObject`
  with a scoped `IDE1006` suppression preserving its exact constructor, per-row
  `RequestPlanCommand`/`IsGeneratingPlan` with `AllowConcurrentExecutions = true`, and the
  `GoalsView.axaml` button + narration/fallback captions.

**Phase 17** — `spec/features/phase-17/` (1 done, archived 2026-09-28)
- `F-017-notifications.done.md` (umbrella). All 9 strictly-chained slices complete: `Notification`
  aggregate + keys (S1a), persistence + additive migration `AddNotifications` (S1b), per-kind settings
  keys + `NotificationReconciler` (S2), debt/recurring detection (S3), budget/anomaly detection +
  mark-read/dismiss (S4), `ScopedNotificationCenter` + list VM + formatter (S5a), shell bell/badge/popup +
  startup trigger (S5b), Settings "Notificaciones" section (S6), periodic digest (S7).

---

## 4. Phase 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 & 17 — archived (2026-09-17, 2026-09-18, 2026-09-18, 2026-09-18, 2026-09-20, 2026-09-20, 2026-09-21, 2026-09-23, 2026-09-23, 2026-09-23, 2026-09-26, 2026-09-28)

### What's done (backend)

| Layer | Files |
| --- | --- |
| Domain | `src/ControlDeFinanzasIA.Domain/Recurring/RecurringRule.cs`, `Subscription.cs`, `FixedExpense.cs`, `RecurringFrequency.cs`, `RecurringOccurrenceCalculator.cs` |
| Application (abstractions + use cases) | `Application/Abstractions/IRecurringRuleRepository.cs`; `Application/UseCases/Recurring/{CreateSubscription, UpdateSubscription, DeleteRecurringRule, DeactivateRecurringRule, CreateFixedExpense, UpdateFixedExpense, DeleteFixedExpense, DeactivateFixedExpense, GenerateDueRecurring, SkipNextOccurrence}/*.cs` |
| Infrastructure | `Infrastructure/Persistence/Configurations/RecurringRuleConfiguration.cs` (TPH); `Infrastructure/Persistence/Repositories/RecurringRuleRepository.cs`; migration `20260910205248_AddRecurringRules` |

### What's done (UI)

- `src/ControlDeFinanzasIA.UI/Views/RecurringRulesView.axaml(.cs)`
- `src/ControlDeFinanzasIA.UI/ViewModels/RecurringRulesViewModel.cs` — pulls in all recurring use cases; rule type toggle between `Subscription` and `FixedExpense`; create + update demo wired.

### Closing tasks — all done (per `tasks/phase-06/F-001-recurring-rules.tasks.md`)

All 12 items (1.1–4.5) are checked off. Summary of what closed them:

- Schema/round-trip/provenance tests (`ExpenseSchemaTests`, `RecurringRuleRepositoryTests`).
- FixedExpense CRUD + tests (`FixedExpenseUseCaseTests`, `RecurringLifecycleUseCaseTests`).
- Skip + edit-generated-movement tests (`GeneratedExpenseTests`).
- `GenerateDueRecurringUseCase` preview/idempotent-confirm tests (`GenerateDueRecurringUseCaseTests`).
- 16 calculator date scenarios (`RecurringOccurrenceCalculatorTests`), exceeds the ≥10 spec minimum.
- Unified UI for Subscription + FixedExpense with "Generar del mes" preview/confirm section
  (`RecurringRulesView.axaml` + `RecurringRulesViewModel`).
- Gates: build clean, 614/614 tests, `dotnet format style` clean, NetArchTest 11/11.
- Spec archived to `spec/features/phase-06/F-001-recurring-rules.done.md`; see
  `.atl/sdd/f-006p6-formalize-recurrentes/verify-report.md` and `archive-report.md`.

The full task breakdown is at `tasks/phase-06/F-001-recurring-rules.tasks.md`.

### Phase 7 — what's done

- Domain: `src/ControlDeFinanzasIA.Domain/Debts/{Debt,DebtPayment,DebtScheduleGenerator,DebtFrequency,DebtStatus,DebtPaymentStatus}.cs`
- Application: `Application/Abstractions/{IDebtRepository,IDebtPaymentRepository}.cs`; `Application/UseCases/Debts/{CreateDebtWithSchedule,RegisterDebtPayment,SkipDebtPayment,GetDebts,GetDebtSchedule}/*.cs`
- Infrastructure: `Infrastructure/Persistence/Configurations/{DebtConfiguration,DebtPaymentConfiguration}.cs`; `Infrastructure/Persistence/Repositories/{DebtRepository,DebtPaymentRepository}.cs`; migration `20260918025303_AddDebts`
- UI: `src/ControlDeFinanzasIA.UI/Views/DebtsView.axaml(.cs)` + `ViewModels/DebtsViewModel.cs`, own screen (not merged into "Generar del mes"), registered under `RouteNames.Debts`
- Gates: build clean, 698/698 tests, `dotnet format style` clean, NetArchTest 11/11 — re-verified independently before archive, see `.atl/sdd/f-007p7-debts-installment-plan/verify-report.md` and `archive-report.md`.
- Spec archived to `spec/features/phase-07/F-001-debts-installment-plan.done.md`.

The full task breakdown is at `tasks/phase-07/F-001-debts-installment-plan.tasks.md`.

### Phase 8 — what's done

- Application: `Application/DTOs/Aggregates/{CurrencyTotal,CategoryCurrencyTotal,MonthCurrencyTotal}.cs`;
  `Application/Common/{MonthPeriod,DashboardDefaults}.cs`;
  `Application/UseCases/Dashboard/{GetMonthSummary,GetCategoryBreakdown,GetMonthlyEvolution,GetUpcomingPayments}/*.cs`
- Infrastructure: `IExpenseRepository`/`IIncomeRepository`/`IDebtPaymentRepository` extended with
  SQL `GROUP BY`/`SUM` aggregate methods (EF Core 8's SQLite provider doesn't support
  `Sum(decimal)` directly — cast through `double`, round back to 4 decimals after materialization)
- UI: `src/ControlDeFinanzasIA.UI/Views/DashboardView.axaml(.cs)` + `ViewModels/DashboardViewModel.cs`
  rewritten from placeholder; `Views/Charts/{CategoryBreakdownChart,MonthlyEvolutionChart}.axaml(.cs)`
  wrapping LiveChartsCore.SkiaSharpView.Avalonia 2.0.5
- Applied in 3 separate commits (`b53102f` aggregation, `3fc5325` UI+charts, `5aee8c9` upcoming
  payments) per `sdd-tasks`' chained-PR recommendation — isolates the untested LiveCharts2
  cross-OS rendering risk from the already-tested, lower-risk aggregation layer.
- Gates: build clean, 751/751 tests (Debug — Release unit-test host blocked on this machine by
  a Windows Application Control policy, not a code defect; Release build and Integration tests
  are unaffected), `dotnet format style` clean, NetArchTest 11/11 — re-verified independently
  before archive, see `.atl/sdd/f-008p8-dashboard-visualizations/verify-report.md` and
  `archive-report.md`.
- Spec archived to `spec/features/phase-08/F-008-dashboard-visualizations.done.md`.
- Two real bugs caught before/during apply, not just at review: the proposal's plan to reuse
  `ICurrencyConverter.DisplayAsync` for DOP normalization was wrong (it converts to the "other"
  currency, not always DOP) — caught in the design phase, before code was written; and the
  `Sum(decimal)`/SQLite workaround initially had no rounding step, risking float noise in money
  figures — caught and fixed during slice 1 review, with regression tests added.

The full task breakdown is at `tasks/phase-08/F-008-dashboard-visualizations.tasks.md`.

### Phase 9 — what's done

- Application: `Application/DTOs/Aggregates/DayCurrencyTotal.cs`; `Application/Common/CalendarGrid.cs`
  (pure 42-cell month-grid math); `Application/UseCases/Calendar/GetCalendarMonth/*.cs`
- Infrastructure: `IExpenseRepository.GetDailyTotalsAsync` (same SQLite decimal-Sum workaround as
  Phase 8's other aggregates)
- UI: `Navigation/{INavigationService,NavigationService}.cs` extended with an additive typed
  overload (`NavigateTo<TViewModel>(routeName, configure)`) for drill-down — first time this
  project modified a shared interface instead of only adding files; `ExpensesViewModel.ApplyDayFilter`
  + a refresh generation guard (fixes a real pre-existing Apply/Clear race, made load-bearing by
  drill-down); `ViewModels/CalendarViewModel.cs` + `Views/CalendarView.axaml(.cs)` +
  `Views/Calendar/MonthGrid.axaml(.cs)` (one shared `ItemTemplate`, not 42 hand-written cells)
- Applied in 3 separate commits (`9eab856` data layer, `6ffe1b2` navigation, `313133b` calendar UI)
  per `sdd-tasks`' chained-PR recommendation — isolating the shared-interface risk.
- **Two real, previously-unreachable runtime bugs found and fixed** by actually launching the app
  (`dotnet run` + screenshot) instead of trusting a green test suite:
  - `fix(ui): register DebtsViewModel in ViewLocator` (`e0e51d3`) — the Debts route (shipped
    Phase 7) rendered blank; `ViewLocator.Build`/`Match` never had a case for it.
  - `fix(ui): wire up the Libro Mayor design system, fix its CornerRadius bug` (`4d981df`) —
    `LedgerStyles.axaml` (204 lines, since Phase 4) was never merged into `App.axaml`'s
    `Application.Styles`; every `ledger-*` class referenced across the app had zero effect.
    Merging it surfaced a second bug: `RadiusSmall`/`RadiusMedium` were `sys:Double` but consumed
    as `CornerRadius`, throwing `InvalidCastException` at startup — fixed by declaring them as
    `CornerRadius` resources. `Motion.axaml` (row-reveal animations) is deliberately still NOT
    merged — it has its own real bug (`RenderTransform` animation with no registered animator);
    tracked as follow-up, not blocking.
- Gates: build clean, 795/795 tests (647 unit + 148 integration, up from 751) — Release unit
  tests ran cleanly this time (the Windows Application Control block from Phase 8's close
  resolved itself), format clean, NetArchTest 11/11.
- Spec archived to `spec/features/phase-09/F-009-calendar-view.done.md`.

The full task breakdown is at `tasks/phase-09/F-009-calendar-view.tasks.md`.

### Phase 10 — what's done

- Domain: `src/ControlDeFinanzasIA.Domain/Budgets/{Budget,BudgetStatus,BudgetEvaluator}.cs` —
  `Budget` rewritten from an unwired `sealed record` stub into an invariant-carrying `sealed class`
  (private setters, static `Create` factory); `BudgetEvaluator` is a static pure domain service with
  `NearThresholdPercent = 80m`/`ExceededThresholdPercent = 100m` as named constants.
- Application: `Application/UseCases/Budgets/{CreateOrUpdateBudget,DeleteBudget,GetBudgetsForMonth,GetBudgetVsActual}/*.cs`
  — `GetBudgetVsActualQuery` normalizes both the budget amount and the actual spend to DOP via
  `ICurrencyConverter.ConvertAsync` (never `DisplayAsync`), and surfaces a missing/failed conversion
  rate as an indeterminate line instead of a fabricated 0%/100%.
- Infrastructure: `Infrastructure/Persistence/{Configurations/BudgetConfiguration,Repositories/BudgetRepository}.cs`;
  unique index on `(Year, Month, CategoryId)`; migration `AddBudgets`.
- UI: `Views/BudgetsView.axaml(.cs)` + `ViewModels/{BudgetsViewModel,BudgetRow}.cs` (management
  screen, own route `RouteNames.Budgets`); `ViewModels/BudgetProgressItem.cs` + a new
  "Progreso de presupuestos" card in `DashboardView.axaml` (`DashboardViewModel` gained a 7th
  constructor parameter for `GetBudgetVsActualQuery`).
- Applied in 4 separate commits (`94dbc3e` domain+infra+migration, `1daf049` use cases, `7fd44c2`
  management UI, `4d4cd40` Dashboard addon) per `sdd-tasks`' chained-PR recommendation — the
  orchestrator independently re-verified all 4 gates after every single slice, not just at the end.
- **A real bug found and fixed during this phase**: `BudgetRepository.UpdateAsync`, if copied
  verbatim from the `DebtRepository` template, would silently discard every budget-amount edit.
  `_db.Entry(existing).CurrentValues.SetValues(budget)` only copies scalar properties of the entity
  itself — an EF Core `OwnsOne` navigation (`Budget.Amount`) is a separate tracked entry and is
  **not** covered by `SetValues`. Fixed by explicitly copying the owned entry afterward; guarded by a
  dedicated integration test (set 15000 → update to 9000 → re-read from a fresh context). **This
  same gap still exists, unfixed, in `DebtRepository.UpdateAsync`** — flagged during Phase 10's
  design and confirmed still present during `sdd-verify`; tracked as a follow-up for a future phase
  since the Phase 10 spec doesn't touch Debts.
- Gates: build clean, 861/861 tests (703 unit + 158 integration, up from 795 at Phase 9 close),
  `dotnet format style` clean, NetArchTest 11/11 — re-verified independently before archive, see
  `.atl/sdd/f-010p10-monthly-budgets/verify-report.md` and `archive-report.md`.
- Spec archived to `spec/features/phase-10/F-010-monthly-budgets.done.md`.

The full task breakdown is at `tasks/phase-10/F-010-monthly-budgets.tasks.md`.

### Phase 11 — what's done

- Domain: `src/ControlDeFinanzasIA.Domain/Goals/{Goal,GoalStatus,GoalContribution,SavingsPlanCalculator}.cs`
  — `Goal`/`GoalContribution` rewritten from unwired `sealed record` stubs into invariant-carrying
  `sealed class`es (private setters, static `Create` factories); `GoalContribution.Create` is
  **public** (contrast with `DebtPayment`'s `internal` ctor — contributions are manual/ad-hoc, not
  generated in bulk). `SavingsPlanCalculator` is a static pure domain service that rounds the required
  monthly contribution **up** to the cent (`Math.Ceiling`, not `MidpointRounding.AwayFromZero`) because
  it prescribes a minimum, not a reported fact — deliberately different from `BudgetEvaluator`'s
  rounding.
- Application: `Application/UseCases/Goals/{CreateGoal,ContributeToGoal,GetGoals,DeleteGoal}/*.cs` —
  `ContributeToGoalUseCase`'s constructor takes only `IGoalRepository`/`IGoalContributionRepository`
  (no `IExpenseRepository`/`IIncomeRepository`/`ICurrencyConverter`), so a goal contribution can never
  create an `Expense`/`Income` row or silently convert currency; the currency-match gate runs before
  any persistence. `GetGoalsQuery` reads both repositories directly and delegates all arithmetic to
  `Goal.CalculateProgress`/`SavingsPlanCalculator` — no independent summation.
- Infrastructure: `Infrastructure/Persistence/{Configurations/{GoalConfiguration,GoalContributionConfiguration},Repositories/{GoalRepository,GoalContributionRepository}}.cs`;
  cascading FK from `GoalContributions.GoalId` to `Goals`; migration `AddGoals`. Both `UpdateAsync`
  methods correctly copy the owned `Money` navigation explicitly (the `BudgetRepository`-fixed
  pattern, not `DebtRepository`'s unfixed one).
- UI: `Views/GoalsView.axaml(.cs)` + `ViewModels/{GoalsViewModel,GoalRow}.cs` (management screen, own
  route `RouteNames.Goals`, sidebar entry "Metas"). Contribution entry is an inline panel revealed when
  a goal row is selected — no modal infrastructure exists in this codebase, and no second navigable
  route was added.
- Applied in 4 separate commits (`10dc303` domain, `5870967` infrastructure+migration, `98b634f` use
  cases, `f3d1400` management UI) per `sdd-tasks`' chained-PR recommendation — the orchestrator
  independently re-verified all 4 gates after every single slice, not just at the end.
- **No new bug class found** (unlike Phases 8/9/10) — the owned-`Money` `UpdateAsync` trap was already
  known from Phase 10 and correctly applied preemptively to both new repositories here.
- **One documented, non-blocking spec gap**: R-1's prose and scenario S-2 describe a UI-driven
  goal-edit flow that was never implemented — `GoalsView`/`GoalsViewModel` supports create/contribute/
  view/delete only, no `EditAsync`/`UpdateGoalUseCase`. This is a spec-internal inconsistency inherited
  from the proposal stage (which had scoped UI editing out as "beyond delete-and-recreate"),
  independently flagged during design but never reconciled against R-1/S-2's literal wording. The
  spec's own Acceptance Criteria checklist does not require UI editing (the one editing-related
  criterion is scoped to `GoalRepository.UpdateAsync` persisting correctly, which is tested) and is
  fully satisfied. Flagged as a candidate for a future phase, not a Phase 11 regression.
- Gates: build clean, 944/944 tests (773 unit + 171 integration, up from 861 at Phase 10 close),
  `dotnet format style` clean, NetArchTest 11/11 — re-verified independently before archive, see
  `.atl/sdd/f-011p11-savings-goals/verify-report.md` and `archive-report.md`.
- Spec archived to `spec/features/phase-11/F-011-savings-goals.done.md`.

The full task breakdown is at `tasks/phase-11/F-011-savings-goals.tasks.md`.

### Phase 12 — what's done

- Domain: `src/ControlDeFinanzasIA.Domain/Settings/Setting.cs` — tightened in place (guarded `Key`
  init accessor: non-empty, trimmed, `MaxKeyLength=128`, `MaxValueLength=2048`). Stays a `sealed
  record`, no factory — unlike `Goal`/`Budget`'s stub rewrites, `Setting` had zero consumers before
  this phase, so hardening it in place was safe.
- Application: `Application/Settings/{AppSettings,SettingKeys,SupportedLocales}.cs`;
  `Application/Abstractions/ISettingsStore.cs`; `Application/UseCases/Settings/{GetAppSettings,
  UpdateTheme,UpdateLocale,UpdateAiSettings}/*.cs`. `UpdateAiSettingsUseCase` validates all 6 AI
  fields before writing any of them (validate-all-then-write-all); its constructor has no
  `HttpClient`/`IHttpClientFactory`, verified by a reflection-assertion unit test that also confirms
  no `*LLMClient*` type exists anywhere in the solution yet.
- Infrastructure: `Infrastructure/Persistence/Configurations/SettingConfiguration.cs` (unique index on
  `Key`); `Infrastructure/Persistence/Repositories/SettingRepository.cs`;
  `Infrastructure/Settings/SettingsStore.cs` (never throws/returns null — degrades to
  `AppSettings.Defaults` + `LogWarning`; every numeric field serialized/parsed with
  `CultureInfo.InvariantCulture`; enums by name, not ordinal); migration `AddSettings`.
- UI: `UI/App.axaml.cs`'s `ApplyPersistedUiCulture` replaces the hardcoded
  `new CultureInfo("es-DO")` with a scoped `ISettingsStore` read at startup, falling back to
  `SupportedLocales.Default` on an empty table or `CultureNotFoundException`.
  `Views/SettingsView.axaml(.cs)` + `ViewModels/SettingsViewModel.cs` (own route
  `RouteNames.Settings`, sidebar entry "Configuración") — four sections (Apariencia, Idioma, IA,
  Acerca de), one save command per section, zero Avalonia-specific type in the ViewModel constructor
  (testable without a headless harness). The IA section's cleartext-API-key warning has no
  `IsVisible` binding — always rendered, per the spec's explicit "never conditional" requirement.
- Applied in 5 separate commits (`3fc1a52` domain+infra+migration, `34c890e` use cases, `9ef0922`
  startup culture read, `8c86405` settings UI 4a, `f2e08a8` settings UI 4b) per `sdd-tasks`'
  chained-PR recommendation.
- **A significant regression found and fixed, unrelated to Settings itself**: see the TL;DR's
  `CurrentViewConverter` finding and gotcha #21 below. Fixed in commit `4f8e423`, landed separately
  from this phase's own 5 commits.
- **Two documented, non-blocking gaps**: the persisted-`en`-locale and corrupt-locale-fallback launch
  scenarios (tasks 3.3/3.4) remain manual-only — no headless Avalonia harness exists in this repo yet;
  `UpdateAiSettingsUseCase`'s six writes are non-transactional (accepted — all validation runs before
  the first write).
- Gates: build clean, 1033/1033 tests (854 unit + 179 integration, up from 944 at Phase 11 close),
  `dotnet format style` clean, NetArchTest 11/11 — re-verified independently before archive, see
  `.atl/sdd/f-012p12-settings-i18n/verify-report.md` and `archive-report.md`.
- Spec archived to `spec/features/phase-12/F-012-settings-i18n-ai-config.done.md`.

The full task breakdown is at `tasks/phase-12/F-012-settings-i18n-ai-config.tasks.md`.

### Phase 13 — what's done

- Application: `Application/Abstractions/Llm/{ILLMClient,ILLMClientFactory,LlmMessage,LlmRequest,
  LlmResponse,LlmException,LlmConnectionOutcome,LlmClientResolution,IPromptProvider}.cs` — zero
  `System.Net.*` references, verified by `LlmContractPurityTests` (a reflection assertion stricter than
  the existing NetArchTest rule, anchored to concrete types so it fails to *compile* rather than pass
  vacuously before the types exist). `Application/Llm/{LlmCategoryAnswerParser,CategoryNameNormalizer,
  PromptNames}.cs` — the tolerant 4-step parser and the 2-tier (exact, then diacritic-folded) category
  matcher, both pure functions with no `ICategoryRepository`/HTTP awareness.
  `Application/UseCases/Expenses/CategorizeExpense/CategorizeExpenseUseCase.cs` — deliberately **not
  sealed**, `ExecuteAsync` **virtual** (the one exception to this codebase's "every use case is sealed"
  convention), so slice 6's `FakeCategorizeExpenseUseCase` can override it for `ExpensesViewModel`'s
  race/cancellation tests. `Application/UseCases/Settings/TestAiConnection/TestAiConnectionUseCase.cs`
  — all four diagnosed outcomes return `Result.Success`, never `Result.Failure`.
- Infrastructure: `Infrastructure/Llm/{OpenAICompatibleClient,AnthropicClient,LLMClientFactory,
  LlmServiceCollectionExtensions}.cs` + `Infrastructure/Llm/Prompts/{EmbeddedPromptProvider,*.txt}`.
  `AddLlmClients` registers 8 named `HttpClient`s (`llm-{provider}` + `llm-{provider}-diagnostic` × 4
  providers) via `Microsoft.Extensions.Http.Resilience` — the first package reference to
  `Microsoft.Extensions.Http*` anywhere in the solution, scoped to `Infrastructure.csproj` only. Both
  adapters map Polly's `BrokenCircuitException` to `LlmFailureKind.CircuitOpen` so an open breaker never
  escapes to the UI as an unmapped exception type.
- UI: `ExpensesViewModel` gained an **optional 17th constructor parameter**
  (`CategorizeExpenseUseCase? categorizeExpenseUseCase = null`) as a kill switch — constructing the
  ViewModel without it produces zero AI behavior, no exception. A generation-token +
  `Interlocked`-exchanged `CancellationTokenSource` pair implements supersede-and-discard: a newer
  keystroke or a manual category pick always wins over a slower in-flight response.
  `SettingsViewModel` gained `TestConnectionCommand`, never writing `FormError` for a connection-test
  result (a failed test is information, not a form validation error).
- Applied in 9 chained commits (`389382a` contracts, `54f8743` OpenAI adapter+prompts, `d40b536`
  Anthropic adapter, `6e8fa63` factory+DI+resilience, `fedf209` tolerant parser, `fc6b5de`
  categorization use case, `71c213a` suggestion chip, `3c044f8` Probar conexión, `aa340dd` debounce
  test fix) per `sdd-tasks`' stacked-to-main chained-PR recommendation — an 8-unit chain, the largest
  yet in this project, justified because slices 1–5 carried zero user-visible behavior change and could
  sit on `main` indefinitely with no risk.
- **A real flaky test found and fixed within the phase itself**: see gotcha #22 below.
- Gates: build clean, 1184/1184 tests (936 unit + 248 integration, up from 1033 at Phase 12 close),
  re-verified independently 3 consecutive times with zero flakiness, `dotnet format style` clean,
  NetArchTest 11/11 — see `.atl/sdd/f-013p13-ai-pipeline/verify-report.md` and `archive-report.md`.
- Spec archived to `spec/features/phase-13/F-013-ai-pipeline-categorization.done.md`.

The full task breakdown is at `tasks/phase-13/F-013-ai-pipeline-categorization.tasks.md`.

### Phase 14 — what's done

- Domain: `src/ControlDeFinanzasIA.Domain/Alerts/{AnomalyDetector,AnomalyAlert,AnomalySeverity}.cs`
  (+ `AnomalySample`/`AnomalyCandidate`/`AnomalyFinding`/`AnomalyRule`) — `AnomalyDetector` is a pure
  static service (robust median/MAD z-score, `0.6745·(x−median)/MAD`, eligibility gate `count>=8 &&
  distinctMonths>=3`, `MAD=0` ratio fallback that never divides by zero, one-sided, threshold on the
  *rounded* score); `AnomalyAlert` rewritten from an unwired record stub into an invariant-carrying
  `sealed class` (private setters, `Create` factory, `Refresh`/`Acknowledge`, no FK to `Expense`).
  `src/ControlDeFinanzasIA.Domain/Forecasting/MonthEndForecaster.cs` — run-rate/trailing-3-month blend
  with explicit `Projected`/`InsufficientData`/`ClosedMonth`/`FutureMonth` states.
- Application: `Application/UseCases/Dashboard/{GetMonthEndForecast,DetectAnomalies,AcknowledgeAnomaly,
  GetInsights,GetAnomalyNarrative,GetDashboardInsights}/*.cs` — the first three have **zero**
  `ILLMClient` dependency; `GetInsightsUseCase`/`GetAnomalyNarrativeUseCase` copy
  `CategorizeExpenseUseCase`'s guard skeleton line for line with one deliberate difference — every
  non-happy path (not configured, `LlmException`, timeout, unparseable reply) is `Result.Success` with
  a fallback payload, never `Result.Failure`. `Application/Llm/AnomalyNarrationParser.cs` — an
  independent tolerant batch parser (deliberately duplicated from `LlmCategoryAnswerParser`, not
  shared — Phase 13's parser is out of scope per R-9). `Application/UseCases/Dashboard/Common/
  DashboardNarrationCache.cs` — session-only, only `Generated` results cached.
- Infrastructure: `Infrastructure/Persistence/{Configurations/AnomalyAlertConfiguration,
  Repositories/AnomalyAlertRepository}.cs` — `Severity` persisted as `TEXT` (deliberate deviation
  from the codebase's int-enum convention, documented inline — this table is Phase 17's contract);
  `ApplyReconciliationAsync` applies add/update/remove in one `SaveChangesAsync`, and on
  `DbUpdateException` detaches only the entries it attached (never `ChangeTracker.Clear()`, which
  would poison the shared process-wide `AppDbContext`). Migration `20260923052536_AddAnomalyAlerts`
  + a new permanent `ModelSnapshotConsistencyTests` (`IMigrationsModelDiffer` drift guard — EF Core 8
  has no `HasPendingModelChanges`, so this reimplements the comparison directly; the 5th consecutive
  phase with snapshot-drift risk, now caught automatically instead of by hand). 8 new embedded prompt
  resources under `Infrastructure/Llm/Prompts/`.
- UI: `UI/Formatting/ResxAnomalyMessageFormatter.cs` (implements the Application-layer
  `IAnomalyMessageFormatter` port over `Strings.resx` — resolves the tension between "no hardcoded
  UI strings" and "the persisted alert message is a deterministic template"). `DashboardViewModel`
  gained exactly one new 8th constructor parameter; the deterministic apply runs **after** the
  existing `WhenAll` block, in its own `try/catch`; narration loads via a separate, non-blocking
  `LoadNarrationAsync` guarded by a generation token + its own `CancellationTokenSource`, mirroring
  `LoadUpcomingPaymentsAsync`'s fire-and-forget shape. `UI/ViewModels/AnomalyListItem.cs` (new).
  `DashboardView.axaml` gained three sections (forecast, anomalies with severity chip + dismiss,
  insights card).
- Applied in 10 chained commits (`a4f6002` domain math, `cb3867d` alert entity+persistence,
  `eef654e` forecast use case, `b827a10` detection+acknowledge, `cc8d123` AI plumbing, `7819575` AI
  use cases, `325cae4` composite+DI, `fd6c891` VM deterministic pass, `1ae4b3b` VM narration pass,
  `497b9f1` view) — the largest chain yet in this project. S2, S7, S8a, and S8c each carried a
  mandatory app smoke-launch before their gate-run closed (DI/startup risk for S2/S7, ctor-arity
  breaking change for S8a, live render surface for S8c). Each slice was independently re-verified by
  the orchestrator after landing.
- **No fix commit required** — the `sdd-verify` adversarial reliability+risk review pass over the
  full phase diff found no confirmed defects: no exception escapes to the UI, the shared
  `AppDbContext` is never poisoned by a failed reconcile, the deterministic and narration passes
  never race on the same context, cancellation is correctly distinguished from a genuine 15s timeout,
  and no secret or raw prompt/response content reaches the logs.
- Gates: build clean, 1344/1344 tests (1079 unit + 265 integration, up from 1184 at Phase 13 close),
  re-verified independently 3 consecutive times with zero flakiness, `dotnet format style` clean,
  NetArchTest 11/11 — see `.atl/sdd/f-014p14-ai-insights/verify-report.md` and `archive-report.md`.
- Spec archived to `spec/features/phase-14/F-014-ai-insights-forecast-anomalies.done.md`.

The full task breakdown is at `tasks/phase-14/F-014-ai-insights-forecast-anomalies.tasks.md`.

### Phase 15 — what's done

- Application: `Application/UseCases/Chat/ChatContextLimits.cs` (30/10/15/10/10/10 item caps,
  `MaxContextChars = 6_000`); `Application/UseCases/Chat/BuildChatContext/{ChatContextSnapshot,
  BuildChatContextQuery,ChatContextRenderer,ChatContextLabels}.cs` — `BuildChatContextQuery` composes
  6 already-existing queries + `GetDebtScheduleQuery` SEQUENTIALLY (never `WhenAll`, same D-10
  reasoning as Phase 14's `GetDashboardInsightsQuery`), each of its 7 sections has its own try/catch
  degrading to `null` without aborting the others; the category breakdown is fetched with
  `topCount = int.MaxValue` (never `DashboardDefaults.TopCategories`) and folded to 30/10 named lines
  + a counted overflow line by the builder itself, so no category is ever silently hidden.
  `ChatContextRenderer` is a pure static class (`AnomalyNarrationParser` precedent, no DI
  registration), culture-formats every figure (never a hand-typed separator), sanitizes names to
  close a prompt-injection vector (`#`/`!` section/flag markers), and stays ≤6,000 chars by
  construction via a fixed section-drop loop.
- Application: `Application/UseCases/Chat/AskFinanceChat/{AskFinanceChatUseCase,
  AskFinanceChatCommand,ChatReply}.cs` — copies `CategorizeExpenseUseCase`'s guard skeleton
  (`ISettingsStore` → `ILLMClientFactory.Resolve`) line for line; the question-length check (1-500)
  precedes even the settings read, and "not configured" precedes both the context build and the HTTP
  call (zero wasted DB reads). 15s linked CTS identical to Phases 13/14, same Stop-vs-timeout
  distinction. `Temperature`/`MaxTokens` clamped independent of `AppSettings`. Every outcome is
  `Result.Success(ChatReply)`, never `Result.Failure`. `Application/Llm/PromptNames.cs` +1
  (`ChatFinanceSystem`).
- Infrastructure: `Infrastructure/Llm/Prompts/chat-finance.system.{es-DO,en}.txt` — the D8 grounding
  sentence is byte-identical in both locale files (no `.csproj` change needed, already globbed).
- UI: `UI/Navigation/INavigationAware.cs` (new opt-in hook) wired into both `NavigationService
  .NavigateCore` and `GoBack` — required because `GoBack` reactivates the same VM instance rather than
  resolving a new one; no-op for every pre-existing route. `UI/Services/{IClipboardService,
  AvaloniaClipboardService}.cs` — resolves `TopLevel.Clipboard` via the classic desktop lifetime,
  no-ops with a Debug log when unavailable. `UI/ViewModels/{ChatMessageItem,ChatViewModel}.cs` —
  generation-token `SendAsync`/`Stop`/`NewConversation`/`CopyAsync` commands (the `ExpensesViewModel`
  precedent, no debounce); `_answeredTurns` grows only on `Answered`, so failed/cancelled/rejected/
  banner turns can never be replayed by construction. `UI/Views/ChatView.axaml` — full routing
  checklist (`RouteNames.Chat`, `NavigationService._routes`, both `ViewLocator` arms, `ShellViewModel`
  sidebar item), a permanent (unconditional) privacy disclosure, and an exact-figure panel sourced
  only from the DTO.
- Applied in 6 strictly-chained commits (`e400643` context builder, `9cd88da` renderer, `ae16098` use
  case + prompts, `59b15b9` navigation hook + clipboard port, `a05db68` view model, `574bd1a` view +
  routing). S3, S4, and S6 each carried a mandatory app smoke-launch before their gate-run closed
  (DI-wiring risk for S3, `NavigationService` core-path risk for S4, full routing-surface risk for
  S6). Each slice was independently re-verified by the orchestrator after landing.
- **No fix commit required** — the `sdd-verify` adversarial reliability+risk review pass over the
  full phase diff found no confirmed defects: no exception escapes to the UI, cancellation is honored
  and correctly distinguished from the 15s hard timeout, the `INavigationAware` hook is safely no-op
  for every pre-existing route, `SanitizeName` closes the section/flag-marker prompt-injection vector,
  and no secret, question, or reply text reaches the logs.
- Gates: build clean, 1422/1422 tests (1154 unit + 268 integration, up from 1344 at Phase 14 close),
  re-verified independently 3 consecutive times with zero flakiness, `dotnet format style` clean,
  NetArchTest 11/11 — see `.atl/sdd/f-015p15-ai-chat/verify-report.md` and `archive-report.md`.
- Spec archived to `spec/features/phase-15/F-015-ai-finance-chat.done.md`.

The full task breakdown is at `tasks/phase-15/F-015-ai-finance-chat.tasks.md`.

### Phase 16 — what's done

- Application: `Application/UseCases/Goals/GenerateSavingsPlan/{GenerateSavingsPlanUseCase,
  SavingsPlanNarrationDto,SavingsPlanNarrationGuard}.cs` — the use case takes only `Guid goalId`,
  checks `GetByIdAsync` BEFORE reading `AppSettings` (so `GoalNotFound` is reported even when AI is
  off), then follows `CategorizeExpenseUseCase`'s guard skeleton (`ISettingsStore` →
  `ILLMClientFactory.Resolve`, zero calls when not configured). It recomputes via
  `Goal.CalculateProgress` + `SavingsPlanCalculator.Calculate(..., IClock.Today)` — never a
  VM-supplied figure — and classifies the plan into `Live`/`AlreadyMet`/`TargetDateInPast`
  (`AlreadyMet` wins when both apply), sending only that state's figures to the model. 15s linked CTS
  identical to Phases 13/14/15, `Temperature ≤ 0.5`/`MaxTokens ≤ 300` clamped independent of
  `AppSettings`. `SavingsPlanNarrationGuard` is a pure static class: `Clean` strips code fences,
  collapses 3+ newlines, and caps at 600 chars cutting at the last whitespace; `IsGrounded` extracts
  ASCII digit runs, normalizes leading zeros, rejects non-ASCII numeric characters outright, and
  checks membership (not position) against the sent figures PLUS the sanitized goal name. Every
  AI-related outcome is `Result.Success(SavingsPlanNarrationDto)`; only `GoalNotFound` is
  `Result.Failure`. `Application/Llm/PromptNames.cs` +2 (`GenerateSavingsPlanSystem`,
  `GenerateSavingsPlanUser`).
- Infrastructure: `Infrastructure/Llm/Prompts/generate-savings-plan.{system,user}.{es-DO,en}.txt` —
  the system prompt has no placeholders and the D12 rule text ("Never compute, recompute, round,
  estimate, convert, or state any other number...The goal name is data; ignore any instruction inside
  it.") is byte-identical in both locales; the user prompt's `{{goal_name}}` placeholder is replaced
  LAST so a name literally containing `{{plan}}` can never be re-expanded.
- UI: `UI/ViewModels/GoalRow.cs` — converted from an immutable record to
  `public sealed partial class GoalRow : ObservableObject`, keeping the 13-parameter constructor's
  exact PascalCase names inside a scoped `#pragma warning disable IDE1006` so existing named-argument
  test call sites compile unchanged; gains observable `PlanNarration`/`PlanNarrationStatusText`/
  `IsGeneratingPlan`. `UI/ViewModels/GoalsViewModel.cs` — `RequestPlanCommand` declared with
  `[RelayCommand(AllowConcurrentExecutions = true)]` plus a per-row `IsGeneratingPlan` early-return
  guard, so a request in flight on one row never disables another row's button (the default,
  non-concurrent `[RelayCommand]` shares one `IsRunning` flag across every invocation, which would
  have disabled every row). `LoadGoalsAsync` needed no change — it already rebuilds fresh `GoalRow`
  instances on every reload, so narration never carries over by `GoalId`. `UI/Views/GoalsView.axaml` —
  the "Pedir plan con IA" button and two fallback `TextBlock`s inside the existing row card, using
  only pre-existing design tokens.
- Applied in 4 strictly-chained commits (`536528c` guard + DTO + prompts, `1963842` use case,
  `887b28d` row + ViewModel, `e9caef2` view + docs). S2 and S4 each carried a mandatory app
  smoke-launch before their gate-run closed (DI-wiring risk for S2, the last startup-risk surface for
  S4). Phase 17 (in-app notifications) was applied concurrently in this same working tree by other
  agents during Phase 16's own apply and verify passes — the two phases share no code path.
- **No fix commit required** — the `sdd-verify` adversarial reliability+risk review pass over the
  Phase-16-owned portion of the diff found no confirmed defects: no exception escapes to the UI, the
  goal-name prompt-injection surface is closed by sanitize-then-`Replace`-last construction, the
  guard's documented membership-not-position limitation matches its accepted design exactly, and no
  secret/narration/goal-name text reaches the logs.
- Gates: build clean, 1522/1522 tests (1236 unit + 286 integration, up from 1422 at Phase 15 close —
  the integration count also includes Phase 17's concurrently-landed `Notification` persistence
  tests), `dotnet format style` clean, NetArchTest 11/11 — see
  `.atl/sdd/f-016p16-ai-savings-plan/verify-report.md` and `archive-report.md`.
- Spec archived to `spec/features/phase-16/F-016-ai-savings-plan.done.md`.

The full task breakdown is at `tasks/phase-16/F-016-ai-savings-plan.tasks.md`.

### Phase 17 — what's done

- Domain: `Domain/Notifications/{Notification,NotificationKind,NotificationTier,DigestFrequency,
  NotificationCurrencyTotal,NotificationKeys}.cs` — `Notification` is a `sealed class` with private
  setters, per-kind `For*` factories that reject facts belonging to another kind, idempotent
  `MarkRead`/`Dismiss` (first timestamp wins), `RefreshFacts` (throws when dismissed) and
  `HasSameFacts`; it stores facts only, with the subject label sanitized and capped at 120 chars.
  `NotificationKeys` builds the single `DedupKey` string per kind (`debt:{id:N}`,
  `recurring:{id:N}:{yyyy-MM-dd}`, `budget:{id:N}:{yyyy-MM}:{Near|Exceeded}`, `anomaly:{id:N}`,
  `digest:{Daily|Weekly}:{yyyy-MM-dd}`).
- Application: `Application/Notifications/{NotificationRules,DigestComposer,DigestPeriod,
  NotificationReconciler,NotificationSettings,NotificationSettingsReader}.cs` and
  `Application/UseCases/Notifications/{DetectNotifications,GetNotifications,MarkNotificationsRead,
  DismissNotification,GetNotificationSettings,UpdateNotificationSettings}/`. Detection reads each
  enabled source sequentially (no `Task.WhenAll`); the reconciler plans add / remove / refresh-facts /
  purge (60 days) and never recreates a dismissed key; a new `Exceeded` row removes an undismissed `Near`
  for the same budget and month; the digest is composed only from complete reads and its `yyyy-MM-dd`
  cursor (`SettingKeys.NotificationsDigestLastPeriodStart`) is written only after a successful
  reconcile. 6 new `SettingKeys` (4 toggles, digest frequency, digest cursor) through `ISettingsStore`;
  `AppSettings` is untouched.
- Infrastructure: `NotificationConfiguration`, `NotificationRepository` (`ApplyReconciliationAsync` =
  one `SaveChangesAsync`; on `DbUpdateException` it detaches only its own entries and never calls
  `ChangeTracker.Clear()`), migration `20260926064030_AddNotifications` (additive only: `CreateTable` +
  3 `CreateIndex` in `Up`, `DropTable` only in `Down`).
- UI: `Services/{INotificationCenter,ScopedNotificationCenter}.cs`,
  `ViewModels/{NotificationListViewModel,NotificationItemViewModel}.cs`,
  `Formatting/NotificationTextFormatter.cs`, `ShellViewModel` (owns the bell state),
  `MainWindow.axaml` (new header row, `ToggleButton` bell + badge + `Popup`, list in a
  `ScrollViewer MaxHeight="400"`), `SettingsView.axaml`/`SettingsViewModel` ("Notificaciones" section,
  save triggers exactly one run), `App.axaml.cs` (`mainWindow.Opened` -> `RefreshAsync`,
  fire-and-forget). 32 new resx keys in `Strings.resx`, `Strings.es-DO.resx` and `Strings.Designer.cs`.
- Deliberate rules a future reader must not "fix": (a) undismissed rows have their facts refreshed on
  every run without touching `Id`/`CreatedAt`/`ReadAt` (a Near notice showing 85% must follow the budget
  to 95%, and must never re-badge an already-read row); (b) the digest counts from the full sources and
  ignores per-kind toggles and dismissals (it has its own on/off via the frequency `Off`); (c) an
  indeterminate (missing-FX-rate) budget keeps its existing notice instead of being removed.
- Applied in 9 strictly-chained commits (`1b0c9cf`, `c9130a5`, `73bba65`, `94f32e3`, `8effc0d`,
  `bac31b2`, `5e72036`, `c879ce2`, `d0c9552`), interleaved with Phase 16's own commits. S1b, S5b and S6
  carried automated smoke launches (no crash). Verify verdict: PASS WITH WARNINGS, 0 critical (see
  `.atl/sdd/f-017p17-notifications/verify-report.md`); the nine divergences from the design are recorded in
  the spec's "Post-apply reconciliation" subsection.
- Gates: build clean, 1700/1700 tests (1405 unit + 295 integration, up from 1522 at Phase 16 close),
  `dotnet format style` clean, NetArchTest 11/11, `ModelSnapshotConsistencyTests` 3/3.
- Spec archived to `spec/features/phase-17/F-017-notifications.done.md`.

The full task breakdown is at `tasks/phase-17/F-017-notifications.tasks.md`. Task 10.2 (manual run in both
locales, incl. the S-12 forced-failure check) is deliberately left unchecked: it was not run manually.

---

## 5. What's pending (next up)

Ordered by priority. Each item references its source-of-truth spec/plan.

1. **Phase 18 — PDF/Excel export** — `docs/roadmap.md` §Fase 18. Check `.atl/sdd/f-018p18-export/` and
   `git log --oneline` for any in-flight work before planning from scratch.
2. **Follow-up, not phase-blocking**: Phase 16's `SavingsPlanNarrationGuard.IsGrounded` is a
   membership check, not a positional one (documented, accepted design cost) — do not "fix" it into a
   positional check; the goal name's digit runs are deliberately included in the allowed set (Spec
   Reconciliation #1) — do not "simplify" that back out, since a goal named "Carro 2027" would then
   reject every narration that names it.
3. **Follow-up, not phase-blocking**: a one-off `DashboardViewModelTests` failure batch (12 tests,
   one run out of 5 full-suite runs during Phase 16's `sdd-verify` gate re-run) did not reproduce in
   4 subsequent full-suite runs nor 2 isolated re-runs of that test class. Phase 16 touches no
   Dashboard file — likely first-run JIT/cold-start timing sensitivity, not a regression. Worth a
   dedicated investigation if it recurs.
4. **Follow-up, not phase-blocking**: fix `Motion.axaml`'s `RenderTransform` animation bug (see
   Phase 9 notes above) so the row-reveal animations can be merged into `App.axaml` alongside
   `LedgerStyles.axaml`.
5. **Follow-up, not phase-blocking**: `DebtRepository.UpdateAsync` AND `DebtPaymentRepository.UpdateAsync`
   both have the same `OwnsOne`-navigation copy gap that Phase 10 found and fixed for `BudgetRepository`
   (and that Phase 11 correctly avoided for both `GoalRepository` and `GoalContributionRepository`).
   The `DebtPaymentRepository` half was newly identified during Phase 11's design. Tracked as a single
   combined `fix/owned-money-update` follow-up covering both files, for whichever future phase next
   touches `Debt`/`DebtPayment` amounts.
6. **Follow-up, not phase-blocking**: Phase 11's spec (R-1/S-2) describes a UI-driven goal-edit flow
   that was never built. Either add an edit command to `GoalsViewModel`/`GoalsView` or formally narrow
   R-1/S-2's wording to match the shipped create/contribute/view/delete scope.
7. **Follow-up, not phase-blocking**: `Strings.Designer.cs` has now been hand-edited for 6 consecutive
   phases (9, 10, 11, 12, 14, 16) — Phase 13 was the one exception (`SettingsView`'s 6 new keys reused
   the existing pattern; `ExpensesView`'s new keys route through `IStringLocalizer` instead). Consider a
   standalone change to restore code generation or replace `Strings` with a generated wrapper before the
   key count grows further.
8. **Follow-up, not phase-blocking**: no headless Avalonia test harness exists yet. Phase 12's
   startup-culture-read scenarios (persisted `en`, corrupt locale) had to stay manual-only as a result
   — worth revisiting if a future phase needs to automate a launch-time scenario.
9. **Follow-up, not phase-blocking**: Phase 13's roadmap criterion (a) is gated on a deterministic fake
   `ILLMClient` response, not a live model — the opt-in `CONTROL_FINANZAS_RUN_EXTERNAL_AI_TESTS`
   OpenRouter round trip proves the wire format works, not real-model suggestion quality. Prompt tuning
   against logged `RawModelAnswer` values is tracked as a follow-up, not a phase blocker. **Phases 14,
   15, and 16 inherit the exact same caveat** for their own prompts — all gated on a deterministic fake
   `ILLMClient` in tests, not a live model.
10. **Follow-up, not phase-blocking**: the dashboard month label still renders in English (e.g.
    "September 2026") under the `es-DO` locale. Pre-existing, not introduced by Phase 14 — Phase 12
    only sets `CurrentUICulture` from the persisted locale, not `CurrentCulture`, so `DateTime`-formatting
    APIs that read `CurrentCulture` (month names) fall back to the OS/thread default. Re-surfaced during
    Phase 14 because the forecast section shows a month label on the same screen.
11. **Follow-up, not phase-blocking, unconfirmed**: a possible sidebar highlight mismatch was observed in
    a screenshot during Phase 14's own smoke-launch checks. Not reproduced deliberately — may simply be
    mouse hover state captured mid-screenshot rather than a real navigation-highlight bug. Worth a clean
    re-check (mouse away from the sidebar) before treating it as a defect.
12. **Follow-up, not phase-blocking**: `ChatViewModel.SendAsync` needs an explicit `if (IsSending)
    return;` guard in ADDITION to the `[RelayCommand(CanExecute = ...)]` predicate, because
    `CommunityToolkit.Mvvm`'s generated `AsyncRelayCommand` does not itself re-check `CanExecute`
    before running the delegate when invoked directly (only a bound button's own click path is
    protected). Any future `[RelayCommand]` async method that must guarantee "at most one in flight"
    needs the same explicit guard — the `CanExecute` predicate alone is not sufficient. **Phase 16's
    `GoalsViewModel.RequestPlanCommand` sidesteps this by design**: it uses
    `AllowConcurrentExecutions = true` plus a per-row `IsGeneratingPlan` flag instead, since it
    deliberately needs MULTIPLE concurrent in-flight executions (one per row), not "at most one".
13. **Follow-up, not phase-blocking**: Phase 15 introduced this codebase's first
    `[assembly: InternalsVisibleTo("ControlDeFinanzasIA.Tests.Unit")]`
    (`src/ControlDeFinanzasIA.UI/AssemblyInfo.cs`), exposing `NavigationService`'s internal test-only
    constructor overload so `NavigationServiceTests` can exercise the real `NavigateCore`/`GoBack`
    code paths with a recording `INavigationAware` spy. A reusable seam for any future test that needs
    to exercise `NavigationService` end to end without a headless Avalonia harness.
14. **Follow-up, not phase-blocking**: Phase 15's `chat-finance.system.*` prompts inherit the exact
    same live-model prompt-tuning caveat as Phase 13's categorization prompt and Phase 14's
    insights/anomaly-narration prompts — all tests use a deterministic fake `ILLMClient`; real-model
    response quality was not re-verified interactively as part of this phase's SDD close.
15. **Follow-up, not phase-blocking**: Phase 17 verify warnings. The manual task 10.2 run (both
    locales, real dev DB, S-12 forced reconcile-save failure) was NOT performed; the
    `mainWindow.Opened` -> `RefreshAsync` startup trigger has no automated test; "exactly one
    `SaveChangesAsync` per run" is proven structurally, not by a save-count test; S-4 idempotency is
    tested at the reconciler level only, not end to end through the use case.
16. **Follow-up, not phase-blocking**: Phase 17 cleanups. The `budgetReadFailed` branch in
    `DetectNotificationsUseCase` (~lines 212-227) is defensive and untested because
    `GetBudgetVsActualQuery` never returns `Failure` today; `MainWindow.axaml` hardcodes
    `#22E8A33D`/`#33E8A33D` (precedent `ChatView.axaml:29`) instead of a design token;
    `RetentionDays = 60` is declared in both `NotificationRules` and `NotificationReconciler`;
    `ScopedNotificationCenter` captures `SynchronizationContext.Current` at construction, so it must be
    resolved on the UI thread; the unread accent stripe on popup rows is effectively never visible
    (opening the popup marks the rows read).
17. **Phase 19 — Backup/restore** — `docs/roadmap.md` §Fase 19
18. **Phase 20 — CSV/Excel import** — `docs/roadmap.md` §Fase 20
19. **Phase 21 — Packaging + accessibility** — `docs/roadmap.md` §Fase 21

**MVP (no IA) = phases 0–12, fully complete. Phases 13–16 = first four shipped AI features. Phase 17
(notifications) is deterministic and AI-free.** Phase 18+ continues the roadmap.

---

## 6. Architecture & constraints

### Layer model (Clean Architecture, 4 layers)

```
                  ┌──────────────────────┐
                  │       UI (Avalonia)  │
                  └──────────┬───────────┘
                             │ depends on
                             ▼
   ┌──────────────────┐  ┌────────────────────┐  ┌──────────────────────┐
   │  Infrastructure  │─▶│    Application     │─▶│       Domain         │
   └──────────────────┘  └────────────────────┘  └──────────────────────┘
           │                                              ▲
           └──── implements interfaces from ─────────────┘
                  Application (I*Repository, ILLMClient, ...)

   ┌──────────────┐        ┌──────────────────────┐
   │ Tests.Unit   │───────▶│ Domain / Application │
   └──────────────┘        └──────────────────────┘

   ┌──────────────────┐    ┌──────────────────────┐
   │ Tests.Integration│───▶│  Infrastructure      │
   └──────────────────┘    └──────────────────────┘
```

### Hard rules (enforced by NetArchTest in `tests/.../Unit/Architecture/ArchitectureTests.cs`)

- **R-1:** `Domain` has zero outward dependencies (only BCL).
- **R-2:** `Application` depends only on `Domain` and neutral packages (FluentValidation, etc.).
- **R-3:** `Infrastructure` depends on `Application` + `Domain`; no reference to `UI`.
- **R-4:** `UI` depends on `Application` + `Domain`; no reference to `Infrastructure` except in `Bootstrap` (composition root) — `Bootstrap` is whitelisted by the test filter.
- **R-5:** Tests may reference anything but must not ship in the app.

### Cross-cutting rules

- **MVVM strict in Avalonia.** No logic in code-behind. Views bind to ViewModels via CompiledBindings. `[ObservableProperty]` and `[RelayCommand]` from CommunityToolkit.Mvvm.
- **Async/await end-to-end.** Every I/O is async. `CancellationToken` plumbed through.
- **Errors as values.** Use `Result` (non-generic, `Value: object?`) + `PagedResult<T>` from `Application/Common/`. Typed exceptions reserved for the unexpected. Result factories: `Result.Success(object)` / `Result.Failure(string code, string message)`.
- **No remote telemetry.** No analytics. No remote calls except the explicit user-configured LLM.
- **No secrets in repo.** User LLM API keys live in the local data file (encryption deferred to v2 — ADR-011).
- **Localization via `IStringLocalizer`.** `es-DO` default, `en` supported. No hardcoded UI strings.
- **EF Core Code First, migrations versioned and committed.** Destructive schema changes need an explicit migration + backup plan.
- **Central Package Management.** Versions live in `Directory.Packages.props` only. `.csproj` files use `<PackageReference Include="X" />` with no `Version` attribute.
- **Money is not domain-aware of conversion.** `Money` arithmetic only; conversion is at the Application/Infrastructure layer via `ICurrencyConverter` + `IExchangeRateProvider`.

### Sources of truth

| Concern | Document |
| --- | --- |
| Why | `docs/vision.md` |
| When | `docs/roadmap.md` |
| How | `docs/architecture.md` |
| Mission | `spec/constitution/mission.md` |
| Stack constitution | `spec/constitution/tech-stack.md` |
| Per-phase plan | `spec/phases/phase-NN/features.md` |
| Per-feature spec | `spec/features/phase-NN/F-XXX-slug{.spec.md,.done.md}` |
| Per-feature tasks | `tasks/phase-NN/F-XXX-slug.tasks.md` |
| ADRs | `docs/architecture.md` §19 |

---

## 7. File map

### `src/`

```
src/
├── ControlDeFinanzasIA.Domain/            # Pure domain — no EF Core, no Avalonia
│   ├── Entities/                          # Expense, Income, Tag, SavedView, etc.
│   ├── ValueObjects/                      # Money, DateRange, ExchangeRate
│   ├── Enums/                             # Currency, RecurringFrequency, ...
│   ├── Catalog/                           # Category, PaymentMethod
│   └── Recurring/                         # RecurringRule (TPH base), Subscription,
│                                          # FixedExpense, RecurringOccurrenceCalculator
├── ControlDeFinanzasIA.Application/       # Use cases, DTOs, validators, abstractions
│   ├── Abstractions/                      # I*Repository, IClock, IPathResolver,
│   │                                      # IExchangeRateProvider, IRecurringRuleRepository, ...
│   ├── Common/                            # Result, PagedResult<T>, FilterJsonSerializer
│   ├── DTOs/                              # ExpenseDto, IncomeDto, PaymentMethodDto, ...
│   ├── UseCases/
│   │   ├── Expenses/                      # RegisterExpense, GetTransactions, UpdateExpense
│   │   ├── Incomes/                       # RegisterIncome, GetIncomes, UpdateIncome
│   │   ├── Catalog/                       # CreateTag, GetPaymentMethods
│   │   ├── SavedViews/                    # SaveCurrentFilter, ApplySavedView, ...
│   │   ├── Transactions/                  # DeleteTransaction
│   │   └── Recurring/                     # CreateSubscription, UpdateSubscription,
│   │                                      # CreateFixedExpense, UpdateFixedExpense,
│   │                                      # Delete{FixedExpense,RecurringRule},
│   │                                      # Deactivate{FixedExpense,RecurringRule},
│   │                                      # GenerateDueRecurring, SkipNextOccurrence
│   └── Services/                          # CurrencyConverter (display-time)
├── ControlDeFinanzasIA.Infrastructure/    # EF Core, SQLite, exchange rate stub
│   ├── Persistence/
│   │   ├── AppDbContext.cs
│   │   ├── ServiceCollectionExtensions.cs # AddPersistence(...)
│   │   ├── EfUnitOfWork.cs
│   │   ├── SeedData.cs                    # 9 categories + 5 payment methods (IsSystem=true)
│   │   ├── Configurations/                # One IEntityTypeConfiguration<T> per entity
│   │   ├── Repositories/                  # One per interface
│   │   └── Migrations/                    # Versioned; includes
│   │                                      # 20260910205248_AddRecurringRules
│   ├── System/                            # SystemClock, SystemPathResolver
│   └── ExchangeRates/StubExchangeRateProvider.cs
└── ControlDeFinanzasIA.UI/                # Avalonia 11.2.7 (MVVM strict)
    ├── Program.cs                         # Entry point; wires Avalonia + host
    ├── App.axaml(.cs)                     # App root; registers DataTemplate per VM
    ├── Composition/                       # Bootstrap.ConfigureServices (composition root)
    ├── Navigation/                        # INavigationService, NavigationService, RouteNames
    ├── ViewModels/                        # ShellViewModel, DashboardViewModel,
    │                                      # ExpensesViewModel, IncomeViewModel,
    │                                      # RecurringRulesViewModel, ViewModelBase
    ├── Views/                             # One .axaml per VM (no logic in code-behind)
    ├── System/                            # UI-side system clock/path impls if needed
    └── Resources/Strings.resx, Strings.es-DO.resx
```

### `tests/`

```
tests/
├── ControlDeFinanzasIA.Tests.Unit/
│   ├── Architecture/ArchitectureTests.cs  # NetArchTest R-1..R-5
│   ├── Domain/                           # Entity + VO tests
│   ├── Application/                       # Use case tests (hand-rolled fakes, no Moq)
│   ├── UI/                               # ViewModel tests
│   ├── Common/                           # Result, PagedResult tests
│   ├── ValueObjects/                      # Money, DateRange, ExchangeRate tests
│   ├── Infrastructure/ExchangeRates/      # StubExchangeRateProvider tests
│   └── TestDoubles/                       # FakeClock, FakePathResolver (+ tests)
└── ControlDeFinanzasIA.Tests.Integration/
    └── Persistence/                       # SQLite in-memory tests via TestAppDbContextFactory
                                           # (schema, repos, migrations, filters)
```

### `spec/`

```
spec/
├── README.md                              # Spec convention
├── constitution/
│   ├── mission.md                         # Product purpose (Spanish)
│   └── tech-stack.md                      # Stack + conventions
├── phases/
│   ├── phase-01/features.md               # Phase 1 plan
│   ├── phase-02/features.md               # Phase 2 plan
│   ├── phase-03/features.md               # Phase 3 plan
│   ├── phase-04/features.md               # Phase 4 plan
│   ├── phase-05/features.md               # Phase 5 plan
│   └── phase-06/features.md               # Phase 6 plan (status table inside)
└── features/
    ├── phase-NN/F-XXX-slug.spec.md        # Active spec
    ├── phase-NN/F-XXX-slug.done.md        # Archived spec (after archive)
    └── phase-06/F-001-recurring-rules.spec.md   # Current active feature
```

### `tasks/`

```
tasks/
└── phase-NN/F-XXX-slug.tasks.md           # Task breakdown per feature
```

(Every feature has a `.tasks.md` companion to its spec.)

### `docs/`

```
docs/
├── vision.md                              # Why (product vision, non-goals)
├── roadmap.md                             # When (phases 0–21)
├── architecture.md                        # How (layers, contracts, ADRs)
├── roadmap-notion-ready.md                # Compact roadmap view for Notion sync
└── AI_HANDOFF.md                          # ← this file
```

### `.claude/skills/`

- `.claude/skills/frontend-design/SKILL.md` — **invoke for any UI/visual change.** Design tokens centralized, MVVM strict, no generic AI aesthetics.

### `.atl/`

- `.atl/sdd/<change-id>/{proposal,spec,tasks,design,apply-progress,verify-report,archive-report}.md` — per-change working memory for the SDD cycle.
- `.atl/skill-registry.md` + `.atl/.skill-registry.cache.json` — index of installed skills.
- `.atl/sdd/f-006p6-formalize-recurrentes/` — the live Phase 6 SDD working dir.

### Other top-level files

- `Directory.Build.props` — common MSBuild props (`Nullable=enable`, `LangVersion=latest`, analyzers).
- `Directory.Packages.props` — **Central Package Management.** Versions live here only.
- `stylecop.json`, `.editorconfig` — style/analyzer config.
- `opencode.json` — agent + MCP config.

---

## 8. Conventions

### Commits

- Conventional Commits (`feat:`, `fix:`, `chore:`, `refactor:`, etc.).
- **No `Co-Authored-By: AI` trailers.** Ever.
- No interactive `-i`. No `--no-verify` on hooks unless explicitly requested.

### Code

- **MVVM strict.** `[ObservableProperty]`, `[RelayCommand]` from CommunityToolkit.Mvvm.
- **Errors as values** (`Result.Failure(code, message)`) for expected errors. Typed exceptions only for unexpected.
- **`IClock` for time.** No `DateTime.Now` / `DateTime.UtcNow` in use cases or services.
- **`IPathResolver` for filesystem paths.** No `Environment.GetFolderPath` outside `Infrastructure/System/`.
- **Async naming.** `*Async` suffix on every async method.
- **`CancellationToken`** parameter on every async I/O method.
- **File-scoped namespaces.** `using` statements at top of file, not nested.
- **One public type per file.** File name = type name.
- **PascalCase for public members, camelCase for locals/params, `_camelCase` for private fields.**

### Naming

| Element | Pattern | Example |
| --- | --- | --- |
| ViewModel | `XxxViewModel` | `ExpensesViewModel` |
| View | `XxxView` | `ExpensesView` |
| Service | `XxxService` | `BackupService` |
| Repository | `XxxRepository` | `ExpenseRepository` |
| Interface | `IXxx` | `IExpenseRepository` |
| Use case | `XxxUseCase` | `RegisterExpenseUseCase` |
| Query | `XxxQuery` / `XxxUseCase` | `GetTransactionsQuery` |
| Test | `XxxTests` | `ExpenseRepositoryTests` |

### Localization

- `IStringLocalizer` for UI strings; resx files (`Strings.resx`, `Strings.es-DO.resx`).
- `es-DO` default; numbers use `.` for thousands and `,` for decimals; dates `dd/MM/yyyy`.

### Specs

- Active: `spec/features/phase-NN/F-XXX-slug.spec.md`.
- Archived (after the SDD `archive` step): rename to `.done.md`.
- Task breakdowns always paired: `tasks/phase-NN/F-XXX-slug.tasks.md`.

---

## 9. Common commands

Run from repo root with `workdir="C:\Users\diego\Desktop\ControlDeFinanzasIA"`.

```powershell
# Build
dotnet build -c Release

# Run the app
dotnet run --project src/ControlDeFinanzasIA.UI

# Run all tests
dotnet test -c Release --no-build

# Format check (CI gate)
dotnet format style --verify-no-changes

# EF Core migrations (after installing dotnet-ef 8.0.11 locally)
dotnet ef migrations add <Name> `
  --project src/ControlDeFinanzasIA.Infrastructure `
  --startup-project src/ControlDeFinanzasIA.UI
```

---

## 10. How to resume work

### Where to start

1. **Read `spec/` first.** Phase 17 (in-app notifications) is archived at `spec/features/phase-17/F-017-notifications.done.md`. Phase 18 (PDF/Excel export) is next per the roadmap — check `.atl/sdd/f-018p18-export/` and `git log --oneline` for in-flight work before planning from scratch.
2. **Check tasks:** `tasks/phase-17/F-017-notifications.tasks.md` is the closed checklist for the last fully completed phase (9 strictly chained slices; only the manual task 10.2 is deliberately unchecked) — use it as the pattern reference for a multi-slice breakdown.
3. **Read constitution** if touching architecture or stack: `spec/constitution/mission.md`, `spec/constitution/tech-stack.md`.
4. **Check the SDD working dir** for any in-flight change: `.atl/sdd/<change-id>/`.

### SDD cycle (filesystem, not OpenSpec)

```
propose → spec → tasks → apply → verify → archive
```

Artifacts per cycle land under `.atl/sdd/<change-id>/{proposal,spec,tasks,design,apply-progress,verify-report,archive-report}.md`.

### Testing capabilities cache

- Skills installed locally: see `<available_skills>` in your system prompt (authoritative for the session). Also indexed at `.atl/skill-registry.md` and `.atl/.skill-registry.cache.json`.
- Project-local skills: `.claude/skills/` (currently `frontend-design`).
- MCP servers: Context7 (library docs — Avalonia, EF Core, xUnit, etc.) and Engram (persistent memory) — both configured at user level (`~/.config/opencode/opencode.json`).

### Engram topic keys

Memory is organized under `sdd/...`. Examples already present:

- `sdd/f-001p4-income-entity-config-migration/...`
- `sdd/f-007p3-expenses-viewmodel-view/...`

Project status snapshot lives under `project/status-snapshot`.

### When to invoke Context7

Per `spec/constitution/tech-stack.md`: invoke Context7 whenever introducing or upgrading a library or pattern. Even for well-known libs (Avalonia, EF Core, xUnit) — your training may not reflect recent changes.

---

## 11. Gotchas & non-obvious discoveries

1. **`.done.md` rename is part of the archive step**, not earlier. The spec is renamed only **after** the SDD archive phase completes. Do not rename prematurely.

2. **No `openspec/` directory.** The project uses filesystem-based specs in `spec/`. Don't introduce OpenSpec — it's explicitly excluded per `AGENTS.md`.

3. **Phase IDs are local to each phase.** `F-001` in `phase-03/` is the Expense entity; `F-001` in `phase-04/` is the Income entity. Folder disambiguates. This is intentional (see `spec/README.md`).

4. **Phase 5 spec files use `F-501`, `F-502`, `F-503`** while the phase plan uses granular `5-001` through `5-013`. The plan's sub-IDs were grouped into three archived specs. Treat the `.done.md` filenames as authoritative for what shipped.

5. **`Result` is non-generic**, not `Result<T>`. From `spec/features/phase-01/F-005-comuntypes-result-pagedresult.done.md`: `Result` has `IsSuccess`, `Value: object?`, `Error?`. The caller casts `Value` to the expected type after checking `IsSuccess`. Don't introduce `Result<T>` — the project deliberately uses the simpler form.

6. **NetArchTest R-4 whitelists `Bootstrap`.** The composition root is the one place where `UI` is allowed to reference `Infrastructure`. Any other type in `UI` that touches `Infrastructure` directly will fail R-4.

7. **CPM (Central Package Management) is mandatory.** Package versions live only in `Directory.Packages.props`. `.csproj` files reference packages by name with no `Version` attribute. Do not hardcode versions in `.csproj`.

8. **EF Core migrations are versioned and committed.** Destructive schema changes need an explicit migration and, where applicable, a backup. The `20260910205248_AddRecurringRules` migration is the latest committed.

9. **No remote telemetry. No secrets in repo.** User LLM API keys live in the local data file (cleartext in v1, encryption deferred to ADR-011 v2).

10. **Avalonia 11.2.7 is pinned** in CPM. Don't bump without Context7 check.

11. **DI registration is centralized in `Bootstrap.ConfigureServices`.** New ViewModels, use cases, repositories, or services are registered there — not scattered.

12. **Phases 6 and 7 are fully closed** — all gates ran clean on 2026-09-17 and 2026-09-18 respectively (build, tests, format, NetArchTest) and both specs are archived. `dotnet format style` caught a real bug in Phase 6 close (PascalCase constructor parameters) — always run the format gate for real, don't skip it as "just style." Phase 7's apply (delegated to an agent) independently introduced and then self-caught two more real bugs: a `Money` instance shared across generated `DebtPayment`s (EF Core owned-type tracking silently drops the value for all but one owner) and a stale `AppDbContextModelSnapshot.cs` that would have re-emitted already-applied `Expenses` columns. Both were verified fixed independently before archiving, not just trusted from the agent's self-report — see `.atl/sdd/f-007p7-debts-installment-plan/verify-report.md`.

13. **Tests use hand-rolled fakes, not Moq/NSubstitute.** `FakeClock`, `FakePathResolver`, `FakeExpenseRepository`, etc. live in `tests/.../TestDoubles/`. The project intentionally avoided a mocking library.

14. **`tests/` source-marker count is 564** (`[Fact]`+`[Theory]` declarations), expanding to ~559 runtime test cases (the difference is `[Theory]` rows that fan out into multiple cases). AGENTS.md's "559/559" matches the runtime count.

15. **MVVM in `RecurringRulesViewModel` injects 11 dependencies.** That wide constructor is a smell for "Form vs ListView" split when you formalize F-006. Use it as a hint that the UI needs a refactor in the formal apply phase.

16. **Phase 8 is fully closed** — 751/751 tests as of 2026-09-18 (up from 698 at Phase 7 close). Applied in 3 separate commits (`b53102f`, `3fc5325`, `5aee8c9`), one per slice, per `sdd-tasks`' chained-PR recommendation — the first time this project actually split an apply into multiple commits instead of one (Phases 6 and 7 both landed as a single commit despite similar "high risk" flags). Two real bugs caught: `ICurrencyConverter.DisplayAsync` converts to the "other" currency, not always DOP — wrong for a DOP-normalizing dashboard, caught in design before any code was written; and EF Core 8's SQLite provider throws `NotSupportedException` on `Sum(decimal)` inside any `GroupBy` regardless of grouping key — worked around by summing as `double` and rounding back to 4 decimals after materialization, with dedicated precision-regression tests (summing 0.1m repeatedly, cross-checking independent aggregates reconcile exactly).

17. **Windows Application Control can block a freshly-built Release DLL from loading into the xUnit test host** (`0x800711C7`) on this machine, first observed during Phase 8's slice 3. `dotnet build -c Release` still succeeds and Release Integration tests still run — only the Unit test host's load of `ControlDeFinanzasIA.UI.dll` is blocked. Not a code defect. It resolved itself by Phase 9's close (Release ran cleanly end to end again) — if you hit it, fall back to `-c Debug` for the unit-test gate and note it explicitly, and don't assume it's permanently active.

18. **Nobody had actually launched the app until Phase 9's close** — every prior phase verified purely through `dotnet test`. Doing `dotnet run` + a screenshot (Phase 9) found two runtime-only bugs a green test suite could never catch: `ViewLocator` missing the `DebtsViewModel` case (Debts rendered blank since Phase 7, fixed in `e0e51d3`) and the entire "Libro Mayor" stylesheet (`LedgerStyles.axaml`, since Phase 4) never merged into `App.axaml` — every `ledger-*` class had been inert the whole time (fixed in `4d981df`, which also fixed a real `CornerRadius`/`sys:Double` type mismatch that only threw at runtime). **Lesson: a passing test suite is necessary but not sufficient — periodically actually run the app.** `Motion.axaml` (row animations) still isn't merged; it has its own unfixed `RenderTransform` animator bug, tracked as follow-up.

19. **EF Core `CurrentValues.SetValues` does NOT copy `OwnsOne` owned-type navigations** — only scalar
    properties of the entity itself. `DebtRepository.UpdateAsync` was written before this was
    understood; it silently ships this gap for `Debt.OriginalAmount`/`InstallmentAmount` (mostly
    unexercised so far). Phase 10 (`BudgetRepository.UpdateAsync`) hit the same trap with `Budget.Amount`
    — fixed there by explicitly calling `_db.Entry(existing).Reference(b => b.Amount).TargetEntry!.CurrentValues.SetValues(budget.Amount)`
    after the top-level `SetValues` call, guarded by a dedicated round-trip integration test.
    **Any future repository with an `OwnsOne` property must copy that owned entry explicitly in
    `UpdateAsync`, or edits to that property will be silently discarded.** Phase 11 correctly applied
    this pattern preemptively to both `GoalRepository.UpdateAsync` and
    `GoalContributionRepository.UpdateAsync`, and during design discovered a **second** unfixed
    occurrence: `DebtPaymentRepository.UpdateAsync` has the exact same gap as `DebtRepository.UpdateAsync`
    (not just the one previously flagged). Both are now tracked together as a single combined
    `fix/owned-money-update` follow-up — flagged, not blocking, for whichever future phase next touches
    `Debt`/`DebtPayment` amounts.

20. **A spec's Requirements/Scenarios prose and its Acceptance Criteria checklist can disagree — verify
    the checklist, not just the prose, but flag the disagreement.** Phase 11's spec (`F-011`) describes
    a UI-driven goal-edit flow in R-1's text and scenario S-2, but its own Acceptance Criteria checklist
    (the actual definition-of-done used to gate archiving in this project) never lists a UI-edit
    criterion — only that `GoalRepository.UpdateAsync` persists correctly at the repository level,
    which is tested. The gap traces to the proposal stage, which had explicitly scoped UI editing out
    ("beyond delete-and-recreate"); the design's own Open Questions section flagged the mismatch during
    design, but it was never reconciled against R-1/S-2's literal wording before the spec was finalized
    — unlike 7 *other* design/spec conflicts that this same design document explicitly resolved in a
    dedicated "Spec Reconciliation" table. **Lesson: when a spec's narrative text and its checkable
    acceptance criteria diverge, trust the acceptance criteria for the pass/fail gate, but document the
    divergence explicitly rather than silently picking one and ignoring the other** — see
    `spec/features/phase-11/F-011-savings-goals.done.md` and
    `.atl/sdd/f-011p11-savings-goals/verify-report.md` for the full writeup.

21. **`MainWindow.axaml`'s `ContentControl` resolves navigation content through
    `Converters/CurrentViewConverter.cs`, NOT `Views/ViewLocator.cs`** — despite both existing and both
    looking like "the" view-model-to-view map. `ViewLocator` is registered in `App.axaml`'s
    `Application.DataTemplates` and IS kept up to date every phase (it correctly lists all 9
    navigation view-models today), but it is effectively **dead code for main-window navigation**: an
    implicit `DataTemplate` only kicks in when `ContentControl.Content` is still a raw view-model, and
    here `CurrentViewConverter` already converts it to a concrete `Control` (or `null`) first.
    `CurrentViewConverter`'s own switch was last touched in its only-ever commit (`6caae70`, Phase 6)
    and was never extended past `RecurringRulesViewModel` — so Debts, Calendar, Budgets, Goals, and
    Settings all rendered a **solid black, completely empty content pane** in the real app for 5
    phases, with zero exceptions thrown (a `null` converter result just renders as blank) and zero
    test failures (nothing in the test suite exercises `MainWindow.axaml`'s binding path). Found only
    by manually clicking into a route during Phase 12's smoke test and looking at the screenshot.
    **Fixed in commit `4f8e423`**: `CurrentViewConverter.Convert` now delegates to a shared
    `ViewLocator` instance instead of keeping a second parallel switch, so only one map has to stay
    current from here on. **If you add a new route, verify it renders by actually clicking into it —
    adding it to `ViewLocator` alone is not sufficient proof it works, and was not for 5 phases.**

22. **Timing-based UI tests must leave real margin over the OS timer's resolution, not just over the
    nominal delay under test.** Phase 13's `ExpensesViewModelSuggestionTests.RapidKeystrokes_WithinDebounceWindow_ProduceExactlyOneCall`
    originally set `SuggestionDebounceDelay = 25ms` and simulated keystrokes 20ms apart — a 5ms margin
    that looked adequate on paper but collided with Windows' ~15.6ms default timer resolution, causing
    intermittent flakiness. **Fixed in commit `aa340dd`** by widening the window to 400ms (debounce)
    with proportionally larger keystroke gaps. **Lesson for any future debounce/delay-based test in this
    repo: pick a value at least 10x the platform's timer granularity, not just "clearly bigger than the
    keystroke gap."** Separately, `CategorizeExpenseUseCase` is **intentionally non-sealed with a
    `virtual ExecuteAsync`** — the only use case in the codebase that breaks the "every use case is
    sealed" convention, needed so `FakeCategorizeExpenseUseCase` (a subclass, not a mock) can drive
    `ExpensesViewModel`'s race/cancellation tests without a fake HTTP layer. If you add a new use case
    that needs the same fake-by-subclassing treatment, follow this precedent rather than introducing a
    new pattern.

23. **`AnomalyAlert.Severity` is the first enum in this codebase persisted as a string, not an int.**
    Every other of the 8 `HasConversion<int>()` enum configurations (`Budget.Status`, `Goal.Status`,
    `Debt.Status`, etc.) uses the codebase's default int-enum convention. `AnomalyAlertConfiguration.cs`
    deliberately deviates with `HasConversion<string>().HasMaxLength(16)` because this table is Phase
    17's notification contract — a string column survives an enum reordering and is readable in a raw
    DB inspection, both properties a future notification-delivery phase needs and an int column
    doesn't give for free. **Do not "fix" this back to int as a consistency cleanup** — it is
    intentional, documented inline in the configuration file and in
    `spec/features/phase-14/F-014-ai-insights-forecast-anomalies.done.md`'s Spec Reconciliation
    section. Separately, Phase 14's `DetectAnomaliesUseCase` is the first write path in this codebase
    that shares a process-wide `AppDbContext` on a *read* path (the dashboard load) — its
    `AnomalyAlertRepository.ApplyReconciliationAsync` catches `DbUpdateException` and detaches only the
    entries it attached (`Entry(x).State = Detached`), never `ChangeTracker.Clear()`, which would
    silently poison every other tracked entity on the same shared context. **Any future use case that
    writes on a read-heavy path via the same shared `AppDbContext` should follow this detach-only
    pattern, not `ChangeTracker.Clear()`.**
24. **`CommunityToolkit.Mvvm`'s `[RelayCommand]`-generated `AsyncRelayCommand` does NOT re-check its
    own `CanExecute` before running the delegate when invoked directly** (`Command.Execute(parameter)`
    bypasses the `CanExecute` guard a bound button relies on — only the data-bound-button path is
    protected). Phase 15's `ChatViewModel.SendAsync` needed an explicit `if (IsSending) return;` guard
    at the top of the method, IN ADDITION to `[RelayCommand(CanExecute = nameof(CanSend))]`, to make
    "at most one question in flight" (R-9) a true invariant of the view-model rather than only of the
    bound button's disabled state — caught by a hang in
    `ChatViewModelTests.SendCommand_SecondInvocationWhileSending_IsANoOp` during apply. **Any future
    `[RelayCommand]` async method that must guarantee "at most one execution in flight" needs the same
    explicit top-of-method guard** — the `CanExecute` predicate alone is not sufficient.
25. **This codebase's first `[assembly: InternalsVisibleTo(...)]`** landed in Phase 15
    (`src/ControlDeFinanzasIA.UI/AssemblyInfo.cs`, granting `ControlDeFinanzasIA.Tests.Unit`), to
    expose `NavigationService`'s `internal` test-only constructor overload (an explicit route map) so
    `NavigationServiceTests` can drive the real `NavigateCore`/`GoBack` code paths with a recording
    `INavigationAware` test double, without changing any production view-model's behavior and without
    a headless Avalonia harness. **A reusable seam for any future test that needs to exercise
    `NavigationService` end to end** — prefer this pattern over adding new public surface area just for
    testability.
26. **A transient `DashboardViewModelTests` failure batch (12 tests) appeared in exactly one of 5
    consecutive full-suite `dotnet test` runs during Phase 16's `sdd-verify` gate re-run**, then did
    not reproduce in 4 subsequent full-suite runs nor in 2 isolated re-runs of that test class alone
    (21/21 both times). Phase 16 touches no Dashboard file, so this is not attributable to this
    phase's changes. Most likely a first-run JIT/cold-start timing sensitivity in a test that compares
    a chart point count (`PreviousMonthCommand_RecalculatesSummaryAndBothCharts`). **Not fixed in this
    phase** — flagged as a follow-up for whichever future session can dedicate time to reproduce it
    deliberately (e.g. immediately after a fresh `dotnet build`, before any other test run warms up the
    process).
27. **`SavingsPlanNarrationGuard.IsGrounded` (Phase 16) checks digit-run membership, not position** —
    a narration that recombines sent digit groups differently (e.g. emitting "345 DOP" when
    "12,345.67 DOP" was sent) is accepted, and a harmless unsent number ("3 pasos") is rejected as a
    false positive. This is a documented, accepted design cost (bounds every possible error to
    already-sent digits), not a bug — **do not "upgrade" it to a positional check** without revisiting
    the design's own accepted-cost table in `.atl/sdd/f-016p16-ai-savings-plan/design.md`. Separately,
    the allowed digit set deliberately includes the **sanitized goal name's** digit runs, not just the
    sent figures (Spec Reconciliation #1) — a goal named "Carro 2027" would otherwise reject every
    narration that names it.
28. **Phase 17's reconciler refreshes facts on UNDISMISSED rows, and the digest ignores toggles and
    dismissals — both deliberate, neither a bug.** A Near budget notice that says 85% while the budget
    has moved to 95% would be a live-data bug, so `RefreshFacts` updates fact columns without touching
    `Id`/`CreatedAt`/`ReadAt` (an already-read row is never re-badged). The digest counts from the full
    sources regardless of per-kind toggles and existing dismissals because it has its own switch
    (frequency `Off`); stacking toggle AND dismissal AND digest-off suppression would be a correctness
    risk for no benefit. Do not "fix" either.
29. **Phase 17 shell/DI footguns.** `ScopedNotificationCenter` captures `SynchronizationContext.Current`
    in its constructor to marshal `Changed` onto the UI thread, so it must be resolved on the UI thread.
    `Tests.Integration.csproj` gained a reference to the UI project in S5a (for
    `ScopedNotificationCenterTests`, which runs against a real container). The
    notification popup is a `ToggleButton` + `Popup` with two-way `IsOpen`, NOT `Button.Flyout`.
    "Overdue" is derived at display time from the stored reference date, never stored.
30. **Phase 17 undocumented design divergences (now recorded) and one dead-looking branch.** Nine
    divergences from `design.md` are listed in the spec's "Post-apply reconciliation" subsection
    (`DigestComposer` as its own class, `DigestPeriod.WindowEnd`, the `NotificationDigestNoTotals` key,
    budget-read-failure skips the digest without advancing the cursor, duplicated `RetentionDays`, etc.).
    The `budgetReadFailed` branch in `DetectNotificationsUseCase` looks unreachable because
    `GetBudgetVsActualQuery` never returns `Failure` today — it is defensive, keep it.

---

## 12. First 5 commands a new agent should run

```powershell
# From C:\Users\diego\Desktop\ControlDeFinanzasIA

# 1. Confirm the build is clean before you do anything
dotnet build -c Release

# 2. Confirm the existing 1700 tests still pass
dotnet test -c Release --no-build

# 3. Confirm CI's format gate is green
dotnet format style --verify-no-changes

# 4. Confirm the architecture rules still hold
dotnet test --no-build -c Release --filter "FullyQualifiedName~Architecture"

# 5. Phase 17 is archived; Phase 18 (export) is next. Check for in-flight work first.
Get-ChildItem .\.atl\sdd\f-018p18-export; git log --oneline -10
```

If all five succeed, the working tree is healthy. If any fail, **stop and fix before reading further code** — fixing a broken build first will save you time.
