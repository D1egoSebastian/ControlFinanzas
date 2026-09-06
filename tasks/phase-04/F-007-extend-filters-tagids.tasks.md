# Tareas — F-007 (Phase 4): Extensión de filtros con `TagIds` + enabler M:N `Income↔Tag` (Opción A)

> **Spec:** `spec/features/phase-04/F-007-extend-filters-tagids.spec.md`
> **change_id:** `f-007p4-extend-filters-tagids`
> **Estado:** Pendiente de implementación
> **Fecha:** 2026-09-03
> **Modo:** Standard (SDD: propose → spec → tasks → apply → verify → archive)
> **Fuentes de verdad:** la spec, `docs/vision.md` §4.2-4.3,
> `spec/constitution/tech-stack.md`, `spec/constitution/mission.md`,
> `spec/phases/phase-04/features.md` (RF-18, RF-20, §4.007),
> `spec/features/phase-04/F-002-income-repository.done.md` (patrón filtro Income + T-12),
> `spec/features/phase-04/F-004-tag-entity-and-many-to-many.done.md` (patrón M:N Expense).
>
> Cada tarea es implementable en menos de 2 horas. La numeración (T-001...) es local a esta feature.
> F-007 modifica `Domain/Transactions/`, `Application/Abstractions/`,
> `Infrastructure/Persistence/` y ambos proyectos de test. NO modifica `Tag*`,
> `Expense.cs`, `ExpenseConfiguration.cs`, use cases, UI, migraciones previas.

---

## Pronóstico de carga de review

| Field | Value |
|-------|-------|
| Estimated changed lines | ~440 |
| 400-line budget risk | Medium |
| Chained PRs recommended | Yes |
| Suggested split | PR 1: T-001..T-003 (enabler Income↔Tag) ~225 líneas; PR 2: T-004..T-007 (filtros + tests + gates) ~215 líneas |
| Delivery strategy | auto-forecast |
| Chain strategy | stacked-to-main |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: Medium

### Unidades de trabajo sugeridas

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | `Income.Tags`, `AddTag`/`RemoveTag`, mapeo M:N `IncomeTags`, migración `AddIncomeTagsJoinTable`, unit + integration tests del enabler | PR 1 | Base = `main`; autónomo, verificable, con rollback propio |
| 2 | `ExpenseFilter.TagIds`, filtros TagIds en `ExpenseRepository` e `IncomeRepository`, T-12 flip, filter integration tests, gates finales | PR 2 | Base = PR 1; depende de `Income.Tags` para filtrar ingresos |

---

## Resumen

F-007 (Phase 4) cierra el **eje de filtrado por tags**: extiende `ExpenseFilter`
de 5 a 6 props, aplica `TagIds` con semántica OR/any-match en `ExpenseRepository`
y `IncomeRepository`, e incluye el **enabler mínimo `Income ↔ Tag`** (colección,
métodos de dominio idempotentes, mapeo M:N y migración aditiva) para que el
filtro de ingresos sea posible. Invierte in-place el test T-12 de
`IncomeRepositoryTests` y cubre todo con tests unitarios e integración pure xUnit
`Assert.*`.

---

## Resumen de tareas

| ID | Tarea | Descripción corta | Estimación diff | Depende de | Verificación |
| --- | --- | --- | --- | --- | --- |
| T-001 | Extender `Income.cs` + `IncomeTests.cs` | `Tags` + `AddTag`/`RemoveTag` idempotentes + init en ambos ctors + 7-8 tests unitarios | +80 | ninguna | build exit 0; tests `IncomeTests` pasan |
| T-002 | Extender `IncomeConfiguration.cs` | Mapeo M:N `IncomeTags` vía `UsingEntity` tras `OwnsOne(Amount)` | +25 | T-001 | build exit 0; modelo EF Core válido |
| T-003 | Generar migración `AddIncomeTagsJoinTable` + `IncomeTagManyToManyTests.cs` | `dotnet ef migrations add`; 4 tests round-trip M:N + cascada | +120 | T-002 | migración generada; M:N tests pasan |
| T-004 | Extender `ExpenseFilter.cs` | Añadir 6ª prop `IReadOnlyList<Guid>? TagIds { get; init; }` + XML doc | +15 | ninguna | build exit 0 |
| T-005 | Extender `ExpenseRepository.cs` + `ExpenseFilterTagTests.cs` | Aplicar filtro TagIds OR + 6 tests de integración | +100 | T-004 | tests `ExpenseFilterTag` pasan |
| T-006 | Extender `IncomeRepository.cs` + `IncomeFilterTagTests.cs` + flip T-12 | Reemplazar marcador ignore por filtro TagIds + 4+2 tests + T-12 rename/flip | +100 | T-003, T-004 | tests `IncomeFilterTag` + `IncomeRepository` pasan |
| T-007 | Verificación final, formato y cierre | Build, tests completos, NetArchTest, format, scope check | 0 | T-001 a T-006 | todos los gates verdes |

**Total estimado:** ~440 líneas cambiadas. T-001, T-003, T-005 y T-006 son las más densas;
T-002 y T-004 son mecánicas; T-007 es verificación y cierre administrativo.

---

## [x] T-001 — Extender `Income.cs` con `Tags` + `AddTag`/`RemoveTag` + extender `IncomeTests.cs`

**AC refs:** AC-1, AC-11

**Pre-condiciones**
- `src/.../Domain/Transactions/Income.cs` verificado con dos constructores privados:
  parameterless (línea 71) y parametrizado (líneas 89-109).
- `src/.../Domain/Transactions/Expense.cs` verificado como patrón espejo de
  `Tags`/`AddTag`/`RemoveTag` (líneas 64, 70-73, 106, 198-230).

**Pasos**

1. Abrir `src/ControlDeFinanzasIA.Domain/Transactions/Income.cs`.
2. Añadir `using ControlDeFinanzasIA.Domain.Catalog;` al tope (referencia intra-Domain, R-1 OK).
3. Declarar `public ICollection<Tag> Tags { get; private set; } = new List<Tag>();`
   (espejo de `Expense.Tags` línea 64).
4. Inicializar `Tags` como `new List<Tag>()` en **ambos** constructores privados (D-8):
   - En el ctor parameterless: `Tags = new List<Tag>();`.
   - En el ctor parametrizado: asignar `Tags = new List<Tag>();` antes de salir.
5. Implementar `public void AddTag(Tag tag)` (D-8):
   - `ArgumentNullException.ThrowIfNull(tag);`
   - Si `string.IsNullOrWhiteSpace(tag.Name)` → `ArgumentException("Tag name cannot be empty.", nameof(tag));`
   - Si `Tags.Any(t => t.Id == tag.Id)` → **return** (no-op idempotente).
   - `Tags.Add(tag);`
6. Implementar `public void RemoveTag(Tag tag)` (D-8):
   - `ArgumentNullException.ThrowIfNull(tag);`
   - `Tag? existing = Tags.FirstOrDefault(t => t.Id == tag.Id);`
   - Si `existing` no es null → `Tags.Remove(existing)`; si es null → no-op.
7. Añadir XML doc en `Tags`, `AddTag` y `RemoveTag` (espejo de `Expense`).
8. Extender `tests/ControlDeFinanzasIA.Tests.Unit/Domain/IncomeTests.cs`:
   - Añadir `using ControlDeFinanzasIA.Domain.Catalog;` al tope.
   - Helper local `CreateTag(string name, string? color = null) => Tag.Create(name, color, _defaultCreatedAt);`.
   - ≥7 tests (espejo de `ExpenseTests.cs` líneas 280-359):
     - `NewIncome_HasEmptyNotNullTagsCollection`
     - `AddTag_WithNewTag_AddsToCollection`
     - `AddTag_WithDuplicateId_IsNoOp`
     - `AddTag_WithNullTag_ThrowsArgumentNullException`
     - `RemoveTag_Existing_RemovesFromCollection`
     - `RemoveTag_NotInCollection_IsNoOp`
     - `RemoveTag_WithNullTag_ThrowsArgumentNullException`
   - Opcional: `AddTag_WithEmptyNameTag_Throws` (guard defensivo).
9. Ejecutar `dotnet build -c Debug --nologo` y
   `dotnet test -c Debug --no-build --nologo --filter "FullyQualifiedName~Tests.Unit.Domain.IncomeTests"`.

**Criterios de verificación**
- `dotnet build` exit 0, 0 warnings, 0 errors.
- `Income.Tags` no es null en una instancia recién creada; `Count == 0`.
- `AddTag`/`RemoveTag` son idempotentes; lanzan excepciones correctas.
- `dotnet test --filter "FullyQualifiedName~Tests.Unit.Domain.IncomeTests"` todos los tests existentes + nuevos pasan.

**Notas**
- D-8 es el riesgo principal: el ctor parameterless de EF Core debe inicializar
  `Tags`; si no, `AddTag` lanzará `NullReferenceException` al materializar.
- Duplicado por `Id` → no-op silencioso; solo `null` lanza excepción.
- El guard defensivo de `AddTag` contra `Name` vacío es defensa en profundidad;
  `Tag.Create` ya rechaza esos valores.

---

## [x] T-002 — Extender `IncomeConfiguration.cs` con mapeo M:N `IncomeTags`

**AC refs:** AC-2

**Pre-condiciones**
- T-001 completada (`Income.Tags` existe).
- `src/.../Infrastructure/Persistence/Configurations/IncomeConfiguration.cs` verificado
  con `OwnsOne(e => e.Amount)` líneas 45-55.
- `src/.../Infrastructure/Persistence/Configurations/ExpenseConfiguration.cs` verificado
  como plantilla M:N líneas 56-77.

**Pasos**

1. Abrir `src/ControlDeFinanzasIA.Infrastructure/Persistence/Configurations/IncomeConfiguration.cs`.
2. Tras el bloque `OwnsOne(e => e.Amount, ...)`, añadir el mapeo M:N (AC-2):
   ```csharp
   // Relación many-to-many entre ingresos y tags mediante la tabla
   // join IncomeTags. La PK es compuesta (IncomeId, TagId) y ambas
   // FKs tienen Cascade para mantener la integridad al borrar un
   // ingreso o un tag.
   b.HasMany(e => e.Tags)
       .WithMany()
       .UsingEntity<Dictionary<string, object>>(
           "IncomeTags",
           right => right.HasOne<Tag>()
               .WithMany()
               .HasForeignKey("TagId")
               .OnDelete(DeleteBehavior.Cascade),
           left => left.HasOne<Income>()
               .WithMany()
               .HasForeignKey("IncomeId")
               .OnDelete(DeleteBehavior.Cascade),
           join =>
           {
               join.HasKey("IncomeId", "TagId");
               join.HasIndex("TagId");
               join.ToTable("IncomeTags");
           });
   ```
3. Confirmar que `using ControlDeFinanzasIA.Domain.Catalog;` ya está presente (línea 1 por `HasOne<Category>()`).
4. Ejecutar `dotnet build -c Debug --nologo`.

**Criterios de verificación**
- `dotnet build` exit 0, 0 warnings, 0 errors.
- `IncomeConfiguration` contiene el bloque `UsingEntity<Dictionary<string, object>>`
  con PK compuesta `(IncomeId, TagId)`, FKs `Cascade` e índice en `TagId`.
- El bloque `OwnsOne(e => e.Amount)` y todo lo existente queda intacto.

**Notas**
- El bloque M:N se **añade después** del `OwnsOne`; cero líneas existentes editadas.
- `Tag` no tiene navegación inversa hacia `Income` (`.WithMany()` vacío) — es catálogo puro.
- No tocar `OnModelCreating`; la configuración se descubre por `ApplyConfigurationsFromAssembly`.

---

## [x] T-003 — Generar migración `AddIncomeTagsJoinTable` + crear `IncomeTagManyToManyTests.cs`

**AC refs:** AC-3, AC-10

**Pre-condiciones**
- T-002 completada (mapeo M:N en `IncomeConfiguration`).
- `dotnet-ef` 8.0.11 disponible localmente.
- `ExpenseTagManyToManyTests.cs` verificado como patrón espejo (179 líneas).

**Pasos**

1. Ejecutar:
   ```powershell
   dotnet ef migrations add AddIncomeTagsJoinTable `
     --project src/ControlDeFinanzasIA.Infrastructure `
     --startup-project src/ControlDeFinanzasIA.UI
   ```
2. Verificar que se crearon 3 archivos en `src/.../Infrastructure/Persistence/Migrations/`:
   - `<timestamp>_AddIncomeTagsJoinTable.cs`
   - `<timestamp>_AddIncomeTagsJoinTable.Designer.cs`
   - `AppDbContextModelSnapshot.cs` (actualizado)
3. Inspeccionar `<timestamp>_AddIncomeTagsJoinTable.cs`:
   - `Up` crea `IncomeTags` (`IncomeId`, `TagId`) + PK compuesta +
     FKs `Cascade` + `IX_IncomeTags_TagId`.
   - `Up` no contiene `InsertData` (no seed).
   - `Up` no toca `Tags`/`Expenses`/`ExpenseTags`/`Incomes`/`Categories`/`PaymentMethods`.
   - `Down`: `DropTable("IncomeTags")` (una sola tabla).
4. Si EF Core generó namespace block-scoped, corregir a file-scoped (IDE0161)
   como en F-001 P4 C-5 / F-004 AC-8.
5. Crear `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/IncomeTagManyToManyTests.cs`:
   - file-scoped namespace `ControlDeFinanzasIA.Tests.Integration.Persistence`.
   - Campos estáticos:
     - `_fixedTimestamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)`.
     - `_comidaCategoryId = new Guid("11111111-1111-1111-1111-000000000001")`.
   - Helper `CreateTag(name, color?)`.
   - Helper `CreateIncome(...)` usando `Income.Create(_comidaCategoryId, date,
     Money.Create(amount, Currency.DOP), description, null, null, _fixedTimestamp)`.
   - Implementar 4 tests (espejo de `ExpenseTagManyToManyTests`):
     - `AddIncomeWithTags_PersistsBothAndJoinRows`
     - `ReloadIncomeWithTags_PopulatesTagsFromJoinTable`
     - `DeleteIncome_CascadesJoinRows`
     - `DeleteTag_CascadesJoinRows`
   - Usar `SqlQueryRaw<IncomeTagRow>("SELECT IncomeId, TagId FROM IncomeTags WHERE IncomeId = {0}", ...)`.
   - Usar `TagRepository.RemoveAsync(tagA.Id)` para la cascada del tag.
6. Ejecutar `dotnet build -c Debug --nologo` y
   `dotnet test -c Debug --no-build --nologo --filter "FullyQualifiedName~IncomeTagManyToMany"`.

**Criterios de verificación**
- `dotnet build` exit 0, 0 warnings, 0 errors.
- Migración generada con `Up`/`Down` correctos; file-scoped namespace.
- `dotnet test --filter "FullyQualifiedName~IncomeTagManyToMany"` 4 passed, 0 failed.

**Notas**
- D-6: UNA sola migración `AddIncomeTagsJoinTable` (no dos). La tabla `Tags` ya existe.
- `--startup-project` debe ser `src/ControlDeFinanzasIA.UI` (convención del repo).
- Sembrar tags primero vía `context.Tags.AddRange(...)` + `SaveChangesAsync`, luego
  construir income, taggear y persistir.

---

## [x] T-004 — Extender `ExpenseFilter.cs` con `TagIds`

**AC refs:** AC-4

**Pre-condiciones**
- `src/.../Application/Abstractions/ExpenseFilter.cs` verificado como record
  `sealed` tonto de 5 props `init` (42 líneas), sin `TagIds`.
- `src/.../Application/Abstractions/IncomeFilter.cs` verificado como patrón espejo
  de la prop `TagIds` (líneas 48-51).

**Pasos**

1. Abrir `src/ControlDeFinanzasIA.Application/Abstractions/ExpenseFilter.cs`.
2. Añadir como 6ª propiedad (después de `DateTo`):
   ```csharp
   /// <summary>
   /// Identificadores de tags para filtrar (semántica OR: la entidad
   /// debe tener al menos uno). <c>null</c> o lista vacía significan
   /// que no se aplica filtro por tags.
   /// </summary>
   public IReadOnlyList<Guid>? TagIds { get; init; }
   ```
3. Actualizar `<remarks>` del tipo para mencionar que `TagIds` tampoco se valida
   (la validación es del caller, 4-008).
4. Preservar file-scoped namespace `ControlDeFinanzasIA.Application.Abstractions`.
5. Ejecutar `dotnet build -c Debug --nologo`.

**Criterios de verificación**
- `dotnet build` exit 0, 0 warnings, 0 errors.
- `ExpenseFilter` tiene 6 props `init`: `Page`, `PageSize`, `CategoryId`, `DateFrom`, `DateTo`, `TagIds`.
- `TagIds` default `null` = sin filtro; callers existentes intactos.

**Notas**
- Record "tonto": sin validación de `TagIds` (p. ej. Guids no vacíos) — eso es del caller/use case 4-008.
- No usar notación posicional: se necesita XML doc por propiedad.

---

## [x] T-005 — Extender `ExpenseRepository.GetByFilterAsync` + crear `ExpenseFilterTagTests.cs`

**AC refs:** AC-5, AC-8

**Pre-condiciones**
- T-004 completada (`ExpenseFilter.TagIds` existe).
- `src/.../Infrastructure/Persistence/Repositories/ExpenseRepository.cs` verificado
  con `GetByFilterAsync` líneas 178-211.
- `ExpenseTagManyToManyTests.cs` verificado como patrón de sembrado con tags.

**Pasos**

1. Abrir `src/.../Infrastructure/Persistence/Repositories/ExpenseRepository.cs`.
2. En `GetByFilterAsync`, después del bloque `if (filter.DateTo is not null)` y
   antes del orden, añadir (D-5):
   ```csharp
   if (filter.TagIds is { Count: > 0 })
   {
       query = query.Where(e => e.Tags.Any(t => filter.TagIds!.Contains(t.Id)));
   }
   ```
3. Actualizar XML doc del método para mencionar `TagIds` OR/any-match.
4. El resto del método (orden `Date desc, CreatedAt desc`, `CountAsync`,
   `Skip`/`Take`, `PagedResult`) queda sin cambios.
5. Crear `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/ExpenseFilterTagTests.cs`:
   - file-scoped namespace `ControlDeFinanzasIA.Tests.Integration.Persistence`.
   - Fixture `using var factory = new TestAppDbContextFactory(); using AppDbContext context = factory.CreateContext(); await context.Database.MigrateAsync();`.
   - Helpers `CreateTag` + `CreateExpense` (espejo de `ExpenseTagManyToManyTests`).
   - 6 tests:
     - `GetByFilterAsync_WithSingleTagId_ReturnsOnlyMatching`
     - `GetByFilterAsync_WithMultipleTagIds_ReturnsExpensesWithAny` (OR/any-match)
     - `GetByFilterAsync_WithEmptyTagIds_ReturnsAll`
     - `GetByFilterAsync_WithNullTagIds_ReturnsAll`
     - `GetByFilterAsync_WithNonExistentTagId_ReturnsEmpty`
     - `GetByFilterAsync_WithCombinedCategoryAndTagIds_ReturnsIntersection`
6. Ejecutar `dotnet build -c Debug --nologo` y
   `dotnet test -c Debug --no-build --nologo --filter "FullyQualifiedName~ExpenseFilterTag"`.

**Criterios de verificación**
- `dotnet build` exit 0, 0 warnings, 0 errors.
- `ExpenseRepository.GetByFilterAsync` aplica `TagIds` con semántica OR.
- `CategoryId`/`DateFrom`/`DateTo` existentes siguen verdes.
- `dotnet test --filter "FullyQualifiedName~ExpenseFilterTag"` 6 passed, 0 failed.

**Notas**
- D-5: el null-forgiving `filter.TagIds!` es requerido para evitar warning CS8602.
- D-1: semántica OR/any-match — `Total == 2` cuando `TagIds = [tagA, tagB]` y E1/E2 tienen uno cada uno.
- D-2: `null`/vacía = sin filtro; tag inexistente = `Total == 0`, no error.

---

## [x] T-006 — Extender `IncomeRepository.GetByFilterAsync` + crear `IncomeFilterTagTests.cs` + flip T-12

**AC refs:** AC-6, AC-7, AC-9

**Pre-condiciones**
- T-003 completada (`Income.Tags` + `IncomeTags` + migración aplicable).
- T-004 completada (`IncomeFilter.TagIds` ya existía como placeholder).
- `src/.../Infrastructure/Persistence/Repositories/IncomeRepository.cs` verificado
  con marcador `_ = filter.TagIds;` líneas 207-208 y remarks líneas 175-178.
- `tests/.../Tests.Integration/Persistence/IncomeRepositoryTests.cs` verificado
  con T-12 `GetByFilterAsync_IgnoresTagIds_ForNow` líneas 301-322.

**Pasos**

1. Abrir `src/.../Infrastructure/Persistence/Repositories/IncomeRepository.cs`.
2. Reemplazar el marcador (líneas 207-208):
   ```csharp
   // TagIds is intentionally ignored (added in 4-007).
   _ = filter.TagIds;
   ```
   por (D-5):
   ```csharp
   if (filter.TagIds is { Count: > 0 })
   {
       query = query.Where(i => i.Tags.Any(t => filter.TagIds!.Contains(t.Id)));
   }
   ```
3. Refrescar el `<remarks>` del método: quitar "se ignora intencionalmente en F-002"
   y poner "aplica `TagIds` con semántica OR/any-match (F-007)".
4. El resto del método queda sin cambios.
5. Refrescar `<remarks>` de `src/.../Application/Abstractions/IncomeFilter.cs`:
   quitar el `<para>` "ignorado por el repositorio hasta 4-007".
6. Abrir `tests/.../Tests.Integration/Persistence/IncomeRepositoryTests.cs`.
7. Renombrar T-12 `GetByFilterAsync_IgnoresTagIds_ForNow` →
   `GetByFilterAsync_WithTagIds_FiltersCorrectly` (misma posición, 12º test).
8. Cambiar el cuerpo de T-12 (AC-7):
   - Sembrar `tagA` vía `context.Tags.Add(tagA); await context.SaveChangesAsync();`.
   - Crear `income1` con `AddTag(tagA)` e `income2` sin tags; persistir vía `repo.AddAsync`.
   - Llamar `repo.GetByFilterAsync(new IncomeFilter { TagIds = [tagA.Id] })`.
   - Assert: `Total == 1` + `Items.Single().Id == income1.Id`.
9. Refrescar remarks de la clase (línea 19): "y el filtrado por `TagIds`" en lugar de
   "placeholder `TagIds`".
10. Crear `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/IncomeFilterTagTests.cs`:
    - file-scoped namespace; helpers `CreateTag` + `CreateIncome`.
    - 4 tests requeridos + 2 opcionales:
      - `GetByFilterAsync_WithTagIds_FiltersCorrectly`
      - `GetByFilterAsync_WithMultipleTagIds_ReturnsIncomesWithAny`
      - `GetByFilterAsync_WithEmptyTagIds_ReturnsAll`
      - `GetByFilterAsync_WithCombinedCategoryAndTagIds_ReturnsIntersection`
      - (opcional) `GetByFilterAsync_WithNullTagIds_ReturnsAll`
      - (opcional) `GetByFilterAsync_WithNonExistentTagId_ReturnsEmpty`
11. Ejecutar `dotnet build -c Debug --nologo` y
    `dotnet test -c Debug --no-build --nologo --filter "FullyQualifiedName~IncomeRepository|FullyQualifiedName~IncomeFilterTag"`.

**Criterios de verificación**
- `dotnet build` exit 0, 0 warnings, 0 errors.
- `IncomeRepository.GetByFilterAsync` aplica `TagIds` con semántica OR.
- T-12 renombrado + contrato invertido; sigue siendo el 12º test del archivo.
- `dotnet test --filter "FullyQualifiedName~IncomeRepository|FullyQualifiedName~IncomeFilterTag"` todos pasan.

**Notas**
- D-7: T-12 se **actualiza in-place**, NO se elimina. Preserva trazabilidad del contrato F-002 D-2.
- El test `GetByFilterAsync_WithTagIds_FiltersCorrectly` en `IncomeFilterTagTests`
  puede tener el mismo nombre que T-12 renombrado (clases distintas).
- Requiere `Income.Tags` + `IncomeTags` para que la query traduzca (AC-6).

---

## [x] T-007 — Verificación final, formato y cierre

**AC refs:** AC-12, AC-13, AC-14, AC-15

**Pre-condiciones**
- T-001 a T-006 completadas.

**Pasos**

1. Ejecutar `dotnet build -c Release --nologo` → 0 warnings, 0 errors.
2. Ejecutar `dotnet build -c Debug --nologo` → 0 warnings, 0 errors.
3. Ejecutar `dotnet test -c Debug --no-build --nologo --filter "FullyQualifiedName~FilterTag|FullyQualifiedName~IncomeTagManyToMany|FullyQualifiedName~IncomeRepository|FullyQualifiedName~ExpenseRepository"` → todos pasan.
4. Ejecutar `dotnet test -c Debug --no-build --nologo --filter "FullyQualifiedName~ArchitectureTests"` → 11/11 passed.
5. Ejecutar `dotnet test -c Debug --no-build --nologo` (suite completa Debug) → verde excepto el 1 fallo pre-existing conocido de `Money`.
6. Ejecutar `dotnet format style --verify-no-changes` → exit 0.
7. Ejecutar `dotnet list src/ControlDeFinanzasIA.Domain package` → vacío (R-1).
8. Verificar scope (AC-14):
   - No modificaciones a `Tag.cs`, `TagConfiguration.cs`, `TagRepository.cs`, `ITagRepository.cs`.
   - No modificaciones a `Expense.cs`, `ExpenseConfiguration.cs`.
   - No modificaciones a use cases (`Register*`, `Update*`, `Delete*`).
   - No UI / ViewModels / XAML.
   - No `Money`/`Currency`/`Result`/`PagedResult`.
   - Solo migración nueva: `AddIncomeTagsJoinTable`; migraciones previas intactas.
9. Verificar doc-comments: ningún comentario menciona "hasta 4-007" como futuro.
10. Actualizar este archivo de tareas marcando todas las casillas `[x]`.
11. Reportar conteos exactos de tests observados.

**Criterios de verificación**
- `dotnet build -c Release` exit 0, 0 warnings, 0 errors.
- `dotnet build -c Debug` exit 0, 0 warnings, 0 errors.
- `dotnet test -c Debug --no-build` suite verde (baseline + ~22-24 nuevos; 1 fallo pre-existing de Money).
- `dotnet test --filter "FullyQualifiedName~ArchitectureTests"` 11/11 passed.
- `dotnet format style --verify-no-changes` exit 0.
- `dotnet list src/ControlDeFinanzasIA.Domain package` vacío.
- Sin archivos fuera de la lista in-scope modificados.

**Notas**
- No renombrar la spec a `.done.md`; eso corresponde a `sdd-archive` de la fase.
- Si `features.md` necesita actualización, marcar 4-007 como completada solo si
  todos los gates pasan.

---

## Orden de ejecución recomendado

**Orden secuencial con dependencias explícitas:**

1. **T-001** debe ejecutarse primero (el resto del enabler necesita `Income.Tags`).
2. **T-002** depende de T-001 (`IncomeConfiguration` mapea `Income.Tags`).
3. **T-003** depende de T-002 (EF Core necesita la configuración para generar la migración).
4. **T-004** puede ejecutarse en paralelo con T-001..T-003 (no depende del enabler Income↔Tag).
5. **T-005** depende de T-004 (`ExpenseFilter.TagIds` + `Expense.Tags` ya existen desde F-004).
6. **T-006** depende de T-003 y T-004 (`Income.Tags` + `IncomeFilter.TagIds`).
7. **T-007** cierra la feature.

```text
T-001 → T-002 → T-003 ─┬─ T-005 ─┐
                        │         ├─ T-007
T-004 ──────────────────┴─ T-006 ─┘
```

T-004 y T-005 pueden trabajarse en paralelo con el enabler (T-001..T-003), pero
T-006 requiere que ambos lados estén listos.

---

## Tareas bloqueantes / habilitadoras

| Tarea | Habilita | Razón |
| --- | --- | --- |
| T-001 | T-002, T-003 | `Income.Tags` debe existir para el mapeo M:N y sus tests. |
| T-002 | T-003 | EF Core necesita descubrir el mapeo M:N para generar la migración. |
| T-003 | T-006 | `IncomeRepository` filtra por `Tags`; los schema/M:N tests aplican la migración. |
| T-004 | T-005, T-006 | `ExpenseFilter.TagIds` debe existir para aplicar el filtro. |
| T-005 | T-007 | Todos los tests deben pasar antes del cierre. |
| T-006 | T-007 | Todos los tests deben pasar antes del cierre. |

---

## Estimación total

| Tarea | Dificultad | Horas estimadas |
| --- | --- | --- |
| T-001 | M | 1.5 |
| T-002 | S | 0.5 |
| T-003 | M | 1.5 |
| T-004 | XS | 0.25 |
| T-005 | M | 1.5 |
| T-006 | M | 1.5 |
| T-007 | S | 0.5 |
| **Total** | | **~7.25 horas (~1 jornada)** |

---

## Riesgos y mitigaciones

| Riesgo | Probabilidad | Impacto | Mitigación |
| --- | --- | --- | --- |
| D-0 Option A enabler omitido o diferido → imposible filtrar `Income` por tags | Resuelto | Alto | Locked decision D-0; T-001..T-003 son obligatorias y van primero. |
| `Income.Tags` no se inicializa en el ctor sin params → NRE al materializar | Media | Alto | AC-1 exige init en **ambos** ctors (espejo `Expense`); IT-1 (`NewIncome_HasEmptyNotNullTagsCollection`) blinda; MT-2 prueba end-to-end. |
| Traducción LINQ de `Tags.Any` + `Contains` sobre SQLite falla | Media | Alto | D-5 con patrón plano; tests FT-1..FT-6, IF-1..IF-4, T-12 con combinaciones; índices `IX_ExpenseTags_TagId` (existente) e `IX_IncomeTags_TagId` (nuevo). |
| Migración genera FKs `Restrict` en lugar de `Cascade` | Media | Alto | AC-2 fuerza `.OnDelete(DeleteBehavior.Cascade)` en ambos lados; AC-3 inspecciona DDL; MT-3/MT-4 verifican cascada real. |
| T-12 olvidada → suite rota | Media | Medio | AC-7 explícito; rename + flip in-place. El gate `~IncomeRepository` la captura inmediatamente. |
| EF emite namespace block-scoped en migración → IDE0161 | Media | Bajo | AC-3 exige file-scoped; gate `dotnet format` en T-007. |
| Bloque M:N en `IncomeConfiguration` rompe `OwnsOne(Money)` | Baja | Alto | AC-2: se añade después del `OwnsOne`, cero líneas existentes editadas; `IncomeSchemaTests` existentes son red de seguridad. |
| Warning CS8602 por `filter.TagIds.Contains` sin `!` | Media | Bajo | D-5 documenta null-forgiving `filter.TagIds!` requerido; gates de build en T-007. |
| Scope creep: tocar `Tag*`, `Expense.cs`, use cases o UI | Baja | Alto | AC-14 prohíbe explícitamente; revisar diff en T-007. |

---

## Notas para apply

- **`Income.Tags` en AMBOS ctors privados (D-8):** olvidar el ctor parameterless
  es el bug clásico de navegaciones de colección en EF Core.
- **Espejar `Expense.cs`/`ExpenseConfiguration.cs`/`ExpenseTagManyToManyTests.cs`:**
  F-004 entregó el patrón probado; copiar adaptando `Income`/`IncomeTags`.
- **M:N `UsingEntity<Dictionary<string,object>>`:** PK compuesta `(IncomeId, TagId)`,
  ambas FKs `Cascade`, índice en `TagId`. Sin `DbSet` para el join.
- **Migración con `--startup-project UI`:** ejecutar el comando exacto de T-003.
- **No seed data:** la migración debe ser DDL puro.
- **`ExpenseFilter.TagIds` default `null`:** callers existentes (`GetTransactionsQuery`, fakes)
  quedan intactos.
- **Pure xUnit `Assert.*`:** sin FluentAssertions, sin mocking library.
- **No scope creep:** no `Tag*` modifications, no `Expense.cs`/`ExpenseConfiguration.cs`,
  no use cases, no UI, no `SavedView`.
- **T-12 update in-place (D-7):** no eliminar; renombrar + invertir contrato.
- **Null-forgiving en repo:** `filter.TagIds!` dentro del lambda para evitar CS8602.

---

## Definition of Done (DoD)

- [ ] AC-1: `Income.Tags` + `AddTag`/`RemoveTag` + init en ambos ctors. (T-001)
- [ ] AC-2: Mapeo M:N `IncomeTags` en `IncomeConfiguration.cs`. (T-002)
- [ ] AC-3: Migración `AddIncomeTagsJoinTable` generada con `Up`/`Down` correctos. (T-003)
- [x] AC-4: `ExpenseFilter` extendida a 6 props con `TagIds`. (T-004)
- [x] AC-5: `ExpenseRepository.GetByFilterAsync` aplica `TagIds` OR. (T-005)
- [x] AC-6: `IncomeRepository.GetByFilterAsync` aplica `TagIds` OR. (T-006)
- [x] AC-7: T-12 renombrada + contrato invertido en `IncomeRepositoryTests.cs`. (T-006)
- [x] AC-8: `ExpenseFilterTagTests.cs` con 6 tests verdes. (T-005)
- [x] AC-9: `IncomeFilterTagTests.cs` con 4+ tests verdes. (T-006)
- [x] AC-10: `IncomeTagManyToManyTests.cs` con 4 tests verdes. (T-003)
- [x] AC-11: `IncomeTests.cs` extendido con ≥7 tests `AddTag`/`RemoveTag`. (T-001)
- [x] AC-12: Migración aplica limpiamente; `MigrateAsync` funciona. (T-003, T-007)
- [x] AC-13: Build limpio, suite verde, NetArchTest R-1..R-5 verdes, format exit 0. (T-007)
- [x] AC-14: Sin scope creep. (T-007)
- [x] AC-15: NetArchTest R-1..R-5 preservados. (T-007)
- [x] AC-16: Estrategia de PRs encadenados (stacked-to-main) documentada. (encabezado)

---

## Referencias

- `spec/features/phase-04/F-007-extend-filters-tagids.spec.md` — la spec completa de F-007 P4.
- `.atl/sdd/f-007p4-extend-filters-tagids/proposal.md` — PRD de origen.
- `spec/phases/phase-04/features.md` — 4-007 dentro del desglose de la fase 4.
- `src/ControlDeFinanzasIA.Domain/Transactions/Income.cs` — entidad a extender.
- `src/ControlDeFinanzasIA.Domain/Transactions/Expense.cs` — patrón `Tags`/`AddTag`/`RemoveTag`.
- `src/ControlDeFinanzasIA.Application/Abstractions/ExpenseFilter.cs` — record a extender.
- `src/ControlDeFinanzasIA.Application/Abstractions/IncomeFilter.cs` — patrón `TagIds`.
- `src/ControlDeFinanzasIA.Infrastructure/Persistence/Configurations/IncomeConfiguration.cs` — config a extender.
- `src/ControlDeFinanzasIA.Infrastructure/Persistence/Configurations/ExpenseConfiguration.cs` — plantilla M:N.
- `src/ControlDeFinanzasIA.Infrastructure/Persistence/Repositories/ExpenseRepository.cs` — repo a extender.
- `src/ControlDeFinanzasIA.Infrastructure/Persistence/Repositories/IncomeRepository.cs` — repo a extender.
- `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/ExpenseTagManyToManyTests.cs` — plantilla M:N tests.
- `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/IncomeRepositoryTests.cs` — T-12 a flipear.
- `tests/ControlDeFinanzasIA.Tests.Unit/Domain/IncomeTests.cs` — archivo a extender.
- `tests/ControlDeFinanzasIA.Tests.Unit/Domain/ExpenseTests.cs` — plantilla unit tests Tags.
- Engram objetivo: `sdd/f-007p4-extend-filters-tagids/tasks`.
