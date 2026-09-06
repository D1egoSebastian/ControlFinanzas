# Specification — F-006: Contratos de interfaces de repositorio

> **Fase:** 1 — Esqueleto de arquitectura y contratos base
> **Feature:** F-006 (Phase 1, local a esta fase)
> **Estado:** Pendiente de implementación
> **Spec ID:** SPEC-F006
>
> **Nota de numeración:** este `F-006` es el de la **Fase 1**
> (`spec/phases/phase-01/features.md`). Los IDs de feature son
> locales a cada fase; el folder los desambigua. Ver `spec/README.md`
> para la convención completa.
>
> Fuentes de verdad: `docs/vision.md`, `docs/architecture.md`
> (que define las entidades y las interfaces), `docs/roadmap.md`,
> `spec/constitution/*`, `spec/phases/phase-01/features.md`
> (F-006 dentro del desglose de la fase 1).

---

## 1. Resumen

F-006 define los **contratos de persistencia** de la fase 1 como
interfaces vacías (sin implementación) en la capa `Application`.
El objetivo es bloquear la tentación de meter EF Core o tipos de
`Infrastructure` en `Application`, y asegurar que las features
futuras solo añadan métodos a las interfaces existentes, no
nuevas interfaces.

| Miembro | Tipo | Carpeta |
| --- | --- | --- |
| 14 interfaces de repositorio | `interface` | `src/ControlDeFinanzasIA.Application/Abstractions/` |
| `IUnitOfWork` | `interface` | `src/ControlDeFinanzasIA.Application/Abstractions/` |

> **Nota de diseño (revisión 2026-07):** la spec original
> proponía que los métodos de repositorio devolvieran
> `Task<Result<T>>` (genérico). Tras la decisión de F-005 de
> cambiar `Result<T>` a `Result` non-generic (con `Value: object?`),
> F-006 usa `Task<Result>` para commands. Los queries mantienen
> `Task<T>` directo (sin envolver en `Result`) porque el caller
> (un use case o query) verifica el resultado por su cuenta.

---

## 2. Contexto

`Application` no debe depender de `Infrastructure` (Clean
Architecture, ADR-001). Si los repositorios estuvieran en
`Infrastructure`, los use cases y queries no podrían usarlos sin
meter EF Core en `Application`. Si los repositorios se definieran en
`Application` con clases concretas, no podrían tener tests unitarios
sin EF Core o SQLite in-memory.

**La solución:** definir interfaces vacías en `Application` que
describen **qué** necesita la capa de aplicación, no **cómo** se
implementa. `Infrastructure` provee las implementaciones concretas.
Esto cumple Clean Architecture y habilita la regla de
NetArchTest en F-007 que verifica que `Application` no depende
de `Infrastructure`.

---

## 3. Objetivo

Establecer los 14 contratos de repositorio + `IUnitOfWork` en
`Application/Abstractions/` como interfaces vacías (sin
implementación), con firmas que cubren los casos de uso básicos
(Add, Update, Remove, GetById, queries básicas) de los agregados
de la fase 1. Permitir que las features futuras (fases 3-11)
implementen estos contratos en `Infrastructure` y añadan métodos
nuevos sin redefinir las interfaces.

---

## 4. Alcance

### 4.1 En alcance

- 14 interfaces de repositorio en
  `src/ControlDeFinanzasIA.Application/Abstractions/`:
  - `IExpenseRepository`
  - `IIncomeRepository`
  - `IDebtRepository`
  - `IDebtPaymentRepository`
  - `IRecurringRuleRepository` (para `Subscription` y `FixedExpense`)
  - `IGoalRepository`
  - `IGoalContributionRepository`
  - `IBudgetRepository`
  - `ICategoryRepository`
  - `ITagRepository`
  - `IPaymentMethodRepository`
  - `ISavedViewRepository`
  - `IAnomalyAlertRepository`
  - `ISettingRepository`
- `IUnitOfWork` con `Task<int> SaveChangesAsync(CancellationToken)`.
- Cada interfaz de repositorio tiene un conjunto de métodos
  básicos (Add, Update, Remove, GetById, queries básicas). Las
  queries complejas se añadirán en futuras features (F-005+).
- Cada método devuelve `Task<T>` o `Task<IReadOnlyList<T>>`
  (queries) o `Task<Result>` (commands).
- Todos los métodos asíncronos aceptan un
  `CancellationToken ct = default` opcional.
- Sin implementación. Sin tests. Las implementaciones y los tests
  entran en F-002 de Fase 2 (persistencia con EF Core + SQLite).

### 4.2 Fuera de alcance

- **Implementaciones concretas de los repositorios** (en
  `Infrastructure/Persistence/Repositories/`). Es F-002 de Fase 2.
  Aquí NO.
- **Mapeo de entidades a tablas** (`IEntityTypeConfiguration<T>`).
  Es F-002 de Fase 2. Aquí NO.
- **`AppDbContext`** (el `DbContext` de EF Core). Es F-002 de Fase 2.
  Aquí NO.
- **Migraciones de EF Core**. Es F-002 de Fase 2. Aquí NO.
- **Value converters** (Money, DateRange a columnas). Es F-002 de
  Fase 2. Aquí NO.
- **Queries complejas** (e.g., `GetByDateRangeAsync`,
  `GetByMonthAsync`, `GetActiveSubscriptionsAsync`). F-006 define
  solo las queries básicas (`GetById`, `GetAll`). Las queries
  complejas se añaden en features futuras cuando los use cases
  las necesiten.
- **Patrón genérico** `IRepository<T, TId>` con métodos genéricos
  en una interface base. La spec define interfaces específicas por
  agregado (no genéricas) porque cada agregado tiene queries
  distintas. El "No se usa el patrón genérico" es una decisión
  explícita de la arquitectura.
- **Use cases y queries** que consumen estos repos. F-006 solo
  define los contratos; los use cases se implementan en features
  posteriores.

---

## 5. Casos de uso

### CU-1 — `IUnitOfWork` coordina una transacción
- **Actor:** use case de la capa Application (e.g.,
  `RegisterExpenseUseCase`).
- **Trigger:** el use case necesita persistir varios agregados
  en una sola transacción atómica.
- **Precondición:** el use case tiene referencias a uno o más
  repositorios inyectados.
- **Postcondición:** el commit es atómico (o se hace todo o nada).

### CU-2 — Repository: `Add` (create)
- **Actor:** use case de la capa Application.
- **Trigger:** el use case crea un nuevo agregado y lo persiste.
  `await expenseRepository.AddAsync(newExpense, ct);`
- **Precondición:** la entidad cumple las invariantes del dominio.
- **Postcondición:** la entidad se persiste en el store.

### CU-3 — Repository: `Update`
- **Actor:** use case de la capa Application.
- **Trigger:** el use case modifica un agregado existente.
  `await expenseRepository.UpdateAsync(updatedExpense, ct);`
- **Precondición:** la entidad existe (verificable por `GetById`).
- **Postcondición:** la entidad se actualiza en el store.

### CU-4 — Repository: `Remove` (delete)
- **Actor:** use case de la capa Application.
- **Trigger:** el use case elimina un agregado existente.
  `await expenseRepository.RemoveAsync(expenseId, ct);`
- **Precondición:** la entidad existe.
- **Postcondición:** la entidad se elimina del store.

### CU-5 — Repository: `GetById`
- **Actor:** use case o query de la capa Application.
- **Trigger:** el use case necesita recuperar un agregado por su
  ID. `var expense = await expenseRepository
  .GetByIdAsync(expenseId, ct);`
- **Precondición:** el ID es válido.
- **Postcondición:** la entidad se devuelve, o `null` si no existe.

### CU-6 — Repository: `GetAll`
- **Actor:** query de la capa Application.
- **Trigger:** la query necesita todos los agregados (para listas
  iniciales, dropdowns, etc.). `var categories = await
  categoryRepository.GetAllAsync(ct);`
- **Precondición:** ninguna.
- **Postcondición:** la colección completa de agregados se devuelve.

---

## 6. Requisitos funcionales

### RF-1 — Patrón común de interface de repositorio

Cada una de las 14 interfaces de repositorio sigue este patrón:

```csharp
public interface I<NOMBRE>Repository
{
    // Commands (devuelven Task<Result> para errores esperados)
    Task<Result> AddAsync(<ENTITY> entity, CancellationToken ct = default);
    Task<Result> UpdateAsync(<ENTITY> entity, CancellationToken ct = default);
    Task<Result> RemoveAsync(Guid id, CancellationToken ct = default);

    // Queries (devuelven Task<T> directo)
    Task<<ENTITY>?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<<ENTITY>>> GetAllAsync(CancellationToken ct = default);
}
```

Notas:
- El nombre del parámetro de tipo es el nombre concreto de la
  entidad (`Expense`, `Income`, `Debt`, etc.).
- El ID es `Guid` para todas las entidades (consistente con el
  modelo de dominio de `architecture.md`).
- El `Add` y `Update` reciben la entidad completa (no campos
  sueltos). Esto valida invariantes en el dominio.
- `GetById` retorna `Task<T?>` (no `T`): si no existe, retorna
  `null`. El caller verifica.
- `GetAll` retorna `Task<IReadOnlyList<T>>` (no `Task<T[]>`): la
  lista es de solo lectura, lo que el caller no puede mutar.

### RF-2 — `IExpenseRepository`

```csharp
public interface IExpenseRepository
{
    Task<Result> AddAsync(Expense expense, CancellationToken ct = default);
    Task<Result> UpdateAsync(Expense expense, CancellationToken ct = default);
    Task<Result> RemoveAsync(Guid id, CancellationToken ct = default);
    Task<Expense?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Expense>> GetAllAsync(CancellationToken ct = default);
}
```

### RF-3 — `IIncomeRepository`

```csharp
public interface IIncomeRepository
{
    Task<Result> AddAsync(Income income, CancellationToken ct = default);
    Task<Result> UpdateAsync(Income income, CancellationToken ct = default);
    Task<Result> RemoveAsync(Guid id, CancellationToken ct = default);
    Task<Income?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Income>> GetAllAsync(CancellationToken ct = default);
}
```

### RF-4 — `IDebtRepository`

```csharp
public interface IDebtRepository
{
    Task<Result> AddAsync(Debt debt, CancellationToken ct = default);
    Task<Result> UpdateAsync(Debt debt, CancellationToken ct = default);
    Task<Result> RemoveAsync(Guid id, CancellationToken ct = default);
    Task<Debt?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Debt>> GetAllAsync(CancellationToken ct = default);
}
```

### RF-5 — `IDebtPaymentRepository`

```csharp
public interface IDebtPaymentRepository
{
    Task<Result> AddAsync(DebtPayment payment, CancellationToken ct = default);
    Task<Result> UpdateAsync(DebtPayment payment, CancellationToken ct = default);
    Task<Result> RemoveAsync(Guid id, CancellationToken ct = default);
    Task<DebtPayment?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<DebtPayment>> GetAllAsync(CancellationToken ct = default);
}
```

### RF-6 — `IRecurringRuleRepository`

```csharp
public interface IRecurringRuleRepository
{
    Task<Result> AddAsync(RecurringRule rule, CancellationToken ct = default);
    Task<Result> UpdateAsync(RecurringRule rule, CancellationToken ct = default);
    Task<Result> RemoveAsync(Guid id, CancellationToken ct = default);
    Task<RecurringRule?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<RecurringRule>> GetAllAsync(CancellationToken ct = default);
}
```

Nota: `RecurringRule` es la clase base abstracta
(`Subscription : RecurringRule` y `FixedExpense : RecurringRule`
heredan). El repositorio maneja la tabla unificada de recurrentes;
los repos separados por tipo concreto se añadirán en features
futuras si es necesario.

### RF-7 — `IGoalRepository`

```csharp
public interface IGoalRepository
{
    Task<Result> AddAsync(Goal goal, CancellationToken ct = default);
    Task<Result> UpdateAsync(Goal goal, CancellationToken ct = default);
    Task<Result> RemoveAsync(Guid id, CancellationToken ct = default);
    Task<Goal?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Goal>> GetAllAsync(CancellationToken ct = default);
}
```

### RF-8 — `IGoalContributionRepository`

```csharp
public interface IGoalContributionRepository
{
    Task<Result> AddAsync(GoalContribution contribution, CancellationToken ct = default);
    Task<Result> UpdateAsync(GoalContribution contribution, CancellationToken ct = default);
    Task<Result> RemoveAsync(Guid id, CancellationToken ct = default);
    Task<GoalContribution?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<GoalContribution>> GetAllAsync(CancellationToken ct = default);
}
```

### RF-9 — `IBudgetRepository`

```csharp
public interface IBudgetRepository
{
    Task<Result> AddAsync(Budget budget, CancellationToken ct = default);
    Task<Result> UpdateAsync(Budget budget, CancellationToken ct = default);
    Task<Result> RemoveAsync(Guid id, CancellationToken ct = default);
    Task<Budget?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Budget>> GetAllAsync(CancellationToken ct = default);
}
```

### RF-10 — `ICategoryRepository`

```csharp
public interface ICategoryRepository
{
    Task<Result> AddAsync(Category category, CancellationToken ct = default);
    Task<Result> UpdateAsync(Category category, CancellationToken ct = default);
    Task<Result> RemoveAsync(Guid id, CancellationToken ct = default);
    Task<Category?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken ct = default);
}
```

### RF-11 — `ITagRepository`

```csharp
public interface ITagRepository
{
    Task<Result> AddAsync(Tag tag, CancellationToken ct = default);
    Task<Result> UpdateAsync(Tag tag, CancellationToken ct = default);
    Task<Result> RemoveAsync(Guid id, CancellationToken ct = default);
    Task<Tag?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Tag>> GetAllAsync(CancellationToken ct = default);
}
```

### RF-12 — `IPaymentMethodRepository`

```csharp
public interface IPaymentMethodRepository
{
    Task<Result> AddAsync(PaymentMethod paymentMethod, CancellationToken ct = default);
    Task<Result> UpdateAsync(PaymentMethod paymentMethod, CancellationToken ct = default);
    Task<Result> RemoveAsync(Guid id, CancellationToken ct = default);
    Task<PaymentMethod?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<PaymentMethod>> GetAllAsync(CancellationToken ct = default);
}
```

### RF-13 — `ISavedViewRepository`

```csharp
public interface ISavedViewRepository
{
    Task<Result> AddAsync(SavedView savedView, CancellationToken ct = default);
    Task<Result> UpdateAsync(SavedView savedView, CancellationToken ct = default);
    Task<Result> RemoveAsync(Guid id, CancellationToken ct = default);
    Task<SavedView?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<SavedView>> GetAllAsync(CancellationToken ct = default);
}
```

### RF-14 — `IAnomalyAlertRepository`

```csharp
public interface IAnomalyAlertRepository
{
    Task<Result> AddAsync(AnomalyAlert alert, CancellationToken ct = default);
    Task<Result> UpdateAsync(AnomalyAlert alert, CancellationToken ct = default);
    Task<Result> RemoveAsync(Guid id, CancellationToken ct = default);
    Task<AnomalyAlert?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<AnomalyAlert>> GetAllAsync(CancellationToken ct = default);
}
```

### RF-15 — `ISettingRepository`

```csharp
public interface ISettingRepository
{
    Task<Result> AddAsync(Setting setting, CancellationToken ct = default);
    Task<Result> UpdateAsync(Setting setting, CancellationToken ct = default);
    Task<Result> RemoveAsync(Guid id, CancellationToken ct = default);
    Task<Setting?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Setting>> GetAllAsync(CancellationToken ct = default);
}
```

### RF-16 — `IUnitOfWork`

```csharp
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
```

Nota: `IUnitOfWork` solo expone `SaveChangesAsync` (commit
implícito). Versiones futuras podrían añadir
`BeginTransactionAsync` / `CommitAsync` / `RollbackAsync` para
control explícito de transacciones, pero F-006 mantiene la API
mínima. `SaveChangesAsync` retorna `int` (filas afectadas, útil
para verificación de éxito).

### RF-17 — Devolución de `Task<Result>` para commands

Para los métodos `Add`, `Update`, `Remove`, la spec exige
`Task<Result>`. Esto permite reportar errores de negocio
esperados (e.g., "transacción no existe", "concurrencia
optimista falló", "violación de constraint") sin lanzar
excepciones.

### RF-18 — Devolución de `Task<T?>` para `GetById`

`GetByIdAsync` retorna `Task<T?>` (no `Task<T>`). Si la entidad
no existe, retorna `null` (no una excepción). El caller verifica
con `if (entity is null) { ... }`.

### RF-19 — Devolución de `Task<IReadOnlyList<T>>` para colecciones

`GetAllAsync` retorna `Task<IReadOnlyList<T>>`. La lista es
read-only, lo que el caller no puede mutar (lo que evita errores
accidentales de "el caller modificó la lista que el repo pensó
que era inmutable").

### RF-20 — `CancellationToken` por defecto

Todos los métodos asíncronos tienen un parámetro
`CancellationToken ct = default` opcional. Esto permite que el
caller pase un token de cancelación si quiere, pero no lo obliga.

---

## 7. Requisitos no funcionales

### RNF-1 — Pureza de Application
- Las 15 interfaces (14 repos + IUnitOfWork) están en
  `Application/Abstractions/`. Dependen solo de:
  - `System.Threading.Tasks` (Task, CancellationToken)
  - `System.Collections.Generic` (IReadOnlyList<T>)
  - `ControlDeFinanzasIA.Application.Common` (Result)
  - Los tipos de entidades (Expense, Income, etc.) que viven en
    `Domain`.
- **NO** dependen de:
  - `Microsoft.EntityFrameworkCore` ni de nada de
    `Infrastructure`.
  - `Infrastructure` ni nada de la capa de persistencia.
- Esto se valida con NetArchTest en F-007: `Application` no debe
  tener `ProjectReference` a `Infrastructure` y no debe tener
  `PackageReference` a EF Core.

### RNF-2 — Inmutabilidad
- Las interfaces no tienen estado (son contratos). No hay
  threadsafety que validar.

### RNF-3 — Cobertura de tests (N/A para F-006)
- No hay tests. La spec dice "Sin tests todavía (no hay
  implementación que probar)". Los tests entran en F-002 de Fase
  2 cuando se implementen los repositorios en `Infrastructure` con
  EF Core in-memory.

### RNF-4 — Sin estado mutable global
- No hay singletons, no hay static state. Cada use case recibe
  sus repos por DI.

---

## 8. Reglas de negocio

### RN-1 — Los repositorios no validan reglas de negocio
- Los repositorios son una capa de persistencia. La validación
  de reglas de negocio se hace en el dominio (entidades) o en el
  use case (FluentValidation). Los repos solo verifican
  constraints físicos (e.g., que la entidad existe antes de
  actualizar).

### RN-2 — `Add` con ID existente retorna `IsSuccess = false`
- Si el caller llama a `AddAsync` con una entidad cuyo `Id` ya
  existe en el store, el repo retorna
  `Result.Failure("AlreadyExists", "...")` en vez de lanzar una
  excepción. Esto es consistente con el patrón "errores esperados
  como valores" (RN-3 de F-005).

### RN-3 — `Update`/`Remove` con ID inexistente retorna `IsSuccess = false`
- Si el ID no existe, el repo retorna
  `Result.Failure("NotFound", "...")`. La interfaz
  `IUnitOfWork.SaveChangesAsync` puede retornar 0 (filas
  afectadas), lo que también es un indicador de no-encontrado.

### RN-4 — `GetById` retorna `null` para no-encontrado
- `GetById` retorna `Task<T?>`. No retorna `default(T)` (que para
  tipos de valor es `0` o `false`, lo que sería ambiguo con
  "existe una entidad con esos valores"). El caller verifica
  explícitamente con `null`.

### RN-5 — Las queries devuelven `IReadOnlyList<T>`, no `List<T>`
- `GetAll` retorna `IReadOnlyList<T>` para evitar que el caller
  mute la lista retornada. El repo es dueño de la lista.

---

## 9. Modelo de dominio

```
Application/Abstractions/

  IUnitOfWork
    + SaveChangesAsync(ct): Task<int>

  IExpenseRepository
    + AddAsync(expense, ct): Task<Result>
    + UpdateAsync(expense, ct): Task<Result>
    + RemoveAsync(id, ct): Task<Result>
    + GetByIdAsync(id, ct): Task<Expense?>
    + GetAllAsync(ct): Task<IReadOnlyList<Expense>>

  IIncomeRepository        (mismo patrón, Income en vez de Expense)
  IDebtRepository         (mismo patrón, Debt en vez de Expense)
  IDebtPaymentRepository  (mismo patrón, DebtPayment)
  IRecurringRuleRepository(mismo patrón, RecurringRule)
  IGoalRepository         (mismo patrón, Goal)
  IGoalContributionRepository (mismo patrón, GoalContribution)
  IBudgetRepository       (mismo patrón, Budget)
  ICategoryRepository     (mismo patrón, Category)
  ITagRepository          (mismo patrón, Tag)
  IPaymentMethodRepository(mismo patrón, PaymentMethod)
  ISavedViewRepository    (mismo patrón, SavedView)
  IAnomalyAlertRepository (mismo patrón, AnomalyAlert)
  ISettingRepository      (mismo patrón, Setting)
```

- **15 interfaces en total** (14 repositorios + 1 IUnitOfWork).
- Patrón uniforme: AddAsync, UpdateAsync, RemoveAsync, GetByIdAsync,
  GetAllAsync.
- Commands devuelven `Task<Result>` (no genérico, ver nota de
  diseño en §1).
- Queries devuelven `Task<T?>` o `Task<IReadOnlyList<T>>`.
- Sin implementación. Los métodos tienen cuerpo vacío en la
  declaración (interface).

---

## 10. Validaciones

Las interfaces de repositorio no validan nada internamente
(solo declaran el contrato). Las validaciones ocurren en el
dominio (entidades) o en el use case (FluentValidation). Los
repos solo verifican constraints físicos (e.g., existencia por ID).

| # | Validación | Cuándo se aplica | Comportamiento esperado |
| --- | --- | --- | --- |
| V-1 | Entidad cumple invariantes | En el dominio (entidad) | Lanza `ArgumentException` en el setter si se viola |
| V-2 | Entidad existe (por ID) | En la implementación de `GetById` | Retorna `null` si no existe |
| V-3 | Constraint físicos (unique key, FK) | En la implementación (EF Core) | Retorna `Result.Failure` con código descriptivo |

---

## 11. Casos borde

### EC-1 — `Add` con entidad `null`
- `AddAsync(null!)` lanza `ArgumentNullException` (defensivo;
  la interfaz no tiene constraint `where T : notnull` porque
  los repositorios son interfaces, no clases genéricas).

### EC-2 — `GetById` con `Guid.Empty` (default)
- Comportamiento depende de la implementación. Lo más probable:
  retorna `null` (no existe ninguna entidad con `Id = Guid.Empty`).

### EC-3 — `GetAll` cuando la tabla está vacía
- Retorna `Task<IReadOnlyList<T>>` con lista vacía (no `null`).
  El caller verifica con `if (list.Count == 0)`.

### EC-4 — `SaveChangesAsync` sin cambios pendientes
- Retorna `int = 0` (filas afectadas). No es un error; es un
  resultado normal.

### EC-5 — `SaveChangesAsync` cuando la BD falla
- Lanza una excepción (e.g., `DbUpdateException`). Esto no es un
  error esperado; es un fallo irrecuperable que el caller maneja
  con un catch global.

### EC-6 — `Update` con la misma entidad en memoria
- Sin cambios: la entidad que se pasa a `Update` es la misma que se
  lee. `SaveChangesAsync` retorna 0 (no hay cambios para
  persistir).

### EC-7 — `Remove` con `Guid` válido pero sin fila
- El repo no puede distinguir "ID válido, fila inexistente" de
  "ID inválido". Retorna `Result.Failure("NotFound", "...")` o
  0 filas afectadas en `SaveChangesAsync`.

### EC-8 — Concurrencia optimista
- `Update` y `Remove` con un `Id` que cambió desde la lectura
  retornan `Result.Failure("ConcurrencyConflict", "...")` (la
  implementación usa EF Core's RowVersion o similar).

---

## 12. Dependencias

### 12.1 Prerrequisitos de implementación

- **F-001 completada**: la solución, los proyectos y la
  configuración global están listos. `Application/Abstractions/`
  existe (vacío).
- **F-002 completada** (recomendado): establece `Money` y el patrón
  de value object. F-006 referencia los tipos de valor de F-002
  (Money, DateRange) en las entidades (e.g., `Expense.Amount` es
  `Money`).
- **F-003 completada** (recomendado): establece `DateRange`. F-006
  lo usa en `Debt` y `RecurringRule`.

### 12.2 Dependencias con otras features

| Feature | Relación |
| --- | --- |
| F-002 (Money) | Las entidades de F-006 usan `Money`. |
| F-003 (DateRange) | Las entidades de F-006 usan `DateRange` (e.g., `Debt.StartDate`, `RecurringRule.StartDate`). |
| F-004 (IClock, IPathResolver) | Las implementaciones de F-006 en `Infrastructure` los usarán (e.g., `IRecurringRuleRepository` consultará con `IClock` para reglas activas). |
| F-005 (Result, PagedResult) | F-006 usa `Result` (non-generic) para commands y `PagedResult<T>` (genérico) para queries paginadas (las queries complejas se añadirán en features futuras, no en F-006). |
| F-007 (NetArchTest) | F-007 valida que `Application` no tiene `PackageReference`s a EF Core, y que `Domain` no referencia a `Application/Abstractions/` (en F-006, las entidades sí referencian a los repositorios). |
| F-008 (Composition root) | F-008 registra las implementaciones de F-006 en el contenedor de DI. |
| F-002 de Fase 2 (Persistencia) | **Consumidor principal**: implementa los repositorios de F-006 con EF Core + SQLite. |

### 12.3 Bloqueos

- **Ninguno para F-006.** Las interfaces de F-006 no requieren que
  las entidades estén implementadas (la spec las referencia como
  tipos del `Domain`). Sin embargo, **el proyecto F-006
  compilará solo si las entidades existen**. Las entidades
  (`Expense`, `Income`, `Debt`, etc.) están definidas en
  `architecture.md` pero no como entidades concretas de C#. Si el
  proyecto F-006 se implementa y las entidades no existen, la
  compilación fallará con CS0246.
- **Decisión:** si las entidades no existen, F-006 introduce
  clases placeholder de las entidades. Esto se documenta en §16.

---

## 13. Fuera de alcance (detallado)

- **Implementaciones concretas de los repositorios** (en
  `Infrastructure/Persistence/Repositories/`). F-002 de Fase 2.
  Aquí NO.
- **`AppDbContext`** y configuraciones de EF Core. F-002 de Fase 2.
  Aquí NO.
- **Migraciones**. F-002 de Fase 2. Aquí NO.
- **`IDbTransaction` o `BeginTransactionAsync`**. F-006 solo
  expone `SaveChangesAsync` (commit implícito). El control
  explícito de transacciones se añadirá en features futuras si es
  necesario.
- **Patrón genérico `IRepository<T, TId>`**. La spec define
  interfaces específicas por agregado. La razón está en
  `architecture.md` §4: cada agregado tiene queries distintas que
  no se pueden generalizar de forma type-safe.
- **Repos con lógica de validación de negocio**. Los repos
  solo persisten. La validación se hace en el dominio o en el use
  case.
- **Paginación en las queries básicas**. F-006 define
  `GetAll` (sin paginación) y `GetById` (sin lista). Las queries
  paginadas se añadirán como métodos adicionales cuando los use
  cases las necesiten (e.g.,
  `GetTransactionsByMonthPagedAsync`).
- **Métodos de batch** (AddRange, UpdateRange, etc.). Se pueden
  añadir en features futuras si son necesarios.
- **Versionado automático de las entidades** (timestamps,
  concurrency tokens). Se define en las propias entidades, no en
  los repositorios.
- **Patrón `Specification`** para queries complejas. La spec
  define solo `GetById` y `GetAll`; las queries complejas usarán
  métodos específicos en futuras features.

---

## 14. Criterios de aceptación

La feature se considera completada cuando **todos** los siguientes
puntos se cumplen.

### AC-1 — Las 15 interfaces existen y compilan
- [ ] `IUnitOfWork` existe en
      `src/ControlDeFinanzasIA.Application/Abstractions/IUnitOfWork.cs`.
- [ ] Las 14 interfaces de repositorio existen en
      `src/ControlDeFinanzasIA.Application/Abstractions/`
      (una por archivo).
- [ ] `dotnet build`: 0 warnings, 0 errors.
- [ ] `Application` sigue sin `PackageReference`s (sin EF Core, sin
      nada de `Infrastructure`).
- [ ] `Application` no tiene `ProjectReference` a `Infrastructure`.

### AC-2 — Convenciones del repo
- [ ] Las interfaces siguen el `.editorconfig` (file-scoped
      namespace, sin `using` redundantes, sin `var` que el IDE
      marca como no aparente).
- [ ] XML doc en cada interface y en cada método público.
- [ ] Naming consistente con el resto del repo (PascalCase para
      interfaces, métodos).
- [ ] Sin placeholders en el código (los archivos no son
      `IRepository1.cs`, etc.).

### AC-3 — Compatibilidad arquitectónica
- [ ] `Domain` no referencia a `Application/Abstractions/` (las
      entidades NO importan los repositorios). Verificable por
      `grep -r "using ControlDeFinanzasIA.Application.Abstractions"`
      en `src/ControlDeFinanzasIA.Domain` debe retornar 0 líneas.
- [ ] `dotnet list src/.../Application package` sigue mostrando
      "No packages were found".
- [ ] NetArchTest (F-007) verifica que `Application` no tiene
      dependencias de `Infrastructure` ni de EF Core.

### AC-4 — Cobertura de tests
- [ ] N/A. La spec dice explícitamente que F-006 no tiene tests.
      Los tests entran en F-002 de Fase 2 cuando se creen los
      repos reales.

### AC-5 — Verificación final
- [ ] `dotnet build -c Debug && -c Release`: 0 warnings, 0 errors.
- [ ] `dotnet format style --verify-no-changes`: exit 0.
- [ ] `Application/Abstractions/` contiene los 15 archivos
      (.cs, uno por interface).
- [ ] `spec/phases/phase-01/features.md` actualizado con F-006
      marcada como completada.
- [ ] Spec renombrada a `.done.md`.

---

## 15. Estructura de archivos esperada

```
src/
└── ControlDeFinanzasIA.Application/
    ├── Abstractions/
    │   ├── IClock.cs                       (F-004, ya existe)
    │   ├── IPathResolver.cs                (F-004, ya existe)
    │   ├── IUnitOfWork.cs                  ← nuevo (F-006)
    │   ├── IExpenseRepository.cs           ← nuevo (F-006)
    │   ├── IIncomeRepository.cs            ← nuevo (F-006)
    │   ├── IDebtRepository.cs              ← nuevo (F-006)
    │   ├── IDebtPaymentRepository.cs       ← nuevo (F-006)
    │   ├── IRecurringRuleRepository.cs     ← nuevo (F-006)
    │   ├── IGoalRepository.cs              ← nuevo (F-006)
    │   ├── IGoalContributionRepository.cs  ← nuevo (F-006)
    │   ├── IBudgetRepository.cs            ← nuevo (F-006)
    │   ├── ICategoryRepository.cs          ← nuevo (F-006)
    │   ├── ITagRepository.cs               ← nuevo (F-006)
    │   ├── IPaymentMethodRepository.cs     ← nuevo (F-006)
    │   ├── ISavedViewRepository.cs         ← nuevo (F-006)
    │   ├── IAnomalyAlertRepository.cs      ← nuevo (F-006)
    │   ├── ISettingRepository.cs           ← nuevo (F-006)
    │   └── ...
    ├── Common/                            (F-005, ya existe)
    └── ...

tests/
└── ControlDeFinanzasIA.Tests.Unit/
    ├── (sin tests nuevos para F-006; los tests entran en F-002 de
    │   Fase 2 cuando se creen los repos reales)
    └── ...
```

---

## 16. Decisiones de diseño explícitas

### 16.1 — Interfaces específicas por agregado, no `IRepository<T, TId>` genérico

`architecture.md` §4 establece explícitamente: "**No** se usa el
patrón genérico `<TEntity, TId>`; cada repo expone métodos
específicos del dominio." Esta es una decisión deliberada porque
cada agregado tiene queries distintas que no se pueden
generalizar sin perder type-safety.

### 16.2 — `Task<Result>` (non-generic) para commands, no `Task<Result<T>>`

La spec original proponía `Task<Result<T>>`. Tras la decisión de
F-005 de cambiar `Result<T>` a `Result` non-generic (con
`Value: object?`), F-006 usa `Task<Result>`. El caller no necesita
el `Value` para un command exitoso: típicamente el use case ya tiene
la entidad que acaba de crear/persistir. Si el command falla,
`Result.Error` tiene el detalle del error.

### 16.3 — `Task<T?>` para `GetById`, no `Task<T>`

`T?` indica claramente la posibilidad de "no encontrado". El caller
verifica con `if (entity is null) { ... }`. Esto es mejor que
`Task<T>` con `default(T)` (que para tipos de valor es 0/false,
ambiguo con "existe una entidad con esos valores").

### 16.4 — `ID` como `Guid` en todas las entidades

Consistente con el modelo de dominio en `architecture.md`. Las
entidades tienen `Id: Guid` como primary key. Si en el futuro se
cambia a otro tipo (e.g., `long`), la spec del repositorio se
actualiza. La spec de F-006 es genérica sobre el ID (un solo
`Guid` para todas las entidades).

### 16.5 — `IReadOnlyList<T>` para `GetAll`, no `List<T>`

`IReadOnlyList<T>` garantiza que el caller no puede mutar la lista
retornada. El repo es dueño de la lista. Si el caller necesita
mutarla, debe crear una nueva colección.

### 16.6 — Sin tests (decisión explícita)

La spec dice "Sin tests todavía (no hay implementación que
probar). Los tests entran en la fase 2 cuando se creen los repos
en `Infrastructure`." Esta es una decisión deliberada: la lógica
a probar (queries reales, transacciones, concurrencia) depende
de la implementación concreta (EF Core + SQLite), no de las
interfaces.

### 16.7 — Entidades: placeholders si no existen

Si las entidades (`Expense`, `Income`, `Debt`, `RecurringRule`,
`Subscription`, `FixedExpense`, `Goal`, `GoalContribution`,
`Budget`, `Category`, `Tag`, `PaymentMethod`, `SavedView`,
`AnomalyAlert`, `Setting`) no existen como tipos de C# cuando se
implementa F-006, hay dos opciones:

1. **Crear placeholders** de las entidades en `Domain` con la
   estructura mínima (Id, propiedades relevantes vacías). Esto
   permite que F-006 compile. Las features futuras refinan
   las entidades.
2. **Esperar a que las entidades existan** (F-006 se implementa
   después de las features que definen las entidades). Esto
   retrasa F-006 innecesariamente.

**Decisión recomendada: opción 1.** Los placeholders son simples
y F-006 desbloquea la cobertura de `Application` y la regla de
NetArchTest en F-007.

---

## 17. Riesgos y mitigaciones

| Riesgo | Probabilidad | Impacto | Mitigación |
| --- | --- | --- | --- |
| El test project no compila por las interfaces genéricas o el uso de `Guid` | Baja | Alto | El probe con F-005 pasó con la misma `Guid`. Si el issue reaparece, replicar la fix (non-generic). |
| Las entidades no existen y F-006 no compila | Media | Alto | Crear placeholders en `Domain`. Las features futuras refinan. |
| Las queries complejas se añaden fuera de F-006 (en features posteriores) — riesgo de que las interfaces se vuelvan obsoletas | Baja | Medio | F-006 es explícitamente "básica". La spec documenta que queries complejas se añaden en futuras features. |
| El caller intenta mutar la `IReadOnlyList<T>` de `GetAll` | Baja | Bajo | `IReadOnlyList` lo impide en tiempo de compilación. |
| Las interfaces quedan "huérfanas" sin implementaciones durante mucho tiempo | Baja | Bajo | F-002 de Fase 2 implementa los repos inmediatamente después de F-006. |
| Acoplamiento accidental a `Infrastructure` al implementar F-006 | Baja | Alto | F-006 NO importa nada de `Infrastructure`. Si lo hace accidentalmente, F-007 lo detecta. |

---

## 18. Referencias

- `docs/vision.md` — describe los casos de uso que requieren
  persistencia.
- `docs/architecture.md` — define las entidades, las interfaces
  listadas, el patrón de `IRepository<T, TId>` (NO usado), y
  `IUnitOfWork`. Fuente de verdad para los nombres de las
  entidades.
- `docs/roadmap.md` § Fase 1, F-006 dentro del desglose.
- `spec/constitution/tech-stack.md` — convenciones de código.
- `spec/phases/phase-01/features.md` — F-006 dentro del
  desglose de features de la fase 1.
- `spec/features/phase-01/F-001-skeleton-solution.done.md` —
  provee la base: `Application/Abstractions/` existe (vacío).
- `spec/features/phase-01/F-002-money-value-object.done.md` —
  patrón de value object.
- `spec/features/phase-01/F-003-datarange-value-object.done.md` —
  patrón de value object.
- `spec/features/phase-01/F-004-Abstracciones-clock-pathresolver.done.md` —
  convención de ubicación de interfaces.
- `spec/features/phase-01/F-005-comuntypes-result-pagedresult.done.md` —
  patrón de `Result` (non-generic con `Value: object?`).
- `spec/README.md` — convención completa del directorio `spec/`.
