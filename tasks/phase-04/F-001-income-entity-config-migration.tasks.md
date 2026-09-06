# Tareas — F-001 (Phase 4): Entidad `Income` + `IncomeConfiguration` + migración `AddIncomesTable`

> **Spec:** `spec/features/phase-04/F-001-income-entity-config-migration.spec.md`
> **Fuentes de verdad:** la spec, `docs/vision.md` §4.2, `docs/architecture.md` §6, §8,
> `docs/roadmap.md` §Fase 4, `spec/constitution/tech-stack.md` (.NET 8, EF Core 8, SQLite),
> `spec/constitution/mission.md` (dominio puro, sin duplicación),
> `spec/phases/phase-04/features.md` (4-001 dentro del desglose de la fase 4),
> `spec/features/phase-03/F-001-expense-domain-entity.done.md` (patrón `Expense` entity a espejar),
> `spec/features/phase-03/F-002-expense-configuration-migration.done.md`
> (patrón `ExpenseConfiguration` + `AddExpenseTable` + `OwnsOne` de `Money`),
> `spec/features/phase-02/F-006-integration-tests.done.md`
> (`TestAppDbContextFactory` reutilizada en schema tests).
>
> Cada tarea es implementable en menos de 2 horas. El proyecto debe seguir
> compilando al finalizar cada una. La numeración (T-001...) es local a esta feature.
> F-001 modifica `Domain/Transactions/`, `Infrastructure/Persistence/` y ambos
> proyectos de test. NO modifica `Application/`, `UI/`, `Directory.Packages.props`
> ni repositorios existentes.

---

## Pronóstico de carga de review

| Field | Value |
|-------|-------|
| Estimated changed lines | ~300 |
| 400-line budget risk | Low |
| Chained PRs recommended | No |
| Suggested split | Single PR |
| Delivery strategy | auto-forecast |
| Chain strategy | stacked-to-main |

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: stacked-to-main
400-line budget risk: Low

### Unidades de trabajo sugeridas

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Entidad `Income`, configuración EF Core, migración `AddIncomesTable`, unit tests y schema tests | PR 1 | Base = `main`; incluye build, tests, formato y NetArchTest |

---

## Resumen

F-001 (Phase 4) entrega el **lado simétrico de los gastos**: la entidad `Income`,
su mapeo EF Core y la migración que crea la tabla `Incomes`. Reescribe in-place
el placeholder `src/.../Domain/Transactions/Income.cs` como `public sealed class
Income` con factory `Create`, método `Update`, 9 propiedades con `private set` y
8 invariantes. Crea `IncomeConfiguration` como espejo de `ExpenseConfiguration`,
divergiendo solo en `CategoryId` nullable con `OnDelete(DeleteBehavior.SetNull)`
y en el campo nuevo `Source: string?` (max 50). Genera la migración aditiva
`AddIncomesTable` (sin seed data) y la cubre con unit tests puros de xUnit y 3
schema tests contra SQLite in-memory. No introduce repositorio, use cases ni UI:
es el nodo inicial que habilita 4-002, 4-003 y 4-005.

---

## Resumen de tareas

| ID | Tarea | Descripción corta | Estimación diff | Depende de | Verificación |
| --- | --- | --- | --- | --- | --- |
| T-001 | Reescribir `Income.cs` in-place | Placeholder vacío → entidad `Income` con `Create`, `Update`, 9 props, 8 invariantes | +70 / -5 | ninguna | `dotnet build` exit 0 | [x]
| T-002 | Crear `IncomeConfiguration.cs` | Mapeo Fluent API de `Income` a tabla `Incomes` con FK nullable `SetNull`, índices y `OwnsOne` | +55 / -0 | ninguna | `dotnet build` exit 0 | [x]
| T-003 | Añadir `DbSet<Income>` a `AppDbContext` | Expression-bodied `public DbSet<Income> Incomes => Set<Income>();` tras `Expenses` | +5 / -0 | ninguna | `dotnet build` exit 0 | [x]
| T-004 | Generar migración `AddIncomesTable` | `dotnet ef migrations add` con `--startup-project UI`; verificar `Up`, `Down` y 3 archivos | +90 / -0 | T-002, T-003 | migración generada, build limpio | [x]
| T-005 | Crear `IncomeTests.cs` | ≥8 unit tests pure xUnit `Assert.*` cubriendo happy path + cada invariante + update | +120 / -0 | T-001 | `dotnet test --filter "FullyQualifiedName~IncomeTests"` ≥8 passed | [x]
| T-006 | Crear `IncomeSchemaTests.cs` | 3 schema tests: tabla existe, columnas/constraint esperados, round-trip de persistencia | +90 / -0 | T-001, T-004 | `dotnet test --filter "FullyQualifiedName~IncomeSchemaTests"` 3 passed | [x]
| T-007 | Verificación final, formato y cierre | Build, tests completos, NetArchTest, format, actualizar `features.md` (sin renombrar spec) | +2 / -2 | T-001 a T-006 | `dotnet test` ≈230 passed; `dotnet format style --verify-no-changes` exit 0 | [x]

**Total estimado:** ~media jornada. T-001 (entidad) y T-005/T-006 (tests) son las
más densas; T-002, T-003 y T-004 son mecánicas; T-007 es verificación y cierre
administrativo.

---

## T-001 — Reescribir `Income.cs` in-place

**Objetivo**
Reemplazar el placeholder vacío de `src/.../Domain/Transactions/Income.cs` por
`public sealed class Income` completo, siguiendo el patrón archivado de
`Expense` (F-001 P3) con las dos divergencias deliberadas: `CategoryId` nullable
(`Guid?`) y campo `Source: string?`.

**Pasos**

1. Abrir `src/ControlDeFinanzasIA.Domain/Transactions/Income.cs` (placeholder vacío).
2. Mantener file-scoped namespace `ControlDeFinanzasIA.Domain.Transactions`.
3. Añadir `using ControlDeFinanzasIA.Domain.ValueObjects;` para `Money`.
4. Declarar `public sealed class Income` **sin base** (sin `Entity<Guid>` — ver §17 C-1).
5. Declarar las 9 propiedades con `private set`:
   - `Guid Id`
   - `Guid? CategoryId`
   - `DateOnly Date`
   - `Money Amount`
   - `string Description`
   - `string? Note`
   - `string? Source`
   - `DateTime CreatedAt`
   - `DateTime UpdatedAt`
6. Añadir dos constructores privados: parameterless para EF Core y parametrizado para el factory.
7. Implementar `public static Income Create(Guid? categoryId, DateOnly date, Money amount, string description, string? note, string? source, DateTime createdAt)`:
   - Generar `Id = Guid.NewGuid()`.
   - Llamar `ValidateAndNormalize(...)` para validar INV-1..INV-7 y normalizar `Description`, `Note`, `Source`.
   - Setear `UpdatedAt = createdAt`.
   - Retornar instancia construida con el ctor parametrizado.
8. Implementar `public void Update(Guid? categoryId, DateOnly date, Money amount, string description, string? note, string? source, DateTime updatedAt)`:
   - Validar `updatedAt >= CreatedAt` (INV-8).
   - Reasignar las 7 propiedades mutables (`CategoryId`, `Date`, `Amount`, `Description`, `Note`, `Source`, `UpdatedAt`) con `ValidateAndNormalize`.
   - No modificar `Id` ni `CreatedAt`.
9. Implementar `private static (string description, string? note, string? source) ValidateAndNormalize(...)` que enfoque INV-1..INV-7:
   - INV-1: `amount` no null → `ArgumentNullException`.
   - INV-2: `amount.Amount > 0` → `ArgumentException`.
   - INV-3: `date >= new DateOnly(1900, 1, 1)` → `ArgumentException`.
   - INV-4: `description` trim no vacío, ≤ 200 → `ArgumentException`.
   - INV-5: `note` trim; si blank → `null`; si no, ≤ 500 → `ArgumentException`.
   - INV-6: `source` trim; si blank → `null`; si no, ≤ 50 → `ArgumentException`.
   - INV-7: `categoryId` es `null` o `!= Guid.Empty` → `ArgumentException` si es `Guid.Empty`.
10. Ejecutar `dotnet build`.

**Verificación**

- `dotnet build "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --nologo`:
  exit 0, 0 warnings, 0 errors.
- Inspección: `public sealed class Income` sin herencia, 9 props `private set`,
  dos ctores privados, `Create` y `Update` públicos, `ValidateAndNormalize` privado.
- `IIncomeRepository.cs` sigue compilando (mismo namespace `Domain.Transactions`).

**Dependencias**
- ninguna.

**Estimación**
- S (1 hora). Patrón conocido de `Expense`; riesgo bajo.

**Trazabilidad**
- AC-1 (`Income.cs` reescrito in-place).
- AC-2 (`public sealed class Income` sin base, 9 props).
- AC-3 (8 invariantes).
- AC-4 (`Create`).
- AC-5 (`Update`).
- RF-1, RF-2, RF-3, RF-4, RF-5.
- INV-1..INV-8.
- DP-1, DP-2, DP-3, DP-4, DP-7.

---

## T-002 — Crear `IncomeConfiguration.cs`

**Objetivo**
Crear la configuración Fluent API `IncomeConfiguration` que mapea `Income` a la
tabla `Incomes`, replica `ExpenseConfiguration` y aplica las dos divergencias:
`CategoryId` nullable con `OnDelete(DeleteBehavior.SetNull)` y `Source` max 50.

**Pasos**

1. Crear `src/ControlDeFinanzasIA.Infrastructure/Persistence/Configurations/IncomeConfiguration.cs`.
2. Usar file-scoped namespace `ControlDeFinanzasIA.Infrastructure.Persistence.Configurations`.
3. Añadir `using Microsoft.EntityFrameworkCore;` y `using Microsoft.EntityFrameworkCore.Metadata.Builders;`.
4. Añadir `using ControlDeFinanzasIA.Domain.Catalog;` para `HasOne<Category>()`.
5. Añadir `using ControlDeFinanzasIA.Domain.Transactions;` para `Income`.
6. Declarar `public sealed class IncomeConfiguration : IEntityTypeConfiguration<Income>`.
7. En `Configure(EntityTypeBuilder<Income> b)`:
   - `b.ToTable("Incomes")`.
   - `b.HasKey(e => e.Id)`.
   - `b.Property(e => e.Id).ValueGeneratedNever()`.
   - `b.Property(e => e.CategoryId)` (sin `IsRequired()` → nullable).
   - `b.HasOne<Category>().WithMany().HasForeignKey(e => e.CategoryId).OnDelete(DeleteBehavior.SetNull)`.
   - `b.Property(e => e.Date).IsRequired()`.
   - `b.Property(e => e.Description).IsRequired().HasMaxLength(200)`.
   - `b.Property(e => e.Note).HasMaxLength(500)`.
   - `b.Property(e => e.Source).HasMaxLength(50)`.
   - `b.Property(e => e.CreatedAt).IsRequired()`.
   - `b.Property(e => e.UpdatedAt).IsRequired()`.
   - `b.HasIndex(e => e.Date)`.
   - `b.HasIndex(e => e.CategoryId)`.
   - `b.OwnsOne(e => e.Amount, money => { money.Property(m => m.Amount).HasColumnName("Amount").IsRequired().HasPrecision(18, 4); money.Property(m => m.Currency).HasColumnName("Currency").IsRequired(); })`.
8. Añadir XML doc en clase y método.
9. Ejecutar `dotnet build`.

**Verificación**

- `dotnet build "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --nologo`:
  exit 0, 0 warnings, 0 errors.
- Inspección: `sealed`, file-scoped namespace, `using` al tope, `ToTable("Incomes")`,
  `ValueGeneratedNever`, FK nullable con `SetNull`, índices `IX_Incomes_Date` y
  `IX_Incomes_CategoryId`, `OwnsOne` espejo de `ExpenseConfiguration`.

**Dependencias**
- ninguna (puede ejecutarse en paralelo con T-001, pero ambas deben terminar antes de T-004).

**Estimación**
- S (menos de 1 hora). Mapeo mecánico espejando `ExpenseConfiguration`.

**Trazabilidad**
- AC-6 (`IncomeConfiguration.cs` creado).
- AC-7 (mapeo completo).
- AC-8 (`OwnsOne(Amount)`).
- RF-6.
- DP-2, DP-5.

---

## T-003 — Añadir `DbSet<Income>` a `AppDbContext`

**Objetivo**
Editar `AppDbContext.cs` para exponer el nuevo `DbSet<Income>` con la convención
expression-bodied del codebase, sin tocar `OnModelCreating`.

**Pasos**

1. Abrir `src/ControlDeFinanzasIA.Infrastructure/Persistence/AppDbContext.cs`.
2. Confirmar que `using ControlDeFinanzasIA.Domain.Transactions;` ya está presente (por `Expense`).
3. Añadir `public DbSet<Income> Incomes => Set<Income>();` inmediatamente después del `DbSet<Expense>` existente.
4. No modificar `OnModelCreating` (la configuración se descubre por `ApplyConfigurationsFromAssembly`).
5. Ejecutar `dotnet build`.

**Verificación**

- `dotnet build "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --nologo`:
  exit 0, 0 warnings, 0 errors.
- Inspección: `DbSet<Income> Incomes => Set<Income>();` aparece tras `Expenses`,
  expression-bodied, get-only; `OnModelCreating` sin cambios.

**Dependencias**
- ninguna (puede ejecutarse en paralelo con T-001 y T-002).

**Estimación**
- XS (15 minutos). Cambio de una línea.

**Trazabilidad**
- AC-9 (`AppDbContext` actualizado).
- RF-7.
- C-4 (expression-bodied `DbSet`).

---

## T-004 — Generar migración `AddIncomesTable`

**Objetivo**
Generar la migración EF Core aditiva `AddIncomesTable` usando `--startup-project
src/ControlDeFinanzasIA.UI` (ground truth de `AGENTS.md`), verificar que `Up`
crea la tabla con las 10 columnas, índices y FK `SetNull`, y que `Down` solo
borra la tabla.

**Pasos**

1. Restaurar herramientas si es necesario: `dotnet tool restore`.
2. Ejecutar:
   ```powershell
   dotnet ef migrations add AddIncomesTable `
     --project src/ControlDeFinanzasIA.Infrastructure `
     --startup-project src/ControlDeFinanzasIA.UI
   ```
3. Verificar que se crearon 3 archivos en `src/ControlDeFinanzasIA.Infrastructure/Persistence/Migrations/`:
   - `<timestamp>_AddIncomesTable.cs`
   - `<timestamp>_AddIncomesTable.Designer.cs`
   - `AppDbContextModelSnapshot.cs` (modificado)
4. Inspeccionar el `<timestamp>_AddIncomesTable.cs`:
   - `Up` contiene `CreateTable(name: "Incomes", ...)` con 10 columnas (`Id`, `CategoryId`, `Date`, `Amount`, `Currency`, `Description`, `Note`, `Source`, `CreatedAt`, `UpdatedAt`).
   - `Up` contiene `CreateIndex(name: "IX_Incomes_Date", ...)` y `CreateIndex(name: "IX_Incomes_CategoryId", ...)`.
   - `Up` contiene `AddForeignKey(..., onDelete: ReferentialAction.SetNull)`.
   - `Up` **no** contiene `InsertData`.
   - `Up` no toca `Categories`, `PaymentMethods` ni `Expenses`.
   - `Down` contiene `migrationBuilder.DropTable(name: "Incomes")`.
5. Ejecutar `dotnet build`.

**Verificación**

- `dotnet build "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --nologo`:
  exit 0, 0 warnings, 0 errors.
- Inspección: 3 archivos de migración presentes; `Up` y `Down` cumplen AC-11, AC-12 y AC-13.

**Dependencias**
- T-002 (`IncomeConfiguration` debe existir para que EF la descubra).
- T-003 (`AppDbContext` debe exponer `Incomes` para generar la migración).

**Estimación**
- S (menos de 1 hora). Generación mecánica + revisión del DDL.

**Trazabilidad**
- AC-10 (migración generada).
- AC-11 (`Up` crea `Incomes`).
- AC-12 (`Up` sin seed y aditiva).
- AC-13 (`Down` = `DropTable`).
- RF-8.
- C-5 (`--startup-project UI`).

---

## T-005 — Crear `IncomeTests.cs`

**Objetivo**
Crear los unit tests de la entidad `Income` en `Tests.Unit/Domain/`, usando
**pure xUnit `Assert.*`** (sin FluentAssertions, sin mocking), cubriendo el
happy path y cada invariante.

**Pasos**

1. Crear `tests/ControlDeFinanzasIA.Tests.Unit/Domain/IncomeTests.cs`.
2. Usar file-scoped namespace `ControlDeFinanzasIA.Tests.Unit.Domain`.
3. Añadir `using` al tope:
   - `using ControlDeFinanzasIA.Domain.Enums;`
   - `using ControlDeFinanzasIA.Domain.Transactions;`
   - `using ControlDeFinanzasIA.Domain.ValueObjects;`
   - `using Xunit;`
4. Declarar `public sealed class IncomeTests`.
5. Implementar al menos 8 tests. Cobertura mínima recomendada (nombres adaptables):
   - `Create_WithValidArgs_ReturnsIncomeWithExpectedState` — happy path con categoría y `Source`.
   - `Create_WithNullAmount_ThrowsArgumentNullException` — INV-1.
   - `Create_WithZeroAmount_ThrowsArgumentException` — INV-2 (`[Theory]` con 0 y -1 si se desea).
   - `Create_WithDateBefore1900_ThrowsArgumentException` — INV-3 floor.
   - `Create_WithFutureDate_Succeeds` — INV-3 futuro permitido.
   - `Create_WithEmptyDescription_ThrowsArgumentException` — INV-4 vacío.
   - `Create_WithDescriptionOver200_ThrowsArgumentException` — INV-4 longitud.
   - `Create_WithNoteOver500_ThrowsArgumentException` — INV-5.
   - `Create_WithSourceOver50_ThrowsArgumentException` — INV-6.
   - `Create_WithNullCategoryId_Succeeds` — INV-7 nullable válido.
   - `Create_WithEmptyCategoryId_ThrowsArgumentException` — INV-7 `Guid.Empty` rechazado.
   - `Create_WithWhitespaceNoteAndSource_NormalizesToNull` — INV-5/INV-6 canonización.
   - `Update_WithValidArgs_RefreshesUpdatedAt_PreservesIdAndCreatedAt` — update happy + inmutabilidad.
   - `Update_WithUpdatedAtBeforeCreatedAt_ThrowsArgumentException` — INV-8.
   - `Update_WithInvalidArgs_ThrowsArgumentException` — re-validación INV-1..INV-7.
   - `Equality_ByReference_TwoInstancesNotEqual` — igualdad por referencia.
6. Usar `Assert.Equal`, `Assert.Null`, `Assert.NotNull`, `Assert.True`, `Assert.False`, `Assert.Throws<ArgumentException>`, `Assert.Throws<ArgumentNullException>`.
7. Ejecutar `dotnet test --filter "FullyQualifiedName~IncomeTests"`.

**Verificación**

- `dotnet build "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --nologo`:
  exit 0, 0 warnings, 0 errors.
- `dotnet test "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --nologo --no-build --filter "FullyQualifiedName~IncomeTests"`:
  ≥8 passed, 0 failed.
- Inspección: file-scoped namespace, `using` al tope, `sealed`, aserciones con `Assert.*`
  (sin `.Should()`), sin `Mock<>`.

**Dependencias**
- T-001 (`Income` debe existir e implementar `Create`/`Update`).

**Estimación**
- M (1–2 horas). Escribir tests deterministas contra invariantes requiere cuidado
  con `Money`, `DateOnly` y normalización.

**Trazabilidad**
- AC-14 (unit tests).
- RNF-6 (pure xUnit `Assert.*`).
- DP-9.
- §11.1.

---

## T-006 — Crear `IncomeSchemaTests.cs`

**Objetivo**
Crear 3 tests de integración que verifiquen la forma del schema de `Incomes`
usando `TestAppDbContextFactory` (Fase 2 F-006) y SQLite in-memory.

**Pasos**

1. Crear `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/IncomeSchemaTests.cs`.
2. Usar file-scoped namespace `ControlDeFinanzasIA.Tests.Integration.Persistence`.
3. Añadir `using` al tope:
   - `using ControlDeFinanzasIA.Domain.Enums;`
   - `using ControlDeFinanzasIA.Domain.Transactions;`
   - `using ControlDeFinanzasIA.Domain.ValueObjects;`
   - `using ControlDeFinanzasIA.Infrastructure.Persistence;`
   - `using Microsoft.EntityFrameworkCore;`
   - `using Xunit;`
4. Declarar `public sealed class IncomeSchemaTests`.
5. Implementar los 3 tests:
   - `Migrate_CreatesIncomesTable` — tras `await context.Database.MigrateAsync()`, verificar que `context.Database.GetDbConnection()` lista la tabla `Incomes` con 10 columnas esperadas.
   - `IncomeConfiguration_HasExpectedColumnTypesAndConstraints` — verificar que `CategoryId` es nullable, `Source` tiene max length 50, la FK usa `SET NULL`, y existen índices `IX_Incomes_Date` e `IX_Incomes_CategoryId`.
   - `Money_OwnsOne_ProducesTwoColumns` — verificar columnas `Amount` (NOT NULL) y `Currency` (NOT NULL) en `Incomes`, y que `Amount` tiene precisión 18,4.
   - `Income_CanBePersistedAndRetrieved_RoundTrip` — crear un `Income` con `Money.Create(...)`, guardar, recuperar por `Id` y verificar que `Amount.Amount` y `Amount.Currency` se hidratan correctamente (patrón validado en `Expense`).
6. En cada test usar:
   ```csharp
   using var factory = new TestAppDbContextFactory();
   using AppDbContext context = factory.CreateContext();
   await context.Database.MigrateAsync();
   ```
7. Ejecutar `dotnet test --filter "FullyQualifiedName~IncomeSchemaTests"`.

**Verificación**

- `dotnet build "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --nologo`:
  exit 0, 0 warnings, 0 errors.
- `dotnet test "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --nologo --no-build --filter "FullyQualifiedName~IncomeSchemaTests"`:
  3 passed (o 4 si se añade round-trip), 0 failed.
- Inspección: file-scoped namespace, `using` al tope, `sealed`, `Assert.*`, reutiliza
  `TestAppDbContextFactory`.

**Dependencias**
- T-001 (`Income` existe).
- T-004 (migración `AddIncomesTable` generada; schema refleja configuración).

**Estimación**
- M (1–2 horas). Schema tests con SQLite requieren lectura de metadatos o round-trip.

**Trazabilidad**
- AC-10, AC-11, AC-12, AC-13 (verificación del schema generado).
- §11.2 schema tests.
- Fase 2 F-006 (`TestAppDbContextFactory`).

---

## T-007 — Verificación final, formato y cierre

**Objetivo**
Ejecutar la batería de verificación final de la spec: build limpio, todos los
tests existentes + nuevos verdes, NetArchTest R-1 a R-5, `dotnet format`,
actualizar `spec/phases/phase-04/features.md` y dejar la spec como `.spec.md`
(sin renombrar — el archive es trabajo de la fase).

**Pasos**

1. Ejecutar `dotnet build -c Release` y `dotnet build -c Debug`:
   0 warnings, 0 errors.
2. Ejecutar `dotnet test` completo: confirmar ~222 existentes + ~8 unit + 3 schema
   ≈ 233 passed, 0 failed.
3. Ejecutar `dotnet test --filter "FullyQualifiedName~ArchitectureTests"`
   (o el filtro que cubra los tests NetArchTest R-1 a R-5): todos passed.
4. Ejecutar `dotnet list src/ControlDeFinanzasIA.Domain package`: debe estar vacío.
5. Ejecutar `dotnet list src/ControlDeFinanzasIA.Application package`: debe seguir
   mostrando solo `FluentValidation` (sin EF Core).
6. Ejecutar `dotnet list src/ControlDeFinanzasIA.UI reference`: debe seguir sin
   referencia directa a `Infrastructure`.
7. Ejecutar `dotnet format style --verify-no-changes`: exit 0.
8. Verificar convenciones en `Income.cs`, `IncomeConfiguration.cs`, `IncomeTests.cs` y
   `IncomeSchemaTests.cs`: file-scoped namespace, `using` al tope, XML doc, `sealed`,
   `Assert.*`.
9. Confirmar scope (AC-16): no se implementó `IncomeRepository` / `IncomeFilter`
   (4-002), no use cases (4-003/4-005), no UI (4-008), no modificación de
   `IIncomeRepository`, no `Source` enum, no seed, no migraciones extra.
10. Actualizar `spec/phases/phase-04/features.md`:
    - Tabla resumen: 4-001 de "🔲 PENDIENTE" a "✅ COMPLETADA".
    - Header de sección 4-001: añadir "— ✅ COMPLETADA".
11. Actualizar este archivo de tareas marcando todas las casillas `[x]`.
12. Ejecutar `dotnet build -c Release` final.

**Verificación**

- `dotnet build -c Release`: exit 0, 0 warnings, 0 errors.
- `dotnet test -c Release --no-build`: ~233 passed, 0 failed.
- `dotnet test --filter "FullyQualifiedName~ArchitectureTests"`: todos passed.
- `dotnet format style --verify-no-changes`: exit 0.
- `dotnet list src/ControlDeFinanzasIA.Domain package`: vacío.
- `spec/phases/phase-04/features.md` actualizado con 4-001 completada.
- La spec permanece como `spec/features/phase-04/F-001-income-entity-config-migration.spec.md`
  (NO renombrar a `.done.md`).

**Dependencias**
- T-001 a T-006.

**Estimación**
- S (1 hora). Verificación exhaustiva + cierre administrativo.

**Trazabilidad**
- AC-15 (build, tests, NetArchTest, format).
- AC-16 (sin scope creep).
- RNF-1, RNF-2, RNF-5.

---

## Orden de ejecución recomendado

**Orden secuencial con paralelismo inicial:**

1. **T-001**, **T-002** y **T-003** pueden ejecutarse en cualquier orden (o en
   paralelo); no se bloquean mutuamente. Todas deben terminar antes de T-004.
2. **T-004** depende de T-002 y T-003 (config + `DbSet` necesarios para generar
   la migración).
3. **T-005** depende de T-001 (la entidad debe existir).
4. **T-006** depende de T-001 y T-004 (entidad + schema migrado).
5. **T-005** y **T-006** pueden ejecutarse en paralelo una vez disponibles sus
   dependencias.
6. **T-007** es verificación y cierre de toda la feature.

```text
T-001 ──┐
T-002 ──┼→ T-004 ─┐
T-003 ──┘         ├→ T-007
             T-005 ┘
             T-006 ┘
```

**No hay paralelización útil** entre T-004 y T-005/T-006 porque T-006 requiere
la migración; T-001/T-002/T-003 sí pueden correr juntas.

---

## Tareas bloqueantes / habilitadoras

| Tarea | Habilita | Razón |
| --- | --- | --- |
| T-001 | T-005 | Los tests unitarios instancian `Income` vía `Create`/`Update`. |
| T-001 | T-006 | Los schema tests crean instancias de `Income` para round-trip. |
| T-002 | T-004 | EF Core necesita descubrir `IncomeConfiguration` para generar el schema. |
| T-003 | T-004 | `AppDbContext.Incomes` debe existir para que la migración incluya la tabla. |
| T-004 | T-006 | Los schema tests aplican la migración `AddIncomesTable`. |
| T-005 | T-007 | Todos los tests deben pasar antes del cierre. |
| T-006 | T-007 | Todos los tests deben pasar antes del cierre. |

---

## Estimación total

| Tarea | Dificultad | Horas estimadas |
| --- | --- | --- |
| T-001 | S | 1 |
| T-002 | S | 0.5 |
| T-003 | XS | 0.25 |
| T-004 | S | 0.75 |
| T-005 | M | 1.5 |
| T-006 | M | 1.5 |
| T-007 | S | 1 |
| **Total** | | **~6.5 horas (~media jornada)** |

---

## Riesgos y mitigaciones

| Riesgo | Probabilidad | Impacto | Mitigación |
| --- | --- | --- | --- |
| Reescritura in-place de `Income.cs` pierde el namespace y rompe `IIncomeRepository` | Baja | Alto | RF-1/AC-1 fijan `ControlDeFinanzasIA.Domain.Transactions`; verificar build tras T-001. |
| `Entity<Guid>` es reintroducido por un implementador que lee el phase plan en lugar de la spec | Media | Alto | §17 C-1 documenta la corrección; T-001 verifica `public sealed class Income` sin base. |
| `CategoryId` se mapea como required o la FK usa `Restrict` en lugar de `SetNull` | Media | Medio | §17 C-3 y RF-6 lo documentan; T-004 inspecciona el DDL; T-006 valida el constraint. |
| `--startup-project` se usa `Infrastructure` por inercia de F-002 P3 | Baja | Bajo | §17 C-5 y T-004 fijan `--startup-project src/ControlDeFinanzasIA.UI`. |
| `OwnsOne` no hidrata `Money` (record con props readonly + ctor privado) | Baja | Medio | Patrón validado en `AddExpenseTable` (F-002 P3); T-006 round-trip lo detecta. |
| `Source` se implementa como enum o se omite la normalización a `null` si blank | Baja | Medio | DP-3 fija `string?` max 50; T-005 valida canonización. |
| Se añade seed data (`InsertData`) a la migración por costumbre | Baja | Medio | AC-12 prohíbe seed; T-004 inspecciona `Up`. |
| Tests usan FluentAssertions o mocking por inercia de Phase 3 | Media | Bajo | DP-9 y RNF-6 exigen pure xUnit `Assert.*`; revisar en PR. |
| `dotnet format style` propone cambios inesperados | Baja | Bajo | Aplicar los cambios. No relajar reglas globales. |

---

## Notas para apply

- **`Income.cs` es un placeholder vacío:** antes de reescribir, verificar que el
  archivo contiene solo `public sealed class Income { }`. Reescribir in-place;
  **no** crear `Domain/Incomes/` ni renombrar el archivo.
- **Espejar `Expense.cs` (F-001 P3) y `ExpenseConfiguration.cs` (F-002 P3)** como
  plantilla, aplicando únicamente las dos divergencias: `CategoryId` nullable +
  `SetNull`, y `Source: string?` max 50.
- **No introducir `Entity<Guid>`:** el codebase no tiene base `Entity<TId>`.
  `Income` debe ser standalone como `Expense`.
- **`CategoryId` nullable + `SetNull`:** asegurar que en `IncomeConfiguration` no
  se llama `IsRequired()` sobre `CategoryId` y que `OnDelete` es
  `DeleteBehavior.SetNull`.
- **`AppDbContext.DbSet<T>` expression-bodied:** usar `public DbSet<Income> Incomes => Set<Income>();`
  exactamente como los demás `DbSet`. No usar `{ get; set; }`.
- **Migración con `--startup-project UI`:** ejecutar el comando exacto de T-004.
  No usar `Infrastructure` como startup project (convención anterior superada).
- **No seed data:** la migración `AddIncomesTable` debe ser DDL puro. Rechazar
  cualquier `InsertData` generado manualmente.
- **Pure xUnit `Assert.*`:** los tests de Phase 4 no usan FluentAssertions ni
  mocking. Verificar `tests/.../Tests.Unit/Domain/ExpenseTests.cs` como referencia
  de estilo, pero recordar que allí se usaba FluentAssertions; aquí se reemplaza
  por `Assert.*`.
- **No tocar `Application/`, `UI/` ni `ServiceCollectionExtensions`:** 4-001 no
  registra repositorio; la configuración se descubre por
  `ApplyConfigurationsFromAssembly`.
- **No renombrar la spec a `.done.md`:** esa es responsabilidad del archive de
  la fase. Solo marcar 4-001 como ✅ COMPLETADA en `spec/phases/phase-04/features.md`.
- **Verificar `--startup-project` path:** usar ruta relativa desde la raíz del
  repo: `src/ControlDeFinanzasIA.UI`.

---

## Definition of Done (DoD)

- [x] AC-1: `Income.cs` reescrito in-place en `Domain/Transactions/`. (T-001)
- [x] AC-2: `public sealed class Income` sin base, 9 props con `private set`. (T-001)
- [x] AC-3: 8 invariantes (INV-1..INV-8) enforced en `Create` y `Update`. (T-001)
- [x] AC-4: `Income.Create(...)` genera `Id`, normaliza y setea `UpdatedAt`. (T-001)
- [x] AC-5: `Income.Update(...)` reasigna mutables, valida `UpdatedAt >= CreatedAt`. (T-001)
- [x] AC-6: `IncomeConfiguration.cs` creado como `IEntityTypeConfiguration<Income>`. (T-002)
- [x] AC-7: Mapeo completo con `CategoryId` nullable, FK `SetNull`, índices. (T-002)
- [x] AC-8: `OwnsOne(e => e.Amount)` espejo de `ExpenseConfiguration`. (T-002)
- [x] AC-9: `AppDbContext` expone `DbSet<Income>` expression-bodied tras `Expenses`. (T-003)
- [x] AC-10: Migración `AddIncomesTable` generada con `--startup-project UI`. (T-004)
- [x] AC-11: `Up` crea `Incomes` con 10 columnas, FK e índices. (T-004)
- [x] AC-12: `Up` sin `InsertData` y no toca otras tablas. (T-004)
- [x] AC-13: `Down` = `DropTable("Incomes")`. (T-004)
- [x] AC-14: `IncomeTests.cs` con ≥8 tests pure xUnit `Assert.*`. (T-005)
- [x] AC-15: Build limpio, ~233 tests verdes, NetArchTest R-1..R-5 verdes, format exit 0. (T-007)
- [x] AC-16: Sin scope creep (no repo, no use cases, no UI, no `Source` enum, no seed). (T-007)

---

## Referencias

- `spec/features/phase-04/F-001-income-entity-config-migration.spec.md` — la spec completa de F-001 P4.
- `spec/phases/phase-04/features.md` — 4-001 dentro del desglose de la fase 4.
- `spec/features/phase-03/F-001-expense-domain-entity.done.md` — entidad `Expense` (espejo).
- `spec/features/phase-03/F-002-expense-configuration-migration.done.md` — tabla `Expenses` + `OwnsOne`.
- `spec/features/phase-02/F-006-integration-tests.done.md` — `TestAppDbContextFactory`.
- `src/ControlDeFinanzasIA.Domain/Transactions/Expense.cs` — patrón de entidad a espejar.
- `src/ControlDeFinanzasIA.Infrastructure/Persistence/Configurations/ExpenseConfiguration.cs` — patrón de configuración a espejar.
- `src/ControlDeFinanzasIA.Domain/ValueObjects/Money.cs` — `Money.Create`, props `Amount`/`Currency`.
- `src/ControlDeFinanzasIA.Domain/Enums/Currency.cs` — `DOP=0, USD=1`.
- `src/ControlDeFinanzasIA.Domain/Catalog/Category.cs` — target de la FK `CategoryId`.
- `src/ControlDeFinanzasIA.Application/Abstractions/IIncomeRepository.cs` — interface existente; no se modifica.
- `AGENTS.md` — convención de migraciones (`--startup-project UI`), NetArchTest R-1..R-5.
- Engram `sdd/controldefinanzasia/testing-capabilities` — pure xUnit `Assert.*`.
- Engram `sdd/f-001p4-income-entity-config-migration/proposal` y `/spec`.
