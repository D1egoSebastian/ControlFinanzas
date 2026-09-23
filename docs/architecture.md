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
- **`IBackupService`** — export/import del archivo completo.
- **`ICsvImporter`** — importación con perfil configurable.

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
- `ImportCsvUseCase` (con perfil de columnas reutilizable)
- `ExportDataUseCase` (respaldo completo)
- `ImportBackupUseCase` (restauración desde archivo de backup)

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
- **`BackupService`** exporta el archivo SQLite a un `.zip` que contiene el `.db` y un `manifest.json` con la versión del schema. El import valida el manifest antes de reemplazar.
- **`CsvImporter`** lee un CSV, aplica un `CsvImportProfile` (mapeo de columnas + formato de fecha + separador decimal), devuelve las entidades detectadas, las pasa por validación y las inserta en una transacción. Perfil guardable.
- **`CsvExporter`** para el reporte a Excel/CSV (no es el backup completo, es un reporte).

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
- **`Views/`** — UserControls (`DashboardView`, `ExpensesView`, `DebtsView`, `GoalsView`, `BudgetsView`, `SubscriptionsView`, `FixedExpensesView`, `RecurringView`, `SettingsView`, `ImportView`, `ExportView`, `ChatView`).
- **`ViewModels/`** — uno por vista. Heredan de `ObservableObject` (CommunityToolkit).
- **`Navigation/`** — `INavigationService` + `NavigationService` + `RouteNames` (constantes).

### Composition root

- En `App.axaml.cs` (o un `Bootstrapper` aparte) se hace **toda** la registración de servicios. Aquí es el único lugar donde UI referencia a Infrastructure:
  - Se registra `AppDbContext`, repos, `ILLMClient` con su factory por proveedor, `IClock`, `IPathResolver`, `ISettingsStore`, `IBackupService`, `ICsvImporter`.
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

### Detalle de un movimiento (modal)

- Para detalles (ver/editar un gasto, una deuda) se usa una **ventana hija** o un `DialogHost` de Avalonia, no navegación apilada en el shell. La razón: la edición es transaccional; el shell no debería perder el contexto del usuario.

### Deep links internos

- En v1: no hay deep links externos. Internamente, los ViewModels pueden pedir navegación por `RouteNames` o por tipo de ViewModel.

### Por qué no multi-ventana

- Una app de finanzas personal no se beneficia de múltiples ventanas. El usuario hace tareas secuenciales, no en paralelo.
- Multi-ventana complica state, foco, atajos, navegación y testing.

---

## 11. Integración de IA — `ILLMClient`

**Fases 13 y 14, implementado.** Lo que sigue documenta el contrato y los adapters realmente
shippeados — `Application/Abstractions/Llm/`, `Infrastructure/Llm/OpenAICompatibleClient.cs`,
`Infrastructure/Llm/AnthropicClient.cs`, `Infrastructure/Llm/LLMClientFactory.cs` — no un plan a
futuro. El primer consumidor (Fase 13) es `CategorizeExpenseUseCase` (sugerencia de categoría al
tipear la descripción de un gasto); Fase 14 reutiliza el mismo `ILLMClient`, sin ninguna
modificación, para `GetInsightsUseCase`/`GetAnomalyNarrativeUseCase` (ver §11a). Ningún archivo bajo
`Infrastructure/Llm/` cambió en Fase 14 salvo la adición aditiva de 8 nuevos recursos de prompt.

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

- Manual, mediante `ExportDataUseCase` que produce un `.zip`:
  - `data.db`
  - `manifest.json` con `{ "schemaVersion": N, "exportedAt": ISO8601, "appVersion": "x.y.z" }`
- El import valida `schemaVersion` y rechaza si es de una versión más nueva (no downgrades).

### Import CSV

- Pantalla `ImportView` con:
  1. Selección de archivo CSV/Excel.
  2. Vista previa de las primeras N filas.
  3. Mapeo de columnas (qué columna es fecha, monto, descripción, categoría, etc.) y formato (separador decimal, formato de fecha).
  4. Guardar el mapeo como `CsvImportProfile` con nombre para reusar.
  5. Confirmación y ejecución.
- Toda la importación ocurre en una **transacción**; si falla, rollback.

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
- **CsvImporter/CsvExporter**: roundtrip con archivos sintéticos.
- **BackupService**: roundtrip export → import, con verificación de integridad.
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
| ADR-009 | **Backup como ZIP con SQLite + manifest, sin auto-backup** | Aceptada |
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
