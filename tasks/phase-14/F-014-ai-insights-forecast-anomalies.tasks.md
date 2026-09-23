# Tasks — F-014 AI Insights, Month-End Forecast, and Anomaly Detection

## Review Workload Forecast

| Field | Value |
|---|---|
| Estimated changed lines | ≈4,280 total across 10 work units (design's own per-slice estimates, generated migration files excluded) |
| 400-line budget risk | High — 7 of 10 units exceed 400 lines (median ≈450), no unit exceeds 500 |
| Chained PRs recommended | Yes |
| Suggested split | PR S1 (domain math) → PR S2 (alert entity + persistence) → PR S3 (forecast use case) → PR S4 (detection + acknowledge) → PR S5 (AI plumbing) → PR S6 (AI use cases) → PR S7 (composite + DI) → PR S8a (VM deterministic) → PR S8b (VM narration) → PR S8c (view) |
| Delivery strategy | auto-chain |
| Chain strategy | stacked-to-main |

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: High

Design's `## Recommended Slicing` mandates ten strictly-chained slices (S1–S7, then a declared
S8a/S8b/S8c split), already the authoritative plan for `sdd-apply`. This document keeps that
ordering verbatim: S1 (pure domain math) has zero dependencies and unblocks the tabular tests the
rest of the phase's algorithm work leans on; S2 (persistence) must land before S3/S4 because both
downstream use cases read/write through `IAnomalyAlertRepository`, and S2 is also the first of two
slices that can break app startup (a bad EF configuration or a snapshot drift silently poisons every
later migration), so it carries a mandatory smoke-launch. S3 and S4 are independent of each other on
paper (forecast vs. detection) but stay chained — not parallelized — because both are consumed
together by S7's composite and parallel branches would double the merge-conflict surface on
`GetDashboardInsightsQuery` for zero schedule benefit at this size. S5 (prompts, parser, cache) has
no I/O dependency beyond S1 but is kept after S4 per the design's own chain. S6 depends on S4 (needs
`AnomalyItemDto`) and S5 (needs the parser/cache) together. S7 is the second startup-risk slice — it
is the first point DI wires the whole vertical together (three deterministic use cases + two LLM use
cases + the composite + the new UI-layer formatter) — so it also carries a mandatory smoke-launch,
run *before* S8a opens. S8a is the third startup-risk slice: it is the one that changes
`DashboardViewModel`'s constructor arity, which is exactly the breaking change the design's D-13
found at three call sites (`DashboardViewModelTests.cs:330`, `NavigationServiceTests.cs:205`,
`ShellViewModelTests.cs:84`) — those three fixtures are fixed inside S8a via one shared builder, not
scattered across later slices. S8b (narration) and S8c (view) are lower-risk and stay chained last
because the View slice needs S8b's bound properties to exist first.

Apply starts only after Phase 13 is fully closed (this design's own precondition — Phase 13 is
CLOSED per `AGENTS.md`, so that gate is already satisfied). S2, S7, S8a, and S8c each get a mandatory
app smoke-launch task before their gate-run closes, because each touches DI/startup or the
dashboard's live render surface.

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|---|---|---|---|
| S1 | Domain math | PR S1 | Base = main. `AnomalySeverity`, `AnomalyDetector`, `MonthEndForecaster` — pure static, zero dependencies. Everything else depends on this. |
| S2 | Alert entity + persistence | PR S2 | Base = main (PR S1 merged). Hardened `AnomalyAlert`, repo contract + impl, config, `DbSet`, DI, migration, snapshot, snapshot-drift test. **Smoke-launch required** — can break app startup via a bad migration/model. |
| S3 | Forecast use case | PR S3 | Base = main (PR S2 merged). `GetAmountRowsAsync` + `GetMonthEndForecastUseCase`, zero `ILLMClient`. |
| S4 | Detection + acknowledge | PR S4 | Base = main (PR S3 merged). `IAnomalyMessageFormatter` port (impl ships in S7), `DetectAnomaliesUseCase`, `AcknowledgeAnomalyUseCase`. |
| S5 | AI plumbing | PR S5 | Base = main (PR S4 merged). Compile-depends only on S1's shapes; prompts, tolerant batch parser, session cache — no adapters touched (Phase 13 untouched). |
| S6 | AI use cases | PR S6 | Base = main (PR S5 merged). `GetInsightsUseCase`, `GetAnomalyNarrativeUseCase` — success-shaped `NotConfigured`/`Unavailable`, never `Result.Failure`. |
| S7 | Composite + DI | PR S7 | Base = main (PR S6 merged). `GetDashboardInsightsQuery`, `ResxAnomalyMessageFormatter`, `Bootstrap.cs` block. **Smoke-launch required** — first point the full vertical is wired into DI. |
| S8a | VM deterministic pass | PR S8a | Base = main (PR S7 merged). Breaking ctor-arity change; fixes the 3 broken fixtures via `DashboardInsightsQueryBuilder`. **Smoke-launch required.** |
| S8b | VM narration pass | PR S8b | Base = main (PR S8a merged). Non-blocking `LoadNarrationAsync`, generation-token guard, no ctor change. |
| S8c | View | PR S8c | Base = main (PR S8b merged). `DashboardView.axaml` three sections, frontend-design pass, `AGENTS.md` status line. **Smoke-launch required.** |

## 1. Domain math

Depends on nothing. Goal: the phase's entire numeric core exists as pure, static, fully
tabular-tested Domain services before any persistence or LLM code exists.

- [ ] 1.1 Write failing `Tests.Unit/Domain/Alerts/AnomalyDetectorTests.cs`: eligibility boundary at
  count 7/8 and distinct months 2/3, one-sidedness (a negative z never flags), `MAD > 0` band edges
  `[3.5,5)`/`[5,8)`/`[8,∞)` evaluated on the *rounded* score, the `MAD = 0` ratio fallback (`≥ 3×
  median` → `Warning`, never divides by zero), output ordering (`Severity` desc → `RatioToMedian` desc
  → `Date` desc → `ExpenseId` asc), and the design's worked example (`[100,100,110,90,100,120,80,100]`
  → median 100, MAD 5 → 125 none / 130 Info / 145 Warning / 160 Critical) plus its MAD=0 table (eight
  ×500 → 1500 Warning, 1499 none). (R-2, D-1, S-1, S-11)
- [ ] 1.2 Create `src/…Domain/Alerts/AnomalySeverity.cs`, `AnomalyDetector.cs` (+
  `AnomalySample`/`AnomalyCandidate`/`AnomalyFinding`/`AnomalyRule`): pure static `Detect`, decimal
  median/MAD, one-sided flagging, threshold on the rounded score, no `options` parameter (the design's
  rejected-alternative call). Run 1.1 green. (R-2, D-1)
- [ ] 1.3 Write failing `Tests.Unit/Domain/Forecasting/MonthEndForecasterTests.cs`: day-1 blend
  worked example (`(1/30)·30000 + (29/30)·1000 = 1966.67` → projected `87,033.33`), no history + day 2
  → `InsufficientData`, no history + day 3 → pure run-rate, `ClosedMonth` (`projected = spentToDate`),
  `FutureMonth` (`projected` null), last day of month (`remaining = 0` → `projected == spent`),
  negative-input guards. (R-1, D-2, S-7, S-8)
- [ ] 1.4 Create `src/…Domain/Forecasting/MonthEndForecaster.cs` (+ `ForecastState`,
  `MonthEndForecast`): `AwayFromZero` rounding, current/closed/future branches exactly per D-2. Run
  1.3 green. (R-1, D-2)
- [ ] 1.5 Run all four gates: `dotnet build -c Release`, `dotnet test -c Release --no-build`,
  `dotnet format style --verify-no-changes`, NetArchTest filter. All green before opening PR S1.

## 2. Alert entity + persistence

Depends on 1. Goal: `AnomalyAlert` hardened, persisted, idempotent, and drift-checked forever — the
first slice that can break app startup.

- [ ] 2.1 Write failing `Tests.Unit/Domain/Alerts/AnomalyAlertTests.cs`: `Create` invariants (empty
  `transactionId`/`categoryId`, undefined `Severity`, whitespace or `> 500`-char `Message` after
  trim, `detectedAtUtc == default`); `Refresh` throws `InvalidOperationException` once acknowledged;
  `Acknowledge` is idempotent (a second call keeps the first timestamp). (R-3, D-3)
- [ ] 2.2 Modify `src/…Domain/Alerts/AnomalyAlert.cs`: record stub → `sealed class`, private setters,
  `Create` factory, `Refresh`, `Acknowledge`, `IsAcknowledged`, `CategoryId`/`DetectedAt` added,
  `Date` as `DateOnly`. No FK to `Expense`. Run 2.1 green. (R-3, D-3)
- [ ] 2.3 Modify `src/…Application/Abstractions/IAnomalyAlertRepository.cs`: add `GetByMonthAsync`,
  `GetByTransactionIdsAsync`, `ApplyReconciliationAsync` + the `AnomalyReconciliationPlan` record
  (`ToAdd`/`ToUpdate`/`ToRemove`, `IsEmpty`). (R-3, D-4)
- [ ] 2.4 Create `tests/…Unit/TestDoubles/FakeAnomalyAlertRepository.cs` implementing the extended
  interface — additive, no existing test changes. (D-13)
- [ ] 2.5 Write failing `tests/…Integration/Persistence/AnomalyAlertRepositoryTests.cs`: round-trip
  `Create`/read, a unique-`TransactionId` violation returns `Result.Failure` **and** leaves the shared
  `AppDbContext` unpoisoned (a following unrelated save still succeeds — the D-4 detach-not-clear
  gotcha), `GetByMonthAsync`, `ApplyReconciliationAsync` applies all three lists in exactly one
  `SaveChangesAsync`. (R-3, D-4)
- [ ] 2.6 Create `src/…Infrastructure/Persistence/Configurations/AnomalyAlertConfiguration.cs`:
  `Severity` as `HasConversion<string>().HasMaxLength(16)` (deliberate deviation from the codebase's
  int-enum convention, per D-4), unique index on `TransactionId`, non-unique index on `Date`, no
  `HasOne<Expense>()`. (R-3, D-4)
- [ ] 2.7 Create `src/…Infrastructure/Persistence/Repositories/AnomalyAlertRepository.cs`: all reads
  `AsNoTracking`, SQL ordering only on `Date`/`Id` (never a `decimal` column), catches
  `DbUpdateException` on reconcile and detaches exactly the entries it attached
  (`Entry(x).State = Detached`, never `ChangeTracker.Clear()`) before returning
  `Result.Failure("AnomalyReconcileConflict", …)`. Run 2.5 green. (R-3, D-4)
- [ ] 2.8 Modify `src/…Infrastructure/Persistence/AppDbContext.cs`: add `DbSet<AnomalyAlert>
  AnomalyAlerts => Set<AnomalyAlert>();` (14th `DbSet`). (D-4)
- [ ] 2.9 Modify `src/…Infrastructure/Persistence/ServiceCollectionExtensions.cs`:
  `services.AddScoped<IAnomalyAlertRepository, AnomalyAlertRepository>();`. (D-4)
- [ ] 2.10 Generate the migration: `dotnet ef migrations add AddAnomalyAlerts --project
  src/ControlDeFinanzasIA.Infrastructure --startup-project src/ControlDeFinanzasIA.UI`. Hand-verify
  `AppDbContextModelSnapshot.cs` contains the `AnomalyAlert` entity block with both indexes and
  `HasConversion<string>` severity — do not trust the tool. `Down()` = `DropTable("AnomalyAlerts")`.
  (D-5)
- [ ] 2.11 Write `tests/…Integration/Persistence/ModelSnapshotConsistencyTests.cs`: the EF 8
  `IMigrationsModelDiffer` drift-detection recipe (no `HasPendingModelChanges` in EF 8) — a permanent
  regression guard, not a one-off check. Confirm it passes against 2.10's migration. Add a clean
  in-memory SQLite `MigrateAsync()` round-trip test alongside it (existing `CategoryRepositoryTests`
  pattern). (D-5)
- [ ] 2.12 Smoke-launch the app (`dotnet run --project src/ControlDeFinanzasIA.UI`) against an
  existing dev DB and a fresh DB: confirm the migration applies cleanly and
  `IAnomalyAlertRepository` resolves from DI with no startup failure.
- [ ] 2.13 Run all four gates. All green before opening PR S2.

## 3. Forecast use case

Depends on 2. Goal: the month-end forecast renders correct numbers with **zero** `ILLMClient`
dependency.

- [ ] 3.1 Write failing `tests/…Integration/Persistence/ExpenseRepositoryAmountRowsTests.cs`: the new
  `GetAmountRowsAsync` projection is `AsNoTracking`, ordered by `Date` then `Id`, correctly filtered
  by `DateRange`. (D-7)
- [ ] 3.2 Create `src/…Application/DTOs/Aggregates/ExpenseAmountRow.cs`; modify
  `IExpenseRepository.cs` / `ExpenseRepository.cs` to add `GetAmountRowsAsync(DateRange, …)`. Run 3.1
  green. (D-7)
- [ ] 3.3 Modify `tests/…Unit/TestDoubles/FakeExpenseRepository.cs`: implement
  `GetAmountRowsAsync` additively — no existing test changes. (D-7, D-13)
- [ ] 3.4 Write failing `tests/…Unit/UseCases/Dashboard/GetMonthEndForecastUseCaseTests.cs`: S-7
  (budget present → projected-vs-budget comparison via `BudgetEvaluator`), S-8 (no budget → budget
  fields null), a budget-conversion failure poisons `BudgetTotalDop` to null + sets
  `HasMissingRates` (never a silently-partial sum), spent-to-date excludes future-dated expenses,
  `FutureMonth`/`ClosedMonth` states, no-history + `elapsed < 3` → `InsufficientData`, zero-spend
  history months excluded from the trailing-3-month average. (R-1, D-6, S-7, S-8)
- [ ] 3.5 Create `src/…Application/UseCases/Dashboard/GetMonthEndForecast/{MonthEndForecastDto,
  GetMonthEndForecastUseCase}.cs`: ctor `IExpenseRepository, IBudgetRepository, ICurrencyConverter,
  IClock`; flow exactly per D-6 (future/closed branch, spent-to-date range, trailing-3-month history
  average, `MonthEndForecaster.Project`, budget conversion + `BudgetEvaluator.Evaluate`); never
  `Result.Failure` for data reasons. Run 3.4 green. (R-1, D-6)
- [ ] 3.6 Run all four gates. All green before opening PR S3.

## 4. Detection + acknowledge

Depends on 3. Goal: idempotent, currency-normalized anomaly detection that never touches an
acknowledged alert, plus its dismiss use case.

- [ ] 4.1 Create `src/…Application/Abstractions/IAnomalyMessageFormatter.cs` (+
  `AnomalyMessageFacts` record) — the port only; the UI-layer `Strings.resx` implementation ships in
  S7 (design's Spec Reconciliation #3, `IAnomalyMessageFormatter` is binding). (R-8, D-7)
- [ ] 4.2 Create `tests/…Unit/TestDoubles/FakeAnomalyMessageFormatter.cs`: returns
  `"{Category}|{Amount}|{Severity}"` so tests assert structure, not copy. (D-13)
- [ ] 4.3 Write failing `tests/…Unit/UseCases/Dashboard/DetectAnomaliesUseCaseTests.cs`: S-1 (thin
  history → no alert), S-2 (two loads → exactly one row for the `TransactionId`), S-3 (acknowledged
  alert untouched and never recreated even though its expense still scores as an outlier), a
  re-dated expense moved into the month, a deleted expense's unacknowledged alert removed, an
  unconverted (rate-outage) expense's existing alert **kept** — not removed (Spec Reconciliation
  #2), S-10 (mixed DOP/USD history normalized before scoring). (R-2, R-3, D-7, S-1, S-2, S-3, S-10)
- [ ] 4.4 Create `src/…Application/UseCases/Dashboard/DetectAnomalies/*.cs`: ctor
  `IExpenseRepository, ICategoryRepository, IAnomalyAlertRepository, ICurrencyConverter,
  IAnomalyMessageFormatter, IClock, ILogger<DetectAnomaliesUseCase>`; flow per D-7 (7-month
  `GetAmountRowsAsync` window, DOP conversion with `unconvertedExpenseIds` tracking, history/candidate
  split, `AnomalyDetector.Detect`, existing-alert union via both `GetByMonthAsync` and
  `GetByTransactionIdsAsync`, the D-7 reconciliation table, `ApplyReconciliationAsync` with
  log-and-continue on failure, return unacknowledged items in detector order). Run 4.3 green. (R-2,
  R-3, D-7)
- [ ] 4.5 Write failing `tests/…Unit/UseCases/Dashboard/AcknowledgeAnomalyUseCaseTests.cs`: unknown
  id → `Failure("NotFound")`; already-acknowledged → idempotent `Success`; happy path →
  `Acknowledge` + `UpdateAsync`. (R-3, D-7)
- [ ] 4.6 Create `src/…Application/UseCases/Dashboard/AcknowledgeAnomaly/*.cs`. Run 4.5 green. (R-3,
  D-7)
- [ ] 4.7 Run all four gates. All green before opening PR S4.

## 5. AI plumbing

Depends on 4. Goal: the tolerant batch parser, the session-only narration cache, and all 8 prompt
resources exist and are provably wired, with zero live LLM calls in this slice.

- [ ] 5.1 Write failing `tests/…Unit/Llm/AnomalyNarrationParserTests.cs`: S-6's table (3 parsed / 1
  malformed / 1 missing → a 3-entry dictionary, the other two untouched by the caller), an
  `items`-wrapped payload, a code-fenced reply, a preamble before the JSON, string-typed ids that
  parse as ints, duplicate ids (first occurrence wins), out-of-range ids ignored, message length
  capped at 280 with a `…` suffix. (R-5, D-9, S-6)
- [ ] 5.2 Create `src/…Application/Llm/AnomalyNarrationParser.cs`: static, balanced-region scanner
  (string/escape-aware, starts at the first `[` or `{`), array-or-`items`-object root, per-item
  tolerant parsing, `RegexOptions.None` + 500 ms timeout. Deliberately duplicated from
  `LlmCategoryAnswerParser` rather than shared — Phase 13's parser is out of scope (R-9). Run 5.1
  green. (R-5, R-9, D-9)
- [ ] 5.3 Write failing `tests/…Unit/UseCases/Dashboard/DashboardNarrationCacheTests.cs`: a cache hit
  makes zero recompute; only `Generated`-status results are ever stored (a fallback is never cached);
  the key includes locale/provider/model so a language or model switch does not serve stale prose;
  capacity-32 overflow clears the whole cache. (D-11)
- [ ] 5.4 Create `src/…Application/UseCases/Dashboard/Common/{NarrativeStatus,
  DashboardNarrationCache}.cs`: `ConcurrentDictionary<string, object>`, key =
  `promptName + locale + provider + model + renderedUserPrompt`. Run 5.3 green. (D-11)
- [ ] 5.5 Create 8 embedded prompt resources
  `src/…Infrastructure/Llm/Prompts/{generate-insights,explain-anomalies}.{system,user}.{es-DO,en}.txt`
  with D-14's non-negotiable content — insights: "use ONLY the figures provided, copied exactly as
  written; never compute/round/estimate/introduce a number, no markdown, no lists, no greeting";
  anomalies: "return ONLY `[{"id":n,"message":"…"}]`, one item per input id, ≤ 200 chars each, never
  invent ids". Already globbed by the existing `<EmbeddedResource>` pattern — no `.csproj` change.
  (R-4, R-5, D-14)
- [ ] 5.6 Modify `src/…Application/Llm/PromptNames.cs`: add `GenerateInsightsSystem`,
  `GenerateInsightsUser`, `ExplainAnomaliesSystem`, `ExplainAnomaliesUser`. (D-14)
- [ ] 5.7 Modify `tests/…Integration/Llm/EmbeddedPromptProviderTests.cs`: add the 4 new names to the
  hardcoded `_allPromptNames` array so the "resolves in both locales" guarantee covers them; confirm
  green. (D-14)
- [ ] 5.8 Run all four gates. All green before opening PR S5.

## 6. AI use cases

Depends on 5. Goal: both narration use cases are success-shaped for every non-happy path — not
configured, failing, or timing out never surfaces as an exception or a `Result.Failure`.

- [ ] 6.1 Modify `tests/…Unit/TestDoubles/FakeLlmClient.cs` additively:
  `Func<LlmRequest, CancellationToken, Task<LlmResponse>>? OnCompleteAsync`, used instead of
  `ResponseToReturn` when set — enables a never-completing client (via a `TaskCompletionSource`, for
  S-9) and per-prompt-content routing so one fake can answer insights and the batch differently. No
  existing test changes. (D-13)
- [ ] 6.2 Modify `tests/…Unit/TestDoubles/FakePromptProvider.cs` additively: `Dictionary<string,
  string> Templates` override consulted first; the existing two-branch fallback stays for Phase 13
  tests. (D-13)
- [ ] 6.3 Write failing `tests/…Unit/UseCases/Dashboard/GetInsightsUseCaseTests.cs`: not configured →
  zero `ILLMClient` calls + `Success(NotConfigured)` (S-4); `LlmException`/timeout/unparseable reply →
  `Success(Unavailable)` + Warning carrying a correlation id; `Skipped` when expenses and incomes are
  both 0, checked **before** settings; caller-initiated cancellation rethrows; `MaxTokens` capped at
  400; output cleanup (strip fences, trim, collapse 3+ newlines, 800-char cap); the narrative never
  introduces a number absent from the supplied `InsightFacts` (S-12). (R-4, R-6, D-8, S-4, S-5, S-12)
- [ ] 6.4 Create `src/…Application/UseCases/Dashboard/GetInsights/*.cs` (+ `InsightsNarrative`
  record): copies `CategorizeExpenseUseCase`'s guard skeleton line for line, with the one deliberate
  difference — `Result.Success(fallback)` for every non-happy path, never `Result.Failure`. Run 6.3
  green. (R-4, R-6, D-8)
- [ ] 6.5 Write failing `tests/…Unit/UseCases/Dashboard/GetAnomalyNarrativeUseCaseTests.cs`: empty
  anomaly list → `Skipped`, zero calls; not configured → zero calls + `NotConfigured`; a throwing
  client → `Unavailable` + Warning with correlation id; top-5 cap (`MaxNarratedAnomalies = 5`, items
  already detector-ordered); the ordinal-to-`AlertId` map never leaks a GUID into the prompt; a
  second identical load hits the cache with zero extra calls; a fallback result is never cached. (R-5,
  R-6, D-8, D-9, D-11, S-4, S-5, S-6)
- [ ] 6.6 Create `src/…Application/UseCases/Dashboard/GetAnomalyNarrative/*.cs` (+ `AnomalyNarration`
  record): batch prompt built with short ordinal keys and sanitized text (80-char description
  truncation, newlines/`"` stripped), `AnomalyNarrationParser` integration, `MaxTokens = 600`. Run 6.5
  green. (R-5, R-6, D-8, D-9)
- [ ] 6.7 Run all four gates. All green before opening PR S6.

## 7. Composite + DI

Depends on 6. Goal: the one new composite query exists, the deterministic step provably never
resolves an `ILLMClient`, and the whole vertical wires into DI without breaking startup.

- [ ] 7.1 Write failing `tests/…Unit/UseCases/Dashboard/GetDashboardInsightsQueryTests.cs`: an
  `ILLMClientFactory` whose `Resolve` throws does not affect `GetDeterministicAsync` (proves the
  deterministic step never even attempts to resolve a client); one throwing step (forecast, detect,
  or a facts query) does not blank the other sections — each has its own `try/catch` →
  `LogError` → null part; `GetNarrationAsync` issues at most 2 concurrent LLM calls via
  `Task.WhenAll` (D11); the deterministic step's internal queries run sequentially, not
  `WhenAll` — the shared `AppDbContext` must not see a write (detection) overlap a read. (R-7, D-10,
  S-9)
- [ ] 7.2 Create `src/…Application/UseCases/Dashboard/GetDashboardInsights/*.cs`: `InsightFacts`,
  `DashboardInsightsSnapshot`, `DashboardNarration` records; `GetDashboardInsightsQuery` with
  `GetDeterministicAsync` (forecast → detection → summary → breakdown → budget-vs-actual, in that
  order), `GetNarrationAsync` (`WhenAll` insights + anomaly narrative), `DismissAnomalyAsync`
  (delegates to `AcknowledgeAnomalyUseCase`). Run 7.1 green. (R-7, D-10)
- [ ] 7.3 Write failing `tests/…Unit/UI/Formatting/ResxAnomalyMessageFormatterTests.cs`: `Format`
  resolves `DashboardAnomalyMessageFormat` + `MoneyFormat.ApproximateDop`, pure, no LLM call. (R-8,
  D-7)
- [ ] 7.4 Add the `DashboardAnomalyMessageFormat` key to `Strings.resx`/`Strings.es-DO.resx`; create
  `src/…UI/Formatting/ResxAnomalyMessageFormatter.cs` implementing `IAnomalyMessageFormatter`. Run
  7.3 green. (R-8, D-7)
- [ ] 7.5 Modify `src/…UI/Composition/Bootstrap.cs`: add a `// Fase 14: insights, pronóstico y
  anomalías (F-014)` banner block — `AddTransient` for the three deterministic use cases, the two AI
  use cases, and the composite; `AddSingleton<DashboardNarrationCache>()`;
  `AddSingleton<IAnomalyMessageFormatter, ResxAnomalyMessageFormatter>()`. (D-14)
- [ ] 7.6 Write/extend an architecture regression assertion confirming NetArchTest R-1..R-5 (in
  particular R-4 — UI must not reference Infrastructure) stay green after the new `Bootstrap.cs`
  block and the UI-side formatter.
- [ ] 7.7 Smoke-launch the app (`dotnet run --project src/ControlDeFinanzasIA.UI`) against an
  existing dev DB to confirm no DI startup failure — this is the first point the entire phase's
  service graph is wired together.
- [ ] 7.8 Run all four gates. All green before opening PR S7.

## 8a. VM deterministic pass

Depends on 7. Goal: `DashboardViewModel` gains its one new ctor parameter, the deterministic
sections render, and the three fixtures the ctor change breaks are fixed via one shared builder —
this is the breaking-change slice.

- [ ] 8a.1 Create `tests/…Unit/TestDoubles/DashboardInsightsQueryBuilder.cs` per D-13: builds a real
  `GetDashboardInsightsQuery` from fakes (`expenseRepo, incomeRepo, converter, clock`, optional
  `budgetRepo, categoryRepo, alertRepo, llmFactory` — default **not configured**, i.e. zero calls —
  `settings`). (D-13)
- [ ] 8a.2 Modify `tests/…Unit/UI/DashboardViewModelTests.cs`: `CreateViewModel` gains optional
  `alertRepo`/`llmFactory` params forwarding into the builder; update the `new DashboardViewModel(`
  call site (line ~330) to pass the new `GetDashboardInsightsQuery` argument — mechanical
  compile-fix only, no new assertions yet. (D-13)
- [ ] 8a.3 Modify `tests/…Unit/UI/NavigationServiceTests.cs` and
  `tests/…Unit/UI/ShellViewModelTests.cs`: pass their existing repos plus the builder's defaults at
  their `new DashboardViewModel(` call sites (lines ~205, ~84) — one added line each, no other
  change. (D-13)
- [ ] 8a.4 Modify `src/…UI/ViewModels/DashboardViewModel.cs`: add the 8th ctor parameter
  `GetDashboardInsightsQuery getDashboardInsightsQuery`, positioned between `getBudgetVsActualQuery`
  and `clock` — compile-fix only, no new behavior yet. Run 8a.2/8a.3 green (fixtures compile,
  existing assertions unchanged). (R-7, D-12)
- [ ] 8a.5 Create `src/…UI/ViewModels/AnomalyListItem.cs`: `partial class : ObservableObject` with
  immutable `AlertId`/`CategoryName`/`Description`/`AmountDisplay`/`DateDisplay`/`Severity`/
  `SeverityLabel` and an observable `Message` + `IsNarrated`. (D-12)
- [ ] 8a.6 Add the deterministic-pass `Strings.resx`/`Strings.es-DO.resx` keys:
  `DashboardForecastTitle`, `DashboardForecastProjectedLabel`, `DashboardForecastSpentToDateLabel`,
  `DashboardForecastBudgetFormat`, `DashboardForecastInsufficientData`, `DashboardForecastClosedMonth`,
  `DashboardForecastFutureMonth`, `DashboardAnomaliesTitle`, `DashboardEmptyAnomaliesMessage`,
  `DashboardAnomalySeverityInfo`/`Warning`/`Critical`, `DashboardAnomalyDismissButton`,
  `DashboardSectionErrorMessage`. (R-8, D-14)
- [ ] 8a.7 Write failing tests in `tests/…Unit/UI/DashboardViewModelTests.cs`: deterministic sections
  populate the forecast + anomalies fields; S-7/S-8 budget/no-budget display; a forecast or
  detection-step failure sets `SectionErrorMessage` without touching `LoadError` or any Phase 8/10
  section; `DismissAnomalyAsync` removes the row on success and logs-only on failure; a stale
  generation (superseded by a second `LoadAsync`) drops the deterministic apply. (R-3, R-7, R-9,
  D-12, S-2, S-3, S-7, S-8)
- [ ] 8a.8 Modify `src/…UI/ViewModels/DashboardViewModel.cs`: `HasForecast`,
  `ForecastProjectedDisplay`, `ForecastSpentToDateDisplay`, `ForecastBudgetDisplay`,
  `ForecastStateMessage`, `ForecastBudgetStatus`; `Anomalies` `ObservableCollection<AnomalyListItem>`;
  `SectionErrorMessage`; `GetDeterministicAsync` awaited **after** the existing `WhenAll` block, in
  its own `try/catch`, applied only when `generation == _loadGeneration`;
  `[RelayCommand] DismissAnomalyAsync`. Run 8a.7 green. (R-3, R-7, R-9, D-12)
- [ ] 8a.9 Smoke-launch the app (`dotnet run --project src/ControlDeFinanzasIA.UI`) against an
  existing dev DB to confirm the dashboard renders the forecast/anomalies sections with no
  DI/startup failure.
- [ ] 8a.10 Run all four gates. All green before opening PR S8a.

## 8b. VM narration pass

Depends on 8a. Goal: the non-blocking narration pass loads via its own generation-guarded task, and
the deterministic sections are proven to render even against a never-completing LLM client.

- [ ] 8b.1 Add the narration-pass `Strings.resx`/`Strings.es-DO.resx` keys: `DashboardInsightsTitle`,
  `DashboardInsightsLoading`, `DashboardInsightsNoData`, `DashboardAiNotConfigured`,
  `DashboardAiUnavailable`. (R-8, D-14)
- [ ] 8b.2 Write failing tests (extend `DashboardViewModelTests.cs` or a new
  `DashboardViewModelNarrationTests.cs`): S-9 — a never-completing `FakeLlmClient.OnCompleteAsync` —
  forecast and anomalies still render within the deterministic step's normal time budget,
  `IsInsightsLoading` stays `true`, `LoadAsync` itself never awaits narration; S-5 — AI down — every
  numeric section stays intact, `InsightsStatusText`/`AnomalyNarrationStatusText` show the fallback
  labels only, a templated anomaly `Message` is never cleared; a second `LoadAsync` (e.g. via
  `NextMonthCommand`) drops the first load's stale narration via the generation token; a
  cancellation from supersede is silent (`OperationCanceledException` swallowed, no `LoadError`).
  (R-4, R-6, R-7, D-12, S-4, S-5, S-6, S-9, S-12)
- [ ] 8b.3 Modify `src/…UI/ViewModels/DashboardViewModel.cs`: `_narrationCts`; `IsInsightsLoading`,
  `InsightsText`, `InsightsStatusText`, `AnomalyNarrationStatusText`; `LoadNarrationAsync`
  (fire-and-forget from `LoadAsync`, cancels/disposes the prior CTS, applies only when
  `generation == _loadGeneration`, catches `OperationCanceledException` silently and any other
  exception into the fallback); `ApplyNarration` maps `Insights`/`Anomalies` per `NarrativeStatus`,
  never clearing a templated `Message`. Run 8b.2 green. (R-4, R-6, R-7, D-12)
- [ ] 8b.4 Run all four gates. All green before opening PR S8b.

## 8c. View

Depends on 8b. Goal: the three new dashboard sections render on tokens, with no logic in
code-behind.

- [ ] 8c.1 Invoke the `.claude/skills/frontend-design` skill for the three new sections (forecast,
  anomalies list with per-row dismiss + severity chip, insights card) before touching XAML — design
  tokens only: `SurfaceBrush` + `RadiusMedium` + `ledger-section`, `ledger-amount`, `ledger-row`;
  severity chips `Info → SageBrush`, `Warning → AmberBrush`, `Critical → TerracottaBrush`; the
  forecast-vs-budget line reuses the Phase 10 `budget-progress`/`.near`/`.exceeded` classes. (R-8,
  D-14)
- [ ] 8c.2 Modify `src/…UI/Views/DashboardView.axaml`: add the forecast section, the anomalies list
  (severity chip + dismiss button per row), and the insights card, all bound to the S8a/S8b
  ViewModel properties — no logic in code-behind. (R-8)
- [ ] 8c.3 Smoke-launch the app (`dotnet run --project src/ControlDeFinanzasIA.UI`) and visually
  confirm all three sections render with real data, both with AI unconfigured (fallback labels
  appear only in narrative spots) and, if a key is available, with AI configured — confirm no
  numeric section is ever blank or replaced by a label.
- [ ] 8c.4 Modify `AGENTS.md`: update the Status line to record Phase 14 complete, mirroring the
  existing Phase 5/6 status-line pattern.
- [ ] 8c.5 Run all four gates. All green before opening PR S8c.

## 9. Cierre

- [ ] 9.1 Run the full gate suite across the whole phase: build, `dotnet test` (unit + integration),
  `dotnet format style --verify-no-changes`, NetArchTest architecture filter. All green, no
  regressions. Confirm zero changes to Phase 13's `Infrastructure/Llm/` adapters, parser, or
  resilience configuration, and zero API-key storage change (Out of Scope, R-9).
- [ ] 9.2 Run the app for real against an existing dev DB and a fresh DB: (a) with enough expense
  history, confirm the insights card, forecast section, and anomalies list all populate; (b) clear
  the AI key / pull the network → the dashboard still shows all numeric data (forecast, anomalies
  with templated messages, every Phase 8/10 section), "IA no disponible"/"IA no configurada" appear
  only in narrative spots; (c) dismiss an anomaly and reload → it stays dismissed; (d) confirm no
  exception reaches the UI in any of the above.
- [ ] 9.3 Update `docs/architecture.md` with a Phase 14 entry following Phases 9–13's pattern.
- [ ] 9.4 Create `spec/phases/phase-14/features.md` following the pattern of
  `spec/phases/phase-10/` through `phase-13/`.
- [ ] 9.5 Archive the spec (`F-014-ai-insights-forecast-anomalies.spec.md` → `.done.md`) and write
  `verify-report.md`/`archive-report.md` in `.atl/sdd/f-014p14-ai-insights/`, independently
  re-verifying every Acceptance Criteria checklist line against source and tests before archiving —
  including the five accepted Reconciliation points (`IAnomalyMessageFormatter` port, kept-not-removed
  alerts on conversion failure, severity-then-ratio ordering, string-persisted `Severity`, the extra
  forecast states).
- [ ] 9.6 Update `docs/roadmap.md` §Fase 14: mark the phase's criteria complete, explicitly noting
  Phase 17 (notifications) as the documented consumer of the persisted `AnomalyAlert` rows this phase
  produces.
- [ ] 9.7 Update `docs/AI_HANDOFF.md` with the Phase 14 summary — including the D-9 severity-as-string
  deviation from the codebase's int-enum convention, so a future reader does not "fix" it.
