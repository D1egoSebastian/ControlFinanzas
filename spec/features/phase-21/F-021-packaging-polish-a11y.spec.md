# F-021 — Packaging, Polish and Accessibility (1.0.0)

> **Phase:** 21 — Packaging, polish, accessibility
> **Status:** Active (spec written 2026-09-30).
> **SDD:** `.atl/sdd/f-021p21-packaging/`
> **Input:** `.atl/sdd/f-021p21-packaging/proposal.md` (D-1..D-28 binding), `.atl/sdd/f-021p21-packaging/exploration.md`
> **Depends on:** Phases 0-20 archived (Phase-19 restore and Phase-20 import are the busy flows gated by R-13).
> **Blocks:** nothing (last roadmap phase).
> **Roadmap:** `docs/roadmap.md` §Fase 21; `docs/architecture.md` §14 (logging), §16 (errors), §18.

## Goal

The app is feature-complete but cannot be handed to anyone: it needs the .NET SDK, has no version or icon, a stale
README and no CHANGELOG; unexpected failures leave nothing on disk and either show nothing or crash; keyboard users
cannot register an expense without the mouse; several screens show hardcoded Spanish and no empty state. Phase 21
makes `1.0.0` distributable (self-contained per OS), diagnosable (local rolling file log plus a visible correlation
id), and usable by keyboard and basic screen reader. The governing rule is **honesty about verification**: only
what was actually run on this Windows machine is called verified; everything else is labelled "authored,
unverified".

Naming note: type, file and test names below are the **contract names** for this spec. The design may adjust a
name, but every requirement MUST keep a concrete artifact and a test that exercises the REAL implementation (not a
fake standing in for the behavior under test; Phase 18 lesson: a stub passed until a real end-to-end test existed).

## Grounding in current code (binding facts)

- `Result` is NON-generic (`IsSuccess`, `Value: object?`, `Error?`). Expected failures stay `Result.Failure` and stay inline; this phase only adds handling for UNEXPECTED exceptions.
- `Program.cs` currently does `ClearProviders()` + `AddConsole()`; there is no file sink although `architecture.md` §14 promises one. There is no global exception handler.
- Correlation ids exist today only as log-only Guids in LLM use cases.
- `IClipboardService` and `IAppRestartService` already exist in `UI/Services/`. `AppRestartService` uses `Environment.ProcessPath` (so single-file publish is forbidden).
- Avalonia is `11.2.7` (Central Package Management). `TreatWarningsAsErrors=true`; StyleCop, resx parity test and NetArchTest R-1..R-5 are active.
- `<Version>` is absent from every props file; `SettingsViewModel.AppVersion` reads the informational version.
- There is no publish profile, RID, CHANGELOG, LICENSE, app icon or git remote. CI is `.github/workflows/build.yml` (ubuntu build/test/format).
- Only one `KeyBinding` exists (Chat Enter). `AutomationProperties` appear only in `ImportView`, the `MainWindow` bell and `ChatView`. The sidebar is a `ListBox` whose item template nests a `Button`; no current section is visible. Focus styles exist only for `ledger-input` TextBox/ComboBox.
- Hardcoded Spanish: Expenses/Income empty text, 62 literals in `ExpensesView`/`IncomeView`/`DebtsView`/`RecurringRulesView`, and `ShellViewModel` sidebar labels "Recurrentes", "Deudas", "Calendario". Empty states already exist in Budgets, Goals, Chat, Calendar, Dashboard, Notifications.
- Headless Avalonia harness: `tests/ControlDeFinanzasIA.Tests.Unit/UI/HeadlessUi.cs` (shared session and xUnit collection).
- Native dependencies per RID: SkiaSharp, QuestPDF, Microsoft.Data.Sqlite.

## Requirements

### R-1 — Version, changelog, license, README

`Directory.Build.props` MUST declare `<Version>1.0.0</Version>` exactly once; no `.csproj` MUST override it. The UI assembly's informational version MUST therefore start with `1.0.0` and be what `SettingsViewModel.AppVersion` shows. The repository MUST contain `CHANGELOG.md` in Keep-a-Changelog format (English) with a `## [1.0.0]` entry summarizing by feature area (Phases 0-21) and an `## [Unreleased]` section; `LICENSE` ("All rights reserved, personal project" unless the owner overrides); and a rewritten `README.md` covering what the app is, download/run per OS, build from source, data directory and `CONTROLDEFINANZASIA_DATA_DIR`, the keyboard shortcut summary, the SmartScreen and Gatekeeper workarounds, and an explicit statement that macOS and Linux builds are "provided, untested". The annotated tag `v1.0.0` MUST be created only when the owner asks for it (no AI trailers); this phase only makes the repo tag-ready.

### R-2 — App icon

One SVG source (`src/ControlDeFinanzasIA.UI/Assets/app-icon.svg`) MUST produce `app-icon.png`, `app-icon.ico` and `app-icon.icns` in the same folder. The UI project MUST set `ApplicationIcon` (ico) and `MainWindow` MUST set `Window.Icon` from the PNG asset. The visual design MUST go through the `frontend-design` skill (no generic aesthetics).

### R-3 — Rolling file logger (Infrastructure)

A minimal `ILoggerProvider` named `RollingFileLoggerProvider` (plus `RollingFileLogger`) MUST live in `src/ControlDeFinanzasIA.Infrastructure/Logging/`, registered through an `AddFileLogging(...)` DI extension, using no new package. It MUST write to `<DataDir>/logs/app-YYYYMMDD.log` (UTC date), where `<DataDir>` is the resolved data directory (honoring `CONTROLDEFINANZASIA_DATA_DIR`). The console provider MUST keep working alongside it. Writes MUST be serialized so concurrent log calls never interleave inside one line.

### R-4 — Rolling rule

A file MUST roll when it would exceed 5 MB within a day (`app-YYYYMMDD.1.log`, `.2.log`, …) and when the UTC date changes. After each roll the provider MUST keep only the newest 5 log files in `<DataDir>/logs/` (by name order then timestamp) and delete the older ones at roll time, bounding disk use to about 25 MB. Files from previous runs MUST be counted and pruned the same way.

### R-5 — Log content and privacy

Each entry MUST be a single line starting with a UTC ISO-8601 timestamp, then level, category, event id and message; an exception MUST append its type, message and stack trace (stack lines MAY be newline-escaped so the entry stays one logical record). Minimum level MUST be `Information`, `Warning` for categories starting with `Microsoft.`. EF Core sensitive-data logging MUST stay off. The LLM API key MUST never be passed to a logger; a test with a sentinel key MUST prove the sentinel never reaches any log file through the real flows that handle the key (settings save, LLM call failure, backup/export).

### R-6 — Logger failure is contained

The provider MUST NEVER throw to its caller. On the first I/O failure (unwritable or missing directory, locked file, full disk) it MUST disable the file sink for the rest of the session without retrying, while the console provider keeps working. No exception, no retry loop, no log storm.

### R-7 — Unexpected-error reporter and correlation id

`IUnexpectedErrorReporter` (contract in `UI/Services/`, implementation `UnexpectedErrorReporter`) MUST, for an unexpected exception only: generate a `Guid`, log the exception at `Error` with the FULL Guid in the message, and expose the first 8 hexadecimal characters of the Guid as the short id shown to the user. `Result.Failure` outcomes MUST NOT produce a correlation id and MUST keep their existing inline messages. The reporter MUST never throw (a failure inside it is swallowed after a best-effort stderr line). Nothing is transmitted off the machine; there is no crash upload and no telemetry.

### R-8 — Global hooks

At startup the app MUST register: `Dispatcher.UIThread.UnhandledException` (mark `Handled = true`, report through R-7, raise the overlay of R-10); `AppDomain.CurrentDomain.UnhandledException` and `TaskScheduler.UnobservedTaskException` (log only, with a correlation id, no UI, `SetObserved` for tasks). The hook bodies MUST never throw. Unhandled errors raised during a Phase-19 restore MUST reach the existing restore rollback path first; the global hook is the last resort, not a replacement.

### R-9 — Startup failure path

A failure before DI or the UI exists MUST be caught in `Program.Main`, written to `startup-error-<utc>.log` in the resolved DataDir (falling back to `%TEMP%` when that directory cannot be created or written) and to stderr, and the process MUST exit with a non-zero code. No UI is attempted on this path. The file contains the exception type, message and stack plus the app version; no secrets.

### R-10 — Error overlay (in-window, no new Window)

`ErrorOverlayViewModel` and `ErrorOverlayView`, hosted in the `MainWindow` overlay layer (ADR-004; no new `Window`), MUST show a localized title, the short id, and localized guidance, with `Copy`, `Close` and `Esc` (= Close). Copy MUST call `IClipboardService` with text containing exactly: short id, full Guid, UTC time and app version, and MUST NOT contain a stack trace, an exception message, a path or any user data. Only one overlay exists at a time: a second unexpected error while it is open is logged with its own Guid and increments a "+N more" counter on the same overlay, which keeps showing the first id. All overlay strings come from resx (es-DO and English).

### R-11 — No hidden corrupt state

After the overlay closes the app keeps running and MUST NOT auto-restart. The overlay copy MUST recommend restarting if problems continue (copy only; `IAppRestartService` is not invoked by the overlay). The handler MUST NOT swallow the exception silently: every handled exception is logged (R-7) before the overlay is shown.

### R-12 — Keyboard shortcuts and single source of truth

A `KeyboardShortcutCatalog` (UI) MUST be the single source feeding (a) the `MainWindow` `KeyBindings`, (b) the read-only Settings list of R-16 and (c) tooltip text. The catalog MUST define: `Ctrl+N` new expense (navigate to Expenses, cancel any edit in progress, focus the Description field); `Ctrl+Shift+N` new income (same for Income); `Ctrl+1`..`Ctrl+9` and `Ctrl+0` jump to sidebar sections in order (1 = first item, 0 = tenth; sections beyond the tenth have no digit); `Ctrl+,` Settings; `Ctrl+Shift+B` open/close the notification bell; `Esc` close the topmost overlay. Each entry MUST have a macOS `Meta` duplicate. `KeyBinding`s bind to `ShellViewModel` commands; there is NO logic in code-behind. Primary action buttons MUST use `IsDefault` so Enter submits the form. Focus requests MUST flow through an attached behavior (`FocusOnRequestBehavior`) bound to a `FocusRequestToken` property on the ViewModel. No shortcut is remappable.

### R-13 — Shortcut gating

While the error overlay or any modal overlay is open, only `Esc` and focus on Copy/Close act; navigation and creation shortcuts MUST be no-ops. While a Phase-20 import commit or a Phase-19 restore is busy, `Ctrl+N`, `Ctrl+Shift+N` and `Ctrl+1`..`Ctrl+0` MUST be no-ops (`Ctrl+,` and `Esc` are also no-ops during a restore). Busy state is read from the existing ViewModel flags, not duplicated.

### R-14 — Keyboard-only expense registration

The flow `Ctrl+N` → fill Description, Amount, Date, Category (Tab order follows visual order) → `Enter` MUST register an expense and show the new row, with no mouse. The first field receives focus after `Ctrl+N`; after a successful save focus returns to Description for the next entry. Tab order and initial focus MUST be logical on every main section's primary form.

### R-15 — Sidebar rework

The sidebar MUST be restructured so each item is a single interactive element (no `Button` nested inside a `ListBoxItem`), keyboard reachable (arrow keys and Tab), and MUST show the current section visibly (selection style) and expose it to automation (`AutomationProperties.Name`; selected state via the selectable item). Sidebar labels MUST come from resx (R-19), including the three formerly hardcoded labels.

### R-16 — Shortcut list in Settings and tooltips

Settings MUST show a localized read-only "Keyboard shortcuts" block generated from `KeyboardShortcutCatalog`. Tooltips on the controls that trigger a catalog action (new expense, new income, bell, settings entry) MUST show the gesture text from the same catalog. A headless test MUST assert that every `KeyBinding` on the real `MainWindow` matches a catalog entry and every catalog entry has a binding (no drift, both directions).

### R-17 — Accessibility (basic, Windows UIA only)

Every interactive control that has no visible text (icon-only buttons, the bell, sidebar items, pickers, toggles) MUST have a non-empty `AutomationProperties.Name` (localized via resx). Visible focus styling MUST exist for buttons, sidebar items, list rows and inputs (a shared focus style in the resource dictionaries, using design tokens). Status messages (error overlay, import result, AI fallback captions) MUST use a live region (`AutomationProperties.LiveSetting`) **if** Avalonia 11.2.7 exposes it; otherwise the limitation MUST be documented in `docs/accessibility.md`. An accessibility statement `docs/accessibility.md` MUST state: Windows UIA is the only platform claimed and tested, VoiceOver and Orca/AT-SPI are best effort and untested, there is no high-contrast theme and no full WCAG audit.

### R-18 — Contrast

A unit test MUST compute WCAG contrast ratios for the design-token text/background pairs actually used by the app (primary text, secondary text, button label on its fill, focus ring on background, error and warning text) in the real palette file(s) and assert at least 4.5:1 for text and 3:1 for focus rings and large text. A failing pair MUST be fixed in the tokens, not excluded from the test.

### R-19 — Localization debt

The 62 hardcoded literals in `ExpensesView`, `IncomeView`, `DebtsView`, `RecurringRulesView` and the three sidebar labels MUST move to `Strings.resx` (neutral English) and `Strings.es-DO.resx` with **manual** accessors in `Strings.Designer.cs`. A XAML scan test MUST fail if any of these four views contains a hardcoded `Text=`, `Content=`, `Header=`, `Watermark=`, `ToolTip.Tip=` or `AutomationProperties.Name=` literal that is not a binding or a resource lookup. The existing resx completeness test MUST cover the new keys (every neutral key in es-DO and vice versa, no empty values). Inline hex colors MUST be replaced by design tokens in views touched by this phase.

### R-20 — Empty states

A reusable `EmptyState` control (`UI/Controls/EmptyState.axaml`) with icon, title, hint and optional action MUST be added and used in Expenses, Income, Debts and Recurring. Expenses and Income MUST distinguish (a) "no data yet": hint plus an action to create one (showing the `Ctrl+N` / `Ctrl+Shift+N` gesture) and (b) "no results for this filter": a clear-filter action that resets the filter. Debts and Recurring show the "no data yet" state only. Existing empty states elsewhere are kept unchanged. The empty state MUST NOT render when data exists and MUST NOT render while loading.

### R-21 — Self-contained publish per RID

`build/Publish.props` (shared) and `scripts/publish.ps1` MUST publish the UI project self-contained, as a folder, for `win-x64`, `osx-arm64`, `osx-x64` and `linux-x64`, with no trimming, no single-file and no ReadyToRun. Output goes to `artifacts/` (gitignored). The published win-x64 folder MUST contain the native libraries of SkiaSharp, QuestPDF and SQLite for that RID; the other three RIDs MUST publish successfully and contain their RID-specific native libraries (layout check by file presence). `AppRestartService` MUST work from the published folder (`Environment.ProcessPath`).

### R-22 — Archives and checksums

`publish.ps1` MUST produce `ControlDeFinanzasIA-1.0.0-<rid>.zip` (win) or `.tar.gz` (osx, linux) per RID and a `SHA256SUMS.txt` whose lines verify against the archives. The version in the file names MUST come from `Directory.Build.props`, not be retyped.

### R-23 — macOS `.app` and Linux AppImage recipes

`scripts/make-app-bundle.ps1` MUST assemble a `ControlDeFinanzasIA.app` folder from the osx publish output with `Contents/MacOS`, `Contents/Resources` (icns) and a valid `Contents/Info.plist` (bundle id, version from R-1, executable name, icon), then zip it. `scripts/appimage/` MUST contain the AppImage recipe (AppDir layout, `.desktop` file, icon, build script for CI). DMG creation and AppImage build run only in CI on the target OS. MSIX is explicitly deferred and NOT authored.

### R-24 — Release workflow (draft only)

`.github/workflows/release.yml` MUST trigger only on a pushed `v*` tag, build/test on the matching runners, run the publish scripts, and create a **draft** GitHub release with the archives and `SHA256SUMS.txt`. It MUST never publish automatically. The existing `build.yml` remains unchanged in behavior.

### R-25 — Footprint and architecture

No schema change, no EF migration, no new NuGet package (`Directory.Packages.props` unchanged except nothing added). The file logger lives in Infrastructure; the reporter, overlay, catalog, behavior and `EmptyState` live in UI; the UI MUST NOT reference `System.Net.Http` for error reporting. An `ArchitectureTests` case MUST assert the logger types are in Infrastructure and that UI error-reporting types do not depend on `System.Net.Http`. NetArchTest R-1..R-5 MUST stay green. `TreatWarningsAsErrors` and `dotnet format style` MUST stay clean.

### R-26 — Test hygiene and documentation

Tests MUST NOT read or write the real AppData directory (temp directories or `CONTROLDEFINANZASIA_DATA_DIR`). Time comes from `IClock` or an injected clock in the logger. Docs MUST be reconciled in the closing slice: `docs/architecture.md` §14, §16, §18 describe the real logger, reporter and packaging; `docs/roadmap.md` records the installer deviation (MSIX deferred, DMG CI-only, no signing) and Phase 21 done; `AGENTS.md` status block is updated; `spec/phases/phase-21/features.md` is reconciled.

## Scenarios

- **S-1 — Version.** Given the built UI assembly, When the informational version is read, Then it starts with `1.0.0`, `SettingsViewModel.AppVersion` returns it, and exactly one `<Version>` element exists across all props/csproj files.
- **S-2 — Repo docs.** Given the repo root, Then `CHANGELOG.md` has `[Unreleased]` and `[1.0.0]`, `LICENSE` exists, and `README.md` contains the sections of R-1 including both OS workaround texts and the "provided, untested" statement.
- **S-3 — Icon.** Given the UI project, Then `app-icon.svg/.png/.ico/.icns` exist, `ApplicationIcon` is set, and the headless `MainWindow` has a non-null `Icon`.
- **S-4 — Log file created.** Given a temp DataDir and the real provider, When an `Information` entry is logged, Then `logs/app-<utc date>.log` exists with one line containing UTC timestamp, level, category and message.
- **S-5 — Level filter.** Given `Debug` entries and `Microsoft.EntityFrameworkCore` `Information` entries, Then neither reaches the file; a `Warning` from `Microsoft.*` does.
- **S-6 — Roll by size.** Given a writer that logs more than 5 MB in one day, Then `app-YYYYMMDD.1.log` appears and no file exceeds 5 MB by more than one entry.
- **S-7 — Roll by date.** Given an injected clock that crosses UTC midnight, Then a new `app-<next date>.log` is used.
- **S-8 — Retention.** Given 8 existing log files in `logs/` and a roll, Then only the newest 5 remain.
- **S-9 — Concurrency.** Given 32 threads logging 200 lines each, Then the file has exactly 6,400 intact lines.
- **S-10 — Logger survives failure.** Given a DataDir whose `logs` path is a regular file (or a locked file), When logging, Then no exception reaches the caller, the file sink is disabled after the first failure, console logging still works, and later calls do not retry.
- **S-11 — Exception formatting.** Given an exception with an inner stack, When logged, Then the entry holds type, message and stack for the same correlation Guid.
- **S-12 — No key leakage.** Given sentinel API key `sk-SENTINEL-9f3a`, When settings are saved, an LLM call fails, and a user export runs with the real components, Then no file in `logs/` contains the sentinel.
- **S-13 — Correlation id.** Given the real reporter and real file logger, When an unexpected `InvalidOperationException` is reported, Then the short id is 8 lowercase hex chars and the same full Guid (whose first 8 hex chars equal the short id) appears in the log file at `Error`.
- **S-14 — Result.Failure unchanged.** Given a use case returning `Result.Failure`, Then no correlation id is generated, nothing is logged at `Error` by the reporter, and the inline message is shown.
- **S-15 — UI-thread hook.** Given a headless window and a command that throws on the UI thread, When it runs, Then `UnhandledException.Handled` is true, the overlay becomes visible with the short id, and the app keeps running.
- **S-16 — Background hooks.** Given an unobserved faulted `Task` and an `AppDomain` unhandled exception raised via the registered handlers, Then each is logged with a correlation id, no overlay appears and the handler does not throw.
- **S-17 — Hook reentrancy.** Given the reporter itself throws (forced logger failure), Then the hook still returns without throwing.
- **S-18 — Startup failure.** Given a forced exception before DI is built, When `Program.Main` handles it, Then `startup-error-<utc>.log` exists in the DataDir (or `%TEMP%` when the DataDir is not writable), stderr received the message, and the exit code is non-zero (run through the real `Program` entry, in a child process for the exit code).
- **S-19 — Overlay content.** Given a reported error, Then the overlay shows the localized title, the short id and restart guidance in es-DO and en; `Esc` and `Close` dismiss it.
- **S-20 — Copy text.** Given the overlay is open, When Copy is pressed, Then `IClipboardService` receives text with short id, full Guid, UTC time and version and contains neither a stack frame (`   at `), the exception message, a path, nor any expense data used in the test.
- **S-21 — Second error.** Given the overlay is open, When a second unexpected error occurs, Then it is logged with its own Guid, the overlay shows "+1 more" and still shows the first short id.
- **S-22 — No auto-restart.** Given the overlay closes, Then `IAppRestartService` was never invoked and the shell remains interactive.
- **S-23 — Restore keeps rollback.** Given a forced unhandled failure inside a restore, Then the existing Phase-19 rollback ran (original database bytes intact) before or independent of the overlay.
- **S-24 — Shortcut parity.** Given the real `MainWindow`, Then its `KeyBindings` equal the `KeyboardShortcutCatalog` entries (gestures and commands), including Meta duplicates, in both directions.
- **S-25 — Ctrl+N.** Given any section, When `Ctrl+N` is pressed, Then Expenses is shown, an edit in progress is cancelled, and the Description field has focus.
- **S-26 — Ctrl+Shift+N.** Same for Income.
- **S-27 — Digit navigation.** Given the sidebar order, When `Ctrl+1` and `Ctrl+0` are pressed, Then the first and tenth sections open; a section beyond the tenth has no digit.
- **S-28 — Ctrl+, and bell.** `Ctrl+,` opens Settings; `Ctrl+Shift+B` toggles the notification popup; `Esc` closes it.
- **S-29 — Gating by overlay.** Given the error overlay is open, When `Ctrl+N` or `Ctrl+2` is pressed, Then nothing navigates; `Esc` closes the overlay.
- **S-30 — Gating by busy flows.** Given an import commit is in `Importing` (and, separately, a restore is busy), When `Ctrl+N` or `Ctrl+3` is pressed, Then nothing happens and the running operation is uninterrupted.
- **S-31 — Keyboard-only register expense.** Given the real `MainWindow`, real `ExpensesViewModel` and a temp SQLite database, When the test sends only key events (`Ctrl+N`, text, `Tab`, digits, `Enter`), Then a new expense row is persisted and visible in the list, and focus is back on Description.
- **S-32 — Sidebar.** Given the sidebar, Then no `ListBoxItem` contains a `Button`, arrow keys move selection, the current section shows the selected style and has an automation name, and the three formerly hardcoded labels render in es-DO and en.
- **S-33 — Settings shortcut list.** Given Settings, Then every catalog entry is listed with its localized action name and gesture; tooltips on the new-expense and bell controls show the catalog gesture.
- **S-34 — Name scan.** Given the real visual tree of every main section (headless, es-DO), Then no interactive control without visible text lacks a non-empty `AutomationProperties.Name`.
- **S-35 — Focus visible.** Given a focused button, sidebar item and list row, Then the shared focus style applies (asserted on the style/pseudo-class in the headless tree).
- **S-36 — Live region.** Given Avalonia 11.2.7, When status messages are rendered, Then `LiveSetting` is set where supported; if the API is absent, `docs/accessibility.md` records it (test asserts one of the two holds).
- **S-37 — Contrast.** Given the real palette tokens, Then all pairs listed in R-18 meet their thresholds.
- **S-38 — Localization scan.** Given the four views, Then the XAML scan finds no hardcoded user-visible literal, and the resx completeness test passes with all new keys in both cultures.
- **S-39 — Empty: no data.** Given an empty database, When Expenses, Income, Debts and Recurring are shown (es-DO and en), Then each renders `EmptyState` with localized title/hint and, for Expenses/Income, the create action with the gesture.
- **S-40 — Empty: filtered.** Given expenses exist and a filter matches none, Then Expenses shows the "no results" state with a clear-filter action that restores the list; the "no data yet" state is NOT shown.
- **S-41 — No empty when data.** Given data exists (or while loading), Then `EmptyState` is not visible.
- **S-42 — Win publish and smoke.** Given `publish.ps1 -Rid win-x64`, Then the folder runs with no .NET runtime on the PATH requirement (launched directly) against a temp `CONTROLDEFINANZASIA_DATA_DIR`, creates the database and a log file, and exits cleanly on a scripted close; the restart path resolves `Environment.ProcessPath` to the published executable.
- **S-43 — Cross-RID layout.** Given publish for `osx-arm64`, `osx-x64` and `linux-x64`, Then each succeeds and contains its RID native libs for SkiaSharp, QuestPDF and SQLite.
- **S-44 — Archives and checksums.** Given the four publishes, Then archive names match `ControlDeFinanzasIA-1.0.0-<rid>.{zip|tar.gz}` and every `SHA256SUMS.txt` line verifies.
- **S-45 — `.app` structure.** Given the osx publish, When `make-app-bundle.ps1` runs, Then the `.app` has `Contents/MacOS/<exe>`, `Contents/Resources/*.icns` and an `Info.plist` that parses as a valid plist with the expected keys and version `1.0.0`.
- **S-46 — AppImage recipe.** Given `scripts/appimage/`, Then the AppDir template, `.desktop` (valid keys) and icon exist and the build script references only files that exist.
- **S-47 — Workflow.** Given `release.yml`, Then it parses as valid YAML, triggers only on `v*` tags, and its release step sets `draft: true`.
- **S-48 — Footprint.** Given the diff, Then no migration file, no `Directory.Packages.props` package change and no schema snapshot change exist; the architecture tests for logger location and `System.Net.Http` pass.

## Acceptance Criteria

Legend: **[L]** verified locally (Windows, headless or scripted run) with the named test or script as evidence. **[U]** authored, unverified: needs a real runner or device and MUST stay unchecked until someone actually runs it.

### Verified locally [L]

- [ ] **(roadmap)** win-x64 self-contained build runs without .NET installed on PATH; other RIDs publish with native libs present (R-21, S-42, S-43). Evidence: `scripts/publish.ps1` run log + `PublishLayoutTests` (file presence per RID) + win smoke script.
- [ ] **(roadmap)** Register-expense flow 100% by keyboard (R-12, R-14, S-25, S-31). Evidence: `KeyboardOnlyExpenseFlowTests` (real `MainWindow`, real VM, temp SQLite).
- [ ] **(roadmap)** Every section shows data or an empty state; no hardcoded UI string in the four views (R-19, R-20, S-38..S-41). Evidence: `EmptyStateViewTests` (es-DO + en), `HardcodedStringsScanTests`, `ResourceCompletenessTests`.
- [ ] **(roadmap)** `1.0.0` versioned; CHANGELOG + README current; tag-ready (R-1, S-1, S-2). Evidence: `VersionTests`, `RepoDocsTests`.
- [ ] Icon set and `Window.Icon` (R-2, S-3). Evidence: `AppIconTests`.
- [ ] Rolling logger: path, format, level filter, 5 MB roll, date roll, keep 5, concurrency (R-3..R-5, S-4..S-9, S-11). Evidence: `RollingFileLoggerTests` (real provider, temp dir, fake clock).
- [ ] Logger survives unwritable directory and never throws; sink disabled after first failure (R-6, S-10). Evidence: `RollingFileLoggerFailureTests`.
- [ ] No API key in logs (R-5, S-12). Evidence: `LogPrivacyTests` (sentinel through real flows).
- [ ] Forced unexpected exception -> overlay with 8-hex id; same Guid in the log; `Result.Failure` unaffected (R-7, S-13, S-14). Evidence: `UnexpectedErrorReporterTests` (real reporter + real logger), `ErrorOverlayEndToEndTests`.
- [ ] Global hooks and startup failure path (R-8, R-9, S-15..S-18). Evidence: `GlobalExceptionHooksTests`, `StartupFailureTests` (child process, exit code).
- [ ] Overlay content, Copy without stack or user data, single overlay with counter, no auto-restart, restore rollback first (R-10, R-11, S-19..S-23). Evidence: `ErrorOverlayViewModelTests`, `ErrorOverlayHeadlessTests`, `RestoreUnhandledFailureTests`.
- [ ] Shortcut parity, navigation, gating (R-12, R-13, R-16, S-24..S-30, S-33). Evidence: `ShortcutCatalogParityTests`, `ShellShortcutTests`, `ShortcutGatingTests`, `SettingsShortcutListTests`.
- [ ] Sidebar rework and current-section visibility (R-15, S-32). Evidence: `SidebarHeadlessTests`.
- [ ] Automation names on all textless interactive controls; focus style; live region or documented limitation (R-17, S-34..S-36). Evidence: `AutomationNameScanTests`, `FocusStyleTests`, `LiveRegionSupportTests`, `docs/accessibility.md`.
- [ ] Palette contrast (R-18, S-37). Evidence: `ContrastTests`.
- [ ] Archives and SHA-256 (R-22, S-44). Evidence: `publish.ps1` run + `ChecksumVerificationTests`.
- [ ] macOS `.app` structure and `Info.plist` lint (R-23, S-45). Evidence: `AppBundleStructureTests` (folder + plist parse only).
- [ ] AppImage recipe files present and self-consistent; workflow YAML lint and `draft: true` (R-23, R-24, S-46, S-47). Evidence: `ReleaseWorkflowTests` (YAML parse), `AppImageRecipeTests`.
- [ ] No schema change, migration or new package; logger in Infrastructure; UI has no `System.Net.Http` error reporting; NetArchTest R-1..R-5 green (R-25, S-48). Evidence: `ArchitectureTests`, `git diff --stat` review.
- [ ] Tests never touch real AppData (R-26). Evidence: temp dirs / `CONTROLDEFINANZASIA_DATA_DIR` in all new tests.
- [ ] Gates: `dotnet build -c Release` 0 warnings/0 errors, `dotnet test` green, `dotnet format style --verify-no-changes` exit 0, docs reconciled (R-26), spec renamed to `.done.md` after archive.

### Authored, unverified [U] (needs real runner or device)

- [ ] Launch and behavior of the published `osx-arm64` / `osx-x64` / `linux-x64` builds on real macOS and Linux (native library loading, SkiaSharp, QuestPDF, SQLite).
- [ ] macOS `.app` launch, DMG build, Gatekeeper behavior; Meta shortcuts on a real Mac.
- [ ] AppImage build and run on a real Linux host.
- [ ] Release workflow execution (no remote exists; YAML lint only) and draft release creation.
- [ ] MSIX: deferred, not authored.
- [ ] Real Windows Narrator pass (only if run by hand); VoiceOver and Orca/AT-SPI behavior (best effort, untested).
- [ ] SmartScreen / Gatekeeper workaround texts in the README (text only, never exercised).
- [ ] Hand visual pass of empty states, error overlay, focus rings and icon in es-DO and en.
- [ ] A real-world crash on macOS/Linux routed through the overlay.

## Out of scope

MSIX; signed DMG; notarization and code signing; Flatpak/Snap; auto-update; crash upload or telemetry of any kind; shortcut remapping; universal macOS binary; trimming, single-file or ReadyToRun; verified VoiceOver/Orca support; high-contrast theme; full WCAG audit; Debts CSV export; any schema change or new NuGet package; persisted window state; an error-history screen.

## Assumptions forced at spec level

1. Short id = first 8 lowercase hex characters of the Guid (`N` format).
2. Line-per-entry logging escapes stack newlines so one entry stays one record (proposal said "one line per entry" and "stack" together).
3. "Newest 5 files" is computed over all `app-*.log` files in the folder, not per day.
4. The win smoke uses the published executable with a scripted close (headless-safe exit); exact mechanism is a design decision.
5. Contrast thresholds: 4.5:1 text, 3:1 focus ring and large text.
6. `Ctrl+,` and `Esc` are also disabled during a restore (proposal D-23 only named Ctrl+N and Ctrl+1..0 for busy flows).
