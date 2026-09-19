# F-009 — Vista de calendario de gastos

> **Fase:** 9 — Vista de calendario de gastos
> **Estado:** ✅ Archivada (2026-09-18)
> **SDD:** `.atl/sdd/f-009p9-calendar-view/`

## Objetivo

Agregar una vista de calendario mensual donde cada día muestra un badge con
su total de gastos normalizado a DOP, con navegación mes a mes y
drill-down: clickear un día abre la vista de Gastos filtrada a exactamente
ese día. La agregación diaria se ejecuta en SQL, reutilizando el patrón
establecido en Fase 8, y la navegación hacia Gastos con un filtro
preconfigurado requiere extender `INavigationService` de forma aditiva.

## Requisitos

### R-1 — Vista mensual con navegación

El sistema DEBE mostrar una grilla de calendario mensual, reutilizando el
periodo de `MonthPeriod.Range(anchor)`, y DEBE permitir navegar al mes
anterior y al mes siguiente, recalculando toda la grilla para el mes
seleccionado.

### R-2 — Badge diario normalizado a DOP

Para cada día visible perteneciente al mes navegado, el sistema DEBE
mostrar un badge numérico con el total de gastos de ese día, convertido y
normalizado a DOP mediante `ICurrencyConverter.ConvertAsync`, usando el
patrón de presentación `≈ {N2}` establecido en Fase 8. Los días sin gastos
DEBEN mostrar un total de cero, no un badge ausente.

### R-3 — Días de meses adyacentes: distinguidos y no navegables por drill-down

La grilla DEBE completar siempre 6 semanas × 7 días (42 celdas), incluyendo
días finales del mes anterior y/o iniciales del mes siguiente cuando sea
necesario para llenar la primera y última semana. Estos días de meses
adyacentes DEBEN mostrarse visualmente distinguidos (p. ej. atenuados) del
resto de la grilla. Clickear un día de un mes adyacente NO DEBE hacer
drill-down a la vista de Gastos: en su lugar DEBE navegar la grilla del
calendario al mes al que pertenece ese día. Decisión explícita de esta
spec: un día de otro mes es una acción de navegación de calendario, no un
punto de entrada a Gastos.

### R-4 — Drill-down a Gastos filtrados por día

Clickear cualquier día perteneciente al mes actualmente navegado (no un día
adyacente, ver R-3) DEBE navegar a la vista de Gastos con un filtro
`DateFrom == DateTo ==` el día clickeado, incluyendo días sin gastos
registrados (la vista de Gastos DEBE mostrar una lista vacía sin lanzar
ninguna excepción).

### R-5 — Agregación en base de datos

El sistema DEBE calcular los totales diarios de gastos del mes navegado
mediante una única consulta SQL (`GROUP BY`/`SUM`) sobre un `DateRange`, y
NO DEBE sumar en memoria a partir de listas paginadas de transacciones.

### R-6 — Navegación parametrizada aditiva

El sistema DEBE extender el mecanismo de navegación con una forma de
navegación parametrizada que permita configurar el estado de la vista de
destino (p. ej. el filtro de fecha) antes de que esa vista se vuelva la
vista activa. Esta extensión DEBE ser aditiva: las 5 rutas de navegación
existentes (Dashboard, Gastos, Ingresos, Recurrentes, Deudas), invocadas
sin parámetros, DEBEN seguir funcionando sin cambios de comportamiento ni
de firma.

### R-7 — Localización de nombres de mes y día

El sistema DEBE mostrar el nombre del mes navegado y los nombres/
abreviaturas de los días de la semana usando `CultureInfo.CurrentCulture`,
siguiendo el mismo patrón que `DashboardViewModel.FormatMonthLabel`.

## Escenarios

### S-1 — Total diario normaliza multi-moneda a un solo valor en DOP

- GIVEN un mes con gastos registrados en varios días, algunos en DOP y
  otros en USD
- WHEN se abre la vista de calendario para ese mes
- THEN cada celda del mes muestra un único badge con el total de ese día
  expresado en DOP

### S-2 — Navegación recalcula la grilla completa

- GIVEN la vista de calendario abierta en el mes actual
- WHEN el usuario navega al mes anterior o al mes siguiente
- THEN la grilla de 42 celdas se recalcula completa para el nuevo mes,
  incluyendo sus días adyacentes de relleno

### S-3 — Click en día sin gastos del mes actual

- GIVEN un día del mes navegado sin ningún gasto registrado
- WHEN el usuario clickea ese día
- THEN el sistema navega a la vista de Gastos filtrada a ese día exacto y
  muestra una lista vacía, sin lanzar ninguna excepción

### S-4 — Click en día con gastos del mes actual

- GIVEN un día del mes navegado con uno o más gastos registrados
- WHEN el usuario clickea ese día
- THEN el sistema navega a la vista de Gastos filtrada a ese día exacto
  (`DateFrom == DateTo ==` ese día) y muestra exactamente los gastos de ese
  día

### S-5 — Mes que empieza a mitad de semana

- GIVEN un mes cuyo primer día cae un miércoles
- WHEN se abre la vista de calendario para ese mes
- THEN la grilla incluye los días finales del mes anterior (lunes y martes
  de esa semana) atenuados, completando la primera semana

### S-6 — Click en día de mes adyacente navega el calendario, no hace drill-down

- GIVEN la grilla del mes actual mostrando días atenuados del mes
  siguiente (trailing) en la última semana
- WHEN el usuario clickea uno de esos días atenuados
- THEN el sistema navega la grilla del calendario al mes al que pertenece
  ese día, sin abrir la vista de Gastos

### S-7 — Las rutas de navegación existentes no sufren regresión

- GIVEN el overload de navegación parametrizado agregado para el
  drill-down del calendario
- WHEN se invoca la navegación sin parámetros hacia Dashboard, Gastos,
  Ingresos, Recurrentes o Deudas
- THEN cada una de las 5 rutas sigue navegando correctamente, sin cambios
  de comportamiento respecto al estado anterior a esta fase

## Fuera de alcance

Selector de semana/año, calendario de ingresos (solo gastos),
presupuestos/metas, mapa de calor por color (badge numérico simple, ya
decidido), listas de gastos dentro de la celda.

## Criterios de aceptación

- [x] El calendario muestra una grilla mensual; cada día del mes navegado
  muestra su total de gastos en DOP (incluyendo cero cuando no hay
  gastos).
- [x] La navegación mes a mes funciona; nombres de mes y de día respetan
  `CultureInfo.CurrentCulture`.
- [x] Los días de meses adyacentes (leading/trailing) que completan la
  grilla de 42 celdas se muestran visualmente distinguidos y, al
  clickearlos, navegan el calendario a su mes, no a Gastos.
- [x] Clickear cualquier día del mes navegado abre Gastos filtrado a
  exactamente ese día, incluyendo días sin gastos.
- [x] `GetDailyTotalsAsync` agrega en SQL, una consulta por mes (confirmado
  también en runtime real vía `dotnet run`, no solo en tests).
- [x] Las 5 rutas de navegación existentes siguen funcionando sin cambios
  después de agregar el overload parametrizado (los 5 tests preexistentes
  pasan sin modificación).
- [x] `docs/architecture.md` §10 refleja la capacidad de navegación real.
- [x] Todos los tests pasan en verde (795/795: 647 unit + 148 integration,
  sin regresiones).
