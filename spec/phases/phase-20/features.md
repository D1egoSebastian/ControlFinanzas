# Phase 20 — CSV / Excel Import

> **Phase:** 20 — CSV / Excel import
> **Status:** Completed (archived 2026-09-30). Slices S1-S7 applied 2026-09-29/30, verification PASS WITH WARNINGS (0 critical, W1-W5 fixed); manual checks NOT run by hand.
> **Spec ID:** F-020 — `spec/features/phase-20/F-020-import.done.md`
> **Dependencies:** Phases 0-19 archived (Phase-18 `ClosedXmlReportWriter`, Phase-19 `IFileOpenDialog`, Phase-17 notifications).
> **Blocks:** nothing.
> **Roadmap:** `docs/roadmap.md` §Fase 20
> **Stack:** .NET 8 · Avalonia 11.2.7 (MVVM strict) · ClosedXML 0.105.1 (Infrastructure only, already pinned) · `System.IO.Compression` and `CodePagesEncodingProvider` (BCL) · no schema change, no migration, no new package.

## 1. Summary

An "Import" action in the Expenses toolbar reads a generic CSV (`.csv`/`.txt`) or the app's own Phase-18 XLSX (reader chosen by content), auto-maps en+es headers (mapping step only when incomplete), shows a dry-run preview, and commits all valid rows in ONE `SaveChangesAsync` through a fresh DI scope with full rollback. Duplicates are skipped by a fingerprint with occurrence counting so re-import inserts 0. Missing categories can be created (cap 50); the fallback is the seeded "Otros" by Id. The design priority is no silent corruption: no guessed day/month or decimal separator, no partial import.

Structure: ports, DTOs, pure services and two use cases in `Application`; `CsvTableReader`, `ClosedXmlTableReader`, `EfImportWriter`, `EfImportLookup` and `AddImport()` in `Infrastructure/Import/`; `ImportViewModel`/`ImportView` hosted in-place inside Expenses (no route), multi-filter `IFileOpenDialog` overload in UI.

## 2. Slice plan

Chained slices, applied in order (S6a and S6b were re-cut during apply: the picker overload landed first, and the view was pulled forward from S7 into S6b).

| ID | Slice | Status |
| --- | --- | --- |
| S1 | Contracts (ports, DTOs, `ImportOptions`, `ImportErrorCodes`, `ImportReason`, `ImportLimits`), `ImportSourceLoader`, `AmountParser`, `DateParser`, `HeaderMapper` + table-driven unit tests | Done, `aa36efa` |
| S2 | `CsvTableReader`: state machine, encodings, delimiter sniff, limits before allocation + tests | Done, `0a8d6dd` |
| S3 | `ClosedXmlTableReader` (ZIP entry guard, typed cells, 4-decimal rounding), `SniffingTabularReader` + tests incl. XLSX from the REAL `ClosedXmlReportWriter` (both reports) and a zip bomb | Done, `6c8c17f` |
| S4a | `CategoryMatcher`, `ImportRowValidator`, `DuplicateDetector`, `ImportText` + unit tests | Done, `7035395` |
| S4b | `IImportLookup`/`EfImportLookup`, `ImportPipeline`, `ImportPlanner`, `PreviewImportUseCase` + integration tests (real SQLite) | Done, `88f44bc` |
| S5 | `ImportMaterializer`, `CommitImportUseCase`, `EfImportWriter` (fresh scope, one save), `AddImport()`; forced-failure rollback, re-import inserts 0, 50k-row timing (single `SaveChangesAsync` kept, about 6.5 s) | Done, `7b84eb2` |
| S6a | Part 1: multi-filter `IFileOpenDialog` overload + `FakeFileOpenDialog` (`21ee899`). Part 2: `ImportViewModel` (state machine, busy guards, silent cancel, error-key mapping), every `Import*` string in resx es-DO/en + manual Designer accessors, VM and resource tests (`fc274fd`). Follow-up: `e16c469` makes the VM tests deterministic | Done, `21ee899` + `fc274fd` + `e16c469` |
| S6b | Avalonia picker override + pure filter builder, `ExpensesViewModel` optional `import` parameter, toolbar command and reload, Bootstrap DI (`ImportViewModel` Transient), `ImportView.axaml` and its XAML scan tests (pulled forward from S7) | Done, `9905715` |
| S7 | `frontend-design` review of `ImportView` (action bar reachable while loading, in-place "choose another file" after a failed load, automation names, headless Avalonia tests), `ImportEndToEndTests` (real export to import round trip, 11 tests), `ImportViewModel` end to end, smoke driven through UI Automation with `CONTROLDEFINANZASIA_DATA_DIR`, docs reconciliation, and a fix for a pre-existing crash in the `ExpensesView` row template (`#Root.((vm:ExpensesViewModel)DataContext)`) | Done, see `git log` (slice S7/9) |
| Post-verify | W1-W5 fixed test-first: acknowledgement gate for ambiguous amounts/dates, XLSX more than four decimals rejected, `UndecodableText`, 51st new category rejected, `System.IO.Compression` NetArchTest guard, spec reconciliation | Done, `43f7c38` |
| UI fixes | Pre-existing Avalonia binding defects found during verification: DatePicker `DateOnlyConverter` bindings (Expenses, Income, Debts, RecurringRules) and row-template `#Root.DataContext.Cmd` in 7 views, shared `HeadlessUi` harness | Done, `b735ced` |
| Cierre | `sdd-verify` (PASS WITH WARNINGS, 0 critical), docs, archive (spec renamed to `.done.md`); manual checks (tasks 8.2, 8.3) NOT run by hand | Done, closing commit `docs(phase-20): verify and close SDD cycle for import` |

## 3. Phase completion criteria

- **(roadmap)** A generic CSV with configured columns produces valid expenses, without duplicates, in one transaction.
- XLSX from the real `ClosedXmlReportWriter` (expense list and month summary; DOP/USD; blank `AmountDOP`; empty category) imports field-identical; a second import inserts 0.
- Forced failure of the single save leaves 0 new rows and 0 new categories and does not poison the shared context.
- Table-driven CSV tests pass for `, ; \t`, quotes/embedded newlines, BOM, UTF-8 vs Windows-1252, decimal comma/point, thousands, negatives/parentheses, each date format and invalid rows.
- Over-limit files and zip bombs are rejected before full parsing; 50,000 rows import within the S5 ceiling.
- Cancelled picker writes nothing and shows no error; busy guards prevent double execution; UI works in es-DO and en.
- Build clean, full suite green, `dotnet format style --verify-no-changes` exit 0, NetArchTest R-1..R-5 green.

## 4. Risks and notes

- **Silent corruption from decimal/date misreads** (high): preview shows parsed values, decimal-comma toggle, explicit date format, no day/month guessing, more than 4 decimals rejected.
- **Silent partial import via the shared DbContext**: fresh scope per writer/lookup call; test that a later save persists nothing after a forced failure.
- Windows-1252 fallback can misdecode: preview shows encoding and sample.
- Huge files / ZIP bombs: caps before allocation and ZIP entry inspection.
- 50k-row single save: `Task.Run`, `AutoDetectChangesEnabled` off, busy indicator, timing test.
- `IFileOpenDialog` overload: shipped as a default interface member (falls back to the first filter), so existing implementers kept compiling; `FakeFileOpenDialog` and `AvaloniaFileOpenDialog` override it (S6a part 1, S6b).
- Persisted mapping profiles deferred: deliberate deviation from the roadmap, recorded in docs.

## 5. References

- `docs/roadmap.md` §Fase 20; `docs/architecture.md` §11g (names reconciled in S7)
- `spec/features/phase-20/F-020-import.done.md`
- `.atl/sdd/f-020p20-import/{exploration,proposal}.md`
- `spec/phases/phase-19/features.md` — format precedent

## 6. Follow-ups (not in this phase)

- Persisted/named mapping profiles (deferred on purpose: documented deviation from the roadmap deliverable).
- Bank-specific formats (OFX/QIF/MT940), PDF import, AI categorization on import.
- Undo of an import (today: restore a Phase-19 backup).
- General category-management UI (import is the first path creating user categories).
- Manual checks not run by hand: real native open dialog with a Phase-18 XLSX, re-import via the UI, es-DO/en visual pass, Settings screen smoke (tasks 8.2, 8.3).
- `ExpensesAi*` resx keys (six) lack Designer accessors (pre-existing); `MainWindow.axaml:160` `$parent[ItemsControl].DataContext.DismissCommand` not exercised headless; a workbook without a `Data` sheet falls back to the first sheet (accepted).

## 7. Verification outcome

Exhaustive verification: PASS WITH WARNINGS, 0 critical. W1-W5 fixed in `43f7c38`. Final gates (HEAD `b735ced`, re-run at the Cierre): build 0 warnings / 0 errors, 3323 tests green (2551 unit + 772 integration, up from 2289 at the Phase 19 close), `dotnet format style --verify-no-changes` exit 0, NetArchTest 18 of 18. See `.atl/sdd/f-020p20-import/verify-report.md` and `archive-report.md`.
