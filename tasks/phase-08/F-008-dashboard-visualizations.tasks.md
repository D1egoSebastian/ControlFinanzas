# Tasks — F-008 Dashboard y visualizaciones base

## Revisión de entrega

Estimated changed lines: 2200–3000. Larger surface than F-006/F-007: un
paquete externo nuevo (`LiveChartsCore.SkiaSharpView.Avalonia`), 4 clases de
query nuevas cada una con su propio archivo de tests, reescritura completa de
`DashboardViewModel` + `DashboardView`, 2 `UserControl` de gráficos nuevos, y
3 interfaces de repositorio (`IExpenseRepository`, `IIncomeRepository`,
`IDebtPaymentRepository`) extendidas con métodos de agregación.

Decision needed before apply: ninguna decisión de negocio pendiente. Las dos
ambigüedades que el design.md marcó como "resolución requerida antes de
tasks" (horizonte de 30 días en R-6/S-5/criterios de aceptación, y que
"Próximos pagos" no recalcula en S-2) ya están reflejadas en
`spec/features/phase-08/F-008-dashboard-visualizations.spec.md` — no se
re-abren aquí.

Chained PRs recommended: **sí**, a diferencia de Fase 6 y Fase 7 (que,
aunque marcadas de alto riesgo, cerraron en un solo apply). La razón para
diverger acá no es el tamaño por sí solo sino dos riesgos cualitativamente
distintos que la Fase 6/7 no tenían: (a) una dependencia externa nueva
(`LiveChartsCore.SkiaSharpView.Avalonia`) cuyo riesgo de renderizado
cross-OS el propio design.md pide validar temprano, no al final; y (b) una
reescritura completa de `DashboardViewModel`/`DashboardView` que, si se
mezcla con la capa de agregación y con "Próximos pagos" en un solo commit,
hace que un fallo de renderizado de gráficos bloquee también la lógica de
agregación (ya probada y de bajo riesgo) en el mismo PR. El corte en 3
slices que ya propone el design.md (agregación → UI dashboard → próximos
pagos) es exactamente el corte de PRs: slice 1 es databases-only y de bajo
riesgo, slice 2 concentra el riesgo (paquete nuevo + reescritura UI), slice
3 es pequeño y aditivo (no toca navegación de periodo, D-6).

400-line budget risk: **alto**, confirmando la propia evaluación del
design.md. Incluso por slice: el slice 2 por sí solo (paquete + 3 queries +
ViewModel + View + 2 UserControls + tests) probablemente supera las 400
líneas. Aceptado dado el corte en PRs encadenados — cada PR se revisa por
separado, no se espera que ninguno quede bajo 400 líneas de forma estricta.

## 1. Capa de agregación

Sin UI. Repositorios, DTOs y cálculo de periodo — la base de la que
dependen los slices 2 y 3.

- [x] 1.1 Crear los DTOs de agregación en
  `src/ControlDeFinanzasIA.Application/DTOs/Aggregates/`: `CurrencyTotal`,
  `CategoryCurrencyTotal`, `MonthCurrencyTotal` (records, grano flat por
  dimensión × moneda, per D-1). (R-3)
  (`Application/DTOs/Aggregates/CurrencyTotal.cs`, `CategoryCurrencyTotal.cs`,
  `MonthCurrencyTotal.cs` — verificado por compilación y por los tests de
  integración de 1.9, que consumen los tres records.)
- [x] 1.2 Crear `MonthPeriod` (Anchor/Range/Previous/Next/TrailingRange) en
  `Application/Common/MonthPeriod.cs` y `DashboardDefaults` (TopCategories=5,
  EvolutionMonths=6, UpcomingHorizonDays=30, UpcomingMaxItems=10) en
  `Application/Common/DashboardDefaults.cs`. (R-1, R-4, R-5, R-6)
  (`MonthPeriodTests` — 14 casos, ver 1.3. `DashboardDefaults` sin tests
  propios: son 4 constantes, verificadas por lectura de código.)
- [x] 1.3 Tests unitarios de `MonthPeriod`: rollover dic→ene, rollover
  ene→dic, febrero en año bisiesto, tamaño de ventana de
  `TrailingRange`. (`tests/…Tests.Unit/Application/Common/MonthPeriodTests.cs`
  — test-only, soporta R-1/R-5)
  (14 casos: `Previous_January_RollsBackToDecemberOfPriorYear`,
  `Next_December_RollsForwardToJanuaryOfNextYear`,
  `Range_FebruaryLeapYear_EndsOnTwentyNine`,
  `Range_FebruaryNonLeapYear_EndsOnTwentyEight`,
  `TrailingRange_SixMonthsCrossingYearBoundary_StartsInPriorYear`,
  `TrailingRange_ZeroMonths_ThrowsArgumentOutOfRangeException`, entre otros.)
- [x] 1.4 Extender `IExpenseRepository` con `GetPeriodTotalsAsync`,
  `GetCategoryTotalsAsync`, `GetMonthlyTotalsAsync` (todas sobre
  `DateRange`, inclusive en ambos extremos). (R-3)
  (`Application/Abstractions/IExpenseRepository.cs` — verificado por
  compilación: `ExpenseRepository` y `FakeExpenseRepository` implementan la
  interfaz sin `NotImplementedException`.)
- [x] 1.5 Extender `IIncomeRepository` con `GetPeriodTotalsAsync`,
  `GetMonthlyTotalsAsync` (sin desglose por categoría, no lo requiere esta
  fase). (R-3)
  (`Application/Abstractions/IIncomeRepository.cs` — mismo criterio que 1.4.)
- [x] 1.6 Implementar en `ExpenseRepository` (EF Core) los tres métodos
  nuevos con `GroupBy`/`Sum` sobre la columna `Amount.Amount`, agrupando por
  `Amount.Currency`; `GetCategoryTotalsAsync` hace join a `Categories` para
  la etiqueta; todas con `AsNoTracking()`, sin `Include`. Verificar que
  `e.Date.Year`/`.Month` traduce a `strftime` en el proveedor SQLite de EF
  Core 8 vía el test de integración de 1.9; si no traduce, fallback a N
  llamadas secuenciales a `GetPeriodTotalsAsync` (una por mes, N ≤ 6) — sigue
  siendo `SUM` en SQL, nunca agrupación en memoria sobre listas de entidades.
  (R-3)
  (`Infrastructure/Persistence/Repositories/ExpenseRepository.cs` —
  `GetPeriodTotalsAsync`/`GetCategoryTotalsAsync`/`GetMonthlyTotalsAsync`.
  **Desviación real, distinta a la anticipada**: `e.Date.Year`/`.Month` SÍ
  traduce a `strftime` sin problema; el bloqueo real fue que el proveedor
  SQLite de EF Core 8 lanza `NotSupportedException` en **cualquier**
  `Sum(decimal)` dentro de un `GroupBy`, sin importar la clave de
  agrupación — confirmado al ver fallar incluso `GetPeriodTotalsAsync`
  (agrupación de un solo nivel por `Currency`). Fix aplicado: cast a
  `double` dentro del `Sum` y de vuelta a `decimal` al proyectar
  (`(decimal)g.Sum(x => (double)x.Amount.Amount)`), documentado en el XML
  doc de cada método. El `SUM` sigue ejecutándose en SQL — satisface R-3 —
  no se aplicó el fallback de N llamadas secuenciales porque no era ese el
  problema. Confirmado en verde por
  `Expense_GetMonthlyTotalsAsync_GroupsAcrossMonths_TranslatesToSql` de 1.9.)
- [x] 1.7 Implementar en `IncomeRepository` (EF Core) `GetPeriodTotalsAsync`
  y `GetMonthlyTotalsAsync` con el mismo patrón (sin join a categorías).
  (R-3)
  (`Infrastructure/Persistence/Repositories/IncomeRepository.cs` — mismo
  workaround de cast a `double` que 1.6. Confirmado por
  `Income_GetPeriodTotalsAsync_MultiCurrency_ReturnsOneRowPerCurrency` e
  `Income_GetMonthlyTotalsAsync_GroupsAcrossMonths_TranslatesToSql`.)
- [x] 1.8 Actualizar `FakeExpenseRepository` y `FakeIncomeRepository` en
  `tests/…Tests.Unit/TestDoubles/` implementando los métodos nuevos
  (agrupación en memoria, aceptable en un test double). (test-only infra)
  (`FakeExpenseRepository.cs` añade `CategoryNames` (`Dictionary<Guid,
  string>`) para emular el join a `Categories` en memoria;
  `FakeIncomeRepository.cs` implementa los dos métodos sin categoría.
  Verificado por compilación — ambos siguen implementando sus interfaces
  completas, usados por el resto de la suite Unit (582 tests en verde).)
- [x] 1.9 Tests de integración contra SQLite real en
  `tests/…Tests.Integration/Persistence/DashboardAggregateQueryTests.cs`:
  grano multi-moneda, periodo vacío → lista vacía, y que el agrupamiento
  mensual traduce a SQL (o queda documentado el fallback de 1.6 si no
  traduce). Mismo patrón que `ExpenseRepositoryTests`. (R-3)
  (`DashboardAggregateQueryTests.cs` — 11 tests: grano multi-moneda
  (`Expense_GetPeriodTotalsAsync_MultiCurrency_ReturnsOneRowPerCurrency`,
  `Income_GetPeriodTotalsAsync_MultiCurrency_ReturnsOneRowPerCurrency`),
  multi-categoría x moneda con join real a `Categories`
  (`Expense_GetCategoryTotalsAsync_MultiCategoryMultiCurrency_ReturnsGrainPerCategoryAndCurrency`),
  periodo vacío → lista vacía (3 casos), filtrado en los bordes del rango
  (`Expense_GetPeriodTotalsAsync_ExcludesRowsOutsideRange`), y agrupamiento
  mensual cruzando meses vía `strftime`
  (`Expense_GetMonthlyTotalsAsync_GroupsAcrossMonths_TranslatesToSql`,
  `Income_GetMonthlyTotalsAsync_GroupsAcrossMonths_TranslatesToSql`). Todos
  en verde contra SQLite real (`TestAppDbContextFactory` + `MigrateAsync`).)

## 2. Dashboard UI (resumen + gráficos)

Depende del slice 1. Introduce LiveCharts2 y reescribe
`DashboardViewModel`/`DashboardView` para el resumen y los dos gráficos que
dependen del periodo navegado. **No** incluye "Próximos pagos" todavía
(slice 3).

- [x] 2.1 Agregar referencia al paquete
  `LiveChartsCore.SkiaSharpView.Avalonia` versión `2.0.5`:
  `<PackageVersion>` en `Directory.Packages.props` con comentario
  justificando la versión (CPM, sin `Version` en el csproj) +
  `<PackageReference>` en `ControlDeFinanzasIA.UI.csproj`. (R-8)
  (`Directory.Packages.props` — `<PackageVersion Include="LiveChartsCore.SkiaSharpView.Avalonia" Version="2.0.5" />`
  con comentario justificando la versión; `ControlDeFinanzasIA.UI.csproj` —
  `<PackageReference Include="LiveChartsCore.SkiaSharpView.Avalonia" />` sin
  `Version`. Verificado por `dotnet restore` (paquete + dependencias
  transitivas `LiveChartsCore`/`LiveChartsCore.SkiaSharpView` resueltas sin
  error) y por `dotnet build -c Release` en verde.)
- [x] 2.2 Chequeo temprano de renderizado cross-OS: levantar un
  `CartesianChart`/`PieChart` mínimo de LiveCharts en la app y confirmar que
  renderiza en la(s) plataforma(s) disponibles en este entorno, **antes**
  de construir el resto del layout — per design.md, hacerlo temprano, no al
  final. Manual (Avalonia no tiene runner headless en este entorno, mismo
  caveat que Fase 7 §5.1). (R-8, criterio de aceptación "renderiza en
  Windows/macOS/Linux")
  (Se construyeron `CategoryBreakdownChart`/`MonthlyEvolutionChart`
  envolviendo `PieChart`/`CartesianChart` y se corrió
  `dotnet build src/ControlDeFinanzasIA.UI/ControlDeFinanzasIA.UI.csproj`
  **antes** de escribir `DashboardView.axaml`/`DashboardViewModel`,
  confirmando que el compilador de AXAML de Avalonia resuelve los tipos de
  LiveCharts2 (`lvc:PieChart`, `lvc:CartesianChart`) y que el build no falla
  al referenciarlos. **Límite real de este entorno**: no hay Windows
  Desktop/X11/macOS con GPU disponible para levantar la ventana y verificar
  renderizado a ojo — mismo caveat manual/best-effort documentado en Fase 7
  §5.1. No se pudo confirmar visualmente que los charts pintan píxeles en
  pantalla; sí se confirmó que compilan, que el paquete trae los assets
  nativos de SkiaSharp para win-x64/osx/linux-x64 vía sus dependencias
  transitivas, y que los controles se instancian sin excepción de tipo en
  tiempo de compilación de AXAML (que ya ejecuta parte de la resolución de
  propiedades `Series`/`XAxes`).)
- [x] 2.3 Crear `GetMonthSummaryQuery` + `MonthSummaryResult` en
  `Application/UseCases/Dashboard/GetMonthSummary/`: deriva rango del mes
  seleccionado y del mes anterior, convierte cada bucket por moneda con
  `ICurrencyConverter.ConvertAsync(money, Currency.DOP, ct)` (no
  `DisplayAsync`), calcula balance neto y delta % (null si el mes anterior
  fue 0), excluye buckets con `ExchangeRateUnavailable` y marca
  `HasMissingRates`. Tests unitarios: delta positivo/negativo/mes anterior
  en 0 → null, exclusión por fallo de FX sin excepción. (R-2, R-3, R-7, S-1,
  S-6)
  (`Application/UseCases/Dashboard/GetMonthSummary/{GetMonthSummaryQuery,MonthSummaryResult}.cs`
  — `tests/…Tests.Unit/Application/Dashboard/GetMonthSummaryQueryTests.cs`,
  6 tests: `ExecuteAsync_MixedCurrencies_NormalizesEverythingToDop` (S-1),
  `ExecuteAsync_ExpensesIncreasedVsPreviousMonth_ReturnsPositiveDelta`,
  `ExecuteAsync_ExpensesDecreasedVsPreviousMonth_ReturnsNegativeDelta`,
  `ExecuteAsync_PreviousMonthHadNoExpenses_ReturnsNullDelta`,
  `ExecuteAsync_FxConversionFails_ExcludesBucketAndSetsHasMissingRatesWithoutThrowing`
  (S-6), `ExecuteAsync_MonthWithNoTransactions_ReturnsZeroSummaryWithoutThrowing`
  (S-3). Usa `ICurrencyConverter.ConvertAsync` — confirmado leyendo
  `CurrencyConverter.cs:141-143` que `DisplayAsync` habría convertido DOP→USD
  en vez de normalizar a DOP.)
- [x] 2.4 Crear `GetCategoryBreakdownQuery` + `CategoryBreakdownResult` en
  `Application/UseCases/Dashboard/GetCategoryBreakdown/`: trae **todas** las
  categorías del periodo desde `GetCategoryTotalsAsync`, normaliza a DOP,
  rankea y arma "Otras" en la Query (no en SQL, per D-2) — top 5 + "Otras"
  cuando hay más de 5. Tests unitarios: empate en el ranking, <5 categorías
  → sin "Otras", suma de slices == `TotalDop`. (R-3, R-4, S-4)
  (`Application/UseCases/Dashboard/GetCategoryBreakdown/{GetCategoryBreakdownQuery,CategoryBreakdownResult}.cs`
  — `tests/…Tests.Unit/Application/Dashboard/GetCategoryBreakdownQueryTests.cs`,
  6 tests: `ExecuteAsync_SevenCategories_ReturnsTop5PlusOtras` (S-4),
  `ExecuteAsync_FewerThanTopCountCategories_ReturnsNoOtrasSlice`,
  `ExecuteAsync_SlicesSum_ReconcilesWithTotalDop`,
  `ExecuteAsync_TiedAmountsAtRankingBoundary_IncludesBothDeterministically`
  (empate en el ranking, desempate determinístico por nombre vía
  `ThenBy(c => c.Label, StringComparer.Ordinal)` para no depender del orden
  de iteración de un `Dictionary`),
  `ExecuteAsync_FxConversionFails_ExcludesBucketAndSetsHasMissingRatesWithoutThrowing`,
  `ExecuteAsync_MonthWithNoExpenses_ReturnsEmptySlicesWithoutThrowing` (S-3).
  **Desviación menor de design.md**: la etiqueta "Otras" se hardcodea como
  constante privada en Application (`_otherCategoryLabel`) porque esta capa
  no tiene acceso a `IStringLocalizer`/Avalonia (prohibido por
  `ArchitectureTests`); documentado en el XML doc de la clase.)
- [x] 2.5 Crear `GetMonthlyEvolutionQuery` + `MonthlyEvolutionResult` en
  `Application/UseCases/Dashboard/GetMonthlyEvolution/`: ventana de
  `DashboardDefaults.EvolutionMonths` (6) meses consecutivos terminando en
  el mes seleccionado, vía `MonthPeriod.TrailingRange`; rellena con 0 los
  meses sin filas (zero-fill, mantiene el eje X estable). Tests unitarios:
  6 puntos incluso con solo 2 meses de datos. (R-3, R-5, S-3)
  (`Application/UseCases/Dashboard/GetMonthlyEvolution/{GetMonthlyEvolutionQuery,MonthlyEvolutionResult}.cs`
  — `tests/…Tests.Unit/Application/Dashboard/GetMonthlyEvolutionQueryTests.cs`,
  3 tests: `ExecuteAsync_OnlyTwoMonthsHaveData_ReturnsSixZeroFilledPointsInOrder`
  (6 puntos con solo 2 meses de datos, orden cronológico Oct 2025..Mar 2026),
  `ExecuteAsync_NoDataAtAll_ReturnsSixZeroPointsWithoutThrowing` (S-3),
  `ExecuteAsync_FxConversionFails_ExcludesBucketAndSetsHasMissingRatesWithoutThrowing`
  (S-6).)
- [x] 2.6 Reescribir `DashboardViewModel`
  (`src/ControlDeFinanzasIA.UI/ViewModels/DashboardViewModel.cs`):
  `SelectedMonth`/`SelectedMonthLabel`, comandos
  `PreviousMonth`/`NextMonth`/`Refresh`, `IsLoading`/`LoadError`,
  propiedades de resumen pre-formateadas
  (`Strings.ApproximateFormat`/`≈ {N2} {Currency}` cuando el periodo tuvo
  filas no-DOP), `CategorySlices`/`EvolutionPoints`
  (`ObservableCollection`) + construcción de `ISeries[]`/`Axis[]` de
  LiveCharts. Constructor inyecta las 3 queries de este slice + `IClock` +
  `ILogger`, arranca con `SelectedMonth = MonthPeriod.Anchor(_clock.Today)`
  y `_ = LoadAsync()` (mismo patrón que `RecurringRulesViewModel`). Sin
  "Próximos pagos" todavía. (R-1, R-2)
  (`UI/ViewModels/DashboardViewModel.cs` — reescritura completa, verificada
  por `tests/…Tests.Unit/UI/DashboardViewModelTests.cs` (ver 2.7) y por el
  build en verde. **Desviaciones de design.md, ambas documentadas también
  en el reporte final:** (a) el formato `≈ {N2} DOP` se aplica siempre a
  los 3 montos del resumen, no condicionalmente "solo si el periodo tuvo
  filas no-DOP" — `MonthSummaryResult` no lleva ese flag adicional y una
  cifra agregada normalizada siempre es, semánticamente, una aproximación;
  (b) `Bootstrap.cs` registra las 3 queries de este slice como
  `AddTransient<>` ahora (no en 3.6 como dice la tabla de archivos), porque
  sin el registro el contenedor de DI no puede resolver
  `DashboardViewModel` y la app no arrancaría — 3.6 solo añadirá
  `GetUpcomingPaymentsQuery` cuando exista.)
- [x] 2.7 Tests de `DashboardViewModel`: navegar mes anterior/siguiente
  recalcula resumen + ambos gráficos (S-2, parte dependiente del periodo);
  carga concurrente de las 3 queries vía `Task.WhenAll`.
  (`tests/…Tests.Unit/UI/DashboardViewModelTests.cs`) (R-1)
  (4 tests: `RefreshCommand_PopulatesSummaryCategoryBreakdownAndEvolutionTogether`
  (las 3 queries del `Task.WhenAll` aportan al mismo snapshot coherente),
  `PreviousMonthCommand_RecalculatesSummaryAndBothCharts`,
  `NextMonthCommand_RecalculatesSummaryAndBothCharts` (S-2),
  `RefreshCommand_FxConversionFails_SetsHasMissingRatesAndDoesNotThrow`
  (S-6, a nivel ViewModel).)
- [x] 2.8 Reescribir `DashboardView.axaml(.cs)`: cabecera con navegación de
  periodo, tarjeta de resumen, ubicación de los dos gráficos, bindings de
  estado vacío (`IsVisible="{Binding !Collection.Count}"`, mismo patrón que
  `RecurringRulesView.axaml:79`), aviso de "tasa no disponible" cuando
  `HasMissingRates`. (R-1, R-2, R-7)
  (`UI/Views/DashboardView.axaml(.cs)` — reescritura completa: header con
  `PreviousMonthCommand`/`NextMonthCommand`/`RefreshCommand`, tarjeta de
  resumen (3 columnas), notice de `HasMissingRates`, dos `Border` con los
  charts. El binding de estado vacío (`!CategorySlices.Count` /
  `!EvolutionPoints.Count`) se pasa como `IsEmpty` a los `UserControl` de
  2.9/2.10 en vez de duplicar el `TextBlock` de "sin datos" en esta vista
  (ver D-7 en 2.9). Verificado por el compilador de AXAML de Avalonia
  (`dotnet build -c Release` en verde, sin errores `AVLN2000`).)
- [x] 2.9 Crear `CategoryBreakdownChart` (UserControl,
  `UI/Views/Charts/CategoryBreakdownChart.axaml(.cs)`): envuelve
  `PieChart`, expone `StyledProperty` de entrada, overlay de estado vacío
  propio (per D-7 — dos controles delgados, no uno parametrizado). (R-4,
  R-8)
  (`UI/Views/Charts/CategoryBreakdownChart.axaml(.cs)` —
  `StyledProperty<IEnumerable<ISeries>?> Series`,
  `StyledProperty<bool> IsEmpty`, `StyledProperty<string> EmptyText`; el
  overlay de estado vacío (`TextBlock` vs. `PieChart`, alternados por
  `IsEmpty`) vive dentro del control, no en `DashboardView.axaml` — la
  razón explícita de D-7 para rechazar "sin wrapper". Verificado por
  compilación del AXAML.)
- [x] 2.10 Crear `MonthlyEvolutionChart` (UserControl,
  `UI/Views/Charts/MonthlyEvolutionChart.axaml(.cs)`): envuelve
  `CartesianChart`, `StyledProperty` de entrada, overlay de estado vacío
  propio. (R-5, R-8)
  (`UI/Views/Charts/MonthlyEvolutionChart.axaml(.cs)` — mismo patrón que
  2.9, con `StyledProperty<IEnumerable<ISeries>?> Series` +
  `StyledProperty<IEnumerable<ICartesianAxis>?> XAxes` adicional para los
  meses del eje X. Verificado por compilación del AXAML.)
- [x] 2.11 Localización: etiquetas de los widgets y textos de estado vacío
  en `UI/Resources/Strings.resx` y `Strings.es-DO.resx`. (R-7)
  (13 claves nuevas añadidas a `Strings.resx` (inglés) y
  `Strings.es-DO.resx` (español) — navegación de periodo, las 3 etiquetas
  de la tarjeta de resumen, títulos de los 2 gráficos, 2 mensajes de estado
  vacío, aviso de tasa no disponible y mensaje de error de carga — todas
  consumidas por `DashboardView.axaml` vía `{x:Static resources:Strings.X}`
  (mismo patrón que `MainWindow.axaml:10`). **Hallazgo no anticipado**:
  `Strings.Designer.cs` **no se regenera automáticamente** en este repo con
  `dotnet build` (no hay un target de MSBuild para
  `ResXFileCodeGenerator` en un csproj SDK-style plano); el build falló con
  `AVLN2000: Unable to resolve "Strings.X"` hasta añadir a mano los 13
  accessors `public static string X => ResourceManager.GetString("X", Culture)!;`
  al final de la clase. Documentado aquí porque es un paso manual fácil de
  olvidar en el próximo slice que agregue claves de localización.)

## 3. Próximos pagos

Depende del slice 1 (`IDebtPaymentRepository`) y del slice 2
(`DashboardViewModel` ya reescrito). Aditivo — no toca la navegación de
periodo (D-6).

- [ ] 3.1 Extender `IDebtPaymentRepository` con
  `GetPendingByDateRangeAsync(DateRange range, ct)`, implementarlo en
  `DebtPaymentRepository` (EF Core, filtro SQL por `Status == Pending` y
  `ScheduledDate` en rango — `DebtPayment` crece con `debts × cuotas`, por
  eso sí lleva filtro SQL a diferencia de `RecurringRule`), y actualizar
  `FakeDebtPaymentRepository` con el método nuevo. (R-6)
- [ ] 3.2 Crear `GetUpcomingPaymentsQuery` +
  `UpcomingPaymentItem`/`UpcomingPaymentKind` en
  `Application/UseCases/Dashboard/GetUpcomingPayments/`: combina
  `DebtPayment` pendientes (`IDebtPaymentRepository`,
  `IDebtRepository` para la etiqueta) con `RecurringRule` cuya
  `NextGenerationDate` cae en el horizonte (`IRecurringRuleRepository`,
  filtro en memoria sobre `GetAllAsync` — `Active &&
  range.Contains(NextGenerationDate)`, mismo patrón que `GetDebtsQuery`
  filtrando `Debt.Status` en memoria), ordena por fecha, limita a
  `DashboardDefaults.UpcomingMaxItems` (10), horizonte
  `DashboardDefaults.UpcomingHorizonDays` (30) desde `IClock.Today`. Tests
  unitarios: límite de 30 días inclusive, mezcla y orden de ambas fuentes,
  tope de 10 elementos, reglas `Active == false` excluidas. (R-6, S-5)
- [ ] 3.3 Conectar `GetUpcomingPaymentsQuery` a `DashboardViewModel`:
  cargar **una sola vez** dentro de `LoadAsync` (no se vuelve a ejecutar en
  `PreviousMonth`/`NextMonth`, per D-6), colección
  `UpcomingPayments`. (R-1a, S-2 — la tarjeta NO cambia al navegar)
- [ ] 3.4 Test de `DashboardViewModel`: navegar entre meses deja
  `UpcomingPayments` sin cambios. (R-1a)
- [ ] 3.5 Tarjeta "Próximos pagos" en `DashboardView.axaml`: lista de
  `UpcomingPaymentItem` (cuota de deuda / regla recurrente) ordenada por
  fecha, cada monto con su formato `≈` por-ítem existente, estado vacío
  cuando `!UpcomingPayments.Count`. (R-6, R-7)
- [ ] 3.6 Registrar en `Bootstrap.cs` las 4 queries del dashboard
  (`GetMonthSummaryQuery`, `GetCategoryBreakdownQuery`,
  `GetMonthlyEvolutionQuery`, `GetUpcomingPaymentsQuery`) como
  `AddTransient<…>()`, junto a `GetDebtsQuery`. No requiere nuevo registro
  de repositorio (`AddPersistence` ya expone las implementaciones
  concretas). (infra)

## 4. Cierre

- [ ] 4.1 Ejecutar build, `dotnet test` (unit + integración), `dotnet
  format style --verify-no-changes`, `ArchitectureTests`/NetArchTest
  (confirma que `LiveChartsCore.SkiaSharpView.Avalonia` en UI sin
  abstracción sigue siendo legal per R-8), smoke test manual si el entorno
  lo permite.
- [ ] 4.2 Crear/actualizar `spec/phases/phase-08/features.md` siguiendo el
  patrón de `spec/phases/phase-06/features.md` y
  `spec/phases/phase-07/features.md` (resumen, modelo, plan de slices con
  la tabla de los 3 slices de este archivo + su estado, criterios de fase
  completada, referencias).
- [ ] 4.3 Archivar la spec
  (`F-008-dashboard-visualizations.spec.md` → `.done.md`), escribir
  `verify-report.md` + `archive-report.md` en
  `.atl/sdd/f-008p8-dashboard-visualizations/`, actualizar `docs/roadmap.md`
  §Fase 8. *(Sin marcar — es un paso posterior a apply + verify, no lo
  alcanza este ciclo.)*
