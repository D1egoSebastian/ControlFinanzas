# Specification — F-001: AppDbContext y AddPersistence

> **Fase:** 2 — Persistencia con EF Core + SQLite
> **Feature:** F-001 (local a esta fase)
> **Estado:** ✅ COMPLETADA
> **Spec ID:** SPEC-F001P2
>
> **Nota de numeración:** este `F-001` es el de la **Fase 2**
> (`spec/phases/phase-02/features.md`). No debe confundirse con
> el `F-001` de Fase 1 (esqueleto de solución en
> `spec/phases/phase-01/features.md`). Los IDs son locales a cada
> fase; el folder los desambigua. Ver `spec/README.md` para la
> convención.
>
> Fuentes de verdad: `docs/vision.md`, `docs/architecture.md`,
> `docs/roadmap.md` (Fase 2), `spec/constitution/tech-stack.md`,
> `spec/constitution/mission.md`,
> `spec/phases/phase-02/features.md` (F-001 dentro del
> desglose de la fase 2).

---

## 1. Resumen

F-001 introduce la **capa de persistencia local** de la
aplicación: añade los paquetes NuGet de Entity Framework Core 8
y SQLite, define el `AppDbContext` con `DbSet<Category>` y
`DbSet<PaymentMethod>` (las dos entidades mínimas que cualquier
feature posterior referencia), implementa `EfUnitOfWork`
(la única implementación concreta de `IUnitOfWork` en v1), y
expone el extension method `AddPersistence` que F-005 enchufará
en el composition root de F-008.

Al final de la feature:
- `Directory.Packages.props` declara los 4 paquetes EF Core
  con versión exacta.
- `Infrastructure.csproj` y `Tests.Integration.csproj`
  referencian los paquetes vía CPM.
- Las 3 clases nuevas (`AppDbContext`, `EfUnitOfWork`,
  `ServiceCollectionExtensions`) existen y compilan.
- `dotnet build` y `dotnet test` (los 111 tests existentes
  de F-002 a F-008) pasan en limpio.
- El test R-1 de NetArchTest (F-007) **sigue pasando**:
  `Domain` no tiene `Microsoft.EntityFrameworkCore.*` y
  `Application` tampoco.

---

## 2. Contexto

La Fase 1 (F-001 a F-008) dejó la app en un estado
"arquitectura declarada y ejercitada en runtime" pero sin
ningún tipo de persistencia. La ventana arranca, los logs
se emiten, los servicios se resuelven, pero **no hay
manera de guardar un solo dato**.

F-001 de Fase 2 es la **primera incursión real del proyecto
en lógica de aplicación** después de los cimientos de
Fase 1. Es la pieza que conecta el dominio (entidades POCO)
con un motor de persistencia real (SQLite via EF Core).

**Por qué SQLite:** `docs/vision.md` lo fija así. La app es
100% local, no hay servidor, y SQLite es el motor SQL
embebido más maduro y soportado en .NET. EF Core tiene
provider de primera clase (`Microsoft.EntityFrameworkCore.Sqlite`).

**Por qué EF Core:** `docs/vision.md` y
`spec/constitution/tech-stack.md` lo fijan. Code First
permite evolucionar el schema con migraciones versionadas
(`docs/architecture.md` §8), y EF Core es el ORM estándar
en .NET.

**Por qué esta feature es "F-001" y no "F-002"** dentro de
Fase 2: el documento de fase
(`spec/phases/phase-02/features.md`) prioriza los
**paquetes y el DbContext** antes que las configuraciones,
las migraciones y los repositorios. Esto es porque sin los
paquetes y el `AppDbContext`, nada de lo demás compila.

---

## 3. Objetivo

1. **Añadir 4 paquetes NuGet de EF Core 8** a
   `Directory.Packages.props` con versión exacta (sin
   rangos, disciplina CPM de F-001 de Fase 1):
   - `Microsoft.EntityFrameworkCore`.
   - `Microsoft.EntityFrameworkCore.Sqlite`.
   - `Microsoft.EntityFrameworkCore.Design`.
   - `Microsoft.EntityFrameworkCore.Relational`.
2. **Referenciar los paquetes** en los `.csproj` correctos
   sin atributo `Version` (CPM).
3. **Crear `AppDbContext`** con `DbSet<Category>` y
   `DbSet<PaymentMethod>`, que aplique configuraciones
   automáticamente y llame a `SeedData.Apply(...)`.
4. **Crear `EfUnitOfWork`** como única implementación
   concreta de `IUnitOfWork` (interfaz definida en F-006
   de Fase 1).
5. **Crear el extension method `AddPersistence`** en
   `ServiceCollectionExtensions` que registra
   `AppDbContext` e `IUnitOfWork` con el scope correcto.
6. **No introducir** `<ProjectReference>` de `UI` a
   `Infrastructure` todavía (eso es F-005).

---

## 4. Alcance

### 4.1 En alcance

- Modificación de `Directory.Packages.props` para añadir
  los 4 `PackageVersion`.
- Modificación de
  `src/ControlDeFinanzasIA.Infrastructure/ControlDeFinanzasIA.Infrastructure.csproj`
  para referenciar 3 paquetes EF Core (Core, Sqlite,
  Design).
- Modificación de
  `tests/ControlDeFinanzasIA.Tests.Integration/ControlDeFinanzasIA.Tests.Integration.csproj`
  para referenciar `Microsoft.EntityFrameworkCore.Sqlite`.
- Creación de
  `src/ControlDeFinanzasIA.Infrastructure/Persistence/AppDbContext.cs`.
- Creación de
  `src/ControlDeFinanzasIA.Infrastructure/Persistence/EfUnitOfWork.cs`.
- Creación de
  `src/ControlDeFinanzasIA.Infrastructure/Persistence/ServiceCollectionExtensions.cs`.
- Verificación de que el build y los 111 tests existentes
  (los 100 originales + 11 de F-007) siguen pasando.
- Verificación de que el test R-1 de NetArchTest
  (`Domain no depende de EF Core`) sigue pasando.

### 4.2 Fuera de alcance

- **Configuraciones de las entidades**
  (`CategoryConfiguration`, `PaymentMethodConfiguration`).
  Esas llegan en F-002 de Fase 2.
- **Migraciones EF Core** (`InitialSchema` y siguientes).
  Es F-002.
- **Seed data** (las 9 categorías y 5 métodos de pago del
  sistema). Es F-003.
- **Repositorios concretos** (`CategoryRepository`,
  `PaymentMethodRepository`, los 13 restantes). Es F-004
  en adelante.
- **Aplicación de migraciones al arranque** (en
  `App.axaml.cs` o donde corresponda). Es F-005.
- **Wiring del `AddPersistence` en el composition root**
  (modificar `Bootstrap.ConfigureServices`). Es F-005.
- **Conversión de value objects** (`Money`, `DateRange`) en
  EF Core. `Category` y `PaymentMethod` no tienen esos
  campos; el patrón se establece cuando se introduzcan
  entidades con `Money` (Fase 3). Documentado en el spec
  de fase 2 §4 (fuera de alcance).
- **Logging de queries SQL**. La spec de fase 2 §4 dice:
  "Logging estructurado de queries EF Core — se introduce
  con `Microsoft.Extensions.Logging` cuando se monte el
  composition root en F-008. Esta feature solo añade el
  provider de EF Core para SQLite; el logging fino se hace
  en la fase de wiring."
- **`<ProjectReference>` de `UI` a `Infrastructure`**. Es
  F-005 (la única dependencia entre `UI` e `Infrastructure`
  pasa por el composition root, que la test R-4 de F-007
  ya filtra).
- **Herramienta `dotnet ef`**. La instalación del CLI tool
  es responsabilidad de F-002 (que es la feature que
  ejecuta `dotnet ef migrations add` por primera vez).
- **Tests de integración** de la persistencia. Es F-006.
- **Modificaciones a `Domain`, `Application`, `UI`,
  `Tests.Unit`**. F-001 solo toca `Infrastructure`,
  `Tests.Integration`, y `Directory.Packages.props`.

---

## 5. Casos de uso

### CU-1 — `AppDbContext` se construye correctamente

- **Actor:** composition root (F-005) o test de integración
  (F-006).
- **Trigger:** se resuelve `AppDbContext` desde el container
  de DI.
- **Precondición:** `AddPersistence` se llamó (en F-005)
  o el test crea un `AppDbContext` directamente.
- **Postcondición:** el contexto expone
  `Categories` y `PaymentMethods` (vacíos al construir; el
  seed se aplica cuando se ejecuta `Database.Migrate()` o
  cuando EF Core materializa el modelo por primera vez).

### CU-2 — `AddPersistence` registra los servicios correctos

- **Actor:** composition root (F-005).
- **Trigger:** `services.AddPersistence(pathResolver)`.
- **Precondición:** `IPathResolver` ya está registrado
  (F-008 lo hace en `Bootstrap.ConfigureServices`).
- **Postcondición:** `IServiceProvider` puede resolver
  `AppDbContext` (scoped) e `IUnitOfWork` (scoped). La
  conexión SQLite apunta a
  `pathResolver.DatabaseFilePath`.

### CU-3 — `IUnitOfWork.SaveChangesAsync` persiste los cambios

- **Actor:** use case (futuro, Fase 3) o test.
- **Trigger:** un repo añade una entidad al DbContext y
  llama a `await unitOfWork.SaveChangesAsync(ct)`.
- **Precondición:** la entidad está en el DbContext
  (añadida por el repo).
- **Postcondición:** los cambios se persisten en SQLite;
  el método devuelve el número de filas afectadas.

### CU-4 — La compilación sigue limpia con EF Core en `Infrastructure`

- **Actor:** build runner (CI o local).
- **Trigger:** `dotnet build` desde la raíz.
- **Precondición:** F-001 completa.
- **Postcondición:** 0 warnings, 0 errors. Los 111 tests
  existentes siguen pasando.

### CU-5 — R-1 de NetArchTest sigue protegiendo el dominio

- **Actor:** test runner.
- **Trigger:** `dotnet test --filter R1_Domain_*`.
- **Precondición:** F-001 añadió EF Core a
  `Infrastructure.csproj` y `Directory.Packages.props`.
- **Postcondición:** R-1 (Domain no depende de EF Core)
  sigue pasando. Esto valida que **`Domain` sigue sin
  paquetes externos**, que es el principio de Clean
  Architecture de F-001 de Fase 1.

---

## 6. Requisitos funcionales

### RF-1 — Paquetes NuGet

Añadir a `Directory.Packages.props` 4 entradas con
`<PackageVersion Include="..." Version="X.Y.Z" />`. Las
versiones exactas se confirman al implementar (vía
Context7); como referencia, EF Core 8.0.10 y 8.0.11 son
releases estables compatibles con .NET 8.

| Paquete | Versión objetivo | Notas |
| --- | --- | --- |
| `Microsoft.EntityFrameworkCore` | `8.0.x` (x = última estable) | Runtime EF Core (queries, change tracking, migraciones). |
| `Microsoft.EntityFrameworkCore.Sqlite` | `8.0.x` | Provider SQLite. |
| `Microsoft.EntityFrameworkCore.Design` | `8.0.x` | Tooling para `dotnet ef migrations`. Solo se referencia en `Infrastructure`. |
| `Microsoft.EntityFrameworkCore.Relational` | `8.0.x` | Transitivo de Sqlite pero se fija explícitamente para que quede visible. |

Añadir `<PackageReference>` (sin `Version`) en:
- `src/ControlDeFinanzasIA.Infrastructure/ControlDeFinanzasIA.Infrastructure.csproj`:
  - `Microsoft.EntityFrameworkCore`
  - `Microsoft.EntityFrameworkCore.Sqlite`
  - `Microsoft.EntityFrameworkCore.Design`
- `tests/ControlDeFinanzasIA.Tests.Integration/ControlDeFinanzasIA.Tests.Integration.csproj`:
  - `Microsoft.EntityFrameworkCore.Sqlite`

**No se añaden** `Microsoft.EntityFrameworkCore.SqlServer`,
`Npgsql`, ni otros providers. v1 es solo SQLite.

**No se añade** `EFCore.NamingConventions` (snake_case). El
spec de `docs/architecture.md` §8 fija PascalCase, coherente
con el código C#.

### RF-2 — `AppDbContext`

- `public class AppDbContext : DbContext` en
  `src/ControlDeFinanzasIA.Infrastructure/Persistence/AppDbContext.cs`.
- **NO** `sealed` (convención de EF Core: el contexto puede
  extenderse en tests; aunque F-006 usará SQLite in-memory
  con `IDbContextOptionsBuilder`, el patrón no requiere
  herencia).
- Constructor público:
  `public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }`.
- Propiedades:
  - `public DbSet<Category> Categories => Set<Category>();`
  - `public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();`
- Override:
  ```csharp
  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
      modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
      SeedData.Apply(modelBuilder);
  }
  ```
- `SeedData` (clase estática) **NO existe todavía en esta
  feature** (la crea F-003). El código de arriba referencia
  el símbolo; F-001 introduce la firma pero la línea
  concreta se completa en F-003. Si se quiere que F-001
  compile de forma aislada, la línea puede ser un
  comentario `// SeedData.Apply(modelBuilder);` (temporal)
  hasta que F-003 lo active. Decisión a documentar en
  §17.

### RF-3 — `EfUnitOfWork`

- `public sealed class EfUnitOfWork : IUnitOfWork` en
  `src/ControlDeFinanzasIA.Infrastructure/Persistence/EfUnitOfWork.cs`.
- Implementa la interfaz `IUnitOfWork` definida en F-006
  de Fase 1 en
  `src/ControlDeFinanzasIA.Application/Abstractions/IUnitOfWork.cs`.
- Constructor:
  `public EfUnitOfWork(AppDbContext db) => _db = db;`
  (campo privado `private readonly AppDbContext _db;`).
- Método:
  ```csharp
  public Task<int> SaveChangesAsync(CancellationToken ct = default)
      => _db.SaveChangesAsync(ct);
  ```
- Devuelve `int` (filas afectadas) — el mismo contrato que
  la interfaz.

### RF-4 — `AddPersistence` extension method

- `public static class ServiceCollectionExtensions` en
  `src/ControlDeFinanzasIA.Infrastructure/Persistence/ServiceCollectionExtensions.cs`.
- Método:
  ```csharp
  public static IServiceCollection AddPersistence(
      this IServiceCollection services,
      IPathResolver pathResolver)
  {
      services.AddDbContext<AppDbContext>(opts =>
          opts.UseSqlite($"Data Source={pathResolver.DatabaseFilePath}"));
      services.AddScoped<IUnitOfWork, EfUnitOfWork>();
      // Repositories registered in F-004 of Phase 2.
      return services;
  }
  ```
- **Importante:** `AddPersistence` **NO** registra
  `IPathResolver` (ya está registrado en
  `Bootstrap.ConfigureServices` por F-008 de Fase 1). El
  caller (Bootstrap) lo resuelve y lo pasa como parámetro.
  Esto evita doble registro y mantiene la
  responsabilidad clara: `AddPersistence` solo añade
  servicios de persistencia, no de runtime.
- **Importante:** `AddPersistence` **NO** registra los
  15 repositorios concretos. Eso es F-004. La línea
  comentada `// Repositories registered in F-004 of Phase 2.`
  lo documenta.

### RF-5 — Lifetimes correctos

- `AppDbContext`: **Scoped**. EF Core lo requiere así
  (un DbContext por unidad de trabajo, para evitar
  concurrencia y problemas de cambio tracking).
  `services.AddDbContext<AppDbContext>(...)` ya lo
  registra como Scoped por convención.
- `IUnitOfWork`: **Scoped**. Mismo DbContext por scope.
  `services.AddScoped<IUnitOfWork, EfUnitOfWork>()`.
- `IPathResolver`: **Singleton** (ya registrado en F-008
  de Fase 1; F-001 no lo toca).

### RF-6 — Connection string

- `Data Source={pathResolver.DatabaseFilePath}`.
- String interpolation es OK para v1 (la ruta no tiene
  caracteres especiales en la práctica: es
  `%APPDATA%\ControlDeFinanzasIA\data.db` en Windows,
  similar en macOS/Linux). Si en el futuro la ruta
  pudiera tener caracteres especiales, se cambia a
  `SqliteConnectionStringBuilder` (F-019+).
- `pathResolver.DatabaseFilePath` devuelve
  `Path.Combine(DataDirectory, "data.db")` (de
  `SystemPathResolver` de F-008).

### RF-7 — Sin `<ProjectReference>` de `UI` a `Infrastructure`

- Esta feature **NO** añade la referencia. Es F-005.
- Verificación: tras F-001,
  `dotnet list src/ControlDeFinanzasIA.UI reference` debe
  seguir mostrando solo `Application` y `Domain`.
- Esta restricción valida la regla arquitectónica R-2 de
  F-007 ("Application solo depende de Domain") y la
  excepción del composition root de R-4 (que se activa
  en F-005).

---

## 7. Requisitos no funcionales

### RNF-1 — Compatibilidad con la arquitectura

- Después de F-001, **el grafo de dependencias sigue
  cumpliendo** las reglas R-1 a R-5 de F-007:
  - R-1 (Domain sin EF Core): `Domain` no toca
    `Microsoft.EntityFrameworkCore.*`. **Verificable:**
    `dotnet list src/ControlDeFinanzasIA.Domain package`
    debe seguir vacío.
  - R-2 (Application sin EF Core): `Application` no
    importa EF Core. **Verificable:** `dotnet list
    src/ControlDeFinanzasIA.Application package` debe
    seguir mostrando solo `FluentValidation`.
  - R-3 (Infrastructure → Application + Domain): ya
    estaba bien; F-001 no lo rompe.
  - R-4 (UI no → Infrastructure salvo composition root):
    F-001 no añade la referencia; sigue bien.
  - R-5 (src/ no → Tests): F-001 no lo cambia.
- Los 11 tests de NetArchTest de F-007 (R-1 a R-5) deben
  seguir pasando **sin modificaciones**.

### RNF-2 — Determinismo del DbContext

- `AppDbContext` no tiene estado estático.
- `ApplyConfigurationsFromAssembly` carga las
  configuraciones del assembly de forma determinista.
- `SeedData.Apply` (introducido en F-003, referenciado en
  F-001) usa Ids deterministas (no `Guid.NewGuid()`).

### RNF-3 — Velocidad

- `AddPersistence` añade < 10 ms al tiempo de arranque
  (el coste de registrar 2 servicios en DI es trivial).

### RNF-4 — Compatibilidad con .NET 8

- EF Core 8 es la versión alineada con .NET 8 LTS.
- No se introduce EF Core 9 (que requeriría .NET 9).

### RNF-5 — Sin scope creep

- F-001 no registra repositorios (F-004).
- F-001 no genera migraciones (F-002).
- F-001 no aplica migraciones al arranque (F-005).
- F-001 no añade `<ProjectReference>` de UI a
  Infrastructure (F-005).
- F-001 no añade tests de integración (F-006).

### RNF-6 — Mensajes y convenciones

- Nombres en inglés (convención de la comunidad .NET y
  de F-002 de Fase 1 §RNF-5).
- XML doc en cada clase y método público nuevo.
- File-scoped namespace (convención del proyecto, ver
  F-001 de Fase 1 §7.3).

---

## 8. Reglas de negocio

### RN-1 — `AppDbContext` es la única puerta de entrada a EF Core

- Cualquier acceso a la base de datos pasa por
  `AppDbContext` (sea vía repositorio o vía
  `IUnitOfWork.SaveChangesAsync`).
- No se crean otros `DbContext` en `Infrastructure`. Si
  en el futuro se necesita una segunda conexión
  (e.g., una DB de backups), se crea un segundo
  `DbContext` separado, no se reutiliza el primero.

### RN-2 — `AppDbContext` está registrado como Scoped

- Razón técnica: EF Core no es thread-safe a nivel de
  `DbContext`. Un `DbContext` por unidad de trabajo
  (un scope de DI) garantiza que un `DbContext` no se
  comparte entre hilos.
- Razón de diseño: los repositorios también son Scoped
  (comparten el `DbContext` con la unidad de trabajo).

### RN-3 — `IUnitOfWork` es Scoped y delega en `AppDbContext`

- `EfUnitOfWork` **NO** mantiene un DbContext propio;
  recibe el del container.
- En el futuro, si se necesita un "Unit of Work" más
  elaborado (con `BeginTransaction` / `Commit` /
  `Rollback` explícitos), se reemplaza esta
  implementación. Por ahora, `SaveChangesAsync` es
  suficiente para v1.

### RN-4 — `IPathResolver` se resuelve en el composition root, no en `AddPersistence`

- F-001 NO registra `IPathResolver` (ya lo hace F-008
  en `Bootstrap.ConfigureServices`).
- `AddPersistence` recibe `IPathResolver` como
  parámetro, resuelto por el caller.
- Esta separación de responsabilidades es coherente
  con la regla "Bootstrap es el composition root,
  AddPersistence es la extensión de DI para
  persistencia".

### RN-5 — `Domain` no conoce EF Core

- Refuerzo de la regla R-1 de F-007.
- Las entidades de Domain (`Category`, `PaymentMethod`)
  son POCOs puros. Las configuraciones EF Core viven
  en `Infrastructure` (en F-002).

---

## 9. Modelo de dominio

No se introducen nuevas entidades de dominio. Lo que
añade F-001 es un modelo de **infraestructura**:

```
src/
└── ControlDeFinanzasIA.Infrastructure/
    └── Persistence/                              ← nuevo
        ├── AppDbContext.cs                       ← clase
        ├── EfUnitOfWork.cs                       ← clase
        └── ServiceCollectionExtensions.cs        ← clase estática
```

**`AppDbContext`:**
- Hereda de `DbContext` (no `IdentityDbContext`; sin auth
  en v1).
- Constructor: `public AppDbContext(DbContextOptions<AppDbContext> options)`.
- 2 `DbSet<>`: `Categories`, `PaymentMethods`.
- `OnModelCreating` aplica configuraciones del assembly
  y llama a `SeedData.Apply` (introducido en F-003).

**`EfUnitOfWork`:**
- Implementa `IUnitOfWork` (de F-006 de Fase 1).
- Un solo método: `SaveChangesAsync` que delega en el
  `AppDbContext` inyectado.

**`ServiceCollectionExtensions`:**
- Clase estática con un método de extensión:
  `AddPersistence(IServiceCollection, IPathResolver)`.
- Registra `AppDbContext` (Scoped, vía `AddDbContext`) e
  `IUnitOfWork` (Scoped, vía `AddScoped`).

---

## 10. Configuración técnica

### 10.1 — Paquetes (CPM)

Ya detallados en §6 RF-1. Resumen:

```xml
<!-- En Directory.Packages.props -->
<PackageVersion Include="Microsoft.EntityFrameworkCore" Version="8.0.x" />
<PackageVersion Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.0.x" />
<PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.x" />
<PackageVersion Include="Microsoft.EntityFrameworkCore.Relational" Version="8.0.x" />
```

```xml
<!-- En Infrastructure.csproj -->
<PackageReference Include="Microsoft.EntityFrameworkCore" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" />
```

```xml
<!-- En Tests.Integration.csproj -->
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" />
```

### 10.2 — Versión de EF Core

- **Default recomendado:** la última 8.0.x estable.
- F-001 no exige una versión específica. El implementador
  confirma la versión vía Context7 antes de añadir los
  `PackageVersion`.
- Si en el futuro la versión cambia (e.g., 8.0.12), se
  actualiza en una PR con su propia revisión. No se
  mezclan upgrades de versión con otras features.

### 10.3 — Conversión de value objects (FUERA de esta feature)

- `Category` y `PaymentMethod` no tienen campos `Money` o
  `DateRange`. No se introduce ningún `ValueConverter` en
  F-001.
- Cuando F-002/F-003 de Fase 3 introduzcan `Expense`
  (con `Amount: Money`), se creará un
  `MoneyValueConverter : ValueConverter<Money, decimal>`
  (o similar) y se registrará en
  `AppDbContext.OnModelCreating`. Ese es tema de Fase 3.

### 10.4 — Convenciones de código

- File-scoped namespace (proyecto rule; bug de Fase 0 ya
  corregido).
- `using` directives al tope del archivo (NUNCA después
  del namespace).
- XML doc en cada clase y método público nuevo.
- `sealed` en clases que no se heredan (`EfUnitOfWork`,
  `ServiceCollectionExtensions`).
- `AppDbContext` **NO** `sealed` (convención de EF Core).

### 10.5 — Logging (FUERA de esta feature)

- F-001 no configura `ILoggerFactory` para EF Core. La
  spec de fase 2 §4 dice que el logging fino se
  introduce "en la fase de wiring" (F-005, cuando
  `App.axaml.cs` aplique migraciones al arranque).
- Si el implementador quiere ver logs de SQL durante
  el desarrollo, puede usar
  `OnConfiguring(DbContextOptionsBuilder)` con
  `optionsBuilder.LogTo(Console.WriteLine)` solo en
  Debug. Esto es opcional y no se commitea (o se
  commitea detrás de `#if DEBUG`).

---

## 11. Validaciones

F-001 no introduce reglas de validación de negocio (las
entidades son POCOs y los repositorios concretos vienen
en F-004). Lo que F-001 valida es:

| # | Validación | Cuándo se aplica | Comportamiento esperado |
| --- | --- | --- | --- |
| V-1 | `pathResolver` no es `null` en `AddPersistence` | Al llamar al método | `ArgumentNullException.ThrowIfNull(pathResolver);` (defensivo). |
| V-2 | `services` no es `null` en `AddPersistence` | Al llamar al método | `ArgumentNullException.ThrowIfNull(services);` (defensivo). |
| V-3 | EF Core carga la configuración del SQLite provider | Al construir `AppDbContext` por primera vez | Sin excepciones; las queries se ejecutan contra `data.db`. |
| V-4 | `IUnitOfWork.SaveChangesAsync` no se invoca sin entidad añadida | En cualquier use case futuro | Devuelve 0 (filas afectadas), sin excepción. |

---

## 12. Casos borde

### EC-1 — Connection string con caracteres especiales

- `pathResolver.DatabaseFilePath` se construye con
  `Path.Combine(DataDirectory, "data.db")`. `DataDirectory`
  se resuelve según el SO (F-008). En la práctica no
  tiene caracteres especiales.
- **Si los tuviera** (e.g., `%APPDATA%` con un username
  con acentos o emojis en el futuro), la string
  interpolation falla con un connection string
  inválido.
- **Mitigación futura** (no en F-001): usar
  `SqliteConnectionStringBuilder` con
  `DataSource = pathResolver.DatabaseFilePath`. F-001
  acepta el riesgo (no es un caso real en v1).

### EC-2 — Archivo `data.db` corrupto o con schema viejo

- Si el usuario tiene un `data.db` con un schema de una
  versión anterior, `Database.Migrate()` (llamado en
  F-005) intenta aplicar las migraciones pendientes.
- **Si el schema es incompatible** (caso de "downgrade"
  o archivo corrupto), la migración falla con
  `SqliteException` y la app no arranca.
- **Mitigación:** F-019 (Backup) introducirá
  auto-backup antes de migraciones destructivas.
  F-001 no aborda esto.
- **Detección en F-001:** los tests de F-006 pueden
  forzar este caso creando un `data.db` con schema
  inválido y verificando que `Migrate` falla
  limpiamente.

### EC-3 — Permisos del directorio de datos

- `SystemPathResolver` (F-008) crea el directorio en su
  constructor y captura `UnauthorizedAccessException`.
  Si no puede crearlo, la app no arranca.
- **F-001 no cambia este comportamiento.** La conexión
  SQLite falla si el directorio no existe (cualquier
  intento de `Migrate` o query).

### EC-4 — `AddPersistence` llamado dos veces

- Si por error se llama a `AddPersistence` dos veces
  (e.g., en tests que construyen el container
  manualmente), EF Core registra dos `DbContextOptions`
  para el mismo tipo. El último gana.
- **Mitigación:** no es un caso de usuario; es un
  caso de developer. Si surge, se documenta en un
  test.

### EC-5 — `SeedData` aún no existe cuando se compila F-001

- F-001 referencia `SeedData.Apply(modelBuilder)` en
  `OnModelCreating`. Pero F-003 (que crea `SeedData`)
  viene después.
- **Decisión:** dos opciones:
  1. **F-001 introduce `SeedData` como stub vacío**
     (clase estática con método `Apply` que no hace
     nada). F-003 lo completa. Esto mantiene F-001
     compilable de forma aislada.
  2. **F-001 comenta la línea** `// SeedData.Apply(modelBuilder);`
     hasta que F-003 la active. F-001 compila sin
     seed (la app arranca, el `data.db` se crea con
     las tablas pero vacío).
- **Decisión recomendada:** opción 1 (stub vacío).
  Razón: mantiene `OnModelCreating` con la firma
  completa, y F-003 solo añade contenido al stub
  sin tocar `AppDbContext`. Menos acoplamiento entre
  features.

### EC-6 — EF Core 8.0.x incompatible con SDK instalado

- Si el SDK de .NET no es 8.0.x, EF Core 8 puede
  no restaurar. **Verificar con**
  `dotnet --version` y `dotnet ef --version` (cuando
  se instale el tool en F-002) antes de empezar.

---

## 13. Dependencias

### 13.1 Prerrequisitos de implementación

- **F-001 a F-008 de Phase 1** (todas completadas):
  - `IPathResolver` está definido en
    `Application/Abstractions/` (F-004).
  - `SystemPathResolver` está implementado en
    `UI/System/` (F-008) y registrado en
    `Bootstrap.ConfigureServices` (F-008).
  - `IUnitOfWork` está definido en
    `Application/Abstractions/` (F-006).
  - `Bootstrap.ConfigureServices` está listo para
    recibir una llamada a `AddPersistence` (F-005 lo
    hará).
  - El `.gitignore` excluye `*.db` (F-001 de Fase 1).
  - El test R-1 de F-007 está listo para verificar
    que `Domain` no se contamina con EF Core.

- **Las 17 entidades de Domain** existen (F-002 de Fase
  1 creó los placeholders; F-006 los consolidó). De
  estas, F-001 referencia `Category` y `PaymentMethod`.

### 13.2 Dependencias con otras features

| Feature | Relación |
| --- | --- |
| F-002 (Phase 2) | Necesita `AppDbContext` y `EfUnitOfWork` para crear las configuraciones y generar la primera migración. |
| F-003 (Phase 2) | Necesita `AppDbContext` y `SeedData.Apply` (referenciado en `OnModelCreating`). F-001 introduce `SeedData` como stub vacío (ver EC-5). |
| F-004 (Phase 2) | Necesita `AppDbContext` y `IUnitOfWork` para implementar los repositorios concretos. F-001 NO registra los repos (lo hace F-004 con llamadas adicionales a `AddPersistence` o en un método separado). |
| F-005 (Phase 2) | Necesita `AddPersistence` para enchufarlo en `Bootstrap.ConfigureServices` y aplicar migraciones al arranque. F-005 también añade el `<ProjectReference>` de `UI` a `Infrastructure`. |
| F-006 (Phase 2) | Testa `AppDbContext` y los repositorios con SQLite in-memory. Necesita `Microsoft.EntityFrameworkCore.Sqlite` en `Tests.Integration.csproj` (F-001 lo añade). |
| F-007 (Phase 1) | El test R-1 verifica que F-001 no contaminó `Domain` con EF Core. |

### 13.3 Bloqueos

- **Ninguno.** Todos los prerrequisitos están
  cumplidos. F-001 puede implementarse ahora mismo.

---

## 14. Fuera de alcance (detallado)

Repetición con énfasis. F-001 **NO** hace lo siguiente:

- **Implementar repositorios concretos.** Es F-004.
- **Crear configuraciones de entidades.** Es F-002.
- **Generar la primera migración.** Es F-002.
- **Crear el seed data.** Es F-003.
- **Aplicar migraciones al arranque.** Es F-005.
- **Wiring del `AddPersistence` en el composition
  root.** Es F-005.
- **`<ProjectReference>` de `UI` a `Infrastructure`.** Es
  F-005.
- **Herramienta `dotnet ef` CLI.** Es F-002.
- **Logging de queries SQL.** Es F-005+.
- **Conversión de value objects (`Money`, `DateRange`)
  en EF Core.** Es Fase 3.
- **Tests de integración** de la persistencia. Es
  F-006.
- **Modificaciones a `Domain`, `Application`, `UI`,
  `Tests.Unit`.** F-001 solo toca `Infrastructure`,
  `Tests.Integration`, y `Directory.Packages.props`.
- **Lock file para multi-proceso.** Es ADR-013,
  fuera de v1.
- **Cifrado del archivo `.db`.** Fuera de v1 (el SO
  puede cifrar el directorio si el usuario lo
  desea).
- **Auto-backup antes de migraciones destructivas.**
  Es F-019.
- **Cualquier otra lógica de aplicación.** F-001 es
  infraestructura pura.

---

## 15. Criterios de aceptación

La feature se considera completada cuando **todos** los
siguientes puntos se cumplen.

### AC-1 — Paquetes instalados

- [ ] `Directory.Packages.props` contiene 4
      `<PackageVersion>` con versión exacta (8.0.x)
      para:
      - `Microsoft.EntityFrameworkCore`
      - `Microsoft.EntityFrameworkCore.Sqlite`
      - `Microsoft.EntityFrameworkCore.Design`
      - `Microsoft.EntityFrameworkCore.Relational`
- [ ] `Infrastructure.csproj` referencia 3 paquetes
      EF Core (Core, Sqlite, Design) sin `Version`.
- [ ] `Tests.Integration.csproj` referencia
      `Microsoft.EntityFrameworkCore.Sqlite` sin
      `Version`.
- [ ] `dotnet restore` desde la raíz pasa sin
      errores.
- [ ] `dotnet list src/ControlDeFinanzasIA.Infrastructure package`
      muestra los 3 paquetes EF Core.

### AC-2 — Compilación

- [ ] `dotnet build -c Release` desde la raíz: 0
      warnings, 0 errors.
- [ ] `dotnet build -c Debug` desde la raíz: 0
      warnings, 0 errors.

### AC-3 — Tests existentes verdes

- [ ] `dotnet test` desde la raíz: 111 tests pasan
      (100 originales + 11 de F-007), 0 failures.
- [ ] El test R-1 de F-007 sigue pasando
      (`Domain` no depende de EF Core ni de otros
      paquetes prohibidos).

### AC-4 — `AppDbContext` existe y compila

- [ ] `src/ControlDeFinanzasIA.Infrastructure/Persistence/AppDbContext.cs`
      existe.
- [ ] `public class AppDbContext : DbContext` con
      constructor público
      `public AppDbContext(DbContextOptions<AppDbContext> options)`.
- [ ] 2 `DbSet<>`:
      `public DbSet<Category> Categories` y
      `public DbSet<PaymentMethod> PaymentMethods`.
- [ ] `OnModelCreating` llama a
      `ApplyConfigurationsFromAssembly` y a
      `SeedData.Apply` (el último referencia el stub
      creado en F-001, que F-003 completa).

### AC-5 — `EfUnitOfWork` existe y compila

- [ ] `src/ControlDeFinanzasIA.Infrastructure/Persistence/EfUnitOfWork.cs`
      existe.
- [ ] `public sealed class EfUnitOfWork : IUnitOfWork`.
- [ ] Constructor que recibe `AppDbContext`.
- [ ] `public Task<int> SaveChangesAsync(CancellationToken ct = default)`
      que delega en `_db.SaveChangesAsync(ct)`.

### AC-6 — `AddPersistence` existe y compila

- [ ] `src/ControlDeFinanzasIA.Infrastructure/Persistence/ServiceCollectionExtensions.cs`
      existe.
- [ ] `public static class ServiceCollectionExtensions`.
- [ ] Método de extensión:
      `public static IServiceCollection AddPersistence(this IServiceCollection services, IPathResolver pathResolver)`.
- [ ] Registra `AppDbContext` con
      `services.AddDbContext<AppDbContext>(opts => opts.UseSqlite($"Data Source={pathResolver.DatabaseFilePath}"))`.
- [ ] Registra `IUnitOfWork` con
      `services.AddScoped<IUnitOfWork, EfUnitOfWork>();`.
- [ ] **NO** registra `IPathResolver` (ya está en
      `Bootstrap.ConfigureServices`).
- [ ] **NO** registra los 15 repositorios (F-004).

### AC-7 — Sin `<ProjectReference>` de UI a Infrastructure

- [ ] `dotnet list src/ControlDeFinanzasIA.UI reference`
      sigue mostrando solo `Application` y `Domain`.
- [ ] **NO** se ha añadido
      `<ProjectReference Include="..\ControlDeFinanzasIA.Infrastructure\..." />`
      en `UI.csproj`.
- [ ] El test R-4 de F-007 sigue pasando
      (`UI_No_Referencia_A_Infrastructure_Salvo_CompositionRoot`).

### AC-8 — `SeedData` stub

- [ ] `src/ControlDeFinanzasIA.Infrastructure/Persistence/SeedData.cs`
      existe con la firma
      `public static class SeedData` y
      `public static void Apply(ModelBuilder modelBuilder)`.
- [ ] El cuerpo de `Apply` es trivial (vacío o con un
      comentario que indique "F-003 implementa el seed").
- [ ] F-003 reemplaza el contenido sin tocar la firma.

### AC-9 — Convenciones del repo

- [ ] File-scoped namespace en los 4 archivos
      nuevos.
- [ ] `using` directives al tope (no después del
      namespace).
- [ ] XML doc en cada clase y método público nuevo.
- [ ] `sealed` en clases que no se heredan
      (`EfUnitOfWork`, `ServiceCollectionExtensions`,
      `SeedData`).
- [ ] `AppDbContext` **NO** `sealed` (convención EF
      Core).
- [ ] `dotnet format style --verify-no-changes`: exit 0.

### AC-10 — Documentación y trazabilidad

- [ ] `spec/phases/phase-02/features.md` actualizado:
      F-001 marcada como ✅ COMPLETADA en la tabla
      resumen y en el header de la sección.
- [ ] La spec se renombra a
      `spec/features/phase-02/F-001-appdbcontext-addpersistence.done.md`.

---

## 16. Estructura de archivos esperada

```
src/
└── ControlDeFinanzasIA.Infrastructure/
    └── Persistence/                                ← nuevo (F-001)
        ├── AppDbContext.cs                         ← F-001
        ├── EfUnitOfWork.cs                         ← F-001
        ├── SeedData.cs                             ← F-001 (stub), F-003 (completo)
        └── ServiceCollectionExtensions.cs          ← F-001

tests/
└── ControlDeFinanzasIA.Tests.Integration/
    └── (sin cambios estructurales en F-001;
         el csproj se modifica para añadir
         Microsoft.EntityFrameworkCore.Sqlite)

Directory.Packages.props                             ← modificado (4 PackageVersion)
```

**Modificaciones a `.csproj`:**
- `Infrastructure.csproj`: +3 `<PackageReference>` (Core,
  Sqlite, Design).
- `Tests.Integration.csproj`: +1 `<PackageReference>`
  (Sqlite).
- `UI.csproj`: **NO** se modifica (F-005 introduce la
  referencia a `Infrastructure`).
- `Domain.csproj`, `Application.csproj`, `Tests.Unit.csproj`:
  **NO** se modifican.

---

## 17. Decisiones de diseño explícitas

### 17.1 — Versión de EF Core 8

**Decisión:** la última 8.0.x estable al momento de
implementar. Confirmada vía Context7. Default de
referencia: 8.0.10 u 8.0.11 (releases estables
compatibles con .NET 8).

**Justificación:** EF Core 8 está alineado con .NET 8
LTS. No se introduce EF Core 9 (requeriría .NET 9).

### 17.2 — `AddPersistence` recibe `IPathResolver` por parámetro, no del container

**Decisión:** el caller (Bootstrap) resuelve
`IPathResolver` desde su Singleton (registrado por
F-008) y lo pasa a `AddPersistence` por parámetro.

**Justificación:**
- `IPathResolver` ya está registrado en
  `Bootstrap.ConfigureServices` (F-008). Re-registrarlo
  en `AddPersistence` es redundante.
- Pasar por parámetro hace la dependencia
  explícita y testeable (los tests pueden pasar un
  `FakePathResolver` sin tocar el container).
- Mantiene la separación: Bootstrap es el composition
  root (registra runtime), `AddPersistence` es la
  extensión de DI para persistencia (registra EF Core).

**Alternativa descartada:** usar `IServiceProvider` o
`IServiceCollection` para resolver `IPathResolver`
dentro de `AddPersistence`. Más complejo, no aporta
beneficio.

### 17.3 — `AppDbContext` no es `sealed`

**Decisión:** `public class AppDbContext : DbContext`
(sin `sealed`).

**Justificación:** convención de EF Core. Algunos
escenarios (tests avanzados, intercepciones de
mapeo) pueden requerir herencia. Aunque F-006 (Phase
2) usará SQLite in-memory con
`DbContextOptionsBuilder`, no se descarta herencia en
el futuro.

**Alternativa descartada:** `sealed class`. Más
restrictivo; pierde flexibilidad sin ganancia
inmediata.

### 17.4 — `EfUnitOfWork` es `sealed`

**Decisión:** `public sealed class EfUnitOfWork :
IUnitOfWork`.

**Justificación:** la implementación es trivial
(delega en `AppDbContext.SaveChangesAsync`). No
hay razón para herencia. `sealed` comunica la
intención.

### 17.5 — `SeedData` como stub vacío en F-001

**Decisión:** F-001 introduce `SeedData.cs` con la
firma `public static void Apply(ModelBuilder
modelBuilder)` pero cuerpo trivial (vacío o
comentario). F-003 lo completa con las 9 categorías
y 5 métodos de pago.

**Justificación:** evita que `AppDbContext.OnModelCreating`
quede con una referencia a un símbolo inexistente
(rompería la compilación de F-001). Permite que F-001
compile de forma aislada.

**Alternativa descartada:** comentar la línea
`// SeedData.Apply(modelBuilder);` en F-001 y
descomentarla en F-003. Más simple en código, pero
hace que F-001 dependa del orden de implementación
de F-003 (no compila si F-003 no se hace primero).
Con el stub, F-001 compila siempre.

### 17.6 — Connection string con string interpolation

**Decisión:** `$"Data Source={pathResolver.DatabaseFilePath}"`.

**Justificación:** simple, suficiente para v1. La
ruta no tiene caracteres especiales en la práctica.

**Alternativa descartada:** `SqliteConnectionStringBuilder`
con `DataSource = ...`. Más robusto, pero overkill
para v1. Si en el futuro la ruta pudiera tener
caracteres especiales, se cambia en F-019+.

### 17.7 — Sin `IDbContextFactory<T>` en F-001

**Decisión:** F-001 NO introduce
`IDbContextFactory<AppDbContext>`.

**Justificación:** el patrón estándar de EF Core es
registrar el `DbContext` directamente. F-006 (Phase
2) usará `DbContextOptionsBuilder` con
`UseSqlite(connection)` para tests, no
necesariamente `IDbContextFactory`.

**Alternativa descartada:** registrar
`IDbContextFactory<AppDbContext>` para tests
paralelos. Más complejo; F-001 no lo necesita y
F-006 puede decidir si lo introduce.

### 17.8 — Sin configuración de logging en F-001

**Decisión:** F-001 no configura `ILoggerFactory` ni
`LogTo` para EF Core. El logging fino de queries
queda para F-005 (cuando se aplica el
composition root completo).

**Justificación:** la spec de fase 2 §4 lo
especifica así. El logging fino de EF Core es
útil durante el desarrollo, pero no es parte
del producto final (en producción, no se
quiere inundar la consola con cada query).

**Si el implementador quiere ver logs durante
el desarrollo** (opcional, no se commitea):
puede añadir `LogTo(Console.WriteLine)` en
`OnConfiguring` detrás de `#if DEBUG`. La spec no
lo exige.

---

## 18. Riesgos y mitigaciones

| Riesgo | Probabilidad | Impacto | Mitigación |
| --- | --- | --- | --- |
| Versión de EF Core 8.0.x incompatible con el SDK .NET 8.0.x instalado. | Baja | Alto | Verificar con `dotnet --version` antes de empezar. Si hay mismatch, ajustar versión. |
| La string interpolation del connection string falla con rutas con caracteres especiales. | Baja | Bajo | F-001 acepta el riesgo (no es un caso real en v1). F-019+ usa `SqliteConnectionStringBuilder` si surge. |
| `SeedData` no compila porque F-003 aún no existe. | Baja (resuelto por EC-5) | Bajo | F-001 introduce `SeedData` como stub vacío. |
| Tests de NetArchTest (F-007) fallan porque `Infrastructure` ahora referencia EF Core. | Baja | Medio | R-1 a R-5 de F-007 validan que `Application` y `Domain` no se contaminan. `Infrastructure` puede tener EF Core (es su trabajo). AC-3 verifica. |
| `dotnet ef` no se puede ejecutar en F-002 porque la herramienta no está instalada. | Media (en F-002, no en F-001) | Bajo | F-002 lo aborda. F-001 solo añade paquetes, no ejecuta el tool. |
| Doble registro de `IPathResolver` si F-005 lo añade también. | Baja (mitigado por RN-4) | Bajo | F-001 NO registra `IPathResolver`; F-005 tampoco (Bootstrap ya lo hace). `AddPersistence` solo recibe el path resolver como parámetro. |
| `OnModelCreating` referencia `SeedData` que no existe si F-001 no introduce el stub. | Media (resuelto por EC-5) | Alto | F-001 introduce el stub vacío. F-003 lo completa. |
| Convención de "no `<ProjectReference>` UI → Infrastructure" se rompe por error en F-001. | Baja | Alto | F-001 NO toca `UI.csproj`. AC-7 verifica con `dotnet list reference`. |

---

## 19. Referencias

- `docs/roadmap.md` — Fase 2 (Entregables esperados,
  Riesgos principales, Criterios de fase completada).
- `docs/architecture.md` — §3 (solución y proyectos),
  §4 (grafo de dependencias), §8 (capa de
  Infrastructure: persistencia, repositorios,
  filesystem, logging), §12 (ruta del archivo de
  datos).
- `docs/vision.md` — §4.10 (Datos y portabilidad).
- `spec/constitution/tech-stack.md` — paquetes,
  convenciones de código, CPM.
- `spec/constitution/mission.md` — Clean
  Architecture, principios.
- `spec/phases/phase-02/features.md` — F-001 dentro
  del desglose de la fase 2.
- `spec/features/phase-01/F-001-skeleton-solution.done.md`
  — convenciones de `.editorconfig`, `stylecop.json`,
  CPM, y `.gitignore` (que excluye `*.db`).
- `spec/features/phase-01/F-004-Abstracciones-clock-pathresolver.done.md`
  — provee `IPathResolver` y `IClock` que F-001
  consume.
- `spec/features/phase-01/F-006-contrato-interfaces-repo.done.md`
  — provee `IUnitOfWork` que F-001 implementa, y las
  15 interfaces de repositorio (F-001 NO las
  implementa, pero `AppDbContext` las referencia
  indirectamente).
- `spec/features/phase-01/F-007-validacion-arquitectonica.done.md`
  — los tests R-1 a R-5 que F-001 no debe romper.
- `spec/features/phase-01/F-008-composition-root.done.md`
  — el `Bootstrap.ConfigureServices` y el
  `SystemPathResolver` que F-005 enchufa.
- `spec/README.md` — convención del directorio
  `spec/`.
- EF Core 8 documentation oficial (a consultar vía
  Context7 cuando se implemente).
