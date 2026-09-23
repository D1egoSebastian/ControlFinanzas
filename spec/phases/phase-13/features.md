# Phase 13 — AI Pipeline + Automatic Expense Categorization

> **Phase:** 13 — Pipeline de IA + categorización automática
> **Status:** ✅ Completed (2026-09-23) — see `spec/features/phase-13/F-013-ai-pipeline-categorization.done.md`
> **Spec ID:** F-013
> **Dependencies:** Phase 12 ✅ (`ISettingsStore`/`AppSettings` supply provider, key, model, base URL,
> temperature, max tokens — read-only in this phase)
> **Blocks:** Phase 14 (insights/forecast/anomalies reuse `ILLMClient`)
> **Roadmap:** `docs/roadmap.md` §Fase 13
> **Stack:** .NET 8 · Avalonia 11.2.7 (MVVM strict) · `Microsoft.Extensions.Http.Resilience` (Polly) ·
> xUnit + FluentAssertions

## 1. Summary

Phase 13 gives the app its first outbound network dependency — an `ILLMClient` abstraction, two
provider adapters (`OpenAICompatibleClient` for OpenAI/OpenRouter/Custom, `AnthropicClient` for
Anthropic), and a resilience layer — and spends it on the single smallest, most valuable AI feature:
typing an expense description suggests an existing category the user can accept with one deliberate
click. Every failure mode (no key, no network, timeout, bad key, unparseable response, no matching
category) leaves the expense form fully usable, with an honest, non-blocking status label. The AI
proposes; it never decides — accepting the suggestion chip is the only code path in the entire feature
that writes `SelectedCategoryId` from AI output.

Confirmed product decisions carried through unchanged: dismissable chip with explicit accept only
(D1); 600ms debounce / 4-char minimum plus four hard call guards, at most one request in flight (D2);
a tolerant parser with a fixed pipeline where no-match is a success, not a failure (D3); fixed
resilience numbers — not framework defaults (D4); `OpenAICompatibleClient` built and externally tested
first against OpenRouter's free tier (D5).

## 2. Model

```
Application/Abstractions/Llm/
  ILLMClient, ILLMClientFactory         — CompleteAsync (throws LlmException), TestConnectionAsync
                                           (never throws), Resolve/ResolveDiagnostic(AppSettings)
  LlmRequest, LlmMessage, LlmResponse   — sealed records, zero System.Net.* references (enforced by
                                           LlmContractPurityTests, stricter than NetArchTest's prefix)
  LlmException, LlmFailureKind          — Transport/Timeout/InvalidCredentials/RateLimited/
                                           ServerError/Malformed/CircuitOpen
  LlmConnectionOutcome, LlmConnectionStatus — 3-state Ok/InvalidCredentials/Unavailable (documented
                                           deviation from architecture.md's original Task<bool>)
  LlmClientResolution, LlmConfigurationStatus — Configured/MissingApiKey/MissingBaseUrl
  IPromptProvider                       — resolves prompt templates by name + locale

Application/Llm/
  PromptNames                           — CategorizeExpenseSystem/CategorizeExpenseUser constants
  LlmCategoryAnswerParser               — tolerant 4-step pipeline: trim/de-fence → balanced-brace
                                           JSON extraction → bare-string fallback → Unparseable
  CategoryNameNormalizer                — Clean (trim/collapse/strip punctuation), Fold (Unicode NFD,
                                           diacritics stripped, upper-invariant) — two-tier matching

Application/UseCases/Expenses/CategorizeExpense/
  CategorizeExpenseUseCase              — not sealed, ExecuteAsync virtual (deliberate, for slice 6's
                                           fake); forced Temperature=0.0; MaxTokens=min(setting,128);
                                           4/200-char guards; correlation-id Guid; no-match is Success

Application/UseCases/Settings/TestAiConnection/
  TestAiConnectionUseCase               — ResolveDiagnostic (no retry/breaker pipeline); all 4 outcomes
                                           (Ok/NotConfigured/InvalidCredentials/Unavailable) are
                                           Result.Success — diagnosing IS the job, in every case

Infrastructure/Llm/
  OpenAICompatibleClient                — Bearer auth, {baseUrl}/chat/completions, response_format only
                                           for AiProvider.OpenAI (OpenRouter free models 400 on it)
  AnthropicClient                       — x-api-key + anthropic-version headers, top-level `system`
                                           field (not a role:"system" message), {baseUrl}/messages
  LLMClientFactory                      — synchronous, pure resolver over AppSettings; 4-provider ×
                                           base-URL table; MissingApiKey checked before MissingBaseUrl
  LlmServiceCollectionExtensions        — AddLlmClients: 8 named HttpClients (llm-{provider} +
                                           llm-{provider}-diagnostic × 4 providers), D4 knobs explicit
  Prompts/EmbeddedPromptProvider        — embedded .txt resources, locale fallback, ConcurrentDictionary
                                           cache; 4 resources (categorize-expense.{system,user}.{es-DO,en})

UI/
  ViewModels/ExpensesViewModel          — optional 17th ctor param (kill switch); SuggestedCategoryId/
                                           Name, IsSuggestionLoading, AiStatusMessage; generation-token +
                                           per-invocation CTS for supersede-and-discard; AcceptSuggestion/
                                           DismissSuggestion commands
  Views/ExpensesView.axaml              — suggestion chip + dismiss button + status TextBlock beside the
                                           category ComboBox
  ViewModels/SettingsViewModel          — IsTestingConnection, AiTestConnectionMessage,
                                           TestConnectionCommand (never writes FormError)
  Views/SettingsView.axaml              — "Probar conexión" button under the existing Save AI button
```

## 3. Slice plan

| ID | Slice | Likely PR | Status |
| --- | --- | --- | --- |
| 1 | Application contracts, zero HTTP surface | PR 1 | ✅ Completed |
| 2 | OpenAI-compatible adapter + prompts | PR 2 | ✅ Completed |
| 3 | Anthropic adapter | PR 3 | ✅ Completed |
| 4 | Factory + DI + resilience | PR 4 | ✅ Completed |
| 5a | Tolerant parser | PR 5a | ✅ Completed |
| 5b | Categorization use case | PR 5b | ✅ Completed |
| 6 | Expense-entry UI | PR 6 | ✅ Completed |
| 7 | Settings "Probar conexión" | PR 7 | ✅ Completed |

Applied in 9 chained commits: `389382a` (contracts), `54f8743` (OpenAI adapter+prompts), `d40b536`
(Anthropic adapter), `6e8fa63` (factory+DI+resilience), `fedf209` (tolerant parser), `fc6b5de`
(categorization use case), `71c213a` (expense-entry suggestion chip), `3c044f8` (Probar conexión),
`aa340dd` (flaky debounce-window test fix — 25ms/20ms-gap debounce test replaced with a 400ms window
after intermittent flakiness under Windows' ~15.6ms timer resolution). Slice 4 (factory+DI+resilience)
was the highest-risk-to-revert unit — the only one touching `Directory.Packages.props`, the only one
able to break app startup — and shipped alone with a mandatory smoke-launch before slice 5a opened, per
the design's own recommendation. Slices 1–5 landed with zero user-visible behavior change; nothing was
reachable from the UI until slice 6.

## 4. Phase completion criteria

- **(roadmap a)** With a valid API key configured, typing "pizza 25" in an expense suggests the correct
  category. ✅ — verified against the deterministic fake `ILLMClient` exercised by slices 5b/6's tests;
  the opt-in OpenRouter round trip is evidence-only (see §5), not the acceptance gate.
- **(roadmap b)** With no API key or no network, the suggestion fails silently, the app keeps working,
  and the UI indicates it via `AiStatusMessage` — no dialog, no exception, no blocked form. ✅
- **(roadmap c)** The "Probar conexión" button in Settings validates provider + key and distinguishes
  not-configured, invalid-credentials, unavailable, and OK. ✅
- `ILLMClient`/`LlmRequest`/`LlmResponse`/`ILLMClientFactory` exist with zero `System.Net.Http`
  references; NetArchTest R1/R2/R4 stay green unmodified. ✅
- Both adapters covered by `FakeHttpMessageHandler` tests asserting distinct URLs, headers, request body
  shape, and response deserialization for 401/429/500/timeout. ✅
- The suggestion chip is the only code path that writes `SelectedCategoryId` from AI output — asserted
  by test. ✅
- No suggestion request fires when a category is already selected, description is under 4 characters,
  no key is configured, or the text repeats the last submitted text in the session. ✅
- Rapid typing produces exactly one call per 600ms pause; a superseded response is discarded and never
  renders; a manual pick cancels any in-flight request. ✅
- `CategorizeExpenseUseCase` always sends `Temperature = 0.0`, independent of `AiTemperature`. ✅
- The tolerant parser resolves clean/fenced/prose-preamble JSON, key aliases, case/diacritic mismatches,
  bare strings, and explicit null/none/ninguna; truncated JSON and prose paragraphs are `Unparseable`. ✅
- An unmatched category name is `Result.Success` with `Matched = false`, never a failure. ✅
- No `Category` row is ever created, updated, or deleted by any AI code path — asserted by a throwing
  test double. ✅
- Every pipeline failure translates to `Result.Failure` and is logged at Warning with a correlation
  `Guid`; no exception escapes to the UI. ✅
- Main-client resilience matches the exact D4/R-9 numbers; a 401/403 fails fast without retrying —
  asserted against attempt **counts** on the real registered pipeline, not framework defaults. ✅
- The diagnostic client carries no retry/breaker and is unaffected by the main client's breaker state. ✅
- The API key never appears in any log line, status message, or test-connection result — asserted with
  a sentinel key. ✅
- No hardcoded user-facing strings; new copy resolves through `Strings.resx`/`Strings.es-DO.resx`. ✅
- No migration, no `AppDbContext` change, no change to `SettingsStore`/`AppSettings`/`Setting` shape. ✅
- Full test suite green, no regressions (1184/1184: 936 unit + 248 integration). ✅

## 5. Findings carried forward, not fixed in this phase

- **The rapid-keystroke debounce test was genuinely flaky under Windows' ~15.6ms timer resolution.**
  `RapidKeystrokes_WithinDebounceWindow_ProduceExactlyOneCall` originally used a 25ms
  `SuggestionDebounceDelay` with 20ms gaps between simulated keystrokes — too close to the OS timer
  granularity for the assertion to be reliable. Fixed in `aa340dd` by widening the window to 400ms.
  **Pattern for future timing-based UI tests in this repo**: never pick a debounce/delay value close to
  Windows' default timer resolution; leave enough margin (10x+) that a scheduler hiccup cannot flip the
  assertion.
- **`CategorizeExpenseUseCase` is intentionally non-sealed with a `virtual ExecuteAsync`** — the one
  deviation from this codebase's "every use case is `sealed`" convention. Needed so slice 6's
  `FakeCategorizeExpenseUseCase` can override it for `ExpensesViewModel`'s race/cancellation tests,
  since the ViewModel's 17th ctor parameter takes the concrete class (not an interface) as its kill
  switch. Documented inline in the class's own XML doc; not expected to recur unless a future phase
  needs the same fake-by-subclassing pattern for another use case.
- **Roadmap criterion (a) is gated on a deterministic fake LLM response, not a live model.** The opt-in
  `CONTROL_FINANZAS_RUN_EXTERNAL_AI_TESTS`-guarded OpenRouter round trip is evidence that the wire
  format works end-to-end, not a guarantee of real-model category-suggestion quality. Prompt tuning
  against logged `RawModelAnswer` values is a follow-up, not a phase blocker.
- **No headless Avalonia test harness still exists** (same gap Phase 12 recorded). Not newly introduced
  by this phase; Phase 13 needed no additional manual-only scenarios beyond the ones Phase 12 already
  tracked.

## 6. References

- `docs/roadmap.md` §Fase 13 — original plan, now marked complete
- `spec/features/phase-13/F-013-ai-pipeline-categorization.done.md` — archived formal spec, evidence per
  criterion
- `.atl/sdd/f-013p13-ai-pipeline/{exploration,proposal,design,verify-report,archive-report}.md` — full
  traceability
- `docs/architecture.md` §11 — `ILLMClient` contract and resilience configuration, updated to reflect
  the shipped implementation (previously aspirational/planning-level text)
- `spec/features/phase-12/F-012-settings-i18n-ai-config.done.md` — Phase 12, the settings this phase
  reads read-only
