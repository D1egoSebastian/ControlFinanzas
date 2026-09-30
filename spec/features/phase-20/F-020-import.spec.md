# F-020 — CSV / Excel Import of Transactions

> **Phase:** 20 — CSV / Excel import
> **Status:** Active (spec written 2026-09-29). Rename to `.done.md` only after archive.
> **SDD:** `.atl/sdd/f-020p20-import/`
> **Input:** `.atl/sdd/f-020p20-import/proposal.md` (D1-D27 and slices S1..S7 binding), `.atl/sdd/f-020p20-import/exploration.md`
> **Depends on:** Phases 0-19 archived (Phase-18 `ClosedXmlReportWriter`, Phase-19 `IFileOpenDialog`, Phase-17 `INotificationCenter`).
> **Blocks:** nothing.
> **Roadmap:** `docs/roadmap.md` §Fase 20; `docs/architecture.md` §Import CSV.

## Goal

Users arriving from a spreadsheet or another finance app cannot bring their history in. Phase 20 adds an "Import"
action to the Expenses toolbar that reads a generic CSV or the app's own Phase-18 XLSX, maps columns, shows an
honest dry-run preview, and commits all valid rows in ONE transaction. The governing rule is: **no silent
corruption**. The system never guesses a day/month or a decimal separator without showing it, never leaves a
partial import, and never duplicates rows on re-import.

## Grounding in current code (binding facts)

- `Result` is NON-generic (`IsSuccess`, `Value: object?`, `Error?`). No `Result<T>`. Use cases return a success `Result` whose `Value` carries the DTO.
- Seeded system category "Otros" has Id `11111111-1111-1111-1111-000000000009` (`IsSystem=true`). Seeded payment method "Otro" is `...-000000000005`.
- `Category.Name` and `PaymentMethod.Name` have a unique index with SQLite `NOCASE` collation (ASCII-only folding), max 100 chars. No category creation path or UI exists today; import is the first.
- `Income.CategoryId` is nullable; `Expense` requires a category. Both enforce amount > 0, date >= 1900-01-01, description 1..200 trimmed, note <= 500.
- `Expense.Amount` is stored as `decimal(18,4)`.
- The UI `AppDbContext` is effectively app-lifetime and shared (VMs resolve from the root provider); a failed `SaveChanges` on it leaves `Added` entities tracked.
- Phase-18 XLSX: expense-list report writes `Expense` rows only; month-summary writes `Expense` and `Income`. Null category is written as `""`. Date is a DateTime cell (`yyyy-mm-dd`), Amount a numeric (double) cell, Currency text, `AmountDOP` may be blank. Text cells use format `@`.
- `IFileOpenDialog.PickAsync(FileTypeFilter, ...)` accepts a single extension today.
- `ExpensesViewModel` has 16 required and 3 optional constructor parameters and is not `INavigationAware`.
- Windows-1252 is unavailable on .NET 8 unless `CodePagesEncodingProvider` is registered (in-box, no package).
- ClosedXML is forbidden in Domain, Application and UI by `ArchitectureTests`.

## Requirements

### R-1 — Ports, DTOs and use cases in Application, library-free

The system MUST define in `Application`:

- `ITabularReader` (reads bytes to a `RawTable`), `IImportWriter` (stages and commits), `IImportLookup` (existing-row, category and payment-method lookup).
- DTOs `RawTable`, `ImportOptions`, `ImportRowResult`, `ImportPreview`, `ImportCommitResult`, and constants `ImportErrorCodes` (see R-24).
- Pure, stateless services `HeaderMapper`, `AmountParser`, `DateParser`, `CategoryMatcher`, `ImportRowValidator`, `DuplicateDetector`.
- Use cases `PreviewImportUseCase` and `CommitImportUseCase`, both returning the non-generic `Result`.

No type from ClosedXML, EF Core, `System.IO.Compression` or Avalonia MAY appear in Application. The pure services MUST have no I/O and no clock or culture dependence (the current culture MUST NOT influence parsing or matching).

### R-2 — Supported formats and content-based reader selection

The system MUST support (a) the Phase-18 XLSX shape and (b) generic delimited text (`.csv`, `.txt`), both producing the same `RawTable` through `ITabularReader`. The file picker MUST offer both filters. The reader MUST be chosen by **content**: bytes beginning with the ZIP signature `PK\x03\x04` select the XLSX reader; anything else selects the CSV reader. The file extension MUST NOT influence reader selection. Bytes MUST be read with a bounded copy that stops at the size cap plus one byte (stream length may be unknown).

### R-3 — CSV parsing (RFC 4180)

The CSV reader MUST be a hand-written state machine (no package) that handles: fields quoted with `"`, doubled `""` as an escaped quote, delimiters and line breaks inside quoted fields, a leading UTF-8 BOM (removed), line endings CRLF, LF and lone CR, and blank lines (skipped). Whitespace outside quotes MUST be preserved in the raw field and trimmed only by downstream validation. An unterminated quoted field at end of input MUST yield `UnreadableFile`.

### R-4 — Encoding detection

The CSV reader MUST first attempt strict UTF-8 (`throwOnInvalidBytes: true`); if decoding fails it MUST decode as Windows-1252. `CodePagesEncodingProvider.Instance` MUST be registered once in Infrastructure. The `RawTable` MUST report which encoding was used, and the preview MUST show the detected encoding and a sample of decoded text so the user can spot mis-decoding.

### R-5 — Delimiter sniffing and override

The reader MUST auto-detect the delimiter among `,`, `;` and TAB (quoted regions ignored during sniffing) using the first lines of the file, and MUST honor an explicit override from `ImportOptions`. Changing the override MUST re-parse from the retained bytes.

### R-6 — Limits enforced before allocation

The system MUST reject with `TooLarge`: a file over 10 MB (10,485,760 bytes), more than 50,000 data rows, more than 50 columns, or any field over 32,768 characters. Limits MUST be checked while streaming/reading, before the offending data is allocated or materialized (a file of 10 MB + 1 byte MUST be rejected without being fully decoded). A file with no header or no data rows after skipping blank lines MUST yield `NoRows`; unreadable content MUST yield `UnreadableFile`.

### R-7 — XLSX reading and ZIP-bomb guard

The XLSX reader (Infrastructure only, ClosedXML) MUST, before handing the stream to ClosedXML, inspect the ZIP central directory and reject with `TooLarge` when the sum of declared uncompressed entry sizes or any single entry exceeds a fixed cap, or when the compression ratio is implausible; declared sizes MUST NOT be the only defense (bytes actually read are also capped). A corrupt or non-XLSX ZIP MUST yield `UnreadableFile`, never an unhandled exception. Typed cells MUST be preserved: date cells become `DateOnly`, numeric cells become `decimal` rounded to 4 places (lossless for Phase-18 exports), text stays text.

### R-8 — Sheet selection

The XLSX reader MUST read the sheet named `Data` when present (case-insensitive), otherwise the first sheet. The first non-empty row is the header row.

### R-9 — Header auto-mapping and mapping step

`HeaderMapper` MUST normalize headers (trim, case-fold, diacritics-fold, collapse separators) and map them to fields using English and Spanish synonyms: Date/Fecha, Description/Descripción/Concepto, Amount/Monto/Importe, Category/Categoría, Type/Tipo, Currency/Moneda, PaymentMethod/Método de pago/Medio de pago, Note/Nota/Notas. Required fields are Date, Description and Amount. The mapping step (column ComboBoxes) MUST be shown only when a required field remains unmapped; when auto-mapping is complete the flow goes straight to the preview. The `AmountDOP` column MUST be ignored. The user MAY override any mapping. Mapping is in memory only (`ImportOptions`); persisted profiles are out of scope. Duplicate headers MUST NOT be silently merged: the first matching column wins and the rest are left unmapped.

### R-10 — Row validation

`ImportRowValidator` MUST reject a row (success-shaped, with a reason code, R-24) when: the amount is not positive after sign handling (zero is rejected); the date is earlier than 1900-01-01; the trimmed description is empty or longer than 200 characters (never truncated); the note is longer than 500 characters (never truncated); the currency is present and not `DOP` or `USD` (case-insensitive); or the type is present and unrecognized. A missing currency column means `DOP`. Future-dated rows MUST be accepted and produce a warning only. Rules MUST mirror the `Expense`/`Income` invariants; the writer still builds entities via `Expense.Create`/`Income.Create`.

### R-11 — Type synonyms, default type and sign convention

Type synonyms (case/diacritics-insensitive): `Expense`, `Gasto`, `Egreso` map to Expense; `Income`, `Ingreso` map to Income. `ImportOptions` carries a default type applied when the type column is unmapped or a cell is empty. An optional sign-convention mode (off / negative-is-expense / negative-is-income) MUST determine the type from the sign when enabled; the stored amount is always the absolute value. Amounts in parentheses (`(1,234.50)`) MUST be treated as negative. Zero MUST be rejected in every mode.

### R-12 — Amount parsing

`AmountParser` MUST: detect the decimal style (point vs comma) from the value; treat a single separator followed by exactly three digits and no other separator (`1,234` or `1.234`) as **thousands** (ambiguity resolved as thousands) unless the "decimal comma" toggle in `ImportOptions` says otherwise; accept both `1.234,56` and `1,234.56`; strip surrounding whitespace; accept an optional leading `-`/`+` and parentheses. It MUST reject (reason `InvalidAmount`) any value containing currency symbols, letters or unsupported characters, and MUST reject (reason `TooManyDecimals`) CSV text with more than 4 fraction digits (never round user text). Parsing MUST return `decimal`, never `double`.

### R-13 — Date parsing

`DateParser` MUST try `yyyy-MM-dd` first (also with `T` time suffix), then the user-selected format from `ImportOptions` (`dd/MM/yyyy`, `MM/dd/yyyy`, `dd-MM-yyyy`, `yyyy/MM/dd`, and similar). It MUST NOT guess day/month: a non-ISO value with no selected format is rejected (`InvalidDate`), and a value that does not parse under the selected format is rejected rather than reinterpreted. The preview MUST show parsed dates so a wrong format is visible before commit. XLSX date cells MUST arrive as `DateOnly` and skip text parsing. Two-digit years MUST be rejected.

### R-14 — Category resolution

`CategoryMatcher` MUST resolve a category name against existing categories with precedence: ordinal, then ordinal-ignore-case, then folded (case + diacritics). A folded match with more than one candidate MUST reject the row as `AmbiguousCategory` (never silently pick one). Matching MUST be by folded value, which is coarser than SQLite `NOCASE`, so a created name can never violate the unique index.

### R-15 — Category creation, cap, and fallback

Unmatched non-empty category names MUST be created only when the "create missing categories" option is on (default on, shown as a checkbox with the list of names in the preview). Created categories: trimmed name of 1..100 characters (longer or empty is rejected as `CategoryNameTooLong`, never truncated), `IsSystem=false`, `Color="#95A5A6"`, `Icon=null`; deduplicated within the batch by folded name. The cap is 50 new categories per import; rows whose category would exceed the cap MUST be handled as if creation were off. When creation is off, an unmatched or empty expense category MUST fall back to the seeded "Otros" resolved **by Id** `11111111-1111-1111-1111-000000000009`, never by name, with a warning; if that category is missing the row is rejected `NoFallbackCategory`. An empty category cell on an income MUST yield `CategoryId = null` (no creation, no fallback). An empty category cell on an expense MUST use the fallback with a warning (Phase 18 writes `""` for a null category).

### R-16 — Payment method (match only)

Payment methods MUST be matched with the same precedence and ambiguity-free rule as R-14. Import MUST NOT create payment methods. An unmatched non-empty value MUST resolve to `null` with a warning (the row is still imported).

### R-17 — Duplicate detection

`DuplicateDetector` MUST compute a fingerprint `(Date, Type, Amount, Currency, normalized Description, resolved Category)` where normalized description is trimmed, whitespace-collapsed, folded. Existing rows are read via `IImportLookup` over the file's min/max date span (expenses and incomes, AsNoTracking projection, no paging through filters). Comparison MUST use **occurrence counting**: if the database already holds N rows with a fingerprint and the file holds M rows with it, min(N, M) rows are skipped as duplicates and the remaining M - N rows are imported; a file legitimately containing two identical rows against an empty database imports both. Skipping duplicates MUST default to ON and be togglable. The comparison MUST use the same 4-decimal `decimal` for both sides. No schema change is permitted for de-duplication. Re-importing the same file MUST insert 0 rows.

### R-18 — Two-step preview and commit

`PreviewImportUseCase(bytes, options)` MUST be a dry run that performs no writes and returns an `ImportPreview` containing: detected format, encoding, delimiter, headers, effective mapping, total rows, counts per outcome (valid, duplicate, rejected, warning), the first 50 rows each with parsed values and reason codes, and the list of categories that would be created. `CommitImportUseCase(bytes, options)` MUST be invoked only from the confirmation panel and MUST **re-parse and re-validate the retained bytes** with the confirmed options rather than trusting preview output. Rows valid at commit but not previewed identically (for example a concurrent edit that creates a duplicate) MUST be reported in `ImportCommitResult` counts.

### R-19 — Commit atomicity in a fresh DI scope

`IImportWriter` (`EfImportWriter`, Infrastructure) MUST open a **fresh `IServiceScopeFactory` scope per call**, run inside `Task.Run` (Sqlite is synchronous), disable `AutoDetectChangesEnabled` while staging, stage every new `Category`, `Expense` and `Income` (built via `Expense.Create`/`Income.Create`), and call `SaveChangesAsync` exactly **once**. Existing repositories MUST NOT be used for import and MUST remain unchanged. Any exception (including a domain `ArgumentException`, a unique-index violation or an injected save failure) MUST result in zero new expenses, zero new incomes and zero new categories persisted, and the shared UI `AppDbContext` MUST NOT be poisoned: a later unrelated save on it MUST persist none of the import entities. `EfImportLookup` MUST likewise use a fresh scope per call. The commit is not cancellable once `SaveChangesAsync` starts.

### R-20 — Success-shaped row outcomes vs file-level failures

Row-level problems (every rejection and warning in R-10..R-17) MUST be success-shaped: the use case returns `Result.Success` carrying counts and per-row reasons, never `Result.Failure` and never an exception. Only file-level problems and system failures MUST be `Result.Failure` with codes `UnreadableFile`, `TooLarge`, `NoRows`, and `Failed` (DB/system failure after rollback). A file in which every row is rejected is a successful preview with zero importable rows; commit MUST NOT be enabled and, if invoked, MUST insert nothing and report zero.

### R-21 — Round trip from the real Phase-18 output

Importing the output of the **real** `ClosedXmlReportWriter` MUST succeed field-identically for: the month summary (Expense and Income rows), and the expense list (Expense only). This covers an empty-string category (expense: fallback to "Otros" with warning; income: null), double amounts persisted at `decimal(18,4)` without drift, DOP and USD currencies, and a blank `AmountDOP` ignored. Importing the same file a second time MUST insert 0 rows.

### R-22 — UI: in-place panel, state machine, guards

The Expenses toolbar MUST show an "Import" button next to "Export". `ExpensesViewModel` MUST gain an optional last constructor parameter `ImportViewModel? import = null` (existing fixtures untouched) and an `IsImportOpen` flag; `ExpensesView` MUST swap its list area for `ImportView` while open. No route is added. `ImportViewModel` MUST expose the states `Idle`, `Mapping`, `Previewed`, `Importing`, `Done` and every command MUST begin with a busy guard `if (State is ...) return;` so double-clicks do nothing while parsing/previewing/importing. A cancelled file dialog (`null` target) MUST show no message and MUST NOT call any use case. The confirmation panel MUST recommend a Phase-19 backup before importing (copy only, not enforced). On `Done`, the parent MUST reload categories and the expense list and fire-and-forget `INotificationCenter.TryRefreshAsync` (optional dependency, failures swallowed). There MUST be no logic in code-behind.

### R-23 — File picker overload

`IFileOpenDialog` MUST gain an additive overload `PickAsync(IReadOnlyList<FileTypeFilter> filters, string? title, CancellationToken)`; the existing single-filter overload MUST delegate to it and keep its behavior. All fakes MUST be updated in the same slice. The Avalonia implementation MUST show one dialog offering CSV/TXT and XLSX filters.

### R-24 — Reason codes and localization

`ImportErrorCodes` MUST define stable string codes: file-level `UnreadableFile`, `TooLarge`, `NoRows`, `Failed`; row-level `InvalidAmount`, `TooManyDecimals`, `ZeroAmount`, `InvalidDate`, `InvalidDescription`, `NoteTooLong`, `InvalidCurrency`, `InvalidType`, `AmbiguousCategory`, `CategoryNameTooLong`, `NoFallbackCategory`; and warning codes `FutureDate`, `CategoryFallback`, `CategoryCreated`, `PaymentMethodUnmatched`, `DuplicateSkipped`. Every code MUST have a localized message in `Strings.es-DO.resx` (es-DO) and the neutral `Strings.resx` (English), with **manual** accessors in `Strings.Designer.cs`. The existing resx completeness test MUST cover the new keys (every neutral key present in es-DO and vice versa, no empty values). The UI MUST map code to message in the ViewModel, never expose raw exception text, and MUST NOT hardcode any UI string.

### R-25 — Architecture and dependencies

No new NuGet package MAY be added. ClosedXML MUST remain Infrastructure-only. `CsvTableReader`, `ClosedXmlTableReader`, `EfImportWriter` and `EfImportLookup` MUST live in Infrastructure, verified by an added `ArchitectureTests` case. `AddImport()` DI extension MUST register the ports; `Bootstrap` MUST register `ImportViewModel`. NetArchTest R-1..R-5 MUST stay green.

### R-26 — Test hygiene

Tests MUST NOT read or write the real AppData directory; they MUST use temp directories or in-memory/temp-file SQLite and, for smoke runs, `CONTROLDEFINANZASIA_DATA_DIR`. Time MUST come from `IClock` fakes where needed. Tests MUST cover the 50,000-row commit within a documented timing ceiling.

## Scenarios

- **S-1 — CSV happy path.** Given a comma-delimited UTF-8 CSV with headers `Date,Description,Amount,Category` and 3 valid rows, When the user picks it, Then auto-mapping completes, no mapping step is shown, the preview lists 3 valid rows, and confirming inserts 3 expenses in one `SaveChangesAsync`.
- **S-2 — Content-based selection.** Given an XLSX renamed to `data.csv`, When picked, Then it is read by the XLSX reader; given a CSV named `data.xlsx`, Then it is read by the CSV reader.
- **S-3 — Quoting and line endings.** Given fields containing `""`, delimiters and embedded CR/LF/CRLF inside quotes, a BOM, and mixed line endings, When parsed, Then values are exact and blank lines are skipped.
- **S-4 — Encoding.** Given a Windows-1252 file with `Café`, When parsed, Then strict UTF-8 fails, text decodes as Windows-1252 and the preview reports the encoding; given valid UTF-8, Then UTF-8 is reported.
- **S-5 — Delimiter.** Given `;`-delimited and TAB-delimited files, When sniffed, Then columns split correctly; When the user overrides the delimiter, Then the bytes are re-parsed.
- **S-6 — Limits.** Given a file of 10 MB + 1 byte, 50,001 rows, 51 columns, or a 32,769-char field, When read, Then `Result.Failure(TooLarge)` is returned before the data is fully materialized.
- **S-7 — ZIP bomb.** Given a small XLSX whose entries declare a huge uncompressed size, When read, Then `TooLarge` is returned without ClosedXML expanding it.
- **S-8 — Sheet selection.** Given a workbook with sheets `Other` and `Data`, Then `Data` is read; given sheets `A` and `B` only, Then `A` is read.
- **S-9 — Auto-mapping en+es.** Given headers `Fecha,Concepto,Monto,Categoría,Moneda`, Then all map without a mapping step; Given headers `When,What,How much`, Then the mapping step appears with Date/Description/Amount unmapped and commit is disabled until all three are mapped.
- **S-10 — Amount forms.** Table-driven: `1234.56`, `1,234.56`, `1.234,56`, `(1,234.50)`, `-50` parse as expected under sign modes; `1,234` and `1.234` are read as 1234; with the decimal-comma toggle `1,234` is 1.234; `$50`, `50 DOP`, `12,3456789` are rejected; `0`, `0.00` are rejected `ZeroAmount`; `1.23456` is rejected `TooManyDecimals`.
- **S-11 — Dates.** `2026-03-04` parses without options; `03/04/2026` with no format is rejected `InvalidDate`; with `dd/MM/yyyy` it is 3 April 2026 and with `MM/dd/yyyy` it is 4 March 2026; `31/02/2026` is rejected; year `1899` is rejected; a two-digit year is rejected; an XLSX date cell needs no format.
- **S-12 — Row validation.** Description of 201 chars, empty description, note of 501 chars, currency `EUR`, unknown type `Foo` each produce their reason code and the row is not imported; other rows still import.
- **S-13 — Type and sign.** Given type column `Gasto`/`Ingreso`, rows land in expenses/incomes; given no type column and default Income, all rows are incomes; given negative-is-expense mode, `-50` is an expense of 50 and `50` an income.
- **S-14 — Future date.** Given a row dated next year, Then it is accepted with a `FutureDate` warning.
- **S-15 — Category resolution.** `comida` matches "Comida" and `alimentacion` matches "Alimentación"; given two existing categories folding to the same key, Then the row is rejected `AmbiguousCategory`.
- **S-16 — Create missing.** Given 3 unknown names and creation on, Then the preview lists them, commit creates 3 non-system categories with color `#95A5A6`, and duplicates within the file create one. Given 51 unknown names, Then only 50 are created and the rest follow the creation-off path. Given the checkbox off, Then unmatched rows use "Otros" by Id with a warning.
- **S-17 — Income category.** Given an income with empty category, Then it imports with `CategoryId = null` and no warning about fallback.
- **S-18 — Payment method.** Given an unmatched payment method, Then the row imports with `null` and a warning; no payment method is created.
- **S-19 — Duplicates.** Given 2 identical rows in the file and 0 in the database, Then both import; given the database already holds 1, Then 1 is skipped and 1 imported; given the same file imported twice, Then the second import inserts 0; with skip turned off, all rows import.
- **S-20 — Re-parse at commit.** Given a preview, When commit runs, Then it re-parses the retained bytes with the confirmed options and its counts come from that re-parse.
- **S-21 — Atomic rollback.** Given a forced failure of the single save (for example an injected exception or a unique violation), Then 0 new expenses, 0 incomes and 0 categories exist, the use case returns `Result.Failure(Failed)`, and a subsequent unrelated save on the shared `AppDbContext` persists none of the import entities.
- **S-22 — All rows invalid.** Given a file where every row is rejected, Then the preview is a success with 0 importable rows and the confirm action is disabled.
- **S-23 — Real round trip.** Given a month-summary XLSX from the real `ClosedXmlReportWriter` with Expense and Income rows in DOP and USD, an empty category and a blank `AmountDOP`, When imported into an empty database, Then all rows are field-identical (date, type, amount, currency, description) and a second import inserts 0. Given an expense-list XLSX, Then only expenses import.
- **S-24 — Cancelled dialog.** Given the user cancels the picker, Then no message is shown, no use case is called and the state stays `Idle`.
- **S-25 — Busy guard.** Given a preview or import is running, When the same command is invoked again, Then it returns immediately and the use case runs once.
- **S-26 — Post-commit.** Given a successful import, Then the Expenses list and category list reload, `TryRefreshAsync` is invoked once and a failure of it is swallowed.
- **S-27 — Localization.** Given es-DO and en cultures, Then every reason code renders a non-empty localized message; the completeness test fails if any key is missing in either resx.
- **S-28 — File-level failure display.** Given `UnreadableFile`, `TooLarge` or `NoRows`, Then the panel shows the localized error and stays out of `Previewed`.
- **S-29 — Performance.** Given 50,000 valid rows, Then commit completes within the documented ceiling with a single save and the UI thread is not blocked.

## Acceptance Criteria

- [ ] **(roadmap)** A generic CSV with configured columns produces valid expenses, without duplicates, in one transaction (S-1, S-19).
- [ ] Supported formats and content-based reader selection (R-2, S-2).
- [ ] CSV parsing: quotes, embedded newlines, BOM, CRLF/LF/CR; UTF-8 strict then Windows-1252; delimiter sniff + override (R-3..R-5, S-3..S-5).
- [ ] Limits enforced before allocation; ZIP-bomb guard (R-6, R-7, S-6, S-7).
- [ ] Sheet selection and header auto-mapping en+es; mapping step only when incomplete (R-8, R-9, S-8, S-9).
- [ ] Row validation, type/sign, amount and date parsing tables green (R-10..R-13, S-10..S-14).
- [ ] Category resolution, ambiguity rejection, creation cap 50 with checkbox, fallback by Id, nullable income category, payment method match-only (R-14..R-16, S-15..S-18).
- [ ] Duplicate fingerprint with occurrence counting; skip on by default; re-import inserts 0 (R-17, S-19).
- [ ] Two-step preview/commit with commit re-parsing retained bytes (R-18, S-20).
- [ ] Commit through a fresh DI scope with ONE `SaveChangesAsync`; forced failure leaves 0 rows and 0 categories and the shared context unpoisoned (R-19, S-21).
- [ ] Success-shaped row outcomes vs `Result.Failure` file-level codes (R-20, S-22, S-28).
- [ ] Round trip from the REAL `ClosedXmlReportWriter` (both reports) is field-identical; second import inserts 0 (R-21, S-23).
- [ ] In-place Import panel in Expenses next to Export; state machine; busy guards; cancelled dialog silent; post-commit reload and refresh; no logic in code-behind (R-22, S-24..S-26).
- [ ] Multi-filter `IFileOpenDialog` overload with fakes updated (R-23).
- [ ] Localized messages for every reason code, resx es-DO + neutral English + manual Designer accessors, completeness test green (R-24, S-27).
- [ ] No new NuGet package; ClosedXML Infrastructure-only; arch guard for the new Infrastructure types; NetArchTest R-1..R-5 green (R-25).
- [ ] Tests never touch the real AppData (R-26); 50,000-row timing test passes (S-29).
- [ ] `CodePagesEncodingProvider` registered; future-dated rows warn only (R-4, R-10, S-14).
- [ ] Gates: `dotnet build -c Release` clean, `dotnet test` green, `dotnet format style --verify-no-changes` exit 0, spec renamed to `.done.md` after archive.
- [ ] Manual check of the real dialog and both locales performed or explicitly recorded as NOT run by hand.

## Out of scope

Bank-specific formats; OFX/QIF/MT940; PDF import; persisted or named mapping profiles (deferred, documented roadmap deviation); AI categorization on import; scheduled or folder-watch import; tags or notes from extra columns; importing debts, goals or budgets; updating existing rows; undo of an import (restore a Phase-19 backup instead); currencies beyond DOP/USD; using `AmountDOP`; exporting rejected rows; import entry in Incomes or Settings; a general category-management UI; making a backup mandatory before import.

## Reconciliation with design (2026-09-29, orchestrator)

All 20 points of `.atl/sdd/f-020p20-import/design.md` "Spec Reconciliation" are accepted; where this spec's wording differs, the design governs (notably: amount interpretation follows the design's worked evidence/grouping table (DD-4) rather than the spec's flat "`1,234` is thousands" rule; ambiguity is rejected or resolved only by the decimal-style toggle, never guessed):

1. `RawTable` rows hold typed `RawCell(Text, Kind)` (Text/Number/Date/Error); XLSX numbers and dates are never re-interpreted by the decimal/date options.
2. No Note/Source column is imported (D13 governs over D3); note and income source are always null.
3. File-level codes add `PreviewStale`, `LookupFailed`, `WriteFailed`, `MappingIncomplete` (with `Details["reason"]`/`["line"]`); row reasons are the `ImportReason` enum (rejections vs warnings).
4. Negative amounts are rejected (`NegativeAmount`) unless sign mode is on; never silently made positive.
5. Sign mode ignores the Type column.
6. Unmatched income category with creation off -> null + warning (expense -> seeded "Otros" by Id).
7. Column-count mismatch rejects the row; unterminated/stray quotes fail the whole file with a line number.
8. UTF-16 with BOM is accepted in addition to UTF-8 / Windows-1252.
9. Windows-1252 comes from `CodePagesEncodingProvider.Instance` directly (no global `RegisterProvider`).
10. Entities are built in Application (`Expense.Create`/`Income.Create`) before the writer; the writer only persists.
11. The picker shows an "All supported files" entry first when several filters are passed.
12. Preview returns the first 50 rows AND the first 50 problem rows plus per-reason counts.
13. Preview dates display with month name (`dd MMM yyyy`); Excel serial numbers and 2-digit years are rejected.
14. Round-trip equality covers Date, Type, Amount, Currency, Description, Category; amounts beyond ~15 significant digits do not survive XLSX (double).
15. `AmountTooLarge` above `99,999,999,999,999.9999` (decimal(18,4)).
16. VM states add `Loading`; `Done` raises `Completed` (parent reload) before Close.
17. Empty currency cell with a mapped column -> default currency + warning; unmapped -> default silently.
18. Blank rows (including all-empty `,,,`) are skipped and not counted as rejected.
19. "One SaveChangesAsync" holds unless the 50k timing test (30 s ceiling) fails; then the rule becomes "one transaction" (chunked saves inside one explicit transaction) — still all-or-nothing.
20. Stale preview is detected by a plan hash, not only by counts.

Carried into apply: S5 confirms the 50k single-SaveChanges ceiling on EF Core 8.0.11 + SQLite; S3 confirms ClosedXML 0.105.1 reports date-formatted numbers as DateTime for user workbooks.
