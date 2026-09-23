# F-013 — AI Pipeline + Automatic Expense Categorization

> **Phase:** 13 — Pipeline de IA + categorización automática
> **Status:** ✅ Archived (2026-09-23) — all acceptance criteria met, see
> `.atl/sdd/f-013p13-ai-pipeline/verify-report.md` and `archive-report.md`
> **SDD:** `.atl/sdd/f-013p13-ai-pipeline/`
> **Depends on:** Phase 12 (`ISettingsStore`/`AppSettings` supply provider, key,
> model, base URL, temperature, max tokens — read-only in this phase)

## Goal

Give the app its first outbound network dependency — an `ILLMClient`
abstraction, two provider adapters, and a resilience layer — and spend it on
the single smallest, most valuable AI feature: typing an expense description
suggests an existing category the user can accept with one deliberate click.
Every failure mode (no key, no network, timeout, bad key, unparseable
response, no matching category) must leave the expense form fully usable,
with an honest, non-blocking status label. The AI proposes; it never decides.

### Confirmed product decisions

- **D1 — Dismissable chip, explicit accept only.** The suggestion renders as
  a separate chip next to the category `ComboBox` in `ExpensesView`. Clicking
  the chip is the ONLY code path that writes `SelectedCategoryId` from AI
  output. The AI never sets `SelectedCategoryId` on its own, under any
  circumstance.
- **D2 — Debounce 600 ms / 4-char minimum, plus four hard call guards.** No
  call fires when a category is already selected, when the trimmed text is
  under 4 characters, when no API key is configured (or `Custom` has no base
  URL), or when the normalized text repeats the last text already submitted
  in this form session. At most one request is in flight at a time.
- **D3 — Tolerant parser with a fixed pipeline; no-match is a success.** The
  parser is a defined, ordered sequence (de-fence → extract/parse JSON →
  bare-string fallback → parse failure), matching is two-tier exact
  (ordinal-ignore-case, then diacritic-stripped) with no fuzzy matching.
  `CategorizeExpenseUseCase` never creates a `Category`.
- **D4 — Resilience numbers are fixed, not left to the framework default.**
  15 s total timeout, 6 s per-attempt timeout, 1 retry (2 attempts total)
  with jittered exponential backoff, circuit breaker at failure ratio 0.5 /
  minimum throughput 4 / 30 s sampling / 15 s break. The diagnostic
  ("Probar conexión") pipeline is separate and carries no retry, no breaker.
- **D5 — `OpenAICompatibleClient` built and externally tested first,**
  against OpenRouter's free tier, because it is wire-identical to the
  `OpenAI` default provider and covers three of the four `AiProvider` values.
  `AnthropicClient` ships in this phase too, covered by fake-handler unit
  tests only.

## Requirements

### R-1 — `ILLMClient` abstraction with zero HTTP surface

`Application/Abstractions/` MUST define `ILLMClient` (`CompleteAsync`,
`TestConnectionAsync`, `ProviderName`), `LlmRequest`, `LlmMessage`,
`LlmResponse` (`Content`, `TokensUsed`, `LatencyMs`), and
`ILLMClientFactory`. None of these types, nor any type in `Application` or
`UI`, MAY reference `System.Net.Http` in any form — not a field, not a
constructor parameter, not an XML-doc `<see cref>`. This is enforced by the
existing `ArchitectureTests.cs:64-75` NetArchTest rules R1/R2/R4, which MUST
stay green against the new types with no modification to the rules
themselves.

### R-2 — Provider adapters and factory

`Infrastructure/Llm/` MUST contain `OpenAICompatibleClient` (serving
`OpenAI`, `OpenRouter`, and `Custom`) and `AnthropicClient` (serving
`Anthropic`), both implementing `ILLMClient` via `IHttpClientFactory`, and
`LLMClientFactory` implementing `ILLMClientFactory`, resolving the adapter
from the persisted `AppSettings.AiProvider`. Base URL resolution MUST use
`AiBaseUrl` when non-empty, otherwise the per-provider default:
`OpenAI → https://api.openai.com/v1`, `OpenRouter → https://openrouter.ai/api/v1`,
`Anthropic → https://api.anthropic.com/v1`. `Custom` with an empty
`AiBaseUrl` MUST be treated as a configuration error and MUST NOT produce an
HTTP call.

### R-3 — Automatic expense categorization use case

`CategorizeExpenseUseCase` MUST read categories via `ICategoryRepository`
read-only (no create/update/delete path is reachable from any AI code path),
build a prompt containing the user's exact existing category names, call
`ILLMClient`, and run the tolerant parser: trim and strip markdown code
fences; extract and parse the first balanced JSON object
(`PropertyNameCaseInsensitive`, accepting `category`, `categoria`, `name`
keys; an explicit JSON `null` or the literal `null`/`none`/`ninguna` is a
deliberate no-match); if no JSON parses, accept the cleaned text as a bare
category name only if single-line and ≤ 60 characters; anything else is a
parse failure treated as no-match. The parsed name MUST be matched against
existing categories in exactly two tiers — ordinal-ignore-case exact match,
then diacritic-stripped (Unicode NFD, drop `NonSpacingMark`) normalized exact
match — with no third tier and no fuzzy/Levenshtein/embedding matching. Two
categories normalizing to the same key MUST be treated as ambiguous → no
match. A successful round trip with no matching category MUST return
`Result.Success(CategorySuggestionDto(CategoryId: null, CategoryName: null,
Matched: false, RawModelAnswer: "…"))` — this is a success, never
`Result.Failure`.

### R-4 — Failure semantics and correlation-id logging

Every pipeline failure (HTTP error, timeout, open circuit breaker, transport
or JSON error) MUST be caught and translated to
`Result.Failure("LLMUnavailable", …)`, logged at Warning with a per-call
correlation `Guid`, and MUST NOT let any exception escape to the UI. When no
API key is configured, or `AiProvider == Custom` with no `AiBaseUrl`, the use
case MUST short-circuit with `Result.Failure("AiNotConfigured", …)` and MUST
NOT make any HTTP call. The API key MUST NOT appear in any log line, status
message, or test-connection result, under any outcome.

### R-5 — Suggestion UX: dismissable chip, explicit accept only

`ExpensesViewModel` MUST expose `SuggestedCategoryId`, `SuggestedCategoryName`,
`IsSuggestionVisible`, `IsSuggestionLoading`, `AiStatusMessage`,
`AcceptSuggestionCommand`, and `DismissSuggestionCommand`. Accepting the chip
MUST be the only code path in the entire feature that writes
`SelectedCategoryId` from AI output. Manually selecting a category in the
`ComboBox` MUST clear any displayed or pending suggestion and cancel any
in-flight suggestion request. Saving or resetting the expense form MUST
clear all suggestion state.

### R-6 — Call guards and cost controls

A suggestion request MUST NOT fire unless all of the following hold: the
trimmed `Description` is at least 4 characters, `SelectedCategoryId` is
`null`, an API key is configured (and, for `Custom`, `AiBaseUrl` is
non-empty), and the normalized description differs from the last text
already submitted in this form session (a repeat MUST reuse the cached
result instead of calling again). A request MUST fire only after 600 ms of
keystroke inactivity since the last `Description` change. At most one
suggestion request MAY be in flight; a new qualifying request MUST cancel
and supersede any prior one, and a superseded response MUST be discarded and
MUST NOT render. The description sent to the model MUST be truncated to 200
characters. The request's `MaxTokens` MUST be `min(AiMaxTokensPerRequest,
128)`.

### R-7 — Deterministic categorization temperature

`CategorizeExpenseUseCase` MUST send `Temperature = 0.0` on every request,
regardless of the user's persisted `AiTemperature` setting. This is a
deliberate, documented override — classification into a closed list is not a
creative task, and `AiTemperature` remains the user's control for later
narrative AI features, not for this use case.

### R-8 — Connection test ("Probar conexión")

`TestAiConnectionUseCase` MUST use a separate, provider-named diagnostic
`HttpClient` (`llm-{provider}-diagnostic`) with no retry policy and no
circuit breaker, distinct from the main categorization pipeline's client, so
that a just-corrected API key is never rejected by a stale open breaker.
The use case MUST distinguish exactly four outcomes: not-configured (no key,
or `Custom` with no base URL — no call made), invalid-credentials (401/403),
unavailable (network error, timeout, 5xx), and OK.

**All four outcomes MUST be returned as `Result.Success`**, carrying a DTO
whose discriminated `Kind` names the outcome — none of them is a
`Result.Failure`. Diagnosing the connection is this use case's entire job,
and it succeeded at that job in all four cases; "not configured" in
particular is the expected first-run state of the Settings screen, not an
error condition. This is a deliberate, local departure from R-4's convention:
the names not-configured and unavailable are `Result.Failure` **codes** on
the categorization path (R-4), where they mean "the feature could not do the
thing you wanted", and `Result.Success` **outcome kinds** here, where they
mean "the diagnosis completed and this is its answer".
`SettingsView` MUST render a "Probar conexión" button wired to a
`TestConnectionCommand` on `SettingsViewModel` with a busy state and a
localized result message.

### R-9 — Resilience configuration

The main categorization `HttpClient` registrations (`llm-{provider}`) MUST
be configured with: total request timeout 15 s, per-attempt timeout 6 s,
maximum 1 retry (2 attempts total) with jittered exponential backoff at 1 s
base delay, and a circuit breaker with failure ratio 0.5, minimum throughput
4, sampling duration 30 s, and break duration 15 s. The retry predicate MUST
NOT retry 401/403/400 responses — a bad credential MUST fail fast, not burn
the retry budget. `CategorizeExpenseUseCase` MUST additionally wrap its call
in a linked `CancellationTokenSource` with a 15 s timeout as defense in
depth. The diagnostic client (R-8) is explicitly exempt from retry and
breaker configuration.

### R-10 — Localization

All new user-facing copy — the suggestion chip label, `AiStatusMessage`
values (e.g. "IA no configurada", "IA no disponible"), the "Probar conexión"
button and its result messages — MUST resolve through `Strings.resx` /
`Strings.es-DO.resx`. No hardcoded user-facing string literals are
introduced.

A hand-written `Strings.Designer.cs` accessor MUST be added for every key
consumed from XAML via `{x:Static resources:Strings.X}` (the build does not
regenerate this file, per the convention established in Phases 8/10/11/12) —
in this phase, the `SettingsView` "Probar conexión" copy. Keys consumed
instead by `ExpensesViewModel` through its already-injected
`IStringLocalizer<Strings>` (`ExpensesViewModel.cs:52`, resolved by string
key at runtime) MUST NOT require an accessor: `ExpensesView.axaml` carries no
`resources:` namespace at all today, and adding accessors it never reads
would mean further hand-edits to a file this project has been trying to stop
hand-editing for four consecutive phases.

## Scenarios

### S-1 — Valid key, "pizza 25" suggests the correct category

- GIVEN `AppSettings.AiApiKey` is a valid key and no category is selected in
  the expense form
- WHEN the user types "pizza 25" and pauses for 600 ms
- THEN exactly one suggestion request fires, and the chip displays the
  correct existing category name

### S-2 — No API key configured: silent, honest, no call

- GIVEN `AppSettings.AiApiKey` is empty
- WHEN the user types a description of 4+ characters and pauses
- THEN no HTTP call is made, no exception is thrown, `AiStatusMessage` shows
  "IA no configurada", no chip appears, and the expense form remains fully
  usable

### S-3 — Network/provider failure fails silently

- GIVEN a valid API key but the provider is unreachable or returns 5xx after
  retries are exhausted
- WHEN the user types a qualifying description and pauses
- THEN `CategorizeExpenseUseCase` returns `Result.Failure("LLMUnavailable",
  …)`, a Warning is logged with a correlation id, `AiStatusMessage` shows "IA
  no disponible", no dialog or exception reaches the UI, and the form remains
  fully usable

### S-4 — "Probar conexión" distinguishes outcomes

- GIVEN the Settings IA section is open
- WHEN the user clicks "Probar conexión" with, respectively, no key
  configured, a valid key, an invalid key, and no network reachable
- THEN the result message distinguishes `AiNotConfigured`, success,
  invalid-credentials, and `LLMUnavailable` respectively, with no HTTP call
  made in the not-configured case

### S-5 — Accepting the chip is the only path that sets the category

- GIVEN a suggestion chip is displayed with a suggested category
- WHEN the user clicks the chip
- THEN `SelectedCategoryId` is set to the suggested category, and this is
  asserted as the ONLY code path in the feature capable of that write — no
  suggestion arrival alone ever mutates `SelectedCategoryId`

### S-6 — No suggestion request when a category is already selected

- GIVEN the user is editing an existing expense whose category is already
  loaded into `SelectedCategoryId`
- WHEN the user edits the `Description` field
- THEN no suggestion request fires, regardless of text length or pause
  duration

### S-7 — Manual category pick cancels and clears

- GIVEN a suggestion request is in flight or a chip is currently displayed
- WHEN the user manually picks a category from the `ComboBox`
- THEN the in-flight request is cancelled, the chip is cleared, and no
  superseded response is later rendered

### S-8 — Rapid typing coalesces into one call

- GIVEN the user types "pizza 25" keystroke by keystroke with no 600 ms gap
  between characters
- WHEN typing completes and the field is idle for 600 ms
- THEN exactly one suggestion request fires for the whole entry

### S-9 — Tolerant parser handles model deviations

- GIVEN raw model responses: clean JSON, markdown-fenced JSON, JSON with a
  prose preamble, `categoria`/`name` key aliases, a case/diacritic-mismatched
  bare string ("alimentacion"), an explicit `null`/`none`, truncated JSON,
  and a full prose paragraph
- WHEN each is run through the parser
- THEN all except the prose paragraph and truncated JSON resolve to a parsed
  candidate name (or explicit no-match for `null`/`none`); the prose
  paragraph and truncated JSON resolve to parse failure → no-match

### S-10 — Unmatched category name is a success, not a failure

- GIVEN the model returns a category name that matches no existing category
  under either matching tier
- WHEN `CategorizeExpenseUseCase` completes
- THEN it returns `Result.Success` with `Matched = false`, and the UI shows
  no chip and no `AiStatusMessage`

### S-11 — No category is ever created by the AI path

- GIVEN any sequence of AI categorization or test-connection calls,
  including ones that produce no match
- WHEN inspecting `Category` table state before and after
- THEN no `Category` row is created, updated, or deleted by any AI code path

### S-12 — A 401/403 fails fast without retrying

- GIVEN the configured API key is invalid
- WHEN a categorization request is made
- THEN the resilience pipeline does not retry the 401/403 response, and the
  use case returns `Result.Failure("LLMUnavailable", …)` well under the 15 s
  total timeout

### S-13 — A corrected key is not blocked by a stale open breaker

- GIVEN the main `llm-{provider}` client's circuit breaker has opened after
  repeated categorization failures with a bad key
- WHEN the user corrects the key in Settings and immediately clicks "Probar
  conexión"
- THEN the diagnostic client's separate pipeline (no shared breaker state)
  returns a real, current answer, not a breaker rejection

### S-14 — API key never leaks into logs or messages

- GIVEN a categorization or test-connection call fails
- WHEN inspecting the logged Warning message, `AiStatusMessage`, and the
  "Probar conexión" result text
- THEN the API key value does not appear in any of them

## Out of Scope

- **Phase 12's cleartext API-key storage decision is not reopened.**
  `AppSettings.AiApiKey` is consumed read-only; nothing in this phase
  encrypts, re-persists, or transforms it. ADR-008 stands; ADR-011 (v2
  encryption) is untouched.
- **No category creation, update, or deletion from AI output**, ever, under
  any code path (see R-3, S-11).
- **No Phase 14 features.** No insights card, no month-end forecast, no
  anomaly narrative — those reuse `ILLMClient` later and are not prototyped
  here.
- No Phase 15 chat view, no Phase 16 work.
- **No fuzzy or embedding-based category matching.** Only the two exact-match
  tiers in R-3 exist; this is a deliberate rejection, not a future TODO for
  this phase.
- **No keyword/synonym/tag field added to `Category`**, and no local/offline
  classifier fallback.
- **No AI suggestions on income, debts, budgets, or goals** — expenses only.
- **No token-usage budget, spend meter, or per-day call cap in the UI.**
- **No streaming responses, no conversation history, no prompt caching.**
- **No schema change or migration.** `AppDbContext`, all repositories, and
  every domain entity are unchanged.
- **No change to `SettingsStore`, `ISettingsStore`, `AppSettings`, or the
  `Setting` table shape.** Phase 13 reads Phase 12's settings; it does not
  modify them.
- No mocking library. Test doubles (`FakeHttpMessageHandler`, a fake
  `ILLMClient`) are hand-written per the established convention.

## Acceptance Criteria

- [x] **(roadmap a)** With a valid API key configured, typing "pizza 25" in
  an expense suggests the correct category. — R-3, R-6, S-1. **Evidence:**
  `CategorizeExpenseUseCase`/`ExpensesViewModel` suggestion pipeline
  confirmed against a deterministic fake `ILLMClient` in slices 5b/6's
  tests; the opt-in OpenRouter round trip is evidence-only, not the gate
  (see `verify-report.md`).
- [x] **(roadmap b)** With no API key or no network, the suggestion fails
  silently, the app keeps working, and the UI indicates it via
  `AiStatusMessage` ("IA no configurada" / "IA no disponible") — no dialog,
  no exception, no blocked form. — R-4, S-2, S-3. **Evidence:**
  `CategorizeExpenseUseCase`'s `AiNotConfigured`/`LLMUnavailable`
  short-circuits (zero client calls in the former) and
  `ExpensesViewModel.RequestSuggestionAsync`'s broad catch, read directly.
- [x] **(roadmap c)** The "Probar conexión" button in Settings validates
  provider + key and distinguishes not-configured, invalid-credentials,
  unavailable, and OK. — R-8, S-4. **Evidence:** `TestAiConnectionUseCase`
  read in full; all four outcomes confirmed as `Result.Success` variants.
- [x] `ILLMClient`/`LlmRequest`/`LlmResponse`/`ILLMClientFactory` exist in
  `Application/Abstractions/` with zero `System.Net.Http` references; the
  existing `ArchitectureTests` R1/R2/R4 NetArchTest rules stay green
  unmodified. — R-1. **Evidence:** `LlmContractPurityTests` (stricter than
  NetArchTest, rejects any `System.Net.*`) + NetArchTest 11/11 re-run green.
- [x] `OpenAICompatibleClient` and `AnthropicClient` are both covered by
  `FakeHttpMessageHandler` tests asserting distinct URLs, headers
  (`Authorization: Bearer` vs `x-api-key` + `anthropic-version`), request
  body shape, and response deserialization for 401 / 429 / 500 / timeout. —
  R-2. **Evidence:** both adapter source files read directly; status-mapping
  table confirmed identical to design D-10.
- [x] `ILLMClientFactory` returns the correct adapter for all four
  `AiProvider` values and resolves base URLs per the documented defaults;
  `Custom` with an empty `AiBaseUrl` is a configuration error and never
  produces a call. — R-2. **Evidence:** `LLMClientFactory.ResolveCore`/
  `ResolveBaseUrl` read directly, matches the design's base-URL table
  exactly.
- [x] The suggestion chip is rendered beside the category `ComboBox`;
  accepting it is the only code path that writes `SelectedCategoryId` from
  AI output — asserted by test. — R-5, S-5. **Evidence:** `AcceptSuggestion`
  confirmed as the sole write site in `ExpensesViewModel.cs`.
- [x] No suggestion request fires when `SelectedCategoryId` is already set,
  when the trimmed description is under 4 characters, when no API key is
  configured, or when the text repeats the last submitted text in the same
  form session. — R-6, S-2, S-6. **Evidence:** `RequestSuggestionAsync`'s
  double-checked guards (before and after the debounce delay) and
  `_suggestionCache` read directly.
- [x] Rapid typing produces exactly one call per 600 ms pause; a superseded
  response is discarded and never renders; a manual `ComboBox` pick cancels
  any in-flight request and clears the suggestion. — R-6, S-7, S-8.
  **Evidence:** generation-token + `Interlocked`-based CTS handoff read
  directly; the `aa340dd` fix widened the rapid-keystroke test's timing
  window from 25 ms to 400 ms to remove Windows-timer-resolution flakiness
  (no flake reproduced across 3 fresh full-suite runs in `sdd-verify`).
- [x] `CategorizeExpenseUseCase` sends `Temperature = 0.0` on every request,
  independent of the user's persisted `AiTemperature`. — R-7. **Evidence:**
  `ClassificationTemperature` const, unconditionally used, confirmed by
  direct read.
- [x] The tolerant parser correctly resolves clean JSON, fenced JSON, a prose
  preamble, `categoria`/`name` aliases, case/diacritic mismatches, a
  bare-string answer, and explicit `null`/`none`, and treats truncated JSON
  and a prose paragraph as parse failures (no-match). — R-3, S-9.
  **Evidence:** `LlmCategoryAnswerParserTests`/`CategoryNameNormalizerTests`
  green in the 936/936 unit run; parser consumption path in
  `CategorizeExpenseUseCase` read directly.
- [x] An LLM-returned name with no exact (normalized) match under either
  matching tier returns `Result.Success` with `Matched = false`, not a
  failure; the UI shows no chip and no status message. — R-3, S-10.
  **Evidence:** `MatchCategory`'s tier-1/tier-2/ambiguity logic read
  directly, returns `null` → `Result.Success(Matched=false)`.
- [x] No `Category` row is ever created, updated, or deleted by any AI code
  path — asserted by test. — R-3, S-11. **Evidence:**
  `StrictReadOnlyCategoryRepository` (throwing `AddAsync`/`UpdateAsync`/
  `RemoveAsync`) used by `CategorizeExpenseUseCaseTests` and
  `ApiKeyIsNeverLoggedTests`, confirmed.
- [x] Every AI pipeline failure is translated to
  `Result.Failure("LLMUnavailable", …)` (or `"AiNotConfigured"` when no key
  is configured) and logged at Warning with a correlation `Guid`; no
  exception escapes to the UI. — R-4, S-2, S-3. **Evidence:**
  `CategorizeExpenseUseCase`'s full catch chain read directly, including the
  caller-cancellation-vs-hard-timeout distinction.
- [x] Resilience on the main client is configured with exactly: 15 s total
  timeout, 6 s per-attempt timeout, 1 retry with jittered exponential
  backoff, breaker at failure ratio 0.5 / minimum throughput 4 / 30 s
  sampling / 15 s break; a 401/403 fails fast without retrying. — R-9, S-12.
  **Evidence:** `LlmServiceCollectionExtensions.ConfigureMainPipeline` read
  directly; `ResiliencePipelineTests` asserts attempt **counts** (1 for
  401/403, 2 for a retryable 500) against the real registered pipeline, not
  framework defaults — non-vacuous by design.
- [x] The diagnostic ("Probar conexión") client carries no retry and no
  circuit breaker, and is unaffected by the main client's breaker state. —
  R-8, R-9, S-13. **Evidence:** distinct `llm-{provider}-diagnostic` named
  `HttpClient` with only `AddTimeout`, confirmed by direct read and by
  `ResiliencePipelineTests.DiagnosticClient_RetryableServerError_ProducesExactlyOneAttempt`.
- [x] The API key never appears in any log line, `AiStatusMessage`, or
  test-connection result. — R-4, S-14. **Evidence:** `ApiKeyIsNeverLoggedTests`
  (sentinel key through 401/500/timeout failures) read in full, plus direct
  read of `SettingsViewModel.MessageForOutcome`'s fixed message strings.
- [x] No hardcoded user-facing strings; all new copy resolves through
  `Strings.resx` / `Strings.es-DO.resx`, with hand-written
  `Strings.Designer.cs` accessors for the XAML-consumed keys only (keys read
  via `IStringLocalizer` need none). — R-10. **Evidence:** confirmed the
  `SettingsView`-only accessor set and `ExpensesView.axaml`'s continued
  absence of a `resources:` namespace.
- [x] No migration, no `AppDbContext` change, no change to `SettingsStore` /
  `AppSettings` / the `Setting` table. — Out of Scope. **Evidence:** `git
  diff` across all 9 phase commits touches no `Migrations/`,
  `SettingsStore.cs`, `AppSettings.cs`, or `Setting.cs`.
- [x] The opt-in external test
  (`CONTROL_FINANZAS_RUN_EXTERNAL_AI_TESTS=1` + key) performs one real
  OpenRouter free-model round trip through `OpenAICompatibleClient`, and is
  skipped by default (not part of the normal suite). — R-2. **Evidence:**
  `OpenRouterRoundTripTests.cs` (task 4.12) confirmed present, env-var
  gated, not counted in the 1184/1184 default run.
- [x] Full test suite green, no regressions. **Evidence:** 1184/1184 (936
  unit + 248 integration), re-run independently 3 consecutive times in
  `sdd-verify`, zero flakiness.
