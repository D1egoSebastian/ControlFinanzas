# Tasks — F-019 Backup and Restore

> Governing inputs: `spec/features/phase-19/F-019-backup-restore.spec.md` (its final "Reconciliation with design"
> section governs where wording differs) and `.atl/sdd/f-019p19-backup/design.md` (per-slice sections binding;
> design notes S4 needed splitting and S6 is split as well). Requirement ids (R-n) and scenario ids (S-n) below
> refer to the spec. Design decisions are cited as DD-n (design) and D-n (proposal). Slice names S1..S7 follow the
> design; S4 becomes S4a/S4b and S6 becomes S6a/S6b.
> Strict TDD: within each slice, failing tests come first, then implementation.
>
> **Shared-file guard:** every touch on a shared file (`Bootstrap.cs`, `App.axaml.cs`, `Strings.resx`,
> `Strings.es-DO.resx`, `Strings.Designer.cs`, `ArchitectureTests.cs`, `SettingsView.axaml`, `AGENTS.md`) MUST be an
> appended block (the Backup block in `SettingsView.axaml` goes before "Acerca de"). Nothing existing may be
> reordered, renamed or restructured. `Directory.Packages.props` and every `.csproj` stay UNCHANGED (R-24).
>
> **Anti-stub rules (Phase 18 lesson, apply to every slice):**
> 1. A task that implements behavior names the concrete artifact (file + method) AND the test that proves it
>    against the REAL implementation (`SqliteBackupService` on a real temp SQLite file, `ScopedNotificationCenter`,
>    the real VM), never a fake of the same component. Fakes are allowed only for the collaborators around it.
> 2. No task may be ticked while its artifact contains `NotImplementedException`, `TODO`, `HACK`, `FIXME`, an empty
>    method body, a hard-coded return that mimics success, or a test with no assertion. A partially met task stays
>    unticked and gets a note.
> 3. Every slice ends with a "Run all four gates" item that includes the no-stubs grep (see 1.9 for the exact
>    command) over the files that slice touched. Non-run manual checks are recorded as "NOT RUN BY HAND", never
>    ticked as done.
> 4. Data-loss invariant used by every restore test: SHA-256 of the live `data.db` before the call equals the value
>    after it, no `data.db-journal` left by the operation, `backups\.work\` empty (R-10).
>
> **Smoke-launch safety rule (S5, S6a, S6b, S7):** any `dotnet run` smoke MUST set a throwaway data directory
> first (the env var / setting that `SystemPathResolver` honors, verified in 5.9) and MUST NOT run a restore
> against `%APPDATA%\ControlDeFinanzasIA\data.db`. If the override cannot be confirmed, the smoke is limited to
> "app starts, Settings opens, Backup block renders" with no button pressed, and the task says so.

## Review Workload Forecast

| Field | Value |
|---|---|
| Estimated changed lines | ≈3,000 total across 9 chained slices (production + tests: S1 ~360, S2 ~430, S3 ~450, S4a ~380, S4b ~370, S5 ~300, S6a ~270, S6b ~360, S7 ~210) |
| 400-line budget risk | High — S2, S3, S4a, S4b sit at or just above the budget; S3 is the largest. If a slice grows past ~450 lines during apply, stop and split along the seam noted in its header |
| Chained PRs recommended | Yes |
| Suggested split | PR S1 → PR S2 → PR S3 → PR S4a → PR S4b → PR S5 → PR S6a → PR S6b → PR S7 |
| Delivery strategy | auto-chain |
| Chain strategy | stacked-to-main |

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: High

The design's slice order is authoritative and strictly chained. S1 is pure Application/test doubles (no DI graph
change). S2 is the technology-risk slice: it MUST confirm on the pinned Microsoft.Data.Sqlite 8.0.11 that
`BackupDatabase` and `Pooling=False` behave as assumed (carried-into-apply item). S3 adds the inspect pipeline
(shared by restore). S4a builds the restore pipeline up to and including a real single-attempt swap; S4b hardens the
swap (hot journal, retry, repair, verify, rollback, `.bak` hook) and carries the failure-injection suite. S5 wires
ports, DI and the startup hook. S6a delivers strings, notification suspension and the restart service; S6b delivers
the ViewModel and the view. S7 is the end-to-end roundtrip plus docs.

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|---|---|---|---|
| S1 | Contracts and create use case | PR S1 | Base = main. `IBackupService`, `IMigrationCatalog`, `IFileOpenDialog`, `OpenTarget`, `FileTypeFilter`, records, `BackupErrorCodes`, `BackupFileName`, `CreateBackupUseCase`, `FakeBackupService`. |
| S2 | Create pipeline | PR S2 | Base = main (S1 merged). `SqliteBackupService.CreateAsync`, `BackupOptions`, `IBackupFileSystem`, `PhysicalBackupFileSystem`, `BackupZip`, `AddBackup()`, `BackupTestDb`. Confirms `BackupDatabase`/`Pooling=False` on 8.0.11. |
| S3 | Inspect pipeline + catalog | PR S3 | Base = main (S2 merged). `EfMigrationCatalog`, `SqliteBackupService.InspectAsync`, capped I/O, zip-slip, hash, integrity, FK, schema compare. Live-bytes-identical after every rejection. |
| S4a | Restore: validation reuse, safety backup, key carry-over, first swap | PR S4a | Base = main (S3 merged). `RestoreAsync` R1, R2, R3, single-attempt R5, R8 retention, free-space check, `FaultyBackupFileSystem`. Split seam: safety backup + validation. |
| S4b | Restore: hardened swap and rollback | PR S4b | Base = main (S4a merged). Hot journal gate R4, retry/backoff/repair R6, post-swap verify + rollback R7, `CompletePendingRestore`, real file-lock test. Split seam: swap + rollback. |
| S5 | Ports wiring | PR S5 | Base = main (S4b merged). `Inspect`/`RestoreBackupUseCase`, `AvaloniaFileOpenDialog`, save overload, `Bootstrap` block, `App` hook, arch guard. **Smoke-launch on throwaway data dir.** |
| S6a | Strings, suspension, restart service | PR S6a | Base = main (S5 merged). resx ×2 + manual Designer accessors + completeness test, `INotificationCenter.SuspendAsync`, `AppRestartService`. **Smoke-launch on throwaway data dir.** |
| S6b | ViewModel and view | PR S6b | Base = main (S6a merged). `BackupSettingsViewModel`, `SettingsViewModel` optional param, `SettingsView.axaml` block, frontend-design pass. **Smoke-launch in both locales on throwaway data dir.** |
| S7 | End-to-end and docs | PR S7 | Base = main (S6b merged). `BackupRestoreEndToEndTests`, `docs/architecture.md`, `docs/roadmap.md`, `spec/phases/phase-19/features.md`, `AGENTS.md`. |

## 1. Contracts and create use case (S1)

Depends on nothing (Phases 0-18 archived). Goal: library-free ports, records, error codes, the file-name helper and
`CreateBackupUseCase` live in Application, with test doubles ready for later slices.

- [x] 1.1 Write failing `tests/…Unit/Application/Backup/OpenTargetTests.cs` mirroring `SaveTargetTests`:
  `OpenReadAsync` delegates lazily to the supplied function and can be called N times; `Dispose` disposes the
  owner exactly once; `OpenReadAsync` after `Dispose` throws `ObjectDisposedException`. (R-1, DD-1)
- [x] 1.2 Write failing `tests/…Unit/Application/Backup/BackupFileNameTests.cs`: `BackupFileName.Create` with a fixed
  UTC instant and a fixed non-UTC `TimeZoneInfo` (constructed with `CreateCustomTimeZone`, not `Local`) returns the
  exact pattern `ControlDeFinanzasIA-backup-yyyyMMdd-HHmm.zip` in local time; invariant digits under es-DO and en
  current cultures; a date crossing midnight in the zone changes the day. (R-2, S-4, D-18)
- [x] 1.3 Write failing `tests/…Unit/Application/Backup/CreateBackupUseCaseTests.cs` using `FakeBackupService`:
  success passes `BackupCreated` through unchanged; a failure `Result` (code and `Details`) passes through
  unchanged; a thrown `IOException` becomes `Result.Failure` with `BackupErrorCodes.IoError` and is logged;
  `OperationCanceledException` propagates; the use case passes `target.OpenWriteAsync` and never opens the target
  itself (opens counted on `SaveTargets.Recording()` stay 0 when the fake service does not call it). (R-1, R-17,
  DD-2)
- [x] 1.4 Create `src/…Application/Abstractions/{IBackupService,IMigrationCatalog,IFileOpenDialog,OpenTarget,
  FileTypeFilter}.cs` exactly as in DD-1 (`CreateAsync(Func<CancellationToken, Task<Stream>> openDestination, ...)`,
  `InspectAsync`/`RestoreAsync` with `openRead`, `CompletePendingRestore()`, optional `string? title` on the picker).
  `IFileSaveDialog` is NOT touched in this slice (S5). No type from `Microsoft.Data.Sqlite`, EF Core,
  `System.IO.Compression` or Avalonia. Run 1.1 green. (R-1, DD-1, Reconciliation #1, #2)
- [x] 1.5 Create `src/…Application/Backup/{BackupManifest,BackupInfo,BackupCreated,RestoreOutcome,BackupErrorCodes,
  BackupFileName}.cs` per DD-1: exactly four codes (`NotABackup`, `Corrupt`, `NewerSchema`, `IoError`), the
  `Details` keys and `ReasonInsufficientSpace` constants, and `BackupFileName.Create(DateTime utcNow, TimeZoneInfo
  zone)` using `TimeZoneInfo.ConvertTimeFromUtc` and invariant culture. Run 1.2 green. (R-1, R-2, R-17,
  Reconciliation #3, D-18)
- [x] 1.6 Create `src/…Application/UseCases/Backup/CreateBackup/CreateBackupUseCase.cs`
  (`ExecuteAsync(SaveTarget, CancellationToken)`), catching every non-`OperationCanceledException`, logging via
  `ILogger<CreateBackupUseCase>` and mapping to `IoError`. Run 1.3 green. (R-1, R-17, DD-2)
- [x] 1.7 Create test doubles `tests/…Unit/TestDoubles/{FakeBackupService,FakeFileOpenDialog,OpenTargets}.cs`:
  `FakeBackupService` records calls per method, returns configurable `Result`s or throws, can block on a
  `TaskCompletionSource`; `FakeFileOpenDialog` returns null or a target, counts calls, records `LastFilter` and
  `LastTitle`; `OpenTargets.Recording()` counts opens and disposals and serves supplied bytes. (R-1, R-20)
- [x] 1.8 Confirm `ReportFormat` remained report-only and no existing file changed except by addition:
  `git diff --name-status` for the slice shows only `A` entries under `Application/Abstractions`,
  `Application/Backup`, `Application/UseCases/Backup` and test doubles. (R-1)
- [x] 1.9 Run all four gates: `dotnet build -c Release`, `dotnet test -c Release --no-build`,
  `dotnet format style --verify-no-changes`, NetArchTest filter (R-1..R-5; Application has no Sqlite/EF/Compression/
  Avalonia reference). Then the no-stubs grep, which must return nothing: `rg -n
  "NotImplementedException|TODO|FIXME|HACK" src/ControlDeFinanzasIA.Application/Backup
  src/ControlDeFinanzasIA.Application/UseCases/Backup src/ControlDeFinanzasIA.Application/Abstractions/IBackupService.cs
  tests/ControlDeFinanzasIA.Tests.Unit/TestDoubles`. All green before opening PR S1. (R-1, R-24)

## 2. Create pipeline (S2)

Depends on 1. Goal: `SqliteBackupService.CreateAsync` produces a valid, key-free, hash-verified ZIP from a consistent
snapshot, writing to the destination only after every check passes. Confirm the two assumptions carried into apply.

- [x] 2.1 **Technology confirmation (first task of the slice, before production code).** Write a throwaway-free,
  permanent characterization test `tests/…Integration/Backup/SqliteBehaviorAssumptionsTests.cs` against the pinned
  Microsoft.Data.Sqlite 8.0.11 proving: (a) `SqliteConnection.BackupDatabase` copies a file DB while a second
  connection commits rows in a loop and the copy passes `integrity_check`; (b) with `Pooling=False` the OS handle is
  released on `Dispose` (the file can be deleted right after) while with pooling on it cannot until `ClearAllPools()`
  (Windows); (c) `PRAGMA secure_delete=ON` + `DELETE` + `VACUUM` leaves no trace of a marker string in the file
  bytes, while a plain `DELETE` does. Record the outcome in the PR description; if any assertion contradicts the
  design, STOP and report before implementing. (R-3, R-6, R-13, DD-3)
- [x] 2.2 Create `tests/…Integration/Backup/BackupTestDb.cs` (fixture): `<temp>\backup-<guid>\` data dir,
  `StubPathResolver`, real file DB created via `AppDbContext.Database.Migrate()`, seeded across every table
  (accounts, categories, expenses, incomes, budgets, goals, debts, recurring, notifications, anomaly alerts, saved
  views, tags, `Settings` with `ai.apiKey = "sk-test-SECRET-123"`), helper `LiveSha256()`, and `Dispose` doing
  `SqliteConnection.ClearAllPools()` plus recursive delete (the `ReportExportEndToEndTests` precedent). (R-16, R-10)
- [x] 2.3 Write failing `tests/…Integration/Backup/SqliteBackupServiceCreateTests.cs` against the REAL
  `SqliteBackupService` on the fixture, with `PhysicalBackupFileSystem`, `FakeClock` and a no-op `Delay`:
  (a) the ZIP has exactly the entries `manifest.json` and `data.db`, nothing else (S-3, R-2); (b) manifest fields:
  `formatVersion 1`, `exportedAt` equals `clock.UtcNow.ToString("O")`, `appVersion` from options, `schemaVersion`
  equals the latest `MigrationId` in the copy, `apiKeyIncluded false` (S-3); (c) `dbSha256` equals the SHA-256 of
  the zipped `data.db` entry bytes (R-5, S-3); (d) the extracted DB passes `integrity_check` and every table is
  row-equal to the live one except the `ai.apiKey` row, all other `Settings` rows preserved (R-6, R-16); (e)
  **byte scan**: the key in UTF-8 and UTF-16LE appears in neither the raw ZIP bytes nor the decompressed `data.db`,
  after the key row was inserted, deleted and re-inserted to force free pages (S-5, R-6); (f) the live DB still
  holds the key and the live SHA-256 is unchanged (R-6, R-10); (g) a DB without a `Settings` table exports fine
  (R-6); (h) a concurrent writer (second connection inserting in a loop while `CreateAsync` runs) yields success,
  `integrity_check` ok and a consistent row set (S-6, R-3); (i) the destination is opened zero times when the copy
  fails, and receives zero bytes when the integrity check fails via the `IBackupFileSystem`/options seam, with
  result `Corrupt` (S-7, R-4, R-7); (j) an existing longer destination is truncated via `SetLength` (R-7); (k)
  `backups\.work\` and the data directory contain no leftover temp file after success, failure and cancellation
  (R-7); (l) any `IOException` maps to `IoError` (R-17); (m) creation never enables WAL (`journal_mode` of the copy
  is `delete`) (R-3). (R-2..R-7, R-17, S-3, S-5, S-6, S-7)
- [x] 2.4 Create `src/…Infrastructure/Backup/{BackupOptions,IBackupFileSystem,PhysicalBackupFileSystem}.cs` per DD-3
  (public seam because Infrastructure has no `InternalsVisibleTo`; `BackupOptions` with retry delays, injectable
  `Delay`, size caps, `SafetyBackupsToKeep`). `PhysicalBackupFileSystem` is a 1:1 wrapper over `System.IO` +
  `DriveInfo`, with `Replace` calling `File.Replace(..., ignoreMetadataErrors: true)`. (R-11, R-13, DD-3)
- [x] 2.5 Create `src/…Infrastructure/Backup/BackupZip.cs` (manifest JSON via `System.Text.Json`, camelCase, strict
  read; capped entry reader/writer; deterministic `Optimal` compression) with unit-level use only through the
  service. Create `src/…Infrastructure/Backup/SqliteBackupService.cs` with the single-flight `SemaphoreSlim`,
  per-call work directory `<DataDir>\backups\.work\<guid>\` deleted in `finally`, pipeline bodies in `Task.Run`,
  and `CreateAsync` per DD-4 steps 1-7 (`Pooling=False`, `ReadOnly` source, `BackupDatabase`, redaction with
  `secure_delete` + `DELETE` + `VACUUM`, `integrity_check`, hash, ZIP, destination opened last). `InspectAsync` and
  `RestoreAsync` and `CompletePendingRestore` are NOT declared as stubs here: the class implements
  `IBackupService` only from S3/S4b, so in this slice the concrete class exposes `CreateAsync` and the interface is
  satisfied by an `internal` partial extended in S3 and S4a/S4b, OR (preferred, no half-implemented interface)
  `CreateAsync` is added to a `sealed partial class SqliteBackupService` that is not yet registered for
  `IBackupService` (registration happens in 2.6 only for the concrete type). Whichever is chosen, no method with
  `throw new NotImplementedException()` may exist. Run 2.3 green. (R-2..R-7, R-24, DD-3, DD-4)
- [x] 2.6 Create `src/…Infrastructure/Backup/BackupServiceCollectionExtensions.cs` (`AddBackup(this IServiceCollection,
  string appVersion)` with `TryAddSingleton`; no `Bootstrap` change yet) and a test
  `BackupServiceCollectionExtensionsTests` resolving the registered singletons, calling `AddBackup` twice
  idempotently. (R-24, DD-3)
- [x] 2.7 Verify no new package: `git diff --name-only` for the slice contains no `Directory.Packages.props` or
  `.csproj`. (R-24)
- [x] 2.8 Run all four gates plus the no-stubs grep over `src/ControlDeFinanzasIA.Infrastructure/Backup` and
  `tests/ControlDeFinanzasIA.Tests.Integration/Backup` (no `NotImplementedException|TODO|FIXME|HACK`). Confirm
  `dotnet test` includes the new integration tests by name (`--filter FullyQualifiedName~Backup`, N > 0). All green
  before opening PR S2. (R-24)

**S2 apply notes (findings that bind later slices):**
- `BackupDatabase` (Microsoft.Data.Sqlite 8.0.11) does NOT wait on a busy source: it throws SQLite error 5 at once
  (extended codes such as 261 `BUSY_RECOVERY` appear against a WAL database; `SqliteErrorCode` is the extended code,
  so mask with `& 0xFF`). The copy is consistent whenever it succeeds. `CreateAsync` therefore retries with a fresh
  destination file (`BackupOptions.SnapshotRetryDelays`, `BackupOptions.Delay`); this is an addition to DD-4 step 1.
- `Pooling=False` releases the OS handle on `Dispose`; pooled connections keep it until `ClearAllPools()` (Windows).
  `secure_delete` + `DELETE` + `VACUUM` removes the bytes; a plain `DELETE` leaves them. All confirmed by tests.
- **The live database is in WAL mode** (EF Core 8 creates SQLite files in WAL). The design assumed a rollback
  journal. Consequences for S4: the live `data.db` may have `data.db-wal` / `data.db-shm` next to it, and swapping
  `data.db` while a stale `-wal` remains would apply the OLD database's frames to the restored file. S4a/S4b MUST
  checkpoint/close all connections and remove (or verify absent) `data.db-wal` and `data.db-shm` around the swap and
  add tests for it; the `data.db-journal` hot-journal gate alone is not enough. The exported/restored copy is
  normalized with `PRAGMA journal_mode = DELETE`.
- The create pipeline opens the live file `ReadOnly`; on a WAL database this can leave empty `data.db-wal` /
  `data.db-shm` sidecars (normal for a WAL database, harmless).
- `SqliteBackupService` is a `sealed partial class` that does not yet implement `IBackupService` (no placeholder
  members); its constructor omits `IMigrationCatalog` until S3 needs it; `AddBackup` registers the concrete type only.
  `BackupOptions` only carries the fields S2 uses (`SwapRetryDelays`, size caps and `SafetyBackupsToKeep` arrive with
  the slices that read them).
- An integrity check also runs on the raw copy before redaction (VACUUM could otherwise hide page damage).

## 3. Inspect pipeline and migration catalog (S3)

Depends on 2. Goal: `InspectAsync` (and the validation core reused by restore) rejects every malformed, corrupt or
future-schema backup without any write outside its own work directory, and the live file is byte-identical after
every rejection. Size note: this is the largest slice; if it exceeds ~450 lines split at the seam "structure +
manifest + caps (steps 1-3)" vs "extract + hash + integrity + schema compare (steps 5-8)".

- [x] 3.1 Write failing `tests/…Integration/Backup/EfMigrationCatalogTests.cs`: `KnownMigrationIds` equals the set
  of ids of the files in `src/…Infrastructure/Persistence/Migrations/` (discovered by reflection over
  `Migration` types with `MigrationAttribute`), is ordinal-sorted, contains `…_AddNotifications` and
  `…_AddSettings`; constructing the catalog opens no connection (no file created for `Data Source=:memory:`).
  (R-9, D-15, DD-3)
- [x] 3.2 Create `src/…Infrastructure/Backup/EfMigrationCatalog.cs` (Singleton; builds once from
  `new AppDbContext(options).Database.GetMigrations()`, disposes the context, caches). Run 3.1 green. (R-9, R-24)
- [x] 3.3 Add a `BackupZipBuilder` test helper in `tests/…Integration/Backup/` that produces crafted ZIPs from
  arbitrary entry names, bytes and manifests (used for every malformed case below). (R-8)
- [x] 3.4 Write failing `tests/…Integration/Backup/SqliteBackupServiceInspectTests.cs` against the REAL service.
  Each case asserts the expected code AND `fixture.LiveSha256()` equal before/after AND no `data.db-journal` AND
  `backups\.work\` empty (R-10, S-33):
  (a) valid backup returns `BackupInfo` with `ExportedAtUtc`, `AppVersion`, `SchemaVersion`, `DatabaseSizeBytes`,
  `IsOlderSchema false` (R-18); (b) an older backup (history row of the latest id deleted in the copy) is accepted
  with `IsOlderSchema true` (S-15, R-9); (c) a fake unknown id `29990101000000_Future` gives `NewerSchema` with
  Details `backupSchema`, `appSchema`, `backupAppVersion` present (S-2, R-9); (d) a manifest `formatVersion 2`
  gives `NewerSchema` (Reconciliation #3); (e) random bytes, empty stream, non-ZIP gives `NotABackup` (S-8); (f)
  missing `manifest.json`, missing `data.db`, an extra third entry, a directory entry each give `NotABackup`
  (S-9); (g) entry names `..\..\evil.db`, `/abs/data.db`, `sub/data.db`, `C:\x\data.db` give `NotABackup` and no
  file appears outside the work directory (scan the data directory tree) (S-10, R-8); (h) declared `data.db`
  length above the cap (cap lowered through `BackupOptions`) and a manifest above 64 KiB give `NotABackup` (S-11);
  (i) a **lying header** (small declared length, larger actual bytes) is stopped by the capped stream and gives
  `NotABackup` without reading past the cap (S-11); (j) malformed JSON, missing required manifest field, wrong
  types give `NotABackup`; (k) one flipped byte in `data.db` gives `Corrupt` (hash) (S-12); (l) non-SQLite bytes
  with a matching hash give `Corrupt` (bad 16-byte header) (S-13); (m) damaged DB pages with a matching hash give
  `Corrupt` from `integrity_check` (S-13); (n) a foreign-key violation with a matching hash gives `Corrupt`
  (S-13); (o) a valid SQLite file without `__EFMigrationsHistory` gives `NotABackup` (S-16); (p) an oversized AND
  wrong-hash ZIP reports the size-cap outcome, proving order (S-14, R-8); (q) a pre-`Settings` backup (history
  without `AddSettings`, no table) sets `LocalApiKeyWillBeLost true` when the live DB has a key, and `false` when
  it has none (S-24, R-15); (r) a picker stream that is non-seekable is accepted (staged to disk first). (R-8,
  R-9, R-10, R-18, S-2, S-8..S-16, S-24)
- [x] 3.5 Create the inspect pipeline in `src/…Infrastructure/Backup/SqliteBackupService.cs` (partial) and
  `BackupZip.cs`: `InspectAsync` per DD-5 steps 1-3 and 5-8: stage to `work\incoming.zip` through a counting stream;
  structure check with ordinal name equality; capped manifest read; extraction of `data.db` to `work\restore.db`
  through a capped hashing stream using a FIXED destination name (never the entry name); hash compare
  case-insensitive; header check; `integrity_check` and `foreign_key_check`; migration-set comparison via
  `IMigrationCatalog` with set membership; `LocalApiKeyWillBeLost` from a `ReadOnly` live read. The shared
  validation core is a private method returning the extracted temp path plus facts so `RestoreAsync` (S4a) reuses it
  without duplication. Run 3.4 green. (R-8, R-9, R-10, R-18, DD-5)
- [x] 3.6 Extend `BackupServiceCollectionExtensions.AddBackup` with `TryAddSingleton<IMigrationCatalog,
  EfMigrationCatalog>()` and extend its test. (R-24)
- [x] 3.7 Run all four gates plus the no-stubs grep over the slice's files (`Infrastructure/Backup`,
  `Tests.Integration/Backup`). Confirm `git diff --name-only` shows no `.csproj`/`Directory.Packages.props`
  change. All green before opening PR S3. (R-24)

**S3 apply notes:** a "lying header" (declared length smaller than the real inflated size) yields `Corrupt`, not
`NotABackup`: the BCL bounds inflation at the declared size, so the truncated payload fails the SHA-256 check; the
service-side capped copy remains as a second line of defence. `SqliteBackupService` now takes `IMigrationCatalog`
(constructor order per DD-3) and inspect lives in `SqliteBackupService.Inspect.cs`; the shared core is
`ValidateAsync` returning the private `Validated` record (extracted path, manifest, info, `BackupHasSettings`).

## 4a. Restore: safety backup, key carry-over, first swap (S4a)

Depends on 3. Goal: `RestoreAsync` re-validates in full, checks free space, normalizes the incoming DB (key
carry-over), writes a valid safety backup with retention, and performs a real single-attempt `File.Replace` so the
roundtrip already works end to end. Hardening of the swap (journal, retry, repair, verify, rollback) is S4b; until
then a swap failure returns `IoError` with the live file untouched. Seam: safety backup + validation reuse vs swap +
rollback.

- [x] 4a.1 Create `tests/…Integration/Backup/FaultyBackupFileSystem.cs`: a decorator over `PhysicalBackupFileSystem`
  that throws a configured exception on the n-th call of a named operation (`Create`, `Replace`, `Move`, `Delete`,
  `GetAvailableFreeSpace` override to a value), records the ordered call log, and optionally runs a side effect
  before throwing (used in S4b). (R-11, R-13)
- [x] 4a.2 Write failing `tests/…Integration/Backup/SqliteBackupServiceRestoreTests.cs` against the REAL service
  (all assert live SHA-256 identity on failure paths, and `backups\.work\` empty):
  (a) **roundtrip**: export A, mutate the live DB, restore A: every table equals A, `Settings` equals A plus the
  local `ai.apiKey` (S-1, R-16, Reconciliation #12); (b) restore re-runs full validation and does not trust a prior
  inspect: a ZIP altered between `InspectAsync` and `RestoreAsync` is rejected (R-8, D-10); (c) every rejection
  case from 3.4 (garbage, missing/extra entry, zip-slip, size caps, hash, corrupt, FK, no history, newer schema)
  through `RestoreAsync` returns the same code and leaves live bytes identical; a table-driven theory reuses the
  `BackupZipBuilder` (S-33, R-10); (d) **key carry-over**: backup without key and live with key: restored DB has the
  local key and the live file is written only by the swap (verify by SHA-256 of live immediately before the swap
  seam) (S-23, R-15); backup with an empty-string key is treated as missing (Reconciliation #9); a backup with a
  non-empty key keeps its own; (e) a pre-`Settings` backup restores successfully with `RestoreOutcome.ApiKeyLost
  true` and no key carried (S-24); (f) **safety backup**: exists at `backups\pre-restore-yyyyMMdd-HHmmss.zip`, is a
  valid backup (feed it back through `InspectAsync`), has `apiKeyIncluded true` and its `data.db` equals the
  pre-restore live DB row-for-row (S-17, R-12, Reconciliation #4); (g) **retention**: with three existing
  `pre-restore-*.zip` plus an unrelated `notes.txt` and a user `my-backup.zip`, a successful restore leaves exactly
  3 newest `pre-restore-*.zip`, deletes the oldest, and leaves the other files untouched; five consecutive restores
  keep the 3 newest; same-second collision gets the `-2` suffix (S-17, R-12); (h) **safety backup failure** (fault
  on `Create` of the safety file) returns `IoError`, live identical, no `.zip` left (only a deleted `.tmp`), and
  `Replace` was never called (call log) (S-18, R-12); (i) **free space** fault returning less than 3x the larger of
  live and incoming size gives `IoError` with `Details[reason] = insufficientSpace`, live identical, no safety
  backup created, checked before extraction (S-19, R-11); (j) cancellation before the safety backup completes gives
  cancellation with live identical; cancellation after that point is ignored by the swap (Reconciliation #7); (k)
  a single `Replace` fault (always) gives `IoError`, live identical, temp deleted, safety ZIP kept (R-13 baseline,
  extended in S4b); (l) after a successful swap `data.db.pre-restore.bak` exists and equals the original bytes
  (R-14). (R-8, R-10..R-12, R-15, R-16, S-1, S-17..S-19, S-23, S-24, S-33)
- [x] 4a.3 Implement in `src/…Infrastructure/Backup/SqliteBackupService.cs` (partial): `RestoreAsync` runs the
  shared validation core (S3, fresh work dir), then the free-space check before extraction (`IoError` +
  `ReasonInsufficientSpace`), then R1 (key carry-over into `restore.db` via `INSERT ... ON CONFLICT(Key) DO
  UPDATE`, `PRAGMA journal_mode=DELETE`, assert no `-journal/-wal/-shm`, hash to `expectedHash`), then R2
  (safety backup through the DD-4 pipeline WITHOUT redaction and `apiKeyIncluded true`, written as `.zip.tmp`,
  re-read and hash-verified, moved to `.zip`, collision suffix), R3 (`ClearAllPools()`), a real single-attempt R5
  swap via `IBackupFileSystem.Replace(restore.db, data.db, data.db.pre-restore.bak)` (moving an anomalous
  leftover `.bak` aside first), R8 retention by ordinal name limited to `pre-restore-*.zip`, R9 cleanup in
  `finally`. Returns `RestoreOutcome`. Cancellation honored until R2 finishes. Run 4a.2 green. Every branch listed
  above maps to a test name in 4a.2; no branch may be left without one. (R-8, R-10..R-12, R-14..R-16, DD-6,
  Reconciliation #4, #7, #9, #11)
- [x] 4a.4 Refactor the DD-4 create pipeline into a private method taking `redactApiKey` so user export (S2 tests
  2.3 unchanged and still green) and safety backup share one implementation (R-12: "same creation pipeline").
  Re-run 2.3 and 4a.2. (R-12, DD-6)
- [x] 4a.5 Run all four gates plus the no-stubs grep over the slice's files. Additionally grep that the live path is
  written by exactly one statement: `rg -n "Replace\(" src/ControlDeFinanzasIA.Infrastructure/Backup` shows the
  swap call in `SqliteBackupService` (and the `PhysicalBackupFileSystem` wrapper) only. All green before opening
  PR S4a. (R-10, R-13)

**S4a apply notes (findings that bind S4b):**
- WAL handling (the live DB is in WAL mode): `Swap` calls `SqliteConnection.ClearAllPools()`, resolves any hot
  `-journal`, and if `-wal`/`-shm` exist runs `PRAGMA wal_checkpoint(TRUNCATE)` on a non-pooled writer connection
  (busy result aborts), then refuses the swap if a non-empty `-wal` remains and requires the sidecars to be deletable.
  After the swap `VerifyInstalled` deletes any `-wal`/`-shm`/`-journal` BEFORE opening the new file; a sidecar that
  cannot be deleted triggers the rollback. The checkpoint changes live bytes (never its rows) only when pending
  frames exist; the original is then retained in `data.db.pre-restore.bak` with those frames folded in.
- The hot-journal gate had to move BEFORE the safety backup (a read-only snapshot cannot recover a hot journal), and
  runs again inside the swap preparation. It is a single attempt; S4b adds the retry schedule around it.
- The minimal post-swap verification (hash + `quick_check`) and a single-attempt rollback via
  `Replace(.bak, data.db, data.db.rollback-<utc>)` were built in S4a (not left as a hole). S4b still owns the retry
  loop, the fault-injection suite for them, the Critical-log assertion and `CompletePendingRestore`. The
  invariant repair (`data.db` missing and `.bak` present -> move back) is already in the single-attempt path.
- `SqliteConnection.ClearAllPools()` is process-wide: tests that arm a pooled connection must tolerate a foreign
  fixture clearing it (see the retry in the pooled-WAL test).
- The class does not yet declare `: IBackupService` (`CompletePendingRestore` is S4b); `AddBackup` is unchanged.
- Retention names use an explicit pattern (`pre-restore-yyyyMMdd-HHmmss[-N].zip`) and a (timestamp, N) order so a
  collision suffix sorts as newer; the next suffix is max+1, never a number freed by retention.

## 4b. Restore: hardened swap, rollback and `.bak` lifecycle (S4b)

Depends on 4a. Goal: the swap survives locked files and hot journals with bounded retries, repairs the name
invariant after a partial Win32 failure, verifies the result and rolls back to byte-identical original bytes on any
post-swap problem; `CompletePendingRestore` implements the `.bak` lifecycle. This slice carries the data-loss
failure-injection suite and one REAL file-lock test.

- [x] 4b.1 Write failing `tests/…Integration/Backup/SqliteBackupServiceSwapFailureTests.cs` against the REAL service
  with `FaultyBackupFileSystem` and a recording `Delay` (every test asserts live SHA-256 equals the original after
  failure and `backups\.work\` empty):
  (a) **retry with backoff**: `Replace` throws `IOException` twice then succeeds: exactly 3 attempts, delays 200 ms
  and 600 ms requested in order, `ClearAllPools()` invoked before each attempt (proved by a pooled connection that
  is closed and file-deletable), swap succeeds, `.bak` exists (S-20, R-13); (b) **permanent failure**: every
  attempt throws `IOException`/`UnauthorizedAccessException`: 3 attempts, delays 200/600/1800, `IoError`, live
  byte-identical, temp `restore.db` deleted, safety ZIP kept (S-21, R-13); (c) **name-invariant repair**: a fault
  that moves live to `data.db.pre-restore.bak` and then throws (simulated Win32 1177) is repaired by moving the
  `.bak` back before the next attempt; final state has the original at `data.db` (DD-6 R6, T8); (d) **hot journal
  rolled back**: a real `data.db-journal` from an interrupted transaction (open a connection, begin a write, copy the
  `-journal` while the transaction is open, then abandon per SQLite semantics) is resolved by the writer-open in R4,
  the journal is gone, and the swap proceeds without rolling the journal into the restored DB (restored DB rows
  equal the backup) (S-22, R-13, Reconciliation #10); (e) **persistent journal** (fault re-creates
  `data.db-journal` after every R4 attempt) gives no swap (`Replace` never called), `IoError`, live and journal
  unchanged (S-22, R-13); (f) **hash mismatch after swap**: a fault that makes `Replace` install a different file
  triggers R7 rollback via `Replace(.bak, data.db, data.db.rollback-<utc>)`, live bytes equal the original, result
  `IoError` (T10, R-13); (g) **`quick_check` failure after swap** (fault installs a damaged file with the expected
  hash seam) also rolls back to the original (T10); (h) **rollback itself fails**: `IoError`, a Critical log naming
  the `.bak` and safety ZIP is emitted (assert with a capturing logger), `.bak` and safety ZIP remain (T11); (i)
  failure injection at each stage (validation, free space, safety backup, extraction, swap, post-verify) never
  changes the live SHA-256 for any non-success outcome (S-33, R-10); (j) retention still runs after an R7 rollback
  when R2 produced a ZIP (DD-6 R8). (R-10, R-13, S-20..S-22, S-33)
- [x] 4b.2 Write failing **real file-lock test** in `tests/…Integration/Backup/SqliteBackupServiceRealLockTests.cs`
  (guard with `[SkippableFact]`/`Skip.IfNot(OperatingSystem.IsWindows())`, and it MUST run and pass on the Windows
  dev machine, the skip is only for other OSes): hold `data.db` open with `new FileStream(path, FileMode.Open,
  FileAccess.Read, FileShare.ReadWrite)` (no `FileShare.Delete`) while `RestoreAsync` runs with the real
  `PhysicalBackupFileSystem` and a no-op `Delay`: result `IoError` after 3 real attempts, live SHA-256 identical,
  no temp left, safety ZIP kept; after releasing the lock the same restore succeeds. No fake file system is used in
  this test. (R-13, S-21, T8)
- [x] 4b.3 Write failing `tests/…Integration/Backup/CompletePendingRestoreTests.cs`: after a successful restore
  `CompletePendingRestore()` deletes `data.db.pre-restore.bak` and sweeps `backups\.work\` (S-1, S-25, R-14); it is
  a no-op when nothing exists; it does NOT throw when the `.bak` is locked (real `FileStream` lock, assert no
  exception and the file remains) or missing (R-14); the safety ZIP and other `backups\` files are never deleted by
  it (R-12, R-14). (R-14, S-25)
- [x] 4b.4 Write failing older-schema test: restore a backup whose history is a proper subset, then run
  `AppDbContext.Database.Migrate()` on the swapped file: succeeds and all data is kept (S-15, D-14). (R-9, S-15)
- [x] 4b.5 Implement in `SqliteBackupService.cs` (partial): R4 hot-journal gate (open `ReadWrite`, `Pooling=False`,
  `SELECT count(*) FROM sqlite_master`, re-check within the retry schedule), R6 retry loop over
  `BackupOptions.SwapRetryDelays` with `ClearAllPools()` and `Delay` before each repeat and the invariant repair,
  R7 post-swap verification (SHA-256 of `data.db` equals `expectedHash`, read-only `quick_check` ok) with rollback
  and Critical log, cancellation ignored from R4 onward, and `CompletePendingRestore()` (never throws, logs).
  Replace the single-attempt swap of 4a.3 rather than adding a second code path. Run 4b.1 through 4b.4 green.
  (R-10, R-13, R-14, DD-6, Reconciliation #7, #10)
- [x] 4b.6 Update `IBackupService` registration: `AddBackup` now registers `IBackupService` →
  `SqliteBackupService` (all four interface members exist and are fully implemented), extending
  `BackupServiceCollectionExtensionsTests` to resolve `IBackupService` and assert the concrete type. (R-24)
- [x] 4b.7 Run all four gates plus the no-stubs grep over the slice's files. Run the failure-injection and real-lock
  tests explicitly by name and record N > 0 executed (not skipped) on Windows:
  `dotnet test -c Release --no-build --filter "FullyQualifiedName~SwapFailure|FullyQualifiedName~RealLock"`.
  All green before opening PR S4b. (R-10, R-13, R-14)

**S4b apply notes:**
- Retry schedule semantics: `BackupOptions.SwapRetryDelays` (200/600/1800 ms) is the number of RETRIES, like
  `SnapshotRetryDelays`, so a permanent failure makes 4 attempts (the task text said "3 attempts" but also asks for
  all three delays to be requested; the delays win). The same schedule paces the rollback attempts.
- Each attempt: `ClearAllPools`, repair the name invariant (live missing and `.bak` present -> move back), R4 sidecar
  gate (hot journal, WAL checkpoint, `-wal`/`-shm` removal), hash of the original, move-aside of an anomalous `.bak`,
  the single `Replace`. Any `IOException`/`UnauthorizedAccessException`/`SqliteException`/gate rejection is a failed
  attempt, followed by the repair. If `Replace` throws after it already installed the file (staged file gone, live
  and `.bak` present) the attempt is treated as installed and goes to verification instead of retrying.
- A `-journal` next to a WAL-mode live database is never resolved by SQLite (WAL ignores it), so the restore fails
  closed with the journal untouched; a real hot journal is exercised in journal mode DELETE.
- Rollback is retried on the schedule, repairs its own partial failure, removes stale sidecars of the rejected file
  and only reports success when the live bytes equal the original hash; otherwise it logs Critical with the `.bak`,
  the rejected file and the safety ZIP paths.
- `CompletePendingRestore` skips when the service gate is held (an operation, e.g. a restore, is running), deletes
  the `.bak` only when the live DB opens, passes `quick_check` and lists every known migration id, then sweeps
  `backups\.work`. Other files (safety ZIPs, `data.db.pre-restore-<utc>.bak`, `data.db.rollback-*`) are never touched.
- `AddBackup` registers `IBackupService` as a factory over the `SqliteBackupService` singleton so both share the gate.
- `[SkippableFact]` is not available (no new package allowed): `WindowsOnlyFactAttribute` sets `Skip` on non-Windows.
- The pre-existing `PoolingOn_KeepsTheHandleUntilClearAllPools_OnWindows` test raced with other fixtures'
  process-wide `ClearAllPools` (seen once in four full runs) and now re-arms the pool until it holds.

## 5. Ports wiring, DI and startup hook (S5)

Depends on 4b. Goal: inspect/restore use cases, the real open dialog, the generic save overload, DI in `Bootstrap`,
the startup `.bak` cleanup in `App`, and the D20 architecture guard. Invoke no visual change yet.

- [x] 5.1 Write failing `tests/…Unit/Application/Backup/{InspectBackupUseCaseTests,RestoreBackupUseCaseTests}.cs`:
  pass-through of `BackupInfo`/`RestoreOutcome`, failure code/details pass-through, thrown exception becomes
  `IoError`, `OperationCanceledException` propagates, the picker stream is opened only by the service. (R-1, R-17,
  DD-2)
- [x] 5.2 Create `src/…Application/UseCases/Backup/InspectBackup/InspectBackupUseCase.cs` and
  `…/RestoreBackup/RestoreBackupUseCase.cs` per DD-2. Run 5.1 green. (R-1, DD-2)
- [x] 5.3 Append the generic `PickAsync(string suggestedFileName, FileTypeFilter filter, string? title = null,
  CancellationToken ct = default)` overload to `src/…Application/Abstractions/IFileSaveDialog.cs` (existing member
  untouched). Update `tests/…Unit/TestDoubles/FakeFileSaveDialog.cs` in the same commit (records `LastFilter`,
  `LastTitle`; both overloads share its call counter and blocking TCS) and every other `IFileSaveDialog`
  implementer found by `rg "IFileSaveDialog" tests src`. The solution must compile. (R-1, DD-1)
- [x] 5.4 Modify `src/…UI/Services/AvaloniaFileSaveDialog.cs`: new overload holds the picker body; the
  `ReportFormat` overload delegates by mapping to `new FileTypeFilter(Strings.ExportPdfFileTypeName, "pdf",
  "application/pdf")` (or xlsx) and `Strings.ExportDialogTitle`, so report behavior is unchanged. Existing Phase 18
  Dashboard/Expenses export tests stay green untouched. (R-1, DD-8)
- [x] 5.5 Create `src/…UI/Services/AvaloniaFileOpenDialog.cs` (`ResolveStorageProvider` like the save dialog,
  `OpenFilePickerAsync` with `AllowMultiple=false`, empty result → `null`, otherwise `new OpenTarget(file.Name, _
  => file.OpenReadAsync(), owner: file)`; no `TopLevel` → warning + `null`). Its title default uses a resx key that
  is added in S6a; until then it takes `title` from the caller and falls back to `Strings.ExportDialogTitle`-free
  literal-free behavior: the fallback MUST be a non-UI-text `null` title, and S6a wires the localized title. (R-1,
  DD-8)
- [x] 5.6 Append the composition block to `src/…UI/Composition/Bootstrap.cs`: `services.AddBackup(ReadAppVersion())`
  (informational version of the UI assembly, same read as `SettingsViewModel.ReadInformationalVersion`), the three
  use cases Transient, `IFileOpenDialog → AvaloniaFileOpenDialog` Singleton. Nothing existing reordered.
  (R-24, DD-10)
- [x] 5.7 Modify `src/…UI/App.axaml.cs`: immediately after `Bootstrap.ApplyMigrations(Services);` call a small
  `CompletePendingRestore(Services, logger)` helper that resolves `IBackupService` and calls
  `CompletePendingRestore()` inside `try/catch` (log Warning, never crash startup). It is NOT reached if
  `ApplyMigrations` throws. (R-14, S-25, DD-10)
- [x] 5.8 Write failing tests: `tests/…Unit/Architecture/ArchitectureTests.cs` (append `"Microsoft.Data.Sqlite"` to
  `_forbiddenPackagesForDomainAndApplication` and `_forbiddenPackagesForUi`; R-1..R-5 and existing rules stay
  green) (R-24, S-32, D-20); `tests/…Unit/Composition/BackupCompositionTests.cs` using the real `Bootstrap`
  container (resolve the three use cases, `IBackupService` is `SqliteBackupService`, `IMigrationCatalog` is
  `EfMigrationCatalog`, `IFileOpenDialog` is `AvaloniaFileOpenDialog`); and a startup-order test that proves the
  hook runs after a successful migration and is skipped when the migration step throws (extract the ordering into
  a testable helper in `App`/`Bootstrap` if needed, without changing existing behavior). Run green after 5.2-5.7.
  (S-25, S-32)
- [x] 5.9 **Smoke launch on a throwaway data directory.** First confirm how `SystemPathResolver` chooses
  `DataDirectory` (read `src/…UI/System/SystemPathResolver.cs`) and how to override it (env var or setting). Launch
  `dotnet run --project src/ControlDeFinanzasIA.UI` with that override pointing at a fresh temp folder; the app
  starts, migrations run, no exception, exit. If no override exists, do NOT launch against the real profile: record
  "smoke not run: no data-dir override" and rely on `BackupCompositionTests`. Record output. (DD-10)
- [x] 5.10 Run all four gates plus the no-stubs grep over the slice's files (Application use cases, `UI/Services`,
  `Composition`, `App.axaml.cs`, tests). Confirm the Phase 18 export tests still pass unchanged. All green before
  opening PR S5. (R-24)

**S5 apply notes:**
- 5.9 RUN via override: `CONTROLDEFINANZASIA_DATA_DIR` (absolute path; blank/relative values ignored, OS default otherwise)
  now redirects `SystemPathResolver`. Launched `dotnet run -c Release --no-build` against a fresh temp dir: 17 migrations
  applied, `data.db` created there, process stayed up ~25 s with 0 exceptions/fail/crit log lines (EF rebuild-table
  warnings only, pre-existing), then killed. Real `%APPDATA%ControlDeFinanzasIA` listing identical before/after.
- 5.7 deviation: instead of a helper in `App`, the ordering lives in `Bootstrap.ApplyMigrationsAndCompletePendingRestore`
  (public, testable; `RunStartupSequence` skips the completion when migrations throw and swallows/logs completion errors).
  `App` calls it in place of the former `Bootstrap.ApplyMigrations(Services)` line.
- 5.8: the guard forbids `Microsoft.Data.Sqlite` only (as listed); `System.IO.Compression` was not added.
- The generic `IFileSaveDialog.PickAsync(string, FileTypeFilter, string?, CancellationToken)` is what the report overload now
  delegates to; `AvaloniaFileOpenDialog` passes a `null` title through until S6a wires the localized one.

## 6a. Strings, notification suspension and restart service (S6a)

Depends on 5. Goal: the localized string set exists and is verified, the notification center can be suspended during
restore, and the app can relaunch itself. No visible UI yet beyond resources.

- [x] 6a.1 Resx first (R-22, S-31): append to `src/…UI/Resources/Strings.resx` (es-DO default) and
  `Strings.en.resx` the same key set from DD-9: `BackupSectionTitle`, `BackupSectionDescription`,
  `BackupExportButton`, `BackupRestoreButton`, `BackupExportDialogTitle`, `BackupRestoreDialogTitle`,
  `BackupFileTypeName`, `BackupWorkingExport`, `BackupWorkingInspect`, `BackupWorkingRestore`,
  `BackupExportSucceeded` (`{0}` file name), `BackupConfirmTitle`, `BackupConfirmWarning`,
  `BackupConfirmDetailsFormat` (`{0}` date, `{1}` app version), `BackupConfirmOlderSchemaNote`,
  `BackupConfirmApiKeyLostNote`, `BackupConfirmButton`, `BackupCancelButton`, `BackupRestoreSucceeded` (`{0}` safety
  file), `BackupRestartNowButton`, `BackupRestartFallback`, `BackupApiKeyNotRestoredNotice`, `BackupErrorNotABackup`,
  `BackupErrorCorrupt`, `BackupErrorNewerSchema` (`{0}` backup app version, `{1}` backup migration id, `{2}` app
  version, `{3}` app migration id), `BackupErrorIoError`, `BackupErrorInsufficientSpace`. Neutral, professional
  Spanish for es-DO; check the repo's actual resx file names with `Glob` first and follow them. (R-17, R-22,
  Reconciliation #5)
- [x] 6a.2 Append MANUAL accessors for every new key to `src/…UI/Resources/Strings.Designer.cs` (hand-maintained; do
  not regenerate). (R-22)
- [x] 6a.3 Write failing then green `tests/…Unit/UI/BackupResourcesTests.cs`: every new key exists in both resx
  files and in `Strings.Designer.cs`, both files define the same key set, no value is empty, and each format string
  formats without exception with the documented number of arguments (`BackupErrorNewerSchema` needs `{0}`..`{3}`).
  Test under es-DO and en, restoring the culture afterwards. (R-22, S-31)
- [x] 6a.4 Wire the localized default title in `AvaloniaFileOpenDialog` to `Strings.BackupRestoreDialogTitle` and the
  backup save default to `Strings.BackupExportDialogTitle` where the VM does not pass one; extend the 5.x tests that
  cover the dialog fallback if they asserted `null`. (R-22, DD-8)
- [x] 6a.5 Write failing `tests/…Integration/Backup/ScopedNotificationCenterSuspendTests.cs` against the REAL
  `ScopedNotificationCenter` on a temp SQLite DB (not a fake of it): `SuspendAsync` waits for an in-flight
  refresh to finish before returning; while suspended `TryRefreshAsync` returns `false`, `GetAsync` returns `null`
  without creating a DI scope (assert scope factory not called), `RefreshAsync` queues on the gate and completes
  only after `Dispose`; disposing the handle re-enables all members and releases the gate exactly once (double
  dispose is safe); no notification row is written while suspended. (R-19, DD-7)
- [x] 6a.6 Modify `src/…UI/Services/INotificationCenter.cs` (append `Task<IDisposable> SuspendAsync(
  CancellationToken ct = default)`), `src/…UI/Services/ScopedNotificationCenter.cs` (gate + `volatile` suspended
  flag per DD-7) and `tests/…Unit/TestDoubles/FakeNotificationCenter.cs` (counts calls, returns a disposable that
  records disposal, order of calls observable). Update every other `INotificationCenter` implementer found by
  `rg "INotificationCenter" src tests`. Run 6a.5 green; existing notification tests stay green. (R-19, DD-7)
- [x] 6a.7 Create `src/…UI/Services/{IAppRestartService,AppRestartService}.cs` per DD-8 (`bool TryRestart()`;
  `dotnet` host adds the entry assembly path; `Process.Start` with `UseShellExecute=false`; then desktop lifetime
  `Shutdown()`; whole body in `try/catch` returning `false`) and register `IAppRestartService` Singleton in the
  `Bootstrap` block. Add `tests/…Unit/TestDoubles/FakeAppRestartService.cs`. Because process launch and lifetime
  shutdown cannot be unit tested, add only the pure-logic test for the argument/host decision (extract a private
  static `BuildStartInfo(processPath, entryAssemblyPath)` made `internal` and exercised through
  `InternalsVisibleTo`); the real relaunch is a MANUAL check recorded in 9.2, and the task notes that
  explicitly. (R-19, DD-8)
- [ ] 6a.8 **Smoke launch on a throwaway data directory** (see the safety rule at the top): app starts in es-DO and
  en, no Backup UI yet, no exception. Do not press any restore button (none exists). (DD-9)
- [x] 6a.9 Run all four gates plus the no-stubs grep over the slice's files (`Resources`, `UI/Services`, tests).
  Confirm the resx completeness test and notification tests ran (N > 0). All green before opening PR S6a. (R-22,
  R-24)

**S6a apply notes:**
- 6a.1: the repo's resx files are `Strings.resx` (neutral, English) and `Strings.es-DO.resx` (Spanish); there is no
  `Strings.en.resx`. 29 keys were added: the 27 from DD-9 plus `BackupRestartRequiredNotice` and `BackupApiKeyKeptNotice`.
- 6a.3: test is `tests/…Unit/UI/BackupResourcesTests.cs` (key set, both cultures, designer, format arity, culture restore).
- 6a.4 NOT ticked: `AvaloniaFileOpenDialog` now defaults to `Strings.BackupRestoreDialogTitle`. The generic
  `AvaloniaFileSaveDialog` default stays `null` (a backup-specific fallback in a generic dialog would be wrong
  coupling); S6b's ViewModel must pass `Strings.BackupExportDialogTitle` explicitly. No existing test asserted `null`.
- 6a.5 deviation from the task text: while suspended `RefreshAsync`/`MarkReadAsync`/`DismissAsync` return immediately as
  no-ops instead of queuing on the gate (a queued call would hang forever after a successful restore, where the handle
  is never disposed). The suspended flag is a counter raised BEFORE waiting on the gate, so no new operation starts
  while the in-flight one drains. Tests live in `tests/…Integration/Backup/ScopedNotificationCenterSuspendTests.cs`
  and use a `GatedScopeFactory` proxy over the real container (real `ScopedNotificationCenter`, real SQLite file).
- 6a.7: `AppRestartService` has an internal constructor with injectable launch/shutdown seams (`Func<ProcessStartInfo,
  bool>`, `Func<bool>`); `BuildStartInfo` is internal and unit tested; `TryRestart` is tested entirely with fake seams.
  The real relaunch stays a MANUAL check (NOT RUN BY HAND). `IAppRestartService` is registered Singleton in `Bootstrap`.
- 6a.8 NOT ticked / not run: this slice touches no AXAML and no new UI is visible, so no smoke launch was performed.

## 6b. BackupSettingsViewModel and Settings view (S6b)

Depends on 6a. Goal: the Backup block in Settings, driven by a fully testable ViewModel with the confirmation
panel, busy guards, silent cancel, error mapping and the restart flow. Invoke the `.claude/skills/frontend-design`
skill for the visual pass before ticking the view task.

- [x] 6b.1 Write failing `tests/…Unit/UI/BackupSettingsViewModelTests.cs` using `FakeBackupService`-backed real use
  cases (real `CreateBackupUseCase`/`InspectBackupUseCase`/`RestoreBackupUseCase` over the fake service),
  `FakeFileSaveDialog`, `FakeFileOpenDialog`, `FakeNotificationCenter`, `FakeAppRestartService` and `FakeClock`:
  (a) **export cancel**: save dialog returns `null`: no use case call, no status or error message, `State` Idle,
  no file created (S-28, R-20); (b) export success sets `BackupExportSucceeded` with the file display name; suggested
  name equals `BackupFileName.Create(clock.UtcNow, TimeZoneInfo.Local)` and the zip filter is passed (S-4, R-2);
  (c) export failure shows the mapped localized message and `IsError` (R-17); (d) **restore pick cancel**: open
  dialog `null`: no inspect call, no message, Idle (S-28); (e) inspect success sets `PendingInfo`, state
  `ConfirmPending`, `PendingSummary` contains the localized backup date and app version, and NO restore call is
  made (S-26, R-19); (f) inspect failure for each code in a theory over `NotABackup`, `Corrupt`, `NewerSchema`,
  `IoError`, `IoError+insufficientSpace` and an unknown code, under es-DO and en: no panel, the localized message,
  never raw exception text or a path (S-27, R-17); the `NewerSchema` message contains all four values (backup app
  version, backup migration id, app version, app migration id) (S-2, R-17); (g) `CancelRestore` disposes the
  `OpenTarget`, clears `PendingInfo`, writes nothing, state Idle (S-26); (h) `ConfirmRestore` acquires
  `SuspendAsync` BEFORE calling the restore use case (ordered call log), keeps the suspension on success and
  disposes it on failure (DD-7); (i) success moves to `RestartRequired`, shows `BackupRestoreSucceeded` with the
  safety file name, and appends `BackupApiKeyNotRestoredNotice` when `RestoreOutcome.ApiKeyLost` (S-24, R-15);
  (j) `RestartNow` with the fake returning `false` sets `RestartFailed` (shows `BackupRestartFallback`); with
  `true`, no message is required (S-30); (k) restart-required is terminal: `Export`, `ChooseRestoreFile` and
  `ConfirmRestore` are no longer executable, `CanExecute` false (Reconciliation #8); (l) **double click**: two
  concurrent `ExecuteAsync` calls on `Export` (dialog blocked on a TCS) produce exactly one dialog call and one use
  case call; same for `ConfirmRestore` (S-29, R-21); (m) `CanExecute` matrix per state, and while Restoring all
  other backup commands are disabled (R-21); (n) an unexpected exception inside any command maps to the `IoError`
  text, is logged, and the state returns to Idle (never stuck busy); `OperationCanceledException` shows no failure
  message; (o) `IsBusy` is true only in Exporting/Inspecting/Restoring. (R-17, R-19..R-21, S-2, S-24, S-26..S-30)
- [x] 6b.2 Create `src/…UI/ViewModels/BackupSettingsViewModel.cs` (Transient) per DD-9: `BackupPanelState` enum,
  `[ObservableProperty]` state with `NotifyCanExecuteChangedFor` on all five commands, `IsBusy`,
  `StatusMessage`, `IsError`, `PendingInfo`, `PendingSummary`, `RestartFailed`, `_pendingSource`, `_suspension`;
  commands `Export`, `ChooseRestoreFile`, `ConfirmRestore`, `CancelRestore`, `RestartNow`, each starting with a
  synchronous state check before the first `await`, `try/catch/finally`, and the error-code mapper (unknown code →
  generic `BackupErrorIoError`). No logic in code-behind. Run 6b.1 green. (R-17, R-19..R-21, R-23, DD-9)
- [x] 6b.3 Modify `src/…UI/ViewModels/SettingsViewModel.cs`: append the optional ctor parameter
  `BackupSettingsViewModel? backup = null`, expose `Backup`. Write failing test in the existing Settings VM tests
  file (appended): `Backup` is exposed when provided and `null` otherwise, and the two existing fixture files
  (`SettingsViewModelTests.cs`, `SettingsViewModelTestConnectionTests.cs`) compile and pass unchanged; add a
  resolved-from-container test that the real `Bootstrap` injects a non-null `Backup`. Register
  `BackupSettingsViewModel` Transient in the `Bootstrap` block. (R-23, DD-9)
- [x] 6b.4 Modify `src/…UI/Views/SettingsView.axaml`: one new `<Border Classes="ledger-row" Padding="16">`
  block placed BEFORE "Acerca de" (About stays last) with `DataContext="{Binding Backup}"`,
  `x:DataType="vm:BackupSettingsViewModel"`, `IsVisible` bound with `ObjectConverters.IsNotNull` on the host
  binding, title and description, Export and Restore buttons, indeterminate `ProgressBar` bound to `IsBusy`, status
  line (Terracotta when `IsError`), confirmation panel (Amber warning, `PendingSummary`, destructive-styled Confirm
  and a Cancel), restart panel (`RestartNow` plus the fallback text visible on `RestartFailed`). All text via
  `Strings` bindings, zero hardcoded literals, zero code-behind logic. Invoke `.claude/skills/frontend-design`
  before finalizing; record what it changed. Verify compiled bindings (`dotnet build` shows no AVLN warnings) and
  add an XAML-content test (string scan or loaded resource) asserting the block exists once before "Acerca de".
  (R-22, R-23, DD-9, Reconciliation #6)
- [ ] 6b.5 **Smoke launch in both locales on a throwaway data directory** (see the safety rule at the top): launch
  twice with the override (es-DO, then en), open Settings, confirm the Backup block renders and toggles nothing;
  it is permitted to press "Export" only if the save dialog target is a throwaway folder; it is FORBIDDEN to
  complete a restore here (restore validation is covered by integration tests; the real restore is a manual check
  on a COPY of the data in 9.2). Record screenshots or console output. If the data-dir override is unavailable,
  press no buttons and say so. (R-22, R-23)
- [x] 6b.6 Run all four gates plus the no-stubs grep over the slice's files (`ViewModels`, `Views`, tests).
  Confirm N > 0 for `BackupSettingsViewModelTests` and `BackupResourcesTests`. All green before opening PR S6b.
  (R-21..R-24)

**S6b apply notes:**
- 6a.4 ticked: `AvaloniaFileOpenDialog` defaults to `Strings.BackupRestoreDialogTitle` (S6a) and the ViewModel passes
  `Strings.BackupExportDialogTitle` (save) and `Strings.BackupRestoreDialogTitle` (open) explicitly; asserted by
  `Export_Success_ShowsFileName_AndPassesSuggestedNameFilterAndTitle` and
  `ChooseRestoreFile_InspectSuccess_ShowsConfirmation_WithoutRestoring`.
- 6a.8 stays open: S6b's launch (see 6b.5) covered the default locale only, so the "es-DO and en" wording of 6a.8 is not met.
- 6b.2/6b.3 deviation: `BackupSettingsViewModel` is registered as a SINGLETON, not Transient. `NavigationService`
  re-resolves transient view-models on every visit, so a fresh instance would come back in `Idle` after a successful
  restore and allow a second restore, which would wait forever on the notification suspension held until process exit
  (and a second export over the restored file). Singleton keeps `RestartRequired` terminal for the session
  (Reconciliation #8). The use cases it depends on stay Transient (stateless). Guarded by
  `BackupCompositionTests.BackupSettingsViewModel_IsASingleton_...`.
- 6b.2: the ViewModel has an extra optional last ctor parameter `string? appVersion = null` (defaults to the assembly's
  informational version, the same value stored in manifests) so the newer-schema message can show "this app's version"
  and tests can pin it. `WorkingMessage` (derived from `State`) carries the progress caption so `StatusMessage` stays
  empty after a cancelled picker. `AreActionsVisible` hides the export/restore buttons once `RestartRequired` is reached.
- 6b.2: on success, `RestoreOutcome.ApiKeyLost` appends `BackupApiKeyNotRestoredNotice`; `ApiKeyCarriedOver` appends
  `BackupApiKeyKeptNotice`; `BackupInfo.LocalApiKeyWillBeLost` adds `BackupConfirmApiKeyLostNote` to the confirmation
  summary. `BackupRestartRequiredNotice` (S6a key) is shown in the restart panel. No new resx key was needed.
- 6b.3: the "resolved from the container" check asserts the singleton registration and that the container can supply the
  `SettingsViewModel` ctor parameter (`IServiceProviderIsService`); it deliberately does not construct `SettingsViewModel`
  because its constructor starts a load against the (real) profile database.
- 6b.4: frontend-design pass within the existing "Libro Mayor" system (no new tokens/colors, no new styles): title in
  `ledger-title`, description/progress captions in `ledger-status`, Export = `ledger-btn-primary`, Restore = `ledger-btn`,
  thin amber indeterminate `ProgressBar`, failure text via `Classes.export-error`, confirmation and restart panels as
  amber-bordered inset cards (3 px leading edge), destructive confirm = `ledger-btn-danger` with a terracotta outline,
  Cancel placed first so the destructive action is the last, least reachable button. The visibility binding sits on the
  outer Border because the inner panel re-targets `DataContext` to `Backup`. `dotnet build` shows 0 warnings (compiled
  bindings resolve). Checked by `SettingsViewBackupBlockTests` (single block, after Notificaciones, before Acerca de,
  About still last, no hardcoded literals, all five commands bound).
- 6b.5 NOT ticked: launched with `CONTROLDEFINANZASIA_DATA_DIR` on a scratchpad dir, default locale only, ~22 s, no
  unhandled exception (only the known EF rebuild-table warnings), then killed; the real `%APPDATA%` listing was identical
  before/after. No computer-use tooling was available to this executor, so Settings was NOT opened and NO screenshot was
  taken: the visual pass (both locales) remains a manual check for 8.5.
- Strict TDD honesty: the test file was written before the ViewModel, but the tests were NOT executed in a red state
  (they referenced types that did not exist yet, so they could not compile). The ViewModel was then written in one step
  and all tests passed on the first run: there was no intermediate red/green cycle per behavior.

## 7. End-to-end and docs (S7)

Depends on 6b. Goal: the roadmap completion criterion is proven through real DI, and docs match the shipped names.

- [ ] 7.1 Write failing then green `tests/…Integration/Backup/BackupRestoreEndToEndTests.cs` using real DI
  (`AddPersistence` + `AddBackup`, real `EfMigrationCatalog`, `PhysicalBackupFileSystem`): seed database A in every
  table incl. `Settings` with `ai.apiKey` → `CreateBackupUseCase` to a real file → create a FRESH data directory
  with a migrated empty DB → `RestoreBackupUseCase` → `AppDbContext.Database.Migrate()` (the `ApplyMigrations`
  equivalent) → every table has identical rows to A, `Settings` minus `ai.apiKey` (the fresh DB has no key to carry
  over so the key row is absent), `CompletePendingRestore()` deletes the `.bak`. Additional case: a fresh directory
  whose live DB HAS a key retains that local key after the restore (S-23). Row comparison is by table via
  `sqlite_master` enumeration so a table added later is covered automatically. (R-16, S-1, S-23, S-25)
- [ ] 7.2 Write failing then green end-to-end negative cases in the same file, through the use cases: a future-schema
  backup gives `NewerSchema` with both versions and the live SHA-256 unchanged (S-2); a backup with one flipped byte
  gives `Corrupt` with the live SHA-256 unchanged (S-12). (R-10, S-2, S-12)
- [ ] 7.3 Update `docs/architecture.md` §Backup: replace `ExportDataUseCase` / `ImportBackupUseCase` /
  `BackupService` with the shipped `CreateBackupUseCase`, `InspectBackupUseCase`, `RestoreBackupUseCase`,
  `SqliteBackupService` and mention `IBackupService`, `IMigrationCatalog`, `IFileOpenDialog`, `IAppRestartService`
  and `INotificationCenter.SuspendAsync`; state explicitly that backup is not report export (Phase 18); document the
  restore pipeline order, `.bak` lifecycle and the MANUAL RECOVERY steps (close the app; copy
  `data.db.pre-restore.bak` over `data.db`, or unzip `data.db` from `backups\pre-restore-*.zip`; what to do after a
  failed post-restore migration). (R-14, R-25, D-21)
- [ ] 7.4 Update `docs/roadmap.md` §Fase 19: mark completion criteria (roundtrip, future-version rejection) complete
  with evidence pointers, and list follow-ups: shell-wide "restart required" banner (T14), automatic revert after a
  failed post-restore migration, migration-failure startup dialog. (R-25, R-26)
- [ ] 7.5 Create/update `spec/phases/phase-19/features.md` (the untracked folder already exists; read it first) to
  reflect the final state, following the pattern of `spec/phases/phase-10/` through `phase-18/`. (R-25)
- [ ] 7.6 Confirm no new package, migration, setting or schema change: `git diff --name-only <phase base>^ HEAD`
  filtered by `Directory.Packages.props`, `.csproj`, `Migrations/`, `SettingKeys`, `AppSettings` is empty; Phase 18
  export behavior unchanged apart from the additive `IFileSaveDialog` overload. (R-24, R-26)
- [ ] 7.7 Run all four gates plus the no-stubs grep across the ENTIRE phase diff:
  `rg -n "NotImplementedException|TODO|FIXME|HACK" -g "*.cs" -g "*.axaml"` limited to the files changed in phase 19
  returns nothing. All green before opening PR S7. (R-24)

## 8. Cierre

- [ ] 8.1 Run the full gate suite across the whole phase: `dotnet build -c Release` (0 warnings/errors),
  `dotnet test -c Release --no-build` (unit + integration; record counts and confirm Backup tests executed, none
  skipped except Windows-only tests on non-Windows), `dotnet format style --verify-no-changes` exit 0, NetArchTest
  R-1..R-5 plus the D20 Sqlite guard, resx completeness. Record final counts. (R-22, R-24, S-31, S-32)
- [ ] 8.2 Manual check on Windows with the REAL native dialogs, both locales, recording screenshots (cannot be
  automated, no headless Avalonia harness): Export opens the native save dialog with the suggested
  `ControlDeFinanzasIA-backup-yyyyMMdd-HHmm.zip` in local time and the ZIP filter; cancel writes nothing and shows no
  message; the exported file opens in Explorer as a ZIP with exactly two entries; Restore opens the native open
  dialog, cancel is silent; picking a non-backup file shows the localized `NotABackup` message and changes nothing.
  Mark "NOT RUN BY HAND" if not performed. (R-19, R-20, S-26..S-28)
- [ ] 8.3 Manual check of a REAL restore with relaunch, on a COPY of the data only: copy
  `%APPDATA%\ControlDeFinanzasIA` to a throwaway folder, run the app pointed at the copy (data-dir override from
  5.9), restore an older backup, confirm the panel shows date and destructive warning, confirm; verify "Restart
  now" relaunches and the app reopens with the restored data and `data.db.pre-restore.bak` gone; then a second
  attempt in a `dotnet run` host to observe either the relaunch or the "close and reopen" fallback text; verify a
  safety ZIP appeared under `<copy>\backups\`. NEVER run this against the real profile. Mark "NOT RUN BY HAND" if
  not performed. (R-14, R-19, S-25, S-30)
- [ ] 8.4 Manual check of a locked file: with the throwaway copy, hold `data.db` open from another process (Explorer
  preview or a `FileStream` script) and attempt a restore: `IoError` message, data intact, safety ZIP kept; then
  release and retry successfully. Mark "NOT RUN BY HAND" if not performed. (R-13, S-21)
- [ ] 8.5 Manual visual check of the Settings Backup block in es-DO and en (spacing, contrast, destructive panel
  emphasis, indeterminate progress, restart panel), recorded with screenshots. Mark "NOT RUN BY HAND" if not
  performed. (R-22, R-23)
- [ ] 8.6 Update `docs/AI_HANDOFF.md` with the Phase 19 summary, including: the live file is written by exactly one
  `File.Replace`; the pipeline order and `.work` directory on the data volume; `Pooling=False` plus
  `ClearAllPools()`; safety backups keep the API key while user exports redact it; four error codes with
  `insufficientSpace` as a detail; `SuspendAsync` is held until process exit on success; `IAsyncRelayCommand
  .ExecuteAsync` bypasses `CanExecute`, hence the body guard; manual recovery paths; smoke launches must use a
  throwaway data dir; and any gotchas hit during apply. (Reconciliation #3, #4, #7, #8)
- [ ] 8.7 Update `AGENTS.md` project status: Phase 19 shipped (backup ZIP with manifest, restore pipeline, safety
  backup retention 3, confirmation panel, restart flow, test counts) and Phase 20 (CSV import) as next; remove the
  "Phase 18 is next" wording if present. Edited in place without restructuring. (R-25)
- [ ] 8.8 Write `verify-report.md` in `.atl/sdd/f-019p19-backup/`, independently re-verifying every Acceptance
  Criteria checklist line against source and tests (read the code, not the checklists), including all 12
  Reconciliation points, the anti-stub grep over the whole phase diff, the data-loss invariants (S-33) by running the
  failure-injection and real-lock tests, and the manual checks 8.2 through 8.5 (mark honestly any that were not run
  by hand). (Acceptance Criteria)
- [ ] 8.9 Archive the spec (`F-019-backup-restore.spec.md` → `.done.md` with `git mv`, acceptance criteria checked
  with evidence) and write `archive-report.md` in `.atl/sdd/f-019p19-backup/`. Only after 8.1 and 8.8 pass.
  (Acceptance Criteria)
