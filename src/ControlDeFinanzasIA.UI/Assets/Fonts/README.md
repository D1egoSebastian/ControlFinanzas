# Fonts — BOLD "Libro Mayor" design pass

This folder is the target location for the three OFL-licensed variable fonts
used by the F-008 Phase 4 design pass:

- `Fraunces-VariableFont.ttf` — display serif for titles
- `InstrumentSans-VariableFont.ttf` — body sans-serif for labels and text
- `SplineSansMono-VariableFont.ttf` — monospaced font for tabular amounts

The fonts are available from Google Fonts under the SIL Open Font License 1.1.
Because the build agent cannot download binary assets, the `.ttf` files are not
present in this changeset. To complete the font embedding:

1. Download the TTF variable font files from Google Fonts.
2. Place them in this directory.
3. Add the corresponding `OFL-*.txt` license files.
4. The project already includes `<AvaloniaResource Include="Assets\Fonts\**" />`
   in `ControlDeFinanzasIA.UI.csproj`, so the fonts will be embedded on the
   next build.

Until the font files are present, `Styles/Tokens.axaml` falls back to system
fonts (`Times New Roman`, `Segoe UI`, `Consolas`) so the UI remains functional.

## Avalonia font references

- `avares://ControlDeFinanzasIA.UI/Assets/Fonts#Fraunces`
- `avares://ControlDeFinanzasIA.UI/Assets/Fonts#Instrument Sans`
- `avares://ControlDeFinanzasIA.UI/Assets/Fonts#Spline Sans Mono`
