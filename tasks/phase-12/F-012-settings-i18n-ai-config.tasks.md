# Tasks — F-012 Settings: Theme, Language, AI Config (Unused)

## Review Workload Forecast

| Field | Value |
|---|---|
| Estimated changed lines | 1270–1350 total across all slices; slice 4 alone ≈550 (resx keys land ×3), which is why it is split into 4a/4b |
| 400-line budget risk | High overall; Medium–High per sub-slice once 4 is split |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 (Domain+App contracts+Infra+migration) → PR 2 (Application use cases) → PR 3 (`App.axaml.cs` startup culture read) → PR 4a (Settings screen: wiring + Apariencia + Idioma) → PR 4b (Settings screen: IA + Acerca de) |
| Delivery strategy | ask-on-risk |
| Chain strategy | stacked-to-main |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: High

Design's "Recommended Slicing" proposes 4 strictly-chained slices — one more than Phase 11 — because
`App.axaml.cs`'s startup-culture read (slice 3) is the one change in this phase that can break app
launch entirely; it depends only on slice 1, is independently revertable (design's own rollback plan
calls it "a one-line restoration"), and must not be buried at the bottom of the ~550-line Settings
screen diff. Slice 4 is estimated ≈550 lines on its own — the ~26 resx keys land three times each
(`.resx` + `.es-DO.resx` + hand-written `Designer.cs` accessor) before a line of XAML — so this plan
takes the design's own documented escape hatch (same discretion Phase 11's tasks used for its slice-1
split): **4a = wiring + Apariencia + Idioma**, **4b = IA + Acerca de**, isolating the highest-scrutiny
content (API key field, cleartext warning) in its own focused review.

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|---|---|---|---|
| 1 | Domain + Application contracts + Infrastructure + migration | PR 1 | Base = main. `Setting` guard, `AppSettings`/enums, `SettingKeys`, `SupportedLocales`, `ISettingsStore`, `SettingConfiguration`, `SettingRepository`, `SettingsStore`, `DbSet`, `AddSettings` migration. |
| 2 | Application use cases | PR 2 | Base = PR 1's merged main. Depends on `ISettingsStore`/`AppSettings`. |
| 3 | Startup culture read | PR 3 | Base = PR 2's merged main. Compiles against slice 1 alone; chained after 2 for review order. `App.axaml.cs` ONLY. |
| 4a | Settings screen: wiring + Apariencia + Idioma | PR 4a | Base = PR 3's merged main. Depends on the use cases (2) and the startup read (3) existing first — a picker that persists a value nothing reads yet is the dishonest-toggle failure mode Q1 forbids. |
| 4b | Settings screen: IA + Acerca de | PR 4b | Base = PR 4a's merged main. Depends on 4a's `SettingsView`/`SettingsViewModel` skeleton. |

## 1. Domain + Application contracts + Infrastructure + migration

No consumers exist yet for `Setting`; safe to land tightened validation now.

- [x] 1.1 Modify `Domain/Settings/Setting.cs`: guard the `Key` `init` accessor (throw `ArgumentException` on empty/whitespace, trim), add `MaxKeyLength = 128` / `MaxValueLength = 2048` consts. Stays a `sealed record` — no `Create` factory. (D-1)
- [x] 1.2 Create `Application/Settings/AppSettings.cs`: `AppTheme` enum (`Dark=0, Light=1`), `AiProvider` enum (`OpenAI=0, Anthropic=1, OpenRouter=2, Custom=3`), 8-field `AppSettings` record, `Defaults` static, `Min/MaxAiTemperature` (0.0–2.0) and `Min/MaxAiMaxTokensPerRequest` (1–128000) consts. (R-5, D-2)
- [x] 1.3 Create `Application/Settings/SettingKeys.cs`: 8 `const string` key literals (`theme`, `locale`, `ai.provider`, `ai.apiKey`, `ai.model`, `ai.baseUrl`, `ai.temperature`, `ai.maxTokensPerRequest`). (D-3)
- [x] 1.4 Create `Application/Settings/SupportedLocales.cs`: `es-DO`/`en` allowlist, `Default = es-DO`, `IsSupported(string?)`. (D-4)
- [x] 1.5 Create `Application/Abstractions/ISettingsStore.cs`: `GetStringAsync`, `GetEnumAsync<T>`, `SetAsync<T>` (returns `Result`), `GetAppSettingsAsync` (bare `AppSettings`, never null/throws). `ISettingRepository.cs` stays untouched (Q3). (D-3)
- [x] 1.6 Create `Infrastructure/Persistence/Configurations/SettingConfiguration.cs`: `ToTable("Settings")`, `HasMaxLength` on `Key`/`Value`, unique index on `Key`. (D-6)
- [x] 1.7 Create `Infrastructure/Persistence/Repositories/SettingRepository.cs`: `BudgetRepository` template minus the owned-`Money` copy in `UpdateAsync`. (D-6)
- [x] 1.8 Create `Infrastructure/Settings/SettingsStore.cs`: `GetAppSettingsAsync` (try/catch broad, degrades to `AppSettings.Defaults` + `LogWarning`), `SetAsync<T>` (upsert via `existing with { Value }`), serialize/parse with `CultureInfo.InvariantCulture` on every numeric path, enums serialized by NAME not ordinal. Ctor takes only `ISettingRepository` + `ILogger` — no `HttpClient`. (D-5)
- [x] 1.9 Modify `Infrastructure/Persistence/AppDbContext.cs`: add `DbSet<Setting> Settings`.
- [x] 1.10 Modify `Infrastructure/Persistence/ServiceCollectionExtensions.cs`: `AddScoped<ISettingRepository, SettingRepository>()` and `AddScoped<ISettingsStore, SettingsStore>()` in `AddPersistence`.
- [x] 1.11 Scaffold migration with `dotnet ef migrations add AddSettings` (do not hand-author). Confirm `Up` contains only `CreateTable("Settings")` + unique index on `Key` and touches no other table; confirm `Down` drops only `Settings`; confirm `AppDbContextModelSnapshot.cs` now carries `Setting` with the unique index and both `HasMaxLength` values. (D-6)
- [x] 1.12 Run `Bootstrap.ApplyMigrations` against an existing dev DB and a fresh DB — the fresh-DB run is not optional, since slice 3 makes first-launch read this table.
- [x] 1.13 Unit tests for `Setting`: `Key` empty/whitespace/null throws; `" theme "` trims to `"theme"`; `Value = ""` accepted; `Id` defaults `Guid.Empty` without throwing; `with { Value = x }` preserves `Id`/`Key`.
- [x] 1.14 Unit tests for `SupportedLocales`: `IsSupported` true for `es-DO`/`en`, false for `fr-FR`/empty/null; `Default == "es-DO"`.
- [x] 1.15 Unit tests for `AppSettings.Defaults`: matches the documented defaults exactly.
- [x] 1.16 Unit tests for `SettingsStore` with a fake `ISettingRepository`: empty table → `Defaults`, never null/throws; throwing repository → `Defaults` + warning logged; absent key `SetAsync` → `AddAsync` once, `UpdateAsync` never; present key → `UpdateAsync` once with the same `Id`/`Key`, `AddAsync` never; unsupported stored locale → `es-DO`; unparseable enum/double/int → its default. (S-10, S-11)
- [x] 1.17 Unit test: load-bearing InvariantCulture regression — write `AiTemperature = 0.7` under `es-DO`, read under `en-US` → `0.7`, not `7.0`; repeat mirrored (write `en-US`, read `es-DO`). (D-5)
- [x] 1.18 Unit test: reflection assertion — no ctor param/field type of `SettingsStore` lives in `System.Net.Http`, and no type named `*LLMClient*` exists in the solution. (R-5, S-6)
- [x] 1.19 Integration tests for `SettingRepository` + real SQLite (`DashboardAggregateQueryTests` pattern): round-trip `Key`/`Value`; `GetAllAsync` ordered by `Key`; a second `SetAsync` for the same key updates the row, table still has exactly 1 row; a direct duplicate-`Key` insert violates the unique index; survives-a-restart — write through one context/store, read through a freshly constructed one. (S-9, S-11)

## 2. Application use cases

Depends on 1 (`ISettingsStore`/`AppSettings`/`SettingKeys`).

- [x] 2.1 Create `Application/UseCases/Settings/GetAppSettings/GetAppSettingsQuery.cs`: ctor `ISettingsStore`; `ExecuteAsync` → `Result.Success(await _store.GetAppSettingsAsync(ct))`. (D-9)
- [x] 2.2 Create `Application/UseCases/Settings/UpdateTheme/UpdateThemeUseCase.cs`: `Enum.IsDefined` guard → `Result.Failure("Validation", ...)`; else `SetAsync(SettingKeys.Theme, theme)`. (D-9)
- [x] 2.3 Create `Application/UseCases/Settings/UpdateLocale/UpdateLocaleUseCase.cs`: `!SupportedLocales.IsSupported(locale)` → `Result.Failure("UnsupportedLocale", ...)`; else `SetAsync(SettingKeys.Locale, locale)`. (D-4, D-9)
- [x] 2.4 Create `Application/UseCases/Settings/UpdateAiSettings/UpdateAiSettingsCommand.cs`: record with `Provider`, `ApiKey`, `Model`, `BaseUrl`, `Temperature`, `MaxTokensPerRequest`. (D-9)
- [x] 2.5 Create `Application/UseCases/Settings/UpdateAiSettings/UpdateAiSettingsValidator.cs`: `Provider Must(Enum.IsDefined)`; `ApiKey`/`Model` `MaximumLength(Setting.MaxValueLength)` (no `NotEmpty` — clearing is legal); `BaseUrl` absolute http/https URI `When` non-empty; `Temperature InclusiveBetween(0.0, 2.0)`; `MaxTokensPerRequest InclusiveBetween(1, 128000)`. (R-5, D-2, D-9)
- [x] 2.6 Create `Application/UseCases/Settings/UpdateAiSettings/UpdateAiSettingsUseCase.cs`: ctor `ISettingsStore`, `IValidator<UpdateAiSettingsCommand>`, `ILogger` — NO `HttpClient`/`IHttpClientFactory`/`ILLMClient`. Validate-all-then-write-all: six sequential `SetAsync` calls, return first non-success immediately; `BaseUrl` normalized to `null` when null/whitespace before write. (R-5, D-9)
- [x] 2.7 Modify `UI/Composition/Bootstrap.cs`: `AddTransient<GetAppSettingsQuery>()`, `AddTransient<UpdateThemeUseCase>()`, `AddTransient<UpdateLocaleUseCase>()`, `AddTransient<UpdateAiSettingsUseCase>()`, `AddTransient<IValidator<UpdateAiSettingsCommand>, UpdateAiSettingsValidator>()`. (D-11)
- [x] 2.8 Unit tests for `GetAppSettingsQuery`: returns `Result.Success` carrying the store's `AppSettings`; delegates, never re-derives a default.
- [x] 2.9 Unit tests for `UpdateThemeUseCase`: `(AppTheme)99` → `Validation` failure; `Light` IS persisted (Q1: persisted even though not rendered); persisted value is the string `"Light"`, not `"1"`. (S-1)
- [x] 2.10 Unit tests for `UpdateLocaleUseCase`: `"fr-FR"` → `UnsupportedLocale`, no write; `"es-DO"`/`"en"` → written. (S-3)
- [x] 2.11 Unit tests for `UpdateAiSettingsUseCase`: `Temperature` -0.1/2.1 → `Validation`, `SetAsync` never called; 0.0/2.0 accepted (inclusive); `MaxTokens` 0/-1/128001 → failure, 1/128000 accepted; non-URI `BaseUrl` → failure; null/empty `BaseUrl` → accepted, normalized `null`; empty `ApiKey` accepted; valid command → exactly 6 `SetAsync` calls with the 6 expected keys. (R-5, S-6, S-7)
- [x] 2.12 Unit test: `UpdateAiSettingsUseCase` ctor has no `HttpClient`/`IHttpClientFactory`/`ILLMClient` dependency (reflection assertion). (R-5, S-6)

## 3. Startup culture read

Depends on 1 only (`AppSettings`/`SupportedLocales`). `App.axaml.cs` is the ONLY file this slice touches — isolated and independently revertable per D-7.

- [x] 3.1 Modify `UI/App.axaml.cs`: remove the hardcoded `new CultureInfo("es-DO")` literal; add `private static void ApplyPersistedUiCulture(IServiceProvider services, ILogger logger)` — `CreateScope()`, resolve `ISettingsStore`, `Task.Run(() => store.GetAppSettingsAsync(...)).GetAwaiter().GetResult()` (not a naked sync-over-async — dispatcher isn't pumping yet), set both `CultureInfo.DefaultThreadCurrentUICulture` and `CultureInfo.CurrentUICulture`, `try/catch (CultureNotFoundException)` falling back to `SupportedLocales.Default`. Call it immediately after `Bootstrap.ApplyMigrations(Services)` and before `ShellViewModel` is resolved / `MainWindow` is constructed. `CultureInfo.CurrentCulture` is deliberately left unchanged (out of scope, D-7). (R-4, S-4, S-5, D-7)
- [x] 3.2 Manual verification: fresh DB → app launches in `es-DO`. (S-5)
- [ ] 3.3 Manual verification: `Setting` row `Locale = "en"` from a prior session → app launches in English (sidebar + views). (S-4)
- [ ] 3.4 Manual verification: hand-corrupt the `locale` row to an unsupported value → app still launches in `es-DO` with a logged warning, no crash (D-4/D-7 three-layer guard).
- [x] 3.5 (Optional, per design's Open Question) If `sdd-verify` requires an automated gate here, extract `ApplyPersistedUiCulture`'s culture-resolution body into a testable static helper taking `AppSettings` and returning `CultureInfo`, and unit-test the `CultureNotFoundException` fallback path — it does not cover the ordering constraint, which is the part manual verification actually protects.

## 4a. Settings screen — wiring + Apariencia + Idioma

Depends on 2 (use cases) and 3 (startup read) — merging the locale picker before the startup read exists would ship a value nothing reads yet (Q1's dishonest-toggle failure mode).

- [x] 4a.1 Modify `UI/Navigation/RouteNames.cs`: add `public const string Settings = "Settings";` — literal value matters, `ShellViewModel`'s sidebar highlight strips `"ViewModel"` from the VM type name. (D-11)
- [x] 4a.2 Modify `UI/Navigation/NavigationService.cs`: add `{ RouteNames.Settings, typeof(SettingsViewModel) }` to the ctor `_routes` dictionary.
- [x] 4a.3 Modify `UI/ViewModels/ShellViewModel.cs`: add the Settings `RouteMenuItem` via `_localizer["SidebarSettings"]` — not a hardcoded string.
- [x] 4a.4 Modify `UI/Views/ViewLocator.cs`: add `SettingsViewModel` in BOTH `Build` and `Match` — the omission Phase 9 caught for `DebtsViewModel`.
- [x] 4a.5 Modify `UI/Composition/Bootstrap.cs`: `AddTransient<SettingsViewModel>()`.
- [x] 4a.6 Create `UI/ViewModels/SettingsViewModel.cs`: ctor `GetAppSettingsQuery`, `UpdateThemeUseCase`, `UpdateLocaleUseCase`, `UpdateAiSettingsUseCase`, `ILogger` — full ctor now (4b wires `SaveAiCommand` later, but signature lands once). `[ObservableProperty]` `Theme`, `Locale`, `IsRestartRequired`, plus all six AI fields + `AppVersion` + `FormError`/`StatusMessage`/`IsLoading` chrome (hydrated by `LoadAsync` even though 4a exposes only Theme/Locale save commands). `LoadAsync` (fire-and-forget from ctor) hydrates all 7 editable fields via `GetAppSettingsQuery`. `SaveThemeCommand` → `UpdateThemeUseCase`. `SaveLocaleCommand` → `UpdateLocaleUseCase`, sets `IsRestartRequired = true` ONLY on success. NO Avalonia types, NO `IClock`. (D-8, D-10)
- [x] 4a.7 Create `UI/Views/SettingsView.axaml` (+ `.axaml.cs`): `ScrollViewer` over `StackPanel Spacing="24" Margin="24" MaxWidth="720"`. **Apariencia**: `ComboBox` bound to `Themes`/`Theme`; always-visible `TextBlock` (`AmberBrush`) with `Strings.SettingsThemeLightNotice`; `SaveThemeCommand` button. **Idioma**: `ComboBox` bound to `Locales`/`Locale`; `SaveLocaleCommand` button; restart-notice `TextBlock` (`AmberBrush`, `Strings.SettingsRestartRequiredNotice`) `IsVisible="{Binding IsRestartRequired}"`. IA/Acerca de sections are NOT added here — 4b appends them to this same file. (R-2, R-3, R-4, D-10)
- [x] 4a.8 Modify `UI/Resources/Strings.resx`, `Strings.es-DO.resx`, `Strings.Designer.cs`: add `SidebarSettings`, `SettingsAppearanceTitle`, `SettingsThemeLabel`, `SettingsThemeDark`, `SettingsThemeLight`, `SettingsThemeLightNotice`, `SettingsLanguageTitle`, `SettingsLanguageLabel`, `SettingsLanguageSpanish`, `SettingsLanguageEnglish`, `SettingsRestartRequiredNotice`, `SettingsSaveButton`, `SettingsSavedMessage`, `SettingsErrorMessage`, `SettingsLoadErrorMessage`. `Designer.cs` accessors written by hand — build does not regenerate them. (R-9, D-12)
- [x] 4a.9 Unit tests for `SettingsViewModel` (Apariencia/Idioma slice): `LoadAsync` hydrates `Theme`/`Locale` from the query; `SaveLocaleAsync` sets `IsRestartRequired` only on success, `false` before any save and after a failed save; `SaveThemeAsync` failure lands in `FormError`, does not clear the picker; `IsRestartRequired` never set by `SaveThemeAsync`; ctor takes no Avalonia type, VM never touches `RequestedThemeVariant`. (S-1, S-2, S-3, D-8)

## 4b. Settings screen — IA + Acerca de

Depends on 4a's `SettingsViewModel`/`SettingsView.axaml` skeleton (extends the same files, not new ones).

- [ ] 4b.1 Modify `UI/ViewModels/SettingsViewModel.cs`: add `SaveAiCommand` → `UpdateAiSettingsUseCase`, builds `UpdateAiSettingsCommand` from the six AI form fields verbatim. `AppVersion` reads `AssemblyInformationalVersionAttribute` with `"0.0.0"` fallback, computed once in the ctor. `IsRestartRequired` is never touched by `SaveAiAsync`. (D-10)
- [ ] 4b.2 Modify `UI/Views/SettingsView.axaml`: append **IA** section — warning `TextBlock` (`TerracottaBrush`, `Strings.SettingsAiApiKeyWarning`) shown ABOVE the fields and always visible; `ComboBox` provider; `TextBox` model; `TextBox` API key with `PasswordChar="●"`; `TextBox` base URL; `NumericUpDown Minimum="0" Maximum="2" Increment="0.1"` temperature; `NumericUpDown Minimum="1" Maximum="128000"` max tokens; `SaveAiCommand` button. No "Probar conexión" button. Append **Acerca de** section — app name + `AppVersion` + `Strings.SettingsAboutDescription`, no controls, no save. (R-5, R-6, S-8, D-10)
- [ ] 4b.3 Modify `UI/Resources/Strings.resx`, `Strings.es-DO.resx`, `Strings.Designer.cs`: add `SettingsAiTitle`, `SettingsAiApiKeyWarning`, `SettingsAiProviderLabel`, `SettingsAiModelWatermark`, `SettingsAiApiKeyWatermark`, `SettingsAiBaseUrlWatermark`, `SettingsAiTemperatureLabel`, `SettingsAiMaxTokensLabel`, `SettingsAboutTitle`, `SettingsAboutVersionLabel`, `SettingsAboutDescription`. (R-9, D-12)
- [ ] 4b.4 Unit tests for `SettingsViewModel` (IA slice): `SaveAiAsync` builds the command from the form fields verbatim; a validation failure lands in `FormError`; `IsRestartRequired` never set by `SaveAiAsync`; `AppVersion` is non-empty. (S-6, S-7)
- [ ] 4b.5 Manual smoke test: all four `SettingsView` sections (Apariencia, Idioma, IA, Acerca de) render together end-to-end in one screen, reachable via the sidebar. (R-2)

## 5. Cierre

- [ ] 5.1 Run build, `dotnet test` (unit + integration), `dotnet format style --verify-no-changes`, and `ArchitectureTests`/NetArchTest. All green, no regressions.
- [ ] 5.2 Run the app (`dotnet run`) against an existing dev DB and a fresh DB; visually confirm `SettingsView`'s four sections, the theme Light "not yet functional" note, the locale restart-required notice, and the AI cleartext warning all render correctly.
- [ ] 5.3 Update `docs/architecture.md` with a Phase 12 entry, following the pattern used for Phases 9–11.
- [ ] 5.4 Create `spec/phases/phase-12/features.md` following the pattern of `spec/phases/phase-10/` and `phase-11/`.
- [ ] 5.5 Archive the spec (`F-012-settings-i18n-ai-config.spec.md` → `.done.md`) and write `verify-report.md`/`archive-report.md` in `.atl/sdd/f-012p12-settings-i18n/`, re-verifying gates independently before archiving.
- [ ] 5.6 Carry forward the design's three non-blocking Open Questions into the archive/PR notes as documented-not-fixed follow-ups: (a) whether the startup locale read should move to `Program.Main` alongside relocating `Bootstrap.ApplyMigrations`; (b) no headless Avalonia harness exists, so slice 3's launch scenarios stayed manual; (c) `UpdateAiSettingsUseCase`'s six non-transactional writes are accepted as-is.
- [ ] 5.7 Note the recurring `Strings.Designer.cs` hand-editing (4th phase running) as a candidate for a future standalone change (restore codegen or a generated wrapper), per design D-12's Open Question.
