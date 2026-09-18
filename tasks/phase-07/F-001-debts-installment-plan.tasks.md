# Tasks — F-001 Deudas con plan de cuotas

## Revisión de entrega

Estimated changed lines: 600–900 (similar order of magnitude to F-006/F-001)
Decision needed before apply: Yes — pago parcial vs total (spec asume total-only,
confirmar antes de codear) y si "Generar del mes" de Fase 6 muestra las
`DebtPayment` del periodo o si Deudas tiene su propia pantalla.
Chained PRs recommended: Yes (foundation → use cases → UI, igual que F-006)
400-line budget risk: Medium-High

## Foundation

- [ ] 1.1 Crear `Debt` y `DebtPayment` en `Domain/Debts/` con factory
  `Debt.Create(...)` validando invariantes (R-1, R-2). Setters privados,
  constructor vacío para EF Core (mismo patrón que `RecurringRule`).
- [ ] 1.2 Crear `DebtScheduleGenerator` (servicio de dominio puro, sin I/O)
  que dado un `Debt` no guardado devuelve `IReadOnlyList<DebtPayment>`.
- [ ] 1.3 Tests del generador: ≥10 escenarios (mensual, quincenal, 1 cuota,
  N cuotas, fin de mes si la frecuencia lo permite).
- [ ] 1.4 Tests de invariantes de `Debt.Create` (monto ≤0, cuotas ≤0,
  descripción vacía, fecha inválida).

## Persistencia

- [ ] 2.1 `IDebtRepository` + `IDebtPaymentRepository` en
  `Application/Abstractions/` (mismo patrón que `IRecurringRuleRepository`:
  Add/Update/Remove/GetById/GetAll + queries específicas).
- [ ] 2.2 `DebtConfiguration` + `DebtPaymentConfiguration` (EF Core) + migración.
- [ ] 2.3 Implementaciones `DebtRepository`/`DebtPaymentRepository` + registro en DI.
- [ ] 2.4 Tests de integración: round-trip de `Debt` + sus `DebtPayment`,
  schema (columnas, FKs, índices) — mismo patrón que `RecurringRuleRepositoryTests`
  y `ExpenseSchemaTests`.

## Use cases

- [ ] 3.1 `CreateDebtWithScheduleUseCase` — crea `Debt` + genera y persiste
  todas las `DebtPayment` en una sola operación transaccional (R-3, S-1, S-2).
- [ ] 3.2 `RegisterDebtPaymentUseCase` — marca cuota Paid, recalcula saldo,
  cierra la deuda si era la última cuota pendiente (R-4, R-6, R-7, S-3, S-5).
- [ ] 3.3 `SkipDebtPaymentUseCase` — marca cuota Skipped sin re-numerar (R-5, S-4).
- [ ] 3.4 `GetDebtsQuery` (lista, filtrable por Active/Closed) +
  `GetDebtScheduleQuery` (calendario de una deuda).
- [ ] 3.5 Tests de cada use case, incluyendo el escenario de cierre
  automático y la invariante de saldo (R-7) en cada transición.

## UI

- [ ] 4.1 `DebtsViewModel` + `DebtsView`: lista de deudas (activas/cerradas),
  formulario de creación (descripción, monto, cuotas, frecuencia, fecha inicio).
- [ ] 4.2 Detalle de deuda: calendario de cuotas con acción "Registrar pago"
  y "Saltar cuota" por fila; saldo pendiente visible.
- [ ] 4.3 Registrar ruta `RouteNames.Debts` + entrada en `NavigationService`
  + registro en DI (`Bootstrap.cs`) + `ViewLocator` ya cubre el mapeo VM→View.
- [ ] 4.4 Localización, estados vacíos, errores como valores (mismo nivel
  que Fase 6 — strings hardcoded en español consistente con el resto de la UI).

## Cierre

- [ ] 5.1 Ejecutar build, tests, `dotnet format style`, NetArchTest, smoke test.
- [ ] 5.2 Actualizar `spec/phases/phase-07/features.md` (marcar slices completados).
- [ ] 5.3 Archivar spec (`F-001-debts-installment-plan.spec.md` → `.done.md`),
  escribir `verify-report.md` + `archive-report.md` en
  `.atl/sdd/f-007p7-debts-installment-plan/`, actualizar `docs/roadmap.md` §Fase 7.
