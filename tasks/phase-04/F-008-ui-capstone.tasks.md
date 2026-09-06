# Tareas — F-008 (Fase 4): UI capstone — `IncomeView` + filtros y vistas guardadas en `ExpensesView` + `GetIncomesQuery` + `DeleteSavedViewUseCase` + `CreateTagUseCase` + design pass BOLD "Libro Mayor"

> **Spec:** `spec/features/phase-04/F-008-ui-capstone.spec.md`
> **change_id:** `f-008p4-ui-capstone`
> **Estado:** Pendiente de implementación
> **Fecha:** 2026-09-04
> **Modo:** Standard (SDD: propose → spec → tasks → apply → verify → archive)
> **Fuentes de verdad:** spec, `.atl/sdd/f-008p4-ui-capstone/proposal.md`,
> `spec/features/phase-04/F-006-saved-view-filter-json.done.md`,
> `spec/features/phase-04/F-005-update-delete-use-cases.done.md`,
> `spec/features/phase-04/F-007-extend-filters-tagids.done.md`,
> `spec/phases/phase-04/features.md`, skill `frontend-design`, `AGENTS.md`.
> Cada tarea es implementable en una sesión. La numeración (T-001...) es local a F-008.

---

## Pronóstico de carga de review

| Field | Value |
|-------|-------|
| Estimated changed lines | ~2,100 |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1: T-001..T-006 (Application + tests) ~600 líneas; PR 2: T-007..T-014 (IncomeView + navegación + tests) ~650 líneas; PR 3: T-015..T-018 (ExpensesView extensión + tests + smoke) ~520 líneas; PR 4: T-019..T-027 (design pass BOLD + localización + gates) ~460 líneas |
| Delivery strategy | auto-forecast |
| Chain strategy | stacked-to-main |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: High

### Unidades de trabajo sugeridas

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Application layer: DTOs, queries, use cases `CreateTag`/`DeleteSavedView`, extensión `GetTransactionsQuery`, fakes y tests unit | PR 1 | Base = `main`; autónomo, verificable, con rollback propio |
| 2 | IncomeView + navegación + VM tests | PR 2 | Depende de PR 1 (tipos Application) |
| 3 | Extensión de ExpensesView (VM + XAML + tests + smoke) | PR 3 | Depende de PR 1 y PR 2 |
| 4 | Design pass BOLD: fuentes, tokens, estilos, motion, localización, gates | PR 4 | Depende de PR 2 y PR 3; capa visual pura |

---

## Resumen

F-008 cierra Phase 4 por el lado del usuario: cablea el CRUD completo de ingresos (`IncomeView`), los filtros y vistas guardadas en gastos, los dos orquestadores que faltaban (`GetIncomesQuery`, `DeleteSavedViewUseCase`, `CreateTagUseCase`) y un pase de diseño BOLD "Libro Mayor". Todo bajo MVVM estricto, bindings compilados, localización es-DO/neutral y pure xUnit sin mocking.

---

## Resumen de tareas

| ID | Tarea | Descripción corta | Estimación diff | Depende de | Verificación |
| --- | --- | --- | --- | --- | --- |
| T-001 | Crear `IncomeDto` + `IncomeExtensions` | Espejo de `ExpenseDto`/`ExpenseExtensions`; sin Tags | +45 | — | build exit 0 |
| T-002 | Crear `GetIncomesQuery` + `GetIncomesQueryRequest` | Clase query 2 deps, filtros día 1, `Value = PagedResult<IncomeDto>` | +80 | T-001 | tests GI pasan |
| T-003 | Crear `CreateTagCommand`/`Validator`/`UseCase` | 4 deps, validación sync, mensajes español | +90 | — | tests CT pasan |
| T-004 | Crear `DeleteSavedViewCommand`/`UseCase` | Espejo `DeleteTransactionUseCase`; guard + propagación | +50 | — | tests DS pasan |
| T-005 | Extender `GetTransactionsQueryRequest`/`Query` | +5 params opcionales + mapeo de filtros | +30 | — | tests RE pasan |
| T-006 | Test infra + tests de orquestadores | `FakeTagRepository`, extender `FakeIncome`/`FakeExpense`, GI/DS/CT/RE | +330 | T-001..T-005 | todos los tests nuevos pasan |
| T-007 | Crear `SelectableTagItem` | Wrapper observable para picker multi-tag | +30 | — | build exit 0 |
| T-008 | Crear `IncomeViewModel` | Sealed partial, 14 deps, CRUD + filtros + vistas guardadas | +230 | T-002, T-003, T-004, T-007 | build exit 0 |
| T-009 | Crear `IncomeView.axaml` | Riel + ledger + paginación; compiled bindings | +140 | T-008 | XAML compila |
| T-010 | Modificar `RouteNames` + `NavigationService` | Ruta `Incomes` → `IncomeViewModel` | +15 | T-008 | navegación resuelve |
| T-011 | Modificar `ShellViewModel` | Añadir `SidebarIncomes` en `MenuItems` | +5 | T-010 | sidebar muestra item |
| T-012 | Modificar `App.axaml` DataTemplate | DataTemplate dual `IncomeViewModel` | +20 | T-009 | compiled bindings |
| T-013 | Modificar `Bootstrap.cs` | Registrar `IncomeViewModel`, `GetIncomesQuery`, `DeleteSavedViewUseCase`, `CreateTagUseCase`, validator | +15 | T-002..T-004 | DI resuelve VM |
| T-014 | Crear `IncomeViewModelTests` | ≥6 tests de VM con fakes + use cases reales | +160 | T-008, T-013 | tests IV pasan |
| T-015 | Extender `ExpensesViewModel` | +8 deps, filtros, vistas guardadas, edit/delete, tag inline | +200 | T-003, T-004, T-006, T-013 | build exit 0 |
| T-016 | Extender `ExpensesView.axaml` | Riel de filtros + ledger + botones de fila | +160 | T-015 | XAML compila |
| T-017 | Extender `ExpensesViewModelTests` | ≥4 tests nuevos; regresión de los 10 existentes | +100 | T-015 | tests EV pasan |
| T-018 | Smoke manual PR-3 | Navegar a Ingresos/Gastos, flujos básicos sin excepciones | 0 | T-009, T-016 | smoke exitoso |
| T-019 | Fuentes OFL embebidas | 3 TTF variables + licencias en `Assets/Fonts/` | (binario) | — | assets presentes |
| T-020 | Modificar `.csproj` para fuentes | `<AvaloniaResource Include="Assets\Fonts\**" />` | +10 | T-019 | build con recursos |
| T-021 | Crear `Styles/Tokens.axaml` | Paleta, FontFamily, spacing, motion, radii | +70 | T-019 | recursos resuelven |
| T-022 | Crear `Styles/LedgerStyles.axaml` | Estilos `.ledger-*` con classes explícitas | +100 | T-021 | visual Libro Mayor |
| T-023 | Crear `Styles/Motion.axaml` | RowReveal, AmberTick, transitions | +80 | T-021 | motion sin code-behind |
| T-024 | Aplicar estilos/tokens a vistas + App.axaml | `RequestedThemeVariant="Dark"`, ResourceInclude Tokens, StyleInclude en vistas | +50 | T-021..T-023, T-009, T-016 | visual cohesivo |
| T-025 | Añadir keys de localización | ~28 keys ×3 archivos (`Strings.resx`, `Strings.es-DO.resx`, `Strings.Designer.cs`) | +100 | — | cultura es-DO/neutral |
| T-026 | Migrar strings hardcodeados restantes | Watermarks y "Página X de Y" a recursos | +50 | T-025 | 0 literales español |
| T-027 | Gates finales | Build, tests, format, NetArchTest, smoke manual + screenshots | 0 | T-001..T-026 | todos los gates verdes |

**Total estimado:** ~2,100 líneas cambiadas.

---

## PR-1 — Application layer + infra de tests

### [x] T-001 — Crear `Application/DTOs/IncomeDto.cs` + `Application/DTOs/IncomeExtensions.cs`

**AC refs:** AC-1

**Pre-condiciones**
- `src/ControlDeFinanzasIA.Application/DTOs/ExpenseDto.cs` y `ExpenseExtensions.cs` verificados como patrón.
- `Income` expone `CategoryId` nullable, `Source`, `Amount` (Money), timestamps.

**Pasos**
1. Crear `src/ControlDeFinanzasIA.Application/DTOs/IncomeDto.cs` con record 10 props (`Id`, `Guid? CategoryId`, `Date`, `Amount`, `Currency`, `Description`, `Note`, `Source`, `CreatedAt`, `UpdatedAt`) y XML doc en inglés.
2. Crear `src/ControlDeFinanzasIA.Application/DTOs/IncomeExtensions.cs` con `public static IncomeDto ToDto(this Income income)` que mapee línea a línea (espejo `ExpenseExtensions`).
3. Ejecutar `dotnet build -c Debug --nologo`.

**Criterios de verificación**
- `dotnet build` exit 0, 0 warnings.
- `IncomeDto` no lleva colección `Tags`.
- `income.ToDto()` produce props iguales incluyendo `CategoryId == null` y `Source`.

**Notas**
- D-3: ubicación fija en `Application/DTOs/`, no en `UseCases/Incomes/`.
- D-3: `Currency` es el enum `ControlDeFinanzasIA.Domain.Enums.Currency`.

---

### [x] T-002 — Crear `Application/UseCases/Incomes/GetIncomes/GetIncomesQueryRequest.cs` + `GetIncomesQuery.cs`

**AC refs:** AC-2, D-1, RF-17

**Pre-condiciones**
- `GetTransactionsQuery`/`GetTransactionsQueryRequest` leídos como patrón.
- `IncomeFilter` tiene 6 props `init` con `TagIds`.

**Pasos**
1. Crear `GetIncomesQueryRequest.cs` como record posicional `(int Page = 1, int PageSize = 50, Guid? CategoryId = null, DateOnly? DateFrom = null, DateOnly? DateTo = null, IReadOnlyList<Guid>? TagIds = null)` con XML doc.
2. Crear `GetIncomesQuery.cs` como `sealed class` con 2 deps (`IIncomeRepository`, `ILogger<GetIncomesQuery>`), `ExecuteAsync(request, ct)`.
3. Implementar validación manual: `Page < 1`, `PageSize <= 0`, `PageSize > 200`, `DateFrom > DateTo`, `TagIds` con `Guid.Empty` → `Result.Failure("InvalidFilter", "...")` en español.
4. Mapear 1:1 a `IncomeFilter`; try/catch → `RepositoryError`; `Value = PagedResult<IncomeDto>`.
5. Ejecutar `dotnet build` y tests de GI cuando estén disponibles.

**Criterios de verificación**
- `dotnet build` exit 0.
- Request válido con filtros produce `LastIncomeFilter` idéntico (AC-6).
- Request inválido no llama al repo (`GetByFilterCallCount == 0`).
- Excepción en repo → `Failure("RepositoryError")` sin propagar excepción.

**Notas**
- D-1: NO crear `GetIncomesUseCase`; estructura query-clase + request-record.
- Mensajes de validación en español, códigos en inglés (D-ML).

---

### [x] T-003 — Crear `Application/UseCases/Catalog/CreateTag/CreateTagCommand.cs`, `CreateTagValidator.cs` y `CreateTagUseCase.cs`

**AC refs:** AC-5, D-5

**Pre-condiciones**
- `Tag.Create(name, color, createdAt)` requiere `createdAt != default`.
- `RegisterIncomeUseCase`/`RegisterIncomeValidator` como patrón de validación sync + details.

**Pasos**
1. Crear `CreateTagCommand.cs`: `public sealed record CreateTagCommand(string Name, string? Color)` con XML doc.
2. Crear `CreateTagValidator.cs`: 2 reglas sync en español — `Name` no vacío post-trim, max 50; `Color` max 9.
3. Crear `CreateTagUseCase.cs`: 4 deps (`ITagRepository`, `IClock`, `IValidator<CreateTagCommand>`, `ILogger<>`). Flow: `ThrowIfNull` → `Validate` sync con details → `Tag.Create` con catch `ArgumentException` → `DomainInvariant` → `AddAsync` → propagación verbatim.
4. Ejecutar `dotnet build`.

**Criterios de verificación**
- `dotnet build` exit 0.
- Comando válido persiste tag con `CreatedAt == clock.UtcNow` y `Name` trimado.
- `Name` vacío/whitespace/>50 o `Color` >9 → `Failure("Validation")` con `Details` y `AddedTags` vacío.
- `AddResultOverride = Failure("DuplicateName")` se propaga verbatim.

**Notas**
- D-5: elevado a use case; NO repo-direct desde VM.
- Validación sync (`_validator.Validate`) para evitar `AsyncValidatorInvokedSynchronouslyException`.

---

### [x] T-004 — Crear `Application/UseCases/SavedViews/DeleteSavedView/DeleteSavedViewCommand.cs` + `DeleteSavedViewUseCase.cs`

**AC refs:** AC-4, D-4

**Pre-condiciones**
- `DeleteTransactionUseCase` leído como espejo.
- `ISavedViewRepository.RemoveAsync` ya existe y resuelve `NotFound`.

**Pasos**
1. Crear `DeleteSavedViewCommand.cs`: `public sealed record DeleteSavedViewCommand(Guid SavedViewId)` con XML doc.
2. Crear `DeleteSavedViewUseCase.cs`: 2 deps (`ISavedViewRepository`, `ILogger<>`); `ThrowIfNull` → guard `Guid.Empty` → `Failure("Validation", "Id cannot be empty.")` → `RemoveAsync` → propagación verbatim.
3. Ejecutar `dotnet build`.

**Criterios de verificación**
- `dotnet build` exit 0.
- Vista existente → `IsSuccess` y desaparece del fake.
- `Guid.Empty` → `Failure("Validation")` sin llamar al repo.
- `RemoveResultOverride = Failure("NotFound")` → propaga `"NotFound"`.

**Notas**
- D-4: paga D-8 de F-006; sin validator (espejo `DeleteTransactionUseCase`).
- Doc-comments en inglés (familia SavedViews).

---

### [x] T-005 — Extender `GetTransactionsQueryRequest` y `GetTransactionsQuery` con filtros opcionales

**AC refs:** AC-3, D-2

**Pre-condiciones**
- T-002 completado (mismo patrón de request/query).
- `FakeExpenseRepository` ya captura `FilterResult`/`GetByFilterCallCount`.

**Pasos**
1. En `GetTransactionsQueryRequest.cs`, añadir 5 params opcionales: `Guid? CategoryId = null`, `DateOnly? DateFrom = null`, `DateOnly? DateTo = null`, `IReadOnlyList<Guid>? TagIds = null`. Actualizar doc-comment.
2. En `GetTransactionsQuery.cs`, reemplazar `CategoryId = null, DateFrom = null, DateTo = null` por mapeo del request incluyendo `TagIds`.
3. Añadir guards `DateFrom > DateTo` y `TagIds` con `Guid.Empty` entre validación de `PageSize` y construcción del filtro.
4. Ejecutar `dotnet build`.

**Criterios de verificación**
- `dotnet build` exit 0; tests existentes de `GetTransactionsQueryTests` siguen pasando.
- Request con filtros produce `LastExpenseFilter` con las 6 props iguales al request.
- Request sin filtros produce `LastExpenseFilter` con filtros `null` (comportamiento pre-F-008).

**Notas**
- Defaults `null` = aditivo puro; callers existentes no requieren cambios.
- D-2: doc-comment "los filtros… llegarán en Phase 4" se actualiza.

---

### [x] T-006 — Infra de tests + tests unit de orquestadores

**AC refs:** AC-6, AC-26

**Pre-condiciones**
- T-001..T-005 completados.
- `FakeExpenseRepository` ya tiene `FilterResult`/`SetFilterResult`/`SetThrowOnGetFilter`.

**Pasos**
1. Crear `tests/ControlDeFinanzasIA.Tests.Unit/TestDoubles/FakeTagRepository.cs`: espejo `FakeSavedViewRepository`, 5 métodos, `AddedTags`, overrides.
2. Extender `FakeIncomeRepository.cs`: añadir `FilterResult`, `GetByFilterCallCount`, `LastIncomeFilter`, `SetFilterResult` ×2, `SetThrowOnGetFilter`; modificar `GetByFilterAsync` para usarlo.
3. Extender `FakeExpenseRepository.cs`: añadir `LastExpenseFilter` capturado en `GetByFilterAsync`.
4. Crear `tests/ControlDeFinanzasIA.Tests.Unit/Application/Incomes/GetIncomes/GetIncomesQueryTests.cs` con ≥5 tests (GI-1..GI-5).
5. Crear `tests/ControlDeFinanzasIA.Tests.Unit/Application/SavedViews/DeleteSavedView/DeleteSavedViewUseCaseTests.cs` con ≥4 tests (DS-1..DS-4).
6. Crear `tests/ControlDeFinanzasIA.Tests.Unit/Application/Catalog/CreateTag/CreateTagUseCaseTests.cs` con ≥4 tests (CT-1..CT-4/5).
7. Extender `tests/ControlDeFinanzasIA.Tests.Unit/Application/Expenses/GetTransactionsQueryTests.cs` con ≥3 tests (RE-1..RE-3).
8. Ejecutar `dotnet build` y `dotnet test --filter "..."` para los 4 filtros.

**Criterios de verificación**
- `dotnet build` exit 0.
- Todos los tests nuevos pasan; tests existentes no regresan.
- `FakeTagRepository` compila e implementa `ITagRepository`.

**Notas**
- Pure xUnit `Assert.*`; sin mocking/FluentAssertions.
- Fakes no implementan semántica OR de tags; blindan el mapeo (F-007 ya cubre la query real).

---

## PR-2 — IncomeView + navegación

### [x] T-007 — Crear `UI/ViewModels/SelectableTagItem.cs`

**AC refs:** AC-8, D-8

**Pre-condiciones**
- `ViewModelBase` usa `CommunityToolkit.Mvvm`.

**Pasos**
1. Crear `src/ControlDeFinanzasIA.UI/ViewModels/SelectableTagItem.cs`.
2. `public sealed partial class SelectableTagItem : ObservableObject` con `Id`, `Name`, `Color?` (init) y `[ObservableProperty] private bool _isSelected;`.
3. Ejecutar `dotnet build`.

**Criterios de verificación**
- `dotnet build` exit 0.
- `IsSelected` es observable y testeable sin UI.

**Notas**
- D-8: wrapper observable para evitar sincronización cruda de `ListBox.SelectedItems`.

---

### [x] T-008 — Crear `UI/ViewModels/IncomeViewModel.cs`

**AC refs:** AC-7, D-6, D-7, D-8, D-11

**Pre-condiciones**
- T-002, T-003, T-004, T-007 completados.
- `ExpensesViewModel` leído como patrón de sealed partial + `[ObservableProperty]` + `[RelayCommand]`.

**Pasos**
1. Crear `IncomeViewModel.cs` como `public sealed partial class IncomeViewModel : ViewModelBase`.
2. Ctor con 14 deps (todos con `ThrowIfNull` + readonly): `RegisterIncomeUseCase`, `UpdateIncomeUseCase`, `DeleteTransactionUseCase`, `GetIncomesQuery`, `SaveCurrentFilterUseCase`, `ApplySavedViewUseCase`, `GetSavedViewsUseCase`, `DeleteSavedViewUseCase`, `CreateTagUseCase`, `ICategoryRepository`, `ITagRepository`, `IClock`, `ILogger<IncomeViewModel>`, `IStringLocalizer<Strings>`.
3. Props observables: form (`Date`, `Amount`, `Currency`, `SelectedCategoryId`, `Description`, `Note`), lista (`Incomes`, `SelectedIncome`, `IsEditMode`, `EditableId`, `PendingDeleteIncomeId`), paginación (`Page`, `TotalPages`, `CanGoToPreviousPage`, `CanGoToNextPage`, `PageSummary`), errores (`ValidationErrors`, `FormError`, `StatusMessage`), filtro (`FilterCategoryId`, `FilterDateFrom`, `FilterDateTo`, `CurrentFilter`), tags (`AvailableTags`, `NewTagName`, `NewTagColor`, `TagCreationError`), vistas guardadas (`SavedViews`, `SelectedSavedView`, `NewViewName`, `PendingDeleteViewId`).
4. Comandos: `Refresh`, `RegisterIncome`, `BeginEdit`, `UpdateIncome`, `CancelEdit`, `DeleteIncome` (2 pasos), `ApplyFilter`, `ClearFilter`, `SaveView`, `ApplySavedView`, `DeleteSavedView`, `CreateTag`, `PreviousPage`, `NextPage`.
5. Ctor inicializa `_date = _clock.Today`, `CurrentFilter = new IncomeFilter { Page = 1, PageSize = 20 }`, y fire-and-forget `LoadCategoriesAsync`, `LoadTagsAsync`, `RefreshAsync`, `LoadSavedViewsAsync`.
6. Ejecutar `dotnet build`.

**Criterios de verificación**
- `dotnet build` exit 0.
- Constructor setea defaults correctos (`Page == 1`, `TotalPages == 0`, `Date == clock.Today`).
- `ApplyFilter` resetea `Page = 1` y `CurrentFilter` refleja props observables + tags seleccionados.
- `DeleteIncome` requiere 2 invocaciones: 1ª arma pendiente, 2ª ejecuta.

**Notas**
- D-6: 14 deps es la consecuencia de espejar + filtros + vistas + tags.
- D-7: cada comando resetea estados pendientes; sin `MessageBox` ni code-behind.
- `PageSummary` usa `_localizer["PageOfPagesFormat"]`.

---

### [x] T-009 — Crear `UI/Views/IncomeView.axaml` + code-behind mínimo

**AC refs:** AC-8, §4

**Pre-condiciones**
- T-007 y T-008 completados.
- `DateOnlyConverter` ya existe.

**Pasos**
1. Crear `IncomeView.axaml` con `x:Class="ControlDeFinanzasIA.UI.Views.IncomeView"` y `x:DataType="vm:IncomeViewModel"`.
2. Layout 2 regiones: riel izquierdo (~280px) con filtros, vistas guardadas y creación de tag; ledger derecho con cabecera Fraunces + regla ámbar, form "nuevo asiento" y lista de ingresos.
3. Usar `x:Static res:Strings.*` para todos los textos; bindings con `DateOnlyConverter` para fechas.
4. DataTemplates con `x:DataType` correcto (`dto:IncomeDto`, `SelectableTagItem`, `SavedView`, `CategoryDto`).
5. Botones de fila: `Command="{Binding $parent[UserControl].BeginEditCommand}" CommandParameter="{Binding}"`.
6. Crear `IncomeView.axaml.cs` mínimo (plantilla generada, sin lógica).
7. Ejecutar `dotnet build`.

**Criterios de verificación**
- `dotnet build` exit 0, sin warnings de compiled bindings.
- XAML renderiza sin `InvalidOperationException`.
- Todos los textos salen de recursos (cero español hardcodeado).

**Notas**
- PR-2 es funcional-first; el design pass de PR-4 aplica estilos/motion.
- D-12: `UserControl.Styles` con `StyleInclude` de `LedgerStyles.axaml` + `Motion.axaml` se deja preparado o se completa en PR-4.

---

### [x] T-010 — Modificar `RouteNames.cs` + `NavigationService.cs`

**AC refs:** AC-16, D-9

**Pre-condiciones**
- T-008 completado (`IncomeViewModel` existe).

**Pasos**
1. En `RouteNames.cs`, añadir después de `Expenses`:
   ```csharp
   /// <summary>The incomes route (consumed by F-008 of Phase 4).</summary>
   public const string Incomes = "Incomes";
   ```
2. En `NavigationService.cs`, añadir al diccionario del ctor: `{ RouteNames.Incomes, typeof(IncomeViewModel) }`.
3. Ejecutar `dotnet build`.

**Criterios de verificación**
- `dotnet build` exit 0.
- `_navService.NavigateTo(RouteNames.Incomes)` resuelve `IncomeViewModel` sin excepción.

**Notas**
- D-9: 4 ediciones puntuales de wiring; `MainWindow.axaml` no se toca.

---

### [x] T-011 — Modificar `ShellViewModel.cs` para añadir "Ingresos"

**AC refs:** AC-17, D-9

**Pre-condiciones**
- T-010 completado.
- Key `SidebarIncomes` disponible en resx (se añade en T-025; en PR-2 se puede dejar stub o añadir key mínima).

**Pasos**
1. En `ShellViewModel.cs`, añadir al collection-initializer de `MenuItems` después de Expenses:
   ```csharp
   new RouteMenuItem(RouteNames.Incomes, _localizer["SidebarIncomes"]),
   ```
2. Ejecutar `dotnet build`.

**Criterios de verificación**
- `dotnet build` exit 0.
- `MenuItems` contiene 3 elementos; el tercero usa `RouteNames.Incomes`.

**Notas**
- El sidebar es data-driven; `MainWindow.axaml` no cambia.

---

### [x] T-012 — Modificar `App.axaml` DataTemplate para `IncomeViewModel`

**AC refs:** AC-18, D-9

**Pre-condiciones**
- T-009 completado (`IncomeView` existe).

**Pasos**
1. En `App.axaml`, dentro de `Application.DataTemplates`, añadir después del de `ExpensesViewModel`:
   ```xml
   <DataTemplate DataType="{x:Type vm:IncomeViewModel}"
                 x:DataType="vm:IncomeViewModel">
       <views:IncomeView />
   </DataTemplate>
   ```
2. Ejecutar `dotnet build`.

**Criterios de verificación**
- `dotnet build` exit 0, sin warnings de compiled bindings.
- DataTemplate dual (`DataType` + `x:DataType`) presente.

**Notas**
- El cambio de `RequestedThemeVariant` y `ResourceInclude` de Tokens va en PR-4 (T-024).

---

### [x] T-013 — Modificar `Bootstrap.cs` para registrar componentes nuevos

**AC refs:** AC-19, D-5

**Pre-condiciones**
- T-002, T-003, T-004, T-008 completados.

**Pasos**
1. Añadir usings (orden alfabético):
   - `ControlDeFinanzasIA.Application.UseCases.Catalog.CreateTag`
   - `ControlDeFinanzasIA.Application.UseCases.Incomes.GetIncomes`
   - `ControlDeFinanzasIA.Application.UseCases.SavedViews.DeleteSavedView`
2. Añadir registros Transient:
   ```csharp
   services.AddTransient<IncomeViewModel>();
   services.AddTransient<GetIncomesQuery>();
   services.AddTransient<DeleteSavedViewUseCase>();
   services.AddTransient<CreateTagUseCase>();
   services.AddTransient<IValidator<CreateTagCommand>, CreateTagValidator>();
   ```
3. Actualizar XML doc/remarks de `ConfigureServices`.
4. Ejecutar `dotnet build`.

**Criterios de verificación**
- `dotnet build` exit 0.
- El composition root resuelve `IncomeViewModel` con sus 14 deps.

**Notas**
- `SaveCurrentFilterUseCase`, `ApplySavedViewUseCase`, `GetSavedViewsUseCase`, `UpdateExpenseUseCase`, `DeleteTransactionUseCase` ya están registrados.

---

### [x] T-014 — Crear `Tests.Unit/UI/IncomeViewModelTests.cs`

**AC refs:** AC-25, §7.4

**Pre-condiciones**
- T-008 y T-013 completados.
- Fakes disponibles: `FakeIncomeRepository`, `FakeCategoryRepository`, `FakeTagRepository`, `FakeSavedViewRepository`, `FakeClock`, `FakeStringLocalizer`, `NullLogger`.

**Pasos**
1. Crear `tests/ControlDeFinanzasIA.Tests.Unit/UI/IncomeViewModelTests.cs`.
2. Helper `CreateViewModel(...)` que construya las 14 deps con fakes + use cases/query reales + validators reales + `NullLogger<>` + `FakeStringLocalizer<Strings>`.
3. Implementar ≥6 tests: `Constructor_InitializesDefaults_AndLoadsCatalogs`, `Refresh_PopulatesIncomesAndPageMetadata`, `RegisterIncome_ValidForm_CallsUseCase_AndRefreshesList`, `ApplyFilter_BuildsFilterFromProps_ResetsPageAndReloads`, `ApplySavedView_UpdatesFilterProps_AndResetsPage`, `SaveView_WithValidName_CallsUseCase_AndReloadsViews`, `DeleteIncome_TwoStepConfirm_DeletesOnSecondInvocation`, `DeleteIncome_OnRepositoryError_ShowsFormError`.
4. Ejecutar `dotnet test --filter "FullyQualifiedName~IncomeViewModelTests"`.

**Criterios de verificación**
- Tests pasan; aserciones puras `Assert.*`.
- `FakeStringLocalizer` retorna key para asserts de mensajes localizados.

**Notas**
- IV-1..IV-9 del spec; cubrir happy path, filtros, vistas guardadas y confirmación 2 pasos.

---

## PR-3 — Extensión de ExpensesView

### [x] T-015 — Extender `ExpensesViewModel.cs` con filtros, vistas guardadas, edit/delete y tag inline

**AC refs:** AC-9, D-2, D-6, D-7, D-8, D-11

**Pre-condiciones**
- T-003, T-004, T-006, T-013 completados (use cases `CreateTag`, `DeleteSavedView` y fakes listos).
- `ExpensesViewModel` actual leído.

**Pasos**
1. Añadir 8 deps al ctor: `UpdateExpenseUseCase`, `DeleteTransactionUseCase`, `SaveCurrentFilterUseCase`, `ApplySavedViewUseCase`, `GetSavedViewsUseCase`, `DeleteSavedViewUseCase`, `CreateTagUseCase`, `ITagRepository`.
2. Añadir props observables espejo de `IncomeViewModel`: `SelectedExpense`, `IsEditMode`, `EditableId`, `PendingDeleteExpenseId`, `StatusMessage`, `FilterCategoryId`, `FilterDateFrom`, `FilterDateTo`, `CurrentFilter`, `AvailableTags`, `NewTagName`, `NewTagColor`, `TagCreationError`, `SavedViews`, `SelectedSavedView`, `NewViewName`, `PendingDeleteViewId`, `PageSummary`.
3. Comandos nuevos: `UpdateExpense`, `BeginEdit`, `CancelEdit`, `DeleteExpense`, `ApplyFilter`, `ClearFilter`, `SaveView`, `ApplySavedView`, `DeleteSavedView`, `CreateTag`.
4. Modificar `RefreshAsync` para usar `new GetTransactionsQueryRequest(Page, 20, CurrentFilter.CategoryId, CurrentFilter.DateFrom, CurrentFilter.DateTo, SelectedTagIds)` y setear `PageSummary`.
5. Inicializar `CurrentFilter` en ctor y fire-and-forget `LoadTagsAsync`/`LoadSavedViewsAsync`.
6. Ejecutar `dotnet build`.

**Criterios de verificación**
- `dotnet build` exit 0.
- Los 10 tests existentes de `ExpensesViewModelTests` siguen pasando sin modificar sus asserts.
- `ApplyFilter` produce `LastExpenseFilter` con las props correctas y `Page == 1`.

**Notas**
- D-11: `ApplyFilter`/`ApplySavedView`/`ClearFilter` resetean `Page = 1`.
- `DeleteExpense` usa `DeleteTransactionCommand(id, TransactionType.Expense)`.

---

### [x] T-016 — Extender `ExpensesView.axaml` con riel de filtros y ledger

**AC refs:** AC-10, §4

**Pre-condiciones**
- T-015 completado.

**Pasos**
1. Reestructurar `ExpensesView.axaml` al layout 2 regiones (riel ~280px + ledger).
2. Conservar bindings del form de registro existente; añadir bindings de filtros, vistas guardadas, tags, botones de fila, `StatusMessage`, `PageSummary`.
3. Migrar watermarks hardcodeados a `x:Static res:Strings.*` (`SelectCategoryWatermark`, `DescriptionWatermark`, `NoteWatermark`).
4. Reemplazar "Página X de Y" hardcodeado por `Text="{Binding PageSummary}"`.
5. Añadir `UserControl.Styles` con `StyleInclude` de `LedgerStyles.axaml` + `Motion.axaml` (o placeholder para PR-4).
6. Ejecutar `dotnet build`.

**Criterios de verificación**
- `dotnet build` exit 0, sin warnings de compiled bindings.
- Todos los DataTemplates nuevos tienen `x:DataType` correcto.
- Cero literales en español en las secciones reescritas.

**Notas**
- PR-3 es funcional-first; el look BOLD final se aplica en PR-4.

---

### [x] T-017 — Extender `Tests.Unit/UI/ExpensesViewModelTests.cs`

**AC refs:** AC-25, §7.5

**Pre-condiciones**
- T-015 completado.

**Pasos**
1. Extender el helper `CreateViewModel` con las 8 deps nuevas.
2. Añadir ≥4 tests: `ApplyFilter_BuildsExpenseFilter_FromObservableProps`, `SaveView_WithDuplicateName_ShowsStatusError_WithoutException`, `ApplySavedView_DeserializesFilter_SyncsPropsAndResetsPage`, `DeleteSavedView_TwoStepConfirm_RemovesViewAndRefreshes`, `CreateTag_WithValidName_RefreshesAvailableTags`, `DeleteExpense_TwoStepConfirm_CallsTransactionDeleteWithExpenseType`.
3. Ejecutar `dotnet test --filter "FullyQualifiedName~ExpensesViewModelTests"`.

**Criterios de verificación**
- Los 10 tests existentes siguen pasando.
- Los nuevos tests pasan.

**Notas**
- EV-1..EV-6 del spec; `FakeStringLocalizer` retorna key.

---

### [x] T-018 — Smoke manual de navegación y flujos básicos

**AC refs:** AC-28 parcial

**Pre-condiciones**
- T-009 y T-016 completados.

**Pasos**
1. `dotnet run --project src/ControlDeFinanzasIA.UI`.
2. Verificar que el sidebar muestra "Ingresos" y navega a `IncomeView`.
3. En Ingresos: registrar un ingreso, ver que aparece en la lista, editar, eliminar en 2 pasos.
4. En Gastos: aplicar filtro combinado, guardar vista, aplicar vista, eliminar vista en 2 pasos.
5. Verificar que no hay excepciones ni estados muertos.

**Criterios de verificación**
- App arranca y los flujos principales funcionan end-to-end.
- No se requieren screenshots en este punto (van en T-027).

**Notas**
- Este smoke es previo al design pass; se repite con screenshots en T-027.

---

## PR-4 — Design pass BOLD "Libro Mayor"

### [x] T-019 — Adquirir e incrustar fuentes OFL `.ttf` en `UI/Assets/Fonts/`

**AC refs:** AC-23, §4.1

**Pre-condiciones**
- Conexión a upstream OFL (Google Fonts o fuentes oficiales).

**Pasos**
1. Descargar TTF variables OFL:
   - `Fraunces-VariableFont.ttf`
   - `InstrumentSans-VariableFont.ttf`
   - `SplineSansMono-VariableFont.ttf`
2. Incluir licencias OFL: `OFL-Fraunces.txt`, `OFL-InstrumentSans.txt`, `OFL-SplineSansMono.txt`.
3. Colocar todo en `src/ControlDeFinanzasIA.UI/Assets/Fonts/`.

**Criterios de verificación**
- Los 3 archivos `.ttf` y las licencias están en `Assets/Fonts/`.
- Formatos válidos: `.ttf`/`.otf`; NO `.woff2`.

**Notas**
- Si una familia no está disponible como TTF, pedir aprobación del usuario antes de sustituir.
- D-15: fuentes `.ttf` (OFL), no `.woff2`.

---

### [x] T-020 — Modificar `ControlDeFinanzasIA.UI.csproj` para incluir fuentes

**AC refs:** AC-23

**Pre-condiciones**
- T-019 completado.

**Pasos**
1. En `ControlDeFinanzasIA.UI.csproj`, añadir:
   ```xml
   <ItemGroup>
     <AvaloniaResource Include="Assets\Fonts\**" />
   </ItemGroup>
   ```
2. CPM y demás ItemGroups intactos.
3. Ejecutar `dotnet build`.

**Criterios de verificación**
- `dotnet build` exit 0.
- Los assets de fuentes están embebidos (`avares://ControlDeFinanzasIA.UI/Assets/Fonts/...`).

**Notas**
- Única edición sancionada del `.csproj`; ningún paquete NuGet nuevo.

---

### [x] T-021 — Crear `UI/Styles/Tokens.axaml`

**AC refs:** AC-20, §4.2

**Pre-condiciones**
- T-019 completado.

**Pasos**
1. Crear `src/ControlDeFinanzasIA.UI/Styles/Tokens.axaml` como `ResourceDictionary`.
2. Definir `SolidColorBrush` resources: `InkBrush` `#16181D`, `SurfaceBrush` `#1F232B`, `HairlineBrush` `#31363F`, `PaperBrush` `#E8E4D9`, `PaperDimBrush` `#9BA0A8`, `AmberBrush` `#E8A33D`, `SageBrush` `#7FB069`, `TerracottaBrush` `#D96459`.
3. Definir `FontFamily` resources: `FontDisplay` (`avares://...#Fraunces`), `FontBody` (`#Instrument Sans`), `FontMono` (`#Spline Sans Mono`).
4. Definir spacing (`Space1`..`Space8`), motion (`MotionFast`/`MotionNormal`/`MotionSlow`) y radii (`RadiusSmall`, `RadiusMedium`).
5. Ejecutar `dotnet build`.

**Criterios de verificación**
- `dotnet build` exit 0.
- Recursos resuelven sin `ResourceNotFoundException`.

**Notas**
- Tokens inertes; el shell puede adoptarlos después, las vistas los consumen explícitamente.

---

### [x] T-022 — Crear `UI/Styles/LedgerStyles.axaml`

**AC refs:** AC-21, §4.6

**Pre-condiciones**
- T-021 completado.

**Pasos**
1. Crear `src/ControlDeFinanzasIA.UI/Styles/LedgerStyles.axaml` como `<Styles>`.
2. Estilos con classes explícitas: `.ledger-btn`, `.ledger-btn-primary`, `.ledger-btn-danger`, `.ledger-input`, `.ledger-row`, `.ledger-combo`, `.ledger-section`, `.ledger-amount`, `.ledger-amount.income`, `.ledger-amount.expense`, `.ledger-title`.
3. Referenciar tokens via `{StaticResource ...}`.
4. FluentTheme como base; overrides puntuales (p. ej. `ComboBoxDropDownBackground`).

**Criterios de verificación**
- `dotnet build` exit 0.
- Estilos no son implícitos globales; no afectan shell/dashboard.

**Notas**
- Classes explícitas evitan colisionar con FluentTheme.

---

### [x] T-023 — Crear `UI/Styles/Motion.axaml`

**AC refs:** AC-22, §4.4

**Pre-condiciones**
- T-021 completado.

**Pasos**
1. Crear `src/ControlDeFinanzasIA.UI/Styles/Motion.axaml` como `<Styles>`.
2. `RowReveal`: opacity 0→1 + TranslateY 8→0, 200ms `CubicEaseOut`; aplicado a `.ledger-row` vía `:nth-child(1..10)` con delays escalonados (~30ms); fallback uniforme si nth-child es impracticable.
3. `AmberTick`: scale 0→1 ~150ms easeOutBack en indicador de hover.
4. `RailSectionTransition`: `Transitions` Width/Opacity ~250ms.
5. Button press/underline animations.
6. Ejecutar `dotnet build`.

**Criterios de verificación**
- `dotnet build` exit 0.
- 100% XAML Avalonia; cero code-behind.

**Notas**
- Fallback sancionado: reveal uniforme del contenedor si stagger resulta impracticable.

---

### [x] T-024 — Aplicar tokens/estilos/motion a vistas y `App.axaml`

**AC refs:** AC-18, AC-20..AC-22, D-12, §4

**Pre-condiciones**
- T-009, T-016, T-021..T-023 completados.

**Pasos**
1. En `App.axaml`, cambiar `RequestedThemeVariant="Default"` a `"Dark"` y añadir `<ResourceInclude Source="avares://ControlDeFinanzasIA.UI/Styles/Tokens.axaml"/>` en `Application.Resources`.
2. En `IncomeView.axaml` y `ExpensesView.axaml`, añadir `UserControl.Styles` con `StyleInclude` de `LedgerStyles.axaml` y `Motion.axaml`.
3. Aplicar classes `ledger-*` a botones, inputs, filas, montos, títulos.
4. Montos de ingreso en `SageBrush`, montos de gasto en `TerracottaBrush`, right-aligned, `FontMono`.
5. Cabecera con título Fraunces y regla ámbar grid-breaking.
6. Ejecutar `dotnet build`.

**Criterios de verificación**
- `dotnet build` exit 0.
- Shell/dashboard no cambian de estilo (siguen FluentTheme Dark).
- Las dos vistas de transacciones muestran identidad "Libro Mayor".

**Notas**
- D-12: estilos a nivel `UserControl`, NO a nivel App.
- Q-3: design pass solo en las 2 vistas de transacciones; tema Dark único.

---

### [x] T-025 — Añadir ~28 keys de localización

**AC refs:** AC-24, D-10

**Pre-condiciones**
- T-009 y T-016 completados (se conoce el keyset exacto).

**Pasos**
1. Añadir keys a `src/ControlDeFinanzasIA.UI/Resources/Strings.resx` (neutral en inglés).
2. Añadir mismas keys a `src/ControlDeFinanzasIA.UI/Resources/Strings.es-DO.resx` (es-DO).
3. Regenerar `Strings.Designer.cs` (ResXFileCodeGenerator, commiteado) para exponer propiedades fuertemente tipadas.
4. Keys mínimas: `SidebarIncomes`, `IncomesTitle`, `EditButton`, `DeleteButton`, `ConfirmDeleteButton`, `UpdateButton`, `CancelEditButton`, `EditFormTitle`, `FiltersSectionTitle`, `FilterTagsLabel`, `FilterDateFromLabel`, `FilterDateToLabel`, `ApplyFilterButton`, `ClearFilterButton`, `SavedViewsSectionTitle`, `SavedViewsPlaceholder`, `NewViewNameWatermark`, `SaveViewButton`, `DeleteViewButton`, `CreateTagSectionTitle`, `TagNameWatermark`, `TagColorWatermark`, `CreateTagButton`, `NoTagsMessage`, `SelectCategoryWatermark`, `DescriptionWatermark`, `NoteWatermark`, `EmptyIncomesMessage`, `IncomeRegisteredMessage`, `IncomeUpdatedMessage`, `ExpenseUpdatedMessage`, `TransactionDeletedMessage`, `ViewSavedMessage`, `ViewDeletedMessage`, `ViewNameRequiredMessage`, `TagNameRequiredMessage`, `DuplicateTagMessage`, `OperationCancelledMessage`.
5. Ejecutar `dotnet build`.

**Criterios de verificación**
- `dotnet build` exit 0.
- `Strings.Designer.cs` contiene las nuevas propiedades.
- Cultura `es-DO` muestra textos en español; cultura neutral en inglés.

**Notas**
- D-10: convención de 3 archivos; NO existe `Strings.en.resx`.
- Reusar keys existentes (`DateLabel`, `AmountLabel`, `CurrencyLabel`, `CategoryLabel`, `PageOfPagesFormat`, etc.).

---

### [x] T-026 — Migrar strings hardcodeados restantes en secciones reescritas

**AC refs:** AC-24

**Pre-condiciones**
- T-025 completado.

**Pasos**
1. En `ExpensesView.axaml` e `IncomeView.axaml`, reemplazar literales españoles hardcodeados por `x:Static res:Strings.*`:
   - "Seleccione..." → `SelectCategoryWatermark`
   - "Descripción del gasto" / "Descripción" → `DescriptionWatermark`
   - "Nota opcional" → `NoteWatermark`
   - "Página X de Y" (Runs) → binding a `PageSummary`
2. Verificar que no quedan literales español visibles en las secciones reescritas.
3. Ejecutar `dotnet build`.

**Criterios de verificación**
- `dotnet build` exit 0.
- Grep de literales español hardcodeados en `IncomeView.axaml`/`ExpensesView.axaml` vacío (salvo comentarios).

**Notas**
- Scope discipline: solo se migran las secciones reescritas; el resto del debt i18n queda para otro feature.

---

### [x] T-027 — Gates finales de verificación

**AC refs:** AC-27, AC-28

**Pre-condiciones**
- T-001..T-026 completados.

**Pasos**
1. `dotnet build -c Release --nologo` → 0 warnings, 0 errors.
2. `dotnet build -c Debug --nologo` → 0 warnings, 0 errors (incluye compilación XAML).
3. `dotnet test -c Debug --no-build --nologo` → suite verde excepto 1 fallo pre-existing conocido de `Money`.
4. `dotnet test --filter "FullyQualifiedName~ArchitectureTests"` → 11/11 passed.
5. `dotnet format style --verify-no-changes` → exit 0.
6. `dotnet list src/ControlDeFinanzasIA.Domain package` → vacío (R-1).
7. Smoke manual (`dotnet run`):
   - Sidebar "Ingresos" → navega → diseño Libro Mayor visible.
   - Registrar/editar/eliminar ingreso.
   - En Gastos: filtro combinado → guardar vista → aplicar → eliminar.
   - Crear tag inline y ver picker actualizado.
   - Shell/dashboard sin estilos Libro Mayor.
8. Tomar screenshots de ambas vistas y adjuntar al verify-report.
9. Verificar scope: cero cambios en Domain/Infrastructure, cero paquetes nuevos, única edición de `.csproj` el ItemGroup de fuentes.

**Criterios de verificación**
- Todos los gates verdes.
- Screenshots evidencian tipografía distintiva y paleta cohesiva.

**Notas**
- No renombrar la spec a `.done.md`; eso es `sdd-archive`.
- Reportar conteos exactos de tests observados.

---

## Riesgos y mitigaciones

| # | Riesgo | Prob. | Impacto | Mitigación |
| --- | --- | --- | --- | --- |
| R-1 | `x:DataType` incorrecto/ausente → build XAML falla o bindings degradan | Media | Alto | Gate AC-27; patrón dual verificado en `App.axaml`; todo DataTemplate con `x:DataType`; `dotnet build` compila XAML. |
| R-2 | Fuentes embebidas: URI `avares://...#Family` o nombre de familia interno erróneo | Media | Medio | Docs Avalonia verificadas (solo ttf/otf); PR-4 prueba 1 fuente end-to-end antes de extender; fallback a 2 fuentes o URIs exactos. |
| R-3 | Overrides de FluentTheme vs Dark fijo afectan contraste o shell | Baja | Medio | FluentTheme como base; estilos con classes `ledger-*` no implícitos; `RequestedThemeVariant="Dark"` soportado; screenshots AC-28. |
| R-4 | Multi-select de tags y confirmación 2 pasos sin code-behind | Media | Medio | `SelectableTagItem.IsSelected` (D-8); estados pendientes como props VM (D-7); tests IV/EV blindan transiciones. |
| R-5 | Diff excede presupuesto de 400 líneas | **Alta** | Medio | 4 PRs stacked-to-main formalizados; inicio/fin claros y rollback propio por PR (§9 de la spec). |
| R-6 | `DeleteTransactionUseCase` con `TransactionType.Income` nunca ejercitado en UI | Baja | Bajo | IV-8/IV-9 cubren happy + NotFound; smoke AC-28 punto 2 lo ejecuta end-to-end. |
| R-7 | Estado pendiente no resetea entre acciones → UX confusa | Baja | Medio | D-7: toda acción de VM resetea `PendingDelete*Id`; tests de transición; smoke. |
| R-8 | Ctor de 14 deps: wiring equivocado en Bootstrap o helpers de tests | Baja | Medio | AC-19 enumera 5 registros exactos; helper IV construye deps explícitamente (error de compile si falta); smoke ejercita composition root. |
| R-9 | Engram `mem_save` no disponible en este toolset | Alta (verificado) | Bajo | Respaldo en filesystem (`tasks/phase-04/F-008-ui-capstone.tasks.md`); el orquestador backfillará a Engram. |

---

## Orden de ejecución recomendado

**Secuencial por PR (stacked-to-main):**

```text
PR-1 (T-001..T-006)
    T-001 ─┐
    T-002 ─┤
    T-003 ─┼─ T-006
    T-004 ─┤
    T-005 ─┘
    ↓
PR-2 (T-007..T-014)
    T-007 ─┐
    T-008 ─┼─ T-014
    T-009 ─┤
    T-010 ─┤
    T-011 ─┤
    T-012 ─┤
    T-013 ─┘
    ↓
PR-3 (T-015..T-018)
    T-015 ─┬─ T-017
    T-016 ─┘
    T-018
    ↓
PR-4 (T-019..T-027)
    T-019 ─┐
    T-020 ─┤
    T-021 ─┼─ T-024
    T-022 ─┤
    T-023 ─┘
    T-025 ─┬─ T-026
    T-027
```

1. **PR-1** debe mergearse primero; PR-2 consume sus tipos Application.
2. **PR-2** entrega `IncomeViewModel`/`IncomeView` + navegación; PR-3 consume `DeleteSavedViewUseCase`/`CreateTagUseCase`.
3. **PR-3** extiende `ExpensesView`; PR-4 es capa visual sobre ambas vistas.
4. **PR-4** cierra con design pass, localización y gates.

---

## Tareas bloqueantes / habilitadoras

| Tarea | Habilita | Razón |
| --- | --- | --- |
| T-001, T-002 | T-008, T-014 | `IncomeViewModel` necesita `IncomeDto` y `GetIncomesQuery`. |
| T-003, T-004 | T-008, T-013 | `IncomeViewModel` necesita `CreateTagUseCase` y `DeleteSavedViewUseCase`. |
| T-006 | T-014, T-017 | Tests de VM necesitan `FakeTagRepository` y fakes extendidos. |
| T-007 | T-008, T-009 | Picker de tags necesita `SelectableTagItem`. |
| T-008 | T-009, T-014 | `IncomeView` binda a `IncomeViewModel`. |
| T-013 | T-014, T-018 | El composition root debe resolver `IncomeViewModel`. |
| T-015 | T-016, T-017 | `ExpensesView.axaml` binda a las nuevas props/comandos. |
| T-021..T-023 | T-024 | Las vistas incluyen los diccionarios de estilos. |

---

## Definition of Done (DoD)

- [ ] AC-1: `IncomeDto` + `IncomeExtensions` creados en `Application/DTOs/`. (T-001)
- [ ] AC-2: `GetIncomesQuery` + `GetIncomesQueryRequest` creados con filtros día 1. (T-002)
- [ ] AC-3: `GetTransactionsQueryRequest`/`GetTransactionsQuery` extensión de filtros. (T-005)
- [ ] AC-4: `DeleteSavedViewCommand`/`UseCase` creados. (T-004)
- [ ] AC-5: `CreateTagCommand`/`Validator`/`UseCase` creados. (T-003)
- [ ] AC-6: `FakeTagRepository` + extensiones `FakeIncome`/`FakeExpense`. (T-006)
- [ ] AC-7: `IncomeViewModel` creado con 14 deps y comandos completos. (T-008)
- [ ] AC-8: `IncomeView.axaml` + `SelectableTagItem` creados. (T-007, T-009)
- [ ] AC-9: `ExpensesViewModel` extendido. (T-015)
- [ ] AC-10: `ExpensesView.axaml` extendido. (T-016)
- [ ] AC-11..AC-14: paginación, filtros, vistas guardadas, edit/delete en ambas vistas. (T-008, T-015, T-017)
- [ ] AC-15: creación de tags inline en ambos rieles. (T-008, T-015)
- [ ] AC-16: `RouteNames.Incomes` + `NavigationService` actualizados. (T-010)
- [ ] AC-17: `ShellViewModel.MenuItems` + key `SidebarIncomes`. (T-011, T-025)
- [ ] AC-18: DataTemplate dual en `App.axaml`. (T-012)
- [ ] AC-19: `Bootstrap.cs` registra componentes nuevos. (T-013)
- [ ] AC-20..AC-22: `Tokens.axaml`, `LedgerStyles.axaml`, `Motion.axaml` creados. (T-021..T-023)
- [ ] AC-23: fuentes OFL `.ttf` embebidas + ItemGroup `AvaloniaResource`. (T-019, T-020)
- [ ] AC-24: ~28 keys ×3 resx + migración de hardcodeados. (T-025, T-026)
- [ ] AC-25: `IncomeViewModelTests` + extensión `ExpensesViewModelTests`. (T-014, T-017)
- [ ] AC-26: tests unit de orquestadores nuevos + regresión `GetTransactionsQuery`. (T-006)
- [ ] AC-27: gates de build/tests/format/NetArchTest/scope limpios. (T-027)
- [ ] AC-28: smoke manual + screenshots de diseño BOLD. (T-018, T-027)

---

## Notas para apply

- **NO modificar Domain/Infrastructure:** entidades, repos, configuraciones, migraciones, `FilterJsonSerializer`, `SavedViewRepository`, `SeedData` congelados.
- **NO añadir paquetes NuGet:** CPM intacto; única edición de `.csproj` es el `ItemGroup` de fuentes.
- **Pure xUnit `Assert.*`:** sin FluentAssertions, sin mocking library.
- **MVVM estricto:** sin lógica en code-behind, sin `MessageBox`.
- **Localización:** keys en `Strings.resx` (neutral = inglés), `Strings.es-DO.resx` y `Strings.Designer.cs`.
- **Fuentes:** `Assets/Fonts/` con TTF variables OFL; `avares://ControlDeFinanzasIA.UI/Assets/Fonts#Fraunces` etc.
- **Rollback:** por PR en orden inverso (PR-4 → PR-3 → PR-2 → PR-1); todo es aditivo.
- **Engram:** `mem_save` no está disponible en el toolset del sub-agente; el orquestador backfillará `sdd/f-008p4-ui-capstone/tasks`.
