# AGENTS.md

Ground truth for any agent working in this repo. Keep it accurate — when
the project state changes, update this file.

## Project at a glance

- **Name:** ControlDeFinanzasIA — cross-platform desktop app for personal
  finance control, AI-assisted, 100% local data.
- **Stack:** C# on **.NET 8 (LTS)**, **Avalonia** UI (MVVM), **SQLite** +
  **Entity Framework Core** (Code First, versioned migrations),
  `Microsoft.Extensions.DependencyInjection`, xUnit + FluentAssertions.
- **Solution:** `ControlDeFinanzasIA.sln` at the repo root.
- **Status (today):** Phases 0–16 fully archived. Phase 17 (in-app
  notifications) is next and already mid-apply, concurrently with Phase
  16's own verify/archive close — check `tasks/phase-17/*.tasks.md`'s
  checkbox state and `git log --oneline` for its real progress before
  continuing it; do not assume it is done. Phase 16 (AI savings plan for
  goals) shipped a "Pedir plan con IA" button on every goal row that asks
  the LLM for a short, motivational explanation of the exact deterministic
  figure `SavingsPlanCalculator` (Phase 11, unmodified) already computes.
  `GenerateSavingsPlanUseCase` takes only `GoalId`, reloads the goal and its
  contributions server-side, recomputes via `Goal.CalculateProgress` +
  `SavingsPlanCalculator.Calculate`, and classifies the plan into exactly
  one state (`Live`/`AlreadyMet`/`TargetDateInPast`, `AlreadyMet` wins when
  both apply) so the prompt only ever receives that state's figures. A pure
  `SavingsPlanNarrationGuard` extracts every digit run from the model's
  cleaned output and drops the narration to `Unavailable` if any run isn't
  among the figures — plus the sanitized goal name — actually sent, closing
  the loop against invented numbers (a membership check, not positional, a
  documented and accepted design limitation). `GoalRow` converted from an
  immutable record to an `ObservableObject` (keeping its exact 13-parameter
  PascalCase constructor via a scoped `IDE1006` suppression) so a per-row
  `IsGeneratingPlan` flag plus `[RelayCommand(AllowConcurrentExecutions =
  true)]` let one row's request run without disabling any other row's
  button. Not configured, a provider failure, or a guard rejection all
  leave the card's number untouched, showing only a localized fallback
  caption. 1522/1522 tests green (1236 unit + 286 integration — the
  integration count also includes Phase 17's concurrently-landed
  `Notification` persistence tests). See
  `spec/features/phase-16/F-016-ai-savings-plan.done.md`, `tasks/phase-16/`,
  and `.atl/sdd/f-016p16-ai-savings-plan/`.
  Phase 15 (chat with AI about your finances) shipped a chat screen that
  answers plain-language finance questions grounded in local data — every
  figure the model can quote is computed and formatted by deterministic
  Application code (`BuildChatContextQuery`, composing 6 already-existing
  queries, category breakdown fetched UNFOLDED so no category is ever
  hidden beyond the dashboard's top-5 default) before the call; the LLM
  only selects and phrases figures it was given. `ChatContextRenderer`
  keeps the rendered context ≤ 6,000 chars by construction, dropping whole
  sections in a fixed order when needed. `AskFinanceChatUseCase` copies
  `CategorizeExpenseUseCase`'s guard skeleton and is success-shaped for
  every outcome (rejected/not-configured/any AI failure — never
  `Result.Failure` or an uncaught exception). The conversation lives in
  memory only, cleared via a new opt-in `INavigationAware` hook because
  `NavigationService.GoBack` reactivates the same `ChatViewModel` instance
  instead of resolving a new one. The exact-figure side panel always
  renders from the DTO, never from LLM text. Phase 14 (AI insights,
  month-end forecast, and anomaly detection) shipped three AI-facing
  dashboard sections — a month-end forecast card, a persisted
  anomaly-alerts list with per-row dismiss, and an AI-written insights
  card — while keeping every number deterministic: two pure Domain
  services (`AnomalyDetector`, `MonthEndForecaster`) compute all figures
  with zero `ILLMClient` dependency, and the LLM only narrates figures it
  is handed via two success-shaped use cases that never fail (not
  configured/offline/error all resolve to a fallback payload, never an
  exception or `Result.Failure`). Phase 13 shipped the app's first
  outbound network dependency: `ILLMClient` +
  `OpenAICompatibleClient`/`AnthropicClient` adapters + a
  `Microsoft.Extensions.Http.Resilience`-backed pipeline (15s total timeout,
  1 retry, circuit breaker), spent on automatic expense categorization — typing
  a description suggests an existing category via a dismissable chip, with
  accepting the chip as the only code path that ever writes the category from
  AI output. Every failure mode (no key, no network, bad key, timeout,
  unparseable response) leaves the form fully usable with an honest status
  message, never an exception. 1422/1422 tests verde (1154 unit + 268
  integration). See `docs/AI_HANDOFF.md`, `spec/phases/phase-15/features.md`,
  `spec/phases/phase-14/features.md`, and `spec/phases/phase-13/features.md`
  for the full phase-13/14/15 detail and `spec/phases/phase-05/features.md`
  through `spec/phases/phase-12/features.md` for earlier phases.
- **Roadmap / architecture / vision:** `docs/roadmap.md`,
  `docs/architecture.md`, `docs/vision.md`.
- **Mission / stack constitution:** `spec/constitution/mission.md`,
  `spec/constitution/tech-stack.md`.

## Repository layout

```
ControlDeFinanzasIA.sln
Directory.Build.props         # common MSBuild props (analyzers, langversion, etc.)
Directory.Packages.props      # Central Package Management
stylecop.json                 # StyleCop config
.editorconfig
opencode.json                 # OpenCode agent + MCP config
src/
  ControlDeFinanzasIA.Application/   # use cases, DTOs, validators, interfaces
  ControlDeFinanzasIA.Domain/        # entities, value objects, domain rules
  ControlDeFinanzasIA.Infrastructure/ # EF Core, repositories, IO, LLM adapters
  ControlDeFinanzasIA.UI/            # Avalonia app (Views + ViewModels)
tests/
  ControlDeFinanzasIA.Tests.Unit/         # unit tests
  ControlDeFinanzasIA.Tests.Integration/  # EF Core + SQLite in-memory tests
spec/
  constitution/   # mission, tech-stack (do not bend without ADR)
  phases/         # per-phase feature decomposition
  features/       # per-feature specs (.spec.md active, .done.md archived)
tasks/            # per-feature task breakdowns (mirror spec structure)
docs/             # vision, architecture, roadmap, ADRs
.claude/skills/   # project-local skills (e.g. frontend-design)
.atl/             # Gentle AI orchestration state
```

## Common commands

```bash
# Build
dotnet build -c Release

# Run the app
dotnet run --project src/ControlDeFinanzasIA.UI

# Run all tests
dotnet test -c Release --no-build

# Format check (CI gate)
dotnet format style --verify-no-changes

# EF Core migrations (after installing dotnet-ef 8.0.11 locally)
dotnet ef migrations add <Name> \
  --project src/ControlDeFinanzasIA.Infrastructure \
  --startup-project src/ControlDeFinanzasIA.UI
```

## MCP

- **Context7** (remote MCP) is configured in the user-level
  `~/.config/opencode/opencode.json` for up-to-date library docs
  (Avalonia, EF Core, xUnit, etc.). The project-level `opencode.json`
  only defines the `agent` section. Set `CONTEXT7_API_KEY` to lift the
  free-tier rate limit. Use Context7 whenever introducing or upgrading
  a library or pattern.
- **Engram** provides persistent memory across sessions, also
  configured at the user level. Memory is organized by topic keys under
  `sdd/...`. Do NOT replace it with OpenSpec — the project uses
  filesystem-based specs.

## What to do before coding

1. **Read the spec first.** Any change starts at `spec/`. For a new
   feature, the canonical inputs are `spec/constitution/*` (mission +
   stack) and the active phase plan in `spec/phases/<phase>/features.md`.
2. **Follow the SDD cycle** for non-trivial work:
   `propose → spec → tasks → apply → verify → archive`. Specs live as
   `spec/features/<phase>/<F-id>-<slug>.spec.md` and get renamed to
   `.done.md` only after archive.
3. **Respect the architecture.** Domain owns invariants, Application
   owns use cases, Infrastructure owns EF Core / IO, UI owns Views and
   ViewModels. Cross-layer references are NetArchTest violations
   (rules R-1..R-5 enforced in `tests/`).
4. **Use filesystem specs, not OpenSpec.** There is intentionally no
   `openspec/` directory in this repo.

## Guidance

- **Spec-Anchored Development:** Before writing code, the agent MUST
  read `spec/` to understand the mission and the stack. The spec is
  the source of truth, not the conversation.
- **Design Lead:** For any visual / UI change, invoke the
  `.claude/skills/frontend-design` skill. No generic AI aesthetics —
  design tokens are centralized, MVVM is strict, no logic in
  code-behind.
- **Context Hygiene:** If a task is long, run `/compact` rather than
  letting the agent's reasoning drift. Prefer reading the spec over
  re-deriving rules from code.
- **Verification:** After implementing, the agent MUST verify against
  the feature's **Acceptance Criteria** (`Acceptance Criteria` section
  in the spec). Gates: `dotnet build` clean, `dotnet test` green,
  `dotnet format style --verify-no-changes` exit 0, NetArchTest R-1..R-5
  green, spec renamed to `.done.md`.

## Conventions for code and PRs

- **MVVM strict** in Avalonia. No logic in code-behind. ViewModels are
  testable without a UI.
- **Async/await end-to-end** for every I/O (DB, network, file).
- **Errors as values** (`Result<T>` from Phase 1 F-005) or typed
  exceptions. Never silent failures.
- **No remote telemetry. No secrets in the repo.** User API keys for the
  LLM live in the local data file, never in source.
- **EF Core migrations are versioned and committed.** Destructive schema
  changes need an explicit migration and, where applicable, a backup.
- **Localization:** Spanish (es-DO default) and English (en). Use
  `IStringLocalizer`; do not hardcode UI strings.
- **Conventional commits.** No "Co-Authored-By: AI" trailers.
- **Architecture rules** (enforced by NetArchTest, do not bypass):
  - R-1: Domain has no outward dependencies.
  - R-2: Application depends only on Domain (and abstractions).
  - R-3: Infrastructure depends on Application + Domain; no UI reference.
  - R-4: UI depends on Application + Domain; no Infrastructure
    reference from Views (only via composition root).
  - R-5: Tests may reference anything but must not ship in the app.

## File conventions

- New specs go under `spec/features/<phase>/` as
  `<F-id>-<kebab-slug>.spec.md`. Rename to `.done.md` only after
  archive.
- Task breakdowns go under `tasks/<phase>/` as
  `<F-id>-<kebab-slug>.tasks.md`.
- Project config lives in `Directory.Build.props` and
  `Directory.Packages.props`. Do not hardcode versions in `.csproj`
  files — Central Package Management is mandatory.
- When this file's facts drift from reality, **update this file** as
  part of the same change.
