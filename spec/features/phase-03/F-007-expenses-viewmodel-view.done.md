# Specification — F-007: UI: `ExpensesViewModel` + `ExpensesView` + ViewModel tests

> **Fase:** 3 — Slice vertical: registrar y listar gastos
> **Feature:** F-007 (local a esta fase) · **Estado:** Pendiente · **Spec ID:** SPEC-F007P3
>
> **change_id:** `f-007p3-expenses-viewmodel-view` · **Prioridad:** Crítica · **Estimación:** L
> **Depende de:** F-004, F-005, F-006 (Phase 3) + Phase 1 `IClock` + Phase 2 `ICategoryRepository`
>
> **Spec convention:** filesystem (`spec/features/phase-03/`), no OpenSpec.
> **Artefacto Engram:** `sdd/f-007p3-expenses-viewmodel-view/spec`.
>
> **Nota de numeración:** este `F-007` es el de la **Fase 3**
> (`spec/phases/phase-03/features.md` §F-007). Es el **cierre vertical**
> de la fase: conecta el formulario con `RegisterExpenseUseCase` (F-004)
> y la lista con `GetTransactionsQuery` (F-005) sobre la navegación de
> F-006. Sin F-007, F-001..F-006 son código sin usuario.
>
> Fuentes de verdad: `docs/vision.md` (§4.1 registro, §4.3 listar),
> `docs/architecture.md` (§9 UI, §10 navegación, §15 cobertura),
> `spec/constitution/tech-stack.md` (MVVM estricto, sin lógica en
> code-behind, `IStringLocalizer` es-DO + en, design tokens
> centralizados), `spec/features/phase-03/F-006-ui-shell-navigation.done.md`
> (infra heredada: `NavigationService`, `ViewModelBase`, `RouteNames`,
> `App.axaml` DataTemplates, `.resx`),
> `.claude/skills/frontend-design/SKILL.md` (MVVM estricto, no estilos
> inline, no estética genérica de IA).

---

## 1. Resumen

F-007 cierra el slice vertical de Fase 3 con **valor visible**: una
vista `ExpensesView` (formulario + lista paginada + controles de
página + errores por campo) y un `ExpensesViewModel` que consume los
**use cases concretos** `RegisterExpenseUseCase` (F-004) y
`GetTransactionsQuery` (F-005), carga categorías desde
`ICategoryRepository` (Phase 2 F-004), y se navega desde el shell de
F-006. Introduce `Avalonia.Headless` 11.2.7 en CPM (sin `XUnit`) para
establecer el patrón de render headless para futuras features; el
smoke de F-007 es **manual** (`dotnet run`).

Al final de la feature:

- 2 tipos nuevos en `Application/DTOs/` (`CategoryDto`,
  `CategoryExtensions`), 1 en `UI/ViewModels/` (`ExpensesViewModel`),
  1 en `UI/Views/` (`ExpensesView.axaml` + `.cs` vacío), 1 fake nuevo
  (`FakeCategoryRepository`), 1 archivo de tests (`ExpensesViewModelTests`, ≥ 7).
- **Modifica** wiring: `NavigationService._routes` (añade
  `Expenses → ExpensesViewModel`), `Bootstrap` (registra
  `ExpensesViewModel` transient), `App.axaml` (DataTemplate), los
  `.resx` + `Strings.Designer.cs` (claves UI), `Directory.Packages.props`
  + `UI.csproj` (`Avalonia.Headless`).
- **NO modifica** `MainWindow.axaml` (ya muestra ambas rutas desde
  F-006), ni `Domain`, ni `Infrastructure`, ni los use cases de
  F-004/F-005 (ver DA #1).
- `dotnet build -c Release`: 0 warnings, 0 errors.
- `dotnet test`: **212 (baseline F-006) + ~7 ≈ 219 verdes**, 0
  failures (el conteo exacto puede superar 219 por expansión de
  `[Theory]`, precedente F-004/F-005).
- `dotnet run --project src/ControlDeFinanzasIA.UI` arranca, navega a
  "Gastos", registra y **muestra el gasto inmediatamente**, y al
  cerrar/reabrir sigue visible (persistencia SQLite).

## 2. Objetivo

1. **Cerrar el slice vertical** uniendo el formulario con el use case
   de registro y la lista con el query de lectura, sobre el shell de
   F-006.
2. **Fijar el patrón de VM de feature**: `sealed partial : ViewModelBase`
   con `[ObservableProperty]`/`[RelayCommand]`, inyectando los
   **sealed concretos** de Application (sin interfaces adicionales —
   DA #1).
3. **Validación server-side only** (DA #2 / DP-2): el VM no duplica
   reglas; siempre llama al use case, que valida con
   `RegisterExpenseValidator`. Los errores se mapean desde
   `Result.Error.Details` → `ValidationErrors[field]`.
4. **Paginación nativa**: `Page`/`TotalPages` con
   `PreviousPage`/`NextPage` y `CanExecute` reactivo, reusando
   `PagedResult<T>.TotalPages` (DP-8); page size fijo = 20 (DP-3).
5. **Localización** vía `IStringLocalizer<Strings>` (patrón F-006):
   nuevas claves (es-DO default + en fallback), sin strings
   hardcodeados.

## 3. Descripción breve

| Tipo | Archivo | Responsabilidad |
| --- | --- | --- |
| `CategoryDto` (sealed record) | `Application/DTOs/CategoryDto.cs` | **NUEVO.** `public sealed record CategoryDto(Guid Id, string Name)` — DTO mínimo para el `ComboBox` de categorías (ver §4 corrección). |
| `CategoryExtensions` (static) | `Application/DTOs/CategoryExtensions.cs` | **NUEVO.** `public static CategoryDto ToDto(this Category c) => new(c.Id, c.Name);` |
| `ExpensesViewModel` (sealed partial) | `UI/ViewModels/ExpensesViewModel.cs` | **NUEVO.** Hereda `ViewModelBase`. 6 dependencias, form fields `[ObservableProperty]`, `Expenses`/`Categories`/`Page`/`TotalPages`, `ValidationErrors`, comandos `RegisterExpense`/`Refresh`/`NextPage`/`PreviousPage`. |
| `ExpensesView.axaml` (+ `.cs`) | `UI/Views/ExpensesView.axaml(.cs)` | **NUEVO.** `UserControl` con `x:DataType="vm:ExpensesViewModel"`: formulario + `ListBox` (ItemsTemplate 4 cols) + paginación + `TextBlock`s de error por campo. Code-behind **vacío**. |
| `NavigationService` | `UI/Navigation/NavigationService.cs` | **MODIFICAR.** Añadir `{ RouteNames.Expenses, typeof(ExpensesViewModel) }` al `_routes` (hoy sólo `Dashboard`). |
| `Bootstrap.ConfigureServices` | `UI/Composition/Bootstrap.cs` | **MODIFICAR.** `services.AddTransient<ExpensesViewModel>()` (use cases ya registrados en F-004/F-005; `ICategoryRepository` vía `AddPersistence`). |
| `App.axaml` | `UI/App.axaml` | **MODIFICAR.** Añade `DataTemplate` `ExpensesViewModel → ExpensesView` con `x:DataType="vm:ExpensesViewModel"`. |
| `Strings.resx` + `Strings.es-DO.resx` + `Strings.Designer.cs` | `UI/Resources/` | **MODIFICAR.** Nuevas claves (AC-14). |
| `Directory.Packages.props` | raíz | **MODIFICAR.** Añade `Avalonia.Headless` 11.2.7 (sin `XUnit`). |
| `ControlDeFinanzasIA.UI.csproj` | `UI/` | **MODIFICAR.** Añade `<PackageReference Include="Avalonia.Headless" />` (sin `Version`; CPM). |
| `ExpensesViewModelTests` | `Tests.Unit/UI/` | **NUEVO.** ≥ 7 tests pure `Assert.*`. |
| `FakeCategoryRepository` | `Tests.Unit/TestDoubles/` | **NUEVO.** Implementa `ICategoryRepository`; `GetAllAsync` configurable. |
| `MainWindow.axaml` | `UI/Views/` | **SIN CAMBIOS.** Ya muestra Dashboard+Expenses vía `ShellViewModel.MenuItems` (F-006). |

## 4. Verificación contra el código (correcciones del brief)

Verificado antes de escribir, difiere del brief del orchestrator:

1. **`NavigationService._routes` sólo tiene `Dashboard`** (líneas 28-31
   del archivo). F-007 **debe** añadir la fila
   `{ RouteNames.Expenses, typeof(ExpensesViewModel) }`. Es un ADD a la
   Modify-list (el brief del orchestrator no lo listaba).
2. **`CategoryDto` no existe** → F-007 lo crea. **Trim de campos
   vs. brief:** el brief sugería
   `CategoryDto(Guid Id, string Name, string? Description, bool
   IsActive)`, pero `Category` (verificado en
   `Domain/Catalog/Category.cs`) **no tiene** `Description` ni
   `IsActive` — sus miembros son `Id, Name, Color?, Icon?, IsSystem,
   CreatedAt, UpdatedAt`. Sourcing `Description`/`IsActive` requeriría
   fabricar valores. Se usa la forma mínima e implementable
   `(Guid Id, string Name)` — exactamente lo que el `ComboBox` necesita
   (`SelectedValueBinding` + display `Name`), y coincide con la propia
   propuesta. *Acción recomendada:* el sdd-design o sdd-tasks puede
   añadir `Color`/`Icon` al DTO en Phase 4 si la UI los necesita.
3. **`MainWindow.axaml` no requiere cambios** de sidebar:
   `ShellViewModel.MenuItems` ya incluye ambos ítems
   (`SidebarDashboard`, `SidebarExpenses`) desde F-006. (El §F-007 del
   phase plan pide "añadir sidebar item" — ya hecho; reducir scope.)
4. **`Currency`** está en `Domain/Enums/Currency.cs` (`DOP=0`,
   `USD=1`); **`Category`** en `Domain/Catalog/Category.cs` (no en
   `Domain/Categories/`).
5. **`PagedResult<T>.TotalPages` ya existe** (ceiling, líneas 36-38 de
   `Common/PagedResult.cs`). F-007 **reusa** `paged.TotalPages` — no
   recalcula manualmente `(Total+19)/20` (DP-8).

> **Trazabilidad de tipos consumidos** (verificado):
> `RegisterExpenseUseCase.ExecuteAsync(RegisterExpenseCommand, ct) → Task<Result>`; success Value = `RegisterExpenseResult`; validation failure → `Result.Failure("Validation", msg, IReadOnlyDictionary<string,string[]>)`; otros errores → `("DomainInvariant"|"CategoryNotFound"|"RepositoryError", ...)`.
> `GetTransactionsQuery.ExecuteAsync(GetTransactionsQueryRequest, ct) → Task<Result>`; success Value = `PagedResult<ExpenseDto>`; filtros → `("InvalidFilter"|"RepositoryError", ...)`.
> `GetTransactionsQueryRequest(int Page = 1, int PageSize = 50)` → el VM pasa `new GetTransactionsQueryRequest(Page, 20)` (DP-3).
> `RegisterExpenseCommand(Guid CategoryId, DateOnly Date, decimal Amount, Currency Currency, string Description, string? Note)`.

## 5. Dependencias

| Dependencia | Estado | Uso en F-007 |
| --- | --- | --- |
| F-004 Phase 3 (archivada) | ✅ | `RegisterExpenseUseCase`, `RegisterExpenseValidator`, `RegisterExpenseCommand`, `RegisterExpenseResult`, `ExpenseDto`, `ExpenseExtensions.ToDto`. Consumidos como concretos (DA #1). |
| F-005 Phase 3 (archivada) | ✅ | `GetTransactionsQuery`, `GetTransactionsQueryRequest`, `PagedResult<ExpenseDto>`. `TotalPages` reusado (DP-8). |
| F-006 Phase 3 (archivada) | ✅ | `ViewModelBase`, `INavigationService`/`NavigationService`, `RouteNames.Expenses`, `ShellViewModel` (sidebar), `App.axaml` DataTemplate pattern, `IStringLocalizer<Strings>` + `.resx`. |
| Phase 2 F-004 `ICategoryRepository` | ✅ | `GetAllAsync() → Task<IReadOnlyList<Category>>` para el `ComboBox`. Registrado vía `AddPersistence` en `Bootstrap`. |
| Phase 1 F-004 `IClock` | ✅ | `Today` para defaultear `Date` (DP-7); `UtcNow` usado internamente por el use case. |
| `Avalonia.Headless` 11.2.7 | **NUEVO (CPM)** | Render headless capacidad; `Avalonia.Headless` **sin `XUnit`** (DA #3). VM tests son C# puro (no lo necesitan); smoke es manual. |
| `CommunityToolkit.Mvvm` 8.4.0 + `Microsoft.Extensions.Localization` 8.0.1 | ✅ CPM (F-006) | `[ObservableProperty]`, `[RelayCommand(CanExecute)]`, `ObservableObject`; `IStringLocalizer<Strings>`. |
| Avalonia 11.2.7 + `FluentTheme` | ✅ CPM | Design tokens; `AvaloniaUseCompiledBindingsByDefault=true` → `x:DataType` en la vista. |
| NetArchTest R-1..R-5 | ✅ | `ExpensesViewModel` (UI) consume `Application`; no referencia `Infrastructure` directamente (R-4). |

## 6. Fuera de alcance (NO en esta feature)

- **Edit/delete** de gastos — Phase 4+. Lista es **read-only display**
  (DP-4).
- **Filtros UI** (`CategoryId`/`DateFrom`/`DateTo`) — Phase 4+.
- **Multi-moneda UI** (conversión, totales por moneda) — Phase 4+. El
  `ComboBox` de `Currency` sólo selecciona DOP/USD; la cantidad se
  guarda tal cual.
- **Persistencia de última ruta** — Phase 4+. El arranque sigue en
  `RouteNames.Dashboard` (DP-3 de F-006).
- **Temas / dark mode** — Phase 4+. Se reusa `FluentTheme`; sin
  estilos inline.
- **`Avalonia.Controls.DataGrid`** — prohibido (DP-1: `ListBox` +
  `ItemsTemplate`).
- **Modificar** `RegisterExpenseUseCase`/`GetTransactionsQuery` o sus
  declarationes, `Expense.cs`, o cualquier tipo de `Domain`/
  `Infrastructure` (DA #1). F-007 sólo **consume**.
- **Wrapping de mensajes de error de use cases** con `IStringLocalizer`
  — los códigos (`Validation`, `DomainInvariant`, `CategoryNotFound`,
  `RepositoryError`) ya están en español en Application; el VM los
  enruta a `ValidationErrors`/`FormError` sin re-localizarlos.
- **FluentAssertions** o **librería de mocking** (Moq/NSubstitute).
- **`Avalonia.Headless.XUnit`** — prohibido en F-007 (DA #3). La smoke
  es manual `dotnet run`.

## 7. Decisiones de diseño (fijadas — no re-abrir)

| # | Decisión | Rationale |
| --- | --- | --- |
| DP-1 | Lista UI = `ListBox` + `ItemsTemplate` 4 columnas (`Date`, `Description`, `Amount`, `Currency`). **Sin `DataGrid`.** | Sin paquetes extra; `ListBox` + `Grid` en el template basta para read-only. |
| DP-2 | **Validación server-side only.** El VM **no** valida antes de llamar al use case; no duplica reglas de `RegisterExpenseValidator`. `Result.Failure("Validation", details)` → `ValidationErrors[field] = string.Join("; ", msgs)`. Otros `Failure` (`DomainInvariant`/`CategoryNotFound`/`RepositoryError`) → log + `FormError` (banner). | Una sola fuente de verdad de validación (Application). Cero drift client/server. |
| DP-3 | Page size **20** fijo. Cada llamada: `new GetTransactionsQueryRequest(Page, 20)`. | Límite legible; `GetTransactionsQuery` valida `PageSize ≤ 200` (F-005). |
| DP-4 | Lista **read-only**. Sin edit/delete/inline-edit. | Phase 4+. Evita scope creep. |
| DP-5 | `Categories` se cargan en el constructor vía `ICategoryRepository.GetAllAsync()` → `ToDto()`. | Reusa el repo de Phase 2; `CategoryDto` mínimo `Id`+`Name` para el `ComboBox`. |
| DP-6 (heredado F-006) | `ExpensesViewModel` recibe `INavigationService` (si lo necesita) **nunca** `ShellViewModel`. `NavigationService` recibe `IServiceProvider`, no VMs hermanos. | Anti-ciclo DI. |
| DP-7 | Default `Date = _clock.Today` (no `DateTime.Today`). | Seam de tiempo respetado (Phase 1); testeable con `FakeClock`. |
| DP-8 | `TotalPages` se lee de `PagedResult<ExpenseDto>.TotalPages` (ya calculado por ceiling). **No** recalcular `(Total+19)/20`. | Reusar el contrato F-005; una sola fuente. |
| DA #1 | Fakes = **reutilizar concretos.** El VM inyecta los **sealed** `RegisterExpenseUseCase`/`GetTransactionsQuery`. Los tests los instancian con `FakeExpenseRepository` (con `GetByFilterAsync`), `FakeClock`, **nuevo** `FakeCategoryRepository`, real `RegisterExpenseValidator`, `NullLogger<>`, `FakeStringLocalizer<Strings>`. **Sin** interfaces nuevas para los use cases. | Precedente `RegisterExpenseUseCaseTests` (construye el sealed con fakes). Respeta el out-of-scope de no tocar F-004/F-005. |
| DA #2 | Botón "Registrar" **siempre habilitado.** Sin `CanRegister` client-side. La validación ocurre server-side y los `TextBlock`s de error aparecen tras el `Result.Failure`. | Cero duplicación client-side (DP-2). El phase plan §F-007 dice "botón desactiva" — **superseded** por DA #2 (siempre habilitado). |
| DA #3 | `Avalonia.Headless` (sin `.XUnit`) introducido en F-007. VM tests = **C# puro** (no rendering). Smoke = **manual** `dotnet run`. | Establece el patrón headless para Phase 4+; sin fricción de build de integración xUnit en este cierre. |

## 8. Cambios al alcance

**Se añade** (sólo `Application/DTOs/`, `UI/`, `Tests.Unit/`):
`CategoryDto.cs` + `CategoryExtensions.cs` en `Application/DTOs/`;
`ExpensesViewModel.cs` en `UI/ViewModels/`; `ExpensesView.axaml(.cs)` en
`UI/Views/`; `ExpensesViewModelTests.cs` en `Tests.Unit/UI/`;
`FakeCategoryRepository.cs` en `Tests.Unit/TestDoubles/`.

**Se modifica** (wiring):
`NavigationService.cs` (1 fila en `_routes`), `Bootstrap.cs` (1
registro transient), `App.axaml` (1 DataTemplate), `Strings.resx` +
`Strings.es-DO.resx` + `Strings.Designer.cs` (claves AC-14),
`Directory.Packages.props` (1 `PackageVersion`), `UI.csproj` (1
`PackageReference`).

**NO se modifica:** `MainWindow.axaml` (corrección #3); `Domain`,
`Infrastructure`; tipografías de F-004/F-005 (`RegisterExpenseUseCase`,
`GetTransactionsQuery`, `Expense.cs`, `Result`, `PagedResult<T>`,
`ExpenseDto`, `ExpenseExtensions`); NetArchTest R-1..R-5.
`Tests.Unit.csproj` **sólo** si se añade un test headless automatizado
(DA #3 = no) — en F-007 no recibe `Avalonia.Headless`.

## 9. Acceptance Criteria

La feature se considera completada cuando **todos** los ACs se cumplen,
verificables mediante tests automatizados y revisión de código.

### AC-1 — `ExpensesViewModel` base y dependencias
- [ ] `public sealed partial class ExpensesViewModel : ViewModelBase`
      en `UI/ViewModels/ExpensesViewModel.cs`. Constructor inyecta 6
      dependencias: `RegisterExpenseUseCase`,
      `GetTransactionsQuery`, `ICategoryRepository`, `IClock`,
      `ILogger<ExpensesViewModel>`, `IStringLocalizer<Strings>`.
- [ ] `ArgumentNullException.ThrowIfNull` por cada una. File-scoped
      namespace, XML doc.

### AC-2 — Estado `[ObservableProperty]`
- [ ] Form: `Date` (`DateOnly?`, default `_clock.Today`), `Amount`
      (`decimal?`), `Currency` (`Currency`, default `Currency.DOP`),
      `SelectedCategoryId` (`Guid?`), `Description` (`string?`),
      `Note` (`string?`).
- [ ] Lista/paginación: `Expenses` (`ObservableCollection<ExpenseDto>`),
      `Categories` (`IReadOnlyList<CategoryDto>`), `Page` (`int`,
      default 1), `TotalPages` (`int`, default 0).
- [ ] Errores: `ValidationErrors` (`Dictionary<string, string>`) y
      `FormError` (`string?`, banner genérico).

### AC-3 — `RegisterExpenseAsync`
- [ ] `[RelayCommand] private async Task RegisterExpenseAsync()` **sin
      `CanExecute`** (DA #2 — siempre habilitado).
- [ ] Construye `RegisterExpenseCommand(SelectedCategoryId ?? Guid.Empty,
      Date ?? _clock.Today, Amount ?? 0m, Currency, Description ?? string.Empty, Note)`.
- [ ] Llama `_registerUseCase.ExecuteAsync(command, ct)`.
- [ ] `IsSuccess`: limpia form (mantiene `Date`/`Currency` defaults),
      limpia `ValidationErrors` + `FormError`, llama `RefreshAsync()`.
- [ ] `Error.Code == "Validation"`: `ValidationErrors.Clear()`;
      `ValidationErrors[e.PropertyName] = string.Join("; ", details[...])`
      por cada campo del `Error.Details`.
- [ ] Otros `Failure` (`DomainInvariant`/`CategoryNotFound`/`RepositoryError`):
      `_logger.LogError`/`LogWarning` + `FormError = localizer["FormErrorMessage"]`
      (banner); `ValidationErrors` se limpia.

### AC-4 — `RefreshAsync`
- [ ] `[RelayCommand] private async Task RefreshAsync()` llama
      `_getTransactionsQuery.ExecuteAsync(new GetTransactionsQueryRequest(Page, 20), ct)`.
- [ ] `IsSuccess`: cast `(PagedResult<ExpenseDto>)result.Value`;
      `Expenses.Clear()`; `Add` por item; `TotalPages = paged.TotalPages` (DP-8).
- [ ] `Failure` (`InvalidFilter`/`RepositoryError`): `_logger.LogError`;
      `Expenses.Clear()`; `TotalPages = 0`.

### AC-5 — `PreviousPageCommand`
- [ ] `[RelayCommand(CanExecute = nameof(CanGoToPreviousPage))]`.
- [ ] `public bool CanGoToPreviousPage => Page > 1;`
- [ ] Decrementa `Page` (sólo si `> 1`) y llama `RefreshAsync()`.
- [ ] `OnPageChanged` partial → `PreviousPageCommand.NotifyCanExecuteChanged()`.

### AC-6 — `NextPageCommand`
- [ ] `[RelayCommand(CanExecute = nameof(CanGoToNextPage))]`.
- [ ] `public bool CanGoToNextPage => Page < TotalPages;`
- [ ] Incrementa `Page` (sólo si `< TotalPages`) y llama `RefreshAsync()`.
- [ ] `OnPageChanged`/`OnTotalPagesChanged` →
      `NextPageCommand.NotifyCanExecuteChanged()`.

### AC-7 — Constructor
- [ ] Llama `LoadCategoriesAsync()` (`_categoryRepository.GetAllAsync()`
      → `Categories = items.Select(c => c.ToDto()).ToList()`) y luego
      `RefreshAsync()` para la primera página.

### AC-8 — Default `Date` del clock (DP-7)
- [ ] `Date = _clock.Today` al construir (no `DateTime.Today`).

### AC-9 — `CategoryDto` + `CategoryExtensions`
- [ ] `public sealed record CategoryDto(Guid Id, string Name)` en
      `Application/DTOs/CategoryDto.cs`.
- [ ] `public static CategoryDto ToDto(this Category c) => new(c.Id, c.Name);`
      en `Application/DTOs/CategoryExtensions.cs`. (Trim vs. brief —
      ver §4 #2.)

### AC-10 — `ExpensesView.axaml` layout
- [ ] `UserControl` con `x:DataType="vm:ExpensesViewModel"`.
- [ ] Form (StackPanel vertical): `DatePicker` Date, `TextBox` Amount
      (`StringFormat` es-DO), `ComboBox` Currency (DOP/USD), `ComboBox`
      categorías (bound a `Categories`, display `Name`, `SelectedValue`
      → `SelectedCategoryId`), `TextBox` Description, `TextBox`
      multilínea Note, `Button` "Registrar" (`Command={RegisterExpenseCommand}`).
- [ ] `TextBlock` rojo por campo, bindeado a
      `ValidationErrors[" fieldName"]`, colapsado cuando no hay error;
      `TextBlock` para `FormError`.
- [ ] Lista: `ListBox ItemsSource={Binding Expenses}` con
      `ItemsTemplate` = `Grid` 4 cols (`Date`, `Description`, `Amount`,
      `Currency`).
- [ ] Paginación: `StackPanel` con "Anterior" (`PreviousPageCommand`) +
      `TextBlock` "Página X de Y" (`Page`/`TotalPages`) + "Siguiente"
      (`NextPageCommand`).
- [ ] `ExpensesView.axaml.cs` code-behind **vacío** (sólo
      `InitializeComponent`). Sin estilos inline; `FluentTheme`.

### AC-11 — CompiledBindings
- [ ] `x:DataType="vm:ExpensesViewModel"` en el `UserControl`
      (`AvaloniaUseCompiledBindingsByDefault=true`).

### AC-12 — `App.axaml` DataTemplate
- [ ] `App.axaml` añade `<DataTemplate DataType="{x:Type vm:ExpensesViewModel}" x:DataType="vm:ExpensesViewModel"><views:ExpensesView /></DataTemplate>`.

### AC-13 — Wiring (`Bootstrap` + `NavigationService`)
- [ ] `Bootstrap.ConfigureServices`: `services.AddTransient<ExpensesViewModel>()`.
- [ ] `NavigationService._routes` añade
      `{ RouteNames.Expenses, typeof(ExpensesViewModel) }` (corrección #1).

### AC-14 — Strings UI (DP-5 / DP localization)
- [ ] `Strings.resx` (en, neutral) + `Strings.es-DO.resx` (es-DO) +
      `Strings.Designer.cs` con claves nuevas: `ExpensesTitle`,
      `RegisterButton`, `DateLabel`, `AmountLabel`, `CurrencyLabel`,
      `CategoryLabel`, `DescriptionLabel`, `NoteLabel`,
      `PreviousPageButton`, `NextPageButton`, `PageOfPagesFormat`
      (`Página {0} de {1}`), `EmptyExpensesMessage`,
      `RegistrationSuccessMessage`, `FormErrorMessage`, `CurrencyDOP`,
      `CurrencyUSD`.
- [ ] El VM usa `IStringLocalizer<Strings>` para los textos (sin
      literales hardcodeados).

### AC-15 — `Avalonia.Headless` (DA #3)
- [ ] `Directory.Packages.props` añade
      `<PackageVersion Include="Avalonia.Headless" Version="11.2.7" />`
      con comentario justificante (alineado a los pins Avalonia 11.2.7).
- [ ] `UI.csproj` añade `<PackageReference Include="Avalonia.Headless" />`
      **sin** `Version` (CPM). **Sin** `Avalonia.Headless.XUnit`.
- [ ] `Tests.Unit.csproj` **no** recibe `Avalonia.Headless` (VM tests
      C# puro; smoke manual).

### AC-16 — Tests (≥ 7, pure xUnit)
- [ ] `tests/ControlDeFinanzasIA.Tests.Unit/UI/ExpensesViewModelTests.cs`
      con ≥ 7 tests (ver §10). Pure `Assert.*`. **No** FluentAssertions,
      **no** librería de mocking, **no** `Avalonia.Headless.XUnit`.

### AC-17 — Fakes (DA #1)
- [ ] `tests/.../TestDoubles/FakeCategoryRepository.cs` **nuevo**
      (implementa `ICategoryRepository`; `GetAllAsync` configurable).
- [ ] Reusa `FakeExpenseRepository` (con `GetByFilterAsync`),
      `FakeClock`, `FakeStringLocalizer<Strings>`, real
      `RegisterExpenseValidator`, `NullLogger<ExpensesViewModel>`
      (`Microsoft.Extensions.Logging.Abstractions`). Instancia los
      **sealed** use cases reales (no fakes de use case).

### AC-18 — Sin scope creep
- [ ] NO edit/delete, NO filtros UI, NO multi-moneda UI, NO
      modificación de `RegisterExpenseUseCase`/`GetTransactionsQuery`/
      `Expense.cs`/`Result`/`PagedResult<T>`/`ExpenseDto`/`Domain`/
      `Infrastructure`, NO `Avalonia.Headless.XUnit`, NO
      `Avalonia.Controls.DataGrid`.

### AC-19 — Arquitectura + botón siempre habilitado
- [ ] NetArchTest R-1..R-5 verdes. `ExpensesViewModel` (UI) consume
      `Application` (use cases + `ICategoryRepository` + `IClock`);
      no referencia `Infrastructure` directamente (R-4 via
      composition-root exception).
- [ ] `RegisterExpenseCommand` / el botón "Registrar" **sin**
      `CanExecute` (DA #2); la validación es server-side.

### AC-20 — Build, tests, format, smoke
- [ ] `dotnet build -c Release`: 0 warnings, 0 errors.
- [ ] `dotnet test -c Release --no-build`: **212 + ~7 ≈ 219 verdes**,
      0 failures.
- [ ] `dotnet format style --verify-no-changes`: exit 0.
- [ ] `dotnet run --project src/ControlDeFinanzasIA.UI`: arranca,
      navega a "Gastos" (`NavigationService.NavigateTo(RouteNames.Expenses)`),
      muestra form + lista + paginación; al registrar con monto
      inválido, aparecen los `TextBlock`s rojos (server-side); al
      registrar válido, el gasto aparece **inmediatamente**; cerrar y
      reabrir → sigue visible (SQLite persistente). Smoke manual
      documentado.

## 10. Test cases

### 10.1 `ExpensesViewModelTests.cs` (≥ 7, pure `Assert.*`)

| # | Método | Escenario |
| --- | --- | --- |
| E1 | `Constructor_LoadsCategories_SetsDefaultDateFromClock` | `FakeCategoryRepository.GetAllAsync` devuelve 2 `Category`. `FakeClock.Today = 2026-08-16`. Tras construir el VM: `Categories.Count == 2` (vía `ToDto`), `Date == _clock.Today`, `Page == 1`. |
| E2 | `RegisterExpense_ValidForm_CallsUseCase_AndRefreshesList` | Form válido (categoria, monto > 0, descripción). `FakeExpenseRepository.AddAsync` success. Invocar `RegisterExpenseCommand` → `AddedExpenses` tiene 1 elem; `Expenses` se repuebla (vía `GetByFilterAsync`); `ValidationErrors` vacío; form limpia `Amount`/`Description`/`Note`. |
| E3 | `RegisterExpense_ServerSideValidationFails_ShowsFieldErrors` | Form con `Amount = 0` (invalida F-004). Invocar comando → el sealed `RegisterExpenseUseCase` retorna `Result.Failure("Validation", details)` con clave `"Amount"`; `ValidationErrors["Amount"]` contiene el mensaje ("El monto debe ser mayor que 0."); `Expenses` sin cambios; `AddedExpenses` vacío. |
| E4 | `RegisterExpense_UseCaseFailsWithNonValidation_SetsFormError` | `FakeExpenseRepository.SetAddResult(Result.Failure("CategoryNotFound", "..."))`. Invocar comando → `FormError` no nulo (banner), `ValidationErrors` vacío, `_logger` registra. |
| E5 | `Refresh_PopulatesExpensesAndPageMetadata` | `FakeExpenseRepository.SetFilterResult(items, total: 45, page: 1, pageSize: 20)`. Invocar `RefreshCommand` → `Expenses.Count == 20`; `TotalPages == 3` (45/20 ceiling, leído de `PagedResult.TotalPages`). |
| E6 | `NextPage_IncrementsPage_AndRefreshes` | `Page=1`, `TotalPages=3`. Invocar `NextPageCommand` → `Page==2`; `GetByFilterCallCount` incrementa. |
| E7 | `PreviousPage_AtFirstPage_DoesNotDecrement` | `Page=1`. `PreviousPageCommand.CanExecute(null) == false`; invocar (si permitido) no cambia `Page`. |
| E8 (rec.) | `NextPage_AtLastPage_DoesNotIncrement` | `Page=3`, `TotalPages=3`. `NextPageCommand.CanExecute(null) == false`. |
| E9 (rec.) | `PreviousPage_OnSecondPage_DecrementsAndRefreshes` | `Page=2`. `PreviousPageCommand.CanExecute(null) == true`; invocar → `Page==1`. |
| E10 (rec.) | `Refresh_OnRepositoryError_ClearsExpensesAndLogs` | `FakeExpenseRepository.SetThrowOnGetFilter(new Exception())`. `RefreshCommand` → `Expenses` vacío, `TotalPages==0`, no lanza. |

### 10.2 `FakeCategoryRepository` (TestDoubles) — hand-rolled

| Miembro | Propósito |
| --- | --- |
| `IReadOnlyList<Category> Categories { get; set; }` | Configurable; default `Array.Empty<Category>()`. |
| `Task<IReadOnlyList<Category>> GetAllAsync(...)` | Devuelve `Categories`. |
| `AddAsync`/`UpdateAsync`/`RemoveAsync`/`GetByIdAsync` | No-ops / `Result.Success` / `null` (no se usan en F-007). |

## 11. Estructura de archivos esperada

```
src/
├── ControlDeFinanzasIA.Application/
│   └── DTOs/
│       ├── ExpenseDto.cs                        (sin cambios — F-004)
│       ├── ExpenseExtensions.cs                 (sin cambios — F-004)
│       ├── CategoryDto.cs                       ← NUEVO (F-007)
│       └── CategoryExtensions.cs                ← NUEVO (F-007)
└── ControlDeFinanzasIA.UI/
    ├── Navigation/
    │   ├── RouteNames.cs                        (sin cambios — F-006)
    │   ├── INavigationService.cs                (sin cambios — F-006)
    │   └── NavigationService.cs                 ← MODIFICAR (_routes += Expenses)
    ├── ViewModels/
    │   ├── ViewModelBase.cs                     (sin cambios — F-006)
    │   ├── ShellViewModel.cs                    (sin cambios — F-006)
    │   ├── DashboardViewModel.cs               (sin cambios — F-006)
    │   └── ExpensesViewModel.cs                 ← NUEVO (F-007)
    ├── Views/
    │   ├── MainWindow.axaml(.cs)                (SIN CAMBIOS — verificado §4 #3)
    │   ├── DashboardView.axaml(.cs)             (sin cambios — F-006)
    │   ├── ExpensesView.axaml                   ← NUEVO (F-007)
    │   └── ExpensesView.axaml.cs                ← NUEVO (code-behind vacío)
    ├── Resources/
    │   ├── Strings.resx                         ← MODIFICAR (claves nuevas, en)
    │   ├── Strings.es-DO.resx                   ← MODIFICAR (claves nuevas, es-DO)
    │   └── Strings.Designer.cs                  ← MODIFICAR (accessors nuevos)
    ├── Composition/
    │   └── Bootstrap.cs                         ← MODIFICAR (AddTransient<ExpensesViewModel>)
    ├── App.axaml                                ← MODIFICAR (DataTemplate ExpensesViewModel→ExpensesView)
    └── ControlDeFinanzasIA.UI.csproj            ← MODIFICAR (PackageReference Avalonia.Headless)

Directory.Packages.props                         ← MODIFICAR (Avalonia.Headless 11.2.7)

(Domain, Infrastructure: SIN cambios; F-004/F-005 use cases: SIN cambios)

tests/
└── ControlDeFinanzasIA.Tests.Unit/
    ├── UI/
    │   ├── NavigationServiceTests.cs            (sin cambios — F-006)
    │   ├── ShellViewModelTests.cs               (sin cambios — F-006)
    │   └── ExpensesViewModelTests.cs            ← NUEVO (F-007, ≥ 7)
    └── TestDoubles/
        ├── FakeClock.cs                         (reusado)
        ├── FakeExpenseRepository.cs             (reusado — DA #1)
        ├── FakeNavigationService.cs              (sin cambios)
        ├── FakeStringLocalizer.cs               (reusado)
        └── FakeCategoryRepository.cs            ← NUEVO (F-007)
```

## 12. Convenciones

- **MVVM estricto** (per `frontend-design` + `tech-stack.md`): sin
  lógica en code-behind. `ExpensesView.axaml.cs` **vacío** salvo
  `InitializeComponent`. Toda acción vía comandos en `ExpensesViewModel`.
  VM testeable sin UI.
- **Design tokens centralizados**: `FluentTheme` (ya en `App.axaml`).
  **No** estilos inline. F-007 **no** innova estética a nivel de tokens
  — reusa `FluentTheme`; el detalle refinado llega en features de
  producto de Phase 4+ respetando el skill `frontend-design`.
- **`CommunityToolkit.Mvvm` source generators**: `ExpensesViewModel` es
  `sealed partial class` (requisito del generador, verificado vía
  Context7 y F-006). Los partial `OnDateChanged`/`OnPageChanged`/
  `OnTotalPagesChanged` llaman `NotifyCanExecuteChanged` en los
  page-commands.
- **`IStringLocalizer<Strings>`** para strings de UI. `.resx` es-DO
  default + en fallback. Códigos de error (`Validation`, etc.) **no**
  se re-localizan en el VM.
- **Código:** file-scoped namespace, `using` al tope, XML doc en cada
  clase y miembro público, `sealed` en tipos concretos, `record`
  inmutable para DTOs.
- **Tests:** pure xUnit `[Fact]`/`[Theory]` + `Assert.*`
  (`True`/`False`/`Equal`/`NotNull`/`Throws`/`ThrowsAsync`/`Single`/
  `Contains`). Naming `Method_Scenario_ExpectedResult`. **No**
  FluentAssertions. **No** librería de mocking. **No**
  `Avalonia.Headless.XUnit` (DA #3). Fakes hand-rolled.
- **CPM:** versiones sólo en `Directory.Packages.props`; `.csproj` sin
  `Version`.
- **Async/await** end-to-end: `ExecuteAsync`/`RefreshAsync`/command
  handlers con `CancellationToken`. La mutación de `Expenses`
  (`ObservableCollection`) tras `await` en el UI thread — Avalonia
  captura el `SynchronizationContext`; en tests xUnit (sin sync context)
  es seguro (precedente F-004/F-005).
- **Errores como valores:** el VM ramifica sobre `Result.IsSuccess`/
  `Error.Code` — nunca try/catch alrededor del use case (éste ya
  encapsula sus excepciones en `Result.Failure`).

## 13. Riesgos

| Riesgo | Prob. | Mitigación |
| --- | --- | --- |
| `DataTemplate` no resuelve con CompiledBindings → vista vacía ("Gastos" muestra la shell sin contenido). | Media | `x:DataType="vm:ExpensesViewModel"` correcto en `App.axaml` y en el `UserControl`; smoke manual (AC-20) documenta el síntoma; el compilador emite warnings de bindings si `x:DataType`/namespace falla. |
| `ObservableCollection` mutada tras `await` (hilo UI). | Media | Avalonia captura el `SynchronizationContext` del UI thread por defecto; smoke manual valida runtime. Tests xUnit corren sin sync context (ok en unit). En el VM, mutar `Expenses` tras el `await` del command es el patrón estándar de Avalonia. |
| `Error.Details` (`string[]` por campo) → `string.Join("; ", msgs)` colapsa mensajes múltiples. | Baja | Documentar el flatten en AC-3; `RegisterExpenseValidator` emite 1 mensaje por regla (caso típico = 1). |
| `Avalonia.Headless` 11.2.7 no alinea con `Avalonia`/`Fluent` 11.2.7 → build roto. | Baja | Pin 11.2.7 exacto (igual a los Avalonia 11.2.7 ya en CPM); verificar `dotnet build` limpio; sin `XUnit` (DA #3) reduce la superficie de integración. |
| Scope creep: añadir edit/delete/filtros "porque ya está la lista". | Baja | AC-18 lo prohíbe explícito; DP-4. |
| `ICategoryRepository` no resuelto en DI (no registrado) → `Activate` lanza al navegar a "Gastos". | Baja | `Bootstrap` ya lo registra vía `AddPersistence` (comentario línea 42-44 lo confirma); verificar en apply. |
| `Strings.Designer.cs` no se regenera al añadir claves → localizer retorna el key. | Media | AC-14 exige extender el designer con los accessors (o regenera `ResXFileCodeGenerator`); `FakeStringLocalizer` fallback = nombre del key (tests robustos). |
| Reactive cross-field: `Page`/`TotalPages` cambian pero los `CanExecute` no se re-evalúan. | Media | Partial `OnPageChanged`/`OnTotalPagesChanged` → `NotifyCanExecuteChanged()` (AC-5/AC-6); tests E7-E9 cubren los bordes. |

## 14. Criterios para considerarla completada

- [ ] `dotnet build -c Release` desde la raíz: 0 warnings, 0 errors.
- [ ] `dotnet test -c Release --no-build`: **212 (baseline F-006) +
      ~7 ≈ 219 verdes**, 0 failures (puede superar 219 por `[Theory]`,
      precedente F-004/F-005).
- [ ] NetArchTest R-1..R-5 verdes (F-007 sólo toca `UI` salvo
      `Bootstrap` + los DTOs en `Application/DTOs/`; no toca `Domain`/
      `Infrastructure`).
- [ ] `dotnet format style --verify-no-changes`: exit 0.
- [ ] Smoke manual: `dotnet run --project src/ControlDeFinanzasIA.UI`
      arranca, navega a "Gastos", muestra form + lista + paginación;
      registrar inválido → `TextBlock`s rojos; registrar válido →
      aparece **inmediatamente**; cerrar/reabrir → sigue (SQLite).
- [ ] F-007 marcada ✅ COMPLETADA en `spec/phases/phase-03/features.md`
      tras apply + verify; la spec se renombra a
      `F-007-expenses-viewmodel-view.done.md` tras sdd-archive
      (cierra Phase 3).

## 15. Referencias

- `spec/phases/phase-03/features.md` — §F-007 (cierre vertical),
  dependencias F-004/F-005/F-006, prioridad Crítica.
- `spec/features/phase-03/F-006-ui-shell-navigation.done.md` — infra
  heredada (`NavigationService`, `ViewModelBase`, `RouteNames`,
  `App.axaml` DataTemplate pattern, `.resx`, `IStringLocalizer`).
- `spec/features/phase-03/F-004-register-expense-use-case.done.md` —
  `RegisterExpenseUseCase` (concreto, sealed), `RegisterExpenseValidator`,
  `RegisterExpenseResult`, `Error.Details`.
- `spec/features/phase-03/F-005-get-transactions-query.done.md` —
  `GetTransactionsQuery`, `GetTransactionsQueryRequest`,
  `PagedResult<ExpenseDto>.TotalPages`.
- `tests/.../Application/Expenses/RegisterExpenseUseCaseTests.cs` —
  precedente DA #1 (sealed use case + hand-rolled fakes, pure `Assert.*`).
- `spec/constitution/tech-stack.md` — MVVM estricto, sin lógica en
  code-behind, `IStringLocalizer` es-DO + en, design tokens
  centralizados, CPM.
- `.claude/skills/frontend-design/SKILL.md` — MVVM estricto, no estilos
  inline, no estética genérica de IA.
- `docs/architecture.md` §9 (UI), §10 (navegación), §15 (cobertura
  VM ≥ 70%).
- `docs/vision.md` §4.1 (registrar gasto), §4.3 (listado básico).
- [Context7 — CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/)
  (`[ObservableProperty]`, `[RelayCommand(CanExecute)]`, `OnPropertyChanged`
  partial methods, requisito `partial class`).
- [Context7 — Avalonia 11: DataTemplates, CompiledBindings,
  `x:DataType`](https://docs.avaloniaui.net/) (confirmar patrón
  `DataTemplate`+`x:DataType` al aplicar).
- [Context7 — Avalonia.Headless 11.2.7](https://docs.avaloniaui.net/)
  (capacidad headless; alineado a 11.2.7).