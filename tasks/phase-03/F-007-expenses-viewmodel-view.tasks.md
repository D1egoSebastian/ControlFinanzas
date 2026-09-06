# Tareas — F-007: UI: `ExpensesViewModel` + `ExpensesView` + ViewModel tests

> **Spec:** `spec/features/phase-03/F-007-expenses-viewmodel-view.spec.md`
> **Fuentes de verdad:** la spec, `docs/architecture.md` §9/§10/§15,
> `spec/constitution/tech-stack.md`, `spec/features/phase-03/F-006-ui-shell-navigation.done.md`,
> `spec/features/phase-03/F-004-register-expense-use-case.done.md`,
> `spec/features/phase-03/F-005-get-transactions-query.done.md`,
> `.claude/skills/frontend-design/SKILL.md`.
>
> F-007 cierra el slice vertical de Fase 3. No modifica `Domain`,
> `Infrastructure`, `RegisterExpenseUseCase`, `GetTransactionsQuery` ni
> `MainWindow.axaml`.

---

## Pronóstico de carga de review

| Field | Value |
|-------|-------|
| Estimated changed lines | ~600–800 |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 → PR 2 |
| Delivery strategy | auto-chain |
| Chain strategy | stacked-to-main |

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: High

### Unidades de trabajo sugeridas

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Foundation + core VM + VM tests | PR 1 | Base = `main`; T-001..T-006. Entrega `CategoryDto`, `ExpensesViewModel`, tests puros, y `Avalonia.Headless` en CPM. ~350–400 líneas. |
| 2 | Vistas, DI, navegación, recursos y verificación | PR 2 | Base = PR 1; T-007..T-012. Entrega `ExpensesView`, DataTemplate, wiring, `.resx`, smoke manual. ~300–400 líneas. |

> Nota: se opta por el **split de 2 PRs** (preferencia del orchestrator) para
> mantener el cierre vertical visible en PR 2 y evitar que la vista quede
> huérfana de su VM. PR 1 es testeable de forma independiente; PR 2 depende
> de él.

---

## Resumen

F-007 conecta el shell de F-006 con los use cases de F-004/F-005:
`ExpensesViewModel` (`sealed partial : ViewModelBase`) expone el formulario de
registro, la lista paginada de gastos, los errores por campo y la paginación.
`ExpensesView` renderiza el formulario y la lista con `ListBox` + `ItemsTemplate`
(de 4 columnas) sin `DataGrid`. Se añade `Avalonia.Headless` 11.2.7 en CPM
(como capacidad para Phase 4+), pero los tests de F-007 son C# puro y el smoke
es manual (`dotnet run`).

Entregables:

- `src/ControlDeFinanzasIA.Application/DTOs/CategoryDto.cs`
- `src/ControlDeFinanzasIA.Application/DTOs/CategoryExtensions.cs`
- `src/ControlDeFinanzasIA.UI/ViewModels/ExpensesViewModel.cs`
- `src/ControlDeFinanzasIA.UI/Views/ExpensesView.axaml` + `.cs` (code-behind vacío)
- `src/ControlDeFinanzasIA.UI/Resources/Strings.resx` + `Strings.es-DO.resx` + `Strings.Designer.cs`
- Modificaciones en `NavigationService.cs`, `Bootstrap.cs`, `App.axaml`
- `Directory.Packages.props` + `ControlDeFinanzasIA.UI.csproj` (`Avalonia.Headless`)
- `tests/ControlDeFinanzasIA.Tests.Unit/UI/ExpensesViewModelTests.cs`
- `tests/ControlDeFinanzasIA.Tests.Unit/TestDoubles/FakeCategoryRepository.cs`
- `dotnet build` limpio; `dotnet test` ≈ **219 verdes**; NetArchTest R-1..R-5 verdes.

---

## Resumen de tareas

| ID | Tarea | Diff | Depende de | Verificación |
| --- | --- | --- | --- | --- |
| [x] T-001 | Añadir `Avalonia.Headless` 11.2.7 a CPM y `UI.csproj` | +6 / -0 | — | `dotnet build` |
| [x] T-002 | Crear `CategoryDto` record mínimo `(Guid Id, string Name)` | +6 / -0 | — | `dotnet build` |
| [x] T-003 | Crear `CategoryExtensions.ToDto(this Category)` | +8 / -0 | T-002 | `dotnet build` |
| [x] T-004 | Crear `FakeCategoryRepository` en `TestDoubles` | +30 / -0 | — | `dotnet build` tests |
| [x] T-005 | Crear `ExpensesViewModel` (`sealed partial`, 6 deps, comandos) | +120 / -0 | T-002, T-003, T-004 | `dotnet build` |
| [x] T-006 | Crear `ExpensesViewModelTests` (≥ 7 tests pure `Assert.*`) | +180 / -0 | T-005 | `dotnet test --filter "FullyQualifiedName~ExpensesViewModelTests"` ≥ 7 passed |
| [x] T-007 | Modificar `NavigationService._routes` para `RouteNames.Expenses` | +1 / -0 | T-005 | `dotnet build` |
| [x] T-008 | Crear `ExpensesView.axaml` + code-behind vacío | +80 / -0 | T-005 | `dotnet build` |
| [x] T-009 | Añadir DataTemplate `ExpensesViewModel → ExpensesView` en `App.axaml` | +5 / -0 | T-008 | `dotnet build` |
| [x] T-010 | Registrar `ExpensesViewModel` como transient en `Bootstrap` | +1 / -0 | T-005 | `dotnet build` |
| [x] T-011 | Añadir claves UI a `Strings.resx`, `Strings.es-DO.resx`, `Strings.Designer.cs` | +80 / -0 | — | `dotnet build` |
| [x] T-012 | Verificación final, formato, smoke y estado de fase | +5 / -3 | T-001..T-011 | `dotnet test` ~219 passed; `dotnet format style --verify-no-changes`; smoke manual |

**Total estimado:** ~10–14 horas.

---

## T-001 — Añadir `Avalonia.Headless` 11.2.7 a CPM y `UI.csproj`

**Archivos:** `Directory.Packages.props`, `src/ControlDeFinanzasIA.UI/ControlDeFinanzasIA.UI.csproj`

**Pasos:** Añadir `<PackageVersion Include="Avalonia.Headless" Version="11.2.7" />` en `Directory.Packages.props` dentro del bloque Avalonia 11.2.7 (con comentario justificante). Añadir `<PackageReference Include="Avalonia.Headless" />` (sin `Version`) en `UI.csproj`. **No** añadir a `Tests.Unit.csproj` (AC-15/DA #3: los tests de VM son C# puro; el paquete es capacidad para Phase 4+). **No** usar `Avalonia.Headless.XUnit`. Ejecutar `dotnet build`.

**Verificación:** `dotnet build "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --nologo` exit 0; `Tests.Unit.csproj` no referencia `Avalonia.Headless`.

**Dependencias:** —
**Estimación:** XS (< 30 min)
**Trazabilidad:** AC-15

---

## T-002 — Crear `CategoryDto`

**Archivos:** `src/ControlDeFinanzasIA.Application/DTOs/CategoryDto.cs`

**Pasos:** Crear `public sealed record CategoryDto(Guid Id, string Name)` en file-scoped namespace `ControlDeFinanzasIA.Application.DTOs`. XML doc. **No** añadir `Description`/`IsActive` — `Category` no los posee (trim verificado en §4 #2 de la spec). Ejecutar `dotnet build`.

**Verificación:** Build exit 0; record inmutable; namespace correcto.

**Dependencias:** —
**Estimación:** XS (< 30 min)
**Trazabilidad:** AC-9

---

## T-003 — Crear `CategoryExtensions.ToDto`

**Archivos:** `src/ControlDeFinanzasIA.Application/DTOs/CategoryExtensions.cs`

**Pasos:** Crear clase `public static class CategoryExtensions` con método `public static CategoryDto ToDto(this Category c) => new(c.Id, c.Name);`. Añadir `using ControlDeFinanzasIA.Domain.Catalog;`. XML doc. Ejecutar `dotnet build`.

**Verificación:** Build exit 0; extensión usable como `category.ToDto()`.

**Dependencias:** T-002
**Estimación:** XS (< 30 min)
**Trazabilidad:** AC-9

---

## T-004 — Crear `FakeCategoryRepository`

**Archivos:** `tests/ControlDeFinanzasIA.Tests.Unit/TestDoubles/FakeCategoryRepository.cs`

**Pasos:** Implementar `ICategoryRepository` con propiedad configurable `IReadOnlyList<Category> Categories { get; set; }` (default `Array.Empty<Category>()`). `GetAllAsync` devuelve `Categories`. `AddAsync`/`UpdateAsync`/`RemoveAsync` retornan `Result.Success(null!)`; `GetByIdAsync` retorna `null`. Hand-rolled; sin mocking. Ejecutar `dotnet build` tests.

**Verificación:** Build tests exit 0; implementa `ICategoryRepository`; `GetAllAsync` configurable.

**Dependencias:** —
**Estimación:** S (< 1 h)
**Trazabilidad:** AC-17

---

## T-005 — Crear `ExpensesViewModel`

**Archivos:** `src/ControlDeFinanzasIA.UI/ViewModels/ExpensesViewModel.cs`

**Pasos:**
- `public sealed partial class ExpensesViewModel : ViewModelBase`.
- Constructor inyecta 6 dependencias: `RegisterExpenseUseCase`, `GetTransactionsQuery`, `ICategoryRepository`, `IClock`, `ILogger<ExpensesViewModel>`, `IStringLocalizer<Strings>`. `ArgumentNullException.ThrowIfNull` por cada una.
- `[ObservableProperty]` fields: `Date` (`DateOnly?`, default `_clock.Today`), `Amount` (`decimal?`), `Currency` (default `Currency.DOP`), `SelectedCategoryId` (`Guid?`), `Description` (`string?`), `Note` (`string?`).
- `[ObservableProperty]` lista/paginación: `Expenses` (`ObservableCollection<ExpenseDto>`), `Categories` (`IReadOnlyList<CategoryDto>`), `Page` (default 1), `TotalPages` (default 0).
- `[ObservableProperty]` errores: `ValidationErrors` (`Dictionary<string, string>`), `FormError` (`string?`).
- `[RelayCommand] private async Task RegisterExpenseAsync()`: construye `RegisterExpenseCommand(SelectedCategoryId ?? Guid.Empty, Date ?? _clock.Today, Amount ?? 0m, Currency, Description ?? string.Empty, Note)`. Llama `_registerUseCase.ExecuteAsync`. Éxito → limpia `Amount`/`Description`/`Note`, conserva `Date`/`Currency`, limpia `ValidationErrors` + `FormError`, llama `RefreshAsync()`. `Error.Code == "Validation"` → `ValidationErrors[field] = string.Join("; ", msgs)`. Otros errores → `_logger` + `FormError = _localizer["FormErrorMessage"]`.
- `[RelayCommand] private async Task RefreshAsync()`: llama `_getTransactionsQuery.ExecuteAsync(new GetTransactionsQueryRequest(Page, 20), ct)`. Éxito → cast a `PagedResult<ExpenseDto>`, `Expenses.Clear()` + `Add` por item, `TotalPages = paged.TotalPages` (DP-8). Fallo → log, `Expenses.Clear()`, `TotalPages = 0`.
- `[RelayCommand(CanExecute = nameof(CanGoToPreviousPage))] private async Task PreviousPageAsync()`: decrementa `Page` si `> 1` y llama `RefreshAsync()`. `public bool CanGoToPreviousPage => Page > 1`.
- `[RelayCommand(CanExecute = nameof(CanGoToNextPage))] private async Task NextPageAsync()`: incrementa `Page` si `< TotalPages` y llama `RefreshAsync()`. `public bool CanGoToNextPage => Page < TotalPages`.
- Partial `OnPageChanged` → `PreviousPageCommand.NotifyCanExecuteChanged(); NextPageCommand.NotifyCanExecuteChanged();`. Partial `OnTotalPagesChanged` → `NextPageCommand.NotifyCanExecuteChanged();`.
- Constructor llama `LoadCategoriesAsync()` (`_categoryRepository.GetAllAsync()` → `.Select(c => c.ToDto()).ToList()`) y luego `RefreshAsync()`.
- File-scoped namespace, `using` al tope, XML doc en clase y miembros públicos.
- Ejecutar `dotnet build`.

**Verificación:** Build exit 0; `sealed partial`; 6 dependencias con null-checks; `RegisterExpenseCommand` **sin** `CanExecute` (DA #2); `Page`/`TotalPages` reactivos; `TotalPages` leído de `PagedResult`.

**Dependencias:** T-002, T-003, T-004
**Estimación:** L (2–3 h)
**Trazabilidad:** AC-1, AC-2, AC-3, AC-4, AC-5, AC-6, AC-7, AC-8

---

## T-006 — Crear `ExpensesViewModelTests`

**Archivos:** `tests/ControlDeFinanzasIA.Tests.Unit/UI/ExpensesViewModelTests.cs`

**Pasos:** Implementar ≥ 7 tests pure `Assert.*` siguiendo la tabla §10.1 de la spec. Usar `FakeExpenseRepository`, `FakeClock`, `FakeCategoryRepository`, `FakeStringLocalizer<Strings>`, real `RegisterExpenseValidator`, `NullLogger<ExpensesViewModel>`. Instanciar los **sealed** use cases reales (`RegisterExpenseUseCase`, `GetTransactionsQuery`) — sin interfaces adicionales (DA #1). Tests sugeridos:

1. `Constructor_LoadsCategories_SetsDefaultDateFromClock`
2. `RegisterExpense_ValidForm_CallsUseCase_AndRefreshesList`
3. `RegisterExpense_ServerSideValidationFails_ShowsFieldErrors`
4. `RegisterExpense_UseCaseFailsWithNonValidation_SetsFormError`
5. `Refresh_PopulatesExpensesAndPageMetadata`
6. `NextPage_IncrementsPage_AndRefreshes`
7. `PreviousPage_AtFirstPage_DoesNotDecrement`
8. `NextPage_AtLastPage_DoesNotIncrement` (recomendado)
9. `PreviousPage_OnSecondPage_DecrementsAndRefreshes` (recomendado)
10. `Refresh_OnRepositoryError_ClearsExpensesAndLogs` (recomendado)

Ejecutar `dotnet test --filter "FullyQualifiedName~ExpensesViewModelTests"`.

**Verificación:** Build tests exit 0; ≥ 7 tests passed; sin FluentAssertions; sin librería de mocking; sin `Avalonia.Headless.XUnit`.

**Dependencias:** T-005
**Estimación:** L (2–3 h)
**Trazabilidad:** AC-16, AC-17

---

## T-007 — Modificar `NavigationService._routes` para `Expenses`

**Archivos:** `src/ControlDeFinanzasIA.UI/Navigation/NavigationService.cs`

**Pasos:** Añadir `{ RouteNames.Expenses, typeof(ExpensesViewModel) }` al diccionario `_routes` junto a `RouteNames.Dashboard`. Ejecutar `dotnet build`.

**Verificación:** Build exit 0; `_routes` contiene `Dashboard` y `Expenses`; `NavigateTo(RouteNames.Expenses)` resuelve `ExpensesViewModel`.

**Dependencias:** T-005
**Estimación:** XS (< 15 min)
**Trazabilidad:** AC-13

---

## T-008 — Crear `ExpensesView.axaml` + code-behind vacío

**Archivos:** `src/ControlDeFinanzasIA.UI/Views/ExpensesView.axaml`, `ExpensesView.axaml.cs`

**Pasos:**
- Crear `UserControl` con `x:Class="ControlDeFinanzasIA.UI.Views.ExpensesView"` y `x:DataType="vm:ExpensesViewModel"`.
- Layout: `Grid` o `DockPanel` con formulario arriba y lista abajo.
- Formulario (`StackPanel`): `DatePicker` bindeado a `Date`, `TextBox` para `Amount` con `StringFormat`, `ComboBox` para `Currency` (ítes `CurrencyDOP`/`CurrencyUSD`), `ComboBox` para categorías (`ItemsSource={Binding Categories}`, display `Name`, `SelectedValue` → `SelectedCategoryId`), `TextBox` para `Description`, `TextBox` multilínea para `Note`, `Button` "Registrar" con `Command={Binding RegisterExpenseCommand}`.
- `TextBlock` rojo por campo bindeado a `ValidationErrors[field]` (`{Binding ValidationErrors[Amount]}`), colapsado cuando no hay error; `TextBlock` para `FormError`.
- Lista: `ListBox ItemsSource={Binding Expenses}` con `ItemsTemplate` = `Grid` de 4 columnas (`Date`, `Description`, `Amount`, `Currency`).
- Paginación: `StackPanel` con botón "Anterior" (`PreviousPageCommand`), `TextBlock` "Página X de Y" usando `Page`/`TotalPages` y `PageOfPagesFormat`, botón "Siguiente" (`NextPageCommand`).
- `ExpensesView.axaml.cs`: code-behind **vacío** salvo constructor que llama `InitializeComponent()`.
- Sin estilos inline; reusa `FluentTheme`. Ejecutar `dotnet build`.

**Verificación:** Build exit 0; `x:DataType` presente; code-behind vacío; bindings respetan nombres del VM.

**Dependencias:** T-005
**Estimación:** M (1–2 h)
**Trazabilidad:** AC-10, AC-11

---

## T-009 — Añadir DataTemplate en `App.axaml`

**Archivos:** `src/ControlDeFinanzasIA.UI/App.axaml`

**Pasos:** En `Application.DataTemplates`, añadir `<DataTemplate DataType="{x:Type vm:ExpensesViewModel}" x:DataType="vm:ExpensesViewModel"><views:ExpensesView /></DataTemplate>` junto al existente de `DashboardViewModel`. Ejecutar `dotnet build`.

**Verificación:** Build exit 0; DataTemplate presente con `x:DataType="vm:ExpensesViewModel"`.

**Dependencias:** T-008
**Estimación:** XS (< 30 min)
**Trazabilidad:** AC-12

---

## T-010 — Registrar `ExpensesViewModel` en `Bootstrap`

**Archivos:** `src/ControlDeFinanzasIA.UI/Composition/Bootstrap.cs`

**Pasos:** Añadir `services.AddTransient<ExpensesViewModel>()` en `ConfigureServices`. No modificar los registros existentes de F-004/F-005. Ejecutar `dotnet build`.

**Verificación:** Build exit 0; `ExpensesViewModel` registrado como transient; `ICategoryRepository` ya registrado vía `AddPersistence`.

**Dependencias:** T-005
**Estimación:** XS (< 15 min)
**Trazabilidad:** AC-13

---

## T-011 — Añadir claves UI a `.resx`

**Archivos:** `src/ControlDeFinanzasIA.UI/Resources/Strings.resx`, `Strings.es-DO.resx`, `Strings.Designer.cs`

**Pasos:** Añadir las claves de AC-14 en ambos `.resx` (en fallback + es-DO default) y extender `Strings.Designer.cs` con los accessors. Claves requeridas:

- `ExpensesTitle`
- `RegisterButton`
- `DateLabel`
- `AmountLabel`
- `CurrencyLabel`
- `CategoryLabel`
- `DescriptionLabel`
- `NoteLabel`
- `PreviousPageButton`
- `NextPageButton`
- `PageOfPagesFormat` (`Página {0} de {1}` en es-DO)
- `EmptyExpensesMessage`
- `RegistrationSuccessMessage`
- `FormErrorMessage`
- `CurrencyDOP`
- `CurrencyUSD`

Ejecutar `dotnet build`.

**Verificación:** Build exit 0; claves presentes en ambos `.resx`; accessors en `Strings.Designer.cs`; `FakeStringLocalizer` puede devolver key fallback si no se configura.

**Dependencias:** —
**Estimación:** S (< 1 h)
**Trazabilidad:** AC-14

---

## T-012 — Verificación final, formato, smoke y estado de fase

**Archivos:** `spec/phases/phase-03/features.md`, `tasks/phase-03/F-007-expenses-viewmodel-view.tasks.md`

**Pasos:**
1. `dotnet build -c Release` y `dotnet build -c Debug`: 0 warnings, 0 errors.
2. `dotnet test -c Release --no-build`: **212 + ~7 ≈ 219 verdes**, 0 failures.
3. `dotnet test --filter "FullyQualifiedName~ArchitectureTests"`: passed.
4. `dotnet format style --verify-no-changes`: exit 0.
5. Verificar convenciones: file-scoped namespace, `using` al tope, XML doc, `sealed`, `sealed partial` en VMs, code-behind vacío.
6. Confirmar AC-18: sin edit/delete, sin filtros UI, sin multi-moneda UI, sin `DataGrid`, sin modificar F-004/F-005, sin `Avalonia.Headless.XUnit`.
7. Smoke manual: `dotnet run --project src/ControlDeFinanzasIA.UI`; navegar a "Gastos"; registrar inválido → `TextBlock`s rojos; registrar válido → gasto aparece inmediatamente; cerrar/reabrir → sigue visible (SQLite).
8. Actualizar `spec/phases/phase-03/features.md`: F-007 a "✅ COMPLETADA". **No** renombrar spec a `.done.md` (eso es `sdd-archive`).
9. Marcar todas las tareas `[x]` en este archivo.

**Verificación:** Build exit 0; ~219 tests passed; NetArchTest R-1..R-5 verdes; format exit 0; smoke manual documentado.

**Dependencias:** T-001..T-011
**Estimación:** S (1 h)
**Trazabilidad:** AC-18, AC-19, AC-20

---

## Orden de ejecución recomendado

**Paralelismo en la base:** T-001, T-002, T-004 y T-011 son independientes.

```text
T-001 ─┐
T-002 ─┴──→ T-003 ─┐
T-004 ─────────────┤
                   ├──→ T-005 ───→ T-006 ─┐
T-011 ─────────────┤                     │
                                           │
                                          ├──→ T-012
                                           │
                                          ├──→ T-007
                                          ├──→ T-010
                                          └──→ T-008 ───→ T-009
```

T-005 es el habilitador central: bloquea T-006, T-007, T-008, T-010. T-011 puede trabajarse en paralelo pero debe terminarse antes de T-012 porque la vista y el VM dependen de las claves.

---

## Tareas bloqueantes / habilitadoras

| Tarea | Habilita | Razón |
| --- | --- | --- |
| T-001 | — | `Avalonia.Headless` en CPM; no bloquea lógica pero debe estar antes del build final. |
| T-002 | T-003 | `CategoryDto` es el tipo de retorno de la extensión. |
| T-003 | T-005 | `ExpensesViewModel` usa `category.ToDto()`. |
| T-004 | T-005, T-006 | `FakeCategoryRepository` inyecta `ICategoryRepository` en tests. |
| T-005 | T-006, T-007, T-008, T-010 | `ExpensesViewModel` es el centro de la feature. |
| T-006 | T-012 | Los tests deben pasar antes del cierre. |
| T-008 | T-009 | `ExpensesView` se referencia en el DataTemplate. |
| T-011 | T-008, T-012 | La vista y el VM usan strings localizados. |

---

## Estimación total

| Tarea | Dificultad | Horas |
| --- | --- | --- |
| T-001 | XS | 0.25 |
| T-002 | XS | 0.25 |
| T-003 | XS | 0.25 |
| T-004 | S | 0.5 |
| T-005 | L | 2–3 |
| T-006 | L | 2–3 |
| T-007 | XS | 0.25 |
| T-008 | M | 1–2 |
| T-009 | XS | 0.25 |
| T-010 | XS | 0.25 |
| T-011 | S | 0.5–1 |
| T-012 | S | 1 |
| **Total** | | **~10–14 h** |

---

## Riesgos y mitigaciones

| Riesgo | Prob. | Impacto | Mitigación |
| --- | --- | --- | --- |
| `DataTemplate` no resuelve con CompiledBindings → "Gastos" muestra shell sin contenido. | Media | Alto | `x:DataType="vm:ExpensesViewModel"` correcto en `App.axaml` y en el `UserControl`; smoke manual (AC-20) documenta el síntoma; warnings de bindings en build. |
| `ObservableCollection` mutada tras `await` en hilo UI. | Media | Medio | Avalonia captura `SynchronizationContext` del UI thread por defecto; smoke manual valida runtime. Tests xUnit corren sin sync context (ok unit). |
| `Avalonia.Headless` 11.2.7 no alinea con Avalonia/Fluent 11.2.7 → build roto. | Baja | Medio | Pin 11.2.7 exacto (=Avalonia); sin `XUnit` (DA #3) reduce superficie de integración; verificar `dotnet build` limpio. |
| `Error.Details` (`string[]` por campo) → `string.Join("; ", msgs)` colapsa múltiples mensajes. | Baja | Bajo | Documentado en AC-3; `RegisterExpenseValidator` emite 1 mensaje por regla. |
| `Strings.Designer.cs` no se regenera al añadir claves → localizer retorna key. | Media | Medio | Extender manualmente el designer con accessors (AC-14); `FakeStringLocalizer` fallback = key (tests robustos). |
| `Page`/`TotalPages` cambian pero `CanExecute` no se re-evalúa. | Media | Medio | Partial `OnPageChanged`/`OnTotalPagesChanged` → `NotifyCanExecuteChanged()` (AC-5/AC-6); tests E7-E9 cubren bordes. |
| `ICategoryRepository` no resuelto en DI al navegar a "Gastos". | Baja | Alto | `Bootstrap` ya registra `AddPersistence` (comentario líneas 41-44); verificar en apply. |
| Scope creep: añadir edit/delete/filtros "porque ya está la lista". | Baja | Medio | AC-18 lo prohíbe explícito; DP-4. |
| CategoryDto trim: brief sugería `Description`/`IsActive` pero `Category` no los tiene. | Resuelto | Bajo | Se usa forma mínima `(Guid Id, string Name)` (§4 #2 de la spec). Phase 4+ puede ampliar si la UI lo requiere. |
| `Tests.Unit.csproj` y `Avalonia.Headless`: el brief del orchestrator sugería añadirlo a tests, pero la spec final (AC-15/DA #3) lo restringe a `UI.csproj`. | Resuelto | Bajo | Seguir la spec: solo `UI.csproj` recibe el `PackageReference`; documentar en T-001 y notas para apply. |

---

## Notas para apply

- **No tocar `Domain`, `Infrastructure`, `RegisterExpenseUseCase`, `GetTransactionsQuery`, `Expense.cs`, `Result`, `PagedResult<T>`, `ExpenseDto`.** F-007 solo consume (DA #1).
- **`CommunityToolkit.Mvvm`:** `ExpensesViewModel` debe ser `public sealed partial class`; los partial `On*Changed` son generados y deben llamar `NotifyCanExecuteChanged()`.
- **`[RelayCommand(CanExecute = nameof(...))]`:** requiere método/miembro `bool` accesible. Refrescar con `NotifyCanExecuteChanged()` cuando cambien las propiedades observadas.
- **Botón "Registrar" siempre habilitado** (DA #2): `RegisterExpenseCommand` **sin** `CanExecute`. La validación es server-side (DP-2).
- **Fakes hand-rolled + use cases reales:** en tests, instanciar `RegisterExpenseUseCase` y `GetTransactionsQuery` como sealed concretos con `FakeExpenseRepository`, `FakeClock`, `FakeCategoryRepository`, `RegisterExpenseValidator`, `NullLogger<>`, `FakeStringLocalizer<Strings>`. Sin interfaces adicionales.
- **CPM:** versiones solo en `Directory.Packages.props`; `.csproj` sin `Version`.
- **`.resx` como `EmbeddedResource`:** ya configurado en `UI.csproj`; extender `Strings.Designer.cs` manualmente o regenerar con `ResXFileCodeGenerator`.
- **Code-behind vacío:** `ExpensesView.axaml.cs` solo llama `InitializeComponent()`.
- **NavigationService._routes:** añadir `{ RouteNames.Expenses, typeof(ExpensesViewModel) }` junto a `Dashboard`.
- **Bootstrap:** añadir `services.AddTransient<ExpensesViewModel>()`; `ICategoryRepository` ya viene por `AddPersistence`.
- **CategoryDto trim:** usar `public sealed record CategoryDto(Guid Id, string Name)`; no inventar `Description`/`IsActive`.
- **Tests pure xUnit:** `Assert.*`; sin FluentAssertions, sin mocking library, sin `Avalonia.Headless.XUnit`.
- **NetArchTest R-4:** `ExpensesViewModel` (UI) consume `Application`; no referencia `Infrastructure` directamente.
- **Smoke manual:** `dotnet run --project src/ControlDeFinanzasIA.UI`; validar registro válido/inválido y persistencia SQLite.
- **Cierre:** actualizar `spec/phases/phase-03/features.md` (F-007 ✅). **No renombrar** la spec a `.done.md` — eso corresponde a `sdd-archive`.

---

## Definition of Done (DoD)

- [x] AC-1: `ExpensesViewModel` `sealed partial : ViewModelBase` con 6 dependencias inyectadas. (T-005)
- [x] AC-2: `[ObservableProperty]` form + lista + errores completos. (T-005)
- [x] AC-3: `RegisterExpenseAsync` sin `CanExecute`, maneja `Validation` y otros errores. (T-005)
- [x] AC-4: `RefreshAsync` usa `GetTransactionsQueryRequest(Page, 20)` y lee `TotalPages`. (T-005)
- [x] AC-5: `PreviousPageCommand` con `CanExecute` reactivo. (T-005)
- [x] AC-6: `NextPageCommand` con `CanExecute` reactivo. (T-005)
- [x] AC-7: Constructor carga categorías y refresca la primera página. (T-005)
- [x] AC-8: `Date = _clock.Today` al construir. (T-005)
- [x] AC-9: `CategoryDto` + `CategoryExtensions.ToDto`. (T-002, T-003)
- [x] AC-10: `ExpensesView.axaml` con form, lista 4 cols, paginación, errores; code-behind vacío. (T-008)
- [x] AC-11: `x:DataType="vm:ExpensesViewModel"` en `UserControl`. (T-008)
- [x] AC-12: `App.axaml` registra DataTemplate `ExpensesViewModel → ExpensesView`. (T-009)
- [x] AC-13: `Bootstrap` registra `ExpensesViewModel`; `NavigationService._routes` añade `Expenses`. (T-007, T-010)
- [x] AC-14: `.resx` + designer con claves UI. (T-011)
- [x] AC-15: `Avalonia.Headless` 11.2.7 en CPM + `UI.csproj`; sin `Avalonia.Headless.XUnit`; sin referencia en `Tests.Unit.csproj`. (T-001)
- [x] AC-16: ≥ 7 tests en `ExpensesViewModelTests` pure `Assert.*`. (T-006)
- [x] AC-17: `FakeCategoryRepository` nuevo; reusa fakes existentes + use cases reales. (T-004, T-006)
- [x] AC-18: Sin scope creep. (T-012)
- [x] AC-19: NetArchTest R-1..R-5 verdes; botón sin `CanExecute`. (T-012)
- [x] AC-20: Build limpio, ~219 tests verdes, format exit 0, smoke manual. (T-012)

---

## Referencias

- `spec/features/phase-03/F-007-expenses-viewmodel-view.spec.md`
- `spec/phases/phase-03/features.md`
- `spec/features/phase-03/F-006-ui-shell-navigation.done.md`
- `spec/features/phase-03/F-004-register-expense-use-case.done.md`
- `spec/features/phase-03/F-005-get-transactions-query.done.md`
- `spec/constitution/tech-stack.md`
- `.claude/skills/frontend-design/SKILL.md`
- `docs/architecture.md` §9, §10, §15
- `docs/vision.md` §4.1, §4.3
