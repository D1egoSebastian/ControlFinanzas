# Specification — F-004: Entidad `Tag` + M:N `Expense↔Tag` + `TagRepository` + migración

> **Fase:** 4 — Transacciones: ingresos, edición, borrado, tags, filtros, vistas guardadas
> **Feature:** F-004 (local a esta fase)
> **Spec ID:** SPEC-F004P4
> **change_id:** `f-004p4-tag-entity-m-n`
> **Modo:** Standard (SDD: propose → spec → tasks → apply → verify → archive)
> **Estado:** Pendiente de implementación (renombrar a `.done.md` tras apply + verify)
>
> **Nota de numeración:** este `F-004` es el de la **Fase 4**
> (`spec/phases/phase-04/features.md`, §RF-9..RF-12). No debe
> confundirse con el `F-004` de Fase 2 (`CategoryRepository`) ni con
> otros `F-004` de fases previas. Los IDs son locales a cada fase; el
> folder los desambigua. Ver `spec/README.md`.
>
> Fuentes de verdad: `docs/vision.md`, `docs/architecture.md`,
> `docs/roadmap.md` (§Fase 4), `spec/constitution/tech-stack.md`,
> `spec/constitution/mission.md`,
> `spec/phases/phase-04/features.md` (§RF-9, RF-10, RF-11 — líneas
> 184-200),
> `.atl/sdd/f-004p4-tag-entity-m-n/proposal.md` (PRD de origen — 67
> líneas),
> `spec/features/phase-04/F-001-income-entity-config-migration.done.md`
> (patrón espejo: entidad `sealed class` + `Configuration` + migración),
> `spec/features/phase-04/F-002-income-repository.done.md` (patrón
> espejo: repositorio + tests de integración),
> `spec/features/phase-03/F-001-expense-domain-entity.done.md`
> (patrón `Expense` entity),
> `spec/features/phase-02/F-004-category-paymentmethod-repositories.done.md`
> (patrón `CategoryRepository` — catálogo).

---

## 1. Metadata

| Campo | Valor |
| --- | --- |
| **change_id** | `f-004p4-tag-entity-m-n` |
| **spec_id** | `SPEC-F004P4` |
| **modo** | Standard (SDD: propose → spec → tasks → apply → verify → archive) |
| **fase** | 4 |
| **feature** | F-004 (local a Fase 4) |
| **dependencies (build)** | Phase 1 F-006 (`ITagRepository` 5 métodos) ✅; Phase 3 F-001 (patrón `Expense` entity) ✅ archivada; Phase 3 F-002 (`ExpenseConfiguration` + `OwnsOne`) ✅ archivada; Phase 2 F-004 (`CategoryRepository` — patrón catálogo) ✅ archivada |
| **dependencies (test infra)** | F-006 Fase 2 (`TestAppDbContextFactory`, SQLite in-memory) ✅; F-003 Fase 2 (seed de 9 categorías con GUIDs `11111111-1111-1111-1111-00000000000X`) ✅ |
| **dependencies (contrato)** | F-006 Fase 1 (`ITagRepository` 5 métodos base) ✅; F-005 Fase 1 (`Result` non-generic, `Value: object?`) ✅ |
| **unblocks** | F-007 Fase 4 (extender filtros `ExpenseFilter.TagIds` + `ExpenseRepository.GetByFilterAsync`), F-008 Fase 4 (UI de asignación de tags en `ExpensesView`), futuros use cases de tags |
| **artifact_store** | Engram (objetivo: `sdd/f-004p4-tag-entity-m-n/spec`) + filesystem (este archivo) |
| **status** | Pendiente de implementación |
| **línea base de tests (pre-F-004)** | ~263 (aproximada, post F-001/F-002/F-003 P4; según contrato de lanzamiento) |

---

## 2. Contexto y objetivo

Phase 3 cerró el slice vertical de `Expense` (registrar + listar +
paginar) y Phase 4 ya entregó el backend simétrico de `Income`
(F-001/F-002/F-003). F-004 abre el **tercer eje** de Phase 4: los
**tags** — categorización many-to-many libre (vs. `Category` que es
jerárquica y 1:N). La visión (`docs/vision.md` §4.3) prevé filtrar la
lista por categoría, **tags** y rango de fecha; el roadmap (§Fase 4)
descompone los tags en 4-004 (entidad + repo + M:N + migración — esta
feature), 4-007 (filtros por `TagIds`) y 4-008 (UI de asignación).

F-004 es el **nodo inicial** del eje de tags: sin el agregado `Tag`
blindado, la tabla join `ExpenseTags` y el `TagRepository`, ni 4-007
ni 4-008 pueden consumir nada. Es **independiente** de la rama de
Income (4-001..4-003) y de la rama Update/Delete (4-005), por lo que
puede implementarse en paralelo.

**Estado actual verificado.** `src/.../Domain/Catalog/Tag.cs` es un
*placeholder* de 11 líneas: `public sealed record Tag` con sólo
`Id` (`Guid`, `init`) y `Name` (`string`, `init`). Existe
únicamente para que `ITagRepository` (Fase 1 F-006) compile.
`ITagRepository` declara **exactamente 5 métodos** (`AddAsync`,
`UpdateAsync`, `RemoveAsync`, `GetByIdAsync`, `GetAllAsync`) —
verificado en `src/.../Application/Abstractions/ITagRepository.cs`
(57 líneas). No hay `TagConfiguration`, no hay `DbSet<Tag>`, no hay
`ExpenseTags`, y `Expense` no tiene colección `Tags`. Esta feature
completa todo eso.

**Trazabilidad.** `vision.md` §4.3 (listado + filtros por tags);
`architecture.md` §6 (capa de dominio), §8 (mapeo Fluent API,
migraciones versionadas, value objects a columnas primitivas);
`roadmap.md` §Fase 4 (entregable: entidad `Tag` + M:N).

**Objetivos concretos:**

1. **Promover `Tag` de `record` placeholder a `public sealed class`**
   con factory `Create`, método `Update`, propiedades con `private
   set` e invariantes (espejo de `Expense`/`Income`, NO de `Category`
   que sigue siendo `record`).
2. **Añadir `Expense.Tags`** (`ICollection<Tag>`) + métodos de
   mutación `AddTag`/`RemoveTag` con semántica idempotente.
3. **Crear `TagConfiguration`** (tabla `Tags`, índice único `Name`).
4. **Extender `ExpenseConfiguration`** con el mapeo M:N a la tabla
   join `ExpenseTags` (PK compuesta, FKs `Cascade`).
5. **Añadir `DbSet<Tag> Tags`** a `AppDbContext` y registrar
   `ITagRepository → TagRepository` en `AddPersistence`.
6. **Generar UNA migración `AddTagsAndExpenseTags`** (crea `Tags` +
   `ExpenseTags`, DDL puro, sin seed).
7. **Implementar `TagRepository`** (5 métodos, espejo de
   `CategoryRepository` — patrón catálogo).
8. **Cubrir con tests** unitarios (Tag + Expense.AddTag/RemoveTag) e
   integración (schema, repo, round-trip M:N). Pure xUnit `Assert.*`,
   sin FluentAssertions, sin mocking.
9. **Mantener NetArchTest R-1..R-5 verdes**; `dotnet build` 0
   warnings; `dotnet format style --verify-no-changes` exit 0.

---

## 3. Decisiones (locked-in — no re-abrir)

### D-1 — `ITagRepository` mantiene 5 métodos (NO se extiende)

**Decisión:** `ITagRepository.cs` **no se modifica**. Conserva sus 5
métodos (`AddAsync`, `UpdateAsync`, `RemoveAsync`, `GetByIdAsync`,
`GetAllAsync`). No se añaden `FindOrCreateByNameAsync` ni
`FindByNamesAsync`.

**Justificación:** la interface real (verificada: 57 líneas, 5
métodos) no los declara, y esos helpers son responsabilidad del use
case consumidor (futuro, fuera de F-004). Extender la abstracción
ahora sería especular (YAGNI). El phase plan RF-11 lista exactamente
estos 5 métodos. La decisión interactiva del proposal (pregunta 1)
confirma: **diferir**.

### D-2 — `Expense.AddTag` duplicado (por `Id`) → no-op silencioso (idempotente)

**Decisión:** `AddTag(Tag tag)` no lanza si el `tag.Id` ya está en la
colección; simplemente retorna sin añadir. `RemoveTag(Tag tag)` no
lanza si el tag no está; simplemente no hace nada. Ambos lanzan
`ArgumentNullException` sólo si `tag is null` (y `AddTag` lanza
`ArgumentException` si `tag.Name` está vacío post-trim).

**Justificación:** un UseCase de asignación de tags (futuro) puede
llamar `AddTag` sobre el set seleccionado sin_trackear cuáles ya
estaban asignados; la idempotencia simplifica el caller y evita
errores ruidosos. Eco de la semántica "set" de los tags. Decisión
interactiva del proposal (pregunta 2) confirmada.

### D-3 — UNA migración `AddTagsAndExpenseTags` (no dos)

**Decisión:** una sola migración crea ambas tablas (`Tags` y
`ExpenseTags`) en su `Up`, y las elimina ambas en su `Down` (orden
inverso: `ExpenseTags` primero, luego `Tags`). El phase plan RF-10
sugería dos migraciones; se consolida en una por atomicidad del
slice.

**Justificación:** `ExpenseTags` tiene FK a `Tags`, por lo que
`Tags` debe existir antes — una migración las crea en orden correcto
y revierte en orden inverso. Dos migraciones fragmentan un cambio
atómico sin beneficio. Decisión interactiva del proposal (pregunta 3)
confirmada.

### D-4 — `Tag` promovido de `record` a `public sealed class` (espejo `Expense`/`Income`)

**Decisión:** `Tag.cs` se reescribe in-place como `public sealed class
Tag` standalone (sin base `Entity<Guid>` — no existe en el codebase,
verificado: `Expense` e `Income` son `sealed class` planas con `Guid
Id` explícito). Propiedades con `private set`, ctor privado, factory
`Create`. **NO** es `record`. **NO** espeja a `Category` (que sigue
siendo `record` con `init`); espeja a `Expense`/`Income` (entidades
con invariantes blindadas).

**Justificación:** los tags son un agregado con invariantes (Name no
vacío, longitud, Color opcional acotado), no un DTO anémico. El
patrón `sealed class` + factory + `ValidateAndNormalize` ya está
probado en `Expense`/`Income`. Corrección C-1 de F-001 P4 aplica
idéntica: `Entity<Guid>` no existe.

### D-5 — Forma exacta de `Tag` (propiedades, factory, invariantes)

**Decisión:** `Tag` expone 4 propiedades con `private set`:
`Id` (`Guid`), `Name` (`string`), `Color` (`string?`), `CreatedAt`
(`DateTime`). Sin `UpdatedAt` (los tags son etiquetas estables; no
hay auditoría de modificación en este slice). Factory `public static
Tag Create(string name, string? color, DateTime createdAt)` genera
`Id` vía `Guid.NewGuid()` y valida antes de construir. Método de
validación privado (espejo de `Income.ValidateAndNormalize`).
Invariantes:
- `Name` no null/vacío/whitespace post-trim; longitud ≤ 50.
- `Color` null OR (trim; si blank → null; si no, longitud ≤ 9). **No**
  se enforcement un patrón hex estricto — sólo longitud (lenient).
- `CreatedAt` distinto de `default(DateTime)` (debe pasarse
  explícitamente; contrato del caller de `DateTimeKind.Utc`, no
  enforcement runtime — eco de `Expense` INV-7).
- Lanza `ArgumentException` (mensajes en inglés) en cada violación.

**Justificación:** el contract AC-1 fija esta forma. El phase plan
RF-9 decía `Tag : Entity<Guid>` con `Name` + `Color?` — la base no
existe (D-4) y se añade `CreatedAt` + `Id` explícitos + factory con
validación. `Color` lenient evita regex frágil; el formato `#RRGGBB`
(lo 7) o `#RRGGBBAA` (lo 9) cabe en max 9.

### D-6 — `Expense.Tags` inicializada en AMBOS ctors privados

**Decisión:** `Expense` gana `public ICollection<Tag> Tags { get;
private set; }`. Se inicializa como `new List<Tag>()` en **ambos**
constructores privados: el existente `private Expense()` (línea 62,
para EF Core) **Y** el `private Expense(Guid, Guid, DateOnly, Money,
string, string?, DateTime, DateTime)` (línea 79, para el factory).
Sin inicialización en el ctor sin params, EF Core dejaría `Tags ==
null` al materializar y `AddTag` lanzaría `NullReferenceException`.

**Justificación:** verificado en `Expense.cs` — tiene 2 ctors
privados. Olvidar el ctor sin params es el bug clásico de
navegaciones de colección en EF Core. El contract D-6 lo explicita.

### D-7 — Tabla join `ExpenseTags`: PK compuesta + FKs `Cascade` + índice en `TagId`

**Decisión:** la tabla `ExpenseTags` tiene columnas `ExpenseId`
(`Guid`, FK → `Expenses.Id`, `OnDelete(Cascade)`) y `TagId` (`Guid`,
FK → `Tags.Id`, `OnDelete(Cascade)`), PK compuesta
`(ExpenseId, TagId)`, e índice secundario en `TagId` (para acelerar
"expenses de un tag" — base de 4-007). Mapeo vía `UsingEntity<
Dictionary<string, object>>` (join entity compartida, sin clase
C# explícita).

**Justificación:** `Cascade` en ambos lads es coherente con
hard-delete (D-8): borrar un `Expense` elimina sus filas join; borrar
un `Tag` elimina sus filas join (desasigna el tag de todos los
expenses sin tocar los expenses). El índice en `TagId` es prerrequisito
de performance de `GetByFilterAsync` con `TagIds` (4-007). El phase
plan RF-10 exigía PK compuesta y FKs; el índice secundario se añade
aquí por anticipation de 4-007 (eco de D-2 de F-002 P4: estabilidad
de schema).

### D-8 — `TagRepository` espeja `CategoryRepository` (patrón catálogo, 5 métodos)

**Decisión:** `public sealed class TagRepository : ITagRepository`,
ctor `TagRepository(AppDbContext db)`, campo `_db`. 5 métodos con el
comportamiento verificado en `CategoryRepository` (189 líneas):
- `AddAsync`: `AnyAsync(t => t.Name == tag.Name)` pre-check →
  `Result.Failure("DuplicateName", ...)`; try `Add`+`SaveChangesAsync`
  → `Result.Success(null!)`; catch `DbUpdateException` →
  `Result.Failure("DuplicateName", ...)` (race condition).
- `UpdateAsync`: `FindAsync(tag.Id)`; null → `NotFound`; sin guard
  `IsSystem` (Tag no tiene `IsSystem`);
  `Entry(existing).CurrentValues.SetValues(tag)` + `SaveChangesAsync`
  → `Success`.
- `RemoveAsync`: `Guid.Empty` → `NotFound`; `FindAsync`; null →
  `NotFound`; `Remove` + `SaveChangesAsync` → `Success` (hard delete,
  las filas join caen por `Cascade` de D-7).
- `GetByIdAsync`: `Guid.Empty` → `null`; else `FindAsync`.
- `GetAllAsync`: `AsNoTracking().OrderBy(t => t.Name).ToListAsync()`
  (alfabético por nombre, igual que `CategoryRepository`).
Sin `GetByFilterAsync` (no está en `ITagRepository`, D-1). Códigos en
inglés, mensajes en español. File-scoped namespace. XML doc en clase
+ cada método.

**Justificación:** `Tag` es un catálogo plano (como `Category`/`
PaymentMethod`), no una transacción con paginación. El patrón
catálogo (`CategoryRepository`) es el espejo correcto para el repo,
aunque la **entidad** `Tag` espeje a `Expense`/`Income` (D-4). El
`catch (DbUpdateException)` sin inspeccionar el inner `SqliteException`
es el patrón verificado en `CategoryRepository` (líneas 56-67) —
robusto y suficiente.

### D-9 — `Tag.Update(string name, string? color)` método de instancia (extensión de D-4/D-5)

**Decisión:** `Tag` gana un método `public void Update(string name,
string? color)` que revalida las invariantes de `Name`/`Color` (las
mismas de `Create`), normaliza (trim `Name`; `Color` → null si blank),
y muta los `private set` de `Name`/`Color`. No toca `Id` ni
`CreatedAt` (inmutables). No recibe timestamp (Tag no tiene
`UpdatedAt`). Lanza `ArgumentException` en violación.

**Justificación:** `ITagRepository.UpdateAsync(Tag, ct)` (D-8) hace
`FindAsync(tag.Id)` + `SetValues(tag)`, por lo que el caller debe
construir un `Tag` con el `Id` existente. Como `Tag.Create` genera un
`Id` nuevo (D-5) y `Tag` es `sealed class` con `private set` (sin
`init` ni ctor público — D-4), el caller **no puede** construir un
`Tag` con un `Id` preexistente salvo cargándolo vía `GetByIdAsync`
(tracked) y mutándolo. `Tag.Update` es ese camino de mutación: el use
case futuro hace `tag = await repo.GetByIdAsync(id); tag.Update(...);
await repo.UpdateAsync(tag);`. Sin `Tag.Update`, `UpdateAsync` sería
intestable (AC-12 `UpdateAsync_Existing_UpdatesFields` no podría
construir el input) y el contrato del repo quedaría inservible. Es la
extensión mínima y coherente con "espejo `Expense`/`Income`" (ambos
tienen `Update` — D-4). No es scope creep: no añade use cases ni UI;
sólo el método de dominio que el repo existente requiere. **No** se
añade `Tag.Reconstruct(id, ...)` (mantenería el invariant de "Create
es el único camino público de construcción").

---

## 4. Criterios de aceptación

La feature se considera completada cuando **todos** los ACs se
cumplen, verificables mediante tests automatizados y revisión de
código. Cada AC incluye escenarios Given/When/Then.

### AC-1 — Entidad `Tag` (full `sealed class`)

- [ ] `src/.../Domain/Catalog/Tag.cs` reescrito in-place (el
      `record` placeholder → entidad). Misma carpeta `Domain/Catalog/`
      junto a `Category.cs`; namespace `ControlDeFinanzasIA.Domain.Catalog`
      sin cambios.
- [ ] `public sealed class Tag` (**sin** base; **sin** `record`).
- [ ] 4 propiedades con `private set`: `Id` (`Guid`), `Name`
      (`string`), `Color` (`string?`), `CreatedAt` (`DateTime`). Sin
      `UpdatedAt`.
- [ ] 2 ctors privados: `private Tag()` (EF Core) y `private Tag(Guid
      id, string name, string? color, DateTime createdAt)` (factory).
- [ ] `public static Tag Create(string name, string? color, DateTime
      createdAt)` genera `Id` vía `Guid.NewGuid()`, valida vía método
      privado, retorna `Tag` o lanza `ArgumentException` (mensajes en
      inglés).
- [ ] Método privado de validación/normalización (espejo
      `Income.ValidateAndNormalize`): `Name` trim + no vacío + ≤50;
      `Color` trim → null si blank OR ≤9; `CreatedAt != default`.
- [ ] XML doc summary en la clase + cada propiedad + `Create`.

- **GIVEN** el proyecto compila con el nuevo `Tag.cs`.
- **WHEN** `Tag.Create("Comida", "#FF8800", utcNow)`.
- **THEN** retorna `Tag` con `Id != Guid.Empty`, `Name == "Comida"`,
  `Color == "#FF8800"`, `CreatedAt == utcNow`.

### AC-2 — `Tag.Update(string, string?)` (D-9)

- [ ] `public void Update(string name, string? color)` revalida
      `Name`/`Color`, normaliza (trim `Name`; `Color` → null si
      blank), muta `Name`/`Color`. `Id`/`CreatedAt` inmutables. Lanza
      `ArgumentException` en violación. XML doc.

- **GIVEN** un `Tag` creado (`Name="Original"`, `Color=null`).
- **WHEN** `tag.Update("Actualizado", "#00AAFF")`.
- **THEN** `tag.Name == "Actualizado"`, `tag.Color == "#00AAFF"`,
  `tag.Id`/`tag.CreatedAt` sin cambios.
- **GIVEN** `tag.Update("   ", null)`.
- **THEN** lanza `ArgumentException` (Name vacío post-trim).

### AC-3 — `Expense.Tags` + `AddTag`/`RemoveTag` (D-2, D-6)

- [ ] `src/.../Domain/Transactions/Expense.cs` gana `public
      ICollection<Tag> Tags { get; private set; }` inicializada como
      `new List<Tag>()` en **ambos** ctors privados (D-6).
- [ ] `public void AddTag(Tag tag)`: `ArgumentNullException.ThrowIfNull(tag)`;
      si `string.IsNullOrWhiteSpace(tag.Name)` → `ArgumentException`;
      si `Tags.Any(t => t.Id == tag.Id)` → **return** (no-op, D-2);
      `Tags.Add(tag)`.
- [ ] `public void RemoveTag(Tag tag)`: `ArgumentNullException.ThrowIfNull(tag)`;
      `var existing = Tags.FirstOrDefault(t => t.Id == tag.Id);` si no
      null → `Tags.Remove(existing)`; si null → no-op (D-2).
- [ ] `using ControlDeFinanzasIA.Domain.Catalog;` añadido a
      `Expense.cs` (referencia intra-Domain, permitida por R-1).
- [ ] XML doc en `Tags`, `AddTag`, `RemoveTag`.

- **GIVEN** un `Expense` recién creado.
- **WHEN** `expense.Tags` se inspecciona.
- **THEN** es una colección no-null y vacía (`Count == 0`).
- **GIVEN** `expense.AddTag(tagA)` donde `tagA.Id` no está en `Tags`.
- **WHEN** se llama.
- **THEN** `expense.Tags.Count == 1` y contiene `tagA`.
- **GIVEN** `expense.Tags` ya contiene `tagA` (mismo `Id`).
- **WHEN** `expense.AddTag(tagA)` de nuevo.
- **THEN** `Count` sigue siendo 1 (no-op idempotente).
- **GIVEN** `expense.AddTag(null!)`.
- **THEN** lanza `ArgumentNullException`.

### AC-4 — `TagConfiguration` (NEW)

- [ ] `src/.../Infrastructure/Persistence/Configurations/TagConfiguration.cs`
      existe como `public sealed class TagConfiguration :
      IEntityTypeConfiguration<Tag>`, file-scoped namespace
      `ControlDeFinanzasIA.Infrastructure.Persistence.Configurations`.
- [ ] `b.ToTable("Tags")`, `b.HasKey(t => t.Id)`,
      `b.Property(t => t.Id).ValueGeneratedNever()`,
      `b.Property(t => t.Name).IsRequired().HasMaxLength(50)`,
      `b.HasIndex(t => t.Name).IsUnique()` (índice único — crítico),
      `b.Property(t => t.Color).HasMaxLength(9)` (nullable, sin
      `IsRequired`), `b.Property(t => t.CreatedAt).IsRequired()`.
- [ ] XML doc summary en la clase + `Configure`.

- **GIVEN** el contexto se construye (arranque o test).
- **WHEN** `ApplyConfigurationsFromAssembly` descubre
  `TagConfiguration`.
- **THEN** la tabla `Tags` se mapea con `Name` único y `Color` max 9
  nullable, sin tocar `OnModelCreating`.

### AC-5 — `ExpenseConfiguration` M:N → `ExpenseTags` (D-7)

- [ ] `src/.../Infrastructure/Persistence/Configurations/ExpenseConfiguration.cs`
      se edita: tras el `OwnsOne(e => e.Amount, ...)` existente, se
      añade el mapeo M:N:

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

- [ ] `using ControlDeFinanzasIA.Domain.Catalog;` presente (ya lo está
      por `HasOne<Category>()` línea 36).
- [ ] XML doc comment sobre el bloque explicando la relación M:N.

- **GIVEN** el modelo se construye con el mapeo M:N.
- **WHEN** EF genera la migración.
- **THEN** se crea `ExpenseTags` con PK `(ExpenseId, TagId)`, ambas
  FKs `Cascade`, índice en `TagId`, y `Tag` **sin** colección inversa
  hacia `Expense` (`.WithMany()` vacío — Tag es catálogo puro).

### AC-6 — `AppDbContext.Tags` (DbSet)

- [ ] `src/.../Infrastructure/Persistence/AppDbContext.cs` gana
      `public DbSet<Tag> Tags => Set<Tag>();` (expression-bodied,
      get-only — convención del codebase, eco de `DbSet<Income>
      Incomes` línea 60) **tras** `Incomes`.
- [ ] `using ControlDeFinanzasIA.Domain.Catalog;` ya presente (línea
      1, por `Categories`/`PaymentMethods`).
- [ ] `OnModelCreating` **sin cambios** (la config se descubre vía
      `ApplyConfigurationsFromAssembly`).
- [ ] XML doc summary del DbSet + actualizar el summary de la clase
      para mencionar `Tag`.

### AC-7 — `ServiceCollectionExtensions.AddPersistence` registra `ITagRepository`

- [ ] `src/.../Infrastructure/Persistence/ServiceCollectionExtensions.cs`
      se edita: tras
      `services.AddScoped<IIncomeRepository, IncomeRepository>();`
      (línea 68), se añade
      `services.AddScoped<ITagRepository, TagRepository>();`.
- [ ] `using ControlDeFinanzasIA.Infrastructure.Persistence.Repositories;`
      ya presente (línea 2) — cubre `TagRepository`.
- [ ] XML doc del método `AddPersistence` actualizado: el summary
      lista `ITagRepository` y se añade un `<para>` referenciando
      F-004 Fase 4 (espejo del patrón de los `<para>` existentes
      líneas 27-34).

- **GIVEN** `AddPersistence` registra 4 repos hoy.
- **WHEN** se aplica el añadido.
- **THEN** registra 5 repos; `ITagRepository` se resuelve a
  `TagRepository` (scoped) desde el contenedor.

### AC-8 — Migración `AddTagsAndExpenseTags` (D-3, D-7)

- [ ] Comando ejecutado sin error:
      `dotnet ef migrations add AddTagsAndExpenseTags --project
      src/ControlDeFinanzasIA.Infrastructure --startup-project
      src/ControlDeFinanzasIA.UI` (Corrección C-5 de F-001 P4:
      `--startup-project` es `UI`).
- [ ] 3 archivos commiteados:
      `<ts>_AddTagsAndExpenseTags.cs`, `<ts>_AddTagsAndExpenseTags.Designer.cs`,
      `AppDbContextModelSnapshot.cs` (actualizado). Timestamp
      `YYYYMMDDhhmmss` asignado por la herramienta.
- [ ] `Up` crea `Tags` (`Id` Guid TEXT PK NOT NULL, `Name` TEXT
      maxLength 50 NOT NULL, `Color` TEXT maxLength 9 nullable,
      `CreatedAt` TEXT NOT NULL) + `IX_Tags_Name` único; crea
      `ExpenseTags` (`ExpenseId` Guid TEXT NOT NULL, `TagId` Guid TEXT
      NOT NULL, PK `(ExpenseId, TagId)`, FK `ExpenseId` → `Expenses.Id`
      `Cascade`, FK `TagId` → `Tags.Id` `Cascade`) +
      `IX_ExpenseTags_TagId`. Orden: `Tags` antes que `ExpenseTags`
      (FK). **Sin** `InsertData` (no seed). No toca `Categories`/
      `PaymentMethods`/`Expenses`/`Incomes` (aditiva).
- [ ] `Down`: `DropTable("ExpenseTags")` **luego**
      `DropTable("Tags")` (orden inverso por FK).
- [ ] File-scoped namespace (EF Core default; corregir IDE0161
      manualmente si el generador lo emite block-scoped — ver
      F-001 P4 §17 C-5).

### AC-9 — `TagRepository` (D-8, 5 métodos, espejo `CategoryRepository`)

- [ ] `src/.../Infrastructure/Persistence/Repositories/TagRepository.cs`
      existe como `public sealed class TagRepository : ITagRepository`,
      file-scoped namespace, ctor `TagRepository(AppDbContext db)`,
      campo `private readonly AppDbContext _db;`.
- [ ] `AddAsync`: `AnyAsync` pre-check → `DuplicateName`; try/catch
      `DbUpdateException` → `DuplicateName`; `Result.Success(null!)`.
- [ ] `UpdateAsync`: `FindAsync` → `NotFound` si null; `SetValues` +
      `SaveChangesAsync` → `Success`.
- [ ] `RemoveAsync`: `Guid.Empty`/null → `NotFound`; `Remove` +
      `SaveChangesAsync` → `Success` (hard delete; join rows caen por
      Cascade).
- [ ] `GetByIdAsync`: `Guid.Empty` → `null`; else `FindAsync`.
- [ ] `GetAllAsync`: `AsNoTracking().OrderBy(t => t.Name).ToListAsync()`.
- [ ] XML doc en clase + cada método. Códigos en inglés
      (`"DuplicateName"`, `"NotFound"`), mensajes en español.

- **GIVEN** un `Tag` con `Name` único.
- **WHEN** `repo.AddAsync(tag)`.
- **THEN** `Result.IsSuccess == true` y `GetByIdAsync` lo recupera.

### AC-10 — Unit tests: invariantes de `Tag` (NEW `TagTests.cs`)

- [ ] `tests/.../Tests.Unit/Domain/TagTests.cs` existe, namespace
      `ControlDeFinanzasIA.Tests.Unit.Domain`, pure xUnit `Assert.*`
      (sin FluentAssertions, sin mocking), helper
      `CreateValidTag(...)` (espejo `CreateValidIncome`).
- [ ] ≥7 tests cubriendo `Create`: `Create_WithValidArgs_ReturnsTag`,
      `Create_WithNullName_Throws`, `Create_WithEmptyName_Throws`,
      `Create_WithWhitespaceName_Throws`, `Create_WithNameOver50Chars_Throws`,
      `Create_WithColorOver9Chars_Throws`, `Create_WithNullColor_Succeeds`.
- [ ] ≥2 tests cubriendo `Update` (D-9):
      `Update_WithValidArgs_RefreshesNameAndColor_PreservesIdAndCreatedAt`,
      `Update_WithEmptyName_Throws` (+ opcional
      `Update_WithColorOverMaxLength_Throws`).
- [ ] Opcional: `Equality_ByReference_TwoInstancesNotEqual` (espejo
      `IncomeTests`).

### AC-11 — Unit tests: `Expense.AddTag`/`RemoveTag` (EXTEND `ExpenseTests.cs`)

- [ ] `tests/.../Tests.Unit/Domain/ExpenseTests.cs` se **extiende**
      (archivo existente) con nuevos tests; namespace sin cambios
      (`ControlDeFinanzasIA.Tests.Unit.Domain`).
- [ ] ≥7 tests: `AddTag_WithNewTag_AddsToCollection`,
      `AddTag_WithDuplicateId_IsNoOp` (D-2),
      `AddTag_WithNullTag_Throws`,
      `AddTag_WithEmptyNameTag_Throws`,
      `RemoveTag_Existing_RemovesFromCollection`,
      `RemoveTag_NotInCollection_IsNoOp` (D-2),
      `RemoveTag_WithNullTag_Throws`.
- [ ] Pure xUnit `Assert.*`. Helper `CreateTag(name, color?)` que
      llama `Tag.Create(name, color, _defaultCreatedAt)`.
- [ ] Un test verifica que un `Expense` recién creado tiene
      `Tags` no-null y vacío (D-6: inicialización en ctors).

### AC-12 — Integration tests: `TagSchemaTests` (NEW)

- [ ] `tests/.../Tests.Integration/Persistence/TagSchemaTests.cs`
      existe, namespace `ControlDeFinanzasIA.Tests.Integration.Persistence`,
      fixture `using var factory = new TestAppDbContextFactory(); using
      AppDbContext context = factory.CreateContext(); await
      context.Database.MigrateAsync();` (patrón verificado de
      `CategoryRepositoryTests`).
- [ ] ≥3 tests: `Tags_Table_HasExpectedColumns` (Id, Name, Color,
      CreatedAt), `Tags_Name_HasUniqueIndex`
      (insertar dos tags con mismo `Name` → `DbUpdateException`),
      `ExpenseTags_HasCompositePKAndBothCascadeFKs` (verificar
      restricciones vía `context.Database.SqlQuery` o intento de
      violación: insertar join row con `ExpenseId` inexistente →
      falla; PK compuesta impide duplicado `(ExpenseId, TagId)`).

### AC-13 — Integration tests: `TagRepositoryTests` (NEW)

- [ ] `tests/.../Tests.Integration/Persistence/TagRepositoryTests.cs`
      existe, pure xUnit `Assert.*`, fixture verificado.
- [ ] ≥6 tests (espejo `CategoryRepositoryTests`):
      `AddAsync_WithUniqueName_Persists`,
      `AddAsync_WithDuplicateName_ReturnsDuplicateName`,
      `UpdateAsync_Existing_UpdatesFields` (usa `tag.Update(...)` — D-9),
      `RemoveAsync_Existing_DeletesTag`,
      `GetByIdAsync_Existing_ReturnsTag`,
      `GetAllAsync_ReturnsAllOrderedByName`.
- [ ] Helper `CreateTag(name, color?)` → `Tag.Create(name, color,
      _fixedTimestamp)` con `_fixedTimestamp = new(2026,1,1,0,0,0,
      DateTimeKind.Utc)` (determinismo, eco de
      `CategoryRepositoryTests`).

### AC-14 — Integration tests: round-trip M:N (NEW `ExpenseTagManyToManyTests`)

- [ ] `tests/.../Tests.Integration/Persistence/ExpenseTagManyToManyTests.cs`
      existe, pure xUnit `Assert.*`, fixture verificado.
- [ ] ≥4 tests:
      `AddExpenseWithTags_PersistsBothAndJoinRows` (crea `Expense` +
      2 `Tag`, `expense.AddTag(t1); expense.AddTag(t2);` persiste vía
      `_db.Expenses.Add`; verificar filas en `ExpenseTags`),
      `ReloadExpenseWithTags_PopulatesTagsFromJoinTable`
      (`_db.Expenses.Include(e => e.Tags).AsNoTracking().First(...)`
      — el repo `GetByIdAsync` **no** carga Tags vía `FindAsync`, por
      lo que el test consulta el contexto con `Include` directo),
      `DeleteExpense_CascadesJoinRows` (borrar expense → filas join
      eliminadas, `Tags` intactos),
      `DeleteTag_CascadesJoinRows` (borrar tag → filas join
      eliminadas, expense intacto).
- [ ] Reutiliza categoría sembrada
      `_comidaCategoryId = new("11111111-1111-1111-1111-000000000001")`
      para construir `Expense` válido (FK `Restrict` a `Categories`).

### AC-15 — Gates de verificación

- [ ] `dotnet build -c Release --nologo` → 0 warnings, 0 errors.
- [ ] `dotnet build -c Debug --nologo` → 0 warnings, 0 errors.
- [ ] `dotnet test -c Debug --no-build --nologo` → suite completa
      verde: línea base previa + ~27-30 nuevos (≈7 Tag unit + ~9 Tag
      Update/Eq + 7 Expense.AddTag/RemoveTag + 3 schema + 6 repo + 4
      M:N).
- [ ] `dotnet test -c Debug --no-build --nologo --filter
      "FullyQualifiedName~ArchitectureTests"` → 11/11 (R-1..R-5).
- [ ] `dotnet test -c Debug --no-build --nologo --filter
      "FullyQualifiedName~Tag|FullyQualifiedName~AddTag|FullyQualifiedName~RemoveTag|FullyQualifiedName~ManyToMany"`
      → todos los tests nuevos en verde.
- [ ] `dotnet format style --verify-no-changes` → exit 0.
- [ ] `dotnet list src/ControlDeFinanzasIA.Domain package` sigue vacío
      (R-1: Domain no gana dependencias).

> Las cuentas de tests son **aproximadas** (línea base según contrato
> de lanzamiento); el gate real es "0 failures + suite previa verde +
> todos los tests nuevos verdes". El implementador (sdd-apply) reporta
> las cuentas exactas observadas.

### AC-16 — Sin scope creep

- [ ] NO modificaciones a `ITagRepository.cs` (5 métodos — D-1). NO
      `FindOrCreateByNameAsync`/`FindByNamesAsync`.
- [ ] NO `ExpenseFilter.TagIds` ni lógica de `TagIds` en
      `ExpenseRepository.GetByFilterAsync` (4-007).
- [ ] NO UI / ViewModels / XAML (4-008). NO use cases (no
      `RegisterTagUseCase` ni `AssignTagsUseCase` — futuros).
- [ ] NO modificaciones a `Income*` (`Income.cs`,
      `IncomeConfiguration.cs`, `IncomeRepository.cs`,
      `IIncomeRepository.cs`, `IncomeFilter.cs`, use cases de Income).
- [ ] NO modificaciones a `Money.cs`, `Currency.cs`, `Result.cs`,
      `Error.cs`, `PagedResult.cs`, `Category.cs`,
      `CategoryConfiguration.cs`, `CategoryRepository.cs`.
- [ ] NO segunda migración (sólo `AddTagsAndExpenseTags`). NO
      `Tag.Reconstruct`. NO `Entity<Guid>`. NO paquetes NuGet nuevos.
      NO FluentAssertions, NO mocking library.

- **GIVEN** el diff de F-004.
- **WHEN** se inspecciona contra la lista de archivos in-scope (§5).
- **THEN** no hay archivos fuera de la lista modificados; no hay
  referencias a 4-007/4-008 en el código de producción.

---

## 5. Áreas afectadas

| Archivo | Acción | Descripción |
| --- | --- | --- |
| `src/ControlDeFinanzasIA.Domain/Catalog/Tag.cs` | **Reescribir in-place** | `record` placeholder → `public sealed class Tag` con factory `Create`, `Update` (D-9), 2 ctors privados, 4 props `private set`, invariantes. |
| `src/ControlDeFinanzasIA.Domain/Transactions/Expense.cs` | **Modified** | + `ICollection<Tag> Tags` (init en ambos ctors, D-6) + `AddTag`/`RemoveTag` (D-2). + `using Domain.Catalog`. |
| `src/ControlDeFinanzasIA.Infrastructure/Persistence/Configurations/TagConfiguration.cs` | **New** | `IEntityTypeConfiguration<Tag>`: tabla `Tags`, índice único `Name`, `Color` max 9 nullable (AC-4). |
| `src/ControlDeFinanzasIA.Infrastructure/Persistence/Configurations/ExpenseConfiguration.cs` | **Modified** | + mapeo M:N `ExpenseTags` vía `UsingEntity` (AC-5, D-7). |
| `src/ControlDeFinanzasIA.Infrastructure/Persistence/AppDbContext.cs` | **Modified** | + `DbSet<Tag> Tags => Set<Tag>();` (AC-6). `OnModelCreating` sin cambios. |
| `src/ControlDeFinanzasIA.Infrastructure/Persistence/ServiceCollectionExtensions.cs` | **Modified** | + `AddScoped<ITagRepository, TagRepository>();` + XML doc (AC-7). |
| `src/ControlDeFinanzasIA.Infrastructure/Persistence/Repositories/TagRepository.cs` | **New** | `sealed class : ITagRepository`, 5 métodos, espejo `CategoryRepository` (AC-9, D-8). |
| `src/ControlDeFinanzasIA.Infrastructure/Persistence/Migrations/<ts>_AddTagsAndExpenseTags.cs` | **New (generado)** | `Up` crea `Tags` + `ExpenseTags` + índices + FKs; `Down` las borra en orden inverso (AC-8). |
| `src/ControlDeFinanzasIA.Infrastructure/Persistence/Migrations/<ts>_AddTagsAndExpenseTags.Designer.cs` | **New (generado)** | Metadata. |
| `src/ControlDeFinanzasIA.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs` | **Edit (generado)** | + `Tags` + `ExpenseTags`. |
| `tests/ControlDeFinanzasIA.Tests.Unit/Domain/TagTests.cs` | **New** | ≥9 tests pure xUnit `Assert.*` (Create + Update + Eq). |
| `tests/ControlDeFinanzasIA.Tests.Unit/Domain/ExpenseTests.cs` | **Modified (extend)** | + 7 tests `AddTag`/`RemoveTag`. |
| `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/TagSchemaTests.cs` | **New** | ≥3 tests de forma del schema. |
| `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/TagRepositoryTests.cs` | **New** | ≥6 tests CRUD (espejo `CategoryRepositoryTests`). |
| `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/ExpenseTagManyToManyTests.cs` | **New** | ≥4 tests round-trip M:N + cascade. |

**Archivos NO modificados (verificados presentes/correctos):**
`ITagRepository.cs` (5 métodos — D-1), `Income.cs`,
`IncomeConfiguration.cs`, `IncomeRepository.cs`, `IIncomeRepository.cs`,
`IncomeFilter.cs`, use cases de Income, `Money.cs`, `Currency.cs`,
`Result.cs`, `Error.cs`, `PagedResult.cs`, `Category.cs`,
`CategoryConfiguration.cs`, `CategoryRepository.cs`,
`ExpenseRepository.cs`, `IExpenseRepository.cs`, `ExpenseFilter.cs`,
`PaymentMethod*.cs`, `SeedData.cs` (no se siembran tags), cualquier
archivo de UI/ViewModel/XAML, migraciones previas, `.csproj` (sin
paquetes nuevos), `Directory.Packages.props`, `Directory.Build.props`.

**Modificaciones a `.csproj`:** ninguna.

---

## 6. Plan de tests

### 6.1 Unit tests — `Tests.Unit/Domain/TagTests.cs`

Pure xUnit `Assert.*` (sin FluentAssertions, sin mocking). Helper
`CreateValidTag(...)` (espejo `CreateValidIncome` de `IncomeTests.cs`):

```csharp
private static readonly DateTime _defaultCreatedAt = new(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);

private static Tag CreateValidTag(string? name = null, string? color = null, DateTime? createdAt = null)
    => Tag.Create(name ?? "Trabajo", color, createdAt ?? _defaultCreatedAt);
```

| # | Test | GIVEN / WHEN / THEN |
| --- | --- | --- |
| T-1 | `Create_WithValidArgs_ReturnsTag` | **GIVEN** name `"Trabajo"`, color `"#FF8800"`. **WHEN** `Tag.Create(...)`. **THEN** `Id != Guid.Empty`, `Name == "Trabajo"`, `Color == "#FF8800"`, `CreatedAt == _defaultCreatedAt`. |
| T-2 | `Create_WithNullName_Throws` | **WHEN** `Tag.Create(null!, ...)`. **THEN** `ArgumentNullException` (ParamName `"name"`). |
| T-3 | `Create_WithEmptyName_Throws` | **WHEN** `Tag.Create("", ...)`. **THEN** `ArgumentException` con `"Name"`. |
| T-4 | `Create_WithWhitespaceName_Throws` | **WHEN** `Tag.Create("   ", ...)`. **THEN** `ArgumentException` (post-trim vacío). |
| T-5 | `Create_WithNameOver50Chars_Throws` | **WHEN** `Tag.Create(new string('a', 51), ...)`. **THEN** `ArgumentException` con `"50"`. |
| T-6 | `Create_WithColorOver9Chars_Throws` | **WHEN** `Tag.Create("X", new string('a', 10), ...)`. **THEN** `ArgumentException` con `"Color"`/`"9"`. |
| T-7 | `Create_WithNullColor_Succeeds` | **WHEN** `Tag.Create("X", null, ...)`. **THEN** `tag.Color == null`. |
| T-8 | `Create_WithWhitespaceColor_NormalizesToNull` | **WHEN** `Tag.Create("X", "   ", ...)`. **THEN** `tag.Color == null`. |
| T-9 | `Update_WithValidArgs_RefreshesNameAndColor_PreservesIdAndCreatedAt` | **GIVEN** tag creado. **WHEN** `tag.Update("Nuevo", "#00AAFF")`. **THEN** `Name`/`Color` actualizados; `Id`/`CreatedAt` inmutables. |
| T-10 | `Update_WithEmptyName_Throws` | **WHEN** `tag.Update("   ", null)`. **THEN** `ArgumentException`. |
| T-11 | `Update_WithColorOverMaxLength_Throws` | **WHEN** `tag.Update("X", new string('a', 10))`. **THEN** `ArgumentException`. |
| T-12 | `Equality_ByReference_TwoInstancesNotEqual` | **GIVEN** dos `Tag.Create("X", null, ts)`. **THEN** `Assert.NotEqual(a, b)` (sealed class, igualdad por referencia). |

### 6.2 Unit tests — `Tests.Unit/Domain/ExpenseTests.cs` (extensión)

Añadir al archivo existente (class `ExpenseTests`, namespace
`ControlDeFinanzasIA.Tests.Unit.Domain`). Helper local `CreateTag`:

```csharp
private static Tag CreateTag(string name, string? color = null)
    => Tag.Create(name, color, _defaultCreatedAt);
```

| # | Test | GIVEN / WHEN / THEN |
| --- | --- | --- |
| ET-1 | `NewExpense_HasEmptyNotNullTagsCollection` | **GIVEN** `Expense.Create(...)`. **THEN** `Tags != null`, `Tags.Count == 0` (D-6). |
| ET-2 | `AddTag_WithNewTag_AddsToCollection` | **GIVEN** expense + tagA. **WHEN** `expense.AddTag(tagA)`. **THEN** `Tags.Count == 1`, contiene tagA. |
| ET-3 | `AddTag_WithDuplicateId_IsNoOp` | **GIVEN** expense con tagA. **WHEN** `expense.AddTag(tagA)` (mismo Id). **THEN** `Count == 1` (D-2). |
| ET-4 | `AddTag_WithNullTag_Throws` | **WHEN** `expense.AddTag(null!)`. **THEN** `ArgumentNullException`. |
| ET-5 | `AddTag_WithEmptyNameTag_Throws` | **GIVEN** tag con `Name == ""` (no se puede vía `Tag.Create` que valida — usar `Tag.Create` válido y nota, o validar que el guard de `AddTag` atrapa whitespace). **THEN** `ArgumentException`. |
| ET-6 | `RemoveTag_Existing_RemovesFromCollection` | **GIVEN** expense con tagA+tagB. **WHEN** `expense.RemoveTag(tagA)`. **THEN** `Count == 1`, queda tagB. |
| ET-7 | `RemoveTag_NotInCollection_IsNoOp` | **GIVEN** expense con tagA. **WHEN** `expense.RemoveTag(tagB)` (no presente). **THEN** `Count == 1` (D-2). |
| ET-8 | `RemoveTag_WithNullTag_Throws` | **WHEN** `expense.RemoveTag(null!)`. **THEN** `ArgumentNullException`. |

> **Nota ET-5:** `Tag.Create` rechaza `Name` vacío, por lo que no es
> posible construir un `Tag` inválido vía el factory público. El test
> ET-5 verifica el **guard defensivo** de `AddTag` (que re-chequea
> `IsNullOrWhiteSpace(tag.Name)`) usando un tag válido pero
> opcionalmente puede omitirse si el implementador considera que el
> invariant de `Tag.Create` ya lo cubre. Se deja a discreción del
> implementador; el mínimo es ET-1..ET-4, ET-6..ET-8 (7 tests).

### 6.3 Integration tests — `TagSchemaTests.cs`

Fixture verificado (`TestAppDbContextFactory` + `CreateContext()` +
`MigrateAsync()`, NO `CreateAsync()` que no existe — ver F-002 P4 §6).

| # | Test | Verifica |
| --- | --- | --- |
| ST-1 | `Tags_Table_HasExpectedColumns` | Tras `MigrateAsync`, `Tags` existe con `Id`, `Name`, `Color`, `CreatedAt` (4 columnas). |
| ST-2 | `Tags_Name_HasUniqueIndex` | Insertar dos `Tag` con mismo `Name` → `DbUpdateException` (índice único `IX_Tags_Name`). |
| ST-3 | `ExpenseTags_HasCompositePKAndBothCascadeFKs` | Insertar join row con `TagId` inexistente → falla (FK); insertar `(ExpenseId, TagId)` duplicado → falla (PK compuesta); borrar `Tag` con join rows → filas join eliminadas (Cascade). |

### 6.4 Integration tests — `TagRepositoryTests.cs`

Espejo `CategoryRepositoryTests.cs` (280 líneas). Helper
`CreateTag(name, color?)` → `Tag.Create(name, color, _fixedTimestamp)`.

| # | Test | GIVEN / WHEN / THEN |
| --- | --- | --- |
| RT-1 | `AddAsync_WithUniqueName_Persists` | **GIVEN** `Tag.Create("Custom", null, ts)`. **WHEN** `repo.AddAsync(tag)`. **THEN** `IsSuccess` true; `GetByIdAsync` recupera con `Name`/`Color`/`CreatedAt` preservados. |
| RT-2 | `AddAsync_WithDuplicateName_ReturnsDuplicateName` | **GIVEN** `Tag.Create("Comida", ...)` (o un tag ya persistido). **WHEN** `repo.AddAsync(otro con mismo Name)`. **THEN** `IsSuccess` false; `Error.Code == "DuplicateName"`. |
| RT-3 | `UpdateAsync_Existing_UpdatesFields` | **GIVEN** tag persistido. **WHEN** `tag.Update("Actualizado", "#FF0000")` + `repo.UpdateAsync(tag)`. **THEN** `IsSuccess` true; `GetByIdAsync` refleja `Name=="Actualizado"`, `Color=="#FF0000"`; `CreatedAt` inmutable. |
| RT-4 | `UpdateAsync_Missing_ReturnsNotFound` | **GIVEN** tag con `Id` inexistente (vía `Create` + no-persistir, o `Guid.NewGuid()` construyendo — nota: requiere `Tag.Update` para tener instancia con Id; o usar `Tag.Create` cuyo Id no existe en DB). **WHEN** `repo.UpdateAsync(tag)`. **THEN** `Error.Code == "NotFound"`. |
| RT-5 | `RemoveAsync_Existing_DeletesTag` | **GIVEN** tag persistido. **WHEN** `repo.RemoveAsync(tag.Id)`. **THEN** `IsSuccess` true; `GetByIdAsync(id) == null` (hard delete). |
| RT-6 | `RemoveAsync_Missing_ReturnsNotFound` | **WHEN** `repo.RemoveAsync(Guid.NewGuid())`. **THEN** `Error.Code == "NotFound"`. |
| RT-7 | `RemoveAsync_WithEmptyGuid_ReturnsNotFound` | **WHEN** `repo.RemoveAsync(Guid.Empty)`. **THEN** `Error.Code == "NotFound"`. |
| RT-8 | `GetByIdAsync_Existing_ReturnsTag` | **GIVEN** tag persistido. **WHEN** `repo.GetByIdAsync(tag.Id)`. **THEN** devuelve el `Tag` con `Name` correcto. |
| RT-9 | `GetByIdAsync_Missing_ReturnsNull` | **WHEN** `repo.GetByIdAsync(Guid.NewGuid())`. **THEN** `null`. |
| RT-10 | `GetByIdAsync_WithEmptyGuid_ReturnsNull` | **WHEN** `repo.GetByIdAsync(Guid.Empty)`. **THEN** `null` (guard, sin tocar DB). |
| RT-11 | `GetAllAsync_ReturnsAllOrderedByName` | **GIVEN** 3 tags `"Zebra"`, `"Apple"`, `"Mango"`. **WHEN** `repo.GetAllAsync()`. **THEN** orden `["Apple", "Mango", "Zebra"]` (alfabético). |

> **Nota RT-4:** `Tag.Create` genera un `Id` nuevo. Para testear
> `NotFound`, basta con persistir ningún tag con ese `Id` — el
> `FindAsync(tag.Id)` retorna null → `NotFound`. No se necesita
> `Reconstruct` (D-9 lo excluye deliberadamente).

### 6.5 Integration tests — `ExpenseTagManyToManyTests.cs`

Verifica el mapeo M:N end-to-end. **Importante:** `ExpenseRepository`
no se usa aquí (no carga `Tags` vía `FindAsync`); los tests consultan
`AppDbContext` directamente. Reutiliza
`_comidaCategoryId = new("11111111-1111-1111-1111-000000000001")`
(sembrada) para construir `Expense` válidos.

| # | Test | GIVEN / WHEN / THEN |
| --- | --- | --- |
| MT-1 | `AddExpenseWithTags_PersistsBothAndJoinRows` | **GIVEN** `Expense.Create(_comidaCategoryId, date, Money.Create(100m, DOP), "Pizza", null, ts)` + 2 `Tag` persistidos. **WHEN** `expense.AddTag(t1); expense.AddTag(t2); _db.Expenses.Add(expense); await _db.SaveChangesAsync();`. **THEN** `Expense` persistido; consultar `ExpenseTags` vía `_db.Database.SqlQueryRaw` o recargar con `Include` muestra 2 filas join `(expense.Id, t1.Id)` y `(expense.Id, t2.Id)`. |
| MT-2 | `ReloadExpenseWithTags_PopulatesTagsFromJoinTable` | **GIVEN** expense con 2 tags persistido (MT-1 setup) + **contexto nuevo** (para evitar cache de change tracker). **WHEN** `var reloaded = await _db.Expenses.Include(e => e.Tags).AsNoTracking().FirstAsync(e => e.Id == expense.Id);`. **THEN** `reloaded.Tags.Count == 2` con los `Name` correctos (valida que el mapeo `UsingEntity` hidrata la navegación). |
| MT-3 | `DeleteExpense_CascadesJoinRows` | **GIVEN** expense con 2 tags persistido. **WHEN** `_db.Expenses.Remove(expense); await _db.SaveChangesAsync();`. **THEN** filas join de ese expense eliminadas (consultar `ExpenseTags`); los 2 `Tag` permanecen intactos (cascade borra join, no el tag). |
| MT-4 | `DeleteTag_CascadesJoinRows` | **GIVEN** expense con tagA+tagB persistido. **WHEN** `_db.Tags.Remove(tagA); await _db.SaveChangesAsync();`. **THEN** fila join `(expense.Id, tagA.Id)` eliminada; `tagB` y su join intactos; `expense` persistido intacto (sólo pierde tagA al recargar con `Include`). |

**Marco y paquetes:** xUnit, `Microsoft.Data.Sqlite`, EF Core SQLite
(ya en `Tests.Integration.csproj` desde Fase 2). **Sin paquetes
nuevos**; **sin FluentAssertions**; **sin mocking**. Aserciones con
`Assert.*` (`Assert.True`, `Assert.False`, `Assert.Equal`,
`Assert.Null`, `Assert.NotNull`, `Assert.Single`, `Assert.Contains`,
`Assert.Throws`).

**Cobertura:** happy paths (T-1, RT-1, MT-1, MT-2), edge cases (T-7
null color, ET-3/ET-7 no-op, RT-7/RT-10 empty guid), error states
(T-2..T-6 invariantes, RT-2 DuplicateName, RT-4/RT-6 NotFound, MT-3/MT-4
cascade).

---

## 7. Fuera de alcance

F-004 **no** hace lo siguiente (cada ítem vive en otra feature):

- **`ExpenseFilter.TagIds`** + lógica de `TagIds` en
  `ExpenseRepository.GetByFilterAsync` (4-007).
- **UI de asignación de tags** (`ExpensesView` multi-select, tag
  picker) + ViewModel (4-008).
- **Use cases de tags** (`RegisterTagUseCase`, `AssignTagsUseCase`,
  `UpdateTagUseCase`, `DeleteTagUseCase`) — futuros (post-4-008).
- **`FindOrCreateByNameAsync` / `FindByNamesAsync`** en
  `ITagRepository` (D-1 — diferidos al use case consumidor).
- **`Income↔Tag`** M:N (los tags se comparten, pero la tabla join de
  Income se introduce cuando Income gane `Tags` — fuera de F-004).
- **Modificaciones a `ITagRepository.cs`** (5 métodos — D-1), a
  `Income*`, a `Money`/`Currency`/`Result`/`Error`/`PagedResult`, a
  `Category*`, a `ExpenseRepository.cs`/`IExpenseRepository.cs`/
  `ExpenseFilter.cs`.
- **`Tag.Reconstruct`** (D-9 lo excluye — `Update` es el camino de
  mutación).
- **Seed data de tags** en `SeedData.cs` (los tags son creados por el
  usuario, no del sistema). `InsertData` en la migración.
- **Soft-delete** / `IsDeleted` / query filters (hard delete, eco de
  `Expense`/`Income`/`Category`).
- **Validación estricta de hex en `Color`** (D-5: lenient, sólo
  longitud).
- **Nuevas migraciones** además de `AddTagsAndExpenseTags`.
- **Paquetes NuGet nuevos** (en particular, no FluentAssertions, no
  mocking). **Modificaciones a `Bootstrap.ConfigureServices`** (F-004
  sólo extiende `AddPersistence`). **CI workflow**,
  **`Directory.Packages.props`**, **`Directory.Build.props`**: sin
  cambios.

---

## 8. Plan de rollback

Cambio mayormente **aditivo** (entidad reescrita + nuevos archivos +
una migración + edición de config/DI). Revertir en orden:

1. **Revertir la migración** (si se aplicó a una DB de usuario):
   `dotnet ef database update <previo> --project
   src/ControlDeFinanzasIA.Infrastructure --startup-project
   src/ControlDeFinanzasIA.UI`, luego `dotnet ef migrations remove
   AddTagsAndExpenseTags` (elimina los 3 archivos generados). Esto
   dropea `ExpenseTags` y `Tags` (los datos de tags del usuario se
   pierden — aceptable en un rollback; no hay datos del sistema en
   `Tags`).
2. **Eliminar** los archivos nuevos: `TagRepository.cs`,
   `TagConfiguration.cs`, `TagTests.cs`, `TagSchemaTests.cs`,
   `TagRepositoryTests.cs`, `ExpenseTagManyToManyTests.cs`.
3. **Revertir** `Tag.cs` al `record` placeholder original (11 líneas:
   `public sealed record Tag { Id; Name; }`) — restaura la
   compilación de `ITagRepository`.
4. **Revertir** `Expense.cs` (quitar `Tags`, `AddTag`, `RemoveTag`,
   `using Domain.Catalog`), `ExpenseConfiguration.cs` (quitar bloque
   M:N), `AppDbContext.cs` (quitar `DbSet<Tag> Tags`),
   `ServiceCollectionExtensions.cs` (quitar `AddScoped<ITagRepository,
   TagRepository>` + restaurar XML doc), `ExpenseTests.cs` (quitar
   tests `AddTag`/`RemoveTag`).
5. **Verificar** `dotnet build` limpio y `dotnet test` suite previa
   verde. NetArchTest R-1..R-5 no se afectan: `TagRepository` en
   Infrastructure (depende de Application+Domain, R-3 OK);
   `Tag`/`Expense.Tags` en Domain (sin dependencias externas, R-1 OK);
   `TagConfiguration`/`ExpenseConfiguration` en Infrastructure (R-3
   OK).

---

## 9. Riesgos y mitigaciones

| # | Riesgo | Prob. | Impacto | Mitigación |
| --- | --- | --- | --- | --- |
| R-1 | `Tag.Update` no estaba en el contract AC original; el implementador lo omite y `UpdateAsync` queda intestable (AC-12). | Media | Alto | **D-9** documenta la necesidad y el flujo (load → Update → UpdateAsync). AC-2 + AC-10 lo fijan. RT-3 lo blinda. |
| R-2 | `Expense.Tags` no se inicializa en el ctor sin params de `Expense`; EF Core la deja null al materializar → NRE en `AddTag`. | Media | Alto | **D-6** + AC-3 exigen inicialización en **ambos** ctors. ET-1 (`NewExpense_HasEmptyNotNullTagsCollection`) blinda. |
| R-3 | El mapeo M:N `UsingEntity<Dictionary<string,object>>` genera una migración con FKs `Restrict` en lugar de `Cascade` (default de EF Core para skip navigations puede variar). | Media | Alto | AC-5 especifica `.OnDelete(DeleteBehavior.Cascade)` en **ambos** lados. AC-8 + ST-3 + MT-3/MT-4 verifican el cascade real. Si EF generara `Restrict`, forzar `Cascade` explícito (ya en el fluent). |
| R-4 | `Tag` reescrito de `record` a `sealed class` rompe referencias existentes a `Tag.Id`/`Tag.Name` con `init`. | Baja | Medio | Verificado: sólo `ITagRepository` referencia `Tag` (no hay use cases/DTOs que la usen aún). Los `private set` exponen `Id`/`Name` como getters públicos — la lectura (`tag.Id`, `tag.Name`) sigue compilando. La escritura vía `init` se perdía, pero no hay callers que la usen (sólo `SeedData` usa `init` en `Category`, no en `Tag`). |
| R-5 | `DuplicateName` case-sensitivity: SQLite default es case-sensitive para TEXT; `"comida"` vs `"Comida"` podrían coexistir (vs `CategoryConfiguration` que usa `.UseCollation("NOCASE")`). | Media | Bajo | D-8 espeja `CategoryRepository` (que usa `AnyAsync(c => c.Name == c.Name)` — case-sensitive en C#, pero la DB tiene `NOCASE`). `TagConfiguration` **no** añade `UseCollation("NOCASE")` (AC-4 no lo lista) para mantener fidelidad al contract; el `AnyAsync` pre-check es case-sensitive en C#. **Decisión:** aceptar case-sensitive (un tag "Comida" y "comida" son distintos) — es coherente con "tags libres del usuario". Documentado; si producto quiere case-insensitive, añadir `.UseCollation("NOCASE")` en `TagConfiguration` + ajustar el `AnyAsync`. |
| R-6 | Engram `mem_save` no disponible en el toolset del sub-agente. | Alta (verificado) | Bajo | Respaldo en filesystem: `.atl/sdd/f-004p4-tag-entity-m-n/proposal.md` ya existe; esta spec en `spec/features/phase-04/F-004-tag-entity-and-many-to-many.spec.md`. El orquestador persiste a Engram con topic keys `sdd/f-004p4-tag-entity-m-n/{proposal,spec}`. El sub-agente NO fabrica observation IDs. |
| R-7 | El implementador consulta `ExpenseRepository.GetByIdAsync` para el round-trip M:N y obtiene `Tags == null`/vacío (no hay `Include`). | Media | Medio | AC-14 + MT-2 aclaran: el repo **no** carga Tags; los tests consultan `_db.Expenses.Include(e => e.Tags)` directo. 4-007/4-008 decidirán si el repo gana un método "con tags". |
| R-8 | La migración generada por `dotnet ef` no respeta el file-scoped namespace (IDE0161) o el orden de `Down`. | Baja | Bajo | AC-8 exige `Down` en orden inverso (`ExpenseTags` luego `Tags`) y file-scoped namespace (corregir manualmente si el generador emite block-scoped — ver F-001 P4 §17 C-5). |

---

## 10. Referencias

- **Propuesta (filesystem):**
  `.atl/sdd/f-004p4-tag-entity-m-n/proposal.md` (67 líneas) — PRD de
  origen. Sus 3 preguntas interactivas (5 métodos, no-op duplicado,
  una migración) fueron consolidadas en D-1/D-2/D-3.
- **Propuesta (Engram objetivo):** topic key
  `sdd/f-004p4-tag-entity-m-n/proposal` (pendiente de persistencia
  por el orquestador — ver R-6).
- **Spec (Engram objetivo):** topic key
  `sdd/f-004p4-tag-entity-m-n/spec` (pendiente de persistencia por el
  orquestador — ver R-6).
- **F-001 Fase 4 (archivada — patrón espejo entidad/config/migración):**
  `spec/features/phase-04/F-001-income-entity-config-migration.done.md`
  (767 líneas) — `sealed class` sin base, factory `Create`,
  `ValidateAndNormalize`, `Configuration` con `OwnsOne`,
  `AddIncomesTable`, `DbSet<T>` expression-bodied, Correcciones C-1/C-4/C-5.
- **F-002 Fase 4 (archivada — patrón espejo repositorio/tests):**
  `spec/features/phase-04/F-002-income-repository.done.md` (728
  líneas) — fixture `TestAppDbContextFactory` verificado,
  `SetValues`, `Result.Success(null!)`, códigos inglés/mensajes
  español, `AsNoTracking`, patrón de tests de integración.
- **F-001 Fase 3 (archivada — patrón `Expense` entity):**
  `spec/features/phase-03/F-001-expense-domain-entity.done.md` —
  `sealed class` con `Create`/`Update`/`ValidateAndNormalize`, 8
  invariantes, igualdad por referencia.
- **F-004 Fase 2 (archivada — patrón `CategoryRepository` catálogo):**
  `spec/features/phase-02/F-004-category-paymentmethod-repositories.done.md`
  — `AnyAsync` pre-check + `catch (DbUpdateException)`, `DuplicateName`,
  `NotFound`, `GetByIdAsync` guard `Guid.Empty`, `GetAllAsync`
  `OrderBy(Name)`.
- **Phase plan:** `spec/phases/phase-04/features.md` §RF-9 (línea 184,
  Tag entity), §RF-10 (línea 190, M:N), §RF-11 (línea 195,
  `ITagRepository`/`TagRepository` 5 métodos).
- **Convención de contrato:** `spec/features/phase-01/F-006-contrato-interfaces-repo.done.md`
  (commands `Task<Result>` vs queries `Task<T>`).
- **Tipos comunes:** `spec/features/phase-01/F-005-comuntypes-result-pagedresult.done.md`
  (`Result` non-generic, `Value: object?`, `Result.Success(null!)`).
- **Test infra:** `spec/features/phase-02/F-006-integration-tests.done.md`
  (`TestAppDbContextFactory`: `IDisposable` + `CreateContext()` sync,
  NO `CreateAsync()`).
- **Arquitectura:** `docs/architecture.md` §4 (R-1..R-5), §6 (capa
  dominio), §8 (Fluent API en Infrastructure, migraciones versionadas).
- **Constitución:** `spec/constitution/mission.md`,
  `spec/constitution/tech-stack.md` (async/await, errores como
  valores, MVVM estricto, sin mocking, pure xUnit).
- **Código verificado (fuentes de la spec):**
  `src/.../Domain/Catalog/Tag.cs` (11 líneas — `record` placeholder a
  promover), `src/.../Application/Abstractions/ITagRepository.cs` (57
  líneas — 5 métodos, confirma D-1),
  `src/.../Domain/Transactions/Expense.cs` (239 líneas — 2 ctors
  privados, confirma D-6), `src/.../Domain/Catalog/Category.cs` (16
  líneas — `record`, contraste con D-4),
  `src/.../Repositories/CategoryRepository.cs` (189 líneas — espejo
  del repo, D-8), `src/.../Configurations/CategoryConfiguration.cs`
  (32 líneas — espejo config catálogo),
  `src/.../Configurations/ExpenseConfiguration.cs` (56 líneas — donde
  añadir M:N), `src/.../Configurations/IncomeConfiguration.cs` (57
  líneas — patrón `OwnsOne`),
  `src/.../Persistence/AppDbContext.cs` (72 líneas — `DbSet<Income>`
  línea 60, patrón expression-bodied),
  `src/.../Persistence/ServiceCollectionExtensions.cs` (72 líneas —
  registro DI línea 68), `src/.../Repositories/ExpenseRepository.cs`
  (212 líneas — `FindAsync` sin `Include`, justifica MT-2),
  `src/.../Persistence/SeedData.cs` (60 líneas — 9 categorías
  sembradas, GUIDs `11111111-...`),
  `src/.../Migrations/20260826000511_AddIncomesTable.cs` (57 líneas —
  espejo de `Up`/`Down`), `src/.../Common/Result.cs` (47 líneas —
  `Result.Success(null!)`),
  `tests/.../Domain/IncomeTests.cs` (285 líneas — espejo unit tests
  entidad), `tests/.../Domain/ExpenseTests.cs` (274 líneas — archivo
  a extender), `tests/.../Persistence/CategoryRepositoryTests.cs`
  (280 líneas — espejo repo tests),
  `tests/.../Persistence/TestAppDbContextFactory.cs` (49 líneas — API
  real `CreateContext()`).
- `AGENTS.md` — convención de migraciones (`--startup-project UI`),
  NetArchTest R-1..R-5, filesystem specs (no OpenSpec), conventional
  commits.
- `spec/README.md` — convención `.spec.md` → `.done.md`.
