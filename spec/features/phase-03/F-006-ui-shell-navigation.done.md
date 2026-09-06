# Specification — F-006: UI: shell, `ShellViewModel`, navegación por contenido, DataTemplates

> **Fase:** 3 — Slice vertical: registrar y listar gastos
> **Feature:** F-006 (local a esta fase) · **Estado:** Pendiente · **Spec ID:** SPEC-F006P3
>
> **Nota de numeración:** este `F-006` es el de la **Fase 3**
> (`spec/phases/phase-03/features.md`). No debe confundirse con el
> `F-006` de Fase 1 (15 interfaces de repositorio). Los IDs son
> locales a cada fase; el folder los desambigua.
>
> Fuentes de verdad: `docs/vision.md` (§4.1), `docs/architecture.md`
> (§9 capa de UI, §10 navegación), `spec/constitution/tech-stack.md`
> (MVVM estricto, sin lógica en code-behind, design tokens
> centralizados, `IStringLocalizer` es-DO + en), `spec/phases/phase-03/features.md`
> §F-006, `spec/features/phase-01/F-008-composition-root.done.md`
> (`Bootstrap.ConfigureServices`, `App.axaml.cs` con ventana inline
> `CreateMainWindow()`), `.claude/skills/frontend-design/SKILL.md`
> (MVVM estricto, no estilos inline, no estética genérica de IA).

---

## 1. Resumen

F-006 introduce el **esqueleto de UI** de la app: el contrato de
navegación por contenido (`RouteNames` + `INavigationService` +
`NavigationService` con pila de historial in-memory), la base
`ViewModelBase` (extiende `ObservableObject`), el `ShellViewModel`
con `[ObservableProperty]`/`[RelayCommand]`, el layout del
`MainWindow` (`DockPanel`: sidebar + content area + statusbar
placeholder), los `DataTemplate`s en `App.axaml` que asocian
`ViewModel → View`, un `DashboardViewModel`/`DashboardView`
placeholder, y la configuración inicial de `IStringLocalizer` con
recursos `.resx` (es-DO default + en fallback) — el patrón que
F-007 hereda y enchufa con la feature de gastos.

`MainWindow.axaml` es **nuevo**: reemplaza el `new Window()` inline
de `App.axaml.cs` (`CreateMainWindow()` de F-008). F-006 añade
**dos paquetes nuevos** a CPM: `CommunityToolkit.Mvvm` (source
generators `[ObservableProperty]`/`[RelayCommand]`) y
`Microsoft.Extensions.Localization` (`IStringLocalizer`,
`AddLocalization()`). No toca `Domain`, `Application` ni
`Infrastructure` (salvo el registro DI en `Bootstrap`).

Al final de la feature:

- 7 tipos nuevos en `UI/Navigation/` + `UI/ViewModels/` + `UI/Views/`.
- 2 archivos `.resx` + 1 `Strings.Designer.cs` (auto-gen) en
  `UI/Resources/`.
- `MainWindow.axaml(.cs)` nuevo; `App.axaml` con `DataTemplate`s;
  `App.axaml.cs` con `ShellViewModel` + navegación inicial a
  `RouteNames.Dashboard`.
- `Bootstrap.ConfigureServices` registra `ShellViewModel` (singleton),
  `INavigationService → NavigationService` (singleton),
  `DashboardViewModel` (transient) y `AddLocalization()`.
- `CommunityToolkit.Mvvm` 8.4.0 y `Microsoft.Extensions.Localization`
  8.0.1 en `Directory.Packages.props` (CPM); `.csproj` sin `Version`.
- `NavigationServiceTests` (≥ 4) + `ShellViewModelTests` (≥ 2) +
  `FakeNavigationService` + fake `IServiceProvider`; pure xUnit
  `Assert.*`, **no** FluentAssertions, **no** librería de mocking,
  **no** `Avalonia.Headless` (diferido a F-007).
- `dotnet build -c Release`: 0 warnings, 0 errors.
- `dotnet test`: ~204 (baseline F-005) + ~6 ≈ **210 verdes**.
- `dotnet run --project src/ControlDeFinanzasIA.UI` arranca y muestra
  el shell con "Dashboard" seleccionado y el placeholder visible.

## 2. Objetivo

1. **Establecer el contrato de navegación** que todas las features
   de producto siguientes consumen (`RouteNames`, `INavigationService`,
   `NavigationService`). F-007 (`ExpensesViewModel`) sólo añade una
   entrada al mapeo de rutas y un `DataTemplate`.
2. **Fijar el patrón MVVM de la app** con `CommunityToolkit.Mvvm`:
   `ViewModelBase` base, `sealed partial class` para source
   generators, `[ObservableProperty]` para estado, `[RelayCommand]`
   para acciones, sin lógica en code-behind.
3. **Introducir `IStringLocalizer`** con `.resx` (es-DO default + en
   fallback) para todos los strings de UI — sidebar, placeholder,
   título de ventana. Fija el patrón de localización que F-007 reusa.
4. **Reemplazar la ventana inline** de F-008 por `MainWindow.axaml`
   con layout `DockPanel` (sidebar + content + statusbar).
5. **Cubrir con ≥ 6 tests** la navegación y el shell usando fakes
   hand-rolled (sin `Avalonia.Headless`).

## 3. Descripción breve

| Tipo | Archivo | Responsabilidad |
| --- | --- | --- |
| `RouteNames` (static class) | `UI/Navigation/RouteNames.cs` | Constantes `Dashboard` y `Expenses` (F-007 consume `Expenses`). |
| `INavigationService` (interface) | `UI/Navigation/INavigationService.cs` | `CurrentViewModel`, `CanGoBack`, `NavigateTo(string)`, `GoBack()`, evento `CurrentChanged`. |
| `NavigationService` (sealed class) | `UI/Navigation/NavigationService.cs` | Extiende `ObservableObject`, implementa `INavigationService`. Resuelve VMs desde `IServiceProvider` vía mapeo `Dictionary<string, Type>`. Pila `Stack<ViewModelBase>`. Lanza `InvalidOperationException` en ruta desconocida. `GoBack` silent no-op sin historial. Dispara `CurrentChanged`. |
| `ViewModelBase` (abstract class) | `UI/ViewModels/ViewModelBase.cs` | `abstract class ViewModelBase : ObservableObject`. Base de todos los VMs. |
| `ShellViewModel` (sealed partial class) | `UI/ViewModels/ShellViewModel.cs` | `[ObservableProperty] current` sincronizado con `INavigationService.CurrentViewModel`. `[RelayCommand] NavigateTo(string)` y `[RelayCommand(CanExecute = nameof(CanGoBack))] GoBack` que delegan al servicio. Inyecta `INavigationService` + `IStringLocalizer<Strings>`. |
| `DashboardViewModel` (sealed partial class) | `UI/ViewModels/DashboardViewModel.cs` | Placeholder. Hereda `ViewModelBase`. Inyecta `IStringLocalizer<Strings>` para el texto del placeholder. |
| `MainWindow.axaml(.cs)` | `UI/Views/MainWindow.axaml(.cs)` | **NUEVO.** `DockPanel`: sidebar (`ListBox` de rutas) + `ContentControl` bindeado a `Shell.Current` + statusbar placeholder. Code-behind **vacío** (sin lógica). |
| `DashboardView.axaml(.cs)` | `UI/Views/DashboardView.axaml(.cs)` | Placeholder localizado. Code-behind vacío. |
| `Strings.resx` + `Strings.es-DO.resx` | `UI/Resources/` | `.resx` neutral/base (en, fallback) + `.resx` es-DO (default). `Strings.Designer.cs` auto-gen. |
| `App.axaml` | `UI/App.axaml` | **MODIFICAR.** Añade `DataTemplate`s (`ShellViewModel → MainWindow`, `DashboardViewModel → DashboardView`) con `x:DataType` para CompiledBindings. |
| `App.axaml.cs` | `UI/App.axaml.cs` | **MODIFICAR.** Resuelve `ShellViewModel`, navega a `RouteNames.Dashboard` al arranque, asigna `DataContext`. Elimina `CreateMainWindow()` inline. |
| `Bootstrap.ConfigureServices` | `UI/Composition/Bootstrap.cs` | **MODIFICAR.** Registra `ShellViewModel` (singleton), `INavigationService → NavigationService` (singleton), `DashboardViewModel` (transient), `services.AddLocalization()`. |
| `Directory.Packages.props` | raíz | **MODIFICAR.** Añade `CommunityToolkit.Mvvm` 8.4.0, `Microsoft.Extensions.Localization` 8.0.1. |
| `ControlDeFinanzasIA.UI.csproj` | `UI/` | **MODIFICAR.** Añade `PackageReference` (sin `Version`) de los 2 paquetes; `<EmbeddedResource>` para `.resx`. |
| `NavigationServiceTests` | `Tests.Unit/UI/` | ≥ 4 tests con fake `IServiceProvider` hand-rolled. |
| `ShellViewModelTests` | `Tests.Unit/UI/` | ≥ 2 tests con `FakeNavigationService` hand-rolled. |
| `FakeNavigationService` | `Tests.Unit/TestDoubles/` | Implementa `INavigationService`; registra llamadas para aserciones. |

## 4. Dependencias

| Dependencia | Estado | Uso en F-006 |
| --- | --- | --- |
| F-008 de Fase 1 (archivada) | ✅ | `Bootstrap.ConfigureServices`, `App.axaml.cs` con `IServiceProvider`, `Program.cs` con host. F-006 extiende `Bootstrap` y reescribe el arranque de la ventana. |
| `CommunityToolkit.Mvvm` 8.4.0 | **NUEVO (CPM)** | `[ObservableProperty]`, `[RelayCommand]` (`CanExecute`), `ObservableObject`. Source generators requieren `partial class`. Compatible .NET 8. |
| `Microsoft.Extensions.Localization` 8.0.1 | **NUEVO (CPM)** | `IStringLocalizer<T>`, `AddLocalization()`. Alineado con los pins `Microsoft.Extensions.*` 8.0.1 existentes. |
| Avalonia 11.2.7 + Avalonia.Themes.Fluent | ✅ CPM | `FluentTheme` (design tokens centralizados, **no** estilos inline). `AvaloniaUseCompiledBindingsByDefault=true` ya en `.csproj` → `DataTemplate`s con `x:DataType`. |
| NetArchTest R-1..R-5 | ✅ | F-006 sólo toca `UI` (salvo `Bootstrap`, excepción R-4 ya establecida). Sin cambios en las reglas. |
| F-001..F-005 de Fase 3 | ✅ archivadas | **No se consumen.** F-006 es infraestructura de UI; no referencia `Expense`, use cases ni repositorios. |

**Bloqueos:** Ninguno. F-006 es la feature más independiente de la
fase (no toca `Domain`/`Application`/`Infrastructure`).

## 5. Fuera de alcance

- **F-007** (`ExpensesViewModel`/`ExpensesView`, navegación real a
  gastos, consumo de `RegisterExpenseUseCase`/`GetTransactionsQuery`).
- **`Avalonia.Headless`** — diferido a F-007. Los tests de F-006 son
  C# puro (VMs + `NavigationService`); no renderizan XAML.
- **Contenido real de dashboard** — placeholder localizado, sin
  datos ni gráficos.
- **Temas / dark mode** (Phase 4+). Se reusa `FluentTheme`; F-006
  **no** innova estética (es infraestructura). Sin estilos inline;
  design tokens centralizados vía `FluentTheme`.
- **Multi-ventana** (Phase 4+). Una sola `MainWindow`.
- **Persistencia de última ruta** (Phase 4+). El historial es
  in-memory; al cerrar la app se pierde.
- **Filtros expuestos en UI** (`CategoryId`/`DateFrom`/`DateTo`) —
  Phase 4.
- **Wrapping de mensajes de error de use cases con `IStringLocalizer`**
  — eso es concern de la capa de Application (F-004/F-005 ya fijaron
  códigos en inglés + mensajes en español). F-006 localiza sólo
  strings de **shell/UI**.
- **Modificar** `Domain`/`Application`/`Infrastructure` (salvo el
  registro DI en `Bootstrap`, que vive en `UI/Composition/`).
- **FluentAssertions** o **librería de mocking**. Tests con pure
  xUnit `Assert.*` y fakes hand-rolled.

## 6. Decisiones de diseño (fijadas por la propuesta — no re-abrir)

| # | Decisión | Rationale |
| --- | --- | --- |
| DP-1 | Ruta desconocida → `throw new InvalidOperationException("Unknown route: X")` en `NavigationService.NavigateTo`. | Es un error de developer (mapeo incompleto), no de usuario. Fallar rápido y ruidoso en desarrollo. F-007 puede añadir rutas sin tocar el contrato. |
| DP-2 | `GoBack()` sin historial es **silent no-op** a nivel servicio (no lanza). El `[RelayCommand] GoBack` en `ShellViewModel` usa `CanExecute = nameof(CanGoBack)` → el botón se auto-deshabilita cuando `_navService.CanGoBack` es `false`. | UX: un botón deshabilitado es más claro que un error. El contrato del servicio es permisivo (no lanza); el VM lo protege vía `CanExecute`. |
| DP-3 | Ruta inicial al arranque: `RouteNames.Dashboard` (per phase plan). | Es el destino neutral por defecto. F-007 añade `Expenses` pero el arranque sigue en Dashboard. |
| DP-4 | `Avalonia.Headless` **diferido a F-007**. Los tests de F-006 son C# puro (`NavigationService` + VMs no renderizan XAML). F-007 introduce `Avalonia.Headless` sólo si los smoke tests lo requieren. | F-006 no necesita renderizar para testear navegación. Diferir reduce el scope y el riesgo de la primera feature de UI. |
| DP-5 | `IStringLocalizer` desde F-006. `.resx` con es-DO default + en fallback. `Strings.resx` (neutral/base = en) + `Strings.es-DO.resx` (es-DO). Se setea la cultura default a es-DO al arranque. | Fija el patrón de localización para F-007. Honra `tech-stack.md` (es-DO default + en, no hardcodear strings de UI). |

> **DP-6 (anti-dependencia circular):** `NavigationService` recibe
> **sólo** `IServiceProvider` (nunca `ShellViewModel`). Los VMs de
> feature reciben `INavigationService`, nunca `ShellViewModel` ni
> hermanos. Esto previene el ciclo DI `Shell ↔ NavigationService ↔
> FeatureVM`.

## 7. Cambios al alcance

**Se añade** (sólo `UI` y `Tests.Unit`): 3 archivos en
`UI/Navigation/`, 3 en `UI/ViewModels/`, 2 vistas (`MainWindow`,
`DashboardView`) con sus code-behind, 2 `.resx` + 1 designer en
`UI/Resources/`, 2 archivos de tests en `Tests.Unit/UI/`, 1
`FakeNavigationService` en `Tests.Unit/TestDoubles/`, y un fake
`IServiceProvider` hand-rolled (inline en `NavigationServiceTests`
o como clase helper).

**Se modifica:** `App.axaml` (DataTemplates), `App.axaml.cs`
(ShellViewModel + navegación inicial, elimina `CreateMainWindow`
inline), `Bootstrap.ConfigureServices` (4 registros nuevos +
`AddLocalization()`), `Directory.Packages.props` (2
`PackageVersion` nuevos), `ControlDeFinanzasIA.UI.csproj` (2
`PackageReference` sin `Version` + `<EmbeddedResource>` para
`.resx`).

**NO se modifica:** `Domain`, `Application`, `Infrastructure`
(ningún archivo — el único punto de contacto es `Bootstrap` en
`UI/Composition/`, excepción R-4 ya establecida); `Program.cs`
(salvo, opcionalmente, setear `CultureInfo.DefaultThreadCurrentUICulture`
a es-DO — decisión menor, ver §11); NetArchTest R-1..R-5;
`Result`/`PagedResult<T>`/use cases/repositorios (F-001..F-005).

## 8. Acceptance Criteria

La feature se considera completada cuando **todos** los ACs se
cumplen, verificables mediante tests automatizados y revisión de
código.

### AC-1 — `RouteNames`
- [ ] `src/ControlDeFinanzasIA.UI/Navigation/RouteNames.cs` existe
      como `public static class RouteNames` con
      `public const string Dashboard = "Dashboard";` y
      `public const string Expenses = "Expenses";`.
- [ ] File-scoped namespace, XML doc. `Expenses` se declara ahora
      (F-007 lo consume) — no se añade después.

### AC-2 — `ViewModelBase`
- [ ] `public abstract class ViewModelBase : ObservableObject` en
      `UI/ViewModels/ViewModelBase.cs` (`ObservableObject` de
      `CommunityToolkit.Mvvm.ComponentModel`).
- [ ] `sealed partial` NO aplica aquí (es abstract); los VMs
      concretos son `sealed partial`.

### AC-3 — `INavigationService`
- [ ] `public interface INavigationService` en
      `UI/Navigation/INavigationService.cs` con 5 miembros:
      `ViewModelBase? CurrentViewModel { get; }`,
      `bool CanGoBack { get; }`,
      `void NavigateTo(string routeName)`,
      `void GoBack()`,
      `event EventHandler? CurrentChanged`.

### AC-4 — `NavigationService` resuelve desde `IServiceProvider`
- [ ] `public sealed class NavigationService : ObservableObject, INavigationService`.
- [ ] Constructor inyecta `IServiceProvider`. Mantiene
      `Dictionary<string, Type> _routes` (mapeo `RouteNames.Dashboard
      → typeof(DashboardViewModel)`; `Expenses` se añade en F-007).
- [ ] `NavigateTo` resuelve el VM vía
      `_services.GetRequiredService(type)` (o `GetService` + null
      check), push sobre `Stack<ViewModelBase> _history`, asigna
      `CurrentViewModel`, dispara `OnPropertyChanged(nameof(CurrentViewModel))`
      y `CurrentChanged`.

### AC-5 — Ruta desconocida lanza (DP-1)
- [ ] `NavigateTo("Nope")` lanza
      `InvalidOperationException` con mensaje que contiene
      `"Unknown route"`.

### AC-6 — `GoBack` silent no-op sin historial (DP-2)
- [ ] `GoBack()` en un servicio sin historial **no lanza**; deja
      `CurrentViewModel` sin cambiar.
- [ ] `CanGoBack` devuelve `false` cuando `_history` tiene ≤ 1
      elemento (no se puede retroceder más allá de la raíz).

### AC-7 — `CurrentChanged` se dispara en cada navegación
- [ ] Cada `NavigateTo` y cada `GoBack` (con historial) dispara
      `CurrentChanged` una vez.

### AC-8 — `ShellViewModel` con `[ObservableProperty]`/`[RelayCommand]`
- [ ] `public sealed partial class ShellViewModel : ObservableObject`
      en `UI/ViewModels/ShellViewModel.cs`.
- [ ] `[ObservableProperty] private ViewModelBase? current;`
      sincronizado con `_navService.CurrentViewModel` (se subscribe
      a `CurrentChanged` y actualiza `Current`).
- [ ] `[RelayCommand] private void NavigateTo(string routeName)` →
      `_navService.NavigateTo(routeName)`.
- [ ] `[RelayCommand(CanExecute = nameof(CanGoBack))] private void GoBack()`
      → `_navService.GoBack()`; `private bool CanGoBack() =>
      _navService.CanGoBack;` (auto-deshabilita el botón).

### AC-9 — `MainWindow.axaml` layout DockPanel
- [ ] `src/ControlDeFinanzasIA.UI/Views/MainWindow.axaml` existe
      (NUEVO). Layout `DockPanel`: sidebar (`ListBox` de rutas con
      `DockPanel.Dock="Left"`) + `ContentControl` centrado bindeado
      a `Shell.Current` + statusbar placeholder
      (`DockPanel.Dock="Bottom"`).
- [ ] `MainWindow.axaml.cs` code-behind **vacío** (sin lógica; sólo
      el constructor que llama a `InitializeComponent`).
- [ ] Strings del sidebar/título vía `IStringLocalizer` (no
      hardcodeados).

### AC-10 — `DataTemplate`s en `App.axaml`
- [ ] `App.axaml` registra `DataTemplate`s para
      `DashboardViewModel → DashboardView` (con `x:DataType` para
      CompiledBindings, ya que `AvaloniaUseCompiledBindingsByDefault=true`).
- [ ] El `ShellViewModel → MainWindow` se resuelve como
      `MainWindow.DataContext` en `App.axaml.cs` (no como
      `DataTemplate` — `MainWindow` es la ventana raíz, no un
      contenido).

### AC-11 — `App.axaml.cs` arranque con `ShellViewModel`
- [ ] Resuelve `ShellViewModel` desde `IServiceProvider`.
- [ ] Navega a `RouteNames.Dashboard` al inicializar.
- [ ] Crea `MainWindow` (desde XAML / `new MainWindow()`), asigna
      `DataContext = shellViewModel`, lo setea como
      `desktop.MainWindow`.
- [ ] **Elimina** el `CreateMainWindow()` inline de F-008 (el método
      `private static Window CreateMainWindow() => new() { ... }`).

### AC-12 — `Bootstrap` registra servicios + `AddLocalization()`
- [ ] `Bootstrap.ConfigureServices` añade:
      `services.AddSingleton<ShellViewModel>()`,
      `services.AddSingleton<INavigationService, NavigationService>()`,
      `services.AddTransient<DashboardViewModel>()`,
      `services.AddLocalization()`.
- [ ] No se modifican registros existentes (`IClock`,
      `IPathResolver`, use cases, `AddPersistence`).

### AC-13 — `IStringLocalizer` para strings de UI (DP-5)
- [ ] `UI/Resources/Strings.resx` (neutral/base = en) y
      `UI/Resources/Strings.es-DO.resx` (es-DO) existen con claves
      para: título de ventana, ítems del sidebar (`Dashboard`,
      `Expenses`), botón `GoBack`, texto del placeholder del
      dashboard.
- [ ] `Strings.Designer.cs` auto-generado (vía
      `ResXFileCodeGenerator` o `<GenerateDocumentationFile>` +
      build action; verificar EmbeddedResource).
- [ ] `.csproj` declara los `.resx` como `<EmbeddedResource>`.
- [ ] `ShellViewModel` y `DashboardViewModel` inyectan
      `IStringLocalizer<Strings>` y lo usan para los strings (no
      literales hardcodeados).
- [ ] La cultura default de UI se setea a es-DO al arranque
      (`CultureInfo.DefaultThreadCurrentUICulture = new("es-DO")`
      en `Program.cs` o `App.axaml.cs`); fallback a `Strings.resx`
      (en) si falta una clave.

### AC-14 — Paquetes CPM y `.csproj`
- [ ] `Directory.Packages.props` añade
      `<PackageVersion Include="CommunityToolkit.Mvvm" Version="8.4.0" />`
      y
      `<PackageVersion Include="Microsoft.Extensions.Localization" Version="8.0.1" />`
      con comentario justificante.
- [ ] `ControlDeFinanzasIA.UI.csproj` añade
      `<PackageReference Include="CommunityToolkit.Mvvm" />` y
      `<PackageReference Include="Microsoft.Extensions.Localization" />`
      **sin** `Version` (CPM).
- [ ] Ningún otro `.csproj` referencia estos paquetes (sólo UI).

### AC-15 — Tests (≥ 4 NavigationService + ≥ 2 ShellViewModel)
- [ ] `tests/ControlDeFinanzasIA.Tests.Unit/UI/NavigationServiceTests.cs`
      con ≥ 4 tests (ver §9) usando un fake `IServiceProvider`
      hand-rolled (diccionario `Type → object`).
- [ ] `tests/ControlDeFinanzasIA.Tests.Unit/UI/ShellViewModelTests.cs`
      con ≥ 2 tests (ver §9) usando `FakeNavigationService`
      hand-rolled.
- [ ] `tests/ControlDeFinanzasIA.Tests.Unit/TestDoubles/FakeNavigationService.cs`
      implementa `INavigationService`; registra llamadas.
- [ ] Pure xUnit `Assert.*`. **No** FluentAssertions, **no**
      librería de mocking, **no** `Avalonia.Headless`.

### AC-16 — Sin scope creep
- [ ] NO `ExpensesViewModel`/`ExpensesView` (F-007), NO consumo de
      `RegisterExpenseUseCase`/`GetTransactionsQuery`, NO
      `Avalonia.Headless`, NO temas/dark mode, NO multi-ventana, NO
      persistencia de ruta, NO wrapping de errores de use cases con
      `IStringLocalizer`, NO modificación de
      `Domain`/`Application`/`Infrastructure`.

### AC-17 — NetArchTest R-1..R-5
- [ ] R-1..R-5 verdes sin cambios. UI referencia `Application` +
      `Domain`; `Bootstrap` (excepción R-4) es el único punto que
      toca `Infrastructure`. Los VMs no referencian
      `Infrastructure` directamente (sólo vía interfaces resueltas
      en DI — F-007 consume use cases; F-006 no).

### AC-18 — Build, tests, format, smoke
- [ ] `dotnet build -c Release`: 0 warnings, 0 errors.
- [ ] `dotnet test -c Release --no-build`: ~204 + ~6 ≈ **210
      verdes**, 0 failures.
- [ ] `dotnet format style --verify-no-changes`: exit 0.
- [ ] `dotnet run --project src/ControlDeFinanzasIA.UI` arranca,
      muestra el shell con sidebar, "Dashboard" seleccionado y el
      placeholder visible en el content area (smoke test manual
      documentado).

## 9. Test cases

### 9.1 `NavigationServiceTests.cs` (≥ 4, fake `IServiceProvider`)

| # | Método | Escenario |
| --- | --- | --- |
| N1 | `NavigateTo_Dashboard_SetsCurrentAndPushesHistory` | Fake provider devuelve `DashboardViewModel` para `typeof(DashboardViewModel)`. `NavigateTo(RouteNames.Dashboard)` → `CurrentViewModel` es el `DashboardViewModel`; `CanGoBack` es `false` (1 elemento en la pila). |
| N2 | `NavigateTo_UnknownRoute_ThrowsInvalidOperationException` | `NavigateTo("Nope")` → `Assert.Throws<InvalidOperationException>`; mensaje contiene `"Unknown route"`. |
| N3 | `GoBack_WithoutHistory_IsSilentNoop` | Servicio fresco, `GoBack()` → no lanza; `CurrentViewModel` sigue `null` (o sin cambiar). |
| N4 | `CurrentChanged_FiresOnEveryNavigation` | Subscribe a `CurrentChanged`; `NavigateTo(Dashboard)` dispara 1 vez; un segundo `NavigateTo` dispara otra. Contar invocaciones con un contador. |
| N5 (rec.) | `NavigateTo_Twice_ThenGoBack_RestoresPreviousCurrent` | Navegar Dashboard → otra ruta simulada → `GoBack()` → `CurrentViewModel` vuelve a ser el `DashboardViewModel`; `CanGoBack` `false`. (Requiere un segundo VM fake registrado en el provider.) |
| N6 (rec.) | `CanGoBack_TrueAfterSecondNavigation_FalseAfterGoBackToRoot` | Tras 2 navegaciones, `CanGoBack` `true`; tras `GoBack`, `false`. |

> Fake `IServiceProvider`: clase hand-rolled con
> `Dictionary<Type, object>` (o `Func<Type, object?>`); `GetService`
> devuelve la instancia registrada; `GetRequiredService` lanza si
> falta. Sin librería de mocking.

### 9.2 `ShellViewModelTests.cs` (≥ 2, `FakeNavigationService`)

| # | Método | Escenario |
| --- | --- | --- |
| S1 | `NavigateToCommand_DelegatesToNavigationService` | `ShellViewModel` con `FakeNavigationService`. Invocar `NavigateToCommand` con `RouteNames.Dashboard` → el fake registra `NavigateTo("Dashboard")` fue llamado. |
| S2 | `GoBackCommand_CanExecute_BoundToCanGoBack` | Fake con `CanGoBack = false` → `GoBackCommand.CanExecute(null)` es `false`. Setear `CanGoBack = true` (vía `NotifyCanExecuteChanged` o re-evaluar) → `CanExecute` es `true`. |
| S3 (rec.) | `Current_SyncedWhenNavigationServiceRaisesCurrentChanged` | Fake dispara `CurrentChanged` con un nuevo `CurrentViewModel` → `ShellViewModel.Current` refleja el nuevo valor. |

### 9.3 `FakeNavigationService`

| # | Miembro | Propósito |
| --- | --- | --- |
| F1 | `CurrentViewModel` (get; set privado) | Configurable para tests. |
| F2 | `CanGoBack` (get; set) | Configurable; drive el `CanExecute` del `GoBackCommand`. |
| F3 | `NavigateTo(string)` | Registra la última ruta en `LastNavigateRoute`; incrementa contador. |
| F4 | `GoBack()` | Registra que fue llamado (`GoBackCalled = true`). |
| F5 | `CurrentChanged` event | Disparable desde el test para S3. |

## 10. Estructura de archivos esperada

```
src/
└── ControlDeFinanzasIA.UI/
    ├── Navigation/                              ← NUEVO (F-006)
    │   ├── RouteNames.cs                        ← nuevo
    │   ├── INavigationService.cs                ← nuevo
    │   └── NavigationService.cs                 ← nuevo
    ├── ViewModels/                              ← NUEVO (F-006)
    │   ├── ViewModelBase.cs                     ← nuevo (abstract : ObservableObject)
    │   ├── ShellViewModel.cs                    ← nuevo (sealed partial)
    │   └── DashboardViewModel.cs                ← nuevo (sealed partial)
    ├── Views/                                   ← NUEVO (F-006)
    │   ├── MainWindow.axaml                     ← nuevo (reemplaza ventana inline)
    │   ├── MainWindow.axaml.cs                  ← nuevo (code-behind vacío)
    │   ├── DashboardView.axaml                  ← nuevo (placeholder)
    │   └── DashboardView.axaml.cs               ← nuevo (code-behind vacío)
    ├── Resources/                               ← NUEVO (F-006)
    │   ├── Strings.resx                         ← nuevo (neutral/base = en)
    │   ├── Strings.es-DO.resx                   ← nuevo (es-DO default)
    │   └── Strings.Designer.cs                  ← auto-gen
    ├── Composition/
    │   └── Bootstrap.cs                         ← MODIFICAR (4 registros + AddLocalization)
    ├── System/                                  (sin cambios)
    ├── App.axaml                                ← MODIFICAR (DataTemplates)
    ├── App.axaml.cs                             ← MODIFICAR (ShellViewModel + Dashboard; elimina CreateMainWindow)
    ├── Program.cs                               (sin cambios salvo cultura default, opcional)
    └── ControlDeFinanzasIA.UI.csproj            ← MODIFICAR (2 PackageReference + EmbeddedResource)

Directory.Packages.props                         ← MODIFICAR (2 PackageVersion)

(Domain, Application, Infrastructure: SIN cambios)

tests/
└── ControlDeFinanzasIA.Tests.Unit/
    ├── UI/                                      ← NUEVO (carpeta)
    │   ├── NavigationServiceTests.cs            ← nuevo
    │   └── ShellViewModelTests.cs               ← nuevo
    └── TestDoubles/
        ├── FakeClock.cs                         (sin cambios)
        ├── FakeExpenseRepository.cs             (sin cambios)
        └── FakeNavigationService.cs             ← nuevo
```

## 11. Convenciones

- **MVVM estricto** (per `frontend-design` skill + `tech-stack.md`):
  sin lógica en code-behind. `MainWindow.axaml.cs` y
  `DashboardView.axaml.cs` están **vacíos** salvo
  `InitializeComponent`. Toda navegación vía comandos en
  `ShellViewModel`. ViewModels testeables sin UI.
- **Design tokens centralizados**: se reusa `FluentTheme` (ya en
  `App.axaml`). **No** estilos inline. F-006 **no** innova estética
  — es infraestructura; la estética refinada llega con features de
  producto (F-007+).
- **`CommunityToolkit.Mvvm` source generators**: toda VM con
  `[ObservableProperty]`/`[RelayCommand]` es `sealed partial class`
  (requisito del generador, verificado vía Context7). `ViewModelBase`
  es `abstract` (no necesita `partial` salvo por consistencia).
- **`IStringLocalizer<Strings>`** para strings de UI (no
  hardcodeados). `.resx` es-DO default + en fallback. Códigos de
  error de use cases (F-004/F-005) **no** se localizan aquí.
- **Código:** file-scoped namespace, `using` al tope, XML doc en
  cada clase y miembro público, `sealed` en tipos concretos.
- **Tests:** pure xUnit `[Fact]`/`[Theory]` + `Assert.*`
  (`True`/`False`/`Equal`/`NotNull`/`Throws`/`ThrowsAsync`).
  Naming `Method_Scenario_ExpectedResult`. **No** FluentAssertions.
  **No** librería de mocking (Moq/NSubstitute). **No**
  `Avalonia.Headless` (DP-4). Fakes hand-rolled
  (`FakeNavigationService`, fake `IServiceProvider`).
- **CPM:** versiones sólo en `Directory.Packages.props`;
  `.csproj` sin `Version`.

## 12. Riesgos

| Riesgo | Prob. | Mitigación |
| --- | --- | --- |
| `DataTemplate` no resuelve con CompiledBindings (`AvaloniaUseCompiledBindingsByDefault=true`) → content vacío. | Media | `x:DataType` correcto en cada `DataTemplate`; smoke test manual (AC-18) documenta el síntoma (vista vacía = `x:DataType`/namespace mal). El compilador emite warnings de bindings si falla. |
| Dependencia circular DI si `INavigationService` recibe `ShellViewModel`. | Baja | DP-6: `NavigationService` recibe sólo `IServiceProvider`; los VMs de feature reciben `INavigationService`, nunca `ShellViewModel`. |
| `.resx`/designer no se genera o no se incrusta como `EmbeddedResource` → `IStringLocalizer` no encuentra recursos. | Media | AC-13 exige `<EmbeddedResource>` en `.csproj` y verificar `Strings.Designer.cs`. `AddLocalization()` busca recursos embebidos por convención. |
| `CommunityToolkit.Mvvm` source generators no corren sin `partial`. | Baja | Toda VM con atributos es `sealed partial class` (AC-8, §11). Error de compilación claro si se omite. |
| `IStringLocalizer<Strings>` no resuelve porque el tipo marker `Strings` no existe o no coincide con el nombre base del `.resx`. | Media | El `.resx` se llama `Strings.resx` y el designer genera `Strings.Designer.cs` con `class Strings`; `IStringLocalizer<Strings>` busca `Strings` por convención. Verificar al apply. |
| Scope creep: tentación de implementar `ExpensesViewModel` aquí. | Baja | AC-16 lo prohíbe explícitamente. `RouteNames.Expenses` se declara pero su mapeo/VM es F-007. |
| `GoBack` command no se re-evalúa al cambiar `CanGoBack` del servicio. | Media | `ShellViewModel` se subscribe a `CurrentChanged` (o `CanGoBack` es property observable) y llama `GoBackCommand.NotifyCanExecuteChanged()` para refrescar el botón. |

## 13. Criterios para considerarla completada

- [ ] `dotnet build -c Release` desde la raíz: 0 warnings, 0 errors.
- [ ] `dotnet test -c Release --no-build`: ~204 (baseline F-005) +
      ~6 ≈ **210 tests verdes**, 0 failures.
- [ ] NetArchTest R-1..R-5 verdes (F-006 sólo toca `UI` salvo
      `Bootstrap`, excepción R-4 ya establecida).
- [ ] `dotnet format style --verify-no-changes`: exit 0.
- [ ] `dotnet run --project src/ControlDeFinanzasIA.UI` arranca,
      muestra el shell con sidebar, "Dashboard" seleccionado y el
      placeholder visible (smoke test manual).
- [ ] F-006 marcada como ✅ COMPLETADA en
      `spec/phases/phase-03/features.md` tras apply + verify; la
      spec se renombra a `F-006-ui-shell-navigation.done.md` tras
      sdd-archive.

## 14. Referencias

- `spec/phases/phase-03/features.md` — F-006 en el desglose de
  Phase 3 (rama UI infra, independiente de F-001..F-005).
- `spec/features/phase-01/F-008-composition-root.done.md` —
  `Bootstrap.ConfigureServices`, `App.axaml.cs` con ventana inline
  `CreateMainWindow()` (que F-006 reemplaza), `Program.cs` con host.
- `spec/features/phase-03/F-001-expense-domain-entity.done.md` …
  `F-005-get-transactions-query.done.md` — baseline ~204 tests; **no
  se consumen** en F-006 (la navegación no referencia `Expense`).
- `spec/constitution/tech-stack.md` — MVVM estricto, sin lógica en
  code-behind, design tokens centralizados, `IStringLocalizer`
  es-DO + en, sin strings hardcodeados.
- `.claude/skills/frontend-design/SKILL.md` — MVVM estricto, no
  estilos inline, no estética genérica de IA.
- `docs/architecture.md` §9 (capa de UI), §10 (navegación).
- `docs/vision.md` §4.1 (gastos — contexto; F-006 es prerrequisito).
- [CommunityToolkit.Mvvm — docs (Context7,
  `/websites/learn_microsoft_en-us_dotnet_communitytoolkit_mvvm`)](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/)
  — `[ObservableProperty]`, `[RelayCommand(CanExecute)]`,
  `ObservableObject`, requisito `partial class`.
- [Microsoft.Extensions.Localization —
  `IStringLocalizer`, `AddLocalization`](https://learn.microsoft.com/en-us/dotnet/core/extensions/globalization-and-localization)
  (alinear a 8.0.1 con los pins existentes).
- [Avalonia 11 — DataTemplates, CompiledBindings,
  `x:DataType`](https://docs.avaloniaui.net/) (Context7 al aplicar
  para confirmar el patrón `DataTemplate` + `x:DataType`).
