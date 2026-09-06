# Tareas — F-006: Tests de Integración con SQLite In-Memory

> **Spec:** `spec/features/phase-02/F-006-integration-tests.md`
> **Fuentes de verdad:** la spec, `docs/architecture.md` §15
> (testing), `docs/roadmap.md` §Fase 2, `docs/vision.md` §3
> (es-DO como locale por defecto), `spec/constitution/tech-stack.md`
> (paquetes, xUnit + FluentAssertions), `spec/constitution/mission.md`
> (Clean Architecture, testability),
> `spec/phases/phase-02/features.md` (F-006 dentro del
> desglose de la fase 2),
> `spec/features/phase-02/F-001-appdbcontext-addpersistence.done.md`
> (provee `AppDbContext`),
> `spec/features/phase-02/F-002-entity-configurations-migration.done.md`
> (generó las 2 migraciones y añadió
> `Microsoft.Data.Sqlite` a `Tests.Integration.csproj`),
> `spec/features/phase-02/F-003-seed-data.done.md` (sembró
> las 14 entidades con IDs deterministas),
> `spec/features/phase-02/F-004-category-paymentmethod-repositories.done.md`
> (los 2 repos que F-006 testea),
> `spec/features/phase-02/F-005-composition-root-persistence.done.md`
> (enchufa `AddPersistence` y aplica migraciones en
> producción; F-006 replica el flujo en memoria),
> `spec/features/phase-01/F-007-validacion-arquitectonica.done.md`
> (los 11 tests de NetArchTest; F-006 no debe romperlos).

> Cada tarea es implementable en menos de 2 horas. El proyecto
> debe seguir compilando al finalizar cada una. La numeración
> (T-001...) es local a esta feature. F-006 añade 4 archivos
> nuevos en `Tests.Integration/Persistence/` y elimina 1 archivo
> (el placeholder de F-001 de Phase 1). NO modifica ningún
> proyecto de `src/`, NO añade tests de UI, NO añade tests de
> las 13 interfaces que no se implementan en Phase 2.

---

## Resumen de tareas

| ID | Tarea | Estimación | Depende de |
| --- | --- | --- | --- |
| T-001 | Verificar que `Microsoft.Data.Sqlite` está en `Tests.Integration.csproj` y crear `TestAppDbContextFactory.cs` | S | — |
| T-002 | Crear `AppDbContextTests.cs` con 4 tests de schema y seed | S | T-001 |
| T-003 | Crear `CategoryRepositoryTests.cs` con ~13 tests | M | T-001, T-002 |
| T-004 | Crear `PaymentMethodRepositoryTests.cs` con ~13 tests | M | T-001, T-002 |
| T-005 | Eliminar `F001PlaceholderTests.cs` | XS | T-002 |
| T-006 | Verificación de build, tests existentes, NetArchTest R-1 a R-5, conformidad arquitectónica | S | T-001 a T-005 |
| T-007 | Format check, actualización de `features.md`, rename de la spec a `.done.md`, build final | S | T-001 a T-006 |

**Total estimado:** 1 jornada. T-003 y T-004 son las más
largas (~13 tests cada una); T-001, T-002 y T-005 son
cortas; T-006 y T-007 son verificación y cierre.

---

## T-001 — Verificar que `Microsoft.Data.Sqlite` está en `Tests.Integration.csproj` y crear `TestAppDbContextFactory.cs`

**Objetivo**
Confirmar que el paquete `Microsoft.Data.Sqlite` (que
F-002 añadió a `Tests.Integration.csproj`) sigue
disponible, y crear el `TestAppDbContextFactory` que
será usado por todos los tests de F-006.

**Descripción**

### T-001.1 — Verificar el paquete

1. Abrir
   `tests/ControlDeFinanzasIA.Tests.Integration/ControlDeFinanzasIA.Tests.Integration.csproj`.
2. Confirmar que tiene
   `<PackageReference Include="Microsoft.Data.Sqlite" />`
   (sin `Version`, vía CPM).
3. Si falta, añadirlo. F-002 lo añadió; si se quitó
   por error, re-añadirlo.

### T-001.2 — Crear `TestAppDbContextFactory`

1. Crear el archivo
   `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/TestAppDbContextFactory.cs`.
2. Contenido (descripción de la estructura):
   - File-scoped namespace:
     `namespace ControlDeFinanzasIA.Tests.Integration.Persistence;`.
   - `using` directives al tope:
     - `using ControlDeFinanzasIA.Infrastructure.Persistence;`
       (para `AppDbContext`).
     - `using Microsoft.Data.Sqlite;` (para
       `SqliteConnection`).
     - `using Microsoft.EntityFrameworkCore;` (para
       `DbContextOptionsBuilder`).
3. Clase:
   - `public sealed class TestAppDbContextFactory :
     IDisposable`.
4. Campo privado:
   - `private readonly SqliteConnection _connection;`.
5. Constructor:
   ```text
   public TestAppDbContextFactory()
   {
       _connection = new SqliteConnection("DataSource=:memory:");
       _connection.Open();
   }
   ```
   - El `Open()` es **obligatorio** para que
     múltiples `DbContext` compartan la misma
     conexión in-memory.
6. Método público:
   ```text
   public AppDbContext CreateContext()
   {
       DbContextOptionsBuilder<AppDbContext> optionsBuilder = new();
       optionsBuilder.UseSqlite(_connection);
       return new AppDbContext(optionsBuilder.Options);
   }
   ```
7. `Dispose()`:
   ```text
   public void Dispose() => _connection.Dispose();
   ```
8. XML doc en la clase y en cada método,
  explicando el patrón de EF Core testing con
  SQLite in-memory.

**Dependencias**
- F-001 de Fase 2 (provee `AppDbContext`).
- F-002 de Fase 2 (añadió `Microsoft.Data.Sqlite` a
  `Tests.Integration.csproj`).

**Archivos / proyectos afectados**
- `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/TestAppDbContextFactory.cs`
  (nuevo).

**Criterios de finalización**
- `Tests.Integration.csproj` tiene
  `Microsoft.Data.Sqlite` (verificado).
- `TestAppDbContextFactory.cs` existe y compila.
- `public sealed class TestAppDbContextFactory :
  IDisposable` con la firma exacta.
- `CreateContext()` devuelve un `AppDbContext`
  configurado con `UseSqlite(_connection)`.
- File-scoped namespace.
- `using` al tope.
- XML doc.
- `dotnet build` desde la raíz: 0 warnings, 0
  errors.

---

## T-002 — Crear `AppDbContextTests.cs` con 4 tests de schema y seed

**Objetivo**
Validar que las 2 migraciones (`InitialSchema` y
`SeedSystemData`) se aplican correctamente, que las
2 tablas se crean con la estructura esperada, y que
el seed inserta los 14 registros del sistema con
los IDs deterministas de F-003.

**Descripción**

### T-002.1 — Crear el archivo

1. Crear el archivo
   `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/AppDbContextTests.cs`.
2. Contenido (descripción de la estructura):
   - File-scoped namespace:
     `namespace ControlDeFinanzasIA.Tests.Integration.Persistence;`.
   - `using` directives al tope:
     - `using ControlDeFinanzasIA.Infrastructure.Persistence;`
       (para `AppDbContext`).
     - `using Microsoft.EntityFrameworkCore;` (para
       `MigrateAsync`).
     - `using Xunit;`.

### T-002.2 — Implementar `Migrate_CreatesSchema`

1. `[Fact] public async Task Migrate_CreatesSchema()`.
2. Cuerpo:
   - `using var factory = new TestAppDbContextFactory();`
   - `using var context = factory.CreateContext();`
   - `await context.Database.MigrateAsync();`
   - Verificar que las tablas `Categories` y
     `PaymentMethods` existen. Esto se puede hacer
     con una query SQL raw:
     `var tables = context.Database.SqlQueryRaw<string>("SELECT name FROM sqlite_master WHERE type='table'").ToList();`
   - Assert: `Assert.Contains("Categories", tables)` y
     `Assert.Contains("PaymentMethods", tables)`.

### T-002.3 — Implementar `SeedData_InsertsSystemCategories`

1. `[Fact] public async Task SeedData_InsertsSystemCategories()`.
2. Cuerpo:
   - `using var factory = new TestAppDbContextFactory();`
   - `using var context = factory.CreateContext();`
   - `await context.Database.MigrateAsync();`
   - Contar:
     `var categories = await context.Set<Category>().ToListAsync();`
   - Assert: `Assert.Equal(9, categories.Count)` y
     `Assert.All(categories, c => Assert.True(c.IsSystem))`.

### T-002.4 — Implementar `SeedData_InsertsSystemPaymentMethods`

1. `[Fact] public async Task SeedData_InsertsSystemPaymentMethods()`.
2. Análogo al anterior, con `PaymentMethod` y
   `Assert.Equal(5, paymentMethods.Count)`.

### T-002.5 — Implementar `SeedData_IsIdempotent`

1. `[Fact] public async Task SeedData_IsIdempotent()`.
2. Cuerpo:
   - `using var factory = new TestAppDbContextFactory();`
   - `using (var context1 = factory.CreateContext()) { await context1.Database.MigrateAsync(); }`
   - `using (var context2 = factory.CreateContext()) { await context2.Database.MigrateAsync(); }`
   - Contar con un tercer contexto:
     `var categories = await factory.CreateContext().Set<Category>().ToListAsync();`
   - Assert: `Assert.Equal(9, categories.Count)`.

**Dependencias**
- T-001 (`TestAppDbContextFactory` existe).
- F-001 de Fase 2 (provee `AppDbContext`).
- F-002 de Fase 2 (las 2 migraciones existen).
- F-003 de Fase 2 (sembró las 14 entidades con IDs
  deterministas).

**Archivos / proyectos afectados**
- `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/AppDbContextTests.cs`
  (nuevo).

**Criterios de finalización**
- El archivo existe y compila.
- 4 tests `[Fact]` con nombres descriptivos.
- Cada test usa `using var factory = new
  TestAppDbContextFactory()` para garantizar
  aislamiento.
- `dotnet build`: 0 warnings, 0 errors.
- Los 4 tests pasan al ejecutar `dotnet test`.

---

## T-003 — Crear `CategoryRepositoryTests.cs` con ~13 tests

**Objetivo**
Validar los 5 métodos de `CategoryRepository` (de
F-004) y las validaciones de reglas de negocio
(`DuplicateName`, `NotFound`,
`SystemCategoryImmutable`).

**Descripción**

### T-003.1 — Crear el archivo

1. Crear el archivo
   `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/CategoryRepositoryTests.cs`.
2. Contenido (descripción de la estructura):
   - File-scoped namespace.
   - `using` directives al tope:
     - `using ControlDeFinanzasIA.Domain.Catalog;`
       (para `Category`).
     - `using ControlDeFinanzasIA.Infrastructure.Persistence;`
       (para `AppDbContext`).
     - `using ControlDeFinanzasIA.Infrastructure.Persistence.Repositories;`
       (para `CategoryRepository`).
     - `using Microsoft.EntityFrameworkCore;` (para
       `MigrateAsync`).
     - `using Xunit;`.

### T-003.2 — Tests de queries

1. `[Fact] GetByIdAsync_WithExistingId_ReturnsCategory`:
   - Aplicar migraciones.
   - Llamar `_repo.GetByIdAsync(new Guid("11111111-...-000000000001"))` (Comida).
   - Assert: el resultado no es null y `Name == "Comida"`.
2. `[Fact] GetByIdAsync_WithMissingId_ReturnsNull`:
   - Aplicar migraciones.
   - Llamar `_repo.GetByIdAsync(Guid.NewGuid())`.
   - Assert: el resultado es null.
3. `[Fact] GetByIdAsync_WithEmptyGuid_ReturnsNull`:
   - Aplicar migraciones.
   - Llamar `_repo.GetByIdAsync(Guid.Empty)`.
   - Assert: el resultado es null (defensivo).
4. `[Fact] GetAllAsync_ReturnsAllCategoriesOrderedByName`:
   - Aplicar migraciones.
   - Llamar `_repo.GetAllAsync()`.
   - Assert: 9 categorías, ordenadas alfabéticamente
     por `Name`.

### T-003.3 — Tests de `AddAsync`

5. `[Fact] AddAsync_WithUniqueName_PersistsCategory`:
   - Aplicar migraciones.
   - Crear `new Category { Id = Guid.NewGuid(), Name
     = "Custom", IsSystem = false, CreatedAt =
     ..., UpdatedAt = ... }`.
   - Llamar `_repo.AddAsync(category)`.
   - Assert: `IsSuccess == true`.
   - Verificar que `GetAllAsync` ahora tiene 10
     categorías.
6. `[Fact] AddAsync_WithDuplicateName_ReturnsFailureWithDuplicateName`:
   - Aplicar migraciones.
   - Crear una categoría con `Name = "Comida"` (ya
     existe en el seed).
   - Assert: `IsSuccess == false`, `Error.Code ==
     "DuplicateName"`.
7. `[Fact] AddAsync_RaceCondition_ReturnsFailureWithDuplicateName`
   (opcional, marcar como `[Skip]` si es difícil de
   reproducir):
   - Aplicar migraciones.
   - Crear 2 categorías con el mismo `Name` en
     tasks concurrentes (`Task.WhenAll` con
     `_repo.AddAsync`).
   - Assert: solo una tiene éxito.

### T-003.4 — Tests de `UpdateAsync`

8. `[Fact] UpdateAsync_WithExistingId_UpdatesCategory`:
   - Aplicar migraciones.
   - Crear una categoría custom, persistirla.
   - Modificar su `Name` y llamar a
     `_repo.UpdateAsync(category)`.
   - Assert: `IsSuccess == true`.
9. `[Fact] UpdateAsync_WithMissingId_ReturnsFailureWithNotFound`:
   - Aplicar migraciones.
   - Crear una categoría con `Id = Guid.NewGuid()` (no
     existe).
   - Llamar a `_repo.UpdateAsync(category)`.
   - Assert: `IsSuccess == false`, `Error.Code ==
     "NotFound"`.
10. `[Fact] UpdateAsync_WithSystemCategoryRenamed_ReturnsFailureWithSystemCategoryImmutable`:
    - Aplicar migraciones.
    - Obtener "Comida" (`IsSystem = true`).
    - Crear una nueva instancia con `Id` de "Comida"
      pero `Name = "Alimentación"`.
    - Llamar a `_repo.UpdateAsync(category)`.
    - Assert: `IsSuccess == false`, `Error.Code ==
      "SystemCategoryImmutable"`.

### T-003.5 — Tests de `RemoveAsync`

11. `[Fact] RemoveAsync_WithExistingId_DeletesCategory`:
    - Aplicar migraciones.
    - Crear una categoría custom, persistirla.
    - Llamar a `_repo.RemoveAsync(category.Id)`.
    - Assert: `IsSuccess == true`.
    - Verificar que `GetByIdAsync(category.Id)` es null.
12. `[Fact] RemoveAsync_WithMissingId_ReturnsFailureWithNotFound`:
    - Aplicar migraciones.
    - Llamar a `_repo.RemoveAsync(Guid.NewGuid())`.
    - Assert: `IsSuccess == false`, `Error.Code ==
      "NotFound"`.
13. `[Fact] RemoveAsync_WithSystemCategory_ReturnsFailureWithSystemCategoryImmutable`:
    - Aplicar migraciones.
    - Llamar a `_repo.RemoveAsync(new Guid("11111111-...-000000000001"))`
      (Comida, del sistema).
    - Assert: `IsSuccess == false`, `Error.Code ==
      "SystemCategoryImmutable"`.

**Dependencias**
- T-001 (`TestAppDbContextFactory`).
- T-002 (los tests de `AppDbContext` pasan; el
  schema y el seed están listos para los tests de
  repo).
- F-004 de Fase 2 (proporciona
  `CategoryRepository`).
- F-001 de Fase 2 (`AppDbContext`).
- F-003 de Fase 2 (sembró los IDs deterministas).

**Archivos / proyectos afectados**
- `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/CategoryRepositoryTests.cs`
  (nuevo).

**Criterios de finalización**
- El archivo existe y compila.
- ~13 tests `[Fact]` con nombres descriptivos.
- Cada test usa `using var factory = new
  TestAppDbContextFactory()` para garantizar
  aislamiento.
- Los IDs del sistema (`11111111-...-001`) se usan
  en los tests que validan entidades del sistema.
- `dotnet build`: 0 warnings, 0 errors.
- Los 13 tests pasan al ejecutar `dotnet test`.

---

## T-004 — Crear `PaymentMethodRepositoryTests.cs` con ~13 tests

**Objetivo**
Misma estructura que `CategoryRepositoryTests` pero
sobre `PaymentMethodRepository` (de F-004). Validar
los 5 métodos y las validaciones de reglas de
negocio con el código de error
`SystemPaymentMethodImmutable` (en vez de
`SystemCategoryImmutable`).

**Descripción**

### T-004.1 — Crear el archivo

1. Crear el archivo
   `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/PaymentMethodRepositoryTests.cs`.
2. Misma estructura que `CategoryRepositoryTests`
   (ver T-003) pero con:
   - `using ControlDeFinanzasIA.Domain.Catalog;` (para
     `PaymentMethod`).
   - `using ControlDeFinanzasIA.Infrastructure.Persistence.Repositories;`
     (para `PaymentMethodRepository`).

### T-004.2 — Tests análogos a `CategoryRepositoryTests`

1. Tests de queries:
   - `GetByIdAsync_WithExistingId_ReturnsPaymentMethod` (usa
     `Id = 22222222-...-001` = Efectivo).
   - `GetByIdAsync_WithMissingId_ReturnsNull`.
   - `GetByIdAsync_WithEmptyGuid_ReturnsNull`.
   - `GetAllAsync_ReturnsAllPaymentMethodsOrderedByName`
     (espera 5 métodos).
2. Tests de `AddAsync`:
   - `AddAsync_WithUniqueName_PersistsPaymentMethod`.
   - `AddAsync_WithDuplicateName_ReturnsFailureWithDuplicateName`
     (usa `Name = "Efectivo"`).
3. Tests de `UpdateAsync`:
   - `UpdateAsync_WithExistingId_UpdatesPaymentMethod`.
   - `UpdateAsync_WithMissingId_ReturnsFailureWithNotFound`.
   - `UpdateAsync_WithSystemPaymentMethodRenamed_ReturnsFailureWithSystemPaymentMethodImmutable`
     (usa `Id = 22222222-...-001` = Efectivo).
4. Tests de `RemoveAsync`:
   - `RemoveAsync_WithExistingId_DeletesPaymentMethod`.
   - `RemoveAsync_WithMissingId_ReturnsFailureWithNotFound`.
   - `RemoveAsync_WithSystemPaymentMethod_ReturnsFailureWithSystemPaymentMethodImmutable`.

**Dependencias**
- T-001 (`TestAppDbContextFactory`).
- T-002 (los tests de `AppDbContext` pasan).
- F-004 de Fase 2 (proporciona
  `PaymentMethodRepository`).

**Archivos / proyectos afectados**
- `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/PaymentMethodRepositoryTests.cs`
  (nuevo).

**Criterios de finalización**
- El archivo existe y compila.
- ~13 tests `[Fact]` con nombres descriptivos.
- Los IDs del sistema (`22222222-...-001`) se usan
  en los tests que validan entidades del sistema.
- El código de error para
  `IsSystem=true && Name cambia` es
  `"SystemPaymentMethodImmutable"` (no
  `"SystemCategoryImmutable"`).
- `dotnet build`: 0 warnings, 0 errors.
- Los 13 tests pasan al ejecutar `dotnet test`.

---

## T-005 — Eliminar `F001PlaceholderTests.cs`

**Objetivo**
Eliminar el placeholder de F-001 de Phase 1, que
F-006 reemplaza con tests reales. Mantener el
placeholder sin sentido es ruido.

**Descripción**

1. Borrar
   `tests/ControlDeFinanzasIA.Tests.Integration/F001PlaceholderTests.cs`.
2. Verificar que el archivo ya no existe:
   `Get-ChildItem tests/ControlDeFinanzasIA.Tests.Integration -Filter F001PlaceholderTests.cs`.
3. Si la compilación falla (por referencia
   inexistente), NO es un problema (el archivo
   borrado no se referencia desde ningún otro).

**Dependencias**
- T-002 (los tests de `AppDbContext` están
  creados; ya no se necesita el placeholder).

**Archivos / proyectos afectados**
- `tests/ControlDeFinanzasIA.Tests.Integration/F001PlaceholderTests.cs`
  (eliminado).

**Criterios de finalización**
- El archivo ya no existe.
- `dotnet build`: 0 warnings, 0 errors.

---

## T-006 — Verificación de build, tests existentes, NetArchTest R-1 a R-5, conformidad arquitectónica

**Objetivo**
Ejecutar la lista de comprobaciones que validan que
F-006 añadió los tests sin romper nada: build
limpio, los 111 tests de `Tests.Unit` siguen
pasando, los ~30 tests nuevos de `Tests.Integration`
pasan, y los 11 tests de NetArchTest de F-007 siguen
pasando.

**Descripción**

### T-006.1 — Build limpio

1. Ejecutar
   `dotnet build "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --nologo`.
2. Confirmar 0 warnings, 0 errors.
3. Si hay warnings o errors, diagnosticar y
   arreglar (probablemente un `using` mal ubicado
   o un nombre de tipo incorrecto).

### T-006.2 — Tests existentes verdes

1. Ejecutar
   `dotnet test "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --nologo --no-build`.
2. Confirmar:
   - `Tests.Unit`: 111 passed, 0 failed.
   - `Tests.Integration`: ~30 nuevos passed, 0
     failed, 0 skipped.
   - Total failures: 0.

### T-006.3 — NetArchTest R-1 a R-5

1. Ejecutar
   `dotnet test --filter "FullyQualifiedName~ArchitectureTests"`.
2. Confirmar que los 11 tests de NetArchTest
   pasan.
3. Si algún test R-N falla, investigar:
   - **R-1 falla:** `Domain` importó algo de `Tests.*`
     (no debería; F-006 no toca `Domain`).
   - **R-2 falla:** `Application` importó algo de
     `Tests.*`.
   - **R-5 falla:** `src/` importó `Tests.*`. Esto
     podría pasar si F-006 añadió una
     dependencia de `Infrastructure` a `Tests.*`
     por error. Verificar `Infrastructure.csproj`.

### T-006.4 — Conformidad arquitectónica

1. Ejecutar
   `dotnet list src/ControlDeFinanzasIA.Domain package`.
   Debe estar **vacío**.
2. Ejecutar
   `dotnet list src/ControlDeFinanzasIA.Application package`.
   Debe seguir mostrando **solo `FluentValidation`**.
3. Ejecutar
   `dotnet list src/ControlDeFinanzasIA.UI reference`.
   Debe seguir mostrando `Application`, `Domain`,
   `Infrastructure`.
4. Ejecutar
   `dotnet list tests/ControlDeFinanzasIA.Tests.Integration package`.
   Debe mostrar `Microsoft.Data.Sqlite` (de F-002)
   y los paquetes de xUnit.

**Dependencias**
- T-001 a T-005 (todas las anteriores).

**Archivos / proyectos afectados**
- Ninguno nuevo. Esta tarea es solo de
  verificación.

**Criterios de finalización**
- Build limpio: 0 warnings, 0 errors.
- Tests.Unit: 111/111 passed.
- Tests.Integration: ~30 passed, 0 failed, 0
  skipped.
- Los 11 tests de NetArchTest pasan.
- `Domain` sin paquetes, `Application` solo
  `FluentValidation`.
- `UI` referencia `Application`, `Domain`, e
  `Infrastructure`.

---

## T-007 — Format check, actualización de `features.md`, rename de la spec a `.done.md`, build final

**Objetivo**
Ejecutar la lista de comprobaciones finales de la
spec (§15 AC-6, AC-7 y AC-8), validar convenciones
de formato, dejar el repo en estado de "F-006
terminada" y marcar la feature como completada en
el desglose de fase. **Esta tarea cierra la Fase 2.**

**Descripción**

### T-007.1 — Format check

1. Ejecutar
   `dotnet format style --verify-no-changes` desde
   la raíz.
2. Confirmar exit 0. Si propone cambios:
   - Aplicarlos con `dotnet format style` y volver
     a verificar.
   - **NO** relajar reglas globales.

### T-007.2 — Cierre de la feature

1. **Actualizar el desglose de fase:** en
   `spec/phases/phase-02/features.md`:
   - Cambiar la fila 006 de la tabla resumen
     de "Pendiente" a "✅ COMPLETADA".
   - Cambiar el header de la sección F-006 de
     `## F-006 — Tests de integración con SQLite in-memory`
     a
     `## F-006 — Tests de integración con SQLite in-memory — ✅ COMPLETADA`.
2. **Actualizar el campo `> **Estado:**`** en el
   header de la spec de `Pendiente de implementación`
   a `✅ COMPLETADA`.
3. **Renombrar la spec:** mover
   `spec/features/phase-02/F-006-integration-tests.md`
   a
   `spec/features/phase-02/F-006-integration-tests.done.md`.
4. **Build final** después del rename:
   `dotnet build -c Release` debe seguir 0/0.
5. **Tests finales**:
   `dotnet test -c Release --no-build` debe
   mostrar:
   - `Tests.Unit`: 111 passed.
   - `Tests.Integration`: ~30 passed, 0 failed,
     0 skipped.
6. **Commit atómico** (cuando el developer
   commitee): los 4 archivos nuevos en
   `Tests.Integration/Persistence/` + la
   eliminación de `F001PlaceholderTests.cs` + el
   rename de la spec + el `features.md` actualizado
   van en un solo commit, idealmente con un mensaje
   como
   `test(persistence): add integration tests for AppDbContext and repositories (F-006)`.
7. **Verificar cierre de Fase 2:** confirmar
   que las **6 features** de Fase 2
   (F-001 a F-006) están marcadas ✅
   COMPLETADA en `spec/phases/phase-02/features.md`.
   Si lo están, la Fase 2 está cerrada.

### Resumen de verificación de AC

| AC | Criterio | Tarea que lo verifica |
| --- | --- | --- |
| AC-1 | `TestAppDbContextFactory` creado | T-001 |
| AC-2 | `AppDbContextTests` con 4 tests | T-002 |
| AC-3 | `CategoryRepositoryTests` con ~13 tests | T-003 |
| AC-4 | `PaymentMethodRepositoryTests` con ~13 tests | T-004 |
| AC-5 | `F001PlaceholderTests.cs` eliminado | T-005 |
| AC-6 | Build limpio, 111 tests, 11 NetArchTest | T-006 |
| AC-7 | Sin scope creep | T-006.4 |
| AC-8 | Documentación y trazabilidad + Fase 2 cerrada | T-007.2 |

**Dependencias**
- T-001 a T-006 (todas las anteriores).

**Archivos / proyectos afectados**
- `spec/phases/phase-02/features.md` (modificado:
  F-006 marcada como completada).
- `spec/features/phase-02/F-006-integration-tests.md`
  (renombrado a `.done.md`).

**Criterios de finalización**
- `dotnet format style --verify-no-changes`: exit 0.
- `dotnet build -c Release`: 0 warnings, 0 errors.
- `dotnet test -c Release --no-build`:
  - `Tests.Unit`: 111 passed.
  - `Tests.Integration`: ~30 passed, 0 failed,
    0 skipped.
- `spec/phases/phase-02/features.md` actualizado
  con F-006 marcada como completada.
- Spec renombrada a `.done.md`.
- **Las 6 features de Fase 2 están ✅ COMPLETADA
  en `features.md`.**

---

## Orden recomendado y trabajo en paralelo

**Orden secuencial estricto:** T-001 → (T-002 en
paralelo posible con T-003 y T-004) → T-005 → T-006
→ T-007.

- **T-001 debe ir primero** (todos los demás tests
  dependen del factory).
- **T-002 puede ir en paralelo con T-003 y T-004**:
  - T-002 testea `AppDbContext` y seed.
  - T-003 y T-004 testean los repos, que dependen
    del seed. T-002 y T-003/T-004 son independientes
    (no comparten código).
- **T-005 depende de T-002** (eliminar el
  placeholder solo cuando los nuevos tests
  existen).
- **T-006 y T-007 son verificación y cierre.**

## Trabajo en paralelo posible

Con 2 personas:
- Persona A: T-001 → T-002 → T-005.
- Persona B: T-003 (paralela a T-002).
- Luego ambos: T-004 → T-006 → T-007.

Con 3 personas:
- Persona A: T-001 → T-002 → T-005.
- Persona B: T-003 (paralela a T-002).
- Persona C: T-004 (paralela a T-002).
- Luego todos: T-006 → T-007.

## Riesgos de la fase de tareas

| Riesgo | Probabilidad | Impacto | Mitigación |
| --- | --- | --- | --- |
| Race condition en `AddAsync` (difícil de reproducir). | Media | Bajo | EC-1: el test es opcional; si se incluye, marcar como `[Skip]`. |
| `SqliteConnection` no se cierra (memory leak). | Baja | Bajo | Cada test usa `using var factory`. xUnit + `using` garantiza la disposición. |
| `dotnet format style` propone cambios. | Baja | Bajo | T-007.1 aplica los cambios. |
| R-5 de F-007 falla porque `Tests.Integration` se considera "src/". | Muy baja | Alto | R-5 verifica `src/` no depende de `Tests.*`. `Tests.Integration` NO es `src/`. AC-6 lo verifica. |
| Los tests son lentos. | Baja | Bajo | T-006.4 mide el tiempo. Si > 5 segundos, optimizar. |
| Un test de repo falla por datos del seed no esperados. | Baja | Medio | F-003 (con F-001) define IDs deterministas; los tests los usan explícitamente. |
| `Microsoft.Data.Sqlite` no está en `Tests.Integration.csproj`. | Baja | Alto | T-001.1 verifica. Si falta, se añade. |
| `F001PlaceholderTests.cs` se borra antes de que T-002 cree los tests nuevos. | Baja | Bajo (regresión) | T-005 depende de T-002 explícitamente. |

## Referencias

- `spec/features/phase-02/F-006-integration-tests.md`
  — la spec completa de F-006. Este tasks file es
  su descomposición técnica.
- `docs/roadmap.md` — Fase 2 (Entregables esperados,
  Riesgos principales, Criterios de fase completada).
- `docs/architecture.md` — §3 (solución y proyectos),
  §4 (grafo de dependencias), §8 (capa de
  Infrastructure: persistencia), §15 (testing).
- `docs/vision.md` — §3 (es-DO como locale por defecto).
- `spec/constitution/tech-stack.md` — paquetes,
  convenciones de código, CPM, testing (xUnit +
  FluentAssertions).
- `spec/constitution/mission.md` — Clean
  Architecture, principios, testability.
- `spec/phases/phase-02/features.md` — F-006 dentro
  del desglose de la fase 2 (última feature
  pendiente).
- `spec/features/phase-02/F-001-appdbcontext-addpersistence.done.md`
  — provee `AppDbContext` que F-006 testea.
- `spec/features/phase-02/F-002-entity-configurations-migration.done.md`
  — generó las 2 migraciones y añadió
  `Microsoft.Data.Sqlite` a `Tests.Integration.csproj`.
- `spec/features/phase-02/F-003-seed-data.done.md` —
  sembró las 14 entidades con IDs deterministas.
- `spec/features/phase-02/F-004-category-paymentmethod-repositories.done.md`
  — implementó los 2 repositorios que F-006
  testea.
- `spec/features/phase-02/F-005-composition-root-persistence.done.md`
  — enchufa `AddPersistence` y aplica migraciones
  en producción; F-006 replica el flujo en memoria.
- `spec/features/phase-01/F-007-validacion-arquitectonica.done.md`
  — los 11 tests de NetArchTest; F-006 no debe
  romperlos.
- `spec/README.md` — convención del directorio
  `spec/`.
- EF Core 8 documentation oficial (a consultar vía
  Context7 cuando se implemente).
- `Microsoft.Data.Sqlite` documentation (a consultar
  vía Context7 cuando se implemente).
