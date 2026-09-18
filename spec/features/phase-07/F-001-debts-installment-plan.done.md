# F-001 — Deudas con plan de cuotas

> **Fase:** 7 — Deudas
> **Estado:** ✅ Archivada (2026-09-18)
> **SDD:** `.atl/sdd/f-007p7-debts-installment-plan/`

## Objetivo

Permitir crear una deuda con un plan de cuotas completo generado al momento
de crearla, registrar pagos contra cada cuota, saltar cuotas puntuales, ver
el saldo pendiente en todo momento, y cerrar la deuda automáticamente al
pagar la última cuota.

## Requisitos

### R-1 — Deuda

El sistema DEBE modelar una deuda con descripción, monto original positivo,
moneda, número total de cuotas (entero positivo), monto por cuota, frecuencia,
fecha de inicio, contraparte opcional, nota opcional y estado (Active/Closed).

### R-2 — Invariantes

El sistema DEBE rechazar descripciones vacías, montos no positivos, número de
cuotas ≤ 0, frecuencias desconocidas y fechas de inicio inválidas sin
persistir.

### R-3 — Generación de calendario al crear

Al crear una deuda, el sistema DEBE generar y persistir el calendario
completo de `DebtPayment` (una por cuota) en la misma operación — a
diferencia de Fase 6, NO hay generación explícita mes a mes.

### R-4 — Registro de pago

El usuario DEBE poder marcar una `DebtPayment` como pagada (pago total de la
cuota; pago parcial fuera de alcance v1). El sistema DEBE recalcular el saldo
pendiente tras cada pago.

### R-5 — Salto de cuota

El usuario DEBE poder marcar una `DebtPayment` como saltada sin afectar la
numeración ni las fechas de las cuotas siguientes.

### R-6 — Cierre automático

Al pagar la última cuota pendiente (todas las `DebtPayment` en estado
Paid o Skipped, con al menos la última en Paid), el sistema DEBE marcar la
`Debt` como `Closed` automáticamente.

### R-7 — Consistencia del saldo

El saldo pendiente DEBE ser siempre igual a
`OriginalAmount - Σ(DebtPayment.Amount donde Status = Paid)`, verificable en
todo momento sin recalcular desde cero fuera del dominio.

## Escenarios

### S-1 — Deuda válida a 12 cuotas

- GIVEN una deuda válida con 12 cuotas mensuales de RD$1,000 a partir del 15/01
- WHEN se ejecuta la creación
- THEN se devuelve una deuda activa con 12 `DebtPayment` persistidas,
  fechas mensuales desde el 15/01, todas en estado Pending

### S-2 — Invariante inválida

- GIVEN un monto no positivo, cuotas ≤ 0, o descripción vacía
- WHEN se ejecuta la creación
- THEN se devuelve `Validation`/`DomainInvariant` y no hay escritura
  (ni de la deuda ni de las cuotas)

### S-3 — Registro de pago actualiza saldo

- GIVEN una deuda con 3 cuotas pendientes de RD$500 cada una
- WHEN se registra el pago de la primera cuota
- THEN la cuota pasa a Paid, el saldo pendiente pasa de RD$1,500 a RD$1,000

### S-4 — Saltar cuota no rompe el plan

- GIVEN una deuda con cuotas 1..5 pendientes
- WHEN se salta la cuota 2
- THEN la cuota 2 queda en Skipped, las cuotas 3–5 conservan su
  `InstallmentNumber` y `ScheduledDate` originales (no se re-numeran)

### S-5 — Pagar la última cuota cierra la deuda

- GIVEN una deuda con todas las cuotas Paid excepto la última (Pending)
- WHEN se registra el pago de la última cuota
- THEN la `Debt.Status` pasa a `Closed` automáticamente en la misma operación

### S-6 — Calendario cubre frecuencias mensual y quincenal

- GIVEN una deuda quincenal a 6 cuotas
- WHEN se genera el calendario
- THEN las 6 `ScheduledDate` están espaciadas 15 días exactos entre sí,
  partiendo de `StartDate`

## Fuera de alcance

- Pagos parciales de una cuota.
- Renegociación de plan (cambiar cuotas/monto a mitad de camino).
- Intereses o mora calculados automáticamente.
- Integración visual con la pantalla "Generar del mes" de Fase 6 — se decide
  en `design.md` si comparten pantalla o quedan separadas; no bloquea esta spec.

## Criterios de aceptación

- [x] `DebtScheduleGenerator` cubre al menos 10 escenarios (15 en
  `DebtScheduleGeneratorTests`: mensual, quincenal, 1 cuota, 36 cuotas, fin de
  mes en año bisiesto y no bisiesto).
- [x] Crear/pagar/saltar cuotas probado con tests que verifiquen R-7
  (consistencia de saldo) en cada transición de estado
  (`RegisterDebtPaymentUseCaseTests.ExecuteAsync_ThreeSequentialPayments_BalanceDecreasesConsistentlyAtEachStep`).
- [x] Cierre automático de deuda probado (S-5:
  `RegisterDebtPaymentUseCaseTests.ExecuteAsync_LastPendingPayment_ClosesDebtAutomatically`).
- [x] Integración y UI verificadas por los gates del repositorio (build limpio,
  698/698 tests, `dotnet format style` limpio, NetArchTest 11/11).
