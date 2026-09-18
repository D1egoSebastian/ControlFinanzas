# F-008 — Dashboard y visualizaciones base

> **Fase:** 8 — Vistas por periodo y visualizaciones base
> **Estado:** ✅ Archivada (2026-09-18)
> **SDD:** `.atl/sdd/f-008p8-dashboard-visualizations/`

## Objetivo

Convertir el Dashboard, hoy un placeholder estático sin bindings, en la
pantalla de control del mes: selector de periodo mensual con navegación,
resumen normalizado a DOP, gráficos de categorías y evolución temporal, y
una tarjeta de próximos pagos que combina cuotas de deuda pendientes y
generaciones de recurrentes próximas — con toda la agregación ejecutada en
base de datos, no en memoria.

## Requisitos

### R-1 — Selector de periodo mensual

El sistema DEBE mostrar el mes actual por defecto al abrir el Dashboard y
DEBE permitir navegar al mes anterior y al mes siguiente, recalculando todo
el contenido del Dashboard para el periodo seleccionado.

### R-2 — Resumen del mes normalizado a DOP

El sistema DEBE mostrar, para el mes seleccionado, el total de gastos, el
total de ingresos, el balance neto (ingresos − gastos) y el delta
porcentual respecto al mes inmediatamente anterior, todos convertidos y
presentados en DOP. La conversión DEBE usar
`ICurrencyConverter.ConvertAsync(money, Currency.DOP, ct)` — no
`DisplayAsync`, que convierte siempre a la moneda "contraria" (DOP→USD
incluido) y por lo tanto NO normaliza a DOP cuando el origen ya es DOP.
El formato de presentación reutiliza `Strings.ApproximateFormat`
(`≈ {N2} {Currency}`).

### R-3 — Agregación en base de datos

El sistema DEBE calcular los totales, agrupaciones por categoría y series
temporales mediante consultas SQL (`GROUP BY`/`SUM`) sobre un `DateRange`,
y NO DEBE traer listas paginadas de transacciones a memoria para sumarlas.

### R-4 — Gráfico de categorías

El sistema DEBE mostrar, para el mes seleccionado, un gráfico con las 5
categorías de mayor gasto individual y una categoría agregada "Otras" que
agrupa la suma del resto; la suma de todas las porciones del gráfico DEBE
reconciliar con el total de gastos del resumen del mes.

### R-5 — Gráfico de evolución temporal

El sistema DEBE mostrar un gráfico de línea con los totales mensuales sobre
una ventana de **6 meses** consecutivos que termina en el mes seleccionado.
Se elige 6 en vez de 12 porque los datos reales empiezan en Fase 4: una
ventana de 12 meses hoy se vería mayormente en cero. El valor es una
constante nombrada (no un número mágico disperso en el código) para poder
ampliarla a 12 con un cambio de una línea cuando haya más historial.

### R-6 — Próximos pagos

El sistema DEBE mostrar una tarjeta "Próximos pagos" que lista, dentro de un
horizonte de **30 días** desde la fecha actual (`IClock.Today`): cuotas de
deuda (`DebtPayment`) en estado `Pending` ordenadas por `ScheduledDate`, y
reglas recurrentes (`RecurringRule`) cuya `NextGenerationDate` cae dentro
del mismo horizonte; ambas fuentes DEBEN combinarse, ordenarse por fecha y
limitarse a un máximo de 10 elementos. Esta tarjeta está anclada a la fecha
actual, no al mes seleccionado en el Dashboard (ver R-1a).

El horizonte es de 30 días, no 14: con 14 días una obligación mensual (el
caso dominante — `Subscription`/`FixedExpense`/`Debt` son mayormente
mensuales) es visible solo ~47% de los días de su propio ciclo. 30 días
garantiza que toda obligación mensual aparezca al menos una vez.

### R-1a — Próximos pagos no depende del periodo navegado

La tarjeta "Próximos pagos" NO DEBE recalcularse al navegar entre meses
(R-1): siempre refleja los próximos 30 días desde hoy, sin importar qué mes
esté seleccionado en el resto del Dashboard. Mostrar "próximos pagos" para
un mes pasado o futuro navegado no tiene sentido de producto.

### R-7 — Estados vacíos

Cuando el mes seleccionado no tiene transacciones, cuando no hay próximos
pagos dentro del horizonte, o cuando la tasa de cambio no está disponible
para una conversión, el sistema DEBE degradar el widget afectado a un
estado vacío o sin conversión — NUNCA DEBE lanzar una excepción no
controlada ni dejar la vista en blanco.

### R-8 — Dependencia de gráficos

El sistema DEBE renderizar los gráficos usando
`LiveChartsCore.SkiaSharpView.Avalonia` como dependencia directa de la capa
UI, sin una abstracción/interfaz adicional detrás, consistente con las
reglas de `ArchitectureTests`.

## Escenarios

### S-1 — Resumen normaliza transacciones multi-moneda a un solo total

- GIVEN un mes con gastos e ingresos registrados tanto en DOP como en USD
- WHEN se abre el Dashboard para ese mes
- THEN el resumen del mes muestra un único total de gastos, un único total
  de ingresos y un balance neto, todos expresados en DOP

### S-2 — Navegación recalcula el contenido dependiente del periodo

- GIVEN el Dashboard abierto en el mes actual
- WHEN el usuario navega al mes anterior o al mes siguiente
- THEN el resumen y los gráficos de categorías y evolución se recalculan
  para el nuevo periodo seleccionado; la tarjeta "Próximos pagos" NO cambia
  (permanece anclada a los 30 días desde hoy, ver R-1a)

### S-3 — Mes sin transacciones

- GIVEN un mes seleccionado sin ningún `Expense` ni `Income` registrado
- WHEN se abre el Dashboard para ese mes
- THEN el resumen muestra totales en cero, el gráfico de categorías y el de
  evolución muestran su estado vacío, y no se lanza ninguna excepción

### S-4 — Top 5 categorías + Otras

- GIVEN un mes con gasto registrado en 7 categorías distintas
- WHEN se abre el Dashboard para ese mes
- THEN el gráfico de categorías muestra las 5 categorías de mayor gasto
  individual y una porción "Otras" cuyo valor es la suma de las 2
  categorías restantes

### S-5 — Próximos pagos combina deudas y recurrentes

- GIVEN una `DebtPayment` pendiente con `ScheduledDate` dentro de los
  próximos 30 días y una `RecurringRule` con `NextGenerationDate` dentro de
  los próximos 30 días
- WHEN se abre el Dashboard
- THEN la tarjeta "Próximos pagos" lista ambos elementos, ordenados por
  fecha

### S-6 — Tasa de cambio no disponible degrada sin romper

- GIVEN un mes con transacciones en una moneda distinta a DOP y un
  proveedor de tasas de cambio que falla para esa conversión
- WHEN se abre el Dashboard para ese mes
- THEN la figura afectada se muestra sin conversión o con un indicador
  claro de que la conversión no está disponible, y el resto del Dashboard
  se renderiza normalmente

## Fuera de alcance

- Selector de semana, año o vista de comparación dedicada (solo mes con
  navegación anterior/siguiente en esta fase).
- Vista de calendario (Fase 9).
- Presupuestos y metas — barras de progreso, umbrales, estados de
  sobregiro (Fases 10-11; esos dominios no existen todavía).
- Drill-down desde un gráfico hacia la lista de transacciones filtrada.
- Exportar o imprimir datos del Dashboard.
- Modificar la semántica de paginación de `ExpenseFilter`/`IncomeFilter`
  usada por las pantallas de lista existentes.

## Criterios de aceptación

- [x] El Dashboard muestra el mes actual al abrir, con navegación
  funcional al mes anterior y siguiente.
- [x] El resumen del mes muestra total de gastos, total de ingresos,
  balance neto y delta vs. mes anterior, todo normalizado a DOP con el
  patrón `≈ convertido` (vía `ConvertAsync`, no `DisplayAsync` — bug real
  detectado en design y verificado corregido).
- [x] El gráfico de categorías muestra 5 categorías + "Otras", y sus
  totales reconcilian con el resumen del mes (probado explícitamente,
  incluyendo el camino de precisión decimal/SQLite).
- [x] El gráfico de evolución temporal renderiza los totales mensuales en
  una línea (ventana de 6 meses, zero-filled).
- [x] La tarjeta "Próximos pagos" lista cuotas de deuda pendientes y/o
  generaciones de recurrentes próximas dentro del horizonte de 30 días,
  anclada a la fecha actual (no cambia al navegar de mes).
- [x] Las agregaciones se ejecutan como SQL `GROUP BY`/`SUM`, no como sumas
  en memoria sobre listas paginadas (confirmado también en runtime real,
  no solo en tests).
- [~] Los gráficos renderizan correctamente en Windows, macOS y Linux —
  verificado hasta el techo posible en este entorno (compila, AXAML
  resuelve los controles de LiveCharts, la app no crashea al cargar el
  Dashboard); el renderizado visual pixel-a-pixel en las 3 plataformas
  queda pendiente de confirmación manual por Diego — no hay herramienta de
  captura de ventanas nativas disponible en este entorno.
- [x] Los estados vacíos (sin transacciones, sin próximos pagos, tasa de
  cambio no disponible) degradan sin lanzar excepciones ni dejar la vista
  en blanco.
- [x] `dotnet format style`, tests unitarios, de integración y de
  arquitectura pasan en verde (Release: build y tests de integración
  verificados; tests unitarios verificados en Debug — el host de tests en
  Release está bloqueado en esta máquina por una política de Windows
  Application Control ajena al código, ver `verify-report.md`).
