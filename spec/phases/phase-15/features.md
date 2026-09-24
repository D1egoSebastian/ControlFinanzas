# Phase 15 — Chat con IA sobre tus finanzas

> **Phase:** 15 — Chat con IA sobre tus finanzas
> **Status:** ✅ Completed (2026-09-23) — see `spec/features/phase-15/F-015-ai-finance-chat.done.md`
> **Spec ID:** F-015
> **Dependencies:** Phase 14 ✅ (must be CLOSED before apply — composite-query and guard patterns
> copied from it), Phase 13 ✅ (`ILLMClient`/`ILLMClientFactory`/`IPromptProvider` reused unmodified),
> Phase 5 ✅ (`ICurrencyConverter`), Phases 8/9/10/11/12 ✅ (month summary, category breakdown,
> budgets, goals, debts, upcoming payments queries — all reused, none modified)
> **Blocks:** None
> **Roadmap:** `docs/roadmap.md` §Fase 15
> **Stack:** .NET 8 · Avalonia 11.2.7 (MVVM strict) · EF Core 8 + SQLite (no migration this phase) ·
> reuses Phase 13's `ILLMClient`/resilience pipeline unmodified

## 1. Summary

Phase 15 gives the app a chat screen that answers plain-language finance questions
("¿cuánto gasté en comida este mes?") grounded in local data. A new composite Application query,
`BuildChatContextQuery`, computes every figure deterministically by composing six already-existing
queries (month summary, category breakdown — fetched **unfolded** so no category is ever hidden
beyond the dashboard's default top-5 —, budget vs. actual, goals, debts + schedule, upcoming
payments), each section degrading independently on failure. A pure static `ChatContextRenderer`
formats that snapshot into a culture-correct, name-sanitized prompt block capped at 6,000 characters
by construction, dropping whole sections in a fixed, tested order when needed. `AskFinanceChatUseCase`
copies `CategorizeExpenseUseCase`'s guard skeleton (`ISettingsStore` → `ILLMClientFactory.Resolve`,
zero HTTP calls when not configured) and is success-shaped for every outcome — rejected, not
configured, and every AI failure all resolve to `Result.Success(ChatReply)`, never an exception or
`Result.Failure`. The conversation lives in memory only, cleared via a new opt-in `INavigationAware`
hook because `NavigationService.GoBack` reactivates the same `ChatViewModel` instance rather than
resolving a new one.

Confirmed product decisions carried through unchanged: one static context snapshot per question, no
tool calling, no intent routing (D1); in-memory-only conversation, cleared on navigate-away/"Nueva
conversación"/restart (D2); question length 1–500 chars, rejected never truncated (D4); history replay
capped at 3 exchanges / 4,000 chars, oldest dropped first, failed/cancelled/banner turns never
replayed (D5); context budget 6,000 chars via per-section caps plus a fixed section-drop order (D6);
`Temperature ≤ 0.3`/`MaxTokens ≤ 500`/reply ≤ 4,000 chars regardless of `AppSettings` (D7); the
grounding rule appears verbatim in both locale prompts (D8); one question in flight, Stop cancels via
a per-invocation CTS (D11); the exact-figure panel renders from the DTO, never from LLM text (D12);
question text and API key are never logged, only length and correlation id (D13).

## 2. Model

```
Application/UseCases/Chat/
  ChatContextLimits                       — CurrentMonthCategories=30, PriorMonthCategories=10,
                                             Budgets=15, Goals=10, Debts=10, UpcomingPayments=10,
                                             UpcomingHorizonDays=30, MaxContextChars=6_000,
                                             MaxNameChars=80, UnfoldedTopCount=int.MaxValue

  BuildChatContext/
    ChatContextSnapshot                   — records for month totals, category lists, budgets,
                                             goals, debts, payments; null = unavailable (a step
                                             threw), empty list = "none" (a real answer)
    BuildChatContextQuery                 — composes 6 existing queries + GetDebtScheduleQuery,
                                             SEQUENTIAL (never WhenAll, same D-10 reasoning as
                                             Phase 14's GetDashboardInsightsQuery), each of 7
                                             sections has its own try/catch degrading to null;
                                             category breakdown called UNFOLDED (topCount =
                                             int.MaxValue), folded to 30/10 named lines + an
                                             overflow line by the builder itself; per-item
                                             ICurrencyConverter.ConvertAsync for goals/debts/
                                             payments, AmountDop = null on failure (never poisons
                                             a sum); "este mes" resolved from IClock, never a
                                             dashboard-selected month
    ChatContextRenderer                   — pure static (no DI, AnomalyNarrationParser precedent),
                                             culture-formatted (real CultureInfo, never hand-typed
                                             separators), SanitizeName strips control chars and a
                                             leading #/! run (closes a prompt-injection vector via
                                             user-entered names), fixed TrimOrder = [Payments,
                                             Debts, Goals, Budgets, PriorMonthCategories], never
                                             trims the current-month summary/category list
    ChatContextLabels                     — two-locale (es-DO/en) section-label table; prompt
                                             DATA, not UI copy — lives in Application on purpose

  AskFinanceChat/
    AskFinanceChatUseCase                 — CategorizeExpenseUseCase guard skeleton; question
                                             length check (1-500) precedes even the settings read;
                                             not-configured precedes context build AND the LLM
                                             call (zero DB reads, zero HTTP); 15s linked CTS,
                                             Stop-vs-timeout distinction identical to Phase 13/14;
                                             Temperature/MaxTokens clamped independent of
                                             AppSettings; history replay drops whole pairs oldest
                                             first; Result.Success(ChatReply) for every outcome,
                                             never Result.Failure
    ChatReply                             — Status (Answered/NotConfigured/Unavailable/Rejected),
                                             Text?, Snapshot? and Locale? (additive — feeds the
                                             exact-figure panel with the same data/culture that
                                             produced the prompt context)

Infrastructure/Llm/Prompts/
  chat-finance.system.{es-DO,en}.txt      — one placeholder ({{context}}), the D8 grounding
                                             sentence byte-identical in both locale files, plus an
                                             es-DO-only Spanish fallback sentence for the "no data"
                                             case

UI/
  Navigation/INavigationAware             — OnNavigatedTo/OnNavigatedFrom, opt-in hook wired into
                                             BOTH NavigationService.NavigateCore and GoBack (GoBack
                                             reactivates the same VM instance, the reason this hook
                                             exists at all); no-op for every pre-existing route
  Services/IClipboardService              — Task SetTextAsync(string); AvaloniaClipboardService
                                             resolves TopLevel.Clipboard via the classic desktop
                                             lifetime, no-ops with a Debug log when unavailable
  ViewModels/ChatViewModel                — sealed partial : ViewModelBase, INavigationAware;
                                             generation-token SendAsync/Stop/NewConversation/
                                             CopyAsync commands (ExpensesViewModel precedent, no
                                             debounce); explicit IsSending guard at the top of
                                             SendAsync in addition to the CanExecute gate (a real
                                             VM invariant, not only a disabled button — catches a
                                             programmatic Execute() bypassing CanExecute);
                                             _answeredTurns grows ONLY on Answered, so
                                             failed/cancelled/rejected/banner turns can never be
                                             replayed by construction
  ViewModels/ChatMessageItem              — Role/Text/State (Pending/Answered/Unavailable/
                                             Cancelled/Rejected/NotConfigured); IsNote drives muted
                                             bubble styling
  Views/ChatView.axaml                    — Grid *,320: left column transcript + input + a
                                             PERSISTENT (unconditional) privacy disclosure; right
                                             column exact-figure panel bound to VM properties
                                             populated only from the DTO
  Behaviors/ScrollToEndOnCollectionChanged — view-only attached property, auto-scroll on new
                                             message
  Converters/ChatMessage*Converter        — role→alignment, state→brush, role→label,
                                             state→copy-enabled (state-mapping only, keeps the
                                             view free of code-behind logic)
```

## 3. Slice plan

| ID | Slice | Likely PR | Status |
| --- | --- | --- | --- |
| S1 | Context builder | PR S1 | ✅ Completed |
| S2 | Renderer | PR S2 | ✅ Completed |
| S3 | Use case + prompts + DI | PR S3 | ✅ Completed |
| S4 | Navigation hook + clipboard port | PR S4 | ✅ Completed |
| S5 | ViewModel + strings | PR S5 | ✅ Completed |
| S6 | View + routing + docs | PR S6 | ✅ Completed |

Applied in 6 strictly-chained commits: `e400643` (S1 context builder), `9cd88da` (S2 renderer),
`ae16098` (S3 use case + grounding prompts), `59b15b9` (S4 navigation-aware hook + clipboard port),
`a05db68` (S5 finance chat view model), `574bd1a` (S6 finance chat view + routing), on top of the
Phase 14 archive commit `bcaf2a8`. Every slice was independently re-verified by the orchestrator after
landing. S3, S4, and S6 each carried a mandatory app smoke-launch before their gate-run closed, per
the design's own risk assessment (S3 wires the composite query/use case into DI; S4 modifies
`NavigationService.NavigateCore`/`GoBack`; S6 changes `RouteNames`, `_routes`, both `ViewLocator` arms,
and `ShellViewModel`'s sidebar — any of which can break app launch). No fix commit was required — the
`sdd-verify` adversarial reliability+risk review pass over the full phase diff found no confirmed
defects.

## 4. Phase completion criteria

- **(roadmap)** "¿cuánto gasté en comida este mes?" returns the exact current-month DOP figure, even
  when "Comida" ranks beyond the dashboard's top-5 default. ✅ — `BuildChatContextQuery` fetches the
  category breakdown with `topCount = int.MaxValue` (never `DashboardDefaults.TopCategories`), keeping
  up to 30 named lines; `BuildChatContextQueryTests` pins the exact line at rank 6 and rank 30.
- **(roadmap)** A question needing data outside the context gets an honest "no tengo ese dato", never
  an estimate. ✅ — the D8 grounding sentence is byte-identical in both locale prompt files and is
  asserted directly against the assembled `Messages[0]` system prompt in
  `AskFinanceChatUseCaseTests`.
- AI not configured → input/Send disabled, a persistent banner shown, zero `ILLMClient` calls. ✅ —
  `resolution.Status != Configured` precedes any client construction or context build in
  `AskFinanceChatUseCase.ExecuteAsync`.
- Any AI failure → localized "IA no disponible", never an exception reaching the UI. ✅ — every
  outcome of `ExecuteAsync` is `Result.Success(ChatReply)`; `ChatViewModel.SendAsync`'s own outer catch
  is defense-in-depth.
- Context ≤ 6,000 chars by construction, with a fixed, tested section-drop order; replay ≤ 6 messages/
  4,000 chars; `Temperature ≤ 0.3`; `MaxTokens ≤ 500`; question ≤ 500 chars, never truncated. ✅ —
  `ChatContextRenderer.Render`'s trim loop plus `Debug.Assert`; `AskFinanceChatUseCase`'s `Math.Min`
  clamps and length-check-before-truncation.
- Stop cancels the in-flight call with a cancellation note, never an error message; a cancelled turn
  is never replayed. ✅ — `ChatViewModel.Stop`/the caught `OperationCanceledException` branch;
  `_answeredTurns` only grows on `Answered`.
- A persistent privacy disclosure is visible near the input (unconditional, not gated on any state);
  the context contains no transaction descriptions, notes, tags, payment-method names, or individual
  transactions. ✅ — `ChatView.axaml`'s `PrivacyDisclosure` `TextBlock` has no `IsVisible` binding; a
  reflection test asserts no `ChatContextSnapshot` record exposes a `Note`/`Tags`/`PaymentMethod*`/
  `Counterparty*` property.
- The exact-figure side panel renders from the `ChatContextSnapshot` DTO, never from LLM text. ✅ —
  `ChatViewModel.ApplyFigures` reads only `reply.Snapshot`, never `reply.Text`.
- No hardcoded user-facing strings; new copy resolves through `Strings.resx`/`Strings.es-DO.resx` with
  hand-added `Strings.Designer.cs` accessors. ✅
- Zero changes to `ILLMClient`, `LlmRequest`, the adapters, resilience configuration, API-key storage,
  or any Phase 14 file. ✅ — confirmed via `git diff bcaf2a8..HEAD -- src/`: none of those files
  appear in the 32-file diff.
- Full test suite green, no regressions (1422/1422: 1154 unit + 268 integration, up from 1344/1344 at
  Phase 14's close). ✅

## 5. Findings carried forward, not fixed in this phase

- **`SendAsync`'s explicit `if (IsSending) return;` guard** (in addition to the `CanExecute`-gated
  `[RelayCommand]`) is required because `CommunityToolkit.Mvvm`'s generated `AsyncRelayCommand` does
  not itself re-check `CanExecute` before running the delegate when invoked directly — a caught hang
  in `ChatViewModelTests.SendCommand_SecondInvocationWhileSending_IsANoOp` during apply. Documented as
  a footgun for any future `[RelayCommand]` async method that must guarantee "at most one in flight" —
  the `CanExecute` predicate alone is not sufficient if the command can ever be invoked
  programmatically.
- **`AssemblyInfo.cs`'s new `[assembly: InternalsVisibleTo("ControlDeFinanzasIA.Tests.Unit")]`** on
  `ControlDeFinanzasIA.UI` exposes `NavigationService`'s internal test-only constructor overload (a
  custom route map) so `NavigationServiceTests` can exercise the real `NavigateCore`/`GoBack` code
  paths with a recording `INavigationAware` spy, without changing any production view-model's
  behavior. First use of `InternalsVisibleTo` in this codebase — a precedent for any future test that
  needs to exercise `NavigationService` end-to-end without a headless Avalonia harness.
- **`ChatContextRendererTests`' worst-case payload only needed 4 of the 5 `TrimOrder` sections dropped**
  to fit under 6,000 chars, not all 5 as the design's illustrative estimate assumed — a favorable
  deviation (less information discarded, same fixed order, same ≤6,000-char ceiling). A separate test
  pins a genuine partial-trim (3-of-5) case.
- **`BuildChatContextQuery` calls `GetCategoryBreakdownQuery` with `topCount = int.MaxValue`
  (`ChatContextLimits.UnfoldedTopCount`), not the literal `topCount = 30` a surface reading of R-2
  suggests.** This is deliberate: the query folds anything past `topCount` into a single `"Otras"`
  slice WITHOUT a folded count, so a literal `topCount = 30` call would lose the overflow count `N`
  that D6's `"Otras categorías (N)"` line requires. Fetching unfolded and letting the builder itself
  fold to 30 named lines plus a counted overflow line preserves both the "30 named categories" intent
  and the exact overflow count. Flagged so a future reader does not "fix" this back to a literal
  `topCount = 30`, which would silently break the overflow-count line.
- **`ViewLocator.Build`'s `ChatViewModel → ChatView` construction is not exercised by a unit test** —
  this repo has no headless Avalonia test harness (a gap Phases 8/12/13/14 already recorded), and
  constructing any `UserControl` outside a running `Avalonia.Application` fails `StaticResource`
  lookups. `ViewLocator.Match` (pure type check) and route resolution are unit-tested; the concrete
  view construction is exercised by the slice-6 smoke-launch instead.
- **Copy's clipboard write (`ChatViewModel.CopyAsync`) has no explicit try/catch** around
  `_clipboard.SetTextAsync` — reviewed in `verify-report.md` and found not to be a confirmed defect:
  `CommunityToolkit.Mvvm`'s `[RelayCommand]`-generated `AsyncRelayCommand.Execute` already discards the
  task (the same fire-and-forget shape every other async `[RelayCommand]` in this codebase uses), so an
  exception there cannot crash the app or reach the UI unhandled. Not expected to be "fixed" with an
  added try/catch — flagged here only so a future reader does not mistake the omission for an
  oversight.
- **No headless Avalonia test harness still exists** (same gap Phases 8/12/13/14 recorded). Not newly
  introduced by this phase.
- **Live-model prompt-tuning for `chat-finance.system.*` is a follow-up**, same caveat carried from
  Phase 13's categorization prompt and Phase 14's insights/anomaly-narration prompts — all tests use a
  deterministic fake `ILLMClient`; real-model response quality was not re-verified interactively as
  part of this SDD close (see `verify-report.md`'s "Manual verification note").

## 6. References

- `docs/roadmap.md` §Fase 15 — original plan, now marked complete
- `spec/features/phase-15/F-015-ai-finance-chat.done.md` — archived formal spec, evidence per
  acceptance criterion
- `.atl/sdd/f-015p15-ai-chat/{exploration,proposal,design,verify-report,archive-report}.md` — full
  traceability
- `docs/architecture.md` — Phase 15 entry
- `spec/features/phase-14/F-014-ai-insights-forecast-anomalies.done.md` — Phase 14, the composite-query
  and success-shaped-result patterns this phase copies
- `spec/features/phase-13/F-013-ai-pipeline-categorization.done.md` — Phase 13, the AI pipeline this
  phase reuses unmodified
