# Specification — F-002: `ExpenseConfiguration` + migración `AddExpenseTable`

> **Fase:** 3 — Slice vertical: registrar y listar gastos
> **Feature:** F-002 (local a esta fase)
> **Estado:** IMPLEMENTADA, VERIFICADA Y ARCHIVADA (cycle cerrado el 2026-08-04)
> **Spec ID:** SPEC-F002P3
>
> **Nota de numeración:** este `F-002` es el de la **Fase 3**
> (`spec/phases/phase-03/features.md`). No debe confundirse con
> el `F-002` de Fase 1 (Money value object en
> `spec/features/phase-01/F-002-money-value-object.done.md`) ni
> con el `F-002` de Fase 2 (configuraciones de `Category` /
> `PaymentMethod` en
> `spec/features/phase-02/F-002-entity-configurations-migration.done.md`).
> Los IDs son locales a cada fase; el folder los desambigua. Ver
> `spec/README.md` para la convención.
>
> Fuentes de verdad: `docs/vision.md`, `docs/architecture.md`,
> `docs/roadmap.md` (§Fase 3), `spec/constitution/tech-stack.md`,
> `spec/constitution/mission.md`,
> `spec/phases/phase-03/features.md` (F-002 dentro del desglose de
> la fase 3),
> `spec/features/phase-03/F-001-expense-domain-entity.done.md`
> (la entidad `Expense` que esta feature mapea — contrato de
> entrada),
> `spec/features/phase-02/F-002-entity-configurations-migration.done.md`
> (patrón estructural que esta spec espeja: configuraciones
> Fluent API + migración versionada),
> `spec/features/phase-02/F-006-integration-tests.done.md`
> (provee `TestAppDbContextFactory` que los tests de schema de
> esta feature reutilizan).

---

## 1. Resumen

F-002 mapea la entidad `Expense` (introducida y blindada en
F-001 de esta fase) al schema de SQLite mediante una
configuración Fluent API `ExpenseConfiguration`, resuelve el
**primer mapeo de un value object compuesto (`Money`) a columnas
primitivas** del proyecto, y produce una nueva migración
versionada `AddExpenseTable` que añade la tabla `Expenses` sin
destruir el schema existente (`Categories`, `PaymentMethods`).

Al final de la feature:

- `src/ControlDeFinanzasIA.Infrastructure/Persistence/Configurations/ExpenseConfiguration.cs`
  existe como `public sealed class ExpenseConfiguration :
  IEntityTypeConfiguration<Expense>` y se aplica
  automáticamente vía `ApplyConfigurationsFromAssembly`
  (heredado de F-001 de Fase 2).
- El value object `Money` se mapea con `OwnsOne(e => e.Amount)`
  en **dos columnas primitivas** (`Amount` decimal +
  `Currency` int). Decisión fijada en §17.1; el patrón será
  replicado por `RecurringRule`, `Debt`, `Goal`, `Budget` e
  `Income` en fases posteriores.
- `AppDbContext` expone un nuevo `DbSet<Expense> Expenses` (la
  única edición al `AppDbContext` en esta feature).
- La migración `AddExpenseTable` está generada y commiteada en
  `Infrastructure/Persistence/Migrations/` con sus 3 archivos
  (la migración, su `Designer`, y el `ModelSnapshot` actualizado).
- `dotnet build` y `dotnet test` siguen pasando en limpio; los
  tests de NetArchTest R-1 a R-5 (F-007 de Fase 1) siguen
  verdes: F-002 no añade paquetes a `Domain` ni a `Application`.
- Tres tests de integración de **forma del schema** cubren la
  existencia de la tabla, las columnas/restricciones, y la
  descomposición de `Money` en dos columnas.

---

## 2. Contexto

La visión (`docs/vision.md` §4.1) fija como capacidad nuclear del
producto **registrar un gasto del día a día**. El roadmap
(`docs/roadmap.md` §Fase 3) materializa esa capacidad como un
slice vertical end-to-end descompuesto en 7 features. F-001
entregó la entidad `Expense` blindada con sus 8 invariantes en
el dominio puro. Pero una entidad de dominio sin mapeo de
persistencia es un contrato sin almacenamiento: ningún
repositorio (F-003), ningún use case de escritura (F-004), ningún
query de lectura (F-005) y ninguna vista (F-007) pueden
materializar un gasto en disco hasta que exista la tabla
`Expenses` y el mapeo que la llena.

F-002 cierra esa brecha. Conecta la entidad `Expense` (definida
en `Domain/Transactions/Expense.cs` por F-001) con la capa de
persistencia de EF Core 8 + SQLite establecida en la Fase 2
(`AppDbContext`, `ApplyConfigurationsFromAssembly`,
migración `InitialSchema`, convención PascalCase,
`ValueGeneratedNever` para `Guid`s generados en dominio).

**Riesgo diferido que esta feature materializa.** La Fase 2
difirió explícitamente la conversión de value objects a EF Core
(F-002 de Fase 2 §4.2: "Conversión de value objects (`Money`,
`DateRange`) en EF Core... el patrón se establece en Fase 3
cuando se introduzca `Expense`"). F-002 de Fase 3 es esa
feature. El patrón que se fija aquí — cómo se descompone `Money`
en columnas — será replicado por **cinco entidades futuras**
(`RecurringRule`, `Debt`, `Goal`, `Budget`, `Income`), todas con
propiedad `Amount: Money`. Fijarlo bien ahora evita un retrofit
costoso cuando ya haya 6 entidades con `Money` persistida.

**Trazabilidad.** `vision.md` §4.1 (gastos del día a día) y §4.2
(subconjunto mínimo de un movimiento: fecha, monto+moneda,
categoría, nota opcional). `architecture.md` §8 (mapeo vía
`IEntityTypeConfiguration<T>`, migraciones versionadas, mapeo
de value objects a columnas primitivas, índices por fecha y
categoría). `roadmap.md` §Fase 3 (entregable: persistencia de
`Expense`).

---

## 3. Objetivo

1. **Crear `ExpenseConfiguration`** con el mapeo Fluent API
   completo para `Expense`: tabla `Expenses`, primary key en
   `Id` con `ValueGeneratedNever`, tipos y nulabilidad de las 8
   propiedades, longitudes máximas para `Description` (200) y
   `Note` (500), FK de `CategoryId` a `Categories.Id` con
   `OnDelete(Restrict)`, e índices en `Date` y `CategoryId`.
2. **Fijar el patrón de mapeo de `Money`** como `OwnsOne`
   produciendo dos columnas primitivas (`Amount` decimal con
   `HasPrecision(18, 4)`, `Currency` int). Decisión justificada
   en §17.1.
3. **Añadir `DbSet<Expense> Expenses` a `AppDbContext`**,
   ubicado tras los `DbSet` existentes de catálogo
   (`Categories`, `PaymentMethods`).
4. **Generar la migración `AddExpenseTable`** con
   `dotnet ef migrations add`; commitear los 3 archivos
   resultantes. La migración es **aditiva**: crea `Expenses` +
   índices + FK; no toca `Categories` ni `PaymentMethods`.
5. **NO incluir `InsertData` en la migración.** `Expense` es
   dato de usuario, no dato del sistema; el seed de sistema
   (categorías y métodos de pago) es de F-003 de Fase 2 y no se
   reproduce aquí.
6. **Añadir tests de integración de forma del schema** que
   verifiquen que la migración produce la tabla, las columnas,
   la FK y la descomposición de `Money` (ver §11).
7. **Mantener NetArchTest R-1 a R-5 en verde.** F-002 sólo
   añade código a `Infrastructure/Persistence/`; no añade
   paquetes a `Domain` ni a `Application`.

---

## 4. Alcance

### 4.1 En alcance

- Creación de
  `src/ControlDeFinanzasIA.Infrastructure/Persistence/Configurations/ExpenseConfiguration.cs`.
- Edición de
  `src/ControlDeFinanzasIA.Infrastructure/Persistence/AppDbContext.cs`
  para añadir `DbSet<Expense> Expenses => Set<Expense>();`.
- Generación y commiteo de la migración `AddExpenseTable` (3
  archivos en `Infrastructure/Persistence/Migrations/`).
- Creación de tests de integración de forma del schema en
  `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/`
  reutilizando `TestAppDbContextFactory` (F-006 de Fase 2).
- Verificación de que `dotnet build` y los tests existentes
  siguen pasando, más los nuevos de schema.
- Verificación de que NetArchTest R-1 a R-5 siguen pasando.

### 4.2 Fuera de alcance

- **Implementar `ExpenseRepository`.** Es F-003 de esta fase.
  F-002 produce la tabla sobre la que el repositorio operará.
- **Extender `IExpenseRepository` con `GetByFilterAsync`.** Es
  F-003.
- **Use cases** (`RegisterExpenseUseCase`,
  `RegisterExpenseValidator`, `GetTransactionsQuery`, DTOs,
  `ExpenseExtensions.ToDto`). Son F-004 y F-005.
- **UI** (`ExpensesViewModel`, `ExpensesView`, navegación,
  DataTemplates). Son F-006 y F-007.
- **Seed data de `Expense`.** `Expense` es dato de usuario; el
  seed de sistema (9 categorías + 5 métodos de pago) es de
  F-003 de Fase 2 y no se reproduce ni extiende aquí.
- **Validación cross-aggregate de `CategoryId`** (verificar que
  la categoría exista antes de persistir). Es de
  `ExpenseRepository.AddAsync` (F-003) y del
  `RegisterExpenseValidator` (F-004). La persistencia sólo
  declara la FK estructural; no re-valida reglas de negocio.
- **CHECK constraints a nivel de DB** que re-validen
  invariantes de F-001 (monto positivo, descripción no vacía,
  etc.). Las invariantes viven en la entidad (F-001 INV-1 a
  INV-8); replicarlas en la DB es scope creep y duplicación de
  lógica (`mission.md`: "Evitar duplicación de lógica").
  F-002 sólo impone restricciones estructurales (nulabilidad,
  longitud máxima, FK).
- **Tests de integración del repositorio** (CRUD, paginación,
  filtros). Son F-003. F-002 sólo testa la **forma del schema**
  que él mismo produce.
- **Soft-delete, audit fields extra** (`IsDeleted`, `CreatedBy`,
  `Version`). Rechazados en v1 por F-001 §17.7 y §17.8.
- **Conversión de moneda** (DOP ↔ USD). No es persistencia.
- **Modificaciones a `Domain` o `Application`.** F-002 sólo
  toca `Infrastructure/Persistence/` y `Tests.Integration`.

---

## 5. Casos de uso

### CU-1 — `ApplyConfigurationsFromAssembly` carga `ExpenseConfiguration`

- **Actor:** `AppDbContext` (F-001 de Fase 2).
- **Trigger:** se construye el `AppDbContext` (al arrancar la
  app o en un test).
- **Precondición:** F-002 creó `ExpenseConfiguration` en
  `Infrastructure/Persistence/Configurations/`; `AppDbContext`
  ya llama a `ApplyConfigurationsFromAssembly` (heredado).
- **Postcondición:** EF Core aplica la configuración al
  construir el modelo. La tabla se llama `Expenses` (PascalCase);
  `Id` es PK con `ValueGeneratedNever`; `Money` se descompone en
  `Amount` + `Currency`; la FK a `Categories` está declarada con
  `Restrict`; los índices en `Date` y `CategoryId` están
  declarados.

### CU-2 — `dotnet ef migrations add AddExpenseTable` genera los 3 archivos

- **Actor:** developer (o CI).
- **Trigger:** se ejecuta el comando desde la raíz del repo.
- **Precondición:** la herramienta `dotnet-ef` 8.0.x está
  instalada localmente (`.config/dotnet-tools.json`, heredado de
  F-002 de Fase 2); `ExpenseConfiguration` y el `DbSet<Expense>`
  ya existen y compilan.
- **Postcondición:** se crean 3 archivos en
  `Infrastructure/Persistence/Migrations/`:
  `YYYYMMDDhhmmss_AddExpenseTable.cs` (métodos `Up` y `Down`),
  `YYYYMMDDhhmmss_AddExpenseTable.Designer.cs` (metadata), y
  `AppDbContextModelSnapshot.cs` (actualizado con la tabla
  `Expenses`). El `Up` contiene **sólo DDL** (CREATE TABLE +
  CREATE INDEX + ADD FK); **no** contiene `InsertData`.

### CU-3 — La migración es commiteable y reproducible

- **Actor:** git.
- **Trigger:** `git add` + `git commit` tras generar la
  migración.
- **Precondición:** los 3 archivos están commiteados.
- **Postcondición:** otro developer que clone el repo puede
  ejecutar `dotnet ef database update` (o, en runtime,
  `db.Database.Migrate()`) y obtiene la tabla `Expenses` sin
  perder `Categories` ni `PaymentMethods`.

### CU-4 — Aplicar la migración a una DB con `InitialSchema` ya aplicada

- **Actor:** la app al arrancar (F-005 de Fase 2) o
  `dotnet ef database update` manual.
- **Trigger:** se aplica `AddExpenseTable` sobre una DB que ya
  tiene `InitialSchema`.
- **Precondición:** `Categories` y `PaymentMethods` existen y
  están sembradas (9 + 5 registros del sistema).
- **Postcondición:** la tabla `Expenses` se crea vacía; las 9
  categorías y 5 métodos siguen intactos; la FK
  `Expenses.CategoryId → Categories.Id` está activa con
  `Restrict`.

### CU-5 — El build y los tests siguen pasando

- **Actor:** build runner / CI.
- **Trigger:** `dotnet build` y `dotnet test`.
- **Precondición:** F-002 completa.
- **Postcondición:** 0 warnings, 0 errors. Los tests
  heredados (≈145 de F-001 de Fase 3 + los de Fases 1 y 2)
  siguen verdes; los 3 nuevos de schema pasan; los 11 tests de
  NetArchTest (R-1 a R-5) siguen verdes.

---

## 6. Requisitos funcionales

### RF-1 — `ExpenseConfiguration`

- `public sealed class ExpenseConfiguration :
  IEntityTypeConfiguration<Expense>` en
  `src/ControlDeFinanzasIA.Infrastructure/Persistence/Configurations/ExpenseConfiguration.cs`.
- Implementa `void Configure(EntityTypeBuilder<Expense> b)`:
  - `b.ToTable("Expenses")` (PascalCase, coherente con
    `CategoryConfiguration` / `PaymentMethodConfiguration` de
    F-002 de Fase 2 y con `architecture.md` §8: "PascalCase
    salvo decisión explícita").
  - `b.HasKey(e => e.Id)`.
  - `b.Property(e => e.Id).ValueGeneratedNever()` (el `Guid` lo
    genera `Expense.Create` vía `Guid.NewGuid()`; F-001 §9.1 y
    §17.10; replicar la decisión `RN-4` de F-002 de Fase 2).
  - `b.Property(e => e.CategoryId).IsRequired()` (`Guid`,
    no-nullable; FK a `Categories.Id`).
  - `b.Property(e => e.Date).IsRequired()` (`DateOnly`; EF Core 8
    mapea nativamente a `TEXT` ISO-8601 en SQLite — verificar
    vía Context7 al implementar; sin `ValueConverter`).
  - `b.Property(e => e.Description).IsRequired().HasMaxLength(200)`
    (eco estructural de INV-4 de F-001).
  - `b.Property(e => e.Note).HasMaxLength(500)` (nullable; eco
    estructural de INV-5 de F-001; sin `IsRequired()`).
  - `b.Property(e => e.CreatedAt).IsRequired()` (`DateTime` →
    `TEXT` en SQLite; el `Kind` UTC no se persiste — contrato del
    caller, F-001 INV-7).
  - `b.Property(e => e.UpdatedAt).IsRequired()` (ídem).
  - **FK a `Categories`**: `b.HasOne<Category>().WithMany()
    .HasForeignKey(e => e.CategoryId)
    .OnDelete(ReferentialAction.Restrict)`. Sin propiedad
    navegable (`Expense` sólo conoce el `Guid`, no la entidad
    `Category` — F-001 §9.4 "no references between aggregates").
    `Restrict` bloquea en DB el borrado de una categoría con
    gastos; el comportamiento de borrado a nivel de Application
    se define en Fase 4.
  - **Índice en `Date`**: `b.HasIndex(e => e.Date)` → produce
    `IX_Expenses_Date` (queries por rango de fecha — F-005
    `GetTransactionsQuery`, filtros de Fase 4).
  - **Índice en `CategoryId`**: `b.HasIndex(e => e.CategoryId)`
    → produce `IX_Expenses_CategoryId` (joins / agregaciones por
    categoría — `architecture.md` §8: "Índices: por fecha, por
    categoría").
- File-scoped namespace:
  `namespace ControlDeFinanzasIA.Infrastructure.Persistence.Configurations;`.
- `using` al tope: `using ControlDeFinanzasIA.Domain.Transactions;`
  (para `Expense`), `using ControlDeFinanzasIA.Domain.Catalog;`
  (para `Category`, referenciada en `HasOne<Category>()`),
  `using Microsoft.EntityFrameworkCore;` y
  `using Microsoft.EntityFrameworkCore.Metadata.Builders;`.
- XML doc en la clase y en `Configure`, citando
  `architecture.md` §8 y F-001 de Fase 3 como fuentes del
  mapeo.
- `sealed` (no se hereda; replicar convención de F-002 de Fase 2).

### RF-2 — Mapeo de `Money` como `OwnsOne` (decisión §17.1)

- Dentro de `ExpenseConfiguration.Configure`, el value object
  `Money` (propiedad `Amount` de `Expense`) se mapea con
  `OwnsOne`:
  - `b.OwnsOne(e => e.Amount, money => { ... })`.
  - Dentro del bloque `money`:
    - `money.Property(m => m.Amount).HasColumnName("Amount")
      .IsRequired().HasPrecision(18, 4)` (columna `Amount`,
      decimal, no-nullable; precisión/escala por DP-5, default
      `18, 4`).
    - `money.Property(m => m.Currency).HasColumnName("Currency")
      .IsRequired()` (columna `Currency`, `int` — EF Core mapea
      el enum `Currency` a `INTEGER` por convención).
  - El owned type vive en la misma tabla `Expenses` (table
    splitting por defecto de EF Core para `OwnsOne` sin
    `ToTable` separado). No se crea una tabla `Money`.
  - `OwnsOne` configura la propiedad como requerida por defecto
    en EF Core 8 (owned non-nullable); si la versión usada
    requiere marcarlo explícitamente, el implementador lo
    confirma vía Context7.

### RF-3 — `DbSet<Expense>` en `AppDbContext`

- Editar
  `src/ControlDeFinanzasIA.Infrastructure/Persistence/AppDbContext.cs`.
- Añadir `public DbSet<Expense> Expenses => Set<Expense>();`
  **tras** los `DbSet` existentes de catálogo (`Categories`,
  `PaymentMethods`).
- **Justificación de ubicación (1 línea):** preserva el
  agrupamiento "entidades de catálogo sembradas primero,
  entidades transaccionales de usuario después", que se lee
  naturalmente y separa visualmente el seed del sistema de los
  datos del usuario.
- `using ControlDeFinanzasIA.Domain.Transactions;` al tope del
  `AppDbContext.cs` (si no está ya).
- Esta es la **única** edición al `AppDbContext` en F-002. La
  llamada a `ApplyConfigurationsFromAssembly` (F-001 de Fase 2)
  no se toca; descubre `ExpenseConfiguration` automáticamente.

### RF-4 — Migración `AddExpenseTable` generada

- Comando (espejo del de F-002 de Fase 2, confirmado en
  `phase-03/features.md` F-002):
  `dotnet ef migrations add AddExpenseTable --project src/ControlDeFinanzasIA.Infrastructure --startup-project src/ControlDeFinanzasIA.Infrastructure`.
  - Nota: el `--startup-project` es `Infrastructure` porque ahí
    vive el `AppDbContext` y la configuración de diseño (F-002
    de Fase 2 ya generó `InitialSchema` con este mismo
    `--startup-project`). El implementador confirma que sigue
    siendo válido tras el composition root de F-005 de Fase 2
    (que aplica migraciones en runtime desde `UI` pero no
    afecta el scaffolding).
- Se crean 3 archivos en
  `src/ControlDeFinanzasIA.Infrastructure/Persistence/Migrations/`:
  - `YYYYMMDDhhmmss_AddExpenseTable.cs` (la migración).
  - `YYYYMMDDhhmmss_AddExpenseTable.Designer.cs` (metadata).
  - `AppDbContextModelSnapshot.cs` (actualizado con `Expenses`).
  - El timestamp `YYYYMMDDhhmmss` lo asigna la herramienta (ver
    DP-4 sobre UTC vs local).
- Los 3 archivos se commitean.

### RF-5 — Contenido de la migración

- El método `Up` de `YYYYMMDDhhmmss_AddExpenseTable.cs`:
  - `CreateTable` para `Expenses` con las columnas `Id`,
    `CategoryId`, `Date`, `Amount`, `Currency`, `Description`,
    `Note`, `CreatedAt`, `UpdatedAt` (ver tabla de columnas en
    §9).
  - `CreateIndex` para `IX_Expenses_Date` y
    `IX_Expenses_CategoryId`.
  - `AddForeignKey` para la FK `Expenses.CategoryId →
    Categories.Id` con `Restrict`.
  - **NO** incluye `InsertData` (`Expense` es dato de usuario;
    decisión §17.4, eco de RN-5 de F-002 de Fase 2).
  - **NO** toca `Categories` ni `PaymentMethods` (es aditiva).
- El método `Down`:
  - `DropTable` para `Expenses` (los índices y la FK se borran
    implícitamente con la tabla en SQLite).
- `BuildTargetModel` y demás metadata en el `Designer.cs` son
  generados por la herramienta, no editados a mano.

### RF-6 — Aplicación automática de la configuración

- Las configuraciones se aplican vía
  `modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly)`
  en `AppDbContext.OnModelCreating` (heredado de F-001 de Fase
  2). F-002 **NO** modifica `OnModelCreating`; sólo añade
  `ExpenseConfiguration.cs`, que el mecanismo automático
  descubre, y el `DbSet` (RF-3).

### RF-7 — Compilación y tests

- `dotnet build` desde la raíz: 0 warnings, 0 errors (Debug y
  Release).
- `dotnet test`: tests heredados verdes (≈145 tras F-001 de
  Fase 3) + 3 nuevos de schema (§11) ≈ 148 verdes.
- 11 tests de NetArchTest (R-1 a R-5) verdes.
- `dotnet format style --verify-no-changes`: exit 0.

---

## 7. Requisitos no funcionales

### RNF-1 — Compatibilidad con la arquitectura (R-1 a R-5 de F-007 de Fase 1)

- Tras F-002, el grafo de dependencias sigue cumpliendo R-1 a
  R-5.
- **R-1 (Domain sin paquetes externos):** F-002 no añade ningún
  `PackageReference` a `Domain.csproj`. `Expense` sigue siendo
  un POCO puro. Verificable: `dotnet list
  src/ControlDeFinanzasIA.Domain package` sigue vacío.
- **R-2 (Application sólo Domain + FluentValidation):** F-002
  no toca `Application`. Sigue pasando.
- **R-3 (Infrastructure → Application + Domain):** F-002 añade
  código a `Infrastructure` que referencia `Domain.Transactions`
  (`Expense`) y `Domain.Catalog` (`Category` para `HasOne<T>`)
  — patrón ya permitido.
- **R-4 (UI no → Infrastructure salvo composition root):** F-002
  no toca `UI`. Sigue pasando.
- **R-5 (src/ no → Tests):** F-002 no toca referencias de
  proyecto. Sigue pasando.

### RNF-2 — Sin paquetes NuGet nuevos

- F-002 no añade nada a `Directory.Packages.props`. Los paquetes
  EF Core 8 + `Microsoft.Data.Sqlite` ya están (F-001 y F-002
  de Fase 2). `dotnet-ef` 8.0.x ya está en
  `.config/dotnet-tools.json` (F-002 de Fase 2).

### RNF-3 — Determinismo del snapshot

- El `ModelSnapshot` es determinista: regenerar la migración con
  el mismo código produce el mismo snapshot (salvo el timestamp
  del nombre de archivo).
- `ExpenseConfiguration` no tiene lógica condicional basada en
  tiempo, entorno o estado global.

### RNF-4 — Reproducibilidad de la migración

- Otro developer que clone el repo ejecuta `dotnet tool
  restore` + `dotnet ef database update` y obtiene el mismo
  schema (incluida `Expenses`).

### RNF-5 — Sin scope creep

- F-002 no implementa el repositorio (F-003), ni use cases
  (F-004/F-005), ni UI (F-006/F-007), ni seed de `Expense`, ni
  CHECK constraints de invariantes, ni tests CRUD del repo.
- F-002 no modifica `OnModelCreating` (sólo el `DbSet` y el
  nuevo archivo de configuración).
- F-002 no modifica `Domain` ni `Application`.

### RNF-6 — Convenciones de código

- File-scoped namespace, `using` al tope, XML doc en la clase y
  método público, `sealed` en `ExpenseConfiguration`, nombres
  en inglés en código (convención .NET del repo). La prosa de
  la spec es español neutro profesional (convención del repo).

---

## 8. Reglas de negocio y de mapeo

### RN-1 — Las configuraciones viven en `Infrastructure`, no en `Domain`

- Refuerzo de R-1. `Expense` en `Domain/Transactions/` es POCO
  puro, sin atributos de EF. `ExpenseConfiguration` vive en
  `Infrastructure/Persistence/Configurations/`. Eco de RN-1 de
  F-002 de Fase 2.

### RN-2 — Tabla en PascalCase

- `Expenses` (no `expenses`, ni `expense`). Coherente con
  `Categories` / `PaymentMethods` y con `architecture.md` §8.
  Eco de RN-2 de F-002 de Fase 2.

### RN-3 — `Money` se descompone en dos columnas primitivas (no se serializa)

- `OwnsOne(e => e.Amount)` produce `Amount` (decimal) +
  `Currency` (int). **No** se usa `ValueConverter<Money,
  string>` a una columna `AmountJson`. Razón (ver §17.1):
  preserva indexabilidad y consultabilidad por monto; alinea
  con `architecture.md` §8 ("mapear a columnas primitivas") y
  con la recomendación explícita del phase plan F-002.
- Esta es la **regla de mapeo de `Money`** que
  `RecurringRule`, `Debt`, `Goal`, `Budget` e `Income`
  replicarán.

### RN-4 — `Guid` generado en Domain, no en DB

- `ValueGeneratedNever` en `Id`. El `Guid` lo provee
  `Expense.Create` (F-001 §9.1, §17.10). Eco de RN-4 de F-002
  de Fase 2.

### RN-5 — La migración no incluye `InsertData`

- `AddExpenseTable` describe sólo el schema (DDL puro). `Expense`
  es dato de usuario, no dato del sistema. El seed de sistema
  (categorías, métodos) es de F-003 de Fase 2 y no se reproduce.
  Eco de RN-5 de F-002 de Fase 2.

### RN-6 — La FK a `Categories` es `Restrict`

- `OnDelete(ReferentialAction.Restrict)`: la DB bloquea el
  borrado de una `Category` que tenga `Expenses` asociados. El
  comportamiento de borrado a nivel de Application (decidir si
  se permite, reasignar, etc.) es de Fase 4. `Restrict` es la
  red de seguridad estructural.

### RN-7 — La persistencia no re-valida invariantes de dominio

- F-002 impone sólo restricciones estructurales (nulabilidad,
  longitud máxima, FK). Las invariantes INV-1 a INV-8 de F-001
  (monto positivo, descripción no vacía, `CategoryId != Empty`,
  etc.) viven en la entidad y **no** se replican como CHECK
  constraints de DB. Razón: evitar duplicación de lógica
  (`mission.md`) y mantener la entidad como única fuente de las
  reglas de negocio.

---

## 9. Modelo de mapeo — tabla `Expenses`

F-002 no introduce entidades de dominio. Sólo añade el mapeo de
`Expense` (definida en F-001) a la tabla `Expenses`. La tabla
sigue la convención de columnas PascalCase y los tipos de
SQLite heredados de F-002 de Fase 2.

**Tabla `Expenses`:**

| Columna | Tipo C# | Tipo SQLite | Restricciones |
| --- | --- | --- | --- |
| `Id` | `Guid` | `TEXT` | PK, NOT NULL, `ValueGeneratedNever` (Guid desde `Expense.Create`). |
| `CategoryId` | `Guid` | `TEXT` | NOT NULL, FK → `Categories.Id`, `OnDelete(Restrict)`, índice `IX_Expenses_CategoryId`. |
| `Date` | `DateOnly` | `TEXT` | NOT NULL (ISO-8601 nativo de EF Core 8), índice `IX_Expenses_Date`. |
| `Amount` | `decimal` (vía `Money.Amount`) | `TEXT` | NOT NULL, `HasPrecision(18, 4)` (DP-5). |
| `Currency` | `Currency` (enum → `int`) | `INTEGER` | NOT NULL (enum mapeado a entero por convención). |
| `Description` | `string` | `TEXT` | NOT NULL, max 200. |
| `Note` | `string?` | `TEXT` | nullable, max 500. |
| `CreatedAt` | `DateTime` | `TEXT` | NOT NULL (`Kind` UTC no persistido; contrato del caller). |
| `UpdatedAt` | `DateTime` | `TEXT` | NOT NULL (ídem). |

**Notas sobre la descomposición de `Money`:** las columnas
`Amount` y `Currency` provienen del `OwnsOne(e => e.Amount)` de
RF-2. No existe una columna `AmountJson` ni una tabla `Money`
separada. `Expense` no expone una propiedad `Currency` en el
dominio (F-001 §9.2); la columna `Currency` es un artefacto de
persistencia que refleja `expense.Amount.Currency`.

Los tipos de columna exactos los genera EF Core; el
implementador verifica el resultado ejecutando
`dotnet ef migrations script` o inspeccionando el `Up` de la
migración generada.

---

## 10. Configuración técnica

### 10.1 — Herramienta `dotnet-ef`

- Ya instalada localmente en `.config/dotnet-tools.json`
  (F-002 de Fase 2), pinneada a 8.0.x alineada con EF Core
  8.0.11. F-002 **no** la reinstala ni la modifica.
- Si la herramienta no se detecta: ejecutar `dotnet tool
  restore` desde la raíz antes del comando de migración.

### 10.2 — Comando de generación

- `dotnet ef migrations add AddExpenseTable --project
  src/ControlDeFinanzasIA.Infrastructure --startup-project
  src/ControlDeFinanzasIA.Infrastructure` (espejo del comando
  de F-002 de Fase 2, confirmado en `phase-03/features.md`
  F-002).
- El `--startup-project` es `Infrastructure` (mismo que para
  `InitialSchema`). El implementador confirma que sigue
  funcionando tras el composition root de F-005 de Fase 2; si
  por el movimiento del composition root el scaffolding
  requiriese `--startup-project src/ControlDeFinanzasIA.UI`, se
  documenta en el apply y se usa ese. La diferencia no afecta
  al schema generado.

### 10.3 — Edición del `AppDbContext`

- Añadir `public DbSet<Expense> Expenses => Set<Expense>();`
  tras los `DbSet` de `Categories` y `PaymentMethods`.
- Añadir `using ControlDeFinanzasIA.Domain.Transactions;` si no
  está presente.
- No tocar `OnModelCreating` (la configuración se descubre vía
  `ApplyConfigurationsFromAssembly`).

### 10.4 — Convenciones de código

- File-scoped namespace:
  `namespace ControlDeFinanzasIA.Infrastructure.Persistence.Configurations;`
  en `ExpenseConfiguration.cs`.
- `using` al tope (nunca después del namespace).
- XML doc en la clase y en `Configure`.
- `sealed` en `ExpenseConfiguration`.
- Nombres en inglés en código.

---

## 11. Validaciones — tests de integración de forma del schema

F-002 añade tests de integración que verifican la **forma del
schema** que la configuración y la migración producen. Se
diferencian de los tests CRUD del repositorio (que son F-003):
éstos validan que la tabla existe, que las columnas tienen los
tipos y restricciones esperadas, y que `Money` se descompone en
dos columnas. Reutilizan `TestAppDbContextFactory` de F-006 de
Fase 2 (SQLite in-memory compartida); **no** se inventa una
factoría nueva.

Ubicación:
`tests/ControlDeFinanzasIA.Tests.Integration/Persistence/ExpenseSchemaTests.cs`.

| # | Test | Verifica |
| --- | --- | --- |
| ST-1 | `Migrate_CreatesExpensesTable` | Tras `MigrateAsync`, la tabla `Expenses` existe en `sqlite_master` con las 9 columnas esperadas (`Id`, `CategoryId`, `Date`, `Amount`, `Currency`, `Description`, `Note`, `CreatedAt`, `UpdatedAt`). |
| ST-2 | `ExpenseConfiguration_HasExpectedColumnTypesAndConstraints` | `Description` es NOT NULL con max 200; `Note` es nullable con max 500; `Id` es PK no auto-generada; la FK `CategoryId → Categories.Id` existe con `Restrict`; los índices `IX_Expenses_Date` y `IX_Expenses_CategoryId` existen. Verificación vía inspección del modelo (`AppDbContext.Model`) o del `Up` de la migración. |
| ST-3 | `Money_OwnsOne_ProducesTwoColumns` | Las columnas `Amount` y `Currency` existen ambas en `Expenses` (no hay una columna `AmountJson` única). `Amount` es NOT NULL; `Currency` es NOT NULL. Confirma la decisión RN-3 / §17.1. |

**Patrón de cada test** (espejo de F-006 de Fase 2 §10.3): cada
test crea `using var factory = new TestAppDbContextFactory()`,
`using var context = factory.CreateContext()`, `await
context.Database.MigrateAsync()`, y luego inspecciona el
modelo o el schema. xUnit `[Fact]` (un test = un escenario).

**Marco y paquetes:** xUnit + FluentAssertions (convención de
Fase 1 §15). `Microsoft.Data.Sqlite` ya está en
`Tests.Integration.csproj` (F-002 de Fase 2). F-002 no añade
paquetes.

> **Nota sobre el split con F-003.** El phase plan F-002 dice
> textualmente "no se añaden tests nuevos en esta feature —
> los de integración vienen en F-003". Esta spec introduce
> **tests de forma del schema** (ST-1 a ST-3) en F-002 por
> dirección explícita del orchestrator, y deja los **tests
> CRUD del repositorio** (AddAsync, GetByFilterAsync,
> paginación, FK validation a nivel Application, etc.) para
> F-003. La separación espor engineering: la forma del schema
> es responsabilidad de quien la produce (F-002); el
> comportamiento del repositorio es responsabilidad de F-003.
> Ver §17.6 y el retorno al orchestrator.

---

## 12. Casos borde

### EC-1 — `Money` como `record` con propiedades readonly y `OwnsOne`

- `Money` (F-002 de Fase 1) es un `record` con propiedades
  `init`/readonly y factory `Money.Create`. EF Core 8 hidrata
  owned types `record` vía el constructor primario o vía
  campos de backing. Si la forma exacta de `Money` (constructor
  privado, propiedades `{ get; }`) impide la hidratación con
  `OwnsOne`, el implementador coordina con F-001 el ajuste
  mínimo (p. ej., exponer el constructor de `Money` como
  `internal`, o cambiar `{ get; }` a `{ get; init; }`). Es un
  riesgo acotado ya surfaceado por F-001 §17.5; no rompe la
  decisión `OwnsOne` (ver §17.5 y §18).

### EC-2 — `DateOnly` en SQLite

- EF Core 8 mapea `DateOnly` nativamente a `TEXT` ISO-8601 en
  SQLite. Sin `ValueConverter`. Si surgen edge cases (p. ej.,
  redondeo de componente horario), el implementador lo
  confirma vía Context7 y, sólo si es necesario, introduce un
  `ValueConverter<DateOnly, string>`. F-001 §9.3 ya fijó la
  decisión; F-002 la consume.

### EC-3 — `DateTime.Kind` no persistido

- `CreatedAt` / `UpdatedAt` se mapean a `TEXT` sin persistir el
  `Kind`. El caller (F-004 use case) es responsable de pasar
  `DateTimeKind.Utc` (F-001 INV-7). F-002 no forza ni valida
  el `Kind` en la DB.

### EC-4 — Aplicar `AddExpenseTable` sobre una DB con `InitialSchema`

- La migración es aditiva: crea `Expenses` sin tocar
  `Categories` / `PaymentMethods`. Las 9 categorías y 5 métodos
  sembrados siguen tras el arranque. Verificable por AC-5 y
  CU-4.

### EC-5 — La FK `Restrict` y el borrado de categorías

- `Restrict` bloquea en DB el borrado de una `Category` con
  gastos. Si un test o la app intenta `RemoveAsync` sobre una
  categoría con gastos, SQLite lanza una excepción de
  constraint. El manejo a nivel de Application (mensaje
  "CategoryInUse") es de Fase 4; F-002 sólo declara la
  restricción estructural.

### EC-6 — El `--startup-project` tras F-005 de Fase 2

- F-005 de Fase 2 movió el composition root a `UI` (aplica
  migraciones en runtime). El scaffolding de migraciones
  (`dotnet ef migrations add`) usa `--startup-project
  Infrastructure` (mismo que `InitialSchema`). Si por el
  movimiento del composition root el scaffolding requiriese
  `UI`, el implementador lo detecta al ejecutar el comando y
  documenta el cambio. No afecta al schema.

### EC-7 — El snapshot diverge de la configuración

- Si alguien edita `ExpenseConfiguration` sin regenerar la
  migración, el snapshot queda desincronizado. Mitigación:
  cualquier cambio en la configuración va acompañado de una
  migración nueva. F-002 genera `AddExpenseTable`; las
  siguientes son de features futuras.

### EC-8 — `Migrations/` commiteado sin los 3 archivos

- Si se commitea sólo el `.cs` y no el `Designer.cs` o el
  `ModelSnapshot.cs`, EF Core no puede aplicar la migración.
  AC-3 exige los 3 archivos; el PR review verifica.

---

## 13. Dependencias

### 13.1 Prerrequisitos de implementación

- **F-001 de Fase 3 (archivada):** provee la entidad `Expense`
  en `Domain/Transactions/Expense.cs` con sus 8 propiedades,
  factory `Create`, constructor privado parameterless para EF
  Core, y las invariantes INV-1 a INV-8. Es el contrato de
  entrada de F-002.
- **F-002 de Fase 2 (completada):** provee `AppDbContext` con
  `ApplyConfigurationsFromAssembly`, las configuraciones
  `CategoryConfiguration` / `PaymentMethodConfiguration`
  (patrón a replicar), la migración `InitialSchema` (crea
  `Categories` y `PaymentMethods` — la FK de `Expenses`
  apunta a `Categories.Id`), `dotnet-ef` 8.0.x local, y la
  convención PascalCase + `ValueGeneratedNever`.
- **F-006 de Fase 2 (completada):** provee
  `TestAppDbContextFactory` que los tests de schema reutilizan.
- **F-002 de Fase 1 (completada):** provee el value object
  `Money` y el enum `Currency` que F-002 mapea con `OwnsOne`.
- **F-007 de Fase 1 (completada):** los tests NetArchTest R-1
  a R-5 que F-002 no debe romper.

### 13.2 Dependencias con otras features (Fase 3)

| Feature | Relación | Notas |
| --- | --- | --- |
| F-001 (Fase 3) | Consumidora-inversa | Provee `Expense` (forma de la entidad). F-002 la mapea. Si la forma dificulta el mapeo (EC-1), F-002 coordina el ajuste con F-001. |
| F-003 (Fase 3) | Consumidora | Implementa `ExpenseRepository` sobre la tabla `Expenses` que F-002 crea. Los tests CRUD del repo son de F-003; los de forma del schema son de F-002 (§11). |
| F-004 (Fase 3) | Consumidora | `RegisterExpenseUseCase` construye `Expense` y lo persiste vía F-003 sobre la tabla de F-002. |
| F-005 (Fase 3) | Consumidora | `GetTransactionsQuery` consulta `Expenses` (usa el índice `IX_Expenses_Date`). |
| F-006 (Fase 3) | Independiente | UI shell no toca persistencia. |
| F-007 (Fase 3) | Consumidora (vía DTO) | La vista consume `ExpenseDto`, no la entidad ni la tabla directamente. |

### 13.3 Bloqueos

- **Ninguno.** F-002 puede implementarse inmediatamente. F-001
  de Fase 3 está archivada; F-002 de Fase 2 y F-006 de Fase 2
  están completas.

---

## 14. Fuera de alcance (detallado)

Repetición con énfasis. F-002 **no** hace lo siguiente:

- Implementar `ExpenseRepository` ni extender
  `IExpenseRepository` con `GetByFilterAsync` (F-003).
- Implementar use cases, validadores, DTOs, `ExpenseExtensions`
  (F-004, F-005).
- Implementar UI, ViewModels, vistas, navegación, DataTemplates
  (F-006, F-007).
- Añadir seed data de `Expense` (dato de usuario; el seed de
  sistema es F-003 de Fase 2).
- Validar cross-aggregate que `CategoryId` exista antes de
  persistir (F-003 repo, F-004 validador).
- Añadir CHECK constraints de DB que re-validen invariantes de
  F-001 (duplicación de lógica; `mission.md`).
- Añadir tests CRUD del repositorio (F-003). F-002 sólo testa
  la forma del schema.
- Añadir soft-delete, `CreatedBy`/`UpdatedBy`, `Version`
  (rechazados en v1 por F-001).
- Modificar `Domain` o `Application` (F-002 sólo toca
  `Infrastructure/Persistence/` y `Tests.Integration`).
- Modificar `OnModelCreating` (la configuración se descubre
  automáticamente; sólo se añade el `DbSet`).
- Añadir paquetes NuGet a ningún proyecto (todo lo necesario
  está desde Fase 2).
- Modificar el CI workflow.

---

## 15. Criterios de aceptación

La feature se considera completada cuando **todos** los
siguientes puntos se cumplen, verificables mediante tests
automatizados y revisión de código.

### AC-1 — `ExpenseConfiguration` creada

- [ ] `src/ControlDeFinanzasIA.Infrastructure/Persistence/Configurations/ExpenseConfiguration.cs`
      existe.
- [ ] `public sealed class ExpenseConfiguration :
      IEntityTypeConfiguration<Expense>`.
- [ ] `b.ToTable("Expenses")` (PascalCase).
- [ ] `Id`: primary key, `ValueGeneratedNever`.
- [ ] `CategoryId`: `IsRequired`, FK a `Categories.Id` con
      `OnDelete(Restrict)`, sin navegación (`HasOne<Category>()`).
- [ ] `Date`: `IsRequired` (`DateOnly` nativo a `TEXT`).
- [ ] `Description`: `IsRequired`, max 200.
- [ ] `Note`: max 500, nullable (sin `IsRequired`).
- [ ] `CreatedAt`, `UpdatedAt`: `IsRequired`.
- [ ] `OwnsOne(e => e.Amount)` con columnas `Amount`
      (`IsRequired`, `HasPrecision(18, 4)`) y `Currency`
      (`IsRequired`).
- [ ] Índices `HasIndex(e => e.Date)` y
      `HasIndex(e => e.CategoryId)`.
- [ ] File-scoped namespace, `sealed`, XML doc, `using` al tope.

### AC-2 — `AppDbContext` actualizado

- [ ] `AppDbContext.cs` expone
      `public DbSet<Expense> Expenses => Set<Expense>();`.
- [ ] El `DbSet` está **tras** los de `Categories` y
      `PaymentMethods`.
- [ ] `using ControlDeFinanzasIA.Domain.Transactions;` presente.
- [ ] `OnModelCreating` **no** se modificó (la configuración se
      descubre vía `ApplyConfigurationsFromAssembly`).

### AC-3 — Migración `AddExpenseTable` generada

- [ ] El comando `dotnet ef migrations add AddExpenseTable
      --project src/ControlDeFinanzasIA.Infrastructure
      --startup-project src/ControlDeFinanzasIA.Infrastructure`
      se ejecutó sin error.
- [ ] Los 3 archivos existen en
      `Infrastructure/Persistence/Migrations/`:
      `YYYYMMDDhhmmss_AddExpenseTable.cs`,
      `YYYYMMDDhhmmss_AddExpenseTable.Designer.cs`,
      `AppDbContextModelSnapshot.cs` (actualizado).
- [ ] El `Up` crea la tabla `Expenses` con las 9 columnas, los
      índices `IX_Expenses_Date` y `IX_Expenses_CategoryId`, y
      la FK a `Categories` con `Restrict`.
- [ ] El `Up` **no** incluye `InsertData`.
- [ ] El `Up` **no** toca `Categories` ni `PaymentMethods`.
- [ ] El `Down` hace `DropTable` de `Expenses`.
- [ ] Los 3 archivos están commiteados.

### AC-4 — Compilación y tests

- [ ] `dotnet build -c Release` desde la raíz: 0 warnings, 0
      errors.
- [ ] `dotnet test` desde la raíz: tests heredados (≈145) + 3
      nuevos de schema ≈ 148 verdes, 0 failures.
- [ ] Los 11 tests de NetArchTest (R-1 a R-5) pasan sin
      modificaciones.
- [ ] `dotnet list src/ControlDeFinanzasIA.Domain package`
      sigue vacío (R-1).
- [ ] `dotnet format style --verify-no-changes`: exit 0.

### AC-5 — Tests de integración de schema

- [ ] `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/ExpenseSchemaTests.cs`
      existe.
- [ ] `Migrate_CreatesExpensesTable` pasa (tabla `Expenses` con
      9 columnas).
- [ ] `ExpenseConfiguration_HasExpectedColumnTypesAndConstraints`
      pasa (max length, nullability, FK `Restrict`, índices).
- [ ] `Money_OwnsOne_ProducesTwoColumns` pasa (columnas
      `Amount` y `Currency` ambas presentes, NOT NULL).
- [ ] Los tests reutilizan `TestAppDbContextFactory` (F-006 de
      Fase 2); no se inventa una factoría nueva.

### AC-6 — Migración aditiva verificada

- [ ] Tras aplicar `AddExpenseTable` sobre una DB con
      `InitialSchema` (via `MigrateAsync` en un test o
      arranque), las tablas `Categories` y `PaymentMethods`
      siguen existiendo y las 9 categorías + 5 métodos
      sembrados siguen presentes (no se pierden datos).

### AC-7 — Sin scope creep

- [ ] NO se ha implementado `ExpenseRepository` (F-003).
- [ ] NO se ha extendido `IExpenseRepository` (F-003).
- [ ] NO se han añadido use cases, validadores, DTOs (F-004,
      F-005).
- [ ] NO se ha añadido UI (F-006, F-007).
- [ ] NO se ha añadido seed de `Expense` ni `InsertData` en la
      migración.
- [ ] NO se han añadido CHECK constraints de DB que re-validen
      invariantes de F-001.
- [ ] NO se han añadido tests CRUD del repositorio (F-003).
- [ ] NO se ha modificado `OnModelCreating`.
- [ ] NO se ha modificado `Domain` ni `Application`.
- [ ] NO se han añadido paquetes NuGet a ningún proyecto.

### AC-8 — Documentación y trazabilidad

- [ ] `spec/phases/phase-03/features.md` actualizado: F-002
      marcada como ✅ COMPLETADA en la tabla resumen y en el
      header de la sección F-002 (tras apply + verify).
- [ ] La spec se renombra a
      `spec/features/phase-03/F-002-expense-configuration-migration.done.md`
      tras apply + verify.
- [ ] Cada AC y cada RN traza a su sección fuente en §19.

---

## 16. Estructura de archivos esperada

| Archivo | Acción | Contenido |
| --- | --- | --- |
| `src/ControlDeFinanzasIA.Infrastructure/Persistence/Configurations/ExpenseConfiguration.cs` | Crear | `ExpenseConfiguration : IEntityTypeConfiguration<Expense>` con mapeo completo (RF-1, RF-2). |
| `src/ControlDeFinanzasIA.Infrastructure/Persistence/AppDbContext.cs` | Editar | Añadir `DbSet<Expense> Expenses => Set<Expense>();` + `using` de `Domain.Transactions` (RF-3). No tocar `OnModelCreating`. |
| `src/ControlDeFinanzasIA.Infrastructure/Persistence/Migrations/YYYYMMDDhhmmss_AddExpenseTable.cs` | Crear (generado por `dotnet ef`) | `Up` crea `Expenses` + índices + FK; `Down` borra `Expenses`. |
| `src/ControlDeFinanzasIA.Infrastructure/Persistence/Migrations/YYYYMMDDhhmmss_AddExpenseTable.Designer.cs` | Crear (generado) | Metadata de la migración. |
| `src/ControlDeFinanzasIA.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs` | Editar (generado) | Snapshot actualizado con `Expenses`. |
| `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/ExpenseSchemaTests.cs` | Crear | 3 tests de forma del schema (ST-1 a ST-3) reutilizando `TestAppDbContextFactory`. |

**Modificaciones a `.csproj`:** ninguna. F-002 no añade ni
quita paquetes ni referencias; los de Fase 2 son suficientes.

**Modificaciones al CI workflow:** ninguna.

**Modificaciones a `Domain` / `Application` / `UI`:** ninguna
(salvo el ajuste mínimo de EC-1 si `Money` requiere exponer un
constructor `internal` para `OwnsOne` — se documenta en el
apply si ocurre).

---

## 17. Decisiones de diseño explícitas y decisiones pendientes

### 17.1 — `Money` como `OwnsOne` en dos columnas primitivas (la decisión que F-002 debía resolver)

**Decisión:** `OwnsOne(e => e.Amount)` produciendo dos columnas
primitivas: `Amount` (decimal, `HasPrecision(18, 4)`) y
`Currency` (int). **No** `ValueConverter<Money, string>` a una
columna `AmountJson`.

**Justificación:**

1. **Recomendación explícita del phase plan F-002:** "Opción A
   (recomendada): `OwnsOne(e => e.Amount)` con dos columnas
   primitivas `Amount` (decimal) + `Currency` (int). Allows
   indexing and querying by amount."
2. **`architecture.md` §8:** "Conversores de value objects...
   para mapear a **columnas primitivas**" (plural). `OwnsOne`
   produce 2 columnas primitivas; `ValueConverter` a string
   produce 1 columna serializada. La intención de §8 (columnas
   primitivas, plural) la satisface `OwnsOne`.
3. **`architecture.md` §8:** "Índices: por fecha, por
   categoría, por tag, por recurrente origen." La
   indexabilidad es first-class; `OwnsOne` preserva la
   consultabilidad por monto; `ValueConverter` a string la
   destruye.
4. **Replicación futura:** `RecurringRule`, `Debt`, `Goal`,
   `Budget`, `Income` todos tienen `Amount: Money`. Fijar
   `OwnsOne` ahora evita retrofit cuando haya 6 entidades.
5. **Riesgo acotado:** la hidratación de un `record` con
   propiedades readonly vía `OwnsOne` (EC-1) es un concern de
   implementación ya surfaceado por F-001 §17.5, no un
   bloqueo estructural.

**Alternativa descartada (Option B):** `ValueConverter<Money,
string>` a una columna `AmountJson` serializando `"1200.50|DOP"`.
Más simple pero no indexable, no consultable por monto, y
rompe la regla de §8 de "columnas primitivas". Descartada.

**Convención gap surfaceada:** `architecture.md` §8 usa el
término "Conversores" (que podría leerse como `ValueConverter`
estricto), pero la frase "para mapear a columnas primitivas"
(plural) y la mención de índices indican que la intención es
la descomposición en columnas, no la serialización. `OwnsOne`
es el mecanismo que cumple la intención; técnicamente no es un
`ValueConverter` sino un owned entity. No es una contradicción
duro, es una terminología laxa en §8 que esta spec aclara.

### 17.2 — `ValueGeneratedNever` en `Id`

**Decisión:** `b.Property(e => e.Id).ValueGeneratedNever()`.

**Justificación:** el `Guid` lo genera `Expense.Create` vía
`Guid.NewGuid()` (F-001 §9.1, §17.10). Eco de RN-4 de F-002 de
Fase 2. Permite que el dominio controle la identidad y que los
tests predefinan/lean IDs.

### 17.3 — Tabla `Expenses` en PascalCase

**Decisión:** `b.ToTable("Expenses")`.

**Justificación:** `architecture.md` §8: "PascalCase salvo
decisión explícita". Coherente con `Categories` /
`PaymentMethods`. Eco de RN-2 de F-002 de Fase 2.

### 17.4 — Sin `InsertData` en la migración

**Decisión:** `AddExpenseTable` es DDL puro.

**Justificación:** `Expense` es dato de usuario, no dato del
sistema. El seed de sistema (categorías, métodos) es de F-003
de Fase 2 y no se reproduce. Eco de RN-5 de F-002 de Fase 2.

### 17.5 — Riesgo de hidratación de `Money` (surfaceado, no resuelto en spec)

**Decisión:** F-002 adopta `OwnsOne`. Si EF Core 8 no puede
hidratar `Money` (record con propiedades `{ get; }` y
constructor privado) vía `OwnsOne`, el implementador coordina
con F-001 el ajuste mínimo (exponer constructor de `Money`
como `internal`, o cambiar `{ get; }` a `{ get; init; }`).
F-001 §17.5 ya anticipó este caso.

**Decisión pendiente (DP-6):** si el reviewer prefiere que
F-002 documente el cambio de `Money` como parte de su scope
(en vez de coordinarlo con F-001), se debate antes del apply.
Default: F-002 no modifica `Money`; si la hidratación lo
requiere, se hace un ajuste mínimo coordinado y documentado en
el apply.

### 17.6 — Tests de schema en F-002 vs phase plan

**Decisión:** F-002 incluye 3 tests de forma del schema
(ST-1 a ST-3). Los tests CRUD del repositorio son F-003.

**Justificación:** el phase plan F-002 dice "no se añaden
tests nuevos en esta feature — los de integración vienen en
F-003". Esta spec, por dirección explícita del orchestrator,
introduce los tests de **forma del schema** en F-002 (quien
produce el schema es quien lo testa) y deja los tests de
**comportamiento del repositorio** en F-003. La separación
mejora la responsabilidad y evita acoplar la validación del
schema a la implementación del repo. **Contradicción con el
phase plan surfaceada al orchestrator** (ver retorno).

### 17.7 — Decisiones pendientes (defaults propuestos)

| ID | Decisión | Default propuesto | Alternativas |
| --- | --- | --- | --- |
| DP-1 | `OnDelete` de la FK a `Categories` | `Restrict` (RN-6). Bloquea borrar categoría con gastos; el comportamiento de Application es Fase 4. | `Cascade` (borra gastos al borrar categoría — peligroso, pierde datos del usuario), `SetNull` (requiere `CategoryId` nullable — rompe INV-6). |
| DP-2 | Índice en `Date` | Single column `IX_Expenses_Date` (RF-1). Suficiente para queries por rango de fecha de F-005. | Composite `Date`+`CategoryId` (diferido a Fase 4 cuando los filtros combinen fecha+categoría). |
| DP-3 | Índice / FTS en `Description` | Ninguno. La búsqueda por texto libre es concern de Fase 8+ (`vision.md` §4.3: "Sin búsqueda por texto libre en v1"). | FTS5 de SQLite (Fase 8+ si se añade búsqueda). |
| DP-4 | Timestamp de la migración (UTC vs local) | UTC, por consistencia con `CreatedAt`/`UpdatedAt`. `dotnet ef` asigna el timestamp; el implementador confirma vía Context7/EF Core docs si la herramienta usa UTC (esperado) o local. El timestamp es una clave de orden, no un valor de auditoría. | Local (si la herramienta lo usa y no es configurable; aceptar y documentar). |
| DP-5 | Precisión/escala de `Amount` | `HasPrecision(18, 4)` (18 dígitos totales, 4 decimales) por recomendación del orchestrator. Soporta hasta 14 dígitos enteros + 4 decimales; holgura para monedas de 3 decimales futuras. | `HasPrecision(18, 2)` (estricto 2 decimales, estándar financiero para DOP/USD de 2 decimales). |
| DP-6 | Ajuste de `Money` para hidratación EF Core | F-002 no modifica `Money`; si `OwnsOne` no hidrata, ajuste mínimo coordinado con F-001 (§17.5). | F-002 asume el cambio de `Money` en su scope. |

Si el usuario no responde antes del apply, se aplican los
defaults.

---

## 18. Riesgos y mitigaciones

| Riesgo | Probabilidad | Impacto | Mitigación |
| --- | --- | --- | --- |
| `OwnsOne` no hidrata `Money` (record con props readonly y constructor privado). | Media | Medio | EC-1 / §17.5: ajuste mínimo coordinado con F-001 (constructor `internal` o `init`). Ya surfaceado por F-001 §17.5. |
| `DateOnly` no se mapea nativamente en EF Core 8 a SQLite. | Baja | Medio | F-001 §9.3 fijó la decisión; EF Core 8 soporta `DateOnly` nativo. Verificar vía Context7 al implementar; si edge case, introducir `ValueConverter<DateOnly, string>`. |
| `DateTime.Kind` no persistido → inconsistencia de auditoría. | Baja | Bajo | Contrato del caller (F-004 usa `IClock.UtcNow`). F-002 no valida el `Kind` (eco F-001 INV-7). |
| La migración incluye `InsertData` por error. | Baja | Bajo | `Expense` no tiene seed; el snapshot no incluye `HasData` para `Expense`. Si lo incluye, el implementador lo elimina del `Up`. AC-3 verifica. |
| `Migrations/` commiteado sin los 3 archivos. | Baja | Alto | AC-3 exige los 3; PR review verifica. |
| El `--startup-project` cambia tras F-005 de Fase 2 y el scaffolding falla. | Baja | Bajo | EC-6: el implementador detecta al ejecutar el comando y documenta el `--startup-project` correcto. No afecta al schema. |
| `OnDelete(Restrict)` rompe tests que borran categorías con gastos. | Baja | Bajo | EC-5: el comportamiento de borrado a nivel Application es Fase 4. Los tests de F-002 no borran categorías; los de F-003/F-004 manejan el constraint. |
| El patrón `OwnsOne` se replica mal en entidades futuras. | Media | Medio | Esta spec fija el patrón con `HasColumnName("Amount")`/`HasColumnName("Currency")` explícitos para evitar nombres como `Amount_Amount`. §17.1 lo documenta. |
| El snapshot diverge de la configuración por edición sin regenerar migración. | Baja | Bajo | EC-7: cualquier cambio en la configuración va con migración nueva. |
| `dotnet format style` propone cambios en archivos generados por `dotnet ef`. | Baja | Bajo | Aplicar los cambios propuestos salvo en `Designer.cs`/`ModelSnapshot.cs` (regenerados). |
| CHECK constraints de invariantes se añaden por error (scope creep). | Baja | Medio | RN-7 y §17 lo prohíben explícitamente; AC-7 verifica. |

---

## 19. Trazabilidad

Tabla que mapea cada RN, AC y decisión a la sección de la
fuente de verdad que la justifica.

| Ítem | Tipo | Fuente de verdad | Sección / referencia |
| --- | --- | --- | --- |
| RN-1 | Configs en `Infrastructure` | `architecture.md` §8; R-1 F-007 | `architecture.md` §8; `spec/features/phase-01/F-007-validacion-arquitectonica.done.md` §5 R-1 |
| RN-2 | Tabla PascalCase `Expenses` | `architecture.md` §8 | `architecture.md` §8 ("PascalCase salvo decisión explícita") |
| RN-3 | `Money` como `OwnsOne` (2 columnas) | Phase plan F-002 + `architecture.md` §8 | `spec/phases/phase-03/features.md` F-002 (Opción A recomendada); `architecture.md` §8 ("columnas primitivas", índices) |
| RN-4 | `ValueGeneratedNever` | F-001 Fase 3 + F-002 Fase 2 RN-4 | `spec/features/phase-03/F-001-expense-domain-entity.done.md` §9.1, §17.10; `spec/features/phase-02/F-002-entity-configurations-migration.done.md` §8 RN-4 |
| RN-5 | Sin `InsertData` | F-002 Fase 2 RN-5 + phase plan F-002 | `spec/features/phase-02/F-002-entity-configurations-migration.done.md` §8 RN-5; `vision.md` §4.10 (datos del usuario) |
| RN-6 | FK `Restrict` | Phase plan F-002 + `vision.md` §4.2 | `spec/phases/phase-03/features.md` F-002 (`OnDelete(ReferentialAction.Restrict)`); `vision.md` §4.2 (categoría obligatoria) |
| RN-7 | Persistencia no re-valida invariantes | `mission.md` + F-001 INV-1..INV-8 | `spec/constitution/mission.md` ("Evitar duplicación de lógica"); `spec/features/phase-03/F-001-expense-domain-entity.done.md` §8 |
| RF-1 | `ExpenseConfiguration` mapeo completo | F-001 (entidad) + F-002 Fase 2 (patrón) | `spec/features/phase-03/F-001-expense-domain-entity.done.md` §6 RF-2 (8 propiedades); `spec/features/phase-02/F-002-entity-configurations-migration.done.md` §6 RF-1/RF-2 |
| RF-2 | `OwnsOne` de `Money` | Decisión §17.1 | §17.1 de esta spec; `spec/features/phase-01/F-002-money-value-object.done.md` (value object `Money`) |
| RF-3 | `DbSet<Expense>` | `architecture.md` §8 | `architecture.md` §8 ("un `DbSet<>` por entidad de dominio") |
| RF-4/RF-5 | Migración `AddExpenseTable` | F-002 Fase 2 (patrón) + phase plan F-002 | `spec/features/phase-02/F-002-entity-configurations-migration.done.md` §6 RF-5/RF-6; `spec/phases/phase-03/features.md` F-002 |
| RNF-1 | NetArchTest R-1 a R-5 | F-007 Fase 1 | `spec/features/phase-01/F-007-validacion-arquitectonica.done.md` §5 |
| AC-1 | `ExpenseConfiguration` creada | F-001 (contrato) + F-002 Fase 2 (patrón) | `spec/features/phase-03/F-001-expense-domain-entity.done.md` §6; `spec/features/phase-02/F-002-entity-configurations-migration.done.md` §15 AC-1 |
| AC-3 | Migración generada (3 archivos) | F-002 Fase 2 AC-2 | `spec/features/phase-02/F-002-entity-configurations-migration.done.md` §15 AC-2 |
| AC-5 | Tests de schema | F-006 Fase 2 (`TestAppDbContextFactory`) + orchestrator | `spec/features/phase-02/F-006-integration-tests.done.md` RF-1; dirección orchestrator (split F-002/F-003) |
| AC-6 | Migración aditiva no pierde datos | `architecture.md` §8 + F-005 Fase 2 | `architecture.md` §8 (migraciones versionadas); `spec/phases/phase-03/features.md` F-002 (criterio de cierre) |
| §17.1 | `OwnsOne` vs `ValueConverter` | Phase plan F-002 + `architecture.md` §8 | `spec/phases/phase-03/features.md` F-002 (Opción A); `architecture.md` §8 |
| §17.6 | Tests de schema en F-002 | Orchestrator + phase plan F-002 | Dirección orchestrator; `spec/phases/phase-03/features.md` F-002 ("los de integración vienen en F-003") — contradicción surfaceada |
| DP-1 | `OnDelete` default `Restrict` | Phase plan F-002 | `spec/phases/phase-03/features.md` F-002 |
| DP-5 | `HasPrecision(18, 4)` | Orchestrator | Dirección orchestrator (recomendación `HasPrecision(18, 4)`) |

---

## 20. Referencias

- `docs/vision.md` §4.1 (Gastos del día a día), §4.2
  (Organización de un movimiento — fecha, monto+moneda,
  categoría, nota opcional), §4.3 ("Sin búsqueda por texto
  libre en v1" — justifica DP-3), §4.10 (Datos del usuario —
  justifica que `Expense` no es seed).
- `docs/architecture.md` §3 (solución y proyectos), §4 (grafo
  de dependencias, R-1 a R-5), §8 (Infrastructure:
  `IEntityTypeConfiguration<T>`, migraciones versionadas,
  value objects a columnas primitivas, PascalCase, índices por
  fecha y categoría), §12 (ruta del archivo de datos),
  §19 (ADR-007 `IClock`).
- `docs/roadmap.md` §Fase 3 (Entregables esperados:
  persistencia de `Expense`; Riesgos: conversión de value
  objects en EF Core).
- `spec/constitution/mission.md` — Clean Architecture, "Evitar
  duplicación de lógica" (justifica RN-7: no CHECK constraints
  de invariantes).
- `spec/constitution/tech-stack.md` — EF Core + SQLite,
  migraciones versionadas y commiteadas, convenciones de
  código.
- `spec/phases/phase-03/features.md` — F-002 dentro del
  desglose de la fase 3 (objetivo, descripción breve con
  `OwnsOne` recomendado, criterios de cierre).
- `spec/features/phase-03/F-001-expense-domain-entity.done.md`
  — la entidad `Expense` que esta feature mapea (8 propiedades,
  invariantes INV-1 a INV-8, `ValueGeneratedNever`, `DateOnly`,
  `Money` sin `Currency` separada, constructor privado para EF
  Core, §17.5 riesgo de hidratación).
- `spec/features/phase-02/F-002-entity-configurations-migration.done.md`
  — patrón estructural que esta spec espeja
  (`IEntityTypeConfiguration<T>` por archivo, PascalCase,
  `ValueGeneratedNever`, migración con 3 archivos, sin
  `InsertData`, `dotnet-ef` local, tabla de columnas).
- `spec/features/phase-02/F-006-integration-tests.done.md` —
  provee `TestAppDbContextFactory` (SQLite in-memory
  compartida) que los tests de schema de F-002 reutilizan.
- `spec/features/phase-02/F-003-seed-data.done.md` — patrón
  `SeedData` (sistema, no usuario); F-002 no colisiona porque
  `Expense` no es seed.
- `spec/features/phase-01/F-002-money-value-object.done.md` —
  el value object `Money` (`Amount` decimal + `Currency` enum)
  que F-002 mapea con `OwnsOne`.
- `spec/features/phase-01/F-007-validacion-arquitectonica.done.md`
  — los tests NetArchTest R-1 a R-5 que F-002 no debe romper.
- `spec/README.md` — convención del directorio `spec`
  (numeración local por fase, sufijos `.spec.md` → `.done.md`).
- EF Core 8 documentation oficial (a consultar vía Context7
  al implementar: `OwnsOne` con `record`, `DateOnly` nativo en
  SQLite, `HasPrecision` en SQLite, `OnDelete(Restrict)`).
- `dotnet ef` CLI documentation (a consultar vía Context7 para
  confirmar el timestamp UTC/local de la migración — DP-4).
