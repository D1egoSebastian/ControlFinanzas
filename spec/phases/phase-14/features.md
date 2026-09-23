# Phase 14 — AI Insights, Month-End Forecast, and Anomaly Detection

> **Phase:** 14 — IA: insights, predicción de fin de mes, anomalías
> **Status:** ✅ Completed (2026-09-23) — see `spec/features/phase-14/F-014-ai-insights-forecast-anomalies.done.md`
> **Spec ID:** F-014
> **Dependencies:** Phase 13 ✅ (AI pipeline — `ILLMClient`/`ILLMClientFactory`/`IPromptProvider` reused
> unmodified), Phase 8 ✅ (dashboard composition), Phase 10 ✅ (budgets, for the forecast-vs-budget
> comparison), Phase 5 ✅ (`ICurrencyConverter`, for DOP normalization)
> **Blocks:** Phase 17 (notifications) consumes the persisted `AnomalyAlert` rows this phase produces
> **Roadmap:** `docs/roadmap.md` §Fase 14
> **Stack:** .NET 8 · Avalonia 11.2.7 (MVVM strict) · EF Core 8 + SQLite · reuses Phase 13's
> `ILLMClient`/resilience pipeline unmodified

## 1. Summary

Phase 14 gives the dashboard three AI-facing sections — a month-end forecast, persisted anomaly
alerts, and an AI-written insight summary — while keeping every number deterministic. Two new pure
Domain services (`AnomalyDetector`, a robust median/MAD z-score detector; `MonthEndForecaster`, a
run-rate/history blend) compute every figure with **zero** `ILLMClient` dependency; the LLM only
narrates figures it is handed, via two success-shaped use cases that never fail — not-configured,
offline, and every AI error all resolve to a fallback payload plus a correlation-id Warning log,
never an exception or `Result.Failure`. One new composite Application query
(`GetDashboardInsightsQuery`) splits a deterministic step (forecast + detected/reconciled anomalies)
from a non-blocking narration step (insights + one batched anomaly-narration call), so the dashboard's
numeric sections render immediately regardless of AI state.

Confirmed product decisions carried through unchanged: one batched LLM call per load narrates all
detected anomalies (D1); the deterministic sections never await the narration step (D2); persistence
is idempotent via a unique index on `TransactionId`, reconciled on every load, and an acknowledged
alert is never touched again (D8); at most 2 LLM calls per load, run concurrently (D11); `AnomalyAlert.Message`
is always the deterministic templated text — LLM narration is display-only and never persisted (D9).

## 2. Model

```
Domain/Alerts/
  AnomalyDetector                       — pure static, robust z-score (0.6745·(x−median)/MAD),
                                           eligibility gate (count>=8 && distinctMonths>=3 && median>0),
                                           MAD=0 ratio fallback (never divides by zero), one-sided,
                                           threshold on the rounded score
  AnomalyAlert                          — sealed class, private setters, validating Create factory,
                                           Refresh (throws if acknowledged), idempotent Acknowledge,
                                           no FK to Expense
  AnomalySeverity                       — Info/Warning/Critical, persisted as string (D-4 deviation
                                           from the codebase's int-enum convention — Phase 17 contract)

Domain/Forecasting/
  MonthEndForecaster                    — pure static, run-rate/trailing-3-month blend, explicit
                                           states (Projected/InsufficientData/ClosedMonth/FutureMonth)

Application/UseCases/Dashboard/
  GetMonthEndForecast/GetMonthEndForecastUseCase   — zero ILLMClient; budget comparison via
                                                       BudgetEvaluator when a budget exists
  DetectAnomalies/DetectAnomaliesUseCase           — zero ILLMClient; idempotent reconcile
                                                       (add/update/remove/keep-on-conversion-failure)
  AcknowledgeAnomaly/AcknowledgeAnomalyUseCase     — dismiss, idempotent
  GetInsights/GetInsightsUseCase                   — 2-4 sentence narrative from supplied figures
                                                       only; success-shaped for every non-happy path
  GetAnomalyNarrative/GetAnomalyNarrativeUseCase   — ONE batched call, top 5 by score, ordinal keys
                                                       (GUID never sent to the model)
  GetDashboardInsights/GetDashboardInsightsQuery   — the one new composite: GetDeterministicAsync
                                                       (sequential, zero LLM) / GetNarrationAsync
                                                       (WhenAll, <=2 concurrent calls) / DismissAnomalyAsync

Application/Llm/
  AnomalyNarrationParser                — tolerant batch parser (independent from Phase 13's
                                           LlmCategoryAnswerParser per R-9), balanced-region scanner,
                                           array-or-items-wrapped root, first-occurrence-wins on
                                           duplicate ids, 280-char cap

Application/UseCases/Dashboard/Common/
  DashboardNarrationCache               — session-only ConcurrentDictionary, only Generated results
                                           cached, key includes locale/provider/model

Infrastructure/Persistence/
  AnomalyAlertConfiguration             — Severity as TEXT, unique index on TransactionId, non-unique
                                           index on Date, no HasOne<Expense>()
  AnomalyAlertRepository                — atomic ApplyReconciliationAsync (1 SaveChangesAsync);
                                           DbUpdateException → detach-only-attached-entries, never
                                           ChangeTracker.Clear()
  Migrations/AddAnomalyAlerts           — additive, clean Down() = DropTable

Infrastructure/Llm/Prompts/
  generate-insights.{system,user}.{es-DO,en}.txt, explain-anomalies.{system,user}.{es-DO,en}.txt
                                         — 8 new embedded resources; non-negotiable "never introduce a
                                           number" / "never invent an id" rules in both locales

UI/
  Formatting/ResxAnomalyMessageFormatter  — implements IAnomalyMessageFormatter over Strings.resx,
                                             resolves the R-8-vs-D9 tension (Spec Reconciliation #3)
  ViewModels/DashboardViewModel           — one new 8th ctor param, deterministic apply after the
                                             existing WhenAll, non-blocking LoadNarrationAsync guarded
                                             by a generation token + its own CancellationTokenSource
  ViewModels/AnomalyListItem              — immutable row facts + observable Message/IsNarrated
  Views/DashboardView.axaml               — forecast section, anomalies list (severity chip + dismiss),
                                             insights card
```

## 3. Slice plan

| ID | Slice | Likely PR | Status |
| --- | --- | --- | --- |
| S1 | Domain math (`AnomalyDetector`, `MonthEndForecaster`) | PR S1 | ✅ Completed |
| S2 | Alert entity + persistence | PR S2 | ✅ Completed |
| S3 | Forecast use case | PR S3 | ✅ Completed |
| S4 | Detection + acknowledge | PR S4 | ✅ Completed |
| S5 | AI plumbing (prompts, parser, cache) | PR S5 | ✅ Completed |
| S6 | AI use cases | PR S6 | ✅ Completed |
| S7 | Composite + DI | PR S7 | ✅ Completed |
| S8a | VM deterministic pass | PR S8a | ✅ Completed |
| S8b | VM narration pass | PR S8b | ✅ Completed |
| S8c | View | PR S8c | ✅ Completed |

Applied in 10 chained commits: `a4f6002` (S1 domain math), `cb3867d` (S2 alert entity + persistence),
`eef654e` (S3 forecast use case), `b827a10` (S4 detection + acknowledge), `cc8d123` (S5 AI plumbing),
`7819575` (S6 AI use cases), `325cae4` (S7 composite + DI), `fd6c891` (S8a VM deterministic pass),
`1ae4b3b` (S8b VM narration pass), `497b9f1` (S8c view). Every slice was independently re-verified by
the orchestrator after landing, not just self-reported by the implementing agent. S2, S7, S8a, and S8c
each carried a mandatory app smoke-launch before their gate-run closed, per the design's own risk
assessment (S2/S7 can break DI/startup; S8a changes `DashboardViewModel`'s constructor arity; S8c is
the live dashboard render surface). No fix commit was required — the `sdd-verify` adversarial
reliability+risk review pass over the full phase diff found no confirmed defects.

## 4. Phase completion criteria

- **(roadmap)** The dashboard shows insights, forecast, and anomalies when there is enough history. ✅
  — verified source-level: `DashboardView.axaml`'s three new sections bind to
  `DashboardViewModel`'s S8a/S8b properties, populated by `GetDashboardInsightsQuery.GetDeterministicAsync`
  (forecast + anomalies, zero LLM) and `GetNarrationAsync` (insights + anomaly narration, non-blocking).
- **(roadmap)** If the AI does not respond, the dashboard shows "IA no disponible" without losing
  numeric data. ✅ — `GetInsightsUseCase`/`GetAnomalyNarrativeUseCase` are success-shaped for every
  non-happy path (not configured, `LlmException`, timeout, unparseable reply all resolve to a fallback
  payload, never `Result.Failure` or an exception); the fallback labels write only to
  `InsightsStatusText`/`AnomalyNarrationStatusText`, never to a numeric-bearing property.
- `AnomalyDetector`/`MonthEndForecaster` are pure static Domain services with zero `ILLMClient`
  dependency, tabular-tested against thresholds, `MAD = 0`, minimum history, and one-sidedness. ✅
- `AnomalyAlert` persistence is idempotent (unique index on `TransactionId`), reconciled on every load,
  and an acknowledged alert is never modified or recreated. ✅
- At most 2 LLM calls per dashboard load (insights + one batched anomaly-narration call), issued
  concurrently. ✅
- A partially unparseable narration batch keeps the deterministic template for unresolved items. ✅
- No hardcoded user-facing strings; new copy resolves through `Strings.resx`/`Strings.es-DO.resx`. ✅
- Zero changes to Phase 13's `ILLMClient`/adapters/parser/resilience configuration or to API-key
  storage. ✅ — confirmed via `git diff 19569d2..HEAD -- src/`: no file under Phase 13's
  `Infrastructure/Llm/` changed except the 8 new, additive `.txt` prompt resources.
- Full test suite green, no regressions (1344/1344: 1079 unit + 265 integration, up from 1184/1184 at
  Phase 13's close). ✅

## 5. Findings carried forward, not fixed in this phase

- **`AnomalySeverity` is persisted as a string column**, a deliberate deviation from the codebase's
  8-configuration int-enum convention (`Debt`, `Budget`, `Goal`, etc.). Documented inline in
  `AnomalyAlertConfiguration.cs`: this table is Phase 17's notification contract, and a string survives
  enum reordering and is readable in a raw DB inspection. Not expected to be "fixed" back to int —
  future readers should treat this as intentional, not drift.
- **`IAnomalyMessageFormatter` lives in Application but is implemented in UI** (`ResxAnomalyMessageFormatter`)
  over `Strings.resx`. This resolves a real tension between R-8 ("no hardcoded UI strings") and D9
  ("the persisted alert message is a deterministic template, never LLM output") — the persisted text is
  localized at detection time via a UI-side port, and a later locale change does not retroactively
  rewrite already-stored alert messages (accepted v1 limitation, carried from the design's own Spec
  Reconciliation #1).
- **The dashboard month label still renders in English** ("September 2026") under the `es-DO` locale.
  Pre-existing, **not introduced by this phase** — Phase 12 only sets `CurrentUICulture` from the
  persisted locale, not `CurrentCulture`, so `DateTime`-formatting APIs that read `CurrentCulture`
  (month names) fall back to the OS/thread default. Accepted Phase 12 limitation, re-surfaced here
  because it is visible on the same screen this phase modifies. Follow-up, not a Phase 14 defect.
- **A possible sidebar highlight mismatch was observed in a screenshot during this phase's own
  smoke-launch checks.** Unconfirmed — may simply be mouse hover state captured mid-screenshot rather
  than a real navigation-highlight bug. Not reproduced deliberately; flagged as a follow-up to check
  with a clean screenshot (mouse away from the sidebar) rather than asserted as a defect.
- **Roadmap criterion "the dashboard shows insights... when there is enough history" is gated on a
  deterministic fake `ILLMClient` in tests, not a live model**, same caveat as Phase 13. Prompt tuning
  against real-model responses for the insights/anomaly-narration prompts is a follow-up, not a phase
  blocker — carried forward alongside Phase 13's equivalent finding for the categorization prompt.
- **No headless Avalonia test harness still exists** (same gap Phase 8/12/13 recorded). Not newly
  introduced by this phase.

## 6. References

- `docs/roadmap.md` §Fase 14 — original plan, now marked complete
- `spec/features/phase-14/F-014-ai-insights-forecast-anomalies.done.md` — archived formal spec,
  evidence per acceptance criterion
- `.atl/sdd/f-014p14-ai-insights/{exploration,proposal,design,verify-report,archive-report}.md` — full
  traceability
- `docs/architecture.md` — Phase 14 entry
- `spec/features/phase-13/F-013-ai-pipeline-categorization.done.md` — Phase 13, the AI pipeline this
  phase reuses unmodified
- `spec/features/phase-10/F-010-monthly-budgets.done.md` — Phase 10, the budget comparison this phase's
  forecast reuses via `BudgetEvaluator`
