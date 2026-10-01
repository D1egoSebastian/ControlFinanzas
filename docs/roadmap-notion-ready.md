# ControlDeFinanzasIA — Roadmap visual y backlog restante

> Documento preparado para copiarse a Notion cuando el conector esté
> habilitado. Estado basado en el repositorio al 2026-09-30.

## Tablero resumido

| Fase | Tema | Estado | Dependencias |
|---|---|---|---|
| 0 | Bootstrap | ✅ Completada | — |
| 1 | Arquitectura y contratos | ✅ Completada | 0 |
| 2 | Persistencia EF Core/SQLite | ✅ Completada | 1 |
| 3 | Slice de gastos | ✅ Completada | 2 |
| 4 | Ingresos, tags, filtros y vistas | ✅ Completada | 3 |
| 5 | Multi-moneda y métodos de pago | ✅ Completada | 4 |
| 6 | Recurrentes | ✅ Completada | 5 |
| 7 | Deudas y cuotas | ✅ Completada | 6 |
| 8 | Dashboard y visualizaciones | ✅ Completada | 7 |
| 9 | Calendario de gastos | ✅ Completada | 8 |
| 10 | Presupuestos | ✅ Completada | 8 |
| 11 | Metas de ahorro | ✅ Completada | 8 |
| 12 | Configuración e i18n completa | ✅ Completada | 8 |
| 13 | Pipeline IA y categorización | ✅ Completada | 12 |
| 14 | Insights, forecast y anomalías | ✅ Completada | 8, 10, 13 |
| 15 | Chat financiero | ✅ Completada | 14 |
| 16 | Plan de ahorro con IA | ✅ Completada | 11, 13 |
| 17 | Notificaciones | ✅ Completada | 7, 10, 14 |
| 18 | Exportación PDF/Excel | ✅ Completada | 4, 5, 8 |
| 19 | Backup/restauración | ✅ Completada | 2, 4, 5, 6, 7 |
| 20 | Importación CSV/Excel | ✅ Completada | 4, 19 |
| 21 | Empaquetado y accesibilidad | ✅ Completada y archivada (la etiqueta `v1.0.0`, las comprobaciones manuales y el primer release son del dueño) | Todas |

## Estado actual: Fase 21 implementada

- [x] Versión 1.0.0, icono, README, CHANGELOG y LICENSE.
- [x] Logger de archivo rotativo, overlay de errores inesperados y atajos de teclado.
- [x] Empty states y localización de Gastos, Ingresos, Deudas y Recurrentes; nombres accesibles y foco visible.
- [x] Publicación self-contained para win-x64, osx-arm64, osx-x64 y linux-x64, con `SHA256SUMS.txt` (Windows ejecutado;
  macOS y Linux solo estructura).
- [ ] `sdd-verify`, archivo de la spec y etiqueta `v1.0.0` (siguiente paso).
- [ ] Pruebas manuales no ejecutadas: Narrator, instalación en Windows limpio, macOS, Linux, ejecución real de
  `release.yml`, revisión visual es-DO / en.
- Diferido: MSIX, firma de código y notarización, DMG / AppImage verificados, Flatpak / Snap, auto-update.

## Riesgos y decisiones abiertas

- Confirmar con el dueño cuál formato de macOS conservar (`.tar.gz` o `.app.zip`).
- Primera ejecución real del flujo de release (crea solo un borrador) cuando se empuje la etiqueta `v1.0.0`.
- Mantener IA opcional y la aplicación funcional offline.

Ver `docs/roadmap.md` (fuente de verdad), `docs/packaging-unverified.md` y `docs/accessibility.md`.
