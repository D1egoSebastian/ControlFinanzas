# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed

- The Windows executable is a GUI-subsystem app (`WinExe`): no console window opens next to the UI and closing a console
  no longer kills the app. A start-up failure still writes `startup-error-*.log` and exits non-zero; with no console
  there is no stderr line for a double-clicked exe (run `dotnet ControlDeFinanzasIA.UI.dll` to see it on a terminal).
- Recurring rules: the form asks for the day of the month the charge is always made (1-31; the month and day for a
  yearly charge) instead of a raw start date, and shows the first charge read-only ("Next charge: ..."). The type
  selector explains Subscription and Fixed monthly expense in plain language. The stored model is unchanged (the day
  lives in the start date) and no migration was needed.
- Recurring rules: saving an edit can no longer change the schedule. The form re-sends the stored start date and next
  charge untouched unless the day, month or frequency changed; a changed schedule moves the next charge to the new
  day. The row "Skip" button now skips the rule of its own row (it used to skip whichever rule was open in the form),
  and the list refreshes after "Confirm" instead of showing a stale next-charge date.
- Recurring rules: editing the amount or currency of a rule was silently dropped by the database (the amount is an
  owned type that the update did not copy). It is persisted now.
- Recurring rules: a day 29, 30 or 31 charge is clamped to the last day of a short month for that month only and
  returns to its own day afterwards (a day 30 charge used to slide to the 31st after February).
- Recurring rules: after creating a rule the form is cleared, so pressing Save twice no longer registers it twice.
- The recurring "Generar del mes" preview now shows rule name, amount, currency and date instead of blank rows.
- Expense and income rows show the currency next to the amount.
- Debt schedule: Pay and Skip are disabled for installments that are no longer pending, rejected actions show a
  localized message without internal ids, and the debt stays selected after a payment.
- Frequency, rule type and status values in the Recurring and Debts screens are localized (es-DO / en).
- Expense and income lists are no longer capped at the newest 20 rows: a pager (previous / next, "Page X of Y" and the
  total count) reaches every record, and any record can be opened for editing.
- Expenses and income can be deleted again: each row has a Delete button with an inline confirmation (the same button
  turns into Confirm, with a Cancel next to it). The list refreshes, the total decrements and the page steps back when
  the last row of the last page is deleted.
- The filter panel of Phase 4 is back on both lists: category and from / to date range (the DatePicker pattern of the
  entry form), Apply and Clear, an inline message for an inverted date range, and the existing empty states for "no
  data yet" and "no results for this filter". Pressing Enter in the search box applies the filter instead of
  registering the entry form above it.
- Expense and income rows show the amount in the other currency again ("≈ 600.00 DOP" under a USD row), as Phase 5
  documented.

### Added

- Dashboard: an "Expenses this month" card right under the summary cards (before the charts) lists the latest expenses
  of the displayed month, newest first (date, description, category, exact amount with its currency, and the DOP
  equivalent for foreign-currency rows; at most 8 rows), with an "N expenses in <month>" line, a "Showing the latest 8"
  note when there are more, a "View all" button that opens Expenses filtered to that month, and an empty state when the
  month has none. It follows the previous / next month buttons. No schema change, no migration, no new package.
- Text search on the Expenses and Income lists (partial, case-insensitive for ASCII, wildcard characters typed by the
  user are searched literally). It is also part of the saved filter and of the filtered PDF / Excel export, whose
  summary now echoes it.
- Saved views on the Expenses list (save the current filter under a name, select it to apply it, delete with an inline
  confirmation).

### Known gaps (documented, not fixed here)

- Tags have no UI: nothing in the app can assign a tag to an expense or income, so a tag filter or "new tag" form
  would be dead controls. The view-models keep the tag members; the views do not expose them.
- Saved views on the Income list and the payment-method filter are not exposed yet (the view-model supports them).

## [1.0.0]

First release. A cross-platform desktop application for personal finance control, AI-assisted, with 100% local data.
Summary by feature area (Phases 0-21).

### Added

- **Foundation (Phases 0-2):** .NET 8 solution with Domain, Application, Infrastructure and Avalonia UI layers,
  architecture rules enforced by tests, SQLite persistence through Entity Framework Core with versioned migrations.
- **Expenses and income (Phases 3-4):** register, list, edit and delete expenses and income, categories, filters and
  saved views (the tag data model exists; there is no tag UI yet, see "Known gaps" under Unreleased).
- **Payment methods and multi-currency (Phase 5):** payment methods and DOP/USD amounts with conversion to a single
  reporting currency.
- **Recurring charges (Phase 6):** subscriptions and fixed expenses with due-date tracking.
- **Debts (Phase 7):** debts with installment plans and payment tracking.
- **Dashboard and calendar (Phases 8-9):** period summaries, charts, and a monthly expense calendar.
- **Budgets and goals (Phases 10-11):** monthly budgets per category with progress tiers, savings goals with target
  dates, manual contributions and a deterministic savings plan.
- **Settings (Phase 12):** theme, language (Spanish es-DO and English) and AI provider configuration.
- **AI assistance (Phases 13-16):** automatic expense categorization, month-end forecast, anomaly detection, insights,
  a chat about your own finances and AI-written savings plans. Every figure is computed locally and deterministically;
  the model only phrases it.
- **Notifications (Phase 17):** in-app bell with due installments, recurring charges, budget alerts, anomalies and a
  periodic digest.
- **Export (Phase 18):** PDF and Excel reports of the dashboard month and of the filtered expense list.
- **Backup and restore (Phase 19):** verified ZIP backups with integrity checks, safe restore with automatic rollback.
- **Import (Phase 20):** CSV and Excel import with preview, column mapping and duplicate detection.
- **Packaging, polish and accessibility (Phase 21):** application version 1.0.0 and icon, rolling log files in the
  data directory, an unexpected-error screen with a short reference id, keyboard shortcuts for the main actions,
  localized empty states, screen-reader names and focus styles, and a build script that produces self-contained
  archives with SHA-256 checksums for Windows (zip), macOS (tar.gz and .app.zip) and Linux (tar.gz). Only the Windows
  archive was run; macOS and Linux are provided, untested.

### Not included

- There is no installer: no MSIX, no DMG or AppImage is distributed yet (their recipes are written but were never
  built), and the binaries are not code-signed or notarized.
- There is no auto-update: new versions are downloaded manually.
- The GitHub release workflow only creates a draft release on a `v*` tag and has never been run.
- Screen-reader support is based on automated checks against Avalonia automation peers; Narrator, NVDA, VoiceOver and
  Orca were never run by hand. See `docs/accessibility.md` and `docs/packaging-unverified.md`.

### Security

- No remote telemetry. The AI provider key is stored only in the local data file and is never written to log files.
