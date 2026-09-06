# Tareas — F-002 (Phase 4): `IncomeRepository` + `IncomeFilter` + tests de integración

> **Spec:** `spec/features/phase-04/F-002-income-repository.spec.md`
> **change_id:** `f-002p4-income-repository`
> **Estado:** ✅ COMPLETADA
> **Fecha:** 2026-08-27
> **Fuentes de verdad:** la spec, `docs/architecture.md` §3, §4, §7, §8,
> `spec/constitution/tech-stack.md` (.NET 8, EF Core 8, SQLite),
> `spec/constitution/mission.md` (dominio puro, sin duplicación),
> `spec/phases/phase-04/features.md` (RF-3, RF-4, RF-5),
> `spec/features/phase-04/F-001-income-entity-config-migration.done.md`
> (`Income`, `IncomeConfiguration`, migración `AddIncomesTable`),
> `spec/features/phase-03/F-003-expense-repository.done.md`
> (patrón espejo: `ExpenseRepository` / `ExpenseFilter` / `ExpenseRepositoryTests`).
>
> Cada tarea es implementable en menos de 2 horas. El proyecto debe seguir
> compilando al finalizar cada una. La numeración (T-001...) es local a esta
> feature. F-002 modifica `Application/Abstractions/`,
> `Infrastructure/Persistence/` y `Tests.Integration/Persistence/`.
> NO modifica `Domain`, `UI`, `Tests.Unit`, `Directory.Packages.props`,
> migraciones ni el CI workflow.

---

## Pronóstico de carga de review

| Field | Value |
|-------|-------|
| Estimated changed lines | ~360-390 |
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
| 1 | Crear `IncomeFilter`, extender `IIncomeRepository`, implementar `IncomeRepository`, registrar DI y tests de integración | PR 1 | Base = `main`; incluye build limpio, tests y formato |

---

## Resumen

F-002 (Phase 4) cierra el **lado de persistencia del slice vertical de `Income`**.
Entrega el record `IncomeFilter` (6 props incluyendo `TagIds` como placeholder de
estabilidad de firma), extiende `IIncomeRepository` con `GetByFilterAsync`,
implementa `IncomeRepository` como espejo de `ExpenseRepository` (adaptando
`CategoryId` nullable y el 7º parámetro `Source` de `Income.Create`), registra el
repo en `AddPersistence` y lo cubre con 14 tests de integración SQLite usando
pure xUnit `Assert.*`. No toca `Domain`, no genera migraciones y mantiene
NetArchTest R-1..R-5 verdes.

---

## Resumen de tareas

| ID | Tarea | Descripción corta | Estimación diff | Depende de | Verificación | Estado |
| --- | --- | --- | --- | --- | --- | --- |
| T-001 | Crear `IncomeFilter.cs` | Record `sealed` tonto con 6 props `init` + XML doc por propiedad | +25 / -0 | ninguna | `dotnet build` exit 0 | [x] |
| T-002 | Extender `IIncomeRepository.cs` | Añadir 6º método `GetByFilterAsync(IncomeFilter, ct)` + XML doc | +12 / -0 | ninguna | `dotnet build` exit 0 | [x] |
| T-003 | Crear `IncomeRepository.cs` | 6 métodos: CRUD, validación FK condicional, orden `Date desc, CreatedAt desc` | +190 / -0 | T-001, T-002 | `dotnet build` exit 0 | [x] |
| T-004 | Registrar `IIncomeRepository` en `AddPersistence` | `AddScoped<IIncomeRepository, IncomeRepository>()` + XML doc | +10 / -0 | T-003 | `dotnet build` exit 0 | [x] |
| T-005 | Crear `IncomeRepositoryTests.cs` | 14 tests CRUD + filtros + paginación + `TagIds` ignorado | +250 / -0 | T-003 | `dotnet test --filter "FullyQualifiedName~IncomeRepository"` 14 passed | [x] |
| T-006 | Verificación final, formato y cierre | Build, tests, NetArchTest, format, scope check | +2 / -2 | T-001 a T-005 | `dotnet test` verde; `dotnet format style --verify-no-changes` exit 0 | [x] |

**Total estimado:** ~media jornada. T-003 (repo) y T-005 (tests) son las más densas;
T-001, T-002 y T-004 son mecánicas; T-006 es verificación y cierre administrativo.

---

## T-001 — Crear `IncomeFilter.cs`

**AC refs:** AC-1

**Pre-condiciones**
- F-001 Phase 4 archivada (`Income`, `IncomeConfiguration`, migración `AddIncomesTable`).
- `ExpenseFilter.cs` disponible como referencia de forma (`init` props + XML doc por propiedad).

**Pasos**
1. Crear `src/ControlDeFinanzasIA.Application/Abstractions/IncomeFilter.cs`.
2. Usar file-scoped namespace `ControlDeFinanzasIA.Application.Abstractions`.
3. Declarar `public sealed record IncomeFilter` con propiedades `init` y defaults:
   - `int Page { get; init; } = 1`
   - `int PageSize { get; init; } = 50`
   - `Guid? CategoryId { get; init; }`
   - `DateOnly? DateFrom { get; init; }`
   - `DateOnly? DateTo { get; init; }`
   - `IReadOnlyList<Guid>? TagIds { get; init; }` (placeholder ignorado hasta 4-007)
4. Añadir XML doc en el tipo + cada propiedad (espejo `ExpenseFilter`).
5. Ejecutar `dotnet build -c Release --nologo`.

**Criterios de verificación**
- `dotnet build` exit 0, 0 warnings, 0 errors.
- `IncomeFilter` es `sealed record`, file-scoped namespace, 6 props `init`, defaults correctos.
- XML doc presente en el tipo y en cada propiedad.
- Sin métodos de validación ni lógica.

**Notas**
- `TagIds` existe por estabilidad de firma (D-2); el repo lo ignora en F-002.
- No usar notación posicional: se necesita XML doc por propiedad.

---

## T-002 — Extender `IIncomeRepository.cs`

**AC refs:** AC-2

**Pre-condiciones**
- T-001 completada (`IncomeFilter` existe).
- Verificado que `IIncomeRepository.cs` tiene 5 métodos y ya referencia
  `using ControlDeFinanzasIA.Application.Common;` para `PagedResult<T>`.

**Pasos**
1. Abrir `src/ControlDeFinanzasIA.Application/Abstractions/IIncomeRepository.cs`.
2. Añadir tras `GetAllAsync`:
   ```csharp
   Task<PagedResult<Income>> GetByFilterAsync(IncomeFilter filter, CancellationToken ct = default);
   ```
3. Añadir XML doc espejando `IExpenseRepository` líneas 58-70 (paginación,
   orden `Date desc, CreatedAt desc`).
4. Confirmar que no se requiere `using` nuevo (`IncomeFilter` mismo namespace;
   `PagedResult<T>` ya cubierto).
5. Ejecutar `dotnet build -c Release --nologo`.

**Criterios de verificación**
- `dotnet build` exit 0, 0 warnings, 0 errors.
- `IIncomeRepository` expone exactamente 6 métodos.
- Los 5 métodos originales conservan sus firmas exactas.
- Nuevo método con XML doc completo.

**Notas**
- Decisión locked D-1: modificar la interface es in-scope y necesario.
- No añadir `using ControlDeFinanzasIA.Application.Abstractions;` (ya está en el namespace).

---

## T-003 — Crear `IncomeRepository.cs`

**AC refs:** AC-3, AC-4, AC-5, AC-6, AC-7, AC-8, AC-9

**Pre-condiciones**
- T-001 y T-002 completadas.
- `Income` entrega `CategoryId` nullable y `Income.Create` con 7 params (incl. `source`).
- `IncomeConfiguration` usa FK `SetNull` y `OwnsOne` de `Money`.

**Pasos**
1. Crear `src/ControlDeFinanzasIA.Infrastructure/Persistence/Repositories/IncomeRepository.cs`.
2. File-scoped namespace `ControlDeFinanzasIA.Infrastructure.Persistence.Repositories`.
3. Añadir `using` necesarios:
   - `ControlDeFinanzasIA.Application.Abstractions`
   - `ControlDeFinanzasIA.Application.Common`
   - `ControlDeFinanzasIA.Domain.Transactions`
   - `Microsoft.EntityFrameworkCore`
4. Declarar `public sealed class IncomeRepository : IIncomeRepository`.
5. Campo `private readonly AppDbContext _db;` y ctor `public IncomeRepository(AppDbContext db)`.
6. Implementar `AddAsync` (AC-4):
   - `ArgumentNullException.ThrowIfNull(income);`
   - Si `income.CategoryId is { } catId`, hacer `_db.Categories.FindAsync(new object?[] { catId }, ct)`;
     si `null` → `Result.Failure("CategoryNotFound", $"La categoría {catId} no existe.");`
   - Si `CategoryId == null`, omitir chequeo.
   - `_db.Incomes.Add(income); await _db.SaveChangesAsync(ct); return Result.Success(null!);`
7. Implementar `UpdateAsync` (AC-5) espejando `ExpenseRepository`:
   - `FindAsync` por `Id`; si `null` → `Result.Failure("NotFound", $"Ingreso {income.Id} no encontrado.");`
   - `_db.Entry(existing).CurrentValues.SetValues(income); await _db.SaveChangesAsync(ct);`
8. Implementar `RemoveAsync` (AC-6): guard `Guid.Empty`, `FindAsync`, `Remove`, `SaveChangesAsync`.
9. Implementar `GetByIdAsync` (AC-7): guard `Guid.Empty → null`; `FindAsync`.
10. Implementar `GetAllAsync` (AC-8): `AsNoTracking().OrderByDescending(Date).ThenByDescending(CreatedAt).ToListAsync()`.
11. Implementar `GetByFilterAsync` (AC-9): `AsNoTracking()`, filtros opcionales
    `CategoryId`/`DateFrom`/`DateTo`, **ignorar `TagIds`**, orden, `CountAsync`,
    `Skip`, `Take`, `new PagedResult<Income>(...)`,
12. Añadir XML doc en clase y cada método público.
13. Ejecutar `dotnet build -c Release --nologo`.

**Criterios de verificación**
- `dotnet build` exit 0, 0 warnings, 0 errors.
- `IncomeRepository` es `sealed`, implementa 6 métodos, `_db` inyectado.
- `AddAsync` salta validación FK cuando `CategoryId == null`.
- `GetAllAsync` y `GetByFilterAsync` ordenan `Date desc, CreatedAt desc`.
- `GetByFilterAsync` ignora explícitamente `TagIds` (comentario en código).

**Notas**
- Diferencia clave vs `ExpenseRepository`: `CategoryId` nullable + FK `SetNull`.
- `Income.Create` ya rechaza `Guid.Empty`; no añadir guard extra en el repo.
- No capturar `DbUpdateException`; no re-validar FK en `UpdateAsync`.

---

## T-004 — Registrar `IIncomeRepository` en `AddPersistence`

**AC refs:** AC-10

**Pre-condiciones**
- T-003 completada (`IncomeRepository` compila).
- `ServiceCollectionExtensions.cs` ya registra `IExpenseRepository`.

**Pasos**
1. Abrir `src/ControlDeFinanzasIA.Infrastructure/Persistence/ServiceCollectionExtensions.cs`.
2. Añadir `services.AddScoped<IIncomeRepository, IncomeRepository>();` tras la línea de `IExpenseRepository`.
3. Actualizar XML doc del método `AddPersistence` para listar `IIncomeRepository`
   (summary + `<para>` referenciando F-002 Phase 4).
4. Confirmar que `using ControlDeFinanzasIA.Infrastructure.Persistence.Repositories;`
   ya está presente (línea 2).
5. Ejecutar `dotnet build -c Release --nologo`.

**Criterios de verificación**
- `dotnet build` exit 0, 0 warnings, 0 errors.
- `AddScoped<IIncomeRepository, IncomeRepository>()` aparece en `AddPersistence`.
- XML doc menciona `IIncomeRepository`.

**Notas**
- Registro scoped, igual que los demás repositorios.
- No modificar `Bootstrap.ConfigureServices` (F-002 solo extiende `AddPersistence`).

---

## T-005 — Crear `IncomeRepositoryTests.cs`

**AC refs:** AC-11

**Pre-condiciones**
- T-003 completada (`IncomeRepository` implementado).
- `TestAppDbContextFactory` existe con API `new TestAppDbContextFactory()` +
  `CreateContext()` + `MigrateAsync()`.

**Pasos**
1. Crear `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/IncomeRepositoryTests.cs`.
2. File-scoped namespace `ControlDeFinanzasIA.Tests.Integration.Persistence`.
3. Añadir `using` al tope:
   - `ControlDeFinanzasIA.Application.Abstractions`
   - `ControlDeFinanzasIA.Application.Common`
   - `ControlDeFinanzasIA.Domain.Enums`
   - `ControlDeFinanzasIA.Domain.Transactions`
   - `ControlDeFinanzasIA.Domain.ValueObjects`
   - `ControlDeFinanzasIA.Infrastructure.Persistence`
   - `ControlDeFinanzasIA.Infrastructure.Persistence.Repositories`
   - `Xunit`
4. Declarar `public sealed class IncomeRepositoryTests`.
5. Campos estáticos:
   - `_fixedTimestamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)`
   - `_comidaCategoryId = new Guid("11111111-1111-1111-1111-000000000001")`
   - segunda categoría sembrada para tests de filtro
6. Implementar helper `CreateIncome(Guid? categoryId, DateOnly date, decimal amount, string description, DateTime? createdAt = null, string? note = null, string? source = null)` usando `Income.Create(...)`.
7. Implementar 14 tests:
   - `AddAsync_WithValidIncome_Persists`
   - `AddAsync_WithUnknownCategoryId_ReturnsCategoryNotFound`
   - `AddAsync_WithNullCategoryId_Persists`
   - `GetByIdAsync_Existing_ReturnsIncome`
   - `GetByIdAsync_Missing_ReturnsNull`
   - `GetByIdAsync_WithEmptyGuid_ReturnsNull`
   - `GetAllAsync_ReturnsAllOrderedByDateDesc`
   - `GetByFilterAsync_NoFilter_ReturnsFirstPageOrdered`
   - `GetByFilterAsync_WithCategoryId_FiltersCorrectly`
   - `GetByFilterAsync_WithDateRange_FiltersCorrectly`
   - `GetByFilterAsync_Paginates_CorrectTotalAndItems`
   - `GetByFilterAsync_IgnoresTagIds_ForNow`
   - `UpdateAsync_Existing_UpdatesFields`
   - `RemoveAsync_Existing_DeletesIncome`
8. En cada test usar el patrón verificado:
   ```csharp
   using var factory = new TestAppDbContextFactory();
   using AppDbContext context = factory.CreateContext();
   await context.Database.MigrateAsync();
   var repo = new IncomeRepository(context);
   ```
9. Aserciones con `Assert.True`, `Assert.False`, `Assert.Equal`, `Assert.Null`,
   `Assert.NotNull`, `Assert.Single`, `Assert.Contains` — **sin FluentAssertions**.
10. Ejecutar `dotnet test -c Debug --filter "FullyQualifiedName~IncomeRepository"`.

**Criterios de verificación**
- `dotnet build` exit 0, 0 warnings, 0 errors.
- `dotnet test -c Debug --no-build --filter "FullyQualifiedName~IncomeRepository"` → 14 passed, 0 failed.
- File-scoped namespace, `sealed`, `using` al tope, `MigrateAsync()` presente.
- Helper `CreateIncome` usa `Guid?` para `categoryId` y 7º arg `source`.

**Notas**
- No usar `TestAppDbContextFactory.CreateAsync()` — no existe.
- `UpdateAsync_Existing_UpdatesFields` debe verificar `Amount`, `Currency`, `Source`, `Note`, `CreatedAt` inmutable y `UpdatedAt`.
- `AddAsync_WithNullCategoryId_Persists` es la rama distintiva de `Income`.

---

## T-006 — Verificación final, formato y cierre

**AC refs:** AC-12, AC-13

**Pre-condiciones**
- T-001 a T-005 completadas.

**Pasos**
1. Ejecutar `dotnet build -c Release --nologo` → 0 warnings, 0 errors.
2. Ejecutar `dotnet build -c Debug --nologo` → 0 warnings, 0 errors.
3. Ejecutar `dotnet test -c Debug --no-build --nologo` → suite completa verde.
4. Ejecutar `dotnet test -c Debug --no-build --nologo --filter "FullyQualifiedName~ArchitectureTests"` → 11/11 passed.
5. Ejecutar `dotnet format style --verify-no-changes` → exit 0.
6. Ejecutar `dotnet list src/ControlDeFinanzasIA.Domain package` → vacío (R-1).
7. Verificar scope: ninguna modificación a `Expense*`, `Income.cs`,
   `IncomeConfiguration.cs`, `AppDbContext.cs`, `Money.cs`, `Currency.cs`,
   `Result.cs`, `PagedResult.cs`, migraciones, UI.
8. Verificar que no se añadieron paquetes NuGet ni FluentAssertions.
9. Actualizar este archivo de tareas marcando todas las casillas `[x]`.
10. (Opcional) Reportar conteos exactos de tests observados en AC-12.

**Criterios de verificación**
- `dotnet build -c Release` exit 0.
- `dotnet test -c Debug --no-build` suite verde (baseline + 14 nuevos).
- `dotnet test --filter "FullyQualifiedName~ArchitectureTests"` 11/11 passed.
- `dotnet format style --verify-no-changes` exit 0.
- Ningún archivo fuera de la lista in-scope modificado.

**Notas**
- No renombrar la spec a `.done.md`; eso es trabajo de `sdd-archive` de la fase.
- Si `features.md` necesita actualización, el implementador lo hace solo si
  confirmó que F-002 está completada.

---

## Orden de ejecución recomendado

**Orden secuencial con paralelismo inicial:**

1. **T-001** y **T-002** pueden ejecutarse en cualquier orden (o en paralelo);
   no se bloquean mutuamente. Ambas deben terminar antes de T-003.
2. **T-003** depende de T-001 y T-002. Es la entrega funcional principal.
3. **T-004** depende de T-003 (necesita que `IncomeRepository` compile).
4. **T-005** depende de T-003 (los tests invocan el repo concreto).
5. **T-006** es verificación y cierre de toda la feature.

```text
T-001 ──┐
        ├→ T-003 → T-004 ─┐
T-002 ──┘                 ├→ T-006
                          │
                     T-005 ┘
```

---

## Tareas bloqueantes / habilitadoras

| Tarea | Habilita | Razón |
| --- | --- | --- |
| T-001 | T-003 | `IncomeRepository.GetByFilterAsync` recibe `IncomeFilter` como parámetro. |
| T-002 | T-003 | `IncomeRepository` implementa `IIncomeRepository`, que debe incluir `GetByFilterAsync`. |
| T-003 | T-004 | `AddPersistence` referencia la clase `IncomeRepository`; debe compilar. |
| T-003 | T-005 | Los tests instancian `IncomeRepository` y ejecutan sus 6 métodos. |
| T-004 | T-006 | El registro DI se verifica indirectamente vía NetArchTest R-3 y build. |
| T-005 | T-006 | Todos los tests deben pasar antes del cierre. |

---

## Estimación total

| Tarea | Dificultad | Horas estimadas |
| --- | --- | --- |
| T-001 | XS | 0.25 |
| T-002 | XS | 0.25 |
| T-003 | M | 2 |
| T-004 | XS | 0.25 |
| T-005 | M | 2-3 |
| T-006 | S | 0.5-1 |
| **Total** | | **~5.5-7 horas** |

---

## Riesgos y mitigaciones

| Riesgo | Probabilidad | Impacto | Mitigación |
| --- | --- | --- | --- |
| `IIncomeRepository` tenía 5 métodos y se omite extenderla | Alta (resuelta) | Medio | D-1 locked: modificar la interface en T-002. |
| `AddAsync` con `CategoryId == null` se olvida el skip del chequeo FK | Media | Medio | D-5 + test T-3 (`AddAsync_WithNullCategoryId_Persists`). |
| `CurrentValues.SetValues` no copia `Money` embebida (`OwnsOne`) en `UpdateAsync` | Media | Alto | Test T-13 verifica round-trip de `Amount`/`Currency`. |
| El implementador usa `TestAppDbContextFactory.CreateAsync()` (no existe) | Media | Bajo | AC-11 + T-005 documentan el patrón verificado. |
| `IncomeFilter` se implementa como record posicional y pierde XML doc por propiedad | Media | Bajo | D-3 + AC-1 fijan forma `init` con XML doc por propiedad. |
| `Skip` negativo (`Page = 0`) lanza en EF Core | Baja | Bajo | D-8: el repo no valida; el caller valida `Page ≥ 1`. |
| `dotnet format style` propone cambios inesperados | Baja | Bajo | Aplicar los cambios. No relajar reglas globales. |
| Se añade FluentAssertions por inercia de Phase 3 | Baja | Bajo | AC-11/T-005 exigen pure xUnit `Assert.*`; revisar en PR. |

---

## Notas para apply

- **No tocar `Domain/Transactions/Income.cs`.** F-001 ya entregó la entidad.
- **No añadir FluentAssertions.** Los tests usan pure xUnit `Assert.*`.
- **No validar `IncomeFilter` en el repo.** Esa validación es del caller (F-003/F-008).
- **Códigos de error en inglés, mensajes en español.** `"NotFound"`, `"CategoryNotFound"`;
  mensajes como `"La categoría {id} no existe."`.
- **Hard delete en `RemoveAsync`.** No soft-delete, no `IsDeleted`, no query filter.
- **`IncomeFilter` va en `Application/Abstractions/` (flat).** No crear subcarpetas.
- **Reutilizar `TestAppDbContextFactory` existente.** No inventar una factoría nueva.
- **`TagIds` es placeholder:** existe en `IncomeFilter` pero `GetByFilterAsync` lo ignora.
- **No nuevas migraciones:** F-001 ya creó `AddIncomesTable`.

---

## Definition of Done (DoD)

- [x] AC-1: `IncomeFilter.cs` existe como `sealed record` con 6 props `init` y XML doc. (T-001)
- [x] AC-2: `IIncomeRepository` expone 6 métodos incluyendo `GetByFilterAsync`. (T-002)
- [x] AC-3: `IncomeRepository.cs` es `sealed class : IIncomeRepository` con ctor `AppDbContext`. (T-003)
- [x] AC-4: `AddAsync` valida FK solo si `CategoryId != null`; `null` persiste OK. (T-003)
- [x] AC-5: `UpdateAsync` actualiza vía `CurrentValues.SetValues`. (T-003)
- [x] AC-6: `RemoveAsync` hard delete con guard `Guid.Empty`. (T-003)
- [x] AC-7: `GetByIdAsync` guard `Guid.Empty → null`. (T-003)
- [x] AC-8: `GetAllAsync` ordena `Date desc, CreatedAt desc` con `AsNoTracking`. (T-003)
- [x] AC-9: `GetByFilterAsync` aplica filtros, pagina, ignora `TagIds`. (T-003)
- [x] AC-10: `AddPersistence` registra `IIncomeRepository → IncomeRepository` scoped. (T-004)
- [x] AC-11: `IncomeRepositoryTests.cs` tiene 14 tests verdes con SQLite in-memory. (T-005)
- [x] AC-12: Build limpio, suite verde, NetArchTest R-1..R-5 verdes, format exit 0. (T-006)
- [x] AC-13: Sin scope creep (no use cases, no UI, no validación de filtro en repo, no soft-delete, no migraciones, no FluentAssertions). (T-006)

---

## Referencias

- `spec/features/phase-04/F-002-income-repository.spec.md` — la spec completa de F-002 P4.
- `spec/phases/phase-04/features.md` — F-002 dentro del desglose de la fase 4.
- `spec/features/phase-04/F-001-income-entity-config-migration.done.md` — entidad `Income` (espejo).
- `spec/features/phase-03/F-003-expense-repository.done.md` — patrón espejo `ExpenseRepository`.
- `src/ControlDeFinanzasIA.Application/Abstractions/IExpenseRepository.cs` — espejo de interface extendida.
- `src/ControlDeFinanzasIA.Application/Abstractions/ExpenseFilter.cs` — espejo del record `init`.
- `src/ControlDeFinanzasIA.Infrastructure/Persistence/Repositories/ExpenseRepository.cs` — espejo de implementación.
- `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/ExpenseRepositoryTests.cs` — espejo de tests.
- `src/ControlDeFinanzasIA.Domain/Transactions/Income.cs` — entidad `Income` (no modificar).
- Engram `sdd/f-002p4-income-repository/proposal` y `/spec` (objetivo de persistencia por el orquestador).
