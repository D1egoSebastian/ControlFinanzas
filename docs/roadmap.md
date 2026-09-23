# Roadmap — ControlDeFinanzasIA

> Orden recomendado de implementación. Cada fase está pensada para convertirse
> en una **Specification** independiente. Las dependencias se entienden como
> "esta fase no puede completarse hasta que las anteriores estén terminadas",
> no como "esta fase no puede empezar antes" — algunas se pueden solapar.

---

## 1. Principios de ordenamiento

El orden de las fases sigue cinco reglas explícitas. Cualquier desviación
debería justificarse en el documento de la Specification correspondiente.

1. **Cimientos antes que features.** El bootstrap del proyecto, el esqueleto
   de arquitectura y la persistencia van antes de cualquier feature de
   producto. Sin esto no hay forma de validar nada.
2. **Slice vertical antes que expansión horizontal.** Una sola feature
   end-to-end (registrar un gasto y verlo) se construye antes de añadir el
   resto del CRUD, los filtros o las visualizaciones. Demuestra que el
   patrón funciona antes de multiplicarlo.
3. **Core de datos antes que inteligencia sobre los datos.** Los
   presupuestos, las metas, las visualizaciones y la IA necesitan datos
   sólidos para ser útiles. La IA, en particular, se hace al final porque
   depende de casi todo lo demás.
4. **Determinístico antes que IA.** Cada capacidad que la IA puede ofrecer
   tiene una versión determinista (categoría sugerida pero elegible,
   detector de anomalías base, cálculo puro de plan de ahorro). El camino
   determinista va primero; la IA se monta encima como mejora, no como
   reemplazo. Esto cumple la regla de "la app funciona sin IA".
5. **Configuración antes que uso de la configuración.** Los ajustes de
   tema, idioma y clave de IA se almacenan y editan antes de que cualquier
   feature los consuma. Esto evita re-trabajo de localización y de wiring
   de IA en fases intermedias.

---

## 2. Vista de conjunto

| # | Fase | Tipo | Resultado visible |
| --- | --- | --- | --- |
| 0 | Bootstrap del proyecto | Cimientos | Solución compila, ventana vacía arranca |
| 1 | Esqueleto de arquitectura y contratos | Cimientos | Las 4 capas existen; NetArchTest valida el grafo |
| 2 | Persistencia con EF Core + SQLite | Cimientos | DB local, primera migración, ruta por SO |
| 3 | Slice vertical: registrar y listar gastos | Core | "Hello world" funcional: registrar y ver un gasto |
| 4 | Transacciones: ingresos, edición, borrado, tags, filtros, vistas | Core | CRUD completo de gastos/ingresos con búsqueda |
| 5 | Método de pago + multi-moneda DOP/USD | Core | Cada movimiento lleva método y moneda, conversión display-time |
| 6 | Recurrentes: suscripciones y gastos fijos | Features | Crear/editar/eliminar suscripciones; generación del mes |
| 7 | Deudas con plan de cuotas | Features | Crear deuda, ver calendario, registrar pagos |
| 8 | Vistas por periodo y visualizaciones base | Visualización | Dashboard con resumen del mes, gráficos por categoría, evolución |
| 9 | Vista de calendario de gastos | Visualización | Calendario con totales diarios |
| 10 | Presupuestos mensuales por categoría | Features | Crear presupuesto, ver progreso, alerta de límite |
| 11 | Metas de ahorro con fecha objetivo y aportes manuales | Features | Crear meta, contribuir, ver progreso, plan determinista |
| 12 | Configuración: tema, idioma, IA (sin uso) | Cimientos | Settings editables; i18n funciona; API key persistida |
| 13 | Pipeline de IA + categorización automática | IA | Al escribir un gasto, la IA sugiere categoría |
| 14 | IA: insights, predicción de fin de mes, anomalías | IA | Dashboard muestra insights, forecast, anomalías |
| 15 | Chat con IA sobre tus finanzas | IA | Vista de chat; preguntas en lenguaje natural |
| 16 | Plan de ahorro con IA para metas | IA | Cada meta puede pedir un plan explicado por IA |
| 17 | Notificaciones | Features | Avisos por pagos, presupuestos, resumen, anomalías |
| 18 | Exportación a PDF y Excel | Features | Botón de exportar reporte actual |
| 19 | Backup y restauración | Features | Exportar/importar archivo de respaldo completo |
| 20 | Importación CSV / Excel | Features | Wizard de import con mapeo de columnas |
| 21 | Empaquetado, pulido, accesibilidad | Cierre | Instaladores Win/Mac/Linux; app pulida |

**Fases 0 a 12 = MVP funcional sin IA.** Todo lo de la 13 en adelante es
mejora progresiva. La 21 es el cierre.

---

## 3. Fases

---

### Fase 0 — Bootstrap del proyecto

**Objetivo**
Tener una solución que compila, levanta y muestra una ventana vacía de
Avalonia, con tooling mínimo acordado.

**Justificación**
Nada de lo demás se puede hacer sin esta base. Hacerla primero previene
el escenario de "ya tenemos código y ahora hay que reorganizar". El
tooling (editorconfig, warnings-as-errors, CI) también va aquí para
impedir que se introduzca deuda desde el día uno.

**Dependencias**
- Ninguna.

**Entregables esperados**
- `ControlDeFinanzasIA.sln` con los 6 proyectos vacíos (placeholders).
- `ControlDeFinanzasIA.UI` arranca y muestra una `MainWindow` vacía.
- `.editorconfig`, configuración de StyleCop, `Nullable` activado.
- Pipeline de CI mínimo que ejecute `dotnet build` y `dotnet test`.
- Script de **commit pre-push** opcional (formato conventional commits).

**Riesgos principales**
- Particularidades del template Avalonia en .NET 8.
- RIDs y publicación cross-platform todavía no configurados.
- Conflictos de versión de paquetes NuGet.

**Criterios de fase completada**
- `dotnet build` pasa sin warnings.
- `dotnet test` corre (aunque no haya tests aún).
- La app arranca en Windows, macOS y Linux mostrando una ventana.
- CI en verde.

---

### Fase 1 — Esqueleto de arquitectura y contratos base

**Objetivo**
Que las 4 capas existan con sus proyectos, dependencias y reglas, y que
los contratos centrales estén definidos y protegidos por tests
arquitectónicos.

**Justificación**
Validar la arquitectura antes de escribir lógica. Si las capas están bien
puestas ahora, todo lo posterior encaja sin reorganizar. Esperar a
tener features implementadas para validar el grafo es la receta típica
del "ahora hay que mover 200 archivos de proyecto".

**Dependencias**
- Fase 0.

**Entregables esperados**
- Proyectos `Domain`, `Application`, `Infrastructure`, `UI`, `Tests.Unit`,
  `Tests.Integration` configurados con sus referencias según
  `docs/architecture.md` §4.
- En `Domain`: `Money`, `Currency`, `DateRange`, `IClock`, `IPathResolver`,
  enums vacíos.
- En `Application`: `Result<T>`, `PagedResult<T>`, interfaces
  `IExpenseRepository`, `ICategoryRepository`, `IPaymentMethodRepository`
  (vacías, solo firma).
- `NetArchTest` configurado en `Tests.Unit` validando el grafo.
- DI host configurado en el composition root de UI (vacío pero
  funcional).

**Riesgos principales**
- Definir contratos demasiado pronto y arrepentirse (mitigado: interfaces
  pequeñas y co-secuenciales a las features).
- Confundir "Application" con "servicios varios" y meter lógica de UI.

**Criterios de fase completada**
- `NetArchTest` pasa: Domain no referencia a nadie, Application no
  referencia a Infrastructure ni a UI, etc.
- Tests unitarios de `Money` y `Result<T>` en verde.
- `dotnet build` y `dotnet test` en verde.

---

### Fase 2 — Persistencia con EF Core + SQLite

**Objetivo**
Tener la base de datos local funcionando con migraciones, ruta por SO
resuelta, y al menos un repositorio funcional.

**Justificación**
Toda feature que perdure datos pasa por aquí. Definir el patrón de
persistencia (DbContext, configuraciones, migraciones, repos, value
objects) ahora evita retrofit doloroso cuando ya haya 20 entidades. La
ruta por SO se fija aquí para que ninguna feature tenga que pensar en eso
de nuevo.

**Dependencias**
- Fase 1.

**Entregables esperados**
- `AppDbContext` con `DbSet<Category>` y `DbSet<PaymentMethod>` (entidades
  mínimas para arrancar).
- Migración inicial versionada.
- `PathResolver` con la ruta por SO.
- `CategoryRepository`, `PaymentMethodRepository` (solo CRUD básico).
- Categorías y métodos de pago iniciales sembrados al primer arranque
  (seed data).
- Tests de integración: repo contra SQLite temporal, migración limpia,
  sembrado correcto.

**Riesgos principales**
- Conversión de value objects en EF Core (Money, DateRange).
- Política de migraciones destructivas (qué hacer si la versión nueva
  rompe el archivo existente).
- Sembrado idempotente (no duplicar datos en cada arranque).

**Criterios de fase completada**
- App arranca en un equipo limpio, crea el directorio y el `.db` si no
  existen, aplica migraciones, siembra categorías y métodos por defecto.
- Tests de integración de repos en verde.
- Eliminar y recrear el archivo no rompe la app.

---

### Fase 3 — Slice vertical: registrar y listar gastos

**Objetivo**
Implementar la primera feature end-to-end: registrar un gasto con
categoría, persistirlo, listarlo, navegar a la pantalla.

**Justificación**
Esta es la **prueba de fuego** de la arquitectura. Si aquí todo fluye
(Domain → Application → Infrastructure → UI con navegación, MVVM, DI,
Result, validación, test unitario, test de integración, test de
ViewModel), el resto del proyecto es iteración sobre el mismo patrón.
Saltarse este paso y empezar por "todas las features a la vez" es la
forma más rápida de acumular re-trabajo.

**Dependencias**
- Fase 2.

**Entregables esperados**
- `Expense` entidad en `Domain` con `CategoryId`, `Date`, `Amount`,
  `Description`, `Note?`.
- `RegisterExpenseUseCase` y `GetTransactionsQuery` en `Application` con
  sus DTOs y `RegisterExpenseValidator` con FluentValidation.
- `ExpenseRepository` en `Infrastructure` con queries básicas.
- `ExpensesView` y `ExpensesViewModel` en `UI`, formulario simple + lista,
  navegación desde el shell.
- `DataTemplate` registrando `ExpensesViewModel → ExpensesView`.
- Tests: unitarios del use case, integración del repo, ViewModel.

**Riesgos principales**
- CompiledBindings mal configurados.
- DataTemplate no resuelve y se muestra un TextBox vacío.
- Navegación entre ViewModels con dependencias circulares.

**Criterios de fase completada**
- El usuario abre la app, navega a "Gastos", registra uno, lo ve en la
  lista, cierra la app, la vuelve a abrir y el gasto sigue ahí.
- Cobertura de tests ≥ 80% en Domain y Application de lo construido.

---

### Fase 4 — Transacciones: ingresos, edición, borrado, tags, filtros, vistas guardadas

**Objetivo**
Completar el CRUD sobre transacciones (gastos + ingresos), añadir tags,
filtros y vistas guardadas.

**Justificación**
Es la extensión natural de la fase 3 y desbloquea la mayoría de las
features posteriores. Todo lo demás (presupuestos, recurrentes,
visualizaciones) filtra o agrupa transacciones; por eso el sistema de
filtros y vistas se construye aquí, una sola vez y bien.

**Dependencias**
- Fase 3.

**Entregables esperados**
- `Income` entidad + `RegisterIncomeUseCase`.
- `Tag` entidad + relación muchos-a-muchos con `Transaction`.
- `UpdateTransactionUseCase`, `DeleteTransactionUseCase`.
- Filtros por categoría, tags, rango de fecha.
- `SavedView` entidad con filtros serializados en JSON.
- UI: filtros aplicados, lista paginada, gestión de vistas guardadas.
- Tests de los nuevos use cases, del repositorio con queries complejas,
  de serialización de filtros.

**Riesgos principales**
- Mapeo de filtros a EF Core (especificación vs expresión dinámica).
- Versionado del JSON de `SavedView` (cambios futuros no rompen vistas
  guardadas).
- Rendimiento de queries con muchos joins.

**Criterios de fase completada**
- CRUD completo de gastos e ingresos.
- Filtros y vistas guardadas funcionan, persisten y se restauran.
- Tests de integración cubren las queries más usadas.

---

### Fase 5 — Método de pago + multi-moneda DOP/USD ✅ COMPLETADA (2026-09-08)

**Objetivo**
Que cada movimiento lleve método de pago y moneda original, y que los
reportes muestren conversión a la otra moneda con tasa en vivo.

**Justificación**
Multi-moneda es requisito de `vision.md` y el método de pago se cruza
con los gastos. Hacerlo ahora, antes de las visualizaciones y los
reportes, evita una migración de datos dolorosa más adelante (toda la
historia sin moneda quedaría mal modelada).

**Dependencias**
- Fase 4 (porque modifica el modelo de `Transaction`).

**Entregables esperados**
- `PaymentMethod` integrada a `Transaction` (ya estaba la entidad; ahora
  el uso real).
- `Money` (value object) ya creado, ahora persistido en `Transaction`.
- `IExchangeRateProvider` con una implementación stub que devuelve una
  tasa fija (placeholder).
- Conversión display-time: las vistas muestran el equivalente en la otra
  moneda.
- Migración de datos: transacciones existentes toman moneda por defecto
  (DOP) y método "Efectivo".
- Tests del cálculo de conversión y del provider stub.

**Riesgos principales**
- Conversión del value object en EF Core sin perder precisión.
- Migración no destructiva del modelo (añadir columnas nullable o con
  default).
- Definir la API real del tipo de cambio más adelante (deferred).

**Criterios de fase completada**
- Cada gasto/ingreso tiene método y moneda.
- Reportes convierten y muestran ambas monedas.
- Datos antiguos (si los hubiera) sobreviven la migración.

> **Nota:** `IExchangeRateProvider` real (llamando a una API de tipos de
> cambio) se elige e implementa cuando haya conectividad clara. El stub
> mantiene la app funcionando offline y desacopla la decisión.

**Estado actual (2026-09-08)**
- ✅ 13/13 features implementadas (5-001 a 5-013).
- ✅ 559/559 tests verdes.
- ✅ Backend completo: entidad, migración no-destructiva, commands/validators,
  DTOs extendidos, repos con `Include`, filtros, query de catálogo.
- ✅ UI completa: picker de método en formularios + segunda línea de conversión
  (`≈ 1,500.00 DOP`) en la lista + filter rail multi-select.
- ⏳ Provider de tasas real: pendiente hasta decidir API (OpenExchangeRates,
  banco central RD, etc.). El contrato `IExchangeRateProvider` está estable
  y permite enchufar el provider real sin tocar use cases ni UI.

---

### Fase 6 — Recurrentes: suscripciones y gastos fijos ✅ COMPLETADA (2026-09-17)

**Objetivo**
Permitir crear suscripciones y gastos fijos recurrentes, generar los
movimientos del mes automáticamente, editarlos y saltarlos.

**Justificación**
Las suscripciones son la fuente más fiable de movimientos del usuario
(Netflix, internet, etc.). Implementarlas aquí, sobre el CRUD de la
fase 4, da valor rápido y es prerrequisito conceptual para las deudas
de la fase 7. La generación automática también se prueba aquí con un
botón manual (no background job todavía) para mantener el comportamiento
explícito.

**Dependencias**
- Fases 4 y 5. Phase 5 debe estar cerrada porque las reglas recurrentes
  reutilizan `Money`, `Currency` y `PaymentMethodId`.

**Entregables esperados**
- `RecurringRule` (base), `Subscription`, `FixedExpense` en `Domain`.
- `RecurringOccurrenceCalculator` (servicio de dominio puro).
- Use cases: `CreateSubscriptionUseCase`, `UpdateSubscriptionUseCase`,
  `DeleteSubscriptionUseCase`, equivalentes para `FixedExpense`,
  `SkipRecurringMonthUseCase`, `EditGeneratedTransactionUseCase`,
  `GenerateDueRecurringUseCase` (botón manual).
- UI: lista de suscripciones, gestión, pantalla de "Generar del mes"
  que muestra lo que se va a crear antes de confirmar.
- Tests: calculadora con varios escenarios (mensual, bimestral, fin de
  mes, leap years), idempotencia de generación.

**Riesgos principales**
- Fechas de generación en bordes (29-31 de mes, año bisiesto).
- Edición de un movimiento generado sin romper la regla que lo creó.
- Marcar visualmente "auto-generado" sin que el usuario lo confunda con
  un movimiento manual.

**Criterios de fase completada**
- Crear una suscripción mensual, generar el mes, ver el gasto, editar
  el monto, saltar el mes siguiente.
- Tests de la calculadora cubren al menos 10 escenarios de fecha.

**Estado actual (2026-09-17) — ✅ FASE COMPLETADA**
- ✅ 6-001 `RecurringOccurrenceCalculator` (16 casos de fecha en `RecurringOccurrenceCalculatorTests`).
- ✅ 6-002 `RecurringRule` fortalecida con factory `Create` + invariantes (19 tests).
- ✅ 6-003 `IRecurringRuleRepository` + EF Core TPH + migración `20260910205248_AddRecurringRules`.
- ✅ 6-004 `CreateSubscriptionUseCase` + `UpdateSubscriptionUseCase` (backend + tests).
- ✅ 6-005 `GenerateDueRecurringUseCase` con preview/confirmación idempotente (no duplica en confirmaciones repetidas).
- ✅ 6-006 UI unificada Subscription + FixedExpense (`RecurringRulesViewModel` + `RecurringRulesView.axaml`):
  crear/editar/desactivar/eliminar/saltar.
- ✅ 6-007 Sección "Generar del mes" integrada en `RecurringRulesView` con preview y confirmación.
- ✅ 6-008 CRUD completo de `FixedExpense` (Create/Update/Delete/Deactivate) con tests.
- ✅ Provenance: `Expense.RecurringRuleId`/`RecurringPeriod` (migración `20260912150000_AddRecurringExpenseProvenance`);
  editar un movimiento generado no muta la regla origen.
- ✅ Gates: build limpio, 614/614 tests verdes (492 unit + 122 integration), `dotnet format style` limpio, NetArchTest 11/11.
- ✅ SDD archivado: `spec/features/phase-06/F-001-recurring-rules.done.md`.

Detalle operativo: `spec/features/phase-06/F-001-recurring-rules.spec.md`,
`tasks/phase-06/F-001-recurring-rules.tasks.md`,
`.atl/sdd/f-006p6-formalize-recurrentes/`.

---

### Fase 7 — Deudas con plan de cuotas ✅ COMPLETADA (2026-09-18)

**Objetivo**
Modelar deudas con plan de cuotas: crear la deuda, ver el calendario de
pagos, registrar pagos, ver el saldo pendiente, cerrar la deuda.

**Justificación**
Conceptualmente hermana de la fase 6 (cuotas son recurrentes con
cantidad finita), pero con reglas propias (plan completo generado al
crear, pagos pueden saltarse, deuda se cierra). Mantenerla como fase
separada aísla su complejidad y permite iterar sobre las lecciones de
la fase 6.

**Dependencias**
- Fase 6 (comparte patrón de generación de dominio; reutiliza `Money`,
  `Currency`. Nota: en la implementación, los pagos de deuda quedaron en su
  propia pantalla `DebtsView`, no integrados a "Generar del mes" — ver
  decisión resuelta más abajo).

**Entregables esperados**
- `Debt` y `DebtPayment` entidades.
- `DebtScheduleGenerator` (servicio de dominio puro).
- Use cases: `CreateDebtWithScheduleUseCase`, `RegisterDebtPaymentUseCase`,
  `SkipDebtPaymentUseCase`, `GetDebtScheduleQuery`.
- UI: lista de deudas, detalle con calendario de cuotas, registro de
  pago.
- Integración con la generación de recurrentes: las `DebtPayment` del
  mes también se generan como movimientos visibles.
- Tests: generador con varios escenarios (cuotas mensuales, quincenales,
  con y sin saltos), consistencia del saldo.

**Riesgos principales (resueltos)**
- Pago parcial vs total → **resuelto: solo pago total en v1** (parcial queda
  fuera de alcance, documentado explícitamente en la spec).
- Saltar cuotas manteniendo coherencia del plan → **resuelto**: `SkipDebtPaymentUseCase`
  no re-numera ni desplaza las cuotas siguientes (probado).
- Interacción con la fase 6 → **resuelto: pantalla separada** (`DebtsView`).
  Pagar una cuota es un evento de seguimiento explícito, distinto del modelo
  de preview/confirmación de "Generar del mes".

**Criterios de fase completada**
- Crear una deuda a 12 cuotas, ver el calendario, registrar 3 pagos,
  ver el saldo actualizado, cerrar la deuda al pagar la última.
- El saldo pendiente nunca es inconsistente con los pagos registrados.

**Estado actual (2026-09-18) — ✅ FASE COMPLETADA**
- ✅ `Debt`/`DebtPayment` (aggregate con factory `Create`, invariantes) + `DebtScheduleGenerator`
  (servicio de dominio puro, 15 escenarios de fecha en `DebtScheduleGeneratorTests`).
- ✅ `IDebtRepository`/`IDebtPaymentRepository` + EF Core + migración `20260918025303_AddDebts`.
- ✅ `CreateDebtWithScheduleUseCase` (calendario completo en una operación transaccional),
  `RegisterDebtPaymentUseCase` (recalcula saldo, cierra automáticamente en la última cuota),
  `SkipDebtPaymentUseCase`, `GetDebtsQuery`, `GetDebtScheduleQuery`.
- ✅ UI: `DebtsView`/`DebtsViewModel` — lista de deudas, calendario de cuotas, registrar pago,
  saltar cuota, saldo pendiente visible.
- ✅ Gates: build limpio, 698/698 tests verdes (568 unit + 130 integration, subiendo desde 614),
  `dotnet format style` limpio, NetArchTest 11/11 — verificados de forma independiente antes de
  archivar, no solo confiando en el reporte del agente que implementó.
- ✅ Dos bugs reales detectados y corregidos durante el apply: instancia de `Money` compartida
  entre cuotas generadas (EF Core owned-type la perdía silenciosamente para todas menos una),
  y `AppDbContextModelSnapshot.cs` desactualizado respecto a una migración de Fase 6 escrita a
  mano (habría reintentado añadir columnas de `Expenses` ya existentes).
- ✅ SDD archivado: `spec/features/phase-07/F-001-debts-installment-plan.done.md`.

---

### Fase 8 — Vistas por periodo y visualizaciones base ✅ COMPLETADA (2026-09-18)

**Objetivo**
Implementar las vistas por periodo (semana, mes, año, comparación) y las
visualizaciones principales: resumen del mes, gráficos por categoría,
evolución temporal.

**Justificación**
El usuario ya tiene datos en la fase 7. Es el momento de darle
**sentido** a esos datos. Esta fase entrega el dashboard principal y
cambia la percepción de la app de "tracker" a "herramienta de
control". Las visualizaciones de calendario (fase 9) y presupuestos
(fase 10) se apoyan en lo construido aquí.

**Dependencias**
- Fases 4, 5 (datos + multi-moneda para mostrar conversiones en los
  gráficos).

**Entregables esperados**
- `DateRange` value object ya creado, ahora usado en queries.
- Componente de selección de periodo (semana, mes, año, comparación).
- `DashboardView` con: resumen del mes, top categorías, comparación
  con mes anterior, evolución temporal.
- Componentes de gráficos reutilizables.
- Tests del cálculo de agregaciones y del componente de periodo.

**Riesgos principales (resueltos)**
- Elección de librería de gráficos → **resuelto: LiveCharts2**
  (`LiveChartsCore.SkiaSharpView.Avalonia` 2.0.5, MIT). Única opción mantenida y compatible con
  Avalonia 11.2.7 al momento de evaluar (OxyPlot solo tenía soporte prerelease para Avalonia 11;
  ScottPlot requiere Avalonia 12).
- Rendimiento de agregaciones sobre datasets grandes → **resuelto**: agregación en SQL
  (`GROUP BY`/`SUM`), nunca en memoria. El proveedor SQLite de EF Core 8 no soporta `Sum(decimal)`
  directamente (lanza `NotSupportedException`); se castea a `double` y se redondea de vuelta a 4
  decimales tras materializar, con tests de precisión dedicados.
- Formato correcto de números por cultura → reusa el patrón `≈ {N2} {Currency}` ya establecido.

**Alcance ajustado respecto al plan original** (documentado, no drift silencioso):
- Selector de periodo: solo **mes** con navegación anterior/siguiente en este slice (semana/año/
  comparación dedicada quedan diferidos — Fases 6 y 7 mostraron que el riesgo de PR sube mucho al
  no acotar el alcance inicial). El criterio "comparación con mes anterior" se satisface con un
  delta % inline en la tarjeta de resumen, no una vista de comparación separada.
- Se agregó una tarjeta **"Próximos pagos"** (cuotas de deuda + recurrentes próximas) no prevista
  en el plan original — decisión deliberada del usuario durante el proposal, ya que Fases 6 y 7
  dejaron los datos disponibles y es lo más accionable de una pantalla de inicio financiera.

**Criterios de fase completada**
- Dashboard muestra mes actual, navegación a meses anteriores/posteriores. ✅
- Comparación este mes vs mes anterior funciona (como delta inline). ✅
- Gráficos se renderizan correctamente en los 3 SO — verificado hasta el techo posible en este
  entorno (compila, resuelve en runtime sin excepción); confirmación visual pixel-a-pixel
  pendiente de que Diego abra la app localmente.

**Estado actual (2026-09-18) — ✅ FASE COMPLETADA**
- ✅ Capa de agregación: DTOs, `MonthPeriod`/`DashboardDefaults`, métodos SQL en
  `IExpenseRepository`/`IIncomeRepository`.
- ✅ `GetMonthSummaryQuery`, `GetCategoryBreakdownQuery`, `GetMonthlyEvolutionQuery`,
  `GetUpcomingPaymentsQuery` — normalizan a DOP vía `ConvertAsync` (no `DisplayAsync`, bug real
  detectado en el diseño antes de escribir código).
- ✅ `DashboardViewModel`/`DashboardView` reescritos: navegación de mes, resumen, 2 gráficos
  LiveCharts2, tarjeta de próximos pagos.
- ✅ Gates: build limpio, 751/751 tests verdes (608 unit + 143 integration, subiendo desde 614 al
  cierre de Fase 7), `dotnet format style` limpio, NetArchTest 11/11.
- ⚠️ Nota de entorno: los tests unitarios en configuración Release están bloqueados en esta
  máquina por una política de Windows Application Control (no es un defecto de código — el build
  Release compila limpio y los tests de integración sí corren en Release). Verificados en Debug.
- ✅ SDD archivado: `spec/features/phase-08/F-008-dashboard-visualizations.done.md`.

---

### Fase 9 — Vista de calendario de gastos ✅ COMPLETADA (2026-09-18)

**Objetivo**
Una vista de calendario donde cada día muestra los gastos (totales o
lista).

**Justificación**
Es la última visualización "estática" del set definido en `vision.md`.
Deferirla a una fase propia permite que la decisión de UX del calendario
(densidad, drill-down) no contamine las decisiones de la fase 8.

**Dependencias**
- Fase 8.

**Entregables esperados**
- `CalendarView` con navegación mes a mes.
- Cada día muestra total gastado o un indicador visual.
- Drill-down a los gastos del día al hacer click.
- Tests del componente de calendario y de la query de totales diarios.

**Riesgos principales (resueltos)**
- UX densa cuando hay muchos días con gastos → **resuelto: badge numérico simple** (decisión del
  usuario), sin mapa de calor. Un día de 42 en la grilla es lo bastante chico para que un badge
  de texto no se sienta denso.
- Localización de nombres de día/mes → **resuelto**: reusa `CultureInfo.CurrentCulture`, mismo
  patrón que `DashboardViewModel.FormatMonthLabel` de Fase 8.

**Alcance ajustado respecto al plan original**:
- Días de meses adyacentes (relleno de la grilla de 42 celdas) NO hacen drill-down al clickearlos
  — navegan el calendario a su propio mes. El plan original no distinguía este caso; se fijó como
  decisión explícita de la spec.
- Esta fase agregó, además, una extensión real a `INavigationService` (navegación parametrizada)
  no prevista en el plan original — necesaria para que el drill-down funcione sin acoplar el
  calendario a los internals de `ExpensesViewModel`.

**Criterios de fase completada**
- Calendario funcional con drill-down a gastos del día. ✅

**Estado actual (2026-09-18) — ✅ FASE COMPLETADA**
- ✅ `GetDailyTotalsAsync` (agregación SQL diaria), `CalendarGrid` (grilla pura de 42 celdas),
  `GetCalendarMonthQuery` (normaliza a DOP).
- ✅ `INavigationService` extendido con navegación parametrizada aditiva — las 5 rutas existentes
  sin regresión (confirmado con sus tests preexistentes sin modificar).
- ✅ `CalendarViewModel`/`CalendarView`/`MonthGrid` — un solo `ItemTemplate` compartido, no 42
  celdas escritas a mano.
- ✅ Gates: build limpio, 795/795 tests verdes (647 unit + 148 integration, subiendo desde 751 al
  cierre de Fase 8), `dotnet format style` limpio, NetArchTest 11/11.
- ✅ **Primera vez que se ejecutó la app de verdad** (`dotnet run` + captura de pantalla) en vez de
  confiar solo en tests — encontró y permitió corregir dos bugs reales preexistentes: la ruta
  Deudas renderizaba en blanco desde Fase 7 (`ViewLocator` sin el caso `DebtsViewModel`), y todo
  el sistema de diseño "Libro Mayor" (desde Fase 4) nunca tuvo efecto en runtime porque su hoja de
  estilos no estaba fusionada en `App.axaml` — al fusionarla apareció un bug real de tipo
  (`CornerRadius` declarado como `double`) que solo se manifestaba ejecutando la app.
- ✅ SDD archivado: `spec/features/phase-09/F-009-calendar-view.done.md`.

---

### Fase 10 — Presupuestos mensuales por categoría ✅ COMPLETADA (2026-09-20)

**Objetivo**
Permitir crear un presupuesto mensual por categoría, ver el progreso en
el dashboard y alertar cuando se acerque al límite.

**Justificación**
Los presupuestos son la primera feature que **evalúa** los datos
(presupuesto vs real). Necesita las visualizaciones de la fase 8 para
mostrar el progreso. Se hace antes que metas porque conceptualmente es
más simple (un valor mensual) y allana el camino para metas
(multi-periodo con fecha objetivo).

**Dependencias**
- Fase 8.

**Entregables esperados**
- `Budget` entidad, `SetMonthlyBudgetUseCase`, `EvaluateBudgetsQuery`.
- `BudgetEvaluator` (servicio de dominio puro): porcentaje consumido y
  estado (OK / Cerca / Excedido).
- UI: gestión de presupuestos del mes, barras de progreso en dashboard.
- Tests del evaluador con varios escenarios (0%, 50%, 100%, 120%).

**Riesgos principales (resueltos)**
- Definir el umbral "Cerca del límite" (¿80%? ¿90%?) → **resuelto: 80%**
  (`Near` desde 80%, `Exceeded` desde 100%), constantes nombradas en
  `BudgetEvaluator`.
- Presupuestos que cruzan meses: al llegar fin de mes, ¿se arrastran? →
  **resuelto: sin rollover**. Cada mes se evalúa de forma completamente
  independiente.
- Interacción con la conversión multi-moneda: el presupuesto está en
  DOP pero el gasto real en USD; ¿cómo se compara? → **resuelto**: ambos
  lados (presupuesto y gasto real) se normalizan a DOP vía
  `ICurrencyConverter.ConvertAsync` (nunca `DisplayAsync`); una tasa
  faltante en cualquiera de los dos lados marca la línea como
  indeterminada, nunca como un 0%/100% silenciosamente incorrecto.

**Criterios de fase completada**
- Crear un presupuesto para una categoría, ver el progreso, recibir
  alerta al acercarse al límite. ✅ (alerta = color-coding en el dashboard;
  no se agregaron notificaciones push/toast, explícitamente fuera de
  alcance para v1)

**Alcance ajustado respecto al plan original**:
- No se agregaron notificaciones/alertas fuera de la app — el estado
  color-coded en el dashboard es la única superficie de estado para esta
  fase (decisión explícita de la spec, R-8).
- Presupuestos globales (sin categoría) quedan explícitamente rechazados
  en v1 a nivel de caso de uso, aunque `CategoryId` se mantiene nullable
  en el esquema para no requerir una reconstrucción de tabla si se
  agregan en una fase futura.

**Estado actual (2026-09-20) — ✅ FASE COMPLETADA**
- ✅ `Budget` (clase con invariantes + factory `Create`), `BudgetStatus`,
  `BudgetEvaluator` (servicio de dominio puro, umbrales 80%/100% como
  constantes).
- ✅ `BudgetRepository` con upsert vía índice único `(Year, Month,
  CategoryId)` en dos capas (caso de uso + índice DB); corregido un bug
  real de copia de tipo `OwnsOne` (`Amount`) que habría hecho que editar
  un presupuesto no guardara nada — encontrado y corregido durante esta
  fase, no heredado de `DebtRepository` sin arreglar (ver Fase 11 abajo).
- ✅ `GetBudgetVsActualQuery`: normaliza ambos lados a DOP vía
  `ConvertAsync`, nunca `DisplayAsync`; tasa faltante marca la línea como
  indeterminada.
- ✅ `BudgetsView`/`BudgetsViewModel` — gestión CRUD de presupuestos por
  mes; card de progreso en el Dashboard reutilizando la navegación de mes
  existente (sin selector de mes independiente).
- ✅ Gates: build limpio, 861/861 tests verdes (703 unit + 158
  integration, subiendo desde 795 al cierre de Fase 9), `dotnet format
  style` limpio, NetArchTest 11/11.
- ✅ Aplicado en 4 commits encadenados (`94dbc3e`, `1daf049`, `7fd44c2`,
  `4d4cd40`), con re-verificación independiente de los 4 gates después de
  cada slice, no solo al final.
- ✅ SDD archivado: `spec/features/phase-10/F-010-monthly-budgets.done.md`.

---

### Fase 11 — Metas de ahorro con fecha objetivo y aportes manuales ✅ COMPLETADA (2026-09-20)

**Objetivo**
Crear metas con monto y fecha objetivo, registrar aportes manuales,
ver el progreso. Incluir la versión **determinista** del plan de ahorro
("si quieres llegar a X en Y meses, debes aportar Z al mes").

**Justificación**
Las metas cierran el ciclo de "planificación". El cálculo determinista
del plan de ahorro se hace aquí, sin IA, para establecer la fórmula y
poder contrastarla con la versión IA en la fase 16. La IA no entra
todavía — llega cuando la configuración y el resto del pipeline de IA
estén listos.

**Dependencias**
- Fases 4 (datos), 10 (modelo de presupuesto → paralelo posible, no
  bloquea).

**Entregables esperados**
- `Goal`, `GoalContribution` entidades.
- `CreateGoalUseCase`, `ContributeToGoalUseCase`, `GetGoalsQuery`.
- `SavingsPlanCalculator` (servicio de dominio puro): monto mensual
  necesario.
- UI: lista de metas, detalle, registro de aporte, plan sugerido
  determinista.
- Tests del calculador con varios escenarios (meta ya cumplida, fecha
  pasada, monto muy ambicioso).

**Riesgos principales (resueltos)**
- Aportes en moneda distinta a la meta: comportamiento a definir. →
  **resuelto: rechazados.** `ContributeToGoalUseCase` compara la moneda del
  aporte contra la de la meta ANTES de cualquier persistencia y responde con
  un error `CurrencyMismatch` localizado; nunca se usa `ICurrencyConverter`
  en el camino de metas.
- Fecha objetivo en el pasado: rechazar vs advertencia. → **resuelto:
  advertencia, no rechazo.** `Goal.Create` acepta una `TargetDate` pasada sin
  restricción; `SavingsPlanCalculator` la surface como un estado explícito
  `TargetDateInPast`, no lanzable, en vez de una excepción de división por
  cero.
- "Aporte automático desde ingreso no gastado" se documenta como
  v2 (no se implementa aquí; se aclara en la Specification). →
  **confirmado diferido** — no implementado en v1.

**Criterios de fase completada**
- Crear una meta, aportar manualmente, ver el progreso, obtener el plan
  determinista. ✅
- La cifra del plan determinista es correcta con datos conocidos (test). ✅
  (`SavingsPlanCalculator` contra la tabla de 10 casos límite del diseño,
  incluyendo el caso que distingue redondeo hacia arriba de
  `MidpointRounding.AwayFromZero`)

**Alcance ajustado respecto al plan original**:
- Integración con el Dashboard diferida explícitamente fuera de v1 (Q3) —
  `GoalsView` es una pantalla de gestión independiente; `DashboardViewModel`/
  `DashboardView` no cambian en esta fase.
- **Edición de meta vía UI no implementada.** La especificación (R-1/S-2)
  describe una edición de monto/fecha/nombre desde `GoalsView` que nunca se
  construyó — solo crear/aportar/ver/borrar. Esta es una inconsistencia
  interna de la especificación heredada de la etapa de propuesta (que
  explícitamente excluyó la edición como "más allá de borrar y recrear"),
  detectada durante el diseño pero nunca reconciliada contra el texto de
  R-1/S-2. El checklist de Acceptance Criteria de la spec (el criterio real
  de "hecho") no exige edición vía UI y se cumple en su totalidad — el único
  criterio relacionado con edición está acotado a que
  `GoalRepository.UpdateAsync` persista correctamente (verificado por test
  de integración), no a un flujo de usuario. Ver
  `spec/features/phase-11/F-011-savings-goals.done.md` para el detalle
  completo. No bloqueante; queda como candidato para una fase futura.

**Estado actual (2026-09-20) — ✅ FASE COMPLETADA**
- ✅ `Goal`/`GoalContribution` (clases con invariantes + factories
  `Create`, esta última PÚBLICA a diferencia de `DebtPayment`), `GoalStatus`,
  `SavingsPlanCalculator` (servicio de dominio puro, redondeo hacia arriba a
  centavos — deliberadamente distinto de la convención de `BudgetEvaluator`,
  porque este valor prescribe un mínimo, no reporta un hecho observado).
- ✅ `GoalRepository`/`GoalContributionRepository` con el mismo patrón fijo
  de `BudgetRepository` para `UpdateAsync` (copia explícita de la navegación
  `OwnsOne`); FK con cascada de `GoalContributions` → `Goals` para que borrar
  una meta no deje aportes huérfanos.
- ✅ `ContributeToGoalUseCase`: la puerta de moneda (Q1/R-4) corre antes de
  cualquier persistencia; el constructor no tiene `IExpenseRepository`/
  `IIncomeRepository`/`ICurrencyConverter`, garantía verificable por firma.
- ✅ `GetGoalsQuery`: lee ambos repositorios directamente, delega toda la
  aritmética al dominio (`Goal.CalculateProgress`, `SavingsPlanCalculator`),
  sin recalcular sumas de forma independiente.
- ✅ `GoalsView`/`GoalsViewModel` — gestión de metas (crear/aportar/borrar);
  panel de aporte inline revelado al seleccionar una fila, no un modal ni
  una segunda ruta navegable.
- ✅ Gates: build limpio, 944/944 tests verdes (773 unit + 171 integration,
  subiendo desde 861/861 al cierre de Fase 10), `dotnet format style`
  limpio, NetArchTest 11/11.
- ✅ Aplicado en 4 commits encadenados (`10dc303` dominio, `5870967`
  infraestructura+migración, `98b634f` casos de uso, `f3d1400` UI de
  gestión), con re-verificación independiente de los 4 gates después de
  cada slice, no solo al final.
- ✅ SDD archivado: `spec/features/phase-11/F-011-savings-goals.done.md`.
- ⚠️ Gap documentado, no bloqueante: edición de meta vía UI (ver "Alcance
  ajustado" arriba).

---

### Fase 12 — Configuración: tema, idioma, IA (sin uso) ✅ COMPLETADA (2026-09-21)

**Objetivo**
Pantalla de configuración editable para tema, idioma y parámetros de IA
(proveedor, modelo, API key). La IA no se usa todavía — solo se guarda
su configuración.

**Justificación**
Esta fase precede al uso de la IA por una razón concreta: los prompts
y los mensajes de la IA deben estar localizados, y los cambios de
localización afectan a todo el código que muestra strings. Hacer el
i18n **antes** de la fase 13 evita tener que localizar dos veces. La
configuración de IA se guarda ya porque el usuario la meterá una vez y
no se le debería pedir otra al activar las features.

**Dependencias**
- Fases 0–11 (toda la app construida salvo IA y pulido).

**Entregables esperados**
- `AppSettings` POCO.
- `SettingsView` con secciones: Apariencia (tema), Idioma, IA (proveedor,
  modelo, API key, parámetros), Acerca de.
- `I18n` con recursos `.resx` para `es` y `en`.
- Tema claro y oscuro con detección del sistema.
- Persistencia de settings en la tabla `Setting`.
- Advertencia visible de que la API key se guarda en claro en v1.
- Tests de cambio de tema, cambio de idioma, persistencia de settings.

**Riesgos principales**
- Tematización de Avalonia (data templates vs styles, tokens de diseño).
- i18n en XAML (cómo se bindea a recursos sin acoplarse a un framework
  específico).
- API key en claro: hay que dejarlo claro en la UI y ADR.

**Criterios de fase completada**
- Cambiar tema funciona y persiste.
- Cambiar idioma traduce la UI completa y persiste.
- Guardar API key funciona, se persiste, sobrevive al reinicio.
- Una vez guardada la key, sigue sin haber **ninguna** llamada a la IA.

**Alcance ajustado respecto al plan original**:
- **Sin tema claro funcional ni detección del sistema.** Decisión confirmada: `Theme` se persiste
  (incluyendo `Light`) pero solo `Dark` se aplica de verdad a `RequestedThemeVariant` en v1;
  seleccionar `Light` muestra una nota localizada "aún no funcional", nunca un no-op silencioso. Sin
  `ThemeDictionaries` ni detección de tema del SO.
- **Sin traducción en vivo.** Decisión confirmada: guardar el idioma requiere reiniciar la app para que
  aplique — cada vista bindea su copy vía `{x:Static resources:Strings.X}`, que resuelve una sola vez
  al cargar el XAML y no puede re-bindearse en caliente. La UI muestra un aviso de "reinicio requerido"
  tras guardar, en vez de re-renderizar de inmediato.
- Ambos ajustes estaban ya en la spec original (R-3/R-4) como decisiones de producto confirmadas antes
  de diseñar, no descubiertos durante el desarrollo.

**Estado actual (2026-09-21) — ✅ FASE COMPLETADA**
- ✅ `Setting` (record, sin factory — sin consumidores previos fue seguro endurecer su validación in
  situ: `Key` no vacío y recortado, `MaxKeyLength=128`, `MaxValueLength=2048`); `AppSettings` (record,
  8 campos) + `Defaults`; `SettingKeys`; `SupportedLocales` (allowlist `es-DO`/`en`).
- ✅ `ISettingsStore`/`SettingsStore`: nunca lanza ni devuelve null (degrada a `Defaults` +
  `LogWarning`); todo campo numérico serializa/parsea con `CultureInfo.InvariantCulture` (evita que
  `AiTemperature` se corrompa entre locales con coma/punto decimal); enums por nombre, no ordinal.
- ✅ `GetAppSettingsQuery`, `UpdateThemeUseCase`, `UpdateLocaleUseCase`, `UpdateAiSettingsUseCase`
  (validar-todo-antes-de-escribir-todo; sin `HttpClient`/`ILLMClient` en el constructor, verificado por
  test de reflexión).
- ✅ `App.axaml.cs`: reemplazado el `new CultureInfo("es-DO")` fijo por una lectura de
  `ISettingsStore` al arrancar, con fallback a `es-DO` ante tabla vacía o `CultureNotFoundException`.
- ✅ `SettingsView`/`SettingsViewModel`: cuatro secciones (Apariencia, Idioma, IA, Acerca de), un
  comando de guardado por sección, sin ningún tipo de Avalonia en el ViewModel (testable sin harness
  headless). Advertencia de API key en texto plano siempre visible, no condicionada a si ya hay una key
  cargada.
- ✅ Gates: build limpio, 1033/1033 tests verdes (854 unit + 179 integration, subiendo desde 944/944 al
  cierre de Fase 11), `dotnet format style` limpio, NetArchTest 11/11.
- ✅ Aplicado en 5 commits encadenados de la fase (`3fc1a52` dominio+infra+migración, `34c890e` casos
  de uso, `9ef0922` lectura de cultura al arrancar, `8c86405` Apariencia+Idioma, `f2e08a8` IA+Acerca
  de), con re-verificación independiente de los gates durante `sdd-verify`.
- ✅ SDD archivado: `spec/features/phase-12/F-012-settings-i18n-ai-config.done.md`.
- ⚠️ Bug real, no relacionado con esta fase, encontrado y arreglado durante el smoke test manual: desde
  la Fase 6, `CurrentViewConverter.cs` — el converter que `MainWindow.axaml` realmente usa para
  resolver la vista activa, no `ViewLocator.cs` — nunca se actualizó con las rutas agregadas después
  (Deudas, Calendario, Presupuestos, Metas, y esta fase). Esas pantallas renderizaban un panel
  completamente negro en la app real pese a que las pruebas pasaban 1033/1033 — `ViewLocator` se
  mantuvo al día cada fase pero resultó ser código muerto para la navegación principal. Arreglado en el
  commit `4f8e423` (separado de los commits propios de esta fase): `CurrentViewConverter` ahora delega
  en `ViewLocator` en vez de mantener su propio switch paralelo. Ver
  `spec/features/phase-12/F-012-settings-i18n-ai-config.done.md` para el detalle completo.
- ⚠️ Gaps documentados, no bloqueantes: los escenarios de arranque con locale `en` persistido y con
  locale corrupto (tareas 3.3/3.4) quedan como verificación manual — no existe un harness de Avalonia
  headless en este repo todavía; `UpdateAiSettingsUseCase` hace seis escrituras no transaccionales,
  aceptado porque toda la validación corre antes de la primera escritura.

---

### Fase 13 — Pipeline de IA + categorización automática ✅ COMPLETADA (2026-09-23)

**Objetivo**
Implementar `ILLMClient` con su primer adapter, el factory por
proveedor, la resiliencia (Polly), y la primera feature de IA:
**categorización automática de gastos a partir del texto**.

**Justificación**
La categorización es la feature de IA más pequeña y la más valiosa
para el flujo del usuario (escribir "pizza 25" → categoría
"Comida"). Construir el pipeline completo de IA (cliente, factory,
settings ya leídos, resiliencia, fallback, logging con correlation id)
sobre una feature concreta y pequeña es más seguro que hacerlo sobre
una feature grande. Lo que se valida aquí se reutiliza en las fases 14,
15 y 16.

**Dependencias**
- Fase 12 (settings y i18n).

**Entregables esperados**
- `ILLMClient` y `LlmRequest` / `LlmResponse` (en `Application`).
- `OpenAICompatibleClient` (base, sirve para OpenAI, OpenRouter y
  custom).
- `AnthropicClient` (primer adapter paralelo).
- `ILLMClientFactory` que lee de settings.
- `CategorizeExpenseUseCase` que recibe texto y devuelve categoría
  sugerida.
- UI: al escribir la descripción, la IA sugiere categoría (con opción
  de aceptar/ignorar).
- `TestAiConnectionUseCase` (botón "Probar conexión" en Settings).
- Tests: del factory, del cliente con `HttpMessageHandler` mockeado, del
  use case, fallback cuando el proveedor falla.

**Riesgos principales**
- Parsing de respuestas JSON cuando el modelo gratuito no soporta JSON
  mode estricto.
- Latencia: el usuario está esperando; la llamada debe ser asíncrona y
  no bloquear la UI.
- Costos: aunque el modelo sea gratuito, los reintentos pueden sumar.
- Seguridad: la API key sale del equipo por primera vez — el usuario
  debe ser consciente y debe poder probar la conexión antes de usarla.

**Criterios de fase completada**
- ✅ Con una API key válida configurada, escribir "pizza 25" en un gasto
  sugiere automáticamente la categoría correcta. **Verificado contra una
  respuesta determinística de un `ILLMClient` fake ejercitado en las
  pruebas de los slices 5b/6 — no contra un modelo real.** El test externo
  opt-in contra OpenRouter (`CONTROL_FINANZAS_RUN_EXTERNAL_AI_TESTS=1`)
  prueba que el wire format funciona de punta a punta, no la calidad de la
  sugerencia de un modelo real; el tuning del prompt contra las respuestas
  logueadas (`RawModelAnswer`) queda como follow-up, no como bloqueante de
  esta fase.
- ✅ Sin API key o sin red, la sugerencia falla silenciosamente y la app
  sigue funcionando; la UI lo indica (`AiStatusMessage`: "IA no
  configurada" / "IA no disponible").
- ✅ El botón "Probar conexión" en Settings valida proveedor + key y
  distingue cuatro resultados: no configurado, credenciales inválidas, no
  disponible, y OK.

**Estado actual (2026-09-23) — ✅ FASE COMPLETADA**
- ✅ `Application/Abstractions/Llm/`: `ILLMClient`, `ILLMClientFactory`, `LlmRequest`/`LlmMessage`/
  `LlmResponse`, `LlmException`/`LlmFailureKind` (7 miembros, incluyendo `CircuitOpen`),
  `LlmConnectionOutcome`/`LlmConnectionStatus` (3 estados, desviación documentada de `Task<bool>`),
  `LlmClientResolution`/`LlmConfigurationStatus`, `IPromptProvider` — cero referencias a
  `System.Net.*`, verificado por `LlmContractPurityTests` (más estricto que NetArchTest) y por
  NetArchTest R1/R2/R4 sin modificar.
- ✅ `Infrastructure/Llm/`: `OpenAICompatibleClient` (OpenAI/OpenRouter/Custom, `Bearer` auth,
  `response_format` solo para `OpenAI`) y `AnthropicClient` (`x-api-key` + `anthropic-version`,
  `system` como campo top-level, no un mensaje `role:"system"`) — construidos por la factory, no por
  DI directa. `LLMClientFactory`: resolutor síncrono y puro, `MissingApiKey` verificado antes que
  `MissingBaseUrl` para cualquier proveedor. `LlmServiceCollectionExtensions.AddLlmClients`: 8
  `HttpClient`s nombrados (uno principal + uno de diagnóstico por cada uno de los 4 `AiProvider`), con
  los números de resiliencia exactos del diseño (15s timeout total, 6s por intento, 1 reintento
  exponencial jitterizado, breaker ratio 0.5/throughput 4/sampling 30s/break 15s en el principal; solo
  timeout en el de diagnóstico).
- ✅ `Application/Llm/LlmCategoryAnswerParser` (pipeline tolerante de 4 pasos) +
  `CategoryNameNormalizer` (dos niveles: exacto ordinal-insensible, luego diacríticos NFD-folded;
  ambigüedad → sin match) + `CategorizeExpenseUseCase` (temperatura forzada 0.0, `MaxTokens=min(setting,128)`,
  guard de 4/200 caracteres, correlation id, sin match es `Result.Success`).
- ✅ `ExpensesViewModel`: parámetro 17° de constructor opcional (kill switch), chip de sugerencia con
  token de generación + CTS por invocación para descartar respuestas supersedidas, `AcceptSuggestionCommand`
  como único punto de escritura de `SelectedCategoryId` desde IA.
- ✅ `TestAiConnectionUseCase` + botón "Probar conexión" en `SettingsView`, usando el pipeline de
  diagnóstico (sin retry/breaker) para que una key recién corregida nunca choque con un breaker abierto.
- ✅ Gates: build limpio, 1184/1184 tests verdes (936 unit + 248 integration, subiendo desde 1033/1033
  al cierre de Fase 12), re-verificado 3 veces consecutivas sin flakiness, `dotnet format style` limpio,
  NetArchTest 11/11.
- ✅ Aplicado en 9 commits encadenados (`389382a` contratos, `54f8743` adapter OpenAI+prompts,
  `d40b536` adapter Anthropic, `6e8fa63` factory+DI+resiliencia, `fedf209` parser tolerante, `fc6b5de`
  caso de uso de categorización, `71c213a` chip de sugerencia UI, `3c044f8` Probar conexión, `aa340dd`
  fix de un test de debounce flaky), con re-verificación independiente de los gates durante
  `sdd-verify`.
- ✅ SDD archivado: `spec/features/phase-13/F-013-ai-pipeline-categorization.done.md`.
- ⚠️ Gap no bloqueante: el test `RapidKeystrokes_WithinDebounceWindow_ProduceExactlyOneCall` fue
  genuinamente flaky con un debounce de 25ms y gaps de 20ms entre teclas simuladas — demasiado cerca de
  la resolución del temporizador de Windows (~15.6ms). Arreglado en `aa340dd` ampliando la ventana a
  400ms. **Patrón para futuros tests de timing en este repo**: dejar un margen de 10x+ sobre la
  resolución del temporizador del SO, no acercarse a él.

---

### Fase 14 — IA: insights, predicción de fin de mes, anomalías

**Objetivo**
Las tres features restantes de IA orientadas al dashboard: insights
personalizados, predicción de cierre de mes y explicación de anomalías
(sobre la base del detector determinista de la fase 11 / futuras).

**Justificación**
Mismo pipeline de IA, distintas features. Cada una reutiliza
`ILLMClient` y los use cases siguen el patrón de la fase 13. Se
agrupan aquí porque se muestran juntas en el dashboard.

**Dependencias**
- Fase 13 (pipeline de IA) + fase 8 (dashboard) + fase 10
  (presupuestos, para anomalías vs presupuesto).

**Entregables esperados**
- `GetInsightsUseCase`: resumen del mes en lenguaje natural.
- `GetMonthEndForecastUseCase`: proyección basada en histórico.
- `GetAnomalyNarrativeUseCase`: el `AnomalyDetector` determinista detecta
  la anomalía; la IA escribe el mensaje.
- UI: tarjeta de insights en el dashboard, sección de predicción,
  lista de anomalías.
- Tests: use cases con `ILLMClient` mockeado, fallback a mensaje
  genérico si la IA falla.

**Riesgos principales**
- Calidad de los prompts (revisarlos a mano en cada proveedor).
- Tiempo de respuesta: tres llamadas de IA en la carga del dashboard
  pueden sumar; considerar paralelismo y caché.
- Coste de tokens con prompts largos.

**Criterios de fase completada**
- El dashboard muestra insights, forecast y anomalías cuando hay
  suficiente histórico.
- Si la IA no responde, el dashboard muestra "IA no disponible" sin
  perder los datos numéricos.

---

### Fase 15 — Chat con IA sobre tus finanzas

**Objetivo**
Vista de chat donde el usuario pregunta cosas como "¿cuánto gasté en
restaurantes este mes?" y la IA responde con datos reales.

**Justificación**
Es la feature de IA más ambiciosa: requiere construir contexto a partir
de los datos locales en cada pregunta. Se hace al final del bloque de
IA porque depende de que todas las features anteriores estén
implementadas y los datos sean ricos.

**Dependencias**
- Fase 14 (pipeline maduro) + todas las anteriores (datos ricos para
  responder).

**Entregables esperados**
- `AskFinanceChatUseCase` que arma el contexto (resumen del mes,
  top categorías, deudas activas, metas, presupuestos) y lo pasa como
  system prompt a la IA.
- `ChatView` con historial de la conversación, scroll, copy del
  mensaje.
- Streaming opcional (nice-to-have, no requerido en v1).
- Tests: use case con `ILLMClient` mockeado, construcción de contexto
  con datos sintéticos.

**Riesgos principales**
- Tamaño del contexto: budgets + 12 meses de gastos pueden exceder
  límites; hay que resumir o filtrar.
- Privacidad: el system prompt viaja a la API; documentar
  explícitamente.
- Alucinaciones: la IA puede inventar cifras; mostrar siempre las
  cifras exactas en la UI como apoyo.

**Criterios de fase completada**
- Preguntar "¿cuánto gasté en comida este mes?" devuelve una respuesta
  con la cifra correcta basada en los datos locales.

---

### Fase 16 — Plan de ahorro con IA para metas

**Objetivo**
Que cada meta pueda pedir un plan de ahorro **explicado** por la IA,
sobre la cifra determinista calculada en la fase 11.

**Justificación**
Es la feature de IA más ligera que queda pendiente. Es el "yapa" del
sistema de metas: ya tenemos el cálculo, ahora lo humanizamos. Hacerla
separada de la fase 11 permite que la fase 11 quede firme sin
depender de la IA.

**Dependencias**
- Fase 11 (metas y calculador determinista) + fase 13 (pipeline de IA).

**Entregables esperados**
- `GenerateSavingsPlanUseCase` que llama al `SavingsPlanCalculator` y
  pasa el resultado a la IA para producir una explicación motivadora.
- UI: botón "Pedir plan con IA" en la meta; muestra cifra determinista
  + texto generado.
- Tests: use case con mock de `ILLMClient`, fallback a solo cifra
  determinista si la IA falla.

**Riesgos principales**
- La IA puede contradecir la cifra determinista si el prompt no es
  claro. El prompt debe recibir la cifra y solo pedir "explica y
  motiva".

**Criterios de fase completada**
- Cada meta muestra un plan con cifra correcta y una explicación
  legible.

---

### Fase 17 — Notificaciones

**Objetivo**
Avisos in-app (y opcionalmente del sistema) por: pagos próximos,
presupuestos cerca del límite, resumen diario/semanal, anomalías.

**Justificación**
Requiere que las features que disparan las notificaciones estén
estables (deudas, suscripciones, presupuestos, anomalías). Por eso va
después de las fases 7, 10, 14. Se hace antes que backup/import para
no dejar al usuario sin avisos mientras se hacen las features de
datos.

**Dependencias**
- Fases 7 (deudas), 10 (presupuestos), 14 (anomalías).

**Entregables esperados**
- Servicio de notificaciones in-app (bandeja en el shell).
- Reglas: cuota de deuda en X días, suscripción a cobrarse, presupuesto
  al 80% / 100%, anomalía detectada, resumen diario/semanal.
- Configuración del usuario para activar/desactivar cada tipo.
- (Opcional) integración con notificaciones del SO.
- Tests: cada regla con datos sintéticos.

**Riesgos principales**
- Cuándo evaluar las reglas (al arrancar, periódicamente, idle).
- APIs de notificación del SO: cross-platform en Avalonia puede ser
  limitado; empezar por in-app.
- Ruido: si el usuario tiene muchas suscripciones, el resumen puede
  ser largo.

**Criterios de fase completada**
- Al abrir la app, la bandeja muestra avisos relevantes del día.
- Las alertas de presupuesto se disparan al pasar el 80% / 100%.
- El usuario puede silenciar tipos de aviso.

---

### Fase 18 — Exportación a PDF y Excel

**Objetivo**
Exportar el reporte actual (dashboard, vista de gastos, vista de
deudas) a PDF y a Excel/CSV.

**Justificación**
Necesita que las visualizaciones y los datos estén estables (fase 8).
Se hace antes que backup porque la exportación de reporte es una
feature visible que el usuario pide; el backup, aunque crítico, no es
una feature "de producto" en el mismo sentido.

**Dependencias**
- Fases 4, 5, 8 (datos, multi-moneda, visualizaciones).

**Entregables esperados**
- `PdfExporter` y `ExcelExporter` (o `CsvExporter` según decisión).
- Botón "Exportar" en el dashboard y en vistas principales.
- Decisión de librería PDF cross-platform (QuestPDF es buen candidato).
- Tests: que el archivo generado sea válido y contenga los datos
  esperados.

**Riesgos principales**
- Librería PDF: opciones cross-platform limitadas, hay que evaluar.
- Localización del PDF (es-DO por defecto).
- Layouts: la complejidad del layout puede ser alta; empezar por
  plantillas simples (resumen del mes).

**Criterios de fase completada**
- Exportar el mes actual a PDF y a Excel produce archivos válidos con
  los datos correctos.

---

### Fase 19 — Backup y restauración

**Objetivo**
Botón "Exportar todo a un archivo" y "Restaurar desde archivo" en
Settings, con el formato ZIP+manifest definido en `architecture.md`.

**Justificación**
Crítico para la confianza del usuario (puede perder su PC y recuperar
todo). Se hace **después** de que la mayoría de features estén
estables para evitar hacer backup de un schema que va a cambiar mucho
en la semana siguiente. Se hace antes que CSV import porque comparte
la UX de "importar archivo".

**Dependencias**
- Fases 2, 4, 5, 6, 7 (schema razonablemente estable).

**Entregables esperados**
- `BackupService` con export/import.
- Manifiesto con versión de schema.
- UI: dos botones en Settings, diálogo de progreso, validación al
  importar.
- Rechazo de backups de versión más nueva (sin downgrade).
- Tests de roundtrip.

**Riesgos principales**
- Validación al importar: archivo corrupto, schema incompatible, datos
  inconsistentes.
- Versión de schema: cómo evolucionarla sin romper backups viejos.
- Tamaño: un ZIP de SQLite no es enorme, pero hay que medir.

**Criterios de fase completada**
- Export, instalar app fresca, import, todos los datos aparecen
  idénticos.
- Un backup de versión futura es rechazado con mensaje claro.

---

### Fase 20 — Importación CSV / Excel

**Objetivo**
Wizard de import con mapeo configurable de columnas y perfiles
guardados.

**Justificación**
Necesita el CRUD de transacciones (fase 4) y la experiencia de
"importar archivo" de la fase 19. Se hace al final porque es una
feature opcional para usuarios que vienen de otra app; no bloquea el
uso principal.

**Dependencias**
- Fases 4, 19.

**Entregables esperados**
- `CsvImporter` con parser configurable.
- `CsvImportProfile` con mapeo de columnas + formato de fecha +
  separador decimal.
- `ImportView` con preview, mapeo, confirmación.
- Todo en una transacción: si falla, rollback.
- Tests con CSVs sintéticos (varios formatos).

**Riesgos principales**
- UX del mapeo de columnas: que sea clara sin ser tediosa.
- Deduplicación: si el usuario importa el mismo CSV dos veces, ¿se
  duplican los gastos? Decisión a fijar.
- Codificaciones (UTF-8, Latin-1) y separadores (`,` vs `;`).

**Criterios de fase completada**
- Importar un CSV genérico con configuración de columnas produce
  gastos válidos, sin duplicar, en una transacción.

---

### Fase 21 — Empaquetado, pulido, accesibilidad

**Objetivo**
Publicar la app como binarios self-contained por SO, pulir
experiencias (empty states, errores, atajos de teclado) y verificar
accesibilidad básica.

**Justificación**
El pulido se hace al final porque depende del conjunto de pantallas
que existan. Hacerlo antes implicaría rehacerlo cada vez que cambia
una pantalla.

**Dependencias**
- Todas las anteriores.

**Entregables esperados**
- Build self-contained para `win-x64`, `osx-arm64`, `osx-x64`,
  `linux-x64`.
- Instaladores o paquetes por SO (decisión en la Specification: MSIX /
  DMG / AppImage en v1; Flatpak / Snap como siguientes pasos).
- Empty states en todas las listas.
- Atajos de teclado (Ctrl+N = nuevo gasto, etc.).
- Diálogos de error con correlation id visibles.
- Navegación completa con teclado.
- Screen reader labels (al menos los críticos).
- README y CHANGELOG actualizados.

**Riesgos principales**
- Particularidades de cada SO (notificaciones, paths, instaladores).
- Tiempo de empaquetado y firma (firma de macOS requiere developer
  account).
- Accesibilidad: Avalonia tiene soporte limitado; documentar lo que
  sí y lo que no.

**Criterios de fase completada**
- App instalable en Win/Mac/Linux sin necesidad de .NET preinstalado.
- Flujo principal (registrar gasto) funciona 100% con teclado.
- Empty states visibles en todas las secciones.
- Versión etiquetada lista para distribuir.

---

## 4. Grafo de dependencias entre fases

```
0 ──▶ 1 ──▶ 2 ──▶ 3 ──▶ 4 ──┬──▶ 5 ──┐
                              │         │
                              ├──▶ 6 ──▶ 7
                              │         │
                              │         ▼
                              │   ┌─── 8 ──┬──▶ 9
                              │   │         │
                              │   │         ├──▶ 10 ──▶ 11
                              │   │         │
                              │   └─────────┴──▶ 12 ──▶ 13 ──▶ 14 ──┬──▶ 15
                              │                                       │
                              │                                       └──▶ 16
                              │
                              ├──▶ 17 (deps: 7, 10, 14)
                              ├──▶ 18 (deps: 4, 5, 8)
                              ├──▶ 19 (deps: 2, 4, 5, 6, 7)
                              └──▶ 20 (deps: 4, 19)
                                        │
                                      21 (todas)
```

Las flechas son "depende de". Trabajo posible en paralelo
(entre fases que no comparten flecha): ver §6.

---

## 5. MVP y límites

**MVP funcional sin IA = fases 0 a 12.** La app ya es usable para
control personal con: gastos, ingresos, deudas, recurrentes,
presupuestos, metas (con cálculo determinista), filtros, vistas
guardadas, multi-moneda, tema, idioma, backup/restore.

**MVP completo = fases 0 a 21.** Incluye IA y pulido.

**Límites explícitos fuera del MVP:**
- Aporte automático a metas desde "ingreso no gastado" del mes
  (definido en vision pero diferido por complejidad).
- Cifrado en reposo de la API key (ADR-011, pendiente para v2).
- Auto-update de la app (ADR-012, pendiente para v2).
- Multi-proceso / lock file (ADR-013, no necesario en v1).
- Notificaciones nativas del SO (empezamos in-app, del SO como v2).

---

## 6. Trabajo en paralelo posible

No todas las fases son secuenciales. Estas se pueden solapar sin riesgo
siempre que las dependencias estén terminadas:

- **Fase 5 → Fase 6** — Phase 6 se ejecuta después de multi-moneda porque
  las suscripciones y gastos fijos reutilizan `Money`, `Currency` y
  `PaymentMethodId`. Se elimina la alternativa paralela que contradice el
  contrato de dependencias de Phase 6.
- **Fase 8 (visualizaciones) y fase 10 (presupuestos)** — los
  presupuestos pueden desarrollarse en paralelo si se acuerda el
  contrato del evaluador desde el inicio.
- **Fase 11 (metas) en paralelo con fase 9 (calendario)** — no
  comparten código.
- **Fase 17 (notificaciones) en paralelo con fase 18 (export)** — no
  comparten código; se pueden hacer por personas distintas.
- **Fase 13 (IA) secuencial estricto** — la fase 14, 15 y 16 no pueden
  empezar hasta que 13 esté verde.

Recomendación: incluso con paralelismo, **una persona sola** debe
mantener el orden estricto. El paralelismo está pensado para equipos
de 2+.

---

## 7. Riesgos globales del roadmap

- **Scope creep:** la visión es ambiciosa (20+ fases). Cualquier feature
  que aparezca "porque sí" debe evaluarse contra los no-goals de
  `vision.md` antes de abrirse como fase nueva.
- **IA siendo bloqueante:** si la API de un proveedor cambia o los
  modelos gratuitos se degradan, las fases 13–16 pueden frenarse. El
  pipeline está pensado para ser tolerante, pero la calidad puede
  bajar.
- **Cambio de stack a mitad de camino:** cambiar Avalonia por otro
  framework después de la fase 3 es muy caro. La fase 1 valida esto,
  pero el riesgo está ahí.
- **Empaquetado cross-platform (fase 21):** es la fase con más
  particularidades por SO. Reservar tiempo extra.

---

## 8. Referencias

- `docs/vision.md` — qué se construye (alcance, no-goals).
- `docs/architecture.md` — cómo se construye (capas, contratos, ADRs).
- `spec/constitution/mission.md` — propósito ejecutivo.
- `spec/constitution/tech-stack.md` — stack y convenciones.
- `spec/constitution/features/` — features individuales (se poblará
  fase por fase, una Specification por fase).

---

*Last reviewed: 2026-09-17.*
