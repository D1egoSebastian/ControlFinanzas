# Fase 8 — Vistas por periodo y visualizaciones base

> **Fase:** 8 — Vistas por periodo y visualizaciones base
> **Estado:** ✅ Completada (2026-09-18) — ver `spec/features/phase-08/F-008-dashboard-visualizations.done.md`
> **Spec ID:** PHASE-08-PLAN
> **Dependencias:** Phase 4 ✅, Phase 5 ✅ (datos + multi-moneda); Phase 6 ✅, Phase 7 ✅
> (recurrentes y deudas, consumidos por "Próximos pagos" — adición deliberada sobre el
> roadmap original, ver proposal.md)
> **Roadmap:** `docs/roadmap.md` §Fase 8
> **Stack:** .NET 8 · Avalonia 11.2.7 (MVVM strict) · EF Core 8.0.11 + SQLite ·
> LiveChartsCore.SkiaSharpView.Avalonia 2.0.5 · xUnit + FluentAssertions 11.10.0

## 1. Resumen

Fase 8 convierte el Dashboard, hasta entonces un placeholder estático sin bindings, en la
pantalla de control del mes: selector de periodo mensual con navegación anterior/siguiente,
resumen normalizado a DOP (total de gastos, total de ingresos, balance neto, delta % vs. mes
anterior), gráfico de categorías (top 5 + "Otras"), gráfico de evolución temporal (6 meses,
zero-filled), y una tarjeta "Próximos pagos" que combina cuotas de deuda pendientes (Fase 7) y
próximas generaciones de recurrentes (Fase 6). Toda la agregación se ejecuta en SQL
(`GROUP BY`/`SUM`), nunca sumando en memoria sobre listas paginadas.

## 2. Modelo

```
Application/DTOs/Aggregates/
  CurrencyTotal, CategoryCurrencyTotal, MonthCurrencyTotal   — grano flat por dimensión × moneda

Application/Common/
  MonthPeriod           — Anchor/Range/Previous/Next/TrailingRange
  DashboardDefaults      — TopCategories=5, EvolutionMonths=6, UpcomingHorizonDays=30, UpcomingMaxItems=10

Application/UseCases/Dashboard/
  GetMonthSummaryQuery, GetCategoryBreakdownQuery, GetMonthlyEvolutionQuery, GetUpcomingPaymentsQuery
```

Sin capa de dominio nueva — esta fase es agregación de lectura sobre entidades ya existentes
(`Expense`, `Income`, `DebtPayment`, `RecurringRule`), no un nuevo agregado transaccional.

## 3. Plan de slices

| ID | Slice | Estimación | Estado |
| --- | --- | --- | --- |
| 1 | Capa de agregación (DTOs, `MonthPeriod`, métodos SQL `GROUP BY`/`SUM` en `IExpenseRepository`/`IIncomeRepository`) | M | ✅ Completada |
| 2 | Dashboard UI (LiveCharts2, `DashboardViewModel`/`DashboardView`, resumen + 2 gráficos) | L | ✅ Completada |
| 3 | Próximos pagos (`IDebtPaymentRepository` extendido, `GetUpcomingPaymentsQuery`, tarjeta UI) | M | ✅ Completada |

Aplicados en 3 commits separados (`b53102f`, `3fc5325`, `5aee8c9`) siguiendo la recomendación de
PRs encadenados de `sdd-tasks` — aislar el riesgo de renderizado cross-OS de LiveCharts2 (slice 2)
de la capa de agregación, ya probada y de bajo riesgo (slice 1).

## 4. Criterios de fase completada

- Abrir el Dashboard muestra el mes actual con resumen, gráficos y próximos pagos.
- Navegar al mes anterior/siguiente recalcula resumen y gráficos; "Próximos pagos" no cambia
  (ancla a la fecha actual, no al mes navegado).
- Las agregaciones se ejecutan como SQL, confirmado tanto en tests como en runtime real.
- Los estados vacíos degradan sin excepciones.

## 5. Riesgos materializados y resueltos

- **`DisplayAsync` vs `ConvertAsync`**: la propuesta original asumía reusar `DisplayAsync` para
  normalizar a DOP; el diseño encontró que ese método convierte siempre a la moneda "contraria"
  (rompería la normalización). Corregido en spec antes de escribir código.
- **`Sum(decimal)` no soportado por el proveedor SQLite de EF Core 8**: lanza
  `NotSupportedException` dentro de cualquier `GroupBy`. Workaround: castear a `double`, sumar en
  SQL, redondear de vuelta a 4 decimales tras materializar — con tests de precisión dedicados
  (suma de fracciones de centavo, reconciliación exacta entre agregados independientes).
- **Renderizado cross-OS de LiveCharts2**: verificado hasta compilar + resolver en runtime sin
  excepción; el renderizado visual pixel-a-pixel en Windows/macOS/Linux queda para confirmación
  manual (mismo límite que el smoke test de Fase 7 — sin herramienta de captura de ventana nativa
  en este entorno).
- **Bloqueo de Windows Application Control en tests unitarios de Release**: hallazgo de entorno,
  no de código — ver `.atl/sdd/f-008p8-dashboard-visualizations/verify-report.md`.

## 6. Referencias

- `docs/roadmap.md` §Fase 8 — plan original
- `spec/features/phase-08/F-008-dashboard-visualizations.done.md` — spec formal archivada
- `.atl/sdd/f-008p8-dashboard-visualizations/{exploration,proposal,design}.md` — trazabilidad
  completa de decisiones (elección de LiveCharts2, ventana de 6 meses, horizonte de 30 días)
- `spec/features/phase-07/F-001-debts-installment-plan.done.md` — precedente directo del patrón
  de agregado con factory + tests de consistencia numérica
