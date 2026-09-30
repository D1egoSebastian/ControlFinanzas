# F-019 — Backup and Restore

> **Phase:** 19 — Backup and restore
> **Status:** Completed and archived (2026-09-29). Verify: PASS WITH WARNINGS, 0 critical. Manual native-dialog and relaunch checks NOT run by hand (see Acceptance Criteria).
> **SDD:** `.atl/sdd/f-019p19-backup/`
> **Input:** `.atl/sdd/f-019p19-backup/proposal.md` (D1-D21, S1-S7 binding), `.atl/sdd/f-019p19-backup/exploration.md`
> **Depends on:** Phases 0-18 archived (Settings table and `ai.apiKey`, migrations, `IFileSaveDialog` stream-based dialog precedent from Phase 18).
> **Blocks:** Phase 20 (CSV import reuses `IFileOpenDialog`). Not to be confused with Phase 18 report export.
> **Roadmap:** `docs/roadmap.md` §Fase 19; `docs/architecture.md` §Backup; ADR-008, ADR-009.

## Goal

All of the user's financial history lives in one local SQLite file (`<DataDir>\data.db`). Phase 19 adds two
manual actions in Settings: "Export everything to a file" (a ZIP) and "Restore from a file". Restore replaces
the whole database and is the most dangerous operation in the app. The governing rule is: **never lose data**.
The live file is not touched until the backup is fully validated, a safety copy exists first, and every
rejection or failure leaves the live bytes unchanged.

## Grounding in current code (binding facts)

- `Result` is NON-generic (`IsSuccess`, `Value: object?`, `Error?`). No `Result<T>`.
- `IClock` exposes only `UtcNow` and `Today`; there is no local `Now`.
- `IFileSaveDialog` is stream-based (`SaveTarget.OpenWriteAsync`), with no file path. Backup ports are stream-based.
- No dialog/confirmation infrastructure exists; confirmation is an inline panel in the view.
- `IPathResolver` (Application) exposes `DataDirectory` and `DatabaseFilePath`; implementation is in UI, injected into Infrastructure by DI.
- The migration catalog lives inside Infrastructure (UI never references EF Core, rule R-4).
- Journal mode is DELETE (no WAL). `Microsoft.Data.Sqlite` is already a transitive and directly used dependency; no new NuGet package.
- `App` currently calls `Bootstrap.ApplyMigrations` without try/catch.

## Requirements

### R-1 — Ports, records and use cases in Application, library-free

The system MUST define in `Application/Abstractions/`:

- `IBackupService` with `CreateAsync(Stream destination, CancellationToken)`, `InspectAsync(Func<CancellationToken, Task<Stream>> openRead, CancellationToken)` returning the `BackupInfo`, `RestoreAsync(Func<CancellationToken, Task<Stream>> openRead, CancellationToken)`, and `CompletePendingRestore()`.
- `IFileOpenDialog` with `PickAsync(FileTypeFilter, CancellationToken)` returning `OpenTarget?` (`null` = cancelled; `OpenTarget` exposes a stream opener and mirrors `SaveTarget`).
- `IFileSaveDialog` gains a generic `PickAsync(suggestedName, FileTypeFilter, CancellationToken)` overload; the existing `ReportFormat` overload MUST delegate to it and keep its behavior. Existing fakes MUST be updated in the same slice. `ReportFormat` MUST stay report-only.
- `IMigrationCatalog` returning the set of `MigrationId`s known to the running assembly.
- Records `BackupManifest` and `BackupInfo` (manifest fields, the backup date, and the schema versions needed for messages).
- `BackupErrorCodes` (see R-17).
- Use cases `CreateBackupUseCase`, `InspectBackupUseCase`, `RestoreBackupUseCase`, all returning the non-generic `Result`.

No type from `Microsoft.Data.Sqlite`, EF Core, `System.IO.Compression` or Avalonia MAY appear in Application. Failures MUST be returned as `Result.Failure` with a code from R-17, never thrown across the use case boundary.

### R-2 — Backup file format and manifest

A backup MUST be a ZIP named by default `ControlDeFinanzasIA-backup-yyyyMMdd-HHmm.zip` containing exactly two entries at the archive root: `manifest.json` and `data.db`. The manifest MUST contain:

| Field | Value |
| --- | --- |
| `schemaVersion` | The latest applied `MigrationId` in the exported copy (informational) |
| `exportedAt` | `IClock.UtcNow` formatted ISO 8601 round-trip (`"O"`) |
| `appVersion` | The running application version |
| `dbSha256` | Lower-case hex SHA-256 of the exact `data.db` bytes stored in the ZIP |
| `apiKeyIncluded` | `false` |

The suggested file name timestamp MUST be the local time derived as `TimeZoneInfo.ConvertTimeFromUtc(IClock.UtcNow, TimeZoneInfo.Local)`. `exportedAt` MUST be UTC.

### R-3 — Consistent snapshot while a writer is active

Creation MUST NOT zip the live file. It MUST copy the database with `SqliteConnection.BackupDatabase` into a temporary `.db` file (same data directory) using its own connection with `Pooling=False`, and MUST NOT enable WAL. A backup taken while another connection is committing writes MUST yield a copy that passes `PRAGMA integrity_check`.

### R-4 — Integrity check of the copy before packaging

Before packaging, the temporary copy MUST pass `PRAGMA integrity_check` returning exactly `ok`. If it does not, creation MUST fail with `Corrupt`, write nothing to the destination stream, and delete the temporary file.

### R-5 — SHA-256

`dbSha256` MUST be computed over the final bytes of `data.db` as written into the ZIP (after redaction and VACUUM, R-6). Inspecting or restoring a backup MUST recompute the hash of the extracted bytes and compare it in a case-insensitive comparison to the manifest value.

### R-6 — API key redaction leaves no key bytes

On the temporary copy only, and before hashing, creation MUST execute in order: `PRAGMA secure_delete=ON`, `DELETE` of the `ai.apiKey` row from `Settings` (if the table exists), then `VACUUM`. The live database MUST never be modified. The exported ZIP, byte-scanned in both its raw and decompressed forms, MUST NOT contain the key's byte sequence (UTF-8 and UTF-16LE). All other `Settings` rows MUST be preserved.

### R-7 — Temp file hygiene on create

Every temporary file created during creation MUST be deleted on success, failure and cancellation. Creation writes to the destination stream only after all checks pass, so a failed creation leaves the destination without partial content.

### R-8 — Restore validation pipeline, strict order

`RestoreAsync` MUST re-run the full validation (it MUST NOT trust a prior `InspectAsync`) and MUST execute these steps in this order, stopping at the first failure:

1. **ZIP shape:** the stream is a readable ZIP; it contains exactly `manifest.json` and `data.db`, no other entries, no directories, and no path separators or `..` in entry names; the manifest parses and has all required fields. Otherwise `NotABackup`.
2. **Zip-slip guard:** any entry whose full extracted path would fall outside the temp directory is rejected (`NotABackup`). Extraction MUST use a fixed destination file name, never the entry name.
3. **Size cap:** `manifest.json` uncompressed size <= 64 KiB and `data.db` uncompressed size <= 1 GiB, enforced on both the declared size and the bytes actually read (a lying header MUST NOT bypass the cap). Otherwise `NotABackup`.
4. **Hash:** extract `data.db` to a temp file in the live database's directory (same volume), compute SHA-256, compare with `dbSha256`. Mismatch => `Corrupt`.
5. **integrity_check:** open the temp file read-only; `PRAGMA integrity_check` must return `ok`, else `Corrupt`.
6. **foreign_key_check:** `PRAGMA foreign_key_check` must return zero rows, else `Corrupt`.
7. **Migration-history comparison** (R-9).

Only after all seven steps succeed MAY the pipeline proceed to the free-space check (R-11) and the safety backup (R-12). `InspectAsync` runs steps 1-7 and returns `BackupInfo` without any write outside its own temp directory.

### R-9 — Migration-history comparison

The service MUST read the applied `MigrationId`s from the `__EFMigrationsHistory` table of the extracted copy and compare them as sets against `IMigrationCatalog`:

- Any applied id NOT known to the catalog => `NewerSchema` (hard reject). The message MUST show both versions: the backup's latest applied id and the app's latest known id.
- All applied ids known (proper subset or equal) => accept. An older backup is migrated by the normal startup `Migrate` at the next start.
- Comparison MUST be by `MigrationId` set membership, never by counts or dates. A backup with no `__EFMigrationsHistory` table MUST be rejected with `NotABackup`.
- The catalog MUST be implemented in Infrastructure from the assembly-defined migrations (`Database.GetMigrations()`) and MUST NOT open a database connection.

### R-10 — Live database byte-identical after every rejection

For every failure at or before the swap (garbage stream, missing/extra entry, malformed manifest, zip-slip, oversized entry, bad hash, corrupt database, foreign-key violation, newer schema, insufficient space, safety-backup failure, swap failure), the live `data.db` MUST be byte-identical to its state before the call (compared by SHA-256), no `data.db-journal` MUST be left behind by the operation, and all temp files MUST be deleted.

### R-11 — Free-space pre-check

Before extracting and swapping, the service MUST verify the volume holding the data directory has at least 3x the size of the larger of the live database and the incoming `data.db` free. Otherwise it MUST fail with `InsufficientSpace` and change nothing. The check MUST be behind a seam so tests can simulate low space.

### R-12 — Pre-restore safety backup and retention of 3

After validation succeeds and before any change to the live file, the service MUST create a safety backup at `<DataDir>\backups\pre-restore-<yyyyMMdd-HHmmss>.zip` using the same creation pipeline as R-2..R-6 (so it is a valid, restorable backup; the API key is redacted, consistent with D3). If the safety backup fails, restore MUST abort with `IoError` (or `Corrupt`) and R-10 applies. After each successful safety backup, only the 3 newest `pre-restore-*.zip` files (by timestamp in name, ties broken by write time) MUST remain. Retention MUST NOT touch any other file, and user-exported backups MUST never be deleted by the app.

### R-13 — Connection release, atomic swap, retry and rollback

The service's own connections MUST use `Pooling=False`. The swap sequence MUST be:

1. The UI has the backup commands busy and disabled (R-21).
2. `SqliteConnection.ClearAllPools()` to close idle pooled handles.
3. If `<live>-journal` exists, the swap MUST NOT proceed over it: wait and retry per step 5; if it persists after the last attempt, fail with `IoError`. A hot journal MUST never be rolled into the restored database.
4. `File.Replace(tempRestored, live, live + ".pre-restore.bak", ignoreMetadataErrors: true)`.
5. On `IOException` or `UnauthorizedAccessException`, repeat steps 2-4 up to 3 attempts with backoff of 200, 600 and 1800 ms. The delays MUST be injectable so tests do not sleep.
6. On final failure: delete the temp file, leave the live file untouched (the replace is atomic: replaced or unchanged), return `IoError`. The safety backup from R-12 MUST be kept.

Any failure after the safety backup was created MUST roll back to the original bytes (live file identical per R-10) and report the failure.

### R-14 — `.bak` lifecycle

After a successful swap, `<live>.pre-restore.bak` MUST be kept. `CompletePendingRestore()` MUST delete it, and MUST be called by `App` only after `Bootstrap.ApplyMigrations` has completed successfully at startup. If migrations throw, the `.bak` and the safety ZIP MUST remain. `CompletePendingRestore()` MUST NOT throw when the file is absent or cannot be deleted (it logs and returns). Automatic revert after a failed post-restore migration is out of scope; manual recovery (the `.bak` and the safety ZIP) MUST be documented in `docs/architecture.md`.

### R-15 — Keep the local API key on restore

If the incoming database has no `ai.apiKey` value and the live database has one, the service MUST write the local value into the extracted temp restore database (never into the live file directly) before the swap, so the restored database contains the local key. If the incoming database has a key (for example a legacy or hand-made file), it MUST be kept as is. If the incoming database predates the `Settings` table (migration `AddSettings` not in its history), the key MUST NOT be carried over, the restore MUST still succeed, and the result MUST indicate that the key must be re-entered so the UI can say so (R-22). All other rows come from the backup.

### R-16 — Roundtrip identity

Export from database A, then restore into a fresh data directory (a different `data.db`), MUST yield a database whose every table (including `Settings`, excluding the redacted API key row and excluding `__EFMigrationsHistory` differences only if the backup is older) has the same rows and values as A. This is the roadmap completion criterion.

### R-17 — Typed failure codes and localized messages

`BackupErrorCodes` MUST include at least: `NotABackup`, `Corrupt`, `NewerSchema`, `IoError`, `InsufficientSpace`. Every failure `Result` from the backup use cases MUST carry exactly one of these codes. The ViewModel MUST map every code to a localized resx message (es-DO and en) and MUST NOT show raw exception text or paths. `NewerSchema` messages MUST include the backup's version and the app's version. Unknown codes MUST map to a generic localized failure message.

### R-18 — Inspect feeds the confirm step

`InspectBackupUseCase` MUST return a `BackupInfo` containing `exportedAt`, `appVersion`, `schemaVersion` and the data size, used only to fill the confirmation panel. It MUST not write to the data directory.

### R-19 — Restart-required UX with confirmation panel

There is no live reload. After a successful restore the VM MUST show a restart-required state. Before restore starts, the VM MUST show an inline confirmation panel (state `PendingRestore` holding the `BackupInfo`) that displays the backup's date and a destructive warning (current data will be replaced; a safety copy is created first), with Confirm and Cancel actions. Restore MUST NOT start until the user confirms. Cancel clears `PendingRestore` and writes nothing. After success, actions "Restart now" and a static "close and reopen the app" message MUST be available. `IAppRestartService` (UI) relaunches and shuts down the desktop lifetime; it MUST return `false` and never throw on failure, and the VM MUST then show the "close and reopen" fallback message.

### R-20 — Cancelled dialogs are not errors

If either the save or open dialog returns `null`, the VM MUST return to idle, write nothing, run no use case, and show NO status or error message. A cancelled save MUST NOT leave an empty or partial file.

### R-21 — Busy guards

Every backup command (`Export`, `Restore`/pick, `ConfirmRestore`, `CancelRestore`, `RestartNow`) MUST start with `if (IsBusy) return;` (or equivalent CanExecute plus guard), set `IsBusy` in `try/finally`, and be disabled while busy. A double click MUST invoke the use case exactly once. While a restore is running all other backup commands MUST be disabled. Progress is an indeterminate busy indicator plus a status line; no percentage.

### R-22 — Localization

All new UI strings MUST be in `Strings.resx` (es-DO default) and `Strings.en.resx`, with manual accessors added to `Strings.Designer.cs`. No hardcoded UI text. A test MUST assert that every new key exists in both cultures and is non-empty. The "API key must be re-entered" notice and every R-17 message are included.

### R-23 — Settings section and ViewModel

A separate `BackupSettingsViewModel` MUST be hosted by the Settings screen, rendered as a new `<Border Classes="ledger-row">` block at the end of `SettingsView.axaml`. It MUST have no logic in code-behind and MUST be fully testable without a UI. `SettingsViewModel` fixtures MUST be updated for the added dependency. The `frontend-design` skill MUST be applied for the visual pass.

### R-24 — Architecture rules

- Application and UI MUST NOT reference `Microsoft.Data.Sqlite`, and UI MUST NOT reference EF Core or Infrastructure types (`ArchitectureTests` forbidden lists updated accordingly, and R-1..R-5 stay green).
- `SqliteBackupService`, `EfMigrationCatalog` and `AddBackup()` live in `Infrastructure/Backup/`. The service uses its own connections, not `AppDbContext`, and restore MUST NOT go through `Bootstrap.ApplyMigrations`.
- No new NuGet package; `Directory.Packages.props` and `.csproj` files are unchanged.

### R-25 — Docs

`docs/architecture.md` §Backup MUST be updated: names `ExportDataUseCase`/`ImportBackupUseCase`/`BackupService` replaced by the D7 names, explicit statement that backup is not report export, manual recovery steps. `docs/roadmap.md`, `AGENTS.md` and `spec/phases/phase-19/features.md` reflect the final state.

### R-26 — Explicit scope exclusions

Phase 19 MUST NOT add: scheduled/auto backup, cloud sync, encrypted or password-protected ZIP, API-key encryption, downgrade of newer schemas, partial/selective restore, merge into an existing database, live reload, CSV import, backup before destructive migrations, multi-process lock, automatic revert after a failed post-restore migration, or a migration-failure startup dialog.

## Scenarios

### S-1 — Roadmap: Roundtrip export -> fresh directory -> restore
- Given database A populated in every table (accounts, categories, expenses, incomes, budgets, goals, debts, recurring, notifications, saved views, tags, Settings with an `ai.apiKey`)
- When a backup is created, then restored into a fresh data directory
- Then every table in the restored database equals A row for row, except the `ai.apiKey` row which is absent; and `CompletePendingRestore()` deletes the `.bak`.

### S-2 — Roadmap: future-version backup is rejected with both versions
- Given a backup whose database has an applied `MigrationId` unknown to `IMigrationCatalog`
- When it is inspected or restored
- Then the result is `NewerSchema`, the localized message shows the backup version and the app version, and the live database is byte-identical.

### S-3 — Manifest contents
- Given a backup created at a known `IClock.UtcNow`
- When the ZIP is opened
- Then it has exactly `manifest.json` and `data.db`, `exportedAt` is the ISO 8601 UTC instant, `apiKeyIncluded` is `false`, `schemaVersion` is the latest applied `MigrationId`, and `dbSha256` equals the SHA-256 of the zipped `data.db`.

### S-4 — Suggested file name uses local time
- Given a UTC instant and a non-UTC local zone
- When the save dialog is invoked
- Then the suggested name is `ControlDeFinanzasIA-backup-yyyyMMdd-HHmm.zip` in local time.

### S-5 — API key bytes are absent from the export
- Given a live database with `ai.apiKey = "sk-test-SECRET-123"` in a record later deleted (free pages likely)
- When a backup is created
- Then neither the raw ZIP bytes nor the decompressed `data.db` contain the key in UTF-8 or UTF-16LE, the other Settings rows survive, and the live database still holds the key.

### S-6 — Backup while a writer is active
- Given a second connection inserting rows in a loop
- When a backup is created
- Then creation succeeds, the copy passes `integrity_check`, and it contains a transactionally consistent set of rows.

### S-7 — Corrupt copy aborts creation
- Given the integrity check of the temp copy fails (seam)
- When a backup is created
- Then the result is `Corrupt`, the destination stream received no bytes, and no temp file remains.

### S-8 — Garbage and non-ZIP input
- Given random bytes or an empty stream
- When restore is invoked
- Then `NotABackup`, live database byte-identical.

### S-9 — Missing or extra entries
- Given a ZIP missing `manifest.json`, or missing `data.db`, or with an extra entry
- When restored
- Then `NotABackup`, live database byte-identical.

### S-10 — Zip-slip
- Given a ZIP with an entry named `..\..\evil.db` or `/abs/data.db`
- When restored
- Then `NotABackup`, no file is written outside the temp directory, live database byte-identical.

### S-11 — Size caps
- Given a `data.db` entry with a declared size above 1 GiB, and separately an entry whose header lies (small declared, larger actual)
- When restored
- Then both are rejected before the live file is touched, and reading stops at the cap; a manifest above 64 KiB is rejected.

### S-12 — Hash mismatch
- Given a valid backup with one byte of `data.db` flipped
- When restored
- Then `Corrupt`, live database byte-identical.

### S-13 — Corrupt database with valid hash
- Given a ZIP whose manifest hash matches but the database file is damaged (or violates a foreign key)
- When restored
- Then `Corrupt` from step 5 or 6, live database byte-identical.

### S-14 — Validation order
- Given a ZIP that is both oversized and has a wrong hash
- When restored
- Then the reported code is the size-cap outcome (earlier step), proving the fixed order of R-8.

### S-15 — Older backup is accepted
- Given a backup whose migration history is a proper subset of the catalog
- When restored and the app next starts
- Then restore succeeds, and the startup `Migrate` applies the missing migrations keeping all data.

### S-16 — Backup has no migration history
- Given a valid SQLite file without `__EFMigrationsHistory`
- When restored
- Then `NotABackup`, live database byte-identical.

### S-17 — Safety backup and retention
- Given `backups\` already holds 3 `pre-restore-*.zip`
- When a restore succeeds
- Then a new safety ZIP exists (valid, restorable), the oldest is removed, exactly 3 remain, and any other file in the folder is untouched.

### S-18 — Safety backup failure aborts
- Given the safety backup cannot be written (seam)
- When restore runs
- Then the result is a failure, the live database is byte-identical, and no swap was attempted.

### S-19 — Insufficient free space
- Given a free-space seam reporting less than 3x the database size
- When restore runs
- Then `InsufficientSpace`, live database byte-identical, no safety backup created.

### S-20 — Swap retry with backoff
- Given the first two swap attempts throw `IOException` and the third succeeds
- When restore runs
- Then delays 200 ms and 600 ms were requested, pools were cleared before each attempt, the swap succeeded, and the `.bak` exists.

### S-21 — Swap fails permanently
- Given every swap attempt throws
- When restore runs
- Then three attempts occurred, the result is `IoError`, the live database is byte-identical, the temp file is deleted, and the safety ZIP remains.

### S-22 — Leftover journal file
- Given `data.db-journal` exists next to the live file
- When restore reaches the swap and the journal persists
- Then no swap occurs, `IoError`, live database and the journal are unchanged.

### S-23 — Local API key survives restore
- Given a backup with no `ai.apiKey` and a live database with a key
- When restore succeeds
- Then the restored database contains the local key; the live file was not written outside the swap.

### S-24 — Old backup predates Settings
- Given a backup taken before `AddSettings`
- When restored
- Then restore succeeds, no key is carried over, and the VM shows the "re-enter your API key" notice.

### S-25 — `.bak` lifecycle
- Given a completed restore
- When the app starts and `ApplyMigrations` succeeds
- Then `CompletePendingRestore()` deletes the `.bak`; when migrations throw, it is not called and the `.bak` stays.

### S-26 — Confirmation panel
- Given the user picks a valid backup file
- When inspection succeeds
- Then the panel shows the backup date and the destructive warning, and no restore runs until Confirm; Cancel clears the panel and writes nothing.

### S-27 — Inspect failure shows localized message
- Given the user picks a garbage file
- When inspection fails
- Then no panel appears and the localized `NotABackup` message is shown; the same for each code in R-17 (theory over codes and both cultures).

### S-28 — Cancelled dialog is silent
- Given the open or save dialog returns `null`
- When the command runs
- Then no use case is invoked, no message is shown, `IsBusy` is false, and no file is created.

### S-29 — Double click runs once
- Given `IsBusy` is true
- When Export or Confirm is invoked again
- Then the second call returns immediately; the use case ran exactly once.

### S-30 — Restart flow
- Given a successful restore
- When "Restart now" is invoked and the restart service returns `false`
- Then the "close and reopen" fallback message is shown; when it returns `true`, no message is required.

### S-31 — Localized resources complete
- Given the new resx keys
- When resources are loaded in es-DO and en
- Then every key is present and non-empty in both.

### S-32 — Architecture guard
- Given the built assemblies
- When NetArchTest runs
- Then Application and UI reference neither `Microsoft.Data.Sqlite` nor EF Core (UI) nor Infrastructure, and R-1..R-5 pass.

### S-33 — No data loss end to end
- Given failure injection at each stage (validation, free space, safety backup, extraction, swap)
- When restore runs and fails
- Then in every case the live database SHA-256 equals its pre-call value.

## Out of Scope

See R-26. Additionally: percentage progress, a generic dialog service, and changes to Phase 18 report behavior (only the additive `IFileSaveDialog` overload).

## Acceptance Criteria

Evidence paths: test classes are under `tests/ControlDeFinanzasIA.Tests.Integration/Backup/` unless stated (`Tests.Unit/...` otherwise). Gates at close (2026-09-29, HEAD f87be48): build 0 warnings / 0 errors; 1725 unit + 564 integration = 2289 green; `dotnet format style --verify-no-changes` exit 0.

- [x] Backup ZIP contains exactly `manifest.json` + `data.db`; manifest fields per R-2; SHA-256 verified (S-3, S-12). Evidence: `SqliteBackupServiceCreateTests` (`Create_ProducesAZipWithExactlyManifestAndDataDb`, `Create_ManifestCarriesTheDocumentedFields`, `Create_DbSha256MatchesTheZippedDataDbBytes`); `SqliteBackupServiceInspectTests.Inspect_FlippedByteInDatabase_IsCorruptByHash`.
- [x] Copy taken via `BackupDatabase`, integrity-checked, consistent under a concurrent writer (S-6, S-7). Evidence: `Create_WhileAnotherConnectionIsWriting_YieldsAConsistentCopy`, `Create_WhenTheCopyFailsIntegrityCheck_ReturnsCorruptAndWritesZeroBytes`, `Create_LeavesNoTempFilesAfterSuccess`, `SqliteBehaviorAssumptionsTests`.
- [x] API key absent from exported bytes incl. free pages (`secure_delete` + `VACUUM`) (S-5). Evidence: `Create_KeyBytesNeverAppearInTheZipOrTheDatabase_EvenWhenAPreviousKeyLingersInFreeSpace`, `Create_LeavesTheLiveDatabaseUntouchedAndKeepsItsKey`, `BackupRestoreEndToEndTests.Roundtrip_ExportNeverLeaksTheApiKeyIntoTheFile`.
- [x] Restore pipeline runs in the R-8 order; every rejection leaves the live DB byte-identical (S-8..S-16, S-33). Evidence: `SqliteBackupServiceInspectTests` (garbage, empty, missing/extra entry, zip-slip, size caps, lying header, hash, damaged pages, FK violation, no migration history, `Inspect_OversizedAndWrongHash_ReportsTheSizeCapFirst`); `SqliteBackupServiceRestoreTests.Restore_RejectedBackups_ReturnTheSameCodeAndLeaveTheLiveDatabaseIdentical`, `Restore_ZipAlteredAfterInspect_IsRejectedBecauseItIsRevalidated`; failure-injection via `FaultyBackupFileSystem` and `RestoreHarness.AssertFailedClosed`.
- [x] Newer schema rejected showing both versions; older accepted (S-2, S-15). Evidence: `Inspect_UnknownMigrationId_IsNewerSchemaWithBothVersions`, `BackupRestoreEndToEndTests.FutureSchemaBackup_IsRejectedAsNewerSchemaWithBothVersions_AndTheLiveDatabaseIsByteIdentical`, `SqliteBackupServiceOlderSchemaTests.Restore_OfABackupWithAProperSubsetOfTheMigrations_ThenMigrate_SucceedsAndKeepsEveryRow`, `BackupSettingsViewModelTests.NewerSchemaMessage_ContainsAllFourValues`.
- [x] Safety backup precedes every swap; retention keeps 3 (S-17, S-18). Evidence: `Restore_WritesAValidSafetyBackupThatKeepsTheKeyAndEqualsThePreRestoreDatabase`, `Restore_Retention_KeepsTheThreeNewestSafetyZipsAndTouchesNothingElse`, `Restore_SafetyBackupCreateFails_IsIoErrorAndNeverSwaps`, `SafetyBackupRetentionOrderTests`.
- [x] Free-space pre-check (S-19). Evidence: `Restore_FreeSpaceBoundary_IsThreeTimesTheLargerOfLiveAndIncoming` (surfaced as `IoError` with `Details["reason"] = "insufficientSpace"`, Reconciliation #3).
- [x] Release/swap/retry/rollback, no swap over a journal (S-20..S-22). Evidence: `SqliteBackupServiceSwapFailureTests` (`Swap_TwoTransientLockFailures_ThenSucceeds_WithBackoffAndPoolsReleasedBeforeEveryAttempt`, `Swap_EveryAttemptFails_IsIoErrorAfterTheWholeScheduleWithTheLiveDatabaseByteIdentical`, `HotJournal_*`), `SqliteBackupServiceRealLockTests`, `Restore_InstalledFileDiffersFromTheValidatedOne_IsRolledBackToTheOriginal`, `Restore_LiveInWalModeWithPendingFramesHeldByAPooledConnection_RestoresExactlyTheBackupAndPreservesTheOldFramesInTheBak`.
- [x] `.bak` kept until the next successful start (S-25). Evidence: `CompletePendingRestoreTests` (`AfterASuccessfulRestore_DeletesTheBakAndSweepsTheWorkDirectory_ButNeverTheSafetyZipOrOtherFiles`, `WhenTheLiveDatabaseDoesNotCarryEveryMigration_KeepsTheBak`, `WhenARollbackFailedEarlier_KeepsTheBakEvenIfTheLiveDatabaseIsHealthy_AndWarnsOnEveryStart`).
- [x] Local API key kept; pre-Settings backup handled (S-23, S-24). Evidence: `Restore_BackupWithoutKey_CarriesTheLocalKeyIntoTheFileBeforeTheSwap_AndTheLiveFileChangesOnlyAtTheSwap`, `Restore_PreSettingsBackup_RestoresAndReportsTheKeyAsLost`, `BackupSettingsViewModelTests.ConfirmRestore_Success_ShowsSafetyFileName_AndKeyLostNotice`.
- [x] Confirmation panel with date and destructive warning; restart flow with fallback (S-26, S-30). Evidence at ViewModel level: `BackupSettingsViewModelTests.ChooseRestoreFile_InspectSuccess_ShowsConfirmation_WithoutRestoring`, `CancelRestore_DisposesSource_ClearsPending_WritesNothing_ReturnsToIdle`, `RestartNow_WhenRestartFails_ShowsFallback`, `RestartNow_WhenRestartSucceeds_ShowsNoFallback`; `AppRestartServiceTests`. The rendered panel and the real relaunch were not observed (see the last criterion).
- [x] Cancelled dialogs silent; busy guards (S-28, S-29). Evidence: `Export_Cancel_CallsNoUseCase_ShowsNoMessage_StaysIdle`, `ChooseRestoreFile_Cancel_CallsNoInspect_ShowsNoMessage_StaysIdle`, `Export_DoubleInvocation_OpensOneDialogAndRunsOneUseCase`, `ConfirmRestore_DoubleInvocation_SuspendsAndRestoresOnce`.
- [x] Codes mapped to localized messages; resx es-DO + neutral English + Designer complete (S-27, S-31). Evidence: `BackupSettingsViewModelTests.ErrorMessage_MapsEveryErrorCodeAndReason_ToANonBlankLocalizedMessage`, `BackupResourcesTests` (`BothResources_DefineTheSameBackupKeySet`, `Key_IsDefinedAndNonEmpty_InBothLocales_AndInTheDesigner`). Layout note in Post-apply reconciliation.
- [x] Roundtrip identity (S-1) and end-to-end test (slice S7). Evidence: `BackupRestoreEndToEndTests.Roundtrip_ExportFromA_RestoreIntoAFreshInstall_ReproducesEveryTableExceptTheApiKey`, `SettingsViewModel_ExportThenRestore_DrivesTheRealPipelineEndToEnd`.
- [x] No new package; architecture rules and D20 guard green (S-32). Evidence: the phase did not change `Directory.Packages.props` or any `.csproj`; `ArchitectureTests` forbid `Microsoft.Data.Sqlite` in Application and UI; suite green.
- [x] Docs reconciled (R-25); `AGENTS.md` updated. Evidence: `docs/architecture.md` §11f and §12, `docs/roadmap.md` §Fase 19, `docs/AI_HANDOFF.md`, `AGENTS.md` status line, `spec/phases/phase-19/features.md`.
- [x] Gates: `dotnet build` clean, `dotnet test` green, `dotnet format style --verify-no-changes` exit 0, NetArchTest R-1..R-5 green, spec renamed to `.done.md` after archive. Evidence: closing run above (0/0, 2289 green, format exit 0); this file was renamed with `git mv`.
- [ ] Manual check of the real open/save dialogs and the relaunch on Windows recorded (cannot be automated; no headless Avalonia harness). **NOT RUN BY HAND.** No session drove the native save/open dialogs, the real relaunch through `IAppRestartService`, or the Backup section visually in es-DO and en (tasks 6a.8, 6b.5, 8.2 to 8.5). Only automated coverage exists (ViewModel tests with fake dialogs and restart service, `AppRestartServiceTests`, `SettingsViewBackupBlockTests`).

## Reconciliation with design (2026-09-29, orchestrator)

All 12 points of `.atl/sdd/f-019p19-backup/design.md` "Spec Reconciliation" are accepted; where this spec's wording differs, the design governs:

1. `CreateAsync` takes `Func<CancellationToken, Task<Stream>> openDestination` so a failed backup never creates or truncates the user's file.
2. Both generic picker members take an optional `string? title`.
3. Exactly four error codes (`NotABackup`, `Corrupt`, `NewerSchema`, `IoError`). Insufficient space is `IoError` with `Details["reason"] = "insufficientSpace"` (this supersedes the spec's separate `InsufficientSpace` code); the UI shows a distinct message. A manifest `formatVersion > 1` maps to `NewerSchema`.
4. Safety backups KEEP the API key (`apiKeyIncluded: true`) and never leave `<DataDir>`; user exports still redact it (this supersedes any spec wording that safety backups redact).
5. The newer-schema message shows the backup's app version + latest `MigrationId` and this app's version + latest `MigrationId`.
6. The Backup block sits before "Acerca de" (About stays last).
7. Cancellation is honored until the safety backup completes; the swap/verify/rollback phase ignores it.
8. Restart-required is terminal for the Backup section in that session; other Settings sections stay enabled.
9. Key carry-over applies when the backup's key row is missing OR empty; a backup with a non-empty key keeps its own.
10. A hot journal is resolved by letting SQLite roll it back on the live file before swapping.
11. Safety file naming uses seconds: `pre-restore-yyyyMMdd-HHmmss.zip`.
12. The roundtrip criterion compares every table's rows; `Settings` excludes only `ai.apiKey`.

Carried into apply: S2 must confirm `BackupDatabase` and `Pooling=false` behavior on the pinned Microsoft.Data.Sqlite 8.0.11. A shell-wide "restart required" banner is a non-blocking follow-up (T14). S4 may need splitting (~660 lines estimated).

## Post-verify reconciliation (2026-09-29)

Appended after the Phase 19 verify (PASS WITH WARNINGS). Earlier sections are history and are not rewritten.

- Wording note for R-13 / S-21: "three attempts" is superseded by the shipped behavior. The swap runs the initial attempt plus 3 retries at 200 / 600 / 1800 ms, i.e. 4 attempts in total, and only then returns `IoError`.
- Fix 1 (failed rollback keeps the original): when a rollback cannot put the original back (threat T11), `RollBackAsync` writes a durable marker `<DataDir>\backups\.rollback-failed` naming the live path, the retained `.bak`, the rejected file and the safety ZIP. While the marker exists `CompletePendingRestore` never deletes `data.db.pre-restore.bak` and logs a Warning naming the `.bak` and the marker on every start. Code never deletes the marker: after recovering by hand (copy the `.bak`, or restore the safety ZIP, over the live database) the user deletes it. A failure to write the marker is logged Critical and never changes the restore result.
- Fix 2 (no hung notification calls): `ScopedNotificationCenter` callers queued on the gate when `SuspendAsync` raises the flag are released as no-ops, and the flag is re-checked right after the gate is acquired. An in-flight run is still awaited by `SuspendAsync`; new calls while suspended stay no-ops; a cancelled suspend leaves the center usable.
- Fix 3 (orphan temp sweep): `CompletePendingRestore` also deletes `backups\pre-restore-*.zip.tmp` (a crash mid safety-backup leaves one holding the API key in clear), only while the service gate is free. Finished `pre-restore-*.zip`, `.bak`, rollback files and any other file are never touched.
- Fix 4 (retention order): safety ZIP retention ranks by file system write time (`LastWriteTimeUtc`) first, with the name stamp and `-N` suffix as the deterministic tie-breaker, so a DST fall-back hour cannot prune a newer ZIP. Only `pre-restore-yyyyMMdd-HHmmss[-N].zip` names remain eligible.

## Post-apply reconciliation (2026-09-29)

Items not already recorded above (Reconciliation with design, Post-verify reconciliation):

- The grounding fact "Journal mode is DELETE (no WAL)" was wrong for the live database: it runs in WAL mode (`SqliteBackupService.Restore.cs` documents this). Verification caught that a restore over a WAL database with a pooled connection holding pending frames had to be handled. The shipped swap releases pooled connections, folds the WAL into the file, keeps old frames in the `.bak`, deletes stale sidecars after the swap or rolls back. Evidence: `Restore_LiveInWalModeWithPendingFramesHeldByAPooledConnection_*`, `Restore_LeftoverWalFromACrashedProcess_*`, `Restore_StaleSidecar*`, `Create_FromTheWalModeLiveDatabaseEfCreates_ProducesADeleteModeCopyAndLeavesTheLiveModeAlone`. The exported copy is always DELETE mode.
- Resource layout: R-22 names `Strings.resx` (es-DO default) plus `Strings.en.resx`. The repo convention is a neutral English `Strings.resx` plus `Strings.es-DO.resx`; the phase follows it. Completeness is asserted in both cultures by `BackupResourcesTests`.
- Accepted, not fixed: leftover `data.db.pre-restore-<utc>.bak` and `data.db.rollback-*` aside files are never swept (safe clutter, but each holds a full copy including the API key; documented); the `ClearAllPools` ordering guarantee is Windows-only (Unix file handle semantics differ; Windows-first target); `AppRestartService` could leave two instances if `Shutdown` fails after `Process.Start` succeeded; no `PRAGMA trusted_schema=OFF` defense against hand-crafted triggers or views in a restored file (suggestion, follow-up candidate); other screens accept writes between restore and restart (T14, shell-wide restart-required banner is a documented follow-up).
- Process note: strict TDD was not literal for the S6b ViewModel tests and some earlier slices (recorded in the tasks apply notes).
- Manual checks (tasks 6a.8, 6b.5, 8.2 to 8.5) were NOT run by hand and stay unchecked.
