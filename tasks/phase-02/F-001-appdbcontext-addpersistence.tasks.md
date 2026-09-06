# Tareas — F-001: AppDbContext y AddPersistence

> **Spec:** `spec/features/phase-02/F-001-appdbcontext-addpersistence.md`
> **Fuentes de verdad:** la spec, `docs/architecture.md` §3, §4,
> §8, §12, `docs/roadmap.md` §Fase 2 (Entregables esperados),
> `spec/constitution/tech-stack.md` (paquetes, CPM),
> `spec/constitution/mission.md` (Clean Architecture),
> `spec/phases/phase-02/features.md` (F-001 dentro del
> desglose de la fase 2),
> `spec/features/phase-01/F-001-skeleton-solution.done.md`
> (convención de CPM, `.gitignore` que excluye `*.db`),
> `spec/features/phase-01/F-004-Abstracciones-clock-pathresolver.done.md`
> (provee `IPathResolver` y `IClock` que F-001 consume),
> `spec/features/phase-01/F-006-contrato-interfaces-repo.done.md`
> (provee `IUnitOfWork` que F-001 implementa),
> `spec/features/phase-01/F-007-validacion-arquitectonica.done.md`
> (los tests R-1 a R-5 que F-001 no debe romper),
> `spec/features/phase-01/F-008-composition-root.done.md`
> (el `Bootstrap.ConfigureServices` y el
> `SystemPathResolver` que F-005 enchufa).
>
> Cada tarea es implementable en menos de 2 horas. El proyecto
> debe seguir compilando al finalizar cada una. La numeración
> (T-001...) es local a esta feature. F-001 solo añade
> archivos en `src/ControlDeFinanzasIA.Infrastructure/Persistence/`
> y modifica `Directory.Packages.props` + 2 `.csproj`.

---

## Resumen de tareas

| ID | Tarea | Estimación | Depende de |
| --- | --- | --- | --- |
| T-001 | Verificar / añadir los 4 paquetes EF Core en `Directory.Packages.props` y referenciarlos en `Infrastructure.csproj` + `Tests.Integration.csproj` | XS | — |
| T-002 | Crear `SeedData` (stub vacío) en `Infrastructure/Persistence/SeedData.cs` | XS | — |
| T-003 | Crear `AppDbContext` en `Infrastructure/Persistence/AppDbContext.cs` | S | T-001, T-002 |
| T-004 | Crear `EfUnitOfWork` en `Infrastructure/Persistence/EfUnitOfWork.cs` | XS | T-001 |
| T-005 | Crear `ServiceCollectionExtensions` con `AddPersistence` en `Infrastructure/Persistence/ServiceCollectionExtensions.cs` | S | T-003, T-004 |
| T-006 | Verificación de build, tests existentes, NetArchTest R-1 a R-5, y conformidad arquitectónica | S | T-001 a T-005 |
| T-007 | Format check, actualización de `features.md`, rename de la spec a `.done.md` y build final | S | T-001 a T-006 |

**Total estimado:** menos de 1 jornada. T-003 y T-005 son
las más largas (clases con cuerpo sustancial); T-001, T-002 y
T-004 son triviales. T-006 y T-007 son verificación y cierre.

---

## T-001 — Verificar / añadir los 4 paquetes EF Core en `Directory.Packages.props` y referenciarlos en `Infrastructure.csproj` + `Tests.Integration.csproj`

**Objetivo**
Confirmar que los 4 paquetes de Entity Framework Core 8
están disponibles en CPM y que los 2 proyectos que los
consumen (`Infrastructure` y `Tests.Integration`) los
referencian sin atributo `Version`. Si falta alguno,
añadirlo siguiendo la disciplina CPM heredada de F-001
de Phase 1.

**Descripción**

### T-001.1 — Verificar / añadir `PackageVersion` en `Directory.Packages.props`

1. Abrir `Directory.Packages.props` y comprobar si
   contiene los 4 `PackageVersion` con versión exacta:
   - `Microsoft.EntityFrameworkCore` (8.0.x).
   - `Microsoft.EntityFrameworkCore.Sqlite` (8.0.x).
   - `Microsoft.EntityFrameworkCore.Design` (8.0.x).
   - `Microsoft.EntityFrameworkCore.Relational` (8.0.x).
2. Si falta alguno, añadirlo con la versión confirmada
   vía Context7 (última 8.0.x estable al momento de
   implementar; default de referencia: 8.0.10 u
   8.0.11). Versión exacta, sin rangos.
3. Mantener el orden alfabético del archivo (si ya
   está organizado así) o añadir al final (si no).
4. No se añade ningún otro paquete de EF Core (no
   `SqlServer`, no `Npgsql`).

### T-001.2 — Verificar / añadir `<PackageReference>` en `Infrastructure.csproj`

1. Abrir
   `src/ControlDeFinanzasIA.Infrastructure/ControlDeFinanzasIA.Infrastructure.csproj`
   y comprobar que tiene 3 `<PackageReference>` sin
   `Version`:
   - `Microsoft.EntityFrameworkCore`
   - `Microsoft.EntityFrameworkCore.Sqlite`
   - `Microsoft.EntityFrameworkCore.Design`
2. Si falta alguno, añadirlo dentro del `<ItemGroup>`
   apropiado. La spec deja libertad de cómo agrupar
   los paquetes (alfabético, por proveedor, etc.);
   seguir la convención existente del archivo si la
   hay.
3. No se añade `Microsoft.EntityFrameworkCore.Relational`
   en este `.csproj` (es transitivo de Sqlite; la spec
   lo fija en CPM pero no requiere referencia explícita
   en el proyecto).

### T-001.3 — Verificar / añadir `<PackageReference>` en `Tests.Integration.csproj`

1. Abrir
   `tests/ControlDeFinanzasIA.Tests.Integration/ControlDeFinanzasIA.Tests.Integration.csproj`
   y comprobar que tiene 1 `<PackageReference>` sin
   `Version`:
   - `Microsoft.EntityFrameworkCore.Sqlite`
2. Si falta, añadirlo.

### T-001.4 — Verificar el restore

1. Ejecutar `dotnet restore` desde la raíz.
2. Confirmar que los 4 paquetes se restauran sin
   errores.
3. Verificar con
   `dotnet list src/ControlDeFinanzasIA.Infrastructure package --include-transitive`
   que los 3 paquetes directos están presentes y que
   `Microsoft.EntityFrameworkCore.Relational` aparece
   como transitivo de Sqlite.

**Dependencias**
- F-001 a F-008 de Phase 1 (todas completadas, proveen
  los proyectos y la configuración de CPM).

**Archivos / proyectos afectados**
- `Directory.Packages.props` (modificado solo si falta
  algún `PackageVersion`).
- `src/ControlDeFinanzasIA.Infrastructure/ControlDeFinanzasIA.Infrastructure.csproj`
  (modificado solo si falta algún `<PackageReference>`).
- `tests/ControlDeFinanzasIA.Tests.Integration/ControlDeFinanzasIA.Tests.Integration.csproj`
  (modificado solo si falta el `<PackageReference>`).
- `obj/project.assets.json` (regenerado por
  `dotnet restore`).

**Criterios de finalización**
- `Directory.Packages.props` contiene los 4
  `PackageVersion` con versión exacta (8.0.x).
- `Infrastructure.csproj` contiene 3
  `<PackageReference>` (Core, Sqlite, Design) sin
  `Version`.
- `Tests.Integration.csproj` contiene 1
  `<PackageReference>` (Sqlite) sin `Version`.
- `dotnet restore` desde la raíz pasa sin errores.
- `dotnet list src/ControlDeFinanzasIA.Infrastructure package --include-transitive`
  muestra los 3 paquetes directos y `Relational` como
  transitivo.
- Si todo estaba presente, no se hace ningún cambio
  (no-op).

---

## T-002 — Crear `SeedData` (stub vacío) en `Infrastructure/Persistence/SeedData.cs`

**Objetivo**
Introducir el archivo `SeedData.cs` con la firma
completa que F-001 referencia en `AppDbContext.OnModelCreating`,
pero con el cuerpo trivial (stub vacío). Esto permite
que F-001 compile de forma aislada; F-003 reemplazará
el cuerpo con las 9 categorías y 5 métodos de pago.

**Descripción**

1. Crear el archivo
   `src/ControlDeFinanzasIA.Infrastructure/Persistence/SeedData.cs`.
2. Contenido (descripción de la estructura, no
   implementación completa):
   - File-scoped namespace:
     `namespace ControlDeFinanzasIA.Infrastructure.Persistence;`.
   - `using` directives necesarias al tope (NUNCA
     después del namespace — bug de Fase 0 ya
     corregido):
     - `using Microsoft.EntityFrameworkCore;` para
       `ModelBuilder`.
   - `[`ninguno por ahora`]`. La spec no exige un
     atributo en `SeedData` (no es composition root;
     no es atributo marker).
   - `public static class SeedData` (sealed por
     convención; no se hereda).
   - Método:
     - `public static void Apply(ModelBuilder modelBuilder)`.
     - Cuerpo: por ahora, un comentario que documente
       que F-003 implementa el seed. Ejemplo:
       ```text
       // F-003 implements the seed data: 9 system
       // categories and 5 system payment methods,
       // inserted via modelBuilder.Entity<T>().HasData(...).
       // This stub exists so AppDbContext.OnModelCreating
       // can call SeedData.Apply(modelBuilder) without a
       // missing-symbol compile error.
       ```
3. XML doc en la clase y en el método, explicando:
   - Que es un stub en F-001.
   - Que F-003 lo completa.
   - Que el patrón de uso es
     `modelBuilder.Entity<Category>().HasData(...)` y
     `modelBuilder.Entity<PaymentMethod>().HasData(...)`.

**Dependencias**
- T-001 (los paquetes EF Core están disponibles, así
  `ModelBuilder` se puede referenciar).

**Archivos / proyectos afectados**
- `src/ControlDeFinanzasIA.Infrastructure/Persistence/SeedData.cs`
  (nuevo).

**Criterios de finalización**
- El archivo existe y compila.
- `public static class SeedData` con
  `public static void Apply(ModelBuilder modelBuilder)`.
- Cuerpo trivial (vacío o con el comentario del paso 2).
- File-scoped namespace.
- `using` al tope.
- XML doc en la clase y en el método citando F-003.
- `dotnet build` desde la raíz: 0 warnings, 0 errors
  (incluyendo la compilación del stub en
  `Infrastructure.csproj`).

---

## T-003 — Crear `AppDbContext` en `Infrastructure/Persistence/AppDbContext.cs`

**Objetivo**
Crear la clase `AppDbContext` (la única puerta de entrada
a EF Core) con `DbSet<Category>` y `DbSet<PaymentMethod>`,
y `OnModelCreating` que aplica configuraciones del
assembly y llama al stub de `SeedData`.

**Descripción**

1. Crear el archivo
   `src/ControlDeFinanzasIA.Infrastructure/Persistence/AppDbContext.cs`.
2. Contenido (descripción de la estructura):
   - File-scoped namespace:
     `namespace ControlDeFinanzasIA.Infrastructure.Persistence;`.
   - `using` directives al tope:
     - `using ControlDeFinanzasIA.Domain.Catalog;` (para
       `Category` y `PaymentMethod`; verificar el
       namespace exacto mirando los archivos
       `src/ControlDeFinanzasIA.Domain/Catalog/Category.cs`
       y `PaymentMethod.cs`).
     - `using Microsoft.EntityFrameworkCore;` (para
       `DbContext`, `DbSet`, `ModelBuilder`).
3. Clase:
   - `public class AppDbContext : DbContext` (NO
     `sealed` — convención de EF Core documentada en
     §17.3 de la spec).
4. Campos y propiedades:
   - `public DbSet<Category> Categories => Set<Category>();`
   - `public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();`
5. Constructor:
   - `public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }`.
6. Override:
   - `protected override void OnModelCreating(ModelBuilder modelBuilder)`.
   - Cuerpo:
     ```text
     modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
     SeedData.Apply(modelBuilder);
     ```
   - El `SeedData` referenciado es el stub de T-002.
     F-003 lo completa con el seed real.
7. XML doc en la clase y en cada miembro público,
   citando:
   - `docs/architecture.md` §8 (capa de
     Infrastructure, sección "Persistencia").
   - La spec de F-001 §RN-1 (única puerta de entrada a
     EF Core).
   - La spec de F-001 §RN-2 (registrado como Scoped).

**Dependencias**
- T-001 (paquetes disponibles, `DbContext` /
  `DbContextOptions` / `ModelBuilder` resuelven).
- T-002 (existe `SeedData` para que `OnModelCreating`
  compile).
- F-006 de Phase 1 (define `IUnitOfWork` que F-001
  no usa directamente, pero que confirma el patrón
  de "Domain → Application → Infrastructure").
- F-002 de Phase 1 (las entidades `Category` y
  `PaymentMethod` ya existen en
  `Domain/Catalog/`).

**Archivos / proyectos afectados**
- `src/ControlDeFinanzasIA.Infrastructure/Persistence/AppDbContext.cs`
  (nuevo).

**Criterios de finalización**
- El archivo existe y compila.
- `public class AppDbContext : DbContext` (sin
  `sealed`).
- Constructor público que recibe
  `DbContextOptions<AppDbContext>`.
- 2 `DbSet<>`: `Categories` y `PaymentMethods`.
- `OnModelCreating` llama a
  `ApplyConfigurationsFromAssembly` y a
  `SeedData.Apply`.
- File-scoped namespace.
- `using` al tope.
- XML doc en la clase, las propiedades y el
  constructor.
- `dotnet build` desde la raíz: 0 warnings, 0
  errors.

---

## T-004 — Crear `EfUnitOfWork` en `Infrastructure/Persistence/EfUnitOfWork.cs`

**Objetivo**
Implementar la única versión concreta de `IUnitOfWork`
para EF Core. `SaveChangesAsync` delega en el
`AppDbContext` inyectado. Esta es la única
implementación de `IUnitOfWork` en v1 (si en el futuro
se necesita una versión más elaborada, se reemplaza
esta, no se añade una segunda).

**Descripción**

1. Crear el archivo
   `src/ControlDeFinanzasIA.Infrastructure/Persistence/EfUnitOfWork.cs`.
2. Contenido (descripción de la estructura):
   - File-scoped namespace:
     `namespace ControlDeFinanzasIA.Infrastructure.Persistence;`.
   - `using` directives al tope:
     - `using ControlDeFinanzasIA.Application.Abstractions;`
       (para `IUnitOfWork`).
3. Clase:
   - `public sealed class EfUnitOfWork : IUnitOfWork`
     (`sealed` documentado en §17.4 de la spec).
4. Campo privado:
   - `private readonly AppDbContext _db;`
5. Constructor:
   - `public EfUnitOfWork(AppDbContext db) => _db = db;`
6. Método:
   - `public Task<int> SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);`
7. XML doc en la clase, el constructor y el método,
   citando:
   - La spec de F-001 §RN-3 (`IUnitOfWork` es Scoped
     y delega en `AppDbContext`).
   - La spec de F-001 §RF-3 (firma exacta de
     `SaveChangesAsync`).
   - El spec F-006 de Phase 1 (la interfaz
     `IUnitOfWork` que se implementa).

**Dependencias**
- T-001 (paquetes disponibles; `SaveChangesAsync` de
  EF Core resuelve).
- F-006 de Phase 1 (define `IUnitOfWork` con la firma
  exacta que F-001 implementa).
- T-003 (existe `AppDbContext` que se inyecta).

**Archivos / proyectos afectados**
- `src/ControlDeFinanzasIA.Infrastructure/Persistence/EfUnitOfWork.cs`
  (nuevo).

**Criterios de finalización**
- El archivo existe y compila.
- `public sealed class EfUnitOfWork : IUnitOfWork`.
- Constructor público que recibe `AppDbContext`.
- `public Task<int> SaveChangesAsync(CancellationToken ct = default)`
  que delega en `_db.SaveChangesAsync(ct)`.
- File-scoped namespace.
- `using` al tope.
- XML doc en la clase, constructor y método.
- `dotnet build` desde la raíz: 0 warnings, 0
  errors.

---

## T-005 — Crear `ServiceCollectionExtensions` con `AddPersistence` en `Infrastructure/Persistence/ServiceCollectionExtensions.cs`

**Objetivo**
Exponer el extension method `AddPersistence` que F-005
enchufará en el composition root. Registra
`AppDbContext` (Scoped) e `IUnitOfWork` (Scoped) con
los lifetimes correctos. **NO** registra `IPathResolver`
(ya está en `Bootstrap.ConfigureServices` por F-008)
ni los 15 repositorios (F-004).

**Descripción**

1. Crear el archivo
   `src/ControlDeFinanzasIA.Infrastructure/Persistence/ServiceCollectionExtensions.cs`.
2. Contenido (descripción de la estructura):
   - File-scoped namespace:
     `namespace ControlDeFinanzasIA.Infrastructure.Persistence;`.
   - `using` directives al tope:
     - `using ControlDeFinanzasIA.Application.Abstractions;`
       (para `IPathResolver` e `IUnitOfWork`).
     - `using Microsoft.EntityFrameworkCore;` (para
       `AddDbContext`, `UseSqlite`).
     - `using Microsoft.Extensions.DependencyInjection;`
       (para `IServiceCollection`, `AddScoped`).
3. Clase:
   - `public static class ServiceCollectionExtensions`
     (`sealed` implícito por `static`; convención del
     proyecto).
4. Método de extensión:
   - Firma:
     `public static IServiceCollection AddPersistence(this IServiceCollection services, IPathResolver pathResolver)`.
   - Cuerpo:
     ```text
     ArgumentNullException.ThrowIfNull(services);
     ArgumentNullException.ThrowIfNull(pathResolver);

     services.AddDbContext<AppDbContext>(opts =>
         opts.UseSqlite($"Data Source={pathResolver.DatabaseFilePath}"));
     services.AddScoped<IUnitOfWork, EfUnitOfWork>();
     // Repositories registered in F-004 of Phase 2.

     return services;
     ```
   - **No** se añade `services.AddSingleton<IPathResolver>(pathResolver);`
     porque `IPathResolver` ya está registrado en
     `Bootstrap.ConfigureServices` por F-008. Re-registrarlo
     es redundante.
   - **No** se registran los 15 repositorios. Eso
     llega en F-004.
5. XML doc en la clase y en el método, citando:
   - La spec de F-001 §RF-4 (firma y cuerpo exactos).
   - La spec de F-001 §RN-4 (`IPathResolver` se
     resuelve en el composition root, no en
     `AddPersistence`).
   - La spec de F-001 §17.2 (justificación del
     parámetro en vez de re-registro).

**Dependencias**
- T-001 (paquetes disponibles; `AddDbContext`,
  `UseSqlite`, `AddScoped`, etc. resuelven).
- T-003 (existe `AppDbContext` que se registra).
- T-004 (existe `EfUnitOfWork` que se registra).
- F-004 de Phase 1 (define `IPathResolver` con la
  firma que `AddPersistence` recibe).
- F-006 de Phase 1 (define `IUnitOfWork` con la firma
  que `AddPersistence` registra).

**Archivos / proyectos afectados**
- `src/ControlDeFinanzasIA.Infrastructure/Persistence/ServiceCollectionExtensions.cs`
  (nuevo).

**Criterios de finalización**
- El archivo existe y compila.
- `public static class ServiceCollectionExtensions`.
- Método de extensión con la firma exacta
  `AddPersistence(this IServiceCollection, IPathResolver)`.
- Registra `AppDbContext` con `AddDbContext` y
  `UseSqlite` apuntando a
  `pathResolver.DatabaseFilePath`.
- Registra `IUnitOfWork` con `AddScoped`.
- **NO** registra `IPathResolver` (verificación: el
  método no contiene `AddSingleton<IPathResolver>` ni
  equivalente).
- **NO** registra los 15 repositorios.
- File-scoped namespace.
- `using` al tope.
- XML doc en la clase y en el método.
- `dotnet build` desde la raíz: 0 warnings, 0
  errors.

---

## T-006 — Verificación de build, tests existentes, NetArchTest R-1 a R-5, y conformidad arquitectónica

**Objetivo**
Ejecutar la lista de comprobaciones que validan que
F-001 no rompió nada: build limpio, 111 tests verdes
(100 originales + 11 de F-007), y los 5 tests de
NetArchTest de F-007 (R-1 a R-5) siguen pasando. Esta
es la red de seguridad que demuestra que la
arquitectura sigue intacta.

**Descripción**

### T-006.1 — Build limpio

1. Ejecutar
   `dotnet build "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --nologo`
   desde la raíz.
2. Confirmar 0 warnings, 0 errors.
3. Si hay warnings o errors, NO continuar. Diagnosticar
   y arreglar (probablemente un `using` mal ubicado o
   una firma que no coincide con `IUnitOfWork` o
   `IPathResolver`).

### T-006.2 — Tests existentes verdes

1. Ejecutar
   `dotnet test "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --nologo --no-build`
   desde la raíz.
2. Confirmar:
   - `Tests.Unit`: 111 passed, 0 failed.
   - `Tests.Integration`: 1 skipped (placeholder
     `F001PlaceholderTests`), 0 failed.
   - Total failures: 0.

### T-006.3 — NetArchTest R-1 a R-5

1. Ejecutar
   `dotnet test --filter "FullyQualifiedName~R1_Domain"`.
   Confirmar que R-1 sigue pasando
   (`Domain` no depende de EF Core ni de otros
   paquetes prohibidos). Esto es el chequeo más
   importante: si R-1 falla, F-001 contaminó
   `Domain`.
2. Ejecutar
   `dotnet test --filter "FullyQualifiedName~R"`.
   Confirmar que los 11 tests de NetArchTest
   (R-1_Domain × 2, R-2_Application × 2,
   R-3_Infrastructure × 2, R-4_UI × 3, R-5 × 2) siguen
   pasando.
3. Si algún test R-N falla, investigar:
   - **R-1 falla:** `Domain` importó EF Core por
     error (no debería; F-001 solo toca
     `Infrastructure`). Verificar que
     `Domain.csproj` sigue vacío de
     `<PackageReference>`.
   - **R-2 falla:** `Application` importó EF Core
     por error. Verificar que `Application.csproj`
     sigue mostrando solo `FluentValidation` (de
     F-002 de Phase 1) y los nuevos paquetes
     añadidos **NO** se filtraron a `Application`.
   - **R-3 falla:** probablemente porque
     `Infrastructure` ahora referencia `Domain` y
     `Application` (ya lo hacía). Verificar que
     R-3 sigue pasando tal cual. Si falla, es un
     cambio sutil en las firmas.
   - **R-4 falla:** porque se añadió la
     dependencia `UI → Infrastructure` por error
     (no debería; F-001 NO la añade). Verificar
     `dotnet list src/ControlDeFinanzasIA.UI reference`.
   - **R-5 falla:** `src/` importó `Tests.*` por
     error. Verificar `dotnet list reference` en
     cada proyecto de `src/`.

### T-006.4 — Conformidad arquitectónica

1. Ejecutar
   `dotnet list src/ControlDeFinanzasIA.Domain package`.
   Debe estar **vacío** (Domain sigue sin paquetes
   externos, conforme a R-1 de F-007).
2. Ejecutar
   `dotnet list src/ControlDeFinanzasIA.Application package`.
   Debe seguir mostrando **solo `FluentValidation`**
   (de F-002 de Phase 1) y **NO** `Microsoft.EntityFrameworkCore.*`.
3. Ejecutar
   `dotnet list src/ControlDeFinanzasIA.UI reference`.
   Debe seguir mostrando solo `Application` y `Domain`
   (NO `Infrastructure` — esa dependencia la añade
   F-005).
4. Ejecutar
   `dotnet list src/ControlDeFinanzasIA.Infrastructure reference`.
   Debe mostrar `Application` (y, transitivamente,
   `Domain`).
5. Ejecutar
   `dotnet list src/ControlDeFinanzasIA.Infrastructure package`.
   Debe mostrar los 3 paquetes EF Core directos
   (Core, Sqlite, Design) y posiblemente `Relational`
   como transitivo de Sqlite.

**Dependencias**
- T-001 a T-005 (todas las anteriores).

**Archivos / proyectos afectados**
- Ninguno nuevo. Esta tarea es solo de verificación.

**Criterios de finalización**
- Build limpio: 0 warnings, 0 errors (Debug y
  Release).
- Tests.Unit: 111/111 passed.
- Tests.Integration: 1 skipped, 0 failed.
- 11 tests de NetArchTest de F-007: 11/11 passed (R-1
  a R-5).
- `Domain` no tiene paquetes NuGet.
- `Application` solo tiene `FluentValidation`.
- `UI` no tiene `<ProjectReference>` a
  `Infrastructure`.
- `Infrastructure` referencia correctamente
  `Application` y tiene los 3 paquetes EF Core.

---

## T-007 — Format check, actualización de `features.md`, rename de la spec a `.done.md` y build final

**Objetivo**
Ejecutar la lista de comprobaciones finales de la
spec (§15 AC-9 y AC-10), validar convenciones de
formato, dejar el repo en estado de "F-001 terminada"
y marcar la feature como completada en el desglose
de fase.

**Descripción**

### T-007.1 — Format check

1. Ejecutar
   `dotnet format style --verify-no-changes` desde
   la raíz.
2. Confirmar exit 0. Si propone cambios (e.g.,
   longitud de línea, orden de `using`):
   - Aplicarlos con `dotnet format style` y volver
     a verificar.
   - **NO** relajar reglas globales en
     `.editorconfig` o `stylecop.json` para hacer
     pasar el check. Las reglas son el contrato.
3. Si hay problemas persistentes, documentar el
   issue en el commit / PR.

### T-007.2 — Verificar convenciones en los 4 archivos nuevos

1. Confirmar que los 4 archivos nuevos en
   `src/ControlDeFinanzasIA.Infrastructure/Persistence/`
   usan:
   - File-scoped namespace.
   - `using` directives al tope (NO después del
     namespace — bug de Fase 0 ya corregido).
   - XML doc en cada clase y método público.
   - `sealed` en `EfUnitOfWork` y
     `ServiceCollectionExtensions`. `AppDbContext`
     NO es `sealed` (convención EF Core).
2. Si algún archivo no cumple, arreglarlo antes de
   cerrar F-001.

### T-007.3 — Cierre de la feature

1. **Actualizar el desglose de fase:** en
   `spec/phases/phase-02/features.md`:
   - Cambiar la fila 001 de la tabla resumen de
     "Pendiente" a "✅ COMPLETADA" (mismo formato
     que F-001 a F-008 de Phase 1 en su
     `spec/phases/phase-01/features.md`).
   - Cambiar el header de la sección F-001 de
     `## F-001 — AppDbContext + paquetes EF Core + composition root extendido`
     a
     `## F-001 — AppDbContext + paquetes EF Core + composition root extendido — ✅ COMPLETADA`.
2. **Renombrar la spec:** mover
   `spec/features/phase-02/F-001-appdbcontext-addpersistence.md`
   a
   `spec/features/phase-02/F-001-appdbcontext-addpersistence.done.md`
   (convención del proyecto, ver Phase 1).
3. **Build final** después del rename para
   confirmar que nada se rompió:
   `dotnet build -c Release` desde la raíz debe
   seguir 0/0.
4. **Tests finales**:
   `dotnet test -c Release --no-build` debe seguir
   111/1 (passed/skipped).

### Resumen de verificación de AC

| AC | Criterio | Tarea que lo verifica |
| --- | --- | --- |
| AC-1 | 4 paquetes en CPM, 3 + 1 referencias en `.csproj` | T-001 |
| AC-2 | Build limpio Debug y Release | T-001 (parcial), T-006.1, T-007.3 |
| AC-3 | 111 tests verdes, R-1 de F-007 sigue pasando | T-006.2, T-006.3 |
| AC-4 | `AppDbContext` con 2 `DbSet<>` y `OnModelCreating` | T-003 |
| AC-5 | `EfUnitOfWork` que delega en `AppDbContext` | T-004 |
| AC-6 | `AddPersistence` con DbContext + IUnitOfWork | T-005 |
| AC-7 | NO `<ProjectReference>` UI → Infrastructure | T-006.4 |
| AC-8 | `SeedData` stub vacío (F-003 lo completa) | T-002 |
| AC-9 | Convenciones del repo (file-scoped, sealed, format check) | T-007.1, T-007.2 |
| AC-10 | Documentación y trazabilidad (features.md + rename) | T-007.3 |

**Dependencias**
- T-001 a T-006 (todas las anteriores).

**Archivos / proyectos afectados**
- `spec/phases/phase-02/features.md` (modificado: F-001
  marcada como completada).
- `spec/features/phase-02/F-001-appdbcontext-addpersistence.md`
  (renombrado a `.done.md`).

**Criterios de finalización**
- `dotnet format style --verify-no-changes`: exit 0.
- `dotnet build -c Release`: 0 warnings, 0 errors.
- `dotnet test -c Release --no-build`: 111 passed,
  1 skipped, 0 failures.
- Los 4 archivos nuevos siguen las convenciones del
  repo (file-scoped, `using` al tope, XML doc, sealed
  correcto).
- `spec/phases/phase-02/features.md` actualizado con
  F-001 marcada como completada (tabla resumen y
  header de sección).
- Spec renombrada a `.done.md`.

---

## Orden recomendado y trabajo en paralelo

**Orden secuencial estricto:** T-001 → (T-002 ‖ T-004 en
paralelo posible) → T-003 → T-005 → T-006 → T-007.

- **T-001 debe ir primero** porque todos los demás
  dependen de los paquetes NuGet.
- **T-002 y T-004 son independientes entre sí** (archivos
  distintos, no comparten código). Si hay 2 personas,
  se pueden hacer en paralelo. T-001 no bloquea estas
  (los paquetes son los que están disponibles después de
  T-001; pero el `using` de `ModelBuilder` en T-002 y
  el de `IUnitOfWork` en T-004 requieren los paquetes
  restaurados). En la práctica se secuencian porque
  T-001 es trivial (1 minuto).
- **T-003 depende de T-001 y T-002** (usa paquetes y
  referencia el stub `SeedData`).
- **T-005 depende de T-003 y T-004** (registra ambos).
- **T-006 es verificación** de T-001 a T-005.
- **T-007 es cierre** de toda la feature.

## Trabajo en paralelo posible

Con 2 personas:
- Persona A: T-001 → T-002 → T-003 → T-005.
- Persona B: T-004 (paralela a T-002) → T-006 → T-007.

Con 1 persona (el caso más probable, porque la feature
es pequeña): orden secuencial estricto.

## Riesgos de la fase de tareas

| Riesgo | Probabilidad | Impacto | Mitigación |
| --- | --- | --- | --- |
| R-1 de F-007 falla porque `Domain` importó EF Core por error. | Baja | Alto | T-001 NO toca `Domain.csproj`. T-006.3 verifica R-1 explícitamente. Si falla, es un error de implementación; arreglar antes de cerrar. |
| `AddPersistence` registra doble `IPathResolver`. | Baja | Bajo | RN-4 y §17.2 lo documentan. T-005 no añade el registro. |
| `SeedData` no compila porque la firma no coincide con la de F-003. | Baja (mitigado por EC-5 de la spec) | Bajo | T-002 introduce el stub con la firma exacta. F-003 reemplazará el cuerpo sin tocar la firma. |
| La versión de EF Core 8.0.x es incompatible con el SDK .NET 8.0.x instalado. | Baja | Alto | T-001 verifica con `dotnet --version` antes de empezar. Si hay mismatch, ajustar versión en `Directory.Packages.props`. |
| Tests de NetArchTest fallan por cambios sutiles en las firmas de los repositorios. | Muy baja (F-001 no implementa repositorios) | Bajo | T-006.3 verifica los 11 tests de NetArchTest. |
| `dotnet format style` propone cambios inesperados. | Baja | Bajo | Aplicar los cambios propuestos (no relajar reglas globales). Si los cambios son incorrectos, es un bug en los archivos nuevos; arreglar el código. |
| El `<ItemGroup>` de `Infrastructure.csproj` no tiene un lugar claro para los 3 paquetes EF Core. | Baja | Bajo | Si el archivo no tiene un `<ItemGroup>` dedicado a paquetes NuGet, crear uno o seguir la convención existente. T-001 no exige estructura específica del `.csproj`. |
| `AppDbContext` queda `sealed` por error. | Baja | Bajo | La spec §17.3 lo documenta (NO `sealed`). T-003 lo especifica explícitamente en los criterios. |
| `EfUnitOfWork` queda sin `sealed` por error. | Baja | Bajo | La spec §17.4 lo documenta (SÍ `sealed`). T-004 lo especifica explícitamente en los criterios. |
| El `<PackageReference>` de `Microsoft.EntityFrameworkCore.Relational` se añade al `Infrastructure.csproj` por error. | Baja | Bajo | T-001.2 NO lo añade (es transitivo de Sqlite). Si se añade por error, no rompe nada, pero es redundante. T-006.4 verifica con `dotnet list package --include-transitive`. |

## Referencias

- `spec/features/phase-02/F-001-appdbcontext-addpersistence.md`
  — la spec completa de F-001. Este tasks file es su
  descomposición técnica.
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
  del desglose de la fase 2 (6 features totales).
- `spec/features/phase-01/F-001-skeleton-solution.done.md`
  — convenciones de `.editorconfig`, `stylecop.json`,
  CPM, y `.gitignore` (que excluye `*.db`).
- `spec/features/phase-01/F-004-Abstracciones-clock-pathresolver.done.md`
  — provee `IPathResolver` y `IClock` que F-001
  consume.
- `spec/features/phase-01/F-006-contrato-interfaces-repo.done.md`
  — provee `IUnitOfWork` que F-001 implementa, y
  las 15 interfaces de repositorio (F-001 NO las
  implementa).
- `spec/features/phase-01/F-007-validacion-arquitectonica.done.md`
  — los tests R-1 a R-5 que F-001 no debe romper.
- `spec/features/phase-01/F-008-composition-root.done.md`
  — el `Bootstrap.ConfigureServices` y el
  `SystemPathResolver` que F-005 enchufa.
- `spec/README.md` — convención del directorio
  `spec/`.
- EF Core 8 documentation oficial (a consultar vía
  Context7 cuando se implemente).
