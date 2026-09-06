# Specification — F-007: Extensión de filtros con `TagIds` + enabler M:N `Income↔Tag` (Opción A)

> **Fase:** 4 — Transacciones: ingresos, edición, borrado, tags, filtros, vistas guardadas
> **Feature:** F-007 (local a esta fase)
> **Spec ID:** SPEC-F007P4
> **change_id:** `f-007p4-extend-filters-tagids`
> **Modo:** Standard (SDD: propose → spec → tasks → apply → verify → archive)
> **Estado:** Pendiente de implementación (renombrar a `.done.md` tras apply + verify)
>
> **Nota de numeración:** este `F-007` es el de la **Fase 4**
> (`spec/phases/phase-04/features.md`, §4.1 líneas 45-48, §RF-18, §RF-20).
> Los IDs son locales a cada fase; el folder los desambigua. Ver
> `spec/README.md`.
>
> Fuentes de verdad: `docs/vision.md` (§4.2 "cada **movimiento** lleva
> Tags libres"; §4.3 filtros por tags), `docs/architecture.md`,
> `docs/roadmap.md` (§Fase 4), `spec/constitution/tech-stack.md`,
> `spec/constitution/mission.md`,
> `spec/phases/phase-04/features.md` (§RF-18 línea 236, §RF-20 línea
> 250, líneas 47-48 "los mismos filtros"),
> `.atl/sdd/f-007p4-extend-filters-tagids/proposal.md` (PRD de origen
> — 196 líneas; D-0/Opción A ratificada en la ronda interactiva),
> `spec/features/phase-04/F-002-income-repository.done.md` (patrón
> espejo: `IncomeFilter` 6 props + `IncomeRepository` + T-12 + fixture
> de tests),
> `spec/features/phase-04/F-004-tag-entity-and-many-to-many.done.md`
> (patrón espejo: M:N `UsingEntity` + `AddTag`/`RemoveTag` +
> `ExpenseTagManyToManyTests` + migración join),
> `spec/features/phase-03/F-003-expense-repository.done.md`
> (`ExpenseRepository.GetByFilterAsync` — contrato base del filtro).

---

## 1. Metadata

| Campo | Valor |
| --- | --- |
| **change_id** | `f-007p4-extend-filters-tagids` |
| **spec_id** | `SPEC-F007P4` |
| **modo** | Standard (SDD: propose → spec → tasks → apply → verify → archive) |
| **fase** | 4 |
| **feature** | F-007 (local a Fase 4) |
| **dependencies (build)** | F-004 Fase 4 (`Tag` + `ExpenseTags` M:N + `Expense.AddTag`/`RemoveTag` + índice `IX_ExpenseTags_TagId`) ✅ archivada; F-002 Fase 4 (`IncomeFilter` 6 props con `TagIds` placeholder + `IncomeRepository` + T-12) ✅ archivada; Phase 3 F-003 (`ExpenseRepository.GetByFilterAsync` con `CategoryId`/`DateFrom`/`DateTo`) ✅ archivada |
| **dependencies (test infra)** | F-006 Fase 2 (`TestAppDbContextFactory`, SQLite in-memory + `MigrateAsync` por test — la migración nueva se aplica sola) ✅; F-003 Fase 2 (seed de categorías con GUIDs `11111111-1111-1111-1111-00000000000X`) ✅ |
| **dependencies (contrato)** | F-006 Fase 1 (`IExpenseRepository`/`IIncomeRepository` — `GetByFilterAsync` ya existe en ambas, línea 70 de cada interface → **cero cambio de interface**) ✅; F-005 Fase 1 (`Result` non-generic, `Value: object?`, `PagedResult<T>`) ✅ |
| **unblocks** | F-006 Fase 4 (`SavedView` — `ApplySavedView` de ingresos con tags dejaría de ser no-op silencioso), F-008 Fase 4 (UI de filtros + `GetIncomesQuery`) |
| **artifact_store** | Engram (objetivo: `sdd/f-007p4-extend-filters-tagids/spec` + `sdd/f-007p4-extend-filters-tagids/proposal`) + filesystem (este archivo) |
| **status** | Pendiente de implementación |
| **línea base de tests (pre-F-007)** | Suite post 4-005 (aproximada; el implementador reporta las cuentas exactas observadas). Incluye 1 fallo **pre-existing conocido de `Money`** (ver AC-13) |
| **D-0 ratificada** | **Opción A** (enabler `Income↔Tag` dentro de F-007) — ratificada en la ronda interactiva del proposal; ver §3 D-0 |

---

## 2. Contexto y objetivo

Phase 4 ya entregó el catálogo de tags completo (F-004: entidad `Tag`,
`TagRepository`, M:N `Expense↔Tag` vía tabla join `ExpenseTags`, métodos
idempotentes `Expense.AddTag`/`RemoveTag`) y el backend de ingresos
(F-001/F-002/F-003: entidad, repo, use case de registro). Pero el eje de
**filtrado por tags** quedó abierto en ambos lados: `ExpenseFilter` ni
siquiera tiene la propiedad `TagIds` (verificado: 5 props `init`, 42
líneas), y `IncomeFilter.TagIds` existe desde F-002 D-2 únicamente como
**placeholder de estabilidad de firma** — el repositorio lo ignora con el
marcador explícito `_ = filter.TagIds;` y el comentario "intentionally
ignored (added in 4-007)". F-007 enciende esa promesa y cierra el cuarteto
de filtros (`CategoryId`, `TagIds`, `DateFrom`, `DateTo`) que consumirán
4-006 (`SavedView`) y 4-008 (UI de filtros + `GetIncomesQuery`).

**Hallazgo crítico resuelto (D-0).** El lado `Income` **no tiene relación
M:N con `Tag`** (verificado: `Income.cs` 268 líneas sin colección `Tags`;
`IncomeConfiguration.cs` 57 líneas sin mapeo M:N; no existe tabla
`IncomeTags`). Aplicar `TagIds` en `IncomeRepository.GetByFilterAsync` es
imposible sin ese enabler — el phase plan (líneas 47-48, RF-20) exige
"queries con TagIds" para ambos repos. La ronda interactiva ratificó la
**Opción A**: F-007 incluye el enabler mínimo `Income↔Tag` replicando
EXACTAMENTE el patrón probado de F-004 (`Income.Tags` +
`AddTag`/`RemoveTag` idempotentes, mapeo M:N `IncomeTags` con PK
compuesta + FKs `Cascade` + índice `TagId`, UNA migración aditiva
`AddIncomeTagsJoinTable`). La Opción B (solo Expense, diferir Income a
Phase 5+) fue rechazada: rompería la promesa explícita de F-002 D-2 y un
`SavedView` de ingresos con tags (4-006) haría no-op silencioso — viola
"no silent failures".

**Estado actual verificado (lectura de fuente, no suposiciones):**

| Elemento | Estado real (verificado) |
| --- | --- |
| `ExpenseFilter.cs` | `sealed record`, 5 props `init` (`Page`, `PageSize`, `CategoryId?`, `DateFrom?`, `DateTo?`), XML doc por prop, sin validación ("dumb record"). **NO tiene `TagIds`** |
| `IncomeFilter.cs` | 6 props `init` incl. `IReadOnlyList<Guid>? TagIds` — placeholder ignorado; remarks "hasta 4-007" (líneas 13-16, 49) a refrescar |
| `IExpenseRepository` / `IIncomeRepository` | 6 métodos c/u; `GetByFilterAsync(Filter, ct)` ya existe en ambas firmas (línea 70 de cada una) → **cero cambio de interface** |
| `ExpenseRepository.GetByFilterAsync` | Aplica `CategoryId`/`DateFrom`/`DateTo` (líneas 184-197) + orden `Date desc, CreatedAt desc` + paginación. Sin `TagIds` |
| `IncomeRepository.GetByFilterAsync` | Ídem + marcador explícito `_ = filter.TagIds;` con comentario "// TagIds is intentionally ignored (added in 4-007)." (líneas 207-208) + remarks del método (líneas 175-178) a reemplazar |
| `Expense` | `ICollection<Tag> Tags` (línea 64) + `AddTag`/`RemoveTag` idempotentes por `Id` (líneas 198-230), init en ambos ctors (líneas 72, 106) — **patrón espejo exacto** |
| `Income` | **SIN colección `Tags`, SIN `AddTag`/`RemoveTag`** (268 líneas); 2 ctors privados: `private Income()` (línea 71) y `private Income(Guid, Guid?, DateOnly, Money, string, string?, string?, DateTime, DateTime)` (líneas 89-109) |
| `ExpenseConfiguration.cs` | M:N `ExpenseTags` vía `UsingEntity<Dictionary<string, object>>` (líneas 56-77) con PK compuesta, ambas FKs `Cascade`, índice `TagId` — **plantilla a replicar**; NO se modifica (solo se verifica) |
| `IncomeConfiguration.cs` | **SIN mapeo M:N** (FK `Category` `SetNull` línea 40, índices, `OwnsOne Amount` líneas 45-55) — el bloque M:N se AÑADE después de `OwnsOne` |
| Migraciones | 5 versionadas; `20260827223645_AddTagsAndExpenseTags` creó `Tags` + `ExpenseTags` con FKs `Cascade` + `IX_ExpenseTags_TagId` — **plantilla Up/Down**. **NO existe `IncomeTags`** |
| `IncomeRepositoryTests.cs` | 14 tests incl. **T-12 `GetByFilterAsync_IgnoresTagIds_ForNow`** (líneas 301-322) que blinda el contrato "ignorado"; remarks de la clase (línea 19) mencionan "el placeholder `TagIds`" |
| `ExpenseTagManyToManyTests.cs` | 4 tests (persist join rows vía `SqlQueryRaw`, reload con `Include` + `ChangeTracker.Clear()`, cascada income/tag) — **patrón espejo** para `IncomeTagManyToManyTests` |
| `ExpenseTests.cs` (unit) | Sección "T-17 a T-24: Tags" (líneas 280-359): 7 tests `AddTag`/`RemoveTag` con helper `CreateTag` — **patrón espejo** para `IncomeTests` |
| Fakes (`FakeExpenseRepository`/`FakeIncomeRepository`) | Stubs con resultados preconfigurados, sin lógica de filtro → **sin cambios** |
| Test infra | `TestAppDbContextFactory` (49 líneas, `CreateContext()` sync) → la migración nueva se aplica sola vía `MigrateAsync`, **sin cambios** |

**Objetivos concretos:**

1. **Extender `ExpenseFilter`** de 5 → 6 props: añadir
   `IReadOnlyList<Guid>? TagIds { get; init; }` con XML doc (espejo de
   `IncomeFilter.TagIds`).
2. **Aplicar `TagIds`** en `ExpenseRepository.GetByFilterAsync` con
   semántica OR/any-match (D-1), junto a los filtros opcionales
   existentes.
3. **Aplicar `TagIds`** en `IncomeRepository.GetByFilterAsync`
   (reemplaza el marcador `_ = filter.TagIds;`).
4. **Enabler (Opción A):** `Income.Tags` + `AddTag`/`RemoveTag`
   idempotentes (espejo F-004 D-2/D-6) + M:N `IncomeTags` en
   `IncomeConfiguration` (espejo de `ExpenseTags`) + migración única
   `AddIncomeTagsJoinTable`.
5. **Invertir el contrato de T-12** (rename + cambio de semántica, sin
   eliminar el test — D-7).
6. **Cubrir con tests** unitarios (≥7 `Income.AddTag`/`RemoveTag`) e
   integración (≥5 Expense filter, ≥4 Income filter, ≥4 M:N round-trip
   + cascade). Pure xUnit `Assert.*`, sin FluentAssertions, sin mocking.
7. **Refrescar doc-comments obsoletos** ("hasta 4-007" / "added in
   4-007" dejan de ser futuro): `IncomeFilter` remarks, `IncomeRepository`
   remarks + marcador, remarks de `IncomeRepositoryTests`. Solo
   comentarios; cero cambio de API.
8. **Mantener NetArchTest R-1..R-5 verdes**; `dotnet build` 0 warnings;
   `dotnet format style --verify-no-changes` exit 0.

---

## 3. Decisiones (locked-in — no re-abrir)

### D-0 — Opción A ratificada: el enabler `Income↔Tag` M:N es in-scope

**Decisión:** F-007 incluye el enabler mínimo `Income ↔ Tag`: colección
`Income.Tags` (`ICollection<Tag>`, inicializada en ambos ctors privados)
+ métodos de dominio `AddTag`/`RemoveTag` idempotentes por `Id` + mapeo
M:N `IncomeTags` en `IncomeConfiguration` + UNA migración aditiva
`AddIncomeTagsJoinTable`.

**Justificación:** la instrucción de lanzamiento original decía "NO
modificar `Income.cs`, sus configuraciones" y "no new migrations", PERO
también exigía "aplicar `TagIds` en `IncomeRepository.GetByFilterAsync`"
— eso es **imposible** sin el enabler: no existen `Income.Tags`, ni tabla
`IncomeTags`, ni dato que relacione ingresos con tags (verificado). La
lista "no modificar" descansaba en la premisa falsa de que Income ya
tenía tags. La decisión comprometida del phase plan (RF-20 línea 250:
"queries con TagIds" para `IncomeRepository`; líneas 47-48: "los mismos
filtros") y la promesa explícita de F-002 D-2 ("ignorado hasta 4-007")
ganan sobre esa lista. La visión (`docs/vision.md` §4.2: "cada
**movimiento** lleva... Tags libres") cubre gastos E ingresos. El enabler
es aditivo, pequeño y replica un patrón ya probado (F-004 lo hizo para
`Expense` en un feature). La Opción B (solo Expense, `IncomeFilter.TagIds`
sigue ignorado) quedó documentada como fallback rechazado: rompe el phase
plan y hace que un `SavedView` de ingresos con tags (4-006) sea no-op
silencioso — viola "no silent failures".

### D-1 — Semántica `TagIds` = OR / any-match

**Decisión:** `TagIds` filtra con semántica **OR**: devuelve las
entidades que tienen **al menos UNO** de los tags pedidos. No es AND
(todos los tags), no es conteo.

**Justificación:** espejo de la semántica de la UI (multi-select: "muéstrame
gastos de Comida O Transporte"). El test
`GetByFilterAsync_WithMultipleTagIds_Returns*WithAny` en ambos repos
blinda la semántica contra deriva a AND (eco del risk R-5 del proposal).

### D-2 — Edge semantics: `null` / vacía = sin filtro; tag inexistente = resultado vacío

**Decisión:**
- `TagIds == null` → **no se aplica filtro** (todas las entidades).
- `TagIds` lista vacía (`Count == 0`) → **no se aplica filtro**.
- `TagIds` con Guids que no existen en `Tags` → **resultado vacío**
  (`Total == 0`, `Items.Count == 0`), **no error**.

**Justificación:** consistente con los otros filtros opcionales
(`CategoryId? null = sin filtro`). Un tag inexistente no es un error de
negocio: el filtro es un criterio de búsqueda, no una FK a validar (el
repo no valida el filtro — precedente F-002 D-8). Los tests
`WithEmptyTagIds_ReturnsAll`, `WithNullTagIds_ReturnsAll` y
`WithNonExistentTagId_ReturnsEmpty` blindan las tres ramas.

### D-3 — Ambos repositorios aplican el filtro

**Decisión:** `ExpenseRepository.GetByFilterAsync` Y
`IncomeRepository.GetByFilterAsync` aplican `TagIds` con la misma lógica
(adaptando el lambda param: `e` para Expense, `i`/`e` para Income según
el estilo local). Los filtros `CategoryId`/`DateFrom`/`DateTo` existentes
quedan intactos (los tests existentes siguen verdes).

**Justificación:** phase plan líneas 47-48 ("los mismos filtros") y RF-18
+ RF-20. Simetría `Expense`/`Income` del slice.

### D-4 — `ExpenseFilter` extiende de 5 → 6 props (espejo de `IncomeFilter`)

**Decisión:** `ExpenseFilter` gana `public IReadOnlyList<Guid>? TagIds
{ get; init; }` con XML doc (espejo de `IncomeFilter.TagIds` líneas
48-51). Total 6 props: `Page`, `PageSize`, `CategoryId`, `DateFrom`,
`DateTo`, `TagIds`. Sigue siendo "dumb record" sin validación (la
validación de `TagIds` — p. ej. Guids no vacíos — es del caller/use case,
4-008). File-scoped namespace preservado.

**Justificación:** espejo de D-2 de F-002 P4 (estabilidad de firma +
XML doc por propiedad). `TagIds` default `null` = sin filtro → los
callers existentes (`GetTransactionsQuery`, fakes) quedan intactos aunque
la propiedad se agregue.

### D-5 — Traducción LINQ plana con guarda `is { Count: > 0 }` (sin `Include`)

**Decisión:** en ambos repos, junto a los filtros opcionales existentes
(después del bloque `DateTo`, antes del orden):

```csharp
if (filter.TagIds is { Count: > 0 })
{
    query = query.Where(e => e.Tags.Any(t => filter.TagIds!.Contains(t.Id)));
}
```

- Guarda con property pattern `is { Count: > 0 }` cubre `null` y vacía
  (D-2) en una sola condición.
- **El operador null-forgiving `!` es requerido**: el guard garantiza
  no-null, pero el flow analysis del compilador NO cruza reads de
  propiedades hacia el lambda capturado → sin `!` habría warning CS8602
  y el gate de 0 warnings fallaría.
- `IQueryable` building incremental (nada de expressions dinámicas —
  mitigación del risk table del phase plan).
- El repo **NO carga `Tags` en el resultado** (`AsNoTracking`, sin
  `Include`): `Tags.Any(...)` se traduce a `EXISTS` sobre el join + `IN`
  sobre la colección primitiva — EF Core 8 no necesita `Include` para
  filtrar por navegación. Consistente con F-004 R-7 (el repo no hidrata
  Tags; los tests verifican tags vía contexto directo cuando lo
  necesitan). Los índices `IX_ExpenseTags_TagId` (existente) e
  `IX_IncomeTags_TagId` (nuevo) soportan el `EXISTS`.

**Justificación:** patrón plano y verificable; la traducción SQLite de
`Any` + `Contains` sobre colección primitiva es soportada por EF Core 8
(parameterized primitive collections) y queda probada por los
integration tests de §6. Si la traducción fallara (risk R-1), el fallback
documentado es capturar a local `var tagIds = filter.TagIds;` y/o
`.ToArray()` — el shape del guard no cambia.

### D-6 — Una migración por tabla join: `AddIncomeTagsJoinTable` (nueva)

**Decisión:** UNA migración nueva `AddIncomeTagsJoinTable` crea solo la
tabla `IncomeTags` (DDL puro, aditiva, sin `InsertData`); su `Down` la
elimina. `AddTagsAndExpenseTags` (F-004) ya existe y NO se toca. No hay
segunda migración: `Tags` ya existe; solo falta el join de Income.

**Justificación:** eco de D-3 de F-004 (atomicidad del slice: una tabla
join = una migración). La tabla `Tags` es compartida por ambos joins —
recrearla sería destructivo y está fuera de alcance.

### D-7 — T-12 se ACTUALIZA in-place (rename + semántica invertida), NO se elimina

**Decisión:** el test `GetByFilterAsync_IgnoresTagIds_ForNow` (T-12,
`IncomeRepositoryTests.cs` líneas 301-322) se **renombra** a
`GetByFilterAsync_WithTagIds_FiltersCorrectly` y se **invierte su
contrato**: en lugar de afirmar que `TagIds` es ignorado (assert
`Total == 3` con tag inexistente), afirma que `TagIds` **ES aplicado**
(devuelve solo los ingresos con el tag pedido). El test permanece en el
mismo archivo (`IncomeRepositoryTests.cs`) y en la misma posición (12ª
en la lista de tests). Los remarks de la clase (línea 19, "el
placeholder `TagIds`") se refrescan.

**Justificación:** esta decisión **supera** el D-5 del proposal ("T-12 se
elimina; su sucesor vive en `IncomeFilterTagTests`") — la ronda
interactiva ratificó mantener el test in-place como regresión explícita
del campo antes-ignorado, preservando la posición y trazabilidad del
contrato F-002 D-2. El contrato "ignorado" queda invertido y cubierto
dos veces (T-12 renombrado + suite de `IncomeFilterTagTests`). NOTA: el
nuevo `IncomeFilterTagTests` también contiene un test llamado
`GetByFilterAsync_WithTagIds_FiltersCorrectly` — nombres idénticos en
clases distintas son legales en xUnit y ambos blindan el mismo contrato
desde fixtures distintas.

### D-8 — El enabler replica EXACTAMENTE el patrón F-004 (heredadas)

**Decisión (hereda D-2/D-6/D-7 de F-004 P4):**
- `Income.AddTag(Tag)`: `ArgumentNullException.ThrowIfNull(tag)` + guard
  `IsNullOrWhiteSpace(tag.Name)` → `ArgumentException` + duplicado por
  `Id` → **no-op silencioso** (idempotente).
- `Income.RemoveTag(Tag)`: `ArgumentNullException.ThrowIfNull(tag)` +
  `FirstOrDefault(t => t.Id == tag.Id)` → remove si existe, **no-op** si
  no (idempotente).
- `Tags` inicializada como `new List<Tag>()` en **AMBOS** ctors privados
  (el sin-parámetros para EF Core, línea 71, y el del factory, líneas
  89-109) — sin init en el ctor sin params, EF dejaría `Tags == null` al
  materializar y `AddTag` lanzaría `NullReferenceException`.
- M:N vía `UsingEntity<Dictionary<string, object>>("IncomeTags", ...)`
  con PK compuesta `(IncomeId, TagId)`, **ambas FKs `Cascade`**, índice
  sobre `TagId`. **Sin `DbSet`** para el join (igual que `ExpenseTags`).
- `using ControlDeFinanzasIA.Domain.Catalog;` añadido a `Income.cs`
  (referencia intra-Domain, permitida por R-1 — `Expense` ya lo hace).
- Las interfaces NO cambian (`GetByFilterAsync` ya existe en ambas —
  solo implementaciones).

---

## 4. Criterios de aceptación

La feature se considera completada cuando **todos** los ACs siguientes
se cumplen, verificables mediante tests automatizados y revisión de
código. Cada AC incluye escenarios Given/When/Then.

### AC-1 — `Income.Tags` + `AddTag`/`RemoveTag` (D-0, D-8)

- [ ] `src/ControlDeFinanzasIA.Domain/Transactions/Income.cs` (modify)
      gana `public ICollection<Tag> Tags { get; private set; } =
      new List<Tag>();` (espejo de `Expense.Tags` línea 64).
- [ ] `Tags` inicializada como `new List<Tag>()` en **AMBOS** ctors
      privados: `private Income()` (línea 71) **Y** el ctor de 9 params
      (líneas 89-109) — espejo de `Expense` líneas 72/106.
- [ ] `public void AddTag(Tag tag)`: `ArgumentNullException.ThrowIfNull(tag)`;
      si `string.IsNullOrWhiteSpace(tag.Name)` → `ArgumentException`
      ("Tag name cannot be empty.", `nameof(tag)`); si
      `Tags.Any(t => t.Id == tag.Id)` → **return** (no-op); `Tags.Add(tag)`.
      Espejo de `Expense.AddTag` líneas 198-213.
- [ ] `public void RemoveTag(Tag tag)`: `ArgumentNullException.ThrowIfNull(tag)`;
      `Tag? existing = Tags.FirstOrDefault(t => t.Id == tag.Id);` si no
      null → `Tags.Remove(existing)`; si null → no-op. Espejo de
      `Expense.RemoveTag` líneas 221-230.
- [ ] `using ControlDeFinanzasIA.Domain.Catalog;` añadido al tope de
      `Income.cs`.
- [ ] XML doc en `Tags`, `AddTag`, `RemoveTag` (espejo de `Expense`).

- **GIVEN** un `Income` recién creado vía `Income.Create(...)`.
- **WHEN** se inspecciona `income.Tags`.
- **THEN** es una colección no-null y vacía (`Count == 0`).
- **GIVEN** `income.AddTag(tagA)` donde `tagA.Id` no está en `Tags`.
- **WHEN** se llama.
- **THEN** `Tags.Count == 1` y contiene `tagA`.
- **GIVEN** `Tags` ya contiene `tagA` (mismo `Id`).
- **WHEN** `income.AddTag(tagA)` de nuevo.
- **THEN** `Count` sigue siendo 1 (no-op idempotente).
- **GIVEN** `income.AddTag(null!)`.
- **WHEN** se llama.
- **THEN** lanza `ArgumentNullException`.

### AC-2 — `IncomeConfiguration` M:N → `IncomeTags` (D-0, D-8)

- [ ] `src/.../Infrastructure/Persistence/Configurations/IncomeConfiguration.cs`
      (modify): tras el bloque `OwnsOne(e => e.Amount, ...)` existente
      (líneas 45-55), se AÑADE el mapeo M:N (espejo literal de
      `ExpenseConfiguration` líneas 56-77, adaptando `Income`/`IncomeTags`):

```csharp
builder.HasMany(e => e.Tags)
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

- [ ] Ambas FKs son `DeleteBehavior.Cascade` (verificado en la
      migración generada + tests MT-3/MT-4).
- [ ] XML doc comment sobre el bloque explicando la relación M:N
      (espejo del comentario de `ExpenseConfiguration` líneas 56-59).
- [ ] El bloque `OwnsOne(e => e.Amount)` y todo lo existente (FK
      `SetNull`, índices, longitudes) queda **intacto** — el M:N se
      añade, no se reordena (los `IncomeSchemaTests` existentes lo
      blindan).
- [ ] `using ControlDeFinanzasIA.Domain.Catalog;` ya presente (línea 1
      por `HasOne<Category>()`).

- **GIVEN** el modelo se construye con el mapeo M:N.
- **WHEN** EF genera la migración.
- **THEN** se crea `IncomeTags` con PK `(IncomeId, TagId)`, ambas FKs
  `Cascade`, índice en `TagId`, y `Tag` **sin** colección inversa hacia
  `Income` (`.WithMany()` vacío — Tag es catálogo puro).

### AC-3 — Migración `AddIncomeTagsJoinTable` (D-6)

- [ ] Comando ejecutado sin error:
      `dotnet ef migrations add AddIncomeTagsJoinTable --project
      src/ControlDeFinanzasIA.Infrastructure --startup-project
      src/ControlDeFinanzasIA.UI` (requiere `dotnet-ef` 8.0.11 local).
- [ ] 3 archivos commiteados:
      `<YYYYMMDDhhmmss>_AddIncomeTagsJoinTable.cs`,
      `<YYYYMMDDhhmmss>_AddIncomeTagsJoinTable.Designer.cs`,
      `AppDbContextModelSnapshot.cs` (actualizado por la herramienta).
- [ ] `Up` crea la tabla `IncomeTags` (espejo de
      `20260827223645_AddTagsAndExpenseTags.cs` líneas 25-47, adaptada):

```csharp
migrationBuilder.CreateTable(
    name: "IncomeTags",
    columns: table => new
    {
        IncomeId = table.Column<Guid>(type: "TEXT", nullable: false),
        TagId = table.Column<Guid>(type: "TEXT", nullable: false)
    },
    constraints: table =>
    {
        table.PrimaryKey("PK_IncomeTags", x => new { x.IncomeId, x.TagId });
        table.ForeignKey(
            name: "FK_IncomeTags_Incomes_IncomeId",
            column: x => x.IncomeId,
            principalTable: "Incomes",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
        table.ForeignKey(
            name: "FK_IncomeTags_Tags_TagId",
            column: x => x.TagId,
            principalTable: "Tags",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    });

migrationBuilder.CreateIndex(
    name: "IX_IncomeTags_TagId",
    table: "IncomeTags",
    column: "TagId");
```

- [ ] **NO `InsertData`** (no hay seed data — los tags son del usuario).
- [ ] `Down`: `migrationBuilder.DropTable(name: "IncomeTags");` (una
      sola tabla — no hay orden inverso que respetar).
- [ ] File-scoped namespace (corregir manualmente si el generador
      emite block-scoped — precedente F-001 P4 §17 C-5 / F-004 AC-8).
- [ ] No toca `Tags`/`Expenses`/`ExpenseTags`/`Incomes`/`Categories`/
      `PaymentMethods` ni ninguna migración previa (aditiva pura).

- **GIVEN** una BD en el estado post-migración-5.
- **WHEN** se aplica `AddIncomeTagsJoinTable`.
- **THEN** existe `IncomeTags` con PK compuesta, 2 FKs `Cascade` e
  índice `IX_IncomeTags_TagId`; ningún objeto pre-existente cambió.

### AC-4 — `ExpenseFilter` extendida a 6 props (D-4)

- [ ] `src/ControlDeFinanzasIA.Application/Abstractions/ExpenseFilter.cs`
      (modify) gana como 6ª propiedad (espejo de `IncomeFilter.TagIds`
      líneas 48-51):

```csharp
/// <summary>
/// Identificadores de tags para filtrar (semántica OR: la entidad
/// debe tener al menos uno). <c>null</c> o lista vacía significan
/// que no se aplica filtro por tags.
/// </summary>
public IReadOnlyList<Guid>? TagIds { get; init; }
```

- [ ] File-scoped namespace `ControlDeFinanzasIA.Application.Abstractions`
      preservado; XML doc por propiedad preservado (los 5 existentes
      intactos).
- [ ] 6 props totales: `Page`, `PageSize`, `CategoryId`, `DateFrom`,
      `DateTo`, `TagIds`. Sin validación (dumb record; la validación de
      `TagIds` es del caller, 4-008).

- **GIVEN** el proyecto compila con `ExpenseFilter` extendida.
- **WHEN** un caller construye `new ExpenseFilter()` o
  `new ExpenseFilter { TagIds = new[] { t1 } }`.
- **THEN** `TagIds` es `null` por default (callers existentes intactos)
  o la lista provista; el record sigue inmutable (`init`).

### AC-5 — `ExpenseRepository.GetByFilterAsync` aplica `TagIds` (D-1, D-3, D-5)

- [ ] `src/.../Infrastructure/Persistence/Repositories/ExpenseRepository.cs`
      (modify `GetByFilterAsync`, líneas 178-211): después del bloque
      `if (filter.DateTo is not null)` y antes del orden, se AÑADE:

```csharp
if (filter.TagIds is { Count: > 0 })
{
    query = query.Where(e => e.Tags.Any(t => filter.TagIds!.Contains(t.Id)));
}
```

- [ ] El resto del método (orden `Date desc, CreatedAt desc`, `CountAsync`,
      `Skip`/`Take`, `PagedResult`) queda **sin cambios**.
- [ ] Comportamiento de `CategoryId`/`DateFrom`/`DateTo` sin cambios
      (tests existentes de `ExpenseRepositoryTests` siguen verdes).
- [ ] XML doc del método actualizado (menciona `TagIds` OR/any-match).

- **GIVEN** expenses sembrados: E1 con tagA, E2 con tagB, E3 sin tags.
- **WHEN** `repo.GetByFilterAsync(new ExpenseFilter { TagIds = [tagA.Id] })`.
- **THEN** `Total == 1`, `Items.Single().Id == E1.Id` (OR/any-match).
- **GIVEN** ídem siembra.
- **WHEN** `TagIds = [tagA.Id, tagB.Id]`.
- **THEN** `Total == 2` (E1 y E2 — cada uno tiene al menos uno).
- **GIVEN** ídem siembra.
- **WHEN** `TagIds = []` o `TagIds = null`.
- **THEN** `Total == 3` (sin filtro).
- **GIVEN** ídem siembra.
- **WHEN** `TagIds = [Guid.NewGuid()]` (inexistente).
- **THEN** `Total == 0`, `Items.Count == 0` (vacío, no error).

### AC-6 — `IncomeRepository.GetByFilterAsync` aplica `TagIds` (antes ignorado) (D-1, D-3, D-5)

- [ ] `src/.../Infrastructure/Persistence/Repositories/IncomeRepository.cs`
      (modify `GetByFilterAsync`, líneas 186-222): el marcador
      `// TagIds is intentionally ignored (added in 4-007).` +
      `_ = filter.TagIds;` (líneas 207-208) se **REEMPLAZA** por:

```csharp
if (filter.TagIds is { Count: > 0 })
{
    query = query.Where(i => i.Tags.Any(t => filter.TagIds!.Contains(t.Id)));
}
```

- [ ] El resto del método (orden, conteo, paginación) queda sin cambios.
- [ ] Comportamiento de `CategoryId`/`DateFrom`/`DateTo` sin cambios
      (tests T-8..T-11 de `IncomeRepositoryTests` siguen verdes).
- [ ] XML doc del método: el `<para>` "TagIds se ignora intencionalmente
      en F-002; el filtrado por tags se implementará en 4-007"
      (líneas 175-178) se refresca a "aplica `TagIds` con semántica
      OR/any-match (F-007)".
- [ ] **Requiere AC-1..AC-3** (`Income.Tags` + join `IncomeTags` deben
      existir para que la query traduzca).

- **GIVEN** incomes sembrados: I1 con tagA, I2 con tagB, I3 sin tags.
- **WHEN** `repo.GetByFilterAsync(new IncomeFilter { TagIds = [tagA.Id] })`.
- **THEN** `Total == 1`, `Items.Single().Id == I1.Id`.
- **GIVEN** ídem siembra.
- **WHEN** `TagIds = []` o `null`.
- **THEN** `Total == 3` (sin filtro — el default `null` de los callers
  existentes no cambia comportamiento).

### AC-7 — T-12 contract flip en `IncomeRepositoryTests` (D-7)

- [ ] `tests/.../Tests.Integration/Persistence/IncomeRepositoryTests.cs`
      (modify): el test `GetByFilterAsync_IgnoresTagIds_ForNow` (líneas
      301-322) se RENOMBRA a `GetByFilterAsync_WithTagIds_FiltersCorrectly`
      y se invierte su semántica: sembrar incomes **con tags** (vía
      contexto, patrón §6.5) y afirmar que el filtro **aplica**
      (devuelve solo los ingresos con el tag pedido — ya NO
      `Total == 3` con tag inexistente).
- [ ] El test permanece en el mismo archivo y en la misma posición
      (12º entre `GetByFilterAsync_Paginates_CorrectTotalAndItems` y
      `UpdateAsync_Existing_UpdatesFields`).
- [ ] Los remarks de la clase (línea 19: "...y el placeholder
      `TagIds`") se refrescan a la semántica aplicada.
- [ ] NO se elimina ningún test del archivo (siguen siendo 14).

- **GIVEN** I1 con tagA, I2 con tagB persistidos (sembrados con tags
  reales vía contexto).
- **WHEN** `repo.GetByFilterAsync(new IncomeFilter { TagIds = [tagA.Id] })`.
- **THEN** `Total == 1`, `Items.Single().Id == I1.Id` — el contrato
  "ignorado" de F-002 D-2 queda oficialmente invertido.

### AC-8 — Integration tests: `ExpenseFilterTagTests` (NEW, ≥5)

- [ ] `tests/.../Tests.Integration/Persistence/ExpenseFilterTagTests.cs`
      existe, namespace
      `ControlDeFinanzasIA.Tests.Integration.Persistence`, pure xUnit
      `Assert.*` (sin FluentAssertions, sin mocking), fixture verificado
      (`new TestAppDbContextFactory()` + `CreateContext()` +
      `MigrateAsync()`), sembrado de tags según el patrón verificado de
      `ExpenseTagManyToManyTests` (`context.Tags.AddRange(...)` +
      `SaveChangesAsync` → `expense.AddTag(tag)` → `repo.AddAsync` /
      `context.Expenses.Add` + `SaveChangesAsync`), helper `CreateTag`
      + `_comidaCategoryId` sembrada.
- [ ] ≥5 tests (6 requeridos, ver §6.2):
  `GetByFilterAsync_WithSingleTagId_ReturnsOnlyMatching`,
  `GetByFilterAsync_WithMultipleTagIds_ReturnsExpensesWithAny` (OR),
  `GetByFilterAsync_WithEmptyTagIds_ReturnsAll`,
  `GetByFilterAsync_WithNullTagIds_ReturnsAll`,
  `GetByFilterAsync_WithNonExistentTagId_ReturnsEmpty`,
  `GetByFilterAsync_WithCombinedCategoryAndTagIds_ReturnsIntersection`.

- **GIVEN** los 6 tests implementados según §6.2.
- **WHEN** `dotnet test -c Debug --no-build --nologo --filter "FullyQualifiedName~ExpenseFilterTagTests"`.
- **THEN** los 6 pasan en verde.

### AC-9 — Integration tests: `IncomeFilterTagTests` (NEW, ≥4)

- [ ] `tests/.../Tests.Integration/Persistence/IncomeFilterTagTests.cs`
      existe, pure xUnit `Assert.*`, fixture verificado, espejo de AC-8
      para `Income` (helper `CreateIncome` con `categoryId` nullable +
      7º arg `source`, sembrado de tags vía contexto).
- [ ] ≥4 tests requeridos (ver §6.3):
  `GetByFilterAsync_WithTagIds_FiltersCorrectly` (regresión del campo
  antes-ignorado), `GetByFilterAsync_WithMultipleTagIds_ReturnsIncomesWithAny`,
  `GetByFilterAsync_WithEmptyTagIds_ReturnsAll`,
  `GetByFilterAsync_WithCombinedCategoryAndTagIds_ReturnsIntersection`.
- [ ] 2 tests opcionales por simetría con Expense (recomendados —
  ratificado en la ronda interactiva del proposal):
  `GetByFilterAsync_WithNullTagIds_ReturnsAll`,
  `GetByFilterAsync_WithNonExistentTagId_ReturnsEmpty`.

- **GIVEN** los 4+ tests implementados según §6.3.
- **WHEN** `dotnet test -c Debug --no-build --nologo --filter "FullyQualifiedName~IncomeFilterTagTests"`.
- **THEN** todos pasan en verde.

### AC-10 — Integration tests: `IncomeTagManyToManyTests` (NEW, ≥4)

- [ ] `tests/.../Tests.Integration/Persistence/IncomeTagManyToManyTests.cs`
      existe, pure xUnit `Assert.*`, espejo estructural de
      `ExpenseTagManyToManyTests.cs` (179 líneas): helpers
      `CreateTag`/`CreateIncome`, verificación de join rows vía
      `context.Database.SqlQueryRaw<IncomeTagRow>("SELECT IncomeId,
      TagId FROM IncomeTags WHERE IncomeId = {0}", ...)`, reload con
      `ChangeTracker.Clear()` + `Include(i => i.Tags).AsNoTracking()`,
      contadores `CountIncomeTagsAsync`/`CountTagJoinRowsAsync` vía
      `SELECT COUNT(*) AS Value`, `TagRepository.RemoveAsync` para la
      cascada del tag.
- [ ] ≥4 tests (ver §6.4):
  `AddIncomeWithTags_PersistsBothAndJoinRows`,
  `ReloadIncomeWithTags_PopulatesTagsFromJoinTable`,
  `DeleteIncome_CascadesJoinRows`,
  `DeleteTag_CascadesJoinRows`.

- **GIVEN** los 4 tests implementados según §6.4.
- **WHEN** `dotnet test -c Debug --no-build --nologo --filter "FullyQualifiedName~IncomeTagManyToMany"`.
- **THEN** los 4 pasan en verde (el mapeo M:N hidrata la navegación y
  ambas cascadas operan).

### AC-11 — Unit tests: `IncomeTests` extensión `AddTag`/`RemoveTag` (≥7)

- [ ] `tests/.../Tests.Unit/Domain/IncomeTests.cs` (modify — extender
      el archivo existente, 285 líneas) gana una sección "Tags" (espejo
      de la sección "T-17 a T-24: Tags" de `ExpenseTests.cs` líneas
      280-359) + helper local `CreateTag(string name, string? color =
      null) => Tag.Create(name, color, _defaultCreatedAt);` (reutiliza
      el `_defaultCreatedAt` existente, línea 20) + `using
      ControlDeFinanzasIA.Domain.Catalog;`.
- [ ] ≥7 tests requeridos, con la nomenclatura **exacta** del espejo
      `ExpenseTests` (ver §6.1):
  `NewIncome_HasEmptyNotNullTagsCollection`,
  `AddTag_WithNewTag_AddsToCollection`,
  `AddTag_WithDuplicateId_IsNoOp`,
  `AddTag_WithNullTag_ThrowsArgumentNullException`,
  `RemoveTag_Existing_RemovesFromCollection`,
  `RemoveTag_NotInCollection_IsNoOp`,
  `RemoveTag_WithNullTag_ThrowsArgumentNullException`.
- [ ] 1 test opcional (nota ET-5 de F-004 §6.2 aplica):
  `AddTag_WithEmptyNameTag_Throws` — `Tag.Create` rechaza `Name` vacío,
  por lo que no es posible construir un `Tag` inválido vía el factory
  público; el test blinda el **guard defensivo** de `AddTag` y queda a
  discreción del implementador.

> **Nota de nomenclatura (discrepancia resuelta):** el contrato de
> lanzamiento listaba `AddTag_WithNullTag_Throws` /
> `RemoveTag_WithNullTag_Throws`; el espejo real implementado en
> `ExpenseTests.cs` usa el sufijo largo `..._ThrowsArgumentNullException`
> (líneas 317, 354). F-007 adopta la nomenclatura del **código** (el
> espejo es autoritativo); los nombres cortos del contrato son aliases.

- **GIVEN** los 7+ tests implementados según §6.1.
- **WHEN** `dotnet test -c Debug --no-build --nologo --filter "FullyQualifiedName~Tests.Unit.Domain.IncomeTests"`.
- **THEN** todos pasan en verde (existentes + nuevos).

### AC-12 — La migración aplica limpiamente

- [ ] `dotnet ef migrations add AddIncomeTagsJoinTable --project
      src/ControlDeFinanzasIA.Infrastructure --startup-project
      src/ControlDeFinanzasIA.UI` ejecuta sin error (después de AC-1 +
      AC-2).
- [ ] `dotnet ef database update` (o, en tests, `MigrateAsync()` del
      fixture) aplica la migración sin error — los
      `IncomeSchemaTests`/`IncomeTagManyToManyTests` existentes la
      ejercitan vía `MigrateAsync` sin cambios de infra.
- [ ] `dotnet ef migrations script` muestra el `Up` (CreateTable +
      CreateIndex) y `Down` (DropTable) esperados — sin `InsertData`.

- **GIVEN** una BD SQLite (test in-memory o local del usuario).
- **WHEN** `MigrateAsync()` corre con la 6ª migración presente.
- **THEN** aplica limpiamente: `IncomeTags` existe con las restricciones
  de AC-3; las migraciones 1-5 no se re-ejecutan ni alteran.

### AC-13 — Gates de verificación

- [ ] `dotnet build -c Release --nologo` → 0 warnings, 0 errors.
- [ ] `dotnet build -c Debug --nologo` → 0 warnings, 0 errors.
- [ ] `dotnet test -c Debug --no-build --nologo` (suite completa) →
      verde excepto el **1 fallo pre-existing conocido de `Money`**
      (pre-existente a F-007, fuera de su alcance — no introducido ni
      agravado por este feature; el implementador reporta el conteo
      exacto observado).
- [ ] `dotnet test -c Debug --no-build --nologo --filter
      "FullyQualifiedName~ArchitectureTests"` → 11/11 (R-1..R-5).
- [ ] `dotnet test -c Debug --no-build --nologo --filter
      "FullyQualifiedName~FilterTagTests|FullyQualifiedName~IncomeTagManyToMany|FullyQualifiedName~IncomeRepository"`
      → todos pasan (≈28: 6 ExpenseFilterTag + 4+ IncomeFilterTag + 4
      IncomeTagManyToMany + 14 IncomeRepositoryTests incl. T-12
      renombrado).
- [ ] `dotnet test -c Debug --no-build --nologo --filter
      "FullyQualifiedName~Tests.Unit.Domain.IncomeTests"` → todos
      pasan (existentes + 7-8 nuevos).
- [ ] `dotnet format style --verify-no-changes` → exit 0.
- [ ] `dotnet list src/ControlDeFinanzasIA.Domain package` sigue vacío
      (R-1: Domain no gana dependencias).

> Las cuentas de tests son **aproximadas** (línea base post-4-005 según
> contrato de lanzamiento); el gate real es "0 failures nuevos + suite
> previa sigue verde (excluyendo el fallo pre-existing de Money) + ~22-24
> tests nuevos verdes". El implementador (sdd-apply) reporta las cuentas
> exactas observadas.

### AC-14 — Sin scope creep

- [ ] NO modificaciones a `Tag.cs`, `TagConfiguration.cs`,
      `TagRepository.cs`, `ITagRepository.cs` (cerrados en 4-004).
- [ ] NO modificaciones a `Expense.cs`, `ExpenseConfiguration.cs` (el
      M:N de Expense ya está en lugar desde F-004 — solo se **verifica**
      que existe como plantilla; cero líneas cambiadas).
- [ ] NO modificaciones a `RegisterIncomeUseCase`/
      `RegisterExpenseUseCase`/`Update*UseCase`/`Delete*UseCase` ni
      nuevos use cases (los use cases de filtrado son 4-008).
- [ ] NO `SavedView` (4-006), NO UI/ViewModel/XAML (4-008).
- [ ] NO `Money.cs`, `Currency.cs`, `Result.cs`, `Error.cs`,
      `PagedResult.cs`, `IClock`.
- [ ] NO migraciones además de `AddIncomeTagsJoinTable`; NO se tocan
      migraciones previas ni `SeedData.cs`.
- [ ] NO `IExpenseRepository`/`IIncomeRepository` (cero cambio de
      interface), NO `AppDbContext.cs` (el join no necesita DbSet),
      NO `ServiceCollectionExtensions.cs` (no hay repos nuevos), NO
      fakes, NO `TestAppDbContextFactory`.
- [ ] NO FluentAssertions, NO mocking library, NO paquetes NuGet nuevos.
- [ ] **Única excepción sancionada** (D-0): el enabler `Income.Tags` +
      M:N `IncomeTags` + migración + sus tests (AC-1..AC-3, AC-10,
      AC-11) — aprobado por la ronda interactiva.
- [ ] **Excepción doc-only sancionada**: refrescar remarks obsoletos
      "hasta 4-007"/"added in 4-007" en `IncomeFilter.cs` (líneas
      13-16, 49), `IncomeRepository.cs` (AC-6) e
      `IncomeRepositoryTests.cs` (AC-7). Solo comentarios; cero cambio
      de API.

- **GIVEN** el diff de F-007.
- **WHEN** se inspecciona contra la lista de archivos in-scope (§5).
- **THEN** no hay archivos fuera de la lista modificados; ningún
  comentario/doc menciona ya "hasta 4-007" como futuro.

### AC-15 — NetArchTest R-1..R-5 preservados

- [ ] `dotnet test -c Debug --no-build --nologo --filter
      "FullyQualifiedName~ArchitectureTests"` → 11/11.
- [ ] `Income.cs` referencia `Domain.Catalog` (`Tag`) — referencia
      **intra-Domain**, permitida por R-1 (`Expense` ya lo hace desde
      F-004 sin violación).
- [ ] Ninguna dependencia nueva que viole R-1..R-5: los cambios tocan
      Domain (entidad), Application (record doc/prop) e Infrastructure
      (config, repo, migración) — direcciones permitidas (R-2/R-3).

### AC-16 — Estrategia de PRs encadenados (stacked-to-main)

- [ ] Diff estimado ~500-600 líneas (> presupuesto de 400) →
      **2 PRs encadenados** (estrategia `stacked-to-main`), a confirmar
      por `sdd-tasks`:
  - **PR-1 (enabler):** AC-1 (`Income.Tags`) + AC-2 (M:N config) +
    AC-3 (migración) + AC-11 (unit tests) + AC-10 (M:N integration
    tests) ≈ 280 líneas — autónomo, verificable, con rollback propio.
  - **PR-2 (filtros):** AC-4 (`ExpenseFilter.TagIds`) + AC-5 + AC-6
    (filtros en ambos repos) + AC-7 (T-12 flip) + AC-8 + AC-9 (filter
    tests) + AC-12..AC-15 (gates) ≈ 250 líneas — depende de PR-1
    (`Income.Tags` debe existir para que `IncomeRepository` filtre).
- [ ] Cada slice con inicio/fin claros, scope autónomo, verificación y
      rollback razonable (guard de carga de revisión, §E de
      `sdd-phase-common`).

---

## 5. Áreas afectadas

| Archivo | Acción | Descripción |
| --- | --- | --- |
| `src/ControlDeFinanzasIA.Domain/Transactions/Income.cs` | **Modify** | + `ICollection<Tag> Tags` (init en ambos ctors, D-8) + `AddTag`/`RemoveTag` idempotentes + `using Domain.Catalog` (AC-1). |
| `src/.../Infrastructure/Persistence/Configurations/IncomeConfiguration.cs` | **Modify** | + mapeo M:N `IncomeTags` vía `UsingEntity` tras el `OwnsOne(Amount)` existente (AC-2). |
| `src/.../Infrastructure/Persistence/Migrations/<ts>_AddIncomeTagsJoinTable.cs` | **New (generado)** | `Up` crea `IncomeTags` + PK compuesta + FKs `Cascade` + `IX_IncomeTags_TagId`; `Down` dropea la tabla (AC-3). |
| `src/.../Infrastructure/Persistence/Migrations/<ts>_AddIncomeTagsJoinTable.Designer.cs` | **New (generado)** | Metadata de la migración. |
| `src/.../Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs` | **Edit (generado)** | + entidad join `IncomeTags`. |
| `src/ControlDeFinanzasIA.Application/Abstractions/ExpenseFilter.cs` | **Modify** | + 6ª prop `IReadOnlyList<Guid>? TagIds { get; init; }` + XML doc (AC-4). |
| `src/.../Infrastructure/Persistence/Repositories/ExpenseRepository.cs` | **Modify** | Filtro `TagIds` OR en `GetByFilterAsync` + XML doc (AC-5). |
| `src/.../Infrastructure/Persistence/Repositories/IncomeRepository.cs` | **Modify** | Filtro `TagIds` OR (fuera el marcador `_ = filter.TagIds;`) + XML doc refrescado (AC-6). |
| `src/ControlDeFinanzasIA.Application/Abstractions/IncomeFilter.cs` | **Modify (doc-only)** | Remarks "ignorado hasta 4-007" → aplicado; cero cambio de API (AC-14 excepción doc-only). |
| `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/IncomeRepositoryTests.cs` | **Modify** | T-12 renombrada + semántica invertida + remarks de la clase (AC-7). |
| `tests/.../Tests.Integration/Persistence/ExpenseFilterTagTests.cs` | **New** | 6 tests del filtro `TagIds` de Expense (AC-8). |
| `tests/.../Tests.Integration/Persistence/IncomeFilterTagTests.cs` | **New** | 4+2 tests del filtro `TagIds` de Income (AC-9). |
| `tests/.../Tests.Integration/Persistence/IncomeTagManyToManyTests.cs` | **New** | 4 tests round-trip M:N + cascada (AC-10). |
| `tests/ControlDeFinanzasIA.Tests.Unit/Domain/IncomeTests.cs` | **Modify (extend)** | + sección Tags: helper `CreateTag` + 7-8 tests `AddTag`/`RemoveTag` (AC-11). |
| `src/.../Infrastructure/Persistence/Configurations/ExpenseConfiguration.cs` | **Verify-only** | El M:N `ExpenseTags` ya existe (F-004, líneas 56-77) — se usa como plantilla; **cero líneas cambiadas**. |

**Archivos NO modificados (verificados presentes/correctos):**
`Tag.cs`, `TagConfiguration.cs`, `TagRepository.cs`, `ITagRepository.cs`
(cerrados en 4-004), `Expense.cs`, `IExpenseRepository.cs`,
`IIncomeRepository.cs` (`GetByFilterAsync` ya existe en ambas — cero
cambio de interface), `Money.cs`, `Currency.cs`, `Result.cs`, `Error.cs`,
`PagedResult.cs`, `IClock`, `AppDbContext.cs` (el join no necesita
DbSet), `SeedData.cs`, `ServiceCollectionExtensions.cs`, migraciones
previas (1-5), `RegisterIncomeUseCase`/`RegisterExpenseUseCase`/
`Update*UseCase`/`Delete*UseCase` y sus commands/validators, fakes
(`FakeExpenseRepository`/`FakeIncomeRepository` — stubs sin lógica de
filtro), `TestAppDbContextFactory.cs`, `IncomeSchemaTests.cs`,
`TagSchemaTests.cs`, `TagRepositoryTests.cs`,
`ExpenseTagManyToManyTests.cs`, `ExpenseRepositoryTests.cs`,
`ExpenseTests.cs`, cualquier archivo de UI/ViewModel/XAML.

**Modificaciones a `.csproj`:** ninguna (sin paquetes nuevos, sin
referencias nuevas).

---

## 6. Plan de tests

Marco y paquetes: xUnit + `Microsoft.Data.Sqlite` + EF Core SQLite (ya
en los proyectos de tests desde Fase 2). **Sin paquetes nuevos; sin
FluentAssertions; sin mocking.** Aserciones con `Assert.*` (`Assert.True`,
`Assert.False`, `Assert.Equal`, `Assert.Null`, `Assert.NotNull`,
`Assert.Single`, `Assert.Contains`, `Assert.All`, `Assert.Throws`).

Fixture verificado (NO el shorthand inexistente `CreateAsync()` —
ver F-002 P4 §6):

```csharp
using var factory = new TestAppDbContextFactory();
using AppDbContext context = factory.CreateContext();
await context.Database.MigrateAsync();
```

**Patrón de sembrado con tags (verificado en
`ExpenseTagManyToManyTests.cs`):** los tags se persisten PRIMERO vía
`context.Tags.AddRange(tag1, tag2); await context.SaveChangesAsync();`;
después la transacción se construye, se taggea (`income.AddTag(tag1)`)
y se persiste vía `repo.AddAsync(income)` (internamente
`_db.Incomes.Add` + `SaveChangesAsync` — EF inserta solo las filas join
porque los tags ya están tracked como Unchanged) o vía `context.Incomes.Add`.
`_comidaCategoryId = new("11111111-1111-1111-1111-000000000001")` y
`_fixedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)` para
determinismo.

### 6.1 Unit tests — `Tests.Unit/Domain/IncomeTests.cs` (extensión)

Espejo de la sección "T-17 a T-24: Tags" de `ExpenseTests.cs` (líneas
280-359). Helper local:

```csharp
private static Tag CreateTag(string name, string? color = null)
    => Tag.Create(name, color, _defaultCreatedAt);
```

| # | Test | GIVEN / WHEN / THEN |
| --- | --- | --- |
| IT-1 | `NewIncome_HasEmptyNotNullTagsCollection` | **GIVEN** `Income.Create(...)` válido. **THEN** `Assert.NotNull(income.Tags)` + `Assert.Empty(income.Tags)` (init en ambos ctors, D-8). |
| IT-2 | `AddTag_WithNewTag_AddsToCollection` | **GIVEN** income + `tagA = CreateTag("Trabajo")`. **WHEN** `income.AddTag(tagA)`. **THEN** `Assert.Single(income.Tags)` + `Assert.Contains(tagA, income.Tags)`. |
| IT-3 | `AddTag_WithDuplicateId_IsNoOp` | **GIVEN** income con `tagA`. **WHEN** `income.AddTag(tagA)` de nuevo (mismo `Id`). **THEN** `Assert.Single(income.Tags)` (idempotente, D-8). |
| IT-4 | `AddTag_WithNullTag_ThrowsArgumentNullException` | **WHEN** `income.AddTag(null!)`. **THEN** `Assert.Throws<ArgumentNullException>`. |
| IT-5 | `AddTag_WithEmptyNameTag_Throws` *(opcional — nota ET-5 de F-004 §6.2)* | `Tag.Create` rechaza `Name` vacío, por lo que un `Tag` inválido no es construible vía factory público; el test blinda el guard defensivo de `AddTag` (`IsNullOrWhiteSpace(tag.Name)`). A discreción del implementador. |
| IT-6 | `RemoveTag_Existing_RemovesFromCollection` | **GIVEN** income con `tagA` + `tagB`. **WHEN** `income.RemoveTag(tagA)`. **THEN** `Assert.Single` + `Assert.Contains(tagB)` + `Assert.DoesNotContain(tagA, income.Tags)`. |
| IT-7 | `RemoveTag_NotInCollection_IsNoOp` | **GIVEN** income con `tagA` (no `tagB`). **WHEN** `income.RemoveTag(tagB)`. **THEN** `Assert.Single(income.Tags)` (idempotente). |
| IT-8 | `RemoveTag_WithNullTag_ThrowsArgumentNullException` | **WHEN** `income.RemoveTag(null!)`. **THEN** `Assert.Throws<ArgumentNullException>`. |

### 6.2 Integration tests — `ExpenseFilterTagTests.cs` (NEW)

Estilo de `ExpenseRepositoryTests`/`IncomeRepositoryTests` + sembrado de
tags del patrón §6. Helper `CreateTag` + `CreateExpense(description)`
(espejo de `ExpenseTagManyToManyTests` líneas 25-32).

| # | Test | GIVEN / WHEN / THEN |
| --- | --- | --- |
| FT-1 | `GetByFilterAsync_WithSingleTagId_ReturnsOnlyMatching` | **GIVEN** E1 con tagA, E2 con tagB, E3 sin tags (sembrados). **WHEN** `repo.GetByFilterAsync(new ExpenseFilter { TagIds = [tagA.Id] })`. **THEN** `Total == 1`, `Items.Single().Id == E1.Id`. |
| FT-2 | `GetByFilterAsync_WithMultipleTagIds_ReturnsExpensesWithAny` | **GIVEN** ídem FT-1. **WHEN** `TagIds = [tagA.Id, tagB.Id]`. **THEN** `Total == 2` (E1 y E2 — **OR/any-match**, D-1; E3 sin tags excluido). |
| FT-3 | `GetByFilterAsync_WithEmptyTagIds_ReturnsAll` | **GIVEN** ídem FT-1. **WHEN** `TagIds = Array.Empty<Guid>()`. **THEN** `Total == 3` (sin filtro, D-2). |
| FT-4 | `GetByFilterAsync_WithNullTagIds_ReturnsAll` | **GIVEN** ídem FT-1. **WHEN** `TagIds = null` (default). **THEN** `Total == 3` (sin filtro, D-2). |
| FT-5 | `GetByFilterAsync_WithNonExistentTagId_ReturnsEmpty` | **GIVEN** ídem FT-1. **WHEN** `TagIds = [Guid.NewGuid()]`. **THEN** `Total == 0` + `Items.Count == 0` (vacío, no error, D-2). |
| FT-6 | `GetByFilterAsync_WithCombinedCategoryAndTagIds_ReturnsIntersection` | **GIVEN** E1 (catX, tagA), E2 (catY, tagA), E3 (catX, tagB). **WHEN** `CategoryId = catX` + `TagIds = [tagA.Id]`. **THEN** `Total == 1`, `Items.Single().Id == E1.Id` (AND entre tipos de filtro, OR dentro de `TagIds`). |

### 6.3 Integration tests — `IncomeFilterTagTests.cs` (NEW)

Espejo de §6.2 para `Income` (helper `CreateIncome` con `categoryId`
nullable + 7º arg `source`, espejo de `IncomeRepositoryTests` líneas
389-406).

| # | Test | GIVEN / WHEN / THEN |
| --- | --- | --- |
| IF-1 | `GetByFilterAsync_WithTagIds_FiltersCorrectly` | **GIVEN** I1 con tagA, I2 con tagB, I3 sin tags. **WHEN** `repo.GetByFilterAsync(new IncomeFilter { TagIds = [tagA.Id] })`. **THEN** `Total == 1`, `Items.Single().Id == I1.Id` (regresión del campo antes-ignorado — F-002 D-2 invertido). |
| IF-2 | `GetByFilterAsync_WithMultipleTagIds_ReturnsIncomesWithAny` | **GIVEN** ídem IF-1. **WHEN** `TagIds = [tagA.Id, tagB.Id]`. **THEN** `Total == 2` (OR/any-match, D-1). |
| IF-3 | `GetByFilterAsync_WithEmptyTagIds_ReturnsAll` | **GIVEN** ídem IF-1. **WHEN** `TagIds = Array.Empty<Guid>()`. **THEN** `Total == 3` (sin filtro, D-2). |
| IF-4 | `GetByFilterAsync_WithCombinedCategoryAndTagIds_ReturnsIntersection` | **GIVEN** I1 (catX, tagA), I2 (catY, tagA), I3 (catX, tagB) — categorías sembradas del seed. **WHEN** `CategoryId = catX` + `TagIds = [tagA.Id]`. **THEN** `Total == 1`, `Items.Single().Id == I1.Id`. |
| IF-5 | `GetByFilterAsync_WithNullTagIds_ReturnsAll` *(opcional, simetría)* | **WHEN** `TagIds = null`. **THEN** `Total == 3`. |
| IF-6 | `GetByFilterAsync_WithNonExistentTagId_ReturnsEmpty` *(opcional, ratificado en ronda interactiva)* | **WHEN** `TagIds = [Guid.NewGuid()]`. **THEN** `Total == 0` (vacío, no error). |

### 6.4 Integration tests — `IncomeTagManyToManyTests.cs` (NEW)

Espejo estructural de `ExpenseTagManyToManyTests.cs` (179 líneas) con
`Income`/`Incomes`/`IncomeTags` en lugar de `Expense`/`Expenses`/
`ExpenseTags`. NOTA: `IncomeRepository` no se usa para hidratar Tags
(`FindAsync` no carga navegaciones — F-004 R-7); los tests consultan
`AppDbContext` directamente con `Include`.

| # | Test | GIVEN / WHEN / THEN |
| --- | --- | --- |
| MT-1 | `AddIncomeWithTags_PersistsBothAndJoinRows` | **GIVEN** `Income.Create(_comidaCategoryId, date, Money.Create(100m, DOP), "Sueldo", null, null, ts)` + 2 `Tag` persistidos vía `context.Tags.AddRange` + `SaveChangesAsync`. **WHEN** `income.AddTag(t1); income.AddTag(t2); context.Incomes.Add(income); await context.SaveChangesAsync();`. **THEN** `Income` persistido; `SqlQueryRaw<IncomeTagRow>("SELECT IncomeId, TagId FROM IncomeTags WHERE IncomeId = {0}", income.Id)` devuelve 2 filas `(income.Id, t1.Id)` y `(income.Id, t2.Id)`. |
| MT-2 | `ReloadIncomeWithTags_PopulatesTagsFromJoinTable` | **GIVEN** income con 2 tags persistido. **WHEN** `context.ChangeTracker.Clear(); var reloaded = await context.Incomes.AsNoTracking().Include(i => i.Tags).SingleAsync(i => i.Id == income.Id);`. **THEN** `reloaded.Tags.Count == 2` con los `Id` correctos (el `UsingEntity` hidrata la navegación). |
| MT-3 | `DeleteIncome_CascadesJoinRows` | **GIVEN** income con 1 tag persistido (`CountIncomeTagsAsync(context, income.Id) == 1`). **WHEN** `context.Incomes.Remove(income); await context.SaveChangesAsync();`. **THEN** join rows del income == 0; el `Tag` permanece intacto (`context.Tags.FindAsync(tag.Id)` not null) — cascade borra join, no el tag. |
| MT-4 | `DeleteTag_CascadesJoinRows` | **GIVEN** income con tagA + tagB persistidos. **WHEN** `await new TagRepository(context).RemoveAsync(tagA.Id);`. **THEN** join rows de tagA == 0; tagB y su join intactos; el income permanece persistido (`context.Incomes.FindAsync(income.Id)` not null). |

### 6.5 Integration tests — `IncomeRepositoryTests.cs` (modificación T-12)

El test T-12 (líneas 301-322) se transforma (D-7):

| # | Test (antes → después) | GIVEN / WHEN / THEN |
| --- | --- | --- |
| T-12 | `GetByFilterAsync_IgnoresTagIds_ForNow` → `GetByFilterAsync_WithTagIds_FiltersCorrectly` | **GIVEN** tagA persistido vía `context.Tags.Add(tagA); await context.SaveChangesAsync();` + I1 con `AddTag(tagA)` e I2 sin tags, persistidos vía `repo.AddAsync`. **WHEN** `repo.GetByFilterAsync(new IncomeFilter { TagIds = [tagA.Id] })`. **THEN** `Total == 1` + `Items.Single().Id == I1.Id` (**antes** sembraba 3 ingresos sin tags y afirmaba `Total == 3` con tag inexistente — contrato invertido). Misma posición en el archivo (12º), mismos helpers `CreateIncome`/`_comidaCategoryId`. |

**Cobertura global:** happy paths (IT-2, FT-1, IF-1, MT-1, MT-2), edge
cases (IT-1 colección vacía, IT-3/IT-7 no-ops, FT-3/FT-4/IF-3 sin
filtro, FT-5/IF-6 inexistente→vacío, FT-6/IF-4 combinación), error
states (IT-4/IT-8 null→throw, MT-3/MT-4 cascadas). Semántica OR blinda
por FT-2 + IF-2; el campo antes-ignorado queda blindado por T-12
renombrado + IF-1 (doble cobertura, D-7).

---

## 7. Fuera de alcance

F-007 **no** hace lo siguiente (cada ítem vive en otra feature):

- **Use cases / queries con `TagIds`** (`GetTransactionsQuery` con
  `TagIds`, `GetIncomesQuery`) y su validación (`Page ≥ 1`, `PageSize ≥
  1`, `DateFrom ≤ DateTo`, Guids no vacíos) — 4-008. El repo no valida
  el filtro (precedente F-002 D-8).
- **`SavedView`** + serialización versionada de filtros (`FilterDto`s,
  `SavedViewPayloadV1`) — 4-006.
- **Semánticas alternativas de `TagIds`** (AND/todos, conteo mínimo,
  exclusión) — solo OR (D-1).
- **UI de asignación/filtrado por tags** (`ExpensesView` multi-select,
  `IncomeView`) + ViewModels — 4-008.
- **Modificaciones a `Tag*`** (`Tag.cs`, `TagConfiguration.cs`,
  `TagRepository.cs`, `ITagRepository.cs`) — cerrados en 4-004.
- **Modificaciones a `Expense.cs` / `ExpenseConfiguration.cs`** (M:N ya
  en lugar desde F-004; solo se usan como plantilla — verify-only).
- **Segunda tabla join nueva** o cambios a `ExpenseTags` — solo
  `IncomeTags`.
- **Migraciones destructivas** o modificaciones a migraciones previas
  (1-5) / `SeedData.cs` (no se siembran tags — son del usuario).
- **Soft-delete** / `IsDeleted` / query filters (hard delete, eco de
  `Expense`/`Income`).
- **`Include` de `Tags` en los resultados de `GetByFilterAsync`/
  `GetAllAsync`/`GetByIdAsync`** (D-5 — el repo no hidrata Tags; F-004
  R-7).
- **`DbSet` para `IncomeTags`**, cambios a `AppDbContext.OnModelCreating`
  (la config se descubre vía `ApplyConfigurationsFromAssembly`), cambios
  a DI/`AddPersistence` (no hay repos nuevos).
- **Modificaciones a `Money`/`Currency`/`Result`/`Error`/`PagedResult`/
  `IClock`**, a las interfaces `IExpenseRepository`/`IIncomeRepository`
  (cero cambio de interface), a los fakes, a
  `TestAppDbContextFactory`, a los use cases existentes.
- **Paquetes NuGet nuevos** (en particular, no FluentAssertions, no
  mocking library). **CI workflow**, **`Directory.Packages.props`**,
  **`Directory.Build.props`**: sin cambios.

---

## 8. Plan de rollback

Todo es **aditivo y reversible por archivo** (la única mutación de
schema es una tabla join nueva sin datos pre-existentes). Revertir en
orden:

1. **Revertir la migración** (si se aplicó a una BD de usuario):
   `dotnet ef database update <previo> --project
   src/ControlDeFinanzasIA.Infrastructure --startup-project
   src/ControlDeFinanzasIA.UI`, luego `dotnet ef migrations remove
   AddIncomeTagsJoinTable` (elimina los 3 archivos generados). El
   `Down()` dropea `IncomeTags` — **sin pérdida de datos**: no existe
   dato income-tags antes de este feature.
2. **Revertir** `Income.cs` (quitar `Tags`, `AddTag`, `RemoveTag`,
   `using Domain.Catalog`) y `IncomeConfiguration.cs` (quitar el bloque
   M:N — el `OwnsOne(Money)` queda intacto porque el bloque se añadió
   después, sin tocar líneas existentes).
3. **Revertir** `ExpenseFilter.cs` (quitar `TagIds` — default `null`
   significa que los callers existentes nunca dependieron de ella),
   `ExpenseRepository.cs` y `IncomeRepository.cs` (quitar el bloque
   `TagIds`; en Income, **restaurar** el marcador `_ = filter.TagIds;`
   + su comentario y remarks "hasta 4-007").
4. **Eliminar** los archivos de tests nuevos:
   `ExpenseFilterTagTests.cs`, `IncomeFilterTagTests.cs`,
   `IncomeTagManyToManyTests.cs`; **revertir** la extensión de
   `IncomeTests.cs` (sección Tags + helper `CreateTag`).
5. **Restaurar T-12** a su forma original
   (`GetByFilterAsync_IgnoresTagIds_ForNow` con el contrato "ignorado")
   + los remarks de `IncomeFilter.cs`/`IncomeRepositoryTests.cs`.
6. **Verificar** `dotnet build` limpio y `dotnet test` suite previa en
   verde (estado pre-F-007, incluido el fallo pre-existing de `Money`).
   NetArchTest R-1..R-5 no se afectan en ningún paso: `Income.Tags` es
   intra-Domain (R-1 OK); los cambios de repo/config están en
   Infrastructure (R-3 OK); `ExpenseFilter` en Application (R-2 OK).

---

## 9. Riesgos y mitigaciones

| # | Riesgo | Prob. | Impacto | Mitigación |
| --- | --- | --- | --- | --- |
| R-1 | Traducción LINQ de `Tags.Any(t => Contains(t.Id))` sobre SQLite (navegación skip + colección primitiva `IReadOnlyList<Guid>`) — primera query de este tipo en el codebase. | Media | Alto | D-5 con patrón plano (sin expressions dinámicas); integration tests con todas las combinaciones (FT-1..FT-6, IF-1..IF-4, T-12); índices `IX_ExpenseTags_TagId` (existente) e `IX_IncomeTags_TagId` (nuevo) soportan el `EXISTS`. Fallback documentado: capturar local `var tagIds = filter.TagIds;` y/o `.ToArray()` si EF Core no parameteriza `IReadOnlyList<Guid>` — el shape del guard no cambia. |
| R-2 | `Income.Tags` no se inicializa en el ctor sin params (línea 71); EF Core la deja `null` al materializar → `NullReferenceException` en `AddTag`. | Media | Alto | AC-1 exige init en **AMBOS** ctors (espejo de `Expense` líneas 72/106); IT-1 (`NewIncome_HasEmptyNotNullTagsCollection`) blinda; MT-2 (reload con `Include`) lo prueba end-to-end. |
| R-3 | La migración generada trae FKs `Restrict` en lugar de `Cascade` (defaults de EF Core para skip navigations) → cascadas rotas y `DeleteIncome` fallando con referencias. | Media | Alto | AC-2 especifica `.OnDelete(DeleteBehavior.Cascade)` explícito en ambos lados (copiado del fluent verificado de `ExpenseConfiguration` líneas 60-77); AC-3 verifica el DDL generado (`onDelete: ReferentialAction.Cascade`); MT-3/MT-4 verifican el cascade real. |
| R-4 | T-12 olvidada → suite rota (el test existente `GetByFilterAsync_IgnoresTagIds_ForNow` afirmaría `Total == 3` pero el filtro ahora aplica → fail). | Media | Medio | AC-7 explícito (rename + flip, NO eliminar); es la única test existente cuyo contrato se invierte. El gate de AC-13 con filtro `~IncomeRepository` la captura inmediatamente. |
| R-5 | El generador de EF emite namespace block-scoped en la migración → IDE0161 + `dotnet format style` falla. | Media | Bajo | AC-3 exige file-scoped namespace con corrección manual (precedente F-001 P4 §17 C-5, replicado en F-004 AC-8); gate `dotnet format` en AC-13. |
| R-6 | El bloque M:N en `IncomeConfiguration` rompe el `OwnsOne(Money)` existente (reordenar o editar líneas por accidente) → round-trip de `Amount` roto. | Baja | Alto | AC-2: el bloque se **añade después** del `OwnsOne`, cero líneas existentes editadas; `IncomeSchemaTests` existentes (round-trip `OwnsOne`, ST-3 de F-001 P4) siguen en la suite como red. |
| R-7 | Warning CS8602 dentro del lambda (`filter.TagIds.Contains` sin `!`) rompe el gate de 0 warnings. | Media | Bajo | D-5 documenta el null-forgiving `filter.TagIds!` como **requerido** (el guard `is { Count: > 0 }` no es rastreado a través de reads de propiedades hacia el lambda); gates de build en AC-13. |
| R-8 | Engram `mem_save` no disponible en el toolset del sub-agente (verificado: sin funciones `mem_*`). | Alta (verificado) | Bajo | Respaldo en filesystem: proposal ya existe en `.atl/sdd/f-007p4-extend-filters-tagids/proposal.md`; spec en este archivo. El orquestador persiste a Engram con los topic keys `sdd/f-007p4-extend-filters-tagids/{proposal,spec}` (precedente F-002 P4 R-4). El sub-agente NO fabrica observation IDs. |

---

## 10. Referencias

- **Propuesta (filesystem):**
  `.atl/sdd/f-007p4-extend-filters-tagids/proposal.md` (196 líneas) — PRD
  de origen. D-0/Opción A ratificada en ronda interactiva; su D-5
  ("eliminar T-12") fue superado por D-7 de esta spec (update in-place,
  ratificado en la misma ronda); su AC-12 (refrescar doc-comments) se
  conserva como excepción doc-only en AC-14.
- **Propuesta (Engram objetivo):** topic key
  `sdd/f-007p4-extend-filters-tagids/proposal` (pendiente de
  persistencia por el orquestador — ver R-8).
- **Spec (Engram objetivo):** topic key
  `sdd/f-007p4-extend-filters-tagids/spec` (pendiente de persistencia
  por el orquestador — ver R-8).
- **F-002 Fase 4 (archivada — patrón espejo filtros Income):**
  `spec/features/phase-04/F-002-income-repository.done.md` (728 líneas)
  — `IncomeFilter` 6 props (D-2 de F-002: el placeholder `TagIds` que
  esta spec enciende), `IncomeRepository`, fixture de tests, T-12.
- **F-004 Fase 4 (archivada — patrón espejo M:N):**
  `spec/features/phase-04/F-004-tag-entity-and-many-to-many.done.md`
  (~950 líneas) — M:N `UsingEntity` (D-7 de F-004),
  `AddTag`/`RemoveTag` idempotentes (D-2/D-6 de F-004), migración join
  (D-3 de F-004), `ExpenseTagManyToManyTests`.
- **F-003 Fase 3 (archivada — contrato base del filtro):**
  `spec/features/phase-03/F-003-expense-repository.done.md` —
  `ExpenseRepository.GetByFilterAsync` con `CategoryId`/`DateFrom`/
  `DateTo` que F-007 extiende.
- **Phase plan:** `spec/phases/phase-04/features.md` — líneas 45-48
  (alcance de filtros), §RF-18 (línea 236, extender
  `ExpenseRepository.GetByFilterAsync` con `TagIds`), §RF-20 (línea
  250, "queries con TagIds" para `IncomeRepository`), §9 tabla 4-007,
  §11 risk "mapeo de filtros a EF Core".
- **Convención de contrato:** `spec/features/phase-01/F-006-contrato-interfaces-repo.done.md`
  (commands `Task<Result>` vs queries `Task<T>`).
- **Tipos comunes:** `spec/features/phase-01/F-005-comuntypes-result-pagedresult.done.md`
  (`Result` non-generic con `Value: object?`, `PagedResult<T>` 4-arg).
- **Test infra:** `spec/features/phase-02/F-006-integration-tests.done.md`
  (`TestAppDbContextFactory`, SQLite in-memory compartida).
- **Arquitectura:** `docs/architecture.md` §4 (R-1..R-5), §8 (mapeo
  Fluent API + migraciones versionadas), §12 (single-user local —
  justifica hard-delete y cascadas).
- **Constitución:** `spec/constitution/mission.md`,
  `spec/constitution/tech-stack.md` (async/await extremo a extremo,
  errores como valores, "no silent failures").
- **Código verificado (fuentes de esta spec):**
  `src/.../Abstractions/ExpenseFilter.cs` (42 líneas — 5 props, sin
  `TagIds`),
  `src/.../Abstractions/IncomeFilter.cs` (52 líneas — 6 props con
  placeholder + remarks "hasta 4-007" líneas 13-16/49),
  `src/.../Abstractions/IExpenseRepository.cs` + `IIncomeRepository.cs`
  (`GetByFilterAsync` en línea 70 de cada una — cero cambio de
  interface),
  `src/.../Domain/Transactions/Income.cs` (268 líneas — SIN Tags; ctors
  privados líneas 71/89-109),
  `src/.../Domain/Transactions/Expense.cs` (290 líneas — plantilla
  `Tags`/`AddTag`/`RemoveTag` líneas 64/198-230, init en ambos ctors
  líneas 72/106),
  `src/.../Configurations/IncomeConfiguration.cs` (57 líneas — SIN M:N;
  `OwnsOne` líneas 45-55),
  `src/.../Configurations/ExpenseConfiguration.cs` (79 líneas — plantilla
  M:N líneas 56-77),
  `src/.../Repositories/ExpenseRepository.cs` (212 líneas —
  `GetByFilterAsync` líneas 178-211),
  `src/.../Repositories/IncomeRepository.cs` (223 líneas — marcador
  `_ = filter.TagIds;` líneas 207-208, remarks líneas 175-178),
  `src/.../Migrations/20260827223645_AddTagsAndExpenseTags.cs` (70
  líneas — plantilla Up/Down),
  `tests/.../Persistence/IncomeRepositoryTests.cs` (426 líneas — T-12
  líneas 301-322, remarks clase línea 19),
  `tests/.../Persistence/ExpenseRepositoryTests.cs` (estilo de tests de
  filtro),
  `tests/.../Persistence/ExpenseTagManyToManyTests.cs` (179 líneas —
  plantilla M:N tests),
  `tests/.../Persistence/TestAppDbContextFactory.cs` (49 líneas — API
  `CreateContext()`),
  `tests/.../Tests.Unit/Domain/ExpenseTests.cs` (sección Tags líneas
  280-359 — plantilla unit tests),
  `tests/.../Tests.Unit/Domain/IncomeTests.cs` (285 líneas — archivo a
  extender, helper `CreateValidIncome` + `_defaultCreatedAt` línea 20).
