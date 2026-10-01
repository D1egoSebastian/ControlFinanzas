# Phase 21 — Packaging, Polish, Accessibility

> **Phase:** 21 — Packaging, polish, accessibility (release 1.0.0)
> **Status:** Archived (2026-09-30). Implemented in S1-S10, verified (PASS WITH WARNINGS), post-verify fixes applied; the owner's manual checks, the `v1.0.0` tag and the first release run remain.
> **Spec ID:** F-021 — `spec/features/phase-21/F-021-packaging-polish-a11y.done.md`
> **Dependencies:** Phases 0-20 archived.
> **Blocks:** nothing (last roadmap phase; the roadmap has no phase after 21).
> **Roadmap:** `docs/roadmap.md` §Fase 21
> **Stack:** .NET 8 · Avalonia 11.2.7 (MVVM strict) · existing packages only · no schema change, no migration, no new package.

## 1. Summary

Phase 21 makes `1.0.0` distributable and diagnosable. It adds a self-contained publish per RID (`win-x64`, `osx-arm64`, `osx-x64`, `linux-x64`) with archives and SHA-256 checksums, a macOS `.app` script, an AppImage recipe and a draft-only release workflow; an app icon, CHANGELOG, LICENSE and a rewritten README; an own rolling file logger (`<DataDir>/logs`, 5 MB x 5) and an unexpected-error path (global hooks, startup-failure log, in-window overlay with an 8-hex correlation id and a Copy button that leaks no stack or user data); keyboard shortcuts from a single catalog (Ctrl+N and friends), keyboard navigation and a reworked sidebar; basic accessibility (automation names, focus visuals, contrast test, accessibility statement); and UI polish (localization of 62 literals and 3 sidebar labels, a reusable `EmptyState` with "no data yet" vs "no results" states in Expenses, Income, Debts and Recurring).

The governing rule is honesty: only Windows is run and verified here. macOS/Linux builds, `.app`/DMG/AppImage launches, the release workflow and non-Windows screen readers are authored and explicitly listed as unverified.

## 2. Slice plan

Each slice is at most about 400 changed lines and leaves build, tests and format green.

| ID | Slice | Status |
| --- | --- | --- |
| S1 | Identity and docs: `<Version>`, icon set, README rewrite, CHANGELOG, LICENSE, `VersionTests`, `RepoDocsTests`, `AppIconTests` (R-1, R-2) | Done (`f10816a`) |
| S2 | `RollingFileLoggerProvider` in Infrastructure, `AddFileLogging`, Bootstrap wiring, rolling/failure/privacy tests (R-3..R-6) | Done (`dae9bb1`) |
| S3 | `IUnexpectedErrorReporter`, global hooks, startup-failure path, child-process test (R-7..R-9) | Done (`76e2c97`) |
| S4 | `ErrorOverlayViewModel`/`View`, overlay host in `MainWindow`, Copy via `IClipboardService`, end-to-end tests (R-10, R-11) | Done (`085256b`) |
| S5 | Localization cleanup + `EmptyState` control + Expenses/Income empty states (R-19, R-20) | Done (`c2866f7`, `0d60c96`) |
| S6 | Debts/Recurring localization and empty states, sidebar labels to resx (R-19, R-20) | Done (`b4bbdc4`) |
| S7 | `KeyboardShortcutCatalog`, KeyBindings, focus behavior, gating, sidebar rework, focus visuals, Settings shortcut list, keyboard-only expense test (R-12..R-16) | Done (`d96056d`, `055e08a`, `8bf9845`, `08279a7`) |
| S8 | Accessibility pass: automation names, name-scan, contrast, live region check, `docs/accessibility.md` (R-17, R-18) | Done (`b577999`) |
| S9 | Packaging: `build/Publish.props`, `publish.ps1` (4 RIDs), win smoke, archives + checksums, `.app` script, AppImage recipe, draft `release.yml` (R-21..R-24) | Done (`8876338`) |
| S10 | Close-out docs: `architecture.md` §14/§16/§18, `roadmap.md`, `AGENTS.md`, arch guards (R-25, R-26), verify, archive | Done (`23baed0`, `60800fc`; verify and archive in the closing commit) |

## 3. Phase completion criteria

- **(roadmap)** win-x64 self-contained build runs with no preinstalled .NET; other RIDs publish with native libs present.
- **(roadmap)** The register-expense flow completes 100% by keyboard (`Ctrl+N` -> fields -> `Enter` -> row visible), proven headless against the real window and a temp SQLite database.
- **(roadmap)** Every section shows data or an empty state; no hardcoded UI string remains in the four views.
- **(roadmap)** `1.0.0` versioned, CHANGELOG and README current, repo tag-ready (tag only on owner request).
- A forced unexpected exception shows an overlay with an 8-hex id; the same Guid is in the log file; Copy has no stack or user data.
- Logger rolls at 5 MB, keeps 5 files, survives an unwritable directory without throwing; sentinel API key never reaches a log.
- Name-scan and contrast tests pass; shortcut catalog and `MainWindow` KeyBindings match in both directions.
- Build clean, full suite green, `dotnet format style --verify-no-changes` exit 0, NetArchTest R-1..R-5 green, no migration, no new package.
- Unverified items are listed as such in the spec and in the closing docs, not claimed.

## 4. Risks and notes

- **Cross-OS binaries fail on real machines** (medium): layout check per RID, everything non-Windows labelled unverified, draft release only.
- **Global handler hides corrupt state** (medium): every handled error is logged first, no auto-continue claims, overlay recommends restart, Phase-19 restore keeps its own rollback.
- **Log leaks sensitive data** (low): sentinel-key test, EF sensitive logging off, Copy excludes stack and user data.
- **Logger breaks the app on a full disk or locked file** (low): disable the sink on first failure.
- **Shell XAML changes break headless tests** (high): sidebar/shortcuts isolated in S7, full suite every slice.
- **Shortcuts fire during busy flows** (medium): gating requirement and tests.
- **Overpromising accessibility** (medium): Windows UIA only, statement document, unverified table.
- **Tests that pass against stubs** (Phase 18 lesson): every behavior names a real-implementation test (real logger, real reporter, real `MainWindow`, real publish output).
- Deviation from roadmap wording, documented: MSIX deferred (needs a certificate), DMG CI-only, no signing or notarization.

## 5. References

- `docs/roadmap.md` §Fase 21; `docs/architecture.md` §14, §16, §18
- `spec/features/phase-21/F-021-packaging-polish-a11y.done.md`
- `.atl/sdd/f-021p21-packaging/{exploration,proposal}.md`
- `spec/phases/phase-20/features.md` and `spec/features/phase-20/F-020-import.done.md` — format precedent

## 6. Follow-ups (not in this phase)

- MSIX, signed/notarized DMG, Flatpak/Snap, auto-update, universal macOS binary.
- Real-device verification of macOS/Linux builds, VoiceOver and Orca/AT-SPI.
- High-contrast theme, full WCAG audit, shortcut remapping.
- Debts CSV export; leftover Phase-19/20 follow-ups (aside `.bak` sweeping, `ExpensesAi*` Designer accessors).

## 7. Reconciliation with the final state (2026-09-30)

What the phase delivered versus what this plan and the spec said:

| Topic | Plan / spec said | Final state |
| --- | --- | --- |
| Slice count | S1-S10 | 12 commits: S5a/S5b and S7a/S7b were split; S7a, S7b, S9 and S10 span two commits each |
| Names | spec contract names | design names win (`AddRollingFileLogging`, `Assets/Icon/`, `scripts/package-macos-app.ps1`, `packaging/linux/`, `AccessibilityXamlScanTests`); mapped in `tasks/phase-21/` "Reconciliation" #1-#17 |
| macOS archives | `.tar.gz` (spec R-22) | both `.tar.gz` and `.app.zip` are produced; the owner picks one |
| Release workflow parse (S-47, task 9.9) | YAML parse | anchored text rules only; no YAML library, no new package allowed; accepted exception W1, task 9.9 stays unticked |
| Release gate (R-24) | build/test on the matching runners | a `test` job (build + tests, `windows-latest` only) gates every build job; fixed in `ac181ca` (W2) |
| Inline colors (R-19) | none in views | tokenized in `ac181ca` (W3) |
| Publish payload | self-contained folders | test-only and tooling assemblies (Avalonia.Headless, EF design-time, Roslyn) removed; 126-137 MB per RID instead of 141-152 MB; `PublishPayloadTests` guards it (`c11fb6e`, W4) |
| Keyboard-only flow (R-14) | one flow ending in Enter | Tabs to Register then Enter; Enter inside an input is `DefaultButtonTests` (accepted W5) |
| Privacy test (R-5) | real pipeline | the 401 test now goes through the real `HttpClient` pipeline (`ac181ca`, W6) |
| Meta shortcuts | Meta duplicates | macOS only (29 entries, no Meta+Esc); Windows/Linux keep Control only; never run on a Mac |
| Tests | n/a | 3721 (2876 unit + 845 integration), build 0/0, format 0, NetArchTest 20/20 |

Follow-ups added by verify: `exception.ToString()` in log entries can embed user data; two instances sharing a DataDir can exceed the 5 MB roll and prune each other's file; sidebar mouse clicks are not gated during an import or restore; input resting border contrast 1.46:1 (WCAG 1.4.11 gap, in `docs/accessibility.md`).

Still open for the owner: tasks 11.2-11.7 (Narrator, clean-Windows install, macOS and Linux runs, first `release.yml` run, es-DO/en visual pass) and the `v1.0.0` tag.
