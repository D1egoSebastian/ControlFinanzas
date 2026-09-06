# Specification — F-008: UI capstone — `IncomeView` + filtros y vistas guardadas en `ExpensesView` + `GetIncomesQuery` + `DeleteSavedViewUseCase` + `CreateTagUseCase` + design pass BOLD "Libro Mayor"

> **Fase:** 4 — Transacciones: ingresos, edición, borrado, tags, filtros, vistas guardadas
> **Feature:** F-008 (local a esta fase; `4-008` en el phase plan — **última feature de Phase 4**)
> **Spec ID:** SPEC-F008P4
> **change_id:** `f-008p4-ui-capstone`
> **Modo:** Standard (SDD: propose → spec → tasks → apply → verify → archive)
> **Estado:** Pendiente de implementación (renombrar a `.done.md` tras apply + verify)
>
> **Nota de numeración:** este `F-008` es el de la **Fase 4**
> (`spec/phases/phase-04/features.md`, §4.1 líneas 52-54, §RF-16 a
> §RF-20 líneas 222-253, §6 CU-1..CU-7, §9 tabla `4-008`, §12 criterios
> de cierre de fase). Los IDs son locales a cada fase; el folder los
> desambigua. Ver `spec/README.md`.
>
> Fuentes de verdad: `docs/vision.md` (§4.1-§4.3 transacciones, filtros
> y vistas guardadas, §6 localización), `docs/architecture.md` (§4
> R-1..R-5, §7 use cases, §9 UI, §10 navegación), `docs/roadmap.md`
> (§Fase 4), `spec/constitution/tech-stack.md`,
> `spec/constitution/mission.md`,
> `spec/phases/phase-04/features.md` (§RF-16..RF-20, §6 CU-1..CU-7,
> §12 cierre de fase, §13 out of scope),
> `.atl/sdd/f-008p4-ui-capstone/proposal.md` (PRD de origen — 278
> líneas; Q-1..Q-5 ratificadas con defaults asumidos + ronda
> interactiva — supersesiones documentadas en §3.1),
> `spec/features/phase-04/F-006-saved-view-filter-json.done.md`
> (patrón espejo de estructura + maquinaria de vistas guardadas que
> esta feature consume; su D-8 difirió `DeleteSavedViewUseCase` aquí),
> `spec/features/phase-04/F-005-update-delete-use-cases.done.md`
> (patrón use cases + `DeleteTransactionUseCase` espejo + D-ML idioma
> de mensajes),
> `spec/features/phase-04/F-007-extend-filters-tagids.done.md`
> (filtros 6 props con `TagIds` en ambos repos),
> skill `frontend-design` (dirección estética BOLD — §4).

---

## 1. Metadata

| Campo | Valor |
| --- | --- |
| **change_id** | `f-008p4-ui-capstone` |
| **spec_id** | `SPEC-F008P4` |
| **modo** | Standard (SDD: propose → spec → tasks → apply → verify → archive) |
| **fase** | 4 |
| **feature** | F-008 (local a Fase 4; `4-008` en el plan — la más grande de la fase) |
| **dependencies (build)** | 4-001..4-007 **todas archivadas** ✅: entidad `Income` + `IncomeRepository` (6 métodos + `GetByFilterAsync` con filtros 6 props), `RegisterIncome`/`UpdateIncome`/`DeleteTransaction` (+ validators), `Tag` + M:N ambos lados, `SavedView` + `FilterJsonSerializer` + 3 use cases (`SaveCurrentFilter`/`ApplySavedView`/`GetSavedViews`) + `SavedViewRepository` con `RemoveAsync` (el D-8 de F-006 lo dejó listo para este feature) |
| **dependencies (contrato)** | Phase 1 F-005 (`Result` non-generic, `Value: object?` — no se modifica) ✅; `PagedResult<T>` con `TotalPages` ✅; `IClock` ✅; `ExpenseFilter`/`IncomeFilter` 6 props ✅ |
| **dependencies (test infra)** | `FakeExpenseRepository` (con `SetFilterResult`/`GetByFilterCallCount`/`SetThrowOnGetFilter`), `FakeIncomeRepository` (**sin** configuración de filter — se extiende, AC-6), `FakeCategoryRepository`, `FakeSavedViewRepository` (F-006, con `RemoveResultOverride`), `FakeClock`, `FakeStringLocalizer`, `NullLogger<>` ✅. **Nuevo**: `FakeTagRepository` (AC-6 — no existe, verificado por glob). **Sin integration tests nuevos** (F-008 no toca Infrastructure) |
| **dependencies (paquetes)** | **Ninguna nueva** — las fuentes tipográficas del design pass son **assets OFL embebidos** vía `AvaloniaResource` (no paquetes NuGet); CPM intacto. La **única** edición sancionada de `.csproj` es el `ItemGroup` de fuentes (AC-23) |
| **unblocks** | Cierre de Phase 4 (criterios §12 del plan) → Phase 5 (multi-moneda) |
| **artifact_store** | Engram (objetivo: `sdd/f-008p4-ui-capstone/spec` + `sdd/f-008p4-ui-capstone/proposal` — backfill por el orquestador; ver R-8) + filesystem (este archivo) |
| **status** | Pendiente de implementación |
| **línea base de tests (post-4-007)** | Suite verde + 1 fallo **pre-existing conocido de `Money`** (`ExpenseSchemaTests.Money_OwnsOne_ProducesTwoColumns`, documentado en verify-report F-007 — no regresión). El implementador reporta las cuentas exactas observadas |
| **Q-1..Q-5 ratificadas (ronda interactiva + defaults del proposal)** | Q-1 vistas guardadas en **ambas** vistas; Q-2 botones Editar/Eliminar **espejados** en ambas; Q-3 design pass **solo las 2 vistas de transacciones** + tema **Dark único**; Q-4 paginación **conservada**; Q-5 creación de tags **inline** — ver D-6/D-7/D-12/D-11/D-5 |

---

## 2. Contexto y objetivo

Phase 4 cerró todo el eje de datos: CRUD completo de gastos e ingresos,
tags M:N, filtros de 6 props en ambos repos y vistas guardadas con JSON
versionado. Pero el usuario no ve nada de eso: la UI hoy solo expone
**registrar gasto + listado paginado**. No existe `IncomeView`, no hay
ruta "Ingresos", `ExpensesView` no tiene filtros ni vistas guardadas ni
botones de editar/borrar, y los orquestadores de F-006
(`SaveCurrentFilter`, `ApplySavedView`, `GetSavedViews`) y los CRUD de
F-005 (`UpdateExpense`/`UpdateIncome`/`DeleteTransaction`) están
registrados en DI **sin consumidor** — código muerto hasta que exista
UI. F-008 es el capstone que cablea todo y cierra Phase 4 por el lado
del usuario.

Esto requiere cinco frentes: (a) **Application** — `IncomeDto` +
`IncomeExtensions`, `GetIncomesQuery` (+ `GetIncomesQueryRequest` con
filtros desde el día 1, RF-17), la extensión de
`GetTransactionsQueryRequest`/`GetTransactionsQuery` con los filtros
opcionales (sin ella, "aplicar filtro" en Gastos es imposible: el query
hoy hardcodea `CategoryId/DateFrom/DateTo = null` e ignora `TagIds`),
`DeleteSavedViewUseCase` (paga la deuda D-8 de F-006) y
`CreateTagUseCase` (desbloquea el filtro por tags para un usuario real
— hoy no hay UI para crear tags y el picker estaría vacío); (b)
**`IncomeViewModel` + `IncomeView.axaml`** con wiring completo de
navegación (ruta "Ingresos" en el sidebar); (c) la **extensión mayor de
`ExpensesViewModel`/`ExpensesView.axaml`**: riel de filtros (Category,
Tags multi-select, rango de fechas), combobox de vistas guardadas con
Save/Apply/Delete, y botones de Editar/Eliminar por fila; y (d) un
**pase de diseño BOLD** (skill `frontend-design`) que da al capstone
una identidad visual deliberada — el estado actual es FluentTheme
default, estética genérica de dashboard-SaaS.

**Correcciones al contrato de lanzamiento (verificadas en fuente):**

1. `IncomeDto`/`IncomeExtensions` viven en **`Application/DTOs/`**
   (espejo exacto de `ExpenseDto.cs`/`ExpenseExtensions.cs`, verificado
   por glob) — NO en `UseCases/Incomes/GetIncomes/` como listaba el
   contrato. El árbol `Application/DTOs/` ya contiene los 4 DTOs del
   proyecto; crear un quinto en otra carpeta rompería la convención.
2. `GetIncomesQuery` es una **clase** con
   `ExecuteAsync(GetIncomesQueryRequest) → Task<Result>` — espejo
   línea a línea de `GetTransactionsQuery` (87 líneas: 2 deps
   `IExpenseRepository` + `ILogger<>`, validación manual, try/catch).
   **NO** existe un `GetIncomesUseCase` separado ni records wrapper
   `GetIncomesRequest(GetIncomesQueryRequest)`: el contrato bosquejaba
   esa estructura, pero el patrón verificado del proyecto (RF-17 +
   proposal D-1 + `GetTransactionsQueryTests` existente) es
   query-clase + request-record. El test se llama `GetIncomesQueryTests`
   (no `GetIncomesUseCaseTests`).
3. `IncomeDto` **NO** lleva colección `Tags`: el sketch del contrato
   incluía `IReadOnlyList<string> Tags`, pero
   `IncomeRepository.GetByFilterAsync` materializa con `AsNoTracking`
   **sin `Include`** de la navegación `Tags` (verificado, líneas
   186-224) — una prop `Tags` en el DTO llegaría **siempre vacía** en
   producción (dato engañoso = fallo silencioso disfrazado). Espejo de
   `ExpenseDto` (9 props, sin Tags) + `Guid? CategoryId` (Income es
   nullable, verificado) + `Source`. Mostrar tags por fila queda fuera
   de alcance (§8).
4. Los tests de ViewModels viven en **`Tests.Unit/UI/`**
   (`ExpensesViewModelTests.cs`, `ShellViewModelTests.cs`,
   `NavigationServiceTests.cs` — verificado) — NO en
   `Tests.Unit/ViewModels/`. `IncomeViewModelTests.cs` va en
   `Tests.Unit/UI/`.
5. **Fuentes**: Avalonia 11 soporta **`.ttf` y `.otf`** embebidos vía
   `AvaloniaResource` (docs oficiales verificadas vía Context7) — el
   `.woff2` del contrato NO es formato soportado. Se usan TTFs
   variables OFL en `Assets/Fonts/` (proposal §7) y la edición del
   `.csproj` queda **sancionada** como ItemGroup de assets (única
   edición de csproj del feature; CPM intacto).
6. **Localización**: NO existe `Strings.en.resx` — la convención
   verificada es **3 archivos**: `Strings.resx` (neutral = valores en
   inglés) + `Strings.es-DO.resx` + `Strings.Designer.cs`
   (tool-generated, commiteado). Toda key nueva va en los 3 (D-10).
7. `CreateTagUseCase` necesita **4 deps** (`ITagRepository`, `IClock`,
   `IValidator<CreateTagCommand>`, `ILogger<>`) — el sketch "2 deps" es
   imposible: `Tag.Create(name, color, createdAt)` exige `createdAt`
   non-default (verificado en `Tag.cs` línea 77) → `IClock` obligatorio
   (precedente `RegisterIncomeUseCase`).
8. **Test infra no listada por el contrato**: `FakeIncomeRepository`
   no tiene `SetFilterResult`/`GetByFilterCallCount` (devuelve vacío
   hardcoded — verificado) → **se extiende** (AC-6); **NO existe
   `FakeTagRepository`** → **se crea** (AC-6); `FakeExpenseRepository`
   gana captura `LastExpenseFilter` para blindar el mapeo de filtros;
   `GetTransactionsQueryTests` se extiende con tests de regresión del
   mapeo nuevo.

**Estado actual verificado (lectura de fuente, no suposiciones):**

| Elemento | Estado real (verificado) |
| --- | --- |
| `UI/Views/` | Solo `MainWindow`, `ExpensesView` (112 líneas), `DashboardView`. **NO existe** `IncomeView.axaml` |
| `UI/ViewModels/` | `ViewModelBase` (abstract : `ObservableObject`), `ShellViewModel` (75 líneas), `ExpensesViewModel` (277 líneas), `DashboardViewModel`. **NO existe** `IncomeViewModel` |
| `RouteNames.cs` | Solo `Dashboard` + `Expenses` (21 líneas). **FALTA** `Incomes` |
| `NavigationService.cs` | Diccionario de rutas en el ctor (líneas 28-32): `{ RouteNames.X, typeof(XViewModel) }` — añadir `Incomes` es 1 entrada. `NavigateTo` resuelve vía `GetRequiredService` |
| Sidebar | **Dinámico**: `ShellViewModel.MenuItems` (`IReadOnlyList<RouteMenuItem>`, collection-initializer en ctor líneas 33-37 con `RouteMenuItem(RouteNames.X, _localizer["SidebarX"])`) renderizado por `MainWindow.axaml` vía `ListBox`. El item "Ingresos" se agrega en **ShellViewModel**, NO en MainWindow |
| `App.axaml` | 23 líneas. `Application.DataTemplates` con patrón dual `DataType="{x:Type vm:X}"` + `x:DataType="vm:X"` (compiled bindings); `RequestedThemeVariant="Default"`; solo `<FluentTheme />` en Styles |
| `ExpensesViewModel` | sealed partial, 6 deps (`RegisterExpenseUseCase`, `GetTransactionsQuery`, `ICategoryRepository`, `IClock`, `ILogger`, `IStringLocalizer<Strings>`); form props + `ObservableCollection<ExpenseDto>` + `Categories` (`IReadOnlyList<CategoryDto>`) + paginación (`Page`/`TotalPages`, PageSize **20** hardcoded en `RefreshAsync`) + `ValidationErrors`/`FormError` + `CanGoToPreviousPage`/`CanGoToNextPage`. Ctor: guards + `_date = _clock.Today` + fire-and-forget `LoadCategoriesAsync`/`RefreshAsync`. **SIN** filtros, **SIN** Update/Delete (use cases F-005 sin consumidor UI), **SIN** vistas guardadas, **SIN** tags |
| `ExpensesView.axaml` | Labels vía `x:Static res:Strings.*` (localizado); **watermarks y "Página X de Y" hardcodeados en español** (deuda i18n: "Seleccione...", "Descripción del gasto", "Nota opcional", líneas 101-106 — la key `PageOfPagesFormat` YA EXISTE en el resx pero no se usa). FluentTheme default, cero identidad visual |
| `GetTransactionsQueryRequest` | Record posicional `(int Page = 1, int PageSize = 50)` — **SOLO paginación** (10 líneas); su doc-comment anticipa: "los filtros… llegarán en Phase 4" |
| `GetTransactionsQuery` | 87 líneas. Validación manual `Page`/`PageSize` (códigos `InvalidFilter`, mensajes español); filtro construido con `CategoryId = null, DateFrom = null, DateTo = null` **hardcodeados** (líneas 62-69, ignora `TagIds`); try/catch → `RepositoryError`; `Value = PagedResult<ExpenseDto>` |
| `Application/DTOs/` | `ExpenseDto` (9 props record) + `ExpenseExtensions.ToDto()` + `CategoryDto`/`CategoryExtensions`. **NO existe** `IncomeDto` ni `IncomeExtensions` |
| `UseCases/SavedViews/` | `SaveCurrentFilter` (Command/Validator/UseCase — 4 deps, dispatch por runtime type + pairing), `ApplySavedView` (Query/Result/UseCase — `ApplySavedViewResult(EntityType, Filter)`), `GetSavedViews` (Query/UseCase — 1 dep) ✅ registrados en Bootstrap (líneas 83-86). **FALTA** `DeleteSavedView` (D-8 de F-006 lo difirió aquí; `SavedViewRepository.RemoveAsync` ya existe) |
| `UseCases/Incomes/` | `RegisterIncome` (Command sin `Source`, Validator, UseCase, Result), `UpdateIncome` (Command `Id` + 6 campos — el use case resetea `Source` a null, comportamiento F-005 heredado). **FALTA** `GetIncomes` |
| `Bootstrap.cs` | 143 líneas. Registra `ExpensesViewModel` (Transient) + 5 use cases CRUD + `GetTransactionsQuery` + 5 validators + 3 use cases de vistas + 1 validator de vistas. **FALTAN**: `GetIncomesQuery`, `DeleteSavedViewUseCase`, `CreateTagUseCase` (+ validator), `IncomeViewModel` |
| `Income.cs` (Domain) | 319 líneas: `Id`, `CategoryId` (**nullable**), `Date`, `Amount` (Money), `Description`, `Note?`, `Source?` (max 50), `CreatedAt`, `UpdatedAt`, `Tags` (ICollection). `Create`/`Update` con invariantes; `AddTag`/`RemoveTag` |
| `IncomeFilter`/`ExpenseFilter` | Records 6 props `init` (Page, PageSize, CategoryId, DateFrom, DateTo, TagIds). Sus doc-comments designan a **4-008** como responsable de validar coherencia de fechas en ambos queries |
| `Strings.resx` ×3 | 22 keys existentes (SidebarDashboard, SidebarExpenses, labels de form, PreviousPageButton/NextPageButton/**PageOfPagesFormat** sin uso, FormErrorMessage, etc.). **FALTAN** ~28 keys nuevas |
| Tests de VMs | `Tests.Unit/UI/` (`ExpensesViewModelTests` 277 líneas — patrón: fakes + use cases **reales** + `NullLogger<>` + `FakeStringLocalizer` que retorna el key; asserts tipo `Assert.Equal("FormErrorMessage", viewModel.FormError)`) |
| Fakes | `FakeExpenseRepository` (141 líneas — `SetFilterResult` ×2 overloads, `GetByFilterCallCount`, `SetThrowOnGetFilter`, overrides Add/Update/Remove/GetById), `FakeIncomeRepository` (97 líneas — **sin** filter setup), `FakeCategoryRepository` (`SetCategories`), `FakeSavedViewRepository` (99 líneas — `RemoveResultOverride`, `GetByEntityTypeAsync` filtra la lista capturada). **NO existe** `FakeTagRepository` |
| `IClock` | `UtcNow` + `Today`; `SystemClock` singleton en Bootstrap línea 63; `FakeClock` seteable |
| Avalonia | `AvaloniaUseCompiledBindingsByDefault=true` (csproj línea 7, verificado) — todo DataTemplate/binding necesita `x:DataType` correcto. `Avalonia.Headless` ya referenciado. Avalonia 11.2.7 vía CPM |

**Objetivos concretos:**

1. **Application (PR-1)**: `IncomeDto` + `IncomeExtensions` (DTOs/);
   `GetIncomesQueryRequest` + `GetIncomesQuery` (filtros día 1, RF-17);
   extensión de `GetTransactionsQueryRequest` (+5 params opcionales) +
   mapeo en `GetTransactionsQuery` (D-2); `DeleteSavedViewCommand` +
   `DeleteSavedViewUseCase` (D-4 — paga D-8 de F-006);
   `CreateTagCommand` + `CreateTagValidator` + `CreateTagUseCase`
   (D-5); DI Bootstrap (5 registros).
2. **Test infra (PR-1)**: `FakeTagRepository` (nuevo) + extensión de
   `FakeIncomeRepository`/`FakeExpenseRepository` + tests unit de los
   3 orquestadores nuevos + regresión de `GetTransactionsQuery`.
3. **IncomeView (PR-2)**: `IncomeViewModel` (MVVM estricto, CRUD
   completo + filtros + vistas guardadas) + wiring de navegación
   (`RouteNames.Incomes`, `NavigationService`, `ShellViewModel`,
   `App.axaml` + `SidebarIncomes` ×3 resx) + `IncomeView.axaml`
   funcional + `IncomeViewModelTests` (≥6).
4. **ExpensesView (PR-3)**: extensión del VM (filtros + vistas
   guardadas + StatusMessage + Edit/Delete + CreateTag) + extensión
   del XAML (riel de filtros + combobox + botones de fila) + tests de
   los comandos nuevos (≥4).
5. **Design pass BOLD (PR-4)**: fuentes OFL `.ttf` embebidas +
   `Tokens.axaml` + `LedgerStyles.axaml` + `Motion.axaml` + aplicación
   a las 2 vistas + `RequestedThemeVariant="Dark"` + migración de
   strings hardcodeados restantes (D-12).

---

## 3. Decisiones (locked-in — no re-abrir)

Las decisiones D-1..D-12 fueron bloqueadas en el proposal + ronda
interactiva + contrato de lanzamiento. Donde la verificación de fuente
**supera** al contrato, se documenta explícitamente en §3.1 (misma
práctica que F-006 §3.1).

### D-1 — `GetIncomesQueryRequest` plano con filtros día 1 + `GetIncomesQuery` espejo de `GetTransactionsQuery`

**Decisión:** `GetIncomesQueryRequest` es un record posicional con
defaults — `(int Page = 1, int PageSize = 50, Guid? CategoryId = null,
DateOnly? DateFrom = null, DateOnly? DateTo = null,
IReadOnlyList<Guid>? TagIds = null)` — mapeado 1:1 a `IncomeFilter`.
`GetIncomesQuery` es `public sealed class` con **2 deps**
(`IIncomeRepository`, `ILogger<GetIncomesQuery>`) y
`ExecuteAsync(GetIncomesQueryRequest, ct) → Task<Result>` con
`Value = PagedResult<IncomeDto>` (RF-17). Validación manual (Page ≥ 1,
1 ≤ PageSize ≤ 200, `DateFrom ≤ DateTo` cuando ambos set, `TagIds` sin
`Guid.Empty`) → códigos `"InvalidFilter"`; try/catch →
`"RepositoryError"` (mensajes español, espejo). **NO** hay
`GetIncomesUseCase` ni records wrapper (corrección #2 de §2).

**Justificación:** espejo del patrón verificado — RF-17 del plan lo
dice literal ("Paralelo a `GetTransactionsQuery`"); los doc-comments
de `IncomeFilter` designan a 4-008 como validador de la coherencia de
fechas; filtros día 1 evitan re-tocar el query en el futuro.

### D-2 — `GetTransactionsQueryRequest` gana los mismos 5 params opcionales; el query deja de hardcodear nulls

**Decisión:** `GetTransactionsQueryRequest` gana
`Guid? CategoryId = null, DateOnly? DateFrom = null, DateOnly?
DateTo = null, IReadOnlyList<Guid>? TagIds = null` (defaults `null` →
callers y tests existentes compilan sin cambio). `GetTransactionsQuery`
reemplaza los nulls hardcodeados por el mapeo del request (incl.
`TagIds`) y añade los guards de coherencia (`DateFrom > DateTo` →
`InvalidFilter`; `TagIds` con `Guid.Empty` → `InvalidFilter`). El orden
del repo (`Date desc, CreatedAt desc`) ya lo garantiza
`GetByFilterAsync` — el VM no reordena.

**Justificación:** sin esto, AC-12 ("aplicar filtro refresca la
lista") es imposible — hoy el query ignora todo filtro. El
doc-comment del request lo anticipaba ("los filtros… llegarán en
Phase 4"). Aditivo puro: defaults `null` = comportamiento actual.

### D-3 — `IncomeDto` + `IncomeExtensions` en `Application/DTOs/` (espejo de `ExpenseDto`, sin Tags)

**Decisión:** `public sealed record IncomeDto(Guid Id, Guid?
CategoryId, DateOnly Date, decimal Amount, Currency Currency, string
Description, string? Note, string? Source, DateTime CreatedAt,
DateTime UpdatedAt)` en `Application/DTOs/IncomeDto.cs`;
`IncomeExtensions.ToDto(this Income income)` en
`Application/DTOs/IncomeExtensions.cs` — espejo línea a línea de
`ExpenseDto`/`ExpenseExtensions` (inglés, XML doc por prop),
adaptando `CategoryId` nullable + `Source`. **Sin** colección `Tags`
(corrección #3 de §2).

**Justificación:** el repo materializa `AsNoTracking` sin `Include` de
`Tags` → una prop Tags llegaría siempre vacía en producción. Dato
muerto en un DTO = engaño silencioso; la constitución del repo es "no
silent failures". El filtro por tags funciona vía SQL (F-007) sin
necesidad de materializar la colección.

### D-4 — `DeleteSavedViewCommand` + `DeleteSavedViewUseCase` — espejo de `DeleteTransactionUseCase` (paga D-8 de F-006)

**Decisión:** `DeleteSavedViewCommand(Guid SavedViewId)` +
`DeleteSavedViewUseCase` (2 deps: `ISavedViewRepository`,
`ILogger<DeleteSavedViewUseCase>`) en
`UseCases/SavedViews/DeleteSavedView/`. Flow:
`ArgumentNullException.ThrowIfNull(command)` → guard `Guid.Empty` →
`Result.Failure("Validation", "Id cannot be empty.")` → `RemoveAsync`
(el repo resuelve `NotFound` con fetch interno) → **propagación
verbatim**. **Sin validator** (espejo `DeleteTransactionUseCase`
remarks). Doc-comments en inglés (espejo familia SavedViews).

**Justificación:** F-006 D-8 difirió este use case exactamente hasta
aquí ("junto al botón Eliminar vista, RF-19"). Un fetch previo en el
use case sería redundante: `SavedViewRepository.RemoveAsync` ya hace
`FindAsync` + `NotFound`.

### D-5 — `CreateTagCommand` + `CreateTagValidator` + `CreateTagUseCase` (affordance mínimo de Q-5, elevado a use case)

**Decisión:** en `UseCases/Catalog/CreateTag/` (aggregate `Catalog`,
donde vive `Tag`): `CreateTagCommand(string Name, string? Color)` +
`CreateTagValidator` (2 reglas sync: Name no vacío post-trim + max 50;
Color null o max 9 — mensajes español, espejo `RegisterIncomeValidator`)
+ `CreateTagUseCase` con **4 deps** (`ITagRepository`, `IClock`,
`IValidator<CreateTagCommand>`, `ILogger<CreateTagUseCase>`). Flow:
`ThrowIfNull` → `Validate` sync → failure `"Validation"` con details
por campo → `Tag.Create(name, color, _clock.UtcNow)` con catch
`ArgumentException` → `"DomainInvariant"` (defense in depth, F-005
D-4) → `AddAsync` → **propagación verbatim** (expone `"DuplicateName"`)
→ `Result.Success(tag)`.

**Justificación (Q-5 ratificada + elevada):** el proposal recomendaba
repo-direct en el VM (`TagRepository.AddAsync` sin use case). La ronda
interactiva la **elevó** a use case: el precedente de repo-direct en
VM es **solo para pickers de lectura** (`ICategoryRepository.GetAllAsync`);
una **escritura** desde el VM saltaría la capa de orquestación
(architecture §7) y duplicaría validación. Costo: 3 archivos pequeños
(~90 líneas) + 1 DI + tests — dentro del espíritu "~60-80 líneas" de
Q-5. Desbloquea el filtro por tags para usuarios reales (hoy el picker
estaría vacío).

### D-6 — `IncomeViewModel` sealed partial (MVVM estricto): espejo estructural de `ExpensesViewModel` + CRUD + filtros + vistas guardadas

**Decisión:** `public sealed partial class IncomeViewModel :
ViewModelBase` con **14 deps** (AC-7): `RegisterIncomeUseCase`,
`UpdateIncomeUseCase`, `DeleteTransactionUseCase`, `GetIncomesQuery`,
`SaveCurrentFilterUseCase`, `ApplySavedViewUseCase`,
`GetSavedViewsUseCase`, `DeleteSavedViewUseCase`, `CreateTagUseCase`,
`ICategoryRepository`, `ITagRepository`, `IClock`,
`ILogger<IncomeViewModel>`, `IStringLocalizer<Strings>`.
Propiedades espejo del form de `ExpensesViewModel` + `SelectedIncome`,
`IsEditMode`, `EditableId`, `PendingDeleteIncomeId`, `StatusMessage`,
props de filtro (`FilterCategoryId`, `FilterDateFrom`, `FilterDateTo`),
`AvailableTags` (`IReadOnlyList<SelectableTagItem>`), `CurrentFilter`,
props de vistas guardadas (`SavedViews`, `SelectedSavedView`,
`NewViewName`, `PendingDeleteViewId`), `TagCreationError`,
`PageSummary`. Comandos: `Refresh`, `RegisterIncome`, `UpdateIncome`,
`DeleteIncome`, `BeginEdit`, `CancelEdit`, `ApplyFilter`,
`ClearFilter`, `SaveView`, `ApplySavedView`, `DeleteSavedView`,
`CreateTag`, `PreviousPage`, `NextPage`. Ctor: guards `ThrowIfNull`
×14 + `_date = _clock.Today` + fire-and-forget
(`LoadCategoriesAsync`, `LoadTagsAsync`, `RefreshAsync`,
`LoadSavedViewsAsync` — patrón existente).

**Justificación (Q-1 + Q-2):** simetría total — el plan §12 exige
filtros en ambas vistas y la maquinaria F-006 es type-aware; CRUD
completo de ingresos en UI pero gastos sin editar/borrar dejaría el
capstone cojo. La cantidad de deps es la consecuencia directa de
espejar + Q-1 + Q-2 + D-5: se documenta explícitamente (AC-7) para
que `sdd-tasks` lo planifique sin sorpresas.

### D-7 — Interacción MVVM-estricta: confirmación de borrado en 2 pasos + `StatusMessage` transitorio + `NewViewName` inline

**Decisión:** **sin diálogos de plataforma, sin code-behind**. El
borrado (fila o vista guardada) usa **confirmación en 2 pasos**: el
botón "Eliminar" pasa a estado pendiente (`PendingDeleteIncomeId` /
`PendingDeleteViewId` = Id del objetivo) y su etiqueta cambia a
"Confirmar" (vía converter o clases de estilo — mecanismo en design,
comportamiento en spec); la siguiente acción del usuario ejecuta o
resetea el estado pendiente. El feedback de operaciones de lista va
por **`StatusMessage`** (línea de estado éxito/error que se limpia al
iniciar la siguiente acción). El nombre de vista nueva se captura vía
`TextBox` inline (`NewViewName`; botón Save habilitado si no vacío) —
NO `MessageBox`. Todo testeable sin UI.

**Justificación:** los diálogos de plataforma rompen MVVM estricto y
la testeabilidad; el patrón 2-pasos es 100% estado de VM. Resuelve el
risk "UX confusa" del proposal: el reset del estado pendiente queda
blidado por tests (§7).

### D-8 — Picker de tags: `ITagRepository.GetAllAsync()` → wrappers observables

**Decisión:** el VM expone `AvailableTags` como
`IReadOnlyList<SelectableTagItem>` — wrapper observable mínimo
(`Id`, `Name`, `Color?` inmutables + `IsSelected` observable, archivo
`UI/ViewModels/SelectableTagItem.cs`, ~30 líneas) construido desde
`ITagRepository.GetAllAsync()` (entidad `Tag` de Domain en VM —
permitido por R-4, precedente `Category`). El picker renderiza
checkboxes (o `ListBox` multi-select) bindados a `IsSelected`;
`TagIds` del filtro = ids con `IsSelected == true` (o `null` si
ninguno). Estado vacío visible si no hay tags (mensaje + affordance
de creación, D-5). Sin lógica en code-behind.

**Justificación:** la selección multi-tag necesita estado observable
**por item** para bindings compile-safe; sincronizar
`ListBox.SelectedItems` con el VM sin code-behind es frágil en
Avalonia (risk del proposal). Los wrappers son trivialmente
testeables.

### D-9 — Wiring de navegación = 4 ediciones puntuales

**Decisión:** (1) `RouteNames` + `public const string Incomes =
"Incomes";` con XML doc; (2) `NavigationService` + entrada
`{ RouteNames.Incomes, typeof(IncomeViewModel) }` en el diccionario
del ctor; (3) `ShellViewModel.MenuItems` + `new
RouteMenuItem(RouteNames.Incomes, _localizer["SidebarIncomes"])`
después de la entrada de Expenses; (4) `App.axaml` + DataTemplate
dual (`DataType="{x:Type vm:IncomeViewModel}"` +
`x:DataType="vm:IncomeViewModel"` → `<views:IncomeView />`).
`MainWindow.axaml` **no se toca** (sidebar dinámico; Q-3).

**Justificación:** verificado: el sidebar es data-driven desde
`ShellViewModel.MenuItems`; `MainWindow` solo lo renderiza. Blast
radius mínimo y reversible.

### D-10 — Localización: keys en los 3 archivos resx + migración de hardcodeados

**Decisión:** todo string nuevo vía `Strings.resx` (neutral, valores
inglés) + `Strings.es-DO.resx` + `Strings.Designer.cs` — key en los 3
(D-6 del proposal; convención verificada). Los strings hardcodeados
de las secciones **reescritas** ("Seleccione...", "Descripción del
gasto", "Nota opcional", "Página X de Y") migran a resources — la key
`PageOfPagesFormat` ya existe y pasa a usarse vía la prop VM
`PageSummary` (compuesta con `_localizer["PageOfPagesFormat"]`,
Page y TotalPages — testeable). Key mínima: `SidebarIncomes`. Lista
completa en AC-24 (~28 keys nuevas + reuso).

**Justificación:** AGENTS.md: "Use `IStringLocalizer`; do not
hardcode UI strings". La migración se limita a secciones reescritas
(scope discipline — el resto del debt i18n queda para otro feature).

### D-11 — Paginación conservada; `Page = 1` al aplicar filtro/vista

**Decisión:** los controles de paginación existentes (`Page`,
`TotalPages`, `PreviousPage`/`NextPage` con
`CanGoToPreviousPage`/`CanGoToNextPage`, PageSize 20) se conservan en
ambas vistas (Q-4). `ApplyFilter`, `ApplySavedView` y `ClearFilter`
resetean `Page = 1` (un filtro define una nueva primera página);
`Refresh` re-ejecuta el query con la `Page` corriente. `SaveView`
persiste el `CurrentFilter` **completo** incl. `Page`/`PageSize`
(F-006 D-7: la decisión de resetear `Page` es de la UI al **aplicar**,
no de la persistencia).

**Justificación:** paginación gratis con `PagedResult` (Q-4);
resolución explícita del hueco F-006 D-7/Q-2: persistir completo +
resetear al aplicar.

### D-12 — Design pass BOLD "Libro Mayor" (Q-3): FluentTheme de base + tokens + fuentes OFL + Dark único + SOLO las 2 vistas

**Decisión:** `FluentTheme` se conserva como **base de controles**
(inputs accesibles) + capa de tokens (`Styles/Tokens.axaml` como
`ResourceDictionary` con colores, fonts, spacing, durations) + estilos
de ledger (`Styles/LedgerStyles.axaml`) + motion
(`Styles/Motion.axaml`) — **incluidos a nivel de `UserControl.Styles`
en `IncomeView`/`ExpensesView`**, NO a nivel App (los estilos
implícitos no deben re-stylear shell/dashboard). `Tokens.axaml` sí va
en `Application.Resources` vía `ResourceInclude` (recursos inertes
hasta ser referenciados — el shell los podrá adoptar después).
`RequestedThemeVariant` fijo a **`"Dark"`** en `App.axaml` (tema
único; el shell adopta el Dark variant pero SIN restyling propio).
Fuentes OFL `.ttf` embebidas en `Assets/Fonts/` vía
`<AvaloniaResource Include="Assets\Fonts\**" />` + `<FontFamily>`
resources en `Tokens.axaml`, referenciadas
`avares://ControlDeFinanzasIA.UI/Assets/Fonts#Fraunces` (etc.).
Dirección estética completa en §4.

**Justificación (Q-3):** el mínimo viable de identidad son las 2
vistas de transacciones (donde vive el producto); re-stylear el shell
multiplica el riesgo de XAML por cero valor de capstone. Dark único
elimina la matriz de temas. FluentTheme como base de controles
mantiene la accesibilidad de inputs nativos (proposal D-10).

### 3.1 Tabla de supersesiones sobre el contrato de lanzamiento (verificación de fuente)

| Tema | Contrato de lanzamiento | Esta spec (locked) |
| --- | --- | --- |
| Ubicación de `IncomeDto`/`IncomeExtensions` | `UseCases/Incomes/GetIncomes/` | **`Application/DTOs/`** — espejo de `ExpenseDto` (convención verificada del árbol) |
| Shape de `IncomeDto` | 10 props incl. `IReadOnlyList<string> Tags`, `string Currency`, sin `UpdatedAt` | **10 props sin Tags**, `Currency Currency` (enum, espejo), con `UpdatedAt` (necesario para el flujo de edición) — corrección #3 |
| Estructura del query | `GetIncomesRequest(Filter)` + `GetIncomesQuery(Request)` records + `GetIncomesUseCase` (2 deps: repo + `IClock`) | **`GetIncomesQueryRequest` plano (6 params)** + **clase `GetIncomesQuery`** (2 deps: repo + `ILogger<>`, sin `IClock`) — espejo `GetTransactionsQuery` (RF-17, proposal D-1) |
| Nombre/carpetas de tests del query | `GetIncomesUseCaseTests` en `Application/UseCases/Incomes/GetIncomes/` | **`GetIncomesQueryTests`** en `Tests.Unit/Application/Incomes/GetIncomes/` (convención F-005 C-3: sin segmento `UseCases`) |
| Carpeta de tests de VMs | `Tests.Unit/ViewModels/` | **`Tests.Unit/UI/`** (árbol verificado) |
| Formato de fuentes | `.woff2` en `Resources/Fonts/` | **`.ttf` OFL** en **`Assets/Fonts/`** — Avalonia solo soporta ttf/otf (docs verificadas); ItemGroup `AvaloniaResource` sancionado |
| Resx inglés | `Strings.en.resx` | **NO existe** — convención de 3 archivos (neutral = inglés + es-DO + Designer) |
| Deps de `CreateTagUseCase` | 2 deps (repo + validator opcional) | **4 deps** (repo + `IClock` + validator + logger) — `Tag.Create` exige `createdAt` |
| Q-5 (creación de tags) | — (proposal: repo-direct en VM) | **Elevada a use case** (D-5): una escritura no puede saltar la capa de orquestación |
| Layout de `IncomeView` | 3 columnas (riel 260px + ledger + riel derecho 200px "optional") | **2 regiones** (riel ~280px + ledger) — el panel derecho se **corta**: duplica la info del form de edición; el form vive como sección "nuevo asiento" sobre el ledger (paridad con la UX existente) |
| `IsBusy` en el VM | listado como propiedad requerida | **Cortado** — espejo de `ExpensesViewModel` (D-6) que no lo tiene; `AsyncRelayCommand` de CommunityToolkit ya previene la doble ejecución del mismo comando |
| Test infra faltante | no listada | **`FakeTagRepository` (nuevo)** + extensión `FakeIncomeRepository` + `LastExpenseFilter` en `FakeExpenseRepository` + regresión `GetTransactionsQueryTests` (§2 corrección #8) |

### 3.2 Idioma de mensajes (hereda D-ML de F-005 / §3.2 de F-006)

Convención del codebase: **códigos de error en inglés** + **mensajes
humanos en español** (validadores, resúmenes de use case, mensajes de
query); excepciones técnicas que propagan vía `ex.Message` en inglés.
F-008 por archivo espejo:

| Mensaje | Idioma | Origen (espejo) |
| --- | --- | --- |
| Validación manual de `GetIncomesQuery` ("Page debe ser mayor o igual a 1.", etc.) | **Español** | `GetTransactionsQuery` (español) |
| Resumen de fallo de validación de `CreateTagUseCase` | **Español** | "El comando para crear la etiqueta no es válido." (espejo `RegisterIncomeUseCase`) |
| FluentValidation por-campo de `CreateTagValidator` | **Español** | espejo `RegisterIncomeValidator` |
| `"Validation"` de `DeleteSavedViewUseCase` ("Id cannot be empty.") | **Inglés** | espejo verbatim `DeleteTransactionUseCase` |
| `DomainInvariant` (`CreateTagUseCase`) | **Inglés** | `ex.Message` de `Tag.Create` ("Name is required.") |
| Doc-comments C# | Por archivo espejo | `GetIncomesQuery` español; `DeleteSavedView`/`CreateTag`/DTOs/VMs inglés; `SelectableTagItem` inglés |
| Códigos (`InvalidFilter`, `RepositoryError`, `Validation`, `DuplicateName`, `NotFound`, `DomainInvariant`) | **Inglés** | convención del codebase |

### 3.3 Otras decisiones de forma

- **Validación sync** `_validator.Validate(command)` — NO
  `ValidateAsync` (F-005 D-2; reglas sync, evita
  `AsyncValidatorInvokedSynchronouslyException`).
- **Fallo de validación con `details` por campo** (diccionario
  `PropertyName → string[]`), espejo `RegisterIncomeUseCase` líneas
  62-72 — solo aplica a `CreateTagUseCase` (los otros 2 orquestadores
  no llevan validator).
- **Árbol de tests** (F-005 C-3):
  `Tests.Unit/Application/<Aggregate>/<UseCase>/` —
  `Incomes/GetIncomes/`, `SavedViews/DeleteSavedView/`,
  `Catalog/CreateTag/`; VMs en `Tests.Unit/UI/`.
- **Doc-refresh del doc-comment de `GetTransactionsQueryRequest`**: la
  nota "los filtros… llegarán en Phase 4" se actualiza (ya llegaron) —
  edición sancionada porque el archivo ya se modifica (AC-3).
- **`RegisterIncomeCommand` no lleva `Source`** (verificado) — el form
  de IncomeView NO expone campo Source; `UpdateIncomeUseCase` resetea
  `Source` a null al editar (comportamiento heredado de F-005,
  documentado como limitación conocida, §8).
- **Ningún paquete NuGet nuevo.** Única edición de `.csproj`: el
  `ItemGroup` `<AvaloniaResource Include="Assets\Fonts\**" />` (AC-23).
- **Sin `MessageBox`/diálogos de plataforma** (D-7). Sin lógica en
  code-behind (los `.axaml.cs` quedan como los existentes: mínimo
  generado por la plantilla).
- **CommunityToolkit.Mvvm** para `[ObservableProperty]` /
  `[RelayCommand]` — patrón existente en todos los VMs.

---

## 4. Dirección estética (BOLD — skill `frontend-design`)

**Concepto: "Libro Mayor" — ledger editorial.** La app es el libro de
cuentas privado de alguien que se toma en serio su dinero: los números
son el héroe, la tipografía es de estado financiero impreso, no de
dashboard-SaaS genérico. La referencia mental es el libro contable
físico: renglones, hairlines, una pluma ámbar que subraya lo
importante.

### 4.1 Tipografía (3 fuentes OFL embebidas — la firma del diseño)

| Rol | Fuente | Uso | Especificación |
| --- | --- | --- | --- |
| Display | **Fraunces** (serif editorial con carácter) | Títulos de vista ("Ingresos", "Gastos"), numerales de cabecera, contador de total | Weight 600, tamaño 28 (título); 600/16 (section heads) |
| Body/UI | **Instrument Sans** (sans fresca, NO Inter/Roboto) | Labels, inputs, botones, texto de fila | Weight 400, tamaño 14; 500 para énfasis |
| Tabular | **Spline Sans Mono** | **Montos** — la firma del ledger: cifras alineadas a la derecha en columna, ancho fijo | Weight 400-500, tamaño 14-15, right-aligned |

Todas **OFL**, embebidas como `.ttf` variables en `Assets/Fonts/`
(vía `AvaloniaResource`, AC-23) y expuestas como recursos `FontFamily`
en `Tokens.axaml`:
`avares://ControlDeFinanzasIA.UI/Assets/Fonts#Fraunces`,
`...#Instrument Sans`, `...#Spline Sans Mono`. Archivos de licencia
OFL incluidos en `Assets/Fonts/`. **Prohibido** Inter/Roboto/Arial/
system-fonts como identidad (skill `frontend-design`).

### 4.2 Paleta (tokens en `Tokens.axaml` — dominante + acentos sharp)

| Token | Valor | Rol |
| --- | --- | --- |
| `InkBrush` | `#16181D` | Fondo profundo (tinta) — dominante |
| `SurfaceBrush` | `#1F232B` | Superficies elevadas (riel, panel de form) |
| `HairlineBrush` | `#31363F` | Líneas de renglón contable (separadores de fila, borders) |
| `PaperBrush` | `#E8E4D9` | Texto principal (papel cálido) |
| `PaperDimBrush` | ~`#9BA0A8` | Texto secundario/labels (papel atenuado — valor exacto a definir en el design pass) |
| `AmberBrush` | `#E8A33D` | Acento sharp — "la pluma del contador": regla de cabecera, tick de hover, foco, CTA primario |
| `SageBrush` | `#7FB069` | Semántica ingreso (montos de ingreso, signo +) |
| `TerracottaBrush` | `#D96459` | Semántica gasto (montos de gasto, signo −, estados de error/destructivo) |

**Tema Dark único** (`RequestedThemeVariant="Dark"`). Nada de
gradientes morados sobre blanco; nada de temas múltiples. Dominante
tinta + acentos ámbar/sage/terracota con propósito semántico.

### 4.3 Composición espacial (asimétrica, grid-breaking)

- **Riel izquierdo persistente (~280px)** para filtros + vistas
  guardadas + creación de tag: fondo `SurfaceBrush`, separado del
  ledger por un hairline vertical. Persiste mientras se navega la
  página.
- **Ledger a la derecha (`*`)**: banda de cabecera editorial — título
  Fraunces sobre una **regla ámbar** (hairline 2px `AmberBrush`) que
  **cruza el borde del riel** (grid-breaking: la regla se extiende
  sobre el riel, rompiendo la columna); debajo, sección "nuevo
  asiento" (form) y el ledger de transacciones.
- **Renglones contables**: cada fila es un renglón — hairline inferior
  `HairlineBrush`, fecha en Instrument Sans, descripción en body,
  monto en **Spline Sans Mono right-aligned** con color de semántica
  (sage para ingreso, terracotta para gasto), botones de fila como
  texto-acción ámbar.
- **Generous negative space** en la cabecera; densidad controlada en
  el ledger (renglones compactos de ~40-44px).

### 4.4 Motion (solo `Animation`/`Transitions` de Avalonia — sin code-behind)

| Efecto | Especificación |
| --- | --- |
| **Reveal escalonado de filas** | Al cargar/refrescar: filas entran con opacity 0→1 + TranslateY 8→0, ~200ms easeOutCubic, stagger ~30ms por fila. Implementación pure-XAML vía selectores `:nth-child(n)` con `Animation Delay = n×30ms`, **cap a las primeras ~10 filas** (el resto entra sin delay). **Fallback sancionado**: si el stagger por nth-child resulta impracticable, reveal uniforme del contenedor (misma curva/duración) — JAMÁS code-behind |
| **Hover de fila** | Wash sutil de fondo (`SurfaceBrush` → mezcla hacia ámbar ~8%) + **tick ámbar**: marca de 2-3px en el borde izquierdo de la fila que hace scale 0→1 en ~150ms easeOutBack |
| **Transición del riel** | `Transitions` en Width/Opacity ~250ms al expandir/colapsar secciones del riel |
| **Botones** | Press: scale 0.98 ~100ms; hover CTA: subrayado ámbar que crece de izquierda a derecha ~150ms |

### 4.5 Profundidad y fondo

Superficies por **capas con hairlines** (ink → surface → hairlines),
NO sombras difusas genéricas. El motivo visual es el **rayado
contable**: regla ámbar de cabecera + hairlines de renglón. Sin
noise/mesh textures (costo/asset innecesario en desktop) — la
atmósfera sale de la tipografía y las capas.

### 4.6 Implementación pragmática

`FluentTheme` como base de controles (inputs accesibles: DatePicker,
ComboBox, TextBox siguen siendo FluentTheme **re-mapeados a tokens**:
foreground `PaperBrush`, backgrounds `InkBrush`/`SurfaceBrush`,
borders/foco `AmberBrush`, corner-radius ~4-6px, sin drop-shadows).
La identidad la llevan fuente, paleta y layout. `LedgerStyles.axaml`
define estilos con **classes** explícitas (`ledger-btn`,
`ledger-input`, `ledger-row`, `ledger-combo`, `ledger-section`) para
no colisionar con FluentTheme; se incluye **a nivel de
`UserControl.Styles`** en las 2 vistas (D-12 — shell y dashboard no
se ven afectados).

---

## 5. Criterios de aceptación

La feature se considera completada cuando **todos** los ACs
siguientes se cumplen, verificables mediante tests automatizados y
revisión de código. Cada AC incluye escenarios Given/When/Then.

### AC-1 — `IncomeDto` + `IncomeExtensions` (NEW ×2, en `Application/DTOs/`) (D-3)

- [ ] `src/ControlDeFinanzasIA.Application/DTOs/IncomeDto.cs` (NEW):

```csharp
using ControlDeFinanzasIA.Domain.Enums;

namespace ControlDeFinanzasIA.Application.DTOs;

/// <summary>
/// Data transfer object that represents an <see cref="Income"/> in the
/// application layer. It is the contract exposed to the UI and other
/// consumers outside of <c>Application</c>.
/// </summary>
public sealed record IncomeDto(
    Guid Id,
    Guid? CategoryId,
    DateOnly Date,
    decimal Amount,
    Currency Currency,
    string Description,
    string? Note,
    string? Source,
    DateTime CreatedAt,
    DateTime UpdatedAt);
```

- [ ] `src/.../Application/DTOs/IncomeExtensions.cs` (NEW):
      `public static class IncomeExtensions` con
      `public static IncomeDto ToDto(this Income income) => new(
      income.Id, income.CategoryId, income.Date, income.Amount.Amount,
      income.Amount.Currency, income.Description, income.Note,
      income.Source, income.CreatedAt, income.UpdatedAt);` — espejo
      línea a línea de `ExpenseExtensions` (inglés, XML doc en clase +
      método + params).
- [ ] Sin `Tags` en el DTO (§2 corrección #3 — el repo no materializa
      la navegación).

- **GIVEN** un `Income.Create(...)` con `CategoryId = null`,
  `Source = "Sueldo"`.
- **WHEN** `income.ToDto()`.
- **THEN** las 10 props iguales (`CategoryId == null`,
  `Source == "Sueldo"`, `Amount == income.Amount.Amount`,
  `Currency == income.Amount.Currency`).

### AC-2 — `GetIncomesQueryRequest` + `GetIncomesQuery` (NEW ×2) (D-1, RF-17)

- [ ] `src/.../Application/UseCases/Incomes/GetIncomes/GetIncomesQueryRequest.cs`
      (NEW): record posicional con XML doc por param:

```csharp
/// <summary>
/// Request inmutable para <see cref="GetIncomesQuery"/>: paginación
/// más filtros opcionales (categoría, rango de fechas, tags OR).
/// Todos los filtros son opcionales; <c>null</c> significa sin filtro.
/// </summary>
public sealed record GetIncomesQueryRequest(
    int Page = 1,
    int PageSize = 50,
    Guid? CategoryId = null,
    DateOnly? DateFrom = null,
    DateOnly? DateTo = null,
    IReadOnlyList<Guid>? TagIds = null);
```

- [ ] `src/.../Application/UseCases/Incomes/GetIncomes/GetIncomesQuery.cs`
      (NEW): `public sealed class GetIncomesQuery` con 2 deps
      (`IIncomeRepository repository`, `ILogger<GetIncomesQuery>
      logger`), ctor con asignación directa (espejo
      `GetTransactionsQuery`) y `public async Task<Result>
      ExecuteAsync(GetIncomesQueryRequest request, CancellationToken
      ct = default)`. Flow (en orden):
  1. `ArgumentNullException.ThrowIfNull(request)`.
  2. `request.Page < 1` → `Result.Failure("InvalidFilter", "Page debe
     ser mayor o igual a 1.")`.
  3. `request.PageSize <= 0` → `Result.Failure("InvalidFilter",
     "PageSize debe ser mayor a 0.")`; `> 200` →
     `Result.Failure("InvalidFilter", "PageSize debe ser menor o
     igual a 200.")`.
  4. `request.DateFrom > request.DateTo` (ambos no-null) →
     `Result.Failure("InvalidFilter", "DateFrom no puede ser
     posterior a DateTo.")`.
  5. `request.TagIds` contiene `Guid.Empty` →
     `Result.Failure("InvalidFilter", "TagIds no puede contener
     identificadores vacíos.")`.
  6. `IncomeFilter filter = new() { Page = request.Page, PageSize =
     request.PageSize, CategoryId = request.CategoryId, DateFrom =
     request.DateFrom, DateTo = request.DateTo, TagIds =
     request.TagIds };` — **mapeo 1:1, sin hardcodeos**.
  7. try: `PagedResult<Income> page = await
     _repository.GetByFilterAsync(filter, ct);` → `items =
     page.Items.Select(i => i.ToDto()).ToList();` → `return
     Result.Success(new PagedResult<IncomeDto>(items, page.Total,
     filter.Page, filter.PageSize));`.
  8. catch `OperationCanceledException` → `throw;` (espejo); catch
     `Exception ex` → `_logger.LogError(ex, "Error al obtener
     ingresos (Page={Page}, PageSize={PageSize})", filter.Page,
     filter.PageSize)` → `Result.Failure("RepositoryError", "Ocurrió
     un error al obtener los ingresos.")`.
- [ ] XML doc español en clase + ctor + método (espejo
      `GetTransactionsQuery`, incl. remarks del cast de `Value` a
      `PagedResult<IncomeDto>`).

- **GIVEN** `FakeIncomeRepository` con `SetFilterResult(incomes(5), total: 12, page: 2, pageSize: 5)`.
- **WHEN** `ExecuteAsync(new GetIncomesQueryRequest(Page: 2, PageSize: 5))`.
- **THEN** `IsSuccess`; `Value` castea a `PagedResult<IncomeDto>` con
  5 `IncomeDto` (props mapeadas por `ToDto`), `Total == 12`,
  `Page == 2`, `PageSize == 5`, `TotalPages == 3`.
- **GIVEN** `Page = 0` (o `PageSize = 0`, `PageSize = 201`,
  `DateFrom = 2026-02-01, DateTo = 2026-01-01`, `TagIds = [Guid.Empty]`).
- **WHEN** `ExecuteAsync`.
- **THEN** `Failure("InvalidFilter")` con el mensaje español exacto;
  el repo **no** recibió `GetByFilterAsync`
  (`GetByFilterCallCount == 0`).
- **GIVEN** fake con `SetThrowOnGetFilter(new InvalidOperationException("boom"))`.
- **WHEN** `ExecuteAsync` con request válido.
- **THEN** `Failure("RepositoryError")` — sin excepción al caller.

### AC-3 — `GetTransactionsQueryRequest` + `GetTransactionsQuery` extendidos (MODIFY ×2) (D-2)

- [ ] `GetTransactionsQueryRequest.cs` (MODIFY): AÑADIR al record
      posicional los 5 params opcionales (mismos nombres/defaults que
      AC-2) + actualizar el doc-comment (la nota "los filtros…
      llegarán en Phase 4" se reemplaza por la descripción de los
      filtros presentes).
- [ ] `GetTransactionsQuery.cs` (MODIFY): reemplazar el bloque
      hardcodeado (`CategoryId = null, DateFrom = null, DateTo =
      null`) por el mapeo del request (las 6 props incl. `TagIds`);
      añadir los guards de coherencia de AC-2 pasos 4-5 (mismos
      mensajes) entre la validación de PageSize y la construcción del
      filtro. **Cero cambios** en el resto del flow (validación
      Page/PageSize, try/catch, `RepositoryError`).
- [ ] Compatibilidad: callers existentes (`ExpensesViewModel` con
      `new GetTransactionsQueryRequest(Page, 20)`) y tests existentes
      compilan **sin cambio** (defaults `null` = comportamiento
      anterior).

- **GIVEN** `new GetTransactionsQueryRequest(Page: 1, PageSize: 20, CategoryId: catId, DateFrom: d1, DateTo: d2, TagIds: [t1, t2])`.
- **WHEN** `ExecuteAsync` contra `FakeExpenseRepository`.
- **THEN** el filtro capturado (`LastExpenseFilter`, AC-6) tiene las
  6 props iguales al request (regresión del mapeo — antes nulls).
- **GIVEN** request sin filtros (defaults).
- **WHEN** `ExecuteAsync`.
- **THEN** `LastExpenseFilter` con `CategoryId/DateFrom/DateTo/TagIds
  == null` — comportamiento pre-F-008 preservado.

### AC-4 — `DeleteSavedViewCommand` + `DeleteSavedViewUseCase` (NEW ×2) (D-4)

- [ ] `src/.../Application/UseCases/SavedViews/DeleteSavedView/DeleteSavedViewCommand.cs`
      (NEW):

```csharp
/// <summary>
/// Immutable command to delete an existing saved view by identity.
/// </summary>
/// <param name="SavedViewId">Identifier of the saved view to remove.</param>
public sealed record DeleteSavedViewCommand(Guid SavedViewId);
```

- [ ] `src/.../Application/UseCases/SavedViews/DeleteSavedView/DeleteSavedViewUseCase.cs`
      (NEW): `public sealed class DeleteSavedViewUseCase` con 2 deps
      (`ISavedViewRepository _savedViewRepository`,
      `ILogger<DeleteSavedViewUseCase> _logger`), ctor con params con
      XML doc, y `public async Task<Result> ExecuteAsync(
      DeleteSavedViewCommand command, CancellationToken ct =
      default)`:

```csharp
ArgumentNullException.ThrowIfNull(command);

if (command.SavedViewId == Guid.Empty)
{
    return Result.Failure("Validation", "Id cannot be empty.");
}

return await _savedViewRepository.RemoveAsync(command.SavedViewId, ct);
```

- [ ] XML doc inglés (espejo `DeleteTransactionUseCase`: summary +
      remarks "does not use FluentValidation; the repository result
      is propagated verbatim").

- **GIVEN** `FakeSavedViewRepository` con una vista sembrada.
- **WHEN** `ExecuteAsync(new DeleteSavedViewCommand(view.Id))`.
- **THEN** `IsSuccess`; la vista ya no está en `GetAllAsync()`.
- **GIVEN** `new DeleteSavedViewCommand(Guid.Empty)`.
- **WHEN** `ExecuteAsync`.
- **THEN** `Failure("Validation")` ("Id cannot be empty."); el repo no
  recibió `RemoveAsync`.
- **GIVEN** `RemoveResultOverride = Result.Failure("NotFound", "...")`.
- **WHEN** `ExecuteAsync` con id válido.
- **THEN** el failure del repo se propaga verbatim (`"NotFound"`).

### AC-5 — `CreateTagCommand` + `CreateTagValidator` + `CreateTagUseCase` (NEW ×3) (D-5)

- [ ] `src/.../Application/UseCases/Catalog/CreateTag/CreateTagCommand.cs`
      (NEW): `public sealed record CreateTagCommand(string Name,
      string? Color);` + XML doc (timestamps desde `IClock`, espejo
      `RegisterIncomeCommand`).
- [ ] `.../CreateTag/CreateTagValidator.cs` (NEW):
      `public sealed class CreateTagValidator :
      AbstractValidator<CreateTagCommand>` con exactamente 2 reglas
      sync (mensajes español):

```csharp
RuleFor(c => c.Name)
    .NotEmpty().WithMessage("El nombre de la etiqueta es obligatorio.")
    .MaximumLength(50).WithMessage("El nombre no puede superar 50 caracteres.")
    .Must(n => n.Trim().Length > 0).WithMessage("El nombre no puede contener solo espacios en blanco.");

RuleFor(c => c.Color)
    .MaximumLength(9).WithMessage("El color no puede superar 9 caracteres.");
```

- [ ] `.../CreateTag/CreateTagUseCase.cs` (NEW): `public sealed class
      CreateTagUseCase` con **4 deps** (`ITagRepository`, `IClock`,
      `IValidator<CreateTagCommand>`, `ILogger<CreateTagUseCase>`) y
      `public async Task<Result> ExecuteAsync(CreateTagCommand
      command, CancellationToken ct = default)`. Flow: (1)
      `ThrowIfNull`; (2) `_validator.Validate` sync → inválido →
      `Result.Failure("Validation", "El comando para crear la
      etiqueta no es válido.", details)`; (3) `Tag tag; try { tag =
      Tag.Create(command.Name, command.Color, _clock.UtcNow); }
      catch (ArgumentException ex) { _logger.LogWarning(...); return
      Result.Failure("DomainInvariant", ex.Message); }`; (4) `Result
      addResult = await _tagRepository.AddAsync(tag, ct);` → si
      falla, propagar verbatim (expone `"DuplicateName"`); (5)
      `return Result.Success(tag);`.
- [ ] XML doc inglés (espejo `RegisterIncomeUseCase`, incl. remarks
      del cast de `Value` a `Tag`).

- **GIVEN** command válido (`"Transporte"`, `Color = "#E8A33D"`).
- **WHEN** `ExecuteAsync`.
- **THEN** `IsSuccess`; `AddedTags` tiene 1 `Tag` con `Name ==
  "Transporte"` (trim aplicado), `Color` verbatim, `CreatedAt ==
  _clock.UtcNow`.
- **GIVEN** Theory `Name ∈ {"", " ", "\t"}` o `Name` de 51 chars o
  `Color` de 10 chars.
- **WHEN** `ExecuteAsync`.
- **THEN** `Failure("Validation")` con `Details` conteniendo `Name`
  (o `Color`); `Assert.Empty(AddedTags)`.
- **GIVEN** `AddResultOverride = Result.Failure("DuplicateName", "...")`.
- **WHEN** `ExecuteAsync` con nombre válido.
- **THEN** propaga `"DuplicateName"` — sin excepción.

### AC-6 — Test infra: `FakeTagRepository` (NEW) + extensión de `FakeIncomeRepository`/`FakeExpenseRepository` (MODIFY ×2)

- [ ] `tests/.../Tests.Unit/TestDoubles/FakeTagRepository.cs` (NEW):
      `public sealed class FakeTagRepository : ITagRepository`,
      hand-rolled (sin mocking), espejo del patrón
      `FakeSavedViewRepository`: `AddedTags` (captura `IReadOnlyList<Tag>`),
      propiedades `AddResultOverride`/`UpdateResultOverride`/
      `RemoveResultOverride` (`Result?`) + `GetByIdResultOverride`
      (`Tag?`); `GetAllAsync` retorna snapshot de la lista capturada;
      `AddAsync` captura + `AddResultOverride ??
      Result.Success(null!)`; `RemoveAsync`/`UpdateAsync` in-memory
      por Id; `GetByIdAsync` busca en la lista capturada;
      `/// <inheritdoc />` en los 5 métodos + XML doc de clase
      (inglés, espejo de los otros fakes).
- [ ] `FakeIncomeRepository.cs` (MODIFY): AÑADIR (espejo
      `FakeExpenseRepository` líneas 44-80): `PagedResult<Income>
      FilterResult { get; private set; }`, `int
      GetByFilterCallCount { get; private set; }`, `IncomeFilter?
      LastIncomeFilter { get; private set; }` (captura del filtro
      recibido), `SetFilterResult(PagedResult<Income>)` +
      `SetFilterResult(IEnumerable<Income>, int total, int page = 1,
      int pageSize = 50)`, `SetThrowOnGetFilter(Exception)`; el
      `GetByFilterAsync` pasa de devolver vacío hardcoded a:
      incrementa contador, captura `LastIncomeFilter`, lanza si
      configurado, si no retorna `FilterResult`.
- [ ] `FakeExpenseRepository.cs` (MODIFY): AÑADIR `ExpenseFilter?
      LastExpenseFilter { get; private set; }` (captura en
      `GetByFilterAsync`) — habilita las regresiones de mapeo de
      AC-3.

- **GIVEN** `FakeTagRepository` recién construido.
- **WHEN** `GetAllAsync()` sin llamadas previas.
- **THEN** lista vacía; `AddResultOverride == null` (default success).
- **GIVEN** `FakeIncomeRepository.SetFilterResult(incomes(3), 9)`.
- **WHEN** `GetByFilterAsync(new IncomeFilter { Page = 2 })`.
- **THEN** retorna `PagedResult` con 3 items / `Total == 9`;
  `GetByFilterCallCount == 1`; `LastIncomeFilter.Page == 2`.

### AC-7 — `IncomeViewModel` sealed partial (NEW — MVVM estricto, testeable sin UI) (D-6, D-7, D-8, D-11)

- [ ] `src/.../UI/ViewModels/IncomeViewModel.cs` (NEW):
      `public sealed partial class IncomeViewModel : ViewModelBase`.
      **14 deps** en el ctor (todas con
      `ArgumentNullException.ThrowIfNull` + asignación a campos
      `readonly`, espejo `ExpensesViewModel`): `RegisterIncomeUseCase`,
      `UpdateIncomeUseCase`, `DeleteTransactionUseCase`,
      `GetIncomesQuery`, `SaveCurrentFilterUseCase`,
      `ApplySavedViewUseCase`, `GetSavedViewsUseCase`,
      `DeleteSavedViewUseCase`, `CreateTagUseCase`,
      `ICategoryRepository`, `ITagRepository`, `IClock`,
      `ILogger<IncomeViewModel>`, `IStringLocalizer<Strings>`. Ctor
      termina con `_date = _clock.Today;` + `CurrentFilter = new
      IncomeFilter { Page = 1, PageSize = 20 };` + fire-and-forget
      `_ = LoadCategoriesAsync(); _ = LoadTagsAsync(); _ =
      RefreshAsync(); _ = LoadSavedViewsAsync();` (patrón existente).
- [ ] **Propiedades observables** (espejo `ExpensesViewModel` +
      extensiones; todas con XML doc inglés):

| Grupo | Propiedades |
| --- | --- |
| Form | `Date` (`DateOnly?`), `Amount` (`decimal?`), `Currency` (`Currency`, default `DOP`), `SelectedCategoryId` (`Guid?`), `Description` (`string?`), `Note` (`string?`) |
| Lista | `Incomes` (`ObservableCollection<IncomeDto>`), `SelectedIncome` (`IncomeDto?`), `IsEditMode` (`bool`), `EditableId` (`Guid?`), `PendingDeleteIncomeId` (`Guid?`) |
| Paginación | `Page` (`int`, 1), `TotalPages` (`int`), `CanGoToPreviousPage`/`CanGoToNextPage` (computed), `PageSummary` (`string?`, compuesto con `_localizer["PageOfPagesFormat"]` — actualizado en `OnPageChanged`/`OnTotalPagesChanged`) |
| Errores/estado | `ValidationErrors` (`Dictionary<string, string>`), `FormError` (`string?`), `StatusMessage` (`string?` — se limpia al iniciar cada comando, D-7) |
| Filtro | `FilterCategoryId` (`Guid?`), `FilterDateFrom` (`DateOnly?`), `FilterDateTo` (`DateOnly?`), `CurrentFilter` (`IncomeFilter`) |
| Tags | `AvailableTags` (`IReadOnlyList<SelectableTagItem>`, vacío default), `NewTagName` (`string?`), `NewTagColor` (`string?`), `TagCreationError` (`string?`) |
| Vistas guardadas | `SavedViews` (`IReadOnlyList<SavedView>`, vacío default), `SelectedSavedView` (`SavedView?`), `NewViewName` (`string?`), `PendingDeleteViewId` (`Guid?`) |

- [ ] **Comandos** (todos `[RelayCommand]` sobre métodos privados,
      espejo patrones existentes):
  - `RefreshAsync()` — `_getIncomesQuery.ExecuteAsync(new
    GetIncomesQueryRequest(Page, 20, CurrentFilter.CategoryId,
    CurrentFilter.DateFrom, CurrentFilter.DateTo, SelectedTagIds))`
    → éxito: repuebla `Incomes` + `TotalPages` + `PageSummary`;
    fallo: limpia `Incomes`, `TotalPages = 0`, `FormError =
    _localizer["FormErrorMessage"]` + log (espejo del existente).
    `SelectedTagIds` = ids de `AvailableTags` con `IsSelected`, o
    `null` si ninguno.
  - `RegisterIncomeAsync()` — espejo del existente en Expenses
    (build `RegisterIncomeCommand` desde el form; `"Validation"` →
    `ValidationErrors` con details; éxito: limpia form + refresh),
    + `StatusMessage = _localizer["IncomeRegisteredMessage"]`.
  - `BeginEdit(IncomeDto dto)` — carga el form desde el DTO
    (`Date/Amount/Currency/SelectedCategoryId/Description/Note`),
    `EditableId = dto.Id`, `IsEditMode = true`, resetea
    `PendingDeleteIncomeId`.
  - `UpdateIncomeAsync()` — `UpdateIncomeCommand(EditableId!.Value,
    SelectedCategoryId, Date ?? _clock.Today, Amount ?? 0m,
    Currency, Description ?? "", Note)` → éxito: `IsEditMode =
    false`, `EditableId = null`, limpia form, `StatusMessage`,
    refresh; `"Validation"` → `ValidationErrors`; otro → `FormError`.
  - `CancelEdit()` — `IsEditMode = false`, `EditableId = null`,
    limpia form + errores.
  - `DeleteIncome(IncomeDto dto)` — **2 pasos** (D-7): si
    `PendingDeleteIncomeId != dto.Id` → `PendingDeleteIncomeId =
    dto.Id` (el botón de esa fila pasa a "Confirmar") y return; si ya
    está pendiente → `_deleteTransactionUseCase.ExecuteAsync(new
    DeleteTransactionCommand(dto.Id, TransactionType.Income))` →
    éxito: resetea pendiente, `StatusMessage`, refresh; fallo:
    `FormError` + log. Toda otra acción de VM resetea
    `PendingDeleteIncomeId = null`.
  - `ApplyFilterAsync()` — `Page = 1`; `CurrentFilter = new
    IncomeFilter { Page = 1, PageSize = 20, CategoryId =
    FilterCategoryId, DateFrom = FilterDateFrom, DateTo =
    FilterDateTo, TagIds = SelectedTagIds }`; `StatusMessage = null`;
    refresh.
  - `ClearFilterAsync()` — resetea `FilterCategoryId/FilterDateFrom/
    FilterDateTo`, deselecciona todos los `SelectableTagItem`,
    `CurrentFilter = new IncomeFilter { Page = 1, PageSize = 20 }`,
    `Page = 1`, refresh.
  - `SaveViewAsync()` — `NewViewName` vacío/whitespace →
    `StatusMessage = _localizer["ViewNameRequiredMessage"]` y
    return; `_saveCurrentFilterUseCase.ExecuteAsync(new
    SaveCurrentFilterCommand(NewViewName.Trim(),
    TransactionType.Income, CurrentFilter))` → éxito:
    `NewViewName = null`, `_ = LoadSavedViewsAsync()`,
    `StatusMessage = _localizer["ViewSavedMessage"]`; fallo
    (`DuplicateName`/`Validation`/...): `StatusMessage` con
    `_localizer["FormErrorMessage"]` + log — sin excepción.
  - `ApplySavedViewAsync()` — si `SelectedSavedView is null` →
    return; `_applySavedViewUseCase.ExecuteAsync(new
    ApplySavedViewQuery(SelectedSavedView.Id))` → éxito: cast
    `ApplySavedViewResult`; `Filter` es `IncomeFilter` aplicado →
    **sincroniza las props de filtro observables** desde el filter
    (`FilterCategoryId/FilterDateFrom/FilterDateTo` +
    selección de `AvailableTags` por `TagIds`), `CurrentFilter =
    filter with { Page = 1 }` (D-11), `Page = 1`, refresh; fallo
    (`NotFound`/`IncompatibleVersion`/`CorruptedFilter`):
    `StatusMessage` de error + log — nunca success silencioso.
    Disparado desde `OnSelectedSavedViewChanged` (partial method,
    fire-and-forget `_ = ApplySavedViewAsync();` solo cuando el
    valor nuevo no es `null`).
  - `DeleteSavedViewAsync()` — **2 pasos** (D-7) con
    `SelectedSavedView`/`PendingDeleteViewId` (espejo de
    `DeleteIncome`): confirmado → `_deleteSavedViewUseCase.
    ExecuteAsync(new DeleteSavedViewCommand(SelectedSavedView.Id))`
    → éxito: `SelectedSavedView = null`, `_ =
    LoadSavedViewsAsync()`, `StatusMessage`; fallo: `StatusMessage`
    de error + log.
  - `CreateTagAsync()` — `NewTagName` vacío → `TagCreationError =
    _localizer["TagNameRequiredMessage"]` y return;
    `_createTagUseCase.ExecuteAsync(new CreateTagCommand(
    NewTagName, NewTagColor))` → éxito: `NewTagName = null`,
    `NewTagColor = null`, `TagCreationError = null`, `_ =
    LoadTagsAsync()`, `StatusMessage`; fallo: `TagCreationError`
    (`"DuplicateName"` → mensaje localizado específico; otro →
    `FormErrorMessage`) + log.
  - `PreviousPageAsync()`/`NextPageAsync()` — espejo existente
    (con `NotifyCanExecuteChanged` en `OnPageChanged`/
    `OnTotalPagesChanged`).
- [ ] **Métodos privados**: `LoadCategoriesAsync()` (espejo
      existente: `GetAllAsync` → `Categories` con `ToDto()`),
      `LoadTagsAsync()` (`_tagRepository.GetAllAsync` → `AvailableTags`
      preservando `IsSelected` de items ya seleccionados),
      `LoadSavedViewsAsync()` (`_getSavedViewsUseCase.ExecuteAsync(
      new GetSavedViewsQuery(TransactionType.Income))` → cast
      `IReadOnlyList<SavedView>` → `SavedViews`).
- [ ] **Cero code-behind, cero diálogos de plataforma.** Todo el
      estado es observable y testeable sin UI (patrón
      `ExpensesViewModelTests`).

- **GIVEN** ctor con fakes estándar (repositorios vacíos).
- **WHEN** se construye el VM.
- **THEN** `Page == 1`, `TotalPages == 0`, `Incomes` vacío,
  `Categories` poblada (si el fake tiene), `AvailableTags` vacía,
  `SavedViews` vacía, `IsEditMode == false`,
  `PendingDeleteIncomeId == null`, `Date == _clock.Today`.
- **GIVEN** `SetFilterResult(incomes(20), 45)` + `RefreshCommand`.
- **WHEN** `NextPageCommand.ExecuteAsync(null)`.
- **THEN** `Page == 2`; `GetByFilterCallCount` incrementó en 1
  (`LastIncomeFilter.Page == 2`, `PageSize == 20`).
- **GIVEN** filtro configurado (`FilterCategoryId = cat`,
  `FilterDateFrom = d1`, `FilterDateTo = d2`, 2 tags seleccionados).
- **WHEN** `ApplyFilterCommand.ExecuteAsync(null)`.
- **THEN** `Page == 1`; `LastIncomeFilter` tiene `CategoryId == cat`,
  `DateFrom == d1`, `DateTo == d2`, `TagIds` con los 2 ids (orden
  estable).
- **GIVEN** vista "Sueldo" guardada con filtro `CategoryId = cat`.
- **WHEN** `SelectedSavedView = view` → `ApplySavedViewAsync` corre.
- **THEN** `FilterCategoryId == cat`, `CurrentFilter.CategoryId ==
  cat`, `CurrentFilter.Page == 1`, `Page == 1`,
  `GetByFilterCallCount` incrementó.
- **GIVEN** fila con `PendingDeleteIncomeId == null`.
- **WHEN** `DeleteIncomeCommand.ExecuteAsync(dto)` dos veces.
- **THEN** la 1ª solo arma `PendingDeleteIncomeId == dto.Id` (el repo
  no recibió `RemoveAsync`); la 2ª ejecuta
  `DeleteTransactionUseCase` y resetea el pendiente.

### AC-8 — `IncomeView.axaml` + `SelectableTagItem` (NEW — diseño BOLD "Libro Mayor") (D-6, D-8, §4)

- [ ] `src/.../UI/ViewModels/SelectableTagItem.cs` (NEW):
      `public sealed partial class SelectableTagItem : ObservableObject`
      con `Id` (`Guid`, init), `Name` (`string`, init), `Color`
      (`string?`, init) + `[ObservableProperty] private bool
      _isSelected;` + XML doc inglés. (~30 líneas.)
- [ ] `src/.../UI/Views/IncomeView.axaml` (NEW) +
      `IncomeView.axaml.cs` (NEW, mínimo generado — sin lógica):
      `<UserControl ... x:Class="ControlDeFinanzasIA.UI.Views.IncomeView"
      x:DataType="vm:IncomeViewModel">` — **compiled bindings
      obligatorios** (`AvaloniaUseCompiledBindingsByDefault=true`):
      todo `DataTemplate` con su `x:DataType` correcto
      (`dto:IncomeDto`, `SelectableTagItem`, `SavedView`, `CategoryDto`).
- [ ] **Estructura** (2 regiones, §4.3):
  - **Riel izquierdo (~280px, persistente)**: sección Filtros
    (ComboBox Category con opción "Sin categoría" = `SelectedItem
    null` + PlaceholderText localizado; picker de Tags — checkboxes
    bindados a `IsSelected` de `SelectableTagItem` con estado vacío
    visible; DatePicker desde/hasta con `DateOnlyConverter`;
    botones "Aplicar filtros" / "Limpiar filtros"), sección Vistas
    guardadas (ComboBox `SavedViews` con `DisplayMemberBinding =
    Name`, `NewViewName` TextBox inline, botones Guardar/Eliminar
    vista), sección Nueva etiqueta (TextBox nombre + color opcional
    + botón Crear + `TagCreationError` inline).
  - **Ledger (derecha, `*`)**: cabecera editorial (título
    `IncomesTitle` en Fraunces + regla ámbar grid-breaking +
    `StatusMessage` como línea de estado), sección "nuevo asiento"
    (el form de registro/edición existente-patrón: `Date`, `Amount`,
    `Currency`, `SelectedCategoryId`, `Description`, `Note` +
    `ValidationErrors` por campo + `FormError`; en `IsEditMode`
    muestra botones "Guardar cambios"/"Cancelar" en lugar de
    "Registrar"), y el **ledger de ingresos**: `ListBox`/`ItemsControl`
    de `Incomes` con renglones (fecha, descripción con ellipsis,
    monto Spline Sans Mono right-aligned en `SageBrush`, botones
    Editar/Eliminar por fila) + paginación (`PreviousPageButton`,
    `PageSummary`, `NextPageButton`).
- [ ] Botones de fila: `Command="{Binding $parent[UserControl].
      BeginEditCommand}"` + `CommandParameter="{Binding}"` (el DTO de
      la fila) — patrón compiled-bindings; el label "Eliminar"→
      "Confirmar" según `PendingDeleteIncomeId == fila.Id` (vía
      converter o clases de estilo — sin code-behind).
- [ ] Strings 100% localizados (`x:Static res:Strings.*` /
      bindings a props VM ya localizadas) — **cero español
      hardcodeado** (AC-24).
- [ ] Incluye `UserControl.Styles` → `StyleInclude` de
      `LedgerStyles.axaml` + `Motion.axaml` (D-12 — ver AC-21/AC-22;
      en PR-2 el archivo puede ser funcional-first y el design pass
      de PR-4 lo aplica).

- **GIVEN** la app compilada.
- **WHEN** se abre la vista (navegación AC-16).
- **THEN** renderiza sin `InvalidOperationException`; el ledger
  muestra `Incomes`; los montos right-aligned; los botones Editar/
  Eliminar responden a los comandos del VM.
- **GIVEN** `dotnet build`.
- **WHEN** compila el XAML.
- **THEN** 0 warnings de compiled bindings (todo `x:DataType`
  presente y correcto — gate AC-27).

### AC-9 — `ExpensesViewModel` extendido (MODIFY — mayor) (D-2, D-6, D-7, D-8, D-11)

- [ ] `src/.../UI/ViewModels/ExpensesViewModel.cs` (MODIFY): conserva
      **intacto** todo lo existente (6 deps actuales, form, register,
      refresh, paginación, `ValidationErrors`/`FormError`,
      `LoadCategoriesAsync`, `OnPageChanged`/`OnTotalPagesChanged`) y
      AÑADE el **espejo exacto de AC-7 adaptado a Expense**:
  - **+8 deps**: `UpdateExpenseUseCase`, `DeleteTransactionUseCase`,
    `SaveCurrentFilterUseCase`, `ApplySavedViewUseCase`,
    `GetSavedViewsUseCase`, `DeleteSavedViewUseCase`,
    `CreateTagUseCase`, `ITagRepository` (total ctor: 14, espejo de
    IncomeViewModel).
  - **+props**: `Expenses` ya existe; añade `SelectedExpense`
    (`ExpenseDto?`), `IsEditMode`, `EditableId`, `PendingDeleteExpenseId`,
    `StatusMessage`, `FilterCategoryId`, `FilterDateFrom`,
    `FilterDateTo`, `CurrentFilter` (`ExpenseFilter`), `AvailableTags`,
    `NewTagName`, `NewTagColor`, `TagCreationError`, `SavedViews`,
    `SelectedSavedView`, `NewViewName`, `PendingDeleteViewId`,
    `PageSummary`.
  - **+comandos**: `UpdateExpense` (usa `UpdateExpenseCommand`),
    `BeginEdit`, `CancelEdit`, `DeleteExpense` (`DeleteTransactionCommand(id,
    TransactionType.Expense)`), `ApplyFilter`, `ClearFilter`,
    `SaveView` (`TransactionType.Expense`), `ApplySavedView`
    (`ExpenseFilter` en el cast), `DeleteSavedView`, `CreateTag` —
    mismos flows de AC-7 (los helpers/`LoadTagsAsync`/
    `LoadSavedViewsAsync` espejo).
  - `RefreshAsync` (MODIFY del existente): el request pasa de
    `new GetTransactionsQueryRequest(Page, 20)` a `new
    GetTransactionsQueryRequest(Page, 20, CurrentFilter.CategoryId,
    CurrentFilter.DateFrom, CurrentFilter.DateTo, SelectedTagIds)` +
    `PageSummary` en el éxito — resto del cuerpo intacto.
  - Ctor (MODIFY): + guards de los 8 deps nuevos + `CurrentFilter =
    new ExpenseFilter { Page = 1, PageSize = 20 };` + fire-and-forget
    `_ = LoadTagsAsync(); _ = LoadSavedViewsAsync();`.

- **GIVEN** `FilterCategoryId = cat` + `FilterDateFrom = d1` y
  `ApplyFilterCommand`.
- **WHEN** se ejecuta.
- **THEN** `LastExpenseFilter.CategoryId == cat`, `DateFrom == d1`,
  `Page == 1` (los tests de §7 usan `LastExpenseFilter`, AC-6).
- **GIVEN** todos los tests existentes de `ExpensesViewModelTests`
  (10, verificado).
- **WHEN** `dotnet test` tras la extensión.
- **THEN** siguen pasando sin edición de sus asserts existentes
  (regresión — el VM crece sin romper el contrato previo).

### AC-10 — `ExpensesView.axaml` extendido (MODIFY — mayor, diseño BOLD) (D-6, §4)

- [ ] `src/.../UI/Views/ExpensesView.axaml` (MODIFY): reestructurar
      al layout de 2 regiones de AC-8 (riel de filtros ~280px +
      ledger) con el MISMO contrato de bindings existente (form
      RegisterExpense + lista + paginación conservan sus bindings
      actuales) + los nuevos bindings de AC-9 (filtros, vistas
      guardadas, tags, botones de fila, `StatusMessage`,
      `PageSummary` en lugar del "Página X de Y" hardcodeado).
- [ ] `x:DataType="vm:ExpensesViewModel"` (ya presente) se conserva;
      todo DataTemplate nuevo con `x:DataType` correcto.
- [ ] Los watermarks hardcodeados ("Seleccione...",
      "Descripción del gasto", "Nota opcional") migran a keys
      localizadas (AC-24) — las secciones se reescriben de todas
      formas en el redesign.
- [ ] `UserControl.Styles` → `StyleInclude` de `LedgerStyles.axaml` +
      `Motion.axaml` (D-12).
- [ ] Los montos de gasto en `TerracottaBrush` right-aligned Spline
      Sans Mono (semántica §4.2).

- **GIVEN** la app compilada.
- **WHEN** se navega a "Gastos".
- **THEN** el riel de filtros renderiza (Category/Tags/fechas +
  vistas guardadas); aplicar un filtro refresca el ledger; "Página X
  de Y" ahora sale de `PageSummary` localizado.
- **GIVEN** `dotnet build`.
- **WHEN** compila el XAML.
- **THEN** 0 warnings de compiled bindings.

### AC-11 — Paginación en ambas vistas (D-11, Q-4)

- [ ] Ambas vistas mantienen controles de paginación
      (`PreviousPage`/`NextPage` con `CanGo*` + `PageSummary`
      localizado).
- [ ] `ApplyFilter`/`ApplySavedView`/`ClearFilter` resetean `Page =
      1` antes del refresh (AC-7/AC-9).
- [ ] PageSize se mantiene en **20** (constante del mirror, gratuito
      con `PagedResult`).

- **GIVEN** `Page = 3` (tras 2 `NextPage`).
- **WHEN** `ApplyFilterCommand`.
- **THEN** `Page == 1` y el refresh usó `Page = 1`
  (`LastIncomeFilter.Page == 1` / `LastExpenseFilter.Page == 1`).

### AC-12 — Flujos de filtro (Apply + Clear) en ambas vistas (D-2, AC-3)

- [ ] "Aplicar filtros" → construye `CurrentFilter` desde las props
      observables → `Page = 1` → refresh (AC-7/AC-9).
- [ ] "Limpiar filtros" → resetea las props de filtro + selección de
      tags + `CurrentFilter` default + `Page = 1` → refresh.
- [ ] El orden del resultado (`Date desc, CreatedAt desc`) lo
      garantiza el repo vía `GetByFilterAsync` — el VM **no reordena**
      (D-2).
- [ ] TagIds con semántica OR (F-007) — la selección de N tags
      devuelve entidades con **al menos uno** de los tags.

- **GIVEN** una lista cargada y filtros que no matchean nada.
- **WHEN** `ApplyFilterCommand`.
- **THEN** `Incomes`/`Expenses` queda vacía, `TotalPages == 0`, sin
  `FormError` (resultado vacío ≠ error).
- **GIVEN** filtros activos.
- **WHEN** `ClearFilterCommand`.
- **THEN** `LastFilter` del fake muestra `CategoryId/DateFrom/DateTo/
  TagIds == null` y `Page == 1`.

### AC-13 — Vistas guardadas: UI + flujos en ambas vistas (D-7, D-9, Q-1)

- [ ] Combobox poblado al cargar la vista (`LoadSavedViewsAsync` con
      `GetSavedViewsQuery(EntityType)` — solo vistas del tipo
      correspondiente).
- [ ] **Guardar**: `NewViewName` inline (botón deshabilitado si
      vacío) → `SaveCurrentFilterCommand(name, EntityType,
      CurrentFilter)` → éxito: refresca lista de vistas +
      `StatusMessage`; `DuplicateName`/`Validation` → error visible
      (`StatusMessage`), sin excepción (AC-12 del proposal).
- [ ] **Aplicar** (auto al seleccionar en el combobox):
      `ApplySavedViewUseCase` → cast `ApplySavedViewResult` →
      sincroniza props de filtro observables + `CurrentFilter` con
      `Page = 1` → refresh; `IncompatibleVersion`/`CorruptedFilter`/
      `NotFound` → `StatusMessage` de error, **nunca success
      silencioso** (AC-13 del proposal).
- [ ] **Eliminar**: confirmación 2 pasos (`PendingDeleteViewId`) →
      `DeleteSavedViewUseCase` → refresca vistas + `SelectedSavedView
      = null` + `StatusMessage`; errores propagados visibles (AC-14
      del proposal).

- **GIVEN** `NewViewName = "  "` (whitespace).
- **WHEN** `SaveViewCommand`.
- **THEN** no se llama al use case (`Assert.Empty(AddedSavedViews)`);
  `StatusMessage` muestra el error localizado.
- **GIVEN** `AddResultOverride = Failure("DuplicateName", ...)`.
- **WHEN** `SaveViewCommand` con nombre válido.
- **THEN** `StatusMessage` con error; sin excepción; `SavedViews` sin
  cambios.
- **GIVEN** vista seleccionada cuyo `FilterJson` es corrupto (vía
      fake `GetByIdResultOverride` con vista fabricada).
- **WHEN** `SelectedSavedView = view`.
- **THEN** `StatusMessage` de error; el `CurrentFilter` **no** cambia
  (no se aplica un filtro corrupto); sin excepción.

### AC-14 — Botones Editar/Eliminar por fila en ambas vistas (Q-2, D-7)

- [ ] Cada fila del ledger tiene botones "Editar" y "Eliminar"
      (texto-acción ámbar, §4.3).
- [ ] **Editar** → `BeginEdit(dto)` carga el form pre-poblado +
      `IsEditMode = true` → "Guardar cambios" ejecuta
      `UpdateIncome`/`UpdateExpense` → éxito: form se limpia +
      `IsEditMode = false` + refresh + `StatusMessage`; "Cancelar"
      aborta sin tocar datos.
- [ ] **Eliminar** → 2 pasos (`PendingDelete*Id`) →
      `DeleteTransactionCommand(id, TransactionType.Income/Expense)`
      → éxito: refresh + `StatusMessage`; `NotFound` → `FormError`.
- [ ] Los 6 use cases CRUD de F-005/F-003 quedan **consumidos por
      UI** (fin del "sin consumidor").

- **GIVEN** una fila existente y `BeginEditCommand.ExecuteAsync(dto)`.
- **WHEN** se inspecciona el form.
- **THEN** `Date == dto.Date`, `Amount == dto.Amount`,
  `SelectedCategoryId == dto.CategoryId`, `IsEditMode == true`,
  `EditableId == dto.Id`.
- **GIVEN** `IsEditMode = true` con `EditableId` seteado.
- **WHEN** `UpdateIncomeCommand` con form válido (fake repo exitoso).
- **THEN** `IsEditMode == false`, `EditableId == null`, form limpio,
  `StatusMessage` de éxito, refresh ejecutado.
- **GIVEN** `RemoveResultOverride = Failure("NotFound", "...")` (fake
      income repo).
- **WHEN** delete confirmado (2ª pulsación).
- **THEN** `FormError` seteado; `PendingDeleteIncomeId == null`
  (reset); sin excepción.

### AC-15 — Creación de tags inline en ambos rieles (Q-5, D-5)

- [ ] Sección "Nueva etiqueta" en ambos rieles: `NewTagName` (TextBox)
      + `NewTagColor` (TextBox opcional, placeholder localizado) +
      botón "Crear etiqueta" + `TagCreationError` inline.
- [ ] Flujo: `CreateTagCommand(NewTagName, NewTagColor)` →
      `CreateTagUseCase` → éxito: limpia inputs + recarga
      `AvailableTags` (preservando selección) + `StatusMessage`;
      fallo: `TagCreationError` visible (`"DuplicateName"` → mensaje
      específico; otro → `FormErrorMessage`).
- [ ] Estado vacío del picker visible cuando no hay tags (mensaje
      localizado `NoTagsMessage` + la sección de creación es la vía
      de salida).

- **GIVEN** `AvailableTags` vacía.
- **WHEN** `CreateTagCommand.ExecuteAsync(null)` con
  `NewTagName = "Transporte"`.
- **THEN** `AddedTags` (fake) tiene 1 `Tag` "Transporte";
  `AvailableTags` repoblada con 1 item; `NewTagName == null`;
  `TagCreationError == null`.
- **GIVEN** `AddResultOverride = Failure("DuplicateName", "...")` y
      tag "Transporte" ya existente.
- **WHEN** `CreateTagCommand` con `NewTagName = "Transporte"`.
- **THEN** `TagCreationError` con mensaje localizado; `AvailableTags`
  sin duplicados; sin excepción.

### AC-16 — `RouteNames.Incomes` + entrada en `NavigationService` (MODIFY ×2) (D-9)

- [ ] `src/.../UI/Navigation/RouteNames.cs` (MODIFY): AÑADIR después
      de `Expenses`:

```csharp
/// <summary>
/// The incomes route (consumed by F-008 of Phase 4).
/// </summary>
public const string Incomes = "Incomes";
```

- [ ] `src/.../UI/Navigation/NavigationService.cs` (MODIFY): AÑADIR
      al diccionario del ctor (línea 31, tras la entrada de
      `Expenses`): `{ RouteNames.Incomes, typeof(IncomeViewModel) }`.

- **GIVEN** el shell construido.
- **WHEN** `NavigateTo(RouteNames.Incomes)`.
- **THEN** `CurrentViewModel` es `IncomeViewModel` (resuelto del
  proveedor DI); sin `InvalidOperationException` de ruta desconocida.

### AC-17 — `ShellViewModel.MenuItems` + key `SidebarIncomes` (MODIFY) (D-9, D-10)

- [ ] `src/.../UI/ViewModels/ShellViewModel.cs` (MODIFY): AÑADIR al
      collection-initializer de `MenuItems` (tras la entrada de
      Expenses):

```csharp
new RouteMenuItem(RouteNames.Incomes, _localizer["SidebarIncomes"]),
```

- [ ] Key `SidebarIncomes` presente en los 3 resx (AC-24) —
      `MainWindow.axaml` NO se toca (el sidebar es data-driven).

- **GIVEN** la app iniciada.
- **WHEN** se observa el sidebar.
- **THEN** 3 items: Dashboard, Gastos, **Ingresos** (este último con
  el texto localizado de `SidebarIncomes`); el click navega a
  `IncomeView` vía `NavigateTo`.

### AC-18 — `App.axaml` DataTemplate dual para `IncomeViewModel` (MODIFY) (D-9, D-12)

- [ ] `src/.../UI/App.axaml` (MODIFY): AÑADIR al
      `Application.DataTemplates` (tras el de `ExpensesViewModel`,
      espejo del patrón dual verificado):

```xml
<DataTemplate DataType="{x:Type vm:IncomeViewModel}"
              x:DataType="vm:IncomeViewModel">
    <views:IncomeView />
</DataTemplate>
```

- [ ] (Design pass, AC-23): el mismo archivo gana
      `RequestedThemeVariant="Dark"` + `<ResourceInclude Source=
      "avares://ControlDeFinanzasIA.UI/Styles/Tokens.axaml"/>` en
      `Application.Resources`.

- **GIVEN** `CurrentViewModel` es `IncomeViewModel`.
- **WHEN** el `ContentControl` del shell lo renderiza.
- **THEN** el DataTemplate dual matchea y despliega `IncomeView`
  (compiled bindings activos por el `x:DataType`).

### AC-19 — `Bootstrap` DI para los componentes nuevos (MODIFY) (D-5, D-6)

- [ ] `src/.../UI/Composition/Bootstrap.cs` (MODIFY): AÑADIR 3 usings
      (orden alfabético con los existentes):
      `ControlDeFinanzasIA.Application.UseCases.Catalog.CreateTag`,
      `ControlDeFinanzasIA.Application.UseCases.Incomes.GetIncomes`,
      `ControlDeFinanzasIA.Application.UseCases.SavedViews.DeleteSavedView`.
- [ ] AÑADIR 5 registros (junto a los grupos existentes, espejo de
      estilo):

```csharp
services.AddTransient<IncomeViewModel>();
services.AddTransient<GetIncomesQuery>();
services.AddTransient<DeleteSavedViewUseCase>();
services.AddTransient<CreateTagUseCase>();
services.AddTransient<IValidator<CreateTagCommand>, CreateTagValidator>();
```

- [ ] Remarks del método `ConfigureServices` actualizados: menciona
      el query de ingresos, el borrado de vistas guardadas, la
      creación de tags (+ validator) y `IncomeViewModel` (espejo del
      párrafo existente de SavedViews, líneas 48-56).
- [ ] `ApplyMigrations`/`AddPersistence` **sin cambios** (cero
      Infrastructure en este feature).

- **GIVEN** el composition root completo.
- **WHEN** el proveedor resuelve `IncomeViewModel`.
- **THEN** sus 14 deps se resuelven (repositorios Scoped vía
  `AddPersistence`, `IClock` singleton, queries/use cases/validators
  Transient, `ILogger<>`, localizer).

### AC-20 — `Tokens.axaml` (NEW — design tokens ResourceDictionary) (D-12, §4.2)

- [ ] `src/.../UI/Styles/Tokens.axaml` (NEW):
      `<ResourceDictionary>` (sin code-behind) con:
  - **Colores** (§4.2): `InkBrush` `#16181D`, `SurfaceBrush`
    `#1F232B`, `HairlineBrush` `#31363F`, `PaperBrush` `#E8E4D9`,
    `PaperDimBrush` (~`#9BA0A8`, ajustable en design), `AmberBrush`
    `#E8A33D`, `SageBrush` `#7FB069`, `TerracottaBrush` `#D96459` —
    como `SolidColorBrush` resources.
  - **Fuentes** (§4.1): `FontDisplay` / `FontBody` / `FontMono` como
    `<FontFamily>` resources con URIs
    `avares://ControlDeFinanzasIA.UI/Assets/Fonts#Fraunces` /
    `#Instrument Sans` / `#Spline Sans Mono`.
  - **Spacing**: `Space1`..`Space8` (4/8/12/16/20/24/28/32 —
    `Thickness` o `double`, a elección del design).
  - **Motion**: `MotionFast` (`0:0:0.15`), `MotionNormal`
    (`0:0:0.2`), `MotionSlow` (`0:0:0.25`) como `TimeSpan` resources.
  - **Radii**: `RadiusSmall` (4), `RadiusMedium` (6).
- [ ] Incluido desde `App.axaml` vía `<ResourceInclude>` en
      `Application.Resources` (D-12 — recursos inertes para el shell,
      consumidos por las 2 vistas).

- **GIVEN** la app iniciada.
- **WHEN** `IncomeView`/`ExpensesView` resuelven
  `{StaticResource AmberBrush}`/`{StaticResource FontMono}`.
- **THEN** los recursos resuelven (sin
  `ResourceNotFoundException`/fallback a default) — verificado en el
  smoke manual AC-28.

### AC-21 — `LedgerStyles.axaml` (NEW — estilos de controles ledger) (D-12, §4.6)

- [ ] `src/.../UI/Styles/LedgerStyles.axaml` (NEW): `<Styles>` con
      estilos **con classes explícitas** (no implícitos globales —
      no colisionan con FluentTheme ni re-stylean el shell):
  - `.ledger-btn` (Button): transparente, texto `PaperBrush`,
    hover → texto/subrayado `AmberBrush`, padding horizontal
    `Space3`, sin borde (flat), press scale 0.98.
  - `.ledger-btn-primary` (CTA "Registrar"/"Aplicar"): borde
    `AmberBrush` 1px, hover → fondo ámbar ~12%.
  - `.ledger-btn-danger` (Eliminar): texto `TerracottaBrush`.
  - `.ledger-input` (TextBox/ComboBox/DatePicker): fondo
    `InkBrush`/`SurfaceBrush`, foreground `PaperBrush`, caret/foco
    `AmberBrush`, underline `HairlineBrush` (mínimo), corner radius
    `RadiusSmall`, watermark `PaperDimBrush`.
  - `.ledger-row` (ListBoxItem/row container): sin chrome de
    selección Fluent default; hover → wash de fondo + tick ámbar
    (§4.4); hairline inferior `HairlineBrush`; altura ~40-44px.
  - `.ledger-combo` (ComboBox): flat, popup `SurfaceBrush`, ítems
    hover ámbar.
  - `.ledger-section` (secciones del riel): título Instrument Sans
    500/12 `PaperDimBrush` uppercase + hairline.
  - `.ledger-amount` (TextBlock de monto): `FontFamily
    {StaticResource FontMono}`, right-aligned; variantes
    `.income` (`SageBrush`) / `.expense` (`TerracottaBrush`).
  - `.ledger-title` (título de vista): `FontDisplay` weight 600
    tamaño 28 `PaperBrush`.
- [ ] Incluido **a nivel `UserControl.Styles`** en `IncomeView` y
      `ExpensesView` (D-12) — NO en `App.axaml`.
- [ ] Re-mapeo de FluentTheme: los estilos referencian recursos de
      FluentTheme solo donde haga falta (p. ej.
      `ComboBoxDropDownBackground` → `SurfaceBrush`) — overrides
      puntuales, sin redefinir el theme entero.

- **GIVEN** ambas vistas con `LedgerStyles` incluido.
- **WHEN** se renderizan.
- **THEN** botones/inputs/filas muestran el look "Libro Mayor"
  (verificación visual AC-28 — screenshots); el shell/dashboard
  **no** cambian (siguen FluentTheme default Dark).

### AC-22 — `Motion.axaml` (NEW — animaciones Avalonia) (D-12, §4.4)

- [ ] `src/.../UI/Styles/Motion.axaml` (NEW): `<Styles>` con las
      animaciones de §4.4:
  - **RowReveal**: opacity 0→1 + TranslateY 8→0, 200ms easeOutCubic
    (`CubicEaseOut`), aplicado a `.ledger-row` — stagger vía
    selectores `:nth-child(1..~10)` con `Delay = n × 30ms` (cap ~10;
    fallback sancionado: reveal uniforme del contenedor si el
    stagger nth-child es impracticable — JAMÁS code-behind).
  - **AmberTick**: scale 0→1 ~150ms easeOutBack (`SpringEase` o
    equivalente) en el indicador ámbar de hover de fila.
  - **RailSectionTransition**: `Transitions` (Width/Opacity ~250ms)
    en las secciones del riel.
  - **ButtonPress/Underline**: press scale 0.98 ~100ms; underline
    ámbar que crece 150ms en hover del CTA.
- [ ] Incluido a nivel `UserControl.Styles` en ambas vistas (tras
      `LedgerStyles`).
- [ ] **Sin code-behind**: 100% `Animation`/`Transitions` de Avalonia
      en XAML (restricción constitucional MVVM estricto).

- **GIVEN** la vista recién cargada con 10+ filas.
- **WHEN** el `ItemsControl` se pobló.
- **THEN** las filas entran con reveal escalonado (verificación
  visual AC-28); sin jank perceptible a 60fps en datasets de página
  (20 filas).

### AC-23 — Fuentes OFL embebidas + tema Dark (NEW assets + MODIFY csproj/App.axaml) (D-12, §4.1)

- [ ] `src/.../UI/Assets/Fonts/` (NEW — 3 TTF variables OFL +
      licencias):
      `Fraunces-Variable.ttf`, `InstrumentSans-Variable.ttf`,
      `SplineSansMono-Variable.ttf` (+ `OFL-Fraunces.txt`,
      `OFL-InstrumentSans.txt`, `OFL-SplineSansMono.txt` — las
      licencias OFL acompañan a los binarios). Los `.ttf` se
      descargan en apply (Google Fonts / upstream OFL); **si una
      familia no estuviera disponible como TTF**, la sustitución
      requiere aprobación del usuario (misma familia tipográfica de
      carácter, no Inter/Roboto/Arial).
- [ ] `src/.../UI/ControlDeFinanzasIA.UI.csproj` (MODIFY — **única**
      edición sancionada): AÑADIR `<ItemGroup>` con
      `<AvaloniaResource Include="Assets\Fonts\**" />` (verificado en
      docs Avalonia — formato soportado: ttf/otf; **woff2 NO**).
      CPM y el resto del csproj **intactos**.
- [ ] `App.axaml` (MODIFY — design pass): `RequestedThemeVariant`
      pasa de `"Default"` a **`"Dark"`** (tema único, Q-3).
- [ ] `Tokens.axaml` expone los 3 `<FontFamily>` (AC-20) — las
      referencias `avares://ControlDeFinanzasIA.UI/Assets/Fonts#…`
      resuelven contra los binarios embebidos.

- **GIVEN** la app empaquetada en otra máquina (sin fuentes
  instaladas).
- **WHEN** se abre `IncomeView`.
- **THEN** Fraunces/Instrument Sans/Spline Sans Mono renderizan desde
  los assets embebidos (sin dependencia del host) — verificación AC-28.

### AC-24 — Localización: ~28 keys nuevas ×3 resx + migración de hardcodeados (MODIFY ×3) (D-10)

- [ ] `src/.../UI/Resources/Strings.resx` (neutral = EN),
      `Strings.es-DO.resx`, `Strings.Designer.cs` (regenerado) —
      **cada key en los 3 archivos** (convención verificada). Keys
      nuevas (valores es-DO en la tabla; neutral = inglés):

| Key | Neutral (EN) | es-DO |
| --- | --- | --- |
| `SidebarIncomes` | Incomes | Ingresos |
| `IncomesTitle` | Income ledger | Ingresos |
| `EditButton` | Edit | Editar |
| `DeleteButton` | Delete | Eliminar |
| `ConfirmDeleteButton` | Confirm | Confirmar |
| `UpdateButton` | Save changes | Guardar cambios |
| `CancelEditButton` | Cancel | Cancelar |
| `EditFormTitle` | Edit entry | Editar asiento |
| `FiltersSectionTitle` | Filters | Filtros |
| `FilterTagsLabel` | Tags | Etiquetas |
| `FilterDateFromLabel` | From | Desde |
| `FilterDateToLabel` | To | Hasta |
| `ApplyFilterButton` | Apply filters | Aplicar filtros |
| `ClearFilterButton` | Clear filters | Limpiar filtros |
| `SavedViewsSectionTitle` | Saved views | Vistas guardadas |
| `SavedViewsPlaceholder` | Select a view… | Seleccione una vista… |
| `NewViewNameWatermark` | View name | Nombre de la vista |
| `SaveViewButton` | Save view | Guardar vista |
| `DeleteViewButton` | Delete view | Eliminar vista |
| `CreateTagSectionTitle` | New tag | Nueva etiqueta |
| `TagNameWatermark` | Tag name | Nombre de la etiqueta |
| `TagColorWatermark` | Color (optional) | Color (opcional) |
| `CreateTagButton` | Create tag | Crear etiqueta |
| `NoTagsMessage` | No tags yet — create one below. | Sin etiquetas todavía — crea una abajo. |
| `SelectCategoryWatermark` | Select… | Seleccione… |
| `DescriptionWatermark` | Description | Descripción |
| `NoteWatermark` | Optional note | Nota opcional |
| `EmptyIncomesMessage` | No income registered yet. | No hay ingresos registrados todavía. |
| `IncomeRegisteredMessage` | Income registered successfully. | Ingreso registrado correctamente. |
| `IncomeUpdatedMessage` | Income updated successfully. | Ingreso actualizado correctamente. |
| `ExpenseUpdatedMessage` | Expense updated successfully. | Gasto actualizado correctamente. |
| `TransactionDeletedMessage` | Transaction deleted. | Transacción eliminada. |
| `ViewSavedMessage` | View saved. | Vista guardada. |
| `ViewDeletedMessage` | View deleted. | Vista eliminada. |
| `ViewNameRequiredMessage` | The view name is required. | El nombre de la vista es obligatorio. |
| `TagNameRequiredMessage` | The tag name is required. | El nombre de la etiqueta es obligatorio. |
| `DuplicateTagMessage` | A tag with that name already exists. | Ya existe una etiqueta con ese nombre. |
| `OperationCancelledMessage` | Operation cancelled. | Operación cancelada. |

- [ ] **Reuso** (ya existen, verificado): `DateLabel`, `AmountLabel`,
      `CurrencyLabel`, `CategoryLabel`, `DescriptionLabel`,
      `NoteLabel`, `RegisterButton`, `PreviousPageButton`,
      `NextPageButton`, `FormErrorMessage`, `EmptyExpensesMessage`,
      `RegistrationSuccessMessage`, **`PageOfPagesFormat`** (existe
      sin uso — pasa a usarse vía `PageSummary`).
- [ ] **Migración de hardcodeados** en las secciones reescritas de
      `ExpensesView` (y espejo en IncomeView): "Seleccione..." →
      `SelectCategoryWatermark`; "Descripción del gasto" →
      `DescriptionWatermark`; "Nota opcional" → `NoteWatermark`;
      "Página X de Y" (Runs hardcodeados) → `PageSummary` con
      `PageOfPagesFormat`.
- [ ] `Strings.Designer.cs` regenerado (ResXFileCodeGenerator,
      commiteado — convención existente).

- **GIVEN** cultura `es-DO`.
- **WHEN** la UI renderiza las vistas nuevas.
- **THEN** todos los strings visibles salen de los resx (cero
  literales en XAML/VM); con cultura neutral (en) los mismos strings
  en inglés.

### AC-25 — Tests unit de ViewModels (NEW + MODIFY) (AC-7, AC-9)

- [ ] `tests/.../Tests.Unit/UI/IncomeViewModelTests.cs` (NEW) —
      **≥6 requeridos** (§7.4, IV-1..IV-9): patrón espejo
      `ExpensesViewModelTests` — helper `CreateViewModel(...)` con
      fakes + use cases/query **reales** + validators reales +
      `NullLogger<>` + `FakeStringLocalizer<Strings>`; aserciones
      puras `Assert.*`. Tests: `Constructor_InitializesDefaults_*
      AndLoadsCatalogs`, `Refresh_PopulatesIncomesAndPageMetadata`,
      `RegisterIncome_ValidForm_CallsUseCase_AndRefreshesList`,
      `ApplyFilter_BuildsFilterFromProps_ResetsPageAndReloads`,
      `ApplySavedView_UpdatesFilterProps_AndResetsPage`,
      `SaveView_WithValidName_CallsUseCase_AndReloadsViews`,
      `DeleteIncome_TwoStepConfirm_DeletesOnSecondInvocation`,
      `DeleteIncome_OnRepositoryError_ShowsFormError` (+ error paths
      de validation/register, previstos en §7.4).
- [ ] `tests/.../Tests.Unit/UI/ExpensesViewModelTests.cs` (MODIFY) —
      **≥4 nuevos requeridos** (§7.5, EV-1..EV-6), mismo patrón: el
      helper `CreateViewModel` se extiende con los 8 deps nuevos
      (fakes + use cases reales); los 10 tests existentes siguen
      pasando **sin edición de sus asserts** (regresión). Tests
      nuevos: `ApplyFilter_BuildsExpenseFilter_FromObservableProps`,
      `SaveView_WithDuplicateName_ShowsStatusError_WithoutException`,
      `ApplySavedView_DeserializesFilter_SyncsPropsAndResetsPage`,
      `DeleteSavedView_TwoStepConfirm_RemovesViewAndRefreshes` (+
      `CreateTag_RefreshesAvailableTags`,
      `DeleteExpense_TwoStepConfirm_CallsTransactionDelete`).

- **GIVEN** los tests implementados según §7.4-§7.5.
- **WHEN** `dotnet test -c Debug --no-build --nologo --filter "FullyQualifiedName~IncomeViewModel|FullyQualifiedName~ExpensesViewModel"`.
- **THEN** todos pasan (10 existentes de Expenses + ≥15 nuevos).

### AC-26 — Tests unit de orquestadores nuevos + regresión del query de gastos (NEW ×3 + MODIFY ×1)

- [ ] `tests/.../Tests.Unit/Application/Incomes/GetIncomes/GetIncomesQueryTests.cs`
      (NEW) — **≥3 requeridos** (§7.1, GI-1..GI-5): happy path con
      DTOs + metadatos de página; `InvalidFilter` (Theory
      Page/PageSize/DateFrom>DateTo/TagIds con Guid.Empty — repo sin
      llamadas); `RepositoryError` con `SetThrowOnGetFilter`;
      passthrough de filtros 1:1 vía `LastIncomeFilter`; `null`
      request → `ArgumentNullException`.
- [ ] `tests/.../Tests.Unit/Application/SavedViews/DeleteSavedView/DeleteSavedViewUseCaseTests.cs`
      (NEW) — **≥2 requeridos** (§7.2, DS-1..DS-4): happy path
      remove + propagación; `Guid.Empty` → `"Validation"` sin llamar
      al repo; `RemoveResultOverride` → propagación verbatim
      (`"NotFound"`); `null` command → `ArgumentNullException`.
- [ ] `tests/.../Tests.Unit/Application/Catalog/CreateTag/CreateTagUseCaseTests.cs`
      (NEW) — **≥3 requeridos** (§7.3, CT-1..CT-5): happy path
      (persiste con timestamp del clock); validación (Theory
      vacío/whitespace/>50/color >9 con details); `DuplicateName`
      propagado; `DomainInvariant` (defense in depth — name con
      caracteres que el validator deja pasar pero el dominio
      rechaza, p. ej. vía mock-imposible: se cubre con el path
      `Color` >9 ya atrapado por validator — el test de
      `DomainInvariant` usa `Tag.Create` boundary documentado en
      §7.3); `null` command.
- [ ] `tests/.../Tests.Unit/Application/Expenses/GetTransactions/GetTransactionsQueryTests.cs`
      (MODIFY) — **≥3 nuevos** (§7.6, RE-1..RE-3): passthrough de
      filtros vía `LastExpenseFilter`; defaults-null preservan
      comportamiento pre-F-008; `DateFrom > DateTo` →
      `"InvalidFilter"`. Los tests existentes siguen pasando sin
      edición (regresión).

- **GIVEN** los tests implementados según §7.1-§7.3 y §7.6.
- **WHEN** `dotnet test -c Debug --no-build --nologo --filter "FullyQualifiedName~GetIncomesQuery|FullyQualifiedName~DeleteSavedViewUseCase|FullyQualifiedName~CreateTagUseCase|FullyQualifiedName~GetTransactionsQuery"`.
- **THEN** todos pasan (≈14-17 tests nuevos incluida la regresión).

### AC-27 — Gates de verificación + scope creep

- [ ] `dotnet build -c Release --nologo` → 0 warnings, 0 errors.
- [ ] `dotnet build -c Debug --nologo` → 0 warnings, 0 errors —
      **incluye la compilación XAML de Avalonia**: todo
      DataTemplate/binding compila con `x:DataType` correcto (0
      avisos de compiled bindings; `AvaloniaUseCompiledBindingsByDefault=true`).
- [ ] `dotnet test -c Debug --no-build --nologo` (suite completa) →
      verde + **únicamente** el 1 fallo pre-existing de `Money`
      (documentado; no regresión). Línea base: suite post-4-007 +
      ~29 nuevos (§7).
- [ ] `dotnet test -c Debug --no-build --nologo --filter
      "FullyQualifiedName~ArchitectureTests"` → 11/11 (R-1..R-5) —
      los archivos nuevos no violan: VMs referencian Application +
      Domain (R-4); Bootstrap es composition root sancionado.
- [ ] `dotnet format style --verify-no-changes` → exit 0.
- [ ] `dotnet list src/ControlDeFinanzasIA.Domain package` sigue
      vacío (R-1); `Directory.Packages.props` **sin cambios** (cero
      paquetes nuevos).
- [ ] **Sin scope creep**: cero cambios en Domain (entidades, filtros
      — solo se **consumen**, `Money`, `Currency`, `Result`,
      `PagedResult`, `IClock`), cero cambios en Infrastructure
      (repos, configuraciones, migraciones, `FilterJsonSerializer`,
      `SeedData`, `SavedViewRepository`), cero modificaciones a use
      cases existentes (Register/Update/Delete/Save/Apply/GetSavedViews
      — solo se consumen), `DashboardView`/`DashboardViewModel`/
      `MainWindow.axaml` **intactos** (Q-3), fakes existentes solo
      **extendidos** (AC-6), `Avalonia.Headless` ya presente (cero
      UI tests automatizados en este feature — nota §7.7).
- [ ] La única edición de `.csproj` es el ItemGroup
      `AvaloniaResource` de fuentes (AC-23); ningún otro `.csproj`
      tocado.
- [ ] NO FluentAssertions, NO mocking library, NO paquetes NuGet
      nuevos.

- **GIVEN** el diff completo de F-008.
- **WHEN** se inspecciona contra §6 (Áreas afectadas).
- **THEN** no hay archivos fuera de la lista; los archivos
  pre-existentes editados son exactamente los sancionados.

### AC-28 — Verificación manual + screenshots + forecast de PRs encadenados

- [ ] **Smoke manual** (`dotnet run --project
      src/ControlDeFinanzasIA.UI`):
  1. Sidebar muestra "Ingresos" → navega → `IncomeView` renderiza el
     diseño "Libro Mayor" (título Fraunces sobre regla ámbar, montos
     Spline Sans Mono right-aligned sage).
  2. Registra un ingreso → aparece en el ledger paginado (orden Date
     desc) → "Editar" pre-pobla el form → "Guardar cambios" refresca
     → "Eliminar" (2 pasos) lo quita.
  3. En Gastos: aplica filtro combinado (categoría + 2 tags + rango)
     → el ledger refresca; guarda la vista → aparece en el combobox
     → aplica (selección) → restaura el filtro (props del riel
     sincronizadas) → eliminar (2 pasos) la quita.
  4. Crea un tag inline → queda disponible en el picker; el estado
     vacío del picker muestra el mensaje guía.
  5. Shell/dashboard: tema Dark, FluentTheme base, **sin** estilos
     Libro Mayor (Q-3).
  6. **Screenshots** de ambos views (título, riel, ledger, hover
     tick, stagger) adjuntos al verify-report.
- [ ] **Forecast de PRs (guard de review, §E de sdd-phase-common):**
      ~26-30 archivos, **~1,700-2,300 líneas cambiadas** → excede por
      ~4-5× el budget de 400. `sdd-tasks` DEBE formalizar **4 PRs
      stacked-to-main** (§9 del proposal):
      **PR-1** Application + test infra + DI (~500-600 líneas);
      **PR-2** `IncomeViewModel` + wiring + `IncomeView` funcional +
      keys resx mínimas (~550-650); **PR-3** extensión de Expenses
      (VM + XAML + tests + keys de filtro/vistas) (~450-550);
      **PR-4** design pass (fuentes + tokens + estilos + motion +
      Dark + migración de hardcodeados) (~350-450). Líneas de guard:
      `Decision needed before apply: Yes`, `Chained PRs recommended:
      Yes`, `400-line budget risk: High`.

- **GIVEN** los 6 puntos del smoke.
- **WHEN** se ejecutan end-to-end.
- **THEN** todos completan sin excepciones ni estados muertos; los
  screenshots evidencian la identidad BOLD (no estética genérica de
  IA); Phase 4 cumple los criterios §12 del plan.

---

## 6. Áreas afectadas

Base: `C:\Users\diego\Desktop\ControlDeFinanzasIA\`

| # | Archivo | Acción | AC | Descripción |
| --- | --- | --- | --- | --- |
| 1 | `src\ControlDeFinanzasIA.Application\DTOs\IncomeDto.cs` | **Create** | AC-1 | Record 10 props (espejo `ExpenseDto` + `CategoryId` nullable + `Source`; sin Tags) |
| 2 | `src\...Application\DTOs\IncomeExtensions.cs` | **Create** | AC-1 | `ToDto()` espejo `ExpenseExtensions` |
| 3 | `src\...Application\UseCases\Incomes\GetIncomes\GetIncomesQueryRequest.cs` | **Create** | AC-2 | Record plano 6 params (paginación + 5 filtros opcionales) |
| 4 | `src\...Application\UseCases\Incomes\GetIncomes\GetIncomesQuery.cs` | **Create** | AC-2 | Clase query 2 deps, validación manual, mapeo 1:1, `Value = PagedResult<IncomeDto>` |
| 5 | `src\...Application\UseCases\Expenses\GetTransactions\GetTransactionsQueryRequest.cs` | **Modify** | AC-3 | +5 params opcionales de filtro + doc-refresh |
| 6 | `src\...Application\UseCases\Expenses\GetTransactions\GetTransactionsQuery.cs` | **Modify** | AC-3 | Mapeo del request (fin de nulls hardcodeados) + guards de coherencia |
| 7 | `src\...Application\UseCases\SavedViews\DeleteSavedView\DeleteSavedViewCommand.cs` | **Create** | AC-4 | Record `(Guid SavedViewId)` — paga D-8 de F-006 |
| 8 | `src\...Application\UseCases\SavedViews\DeleteSavedView\DeleteSavedViewUseCase.cs` | **Create** | AC-4 | Espejo `DeleteTransactionUseCase` (guard + remove + propagación) |
| 9 | `src\...Application\UseCases\Catalog\CreateTag\CreateTagCommand.cs` | **Create** | AC-5 | Record `(Name, Color?)` |
| 10 | `src\...Application\UseCases\Catalog\CreateTag\CreateTagValidator.cs` | **Create** | AC-5 | 2 reglas sync (mensajes español) |
| 11 | `src\...Application\UseCases\Catalog\CreateTag\CreateTagUseCase.cs` | **Create** | AC-5 | 4 deps (repo + clock + validator + logger) |
| 12 | `src\ControlDeFinanzasIA.UI\ViewModels\IncomeViewModel.cs` | **Create** | AC-7 | Sealed partial, 14 deps, CRUD + filtros + vistas guardadas + tags (MVVM estricto) |
| 13 | `src\...UI\ViewModels\SelectableTagItem.cs` | **Create** | AC-8 | Wrapper observable del picker de tags (~30 líneas) |
| 14 | `src\ControlDeFinanzasIA.UI\Views\IncomeView.axaml` (+ `.axaml.cs` mínimo) | **Create** | AC-8 | Vista "Libro Mayor": riel de filtros + form + ledger + paginación |
| 15 | `src\...UI\Navigation\RouteNames.cs` | **Modify** | AC-16 | + `Incomes` con XML doc |
| 16 | `src\...UI\Navigation\NavigationService.cs` | **Modify** | AC-16 | + entrada de ruta `Incomes → IncomeViewModel` |
| 17 | `src\...UI\ViewModels\ShellViewModel.cs` | **Modify** | AC-17 | + `RouteMenuItem` Ingresos en `MenuItems` |
| 18 | `src\ControlDeFinanzasIA.UI\App.axaml` | **Modify** | AC-18, AC-23 | + DataTemplate dual IncomeView + `RequestedThemeVariant="Dark"` + ResourceInclude Tokens |
| 19 | `src\...UI\ViewModels\ExpensesViewModel.cs` | **Modify (mayor)** | AC-9 | +8 deps, +filtros, +vistas guardadas, +StatusMessage, +Edit/Delete, +CreateTag, +PageSummary |
| 20 | `src\...UI\Views\ExpensesView.axaml` | **Modify (mayor)** | AC-10 | Riel de filtros + vistas guardadas + botones de fila + redesign §4 + migración de strings |
| 21 | `src\...UI\Composition\Bootstrap.cs` | **Modify** | AC-19 | +3 usings + 5 registros Transient + doc-refresh |
| 22 | `src\...UI\Resources\{Strings.resx, Strings.es-DO.resx, Strings.Designer.cs}` | **Modify ×3** | AC-24 | ~28 keys nuevas ×3 + regeneración Designer |
| 23 | `src\ControlDeFinanzasIA.UI\Assets\Fonts\*.ttf` + `OFL-*.txt` | **Create (assets)** | AC-23 | Fraunces / Instrument Sans / Spline Sans Mono (OFL, TTF variables) + licencias |
| 24 | `src\ControlDeFinanzasIA.UI\ControlDeFinanzasIA.UI.csproj` | **Modify** | AC-23 | + `<AvaloniaResource Include="Assets\Fonts\**" />` (única edición sancionada) |
| 25 | `src\...UI\Styles\Tokens.axaml` | **Create** | AC-20 | ResourceDictionary: paleta + FontFamily + spacing + motion + radii |
| 26 | `src\...UI\Styles\LedgerStyles.axaml` | **Create** | AC-21 | Estilos `.ledger-*` con classes explícitas |
| 27 | `src\...UI\Styles\Motion.axaml` | **Create** | AC-22 | RowReveal (stagger) + AmberTick + transitions |
| 28 | `tests\...Tests.Unit\TestDoubles\FakeTagRepository.cs` | **Create** | AC-6 | Fake hand-rolled (captura + overrides, espejo SavedView fake) |
| 29 | `tests\...Tests.Unit\TestDoubles\FakeIncomeRepository.cs` | **Modify** | AC-6 | + FilterResult/GetByFilterCallCount/LastIncomeFilter/SetFilterResult/SetThrowOnGetFilter |
| 30 | `tests\...Tests.Unit\TestDoubles\FakeExpenseRepository.cs` | **Modify** | AC-6 | + captura `LastExpenseFilter` |
| 31 | `tests\...Tests.Unit\Application\Incomes\GetIncomes\GetIncomesQueryTests.cs` | **Create** | AC-26 | GI-1..GI-5 (§7.1) |
| 32 | `tests\...Tests.Unit\Application\SavedViews\DeleteSavedView\DeleteSavedViewUseCaseTests.cs` | **Create** | AC-26 | DS-1..DS-4 (§7.2) |
| 33 | `tests\...Tests.Unit\Application\Catalog\CreateTag\CreateTagUseCaseTests.cs` | **Create** | AC-26 | CT-1..CT-5 (§7.3) |
| 34 | `tests\...Tests.Unit\UI\IncomeViewModelTests.cs` | **Create** | AC-25 | IV-1..IV-9 (§7.4) |
| 35 | `tests\...Tests.Unit\UI\ExpensesViewModelTests.cs` | **Modify (extensión)** | AC-25 | EV-1..EV-6 (§7.5) + helper extendido |
| 36 | `tests\...Tests.Unit\Application\Expenses\GetTransactions\GetTransactionsQueryTests.cs` | **Modify (extensión)** | AC-26 | RE-1..RE-3 (§7.6) |

**Archivos NO modificados (verificados presentes/correctos):**
Domain completo (`Income.cs`, `Expense.cs`, `Tag.cs`, `Category.cs`,
`SavedView.cs`, `Money`, `Currency`, `TransactionType`, filtros
`ExpenseFilter`/`IncomeFilter` — solo se consumen), Infrastructure
completo (`IncomeRepository`, `ExpenseRepository`, `TagRepository`,
`SavedViewRepository`, `FilterJsonSerializer`, `AppDbContext`,
migraciones 1-7, `ServiceCollectionExtensions`, `SeedData`,
`TestAppDbContextFactory`), use cases existentes
(`RegisterExpense`/`RegisterIncome`/`UpdateExpense`/`UpdateIncome`/
`DeleteTransaction`/`SaveCurrentFilter`/`ApplySavedView`/
`GetSavedViews`/`GetTransactions` — solo se consumen; nota:
`GetTransactionsQuery`/`Request` SÍ se modifican, AC-3),
`DashboardView`/`DashboardViewModel`/`MainWindow.axaml` (Q-3),
`Program.cs`, `Converters/`, `INavigationService.cs`, fakes no
listados (`FakeCategoryRepository`, `FakeSavedViewRepository`,
`FakeClock`, `FakeStringLocalizer`, `FakeNavigationService`),
`Directory.Build.props`, `Directory.Packages.props`, `stylecop.json`,
todos los `.csproj` excepto el de UI (AC-23).

**Pronóstico de entrega (guard de review):** ~26-30 archivos,
~1,700-2,300 líneas cambiadas → **excede por ~4-5× el budget de
400**. `sdd-tasks` DEBE pronosticar **chained PRs stacked-to-main**
(4 slices, AC-28) con líneas de guard obligatorias: `Decision needed
before apply: Yes`, `Chained PRs recommended: Yes`, `400-line budget
risk: High`.

---

## 7. Plan de tests

Marco: xUnit + pure `Assert.*` (`Assert.True`, `Assert.False`,
`Assert.Equal`, `Assert.Null`, `Assert.NotNull`, `Assert.Empty`,
`Assert.Single`, `Assert.Contains`, `Assert.All`, `Assert.Throws`,
`Assert.IsType`). **Sin FluentAssertions, sin mocking library** —
hand-rolled fakes (AC-6) + use cases reales (patrón
`ExpensesViewModelTests` verificado: fakes + use cases/query reales +
validators reales + `NullLogger<T>.Instance` +
`FakeStringLocalizer<Strings>`, que retorna el **key** — los asserts
de mensajes localizados comparan contra el nombre de la key).

Timestamps deterministas: `FakeClock { UtcNow = new DateTime(2026, 1,
15, 10, 0, 0, DateTimeKind.Utc), Today = new DateOnly(2026, 1, 15) }`;
`NullLogger<T>.Instance` para todos los loggers. Helpers de entidades:
`CreateIncomes(int count)` (espejo `CreateExpenses` — `Income.Create`
con `Money.Create`, `Source: null`), `CreateSavedView(name, type)`
(vía `SavedView.Create` + `FilterJsonSerializer.Serialize`).

### 7.1 Unit — `GetIncomesQueryTests` (GI-1..GI-5)

Namespace `ControlDeFinanzasIA.Tests.Unit.Application.Incomes.GetIncomes`.
Ctor del test: `FakeIncomeRepository` + `NullLogger<GetIncomesQuery>.Instance`.

| # | Test | GIVEN / WHEN / THEN |
| --- | --- | --- |
| GI-1 | `ExecuteAsync_WithValidRequest_ReturnsPagedIncomeDtos` | **GIVEN** fake `SetFilterResult(CreateIncomes(5), total: 12, page: 2, pageSize: 5)`. **WHEN** `ExecuteAsync(new GetIncomesQueryRequest(2, 5))`. **THEN** `IsSuccess`; `Assert.IsType<PagedResult<IncomeDto>>(result.Value)`; 5 items con props mapeadas por `ToDto` (spot-check: `Assert.Equal(income.Description, dto.Description)`, `Assert.Equal(income.Amount.Amount, dto.Amount)`); `Total == 12`, `Page == 2`, `PageSize == 5`, `TotalPages == 3`. |
| GI-2 | `ExecuteAsync_WithInvalidPaginationOrDates_ReturnsInvalidFilter` | **WHEN** Theory: `Page = 0` / `PageSize = 0` / `PageSize = 201` / `DateFrom = 2026-02-01, DateTo = 2026-01-01`. **THEN** `Failure` con `Error.Code == "InvalidFilter"`; **AND** `GetByFilterCallCount == 0` (el repo nunca se llama). |
| GI-3 | `ExecuteAsync_WithTagIdsContainingEmptyGuid_ReturnsInvalidFilter` | **WHEN** `TagIds = [Guid.NewGuid(), Guid.Empty]`. **THEN** `Failure("InvalidFilter")`; repo sin llamadas. |
| GI-4 | `ExecuteAsync_MapsFiltersOneToOne_ToRepositoryFilter` | **GIVEN** request con los 5 filtros seteados. **WHEN** `ExecuteAsync`. **THEN** `LastIncomeFilter` (fake) tiene las 6 props **idénticas** al request (CategoryId/DateFrom/DateTo/TagIds + Page/PageSize). |
| GI-5 | `ExecuteAsync_WhenRepositoryThrows_ReturnsRepositoryError` | **GIVEN** `SetThrowOnGetFilter(new InvalidOperationException("boom"))`. **WHEN** request válido. **THEN** `Failure("RepositoryError")` — sin excepción al caller. |

### 7.2 Unit — `DeleteSavedViewUseCaseTests` (DS-1..DS-4)

Namespace `ControlDeFinanzasIA.Tests.Unit.Application.SavedViews.DeleteSavedView`.
Ctor: `FakeSavedViewRepository` + `NullLogger<DeleteSavedViewUseCase>.Instance`.

| # | Test | GIVEN / WHEN / THEN |
| --- | --- | --- |
| DS-1 | `ExecuteAsync_WithExistingView_RemovesAndSucceeds` | **GIVEN** vista sembrada vía `AddAsync` en el fake. **WHEN** `ExecuteAsync(new DeleteSavedViewCommand(view.Id))`. **THEN** `IsSuccess`; `GetAllAsync()` ya no la contiene. |
| DS-2 | `ExecuteAsync_WithEmptyGuid_ReturnsValidation_WithoutRepositoryCall` | **WHEN** `new DeleteSavedViewCommand(Guid.Empty)`. **THEN** `Failure` con `Error.Code == "Validation"` y `Error.Message == "Id cannot be empty."`; la vista sigue en el fake (no hubo remove). |
| DS-3 | `ExecuteAsync_WhenRepositoryFails_PropagatesVerbatim` | **GIVEN** `RemoveResultOverride = Result.Failure("NotFound", "...")`. **WHEN** id válido. **THEN** el mismo `Error.Code == "NotFound"` se propaga (assert del code y del message verbatim). |
| DS-4 | `ExecuteAsync_WithNullCommand_ThrowsArgumentNullException` | **WHEN** `ExecuteAsync(null!)`. **THEN** `Assert.Throws<ArgumentNullException>`. |

### 7.3 Unit — `CreateTagUseCaseTests` (CT-1..CT-5)

Namespace `ControlDeFinanzasIA.Tests.Unit.Application.Catalog.CreateTag`.
Ctor: `FakeTagRepository` + `FakeClock` + `CreateTagValidator` real +
`NullLogger<CreateTagUseCase>.Instance`.

| # | Test | GIVEN / WHEN / THEN |
| --- | --- | --- |
| CT-1 | `ExecuteAsync_WithValidCommand_PersistsTagWithClockTimestamp` | **GIVEN** `Name = "  Transporte  "`, `Color = "#E8A33D"`. **WHEN** `ExecuteAsync`. **THEN** `IsSuccess`; `Assert.IsType<Tag>(result.Value)`; el tag capturado tiene `Name == "Transporte"` (trim), `Color` verbatim, `CreatedAt == clock.UtcNow`. |
| CT-2 | `ExecuteAsync_WithInvalidNameOrColor_ReturnsValidationWithDetails` | **WHEN** Theory: `Name ∈ {"", " ", "\t"}` / `Name` 51 chars / `Color` 10 chars. **THEN** `Failure("Validation")` con `Error.Details` conteniendo `"Name"` (o `"Color"`); `Assert.Empty(AddedTags)`. |
| CT-3 | `ExecuteAsync_WhenRepositoryReturnsDuplicateName_Propagates` | **GIVEN** `AddResultOverride = Result.Failure("DuplicateName", "...")`. **WHEN** command válido. **THEN** `Error.Code == "DuplicateName"` propagado — sin excepción. |
| CT-4 | `ExecuteAsync_WithNullCommand_ThrowsArgumentNullException` | **WHEN** `ExecuteAsync(null!)`. **THEN** `Assert.Throws<ArgumentNullException>`. |
| CT-5 | `ExecuteAsync_DomainInvariantPath_IsDefenseInDepth` | **NOTA de diseño (eco F-006 §6.6):** el único guard del dominio inalcanzable por API pública tras el validator (`Name ≤ 50`, `Color ≤ 9`) es el `createdAt == default` — imposible vía `IClock` real. El catch `ArgumentException → "DomainInvariant"` queda como defense in depth **documentado** (espejo `SaveCurrentFilterUseCase` paso 4); si la fase verify encuentra una vía de ejercicio, se añade el test sin ampliar scope. |

### 7.4 Unit — `IncomeViewModelTests` (IV-1..IV-9)

Namespace `ControlDeFinanzasIA.Tests.Unit.UI`. Helper `CreateViewModel`
con todos los defaults overridables (espejo `ExpensesViewModelTests`
líneas 223-255): `FakeIncomeRepository`, `FakeCategoryRepository`,
`FakeTagRepository`, `FakeSavedViewRepository`, `FakeClock`,
`FakeExpenseRepository` (para `DeleteTransactionUseCase` — 3 deps:
repos de ambos tipos + logger), use cases reales
(`RegisterIncomeUseCase` + validator, `UpdateIncomeUseCase` +
validator, `DeleteTransactionUseCase`, `SaveCurrentFilterUseCase` +
validator, `ApplySavedViewUseCase`, `GetSavedViewsUseCase`,
`DeleteSavedViewUseCase`, `CreateTagUseCase` + validator,
`GetIncomesQuery`), `NullLogger<>`, `FakeStringLocalizer<Strings>`.

| # | Test | GIVEN / WHEN / THEN |
| --- | --- | --- |
| IV-1 | `Constructor_InitializesDefaults_AndLoadsCatalogs` | **GIVEN** fakes con 2 categorías y 2 tags. **WHEN** ctor. **THEN** `Page == 1`, `TotalPages == 0`, `Incomes` vacío, `Categories.Count == 2`, `AvailableTags.Count == 2`, `SavedViews` vacía, `Date == clock.Today`, `IsEditMode == false`, `PendingDeleteIncomeId == null`, `CurrentFilter.Page == 1 && PageSize == 20`. |
| IV-2 | `Refresh_PopulatesIncomesAndPageMetadata` | **GIVEN** `SetFilterResult(CreateIncomes(20), 45)`. **WHEN** `RefreshCommand.ExecuteAsync(null)`. **THEN** `Incomes.Count == 20`; `TotalPages == 3`; `PageSummary` contiene `PageOfPagesFormat` key (FakeStringLocalizer retorna el key). |
| IV-3 | `RegisterIncome_ValidForm_CallsUseCase_AndRefreshesList` | **GIVEN** form válido. **WHEN** `RegisterIncomeCommand`. **THEN** `Assert.Single(AddedIncomes)`; form limpio (`Amount/Description/Note == null`); `StatusMessage == "IncomeRegisteredMessage"`; refresh ejecutado (`GetByFilterCallCount` incrementó). |
| IV-4 | `RegisterIncome_WithValidationErrors_ShowsFieldErrors` | **GIVEN** `Amount = 0m`. **WHEN** `RegisterIncomeCommand`. **THEN** `Assert.Empty(AddedIncomes)`; `ValidationErrors` contiene `"Amount"` (mensaje del validator real); `StatusMessage == null`. |
| IV-5 | `ApplyFilter_BuildsFilterFromProps_ResetsPageAndReloads` | **GIVEN** `Page = 3` (2 NextPage), `FilterCategoryId = cat`, `FilterDateFrom = d1`, `FilterDateTo = d2`, 2 tags con `IsSelected = true`. **WHEN** `ApplyFilterCommand`. **THEN** `Page == 1`; `LastIncomeFilter.CategoryId == cat`, `DateFrom == d1`, `DateTo == d2`, `TagIds` con los 2 ids; `GetByFilterCallCount` incrementó. |
| IV-6 | `ApplySavedView_UpdatesFilterProps_AndResetsPage` | **GIVEN** vista "Sueldo" (`IncomeFilter { CategoryId = cat }`) sembrada en el fake. **WHEN** `SelectedSavedView = view` (dispara el flow). **THEN** `FilterCategoryId == cat`; `CurrentFilter.CategoryId == cat`; `CurrentFilter.Page == 1`; `GetByFilterCallCount` incrementó. |
| IV-7 | `SaveView_WithValidName_CallsUseCase_AndReloadsViews` | **GIVEN** `NewViewName = "Mis ingresos"`. **WHEN** `SaveViewCommand`. **THEN** `Assert.Single(AddedSavedViews)` con `EntityType == Income`; `NewViewName == null`; `StatusMessage == "ViewSavedMessage"`. |
| IV-8 | `DeleteIncome_TwoStepConfirm_DeletesOnSecondInvocation` | **GIVEN** 1 ingreso en el ledger. **WHEN** 1ª `DeleteIncomeCommand.ExecuteAsync(dto)`. **THEN** `PendingDeleteIncomeId == dto.Id`; `RemoveAsync` NO llamado (sin overrides, el fake no remueve). **WHEN** 2ª invocación. **THEN** `IsSuccess` interno; ingreso removido del fake; `PendingDeleteIncomeId == null`; `StatusMessage == "TransactionDeletedMessage"`; refresh ejecutado. |
| IV-9 | `DeleteIncome_OnRepositoryError_ShowsFormError_AndResetsPending` | **GIVEN** `RemoveResultOverride = Result.Failure("NotFound", "...")`. **WHEN** 2ª invocación (confirmado). **THEN** `FormError == "FormErrorMessage"`; `PendingDeleteIncomeId == null`; sin excepción. |

### 7.5 Unit — `ExpensesViewModelTests` extensión (EV-1..EV-6)

Mismo archivo y patrón existente; el helper `CreateViewModel` se
extiende con los 8 deps nuevos (AC-9). Los 10 tests existentes
quedan **intactos** (regresión).

| # | Test | GIVEN / WHEN / THEN |
| --- | --- | --- |
| EV-1 | `ApplyFilter_BuildsExpenseFilter_FromObservableProps` | Espejo IV-5 con `LastExpenseFilter` + `ExpenseFilter` (`CurrentFilter`). |
| EV-2 | `SaveView_WithDuplicateName_ShowsStatusError_WithoutException` | **GIVEN** `AddResultOverride = Failure("DuplicateName", ...)` + `NewViewName` válido. **WHEN** `SaveViewCommand`. **THEN** `StatusMessage == "FormErrorMessage"` (o key de error); sin excepción; `SavedViews` sin cambios. |
| EV-3 | `ApplySavedView_DeserializesFilter_SyncsPropsAndResetsPage` | Espejo IV-6 con vista `Expense` + `ExpenseFilter`. |
| EV-4 | `DeleteSavedView_TwoStepConfirm_RemovesViewAndRefreshes` | **GIVEN** 1 vista en `SavedViews` + `SelectedSavedView = view`. **WHEN** 1ª `DeleteSavedViewCommand` → `PendingDeleteViewId == view.Id` sin remove; 2ª → vista removida; `SelectedSavedView == null`; `StatusMessage == "ViewDeletedMessage"`. |
| EV-5 | `CreateTag_WithValidName_RefreshesAvailableTags` | **GIVEN** `AvailableTags` vacía + `NewTagName = "Transporte"`. **WHEN** `CreateTagCommand`. **THEN** `Assert.Single(AddedTags)`; `AvailableTags.Count == 1`; `NewTagName == null`; `TagCreationError == null`. |
| EV-6 | `DeleteExpense_TwoStepConfirm_CallsTransactionDeleteWithExpenseType` | **GIVEN** `FakeExpenseRepository` con 1 gasto. **WHEN** 2 × `DeleteExpenseCommand.ExecuteAsync(dto)`. **THEN** el gasto fue removido del fake; `PendingDeleteExpenseId == null`; `StatusMessage == "TransactionDeletedMessage"` (el dispatch usa `TransactionType.Expense` — cubierto por el fake de expenses). |

### 7.6 Unit — `GetTransactionsQueryTests` extensión, regresión del mapeo (RE-1..RE-3)

Archivo existente (`Tests.Unit/Application/Expenses/GetTransactions/`);
los tests existentes quedan intactos (los defaults `null` preservan
su comportamiento).

| # | Test | GIVEN / WHEN / THEN |
| --- | --- | --- |
| RE-1 | `ExecuteAsync_WithFilters_MapsThemToRepositoryFilter` | **GIVEN** request con los 5 filtros. **WHEN** `ExecuteAsync`. **THEN** `LastExpenseFilter` con las 6 props iguales al request (antes de F-008: siempre nulls). |
| RE-2 | `ExecuteAsync_WithoutFilters_PreservesPreviousBehavior` | **GIVEN** request solo paginación. **WHEN** `ExecuteAsync`. **THEN** `LastExpenseFilter` con filtros `null` — idéntico al comportamiento pre-F-008 (regresión). |
| RE-3 | `ExecuteAsync_WithInvertedDateRange_ReturnsInvalidFilter` | **GIVEN** `DateFrom > DateTo`. **WHEN** `ExecuteAsync`. **THEN** `Failure("InvalidFilter")`; `GetByFilterCallCount` sin incremento. |

### 7.7 Cobertura y notas

**Cobertura global:** happy paths (GI-1, DS-1, CT-1, IV-2/3/6/7,
EV-3/5, RE-1), edge cases (GI-2/3, IV-5, EV-1/4, RE-2/3 — defaults,
límites de paginación, whitespace, 2-pasos), error states (GI-5,
DS-2/3, CT-2/3, IV-4/9, EV-2, RE-3). Los flujos de UI completos
(save → combobox → apply → delete) quedan además cubiertos por el
smoke manual AC-28 (los VM tests blindan el estado; el XAML es la
cara).

**Notas de límites (documentados, no holes):**

- **Sin UI tests automatizados en este feature**: `Avalonia.Headless`
  ya está referenciado (csproj verificado), pero headless-testing de
  las vistas queda como nota para una fase futura de UI-hardening —
  F-008 blinda la lógica en los VMs (testeables sin UI, por diseño).
  `sdd-tasks/apply` NO deben añadir UI tests silenciosamente
  (disciplina de scope, espejo F-006 §6.6).
- **`TagIds` en `FakeIncomeRepository`/`FakeExpenseRepository`**: los
  fakes no implementan la semántica OR de filtros (retornan el
  `FilterResult` configurado) — la semántica de filtros ya está
  cubierta por los integration tests de F-007 (repo real). Los tests
  de F-008 blinan el **mapeo** (passthrough), no la query SQL.
- **`BeginEdit`/`CancelEdit`/`ClearFilter`**: cubiertos vía asserts
  de estado dentro de IV-5/EV-1 (ClearFilter) e IV-8/EV-6 (estado
  pendiente); si `sdd-tasks` desea tests dedicados, los añade sin
  ampliar scope de feature.

**Cuenta estimada:** GI 5 + DS 4 + CT 4 + IV 9 + EV 6 + RE 3 ≈ **31
tests nuevos** (mínimo contractual ≥15, proposal AC-15: ≥15 con
`GetIncomesQueryTests` ≥3, `DeleteSavedViewUseCaseTests` ≥2,
`IncomeViewModelTests` ≥6, extensión `ExpensesViewModelTests` ≥4 —
todas las cotas superadas).

---

## 8. Fuera de alcance

F-008 **no** hace lo siguiente (cada ítem vive en otra feature o se
difiere explícitamente):

- **Cualquier cambio a Domain/Infrastructure**: entidades
  (`Income`/`Expense`/`Tag`/`Category`/`SavedView`), filtros
  (`ExpenseFilter`/`IncomeFilter` — solo se consumen), repos,
  configuraciones, migraciones (cero `dotnet-ef`), `FilterJsonSerializer`,
  `SeedData`, `ServiceCollectionExtensions`. F-008 es Application (4
  orquestadores + DTO + extensión de request) + UI + tests.
- **Tags por fila en el ledger**: `IncomeDto`/`ExpenseDto` no llevan
  tags (D-3 + corrección #3 — el repo no materializa la navegación;
  mostrar chips de tags requeriría `Include` = cambio Infrastructure).
  El **filtro** por tags sí funciona (SQL, F-007).
- **Edición del campo `Source` de ingresos**: `UpdateIncomeCommand`
  no lo modela (F-005 lo difirió — el use case resetea `Source` a
  null al editar). Limitación conocida y heredada; el form no expone
  el campo.
- **Gestión completa de tags** (rename/delete/recolor de tags
  existentes, asignación de tags a transacciones desde el form):
  solo se crea el affordance mínimo de creación (D-5/Q-5). El CRUD
  completo de tags y el taggear-transacciones (CU-4 del plan) van en
  fase posterior.
- **UI tests automatizados** (headless) — nota §7.7.
- **Restyle del shell/sidebar/dashboard** (Q-3): solo adoptan el Dark
  variant; `MainWindow.axaml` intacto.
- **Theming/toggle de temas** (Dark único), **light theme**,
  **mobile/touch**, **print stylesheet**, **i18n más allá de
  es-DO/neutral** existente.
- **Animación más allá de `Animation`/`Transitions` de Avalonia**
  (sin Lottie/GRAL/¿video?), **drag-and-drop** de columnas/filas,
  **virtualización custom** del ledger (`ListBox` estándar).
- **Recurrentes (P6), multi-moneda (P5), charts (P10), presupuestos
  (P8)** — out of scope del plan §13.
- **`GetSavedViews` con caché / invalidación cruzada entre vistas**
  (cada VM recarga su lista al montar/actuar — suficiente para
  single-user local).
- **Migración del debt i18n restante fuera de las secciones
  reescritas** (scope de D-10).

---

## 9. Plan de rollback

Todo es **aditivo** — cero cambios de esquema, cero Domain/Infra,
cero datos de usuario afectados (las filas de `SavedViews` que el
usuario haya creado son **dato**, no schema: se persisten y sobreviven
a cualquier revert de UI). Revertir **en orden inverso de la cadena**
(AC-28):

1. **PR-4 primero (design pass):** eliminar `Assets/Fonts/` (binarios
   + licencias), `Styles/{Tokens,LedgerStyles,Motion}.axaml`, el
   `ItemGroup` `AvaloniaResource` del csproj, y revertir `App.axaml`
   (`RequestedThemeVariant` de vuelta a `"Default"`, quitar el
   `ResourceInclude` de Tokens) + quitar los `StyleInclude` de
   `UserControl.Styles` en ambas vistas. Las vistas vuelven a
   FluentTheme default — **funcionalmente intactas** (los estilos son
   una capa, no lógica).
2. **PR-3 (extensión de Expenses):** revertir
   `ExpensesViewModel.cs` y `ExpensesView.axaml` al commit anterior +
   eliminar los tests EV y las keys resx usadas solo por PR-3. Los
   datos de `SavedViews` del usuario **no se tocan** (solo se oculta
   la UI que los gestiona).
3. **PR-2 (IncomeView + wiring):** eliminar `IncomeView.axaml`
   (+ `.axaml.cs`), `IncomeViewModel.cs`, `SelectableTagItem.cs`,
   los tests IV, la key `SidebarIncomes` (×3 resx) y las 4 ediciones
   de wiring (`RouteNames`, `NavigationService`, `ShellViewModel`,
   `App.axaml`). La ruta "Ingresos" desaparece limpiamente — ningún
   otro consumer la referencia (blast radius verificado).
4. **PR-1 (Application + test infra):** eliminar archivo por archivo:
   `IncomeDto`/`IncomeExtensions`, `GetIncomesQuery`(+Request),
   `DeleteSavedView` ×2, `CreateTag` ×3, `FakeTagRepository`; revertir
   `GetTransactionsQueryRequest`/`GetTransactionsQuery` a los nulls
   hardcodeados (comportamiento pre-F-008), los 5 registros +
   3 usings de `Bootstrap`, las extensiones de fakes y los tests
   GI/DS/CT/RE.
5. **NOTA de orden (crítica):** revertir PR-1 **después** de que
   PR-2/PR-3 estén mergeados rompe el compile (sus tipos consumen los
   de PR-1) — revertir **siempre** en orden inverso 4→3→2→1. Sin
   migraciones que deshacer; sin `dotnet-ef`.
6. **Verificar** tras cada revert: `dotnet build` limpio y `dotnet
   test` suite baseline en verde (estado pre-F-008, incluido el único
   fallo pre-existing de `Money`). NetArchTest R-1..R-5 no se afectan
   en ningún paso: los cambios tocan Application (2 orquestadores +
   DTO + request) y UI (VMs/XAML/DI — composition root sancionado
   por R-4).

---

## 10. Riesgos y mitigaciones

| # | Riesgo | Prob. | Impacto | Mitigación |
| --- | --- | --- | --- | --- |
| R-1 | Compiled bindings: `x:DataType` incorrecto o ausente en los DataTemplates nuevos (fila de ledger, tags, vistas, categorías) → build XAML falla o bindings degradan a runtime. | Media | Alto | Gate AC-27 explícito (0 warnings de compiled bindings); patrón dual verificado en `App.axaml`; `$parent[UserControl]` + `CommandParameter="{Binding}"` para los comandos de fila; `dotnet build` compila el XAML y atrapa el 90% en CI local. |
| R-2 | Fuentes embebidas: sintaxis `avares://…#Family`, nombre de familia interno distinto al esperado en TTFs variables ("Fraunces" vs "Fraunces Variable"), o formato no soportado. | Media | Medio | Docs Avalonia verificadas vía Context7 (solo ttf/otf + `AvaloniaResource` — el woff2 del contrato se corrigió); PR-4 arranca probando **1 fuente** end-to-end antes de extender; fallback documentado: reducir a 2 fuentes o URIs por archivo con el family name exacto de la name table; licencias OFL acompañan los binarios (costo cero). |
| R-3 | Overrides de FluentTheme vs Dark fijo: los tokens podrían no llegar a los internals de FluentTheme (focus adorners, popup chrome) o el remapeo lava el contraste. | Baja | Medio | D-12: FluentTheme se conserva como base; estilos vía **classes** `ledger-*` (no implícitos globales — no colisionan ni re-stylean el shell); `RequestedThemeVariant="Dark"` es un switch first-class soportado; PR-4 prueba una vista antes de ambas; verificación visual AC-28 con screenshots. |
| R-4 | MVVM-estricto: sincronizar multi-select de tags y el estado 2-pasos de borrado sin code-behind. | Media | Medio | D-8 wrappers `SelectableTagItem.IsSelected` (bindings compile-safe por item); D-7 estado pendiente como props del VM (`PendingDelete*Id`) con reset en la siguiente acción; IV-5/IV-8/EV-4/EV-6 blindan las transiciones; los converters de label son artefactos de vista (sin lógica). |
| R-5 | Exceso de líneas por PR (presupuesto 400) — la feature más grande de Phase 4. | **Alta** | Medio | Pronóstico AC-28/§6: **4 PRs stacked-to-main** con inicio/fin claros y rollback propio (§9); `sdd-tasks` formaliza el corte con las líneas de guard obligatorias. |
| R-6 | `DeleteTransactionUseCase` con `TransactionType.Income` desde IncomeView — flujo nunca ejercitado en UI. | Baja | Bajo | IV-8/IV-9 cubren happy + `NotFound`; el use case está testeado desde F-005 (dispatch verificado); smoke AC-28 punto 2 lo ejecuta end-to-end. |
| R-7 | `StatusMessage`/confirmación 2 pasos — UX confusa si el estado pendiente no resetea (borrar la fila A arma el estado y editar la fila B debería desarmarlo). | Baja | Medio | D-7: **toda acción de VM resetea** el pendiente (documentado en AC-7 para cada comando); tests de transición; smoke AC-28 verifica los flujos completos. |
| R-8 | Engram `mem_save` no disponible en el toolset del sub-agente de spec (verificado: sin funciones `mem_*` — mismo estado que F-002..F-007 P4). | Alta (verificado) | Bajo | Respaldo en filesystem: proposal en `.atl/sdd/f-008p4-ui-capstone/proposal.md` (pre-existente) y esta spec en su path canónico. El **orquestador** backfillará a Engram los topic keys `sdd/f-008p4-ui-capstone/{proposal,spec}`. El sub-agente NO fabrica observation IDs. |
| R-9 | Ctors de 14 deps: wiring equivocado en Bootstrap o en los helpers de tests (registro faltante → fallo de resolve en runtime). | Baja | Medio | AC-19 enumera los 5 registros exactos; el helper IV construye las 14 deps **explícitamente** (una dep faltante = error de compile en tests, no sorpresa de runtime); el smoke AC-28 ejercita la resolución real del composition root. |

---

## 11. Referencias

- **Propuesta (filesystem):**
  `.atl/sdd/f-008p4-ui-capstone/proposal.md` (278 líneas) — PRD de
  origen. Q-1..Q-5 ratificadas (defaults asumidos + ronda
  interactiva); sus decisiones D-1..D-11 fueron refinadas por la
  verificación de fuente — supersesiones del contrato de lanzamiento
  documentadas en §3.1 de esta spec.
- **Propuesta (Engram objetivo):** topic key
  `sdd/f-008p4-ui-capstone/proposal` (pendiente de persistencia por
  el orquestador — ver R-8).
- **Spec (Engram objetivo):** topic key `sdd/f-008p4-ui-capstone/spec`
  (pendiente de persistencia por el orquestador — ver R-8).
- **F-006 Fase 4 (archivada — estructura espejo + maquinaria
  consumida):** `spec/features/phase-04/F-006-saved-view-filter-json.done.md`
  (1,510 líneas) — estructura de 10 secciones replicada (+§4 estética);
  `SavedView`/`FilterJsonSerializer`/`SaveCurrentFilter`/
  `ApplySavedView`/`GetSavedViews` que esta feature consume; su D-8
  difirió `DeleteSavedViewUseCase` aquí; su D-7/Q-2 se resuelve en
  D-11 (persistir completo + resetear `Page` al aplicar).
- **F-005 Fase 4 (archivada — patrón use cases):**
  `spec/features/phase-04/F-005-update-delete-use-cases.done.md`
  (1,339 líneas) — `DeleteTransactionUseCase` (espejo de
  `DeleteSavedViewUseCase`), D-2 validación sync, D-3 details por
  campo, D-4 `ArgumentException` → `"DomainInvariant"`, C-3
  convención de árbol de tests, D-ML idioma de mensajes.
- **F-007 Fase 4 (archivada — filtros):**
  `spec/features/phase-04/F-007-extend-filters-tagids.done.md`
  (1,091 líneas) — `ExpenseFilter`/`IncomeFilter` 6 props con
  `TagIds` (semántica OR) que los queries de esta feature mapean.
- **F-004 Fase 4 (archivada — `Tag`):**
  `spec/features/phase-04/F-004-tag-entity-and-many-to-many.done.md`
  — entidad `Tag` con `Create(name, color, createdAt)` que
  `CreateTagUseCase` consume.
- **Phase plan:** `spec/phases/phase-04/features.md` — §4.1 líneas
  52-54 (alcance UI), §RF-16 (IncomeView), §RF-17 (`GetIncomesQuery`
  — "Paralelo a `GetTransactionsQuery`"), §RF-19 (filter inputs +
  saved views UI), §RF-20 (tests), §6 CU-1..CU-7, §8 reglas de
  negocio, §9 tabla 4-008, §12 criterios de cierre de fase, §13 out
  of scope.
- **Skill de diseño:** `.claude/skills/frontend-design/SKILL.md` —
  dirección BOLD, anti-estética-genérica (Inter/Roboto/Arial
  prohibidos), tipografía distintiva + paleta dominante con acentos
  sharp + motion de alto impacto (§4 de esta spec).
- **Docs Avalonia (verificadas vía Context7, `/avaloniaui/avalonia-docs`):**
  "Custom fonts" / "How to add a custom font" — soporte **ttf/otf**
  (woff2 NO), `<AvaloniaResource Include="Assets\**" />`, formato URI
  `avares://Assembly/Path/To/Fonts#Family Name`, declaración como
  recurso `FontFamily` en `Application.Resources` (AC-20/AC-23).
- **Código verificado (fuentes de esta spec):**
  `src/.../UI/ViewModels/{ShellViewModel,ExpensesViewModel,ViewModelBase}.cs`
  (75/277/10 líneas — plantillas VM + `MenuItems` + fire-and-forget),
  `src/.../UI/Views/{ExpensesView.axaml, MainWindow.axaml}` (112/32
  líneas — plantilla de vista + shell data-driven),
  `src/.../UI/App.axaml` (23 líneas — patrón DataTemplate dual),
  `src/.../UI/Navigation/{RouteNames,NavigationService,RouteMenuItem}.cs`
  (21/83/8 líneas — wiring de rutas),
  `src/.../UI/Composition/Bootstrap.cs` (143 líneas — DI),
  `src/.../UI/ControlDeFinanzasIA.UI.csproj` (42 líneas —
  `AvaloniaUseCompiledBindingsByDefault=true`, resx ItemGroups),
  `src/.../Application/UseCases/Expenses/GetTransactions/{GetTransactionsQuery,GetTransactionsQueryRequest}.cs`
  (87/10 líneas — espejo del query + nulls hardcodeados),
  `src/.../Application/DTOs/{ExpenseDto,ExpenseExtensions}.cs`
  (19/26 líneas — espejo del DTO),
  `src/.../Application/UseCases/Transactions/DeleteTransaction/{DeleteTransactionUseCase,DeleteTransactionCommand}.cs`
  (71/— líneas — espejo del delete),
  `src/.../Application/UseCases/SavedViews/**` (Save/Apply/Get —
  consumidos tal cual),
  `src/.../Application/UseCases/Incomes/**` (Register/Update —
  commands sin `Source`),
  `src/.../Application/Abstractions/{IncomeFilter,ExpenseFilter,IIncomeRepository,ITagRepository}.cs`,
  `src/.../Domain/Transactions/Income.cs` (319 líneas —
  `CategoryId` nullable, `Source`, `Tags` ICollection),
  `src/.../Domain/Catalog/{Tag,Category}.cs` (142/16 líneas —
  `Tag.Create` exige `createdAt`),
  `src/.../Domain/Enums/{Currency,TransactionType}.cs`,
  `src/.../Application/Common/{Result,PagedResult,FilterJsonSerializer}.cs`,
  `src/.../Infrastructure/Persistence/Repositories/IncomeRepository.cs`
  (225 líneas — `GetByFilterAsync` AsNoTracking **sin Include**,
  corrección #3),
  `src/.../UI/Resources/Strings.resx` (127 líneas — 22 keys,
  `PageOfPagesFormat` sin uso),
  `tests/.../Tests.Unit/UI/ExpensesViewModelTests.cs` (277 líneas —
  plantilla de tests de VM),
  `tests/.../Tests.Unit/TestDoubles/{FakeExpenseRepository,FakeIncomeRepository,FakeSavedViewRepository}.cs`
  (141/97/99 líneas — patrones de fakes + gap de filter setup en
  Income),
  `tests/.../Tests.Unit/Application/Expenses/GetTransactions/GetTransactionsQueryTests.cs`
  (regresión del mapeo).
- **Arquitectura y constitución:** `docs/architecture.md` (§4 R-1..R-5,
  §7 use cases, §9 UI, §10 navegación), `docs/vision.md` (§4.3
  filtros/vistas guardadas, §6 localización), `docs/roadmap.md`
  (§Fase 4), `spec/constitution/mission.md`,
  `spec/constitution/tech-stack.md` (MVVM estricto, async/await
  extremo a extremo, errores como valores, "no silent failures").
- **AGENTS.md del repo** — gates de verificación, convenciones de
  commits/localización, NetArchTest R-1..R-5, CPM obligatorio.









