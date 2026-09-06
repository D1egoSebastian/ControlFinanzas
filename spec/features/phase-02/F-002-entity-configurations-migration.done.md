# Specification — F-002: Configuraciones de Entidades y Migración Inicial

> **Fase:** 2 — Persistencia con EF Core + SQLite
> **Feature:** F-002 (local a esta fase)
> **Estado:** ✅ COMPLETADA
> **Spec ID:** SPEC-F002P2
>
> **Nota de numeración:** este `F-002` es el de la **Fase 2**
> (`spec/phases/phase-02/features.md`). No debe confundirse con
> el `F-002` de Fase 1 (Money value object en
> `spec/phases/phase-01/features.md`). Los IDs son locales a cada
> fase; el folder los desambigua. Ver `spec/README.md` para la
> convención.
>
> Fuentes de verdad: `docs/vision.md`, `docs/architecture.md`,
> `docs/roadmap.md` (Fase 2), `spec/constitution/tech-stack.md`,
> `spec/constitution/mission.md`,
> `spec/phases/phase-02/features.md` (F-002 dentro del
> desglose de la fase 2),
> `spec/features/phase-02/F-001-appdbcontext-addpersistence.done.md`
> (provee `AppDbContext` que esta feature extiende).

---

## 1. Resumen

F-002 define el **mapeo entre las entidades de Domain y el
schema de SQLite** mediante configuraciones Fluent API de EF
Core, y genera la **primera migración versionada**
(`InitialSchema`) que crea las tablas, columnas e índices
únicos para `Category` y `PaymentMethod`.

Al final de la feature:
- `CategoryConfiguration` y `PaymentMethodConfiguration`
  existen en `Infrastructure/Persistence/Configurations/`
  y se aplican automáticamente en
  `AppDbContext.OnModelCreating` (que ya llama a
  `ApplyConfigurationsFromAssembly` desde F-001).
- La migración `InitialSchema` está generada y commiteada
  en `Infrastructure/Persistence/Migrations/` con sus 3
  archivos (la migración, su `Designer`, y el
  `ModelSnapshot`).
- `dotnet build` y `dotnet test` (los 111 tests existentes)
  siguen pasando en limpio.
- Los 11 tests de NetArchTest de F-007 (R-1 a R-5) siguen
  pasando: F-002 no rompe la arquitectura.
- La herramienta `dotnet ef` está instalada **localmente**
  (en `.config/dotnet-tools.json`), no globalmente.

---

## 2. Contexto

F-001 de Fase 2 introdujo el `AppDbContext`, los paquetes
EF Core, el `EfUnitOfWork`, y la extension method
`AddPersistence`. Pero el `AppDbContext` no tiene todavía
ningún mapeo entidad-tabla: si se ejecutara
`Database.Migrate()` ahora mismo, EF Core crearía
columnas con defaults (Name sin max length, etc.), que
no coinciden con la spec de `docs/architecture.md` §8 ni
con las necesidades del producto.

F-002 llena ese hueco. Define **explícitamente** cómo se
mapea cada entidad (PascalCase table, GUID no generado por
DB, Name único con collation NOCASE, etc.) y emite la
primera migración que materializa ese schema en SQLite.

**Por qué Fluent API en archivos separados:**
`docs/architecture.md` §8 lo fija: "Mapeo vía clases
`IEntityTypeConfiguration<T>` en
`Persistence/Configurations/`. Las entidades de dominio
quedan limpias de atributos de EF." Esto es Clean
Architecture: el dominio no conoce EF.

**Por qué migración inicial versionada:**
`docs/architecture.md` §8 lo fija: "Migraciones
versionadas en `Persistence/Migrations/`, commiteadas."
Esto permite que el schema evolucione con el tiempo sin
perder datos (cada cambio es un commit reproducible).

**Por qué la herramienta `dotnet-ef` local y no global:**
disciplina del proyecto. Cada developer que clone el repo
y ejecute `dotnet tool restore` obtendrá la versión
correcta de `dotnet-ef` (8.0.x) sin instalar nada
globalmente. La versión queda pinneada en
`.config/dotnet-tools.json`.

---

## 3. Objetivo

1. **Crear `CategoryConfiguration`** con el mapeo
   Fluent API completo para `Category`: tabla
   `Categories`, primary key en `Id`, índice único en
   `Name` con collation `NOCASE`, longitudes máximas,
   columnas requeridas.
2. **Crear `PaymentMethodConfiguration`** con el mismo
   patrón pero sin `Color`/`Icon` (campos exclusivos de
   `Category`).
3. **Instalar la herramienta `dotnet-ef`** localmente
   (en `.config/dotnet-tools.json`), pinneada a la
   versión 8.0.x alineada con EF Core 8.0.11.
4. **Generar la primera migración** `InitialSchema` con
   `dotnet ef migrations add InitialSchema`. Los 3
   archivos resultantes se commitean en
   `Infrastructure/Persistence/Migrations/`.
5. **NO incluir `InsertData` en la migración.** El seed
   se aplica vía `SeedData.Apply(modelBuilder)` (stub
   en F-001, completo en F-003). La migración describe
   solo el schema; los datos vienen por separado.

---

## 4. Alcance

### 4.1 En alcance

- Modificación de
  `src/ControlDeFinanzasIA.Infrastructure/`: añadir
  carpeta `Configurations/` con 2 archivos.
- Modificación de la raíz del repo: añadir
  `.config/dotnet-tools.json` con la entrada de
  `dotnet-ef` 8.0.x.
- Adición de
  `src/ControlDeFinanzasIA.Infrastructure/Persistence/Migrations/`
  con los 3 archivos de la primera migración.
- Verificación de que el build y los 111 tests existentes
  siguen pasando.
- Verificación de que el test R-1 de NetArchTest (F-007)
  sigue pasando (`Domain` sigue sin paquetes externos).

### 4.2 Fuera de alcance

- **Implementar el cuerpo de `SeedData.Apply`.** Es
  F-003 de Fase 2. F-001 introdujo `SeedData` como
  stub vacío; F-002 lo deja igual. La migración se
  genera con el stub vacío, por lo que **no** incluye
  `InsertData`.
- **Crear los 15 repositorios concretos.** Es F-004.
- **Wiring del `AddPersistence` en `Bootstrap.ConfigureServices`.**
  Es F-005.
- **Aplicar migraciones al arranque.** Es F-005.
- **`<ProjectReference>` de `UI` a `Infrastructure`.**
  Es F-005.
- **Tests de integración con SQLite in-memory.** Es
  F-006.
- **Conversión de value objects** (`Money`,
  `DateRange`) en EF Core. `Category` y `PaymentMethod`
  no tienen esos campos; el patrón se establece en
  Fase 3 cuando se introduzca `Expense` (con
  `Amount: Money`).
- **Migraciones adicionales.** Solo se genera la
  primera (`InitialSchema`). Las siguientes llegan
  cuando se añadan entidades o se modifiquen las
  existentes.
- **Modificaciones a `Domain`, `Application`, `UI`,
  `Tests.Unit`, `Tests.Integration`.** F-002 solo
  toca `Infrastructure` y la raíz del repo (para
  `.config/dotnet-tools.json`).
- **El CI workflow.** F-002 no requiere cambios en
  CI; el workflow existente (`dotnet build` +
  `dotnet test`) sigue siendo suficiente.

---

## 5. Casos de uso

### CU-1 — `ApplyConfigurationsFromAssembly` carga las configuraciones

- **Actor:** `AppDbContext` (F-001).
- **Trigger:** se construye el `AppDbContext` por
  primera vez (e.g., al arrancar la app o en un test).
- **Precondición:** F-002 creó
  `CategoryConfiguration` y
  `PaymentMethodConfiguration` en
  `Infrastructure/Persistence/Configurations/`.
- **Postcondición:** EF Core aplica las
  configuraciones al construir el modelo. Las
  tablas se llaman `Categories` y `PaymentMethods`
  (PascalCase, no `Categories` ni `category`); los
  índices únicos están en `Name` con collation
  `NOCASE`; las longitudes máximas son las definidas
  en las configuraciones.

### CU-2 — `dotnet ef migrations add InitialSchema` genera los 3 archivos

- **Actor:** developer (o CI, si se decide automatizar).
- **Trigger:** se ejecuta el comando desde la raíz del
  repo.
- **Precondición:** la herramienta `dotnet-ef` está
  instalada localmente (`.config/dotnet-tools.json`
  existe y `dotnet tool restore` ya se ejecutó).
- **Postcondición:**
  - `Infrastructure/Persistence/Migrations/YYYYMMDDhhmmss_InitialSchema.cs`
    (el método `Up` crea las tablas; el método
    `Down` las borra).
  - `Infrastructure/Persistence/Migrations/YYYYMMDDhhmmss_InitialSchema.Designer.cs`
    (metadata de la migración).
  - `Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs`
    (snapshot actual del modelo).
  - El timestamp `YYYYMMDDhhmmss` lo asigna la
    herramienta en el momento de la generación.
  - **El `Up` de la migración contiene solo DDL**
    (CREATE TABLE, CREATE INDEX). **No** contiene
    `InsertData` porque `SeedData.Apply` es un stub
    vacío en F-001/F-002.

### CU-3 — La migración es commiteable y reproducible

- **Actor:** git.
- **Trigger:** `git add` y `git commit` después de
  generar la migración.
- **Precondición:** la migración y su Designer están
  commiteados; el `ModelSnapshot` también.
- **Postcondición:** otro developer que clone el repo
  puede ejecutar `dotnet ef database update` (o, en
  F-005, `db.Database.Migrate()` desde la app) y
  obtiene el mismo schema.

### CU-4 — El build y los tests siguen pasando

- **Actor:** build runner / CI.
- **Trigger:** `dotnet build` y `dotnet test`.
- **Precondición:** F-002 completa.
- **Postcondición:** 0 warnings, 0 errors. Los 111
  tests existentes (100 + 11 de F-007) verdes. Los 11
  tests de NetArchTest (R-1 a R-5) verdes.

---

## 6. Requisitos funcionales

### RF-1 — `CategoryConfiguration`

- `public sealed class CategoryConfiguration :
  IEntityTypeConfiguration<Category>` en
  `src/ControlDeFinanzasIA.Infrastructure/Persistence/Configurations/CategoryConfiguration.cs`.
- Implementa `void Configure(EntityTypeBuilder<Category> b)`:
  - `b.ToTable("Categories")` (PascalCase, coherente
    con el código C# y con `docs/architecture.md` §8
    que fija "PascalCase salvo decisión explícita").
  - `b.HasKey(c => c.Id)`.
  - `b.Property(c => c.Id).ValueGeneratedNever()`
    (Guid generado en Domain, no en DB; permite que
    el dominio controle la identidad y que los tests
    predefinan IDs).
  - `b.Property(c => c.Name).IsRequired().HasMaxLength(100).UseCollation("NOCASE")`
    (SQLite: case-insensitive; "comida" y "Comida"
    no pueden coexistir).
  - `b.HasIndex(c => c.Name).IsUnique()`.
  - `b.Property(c => c.IsSystem).IsRequired()`.
  - `b.Property(c => c.CreatedAt).IsRequired()`.
  - `b.Property(c => c.UpdatedAt).IsRequired()`.
  - `b.Property(c => c.Color).HasMaxLength(9)` (un
    color hex `#RRGGBB` o `#RRGGBBAA` cabe en 9
    caracteres).
  - `b.Property(c => c.Icon).HasMaxLength(50)`.
- File-scoped namespace:
  `namespace ControlDeFinanzasIA.Infrastructure.Persistence.Configurations;`.
- `using` al tope:
  - `using ControlDeFinanzasIA.Domain.Catalog;` (para
    `Category`).
  - `using Microsoft.EntityFrameworkCore;` (para
    `EntityTypeBuilder`, `IEntityTypeConfiguration`).
  - `using Microsoft.EntityFrameworkCore.Metadata.Builders;`
    (para `EntityTypeBuilder<T>`).
- XML doc en la clase y en el método `Configure`,
  citando `docs/architecture.md` §8.

### RF-2 — `PaymentMethodConfiguration`

- `public sealed class PaymentMethodConfiguration :
  IEntityTypeConfiguration<PaymentMethod>` en
  `src/ControlDeFinanzasIA.Infrastructure/Persistence/Configurations/PaymentMethodConfiguration.cs`.
- Implementa `void Configure(EntityTypeBuilder<PaymentMethod> b)`:
  - Idéntica estructura que `CategoryConfiguration`
    pero sobre `PaymentMethod`. Sin `Color` ni
    `Icon` (campos exclusivos de `Category`).
  - `b.ToTable("PaymentMethods")`.
  - `b.Property(p => p.Id).ValueGeneratedNever()`.
  - `b.Property(p => p.Name).IsRequired().HasMaxLength(100).UseCollation("NOCASE")`.
  - `b.HasIndex(p => p.Name).IsUnique()`.
  - `b.Property(p => p.IsSystem).IsRequired()`.
  - `b.Property(p => p.CreatedAt).IsRequired()`.
  - `b.Property(p => p.UpdatedAt).IsRequired()`.
- Mismo namespace, `using` y XML doc que
  `CategoryConfiguration`.

### RF-3 — Aplicación automática de las configuraciones

- Las configuraciones se aplican vía
  `modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly)`
  en `AppDbContext.OnModelCreating` (que ya existe
  desde F-001). F-002 **NO** modifica
  `AppDbContext.cs`; solo añade los archivos de
  configuración que el `ApplyConfigurationsFromAssembly`
  descubre automáticamente.

### RF-4 — Herramienta `dotnet-ef` instalada localmente

- Crear (si no existe) `.config/dotnet-tools.json` en la
  raíz del repo.
- El archivo contiene una entrada para `dotnet-ef` con
  versión 8.0.x (alineada con EF Core 8.0.11; el
  default de referencia es 8.0.11 pero se confirma vía
  Context7 al implementar).
- Comando equivalente al que el implementador ejecuta:
  `dotnet new tool-manifest` (si no existe) y
  `dotnet tool install dotnet-ef --version 8.0.x`.
- **No** se instala globalmente. La disciplina del
  proyecto es "herramientas locales en el manifest
  del repo, no globales".
- El archivo `.config/dotnet-tools.json` se commitea.

### RF-5 — Migración `InitialSchema` generada

- Comando:
  `dotnet ef migrations add InitialSchema --project src/ControlDeFinanzasIA.Infrastructure --startup-project src/ControlDeFinanzasIA.Infrastructure`.
  (El `--startup-project` es `Infrastructure` porque
  ahí vive el `AppDbContext` y el `Program.cs`/
  composition root que EF Core necesita para
  inicializar el host; en F-005 se moverá a `UI`
  cuando se enchufe `AddPersistence`. Por ahora, el
  startup project es `Infrastructure` mismo.)
- Después de la ejecución, se crean 3 archivos en
  `src/ControlDeFinanzasIA.Infrastructure/Persistence/Migrations/`:
  - `YYYYMMDDhhmmss_InitialSchema.cs` (la migración).
  - `YYYYMMDDhhmmss_InitialSchema.Designer.cs`
    (metadata).
  - `AppDbContextModelSnapshot.cs` (snapshot del
    modelo).
  - El timestamp lo asigna la herramienta; el
    implementador no lo controla.
- Los 3 archivos se commitean.

### RF-6 — Contenido de la migración

- El método `Up` de
  `YYYYMMDDhhmmss_InitialSchema.cs`:
  - Crea la tabla `Categories` con las columnas
    definidas en `CategoryConfiguration`
    (`Id`, `Name`, `Color`, `Icon`, `IsSystem`,
    `CreatedAt`, `UpdatedAt`).
  - Crea el índice único `IX_Categories_Name`.
  - Crea la tabla `PaymentMethods` con las columnas
    definidas en `PaymentMethodConfiguration`
    (`Id`, `Name`, `IsSystem`, `CreatedAt`,
    `UpdatedAt`).
  - Crea el índice único `IX_PaymentMethods_Name`.
  - **NO** incluye `InsertData` (decisión
    documentada en §17.5; el seed viene por
    `SeedData.Apply` en `OnModelCreating`).
- El método `Down`:
  - Borra las 2 tablas y sus índices.
- `BuildTargetModel` y demás metadata en
  `InitialSchema.Designer.cs` (generados por la
  herramienta, no editados a mano).

### RF-7 — Compilación y tests verdes

- `dotnet build` desde la raíz: 0 warnings, 0 errors
  (Debug y Release).
- `dotnet test`: 111 tests verdes (100 + 11 de F-007).
- 11 tests de NetArchTest verdes.
- `dotnet format style --verify-no-changes`: exit 0.

---

## 7. Requisitos no funcionales

### RNF-1 — Compatibilidad con la arquitectura

- Después de F-002, el grafo de dependencias sigue
  cumpliendo las reglas R-1 a R-5 de F-007.
- En particular:
  - R-1 (Domain sin EF Core): `Domain` no importa
    `Microsoft.EntityFrameworkCore.*`. Las
    configuraciones viven en `Infrastructure`.
  - R-4 (UI no → Infrastructure): F-002 NO añade
    la referencia (es F-005).
- Las 11 tests de NetArchTest pasan sin
  modificaciones.

### RNF-2 — Determinismo del snapshot

- El `ModelSnapshot` es determinista: ejecutar
  `dotnet ef migrations add InitialSchema` dos
  veces seguidas (con el mismo código) produce
  el mismo snapshot (excepto el timestamp del
  nombre del archivo).
- Las configuraciones no tienen lógica condicional
  basada en tiempo, entorno o estado global.

### RNF-3 — Reproducibilidad de la migración

- Otro developer que clone el repo puede ejecutar
  `dotnet tool restore` + `dotnet ef database update`
  y obtener el mismo schema que el commit actual.
- El archivo `.config/dotnet-tools.json` con
  `dotnet-ef` pinneado a 8.0.x garantiza esto.

### RNF-4 — Sin scope creep

- F-002 NO implementa repositorios (F-004).
- F-002 NO implementa el seed data (F-003).
- F-002 NO aplica migraciones al arranque (F-005).
- F-002 NO añade `<ProjectReference>` de UI a
  Infrastructure (F-005).
- F-002 NO añade tests de integración (F-006).
- F-002 NO modifica `AppDbContext` (las
  configuraciones se descubren automáticamente).

### RNF-5 — Mensajes y convenciones

- Nombres en inglés en el código (convención
  .NET, ya aplicada en todo el repo).
- XML doc en cada clase y método público.
- File-scoped namespace (proyecto rule).
- `using` directives al tope del archivo.
- `sealed` en las clases de configuración
  (no se heredan).

---

## 8. Reglas de negocio

### RN-1 — Las configuraciones viven en `Infrastructure`, no en `Domain`

- Refuerzo de la regla R-1 de F-007.
- `Category` y `PaymentMethod` en `Domain/Catalog/`
  son POCOs puros, sin atributos de EF.
- Las configuraciones viven en
  `Infrastructure/Persistence/Configurations/`.
- `AppDbContext.OnModelCreating` (F-001) las aplica
  vía `ApplyConfigurationsFromAssembly`.

### RN-2 — Tablas en PascalCase

- `Categories`, `PaymentMethods` (no
  `categories`, `payment_methods`, ni
  `Category`, `PaymentMethod`).
- Decisión coherente con `docs/architecture.md` §8
  ("snake_case o PascalCase en columnas: se fija
  PascalCase").

### RN-3 — Collation `NOCASE` para nombres únicos

- SQLite: `NOCASE` collation hace que las
  comparaciones de strings sean case-insensitive
  (en el contexto del índice y de las queries que
  lo usan).
- Esto significa que `WHERE Name = 'comida'` y
  `WHERE Name = 'Comida'` devuelven la misma fila.
- El índice único `IX_Categories_Name` impide que
  existan ambas al mismo tiempo.
- **Limitación:** `NOCASE` es específico de SQLite.
  Si en el futuro se añade SQL Server (v2+), se
  cambia a `IsCaseSensitive(false)` en la
  configuración. v1 es solo SQLite.

### RN-4 — Guid generado en Domain, no en DB

- `ValueGeneratedNever` le dice a EF Core que el Guid
  ya viene listo de la entidad (generado por Domain).
- Permite que el dominio controle la identidad.
- Permite que los tests predefinan IDs (e.g., en
  `SeedData.Apply` cuando F-003 lo implemente).
- **Implicación para el diseño de Domain:** las
  entidades deben tener constructores o factories
  que asignen el `Id` antes de persistir (F-003
  y F-004 pueden necesitarlo).

### RN-5 — El seed NO va en la migración

- La migración `InitialSchema` describe solo el
  schema (DDL puro).
- El seed data se aplica vía
  `SeedData.Apply(modelBuilder)` en
  `OnModelCreating` (F-001 ya tiene el stub;
  F-003 lo completa).
- **Razón:** mantener la migración limpia y
  legible. Si el seed estuviera en la migración,
  cualquier cambio en el seed (e.g., añadir una
  nueva categoría) requeriría una nueva
  migración. Con `HasData` en el modelo, EF Core
  gestiona el upsert automáticamente.
- **Implicación práctica:** cuando F-005 llame a
  `db.Database.Migrate()`, las migraciones se
  aplican, pero el seed (definido por F-003 vía
  `HasData`) se aplica por separado. En la
  práctica, EF Core emite `InsertData` en la
  migración automáticamente cuando detecta
  `HasData` en el modelo, pero solo si el
  snapshot lo incluye. En F-002 el snapshot
  tiene el stub vacío, por lo que la migración
  no incluye `InsertData`. F-003 (cuando
  rellene el `HasData`) generaría una nueva
  migración (e.g., `SeedSystemData`) si fuera
  estricta la disciplina. **Decisión:** F-003
  decide si crea una migración explícita o si
  deja que el seed se aplique en runtime (F-005).

---

## 9. Modelo de dominio

F-002 **NO** introduce nuevas entidades de dominio.
Solo añade el modelo de **mapeo** entre las entidades
existentes y SQLite:

```
Infrastructure/Persistence/
├── AppDbContext.cs                       (F-001)
├── EfUnitOfWork.cs                       (F-001)
├── SeedData.cs                           (F-001 stub, F-003 completo)
├── ServiceCollectionExtensions.cs        (F-001)
├── Configurations/                       ← nuevo (F-002)
│   ├── CategoryConfiguration.cs           ← F-002
│   └── PaymentMethodConfiguration.cs      ← F-002
└── Migrations/                           ← nuevo (F-002)
    ├── YYYYMMDDhhmmss_InitialSchema.cs   ← generado por dotnet ef
    ├── YYYYMMDDhhmmss_InitialSchema.Designer.cs
    └── AppDbContextModelSnapshot.cs
```

**`CategoryConfiguration`:**
- `IEntityTypeConfiguration<Category>`.
- Mapea `Category` a la tabla `Categories` con
  índice único en `Name` (NOCASE) y longitudes
  máximas explícitas.

**`PaymentMethodConfiguration`:**
- `IEntityTypeConfiguration<PaymentMethod>`.
- Mapea `PaymentMethod` a la tabla
  `PaymentMethods` (sin `Color`/`Icon`).

**`Migrations/`:**
- Carpeta estándar de EF Core para migraciones.
- La primera migración es `InitialSchema` (las
  siguientes se nombran con la convención
  `<descripción>` y el timestamp lo asigna la
  herramienta).

---

## 10. Configuración técnica

### 10.1 — `.config/dotnet-tools.json`

- Crear el archivo en la raíz del repo.
- Contenido (descripción):
  ```json
  {
    "version": 1,
    "isRoot": true,
    "tools": {
      "dotnet-ef": {
        "version": "8.0.x",
        "commands": ["dotnet-ef"]
      }
    }
  }
  ```
- La versión exacta se confirma vía Context7
  (alineada con `Microsoft.EntityFrameworkCore`
  8.0.11 que F-001 instaló; default de
  referencia: 8.0.11).
- **No se añade** `dotnet-ef` globalmente. La
  disciplina es "herramientas locales en el
  manifest, no globales".

### 10.2 — Comando `dotnet ef`

- Para generar la migración:
  ```text
  dotnet ef migrations add InitialSchema ^
    --project src/ControlDeFinanzasIA.Infrastructure ^
    --startup-project src/ControlDeFinanzasIA.Infrastructure
  ```
- El `--startup-project` es `Infrastructure` mismo
  en F-002. En F-005, cuando se mueva el
  composition root a `UI`, el `--startup-project`
  cambiará a `UI`. F-002 no anticipa eso; solo
  genera la migración con el startup actual.
- Si la herramienta no se detecta: ejecutar
  `dotnet tool restore` desde la raíz antes de
  `dotnet ef migrations add`.

### 10.3 — Tabla de columnas generadas

**Tabla `Categories`:**

| Columna | Tipo | Restricciones |
| --- | --- | --- |
| `Id` | `Guid` | PK, NOT NULL, no auto-generado (Guid desde Domain). |
| `Name` | `TEXT` | NOT NULL, max 100, collation `NOCASE`, índice único. |
| `Color` | `TEXT` | nullable, max 9 (formato `#RRGGBB` o `#RRGGBBAA`). |
| `Icon` | `TEXT` | nullable, max 50. |
| `IsSystem` | `INTEGER` | NOT NULL (bool de EF Core se mapea a INTEGER en SQLite). |
| `CreatedAt` | `TEXT` | NOT NULL (DateTime se mapea a TEXT en SQLite por convención). |
| `UpdatedAt` | `TEXT` | NOT NULL. |

**Tabla `PaymentMethods`:**

| Columna | Tipo | Restricciones |
| --- | --- | --- |
| `Id` | `Guid` | PK, NOT NULL, no auto-generado. |
| `Name` | `TEXT` | NOT NULL, max 100, collation `NOCASE`, índice único. |
| `IsSystem` | `INTEGER` | NOT NULL. |
| `CreatedAt` | `TEXT` | NOT NULL. |
| `UpdatedAt` | `TEXT` | NOT NULL. |

Los tipos de columna exactos los genera EF Core;
el implementador verifica el resultado
ejecutando `dotnet ef migrations script` o
inspeccionando el `Up` de la migración generada.

### 10.4 — Convenciones de código

- File-scoped namespace:
  `namespace ControlDeFinanzasIA.Infrastructure.Persistence.Configurations;`.
- `using` al tope (NUNCA después del namespace).
- XML doc en cada clase y método público.
- `sealed` en las 2 configuraciones.
- Nombres en inglés.

---

## 11. Validaciones

F-002 no introduce reglas de validación de negocio
directamente (las entidades son POCOs y las
configuraciones son mappings). Lo que F-002 valida
es la **estructura del schema**:

| # | Validación | Cuándo se aplica | Comportamiento esperado |
| --- | --- | --- | --- |
| V-1 | El comando `dotnet ef migrations add` no produce errores. | Al ejecutar el comando. | Si falla, el implementador diagnostica (versión de `dotnet-ef` incorrecta, paquete faltante, etc.) antes de continuar. |
| V-2 | Los 3 archivos de la migración existen en `Migrations/`. | Después de la generación. | Si falta alguno, regenerar la migración. |
| V-3 | El `Up` de la migración crea las tablas con todas las columnas y los índices únicos. | Inspección manual del `Up`. | Si falta algo, ajustar las configuraciones y regenerar la migración. |
| V-4 | `dotnet build` desde la raíz: 0 warnings, 0 errors. | Después de la generación. | Si hay warnings, ajustar (probablemente la herramienta emite `using` redundantes que `dotnet format` limpia). |
| V-5 | `dotnet test`: 111 tests verdes. | Después del build. | Si falla, investigar (no debería; las configuraciones no afectan los tests existentes). |

---

## 12. Casos borde

### EC-1 — Versión de `dotnet-ef` no instalada localmente

- Si el developer clona el repo por primera vez y
  ejecuta `dotnet ef migrations add` sin haber
  hecho `dotnet tool restore`, el comando falla
  con "tool not found".
- **Mitigación:** documentar en el README y/o en
  el `Directory.Packages.props` que el primer paso
  tras clonar es `dotnet tool restore`. F-002
  genera el `.config/dotnet-tools.json` que
  registra la herramienta, pero el developer
  debe ejecutar `restore` para que se instale
  localmente.

### EC-2 — `dotnet-ef` versión incorrecta

- Si por error se instala `dotnet-ef` 9.0.x (que
  requeriría .NET 9), la generación de la
  migración falla con un mensaje claro.
- **Mitigación:** el `.config/dotnet-tools.json`
  pinnea la versión exacta. Si el developer
  intenta usar otra, `dotnet tool restore` le
  advertirá.

### EC-3 — `Migrations/` se commitea sin los 3 archivos

- Si por error solo se commitea el `.cs` de la
  migración y no el `Designer.cs` o el
  `ModelSnapshot.cs`, EF Core no puede aplicar la
  migración (le falta metadata).
- **Mitigación:** la spec exige los 3 archivos
  en RF-5; el PR review debe verificar.

### EC-4 — El snapshot diverge de las configuraciones

- Si el developer edita una configuración sin
  regenerar la migración, el snapshot queda
  desincronizado.
- **Mitigación:** cualquier cambio en una
  configuración debe ir acompañado de una nueva
  migración. F-002 genera solo la primera; las
  siguientes son trabajo de features futuras (F-003+
  cuando cambien las entidades).

### EC-5 — El `--startup-project` cambia en F-005

- F-002 usa `--startup-project
  src/ControlDeFinanzasIA.Infrastructure`. Cuando
  F-005 mueva el composition root a `UI`, el
  comando de generación de migraciones futuras
  cambiará a `--startup-project
  src/ControlDeFinanzasIA.UI`.
- **Implicación:** la migración `InitialSchema`
  generada en F-002 no necesita regenerarse;
  solo las migraciones futuras (e.g., cuando F-003
  llene el seed) usarán el nuevo startup project.
- **Decisión a documentar en F-005:** qué
  `--startup-project` usar para migraciones
  futuras.

### EC-6 — Aplicar la migración a una DB existente

- En F-002, la migración se genera pero no se
  aplica (no hay `Database.Migrate()` en F-002).
- La aplicación ocurre en F-005 (al arrancar la
  app) o manualmente con
  `dotnet ef database update`.
- **Implicación para v1:** en una instalación
  limpia, la app crea la DB y aplica la
  migración al arrancar. En una actualización
  con DB existente, la migración detecta el
  estado actual y aplica solo lo pendiente.

---

## 13. Dependencias

### 13.1 Prerrequisitos de implementación

- **F-001 de Fase 2** (completada): provee
  `AppDbContext` (con `OnModelCreating` que llama
  a `ApplyConfigurationsFromAssembly` y a
  `SeedData.Apply`), los paquetes EF Core 8.0.11
  en CPM, y los `<PackageReference>` en
  `Infrastructure.csproj`.
- **F-002 a F-008 de Phase 1** (todas
  completadas): proveen el proyecto
  `Infrastructure`, las convenciones de CPM, y
  el `.gitignore` (que excluye archivos
  generados).
- **`dotnet ef` 8.0.x** instalado localmente
  (en `.config/dotnet-tools.json`; se instala
  en T-001 de las tareas de F-002).

### 13.2 Dependencias con otras features

| Feature | Relación |
| --- | --- |
| F-001 (Phase 2) | Provee `AppDbContext`, paquetes EF Core, y `SeedData` stub. F-002 lo usa. |
| F-003 (Phase 2) | Rellena el cuerpo de `SeedData.Apply` con `HasData(...)` para las 9 categorías y 5 métodos de pago. Si F-003 genera una nueva migración (e.g., `SeedSystemData`), se aplica aquí. Si no, el seed se aplica via el snapshot. |
| F-004 (Phase 2) | Implementa los repositorios concretos. F-002 provee el schema sobre el que operan. |
| F-005 (Phase 2) | Aplica la migración al arrancar la app (`db.Database.Migrate()`). F-002 genera la migración pero no la aplica. |
| F-006 (Phase 2) | Testa las configuraciones y la migración con SQLite in-memory. F-002 provee el código bajo test. |
| F-007 (Phase 1) | Los tests R-1 a R-5 verifican que F-002 no rompe la arquitectura. |

### 13.3 Bloqueos

- **Ninguno.** F-002 puede implementarse ahora
  mismo. El único prerrequisito bloqueante
  (F-001) está cumplido.

---

## 14. Fuera de alcance (detallado)

Repetición con énfasis. F-002 **NO** hace lo
siguiente:

- Implementar el cuerpo de `SeedData.Apply`
  (F-003).
- Crear los 15 repositorios concretos (F-004).
- Wire-up de `AddPersistence` en
  `Bootstrap.ConfigureServices` (F-005).
- Aplicar la migración al arrancar la app
  (F-005).
- `<ProjectReference>` de `UI` a `Infrastructure`
  (F-005).
- Tests de integración de las configuraciones
  (F-006).
- Conversión de value objects en EF Core (Fase
  3, cuando se introduzca `Expense` con
  `Amount: Money`).
- Migraciones adicionales a `InitialSchema`
  (las siguientes son trabajo de features
  futuras).
- Modificaciones a `AppDbContext` (las
  configuraciones se descubren
  automáticamente).
- Modificaciones a `Domain`, `Application`,
  `UI`, `Tests.Unit`, `Tests.Integration`.
- Modificaciones al CI workflow.
- Modificaciones a `.editorconfig`,
  `stylecop.json`, `Directory.Build.props`.

---

## 15. Criterios de aceptación

La feature se considera completada cuando
**todos** los siguientes puntos se cumplen.

### AC-1 — Configuraciones creadas

- [ ] `src/ControlDeFinanzasIA.Infrastructure/Persistence/Configurations/CategoryConfiguration.cs`
      existe.
- [ ] `public sealed class CategoryConfiguration :
      IEntityTypeConfiguration<Category>`.
- [ ] Mapea a tabla `Categories`.
- [ ] `Id`: primary key, `ValueGeneratedNever`.
- [ ] `Name`: required, max 100, `NOCASE`
      collation, índice único.
- [ ] `Color`: max 9, nullable.
- [ ] `Icon`: max 50, nullable.
- [ ] `IsSystem`, `CreatedAt`, `UpdatedAt`:
      required.
- [ ] `src/ControlDeFinanzasIA.Infrastructure/Persistence/Configurations/PaymentMethodConfiguration.cs`
      existe.
- [ ] `public sealed class PaymentMethodConfiguration :
      IEntityTypeConfiguration<PaymentMethod>`.
- [ ] Mapea a tabla `PaymentMethods` (sin
      `Color`/`Icon`).
- [ ] Mismas reglas de `Name`, `Id`,
      `IsSystem`, `CreatedAt`, `UpdatedAt` que
      `Category`.

### AC-2 — Migración generada

- [ ] El comando
      `dotnet ef migrations add InitialSchema --project src/ControlDeFinanzasIA.Infrastructure --startup-project src/ControlDeFinanzasIA.Infrastructure`
      se ejecutó exitosamente.
- [ ] Los 3 archivos existen en
      `Infrastructure/Persistence/Migrations/`:
      - `YYYYMMDDhhmmss_InitialSchema.cs`
      - `YYYYMMDDhhmmss_InitialSchema.Designer.cs`
      - `AppDbContextModelSnapshot.cs`
- [ ] El `Up` de la migración crea las tablas
      `Categories` y `PaymentMethods` con
      todas las columnas definidas en las
      configuraciones.
- [ ] El `Up` crea los índices únicos
      `IX_Categories_Name` y
      `IX_PaymentMethods_Name`.
- [ ] El `Up` **NO** incluye `InsertData`
      (el seed viene por `HasData` en
      `OnModelCreating`; el snapshot actual
      tiene el stub vacío).
- [ ] El `Down` borra las 2 tablas y sus
      índices.
- [ ] Los 3 archivos están commiteados en
      git (no solo el `.cs`).

### AC-3 — Herramienta `dotnet-ef` instalada

- [ ] `.config/dotnet-tools.json` existe en la
      raíz del repo.
- [ ] Contiene una entrada para `dotnet-ef`
      con versión 8.0.x (alineada con EF Core
      8.0.11).
- [ ] `dotnet tool restore` desde la raíz
      instala la herramienta localmente sin
      errores.
- [ ] `dotnet ef --version` (ejecutado tras
      `restore`) muestra la versión 8.0.x.

### AC-4 — Compilación y tests

- [ ] `dotnet build -c Release` desde la
      raíz: 0 warnings, 0 errors.
- [ ] `dotnet test` desde la raíz: 111 tests
      verdes (100 + 11 de F-007), 0 failures.
- [ ] Los 11 tests de NetArchTest de F-007
      pasan sin modificaciones.

### AC-5 — Convenciones del repo

- [ ] File-scoped namespace en los 2
      archivos de configuración.
- [ ] `using` directives al tope (NUNCA
      después del namespace).
- [ ] XML doc en cada clase y método
      público.
- [ ] `sealed` en ambas configuraciones.
- [ ] Nombres en inglés.
- [ ] `dotnet format style --verify-no-changes`:
      exit 0.

### AC-6 — Sin scope creep

- [ ] NO se ha modificado `AppDbContext.cs`
      (las configuraciones se descubren
      automáticamente vía
      `ApplyConfigurationsFromAssembly`).
- [ ] NO se ha implementado el cuerpo de
      `SeedData.Apply` (sigue siendo el
      stub de F-001).
- [ ] NO se han creado los 15 repositorios
      (F-004).
- [ ] NO se ha añadido `<ProjectReference>`
      de `UI` a `Infrastructure` (F-005).
- [ ] NO se ha añadido lógica de aplicación
      de migraciones al arranque (F-005).
- [ ] NO se han creado tests de integración
      (F-006).
- [ ] NO se ha modificado `Domain`,
      `Application`, `UI`, `Tests.Unit`.

### AC-7 — Documentación y trazabilidad

- [ ] `spec/phases/phase-02/features.md`
      actualizado: F-002 marcada como ✅
      COMPLETADA en la tabla resumen y en el
      header de la sección.
- [ ] La spec se renombra a
      `spec/features/phase-02/F-002-entity-configurations-migration.done.md`.

---

## 16. Estructura de archivos esperada

```
ControlDeFinanzasIA/                              (raíz)
├── .config/                                      ← nuevo (F-002)
│   └── dotnet-tools.json                          ← dotnet-ef 8.0.x
├── src/
│   └── ControlDeFinanzasIA.Infrastructure/
│       └── Persistence/                          (existente de F-001)
│           ├── AppDbContext.cs
│           ├── EfUnitOfWork.cs
│           ├── SeedData.cs
│           ├── ServiceCollectionExtensions.cs
│           ├── Configurations/                  ← nuevo (F-002)
│           │   ├── CategoryConfiguration.cs       ← F-002
│           │   └── PaymentMethodConfiguration.cs  ← F-002
│           └── Migrations/                      ← nuevo (F-002)
│               ├── YYYYMMDDhhmmss_InitialSchema.cs
│               ├── YYYYMMDDhhmmss_InitialSchema.Designer.cs
│               └── AppDbContextModelSnapshot.cs

src/ControlDeFinanzasIA.Infrastructure/
ControlDeFinanzasIA.Infrastructure.csproj         (sin cambios en F-002; ya tiene los 3 paquetes EF Core de F-001)
```

**Modificaciones a `.csproj`:** ninguna. F-002 no
añade ni quita paquetes ni referencias; los que F-001
añadió son suficientes.

**Modificaciones al CI workflow:** ninguna.

---

## 17. Decisiones de diseño explícitas

### 17.1 — Versión de `dotnet-ef`

**Decisión:** 8.0.x, alineada con EF Core 8.0.11
(la misma versión que F-001 añadió a CPM).
Default de referencia: 8.0.11. Confirmada vía
Context7 al implementar.

**Justificación:** la herramienta debe coincidir
con la versión del runtime. Mezclar `dotnet-ef`
8.0.x con EF Core 8.0.11 funciona; mezclar
`dotnet-ef` 9.0.x con EF Core 8.0.11 no.

**Alternativa descartada:** instalar `dotnet-ef`
globalmente. Contamina el ambiente del
developer; cada proyecto debería pin sus
herramientas.

### 17.2 — Tool manifest local (no global)

**Decisión:** `.config/dotnet-tools.json` en la
raíz del repo.

**Justificación:** disciplina del proyecto. El
archivo es commiteable, reproducible, y permite
que `dotnet tool restore` instale la versión
correcta en cada clone.

### 17.3 — `IEntityTypeConfiguration<T>` por archivo, uno por entidad

**Decisión:** `CategoryConfiguration.cs` y
`PaymentMethodConfiguration.cs` por separado.

**Justificación:** `docs/architecture.md` §8 lo
fija: "una clase por entidad,
`IEntityTypeConfiguration<T>`". Esto mantiene
`OnModelCreating` limpio (delegado a
`ApplyConfigurationsFromAssembly`) y permite
que cada configuración evolucione
independientemente.

### 17.4 — Tablas en PascalCase

**Decisión:** `Categories`, `PaymentMethods`.

**Justificación:** `docs/architecture.md` §8 fija
"PascalCase salvo decisión explícita". El código
C# del proyecto es todo PascalCase; mantener
consistencia en el schema.

**Alternativa descartada:** snake_case
(`categories`, `payment_methods`). Requeriría
añadir `EFCore.NamingConventions` y rompe la
coherencia con el código.

### 17.5 — Seed NO en la migración

**Decisión:** la migración `InitialSchema` no
incluye `InsertData`. El seed se aplica vía
`SeedData.Apply(modelBuilder)` (F-001 ya lo
referencia; F-003 lo completa).

**Justificación:** mantener la migración limpia
y legible. Si el seed estuviera en la migración,
cualquier cambio en el seed requeriría una
nueva migración. Con `HasData` en el modelo,
EF Core gestiona el upsert.

**Implicación práctica:** cuando F-005 llame a
`db.Database.Migrate()`, la migración
`InitialSchema` se aplica (crea las tablas
vacías). El seed (cuando F-003 lo rellene) se
aplica por separado vía el snapshot del
modelo. En la práctica, EF Core emite
`InsertData` automáticamente cuando regeneras
la migración; pero en F-002 la migración
NO se regenera (ya está commiteada), por lo
que el seed se queda pendiente de F-003/F-005.

### 17.6 — `Guid` generado en Domain, no en DB

**Decisión:** `ValueGeneratedNever` en
`Id`.

**Justificación:** permite que el dominio
controle la identidad. Los tests pueden
predefinir IDs en el seed (F-003) o en datos
de prueba. La entidad es dueña de su
identidad, no el DB.

### 17.7 — `NOCASE` collation en SQLite

**Decisión:** `UseCollation("NOCASE")` en
`Name`.

**Justificación:** SQLite por defecto es
case-sensitive. `NOCASE` permite que "comida"
y "Comida" sean tratados como iguales (un
usuario no debería poder tener ambas). El
índice único aprovecha esta collation.

**Limitación documentada:** `NOCASE` es
específico de SQLite. Si en el futuro se
añade SQL Server, se cambia a
`IsCaseSensitive(false)`. v1 es solo
SQLite.

### 17.8 — Modificación o no de `AppDbContext`

**Decisión:** F-002 **NO** modifica
`AppDbContext.cs`. Las configuraciones se
descubren automáticamente vía
`ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly)`.

**Justificación:** F-001 ya escribió la
línea correcta. F-002 solo añade archivos
en la carpeta `Configurations/`, que el
mecanismo automático recoge.

---

## 18. Riesgos y mitigaciones

| Riesgo | Probabilidad | Impacto | Mitigación |
| --- | --- | --- | --- |
| Versión de `dotnet-ef` no coincide con la versión de EF Core. | Baja | Alto | Pinning exacto en `.config/dotnet-tools.json`. Verificar con `dotnet ef --version` antes de generar la migración. |
| La migración incluye `InsertData` aunque la spec dice que no. | Media | Bajo | El snapshot tiene el stub de `SeedData` (F-001), por lo que EF Core no genera `InsertData` automáticamente. Si lo incluye, el implementador lo elimina manualmente de la migración. |
| `dotnet ef` no se puede ejecutar en el entorno actual. | Baja | Medio | Documentar en README y en spec que el primer paso tras clonar es `dotnet tool restore`. |
| `Migrations/` se commitea sin los 3 archivos. | Baja | Alto | La spec RF-5 y AC-2 lo exigen. El PR review debe verificar los 3 archivos. |
| El `--startup-project` se olvida al generar migraciones futuras. | Baja | Bajo | F-005 documenta el nuevo `--startup-project` (que será `UI`). F-002 usa el actual (`Infrastructure`). |
| Tablas o columnas en snake_case por error (alguien edita la configuración). | Baja | Medio | El implementador verifica con `dotnet ef migrations script` o inspección visual del `Up`. La spec §17.4 lo fija. |
| Índices únicos no se generan. | Baja | Alto | El `Up` de la migración debe incluir `migrationBuilder.CreateIndex(...)` para `IX_Categories_Name` y `IX_PaymentMethods_Name`. Verificación visual. |
| `ApplyConfigurationsFromAssembly` no encuentra las configuraciones. | Muy baja | Alto | El assembly es el mismo (`typeof(AppDbContext).Assembly`); las clases son públicas. Si no las encuentra, el problema es de namespace o de visibilidad. |
| `dotnet format style` propone cambios en los archivos de EF Core (que tienen `using` redundantes). | Baja | Bajo | Aplicar los cambios propuestos. Si los `using` redundantes vienen de la plantilla de `dotnet ef`, no se pueden eliminar manualmente (se regeneran al regenerar la migración). |
| El `ModelSnapshot` diverge de las configuraciones porque se regenera con `dotnet ef`. | Baja | Bajo | Si se regenera la migración, los 3 archivos se sobreescriben. Commit atómico. |

---

## 19. Referencias

- `docs/roadmap.md` — Fase 2 (Entregables esperados,
  Riesgos principales, Criterios de fase completada).
- `docs/architecture.md` — §3 (solución y proyectos),
  §4 (grafo de dependencias), §8 (capa de
  Infrastructure: persistencia, repositorios).
- `docs/vision.md` — §4.10 (Datos y portabilidad).
- `spec/constitution/tech-stack.md` — paquetes,
  convenciones de código, CPM.
- `spec/constitution/mission.md` — Clean
  Architecture, principios.
- `spec/phases/phase-02/features.md` — F-002 dentro
  del desglose de la fase 2 (6 features totales).
- `spec/features/phase-02/F-001-appdbcontext-addpersistence.done.md`
  — provee `AppDbContext` y los paquetes EF Core
  que F-002 usa. Define `SeedData` como stub.
- `spec/features/phase-01/F-001-skeleton-solution.done.md`
  — convenciones de `.editorconfig`, `stylecop.json`,
  CPM, y `.gitignore` (que excluye `bin/`, `obj/`,
  etc.).
- `spec/features/phase-01/F-006-contrato-interfaces-repo.done.md`
  — provee `IUnitOfWork` que F-002 referencia (no
  implementa, pero la arquitectura lo considera).
- `spec/features/phase-01/F-007-validacion-arquitectonica.done.md`
  — los tests R-1 a R-5 que F-002 no debe romper.
- `spec/README.md` — convención del directorio
  `spec/`.
- EF Core 8 documentation oficial (a consultar vía
  Context7 cuando se implemente).
- `dotnet ef` CLI documentation (a consultar vía
  Context7 cuando se genere la migración).
