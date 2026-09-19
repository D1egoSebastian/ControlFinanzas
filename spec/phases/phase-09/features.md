# Fase 9 — Vista de calendario de gastos

> **Fase:** 9 — Vista de calendario de gastos
> **Estado:** ✅ Completada (2026-09-18) — ver `spec/features/phase-09/F-009-calendar-view.done.md`
> **Spec ID:** PHASE-09-PLAN
> **Dependencias:** Phase 8 ✅ (reutiliza `MonthPeriod`, el patrón de agregación SQL, y el patrón
> de conversión de moneda a DOP)
> **Roadmap:** `docs/roadmap.md` §Fase 9
> **Stack:** .NET 8 · Avalonia 11.2.7 (MVVM strict) · EF Core 8.0.11 + SQLite ·
> xUnit + FluentAssertions 11.10.0

## 1. Resumen

Fase 9 agrega una vista de calendario mensual: cada día del mes navegado muestra un badge con su
total de gastos normalizado a DOP, y clickear cualquier día del mes actual navega a la vista de
Gastos filtrada a exactamente ese día (drill-down). Días de meses adyacentes (relleno de la
grilla de 42 celdas) navegan el calendario a su propio mes en vez de hacer drill-down. Esta fase
también es la primera en modificar `INavigationService`, agregando un overload de navegación
parametrizado (aditivo, sin regresión en las 5 rutas existentes) — necesario porque el drill-down
requiere preconfigurar el filtro de la vista de destino antes de que se vuelva activa.

## 2. Modelo

```
Application/DTOs/Aggregates/
  DayCurrencyTotal              — grano flat día × moneda

Application/Common/
  CalendarGrid                  — clase estática pura: 42 fechas (6 semanas × 7 días) para
                                   cualquier mes ancla y primer día de semana

Application/UseCases/Calendar/
  GetCalendarMonthQuery         — une CalendarGrid.Dates con GetDailyTotalsAsync, normaliza a DOP

UI/Navigation/
  INavigationService.NavigateTo<TViewModel>(routeName, configure)  — overload aditivo
```

## 3. Plan de slices

| ID | Slice | Estimación | Estado |
| --- | --- | --- | --- |
| 1 | Capa de datos (`GetDailyTotalsAsync`, `CalendarGrid`, `GetCalendarMonthQuery`) | M | ✅ Completada |
| 2 | Navegación parametrizada (`INavigationService` extendido, `ExpensesViewModel.ApplyDayFilter` + generation guard) | M | ✅ Completada |
| 3 | UI del calendario (`CalendarViewModel`/`CalendarView`/`MonthGrid`) | M | ✅ Completada |

Aplicados en 3 commits separados (`9eab856`, `6ffe1b2`, `313133b`) siguiendo la recomendación de
`sdd-tasks` — aislar el cambio de mayor riesgo cualitativo (modificar una interfaz compartida por
las 5 rutas existentes) de la capa de datos (aditiva, bajo riesgo) y de la UI que consume ambas.

## 4. Criterios de fase completada

- Calendario funcional con drill-down a gastos del día. ✅
- Navegación mes a mes recalcula la grilla completa. ✅
- Días adyacentes distinguidos visualmente, navegan el calendario sin hacer drill-down. ✅
- Las 5 rutas de navegación preexistentes no sufren regresión. ✅

## 5. Hallazgos fuera de alcance, corregidos de todas formas

Durante el desarrollo de esta fase se encontraron y corrigieron dos bugs reales de UI que nunca
habían sido ejecutados en runtime (nadie había abierto la app para verificar visualmente hasta
ahora):

- **`ViewLocator` sin el caso `DebtsViewModel`** (commit `e0e51d3`, independiente de esta fase):
  la ruta Deudas, wireada desde Fase 7, renderizaba en blanco desde que se mergeó.
- **`LedgerStyles.axaml`/`Motion.axaml` nunca fusionados en `App.axaml`** (commit `4d981df`):
  todo el sistema de diseño "Libro Mayor" (clases `ledger-*`) construido desde Fase 4 no tenía
  ningún efecto en runtime. Al fusionarlo apareció un segundo bug real: `RadiusSmall`/
  `RadiusMedium` declarados como `sys:Double` pero usados como `CornerRadius`, lo que lanza
  `InvalidCastException` al arrancar la app — solo detectable ejecutándola, no compilándola.
  `Motion.axaml` quedó deliberadamente sin fusionar (tiene su propio bug de animación real,
  `RenderTransform` sin animator registrado) — sigue como trabajo pendiente.

Ambos se encontraron porque, por primera vez en el proyecto, se lanzó la app de verdad
(`dotnet run` + captura de pantalla) en vez de confiar solo en que los tests pasaran.

## 6. Referencias

- `docs/roadmap.md` §Fase 9 — plan original
- `spec/features/phase-09/F-009-calendar-view.done.md` — spec formal archivada
- `.atl/sdd/f-009p9-calendar-view/{exploration,proposal,design}.md` — trazabilidad completa
- `spec/features/phase-08/F-008-dashboard-visualizations.done.md` — precedente del patrón de
  agregación SQL y normalización a DOP que esta fase extiende
