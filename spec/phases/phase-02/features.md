# Fase 2 — Features: Persistencia con EF Core + SQLite

> Descomposición de la **Fase 2** del `docs/roadmap.md` en features
> independientes, cada una candidata a una Specification propia.
>
> **Ubicación de este documento:** `spec/phases/phase-02/features.md`.
> Las specs de cada feature viven en `spec/features/phase-02/`.
> Ver `spec/README.md` para la convención completa del repo.
>
> Fuente de verdad: `docs/vision.md`, `docs/architecture.md`,
> `docs/roadmap.md`, `spec/constitution/tech-stack.md`,
> `spec/constitution/mission.md`,
> `spec/phases/phase-01/features.md` (features de la fase 1 ya
> completadas, sobre las que Fase 2 se construye).
>
> **Reglas de la fase:**
> - Cada feature deja la solución compilando.
> - Cada feature es una capacidad única.
> - Las dependencias entre features se indican explícitamente.
> - Una feature completada = sus criterios de cierre cumplidos.
>
> **Nota sobre archivos heredados:** existe un borrador previo
> en `spec/features/phase-02/F-002-persistence-efcore-sqlite.md`
> que cubría toda la fase como una sola feature. Ese
> documento está **desactualizado** (asumía que F-004, F-006 y
> F-008 de Phase 1 no se habían implementado; por ejemplo,
> definía `ICategoryRepository` con 7 métodos en vez de los
> 5 que F-006 ya consolidó). El presente documento
> **lo supersede** y es la fuente de verdad. El borrador
> previo puede moverse a `.archive/` o eliminarse cuando
> se confirme.

---

## Resumen

| ID | Nombre | Prioridad | Estimación | Depende de | Estado |
| --- | --- | --- | --- | --- | --- |
| 001 | AppDbContext + paquetes EF Core + composition root extendido | Crítica | M | — | ✅ COMPLETADA |
| 002 | Configuraciones de entidades + migración inicial | Crítica | M | 001 | ✅ COMPLETADA |
| 003 | Seed data: 9 categorías y 5 métodos de pago del sistema | Alta | S | 002 | ✅ COMPLETADA |
| 004 | Implementación de `CategoryRepository` y `PaymentMethodRepository` | Alta | M | 001, 002, 003 | ✅ COMPLETADA |
| 005 | Composition root: registrar persistencia + aplicar migraciones al arranque | Crítica | M | 001, 002, 004 | ✅ COMPLETADA |
| 006 | Tests de integración con SQLite in-memory | Alta | M | 001, 002, 003, 004 | ✅ COMPLETADA |

**Tamaño total estimado:** 1–2 semanas de trabajo para una
persona. **MVP de la fase:** 001 + 002 + 003 + 004 + 005
(persistencia funcionando en la app). El 006 (tests) es
necesario para AC pero la app puede correr sin él en
desarrollo.

**Estado de Phase 1 (prerrequisito):** las 8 features de
Fase 1 están completadas (`spec/phases/phase-01/features.md`
lo confirma). Esto significa que F-002 de Fase 2 hereda:
- `AppDbContext` se registra en el composition root
  (`UI/Composition/Bootstrap.cs`).
- `IPathResolver` ya existe y `SystemPathResolver` ya está
  implementado en `UI/System/` (de F-008). El composition
  root lo resuelve y lo pasa a `AddPersistence`.
- `IClock` ya existe y `SystemClock` ya está implementado
  (de F-008). F-002 de Fase 2 no lo necesita directamente
  pero queda disponible para tests.
- Las 15 interfaces de repositorio de F-006 ya están en
  `Application/Abstractions/`. `ICategoryRepository` y
  `IPaymentMethodRepository` ya tienen la firma
  estandarizada de 5 métodos (`Add/Update/Remove/GetById/GetAll`).
  Fase 2 implementa los repositorios concretos para esas
  dos interfaces; los otros 13 quedan para las fases
  correspondientes (F-003+ del roadmap).
- `IUnitOfWork` ya está definido en
  `Application/Abstractions/` con `SaveChangesAsync`.
- El `.gitignore` ya excluye `*.db`, `*.db-wal`, `*.db-shm`
  (de F-001).
- El CI workflow ya corre `dotnet build` y `dotnet test`
  (de F-001 + T-010a).

---

## F-001 — AppDbContext + paquetes EF Core + composition root extendido — ✅ COMPLETADA

**Objetivo**
Introducir la dependencia a Entity Framework Core 8 y SQLite en
el proyecto, definir el `AppDbContext` con los `DbSet<>` para
las dos entidades mínimas (Category y PaymentMethod), y dejar
el extension method `AddPersistence` listo para que F-005 lo
enchufe en el composition root de F-008.

**Descripción breve**
- Añadir a `Directory.Packages.props` (CPM) los 4 paquetes
  NuGet con versiones exactas (sin rangos):
  - `Microsoft.EntityFrameworkCore` (8.0.x).
  - `Microsoft.EntityFrameworkCore.Sqlite` (8.0.x).
  - `Microsoft.EntityFrameworkCore.Design` (8.0.x) — tooling
    para `dotnet ef migrations`.
  - `Microsoft.EntityFrameworkCore.Relational` (8.0.x) —
    transitivo de Sqlite pero se fija explícitamente.
- Añadir `<PackageReference>` (sin `Version`, vía CPM) en:
  - `src/ControlDeFinanzasIA.Infrastructure/ControlDeFinanzasIA.Infrastructure.csproj`:
    `Microsoft.EntityFrameworkCore`,
    `Microsoft.EntityFrameworkCore.Sqlite`,
    `Microsoft.EntityFrameworkCore.Design`.
  - `tests/ControlDeFinanzasIA.Tests.Integration/ControlDeFinanzasIA.Tests.Integration.csproj`:
    `Microsoft.EntityFrameworkCore.Sqlite` (para los tests
    de F-006 de Phase 2).
- Crear `src/ControlDeFinanzasIA.Infrastructure/Persistence/AppDbContext.cs`:
  - `public class AppDbContext : DbContext`.
  - Constructor público que recibe
    `DbContextOptions<AppDbContext> options`.
  - `DbSet<Category> Categories => Set<Category>();`
  - `DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();`
  - `OnModelCreating(ModelBuilder modelBuilder)` que
    llama a
    `modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);`
    y luego a
    `SeedData.Apply(modelBuilder);`
    (SeedData se implementa en F-003).
- Crear
  `src/ControlDeFinanzasIA.Infrastructure/Persistence/ServiceCollectionExtensions.cs`:
  - Clase estática con método de extensión
    `AddPersistence(this IServiceCollection services, IPathResolver pathResolver)`.
  - Registra `AppDbContext` con
    `services.AddDbContext<AppDbContext>(opts => opts.UseSqlite($"Data Source={pathResolver.DatabaseFilePath}"));`.
  - Registra `IUnitOfWork` con
    `services.AddScoped<IUnitOfWork, EfUnitOfWork>();`.
  - Registra los repositorios (placeholder por ahora, F-004
    los implementa de verdad).
- Crear `src/ControlDeFinanzasIA.Infrastructure/Persistence/EfUnitOfWork.cs`:
  - `public sealed class EfUnitOfWork : IUnitOfWork`.
  - Constructor que recibe `AppDbContext`.
  - `public Task<int> SaveChangesAsync(CancellationToken ct = default)
    => _db.SaveChangesAsync(ct);`
- NO se añade `<ProjectReference>` desde `UI` a
  `Infrastructure` todavía (eso es F-005; evita deuda
  técnica).

**Justificación**
Toda feature de Fase 3+ que perdure datos pasa por aquí.
Definir el `AppDbContext` y la convención de DI ahora evita
el retrofit doloroso de "ahora muevo todo a Infrastructure".
F-002 de Fase 2 es la **primera incursión real del proyecto
en lógica de aplicación** (Phase 1 fue cimientos).

**Dependencias**
- F-001 a F-008 de Phase 1 (todas completadas, dan el
  `IPathResolver` y el `IUnitOfWork` que esta feature
  consume).
- F-006 de Phase 1 en particular (define las 15
  interfaces, incluyendo `ICategoryRepository` y
  `IPaymentMethodRepository` con la firma estandarizada).

**Prioridad**
- Crítica. Bloquea F-002 a F-006 de esta fase.

**Estimación**
- M (medio día a un día).

**Criterios para considerarla completada**
- `Directory.Packages.props` contiene los 4 `PackageVersion`
  con versión exacta.
- `Infrastructure.csproj` referencia los 3 paquetes EF Core
  sin `Version`.
- `Tests.Integration.csproj` referencia
  `Microsoft.EntityFrameworkCore.Sqlite` sin `Version`.
- `AppDbContext`, `ServiceCollectionExtensions` y
  `EfUnitOfWork` existen y compilan.
- `dotnet build` desde la raíz pasa con 0 warnings, 0 errors.
- `dotnet test` desde la raíz pasa: 111 tests existentes
  (los 100 originales + 11 de F-007 NetArchTest) verdes.
- Los tests de NetArchTest R-1 a R-5 siguen pasando (no
  se rompió la arquitectura al meter EF Core en
  `Infrastructure`; `Application` sigue sin referencia a
  `Infrastructure`; `Domain` sigue sin paquetes externos).

---

## F-002 — Configuraciones de entidades + migración inicial — ✅ COMPLETADA

**Objetivo**
Definir las configuraciones Fluent API de EF Core para
`Category` y `PaymentMethod` (en archivos separados,
siguiendo `architecture.md` §8: "una clase por entidad,
`IEntityTypeConfiguration<T>`"), y generar la primera
migración versionada (`InitialSchema`).

**Descripción breve**
- Crear
  `src/ControlDeFinanzasIA.Infrastructure/Persistence/Configurations/CategoryConfiguration.cs`:
  - `public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>`.
  - `Configure(EntityTypeBuilder<Category> b)`:
    - `b.ToTable("Categories")` (PascalCase, coherente con
      el código C#).
    - `b.HasKey(c => c.Id)`.
    - `b.Property(c => c.Id).ValueGeneratedNever()` (Guid
      generado en Domain, no en DB).
    - `b.Property(c => c.Name).IsRequired().HasMaxLength(100).UseCollation("NOCASE")`.
    - `b.HasIndex(c => c.Name).IsUnique()`.
    - `b.Property(c => c.IsSystem).IsRequired()`.
    - `b.Property(c => c.CreatedAt).IsRequired()`.
    - `b.Property(c => c.UpdatedAt).IsRequired()`.
    - `b.Property(c => c.Color).HasMaxLength(9)`.
    - `b.Property(c => c.Icon).HasMaxLength(50)`.
- Crear
  `src/ControlDeFinanzasIA.Infrastructure/Persistence/Configurations/PaymentMethodConfiguration.cs`:
  - Idéntica estructura que `CategoryConfiguration` pero
    sobre `PaymentMethod` (sin `Color`/`Icon`).
- Instalar la herramienta `dotnet-ef` localmente (no
  globalmente) con
  `dotnet new tool-manifest` (si no existe) y
  `dotnet tool install dotnet-ef --version 8.0.x`.
- Generar la migración:
  `dotnet ef migrations add InitialSchema --project src/ControlDeFinanzasIA.Infrastructure --startup-project src/ControlDeFinanzasIA.Infrastructure`.
- Verificar que se crean los 3 archivos en
  `src/ControlDeFinanzasIA.Infrastructure/Persistence/Migrations/`:
  - `YYYYMMDDhhmmss_InitialSchema.cs`.
  - `YYYYMMDDhhmmss_InitialSchema.Designer.cs`.
  - `AppDbContextModelSnapshot.cs`.
- El método `Up` de la migración crea las tablas
  `Categories` y `PaymentMethods` con todas las columnas
  y los índices únicos en `Name`.
- **Decisión:** la inserción de seed data se hace en
  `OnModelCreating` vía `SeedData.Apply(modelBuilder)` (ver
  F-003), no en la migración. Esto evita generar
  `InsertData` en la migración (que ensucia el diff) y
  mantiene el seed versionado en código C# (más fácil
  de leer y testear).

**Justificación**
Las configuraciones Fluent API son la **forma estándar de
EF Core** para mapear entidades a tablas. Hacerlas en
archivos separados (uno por entidad) mantiene
`OnModelCreating` limpio y permite que cada configuración
evolucione independientemente. La migración inicial es
necesaria para que la app pueda crear el schema desde
cero en un equipo limpio.

**Dependencias**
- F-001 de Fase 2 (existe `AppDbContext`).

**Prioridad**
- Crítica. Bloquea F-003, F-004, F-005 y F-006.

**Estimación**
- M (medio día).

**Criterios para considerarla completada**
- `CategoryConfiguration` y `PaymentMethodConfiguration`
  existen y compilan.
- La migración `InitialSchema` se genera exitosamente y
  sus 3 archivos están commiteados.
- `dotnet build` desde la raíz pasa con 0 warnings, 0
  errors.
- `dotnet test` pasa: 111 tests verdes.
- Inspeccionar la migración `Up` confirma que crea las
  tablas y los índices únicos.
- `ApplyConfigurationsFromAssembly` en
  `AppDbContext.OnModelCreating` carga las dos
  configuraciones.

---

## F-003 — Seed data: 9 categorías y 5 métodos de pago del sistema — ✅ COMPLETADA

**Objetivo**
Sembrar las categorías y métodos de pago iniciales cuando
se crea la base de datos, de forma **idempotente** (no
duplica datos si se aplica múltiples veces). Las
entidades del sistema tienen `IsSystem=true` para que
la UI pueda impedir su borrado.

**Descripción breve**
- Crear
  `src/ControlDeFinanzasIA.Infrastructure/Persistence/SeedData.cs`:
  - Clase estática `internal static class SeedData`.
  - Método público estático
    `public static void Apply(ModelBuilder modelBuilder)`.
  - Constantes con los Ids deterministas (no
    `Guid.NewGuid()`) para que los tests puedan
    predecirlos:
    - 9 categorías: Comida, Transporte, Hogar, Salud,
      Ocio, Educación, Ropa, Servicios, Otros.
    - 5 métodos de pago: Efectivo, Tarjeta de débito,
      Tarjeta de crédito, Transferencia, Otro.
  - Usa `modelBuilder.Entity<Category>().HasData(...)` y
    `modelBuilder.Entity<PaymentMethod>().HasData(...)`
    para sembrar.
  - Las entidades se crean con `IsSystem=true` y con
    `CreatedAt`/`UpdatedAt` en un timestamp fijo
    (e.g., `new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)`).
  - Colores hex e iconos emoji para las 9 categorías
    (ver tabla de categorías en F-002 spec previa,
    `spec/features/phase-02/F-002-persistence-efcore-sqlite.md`
    §11.1, tabla documentada).
- `AppDbContext.OnModelCreating` ya llama a
  `SeedData.Apply(modelBuilder)` desde F-001.
- Cuando F-002 genere la migración `InitialSchema`, EF
  Core detecta el `HasData` y emite
  `migrationBuilder.InsertData(...)` en el método `Up`.
  (La F-003 decide si la migración los incluye o no;
  ver §10.3 de la spec previa para la discusión.
  **Decisión recomendada:** la migración NO incluye
  el `InsertData`; el seed se aplica vía `HasData` en
  runtime (cuando se llama a `Database.Migrate()`).
  Esto es más limpio: la migración solo describe el
  schema, no los datos.)

**Justificación**
Sin categorías y métodos de pago sembrados, **no se
puede crear ningún gasto** (la FK a `CategoryId` y
`PaymentMethodId` se violaría). El seed es **mínimo
viable** para que la app arranque con datos
razonables. La idempotencia evita duplicados si el
método `Apply` se llama múltiples veces (e.g., en
tests).

**Dependencias**
- F-002 de Fase 2 (existen las configuraciones que
  definen las columnas).

**Prioridad**
- Alta. Sin seed, la app no es usable.

**Estimación**
- S (1–2 horas).

**Criterios para considerarla completada**
- `SeedData.cs` existe con las 9 categorías y los 5
  métodos de pago.
- Ids deterministas (no `Guid.NewGuid`).
- `IsSystem=true` en todas las entidades sembradas.
- `dotnet build` pasa con 0 warnings, 0 errors.
- `dotnet test` pasa: 111 tests verdes.
- La migración `InitialSchema` se regenera (con
  `dotnet ef migrations remove` + `add`) y ahora
  incluye los `InsertData` (o no, según la decisión
  de la spec).
- F-006 (tests de integración) valida que el seed se
  aplica correctamente y es idempotente.

---

## F-004 — Implementación de `CategoryRepository` y `PaymentMethodRepository` — ✅ COMPLETADA

**Objetivo**
Implementar las clases concretas de los dos repositorios
mínimos (`CategoryRepository`, `PaymentMethodRepository`)
que satisfacen las interfaces de F-006 de Phase 1. Son
las **primeras dos de las 15** que F-006 de Phase 1
definió; los otros 13 repositorios se implementan en
las fases correspondientes (Fase 3+ del roadmap).

**Descripción breve**
- **Respetar la firma de F-006 de Phase 1.** Las
  interfaces tienen exactamente 5 métodos:
  ```csharp
  Task<Result> AddAsync(Category category, CancellationToken ct = default);
  Task<Result> UpdateAsync(Category category, CancellationToken ct = default);
  Task<Result> RemoveAsync(Guid id, CancellationToken ct = default);
  Task<Category?> GetByIdAsync(Guid id, CancellationToken ct = default);
  Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken ct = default);
  ```
  (Misma firma para `PaymentMethod`.) El borrador previo
  de la spec F-002 de Fase 2 definía 7 métodos
  (incluyendo `GetSystemCategoriesAsync` y
  `ExistsByNameAsync`); esos métodos NO existen en la
  interfaz actual y NO se implementan en esta feature.
  Si en el futuro la UI los necesita, se añaden como
  métodos adicionales a la interfaz en una feature
  posterior (probablemente Fase 3 cuando se implemente
  la gestión de categorías).
- Crear
  `src/ControlDeFinanzasIA.Infrastructure/Persistence/Repositories/CategoryRepository.cs`:
  - `public sealed class CategoryRepository : ICategoryRepository`.
  - Constructor que recibe `AppDbContext`.
  - `AddAsync`: chequea unicidad del nombre antes de
    `Add` + `SaveChangesAsync`. Si el nombre ya existe,
    devuelve `Result.Failure("DuplicateName", "...")`.
  - `UpdateAsync`: busca la entidad, si no existe
    devuelve `Result.Failure("NotFound", "...")`, si
    `IsSystem=true` y el nombre cambia, devuelve
    `Result.Failure("SystemCategoryImmutable", "...")`,
    sino actualiza con `_db.Entry(existing).CurrentValues.SetValues(category)`
    y `SaveChangesAsync`.
  - `RemoveAsync`: busca, si no existe
    `Result.Failure("NotFound", "...")`, si `IsSystem=true`
    `Result.Failure("SystemCategoryImmutable", "...")`,
    sino `Remove` + `SaveChangesAsync`.
  - `GetByIdAsync`: `FindAsync`.
  - `GetAllAsync`: `AsNoTracking().OrderBy(c => c.Name).ToListAsync()`.
- Crear
  `src/ControlDeFinanzasIA.Infrastructure/Persistence/Repositories/PaymentMethodRepository.cs`:
  - Idéntica estructura sobre `PaymentMethod` (sin
    `Color`/`Icon`).
- Las validaciones de reglas de negocio (e.g., "no
  renombrar una categoría del sistema") viven en el
  **repositorio** (capa de Infrastructure), no en la
  entidad de Domain. Esto mantiene Domain puro
  (Domain no sabe de EF Core ni de reglas de
  persistencia). La UI traduce los `Result.Failure` a
  mensajes de error.
- **NO** se añade `<ProjectReference>` desde `UI` a
  `Infrastructure` (F-005 lo hace).

**Justificación**
Los dos repositorios son la primera prueba end-to-end
de la arquitectura: `Domain` (entidad) →
`Application` (interfaz) → `Infrastructure` (implementación
con EF Core) → `IUnitOfWork` (transacción). Si este
flujo funciona, el patrón es replicable para los
otros 13 repositorios.

**Dependencias**
- F-001 de Fase 2 (existe `AppDbContext`).
- F-002 de Fase 2 (existen configuraciones).
- F-003 de Fase 2 (existe `SeedData`).
- F-006 de Phase 1 (define las interfaces con la firma
  de 5 métodos que esta feature implementa).

**Prioridad**
- Alta. Es la primera implementación real de
  persistencia.

**Estimación**
- M (medio día a un día).

**Criterios para considerarla completada**
- `CategoryRepository` y `PaymentMethodRepository`
  existen y compilan.
- Implementan exactamente los 5 métodos de la interfaz
  de F-006 de Phase 1 (verificación: la firma coincide
  carácter a carácter).
- Las validaciones de unicidad de nombre y de inmutabilidad
  de `IsSystem` se implementan vía `Result.Failure` con
  códigos descriptivos (`"DuplicateName"`,
  `"NotFound"`, `"SystemCategoryImmutable"`).
- `dotnet build` pasa con 0 warnings, 0 errors.
- `dotnet test` pasa: 111 tests verdes.
- F-006 (tests de integración) valida los repos con
  tests parametrizados.
- Los tests de NetArchTest R-1 a R-5 de F-007 siguen
  pasando.

---

## F-005 — Composition root: registrar persistencia + aplicar migraciones al arranque — ✅ COMPLETADA

**Objetivo**
Conectar la persistencia al composition root que F-008
de Phase 1 dejó listo. Esto incluye:
- Añadir `<ProjectReference>` de `UI` a `Infrastructure`
  (la única dependencia que faltaba entre las dos
  capas, justificada por la excepción del composition
  root de F-007 R-4).
- Extender `Bootstrap.ConfigureServices` para llamar a
  `services.AddPersistence(pathResolver)`.
- Hacer que `App.axaml.cs` aplique las migraciones al
  arrancar (dentro de un scope de DI), antes de crear
  la ventana principal.

**Descripción breve**
- **Modificar
  `src/ControlDeFinanzasIA.UI/ControlDeFinanzasIA.UI.csproj`:**
  añadir
  `<ProjectReference Include="..\ControlDeFinanzasIA.Infrastructure\ControlDeFinanzasIA.Infrastructure.csproj" />`.
- **Modificar
  `src/ControlDeFinanzasIA.UI/Composition/Bootstrap.cs`:**
  - Añadir `using ControlDeFinanzasIA.UI.System;` (no,
    `using ControlDeFinanzasIA.Infrastructure.Persistence;`)
    y `using ControlDeFinanzasIA.Application.Abstractions;`
    (ya está).
  - En `ConfigureServices`, después de registrar
    `IClock` y `IPathResolver`, resolver `IPathResolver`
    desde la colección y llamar a
    `services.AddPersistence(pathResolver)`.
  - **Importante:** el método `AddPersistence` ya está
    en F-001. Aquí solo se enchufa.
- **Modificar `src/ControlDeFinanzasIA.UI/App.axaml.cs`:**
  - En `OnFrameworkInitializationCompleted`, después
    de emitir "App starting" y antes de crear la
    ventana, crear un `IServiceScope`:
    ```csharp
    using (IServiceScope scope = Services.CreateScope())
    {
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();
        _logger.LogInformation("Migrations applied");
    }
    ```
  - Si la migración falla, loguear el error y **no
    continuar** (la ventana no se muestra). Decisión
    dura pero segura: datos corruptos son peor que
    app inusable.
  - **No** se hace auto-backup antes de la migración
    en esta feature (queda para F-019 según
    `docs/roadmap.md`).
- **Verificar que el test R-4 de F-007 sigue pasando:**
  - El filtro `.That().DoNotHaveName("Bootstrap")` en
    `R4_UI_No_Referencia_A_Infrastructure_Salvo_CompositionRoot`
    excluye el tipo `Bootstrap` y permite que la
    dependencia `UI → Infrastructure` exista.
  - Si la implementación añade accidentalmente
    dependencias de `UI` a `Infrastructure` desde
    tipos que no son `Bootstrap` (e.g., un ViewModel
    que use `AppDbContext` directamente), R-4 falla.
    Esto es correcto: la regla arquitectónica es
    "UI solo conoce Infrastructure desde el
    composition root".

**Justificación**
F-008 de Phase 1 dejó el composition root listo, pero
vacío (solo registraba `IClock` e `IPathResolver`). F-005
es la primera vez que el composition root **se usa de
verdad** para cablear lógica de aplicación (los
repositorios concretos). Es el primer "smoke test" del
patrón definido en `docs/architecture.md` §9.

**Dependencias**
- F-001 a F-004 de Fase 2 (existen `AppDbContext`,
  configuraciones, migraciones, seed, repositorios).
- F-008 de Phase 1 (existe `Bootstrap.ConfigureServices`).

**Prioridad**
- Crítica. Sin esta feature, la app no aplica
  migraciones ni expone los repositorios concretos.

**Estimación**
- M (medio día).

**Criterios para considerarla completada**
- `UI.csproj` tiene `<ProjectReference>` a
  `Infrastructure`.
- `Bootstrap.ConfigureServices` llama a
  `AddPersistence(pathResolver)`.
- `App.axaml.cs` aplica migraciones al arrancar dentro
  de un scope.
- `dotnet build` pasa con 0 warnings, 0 errors.
- `dotnet test` pasa: 111 tests verdes (R-1 a R-5 de
  F-007 siguen pasando; la dependencia `UI →
  Infrastructure` no rompe la arquitectura porque
  pasa por `Bootstrap`).
- `dotnet run --project src/ControlDeFinanzasIA.UI`
  arranca la app, aplica las migraciones (visible en
  log "Migrations applied"), y muestra la ventana.
- En un equipo limpio (sin `data.db` previo), la app
  crea el archivo `data.db`, aplica la migración
  `InitialSchema`, y siembra las 9 categorías y 5
  métodos de pago.
- Si se borra manualmente `data.db`, al siguiente
  arranque se recrea y se siembra.

---

## F-006 — Tests de integración con SQLite in-memory — ✅ COMPLETADA

**Objetivo**
Validar toda la fase de persistencia con tests de
integración que usan **SQLite in-memory** (conexión
compartida por test) y el patrón `IDbContextFactory`
para paralelizar.

**Descripción breve**
- Crear `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/`:
- `TestAppDbContextFactory.cs`:
  - `internal sealed class TestAppDbContextFactory : IDisposable`.
  - Abre una `SqliteConnection` con
    `"DataSource=:memory:"` y la mantiene abierta
    durante la vida del test (compartida entre
    contextos).
  - `CreateContext()` devuelve un `AppDbContext` con
    `DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options`.
  - `Dispose()` cierra y dispone la conexión.
- `AppDbContextTests.cs`:
  - `[Fact] Migrate_CreatesSchema` — verifica que las
    tablas `Categories` y `PaymentMethods` existen
    después de `Migrate`.
  - `[Fact] SeedData_InsertsSystemCategories` —
    verifica que hay 9 categorías con `IsSystem=true`.
  - `[Fact] SeedData_InsertsSystemPaymentMethods` —
    verifica que hay 5 métodos de pago con
    `IsSystem=true`.
  - `[Fact] SeedData_IsIdempotent` — aplica migración
    dos veces, verifica que no hay duplicados.
- `CategoryRepositoryTests.cs`:
  - `GetByIdAsync_WithExistingId_ReturnsCategory`.
  - `GetByIdAsync_WithMissingId_ReturnsNull`.
  - `GetAllAsync_ReturnsAllCategoriesOrderedByName`.
  - `AddAsync_WithUniqueName_PersistsCategory`.
  - `AddAsync_WithDuplicateName_ReturnsFailureWithDuplicateName`.
  - `UpdateAsync_WithExistingId_UpdatesCategory`.
  - `UpdateAsync_WithMissingId_ReturnsFailureWithNotFound`.
  - `UpdateAsync_WithSystemCategoryRenamed_ReturnsFailureWithSystemCategoryImmutable`.
  - `RemoveAsync_WithExistingId_DeletesCategory`.
  - `RemoveAsync_WithMissingId_ReturnsFailureWithNotFound`.
  - `RemoveAsync_WithSystemCategory_ReturnsFailureWithSystemCategoryImmutable`.
- `PaymentMethodRepositoryTests.cs`:
  - Mismo patrón que `CategoryRepositoryTests` pero
    sobre `PaymentMethod` (sin tests de `Color`/`Icon`
    obviamente).
- Cada test usa `using var factory = new
  TestAppDbContextFactory();` para garantizar
  aislamiento.
- **Eliminar o reemplazar** el placeholder
  `F001PlaceholderTests.cs` (de F-001) que ya no es
  necesario porque ahora hay tests reales.
- `PathResolverTests.cs` (opcional pero recomendado):
  - Tests de los paths resueltos por SO. No necesita
    EF Core; puede ir en `Tests.Unit` o
    `Tests.Integration`. Decisión: en
    `Tests.Integration` (porque lee
    `Environment.GetFolderPath` y
    `RuntimeInformation`, que no son 100% puros).

**Justificación**
Sin tests de integración, **no hay forma de validar**
que la persistencia funciona end-to-end sin ejecutar
la app manualmente cada vez. Los tests con SQLite
in-memory son rápidos (ms) y deterministas, lo que
los hace ideales para CI.

**Dependencias**
- F-001 a F-005 de Fase 2 (todo lo que se testea
  existe).

**Prioridad**
- Alta. Necesario para que CI valide la fase.

**Estimación**
- M (medio día a un día).

**Criterios para considerarla completada**
- `TestAppDbContextFactory` existe y es reutilizable
  por todos los tests.
- Los tests pasan en Debug y Release.
- Cobertura de `Infrastructure/Persistence` (DbContext,
  configuraciones, repos, SeedData) ≥ 80%.
- `dotnet test` desde la raíz pasa: 111 originales +
  ~20 nuevos de F-006 = ~131 tests verdes (estimado,
  el número exacto depende de los tests escritos).
- `dotnet format style --verify-no-changes` exit 0.
- El placeholder `F001PlaceholderTests.cs` está
  eliminado o reemplazado.

---

## Orden recomendado y trabajo en paralelo

**Orden secuencial estricto:** F-001 → F-002 → (F-003, F-004
en paralelo posible) → F-005 → F-006.

**F-003 y F-004 son parcialmente independientes:**
- F-003 (seed) no necesita F-004 (repos) ni viceversa.
- F-003 solo necesita F-002 (configuraciones, para
  que las columnas existan antes de insertar datos).
- F-004 necesita F-001, F-002 y F-003.
- Si hay 2 personas, F-003 se puede hacer en paralelo
  con F-004 (F-004 se apoya en F-003, pero F-003 es
  trivial y termina rápido; en la práctica se
  secuencian).

**F-005 es secuencial** a F-001 a F-004 (consume todo
lo que estas features producen).

**F-006 es secuencial** a F-005 (los tests pueden
referenciar el `Bootstrap` aunque sea indirectamente
vía `AddPersistence`; pero en la práctica F-006
testea `Infrastructure/Persistence` directamente, no
el `Bootstrap`, así que podría hacerse en paralelo
con F-005 si la implementación se coordina).

## Trabajo en paralelo posible

Con 2 personas:
- Persona A: F-001 → F-002 → F-003 → F-004.
- Persona B: espera a que F-001 termine, luego hace
  F-005. En paralelo con F-005, Persona B puede
  empezar F-006 (los tests de F-006 no dependen de
  F-005, solo de F-001 a F-004).

Con 3 personas:
- Persona A: F-001.
- Persona B (empieza cuando A termina F-001): F-002
  + F-003.
- Persona C (empieza cuando A termina F-001): F-004.
- Luego todos: F-005 → F-006.

## Riesgos de la fase

- **Conversión de value objects en EF Core** (Money,
  DateRange). F-002 de Fase 2 NO los introduce
  (Category y PaymentMethod no tienen campos
  monetarios). El riesgo se materializa en Fase 3
  (Expense tiene `Amount: Money`). Para entonces,
  el patrón se establece con `ValueConverter` en
  EF Core. La spec previa
  `spec/features/phase-02/F-002-persistence-efcore-sqlite.md`
  tiene esto documentado en su §4 (fuera de
  alcance).
- **Política de migraciones destructivas.** Si una
  migración nueva rompe el archivo `.db` existente
  (e.g., renombrar una columna), la app no arranca
  en el siguiente arranque. Sin auto-backup en
  esta fase (queda para F-019 según roadmap), el
  usuario debe restaurar manualmente. **Mitigación:**
  en v1, las migraciones de Phase 2 son no
  destructivas (crean tablas, no renombran ni
  eliminan). El riesgo se materializa en fases
  futuras.
- **Sembrado idempotente.** Si la app aplica
  migraciones múltiples veces (e.g., en
  `dotnet ef database update` y luego en el
  arranque), el seed no debe duplicar datos. La
  solución: usar `HasData(...)` en
  `OnModelCreating` (que EF Core trata como
  upsert idempotente) o `InsertData` con Ids
  fijos en la migración. **Verificado por test
  en F-006.**
- **Versión de EF Core incompatible con el SDK.**
  Si la versión de EF Core 8.0.x no es compatible
  con el SDK .NET 8.0.x instalado, las migraciones
  fallan. **Mitigación:** verificar con
  `dotnet ef --version` antes de empezar; ajustar
  versión si es necesario.
- **Tests de SQLite in-memory vs archivo real.**
  SQLite in-memory (`DataSource=:memory:`) se
  comporta ligeramente distinto al de archivo en
  algunos edge cases (locks, transacciones
  concurrentes). **Mitigación:** los tests cubren
  el happy path. Tests contra archivo real se
  añaden en F-019 (backup) si surge la necesidad.

## Referencias

- `docs/roadmap.md` — Fase 2, Entregables esperados,
  Riesgos principales, Criterios de fase completada.
- `docs/architecture.md` — §3 (solución y proyectos),
  §4 (grafo de dependencias), §8 (capa de
  Infrastructure: persistencia, repositorios,
  filesystem, logging), §12 (ruta del archivo de
  datos), §13 (configuración).
- `docs/vision.md` — §4.10 (Datos y portabilidad).
- `spec/constitution/tech-stack.md` — paquetes,
  convenciones de código, CPM.
- `spec/constitution/mission.md` — Clean
  Architecture, principios.
- `spec/phases/phase-01/features.md` — F-001 a F-008
  de Phase 1, todas ✅ COMPLETADA, sobre las que
  Fase 2 se construye.
- `spec/features/phase-01/F-004-Abstracciones-clock-pathresolver.done.md`
  — provee `IClock` e `IPathResolver` que F-001 de
  Fase 2 consume.
- `spec/features/phase-01/F-006-contrato-interfaces-repo.done.md`
  — provee las 15 interfaces, incluyendo
  `ICategoryRepository` e `IPaymentMethodRepository`
  con la firma de 5 métodos.
- `spec/features/phase-01/F-007-validacion-arquitectonica.done.md`
  — el test R-4 con el filtro `Bootstrap` que
  permite la dependencia `UI → Infrastructure` (la
  introduce F-005).
- `spec/features/phase-01/F-008-composition-root.done.md`
  — el `Bootstrap.ConfigureServices` que F-005
  extiende.
- `spec/features/phase-02/F-002-persistence-efcore-sqlite.md`
  — **borrador previo** (desactualizado) que cubre
  la fase como una sola feature. El presente
  documento lo **supersede** y es la fuente de
  verdad. Útil como referencia para detalles
  técnicos (e.g., tabla de categorías del sistema
  con colores hex e iconos emoji en §11.1).
- `spec/README.md` — convención del directorio
  `spec/`.
- EF Core 8 documentation oficial (a consultar vía
  Context7 cuando se implemente).
- `dotnet ef` CLI documentation (a consultar vía
  Context7 cuando se genere la migración).
