# Arquitectura — ControlDeFinanzasIA

> Decisiones de **cómo** se construye el producto definido en `docs/vision.md`,
> respetando el stack de `spec/constitution/tech-stack.md`.
>
> Este documento es **fuente de verdad** para la estructura de la solución,
> las capas, las dependencias, los contratos y las convenciones. Cambios
> importantes deben documentarse como ADR al final del archivo.

---

## 1. Principios arquitectónicos

1. **Clean Architecture en cuatro capas** — Dominio, Aplicación, Infraestructura, Presentación. Las dependencias apuntan **hacia adentro**: Presentación → Infraestructura → Aplicación → Dominio.
2. **Dominio puro, sin framework.** El núcleo del modelo (entidades, value objects, reglas) no conoce EF Core, Avalonia ni HTTP. Esto lo hace testeable de forma aislada y portable.
3. **MVVM estricto en la UI.** Sin code-behind con lógica. Los Views son declarativos (XAML + bindings), los ViewModels son clases POCO testeables.
4. **Inversión de dependencias.** La capa de Aplicación define interfaces (`IExpenseRepository`, `ILLMClient`, `IClock`, `ISettingsStore`). Infraestructura las implementa. UI consume las interfaces, nunca las implementaciones.
5. **Errores esperados como valores, no excepciones.** Validación y reglas de negocio devuelven un `Result<T>`. Las excepciones se reservan para lo inesperado.
6. **Local-first, offline-capable.** Toda la funcionalidad core (registrar, listar, reportar, presupuestar) funciona sin red. La IA falla silenciosamente si no hay key, red o el proveedor cae.
7. **Determinismo testeable.** Ningún ViewModel, use case o servicio de dominio usa `DateTime.Now` ni `Environment.CurrentDirectory` directamente. Se inyectan `IClock` y `IPathResolver`.

---

## 2. Estilo

Clean Architecture + MVVM + CQRS ligero (separación de comandos y queries dentro de Aplicación). Sin DDD táctico avanzado (no aggregate roots formales con consistencia transaccional compleja): la unidad transaccional es el `DbContext` de EF Core y la entidad es la unidad de consistencia. Suficiente para una app personal single-user.

**Por qué Clean Architecture y no solo capas horizontales (UI/Services/Data):**
- Hay **múltiples integraciones externas** (varios proveedores de IA, filesystem, CSV) que necesitan un *seam* limpio.
- Las **reglas financieras** (cálculo de cuotas, aporte automático a metas, evaluación de presupuestos, detección de anomalías) tienen la complejidad suficiente para aislarlas.
- Facilita **testing**: los ViewModels y use cases se prueban sin tocar la DB ni la red.
- Permite **cambiar la UI** (Avalonia → otro framework) o **cambiar la persistencia** (SQLite → Postgres) con impacto acotado.

**Por qué MVVM y no Code-behind reactivo:**
- Avalonia está pensado para MVVM. DataTemplates, bindings y el `INotifyPropertyChanged` son ciudadanos de primera.
- La lógica de UI (estado, comandos, navegación) tiene volumen suficiente para merecer ViewModels testeables.

---

## 3. Solución y proyectos

La solución `ControlDeFinanzasIA.sln` contiene **seis proyectos** C#:

| Proyecto | Tipo | Responsabilidad |
| --- | --- | --- |
| `ControlDeFinanzasIA.Domain` | Class library (net8.0) | Entidades, value objects, servicios de dominio puros, enums, contratos de repositorio. **Sin dependencias** de EF Core, Avalonia o HTTP. |
| `ControlDeFinanzasIA.Application` | Class library (net8.0) | Use cases, DTOs, validadores, orquestación. Define las interfaces (`I*Repository`, `ILLMClient`, `IClock`, `ISettingsStore`). Depende solo de Domain. |
| `ControlDeFinanzasIA.Infrastructure` | Class library (net8.0) | EF Core + SQLite, repositorios concretos, clientes LLM (OpenAI, Anthropic, OpenRouter), settings, filesystem, CSV. Depende de Application y Domain. |
| `ControlDeFinanzasIA.UI` | Executable (net8.0, Avalonia) | Views (XAML), ViewModels, navegación, theming, i18n, composition root, bootstrap. Depende de Application y Domain (consume interfaces). Infrastructure se referencia **solo en el composition root** para registrar implementaciones. |
| `ControlDeFinanzasIA.Tests.Unit` | xUnit | Tests de Domain y Application con mocks. No toca DB ni red reales. |
| `ControlDeFinanzasIA.Tests.Integration` | xUnit | Tests de Infrastructure (EF Core contra SQLite en archivo temporal), importers/exporters, y un subconjunto marcado como "external" para clientes LLM (se ejecutan solo si hay API key en el entorno). |

### Justificación de cada proyecto

- **Domain separado de Application:** las entidades no deben saber de casos de uso. Los use cases no deben filtrarse a las entidades. Mezclarlos lleva a "entidades-anémicas-con-servicios-adentro".
- **Infrastructure separado de Application:** los detalles (SQLite, HTTP, JSON) cambian. Application no debe depender de EF Core ni de `HttpClient`.
- **UI separado y con dependencia solo en composition root:** permite, en el futuro, añadir un segundo front (CLI, app móvil) o testear ViewModels sin cargar Avalonia.
- **Tests.Unit y Tests.Integration separados:** diferentes velocidades, diferentes dependencias (los unit no deben poder tocar la DB accidentalmente).

---

## 4. Grafo de dependencias

```
                    ┌──────────────────────┐
                    │       UI (Avalonia)  │
                    └──────────┬───────────┘
                               │ depende de
                               ▼
┌──────────────────┐   ┌────────────────────┐   ┌──────────────────────┐
│  Infrastructure  │──▶│    Application     │──▶│       Domain         │
└──────────────────┘   └────────────────────┘   └──────────────────────┘
        │                                                ▲
        └────────── implementa interfaces de ────────────┘
                  Application (I*Repository, ILLMClient, ...)

        ┌──────────────┐         ┌──────────────────────┐
        │ Tests.Unit   │────────▶│ Domain / Application │
        └──────────────┘         └──────────────────────┘

        ┌──────────────────┐     ┌──────────────────────┐
        │ Tests.Integration│────▶│  Infrastructure     │
        └──────────────────┘     └──────────────────────┘
```

### Reglas de dependencia (estrictas)

- **Domain no referencia a nadie** (excepto BCL puro). Validado por analyzers / convención en code review.
- **Application solo referencia a Domain** y a BCL/paquetes neutros (FluentValidation, etc.).
- **Infrastructure referencia Application y Domain**, más paquetes externos (EF Core, SQLite, HttpClient, etc.).
- **UI referencia Application y Domain** para ViewModels y contratos. **No referencia Infrastructure directamente**, salvo el `App.axaml.cs` / `Program.cs` que es el composition root y registra implementaciones.
- **Tests** pueden referenciar cualquier proyecto, pero **nadie los referencia a ellos**.

**Cómo se valida esto:** se puede usar `Microsoft.CodeAnalysis.BannedApiAnalyzers` o `NetArchTest` para testear el grafo automáticamente (ver sección de testing). En v1 basta con code review + estructura de carpetas.

---

## 5. Convenciones de código y carpetas

### Lenguaje y formato

- **C# 12**, .NET 8 LTS.
- **Nullable reference types** activados en toda la solución.
- **Warnings as errors** en CI.
- **File-scoped namespaces.**
- **`using` al tope**, no anidados.
- **Un tipo público por archivo**, nombre del archivo = nombre del tipo. Excepción: tipos privados anidados van en el archivo del host.
- **Async suffix** en métodos asíncronos (`RegisterExpenseAsync`).
- **PascalCase** para tipos y miembros públicos, **camelCase** para parámetros y locales, **_camelCase** para campos privados.
- **EditorConfig + StyleCop** en el repo. Sin rechazos a nivel warning.

### Carpetas dentro de cada proyecto

- `Entities/`, `ValueObjects/`, `Enums/`, `DomainServices/`, `Interfaces/` (solo contratos) en **Domain**.
- `UseCases/{Feature}/`, `DTOs/`, `Validators/`, `Common/`, `Abstractions/` en **Application**. Las interfaces de infraestructura (repos, LLM, settings, clock, paths) viven en `Application/Abstractions/`.
- `Persistence/`, `Llm/`, `Settings/`, `Files/`, `Logging/` en **Infrastructure**.
- `Views/`, `ViewModels/`, `Navigation/`, `Resources/`, `Styles/`, `Converters/`, `DesignTime/` en **UI**.

### ViewModels y bindings

- **CommunityToolkit.Mvvm** para `[ObservableProperty]` y `[RelayCommand]`. Reduce boilerplate, estandariza el patrón.
- **No** se usa code-behind para lógica. El code-behind solo inicializa componentes y maneja eventos visuales puros.

---

## 6. Capa de Dominio

### Entidades centrales (modelo de dominio)

- **`Transaction`** (abstracta, base) — `Id`, `Date`, `Amount` (Money), `Currency`, `Description`, `Note?`, `CategoryId`, `TagIds`, `PaymentMethodId`, `CreatedAt`, `UpdatedAt`, `SourceRuleId?` (si fue auto-generada por un recurrente).
  - **`Expense : Transaction`**
  - **`Income : Transaction`**
- **`Debt`** — `Id`, `Description`, `OriginalAmount` (Money), `Currency`, `TotalInstallments`, `InstallmentAmount` (Money), `Frequency` (enum), `StartDate`, `CounterpartyName?`, `Note?`, `Status` (Active/Closed).
  - Genera, mediante un servicio de dominio, las `DebtPayment` esperadas a partir de la fecha de inicio. Cada pago pagado actualiza el saldo pendiente.
- **`DebtPayment`** — `Id`, `DebtId`, `InstallmentNumber`, `ScheduledDate`, `PaidDate?`, `Amount` (Money), `Status` (Pending/Paid/Skipped).
- **`RecurringRule`** (abstracta, base) — `Id`, `Name`, `Amount` (Money), `Currency`, `Frequency` (Weekly/Biweekly/Monthly/...), `StartDate`, `EndDate?`, `CategoryId`, `PaymentMethodId`, `TagIds`, `Active`, `NextGenerationDate`.
  - **`Subscription : RecurringRule`** (lista editable de suscripciones, una por servicio).
  - **`FixedExpense : RecurringRule`** (alquiler, internet, etc.).
  - Las **cuotas de deuda** se modelan como `DebtPayment`, no como `RecurringRule`, porque tienen una cantidad finita y un plan.
- **`Goal`** (Fase 11, implementado) — `Id`, `Name`, `TargetAmount` (Money), `Currency` (denormalizado
  de `TargetAmount.Currency`), `TargetDate` (sin restricción — una fecha pasada es un estado legal),
  `Note?`, `Status` (Active/Completed). Clase con setters privados y factory estático `Create` que
  valida las invariantes (mismo patrón que `Debt`/`Budget`), no un record con setters públicos.
  `CreatedAt` fue eliminado del stub original (sin consumidores, mismo criterio que Fase 10 con
  `Budget`). El progreso (`CalculateProgress`) siempre se calcula sumando `GoalContribution`, nunca se
  almacena un saldo corriente.
- **`GoalContribution`** (Fase 11, implementado) — `Id`, `GoalId`, `Date`, `Amount` (Money). `Create`
  es un factory **público** (a diferencia de `DebtPayment`, cuyo constructor es `internal`): los
  aportes son manuales y ad-hoc, no generados en bloque por un calendario. El campo `Source` del stub
  original fue eliminado (un solo valor posible en v1, ver `docs/roadmap.md` §Fase 11).
- **`Budget`** (Fase 10, implementado) — `Id`, `Year`, `Month`, `CategoryId` (nullable en el esquema
  para futuros presupuestos globales, pero `Create` exige un valor no nulo en v1), `Amount` (Money),
  `Currency` (denormalizado de `Amount.Currency`). Clase con setters privados y factory estático
  `Create` que valida las invariantes (mismo patrón que `Debt`), no un record con setters públicos.
  Índice único en `(Year, Month, CategoryId)` garantiza upsert sin duplicados a nivel de esquema.
- **`Category`** — `Id`, `Name`, `Color?`, `Icon?`, `IsSystem` (no se puede borrar si es del sistema).
- **`Tag`** — `Id`, `Name` (único).
- **`PaymentMethod`** — `Id`, `Name`, `IsSystem`.
- **`SavedView`** — `Id`, `Name`, `FilterJson` (filtro serializado para reutilizar).
- **`AnomalyAlert`** — `Id`, `Date`, `TransactionId`, `Message`, `Severity` (Info/Warning/Critical), `AcknowledgedAt?`.
- **`Setting`** (Fase 12, implementado) — `Key`, `Value` (clave-valor para preferencias y config de IA).
  `sealed record` (no clase con factory, a diferencia de `Goal`/`Budget`): sin consumidores previos,
  fue seguro endurecer su validación in situ (`Key` no vacío, se recorta; `MaxKeyLength = 128`,
  `MaxValueLength = 2048`) sin migrar un patrón existente. Persistido vía `ISettingsStore`, que expone
  el POCO tipado `AppSettings` (nunca las filas crudas) y nunca lanza ni devuelve `null` — ante fila
  ausente, valor corrupto o excepción del repositorio, degrada a `AppSettings.Defaults` con
  `LogWarning`.

### Value objects

- **`Money`** — `Amount` (decimal) + `Currency` (enum). Operaciones aritméticas seguras, validación de redondeo, formato. Las sumas entre monedas distintas lanzan error (la conversión no es dominio; se hace en otra capa).
- **`Currency`** (enum) — `DOP`, `USD`.
- **`DateRange`** — para filtros y reportes (semana, mes, año, personalizado).
- **`RecurrenceFrequency`** (enum) — `Weekly`, `Biweekly`, `Monthly`, `Quarterly`, `Yearly`.

### Servicios de dominio (puros, sin I/O)

- **`RecurringOccurrenceCalculator`** — dada una `RecurringRule` y una fecha de referencia, devuelve la(s) fecha(s) de generación que tocan ese mes (o ninguno). Sin tocar la DB.
- **`DebtScheduleGenerator`** — dado un `Debt` (no guardado), devuelve la lista de `DebtPayment` esperadas. Sin tocar la DB.
- **`SavingsPlanCalculator`** — dados movimientos del periodo y una meta, calcula el aporte mensual necesario para llegar a la meta en una fecha objetivo. Implementación determinista (cálculo puro) que la IA después **refina o explica**; no es la IA la única fuente de la cifra.
- **`BudgetEvaluator`** — dado un presupuesto del mes y los gastos reales por categoría hasta hoy, devuelve porcentaje consumido y estado (OK / Cerca / Excedido).
- **`AnomalyDetector`** — implementación determinista base (z-score sobre histórico por categoría). La IA luego produce el mensaje legible al usuario.

**Por qué deterministas y no solo IA:** la IA no debe ser el único camino para una alerta crítica. Si falla, el detector base sigue funcionando y produce un mensaje genérico.

### Interfaces de dominio (contratos que viven en Domain)

- **`IDomainEvent`** y un **`IDomainEventDispatcher`** opcional. En v1 los use cases leen la entidad recargada tras `SaveChangesAsync`; no se requiere event sourcing.

---

## 7. Capa de Aplicación

### Abstracciones (interfaces que Infrastructure implementa)

- **`IExpenseRepository`**, **`IIncomeRepository`**, **`IDebtRepository`**, **`IDebtPaymentRepository`**, **`IRecurringRuleRepository`**, **`IGoalRepository`**, **`IGoalContributionRepository`**, **`IBudgetRepository`**, **`ICategoryRepository`**, **`ITagRepository`**, **`IPaymentMethodRepository`**, **`ISavedViewRepository`**, **`IAnomalyAlertRepository`**, **`ISettingRepository`**, **`IUnitOfWork`**.
- **`ILLMClient`** — contrato unificado para cualquier proveedor de IA (ver sección 10).
- **`IClock`** — `DateTime UtcNow`, `DateOnly Today`. Para testear fechas.
- **`IPathResolver`** — devuelve la ruta del directorio de datos según el SO.
- **`ISettingsStore`** — lectura/escritura tipada de settings (envuelve `ISettingRepository` con helpers).
- **`IBackupService`** — respaldo y restauración de la base completa (Fase 19): `CreateAsync`, `InspectAsync`, `RestoreAsync` y `CompletePendingRestore()`. Recibe delegados que abren el stream (`Func<CancellationToken, Task<Stream>>`), nunca rutas ni tipos de SQLite/ZIP. Lo acompañan **`IMigrationCatalog`** (ids de migración conocidos por este build), **`IFileOpenDialog`** (selector de archivo que devuelve un `OpenTarget?`) y las clases `OpenTarget`/`FileTypeFilter`. `IAppRestartService` vive en UI (`UI/Services`), no en Application.
- **`ITabularReader`**, **`IImportLookup`**, **`IImportWriter`** — importación CSV / Excel (Fase 20, ver §11g): lectura de bytes a una tabla tipada (`RawTable`), consulta del catálogo y de las transacciones ya guardadas en el rango de fechas del archivo, y escritura atómica de un `ImportBatch`. Ninguno expone tipos de ClosedXML, EF Core, `System.IO.Compression` ni Avalonia. `ITabularReader` recibe `ReadOnlyMemory<byte>` y `TabularReadOptions`; no recibe el nombre del archivo.

### Use cases (uno por intención de usuario)

Agrupados por carpeta según la feature. Todos devuelven `Result<T>` (o `Task<Result<T>>`). Validación previa con FluentValidation.

**Expenses / Income**
- `RegisterExpenseUseCase`, `UpdateExpenseUseCase`, `DeleteExpenseUseCase`
- `GetTransactionsQuery` (paginado, con filtros)
- `RegisterIncomeUseCase`

**Debts**
- `CreateDebtWithScheduleUseCase` (crea el Debt y todas las DebtPayment)
- `RegisterDebtPaymentUseCase` (marca pagada, recalcula saldo)
- `SkipDebtPaymentUseCase`
- `GetDebtsQuery`, `GetDebtScheduleQuery`

**Recurring**
- `CreateSubscriptionUseCase`, `UpdateSubscriptionUseCase`, `DeleteSubscriptionUseCase`
- `CreateFixedExpenseUseCase`, `UpdateFixedExpenseUseCase`, `DeleteFixedExpenseUseCase`
- `SkipRecurringMonthUseCase`
- `EditGeneratedTransactionUseCase` (cambia monto/fecha de un movimiento auto-generado)
- `GenerateDueRecurringUseCase` (job: genera los movimientos del mes que tocan)

**Goals**
- `CreateGoalUseCase`, `UpdateGoalUseCase`, `DeleteGoalUseCase`
- `ContributeToGoalUseCase` (manual)
- `ComputeAutoContributionsUseCase` (calcula aporte automático del periodo)
- `GenerateSavingsPlanUseCase` (combina el cálculo determinista con la IA)

**Budgets**
- `SetMonthlyBudgetUseCase` (crea o actualiza)
- `EvaluateBudgetsQuery` (devuelve % consumido y estado por categoría del mes)

**AI (orquestación)**
- `CategorizeExpenseUseCase` (texto → categoría sugerida, persistida como sugerencia)
- `AskFinanceChatUseCase` (convierte pregunta + contexto en llamada a LLM)
- `GetInsightsUseCase`
- `GetMonthEndForecastUseCase`
- `GetAnomalyNarrativeUseCase` (toma un AnomalyAlert base y devuelve explicación en lenguaje natural)

**Imports / Exports**
- `PreviewImportUseCase`, `CommitImportUseCase` (`Application/UseCases/Import/`, Fase 20; ver §11g). Sin perfiles de mapeo guardados: seguimiento documentado
- `CreateBackupUseCase`, `InspectBackupUseCase`, `RestoreBackupUseCase` (`Application/UseCases/Backup/`, Fase 19; respaldo completo y restauración; no son exportación de reportes, ver §11e)

**Settings**
- `GetSettingsQuery`, `UpdateSettingsUseCase` (tema, locale, configuración de IA)
- `TestAiConnectionUseCase` (smoke test del proveedor configurado)

### DTOs y mapeo

- DTOs en `Application/DTOs/`. Mapeo manual con métodos de extensión (`Expense.ToDto()`). **No** se usa AutoMapper en v1 — agrega magia y bugs difíciles de seguir.
- ViewModels en UI consumen DTOs, **no** entidades de dominio, para evitar que EF Core se filtre a la capa de UI.

### Validación

- **FluentValidation** con un validador por comando (ej. `RegisterExpenseValidator`).
- Validación previa en el use case; la UI también puede llamar al validador para feedback en vivo antes de enviar.

### Patrones transversales

- **`Result<T>`** — tipo discriminated union-like (`Success`/`Failure` con código y mensaje). Para errores esperados.
- **`PagedResult<T>`** — para listados largos.
- **`IClock`** inyectado en use cases que dependen del tiempo (generación de recurrentes, evaluación de presupuestos, alertas).

---

## 8. Capa de Infraestructura

### Persistencia (EF Core + SQLite)

- **`AppDbContext`** único, registrado como **scoped**. Tiene un `DbSet<>` por entidad de dominio.
- **Mapeo** vía clases `IEntityTypeConfiguration<T>` en `Persistence/Configurations/`. Las entidades de dominio quedan limpias de atributos de EF.
- **Migraciones versionadas** en `Persistence/Migrations/`, commiteadas. Cambios destructivos requieren backup automático previo.
- **Conversores de value objects** (Money, DateRange) registrados en el `OnModelCreating` para mapear a columnas primitivas.
- **Snake_case** o **PascalCase** en columnas: se fija **PascalCase** (consistente con C#) salvo decisión explícita en ADR.
- **Índices**: por fecha, por categoría, por tag, por recurrente origen. Se definen en las configuraciones.

### Repositorios

- Implementaciones en `Persistence/Repositories/`. Una por interfaz de Aplicación.
- Patrón: `Add`, `Update`, `Remove`, `GetById`, queries específicas (`GetByMonthAsync`, `GetActiveSubscriptionsAsync`).
- **No** se usa el patrón genérico `<TEntity, TId>`; cada repo expone métodos específicos del dominio.
- **`IUnitOfWork`** envuelve `SaveChangesAsync` para coordinar varios repos en una sola transacción. Implementación trivial sobre el DbContext.

### Settings

- **`SettingsStore`** (implementa `ISettingsStore`) lee/escribe en la tabla `Setting` con helpers tipados (`GetStringAsync`, `GetEnumAsync<T>`, `SetAsync<T>`).
- La API key de IA se guarda como string. En v1: en claro, con **advertencia visible** en la UI de que el archivo de datos contiene secrets. Cifrado con DPAPI en Windows (keychain en macOS, libsecret en Linux) queda como **ADR pendiente** para v2.

### Filesystem

- **`PathResolver`** (implementa `IPathResolver`) devuelve el directorio de datos según el SO, definido en `tech-stack.md`.
- **`SqliteBackupService`** (`Infrastructure/Backup/`, implementa `IBackupService`; Fase 19, implementado) exporta la base a un `.zip` con `manifest.json` + `data.db`. La restauración valida todo el archivo antes de tocar la base viva. Detalle en §11f y §12 «Backup».
- **Importación CSV / Excel (Fase 20, implementado):** `Infrastructure/Import/` contiene `CsvTableReader` (con `CsvRecordParser`), `ClosedXmlTableReader`, `SniffingTabularReader` (el único `ITabularReader` registrado; elige el lector por el contenido), `EfImportLookup` y `EfImportWriter`, más `AddImport()`. La lógica de interpretación (parsers, validación, categorías, duplicados, plan) es pura y vive en Application; ver §11g. No existen `ICsvImporter`, `CsvImporter`, `ImportCsvUseCase` ni `CsvImportProfile`.
- **Reportes PDF / XLSX (Fase 18, implementado):** `QuestPdfReportWriter` y `ClosedXmlReportWriter` en `Infrastructure/Reports/` implementan `IReportWriter`; ver §11e. No existe `CsvExporter`: la Fase 18 no produce CSV (el backup completo es la Fase 19, distinto de un reporte).

### LLM clients

- Estructura en `Llm/` (ver sección 10).

### Logging

- Helper `LoggerSetup` que configura proveedores según el entorno (consola en dev, archivo rotativo en prod). Se invoca desde el composition root.

---

## 9. Capa de UI (Presentación)

### Estructura

- **`Program.cs`** — entry point, crea el host genérico (`Microsoft.Extensions.Hosting`), configura DI, logging y arranca Avalonia.
- **`App.axaml` / `App.axaml.cs`** — `Application` raíz, carga tema, registra `DataTemplate`s que asocian View ↔ ViewModel, resuelve el `ShellViewModel` inicial.
- **`MainWindow.axaml`** — ventana única con layout de shell (sidebar + header + content + status bar).
- **`Views/`** — UserControls (`DashboardView`, `ExpensesView`, `DebtsView`, `GoalsView`, `BudgetsView`, `SubscriptionsView`, `FixedExpensesView`, `RecurringView`, `SettingsView`, `ImportView`, `ChatView`). La exportación (Fase 18) no tiene vista propia: son botones "Exportar PDF / Excel" en `DashboardView` y `ExpensesView`.
- **`ViewModels/`** — uno por vista. Heredan de `ObservableObject` (CommunityToolkit).
- **`Navigation/`** — `INavigationService` + `NavigationService` + `RouteNames` (constantes).

### Composition root

- En `App.axaml.cs` (o un `Bootstrapper` aparte) se hace **toda** la registración de servicios. Aquí es el único lugar donde UI referencia a Infrastructure:
  - Se registra `AppDbContext`, repos, `ILLMClient` con su factory por proveedor, `IClock`, `IPathResolver`, `ISettingsStore`, `IBackupService`, y los puertos de importación (`AddImport()`: `ITabularReader`, `IImportLookup`, `IImportWriter`).
  - Se registran los use cases y los ViewModels (transient por defecto; los ViewModels con estado scoped, scoped).

### Patrón MVVM en la práctica

- Bindings con `CompiledBindings` activados (más rápidos, errores en compilación).
- `DataTemplate`s en `App.axaml` que mapean `TipoDeViewModel → View`. La navegación funciona cambiando el `CurrentViewModel` del shell; el `ContentControl` resuelve la vista automáticamente.
- **Comandos** con `[RelayCommand]` en los ViewModels. Sin `ICommand` manual.
- **Mensajes** entre ViewModels desacoplados vía `WeakReferenceMessenger` (CommunityToolkit) para casos puntuales (ej. "se guardó un gasto, refrescar dashboard").

---

## 10. Navegación

### Patrón elegido

- **Navegación por contenido en una sola ventana**, no multi-ventana. Más simple, mejor UX para una app personal.
- `ShellViewModel` expone `CurrentViewModel` (bindado a `ContentControl`).
- `INavigationService` se inyecta en los ViewModels de feature y permite:
  - `NavigateTo(RouteNames.Dashboard)`
  - `NavigateTo<ExpensesViewModel>(RouteNames.Expenses, vm => vm.ApplyDayFilter(day))` — navegación
    parametrizada (F-009): la ruta se resuelve por `RouteNames`, y el view-model de destino se
    configura mediante una `Action<TViewModel>` antes de convertirse en `CurrentViewModel`.
  - `GoBack()` (con pila de historial en memoria)
- **`INavigationAware` (Fase 15, opt-in):** `OnNavigatedTo()`/`OnNavigatedFrom()`, invocados por
  `NavigationService` en `NavigateCore` y `GoBack` alrededor del cambio de `CurrentViewModel`. Existe
  porque `GoBack()` reactiva la MISMA instancia de ViewModel que ya estaba en la pila de historial, en
  vez de resolver una nueva desde el contenedor DI — para un ViewModel con estado que debe limpiarse
  al salir de pantalla (la conversación en memoria de `ChatViewModel`, F-015), un registro `Transient`
  por sí solo no alcanza, porque `GoBack` nunca vuelve a pedirle una instancia al contenedor. El hook
  es no-op para cualquier ViewModel que no lo implemente (verificado: solo `ChatViewModel` lo hace hoy).

### Detalle de un movimiento (modal)

- Para detalles (ver/editar un gasto, una deuda) se usa una **ventana hija** o un `DialogHost` de Avalonia, no navegación apilada en el shell. La razón: la edición es transaccional; el shell no debería perder el contexto del usuario.

### Deep links internos

- En v1: no hay deep links externos. Internamente, los ViewModels pueden pedir navegación por `RouteNames` o por tipo de ViewModel.

### Por qué no multi-ventana

- Una app de finanzas personal no se beneficia de múltiples ventanas. El usuario hace tareas secuenciales, no en paralelo.
- Multi-ventana complica state, foco, atajos, navegación y testing.

---

## 11. Integración de IA — `ILLMClient`

**Fases 13, 14, 15 y 16, implementado.** Lo que sigue documenta el contrato y los adapters realmente
shippeados — `Application/Abstractions/Llm/`, `Infrastructure/Llm/OpenAICompatibleClient.cs`,
`Infrastructure/Llm/AnthropicClient.cs`, `Infrastructure/Llm/LLMClientFactory.cs` — no un plan a
futuro. El primer consumidor (Fase 13) es `CategorizeExpenseUseCase` (sugerencia de categoría al
tipear la descripción de un gasto); Fase 14 reutiliza el mismo `ILLMClient`, sin ninguna
modificación, para `GetInsightsUseCase`/`GetAnomalyNarrativeUseCase` (ver §11a); Fase 15 lo reutiliza
una tercera vez, también sin ninguna modificación, para `AskFinanceChatUseCase` (ver §11b); Fase 16 lo
reutiliza una cuarta vez, otra vez sin ninguna modificación, para `GenerateSavingsPlanUseCase` (ver
§11c). Ningún archivo bajo `Infrastructure/Llm/` cambió en Fase 14, Fase 15 ni Fase 16 salvo la
adición aditiva de nuevos recursos de prompt (8 en Fase 14, 2 en Fase 15, 4 en Fase 16) — el contrato
`ILLMClient`/`LlmRequest`/los adapters/la resiliencia llevan cuatro fases sin tocarse.

### Contrato

`ILLMClient` vive en `Application/Abstractions/`. Métodos:

- `Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct)`
- `Task<LlmConnectionOutcome> TestConnectionAsync(CancellationToken ct)` — smoke test para el botón "Probar conexión" de Settings. **Actualizado en F-013, slice 7**: ya no es `Task<bool>` — devuelve un `LlmConnectionOutcome` de tres estados (`Ok` / `InvalidCredentials` / `Unavailable`), porque el criterio de completitud del roadmap (c) exige que el botón distinga "tu key es inválida" de "el proveedor no responde", algo que un `bool` no puede expresar. Nunca lanza; `AiNotConfigured` no es un miembro del enum porque esa decisión la toma la factory (`LlmConfigurationStatus`) antes de que exista un cliente con el cual hacer el round trip.
- `string ProviderName { get; }` (para mostrar en la UI)

`LlmRequest` lleva:
- `Messages` (lista de `LlmMessage` con `Role` y `Content`).
- `Model` (opcional, usa el de settings si null).
- `JsonSchema?` (opcional, para forzar salida estructurada).
- `Temperature`, `MaxTokens`.
- `Metadata` (correlation id, etc.).

`LlmResponse` lleva:
- `Content` (texto o JSON ya deserializado).
- `TokensUsed` (para mostrar en UI y posibles límites).
- `LatencyMs`.

### Adapters

- **`OpenAICompatibleClient`** (base) — maneja el formato OpenAI. Es reusable para OpenAI, OpenRouter, y cualquier proveedor compatible.
- **`AnthropicClient`** — implementación específica para Messages API de Anthropic.
- **Factory** `ILLMClientFactory` lee el proveedor de settings y devuelve el adapter concreto.
- `HttpClient` inyectado con `IHttpClientFactory` para timeouts, retries y telemetría interna.

### Configuración

- Settings: `AiProvider` (enum: OpenAI, Anthropic, OpenRouter, Custom), `AiApiKey` (string), `AiModel` (string), `AiBaseUrl?` (para custom compatible), `AiTemperature` (double), `AiMaxTokensPerRequest` (int).
- El usuario edita esto en `SettingsView`. Un botón **"Probar conexión"** invoca `TestAiConnectionUseCase`.

### Prompts

- Los prompts viven como **recursos** en Infrastructure (`Llm/Prompts/*.md` o `.txt`). Plantillas claras, con placeholders para los datos del usuario.
- **Salida estructurada**: cuando aplique (categorización, plan de ahorro), se envía un `JsonSchema` o se parsea la respuesta con un parser tolerante. Un parser tolerante es más robusto ante modelos gratuitos que no soportan JSON mode estricto.

### Resiliencia

- **`Microsoft.Extensions.Http.Resilience`** (Polly por debajo) para timeouts, reintentos con backoff y
  circuit breaker, registrado vía `AddLlmClients` (`Infrastructure/Llm/LlmServiceCollectionExtensions.cs`).
  Cliente principal (`llm-{provider}`): timeout total 15 s, timeout por intento 6 s, 1 reintento (2
  intentos totales) con backoff exponencial jitterizado a 1 s, breaker con ratio de fallo 0.5,
  throughput mínimo 4, ventana de muestreo 30 s, duración de apertura 15 s. El predicado de retry por
  defecto NO reintenta 400/401/403 — una key inválida falla en ~1 s, no agota el presupuesto de
  reintentos. Cliente de diagnóstico (`llm-{provider}-diagnostic`, para "Probar conexión"): solo
  timeout, sin retry ni breaker — nombrado aparte para que una key recién corregida nunca choque con un
  breaker abierto por la anterior.
- **Falla silenciosa**: si el cliente LLM lanza, el use case captura, loguea a nivel Warning con el correlation id, y devuelve un `Result.Failure` con código `LLMUnavailable` (o `AiNotConfigured` si no hay key configurada — cero llamadas HTTP en ese caso). La UI muestra "IA no disponible" sin romper la pantalla; el formulario sigue usable.
- **Timeout duro**: 15 s por request, mas un timeout de 15 s con `CancellationTokenSource` enlazado en el propio use case como defensa en profundidad (se mantiene incluso si un futuro proveedor se registrara sin el handler de resiliencia).

### Por qué `ILLMClient` y no un SDK por proveedor

- Los SDKs oficiales cambian de versión y rompen compatibilidad. Un contrato interno desacoplado nos da control total.
- Modelos gratuitos en OpenRouter no siempre tienen SDK oficial; usar HTTP directo cubre esos casos.
- Mockear `ILLMClient` en tests es trivial; mockear SDKs concretos es más frágil.

---

## 11a. IA en el dashboard — insights, forecast y anomalías (Fase 14)

**Fase 14, implementado.** El principio arquitectónico de esta fase, que gobierna cada decisión de
abajo: **los números los calculan servicios de Dominio puros y use cases determinísticos de
Application; la IA recibe cifras ya formateadas y devuelve prosa. Ningún número que se muestre en el
dashboard sale nunca de texto generado por un LLM.**

### Detección determinística de anomalías

`Domain/Alerts/AnomalyDetector.cs` — servicio estático puro, cero I/O, cero conocimiento de DI. Para
cada gasto candidato de una categoría elegible (`count >= 8` gastos históricos Y `distinctMonths >= 3`
en los 6 meses completos previos), calcula el z-score robusto de Iglewicz-Hoaglin
(`0.6745·(x−median)/MAD`) sobre montos ya normalizados a DOP por el caller. Umbral `>= 3.5`
(redondeado, nunca sobre el valor crudo) con severidad `Info`/`Warning`/`Critical` en
`[3.5,5)`/`[5,8)`/`[8,∞)`. Cuando `MAD = 0` (historial con montos idénticos), cae a una regla de razón
(`x >= 3·median → Warning`) que nunca divide por cero. Unilateral: un gasto barato nunca se marca.

### Pronóstico de fin de mes determinístico

`Domain/Forecasting/MonthEndForecaster.cs` — servicio estático puro. `proyectado = gastado_a_la_fecha +
días_restantes × tasa_diaria`, donde `tasa_diaria` mezcla el run-rate del mes actual con el promedio
diario histórico de los 3 meses completos previos, ponderado por `elapsed/daysInMonth`. Sin historial
y `elapsed < 3` expone un estado explícito `InsufficientData` en vez de una cifra engañosa. Estados
adicionales `ClosedMonth` (mes pasado: proyectado = real) y `FutureMonth` (proyectado = null). La
comparación contra presupuesto reutiliza `BudgetEvaluator.Evaluate` de la Fase 10 — una sola fuente de
verdad para los umbrales Cerca/Excedido.

### Persistencia idempotente de alertas

`AnomalyAlert` (endurecida: `sealed class`, setters privados, factory `Create`, `Refresh`,
`Acknowledge` idempotente) se persiste con un índice único sobre `TransactionId` — la clave de
idempotencia — y **sin FK** a `Expense`, para que la alerta sobreviva al borrado del gasto que la
originó hasta la siguiente reconciliación de su mes. `DetectAnomaliesUseCase` reconcilia en cada carga
del dashboard: agrega hallazgos nuevos, refresca los que cambiaron (preservando el `Id` estable, para
que la Fase 17 pueda anclar "ya notificado"), elimina los que ya no califican, y **nunca modifica ni
recrea una alerta reconocida**. `Severity` se persiste como `TEXT` (`HasConversion<string>()`), una
desviación deliberada de la convención int-enum del resto del proyecto — ver gotcha #23 en
`docs/AI_HANDOFF.md`. `AnomalyAlertRepository.ApplyReconciliationAsync` aplica el plan completo
(agregar/actualizar/eliminar) en un único `SaveChangesAsync` atómico, y ante un `DbUpdateException`
desengancha solo las entradas que él mismo adjuntó (`Entry(x).State = Detached`) — nunca
`ChangeTracker.Clear()`, que envenenaría el `AppDbContext` compartido y process-wide (`DashboardViewModel`
es transient, resuelto desde el root provider).

### Narración de IA: éxito garantizado, nunca una excepción

`GetInsightsUseCase`/`GetAnomalyNarrativeUseCase` copian el esqueleto de guardas de
`CategorizeExpenseUseCase` (Fase 13) línea por línea — `ISettingsStore` → `ILLMClientFactory.Resolve`,
CTS enlazado de 15 s, correlation id — con una diferencia deliberada: **todo camino no-feliz es
`Result.Success` con un payload de repliegue, nunca `Result.Failure`.** Una tarjeta del dashboard
siempre debe renderizar algo. No configurado → cero llamadas HTTP (verificado estructuralmente: la
rama de "no configurado" retorna antes de construir el `LlmRequest`). Narración de anomalías en UNA
sola llamada por lote (`AnomalyNarrationParser`, tolerante, independiente del parser de la Fase 13 —
R-9 prohíbe tocarlo), narrando como máximo las 5 anomalías de mayor puntaje con claves ordinales — el
GUID real nunca llega al modelo.

### Composición del dashboard: paso determinístico separado del paso de narración

`GetDashboardInsightsQuery` es la única query compuesta nueva, con dos operaciones separadamente
esperables: `GetDeterministicAsync` (pronóstico + detección de anomalías + cifras de insights,
**secuencial**, cero `ILLMClient`/`ISettingsStore`) y `GetNarrationAsync` (insights + narración de
anomalías, `Task.WhenAll`, como máximo 2 llamadas concurrentes por carga). El paso determinístico corre
secuencialmente y no en paralelo porque la detección escribe (vía `ApplyReconciliationAsync`) y el
`AppDbContext` compartido no debe ver una escritura solaparse con una lectura. `DashboardViewModel`
gana un único parámetro de constructor nuevo; `LoadAsync` nunca espera la narración — la dispara sin
`await` después de aplicar el paso determinístico, protegida por un token de generación que descarta en
silencio cualquier resultado supersedido por un cambio de mes.

---

## 11b. Chat con IA sobre tus finanzas (Fase 15)

**Fase 15, implementado.** Mismo principio de la §11a, aplicado a una pregunta libre en vez de un
dashboard fijo: **el contexto que ve el modelo lo calcula código determinístico de Application antes
de la llamada; el LLM solo elige y redacta cifras que ya se le entregaron. El panel de cifras exactas
se renderiza siempre desde el DTO, nunca del texto del LLM.**

### Construcción del contexto: composición, nunca acceso directo a repositorios

`BuildChatContextQuery` compone SEIS queries de Application ya existentes (resumen de mes,
desglose de categorías, presupuesto vs. real, metas, deudas + `GetDebtScheduleQuery`, pagos
próximos) — nunca toca un repositorio directamente. Corre **secuencialmente**, mismo razonamiento de
§11a (el `AppDbContext` compartido es efectivamente de proceso completo para los ViewModels
transitorios de esta app). Cada una de sus 7 secciones tiene su propio `try/catch`: una falla deja esa
sección en `null` ("no disponible para el modelo") sin invalidar las demás — la distinción entre
`null` (no disponible) y una lista vacía (`[]`, "no hay elementos") es deliberada, porque el modelo
debe poder decir honestamente "no tienes metas activas" (lista vacía) en vez de "no tengo ese dato"
(sección `null`). El desglose de categorías se pide **sin folding** (`topCount = int.MaxValue`, nunca
el `TopCategories = 5` del dashboard) para que ninguna categoría quede oculta más allá del top-5 — el
propio builder recorta a 30/10 líneas nombradas más una línea de desbordamiento con el conteo exacto.

### Renderizado: presupuesto de caracteres garantizado por construcción

`ChatContextRenderer` — clase estática pura, sin registro DI, mismo estilo que
`AnomalyNarrationParser` (§11a) — formatea el snapshot con la cultura real del locale (nunca un
separador escrito a mano), sanea cada nombre de dominio (colapsa caracteres de control, elimina una
racha inicial de `#`/`!` para que un nombre de categoría o meta no pueda inyectar un falso marcador de
sección/bandera dentro del bloque de contexto) y sostiene un tope de 6,000 caracteres recortando
secciones completas en un orden fijo y probado (pagos → deudas → metas → presupuestos → categorías
del mes anterior) cuando hace falta — el resumen y la lista de categorías del mes actual nunca se
recortan.

### Caso de uso: mismo esqueleto de guardas, resultado siempre exitoso

`AskFinanceChatUseCase` copia el esqueleto de guardas de `CategorizeExpenseUseCase` línea por línea
(`ISettingsStore` → `ILLMClientFactory.Resolve`, CTS enlazado de 15 s, correlation id) con la misma
diferencia deliberada que la Fase 14: **todo camino no-feliz es `Result.Success(ChatReply)`, nunca
`Result.Failure`.** El chequeo de longitud de la pregunta (1-500 caracteres) precede incluso la
lectura de settings; "no configurado" precede tanto la construcción del contexto como la llamada
HTTP — cero lecturas de BD desperdiciadas, no solo cero llamadas HTTP. `Temperature`/`MaxTokens` se
acotan (`Math.Min`) independientemente de lo configurado en `AppSettings`. La historia re-transmitida
se limita a los últimos 3 intercambios, descartando el par más antiguo primero cuando excede 4,000
caracteres — nunca un mensaje suelto sin su par, para mantener la alternancia User/Assistant que
exige la API.

### Limpieza de conversación: el hook `INavigationAware`

La conversación vive solo en memoria (sin tabla, sin migración). `NavigationService.GoBack()`
reactiva la misma instancia de `ChatViewModel`, así que un registro `Transient` por sí solo no
garantiza una conversación limpia al volver de Configuración — de ahí el hook `INavigationAware`
descrito en §10, cuyo `OnNavigatedFrom()` cancela cualquier pregunta en vuelo y limpia la
conversación y el panel de cifras.

---

## 11c. Plan de ahorro con IA para metas (Fase 16)

**Fase 16, implementado.** Mismo principio de §11a/§11b, aplicado a una única cifra por meta en vez
de un dashboard o una conversación libre: **`SavingsPlanCalculator` (Fase 11, sin modificar) calcula
la cifra determinista; el LLM solo explica la cifra que ya se le entregó. La tarjeta de la meta
nunca depende de la IA — solo la narración puede faltar.**

### Recalculo server-side, nunca confía en la ViewModel

`GenerateSavingsPlanUseCase` toma únicamente el `GoalId` de la meta — nunca una cifra calculada por
la ViewModel — y recarga la meta y sus contribuciones desde los repositorios en cada clic. Recalcula
exactamente como `GetGoalsQuery`: `Goal.CalculateProgress(contributions)` seguido de
`SavingsPlanCalculator.Calculate(..., IClock.Today)`. El chequeo de "meta no encontrada" precede la
lectura de `AppSettings`, así que se reporta incluso con la IA apagada — el único desenlace que
produce `Result.Failure` en todo el caso de uso.

### Estado explícito: solo las cifras del estado seleccionado viajan al modelo

El plan recalculado se clasifica en exactamente un estado — `Live`, `AlreadyMet`, o
`TargetDateInPast` (`AlreadyMet` gana cuando ambos aplican, igual que `GoalRow.ShowAlreadyMetMessage`
en Fase 11) — y solo las cifras definidas para ese estado se insertan en el prompt: `Live` recibe
aporte mensual/meses restantes/monto restante/monto objetivo; `AlreadyMet` recibe solo monto
objetivo/monto aportado; `TargetDateInPast` recibe solo monto restante/fecha objetivo. Ningún estado
puede filtrar la cifra de otro porque el bloque de texto se construye con un `switch` por estado, no
con un objeto compartido con campos opcionales.

### Guard numérico determinista: el LLM nunca inventa una cifra sin ser detectado

`SavingsPlanNarrationGuard` (clase estática pura, mismo estilo que `AnomalyNarrationParser`/
`ChatContextRenderer`) extrae cada racha de dígitos ASCII de la narración final y la rechaza —
cayendo a `Unavailable` — si alguna racha no aparece entre las rachas de las cifras realmente
enviadas MÁS el nombre saneado de la meta (sin esto último, una meta llamada "Carro 2027" rechazaría
toda narración que la mencione). Ceros a la izquierda se normalizan (`05` ≡ `5`); cualquier dígito
no-ASCII (ancho completo, superíndices, fracciones) rechaza de inmediato. Es una verificación de
pertenencia, no de posición — una limitación documentada y aceptada en el diseño, no un defecto.

### `GoalRow`: de record inmutable a estado observable por fila

`GoalRow` pasó de `record` a `ObservableObject` para poder actualizar `PlanNarration`/
`PlanNarrationStatusText`/`IsGeneratingPlan` en el lugar, sin reconstruir la fila en cada tecla de
una solicitud en vuelo. El constructor de 13 parámetros mantiene sus nombres PascalCase exactos
(supresión `IDE1006` acotada) para que las pruebas existentes con argumentos nombrados sigan
compilando. `GoalsViewModel.RequestPlanCommand` usa
`[RelayCommand(AllowConcurrentExecutions = true)]` — el valor por defecto (`false`) comparte un único
`IsRunning` entre todas las invocaciones, lo que deshabilitaría el botón de TODAS las filas mientras
una sola está generando; la guarda real "a lo sumo una solicitud por fila" vive en el propio
`GoalRow.IsGeneratingPlan`, no en el comando.

---

## 11d. Notificaciones in-app (Fase 17)

**Fase 17, implementado.** A diferencia de §11a–§11c, esta fase **no usa `ILLMClient` ni genera
texto con IA**: toda regla reutiliza una lectura existente y un umbral existente (deudas, reglas
recurrentes, presupuestos, `AnomalyAlert`), y una tabla `Notification` persistida se reconcilia en cada
corrida de detección, siguiendo el patrón de `AnomalyAlert` de la Fase 14.

### Detección determinista y reconciliación idempotente

`DetectNotificationsUseCase` lee cada fuente habilitada de forma secuencial (nunca `Task.WhenAll`, porque
todas comparten el mismo `AppDbContext` del scope) y arma candidatos con funciones puras
(`NotificationRules`, `DigestComposer`). `NotificationReconciler` planifica agregar / eliminar /
refrescar hechos / purgar contra las filas existentes, y `NotificationRepository.ApplyReconciliationAsync`
aplica el plan completo en un único `SaveChangesAsync`; ante un `DbUpdateException` desengancha solo las
entradas propias, nunca `ChangeTracker.Clear()`. La clave de idempotencia es una única cadena `DedupKey`
con índice único (no un índice compuesto, por la semántica de NULL distintos de SQLite): una fila
descartada nunca se recrea, una fila sin descartar cuyo origen deja de calificar se elimina, y los hechos
de filas sin descartar se refrescan sin tocar `Id`/`CreatedAt`/`ReadAt`. Las filas con fecha de
referencia de más de 60 días se purgan en el mismo guardado. `Notification` guarda solo hechos; el texto
se formatea al mostrarse (`NotificationTextFormatter`, resx `es-DO`/`en`).

### Cuándo corre: scope propio, fire-and-forget, todo fallo tragado

`ScopedNotificationCenter` abre un scope de DI propio (un `AppDbContext` fresco) por corrida. Corre al
abrir la ventana principal (`mainWindow.Opened`), al guardar la sección "Notificaciones" de
Configuración y con el botón de refresco manual del popup; no hay timer. Una corrida manual mientras otra
está en vuelo es un no-op; las de arranque y de guardado de configuración se encolan y corren una vez
después. Toda excepción se registra y se traga: nunca bloquea ni tumba el arranque. Captura
`SynchronizationContext.Current` en su constructor para publicar `Changed` en el hilo de UI.

### Superficie en el shell: sin ruta nueva

`ShellViewModel` es dueño de la campana, la insignia y el estado abierto/cerrado; la lista es un
`NotificationListViewModel` agnóstico de ruta (reutilizable por una futura página "ver todas"). La
insignia muestra el conteo completo de no leídas (`9+` por encima de 9); el popup (un `ToggleButton` +
`Popup`, no `Button.Flyout`) lista como máximo 20 filas, no leídas primero, y abrirlo marca como leídas las
filas listadas. Descartar es una acción distinta, por fila y permanente. Sin click-through en v1.

### Resumen periódico

El `Digest` (`Off`/`Daily`/`Weekly`, por defecto Weekly) lo genera la primera corrida en o después del
inicio del período cuando el cursor `yyyy-MM-dd` (guardado vía `ISettingsStore` solo tras un reconcile
exitoso) va por detrás. Nunca rellena períodos perdidos; con todos los conteos en cero avanza el cursor sin
crear fila; los totales se mantienen por moneda (nunca se suman entre monedas); ignora los interruptores por
tipo y los descartes porque tiene su propio apagado.

---

## 11e. Exportación de reportes a PDF y Excel (Fase 18)

**Fase 18, implementado y archivado (2026-09-29).** Como la Fase 17, no usa `ILLMClient`: el reporte es una
proyección determinista de lecturas existentes (`GetMonthSummaryQuery`, `GetCategoryBreakdownQuery`,
`GetBudgetVsActualQuery`, el repositorio de gastos y `ICurrencyConverter`), sin reglas de negocio nuevas,
sin migración y sin claves de configuración nuevas.

### Puertos y DTOs

- `IReportWriter` (Application/Abstractions): `Format` (`ReportFormat.Pdf`/`Xlsx`) más
  `WriteMonthSummaryAsync` y `WriteExpenseListAsync`; recibe un DTO ya calculado, un `ReportLabels` y un
  `Stream`, y devuelve `Result`. No consulta datos ni lee recursos.
- `IFileSaveDialog` (Application/Abstractions): `PickAsync` devuelve un `SaveTarget?` (`null` = el usuario
  canceló, no es un error). `SaveTarget` abre el stream de forma perezosa y no expone tipos de Avalonia.
- DTOs en `Application/DTOs/Reports/`: `MonthSummaryReport`, `ExpenseListReport`, `ReportTransactionLine`,
  `ReportTransactionKind`, `ReportFilterSummary` (descripción del filtro de la lista: rango de fechas,
  nombre de categoría y cantidad de etiquetas), `ExportReportResult`. Todo el texto visible llega en `ReportLabels`, armado por
  `ReportLabelsFactory` (UI) desde resx `es-DO`/`en`.

### Casos de uso y salida en memoria

`ExportMonthSummaryReportUseCase` y `ExportExpenseListReportUseCase` (`Application/UseCases/Exports/`)
construyen el DTO, eligen el writer registrado para el `ReportFormat` (`ReportOutput.PickWriter`) y guardan.
La lista de gastos se lee completa (todas las páginas, de a 200, `ReportPaging`), con el filtro capturado
antes de abrir el diálogo. `ReportOutput.RenderAndSaveAsync` renderiza primero a un `MemoryStream` y solo
después abre y escribe el destino, de modo que un render fallido nunca crea ni trunca el archivo. Los fallos
son valores `Result` con códigos estables (`ReportWriterUnavailable`, `ReportDataFailed`,
`ReportRenderFailed`, `ReportSaveFailed`; el de gastos añade `InvalidFilter`); solo se propaga la cancelación.

### Writers (solo Infraestructura)

- `QuestPdfReportWriter`: QuestPDF 2026.9.1 con licencia Community (personal, sin ingresos); la versión está
  fijada de forma exacta y `Directory.Packages.props` lleva un banner que exige releer la licencia en cada
  actualización.
- `ClosedXmlReportWriter`: ClosedXML 0.105.1 (MIT). Libro con hoja `Summary` con estilo y hoja plana `Data`
  con encabezados invariantes en inglés.
- `AddReportExporters()` registra ambos como `IReportWriter` (singletons) y aplica la configuración global de
  QuestPDF una sola vez en `QuestPdfConfiguration` (licencia; `ThrowOnMissingTextGlyphs = false`). Application
  no referencia QuestPDF ni ClosedXML (NetArchTest).

### Superficie de UI

`AvaloniaFileSaveDialog` (UI/Services) implementa `IFileSaveDialog` sobre `TopLevel.StorageProvider`.
`DashboardViewModel` expone `ExportPdfCommand`/`ExportExcelCommand` (mes mostrado) y `ExpensesViewModel` los
mismos comandos (filtro actual, ignorando `PaymentMethodIds` porque la lista visible tampoco lo aplica). Un
único `IsExporting` compartido es la guarda contra doble invocación; cancelar el diálogo no escribe nada ni
muestra mensaje. En `ExpensesViewModel` el caso de uso y el diálogo son opcionales (`CanExport` los exige).

### Seguimientos documentados (fuera de esta fase)

Exportación de Deudas (~250 líneas estimadas) y salida CSV: no implementadas.

---

## 11f. Backup y restauración (Fase 19)

**Fase 19, implementada y archivada (2026-09-29).** Como las Fases 17 y 18, no usa `ILLMClient`, no añade migración, paquete
ni clave de configuración. El respaldo es **distinto** de los reportes de la Fase 18: copia toda la base para poder
recuperarla, no proyecta datos para leerlos.

### Puertos y casos de uso

- Application: `IBackupService`, `IMigrationCatalog`, `IFileOpenDialog` (con `OpenTarget`, `FileTypeFilter`), la
  sobrecarga genérica de `IFileSaveDialog.PickAsync(nombre, FileTypeFilter, título)`, y los registros `BackupManifest`,
  `BackupInfo`, `BackupCreated`, `RestoreOutcome`. Códigos de error estables en `BackupErrorCodes`: `NotABackup`,
  `Corrupt`, `NewerSchema`, `IoError` (la falta de espacio es `IoError` con `Details["reason"] = "insufficientSpace"`).
- `CreateBackupUseCase`, `InspectBackupUseCase` y `RestoreBackupUseCase` (`Application/UseCases/Backup/`) delegan en
  `IBackupService`, convierten cualquier excepción inesperada en `Result.Failure(IoError)` con log, y solo propagan la
  cancelación.
- UI: `IAppRestartService`/`AppRestartService` (relanza el ejecutable y cierra el actual; si no puede, la UI muestra
  el texto manual), `AvaloniaFileOpenDialog` y `BackupSettingsViewModel` (singleton, máquina de estados
  `Idle → Exporting | Inspecting → ConfirmPending → Restoring → RestartRequired`, este último terminal en la sesión).

### `SqliteBackupService` (Infrastructure/Backup)

Infrastructure es la única capa que usa `Microsoft.Data.Sqlite` directamente (una regla NetArchTest lo exige para
Application y UI). Se registra con `AddBackup(appVersion)` como singleton y `IBackupService` resuelve a la misma instancia: su compuerta de
un solo vuelo serializa create/inspect/restore. Colaboradores: `IBackupFileSystem`/`PhysicalBackupFileSystem` (permite
inyectar fallos en tests), `EfMigrationCatalog`, `BackupOptions` (topes de tamaño, esperas de reintento, 3 copias de
seguridad).

- **Crear:** instantánea con la API `BackupDatabase` (consistente aunque la app escriba en modo WAL), en un directorio
  de trabajo `<DataDir>\backups\.work\<guid>\`. Sobre la copia se comprueba la integridad, se quita la API key
  (`secure_delete` + `VACUUM`, de modo que ningún byte de la clave queda en el archivo), se normaliza el modo de
  journal y se calcula el SHA-256. El destino del usuario se abre **al final**, de modo que un fallo nunca lo crea ni
  lo trunca.
- **Inspeccionar:** valida forma del ZIP (exactamente `manifest.json` y `data.db`, sin rutas que escapen), topes de
  tamaño, manifiesto, SHA-256, `integrity_check`/`foreign_key_check` y la comparación del historial de migraciones.
  Trabaja siempre sobre copias; la base viva queda byte a byte igual tras cada rechazo.
- **Restaurar (orden del pipeline):** (1) revalida el archivo completo; (2) resuelve un journal caliente de la base viva;
  (3) normaliza la copia entrante (modo de journal sin WAL, arrastra la API key local si el backup no trae una);
  (4) escribe y **relee** una copia de seguridad `backups\pre-restore-yyyyMMdd-HHmmss.zip` (conserva la API key y nunca sale
  del directorio de datos; se conservan las 3 más recientes); (5) libera conexiones, pliega el WAL y exige que no queden
  `-wal`/`-shm`/`-journal`; (6) un único `File.Replace` con reintentos y espera creciente, reparando el invariante de
  nombres si Win32 falla a medias; (7) verifica por hash el archivo instalado y, si hay duda, revierte desde
  `data.db.pre-restore.bak`. La cancelación se respeta hasta publicar la copia de seguridad y se ignora después.
- **Ciclo de vida del `.bak`:** tras un restore correcto se conserva `data.db.pre-restore.bak`. En el siguiente arranque,
  `Bootstrap.ApplyMigrationsAndCompletePendingRestore` ejecuta `ApplyMigrations` y, solo si terminó, llama a
  `IBackupService.CompletePendingRestore()`, que borra el `.bak` (únicamente si la base viva abre, pasa `quick_check` y
  trae todas las migraciones de este build) y limpia `backups\.work`. Nunca borra las copias `pre-restore-*.zip` ni
  ningún otro archivo, es idempotente y no lanza excepciones (un fallo se registra y el arranque continúa).
- **Restore de un esquema más antiguo:** se acepta; el restore no migra. El siguiente arranque migra el archivo y solo
  entonces se libera el `.bak`.

### Suspensión de notificaciones

`INotificationCenter.SuspendAsync()` (implementado por `ScopedNotificationCenter`) espera a la ejecución en curso y
convierte en no-op todo miembro que toque la base mientras exista una suspensión. `BackupSettingsViewModel` la adquiere
antes de restaurar; se libera si el restore falla y se mantiene hasta que el proceso termina si tiene éxito.

### Directorio de datos alternativo para pruebas

`SystemPathResolver` respeta la variable de entorno `CONTROLDEFINANZASIA_DATA_DIR` cuando contiene una ruta absoluta
completa; en otro caso usa la ruta del SO de §12. Permite lanzar la app de pruebas (`dotnet run`) sin tocar los datos
reales del usuario.

### Pruebas y seguimientos

`BackupRestoreEndToEndTests` (Integration) prueba con los casos de uso y el servicio reales y bases SQLite temporales: el
roundtrip de la hoja de ruta (export desde A, instalación limpia, restore, todas las filas iguales salvo `ai.apiKey`),
el arrastre de la clave local, el rechazo de esquema futuro, el archivo corrupto, el esquema antiguo más `Migrate()`, la
liberación del `.bak` y el flujo vía `BackupSettingsViewModel`.

Fuera de esta fase: un aviso global «reinicio necesario» en el shell (otras pantallas pueden seguir escribiendo en el
archivo restaurado hasta reiniciar), la reversión automática ante una migración fallida tras un restore, el diálogo de
error de migración al arrancar, y las comprobaciones manuales con los diálogos nativos y el relanzamiento real.

---

## 11g. Importación CSV / Excel (Fase 20)

**Fase 20, implementado (2026-09-30; verificación y archivo pendientes en el Cierre).** Como las Fases 17 a 19, no usa
`ILLMClient`: es una canalización determinista sin migración, sin paquete nuevo y sin claves de configuración nuevas
(ClosedXML ya estaba fijado por la Fase 18; `CodePagesEncodingProvider` y `System.IO.Compression` son BCL). La prioridad
de diseño es **no corromper datos en silencio**: no se adivinan el separador decimal ni el orden día/mes, y una
importación escribe todo o nada.

### Piezas

| Capa | Tipos |
|---|---|
| Application/Abstractions | `ITabularReader`, `IImportLookup`, `IImportWriter`, `TabularReadOptions`; `IFileOpenDialog` gana una sobrecarga con varios filtros |
| Application/UseCases/Import/Common | `RawCell` (`Text`/`Number`/`Date`/`Error`), `RawRow`, `RawTable`, `ImportOptions`, `ColumnMapping`, `ImportPreview`, `ImportRowResult`, `ImportCommitResult`, `ImportBatch`, `ImportSource`, `ImportSourceLoader`, `ImportErrorCodes`, `ImportReason`, `ImportLimits` |
| Application/UseCases/Import/Parsing | `HeaderMapper`, `AmountParser`, `DateParser`, `ImportRowValidator`, `CategoryMatcher`, `CategoryResolver`, `DuplicateDetector`, `ImportText` (puros: sin reloj, cultura ni E/S) |
| Application/UseCases/Import | `ImportPipeline`, `ImportPlanner`, `ImportPlan`, `ImportMaterializer`, `PreviewImport/PreviewImportUseCase`, `CommitImport/CommitImportUseCase` |
| Infrastructure/Import | `CsvTableReader` (+ `CsvRecordParser`), `ClosedXmlTableReader`, `SniffingTabularReader`, `EfImportLookup`, `EfImportWriter`, `AddImport()` |
| UI | `ImportViewModel` (Transient), `ImportView`, alojados dentro de `ExpensesView` (sin ruta); `AvaloniaFileOpenDialog` con filtros CSV/TXT/XLSX |

`NetArchTest` exige que `CsvTableReader`, `ClosedXmlTableReader`, `SniffingTabularReader`, `EfImportLookup` y
`EfImportWriter` residan en `ControlDeFinanzasIA.Infrastructure.Import`, que nadie dependa de ese namespace salvo la
composición, y que Domain, Application y UI no referencien ClosedXML, EF Core ni SQLite. `System.IO.Compression` no
figura en esa lista: que Application no lo use se comprobó con una búsqueda de pureza en la Fase 20 (tarea 1.12), no con
una regla permanente.

### Canalización

`leer → interpretar → consultar → planificar → hash del plan → confirmar`

1. **Leer.** `ImportSourceLoader` copia el archivo elegido a memoria con tope de `MaxFileBytes` (10 MiB) más un byte,
   sin confiar en `Stream.Length`. `SniffingTabularReader` decide por el contenido, nunca por el nombre: `PK\x03\x04` →
   `ClosedXmlTableReader`; `D0 CF 11 E0` (`.xls` antiguo) → `UnreadableFile/legacyXls`; el resto → `CsvTableReader`.
   Límites aplicados durante la lectura: 50 000 filas de datos, 50 columnas, 32 768 caracteres por campo, y para XLSX
   100 MiB descomprimidos y 1 000 entradas (el ZIP se inspecciona con un flujo contador antes de abrir el libro).
   - CSV: decodificación UTF-8 estricta, con BOM UTF-8/UTF-16 respetado y **Windows-1252 como respaldo** (el respaldo
     se informa con un aviso y una muestra del texto); un NUL sin BOM se rechaza como binario. El delimitador (`,` `;`
     tab) se detecta sobre los primeros 20 registros ignorando las comillas y puede forzarse. Máquina de estados
     RFC 4180 (comillas dobladas, saltos de línea dentro de comillas); comilla sin cerrar → `UnreadableFile/malformedCsv`
     con la línea física.
   - XLSX: hoja `Data` (sin distinguir mayúsculas) o, si no existe, la primera; las celdas son tipadas (`Number`
     redondeado a 4 decimales, `Date`, `Text`; una fórmula usa su valor en caché y nunca se evalúa; un error de Excel
     es una celda `Error`).
2. **Interpretar.** `HeaderMapper` mapea por igualdad exacta de encabezados normalizados (sinónimos en inglés y
   español; `AmountDOP` se ignora a propósito). El paso de mapeo solo aparece si faltan Date, Description o Amount.
   `ImportRowValidator` recoge **todos** los motivos de una fila (no solo el primero) y produce un estado
   `Valid`/`Rejected`; los avisos (`FutureDate`, `AmbiguousAmount`, `CategoryFallback`, `TypeDefaulted`...) no rechazan.
3. **Consultar.** `IImportLookup` devuelve categorías, métodos de pago y las claves de gastos e ingresos ya guardados
   en el rango de fechas de las filas no rechazadas.
4. **Planificar.** `ImportPlanner` resuelve categorías, aplica el tope de categorías nuevas y marca duplicados.
5. **Hash del plan.** SHA-256 en minúsculas sobre `fila#:estado:huella|motivos` más los nombres de categorías nuevas,
   en orden. Vista previa y confirmación ejecutan el mismo `ImportPipeline`.
6. **Confirmar.** `CommitImportUseCase` relee los bytes retenidos con las opciones confirmadas, reconstruye el plan y,
   si su hash difiere del de la vista previa, devuelve `PreviewStale` sin escribir nada.

### Reglas de interpretación

- **Importes (texto).** Gramática fija y `decimal`, nunca la cultura actual: signo inicial o paréntesis, dígitos,
  un único separador decimal y agrupación de miles coherente. Se rechazan símbolos o letras (`$`, `RD$`, `DOP`),
  notación científica, más de 4 decimales (`TooManyDecimals`; nada se redondea en silencio), cero, y valores sobre
  `MaxAmount` (99 999 999 999 999,9999). Con estilo `Auto` se cuentan las pruebas de cada formato; `1.234,56` implica coma
  y `1,234.56` implica punto; sin pruebas se asume punto. Un único separador seguido de exactamente tres dígitos
  (`1,234`) es ambiguo: se lee como miles, con el aviso `AmbiguousAmount`, salvo que el usuario elija el estilo. Una
  mezcla de estilos en el archivo muestra un aviso y rechaza cada valor contradictorio. Las celdas `Number` de XLSX
  ignoran el estilo.
- **Signo y tipo.** Con `Unsigned` un importe negativo es `NegativeAmount` (nunca se vuelve positivo). Con
  `NegativeIsExpense` el signo decide el tipo e ignora la columna Type. Moneda: solo `DOP` y `USD`, por coincidencia
  literal.
- **Fechas.** ISO `yyyy-MM-dd` se acepta siempre (con hora o zona, la fecha se toma tal cual, sin desplazamiento). El
  resto exige uno de tres formatos elegidos por el usuario (`d/M/yyyy`, `M/d/yyyy`, `d-M-yyyy`, separadores literales,
  año de 4 dígitos); por defecto sigue la cultura de la UI. No existe «sin formato»: una fecha de texto que no coincide
  con el formato elegido es `InvalidDate`. Un serial de Excel en una celda `Number` es `InvalidDate` (no se adivina);
  una celda `Date` se lee exacta. Antes de 1900 se rechaza; una fecha futura se acepta con aviso. Si todas las fechas
  de texto son ambiguas o alguna contradice el formato, la vista previa lo indica.
- **Categorías.** Coincidencia ordinal, luego sin distinguir mayúsculas y por plegado de diacríticos; si varias
  categorías del catálogo colisionan tras el plegado el nombre es `AmbiguousCategory` (rechazo). Un gasto sin
  categoría, o sin coincidencia con creación desactivada, usa «Otros» por Id (`CategoryFallback`, aviso); un ingreso
  sin categoría queda `null` sin aviso. Con creación activada se crean categorías nuevas (color `#95A5A6`, sin icono,
  no sistema) con un tope de 50; la 51.ª se rechaza (`TooManyNewCategories`). Los nombres no se truncan. El método de
  pago solo se enlaza si existe; nunca se crea.
- **Duplicados.** Huella = fecha, tipo, importe (`0.0000`), moneda, descripción normalizada y categoría resuelta. Se
  cuentan **ocurrencias**: dos filas idénticas en el archivo con una ya guardada → la primera es duplicado y la
  segunda se importa. Reimportar el mismo archivo inserta 0. `SkipDuplicates = false` las importa con aviso.

### Confirmación atómica en un ámbito nuevo

`EfImportWriter` (Singleton que solo guarda `IServiceScopeFactory`) crea un `AsyncServiceScope` propio, agrega
categorías nuevas, gastos e ingresos con `AutoDetectChangesEnabled = false` y llama **una sola vez** a
`SaveChangesAsync(CancellationToken.None)`, dentro de `Task.Run`. EF envuelve ese único guardado en una transacción:
todo se confirma o nada. El `AppDbContext` compartido de la UI nunca ve una entidad de la importación, así que un
guardado posterior no relacionado no puede persistir un lote fallido. Cualquier excepción se convierte en
`WriteFailed` (registrada, sin texto crudo hacia la UI); una cancelación se ignora una vez iniciada la escritura.
`EfImportLookup` usa el mismo patrón de ámbito nuevo por llamada.

**Compuerta de decisión de 50 000 filas (S5).** `ImportPerformanceTests` (Release, SQLite real en archivo temporal,
lector CSV real, planificador y escritor reales) mide un único `SaveChangesAsync`: vista previa 0,43–0,54 s y
confirmación (replanificar + construir + escribir) 6,33–6,75 s, con techo de 30 s. **Se mantiene un único
`SaveChangesAsync`**; no hizo falta dividir en bloques ni abrir una transacción explícita. La prueba corre en una
colección xUnit sin paralelismo (con la suite completa en paralelo llegó a 18–20 s por competencia de CPU y disco; el
techo no se subió).

### Superficie de UI

`ExpensesView` añade el botón «Importar» junto a los de exportación; `ImportView` sustituye la lista de movimientos en
el mismo lugar mientras `IsImportOpen` (no hay ruta). `ExpensesViewModel` recibe `ImportViewModel? import = null` como
último parámetro opcional y lo cierra al salir de la pantalla (el historial de navegación conserva cada VM, y este
retiene los bytes del archivo). Estados: `Idle`, `Loading`, `Mapping`, `Previewed`, `Importing`, `Done`; cada comando
comprueba el estado de forma síncrona antes de su primer `await` (las guardas de doble ejecución no dependen de
`CanExecute`). Cancelar el selector no escribe nada ni muestra mensaje. Tras un éxito se dispara `Completed` (el host
recarga categorías y lista) y se refrescan las notificaciones sin esperar. Los errores de archivo se muestran con
mensajes localizados (`es-DO`/`en`), nunca con texto de excepción ni rutas.

### Decisiones y seguimientos

- **Desviación documentada de la hoja de ruta:** los **perfiles de mapeo guardados** (`CsvImportProfile`) quedan
  diferidos. El auto-mapeo por sinónimos y el paso de mapeo manual cubren el caso sin persistir nada (sin migración ni
  clave de configuración nueva).
- Fuera de esta fase: perfiles con nombre, formatos bancarios (OFX/QIF/MT940), importación de PDF, categorización con
  IA al importar, deshacer una importación (hoy: restaurar un backup de la Fase 19) y una UI general de gestión de
  categorías.

---

## 12. Persistencia — detalles adicionales

### Ruta del archivo

- `IPathResolver` resuelve el directorio de datos:
  - Windows: `%APPDATA%\ControlDeFinanzasIA\data.db`
  - macOS: `~/Library/Application Support/ControlDeFinanzasIA/data.db`
  - Linux: `$XDG_CONFIG_HOME/ControlDeFinanzasIA/data.db` o `~/.config/ControlDeFinanzasIA/data.db`
- Nombre del archivo fijo: `data.db`. Versión de schema en `__EFMigrationsHistory`.

### Migraciones

- Aplicadas **al arrancar la app**, dentro de un try/catch con backup automático previo si la migración es destructiva (definidas en una lista de migraciones "peligrosas" en código).
- Si la migración falla, la app **no arranca** y muestra un diálogo con instrucciones (no silenciar nunca un fallo de migración).

### Backup

Implementado en la Fase 19 (ver §11f). Es un respaldo completo de la base, **no** una exportación de reportes (Fase 18).

- Manual, mediante `CreateBackupUseCase`, que produce un `.zip` con exactamente dos entradas:
  - `data.db` (instantánea consistente por la API de backup de SQLite; sin la API key de IA)
  - `manifest.json` con `formatVersion`, `schemaVersion` (último `MigrationId`), `exportedAt` (ISO 8601 UTC), `appVersion`, `dbSha256` y `apiKeyIncluded`.
- `InspectBackupUseCase` / `RestoreBackupUseCase` comparan el historial de migraciones del backup con `IMigrationCatalog`: un `MigrationId` desconocido (o `formatVersion` > 1) se rechaza como `NewerSchema` mostrando ambas versiones (no downgrades); un subconjunto propio de migraciones se acepta y se migra en el siguiente arranque.

#### Recuperación manual

Si una restauración falla de forma irrecuperable o la migración posterior a un restore falla al arrancar (la app no arranca, no hay reversión automática):

1. Cierre la aplicación.
2. En el directorio de datos, copie `data.db.pre-restore.bak` sobre `data.db` (es la base original exacta, byte a byte), **o** descomprima `data.db` de `backups\pre-restore-*.zip` (copia de seguridad previa al restore, la más reciente ordena al final) y sustitúyalo.
4. Si existe `backups\.rollback-failed`, un rollback anterior no pudo devolver la base original: mientras el marcador exista `CompletePendingRestore` conserva el `.bak` y registra una advertencia en cada arranque. Tras recuperar a mano, borre el marcador (el código nunca lo borra).
5. Los archivos sueltos `data.db.pre-restore-<utc>.bak` y `data.db.rollback-*` nunca se barren y contienen una copia completa, incluida la API key; bórrelos a mano cuando ya no los necesite.
3. Borre los archivos `data.db-wal` y `data.db-shm` si existen, y arranque de nuevo.

### Import CSV / Excel (Fase 20)

Detalle completo en §11g. Resumen de la persistencia:

- `ImportView` (alojada dentro de `ExpensesView`, sin ruta) ofrece: selección de archivo CSV/TXT/XLSX, vista previa
  de las primeras 50 filas más las primeras 50 filas con problemas, mapeo de columnas (solo cuando el automático es
  incompleto, o a demanda), opciones de separador decimal, formato de fecha, tipo y moneda por defecto, y
  confirmación con la recomendación de hacer un backup antes.
- **No hay perfiles de mapeo guardados** (desviación documentada de la hoja de ruta, ver §11g).
- Toda la importación ocurre en **una sola transacción** (un único `SaveChangesAsync` en un ámbito de DI propio); si
  falla, no queda ninguna fila ni categoría nueva. No hay migración ni tabla nueva.

### Concurrencia

- Single-user, **un solo proceso** a la vez. Se documenta en el manifiesto y en la UI. Si en el futuro hay que soportar multi-proceso, se introduce un lock-file (`data.db.lock`).

---

## 13. Configuración y settings

### Capas de configuración

| Capa | Almacenamiento | Quién la edita |
| --- | --- | --- |
| **App defaults** | Empaquetado en el binario (recursos `.resx`, JSON) | Nadie. Define defaults: monedas, categorías iniciales, métodos de pago iniciales, lista de proveedores soportados. |
| **User settings** | Tabla `Setting` en SQLite | El usuario, desde `SettingsView`. Ocho claves (`theme`, `locale`, `ai.provider`, `ai.apiKey`, `ai.model`, `ai.baseUrl`, `ai.temperature`, `ai.maxTokensPerRequest`) — categorías, métodos de pago, tags y vistas guardadas viven en sus propias tablas, no aquí. |
| **App config de runtime** | JSON al lado del binario (appsettings.json) | Desarrollador. Log level por defecto, rutas de log, lista blanca de proveedores, version del schema. No se shippea lógica sensible. |

### Settings tipados

- `AppSettings` (record, 8 campos): `Theme` (`AppTheme`: `Dark`/`Light`), `Locale`, `AiProvider`
  (`OpenAI`/`Anthropic`/`OpenRouter`/`Custom`), `AiApiKey`, `AiModel`, `AiBaseUrl`, `AiTemperature`
  (0.0–2.0), `AiMaxTokensPerRequest` (1–128000). `Defaults` estático documenta los valores por
  defecto de cada campo.
- `ISettingsStore.GetAppSettingsAsync` devuelve el `AppSettings` completo; `SetAsync<T>` persiste una
  clave a la vez (upsert), serializando enums por **nombre** (no ordinal) y todo valor numérico con
  `CultureInfo.InvariantCulture` — evita que `AiTemperature` se corrompa si el locale de la UI cambia
  entre escritura y lectura (`es-DO` usa coma decimal).
- El tema `Light` se persiste aunque **todavía no se aplique** visualmente en v1 (nota siempre visible
  en el picker); el cambio de idioma requiere reiniciar la app (aviso post-guardado, no ambiente).

### API key

- Se guarda en texto plano en v1. **Advertencia siempre visible** en la sección IA (no condicionada a
  si ya hay una key cargada): "Este archivo de datos guarda tu API key en texto plano. Protégela con
  cifrado de disco (BitLocker, FileVault, LUKS)".
- Cifrado en reposo por SO: **ADR pendiente** (DPAPI en Windows, Keychain en macOS, libsecret en Linux).
- No hay `HttpClient`/`ILLMClient` en `UpdateAiSettingsUseCase` ni en `SettingsStore` — Fase 12 solo
  persiste la configuración, verificado con un assert por reflexión en tests. El consumo real de la key
  (Fase 13, `CategorizeExpenseUseCase`/`TestAiConnectionUseCase`) la lee de solo lectura vía
  `ISettingsStore`; nunca la re-persiste, cifra ni transforma. La key nunca aparece en logs, mensajes de
  estado ni el resultado de "Probar conexión" — asertado por `ApiKeyIsNeverLoggedTests` con una key
  centinela.

---

## 14. Logging

### Stack

- **`Microsoft.Extensions.Logging`** como abstracción.
- **Sinks**:
  - **Consola** (con colores, opcional), en `Debug`.
  - **Archivo rotativo** en producción: `logs/app-YYYYMMDD.log`, tamaño máx 5 MB,retención 5 archivos.
- **Niveles por categoría** configurables via `appsettings.json` o env var (`LOGGING__LOGLEVEL__DEFAULT=Debug`).

### Qué se loguea

- **Info**: arranque, navegación entre pantallas, generación de recurrentes, import/export.
- **Warning**: IA no disponible, validación que falla, archivo de datos no encontrado al primer arranque.
- **Error**: excepciones no manejadas, fallos de migración, fallos de DB.
- **Nunca**: contenido de transacciones, API keys, montos específicos (por defecto). Se puede activar verbose en Debug con un flag.

### Correlation ID

- Cada operación request-driven (ejecutada en respuesta a un click) lleva un `Guid` correlation id que se propaga a logs. En errores se muestra al usuario para reportar bugs.

---

## 15. Testing

### Unit tests (`Tests.Unit`)

- **xUnit + FluentAssertions + NSubstitute** (o Moq, da igual, fijar uno).
- Cobertura objetivo: **dominio y use cases al 100%**, ViewModels en lo que tenga lógica.
- **Domain**: entidades (Money, cálculos de cuotas, evaluación de presupuestos, calculadora de recurrentes, detector de anomalías base).
- **Application**: cada use case con sus colaboradores mockeados. Validan: mapeo DTO, validaciones, manejo de errores, formato de `Result`.
- **ViewModels**: estados, transiciones, comandos. `Avalonia.Headless` o un harness mínimo.

### Integration tests (`Tests.Integration`)

- **EF Core**: repos contra un archivo SQLite temporal (no en memoria, para acercarse al comportamiento real). Patrón `IDbContextFactory` para paralelizar.
- **Importación (Fase 20)**: `CsvTableReader` con fixtures binarios reales (UTF-8, Windows-1252, UTF-16, `;`, tab), `ClosedXmlTableReader` con libros producidos por el `ClosedXmlReportWriter` real, `EfImportWriter` sobre SQLite temporal con un trigger que fuerza el fallo (0 filas nuevas), la prueba de 50 000 filas, y `ImportEndToEndTests`, que recorre exportación real (Fase 18) → importación en una base nueva → reimportación (inserta 0) por DI real (`AddPersistence` + `AddReportExporters` + `AddImport`).
- **Backup (Fase 19)**: `SqliteBackupService` real sobre bases SQLite temporales creadas con las migraciones reales (creación, inspección, restauración, inyección de fallos con `FaultyBackupFileSystem`, bloqueo real de archivo solo en Windows) y `BackupRestoreEndToEndTests`, que recorre `CreateBackupUseCase` → `InspectBackupUseCase` → `RestoreBackupUseCase` por DI real (`AddPersistence` + `AddBackup`) y compara todas las tablas.
- **Llm clients (marcados "external")**: solo se ejecutan si `CONTROL_FINANZAS_RUN_EXTERNAL_AI_TESTS=1` y hay API key. Hacen requests reales a OpenRouter con un modelo gratuito.

### Architecture tests

- En `Tests.Unit` (o un sub-proyecto si crece) usar **NetArchTest** para validar el grafo de dependencias: Domain no referencia a nadie, Application no referencia a Infrastructure, etc. Atrapa violaciones antes de code review.

### End-to-end

- **Avalonia.Headless** + un smoke test por flujo principal: registrar gasto, generar recurrente, crear meta, ejecutar import, conectar IA.

### Cobertura

- **Coverlet** + reportes en CI. Umbral **mínimo 80% en Domain y Application**. UI: sin umbral estricto, pero los ViewModels con lógica > 70%.

---

## 16. Manejo de errores y resiliencia

### `Result<T>` en use cases

```text
Result<T> = Success(T) | Failure(code, message, details?)
```

- **Errores de validación**: code `VALIDATION`, message agregado, details con errores por campo.
- **Errores de negocio**: codes por tipo (`BUDGET_EXCEEDED`, `GOAL_PAST_DATE`, `DEBT_ALREADY_CLOSED`, `LLM_UNAVAILABLE`, `IMPORT_INVALID_FILE`, etc.).
- **Excepciones inesperadas**: burbujean hasta un `IExceptionHandler` global (CommunityToolkit.Mvvm) que muestra un diálogo con correlation id y loguea el stack.

### Capas defensivas

- **DB**: `SaveChanges` envuelto en try/catch que distingue *concurrency* (DbUpdateConcurrencyException) y *constraint* (DbUpdateException) para mensajes útiles.
- **File I/O**: cualquier operación de archivo en try/catch con mensaje claro ("no se pudo escribir el backup en X, ¿está lleno el disco?").
- **HTTP (IA)**: timeout duro + retries con backoff + circuit breaker (ver sección 11).

### Fallback de IA

- La IA **nunca** es el único camino para una función. Si la IA no responde, el detector de anomalías base igual produce alertas; el plan de ahorro se calcula determinísticamente; la categorización automática simplemente no sugiere y deja la categoría en null/pendiente.
- **Fase 14 formaliza este principio como un contrato de tipo, no solo una guía de diseño**: `GetInsightsUseCase`/`GetAnomalyNarrativeUseCase` nunca devuelven `Result.Failure` por una razón de IA — todo camino no-feliz (no configurado, `LlmException`, timeout, respuesta no parseable) es `Result.Success` con un `NarrativeStatus` de repliegue. El pronóstico y las anomalías del dashboard se calculan en un paso completamente separado (`GetDashboardInsightsQuery.GetDeterministicAsync`) que no referencia `ILLMClient`/`ISettingsStore` en absoluto — no es que la IA "falle silenciosamente", es que las secciones numéricas nunca dependen de ella para existir.
- **Fase 15 extiende el mismo contrato de tipo al chat**: `AskFinanceChatUseCase.ExecuteAsync` nunca devuelve `Result.Failure` — pregunta rechazada, IA no configurada, y cualquier falla de IA son todas `Result.Success(ChatReply)` con un `ChatReplyStatus` de repliegue (`Rejected`/`NotConfigured`/`Unavailable`). El panel de cifras exactas (§11b) se alimenta directamente del `ChatContextSnapshot` que `ChatReply` lleva adjunto, nunca del texto de la respuesta — así que incluso una respuesta `Unavailable` deja el panel de cifras actualizado con datos reales.
- **Fase 16 aplica el mismo contrato a la narración del plan de ahorro, con una garantía adicional propia**: `GenerateSavingsPlanUseCase.ExecuteAsync` solo devuelve `Result.Failure` para `GoalNotFound` — no configurada, cualquier falla de IA, y una narración rechazada por el guard numérico son todas `Result.Success(SavingsPlanNarrationDto)` con un `SavingsPlanNarrationStatus` de repliegue (`NotConfigured`/`Unavailable`). A diferencia de §11a/§11b, aquí la cifra determinista (`GoalRow`'s bound properties) ni siquiera es un DTO adjunto a la respuesta — el caso de uso no tiene ninguna vía de escritura hacia `GoalRow` en absoluto, así que un repliegue a `Unavailable` deja la tarjeta exactamente como estaba antes del clic, sin ningún paso adicional de "restaurar" el estado previo.

---

## 17. Internacionalización y formato

- **Idiomas**: `es` (es-DO por defecto) y `en`. Estructura de recursos por cultura.
- **Framework**: `IStringLocalizer` (.NET) o **resx de Avalonia** con binding a `ILocalizer` propio si se prefiere un wrapper. Decisión en ADR.
- **Formato de números y fechas** sigue la cultura activa:
  - es-DO: `dd/MM/yyyy`, separador de miles `.`, decimal `,`.
  - en-US: `MM/dd/yyyy`, separador de miles `,`, decimal `.`.
- **Moneda** se formatea con la cultura, pero el símbolo de moneda muestra `DOP` o `USD` literal (no `$RD` ni nada raro).

---

## 18. Empaquetado y despliegue

- **Self-contained** por SO: el binario incluye el runtime, no requiere .NET instalado.
- **Publicación por RID**: `win-x64`, `osx-arm64` + `osx-x64`, `linux-x64`. Decidir `linux-musl-x64` si se quiere evitar glibc (ver ADR).
- **Instaladores**:
  - Windows: MSIX o instalador propio (siguiente paso).
  - macOS: `.app` + `.dmg`.
  - Linux: AppImage en v1, Flatpak/Snap como siguientes pasos.
- **Auto-update**: **fuera de alcance** en v1. El usuario descarga nuevas versiones manualmente.

---

## 19. Decisiones arquitectónicas (ADRs)

Plantilla: **Contexto · Decisión · Consecuencias**. Lista viva. Se actualiza cuando cambia una decisión.

| # | Decisión | Estado |
| --- | --- | --- |
| ADR-001 | **Clean Architecture en 4 capas con proyectos separados** | Aceptada |
| ADR-002 | **MVVM con CommunityToolkit.Mvvm, sin code-behind lógico** | Aceptada |
| ADR-003 | **EF Core + SQLite, migraciones versionadas, único DbContext** | Aceptada |
| ADR-004 | **Navegación por contenido en una sola ventana** | Aceptada |
| ADR-005 | **`ILLMClient` propio, sin SDKs por proveedor, con Polly** | Aceptada |
| ADR-006 | **`Result<T>` para errores esperados, excepciones solo para inesperados** | Aceptada |
| ADR-007 | **`IClock` y `IPathResolver` inyectados en todo lo dependiente de tiempo/ruta** | Aceptada |
| ADR-008 | **API key de IA en claro en v1, cifrado por SO diferido a v2** | Aceptada (temporal) |
| ADR-009 | **Backup como ZIP con SQLite + manifest, sin auto-backup** (la única copia automática es la de seguridad previa a un restore, `backups\pre-restore-*.zip`; Fase 19, §11f) | Aceptada |
| ADR-010 | **Tests con NetArchTest validando el grafo de dependencias** | Aceptada |
| ADR-011 | **Cifrado en reposo de la API key** (DPAPI/Keychain/libsecret) | Pendiente v2 |
| ADR-012 | **Auto-update de la app** | Pendiente v2 |
| ADR-013 | **Multi-proceso / lock file** | Pendiente (no necesario en v1) |

---

## 20. Referencias

- `docs/vision.md` — qué se construye.
- `docs/roadmap.md` — cuándo se entrega.
- `spec/constitution/mission.md` — propósito ejecutivo.
- `spec/constitution/tech-stack.md` — stack y convenciones de alto nivel.
- `spec/constitution/features/` — features individuales con criterios de aceptación (se poblará feature por feature).
