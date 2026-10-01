# ControlDeFinanzasIA

## What it is

ControlDeFinanzasIA is a cross-platform desktop application (Windows, macOS, Linux) for personal finance control,
assisted by AI. Your data lives in a single local SQLite file on your machine; nothing is uploaded and there is no
telemetry. The AI features are optional and use your own provider key, stored only in the local data file.

It covers expenses and income, categories, budgets, savings goals, debts with installment plans, recurring charges,
a calendar, a dashboard, notifications, PDF/Excel export, backup and restore, and CSV/Excel import. The interface is
available in Spanish (es-DO) and English.

> Project documentation: [docs/vision.md](docs/vision.md) - [docs/architecture.md](docs/architecture.md) -
> [docs/roadmap.md](docs/roadmap.md) - [CHANGELOG.md](CHANGELOG.md)

## Download and run

Release archives are named `ControlDeFinanzasIA-<version>-<platform>` and are listed with their SHA-256 checksums in
`SHA256SUMS.txt`. The packages are self-contained: no .NET installation is needed. The executable inside every
package is `ControlDeFinanzasIA.UI`.

macOS and Linux builds are **provided, untested**: they are built by `scripts/publish.ps1` and their file layout was
checked, but they have never been run on real hardware. Windows is the only platform that was run (the extracted zip
starts and creates its database). The GitHub release workflow (`.github/workflows/release.yml`, draft releases only,
triggered by a `v*` tag) has never been run: no release has been published yet. There is no installer (no MSIX, no DMG
or AppImage is distributed yet) and nothing is code-signed.

### Windows

1. Extract the `.zip` archive anywhere.
2. Run `ControlDeFinanzasIA.UI.exe`.

### macOS

1. Extract `ControlDeFinanzasIA-<version>-osx-<arch>.app.zip` and move `ControlDeFinanzasIA.app` to Applications.
2. Open it. See "macOS Gatekeeper" below the first time.

### Linux

1. Extract the `.tar.gz` archive (`tar -xzf ...`) and run `./ControlDeFinanzasIA.UI`.
2. The app needs the ICU globalization libraries for correct es-DO formatting. Install `libicu` (for example
   `sudo apt install libicu-dev`, or `sudo dnf install libicu`). Without ICU the application fails at startup.

### Windows SmartScreen

The binaries are not code-signed, so Windows SmartScreen may show "Windows protected your PC". Choose
**More info**, then **Run anyway**. Verify the download against `SHA256SUMS.txt` first if in doubt.

### macOS Gatekeeper

The app is not signed or notarized, so macOS may say it cannot be opened. Control-click (or right-click) the app,
choose **Open**, then confirm **Open**. Alternatively run `xattr -dr com.apple.quarantine ControlDeFinanzasIA.app`.

## Build from source

Requires the .NET 8 SDK (`winget install Microsoft.DotNet.SDK.8` on Windows).

```bash
dotnet build -c Release
dotnet run --project src/ControlDeFinanzasIA.UI
dotnet test -c Release --no-build
dotnet format style --verify-no-changes
```

## Data directory

The database, backups and logs live in the per-user data directory:

- Windows: `%APPDATA%\ControlDeFinanzasIA`
- macOS: `~/Library/Application Support/ControlDeFinanzasIA`
- Linux: `$XDG_CONFIG_HOME/ControlDeFinanzasIA` or `~/.config/ControlDeFinanzasIA`

Set the environment variable `CONTROLDEFINANZASIA_DATA_DIR` to an absolute path to run against a different (for
example throwaway) data directory.

## Keyboard shortcuts

On macOS use Cmd instead of Ctrl.

| Shortcut | Action |
|---|---|
| Ctrl+N | New expense |
| Ctrl+Shift+N | New income |
| Ctrl+1 ... Ctrl+9, Ctrl+0 | Go to the 1st ... 9th and 10th sidebar section |
| Ctrl+, | Open Settings |
| Ctrl+Shift+B | Open or close the notification bell |
| Esc | Close the topmost overlay |

Enter submits the focused form.

## Stack

C# on .NET 8 (LTS) - Avalonia UI (MVVM) - SQLite + Entity Framework Core - xUnit and NetArchTest. Conventions and
architecture rules: see [AGENTS.md](AGENTS.md).

## License

All rights reserved. See [LICENSE](LICENSE).
