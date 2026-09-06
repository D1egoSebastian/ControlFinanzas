# Specification — F-006: `SavedView` + serialización JSON versionada de filtros + repositorio + use cases

> **Fase:** 4 — Transacciones: ingresos, edición, borrado, tags, filtros, vistas guardadas
> **Feature:** F-006 (local a esta fase; `4-006` en el phase plan)
> **Spec ID:** SPEC-F006P4
> **change_id:** `f-006p4-saved-view-json`
> **Modo:** Standard (SDD: propose → spec → tasks → apply → verify → archive)
> **Estado:** Pendiente de implementación (renombrar a `.done.md` tras apply + verify)
>
> **Nota de numeración:** este `F-006` es el de la **Fase 4**
> (`spec/phases/phase-04/features.md`, §4.1 líneas 49-51, §RF-12 a
> §RF-15 líneas 202-215, §RF-20 línea 251, §9 tabla `4-006`). Los IDs
> son locales a cada fase; el folder los desambigua. Ver
> `spec/README.md`.
>
> Fuentes de verdad: `docs/vision.md` (§4.3 filtros y vistas guardadas),
> `docs/architecture.md` (§4 R-1..R-5, §7 use cases, §8 persistencia),
> `docs/roadmap.md` (§Fase 4), `spec/constitution/tech-stack.md`,
> `spec/constitution/mission.md`,
> `spec/phases/phase-04/features.md` (§RF-12..RF-15, §8 reglas de
> negocio, §11 risk "Versionado de JSON de SavedView"),
> `.atl/sdd/f-006p4-saved-view-json/proposal.md` (PRD de origen — 223
> líneas; Q-1..Q-4 ratificadas y diseño ajustado en la ronda
> interactiva — supersesiones documentadas en §3),
> `spec/features/phase-04/F-007-extend-filters-tagids.done.md`
> (patrón espejo de estructura + filtros 6 props con `TagIds`),
> `spec/features/phase-04/F-005-update-delete-use-cases.done.md`
> (patrón espejo de use cases: D-2 validación sync, D-4
> `ArgumentException` → `"DomainInvariant"`, C-3 convención de tests,
> D-ML idioma de mensajes),
> `spec/features/phase-04/F-004-tag-entity-and-many-to-many.done.md`
> (patrón espejo `Tag`/`TagRepository`/`TagConfiguration`).

---

## 1. Metadata

| Campo | Valor |
| --- | --- |
| **change_id** | `f-006p4-saved-view-json` |
| **spec_id** | `SPEC-F006P4` |
| **modo** | Standard (SDD: propose → spec → tasks → apply → verify → archive) |
| **fase** | 4 |
| **feature** | F-006 (local a Fase 4) |
| **dependencies (build)** | F-007 Fase 4 (`ExpenseFilter`/`IncomeFilter` 6 props con `TagIds`, aplicados en ambos repos) ✅ archivada; F-005 Fase 4 (`TransactionType` enum en `Domain/Enums`) ✅ archivada; Phase 1 F-006 (`ISavedViewRepository` con 5 métodos + placeholder `SavedView`) ✅ archivada |
| **dependencies (contrato)** | Phase 1 F-005 (`Result` non-generic, `Value: object?` — **no se modifica**) ✅; Phase 1 F-004 (`IClock`) ✅; Phase 1 F-006 (contrato commands `Task<Result>` vs queries) ✅ |
| **dependencies (test infra)** | Phase 2 F-006 (`TestAppDbContextFactory`, SQLite in-memory + `MigrateAsync` por test — la migración nueva se aplica sola) ✅; `FakeClock` ✅; `NullLogger<>` de `Microsoft.Extensions.Logging.Abstractions` ✅ (patrón verificado en `RegisterIncomeUseCaseTests`) |
| **dependencies (paquetes)** | **Ninguna nueva** — `System.Text.Json` es BCL en .NET 8 (verificado: sin entrada en `Directory.Packages.props`); FluentValidation 11.10.0 ya referenciado vía CPM |
| **unblocks** | 4-008 (UI: combobox de vistas guardadas + botones guardar/aplicar/eliminar + `GetIncomesQuery` — última feature de Phase 4) |
| **artifact_store** | Engram (objetivo: `sdd/f-006p4-saved-view-json/spec` + `sdd/f-006p4-saved-view-json/proposal`) + filesystem (este archivo) |
| **status** | Pendiente de implementación |
| **línea base de tests (post-4-007)** | Suite post F-007: **262 unit + 93 integration verdes** + 1 fallo **pre-existing conocido de `Money`** (`ExpenseSchemaTests.Money_OwnsOne_ProducesTwoColumns`, documentado en verify-report F-007 — no regresión). El implementador reporta las cuentas exactas observadas |
| **Q-1..Q-4 ratificadas (ronda interactiva)** | Q-1 **serialización directa** (sin DTO); Q-2 **persistir `Page`/`PageSize`**; Q-3 **diferir `DeleteSavedViewUseCase`** a 4-008; Q-4 **unicidad exacta** (case-sensitive) — ver D-6/D-7/D-8/D-4 |

---

## 2. Contexto y objetivo

Phase 4 ya cerró el eje de filtrado: F-007 completó el cuarteto
(`CategoryId`, `TagIds`, `DateFrom`, `DateTo`) en ambos repositorios con
semántica OR para tags. El usuario puede **filtrar**, pero no puede
**guardar** un filtro recurrente ("mis gastos de transporte del último
mes") ni recuperarlo después. F-006 cierra ese eje y es la base directa
de 4-008 (combobox de vistas guardadas + botones guardar/aplicar/eliminar
sobre `ExpensesView`/`IncomeView`).

Esto requiere cuatro piezas: (a) **promover el placeholder `SavedView`
de Phase 1** (record de 13 líneas, sin invariantes, sin factory) a
entidad completa con `EntityType` + `SchemaVersion` + timestamps; (b)
**serializar `ExpenseFilter`/`IncomeFilter` a JSON versionado** —
round-trip safe y tolerante a versiones futuras — con `System.Text.Json`
(BCL, cero paquetes nuevos); (c) **`SavedViewRepository`** +
`SavedViewConfiguration` + `DbSet` + migración única `AddSavedViewsTable`;
y (d) **tres orquestadores** (`SaveCurrentFilter`, `ApplySavedView`,
`GetSavedViews`) que consumirá la UI de 4-008. Phase 4 no innova
arquitectura: **replica los patrones de `Tag` (4-004)** para
entidad/repo/configuración y de `RegisterIncome` (F-003 P4) /
`GetTransactionsQuery` (F-005 P3) para los use cases.

**Correcciones al contrato de lanzamiento (verificadas en fuente):** (1)
la entidad vive en `Domain/Views/`, **no** en `Domain/Catalog/` — se
promueve in-situ conservando el namespace; (2) el registro DI del
repositorio va en `ServiceCollectionExtensions.AddPersistence`
(**Scoped**), no en `Bootstrap` (que es para use cases/queries/validators
**Transient**); (3) los commands **no** llevan timestamps — el timestamp
sale de `IClock` (precedente `RegisterIncomeUseCase`/`UpdateExpenseUseCase`);
(4) `Result` es **non-generic** (`Value: object?`) — todos los
orquestadores retornan `Task<Result>` y el caller castea `Value`. La
ronda interactiva ratificó Q-1..Q-4 y **ajustó el diseño del proposal**
(command único con `object Filter`, excepciones tipadas en el serializer
mapeadas a `Result` en el use case, `ApplySavedViewResult` de 2 props) —
cada supersesión está documentada en §3.

**Estado actual verificado (lectura de fuente, no suposiciones):**

| Elemento | Estado real (verificado) |
| --- | --- |
| `src/.../Domain/Views/SavedView.cs` | **Placeholder de 13 líneas** (Phase 1 F-006): `sealed record` con `Id`, `Name`, `FilterJson: IReadOnlyDictionary<string, string[]>?`. **SIN** `EntityType`, **SIN** `SchemaVersion`, **SIN** timestamps, **SIN** factory, **SIN** invariantes |
| `src/.../Application/Abstractions/ISavedViewRepository.cs` | **EXISTE** (57 líneas) con los 5 métodos estándar (`AddAsync`, `UpdateAsync`, `RemoveAsync`, `GetByIdAsync`, `GetAllAsync`), XML doc en español, `using Application.Common` + `Domain.Views`. **FALTA** `GetByEntityTypeAsync` — mismo estado que `ITagRepository` antes de 4-004 |
| `SavedViewRepository` / `SavedViewConfiguration` | **NO EXISTEN** (verificado por glob) |
| `AppDbContext.cs` | 80 líneas, 5 DbSets (`Categories`, `PaymentMethods`, `Expenses`, `Incomes`, `Tags`). **SIN** `DbSet<SavedView>`. Config descubierta vía `ApplyConfigurationsFromAssembly` |
| Migraciones | 6 versionadas; última `20260903233252_AddIncomeTagsJoinTable`. **NO existe** tabla `SavedViews`. Convención `<YYYYMMDDhhmmss>_Name`, file-scoped namespace |
| `Domain/Enums/TransactionType.cs` | **EXISTE** (14 líneas, creado en F-005 P4): `{ Expense, Income }`, XML doc por valor. Se reutiliza como `EntityType` (D-2 de esta spec no crea enum nuevo) |
| `ExpenseFilter.cs` / `IncomeFilter.cs` | 50/54 líneas; 6 props `init` c/u (incl. `IReadOnlyList<Guid>? TagIds`, F-007 ✅). "Dumb records" sin validación — serializables con STJ (records + init + `DateOnly`/`Guid`/`IReadOnlyList<Guid>` soportados nativamente en .NET 8) |
| `Result.cs` | 47 líneas, non-generic, `Value: object?`, `Success(object)` / `Failure(code, msg[, details])` — **no se toca** |
| `Tag.cs` / `TagRepository.cs` / `TagConfiguration.cs` | 142/183/29 líneas — **plantilla exacta a replicar**: sealed class con private setters + 2 ctors privados + `Create`/`Update` + `ValidateAndNormalize`; repo con pre-check `DuplicateName` + catch `DbUpdateException` (SQLite 19) + `NotFound` + guards `Guid.Empty` + `AsNoTracking` + `OrderBy(Name)`; config con PK `ValueGeneratedNever` + unique index sobre `Name` |
| DI repos | Repos **Scoped** en `Infrastructure/Persistence/ServiceCollectionExtensions.AddPersistence` (líneas 70-74: `AddScoped<ITagRepository, TagRepository>()` es el último); use cases/queries/validators **Transient** en `Bootstrap.ConfigureServices` (líneas 66-75) |
| `IClock` | `Application/Abstractions/IClock.cs` con `UtcNow` (+ `Today`); `SystemClock` singleton ya registrado en `Bootstrap` línea 57; `FakeClock` (21 líneas) seteable |
| Fakes | `FakeExpenseRepository`/`FakeIncomeRepository` (97 líneas: overrides + captura de llamadas) — **NO existe** `FakeSavedViewRepository` |
| Convención de tests | F-005 C-3 (locked): `Tests.Unit/Application/<Aggregate>/<UseCase>/<X>Tests.cs` — **sin segmento `UseCases`** (verificado: `Application/Incomes/RegisterIncome/RegisterIncomeUseCaseTests.cs`, `Application/Transactions/DeleteTransaction/...`) |
| Línea base de tests (post 4-007) | **262 unit + 93 integration verdes** + 1 fallo **pre-existing conocido de `Money`** (documentado en verify-report F-007) |
| Blast radius del placeholder | `grep SavedView` → solo 2 archivos (`SavedView.cs` + `ISavedViewRepository.cs`). Sin DbSet, sin tests, sin UI, sin `SeedData` → reemplazo del shape es **seguro** |

**Objetivos concretos:**

1. **Promover `SavedView`** de record placeholder a `public sealed class`
   con 7 props, 2 ctors privados, `Create`/`Update` con invariantes
   (D-1/D-2/D-3) — espejo línea a línea de `Tag.cs`.
2. **Extender `ISavedViewRepository`** con `GetByEntityTypeAsync` (D-11);
   las 5 firmas existentes quedan intactas (compatibilidad del contrato
   Phase 1).
3. **`SavedViewConfiguration` + `DbSet<SavedView>` + migración única
   `AddSavedViewsTable`** (aditiva, sin `InsertData`, índice único sobre
   `Name` + índice sobre `EntityType`).
4. **`SavedViewRepository`** (6 métodos) espejo de `TagRepository`
   (pre-check + race + guards + `AsNoTracking` + orden).
5. **`FilterJsonSerializer`** en `Application/Common`: envelope
   `{ schemaVersion, type, data }` con `System.Text.Json` camelCase,
   `CurrentSchemaVersion = 1`, reglas forward/backward-compat (D-5,
   D-9, D-10) y overload no-genérico.
6. **3 orquestadores** (`SaveCurrentFilterUseCase`,
   `ApplySavedViewUseCase`, `GetSavedViewsUseCase`) + 1 command + 1
   validator + 2 query records + `ApplySavedViewResult` — `Result`
   non-generic, timestamps desde `IClock`.
7. **DI completa**: 1 Scoped en `AddPersistence` + 4 Transient en
   `Bootstrap`; doc-comments de los 3 archivos tocados actualizados.
8. **Tests**: 4 archivos unit (serializer + 3 use cases, con
   `FakeSavedViewRepository` nuevo) + 1 integration (repo, con JSON
   round-trip a través de la BD). Pure xUnit `Assert.*`, sin
   FluentAssertions, sin mocking. Gates verdes (AC-16).

---

## 3. Decisiones (locked-in — no re-abrir)

Las decisiones D-1..D-12 fueron bloqueadas en el proposal + ronda
interactiva. Donde la ronda **supera** al proposal, se documenta
explícitamente (misma práctica que F-007 D-7).

### D-1 — `SavedView` se promueve in-situ como `sealed class` en `Domain/Views/`

**Decisión:** `public sealed class SavedView` (NO record, NO base class)
reemplaza al placeholder de 13 líneas **en el mismo archivo**
`Domain/Views/SavedView.cs`; el namespace
`ControlDeFinanzasIA.Domain.Views` se conserva. Private setters, ctor
privado sin parámetros para EF Core + ctor privado completo invocado por
el factory — espejo exacto de `Tag.cs` (que también se promovió de
record placeholder en 4-004).

**Justificación:** el blast radius del placeholder es de 2 archivos
(verificado por grep: solo `ISavedViewRepository.cs`, cuyas firmas van
por nombre de tipo y sobreviven al cambio de shape). Mover la entidad a
`Domain/Catalog/` rompería el namespace del interface de Phase 1 sin
ganancia — `Views` es un aggregate distinto del catálogo.

### D-2 — 7 propiedades: `Id`, `Name`, `EntityType`, `FilterJson`, `SchemaVersion`, `CreatedAt`, `UpdatedAt`

**Decisión:** `Id` (Guid, `Guid.NewGuid()` en `Create`,
`ValueGeneratedNever`); `Name` (string, requerido, post-trim no vacío,
≤ 100, **índice único**); `EntityType` (`TransactionType` — se reutiliza
el enum de F-005 P4, NO se crea `SavedViewEntityType`); `FilterJson`
(string, requerido, contiene el envelope serializado completo);
`SchemaVersion` (int, ≥ 1, default 1, **columna denormalizada** además
del campo dentro del envelope); `CreatedAt`/`UpdatedAt` (DateTime UTC,
no-default, responsabilidad del caller vía `IClock` — espejo `Tag`).

**Justificación:** reutilizar `TransactionType` evita un enum duplicado
con members idénticos (`{Expense, Income}` — RF-12 ofrecía ambos). La
columna `SchemaVersion` existe a propósito (doble con el envelope): es
la fuente para queries de mantenimiento ("¿cuántas vistas hay en
versión 1?") **sin parsear JSON**; el envelope es la fuente de verdad al
parsear. Columna y envelope SIEMPRE coinciden al escribir (D-3).

### D-3 — `Create(name, entityType, filterJson, createdAt)` + `Update(name, filterJson, updatedAt)`; `EntityType` y `SchemaVersion` inmutables vía `Update`

**Decisión:** factory estático
`Create(string name, TransactionType entityType, string filterJson, DateTime createdAt)`
+ método de instancia
`Update(string name, string filterJson, DateTime updatedAt)` — ambos
validan invariantes (AC-1). `Create` estampa `SchemaVersion = 1`
(internamente, sin parámetro) y `UpdatedAt = createdAt`. `Update` NO
modifica `Id`, `EntityType`, `SchemaVersion` ni `CreatedAt` (una vista no
cambia de tipo — regla de negocio §8 del plan; el versionado se
re-estampa en un futuro bump vía nuevo método/overload, fuera de alcance
hoy).

**Justificación:** espejo de `Tag.Create`/`Tag.Update` (factory +
re-validación) y de `Income.Update` (param `updatedAt`). El acople
columna↔serializer se blinda con el test
`ExecuteAsync_AssignsSchemaVersionFromSerializer` (AC-14): afirma
`SavedView.SchemaVersion == FilterJsonSerializer.CurrentSchemaVersion` —
si un futuro bump de `CurrentSchemaVersion` no actualiza el estampado
del factory, ese test falla y fuerza la revisión (tripwire deliberado).

> **Supersesión sobre el proposal:** su D-1 proponía
> `Create(..., schemaVersion, createdAt)` / `Update(..., schemaVersion,
> updatedAt)`. La ronda interactiva **eliminó el parámetro** (SchemaVersion
> se estampa internamente = 1): ningún caller actual puede suministrar
> otra versión y el parámetro invita a divergencia columna↔envelope.

### D-4 — Unicidad de `Name` = match exacto (case-sensitive)

**Decisión:** la unicidad de `Name` es por **match exacto** (collation
BINARY de SQLite), espejo de `Tag` (4-004). "Transporte" y "transporte"
son vistas distintas y ambas válidas.

**Justificación:** consistente con `TagRepository` y el unique index por
defecto de SQLite. Case-insensitive requeriría collation custom (soporte
limitado en SQLite) o normalización (lowercase) en el dominio — ninguna
existen en el codebase. Q-4 ratificada.

### D-5 — Envelope JSON `{ "schemaVersion": int, "type": string, "data": <filter> }`

**Decisión:** `FilterJson` almacena un envelope con tres campos:
`schemaVersion` (int, = `CurrentSchemaVersion` al escribir), `type`
(string: `"ExpenseFilter"` o `"IncomeFilter"` — el nombre del tipo CLR
del filter), y `data` (el JSON del filter record **directamente**).
Serialización con `System.Text.Json`,
`PropertyNamingPolicy = JsonNamingPolicy.CamelCase`,
`WriteIndented = false`. Round-trip safe:
`serialize → deserialize → serialize` produce bytes idénticos.

**Justificación:** el envelope desacopla el formato almacenado del shape
de los filters (la motivación de los DTOs de RF-13 se satisface con
`schemaVersion` sin duplicar mantenimiento — ver D-6). camelCase es la
convención JSON del stack; sin indent minimiza el tamaño de la columna.

### D-6 — Serialización directa del filter en `data` (sin capa DTO)

**Decisión:** `data` contiene el filter record (`ExpenseFilter` /
`IncomeFilter`) serializado **tal cual** — sus 6 props (`Page`,
`PageSize`, `CategoryId`, `DateFrom`, `DateTo`, `TagIds`) con defaults
`null` preservados explícitamente en el JSON. NO se crean
`ExpenseFilterDto`/`IncomeFilterDto` (RF-13 queda **supersada**).

**Justificación (Q-1 ratificada):** los filters son "dumb records" ya
versionados por el envelope; la capa DTO duplica mantenimiento en cada
extensión de filtro (F-007 ya extendió una vez — añadir `TagIds` habría
tocado 2 DTOs + 2 mapeos). STJ serializa records con init props +
`DateOnly` + `Guid` + `IReadOnlyList<Guid>` nativamente en .NET 8.

### D-7 — `Page`/`PageSize` se persisten en la vista

**Decisión (Q-2 ratificada):** el filter se serializa completo, incl.
`Page`/`PageSize`. Cero mapping, round-trip trivial. Si el producto
prefiere "vista = criterios sin paginación", **4-008** resetea `Page = 1`
al aplicar en el ViewModel — decisión de UI, no de persistencia.

**Justificación:** "stripear" `Page`/`PageSize` exigiría un DTO
intermedio (convirtiendo Q-1 en DTO obligatorio) para guardar
`PageSize` razonable o perderlo. Persistir y resetear en la UI es el
camino de menor maquinaria.

### D-8 — `DeleteSavedViewUseCase` se difiere a 4-008

**Decisión (Q-3 ratificada):** este feature NO crea un use case de
borrado. `SavedViewRepository.RemoveAsync` **sí existe** (contrato de
repo completo) y queda disponible para 4-008, que lo introducirá junto
al botón "Eliminar vista" (RF-19), espejo de `DeleteTransactionUseCase`
sin validator.

**Justificación:** disciplina de scope (AC-16): un use case sin
consumidor es código muerto hasta 4-008. El repo ya expone la operación.

### D-9 — Forward-compat: `schemaVersion > current` → `InvalidOperationException`

**Decisión:** al deserializar, si el envelope declara un
`schemaVersion` **mayor** que `FilterJsonSerializer.CurrentSchemaVersion`,
el serializer lanza
`InvalidOperationException($"Schema version {envelope.SchemaVersion} is not supported. Current version: {CurrentSchemaVersion}.")`.
`ApplySavedViewUseCase` captura esa excepción y la mapea a
`Result.Failure("IncompatibleVersion", ex.Message)` (código del plan §8).
Versiones **menores o iguales** a la actual se aceptan (path de
migración forward: una v2 podrá leer datos v1).

**Justificación:** "ApplySavedView rechaza versiones futuras con
`IncompatibleVersion` pero acepta versiones iguales o anteriores"
(regla de negocio §8 del plan). Rechazar — nunca degradar silenciosamente
— es la constitución "no silent failures". Excepciones tipadas dentro,
errors-as-values en el boundary (eco de la constitución y del patrón
`DomainInvariant` de F-005 D-4).

> **Supersesión sobre el proposal:** su D-5 definía una excepción custom
> `FilterJsonException` + códigos a nivel serializer. La ronda
> interactiva la simplificó a **dos excepciones estándar** con semántica
> disjunta: `InvalidOperationException` = versión no soportada;
> `JsonException` = payload corrupto/malformado. El use case las mapea a
> `"IncompatibleVersion"` / `"CorruptedFilter"`.

### D-10 — Backward-compat: envelope legacy sin `schemaVersion` → default 1

**Decisión:** si el JSON almacenado NO trae el campo `schemaVersion`
(formato legacy hipotético), se interpreta como **versión 1**. El
default se resuelve en `TryParseEnvelope` (parseo tolerante del
envelope). Un `schemaVersion` **explícito < 1** (p. ej. `0` o negativo)
NO se trata como legacy: se rechaza como malformado (`JsonException`).

**Justificación:** tolerar el ausente es el path de migración forward
(una v2 seguirá leyendo vistas v1); rechazar el explícito inválido evita
que basura se camufle de "legacy". El test
`Deserialize_WithMissingSchemaVersion_DefaultsTo1` usa JSON legacy
hard-coded (sin `schemaVersion`) para blindar el contrato.

### D-11 — `ISavedViewRepository` gana exactamente 1 método: `GetByEntityTypeAsync`

**Decisión:** `Task<IReadOnlyList<SavedView>>
GetByEntityTypeAsync(TransactionType entityType, CancellationToken ct =
default)`. Las 5 firmas existentes quedan **intactas** (compatibilidad
del contrato Phase 1). Requiere `using
ControlDeFinanzasIA.Domain.Enums;` en el interface.

**Justificación:** es la query que alimenta el combobox de 4-008
("vistas de gastos" / "vistas de ingresos"). El filtro por columna int
(indexada, AC-2/AC-5) es más barato que parsear JSON.

### D-12 — `EntityType` se almacena explícito (no se deriva del tipo del filter)

**Decisión:** `SavedView.EntityType` es la fuente de verdad del tipo de
vista y se asigna desde el command (`SaveCurrentFilterCommand.EntityType`),
NO se deriva del tipo runtime del filter. Esto permite el query
explícito de D-11. **Regla derivada (cierra el hueco de `object
Filter`):** como el command lleva `EntityType` y `Filter: object` por
separado, `SaveCurrentFilterUseCase` valida el **pairing** al despachar
(§AC-9): runtime type `ExpenseFilter` ⟺ `TransactionType.Expense`,
`IncomeFilter` ⟺ `TransactionType.Income`; mismatch o tipo no soportado
→ `Result.Failure("Validation", ...)`. Nunca se persiste una vista cuyo
`EntityType` contradiga su `data`.

**Justificación:** el proposal eliminaba esta clase de bug por
construcción con 2 commands tipados; la ronda interactiva prefirió 1
command + `object` (menos archivos, un solo validator) — el guard de
pairing restaura la seguridad que el tipado perdía. "No silent
failures": sin el guard, un mismatch se detectaría recién al aplicar
(como `CorruptedFilter`), demasiado tarde y con etiqueta engañosa.

### 3.1 Tabla de supersesiones sobre el proposal (ronda interactiva)

| Tema | Proposal (origen) | Esta spec (locked) |
| --- | --- | --- |
| Command de save | D-6: 2 commands tipados (`SaveExpenseFilterCommand`/`SaveIncomeFilterCommand`) + 2 validators + 2 overloads | **1 command** `SaveCurrentFilterCommand(Name, EntityType, Filter: object)` + **1 validator** (2 reglas) + guard de pairing (D-12) |
| Excepciones del serializer | D-5: `FilterJsonException` custom + códigos a nivel serializer | **`InvalidOperationException`** (versión futura, D-9) + **`JsonException`** (corrupto); el use case mapea a `"IncompatibleVersion"`/`"CorruptedFilter"` |
| Firma del factory | D-1: `Create(..., schemaVersion, ...)` / `Update(..., schemaVersion, ...)` | **Sin parámetro `schemaVersion`** — estampado interno = 1 (D-3, tripwire en AC-14) |
| `ApplySavedViewResult` | D-7: 3 props `(SavedView View, TransactionType EntityType, object Filter)` | **2 props**: `(TransactionType EntityType, object Filter)` — el caller ya tiene la vista (la elegió del listado) |
| Nombres de requests | `ApplySavedViewRequest` / `GetSavedViewsRequest`; clase query `GetSavedViewsQuery` | **`ApplySavedViewQuery`** / **`GetSavedViewsQuery`** (records); clases: `*UseCase` para los 3 |
| `Domain/SavedViewTests.cs` | ≥8 tests de invariantes `Create`/`Update` | **Excluido** (presupuesto de PR): invariantes cubiertas vía validator-first + catch `"DomainInvariant"` + integration (§6.6) |
| Doc-refresh de `TransactionType.cs` | D-2: summary menciona a `SavedView` | **NO sancionado** — el contrato de lanzamiento lista `TransactionType.cs` en "no modificar"; queda intacto |
| DI Bootstrap | D-12: 5 Transient + 2 validators | **4 Transient** (3 use cases + 1 validator) + 1 Scoped en `AddPersistence` |

### 3.2 Idioma de mensajes (hereda D-ML de F-005 — decisión de consistencia)

La convención del codebase (locked en F-005 D-ML, verificada en
`RegisterExpenseValidator`/`RegisterIncomeValidator`/
`TagRepository`/`Result.cs`) es: **códigos de error en inglés** +
**mensajes humanos en español** (validadores, resúmenes de use case,
mensajes de repo). Las invariantes de dominio y las excepciones técnicas
que propagan vía `ex.Message` son **inglés**. F-006 sigue esa misma
convención:

| Mensaje | Idioma | Origen |
| --- | --- | --- |
| FluentValidation por-campo (`Name`, `Filter`) | **Español** | espejo de `RegisterIncomeValidator` (los sketches ingleses del contrato — "Name cannot be empty or whitespace." — se consideran ilustrativos; resolución idéntica a la nota de F-005 D-ML) |
| Resumen de fallo de validación (use case) | **Español** | "El comando para guardar la vista no es válido." (espejo de Register) |
| `DuplicateName` / `NotFound` (repo) | **Español** | espejo de `TagRepository` ("Ya existe un tag con el nombre...") |
| `DomainInvariant` (use case Save) | **Inglés** | `ex.Message` del dominio (espejo `Tag`: "Name is required.") |
| `IncompatibleVersion` / `CorruptedFilter` (use case Apply) | **Inglés** | `ex.Message` del serializer (técnico, espejo del dominio) |
| Códigos de error (`Validation`, `DuplicateName`, `NotFound`, `DomainInvariant`, `IncompatibleVersion`, `CorruptedFilter`) | **Inglés** | convención del codebase / plan §8 |

> Si el usuario prefiere inglés para los mensajes nuevos, se corrige en
> revisión (mismo escape hatch que F-005 D-ML). Ver R-7.

### 3.3 Otras decisiones de forma

- **Validación sync** `_validator.Validate(command)` — NO `ValidateAsync`
  (F-005 D-2; los validators usan reglas sync para evitar
  `AsyncValidatorInvokedSynchronouslyException`).
- **Fallo de validación con `details` por campo** (diccionario
  `PropertyName → string[]`), NO un `string.Join` plano (F-005 D-3,
  espejo `RegisterIncomeUseCase` líneas 62-72).
- **`ArgumentException` alrededor de `SavedView.Create`** →
  `Result.Failure("DomainInvariant", ex.Message)` (F-005 D-4; defense in
  depth: el validator atrapa primero los casos de input).
- **Árbol de tests**: `Tests.Unit/Application/<Aggregate>/<UseCase>/` —
  **sin segmento `UseCases`** (F-005 C-3, locked; verificado en el árbol
  existente). Los 3 archivos de use cases viven en
  `Tests.Unit/Application/SavedViews/{SaveCurrentFilter,ApplySavedView,GetSavedViews}/`.
- **`FilterJsonSerializerTests`** vive en
  `Tests.Unit/Application/Common/` (espejo del namespace fuente
  `Application/Common`; los tests Phase-1 de `Result`/`PagedResult`
  quedan en `Tests.Unit/Common/` — no se mueven, fuera de alcance).
- **Doc-comments** siguen el idioma del archivo espejo: español en
  `SavedView.cs`/`SavedViewConfiguration.cs`/`SavedViewRepository.cs`/
  `FilterJsonSerializer.cs`/`ISavedViewRepository.cs` (espejos
  `Tag`/`TagConfiguration`/`TagRepository`/`Result.cs`) e inglés en los
  3 orquestadores y sus command/validator/queries (espejo familia
  `RegisterIncome`/`UpdateIncome`, F-003/F-005 P4).
- **Ningún paquete NuGet nuevo**, ningún `.csproj` modificado. CPM
  intacto.

---

## 4. Criterios de aceptación

La feature se considera completada cuando **todos** los ACs siguientes
se cumplen, verificables mediante tests automatizados y revisión de
código. Cada AC incluye escenarios Given/When/Then.

### AC-1 — `SavedView` entidad (sealed class, promovida in-place) (D-1, D-2, D-3)

- [ ] `src/ControlDeFinanzasIA.Domain/Views/SavedView.cs` (REPLACE):
      el record placeholder de 13 líneas se reemplaza por completo por:

```csharp
using ControlDeFinanzasIA.Domain.Enums;

namespace ControlDeFinanzasIA.Domain.Views;

/// <summary>
/// Vista guardada: un filtro de transacciones serializado como JSON
/// versionado que el usuario puede nombrar y recuperar. Entidad con
/// identidad (<see cref="Id"/>), igualdad por referencia e invariantes
/// blindadas en <see cref="Create"/> y <see cref="Update"/>.
/// </summary>
public sealed class SavedView
{
    // 7 props con private setters + defaults, XML doc por prop (espejo Tag):
    // Id (Guid), Name (string), EntityType (TransactionType),
    // FilterJson (string), SchemaVersion (int), CreatedAt (DateTime),
    // UpdatedAt (DateTime).

    private SavedView() { }  // EF Core only

    private SavedView(
        Guid id, string name, TransactionType entityType,
        string filterJson, int schemaVersion,
        DateTime createdAt, DateTime updatedAt) { /* asignaciones */ }

    public static SavedView Create(
        string name, TransactionType entityType,
        string filterJson, DateTime createdAt)
    {
        // ArgumentNullException: name/filterJson null
        // ValidateAndNormalize: Name trim + no vacío + ≤ 100;
        //   FilterJson trim + no vacío
        // EntityType: Enum.IsDefined(typeof(TransactionType), ...) si no → ArgumentException
        // createdAt == default → ArgumentException
        // SchemaVersion = 1; UpdatedAt = createdAt; Id = Guid.NewGuid()
    }

    public void Update(string name, string filterJson, DateTime updatedAt)
    {
        // ArgumentNullException: name/filterJson null
        // ValidateAndNormalize (idem)
        // updatedAt == default → ArgumentException
        // asigna Name, FilterJson, UpdatedAt — NO Id/EntityType/SchemaVersion/CreatedAt
    }

    private static (string Name, string FilterJson) ValidateAndNormalize(
        string name, string filterJson) { /* trim + guards, espejo Tag */ }
}
```

- [ ] Invariantes (lanzan `ArgumentNullException`/`ArgumentException`
      con mensajes en inglés, espejo `Tag`):
      INV-1 `Name` no null, no vacío/whitespace post-trim, ≤ 100;
      INV-2 `FilterJson` no null, no vacío post-trim;
      INV-3 `EntityType` definido en el enum;
      INV-4 `CreatedAt`/`UpdatedAt` ≠ `default`;
      INV-5 `SchemaVersion` ≥ 1 (estructural: estampado = 1 por
      `Create`, no suministrable por el caller).
- [ ] XML doc summary en la clase + cada propiedad + cada método
      (español, espejo `Tag.cs`).
- [ ] `using ControlDeFinanzasIA.Domain.Enums;` al tope (referencia
      intra-Domain, permitida por R-1).

- **GIVEN** `SavedView.Create("Transporte", TransactionType.Expense, filterJson, ts)`.
- **WHEN** se inspecciona la instancia.
- **THEN** `Id != Guid.Empty`, `Name == "Transporte"`, `EntityType ==
  Expense`, `FilterJson == filterJson`, `SchemaVersion == 1`,
  `CreatedAt == UpdatedAt == ts`.
- **GIVEN** `Create("   ", ...)` o `Create(new string('x', 101), ...)`.
- **WHEN** se llama.
- **THEN** lanza `ArgumentException` (INV-1).
- **GIVEN** vista creada y `view.Update("Nuevo nombre", nuevoJson, ts2)`.
- **WHEN** se llama.
- **THEN** `Name`/`FilterJson`/`UpdatedAt` cambian; `EntityType`,
  `SchemaVersion`, `Id`, `CreatedAt` permanecen.

### AC-2 — `SavedViewConfiguration` (NEW) (D-2, D-4)

- [ ] `src/.../Infrastructure/Persistence/Configurations/SavedViewConfiguration.cs`
      (NEW): `public sealed class SavedViewConfiguration :
      IEntityTypeConfiguration<SavedView>` (espejo `TagConfiguration`,
      29 líneas):

```csharp
b.ToTable("SavedViews");
b.HasKey(s => s.Id);
b.Property(s => s.Id).ValueGeneratedNever();
b.Property(s => s.Name).IsRequired().HasMaxLength(100);
b.HasIndex(s => s.Name).IsUnique();                       // D-4 — constraint único
b.Property(s => s.EntityType).IsRequired().HasConversion<int>(); // enum → int
b.HasIndex(s => s.EntityType);                            // no único — alimenta GetByEntityTypeAsync
b.Property(s => s.FilterJson).IsRequired().HasColumnType("TEXT"); // JSON largo, sin max
b.Property(s => s.SchemaVersion).IsRequired().HasDefaultValue(1);
b.Property(s => s.CreatedAt).IsRequired();
b.Property(s => s.UpdatedAt).IsRequired();
```

- [ ] XML doc summary en la clase + `Configure` (español, espejo
      `TagConfiguration`).
- [ ] `using ControlDeFinanzasIA.Domain.Views;` +
      `Microsoft.EntityFrameworkCore` + `.Metadata.Builders` (espejo).

- **GIVEN** el modelo EF construido con esta configuración.
- **WHEN** EF genera la migración (AC-5).
- **THEN** `SavedViews` existe con PK `Id` (client-generated), `Name`
  único (max 100), `EntityType` int + índice, `FilterJson` TEXT
  requerido, `SchemaVersion` int default 1, timestamps requeridos.

### AC-3 — `ISavedViewRepository` extendido (D-11)

- [ ] `src/.../Application/Abstractions/ISavedViewRepository.cs`
      (modify): AÑADIR al final, con XML doc en español (espejo del doc
      de `GetAllAsync`):

```csharp
/// <summary>
/// Obtiene todos los <see cref="SavedView"/> del tipo de entidad dado,
/// ordenados por nombre. La colección retornada es de solo lectura.
/// </summary>
/// <param name="entityType">Tipo de transacción (gastos o ingresos).</param>
/// <param name="ct">Token de cancelación opcional.</param>
Task<IReadOnlyList<SavedView>> GetByEntityTypeAsync(
    TransactionType entityType, CancellationToken ct = default);
```

- [ ] `using ControlDeFinanzasIA.Domain.Enums;` añadido al tope.
- [ ] Las 5 firmas existentes y sus doc-comments quedan **sin cambios**
      (compatibilidad del contrato Phase 1).

- **GIVEN** el interface extendido.
- **WHEN** `dotnet build`.
- **THEN** compila sin warnings; ningún implementador existente rompe
  (el único consumidor del tipo hoy es el propio placeholder — blast
  radius verificado).

### AC-4 — `SavedViewRepository` sealed class (NEW, 6 métodos) (D-4, D-8, D-11)

- [ ] `src/.../Infrastructure/Persistence/Repositories/SavedViewRepository.cs`
      (NEW): `public sealed class SavedViewRepository :
      ISavedViewRepository`, ctor `SavedViewRepository(AppDbContext db)`
      (guard `ArgumentNullException.ThrowIfNull(db)` no requerido —
      espejo `TagRepository` no lo tiene), **espejo línea a línea de
      `TagRepository.cs` (183 líneas)** adaptando `Tag`→`SavedView`:
- [ ] `AddAsync(SavedView, ct)`: `ArgumentNullException.ThrowIfNull` +
      **pre-check** `await _db.SavedViews.AnyAsync(s => s.Name ==
      savedView.Name, ct)` → `Result.Failure("DuplicateName", "Ya existe
      una vista guardada con el nombre '{savedView.Name}'.")` + try
      `Add`/`SaveChangesAsync` → `Result.Success(null!)` + catch
      `DbUpdateException` when `IsUniqueViolation(ex)` → idem
      `DuplicateName` (mitiga race sobre el índice único).
- [ ] `UpdateAsync(SavedView, ct)`: `FindAsync`; null →
      `Result.Failure("NotFound", "Vista guardada {savedView.Id} no
      encontrada.")`; `existing.Update(savedView.Name,
      savedView.FilterJson, savedView.UpdatedAt)`; `SaveChangesAsync` →
      `Success(null!)`.
- [ ] `RemoveAsync(Guid, ct)`: guard `Guid.Empty` → `NotFound`;
      `FindAsync`; null → `NotFound`; `Remove` + `SaveChangesAsync` →
      `Success(null!)`. (Operación disponible para 4-008 — D-8; no hay
      use case en este feature.)
- [ ] `GetByIdAsync(Guid, ct)`: guard `Guid.Empty` → `null`;
      `FindAsync`.
- [ ] `GetAllAsync(ct)`: `AsNoTracking().OrderBy(s =>
      s.Name).ToListAsync(ct)` (orden alfabético — alimenta el combobox
      de 4-008).
- [ ] `GetByEntityTypeAsync(TransactionType, ct)`:
      `AsNoTracking().Where(s => s.EntityType == entityType).OrderBy(s
      => s.Name).ToListAsync(ct)`.
- [ ] Private `static bool IsUniqueViolation(DbUpdateException ex)`
      (espejo: `SqliteException.SqliteErrorCode == 19`).
- [ ] XML doc en la clase + cada método público (español, espejo
      `TagRepository`).

- **GIVEN** una vista con nombre único.
- **WHEN** `AddAsync(view)`.
- **THEN** `IsSuccess` y `GetByIdAsync(view.Id)` la devuelve con
  `FilterJson` verbatim.
- **GIVEN** vista "X" persistida.
- **WHEN** `AddAsync` de otra vista "X" (match exacto, D-4).
- **THEN** `Failure("DuplicateName")` — sin excepción.
- **GIVEN** 2 vistas `Expense` + 1 `Income` persistidas.
- **WHEN** `GetByEntityTypeAsync(TransactionType.Expense)`.
- **THEN** exactamente las 2 `Expense`, ordenadas por `Name`.

### AC-5 — Migración `AddSavedViewsTable` (NEW, vía `dotnet ef`)

- [ ] Comando ejecutado sin error: `dotnet ef migrations add
      AddSavedViewsTable --project
      src/ControlDeFinanzasIA.Infrastructure --startup-project
      src/ControlDeFinanzasIA.UI` (requiere `dotnet-ef` 8.0.11 local).
- [ ] 3 archivos commiteados:
      `<YYYYMMDDhhmmss>_AddSavedViewsTable.cs`,
      `<YYYYMMDDhhmmss>_AddSavedViewsTable.Designer.cs`,
      `AppDbContextModelSnapshot.cs` (regenerado por la herramienta).
- [ ] `Up` (forma esperada — tipos SQLite verificados en
      `20260827223645_AddTagsAndExpenseTags`):

```csharp
migrationBuilder.CreateTable(
    name: "SavedViews",
    columns: table => new
    {
        Id = table.Column<Guid>(type: "TEXT", nullable: false),
        Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
        EntityType = table.Column<int>(type: "INTEGER", nullable: false),
        FilterJson = table.Column<string>(type: "TEXT", nullable: false),
        SchemaVersion = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
        CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
        UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
    },
    constraints: table => table.PrimaryKey("PK_SavedViews", x => x.Id));

migrationBuilder.CreateIndex(
    name: "IX_SavedViews_Name",
    table: "SavedViews",
    column: "Name",
    unique: true);

migrationBuilder.CreateIndex(
    name: "IX_SavedViews_EntityType",
    table: "SavedViews",
    column: "EntityType");
```

- [ ] **NO `InsertData`** (las vistas son dato de usuario, no del
      sistema — sin seed).
- [ ] `Down`: `migrationBuilder.DropTable(name: "SavedViews");`.
- [ ] File-scoped namespace (corregir manualmente si el generador emite
      block-scoped — precedente F-001 P4 §17 C-5 / F-004 AC-8 / F-007
      AC-3).
- [ ] No toca tablas ni migraciones previas (aditiva pura).

- **GIVEN** una BD en el estado post-migración-6.
- **WHEN** `MigrateAsync()` (fixture de integration tests).
- **THEN** `SavedViews` existe con PK, unique index `Name`, index
  `EntityType`, default 1 en `SchemaVersion`; migraciones 1-6 no se
  re-ejecutan ni alteran.

### AC-6 — `AppDbContext.SavedViews` (modify)

- [ ] `src/.../Infrastructure/Persistence/AppDbContext.cs`: AÑADIR
      después de `public DbSet<Tag> Tags => Set<Tag>();` (línea 68):

```csharp
/// <summary>
/// Conjunto de entidades <see cref="SavedView" /> mapeado a la tabla
/// <c>SavedViews</c> en SQLite.
/// </summary>
public DbSet<SavedView> SavedViews => Set<SavedView>();
```

- [ ] `using ControlDeFinanzasIA.Domain.Views;` añadido (tras
      `Domain.Transactions`, orden alfabético).
- [ ] Summary de la clase actualizado: menciona `SavedView` en la lista
      de conjuntos expuestos (línea 10-11).
- [ ] `OnModelCreating` **sin cambios** (la config se descubre vía
      `ApplyConfigurationsFromAssembly`).

- **GIVEN** el `DbSet` añadido.
- **WHEN** `dotnet ef migrations add AddSavedViewsTable`.
- **THEN** la migración detecta la entidad + su configuración (AC-2) y
  genera el DDL de AC-5.

### AC-7 — DI de `SavedViewRepository` en `AddPersistence` (modify)

- [ ] `src/.../Infrastructure/Persistence/ServiceCollectionExtensions.cs`:
      después de `services.AddScoped<ITagRepository, TagRepository>();`
      (línea 74), AÑADIR:

```csharp
services.AddScoped<ISavedViewRepository, SavedViewRepository>();
```

- [ ] Los `using` ya presentes cubren ambos tipos (verificado:
      `Application.Abstractions` línea 1 + `Persistence.Repositories`
      línea 2) — **cero usings nuevos**.
- [ ] Remarks de la clase: AÑADIR un `<para>` — "Según la especificación
      de F-006 de la Fase 4, este método también registra
      `<see cref="ISavedViewRepository"/>`." (espejo de los párrafos
      F-002/F-004 existentes, líneas 31-38).
- [ ] Summary de `AddPersistence` actualizado: incluye
      `ISavedViewRepository` en la lista (líneas 43-47).

- **GIVEN** `Bootstrap.ConfigureServices` invoca `AddPersistence`.
- **WHEN** se resuelve `ISavedViewRepository` desde un scope.
- **THEN** devuelve una instancia `SavedViewRepository` sobre el
  `AppDbContext` scoped (misma lifetime que los otros 5 repos).

### AC-8 — `FilterJsonSerializer` (NEW) (D-5, D-6, D-9, D-10)

- [ ] `src/.../Application/Common/FilterJsonSerializer.cs` (NEW):
      file-scoped namespace `ControlDeFinanzasIA.Application.Common`;
      `public static class FilterJsonSerializer`; XML doc (español,
      espejo `Result.cs`) en la clase + cada miembro público.
- [ ] Constante pública: `public const int CurrentSchemaVersion = 1;`.
- [ ] Opciones STJ compartidas (privadas, estáticas):
      `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`,
      `WriteIndented = false`.
- [ ] `public static string Serialize<T>(T filter) where T : class` —
      envuelve en envelope `{ "schemaVersion":
      CurrentSchemaVersion, "type": typeof(T).Name, "data": <filter> }`
      y serializa el envelope completo. El `type` es el nombre del tipo
      CLR (`"ExpenseFilter"` / `"IncomeFilter"` — el caller pasa el
      filter concreto; ver AC-9 dispatch).
- [ ] `public static T Deserialize<T>(string filterJson) where T :
      class` — valida el envelope, aplica la tabla de reglas (abajo) y
      deserializa `data` al tipo `T` pedido.
- [ ] `public static object Deserialize(string filterJson, Type
      targetType)` — overload **no-genérico** para callers que resuelven
      el tipo target en runtime (polymorphic dispatch); mismas reglas,
      chequeo de `type` contra `targetType.Name`; retorna `object`.
- [ ] Envelope interno: `private sealed record Envelope(int
      SchemaVersion, string Type, JsonElement Data);` (modelo del
      envelope; el parseo tolerante legacy ocurre en
      `TryParseEnvelope`, D-10).
- [ ] Guard de input: `Deserialize*` con `filterJson` null →
      `ArgumentNullException` (un `SavedView` materializado nunca tiene
      `FilterJson` vacío por INV-2 — el guard es defense in depth).
- [ ] **Tabla de reglas al deserializar** (comportamiento observable,
      blindado por JS-1..JS-8):

| Condición del envelope | Comportamiento |
| --- | --- |
| JSON malformado (no parsea) | `JsonException` |
| `schemaVersion` **ausente** (legacy) | se interpreta como **1** (D-10, en `TryParseEnvelope`) y continúa |
| `schemaVersion` explícito `< 1` | `JsonException` (malformado — no se camufla de legacy) |
| `schemaVersion` `> CurrentSchemaVersion` | `InvalidOperationException($"Schema version {envelope.SchemaVersion} is not supported. Current version: {CurrentSchemaVersion}.")` (D-9) |
| `schemaVersion` entre 1 y current (inclusive) | acepta (forward-compat) |
| `type` ≠ nombre del tipo target (`typeof(T).Name` / `targetType.Name`) | `JsonException` (payload inconsistente con el target) |
| `data` ausente o no es objeto JSON | `JsonException` |

- **GIVEN** `var json = FilterJsonSerializer.Serialize(new ExpenseFilter { CategoryId = g, TagIds = new[] { t1, t2 } });`.
- **WHEN** `FilterJsonSerializer.Deserialize<ExpenseFilter>(json)`.
- **THEN** el filter devuelto es igual al original en sus 6 props
  (incl. `TagIds` con 2 elementos y `null`s preservados); y
  `Serialize(Deserialize(Serialize(f)))` produce **bytes idénticos**
  (round-trip estable).
- **GIVEN** `{"schemaVersion":2,"type":"ExpenseFilter","data":{...}}` (fabricado).
- **WHEN** `Deserialize<ExpenseFilter>`.
- **THEN** lanza `InvalidOperationException` con el mensaje exacto de
  D-9.
- **GIVEN** `{"type":"ExpenseFilter","data":{...}}` (legacy, sin `schemaVersion`).
- **WHEN** `Deserialize<ExpenseFilter>`.
- **THEN** deserializa como versión 1 (sin error).

### AC-9 — `SaveCurrentFilterCommand` + `SaveCurrentFilterValidator` + `SaveCurrentFilterUseCase` (NEW ×3) (D-2, D-3, D-6, D-12)

- [ ] `src/.../Application/UseCases/SavedViews/SaveCurrentFilter/SaveCurrentFilterCommand.cs`:

```csharp
using ControlDeFinanzasIA.Domain.Enums;

namespace ControlDeFinanzasIA.Application.UseCases.SavedViews.SaveCurrentFilter;

/// <summary>
/// Immutable command to save the current filter as a named saved view.
/// The audit timestamps are sourced from <c>IClock</c> by the use case,
/// never supplied by the caller.
/// </summary>
public sealed record SaveCurrentFilterCommand(
    string Name,
    TransactionType EntityType,
    object Filter);
```

- [ ] `.../SaveCurrentFilter/SaveCurrentFilterValidator.cs`:
      `public sealed class SaveCurrentFilterValidator :
      AbstractValidator<SaveCurrentFilterCommand>` con **exactamente 2
      reglas sync** (estructura locked por contrato; mensajes en
      español per §3.2):

```csharp
RuleFor(c => c.Name)
    .NotEmpty().WithMessage("El nombre de la vista es obligatorio.")
    .MaximumLength(100).WithMessage("El nombre no puede superar 100 caracteres.")
    .Must(n => n.Trim().Length > 0).WithMessage("El nombre no puede contener solo espacios en blanco.");

RuleFor(c => c.Filter)
    .NotNull().WithMessage("El filtro es obligatorio.");
```

- [ ] `.../SaveCurrentFilter/SaveCurrentFilterUseCase.cs`:
      `public sealed class SaveCurrentFilterUseCase` con 4 deps
      (`ISavedViewRepository`, `IClock`,
      `IValidator<SaveCurrentFilterCommand>`,
      `ILogger<SaveCurrentFilterUseCase>`) y
      `public async Task<Result> ExecuteAsync(SaveCurrentFilterCommand
      command, CancellationToken ct = default)`. Flow (en orden):
  1. `ArgumentNullException.ThrowIfNull(command)`.
  2. `ValidationResult validation = _validator.Validate(command);`
     (sync, §3.3) — inválido → `Result.Failure("Validation", "El
     comando para guardar la vista no es válido.", details)` (details
     por campo, F-005 D-3).
  3. **Dispatch por runtime type de `command.Filter`** (D-12): patrón
     `command.Filter switch` — `ExpenseFilter` → exige
     `command.EntityType == TransactionType.Expense` y
     `filterJson = FilterJsonSerializer.Serialize(expenseFilter)`;
     `IncomeFilter` → exige `TransactionType.Income` y
     `Serialize(incomeFilter)`; otro tipo runtime →
     `Result.Failure("Validation", "Unsupported filter type:
     {typeName}.")`; pairing EntityType⟺Filter inconsistente →
     `Result.Failure("Validation", "El tipo del filtro no coincide con
     el tipo de entidad de la vista.")`.
  4. `SavedView savedView;` — `try { savedView =
     SavedView.Create(command.Name.Trim(), command.EntityType,
     filterJson, _clock.UtcNow); } catch (ArgumentException ex) {
     _logger.LogWarning(ex, "..."); return
     Result.Failure("DomainInvariant", ex.Message); }`
     (defense in depth — el validator atrapa primero los casos de
     input; F-005 D-4).
  5. `Result addResult = await _savedViewRepository.AddAsync(savedView,
     ct);` — si falla, **propagar tal cual** (expone `"DuplicateName"`).
  6. `return Result.Success(savedView);` — el caller recibe la entidad
     (con su `Id`) en `Value`.
- [ ] XML doc (inglés, espejo `RegisterIncomeUseCase` — incluye remarks
      del cast de `Result.Value` a `SavedView`).

- **GIVEN** command válido (`Name = "Transporte"`, `EntityType = Expense`, `Filter = new ExpenseFilter { TagIds = [...] }`).
- **WHEN** `ExecuteAsync(command)`.
- **THEN** `IsSuccess`; `Value` es `SavedView` con `Name == "Transporte"`,
  `EntityType == Expense`, `SchemaVersion == 1 ==
  FilterJsonSerializer.CurrentSchemaVersion`, `FilterJson` con envelope
  `{schemaVersion:1, type:"ExpenseFilter", data:{...}}`,
  `CreatedAt == UpdatedAt == _clock.UtcNow`; y el repo recibió exactamente
  1 `AddAsync`.
- **GIVEN** `Name = "   "` (o `""`, o `"\t"`, o > 100 chars) o `Filter = null`.
- **WHEN** `ExecuteAsync`.
- **THEN** `Failure("Validation")` con `Details` conteniendo `Name`
  (o `Filter`); el repo **no** recibió `AddAsync`.
- **GIVEN** repo configurado con `AddResultOverride = Result.Failure("DuplicateName", ...)`.
- **WHEN** `ExecuteAsync` con nombre válido.
- **THEN** el failure del repo se propaga con código `"DuplicateName"`.

### AC-10 — `ApplySavedViewQuery` + `ApplySavedViewResult` + `ApplySavedViewUseCase` (NEW ×3) (D-9, D-12)

- [ ] `src/.../Application/UseCases/SavedViews/ApplySavedView/ApplySavedViewQuery.cs`:

```csharp
public sealed record ApplySavedViewQuery(Guid SavedViewId);
```

  (XML doc: identificador de la vista a aplicar.)
- [ ] `.../ApplySavedView/ApplySavedViewResult.cs`:

```csharp
using ControlDeFinanzasIA.Domain.Enums;

/// <summary>
/// Result of applying a saved view: the stored entity type plus the
/// deserialized, typed filter (an <c>ExpenseFilter</c> or
/// <c>IncomeFilter</c> instance boxed as <c>object</c>).
/// </summary>
public sealed record ApplySavedViewResult(
    TransactionType EntityType,
    object Filter);
```

- [ ] `.../ApplySavedView/ApplySavedViewUseCase.cs`: `public sealed
      class ApplySavedViewUseCase` con 2 deps
      (`ISavedViewRepository`, `ILogger<ApplySavedViewUseCase>`) y
      `public async Task<Result> ExecuteAsync(ApplySavedViewQuery query,
      CancellationToken ct = default)`. Flow:
  1. `ArgumentNullException.ThrowIfNull(query)`.
  2. `SavedView? savedView = await
     _savedViewRepository.GetByIdAsync(query.SavedViewId, ct);` — null
     (incl. `Guid.Empty`, cubierto por el guard del repo) →
     `Result.Failure("NotFound", "Vista guardada {query.SavedViewId}
     no encontrada.")`.
  3. **Switch sobre `savedView.EntityType`** (almacenado — D-12,
     self-describing): `TransactionType.Expense` →
     `FilterJsonSerializer.Deserialize<ExpenseFilter>(savedView.FilterJson)`
     → `Result.Success(new ApplySavedViewResult(TransactionType.Expense,
     filter))`; `TransactionType.Income` → idem con `IncomeFilter`.
     Default defensivo (F-005 D-8) → `Result.Failure("CorruptedFilter",
     "Unknown saved view entity type: {savedView.EntityType}.")`.
  4. La deserialización va envuelta en try/catch: `catch
     (InvalidOperationException ex)` → `Result.Failure("IncompatibleVersion",
     ex.Message)` (D-9); `catch (JsonException ex)` →
     `Result.Failure("CorruptedFilter", ex.Message)`.
- [ ] XML doc (inglés, espejo `GetTransactionsQuery`/`RegisterIncome`
      — remarks del cast de `Value` a `ApplySavedViewResult`).

- **GIVEN** vista `Expense` persistida con `FilterJson` de un `ExpenseFilter` con `TagIds`.
- **WHEN** `ExecuteAsync(new ApplySavedViewQuery(view.Id))`.
- **THEN** `IsSuccess`; `Value` es `ApplySavedViewResult` con
  `EntityType == Expense` y `Filter` es `ExpenseFilter` igual al
  original (round-trip).
- **GIVEN** vista inexistente.
- **WHEN** `ExecuteAsync(new ApplySavedViewQuery(Guid.NewGuid()))`.
- **THEN** `Failure("NotFound")`.
- **GIVEN** vista cuyo `FilterJson` trae `"schemaVersion": 2` (fabricado).
- **WHEN** `ExecuteAsync`.
- **THEN** `Failure("IncompatibleVersion")` — nunca success silencioso.

### AC-11 — `GetSavedViewsQuery` + `GetSavedViewsUseCase` (NEW ×2) (D-11)

- [ ] `src/.../Application/UseCases/SavedViews/GetSavedViews/GetSavedViewsQuery.cs`:

```csharp
using ControlDeFinanzasIA.Domain.Enums;

/// <summary>
/// Request to list saved views. When <see cref="EntityType"/> is null,
/// all views are returned; otherwise only views of that entity type.
/// </summary>
public sealed record GetSavedViewsQuery(TransactionType? EntityType = null);
```

- [ ] `.../GetSavedViews/GetSavedViewsUseCase.cs`: `public sealed class
      GetSavedViewsUseCase` con **1 dep** (`ISavedViewRepository`) y
      `public async Task<Result> ExecuteAsync(GetSavedViewsQuery query,
      CancellationToken ct = default)`:

```csharp
ArgumentNullException.ThrowIfNull(query);

IReadOnlyList<SavedView> views = query.EntityType is { } et
    ? await _savedViewRepository.GetByEntityTypeAsync(et, ct)
    : await _savedViewRepository.GetAllAsync(ct);

return Result.Success(views);
```

  (Sin validator ni logger — query de lectura sin reglas; `Value` es la
  lista completa, el caller castea a `IReadOnlyList<SavedView>`.)
- [ ] XML doc (inglés) en ambos.

- **GIVEN** 2 vistas `Expense` + 1 `Income` en el repo.
- **WHEN** `ExecuteAsync(new GetSavedViewsQuery(TransactionType.Expense))`.
- **THEN** `IsSuccess`; `Value` (cast `IReadOnlyList<SavedView>`) tiene
  exactamente 2 elementos, todos `EntityType == Expense`.
- **WHEN** `ExecuteAsync(new GetSavedViewsQuery())`.
- **THEN** `Value` tiene las 3 vistas.
- **GIVEN** repo vacío.
- **WHEN** `ExecuteAsync(new GetSavedViewsQuery())`.
- **THEN** `IsSuccess`; `Value` es una lista **vacía** (no error, no
  null).

### AC-12 — DI de use cases + validator en `Bootstrap` (modify)

- [ ] `src/.../UI/Composition/Bootstrap.cs`: AÑADIR 3 usings (orden
      alfabético con los existentes de
      `Application.UseCases.*`, líneas 3-8):

```csharp
using ControlDeFinanzasIA.Application.UseCases.SavedViews.ApplySavedView;
using ControlDeFinanzasIA.Application.UseCases.SavedViews.GetSavedViews;
using ControlDeFinanzasIA.Application.UseCases.SavedViews.SaveCurrentFilter;
```

- [ ] AÑADIR 4 registros después de la línea 75
      (`AddTransient<IValidator<UpdateIncomeCommand>, ...>`):

```csharp
services.AddTransient<SaveCurrentFilterUseCase>();
services.AddTransient<ApplySavedViewUseCase>();
services.AddTransient<GetSavedViewsUseCase>();
services.AddTransient<IValidator<SaveCurrentFilterCommand>, SaveCurrentFilterValidator>();
```

- [ ] Remarks del método `ConfigureServices` actualizados: menciona los
      3 orquestadores de vistas guardadas + su validator (espejo del
      párrafo existente líneas 45-50).
- [ ] `AddPersistence`/`ApplyMigrations` **sin cambios** (el registro
      Scoped del repo es AC-7, en Infrastructure).

- **GIVEN** el composition root completo.
- **WHEN** el proveedor resuelve `SaveCurrentFilterUseCase`.
- **THEN** sus 4 deps se resuelven (`ISavedViewRepository` Scoped vía
  `AddPersistence`, `IClock` singleton `SystemClock`, validator
  Transient, `ILogger<>`).

### AC-13 — `FakeSavedViewRepository` (NEW test double)

- [ ] `tests/.../Tests.Unit/TestDoubles/FakeSavedViewRepository.cs`
      (NEW): `public sealed class FakeSavedViewRepository :
      ISavedViewRepository`, hand-rolled (sin mocking library), espejo
      del patrón de `FakeIncomeRepository` (97 líneas) con las 5
      propiedades públicas:

```csharp
public sealed class FakeSavedViewRepository : ISavedViewRepository
{
    private readonly List<SavedView> _addedSavedViews = new();

    /// <summary> Result returned by AddAsync when non-null. Default: success. </summary>
    public Result? AddResultOverride { get; set; }
    /// <summary> Result returned by UpdateAsync when non-null. </summary>
    public Result? UpdateResultOverride { get; set; }
    /// <summary> Result returned by RemoveAsync when non-null. </summary>
    public Result? RemoveResultOverride { get; set; }
    /// <summary> SavedView returned by GetByIdAsync when non-null. </summary>
    public SavedView? GetByIdResultOverride { get; set; }
    /// <summary> SavedViews passed to AddAsync (call capture). </summary>
    public IReadOnlyList<SavedView> AddedSavedViews => _addedSavedViews;

    // AddAsync: captura en _addedSavedViews + retorna AddResultOverride ?? Result.Success(null!)
    // UpdateAsync: override ?? in-memory replace por Id + success
    // RemoveAsync: override ?? in-memory remove por Id + success
    // GetByIdAsync: override ?? búsqueda por Id en _addedSavedViews
    // GetAllAsync: retorna snapshot de _addedSavedViews
    // GetByEntityTypeAsync: filtra _addedSavedViews por EntityType
}
```

- [ ] Los 6 métodos implementados (interface completa de AC-3);
      `/// <inheritdoc />` como los otros fakes.
- [ ] `GetAllAsync`/`GetByEntityTypeAsync` leen de la lista capturada
      (los tests de `GetSavedViewsUseCase` siembran vía `AddAsync`).

- **GIVEN** el fake recién construido.
- **WHEN** se inspecciona `AddedSavedViews` sin llamadas.
- **THEN** está vacío y `AddResultOverride == null` (default success).

### AC-14 — Unit tests (4 archivos, pure xUnit `Assert.*`)

- [ ] `tests/.../Tests.Unit/Application/Common/FilterJsonSerializerTests.cs`
      (NEW) — **≥6 requeridos** (ver §6.1, JS-1..JS-8):
      `Serialize_Then_Deserialize_Round_Trip_PreservesAllProps`
      (ExpenseFilter con TagIds),
      `Serialize_Then_Deserialize_RoundTrip_PreservesIncomeFilter`,
      `Deserialize_WithUnknownSchemaVersion_ThrowsInvalidOperationException`,
      `Deserialize_WithMissingSchemaVersion_DefaultsTo1`,
      `Deserialize_WithMalformedJson_ThrowsJsonException`,
      `Serialize_GeneratesCorrectEnvelopeShape` (+ type-mismatch,
      non-generic parity — ver tabla §6.1).
- [ ] `tests/.../Tests.Unit/Application/SavedViews/SaveCurrentFilter/SaveCurrentFilterUseCaseTests.cs`
      (NEW) — **≥4 requeridos** (ver §6.2, SC-1..SC-9):
      `ExecuteAsync_WithValidCommand_Persists`,
      `ExecuteAsync_WithEmptyName_ReturnsValidationFailure`,
      `ExecuteAsync_WithDuplicateName_ReturnsDuplicateName` (vía
      `AddResultOverride`),
      `ExecuteAsync_AssignsSchemaVersionFromSerializer` (+ Income happy
      path, >100 chars, null filter, unsupported type, pairing mismatch).
- [ ] `tests/.../Tests.Unit/Application/SavedViews/ApplySavedView/ApplySavedViewUseCaseTests.cs`
      (NEW) — **≥3 requeridos** (ver §6.3, AS-1..AS-7):
      `ExecuteAsync_WithExpenseSavedView_ReturnsExpenseFilter`,
      `ExecuteAsync_WithIncomeSavedView_ReturnsIncomeFilter`,
      `ExecuteAsync_WithNonExistentId_ReturnsNotFound` (+ future
      version, corrupted, legacy, empty Guid).
- [ ] `tests/.../Tests.Unit/Application/SavedViews/GetSavedViews/GetSavedViewsUseCaseTests.cs`
      (NEW) — **≥3 requeridos** (ver §6.4, GS-1..GS-4):
      `ExecuteAsync_WithEntityTypeFilter_ReturnsMatching`,
      `ExecuteAsync_WithoutEntityType_ReturnsAll`,
      `ExecuteAsync_WithNoSavedViews_ReturnsEmpty`.
- [ ] Estilo espejo `RegisterIncomeUseCaseTests`: ctor del test con
      `FakeClock` (timestamp fijo), `FakeSavedViewRepository`, validator
      **real**, `NullLogger<T>.Instance`; helpers privados
      `CreateValidCommand`/`CreateSavedView`; aserciones puras
      `Assert.True/False/Equal/NotNull/Null/Single/Contains/Throws/IsType`.
      Sin FluentAssertions, sin mocking.

- **GIVEN** los 4 archivos implementados según §6.1-§6.4.
- **WHEN** `dotnet test -c Debug --no-build --nologo --filter "FullyQualifiedName~FilterJsonSerializer|FullyQualifiedName~SaveCurrentFilterUseCase|FullyQualifiedName~ApplySavedViewUseCase|FullyQualifiedName~GetSavedViewsUseCase"`.
- **THEN** todos pasan (≈24-29 unit tests nuevos).

### AC-15 — Integration tests (NEW)

- [ ] `tests/.../Tests.Integration/Persistence/SavedViewRepositoryTests.cs`
      (NEW): namespace
      `ControlDeFinanzasIA.Tests.Integration.Persistence`, fixture
      verificado por test (`new TestAppDbContextFactory()` +
      `CreateContext()` + `MigrateAsync()` — espejo
      `TagRepositoryTests`), helper estático `CreateSavedView(string
      name, TransactionType entityType, string? filterJson = null)` →
      `SavedView.Create(name, entityType, filterJson ?? <envelope
      válido>, _fixedTimestamp)` con `_fixedTimestamp = new(2026, 1, 1,
      0, 0, 0, DateTimeKind.Utc)`; **≥6 requeridos** (los 7 de la tabla
      §6.5, IR-1..IR-7): `AddAsync_WithUniqueName_Persists`,
      `AddAsync_WithDuplicateName_ReturnsDuplicateName`,
      `UpdateAsync_Existing_UpdatesFields`,
      `RemoveAsync_Existing_DeletesSavedView`,
      `GetByIdAsync_Existing_ReturnsSavedView`,
      `GetAllAsync_ReturnsAllOrderedByName`,
      `GetByEntityTypeAsync_ReturnsMatchingType` (siembra 2 Expense + 1
      Income → query Expense → exactamente 2) — + recomendados IR-8..
      IR-12 (NotFound paths, guard `Guid.Empty`, JSON round-trip **a
      través de la BD**).
- [ ] Pure xUnit `Assert.*`; sin FluentAssertions; sin mocking.

- **GIVEN** los tests implementados según §6.5.
- **WHEN** `dotnet test -c Debug --no-build --nologo --filter "FullyQualifiedName~SavedViewRepository"`.
- **THEN** todos pasan (7-12 integration tests nuevos; la migración se
  aplica sola vía `MigrateAsync`).

### AC-16 — Gates de verificación + scope creep

- [ ] `dotnet build -c Release --nologo` → 0 warnings, 0 errors.
- [ ] `dotnet build -c Debug --nologo` → 0 warnings, 0 errors.
- [ ] `dotnet test -c Debug --no-build --nologo --filter
      "FullyQualifiedName~SavedViewRepository|FullyQualifiedName~FilterJsonSerializer|FullyQualifiedName~SaveCurrentFilterUseCase|FullyQualifiedName~ApplySavedViewUseCase|FullyQualifiedName~GetSavedViewsUseCase"`
      → todos pasan.
- [ ] `dotnet test -c Debug --no-build --nologo --filter
      "FullyQualifiedName~ArchitectureTests"` → 11/11 (R-1..R-5).
- [ ] `dotnet test -c Debug --no-build --nologo` (suite completa) →
      verde + **únicamente** el 1 fallo pre-existing de `Money`
      (documentado; no regresión). Línea base 262 unit + 93 integration
      + ~35-40 nuevos.
- [ ] `dotnet format style --verify-no-changes` → exit 0.
- [ ] `dotnet list src/ControlDeFinanzasIA.Domain package` sigue vacío
      (R-1).
- [ ] **Sin scope creep**: cero modificaciones a `ExpenseFilter.cs`,
      `IncomeFilter.cs`, `Expense.cs`, `Income.cs`, `Tag.cs`,
      `TransactionType.cs`, `IExpenseRepository`/`IIncomeRepository`/
      `ITagRepository` + implementaciones, `Money`/`Currency`/`Result`/
      `PagedResult`/`IClock`, use cases existentes (`Register*`/
      `Update*`/`Delete*`/`GetTransactions`), Views/ViewModels/XAML
      (excepto `Bootstrap.cs`), `SeedData`, migraciones previas,
      fakes existentes, `TestAppDbContextFactory`.
- [ ] **NO `DeleteSavedViewUseCase`** (D-8 — diferido a 4-008).
- [ ] NO FluentAssertions, NO mocking library, NO paquetes NuGet
      nuevos, NO `.csproj` modificados.

- **GIVEN** el diff completo de F-006.
- **WHEN** se inspecciona contra §5 (Áreas afectadas).
- **THEN** no hay archivos fuera de la lista; los únicos archivos
  pre-existentes editados son los 5 sancionados + el snapshot
  regenerado.

---

## 5. Áreas afectadas

Base: `C:\Users\diego\Desktop\ControlDeFinanzasIA\`

| # | Archivo | Acción | AC | Descripción |
| --- | --- | --- | --- | --- |
| 1 | `src\ControlDeFinanzasIA.Domain\Views\SavedView.cs` | **Modify (replace)** | AC-1 | Record placeholder 13 líneas → sealed class completa (7 props + 2 ctors privados + `Create`/`Update` + `ValidateAndNormalize` + invariantes) |
| 2 | `src\ControlDeFinanzasIA.Application\Abstractions\ISavedViewRepository.cs` | **Modify** | AC-3 | + `GetByEntityTypeAsync` + `using Domain.Enums`; 5 firmas intactas |
| 3 | `src\...Infrastructure\Persistence\Configurations\SavedViewConfiguration.cs` | **Create** | AC-2 | Tabla `SavedViews`, PK, unique index `Name`, index `EntityType`, enum→int |
| 4 | `src\...Infrastructure\Persistence\Repositories\SavedViewRepository.cs` | **Create** | AC-4 | 6 métodos, espejo `TagRepository` |
| 5 | `src\...Infrastructure\Persistence\Migrations\<ts>_AddSavedViewsTable.cs` | **Create (generado)** | AC-5 | `Up`: tabla + PK + 2 índices; `Down`: drop; sin `InsertData` |
| 6 | `src\...Infrastructure\Persistence\Migrations\<ts>_AddSavedViewsTable.Designer.cs` | **Create (generado)** | AC-5 | Metadata de la migración |
| 7 | `src\...Infrastructure\Persistence\Migrations\AppDbContextModelSnapshot.cs` | **Edit (generado)** | AC-5 | + entidad `SavedView` |
| 8 | `src\...Infrastructure\Persistence\AppDbContext.cs` | **Modify** | AC-6 | + `DbSet<SavedView> SavedViews` + `using Domain.Views` + doc |
| 9 | `src\...Infrastructure\Persistence\ServiceCollectionExtensions.cs` | **Modify** | AC-7 | + `AddScoped<ISavedViewRepository, SavedViewRepository>()` + doc |
| 10 | `src\ControlDeFinanzasIA.Application\Common\FilterJsonSerializer.cs` | **Create** | AC-8 | Envelope versionado + reglas forward/backward + overload no-genérico |
| 11 | `src\...Application\UseCases\SavedViews\SaveCurrentFilter\SaveCurrentFilterCommand.cs` | **Create** | AC-9 | Record `(Name, EntityType, Filter: object)` |
| 12 | `src\...Application\UseCases\SavedViews\SaveCurrentFilter\SaveCurrentFilterValidator.cs` | **Create** | AC-9 | 2 reglas sync |
| 13 | `src\...Application\UseCases\SavedViews\SaveCurrentFilter\SaveCurrentFilterUseCase.cs` | **Create** | AC-9 | 4 deps; valida → despacha → serializa → crea → persiste |
| 14 | `src\...Application\UseCases\SavedViews\ApplySavedView\ApplySavedViewQuery.cs` | **Create** | AC-10 | Record `(Guid SavedViewId)` |
| 15 | `src\...Application\UseCases\SavedViews\ApplySavedView\ApplySavedViewResult.cs` | **Create** | AC-10 | Record `(TransactionType EntityType, object Filter)` |
| 16 | `src\...Application\UseCases\SavedViews\ApplySavedView\ApplySavedViewUseCase.cs` | **Create** | AC-10 | 2 deps; fetch → switch → deserializa → mapea excepciones |
| 17 | `src\...Application\UseCases\SavedViews\GetSavedViews\GetSavedViewsQuery.cs` | **Create** | AC-11 | Record `(TransactionType? EntityType = null)` |
| 18 | `src\...Application\UseCases\SavedViews\GetSavedViews\GetSavedViewsUseCase.cs` | **Create** | AC-11 | 1 dep; branch `is { } et` |
| 19 | `src\ControlDeFinanzasIA.UI\Composition\Bootstrap.cs` | **Modify** | AC-12 | + 3 usings + 4 Transient + doc |
| 20 | `tests\ControlDeFinanzasIA.Tests.Unit\TestDoubles\FakeSavedViewRepository.cs` | **Create** | AC-13 | Fake con 5 overrides/captura + 6 métodos |
| 21 | `tests\...Tests.Unit\Application\Common\FilterJsonSerializerTests.cs` | **Create** | AC-14 | JS-1..JS-8 (§6.1) |
| 22 | `tests\...Tests.Unit\Application\SavedViews\SaveCurrentFilter\SaveCurrentFilterUseCaseTests.cs` | **Create** | AC-14 | SC-1..SC-9 (§6.2) |
| 23 | `tests\...Tests.Unit\Application\SavedViews\ApplySavedView\ApplySavedViewUseCaseTests.cs` | **Create** | AC-14 | AS-1..AS-7 (§6.3) |
| 24 | `tests\...Tests.Unit\Application\SavedViews\GetSavedViews\GetSavedViewsUseCaseTests.cs` | **Create** | AC-14 | GS-1..GS-4 (§6.4) |
| 25 | `tests\ControlDeFinanzasIA.Tests.Integration\Persistence\SavedViewRepositoryTests.cs` | **Create** | AC-15 | IR-1..IR-12 (§6.5) |

**Archivos NO modificados (verificados presentes/correctos):**
`ExpenseFilter.cs`, `IncomeFilter.cs` (se serializan tal cual — cero
cambios), `Expense.cs`, `Income.cs`, `Tag.cs`, `TagConfiguration.cs`,
`TagRepository.cs`, `TransactionType.cs`, `Money.cs`, `Currency.cs`,
`Result.cs`, `Error.cs`, `PagedResult.cs`, `IClock.cs`,
`IExpenseRepository.cs`, `IIncomeRepository.cs`, `ITagRepository.cs`,
`ExpenseRepository.cs`, `IncomeRepository.cs`, use cases existentes
(`Register*`/`Update*`/`Delete*`/`GetTransactions`) y sus
commands/validators, Views/ViewModels/XAML (excepto `Bootstrap.cs`),
`SeedData.cs`, migraciones previas (1-6), fakes existentes
(`FakeExpenseRepository`/`FakeIncomeRepository`/`FakeClock`/...),
`TestAppDbContextFactory.cs`, tests existentes.

**Modificaciones a `.csproj`:** ninguna (sin paquetes nuevos — STJ es
BCL).

**Pronóstico de entrega (guard de review, §E de `sdd-phase-common`):**
~25 archivos, ~1,100-1,400 líneas cambiadas → **excede el presupuesto
de 400 líneas**. `sdd-tasks` DEBE pronosticar **chained PRs
(stacked-to-main)**: **PR-1 = Dominio + Infrastructure** (AC-1..AC-7 +
IR tests: entidad, interface, config, repo, migración, DbSet, DI
Scoped — verificable y reversible por sí solo); **PR-2 = Application +
DI de Bootstrap + tests unit** (AC-8..AC-14 + AC-16: serializer, use
cases, fake, DI Transient). Líneas de guard requeridas en tasks:
`Decision needed before apply: Yes|No`, `Chained PRs recommended:
Yes`, `400-line budget risk: High`.

---

## 6. Plan de tests

Marco y paquetes: xUnit + `Microsoft.Data.Sqlite` + EF Core SQLite (ya
en los proyectos de tests desde Fase 2). **Sin paquetes nuevos; sin
FluentAssertions; sin mocking.** Aserciones con `Assert.*`
(`Assert.True`, `Assert.False`, `Assert.Equal`, `Assert.Null`,
`Assert.NotNull`, `Assert.Single`, `Assert.Contains`, `Assert.All`,
`Assert.Throws`, `Assert.IsType`).

Fixture verificado (NO el shorthand inexistente `CreateAsync()` —
ver F-002 P4 §6 / F-007 §6):

```csharp
using var factory = new TestAppDbContextFactory();
using AppDbContext context = factory.CreateContext();
await context.Database.MigrateAsync();
```

Timestamp determinista: `_fixedTimestamp = new(2026, 1, 1, 0, 0, 0,
DateTimeKind.Utc)`; `FakeClock { UtcNow = new(2026, 1, 15, 10, 0, 0,
DateTimeKind.Utc) }`; `NullLogger<T>.Instance` para todos los loggers.

### 6.1 Unit — `FilterJsonSerializerTests` (JS-1..JS-8)

Namespace `ControlDeFinanzasIA.Tests.Unit.Application.Common`. JSON
legacy y de versión futura **hard-coded** (strings literales — no
fabricados vía el serializer).

| # | Test | GIVEN / WHEN / THEN |
| --- | --- | --- |
| JS-1 | `Serialize_Then_Deserialize_Round_Trip_PreservesAllProps` | **GIVEN** `ExpenseFilter { Page=2, PageSize=25, CategoryId=g1, DateFrom=2026-01-01, DateTo=2026-01-31, TagIds=[t1,t2] }`. **WHEN** `Serialize` → `Deserialize<ExpenseFilter>`. **THEN** las 6 props iguales (`Assert.Equal` por prop; `TagIds` con 2 elementos en orden); **AND** `Serialize(Deserialize(Serialize(f)))` produce el **mismo string** (bytes idénticos). |
| JS-2 | `Serialize_Then_Deserialize_RoundTrip_PreservesIncomeFilter` | Ídem JS-1 con `IncomeFilter` (simetría; `null`s explícitos: `CategoryId = null` persiste como `"categoryId": null`). |
| JS-3 | `Deserialize_WithUnknownSchemaVersion_ThrowsInvalidOperationException` | **GIVEN** `{"schemaVersion":2,"type":"ExpenseFilter","data":{...}}` hard-coded. **WHEN** `Deserialize<ExpenseFilter>`. **THEN** `Assert.Throws<InvalidOperationException>` — el mensaje contiene "Schema version 2 is not supported. Current version: 1." (D-9 exacto). |
| JS-4 | `Deserialize_WithMissingSchemaVersion_DefaultsTo1` | **GIVEN** `{"type":"ExpenseFilter","data":{...}}` (legacy, sin `schemaVersion`) hard-coded. **WHEN** `Deserialize<ExpenseFilter>`. **THEN** deserializa sin error (interpretado como v1, D-10). |
| JS-5 | `Deserialize_WithMalformedJson_ThrowsJsonException` | **GIVEN** `"not json {{"` / `"{\"schemaVersion\":1}"` (sin `type` ni `data`). **WHEN** `Deserialize<ExpenseFilter>` (Theory con ambos). **THEN** `Assert.Throws<JsonException>`. |
| JS-6 | `Serialize_GeneratesCorrectEnvelopeShape` | **GIVEN** `Serialize(new ExpenseFilter())`. **WHEN** se parsea el string con `JsonDocument`. **THEN** la raíz tiene exactamente las keys `schemaVersion` (= 1), `type` (= `"ExpenseFilter"`) y `data` (objeto); naming camelCase (`page`, `pageSize`, `categoryId`, `dateFrom`, `dateTo`, `tagIds` dentro de `data`); sin indentación (sin `\n` en el string... o `WriteIndented=false` verificado por ausencia de espacios). |
| JS-7 | `Deserialize_WithTypeMismatch_ThrowsJsonException` | **GIVEN** envelope con `"type":"IncomeFilter"` (data de `IncomeFilter`). **WHEN** `Deserialize<ExpenseFilter>`. **THEN** `Assert.Throws<JsonException>` (payload inconsistente con el target — regla de AC-8). |
| JS-8 | `Deserialize_NonGeneric_MatchesGenericOutcome` | **GIVEN** `json = Serialize(new ExpenseFilter {...})`. **WHEN** `Deserialize<ExpenseFilter>(json)` vs `Deserialize(json, typeof(ExpenseFilter))`. **THEN** resultados `Assert.Equal` (el overload no-genérico es equivalente). |

### 6.2 Unit — `SaveCurrentFilterUseCaseTests` (SC-1..SC-9)

Namespace `ControlDeFinanzasIA.Tests.Unit.Application.SavedViews.SaveCurrentFilter`.
Ctor: `FakeClock` + `FakeSavedViewRepository` + `SaveCurrentFilterValidator`
real + `NullLogger<SaveCurrentFilterUseCase>.Instance`. Helper
`CreateValidCommand(TransactionType entityType)` con
`ExpenseFilter { TagIds = [...] }`.

| # | Test | GIVEN / WHEN / THEN |
| --- | --- | --- |
| SC-1 | `ExecuteAsync_WithValidCommand_Persists` | **GIVEN** command válido (Expense). **WHEN** `ExecuteAsync`. **THEN** `IsSuccess`; `Assert.IsType<SavedView>(result.Value)`; `AddedSavedViews` tiene 1 elemento con `Name` trimado, `EntityType == Expense`, `CreatedAt == UpdatedAt == _clock.UtcNow`; el `FilterJson` parsea como envelope con `type == "ExpenseFilter"`. |
| SC-2 | `ExecuteAsync_WithEmptyName_ReturnsValidationFailure` | **WHEN** Theory `Name ∈ {"", " ", "\t"}`. **THEN** `Failure("Validation")` con `Details` conteniendo `"Name"`; `Assert.Empty(AddedSavedViews)`. |
| SC-3 | `ExecuteAsync_WithDuplicateName_ReturnsDuplicateName` | **GIVEN** `AddResultOverride = Result.Failure("DuplicateName", "...")`. **WHEN** `ExecuteAsync` válido. **THEN** propaga `Failure` con `Error.Code == "DuplicateName"`. |
| SC-4 | `ExecuteAsync_AssignsSchemaVersionFromSerializer` | **GIVEN** command válido. **WHEN** `ExecuteAsync`. **THEN** `persisted.SchemaVersion == FilterJsonSerializer.CurrentSchemaVersion` **AND** el `schemaVersion` del envelope dentro de `FilterJson` == `persisted.SchemaVersion` (columna ⟷ envelope siempre coinciden — tripwire D-3). |
| SC-5 | `ExecuteAsync_WithIncomeFilter_PersistsAsIncomeView` | **GIVEN** command `EntityType = Income` + `IncomeFilter`. **THEN** `IsSuccess`; `persisted.EntityType == Income`; envelope `type == "IncomeFilter"`. |
| SC-6 | `ExecuteAsync_WithNameOver100Chars_ReturnsValidationFailure` | **WHEN** `Name = new string('x', 101)`. **THEN** `Failure("Validation")` con details `Name`; repo vacío. |
| SC-7 | `ExecuteAsync_WithNullFilter_ReturnsValidationFailure` | **WHEN** `Filter = null!`. **THEN** `Failure("Validation")` con details `Filter`; repo vacío. |
| SC-8 | `ExecuteAsync_WithUnsupportedFilterType_ReturnsValidationFailure` | **GIVEN** `Filter = "not a filter"` (string). **THEN** `Failure("Validation")` (mensaje menciona el tipo); repo vacío. |
| SC-9 | `ExecuteAsync_WithEntityTypeFilterMismatch_ReturnsValidationFailure` | **GIVEN** `EntityType = TransactionType.Income` + `Filter` es `ExpenseFilter`. **THEN** `Failure("Validation")` (pairing D-12); repo vacío. |

### 6.3 Unit — `ApplySavedViewUseCaseTests` (AS-1..AS-7)

Namespace `ControlDeFinanzasIA.Tests.Unit.Application.SavedViews.ApplySavedView`.
Ctor: `FakeSavedViewRepository` + `NullLogger`. Helper `CreateSavedView(name, entityType)` →
`SavedView.Create(name, entityType, FilterJsonSerializer.Serialize(filter), _fixedTimestamp)`
+ `repo.AddAsync(view)` (el fake lo captura → `GetByIdAsync` lo
encuentra).

| # | Test | GIVEN / WHEN / THEN |
| --- | --- | --- |
| AS-1 | `ExecuteAsync_WithExpenseSavedView_ReturnsExpenseFilter` | **GIVEN** vista Expense con filter de 6 props. **WHEN** `ExecuteAsync(new ApplySavedViewQuery(view.Id))`. **THEN** `IsSuccess`; `ApplySavedViewResult` con `EntityType == Expense` y `Assert.IsType<ExpenseFilter>(result.Filter)` con las props iguales al original. |
| AS-2 | `ExecuteAsync_WithIncomeSavedView_ReturnsIncomeFilter` | Ídem con Income (simetría). |
| AS-3 | `ExecuteAsync_WithNonExistentId_ReturnsNotFound` | **WHEN** `Guid.NewGuid()` no sembrado. **THEN** `Failure("NotFound")`. |
| AS-4 | `ExecuteAsync_WithEmptyGuid_ReturnsNotFound` | **WHEN** `new ApplySavedViewQuery(Guid.Empty)`. **THEN** `Failure("NotFound")` (el guard del repo retorna null; sin excepción). |
| AS-5 | `ExecuteAsync_WithFutureSchemaVersion_ReturnsIncompatibleVersion` | **GIVEN** vista con `FilterJson` hard-coded `"schemaVersion": 2`. **THEN** `Failure("IncompatibleVersion")` con el mensaje del serializer (D-9). |
| AS-6 | `ExecuteAsync_WithCorruptedFilterJson_ReturnsCorruptedFilter` | **GIVEN** vista con `FilterJson = "not json"`. **THEN** `Failure("CorruptedFilter")` (JsonException mapeada — nunca excepción al caller). |
| AS-7 | `ExecuteAsync_WithLegacyJsonWithoutSchemaVersion_ReturnsFilter` | **GIVEN** vista con `FilterJson` legacy hard-coded (sin `schemaVersion`). **THEN** `IsSuccess` con el filter aplicado como v1 (D-10 end-to-end). |

### 6.4 Unit — `GetSavedViewsUseCaseTests` (GS-1..GS-4)

Namespace `ControlDeFinanzasIA.Tests.Unit.Application.SavedViews.GetSavedViews`.
Ctor: `FakeSavedViewRepository` solamente (1 dep). Siembra vía
`AddAsync` en el fake.

| # | Test | GIVEN / WHEN / THEN |
| --- | --- | --- |
| GS-1 | `ExecuteAsync_WithEntityTypeFilter_ReturnsMatching` | **GIVEN** fake con 2 vistas Expense + 1 Income (sembradas vía `AddAsync`). **WHEN** `ExecuteAsync(new GetSavedViewsQuery(TransactionType.Expense))`. **THEN** `IsSuccess`; `Assert.IsType<IReadOnlyList<SavedView>>` con 2 elementos, todos `EntityType == Expense`. |
| GS-2 | `ExecuteAsync_WithoutEntityType_ReturnsAll` | **WHEN** `ExecuteAsync(new GetSavedViewsQuery())`. **THEN** 3 elementos (default `null` = todas). |
| GS-3 | `ExecuteAsync_WithNoSavedViews_ReturnsEmpty` | **GIVEN** fake sin siembra. **THEN** `IsSuccess` + lista **vacía** (no null, no error). |
| GS-4 | `ExecuteAsync_ReturnsSavedViewsInValue` *(opcional)* | Afirma el cast `result.Value` → `IReadOnlyList<SavedView>` documentado en los remarks del use case (contrato del caller). |

### 6.5 Integration — `SavedViewRepositoryTests` (IR-1..IR-12)

Namespace `ControlDeFinanzasIA.Tests.Integration.Persistence`. Fixture
por test (§6 preamble); helper `CreateSavedView(name, entityType,
filterJson?)` con envelope por defecto
`FilterJsonSerializer.Serialize(new ExpenseFilter { ... })` (el
proyecto de Integration ya referencia Application — verificado en
`IncomeRepositoryTests` vía `Application.Common.Result`).

| # | Test | GIVEN / WHEN / THEN |
| --- | --- | --- |
| IR-1 | `AddAsync_WithUniqueName_Persists` | **GIVEN** `CreateSavedView("Transporte", Expense)`. **WHEN** `repo.AddAsync(view)`. **THEN** `IsSuccess`; `GetByIdAsync(view.Id)` no-null con `Name`/`EntityType`/`FilterJson` verbatim; `AddedSavedViews`-equivalente: `context.SavedViews` tiene 1 fila. |
| IR-2 | `AddAsync_WithDuplicateName_ReturnsDuplicateName` | **GIVEN** vista "Duplicate" persistida. **WHEN** `AddAsync(CreateSavedView("Duplicate", Income))` (mismo nombre exacto — D-4; distinto `EntityType`, el unique index es solo sobre `Name`). **THEN** `Failure("DuplicateName")` (pre-check; sin excepción). |
| IR-3 | `UpdateAsync_Existing_UpdatesFields` | **GIVEN** vista persistida. **WHEN** `view.Update("Renamed", nuevoJson, _fixedTimestamp2); repo.UpdateAsync(view)`. **THEN** `IsSuccess`; reload: `Name == "Renamed"`, `FilterJson == nuevoJson`, `UpdatedAt == _fixedTimestamp2`, **AND** `EntityType`/`SchemaVersion`/`CreatedAt` sin cambios (D-3). |
| IR-4 | `RemoveAsync_Existing_DeletesSavedView` | **GIVEN** vista persistida. **WHEN** `repo.RemoveAsync(view.Id)`. **THEN** `IsSuccess`; `GetByIdAsync(view.Id)` == null. |
| IR-5 | `GetByIdAsync_Existing_ReturnsSavedView` | **GIVEN** vista persistida. **WHEN** `GetByIdAsync(view.Id)`. **THEN** no-null; `Id`/`Name`/`FilterJson` iguales. |
| IR-6 | `GetAllAsync_ReturnsAllOrderedByName` | **GIVEN** vistas "Beta", "Alpha", "Gamma" persistidas. **WHEN** `GetAllAsync()`. **THEN** 3 elementos en orden `[Alpha, Beta, Gamma]` (orden alfabético). |
| IR-7 | `GetByEntityTypeAsync_ReturnsMatchingType` | **GIVEN** 2 vistas Expense + 1 Income persistidas. **WHEN** `GetByEntityTypeAsync(TransactionType.Expense)`. **THEN** exactamente 2, todas `EntityType == Expense`, ordenadas por `Name`. |
| IR-8 | `UpdateAsync_Missing_ReturnsNotFound` *(recomendado)* | Vista no persistida → `UpdateAsync` → `Failure("NotFound")`. |
| IR-9 | `RemoveAsync_NonExistent_ReturnsNotFound` *(recomendado)* | `RemoveAsync(Guid.NewGuid())` → `Failure("NotFound")`. |
| IR-10 | `RemoveAsync_WithEmptyGuid_ReturnsNotFound` / `GetByIdAsync_WithEmptyGuid_ReturnsNull` *(recomendado)* | Guards `Guid.Empty` espejo `TagRepository`. |
| IR-11 | `FilterJson_RoundTripsThroughDatabase` *(recomendado — success criterion del plan)* | **GIVEN** `original = ExpenseFilter { 6 props }`; `json = Serialize(original)`; vista persistida con ese `FilterJson`. **WHEN** `context.ChangeTracker.Clear()` (fuerza fetch real) → `GetByIdAsync(view.Id)`. **THEN** `persisted.FilterJson == json` **verbatim** (string exacto — la columna no muta el JSON) **AND** `FilterJsonSerializer.Deserialize<ExpenseFilter>(persisted.FilterJson)` == `original` (round-trip por la BD). |
| IR-12 | `GetByEntityTypeAsync_WithIncomeType_ReturnsOnlyIncome` *(recomendado)* | Simetría de IR-7 por el lado Income. |

### 6.6 Cobertura de invariantes de dominio (nota de diseño)

No existe un archivo dedicado `Domain/SavedViewTests.cs` en este
feature (exclusión deliberada de la ronda interactiva — §3.1, control
de presupuesto de PR). Las invariantes de `SavedView` quedan cubiertas
por capas: (a) el **validator** atrapa `Name` vacío/whitespace/>100 y
`Filter` null antes del factory (SC-2/SC-6/SC-7); (b) el **catch
`ArgumentException` → `"DomainInvariant"`** del use case documenta la
defensa del factory (AC-9 paso 4 — defense in depth, actualmente
inalcanzable por API pública, igual que la nota ET-5 de F-004 §6.2);
(c) los **integration tests** ejercitan `Create`/`Update` happy paths
(IR-1/IR-3) incluyendo inmutabilidad post-`Update` (D-3). Si la fase
verify encuentra huecos, un archivo de invariantes va a 4-008 o a un
feature de retrofit — **sdd-tasks/apply no deben añadirlo
silenciosamente** (disciplina de scope, AC-16).

**Cobertura global:** happy paths (JS-1/JS-2, SC-1/SC-5, AS-1/AS-2,
GS-1/GS-2, IR-1/IR-5/IR-11), edge cases (JS-4 legacy, JS-6 shape,
SC-4 versión, GS-3 vacío, IR-6 orden, IR-7 filtrado), error states
(JS-3/JS-5/JS-7, SC-2/SC-3/SC-6..SC-9, AS-3..AS-6, IR-2/IR-8..IR-10).
El versionado queda blindado por JS-3/JS-4 + AS-5/AS-7 (doble
cobertura serializer + use case); el pairing EntityType⟺Filter por
SC-8/SC-9.

---

## 7. Fuera de alcance

F-006 **no** hace lo siguiente (cada ítem vive en otra feature):

- **`DeleteSavedViewUseCase`** — diferido a 4-008 junto al botón
  "Eliminar vista" (RF-19; D-8). El repo `RemoveAsync` ya existe.
- **UI de vistas guardadas** (combobox, botones guardar/aplicar/
  eliminar, prompt de nombre), `GetIncomesQuery`, reset de `Page = 1`
  al aplicar — todo 4-008.
- **DTOs de serialización** (`ExpenseFilterDto`/`IncomeFilterDto`/
  `SavedViewPayloadV1` de RF-13) — supersedidos por D-5/D-6
  (serialización directa + envelope).
- **`Page`/`PageSize` stripping** — se persisten completos (D-7).
- **Encriptación o datos sensibles en `FilterJson`**; filtros
  cross-entity-type (una vista es siempre Expense **o** Income);
  soft-delete / `IsDeleted` (hard delete, eco de `Expense`/`Income`).
- **Bump de `CurrentSchemaVersion`** (v2) o migración de datos de
  vistas v1 → v2 — el diseño lo habilita (D-9/D-10) pero no lo
  ejecuta; `Update` no re-estampa `SchemaVersion` (D-3).
- **Modificaciones** a `ExpenseFilter`/`IncomeFilter` (se serializan,
  no se cambian), `Expense.cs`/`Income.cs`/`Tag.cs`/
  `TransactionType.cs` (semántica **y doc** — el doc-refresh del
  proposal D-2 NO está sancionado por el contrato de lanzamiento),
  `Money`/`Currency`/`Result`/`Error`/`PagedResult`/`IClock`,
  interfaces/repos existentes, use cases existentes, Views/ViewModels/
  XAML, `SeedData`, migraciones previas, fakes existentes,
  `TestAppDbContextFactory`.
- **Mover `ResultTests`/`PagedResultTests`** de `Tests.Unit/Common/` a
  `Tests.Unit/Application/Common/` (normalización del árbol — fuera de
  alcance; ver §3.3).
- **Paquetes NuGet nuevos** (en particular no FluentAssertions, no
  mocking library, no Newtonsoft.Json — STJ es BCL). **CI workflow**,
  **`Directory.Packages.props`**, **`Directory.Build.props`**: sin
  cambios.

---

## 8. Plan de rollback

Todo es **aditivo y reversible por archivo** (la única mutación de
schema es una tabla nueva sin datos pre-existentes). Revertir en orden:

1. **PR-2 primero (Application + DI Bootstrap):** eliminar
   `FilterJsonSerializer.cs`, la carpeta `UseCases/SavedViews/` (8
   archivos), `FakeSavedViewRepository.cs`, los 4 archivos de unit
   tests, y los 4 registros + 3 usings de `Bootstrap.cs`. Sin impacto
   en datos — los orquestadores no tienen estado.
2. **Revertir la migración** (si se aplicó a una BD de usuario):
   `dotnet ef database update 20260903233252_AddIncomeTagsJoinTable
   --project src/ControlDeFinanzasIA.Infrastructure --startup-project
   src/ControlDeFinanzasIA.UI`, luego `dotnet ef migrations remove
   AddSavedViewsTable` (elimina los 3 archivos generados). El `Down()`
   dropea `SavedViews` — **sin pérdida de datos**: no existe dato de
   usuario en esa tabla antes de este feature.
3. **PR-1 (Infrastructure + Dominio):** eliminar
   `SavedViewRepository.cs` + `SavedViewConfiguration.cs`; revertir
   `AppDbContext.cs` (quitar `DbSet` + using), `ServiceCollectionExtensions.cs`
   (quitar el Scoped + doc).
4. **Restaurar los 2 archivos del placeholder:** `SavedView.cs` al
   record de 13 líneas y `ISavedViewRepository.cs` a los 5 métodos
   (commit de origen de Phase 1 F-006 — el blast radius verificado
   garantiza que nada más los referencia).
5. **Eliminar** `SavedViewRepositoryTests.cs`.
6. **Verificar** `dotnet build` limpio y `dotnet test` suite baseline
   en verde (estado pre-F-006, incluido el fallo pre-existing de
   `Money`). NetArchTest R-1..R-5 no se afectan en ningún paso: los
   cambios tocan Domain (entidad), Application (serializer + use
   cases), Infrastructure (config/repo/migración) y UI (solo
   `Bootstrap` — composition root sancionado por R-4).

---

## 9. Riesgos y mitigaciones

| # | Riesgo | Prob. | Impacto | Mitigación |
| --- | --- | --- | --- | --- |
| R-1 | STJ: round-trip de `DateOnly`/`Guid`/`IReadOnlyList<Guid>` (`TagIds`) y binding de records con naming camelCase — primera serialización de filtros en el codebase. | Media | Alto | Envelope parseado tolerante (`TryParseEnvelope` — D-10) en vez de depender del binding del record envelope; tests de round-trip con las 6 props + `null`s (JS-1/JS-2) + byte-identity; fixture JSON legacy/futuro hard-coded (JS-3/JS-4) blindan la tabla de reglas de AC-8. |
| R-2 | Versionado JSON: un futuro bump v1 → v2 rompe vistas guardadas (risk table del plan §11). | Media | Alto | Envelope explícito (D-5) + columna `SchemaVersion` (query de mantenimiento sin parsear JSON, D-2); rechazo SOLO de versiones mayores (D-9, `IncompatibleVersion` — nunca silencioso); aceptación de menores/iguales + legacy default 1 (D-10); tripwire SC-4 fuerza la revisión del estampado del factory en un bump. |
| R-3 | Duplicado de `Name` en BD real de usuario (race entre pre-check e insert). | Baja | Medio | Pre-check `AnyAsync` + unique index `IX_SavedViews_Name` + catch `DbUpdateException` (SQLite 19) — espejo exacto de `TagRepository` (D-4/AC-4); IR-2 cubre el path determinista. |
| R-4 | El generador de EF emite namespace block-scoped en la migración → IDE0161 + `dotnet format style` falla. | Media | Bajo | AC-5 exige file-scoped namespace con corrección manual (precedente F-001 P4 §17 C-5, replicado en F-004 AC-8 y F-007 AC-3/R-5); gate format en AC-16. |
| R-5 | Hueco de type-safety de `Filter: object` en el command (mismatch `EntityType`⟺filter runtime type, o tipo no soportado) → vista corrupta persistida. | Media | Medio | Guard de pairing + dispatch por runtime type en el use case (D-12, AC-9 paso 3); SC-8/SC-9 blindan ambas ramas; el switch de Apply con default defensivo (F-005 D-8) + type check del serializer (JS-7) como segunda barrera. |
| R-6 | Exceso de líneas por PR (presupuesto 400). | Alta | Medio | Pronóstico §5: chained PRs stacked-to-main (PR-1 Dominio+Infrastructure / PR-2 Application+DI+tests) — decisión formal en `sdd-tasks` con líneas de guard obligatorias (§E de `sdd-phase-common`). |
| R-7 | Idioma de mensajes de validador: el contrato de lanzamiento bosquejó mensajes en inglés; la convención locked del repo (F-005 D-ML) es español. | Media | Bajo | §3.2 resuelve con el precedente F-005 (sketches ingleses = ilustrativos; estructura locked, mensajes espejo `RegisterIncomeValidator`); si el usuario prefiere inglés, se corrige en revisión — cambio cosmético, cero contrato. |
| R-8 | Engram `mem_save` no disponible en el toolset del sub-agente (verificado: sin funciones `mem_*` — mismo estado que F-002..F-007 P4). | Alta (verificado) | Bajo | Respaldo en filesystem: proposal en `.atl/sdd/f-006p4-saved-view-json/proposal.md` (pre-existente) y esta spec en su path canónico. El **orquestador** backfillará a Engram los topic keys `sdd/f-006p4-saved-view-json/{proposal,spec}` (precedente F-007 R-8). El sub-agente NO fabrica observation IDs. |

---

## 10. Referencias

- **Propuesta (filesystem):**
  `.atl/sdd/f-006p4-saved-view-json/proposal.md` (223 líneas) — PRD de
  origen. Q-1..Q-4 ratificadas en la ronda interactiva; sus decisiones
  D-1..D-12 fueron refinadas por la misma ronda — supersesiones
  documentadas en §3.1 de esta spec.
- **Propuesta (Engram objetivo):** topic key
  `sdd/f-006p4-saved-view-json/proposal` (pendiente de persistencia por
  el orquestador — ver R-8).
- **Spec (Engram objetivo):** topic key `sdd/f-006p4-saved-view-json/spec`
  (pendiente de persistencia por el orquestador — ver R-8).
- **F-007 Fase 4 (archivada — estructura espejo + filtros):**
  `spec/features/phase-04/F-007-extend-filters-tagids.done.md` (1,091
  líneas) — estructura de 10 secciones replicada; `ExpenseFilter`/
  `IncomeFilter` 6 props con `TagIds` que este feature serializa.
- **F-005 Fase 4 (archivada — patrón use cases):**
  `spec/features/phase-04/F-005-update-delete-use-cases.done.md` (1,339
  líneas) — D-2 validación sync, D-3 details por campo, D-4
  `ArgumentException` → `"DomainInvariant"`, D-8 switch con default
  defensivo, C-3 convención de árbol de tests, D-ML idioma de mensajes.
- **F-004 Fase 4 (archivada — patrón `Tag`):**
  `spec/features/phase-04/F-004-tag-entity-and-many-to-many.done.md` —
  promoción record placeholder → sealed class, `TagRepository`
  (pre-check + race), `TagConfiguration` (unique index).
- **Phase plan:** `spec/phases/phase-04/features.md` — §4.1 líneas
  49-51 (alcance), §RF-12 (SavedView entity), §RF-13 (serialización
  versionada — supersedida por D-5/D-6), §RF-14 (repo +
  `GetByEntityTypeAsync`), §RF-15 (use cases), §RF-20 línea 251, §6
  CU-6/CU-7, §8 reglas de negocio (IncompatibleVersion, vista global,
  una vista = Expense o Income), §9 tabla 4-006, §11 risk "Versionado
  de JSON de SavedView".
- **Contrato de interfaces:** `spec/features/phase-01/F-006-contrato-interfaces-repo.done.md`
  (commands `Task<Result>` vs queries; el `ISavedViewRepository` de 5
  métodos que esta spec extiende).
- **Tipos comunes:** `spec/features/phase-01/F-005-comuntypes-result-pagedresult.done.md`
  (`Result` non-generic con `Value: object?`).
- **Test infra:** `spec/features/phase-02/F-006-integration-tests.done.md`
  (`TestAppDbContextFactory`, SQLite in-memory + `MigrateAsync` por
  test).
- **Arquitectura:** `docs/architecture.md` §4 (R-1..R-5), §7 (use
  cases), §8 (Fluent API + migraciones versionadas), §12 (single-user
  local — justifica sin encriptación de `FilterJson`).
- **Constitución:** `spec/constitution/mission.md`,
  `spec/constitution/tech-stack.md` (async/await extremo a extremo,
  errores como valores, "no silent failures", MVVM estricto).
- **Código verificado (fuentes de esta spec):**
  `src/.../Domain/Views/SavedView.cs` (13 líneas — placeholder con
  `Id`/`Name`/`FilterJson` dictionary),
  `src/.../Abstractions/ISavedViewRepository.cs` (57 líneas — 5
  métodos),
  `src/.../Abstractions/ExpenseFilter.cs` (50 líneas) +
  `IncomeFilter.cs` (54 líneas) — 6 props `init` con `TagIds`,
  `src/.../Domain/Enums/TransactionType.cs` (14 líneas),
  `src/.../Domain/Catalog/Tag.cs` (142 líneas — plantilla entidad),
  `src/.../Repositories/TagRepository.cs` (183 líneas — plantilla
  repo), `src/.../Configurations/TagConfiguration.cs` (29 líneas —
  plantilla config), `src/.../Configurations/ExpenseConfiguration.cs`
  (79 líneas — verificado para estilo de config; sin M:N en este
  feature), `src/.../AppDbContext.cs` (80 líneas — 5 DbSets),
  `src/.../ServiceCollectionExtensions.cs` (78 líneas —
  `AddPersistence` Scoped, línea 74 = `ITagRepository`),
  `src/.../UI/Composition/Bootstrap.cs` (132 líneas — Transient
  líneas 66-75),
  `src/.../Application/Common/Result.cs` (47 líneas — non-generic),
  `src/.../UseCases/Incomes/RegisterIncome/{RegisterIncomeUseCase,
  RegisterIncomeCommand, RegisterIncomeValidator}.cs` (109/17/45 —
  plantillas use case/validator),
  `src/.../UseCases/Expenses/GetTransactions/GetTransactionsQuery.cs`
  (87 líneas — plantilla query),
  `src/.../Migrations/20260827223645_AddTagsAndExpenseTags.cs` (70
  líneas — tipos de columna SQLite: Guid/string/DateTime → TEXT) y
  `20260903233252_AddIncomeTagsJoinTable.cs` (última migración),
  `tests/.../TestDoubles/FakeIncomeRepository.cs` (97 líneas —
  plantilla fake) + `FakeClock.cs` (21 líneas),
  `tests/.../Application/Incomes/RegisterIncome/RegisterIncomeUseCaseTests.cs`
  (239 líneas — plantilla unit tests),
  `tests/.../Persistence/TagRepositoryTests.cs` (204 líneas —
  plantilla integration) + `TestAppDbContextFactory.cs` (49 líneas),
  `Directory.Packages.props` (105 líneas — sin STJ; FluentValidation
  11.10.0; EF Core 8.0.11).
