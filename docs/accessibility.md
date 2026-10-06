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
- **Contrast** is measured on the real palettes of BOTH theme variants (`ContrastTests`, WCAG relative luminance, the
  same pairs and the same thresholds for Light and Dark; `Tokens.Light.axaml` and `Tokens.Dark.axaml` are parsed). Text
  pairs must reach 4.5:1 and focus rings and graphical fills 3:1. Light variant:

  | Pair ("Sand and Cobalt", light) | On `Canvas` | On `Surface` | On `SurfaceMuted` |
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

  Dark variant (canvas `#14130F`, card `#1D1C17`, muted fill `#262420`; the label on an accent fill is dark ink
  `#0C1030` and the focus ring is white, because the dark accent is a light cobalt):

  | Pair ("Sand and Cobalt", dark) | On `Canvas` | On `Surface` | On `SurfaceMuted` |
  |---|---|---|---|
  | Text `#F3F0E8` | 16.32 | 14.98 | 13.60 |
  | TextSecondary `#CBC7BC` | 11.01 | 10.10 | 9.17 |
  | TextMuted `#A7A398` | 7.38 | 6.77 | 6.15 |
  | Accent `#7088F0` | 5.73 | 5.26 | 4.77 |
  | Income `#4CC38A` | 8.39 | 7.70 | 6.99 |
  | Expense `#F27B93` | 7.11 | 6.52 | 5.92 |
  | Warning `#E5A63A` | 8.72 | 8.00 | 7.26 |

  Soft washes carry their own text in dark too: Text on `AccentSoft` 13.83, Accent on `AccentSoft` 4.85, Income on
  `IncomeSoft` 6.98, Expense on `ExpenseSoft` 6.10, Warning on `WarningSoft` 6.91, and the dark label on the cobalt
  button 5.71 (hover 7.45, pressed 4.77, on the expense badge 7.09). Graphics: the input border on `Surface` 3.85 (on
  `Canvas` 4.20), and the focus ring on `Canvas` 18.59, `Surface` 17.06, `SurfaceMuted` 15.49, the cobalt fill 3.25 and
  `AccentSoft` 15.75. The chart slice colours of each variant are also measured at 3:1 on that variant's card.
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
- **No high-contrast theme and no "follow the system" option.** The app has two looks, light and dark ("Sand and Cobalt"),
  chosen in Settings and applied at once; it does not follow the operating system theme or its high-contrast mode.
- **Chart hover tooltips** are LiveCharts' own light tooltip in both variants (readable, but not themed); the chart
  labels, gridlines and legend follow the theme.
- **Row buttons share one name.** "Edit" and "Delete" buttons in lists have the same name on every row; the row text is
  next to them, but the buttons do not repeat it.
- **No full WCAG audit.** Only the checks listed above exist.

## What was not tested

- Any screen reader (Narrator, NVDA, JAWS, VoiceOver, Orca) on a real machine.
- The real window system behaviour of arrow-key navigation in the sidebar and of the focus ring (tests run in the headless
  Avalonia platform, which draws nothing).
- The macOS symbol form of the shortcut text and the Meta key bindings.
- Text scaling, display scaling above 100 % and right-to-left languages.
