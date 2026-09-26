# Phase 16 — Plan de ahorro con IA para metas

> **Phase:** 16 — Plan de ahorro con IA para metas
> **Status:** ✅ Completed (2026-09-26) — see `spec/features/phase-16/F-016-ai-savings-plan.done.md`
> **Spec ID:** F-016
> **Dependencies:** Phase 11 ✅ (goals + `SavingsPlanCalculator`, unmodified), Phase 13 ✅
> (`ILLMClient`/`ILLMClientFactory`/`IPromptProvider` reused unmodified). No dependency on Phase 14/15
> code.
> **Blocks:** None
> **Roadmap:** `docs/roadmap.md` §Fase 16
> **Stack:** .NET 8 · Avalonia 11.2.7 (MVVM strict) · EF Core 8 + SQLite (no migration this phase) ·
> reuses Phase 13's `ILLMClient`/resilience pipeline unmodified

## 1. Summary

Phase 16 adds a "Pedir plan con IA" button to every goal row. `SavingsPlanCalculator` (Phase 11,
unmodified) still computes every figure; a new `GenerateSavingsPlanUseCase` reloads the goal and its
contributions from the repositories, recomputes the plan server-side, classifies it into exactly one
state (`Live`, `AlreadyMet`, or `TargetDateInPast` — `AlreadyMet` wins when both apply), and sends only
that state's figures to the LLM for a short, motivational explanation. A pure
`SavingsPlanNarrationGuard` extracts every digit run from the model's cleaned output and drops the
narration (falling back to `Unavailable`) if any run is not among the digit runs of the figures — plus
the sanitized goal name — actually sent, closing the loop against the model inventing a number. The
deterministic card figure never depends on AI: not configured, a provider failure, or a rejected
narration all leave the row's number exactly as `SavingsPlanCalculator` computed it, showing only a
localized fallback caption in the narration's place.

Confirmed product decisions carried through unchanged: no feasibility/surplus or category-trim
suggestions in v1 (D1/D2); inline button in the existing goal-row card, no new route (D3); the use
case takes only `GoalId` and never trusts a VM-supplied figure (D5); `Result.Success
(SavingsPlanNarrationDto(Status, Text?))` with `Status ∈ {Generated, NotConfigured, Unavailable}`, and
`Result.Failure` used only for `GoalNotFound` (D6); narration clears on every reload (D7); a per-row
`IsGeneratingPlan` guard with `AllowConcurrentExecutions = true` so one row's request never blocks
another (D8); no session/result cache — every click is a fresh call (D10); `MaxTokens ≤ 300`/
`Temperature ≤ 0.5` regardless of `AppSettings`, cleaned output capped at 600 chars (D11); the D12
prompt rule appears verbatim in both locale system prompts, built with `string.Replace` (never
`string.Format`), with the goal name sanitized and inserted last (D14).

## 2. Model

```
Application/UseCases/Goals/GenerateSavingsPlan/
  GenerateSavingsPlanUseCase              — sealed, ctor (IGoalRepository, IGoalContributionRepository,
                                             ISettingsStore, ILLMClientFactory, IPromptProvider, IClock,
                                             ILogger) — NO ICurrencyConverter (F-011 R-4 precedent);
                                             GetByIdAsync checked BEFORE the settings read (GoalNotFound
                                             reported even when AI is off); CategorizeExpenseUseCase
                                             guard skeleton (ISettingsStore → ILLMClientFactory.Resolve,
                                             zero calls when not configured); recomputes via
                                             Goal.CalculateProgress + SavingsPlanCalculator.Calculate(
                                             ..., IClock.Today); state = AlreadyMet > TargetDateInPast >
                                             Live; per-state figure set sent to the model (Live: monthly
                                             contribution/months remaining/remaining amount/target;
                                             AlreadyMet: target/contributed; TargetDateInPast: remaining
                                             amount/target date); 15s linked CTS, Stop-vs-timeout
                                             distinction identical to Phase 13/14/15; Temperature/
                                             MaxTokens clamped independent of AppSettings;
                                             Result.Success(SavingsPlanNarrationDto) for every AI
                                             outcome, Result.Failure only for GoalNotFound
  SavingsPlanNarrationDto                 — SavingsPlanNarrationStatus enum (Generated/NotConfigured/
                                             Unavailable, its own enum — Phase 14's NarrativeStatus is
                                             not reused), record(Status, Text?)
  SavingsPlanNarrationGuard                — pure static (no DI, AnomalyNarrationParser/
                                             ChatContextRenderer precedent); Clean() strips code fences,
                                             collapses 3+ newlines, trims, caps at 600 chars cutting at
                                             the last whitespace (never splits a figure); IsGrounded()
                                             extracts ASCII digit runs, normalizes leading zeros (05 ≡
                                             5), rejects any non-ASCII numeric character outright, and
                                             checks membership (not position) against the digit runs of
                                             the sent figures PLUS the sanitized goal name

Infrastructure/Llm/Prompts/
  generate-savings-plan.{system,user}.{es-DO,en}.txt — system prompt has no placeholders, the D12
                                             rule text byte-identical in both locales; user prompt has
                                             {{plan}} and {{goal_name}} placeholders, goal name replaced
                                             LAST so a name containing "{{plan}}" is never re-expanded

UI/
  ViewModels/GoalRow                       — record → ObservableObject; the 13-parameter constructor
                                             keeps its exact PascalCase names inside a scoped IDE1006
                                             suppression so existing named-argument test call sites
                                             compile unchanged; new observable PlanNarration/
                                             PlanNarrationStatusText/IsGeneratingPlan
  ViewModels/GoalsViewModel                — RequestPlanCommand, [RelayCommand(AllowConcurrentExecutions
                                             = true)] + per-row IsGeneratingPlan guard (a request in
                                             flight on one row never disables another row's button);
                                             LoadGoalsAsync unchanged — already rebuilds fresh GoalRow
                                             instances on every reload, so narration/IsGeneratingPlan
                                             never carries over by GoalId
  Views/GoalsView.axaml                    — "Pedir plan con IA" button + narration/fallback TextBlocks
                                             inside the existing row card, after the plan/state message
                                             block and before Select/Delete; existing tokens only
                                             (PaperBrush, PaperDimBrush, SageBrush, ledger-btn)
```

## 3. Slice plan

| ID | Slice | Likely PR | Status |
| --- | --- | --- | --- |
| S1 | Guard + DTO + prompts | PR S1 | ✅ Completed |
| S2 | Use case | PR S2 | ✅ Completed |
| S3 | Row + ViewModel | PR S3 | ✅ Completed |
| S4 | View + docs | PR S4 | ✅ Completed |

Applied in 4 strictly-chained commits: `536528c` (S1 guard + DTO + prompts), `1963842` (S2 use case),
`887b28d` (S3 row + ViewModel), `e9caef2` (S4 view + docs), on top of the Phase 15 archive commit
`0669c19`. Every commit was independently re-verified by the orchestrator after landing. S2 and S4 each
carried a mandatory app smoke-launch before their gate-run closed, per the tasks brief's own risk
assessment (S2 wires `GenerateSavingsPlanUseCase` into DI; S4 changes `GoalsView.axaml`, the last
remaining startup-risk surface). Phase 17 (in-app notifications) was applied concurrently in the same
working tree by other agents during this phase's apply and verify passes; the two phases share no
code path and Phase 16 references no Phase 17 type. No fix commit was required — the `sdd-verify`
adversarial reliability+risk review pass over the Phase-16-owned portion of the diff found no
confirmed defects.

## 4. Phase completion criteria

- **(roadmap)** Each goal shows a plan with the correct deterministic figure and, on request, a
  readable explanation. ✅ — `SavingsPlanCalculator`/`Goal.CalculateProgress` compute the figure once;
  the narration path never writes to any of `GoalRow`'s deterministic properties;
  `GenerateSavingsPlanUseCaseTests` covers the Generated path for all three states.
- AI off, unconfigured, or failing → the deterministic plan is shown exactly as before; only the
  narration spot shows a localized fallback caption; not configured → zero `ILLMClient` calls. ✅ —
  `resolution.Status != Configured` precedes both the contribution read and any client construction;
  `GoalsViewModelTests` pins the NotConfigured/Unavailable caption mapping with the figure unchanged.
- A narration containing any number not supplied to the AI is never displayed. ✅ —
  `SavingsPlanNarrationGuard.IsGrounded` rejects any digit run absent from the sent figures plus the
  sanitized goal name, logged at Warning with only the correlation id.
- Narration clears on reload; a double-click on the same row triggers one call; other rows stay usable
  while one row is generating. ✅ — `LoadGoalsAsync`'s existing `Goals.Clear()` + fresh-`GoalRow`
  rebuild satisfies this with no extra code; `[RelayCommand(AllowConcurrentExecutions = true)]` plus
  the per-row `IsGeneratingPlan` guard is asserted by a dedicated concurrent-rows VM test.
- A goal name containing an injection attempt does not change the assembled system prompt's fixed rule
  text. ✅ — `SanitizeGoalName` (control-char strip, `"` removal, 80-char cap) plus `Replace`-only,
  name-inserted-last prompt assembly; a use-case test asserts `Messages[0]` is byte-for-byte identical
  to the system template even for an injection-bearing name.
- Build clean, full suite green, `dotnet format style --verify-no-changes` exit 0, NetArchTest R-1..R-5
  green. ✅ — 1522/1522 (1236 unit + 286 integration, up from 1422/1422 at Phase 15's close).

## 5. Findings carried forward, not fixed in this phase

- **`SavingsPlanNarrationGuard.IsGrounded` is a membership check, not a positional one** (design's own
  accepted-cost table): a reply recombining sent digit groups (e.g., "345 DOP" when "12,345.67 DOP"
  was sent) is accepted, and a harmless unsent number ("3 pasos") is rejected as a false positive.
  Documented and accepted at design time — not to be "fixed" into a positional check, which the design
  explicitly rejected as unnecessary complexity for the risk it bounds.
- **The goal name's digit runs are included in the guard's allowed set** (Spec Reconciliation #1) —
  without this, a goal named "Carro 2027" would reject every narration that names it. Flagged so a
  future reader does not "simplify" the allowed set back to only the sent figures.
- **`GoalRow`'s scoped `#pragma warning disable IDE1006`** around its constructor is intentional (Spec
  Reconciliation #7): it preserves the PascalCase parameter names the pre-existing named-argument test
  call sites depend on. Not to be "cleaned up" to camelCase without also updating those tests.
- **A one-off `DashboardViewModelTests` failure batch** (12 tests, one run out of 5 full-suite runs
  during this phase's `sdd-verify` gate re-run) did not reproduce in 4 subsequent full-suite runs nor
  in 2 isolated re-runs of that test class alone. Phase 16 touches no Dashboard file — this looks like
  first-run JIT/cold-start timing sensitivity, not a regression introduced by this phase. Worth a
  dedicated investigation in a future session if it recurs.
- **No headless Avalonia test harness still exists** (same gap Phases 8/12/13/14/15 recorded). Not
  newly introduced by this phase.
- **Live-model prompt-tuning for `generate-savings-plan.system.*` is a follow-up**, same caveat carried
  from Phases 13/14/15's own prompts — all tests use a deterministic fake `ILLMClient`; real-model
  response quality was not re-verified interactively as part of this SDD close (see `verify-report.md`'s
  "Manual verification note").

## 6. References

- `docs/roadmap.md` §Fase 16 — original plan, now marked complete
- `spec/features/phase-16/F-016-ai-savings-plan.done.md` — archived formal spec, evidence per
  acceptance criterion
- `.atl/sdd/f-016p16-ai-savings-plan/{exploration,proposal,design,verify-report,archive-report}.md` —
  full traceability
- `docs/architecture.md` — Phase 16 entry
- `spec/features/phase-11/F-011-savings-goals.done.md` — Phase 11, the deterministic
  `SavingsPlanCalculator` this phase narrates over unmodified
- `spec/features/phase-13/F-013-ai-pipeline-categorization.done.md` — Phase 13, the AI pipeline this
  phase reuses unmodified
