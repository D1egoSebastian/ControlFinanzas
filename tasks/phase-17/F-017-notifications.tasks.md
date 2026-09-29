# Tasks — F-017 Notificaciones

> **Coexistence gate:** Phase 16 (`F-016-ai-savings-plan`) is being applied concurrently in this
> repo. Every Phase 17 touch on a shared file — `Bootstrap.cs`, `PromptNames.cs` (not touched by
> this phase, listed only for the guard), `Strings.resx`/`Strings.es-DO.resx`/`Strings.Designer.cs`,
> `AGENTS.md` — MUST be an appended block. Nothing existing in those files may be reordered,
> renamed, or restructured, so both phases merge cleanly regardless of commit order.

## Review Workload Forecast

| Field | Value |
|---|---|
| Estimated changed lines | ≈3,810 total across 9 strictly chained slices (design's own estimates: S1a ~380, S1b ~420, S2 ~450, S3 ~470, S4 ~480, S5a ~500, S5b ~250, S6 ~380, S7 ~480) |
| 400-line budget risk | High — S1b, S2, S3, S4, S5a all meet or exceed the 400-line budget |
| Chained PRs recommended | Yes |
| Suggested split | PR S1a → PR S1b → PR S2 → PR S3 → PR S4 → PR S5a → PR S5b → PR S6 → PR S7 |
| Delivery strategy | auto-chain |
| Chain strategy | stacked-to-main |

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: High

The design's `## Recommended Slicing` is authoritative and strictly chained. S1a (Domain) has no
in-phase dependency and is pure/DI-free — lowest risk. S1b is the first startup-risk slice (new
table, migration) — mandatory smoke-launch. S2–S4 are pure Application layers with no DI-graph
change. S5a changes the DI graph (`Bootstrap.cs`). S5b changes `ShellViewModel`'s constructor and
`MainWindow.axaml`'s root layout — the first-of-its-kind shell header row — mandatory smoke-launch.
S6 changes `SettingsViewModel`'s constructor. S7 is additive-only over S2–S4's reconciler and use
case.

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|---|---|---|---|
| S1a | Domain | PR S1a | Base = main. Entity, enums, `NotificationCurrencyTotal`, `NotificationKeys`, Domain tests. |
| S1b | Persistence | PR S1b | Base = main (PR S1a merged). Port, config, repository, DbSet, DI line, `AddNotifications` migration + snapshot, integration tests. **Smoke-launch required.** |
| S2 | Settings + reconciler | PR S2 | Base = main (PR S1b merged). `SettingKeys` +6, settings reader, get/update use cases, `NotificationReconciler` (no digest), `InMemorySettingsStore`. |
| S3 | Detection: due items | PR S3 | Base = main (PR S2 merged). `NotificationRules` (debt, recurring), `DetectNotificationsUseCase` core, `FakeNotificationRepository`. |
| S4 | Detection: budget + anomaly; read/dismiss | PR S4 | Base = main (PR S3 merged). Budget + anomaly rules, `GetNotificationsQuery`, mark-read, dismiss. |
| S5a | UI core | PR S5a | Base = main (PR S4 merged). Center, formatter, list/item VMs, shell+kind resx, Fase 17 DI block, `FakeNotificationCenter`. |
| S5b | Shell surface | PR S5b | Base = main (PR S5a merged). `ShellViewModel` param, `MainWindow` header + popup, `App.Opened` trigger, `ShellViewModelTests` fixture. **Smoke-launch required.** |
| S6 | Settings UI | PR S6 | Base = main (PR S5b merged). `SettingsViewModel` section + save-triggers-run, view section, resx, 2 fixtures. |
| S7 | Digest | PR S7 | Base = main (PR S6 merged). `DigestPeriod`, cursor, digest composition + reconciler branch, digest formatting + resx, `AGENTS.md`. |

## 1. Domain (S1a)

Depends on nothing (Phase 7/10/14 dependencies already archived). Goal: the `Notification`
aggregate and its enums exist as pure, EF-free building blocks with per-kind fact validation.

- [x] 1.1 Write failing `tests/…Unit/Domain/Notifications/NotificationTests.cs`: each
  `For*` factory rejects a fact belonging to another kind and rejects a missing required fact;
  `SanitizeLabel` strips control chars, collapses whitespace, and cuts at 120 chars; `MarkRead`/
  `Dismiss` are idempotent (first timestamp wins); `RefreshFacts` throws
  `InvalidOperationException` when `IsDismissed`; `HasSameFacts` compares only fact columns, not
  `Id`/`CreatedAt`/`ReadAt`/`DismissedAt`. (R-1, R-11, D-1)
- [x] 1.2 Write failing `tests/…Unit/Domain/Notifications/NotificationKeysTests.cs`: exact
  `DedupKey` shape per kind (`debt:{id:N}`, `recurring:{id:N}:{yyyy-MM-dd}`,
  `budget:{id:N}:{yyyy-MM}:{Near|Exceeded}`, `anomaly:{id:N}`, `digest:{Daily|Weekly}:{yyyy-MM-dd}`),
  all dates formatted with `CultureInfo.InvariantCulture`. (R-1, D-2)
- [x] 1.3 Create `src/…Domain/Notifications/{NotificationKind,NotificationTier,DigestFrequency}.cs`
  and `NotificationCurrencyTotal.cs` (`record NotificationCurrencyTotal(Currency Currency, decimal
  Amount)`). (R-2, D-1)
- [x] 1.4 Create `src/…Domain/Notifications/Notification.cs`: sealed class, private setters,
  private parameterless ctor, the fact columns from D-1's table, `SanitizeLabel`, per-kind static
  factories (`ForDebtInstallment`, `ForRecurringCharge`, `ForBudget`, `ForAnomaly`, `ForDigest`),
  `MarkRead`, `Dismiss`, `HasSameFacts`, `RefreshFacts`, `IsRead`, `IsDismissed`. Run 1.1 green.
  (R-1, R-11, D-1)
- [x] 1.5 Create `src/…Domain/Notifications/NotificationKeys.cs`: pure static class building the
  `DedupKey` string per kind. Run 1.2 green. (R-1, D-2)
- [x] 1.6 Run all four gates: `dotnet build -c Release`, `dotnet test -c Release --no-build`,
  `dotnet format style --verify-no-changes`, NetArchTest filter. All green before opening PR S1a.

## 2. Persistence (S1b)

Depends on 1. Goal: `Notification` rows persist through a reconcile-shaped repository with the
`AddNotifications` migration, mirroring the `AnomalyAlert` detach-not-clear rule, with zero diff to
any other table.

- [x] 2.1 Write failing `tests/…Integration/Persistence/NotificationRepositoryTests.cs`: a
  `DedupKey` unique-index violation is caught and the shared `AppDbContext` stays usable for a
  later save (only the call's own attached entries are detached, no `ChangeTracker.Clear()`);
  `ApplyReconciliationAsync` performs exactly one `SaveChangesAsync`; `DigestTotals` round-trips
  through JSON; `Kind`/`Tier`/`DigestFrequency` round-trip as strings; `Currency` round-trips as
  int. (R-1, D-8)
- [x] 2.2 Create `src/…Application/Abstractions/INotificationRepository.cs`: `GetAllAsync`,
  `GetUndismissedAsync`, `GetByIdAsync`, `GetByIdsAsync`, `UpdateRangeAsync`,
  `ApplyReconciliationAsync(plan)`, plus the `NotificationReconciliationPlan` record (`ToAdd`,
  `ToUpdate`, `ToRemove`, `IsEmpty`) living next to it. (D-4, D-8)
- [x] 2.3 Create `src/…Infrastructure/Persistence/Configurations/NotificationConfiguration.cs`:
  table `Notifications`, `HasConversion<string>()` on `Kind`/`Tier`/`DigestFrequency`,
  `HasConversion<int>()` on `Currency`, `HasPrecision(18,4)` on `Amount`/`Percent`, a JSON converter
  + `ValueComparer` on `DigestTotals`, `Ignore(IsRead, IsDismissed)`, unique index on `DedupKey`,
  plus indexes on `ReferenceDate` and `DismissedAt`, no FK to any source table. (D-8)
- [x] 2.4 Create `src/…Infrastructure/Persistence/Repositories/NotificationRepository.cs`: copies
  `AnomalyAlertRepository.ApplyReconciliationAsync` line for line — one `SaveChangesAsync`, and on
  `DbUpdateException` detach only this call's newly-tracked entries, never
  `ChangeTracker.Clear()`. `UpdateRangeAsync` applies the same detach-on-failure rule. Reads use
  `AsNoTracking`. Run 2.1 green. (R-1, D-8)
- [x] 2.5 Modify `src/…Infrastructure/Persistence/AppDbContext.cs`: append
  `public DbSet<Notification> Notifications { get; set; }`. (D-8)
- [x] 2.6 Modify `src/…Infrastructure/ServiceCollectionExtensions.cs` (`AddPersistence`): append
  `services.AddScoped<INotificationRepository, NotificationRepository>();` after
  `IAnomalyAlertRepository`. (D-8)
- [x] 2.7 Generate the migration with `dotnet ef migrations add AddNotifications --project
  src/ControlDeFinanzasIA.Infrastructure --startup-project src/ControlDeFinanzasIA.UI`; regenerate
  `AppDbContextModelSnapshot.cs`. `Down()` drops the table. (D-8)
- [x] 2.8 Modify `tests/…Integration/.../ModelSnapshotConsistencyTests.cs`: add one round-trip
  fact covering the new table. (D-8)
- [x] 2.9 Smoke-launch the app (`dotnet run --project src/ControlDeFinanzasIA.UI`) against an
  existing dev DB: confirm the `AddNotifications` migration applies with no startup failure.
  Screenshot only, no simulated clicks.
- [x] 2.10 Run all four gates. All green before opening PR S1b.

## 3. Settings + reconciler (S2)

Depends on 2. Goal: per-kind toggles and digest frequency read through `SettingKeys`/
`ISettingsStore` only (never `AppSettings`), and the pure reconciler produces add/update/remove
plans for the four detectable kinds (digest branch deferred to S7).

- [x] 3.1 Write failing `tests/…Unit/Application/Notifications/NotificationReconcilerTests.cs`:
  two passes over the same candidate → one row with `CreatedAt` unchanged (S-4); a dismissed key is
  never recreated (S-5); a dismissed Near plus a later Exceeded candidate → Exceeded added,
  unaffected by the dismissal (S-6); an Exceeded candidate removes an undismissed Near for the same
  budget/month (R-5); a disabled kind (evaluated with zero candidates) removes both read and unread
  undismissed rows, and re-enabling it re-adds for sources that still qualify (S-7); a
  `protectedKeys` entry is kept even with no candidate; purge removes rows with `ReferenceDate <
  today − 60` including dismissed ones, and keeps a row at exactly 60 days (S-10); an undismissed
  row with changed facts goes to `ToUpdate` with `Id`/`CreatedAt`/`ReadAt` preserved. (R-5, R-7, R-9,
  D-4)
- [x] 3.2 Write failing `tests/…Unit/Application/Notifications/NotificationSettingsTests.cs`:
  `Defaults` is all-true toggles + `Weekly`; a missing or invalid stored toggle value reads as
  `true`; a missing or invalid frequency reads as `Weekly`; an invalid cursor string reads as
  `null`. (D-7)
- [x] 3.3 Create `tests/…Unit/TestDoubles/InMemorySettingsStore.cs`: a real key→value map
  implementing `ISettingsStore`, distinct from the shared `FakeSettingsStore` (D-15). (D-7)
- [x] 3.4 Modify `src/…Application/Settings/SettingKeys.cs`: append the 6 constants
  (`NotificationsDebtDueEnabled`, `NotificationsRecurringDueEnabled`, `NotificationsBudgetEnabled`,
  `NotificationsAnomalyEnabled`, `NotificationsDigestFrequency`,
  `NotificationsDigestLastPeriodStart`). (R-12, R-13, D-7)
- [x] 3.5 Create `src/…Application/Notifications/NotificationSettings.cs`: record
  `(bool DebtDue, bool RecurringDue, bool Budget, bool Anomaly, DigestFrequency Digest)` with a
  static `Defaults`. (R-12, R-13, D-7)
- [x] 3.6 Create `src/…Application/Notifications/NotificationSettingsReader.cs`:
  `ReadAsync(ISettingsStore, ct)` per D-7's parse rules. Run 3.2 green. (R-12, R-13, D-7)
- [x] 3.7 Create `src/…Application/Notifications/NotificationReconciler.cs`: `Plan(existing,
  candidates, evaluatedKinds, protectedKeys, today, digestDue)` implementing purge → candidates
  (skip dismissed, refresh-or-noop, add) → removals (evaluated kind, not candidate, not
  protected) per D-4, with the digest branch left as a no-op stub for S7. Run 3.1 green. (R-1, R-5,
  R-7, R-9, D-4)
- [x] 3.8 Create `src/…Application/UseCases/Notifications/GetNotificationSettings/
  GetNotificationSettingsQuery.cs`: `Success(NotificationSettings)`. (R-12, R-13)
- [x] 3.9 Create `src/…Application/UseCases/Notifications/UpdateNotificationSettings/
  UpdateNotificationSettingsUseCase.cs`: 5 sequential `SetAsync` calls, returns the first failure
  (the `UpdateAiSettings` precedent). (R-12, R-13)
- [x] 3.10 Run all four gates. All green before opening PR S2.

## 4. Detection: due items (S3)

Depends on 3. Goal: `DetectNotificationsUseCase` exists end to end for the two due-item kinds,
with the purge, toggle-gated reads, and sequential-await rule (R-15) in place.

- [x] 4.1 Write failing `tests/…Unit/Application/Notifications/NotificationRulesTests.cs` (debt +
  recurring): debt installment due date +3 days in, +4 days out, −7 days in, −8 days out (S-8);
  recurring rule `NextGenerationDate` today in, −1 day out, +3 days in, +4 days out; an inactive
  rule is excluded; a rule with `EndDate < NextGenerationDate` is excluded (Reconciliation #5). (R-4,
  D-3)
- [x] 4.2 Write failing `tests/…Unit/Application/UseCases/Notifications/
  DetectNotificationsUseCaseTests.cs` (due-item scope): sequential reads only, asserted via a fake
  that fails on overlapping calls; a disabled source is not read; a reconcile save failure returns
  `Failure`; an end-to-end pass over fakes covering debt + recurring candidates reconciling into
  `ToAdd`/`ToUpdate`/`ToRemove`. (R-1, R-7, R-15, D-5)
- [x] 4.3 Create `tests/…Unit/TestDoubles/FakeNotificationRepository.cs`: a Phase-17-owned double
  recording every call, distinct from any shared fake. (D-15)
- [x] 4.4 Create `src/…Application/Notifications/NotificationRules.cs`: static class with
  `DueHorizonDays = 3`, `OverdueLookbackDays = 7`, `RetentionDays = 60`, `FlyoutCap = 20`, and
  `DebtInstallments(...)` + `RecurringCharges(...)` returning `IReadOnlyList<Notification>`
  candidates. Run 4.1 green. (R-4, D-3)
- [x] 4.5 Create `src/…Application/UseCases/Notifications/DetectNotifications/
  DetectNotificationsUseCase.cs`: ctor per D-5 (no `DetectAnomaliesUseCase`, no LLM); reads
  settings, existing rows, and the two due-item sources sequentially when enabled; builds
  `evaluatedKinds`; calls `NotificationReconciler.Plan` and, when non-empty,
  `ApplyReconciliationAsync`; returns `Success(DetectNotificationsSummary)` or
  `Failure("NotificationReconcileConflict", …)` without catching internally. Run 4.2 green. (R-1,
  R-6, R-7, R-15, D-5)
- [x] 4.6 Run all four gates. All green before opening PR S3.

## 5. Detection: budget + anomaly; read/dismiss (S4)

Depends on 4. Goal: all four detectable kinds are wired into `DetectNotificationsUseCase`, and the
list/mark-read/dismiss use cases exist for the UI to consume.

- [x] 5.1 Extend `NotificationRulesTests.cs` (budget + anomaly): 79.99% → no candidate, 80% →
  Near, 99.99% → Near, 100%+ → Exceeded only (never both); an indeterminate line (missing FX rate)
  produces no candidate and its budget id is reported for `protectedKeys`; an acknowledged
  `AnomalyAlert` is excluded; `Date == today − 60` in, `Date == today − 61` out (Reconciliation #4).
  (R-5, R-6, D-3)
- [x] 5.2 Extend `DetectNotificationsUseCaseTests.cs`: full S-1 end-to-end over fakes across all
  four kinds (installment due in 2 days, subscription due in 1 day, budget at Exceeded, an
  unacknowledged anomaly) yields the combined candidate set and reconcile plan. (R-1..R-7)
- [x] 5.3 Write failing tests for `GetNotificationsQuery`, `MarkNotificationsReadUseCase`, and
  `DismissNotificationUseCase`: list ordering is unread-first, then `ReferenceDate` ascending, then
  `Kind`, then `Id`, capped at 20 (R-10); `UnreadCount` counts all undismissed-and-unread rows
  regardless of the cap (R-10); dismissing a missing/already-purged id returns `Success` (idempotent,
  D-6); marking already-purged ids is a silent no-op. (R-8, R-10, D-6)
- [x] 5.4 Modify `NotificationRules.cs`: append `Budgets(lines, monthStart, now)` (highest-tier-only,
  `unknownBudgetIds` for indeterminate lines) and `Anomalies(alerts, categoryNames, today, now)`
  (`!IsAcknowledged && Date >= today − 60`). Run 5.1 green. (R-5, R-6, D-3)
- [x] 5.5 Modify `DetectNotificationsUseCase.cs`: wire budget and anomaly reads into the sequential
  source loop, add the budget `protectedKeys` set for indeterminate lines, extend
  `evaluatedKinds`. Run 5.2 green. (R-1, R-5, R-6, R-7, D-5)
- [x] 5.6 Create `src/…Application/UseCases/Notifications/GetNotifications/
  GetNotificationsQuery.cs`, `MarkNotificationsRead/MarkNotificationsReadUseCase.cs`, and
  `DismissNotification/DismissNotificationUseCase.cs` per D-6. Run 5.3 green. (R-8, R-10, D-6)
- [x] 5.7 Run all four gates. All green before opening PR S4.

## 6. UI core (S5a)

Depends on 5. Goal: a per-run-scoped notification center, a text formatter, and route-agnostic
list/item ViewModels exist and are wired into DI, without touching the shell yet.

- [ ] 6.1 Write failing `tests/…Unit/UI/NotificationTextFormatterTests.cs`: each kind's title/body
  in both `es-DO` and `en`; overdue vs. due body is derived from `ReferenceDate < today`, never
  stored (R-11); an empty subject label falls back to `NotificationUnnamedSubject`; money uses
  `{amount:N2} {currency}` in `CurrentCulture`; relative-date keys for today/tomorrow/N-days. (R-11,
  D-10)
- [ ] 6.2 Write failing `tests/…Unit/UI/NotificationListViewModelTests.cs`: `BadgeText` is `""` at
  0, `"1"` at 1, `"9"` at 9, `"9+"` at 10 and 12 (S-11); setting `IsOpen = true` calls
  `MarkReadAsync` with only the currently-listed unread ids and does not trigger a refresh (R-8,
  R-14); `RefreshCommand` sets `IsRefreshing` and a second invocation while one is in flight makes
  no second `TryRefreshAsync` call; `DismissCommand` calls the center and the list reloads via
  `Changed`. (R-8, R-10, R-14, D-10)
- [ ] 6.3 Write failing `tests/…Integration/UI/ScopedNotificationCenterTests.cs`: each operation
  runs inside its own `IServiceScopeFactory.CreateScope()`; the `SemaphoreSlim` gate makes
  `TryRefreshAsync` return `false` while a run is in flight; `Changed` fires in `finally` for a
  successful refresh, a failed refresh, dismiss, and mark-read; an exception from
  `DetectNotificationsUseCase` is caught, logged, and swallowed, never propagated. (R-15, D-9)
- [ ] 6.4 Create `tests/…Unit/TestDoubles/FakeNotificationCenter.cs`: records calls, `TryRefresh`
  gateable by the test, can raise `Changed` — a Phase-17-owned double (D-15).
- [ ] 6.5 Modify `src/…UI/Resources/Strings.resx`, `Strings.es-DO.resx`, `Strings.Designer.cs`
  (appended block): shell keys (`NotificationsTitle`, `NotificationsRefreshButton`,
  `NotificationsDismissButton`, `NotificationsEmpty`, `NotificationsBellAutomationFormat`) and
  kind keys (titles/body formats per kind from D-10, `NotificationUnnamedSubject`, the 3
  relative-date keys). (R-11, D-13)
- [ ] 6.6 Create `src/…UI/Formatting/NotificationTextFormatter.cs`: pure static
  `Format(NotificationDto, DateOnly today, CultureInfo culture) → (Title, Body)`. Run 6.1 green.
  (R-11, D-10)
- [ ] 6.7 Create `src/…UI/Services/{INotificationCenter,ScopedNotificationCenter}.cs`: the 5-method
  port and the `Task.Run` + per-scope `AppDbContext` + `SemaphoreSlim(1,1)` gate + captured
  `SynchronizationContext` `Changed` event implementation, catching and logging every non-
  cancellation exception. Run 6.3 green. (R-15, D-9)
- [ ] 6.8 Create `src/…UI/ViewModels/{NotificationListViewModel,NotificationItemViewModel}.cs`:
  observable `Items`/`UnreadCount`/`IsOpen`/`IsRefreshing`/`IsEmpty`, computed `BadgeText`/
  `HasUnread`, `OnIsOpenChanged` mark-read-only-listed-rows, `RefreshCommand`, `DismissCommand`. Run
  6.2 green. (R-8, R-10, R-14, D-10)
- [ ] 6.9 Modify `src/…UI/Composition/Bootstrap.cs`: append the Fase 17 block (after the latest
  landed Phase 15/16 block) registering `DetectNotificationsUseCase`, `GetNotificationsQuery`,
  `MarkNotificationsReadUseCase`, `DismissNotificationUseCase`, `GetNotificationSettingsQuery`,
  `UpdateNotificationSettingsUseCase` as transient, and `INotificationCenter`/
  `NotificationListViewModel` as singleton. (D-14)
- [ ] 6.10 Run all four gates. All green before opening PR S5a.

## 7. Shell surface (S5b)

Depends on 6. Goal: the bell, badge, and flyout render in a new `MainWindow.axaml` header row
owned by `ShellViewModel`, triggered once after the main window shows.

- [ ] 7.1 Modify `tests/…Unit/UI/ShellViewModelTests.cs`: add a `CreateNotifications()` helper
  building `new NotificationListViewModel(new FakeNotificationCenter(), new FakeClock{…},
  NullLogger…)` and pass it as the third argument at all 5 `new(fakeNav, CreateLocalizer())` call
  sites; every existing assertion stays unchanged. (D-15)
- [ ] 7.2 Modify `src/…UI/ViewModels/ShellViewModel.cs`: add a third constructor parameter
  `NotificationListViewModel notifications`, exposed as `public NotificationListViewModel
  Notifications { get; }`; menu/navigation logic untouched. Run 7.1 green. (R-3)
- [ ] 7.3 Invoke the `.claude/skills/frontend-design` skill for the new header row before touching
  XAML — existing tokens only (`SurfaceBrush`, `HairlineBrush`, `AmberBrush`). (R-3)
- [ ] 7.4 Modify `src/…UI/Views/MainWindow.axaml`: root becomes `<Grid RowDefinitions="Auto,*">`;
  row 0 is a header `Border` with a right-aligned bell `ToggleButton` (`IsChecked="{Binding
  Notifications.IsOpen}"`, `AutomationProperties.Name="{Binding Notifications.
  BellAutomationName}"`), an unread badge bound to `Notifications.BadgeText`/`HasUnread`, and a
  `Popup` (`IsOpen="{Binding Notifications.IsOpen, Mode=TwoWay}"`, `IsLightDismissEnabled="True"`,
  `Placement="BottomEdgeAlignedRight"`) containing the refresh button, an `ItemsControl` in a
  `ScrollViewer` with per-row dismiss, and an empty-state `TextBlock`; row 1 is the existing
  `DockPanel`, unchanged; clicking a row does nothing (R-16). (R-3, R-8, R-16, D-10)
- [ ] 7.5 Modify `src/…UI/App.axaml.cs`: after `desktop.MainWindow = mainWindow;`, resolve
  `INotificationCenter` and add `mainWindow.Opened += (_, _) => _ =
  notificationCenter.RefreshAsync();`. (R-15, D-10)
- [ ] 7.6 Smoke-launch the app (`dotnet run --project src/ControlDeFinanzasIA.UI`): confirm the
  new header row, bell, badge, and popup render with no DI/startup failure. Screenshot only, no
  simulated clicks.
- [ ] 7.7 Run all four gates. All green before opening PR S5b.

## 8. Settings UI (S6)

Depends on 7. Goal: `SettingsView` gains a "Notificaciones" section wired to the same per-section
save pattern as the rest of the screen, and saving triggers exactly one detection run.

- [ ] 8.1 Modify `tests/…Unit/UI/SettingsViewModelTests.cs` (the sole ctor site at line ~296) and
  `SettingsViewModelTestConnectionTests.cs` (line ~144): add the 3 new arguments
  (`GetNotificationSettingsQuery`, `UpdateNotificationSettingsUseCase`, `INotificationCenter`) over
  Phase-17-owned doubles; add tests: `LoadAsync` populates the 4 toggles + frequency;
  `SaveNotificationsAsync` success sets `StatusMessage` and calls `RefreshAsync` exactly once;
  failure sets `FormError` and triggers no run. (R-14, R-17, D-11, D-15)
- [ ] 8.2 Modify `src/…UI/Resources/Strings.resx`/`Strings.es-DO.resx`/`Strings.Designer.cs`
  (appended block): `SettingsNotificationsTitle`, `SettingsNotificationsDebtDue`,
  `SettingsNotificationsRecurringDue`, `SettingsNotificationsBudget`,
  `SettingsNotificationsAnomaly`, `SettingsNotificationsDigestLabel`,
  `SettingsNotificationsDigestOff/Daily/Weekly`. (R-17, D-13)
- [ ] 8.3 Modify `src/…UI/ViewModels/SettingsViewModel.cs`: add the 3 ctor params after
  `testAiConnectionUseCase` (`logger` stays last); add `NotifyDebtDue`/`NotifyRecurringDue`/
  `NotifyBudget`/`NotifyAnomaly`/`DigestFrequency` observable state and
  `DigestFrequencyOptions` (the `ThemeOption` shape); extend `LoadAsync` with one
  `GetNotificationSettingsQuery` call; add `[RelayCommand] SaveNotificationsAsync` (clear messages,
  call `UpdateNotificationSettingsUseCase`, on failure set `FormError`, on success set
  `StatusMessage` then `_ = _notificationCenter.RefreshAsync();`, no `ConfigureAwait(false)`). Run
  8.1 green. (R-14, R-17, D-11)
- [ ] 8.4 Modify `src/…UI/Views/SettingsView.axaml`: a new `ledger-row` `Border` between the IA and
  Acerca de sections with 4 `CheckBox`es, a `ComboBox` for the digest frequency, and a
  `ledger-btn-primary` save button. (R-17, D-11)
- [ ] 8.5 Run all four gates. All green before opening PR S6.

## 9. Digest (S7)

Depends on 8. Goal: the digest kind is generated at most once per period with no backfill, ignores
per-kind toggles and dismissals, and the project docs record the phase's status.

- [ ] 9.1 Write failing tests: `DigestPeriodTests` (Daily → `today`; Weekly → the Monday of the ISO
  week, including a Sunday input); `NotificationReconcilerTests` digest branch (a new digest
  removes any older undismissed digest; frequency `Off` removes undismissed digest rows);
  `DetectNotificationsUseCaseTests` digest scenarios — a 5-week-stale cursor with `Weekly` produces
  exactly one digest for the current week and the cursor jumps straight to this Monday, with no
  rows for the intervening weeks (S-9); all-zero counts create no row but still advance the cursor;
  digest content ignores per-kind toggles and existing dismissals (Reconciliation #8); the cursor
  is written only after the reconcile save succeeds. (R-13, D-12)
- [ ] 9.2 Extend `NotificationTextFormatterTests.cs`: `NotificationDigestDailyTitle`/
  `WeeklyTitle`, and the body joining per-currency totals with `" + "`. (R-11, D-10, D-12)
- [ ] 9.3 Create `src/…Application/Notifications/DigestPeriod.cs`: pure
  `Start(DigestFrequency freq, DateOnly today)`. Run 9.1's period-boundary tests green. (D-12)
- [ ] 9.4 Modify `src/…Application/Notifications/NotificationReconciler.cs`: implement the digest
  branch — evaluate `Digest` only when `digestDue || frequency == Off`; adding a new digest removes
  every other undismissed digest; `Off` removes any undismissed digest row. Run 9.1's reconciler
  tests green. (R-13, D-4, D-12)
- [ ] 9.5 Modify `src/…Application/UseCases/Notifications/DetectNotifications/
  DetectNotificationsUseCase.cs`: compute `periodStart`/`digestDue`; read all four sources in full
  (toggle- and dismissal-independent) when `digestDue`; build the digest candidate (payment count +
  per-currency totals, budget Near/Exceeded count, current-month unacknowledged anomaly count); set
  `evaluatedKinds` to include `Digest` per D-5 step 5; persist the `yyyy-MM-dd` cursor via
  `SettingKeys.NotificationsDigestLastPeriodStart` only after a successful reconcile. Run 9.1's use-
  case tests green. (R-13, D-5, D-7, D-12)
- [ ] 9.6 Modify `src/…UI/Formatting/NotificationTextFormatter.cs` and
  `Strings.resx`/`Strings.es-DO.resx`/`Strings.Designer.cs` (appended block):
  `NotificationDigestDailyTitle`, `NotificationDigestWeeklyTitle`, `NotificationDigestFormat`. Run
  9.2 green. (R-11, D-10, D-13)
- [ ] 9.7 Modify `AGENTS.md`: update the Status line to record Phase 17 status, mirroring the
  existing per-phase status-line pattern (append-only, per the coexistence gate above).
- [ ] 9.8 Run all four gates. All green before opening PR S7.

## 10. Cierre

- [ ] 10.1 Run the full gate suite across the whole phase: build, `dotnet test` (unit +
  integration), `dotnet format style --verify-no-changes`, NetArchTest R-1..R-5,
  `ModelSnapshotConsistencyTests`. All green. Confirm zero changes to `AppSettings`,
  `DebtPayment`, `RecurringRule`, `Budget`, `AnomalyAlert`, `GetUpcomingPaymentsQuery`,
  `GetBudgetVsActualQuery`, `DetectAnomaliesUseCase`, `NavigationService`, `CurrentViewConverter`,
  `NavigationServiceTests`, or `FakeSettingsStore` (R-18, Out of Scope).
- [ ] 10.2 Manual run against an existing dev DB in both locales: app open shows the relevant
  notices (S-1); a budget crossing 80%/100% fires Near then Exceeded and removes the undismissed
  Near (S-2, S-3); silencing a kind in Settings stops new notices and removes existing undismissed
  ones (S-3, S-7); the badge renders `9+` above 9 unread and the flyout caps at 20, unread-first
  (S-11); a forced reconcile-save failure (S-12) leaves the app usable and a later manual refresh
  succeeds.
- [ ] 10.3 Write `verify-report.md` in `.atl/sdd/f-017p17-notifications/`, independently
  re-verifying every Acceptance Criteria checklist line against source and tests before archiving —
  including all 13 Spec Reconciliation points (fact refresh on undismissed rows, digest ignoring
  toggles/dismissals, single `DedupKey` string, anomaly 60-day source cutoff, deactivated recurring
  rule exclusion, indeterminate budgets kept, purge+apply in one `SaveChangesAsync`, digest
  window/cursor details, manual-refresh no-op + queued startup/settings runs, flyout marking only
  listed rows, `NavigationServiceTests` needing no change while both `SettingsViewModel` fixtures
  do, the `ToggleButton`+`Popup` flyout shape, and overdue derived at display time).
- [ ] 10.4 Archive the spec (`F-017-notifications.spec.md` → `.done.md`) and write
  `archive-report.md` in `.atl/sdd/f-017p17-notifications/`.
- [ ] 10.5 Create `spec/phases/phase-17/features.md` following the pattern of
  `spec/phases/phase-10/` through `phase-16/`.
- [ ] 10.6 Update `docs/roadmap.md` §Fase 17: mark the phase's completion criteria complete.
- [ ] 10.7 Update `docs/AI_HANDOFF.md` with the Phase 17 summary, including the fact-refresh-on-
  undismissed-rows rule and the digest's toggle/dismissal-independent counting, so a future reader
  does not "fix" either as a bug.
