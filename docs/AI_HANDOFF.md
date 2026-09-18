# AI_HANDOFF.md — ControlDeFinanzasIA

> Self-contained handoff document for any AI agent picking up the project tomorrow.
> Verified against the filesystem on **2026-09-17**. All claims are sourced from
> `AGENTS.md`, `spec/`, `docs/`, or the repo's actual state.

---

## 1. TL;DR

- **What it is:** `ControlDeFinanzasIA` — a cross-platform (.NET 8 + Avalonia MVVM) desktop app for personal-finance control with AI assist. 100% local data. DOP + USD.
- **Current phase:** **Phase 6 archived** (Recurring rules: subscriptions + fixed expenses). Phases 0–6 fully archived. **Phase 7 (Debts) scaffolding in progress.**
- **Test count:** 614/614 passing (492 unit + 122 integration) as of 2026-09-17, Phase 6 close.
- **Key constraint:** Filesystem-based SDD specs in `spec/features/phase-NN/` (NOT OpenSpec). `.spec.md` = active, `.done.md` = archived. Do not create `openspec/`.
- **Architecture:** Clean Architecture (Domain / Application / Infrastructure / UI). NetArchTest rules R-1..R-5 enforced in `Tests.Unit`.

---

## 2. Project at a glance

| Field | Value |
| --- | --- |
| Name | `ControlDeFinanzasIA` |
| Stack | C# on **.NET 8 (LTS)**, **Avalonia 11.2.7** UI, **EF Core 8.0.11 + SQLite** (Code First), `Microsoft.Extensions.DependencyInjection`, **xUnit + FluentAssertions 11.10.0** |
| Solution | `ControlDeFinanzasIA.sln` at repo root |
| Type | Cross-platform desktop (Windows, macOS, Linux), single-user, local data only |
| UI pattern | MVVM strict with **CommunityToolkit.Mvvm** (`[ObservableProperty]`, `[RelayCommand]`) |
| Localization | `es-DO` default + `en` via `IStringLocalizer` / resx |
| Status | Phases 0–5 ✅ archived; Phase 6 in progress (recurring rules) |

---

## 3. Current state — phase-by-phase

Counts derived by globbing `spec/features/phase-NN/*.md` and splitting `.done.md` vs `.spec.md`.

| # | Phase | Total features | Done (`.done.md`) | Active (`.spec.md`) | % complete | Phase plan |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Bootstrap | (predates feature spec convention) | — | — | 100% | `docs/roadmap.md` §Fase 0 |
| 1 | Architecture & contracts | 8 | 8 | 0 | 100% | `spec/phases/phase-01/features.md` |
| 2 | Persistence (EF Core + SQLite) | 6 | 6 | 0 | 100% | `spec/phases/phase-02/features.md` |
| 3 | Slice vertical: register/list expenses | 7 | 7 | 0 | 100% | `spec/phases/phase-03/features.md` |
| 4 | Income + edit/delete + tags + filters + saved views | 8 | 8 | 0 | 100% | `spec/phases/phase-04/features.md` |
| 5 | Payment method + multi-moneda DOP/USD | 3 (`F-501..F-503`; plan says 13 sub-features 5-001..5-013 grouped) | 3 | 0 | 100% | `spec/phases/phase-05/features.md` |
| 6 | Recurring rules (subscriptions + fixed expenses) | 1 umbrella `F-001` covering 8 sub-features (6-001..6-008) | 1 | 0 | 100% | `spec/phases/phase-06/features.md` + `spec/features/phase-06/F-001-recurring-rules.done.md` |

### Per-phase detail

**Phase 1** — `spec/features/phase-01/` (8 done)
- `F-001..F-008`: solution skeleton, Money VO, DateRange VO, IClock/IPathResolver abstractions, Result/PagedResult, repository contracts, NetArchTest, composition root with DI host.

**Phase 2** — `spec/features/phase-02/` (6 done)
- `F-001..F-006`: AppDbContext, EF Core configurations + initial migration, seed data (9 categories + 5 payment methods), `CategoryRepository` + `PaymentMethodRepository`, composition root persistence wiring, integration tests with SQLite in-memory.

**Phase 3** — `spec/features/phase-03/` (7 done)
- `F-001..F-007`: Expense entity, configuration + migration, repository with `GetByFilterAsync`, `RegisterExpenseUseCase` + DTOs, `GetTransactionsQuery`, shell + navigation infrastructure, `ExpensesViewModel` + `ExpensesView` (Phase 3 capstone).

**Phase 4** — `spec/features/phase-04/` (8 done)
- `F-001..F-008`: Income entity, repository, register use case, Tag entity + M:N, update/delete use cases, SavedView with JSON versionado, filter extension with TagIds, `IncomeView` + filter inputs + saved views UI (Phase 4 capstone "Libro Mayor").

**Phase 5** — `spec/features/phase-05/` (3 done)
- `F-501-exchange-rate-stub`, `F-502-currency-converter`, `F-503-payment-method-id-entities`. Plan-level IDs 5-001..5-013 grouped under these three specs. Payment method + multi-moneda wired end-to-end. `559/559 tests verde` at close (2026-09-08).

**Phase 6** — `spec/features/phase-06/` (1 active)
- `F-001-recurring-rules.spec.md` (umbrella). 8 sub-features in `spec/phases/phase-06/features.md`:
  - `6-001` Calculator (Monthly/Bimonthly/Yearly, leap year, end-of-month) — ✅
  - `6-002` Strengthen `RecurringRule` (factory `Create`, invariants) — ✅
  - `6-003` `IRecurringRuleRepository` + EF Core impl + migration `20260910205248_AddRecurringRules` — ✅
  - `6-004` `CreateSubscriptionUseCase` + `UpdateSubscriptionUseCase` — ✅ backend; ⏳ formal tests
  - `6-005` `GenerateDueRecurringUseCase` (botón "Generar del mes") — code exists, preview/confirm path not verified
  - `6-006` UI: lista + form create/edit — 🔄 Subscription demo implemented (`RecurringRulesViewModel` + `RecurringRulesView.axaml`); broader coverage pending
  - `6-007` UI: "Generar del mes" screen with preview — ⏳
  - `6-008` Same flow for `FixedExpense` — backend `Create/Update/Delete/Deactivate FixedExpense` use cases present; UI coverage partial

---

## 4. Phase 6 — archived (2026-09-17)

### What's done (backend)

| Layer | Files |
| --- | --- |
| Domain | `src/ControlDeFinanzasIA.Domain/Recurring/RecurringRule.cs`, `Subscription.cs`, `FixedExpense.cs`, `RecurringFrequency.cs`, `RecurringOccurrenceCalculator.cs` |
| Application (abstractions + use cases) | `Application/Abstractions/IRecurringRuleRepository.cs`; `Application/UseCases/Recurring/{CreateSubscription, UpdateSubscription, DeleteRecurringRule, DeactivateRecurringRule, CreateFixedExpense, UpdateFixedExpense, DeleteFixedExpense, DeactivateFixedExpense, GenerateDueRecurring, SkipNextOccurrence}/*.cs` |
| Infrastructure | `Infrastructure/Persistence/Configurations/RecurringRuleConfiguration.cs` (TPH); `Infrastructure/Persistence/Repositories/RecurringRuleRepository.cs`; migration `20260910205248_AddRecurringRules` |

### What's done (UI)

- `src/ControlDeFinanzasIA.UI/Views/RecurringRulesView.axaml(.cs)`
- `src/ControlDeFinanzasIA.UI/ViewModels/RecurringRulesViewModel.cs` — pulls in all recurring use cases; rule type toggle between `Subscription` and `FixedExpense`; create + update demo wired.

### Closing tasks — all done (per `tasks/phase-06/F-001-recurring-rules.tasks.md`)

All 12 items (1.1–4.5) are checked off. Summary of what closed them:

- Schema/round-trip/provenance tests (`ExpenseSchemaTests`, `RecurringRuleRepositoryTests`).
- FixedExpense CRUD + tests (`FixedExpenseUseCaseTests`, `RecurringLifecycleUseCaseTests`).
- Skip + edit-generated-movement tests (`GeneratedExpenseTests`).
- `GenerateDueRecurringUseCase` preview/idempotent-confirm tests (`GenerateDueRecurringUseCaseTests`).
- 16 calculator date scenarios (`RecurringOccurrenceCalculatorTests`), exceeds the ≥10 spec minimum.
- Unified UI for Subscription + FixedExpense with "Generar del mes" preview/confirm section
  (`RecurringRulesView.axaml` + `RecurringRulesViewModel`).
- Gates: build clean, 614/614 tests, `dotnet format style` clean, NetArchTest 11/11.
- Spec archived to `spec/features/phase-06/F-001-recurring-rules.done.md`; see
  `.atl/sdd/f-006p6-formalize-recurrentes/verify-report.md` and `archive-report.md`.

The full task breakdown is at `tasks/phase-06/F-001-recurring-rules.tasks.md`.

---

## 5. What's pending (next up)

Ordered by priority. Each item references its source-of-truth spec/plan.

1. **Phase 7 — Debts with installment plan** — `docs/roadmap.md` §Fase 7 (lines ~383–424). Proposal/spec/tasks scaffolding is under `.atl/sdd/f-007p7-debts-installment-plan/` and `spec/features/phase-07/`, `tasks/phase-07/` — ready for `apply`.
2. **Phase 8 — Period selector + dashboard + visualizations** — `docs/roadmap.md` §Fase 8 (lines ~426–463)
3. **Phase 9 — Calendar view** — `docs/roadmap.md` §Fase 9
4. **Phase 10 — Monthly budgets** — `docs/roadmap.md` §Fase 10
5. **Phase 11 — Savings goals** — `docs/roadmap.md` §Fase 11
6. **Phase 12 — Settings, i18n, IA config (no IA use yet)** — `docs/roadmap.md` §Fase 12
7. **Phase 13–16 — IA pipeline** (categorization, insights/forecast/anomalies, chat, savings plan explanation) — `docs/roadmap.md` §Fases 13–16
8. **Phase 17 — Notifications** — `docs/roadmap.md` §Fase 17
9. **Phase 18 — PDF/Excel export** — `docs/roadmap.md` §Fase 18
10. **Phase 19 — Backup/restore** — `docs/roadmap.md` §Fase 19
11. **Phase 20 — CSV/Excel import** — `docs/roadmap.md` §Fase 20
12. **Phase 21 — Packaging + accessibility** — `docs/roadmap.md` §Fase 21

**MVP (no IA) = phases 0–12.** Phases 13+ are IA + polish.

---

## 6. Architecture & constraints

### Layer model (Clean Architecture, 4 layers)

```
                  ┌──────────────────────┐
                  │       UI (Avalonia)  │
                  └──────────┬───────────┘
                             │ depends on
                             ▼
   ┌──────────────────┐  ┌────────────────────┐  ┌──────────────────────┐
   │  Infrastructure  │─▶│    Application     │─▶│       Domain         │
   └──────────────────┘  └────────────────────┘  └──────────────────────┘
           │                                              ▲
           └──── implements interfaces from ─────────────┘
                  Application (I*Repository, ILLMClient, ...)

   ┌──────────────┐        ┌──────────────────────┐
   │ Tests.Unit   │───────▶│ Domain / Application │
   └──────────────┘        └──────────────────────┘

   ┌──────────────────┐    ┌──────────────────────┐
   │ Tests.Integration│───▶│  Infrastructure      │
   └──────────────────┘    └──────────────────────┘
```

### Hard rules (enforced by NetArchTest in `tests/.../Unit/Architecture/ArchitectureTests.cs`)

- **R-1:** `Domain` has zero outward dependencies (only BCL).
- **R-2:** `Application` depends only on `Domain` and neutral packages (FluentValidation, etc.).
- **R-3:** `Infrastructure` depends on `Application` + `Domain`; no reference to `UI`.
- **R-4:** `UI` depends on `Application` + `Domain`; no reference to `Infrastructure` except in `Bootstrap` (composition root) — `Bootstrap` is whitelisted by the test filter.
- **R-5:** Tests may reference anything but must not ship in the app.

### Cross-cutting rules

- **MVVM strict in Avalonia.** No logic in code-behind. Views bind to ViewModels via CompiledBindings. `[ObservableProperty]` and `[RelayCommand]` from CommunityToolkit.Mvvm.
- **Async/await end-to-end.** Every I/O is async. `CancellationToken` plumbed through.
- **Errors as values.** Use `Result` (non-generic, `Value: object?`) + `PagedResult<T>` from `Application/Common/`. Typed exceptions reserved for the unexpected. Result factories: `Result.Success(object)` / `Result.Failure(string code, string message)`.
- **No remote telemetry.** No analytics. No remote calls except the explicit user-configured LLM.
- **No secrets in repo.** User LLM API keys live in the local data file (encryption deferred to v2 — ADR-011).
- **Localization via `IStringLocalizer`.** `es-DO` default, `en` supported. No hardcoded UI strings.
- **EF Core Code First, migrations versioned and committed.** Destructive schema changes need an explicit migration + backup plan.
- **Central Package Management.** Versions live in `Directory.Packages.props` only. `.csproj` files use `<PackageReference Include="X" />` with no `Version` attribute.
- **Money is not domain-aware of conversion.** `Money` arithmetic only; conversion is at the Application/Infrastructure layer via `ICurrencyConverter` + `IExchangeRateProvider`.

### Sources of truth

| Concern | Document |
| --- | --- |
| Why | `docs/vision.md` |
| When | `docs/roadmap.md` |
| How | `docs/architecture.md` |
| Mission | `spec/constitution/mission.md` |
| Stack constitution | `spec/constitution/tech-stack.md` |
| Per-phase plan | `spec/phases/phase-NN/features.md` |
| Per-feature spec | `spec/features/phase-NN/F-XXX-slug{.spec.md,.done.md}` |
| Per-feature tasks | `tasks/phase-NN/F-XXX-slug.tasks.md` |
| ADRs | `docs/architecture.md` §19 |

---

## 7. File map

### `src/`

```
src/
├── ControlDeFinanzasIA.Domain/            # Pure domain — no EF Core, no Avalonia
│   ├── Entities/                          # Expense, Income, Tag, SavedView, etc.
│   ├── ValueObjects/                      # Money, DateRange, ExchangeRate
│   ├── Enums/                             # Currency, RecurringFrequency, ...
│   ├── Catalog/                           # Category, PaymentMethod
│   └── Recurring/                         # RecurringRule (TPH base), Subscription,
│                                          # FixedExpense, RecurringOccurrenceCalculator
├── ControlDeFinanzasIA.Application/       # Use cases, DTOs, validators, abstractions
│   ├── Abstractions/                      # I*Repository, IClock, IPathResolver,
│   │                                      # IExchangeRateProvider, IRecurringRuleRepository, ...
│   ├── Common/                            # Result, PagedResult<T>, FilterJsonSerializer
│   ├── DTOs/                              # ExpenseDto, IncomeDto, PaymentMethodDto, ...
│   ├── UseCases/
│   │   ├── Expenses/                      # RegisterExpense, GetTransactions, UpdateExpense
│   │   ├── Incomes/                       # RegisterIncome, GetIncomes, UpdateIncome
│   │   ├── Catalog/                       # CreateTag, GetPaymentMethods
│   │   ├── SavedViews/                    # SaveCurrentFilter, ApplySavedView, ...
│   │   ├── Transactions/                  # DeleteTransaction
│   │   └── Recurring/                     # CreateSubscription, UpdateSubscription,
│   │                                      # CreateFixedExpense, UpdateFixedExpense,
│   │                                      # Delete{FixedExpense,RecurringRule},
│   │                                      # Deactivate{FixedExpense,RecurringRule},
│   │                                      # GenerateDueRecurring, SkipNextOccurrence
│   └── Services/                          # CurrencyConverter (display-time)
├── ControlDeFinanzasIA.Infrastructure/    # EF Core, SQLite, exchange rate stub
│   ├── Persistence/
│   │   ├── AppDbContext.cs
│   │   ├── ServiceCollectionExtensions.cs # AddPersistence(...)
│   │   ├── EfUnitOfWork.cs
│   │   ├── SeedData.cs                    # 9 categories + 5 payment methods (IsSystem=true)
│   │   ├── Configurations/                # One IEntityTypeConfiguration<T> per entity
│   │   ├── Repositories/                  # One per interface
│   │   └── Migrations/                    # Versioned; includes
│   │                                      # 20260910205248_AddRecurringRules
│   ├── System/                            # SystemClock, SystemPathResolver
│   └── ExchangeRates/StubExchangeRateProvider.cs
└── ControlDeFinanzasIA.UI/                # Avalonia 11.2.7 (MVVM strict)
    ├── Program.cs                         # Entry point; wires Avalonia + host
    ├── App.axaml(.cs)                     # App root; registers DataTemplate per VM
    ├── Composition/                       # Bootstrap.ConfigureServices (composition root)
    ├── Navigation/                        # INavigationService, NavigationService, RouteNames
    ├── ViewModels/                        # ShellViewModel, DashboardViewModel,
    │                                      # ExpensesViewModel, IncomeViewModel,
    │                                      # RecurringRulesViewModel, ViewModelBase
    ├── Views/                             # One .axaml per VM (no logic in code-behind)
    ├── System/                            # UI-side system clock/path impls if needed
    └── Resources/Strings.resx, Strings.es-DO.resx
```

### `tests/`

```
tests/
├── ControlDeFinanzasIA.Tests.Unit/
│   ├── Architecture/ArchitectureTests.cs  # NetArchTest R-1..R-5
│   ├── Domain/                           # Entity + VO tests
│   ├── Application/                       # Use case tests (hand-rolled fakes, no Moq)
│   ├── UI/                               # ViewModel tests
│   ├── Common/                           # Result, PagedResult tests
│   ├── ValueObjects/                      # Money, DateRange, ExchangeRate tests
│   ├── Infrastructure/ExchangeRates/      # StubExchangeRateProvider tests
│   └── TestDoubles/                       # FakeClock, FakePathResolver (+ tests)
└── ControlDeFinanzasIA.Tests.Integration/
    └── Persistence/                       # SQLite in-memory tests via TestAppDbContextFactory
                                           # (schema, repos, migrations, filters)
```

### `spec/`

```
spec/
├── README.md                              # Spec convention
├── constitution/
│   ├── mission.md                         # Product purpose (Spanish)
│   └── tech-stack.md                      # Stack + conventions
├── phases/
│   ├── phase-01/features.md               # Phase 1 plan
│   ├── phase-02/features.md               # Phase 2 plan
│   ├── phase-03/features.md               # Phase 3 plan
│   ├── phase-04/features.md               # Phase 4 plan
│   ├── phase-05/features.md               # Phase 5 plan
│   └── phase-06/features.md               # Phase 6 plan (status table inside)
└── features/
    ├── phase-NN/F-XXX-slug.spec.md        # Active spec
    ├── phase-NN/F-XXX-slug.done.md        # Archived spec (after archive)
    └── phase-06/F-001-recurring-rules.spec.md   # Current active feature
```

### `tasks/`

```
tasks/
└── phase-NN/F-XXX-slug.tasks.md           # Task breakdown per feature
```

(Every feature has a `.tasks.md` companion to its spec.)

### `docs/`

```
docs/
├── vision.md                              # Why (product vision, non-goals)
├── roadmap.md                             # When (phases 0–21)
├── architecture.md                        # How (layers, contracts, ADRs)
├── roadmap-notion-ready.md                # Compact roadmap view for Notion sync
└── AI_HANDOFF.md                          # ← this file
```

### `.claude/skills/`

- `.claude/skills/frontend-design/SKILL.md` — **invoke for any UI/visual change.** Design tokens centralized, MVVM strict, no generic AI aesthetics.

### `.atl/`

- `.atl/sdd/<change-id>/{proposal,spec,tasks,design,apply-progress,verify-report,archive-report}.md` — per-change working memory for the SDD cycle.
- `.atl/skill-registry.md` + `.atl/.skill-registry.cache.json` — index of installed skills.
- `.atl/sdd/f-006p6-formalize-recurrentes/` — the live Phase 6 SDD working dir.

### Other top-level files

- `Directory.Build.props` — common MSBuild props (`Nullable=enable`, `LangVersion=latest`, analyzers).
- `Directory.Packages.props` — **Central Package Management.** Versions live here only.
- `stylecop.json`, `.editorconfig` — style/analyzer config.
- `opencode.json` — agent + MCP config.

---

## 8. Conventions

### Commits

- Conventional Commits (`feat:`, `fix:`, `chore:`, `refactor:`, etc.).
- **No `Co-Authored-By: AI` trailers.** Ever.
- No interactive `-i`. No `--no-verify` on hooks unless explicitly requested.

### Code

- **MVVM strict.** `[ObservableProperty]`, `[RelayCommand]` from CommunityToolkit.Mvvm.
- **Errors as values** (`Result.Failure(code, message)`) for expected errors. Typed exceptions only for unexpected.
- **`IClock` for time.** No `DateTime.Now` / `DateTime.UtcNow` in use cases or services.
- **`IPathResolver` for filesystem paths.** No `Environment.GetFolderPath` outside `Infrastructure/System/`.
- **Async naming.** `*Async` suffix on every async method.
- **`CancellationToken`** parameter on every async I/O method.
- **File-scoped namespaces.** `using` statements at top of file, not nested.
- **One public type per file.** File name = type name.
- **PascalCase for public members, camelCase for locals/params, `_camelCase` for private fields.**

### Naming

| Element | Pattern | Example |
| --- | --- | --- |
| ViewModel | `XxxViewModel` | `ExpensesViewModel` |
| View | `XxxView` | `ExpensesView` |
| Service | `XxxService` | `BackupService` |
| Repository | `XxxRepository` | `ExpenseRepository` |
| Interface | `IXxx` | `IExpenseRepository` |
| Use case | `XxxUseCase` | `RegisterExpenseUseCase` |
| Query | `XxxQuery` / `XxxUseCase` | `GetTransactionsQuery` |
| Test | `XxxTests` | `ExpenseRepositoryTests` |

### Localization

- `IStringLocalizer` for UI strings; resx files (`Strings.resx`, `Strings.es-DO.resx`).
- `es-DO` default; numbers use `.` for thousands and `,` for decimals; dates `dd/MM/yyyy`.

### Specs

- Active: `spec/features/phase-NN/F-XXX-slug.spec.md`.
- Archived (after the SDD `archive` step): rename to `.done.md`.
- Task breakdowns always paired: `tasks/phase-NN/F-XXX-slug.tasks.md`.

---

## 9. Common commands

Run from repo root with `workdir="C:\Users\diego\Desktop\ControlDeFinanzasIA"`.

```powershell
# Build
dotnet build -c Release

# Run the app
dotnet run --project src/ControlDeFinanzasIA.UI

# Run all tests
dotnet test -c Release --no-build

# Format check (CI gate)
dotnet format style --verify-no-changes

# EF Core migrations (after installing dotnet-ef 8.0.11 locally)
dotnet ef migrations add <Name> `
  --project src/ControlDeFinanzasIA.Infrastructure `
  --startup-project src/ControlDeFinanzasIA.UI
```

---

## 10. How to resume work

### Where to start

1. **Read `spec/` first.** The active feature is Phase 7 (Debts) under `spec/features/phase-07/`. The phase plan is `spec/phases/phase-07/features.md`. Phase 6 is archived at `spec/features/phase-06/F-001-recurring-rules.done.md`.
2. **Check tasks:** `tasks/phase-07/` is the prioritized checklist for the active phase.
3. **Read constitution** if touching architecture or stack: `spec/constitution/mission.md`, `spec/constitution/tech-stack.md`.
4. **Check the SDD working dir** for any in-flight change: `.atl/sdd/<change-id>/`.

### SDD cycle (filesystem, not OpenSpec)

```
propose → spec → tasks → apply → verify → archive
```

Artifacts per cycle land under `.atl/sdd/<change-id>/{proposal,spec,tasks,design,apply-progress,verify-report,archive-report}.md`.

### Testing capabilities cache

- Skills installed locally: see `<available_skills>` in your system prompt (authoritative for the session). Also indexed at `.atl/skill-registry.md` and `.atl/.skill-registry.cache.json`.
- Project-local skills: `.claude/skills/` (currently `frontend-design`).
- MCP servers: Context7 (library docs — Avalonia, EF Core, xUnit, etc.) and Engram (persistent memory) — both configured at user level (`~/.config/opencode/opencode.json`).

### Engram topic keys

Memory is organized under `sdd/...`. Examples already present:

- `sdd/f-001p4-income-entity-config-migration/...`
- `sdd/f-007p3-expenses-viewmodel-view/...`

Project status snapshot lives under `project/status-snapshot`.

### When to invoke Context7

Per `spec/constitution/tech-stack.md`: invoke Context7 whenever introducing or upgrading a library or pattern. Even for well-known libs (Avalonia, EF Core, xUnit) — your training may not reflect recent changes.

---

## 11. Gotchas & non-obvious discoveries

1. **`.done.md` rename is part of the archive step**, not earlier. The spec is renamed only **after** the SDD archive phase completes. Do not rename prematurely.

2. **No `openspec/` directory.** The project uses filesystem-based specs in `spec/`. Don't introduce OpenSpec — it's explicitly excluded per `AGENTS.md`.

3. **Phase IDs are local to each phase.** `F-001` in `phase-03/` is the Expense entity; `F-001` in `phase-04/` is the Income entity. Folder disambiguates. This is intentional (see `spec/README.md`).

4. **Phase 5 spec files use `F-501`, `F-502`, `F-503`** while the phase plan uses granular `5-001` through `5-013`. The plan's sub-IDs were grouped into three archived specs. Treat the `.done.md` filenames as authoritative for what shipped.

5. **`Result` is non-generic**, not `Result<T>`. From `spec/features/phase-01/F-005-comuntypes-result-pagedresult.done.md`: `Result` has `IsSuccess`, `Value: object?`, `Error?`. The caller casts `Value` to the expected type after checking `IsSuccess`. Don't introduce `Result<T>` — the project deliberately uses the simpler form.

6. **NetArchTest R-4 whitelists `Bootstrap`.** The composition root is the one place where `UI` is allowed to reference `Infrastructure`. Any other type in `UI` that touches `Infrastructure` directly will fail R-4.

7. **CPM (Central Package Management) is mandatory.** Package versions live only in `Directory.Packages.props`. `.csproj` files reference packages by name with no `Version` attribute. Do not hardcode versions in `.csproj`.

8. **EF Core migrations are versioned and committed.** Destructive schema changes need an explicit migration and, where applicable, a backup. The `20260910205248_AddRecurringRules` migration is the latest committed.

9. **No remote telemetry. No secrets in repo.** User LLM API keys live in the local data file (cleartext in v1, encryption deferred to ADR-011 v2).

10. **Avalonia 11.2.7 is pinned** in CPM. Don't bump without Context7 check.

11. **DI registration is centralized in `Bootstrap.ConfigureServices`.** New ViewModels, use cases, repositories, or services are registered there — not scattered.

12. **Phase 6 is fully closed** — all gates ran clean on 2026-09-17 (build, 614/614 tests, format, NetArchTest) and the spec is archived. `dotnet format style` had caught two real bugs, not just noise: `Subscription`/`FixedExpense` public constructors used PascalCase parameter names (`Guid Id, string Name, ...`), which is both a style and a convention violation — always run the format gate for real, don't skip it as "just style."

13. **Tests use hand-rolled fakes, not Moq/NSubstitute.** `FakeClock`, `FakePathResolver`, `FakeExpenseRepository`, etc. live in `tests/.../TestDoubles/`. The project intentionally avoided a mocking library.

14. **`tests/` source-marker count is 564** (`[Fact]`+`[Theory]` declarations), expanding to ~559 runtime test cases (the difference is `[Theory]` rows that fan out into multiple cases). AGENTS.md's "559/559" matches the runtime count.

15. **MVVM in `RecurringRulesViewModel` injects 11 dependencies.** That wide constructor is a smell for "Form vs ListView" split when you formalize F-006. Use it as a hint that the UI needs a refactor in the formal apply phase.

---

## 12. First 5 commands a new agent should run

```powershell
# From C:\Users\diego\Desktop\ControlDeFinanzasIA

# 1. Confirm the build is clean before you do anything
dotnet build -c Release

# 2. Confirm the existing 559 tests still pass
dotnet test -c Release --no-build

# 3. Confirm CI's format gate is green
dotnet format style --verify-no-changes

# 4. Confirm the architecture rules still hold
dotnet test --no-build -c Release --filter "FullyQualifiedName~Architecture"

# 5. Inspect the current Phase 6 task list
Get-Content -Raw .\tasks\phase-06\F-001-recurring-rules.tasks.md
```

If all five succeed, the working tree is healthy. If any fail, **stop and fix before reading further code** — fixing a broken build first will save you time.
