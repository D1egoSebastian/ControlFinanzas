# Tasks — F-020 CSV / Excel Import of Transactions

> Governing inputs: `spec/features/phase-20/F-020-import.spec.md` (its final "Reconciliation with design"
> section governs where wording differs), `.atl/sdd/f-020p20-import/design.md` (per-slice sections, DD-1..DD-15 and the
> 22-row Silent-Data-Corruption Threat Table are binding) and `.atl/sdd/f-020p20-import/proposal.md` (slice names
> S1, S2, S3, S4a, S4b, S5, S6a, S6b, S7 confirmed). Requirement ids (R-n) and scenario ids (S-n) below refer to the
> spec. Design decisions are cited as DD-n (design), proposal decisions as D-n, threat rows as C-n (design table).
> Strict TDD: within each slice, failing tests come first, then implementation.
>
> **Shared-file guard:** every touch on a shared file (`Bootstrap.cs`, `Strings.resx`, `Strings.es-DO.resx`,
> `Strings.Designer.cs`, `ArchitectureTests.cs`, `ExpensesView.axaml`, `ExpensesViewModel.cs`, `AGENTS.md`,
> `FakeFileOpenDialog.cs`) MUST be an appended block or an additive member. Nothing existing may be reordered,
> renamed or restructured. `Directory.Packages.props` and every `.csproj` stay UNCHANGED except the integration test
> project's `Fixtures/*.csv` `CopyToOutputDirectory` item (test assets only, no package) (R-25).
>
> **Anti-stub rules (Phase 18/19 lessons, apply to every slice):**
> 1. A task that implements behavior names the concrete artifact (file + method) AND the test that proves it
>    against the REAL implementation (`CsvTableReader` on real bytes, `ClosedXmlReportWriter` real output,
>    `EfImportWriter` on a real temp SQLite file, the real VM). Fakes are allowed only for the collaborators around
>    the component under test, never for the component itself.
> 2. No task may be ticked while its artifact contains `NotImplementedException`, `TODO`, a stub, an empty method
>    body, a hard-coded return that mimics success, or a test with no assertion. A partially met task stays
>    unticked and gets a note.
> 3. Every slice ends with a "Run all four gates" item that includes the no-stubs grep, which MUST return nothing:
>    `rg -n "NotImplemented|TODO|stub|Task\.FromResult\(Result\.Failure" <files touched by the slice>`.
> 4. **Flakiness rule:** every slice runs the FULL suite (`dotnet test -c Release --no-build`) THREE consecutive
>    times. Any intermittent failure is reported with the failing test names in the slice's apply notes; a slice is
>    not closed with an unexplained intermittent.
> 5. **Silent-corruption rule:** every row C-1..C-22 of the design threat table maps to at least one named test
>    task (traceability table at the end of this file). A row is not "covered" by a fake of the same component.
> 6. Strict TDD honesty: if a test could not be run red first (it does not compile before the type exists), say so in
>    the apply notes rather than claiming a red/green cycle.
> 7. Tests never read or write the real `%APPDATA%\ControlDeFinanzasIA` (R-26): temp directories, temp-file SQLite,
>    `IClock` fakes.
>
> **Smoke-launch safety rule (S6a, S6b, S7):** any `dotnet run` smoke MUST set `CONTROLDEFINANZASIA_DATA_DIR` to a
> throwaway directory first, MUST NOT open, import into or restore against `%APPDATA%\ControlDeFinanzasIA\data.db`, and
> the real profile listing must be recorded identical before/after. If the override cannot be confirmed, the smoke is
> limited to "app starts and Expenses opens", no button pressed, and the task says so.

## Review Workload Forecast

| Field | Value |
|---|---|
| Estimated changed lines | ≈3,660 total across 9 chained slices (production + tests: S1 ~420, S2 ~430, S3 ~380, S4a ~420, S4b ~380, S5 ~440, S6a ~420, S6b ~350, S7 ~420) |
| 400-line budget risk | High — S1, S2, S4a, S5, S6a, S7 sit at or above the budget; S5 is the largest. If a slice grows past ~450 lines during apply, stop and split along the seam noted in its header |
| Chained PRs recommended | Yes |
| Suggested split | PR S1 → PR S2 → PR S3 → PR S4a → PR S4b → PR S5 → PR S6a → PR S6b → PR S7 |
| Delivery strategy | auto-chain |
| Chain strategy | stacked-to-main |

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: High

The proposal's slice order is authoritative and strictly chained. S1 is pure Application (contracts, three parsers,
source loader; no DI change). S2 is the CSV reader (the biggest parsing-risk surface). S3 is the XLSX reader plus the
content sniffer and the first use of the REAL `ClosedXmlReportWriter` as a fixture generator. S4a is the pure
row/category/duplicate logic; S4b joins it to a real lookup and the preview use case. S5 adds the materializer, the
commit use case and the atomic writer with the forced-failure and 50k-row gates. S6a is the ViewModel and every
localized string it needs; S6b is the picker overload, Expenses hosting and DI; S7 is the view, end-to-end round trip
and docs.

**Placement deviation from the design file-change table (recorded up front):** the design lists resx in S6b, but the S6a
ViewModel tests assert localized messages and the "every `ImportReason` has a non-empty string" reflection test, so the
`Import*` resx keys and their manual Designer accessors are created in S6a. S6b then appends only the picker/toolbar keys
(`ImportButton`, `FileDialogAllSupportedTypes`, `ImportFileType*`, `ImportDialogTitle`) if they were not already added.
The design places the materializer/`CommitImportUseCase` in S5 and `ImportView.axaml` in S7; this file follows that.

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|---|---|---|---|
| S1 | Contracts + pure parsers | PR S1 | Base = main. Ports, DTOs, `ImportErrorCodes`, `ImportReason`, `ImportLimits`, `HeaderMapper`, `AmountParser`, `DateParser`, `ImportSourceLoader`. Threat rows C-1..C-4, C-6, C-7, C-11, C-22. |
| S2 | CSV reader | PR S2 | Base = main (S1 merged). `CsvTableReader`: state machine, encodings, sniff, limits. Rows C-8, C-9 (reader half), C-10. |
| S3 | XLSX reader + content sniff | PR S3 | Base = main (S2 merged). `ClosedXmlTableReader` (ZIP guard), `SniffingTabularReader`, REAL `ClosedXmlReportWriter` fixtures for both report types. Row C-16. Confirms ClosedXML date detection. |
| S4a | Row validator, matcher, duplicates | PR S4a | Base = main (S3 merged). `ImportRowValidator`, `CategoryMatcher`, `DuplicateDetector`, `ImportText`. Rows C-9 (validator half), C-12..C-15, C-17. |
| S4b | Lookup, planner, preview | PR S4b | Base = main (S4a merged). `EfImportLookup`, `ImportPipeline`, `ImportPlanner`, `PreviewImportUseCase`. Rows C-5, C-21. |
| S5 | Materializer, commit, atomic writer | PR S5 | Base = main (S4b merged). `ImportMaterializer`, `CommitImportUseCase`, `EfImportWriter`, `AddImport()`, arch guard. Rows C-19, C-20 (+ C-18 unit half). **50k timing + decision gate.** |
| S6a | ViewModel + strings | PR S6a | Base = main (S5 merged). `ImportViewModel`, resx x2 + manual Designer accessors + completeness coverage. Rows C-6 (banners), C-19 (VM half). |
| S6b | Picker overload, hosting, DI | PR S6b | Base = main (S6a merged). `IFileOpenDialog` overload + fakes, `ExpensesViewModel` optional param, `Bootstrap`, fixtures. **Smoke-launch on throwaway data dir.** |
| S7 | View, end-to-end, docs | PR S7 | Base = main (S6b merged). `ImportView.axaml`, frontend-design, `ImportEndToEndTests`, docs. Row C-18. **Smoke-launch in both locales on throwaway data dir.** |

## 1. Contracts and pure parsers (S1)

Depends on nothing (Phases 0-19 archived). Goal: library-free ports, DTOs, codes, limits, the bounded source loader and
the three pure parsers live in Application. No type from ClosedXML, EF Core, `System.IO.Compression` or Avalonia
(R-1, R-25). Size note: if it exceeds ~450 lines, split at the seam "contracts + loader" vs "three parsers".

- [x] 1.1 Write failing `tests/…Unit/Import/ImportSourceLoaderTests.cs` against the REAL `ImportSourceLoader.LoadAsync`
  using in-memory streams wrapped in `OpenTargets.Recording()`: a stream of exactly `ImportLimits.MaxFileBytes`
  passes and the retained bytes equal the input; `MaxFileBytes + 1` returns `Result.Failure(TooLarge)` with
  `Details["reason"] = fileTooLarge` and reads no more than cap + 1 bytes (counting stream asserts the read total);
  a NON-seekable stream with unknown `Length` is read to EOF and equals the input (no truncation); the target is
  opened exactly once and disposed. (R-2, R-6, S-6, C-22)
- [x] 1.2 Write failing `tests/…Unit/Import/HeaderMapperTests.cs`: the exact Phase-18 headers (`Date, Type, Description,
  Category, Amount, Currency, AmountDOP`) map completely and `AmountDOP` lands in `Ignored`, never on Amount; es
  synonyms `Fecha, Concepto, Monto, Categoría, Moneda` (and the same without accents/with odd case/`Método de pago`,
  `Medio`-style separators collapsed) map completely; exact-equality only (`AmountDOPExtra`, `Dates` do not map);
  duplicate `Fecha`/`Date` leaves Date unmapped and listed in `Conflicting`; unknown headers are ignored;
  `When, What, How much` maps nothing and lists Date/Description/Amount as `Missing`; `IsComplete` needs Date,
  Description and Amount. (R-9, S-9, C-11)
- [x] 1.3 Write failing `tests/…Unit/Import/AmountParserTests.cs` (table-driven, one `[Theory]` row per DD-4 table row ×
  style, asserting `Value`, `Error`, `WasAmbiguous`): `1234.56`, `1,234.56`, `1.234,56`, `(1,234.50)`, `-50`, `+50`;
  `1,234`/`1.234` are 1234 under Point (warn `AmbiguousAmount` in Auto) and 1.234/1234 under Comma; `12,5`,
  `1.234.567`, `1 234,56`, `12,34,567`; NBSP/U+202F group separators; `RD$ 1,500.00`, `$50`, `50 DOP`, `1e5`, `--3`,
  `12,3456789`, trailing `-` → `InvalidAmount`; `12.34567` → `TooManyDecimals`; `0`, `-0,00` parse to 0 (rejected later
  by the validator); overflow and `MaxAmount + 0.0001` → `AmountTooLarge`; Number cells bypass style
  (`RawCell("1234.5", Number)` under `Comma` is 1234.5). Also `Detect_*` tests: evidence counts per token shape,
  `IsConflict`, no-evidence → Point, majority resolve; and `Parse_Contradicting_Rejected` (a `1.234,56` token under the
  resolved Point style is `InvalidAmount`). Result is `decimal`, never `double`; the current culture (set to es-DO
  and en inside the test) never changes any result. (R-11, R-12, S-10, C-1, C-2, C-3, C-4)
- [x] 1.4 Write failing `tests/…Unit/Import/DateParserTests.cs`: ISO `2026-03-04` needs no options; ISO with
  `T12:30:00`, `T12:30:00Z`, `T12:30:00+05:00` and a space time suffix takes the date as written (no time-zone shift);
  `2026-02-30` → `InvalidDate` with no fall-through to a user format; `03/04/2026` under each of
  `DayMonthYear` (3 April), `MonthDayYear` (4 March), `03-04-2026` under `DayMonthYearDash`; 1- and 2-digit parts;
  `31/02/2026` and `13/13/2026` rejected; two-digit year `03/04/26` → `InvalidDate`; a `Number` kind cell (Excel
  serial) → `InvalidDate`; a `Date` kind cell parses exactly; `Error` kind → `CellError`; empty → `MissingDate`;
  `DateEvidence` classification (`25/03/2026` under MonthDayYear → `Contradicts`; only both-≤12 tokens →
  `AmbiguousOnly`; consistent; no text dates → `NotApplicable`). The current culture never changes any result.
  (R-13, S-11, C-6, C-7)
- [x] 1.5 Create `src/…Application/Abstractions/{ITabularReader,IImportLookup,IImportWriter,TabularReadOptions}.cs`
  exactly as DD-1 (non-generic `Result`; `ITabularReader.ReadAsync(ReadOnlyMemory<byte>, TabularReadOptions,
  CancellationToken)`). `IFileOpenDialog` is NOT touched in this slice (S6b). (R-1, DD-1)
- [x] 1.6 Create `src/…Application/UseCases/Import/Common/{RawCell,RawRow,RawTable,TableSourceInfo,ImportOptions,
  ColumnMapping,ImportEnums,ImportRowResult,ImportPreview,ImportCommitResult,ImportBatch,ImportLookupSnapshot,
  ImportSource}.cs` per DD-1 (typed `RawCell(Text, Kind)`, `ColumnMapping.IsComplete`, `ImportOptions` defaults:
  `DefaultType = Expense`, `DefaultCurrency = DOP`, `SkipDuplicates = true`, `CreateMissingCategories = true`). No
  behavior beyond the records. (R-1, R-18, Reconciliation #1)
- [x] 1.7 Create `Common/{ImportErrorCodes,ImportReason,ImportReasons,ImportLimits}.cs` per DD-2: file-level codes
  including `PreviewStale`, `LookupFailed`, `WriteFailed`, `MappingIncomplete`, `Details` keys and `Reason*`
  constants; the full `ImportReason` enum with `ImportReasons.IsRejection` (`r < FutureDate`); `ImportLimits` with
  `MaxFileBytes = 10_485_760`, `MaxDataRows = 50_000`, `MaxColumns = 50`, `MaxFieldChars = 32_768`,
  `MaxXlsxUncompressedBytes`, `MaxXlsxEntries`, `MaxNewCategories = 50`, `PreviewRows = 50`, `MaxAmount`,
  `FallbackCategoryId = 11111111-1111-1111-1111-000000000009`, `NewCategoryColor = "#95A5A6"`. A unit test
  (`ImportReasonTests`) asserts `IsRejection` is true for every value before `FutureDate` and false from it on, and
  that the enum has no duplicate names. (R-20, R-24, DD-2)
- [x] 1.8 Create `Common/ImportSourceLoader.cs` (`LoadAsync(OpenTarget, CancellationToken)`, bounded copy to cap + 1 in
  buffers, never `Stream.Length`) and run 1.1 green. (R-2, R-6, C-22)
- [x] 1.9 Create `Parsing/HeaderMapper.cs` (`AutoMap`, `NormalizeHeader`, exact-equality synonym table from DD-3, ignore
  list `AMOUNTDOP`/`MONTODOP`) and run 1.2 green. (R-9, C-11)
- [x] 1.10 Create `Parsing/AmountParser.cs` (`Detect`, `Parse`, strict grammar of DD-4, `decimal` only) and run 1.3
  green. No `double` and no `CultureInfo.CurrentCulture` reference (grep in 1.12). (R-11, R-12, C-1..C-4)
- [x] 1.11 Create `Parsing/DateParser.cs` (`Parse`, `Detect`, `DateEvidence`, literal-separator `TryParseExact`, ISO
  first) and run 1.4 green. (R-13, C-6, C-7)
- [x] 1.12 Purity checks: `rg -n "CurrentCulture|DateTime\.Now|DateTime\.UtcNow|File\.|Stream\b|double|float"
  src/ControlDeFinanzasIA.Application/UseCases/Import/Parsing` returns nothing; `git diff --name-status` shows only
  `A` entries under `Application/Abstractions`, `Application/UseCases/Import` and `tests/…Unit/Import`. (R-1, R-25)
- [x] 1.13 Run all four gates: `dotnet build -c Release`, `dotnet test -c Release --no-build`, `dotnet format style
  --verify-no-changes`, NetArchTest R-1..R-5 (Application has no ClosedXML/EF/Compression/Avalonia reference). Then the
  no-stubs grep over `src/ControlDeFinanzasIA.Application/UseCases/Import`, `src/ControlDeFinanzasIA.Application/
  Abstractions` (new files) and `tests/ControlDeFinanzasIA.Tests.Unit/Import`, which returns nothing. Confirm N > 0
  for `AmountParserTests`, `DateParserTests`, `HeaderMapperTests`, `ImportSourceLoaderTests`. Run the FULL suite 3
  consecutive times and record any intermittent failure by test name. (R-25)

## 2. CSV reader (S2)

Depends on 1. Goal: `CsvTableReader` turns real bytes into a typed `RawTable` with the RFC-4180 state machine, strict
encoding fallback, delimiter sniff and limits enforced before allocation. Size note: if it exceeds ~450 lines, split
at the seam "encoding + sniff" vs "state machine + limits".

- [x] 2.1 Create the checked-in binary fixtures under `tests/…Integration/Import/Fixtures/` (set as
  `CopyToOutputDirectory` items, test-asset-only csproj edit): `bank-es-semicolon-comma-1252.csv` (Windows-1252 bytes,
  `;` delimiter, `1.234,56`, `Categoría`), `excel-unicode-tab-utf16.txt` (UTF-16 LE BOM, TAB), `generic-en-comma-utf8bom.csv`,
  `quotes-multiline.csv`. Add a test `FixturesAreByteExactTests` asserting the 1252 file contains byte `0xED` (not
  `0xC3 0xAD`) so a future editor re-save cannot silently convert it. (R-4, S-4, C-8)
- [x] 2.2 Write failing `tests/…Integration/Import/CsvTableReaderTests.cs` against the REAL `CsvTableReader` (public,
  sealed, stateless; no fakes): (a) `,` `;` TAB sniff picks the right delimiter on the fixtures and on synthetic
  inputs, with quoted regions ignored, and an explicit `TabularReadOptions(';')` overrides the sniff and re-parses the
  same bytes differently (S-5); (b) quoting: `""` escape, delimiter inside quotes, embedded CRLF, LF and lone CR inside
  quotes yield exact values (S-3); (c) UTF-8 BOM removed; CRLF, LF, lone CR record ends; mixed endings; blank lines
  and all-empty `,,,` lines skipped and not counted; whitespace outside quotes preserved in the raw field (S-3, R-3,
  Reconciliation #18); (d) encodings with REAL byte fixtures: valid UTF-8 reports UTF-8 with `EncodingFallback =
  false`; the 1252 file decodes `Categoría`/`Café` correctly with `EncodingFallback = true` and `SampleText` (first
  200 chars); UTF-16 LE/BE BOM files decode; a NUL byte without BOM → `UnreadableFile/binary` (S-4, R-4, C-8);
  (e) malformed input: unterminated quote at EOF and a stray quote in the middle of an unquoted-after-quote field →
  `UnreadableFile/malformedCsv` with `Details["line"]` equal to the physical starting line; a quote inside an unquoted
  field is kept literally (R-3, C-10); (f) `NoRows` for empty bytes, header only, and only blank lines; (g) every cell
  has `RawCellKind.Text`; `SourceRowNumber` is the 1-based physical line where the record started; (h) records whose
  field count differs from the header are NOT dropped by the reader (validator decides, S4a); (i) `ReadAsync` returns
  a faulted/cancelled task never for bad input (file-level failures are `Result.Failure`). (R-3..R-6, S-3..S-6, C-8,
  C-9, C-10)
- [x] 2.3 Write failing limit tests in the same file, small synthetic inputs, each asserting failure BEFORE full
  materialization: 51 columns → `TooLarge/tooManyColumns` and 50 passes; a field of 32,769 chars →
  `TooLarge/fieldTooLong` and 32,768 passes; 50,001 data rows → `TooLarge/tooManyRows` and 50,000 passes; 10 MB + 1
  byte → `TooLarge/fileTooLarge` and the test proves it was not decoded (input is invalid UTF-8/1252 garbage that would
  otherwise fall through to a different result, plus a measured elapsed bound); for the row and field limits, a
  counting hook (or a byte fixture whose tail is malformed CSV) proves the reader stopped at the offending record
  rather than reading to the end. (R-6, S-6)
- [x] 2.4 Create `src/…Infrastructure/Import/CsvTableReader.cs` per DD-9 (size check first; BOM/encoding decision;
  `UTF8Encoding(false, true)`; 1252 via `CodePagesEncodingProvider.Instance.GetEncoding(1252, …)` directly, no
  `Encoding.RegisterProvider`; sniff over the first 20 records scoring `\t`, `;`, `,` in that tie order; the four-state
  machine; limits inside the pass; body in `Task.Run(..., ct)`). Run 2.2 and 2.3 green. (R-3..R-6, DD-9,
  Reconciliation #7, #8, #9)
- [x] 2.5 Add a determinism/culture test: the same bytes parsed under es-DO and en current cultures give identical
  `RawTable`s. (R-1, R-4)
- [x] 2.6 Verify no new package and no `Encoding.RegisterProvider`: `git diff --name-only` shows no
  `Directory.Packages.props`/`.csproj` change other than the fixtures item; `rg -n "RegisterProvider"
  src/ControlDeFinanzasIA.Infrastructure/Import` returns nothing. (R-25, Reconciliation #9)
- [x] 2.7 Run all four gates plus the no-stubs grep over `src/ControlDeFinanzasIA.Infrastructure/Import` and
  `tests/ControlDeFinanzasIA.Tests.Integration/Import`. Confirm `CsvTableReaderTests` executed (N > 0,
  `--filter FullyQualifiedName~Import`). Run the FULL suite 3 consecutive times and record intermittent failures by
  test name. (R-25)
  > Apply notes (S2): tests were written before the types existed, so the red state was a compile failure, not an
  > assertion failure (rule 6). Reader split into CsvTableReader (encoding, sniff) and CsvRecordParser (state
  > machine, limits). Added `.gitattributes` (`-text` for Import/Fixtures) so autocrlf cannot alter the fixture bytes;
  > DI extension `AddImportReaders()` registers only `CsvTableReader` (the port is bound in S3). Full suite 3x green:
  > 1894 unit + 624 integration, no intermittent failures.

## 3. XLSX reader and content sniffing (S3)

Depends on 2. Goal: `ClosedXmlTableReader` (Infrastructure only) reads Phase-18 workbooks and user workbooks into typed
cells behind a ZIP guard, and `SniffingTabularReader` selects the reader by content. Carried into apply: confirm
ClosedXML 0.105.1 date detection for user workbooks.

- [x] 3.1 **ClosedXML date-detection characterization (first task of the slice, before production code).** Write a
  permanent test `tests/…Integration/Import/ClosedXmlBehaviorAssumptionsTests.cs` on pinned ClosedXML 0.105.1 that
  builds workbooks in memory and proves: (a) a numeric cell with a date number format (`yyyy-mm-dd`, `dd/mm/yyyy`)
  reports `XLDataType.DateTime` on reload (the "user workbook" case, where the writer did not assign a `DateTime`);
  (b) a numeric cell with format `0.00` reports `Number` and a `@` text cell holding `0123` reports `Text`; (c) a
  formula cell exposes `CachedValue` without being evaluated; (d) `#N/A` reports an error type. Record the outcome in
  the PR description; if (a) contradicts the design, STOP and report before implementing (the conversion table in
  DD-10 then needs a number-format check). (DD-10, design Open Questions)
- [x] 3.2 Create a shared test helper `tests/…Integration/Import/XlsxFixtures.cs` that produces XLSX bytes from the REAL
  `ClosedXmlReportWriter` for BOTH report types: (a) expense-list report from real `ExportExpenseListReportUseCase`
  inputs; (b) month-summary report with Expense and Income rows; data covers DOP and USD, a null category (written as
  `""`), an income with an empty category, a null `AmountDOP`, an amount with 4 decimals. Nothing here builds the
  workbook by hand for the round-trip assertions (hand-built workbooks are used only for the sheet/formula/error cases
  in 3.3). (R-21, S-23)
- [x] 3.3 Write failing `tests/…Integration/Import/ClosedXmlTableReaderTests.cs` against the REAL reader: (a) REAL
  expense-list and month-summary output: sheet `Data` chosen, headers exactly the Phase-18 set, `Date` cells
  `RawCellKind.Date` as `yyyy-MM-dd`, `Amount` cells `Number` and equal to the source decimals (4-place round),
  `Currency`/`Type`/`Category` `Text`, empty category cell is `("", Text)` (S-8, S-23, R-7); (b) `HeaderMapper.AutoMap`
  over the real headers is complete with `AmountDOP` ignored (R-9, R-21); (c) sheets `Other` + `Data` read `Data`;
  sheets `A` + `B` read `A`; `data` lowercase matches case-insensitively (S-8, R-8); (d) a `@` text cell `0123`
  stays text; formula cell uses `CachedValue`; `#N/A` becomes `RawCellKind.Error` (C-16); (e) `OverflowException` on a
  huge double becomes an `Error` cell; (f) header row = first non-blank row of the used range; empty sheet →
  `NoRows`; 51 columns and 50,001 data rows → `TooLarge` without materializing (R-6). (R-7, R-8, S-8, S-23, C-4,
  C-16)
- [x] 3.4 Write failing ZIP-guard tests: (a) **zip bomb** — an XLSX-shaped ZIP whose entry declares a small length but
  inflates past `MaxXlsxUncompressedBytes` (crafted in the test) → `TooLarge/zipTooLarge`, and ClosedXML is never
  constructed (assert via a thrown-on-use seam or by timing/memory bound, named in the test); (b) more than
  `MaxXlsxEntries` entries → `TooLarge`; (c) a random non-workbook ZIP → `UnreadableFile/notAWorkbook`; a truncated
  ZIP → `UnreadableFile/notAWorkbook`, never an unhandled exception; (d) declared sizes are not the only defense:
  the entry is decompressed through a counting stream. (R-7, S-7)
- [x] 3.5 Write failing `SniffingTabularReaderTests` against the REAL `SniffingTabularReader` composing the real CSV
  and XLSX readers: XLSX bytes (real writer output) are read by the XLSX path regardless of any name; CSV bytes go
  to the CSV path; `D0 CF 11 E0` (OLE/legacy `.xls`) → `UnreadableFile/legacyXls`; `PK\x03\x04` garbage →
  `notAWorkbook`; the reader has no file-name input at all (S-2 asserted by construction and by feeding an XLSX and a
  CSV in swapped "names" through the loader-level helper). (R-2, S-2)
- [x] 3.6 Create `src/…Infrastructure/Import/ClosedXmlTableReader.cs` per DD-10 (ZIP guard with counting stream before
  `new XLWorkbook`, sheet selection, header row, cell conversion table, `decimal.Round(…, 4, AwayFromZero)`, body in
  `Task.Run`). Run 3.3 and 3.4 green. (R-7, R-8, DD-10)
- [x] 3.7 Create `src/…Infrastructure/Import/SniffingTabularReader.cs` (the only `ITabularReader` implementation to be
  registered; takes the two concrete readers). Run 3.5 green. (R-2, DD-10)
- [x] 3.8 Architecture check: ClosedXML types appear only under `src/…Infrastructure`; the existing
  `ArchitectureTests` ClosedXML rules stay green (the Infrastructure-location test for the new types is added in S5).
  (R-25)
- [x] 3.9 Run all four gates plus the no-stubs grep over `src/ControlDeFinanzasIA.Infrastructure/Import` and the S3
  test files. Confirm `ClosedXmlTableReaderTests`, `SniffingTabularReaderTests`, `ClosedXmlBehaviorAssumptionsTests`
  executed. Run the FULL suite 3 consecutive times and record intermittent failures by test name. (R-25)
> Apply notes (S3): ClosedXML 0.105.1 CONFIRMED that a numeric cell with a date number format reports  > `XLDataType.DateTime` on reload (yyyy-mm-dd and dd/mm/yyyy), plain numbers stay Number, `@` text keeps `0123`,  > formula cells expose `CachedValue` (never evaluated), `t="e"` cells report Error; DD-10 needs no number-format  > check. Deviations: (1) tests were written after the production code for 3.3-3.7 (only 3.1 came first), so no red  > run was observed for them; (2) XlsxFixtures builds the reports from real DTOs through the real writer, not through  > the use cases; (3) the reader has a public `ClosedXmlTableReader(Action)` seam invoked right before ClosedXML opens  > the workbook (Infrastructure has no InternalsVisibleTo); (4) a header that lies about its size is neutralised by the  > .NET inflater, which yields at most the declared bytes, so it surfaces as `notAWorkbook`, not `zipTooLarge`;  > (5) ClosedXML rejects text above 32,767 chars, so `fieldTooLong` is a backstop and the over-limit file is  > `UnreadableFile`; (6) rows come from `CellsUsed()` (RowsUsed took 38 s on a cell at row 1,048,576);  > (7) `AddImportReaders()` now binds `ITabularReader` to `SniffingTabularReader`.

## 4a. Row validator, category matcher and duplicate detector (S4a)

Depends on 3. Goal: the pure decision logic is complete and provably consistent with the domain invariants. Size note:
if it exceeds ~450 lines, split at the seam "validator" vs "matcher + duplicates".

- [ ] 4a.1 Write failing `tests/…Unit/Import/ImportRowValidatorTests.cs` against the REAL `ImportRowValidator.Parse`
  (with real `AmountParser`/`DateParser`): (a) one test per rejection reason code proving it fires and the row is
  `IsRejected` (`CellError`, `MissingDate`, `InvalidDate`, `DateBefore1900`, `MissingAmount`, `InvalidAmount`,
  `ZeroAmount`, `NegativeAmount`, `TooManyDecimals`, `AmountTooLarge`, `MissingDescription`, `DescriptionTooLong`,
  `InvalidType`, `UnsupportedCurrency`); (b) **all reasons collected**, not first-fail (bad date + bad amount + empty
  description gives three); (c) column count: trailing empty fields beyond the header count are dropped, any other
  mismatch → `ColumnCountMismatch` and NOTHING else evaluated (`Row_ShiftedColumns_Rejected`) (C-9); (d) description
  201 chars → `DescriptionTooLong` and never truncated, 200 chars valid, whitespace trimmed; (e) type synonyms
  `Expense`/`Gasto`/`Egreso`/`Income`/`Ingreso` in any case and accents; empty type → default + `TypeDefaulted`;
  unmapped type → default silently; `Foo` → `InvalidType`; (f) sign modes: `NegativeIsExpense` gives `-50` an expense of
  50 and `50` an income and IGNORES the Type column; `Unsigned` with `-50` → `NegativeAmount` (never a silent abs)
  (C-13); zero rejected in every mode; parentheses negative; (g) currency: unmapped → default silently; empty with
  mapped column → default + `CurrencyDefaulted`; `dop`/`usd` accepted; `EUR`, `1`, `0`, `DOP,USD` →
  `UnsupportedCurrency` (`Currency_Numeric_Rejected`, C-12); (h) future date accepted with `FutureDate` warning via an
  `IClock`-derived `today` argument (S-14); (i) `AmountWasAmbiguous` propagated from the parser. (R-10, R-11, S-12,
  S-13, S-14, C-9, C-12, C-13, C-15)
- [ ] 4a.2 Write failing `ImportRowValidatorDomainConsistencyTests`: a boundary table (200-char description, 1-char
  description, `1900-01-01`, `0.0001`, `MaxAmount`, both currencies, both types) proves that every row the validator
  ACCEPTS builds with the REAL `Expense.Create` / `Income.Create` without throwing, and every row it REJECTS for
  amount/date/description would have thrown there. (R-10, DD-6, D-22)
- [ ] 4a.3 Write failing `tests/…Unit/Import/CategoryMatcherTests.cs` against the REAL `CategoryMatcher.Match`: ordinal
  beats ignore-case beats folded; `comida` matches `Comida`; `alimentacion` matches `Alimentación`; two catalog entries
  folding to the same key (`Educación`, `EDUCACION`) → `Ambiguous` for `educacion` (S-15, C-14); ordinal exact match
  still wins even when a folded twin exists; `FoldKey` is `CategoryNameNormalizer.Fold` (no re-implementation); a fold
  vs SQLite `NOCASE` test proves every pair `NOCASE`-equal shares a `FoldKey` (so a created name can never violate
  the unique index) (R-14). The resolution table of DD-7 as a `[Theory]` over the planner-facing resolver:
  expense empty/unmapped → `FallbackCategoryId` + `CategoryFallback`; fallback missing from the catalog →
  `NoFallbackCategory`; `Found` → Id; `Ambiguous` → `AmbiguousCategory`; `NotFound` + creation on → new category keyed
  by `FoldKey` with collapsed inner whitespace; name > 100 chars → `CategoryNameTooLong` (never truncated); the 51st
  distinct new key → `TooManyNewCategories`; duplicate spellings in one batch create ONE (first spelling wins);
  expense `NotFound` + creation off → fallback + `CategoryFallback`; income empty → null, no warning (S-17);
  income `NotFound` + creation off → null + `CategoryDropped`; payment method `Found` → Id, empty → null, `NotFound`/
  `Ambiguous` → null + `PaymentMethodNotFound`, never created (S-18). (R-14..R-16, S-15..S-18, C-14, C-15)
- [ ] 4a.4 Write failing `tests/…Unit/Import/DuplicateDetectorTests.cs` against the REAL `DuplicateDetector`:
  `Fingerprint` equality for `12.5` vs `12.50` vs `12.5000` (canonical `0.0000`); description differing only by case
  or whitespace runs is equal, differing by diacritics is NOT (conservative key); `categoryKey` variants (`Guid "N"`,
  `new:<fold>`, `""`); **occurrence counting** `ClassifyDuplicates`: file has 2 identical rows with 0 existing → both
  imported; 1 existing → first is a duplicate, second imported; 2 existing → both duplicates; a re-import (M = N) →
  all duplicates; rejected rows do not consume occurrences; `SkipDuplicates = false` makes them `Valid` +
  `DuplicateImported`; the two sides use the same 4-decimal `decimal`. (R-17, S-19, C-17, C-18)
- [ ] 4a.5 Create `src/…Application/UseCases/Import/Parsing/ImportText.cs` (trim, whitespace collapse, wrapping
  quote/trailing punctuation handling shared by the matcher and the description key) with direct tests inside 4a.3 and
  4a.4. (DD-7, DD-8)
- [ ] 4a.6 Create `Parsing/ImportRowValidator.cs` (`Parse`, `ParsedRow`, order of DD-6, literal currency match, never
  `Enum.TryParse`) and run 4a.1 and 4a.2 green. (R-10, R-11, DD-6)
- [ ] 4a.7 Create `Parsing/CategoryMatcher.cs` (`Match`, `CatalogMatch`, `FoldKey`, the DD-7 resolver) and run 4a.3
  green. (R-14..R-16, DD-7)
- [ ] 4a.8 Create `Parsing/DuplicateDetector.cs` (`Fingerprint`, `ClassifyDuplicates`, plan-hash helper) and run 4a.4
  green. (R-17, DD-8)
- [ ] 4a.9 Purity check as in 1.12 over `Parsing/`: no clock, culture, I/O, `Enum.TryParse` for currency/type.
  (R-1)
- [ ] 4a.10 Run all four gates plus the no-stubs grep over `src/ControlDeFinanzasIA.Application/UseCases/Import` and
  `tests/ControlDeFinanzasIA.Tests.Unit/Import`. Confirm N > 0 for the four new test classes. Run the FULL suite 3
  consecutive times and record intermittent failures by test name. (R-25)

## 4b. Lookup, planner and preview (S4b)

Depends on 4a. Goal: `PreviewImportUseCase` produces an honest dry run against a real SQLite database and never writes.

- [ ] 4b.1 Create `tests/…Integration/Import/ImportTestDb.cs` (mirrors `BackupTestDb`): `<temp>\import-<guid>\data.db`
  migrated with `Database.Migrate()`, a real `ServiceCollection` with `AddPersistence(TestPathResolver)` so
  `IServiceScopeFactory` is genuine, seed helpers for categories/expenses/incomes/payment methods, and `Dispose` doing
  `SqliteConnection.ClearAllPools()` plus recursive delete. Never touches the real AppData. Create
  `tests/…Unit/TestDoubles/{FakeTabularReader,FakeImportLookup}.cs` (fakes only for collaborators; used where the
  component under test is NOT the reader/lookup). (R-26)
- [ ] 4b.2 Write failing `tests/…Integration/Import/EfImportLookupTests.cs` against the REAL `EfImportLookup`: date
  span filter includes both bounds and excludes outside rows; expenses AND incomes returned as
  `ExistingTransactionKey` with amount at 4 places; categories and payment methods returned; the migrated DB has the
  seeded "Otros" with Id `11111111-1111-1111-1111-000000000009` and `IsSystem = true` (guards the duplicated
  `FallbackCategoryId` constant); null min/max → no existing-row query; **fresh scope**: the lookup works when the
  ambient/shared `AppDbContext` is disposed and never reads through a supplied context; a thrown query error maps to
  `Result.Failure(LookupFailed)` and is logged. (R-19, S-15, DD-11, D-15, D-16, D-23)
- [ ] 4b.3 Write failing `tests/…Integration/Import/PreviewImportUseCaseTests.cs` against the REAL
  `PreviewImportUseCase` with the REAL `SniffingTabularReader`, `EfImportLookup` and `ImportTestDb`: (a) S-1 happy path
  (3 valid rows, auto-mapped, no mapping step, counts, `PlanHash` present); (b) counts per outcome (valid, duplicate,
  rejected, warning) on a mixed file; (c) **`Preview_ProblemRowsBeyondSample`**: a problem row at row 5,000 appears in
  `ProblemRows` while `SampleRows` holds exactly the first 50 (C-21); (d) `ReasonCounts` sum matches; (e)
  `CategoriesToCreate` lists the unknown names once each and honors the cap 50 (S-16); (f) **`Xlsx_NumberCell_
  IgnoresCommaStyle`**: an XLSX from the real writer with `DecimalStyle.Comma` still yields the exact source amounts
  (C-5); (g) `PlanHash` is identical across two previews of the same bytes/options/DB and changes when an expense is
  added or an option flips; (h) mapping incomplete (`When,What,How much`) returns a success preview with
  `MissingFields`, no evaluated rows (S-9); (i) all rows invalid → success with `ToImportCount = 0` (S-22, R-20); (j)
  `UnreadableFile`, `TooLarge`, `NoRows` surface as `Result.Failure` with the right codes (S-28); (k) **the preview
  performs no writes**: row counts of every table and the live file SHA-256 are unchanged after the call; (l) a
  user-supplied mapping with an out-of-range or reused index → `MappingIncomplete`; (m) decimal and date evidence
  fields are populated for the ambiguous cases. (R-9, R-17, R-18, R-20, S-1, S-9, S-16, S-22, S-28, C-5, C-21)
- [ ] 4b.4 Create `src/…Infrastructure/Import/EfImportLookup.cs` per DD-11 (Singleton, `IServiceScopeFactory`,
  `Task.Run` + `CreateAsyncScope`, three `AsNoTracking` projections). Run 4b.2 green. (R-19, DD-11)
- [ ] 4b.5 Create `src/…Application/UseCases/Import/{ImportPipeline,ImportPlanner,ImportPlan}.cs` per DD-11/DD-8
  (mapping check, `Detect` → resolved style, `ImportRowValidator.Parse`, min/max date, lookup, `CategoryMatcher`,
  cap 50, fingerprints, occurrence counting, PlanHash from `row#:status:fingerprint|reasons` plus ordered new
  category names). (R-17, R-18, DD-11)
- [ ] 4b.6 Create `src/…Application/UseCases/Import/PreviewImport/PreviewImportUseCase.cs` (returns `ImportPreview`,
  success-shaped for row outcomes, `Result.Failure` only for file-level codes and `LookupFailed`, catches every
  non-OCE exception and logs). Run 4b.3 green. (R-18, R-20, DD-12)
- [ ] 4b.7 Run all four gates plus the no-stubs grep over the slice's `src` and test files. Confirm N > 0 for
  `EfImportLookupTests` and `PreviewImportUseCaseTests`. Run the FULL suite 3 consecutive times and record
  intermittent failures by test name. (R-25)

## 5. Materializer, commit and atomic writer (S5)

Depends on 4b. Goal: `CommitImportUseCase` re-runs the same pipeline over the retained bytes, refuses a stale preview,
builds domain entities, and `EfImportWriter` persists everything in ONE transaction or nothing, without ever touching
the shared UI `AppDbContext`. Size note: this is the largest slice; if it exceeds ~450 lines split at the seam
"materializer + commit use case" vs "writer + timing + arch guard".

- [ ] 5.1 Write failing `tests/…Unit/Import/ImportMaterializerTests.cs` against the REAL `ImportMaterializer.Build`:
  each `Valid` row builds via `Expense.Create` / `Income.Create` with `note: null`, `source: null`, resolved
  category/payment-method ids and `Money.Create(amount, currency)`; `Duplicate` and `Rejected` rows are excluded;
  new `Category` has a fresh Id, trimmed name, `Color = "#95A5A6"`, `Icon = null`, `IsSystem = false`,
  `CreatedAt = UpdatedAt = utcNow` from the `IClock` fake; an income with null category builds; a forced
  `ArgumentException` (constructed by handing the materializer a plan row the validator would have rejected) surfaces
  as an exception the use case maps, never a partially built batch. (R-15, R-19, S-16, DD-12, Reconciliation #10)
- [ ] 5.2 Write failing `tests/…Integration/Import/EfImportWriterTests.cs` against the REAL `EfImportWriter` on
  `ImportTestDb`: (a) happy path inserts categories + expenses + incomes and `SaveChangesAsync` is called exactly once
  (count via an `IInterceptor`/`DbCommandInterceptor` or `SavingChanges` event registered on the scope's context
  through the DI seam); (b) **forced failure** via a real SQLite trigger created in the test DB: `CREATE TRIGGER
  boom BEFORE INSERT ON Expenses WHEN NEW.Description = 'BOOM' BEGIN SELECT RAISE(ABORT,'forced'); END` on the LAST
  row of a batch that also creates categories and incomes → `Result.Failure(WriteFailed)`, and afterwards 0 new
  expenses, 0 new incomes and 0 new categories (row counts of all three tables equal the pre-call values) (S-21,
  C-20); (c) a unique-index violation (a new category whose name collides `NOCASE` with an existing one) fails the
  whole batch with the same zero-rows assertion; (d) **shared context not poisoned (C-20)**: an `AppDbContext`
  resolved from the ROOT provider is created before the call, the failing write runs, then the test saves an
  unrelated change on that shared context and asserts NONE of the import entities were persisted and its
  `ChangeTracker` holds no import entity; (e) cancellation token passed to `WriteAsync` is ignored once saving starts
  (the writer uses `CancellationToken.None`); (f) `AutoDetectChangesEnabled` is off on the scope's context while
  staging. (R-19, S-21, C-20, DD-13)
- [ ] 5.3 Write failing `tests/…Integration/Import/CommitImportUseCaseTests.cs` against the REAL `CommitImportUseCase`,
  `ImportPipeline`, `EfImportLookup` and `EfImportWriter` (fakes only for `IClock`): (a) S-1 commit inserts the
  expected rows and returns counts; (b) **`Commit_DbChangedAfterPreview_Stale`**: an expense added between preview and
  commit changes the plan hash → `Result.Failure(PreviewStale)` and nothing written (C-19); (c) a different options
  instance/flag than the preview → `PreviewStale`; (d) commit RE-PARSES the retained bytes with the confirmed
  options: a fake `ITabularReader` counter asserts the reader ran again at commit (this fake wraps the real reader,
  it does not replace it) (S-20, R-18); (e) **re-import inserts 0**: importing the same CSV twice → second commit
  reports all duplicates and the tables are unchanged (S-19, C-17, C-18); (f) two identical rows against an empty
  DB import both; against a DB with one, one is skipped; with `SkipDuplicates = false` all import (S-19); (g)
  an `ArgumentException` in the materializer → `WriteFailed` and the writer is never called (assert with a spy
  wrapping the real writer); (h) all rows rejected → success with zero counts and nothing inserted (S-22, R-20); (i)
  mapping incomplete → `MappingIncomplete`; (j) any DB failure after rollback returns `Failed`-class `WriteFailed`
  with no partial data; (k) categories created only when the option is on, deduplicated by fold, cap 50 with the 51st
  falling to the creation-off path, unmatched expense with creation off uses "Otros" by Id (S-16, R-15). (R-15,
  R-18..R-20, S-1, S-16, S-19..S-22, C-17, C-18, C-19)
- [ ] 5.4 **50,000-row timing test and decision gate.** Write `tests/…Integration/Import/ImportPerformanceTests.cs`:
  generate a 50,000-row CSV in memory (valid rows, a bounded set of categories), run the REAL commit through DI in
  Release and assert (a) it completes within the documented ceiling of 30 s, (b) exactly one `SaveChangesAsync`
  occurred, (c) 50,000 expenses exist, (d) the calling thread is released (the call is awaited from a test that also
  observes the sync context is not blocked; `Task.Run` used). The test logs the measured time. **Decision gate
  recorded in the apply notes with the measured number:** if the ceiling passes, keep "single SaveChangesAsync"; if it
  fails, switch `EfImportWriter` to one explicit `BeginTransactionAsync` with chunked `SaveChangesAsync` +
  `ChangeTracker.Clear()`, update Reconciliation #19 wording in docs/notes ("one transaction"), and keep the
  forced-failure tests of 5.2 green (all-or-nothing must still hold, including a failure in a LATER chunk). If the
  ceiling had to be relaxed, say so and why; never silently raise it. (R-19, R-26, S-29, DD-13, Reconciliation #19)
- [ ] 5.5 Create `src/…Application/UseCases/Import/ImportMaterializer.cs` and run 5.1 green. (DD-12)
- [ ] 5.6 Create `src/…Application/UseCases/Import/CommitImport/CommitImportUseCase.cs` per DD-12 (build plan → mapping
  check → plan-hash comparison → materialize → `writer.WriteAsync(batch, CancellationToken.None)`; every non-OCE
  exception caught, logged and mapped to `LookupFailed`/`WriteFailed`; OCE only before the write). Run 5.3 green.
  (R-18..R-20, DD-12)
- [ ] 5.7 Create `src/…Infrastructure/Import/EfImportWriter.cs` per DD-13 (fresh `AsyncServiceScope`, `Task.Run`,
  `AutoDetectChangesEnabled = false`, `AddRange`, one `SaveChangesAsync(CancellationToken.None)`, map
  `DbUpdateException`/`SqliteException`/`InvalidOperationException` to `WriteFailed`). Run 5.2 and 5.4 green. Existing
  repositories are NOT used or modified: `git diff` shows no change to any `*Repository.cs`. (R-19, DD-13, D-6)
- [ ] 5.8 Create `src/…Infrastructure/Import/ImportServiceCollectionExtensions.cs` (`AddImport`: `TryAddSingleton` for
  `CsvTableReader`, `ClosedXmlTableReader`, `ITabularReader → SniffingTabularReader`, `IImportLookup`, `IImportWriter`)
  and `ImportServiceCollectionExtensionsTests`: resolves every port as a Singleton, calling `AddImport` twice is
  idempotent, and the singletons hold only `IServiceScopeFactory` (no captured scoped context). (R-25, DD-13)
- [ ] 5.9 Append to `tests/…Unit/Architecture/ArchitectureTests.cs` (D-26/DD-15): `CsvTableReader`,
  `ClosedXmlTableReader`, `SniffingTabularReader`, `EfImportLookup`, `EfImportWriter` reside in
  `ControlDeFinanzasIA.Infrastructure.Import`; no Application/UI type depends on that namespace outside the
  composition root; the existing ClosedXML rules and R-1..R-5 stay green. (R-25)
- [ ] 5.10 Confirm no new package/migration/setting: `git diff --name-only` for the slice contains no
  `Directory.Packages.props`, `.csproj` (beyond the S2 fixtures item), `Migrations/`, `SettingKeys` or `AppSettings`.
  (R-25)
- [ ] 5.11 Run all four gates plus the no-stubs grep over the slice's `src` and test files (must also return nothing for
  `Task\.FromResult\(Result\.Failure` in `EfImportWriter`/`CommitImportUseCase`). Confirm N > 0 for the new classes and
  that `ImportPerformanceTests` ran in Release. Run the FULL suite 3 consecutive times and record intermittent
  failures by test name (the timing test is the likeliest intermittent: report its 3 measured times). (R-25)

## 6a. ImportViewModel and localized strings (S6a)

Depends on 5. Goal: the whole import flow is testable without a UI, and every reason/error code has a localized message
in both locales. Size note: if it exceeds ~450 lines, split at the seam "strings + completeness" vs "ViewModel".

- [ ] 6a.1 Append the `Import*` keys to `src/…UI/Resources/Strings.resx` (neutral English) and `Strings.es-DO.resx`
  (neutral professional Spanish) and add the **manual** accessors to `Strings.Designer.cs` (the file is hand-maintained;
  do not regenerate it): the DD-14 list — banners, `ImportField*` ×7, `ImportDecimalStyle*` ×3, `ImportDateFormat*` ×3,
  `ImportSignMode*` ×2, option labels, summary/status/tag/backup-recommendation/confirm/cancel/close/working/done/
  stale keys, `ImportError*` (`Failed`, `Unreadable`, `MalformedCsv` with `{0}` line, `LegacyXls`, `Binary`,
  `TooLarge` with the limits in the text, `NoRows`, `PreviewStale`, `LookupFailed`, `WriteFailed`,
  `MappingIncomplete`), and `ImportReason<Name>` for EVERY `ImportReason` value. Append only; no existing key touched.
  (R-24, R-22, DD-14)
- [ ] 6a.2 Extend the existing resx completeness test (do not add a parallel weaker one): every neutral key exists in
  es-DO and vice versa, no empty values, including the new keys; add `ImportResourcesTests`: reflection over
  `Enum.GetValues<ImportReason>()` proves `Strings.ResourceManager.GetString("ImportReason" + name, culture)` is
  non-empty for `es-DO` and `en`; every `ImportErrorCodes` file-level code and every `Reason*` detail maps to a
  non-empty message in both cultures; the Designer accessor exists for each new key (reflection over `Strings`
  properties vs resx keys); the `{0}` placeholder is present in `ImportErrorMalformedCsv` for both cultures. (R-24,
  S-27)
- [ ] 6a.3 Write failing `tests/…Unit/UI/ImportViewModelTests.cs` against the REAL `ImportViewModel` with fake picker,
  fake use-case collaborators (interfaces are the use cases' own ports: fakes of `ITabularReader`/`IImportLookup`/
  `IImportWriter` feed the REAL `PreviewImportUseCase`/`CommitImportUseCase`) and `FakeNotificationCenter`:
  (a) **cancelled picker** (`null`) → `CloseRequested` raised, no message, `IsError` false, no use case called
  (verify the fake reader call count is 0), state stays `Idle` (S-24); (b) load failure per file-level code and
  reason (`UnreadableFile` × `malformedCsv`/`legacyXls`/`binary`/other, `TooLarge`, `NoRows`) → the exact localized
  text under es-DO and en, never raw exception text, state does not reach `Previewed` (S-28, R-24); (c) auto-complete
  mapping → `Previewed` with no mapping step; incomplete (`When,What,How much`) → `Mapping`, Confirm disabled until
  Date/Description/Amount are mapped (S-9); (d) any option/column change sets `IsPreviewStale`, cancels the previous
  CTS and triggers ONE debounced re-preview (`PreviewDebounce = TimeSpan.Zero`, rapid changes collapse); the re-preview
  reuses the retained bytes and never re-opens the picker; changing the delimiter override re-parses from retained
  bytes (R-5, S-5); (e) Confirm disabled while stale, busy, or `ToImportCount == 0` (S-22); (f) **busy guard**: two
  concurrent `ExecuteAsync` calls on `ChooseFile` and on `Confirm` (dialog / commit blocked on a TCS) produce exactly
  one dialog call and one commit; commands called in `Loading`/`Importing` return immediately (S-25, R-22); (g)
  `PreviewStale` from commit → re-preview + `ImportStaleMessage`, returns to `Previewed`, nothing written (C-19); (h)
  success → `Completed` raised once with counts BEFORE `CloseRequested`, `TryRefreshAsync` invoked once as
  fire-and-forget, and a throwing `TryRefreshAsync` is swallowed (S-26); (i) commit failure → mapped message, state
  back to `Previewed`, never stuck in `Importing`; (j) any unexpected exception → `ImportErrorFailed`, logged,
  state returns to a non-busy state; `OperationCanceledException` during `Loading` is silent; (k) `Cancel` from
  `Mapping`/`Previewed`/`Loading` cancels the CTS, drops the retained source and raises `CloseRequested`; (l) date
  banners: `DateEvidence.AmbiguousOnly` and `Contradicts` and the decimal-conflict/ambiguous flags expose the
  matching banner strings (C-6); (m) the default date format follows the UI culture (es → DayMonthYear, en →
  MonthDayYear); (n) the `ImportErrorKey(code, reason)` mapper covers every code and defaults to `ImportErrorFailed`;
  (o) the confirm panel exposes the backup recommendation copy but Confirm is not gated on it (copy only). (R-5, R-22,
  R-24, S-5, S-9, S-22, S-24..S-28, C-6, C-19)
- [ ] 6a.4 Create `src/…UI/ViewModels/ImportViewModel.cs` (+ `ImportRowItem`, `ColumnChoice`, `ImportPanelState`,
  `ImportCompletedEventArgs`) per DD-14: Transient; `[ObservableProperty]` state with `NotifyCanExecuteChangedFor` on
  every command; each command starts with a synchronous `State` check before its first `await`;
  `try/catch/finally`; `Completed` then `CloseRequested`; `_previewOptions` is the exact instance used for the last
  preview and is the one passed to commit; the picker is called with `[csv, txt, xlsx]` filters via the overload
  added in S6b (until then the ViewModel targets the interface member added additively in 6b.1: see the ordering note
  below). No logic in code-behind, no hardcoded UI strings. Run 6a.3 green. (R-22, R-24, DD-14)
  **Ordering note:** because the list-overload of `IFileOpenDialog` is added in S6b, 6a.4 depends on it compiling.
  Add the interface member and the `FakeFileOpenDialog` overload as the FIRST commit of this slice (a five-line
  additive change described in 8.1), and let S6b add only the Avalonia implementation. If that split is undesirable,
  swap so the overload lands in S6a; record which was chosen in the apply notes.
- [ ] 6a.5 Register nothing in `Bootstrap` yet (S6b). `ImportViewModel` is constructed directly in tests. (DD-15)
- [ ] 6a.6 Run all four gates plus the no-stubs grep over `src/ControlDeFinanzasIA.UI/ViewModels`, the resx/Designer
  additions and the slice's tests. Confirm N > 0 for `ImportViewModelTests` and `ImportResourcesTests`. Run the FULL
  suite 3 consecutive times and record intermittent failures by test name. (R-24)

## 6b. Picker overload, Expenses hosting and DI (S6b)

Depends on 6a. Goal: the Expenses screen can host the import flow with no change to existing fixtures.

- [ ] 6b.1 Write failing tests then implement the additive overload: `IFileOpenDialog.PickAsync(IReadOnlyList<
  FileTypeFilter> filters, string? title, CancellationToken)`; the existing single-filter member delegates to it with
  `[filter]` and keeps its behavior byte-identical (Phase-19 restore tests stay green untouched). `FakeFileOpenDialog`
  gains the overload, `LastFilters`, and `LastFilter = filters[0]`; `FakeFileOpenDialogTests` prove delegation and
  recording. Update EVERY other implementer of `IFileOpenDialog` found by `rg -n "IFileOpenDialog"` (all fakes) in the
  same slice. (R-23, S-2, DD-14, D-20)
- [ ] 6b.2 Modify `src/…UI/Services/AvaloniaFileOpenDialog.cs`: move the body to the list overload; with more than one
  filter prepend one `FilePickerFileType(Strings.FileDialogAllSupportedTypes)` holding every pattern; the single-filter
  overload delegates. Add the picker strings (`FileDialogAllSupportedTypes`, `ImportFileTypeCsv`, `ImportFileTypeText`,
  `ImportFileTypeExcel`, `ImportDialogTitle`, `ImportButton`) if not already added in 6a.1, resx ×2 + Designer, under
  the completeness test. The native dialog is verified by hand in the Cierre (no headless harness); a unit test asserts
  the pattern list the implementation builds through an extracted pure builder. (R-23, R-24)
- [ ] 6b.3 Write failing `tests/…Unit/UI/ExpensesViewModelImportTests.cs`: (a) the existing `ExpensesViewModel`
  fixtures that omit `import` still compile unchanged and `OpenImportCommand.CanExecute` is false; (b) with an
  `ImportViewModel`: `OpenImport` sets `IsImportOpen = true` and drives `Import.ChooseFileCommand` once; not executable
  while `IsImportOpen` or `IsExporting`; (c) `CloseRequested` sets `IsImportOpen = false`; (d) `Completed` triggers ONE
  categories reload and ONE list refresh (assert with the existing fake repositories' call counts) (S-26); (e)
  subscribing happens once in the constructor (no double reload after reopening). Verify `ExpensesViewModel` keeps its
  existing 16 required + 3 optional parameters in order with `ImportViewModel? import = null` appended as the LAST
  one. (R-22, S-26, DD-14, D-14)
- [ ] 6b.4 Modify `src/…UI/ViewModels/ExpensesViewModel.cs`: append the optional ctor parameter, `Import`,
  `[ObservableProperty] bool _isImportOpen`, `[RelayCommand(CanExecute = nameof(CanOpenImport))] OpenImportAsync`,
  the constructor subscriptions to `CloseRequested`/`Completed`. Do not touch existing members. Run 6b.3 green. (R-22)
- [ ] 6b.5 Append the `Bootstrap.cs` block (`AddImport`, `ImportPipeline`, `PreviewImportUseCase`,
  `CommitImportUseCase`, `ImportViewModel` Transient) and add `ImportCompositionTests` against the REAL `Bootstrap`
  provider: resolves `ImportViewModel`, both use cases, and every `AddImport` port; `IFileOpenDialog` is the Phase-19
  registration; `ExpensesViewModel` resolves with a non-null `Import`; `ITabularReader` resolves to
  `SniffingTabularReader`. The test resolves objects but never opens a database on the real profile
  (`CONTROLDEFINANZASIA_DATA_DIR`/test path resolver). (R-25, DD-15)
- [ ] 6b.6 **Smoke launch on a throwaway data directory** (see the safety rule at the top): set
  `CONTROLDEFINANZASIA_DATA_DIR` to a scratchpad dir, `dotnet run --project src/ControlDeFinanzasIA.UI`, confirm the app
  starts and Expenses opens with no unhandled exception, then kill it. Record that the real
  `%APPDATA%\ControlDeFinanzasIA` listing is identical before/after. The Import button is not yet visible (view arrives
  in S7): say so. If tooling for screenshots is not available, record "NOT RUN BY HAND". (R-22, R-26)
- [ ] 6b.7 Run all four gates plus the no-stubs grep over the slice's files. Confirm every fixture that constructs
  `ExpensesViewModel` still compiles unchanged (full solution build) and N > 0 for `ExpensesViewModelImportTests`,
  `ImportCompositionTests`, `FakeFileOpenDialogTests`. Run the FULL suite 3 consecutive times and record intermittent
  failures by test name. (R-23, R-25)

## 7. Import view, end-to-end round trip and docs (S7)

Depends on 6b. Goal: the user can import from the Expenses screen, and the roadmap criterion is proven through real DI
with the REAL exporter.

- [ ] 7.1 Write failing `tests/…Integration/Import/ImportEndToEndTests.cs` using real DI (`AddPersistence` + `AddImport`,
  real `ClosedXmlReportWriter`, real `PreviewImportUseCase`/`CommitImportUseCase`): (a) seed database A → real
  `ExportExpenseListReportUseCase` to XLSX bytes → fresh migrated DB B → preview then commit → rows are
  field-identical on Date, Type (Expense only), Amount (4-place `decimal`, no drift from the double), Currency
  (DOP and USD), Description, category name (Reconciliation #14); a null category exports as `""` and imports as the
  "Otros" fallback WITH a warning; (b) the same for the real month-summary report: Expense AND Income rows, income
  empty category → `CategoryId = null` with no fallback warning; blank `AmountDOP` ignored; (c) a SECOND import of
  the same file into B inserts 0 rows (all duplicates); importing into A itself inserts 0 (C-18, R-17, S-23); (d) a
  generic es bank CSV fixture (`;` delimiter, Windows-1252, `1.234,56`, `dd/MM/yyyy`) with the decimal/date options set
  produces the expected rows; the same file with `Auto` + no date format is rejected row by row with `InvalidDate`
  (S-11, S-10); (e) the tables outside expenses/incomes/categories are byte-identical before/after (no unintended
  writes). (R-17, R-21, S-19, S-23, C-18)
- [ ] 7.2 Write failing `ImportViewModel` end-to-end test in the same file using the REAL use cases, real DB and real
  readers with only the picker faked: pick the real XLSX bytes → `Previewed` → Confirm → `Done`, `Completed` raised,
  row counts match, `TryRefreshAsync` (fake center) called once. (R-22, S-26)
- [ ] 7.3 Write `ImportViewXamlTests` (string scan of `ImportView.axaml` and `ExpensesView.axaml`, precedent
  `SettingsViewBackupBlockTests`): `ImportButton` exists once in the toolbar next to Export (`ColumnDefinitions=
  "*,Auto,Auto,Auto"`), the list block is wrapped so it hides while `IsImportOpen`, the `ImportView` sibling is bound
  to `Import`/`IsImportOpen`, `x:DataType="vm:ImportViewModel"`, all text via `Strings` bindings with zero hardcoded
  literals, no `Click=` handlers, and the code-behind (`ImportView.axaml.cs`) contains only `InitializeComponent`.
  (R-22, R-24)
- [ ] 7.4 Create `src/…UI/Views/ImportView.axaml(.cs)` per DD-14 (header with file name and source facts, encoding
  fallback banner with `SampleText`, decimal/date banners, options grid, seven-ComboBox mapping grid expanded in
  `Mapping` and collapsible in `Previewed`, summary strip, categories-to-create chips, preview `ItemsControl` of 50
  rows with status badge, `dd MMM yyyy` date, amount + currency, description, category + "new" tag, reasons, an "Only
  problems" toggle, reason counts, confirm panel with the backup recommendation, indeterminate `ProgressBar` bound to
  `IsBusy`, Done panel). Modify `ExpensesView.axaml`: toolbar grid to `*,Auto,Auto,Auto`, `ImportButton`
  (`ledger-btn`) in column 3, list block wrapped in a `StackPanel IsVisible="{Binding !IsImportOpen}"`, sibling
  `<views:ImportView DataContext="{Binding Import}" IsVisible="{Binding IsImportOpen}"/>`. No `ViewLocator` or route
  change. Invoke `.claude/skills/frontend-design` before finalizing; record what it changed (existing "Libro Mayor"
  tokens only). `dotnet build` shows no AVLN compiled-binding warnings. Run 7.3 green. (R-22, R-24)
- [ ] 7.5 **Smoke launch in both locales on a throwaway data directory** (safety rule at the top): launch twice with
  `CONTROLDEFINANZASIA_DATA_DIR` pointing at a scratchpad copy/empty DB (es-DO, then en), open Expenses, confirm the
  Import button and empty panel render; it is permitted to import a small throwaway CSV into the throwaway DB only.
  Record screenshots or console output and the before/after listing of the real profile. If screenshot tooling is
  unavailable, record "NOT RUN BY HAND". (R-22, R-26)
- [ ] 7.6 Docs: replace `ICsvImporter`/`CsvImporter`/`ImportCsvUseCase`/`CsvImportProfile` in `docs/architecture.md`
  §Import CSV with the shipped names (`ITabularReader`, `IImportLookup`, `IImportWriter`, `CsvTableReader`,
  `ClosedXmlTableReader`, `SniffingTabularReader`, `EfImportLookup`, `EfImportWriter`, `PreviewImportUseCase`,
  `CommitImportUseCase`, `ImportPipeline`, `ImportViewModel`), a NEW section describing the pipeline
  (`read → parse → lookup → plan → plan hash → commit`), the amount/date interpretation rules, the fresh-scope atomic
  commit and the outcome of the 50k decision gate; `docs/roadmap.md` §Fase 20 (mark criteria with evidence pointers,
  record the deferral of persisted mapping profiles as a documented roadmap deviation); create/update
  `spec/phases/phase-20/features.md` (the untracked folder already exists; read it first). Do not update `AGENTS.md`
  here: that is done in the Cierre after verify. (D-27, R-25)
- [ ] 7.7 Confirm no new package, migration, setting or schema change across the WHOLE phase diff (`git diff
  --name-only <phase base>^ HEAD` filtered by `Directory.Packages.props`, `.csproj` (only the fixtures item),
  `Migrations/`, `SettingKeys`, `AppSettings` is empty otherwise). (R-25)
- [ ] 7.8 Run all four gates plus the no-stubs grep across the ENTIRE phase diff (`rg -n
  "NotImplemented|TODO|stub|Task\.FromResult\(Result\.Failure" -g "*.cs" -g "*.axaml"` limited to the files changed in
  phase 20 returns nothing). Confirm N > 0 for `ImportEndToEndTests`, `ImportViewXamlTests`. Run the FULL suite 3
  consecutive times and record intermittent failures by test name. (R-25)

## 8. Cierre

- [ ] 8.1 Run the full gate suite across the whole phase: `dotnet build -c Release` (0 warnings/errors), `dotnet test -c
  Release --no-build` (unit + integration; record counts, confirm Import tests executed and none skipped),
  `dotnet format style --verify-no-changes` exit 0, NetArchTest R-1..R-5 plus the D-26 location guard, resx
  completeness. Record final counts, the 3-run stability result and every intermittent test name (or "none").
  (R-25, R-26)
- [ ] 8.2 Manual check on Windows with the REAL native open dialog and REAL files, on a COPY of the data only: copy
  `%APPDATA%\ControlDeFinanzasIA` to a throwaway folder and run the app pointed at it with
  `CONTROLDEFINANZASIA_DATA_DIR`; (a) the picker shows "All supported files" first, then CSV/TXT and XLSX filters; (b)
  cancel writes nothing and shows no message; (c) import a real CSV (bank export style) and a real XLSX (a Phase-18
  export from the copy) end to end; (d) re-import inserts 0; (e) a Windows-1252 CSV shows the encoding banner and
  sample. Record screenshots. If not performed, mark it "NOT RUN BY HAND" and leave it unticked. (R-2, R-23, S-2,
  S-24)
- [ ] 8.3 Visual check of the Import panel in BOTH locales (es-DO and en) on the throwaway copy: labels, banners,
  dates rendered with month names, long descriptions, mapping step layout, error states. If not performed, mark it
  "NOT RUN BY HAND" and leave it unticked. (R-24, S-27)
- [ ] 8.4 Update `AGENTS.md` project status (append-only within the Status block): Phase 20 shipped, test counts from
  8.1, verify verdict, the 50k decision outcome, and the pointers to `spec/features/phase-20/`, `tasks/phase-20/`,
  `.atl/sdd/f-020p20-import/`; update `docs/AI_HANDOFF.md` with the import behavior and the manual-check status.
  (R-25)
- [ ] 8.5 Run `sdd-verify`; resolve or explicitly record every critical/warning finding with a test-first fix against
  the real component (Phase 19 precedent). Do not tick this item while warnings are open without a note.
- [ ] 8.6 Archive: rename `spec/features/phase-20/F-020-import.spec.md` to `F-020-import.done.md` ONLY after archive,
  tick the Acceptance Criteria of the spec with evidence, and record the manual checks still NOT RUN BY HAND. Commit
  history uses conventional commits with no AI attribution trailers.

## Threat-table traceability (design C-1..C-22 → named test tasks)

| Row | Vector | Test task(s) |
|---|---|---|
| C-1 | `1.234`/`1,234` wrong decimal meaning | 1.3 (`AmountParserTests` table), 7.1(d) |
| C-2 | Mixed styles in one file | 1.3 (`Detect_Conflict_*`, `Parse_Contradicting_Rejected`) |
| C-3 | Invalid grouping, symbols, exponents | 1.3 |
| C-4 | >4 decimals silently rounded | 1.3 (`TooManyDecimals`), 3.3(a) (Number cells rounded to 4 only) |
| C-5 | XLSX number reinterpreted by decimal toggle | 4b.3(f) `Xlsx_NumberCell_IgnoresCommaStyle`, 1.3 (Number cell) |
| C-6 | Day/month swap | 1.4 (`DateParserTests`, evidence), 6a.3(l) (banners), 7.1(d) |
| C-7 | Two-digit years, Excel serials, time-zone shift | 1.4 |
| C-8 | Wrong text decoding | 2.1, 2.2(d) (`Csv_Encoding_*` with real byte fixtures) |
| C-9 | Wrong delimiter / shifted columns | 2.2(a) (`Csv_Sniff_*`), 4a.1(c) (`Row_ShiftedColumns_Rejected`) |
| C-10 | Unterminated/stray quote swallows rows | 2.2(e) (`Csv_UnterminatedQuote_*`) |
| C-11 | Header substring collision / duplicate headers | 1.2 (`HeaderMapperTests`), 3.3(b) |
| C-12 | `Enum.TryParse` currency | 4a.1(g) (`Currency_Numeric_Rejected`) |
| C-13 | Negative silently made positive | 4a.1(f) |
| C-14 | Wrong category silently picked | 4a.3 (`CategoryMatcherTests`, ambiguity) |
| C-15 | Description or name truncated | 4a.1(d), 4a.3 (`CategoryNameTooLong`) |
| C-16 | Excel error/formula cells | 3.3(d) (`Xlsx_ErrorCell_*`, `CachedValue`) |
| C-17 | Legit repeated rows dropped | 4a.4 (occurrence counting), 5.3(f) |
| C-18 | Re-import duplicates after round trip | 4a.4, 5.3(e), 7.1(c) |
| C-19 | Preview ≠ commit | 5.3(b)(c) `Commit_DbChangedAfterPreview_Stale`, 6a.3(g) |
| C-20 | Partial import / poisoned shared context | 5.2(b)(c)(d) trigger-forced failure + shared-context save |
| C-21 | Rejected rows hidden beyond first 50 | 4b.3(c) `Preview_ProblemRowsBeyondSample` |
| C-22 | Truncated/partial file read | 1.1 (`ImportSourceLoaderTests`) |

## Scenario coverage index (spec S-1..S-29)

S-1: 4b.3(a), 5.3(a) · S-2: 3.5, 6b.1 · S-3: 2.2(b)(c) · S-4: 2.2(d) · S-5: 2.2(a), 6a.3(d) · S-6: 1.1, 2.3 · S-7: 3.4 ·
S-8: 3.3(c) · S-9: 1.2, 4b.3(h), 6a.3(c) · S-10: 1.3 · S-11: 1.4, 7.1(d) · S-12: 4a.1 · S-13: 4a.1(e)(f) · S-14: 4a.1(h) ·
S-15: 4a.3 · S-16: 4a.3, 5.3(k) · S-17: 4a.3 · S-18: 4a.3 · S-19: 4a.4, 5.3(e)(f) · S-20: 5.3(d) · S-21: 5.2 ·
S-22: 4b.3(i), 5.3(h), 6a.3(e) · S-23: 3.3, 7.1 · S-24: 6a.3(a), 8.2 · S-25: 6a.3(f) · S-26: 6a.3(h), 6b.3(d), 7.2 ·
S-27: 6a.2, 8.3 · S-28: 4b.3(j), 6a.3(b) · S-29: 5.4.
