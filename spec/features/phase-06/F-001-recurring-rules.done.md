# F-001 — Reglas recurrentes y generación explícita

> **Fase:** 6 — Recurrentes
> **Estado:** ✅ Archivada (2026-09-17)
> **SDD:** `.atl/sdd/f-006p6-formalize-recurrentes/`

## Objetivo

Permitir administrar suscripciones y gastos fijos recurrentes y generar sus
movimientos de forma explícita, predecible e idempotente.

## Requisitos

### R-1 — Regla recurrente

El sistema DEBE modelar una regla con nombre, monto positivo, moneda,
frecuencia, fechas, categoría, método de pago, tags opcionales, estado activo
y próxima fecha de generación.

### R-2 — Invariantes

El sistema DEBE rechazar nombres vacíos, montos no positivos, identificadores
vacíos, frecuencias desconocidas y rangos de fechas inválidos sin persistir.

### R-3 — Cálculo determinista

El calculador DEBE soportar frecuencias mensual, bimestral y anual; fin de mes,
años bisiestos, reglas inactivas y reglas expiradas. Para los días 29–31 en
meses más cortos se usará el último día válido del mes; esta política DEBE ser
estable y testeable.

### R-4 — Generación segura

La generación DEBE ofrecer preview antes de confirmar y NO DEBE duplicar un
movimiento para la misma regla y periodo.

### R-5 — Ciclo de vida

El usuario DEBE poder crear, editar y desactivar reglas, saltar una ocurrencia
y editar un movimiento generado sin mutar la regla original.

## Escenarios

### S-1 — Suscripción válida

- GIVEN una suscripción mensual válida
- WHEN se ejecuta la creación
- THEN se devuelve una regla activa con próxima fecha calculada

### S-2 — Invariante inválida

- GIVEN un monto no positivo o identificador vacío
- WHEN se ejecuta la creación
- THEN se devuelve `Validation`/`DomainInvariant` y no hay escritura

### S-3 — Mes corto

- GIVEN una regla fijada en el día 31
- WHEN se calcula febrero o un mes de 30 días
- THEN se aplica la política documentada de último día válido

### S-4 — Preview y confirmación

- GIVEN ocurrencias pendientes del periodo
- WHEN el usuario previsualiza y confirma
- THEN se crean una vez y aparecen como movimientos rastreables

### S-5 — Regla inactiva

- GIVEN una regla desactivada o expirada
- WHEN se genera el periodo
- THEN no se crea ningún movimiento

## Fuera de alcance

- Jobs en background.
- Provider real de tipos de cambio.
- Notificaciones nativas del sistema.

## Criterios de aceptación

- [x] Calculator cubre al menos 10 escenarios de fechas (16 en `RecurringOccurrenceCalculatorTests`).
- [x] CRUD de suscripciones y gastos fijos probado.
- [x] Generación preview/confirmación idempotente.
- [x] Integración y UI verificadas por los gates del repositorio (build, 614/614 tests, format, NetArchTest).
