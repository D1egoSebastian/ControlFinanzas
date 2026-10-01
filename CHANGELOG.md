# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Nothing yet.

## [1.0.0]

First release. A cross-platform desktop application for personal finance control, AI-assisted, with 100% local data.
Summary by feature area (Phases 0-21).

### Added

- **Foundation (Phases 0-2):** .NET 8 solution with Domain, Application, Infrastructure and Avalonia UI layers,
  architecture rules enforced by tests, SQLite persistence through Entity Framework Core with versioned migrations.
- **Expenses and income (Phases 3-4):** register, list, edit and delete expenses and income, categories, tags, filters
  and saved views.
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
