# Accessibility statement (ControlDeFinanzasIA 1.0.0)

This is an honest statement of what the app does for assistive technology, what it does not do, and what was never
tested. It is not a certification and it is not a full WCAG audit.

## Platforms

- **Windows UI Automation (UIA)** is the only platform this statement makes claims about. The automated checks below run
  against the real Avalonia automation peers, which is what the Windows UIA provider reads.
- **VoiceOver (macOS)** and **Orca / AT-SPI (Linux)**: best effort only. The same automation properties are set, but
  nothing was run on those platforms and nothing is claimed.
- **Narrator, NVDA, JAWS and Windows High Contrast were never run by hand.** No human screen-reader pass was done for
  1.0.0.

## What works (and is covered by an automated test)

- **Every focusable control has an accessible name.** A runtime scan opens each of the ten sections in the real main
  window and asks the real automation peer of every focusable, enabled, visible control for its name; an empty name or a
  type-name fallback (for example `Avalonia.Controls.Grid`) fails the test (`AutomationNameScanTests`). A static scan of
  every `.axaml` file adds the rule that glyph-only or element-content buttons and unlabelled inputs must set
  `AutomationProperties.Name`, and that a placeholder is not accepted as a name (`AccessibilityXamlScanTests`).
- **Names come from the localized resources** (Spanish `es-DO` and English), with the `A11y*` keys for controls that have
  no visible label (date pickers, currency and frequency selectors, the dismiss button of the AI suggestion, the
  spin buttons of numeric inputs). Calendar day buttons are named with the full date and the day total.
- **The template parts of composite inputs are named** by a style (the date picker flyout button and the numeric input
  text box and spin buttons), because otherwise they are announced by their content type.
- **Keyboard**: every action of the main flow is reachable without a mouse; shortcuts are generated from one catalog and
  listed in Settings; Enter submits the form on screen; Tab follows the visual order on the four primary forms
  (`TabOrderTests`), and a shortcut navigation moves focus into the new screen.
- **Focus is visible**: a 2 px ring appears on keyboard focus (cobalt on neutral controls, near-ink on the cobalt primary button) (`:focus-visible`, not on mouse focus).
- **Focus is restored after the error overlay**: closing the overlay returns focus to the control that had it before the
  overlay opened (`ErrorOverlayHeadlessTests`), and Enter cannot reach a default button behind it.
- **Contrast** is measured on the real palette (`ContrastTests`, WCAG relative luminance). Text pairs must reach 4.5:1 and
  focus rings and graphical fills 3:1:

  | Pair ("Sand and Cobalt" palette) | On `Canvas` | On `Surface` | On `SurfaceMuted` |
  |---|---|---|---|
  | Text (body text) | 15.87 | 17.45 | 14.89 |
  | TextSecondary (secondary text) | 7.94 | 8.73 | 7.45 |
  | TextMuted (captions, sidebar digit hint) | 5.56 | 6.11 | 5.22 |
  | Accent (cobalt: links, selected item) | 5.14 | 5.65 | 4.82 |
  | Income (green) | 5.58 | 6.13 | 5.23 |
  | Expense (rose) | 5.53 | 6.08 | 5.19 |
  | Warning (amber text) | 5.56 | 6.11 | 5.22 |

  Soft washes carry their own text: Text on `AccentSoft` 14.92, Accent on `AccentSoft` 4.83, Income on `IncomeSoft`
  5.33, Expense on `ExpenseSoft` 5.14, Warning on `WarningSoft` 5.41, and the white label on the cobalt button 5.65.
  Graphics need 3:1: the input border on `Surface` 3.41 (on `Canvas` 3.11) and the focus ring on `Canvas` 15.87,
  `Surface` 17.45 and the cobalt fill 3.09. The `WarningFill` colour is a lighter amber used only for the budget
  progress fill, never for text; a test fails if a fill-only token is used as a text foreground. Calendar days of the
  adjacent month do not use reduced opacity (it measured 3.25:1).
- **Status and error text is marked as a polite live region**: `AutomationProperties.LiveSetting` is set to `Polite` on
  the status, error and AI-fallback text of the views, the import result and the error overlay (`LiveRegionSupportTests`).

## What does not work, or is not claimed

- **Live-region delivery is unverified.** The property is set, but whether the Avalonia Windows UIA peer raises the
  live-region event so that a screen reader announces a change is unverified until a manual Narrator run. Do not rely on
  it to announce results; the text is also on screen and focus is not moved to it.
- **Placeholder (watermark) colour** is the Fluent default and was not measured.
- **Reduced-opacity states** (a note in the chat transcript, the AI suggestion while loading, a stale import preview) dim
  text and were not measured.
- **Charts** (category breakdown, monthly evolution) are drawn graphics; they have no text alternative beyond their
  titles and the figures listed next to them.
- **No high-contrast theme.** The app ships one light look ("Sand and Cobalt"); the theme selector in Settings saves a
  choice for a future release and says so. It does not follow the operating system high-contrast mode.
- **Row buttons share one name.** "Edit" and "Delete" buttons in lists have the same name on every row; the row text is
  next to them, but the buttons do not repeat it.
- **No full WCAG audit.** Only the checks listed above exist.

## What was not tested

- Any screen reader (Narrator, NVDA, JAWS, VoiceOver, Orca) on a real machine.
- The real window system behaviour of arrow-key navigation in the sidebar and of the focus ring (tests run in the headless
  Avalonia platform, which draws nothing).
- The macOS symbol form of the shortcut text and the Meta key bindings.
- Text scaling, display scaling above 100 % and right-to-left languages.
