# Packaging: what was verified and what was not

Scope: F-021 slice S9 (publish scripts, archives, checksums, macOS `.app`, Linux AppImage recipe, draft release
workflow). Written on Windows 11 with Windows PowerShell 5.1, .NET SDK 8.0.422 and no `pwsh`, no macOS and no Linux.

## How to build the release archives

```powershell
powershell -File scripts/publish.ps1 -Rid all      # or win-x64 | osx-arm64 | osx-x64 | linux-x64
powershell -File scripts/verify-layout.ps1 -Rid win-x64
powershell -File scripts/smoke-win.ps1             # win-x64 only, needs a Windows desktop session
```

Everything lands in the git-ignored `artifacts/` folder: `ControlDeFinanzasIA-<version>-<rid>.zip|tar.gz`,
`ControlDeFinanzasIA-<version>-osx-*.app.zip` and `SHA256SUMS.txt` (`sha256sum` format). The version is read from
`Directory.Build.props`. The scripts need only Windows PowerShell 5.1 or PowerShell 7: the archive writer is a small C#
class in `scripts/lib/Archive.psm1` (not `System.Formats.Tar`, which needs .NET 7 / PowerShell 7.3).

## Verified by running it on this machine

| Item | Evidence |
|---|---|
| `build/Publish.props` + UI import + `artifacts/` ignored | `PublishPropsTests` (6 tests) |
| Self-contained publish for win-x64, osx-arm64, osx-x64, linux-x64 | Real `dotnet publish -r <rid>` of all four (runtime packs downloaded) |
| Executable and native libraries per RID | `scripts/verify-layout.ps1` on all four real outputs, plus magic-byte check (PE / ELF / Mach-O), run by `PublishLayoutTests` |
| Archive names, version, `SHA256SUMS.txt` recomputed | `ChecksumVerificationTests` over the real files |
| Unix modes inside the archives (T13) | Test re-reads tar headers (0755 only on `ControlDeFinanzasIA.UI` and `createdump`, valid header checksums) and zip central directories (host byte 3, mode 0755 on the bundle executable); cross-checked with the Windows `tar.exe` (bsdtar) and GNU tar listing |
| win-x64 archive runs | `scripts/smoke-win.ps1`: extracted zip, `DOTNET_ROOT` removed and no dotnet folder on `PATH`, temp data dir, `data.db` created, `Migrations applied` logged, `coreclr.dll` loaded from the extracted folder, window closed, exit code 0, no `startup-error-*.log`. The real `%APPDATA%\ControlDeFinanzasIA` listing was identical before and after |
| `.app` folder structure and `Info.plist` | `AppBundleStructureTests` runs `scripts/package-macos-app.ps1` over a synthetic publish folder; the same script ran over the real osx outputs |
| AppImage recipe files and shell syntax | `AppImageRecipeTests`; `sh -n` on `AppRun` and `build-appimage.sh` |
| Workflow text rules | `ReleaseWorkflowTests` (tag trigger only, tag check job first, a `test` job running `dotnet build` and `dotnet test` in Release that every packaging job needs, appimagetool pinned by URL and SHA-256, draft via `gh`, only the last job has `contents: write`, only first-party `actions/*`, `build.yml` unchanged) |

## Authored but NOT verified

- Running the macOS or Linux binaries: no Mac or Linux machine was available. Layout and file formats were checked, not
  execution. The `.app` was never launched; Gatekeeper will block it (unsigned, not notarized).
- `LSMinimumSystemVersion` is `12.0`. Evidence: the `libe_sqlite3`, `libQuestPdfSkia` and `libqpdf` dylibs in both osx
  outputs declare a minimum OS of 12.0, the .NET runtime binaries declare 10.15 (x64) and 11.0 (arm64), and .NET 8
  documents macOS 12 as its supported minimum. Skia, HarfBuzz and AvaloniaNative dylibs were not parsed.
- The AppImage build (`appimagetool` is pinned to release 1.9.0 with a SHA-256 check, but that hash is UNVERIFIED against upstream: the release publishes no checksum, so the value in `release.yml` was computed by us from the downloaded asset, identical on two downloads, i.e. a trust-on-first-use pin that detects a later swap, not proof the first download was genuine; the AppImage itself was never built), the
  DMG creation (`hdiutil`), and the execution of `release.yml` on GitHub: none was run. The workflow was checked only with
  text rules: there is no YAML library in the test projects and no package may be added, so it was not parsed as YAML.
  The first real tag push is the first test of the workflow; it only creates a draft release.
- Unix modes were checked by reading the archives, not by extracting them on Linux or macOS.
- The `tar.gz` for macOS (R-22) and the `.app.zip` (design) both exist; the owner should decide at verify time whether to
  keep both.
- MSIX, signing and notarization: out of scope for 1.0.0.
- `PublishLayoutTests` and `ChecksumVerificationTests` need `artifacts/` (run `scripts/publish.ps1 -Rid all` first).
  When the `artifacts/` folder does not exist (a fresh clone, or the existing `build.yml`, which runs the plain test
  suite without publishing) those tests are SKIPPED with an explicit message that names the command to run. Set the
  environment variable `CONTROLDEFINANZASIA_REQUIRE_PACKAGING=1` to make a missing `artifacts/` a failure instead (use it
  before a release). When `artifacts/` exists the tests are real and a missing file fails them. The gate is covered by
  `PackagingGateTests`. The release workflow runs `verify-layout.ps1` and the smoke test itself.

## Observations

- Output sizes (after the verify W4 fix): win-x64 133 MB (was 148), osx-arm64 137 MB (was 152), osx-x64 131 MB (was 146), linux-x64 126 MB (was 141), before compression. `Avalonia.Headless` now lives only in the unit test project, and `Microsoft.EntityFrameworkCore.Design` (with Roslyn and T4) is excluded from every publish; `PublishPayloadTests` fails if any of them reappears in a publish output. The `release.yml` `test` job runs on `windows-latest` only, the one platform where the suite has been run; running it on macOS and Linux runners is left for the first real tag push to show.
- Follow-up (verify suggestion 2): log entries still write `exception.ToString()` (type, message and stack, DD-1). Exception messages can embed user data (format errors, paths). The log is local-only and never uploaded; scrubbing messages would remove the detail that makes the log useful, so it was left as a product decision rather than changed silently.
- `DebugType=embedded` applies to the UI project only (the props file is imported there), so the Domain, Application and
  Infrastructure assemblies still ship a loose `.pdb`.
