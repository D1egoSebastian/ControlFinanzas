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
- **Status (today):** Phases 0–21 fully archived. Phase 21 (packaging,
  polish, accessibility — the last roadmap phase) is archived: its spec is
  `spec/features/phase-21/F-021-packaging-polish-a11y.done.md`. The roadmap
  has no phase after 21; what remains is the owner's manual checks, the tag
  `v1.0.0` (it does not exist; create it only when the owner asks) and the
  first real release run. Verify history: independent verification PASSED
  WITH WARNINGS (0 critical, 6 warnings, 6 suggestions); W2, W3, W4, W6 and
  the README wording were fixed in `c11fb6e` and `ac181ca`; W1
  (`release.yml` never parsed as YAML, task 9.9 unticked) and W5 (the
  keyboard-only flow Tabs to Register then presses Enter; Enter inside an
  input is covered by `DefaultButtonTests`) are accepted, documented
  exceptions.
  Phase 21 shipped version 1.0.0 (one source in `Directory.Build.props`), an
  app icon, README/CHANGELOG/LICENSE, an own rolling file logger
  (`<DataDir>/logs/app-YYYYMMDD[.n].log`, 5 MB x 5, UTC, never throws, no new
  package), `UnexpectedErrorReporter` + `GlobalExceptionHooks` + an error
  overlay (short id, copy, `+N more`) + `StartupGuard`
  (`startup-error-*.log`), a single `KeyboardShortcutCatalog` driving the
  window bindings, tooltips and the Settings list, `IsDefault` Enter submit,
  a sidebar on `NavigationSections` with a visible current section, empty
  states and es-DO/en localization of Expenses/Income/Debts/Recurring, named
  controls, contrast and live-region tests (`docs/accessibility.md`), and
  packaging: `build/Publish.props`, `scripts/publish.ps1` (Windows
  PowerShell 5.1 or 7) producing self-contained `.zip`/`.tar.gz`/`.app.zip`
  archives plus `SHA256SUMS.txt` in the git-ignored `artifacts/`,
  `verify-layout.ps1`, `smoke-win.ps1`, a macOS `.app` script, an AppImage
  recipe and a draft-only `release.yml` (tag `v*`). Deviations: MSIX
  deferred, no installer, DMG only as a CI step, no signing or
  notarization, both macOS `.tar.gz` and `.app.zip` exist (owner to
  choose). No migration, no new package, no new setting. 2876 unit + 845
  integration tests green (3721), build 0/0, format exit 0, NetArchTest
  20/20. Commits: `f10816a` (S1) to `60800fc` (S10), post-verify fixes
  `c11fb6e` and `ac181ca`. After the W4 fix the test-only and tooling
  assemblies (Avalonia.Headless, EF design-time, Roslyn) are out of the
  publish payload and `PublishPayloadTests` guards it: folders are 126 MB
  (linux-x64), 131 MB (osx-x64), 133 MB (win-x64) and 137 MB (osx-arm64),
  down from 141-152 MB; `release.yml` now runs build + test (windows-latest
  only) before publishing and pins `appimagetool`. Packaging tests: `PublishLayoutTests` and `ChecksumVerificationTests`
  need `artifacts/` (run `scripts/publish.ps1 -Rid all`); when it is missing
  they SKIP with an explicit message, unless the environment variable
  `CONTROLDEFINANZASIA_REQUIRE_PACKAGING=1` is set, in which case they fail.
  Not done by hand: Narrator/NVDA/VoiceOver/Orca passes, install from the
  zip on a clean Windows machine, running the macOS and Linux builds, the
  AppImage/DMG builds, the real execution of `release.yml` (no git remote),
  and the es-DO/en visual pass (tasks 11.2-11.7). Follow-ups: log entries
  carry `exception.ToString()`, which can embed user data; two app
  instances sharing one data directory can exceed the 5 MB roll and prune
  each other's file; sidebar mouse clicks are not gated during an import
  or restore; the Meta shortcut path was never run on a Mac; the input
  resting border has a 1.46:1 contrast (a WCAG 1.4.11 gap, documented in
  `docs/accessibility.md`). See
  `docs/packaging-unverified.md`, `docs/accessibility.md`, `tasks/phase-21/`
  and `.atl/sdd/f-021p21-packaging/`. Earlier status: Phase 20 (CSV/Excel
  import) shipped an "Importar" button in
  the Expenses toolbar that swaps the list for `ImportView` in place (no
  route). It reads a generic CSV/TXT (strict UTF-8, Windows-1252 fallback,
  UTF-16 with BOM; `,` `;` tab sniffed) or a Phase-18 XLSX, chosen by
  content and never by extension; headers auto-map in English and Spanish
  (the mapping step shows only when Date, Description or Amount is
  missing); a dry-run preview shows the first 50 rows and the first 50
  problem rows; confirming re-parses the retained bytes and writes through
  `EfImportWriter` in a fresh DI scope with ONE `SaveChangesAsync` (all or
  nothing; 50,000 rows in about 6.5 s). Duplicates are skipped by an
  occurrence-counting fingerprint, so re-importing a file inserts 0;
  missing categories can be created (cap 50, the 51st is rejected), and
  expenses without a category fall back to the seeded "Otros" by Id. The
  rule is no silent corruption: no guessed day/month or decimal separator,
  more than 4 decimals rejected (CSV and XLSX), undecodable text rejected
  (`UndecodableText`), and ambiguous amounts or dates need an
  acknowledgement checkbox before Confirm works. ClosedXML and ZIP reading
  stay in Infrastructure (NetArchTest also forbids `System.IO.Compression`
  in Domain and Application). No migration, no new package, no new
  setting. 3323/3323 tests green (2551 unit + 772 integration), build 0/0,
  format exit 0, NetArchTest 18/18. Commits: `aa36efa` to `2f3108a`
  (slices S1-S7), `43f7c38` (post-verify fixes), `b735ced` (pre-existing UI
  binding fixes: DatePicker `DateOnlyConverter` in Expenses/Income/Debts/
  RecurringRules and the row-template `#Root.DataContext.Cmd` crash in 7
  views). Verify history: exhaustive verification PASSED WITH WARNINGS, 0
  critical; W1-W5 (acknowledgement gate, XLSX more than 4 decimals, lossy
  Windows-1252 text, spec drift, missing architecture guard) fixed
  test-first. Documented deviation: persisted/named mapping profiles
  (`CsvImportProfile`) were deferred; auto-mapping plus the manual mapping
  step cover the case. Not done by hand: the real native open dialog with
  a Phase-18 XLSX, re-import via the UI, the es-DO/en visual pass and the
  Settings screen smoke (tasks 8.2, 8.3). Accepted follow-ups: a workbook
  without a `Data` sheet falls back to the first sheet; six `ExpensesAi*`
  resx keys lack Designer accessors (pre-existing);
  `MainWindow.axaml:160` `$parent[ItemsControl].DataContext.DismissCommand`
  not exercised headless; named profiles, OFX/QIF/MT940, PDF import, AI
  categorization on import and undo of an import (restore a Phase-19
  backup) are out of scope. See
  `spec/features/phase-20/F-020-import.done.md`, `tasks/phase-20/`, and
  `.atl/sdd/f-020p20-import/`. Phase 19
  (backup and restore) shipped a Settings "Backup" block (before "Acerca
  de") with "export everything" and "restore from a file". A backup is a
  ZIP with exactly `manifest.json` + `data.db` (SHA-256, schema version),
  taken through `SqliteConnection.BackupDatabase` into a temp copy that
  passes `integrity_check`; the API key is redacted in user exports
  (`secure_delete` + `VACUUM`, so no key bytes remain) but kept in the
  pre-restore safety ZIPs. Restore runs a fixed validation pipeline (ZIP
  shape, zip-slip, size caps, hash, `integrity_check`, `foreign_key_check`,
  migration-history set comparison: unknown migration = rejected showing
  both versions, older = accepted), then a free-space check, a safety ZIP
  in `<DataDir>\backups\` (keep 3), and a WAL-aware atomic swap
  (`ClearAllPools` + one `File.Replace`, retry 200/600/1800 ms, rollback to
  the original bytes on failure; `data.db.pre-restore.bak` kept until the
  next successful start, and a `.rollback-failed` marker keeps it if a
  rollback itself fails). Notifications are suspended during restore, the
  local API key is carried into the restored file, and a restart is required
  (`IAppRestartService`, with a "close and reopen" fallback).
  `CONTROLDEFINANZASIA_DATA_DIR` overrides the data directory for
  throwaway runs. No migration, no new package, no new setting. 2289/2289
  tests green (1725 unit + 564 integration), build 0/0, format exit 0.
  Verify history: exhaustive verification PASSED WITH WARNINGS, 0
  critical, no data-loss path found; it caught that the live database
  runs in WAL mode (the spec assumed DELETE) and four smaller defects,
  fixed in `f87be48`. Not done by hand: the real native save/open dialogs,
  the real relaunch, and the es-DO/en visual pass (tasks 6a.8, 6b.5,
  8.2-8.5). Accepted follow-ups: leftover aside `.bak` files are never
  swept, shell-wide restart-required banner, `trusted_schema=OFF`. See
  `spec/features/phase-19/F-019-backup-restore.done.md`, `tasks/phase-19/`,
  and `.atl/sdd/f-019p19-backup/`.
  Phase 18
  (PDF/Excel export) shipped "Exportar PDF" / "Exportar Excel" buttons on
  the Dashboard (the displayed month summary) and on Expenses (the current
  filter, every page, not just the visible one). QuestPDF Community and
  ClosedXML live behind `IReportWriter` in Infrastructure only (NetArchTest
  enforces it; the QuestPDF license is set in `AddReportExporters()` via
  `QuestPdfConfiguration`), and `IFileSaveDialog` is implemented in UI over
  Avalonia `StorageProvider`. Reports reuse the existing queries and
  `ICurrencyConverter`: totals are DOP-normalized, every line keeps its
  original amount + currency, and a missing rate leaves the DOP cell blank
  (never 0) and adds a footer note. The XLSX has two sheets, `Summary`
  (styled) and `Data` (flat, stable English headers, the Phase 20 import
  contract). Each export renders in memory first and copies to the target
  only on success, so a failed render never creates or truncates a file;
  cancelling the dialog is silent. No schema change, no migration, no new
  setting; Debts export and CSV are documented follow-ups. 1908/1908 tests
  green (1550 unit + 358 integration), build 0/0, format exit 0. Verify
  history: the first independent verification FAILED because
  `QuestPdfReportWriter.WriteExpenseListAsync` was a stub (fixed in
  3515cb9); the second, exhaustive one PASSED WITH WARNINGS, 0 critical,
  and a permanent end-to-end test (`ReportExportEndToEndTests`, real use
  cases + real writers, {Pdf, Xlsx} x {es-DO, en}) closed its main warning.
  Not done by hand: the real save dialog, opening the files in a PDF/Excel
  viewer, and a visual check in both locales (tasks 8.2/8.3). See
  `spec/features/phase-18/F-018-export.done.md`, `tasks/phase-18/`, and
  `.atl/sdd/f-018p18-export/`.
  Phase 17 (in-app
  notifications) shipped a shell bell + unread badge + popup (`9+` above 9,
  20-row cap, mark-read on open, per-item permanent dismiss, manual
  refresh, no click-through) fed by a deterministic
  `DetectNotificationsUseCase`, with no AI involved. It runs at app start,
  after saving the Settings "Notificaciones" section, and on manual
  refresh, each time in its own DI scope via `ScopedNotificationCenter`
  (every failure logged and swallowed). Four detected kinds — debt
  installments due/overdue, recurring charges due, budget Near/Exceeded
  (highest tier only), unacknowledged anomalies — plus a periodic digest
  (`Off`/`Daily`/`Weekly`, default Weekly; `yyyy-MM-dd` cursor written only
  after a successful reconcile, never backfills, totals per currency,
  independent of per-kind toggles and dismissals). `NotificationReconciler`
  persists `Notification` rows (facts only, text formatted at display time
  from resx) keyed by a single unique `DedupKey`: idempotent, one
  `SaveChangesAsync`, 60-day purge, dismissed keys never recreated, facts
  refreshed on undismissed rows. Four per-kind toggles (default on) and the
  digest picker live in Settings via new `SettingKeys` (`AppSettings`
  untouched). 1700/1700 tests green (1405 unit + 295 integration); verify
  PASS WITH WARNINGS, 0 critical (the manual run in both locales and the
  forced-failure check were not run by hand). See
  `spec/features/phase-17/F-017-notifications.done.md`, `tasks/phase-17/`,
  and `.atl/sdd/f-017p17-notifications/`.
  Phase 16 (AI savings plan for
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
  **Post-QA fixes (1.0 blockers, after the QA audit of 2026-10-01).** Phases 20
  and 21 are also archived (the "Phase 20 is next" wording above predates them;
  see `docs/AI_HANDOFF.md`). The audit found that the Phase 6 view rewrite
  (`6caae70`) had removed the pager, the per-row delete and the filter panel
  from `ExpensesView` and `IncomeView` while the view-models kept them, so only
  the newest 20 records were reachable and nothing could be deleted. Slice A
  fixed the console-subsystem exe (`WinExe`), the blank recurring preview, the
  currency on rows, the debt Pay/Skip state and the untranslated enum labels.
  Slice B (`b2f96c5`) restored, on BOTH lists: a pager ("Page X of Y" plus the
  total count; the page steps back after deleting the last row of the last
  page), delete with an inline confirmation (the same button turns into
  Confirm, with a Cancel), a filter panel (text search, category, from/to
  dates, apply/clear, an inline message for an inverted range; Enter in the
  search box applies the filter instead of triggering the form's default
  button) and the "≈ other currency" line on each row. Text search is new end to
  end (`ExpenseFilter.Text` / `IncomeFilter.Text`, escaped `LIKE`, echoed in the
  PDF/XLSX export summary). Expenses also got saved views. Deliberately NOT
  exposed: tags (nothing in the app can assign a tag, so a tag filter or a "new
  tag" form would be dead controls), saved views on Income and the
  payment-method filter. The new tests drive the real views (headless) and the
  real composition root over a temp SQLite file; a guard test now fails if a
  list view stops binding the pager, delete or filter commands. 3805/3805 tests
  green (2955 unit + 850 integration), build 0/0, format exit 0, no migration,
  no new package. Not done by hand: macOS/Linux, a screen reader, the
  from/to date pickers in the real binary (covered headless only) and the real
  save dialog with a text-filtered export.
- **Recurring rules form (pre-1.0 slice C):** a recurring charge is defined
  by the day of the month it is always made on (1-31; month + day for a
  yearly charge), picked in the form with a read-only "Next charge" line; the
  type selector (Subscription / Fixed monthly expense) has plain-language help.
  The persisted model is unchanged: the chosen day is the day of
  `RecurringRule.StartDate` (the anchor, see `RecurringSchedule`) and
  `NextGenerationDate` is the cursor; a day 31 charge created in a short month
  stores the closest earlier month that has the day as its start date. An edit
  re-sends the stored start date and cursor untouched unless the day, month or
  frequency changed; the row "Skip" skips its own rule. The repository update
  now also copies the owned `Amount`. No migration. 3883/3883 tests green
  (3028 unit + 855 integration), build 0/0, format exit 0.
- **Dashboard month expenses (pre-1.0 slice D):** a "Gastos del mes" card
  sits right under the Dashboard summary cards and lists the displayed
  month's latest expenses (cap `DashboardDefaults.MonthExpensesLimit` = 8,
  newest first) through the new `GetMonthExpensesQuery` (reuses
  `IExpenseRepository.GetByFilterAsync` with the month range, plus category
  names and the per-row DOP equivalent). A failed load is a `Result.Failure`
  shown as the usual localized load error, never as the empty state.
  `DashboardViewModel` now also takes `INavigationService`;
  `ViewAllExpensesCommand` opens Expenses through
  `ExpensesViewModel.ApplyDateRangeFilter` (`ApplyDayFilter` delegates to
  it). No migration, no new package. 3912/3912 tests green (3056 unit + 856
  integration).
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
# Set CONTROLDEFINANZASIA_DATA_DIR (absolute path) to run against a throwaway data directory

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
