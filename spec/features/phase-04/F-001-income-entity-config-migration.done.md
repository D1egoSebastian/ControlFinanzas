# Specification — F-001: Entidad `Income` + `IncomeConfiguration` + migración `AddIncomesTable`

> **Fase:** 4 — Transacciones: ingresos, edición, borrado, tags, filtros, vistas guardadas
> **Feature:** F-001 (local a esta fase)
> **Estado:** ✅ ARCHIVADA (cycle cerrado el 2026-08-25) — Phase 4 F-001 / 8
> **Spec ID:** SPEC-F001P4
>
> **Nota de numeración:** este `F-001` es el de la **Fase 4**
> (`spec/phases/phase-04/features.md`). No debe confundirse con el
> `F-001` de Fase 1 (esqueleto), Fase 2 (`AppDbContext`) ni Fase 3
> (entidad `Expense`). Los IDs son locales a cada fase; el folder
> los desambigua. Ver `spec/README.md`.
>
> Fuentes de verdad: `docs/vision.md`, `docs/architecture.md`,
> `docs/roadmap.md` (§Fase 4), `spec/constitution/tech-stack.md`,
> `spec/constitution/mission.md`,
> `spec/phases/phase-04/features.md` (RF-1, RF-2),
> `spec/features/phase-03/F-001-expense-domain-entity.done.md` y
> `spec/features/phase-03/F-002-expense-configuration-migration.done.md`
> (patrones archivados que esta feature espeja).

---

## 1. Resumen

F-001 introduce el **lado simétrico de los gastos**: la entidad
`Income` (ingreso) en el proyecto `ControlDeFinanzasIA.Domain`, su
mapeo EF Core `IncomeConfiguration`, la migración `AddIncomesTable` y
la batería de unit tests de invariantes. Replica los patrones
archivados de F-001/F-002 de Phase 3 (`Expense` entity +
`ExpenseConfiguration` + `AddExpenseTable`), con **dos divergencias
deliberadas y documentadas**: `CategoryId` es **nullable** (`Guid?`)
con `OnDelete(SetNull)` — ingresos sin categoría son válidos — y se
añade un campo nuevo `Source: string?` (max 50) que `Expense` no
tiene. Sin repositorio, sin use cases, sin UI: dominio + persistencia
del schema.

Al final de la feature:

- `src/ControlDeFinanzasIA.Domain/Transactions/Income.cs` se
  **reescribe in-place** (el placeholder vacío actual → entidad
  completa) como `public sealed class Income` con factory `Create`,
  método `Update`, dos constructores privados, 9 propiedades con
  `private set` y 8 invariantes blindadas.
- `src/ControlDeFinanzasIA.Infrastructure/Persistence/Configurations/IncomeConfiguration.cs`
  existe como `public sealed class IncomeConfiguration :
  IEntityTypeConfiguration<Income>` y se descubre automáticamente vía
  `ApplyConfigurationsFromAssembly`.
- `AppDbContext` expone un nuevo `DbSet<Income> Incomes => Set<Income>();`
  (expression-bodied, get-only — convención real del codebase).
- La migración `AddIncomesTable` está generada y commiteada (3
  archivos en `Migrations/`).
- `dotnet build` limpio; `dotnet test` 222 existentes + nuevos verdes
  (unit + schema); NetArchTest R-1..R-5 verdes; `dotnet format style`
  exit 0.

---

## 2. Contexto

Phase 3 cerró el slice vertical de `Expense` (registrar + listar +
paginar). Phase 4 abre **ingresos**: el lado contable simétrico. La
visión (`docs/vision.md` §4.2) define los movimientos como
`Expense` o `Income`; el roadmap (§Fase 4) descompone el backend de
Income en 4-001 (entidad + config + migración), 4-002 (repo), 4-003
(use case) y 4-008 (UI). F-001 es el **nodo inicial** del backend de
Income: sin la entidad blindada y su tabla, 4-002, 4-003 y 4-005 no
pueden consumir nada.

F-001 **reescribe** `Income.cs` que Phase 3 F-001 dejó como
placeholder vacío (`public sealed class Income { }`) precisamente para
que `IIncomeRepository` (Fase 1 F-006) siguiera compilando. Esta
feature completa ese placeholder. No crea carpeta nueva, no toca
`Expense`, no toca `Money`, no toca `IIncomeRepository`.

**Trazabilidad.** `vision.md` §4.2 (subconjunto mínimo de un
movimiento); `architecture.md` §6 (capa de dominio), §8 (mapeo Fluent
API en Infrastructure, migraciones versionadas, value objects a
columnas primitivas); `roadmap.md` §Fase 4 (entregable: entidad
`Income`).

---

## 3. Objetivo

1. **Reescribir `Income.cs` in-place** como `public sealed class
   Income` standalone (sin base `Entity<Guid>` — no existe en el
   codebase), con factory `Create`, método `Update`, propiedades con
   `private set`, y 8 invariantes validadas en construcción y
   actualización.
2. **Crear `IncomeConfiguration`** con mapeo Fluent API: tabla
   `Incomes`, `Id` PK `ValueGeneratedNever`, `CategoryId` **nullable**
   FK a `Categories.Id` con `OnDelete(SetNull)`, tipos/longitudes,
   índices en `Date` y `CategoryId`, y `OwnsOne(e => e.Amount)`
   espejo exacto de `ExpenseConfiguration`.
3. **Añadir `DbSet<Income> Incomes => Set<Income>();`** a
   `AppDbContext` (expression-bodied), tras `Expenses`. No tocar
   `OnModelCreating`.
4. **Generar la migración `AddIncomesTable`** aditiva (DDL puro, sin
   `InsertData`), commitear los 3 archivos.
5. **Cubrir la entidad con unit tests** (pure xUnit `Assert.*`, SIN
   FluentAssertions, SIN mocking).
6. **Mantener NetArchTest R-1..R-5 en verde.**

---

## 4. Alcance

### 4.1 En alcance

- Reescritura in-place de
  `src/ControlDeFinanzasIA.Domain/Transactions/Income.cs`.
- Creación de
  `src/ControlDeFinanzasIA.Infrastructure/Persistence/Configurations/IncomeConfiguration.cs`.
- Edición de `AppDbContext.cs` (+ `DbSet<Income> Incomes`).
- Generación y commiteo de la migración `AddIncomesTable` (3
  archivos).
- Creación de `tests/.../Tests.Unit/Domain/IncomeTests.cs` (≥8 tests).
- Creación de
  `tests/.../Tests.Integration/Persistence/IncomeSchemaTests.cs` (3
  tests de forma del schema, reutilizando `TestAppDbContextFactory`).

### 4.2 Fuera de alcance

- **`IncomeRepository` + `IncomeFilter`** (4-002).
- **`RegisterIncomeUseCase` + validator + command + `RegisterIncomeResult`**
  (4-003). El `FutureDateWarning` vive en el **use case 4-003**, NO en
  la entidad; la entidad admite fechas futuras (INV-3).
- **`UpdateIncomeUseCase` / `DeleteTransactionUseCase`** (4-005).
- **UI / ViewModels / `GetIncomesQuery`** (4-008).
- **Modificar `IIncomeRepository`** (ya existe de Phase 1 F-006 — se
  verifica presencia, no se toca; el namespace `Domain.Transactions`
  no cambia, así que sigue compilando).
- **`Source` enum** — decidido `string?`, no enum.
- **Seed data / `InsertData`** en la migración.
- **Modificar `Expense.cs`, `Money.cs`, `Currency.cs`, `Category.cs`,
  `ServiceCollectionExtensions.cs`** — ninguno se toca (4-001 no
  introduce repositorio; la config se descubre sola).

### 4.3 Cambios al alcance (aclaración)

- **4-001 reescribe el placeholder `Income.cs` in-place.** NO añade
  una carpeta `Domain/Incomes/`; `Income` sigue en
  `Domain/Transactions/` junto a `Expense` (convención por agregado
  del codebase, verificada).
- **No modifica F-003 ni ninguna spec de Phase 3.** No toca `Expense`,
  su config ni su migración.
- **No introduce `Entity<Guid>` ni base abstracta** (ver §17, Corrección C-1).

---

## 5. Casos de uso

### CU-1 — Registrar un ingreso válido (con categoría)

- **Actor:** use case `RegisterIncomeUseCase` (4-003, futuro).
- **Trigger:** el use case obtiene `createdAt` de `IClock.UtcNow` y
  llama `Income.Create(categoryId, date, amount, description, note,
  source, createdAt)`.
- **Postcondición:** instancia de `Income` con `Id` generado, todas
  las props con valores recibidos (descripción trimada, nota y source
  normalizados a `null` si blank), `UpdatedAt == CreatedAt`.

### CU-2 — Registrar un ingreso sin categoría

- **Trigger:** `Income.Create(null, date, amount, ...)` con
  `categoryId: null`.
- **Postcondición:** el factory acepta (ingresos sin categoría son
  válidos, INV-7). `income.CategoryId == null`. **Diverge de
  `Expense`** (que rechaza `Guid.Empty`).

### CU-3 — Rechazar monto cero o negativo

- **Trigger:** `Income.Create(..., Money.Create(0m, DOP), ...)`.
- **Postcondición:** `ArgumentException` (INV-2). No se crea instancia.

### CU-4 — Aceptar fecha futura (ingreso planeado)

- **Trigger:** `Income.Create(..., DateOnly(2099, 12, 31), ...)`.
- **Postcondición:** el factory acepta (INV-3 sólo rechaza `< 1900-01-01`).
  El warning de "fecha futura" lo emite el **use case 4-003**, no la
  entidad.

### CU-5 — Actualizar un ingreso existente

- **Trigger:** `income.Update(categoryId, date, amount, description,
  note, source, updatedAt)` con `updatedAt >= CreatedAt`.
- **Postcondición:** props mutables reasignadas; `Id`/`CreatedAt`
  inmutables; invariantes revalidadas.

### CU-6 — `ApplyConfigurationsFromAssembly` descubre `IncomeConfiguration`

- **Trigger:** se construye el `AppDbContext` (arranque o test).
- **Postcondición:** EF Core aplica `IncomeConfiguration` sin tocar
  `OnModelCreating`. La tabla se llama `Incomes`; `Id` PK
  `ValueGeneratedNever`; `Money` → `Amount` + `Currency`; FK a
  `Categories` con `SetNull`; índices en `Date` y `CategoryId`.

### CU-7 — Borrar una `Category` preserva los `Income` asociados

- **Trigger:** `RemoveAsync` de una `Category` que tiene `Incomes`
  con ese `CategoryId`.
- **Postcondición:** SQLite setea `CategoryId` a `null` en los
  `Incomes` afectados (vs `Expense` que bloquea con `Restrict`).
  **Divergencia deliberada** (DP-2).

---

## 6. Requisitos funcionales

### RF-1 — Tipo y ubicación

- `public sealed class Income` en
  `src/ControlDeFinanzasIA.Domain/Transactions/Income.cs` (mismo
  archivo del placeholder que reemplaza; reescritura in-place).
- Namespace `ControlDeFinanzasIA.Domain.Transactions` (sin cambios;
  `IIncomeRepository` de Fase 1 F-006 sigue compilando).
- **NO** hereda de ninguna base (`Entity<Guid>` no existe en el
  codebase — ver §17 Corrección C-1). **NO** es `record`. **NO**
  implementa interfaces de dominio.

### RF-2 — Propiedades públicas

La entidad expone **nueve** propiedades públicas con `private set`:

| Propiedad | Tipo | Nulabilidad | Invariante |
| --- | --- | --- | --- |
| `Id` | `Guid` | no nullable | generado en `Create` vía `Guid.NewGuid()` |
| `CategoryId` | `Guid?` | **nullable** | INV-7 (null válido) |
| `Date` | `DateOnly` | no nullable | INV-3 |
| `Amount` | `Money` | no nullable (guard defensivo) | INV-1, INV-2 |
| `Description` | `string` | no nullable | INV-4 |
| `Note` | `string?` | nullable | INV-5 |
| `Source` | `string?` | nullable | INV-6 |
| `CreatedAt` | `DateTime` | no nullable | INV-8 |
| `UpdatedAt` | `DateTime` | no nullable | INV-8 |

**No hay propiedad `Currency` separada** (la moneda se accede vía
`income.Amount.Currency`, eco de `Expense` §9.2).

### RF-3 — Factory estático `Create`

- Firma: `public static Income Create(Guid? categoryId, DateOnly date,
  Money amount, string description, string? note, string? source,
  DateTime createdAt)`.
- Genera `Id` internamente vía `Guid.NewGuid()` (eco de `Expense`;
  `IncomeConfiguration` fija `ValueGeneratedNever`).
- Valida INV-1..INV-7 antes de construir. Si alguna falla, lanza
  `ArgumentNullException` (INV-1) o `ArgumentException` (resto) con
  mensaje en inglés.
- Normaliza `Description` (trim), `Note` (trim + null canonization) y
  `Source` (trim + null canonization) — espejo del
  `ValidateAndNormalize` de `Expense`, extendido con `Source`.
- Setea `UpdatedAt = createdAt`.

### RF-4 — Método `Update`

- Firma: `public void Update(Guid? categoryId, DateOnly date, Money
  amount, string description, string? note, string? source, DateTime
  updatedAt)`.
- Reasigna las 6 props mutables (`CategoryId`, `Date`, `Amount`,
  `Description`, `Note`, `Source`, `UpdatedAt`) con las mismas
  normalizaciones que `Create`.
- Revalida INV-1..INV-7 y valida INV-8 (`updatedAt >= CreatedAt`).
- No modifica `Id` ni `CreatedAt` (inmutables).

### RF-5 — Constructores privados

- `private Income() { }` — para EF Core (materialización).
- `private Income(Guid id, Guid? categoryId, DateOnly date, Money
  amount, string description, string? note, string? source, DateTime
  createdAt, DateTime updatedAt)` — para el factory.
- No hay constructor público. Único camino público: `Income.Create(...)`.

### RF-6 — `IncomeConfiguration`

- `public sealed class IncomeConfiguration :
  IEntityTypeConfiguration<Income>` en
  `src/ControlDeFinanzasIA.Infrastructure/Persistence/Configurations/IncomeConfiguration.cs`.
- `b.ToTable("Incomes")` (PascalCase).
- `b.HasKey(e => e.Id)` + `b.Property(e => e.Id).ValueGeneratedNever()`.
- `b.Property(e => e.CategoryId)` — **nullable** (sin `IsRequired()`).
  FK: `b.HasOne<Category>().WithMany().HasForeignKey(e =>
  e.CategoryId).OnDelete(DeleteBehavior.SetNull)`. **Diverge de
  `Expense` (`Restrict`).**
- `b.Property(e => e.Date).IsRequired()`.
- `b.Property(e => e.Description).IsRequired().HasMaxLength(200)`.
- `b.Property(e => e.Note).HasMaxLength(500)` (nullable).
- `b.Property(e => e.Source).HasMaxLength(50)` (nullable).
- `b.Property(e => e.CreatedAt).IsRequired()` + `b.Property(e =>
  e.UpdatedAt).IsRequired()`.
- `b.HasIndex(e => e.Date)` + `b.HasIndex(e => e.CategoryId)`.
- `b.OwnsOne(e => e.Amount, money => { money.Property(m =>
  m.Amount).HasColumnName("Amount").IsRequired().HasPrecision(18, 4);
  money.Property(m => m.Currency).HasColumnName("Currency").IsRequired();
  })` — **espejo exacto de `ExpenseConfiguration`**.

### RF-7 — `DbSet<Income>` en `AppDbContext`

- Editar `AppDbContext.cs`: añadir `public DbSet<Income> Incomes =>
  Set<Income>();` (expression-bodied, get-only) **tras** `Expenses`.
- Añadir `using ControlDeFinanzasIA.Domain.Transactions;` si no está
  (ya está presente por `Expense`).
- No tocar `OnModelCreating` (la config se descubre vía
  `ApplyConfigurationsFromAssembly`).
- **No tocar `ServiceCollectionExtensions.AddPersistence`** (4-001 no
  registra repositorio; la config se descubre sola).

### RF-8 — Migración `AddIncomesTable`

- Comando: `dotnet ef migrations add AddIncomesTable --project
  src/ControlDeFinanzasIA.Infrastructure --startup-project
  src/ControlDeFinanzasIA.UI` (ver §17 Corrección C-5:
  `--startup-project` es `UI` per `AGENTS.md`, NO `Infrastructure`).
- 3 archivos en `Migrations/`: `<ts>_AddIncomesTable.cs`,
  `<ts>_AddIncomesTable.Designer.cs`, `AppDbContextModelSnapshot.cs`
  (actualizado). Timestamp `YYYYMMDDhhmmss` asignado por la herramienta.
- `Up`: `CreateTable` para `Incomes` (9 cols + cols `OwnsOne`),
  `CreateIndex` para `IX_Incomes_Date` + `IX_Incomes_CategoryId`,
  `AddForeignKey` con `SetNull`. **Sin** `InsertData`. No toca
  `Categories`/`PaymentMethods`/`Expenses` (aditiva).
- `Down`: `migrationBuilder.DropTable("Incomes")`.

---

## 7. Requisitos no funcionales

### RNF-1 — Pureza de Dominio (R-1)

`Income.cs` sólo depende de BCL y del propio dominio (`Money`,
`Currency`). Sin EF Core, sin Avalonia. Tras F-001, `dotnet list
src/ControlDeFinanzasIA.Domain package` sigue vacío. NetArchTest R-1
sigue pasando.

### RNF-2 — NetArchTest R-2..R-5

F-001 no toca `Application` ni `UI`; añade código a `Infrastructure`
que referencia `Domain.Transactions` (`Income`) y `Domain.Catalog`
(`Category` para `HasOne<T>`) — patrón ya permitido. R-2..R-5 siguen
pasando.

### RNF-3 — Determinismo

`Income.Create` es determinista salvo por `Guid.NewGuid()`. Los tests
verifican `income.Id != Guid.Empty` y leen el `Id` de la instancia
creada cuando lo necesitan.

### RNF-4 — Inmutabilidad de identidad y auditoría de creación

`Id` y `CreatedAt` inmutables post-construcción. Las props mutables
sólo cambian vía `Update`. No se exponen métodos `With...`.

### RNF-5 — Cero warnings

`dotnet build -c Release`: 0 warnings, 0 errors.
`dotnet format style --verify-no-changes`: exit 0.

### RNF-6 — Convenciones de tests

**Pure xUnit `Assert.*`** (decisión de Phase 4 — diverge de Phase 3
F-001 que usaba FluentAssertions). **Sin mocking library** (hand-rolled
fakes si llegaran a necesitarse, pero F-001 no requiere repositorio).
`[Fact]` y `[Theory]` + `[InlineData]` según corresponda.

---

## 8. Reglas de negocio e invariantes

Cada invariante es enforceable en `Create`/`Update` sin dependencias
externas y testable sin DB.

### INV-1 — `Amount` no es `null`
`ArgumentNullException(nameof(amount))` si `amount is null`.

### INV-2 — `Amount.Amount` estrictamente positivo
`amount.Amount > 0`. `ArgumentException`. Eco de `Expense` INV-2;
`Money.Create(0)` es válido para agregaciones, pero un ingreso de
monto cero no es ingreso.

### INV-3 — `Date >= 1900-01-01`; fechas futuras permitidas
`date >= new DateOnly(1900, 1, 1)`. `ArgumentException`. El
`FutureDateWarning` lo emite el use case 4-003, no la entidad
(decisión #3 del round interactivo).

### INV-4 — `Description` no vacía post-trim, ≤ 200
Tras trim, no vacía ni null, longitud ≤ 200. `ArgumentException`.
**Required** (decisión #2 del round interactivo — mismo que `Expense`).

### INV-5 — `Note` ≤ 500 si no null (normalizada a `null` si blank)
Trim; si vacío/whitespace → `null`; si no, ≤ 500. `ArgumentException`.

### INV-6 — `Source` ≤ 50 si no null (normalizada a `null` si blank)
Trim; si vacío/whitespace → `null`; si no, ≤ 50. `ArgumentException`.
**Campo nuevo** que `Expense` no tiene (decisión #1 del round
interactivo: `string?` opcional, max 50).

### INV-7 — `CategoryId` es `null` OR `!= Guid.Empty`
Si `categoryId` no es null, debe ser `!= Guid.Empty`. Si es null, es
válido (ingreso sin categoría). `ArgumentException` si es `Guid.Empty`.
**Diverge de `Expense` INV-6** (que exige `!= Guid.Empty` siempre).

### INV-8 — `CreatedAt` UTC (contrato del caller) + `UpdatedAt >= CreatedAt` en `Update`
El caller pasa `createdAt` con `DateTimeKind.Utc` (contrato, no
enforcement runtime — eco de `Expense` INV-7). En `Update`,
`updatedAt >= this.CreatedAt` o `ArgumentException` (eco de `Expense`
INV-8).

---

## 9. Modelo de dominio — API surface

`Income` sigue la forma de `Expense`: identidad `Id: Guid` generada
dentro de `Create` (no por la DB); `Amount: Money` embebe la moneda
(sin `Currency` separada); `Date: DateOnly` (EF Core 8 mapea nativo a
`TEXT`); referencias FK por `Guid` (no navegación — `Income` y
`Category` son agregados separados); `Description` obligatoria /
`Note`+`Source` opcionales normalizadas a `null`; `CreatedAt`/
`UpdatedAt` pasados por parámetro (la entidad no referencia `IClock`
— R-1). Igualdad por referencia (`sealed class`, no `record`); no se
sobrescriben `Equals`/`GetHashCode`. Hard-delete (sin soft-delete,
eco de `Expense` §17.7).

---

## 10. Configuración técnica — tabla `Incomes`

| Columna | Tipo C# | Tipo SQLite | Restricciones |
| --- | --- | --- | --- |
| `Id` | `Guid` | `TEXT` | PK, NOT NULL, `ValueGeneratedNever` |
| `CategoryId` | `Guid?` | `TEXT` | **nullable**, FK → `Categories.Id`, `OnDelete(SetNull)`, índice `IX_Incomes_CategoryId` |
| `Date` | `DateOnly` | `TEXT` | NOT NULL, índice `IX_Incomes_Date` |
| `Amount` | `decimal` (`Money.Amount`) | `TEXT` | NOT NULL, `HasPrecision(18, 4)` |
| `Currency` | `Currency` (enum → `int`) | `INTEGER` | NOT NULL |
| `Description` | `string` | `TEXT` | NOT NULL, max 200 |
| `Note` | `string?` | `TEXT` | nullable, max 500 |
| `Source` | `string?` | `TEXT` | nullable, max 50 |
| `CreatedAt` | `DateTime` | `TEXT` | NOT NULL |
| `UpdatedAt` | `DateTime` | `TEXT` | NOT NULL |

`Money` se descompone vía `OwnsOne(e => e.Amount)` en `Amount` +
`Currency` (RN-3 de F-002 Phase 3). No hay tabla `Money` ni columna
`AmountJson`. `CategoryId` nullable + `SetNull` es la divergencia
clave vs `Expenses`.

---

## 11. Validaciones — batería de tests

### 11.1 Unit tests — `Tests.Unit/Domain/IncomeTests.cs`

Pure xUnit `Assert.*` (sin FluentAssertions). ≥8 tests; se listan 16
(el implementador decide la granularity, mínimo 8 cubriendo todos los
casos):

| # | Test | Cubre |
| --- | --- | --- |
| T-1 | `Create_WithValidArgs_ReturnsIncomeWithExpectedState` | Happy path (con categoría y source) |
| T-2 | `Create_WithNullAmount_ThrowsArgumentNullException` | INV-1 |
| T-3 | `Create_WithZeroOrNegativeAmount_ThrowsArgumentException` | INV-2 (`[Theory]` 0, -1) |
| T-4 | `Create_WithDateBeforeSanityFloor_ThrowsArgumentException` | INV-3 floor |
| T-5 | `Create_WithFutureDate_Succeeds` | INV-3 futuro permitido |
| T-6 | `Create_WithEmptyOrWhitespaceDescription_ThrowsArgumentException` | INV-4 vacío |
| T-7 | `Create_WithDescriptionOverMaxLength_ThrowsArgumentException` | INV-4 longitud (201) |
| T-8 | `Create_WithNoteOverMaxLength_ThrowsArgumentException` | INV-5 (501) |
| T-9 | `Create_WithSourceOverMaxLength_ThrowsArgumentException` | INV-6 (51) |
| T-10 | `Create_WithNullCategoryId_Succeeds` | INV-7 (nullable válido) |
| T-11 | `Create_WithEmptyCategoryId_ThrowsArgumentException` | INV-7 (`Guid.Empty` rechazado) |
| T-12 | `Create_WithNullOrWhitespaceNoteAndSource_NormalizesToNull` | INV-5/INV-6 canonización |
| T-13 | `Update_WithValidArgs_RefreshesUpdatedAt_PreservesIdAndCreatedAt` | Update happy + inmutabilidad |
| T-14 | `Update_WithUpdatedAtBeforeCreatedAt_ThrowsArgumentException` | INV-8 |
| T-15 | `Update_WithInvalidArgs_ThrowsArgumentException` | Re-validación INV-1..INV-7 |
| T-16 | `Equality_ByReference_TwoInstancesNotEqual` | Igualdad por referencia |

### 11.2 Schema tests — `Tests.Integration/Persistence/IncomeSchemaTests.cs`

Reutilizan `TestAppDbContextFactory` (F-006 Fase 2). xUnit `[Fact]`.

| # | Test | Verifica |
| --- | --- | --- |
| ST-1 | `Migrate_CreatesIncomesTable` | Tras `MigrateAsync`, `Incomes` existe con las 10 columnas |
| ST-2 | `IncomeConfiguration_HasExpectedColumnTypesAndConstraints` | `CategoryId` nullable; `Source` max 50; FK `SetNull`; índices |
| ST-3 | `Money_OwnsOne_ProducesTwoColumns` | Columnas `Amount` y `Currency` presentes, NOT NULL |

---

## 12. Casos borde

- **EC-1:** `CategoryId = null` válido (INV-7) — diverge de `Expense`.
- **EC-2:** `Source = ""` / `"   "` / `null` → todos normalizan a
  `null`.
- **EC-3:** `Date = 1900-01-01` válido (floor inclusivo); `Date =
  2099-12-31` válido (futuro permitido, warning en 4-003).
- **EC-4:** `Description = "  salario  "` → `"salario"` (trim).
- **EC-5:** Borrar `Category` con `Incomes` → `SetNull` (preserva
  ingresos). Diverge de `Expense` (`Restrict` bloquea).
- **EC-6:** `Update` con `UpdatedAt == CreatedAt` válido (inclusivo).
- **EC-7:** `OwnsOne` de `Money` (record con props `{ get; }` y ctor
  privado) — ya probado en `Expense`/`AddExpenseTable` (F-002 P3).
  Riesgo acotado.

---

## 13. Dependencias

### Prerrequisitos
- **Phase 1 F-002:** `Money` + `Currency` (consumidos, no modificados).
- **Phase 1 F-006:** `IIncomeRepository` existe (referencia `Income`;
  se mantiene compilando — namespace sin cambios). Verificado en
  `src/.../Application/Abstractions/IIncomeRepository.cs`.
- **Phase 1 F-007:** NetArchTest R-1..R-5 — no romper.
- **Phase 3 F-001 (archivada):** patrón `Expense` entity (espejo).
- **Phase 3 F-002 (archivada):** patrón `ExpenseConfiguration` +
  `AddExpenseTable` + `OwnsOne` ya probado.
- **Phase 3 F-006 (archivada):** `TestAppDbContextFactory` para
  schema tests.
- `dotnet-ef` 8.0.x local (`.config/dotnet-tools.json`).

### Dependencias con otras features (Phase 4)
| Feature | Relación |
| --- | --- |
| 4-002 | Consumidora — `IncomeRepository` persiste/recupera `Income` sobre `Incomes` |
| 4-003 | Consumidora — `RegisterIncomeUseCase` construye `Income` vía `Create`; emite `FutureDateWarning` |
| 4-005 | Consumidora — `UpdateIncomeUseCase`/`DeleteTransactionUseCase` |
| 4-008 | Consumidora (vía DTO) — UI consume `IncomeDto`, no la entidad |

### Bloqueos
**Ninguno.** F-001 P4 puede implementarse inmediatamente.

---

## 14. Fuera de alcance (detallado)

- `IncomeRepository` + `IncomeFilter` (4-002).
- Use cases / validators / commands / `RegisterIncomeResult` (4-003).
  `FutureDateWarning` es del use case, no de la entidad.
- `UpdateIncomeUseCase` / `DeleteTransactionUseCase` (4-005).
- UI / ViewModels / `GetIncomesQuery` (4-008).
- Modificar `IIncomeRepository`, `Expense`, `Money`, `Currency`,
  `Category`, `ServiceCollectionExtensions`.
- `Source` enum (decidido `string?`).
- Seed / `InsertData`. CHECK constraints de DB que re-validen
  invariantes (duplicación de lógica). Soft-delete / audit extra.
- Nuevos paquetes NuGet. Modificar `OnModelCreating`. Migraciones
  extra.

---

## 15. Criterios de aceptación

### AC-1 — `Income.cs` reescrito in-place
- [ ] `src/.../Domain/Transactions/Income.cs` reescrito (placeholder
      → entidad). Misma carpeta que `Expense.cs`; **NO** se crea
      `Domain/Incomes/`.
- [ ] Namespace `ControlDeFinanzasIA.Domain.Transactions` sin cambios.

### AC-2 — `public sealed class Income` sin base
- [ ] `public sealed class Income` (**sin** base `Entity<Guid>` — no
      existe en el codebase; ver §17 C-1).
- [ ] 9 props con `private set`: `Id` (`Guid`), `CategoryId`
      (`Guid?`), `Date` (`DateOnly`), `Amount` (`Money`),
      `Description` (`string`), `Note` (`string?`), `Source`
      (`string?`), `CreatedAt` (`DateTime`), `UpdatedAt` (`DateTime`).
- [ ] Sin propiedad `Currency` separada.

### AC-3 — 8 invariantes en `Create` y `Update`
- [ ] INV-1..INV-8 enforced vía `ValidateAndNormalize` privado
      (espejo de `Expense`, extendido con `Source` e `INV-7` nullable).

### AC-4 — `Income.Create(...)`
- [ ] `Create(Guid? categoryId, DateOnly date, Money amount, string
      description, string? note, string? source, DateTime createdAt)`
      genera `Id` vía `Guid.NewGuid()`, setea `UpdatedAt = createdAt`,
      normaliza `Description`/`Note`/`Source`, retorna `Income` o
      lanza `ArgumentNullException`/`ArgumentException`.

### AC-5 — `Income.Update(...)`
- [ ] `Update(Guid? categoryId, DateOnly date, Money amount, string
      description, string? note, string? source, DateTime updatedAt)`
      reasigna mutables, revalida invariantes, refresca `UpdatedAt`.
      `Id`/`CreatedAt` inmutables. Valida `updatedAt >= CreatedAt`.

### AC-6 — `IncomeConfiguration.cs` creada
- [ ] `src/.../Infrastructure/Persistence/Configurations/IncomeConfiguration.cs`
      existe como `public sealed class IncomeConfiguration :
      IEntityTypeConfiguration<Income>`.

### AC-7 — Mapeo
- [ ] `b.ToTable("Incomes")`, `Id` PK `ValueGeneratedNever`,
      `CategoryId` **nullable** FK → `Categories.Id` con
      `OnDelete(DeleteBehavior.SetNull)`, `Date` `IsRequired`,
      `Description` required max 200, `Note` max 500 nullable,
      `Source` max 50 nullable, `CreatedAt`/`UpdatedAt` `IsRequired`.
- [ ] Índices `IX_Incomes_Date` + `IX_Incomes_CategoryId`.

### AC-8 — `OwnsOne(Amount)`
- [ ] `OwnsOne(e => e.Amount)` con `Amount` (decimal,
      `HasPrecision(18,4)`, `IsRequired`) + `Currency` (int,
      `IsRequired`) — espejo exacto de `ExpenseConfiguration`.

### AC-9 — `AppDbContext` actualizado
- [ ] `public DbSet<Income> Incomes => Set<Income>();` (expression-
      bodied, get-only — convención real del codebase, ver §17 C-4)
      tras `Expenses`.
- [ ] `OnModelCreating` **sin cambios**.
- [ ] `ServiceCollectionExtensions.AddPersistence` **sin cambios**.

### AC-10 — Migración generada
- [ ] `dotnet ef migrations add AddIncomesTable --project
      src/ControlDeFinanzasIA.Infrastructure --startup-project
      src/ControlDeFinanzasIA.UI` ejecutado sin error (ver §17 C-5).
- [ ] 3 archivos commiteados: `<ts>_AddIncomesTable.cs`,
      `.Designer.cs`, `AppDbContextModelSnapshot.cs`.

### AC-11 — `Up` crea `Incomes`
- [ ] `Up` crea `Incomes` (10 cols incluidas `Amount`/`Currency`),
      FK `SetNull` a `Categories`, índices `IX_Incomes_Date` +
      `IX_Incomes_CategoryId`.

### AC-12 — `Up` sin seed y aditiva
- [ ] `Up` **sin** `InsertData`. `Up` no toca
      `Categories`/`PaymentMethods`/`Expenses`.

### AC-13 — `Down`
- [ ] `Down` = `migrationBuilder.DropTable("Incomes")`.

### AC-14 — Unit tests
- [ ] `tests/.../Tests.Unit/Domain/IncomeTests.cs` con ≥8 tests
      (happy `Create` + cada invariante + `Update` happy + igualdad
      por referencia). Pure xUnit `Assert.*`, **sin** FluentAssertions,
      **sin** mocking.

### AC-15 — Build y tests
- [ ] `dotnet build -c Release` 0 warnings/0 errors.
- [ ] `dotnet test` 222 existentes + nuevos (unit + schema) verdes
      (≈230).
- [ ] NetArchTest R-1..R-5 verde.
- [ ] `dotnet format style --verify-no-changes` exit 0.

### AC-16 — Sin scope creep
- [ ] No `IncomeRepository` (4-002), no use cases (4-003/4-005), no
      UI (4-008), no `IIncomeRepository` modificado, no `Source` enum,
      no seed, no migraciones extra.

---

## 16. Estructura de archivos esperada

| Archivo | Acción | Contenido |
| --- | --- | --- |
| `src/ControlDeFinanzasIA.Domain/Transactions/Income.cs` | **Reescribir in-place** | `public sealed class Income` con factory `Create`, `Update`, 2 ctors privados, 9 props `private set`, 8 invariantes. |
| `src/ControlDeFinanzasIA.Infrastructure/Persistence/Configurations/IncomeConfiguration.cs` | Crear | `IEntityTypeConfiguration<Income>` (RF-6, RF-8). |
| `src/ControlDeFinanzasIA.Infrastructure/Persistence/AppDbContext.cs` | Editar | + `DbSet<Income> Incomes => Set<Income>();` (expression-bodied). No tocar `OnModelCreating`. |
| `src/ControlDeFinanzasIA.Infrastructure/Persistence/Migrations/<ts>_AddIncomesTable.cs` | Crear (generado) | `Up` crea `Incomes` + índices + FK; `Down` la borra. |
| `src/ControlDeFinanzasIA.Infrastructure/Persistence/Migrations/<ts>_AddIncomesTable.Designer.cs` | Crear (generado) | Metadata. |
| `src/ControlDeFinanzasIA.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs` | Editar (generado) | + `Incomes`. |
| `tests/ControlDeFinanzasIA.Tests.Unit/Domain/IncomeTests.cs` | Crear | ≥8 tests pure xUnit `Assert.*`. |
| `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/IncomeSchemaTests.cs` | Crear | 3 tests de forma del schema. |

**Sin cambios:** `Expense.cs`, `Money.cs`, `Currency.cs`, `Category.cs`,
`ExpenseConfiguration.cs`, `ServiceCollectionExtensions.cs`,
`IIncomeRepository.cs`, `Directory.Packages.props`, `Application/`, `UI/`.

---

## 17. Decisiones de diseño y correcciones del plan

### Correcciones del plan (CRÍTICAS — preservar para sdd-tasks/sdd-apply)

El phase plan `spec/phases/phase-04/features.md` RF-1 dice
textualmente `public sealed class Income : Entity<Guid>`. El
codebase real diverge en cinco puntos, ya corregidos por el
sub-agente de propuesta y **consolidados aquí** para que el agente de
implementación no los re-introduzca:

- **C-1 — `Entity<Guid>` no existe.** Verificado: `Expense` es
  `public sealed class Expense` standalone con `Guid Id` explícito.
  La spec fija `Income` como `public sealed class Income` (sin base).
  Espejo del patrón archivado, no del texto aspiracional del plan.
- **C-2 — `Income.cs` ya existe como placeholder vacío.** Lo dejó
  Phase 3 F-001 en `src/.../Domain/Transactions/Income.cs` para que
  `IIncomeRepository` compile. F-001 P4 lo **reescribe in-place**; NO
  crea carpeta `Domain/Incomes/`.
- **C-3 — `CategoryId` es `Guid?` con `OnDelete(SetNull)`.**
  Divergencia intencional de `Expense` (`Guid` required +
  `Restrict`). Ingresos sin categoría son válidos; borrar una
  `Category` preserva los `Income` seteando `CategoryId` a null.
- **C-4 — `AppDbContext.DbSet<T>` es expression-bodied**
  (`=> Set<T>()`, get-only), no `{ get; set; }`. El `DbSet<Income>`
  sigue este patrón exacto.
- **C-5 — `--startup-project` es `UI`, no `Infrastructure`.**
  `AGENTS.md` (ground truth actual) fija
  `--startup-project src/ControlDeFinanzasIA.UI`. La spec de F-002
  P3 usaba `Infrastructure` (convención anterior); ya está superada.

### Decisiones de diseño explícitas

- **DP-1 — Standalone sealed class, sin base.** Espejo de `Expense`
  (§17.1 de F-001 P3). No especular jerarquía `Transaction` con un
  solo subtipo nuevo (YAGNI).
- **DP-2 — `CategoryId` nullable + `SetNull`.** Ingresos sin
  categoría válidos (RF-1 del plan). `SetNull` preserva datos del
  usuario al borrar `Category`. Alternativa `Cascade` descartada
  (pierde datos); `Restrict` rompería la semántica "ingreso sin
  categoría es válido".
- **DP-3 — `Source: string?`, max 50, normalizado a `null` si blank.**
  Decisión #1 del round interactivo. No es enum (flexibilidad para
  "Sueldo", "Freelance", "Transferencia", etc.).
- **DP-4 — `Description` required, max 200.** Decisión #2 del round
  interactivo (mismo que `Expense`).
- **DP-5 — `Money` como `OwnsOne` (dos columnas primitivas).** Espejo
  exacto de `ExpenseConfiguration` (RN-3 de F-002 P3). Ya probado en
  `AddExpenseTable`. No es un value object nuevo.
- **DP-6 — Reescritura in-place del placeholder.** No crea archivo
  nuevo ni carpeta nueva; reemplaza el contenido de `Income.cs` (C-2).
- **DP-7 — Fechas futuras permitidas en la entidad; warning en el use
  case.** Decisión #3 del round interactivo. La entidad admite
  `Date >= 1900-01-01` (INV-3); `FutureDateWarning` lo emite
  `RegisterIncomeUseCase` (4-003), no la entidad.
- **DP-8 — Hard-delete, sin soft-delete.** Eco de `Expense` §17.7.
- **DP-9 — Pure xUnit `Assert.*` en tests.** Convención de Phase 4
  (diverge de Phase 3 F-001 que usaba FluentAssertions). Sin mocking
  library.

---

## 18. Riesgos y mitigaciones

| Riesgo | Prob. | Impacto | Mitigación |
| --- | --- | --- | --- |
| Plan-vs-codebase drift: `Income : Entity<Guid>` no coincide (no existe `Entity<TId>`) | Alta (ya detectada) | Alto | §17 C-1 documenta la corrección; spec fija `sealed class` sin base. |
| `CategoryId` nullable + `SetNull` diverge de `Expense` (`Restrict`) — inconsistencia aparente | Media | Medio | DP-2: divergencia deliberada y documentada. Ingreso sin categoría es válido. |
| `--startup-project`: F-002 P3 usó `Infrastructure`, `AGENTS.md` fija `UI` | Baja | Bajo | §17 C-5: usar `--startup-project src/ControlDeFinanzasIA.UI`. |
| `OwnsOne` no hidrata `Money` (record con props `{ get; }` + ctor privado) | Baja (ya probado en `Expense`) | Medio | Patrón validado en `AddExpenseTable` (F-002 P3). Eco de §17.5 F-002 P3. |
| `DateOnly` no se mapea nativamente en EF Core 8 a SQLite | Baja | Medio | Ya probado en `Expenses`. EF Core 8 mapea `DateOnly` nativo a `TEXT`. |
| Tests Phase 4 usan pure xUnit (no FluentAssertions) — diverge de Phase 3 | Baja | Bajo | DP-9: convención de Phase 4 documentada. |
| `Income` reescrito pierde el namespace y rompe `IIncomeRepository` | Baja | Alto | RF-1: namespace `Domain.Transactions` sin cambios. AC-1 verifica. |

---

## 19. Referencias

- `docs/vision.md` §4.1/§4.2 (movimientos: `Expense`/`Income`,
  subconjunto mínimo), §2 (single-user).
- `docs/architecture.md` §1 (dominio puro), §4 (R-1..R-5), §6 (capa
  de dominio), §8 (mapeo Fluent API, migraciones versionadas, value
  objects a columnas primitivas), §19 (ADR-007 `IClock`).
- `docs/roadmap.md` §Fase 4 (entregable: entidad `Income`).
- `spec/constitution/mission.md`, `spec/constitution/tech-stack.md`.
- `spec/phases/phase-04/features.md` §RF-1, RF-2 (contexto del plan).
- `spec/features/phase-03/F-001-expense-domain-entity.done.md` —
  patrón `Expense` entity (espejo): `sealed class` sin base, factory
  `Create`, `Update`, `ValidateAndNormalize`, 8 invariantes.
- `spec/features/phase-03/F-002-expense-configuration-migration.done.md`
  — patrón `ExpenseConfiguration` + `AddExpenseTable` + `OwnsOne`
  (espejo + `SetNull` divergence).
- `spec/features/phase-03/F-006-*.done.md` — `TestAppDbContextFactory`
  reutilizada por los schema tests.
- `src/.../Domain/ValueObjects/Money.cs` — `Money.Create`, props
  `Amount`/`Currency` (consumido, no modificado).
- `src/.../Domain/Enums/Currency.cs` — `DOP=0, USD=1`.
- `src/.../Domain/Catalog/Category.cs` — target de la FK
  `CategoryId`.
- `src/.../Application/Abstractions/IIncomeRepository.cs` —
  interface existente (Fase 1 F-006); no se modifica.
- `AGENTS.md` — convención de migraciones (`--startup-project`
  `UI`), NetArchTest R-1..R-5, MVVM estricto.
- `spec/README.md` — convención `.spec.md` → `.done.md`.
