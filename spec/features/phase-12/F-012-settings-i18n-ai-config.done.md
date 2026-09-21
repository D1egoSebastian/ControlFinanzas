# F-012 — Settings: Theme, Language, AI Config (Unused)

> **Phase:** 12 — Configuración: tema, idioma, IA (sin uso)
> **Status:** ✅ Archived (2026-09-21) — all acceptance criteria met, see
> `.atl/sdd/f-012p12-settings-i18n/verify-report.md` and `archive-report.md`
> **SDD:** `.atl/sdd/f-012p12-settings-i18n/`
> **Blocks:** Phase 13 (`ILLMClientFactory` reads the settings keys this phase writes)

## Goal

Give the user a single Settings screen where theme, language, and AI provider
configuration persist to a real `Setting` table and survive an app restart —
without making the app render a functional light theme, without live
language switching, and without any network call to an AI provider.

### Confirmed product decisions

- **Theme: persist but don't render.** `Theme` is stored and shown in
  `SettingsView`, but only `Dark` actually applies to
  `Avalonia.Application.Current.RequestedThemeVariant` in v1. Selecting
  `Light` MUST show an explicit, localized "not yet functional" note — never
  a silent no-op.
- **Language: restart-to-apply.** Saving `Locale` takes effect on the next
  launch, not live. Every view binds copy via `{x:Static
  resources:Strings.X}`, which resolves once at XAML-load time and cannot
  live-rebind. The UI MUST show a restart-required notice after saving.
- **AI config: persistence only.** All six AI fields are captured and
  stored. No `ILLMClient`, no adapter, no "Probar conexión" button, and zero
  network calls of any kind exist in this phase — that is Phase 13.
- **Layering: `ISettingRepository` (raw CRUD) stays; a new `ISettingsStore`
  (typed façade) composes it.** Use cases depend only on `ISettingsStore`.

## Requirements

### R-1 — Settings persistence core

`Setting(Key, Value)` rows MUST persist via `ISettingRepository` (raw CRUD,
unchanged low-level Application port). A new `ISettingsStore` typed façade
MUST compose `ISettingRepository` and expose `GetStringAsync`,
`GetEnumAsync<T>`, `SetAsync<T>`, and `GetAppSettingsAsync()`, the last
returning an `AppSettings` DTO. `GetAppSettingsAsync()` MUST return
documented defaults (`Locale = "es-DO"`, `Theme = Dark`, `AiProvider =
OpenAI`, empty key/model, sane temperature/max-tokens) on an empty or
unreadable `Setting` table, and MUST NOT throw or return null. `SetAsync<T>`
MUST upsert by `Key` — saving the same key twice MUST update the existing
row, not create a duplicate, backed by a unique index on `Key`.

### R-2 — Settings management screen

The system MUST provide a dedicated screen (`SettingsView` /
`SettingsViewModel`, reachable via `RouteNames.Settings`) with four
sections: Apariencia, Idioma, IA, and Acerca de. `RouteNames.Settings` MUST
equal `"Settings"` so `ShellViewModel`'s sidebar-highlight convention
resolves correctly.

### R-3 — Theme preference (persist-but-don't-render)

The Apariencia section MUST let the user pick a theme preference and persist
it via `UpdateThemeUseCase`. Only `Dark` MUST actually apply to
`Avalonia.Application.Current.RequestedThemeVariant`. Selecting `Light` MUST
be possible and MUST persist, but MUST display a clear, localized note
stating it is not yet functional in v1. The system MUST NOT let `Light`
appear to apply with no explanatory note — a selectable option that
silently does nothing is a defect, not an acceptable v1 cut.

### R-4 — Language preference (restart-to-apply)

The Idioma section MUST let the user pick a language and persist it via
`UpdateLocaleUseCase`. The change MUST take effect on the next app launch,
not immediately. Saving MUST display a clear, localized restart-required
notice. `App.axaml.cs` MUST read the persisted `Locale` from
`ISettingsStore` at startup instead of the current hardcoded `new
CultureInfo("es-DO")` literal, and MUST fall back to `es-DO` when no
`Locale` setting exists yet.

### R-5 — AI configuration (persistence only, zero network calls)

The IA section MUST let the user enter and persist `AiProvider` (enum:
`OpenAI | Anthropic | OpenRouter | Custom`), `AiApiKey`, `AiModel`,
`AiBaseUrl` (optional), `AiTemperature`, and `AiMaxTokensPerRequest` via
`UpdateAiSettingsUseCase`. The use case MUST reject out-of-range
`AiTemperature` and `AiMaxTokensPerRequest` values with local validation
only.

**Reconciliation note (2026-09-20):** the valid ranges are `AiTemperature`
∈ `[0.0, 2.0]` and `AiMaxTokensPerRequest` ∈ `[1, 128000]`, both inclusive.
These specific bounds accommodate OpenAI's 0–2 temperature range (narrower
than Anthropic's 0–1 would incorrectly reject valid OpenAI configurations)
and a generous-but-bounded token ceiling. `AiBaseUrl` is NOT required to be
present even when `AiProvider == Custom` — that precondition belongs to
Phase 13's client factory, where it actually gates a real connection
attempt; this phase only stores whatever the user enters. The system MUST display a clear, localized, always-visible warning
that the API key is stored in cleartext (no encryption in v1). The system
MUST NOT make any network call from the settings path, and MUST NOT
reference `ILLMClient`, `ILLMClientFactory`, or any provider adapter type —
none exist in the codebase at this phase.

### R-6 — "Acerca de" section

The Acerca de section MUST display the app version and relevant links. It
requires no persistence and MUST NOT write to the `Setting` table.

### R-7 — Settings survive an app restart

Every persisted setting (theme, locale, all six AI fields) MUST be
retrievable, unchanged, after the process restarts — verified via a direct
`ISettingRepository`/`ISettingsStore` round-trip test using a fresh
`DbContext`/service-provider instance, not an in-memory cache, matching the
roadmap's own stated completion criterion.

### R-8 — Explicit scope exclusions

The following are explicit non-requirements for this phase, not omissions:

- **`ILLMClient`, `ILLMClientFactory`, any provider adapter, or a "Probar
  conexión" button.** Phase 13.
- **API key encryption at rest.** ADR-011, Pendiente v2; ADR-008 (cleartext
  in v1) is already Accepted and not reopened here.
- **A functional light theme**, any `ThemeDictionaries` rework of
  `Tokens.axaml`/`LedgerStyles.axaml`, or OS theme detection.
- **Live / no-restart language switching**, and therefore no rewrite of any
  existing view's `{x:Static resources:Strings.*}` bindings.

### R-9 — Localization

All static user-facing copy on `SettingsView` — including the Light-theme
"not yet functional" note, the language restart notice, and the API-key
cleartext warning — MUST resolve through `Strings.resx` /
`Strings.es-DO.resx` / `Strings.Designer.cs`. No hardcoded user-facing
string literals are introduced. This follows, and does not reopen, the
convention established in Phase 10/11.

## Scenarios

### S-1 — Setting theme to Light shows the "not yet functional" note

- GIVEN `SettingsView` open on the Apariencia section with `Theme = Dark`
  persisted
- WHEN the user selects `Light` and saves
- THEN `Theme = Light` persists, `RequestedThemeVariant` remains `Dark` (no
  visual change occurs), and a localized "Dark only in v1" note is visibly
  displayed next to the picker

### S-2 — Setting theme to Dark actually applies

- GIVEN `SettingsView` open with `Theme = Light` persisted (from S-1)
- WHEN the user selects `Dark` and saves
- THEN `Theme = Dark` persists and `Avalonia.Application.Current.RequestedThemeVariant`
  is set to `Dark`

**Reconciliation note (2026-09-20):** `App.axaml` already hardcodes
`RequestedThemeVariant="Dark"` and Phase 12 does not modify `App.axaml`
(R-8). Since Light never actually applies (S-1), `RequestedThemeVariant`
never leaves `Dark` at any point in this phase regardless of what the user
selects — S-1 and S-2 are both satisfied by this existing, unchanged value.
No runtime write to `RequestedThemeVariant` from `SettingsViewModel` or
anywhere else is required or expected; do not add one. This keeps
`SettingsViewModel` free of any Avalonia-specific type, per the design's
testability rationale.

### S-3 — Changing language shows the restart notice

- GIVEN `SettingsView` open on the Idioma section with `Locale = "es-DO"`
- WHEN the user selects English and saves
- THEN `Locale = "en"` persists, the UI does NOT re-render in English
  immediately, and a localized restart-required notice is displayed

### S-4 — `App.axaml.cs` reads persisted locale at startup

- GIVEN a `Setting` row `Locale = "en"` exists from a prior session
- WHEN the app launches
- THEN `CultureInfo.DefaultThreadCurrentUICulture` is set to `en`, not the
  hardcoded `es-DO` literal

### S-5 — `App.axaml.cs` falls back to `es-DO` with no persisted locale

- GIVEN an empty `Setting` table (first launch)
- WHEN the app launches
- THEN `CultureInfo.DefaultThreadCurrentUICulture` is set to `es-DO`,
  without throwing

### S-6 — Saving AI config persists with zero network activity

- GIVEN `SettingsView` open on the IA section
- WHEN the user enters `AiProvider = OpenAI`, an API key, a model, and valid
  temperature/max-tokens values, then saves
- THEN all six fields persist via `UpdateAiSettingsUseCase`, and no HTTP
  request or `ILLMClient`/adapter type is invoked or referenced anywhere in
  the call path

### S-7 — `UpdateAiSettingsUseCase` rejects out-of-range values

- GIVEN `SettingsView` open on the IA section
- WHEN the user enters an out-of-range `AiTemperature` or
  `AiMaxTokensPerRequest` value and saves
- THEN `UpdateAiSettingsUseCase` rejects the save with a validation error,
  and no `Setting` row is written for the invalid field

### S-8 — API-key cleartext warning is always visible

- GIVEN `SettingsView` open on the IA section, regardless of whether an API
  key has been entered yet
- WHEN the user views the section
- THEN a localized, clearly visible warning stating the API key is stored in
  cleartext (no encryption in v1) is displayed

### S-9 — Settings survive an app restart

- GIVEN `Theme = Dark`, `Locale = "en"`, and all six AI fields persisted in
  a prior session
- WHEN a fresh `ISettingsStore`/`DbContext` instance queries
  `GetAppSettingsAsync()` (simulating a restart)
- THEN every persisted value round-trips unchanged

### S-10 — `GetAppSettingsAsync()` returns defaults on an empty table

- GIVEN an empty `Setting` table
- WHEN `GetAppSettingsAsync()` is called
- THEN it returns `Locale = "es-DO"`, `Theme = Dark`, `AiProvider = OpenAI`,
  empty key/model, and sane default temperature/max-tokens — never null,
  never throwing

### S-11 — Saving the same setting key twice upserts, not duplicates

- GIVEN `Theme = Dark` already persisted
- WHEN the user saves `Theme = Light` and then `Theme = Dark` again
- THEN exactly one `Setting` row exists for the `Theme` key at all times,
  confirmed against the unique index on `Key`

## Out of Scope

`ILLMClient`, `ILLMClientFactory`, any provider adapter
(`OpenAICompatibleClient`, `AnthropicClient`), the "Probar conexión" /
`TestAiConnectionUseCase` button (Phase 13), any network call whatsoever
including API-key shape validation against a provider, API key encryption
at rest (ADR-011, v2), a functional light theme or `ThemeDictionaries`
rework of `Tokens.axaml`/`LedgerStyles.axaml`, OS theme detection, live /
no-restart language switching and any rewrite of existing
`{x:Static resources:Strings.*}` bindings, and user-editable
categories/payment methods/tags/saved views.

## Acceptance Criteria

- [x] `SettingsView` is reachable from the sidebar via `RouteNames.Settings`
  and renders four sections: Apariencia, Idioma, IA, Acerca de. **Evidence:**
  confirmed both by direct source read and by re-running the app and
  screenshotting all four sections during `sdd-verify` (see
  `verify-report.md`).
- [x] Changing the theme persists and survives an app restart. **Evidence:**
  `SettingRepository` + real SQLite integration test, fresh-`DbContext`
  round-trip (task 1.19).
- [x] Only Dark actually renders; Light carries an explicit, localized "not
  yet functional" note and is never a silent no-op. **Evidence:** the notice
  `TextBlock` in `SettingsView.axaml` has no `IsVisible` binding — always
  rendered, not conditional on the current selection.
- [x] Changing the language persists; the UI translates on the next launch,
  with a clear restart-required notice shown on save. **Evidence:**
  `SaveLocaleAsync` sets `IsRestartRequired = true` only in the success
  branch (unit test, task 4a.9).
- [x] `App.axaml.cs` no longer contains a hardcoded `new
  CultureInfo("es-DO")`; it reads the persisted locale and falls back to
  `es-DO` when absent. **Evidence:** `ApplyPersistedUiCulture`, commit
  `9ef0922`. Persisted-`en` and corrupt-locale fallback paths verified
  manually (tasks 3.3/3.4) — no headless Avalonia harness exists yet to
  automate them; see `verify-report.md` §5/Open Questions.
- [x] Saving the AI API key persists and survives an app restart. **Evidence:**
  same round-trip integration test as theme, covering all `Setting` keys.
- [x] All six AI fields (provider, key, model, base URL, temperature, max
  tokens) persist and round-trip. **Evidence:** `SaveAiAsync` unit tests
  (task 4b.4) + the integration round-trip test.
- [x] `UpdateAiSettingsUseCase` rejects out-of-range temperature and
  max-tokens values. **Evidence:** `UpdateAiSettingsValidator` boundary
  tests (task 2.11), validate-all-then-write-all confirmed by direct read.
- [x] Zero calls to any AI provider are made anywhere in the app after
  saving a key — asserted by test; no `ILLMClient`, adapter, or
  test-connection button exists in the codebase. **Evidence:**
  reflection-assertion tests (tasks 1.18, 2.12); `grep -rn "LLMClient" src/`
  returns no matches, re-confirmed during `sdd-verify`.
- [x] `GetAppSettingsAsync()` returns documented defaults on an empty
  `Setting` table and never throws or returns null. **Evidence:**
  `SettingsStoreTests` empty-table and throwing-repository cases (task 1.16).
- [x] Saving the same key twice updates the existing row; the unique index
  on `Key` holds. **Evidence:** integration test asserting exactly one row
  after a second `SetAsync` for the same key, plus a direct-duplicate-insert
  test confirming the unique index (task 1.19).
- [x] `Setting` rejects an empty `Key`. **Evidence:** `Setting` unit tests,
  empty/whitespace/null all throw (task 1.13).
- [x] `ISettingRepository` keeps its raw-CRUD shape; use cases depend only
  on `ISettingsStore`. **Evidence:** `ISettingRepository.cs` unchanged
  (confirmed via `git diff` across all 5 phase commits); every use case
  constructor takes `ISettingsStore`, never `ISettingRepository` directly.
- [x] No hardcoded user-facing strings; all new copy resolves through
  `Strings.resx` / `Strings.es-DO.resx`, with `Strings.Designer.cs`
  accessors added. **Evidence:** `SettingsView.axaml` read in full, zero
  hardcoded literals found; 26 new keys added across all three resource
  files.
- [x] `App.axaml`, `Tokens.axaml`, `LedgerStyles.axaml`, and all
  pre-existing views are unchanged. **Evidence:** `git diff` across all 5
  phase commits touches no file under `Styles/` and no pre-existing view.
- [x] `AppDbContextModelSnapshot.cs` matches the `AddSettings` migration.
  **Evidence:** confirmed by direct read during task 1.11 and re-confirmed
  by the clean `dotnet build`/integration-test run in `sdd-verify`.
- [x] Full test suite green, no regressions. **Evidence:** 1033/1033 (854
  unit + 179 integration), re-run independently in `sdd-verify` under both
  Debug and Release.

**Note (2026-09-21, added during `sdd-verify`):** a significant regression
unrelated to this spec's own scope was found and fixed while verifying the
first acceptance criterion above (actually clicking into the rendered
`SettingsView`) — see `verify-report.md` for the full writeup of the
`CurrentViewConverter` finding (fixed in commit `4f8e423`, landed separately
from this phase's own commits).
