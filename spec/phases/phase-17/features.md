# Phase 17 — Notificaciones

> **Phase:** 17 — Notificaciones (in-app)
> **Status:** ✅ Completed (2026-09-28) — see `spec/features/phase-17/F-017-notifications.done.md`
> **Spec ID:** F-017
> **Dependencies:** Phase 6 ✅ (recurring rules), Phase 7 ✅ (debts), Phase 10 ✅ (budgets), Phase 14 ✅
> (`AnomalyAlert`). No dependency on the Phase 13-16 AI pipeline: notification text is never AI-generated.
> **Blocks:** None
> **Roadmap:** `docs/roadmap.md` §Fase 17
> **Stack:** .NET 8 · Avalonia 11.2.7 (MVVM strict) · EF Core 8 + SQLite (one additive migration,
> `20260926064030_AddNotifications`)

## 1. Summary

Phase 17 adds a bell with an unread badge to a new header row of the shell. On app start (and after
saving the Notificaciones settings, and on a manual refresh button) a deterministic
`DetectNotificationsUseCase` reads four existing sources, builds candidate notifications, and hands
them to a `NotificationReconciler` that persists the result in a `Notification` table with one
`SaveChangesAsync`. The four detectable kinds are `DebtInstallmentDue` (window `[today-7, today+3]`,
overdue derived at display time), `RecurringChargeDue` (forward window `[today, today+3]`),
`BudgetThreshold` (current month, highest tier only, `Near` at 80% / `Exceeded` at 100%) and `Anomaly`
(unacknowledged `AnomalyAlert` rows). A fifth kind, `Digest`, is generated at most once per period
(`Off`/`Daily`/`Weekly`, default Weekly) behind a `yyyy-MM-dd` cursor that is written only after a
successful reconcile and never backfills missed periods.

The table is keyed by a single `DedupKey` string with a unique index, so re-running detection is
idempotent; a dismissed row is never recreated; an undismissed row whose source stops qualifying is
removed; facts on undismissed rows are refreshed without touching `Id`/`CreatedAt`/`ReadAt`; rows whose
reference date is more than 60 days old are purged in the same save. `Notification` stores facts only;
`NotificationTextFormatter` renders the text at display time from resx (`es-DO`/`en`).

Confirmed product decisions carried through unchanged: persisted reconciled table (D1); bell + badge +
popup in the shell, no new route (D2); budget key `(BudgetId, Month, Tier)` so a dismissed Near never hides
a later Exceeded (D3); trigger is app start plus manual refresh only, no timer (D6); the four per-kind
toggles default on and live behind new `SettingKeys` via `ISettingsStore`, `AppSettings` untouched
(D6/D16); opening the popup marks the listed rows read, dismissing is separate and permanent (D12); 20-row
cap with unread-first ordering and a full-count badge rendering `9+` above 9 (D14); each run has its own
DI scope and swallows every failure (D20); no click-through (D21).

## 2. Model

```
Domain/Notifications/
  Notification                — sealed, private setters, per-kind For* factories, MarkRead/Dismiss
                                 (idempotent, first timestamp wins), RefreshFacts (throws when dismissed),
                                 HasSameFacts; facts only, label sanitized and capped at 120 chars
  NotificationKind/Tier/DigestFrequency, NotificationCurrencyTotal, NotificationKeys (DedupKey shapes)

Application/
  Abstractions/INotificationRepository
  Notifications/
    NotificationRules         — pure debt / recurring / budget / anomaly candidate builders
    DigestComposer            — pure digest builder (separate class, not in NotificationRules)
    DigestPeriod              — Start(frequency, today), WindowEnd(frequency, today)
    NotificationReconciler    — plan: add / remove / refresh facts / purge; digest branch
    NotificationSettings(+Reader) — per-kind toggles + digest frequency, missing/invalid = on / Weekly
  UseCases/Notifications/
    DetectNotifications       — sequential source reads, toggles, digest cursor, single reconcile
    GetNotifications, MarkNotificationsRead, DismissNotification,
    GetNotificationSettings, UpdateNotificationSettings

Infrastructure/Persistence/
  Configurations/NotificationConfiguration, Repositories/NotificationRepository
  (ApplyReconciliationAsync = one SaveChanges, detach-not-clear on DbUpdateException),
  Migrations/20260926064030_AddNotifications (additive only)

UI/
  Services/INotificationCenter, ScopedNotificationCenter  — own DI scope per run, in-flight guard,
                                 queued startup/settings runs, Changed event marshalled to the UI context
  ViewModels/NotificationListViewModel, NotificationItemViewModel; ShellViewModel owns the bell state
  Formatting/NotificationTextFormatter
  Views/MainWindow.axaml      — new header row: ToggleButton bell + badge + Popup (ScrollViewer MaxHeight 400)
  Views/SettingsView.axaml + SettingsViewModel — "Notificaciones" section, save triggers one run
  App.axaml.cs                — mainWindow.Opened -> INotificationCenter.RefreshAsync (fire-and-forget)
```

## 3. Slice plan

| ID | Slice | Commit | Status |
| --- | --- | --- | --- |
| S1a | Domain entity, kinds, keys | `1b0c9cf` | ✅ Completed |
| S1b | Persistence + migration | `c9130a5` | ✅ Completed |
| S2 | Settings keys + reconciler | `73bba65` | ✅ Completed |
| S3 | Debt + recurring detection | `94f32e3` | ✅ Completed |
| S4 | Budget + anomaly detection, read/dismiss | `8effc0d` | ✅ Completed |
| S5a | UI service, list VM, formatter | `bac31b2` | ✅ Completed |
| S5b | Shell bell + startup trigger | `5e72036` | ✅ Completed |
| S6 | Settings section | `c879ce2` | ✅ Completed |
| S7 | Digest | `d0c9552` | ✅ Completed |

Nine strictly chained slice commits. S1b, S5b and S6 carried automated smoke launches (new table, first
shell header row, new `SettingsViewModel` constructor arguments); no crash was observed. Phase 16 was
verified and archived concurrently in the same working tree (`7f7f012`); the two phases share no code
path.

## 4. Phase completion criteria

- **(roadmap)** On app open the bell shows the relevant notices for today. ✅ (test level) —
  `DetectNotificationsUseCaseTests` cover all sources; the startup trigger is wired in `App.axaml.cs` but
  has no automated test and no manual run was performed.
- **(roadmap)** Budget alerts fire at 80% and 100%; a dismissed Near never hides a later Exceeded. ✅ —
  `Plan_DismissedNearPlusLaterExceededCandidate_AddsExceededUnaffected` and siblings.
- **(roadmap)** The user can silence each kind. ✅ — per-kind toggles remove existing undismissed rows and
  block new ones; digest has its own frequency `Off`.
- Idempotency, dismissed-never-recreated, horizon edges, digest no-backfill, 60-day purge, `9+` badge and
  20-row cap all covered by named tests (see the spec's Acceptance Criteria evidence). ✅
- Build clean, full suite green, `dotnet format style --verify-no-changes` exit 0, NetArchTest R-1..R-5
  green, `ModelSnapshotConsistencyTests` green. ✅ — 1700/1700 (1405 unit + 295 integration, up from
  1522/1522 at Phase 16's close), NetArchTest 11/11, snapshot tests 3/3.

## 5. Findings carried forward, not fixed in this phase

- **Verify warnings:** W1 apply-progress not on disk; W2 nine undocumented design divergences (now recorded
  in the spec's "Post-apply reconciliation"); W3 manual task 10.2 and the S-12 forced-failure check were not
  run manually; W4 "exactly one SaveChanges" proven structurally, not by a save-count test; W5 the S-12
  startup trigger has no automated test.
- **Unreachable `budgetReadFailed` branch** in `DetectNotificationsUseCase` (~lines 212-227): defensive and
  untested because `GetBudgetVsActualQuery` never returns `Failure` today.
- **Fact refresh on undismissed rows is intentional** (Reconciliation): a Near notice showing 85% must
  follow the budget to 95% without touching `Id`/`CreatedAt`/`ReadAt`. Not to be "fixed" into immutability.
- **The digest ignores per-kind toggles and dismissals** by design (its own on/off is the frequency).
  Not to be "fixed" into double-suppression.
- **`RetentionDays = 60` is declared in both `NotificationRules` and `NotificationReconciler`;** keep them
  in step.
- **`MainWindow.axaml` hardcodes `#22E8A33D` / `#33E8A33D`** (precedent `ChatView.axaml:29`); move to a
  design token when next touched.
- **`ScopedNotificationCenter` captures `SynchronizationContext.Current`** at construction; it must be
  resolved on the UI thread.
- **S-4 idempotency is tested at reconciler level only,** not end to end through the use case.
- **The unread accent stripe on popup rows is effectively never visible,** because opening the popup marks
  the listed rows read.
- **No headless Avalonia test harness still exists** (same gap recorded since Phase 8).

## 6. References

- `docs/roadmap.md` §Fase 17 — original plan, now marked complete
- `spec/features/phase-17/F-017-notifications.done.md` — archived formal spec, evidence per acceptance
  criterion, post-apply reconciliation
- `.atl/sdd/f-017p17-notifications/{exploration,proposal,design,verify-report,archive-report}.md` — full
  traceability
- `docs/architecture.md` — Phase 17 entry
- `spec/features/phase-14/F-014-ai-insights-forecast-anomalies.done.md` — Phase 14, the `AnomalyAlert`
  pattern this phase mirrors
