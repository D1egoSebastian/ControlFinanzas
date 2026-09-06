# Tareas — F-004 (Phase 4): Entidad `Tag` + M:N `Expense↔Tag` + `TagRepository` + migración

> **Spec:** `spec/features/phase-04/F-004-tag-entity-and-many-to-many.spec.md`
> **change_id:** `f-004p4-tag-entity-m-n`
> **Estado:** Implementación completada (PR-1 + PR-2). Pendiente `sdd-verify` + `sdd-archive`.
> **Fecha:** 2026-08-27
> **Fuentes de verdad:** la spec, `docs/vision.md` §4.3, `docs/architecture.md` §6, §8,
> `spec/constitution/tech-stack.md` (.NET 8, EF Core 8, SQLite),
> `spec/constitution/mission.md` (dominio puro, sin duplicación),
> `spec/phases/phase-04/features.md` (4-004 dentro del desglose de la fase 4),
> `spec/features/phase-04/F-001-income-entity-config-migration.done.md`
> (patrón entidad `sealed class` + `Configuration` + migración),
> `spec/features/phase-04/F-002-income-repository.done.md`
> (patrón repositorio + tests de integración),
> `spec/features/phase-02/F-004-category-paymentmethod-repositories.done.md`
> (patrón `CategoryRepository` — catálogo).
>
> Cada tarea es implementable en menos de 2 horas. El proyecto debe seguir
> compilando al finalizar cada una. La numeración (T-001...) es local a esta feature.
> F-004 modifica `Domain/Catalog/`, `Domain/Transactions/`, `Infrastructure/Persistence/`
> y ambos proyectos de test. NO modifica `Application/` (salvo DI ya existente),
> `UI/`, `Directory.Packages.props` ni repositorios de `Income`/`Expense`.

---

## Pronóstico de carga de review

| Field | Value |
|-------|-------|
| Estimated changed lines | ~450 |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1: T-001..T-004 (entity + config + migration) ~270 líneas; PR 2: T-005..T-007 (repo + tests + gates) ~180 líneas |
| Delivery strategy | auto-forecast |
| Chain strategy | stacked-to-main |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: High

### Unidades de trabajo sugeridas

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | `Tag` entity, `Expense.Tags`, EF Core configs, migración `AddTagsAndExpenseTags`, schema tests | PR 1 | Base = `main`; incluye build, tests de schema y formato |
| 2 | `TagRepository`, DI, `TagRepositoryTests`, round-trip M:N tests, gates finales | PR 2 | Base = PR 1; depende de la migración y del schema |

---

## Resumen

F-004 (Phase 4) abre el **eje de tags**: promueve `Tag` de `record` placeholder a
`public sealed class` con invariantes, añade la colección `Tags` y los métodos
`AddTag`/`RemoveTag` a `Expense`, crea el mapeo M:N a la tabla `ExpenseTags`,
genera la migración única `AddTagsAndExpenseTags`, implementa `TagRepository` como
espejo de `CategoryRepository` y cubre todo con tests unitarios e integración
(pure xUnit `Assert.*`). Es el nodo inicial que habilita 4-007 (filtros por
`TagIds`) y 4-008 (UI de asignación de tags).

---

## Resumen de tareas

| ID | Tarea | Descripción corta | Estimación diff | Depende de | Verificación |
| --- | --- | --- | --- | --- | --- |
| T-001 | Reescribir `Tag.cs` in-place + `TagTests.cs` | `record` placeholder → `sealed class` con `Create`, `Update`, 4 props, invariantes | +80 | ninguna | `dotnet build` exit 0; ≥9 tests TagTests pasan |
| T-002 | Extender `Expense.cs` + `ExpenseTests.cs` | `Tags` + `AddTag`/`RemoveTag` (idempotentes) + init en ambos ctors | +50 | T-001 | `dotnet build` exit 0; ≥7 tests AddTag/RemoveTag pasan |
| T-003 | Crear `TagConfiguration.cs` + extender `ExpenseConfiguration.cs` + `AppDbContext.DbSet<Tag>` | Mapeo `Tags` con índice único `Name`; M:N `ExpenseTags` con PK compuesta y cascadas | +60 | T-001, T-002 | `dotnet build` exit 0; modelo EF Core válido |
| T-004 | Generar migración `AddTagsAndExpenseTags` + `TagSchemaTests.cs` | `dotnet ef migrations add`; verificar `Up`/`Down`; 3 schema tests | +80 | T-003 | migración generada; schema tests pasan |
| T-005 | Crear `TagRepository.cs` + registrar DI + `TagRepositoryTests.cs` | 5 métodos espejo `CategoryRepository`; 6+ tests CRUD | +100 | T-001, T-004 | repo tests pasan |
| T-006 | Crear `ExpenseTagManyToManyTests.cs` | 4 tests round-trip M:N + cascada ambos lados | +80 | T-002, T-004 | M:N tests pasan |
| T-007 | Verificación final, formato y cierre | Build, tests completos, NetArchTest, format, scope check | 0 | T-001 a T-006 | todos los gates verdes |

**Total estimado:** ~450 líneas cambiadas. T-001, T-005 y T-006 son las más densas;
T-002, T-003 y T-004 son mecánicas; T-007 es verificación y cierre administrativo.

---

## [x] T-001 — Reescribir `Tag.cs` in-place + crear `TagTests.cs`

**AC refs:** AC-1, AC-2, AC-9, AC-10

**Pre-condiciones**
- `src/.../Domain/Catalog/Tag.cs` existe como `record` placeholder de 11 líneas.
- `src/.../Domain/Transactions/Income.cs` verificado como patrón de `sealed class`
  con private ctor + factory `Create` + `Update` + `ValidateAndNormalize`.
- No hay callers de `Tag` fuera de `ITagRepository` (D-4, R-4).

**Pasos**

1. Abrir `src/ControlDeFinanzasIA.Domain/Catalog/Tag.cs`.
2. Mantener file-scoped namespace `ControlDeFinanzasIA.Domain.Catalog`.
3. Reemplazar `public sealed record Tag` por `public sealed class Tag` **sin base** (D-4).
4. Declarar 4 propiedades con `private set`:
   - `Guid Id`
   - `string Name`
   - `string? Color`
   - `DateTime CreatedAt`
5. Añadir dos constructores privados: parameterless para EF Core y parametrizado
   `(Guid id, string name, string? color, DateTime createdAt)` para el factory.
6. Implementar `public static Tag Create(string name, string? color, DateTime createdAt)`:
   - Generar `Id = Guid.NewGuid()`.
   - Validar y normalizar vía método privado `ValidateAndNormalize`.
   - Invariantes: `Name` no null/vacío/whitespace post-trim, ≤ 50; `Color` trim
     → null si blank, OR ≤ 9; `CreatedAt != default(DateTime)` (D-5).
   - Lanzar `ArgumentException` con mensajes en inglés en cada violación.
7. Implementar `public void Update(string name, string? color)` (D-9):
   - Revalidar `Name`/`Color` con las mismas reglas de `Create`.
   - Normalizar y mutar `Name`/`Color`; no tocar `Id` ni `CreatedAt`.
8. Añadir XML doc summary en la clase, cada propiedad, `Create` y `Update`.
9. Crear `tests/ControlDeFinanzasIA.Tests.Unit/Domain/TagTests.cs`:
   - file-scoped namespace `ControlDeFinanzasIA.Tests.Unit.Domain`.
   - Helper `CreateValidTag(...)` con `_defaultCreatedAt = new(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc)`.
   - ≥7 tests de `Create` (happy path, null/empty/whitespace name, name > 50,
     color > 9, null color, whitespace color normalizes to null).
   - ≥2 tests de `Update` (valid args refrescan Name/Color y preservan Id/CreatedAt;
      empty name y color > 9 lanzan `ArgumentException`).
   - Opcional: `Equality_ByReference_TwoInstancesNotEqual`.
10. Ejecutar `dotnet build -c Debug --nologo` y
    `dotnet test -c Debug --no-build --nologo --filter "FullyQualifiedName~TagTests"`.

**Criterios de verificación**
- `dotnet build` exit 0, 0 warnings, 0 errors.
- `public sealed class Tag` sin herencia, 4 props `private set`, 2 ctores privados,
  `Create` y `Update` públicos, `ValidateAndNormalize` privado.
- `ITagRepository.cs` sigue compilando (mismo namespace `Domain.Catalog`).
- `dotnet test --filter "FullyQualifiedName~TagTests"` ≥9 passed, 0 failed.

**Notas**
- D-4: NO usar `record`; NO introducir `Entity<Guid>` (no existe en el codebase).
- D-5: `Color` es lenient — solo longitud máxima 9, sin regex hex estricto.
- D-9: `Update` es obligatorio para que `TagRepository.UpdateAsync` sea testeable.
- Los mensajes de excepción están en inglés (AC-1); mensajes de error del repo
  en español (D-8).

---

## [x] T-002 — Extender `Expense.cs` con `Tags` + `AddTag`/`RemoveTag` + extender `ExpenseTests.cs`

**AC refs:** AC-2 (parcial), AC-3, AC-11

**Pre-condiciones**
- T-001 completada (`Tag` es `sealed class` con `Id` y `Name`).
- `src/.../Domain/Transactions/Expense.cs` verificado con **dos** constructores
  privados: parameterless (línea 62) y parametrizado (línea 79).

**Pasos**

1. Abrir `src/ControlDeFinanzasIA.Domain/Transactions/Expense.cs`.
2. Añadir `using ControlDeFinanzasIA.Domain.Catalog;` al tope (referencia intra-Domain, R-1 OK).
3. Declarar `public ICollection<Tag> Tags { get; private set; } = new List<Tag>();`.
4. Inicializar `Tags` como `new List<Tag>()` en **ambos** constructores privados (D-6):
   - En el ctor parameterless: `Tags = new List<Tag>();`.
   - En el ctor parametrizado: asignar `Tags = new List<Tag>();` antes de salir.
5. Implementar `public void AddTag(Tag tag)` (D-2):
   - `ArgumentNullException.ThrowIfNull(tag);`
   - Si `string.IsNullOrWhiteSpace(tag.Name)` → `ArgumentException`.
   - Si `Tags.Any(t => t.Id == tag.Id)` → **return** (no-op idempotente).
   - `Tags.Add(tag);`
6. Implementar `public void RemoveTag(Tag tag)` (D-2):
   - `ArgumentNullException.ThrowIfNull(tag);`
   - `var existing = Tags.FirstOrDefault(t => t.Id == tag.Id);`
   - Si `existing` no es null → `Tags.Remove(existing)`; si es null → no-op.
7. Añadir XML doc en `Tags`, `AddTag` y `RemoveTag`.
8. Extender `tests/ControlDeFinanzasIA.Tests.Unit/Domain/ExpenseTests.cs`:
   - Helper local `CreateTag(string name, string? color = null)` →
     `Tag.Create(name, color, _defaultCreatedAt)`.
   - ≥7 tests:
     - `NewExpense_HasEmptyNotNullTagsCollection` (D-6).
     - `AddTag_WithNewTag_AddsToCollection`.
     - `AddTag_WithDuplicateId_IsNoOp` (D-2).
     - `AddTag_WithNullTag_Throws`.
     - `AddTag_WithEmptyNameTag_Throws` (guard defensivo).
     - `RemoveTag_Existing_RemovesFromCollection`.
     - `RemoveTag_NotInCollection_IsNoOp` (D-2).
     - `RemoveTag_WithNullTag_Throws`.
9. Ejecutar `dotnet build -c Debug --nologo` y
   `dotnet test -c Debug --no-build --nologo --filter "FullyQualifiedName~ExpenseTests"`.

**Criterios de verificación**
- `dotnet build` exit 0, 0 warnings, 0 errors.
- `Expense.Tags` no es null en una instancia recién creada; `Count == 0`.
- `AddTag`/`RemoveTag` son idempotentes; lanzan excepciones correctas.
- `dotnet test --filter "FullyQualifiedName~ExpenseTests"` todos los tests
  existentes + nuevos pasan.

**Notas**
- D-6 es el riesgo principal: el ctor parameterless de EF Core debe inicializar
  `Tags`; si no, `AddTag` lanzará `NullReferenceException` al materializar.
- D-2: duplicado por `Id` → no-op silencioso; solo `null` lanza excepción.
- El guard defensivo de `AddTag` contra `Name` vacío es defensa en profundidad;
  `Tag.Create` ya rechaza esos valores.

---

## [x] T-003 — Configuraciones EF Core: `TagConfiguration`, `ExpenseConfiguration` M:N y `AppDbContext.DbSet<Tag>`

**AC refs:** AC-4, AC-5, AC-6

**Pre-condiciones**
- T-001 completada (`Tag` es entidad).
- T-002 completada (`Expense.Tags` existe).
- `CategoryConfiguration.cs` verificado como patrón de configuración de catálogo.
- `ExpenseConfiguration.cs` verificado con `OwnsOne(Amount)` y `HasOne<Category>()`.
- `AppDbContext.cs` verificado con `DbSet<T>` expression-bodied.

**Pasos**

1. Crear `src/ControlDeFinanzasIA.Infrastructure/Persistence/Configurations/TagConfiguration.cs`:
   - file-scoped namespace `ControlDeFinanzasIA.Infrastructure.Persistence.Configurations`.
   - `public sealed class TagConfiguration : IEntityTypeConfiguration<Tag>`.
   - `b.ToTable("Tags")`.
   - `b.HasKey(t => t.Id)`; `b.Property(t => t.Id).ValueGeneratedNever()`.
   - `b.Property(t => t.Name).IsRequired().HasMaxLength(50)`.
   - `b.HasIndex(t => t.Name).IsUnique()` (índice único crítico).
   - `b.Property(t => t.Color).HasMaxLength(9)` (nullable, sin `IsRequired`).
   - `b.Property(t => t.CreatedAt).IsRequired()`.
   - XML doc en clase + `Configure`.
2. Abrir `src/.../Infrastructure/Persistence/Configurations/ExpenseConfiguration.cs`.
3. Tras el bloque `OwnsOne(e => e.Amount, ...)`, añadir el mapeo M:N (AC-5):
   ```csharp
   b.HasMany(e => e.Tags)
       .WithMany()
       .UsingEntity<Dictionary<string, object>>(
           "ExpenseTags",
           right => right.HasOne<Tag>()
               .WithMany()
               .HasForeignKey("TagId")
               .OnDelete(DeleteBehavior.Cascade),
           left => left.HasOne<Expense>()
               .WithMany()
               .HasForeignKey("ExpenseId")
               .OnDelete(DeleteBehavior.Cascade),
           join =>
           {
               join.HasKey("ExpenseId", "TagId");
               join.HasIndex("TagId");
               join.ToTable("ExpenseTags");
           });
   ```
4. Añadir XML doc comment sobre el bloque M:N.
5. Abrir `src/.../Infrastructure/Persistence/AppDbContext.cs`.
6. Añadir `public DbSet<Tag> Tags => Set<Tag>();` inmediatamente después de `Incomes`.
7. Actualizar XML doc summary de la clase para mencionar `Tag`.
8. Ejecutar `dotnet build -c Debug --nologo`.

**Criterios de verificación**
- `dotnet build` exit 0, 0 warnings, 0 errors.
- `TagConfiguration` usa `ToTable("Tags")`, índice único `Name`, `Color` max 9 nullable,
  `CreatedAt` requerido.
- `ExpenseConfiguration` contiene el bloque `UsingEntity<Dictionary<string, object>>`
  con PK compuesta `(ExpenseId, TagId)`, FKs `Cascade` e índice en `TagId`.
- `AppDbContext.Tags` es expression-bodied get-only, ubicado tras `Incomes`.

**Notas**
- D-7: PK compuesta + FKs `Cascade` en ambos lados + índice secundario en `TagId`.
- `Tag` no tiene navegación inversa hacia `Expense` (`.WithMany()` vacío) — es
  catálogo puro.
- No tocar `OnModelCreating`; la configuración se descubre por
  `ApplyConfigurationsFromAssembly`.

---

## [x] T-004 — Generar migración `AddTagsAndExpenseTags` + crear `TagSchemaTests.cs`

**AC refs:** AC-7, AC-8, AC-11

**Pre-condiciones**
- T-003 completada (`TagConfiguration`, M:N en `ExpenseConfiguration`, `DbSet<Tag>`).
- `dotnet-ef` 8.0.11 disponible localmente.

**Pasos**

1. Ejecutar:
   ```powershell
   dotnet ef migrations add AddTagsAndExpenseTags `
     --project src/ControlDeFinanzasIA.Infrastructure `
     --startup-project src/ControlDeFinanzasIA.UI
   ```
2. Verificar que se crearon 3 archivos en `src/.../Infrastructure/Persistence/Migrations/`:
   - `<timestamp>_AddTagsAndExpenseTags.cs`
   - `<timestamp>_AddTagsAndExpenseTags.Designer.cs`
   - `AppDbContextModelSnapshot.cs` (actualizado)
3. Inspeccionar `<timestamp>_AddTagsAndExpenseTags.cs`:
   - `Up` crea `Tags` (`Id`, `Name`, `Color`, `CreatedAt`) + `IX_Tags_Name` único.
   - `Up` crea `ExpenseTags` (`ExpenseId`, `TagId`) + PK compuesta +
     FKs `Cascade` + `IX_ExpenseTags_TagId`.
   - `Up` no contiene `InsertData` (no seed).
   - `Up` no toca `Categories`/`PaymentMethods`/`Expenses`/`Incomes`.
   - `Down`: `DropTable("ExpenseTags")` **luego** `DropTable("Tags")` (D-3).
4. Si EF Core generó namespace block-scoped, corregir a file-scoped (IDE0161)
   como en F-001 P4 C-5.
5. Crear `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/TagSchemaTests.cs`:
   - file-scoped namespace `ControlDeFinanzasIA.Tests.Integration.Persistence`.
   - Fixture `using var factory = new TestAppDbContextFactory(); using AppDbContext context = factory.CreateContext(); await context.Database.MigrateAsync();`.
   - ≥3 tests:
     - `Tags_Table_HasExpectedColumns` — verifica 4 columnas (`Id`, `Name`, `Color`, `CreatedAt`).
     - `Tags_Name_HasUniqueIndex` — insertar dos tags con mismo `Name` → `DbUpdateException`.
     - `ExpenseTags_HasCompositePKAndBothCascadeFKs` — join row con `TagId`
       inexistente falla; duplicado `(ExpenseId, TagId)` falla; borrar `Tag`
       elimina filas join por cascada.
6. Ejecutar `dotnet build -c Debug --nologo` y
   `dotnet test -c Debug --no-build --nologo --filter "FullyQualifiedName~TagSchemaTests"`.

**Criterios de verificación**
- `dotnet build` exit 0, 0 warnings, 0 errors.
- Migración generada con `Up`/`Down` correctos; file-scoped namespace.
- `dotnet test --filter "FullyQualifiedName~TagSchemaTests"` ≥3 passed, 0 failed.

**Notas**
- D-3: UNA sola migración `AddTagsAndExpenseTags` (no dos).
- `--startup-project` debe ser `src/ControlDeFinanzasIA.UI` (convención del repo).
- La tabla `Tags` debe crearse **antes** que `ExpenseTags` por la FK.

---

## [x] T-005 — Crear `TagRepository.cs` + registrar DI + crear `TagRepositoryTests.cs`

**AC refs:** AC-6 (parcial), AC-9, AC-12, AC-13

**Pre-condiciones**
- T-001 completada (`Tag` con `Update`).
- T-004 completada (migración generada; schema disponible).
- `CategoryRepository.cs` verificado como patrón catálogo (5 métodos,
  `DuplicateName`, `NotFound`, orden por `Name`).
- `ServiceCollectionExtensions.cs` verificado con los 4 registros existentes.

**Pasos**

1. Crear `src/ControlDeFinanzasIA.Infrastructure/Persistence/Repositories/TagRepository.cs`:
   - file-scoped namespace `ControlDeFinanzasIA.Infrastructure.Persistence.Repositories`.
   - `public sealed class TagRepository : ITagRepository`.
   - Campo `private readonly AppDbContext _db;` y ctor `public TagRepository(AppDbContext db)`.
   - `AddAsync`: pre-check `AnyAsync(t => t.Name == tag.Name)` → `DuplicateName`;
     try `Add` + `SaveChangesAsync` → `Success`; catch `DbUpdateException` →
     `DuplicateName` (race). Códigos en inglés, mensajes en español.
   - `UpdateAsync`: `FindAsync(tag.Id)`; null → `NotFound`; `SetValues` +
     `SaveChangesAsync` → `Success`.
   - `RemoveAsync`: `Guid.Empty` → `NotFound`; `FindAsync`; null → `NotFound`;
     `Remove` + `SaveChangesAsync` → `Success`.
   - `GetByIdAsync`: `Guid.Empty` → null; else `FindAsync`.
   - `GetAllAsync`: `AsNoTracking().OrderBy(t => t.Name).ToListAsync()`.
   - XML doc en clase + cada método.
2. Abrir `src/.../Infrastructure/Persistence/ServiceCollectionExtensions.cs`.
3. Añadir `services.AddScoped<ITagRepository, TagRepository>();` tras `IIncomeRepository`.
4. Actualizar XML doc del método `AddPersistence` para listar `ITagRepository`
   y añadir `<para>` referenciando F-004 Phase 4.
5. Crear `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/TagRepositoryTests.cs`:
   - file-scoped namespace `tests/.../Tests.Integration/Persistence`.
   - Helper `CreateTag(name, color?)` → `Tag.Create(name, color, _fixedTimestamp)`
     con `_fixedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)`.
   - ≥6 tests (espejo `CategoryRepositoryTests`):
     - `AddAsync_WithUniqueName_Persists`.
     - `AddAsync_WithDuplicateName_ReturnsDuplicateName`.
     - `UpdateAsync_Existing_UpdatesFields` (usa `tag.Update(...)` — D-9).
     - `RemoveAsync_Existing_DeletesTag`.
     - `GetByIdAsync_Existing_ReturnsTag`.
     - `GetAllAsync_ReturnsAllOrderedByName`.
6. Ejecutar `dotnet build -c Debug --nologo` y
   `dotnet test -c Debug --no-build --nologo --filter "FullyQualifiedName~TagRepository"`.

**Criterios de verificación**
- `dotnet build` exit 0, 0 warnings, 0 errors.
- `TagRepository` implementa 5 métodos; `AddPersistence` registra `ITagRepository`.
- `dotnet test --filter "FullyQualifiedName~TagRepository"` ≥6 passed, 0 failed.

**Notas**
- D-1: `ITagRepository` no se modifica; mantiene 5 métodos.
- D-8: `TagRepository` espeja `CategoryRepository` sin lógica de `IsSystem`.
- `UpdateAsync` requiere `Tag.Update` (D-9) para construir una instancia mutada
  con el `Id` existente.

---

## [x] T-006 — Crear `ExpenseTagManyToManyTests.cs`

**AC refs:** AC-13, AC-14

**Pre-condiciones**
- T-002 completada (`Expense.AddTag`/`RemoveTag`).
- T-004 completada (migración aplicable; schema M:N disponible).
- `TestAppDbContextFactory` verificado con `CreateContext()` + `MigrateAsync()`.

**Pasos**

1. Crear `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/ExpenseTagManyToManyTests.cs`:
   - file-scoped namespace `ControlDeFinanzasIA.Tests.Integration.Persistence`.
   - Campos estáticos:
     - `_fixedTimestamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)`.
     - `_comidaCategoryId = new Guid("11111111-1111-1111-1111-000000000001")`.
   - Helper `CreateTag(name, color?)`.
   - Helper `CreateExpense(...)` usando `Expense.Create(_comidaCategoryId, date,
     Money.Create(amount, Currency.DOP), description, null, _fixedTimestamp)`.
2. Implementar ≥4 tests:
   - `AddExpenseWithTags_PersistsBothAndJoinRows`: crea expense + 2 tags,
     `expense.AddTag(t1); expense.AddTag(t2);`, persiste vía `_db.Expenses.Add`,
     verifica 2 filas en `ExpenseTags`.
   - `ReloadExpenseWithTags_PopulatesTagsFromJoinTable`: nuevo contexto,
     `_db.Expenses.Include(e => e.Tags).AsNoTracking().FirstAsync(...)`,
     verifica `Tags.Count == 2`.
   - `DeleteExpense_CascadesJoinRows`: borra expense, verifica que las filas join
     desaparecen y los tags permanecen.
   - `DeleteTag_CascadesJoinRows`: borra un tag, verifica que su fila join
     desaparece, el otro tag permanece y el expense sigue existiendo.
3. Usar pure xUnit `Assert.*` (`Assert.True`, `Assert.Equal`, `Assert.Null`, etc.).
4. Ejecutar `dotnet test -c Debug --no-build --nologo --filter "FullyQualifiedName~ManyToMany"`.

**Criterios de verificación**
- `dotnet build` exit 0, 0 warnings, 0 errors.
- `dotnet test --filter "FullyQualifiedName~ManyToMany"` ≥4 passed, 0 failed.
- Los tests demuestran cascada en ambos lados y carga eager vía `Include`.

**Notas**
- `ExpenseRepository` **no** se usa aquí; no carga `Tags` vía `FindAsync`.
- Los tests consultan `AppDbContext` directamente para validar el mapeo M:N.
- Reutilizar la categoría sembrada `_comidaCategoryId` para construir expenses válidos.

---

## [x] T-007 — Verificación final, formato y cierre

**AC refs:** AC-14, AC-15, AC-16

**Pre-condiciones**
- T-001 a T-006 completadas.

**Pasos**

1. Ejecutar `dotnet build -c Release --nologo` → 0 warnings, 0 errors.
2. Ejecutar `dotnet build -c Debug --nologo` → 0 warnings, 0 errors.
3. Ejecutar `dotnet test -c Debug --no-build --nologo --filter "FullyQualifiedName~Tag|FullyQualifiedName~Expense\.AddTag|FullyQualifiedName~ManyToMany"` → todos los tests nuevos pasan.
4. Ejecutar `dotnet test -c Debug --no-build --nologo --filter "FullyQualifiedName~ArchitectureTests"` → 11/11 passed.
5. Ejecutar `dotnet test -c Debug --no-build --nologo` → suite completa verde
   (baseline + ~27-30 nuevos; 1 fallo pre-existente sin cambios es aceptable).
6. Ejecutar `dotnet format style --verify-no-changes` → exit 0.
7. Ejecutar `dotnet list src/ControlDeFinanzasIA.Domain package` → vacío (R-1).
8. Verificar scope (AC-16):
   - `ITagRepository.cs` sin cambios (5 métodos).
   - Ningún archivo `Income*` modificado.
   - `ExpenseRepository.cs` sin cambios.
   - Sin UI / ViewModels / XAML.
   - Sin modificaciones a `Money`/`Currency`/`Result`/`PagedResult`.
   - Solo una migración nueva: `AddTagsAndExpenseTags`.
9. Actualizar este archivo de tareas marcando todas las casillas `[x]`.
10. Reportar conteos exactos de tests observados.

**Criterios de verificación**
- `dotnet build -c Release` exit 0, 0 warnings, 0 errors.
- `dotnet build -c Debug` exit 0, 0 warnings, 0 errors.
- `dotnet test -c Debug --no-build` suite verde (baseline + nuevos).
- `dotnet test --filter "FullyQualifiedName~ArchitectureTests"` 11/11 passed.
- `dotnet format style --verify-no-changes` exit 0.
- `dotnet list src/ControlDeFinanzasIA.Domain package` vacío.
- Sin archivos fuera de la lista in-scope modificados.

**Notas**
- No renombrar la spec a `.done.md`; eso corresponde a `sdd-archive` de la fase.
- Si `features.md` necesita actualización, marcar 4-004 como completada solo si
  todos los gates pasan.

---

## Orden de ejecución recomendado

**Orden secuencial con dependencias explícitas:**

1. **T-001** debe ejecutarse primero (el resto necesita `Tag` como `sealed class`).
2. **T-002** depende de T-001 (`Expense.AddTag` usa `Tag`).
3. **T-003** depende de T-001 y T-002.
4. **T-004** depende de T-003 (configuraciones necesarias para generar la migración).
5. **T-005** depende de T-001 y T-004 (`Tag` + schema migrado).
6. **T-006** depende de T-002 y T-004 (`Expense.AddTag` + schema migrado).
7. **T-007** cierra la feature.

```text
T-001 → T-002 ─┬─ T-003 ── T-004 ─┬─ T-005 ─┐
               │                   │         ├─ T-007
               │                   └─ T-006 ─┘
               └───────────────────────────────┘
```

T-005 y T-006 pueden ejecutarse en paralelo una vez T-004 está lista.

---

## Tareas bloqueantes / habilitadoras

| Tarea | Habilita | Razón |
| --- | --- | --- |
| T-001 | T-002, T-003, T-005 | `Tag` debe ser `sealed class` antes de que `Expense`/`EF`/`Repo` lo usen. |
| T-002 | T-003, T-006 | `Expense.Tags` y `AddTag` deben existir para el mapeo M:N y sus tests. |
| T-003 | T-004 | EF Core necesita descubrir `TagConfiguration` y `ExpenseConfiguration` M:N. |
| T-004 | T-005, T-006 | Los schema tests y repo tests aplican la migración `AddTagsAndExpenseTags`. |
| T-005 | T-007 | Todos los tests deben pasar antes del cierre. |
| T-006 | T-007 | Todos los tests deben pasar antes del cierre. |

---

## Estimación total

| Tarea | Dificultad | Horas estimadas |
| --- | --- | --- |
| T-001 | M | 1.5 |
| T-002 | S | 1 |
| T-003 | S | 0.75 |
| T-004 | M | 1.5 |
| T-005 | M | 1.5 |
| T-006 | M | 1.5 |
| T-007 | S | 0.5 |
| **Total** | | **~8 horas (~1 jornada)** |

---

## Riesgos y mitigaciones

| Riesgo | Probabilidad | Impacto | Mitigación |
| --- | --- | --- | --- |
| Se omite `Tag.Update` y `TagRepository.UpdateAsync` queda intestable | Media | Alto | **D-9** + AC-2 + AC-10; RT-3 (`UpdateAsync_Existing_UpdatesFields`) lo blinda. |
| `Expense.Tags` no se inicializa en el ctor sin params → NRE al materializar | Media | Alto | **D-6** + AC-3; ET-1 (`NewExpense_HasEmptyNotNullTagsCollection`) lo detecta. |
| EF Core genera FKs `Restrict` en lugar de `Cascade` en `ExpenseTags` | Media | Alto | AC-5 fuerza `.OnDelete(DeleteBehavior.Cascade)` en ambos lados; MT-3/MT-4 verifican. |
| `Tag` record→class rompe referencias existentes | Baja | Medio | Verificado: solo `ITagRepository` referencia `Tag`; lectura de props sigue compilando. |
| `DuplicateName` case-sensitive (SQLite default) permite "Comida" y "comida" | Media | Bajo | Aceptado por diseño (D-8); `AnyAsync` pre-check es case-sensitive; documentado en spec R-5. |
| Se extiende `ITagRepository` con helpers fuera de scope | Baja | Alto | **D-1** locked; AC-16 prohíbe modificar `ITagRepository`. Revisar en PR. |
| Se añade seed data a la migración | Baja | Medio | AC-8 prohíbe `InsertData`; T-004 inspecciona `Up`. |
| Tests usan FluentAssertions o mocking por inercia | Media | Bajo | RNF-6 / AC-10 exigen pure xUnit `Assert.*`; revisar en PR. |
| `dotnet format style` propone cambios inesperados | Baja | Bajo | Aplicar los cambios. No relajar reglas globales. |

---

## Notas para apply

- **`Tag.cs` es reescritura in-place:** reemplazar el `record` placeholder por
  `public sealed class Tag` completo. NO crear `Domain/Tags/` ni renombrar el archivo.
- **Espejar `Income.cs` (F-001 P4)** para la forma de `Tag`: private ctor + factory
  `Create` + `Update` + `ValidateAndNormalize` + props `private set`.
- **NO introducir `Entity<Guid>`:** el codebase no tiene base `Entity<TId>`;
  `Tag` debe ser standalone como `Expense`/`Income`.
- **`Tag.Update` es obligatorio (D-9):** sin él, `UpdateAsync` no tiene camino de
  mutación público. No añadir `Tag.Reconstruct`.
- **`Expense.Tags` en AMBOS ctors privados (D-6):** olvidar el ctor parameterless
  es el bug clásico de navegaciones de colección en EF Core.
- **Migración con `--startup-project UI`:** ejecutar el comando exacto de T-004.
- **M:N `UsingEntity<Dictionary<string,object>>`:** PK compuesta `(ExpenseId, TagId)`,
  ambas FKs `Cascade`, índice en `TagId`.
- **No seed data:** la migración debe ser DDL puro.
- **Pure xUnit `Assert.*`:** sin FluentAssertions, sin mocking library.
- **No scope creep:** no `ExpenseFilter.TagIds`, no `ExpenseRepository` modifications,
  no UI, no use cases de tags, no `Income↔Tag`.
- **Verificación de cascada:** MT-3 y MT-4 son críticos para validar D-7.

---

## Definition of Done (DoD)

- [x] AC-1: `Tag.cs` reescrito in-place como `public sealed class Tag` sin base. (T-001)
- [x] AC-2: `Tag.Update(string, string?)` implementado y testeado. (T-001)
- [x] AC-3: `Expense.Tags` + `AddTag`/`RemoveTag` + init en ambos ctors. (T-002)
- [x] AC-4: `TagConfiguration.cs` creado con índice único `Name`. (T-003)
- [x] AC-5: Mapeo M:N `ExpenseTags` en `ExpenseConfiguration.cs`. (T-003)
- [x] AC-6: `AppDbContext.Tags` expression-bodied y `ITagRepository` registrado en DI. (T-003, T-005)
- [x] AC-7: `ServiceCollectionExtensions` actualizado con `AddScoped<ITagRepository, TagRepository>`. (T-005)
- [x] AC-8: Migración `AddTagsAndExpenseTags` generada con `Up`/`Down` correctos. (T-004)
- [x] AC-9: `TagRepository.cs` creado con 5 métodos espejo `CategoryRepository`. (T-005)
- [x] AC-10: `TagTests.cs` con ≥9 tests pure xUnit. (T-001)
- [x] AC-11: `ExpenseTests.cs` extendido con ≥7 tests `AddTag`/`RemoveTag`. (T-002)
- [x] AC-12: `TagSchemaTests.cs` con ≥3 tests de schema. (T-004)
- [x] AC-13: `TagRepositoryTests.cs` con ≥6 tests CRUD. (T-005)
- [x] AC-14: `ExpenseTagManyToManyTests.cs` con ≥4 tests round-trip M:N. (T-006)
- [x] AC-15: Build limpio, suite verde, NetArchTest R-1..R-5 verdes, format exit 0. (T-007)
- [x] AC-16: Sin scope creep. (T-007)

---

## Referencias

- `spec/features/phase-04/F-004-tag-entity-and-many-to-many.spec.md` — la spec completa de F-004 P4.
- `.atl/sdd/f-004p4-tag-entity-m-n/proposal.md` — PRD de origen.
- `spec/phases/phase-04/features.md` — 4-004 dentro del desglose de la fase 4.
- `src/ControlDeFinanzasIA.Domain/Catalog/Tag.cs` — placeholder a reescribir.
- `src/ControlDeFinanzasIA.Domain/Transactions/Expense.cs` — patrón de entidad y ctors.
- `src/ControlDeFinanzasIA.Domain/Transactions/Income.cs` — patrón `sealed class` + factory.
- `src/ControlDeFinanzasIA.Infrastructure/Persistence/Configurations/CategoryConfiguration.cs` — patrón catálogo.
- `src/ControlDeFinanzasIA.Infrastructure/Persistence/Configurations/ExpenseConfiguration.cs` — donde añadir M:N.
- `src/ControlDeFinanzasIA.Infrastructure/Persistence/Repositories/CategoryRepository.cs` — patrón repo catálogo.
- `tests/ControlDeFinanzasIA.Tests.Unit/Domain/IncomeTests.cs` — patrón tests entidad.
- `tests/ControlDeFinanzasIA.Tests.Unit/Domain/ExpenseTests.cs` — archivo a extender.
- `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/CategoryRepositoryTests.cs` — patrón repo tests.
- `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/TestAppDbContextFactory.cs` — fixture SQLite.
- `AGENTS.md` — convenciones de arquitectura, migraciones y NetArchTest R-1..R-5.
