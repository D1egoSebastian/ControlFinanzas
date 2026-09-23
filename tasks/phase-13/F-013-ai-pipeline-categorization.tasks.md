# Tasks — F-013 AI Pipeline + Automatic Expense Categorization

## Review Workload Forecast

| Field | Value |
|---|---|
| Estimated changed lines | ≈3,970 total (2,060 source + 1,910 tests) across 8 work units |
| 400-line budget risk | High — 7 of 8 units exceed 400 lines (median ≈480), no unit exceeds 700 |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 (contracts) → PR 2 (OpenAI adapter+prompts) → PR 3 (Anthropic adapter) → PR 4 (factory+DI+resilience) → PR 5a (parser) → PR 5b (categorization use case) → PR 6 (expense-entry UI) → PR 7 (Probar conexión) |
| Delivery strategy | auto-chain |
| Chain strategy | stacked-to-main |

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: High

Design's `## Recommended Slicing for sdd-tasks` mandates seven strictly-chained slices (one — slice 5 —
carrying a declared internal 5a/5b split, for 8 work units), already approved over both a 5-slice
consolidation and the design's own 9-unit hard-budget escape hatch. This plan keeps that ordering
verbatim: slice 3 (`AnthropicClient`) lands *before* slice 4 (the factory), because a factory shipping
without the Anthropic adapter would have to throw, silently misroute to `NotConfigured`, or — worst —
silently return the OpenAI-shaped client for `AiProvider.Anthropic`, producing a factory that looks
correct and is not. Slice 5a (the tolerant parser) depends only on slice 1 and is the one safe parallel
branch if ever needed, but stays chained after 4 per the design's recommendation. Slice 4 is the
highest-risk-to-revert unit — the only one touching `Directory.Packages.props`, the only one that can
break app startup, and the only one where a stray `AddHttpClient` call in `Bootstrap.cs` trips architecture
rule R-4 — so it ships alone with a mandatory smoke-launch before the next slice opens. Nothing is
user-visible until slice 6, so slices 1–5 can sit on `main` indefinitely with zero behavioural effect,
which is what makes an 8-unit stacked chain safe rather than reckless.

One task (4.8) was added by the orchestrator after this breakdown was first generated, which is why
slice 4 runs ≈70 lines above the design's own forecast table for it. The spec's R-9/S-12 and its
matching acceptance-criteria line require that a 401/403 fail fast without retrying, but every task
covering it asserted only at the adapter's status-mapping level and otherwise relied on the resilience
framework's documented default retry predicate — an acceptance criterion with no direct evidence
behind it, which is the exact failure mode Phase 11 shipped and `docs/AI_HANDOFF.md` gotcha #20
records. Task 4.8 asserts attempt counts against the really-registered pipeline instead.

Task 4.9 was added for the same reason after slice 2 landed. `LlmFailureKind.CircuitOpen` exists as an
enum member from slice 1, but slice 2 could not wire Polly's `BrokenCircuitException` to it because the
resilience package is not referenced until slice 4 — so nothing in the original breakdown ever mapped
it, leaving a dead enum member and an exception type that would escape unmapped when the breaker
opens, against R-4's "no exception escapes to the UI".

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|---|---|---|---|
| 1 | Application contracts, zero HTTP surface | PR 1 | Base = main. `ILLMClient`, `ILLMClientFactory`, records, `LlmException`, `LlmContractPurityTests`. Everything else depends on this. |
| 2 | OpenAI-compatible adapter + prompts | PR 2 | Base = main (PR 1 merged). `OpenAICompatibleClient`, `EmbeddedPromptProvider`, 4 prompt resources, `FakeHttpMessageHandler`. |
| 3 | Anthropic adapter | PR 3 | Base = main (PR 2 merged). Independently shippable/droppable; consumes slice 2's `FakeHttpMessageHandler`. |
| 4 | Factory + DI + resilience | PR 4 | Base = main (PR 3 merged). Highest risk to revert — review and smoke-launch alone before PR 5a opens. |
| 5a | Tolerant parser | PR 5a | Base = main (PR 4 merged). Compile-depends only on slice 1; highest-complexity logic, reviewed alone. |
| 5b | Categorization use case | PR 5b | Base = main (PR 5a merged). Needs slice 4's real factory to contrast against its fake; `Bootstrap.cs` registration. |
| 6 | Expense-entry UI | PR 6 | Base = main (PR 5b merged). `ExpensesViewModel` optional 17th ctor param is the kill switch. |
| 7 | Settings "Probar conexión" | PR 7 | Base = main (PR 4 merged; independent of 5/6). Delivers roadmap criterion (c). |

## 1. Application contracts

Depends on nothing. Goal: CI proves the contract is HTTP-free before any adapter exists.

- [x] 1.1 Write failing `Tests.Unit/Llm/LlmContractPurityTests.cs`: reflection assertion — no public type in `Application.Abstractions.Llm` has a property/ctor-param/method-param/return/generic-argument type whose full name starts with `System.Net.` (stricter than NetArchTest's prefix match). (R-1, D-1)
- [x] 1.2 Create `Application/Abstractions/Llm/LlmMessage.cs`: `LlmMessage` record + `LlmRole` enum. (R-1, D-1)
- [x] 1.3 Create `Application/Abstractions/Llm/LlmRequest.cs`: record incl. `Metadata` dictionary (ordinal comparer). (R-1, D-1)
- [x] 1.4 Create `Application/Abstractions/Llm/LlmResponse.cs`: `LlmResponse` record (`Content`, `TokensUsed`, `LatencyMs`). (R-1, D-1)
- [x] 1.5 Create `Application/Abstractions/Llm/LlmException.cs`: `LlmException` + 7-member `LlmFailureKind` enum. No `HttpStatusCode` anywhere. (R-1, D-1)
- [x] 1.6 Create `Application/Abstractions/Llm/LlmConnectionOutcome.cs`: `LlmConnectionOutcome` + `LlmConnectionStatus` (`Ok`/`InvalidCredentials`/`Unavailable` — deliberately no `NotConfigured` member). (R-8, D-2)
- [x] 1.7 Create `Application/Abstractions/Llm/LlmClientResolution.cs`: `LlmClientResolution` + `LlmConfigurationStatus` enum. (R-2, D-3)
- [x] 1.8 Create `Application/Abstractions/Llm/ILLMClient.cs`: `ProviderName`, `CompleteAsync` (throws `LlmException`), `TestConnectionAsync` (never throws). (R-1, D-1, D-2)
- [x] 1.9 Create `Application/Abstractions/Llm/ILLMClientFactory.cs`: `Resolve(AppSettings)` / `ResolveDiagnostic(AppSettings)` — synchronous, pure. (R-2, D-3)
- [x] 1.10 Create `Application/Abstractions/Llm/IPromptProvider.cs` + `Application/Llm/PromptNames.cs` (two `const string` names). (D-4)
- [x] 1.11 Run 1.1 green; confirm existing `ArchitectureTests` R1/R2/R4 NetArchTest rules stay green unmodified. (R-1)
- [x] 1.12 Run all four gates: build, `dotnet test`, `dotnet format style --verify-no-changes`, NetArchTest filter. All green before opening PR 1.

## 2. OpenAI-compatible adapter + prompts

Depends on 1. Goal: one adapter provably speaking the OpenAI wire format, no DI, no resilience yet.

- [x] 2.1 Extend `Tests.Unit/TestDoubles/RecordingLogger.cs` additively: `Messages`, `Properties` alongside the existing `Levels`. No existing test changes. (D-8)
- [x] 2.2 Create `Tests.Integration/TestDoubles/FakeHttpMessageHandler.cs`: canned-response ctor + responder-delegate ctor; buffers each request's method/URI/headers/body into a `CapturedRequest` record. (D-14)
- [x] 2.3 Write failing `Tests.Integration/Llm/EmbeddedPromptProviderTests.cs`: every `PromptNames` constant resolves in `es-DO` and `en`; unknown locale falls back to default; the system template literally contains `{{categories}}` and the "exactly one name…or null" instruction. (D-4)
- [x] 2.4 Create 4 embedded resources `Infrastructure/Llm/Prompts/categorize-expense.{system,user}.{es-DO,en}.txt` with D-4's non-negotiable content (closed list, "never invent, never explain", `{"category":"<name>"}` output shape). (D-4)
- [x] 2.5 Create `Infrastructure/Llm/Prompts/EmbeddedPromptProvider.cs`: resource reader, locale fallback, `ConcurrentDictionary` cache, throws `InvalidOperationException` for a missing known name. Modify `Infrastructure.csproj` to add `<EmbeddedResource Include="Llm\Prompts\*.txt" />`. Run 2.3 green. (D-4)
- [x] 2.6 Write failing `Tests.Integration/Llm/OpenAICompatibleClientTests.cs`: absolute `{baseUrl}/chat/completions` URI; `Authorization: Bearer`; body has `model`, system message, user message, `temperature:0`, `max_tokens`; `response_format` present only for `OpenAI`; reads `choices[0].message.content` and `usage.total_tokens`; `LatencyMs>0`; 401→`InvalidCredentials`, 429→`RateLimited`, 500→`ServerError`, delayed-past-token→`Timeout`, no-`choices` 200→`Malformed`. (R-2, D-10)
- [x] 2.7 Create `Infrastructure/Llm/OpenAICompatibleClient.cs`: implements `ILLMClient` via `IHttpClientFactory`, `SnakeCaseLower` JSON, status-mapping table. Run 2.6 green. (R-2, D-10)
- [x] 2.8 Run all four gates. All green before opening PR 2.

## 3. Anthropic adapter

Depends on 1, 2 (`FakeHttpMessageHandler`). Goal: prove the abstraction is not OpenAI-shaped.

- [x] 3.1 Write failing `Tests.Integration/Llm/AnthropicClientTests.cs`: `x-api-key` present, `Authorization` **absent**; `anthropic-version` header present; body has a top-level `system` field and **no** `role:"system"` message; reads `content[0].text`; `usage.input_tokens+output_tokens`; the same five failure mappings as slice 2. (R-2, D-10)
- [x] 3.2 Create `Infrastructure/Llm/AnthropicClient.cs`: Messages API adapter, written from the wire spec — not by copy-pasting `OpenAICompatibleClient` and swapping a URL. Run 3.1 green. (R-2, D-10)
- [x] 3.3 Run all four gates. All green before opening PR 3.

## 4. Factory + DI + resilience

Depends on 1, 2, 3. Highest risk to revert in the phase — review and smoke-launch alone.

- [x] 4.1 Modify `Directory.Packages.props`: add `Microsoft.Extensions.Http` and `Microsoft.Extensions.Http.Resilience` `PackageVersion` entries with a justifying comment banner per the file's own convention. (D-9)
- [x] 4.2 Modify `Infrastructure.csproj`: add the two `PackageReference` entries — **Infrastructure only**, never UI. (R-1, D-9)
- [x] 4.3 Modify `Tests.Integration.csproj`: add `Microsoft.Extensions.DependencyInjection` (for the `AddLlmClients` resolution test). (D-14)
- [x] 4.4 Write failing `Tests.Integration/Llm/LLMClientFactoryTests.cs`: table of `(AppSettings) → (adapter type, base URL, status)` for all 4 providers × base-URL branches; empty key → `MissingApiKey` for every provider (checked before base URL); `Custom` + empty base URL → `MissingBaseUrl`; `Resolve`/`ResolveDiagnostic` bind different named `HttpClient`s. (R-2, D-3)
- [x] 4.5 Create `Infrastructure/Llm/LLMClientFactory.cs`: synchronous, pure resolver over the static base-URL table. Run 4.4 green. (R-2, D-3)
- [x] 4.6 Write failing `Tests.Integration/Llm/AddLlmClientsTests.cs`: `ILLMClientFactory`/`IPromptProvider` resolve; all **8** named `HttpClient`s resolve distinct instances; provider builds with `ValidateOnBuild=true`. (R-9, D-9)
- [x] 4.7 Create `Infrastructure/Llm/LlmServiceCollectionExtensions.cs`: `AddLlmClients()` — 8 named clients (`llm-{provider}` + `llm-{provider}-diagnostic` × 4 providers); main clients get every D-4/D-9 knob explicit (15s total, 6s attempt, 1 retry jittered exponential @1s, breaker ratio 0.5/throughput 4/sample 30s/break 15s, default retry predicate skips 401/403/400); diagnostic clients get timeout only, no retry, no breaker; `AddSingleton<ILLMClientFactory>`, `AddSingleton<IPromptProvider>`; returns `IServiceCollection`, no `Action<HttpClient>`/`IHttpClientBuilder` in the signature. Run 4.6 green. (R-2, R-8, R-9, D-9)
- [x] 4.8 Write `Tests.Integration/Llm/ResiliencePipelineTests.cs` asserting attempt **counts** against the real `AddLlmClients` pipeline (not the framework's documented defaults): resolve the named `llm-{provider}` client from a real `ServiceCollection`, back it with a counting `FakeHttpMessageHandler`, and assert (a) a 401 and a 403 each produce **exactly 1** attempt, and (b) a retryable 500 produces **2** attempts. Assertion (b) is what stops (a) from passing vacuously — without it, a pipeline where retry was never configured at all would also report 1 attempt for a 401. Then assert the diagnostic client produces exactly 1 attempt for the 500 too, proving it is genuinely retry-free. (R-9, S-12, D-9)
- [x] 4.9 Map the open-breaker case into `LlmFailureKind.CircuitOpen` in **both** adapters (`OpenAICompatibleClient`, `AnthropicClient`): catch Polly's `BrokenCircuitException`/`IsolatedCircuitException` and throw `LlmException(LlmFailureKind.CircuitOpen)`, with a test per adapter driving the breaker open through the real pipeline. Slice 2 deliberately left this unmapped because Polly is not a package reference until this slice — without it `CircuitOpen` is a dead enum member and an open breaker escapes as an unmapped exception type, violating R-4's "every pipeline failure is translated, no exception escapes to the UI". (R-4, D-1, D-10)
- [x] 4.10 Modify `UI/Composition/Bootstrap.cs`: add exactly one line, `services.AddLlmClients();`, under a `// Fase 13 (F-013, slice 4)` banner next to `AddExchangeRates`. Never call `AddHttpClient` directly in `Bootstrap.cs` — `Bootstrap`'s R-4 exemption covers only the Infrastructure-reference rule. (D-9)
- [x] 4.11 Write/extend an architecture regression assertion confirming `R4_UI_No_Depende_De_Paquetes_Prohibidos_EfCore_HttpClient` stays green after the new `Bootstrap.cs` line. (D-9)
- [x] 4.12 Create `Tests.Integration/Llm/External/OpenRouterRoundTripTests.cs`: opt-in `[Fact]` guarded by `CONTROL_FINANZAS_RUN_EXTERNAL_AI_TESTS` + `CONTROL_FINANZAS_OPENROUTER_API_KEY` env vars, returning immediately when either is unset; one real round trip through `OpenAICompatibleClient` against an OpenRouter free-tier model; asserts a response returned and `Parse(...).Kind != Unparseable` only — never asserts which category; skipped by default, not part of the normal suite. (R-2, D-14)
- [x] 4.13 Run all four gates; smoke-launch the app (`dotnet run`) against an existing dev DB to confirm no DI startup failure. All green before opening PR 4.

## 5a. Tolerant parser

Depends only on 1. Goal: the phase's highest-complexity logic, reviewed with nothing else in the diff.

- [x] 5a.1 Write failing `Tests.Unit/Llm/LlmCategoryAnswerParserTests.cs`: clean/fenced/bare-fenced/prose-preamble JSON; `categoria`/`name` aliases; explicit JSON `null` and string `null`/`none`/`ninguna`; bare string ≤60 chars single-line; truncated JSON falling to bare-string; prose paragraph → `Unparseable`; 61-char bare string → `Unparseable`; multi-line bare string → `Unparseable`; balanced-brace regression `{"category":"a} b"}`; empty/whitespace/null input. (R-3, D-5, S-9)
- [x] 5a.2 Create `Application/Llm/LlmCategoryAnswerParser.cs`: trim/de-fence → balanced-brace JSON extraction (depth-counter, string/escape aware) → bare-string fallback → `Unparseable`; regex with explicit 500ms `TimeSpan` timeout. Run 5a.1 green. (R-3, D-5)
- [x] 5a.3 Write failing `Tests.Unit/Llm/CategoryNameNormalizerTests.cs`: `Fold("alimentacion")==Fold("Alimentación")`; `Clean` strips quotes/punctuation; folding does not collapse distinct names (`"Casa"` vs `"Caza"`). (R-3, D-6, S-9)
- [x] 5a.4 Create `Application/Llm/CategoryNameNormalizer.cs`: `Clean`/`Fold` (Unicode NFD, drop `NonSpacingMark`, upper-invariant). Run 5a.3 green. (R-3, D-6)
- [x] 5a.5 Run all four gates. All green before opening PR 5a. **Resolved 2026-09-23**: re-run by the orchestrator — build 0/0, 1141/1141 tests, NetArchTest 11/11, format clean; the Application Control block below cleared on its own (same transient pattern as Phase 8→9). Original note: **BLOCKED (partial)**: gates 1 (`dotnet build`) and 3 (`dotnet format style --verify-no-changes`) are green; gate 2/4 cannot be fully confirmed — `ArchitectureTests` (which also *is* gate 4's NetArchTest filter) fails all 11 methods with `FileLoadException: An Application Control policy has blocked this file (0x800711C7)` while reflection-loading `ControlDeFinanzasIA.Tests.Integration.dll` via `Assembly.LoadFrom`. Confirmed environmental, not a regression: reproduces identically with slice 5a's new files stashed out, after `dotnet clean` + full rebuild, and when running `Tests.Integration.csproj` directly as its own test host (same error). Root cause: this machine's Windows Smart App Control policy (`VerifiedAndReputablePolicyState=1`) blocking a freshly-compiled, locally-built test binary. All 37 new slice-5a tests (27 parser + 10 normalizer) pass; the other 882 pre-existing Tests.Unit tests pass; only the 11 `ArchitectureTests` methods are affected, all via the same shared static-constructor exception.

## 5b. Categorization use case

Depends on 4, 5a. Goal: no-match-is-success, zero-call guards, forced temperature, correlation-id logging.

- [ ] 5b.1 Create `Tests.Unit/TestDoubles/{FakeLlmClient,FakeLlmClientFactory,FakePromptProvider,StrictReadOnlyCategoryRepository,FakeCategorizeExpenseUseCase}.cs` — hand-rolled, no mocking library; `StrictReadOnlyCategoryRepository`'s `AddAsync`/`UpdateAsync`/`RemoveAsync` throw. (D-6, D-14)
- [ ] 5b.2 Write failing `Tests.Unit/UseCases/CategorizeExpenseUseCaseTests.cs`: happy path `Matched=true`; unmatched → `Success{Matched=false}` (assert `IsSuccess==true` explicitly); ambiguous duplicate-normalizing categories → `Matched=false`; empty key → `Failure("AiNotConfigured")` with **zero** `ILLMClient` calls; `Custom` no base URL → same; empty category list → success, zero calls; a throwing client → `Failure("LLMUnavailable")` + Warning carrying `CorrelationId`; `Temperature==0.0` on the captured request even when `AiTemperature=1.9`; `MaxTokens==128` even when the setting is 2000; a 250-char description reaches the prompt truncated to 200; `StrictReadOnlyCategoryRepository` never trips. (R-3, R-4, R-6, R-7, D-6, D-7, D-8, S-10, S-11, S-12)
- [ ] 5b.3 Write failing `Tests.Unit/Llm/ApiKeyIsNeverLoggedTests.cs`: sentinel key `"sk-SENTINEL-DO-NOT-LOG"` through 401/500/timeout failures; sentinel appears in no `RecordingLogger` message, no `Result.Error.Message`, no DTO. (R-4, D-8, S-14)
- [ ] 5b.4 Create `Application/UseCases/Expenses/CategorizeExpense/{CategorizeExpenseCommand,CategorySuggestionDto,CategorizeExpenseUseCase}.cs`: ctor `ISettingsStore, ILLMClientFactory, ICategoryRepository, IPromptProvider, ILogger` — no `HttpClient`/`IHttpClientFactory`/`System.Net.*`; correlation-id `Guid`; 4-char/200-char guards; forced `Temperature=0.0` (`ClassificationTemperature` const with the inline reason comment); `MaxTokens=min(AiMaxTokensPerRequest,128)`; 15s linked-CTS defense-in-depth; the D-7 failure-translation table (rethrow only when `ct` itself was cancelled). Run 5b.2 and 5b.3 green. (R-3, R-4, R-6, R-7, D-6, D-7, D-8)
- [ ] 5b.5 Modify `UI/Composition/Bootstrap.cs`: `AddTransient<CategorizeExpenseUseCase>()`. This exact registration is what slice 6's kill-switch test removes to prove the feature is inert without it. (D-12)
- [ ] 5b.6 Run all four gates. All green before opening PR 5b.

## 6. Expense-entry UI

Depends on 5b. Goal: the user-visible feature, with supersede-and-discard proven.

- [ ] 6.1 Modify `UI/Resources/Strings.resx`, `Strings.es-DO.resx`: add 6 keys (`ExpensesAiSuggestionPrefix`, `ExpensesAiSuggestionAcceptHint`, `ExpensesAiSuggestionDismissHint`, `ExpensesAiNotConfiguredMessage`, `ExpensesAiUnavailableMessage`, `ExpensesAiSuggestionLoading`). No `Strings.Designer.cs` accessors — `ExpensesViewModel` already resolves via its injected `IStringLocalizer<Strings>` and `ExpensesView.axaml` carries no `resources:` namespace. (R-10, D-15)
- [ ] 6.2 Write failing `Tests.Unit/ViewModels/ExpensesViewModelSuggestionTests.cs`: race R-a (5 rapid `Description` writes → 1 call), R-b (slower-then-faster supersede → later result wins and stays), R-c (manual pick cancels in-flight, no chip, `SelectedCategoryId` keeps user's value), R-d (cancelled task never surfaces as `FormError`/`AiStatusMessage`), R-e (`BeginEdit` on an already-categorized expense → zero calls), R-f (Accept writes the category exactly once, only via the command); length 3→0 calls, length 4→1 call; repeat text hits the cache with zero extra calls; `IsSuggestionLoading` returns to `false` on every path. Set `SuggestionDebounceDelay` to 20–30ms for the suite. (R-5, R-6, D-12, S-5, S-6, S-7, S-8)
- [ ] 6.3 Add to the same test file: a successful `Matched=true` result never mutates `SelectedCategoryId` directly; `DismissSuggestionCommand` clears the chip, leaves `SelectedCategoryId` untouched; a failure sets `AiStatusMessage` and leaves `Amount`/`Description`/`RegisterExpenseCommand` fully usable; a no-match sets neither chip nor status message. (R-5, D-12, S-5, S-10)
- [ ] 6.4 Add a kill-switch test: `ExpensesViewModel` constructed with `categorizeExpenseUseCase: null` — typing a long description produces no exception, no status message, no chip. (D-12)
- [ ] 6.5 Modify `UI/ViewModels/ExpensesViewModel.cs`: optional 17th ctor param `CategorizeExpenseUseCase? categorizeExpenseUseCase = null`; `SuggestedCategoryId`/`SuggestedCategoryName`/`IsSuggestionLoading`/`AiStatusMessage` observable properties + computed `IsSuggestionVisible`/`SuggestedCategoryChipLabel`; public settable `SuggestionDebounceDelay` (default 600ms); `_suggestionCache`; generation-token + per-invocation-owned CTS (cancel-only on supersede, dispose in own `finally`); `OnDescriptionChanged`/`OnSelectedCategoryIdChanged` hooks; `AcceptSuggestionCommand`/`DismissSuggestionCommand`; `ClearForm`/`BeginEdit` gain cancel-and-clear calls, with `BeginEdit` setting `SelectedCategoryId` **before** `Description` (load-bearing ordering — comment it). Run 6.2–6.4 green. (R-5, R-6, R-7, D-12)
- [ ] 6.6 Modify `UI/Views/ExpensesView.axaml`: wrap the category `ComboBox` cell in an inner `StackPanel Spacing="4"`; add the chip `Button` (bound to `AcceptSuggestionCommand`/`SuggestedCategoryChipLabel`) + dismiss `Button` + `AiStatusMessage` `TextBlock`, all `IsVisible`-bound. No spinner — `IsSuggestionLoading` only dims the chip area's opacity. (R-5, D-13)
- [ ] 6.7 Run all four gates. All green before opening PR 6.

## 7. Settings "Probar conexión"

Depends on 4 only. Goal: roadmap criterion (c).

- [ ] 7.1 Modify `UI/Resources/Strings.resx`, `Strings.es-DO.resx`, `Strings.Designer.cs`: add 6 keys (`SettingsAiTestConnectionButton`, `...Testing`, `...Ok` (with `{0}` placeholder), `...NotConfigured`, `...InvalidCredentials`, `...Unavailable`) + hand-written Designer accessors for all 6 — XAML-consumed via `{x:Static}`. (R-10, D-15)
- [ ] 7.2 Write failing `Tests.Unit/UseCases/TestAiConnectionUseCaseTests.cs`: each of the 4 `AiConnectionResultKind` values returned as `Result.Success`; not-configured makes zero client calls; `ResolveDiagnostic` — not `Resolve` — is the method invoked (recording fake factory). (R-8, D-11, S-4, S-13)
- [ ] 7.3 Create `Application/UseCases/Settings/TestAiConnection/{TestAiConnectionDto,TestAiConnectionUseCase}.cs`: ctor `ISettingsStore, ILLMClientFactory, ILogger`; maps `LlmConnectionStatus` + not-configured to the flat `AiConnectionResultKind`; 15s hard ceiling; **all four outcomes are `Result.Success`**, never `Result.Failure`. Run 7.2 green. (R-8, D-11)
- [ ] 7.4 Modify `UI/Composition/Bootstrap.cs`: `AddTransient<TestAiConnectionUseCase>()`. (D-11)
- [ ] 7.5 Write failing `Tests.Unit/ViewModels/SettingsViewModelTestConnectionTests.cs`: each `Kind` maps to its own localized message; `IsTestingConnection` is `true` during and `false` after, including on the exception path; `FormError` is never written by the test command. (R-8, D-11, S-4)
- [ ] 7.6 Modify `UI/ViewModels/SettingsViewModel.cs`: `IsTestingConnection`, `AiTestConnectionMessage`, `TestConnectionCommand` (`try`/`finally`, `CanExecute = !IsTestingConnection` via `NotifyCanExecuteChangedFor`). Run 7.5 green. (R-8, D-11)
- [ ] 7.7 Modify `UI/Views/SettingsView.axaml`: add the "Probar conexión" button just under the existing `SaveAiCommand` button + a bound result `TextBlock`; button `IsEnabled` bound to the command's `CanExecute`. (R-8, D-11)
- [ ] 7.8 Modify `docs/architecture.md` §11: update `TestConnectionAsync`'s signature to the three-state `LlmConnectionOutcome`, with a one-line note that roadmap criterion (c) is what requires it. (D-2)
- [ ] 7.9 Run all four gates. All green before opening PR 7.

## 8. Cierre

- [ ] 8.1 Run the full gate suite across the whole phase: build, `dotnet test` (unit + integration), `dotnet format style --verify-no-changes`, NetArchTest architecture filter. All green, no regressions. Confirm no migration, no `AppDbContext`/`AppDbContextModelSnapshot.cs` change, no change to `ISettingsStore`/`AppSettings`/`SettingsStore`/`Setting` table shape (Out of Scope).
- [ ] 8.2 Run the app for real against an existing dev DB and a fresh DB: (a) valid key, type "pizza 25" → correct chip appears and one click fills the category — **verified against the deterministic fake LLM response exercised in slices 5b/6's tests, not a live free model**; the opt-in OpenRouter round trip (4.10) is evidence-only, and a poor real-model answer is recorded as a known limitation (tune `Infrastructure/Llm/Prompts/*.txt` using logged `RawModelAnswer`, not the pipeline) that does NOT block phase completion; (b) clear the key / pull the network → silent failure, `AiStatusMessage` shows the right label, form stays usable, no dialog, no crash; (c) "Probar conexión" with a good key, a bad key, and no network gives three distinct messages.
- [ ] 8.3 Update `docs/architecture.md` with a Phase 13 entry following Phases 9–12's pattern (in addition to the §11 `TestConnectionAsync` amendment already made in 7.8).
- [ ] 8.4 Create `spec/phases/phase-13/features.md` following the pattern of `spec/phases/phase-10/`, `phase-11/`, `phase-12/`.
- [ ] 8.5 Archive the spec (`F-013-ai-pipeline-categorization.spec.md` → `.done.md`) and write `verify-report.md`/`archive-report.md` in `.atl/sdd/f-013p13-ai-pipeline/`, independently re-verifying every Acceptance Criteria checklist line against source and tests before archiving.
- [ ] 8.6 Update `docs/roadmap.md` §Fase 13: mark criteria (a)/(b)/(c) complete, recording the deterministic-fake-vs-live-model distinction from 8.2 explicitly so a future reader does not mistake the fake-backed pass for a live-model guarantee.
- [ ] 8.7 Update `docs/AI_HANDOFF.md` with the Phase 13 summary.
- [ ] 8.8 Record the design's three Open Questions as resolved in the archive/PR notes: (a) `docs/architecture.md` §11 amended inline in slice 7 (7.8), not via a new ADR; (b) the 7-slice/8-unit chain shipped as designed, not consolidated to 5; (c) roadmap criterion (a) gated on the deterministic fake response (8.2), with real-model quality tracked as a prompt-tuning follow-up, not a phase blocker.
