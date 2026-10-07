# ControlDeFinanzasIA

## Que es?

ControlDeFinanzasIA es una aplicacion de escritorio hecho compatibles para los 3 ecosistemas (Windows, macOS, Linux) para el control de las finanzas del usuario, con la funcionalidad extra de que puede usarse con la ayuda de la inteligencia artificial mediante una api key, para pedir "asesoria financiera" con la ayuda de la IA, por lo que por supuesto, se puede usar sin ella.

Todos tus datos viven en un archivo local en tu maquina de SQLite, esta aplicacion no usa datos por cuenta propia, no cuenta con un sistema de autenticacion todo lo que se genere esta almacenado en su propia maquina.

Esta aplicacion cubre las diferentes categorias como de, gastos, gastos por categorias, ahorros, metas de ahorro, presupuestos, deudas, cobros de suscripciones, calendario de pagos, un dashboard intuitivo con la informacion de los gastos, notificaciones sobre tu estado financiero, cubre exportaciones e importaciones a pdf, excel, csv, archivos backup.

Cuenta con 2 idiomas español e ingles 
Cuenta con modo oscuro y modo claro

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



## Stack

C# on .NET 8 (LTS) - Avalonia UI (MVVM) - SQLite + Entity Framework Core - xUnit and NetArchTest. Conventions and
architecture rules: see [AGENTS.md](AGENTS.md).

## License

All rights reserved. See [LICENSE](LICENSE).
