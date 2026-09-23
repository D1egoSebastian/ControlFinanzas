# F-014 — AI Insights, Month-End Forecast, and Anomaly Detection

> **Phase:** 14 — IA: insights, predicción de fin de mes, anomalías
> **Status:** Draft — ready for design
> **SDD:** `.atl/sdd/f-014p14-ai-insights/`
> **Input:** `.atl/sdd/f-014p14-ai-insights/proposal.md`, `.atl/sdd/f-014p14-ai-insights/exploration.md`
> **Depends on:** Phase 13 (AI pipeline — must be CLOSED before apply), Phase 8 (dashboard), Phase 10 (budgets), Phase 5 (`ICurrencyConverter`)
> **Blocks:** Phase 17 (notifications) consumes the persisted `AnomalyAlert` rows this phase produces

## Goal

Give the dashboard three AI-facing sections — a month-end forecast, persisted
anomaly alerts, and an AI-written insight summary — while keeping every
number deterministic. The deterministic layer computes every figure; the LLM
only narrates figures it is handed. If AI is not configured, offline, or
fails, the dashboard stays fully numeric and only the narrative spots show a
fallback label.

### Confirmed product decisions

All decisions below are binding (D1–D12 from the proposal) and are not
reopened here:

- **Narration call shape (D1):** one batched LLM call per dashboard load
  narrates all detected anomalies; unparsed items keep their template.
- **Dashboard composition (D2):** one new composite Application query, one
  new `DashboardViewModel` ctor parameter. Deterministic results (forecast,
  anomalies) never wait on the LLM; narration loads in a separate
  non-blocking pass.
- **`AnomalyAlert.Severity` (D3):** becomes `AnomalySeverity { Info, Warning,
  Critical }`.
- **Anomaly granularity (D4):** per-expense outlier vs. its category
  history. No category-month spike detector in v1.
- **Detection algorithm (D5):** robust (median-based) z-score,
  `0.6745·(x−median)/MAD`, over DOP-normalized amounts. Flag at `≥ 3.5`.
- **Minimum history (D6):** ≥ 3 distinct prior months AND ≥ 8 prior expenses
  in the trailing 6 full months (current month excluded) before a category
  is eligible for detection.
- **Multi-currency (D7):** all detector/forecaster inputs normalized to DOP
  via `ICurrencyConverter.ConvertAsync`.
- **Trigger and persistence (D8):** detection runs inside the composite's
  deterministic step, is idempotent (unique index on `TransactionId`), and
  reconciles unacknowledged alerts on every load. Acknowledged alerts are
  never touched or recreated.
- **What gets persisted (D9):** `AnomalyAlert.Message` is always the
  deterministic templated text; LLM narration is display-only, in memory,
  never saved.
- **Forecast method (D10):** current month only, blending month-to-date pace
  with the trailing 3 complete months, compared against budgets when
  present.
- **Call limits (D11):** at most 2 LLM calls per load, run concurrently. Top
  5 anomalies by score are narrated; the rest keep their template.
- **Batch contract and cache (D12):** ordinal keys, tolerant parser, in-memory
  session cache keyed by (month, input-figure fingerprint) — not durable.

## Requirements

### R-1 — Deterministic month-end forecast

The system MUST compute the current month's end-of-month projection with
**zero** `ILLMClient` dependency, via `GetMonthEndForecastUseCase` and a pure
static `MonthEndForecaster` domain service. `projected = spentToDate +
remainingDays × dailyRate`, where `dailyRate = w·runRate + (1−w)
·historicalDailyAvg` and `w = elapsedDays/daysInMonth`. `historicalDailyAvg`
MUST come from the trailing 3 complete months; with no history it MUST fall
back to pure run-rate. If `elapsedDays < 3` and there is no history, the
result MUST expose an explicit "insufficient data" state rather than a
misleading number. When the current month has one or more budgets (Phase 10),
the result MUST also include a projected-vs-total-budget comparison. All
inputs MUST be DOP-normalized via `ICurrencyConverter`.

### R-2 — Deterministic anomaly detection

The system MUST detect per-expense outliers with a pure static
`AnomalyDetector` domain service, with **zero** `ILLMClient` dependency. For
each candidate expense, the score is the modified z-score
`0.6745·(x − median)/MAD` computed over that expense's category history,
DOP-normalized. A category is eligible for detection only with ≥ 3 distinct
prior months of expenses AND ≥ 8 prior expenses in the trailing 6 full months
(current month excluded); otherwise no alert is produced for that category.
Expenses scoring `≥ 3.5` MUST be flagged, with severity `3.5–5 → Info`,
`5–8 → Warning`, `≥ 8 → Critical`. When `MAD = 0`, the system MUST fall back
to a ratio rule (`x ≥ 3× median` → `Warning`) instead of dividing by zero.

### R-3 — `AnomalyAlert` persistence, idempotency, and dismissal

`AnomalyAlert` MUST be hardened into a `sealed class` with private setters, a
validating `Create(...)` factory, an `Acknowledge(DateTime)` method, and
`Severity` typed as `AnomalySeverity`. Detection MUST run inside the
composite query's deterministic step for the selected month via a dedicated
`DetectAnomaliesUseCase`, backed by a unique index on `TransactionId` and
supported by a repository, EF configuration, `DbSet<AnomalyAlert>`, and an
additive migration. On every load the system MUST reconcile unacknowledged
alerts for that month against the detector's current output — adding
newly-qualifying alerts and removing ones that no longer qualify or whose
expense was deleted. Acknowledged (dismissed) alerts MUST NOT be modified or
recreated by any later reconciliation, even if the same expense would still
score as an outlier. `AnomalyAlert` MUST NOT hold a foreign key to `Expense`.
`AnomalyAlert.Message` MUST always store the deterministic templated text —
never LLM output.

### R-4 — Insights narration from supplied figures only

`GetInsightsUseCase` MUST produce a 2–4 sentence narrative built exclusively
from figures already computed by existing dashboard queries (month summary,
category breakdown, budget vs. actual) and passed into the prompt. It MUST
follow Phase 13's `CategorizeExpenseUseCase` guard pattern
(`ISettingsStore` → `ILLMClientFactory.Resolve`): when AI is not configured,
it MUST return `Result.Success` with a fallback payload and make **no**
HTTP call. Any AI failure (`LlmException` or unparseable reply) MUST be
caught and converted to `Result.Success` with the fallback payload plus a
correlation-id Warning log — never a thrown exception or `Result.Failure` for
"not configured". The narrative MUST NOT introduce, recompute, or "correct"
any number not present in the supplied figures.

### R-5 — Batched anomaly narration with tolerant parsing

`GetAnomalyNarrativeUseCase` MUST issue exactly ONE batched LLM call per
dashboard load, narrating the top 5 anomalies by score; anomalies beyond the
top 5 MUST keep their deterministic template with no narration attempt.
Anomalies MUST be sent with short ordinal keys (`1..n`), and the expected
reply shape is `[{"id":1,"message":"..."}]`. The parser MUST strip code
fences and preambles, accept an `items`-wrapped payload, ignore unknown or
duplicate ids, and cap each message at 280 characters. Any item the parser
cannot resolve MUST keep its original deterministic template `Message`
unaffected — a partially malformed batch MUST NOT be treated as a failure of
the whole call.

### R-6 — Degraded mode (AI not configured, offline, or failing)

When AI is not configured, the system MUST make **zero** `ILLMClient` /
HTTP calls across both `GetInsightsUseCase` and `GetAnomalyNarrativeUseCase`,
and both MUST return `Result.Success` with their fallback payload. When an AI
call throws or times out, the system MUST catch it and return
`Result.Success` with the fallback payload plus a Warning log carrying a
correlation id — no exception MUST reach the UI. Regardless of AI state, the
forecast section, the anomalies list (with templated messages), and every
pre-existing Phase 8/10 dashboard section MUST remain fully populated with
real numbers. The labels "IA no disponible" / "IA no configurada" MUST
appear **only** in narrative-only UI spots (the insights card, the
anomaly-narration text), and MUST NEVER replace or blank out a numeric value.

### R-7 — Dashboard composition (non-blocking narration)

The system MUST expose exactly ONE new composite Application query
(`GetDashboardInsightsQuery`) with two separately awaitable operations: a
deterministic step (forecast + detected anomalies) and a narration step
(insights + batched anomaly narration). `DashboardViewModel` MUST gain
exactly one new constructor parameter for this composite. `LoadAsync` MUST
populate the deterministic sections without awaiting the narration step.
Narration MUST load via a separate, non-blocking `LoadNarrationAsync` pass
that mirrors the existing `LoadUpcomingPaymentsAsync` pattern, including
dropping stale results with the existing generation-token mechanism. At most
2 LLM calls MUST occur per dashboard load (insights + one narration batch),
issued concurrently.

### R-8 — Localization

All new user-facing copy — the insights card, forecast section, anomalies
list, and every AI-fallback label — MUST resolve through `Strings.resx` /
`Strings.es-DO.resx` with hand-added `Strings.Designer.cs` accessors. No
hardcoded user-facing string literals are introduced.

### R-9 — Explicit scope exclusions

The following are explicit non-requirements for this phase, not omissions:

- Chat (Phase 15), savings-plan narration (Phase 16), and notification
  delivery (Phase 17) MUST NOT be implemented here.
- A dedicated all-anomalies screen/route, or acknowledge/dismiss UI beyond a
  per-row dismiss, MUST NOT be built.
- Durable caching or cache tables MUST NOT be introduced — the narration
  cache (D12) MUST remain in-memory, per session only.
- Phase 13's categorization use case, parser, adapters, and resilience
  configuration MUST NOT be modified.
- API-key storage (ADR-008/011) MUST NOT change.
- Income anomalies, category-month spike detection, and folding upcoming
  recurring payments into the forecast MUST NOT be implemented.
- Streaming, conversation history, and prompt caching MUST NOT be added.

## Scenarios

### S-1 — Thin history produces no anomaly

- GIVEN a category with fewer than 3 distinct prior months of expenses (or
  fewer than 8 prior expenses in the trailing 6 full months)
- WHEN an expense in that category is evaluated during dashboard load
- THEN no `AnomalyAlert` is created for that expense, regardless of how far
  its amount deviates from the category's few data points

### S-2 — Clear outlier is persisted once across two loads

- GIVEN a category with sufficient eligible history and one new expense
  scoring `≥ 3.5` against that history
- WHEN the dashboard loads twice in a row for the same month
- THEN exactly one `AnomalyAlert` row exists for that `TransactionId` after
  both loads — the unique index and reconciliation prevent a duplicate

### S-3 — Dismissed alert is never recreated

- GIVEN an `AnomalyAlert` that has been acknowledged (`AcknowledgedAt` set)
  for an expense that still scores as an outlier
- WHEN the dashboard reloads for that month
- THEN the acknowledged alert is left untouched and no new alert is created
  for the same `TransactionId`

### S-4 — AI not configured makes zero HTTP calls

- GIVEN `ISettingsStore.GetAppSettingsAsync()` returns no configured AI
  provider/key
- WHEN the dashboard's narration step runs (`GetInsightsUseCase` and
  `GetAnomalyNarrativeUseCase`)
- THEN both return `Result.Success` with their fallback payload, and the
  fake `ILLMClient` used in the test records zero invocations

### S-5 — AI call failure leaves numeric data intact

- GIVEN a configured AI provider whose client throws `LlmException` on every
  call
- WHEN the dashboard loads
- THEN the forecast section, the anomalies list (with templated messages),
  and all other numeric dashboard sections render normally, only the
  insights card and anomaly narration show the AI-unavailable fallback, and
  no exception reaches the `DashboardViewModel`

### S-6 — Partially unparseable batch keeps template for unparsed items

- GIVEN 5 anomalies sent for narration and an LLM reply that contains valid
  JSON for 3 ordinal ids, malformed content for a 4th, and no entry at all
  for the 5th
- WHEN `GetAnomalyNarrativeUseCase` parses the reply
- THEN the 3 successfully parsed anomalies show LLM-narrated text, and the
  other 2 keep their original deterministic templated `Message` unchanged

### S-7 — Forecast with a budget present shows the budget delta

- GIVEN the current month has at least one active budget (Phase 10)
- WHEN `GetMonthEndForecastUseCase` runs
- THEN the result includes a projected-vs-total-budget comparison alongside
  the plain month-end projection

### S-8 — Forecast without a budget omits the budget comparison

- GIVEN the current month has no active budget
- WHEN `GetMonthEndForecastUseCase` runs
- THEN the result contains the plain month-end projection only, with no
  budget-comparison field populated

### S-9 — Deterministic sections never wait on the LLM

- GIVEN a fake `ILLMClient` whose calls never complete (simulating a hung
  network request)
- WHEN the dashboard loads
- THEN the forecast and anomalies sections render within the deterministic
  step's normal time budget, independent of the still-pending narration task

### S-10 — Multi-currency expenses are normalized before detection

- GIVEN a category history mixing DOP and USD expenses
- WHEN `AnomalyDetector` evaluates a new expense in that category
- THEN all amounts (history and candidate) are converted to DOP via
  `ICurrencyConverter.ConvertAsync` before the z-score is computed

### S-11 — `MAD = 0` triggers the ratio fallback

- GIVEN a category history where the median absolute deviation is 0 (e.g.
  identical historical amounts)
- WHEN a new expense at 3× or more of the median is evaluated
- THEN the expense is flagged as `Warning` via the ratio rule, without
  dividing by zero

### S-12 — Insights narrative never introduces numbers not supplied

- GIVEN a fixed set of dashboard figures (totals, category breakdown, budget
  vs. actual) passed to `GetInsightsUseCase`
- WHEN the LLM returns a narrative response
- THEN every number that appears anywhere in the rendered dashboard comes
  from the DTOs computed by deterministic queries, never from parsing or
  trusting a number inside the LLM's narrative text

## Reconciliation with design (2026-09-23, orchestrator)

The design's Spec Reconciliation section was reviewed and all five points
are accepted as binding refinements of this spec:

1. **R-8 vs stored template message:** the deterministic anomaly message is
   produced through an Application port `IAnomalyMessageFormatter`,
   implemented in the UI layer over `Strings.resx`. The message is
   localized at detection time; a later locale change does not rewrite
   already-stored messages (accepted v1 limitation).
2. **R-3 narrowed:** an existing alert whose expense fails currency
   conversion on the current load is kept, not removed.
3. **R-5 "top by score":** ordering is by severity, then ratio-to-median
   (the z-score is undefined under the MAD = 0 rule).
4. **`AnomalyAlert.Severity` persisted by enum name** (string column),
   matching the Phase 12 settings precedent of name-based enum storage.
5. **Forecast states:** the forecaster also exposes explicit states for a
   closed month and a future month (superset of R-1).

## Out of Scope

Chat (Phase 15), savings-plan narration (Phase 16), notification delivery
(Phase 17), a dedicated all-anomalies screen or route, acknowledge UI beyond
a per-row dismiss, durable caching or cache tables, any change to Phase 13's
categorization pipeline/parser/adapters/resilience, any API-key storage
change (ADR-008/011), income anomalies, category-month spike detection,
folding upcoming recurring payments into the forecast, streaming,
conversation history, and prompt caching.

## Acceptance Criteria

- [ ] With enough history, the dashboard shows the insights card, the
  month-end forecast section, and the anomalies list.
- [ ] AI down or not configured → the dashboard still shows all numeric data
  (forecast, anomalies with templated messages, all Phase 8/10 sections) and
  shows "IA no disponible" / "IA no configurada" only in narrative spots.
- [ ] Forecast and anomalies render without waiting on any LLM call
  (asserted with a never-completing fake `ILLMClient`).
- [ ] AI not configured → zero `ILLMClient` calls. Every AI failure →
  `Result.Success` with fallback plus a Warning log with correlation id. No
  exception reaches the UI.
- [ ] At most 2 LLM calls per dashboard load; anomaly narration is one
  batched call narrating the top 5 by score.
- [ ] A partially parsed batch narrates the parsed items; unparsed items keep
  their templated `Message`.
- [ ] No anomaly is flagged for a category below the minimum-history
  threshold. Detection is idempotent; acknowledged alerts are never
  recreated.
- [ ] Persisted `AnomalyAlert.Message` is always the deterministic template.
- [ ] `AnomalyAlert` is hardened (class, private setters, `Create`,
  `Severity` enum). The `AddAnomalyAlerts` migration and
  `AppDbContextModelSnapshot.cs` are consistent.
- [ ] `AnomalyDetector` and `MonthEndForecaster` are pure static services
  with tabular tests covering thresholds, `MAD = 0`, minimum history, and
  currency normalization.
- [ ] Forecast blend, day-1 behavior, no-history fallback, and
  budget/no-budget paths are covered by tests.
- [ ] No hardcoded UI strings; `Strings.Designer.cs` accessors added by hand.
- [ ] Build clean, full suite green, `dotnet format style --verify-no-changes`
  exit 0, NetArchTest R-1..R-5 green.
