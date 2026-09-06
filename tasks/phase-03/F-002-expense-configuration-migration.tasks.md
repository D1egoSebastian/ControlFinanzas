# Tareas — F-002: `ExpenseConfiguration` + migración `AddExpenseTable`

> **Spec:** `spec/features/phase-03/F-002-expense-configuration-migration.spec.md`
> **Fuentes de verdad:** la spec, `docs/vision.md` §4.1, §4.2,
> `docs/architecture.md` §3, §4, §8,
> `docs/roadmap.md` §Fase 3,
> `spec/constitution/tech-stack.md` (.NET 8, EF Core 8, SQLite),
> `spec/constitution/mission.md` (dominio puro, sin duplicación),
> `spec/phases/phase-03/features.md` (F-002 dentro del desglose de la fase 3),
> `spec/features/phase-03/F-001-expense-domain-entity.done.md` (provee `Expense`),
> `spec/features/phase-02/F-002-entity-configurations-migration.done.md`
> (patrón de configuraciones Fluent API + migración versionada),
> `spec/features/phase-02/F-006-integration-tests.done.md`
> (provee `TestAppDbContextFactory`).
>
> Cada tarea es implementable en menos de 2 horas. El proyecto debe
> seguir compilando al finalizar cada una. La numeración (T-001...)
> es local a esta feature. F-002 modifica
> `Infrastructure/Persistence/` y `Tests.Integration/Persistence/`.
> NO modifica `Domain`, `Application`, `UI`, `Directory.Packages.props`
> ni el CI workflow.

---

## Resumen de tareas

| ID | Tarea | Descripción corta | Estimación | Depende de | Verificación |
| --- | --- | --- | --- | --- | --- |
| T-001 | Verificar `dotnet-ef` y línea base del build/tests | Confirma herramienta, build limpio y 164 tests verdes antes de F-002 | S | ninguna | `dotnet ef --version`; `dotnet build` exit 0; `dotnet test` 164 passed | ✅ COMPLETADA |
| T-002 | Crear `ExpenseConfiguration.cs` con Fluent API | Mapeo completo de `Expense`; `OwnsOne(e => e.Amount)` sin migración aún | M | T-001 | `dotnet build` exit 0; configuración compila | ✅ COMPLETADA |
| T-003 | Agregar `DbSet<Expense> Expenses` a `AppDbContext` | Única edición al contexto; no tocar `OnModelCreating` | S | T-002 | `dotnet build` exit 0; `dotnet test` 164 passed | ✅ COMPLETADA |
| T-004 | Generar migración `AddExpenseTable` | Ejecuta `dotnet ef migrations add`; ajusta `Money` si `OwnsOne` no hidrata | M | T-002, T-003 | 3 archivos en `Migrations/`; `Up` DDL puro sin `InsertData`; build exit 0 | ✅ COMPLETADA |
| T-005 | Crear `ExpenseSchemaTests.cs` con 3 tests de integración | ST-1, ST-2, ST-3 reutilizando `TestAppDbContextFactory` | M | T-004 | `dotnet test --filter "FullyQualifiedName~ExpenseSchemaTests"` 3 passed | ✅ COMPLETADA |
| T-006 | Verificación final, formato y cierre | Build, tests, NetArchTest, format, `features.md`, rename a `.done.md` | M | T-001 a T-005 | `dotnet test` 167 passed; `dotnet format style --verify-no-changes` exit 0; spec renombrada | ✅ COMPLETADA |

**Total estimado:** ~1 jornada. T-002 y T-005 son las más densas
(`OwnsOne` + tests de schema); T-001, T-003 y T-004 son mecánicas;
T-006 es verificación y cierre administrativo.

---

## T-001 — Verificar `dotnet-ef` y línea base del build/tests

**Objetivo**
Confirmar que la herramienta `dotnet-ef` 8.0.x está disponible en el
manifest local, que la solución compila limpio y que los 164 tests
existentes (134 Unit + 30 Integration) pasan antes de introducir
ningún cambio de F-002.

**Pasos**

1. Verificar que `.config/dotnet-tools.json` existe y contiene `dotnet-ef` 8.0.x.
2. Ejecutar `dotnet tool restore` desde la raíz si es necesario.
3. Ejecutar `dotnet ef --version` y confirmar que muestra 8.0.x.
4. Ejecutar `dotnet build -c Release` desde la raíz.
5. Ejecutar `dotnet test` desde la raíz y anotar el recuento actual.

**Verificación**

- `dotnet ef --version` muestra `8.0.x`.
- `dotnet build "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --nologo`:
  exit 0, 0 warnings, 0 errors.
- `dotnet test "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --nologo --no-build`:
  164 passed, 0 failed (134 Unit + 30 Integration).

**Dependencias**
- ninguna.

**Estimación**
- S (menos de 1 hora). Es pura verificación de la línea base.

**Trazabilidad**
- AC-4 (build y tests pasan).
- RNF-2 (`dotnet-ef` 8.0.x ya instalado).
- §10.1 herramienta `dotnet-ef`.

---

## T-002 — Crear `ExpenseConfiguration.cs` con Fluent API

**Objetivo**
Crear el mapeo Fluent API de `Expense` a la tabla `Expenses`, incluyendo
la descomposición de `Money` vía `OwnsOne(e => e.Amount)` en dos columnas
primitivas (`Amount` decimal + `Currency` int), la FK a `Categories` con
`OnDelete(Restrict)`, los índices en `Date` y `CategoryId`, y las
longitudes máximas de `Description` y `Note`. No se genera la migración
aún; solo se escribe y compila la configuración.

**Pasos**

1. Crear `src/ControlDeFinanzasIA.Infrastructure/Persistence/Configurations/ExpenseConfiguration.cs`.
2. Usar file-scoped namespace `ControlDeFinanzasIA.Infrastructure.Persistence.Configurations`.
3. Añadir `using` al tope:
   - `using ControlDeFinanzasIA.Domain.Catalog;` (para `HasOne<Category>()`).
   - `using ControlDeFinanzasIA.Domain.Transactions;` (para `Expense`).
   - `using Microsoft.EntityFrameworkCore;`.
   - `using Microsoft.EntityFrameworkCore.Metadata.Builders;`.
4. Declarar `public sealed class ExpenseConfiguration : IEntityTypeConfiguration<Expense>`.
5. En `Configure(EntityTypeBuilder<Expense> b)`:
   - `b.ToTable("Expenses")`.
   - `b.HasKey(e => e.Id)`.
   - `b.Property(e => e.Id).ValueGeneratedNever()`.
   - `b.Property(e => e.CategoryId).IsRequired()`.
   - `b.Property(e => e.Date).IsRequired()` (`DateOnly` nativo en EF Core 8).
   - `b.Property(e => e.Description).IsRequired().HasMaxLength(200)`.
   - `b.Property(e => e.Note).HasMaxLength(500)` (nullable, sin `IsRequired`).
   - `b.Property(e => e.CreatedAt).IsRequired()`.
   - `b.Property(e => e.UpdatedAt).IsRequired()`.
   - FK: `b.HasOne<Category>().WithMany().HasForeignKey(e => e.CategoryId).OnDelete(ReferentialAction.Restrict)`.
   - Índices: `b.HasIndex(e => e.Date)` y `b.HasIndex(e => e.CategoryId)`.
   - `OwnsOne` de `Money`:
     ```text
     b.OwnsOne(e => e.Amount, money =>
     {
         money.Property(m => m.Amount).HasColumnName("Amount").IsRequired().HasPrecision(18, 4);
         money.Property(m => m.Currency).HasColumnName("Currency").IsRequired();
     });
     ```
6. Añadir XML doc en la clase y en `Configure`, citando `architecture.md` §8
   y F-001 de Fase 3.
7. Ejecutar `dotnet build`.

**Verificación**

- `dotnet build "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --nologo`:
  exit 0, 0 warnings, 0 errors.
- Inspección: el archivo existe, usa file-scoped namespace, `using` al tope,
  clase `sealed`, XML doc y la firma correcta.

**Dependencias**
- T-001 (línea base verificada).
- F-001 de Fase 3 (entidad `Expense` existe en `Domain/Transactions/`).
- F-002 de Fase 1 (`Money` y `Currency` existen).
- F-002 de Fase 2 (patrón de configuraciones y convenciones).

**Estimación**
- M (1–2 horas). El cuerpo de la configuración es similar a las de
  catálogo, pero el `OwnsOne` de `Money` requiere atención.

**Trazabilidad**
- AC-1 (`ExpenseConfiguration` creada con todas las reglas).
- RF-1 (mapeo completo de `Expense`), RF-2 (mapeo `OwnsOne` de `Money`).
- RN-2 (PascalCase), RN-3 (`Money` a columnas primitivas), RN-4
  (`ValueGeneratedNever`), RN-6 (`Restrict`).
- §9 tabla de columnas.

---

## T-003 — Agregar `DbSet<Expense> Expenses` a `AppDbContext`

**Objetivo**
Editar `AppDbContext.cs` para exponer `DbSet<Expense> Expenses`, ubicado
detrás de los `DbSet` existentes de catálogo. Esta es la única edición
permitida al `AppDbContext` en F-002; `OnModelCreating` no se toca.

**Pasos**

1. Abrir `src/ControlDeFinanzasIA.Infrastructure/Persistence/AppDbContext.cs`.
2. Añadir `using ControlDeFinanzasIA.Domain.Transactions;` al tope (si no está).
3. Añadir la propiedad:
   ```text
   /// <summary>
   /// Conjunto de entidades <see cref="Expense" /> mapeado a la tabla
   /// <c>Expenses</c> en SQLite.
   /// </summary>
   public DbSet<Expense> Expenses => Set<Expense>();
   ```
   justo después de `PaymentMethods`.
4. Ejecutar `dotnet build`.
5. Ejecutar `dotnet test` y confirmar que los tests heredados siguen verdes.

**Verificación**

- `dotnet build "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --nologo`:
  exit 0, 0 warnings, 0 errors.
- `dotnet test "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --nologo --no-build`:
  164 passed, 0 failed.
- Inspección: `OnModelCreating` no se modificó.

**Dependencias**
- T-002 (`ExpenseConfiguration` compila).

**Estimación**
- S (menos de 1 hora). Cambio mecánico de una propiedad y un `using`.

**Trazabilidad**
- AC-2 (`AppDbContext` actualizado).
- RF-3 (`DbSet<Expense>`).
- §10.3 edición del `AppDbContext`.

---

## T-004 — Generar migración `AddExpenseTable`

**Objetivo**
Generar la migración aditiva `AddExpenseTable` que crea la tabla
`Expenses`, sus índices y su FK a `Categories`, sin tocar las tablas
existentes ni incluir `InsertData`. Si `OwnsOne` no puede hidratar el
`record Money` (propiedades `{ get; }` + constructor privado), coordinar
el ajuste mínimo con F-001 en esta misma tarea.

**Pasos**

1. Verificar que `dotnet ef --version` sigue en 8.0.x.
2. Confirmar que `ExpenseConfiguration.cs` y el `DbSet<Expense>` existen y compilan.
3. Ejecutar desde la raíz:
   ```text
   dotnet ef migrations add AddExpenseTable --project src/ControlDeFinanzasIA.Infrastructure --startup-project src/ControlDeFinanzasIA.Infrastructure
   ```
4. Si el comando falla por hidratación de `Money`, evaluar el error:
   - Opción preferida: coordinar con F-001 para exponer el constructor
     de `Money` como `internal` (sin romper inmutabilidad) o cambiar
     `{ get; }` a `{ get; init; }`.
   - Documentar cualquier cambio a `Money` en el apply de F-002.
5. Verificar que existen 3 archivos en `Migrations/`:
   - `YYYYMMDDhhmmss_AddExpenseTable.cs`
   - `YYYYMMDDhhmmss_AddExpenseTable.Designer.cs`
   - `AppDbContextModelSnapshot.cs` (actualizado)
6. Inspeccionar el `Up` de la migración:
   - Debe contener `CreateTable` para `Expenses` con las 9 columnas:
     `Id`, `CategoryId`, `Date`, `Amount`, `Currency`, `Description`,
     `Note`, `CreatedAt`, `UpdatedAt`.
   - Debe contener `CreateIndex` para `IX_Expenses_Date` y
     `IX_Expenses_CategoryId`.
   - Debe contener `AddForeignKey` de `Expenses.CategoryId` →
     `Categories.Id` con `onDelete: Restrict`.
   - **NO debe contener `InsertData`.**
7. Inspeccionar el `Down`: debe contener `DropTable("Expenses")`.
8. Ejecutar `dotnet build`.

**Verificación**

- `dotnet ef migrations add ...` completa con exit 0.
- `dotnet build "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --nologo`:
  exit 0, 0 warnings, 0 errors.
- Los 3 archivos de migración existen en `Infrastructure/Persistence/Migrations/`.
- El `Up` contiene `CreateTable("Expenses", ...)`, `CreateIndex` para
  `IX_Expenses_Date` e `IX_Expenses_CategoryId`, `AddForeignKey` con
  `onDelete: Restrict`, y **no contiene `InsertData`**.
- (Opcional) `dotnet ef migrations script --project src/ControlDeFinanzasIA.Infrastructure --startup-project src/ControlDeFinanzasIA.Infrastructure`
  genera SQL con solo `CREATE TABLE`, `CREATE INDEX`, `ALTER TABLE ... ADD FOREIGN KEY`;
  sin `INSERT INTO`.

**Dependencias**
- T-002 (`ExpenseConfiguration` creada).
- T-003 (`DbSet<Expense>` agregado).
- F-002 de Fase 2 (`InitialSchema` y `SeedSystemData` ya crean `Categories` y
  `PaymentMethods`; la FK apunta a `Categories.Id`).

**Estimación**
- M (1–2 horas). El comando es rápido, pero la inspección del `Up` y el
  posible ajuste de `Money` pueden consumir tiempo.

**Trazabilidad**
- AC-3 (migración generada con 3 archivos, `Up` DDL puro, `Down` correcto).
- RF-4 (comando de migración), RF-5 (contenido de la migración).
- RN-5 (sin `InsertData`), RN-6 (`Restrict`).
- EC-1 (hidratación de `Money`), EC-6 (`--startup-project`).
- §10.2 comando de generación.

---

## T-005 — Crear `ExpenseSchemaTests.cs` con 3 tests de integración

**Objetivo**
Añadir tests de integración que verifiquen la **forma del schema**
producido por la configuración y la migración: existencia de la tabla
`Expenses`, columnas/restricciones/FK/índices, y descomposición de `Money`
en dos columnas. Reutilizar `TestAppDbContextFactory` de F-006 de Fase 2.

**Pasos**

1. Crear `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/ExpenseSchemaTests.cs`.
2. Usar file-scoped namespace `ControlDeFinanzasIA.Tests.Integration.Persistence`.
3. Añadir `using` al tope: `FluentAssertions`, `Microsoft.Data.Sqlite`,
   `Microsoft.EntityFrameworkCore`, `Xunit`, `ControlDeFinanzasIA.Infrastructure.Persistence`.
4. Implementar la clase `public sealed class ExpenseSchemaTests` con 3 `[Fact]`:
   - **ST-1 — `Migrate_CreatesExpensesTable`**: crea contexto con la factoría,
     ejecuta `await context.Database.MigrateAsync()`, luego consulta
     `sqlite_master` o inspecciona `context.Model` para confirmar que la
     tabla `Expenses` existe con 9 columnas.
   - **ST-2 — `ExpenseConfiguration_HasExpectedColumnTypesAndConstraints`**:
     verifica que `Description` es requerida con max 200, `Note` es
     nullable con max 500, `Id` es PK sin generación automática, existe
     la FK `CategoryId → Categories.Id` con `Restrict`, y existen los
     índices `IX_Expenses_Date` e `IX_Expenses_CategoryId`.
     Preferir inspección de `context.Model` para determinismo.
   - **ST-3 — `Money_OwnsOne_ProducesTwoColumns`**: confirma que existen
     las columnas `Amount` y `Currency` en `Expenses`, ambas NOT NULL,
     y que no existe una columna `AmountJson` ni una tabla `Money`.
5. Ejecutar `dotnet test --filter "FullyQualifiedName~ExpenseSchemaTests"`.
6. Una vez verdes, ejecutar `dotnet test` completo.

**Verificación**

- `dotnet build "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --nologo`:
  exit 0, 0 warnings, 0 errors.
- `dotnet test "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --nologo --no-build --filter "FullyQualifiedName~ExpenseSchemaTests"`:
  3 passed, 0 failed.
- `dotnet test "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --nologo --no-build`:
  167 passed, 0 failed (164 existentes + 3 nuevos).

**Dependencias**
- T-004 (migración `AddExpenseTable` generada y compila).
- F-006 de Fase 2 (`TestAppDbContextFactory` reutilizable).

**Estimación**
- M (2–3 horas). Escribir tests deterministas contra el modelo de EF Core
  requiere precisión en las aserciones.

**Trazabilidad**
- AC-5 (tests de integración de schema ST-1..ST-3).
- RF-7 (compilación y tests).
- §11 tests de integración de forma del schema.

---

## T-006 — Verificación final, formato y cierre

**Objetivo**
Ejecutar la batería de verificación final de la spec: build limpio,
todos los tests verdes, NetArchTest R-1 a R-5, `dotnet format`,
confirmar que no se introdujeron paquetes en `Domain`, actualizar
`spec/phases/phase-03/features.md` y renombrar la spec a `.done.md`.

**Pasos**

1. Ejecutar `dotnet build -c Release` y `dotnet build -c Debug`:
   0 warnings, 0 errors.
2. Ejecutar `dotnet test` completo: confirmar 167 passed, 0 failed.
3. Ejecutar `dotnet test --filter "FullyQualifiedName~ArchitectureTests"`
   (o el filtro que cubra los 11 tests NetArchTest R-1 a R-5): todos passed.
4. Ejecutar `dotnet list src/ControlDeFinanzasIA.Domain package`: debe estar vacío.
5. Ejecutar `dotnet list src/ControlDeFinanzasIA.Application package`: debe seguir
   mostrando solo `FluentValidation` (sin EF Core).
6. Ejecutar `dotnet list src/ControlDeFinanzasIA.UI reference`: debe seguir sin
   referencia a `Infrastructure`.
7. Ejecutar `dotnet format style --verify-no-changes`: exit 0.
8. Verificar convenciones en `ExpenseConfiguration.cs` y `ExpenseSchemaTests.cs`:
   file-scoped namespace, `using` al tope, XML doc, `sealed`.
9. Confirmar scope (AC-7): no se creó `ExpenseRepository`, no se extendió
   `IExpenseRepository`, no se añadieron use cases/DTOs/UI, no hay seed de
   `Expense`, no hay CHECK constraints, no se modificó `OnModelCreating`.
10. Actualizar `spec/phases/phase-03/features.md`:
    - Tabla resumen: F-002 de "🔲 PENDIENTE" a "✅ COMPLETADA".
    - Header de sección F-002: añadir "— ✅ COMPLETADA".
11. Renombrar `spec/features/phase-03/F-002-expense-configuration-migration.spec.md`
    a `spec/features/phase-03/F-002-expense-configuration-migration.done.md`.
12. Ejecutar `dotnet build -c Release` final.

**Verificación**

- `dotnet build -c Release`: exit 0, 0 warnings, 0 errors.
- `dotnet test -c Release --no-build`: 167 passed, 0 failed.
- `dotnet test --filter "FullyQualifiedName~ArchitectureTests"`: todos passed.
- `dotnet format style --verify-no-changes`: exit 0.
- `dotnet list src/ControlDeFinanzasIA.Domain package`: vacío.
- `spec/phases/phase-03/features.md` actualizado con F-002 completada.
- Spec renombrada a `.done.md`.

**Dependencias**
- T-001 a T-005.

**Estimación**
- M (1–2 horas). Verificación exhaustiva + cierre administrativo.

**Trazabilidad**
- AC-4 (build y tests), AC-6 (migración aditiva verificada por los tests),
  AC-7 (sin scope creep), AC-8 (documentación y trazabilidad).
- RNF-1 (NetArchTest R-1 a R-5), RNF-2 (sin paquetes nuevos).

---

## Orden de ejecución recomendado

**Orden secuencial estricto:** T-001 → T-002 → T-003 → T-004 → T-005 → T-006.

- **T-001 debe ir primero** porque establece la línea base y confirma que
  `dotnet-ef` funciona. Sin ella, no se detectan regressions de F-002.
- **T-002 debe ir antes que T-003 y T-004** porque define el mapeo. Agregar
  el `DbSet` o generar la migración antes de tener la configuración haría
  que EF Core use convenciones por defecto y perdería `OwnsOne`,
  `ValueGeneratedNever`, `Restrict`, longitudes máximas e índices.
- **T-003 debe ir antes que T-004** porque `dotnet ef migrations add` necesita
  que el `DbSet<Expense>` esté expuesto para incluir `Expenses` en el modelo.
- **T-004 debe ir antes que T-005** porque los tests de schema migran la BD
  y esperan que `AddExpenseTable` exista.
- **T-005 debe ir antes que T-006** porque es la última entrega funcional; los
  tests de schema deben pasar antes del cierre.
- **T-006 es verificación y cierre** de toda la feature.

**No hay paralelización útil** para una feature tan pequeña y secuencial;
cada tarea depende del resultado inmediato anterior.

---

## Definition of Done (DoD)

La feature se considera completada cuando **todos** los siguientes puntos
se cumplen:

- [x] **AC-1 — `ExpenseConfiguration` creada:**
  `src/ControlDeFinanzasIA.Infrastructure/Persistence/Configurations/ExpenseConfiguration.cs`
  existe como `public sealed class ExpenseConfiguration : IEntityTypeConfiguration<Expense>`,
  con `ToTable("Expenses")`, `ValueGeneratedNever` en `Id`, FK a `Categories` con
  `OnDelete(Restrict)`, índices en `Date` y `CategoryId`, `OwnsOne(e => e.Amount)`
  con columnas `Amount` (`HasPrecision(18, 4)`) y `Currency`, y convenciones
  de código. (T-002)

- [x] **AC-2 — `AppDbContext` actualizado:**
  expone `public DbSet<Expense> Expenses => Set<Expense>();` tras los `DbSet`
  de catálogo; `OnModelCreating` no se modificó. (T-003)

- [x] **AC-3 — Migración `AddExpenseTable` generada:**
  el comando `dotnet ef migrations add AddExpenseTable` se ejecutó sin error;
  existen los 3 archivos en `Migrations/`; el `Up` crea `Expenses` con 9
  columnas, 2 índices y la FK con `Restrict`; el `Up` no incluye `InsertData`;
  el `Down` borra `Expenses`. (T-004)

- [x] **AC-4 — Compilación y tests:**
  `dotnet build -c Release` 0 warnings / 0 errors; `dotnet test` 167 passed;
  NetArchTest R-1 a R-5 pasan; `dotnet list src/ControlDeFinanzasIA.Domain package`
  sigue vacío; `dotnet format style --verify-no-changes` exit 0. (T-006)

- [x] **AC-5 — Tests de integración de schema:**
  `ExpenseSchemaTests.cs` existe con `Migrate_CreatesExpensesTable`,
  `ExpenseConfiguration_HasExpectedColumnTypesAndConstraints` y
  `Money_OwnsOne_ProducesTwoColumns`; los 3 tests pasan reutilizando
  `TestAppDbContextFactory`. (T-005)

- [x] **AC-6 — Migración aditiva verificada:**
  tras aplicar `AddExpenseTable` sobre una BD con `InitialSchema` +
  `SeedSystemData`, las tablas `Categories` y `PaymentMethods` siguen
  existiendo (verificable implícitamente por la FK y los tests heredados). (T-005, T-006)

- [x] **AC-7 — Sin scope creep:**
  no se implementó `ExpenseRepository`, no se extendió `IExpenseRepository`,
  no se añadieron use cases/DTOs/UI, no hay seed de `Expense`, no hay CHECK
  constraints, no se añadieron tests CRUD del repo, no se modificó
  `OnModelCreating`, `Domain` ni `Application`, ni se añadieron paquetes NuGet. (T-006)

- [x] **AC-8 — Documentación y trazabilidad:**
  `spec/phases/phase-03/features.md` marca F-002 como ✅ COMPLETADA y la spec
  se renombró a `.done.md`. (T-006)

---

## Riesgos y mitigaciones

| Riesgo | Probabilidad | Impacto | Mitigación |
| --- | --- | --- | --- |
| `OwnsOne` no hidrata `Money` (`record` con props `{ get; }` y constructor privado). | Media | Medio | T-004 incluye el ajuste coordinado con F-001: exponer constructor de `Money` como `internal` o cambiar `{ get; }` a `{ get; init; }`. Documentar cualquier cambio en el apply. Ver EC-1 y §17.5. |
| `DateOnly` no se mapea nativamente en EF Core 8 a SQLite. | Baja | Medio | F-001 fijó `DateOnly` sin conversor. Si el scaffold o los tests revelan un edge case, introducir `ValueConverter<DateOnly, string>` en T-004 y regenerar. Ver EC-2. |
| La migración incluye `InsertData` por error. | Baja | Bajo | `Expense` no tiene `HasData`. T-004 inspecciona el `Up` y elimina cualquier `InsertData` si apareciera. AC-3 verifica. Ver EC-3 / RN-5. |
| `Migrations/` commiteado sin los 3 archivos. | Baja | Alto | AC-3 exige los 3 archivos; T-004 verifica su existencia antes de pasar a T-005. Ver EC-8. |
| El `--startup-project` cambia tras F-005 de Fase 2 y el scaffolding falla. | Baja | Bajo | T-001/T-004 ejecutan el comando con `--startup-project Infrastructure`. Si falla, probar con `UI` y documentar; el schema generado no cambia. Ver EC-6. |
| `OnDelete(Restrict)` rompe tests futuros que borran categorías. | Baja | Bajo | F-002 no borra categorías. El comportamiento de borrado es de Fase 4; los tests de F-003/F-004 manejarán el constraint. Ver EC-5. |
| CHECK constraints de invariantes se añaden por error. | Baja | Medio | T-002/T-004 revisan que la configuración solo imponga nulabilidad, longitudes y FK. AC-7 verifica explícitamente que no hay CHECK constraints. Ver RN-7. |
| Los tests de schema dependen del timestamp de la migración. | Baja | Bajo | Inspeccionar `context.Model` o `sqlite_master` en lugar de leer el nombre de archivo. Ver §11. |

---

## Referencias

- `spec/features/phase-03/F-002-expense-configuration-migration.spec.md` — la spec
  completa de F-002. Este tasks file es su descomposición técnica.
- `spec/phases/phase-03/features.md` — F-002 dentro del desglose de la fase 3.
- `spec/features/phase-03/F-001-expense-domain-entity.done.md` — entidad `Expense`
  que F-002 mapea.
- `spec/features/phase-02/F-002-entity-configurations-migration.done.md` — patrón
  estructural de configuraciones Fluent API + migración versionada que F-002 espeja.
- `spec/features/phase-02/F-006-integration-tests.done.md` — provee
  `TestAppDbContextFactory` que F-002 reutiliza.
- `spec/features/phase-01/F-002-money-value-object.done.md` — value object `Money`
  y enum `Currency` que F-002 mapea con `OwnsOne`.
- `spec/features/phase-01/F-007-validacion-arquitectonica.done.md` — tests
  NetArchTest R-1 a R-5 que F-002 no debe romper.
- `tasks/phase-03/F-001-expense-domain-entity.tasks.md` — formato de referencia
  para tasks files de la Fase 3.
- `tasks/phase-02/F-002-entity-configurations-migration.tasks.md` — formato de
  referencia estructural para tareas de configuración + migración.
- `docs/architecture.md` — §8 (mapeo Fluent API, value objects a columnas
  primitivas, índices, convención PascalCase).
- `docs/vision.md` — §4.1 (gastos del día a día), §4.2 (subconjunto mínimo de
  un movimiento).
- `docs/roadmap.md` — §Fase 3 (slice vertical registrar/listar gastos).
- `spec/constitution/tech-stack.md` — .NET 8, EF Core 8.0.11, SQLite, Clean
  Architecture.
- `spec/constitution/mission.md` — dominio puro, sin duplicación de lógica.
