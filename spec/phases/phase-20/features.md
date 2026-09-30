# Phase 20 — CSV / Excel Import

> **Phase:** 20 — CSV / Excel import
> **Status:** In progress (spec written 2026-09-29; no slice applied yet).
> **Spec ID:** F-020 — `spec/features/phase-20/F-020-import.spec.md`
> **Dependencies:** Phases 0-19 archived (Phase-18 `ClosedXmlReportWriter`, Phase-19 `IFileOpenDialog`, Phase-17 notifications).
> **Blocks:** nothing.
> **Roadmap:** `docs/roadmap.md` §Fase 20
> **Stack:** .NET 8 · Avalonia 11.2.7 (MVVM strict) · ClosedXML 0.105.1 (Infrastructure only, already pinned) · `System.IO.Compression` and `CodePagesEncodingProvider` (BCL) · no schema change, no migration, no new package.

## 1. Summary

An "Import" action in the Expenses toolbar reads a generic CSV (`.csv`/`.txt`) or the app's own Phase-18 XLSX (reader chosen by content), auto-maps en+es headers (mapping step only when incomplete), shows a dry-run preview, and commits all valid rows in ONE `SaveChangesAsync` through a fresh DI scope with full rollback. Duplicates are skipped by a fingerprint with occurrence counting so re-import inserts 0. Missing categories can be created (cap 50); the fallback is the seeded "Otros" by Id. The design priority is no silent corruption: no guessed day/month or decimal separator, no partial import.

Structure: ports, DTOs, pure services and two use cases in `Application`; `CsvTableReader`, `ClosedXmlTableReader`, `EfImportWriter`, `EfImportLookup` and `AddImport()` in `Infrastructure/Import/`; `ImportViewModel`/`ImportView` hosted in-place inside Expenses (no route), multi-filter `IFileOpenDialog` overload in UI.

## 2. Slice plan

Chained slices, each about 450 changed lines or fewer, applied in order.

| ID | Slice | Status |
| --- | --- | --- |
| S1 | Contracts (ports, DTOs, `ImportOptions`, `ImportErrorCodes`), `AmountParser`, `DateParser`, `HeaderMapper` + table-driven unit tests | Pending |
| S2 | `CsvTableReader`: state machine, encodings, delimiter sniff, limits before allocation + tests | Pending |
| S3 | `ClosedXmlTableReader` (ZIP entry guard, typed cells, 4-decimal rounding), content sniff + tests incl. XLSX from the REAL `ClosedXmlReportWriter` (both reports) and a zip bomb | Pending |
| S4a | `CategoryMatcher`, `ImportRowValidator`, `DuplicateDetector` + unit tests | Pending |
| S4b | `IImportLookup`/`EfImportLookup`, `PreviewImportUseCase` + integration tests (real SQLite) | Pending |
| S5 | `EfImportWriter` (fresh scope, one save), `CommitImportUseCase`, `AddImport()`; forced-failure rollback, re-import inserts 0, 50k-row timing | Pending |
| S6a | `ImportViewModel` (state machine, busy guards, silent cancel, error-key mapping) + VM tests | Pending |
| S6b | `IFileOpenDialog` overload + fakes, resx es-DO/en + Designer, Bootstrap DI, `ExpensesViewModel` optional param + toolbar command + reload + fixtures | Pending |
| S7 | `ImportView.axaml` (mapping combos, preview grid, confirm panel), `frontend-design` pass, end-to-end export-to-import round trip, smoke with `CONTROLDEFINANZASIA_DATA_DIR`, docs reconciliation, archive | Pending |

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
- `IFileOpenDialog` overload breaks existing fakes (certain); updated in S6b.
- Persisted mapping profiles deferred: deliberate deviation from the roadmap, recorded in docs.

## 5. References

- `docs/roadmap.md` §Fase 20; `docs/architecture.md` §Import CSV (names to be reconciled in S7)
- `spec/features/phase-20/F-020-import.spec.md`
- `.atl/sdd/f-020p20-import/{exploration,proposal}.md`
- `spec/phases/phase-19/features.md` — format precedent

## 6. Follow-ups (not in this phase)

- Persisted/named mapping profiles.
- Bank-specific formats (OFX/QIF/MT940), PDF import, AI categorization on import.
- Undo of an import (today: restore a Phase-19 backup).
- General category-management UI (import is the first path creating user categories).
