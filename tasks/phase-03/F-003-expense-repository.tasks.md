# Tareas — F-003: `ExpenseRepository` + query paginado `GetByFilterAsync`

> **Spec:** `spec/features/phase-03/F-003-expense-repository.spec.md`
> **Fuentes de verdad:** la spec, `docs/vision.md` §4.1, §4.3,
> `docs/architecture.md` §3, §4, §7, §8,
> `docs/roadmap.md` §Fase 3,
> `spec/constitution/tech-stack.md` (.NET 8, EF Core 8, SQLite),
> `spec/constitution/mission.md` (dominio puro, sin duplicación),
> `spec/phases/phase-03/features.md` (F-003 dentro del desglose de la fase 3),
> `spec/features/phase-03/F-001-expense-domain-entity.done.md` (provee `Expense`),
> `spec/features/phase-03/F-002-expense-configuration-migration.done.md`
> (provee tabla `Expenses`, FK `Restrict`, `OwnsOne` de `Money`),
> `spec/features/phase-01/F-006-contrato-interfaces-repo.done.md`
> (`IExpenseRepository` con 5 métodos estándar),
> `spec/features/phase-01/F-005-comuntypes-result-pagedresult.done.md`
> (`Result` y `PagedResult<T>`),
> `spec/features/phase-02/F-004-category-paymentmethod-repositories.done.md`
> (patrón de repositorio a replicar),
> `spec/features/phase-02/F-006-integration-tests.done.md`
> (`TestAppDbContextFactory`).
>
> Cada tarea es implementable en menos de 2 horas. El proyecto debe
> seguir compilando al finalizar cada una. La numeración (T-001...)
> es local a esta feature. F-003 modifica `Application/Abstractions/`,
> `Infrastructure/Persistence/` y `Tests.Integration/Persistence/`.
> NO modifica `Domain`, `UI`, `Tests.Unit`, `Directory.Packages.props`
> ni el CI workflow.

---

## Pronóstico de carga de review

| Field | Value |
|-------|-------|
| Estimated changed lines | ~560 |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1: abstracciones + repo + DI (~260 líneas); PR 2: tests de integración (~320 líneas) |
| Delivery strategy | auto-forecast |
| Chain strategy | pending |

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: pending
400-line budget risk: High

### Unidades de trabajo sugeridas

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Extender `IExpenseRepository`, crear `ExpenseFilter`, implementar `ExpenseRepository` y registrarlo en `AddPersistence` | PR 1 | Base = `main`; incluye build limpio y convenciones |
| 2 | Crear `ExpenseRepositoryTests` con al menos 10 tests de integración | PR 2 | Base = PR 1; reutiliza `TestAppDbContextFactory` y valida SQLite |

---

## Resumen

F-003 entrega el **repositorio concreto de `Expense`** que cierra el slice
vertical de persistencia iniciado por F-001 (entidad) y F-002 (schema + migración).
Extiende `IExpenseRepository` con `GetByFilterAsync`, introduce el record
`ExpenseFilter`, implementa los 6 métodos en `ExpenseRepository` (CRUD,
validación de FK a `Categories`, paginación por `Date desc, CreatedAt desc`),
registra el repo en `AddPersistence` y lo cubre con tests de integración
SQLite. La feature no toca `Domain`, no genera migraciones y sigue la
convención de `CategoryRepository`: `sealed`, `_db` inyectado, `Result.Failure`
con códigos en inglés y mensajes en español, `AsNoTracking` en queries y
`Result.Success(null!)` en comandos.

---

## Resumen de tareas

| ID | Tarea | Descripción corta | Estimación diff | Depende de | Verificación |
| --- | --- | --- | --- | --- | --- |
| T-001 | Extender `IExpenseRepository` con `GetByFilterAsync` | Añadir firma + XML doc; mantener 5 métodos originales intactos | +15 / -0 | ninguna | `dotnet build` exit 0 | [x]
| T-002 | Crear `ExpenseFilter.cs` | Record `sealed` tonto con `Page`, `PageSize`, `CategoryId?`, `DateFrom?`, `DateTo?` | +35 / -0 | ninguna | `dotnet build` exit 0 | [x]
| T-003 | Implementar `ExpenseRepository` | Los 6 métodos: CRUD, FK validation, orden `Date desc`, `AsNoTracking`, hard delete | +200 / -0 | T-001, T-002 | `dotnet build` exit 0 | [x]
| T-004 | Registrar `IExpenseRepository` en `AddPersistence` | `AddScoped<IExpenseRepository, ExpenseRepository>()` tras los repos de catálogo | +10 / -5 | T-003 | `dotnet build` exit 0 | [x]
| T-005 | Crear `ExpenseRepositoryTests.cs` | 10 tests (T-1..T-10) de CRUD, FK, filtros y paginación con SQLite in-memory | +320 / -0 | T-003 | `dotnet test --filter "FullyQualifiedName~ExpenseRepositoryTests"` ≥10 passed | [x]
| T-006 | Verificación final, formato y cierre | Build, tests (~167+10 ≈ 177), NetArchTest, format, `features.md`, rename a `.done.md` | +2 / -2 | T-001 a T-005 | `dotnet test` 177 passed; `dotnet format style --verify-no-changes` exit 0 | [x]

**Total estimado:** ~1 jornada. T-003 (repo) y T-005 (tests) son las más densas;
T-001, T-002 y T-004 son mecánicas; T-006 es verificación y cierre administrativo.

---

## T-001 — Extender `IExpenseRepository` con `GetByFilterAsync`

**Objetivo**
Añadir el sexto método a la interface `IExpenseRepository` sin modificar los
5 métodos originales definidos en F-006 de Fase 1, con XML doc que documente
la paginación y el orden por fecha descendente.

**Pasos**

1. Abrir `src/ControlDeFinanzasIA.Application/Abstractions/IExpenseRepository.cs`.
2. Añadir `Task<PagedResult<Expense>> GetByFilterAsync(ExpenseFilter filter, CancellationToken ct = default);` tras `GetAllAsync`.
3. Añadir XML doc describiendo que devuelve una página de gastos que cumplen
   `filter`, ordenados por `Date` descendente.
4. Confirmar que `using ControlDeFinanzasIA.Application.Common;` ya cubre
   `PagedResult<T>` y que `ExpenseFilter` vive en el mismo namespace, por lo
   que no se necesita `using` adicional.
5. Ejecutar `dotnet build`.

**Verificación**

- `dotnet build "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --nologo`:
  exit 0, 0 warnings, 0 errors.
- Inspección: los 5 métodos originales conservan sus firmas exactas; el nuevo
  método tiene XML doc.

**Dependencias**
- ninguna.

**Estimación**
- S (menos de 1 hora). Cambio mecánico de firma + documentación.

**Trazabilidad**
- AC-1 (`IExpenseRepository` extendida).
- RF-1 (firma y ubicación del nuevo método).
- §9.1 API surface.

---

## T-002 — Crear `ExpenseFilter.cs`

**Objetivo**
Crear el record "tonto" `ExpenseFilter` en `Application/Abstractions/` como
contrato de entrada de `GetByFilterAsync`, sin auto-validación y sin filtro
por `Currency`.

**Pasos**

1. Crear `src/ControlDeFinanzasIA.Application/Abstractions/ExpenseFilter.cs`.
2. Usar file-scoped namespace `ControlDeFinanzasIA.Application.Abstractions`.
3. Declarar `public sealed record ExpenseFilter` con propiedades init-only y defaults:
   - `int Page = 1`
   - `int PageSize = 50`
   - `Guid? CategoryId = null`
   - `DateOnly? DateFrom = null`
   - `DateOnly? DateTo = null`
4. Añadir XML doc en el record explicando que es un contrato de transporte
   sin validación (la validación vive en F-005).
5. Ejecutar `dotnet build`.

**Verificación**

- `dotnet build "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --nologo`:
  exit 0, 0 warnings, 0 errors.
- Inspección: `sealed`, file-scoped namespace, `using` al tope, no hay
  propiedad `Currency`, no hay métodos de validación.

**Dependencias**
- ninguna (puede ejecutarse en paralelo con T-001, pero ambas deben terminar
  antes de T-003).

**Estimación**
- S (menos de 1 hora). Record trivial.

**Trazabilidad**
- AC-2 (`ExpenseFilter` creado).
- RF-2 (propiedades y defaults).
- DP-2 (record tonto sin auto-validación), DP-6 (sin `Currency`).
- §9.2 API surface.

---

## T-003 — Implementar `ExpenseRepository`

**Objetivo**
Crear el repositorio concreto `ExpenseRepository` que implemente los 6 métodos
con el patrón de F-004 de Fase 2: `sealed`, `_db` inyectado, `FindAsync`,
`CurrentValues.SetValues`, `AsNoTracking`, códigos de error en inglés y
mensajes en español.

**Pasos**

1. Crear `src/ControlDeFinanzasIA.Infrastructure/Persistence/Repositories/ExpenseRepository.cs`.
2. Usar file-scoped namespace `ControlDeFinanzasIA.Infrastructure.Persistence.Repositories`.
3. Añadir `using` necesarios:
   - `using ControlDeFinanzasIA.Application.Abstractions;`
   - `using ControlDeFinanzasIA.Application.Common;`
   - `using ControlDeFinanzasIA.Domain.Transactions;`
   - `using Microsoft.EntityFrameworkCore;`
4. Declarar `public sealed class ExpenseRepository : IExpenseRepository`.
5. Campo privado `private readonly AppDbContext _db;` y constructor público
   `public ExpenseRepository(AppDbContext db) => _db = db;`.
6. Implementar `AddAsync` (RF-4):
   - `ArgumentNullException.ThrowIfNull(expense);`
   - `Category? category = await _db.Categories.FindAsync(new object?[] { expense.CategoryId }, ct);`
   - Si `category is null`, devolver `Result.Failure("CategoryNotFound", $"La categoría {expense.CategoryId} no existe.");`
   - `_db.Expenses.Add(expense); await _db.SaveChangesAsync(ct);`
   - Devolver `Result.Success(null!);`
7. Implementar `UpdateAsync` (RF-5):
   - `ArgumentNullException.ThrowIfNull(expense);`
   - `Expense? existing = await _db.Expenses.FindAsync(new object?[] { expense.Id }, ct);`
   - Si `existing is null`, `Result.Failure("NotFound", $"Gasto {expense.Id} no encontrado.");`
   - `_db.Entry(existing).CurrentValues.SetValues(expense); await _db.SaveChangesAsync(ct);`
   - Devolver `Result.Success(null!);`
   - **No** re-validar FK; no capturar `DbUpdateException`.
8. Implementar `RemoveAsync` (RF-6):
   - Si `id == Guid.Empty`, `Result.Failure("NotFound", $"Gasto {id} no encontrado.");`
   - `Expense? existing = await _db.Expenses.FindAsync(new object?[] { id }, ct);`
   - Si `existing is null`, `Result.Failure("NotFound", $"Gasto {id} no encontrado.");`
   - `_db.Expenses.Remove(existing); await _db.SaveChangesAsync(ct);`
   - Devolver `Result.Success(null!);`
9. Implementar `GetByIdAsync` (RF-7):
   - Si `id == Guid.Empty`, devolver `null`.
   - `return await _db.Expenses.FindAsync(new object?[] { id }, ct);`
10. Implementar `GetAllAsync` (RF-8):
    - `return await _db.Expenses.AsNoTracking().OrderByDescending(e => e.Date).ToListAsync(ct);`
11. Implementar `GetByFilterAsync` (RF-9):
    - `ArgumentNullException.ThrowIfNull(filter);`
    - `IQueryable<Expense> query = _db.Expenses.AsNoTracking();`
    - Aplicar filtros opcionales:
      - `if (filter.CategoryId is not null) query = query.Where(e => e.CategoryId == filter.CategoryId);`
      - `if (filter.DateFrom is not null) query = query.Where(e => e.Date >= filter.DateFrom);`
      - `if (filter.DateTo is not null) query = query.Where(e => e.Date <= filter.DateTo);`
    - Orden: `query = query.OrderByDescending(e => e.Date).ThenByDescending(e => e.CreatedAt);`
    - `int total = await query.CountAsync(ct);`
    - `int skip = (filter.Page - 1) * filter.PageSize;`
    - `List<Expense> items = await query.Skip(skip).Take(filter.PageSize).ToListAsync(ct);`
    - `return new PagedResult<Expense>(items, total, filter.Page, filter.PageSize);`
12. Añadir XML doc en clase y cada método público.
13. Ejecutar `dotnet build`.

**Verificación**

- `dotnet build "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --nologo`:
  exit 0, 0 warnings, 0 errors.
- Inspección: `sealed`, file-scoped namespace, `using` al tope, XML doc,
  6 métodos implementados, `RemoveAsync` usa `_db.Expenses.Remove`,
  `GetByFilterAsync` usa `OrderByDescending(...).ThenByDescending(...)`.

**Dependencias**
- T-001 (`IExpenseRepository` extendida).
- T-002 (`ExpenseFilter` existe).
- F-001 de Fase 3 (`Expense` y sus propiedades).
- F-002 de Fase 3 (tabla `Expenses`, FK `Restrict`, `OwnsOne` de `Money`).
- F-004 de Fase 2 (patrón de `CategoryRepository`).

**Estimación**
- M (2–3 horas). Es el cuerpo principal de la feature; requiere precisión
  en FK validation, orden y paginación.

**Trazabilidad**
- AC-3 (`ExpenseRepository` creado con los 6 métodos).
- RF-3 a RF-9.
- RN-1 a RN-8.
- DP-3 (repo no valida filtro), DP-5 (tie-break `CreatedAt`), DP-7 (hard delete).

---

## T-004 — Registrar `IExpenseRepository` en `AddPersistence`

**Objetivo**
Añadir el registro scoped de `IExpenseRepository → ExpenseRepository` en el
método `AddPersistence` y actualizar el XML doc del método para listar el
nuevo repo.

**Pasos**

1. Abrir `src/ControlDeFinanzasIA.Infrastructure/Persistence/ServiceCollectionExtensions.cs`.
2. Tras las líneas de `ICategoryRepository` e `IPaymentMethodRepository`, añadir:
   `services.AddScoped<IExpenseRepository, ExpenseRepository>();`
3. Actualizar el XML doc de `AddPersistence` para incluir `<see cref="IExpenseRepository"/>`
   en la lista de repos registrados.
4. Confirmar que `using ControlDeFinanzasIA.Infrastructure.Persistence.Repositories;`
   ya está presente (registra `CategoryRepository` / `PaymentMethodRepository`).
5. Ejecutar `dotnet build`.

**Verificación**

- `dotnet build "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --nologo`:
  exit 0, 0 warnings, 0 errors.
- Inspección: `AddScoped<IExpenseRepository, ExpenseRepository>()` aparece tras
  los repos de catálogo; el XML doc menciona `IExpenseRepository`.

**Dependencias**
- T-003 (`ExpenseRepository` compila).

**Estimación**
- S (menos de 1 hora). Cambio mecánico de DI.

**Trazabilidad**
- AC-4 (`AddPersistence` registra el repo).
- RF-10.

---

## T-005 — Crear `ExpenseRepositoryTests.cs`

**Objetivo**
Crear los tests de integración del repositorio con SQLite in-memory,
reutilizando `TestAppDbContextFactory` y siguiendo la convención de
`CategoryRepositoryTests`: pure xUnit `Assert.*`, naming
`Method_Scenario_ExpectedResult`, al menos 10 tests (T-1..T-10 de §11).

**Pasos**

1. Crear `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/ExpenseRepositoryTests.cs`.
2. Usar file-scoped namespace `ControlDeFinanzasIA.Tests.Integration.Persistence`.
3. Añadir `using` al tope:
   - `using ControlDeFinanzasIA.Domain.Transactions;`
   - `using ControlDeFinanzasIA.Domain.ValueObjects;`
   - `using ControlDeFinanzasIA.Infrastructure.Persistence;`
   - `using ControlDeFinanzasIA.Infrastructure.Persistence.Repositories;`
   - `using Xunit;`
4. Implementar la clase `public sealed class ExpenseRepositoryTests`.
5. Usar un timestamp fijo estático para `CreatedAt`/`UpdatedAt` (patrón de
   `CategoryRepositoryTests`).
6. Implementar los 10 tests de §11:
   - **T-1** `AddAsync_WithValidExpense_PersistsAndReturnsSuccess`
   - **T-2** `AddAsync_WithUnknownCategoryId_ReturnsCategoryNotFound`
   - **T-3** `GetByIdAsync_WithExistingId_ReturnsExpense`
   - **T-4** `GetByIdAsync_WithMissingId_ReturnsNull`
   - **T-5** `GetAllAsync_ReturnsAllOrderedByDateDesc`
   - **T-6** `GetByFilterAsync_WithNoFilter_ReturnsFirstPageOrderedByDateDesc`
   - **T-7** `GetByFilterAsync_WithDateRange_FiltersCorrectly`
   - **T-8** `GetByFilterAsync_PaginatesWithCorrectTotalAndItems`
   - **T-9** `UpdateAsync_WithExistingExpense_UpdatesFields`
   - **T-10** `RemoveAsync_WithExistingExpense_DeletesAndReturnsSuccess`
7. En cada test, usar el patrón:
   - `using var factory = new TestAppDbContextFactory();`
   - `using AppDbContext context = factory.CreateContext();`
   - `await context.Database.MigrateAsync();`
   - `var repo = new ExpenseRepository(context);`
8. Para `CategoryId` válido, usar una categoría sembrada (por ejemplo,
   `11111111-1111-1111-1111-000000000001` = "Comida").
9. Ejecutar `dotnet test --filter "FullyQualifiedName~ExpenseRepositoryTests"`.
10. (Opcional) Si sobra tiempo, añadir los tests recomendados de §11:
    filtro por categoría, página fuera de rango, `UpdateAsync`/`RemoveAsync`
    `NotFound`, y round-trip de `Money` con precisión.

**Verificación**

- `dotnet build "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --nologo`:
  exit 0, 0 warnings, 0 errors.
- `dotnet test "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --nologo --no-build --filter "FullyQualifiedName~ExpenseRepositoryTests"`:
  ≥10 passed, 0 failed.
- Inspección: file-scoped namespace, `using` al tope, `sealed`, aserciones
  con `Assert.*` (sin `Should()`), `MigrateAsync()` presente.

**Dependencias**
- T-003 (`ExpenseRepository` implementado).
- F-006 de Fase 2 (`TestAppDbContextFactory` reutilizable).
- F-003 de Fase 2 (seed de 9 categorías con GUIDs conocidos).

**Estimación**
- M (2–3 horas). Escribir 10 tests deterministas contra SQLite requiere
  cuidado con `CreatedAt`, `Money` y orden.

**Trazabilidad**
- AC-5 (tests de integración).
- §11 T-1..T-10.
- RNF-6 (determinismo en tests).

---

## T-006 — Verificación final, formato y cierre

**Objetivo**
Ejecutar la batería de verificación final de la spec: build limpio,
todos los tests existentes + nuevos verdes, NetArchTest R-1 a R-5,
`dotnet format`, pureza de `Domain`/`Application`, actualizar
`spec/phases/phase-03/features.md` y renombrar la spec a `.done.md`.

**Pasos**

1. Ejecutar `dotnet build -c Release` y `dotnet build -c Debug`:
   0 warnings, 0 errors.
2. Ejecutar `dotnet test` completo: confirmar ~167 existentes + ~10 nuevos
   ≈ 177 passed, 0 failed.
3. Ejecutar `dotnet test --filter "FullyQualifiedName~ArchitectureTests"`
   (o el filtro que cubra los 11 tests NetArchTest R-1 a R-5): todos passed.
4. Ejecutar `dotnet list src/ControlDeFinanzasIA.Domain package`: debe estar vacío.
5. Ejecutar `dotnet list src/ControlDeFinanzasIA.Application package`: debe seguir
   mostrando solo `FluentValidation` (sin EF Core).
6. Ejecutar `dotnet list src/ControlDeFinanzasIA.UI reference`: debe seguir sin
   referencia a `Infrastructure`.
7. Ejecutar `dotnet format style --verify-no-changes`: exit 0.
8. Verificar convenciones en `ExpenseRepository.cs`, `ExpenseFilter.cs` y
   `ExpenseRepositoryTests.cs`: file-scoped namespace, `using` al tope,
   XML doc, `sealed`, códigos en inglés, mensajes en español.
9. Confirmar scope (AC-7): no se implementó `RegisterExpenseUseCase` /
   `GetTransactionsQuery` / DTOs / UI / validación de `ExpenseFilter` /
   soft-delete / `IQueryRepository<T>` / migraciones / seed de `Expense` /
   FluentAssertions.
10. Actualizar `spec/phases/phase-03/features.md`:
    - Tabla resumen: F-003 de "🔲 PENDIENTE" a "✅ COMPLETADA".
    - Header de sección F-003: añadir "— ✅ COMPLETADA".
11. Renombrar `spec/features/phase-03/F-003-expense-repository.spec.md`
    a `spec/features/phase-03/F-003-expense-repository.done.md`.
12. Ejecutar `dotnet build -c Release` final.

**Verificación**

- `dotnet build -c Release`: exit 0, 0 warnings, 0 errors.
- `dotnet test -c Release --no-build`: ~177 passed, 0 failed.
- `dotnet test --filter "FullyQualifiedName~ArchitectureTests"`: todos passed.
- `dotnet format style --verify-no-changes`: exit 0.
- `dotnet list src/ControlDeFinanzasIA.Domain package`: vacío.
- `spec/phases/phase-03/features.md` actualizado con F-003 completada.
- Spec renombrada a `.done.md`.

**Dependencias**
- T-001 a T-005.

**Estimación**
- M (1–2 horas). Verificación exhaustiva + cierre administrativo.

**Trazabilidad**
- AC-4, AC-5, AC-6, AC-7, AC-8, AC-9, AC-10.
- RNF-1 (NetArchTest R-1 a R-5), RNF-2 (sin paquetes nuevos).

---

## Orden de ejecución recomendado

**Orden secuencial con paralelismo inicial:**

1. **T-001** y **T-002** pueden ejecutarse en cualquier orden (o en paralelo);
   no se bloquean mutuamente. Ambas deben terminar antes de T-003.
2. **T-003** depende de T-001 y T-002. Es la entrega funcional principal.
3. **T-004** depende de T-003 (necesita que `ExpenseRepository` compile).
4. **T-005** depende de T-003 (los tests invocan el repo concreto).
5. **T-006** es verificación y cierre de toda la feature.

```text
T-001 ──┐
        ├→ T-003 → T-004 ─┐
T-002 ──┘                 ├→ T-006
                          │
                     T-005 ┘
```

**No hay paralelización útil** entre T-003/T-004/T-005 porque todas dependen
 del repo; T-001 y T-002 sí pueden correr juntas.

---

## Tareas bloqueantes / habilitadoras

| Tarea | Habilita | Razón |
| --- | --- | --- |
| T-001 | T-003 | `ExpenseRepository` implementa `IExpenseRepository`, que debe incluir `GetByFilterAsync`. |
| T-002 | T-003 | `ExpenseRepository.GetByFilterAsync` recibe `ExpenseFilter` como parámetro. |
| T-003 | T-004 | `AddPersistence` referencia la clase `ExpenseRepository`; debe compilar. |
| T-003 | T-005 | Los tests instancian `ExpenseRepository` y ejecutan sus 6 métodos. |
| T-004 | T-006 | El registro DI se verifica indirectamente vía NetArchTest R-3 y build. |
| T-005 | T-006 | Todos los tests deben pasar antes del cierre. |

---

## Estimación total

| Tarea | Dificultad | Horas estimadas |
| --- | --- | --- |
| T-001 | S | 0.5 |
| T-002 | S | 0.5 |
| T-003 | M | 2–3 |
| T-004 | S | 0.5 |
| T-005 | M | 2–3 |
| T-006 | M | 1–2 |
| **Total** | | **~7–10 horas (~1 jornada)** |

---

## Riesgos y mitigaciones

| Riesgo | Probabilidad | Impacto | Mitigación |
| --- | --- | --- | --- |
| `GetByFilterAsync` no traduce `ThenByDescending(e => e.CreatedAt)` a SQL en SQLite. | Baja | Medio | El orden opera sobre columnas primitivas (`CreatedAt` `TEXT`), no sobre `Money`. Los tests T-6/T-8 lo validan en SQLite in-memory. Ver RF-9. |
| `Skip` con valor negativo (`Page = 0`) lanza en EF Core. | Baja | Bajo | DP-3: el repo no valida; F-005 valida `Page ≥ 1`. No añadir validación defensiva en el repo. |
| Paginación no determinista sin tie-break. | Media | Medio | DP-5: `OrderByDescending(e => e.Date).ThenByDescending(e => e.CreatedAt)`. Test T-8 valida. |
| `AddAsync` no detecta `CategoryNotFound` por race condition. | Muy baja | Bajo | RN-1: la FK `Restrict` de F-002 es la red de seguridad. No se captura `DbUpdateException`. |
| `UpdateAsync` con `CategoryId` inexistente lanza `DbUpdateException`. | Baja | Medio | RF-5 + EC-3: el repo no captura. El use case Phase 4 validará la categoría. |
| Round-trip de `Money` con `HasPrecision(18,4)` pierde decimales. | Baja | Alto | F-002 ya fijó `OwnsOne`. Test T-1 valida round-trip con monto DOP. |
| El developer añade FluentAssertions "porque la doc lo dice". | Media | Bajo | §10.4 y AC-5/AC-8 exigen `Assert.*`. Revisar en PR. |
| El developer valida `ExpenseFilter` en el repo (rompe DP-3). | Media | Bajo | AC-7 prohíbe explícitamente la validación en el repo; la validación es de F-005. |
| `IExpenseRepository` pierde uniformidad con los otros 14 repos. | Aceptado | Bajo | Decisión del phase plan: `GetByFilterAsync` es específico de `Expense` por paginación de transacciones. No se introduce `IQueryRepository<T>`. |
| `dotnet format style` propone cambios inesperados. | Baja | Bajo | Aplicar los cambios. No relajar reglas globales. |
| R-2 falla porque `Application` adquiere dependencia nueva. | Muy baja | Alto | `ExpenseFilter` solo usa BCL; `IExpenseRepository` ya referencia `PagedResult` y `Expense`. No hay `PackageReference` nueva. |

---

## Notas para apply

- **No tocar `Domain/Transactions/Expense.cs`.** `CreatedAt`/`UpdatedAt` ya
  existen (F-001 INV-7, F-002 las mapea). No hay migración en F-003.
- **No añadir FluentAssertions.** Los tests usan pure xUnit `Assert.*`;
  `FluentAssertions` no está en `Directory.Packages.props`.
- **No validar `ExpenseFilter` en el repo.** Esa validación es de F-005
  (`GetTransactionsQuery`). El repo confía en el caller (DP-3).
- **Códigos de error en inglés, mensajes en español.** `"NotFound"` y
  `"CategoryNotFound"`; mensajes como `"La categoría {id} no existe."`.
- **Hard delete en `RemoveAsync`.** No soft-delete, no `IsDeleted`, no query
  filter.
- **`ExpenseFilter` va en `Application/Abstractions/` (flat).** No crear
  subcarpeta `Queries/` ni mover a `Common/`.
- **Reutilizar `TestAppDbContextFactory` existente.** No inventar una factoría
  nueva.
- **El registro DI puede compilarse antes de que el repo esté terminado,**
  pero T-004 se deja depender de T-003 para mantener el orden.

---

## Definition of Done (DoD)

- [x] AC-1: `IExpenseRepository` expone `GetByFilterAsync`; los 5 métodos
      originales se mantienen. (T-001)
- [x] AC-2: `ExpenseFilter.cs` existe como record `sealed` con los campos
      esperados y sin validación. (T-002)
- [x] AC-3: `ExpenseRepository.cs` implementa los 6 métodos con FK validation,
      orden, paginación y hard delete. (T-003)
- [x] AC-4: `AddPersistence` registra `IExpenseRepository → ExpenseRepository`
      como `Scoped`. (T-004)
- [x] AC-5: `ExpenseRepositoryTests.cs` tiene ≥10 tests verdes con SQLite
      in-memory. (T-005)
- [x] AC-6: `dotnet build` limpio, `dotnet test` ≈177 verdes, NetArchTest
      R-1 a R-5 verdes, `dotnet format style --verify-no-changes` exit 0.
      (T-006)
- [x] AC-7: Sin scope creep (no use cases, no UI, no validación de filtro en
      repo, no soft-delete, no migraciones, no FluentAssertions). (T-006)
- [x] AC-8: Convenciones (`sealed`, file-scoped namespace, `using` al tope,
      XML doc, `Assert.*`). (T-001 a T-006)
- [x] AC-9 / AC-10: `features.md` actualizado y spec renombrada a
      `.done.md`. (T-006)

---

## Referencias

- `spec/features/phase-03/F-003-expense-repository.spec.md` — la spec completa
  de F-003.
- `spec/phases/phase-03/features.md` — F-003 dentro del desglose de la fase 3.
- `spec/features/phase-03/F-001-expense-domain-entity.done.md` — entidad
  `Expense`.
- `spec/features/phase-03/F-002-expense-configuration-migration.done.md` —
  tabla `Expenses`.
- `spec/features/phase-01/F-006-contrato-interfaces-repo.done.md` —
  `IExpenseRepository` original.
- `spec/features/phase-01/F-005-comuntypes-result-pagedresult.done.md` —
  `Result` y `PagedResult<T>`.
- `spec/features/phase-02/F-004-category-paymentmethod-repositories.done.md` —
  patrón de repositorio.
- `spec/features/phase-02/F-006-integration-tests.done.md` —
  `TestAppDbContextFactory`.
- `tasks/phase-03/F-001-expense-domain-entity.tasks.md` — formato de
  referencia.
- `tasks/phase-03/F-002-expense-configuration-migration.tasks.md` — formato de
  referencia.
- Engram `sdd/controldefinanzasia/testing-capabilities` — pure xUnit
  `Assert.*`.
- Engram `sdd/f-003p3-expense-repository/proposal` y
  `sdd/f-003p3-expense-repository/spec`.