# Tasks — F-009 Vista de calendario de gastos

## Revisión de entrega

| Field | Value |
|---|---|
| Estimated changed lines | 800–1100 |
| 400-line budget risk | Medium (overall); slice 2 alone likely stays under 400 |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 (capa de datos) → PR 2 (navegación) → PR 3 (UI del calendario) |
| Delivery strategy | ask-on-risk |
| Chain strategy | stacked-to-main |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: Medium

A diferencia de Fase 8 (donde el corte en 3 PRs respondía a tamaño + riesgo
de renderizado de un paquete externo nuevo), acá el tamaño total no es
alarmante por sí solo: no hay paquete nuevo, no hay migración, y la
superficie es más chica que la de Fase 8. La razón para encadenar es
cualitativa, no de volumen: la navegación parametrizada (PR 2) es la
**primera vez en este proyecto que un cambio SDD modifica una interfaz
compartida ya usada por las 5 rutas existentes** (`INavigationService`), en
vez de solo agregar archivos nuevos. Aislarla en su propio PR — con su
propia suite probando las 5 rutas sin regresión (S-7) — permite revisar y,
si hace falta, revertir ese cambio de forma independiente de la capa de
datos (bajo riesgo, aditiva, sin UI) y de la UI del calendario (consume
ambas). Las 3 unidades son aditivas y mergeables a `main` en secuencia: PR
1 y PR 2 no rompen nada por sí solos (no hay consumidores todavía), PR 3
depende de ambos. Un solo apply (como Fase 6/7) sería razonable si el
cambio de interfaz no existiera; con él presente, la recomendación es
encadenar.

## 1. Capa de datos

Sin UI, bajo riesgo.

- [x] 1.1 Crear `DayCurrencyTotal` en `Application/DTOs/Aggregates/DayCurrencyTotal.cs` (`DateOnly Date, Currency Currency, decimal Amount`). (R-5)
  Evidencia: compila junto con `GetCalendarMonthQueryTests` (usa el record vía `IReadOnlyList<DayCurrencyTotal>`).
- [x] 1.2 Agregar `GetDailyTotalsAsync(DateRange, CancellationToken)` a `IExpenseRepository`. (R-5)
  Evidencia: `CalendarAggregateQueryTests` compila e invoca `repo.GetDailyTotalsAsync(period)` contra la interfaz.
- [x] 1.3 Implementar en `ExpenseRepository`: `GroupBy(Date, Currency)`, `Sum` con cast a `double`/`decimal` (patrón D-1), `Math.Round(_, 4)` post-materialización — **obligatorio**, evita el bug de precisión de Fase 8. (R-5)
  Evidencia: `CalendarAggregateQueryTests.GetDailyTotalsAsync_ManyFractionalCentsSameDay_SumsExactlyDespiteFloatingPointPath` (23x 0.1m DOP en el mismo día → exactamente 2.3m).
- [x] 1.4 Actualizar `FakeExpenseRepository` con el método nuevo (agrupación en memoria). (test-only)
  Evidencia: usado por `GetCalendarMonthQueryTests` (todos los fixtures pasan `FakeExpenseRepository`).
- [x] 1.5 Crear `CalendarGrid` en `Application/Common/CalendarGrid.cs`: `CellCount = 42`, `LeadingOffset`, `GridStart`, `Dates` — estático, puro, sin dependencias (D-2). (R-1, R-3)
  Evidencia: `CalendarGridTests` (13 tests, todos en verde).
- [x] 1.6 Tests unitarios de `CalendarGrid`: offset inicial para cada `DayOfWeek`; mes que arranca justo en `firstDayOfWeek` (offset 0); mes de 31 días; febrero bisiesto/no bisiesto; siempre exactamente 42 fechas contiguas. (R-3, S-5)
  Evidencia: `CalendarGridTests.LeadingOffset_EveryFirstDayOfWeek_ReturnsExpectedOffset` (7 casos), `LeadingOffset_MonthStartsExactlyOnFirstDayOfWeek_ReturnsZero`, `GridStart_MonthStartsExactlyOnFirstDayOfWeek_EqualsMonthAnchor`, `Dates_ThirtyOneDayMonth_ContainsFirstAndLastDayOfMonth`, `Dates_LeapYearFebruary_ContainsTwentyNinth`, `Dates_NonLeapYearFebruary_DoesNotContainTwentyNinth`, `Dates_AnyFirstDayOfWeek_ReturnsExactlyFortyTwoContiguousDates` (7 casos).
- [x] 1.7 Crear `CalendarDay`/`CalendarMonthResult` en `Application/UseCases/Calendar/GetCalendarMonth/CalendarMonthResult.cs` (`TotalDop` nullable: `0m` día actual sin gastos, `null` día adyacente). (R-2, D-3)
  Evidencia: `GetCalendarMonthQueryTests.ExecuteAsync_CurrentMonthDayWithNoExpenses_ReturnsZeroTotal` y `ExecuteAsync_AdjacentMonthDay_ReturnsNullTotal`.
- [x] 1.8 Crear `GetCalendarMonthQuery`: une `CalendarGrid.Dates` con `GetDailyTotalsAsync`, normaliza a DOP vía `ICurrencyConverter.ConvertAsync` (no `DisplayAsync`), marca `HasMissingRates` sin lanzar en fallo de conversión. (R-1, R-2, R-5)
  Evidencia: `GetCalendarMonthQueryTests.ExecuteAsync_MultiCurrencyDay_CollapsesToSingleDopTotal` y `ExecuteAsync_FxConversionFails_SetsHasMissingRatesWithoutThrowing`.
- [x] 1.9 Tests unitarios de `GetCalendarMonthQuery` con fakes: multi-moneda colapsa a un total DOP por día (S-1); día actual sin gastos → `TotalDop == 0m`; día adyacente → `TotalDop == null`; fallo de conversión → `HasMissingRates=true`, sin throw; exactamente un `IsToday`. (S-1, S-3, S-4)
  Evidencia: `GetCalendarMonthQueryTests` (6 tests, todos en verde): `ExecuteAsync_MultiCurrencyDay_CollapsesToSingleDopTotal`, `ExecuteAsync_CurrentMonthDayWithNoExpenses_ReturnsZeroTotal`, `ExecuteAsync_AdjacentMonthDay_ReturnsNullTotal`, `ExecuteAsync_FxConversionFails_SetsHasMissingRatesWithoutThrowing`, `ExecuteAsync_TodayInRange_ExactlyOneDayIsToday`, `ExecuteAsync_AlwaysReturnsFortyTwoDays`.
- [x] 1.10 Tests de integración de `GetDailyTotalsAsync` contra SQLite real (mismo harness que `DashboardAggregateQueryTests`): una fila por (día, moneda), bordes de rango inclusivos, montos redondeados a 4 decimales, sin `NotSupportedException` en el `Sum(decimal)`. (R-5)
  Evidencia: `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/CalendarAggregateQueryTests.cs` (5 tests, todos en verde): `GetDailyTotalsAsync_MultiCurrencySameDay_ReturnsOneRowPerCurrency`, `GetDailyTotalsAsync_DifferentDays_ReturnsOneRowPerDay`, `GetDailyTotalsAsync_RangeBoundaries_AreInclusive`, `GetDailyTotalsAsync_EmptyPeriod_ReturnsEmptyList`, `GetDailyTotalsAsync_ManyFractionalCentsSameDay_SumsExactlyDespiteFloatingPointPath`.

## 2. Navegación parametrizada

Riesgo concentrado — toca una interfaz compartida por las 5 rutas existentes.

- [x] 2.1 Agregar el overload tipado a `INavigationService`: `void NavigateTo<TViewModel>(string routeName, Action<TViewModel> configure) where TViewModel : ViewModelBase`. (R-6)
  Evidencia: `src/ControlDeFinanzasIA.UI/Navigation/INavigationService.cs` — firma agregada sin tocar `NavigateTo(string)`; compila junto con `NavigationServiceTests`.
- [x] 2.2 Refactorizar `NavigationService`: extraer `NavigateCore(string, Action<ViewModelBase>?)`; `NavigateTo(string)` pasa a `=> NavigateCore(routeName, null)`; `configure` corre después de `GetRequiredService` y antes de `_history.Push`/`CurrentViewModel =`/`OnCurrentChanged()`; overload valida ruta existente y `typeof(TViewModel).IsAssignableFrom(_routes[routeName])`, lanza `InvalidOperationException` en mismatch. (R-6, D-4)
  Evidencia: `src/ControlDeFinanzasIA.UI/Navigation/NavigationService.cs` — `NavigateCore` privado es ahora el único punto de push/set/fire; `NavigateTo(string)` es one-liner; el overload tipado valida ruta+tipo antes de delegar. `FakeNavigationService` (test double) actualizado en paralelo para no romper el build (único otro implementador de `INavigationService`).
- [x] 2.3 Tests de `NavigationService`: las 5 rutas existentes sin cambio de comportamiento (S-7); overload tipado ejecuta `configure` antes de `CurrentViewModel`/`CurrentChanged`; ruta desconocida lanza; mismatch de tipo lanza. (S-7)
  Evidencia: `tests/ControlDeFinanzasIA.Tests.Unit/UI/NavigationServiceTests.cs` — 5 tests preexistentes en verde sin modificación (`NavigateTo_Dashboard_SetsCurrentAndPushesHistory`, `NavigateTo_UnknownRoute_ThrowsInvalidOperationException`, `GoBack_WithoutHistory_IsSilentNoop`, `CurrentChanged_FiresOnEveryNavigation`, `NavigateTo_Twice_ThenGoBack_RestoresPreviousCurrent`) + 4 tests nuevos: `NavigateToTyped_ConfigureRunsBeforeCurrentViewModelChanges_AndBeforeCurrentChanged`, `NavigateToTyped_UnknownRoute_ThrowsInvalidOperationException`, `NavigateToTyped_RouteTypeMismatch_ThrowsInvalidOperationException`, `NavigateToTyped_ValidRoute_PushesHistoryAndSupportsGoBack`.
- [x] 2.4 Agregar `ExpensesViewModel.ApplyDayFilter(DateOnly day)`: setea `FilterDateFrom`/`FilterDateTo` y ejecuta `ApplyFilterCommand` explícitamente (setear las propiedades solo no dispara recarga). (R-4, D-4)
  Evidencia: `src/ControlDeFinanzasIA.UI/ViewModels/ExpensesViewModel.cs` — método público nuevo; confirmado que no existen `OnFilterDateFromChanged`/`OnFilterDateToChanged` antes de escribirlo.
- [x] 2.5 Agregar generation guard a `ExpensesViewModel.RefreshAsync` (`_refreshGeneration`, incrementar al inicio, descartar mutación de estado si la generación cambió durante el `await`) — necesario porque la carga del constructor y la de `ApplyDayFilter` quedan en vuelo simultáneamente. (D-4)
  Evidencia: `src/ControlDeFinanzasIA.UI/ViewModels/ExpensesViewModel.cs` — `_refreshGeneration` chequeado una vez, después de ambos `await` (`_getTransactionsQuery.ExecuteAsync` y `EnrichWithConversionAsync`), antes de mutar `Expenses`/`TotalPages`/`FormError` en cualquiera de las dos ramas (éxito/error).
- [x] 2.6 Tests de `ExpensesViewModel`: `ApplyDayFilter` deja `CurrentFilter.DateFrom == DateTo == day` de forma síncrona antes del primer `await`; un refresh superado por uno más nuevo es descartado por el guard sin pisar el resultado correcto. (R-4)
  Evidencia: `tests/ControlDeFinanzasIA.Tests.Unit/UI/ExpensesViewModelTests.cs` — `ApplyDayFilter_SetsCurrentFilterToSingleDay_Synchronously`, `ApplyDayFilter_RefreshesListForThatDay`, y `Refresh_SupersededBySecondCall_DiscardsStaleResultViaGenerationGuard` (usa el nuevo test double `SequencedExpenseRepository` para forzar que la llamada más nueva complete primero y la más vieja complete después, y verifica que la vieja no pisa el estado).

## 3. UI del calendario

Depende de 1 y 2.

- [x] 3.1 Crear `CalendarCell` en `UI/ViewModels/CalendarCell.cs` (`Date, DayNumber, TotalDisplay, IsCurrentMonth, IsToday`).
  Evidencia: `src/ControlDeFinanzasIA.UI/ViewModels/CalendarCell.cs` — record con los 5 campos exactos del design (D-6).
- [x] 3.2 Crear `CalendarViewModel`: ctor (`GetCalendarMonthQuery, INavigationService, IClock, ILogger`), `SelectedMonth`/`SelectedMonthLabel` (formato `MMMM yyyy` vía `CultureInfo.CurrentCulture`, patrón `DashboardViewModel.FormatMonthLabel`), `WeekdayHeaders` rotados a `FirstDayOfWeek`, `Cells` (42 tras cada load), comandos `PreviousMonthAsync`/`NextMonthAsync`/`SelectDayAsync`. (R-1, R-2, R-3, R-7)
  Evidencia: `src/ControlDeFinanzasIA.UI/ViewModels/CalendarViewModel.cs` — ctor idéntico en forma al de `DashboardViewModel` (mismo patrón de `ArgumentNullException.ThrowIfNull` + `_ = LoadAsync()`); `CalendarViewModelTests.Constructor_LoadsCurrentMonth_PopulatesFortyTwoCells` confirma 42 celdas tras construcción.
- [x] 3.3 Implementar `SelectDayAsync`: día de mes adyacente → `SelectedMonth = Anchor(cell.Date)` + recarga, sin navegar (R-3/S-6); día del mes actual → `INavigationService.NavigateTo<ExpensesViewModel>(RouteNames.Expenses, vm => vm.ApplyDayFilter(cell.Date))` (R-4).
  Evidencia: `CalendarViewModel.SelectDayAsync` — bifurca exactamente por `cell.IsCurrentMonth`, sin rama intermedia; confirmado por `CalendarViewModelTests.SelectDayCommand_CurrentMonthDay_NavigatesToExpensesFilteredToThatDay` y `SelectDayCommand_AdjacentMonthDay_NavigatesCalendarGrid_DoesNotDrillDown`.
- [x] 3.4 Tests de `CalendarViewModel`: 42 `Cells` tras load; prev/next recalculan la grilla completa (S-2); click en día del mes actual invoca el overload tipado con filtro correcto (S-3/S-4); click en día adyacente cambia `SelectedMonth` y NO navega (S-6).
  Evidencia: `tests/ControlDeFinanzasIA.Tests.Unit/UI/CalendarViewModelTests.cs` (6 tests, todos en verde): `Constructor_LoadsCurrentMonth_PopulatesFortyTwoCells`, `PreviousMonthCommand_RecomputesWholeGridForPreviousMonth`, `NextMonthCommand_RecomputesWholeGridForNextMonth`, `SelectDayCommand_CurrentMonthDay_NavigatesToExpensesFilteredToThatDay`, `SelectDayCommand_AdjacentMonthDay_NavigatesCalendarGrid_DoesNotDrillDown`, `SelectDayCommand_NullCell_IsNoOp`.
- [x] 3.5 Agregar `RouteNames.Calendar = "Calendar"`.
  Evidencia: `src/ControlDeFinanzasIA.UI/Navigation/RouteNames.cs` — constante agregada siguiendo el patrón de `Debts`; compila junto con `NavigationService` y `CalendarViewModelTests`.
- [x] 3.6 Registrar `CalendarViewModel → CalendarView` en `ViewLocator.Build` y `.Match`, sobre el archivo ya corregido (commit `e0e51d3` agregó el caso `DebtsViewModel`) — no reintroducir la omisión.
  Evidencia: `src/ControlDeFinanzasIA.UI/Views/ViewLocator.cs` — caso agregado en `Build` **y** en `Match`, sin tocar el caso `DebtsViewModel` ya presente.
- [x] 3.7 Agregar entrada de sidebar en `ShellViewModel` (`RouteMenuItem(RouteNames.Calendar, "Calendario")`) y ruta en el mapa de `NavigationService`.
  Evidencia: `src/ControlDeFinanzasIA.UI/ViewModels/ShellViewModel.cs` (entrada agregada al arreglo `MenuItems`, mismo patrón hardcoded que `"Recurrentes"`/`"Deudas"`) y `src/ControlDeFinanzasIA.UI/Navigation/NavigationService.cs` (`_routes` incluye `{ RouteNames.Calendar, typeof(CalendarViewModel) }`).
- [x] 3.8 Registrar en `Bootstrap.cs`: `AddTransient<GetCalendarMonthQuery>()`, `AddTransient<CalendarViewModel>()`.
  Evidencia: `src/ControlDeFinanzasIA.UI/Composition/Bootstrap.cs` — ambas líneas agregadas junto a los registros de Fase 8/Debts; `dotnet build -c Release` confirma resolución de dependencias.
- [x] 3.9 Extraer `MoneyFormat.ApproximateDop` a `UI/Formatting/MoneyFormat.cs`; `DashboardViewModel.FormatDop` delega a él (D-5, two-line touch, sin cambio de comportamiento).
  Evidencia: `src/ControlDeFinanzasIA.UI/Formatting/MoneyFormat.cs` (nuevo); `DashboardViewModel.FormatDop` es ahora `=> MoneyFormat.ApproximateDop(amount)` — mismo cuerpo (`Strings.ApproximateFormat` + `CultureInfo.CurrentCulture`), sin cambio de comportamiento; los 647 tests unitarios existentes (incluidos los de Dashboard) siguen en verde.
- [x] 3.10 Crear `CalendarView.axaml(.cs)`: header con mes navegado, comandos prev/next, fila de encabezados de día, host de `MonthGrid`. (R-1, R-7)
  Evidencia: `src/ControlDeFinanzasIA.UI/Views/CalendarView.axaml(.cs)` — header con `SelectedMonthLabel` + `PreviousMonthCommand`/`NextMonthCommand`, fila `WeekdayHeaders` sobre `UniformGrid Rows="1" Columns="7"`, host `<calendar:MonthGrid Cells="{Binding Cells}" DayCommand="{Binding SelectDayCommand}" .../>`.
- [x] 3.11 Crear `MonthGrid.axaml(.cs)` (`UserControl`): `ItemsControl` sobre `UniformGrid Rows="6" Columns="7"` con **un** `ItemTemplate` compartido (no 42 celdas manuales); `Button` con `Command`/`CommandParameter`; clases `adjacent`/`today` vía binding a `IsCurrentMonth`/`IsToday`. (R-3, D-6)
  Evidencia: `src/ControlDeFinanzasIA.UI/Views/Calendar/MonthGrid.axaml(.cs)` — un único `DataTemplate x:DataType="vm:CalendarCell"` reutilizado por el `ItemsControl`, `ItemsPanel` de `UniformGrid Rows="6" Columns="7"`, `Classes.adjacent="{Binding !IsCurrentMonth}"`/`Classes.today="{Binding IsToday}"`; estilos `Button.calendar-day(.adjacent|.today)` agregados a `Styles/LedgerStyles.axaml`. Tres `StyledProperty` únicamente (`Cells`, `DayCommand`, `EmptyText`), sin lógica adicional en el code-behind.
- [x] 3.12 Localización: agregar claves de UI en `Strings.resx`/`Strings.es-DO.resx` (título, estado vacío, error de carga); nombres de mes/día ya vienen de `CultureInfo.CurrentCulture`, no van al `.resx`. (R-7)
  Evidencia: `CalendarTitle`, `CalendarEmptyMessage`, `CalendarLoadErrorMessage` agregadas a `Strings.resx` y `Strings.es-DO.resx`, con accesores manuales agregados a mano en `Strings.Designer.cs` (el build no lo regenera — hallazgo de Fase 8).

## 4. Cierre

- [ ] 4.1 Ejecutar build, `dotnet test` (unit + integración), `dotnet format style --verify-no-changes`, `ArchitectureTests`/NetArchTest.
- [ ] 4.2 Corregir `docs/architecture.md` §10 (líneas 324-326): reemplazar por el texto exacto del design (nuevo overload documentado, eliminar la línea obsoleta de `NavigateTo<ExpenseDetailViewModel>(id)` — ese view-model no existe en `src/`).
- [ ] 4.3 Crear `spec/phases/phase-09/features.md` siguiendo el patrón de `spec/phases/phase-06/` y `phase-07/`.
- [ ] 4.4 Archivar la spec (`F-009-calendar-view.spec.md` → `.done.md`) y escribir `verify-report.md`/`archive-report.md` en `.atl/sdd/f-009p9-calendar-view/` — dejar sin marcar, paso separado después de apply + verify.
