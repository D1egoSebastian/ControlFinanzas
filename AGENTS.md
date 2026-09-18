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
- **Status (today):** Phases 1, 2, 3, 4, and **5 fully archived**. Phase 6 is in progress:
  F6-001/F6-002 are complete, while F6-003 persistence and the F6-004
  Subscription create/update demo slice and initial Subscription UI are
  implemented; generation and FixedExpense remain pending.
  Phase 3 slice vertical of `Expense` complete and verified. Phase 4
  UI capstone F-008 delivered via 4 chained PRs + 1 fix-PR with
  BOLD "Libro Mayor" design pass (verdict PASS WITH WARNINGS, 5 minor
  non-blocking warnings). Phase 5 complete — multi-moneda DOP/USD
  with stub `IExchangeRateProvider` + `ICurrencyConverter` cache,
  `PaymentMethodId` threaded end-to-end (entity → migration →
  commands/validators → DTOs → repos with Include → filters →
  query catalog → form picker → multi-select filter rail → second-line
  conversion display). 559/559 tests verde. See
  `spec/phases/phase-05/features.md` for the Phase 5 plan and
  roadmap.
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
