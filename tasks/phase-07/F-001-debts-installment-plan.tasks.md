# Tasks — F-001 Deudas con plan de cuotas

## Revisión de entrega

Estimated changed lines: 600–900 (similar order of magnitude to F-006/F-001) —
actual: 46 files, ~4,468 insertions (single apply pass, not chained; accepted
given the whole feature landed green in one commit).
Decisions (resolved): pago total-only (confirmed, as spec assumed). Deudas
tiene su propia pantalla (`DebtsView`), separada de "Generar del mes" —
modelos de interacción distintos (seguimiento de pago vs. preview de
generación).
Applied: 2026-09-18. Verified independently (not just the applying agent's
self-report) before archive — see `.atl/sdd/f-007p7-debts-installment-plan/verify-report.md`.

## Foundation

- [x] 1.1 Crear `Debt` y `DebtPayment` en `Domain/Debts/` con factory
  `Debt.Create(...)` validando invariantes (R-1, R-2). Setters privados,
  constructor vacio para EF Core (mismo patron que `RecurringRule`).
  (`src/ControlDeFinanzasIA.Domain/Debts/Debt.cs`, `DebtPayment.cs`;
  `DebtTests.Create_ValidDebt_ReturnsActiveDebtWithProvidedValues`.)
- [x] 1.2 Crear `DebtScheduleGenerator` (servicio de dominio puro, sin I/O)
  que dado un `Debt` no guardado devuelve `IReadOnlyList<DebtPayment>`.
  (`src/ControlDeFinanzasIA.Domain/Debts/DebtScheduleGenerator.cs`.)
- [x] 1.3 Tests del generador: ≥10 escenarios (mensual, quincenal, 1 cuota,
  N cuotas, fin de mes si la frecuencia lo permite).
  (`tests/ControlDeFinanzasIA.Tests.Unit/Domain/Debts/DebtScheduleGeneratorTests.cs`
  — 15 casos: mensual, quincenal, 1 cuota, 36 cuotas, fin de mes no bisiesto,
  31 en año bisiesto, 31 en año no bisiesto, gaps quincenales exactos de 15 días.)
- [x] 1.4 Tests de invariantes de `Debt.Create` (monto ≤0, cuotas ≤0,
  descripción vacía, fecha inválida).
  (`tests/ControlDeFinanzasIA.Tests.Unit/Domain/Debts/DebtTests.cs` — 13 casos
  de invariantes + `DebtPaymentTests.cs` para transiciones Pending→Paid/Skipped.)

## Persistencia

- [x] 2.1 `IDebtRepository` + `IDebtPaymentRepository` en
  `Application/Abstractions/` (mismo patrón que `IRecurringRuleRepository`:
  Add/Update/Remove/GetById/GetAll + queries específicas).
  (`IDebtRepository.cs` añade `AddWithScheduleAsync` para la escritura
  transaccional de R-3; `IDebtPaymentRepository.cs` añade `GetByDebtIdAsync`.)
- [x] 2.2 `DebtConfiguration` + `DebtPaymentConfiguration` (EF Core) + migración.
  (`Infrastructure/Persistence/Configurations/DebtConfiguration.cs`,
  `DebtPaymentConfiguration.cs`; migración `20260918025303_AddDebts`.)
- [x] 2.3 Implementaciones `DebtRepository`/`DebtPaymentRepository` + registro en DI.
  (`Infrastructure/Persistence/Repositories/DebtRepository.cs`,
  `DebtPaymentRepository.cs`; registrados en `ServiceCollectionExtensions.AddPersistence`.)
- [x] 2.4 Tests de integración: round-trip de `Debt` + sus `DebtPayment`,
  schema (columnas, FKs, índices) — mismo patrón que `RecurringRuleRepositoryTests`
  y `ExpenseSchemaTests`.
  (`tests/ControlDeFinanzasIA.Tests.Integration/Persistence/DebtRepositoryTests.cs`,
  `DebtSchemaTests.cs` — incluye FK cascade DebtPayments→Debts y el índice
  único `(DebtId, InstallmentNumber)`.)

## Use cases

- [x] 3.1 `CreateDebtWithScheduleUseCase` — crea `Debt` + genera y persiste
  todas las `DebtPayment` en una sola operación transaccional (R-3, S-1, S-2).
  (`Application/UseCases/Debts/CreateDebtWithSchedule/`; un único
  `SaveChangesAsync` vía `IDebtRepository.AddWithScheduleAsync`.)
- [x] 3.2 `RegisterDebtPaymentUseCase` — marca cuota Paid, recalcula saldo,
  cierra la deuda si era la última cuota pendiente (R-4, R-6, R-7, S-3, S-5).
  (`Application/UseCases/Debts/RegisterDebtPayment/RegisterDebtPaymentUseCase.cs`;
  `RegisterDebtPaymentUseCaseTests.ExecuteAsync_LastPendingPayment_ClosesDebtAutomatically`
  prueba S-5.)
- [x] 3.3 `SkipDebtPaymentUseCase` — marca cuota Skipped sin re-numerar (R-5, S-4).
  (`SkipDebtPaymentUseCaseTests.ExecuteAsync_SkippingMiddleInstallment_DoesNotRenumberOrShiftFollowingInstallments`
  prueba S-4.)
- [x] 3.4 `GetDebtsQuery` (lista, filtrable por Active/Closed) +
  `GetDebtScheduleQuery` (calendario de una deuda).
  (`Application/UseCases/Debts/GetDebts/`, `GetDebtSchedule/`.)
- [x] 3.5 Tests de cada use case, incluyendo el escenario de cierre
  automático y la invariante de saldo (R-7) en cada transición.
  (`RegisterDebtPaymentUseCaseTests.ExecuteAsync_ThreeSequentialPayments_BalanceDecreasesConsistentlyAtEachStep`
  prueba R-7 en cada paso; `DebtTests` prueba R-7/R-6 a nivel de dominio puro.)

## UI

- [x] 4.1 `DebtsViewModel` + `DebtsView`: lista de deudas (activas/cerradas),
  formulario de creación (descripción, monto, cuotas, frecuencia, fecha inicio).
  (`UI/ViewModels/DebtsViewModel.cs`, `UI/Views/DebtsView.axaml`.)
- [x] 4.2 Detalle de deuda: calendario de cuotas con acción "Registrar pago"
  y "Saltar cuota" por fila; saldo pendiente visible.
  (Panel derecho de `DebtsView.axaml`: `RegisterPaymentCommand`/`SkipPaymentCommand`
  por fila, `PendingBalance` bindeado desde `Debt.CalculatePendingBalance`.)
- [x] 4.3 Registrar ruta `RouteNames.Debts` + entrada en `NavigationService`
  + registro en DI (`Bootstrap.cs`) + `ViewLocator` ya cubre el mapeo VM→View.
  (`RouteNames.Debts`, `NavigationService` route map, `ShellViewModel` sidebar
  item "Deudas", `Bootstrap.ConfigureServices` registra
  `DebtsViewModel` y los 5 use cases/queries de Debts.)
- [x] 4.4 Localización, estados vacíos, errores como valores (mismo nivel
  que Fase 6 — strings hardcoded en español consistente con el resto de la UI).
  (Estados vacíos "Todavía no hay deudas registradas." / "Selecciona una deuda
  para ver su calendario."; `Result.Failure` en todos los use cases; strings en
  español hardcoded, igual patrón que `RecurringRulesView`/`ExpensesView`.)

## Cierre

- [x] 5.1 Ejecutar build, tests, `dotnet format style`, NetArchTest, smoke test.
  (`dotnet build -c Release` 0 warnings/0 errors; `dotnet test -c Release`
  698/698 verdes (568 unit + 130 integration); `dotnet format style
  --verify-no-changes` limpio; NetArchTest 11/11. Smoke test manual de la UI
  no ejecutado — Avalonia no tiene runner headless en este entorno; cubierto
  por `DebtsViewModelTests` en su lugar.)
- [x] 5.2 Actualizar `spec/phases/phase-07/features.md` (marcar slices completados).
- [x] 5.3 Archivar spec (`F-001-debts-installment-plan.spec.md` → `.done.md`),
  escribir `verify-report.md` + `archive-report.md` en
  `.atl/sdd/f-007p7-debts-installment-plan/`, actualizar `docs/roadmap.md` §Fase 7.
  (Gates re-verificados de forma independiente antes de archivar — no se confió
  solo en el reporte del agente que hizo el apply. Ver `verify-report.md`.)
