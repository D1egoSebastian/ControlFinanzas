# Tareas — F-006: UI: shell, `ShellViewModel`, navegación por contenido, DataTemplates

> **Spec:** `spec/features/phase-03/F-006-ui-shell-navigation.spec.md`
> **Fuentes de verdad:** la spec, `docs/architecture.md` §9/§10,
> `spec/constitution/tech-stack.md`, `spec/features/phase-01/F-008-composition-root.done.md`,
> `.claude/skills/frontend-design/SKILL.md`.
>
> F-006 añade infraestructura de UI; no toca `Domain`, `Application` ni
> `Infrastructure` (salvo `Bootstrap`).

---

## Pronóstico de carga de review

| Field | Value |
|-------|-------|
| Estimated changed lines | ~450–550 |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 → PR 2 → PR 3 |
| Delivery strategy | auto-forecast |
| Chain strategy | stacked-to-main |

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: High

### Unidades de trabajo sugeridas

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Paquetes CPM + recursos `.resx` base | PR 1 | Base = `main`; T-001..T-003 |
| 2 | Contrato de navegación + ViewModels base | PR 2 | Base = `main`; T-004..T-008 + T-013 |
| 3 | Vistas, DataTemplates, wiring, tests y verificación | PR 3 | Base = PR 2; T-009..T-012 + T-014..T-017 |

---

## Resumen

F-006 introduce el esqueleto de UI: navegación por contenido
(`RouteNames`, `INavigationService`, `NavigationService`), `ViewModelBase`,
`ShellViewModel`, `MainWindow` (`DockPanel`: sidebar + content + statusbar),
`DataTemplate`s en `App.axaml`, `DashboardViewModel`/`DashboardView` placeholder,
y `IStringLocalizer` con `.resx` (es-DO default + en fallback). Reemplaza el
`new Window()` inline de F-008 por `MainWindow.axaml`.

Entregables:

- `src/ControlDeFinanzasIA.UI/Navigation/RouteNames.cs`
- `src/ControlDeFinanzasIA.UI/Navigation/INavigationService.cs`
- `src/ControlDeFinanzasIA.UI/Navigation/NavigationService.cs`
- `src/ControlDeFinanzasIA.UI/ViewModels/ViewModelBase.cs`
- `src/ControlDeFinanzasIA.UI/ViewModels/ShellViewModel.cs`
- `src/ControlDeFinanzasIA.UI/ViewModels/DashboardViewModel.cs`
- `src/ControlDeFinanzasIA.UI/Views/MainWindow.axaml` + `.cs`
- `src/ControlDeFinanzasIA.UI/Views/DashboardView.axaml` + `.cs`
- `src/ControlDeFinanzasIA.UI/Resources/Strings.resx` + `Strings.es-DO.resx` + `Strings.Designer.cs`
- `src/ControlDeFinanzasIA.UI/App.axaml` (DataTemplates)
- `src/ControlDeFinanzasIA.UI/App.axaml.cs` (arranque con `ShellViewModel`)
- `src/ControlDeFinanzasIA.UI/Composition/Bootstrap.cs`
- `Directory.Packages.props` (2 `PackageVersion` nuevos)
- `src/ControlDeFinanzasIA.UI/ControlDeFinanzasIA.UI.csproj`
- `tests/ControlDeFinanzasIA.Tests.Unit/UI/NavigationServiceTests.cs`
- `tests/ControlDeFinanzasIA.Tests.Unit/UI/ShellViewModelTests.cs`
- `tests/ControlDeFinanzasIA.Tests.Unit/TestDoubles/FakeNavigationService.cs`
- `dotnet build` limpio; `dotnet test` ≈ **210 verdes**; NetArchTest R-1..R-5 verdes.

---

## Resumen de tareas

| ID | Tarea | Diff | Depende de | Verificación |
| --- | --- | --- | --- | --- |
| [x] T-001 | Añadir `CommunityToolkit.Mvvm` 8.4.0 a CPM y `UI.csproj` | +4 / -0 | — | `dotnet build` |
| [x] T-002 | Añadir `Microsoft.Extensions.Localization` 8.0.1 a CPM y `UI.csproj` | +4 / -0 | — | `dotnet build` |
| [x] T-003 | Crear `Strings.resx`, `Strings.es-DO.resx`, `Strings.Designer.cs` | +60 / -0 | T-002 | `dotnet build` |
| [x] T-004 | Crear `RouteNames` con `Dashboard` y `Expenses` | +12 / -0 | — | `dotnet build` |
| [x] T-005 | Crear `INavigationService` y `NavigationService` | +60 / -0 | T-004 | `dotnet build` |
| [x] T-006 | Crear `ViewModelBase` (abstract : `ObservableObject`) | +10 / -0 | T-001 | `dotnet build` |
| [x] T-007 | Crear `ShellViewModel` (`sealed partial`, `[ObservableProperty]`, `[RelayCommand]`) | +45 / -0 | T-001, T-005, T-006 | `dotnet build` |
| [x] T-008 | Crear `DashboardViewModel` placeholder | +20 / -0 | T-001, T-003, T-006 | `dotnet build` |
| [x] T-009 | Crear `MainWindow.axaml` (DockPanel) + code-behind vacío | +55 / -0 | T-007 | `dotnet build` |
| [x] T-010 | Crear `DashboardView.axaml` placeholder + code-behind vacío | +25 / -0 | T-003, T-008 | `dotnet build` |
| [x] T-011 | Modificar `App.axaml` para registrar DataTemplates | +20 / -2 | T-007, T-008, T-010 | `dotnet build` |
| [x] T-012 | Modificar `App.axaml.cs` para arranque con `ShellViewModel` | +15 / -15 | T-005, T-007, T-009 | `dotnet build` |
| [x] T-013 | Registrar servicios y `AddLocalization()` en `Bootstrap` | +8 / -0 | T-005, T-007, T-008 | `dotnet build` |
| [x] T-014 | Crear `FakeNavigationService` en `TestDoubles` | +35 / -0 | — | `dotnet build` tests |
| [x] T-015 | Crear `NavigationServiceTests` (≥ 4 tests, fake `IServiceProvider`) | +90 / -0 | T-005, T-014 | `dotnet test --filter "FullyQualifiedName~NavigationServiceTests"` ≥ 4 passed |
| [x] T-016 | Crear `ShellViewModelTests` (≥ 2 tests, `FakeNavigationService`) | +45 / -0 | T-007, T-014 | `dotnet test --filter "FullyQualifiedName~ShellViewModelTests"` ≥ 2 passed |
| [x] T-017 | Verificación final, formato y cierre | +5 / -3 | T-001..T-016 | `dotnet test` ~210 passed; `dotnet format style --verify-no-changes` |

**Total estimado:** ~10–14 horas.

---

## T-001 — Añadir `CommunityToolkit.Mvvm` 8.4.0 a CPM y `UI.csproj`

**Archivos:** `Directory.Packages.props`, `src/ControlDeFinanzasIA.UI/ControlDeFinanzasIA.UI.csproj`

**Pasos:** Añadir `PackageVersion Include="CommunityToolkit.Mvvm" Version="8.4.0"` en CPM. Añadir `PackageReference Include="CommunityToolkit.Mvvm"` (sin `Version`) en `UI.csproj`. Ejecutar `dotnet build`.

**Verificación:** `dotnet build "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --nologo` exit 0; ningún otro `.csproj` referencia el paquete.

**Dependencias:** —
**Estimación:** XS (< 30 min)
**Trazabilidad:** AC-14

---

## T-002 — Añadir `Microsoft.Extensions.Localization` 8.0.1 a CPM y `UI.csproj`

**Archivos:** `Directory.Packages.props`, `src/ControlDeFinanzasIA.UI/ControlDeFinanzasIA.UI.csproj`

**Pasos:** Añadir `PackageVersion Include="Microsoft.Extensions.Localization" Version="8.0.1"` en CPM. Añadir `PackageReference Include="Microsoft.Extensions.Localization"` (sin `Version`) en `UI.csproj`. Ejecutar `dotnet build`.

**Verificación:** `dotnet build ... -c Release --nologo` exit 0.

**Dependencias:** —
**Estimación:** XS (< 30 min)
**Trazabilidad:** AC-14

---

## T-003 — Crear recursos `.resx`

**Archivos:** `src/ControlDeFinanzasIA.UI/Resources/Strings.resx`, `Strings.es-DO.resx`, `Strings.Designer.cs`, `src/ControlDeFinanzasIA.UI/ControlDeFinanzasIA.UI.csproj`

**Pasos:** Crear carpeta `Resources/`. Crear `Strings.resx` (en fallback) y `Strings.es-DO.resx` (es-DO default) con claves: `WindowTitle`, `SidebarDashboard`, `SidebarExpenses`, `GoBackButton`, `DashboardPlaceholder`. Generar `Strings.Designer.cs` con clase `Strings` pública. Declarar ambos `.resx` como `<EmbeddedResource>` en `.csproj`. Ejecutar `dotnet build`.

**Verificación:** `dotnet build ... -c Release --nologo` exit 0; existen los 3 archivos; `<EmbeddedResource>` presente.

**Dependencias:** T-002
**Estimación:** S (< 1 h)
**Trazabilidad:** AC-13

---

## T-004 — Crear `RouteNames`

**Archivos:** `src/ControlDeFinanzasIA.UI/Navigation/RouteNames.cs`

**Pasos:** Crear `public static class RouteNames` con `public const string Dashboard = "Dashboard"` y `public const string Expenses = "Expenses"`. File-scoped namespace, XML doc. Ejecutar `dotnet build`.

**Verificación:** Build exit 0; 2 constantes públicas; XML doc.

**Dependencias:** —
**Estimación:** XS (< 30 min)
**Trazabilidad:** AC-1

---

## T-005 — Crear `INavigationService` y `NavigationService`

**Archivos:** `src/ControlDeFinanzasIA.UI/Navigation/INavigationService.cs`, `src/ControlDeFinanzasIA.UI/Navigation/NavigationService.cs`

**Pasos:**
- `INavigationService`: `CurrentViewModel`, `CanGoBack`, `NavigateTo`, `GoBack`, `CurrentChanged`.
- `NavigationService`: `sealed class : ObservableObject, INavigationService`; constructor inyecta `IServiceProvider`; mantiene `Dictionary<string, Type> _routes` (`RouteNames.Dashboard → typeof(DashboardViewModel)`) y `Stack<ViewModelBase> _history`; `NavigateTo` lanza `InvalidOperationException("Unknown route: X")` si no existe, resuelve con `GetRequiredService`, push, asigna `CurrentViewModel`, notifica propiedad y dispara `CurrentChanged`; `GoBack` es no-op si `_history.Count <= 1`, pop/asigna/notifica/dispara; `CanGoBack` es `_history.Count > 1`.
- XML doc en clase y miembros públicos.
- Ejecutar `dotnet build`.

**Verificación:** Build exit 0; `sealed`; inyecta `IServiceProvider`; ruta desconocida lanza; `GoBack` sin historial no lanza; `CanGoBack` correcto.

**Dependencias:** T-004
**Estimación:** M (1–2 h)
**Trazabilidad:** AC-2..AC-7

---

## T-006 — Crear `ViewModelBase`

**Archivos:** `src/ControlDeFinanzasIA.UI/ViewModels/ViewModelBase.cs`

**Pasos:** Crear `public abstract class ViewModelBase : ObservableObject`. File-scoped namespace, XML doc. Ejecutar `dotnet build`.

**Verificación:** Build exit 0; clase abstracta hereda de `ObservableObject`.

**Dependencias:** T-001
**Estimación:** XS (< 30 min)
**Trazabilidad:** AC-2

---

## T-007 — Crear `ShellViewModel`

**Archivos:** `src/ControlDeFinanzasIA.UI/ViewModels/ShellViewModel.cs`

**Pasos:**
- `public sealed partial class ShellViewModel : ObservableObject`.
- Constructor inyecta `INavigationService` y `IStringLocalizer<Strings>`.
- Suscribirse a `_navService.CurrentChanged` para actualizar `Current` y llamar `GoBackCommand.NotifyCanExecuteChanged()`.
- `[ObservableProperty] private ViewModelBase? current;`.
- `[RelayCommand] private void NavigateTo(string routeName)` → `_navService.NavigateTo(routeName)`.
- `[RelayCommand(CanExecute = nameof(CanGoBack))] private void GoBack()` → `_navService.GoBack()`; `private bool CanGoBack() => _navService.CanGoBack`.
- Ejecutar `dotnet build`.

**Verificación:** Build exit 0; `sealed partial`; `[ObservableProperty]` y `[RelayCommand]` presentes; `CanExecute` delega a `_navService.CanGoBack`; `CurrentChanged` actualiza `Current`.

**Dependencias:** T-001, T-005, T-006
**Estimación:** M (1–2 h)
**Trazabilidad:** AC-8

---

## T-008 — Crear `DashboardViewModel` placeholder

**Archivos:** `src/ControlDeFinanzasIA.UI/ViewModels/DashboardViewModel.cs`

**Pasos:** `public sealed partial class DashboardViewModel : ViewModelBase`. Constructor inyecta `IStringLocalizer<Strings>`. `[ObservableProperty] private string title;` inicializado con `_localizer["DashboardPlaceholder"]`. Ejecutar `dotnet build`.

**Verificación:** Build exit 0; hereda `ViewModelBase`; `[ObservableProperty]`; título desde localizador.

**Dependencias:** T-001, T-003, T-006
**Estimación:** XS (< 30 min)
**Trazabilidad:** AC-8, AC-13

---

## T-009 — Crear `MainWindow.axaml`

**Archivos:** `src/ControlDeFinanzasIA.UI/Views/MainWindow.axaml`, `.cs`

**Pasos:**
- `Window` con `x:Class="ControlDeFinanzasIA.UI.Views.MainWindow"`; `Title` desde localizador.
- `DockPanel`: `ListBox` (sidebar, `DockPanel.Dock="Left"`) con ítems localizados; `ContentControl` centrado con `Content="{Binding Current}"`; placeholder de statusbar (`DockPanel.Dock="Bottom"`).
- Code-behind vacío: constructor llama `InitializeComponent()`.
- Ejecutar `dotnet build`.

**Verificación:** Build exit 0; layout `DockPanel`; code-behind vacío; strings no hardcodeados.

**Dependencias:** T-007
**Estimación:** M (1–2 h)
**Trazabilidad:** AC-9

---

## T-010 — Crear `DashboardView.axaml`

**Archivos:** `src/ControlDeFinanzasIA.UI/Views/DashboardView.axaml`, `.cs`

**Pasos:** `UserControl` con `x:Class="ControlDeFinanzasIA.UI.Views.DashboardView"`; `x:DataType="vm:DashboardViewModel"`; `TextBlock` con `{Binding Title}`. Code-behind vacío. Ejecutar `dotnet build`.

**Verificación:** Build exit 0; `x:DataType` correcto; code-behind vacío.

**Dependencias:** T-003, T-008
**Estimación:** XS (< 1 h)
**Trazabilidad:** AC-10, AC-13

---

## T-011 — Modificar `App.axaml` para DataTemplates

**Archivos:** `src/ControlDeFinanzasIA.UI/App.axaml`

**Pasos:** Añadir namespaces `vm` y `v`. En `Application.DataTemplates`, añadir `DataTemplate` para `DashboardViewModel → DashboardView` con `x:DataType="vm:DashboardViewModel"`. **No** registrar `ShellViewModel → MainWindow` (la ventana raíz se asigna en `App.axaml.cs`). Ejecutar `dotnet build`.

**Verificación:** Build exit 0; `DataTemplate` para `DashboardViewModel` con `x:DataType`; no DataTemplate para `ShellViewModel`.

**Dependencias:** T-007, T-008, T-010
**Estimación:** S (< 1 h)
**Trazabilidad:** AC-10

---

## T-012 — Modificar `App.axaml.cs` para arranque con `ShellViewModel`

**Archivos:** `src/ControlDeFinanzasIA.UI/App.axaml.cs`

**Pasos:**
- Eliminar `CreateMainWindow()` inline de F-008.
- En `OnFrameworkInitializationCompleted`: resolver `ShellViewModel`; navegar a `RouteNames.Dashboard`; crear `new MainWindow { DataContext = shellViewModel }`; asignar `desktop.MainWindow` y llamar `Show()`.
- Opcional: setear `CultureInfo.DefaultThreadCurrentUICulture = new("es-DO")`.
- Ejecutar `dotnet build`.

**Verificación:** Build exit 0; no existe ventana inline; `new MainWindow()` con `DataContext = shellViewModel`; navega a `Dashboard`.

**Dependencias:** T-005, T-007, T-009
**Estimación:** S (< 1 h)
**Trazabilidad:** AC-11

---

## T-013 — Registrar servicios y `AddLocalization()` en `Bootstrap`

**Archivos:** `src/ControlDeFinanzasIA.UI/Composition/Bootstrap.cs`

**Pasos:** En `ConfigureServices`, añadir `services.AddSingleton<ShellViewModel>()`, `services.AddSingleton<INavigationService, NavigationService>()`, `services.AddTransient<DashboardViewModel>()`, `services.AddLocalization()`. Añadir `using` de `Navigation` y `ViewModels`. No modificar registros existentes. Ejecutar `dotnet build`.

**Verificación:** Build exit 0; 4 registros presentes; `AddLocalization()`; registros existentes intactos.

**Dependencias:** T-005, T-007, T-008
**Estimación:** XS (< 30 min)
**Trazabilidad:** AC-12

---

## T-014 — Crear `FakeNavigationService`

**Archivos:** `tests/ControlDeFinanzasIA.Tests.Unit/TestDoubles/FakeNavigationService.cs`

**Pasos:** Implementar `INavigationService` con propiedades configurables (`CurrentViewModel`, `CanGoBack`), registros de llamadas (`LastNavigateRoute`, `NavigateToCallCount`, `GoBackCalled`) y `event EventHandler? CurrentChanged` disparable. Ejecutar `dotnet build` tests.

**Verificación:** Build tests exit 0; implementa `INavigationService`; registra llamadas; `CurrentChanged` disparable.

**Dependencias:** —
**Estimación:** S (< 1 h)
**Trazabilidad:** AC-15, §9.3

---

## T-015 — Crear `NavigationServiceTests`

**Archivos:** `tests/ControlDeFinanzasIA.Tests.Unit/UI/NavigationServiceTests.cs`

**Pasos:**
- Crear fake `IServiceProvider` hand-rolled con `Dictionary<Type, object>`.
- Implementar ≥ 4 tests obligatorios (§9.1 N1..N4):
  - `NavigateTo_Dashboard_SetsCurrentAndPushesHistory`
  - `NavigateTo_UnknownRoute_ThrowsInvalidOperationException`
  - `GoBack_WithoutHistory_IsSilentNoop`
  - `CurrentChanged_FiresOnEveryNavigation`
- Implementar recomendados N5/N6 si cabe.
- Usar solo `Assert.*`. Ejecutar `dotnet test --filter "FullyQualifiedName~NavigationServiceTests"`.

**Verificación:** Build exit 0; ≥ 4 tests passed; fake `IServiceProvider` hand-rolled; sin mocking ni FluentAssertions.

**Dependencias:** T-005, T-014
**Estimación:** M (2–3 h)
**Trazabilidad:** AC-15, §9.1

---

## T-016 — Crear `ShellViewModelTests`

**Archivos:** `tests/ControlDeFinanzasIA.Tests.Unit/UI/ShellViewModelTests.cs`

**Pasos:**
- Implementar ≥ 2 tests obligatorios (§9.2 S1, S2):
  - `NavigateToCommand_DelegatesToNavigationService`
  - `GoBackCommand_CanExecute_BoundToCanGoBack`
- Implementar S3 recomendado si cabe.
- Usar `FakeNavigationService` y fake hand-rolled de `IStringLocalizer<Strings>`.
- Usar solo `Assert.*`. Ejecutar `dotnet test --filter "FullyQualifiedName~ShellViewModelTests"`.

**Verificación:** Build exit 0; ≥ 2 tests passed; usa `FakeNavigationService`; sin mocking ni FluentAssertions.

**Dependencias:** T-007, T-014
**Estimación:** S (1–2 h)
**Trazabilidad:** AC-15, §9.2

---

## T-017 — Verificación final, formato y cierre

**Archivos:** `spec/phases/phase-03/features.md`

**Pasos:**
1. `dotnet build -c Release` y `dotnet build -c Debug`: 0 warnings, 0 errors.
2. `dotnet test -c Release --no-build`: ~210 passed.
3. `dotnet test --filter "FullyQualifiedName~ArchitectureTests"`: passed.
4. `dotnet format style --verify-no-changes`: exit 0.
5. Verificar convenciones: file-scoped namespace, `using` al tope, XML doc, `sealed`, `sealed partial` en VMs.
6. Confirmar AC-16: sin `ExpensesViewModel`/`ExpensesView`, sin use cases F-004/F-005, sin `Avalonia.Headless`, sin scope creep.
7. `dotnet run --project src/ControlDeFinanzasIA.UI`: smoke test manual.
8. Actualizar `spec/phases/phase-03/features.md`: F-006 a "✅ COMPLETADA". No renombrar spec a `.done.md` (eso es `sdd-archive`).

**Verificación:** Build exit 0; ~210 tests passed; NetArchTest R-1..R-5 verdes; format exit 0.

**Dependencias:** T-001..T-016
**Estimación:** S (1 h)
**Trazabilidad:** AC-17, AC-18, §13

---

## Orden de ejecución recomendado

**Paralelismo en la base:** T-001, T-002, T-004 y T-014 son independientes.

```text
T-001 ─┬──→ T-006 ───→ T-007 ───→ T-009 ─┐
       │                                  │
T-002 ─┴──→ T-003 ───→ T-008 ───→ T-010 ─┤
                                         ├──→ T-011
T-004 ───→ T-005 ────────────────────────┤   T-012
       │                                  │   T-013
       ├──→ T-015 ────────────────────────┤
       │                                  │
T-014 ─┴──→ T-016 ───────────────────────┘
                                          ↓
                                         T-017
```

---

## Tareas bloqueantes / habilitadoras

| Tarea | Habilita | Razón |
| --- | --- | --- |
| T-001 | T-006, T-007, T-008 | Source generators de `CommunityToolkit.Mvvm`. |
| T-002 | T-003 | Paquete `Microsoft.Extensions.Localization`. |
| T-003 | T-008, T-010 | Recursos `.resx` listos. |
| T-004 | T-005 | `RouteNames` define el contrato. |
| T-005 | T-007, T-012, T-013, T-015 | `NavigationService` implementa `INavigationService`. |
| T-006 | T-007, T-008 | `ViewModelBase` es base de VMs. |
| T-007 | T-009, T-011, T-012, T-016 | `ShellViewModel` es DataContext del shell. |
| T-008 | T-010, T-011, T-013 | `DashboardViewModel` es ruta inicial. |
| T-009 | T-012 | `MainWindow` se instancia en `App.axaml.cs`. |
| T-010 | T-011 | `DashboardView` se referencia en `DataTemplate`. |
| T-014 | T-015, T-016 | `FakeNavigationService` usado en tests. |
| T-015, T-016 | T-017 | Tests deben pasar antes del cierre. |

---

## Estimación total

| Tarea | Dificultad | Horas |
| --- | --- | --- |
| T-001 | XS | 0.25 |
| T-002 | XS | 0.25 |
| T-003 | S | 0.5 |
| T-004 | XS | 0.25 |
| T-005 | M | 1–2 |
| T-006 | XS | 0.25 |
| T-007 | M | 1–2 |
| T-008 | XS | 0.25 |
| T-009 | M | 1–2 |
| T-010 | XS | 0.5 |
| T-011 | S | 0.5 |
| T-012 | S | 0.5 |
| T-013 | XS | 0.25 |
| T-014 | S | 0.5 |
| T-015 | M | 2–3 |
| T-016 | S | 1–2 |
| T-017 | S | 1 |
| **Total** | | **~10–14 h** |

---

## Riesgos y mitigaciones

| Riesgo | Prob. | Impacto | Mitigación |
| --- | --- | --- | --- |
| `DataTemplate` no resuelve con CompiledBindings → content vacío. | Media | Alto | `x:DataType` correcto; smoke test manual documenta el síntoma. |
| `.resx`/designer no se incrusta como `EmbeddedResource` → `IStringLocalizer` falla. | Media | Alto | Verificar `<EmbeddedResource>` y `Strings.Designer.cs`. |
| `CommunityToolkit.Mvvm` source generators no corren sin `partial`. | Baja | Alto | VMs con atributos son `sealed partial class`. |
| `IStringLocalizer<Strings>` no resuelve por tipo marker mal configurado. | Media | Medio | `.resx` llamado `Strings.resx`; designer genera `class Strings`. |
| `GoBackCommand` no se re-evalúa al cambiar `CanGoBack`. | Media | Medio | Llamar `NotifyCanExecuteChanged()` en `CurrentChanged`. |
| Dependencia circular DI si `NavigationService` recibe `ShellViewModel`. | Baja | Alto | DP-6: `NavigationService` recibe sólo `IServiceProvider`. |
| Avalonia Designer no renderiza `MainWindow.axaml`. | Baja | Bajo | Verificar `x:Class`; no es bloqueante. |
| Scope creep: implementar `ExpensesViewModel` aquí. | Baja | Medio | AC-16 lo prohíbe; `RouteNames.Expenses` se declara pero su VM es F-007. |

---

## Notas para apply

- **No tocar `Domain`, `Application` ni `Infrastructure`.** Sólo `Bootstrap`.
- **`CommunityToolkit.Mvvm`:** VMs con atributos son `sealed partial class`.
- **`[RelayCommand(CanExecute = nameof(CanGoBack))]`:** requiere método
  `bool CanGoBack()`. Refrescar con `NotifyCanExecuteChanged()` en `CurrentChanged`.
- **`IStringLocalizer<Strings>`:** inyectar en VMs; setear cultura default `es-DO`.
- **`.resx` como `EmbeddedResource`:** verificar en `.csproj`.
- **`MainWindow.axaml` reemplaza ventana inline:** eliminar `CreateMainWindow()`
  de `App.axaml.cs`; `new MainWindow { DataContext = shellViewModel }`.
- **DataTemplates en `App.axaml`:** sólo `DashboardViewModel → DashboardView`.
  `ShellViewModel` no va como DataTemplate.
- **Code-behind vacío:** sólo `InitializeComponent()`.
- **Tests pure xUnit:** `Assert.*`; fakes hand-rolled. Sin FluentAssertions,
  sin mocking library, sin `Avalonia.Headless`.
- **NetArchTest R-4:** Views/VMs no referencian `Infrastructure`.
- **CPM:** versiones sólo en `Directory.Packages.props`.

---

## Definition of Done (DoD)

- [x] AC-1: `RouteNames` con `Dashboard` y `Expenses`. (T-004)
- [x] AC-2: `ViewModelBase` abstract : `ObservableObject`. (T-006)
- [x] AC-3: `INavigationService` con 5 miembros. (T-005)
- [x] AC-4: `NavigationService` resuelve desde `IServiceProvider`. (T-005)
- [x] AC-5: Ruta desconocida lanza `InvalidOperationException`. (T-005)
- [x] AC-6: `GoBack` silent no-op; `CanGoBack` correcto. (T-005)
- [x] AC-7: `CurrentChanged` dispara en cada navegación. (T-005)
- [x] AC-8: `ShellViewModel` con `[ObservableProperty]`, `[RelayCommand]`, `CanExecute`. (T-007)
- [x] AC-9: `MainWindow.axaml` con `DockPanel` y code-behind vacío. (T-009)
- [x] AC-10: `DataTemplate` para `DashboardViewModel`. (T-011)
- [x] AC-11: `App.axaml.cs` arranca con `ShellViewModel` y navega a `Dashboard`. (T-012)
- [x] AC-12: `Bootstrap` registra servicios + `AddLocalization()`. (T-013)
- [x] AC-13: `.resx` es-DO + en; `IStringLocalizer` en VMs; cultura default es-DO. (T-003, T-007, T-008)
- [x] AC-14: Paquetes en CPM; `.csproj` sin `Version`. (T-001, T-002)
- [x] AC-15: ≥ 4 `NavigationServiceTests` + ≥ 2 `ShellViewModelTests`; fakes hand-rolled. (T-014, T-015, T-016)
- [x] AC-16: Sin scope creep. (T-017)
- [x] AC-17: NetArchTest R-1..R-5 verdes. (T-017)
- [x] AC-18: Build limpio, ~210 tests verdes, format exit 0, smoke manual. (T-017)

---

## Referencias

- `spec/features/phase-03/F-006-ui-shell-navigation.spec.md`
- `spec/phases/phase-03/features.md`
- `spec/features/phase-01/F-008-composition-root.done.md`
- `spec/constitution/tech-stack.md`
- `.claude/skills/frontend-design/SKILL.md`
- `tasks/phase-03/F-005-get-transactions-query.tasks.md`
