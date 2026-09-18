# Fase 7 — Deudas con plan de cuotas

> **Fase:** 7 — Deudas
> **Estado:** ✅ Completada (2026-09-18) — ver `spec/features/phase-07/F-001-debts-installment-plan.done.md`
> **Spec ID:** PHASE-07-PLAN
> **Dependencias:** Phase 6 ✅ (comparte el patrón de generación y reutiliza `Money`, `Currency`, `PaymentMethodId`)
> **Roadmap:** `docs/roadmap.md` §Fase 7 (líneas ~383–424)
> **Stack:** .NET 8 · Avalonia 11.2.7 (MVVM strict) · EF Core 8.0.11 + SQLite · xUnit + FluentAssertions 11.10.0

## 1. Resumen

Fase 7 modela deudas con un plan de cuotas: se crea la deuda una vez (monto,
número de cuotas, frecuencia, fecha de inicio) y el sistema genera el
calendario completo de pagos esperados de inmediato (a diferencia de Fase 6,
donde la generación es explícita mes a mes). El usuario registra cada pago
contra su cuota, puede saltar una cuota puntual, ve el saldo pendiente en
todo momento, y la deuda se cierra automáticamente al pagar la última cuota.

Conceptualmente es hermana de Fase 6 (ambas son "generación periódica de
movimientos"), pero con reglas propias: la cantidad de ocurrencias es finita
y conocida desde el inicio (no hay "próxima regeneración" indefinida), y el
pago es un evento explícito del usuario (no un movimiento auto-generado
silencioso).

## 2. Modelo de dominio

```
Domain/Debts/
  Debt                              — entidad raíz: Id, Description, OriginalAmount (Money),
                                       Currency, TotalInstallments, InstallmentAmount (Money),
                                       Frequency (enum), StartDate, CounterpartyName?, Note?,
                                       Status (Active/Closed)
  DebtPayment                       — Id, DebtId, InstallmentNumber, ScheduledDate,
                                       PaidDate?, Amount (Money), Status (Pending/Paid/Skipped)
  DebtScheduleGenerator             — servicio de dominio puro: dado un Debt no guardado,
                                       devuelve la lista completa de DebtPayment esperadas
```

Reutiliza el patrón de Fase 6: factory `Debt.Create(...)` valida invariantes
(monto positivo, cuotas > 0, fecha de inicio válida) y no expone setters
públicos. `DebtScheduleGenerator` es puro (sin I/O), igual que
`RecurringOccurrenceCalculator` — misma razón: testeable sin DB, sin `IClock`
para el cálculo del calendario (solo para "hoy" al evaluar mora, si aplica).

## 3. Casos de uso

```
Application/UseCases/Debts/
  CreateDebtWithScheduleUseCase     — crea el Debt y persiste todas las DebtPayment de una vez
  RegisterDebtPaymentUseCase        — marca una cuota como pagada, recalcula saldo
  SkipDebtPaymentUseCase            — marca una cuota como saltada (no cierra la deuda por eso)
  GetDebtsQuery                     — lista de deudas (activas/cerradas)
  GetDebtScheduleQuery              — calendario de cuotas de una deuda
  CloseDebtUseCase                  — (posible use case explícito, o efecto secundario de
                                       RegisterDebtPaymentUseCase al pagar la última cuota —
                                       decisión a fijar en design.md)
```

## 4. Plan de slices

| ID | Slice | Estimación | Estado |
| --- | --- | --- | --- |
| 7-001 | `Debt` + `DebtPayment` entidades, invariantes, tests | M | ✅ Completada |
| 7-002 | `DebtScheduleGenerator` (servicio de dominio puro) + tests (≥10 escenarios: mensual, quincenal, con/sin saltos, 1 cuota, N cuotas) | M | ✅ Completada |
| 7-003 | `IDebtRepository` + `IDebtPaymentRepository` + EF Core config + migración | M | ✅ Completada |
| 7-004 | `CreateDebtWithScheduleUseCase` + tests | M | ✅ Completada |
| 7-005 | `RegisterDebtPaymentUseCase` (pago total; parcial fuera de alcance v1) + `SkipDebtPaymentUseCase` + tests | M | ✅ Completada |
| 7-006 | Cierre automático de la deuda al pagar la última cuota + test de consistencia de saldo | S | ✅ Completada |
| 7-007 | UI: lista de deudas + detalle con calendario de cuotas + registro de pago | L | ✅ Completada |
| 7-008 | Integración con "Generar del mes" de Fase 6: las `DebtPayment` del mes visibles junto a recurrentes (decisión de diseño: ¿pantalla compartida o separada?) | M | ✅ Completada — decisión: pantalla `DebtsView` separada (ver §6) |

## 5. Criterios de fase completada

- Crear una deuda a 12 cuotas mensuales → ver el calendario completo de
  inmediato (sin necesidad de "generar" mes a mes).
- Registrar 3 pagos → ver el saldo pendiente actualizado correctamente.
- Saltar una cuota → el saldo la sigue contando como pendiente, el plan no
  se re-numera.
- Pagar la última cuota → la deuda pasa a `Closed` automáticamente.
- El saldo pendiente nunca es inconsistente con los pagos registrados
  (invariante probada: `saldo = OriginalAmount - Σ(pagos con Status=Paid)`).

## 6. Riesgos principales (heredados de `docs/roadmap.md` §Fase 7)

- **Pago parcial vs total:** v1 solo soporta pago total de una cuota
  (simplificación deliberada; documentar como fuera de alcance si se
  confirma en design.md).
- **Saltar cuotas manteniendo coherencia del plan:** una cuota saltada no
  debe desnumerar ni desfasar las siguientes.
- **Interacción con Fase 6:** ¿la pantalla "Generar del mes" de recurrentes
  incluye las `DebtPayment` del periodo, o es una pantalla separada?
  Decisión pendiente — se fija en `design.md` antes de `apply`.

## 7. Fuera de alcance (v1, heredado de `docs/vision.md`)

- Pagos parciales de una cuota.
- Renegociación de plan (cambiar número de cuotas o monto a mitad de camino).
- Intereses/mora calculados automáticamente.

## 8. Referencias

- `docs/roadmap.md` §Fase 7 (líneas ~383–424) — plan original
- `docs/architecture.md` §6 — modelo de dominio de `Debt`/`DebtPayment`
- `spec/features/phase-06/F-001-recurring-rules.done.md` — precedente directo:
  mismo patrón de servicio de dominio puro + generación + tests de idempotencia
- `.atl/sdd/f-007p7-debts-installment-plan/` — working dir del ciclo SDD activo
