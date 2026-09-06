# Tareas — F-006 (Phase 4): `SavedView` + serialización JSON versionada de filtros + repositorio + use cases

> **Spec:** `spec/features/phase-04/F-006-saved-view-filter-json.spec.md`
> **change_id:** `f-006p4-saved-view-json`
> **Estado:** Pendiente de implementación
> **Fecha:** 2026-09-03
> **Modo:** Standard (SDD: propose → spec → tasks → apply → verify → archive)
> **Fuentes de verdad:** la spec, `docs/vision.md` §4.3,
> `spec/constitution/tech-stack.md`, `spec/constitution/mission.md`,
> `spec/phases/phase-04/features.md` (RF-12..RF-15, §4.006, §8 reglas de negocio),
> `spec/features/phase-04/F-007-extend-filters-tagids.done.md` (filtros 6 props con `TagIds`),
> `spec/features/phase-04/F-005-update-delete-use-cases.done.md` (patrón use cases),
> `spec/features/phase-04/F-004-tag-entity-and-many-to-many.done.md` (patrón `Tag`/`TagRepository`).
>
> Cada tarea es implementable en menos de 2 horas. La numeración (T-001...) es local a esta feature.
> F-006 modifica `Domain/Views/`, `Application/Abstractions/`, `Application/Common/`,
> `Application/UseCases/SavedViews/`, `Infrastructure/Persistence/`, `UI/Composition/` y ambos proyectos de test.
> NO modifica `ExpenseFilter.cs`, `IncomeFilter.cs`, `Expense.cs`, `Income.cs`, `Tag.cs`,
> `TransactionType.cs` ni migraciones previas.

---

## Pronóstico de carga de review

| Field | Value |
|-------|-------|
| Estimated changed lines | ~1,105 |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1: T-001..T-005 (Domain + Infrastructure) ~415 líneas; PR 2: T-006..T-009 (Application + tests + DI Bootstrap) ~490 líneas; T-010 (gates) se fusiona en PR-2 |
| Delivery strategy | auto-forecast |
| Chain strategy | stacked-to-main |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: High

### Unidades de trabajo sugeridas

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | `SavedView` entidad, `ISavedViewRepository` extendido, `SavedViewConfiguration`, migración `AddSavedViewsTable`, `SavedViewRepository`, `SavedViewRepositoryTests` | PR 1 | Base = `main`; autónomo, verificable, con rollback propio |
| 2 | `FilterJsonSerializer`, 3 use cases (`SaveCurrentFilter`, `ApplySavedView`, `GetSavedViews`), `FakeSavedViewRepository`, 4 archivos de unit tests, DI Bootstrap | PR 2 | Base = PR 1; depende de la entidad y el repositorio |

---

## Resumen

F-006 (Phase 4) cierra el **eje de vistas guardadas**: promueve el placeholder
`SavedView` de Phase 1 a `public sealed class` con 7 props e invariantes,
serializa `ExpenseFilter`/`IncomeFilter` a JSON versionado mediante envelope
`{ schemaVersion, type, data }`, persiste en tabla `SavedViews` con migración
aditiva, expone `SavedViewRepository` (6 métodos, espejo `TagRepository`) y
entrega 3 orquestadores (`SaveCurrentFilterUseCase`, `ApplySavedViewUseCase`,
`GetSavedViewsUseCase`) que consumirá la UI de 4-008. Todo con pure xUnit
`Assert.*`, sin FluentAssertions, sin mocking library.

---

## Resumen de tareas

| ID | Tarea | Descripción corta | Estimación diff | Depende de | Verificación |
| --- | --- | --- | --- | --- | --- |
| T-001 | Promover `SavedView.cs` a `sealed class` | 7 props, 2 ctors privados, `Create`/`Update`, `ValidateAndNormalize` | +120 | ninguna | build exit 0; tests existentes no rompen |
| T-002 | Crear `SavedViewConfiguration.cs` + `AppDbContext.SavedViews` | Tabla `SavedViews`, PK, unique index `Name`, index `EntityType` | +50 | T-001 | build exit 0; modelo EF Core válido |
| T-003 | Generar migración `AddSavedViewsTable` | `dotnet ef migrations add`; verificar `Up`/`Down` | +50 | T-002 | migración generada; build exit 0 |
| T-004 | Crear `SavedViewRepository.cs` + `SavedViewRepositoryTests.cs` | 6 métodos espejo `TagRepository`; ≥6 integration tests | +180 | T-001, T-003 | tests `SavedViewRepository` pasan |
| T-005 | Extender `ISavedViewRepository.cs` + DI Scoped | Añadir `GetByEntityTypeAsync`; registrar `ISavedViewRepository` en `AddPersistence` | +15 | T-001 | build exit 0 |
| T-006 | Crear `FilterJsonSerializer.cs` + tests | Envelope versionado + overload no-genérico; ≥6 round-trip/version tests | +120 | ninguna | tests `FilterJsonSerializer` pasan |
| T-007 | Crear `SaveCurrentFilterUseCase` + Command + Validator + tests | 4 deps, dispatch por tipo, pairing D-12; ≥4 unit tests | +140 | T-001, T-006 | tests `SaveCurrentFilterUseCase` pasan |
| T-008 | Crear `ApplySavedViewUseCase` + Query + Result + tests | 2 deps, deserializa según `EntityType`; ≥3 unit tests | +120 | T-001, T-006 | tests `ApplySavedViewUseCase` pasan |
| T-009 | Crear `GetSavedViewsUseCase` + Query + tests + Bootstrap DI + fake | 1 dep, filtro opcional por `EntityType`; ≥3 unit tests; 4 Transient en Bootstrap | +110 | T-001 | tests `GetSavedViewsUseCase` pasan |
| T-010 | Verificación final, formato y cierre | Build, tests completos, NetArchTest, format, scope check | 0 | T-001 a T-009 | todos los gates verdes |

**Total estimado:** ~1,105 líneas cambiadas. T-001, T-004, T-007 y T-008 son las más densas;
T-002, T-003, T-005 y T-009 son mecánicas; T-010 es verificación y cierre administrativo.

---

## [x] T-001 — Promover `SavedView` a `sealed class` con invariantes

**AC refs:** AC-1

**Pre-condiciones**
- `src/.../Domain/Views/SavedView.cs` verificado como `sealed record` placeholder de 13 líneas.
- `src/.../Domain/Catalog/Tag.cs` verificado como patrón `sealed class` con private setters,
  2 ctors privados, `Create`/`Update`, `ValidateAndNormalize`.
- `src/.../Domain/Enums/TransactionType.cs` existe con `{ Expense, Income }`.

**Pasos**

1. Abrir `src/ControlDeFinanzasIA.Domain/Views/SavedView.cs`.
2. Reemplazar `public sealed record SavedView` por `public sealed class SavedView` sin base.
3. Añadir `using ControlDeFinanzasIA.Domain.Enums;` al tope (referencia intra-Domain, R-1 OK).
4. Declarar 7 propiedades con `private set`:
   - `Guid Id`
   - `string Name`
   - `TransactionType EntityType`
   - `string FilterJson`
   - `int SchemaVersion`
   - `DateTime CreatedAt`
   - `DateTime UpdatedAt`
5. Añadir dos constructores privados: parameterless para EF Core y parametrizado
   `(Guid id, string name, TransactionType entityType, string filterJson, int schemaVersion, DateTime createdAt, DateTime updatedAt)`.
6. Implementar `public static SavedView Create(string name, TransactionType entityType, string filterJson, DateTime createdAt)`:
   - Lanzar `ArgumentNullException` si `name` o `filterJson` son null.
   - Validar y normalizar vía `ValidateAndNormalize(name, filterJson)`.
   - Verificar `Enum.IsDefined(typeof(TransactionType), entityType)`.
   - Verificar `createdAt != default`.
   - Asignar `Id = Guid.NewGuid()`, `SchemaVersion = 1`, `UpdatedAt = createdAt`.
7. Implementar `public void Update(string name, string filterJson, DateTime updatedAt)`:
   - Revalidar `name`/`filterJson`.
   - Verificar `updatedAt != default`.
   - Mutar `Name`, `FilterJson`, `UpdatedAt` únicamente.
   - NO modificar `Id`, `EntityType`, `SchemaVersion`, `CreatedAt`.
8. Implementar `private static (string Name, string FilterJson) ValidateAndNormalize(string name, string filterJson)`:
   - `name = name.Trim();` → exigir `!string.IsNullOrWhiteSpace(name)` y `name.Length <= 100`.
   - `filterJson = filterJson.Trim();` → exigir `!string.IsNullOrWhiteSpace(filterJson)`.
9. Añadir XML doc en la clase, cada propiedad, `Create` y `Update` (español, espejo `Tag`).
10. Ejecutar `dotnet build -c Debug --nologo`.

**Criterios de verificación**
- `dotnet build` exit 0, 0 warnings, 0 errors.
- `public sealed class SavedView` sin herencia, 7 props `private set`, 2 ctores privados,
  `Create` y `Update` públicos, `ValidateAndNormalize` privado.
- `ISavedViewRepository.cs` sigue compilando (mismo namespace `Domain.Views`).
- `Create("Transporte", TransactionType.Expense, json, ts)` produce `SchemaVersion == 1`,
  `CreatedAt == UpdatedAt == ts`, `Id != Guid.Empty`.
- `Update` cambia `Name`/`FilterJson`/`UpdatedAt`; preserva `Id`, `EntityType`, `SchemaVersion`, `CreatedAt`.

**Notas**
- D-1: NO mover a `Domain/Catalog/`; conservar namespace `ControlDeFinanzasIA.Domain.Views`.
- D-3: `SchemaVersion` se estampa internamente = 1; ningún caller suministra otra versión.
- D-12: `EntityType` es inmutable vía `Update`.
- Los mensajes de excepción están en inglés (espejo `Tag`); los mensajes de error del repo
  en español (D-ML de F-005).

---

## [x] T-002 — Crear `SavedViewConfiguration.cs` + `AppDbContext.SavedViews`

**AC refs:** AC-2, AC-6

**Pre-condiciones**
- T-001 completada (`SavedView` es entidad).
- `src/.../Infrastructure/Persistence/Configurations/TagConfiguration.cs` verificado como patrón.
- `src/.../Infrastructure/Persistence/AppDbContext.cs` verificado con 5 DbSets expression-bodied.

**Pasos**

1. Crear `src/ControlDeFinanzasIA.Infrastructure/Persistence/Configurations/SavedViewConfiguration.cs`:
   - file-scoped namespace `ControlDeFinanzasIA.Infrastructure.Persistence.Configurations`.
   - `public sealed class SavedViewConfiguration : IEntityTypeConfiguration<SavedView>`.
   - `b.ToTable("SavedViews")`.
   - `b.HasKey(s => s.Id)`; `b.Property(s => s.Id).ValueGeneratedNever()`.
   - `b.Property(s => s.Name).IsRequired().HasMaxLength(100)`.
   - `b.HasIndex(s => s.Name).IsUnique()` (D-4).
   - `b.Property(s => s.EntityType).IsRequired().HasConversion<int>()`.
   - `b.HasIndex(s => s.EntityType)` (no único — alimenta `GetByEntityTypeAsync`).
   - `b.Property(s => s.FilterJson).IsRequired().HasColumnType("TEXT")`.
   - `b.Property(s => s.SchemaVersion).IsRequired().HasDefaultValue(1)`.
   - `b.Property(s => s.CreatedAt).IsRequired()`.
   - `b.Property(s => s.UpdatedAt).IsRequired()`.
   - XML doc en clase + `Configure` (español, espejo `TagConfiguration`).
2. Abrir `src/.../Infrastructure/Persistence/AppDbContext.cs`.
3. Añadir `using ControlDeFinanzasIA.Domain.Views;` (orden alfabético tras `Domain.Transactions`).
4. Añadir después de `public DbSet<Tag> Tags => Set<Tag>();`:
   ```csharp
   /// <summary>
   /// Conjunto de entidades <see cref="SavedView" /> mapeado a la tabla
   /// <c>SavedViews</c> en SQLite.
   /// </summary>
   public DbSet<SavedView> SavedViews => Set<SavedView>();
   ```
5. Actualizar XML doc summary de la clase para mencionar `SavedView` en la lista de conjuntos.
6. Ejecutar `dotnet build -c Debug --nologo`.

**Criterios de verificación**
- `dotnet build` exit 0, 0 warnings, 0 errors.
- `SavedViewConfiguration` usa `ToTable("SavedViews")`, índice único `Name`, `EntityType` int + índice,
  `FilterJson` TEXT requerido, `SchemaVersion` default 1, timestamps requeridos.
- `AppDbContext.SavedViews` es expression-bodied get-only, ubicado tras `Tags`.

**Notas**
- `SavedView` no tiene navegación inversa hacia `Expense`/`Income` — es catálogo puro de vistas.
- No tocar `OnModelCreating`; la configuración se descubre por `ApplyConfigurationsFromAssembly`.

---

## [x] T-003 — Generar migración `AddSavedViewsTable`

**AC refs:** AC-5

**Pre-condiciones**
- T-002 completada (`SavedViewConfiguration` + `DbSet<SavedViews>`).
- `dotnet-ef` 8.0.11 disponible localmente.
- Última migración existente: `20260903233252_AddIncomeTagsJoinTable`.

**Pasos**

1. Ejecutar:
   ```powershell
   dotnet ef migrations add AddSavedViewsTable `
     --project src/ControlDeFinanzasIA.Infrastructure `
     --startup-project src/ControlDeFinanzasIA.UI
   ```
2. Verificar que se crearon 3 archivos en `src/.../Infrastructure/Persistence/Migrations/`:
   - `<timestamp>_AddSavedViewsTable.cs`
   - `<timestamp>_AddSavedViewsTable.Designer.cs`
   - `AppDbContextModelSnapshot.cs` (actualizado)
3. Inspeccionar `<timestamp>_AddSavedViewsTable.cs`:
   - `Up` crea `SavedViews` (`Id`, `Name`, `EntityType`, `FilterJson`, `SchemaVersion`, `CreatedAt`, `UpdatedAt`) + PK + `IX_SavedViews_Name` único + `IX_SavedViews_EntityType`.
   - `Up` no contiene `InsertData` (no seed).
   - `Up` no toca `Categories`/`PaymentMethods`/`Expenses`/`Incomes`/`Tags`/`ExpenseTags`/`IncomeTags`.
   - `Down`: `DropTable("SavedViews")` (una sola tabla).
4. Si EF Core generó namespace block-scoped, corregir a file-scoped (IDE0161)
   como en F-001 P4 C-5 / F-004 AC-8 / F-007 AC-3.
5. Ejecutar `dotnet build -c Debug --nologo`.

**Criterios de verificación**
- `dotnet build` exit 0, 0 warnings, 0 errors.
- Migración generada con `Up`/`Down` correctos; file-scoped namespace.

**Notas**
- UNA sola migración `AddSavedViewsTable`.
- `--startup-project` debe ser `src/ControlDeFinanzasIA.UI` (convención del repo).
- La migración es aditiva pura; no altera tablas previas.

---

## [x] T-004 — Crear `SavedViewRepository.cs` + `SavedViewRepositoryTests.cs`

**AC refs:** AC-4, AC-15

**Pre-condiciones**
- T-001 completada (`SavedView` con `Update`).
- T-003 completada (migración generada; schema disponible).
- `src/.../Infrastructure/Persistence/Repositories/TagRepository.cs` verificado como plantilla.
- `TestAppDbContextFactory.cs` verificado con `CreateContext()` + `MigrateAsync()`.

**Pasos**

1. Crear `src/ControlDeFinanzasIA.Infrastructure/Persistence/Repositories/SavedViewRepository.cs`:
   - file-scoped namespace `ControlDeFinanzasIA.Infrastructure.Persistence.Repositories`.
   - `public sealed class SavedViewRepository : ISavedViewRepository`.
   - Campo `private readonly AppDbContext _db;` y ctor `public SavedViewRepository(AppDbContext db)`.
   - `AddAsync(SavedView savedView, ct)`:
     - `ArgumentNullException.ThrowIfNull(savedView)`.
     - Pre-check `AnyAsync(s => s.Name == savedView.Name, ct)` → `Result.Failure("DuplicateName", $"Ya existe una vista guardada con el nombre '{savedView.Name}'.")`.
     - try `Add` + `SaveChangesAsync` → `Result.Success(null!)`.
     - catch `DbUpdateException` cuando `IsUniqueViolation(ex)` → mismo `DuplicateName`.
   - `UpdateAsync(SavedView savedView, ct)`:
     - `FindAsync(savedView.Id, ct)`; null → `Result.Failure("NotFound", $"Vista guardada {savedView.Id} no encontrada.")`.
     - `existing.Update(savedView.Name, savedView.FilterJson, savedView.UpdatedAt)`.
     - `SaveChangesAsync` → `Success(null!)`.
   - `RemoveAsync(Guid id, ct)`:
     - guard `Guid.Empty` → `NotFound`.
     - `FindAsync`; null → `NotFound`; `Remove` + `SaveChangesAsync` → `Success(null!)`.
   - `GetByIdAsync(Guid id, ct)`: guard `Guid.Empty` → null; else `FindAsync`.
   - `GetAllAsync(ct)`: `AsNoTracking().OrderBy(s => s.Name).ToListAsync(ct)`.
   - `GetByEntityTypeAsync(TransactionType entityType, ct)`: `AsNoTracking().Where(s => s.EntityType == entityType).OrderBy(s => s.Name).ToListAsync(ct)`.
   - Private `static bool IsUniqueViolation(DbUpdateException ex)` (espejo: `SqliteException.SqliteErrorCode == 19`).
   - XML doc en clase + cada método público (español, espejo `TagRepository`).
2. Crear `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/SavedViewRepositoryTests.cs`:
   - file-scoped namespace `ControlDeFinanzasIA.Tests.Integration.Persistence`.
   - Fixture por test: `using var factory = new TestAppDbContextFactory(); using AppDbContext context = factory.CreateContext(); await context.Database.MigrateAsync();`.
   - Helper `CreateSavedView(string name, TransactionType entityType, string? filterJson = null)` con `_fixedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)`.
   - Implementar ≥6 tests (espejo `TagRepositoryTests`):
     - `AddAsync_WithUniqueName_Persists`.
     - `AddAsync_WithDuplicateName_ReturnsDuplicateName` (mismo nombre, distinto `EntityType` — probar que el unique index es solo sobre `Name`).
     - `UpdateAsync_Existing_UpdatesFields` (usa `view.Update(...)`; verifica `EntityType`/`SchemaVersion`/`CreatedAt` sin cambios).
     - `RemoveAsync_Existing_DeletesSavedView`.
     - `GetByIdAsync_Existing_ReturnsSavedView`.
     - `GetAllAsync_ReturnsAllOrderedByName`.
     - `GetByEntityTypeAsync_ReturnsMatchingType`.
   - Opcionales recomendados: `UpdateAsync_Missing_ReturnsNotFound`, `RemoveAsync_NonExistent_ReturnsNotFound`,
     `RemoveAsync_WithEmptyGuid_ReturnsNotFound`, `FilterJson_RoundTripsThroughDatabase`.
3. Ejecutar `dotnet build -c Debug --nologo` y
   `dotnet test -c Debug --no-build --nologo --filter "FullyQualifiedName~SavedViewRepository"`.

**Criterios de verificación**
- `dotnet build` exit 0, 0 warnings, 0 errors.
- `SavedViewRepository` implementa 6 métodos.
- `dotnet test --filter "FullyQualifiedName~SavedViewRepository"` ≥6 passed, 0 failed.

**Notas**
- D-4: unicidad exacta case-sensitive (espejo `TagRepository`).
- D-11: `GetByEntityTypeAsync` es el sexto método; devuelve lista ordenada por `Name`.
- `UpdateAsync` requiere `SavedView.Update` (T-001) para construir una instancia mutada con el `Id` existente.

---

## [x] T-005 — Extender `ISavedViewRepository` + registrar DI Scoped

**AC refs:** AC-3, AC-7

**Pre-condiciones**
- T-001 completada (`SavedView` existe).
- `src/.../Application/Abstractions/ISavedViewRepository.cs` verificado con 5 métodos.
- `src/.../Infrastructure/Persistence/ServiceCollectionExtensions.cs` verificado con registros Scoped existentes.

**Pasos**

1. Abrir `src/ControlDeFinanzasIA.Application/Abstractions/ISavedViewRepository.cs`.
2. Añadir `using ControlDeFinanzasIA.Domain.Enums;` al tope.
3. Añadir al final, con XML doc en español:
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
4. Verificar que las 5 firmas existentes quedan intactas.
5. Abrir `src/.../Infrastructure/Persistence/ServiceCollectionExtensions.cs`.
6. Añadir `services.AddScoped<ISavedViewRepository, SavedViewRepository>();` tras `AddScoped<ITagRepository, TagRepository>();`.
7. Actualizar XML doc de `AddPersistence` para listar `ISavedViewRepository` y añadir `<para>` referenciando F-006 Phase 4.
8. Ejecutar `dotnet build -c Debug --nologo`.

**Criterios de verificación**
- `dotnet build` exit 0, 0 warnings, 0 errors.
- `ISavedViewRepository` tiene 6 métodos; las 5 firmas originales no cambian.
- `AddPersistence` registra `ISavedViewRepository` como Scoped.

**Notas**
- No se requieren usings nuevos en `ServiceCollectionExtensions.cs` (ya existen `Application.Abstractions` y `Persistence.Repositories`).
- El registro Scoped asegura que el repo comparta `AppDbContext` con otros repos en el mismo request/scope.

---

## [x] T-006 — Crear `FilterJsonSerializer.cs` + `FilterJsonSerializerTests.cs`

**AC refs:** AC-8

**Pre-condiciones**
- `ExpenseFilter.cs` / `IncomeFilter.cs` verificados como records tontos de 6 props `init` con `TagIds`.
- `System.Text.Json` es BCL en .NET 8; ningún paquete nuevo.

**Pasos**

1. Crear `src/ControlDeFinanzasIA.Application/Common/FilterJsonSerializer.cs`:
   - file-scoped namespace `ControlDeFinanzasIA.Application.Common`.
   - `public static class FilterJsonSerializer`.
   - `public const int CurrentSchemaVersion = 1;`.
   - Opciones STJ privadas estáticas: `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`, `WriteIndented = false`.
   - `public static string Serialize<T>(T filter) where T : class`:
     - Envuelve en envelope `{ "schemaVersion": CurrentSchemaVersion, "type": typeof(T).Name, "data": <filter> }`.
     - Serializa el envelope completo.
   - `public static T Deserialize<T>(string filterJson) where T : class`:
     - Guard `filterJson` null → `ArgumentNullException`.
     - Parsea envelope tolerante (`TryParseEnvelope`); default `schemaVersion` = 1 si ausente.
     - Si `schemaVersion > CurrentSchemaVersion` → `InvalidOperationException($"Schema version {envelope.SchemaVersion} is not supported. Current version: {CurrentSchemaVersion}.")`.
     - Si `schemaVersion < 1` → `JsonException`.
     - Si `type` ≠ `typeof(T).Name` → `JsonException`.
     - Si `data` ausente o no es objeto → `JsonException`.
     - Deserializa `data` a `T`.
   - `public static object Deserialize(string filterJson, Type targetType)`:
     - Mismas reglas que el genérico; chequea `type` contra `targetType.Name`; retorna `object`.
   - Modelo interno: `private sealed record Envelope(int SchemaVersion, string Type, JsonElement Data);`.
   - XML doc en clase + cada miembro público (español, espejo `Result.cs`).
2. Crear `tests/ControlDeFinanzasIA.Tests.Unit/Application/Common/FilterJsonSerializerTests.cs`:
   - file-scoped namespace `ControlDeFinanzasIA.Tests.Unit.Application.Common`.
   - ≥6 tests:
     - `Serialize_Then_Deserialize_Round_Trip_PreservesAllProps` (ExpenseFilter con TagIds).
     - `Serialize_Then_Deserialize_RoundTrip_PreservesIncomeFilter`.
     - `Deserialize_WithUnknownSchemaVersion_ThrowsInvalidOperationException` (JSON hard-coded con `"schemaVersion":2`).
     - `Deserialize_WithMissingSchemaVersion_DefaultsTo1` (JSON legacy hard-coded sin `schemaVersion`).
     - `Deserialize_WithMalformedJson_ThrowsJsonException`.
     - `Serialize_GeneratesCorrectEnvelopeShape` (verifica keys camelCase, `WriteIndented=false`).
     - `Deserialize_WithTypeMismatch_ThrowsJsonException`.
     - `Deserialize_NonGeneric_MatchesGenericOutcome`.
3. Ejecutar `dotnet build -c Debug --nologo` y
   `dotnet test -c Debug --no-build --nologo --filter "FullyQualifiedName~FilterJsonSerializer"`.

**Criterios de verificación**
- `dotnet build` exit 0, 0 warnings, 0 errors.
- `Serialize` → `Deserialize` preserva las 6 props incluyendo `TagIds` y `null`s.
- `Serialize(Deserialize(Serialize(f)))` produce el mismo string (round-trip estable).
- Versiones futuras rechazan con `InvalidOperationException`; JSON legacy se interpreta como v1; JSON malformado/type mismatch lanzan `JsonException`.

**Notas**
- D-5: envelope `{ schemaVersion, type, data }`.
- D-6: serialización directa del filter, sin DTOs.
- D-9: forward-compat throw cuando `schemaVersion > CurrentSchemaVersion`.
- D-10: backward-compat default a 1 cuando `schemaVersion` ausente; explícito `< 1` → `JsonException`.

---

## [x] T-007 — Crear `SaveCurrentFilterUseCase` + Command + Validator + tests

**AC refs:** AC-9

**Pre-condiciones**
- T-001 completada (`SavedView.Create`).
- T-006 completada (`FilterJsonSerializer`).
- `RegisterIncomeUseCase`/`RegisterIncomeValidator` verificados como patrón.
- `FakeClock` y `NullLogger<>.Instance` disponibles.

**Pasos**

1. Crear `src/.../Application/UseCases/SavedViews/SaveCurrentFilter/SaveCurrentFilterCommand.cs`:
   - file-scoped namespace `ControlDeFinanzasIA.Application.UseCases.SavedViews.SaveCurrentFilter`.
   - `public sealed record SaveCurrentFilterCommand(string Name, TransactionType EntityType, object Filter);`.
   - XML doc en inglés.
2. Crear `src/.../Application/UseCases/SavedViews/SaveCurrentFilter/SaveCurrentFilterValidator.cs`:
   - `public sealed class SaveCurrentFilterValidator : AbstractValidator<SaveCurrentFilterCommand>`.
   - 2 reglas sync:
     - `Name`: `NotEmpty`, `MaximumLength(100)`, `Must(n => n.Trim().Length > 0)` — mensajes en español.
     - `Filter`: `NotNull` — mensaje en español.
3. Crear `src/.../Application/UseCases/SavedViews/SaveCurrentFilter/SaveCurrentFilterUseCase.cs`:
   - `public sealed class SaveCurrentFilterUseCase`.
   - 4 deps readonly: `ISavedViewRepository`, `IClock`, `IValidator<SaveCurrentFilterCommand>`, `ILogger<SaveCurrentFilterUseCase>`.
   - `ExecuteAsync(SaveCurrentFilterCommand command, CancellationToken ct = default)`:
     - `ArgumentNullException.ThrowIfNull(command)`.
     - `_validator.Validate(command)` sync → inválido → `Result.Failure("Validation", "El comando para guardar la vista no es válido.", details)`.
     - Dispatch por runtime type de `command.Filter` (D-12):
       - `ExpenseFilter` → exige `EntityType == TransactionType.Expense`; serializa.
       - `IncomeFilter` → exige `EntityType == TransactionType.Income`; serializa.
       - otro tipo → `Result.Failure("Validation", $"Unsupported filter type: {command.Filter.GetType()}.")`.
       - mismatch EntityType↔Filter → `Result.Failure("Validation", "El tipo del filtro no coincide con el tipo de entidad de la vista.")`.
     - `try { var savedView = SavedView.Create(command.Name.Trim(), command.EntityType, filterJson, _clock.UtcNow); } catch (ArgumentException ex) { _logger.LogWarning(ex, "..."); return Result.Failure("DomainInvariant", ex.Message); }`.
     - `var addResult = await _savedViewRepository.AddAsync(savedView, ct);` → si falla, propagar.
     - `return Result.Success(savedView);`.
   - XML doc en inglés.
4. Crear `tests/ControlDeFinanzasIA.Tests.Unit/Application/SavedViews/SaveCurrentFilter/SaveCurrentFilterUseCaseTests.cs`:
   - file-scoped namespace `ControlDeFinanzasIA.Tests.Unit.Application.SavedViews.SaveCurrentFilter`.
   - Fixture: `FakeClock`, `FakeSavedViewRepository`, `SaveCurrentFilterValidator` real, `NullLogger<SaveCurrentFilterUseCase>.Instance`.
   - Helper `CreateValidCommand(TransactionType entityType)`.
   - ≥4 tests (ver §6.2 de la spec):
     - `ExecuteAsync_WithValidCommand_Persists`.
     - `ExecuteAsync_WithEmptyName_ReturnsValidationFailure`.
     - `ExecuteAsync_WithDuplicateName_ReturnsDuplicateName`.
     - `ExecuteAsync_AssignsSchemaVersionFromSerializer`.
     - Opcionales: Income happy path, >100 chars, null filter, unsupported type, pairing mismatch.
5. Ejecutar `dotnet build -c Debug --nologo` y
   `dotnet test -c Debug --no-build --nologo --filter "FullyQualifiedName~SaveCurrentFilterUseCase"`.

**Criterios de verificación**
- `dotnet build` exit 0, 0 warnings, 0 errors.
- `SaveCurrentFilterUseCase` retorna `Result.Success(SavedView)` con `SchemaVersion == CurrentSchemaVersion`.
- Validación rechaza name vacío/whitespace/>100 y filter null.
- `DuplicateName` del repo se propaga con código `"DuplicateName"`.
- `dotnet test --filter "FullyQualifiedName~SaveCurrentFilterUseCase"` ≥4 passed, 0 failed.

**Notas**
- D-12: el guard de pairing es crítico — nunca persistir una vista cuyo `EntityType` contradiga el tipo del filter.
- El command lleva `Filter: object`; el use case despacha por runtime type.
- Timestamps desde `IClock`; nunca del caller.

---

## [x] T-008 — Crear `ApplySavedViewUseCase` + Query + Result + tests

**AC refs:** AC-10

**Pre-condiciones**
- T-001 completada (`SavedView` con `EntityType`).
- T-006 completada (`FilterJsonSerializer`).
- `GetTransactionsQuery` verificado como patrón de query simple.

**Pasos**

1. Crear `src/.../Application/UseCases/SavedViews/ApplySavedView/ApplySavedViewQuery.cs`:
   - file-scoped namespace `ControlDeFinanzasIA.Application.UseCases.SavedViews.ApplySavedView`.
   - `public sealed record ApplySavedViewQuery(Guid SavedViewId);`.
   - XML doc en inglés.
2. Crear `src/.../Application/UseCases/SavedViews/ApplySavedView/ApplySavedViewResult.cs`:
   - file-scoped namespace; `using ControlDeFinanzasIA.Domain.Enums;`.
   - `public sealed record ApplySavedViewResult(TransactionType EntityType, object Filter);`.
   - XML doc en inglés.
3. Crear `src/.../Application/UseCases/SavedViews/ApplySavedView/ApplySavedViewUseCase.cs`:
   - `public sealed class ApplySavedViewUseCase`.
   - 2 deps readonly: `ISavedViewRepository`, `ILogger<ApplySavedViewUseCase>`.
   - `ExecuteAsync(ApplySavedViewQuery query, CancellationToken ct = default)`:
     - `ArgumentNullException.ThrowIfNull(query)`.
     - `var savedView = await _savedViewRepository.GetByIdAsync(query.SavedViewId, ct);` → null → `Result.Failure("NotFound", $"Vista guardada {query.SavedViewId} no encontrada.")`.
     - Switch sobre `savedView.EntityType`:
       - `TransactionType.Expense` → deserializa `ExpenseFilter`; success con `ApplySavedViewResult(Expense, filter)`.
       - `TransactionType.Income` → deserializa `IncomeFilter`; success con `ApplySavedViewResult(Income, filter)`.
       - default → `Result.Failure("CorruptedFilter", $"Unknown saved view entity type: {savedView.EntityType}.")`.
     - try/catch alrededor de la deserialización:
       - `InvalidOperationException` → `Result.Failure("IncompatibleVersion", ex.Message)`.
       - `JsonException` → `Result.Failure("CorruptedFilter", ex.Message)`.
   - XML doc en inglés.
4. Crear `tests/ControlDeFinanzasIA.Tests.Unit/Application/SavedViews/ApplySavedView/ApplySavedViewUseCaseTests.cs`:
   - file-scoped namespace `ControlDeFinanzasIA.Tests.Unit.Application.SavedViews.ApplySavedView`.
   - Fixture: `FakeSavedViewRepository`, `NullLogger<ApplySavedViewUseCase>.Instance`.
   - Helper `CreateSavedView(...)` que persiste en el fake vía `AddAsync`.
   - ≥3 tests (ver §6.3 de la spec):
     - `ExecuteAsync_WithExpenseSavedView_ReturnsExpenseFilter`.
     - `ExecuteAsync_WithIncomeSavedView_ReturnsIncomeFilter`.
     - `ExecuteAsync_WithNonExistentId_ReturnsNotFound`.
     - Opcionales: empty Guid, future schema version, corrupted JSON, legacy JSON.
5. Ejecutar `dotnet build -c Debug --nologo` y
   `dotnet test -c Debug --no-build --nologo --filter "FullyQualifiedName~ApplySavedViewUseCase"`.

**Criterios de verificación**
- `dotnet build` exit 0, 0 warnings, 0 errors.
- `ApplySavedViewUseCase` retorna `Result.Success(ApplySavedViewResult)` con filter tipado y `EntityType` correcto.
- Vista inexistente → `Failure("NotFound")`.
- `schemaVersion` futuro → `Failure("IncompatibleVersion")`; JSON corrupto → `Failure("CorruptedFilter")`.
- `dotnet test --filter "FullyQualifiedName~ApplySavedViewUseCase"` ≥3 passed, 0 failed.

**Notas**
- D-9: forward-compat throw mapeado a `IncompatibleVersion`.
- D-10: legacy JSON sin `schemaVersion` debe deserializar como v1 (test AS-7).
- El use case es self-describing: no requiere que el caller declare el tipo esperado.

---

## [x] T-009 — Crear `GetSavedViewsUseCase` + Query + tests + `FakeSavedViewRepository` + Bootstrap DI

**AC refs:** AC-11, AC-12, AC-13

**Pre-condiciones**
- T-001 completada (`SavedView` existe).
- T-005 completada (`ISavedViewRepository.GetByEntityTypeAsync`).
- `Bootstrap.cs` verificado con registros Transient existentes.
- `FakeIncomeRepository.cs` verificado como patrón de fake.

**Pasos**

1. Crear `src/.../Application/UseCases/SavedViews/GetSavedViews/GetSavedViewsQuery.cs`:
   - file-scoped namespace `ControlDeFinanzasIA.Application.UseCases.SavedViews.GetSavedViews`.
   - `using ControlDeFinanzasIA.Domain.Enums;`.
   - `public sealed record GetSavedViewsQuery(TransactionType? EntityType = null);`.
   - XML doc en inglés.
2. Crear `src/.../Application/UseCases/SavedViews/GetSavedViews/GetSavedViewsUseCase.cs`:
   - `public sealed class GetSavedViewsUseCase`.
   - 1 dep readonly: `ISavedViewRepository`.
   - `ExecuteAsync(GetSavedViewsQuery query, CancellationToken ct = default)`:
     - `ArgumentNullException.ThrowIfNull(query)`.
     - `var views = query.EntityType is { } et ? await _savedViewRepository.GetByEntityTypeAsync(et, ct) : await _savedViewRepository.GetAllAsync(ct);`.
     - `return Result.Success(views);`.
   - XML doc en inglés.
3. Crear `tests/ControlDeFinanzasIA.Tests.Unit/TestDoubles/FakeSavedViewRepository.cs`:
   - `public sealed class FakeSavedViewRepository : ISavedViewRepository`.
   - Hand-rolled; espejo `FakeIncomeRepository`.
   - Propiedades públicas: `AddResultOverride`, `UpdateResultOverride`, `RemoveResultOverride`, `GetByIdResultOverride`, `AddedSavedViews`.
   - Implementar 6 métodos con `/// <inheritdoc />`; `GetAllAsync`/`GetByEntityTypeAsync` leen de la lista capturada.
4. Crear `tests/ControlDeFinanzasIA.Tests.Unit/Application/SavedViews/GetSavedViews/GetSavedViewsUseCaseTests.cs`:
   - file-scoped namespace `ControlDeFinanzasIA.Tests.Unit.Application.SavedViews.GetSavedViews`.
   - Fixture: `FakeSavedViewRepository`.
   - Siembra vía `AddAsync` en el fake.
   - ≥3 tests (ver §6.4 de la spec):
     - `ExecuteAsync_WithEntityTypeFilter_ReturnsMatching`.
     - `ExecuteAsync_WithoutEntityType_ReturnsAll`.
     - `ExecuteAsync_WithNoSavedViews_ReturnsEmpty`.
5. Abrir `src/ControlDeFinanzasIA.UI/Composition/Bootstrap.cs`:
   - Añadir 3 usings (orden alfabético):
     - `ControlDeFinanzasIA.Application.UseCases.SavedViews.ApplySavedView`
     - `ControlDeFinanzasIA.Application.UseCases.SavedViews.GetSavedViews`
     - `ControlDeFinanzasIA.Application.UseCases.SavedViews.SaveCurrentFilter`
   - Añadir 4 registros Transient tras `IValidator<UpdateIncomeCommand>`:
     - `services.AddTransient<SaveCurrentFilterUseCase>();`
     - `services.AddTransient<ApplySavedViewUseCase>();`
     - `services.AddTransient<GetSavedViewsUseCase>();`
     - `services.AddTransient<IValidator<SaveCurrentFilterCommand>, SaveCurrentFilterValidator>();`
   - Actualizar XML doc/remarks de `ConfigureServices` para mencionar los 3 orquestadores + validator.
6. Ejecutar `dotnet build -c Debug --nologo` y
   `dotnet test -c Debug --no-build --nologo --filter "FullyQualifiedName~GetSavedViewsUseCase"`.

**Criterios de verificación**
- `dotnet build` exit 0, 0 warnings, 0 errors.
- `GetSavedViewsUseCase` retorna `Result.Success(IReadOnlyList<SavedView>)`.
- `Bootstrap` resuelve `SaveCurrentFilterUseCase` con sus 4 deps (`ISavedViewRepository` Scoped,
  `IClock` singleton, validator Transient, `ILogger<>`).
- `dotnet test --filter "FullyQualifiedName~GetSavedViewsUseCase"` ≥3 passed, 0 failed.

**Notas**
- D-8: NO registrar `DeleteSavedViewUseCase` (diferido a 4-008).
- El fake debe permitir sembrar vistas vía `AddAsync` para que `GetAllAsync`/`GetByEntityTypeAsync` devuelvan datos.

---

## [x] T-010 — Verificación final, formato y cierre

**AC refs:** AC-16

**Pre-condiciones**
- T-001 a T-009 completadas.

**Pasos**

1. Ejecutar `dotnet build -c Release --nologo` → 0 warnings, 0 errors.
2. Ejecutar `dotnet build -c Debug --nologo` → 0 warnings, 0 errors.
3. Ejecutar `dotnet test -c Debug --no-build --nologo --filter "FullyQualifiedName~SavedViewRepository|FullyQualifiedName~FilterJsonSerializer|FullyQualifiedName~SaveCurrentFilterUseCase|FullyQualifiedName~ApplySavedViewUseCase|FullyQualifiedName~GetSavedViewsUseCase"` → todos pasan.
4. Ejecutar `dotnet test -c Debug --no-build --nologo --filter "FullyQualifiedName~ArchitectureTests"` → 11/11 passed.
5. Ejecutar `dotnet test -c Debug --no-build --nologo` (suite completa Debug) → verde excepto el 1 fallo pre-existing conocido de `Money`.
6. Ejecutar `dotnet format style --verify-no-changes` → exit 0.
7. Ejecutar `dotnet list src/ControlDeFinanzasIA.Domain package` → vacío (R-1).
8. Verificar scope (AC-16):
   - No modificaciones a `ExpenseFilter.cs`, `IncomeFilter.cs`, `Expense.cs`, `Income.cs`, `Tag.cs`, `TransactionType.cs`.
   - No modificaciones a `IExpenseRepository`/`IIncomeRepository`/`ITagRepository` + implementaciones.
   - No modificaciones a `Money`/`Currency`/`Result`/`PagedResult`/`IClock`, use cases existentes.
   - No UI / ViewModels / XAML salvo `Bootstrap.cs`.
   - No `SeedData`, migraciones previas, fakes existentes, `TestAppDbContextFactory`.
   - No `DeleteSavedViewUseCase` creado (D-8).
   - No FluentAssertions, no mocking library, no paquetes NuGet nuevos.
9. Actualizar este archivo de tareas marcando todas las casillas `[x]`.
10. Reportar conteos exactos de tests observados.

**Criterios de verificación**
- `dotnet build -c Release` exit 0, 0 warnings, 0 errors.
- `dotnet build -c Debug` exit 0, 0 warnings, 0 errors.
- `dotnet test -c Debug --no-build` suite verde (baseline + ~35-40 nuevos; 1 fallo pre-existing de Money).
- `dotnet test --filter "FullyQualifiedName~ArchitectureTests"` 11/11 passed.
- `dotnet format style --verify-no-changes` exit 0.
- `dotnet list src/ControlDeFinanzasIA.Domain package` vacío.
- Sin archivos fuera de la lista in-scope modificados.

**Notas**
- No renombrar la spec a `.done.md`; eso corresponde a `sdd-archive` de la fase.
- Si `features.md` necesita actualización, marcar 4-006 como completada solo si todos los gates pasan.

---

## Orden de ejecución recomendado

**Orden secuencial con dependencias explícitas:**

```text
T-001 ─┬─ T-002 ── T-003 ─┬─ T-004 ─┐
       │                   │        │
       └─ T-005 ───────────┘        │
                                    │
T-006 ─┬─ T-007 ──┐                 │
       │          ├─ T-009 ── T-010 ┤
       └─ T-008 ──┘                 │
```

1. **T-001** debe ejecutarse primero (el resto necesita `SavedView` como entidad).
2. **T-002** depende de T-001 (configuración EF Core necesita la entidad).
3. **T-003** depende de T-002 (EF Core necesita la configuración para generar la migración).
4. **T-004** depende de T-001 y T-003 (`SavedView` + schema migrado).
5. **T-005** depende de T-001 (extensión del interface + DI; no requiere migración).
6. **T-006** puede ejecutarse en paralelo con T-001..T-005 (no depende de infraestructura).
7. **T-007** depende de T-001 y T-006.
8. **T-008** depende de T-001 y T-006.
9. **T-009** depende de T-001 y T-005 (uso del interface extendido y del fake).
10. **T-010** cierra la feature.

---

## Tareas bloqueantes / habilitadoras

| Tarea | Habilita | Razón |
| --- | --- | --- |
| T-001 | T-002, T-003, T-004, T-005, T-007, T-008, T-009 | `SavedView` debe ser `sealed class` con `Create`/`Update` antes de que EF Core, el repo y los use cases lo usen. |
| T-002 | T-003 | EF Core necesita descubrir `SavedViewConfiguration`. |
| T-003 | T-004 | Los tests de `SavedViewRepository` aplican la migración `AddSavedViewsTable`. |
| T-005 | T-009 | `GetSavedViewsUseCase` consume `GetByEntityTypeAsync`. |
| T-006 | T-007, T-008 | `SaveCurrentFilterUseCase` y `ApplySavedViewUseCase` dependen de `FilterJsonSerializer`. |
| T-009 | T-010 | Todos los tests y el DI deben estar listos antes del cierre. |

---

## Estimación total

| Tarea | Dificultad | Horas estimadas |
| --- | --- | --- |
| T-001 | M | 1.5 |
| T-002 | S | 0.5 |
| T-003 | S | 0.75 |
| T-004 | M | 1.5 |
| T-005 | XS | 0.25 |
| T-006 | M | 1.5 |
| T-007 | M | 1.5 |
| T-008 | M | 1.25 |
| T-009 | M | 1.25 |
| T-010 | S | 0.5 |
| **Total** | | **~10.5 horas (~1.3 jornadas)** |

---

## Riesgos y mitigaciones

| # | Riesgo | Probabilidad | Impacto | Mitigación |
| --- | --- | --- | --- | --- |
| R-1 | STJ: round-trip de `DateOnly`/`Guid`/`IReadOnlyList<Guid>` (`TagIds`) y naming camelCase; primer serializer de filtros en el codebase. | Media | Alto | Envelope parseado tolerante (`TryParseEnvelope`); tests JS-1/JS-2 con las 6 props + `null`s + byte-identity; JSON legacy/futuro hard-coded (JS-3/JS-4) blindan reglas de versión. |
| R-2 | Versionado JSON: futuro bump v1 → v2 rompe vistas guardadas. | Media | Alto | Envelope explícito + columna `SchemaVersion`; rechazo SOLO de versiones mayores (`IncompatibleVersion`); aceptación menores/iguales + legacy default 1; tripwire SC-4 fuerza revisión del estampado del factory. |
| R-3 | Duplicado de `Name` en BD real (race entre pre-check e insert). | Baja | Medio | Pre-check `AnyAsync` + unique index `IX_SavedViews_Name` + catch `DbUpdateException` (SQLite 19) — espejo `TagRepository`; IR-2 cubre el path determinista. |
| R-4 | EF emite namespace block-scoped en la migración → IDE0161. | Media | Bajo | AC-5 exige file-scoped namespace con corrección manual; gate `dotnet format` en T-010. |
| R-5 | Hueco de type-safety de `Filter: object` → mismatch `EntityType`⟺filter runtime type. | Media | Medio | Guard de pairing + dispatch por runtime type (D-12); SC-8/SC-9 blindan ambas ramas; switch de Apply con default defensivo + type check del serializer como segunda barrera. |
| R-6 | Exceso de líneas por PR (presupuesto 400). | Alta | Medio | Chained PRs stacked-to-main documentados en el pronóstico de review; PR-1 Domain+Infra ~415 líneas, PR-2 Application+DI+tests ~490 líneas. |
| R-7 | Idioma de mensajes de validador: contrato bosquejó inglés; convención locked del repo es español. | Media | Bajo | §3.2 de la spec sigue precedente F-005 D-ML (mensajes en español); si el usuario prefiere inglés, se corrige en revisión. |
| R-8 | Sustitución del placeholder rompe consumidores ocultos. | Baja | Bajo | Blast radius verificado por grep: solo `ISavedViewRepository` (firmas por nombre, compatibles). |

---

## Notas para apply

- **`SavedView.cs` es reescritura in-place:** reemplazar el `record` placeholder por `public sealed class SavedView` completo. NO mover a `Domain/Catalog/`.
- **Espejar `Tag.cs`/`TagRepository.cs`/`TagConfiguration.cs`:** F-004 entregó el patrón probado; copiar adaptando `SavedView`/`SavedViews`.
- **NO introducir `Entity<Guid>`:** el codebase no tiene base `Entity<TId>`.
- **`SavedView.Update` es obligatorio:** sin él, `UpdateAsync` no tiene camino de mutación público.
- **`SchemaVersion` se estampa internamente = 1:** ningún caller lo suministra (D-3).
- **Envelope JSON `{ schemaVersion, type, data }`:** camelCase, sin indentar, `type` = nombre del tipo CLR.
- **Migración con `--startup-project UI`:** ejecutar el comando exacto de T-003.
- **No seed data:** la migración debe ser DDL puro.
- **Pure xUnit `Assert.*`:** sin FluentAssertions, sin mocking library.
- **No scope creep:** no `ExpenseFilter`/`IncomeFilter` modifications, no `DeleteSavedViewUseCase`, no UI.

---

## Definition of Done (DoD)

- [ ] AC-1: `SavedView.cs` reescrito in-place como `public sealed class SavedView` sin base. (T-001)
- [ ] AC-2: `SavedViewConfiguration.cs` creado con índice único `Name` + índice `EntityType`. (T-002)
- [ ] AC-3: `ISavedViewRepository` extendido con `GetByEntityTypeAsync`. (T-005)
- [ ] AC-4: `SavedViewRepository.cs` creado con 6 métodos espejo `TagRepository`. (T-004)
- [ ] AC-5: Migración `AddSavedViewsTable` generada con `Up`/`Down` correctos. (T-003)
- [ ] AC-6: `AppDbContext.SavedViews` expression-bodied y `DbSet` descubierto. (T-002)
- [ ] AC-7: `ServiceCollectionExtensions` actualizado con `AddScoped<ISavedViewRepository, SavedViewRepository>`. (T-005)
- [x] AC-8: `FilterJsonSerializer.cs` creado con envelope + reglas de versión + overload no-genérico. (T-006)
- [x] AC-9: `SaveCurrentFilterCommand`/`Validator`/`UseCase` creados. (T-007)
- [x] AC-10: `ApplySavedViewQuery`/`Result`/`UseCase` creados. (T-008)
- [x] AC-11: `GetSavedViewsQuery`/`UseCase` creados. (T-009)
- [x] AC-12: `Bootstrap.cs` actualizado con 3 usings + 4 registros Transient. (T-009)
- [x] AC-13: `FakeSavedViewRepository.cs` creado con 6 métodos + overrides. (T-009)
- [x] AC-14: 4 archivos de unit tests creados (≥6 + ≥4 + ≥3 + ≥3). (T-006, T-007, T-008, T-009)
- [x] AC-15: `SavedViewRepositoryTests.cs` creado con ≥6 integration tests. (T-004)
- [x] AC-16: Build limpio, suite verde, NetArchTest R-1..R-5 verdes, format exit 0, sin scope creep. (T-010)

---

## Referencias

- `spec/features/phase-04/F-006-saved-view-filter-json.spec.md` — la spec completa de F-006 P4.
- `.atl/sdd/f-006p4-saved-view-json/proposal.md` — PRD de origen.
- `spec/phases/phase-04/features.md` — 4-006 dentro del desglose de la fase 4.
- `spec/features/phase-04/F-007-extend-filters-tagids.done.md` — filtros 6 props con `TagIds` (dependencia).
- `spec/features/phase-04/F-005-update-delete-use-cases.done.md` — patrón use cases.
- `spec/features/phase-04/F-004-tag-entity-and-many-to-many.done.md` — patrón `Tag`/`TagRepository`/`TagConfiguration`.
- `src/ControlDeFinanzasIA.Domain/Views/SavedView.cs` — placeholder a reescribir.
- `src/ControlDeFinanzasIA.Domain/Catalog/Tag.cs` — plantilla entidad.
- `src/ControlDeFinanzasIA.Infrastructure/Persistence/Repositories/TagRepository.cs` — plantilla repo.
- `src/ControlDeFinanzasIA.Infrastructure/Persistence/Configurations/TagConfiguration.cs` — plantilla config.
- `src/ControlDeFinanzasIA.Application/Abstractions/ExpenseFilter.cs` / `IncomeFilter.cs` — records a serializar.
- `src/ControlDeFinanzasIA.UI/Composition/Bootstrap.cs` — DI de use cases.
- `tests/ControlDeFinanzasIA.Tests.Unit/TestDoubles/FakeIncomeRepository.cs` — plantilla fake.
- Engram objetivo: `sdd/f-006p4-saved-view-json/tasks`.
