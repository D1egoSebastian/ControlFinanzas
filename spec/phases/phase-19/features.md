# Phase 19 — Backup and Restore

> **Phase:** 19 — Backup and restore
> **Status:** Implemented, pending archive (slices S1-S7 applied 2026-09-29; verify and manual checks outstanding)
> **Spec ID:** F-019 — `spec/features/phase-19/F-019-backup-restore.spec.md`
> **Dependencies:** Phases 0-18 archived (Settings, migrations, `IFileSaveDialog` precedent).
> **Blocks:** Phase 20 (CSV import reuses `IFileOpenDialog`). Distinct from Phase 18 (report export).
> **Roadmap:** `docs/roadmap.md` §Fase 19
> **Stack:** .NET 8 · Avalonia 11.2.7 (MVVM strict) · `Microsoft.Data.Sqlite` (transitive, Infrastructure only) · `System.IO.Compression` (BCL) · no schema change, no migration, no new package.

## 1. Summary

Two manual Settings actions: export everything to a ZIP (`manifest.json` + `data.db`, SHA-256, API key redacted with `secure_delete` + `VACUUM`) and restore from such a file. Restore validates the whole backup before touching the live database (ZIP shape, zip-slip, size caps, hash, integrity and foreign-key checks, migration-history comparison: newer schema is rejected showing both versions, older is accepted), takes a safety backup (`backups\pre-restore-*.zip`, keep 3), releases connections, swaps atomically with retry/backoff, keeps a `.bak` until the next successful start, and requires a restart (no live reload). The local API key is preserved. No auto-backup (ADR-009).

Structure: ports and three use cases in `Application`; `SqliteBackupService`, `EfMigrationCatalog` and `AddBackup()` in `Infrastructure/Backup/`; `AvaloniaFileOpenDialog`, generic save overload, `IAppRestartService`, `BackupSettingsViewModel` with an inline confirmation panel in UI.

## 2. Slice plan

Nine chained slices, applied in order (the design's S4 became S4a/S4b, S6 became S6a/S6b, and a data-directory override was added as S5.1).

| ID | Slice | Status |
| --- | --- | --- |
| S1 | Contracts (`IBackupService`, `IFileOpenDialog`, `IMigrationCatalog`, `FileTypeFilter`, `OpenTarget`, records, `BackupErrorCodes`, `BackupFileName`), `IFileSaveDialog` generic overload, `CreateBackupUseCase`, fakes and unit tests | Done |
| S2 | `SqliteBackupService.CreateAsync` (BackupDatabase, key redaction, integrity check, manifest, SHA-256, ZIP), `AddBackup()`, real temp-dir SQLite tests incl. concurrent writer and key-byte scan | Done |
| S3 | Inspect pipeline, `EfMigrationCatalog`, newer-schema rejection, zip-slip and size caps, hash/corruption tests | Done |
| S4a | Restore up to and including a real single-attempt swap: validation reuse, free-space check, safety backup + retention, key carry-over | Done |
| S4b | Hardened swap: hot journal, retry/backoff/repair, post-swap verify + rollback, `CompletePendingRestore`, failure-injection and real file-lock tests | Done |
| S5 | `InspectBackupUseCase`, `RestoreBackupUseCase`, `AvaloniaFileOpenDialog`, `Bootstrap` DI, `App` startup hook, arch guard for `Microsoft.Data.Sqlite` | Done |
| S5.1 | `CONTROLDEFINANZASIA_DATA_DIR` override in `SystemPathResolver` so smoke launches never touch the real data directory | Done |
| S6a | Strings (es-DO + en), `INotificationCenter.SuspendAsync`, `IAppRestartService`/`AppRestartService` | Done |
| S6b | `BackupSettingsViewModel`, Backup block in Settings (before "Acerca de"), `SettingsViewModel` optional parameter | Done |
| S7 | `BackupRestoreEndToEndTests` (real use cases + service, roundtrip, future schema, corrupt file, older schema + `Migrate()`, `.bak` cleanup, VM flow) and docs reconciliation | Done |

## 3. Phase completion criteria

- **(roadmap)** Export -> fresh data directory -> restore shows identical data in every table (API key excepted).
- **(roadmap)** A backup with an unknown `MigrationId` is rejected with a clear message showing both versions.
- Every rejection and failure path leaves the live `data.db` byte-identical.
- Exported ZIP contains no API-key bytes; the local key survives restore.
- Safety backup exists before every swap; retention keeps 3.
- Cancelled dialogs write nothing and show no error; busy guards prevent double execution; UI works in es-DO and en.
- Build clean, full suite green, `dotnet format style --verify-no-changes` exit 0, NetArchTest R-1..R-5 green.
- Manual verification of the real dialogs and relaunch (not automatable) recorded.

## 4. Risks and notes

- **Data loss on restore** (critical): validation before touching the live file, safety ZIP, atomic `File.Replace`, byte-identity tests on every failure.
- Windows file locks and antivirus: `Pooling=False`, `ClearAllPools()`, 3 retries with backoff; failure yields `IoError` with the live file intact.
- Leftover `data.db-journal`: never swap over it.
- `App` runs `ApplyMigrations` without try/catch: a failing post-restore migration crashes startup; `.bak` and safety ZIP are kept and manual recovery is documented (automatic revert is a follow-up).
- Backups predating the Settings table lose the local API key (documented, UI notice).
- `IFileSaveDialog` overload breaks existing fakes (certain); updated in S1.

## 5. References

- `docs/roadmap.md` §Fase 19; `docs/architecture.md` §Backup; ADR-008, ADR-009
- `spec/features/phase-19/F-019-backup-restore.spec.md`
- `.atl/sdd/f-019p19-backup/{exploration,proposal}.md`
- `spec/phases/phase-18/features.md` — format precedent

## 6. Follow-ups (not in this phase)

- Shell-wide "restart required" banner (T14): after a restore, other screens can still write to the restored file until the user restarts.
- Manual checks with the real native dialogs and the real relaunch, in both locales (not automatable): recorded in the Cierre step, not run by hand in S7.
- Automatic revert after a failed post-restore migration and a migration-failure startup dialog.
- Auto/scheduled backup, encryption, cloud sync (out of scope per ADR-009).
