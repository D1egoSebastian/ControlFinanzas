# ControlDeFinanzasIA — Roadmap visual y backlog restante

> Documento preparado para copiarse a Notion cuando el conector esté
> habilitado. Estado basado en el repositorio al 2026-09-10.

## Tablero resumido

| Fase | Tema | Estado | Dependencias |
|---|---|---|---|
| 0 | Bootstrap | ✅ Completada | — |
| 1 | Arquitectura y contratos | ✅ Completada | 0 |
| 2 | Persistencia EF Core/SQLite | ✅ Completada | 1 |
| 3 | Slice de gastos | ✅ Completada | 2 |
| 4 | Ingresos, tags, filtros y vistas | ✅ Completada | 3 |
| 5 | Multi-moneda y métodos de pago | ✅ Completada | 4 |
| 6 | Recurrentes | 🔄 En curso | 5 |
| 7 | Deudas y cuotas | ⏳ Pendiente | 6 |
| 8 | Dashboard y visualizaciones | ⏳ Pendiente | 7 |
| 9 | Calendario de gastos | ⏳ Pendiente | 8 |
| 10 | Presupuestos | ⏳ Pendiente | 8 |
| 11 | Metas de ahorro | ⏳ Pendiente | 8 |
| 12 | Configuración e i18n completa | ⏳ Pendiente | 8 |
| 13 | Pipeline IA y categorización | ⏳ Pendiente | 12 |
| 14 | Insights, forecast y anomalías | ⏳ Pendiente | 8, 10, 13 |
| 15 | Chat financiero | ⏳ Pendiente | 14 |
| 16 | Plan de ahorro con IA | ⏳ Pendiente | 11, 13 |
| 17 | Notificaciones | ⏳ Pendiente | 7, 10, 14 |
| 18 | Exportación PDF/Excel | ⏳ Pendiente | 4, 5, 8 |
| 19 | Backup/restauración | ⏳ Pendiente | 2, 4, 5, 6, 7 |
| 20 | Importación CSV/Excel | ⏳ Pendiente | 4, 19 |
| 21 | Empaquetado y accesibilidad | ⏳ Pendiente | Todas |

## Estado actual: Phase 6

- [x] Persistencia EF Core, migración y repositorio de `RecurringRule`.
- [x] Create/update de `Subscription` en backend.
- [ ] CRUD de `FixedExpense`.
- [ ] Preview y confirmación de generación mensual.
- [ ] Idempotencia de generación y trazabilidad de movimientos creados.
- [ ] Saltar ocurrencias y editar movimientos generados.
- [ ] Completar UI de reglas para FixedExpense y pantalla “Generar del mes”.
- [ ] Tests de fechas, integración, ViewModels y gates SDD.

## Backlog funcional por fases futuras

- Phase 7: `Debt`, `DebtPayment`, generador de cuotas, pagos, saltos, saldo y UI.
- Phase 8: selector de periodos, agregaciones, dashboard y gráficos reutilizables.
- Phase 9: calendario mensual, totales diarios y drill-down.
- Phase 10: presupuestos, evaluador, progreso y umbrales.
- Phase 11: metas, aportes manuales y calculador determinista.
- Phase 12: settings, tema, idioma, configuración IA y persistencia.
- Phase 13: `ILLMClient`, adapters, resiliencia y categorización.
- Phase 14: insights, proyección mensual y narrativas de anomalías.
- Phase 15: chat con contexto financiero local.
- Phase 16: explicación IA del plan de ahorro determinista.
- Phase 17: bandeja y reglas de notificación in-app.
- Phase 18: exportadores PDF y Excel/CSV.
- Phase 19: backup ZIP + manifest y restauración segura.
- Phase 20: wizard CSV/Excel, mapeo, preview, deduplicación y rollback.
- Phase 21: builds self-contained, instaladores, accesibilidad y release.

## Riesgos y decisiones abiertas

- Elegir estrategia de PR para Phase 6: se recomiendan 4 unidades encadenadas.
- Definir la política exacta para fechas 29–31 y meses cortos.
- Decidir provider real de tipos de cambio.
- Resolver umbral de presupuesto y deduplicación de importaciones.
- Mantener IA opcional y la aplicación funcional offline.
