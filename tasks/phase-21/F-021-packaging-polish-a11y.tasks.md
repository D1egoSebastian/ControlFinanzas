# Tasks — F-021 Packaging, Polish and Accessibility (1.0.0)

> Governing inputs: `spec/features/phase-21/F-021-packaging-polish-a11y.spec.md` (requirements R-1..R-26, scenarios
> S-1..S-48), `.atl/sdd/f-021p21-packaging/design.md` (DD-1..DD-13, Threat Table T1..T18 and its 14-item "Proposal
> Reconciliation" are binding; **the design wins over the proposal and, where listed in the Reconciliation section
> below, over the spec's contract names**), `.atl/sdd/f-021p21-packaging/proposal.md` (D-1..D-28) and
> `.atl/sdd/f-021p21-packaging/exploration.md`. Format template: `tasks/phase-20/F-020-import.tasks.md`.
> Requirement ids (R-n) and scenario ids (S-n) refer to the spec. Design decisions are cited as DD-n, proposal
> decisions as D-n, threat rows as T-n. Slice names (S1..S10 with S5a/S5b and S7a/S7b) are the design's.
> Strict TDD: within each slice, failing tests come first, then implementation.

## Reconciliation (spec vs design; read before applying)

The spec is the requirement source; the design is the implementation source. Where they differ in a NAME or a MECHANISM
(never in a requirement's intent), the design wins and the spec's contract name is mapped here. The spec's own
"contract names" clause (spec intro) explicitly allows this. Every requirement still has a concrete artifact and a test
on the REAL implementation. At archive time the spec gets a short "Post-apply reconciliation" appended (Phase 20
precedent) restating this table.

| # | Spec says | Design says (WINS) | Consequence in these tasks |
|---|---|---|---|
| 1 | R-3 `AddFileLogging(...)` | `AddRollingFileLogging()`; the spec's `RollingFileLogger` is an inner logger type created by the provider; a `RollingFileSink` owns lock, stream and rolling | Tasks use the design names. |
| 2 | R-2 icon sources at `Assets/app-icon.*`; `Window.Icon` from the PNG | Folder `Assets/Icon/`; `MainWindow Icon="/Assets/Icon/app-icon.ico"`; `tools/IconRenderer` (outside the `.sln`) renders PNG/ICO/ICNS; `app-icon.png` (512) is kept as the spec's contract PNG next to sized `app-icon-*.png` | S3 icon test asserts the `Assets/Icon/` paths. |
| 3 | R-4 retention "by name order then timestamp" | Order by parsed (date, index) from the file name only, never by timestamp (filesystem flakiness) | Retention test uses names only. |
| 4 | R-5 stack lines "MAY be newline-escaped" (spec assumption 2) | Continuation lines are indented 4 spaces; every entry starts with a timestamp; entries above 64 K chars truncated with `…[truncated]` | Format tests assert indentation, not escaping. |
| 5 | R-8 hooks, no breaker | UI-thread hook sets `Handled = true` only after a successful report, with a breaker of 20 reports per 10 s (beyond it, log Critical once and leave `Handled = false`); hooks installed only for the desktop lifetime and disposed on exit | Extra breaker tests; hook tests install/dispose explicitly. |
| 6 | R-13 / assumption 6: `Ctrl+,` and `Esc` are no-ops during a restore | `IBusyAware` gates `NewExpense`, `NewIncome`, `GoToSection`, `OpenSettings` for BOTH busy flows (import commit and restore); `Esc` is handled by the overlay rule. Resolution: **the error overlay is always dismissable by `Esc` (overlay open beats busy)**; with no overlay open, `Esc` does not close the bell popup during a restore; `Ctrl+,` is a no-op during an import commit as well (stricter than spec, harmless) | Gating tests cover the overlay-over-restore case explicitly. |
| 7 | R-19 scan lists `Text=`, `Content=`, `Header=`, `Watermark=`, `ToolTip.Tip=`, `AutomationProperties.Name=` | Avalonia 11 uses `PlaceholderText`; design scan also covers it | Scan covers the union of both lists. |
| 8 | R-17 live region "if 11.2.7 exposes it" | The property exists in 11.2.7; set `LiveSetting="Polite"` on status blocks. Whether the Windows peer raises the event is unknown until the manual Narrator run | `docs/accessibility.md` states "set, peer behavior unverified"; S-36 asserts the property is set AND the doc sentence exists. |
| 9 | R-12 implies hand-declared `KeyBindings` | Bindings are GENERATED from the catalog by the `KeyboardShortcuts` behavior (`ForPlatform(OperatingSystem.IsMacOS())`); Meta duplicates exist ONLY on macOS (Win+1..9 / Win+, belong to the OS); the notifications `Popup` is a separate `TopLevel` and needs its own popup-local `Esc` binding | Parity test compares `ForPlatform(false)` on this machine; macOS Meta set is asserted on the catalog (pure) and stays [U] on a real Mac. |
| 10 | R-22 archive rule: `.zip` (win) or `.tar.gz` (osx, linux) | Design: osx ships as the `.app` zip, linux `.tar.gz` | Both are produced for osx: `ControlDeFinanzasIA-1.0.0-<rid>.tar.gz` (satisfies S-44 and R-22 literally) plus `ControlDeFinanzasIA-1.0.0-<rid>.app.zip` (design). Both listed in `SHA256SUMS.txt`. Flag for the owner at verify. |
| 11 | R-23 `scripts/make-app-bundle.ps1`, `scripts/appimage/` | `scripts/package-macos-app.ps1`, `packaging/macos/Info.plist.template`, `packaging/linux/{AppRun,*.desktop,build-appimage.sh}` | Design names used; S-45 and S-46 tests point at them. |
| 12 | R-21/R-22 silent on the tool | `#Requires -Version 7.2` (`TarWriter`, zip external attributes keep Unix exec bits from a Windows host); executable stays `ControlDeFinanzasIA.UI` | Task 9.1 verifies `pwsh` 7.2+ exists on this machine; if not, script tasks are written but marked UNVERIFIED. |
| 13 | R-1 informational version "starts with `1.0.0`" | `IncludeSourceRevisionInInformationalVersion=false`, so the string is exactly `1.0.0` (no `+sha`); one `AppVersionInfo.Current` replaces three reflection copies | `AppVersionTests` asserts exact equality. |
| 14 | R-19 "62 hardcoded literals" | Plus ~15 user-visible Spanish strings in `DebtsViewModel` (6) and `RecurringRulesViewModel` (~9) | S6 moves them too; otherwise "no hardcoded UI string" is only true in XAML. |
| 15 | R-18 contrast on "design-token pairs" | Measured: `Terracotta #D96459` on `Surface` = 4.43:1 (fails). New token `TerracottaTextColor #F08A82` (6.5:1) for text; `Terracotta` stays for fills and large amounts | S5a adds the token; the contrast test in S8 must not exclude any failing pair. |
| 16 | Spec test names (contract): `ContrastTests`, `HardcodedStringsScanTests`, `AutomationNameScanTests`, `LiveRegionSupportTests`, `ShellShortcutTests`, `AppBundleStructureTests` | Design names: `PaletteContrastTests`, `HardcodedUiStringTests`, `AccessibilityXamlScanTests` + runtime scan | Spec names are the acceptance evidence; the design's additional tests keep their names (static XAML scan is additional to the runtime scan). |
| 17 | S-18 startup failure "through the real Program entry, in a child process" | `StartupGuard.Run` with injectable writers for unit tests | Both: unit tests on `StartupGuard`, plus ONE child-process test that starts the real built app with `CONTROLDEFINANZASIA_DATA_DIR` pointing at a regular file (real failure, no test hook in production code; falls back to `%TEMP%`). |

**Spec/design gaps noted, no conflict:** the spec adds `IBusyAware` nowhere (design DD-6 introduces it; R-13 says
"read from existing ViewModel flags", which `IBusyAware` does by delegating, never duplicating state). The spec lists
`Esc` in the catalog; the design keeps Enter out of the catalog (`IsDefault`, listed in Settings text only).

> **Shared-file guard:** every touch on a shared file (`Bootstrap.cs`, `Program.cs`, `App.axaml.cs`,
> `MainWindow.axaml`, `ShellViewModel.cs`, `Strings.resx`, `Strings.es-DO.resx`, `Strings.Designer.cs`,
> `ArchitectureTests.cs`, `LedgerStyles.axaml`, `Tokens.axaml`, `AGENTS.md`) MUST be an appended block or an additive
> member unless the task says it restructures that file (only `MainWindow.axaml` root/sidebar in S4/S7b and
> `Program.Main` in S3). `Directory.Packages.props` stays UNCHANGED for the whole phase; `.csproj` changes are limited
> to the UI project (icon, resources, `Publish.props` import) (R-25). No migration, no schema snapshot change.
>
> **Anti-stub rules (Phase 18/19/20 lessons, apply to every slice):**
> 1. A task that implements behavior names the concrete artifact (file + type/method) AND the test that proves it
>    against the REAL implementation: `RollingFileLoggerProvider` writing real files in a temp dir, `UnexpectedErrorReporter`
>    over the real logger, the real `MainWindow` with the real `ShellViewModel`, the real `Program`/`StartupGuard`,
>    real scripts producing real archives. Fakes are allowed only for collaborators around the component under test
>    (`IClock`, `IClipboardService`, `IAppRestartService`), never for the component itself.
> 2. No task may be ticked while its artifact contains `NotImplementedException`, `TODO`, a stub, an empty method
>    body, a hard-coded return that mimics success, an empty script branch, or a test with no assertion. A partially met
>    task stays unticked and gets a note.
> 3. Every slice ends with a **gate block** (defined once below and referenced by number in each slice) including the
>    no-stubs grep, which MUST return nothing.
> 4. **Flakiness rule:** every slice runs the FULL suite (`dotnet test -c Release --no-build`) THREE consecutive times.
>    Any intermittent failure is reported with failing test names in the slice's apply notes. Logger concurrency,
>    child-process and timing tests are the likely intermittents.
> 5. **Threat rule:** every row T1..T18 and every scenario S-1..S-48 maps to at least one named test task (tables at
>    the end). A row is not "covered" by a fake of the same component.
> 6. Strict TDD honesty: if a test could not be run red first (it does not compile before the type exists), say so in
>    the apply notes instead of claiming a red/green cycle.
> 7. Tests never read or write the real `%APPDATA%\ControlDeFinanzasIA` (R-26): temp directories, `IClock` fakes, or
>    `CONTROLDEFINANZASIA_DATA_DIR`. Every `dotnet run`/published-exe smoke sets `CONTROLDEFINANZASIA_DATA_DIR` to a
>    throwaway directory first, and the real profile listing is recorded identical before/after.
> 8. **Honesty rule (spec goal):** only what was actually run on this Windows machine is called verified. macOS/Linux
>    runs, DMG, AppImage build, release workflow execution, Narrator/VoiceOver/Orca and hand visual passes stay
>    unticked and labelled "authored, unverified" in the apply notes and in Cierre.
> 9. **Localization rule:** new resx keys are added to BOTH `Strings.resx` (neutral English) and `Strings.es-DO.resx`
>    (neutral professional Spanish) and get a MANUAL accessor in `Strings.Designer.cs` (append-only). Generated
>    artifacts (code, comments, docs, resx neutral values) are English; es-DO values are Spanish.
> 10. **Visual rule:** every task that adds or changes UI appearance invokes `.claude/skills/frontend-design` (no generic
>     aesthetics; tokens only, no inline hex in touched views).

### Gate block (referenced as "Gate block G" in every slice; always the LAST task of the slice)

Each slice's final task is "Run gate block G" and must record, in the apply notes:

1. `dotnet build -c Release` : 0 warnings, 0 errors.
2. `dotnet test -c Release --no-build` : full suite green, FULL suite run 3 consecutive times; record unit + integration
   counts (baseline before this phase: 2551 unit + 772 integration = 3323; counts must only grow) and intermittent
   test names or "none". Confirm the slice's new test classes executed (N > 0, none skipped).
3. `dotnet format style --verify-no-changes` : exit 0.
4. NetArchTest: `dotnet test -c Release --no-build --filter Architecture` green (R-1..R-5 plus the Phase 21 guards
   once they exist).
5. No-stubs grep over the slice's touched `src` and test files returns NOTHING:
   `rg -n "NotImplemented|TODO|stub|Task\.FromResult\(Result\.Failure" <files touched by the slice>`; scripts also get
   `rg -n "TODO|not implemented|Write-Host .*stub" scripts packaging`.
6. Footprint check: `git diff --name-only` for the slice shows no `Directory.Packages.props`, no `Migrations/`, no
   `*ModelSnapshot.cs`, no `SettingKeys`/`AppSettings` change (R-25), and no test touching the real AppData.
7. Commit: one conventional commit for the slice, e.g. `feat(phase-21): <summary> (slice S<n>/12)`. The message has NO
   `Co-Authored-By` line and no AI attribution of any kind; verify with `git log -1 --format=%B`.

## Review Workload Forecast

| Field | Value |
|---|---|
| Estimated changed lines | ≈4,300 total across 12 chained slices (production + tests + resx/Designer, excluding binary assets): S1 ~380 (+binary, docs), S2 ~450, S3 ~420, S4 ~380, S5a ~400, S5b ~300, S6 ~370, S7a ~450, S7b ~380, S8 ~370, S9 ~350 (scripts), S10 ~150 (docs) |
| 400-line budget risk | High for S2, S3, S7a (each at or above 420). Split seams: S2 → "formatter + sink" / "provider + DI + privacy"; S3 → "reporter + startup guard" / "hooks + restore test"; S7a → "catalog + behavior" / "shell commands + gating + keyboard E2E". If a slice grows past ~450 lines during apply, stop and split at its seam |
| Chained PRs recommended | Yes |
| Suggested split | PR S1 → S2 → S3 → S4 → S5a → S5b → S6 → S7a → S7b → S8 → S9 → S10 |
| Delivery strategy | auto-chain |
| Chain strategy | stacked-to-main |

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: High

The design's slice order is authoritative and strictly chained. S1 is identity (version, icon, docs, `AppVersionInfo`).
S2 is the Infrastructure file logger. S3 is the Application-facing reporter, global hooks and startup failure path.
S4 is the error overlay view and its host in `MainWindow`, plus `FocusOnRequest`. S5a/S5b introduce `EmptyState`,
the `TerracottaText` token and localization for Expenses (S5a) and Income (S5b). S6 covers Debts/Recurring, the sidebar
labels and VM strings. S7a is the catalog, behavior, shell commands and gating; S7b is the sidebar rework, focus
styles, `IsDefault` and the Settings list. S8 is the accessibility pass. S9 is packaging. S10 is docs close-out.

**Placement notes (recorded up front):** (a) the design file table puts `FocusOnRequest.cs` and `FocusRequestToken` in
S4; S5a/S7a consume them. (b) `ErrorOverlay*` resx keys ship with the S4 view, not S3 (S3 has state only; the overlay
copy payload is invariant English by design). (c) `Shortcut*` action-name resx keys ship in S7a (the catalog exposes
`DescriptionKey`) and are consumed by S7b's Settings list. (d) `README.md` is rewritten in S1 with every section present;
shortcut summary and SmartScreen/Gatekeeper texts are final there (their facts are already fixed by the spec); S10 only
re-checks README against the final catalog.

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|---|---|---|---|
| S1 | Identity + repo docs | PR S1 | Version 1.0.0, `AppVersionInfo`, icon set + renderer, README/CHANGELOG/LICENSE. R-1, R-2. |
| S2 | Rolling file logger | PR S2 | Infrastructure only. R-3..R-6, S-4..S-12, T1-T3. |
| S3 | Reporter, hooks, startup path | PR S3 | R-7..R-9, R-11 (hooks half), S-13..S-18, S-23, T4, T7, T17. |
| S4 | Error overlay + focus behavior | PR S4 | R-10, R-11, S-19..S-22, T5, T6, T8. |
| S5a | EmptyState + Expenses | PR S5a | R-19/R-20 for Expenses, `TerracottaText`, generic `StringsParityTests`. |
| S5b | Income | PR S5b | Same pattern for Income. |
| S6 | Debts + Recurring + sidebar labels | PR S6 | R-19/R-20 rest, VM literals (~15). |
| S7a | Shortcut catalog + shell commands + gating + keyboard E2E | PR S7a | R-12..R-14, S-24..S-31, T9, T10. |
| S7b | Sidebar rework, focus styles, `IsDefault`, Settings list | PR S7b | R-15, R-16, S-32, S-33, S-35, T11. |
| S8 | Accessibility pass + statement | PR S8 | R-17, R-18, S-34, S-36, S-37, T12. |
| S9 | Packaging scripts + workflow | PR S9 | R-21..R-24, S-42..S-47, T13-T16. |
| S10 | Docs close-out | PR S10 | R-26 docs, footprint proof S-48. |

---

## 1. Identity and repo docs (S1)

Depends on nothing (Phases 0-20 archived). Goal: `1.0.0` from exactly one place, one version reader, the icon set with
`Window.Icon`, and the three repo documents. Size note: binary assets and prose are not counted in the line budget.

- [x] 1.1 Write failing `tests/…Unit/Packaging/AppVersionTests.cs` against the REAL built UI assembly and the REAL
  props files: (a) `AppVersionInfo.Current` equals exactly `1.0.0` (no `+sha`); (b) exactly one `<Version>` element across
  `Directory.Build.props`, every `*.props` and every `*.csproj` under the repo (parsed as XML; locate the repo root by
  walking up to `ControlDeFinanzasIA.sln`); (c) `IncludeSourceRevisionInInformationalVersion` is `false` in the props;
  (d) `SettingsViewModel.AppVersion` and the backup manifest version source both return `AppVersionInfo.Current`
  (real VMs/ports, existing fakes only for collaborators). (R-1, S-1, DD-12)
- [x] 1.2 Edit `Directory.Build.props` (additive): `<Version>1.0.0</Version>` and
  `<IncludeSourceRevisionInInformationalVersion>false</IncludeSourceRevisionInInformationalVersion>`. Create
  `src/…UI/Diagnostics/AppVersionInfo.cs` (`Current`, reads `AssemblyInformationalVersionAttribute`, strips nothing
  because the revision suffix is disabled) and switch the three reflection copies (`SettingsViewModel`,
  `BackupSettingsViewModel`, `Bootstrap.ReadAppVersion`) to it. Run 1.1 green. (R-1, DD-12 Version row)
- [x] 1.3 Write failing `tests/…Unit/Packaging/IconAssetTests.cs`: `Assets/Icon/app-icon.svg`, `app-icon.png`, `app-icon.ico`,
  `app-icon.icns` exist; the SVG contains only `<rect>`/`<path d fill>` elements (renderer subset); PNG header + 1024/512
  dimensions read from the IHDR; ICO header reports the 6 expected entries (16/24/32/48/64/256); ICNS magic `icns`,
  declared length equals file length, contains the `ic07`..`ic10` chunks; `UI.csproj` sets `ApplicationIcon` and an
  `AvaloniaResource` for `Assets\Icon\*.ico;*.png`; the REAL headless `MainWindow` (via `HeadlessUi`) has a non-null
  `Icon`. (R-2, S-3, DD-12 Icon row)
- [x] 1.4 Invoke `frontend-design`, then author `src/…UI/Assets/Icon/app-icon.svg` (Ink rounded tile, three Paper
  ledger rules, one Amber rising tick; 16/24 px variants drop to two rules). Create `tools/IconRenderer/` (outside the
  `.sln`; Avalonia + Skia already in CPM, NO new package; parses the SVG subset into `Geometry.Parse`, renders PNGs
  16…1024 via `RenderTargetBitmap`, packs ICO and ICNS byte-for-byte) and `scripts/render-icon.ps1`. Run the renderer
  once and COMMIT the outputs. Edit `UI.csproj` and `MainWindow.axaml` (`Icon="/Assets/Icon/app-icon.ico"`). Run 1.3
  green; open the PNG and look at it once (a rendered glyph that is blank is a stub). (R-2, DD-12)
- [x] 1.5 Write failing `tests/…Unit/Packaging/RepoDocsTests.cs` (reads repo files): `CHANGELOG.md` has `## [Unreleased]`
  and `## [1.0.0]` with Keep-a-Changelog category headings and entries for every feature area Phases 0-21;
  `LICENSE` exists and contains "All rights reserved"; `README.md` contains headings for: what the app is, download and
  run per OS (names the file to run, `ControlDeFinanzasIA.UI`), build from source, data directory and
  `CONTROLDEFINANZASIA_DATA_DIR`, keyboard shortcuts, SmartScreen workaround, Gatekeeper workaround, and the literal
  statement that macOS and Linux builds are "provided, untested"; README states the ICU prerequisite for Linux (T14).
  (R-1, S-2, T14, T15)
- [x] 1.6 Write `CHANGELOG.md`, `LICENSE`, and rewrite `README.md` (English). The shortcut summary lists exactly the
  spec R-12 gestures. The annotated tag `v1.0.0` is NOT created (owner request only). Run 1.5 green. (R-1, D-28)
- [x] 1.7 **Run gate block G** (grep files: `AppVersionInfo.cs`, edited VMs, `Bootstrap.cs`, the two test files, the
  _Apply notes S1 (Strict TDD): tests written first; red = compile failure (AppVersionInfo missing) before implementation, then green. 18 new tests (AppVersionTests 6, IconAssetTests 8, RepoDocsTests 4) all executed. Gate G: build 0/0; full suite x3 = 2569 unit + 772 integration (baseline 2551 + 772, +18), no intermittents; format exit 0; Architecture filter green; footprint clean (no Packages.props, Migrations, snapshot, SettingKeys change). No-stub grep: only a pre-existing comment in Bootstrap.cs:224 ("provider stub", Phase 5). Icon rendered once with tools/IconRenderer (SkiaSharp via Avalonia.Desktop, outside the .sln, no package added) and viewed. pwsh is not installed on this machine, so scripts/render-icon.ps1 is authored but unexecuted (the renderer was run with dotnet run directly). Tag v1.0.0 not created._
  renderer). Also record `git status` showing the binary assets and the renderer outside the `.sln`.

## 2. Rolling file logger (S2)

Depends on 1. Goal: a real, never-throwing, rolling file provider in Infrastructure, registered once in `Bootstrap`.
No UI type except `Bootstrap` may reference `Infrastructure.Logging` (R-4 of the arch rules). Tests live in the
INTEGRATION project (real files, real DI), using temp directories and a fake `IClock`. Size note: if it exceeds ~450
lines split at "formatter + sink" vs "provider + DI + privacy".

- [x] 2.1 Write failing `tests/…Integration/Logging/LogEntryFormatterTests.cs` against the REAL `LogEntryFormatter.Format`:
  line is `2026-09-30T14:03:11.123Z INF Category[EventId] message` (UTC ISO-8601, 3-letter level, category, event id);
  exception appended as `type: message` followed by the stack with every continuation line indented 4 spaces; an inner
  exception chain is included; an entry over `MaxEntryChars` ends with `…[truncated]`; the local time zone (set to a
  non-UTC zone in the test) never changes the timestamp. (R-5, S-11, Reconciliation #4)
- [x] 2.2 Write failing `tests/…Integration/Logging/RollingFileSinkTests.cs` against the REAL `RollingFileSink` in a temp
  dir with a fake clock: first write creates `app-<utc yyyyMMdd>.log`; with a tiny `MaxFileBytes` the next write rolls to
  `app-YYYYMMDD.1.log`, `.2.log`, and no file exceeds the cap by more than one entry; crossing UTC midnight via the
  clock opens `app-<next date>.log`; a fresh sink over an existing directory resumes today's highest index when under
  the cap; 8 pre-existing files plus a roll leaves exactly the newest 5 ordered by parsed (date, index) (set odd
  `LastWriteTime` values to prove timestamps are ignored); files from a previous "run" are pruned the same way;
  a delete failure (file opened with `FileShare.None`) is reported once to stderr and does NOT disable the sink.
  (R-4, S-6, S-7, S-8, T2, Reconciliation #3)
- [x] 2.3 Write failing `tests/…Integration/Logging/RollingFileSinkConcurrencyTests.cs`: 32 threads x 200 lines through the
  REAL provider produce a file with exactly 6,400 intact lines (each matches the timestamp-prefixed pattern, none
  interleaved); uses one provider instance and real files. (R-3, S-9)
- [x] 2.4 Write failing `tests/…Integration/Logging/RollingFileLoggerFailureTests.cs`: (a) `logs` path is a regular file;
  (b) the log file is held open with `FileShare.None`; (c) missing/invalid directory. In each case `Log`, `IsEnabled` and
  `Dispose` never throw, `provider.IsDisabled` becomes true after the FIRST failure, exactly one stderr line is written
  (capture `Console.Error`), later calls do not retry (count open attempts through a wrapper-free check: the locked
  file is released and a later call still writes nothing), and a console `ILoggerProvider` composed in the same
  `LoggerFactory` still receives the entries. (R-6, S-10, T3)
- [x] 2.5 Write failing `tests/…Integration/Logging/LoggingRegistrationTests.cs` with REAL DI (`ServiceCollection` +
  `AddRollingFileLogging()` + a real `IPathResolver` rooted at a temp dir): the provider is a Singleton resolved by
  `ILoggerFactory`; `Debug` entries and `Microsoft.EntityFrameworkCore` `Information` entries never reach the file, a
  `Microsoft.*` `Warning` does, an app-category `Information` does; the file lands in
  `<DataDir>/logs/app-<utc date>.log` honoring `CONTROLDEFINANZASIA_DATA_DIR` (set and restored inside the test);
  `ClearProviders()` followed by `Bootstrap` registration keeps the provider (simulate `Program`'s order). (R-3, R-5,
  S-4, S-5, DD-1 Registration row)
- [x] 2.6 Create `src/…Infrastructure/Logging/{RollingFileLoggerOptions, LogEntryFormatter, RollingFileSink,
  RollingFileLogger, RollingFileLoggerProvider, LoggingServiceCollectionExtensions}.cs` per DD-1 (public types,
  `FileStream(Append, FileShare.ReadWrite | FileShare.Delete)`, UTF-8 no BOM, single `lock`, flush per entry, no
  background thread, scopes ignored, `MinLevel` filter rules via `Configure<LoggerFilterOptions>`; NO new package;
  EF sensitive-data logging not enabled anywhere). Run 2.1-2.5 green. (R-3..R-6, DD-1)
- [x] 2.7 Edit `src/…UI/Composition/Bootstrap.cs` (additive): `services.AddRollingFileLogging();`. Do NOT touch
  `Program.cs` logging here (R-4: only `Bootstrap` may reference Infrastructure). Write
  `tests/…Integration/Logging/BootstrapLoggingTests.cs`: building the REAL `Bootstrap` service provider against a temp
  `CONTROLDEFINANZASIA_DATA_DIR` and logging once through `ILogger<T>` creates the log file. (R-3, Reconciliation #8 of
  the design)
- [x] 2.8 Write failing `tests/…Integration/Logging/LogPrivacyTests.cs` with sentinel key `sk-SENTINEL-9f3a` through the
  REAL flows with the REAL file provider: (a) save AI settings via the real settings use case; (b) a real LLM call
  failure (`OpenAICompatibleClient` against a handler that returns 401/timeout, resilience pipeline real); (c) a real
  user export/backup through the real `SqliteBackupService` (the key is redacted in user exports but may live in safety
  ZIPs, which are not logs). After each flow, every file in `logs/` is read in full and MUST NOT contain the sentinel
  (nor the `Authorization` header value). If a flow does log the key, fix the logging call site (test-first), never the
  test. (R-5, S-12, T1)
- [x] 2.9 Append to `tests/…Unit/Architecture/ArchitectureTests.cs` (D-16/DD-13): `RollingFileLoggerProvider`,
  `RollingFileLogger`, `RollingFileSink`, `LogEntryFormatter` reside in `ControlDeFinanzasIA.Infrastructure.Logging`;
  no type outside Infrastructure except the type named `Bootstrap` depends on that namespace; R-1..R-5 stay green.
  (R-25)
- [x] 2.10 **Run gate block G** (grep files: `Infrastructure/Logging/*`, `Bootstrap.cs`, the new test files). Also record
  that no test wrote to the real AppData (list the temp roots used).

_Apply notes S2 (Strict TDD): tests were authored before the implementation but the first compile ran with both present, so no separate red run was observed. Two honest reds did occur against real code: (1) a test-side file-share bug in the delete-failure test; (2) a REAL production finding in 2.8: Microsoft.Extensions.Http logs the `Authorization: Bearer <key>` header at Trace in category `System.Net.Http.HttpClient.llm-*`. The file provider never receives Trace (rule Information), but the key existed in the pipeline; fixed at the call site with `.RedactLoggedHeaders(_ => true)` on both named LLM clients in `LlmServiceCollectionExtensions` (test stays as-is, a Trace in-memory provider proves no level leaks the key). Gate G: build 0/0; full suite x3 = 2570 unit + 807 integration (baseline 2569 + 772; +1 arch test, +35 logging/privacy tests), 0 skipped, no intermittents; format exit 0; Architecture filter 19/19; no-stub grep empty; footprint clean (no Packages.props/Migrations/snapshot/SettingKeys). Temp roots: `%TEMP%\cdfia-log-<guid>` (TempDir) and the env-var override for the Bootstrap/privacy tests; real `%APPDATA%\ControlDeFinanzasIA` has no `logs` folder after the runs. Deviations: `RollingFileSink` also exposes `IsDisabled`/`Dispose`; `RollingFileLogger` is internal (created only by the provider); the locked-file and delete-failure tests are Windows-only (`WindowsOnlyFact`, Linux/macOS file-sharing semantics differ) and were run on Windows only. Retention report text contains the word "retention" (asserted). Commit: see git log (slice S2/12)._

## 3. Unexpected-error reporter, global hooks and startup failure path (S3)

Depends on 2. Goal: one Guid per unexpected failure, written in full to the real log and exposed as an 8-hex short id;
global hooks never throw; a pre-UI failure leaves a file and a non-zero exit. The overlay is only a state object here
(`ErrorOverlayViewModel` with `IErrorPresenter`); its view is S4. Size note: if above ~450 lines split at "reporter +
startup guard" vs "hooks + restore test".

- [x] 3.1 Write failing `tests/…Integration/Diagnostics/UnexpectedErrorReporterTests.cs` with the REAL
  `UnexpectedErrorReporter` over the REAL `RollingFileLoggerProvider` (temp dir, fake `IClock`) and the real
  `ErrorOverlayViewModel` as presenter: reporting an `InvalidOperationException` with an inner exception returns a Guid;
  `CorrelationId.Short(guid)` is 8 lowercase hex chars equal to the first 8 of `guid.ToString("N")`; the log file holds an
  `ERR` line containing the FULL Guid and the exception type, message and stack for that same Guid; the presenter
  received `(guid, clock.UtcNow)`; a presenter that throws still leaves the log entry and `Report` does not throw; a
  logger that is disabled (provider `IsDisabled`) still does not throw and writes a best-effort stderr line; no network
  type is used (see 3.8). (R-7, S-13, S-17, T4)
- [x] 3.2 Write failing `tests/…Unit/Diagnostics/ResultFailureUnchangedTests.cs` against the REAL use case + VM paths
  that return `Result.Failure` (pick the Expense register path with an invalid amount): no correlation id is generated,
  the reporter fake/spy is never called, nothing is logged at `Error` by the reporter, and the inline message is
  unchanged. (R-7, S-14)
- [x] 3.3 Create `src/…UI/Diagnostics/{IUnexpectedErrorReporter, UnexpectedErrorReporter, IErrorPresenter,
  CorrelationId}.cs` and `src/…UI/ViewModels/ErrorOverlayViewModel.cs` (state half: `IsOpen`, `CorrelationId`, `ShortId`,
  `OccurredAtUtc`, `AdditionalCount`, `AdditionalText`, `Present`, `Close`, `CopyText`; commands and view come in S4).
  Log template `"Unexpected error {CorrelationId} in {Source}"` with the exception. Register in `Bootstrap` (additive):
  `ErrorOverlayViewModel` singleton, `IErrorPresenter` forwarding to it, `IUnexpectedErrorReporter`. Run 3.1, 3.2 green.
  (R-7, DD-2, DD-13)
- [x] 3.4 Write failing `tests/…Unit/Diagnostics/GlobalExceptionHooksTests.cs` (headless + real reporter + real provider in
  a temp dir; each test installs and DISPOSES the hooks, T17): (a) a command that throws on the UI thread (posted to
  `Dispatcher.UIThread`) sets `UnhandledException.Handled = true`, opens the overlay with the short id, and the
  dispatcher keeps running (a second posted job executes); (b) the breaker: 21 reports within 10 s (fake clock) makes the
  21st leave `Handled = false` and log Critical once; (c) an unobserved faulted `Task` (force GC or raise the registered
  handler with `UnobservedTaskExceptionEventArgs`) logs with a Guid at Warning, calls `SetObserved`, no overlay opens;
  (d) the `AppDomain.UnhandledException` handler logs Critical with a Guid, no UI, does not throw; (e) a reporter that
  throws (forced logger failure) still lets every hook return. (R-8, S-15, S-16, S-17, T4, T17)
- [x] 3.5 Create `src/…UI/Diagnostics/GlobalExceptionHooks.cs` (`Install(IUnexpectedErrorReporter) : IDisposable`,
  20-per-10-s breaker using `IClock`). Edit `src/…UI/App.axaml.cs` (additive): call `Install` INSIDE the
  desktop-lifetime branch of `OnFrameworkInitializationCompleted` and dispose on `desktop.Exit`; the headless session
  (non-desktop lifetime) never installs it. Run 3.4 green. (R-8, DD-2)
- [x] 3.6 Write failing `tests/…Integration/Restore/RestoreUnhandledFailureTests.cs` using the existing Phase-19 restore
  harness (find the real `SqliteBackupService` restore tests and reuse their temp-file SQLite setup): force an
  UNEXPECTED exception inside the swap (e.g. a file-system seam already used by the Phase-19 rollback tests); assert the
  ORIGINAL database bytes are intact (rollback ran) before the exception reaches a registered hook, and that the hook
  then reports it (log contains the Guid). No change to Phase-19 production code is allowed; if the exception cannot
  be forced without one, stop and record it instead of adding a hook. (R-8, R-11, S-23, T4)
- [x] 3.7 Write failing `tests/…Unit/Diagnostics/StartupGuardTests.cs` against the REAL `StartupGuard.Run` and
  `StartupFailureWriter` with injected stderr and clock: a body that throws returns exit code 1, writes
  `startup-error-<yyyyMMddTHHmmssZ>.log` to the preferred temp DataDir containing app version, UTC time, OS/RID, Guid,
  `exception.ToString()` and NO environment/secret values; an unwritable preferred directory (a regular file) falls back
  to `Path.GetTempPath()` (use a unique temp subfolder override to avoid polluting the real `%TEMP%`); a double failure
  (both locations unwritable) prints the exception to stderr only and still returns 1; a body that returns 0 returns 0.
  (R-9, S-18, T7)
- [x] 3.8 Create `src/…UI/Diagnostics/{StartupGuard, StartupFailureWriter}.cs` and edit `src/…UI/Program.cs`
  (restructure `Main` to `int Main`; the whole body, host build included, runs inside `StartupGuard.Run`; `Program` still
  does not reference Infrastructure). Write the child-process test
  `tests/…Integration/Diagnostics/StartupFailureProcessTests.cs` (Reconciliation #17): launch the REAL built UI
  (`dotnet <ControlDeFinanzasIA.UI.dll>` from the test output or build folder) with
  `CONTROLDEFINANZASIA_DATA_DIR` set to a path that is a regular file; assert non-zero exit code within a 60 s kill
  timeout, a `startup-error-*.log` in `%TEMP%` (cleaned up by the test after listing which file it created), and the
  Guid on stderr. If the failure cannot be produced without a production test hook, stop and report; do NOT add an
  env-var failure switch to `Program`. Run 3.7 green. (R-9, S-18, DD-3)
- [x] 3.9 Append to `tests/…Unit/Architecture/ArchitectureTests.cs` (R-25/DD-13): `UnexpectedErrorReporter`,
  `GlobalExceptionHooks`, `ErrorOverlayViewModel` reside in `ControlDeFinanzasIA.UI`; the namespace
  `ControlDeFinanzasIA.UI.Diagnostics` has no dependency on `System.Net`, `System.Net.Http` or `System.Net.Sockets`
  (no upload path can exist). (R-7, R-25, S-48)
- [x] 3.10 **Run gate block G** (grep files: `UI/Diagnostics/*`, `ErrorOverlayViewModel.cs`, `Program.cs`,
  `App.axaml.cs`, the new tests). Confirm the child-process test left no stray `startup-error-*.log` or process behind.
  If it is timing-sensitive, report its three measured durations.

_Apply notes S3 (Strict TDD, honest): the production types were drafted BEFORE the tests, so no red run was observed for S3 (the tests did not exist yet when the types compiled); they went green on the first or second run against the real implementation. Gate G: build 0/0; full suite x3 consecutive = 2582 unit + 815 integration (baseline 2570 + 807; +12 unit, +8 integration), 0 skipped, no intermittents; format exit 0; Architecture filter 20/20 (new F021 reporter/no-network guard); no-stub grep empty over the slice files; footprint clean (no Packages.props, Migrations, snapshot, SettingKeys/AppSettings); real `%APPDATA%\ControlDeFinanzasIA` still has no `logs` folder and no stray `startup-error-*.log` in `%TEMP%`. New tests: Integration `UnexpectedErrorReporterTests` (5), `ErrorReportingCompositionTests` (1, real Bootstrap), `RestoreUnhandledFailureTests` (1), `StartupFailureProcessTests` (1, real child process, single run ~1 s); Unit `GlobalExceptionHooksTests` (5), `StartupGuardTests` (5), `ResultFailureUnchangedTests` (1). Deviations: (1) `ErrorOverlayViewModel` ctor is `(string appVersion)` only; the `IClipboardService` parameter, `CopyAsync`, `InitialFocus` and localized captions move to S4 (an unused clipboard field would be dead code now); `AdditionalText` is the invariant English `+N more` until S4 adds the `ErrorOverlayMore` resx key. (2) `GlobalExceptionHooks.Install` has an extra overload taking `IClock` (breaker window); handler bodies are internal (`Subscription.HandleUiException` etc.) and the real Avalonia dispatcher path is also exercised in headless (`UiThreadFailure_...`). (3) `StartupGuard.Run`/`StartupFailureWriter.Write` take extra optional `fallbackDirectory`/`Guid` params (test seam for the fallback folder; the Guid is written to the file and stderr). (4) `UnexpectedErrorReporter` always writes one best-effort stderr line per report, not only when the file logger is disabled. (5) 3.6: the seam exists (`FaultyBackupFileSystem.On("Replace").Throws(new InvalidOperationException(...))` escapes `RestoreAsync` because its catch filters only cover I/O/SQLite/rejected). The original stays byte-identical because the injected `Replace` throws BEFORE touching the file, so the rollback path itself is not what is proven; the test then reports the escaped exception through the real reporter (log holds the Guid). Observation only, Phase 19 code untouched: an unexpected non-I/O exception thrown AFTER a successful `Replace` would not trigger the rollback (catch filters), which is a Phase 19 follow-up candidate, not exercised here. (6) 3.6 routes through `reporter.Report` directly (the integration project cannot reach the internal hook handler); hook routing itself is covered by `GlobalExceptionHooksTests`. (7) `ExpensesViewModelTests.CreateViewModel`/`CreateCategory` changed from private to internal so `ResultFailureUnchangedTests` reuses the real builder. Not verified: `App.OnFrameworkInitializationCompleted` hook installation/disposal (no desktop-lifetime harness; covered by build + review only), hook disposal removing the events (no direct test), a real `Dispatcher` unhandled exception in the running desktop app.

## 4. Error overlay and focus behavior (S4)

Depends on 3. Goal: the in-window overlay with Copy/Close/Esc, hosted in `MainWindow` (no new `Window`, ADR-004), the
background disabled while modal, and the reusable `FocusOnRequest` behavior. Uses `frontend-design` (DD-4 visual).

- [x] 4.1 Write failing `tests/…Unit/ViewModels/ErrorOverlayViewModelTests.cs` against the REAL `ErrorOverlayViewModel`
  with a fake `IClipboardService` and fake clock: `CopyAsync` sends exactly
  `ControlDeFinanzasIA {version}\nError ID: {short} ({full Guid})\nTime (UTC): {yyyy-MM-ddTHH:mm:ssZ}` and the text
  contains no `"   at "`, no exception message or type, no path, and none of a set of expense descriptions/amounts
  seeded into the test; `Present` while open increments `AdditionalCount` ("+1 more"), logs nothing itself, keeps the
  FIRST id; `Close` resets everything; after copy the "Copied" caption flag is set; the VM never calls
  `IAppRestartService` (inject a spy: zero calls after close). (R-10, R-11, S-20, S-21, S-22, T5, T6)
- [x] 4.2 Add the `ErrorOverlayViewModel` commands (`[RelayCommand] CopyAsync`, `Close`), `InitialFocus`
  (`FocusRequestToken`) and create `src/…UI/Behaviors/FocusOnRequest.cs` + `src/…UI/Input/FocusRequestToken.cs`
  (DD-5: fresh token per request; waits for `AttachedToVisualTree` when not yet attached;
  `FirstFocusableToken` via `KeyboardNavigationHandler`). No logic in code-behind. Run 4.1 green. (R-10, R-12 prerequisite,
  DD-4, DD-5)
- [x] 4.3 Write failing `tests/…Unit/UI/FocusOnRequestTests.cs` (headless, real controls): setting a new token on a
  `TextBox` VM property focuses it; the same property set twice (two fresh tokens) focuses twice; a token raised before
  the control is attached focuses it once attached; a disabled control is not focused and does not throw. (R-12, R-14,
  DD-5)
- [x] 4.4 Invoke `frontend-design`, then create resx keys (`ErrorOverlayTitle`, `ErrorOverlayBody`,
  `ErrorOverlayRestartHint`, `ErrorOverlayIdLabel`, `ErrorOverlayMore` with `{0}`, `ErrorOverlayCopy`,
  `ErrorOverlayCopied`, `ErrorOverlayClose`) in both resx files + manual Designer accessors, then create
  `src/…UI/Views/ErrorOverlayView.axaml(.cs)` (scrim 80 % Ink, 440 px card on Surface, 2 px Terracotta top rule, mono
  `REF` line, Copy `ledger-btn`, Close `ledger-btn-primary`, 150 ms fade, `KeyboardNavigation.TabNavigation="Cycle"`,
  `AutomationProperties.Name` = title, `LiveSetting="Polite"` on the body, local `Esc` KeyBinding to `CloseCommand`; tokens
  only, no inline hex). Edit `MainWindow.axaml` (restructure root to a `Panel`: existing `Grid` with
  `IsEnabled="{Binding !IsModalOpen}"` plus `<views:ErrorOverlayView DataContext="{Binding ErrorOverlay}"
  IsVisible="{Binding ErrorOverlay.IsOpen}"/>`) and `ShellViewModel.cs` (optional 4th constructor parameter
  `ErrorOverlayViewModel? errorOverlay = null`, `ErrorOverlay`, `IsModalOpen`; opening the overlay closes the notifications
  popup) so the existing `ShellViewModelTests` compile unchanged. (R-10, R-11, DD-4)
- [x] 4.5 Write `tests/…Unit/UI/ErrorOverlayHeadlessTests.cs` (real `MainWindow`, real `ShellViewModel`, real overlay VM,
  headless): reported error makes the overlay visible over the shell with the localized title and short id in es-DO AND
  en; Close has focus on open; Tab cycles inside the card only; `Esc` and Close dismiss it; the shell stays interactive
  and `IAppRestartService` spy has zero calls after close; with the overlay open, pressing `Enter` does NOT fire the
  background `IsDefault` button (register an expense in a real Expenses VM over a temp DB and assert no row was added);
  the notifications popup is closed when the overlay opens; a second `Present` shows "+1 more" and keeps the first id.
  (R-10, R-11, S-19, S-21, S-22, T5, T8)
- [x] 4.6 Write end-to-end `tests/…Integration/Diagnostics/ErrorOverlayEndToEndTests.cs`: real `Bootstrap` provider
  against a temp DataDir, headless `MainWindow`, a real command that throws unexpectedly; the overlay's short id equals
  the first 8 hex of the Guid that appears in the REAL log file at `ERR`. (R-7, R-10, S-13, S-19)
- [x] 4.7 **Run gate block G** (grep files: `ErrorOverlayView.axaml(.cs)`, `FocusOnRequest.cs`, `FocusRequestToken.cs`,
  `MainWindow.axaml`, `ShellViewModel.cs`, resx, new tests). Also confirm the existing `ShellViewModelTests` and
  `MainWindow` headless tests pass unmodified.

_Apply notes S4 (Strict TDD, reds observed): to get behavioral reds (not just compile errors) the public surface was first added as inert skeletons (empty `FocusOnRequest` handlers, no-op `CopyAsync`, English-only `AdditionalText`, empty `ErrorOverlayView`, `ShellViewModel` members) and the tests were run: `ErrorOverlayViewModelTests` + `FocusOnRequestTests` = 11 failed / 1 passed of 12 (the passing one is the constructor-shape test, true of the skeleton); `ErrorOverlayHeadlessTests` = 7 failed / 1 passed of 8 (the IsDefault test failed with `Expected: 0` because nothing disabled the background yet). Then implementation: 12/12 and 8/8 green. Not red-first: `ErrorOverlayResourcesTests` (3) and `ShellViewModelOverlayTests` (3) and `ErrorOverlayEndToEndTests` (1) were written after the code they cover. Gate G: build 0/0 (`--no-incremental`); full suite x3 = 2609 unit + 815 integration each run (baseline 2582 + 815; +27 unit: ErrorOverlayViewModelTests 7, FocusOnRequestTests 5, ErrorOverlayHeadlessTests 8 cases, ErrorOverlayResourcesTests 3, ShellViewModelOverlayTests 3, ErrorOverlayEndToEndTests 1), 0 skipped, no intermittents; format exit 0; Architecture filter 20/20; no-stub grep over the slice files empty; footprint clean (no Packages.props, Migrations, snapshot, SettingKeys/AppSettings, csproj); no stray dotnet/testhost process after `dotnet build-server shutdown`. `ShellViewModelTests`, the `MainWindow` icon headless test and all S1-S3 tests pass unmodified. Deviations: (1) `ErrorOverlayViewModel` ctor is now `(IClipboardService, string)`; the 4 S3 call sites (`Bootstrap`, `ReporterRig`, two Integration tests via a new `RecordingClipboardService`) were updated. (2) 4.5 uses the real `ExpensesViewModel` over the existing fake repositories (`ExpensesViewModelTests.CreateViewModel`), not a temp SQLite DB. (3) 4.5 "IAppRestartService spy has zero calls": the overlay has no such dependency by construction, so a spy injected anywhere would be vacuous; proven instead by a reflection test on the constructor and fields (`ItNeverDependsOnTheRestartService_ByConstruction`). (4) No `IsDefault` button exists in the app before S7b, so 4.5 arms the REAL "Registrar gasto" button with `IsDefault = true` inside the test; with the overlay open Enter adds 0 expenses, after close Enter adds 1 (control case). (5) 4.6 lives in `tests/…Unit/Diagnostics/ErrorOverlayEndToEndTests.cs`, not Integration: only the Unit project references `Avalonia.Headless`, and adding it to Integration would be a package change. (6) `MainWindow` binds the overlay `IsVisible` to `{Binding IsOpen, FallbackValue=False}` on the overlay's own DataContext (not `ErrorOverlay.IsOpen` through the shell) so a shell with no overlay hides it. (7) The short id is rendered lowercase to match the log and the copy text exactly. (8) `FirstFocusableToken` has a real test (host `ContentControl`, skips a disabled box) though no consumer exists yet (S5a/S7a). (9) The generic `StringsParityTests` is task 5a.1 and is NOT part of S4; S4 adds only the overlay-specific `ErrorOverlayResourcesTests`. Unverified: a human visual pass of the overlay (headless drawing is off, no frame was captured), the entrance fade (150 ms `Animation` on the `open` class), real OS clipboard copy, `Esc` when focus has been lost to a non-focusable area (window-level `EscapeCommand` arrives in S7a). Commit: see git log (slice S4/12)._

## 5a. EmptyState control, TerracottaText token, and Expenses (S5a)

Depends on 4. Goal: reusable `EmptyState`, the failing-contrast fix, the generic resx parity test, and the full
localization/empty-state treatment of Expenses, including `BeginNewEntry`. Uses `frontend-design` (DD-9 visual).

- [x] 5a.1 Write failing `tests/…Unit/Localization/StringsParityTests.cs` (generic, reflection + XML over the REAL
  resx files and `Strings.Designer.cs`): every key in `Strings.resx` exists in `Strings.es-DO.resx` and vice versa, no
  empty values in either, every key that is referenced from a view through `{x:Static resources:Strings.*}` has a
  Designer property, and the `{0}` placeholder count matches between neutral and es-DO. Extends (does not replace) the
  existing per-feature completeness tests. Known pre-existing gaps (six `ExpensesAi*` keys without accessors) go to an
  explicit allowlist with a comment, not silently. (R-19, S-38, Reconciliation design #6)
- [x] 5a.2 Write failing `tests/…Unit/UI/HardcodedStringsScanTests.cs` (REAL `.axaml` files parsed as XML): for
  `ExpensesView.axaml` (this slice; the other three views and `MainWindow` are added in 5b/6) no attribute among
  `Text`, `Content`, `Header`, `Watermark`, `PlaceholderText`, `ToolTip.Tip`, `AutomationProperties.Name` has a literal
  value containing a letter unless it starts with `{` (binding/resource). The scan helper takes a file list so later
  slices only append files. (R-19, S-38, DD-11 scan 3)
- [x] 5a.3 Write failing `tests/…Unit/UI/EmptyStateViewTests.cs` (headless, es-DO and en, real `ExpensesViewModel` over a
  temp SQLite DB and the real use cases): empty DB after the first load shows the "no data yet" `EmptyState` with
  localized title/hint and the create action showing the `Ctrl+N` text; data present hides it; DURING loading
  (`HasLoaded == false`) it is not visible; a filter that matches nothing (data exists) shows the "no results" state with
  a clear-filter action and NOT the no-data state; invoking clear-filter restores the list; the create action raises
  `BeginNewEntry`. Also `ExpensesViewModel.BeginNewEntry()` cancels an edit in progress and produces a new
  `DescriptionFocusRequest` token each call. (R-14, R-20, S-39, S-40, S-41, DD-9)
- [x] 5a.4 Add `TerracottaTextColor #F08A82` + brush to `Styles/Tokens.axaml` and switch `ledger-status.export-error`
  to it (appended; `Terracotta` stays). Create `src/…UI/Controls/EmptyState.axaml(.cs)` (styled properties `Title`,
  `Hint`, `ActionText`, `ActionCommand`, `ShortcutText`; no logic). Invoke `frontend-design` for the editorial block
  (three Hairline rules + one Amber tick, `AccessibilityView=Raw`). (R-18 prerequisite, R-20, DD-9, Reconciliation #15)
- [x] 5a.5 Add `ExpensesViewModel` members (additive): `HasLoaded`, `HasActiveFilter` (predicate pinned by 5a.3: applied
  filter differs from the VM default filter), `IsEmptyNoData`, `IsEmptyFiltered`, `BeginNewEntry()`,
  `BeginNewEntryCommand`, `ClearFilterCommand`, `DescriptionFocusRequest` (`FocusRequestToken`), raised on list refresh.
  Move every literal in `ExpensesView.axaml` to `{x:Static resources:Strings.*}` (`Expenses*`, `EmptyState*` prefixes),
  replace inline hex with tokens (`PaperDimBrush`, `SurfaceBrush`, `TerracottaTextBrush`, `SageBrush`), bind
  `FocusOnRequest.Token` on the Description `TextBox`, and host `EmptyState` (both states). Add all keys to both resx
  files + manual Designer accessors. Run 5a.1-5a.3 green. (R-14, R-19, R-20, DD-10)
- [x] 5a.6 Confirm existing Expenses/Import/Export tests still pass unmodified, and that no new view text bypasses resx
  (re-run the scan). **Run gate block G** (grep files: `EmptyState.axaml(.cs)`, `ExpensesView.axaml`,
  `ExpensesViewModel.cs`, `Tokens.axaml`, new tests).

_Apply notes S5a (Strict TDD, reds observed): public surface first added as inert skeletons (VM flags returning false, no-op `BeginNewEntry`, empty `EmptyState` panel, resx keys + Designer accessors as data), tests run: 14 failed / 9 passed of 23 (`ExpensesViewModelEmptyStateTests` 7 of 8 failed, `EmptyStateViewTests` 6 of 6 failed at that point, `HardcodedStringsScanTests.TheConvertedViews_HaveNoLiteralUserVisibleText` failed on the current Spanish literals); `TerracottaTextTokenTests` 2/2 red before the token existed. The 9 that passed were already true: the six `StringsParityTests` (the repo was already in es-DO/en parity, so they are guards, NOT red-first), the scanner control case, and `AFailedFirstLoad_NeverReportsLoaded` (true of the skeleton). Not red-first: the `TheExpensesForm_RendersInTheActiveLanguage` theory (written after the view conversion; the scan was the red for that behavior). Green after implementation: 9/9 `EmptyStateViewTests` cases, 8/8 VM tests, scan 2/2, tokens 2/2, parity 6/6. Gate G: build 0/0 (`--no-incremental`); full suite x3 = 2636 unit + 815 integration each run (baseline 2609 + 815; +27 unit), 0 skipped, no intermittents; format exit 0; Architecture filter 20/20; no-stub grep empty; footprint clean (no Packages.props, Migrations, snapshot, SettingKeys/AppSettings, csproj). Existing tests updated minimally because they asserted literal Spanish that moved to resx: `ImportViewXamlTests.ImportPanel_IsHostedOnce...` (ordering marker `"No hay gastos registrados."` -> `IsVisible="{Binding IsEmptyNoData}"`) and `ImportViewHeadlessTests.ExpensesView_WithARow...` (`"Editar"` -> `Strings.ExpensesEditButton`). Deviations: (1) 5a.3 uses the real `ExpensesViewModel` + real use cases over the existing fake repositories (`FakeExpenseRepository { PageAddedItems = true }`), not a temp SQLite DB: the Unit project has no Infrastructure/EF reference and adding one is a package change (same precedent as S4 deviation 2); the loading case uses a small `GatedExpenseRepository` decorator around the fake (collaborator only). (2) `HasActiveFilter` predicate pinned: applied `CurrentFilter` has CategoryId, DateFrom, DateTo or non-empty TagIds (PaymentMethodIds ignored because the list ignores it); `IsEmptyNoData`/`IsEmptyFiltered` also require `HasLoaded` (set only after a SUCCESSFUL refresh) and `Expenses.Count == 0 && TotalPages == 0`. (3) `BeginNewEntry()` cancels an edit in progress but leaves a half-typed NEW entry untouched. (4) The `Ctrl+N` chip is a literal `ShortcutText="Ctrl+N"` in the view (attribute outside the scan list; S7a may switch it to the catalog display string). (5) Size: 1216 added / 30 deleted lines (about 400 production incl. 209 resx/Designer, about 750 tests), over the ~450 guideline; S5a has no named sub-seam (S5a/S5b is the seam and is already applied), so it was not split further. (6) Inline hex replaced by tokens in `ExpensesView` (Background `#16181D`->InkBrush, `#242932`->SurfaceBrush (Surface is `#1F232B`, per DD-10), text `#E8E4D9`/`#A9AFB8`/`#E8A33D`/`#F08A82`/`#71C7A4` -> Paper/PaperDim/Amber/TerracottaText/Sage brushes). The `ExpensesAi*` six keys are on an explicit allowlist in `StringsParityTests` (plus a stale-entry guard). Unverified: a human visual pass of `EmptyState` (headless drawing is off, no frame captured) in both locales and the real `Ctrl+N` key (window shortcut arrives in S7a); the `✕` dismiss button still has no `AutomationProperties.Name` (S8 scan). Commit: see git log (slice S5a/12)._


## 5b. Income localization and empty states (S5b)

Depends on 5a. Goal: the same pattern for Income, using the same control and helpers.

- [x] 5b.1 Extend `HardcodedStringsScanTests` to include `IncomeView.axaml` (append to the file list; red first: the scan
  fails on the current literals). (R-19, S-38)
- [x] 5b.2 Write failing `tests/…Unit/UI/IncomeEmptyStateViewTests.cs` (headless, es-DO and en, real `IncomeViewModel` over
  a temp SQLite DB): no data yet (action shows `Ctrl+Shift+N`), filtered-none with clear-filter, data present, loading;
  `BeginNewEntry` cancels an edit and raises the token. (R-14, R-20, S-39, S-40, S-41)
- [x] 5b.3 Implement the `IncomeViewModel` members (same shape as 5a.5), convert `IncomeView.axaml` (resx
  `Income*`/`EmptyState*` keys both cultures + Designer accessors, tokens instead of inline hex, `FocusOnRequest` on
  Description, `EmptyState` hosted). Run 5b.1, 5b.2 green; the generic `StringsParityTests` stays green. (R-19, R-20)
- [x] 5b.4 **Run gate block G** (grep files: `IncomeView.axaml`, `IncomeViewModel.cs`, resx, new tests). Record the
  running total of localized literals converted (Expenses + Income) against the 62 in R-19.

_Apply notes S5b (Strict TDD, reds observed): (1) 5b.1 red first: adding `IncomeView.axaml` to `_scannedViews` failed with the 15 real Spanish literals listed by the scan. (2) Public surface added as inert skeletons (flags returning false, no-op `BeginNewEntry`, resx keys + Designer accessors as data, a `GetByFilterGate` on `FakeIncomeRepository`), then `IncomeViewModelEmptyStateTests` + `IncomeEmptyStateViewTests` + parity + scan run: 16 failed / 10 passed of 26 (the 10 passing were already true: 6 parity guards, scanner control case, and cases true of the skeleton such as `AFailedFirstLoad_NeverReportsLoaded` and the 2 "no empty state with data" cases; `TheIncomeForm_RendersInTheActiveLanguage` es-DO passed because the old view was already Spanish, only `en` was red). After implementation 26/26 green. Gate G: build 0/0 (`--no-incremental`); full suite x3 = 2654 unit + 815 integration each run (baseline 2636 + 815; +18 unit: IncomeViewModelEmptyStateTests 8, IncomeEmptyStateViewTests 10 cases), 0 skipped, no intermittents; format exit 0; Architecture filter 20/20; no-stub grep empty; footprint clean (no Packages.props, Migrations, snapshot, SettingKeys/AppSettings, csproj). Localized literals converted in IncomeView: 15 (14 visible attributes + the old empty message replaced by `EmptyState`); running total Expenses 15 + Income 15 = 30 of the 62 in R-19 (the Expenses figure is by symmetry of the S5a key set, not recounted). No existing test asserted Income literals, so no existing test was modified. Deviations: (1) 5b.2 uses the real `IncomeViewModel` + real use cases over the existing fake repositories (same precedent as S4/S5a: the Unit project has no EF reference); the loading case uses a new additive `FakeIncomeRepository.GetByFilterGate` (test double only). (2) Same predicate/`HasLoaded` semantics as Expenses. (3) `Ctrl+Shift+N` is a literal `ShortcutText` in the view (outside the scan list; S7a may switch to the catalog). (4) Extra `IncomeViewModelEmptyStateTests` (8) mirror the Expenses VM tests. (5) Inline hex in IncomeView replaced by tokens (`InkBrush`, `SurfaceBrush`, `PaperBrush`, `PaperDimBrush`, `SageBrush`, `TerracottaTextBrush`). Unverified: a human visual pass of the Income empty states in both locales (headless drawing is off), the real `Ctrl+Shift+N` key (arrives in S7a). Commit: see git log (slice S5b/12)._

## 6. Debts, Recurring, VM strings and sidebar labels (S6)

Depends on 5b. Goal: finish R-19/R-20: Debts and Recurring views, ~15 VM literals (Reconciliation #14), the three
sidebar labels. Empty states here are "no data yet" only (no filter).

- [x] 6.1 Extend `HardcodedStringsScanTests` to `DebtsView.axaml` and `RecurringRulesView.axaml` (red first). (R-19,
  S-38)
- [x] 6.2 Write failing `tests/…Unit/ViewModels/DebtsRecurringLocalizationTests.cs` against the REAL `DebtsViewModel` and
  `RecurringRulesViewModel` (existing use-case fakes only): every status and error message produced by a successful and a
  failed path resolves through `Strings` in BOTH cultures (assert the es-DO text differs from en and contains no
  leftover literal from the old code; scan the two VM source files with a regex for remaining Spanish sentences as a
  regression guard). (R-19, design Reconciliation #1)
- [x] 6.3 Write failing `tests/…Unit/UI/DebtsRecurringEmptyStateTests.cs` (headless, es-DO and en, real VMs over temp DB):
  empty Debts and empty Recurring render `EmptyState` with localized title and hint (no action, no filter state);
  data present hides it; loading hides it; the former hardcoded empty `TextBlock`s are gone. (R-20, S-39, S-41)
- [x] 6.4 Write failing `tests/…Unit/ViewModels/ShellSidebarLabelTests.cs`: the real `ShellViewModel.MenuItems` labels
  for "Recurrentes"/"Deudas"/"Calendario" (and all ten) come from resx and render differently in es-DO vs en; no label is
  a literal in `ShellViewModel.cs`. (R-15, R-19, S-32, S-38)
- [x] 6.5 Implement: convert the two views (resx `Debts*`/`Recurring*`/`EmptyState*`, tokens instead of inline hex,
  `EmptyState` hosted), move the ~15 VM strings to resx, move the three sidebar labels to resx (`Sidebar*`), all in
  both resx files + manual Designer accessors (append-only; keep `RouteMenuItem`/`MenuItems` shapes for existing tests).
  Invoke `frontend-design` for the two empty states. Run 6.1-6.4 green. (R-19, R-20, DD-10)
- [x] 6.6 Final tally in the apply notes: all 62 view literals + 3 labels + VM strings converted; the scan covers all four
  views; the generic parity test passes with all new keys. **Run gate block G** (grep files: both views, both VMs,
  `ShellViewModel.cs`, resx, new tests).

_Apply notes S6 (Strict TDD, reds observed): tests written first against the real view models/views/shell; resx keys + Designer accessors added as data and inert `HasLoaded`/`IsEmptyNoData` members (always false) so the tests compile, then run: 26 failed / 21 passed of 47 (Sidebar 5 of 6 failed; both VM source guards failed; the scan failed listing the Debts/Recurring literals and the new StringFormat scan failed on `{0} cuotas`; every `en` localization case failed while the `es-DO` ones passed because the old code was already Spanish; empty-state cases failed on the skeleton). After implementation: 62/62 of the focused filter (incl. DatePicker and RowTemplate suites) green. Gate G: build 0/0 (`--no-incremental`); full suite x3 = 2693 unit + 815 integration each run (baseline 2654 + 815; +39 unit: DebtsRecurringLocalizationTests 21 cases, DebtsRecurringEmptyStateTests 11, ShellSidebarLabelTests 6, scan +1), 0 skipped, no intermittents; format exit 0 (one using-order fix in the new test file); Architecture filter 20/20; no-stub grep empty; footprint clean (no Packages.props, Migrations, snapshot, SettingKeys/AppSettings, csproj); no stray dotnet/testhost process. Tally: view literals converted this slice 34 (Debts 17, Recurring 17; Expenses 15 + Income 15 before, so 64 in the four views vs the 62 pre-counted in R-19), 3 sidebar labels (`SidebarRecurring/Debts/Calendar`), 23 VM strings (Debts 9, Recurring 14; the Reconciliation #14 estimate was ~15), 62 new resx keys in both cultures; the scan now covers all four views. The DatePicker `DateOnlyConverter` bindings in both views are untouched (DatePickerBindingHeadlessTests green). No existing test asserted the moved Spanish literals, so none was modified. Deviations: (1) new test files live in `tests/…Unit/UI/` (the task text says `ViewModels/`, a folder that does not exist; all VM tests live in `UI/`). (2) 6.3 uses the real VMs + real use cases over the existing fake repositories (same precedent as S4/S5a/S5b: no EF in the Unit project); additive `GetAllGate` on `FakeDebtRepository`/`FakeRecurringRuleRepository` for the loading case. (3) Added a StringFormat scan (`{0} cuotas` hid a literal in a value starting with a brace, which the attribute scan skips) and moved the `StringFormat` texts to `x:Static` resx formats. (4) `HasLoaded`/`IsEmptyNoData` added to both VMs (no filter, so no `IsEmptyFiltered`); `HasLoaded` is set only after a SUCCESSFUL load. (5) Messages that arrive inside `Result.Error.Message` from Application use cases (validators) are not VM literals and remain out of scope. (6) Size: about 680 lines resx/Designer, about 150 lines of other production code, about 700 lines of tests. Unverified: a human visual pass of the two empty states in both locales (headless drawing is off, no frame captured); the Debts schedule-load failure and the Recurring preview-failure VM messages are covered by the source guard and the resx-difference test, not by a forced failure (no throwing fake for the payment repository/preview). Commit: see git log (slice S6/12)._

## 7a. Shortcut catalog, behavior, shell commands and gating (S7a)

Depends on 6. Goal: one catalog generating the real `KeyBindings`, shell commands with busy/overlay gating, and the
keyboard-only expense flow proven on the real window. Size note: if above ~450 lines split at "catalog + behavior" vs
"shell commands + gating + keyboard E2E".

- [x] 7a.1 Write failing `tests/…Unit/Input/KeyboardShortcutCatalogTests.cs` against the REAL `KeyboardShortcutCatalog`:
  `All` has exactly 15 Ctrl entries (NewExpense, NewIncome, GoToSection 1..9 and 0, OpenSettings `Ctrl+OemComma`,
  ToggleNotifications `Ctrl+Shift+B`, Escape); gestures are unique; `ForPlatform(false)` has no Meta entry and
  `ForPlatform(true)` adds exactly one Meta duplicate per entry; `Format` yields `Ctrl+Shift+N` on Windows/Linux and a
  symbol form on macOS with the "Shift" label from resx; every `DescriptionKey` resolves to a non-empty string in both
  cultures. (R-12, S-24, T10, DD-6)
- [x] 7a.2 Create `src/…UI/Input/{ShortcutAction, KeyboardShortcut, KeyboardShortcutCatalog, KeyboardShortcuts,
  IShortcutCommandSource, IBusyAware}.cs` and add the `Shortcut*` resx keys (both cultures + Designer). The
  `KeyboardShortcuts` attached behavior (enabled on the window from XAML: `KeyboardShortcuts.Enabled="True"`) GENERATES
  one `KeyBinding` per `ForPlatform(OperatingSystem.IsMacOS())` entry and resolves each command from
  `IShortcutCommandSource` (implemented by `ShellViewModel`). No code-behind logic. Run 7a.1 green. (R-12, DD-6)
- [x] 7a.3 Write failing `tests/…Unit/UI/ShortcutCatalogParityTests.cs` (REAL `MainWindow` headless, real
  `ShellViewModel`): `MainWindow.KeyBindings` gestures and bound commands equal the catalog for the current platform,
  in BOTH directions (no binding without an entry, no entry without a binding); the popup's local `Esc` binding maps to
  the same `EscapeCommand`. (R-12, R-16, S-24, D-26)
- [x] 7a.4 Write failing `tests/…Unit/ViewModels/ShellShortcutTests.cs` (real `ShellViewModel`, real `NavigationService`,
  real VMs via test DI with fake repositories): `NewExpenseCommand` from another section navigates to Expenses and calls
  `BeginNewEntry` through the route `configure` callback; from Expenses it calls `BeginNewEntry` on the SAME instance
  (no second VM); an edit in progress is cancelled; `NewIncomeCommand` likewise; `GoToSectionCommand(1)` and `(0)` open
  the first and tenth section in `NavigationSections` order (Ctrl+0 = 10th), sections beyond ten have no digit;
  `GoToSection` to the current section does not reload or push history; `OpenSettingsCommand` opens Settings;
  `ToggleNotificationsCommand` flips the bell popup; `EscapeCommand` closes the overlay first, else the popup. (R-12,
  S-25..S-28)
- [x] 7a.5 Write failing `tests/…Unit/ViewModels/ShortcutGatingTests.cs` (real VMs): with the error overlay open,
  `NewExpense`, `NewIncome`, `GoToSection`, `OpenSettings` do nothing and `Escape` closes the overlay; with
  `ExpensesViewModel.IsBusy` true (import in `Importing`) and, separately, `SettingsViewModel.IsBusy` true (restore in
  progress) the same four are no-ops and the running operation is uninterrupted (state unchanged, no cancel called); the
  overlay-over-restore case: `Escape` still closes the overlay (Reconciliation #6); with no overlay and a restore busy,
  `Escape` does not close the popup; bell toggle stays allowed. The gate lives in the command bodies, not `CanExecute`
  (Phase 18/19 lesson). (R-13, S-29, S-30, T9)
- [x] 7a.6 Implement: `ShellViewModel` commands (`NewExpenseCommand`, `NewIncomeCommand`, `GoToSectionCommand(int)`,
  `OpenSettingsCommand`, `ToggleNotificationsCommand`, `EscapeCommand`, `IShortcutCommandSource.Resolve`),
  `ExpensesViewModel.IsBusy => Import?.IsBusy == true` and `SettingsViewModel.IsBusy => Backup.IsRestoring` as
  `IBusyAware` (read existing flags; do NOT add new state; verify the real flag names with `rg` first and adapt),
  ordered section source `src/…UI/Navigation/NavigationSections.cs` stub-free (types + routes + label keys; the sidebar
  view comes in 7b), `MainWindow.axaml` (`KeyboardShortcuts.Enabled`, popup `Esc` binding). Run 7a.3-7a.5 green.
  (R-12, R-13, DD-6, DD-7)
- [x] 7a.7 Write `tests/…Integration/Keyboard/KeyboardOnlyExpenseFlowTests.cs` (REAL `MainWindow`, REAL `ShellViewModel`,
  REAL `ExpensesViewModel` and use cases, temp-file SQLite, headless key events only; es-DO and en): start on another
  section, send `Ctrl+N`, assert Description has focus, send text via `KeyTextInput` and `Tab` through Amount, Date,
  Category (Tab order follows visual order; add `TabIndex` only where the tree order differs), send `Enter`; assert a new
  expense row is persisted in the DB and visible in the list, and focus is back on Description. No mouse and no direct
  command call in this test. (R-14, S-25, S-31)
- [x] 7a.8 **Run gate block G** (grep files: `UI/Input/*`, `NavigationSections.cs`, `ShellViewModel.cs`,
  `ExpensesViewModel.cs`, `SettingsViewModel.cs`, `MainWindow.axaml`, new tests). Note that the Meta-key behavior on a real
  Mac is authored, unverified (recorded in Cierre).

_Apply notes S7a (Strict TDD). Split at the design seam into two commits: part 1 "catalog + behavior" and part 2 "shell commands + gating + keyboard E2E" (the slice is about 2,000 lines incl. tests and resx, far above the ~450 budget; part 1 was gated on its own: build 0/0, full suite 2709 unit + 815 integration, format 0, Architecture 20/20). Reds observed: catalog/parity/shell/gating tests ran against inert skeletons (empty catalog, no-op commands): 32 failed / 12 passed of 44; the passes were vacuous ones, so the tests were hardened (counts asserted, positive controls in every gating test) before the green step. The keyboard E2E ran red on exactly one assertion (focus back on Description after register: 2/2 cultures) and went green after `RegisterExpenseAsync` raises `DescriptionFocusRequest` on success (new production behavior, R-14). The behavior tests (`KeyboardShortcutsBehaviorTests`) were written after the behavior; a mutation (skip `KeyBindings.Add`) turned all 6 red, then it was restored. Gate G (part 2 tree): build 0/0 (`--no-incremental`); full suite x3 = 2746 unit + 815 integration each run (baseline 2693 + 815; +53 unit: catalog 10, behavior 6, parity 4, shell commands 23, gating 8, keyboard E2E 2), 0 skipped, no intermittents; format exit 0 (var/pattern fixes applied by `dotnet format style`); Architecture filter 20/20; no-stub grep empty; footprint clean (no Packages.props, Migrations, snapshot, SettingKeys/AppSettings, csproj); the real `%APPDATA%\ControlDeFinanzasIA` listing unchanged; build servers shut down, no stray dotnet/testhost.
Deviations: (1) `Escape` has no Meta twin on macOS (Meta+Esc is not a documented gesture), so `ForPlatform(true)` = 15 + 14 = 29, not 30. (2) `SettingsViewModel.IsBusy` reads `Backup.State == Restoring` (there is no `IsRestoring`; `BackupSettingsViewModel.IsBusy` also covers export/inspect, which shortcuts may interrupt safely). (3) `Escape` with no overlay is also suppressed during an import commit (IBusyAware is one flag), stricter than Reconciliation #6, harmless. (4) While the overlay is open `Ctrl+Shift+B` is suppressed too (the overlay closes the popup on open; "only Escape acts"). (5) The keyboard E2E lives in `tests/…Unit/Keyboard/`, not Integration: the single shared headless session is in the Unit project and the Integration project has no Avalonia.Headless reference (a csproj change is out of scope). It still uses the REAL composition root (`Bootstrap`), the real `NavigationService`/`ShellViewModel`/`ExpensesViewModel`/use cases and a migrated temp-file SQLite database (path resolver and `AppDbContext` registration swapped to the temp dir). (6) `IsDefault` belongs to S7b, so the E2E reaches the register button with `Tab` and presses `Enter` on it; Enter-inside-an-input submit is S7b (7b.4). Tab order needed no `TabIndex`. (7) The popup-local `Esc` uses the same behavior (`EscapeOnly`, explicit `Source`) instead of a hand-written `KeyBinding`. (8) `ShellViewModel.MenuItems` still lists its ten labels by hand: an existing source guard (`ShellSidebarLabelTests`) forbids `_localizer[variable]`; S7b moves the sidebar onto `NavigationSections`. (9) Added `ShortcutTexts` (x:Static) so the Expenses/Income empty-state chips come from `Format`; `ShortcutKeyShift` is "Shift" in both cultures (the chip tests assert `Ctrl+Shift+N`). Unverified: Meta bindings on a real Mac; the `Format` symbol form has only a pure test; the popup-local `Esc` is asserted structurally (a real key event inside the popup `TopLevel` is not simulated headlessly); a human visual/keyboard pass was not run. Commits: see git log (slice S7a/12, parts 1 and 2)._

## 7b. Sidebar rework, focus styles, IsDefault and Settings shortcut list (S7b)

Depends on 7a. Goal: one interactive element per sidebar item, a visible current section, `:focus-visible` styles,
Enter submit via `IsDefault`, and the Settings shortcut block fed by the catalog. Uses `frontend-design` (DD-7/DD-8).

- [ ] 7b.1 Write failing `tests/…Unit/Navigation/NavigationSectionsTests.cs`: every `NavigationSection.ViewModelType`
  equals the type `NavigationService` maps for its route (covers the real `Incomes` and `Recurring` routes that the old
  name-based sync missed); ten sections in the catalog order; `IsCurrent` follows `CurrentViewModel.GetType()` for all
  ten routes including Income and Recurring. (R-15, S-32, T11, design Reconciliation #3)
- [ ] 7b.2 Write failing `tests/…Unit/UI/SidebarHeadlessTests.cs` (real `MainWindow`): no `ListBoxItem` contains a `Button`
  (there is no `ListBox` left or no nested button); each item is a single tab stop; arrow keys and Tab reach each item;
  Enter/Space activate navigation; the current item has the `current` class and `AutomationProperties.Name` (label) and
  `ItemStatus` = localized "current section"; the digit hint shows `1`..`0`; the three formerly hardcoded labels render
  in es-DO and en. (R-15, S-32)
- [ ] 7b.3 Write failing `tests/…Unit/UI/FocusStyleTests.cs` (headless): after keyboard `Tab` focus on a
  `ledger-btn`, a sidebar item and a list row, the shared focus style is applied (assert the style/pseudo-class or the
  resolved border brush equals `Amber`); first PIN whether Avalonia 11.2.7 supports `:focus-visible` in a micro-test; if
  not, use `:focus` and record the fallback in the apply notes. Mouse-click focus must not show the ring when
  `:focus-visible` is supported. (R-17, S-35, design Open Question 1)
- [ ] 7b.4 Write failing `tests/…Unit/UI/DefaultButtonTests.cs` (headless, real VMs): in Expenses, Income, Debts and
  Recurring the primary button is `IsDefault` in register mode, the update button in edit mode (never both), and Enter
  inside an input submits; the import Confirm button is NEVER default. Extend with the overlay-open case from 4.5.
  (R-12, R-14, DD-8, T8)
- [ ] 7b.5 Write failing `tests/…Unit/UI/SettingsShortcutListTests.cs` (real `SettingsViewModel`/view): every catalog entry
  is listed with its localized action name and gesture text (es-DO and en), the list is read-only, and there is one
  "Enter submits the form" line; tooltips and `AcceleratorKey` on new-expense, new-income, bell and settings entry controls
  show `KeyboardShortcutCatalog.Format` output (never typed by hand). (R-16, S-33, DD-6)
- [ ] 7b.6 Implement: `SidebarItemViewModel`, sidebar `ItemsControl` of flat `Button.sidebar-item`s with `current` class,
  `ItemStatus`, `PositionInSet`/`SizeOfSet` (keep `MenuItems`/`RouteMenuItem`/`NavigateToCommand` for existing tests),
  `LedgerStyles.axaml` focus-visible + sidebar styles (200 px rail, 3 px amber tick, `#14E8A33D` wash, mono digit hint;
  tokens only), `IsDefault` bindings in the four views, `SettingsViewModel.Shortcuts` + `SettingsView.axaml` block
  (localized heading `Shortcuts*` keys both cultures + Designer), tooltips from `Format`. Invoke `frontend-design`. Run
  7b.1-7b.5 green. (R-15, R-16, R-17, DD-7, DD-8)
- [ ] 7b.7 **Run gate block G** (grep files: `SidebarItemViewModel.cs`, `MainWindow.axaml`, `LedgerStyles.axaml`, the four
  views, `SettingsView.axaml`, `SettingsViewModel.cs`, new tests). Re-run `ShortcutCatalogParityTests` and the keyboard
  E2E unchanged.

## 8. Accessibility pass and statement (S8)

Depends on 7b. Goal: names on every textless interactive control, contrast proven on the real palette, live regions set,
honest accessibility statement.

- [ ] 8.1 Write failing `tests/…Unit/Accessibility/ContrastTests.cs` (REAL `Tokens.axaml` parsed; WCAG relative luminance):
  `Paper`, `PaperDim`, `Amber`, `Sage`, `TerracottaText` on `Ink` and on `Surface` >= 4.5:1; the button label on its fill
  (`ledger-btn-primary` label/fill pair resolved from `LedgerStyles.axaml`) >= 4.5:1; focus ring `Amber` on both backgrounds
  >= 3:1; error and warning text pairs >= 4.5:1; large-amount text >= 3:1. The test enumerates the pairs the app actually
  uses and MUST NOT exclude a failing pair; a failure is fixed in `Tokens.axaml` (test-first). The current
  `Terracotta`-on-`Surface` 4.43:1 is asserted as "only used for fills/large text" by a scan that no text `Foreground`
  binds to `TerracottaBrush`. (R-18, S-37, T12)
- [ ] 8.2 Write failing `tests/…Unit/Accessibility/AccessibilityXamlScanTests.cs` (static, all `Views/**/*.axaml` and
  `Controls/*.axaml` parsed as XML): every `Button`/`ToggleButton`/`RepeatButton` whose content is not text (no text
  `Content`, no `x:Static`, no `TextBlock` child, or glyph-only such as `✕`) sets `AutomationProperties.Name`; every
  `TextBox`/`ComboBox`/`DatePicker`/`CheckBox` without text content sets `Name` or `LabeledBy` (placeholder text is not
  accepted). Extends `HardcodedStringsScanTests` file list to ALL views and `MainWindow`; pre-existing hits elsewhere go to
  an explicit allowlist with a comment (visible debt). (R-17, R-19, DD-11 scans 1 and 3)
- [ ] 8.3 Write failing `tests/…Unit/Accessibility/AutomationNameScanTests.cs` (runtime, headless, es-DO): real
  `MainWindow` with real shell VM plus each main section (Dashboard, Expenses, Income, Budgets, Goals, Debts, Recurring,
  Calendar, Chat, Settings) through `NavigationService`; walk the visual tree and assert that every focusable, enabled,
  visible control that has no visible text has a non-empty `ControlAutomationPeer.CreatePeerForElement(c).GetName()`;
  assert the bell, sidebar items, pickers and toggles are included in the walk (count > 0 per category). Catches names lost
  through templates. (R-17, S-34, DD-11 scan 2)
- [ ] 8.4 Write failing `tests/…Unit/Accessibility/LiveRegionSupportTests.cs`: `AutomationProperties.LiveSetting` is set
  to `Polite` on the status/error `TextBlock`s of the error overlay, the four views' status blocks, the import result and
  the AI fallback captions (assert by x:Name/class on the real visual tree), AND `docs/accessibility.md` contains the
  sentence that the property is set but peer event delivery is unverified until a manual Narrator run. (R-17, S-36,
  Reconciliation #8)
- [ ] 8.5 Implement: add missing `AutomationProperties.Name` (localized resx, both cultures + Designer; accessor names
  prefixed `A11y*`), `LiveSetting`, `TabIndex` only where visual order differs, tokens fixing any failing contrast pair, and
  create `docs/accessibility.md` (Windows UIA is the only platform claimed and tested; VoiceOver and Orca/AT-SPI best effort,
  untested; no high-contrast theme; no full WCAG audit; live-region limitation; manual Narrator check not run). Invoke
  `frontend-design` for any token change. Run 8.1-8.4 green. (R-17, R-18, DD-11)
- [ ] 8.6 Write `tests/…Unit/Accessibility/TabOrderTests.cs` (headless): on the primary form of Expenses, Income, Debts and
  Recurring, the first tab stop is the Description (or the first field) after navigation, and Tab order equals visual
  order (assert by control positions). Initial focus after navigation via `FocusOnRequest.FirstFocusableToken` lands in
  the new screen. (R-14, R-17)
- [ ] 8.7 **Run gate block G** (grep files: touched views, `Tokens.axaml`, `docs/accessibility.md`, new tests).

## 9. Packaging scripts and draft release workflow (S9)

Depends on 8. Goal: self-contained publish per RID, archives with correct Unix modes, checksums, `.app` and AppImage
recipes, and a draft-only workflow. Only win-x64 is RUN; everything else is layout-checked or YAML-linted and labelled
"authored, unverified". Size note: scripts are dense; if above ~450 lines split at "publish + archives" vs "app bundle,
AppImage, workflow".

- [ ] 9.1 Preflight (record in the apply notes): `pwsh --version` is 7.2 or newer on this machine; `dotnet --list-sdks`
  shows the .NET 8 SDK; the runtime packs for the four RIDs can be restored (online). If `pwsh` 7.2+ is missing, write the
  scripts anyway, mark every script-run task UNVERIFIED and do not tick it. (Reconciliation #12)
- [ ] 9.2 Write failing `tests/…Unit/Packaging/PublishPropsTests.cs` (parses the REAL `build/Publish.props` and
  `UI.csproj`): properties `SelfContained=true`, `PublishSingleFile=false`, `PublishTrimmed=false`,
  `PublishReadyToRun=false`, `DebugType=embedded`, `SatelliteResourceLanguages=en;es;es-DO`, `InvariantGlobalization=false`
  exist and are ALL conditioned on `'$(RuntimeIdentifier)' != ''`; the UI project imports the file; `.gitignore` contains
  `artifacts/`; `AppRestartService` source still uses `Environment.ProcessPath` (guards the single-file ban). (R-21,
  R-25, DD-12)
- [ ] 9.3 Create `build/Publish.props`, edit `UI.csproj` (import) and `.gitignore`. Run 9.2 green. Then create
  `scripts/publish.ps1` (`#Requires -Version 7.2`; `-Rid win-x64|osx-arm64|osx-x64|linux-x64|all`; reads the version from
  `Directory.Build.props`, never typed; `dotnet publish src/ControlDeFinanzasIA.UI -c Release -r <rid>` to
  `artifacts/publish/<rid>/`; `scripts/lib/Archive.psm1` writes zip entries with `ExternalAttributes = 0x81ED << 16` and tar
  via `System.Formats.Tar.TarWriter` with mode 755 for `ControlDeFinanzasIA.UI` and `createdump`; archives
  `ControlDeFinanzasIA-1.0.0-<rid>.zip` (win) / `.tar.gz` (osx, linux) plus the osx `.app.zip` from 9.7; `SHA256SUMS.txt` in
  `sha256sum` format). (R-21, R-22, DD-12, Reconciliation #10)
- [ ] 9.4 **RUN** `scripts/publish.ps1 -Rid all` (needs network for runtime packs). Record the publish log, exact native
  library file names per RID from the first real output, and .NET 8's minimum macOS version used for
  `LSMinimumSystemVersion`. Write `scripts/verify-layout.ps1` (per RID: executable, SkiaSharp, HarfBuzzSharp, `e_sqlite3`,
  QuestPDF native present with the RID's prefix/extension; names taken from 9.4, not guessed) and
  `tests/…Integration/Packaging/PublishLayoutTests.cs` that runs it (skips with an explicit "artifacts missing" failure
  message, NOT a silent pass, when `artifacts/publish/<rid>` is absent; the test is tagged so CI can opt out). (R-21, S-43)
- [ ] 9.5 Write `scripts/smoke-win.ps1` (extracts the win zip to a temp dir; starts the exe with
  `CONTROLDEFINANZASIA_DATA_DIR`=temp, `DOTNET_ROOT` removed and dotnet dirs stripped from `PATH`; waits <= 30 s for
  `data.db` and a log line `Migrations applied` in `logs/`; `CloseMainWindow()`; asserts exit code 0 within 10 s and no
  `startup-error-*.log`) and **RUN** it. If `Migrations applied` is not logged by production code today, add that single
  `Information` log line test-first (assert in `BootstrapLoggingTests`) instead of weakening the script. Record the
  run output and the `Environment.ProcessPath` value observed. (R-21, S-42)
- [ ] 9.6 Write failing `tests/…Integration/Packaging/ChecksumVerificationTests.cs`: against the REAL archives in
  `artifacts/`: names match `ControlDeFinanzasIA-1.0.0-<rid>.{zip|tar.gz}` for the four RIDs, the version in every name
  equals `AppVersionInfo.Current`, every `SHA256SUMS.txt` line verifies by recomputing SHA-256, and an archive round-trip
  re-read shows exec mode on `ControlDeFinanzasIA.UI` for tar entries and zip external attributes (T13). Run green after
  9.4. (R-22, S-44, T13)
- [ ] 9.7 Write failing `tests/…Unit/Packaging/AppBundleStructureTests.cs` (runs `scripts/package-macos-app.ps1` on the
  osx publish output into a temp dir, folder + plist parse only): `ControlDeFinanzasIA.app/Contents/MacOS/ControlDeFinanzasIA.UI`,
  `Contents/Resources/AppIcon.icns`, `Contents/Info.plist` parses as XML plist with keys `CFBundleExecutable`,
  `CFBundleIdentifier` (`com.diego.controldefinanzasia`), `CFBundleShortVersionString`/`CFBundleVersion` = `1.0.0` (from the
  props), `CFBundleIconFile`, `NSHighResolutionCapable`, `LSMinimumSystemVersion` (value pinned in 9.4). Then create
  `scripts/package-macos-app.ps1` and `packaging/macos/Info.plist.template`; run green. DMG creation is CI-only and NOT
  run. (R-23, S-45)
- [ ] 9.8 Write failing `tests/…Unit/Packaging/AppImageRecipeTests.cs`: `packaging/linux/AppRun`,
  `controldefinanzasia.desktop` (valid keys `Type`, `Name`, `Exec`, `Icon`, `Categories`), the icon reference, and
  `build-appimage.sh` exist; every file the script references exists in the repo or is explicitly produced by an earlier
  line of the script. Then author the files. No AppImage is built here. (R-23, S-46)
- [ ] 9.9 Write failing `tests/…Unit/Packaging/ReleaseWorkflowTests.cs` (YAML parse with the YAML library already
  available in the test project; if none exists, a minimal structural parser is forbidden: add the check as a
  `scripts/lint-workflow.ps1` using `ConvertFrom-Yaml` from PowerShell 7 instead and call it from the test; NO new NuGet
  package): `.github/workflows/release.yml` parses; `on.push.tags` is exactly `['v*']` and there is no other trigger; a
  first job fails unless the tag equals `v` + the props version; the release step runs `gh release create --draft` (no
  third-party release action); only the last job has `contents: write`, all others `contents: read`; `build.yml` is byte-for-
  byte unchanged (`git diff --quiet` against HEAD for that file). Then author `release.yml` (windows-latest: win-x64 + smoke;
  macos-14: both osx RIDs, `.app`, DMG via `hdiutil`; ubuntu-latest: linux-x64, AppImage). (R-24, S-47, T16)
- [ ] 9.10 **Run gate block G** (grep files: `build/Publish.props`, `scripts/**`, `packaging/**`, `release.yml`, new tests).
  Add to the apply notes the verified/unverified table for this slice: win-x64 publish + smoke RUN; other RIDs layout
  only; `.app` structure only; AppImage, DMG, workflow execution, MSIX: authored, unverified / deferred.

## 10. Docs close-out (S10)

Depends on 9. Goal: docs describe what exists. No production code.

- [ ] 10.1 Write failing `tests/…Unit/Packaging/DocsReconciledTests.cs` (reads the repo docs): `docs/architecture.md` §14
  describes the real rolling file logger (path, 5 MB x 5, UTC, never throws) and states nothing about a Serilog sink;
  §16 describes the real mechanism (`UnexpectedErrorReporter`, `GlobalExceptionHooks`, overlay, startup guard) and no longer
  mentions `IExceptionHandler (CommunityToolkit)` (design Reconciliation #14); §18 describes the publish/packaging layout;
  `docs/roadmap.md` records the installer deviation (MSIX deferred, DMG CI-only, no signing) and Phase 21 done;
  `AGENTS.md` status block mentions Phase 21; `README.md` shortcut summary lists every `KeyboardShortcutCatalog.All` gesture.
  (R-26)
- [ ] 10.2 Edit `docs/architecture.md` (§14, §16, §18, ADR list), `docs/roadmap.md`, `spec/phases/phase-21/features.md`
  (reconcile), `CHANGELOG.md` (move any Phase 21 notes under `[1.0.0]`, keep `[Unreleased]`), `docs/AI_HANDOFF.md` if present,
  and append to the `AGENTS.md` Status block (append-only; counts from 10.3; verify history; pointers to
  `spec/features/phase-21/`, `tasks/phase-21/`, `.atl/sdd/f-021p21-packaging/`; list what was NOT run by hand). Run 10.1
  green. (R-26)
- [ ] 10.3 Footprint proof (S-48): `git diff --stat main..HEAD` for the phase shows no migration, no `ModelSnapshot`, no
  `Directory.Packages.props` change; `ArchitectureTests` logger-location and `System.Net.Http` cases pass. Record the diff
  stat in the apply notes. (R-25, S-48)
- [ ] 10.4 **Run gate block G** over the whole phase (not just the slice): final counts, 3 consecutive full runs, every
  intermittent test name or "none", `--filter Architecture`, and `dotnet format style --verify-no-changes` exit 0.

## 11. Cierre

- [ ] 11.1 Run the full gate suite across the whole phase on the final HEAD: `dotnet build -c Release` (0/0), `dotnet test
  -c Release --no-build` (record unit + integration counts; confirm every Phase 21 test class executed and none skipped),
  `dotnet format style --verify-no-changes` exit 0, NetArchTest R-1..R-5 plus the Phase 21 guards, resx parity, the
  no-stubs grep over all touched files. Record final counts, the 3-run stability result and every intermittent test
  name (or "none"). Note the honest split: which Acceptance Criteria are [L] (verified here) vs [U] (below). (R-25, R-26)
- [ ] 11.2 **NOT RUN BY HAND.** Real Windows Narrator pass: launch the published win-x64 app (throwaway
  `CONTROLDEFINANZASIA_DATA_DIR`), navigate the sidebar, register an expense by keyboard, trigger the error overlay path if
  reachable, and confirm the live-region announcements. Record what Narrator actually said. Until run, `docs/accessibility.md`
  keeps the "peer event delivery unverified" sentence. (R-17, S-36, AC [U])
- [ ] 11.3 **NOT RUN BY HAND.** Install-from-zip on a CLEAN Windows machine or VM (no .NET SDK or runtime installed): extract
  `ControlDeFinanzasIA-1.0.0-win-x64.zip`, verify the checksum against `SHA256SUMS.txt`, apply the SmartScreen workaround
  from the README, launch, create data, restart through the app's own restart path. (R-21, R-22, S-42, AC [U])
- [ ] 11.4 **NOT RUN BY HAND.** macOS runs: launch `osx-arm64` and `osx-x64` builds and the `.app` on real Macs (native
  libraries, SkiaSharp, QuestPDF, SQLite), apply the Gatekeeper workaround, check Meta shortcuts, build the DMG in CI, and
  route a real crash through the overlay. README keeps "provided, untested" until done. (R-21, R-23, AC [U])
- [ ] 11.5 **NOT RUN BY HAND.** Linux run: launch `linux-x64` on a real host (ICU present), build and run the AppImage in CI,
  route a real crash through the overlay. (R-21, R-23, AC [U])
- [ ] 11.6 **NOT RUN BY HAND.** Release workflow execution: no git remote exists, so the workflow was YAML-linted only. First
  real run happens when the owner pushes a `v1.0.0` tag; confirm a DRAFT release with archives and `SHA256SUMS.txt`, never
  published automatically. The annotated tag `v1.0.0` is created only on the owner's request (no AI trailer). (R-1, R-24,
  AC [U])
- [ ] 11.7 **NOT RUN BY HAND.** es-DO and en visual pass on a throwaway data copy: icon (taskbar, title bar), error overlay
  (scrim, REF line, `+N more`), empty states in Expenses/Income/Debts/Recurring, sidebar current-item style and digit hints,
  focus rings on buttons/sidebar/rows/inputs, Settings shortcut list, contrast legibility. Record screenshots. (R-2, R-10,
  R-15, R-17, R-20, AC [U])
- [ ] 11.8 Run `sdd-verify`; resolve or explicitly record every critical/warning finding with a test-first fix against the real
  component (Phase 19/20 precedent). Do not tick while warnings are open without a note. Flag Reconciliation #10 (osx archive
  format) for the owner's confirmation.
- [ ] 11.9 Archive: rename `spec/features/phase-21/F-021-packaging-polish-a11y.spec.md` to `F-021-packaging-polish-a11y.done.md`
  ONLY after archive with `git mv`, tick the Acceptance Criteria of the spec with evidence, leave every [U] item unticked,
  append a "Post-apply reconciliation" restating the Reconciliation table above, and update `AGENTS.md`/memory pointers.
  Conventional commits, no AI trailers.

---

## Threat-table traceability (design T1..T18 → named test tasks)

| Row | Threat | Test task(s) |
|---|---|---|
| T1 | Key/user data in logs | 2.8 `LogPrivacyTests` |
| T2 | Log fills disk | 2.2 `RollingFileSinkTests` |
| T3 | Logger throws or loops | 2.4 `RollingFileLoggerFailureTests` |
| T4 | Global handler hides corrupt state | 3.1, 3.4 (breaker, `Handled` only after report), 3.6 `RestoreUnhandledFailureTests` |
| T5 | Dialog storm | 4.1, 4.5 |
| T6 | Copy leaks data | 4.1 |
| T7 | Crash before UI leaves no trace | 3.7, 3.8 |
| T8 | Enter fires a default button behind the overlay | 4.5, 7b.4 |
| T9 | Shortcut interrupts import/restore | 7a.5 |
| T10 | Meta duplicates hijack OS keys | 7a.1 |
| T11 | Sidebar wrong/no current section | 7b.1 |
| T12 | Terracotta text unreadable | 8.1 |
| T13 | Exec bits lost in archives | 9.6 |
| T14 | Linux without ICU | 1.5 (README), Cierre 11.5 (unverified) |
| T15 | Unsigned binaries blocked | 1.5 (README text), Cierre 11.3/11.4 (unverified) |
| T16 | Accidental public release | 9.9 |
| T17 | Hooks leak into other headless tests | 3.4, 3.5 |
| T18 | Two instances share a log file | Accepted (`FileShare.ReadWrite`), no test; recorded in `docs/architecture.md` §14 by 10.2 |

## Scenario coverage index (spec S-1..S-48)

| Scenario | Task(s) | Scenario | Task(s) |
|---|---|---|---|
| S-1 | 1.1 | S-25 | 7a.4, 7a.7, 5a.3 |
| S-2 | 1.5, 1.6 | S-26 | 7a.4, 5b.2 |
| S-3 | 1.3 | S-27 | 7a.4 |
| S-4 | 2.5, 2.7 | S-28 | 7a.4 |
| S-5 | 2.5 | S-29 | 7a.5 |
| S-6 | 2.2 | S-30 | 7a.5 |
| S-7 | 2.2 | S-31 | 7a.7 |
| S-8 | 2.2 | S-32 | 6.4, 7b.1, 7b.2 |
| S-9 | 2.3 | S-33 | 7b.5 |
| S-10 | 2.4 | S-34 | 8.3 |
| S-11 | 2.1 | S-35 | 7b.3 |
| S-12 | 2.8 | S-36 | 8.4 |
| S-13 | 3.1, 4.6 | S-37 | 8.1 |
| S-14 | 3.2 | S-38 | 5a.1, 5a.2, 5b.1, 6.1 |
| S-15 | 3.4 | S-39 | 5a.3, 5b.2, 6.3 |
| S-16 | 3.4 | S-40 | 5a.3, 5b.2 |
| S-17 | 3.1, 3.4 | S-41 | 5a.3, 5b.2, 6.3 |
| S-18 | 3.7, 3.8 | S-42 | 9.5 (RUN), 11.3 (unverified) |
| S-19 | 4.5, 4.6 | S-43 | 9.4 |
| S-20 | 4.1 | S-44 | 9.6 |
| S-21 | 4.1, 4.5 | S-45 | 9.7 |
| S-22 | 4.1, 4.5 | S-46 | 9.8 |
| S-23 | 3.6 | S-47 | 9.9 |
| S-24 | 7a.1, 7a.3 | S-48 | 2.9, 3.9, 10.3 |

## Requirement coverage index (spec R-1..R-26)

R-1: 1.1, 1.2, 1.5, 1.6 · R-2: 1.3, 1.4 · R-3: 2.3, 2.5, 2.6, 2.7 · R-4: 2.2 · R-5: 2.1, 2.5, 2.8 · R-6: 2.4 ·
R-7: 3.1, 3.2, 3.3 · R-8: 3.4, 3.5, 3.6 · R-9: 3.7, 3.8 · R-10: 4.1, 4.4, 4.5 · R-11: 4.1, 4.5, 3.6 ·
R-12: 4.3, 7a.1-7a.7 · R-13: 7a.5 · R-14: 5a.3, 7a.7, 8.6 · R-15: 6.4, 7b.1, 7b.2 · R-16: 7a.3, 7b.5 ·
R-17: 7b.3, 8.2-8.6 · R-18: 8.1 · R-19: 5a.1, 5a.2, 5b.1, 6.1, 6.2 · R-20: 5a.3, 5b.2, 6.3 · R-21: 9.2-9.5 ·
R-22: 9.3, 9.6 · R-23: 9.7, 9.8 · R-24: 9.9 · R-25: 2.9, 3.9, 9.2, 10.3 + gate item 6 of every slice ·
R-26: every test task (temp dirs, `IClock`) + 10.1, 10.2.
