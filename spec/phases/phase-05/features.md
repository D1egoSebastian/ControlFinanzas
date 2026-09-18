# Fase 5 — Método de pago + multi-moneda DOP/USD

> **Fase:** 5 — Multi-moneda (DOP/USD) + integración real de `PaymentMethod`
> **Estado:** ✅ COMPLETADA (2026-09-08 — 559/559 tests verdes)
> **Spec ID:** PHASE-05-PLAN
> **Dependencias:** Phase 1 ✅, Phase 2 ✅, Phase 3 ✅, Phase 4 ✅
> **Roadmap:** `docs/roadmap.md` §Fase 5 (líneas 270-310)
> **Stack:** .NET 8 · Avalonia 11.2.7 (MVVM strict) · EF Core 8.0.11 + SQLite · xUnit + FluentAssertions 11.10.0

## 1. Resumen

Phase 5 cierra dos brechas que el modelo de `Transaction` arrastra desde el
slice vertical de Phase 3:

1. **`PaymentMethod` realmente usado.** El catálogo (`Domain/Catalog/PaymentMethod.cs`)
   y su repositorio existen desde Phase 2, pero `Expense` e `Income` **no
   llevan aún un `PaymentMethodId`**: la columna FK no existe en la tabla,
   el campo no aparece en los commands de los use cases ni en los DTOs, y
   la UI no tiene picker. Phase 5 conecta todo el cableado.
2. **Conversión multi-moneda display-time.** El `enum Currency { DOP, USD }`
   y el value object `Money` ya están en sitio desde Phase 1 (F-002), y el
   form de gastos ya tiene un `ComboBox` de moneda. Lo que falta es el
   **proveedor de tasas de cambio** (`IExchangeRateProvider`) y un
   servicio de **conversión display-time** que muestre la otra moneda
   en listas, resúmenes y filtros, **sin** almacenar el monto convertido
   en la base (alineado con `docs/vision.md` §8: *"la conversión se
   aplica en tiempo de display, no al guardar"*).

Phase 5 **no** innova arquitectura: replica los patrones de Phase 4
(Domain entity → Configuration → Repository → UseCase → Validator → UI
View+ViewModel → tests). El leverage es alto porque los precedentes
están frescos.

> **Importante sobre el alcance del `IExchangeRateProvider`.** El roadmap
> (`docs/roadmap.md` §Fase 5, líneas 287-310) es explícito: la API real
> del tipo de cambio se difiere hasta tener conectividad clara.
> **Phase 5 entrega un `StubExchangeRateProvider`** que devuelve una tasa
> fija configurable (placeholder documentado). La interfaz es estable y
> el adapter real (OpenExchangeRates, exchangerate.host, etc.) se enchufa
> en una fase futura sin tocar use cases.

## 2. Contexto

`docs/vision.md` §4.2 define que cada movimiento lleva, como mínimo:

> *"Fecha, monto y moneda original (DOP o USD), categoría, tags libres
> opcionales, método de pago (etiqueta: efectivo, débito, crédito,
> transferencia, otro). Es solo una etiqueta, **no** se llevan balances
> por método."*

§8 (casos de borde clave) refuerza:

> *"Conversión de moneda se aplica en tiempo de display, no al guardar.
> El movimiento se almacena en su moneda original; los reportes
> convierten usando una tasa **actualizada periódicamente** desde una API."*

Phase 3 y Phase 4 construyeron el CRUD de `Expense` + `Income` con
moneda **persistente pero sin conversión**. Esto funciona para el
usuario dominicano que registra todo en DOP, pero rompe cuando mezcla
USD (compra en Amazon, viaje, freelance de cliente americano) porque
los reportes no muestran el equivalente en la otra moneda.

Phase 5 es **prerrequisito conceptual** de las visualizaciones (Phase 8
— dashboard), los presupuestos (Phase 10 — "¿cómo se compara un
presupuesto en DOP contra un gasto real en USD?") y la exportación
(Phase 18 — el reporte debe mostrar ambas monedas).

## 3. Objetivos

1. **Cablear `PaymentMethodId`** en `Expense` + `Income` (entidad,
   migración, repositorio, use cases, DTOs, UI) para que cada
   movimiento lleve el método con el que se pagó/cobró.
2. **Introducir `IExchangeRateProvider`** en `Application/Abstractions/`
   con un **stub determinista** que devuelve una tasa fija configurable,
   para mantener la app offline-capable y desacoplar la decisión de la
   API real.
3. **Conversión display-time** vía un servicio de aplicación
   (`ICurrencyConverter` o equivalente) que toma un `Money` y devuelve
   el `Money` equivalente en la otra moneda, consultando el provider.
4. **UX: ver siempre ambas monedas** en la lista de transacciones,
   usando el componente existente (`Money.ToString(culture)` ya
   formatea correctamente) más una segunda línea con la conversión.
5. **Picking de método en los formularios** de gastos e ingresos, con
   default razonable (`Efectivo` cuando se crea un movimiento nuevo sin
   selección explícita).

## 4. Alcance

### 4.1 In scope

- `Expense.PaymentMethodId` (Guid? nullable para gastos en efectivo sin
  método asignado explícitamente, ej. "pagado en mano" no requiere
  tarjeta) e `Income.PaymentMethodId` (Guid? por simetría).
- Migración EF Core `AddPaymentMethodIdToTransactions` que añade la
  columna a `Expenses` e `Incomes` con default `Efectivo` (seed lookup)
  para los registros existentes — **no destructiva**.
- Actualizar `ExpenseConfiguration` y `IncomeConfiguration`: FK a
  `PaymentMethods.Id` con `OnDelete(DeleteBehavior.SetNull)`, índice
  en `PaymentMethodId` para futuras agregaciones por método.
- Actualizar `Expense.Create` / `Expense.Update` /
  `Income.Create` / `Income.Update` con invariantes:
  `PaymentMethodId`, si está presente, no debe ser `Guid.Empty`.
- `RegisterExpenseCommand` / `UpdateExpenseCommand` /
  `RegisterIncomeCommand` / `UpdateIncomeCommand` aceptan
  `Guid? PaymentMethodId`.
- Validadores FluentValidation aceptan `PaymentMethodId` opcional.
- `ExpenseDto` / `IncomeDto` exponen `PaymentMethodId` (Guid?) y
  `PaymentMethodName` (string? — populado por la query).
- `ExpenseRepository.AddAsync` / `UpdateAsync` validan la FK a
  `PaymentMethods` (igual patrón que `CategoryRepository` →
  `CategoryNotFound`).
- `ExpenseRepository.GetByFilterAsync` /
  `IncomeRepository.GetByFilterAsync` aceptan `PaymentMethodIds`
  (IReadOnlyList<Guid>?) con semántica OR (mismo patrón que `TagIds`
  de Phase 4 F-007).
- `ExpenseFilter` / `IncomeFilter` ampliados con `PaymentMethodIds`.
- Nueva interfaz `IExchangeRateProvider` en
  `Application/Abstractions/IExchangeRateProvider.cs` con:
  `Task<ExchangeRate> GetRateAsync(Currency from, Currency to, CancellationToken ct)`
  + `Task<IReadOnlyDictionary<(Currency, Currency), decimal>> GetLatestRatesAsync(CancellationToken ct)`.
- `ExchangeRate` value object en `Domain/ValueObjects/ExchangeRate.cs`
  con invariantes: `Rate > 0`, `From != To`, `AsOf` (DateTime UTC).
- `StubExchangeRateProvider` en
  `Infrastructure/ExchangeRates/StubExchangeRateProvider.cs` con tasas
  configurables vía `appsettings.json` o env vars (default:
  `1 USD = 60 DOP`, `1 DOP = 0.0167 USD`, inversas coherentes).
- Servicio de aplicación `CurrencyConverter` (o `ICurrencyConverter` +
  `CurrencyConverter` en `Application/Services/`) que envuelve el
  provider y aplica la conversión con redondeo bancario a 4 decimales
  (alineado con `Money.HasPrecision(18,4)`).
- Caching en memoria del `CurrencyConverter` con TTL configurable
  (default 1 hora) para no martillar el provider en cada render de
  lista. Se documenta como punto a evolucionar.
- UI: añadir `ComboBox` de `PaymentMethod` al `ExpensesView` y al
  `IncomeView`, con opción "Sin método" (SelectedValue null).
- UI: en la lista de gastos/ingresos, mostrar **segunda línea** con
  la conversión a la otra moneda (`100.00 USD ≈ 6,000.00 DOP` o
  viceversa), solo cuando difiera de la moneda original.
- i18n: nuevas claves en `Strings.resx` y `Strings.es-DO.resx`:
  `PaymentMethodLabel`, `SelectPaymentMethodWatermark`,
  `NoPaymentMethodOption`, `ConvertedAmountLabel`,
  `CurrencyConversionUnavailableMessage`,
  `ExchangeRateProviderWarning`, etc.
- Migración de datos existentes: registros pre-Phase-5 toman
  `PaymentMethodId = Efectivo` (lookup del seed) por defecto.
- Tests: unitarios para `ExchangeRate.Create`, `CurrencyConverter`,
  `StubExchangeRateProvider`; integración para los repos
  actualizados; VM para el nuevo picker + segunda línea de
  conversión.

### 4.2 Out of scope (Phase 6+)

- **`IExchangeRateProvider` real** llamando a una API externa
  (OpenExchangeRates, exchangerate.host, banco central RD). El stub
  cubre Phase 5; el adapter real se elige cuando haya decisión de
  conectividad y se enchufa en una fase posterior.
- **Almacenamiento de tasas históricas** por fecha de transacción.
  Vision §8 menciona "tasa actualizada periódicamente" — Phase 5
  implementa "tasa vigente al momento de la consulta" (cache TTL).
  El versionado histórico por transacción queda para una fase futura
  cuando los reportes lo necesiten (probablemente Phase 8 — vistas).
- **`base_currency` configurable por el usuario** (la moneda en la
  que se muestran los totales del dashboard). Queda en Phase 8/12
  (settings).
- **Reportes con conversión cruzada** (top categorías en USD cuando
  los gastos están en DOP). El "convertido" se muestra **al lado** del
  monto original, no se agregan valores cruzados todavía.
- **Recurrentes (Phase 6)**, **deudas (Phase 7)**, **presupuestos
  (Phase 10)**, **metas (Phase 11)**. Todos modelarán `Money` pero
  no necesitan el `PaymentMethodId` ni el provider en sus flujos
  principales (Phase 6 sí lo necesitará para las suscripciones).
- **Background job que refresca tasas** automáticamente. El TTL del
  cache + consulta lazy al provider es suficiente para v1.
- **UI: gráfico con doble eje moneda.** Phase 8+.
- **Settings de moneda preferida** del usuario. Phase 12.
- **Notificaciones de cambio significativo** de tasa. Phase 17.
- **CRUD de métodos de pago personalizados** (el usuario ya tiene los
  5 métodos del sistema; agregar/renombrar/borrar métodos
  personalizados es Phase 12 settings o una sub-feature de Phase 5
  que se decide en la spec).

### 4.3 Cambios al alcance

- **Modifica:**
  `Expense.cs` (agrega `PaymentMethodId`), `Income.cs` (idem),
  `ExpenseConfiguration.cs` (FK + índice), `IncomeConfiguration.cs`
  (idem), `ExpenseRepository.cs` (FK validation + filtro
  `PaymentMethodIds`), `IncomeRepository.cs` (idem),
  `ExpenseDto.cs` / `IncomeDto.cs` (+ `PaymentMethodId`,
  `PaymentMethodName`), `ExpenseFilter.cs` /
  `IncomeFilter.cs` (+ `PaymentMethodIds`), commands de
  Register/Update (Expense + Income) + validadores,
  `ExpensesViewModel.cs` (PaymentMethod picker + conversión) +
  `ExpensesView.axaml` (UI), `IncomeViewModel.cs` +
  `IncomeView.axaml` (idem), `Bootstrap.cs` (registra
  `IExchangeRateProvider` + `ICurrencyConverter`), `Strings.resx` y
  `Strings.es-DO.resx` (claves nuevas).
- **Crea:**
  `Domain/ValueObjects/ExchangeRate.cs`,
  `Application/Abstractions/IExchangeRateProvider.cs`,
  `Application/Services/CurrencyConverter.cs` (o carpeta
  `Application/ExchangeRates/`),
  `Infrastructure/ExchangeRates/StubExchangeRateProvider.cs`,
  migración `AddPaymentMethodIdToTransactions` (versión + nombre).
- **NO modifica:**
  `Money.cs` (la conversión no es responsabilidad de `Money` —
  confirmado en Phase 1 F-002), `Currency.cs` (los valores `DOP` y
  `USD` ya están bien), `Result` / `PagedResult`, `Money.Create` /
  `Money.Add` / `Money.Subtract`, el modelo polimórfico `Transaction`
  (se mantiene `Expense` e `Income` como entidades separadas —
  alineado con Phase 4 §4.2), `Category`, `Tag`, `SavedView`.

## 5. Casos de uso

### CU-1 — Registrar gasto en USD con método de pago

1. Usuario navega a "Gastos".
2. Llena el form: categoría "Comida", monto `25.00`, **selector de
   moneda = USD**, **selector de método = Tarjeta de crédito**, fecha,
   descripción "Almuerzo NYC".
3. Click "Registrar".
4. Validator de `RegisterExpenseCommand` valida (monto > 0,
   descripción no vacía, moneda válida, método opcional pero si está
   presente != Guid.Empty).
5. Use case crea `Expense` con `Amount = Money.Create(25, USD)` y
   `PaymentMethodId = <Tarjeta de crédito>`.
6. Repo persiste.
7. Lista muestra: `25.00 USD` en la columna principal y
   `≈ 1,500.00 DOP` (tasa 60:1 del stub) en la segunda línea.
8. Status message confirma el registro.

### CU-2 — Registrar ingreso sin método de pago

1. Usuario registra un ingreso de `500 USD` por "Freelance proyecto X".
2. **No selecciona método de pago** (los ingresos típicamente llegan a
   cuenta, no se "pagan con tarjeta"; el método es opcional para
   ingresos).
3. `PaymentMethodId` queda `null` en la entidad y en la columna.

### CU-3 — Migración silenciosa de datos existentes

1. Usuario abre la app tras instalar Phase 5.
2. EF Core aplica la migración `AddPaymentMethodIdToTransactions`.
3. La migración ejecuta un `UPDATE` para asignar
   `PaymentMethodId = (SELECT Id FROM PaymentMethods WHERE Name =
   'Efectivo')` a todos los gastos e ingresos existentes sin método.
4. La app arranca normal; los gastos pre-existentes aparecen con
   "Efectivo" en cualquier vista que muestre el método.

### CU-4 — Filtrar gastos por método

1. En `ExpensesView`, en el filter rail, nuevo `ComboBox` multi-select
   de métodos (mismo patrón visual que el de tags).
2. Usuario selecciona "Tarjeta de crédito".
3. `ApplyFilterCommand` actualiza `CurrentFilter.PaymentMethodIds` y
   dispara `RefreshAsync`.
4. `ExpenseRepository.GetByFilterAsync` aplica
   `.Where(e => filter.PaymentMethodIds.Contains(e.PaymentMethodId))`.

### CU-5 — Mostrar conversión cuando cambia el provider

1. Usuario navega a la lista de gastos.
2. ViewModel solicita conversión al `CurrencyConverter` para cada fila.
3. `CurrencyConverter` consulta al `IExchangeRateProvider`; si la
   tasa está en cache (TTL vigente), la usa; si no, va al provider.
4. Si el provider lanza (red caída, timeout), el converter devuelve
   `Result.Failure("ExchangeRateUnavailable", ...)`.
5. El ViewModel captura y muestra la segunda línea solo cuando hay
   conversión exitosa; cuando no, muestra el `statusMessage`
   "Conversión no disponible temporalmente".

### CU-6 — Editar método de pago

1. Usuario hace click en "Editar" en una fila de gasto.
2. El form pre-popula `PaymentMethodId` con el actual (ej.
   "Efectivo").
3. Usuario cambia a "Tarjeta de débito".
4. `UpdateExpenseUseCase` valida la FK y persiste el cambio.

## 6. Requisitos funcionales

### RF-1 — `Expense.PaymentMethodId` + invariantes

- Nueva propiedad `public Guid? PaymentMethodId { get; private set; }`.
- Invariante INV-N (nueva): si `PaymentMethodId.HasValue`, debe ser
  distinto de `Guid.Empty`.
- `Create(...)` acepta `Guid? paymentMethodId` como último parámetro.
- `Update(...)` acepta `Guid? paymentMethodId` como nuevo parámetro.
- Default al crear sin método explícito: `null` (el caller — use case —
  decide si asigna `Efectivo` o deja null; alineado con vision §4.2).

### RF-2 — `Income.PaymentMethodId` + invariantes

- Análogo a RF-1. Mismas reglas. Default: `null` para ingresos.

### RF-3 — Configuración EF Core + migración

- `ExpenseConfiguration`: agregar propiedad con FK:
  ```csharp
  b.Property(e => e.PaymentMethodId);
  b.HasOne<PaymentMethod>()
      .WithMany()
      .HasForeignKey(e => e.PaymentMethodId)
      .OnDelete(DeleteBehavior.SetNull);
  b.HasIndex(e => e.PaymentMethodId);
  ```
- `IncomeConfiguration`: análogo.
- Migración `AddPaymentMethodIdToTransactions`: añade columna
  nullable `PaymentMethodId TEXT` a `Expenses` e `Incomes`; ejecuta
  `UPDATE` para asignar `Efectivo` a filas existentes; añade los FKs
  e índices. **No destructiva** (columna nullable + update idempotente
  con lookup por nombre).

### RF-4 — Commands actualizados

- `RegisterExpenseCommand(Guid CategoryId, DateOnly Date, decimal Amount, Currency Currency, string Description, string? Note, Guid? PaymentMethodId)`.
- `UpdateExpenseCommand(...idem..., Guid? PaymentMethodId)`.
- `RegisterIncomeCommand(Guid? CategoryId, DateOnly Date, decimal Amount, Currency Currency, string Description, string? Note, string? Source, Guid? PaymentMethodId)`.
- `UpdateIncomeCommand(...idem..., Guid? PaymentMethodId)`.

### RF-5 — Validadores FluentValidation

- Regla nueva en los 4 validadores:
  `RuleFor(c => c.PaymentMethodId).Must(id => id == null || id != Guid.Empty).WithMessage("Método de pago inválido.")`.

### RF-6 — DTOs extendidos

- `ExpenseDto(... PaymentMethodId (Guid?), PaymentMethodName (string?))`.
- `IncomeDto(... PaymentMethodId (Guid?), PaymentMethodName (string?))`.
- `PaymentMethodName` se popula en el repo (`Include(p => p.PaymentMethod)`)
  o en el mapping (`ToDto()` con un lookup opcional de `IPaymentMethodRepository`).
- Decisión de implementación en la spec: **lookup en el repo** vía
  `Include` para evitar N+1, con fallback a `PaymentMethodName = null`
  si el método fue borrado (improbable — son del sistema — pero
  defensivo).

### RF-7 — Filtros extendidos

- `ExpenseFilter` agrega `public IReadOnlyList<Guid>? PaymentMethodIds { get; init; }`.
- `IncomeFilter` idem.
- `ExpenseRepository.GetByFilterAsync` /
  `IncomeRepository.GetByFilterAsync` aplican:
  `if (filter.PaymentMethodIds is { Count: > 0 }) query = query.Where(e => filter.PaymentMethodIds.Contains(e.PaymentMethodId.Value));`
  (cuidado con el `null` interno si el método es opcional — usar
  `e.PaymentMethodId != null && filter.PaymentMethodIds.Contains(e.PaymentMethodId.Value)`).

### RF-8 — `IExchangeRateProvider` (abstracción)

- Ubicación: `src/ControlDeFinanzasIA.Application/Abstractions/IExchangeRateProvider.cs`.
- Firma:
  ```csharp
  public interface IExchangeRateProvider
  {
      string ProviderName { get; }
      Task<ExchangeRate> GetRateAsync(Currency from, Currency to, CancellationToken ct = default);
      Task<IReadOnlyDictionary<CurrencyPair, decimal>> GetLatestRatesAsync(CancellationToken ct = default);
  }
  ```
- `CurrencyPair` = `record (Currency From, Currency To)` con
  equality correcto.

### RF-9 — `ExchangeRate` value object

- `public sealed record ExchangeRate(Currency From, Currency To, decimal Rate, DateTime AsOf, string Source)`.
- Invariantes (en factory `Create`):
  - `From != To`.
  - `Rate > 0` y finito.
  - `AsOf.Kind == DateTimeKind.Utc`.
  - `Source` no vacío.
- `decimal Convert(decimal amount) => Math.Round(amount * Rate, 4, MidpointRounding.ToEven);`
- Conversión inversa: `ExchangeRate.Create(To, From, 1/Rate, AsOf, Source)` (verificar coherencia).

### RF-10 — `StubExchangeRateProvider`

- Ubicación: `src/ControlDeFinanzasIA.Infrastructure/ExchangeRates/StubExchangeRateProvider.cs`.
- Tasas configurables: leer de
  `IConfiguration["ExchangeRates:UsdToDop"]` (default `60.0m`).
- Coherencia: deriva `DopToUsd = 1 / UsdToDop` (con redondeo a 4 decimales).
- `ProviderName = "stub"`.
- `GetRateAsync` devuelve `ExchangeRate.Create(...)` con
  `AsOf = _clock.UtcNow`, `Source = "stub-fixed"`.
- `GetLatestRatesAsync` devuelve las 2 tasas (DOP↔USD).
- Thread-safe (uso de `IMemoryCache` o `ConcurrentDictionary` para
  evitar construir en cada call — opcional, se decide en la spec).

### RF-11 — `CurrencyConverter` (servicio de aplicación)

- Ubicación: `src/ControlDeFinanzasIA.Application/Services/CurrencyConverter.cs`.
- Firma:
  ```csharp
  public interface ICurrencyConverter
  {
      Task<Money> ConvertAsync(Money source, Currency target, CancellationToken ct = default);
      Task<IReadOnlyList<MoneyDisplay>> DisplayAsync(Money source, CancellationToken ct = default);
  }
  public sealed record MoneyDisplay(Money Original, Money? Converted, string? ProviderName);
  ```
- Implementación: cache en memoria con TTL configurable (default
  `TimeSpan.FromHours(1)`), fallando con
  `Result.Failure("ExchangeRateUnavailable", ...)`.
- `ConvertAsync`: si `source.Currency == target`, devuelve `source`
  sin consultar el provider.
- `DisplayAsync`: si la moneda origen es USD, devuelve la conversión a
  DOP; si es DOP, devuelve USD. Útil para la UI.

### RF-12 — UI: PaymentMethod picker

- En `ExpensesView.axaml` y `IncomeView.axaml`, añadir un
  `ComboBox` debajo del de categoría:
  ```xml
  <TextBlock Text="{x:Static res:Strings.PaymentMethodLabel}" ... />
  <ComboBox ItemsSource="{Binding PaymentMethods}"
            DisplayMemberBinding="{Binding Name}"
            SelectedValue="{Binding SelectedPaymentMethodId}"
            SelectedValueBinding="{Binding Id}"
            PlaceholderText="{x:Static res:Strings.SelectPaymentMethodWatermark}"
            Classes="ledger-input" />
  ```
- ViewModel expone `IReadOnlyList<CategoryDto>`-like para
  `PaymentMethodDto` (mapeo análogo a `CategoryExtensions` →
  `PaymentMethodExtensions` en `Application/DTOs/`).
- ViewModel carga los métodos en `LoadPaymentMethodsAsync()` análogo
  a `LoadCategoriesAsync()`.

### RF-13 — UI: conversión display-time

- En la lista (`ListBox.ItemTemplate`), añadir segunda línea en
  `Grid` con `TextBlock` bound a una `ConvertedAmountDisplay`:
  ```xml
  <TextBlock Grid.Column="6"
             Text="{Binding ConvertedAmountDisplay}"
             Foreground="{StaticResource PaperDimBrush}"
             FontFamily="{StaticResource FontBody}"
             FontSize="12" />
  ```
- `ExpenseDto` / `IncomeDto` extienden con `string? ConvertedAmountDisplay`
  que el ViewModel popula consultando `ICurrencyConverter` después del
  `RefreshAsync`. Formato: `"≈ 1,500.00 DOP"` o vacío/null si falla.
- Cuando la moneda origen ya es la preferida del usuario (definida en
  Phase 12 — settings), la segunda línea se oculta. Para Phase 5:
  siempre se muestra cuando difiere de la otra moneda disponible
  (siempre hay otra: DOP ↔ USD).

### RF-14 — DI registration

- `Bootstrap.cs`: registrar `IExchangeRateProvider` →
  `StubExchangeRateProvider` (Singleton; el stub no tiene estado).
- Registrar `ICurrencyConverter` → `CurrencyConverter` (Singleton;
  cache en memoria).
- Registrar `IPaymentMethodRepository` (ya existe de Phase 2).
- Registrar `GetPaymentMethodsQuery` (nuevo) en el composition root.

### RF-15 — i18n

- Claves nuevas en `Strings.resx` y `Strings.es-DO.resx`:
  - `PaymentMethodLabel`: "Payment method" / "Método de pago".
  - `SelectPaymentMethodWatermark`: "Select payment method…" /
    "Selecciona un método de pago…".
  - `NoPaymentMethodOption`: "No payment method" / "Sin método de pago".
  - `ConvertedAmountLabel`: "≈ {0}" / "≈ {0}" (placeholder).
  - `CurrencyConversionUnavailableMessage`: "Currency conversion
    temporarily unavailable." / "Conversión de moneda no disponible
    temporalmente."
  - `FilterPaymentMethodsLabel`: "Payment methods" / "Métodos de pago".

### RF-16 — Tests

- Unit tests para:
  - `ExchangeRate.Create` (invariantes, conversión).
  - `StubExchangeRateProvider.GetRateAsync` (tasas coherentes).
  - `CurrencyConverter.ConvertAsync` (cache TTL, fallback, misma
    moneda = no-op).
- Integration tests para:
  - `ExpenseRepository.AddAsync` con `PaymentMethodId` válido e
    inválido (`PaymentMethodNotFound`).
  - `ExpenseRepository.GetByFilterAsync` con `PaymentMethodIds`.
  - Migración `AddPaymentMethodIdToTransactions` aplica el update
    de Efectivo a filas existentes (con `TestDbContext` sembrado).
- VM tests para:
  - `ExpensesViewModel` carga `PaymentMethods`, expone
    `SelectedPaymentMethodId`, persiste en `RegisterExpenseAsync`.
  - `ConvertedAmountDisplay` se popula cuando hay provider
    funcional y queda null cuando falla.

## 7. Requisitos no funcionales

- `dotnet build`: 0 warnings, 0 errors (Release + Debug).
- `dotnet test`: **559/559 tests verdes** al cierre de Phase 5
  (2026-09-08). Este valor es evidencia histórica del cierre; debe
  revalidarse antes de declarar una nueva fase como verificada.
- NetArchTest R-1..R-5 verdes (Application no depende de
  Infrastructure; UI referencia Application + Domain;
  `IExchangeRateProvider` vive en Application).
- `dotnet format style --verify-no-changes` exit 0.
- `dotnet run --project src/ControlDeFinanzasIA.UI` arranca +
  navega a "Gastos" + muestra picker de método + segunda línea de
  conversión + filtra por método.
- Migración es **no destructiva** (columna nullable + update
  idempotente con lookup por nombre).
- Performance: el `ConvertedAmountDisplay` se calcula una vez por
  `RefreshAsync`, no por cada `PropertyChanged`. La cache del
  converter tiene TTL para no martillar el provider.
- Privacidad: ningún endpoint externo se llama en Phase 5 (todo es
  stub local). Cuando llegue el provider real, se documenta el
  contrato de privacidad igual que `ILLMClient`.

## 8. Reglas de negocio

- **`PaymentMethodId` opcional** en gastos e ingresos (vision §4.2:
  "es solo una etiqueta, no se llevan balances por método"). Default
  al crear movimiento nuevo: `null` (no se fuerza Efectivo; el
  usuario decide si lo selecciona).
- **Migración de datos existentes**: al instalar Phase 5 sobre un
  archivo `.db` previo, los gastos e ingresos pre-existentes toman
  `Efectivo` automáticamente (no se quedan con `null` para preservar
  la información histórica que el usuario asume). Esto se documenta
  en el changelog y en el UI de Acerca de / Settings cuando exista
  (Phase 12).
- **Conversión es display-time, no storage**: `Money.Amount` siempre
  se guarda en la moneda original. La conversión se calcula al
  mostrar, no se duplica en una columna `AmountInBaseCurrency`.
- **El provider real se difiere** explícitamente (alineado con
  roadmap §Fase 5 nota). El stub usa una tasa fija; cuando se
  enchufe el provider real, la interfaz y el converter no cambian.
- **Tasa inversa coherente**: si `Rate(USD→DOP) = 60`, entonces
  `Rate(DOP→USD) = 1/60` redondeado a 4 decimales (alineado con
  `Money.HasPrecision(18,4)`). El stub valida esto en un test
  dedicado.
- **Filtro por método es OR**: gastos que tengan cualquiera de los
  métodos seleccionados (mismo patrón que `TagIds` de Phase 4 F-007).
- **`PaymentMethodName` en DTO puede ser null** si el método fue
  borrado (defensivo, aunque los del sistema no se pueden borrar).
- **`ConvertedAmountDisplay` se oculta** si la conversión falla
  (red caída, provider no responde) — no se muestra error en cada
  fila, sino un status global en la barra superior.

## 9. Descomposición en features

| ID    | Feature                                                                                                  | Criticidad | Estimación | Estado            |
|-------|----------------------------------------------------------------------------------------------------------|------------|------------|-------------------|
| 5-001 | `Expense.PaymentMethodId` + `Income.PaymentMethodId` + invariantes + `Create`/`Update` extendidos        | Crítica    | S          | ✅ COMPLETADA      |
| 5-002 | Migración `AddPaymentMethodIdToTransactions` + FK en `ExpenseConfiguration`/`IncomeConfiguration` + seed lookup de Efectivo | Crítica | M          | ✅ COMPLETADA      |
| 5-003 | `IPaymentMethodRepository` use cases (CRUD ya existe de Phase 2) + `GetPaymentMethodsQuery` + `PaymentMethodExtensions` (DTO) + `PaymentMethodDto` | Alta | S          | ✅ COMPLETADA      |
| 5-004 | Commands actualizados (Register/Update Expense + Income) + validadores + tests                           | Crítica    | S          | ✅ COMPLETADA      |
| 5-005 | DTOs extendidos (`PaymentMethodId`, `PaymentMethodName`) + repos con `Include` + tests de query           | Crítica    | S          | ✅ COMPLETADA      |
| 5-006 | Filtros extendidos (`PaymentMethodIds` en `ExpenseFilter`/`IncomeFilter` + repo) + tests                  | Alta       | S          | ✅ COMPLETADA      |
| 5-007 | `ExchangeRate` value object + invariantes + tests                                                        | Alta       | S          | ✅ COMPLETADA      |
| 5-008 | `IExchangeRateProvider` + `StubExchangeRateProvider` (tasas configurables, thread-safe) + tests          | Crítica    | M          | ✅ COMPLETADA      |
| 5-009 | `ICurrencyConverter` + `CurrencyConverter` (cache TTL, fallback, misma-moneda no-op) + tests           | Crítica    | M          | ✅ COMPLETADA      |
| 5-010 | UI: `PaymentMethod` picker en `ExpensesView` + `IncomeView` + ViewModels cargan métodos                  | Alta       | M          | ✅ COMPLETADA      |
| 5-011 | UI: `ConvertedAmountDisplay` segunda línea + binding + manejo de fallo del provider                      | Alta       | M          | ✅ COMPLETADA      |
| 5-012 | i18n: claves nuevas en `Strings.resx` y `Strings.es-DO.resx` + diseño de las 2 vistas con skill frontend | Media      | S          | ✅ COMPLETADA      |
| 5-013 | DI registration en `Bootstrap.cs` + verificación manual + smoke test `dotnet run`                       | Crítica    | S          | ✅ COMPLETADA      |

> **Nota sobre IDs:** los IDs `5-001` a `5-013` son locales a Phase 5.
> Los `F-XXX` también se usan (siguen siendo locales al plan, ver
> convención en `spec/README.md`).

### Dependencias entre features

- 5-001 → 5-002 (entity antes de migración).
- 5-001 → 5-004 (entity antes de commands).
- 5-002 → 5-005 (migración antes de queries).
- 5-003 → 5-004 → 5-005 → 5-006 (cadena secuencial del cableado).
- 5-007 → 5-008 → 5-009 (value object → provider → converter).
- 5-009 + 5-005 → 5-010 → 5-011 (UI requiere DTOs y converter).
- 5-010 + 5-011 + 5-012 → 5-013 (DI al final, después de todo lo
  demás).

### Orden recomendado

```
5-001 → 5-002 → 5-007 → 5-008 → 5-009 → 5-003 → 5-004 → 5-005 → 5-006 → 5-010 → 5-011 → 5-012 → 5-013
```

Dos cadenas paralelas posibles:

- **Rama A (PaymentMethod)**: 5-001 → 5-002 → 5-003 → 5-004 → 5-005 →
  5-006 → 5-010 → 5-013.
- **Rama B (Multi-moneda)**: 5-007 → 5-008 → 5-009 → 5-011 → 5-013.
- **Feature compartida**: 5-012 (i18n) corre al final para tocar todos
  los textos juntos.

### Trabajo en paralelo (si el equipo lo permite)

- Persona A (rama PaymentMethod): 5-001 → 5-002 → 5-003 → 5-004 →
  5-005 → 5-006 → 5-010.
- Persona B (rama Multi-moneda): 5-007 → 5-008 → 5-009 → 5-011.
- Persona C (UI + i18n + DI): espera a que A y B cierren 5-005 y
  5-009, luego hace 5-012 → 5-013.

## 10. Estimación

- **L (1-2 semanas)** para Phase 5 completa con un equipo pequeño.
- **M (3-5 días)** para el MVP (5-001 + 5-002 + 5-003 + 5-004 + 5-005
  + 5-007 + 5-008 + 5-009 + 5-013).
- **S (1-2 días)** para solo la rama PaymentMethod (5-001 a 5-006 +
  5-010) si la multi-moneda se difiere a Phase 5b.
- **S (1-2 días)** para solo la rama multi-moneda (5-007 a 5-009 +
  5-011) sin tocar el cableado de PaymentMethod.

## 11. Riesgos principales

| Riesgo | Probabilidad | Impacto | Mitigación |
|--------|-------------|--------|------------|
| **Decisión del `IExchangeRateProvider` real**: la spec de Phase 5 debe dejar la interfaz tan estable que un cambio de proveedor no toque use cases. | Alta (decisión de producto) | Medio | Interfaz minimal (`GetRateAsync` + `GetLatestRatesAsync` + `ProviderName`); stub funcional; ADR-014 al final con opciones de providers reales (OpenExchangeRates free tier, exchangerate.host, BC RD API). |
| **Migración de datos existentes no destructiva**: el `UPDATE` para asignar `Efectivo` debe ser idempotente (re-ejecutable). | Baja | Alto | Test de integración que ejecuta la migración dos veces sobre el mismo DB y verifica que las filas no cambian la segunda vez. |
| **Tasa inversa coherente**: `1/60 = 0.0166...` redondeado puede perder precisión al hacer round-trips (`USD→DOP→USD` ≠ USD original). | Media | Bajo | Documentar la pérdida en ADR; permitir una tolerancia de 0.01 USD en tests de round-trip. La app no hace round-trips en flujos reales. |
| **Performance del lookup de `PaymentMethodName` en cada fila**: si el repo hace N+1 (`Include` por fila), la lista de 50 gastos × 1 query cada uno = 50 queries. | Media | Medio | `Include(p => p.PaymentMethod)` en `GetByFilterAsync` y `GetAllAsync`; verificar con `dotnet ef dbcontext info` o SQL logging en integration test. |
| **Cache del `CurrencyConverter` stale**: si la tasa cambia mucho (ej. crisis cambiaria RD), el cache de 1h muestra números viejos. | Baja | Bajo | TTL configurable + botón "Refrescar tasas" opcional en settings (Phase 12). Documentar la limitación. |
| **`PaymentMethodId` nullable + filtro**: `Where(e => filter.PaymentMethodIds.Contains(e.PaymentMethodId.Value))` lanza si `e.PaymentMethodId == null`. | Media | Alto | Test dedicado con movimientos `null` y filtro activo. |
| **i18n: claves de conversión** ("≈ 1,500.00 DOP") tienen formato variable según cultura. | Baja | Bajo | Usar `Money.ToString(culture)` que ya formatea correctamente; pasar la cultura desde `IClock` o `CultureInfo.CurrentCulture`. |
| **Provider real sin internet = UI muerta**: si Phase 5 enchufa un provider real y el usuario está offline, la conversión falla en cada render. | Media | Medio | El fallback del converter (devuelve `Failure`) + UI muestra `ConvertedAmountDisplay = null` sin romper. **No** se hace la app inutilizable sin provider. |
| **Tests del provider** marcados como flaky si dependen de la hora del sistema (`AsOf = _clock.UtcNow`). | Baja | Bajo | Inyectar `IClock` en `StubExchangeRateProvider` y asserts sobre delta, no igualdad exacta. |

## 12. Criterios para considerarla completada

- ✅ `PaymentMethodId` cableado en `Expense` + `Income` (entity,
  config, migración, repos, DTOs, use cases, UI).
- ✅ Migración `AddPaymentMethodIdToTransactions` aplicada sobre un
  archivo `.db` con datos de Phase 4; los registros pre-existentes
  quedan con `Efectivo`.
- ✅ `IExchangeRateProvider` + `StubExchangeRateProvider`
  implementados y registrados en DI.
- ✅ `CurrencyConverter` con cache TTL implementado y testeado.
- ✅ Segunda línea de conversión visible en `ExpensesView` e
  `IncomeView` cuando difiere de la otra moneda.
- ✅ Picker de `PaymentMethod` funcional en ambos formularios.
- ✅ Filtro por método funciona (OR semantics).
- ✅ Tests de integración cubren queries con `PaymentMethodIds` y la
  migración completa (≥ 5 escenarios).
- ✅ **559/559 tests verdes** al cierre de Phase 5.
- ✅ NetArchTest R-1..R-5 verdes.
- ✅ `dotnet run` arranca + registra un gasto de `25 USD` con
  método "Tarjeta de crédito" + lo ve en la lista con
  `≈ 1,500.00 DOP` + filtra por método.

## 13. Out of scope (detallado)

- **`IExchangeRateProvider` real** con API externa (OpenExchangeRates,
  exchangerate.host, BC RD). Se difiere a Phase 5b o una fase posterior
  con decisión de conectividad.
- **Almacenamiento histórico de tasas por transacción**. Phase 5
  muestra la **tasa vigente al consultar** (con cache). El versionado
  histórico se aborda cuando los reportes lo necesiten (Phase 8+).
- **`base_currency` configurable** del usuario. Phase 12 settings.
- **CRUD completo de métodos personalizados** (los 5 del sistema son
  suficientes para v1; el repo ya soporta add/update/remove pero la UI
  no se expone). Decisión: Phase 12 o sub-feature de Phase 5 a fijar
  en la spec.
- **Background job** que refresca tasas automáticamente. Lazy con TTL
  es suficiente para v1.
- **Gráfico de doble eje moneda** en visualizaciones. Phase 8.
- **Reportes con conversión cruzada** (sumar gastos en DOP + USD
  mezclados en un solo total). Phase 8.
- **Notificaciones de cambio significativo** de tasa. Phase 17.
- **Phase 6 — Recurrentes (suscripciones, gastos fijos)**.
- **Phase 7 — Deudas con plan de cuotas**.
- **Phase 8 — Dashboard / visualizaciones**.
- **Phase 10 — Presupuestos** (interacción presupuesto en DOP vs gasto
  real en USD queda para Phase 10).
- **Phase 11 — Metas de ahorro**.

## 14. Referencias

- `docs/vision.md` §3 (propuesta de valor), §4.2 (organización de un
  movimiento), §8 (casos de borde clave — conversión display-time).
- `docs/roadmap.md` §Fase 5 (líneas 270-310).
- `docs/architecture.md` §6 (Dominio: entidades centrales,
  `Transaction` menciona `PaymentMethodId`), §7 (Application:
  abstracciones y use cases), §8 (Infraestructura: persistencia y
  mapping), §9 (UI), §17 (i18n y formato).
- `spec/constitution/tech-stack.md` — MVVM strict, async/await,
  errors-as-values, i18n con `IStringLocalizer`.
- `spec/constitution/mission.md` — Clean Architecture.
- `spec/features/phase-04/F-00{1..8}-*.done.md` — precedentes de
  Phase 4 que se replican (especialmente F-005 Update/Delete y F-007
  filtros extendidos con `TagIds`).
- `spec/phases/phase-04/features.md` — formato y estructura del phase
  plan.
- `src/ControlDeFinanzasIA.Domain/ValueObjects/Money.cs` — el value
  object **no** se modifica (la conversión no es responsabilidad del
  dominio).
- `src/ControlDeFinanzasIA.Domain/Catalog/PaymentMethod.cs` — la
  entidad ya existe; Phase 5 la cablea.
- `src/ControlDeFinanzasIA.Domain/Enums/Currency.cs` — el enum ya
  tiene `DOP` y `USD`.
- `src/ControlDeFinanzasIA.Infrastructure/Persistence/SeedData.cs` —
  los 5 métodos del sistema (Efectivo, Tarjeta de débito, Tarjeta de
  crédito, Transferencia, Otro) ya están sembrados; la migración de
  Phase 5 los referencia por nombre.
