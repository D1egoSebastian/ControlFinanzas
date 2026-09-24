# Tasks — F-015 Chat con IA sobre tus finanzas

> **Apply gate:** `sdd-apply` MUST NOT start any slice of this phase until Phase 14
> (`F-014-ai-insights-forecast-anomalies`) is fully archived (spec renamed to `.done.md`,
> `verify-report.md`/`archive-report.md` written). Both the proposal and the design name this as a
> hard precondition — S3's `FakeLlmClient.OnCompleteAsync`/`FakePromptProvider.Templates` extensions
> and the shared `PromptNames.cs`/`EmbeddedPromptProviderTests`/`Bootstrap.cs`/resx touch points
> assume Phase 14 has already landed those files.

## Review Workload Forecast

| Field | Value |
|---|---|
| Estimated changed lines | ≈2,480 total across 6 strictly chained work units (design's own per-slice estimates) |
| 400-line budget risk | High — S1 (~480), S3 (~500), S5 (~480), S6 (~420) all exceed the 400-line budget; only S4 (~180) is under |
| Chained PRs recommended | Yes |
| Suggested split | PR S1 (context builder) → PR S2 (renderer) → PR S3 (use case + prompts + DI) → PR S4 (navigation hook + clipboard port) → PR S5 (ViewModel + strings) → PR S6 (view + routing + docs) |
| Delivery strategy | auto-chain |
| Chain strategy | stacked-to-main |

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: High

The design's `## Recommended Slicing` is authoritative and strictly chained — S1 → S2 → S3 → S4 → S5
→ S6, each additive, no unit exceeding ~500 lines. S1 (context builder) has no in-phase dependency
and unblocks every downstream consumer of `ChatContextSnapshot`. S2 (renderer) depends on S1's
records only. S3 (use case + prompts + DI) is the first startup-risk slice — it wires
`BuildChatContextQuery`/`AskFinanceChatUseCase` into `Bootstrap.cs` — so it carries a mandatory
smoke-launch. S4 (navigation hook + clipboard port) is independent of S3 on paper but stays chained
after it per the design's own order; it is also a startup-risk slice because it modifies
`NavigationService.NavigateCore`/`GoBack`, so it also gets a smoke-launch. S5 (ViewModel) depends on
S3 (use case) and S4 (ports) together. S6 (view + routing) is the last and largest startup-risk
slice — it changes `RouteNames`, `NavigationService._routes`, both `ViewLocator` arms, and
`ShellViewModel`'s sidebar, any of which can break app launch — so it gets the full routing checklist
plus a mandatory smoke-launch.

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|---|---|---|---|
| S1 | Context builder | PR S1 | Base = main. `ChatContextLimits`, `ChatContextSnapshot` records, `BuildChatContextQuery` (7 sections, caps, unfolded fold, DOP conversion, per-section degrade). |
| S2 | Renderer | PR S2 | Base = main (PR S1 merged). `ChatContextRenderer`, `ChatContextLabels`, formatting/sanitize/fixed trim order. |
| S3 | Use case + prompts + DI | PR S3 | Base = main (PR S2 merged). `AskFinanceChatUseCase` + DTOs, `PromptNames` +1, two `.txt` prompts, `EmbeddedPromptProviderTests` +1, Fase 15 DI block (query + use case). **Smoke-launch required.** |
| S4 | Navigation hook + clipboard port | PR S4 | Base = main (PR S3 merged). `INavigationAware` + `NavigationService` hook, `IClipboardService` + Avalonia impl + fake + DI. **Smoke-launch required.** |
| S5 | ViewModel + strings | PR S5 | Base = main (PR S4 merged). `ChatMessageItem`, `ChatViewModel`, VM-facing resx keys + Designer accessors, VM tests. |
| S6 | View + routing + docs | PR S6 | Base = main (PR S5 merged). `ChatView.axaml`, scroll behavior, view-facing resx keys, full routing checklist, VM DI, frontend-design pass, `AGENTS.md`. **Smoke-launch required.** |

## 1. Context builder

Depends on nothing (Phase 14 archived — see gate note above). Goal: `ChatContextSnapshot` exists,
fully populated by a composite over the existing Phase 8–12 queries, with per-section degradation and
item caps already applied.

- [x] 1.1 Write failing `tests/…Unit/UseCases/Chat/BuildChatContextQueryTests.cs`: S-1 ("Comida"
  ranked 6th and again at rank 30 keeps its exact DOP figure); 31 categories → 30 named lines +
  `Otras categorías (1)` with the exact overflow amount; prior-month categories capped at 10; budgets
  capped at 15 in query order; goals `Status == Active` only, ≤ 10; debts `Active` only, ≤ 10, pending
  balance via `Debt.CalculatePendingBalance`, next due date from min `Pending.ScheduledDate`; payments
  ≤ 10; a throwing repo/query per section leaves that section `null` and the rest intact; caller
  cancellation rethrows `OperationCanceledException` unchanged; a missing USD rate sets
  `HasMissingRates = true` and that item's `AmountDop == null` without poisoning any sum (S-9); the
  privacy reflection test asserting no snapshot record exposes a `Note`/`Tags`/`PaymentMethod*`/
  `Counterparty*` property (S-10). (R-2, D-1, D-2, D-3, S-1, S-9, S-10)
- [x] 1.2 Create `src/…Application/UseCases/Chat/ChatContextLimits.cs` (`CurrentMonthCategories = 30`,
  `PriorMonthCategories = 10`, `Budgets = 15`, `Goals = 10`, `Debts = 10`, `UpcomingPayments = 10`,
  `UpcomingHorizonDays = 30`, `MaxContextChars = 6_000`, `MaxNameChars = 80`,
  `UnfoldedTopCount = int.MaxValue`). (D-1)
- [x] 1.3 Create `src/…Application/UseCases/Chat/BuildChatContext/ChatContextSnapshot.cs`
  (`ChatAmount`, `ChatMonthTotals`, `ChatCategoryLine`, `ChatCategoryList`, `ChatBudgetLine`,
  `ChatGoalLine`, `ChatDebtLine`, `ChatPaymentLine`, `ChatContextSnapshot`, `ChatContextSection`) —
  `null` means unavailable, empty list means "none". (R-2, D-2)
- [x] 1.4 Create `src/…Application/UseCases/Chat/BuildChatContext/BuildChatContextQuery.cs`: 7
  sequential sections (never `WhenAll`), each with its own try/catch degrading to `null`; category
  breakdown called with `topCount = UnfoldedTopCount` and folded to 30 named lines +
  `OverflowCount`/`OverflowAmountDop` by the builder itself; goal/debt lists capped **before**
  per-debt `GetDebtScheduleQuery` reads; per-item `ICurrencyConverter.ConvertAsync` for goals, debts,
  payments with `AmountDop = null` on failure; "este mes" resolved from `IClock.Today`, never a
  dashboard month. Run 1.1 green. (R-2, D-1, D-2, D-3)
- [x] 1.5 Run all four gates: `dotnet build -c Release`, `dotnet test -c Release --no-build`,
  `dotnet format style --verify-no-changes`, NetArchTest filter. All green before opening PR S1.

## 2. Renderer

Depends on 1. Goal: a pure, culture-formatted renderer that stays ≤ 6,000 chars by construction, with
a fixed, test-pinned section trim order.

- [x] 2.1 Write failing `tests/…Unit/UseCases/Chat/ChatContextRendererTests.cs`: the exact "Comida"
  line built independently for `CultureInfo("es-DO")` and `CultureInfo("en")` (never hand-typed
  separators); empty vs. unavailable vs. trimmed-omitted section text; fixed trim order (S-5) — each
  step drops exactly the next `TrimOrder` section (payments → debts → goals → budgets → prior-month
  categories) and replaces it with a one-line "omitted" marker; the worst-case synthetic payload (30
  × 80-char names, `999,999,999.99` amounts, every section at cap) stays `Text.Length <= 6_000` with
  the current-month summary and category list intact and all five trimmable sections omitted, in
  order; `SanitizeName` collapses control chars/newlines, strips a leading `#`/`!` run, caps at 80
  chars with `…`; the `!!` flag line appears if and only if `HasMissingRates`. (R-3, D-4, S-5)

  **Deviation:** with this renderer's actual per-line format overhead, the fully-capped worst-case
  payload only needs the first FOUR `TrimOrder` sections dropped (payments, debts, goals, budgets) —
  not all five — to fit under 6,000 chars. `PriorMonthCategories` is capped at 10 items and, even
  fully maxed (80-char names), can never by itself push `baseline + PriorMonthCategories` back over
  budget once the other four sections are gone. This is documented in the test
  (`Render_WorstCaseSyntheticPayload_StaysUnderBudgetWithNeverTrimmedSectionsIntact` +
  `Render_WorstCasePriorCategoriesAloneNeverNeedsTrimming`) and is a favorable deviation: less
  information is discarded than design.md's illustrative estimate assumed, while the ≤6,000-char
  ceiling and the fixed drop order still hold exactly. A separate test
  (`Render_OverBudgetFromGoalsAndDebts_TrimsOnlyPaymentsDebtsGoalsInFixedOrder`) pins a genuine
  partial-trim case (3 of 5 sections dropped, in order) to prove the loop stops as soon as the budget
  is satisfied.
- [x] 2.2 Create `src/…Application/UseCases/Chat/BuildChatContext/ChatContextLabels.cs`: a static
  two-locale (`es-DO`/`en`) section-label table — prompt data, not UI copy. (D-4)
- [x] 2.3 Create `src/…Application/UseCases/Chat/BuildChatContext/ChatContextRenderer.cs`: a pure
  static class (no DI registration), `TrimOrder` array, `Render`, `FormatDop`, `SanitizeName`;
  per-section caps enforced first, then the fixed drop-and-rejoin loop while
  `text.Length > MaxContextChars`; header/flag line/both summaries/current-month category list never
  touched. Run 2.1 green. (R-3, D-4)
- [x] 2.4 Run all four gates. All green before opening PR S2.

## 3. Use case + prompts + DI

Depends on 2. Goal: `AskFinanceChatUseCase` is success-shaped for every outcome (never
`Result.Failure`, never an uncaught exception), the grounding prompt exists in both locales, and the
Application-side DI wiring resolves cleanly.

- [x] 3.1 Write failing `tests/…Unit/UseCases/Chat/AskFinanceChatUseCaseTests.cs`: 0-char and
  501-char trimmed questions → `Rejected`, zero `ILLMClient` calls, no settings read (S-3); not
  configured → `NotConfigured`, zero calls, no context build (S-2); the exact assembled `Messages`
  list (roles/order), with the D8 grounding sentence present verbatim in `Messages[0]` for both
  `es-DO` and `en` (S-8); `Temperature = min(0.7, 0.3) = 0.3` and `min(0.1, 0.3) = 0.1`;
  `MaxTokens = min(2000, 500)` and `min(200, 500)`; replay after 10 and 20 prior turns → ≤ 6 messages,
  ≤ 4,000 total chars, alternating User/Assistant, oldest pair dropped first (S-4); assistant replay
  entries capped at 600 chars; `LlmException` → `Unavailable` carrying the snapshot (S-7); a
  never-completing client past the 15 s `HardTimeout` → `Unavailable`; caller-initiated cancellation
  rethrows `OperationCanceledException` unchanged; empty response content → `Unavailable`; reply
  capped at 4,000 chars; a sentinel question string never appears in any log message/argument;
  `GetAvailabilityAsync` for configured and unconfigured settings. (R-4, R-5, R-6, R-7, R-8, D-5, S-2,
  S-3, S-4, S-7, S-8)
- [x] 3.2 Modify `tests/…Unit/TestDoubles/FakeLlmClient.cs`/`FakePromptProvider.cs` additively
  (`OnCompleteAsync`, `Templates`) only if the Phase 14 extensions are not already present in the
  tree — verify first; no existing test changes either way. (D-13 coexistence note)
- [x] 3.3 Create `src/…Application/UseCases/Chat/AskFinanceChat/{AskFinanceChatUseCase,
  AskFinanceChatCommand,ChatReply}.cs` (`ChatReply.cs` holds `ChatTurn`, `ChatReplyStatus`,
  `ChatAvailability`): the `CategorizeExpenseUseCase` guard skeleton (`ISettingsStore` →
  `ILLMClientFactory.Resolve`), the linked 15 s CTS, `Result.Success(ChatReply)` for every outcome.
  Run 3.1 green. (R-4, R-5, R-6, R-7, R-8, D-5)
- [x] 3.4 Modify `src/…Application/Llm/PromptNames.cs`: append `ChatFinanceSystem =
  "chat-finance.system"`. Create `src/…Infrastructure/Llm/Prompts/chat-finance.system.{es-DO,en}.txt`
  with the `{{context}}` placeholder and the D8 grounding sentence byte-identical in both locales
  (plus the es-DO Spanish fallback sentence). No `.csproj` change (already globbed). (R-8, D-6)
- [x] 3.5 Modify `tests/…Integration/Llm/EmbeddedPromptProviderTests.cs`: add
  `ChatFinanceSystem` to `_allPromptNames`. Confirm green. (D-6)
- [x] 3.6 Modify `src/…UI/Composition/Bootstrap.cs`: add the Fase 15 block —
  `AddTransient<BuildChatContextQuery>()`, `AddTransient<AskFinanceChatUseCase>()`. (D-12)
- [x] 3.7 Smoke-launch the app (`dotnet run --project src/ControlDeFinanzasIA.UI`) against an
  existing dev DB: confirm `BuildChatContextQuery` and `AskFinanceChatUseCase` resolve from DI with no
  startup failure. Screenshot only, no simulated clicks.
- [x] 3.8 Run all four gates. All green before opening PR S3.

## 4. Navigation hook + clipboard port

Depends on 3. Goal: a reliable navigate-away signal for VMs that `GoBack` re-activates, plus a
UI-layer clipboard port with no new Application/Infrastructure coupling.

- [x] 4.1 Write failing `tests/…Unit/UI/NavigationServiceTests.cs` additions: `OnNavigatedFrom` fires
  on both `NavigateTo` and `GoBack`; `OnNavigatedTo` fires after the new VM becomes
  `CurrentViewModel`; a non-`INavigationAware` VM is unaffected — using a recording test VM. (D-7)
- [x] 4.2 Create `src/…UI/Navigation/INavigationAware.cs` (`OnNavigatedTo`/`OnNavigatedFrom`). Modify
  `src/…UI/Navigation/NavigationService.cs`: call `(CurrentViewModel as
  INavigationAware)?.OnNavigatedFrom()` before switching and `(newVm as
  INavigationAware)?.OnNavigatedTo()` after `OnCurrentChanged()`, in both `NavigateCore` and `GoBack`.
  Run 4.1 green. (D-7)
- [x] 4.3 Create `tests/…Unit/TestDoubles/FakeClipboardService.cs`: records the last `SetTextAsync`
  call. (D-8, D-13)
- [x] 4.4 Create `src/…UI/Services/IClipboardService.cs` (`Task SetTextAsync(string text)`) and
  `src/…UI/Services/AvaloniaClipboardService.cs`: resolves `TopLevel.GetTopLevel(mainWindow)
  ?.Clipboard` via `IClassicDesktopStyleApplicationLifetime`; no-op with a Debug log when unavailable.
  (D-8, R-13)
- [x] 4.5 Modify `src/…UI/Composition/Bootstrap.cs`: `AddSingleton<IClipboardService,
  AvaloniaClipboardService>()`. (D-8, D-12)
- [x] 4.6 Smoke-launch the app: confirm `IClipboardService` resolves from DI and existing navigation
  (sidebar routes, back) still works with no startup failure. Screenshot only, no simulated clicks.
- [x] 4.7 Run all four gates. All green before opening PR S4.

## 5. ViewModel + strings

Depends on 4. Goal: `ChatViewModel` drives the full send/stop/copy/reset flow against the real use
case and ports, with generation-token guards matching the `ExpensesViewModel` precedent.

- [ ] 5.1 Write failing `tests/…Unit/UI/ChatViewModelTests.cs`: `Send` appends a user bubble then an
  assistant bubble; `_answeredTurns` grows only on `Answered`; `SendCommand` is disabled while
  sending (a second `Execute` is a no-op); `Stop` produces a localized cancellation note (not an
  error) and the cancelled turn is excluded from the next request's replayed `LlmRequest` (S-6);
  not-configured → `ShowNotConfiguredBanner == true`, `SendCommand.CanExecute == false`, zero calls
  (S-2); `Unavailable` → localized note, no exception reaches the VM; `CopyAsync` sends the exact
  displayed bubble text to the fake clipboard (S-11); `OnNavigatedFrom` mid-flight cancels, clears
  everything, and a late completion is dropped by the generation-token check (S-12); the figures panel
  values equal `ChatContextRenderer.FormatDop` of the snapshot's current-month totals. (R-9, R-10,
  R-11, R-12, R-13, D-9, S-2, S-6, S-9, S-11, S-12)
- [ ] 5.2 Create `src/…UI/ViewModels/ChatMessageItem.cs`: `Role`, observable `Text`, observable
  `State` (`Pending`/`Answered`/`Unavailable`/`Cancelled`/`Rejected`/`NotConfigured`),
  `IsNote` derived property. (D-9)
- [ ] 5.3 Create `src/…UI/ViewModels/ChatViewModel.cs`: `sealed partial class : ViewModelBase,
  INavigationAware`; ctor `AskFinanceChatUseCase, IClipboardService, IStringLocalizer<Strings>,
  ILogger<ChatViewModel>`; `Messages`/`FigureLines` collections; `_answeredTurns` as the only replay
  source; generation-token `SendAsync`/`Stop`/`NewConversation`/`CopyAsync` commands;
  `ResetConversation()`; `OnNavigatedTo()` calling `GetAvailabilityAsync`; `ApplyFigures` using
  `ChatContextRenderer.FormatDop` with the reply's `Locale`. Run 5.1 green. (R-9, R-10, R-11, R-12,
  R-13, D-9)
- [ ] 5.4 Add VM-facing keys to `Strings.resx`/`Strings.es-DO.resx` + hand-added
  `Strings.Designer.cs` accessors: `ChatThinking`, `ChatAiNotConfiguredBanner`, `ChatAiUnavailable`,
  `ChatCancelledNote`, `ChatQuestionRejected`, `ChatPrivacyDisclosureFormat`, `ChatYouLabel`,
  `ChatAssistantLabel`, `ChatFiguresTitle`, `ChatFiguresExpensesLabel`, `ChatFiguresIncomesLabel`,
  `ChatFiguresNetLabel`, `ChatFiguresMissingRates`, `ChatFiguresEmpty`. (R-15, D-11)
- [ ] 5.5 Run all four gates. All green before opening PR S5.

## 6. View + routing + docs

Depends on 5. Goal: the chat screen is reachable from the sidebar with tokens-only styling, the full
routing checklist is wired, and the project docs reflect the new phase.

- [ ] 6.1 Invoke the `.claude/skills/frontend-design` skill for `ChatView` before touching XAML —
  design tokens only: `SurfaceBrush`, `RadiusMedium`, `ledger-section`/`ledger-row`/`ledger-amount`,
  `AmberBrush` (banner), `SageBrush`/`TerracottaBrush` (message-state accents); no new tokens. (D-10)
- [ ] 6.2 Write failing routing tests in `tests/…Unit/UI/{NavigationServiceTests,
  ShellViewModelTests}.cs`: `RouteNames.Chat` resolves to `ChatViewModel`; `ViewLocator.Match`
  returns `true` and `ViewLocator.Build` returns `ChatView`; the sidebar item count increases by one
  and the "Chat" item highlights when active. (R-1)
- [ ] 6.3 **Full routing checklist**: modify `src/…UI/Navigation/RouteNames.cs` (add
  `Chat = "Chat"`, matching the `ChatViewModel` name literal); modify
  `src/…UI/Navigation/NavigationService.cs` (`_routes` entry); modify `src/…UI/Views/ViewLocator.cs`
  (**both** the `Build` arm and the `Match` arm); modify `src/…UI/ViewModels/ShellViewModel.cs`
  (sidebar item placed before Settings, resx-backed label). Run 6.2 green. (R-1)
- [ ] 6.4 Add view-facing keys to `Strings.resx`/`Strings.es-DO.resx` + hand-added
  `Strings.Designer.cs` accessors: `SidebarChat`, `ChatTitle`, `ChatNewConversationButton`,
  `ChatInputPlaceholder`, `ChatSendButton`, `ChatStopButton`, `ChatCopyButton`, `ChatEmptyState`.
  (R-15, D-11)
- [ ] 6.5 Create `src/…UI/Views/ChatView.axaml` (+ empty `.axaml.cs`), compiled bindings
  (`x:DataType="vm:ChatViewModel"`), `Grid ColumnDefinitions="*,320"`: left column header +
  `NewConversation` button, banner `Border` bound to `ShowNotConfiguredBanner`, `ScrollViewer`/
  `ItemsControl` over `Messages` with role-aligned bubbles + per-bubble Copy button, input row
  (`TextBox MaxLength="500"`, `IsEnabled="{Binding IsInputEnabled}"`, Enter → `SendCommand`, Send/Stop
  buttons), permanent privacy caption; right column figures panel (title, month label, three totals,
  category list, missing-rates caption). No logic in code-behind. (R-10, R-11, D-10)
- [ ] 6.6 Create `src/…UI/Behaviors/ScrollToEndOnCollectionChanged.cs`: view-only attached property
  that auto-scrolls when `Messages` changes. (D-10)
- [ ] 6.7 Modify `src/…UI/Composition/Bootstrap.cs`: `services.AddTransient<ChatViewModel>();`.
  (D-12)
- [ ] 6.8 Smoke-launch the app: navigate to Chat via the sidebar, confirm the view renders with no
  DI/startup failure, and the unconfigured banner shows when AI is not configured. Screenshot only, no
  simulated clicks.
- [ ] 6.9 Modify `AGENTS.md`: update the Status line to record Phase 15 status, mirroring the existing
  per-phase status-line pattern.
- [ ] 6.10 Run all four gates. All green before opening PR S6.

## 7. Cierre

- [ ] 7.1 Run the full gate suite across the whole phase: build, `dotnet test` (unit + integration),
  `dotnet format style --verify-no-changes`, NetArchTest architecture filter. All green, no
  regressions. Confirm zero changes to `ILLMClient`, `LlmRequest`, the adapters, resilience
  configuration, API-key storage, or any Phase 14 file (Out of Scope, R-16).
- [ ] 7.2 Run the app for real against an existing dev DB: (a) ask "¿cuánto gasté en comida este mes?"
  and confirm the exact current-month DOP figure, even when "Comida" ranks beyond top 5; (b) ask a
  question outside the context and confirm the localized "no tengo ese dato" reply; (c) clear the AI
  key and confirm input/Send stay disabled with zero calls; (d) issue Stop on a pending question and
  confirm a cancellation note, not an error; (e) navigate away and back and confirm the conversation
  is empty; (f) confirm the figures side panel matches the context DTO, not LLM text.
- [ ] 7.3 Update `docs/architecture.md` with a Phase 15 entry following Phases 9–14's pattern.
- [ ] 7.4 Create `spec/phases/phase-15/features.md` following the pattern of
  `spec/phases/phase-10/` through `phase-14/`.
- [ ] 7.5 Archive the spec (`F-015-ai-finance-chat.spec.md` → `.done.md`) and write
  `verify-report.md`/`archive-report.md` in `.atl/sdd/f-015p15-ai-chat/`, independently re-verifying
  every Acceptance Criteria checklist line against source and tests before archiving — including the
  five accepted Reconciliation points (unfolded category fetch, `ChatReply.Snapshot`/`Locale`,
  `INavigationAware` clearing, culture-derived number formatting, static renderer + prompt-data
  labels).
- [ ] 7.6 Update `docs/roadmap.md` §Fase 15: mark the phase's criteria complete.
- [ ] 7.7 Update `docs/AI_HANDOFF.md` with the Phase 15 summary, including the `topCount =
  int.MaxValue` (unfolded fetch) deviation from a literal R-2 reading, so a future reader does not
  "fix" it.
