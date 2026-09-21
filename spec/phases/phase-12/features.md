# Phase 12 — Settings: Theme, Language, AI Config (Unused)

> **Phase:** 12 — Configuración: tema, idioma, IA (sin uso)
> **Status:** ✅ Completed (2026-09-21) — see `spec/features/phase-12/F-012-settings-i18n-ai-config.done.md`
> **Spec ID:** F-012
> **Dependencies:** Phase 1 ✅ (`Setting` stub, `ISettingRepository`)
> **Blocks:** Phase 13 (`ILLMClientFactory` reads the settings keys this phase writes)
> **Roadmap:** `docs/roadmap.md` §Fase 12
> **Stack:** .NET 8 · Avalonia 11.2.7 (MVVM strict) · EF Core 8.0.11 + SQLite ·
> xUnit + FluentAssertions

## 1. Summary

Phase 12 gives the user a single Settings screen (Apariencia, Idioma, IA, Acerca de) where theme,
language, and AI provider configuration persist to a real `Setting` table and survive an app restart.
Confirmed product decisions: theme persists but only `Dark` actually renders in v1 (`Light` shows an
explicit "not yet functional" note, never a silent no-op); language changes require a restart, not
live rebinding; AI configuration is persistence-only — zero `ILLMClient`, zero network calls, zero
"Probar conexión" button (Phase 13). `ISettingRepository` (raw CRUD) stays untouched; a new
`ISettingsStore` typed façade composes it, and every use case depends only on the façade.

## 2. Model

```
Domain/Settings/
  Setting                       — tightened stub → guarded Key (non-empty, trimmed, MaxKeyLength=128,
                                   MaxValueLength=2048). Stays a sealed record (no factory) — zero
                                   consumers existed before this phase, unlike Goal/Budget's stubs.

Application/Settings/
  AppSettings                   — 8-field record + Defaults static (AppTheme, AiProvider enums)
  SettingKeys                   — 8 const string key literals
  SupportedLocales               — es-DO/en allowlist, load-bearing for startup safety (D-4)

Application/Abstractions/
  ISettingsStore                — GetStringAsync, GetEnumAsync<T>, SetAsync<T>, GetAppSettingsAsync

Application/UseCases/Settings/
  GetAppSettings/                — thin read, delegates to the store
  UpdateTheme/                   — Enum.IsDefined guard
  UpdateLocale/                  — SupportedLocales.IsSupported guard
  UpdateAiSettings/               — FluentValidation (temperature/max-tokens ranges, BaseUrl shape),
                                    6 sequential SetAsync calls, validate-all-then-write-all

Infrastructure/
  Persistence/Configurations/SettingConfiguration — unique index on Key
  Persistence/Repositories/SettingRepository      — BudgetRepository template (no owned-Money copy)
  Settings/SettingsStore         — try/catch broad → AppSettings.Defaults + LogWarning; upsert via
                                    `existing with { Value }`; InvariantCulture on every numeric path;
                                    enums serialized by NAME not ordinal

UI/
  App.axaml.cs                   — ApplyPersistedUiCulture: reads locale at startup, replaces the
                                    hardcoded new CultureInfo("es-DO") literal
  ViewModels/SettingsViewModel   — sectioned preferences form, per-section save commands, no Avalonia
                                    type in the ctor (testable outside a headless harness)
  Views/SettingsView.axaml       — Apariencia, Idioma, IA, Acerca de
```

## 3. Slice plan

| ID | Slice | Estimate | Status |
| --- | --- | --- | --- |
| 1 | Domain + Application contracts + Infrastructure + migration | L | ✅ Completed |
| 2 | Application use cases | M | ✅ Completed |
| 3 | Startup culture read (`App.axaml.cs` only) | S | ✅ Completed |
| 4a | Settings screen — wiring + Apariencia + Idioma | M | ✅ Completed |
| 4b | Settings screen — IA + Acerca de | M | ✅ Completed |

Applied in 6 chained commits: `3fc1a52` (domain+infra+migration), `34c890e` (use cases), `9ef0922`
(startup read), `8c86405` (Apariencia+Idioma), `f2e08a8` (IA+Acerca de). One unplanned commit,
`4f8e423`, landed between 4a and the close of 4b — see §5.

Slice 4 was split into 4a/4b per the design's own documented escape hatch (mirroring Phase 11's
slice-1 split): 4a shipped the wiring and the two lower-scrutiny sections; 4b isolated the IA
section's API-key field and cleartext warning in its own focused review.

## 4. Phase completion criteria

- `SettingsView` reachable from the sidebar via `RouteNames.Settings`, renders all four sections. ✅
- Theme persists and survives a restart; only `Dark` renders, `Light` carries an always-visible
  localized "not yet functional" note. ✅
- Language persists; UI translates on next launch; restart-required notice shown only after a
  successful save (`IsRestartRequired` never touched by the other two save commands). ✅
- `App.axaml.cs` no longer hardcodes `new CultureInfo("es-DO")`; reads the persisted locale, falls
  back to `es-DO` on an empty table or `CultureNotFoundException`. ✅ (manual verification for the
  persisted-`en` and corrupt-locale fallback paths — see §5, no headless harness exists yet)
- All six AI fields persist and round-trip; `UpdateAiSettingsUseCase` rejects out-of-range temperature
  (outside `[0.0, 2.0]`) and max-tokens (outside `[1, 128000]`) with no partial write. ✅
- Zero `ILLMClient`/adapter/network call anywhere in the call path — asserted by a reflection test on
  `SettingsStore`'s and `UpdateAiSettingsUseCase`'s constructors. ✅
- API-key cleartext warning always visible in the IA section, not conditional on a key being present. ✅
- `GetAppSettingsAsync()` returns documented defaults on an empty/unreadable table, never throws or
  returns null. ✅
- Saving the same key twice upserts (unique index on `Key` holds, confirmed via a direct-duplicate-insert
  integration test). ✅
- All new copy resolves through `Strings.resx`/`Strings.es-DO.resx`, `Strings.Designer.cs` hand-edited
  (4th consecutive phase — see §5). ✅
- Full test suite green, no regressions (1033/1033: 854 unit + 179 integration). ✅

## 5. Findings carried forward, not fixed in this phase

- **`CurrentViewConverter.cs` had never been updated past Phase 6, silently blank-rendering every
  route added since** (Debts, Calendar, Budgets, Goals, and this phase's Settings) — `ViewLocator.cs`
  was kept current every phase but turned out to be dead code for `MainWindow`'s navigation, since
  `CurrentViewConverter` (a separate, parallel view-model→view map) is what its `ContentControl`
  actually binds through. Found by manually clicking into `Configuración` and `Presupuestos` during
  this phase's smoke test (4b.5) — 1033 passing tests never exercised it, and no exception was ever
  thrown, so it went undetected across 5 phases. **Fixed in commit `4f8e423`**, landed separately from
  this phase's own slices since it is an unrelated cross-phase regression: `CurrentViewConverter` now
  delegates to `ViewLocator` instead of keeping its own parallel switch, collapsing two maps that had
  to be kept in sync into one.
- **No headless Avalonia test harness exists in this repo** (design's Open Question). Slice 3's
  persisted-`en`-locale and corrupt-locale-fallback launch scenarios (tasks 3.3/3.4) remain manual
  verification steps, not automated tests — every other assertion in the phase is automated.
- **`UpdateAiSettingsUseCase` performs six non-transactional writes.** All validation runs before the
  first write, so the only reachable partial-save is a disk failure mid-sequence. Accepted per design
  D-5's Open Question; a batched `ISettingsStore.SetManyAsync` becomes worth its weight only if a
  future phase adds more multi-key settings groups.
- **`Strings.Designer.cs` hand-edited for the fourth consecutive phase** (design D-12's Open
  Question). The recurrence is now a pattern — a future standalone change should either restore code
  generation or replace `Strings` with a generated wrapper before the key count grows further.
- **Whether the startup locale read should move to `Program.Main`** alongside relocating
  `Bootstrap.ApplyMigrations` (design D-7's Open Question) — the contained `Task.Run` inside
  `OnFrameworkInitializationCompleted` was picked deliberately for this phase; a cleaner startup would
  be a separate, out-of-scope change.

## 6. References

- `docs/roadmap.md` §Fase 12 — original plan, now marked complete
- `spec/features/phase-12/F-012-settings-i18n-ai-config.done.md` — archived formal spec, evidence per
  criterion
- `.atl/sdd/f-012p12-settings-i18n/{exploration,proposal,design,verify-report,archive-report}.md` —
  full traceability
- `spec/features/phase-11/F-011-savings-goals.done.md` — precedent for splitting a slice via the
  design's own documented escape hatch
