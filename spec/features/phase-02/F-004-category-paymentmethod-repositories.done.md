# Specification — F-004: Repositorios Concretos de Category y PaymentMethod

> **Fase:** 2 — Persistencia con EF Core + SQLite
> **Feature:** F-004 (local a esta fase)
> **Estado:** ✅ COMPLETADA
> **Spec ID:** SPEC-F004P2
>
> **Nota de numeración:** este `F-004` es el de la **Fase 2**
> (`spec/phases/phase-02/features.md`). No debe confundirse con
> el `F-004` de Fase 1 (Abstracciones IClock e IPathResolver
> en `spec/phases/phase-01/features.md`). Los IDs son locales
> a cada fase; el folder los desambigua. Ver `spec/README.md`
> para la convención.
>
> Fuentes de verdad: `docs/vision.md`, `docs/architecture.md`,
> `docs/roadmap.md` (Fase 2), `spec/constitution/tech-stack.md`,
> `spec/constitution/mission.md`,
> `spec/phases/phase-02/features.md` (F-004 dentro del
> desglose de la fase 2),
> `spec/features/phase-02/F-001-appdbcontext-addpersistence.done.md`
> (provee `AppDbContext`, `IUnitOfWork`, y
> `AddPersistence` que F-004 extiende),
> `spec/features/phase-02/F-002-entity-configurations-migration.done.md`
> (provee las configuraciones y la migración
> `InitialSchema`),
> `spec/features/phase-02/F-003-seed-data.done.md` (provee
> el seed con 9 categorías y 5 métodos de pago),
> `spec/features/phase-01/F-006-contrato-interfaces-repo.done.md`
> (define las 15 interfaces, incluyendo
> `ICategoryRepository` e `IPaymentMethodRepository` con
> la firma de 5 métodos que F-004 implementa).

---

## 1. Resumen

F-004 implementa las **clases concretas** de los 2
repositorios mínimos: `CategoryRepository` y
`PaymentMethodRepository`. Son las **primeras 2 de
las 15** interfaces de repositorio que F-006 de
Phase 1 definió. Las implementaciones siguen
exactamente la firma de 5 métodos de las interfaces
(`AddAsync`, `UpdateAsync`, `RemoveAsync`,
`GetByIdAsync`, `GetAllAsync`), con validaciones de
reglas de negocio vía `Result.Failure` con códigos
descriptivos (`NotFound`, `DuplicateName`,
`SystemCategoryImmutable`,
`SystemPaymentMethodImmutable`).

Al final de la feature:
- `CategoryRepository` y `PaymentMethodRepository`
  existen en
  `Infrastructure/Persistence/Repositories/`
  como `public sealed class`.
- Ambos repos están **registrados** en
  `AddPersistence` (de F-001) como `Scoped`, junto
  con `AppDbContext` e `IUnitOfWork`.
- `dotnet build` y `dotnet test` (los 111 tests
  existentes) siguen pasando en limpio.
- Los 11 tests de NetArchTest de F-007 (R-1 a R-5)
  siguen pasando.
- F-005 (composition root) puede llamar a
  `AddPersistence` y obtener los 2 repos
  resolubles desde DI.

---

## 2. Contexto

F-006 de Phase 1 consolidó las 15 interfaces de
repositorio en
`src/ControlDeFinanzasIA.Application/Abstractions/`
con la firma de 5 métodos. F-001 de Phase 2 proveyó
`AppDbContext`, `EfUnitOfWork`, y el extension method
`AddPersistence`. F-002 de Phase 2 agregó las
configuraciones de las 2 entidades mínimas
(`CategoryConfiguration`,
`PaymentMethodConfiguration`). F-003 de Phase 2
sembró las 9 categorías y los 5 métodos de pago
del sistema.

F-004 cierra el ciclo. Implementa los 2 repositorios
concretos que faltan para que la app pueda hacer
CRUD sobre las entidades sembradas. Es la primera
feature de Phase 2 que **produce código de
aplicación real** (no solo infraestructura de EF
Core).

**Por qué solo 2 repositorios y no los 15:**

El phase planning doc
(`spec/phases/phase-02/features.md` §F-004)
específica: "Son las **primeras 2 de las 15** que
F-006 de Phase 1 definió; los otros 13 repositorios
se implementan en las fases correspondientes (Fase
3+ del roadmap)." Implementar los 13 restantes en
F-004 sería scope creep: las entidades
correspondientes (Expense, Income, Debt, etc.)
tienen value objects (`Money`, `DateRange`) que
requieren value converters en EF Core, y los use
cases que las consumen llegan en Fase 3+. Hacer
los repositorios de las entidades mínimas (Category
y PaymentMethod, que no tienen value objects)
permite validar el patrón end-to-end antes de
extenderlo a las 13 más complejas.

**Por qué F-004 modifica `AddPersistence` (de F-001):**

F-001 §RF-4 lo anticipó: "**NO** registra los 15
repositorios concretos. Eso es F-004." Ahora F-004
añade los registros de los 2 repos que implementa.
Los 13 restantes se añadirán en features futuras
cuando se implementen sus repos.

**Por qué la firma de 5 métodos (no 7):**

F-006 de Phase 1 consolidó la firma a 5 métodos.
El borrador original del F-002 de Phase 2 (borrado)
proponía 7 métodos (incluyendo
`GetSystemCategoriesAsync` y `ExistsByNameAsync`);
F-006 los descartó para mantener consistencia con
las 15 interfaces. F-004 respeta esa decisión. Las
validaciones de negocio se implementan **dentro** de
los 5 métodos, no como métodos adicionales.

---

## 3. Objetivo

1. **Crear `CategoryRepository`** en
   `Infrastructure/Persistence/Repositories/CategoryRepository.cs`,
   implementando `ICategoryRepository` con los 5
   métodos estándar.
2. **Crear `PaymentMethodRepository`** en
   `Infrastructure/Persistence/Repositories/PaymentMethodRepository.cs`,
   implementando `IPaymentMethodRepository` con
   los 5 métodos estándar.
3. **Modificar `AddPersistence`** (de F-001) para
   registrar los 2 repos como `Scoped` después de
   `AppDbContext` e `IUnitOfWork`.
4. **NO** crear los otros 13 repositorios (queda
   para futuras features de Fase 3+).
5. **NO** añadir tests de integración (queda para
   F-006 de Phase 2).

---

## 4. Alcance

### 4.1 En alcance

- Creación de
  `src/ControlDeFinanzasIA.Infrastructure/Persistence/Repositories/CategoryRepository.cs`
  (nuevo).
- Creación de
  `src/ControlDeFinanzasIA.Infrastructure/Persistence/Repositories/PaymentMethodRepository.cs`
  (nuevo).
- Modificación de
  `src/ControlDeFinanzasIA.Infrastructure/Persistence/ServiceCollectionExtensions.cs`
  (de F-001) para registrar los 2 repos en
  `AddPersistence`.
- Verificación de que el build y los 111 tests
  existentes siguen pasando.
- Verificación de que el test R-1 de NetArchTest
  (F-007) sigue pasando (`Domain` sigue sin
  paquetes externos).

### 4.2 Fuera de alcance

- **Los otros 13 repositorios** (Fase 3+ del
  roadmap). F-004 solo implementa `Category` y
  `PaymentMethod`.
- **Uso explícito de `IUnitOfWork` desde los
  repositorios.** Los repos usan
  `_db.SaveChangesAsync` directamente. `IUnitOfWork`
  está disponible para use cases que necesiten
  coordinar varios repos en una sola transacción
  (Fase 3+); F-004 no lo usa.
- **Tests de integración con SQLite in-memory.** Es
  F-006 de Phase 2. F-004 implementa el código;
  F-006 lo testea.
- **Modificación de las interfaces** (de F-006 de
  Phase 1). F-004 respeta la firma; si se necesitan
  más métodos, se añaden en features futuras con
  su propia spec.
- **Wiring de `AddPersistence` en
  `Bootstrap.ConfigureServices`.** Es F-005. F-004
  modifica `AddPersistence`; F-005 lo llama.
- **Migraciones adicionales.** F-004 no genera
  migraciones; usa el schema existente (de F-002)
  y los datos sembrados (de F-003).
- **Conversión de value objects** (`Money`,
  `DateRange`) en EF Core. No aplica a `Category` y
  `PaymentMethod` (no tienen esos campos).
- **Modificaciones a `Domain`, `Application`,
  `UI`, `Tests.Unit`, `Tests.Integration`.** F-004
  solo toca `Infrastructure`.
- **El CI workflow.** El workflow existente sigue
  siendo suficiente.

---

## 5. Casos de uso

### CU-1 — El usuario lista todas las categorías

- **Actor:** un ViewModel (futuro, Fase 3) o un
  use case de la capa `Application` (cuando se
  cree, F-005+).
- **Trigger:** la UI muestra la lista de
  categorías en un dropdown.
- **Precondición:** F-005 ha llamado a
  `AddPersistence` y ha resuelto
  `ICategoryRepository` desde el container.
- **Postcondición:** `GetAllAsync` devuelve
  `IReadOnlyList<Category>` con las 9 categorías
  del sistema + las custom (si el usuario ha
  creado alguna). Ordenadas alfabéticamente por
  nombre (con collation `NOCASE` de SQLite).

### CU-2 — El usuario crea una categoría custom

- **Actor:** un use case de la capa `Application`
  (futuro, Fase 3+).
- **Trigger:** la UI muestra un diálogo de creación
  de categoría.
- **Precondición:** la entidad a crear tiene
  `Id` nuevo (Guid), `Name` no vacío, `IsSystem =
  false`, `CreatedAt`/`UpdatedAt` en el momento
  actual.
- **Postcondición:** la categoría se persiste en
  la DB. Si el `Name` ya existe (case-insensitive
  vía `NOCASE`), el repo devuelve
  `Result.Failure("DuplicateName", "...")`. Si
  éxito, devuelve `Result.Success()`.

### CU-3 — El usuario edita una categoría custom

- **Actor:** un use case de la capa `Application`.
- **Trigger:** la UI muestra un diálogo de edición.
- **Precondición:** la categoría existe en la DB
  (con el ID dado).
- **Postcondición:** los cambios se persisten. Si
  el ID no existe, el repo devuelve
  `Result.Failure("NotFound", "...")`. Si
  `IsSystem=true` y el `Name` cambia, devuelve
  `Result.Failure("SystemCategoryImmutable", "...")`.
  Si éxito, devuelve `Result.Success()`.

### CU-4 — El usuario intenta renombrar "Comida" (del sistema)

- **Actor:** un use case.
- **Trigger:** la UI permite editar una categoría
  con `IsSystem=true`.
- **Precondición:** la categoría "Comida" existe
  con `Id=11111111-...-000000000001`,
  `IsSystem=true`.
- **Postcondición:** el repo detecta que
  `IsSystem=true` y el `Name` cambia, devuelve
  `Result.Failure("SystemCategoryImmutable", "...")`.
  La categoría NO se modifica. La UI muestra el
  error al usuario.

### CU-5 — El usuario elimina una categoría custom

- **Actor:** un use case.
- **Trigger:** la UI muestra un diálogo de
  confirmación de eliminación.
- **Precondición:** la categoría existe.
- **Postcondición:** la categoría se elimina. Si
  el ID no existe, `Result.Failure("NotFound", "...")`.
  Si `IsSystem=true`,
  `Result.Failure("SystemCategoryImmutable", "...")`.

### CU-6 — El usuario lista los métodos de pago

- **Actor:** un use case o ViewModel.
- **Trigger:** la UI muestra un dropdown de métodos
  de pago.
- **Postcondición:** `GetAllAsync` devuelve los 5
  métodos del sistema + los custom.

### CU-7 — El usuario obtiene una categoría por ID

- **Actor:** un use case o ViewModel.
- **Trigger:** la UI carga el detalle de una
  categoría.
- **Postcondición:** `GetByIdAsync` devuelve la
  categoría, o `null` si no existe.

---

## 6. Requisitos funcionales

### RF-1 — `CategoryRepository` con 5 métodos

- `public sealed class CategoryRepository :
  ICategoryRepository` en
  `src/ControlDeFinanzasIA.Infrastructure/Persistence/Repositories/CategoryRepository.cs`.
- Constructor: `public CategoryRepository(AppDbContext
  db) => _db = db;` (campo privado
  `private readonly AppDbContext _db;`).
- Implementa los 5 métodos de `ICategoryRepository`
  (de F-006 de Phase 1):
  - `public async Task<Result> AddAsync(Category category, CancellationToken ct = default)`.
  - `public async Task<Result> UpdateAsync(Category category, CancellationToken ct = default)`.
  - `public async Task<Result> RemoveAsync(Guid id, CancellationToken ct = default)`.
  - `public async Task<Category?> GetByIdAsync(Guid id, CancellationToken ct = default)`.
  - `public async Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken ct = default)`.
- Cada método valida su input (defensivo: `null`
  checks en `category` o `id` con `Guid.Empty`).

### RF-2 — `PaymentMethodRepository` con 5 métodos

- `public sealed class PaymentMethodRepository :
  IPaymentMethodRepository` en
  `src/ControlDeFinanzasIA.Infrastructure/Persistence/Repositories/PaymentMethodRepository.cs`.
- Misma estructura que `CategoryRepository` pero
  sobre `PaymentMethod`.

### RF-3 — Lógica de `AddAsync` (categoría y método)

1. Verificar que el `Name` no esté duplicado
   consultando
   `await _db.Categories.AnyAsync(c => c.Name == category.Name, ct)`.
2. Si existe, devolver
   `Result.Failure("DuplicateName", "Ya existe una categoría con el nombre 'X'.")`.
3. Si no, `_db.Categories.Add(category)` +
   `await _db.SaveChangesAsync(ct)`.
4. Devolver `Result.Success()`.
- **Misma lógica para `PaymentMethod`** (con
  `_db.PaymentMethods`).

### RF-4 — Lógica de `UpdateAsync` (categoría y método)

1. Buscar la entidad con
   `await _db.Categories.FindAsync(new object?[] { category.Id }, ct)`.
2. Si `null`, devolver
   `Result.Failure("NotFound", "Categoría {id} no encontrada.")`.
3. Si `existing.IsSystem && existing.Name != category.Name`,
   devolver
   `Result.Failure("SystemCategoryImmutable", "No se puede renombrar una categoría del sistema.")`.
4. Si todo OK, `_db.Entry(existing).CurrentValues.SetValues(category)`
   + `await _db.SaveChangesAsync(ct)`.
5. Devolver `Result.Success()`.
- **Misma lógica para `PaymentMethod`** (con código
  de error `SystemPaymentMethodImmutable`).

### RF-5 — Lógica de `RemoveAsync` (categoría y método)

1. Buscar la entidad con `FindAsync`.
2. Si `null`, devolver
   `Result.Failure("NotFound", "Categoría {id} no encontrada.")`.
3. Si `existing.IsSystem`, devolver
   `Result.Failure("SystemCategoryImmutable", "No se puede eliminar una categoría del sistema.")`.
4. Si no, `_db.Categories.Remove(existing)` +
   `await _db.SaveChangesAsync(ct)`.
5. Devolver `Result.Success()`.
- **Misma lógica para `PaymentMethod`** (con código
  de error `SystemPaymentMethodImmutable`).

### RF-6 — `GetByIdAsync` y `GetAllAsync`

- `GetByIdAsync(Guid id, ct)`:
  - `await _db.Categories.FindAsync(new object?[] { id }, ct)`.
  - Devuelve `Category?` (null si no existe).
- `GetAllAsync(ct)`:
  - `await _db.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct)`.
  - Devuelve `IReadOnlyList<Category>` ordenado
    alfabéticamente.
- **Misma estructura para `PaymentMethod`**.

### RF-7 — `AddPersistence` registra los 2 repos

- Modificar
  `src/ControlDeFinanzasIA.Infrastructure/Persistence/ServiceCollectionExtensions.cs`
  (de F-001):
- En el método `AddPersistence`, después de
  `services.AddScoped<IUnitOfWork, EfUnitOfWork>();`,
  añadir:
  - `services.AddScoped<ICategoryRepository, CategoryRepository>();`
  - `services.AddScoped<IPaymentMethodRepository, PaymentMethodRepository>();`
- Comentar la línea
  `// Repositories registered in F-004 of Phase 2.`
  (que F-001 dejó) y borrarla (ya no aplica).
- Mantener el resto del método
  `AddPersistence` sin cambios (registros de
  `AppDbContext`, `IUnitOfWork`).

### RF-8 — Códigos de error canónicos

- `Result.Failure("NotFound", "...")` — la
  entidad no existe.
- `Result.Failure("DuplicateName", "...")` — el
  nombre ya existe (case-insensitive).
- `Result.Failure("SystemCategoryImmutable", "...")`
  — se intenta modificar o eliminar una categoría
  del sistema.
- `Result.Failure("SystemPaymentMethodImmutable", "...")`
  — idem para métodos de pago.
- Los códigos son strings (no enums) porque `Result`
  usa `string` para `Error.Code` (ver F-005 de
  Phase 1).

### RF-9 — Compilación y tests

- `dotnet build` desde la raíz: 0 warnings, 0
  errors (Debug y Release).
- `dotnet test`: 111 tests verdes (100 + 11 de
  F-007), 0 failures.
- 11 tests de NetArchTest de F-007 pasan.
- `dotnet format style --verify-no-changes`: exit
  0.

---

## 7. Requisitos no funcionales

### RNF-1 — Compatibilidad con la arquitectura

- Después de F-004, el grafo de dependencias sigue
  cumpliendo las reglas R-1 a R-5 de F-007:
  - R-1 (Domain sin EF Core): F-004 no toca
    `Domain`.
  - R-2 (Application sin EF Core): F-004 no
    implementa lógica de aplicación; solo
    persistencia.
  - R-3 (Infrastructure → Application + Domain):
    F-004 implementa las interfaces de
    `Application`, no al revés.
  - R-4 (UI no → Infrastructure salvo composition
    root): F-004 no toca `UI`.
  - R-5 (src/ no → Tests): F-004 no toca
    `Tests.Unit` ni `Tests.Integration`.
- Los 11 tests de NetArchTest pasan sin
  modificaciones.

### RNF-2 — Determinismo

- Las validaciones de unicidad consultan la DB
  (que es determinista para una DB dada).
- Los IDs son Guid (no aleatorios; generados por
  el caller en `Application`).

### RNF-3 — Async con CancellationToken

- Todos los métodos públicos aceptan
  `CancellationToken ct = default`. Pasan el
  `ct` a las llamadas async de EF Core.

### RNF-4 — Mensajes de error en español

- Los mensajes de `Result.Failure` (segundo
  parámetro) están en español (es-DO per
  `docs/vision.md` §3). La UI puede traducirlos
  (F-012) si añade i18n.

### RNF-5 — Sin scope creep

- F-004 NO implementa los otros 13 repositorios.
- F-004 NO añade tests de integración.
- F-004 NO enchufa `AddPersistence` en
  `Bootstrap.ConfigureServices` (F-005).
- F-004 NO añade migraciones.
- F-004 NO modifica las interfaces de F-006.

---

## 8. Reglas de negocio

### RN-1 — Unicidad de `Name` (case-insensitive)

- `Category.Name` y `PaymentMethod.Name` son únicos
  con collation `NOCASE` (configurado en F-002).
- El repo consulta la DB con `.AnyAsync(c =>
  c.Name == category.Name)` antes de insertar.
- Si existe (case-insensitive), devuelve
  `Result.Failure("DuplicateName", "...")`.
- **Limitación:** la consulta y la inserción no
  están en una transacción. Si dos requests
  concurrentes con el mismo nombre llegan
  simultáneamente, ambos pueden pasar la consulta
  antes de que uno se inserte. El índice único
  de la DB (de F-002) detecta el conflicto; EF
  Core lanza `DbUpdateException`. **Mitigación:**
  el repo captura `DbUpdateException` en
  `AddAsync` y la convierte a
  `Result.Failure("DuplicateName", "...")`.

### RN-2 — Entidades del sistema son inmutables

- Las 9 categorías y 5 métodos de pago con
  `IsSystem=true` (sembrados por F-003) **no
  se pueden renombrar ni eliminar**. Solo se
  pueden consultar.
- El repo detecta `IsSystem=true` en
  `UpdateAsync` y `RemoveAsync` y devuelve
  `Result.Failure("SystemCategoryImmutable", "...")`
  o `Result.Failure("SystemPaymentMethodImmutable", "...")`.
- **Limitación:** un usuario con acceso directo a
  la DB puede modificar entidades del sistema. La
  regla se aplica solo a través del repo. Esto es
  estándar en Clean Architecture (la DB es
  trusted, pero la app protege la UI).

### RN-3 — Entidad no encontrada

- Si el ID no existe, los métodos
  `UpdateAsync`, `RemoveAsync`, y `GetByIdAsync`
  lo manejan devolviendo
  `Result.Failure("NotFound", "...")` o `null`
  respectivamente.

### RN-4 — Validación antes de persistir

- Las validaciones de reglas de negocio (RN-1,
  RN-2, RN-3) se hacen **antes** de
  `_db.SaveChangesAsync()`, no después. Esto
  evita que la DB rechace operaciones que el
  dominio puede rechazar antes.
- **Limitación:** EF Core no usa transacciones
  explícitas en este scope. Si la validación
  pasa pero `SaveChangesAsync` falla por una
  razón no anticipada (e.g., constraint violation
  inesperada), el repo **no** captura la
  excepción — la deja subir. La capa superior
  (use case / UI) la traduce a un error genérico.

### RN-5 — Códigos de error como strings

- Los códigos de `Result.Failure` son strings,
  no enums. Esto se alinea con la decisión de F-005
  de Phase 1 (`Result<T>` usa `string` para
  `Error.Code`).
- Los códigos son case-sensitive. La UI y los
  tests deben usar los códigos exactos.

---

## 9. Modelo de dominio

F-004 **NO** introduce nuevas entidades de dominio.
Solo añade el modelo de **acceso a datos** para
las entidades existentes:

```
Infrastructure/Persistence/Repositories/        ← NUEVO
├── CategoryRepository.cs                      ← F-004
└── PaymentMethodRepository.cs                 ← F-004

Infrastructure/Persistence/ServiceCollectionExtensions.cs  ← F-004 (modificado: registra los 2 repos)
```

**`CategoryRepository`:**
- `public sealed class CategoryRepository :
  ICategoryRepository`.
- Constructor: `CategoryRepository(AppDbContext db)`.
- 5 métodos públicos (per RF-1).
- Campo privado: `_db` (AppDbContext).

**`PaymentMethodRepository`:**
- `public sealed class PaymentMethodRepository :
  IPaymentMethodRepository`.
- Misma estructura que `CategoryRepository`.

---

## 10. Configuración técnica

### 10.1 — Inyección de dependencias

- Los 2 repos se registran como `Scoped` (un
  `AppDbContext` por scope; el repo comparte el
  scope).
- El registro va en `AddPersistence` (de F-001),
  después de `IUnitOfWork`:

  ```text
  services.AddDbContext<AppDbContext>(opts => ...);
  services.AddScoped<IUnitOfWork, EfUnitOfWork>();
  services.AddScoped<ICategoryRepository, CategoryRepository>();
  services.AddScoped<IPaymentMethodRepository, PaymentMethodRepository>();
  ```

### 10.2 — Convenciones de código

- File-scoped namespace:
  `namespace ControlDeFinanzasIA.Infrastructure.Persistence.Repositories;`.
- `using` directives al tope (NUNCA después del
  namespace).
- XML doc en cada clase y método público,
  citando la regla de negocio que implementa.
- `sealed` en ambas clases.
- Nombres en inglés.

### 10.3 — Manejo de excepciones

- Las validaciones de unicidad de `Name` (RN-1)
  se hacen con `AnyAsync` antes de `Add` +
  `SaveChangesAsync`.
- **Race condition** (dos requests concurrentes
  con el mismo nombre): el repo captura
  `DbUpdateException` (de la constraint única
  en la DB) y la convierte a
  `Result.Failure("DuplicateName", "...")`.
- **Otras excepciones de EF Core** (concurrency,
  connection failure, etc.) **no** se capturan;
  se dejan subir. La capa superior las maneja.

### 10.4 — Logging

- F-004 NO loguea nada. `ILogger` se introduce en
  F-005 (composition root).

---

## 11. Validaciones

| # | Validación | Cuándo se aplica | Comportamiento esperado |
| --- | --- | --- | --- |
| V-1 | `category` es `null` en `AddAsync` o `UpdateAsync`. | Al llamar al método. | `ArgumentNullException.ThrowIfNull(category)`. |
| V-2 | `id` es `Guid.Empty` en `RemoveAsync` o `GetByIdAsync`. | Al llamar al método. | `ArgumentNullException.ThrowIfNull(id)` (no aplica a Guid.Empty porque Guid es struct). Mejor: devolver `Result.Failure("NotFound", "...")` para `Guid.Empty`. |
| V-3 | `Name` ya existe (case-insensitive). | En `AddAsync` antes de `Add`. | `Result.Failure("DuplicateName", "...")`. |
| V-4 | `Name` cambia en una entidad con `IsSystem=true`. | En `UpdateAsync` antes de `SetValues`. | `Result.Failure("SystemCategoryImmutable", "...")` (o `SystemPaymentMethodImmutable`). |
| V-5 | `IsSystem=true` en `RemoveAsync`. | En `RemoveAsync` antes de `Remove`. | `Result.Failure("SystemCategoryImmutable", "...")` (o `SystemPaymentMethodImmutable`). |
| V-6 | `id` no existe. | En `UpdateAsync`/`RemoveAsync` (después de `FindAsync`). | `Result.Failure("NotFound", "...")`. |
| V-7 | `DbUpdateException` (race condition). | En `AddAsync` (después de `SaveChangesAsync`). | `Result.Failure("DuplicateName", "...")`. |

---

## 12. Casos borde

### EC-1 — Race condition en unicidad

- Dos requests concurrentes con el mismo `Name`.
- Ambos pasan la consulta `AnyAsync` antes de que
  uno se inserte.
- El primero inserta OK; el segundo intenta
  insertar y la DB lanza `DbUpdateException` (por
  el índice único).
- **Mitigación:** el repo captura
  `DbUpdateException` y devuelve
  `Result.Failure("DuplicateName", "...")`. Ver
  V-7.

### EC-2 — Eliminar dos veces seguidas el mismo ID

- El primer `RemoveAsync` elimina la entidad.
- El segundo `RemoveAsync` (con el mismo ID) no la
  encuentra. Devuelve
  `Result.Failure("NotFound", "...")`.

### EC-3 — Update con una entidad eliminada por otro thread

- `FindAsync` devuelve `null`. El repo devuelve
  `Result.Failure("NotFound", "...")`.

### EC-4 — Eliminar una categoría con FK en uso (futuro, Fase 3+)

- En F-004, `Category` y `PaymentMethod` no tienen
  relaciones FK desde otras entidades (las FKs
  vienen con `Expense.CategoryId` y
  `Expense.PaymentMethodId` en Fase 3+).
- Si en el futuro un `Expense` referencia
  `CategoryId`, la DB rechazará el `Delete`
  (`DbUpdateException` por FK violation). El repo
  **no** captura esta excepción (no es
  `DbUpdateException` por unicidad). F-004 lo
  documenta; la mejora queda para cuando las FKs
  existan.

### EC-5 — `IsSystem=true` con `Name` que ya existe (no se está cambiando)

- `UpdateAsync` detecta `IsSystem=true` Y `Name`
  cambiado. Si `Name` no cambia, el repo permite
  la actualización (e.g., el usuario edita el
  `Color` o `Icon` de "Comida" sin cambiar el
  nombre). La DB lo permite.

### EC-6 — GUID v7 / v4 / etc.

- `Guid.NewGuid()` (en el caller, no en el repo)
  genera v4 por defecto en .NET 8. No hay
  restricción sobre la versión del Guid en el
  repo. El repo acepta cualquier Guid válido.

---

## 13. Dependencias

### 13.1 Prerrequisitos de implementación

- **F-001 de Fase 2** (completada): provee
  `AppDbContext` (con `Categories` y
  `PaymentMethods`), `EfUnitOfWork`, y
  `AddPersistence` (que F-004 extiende).
- **F-002 de Fase 2** (completada): provee las
  configuraciones (`CategoryConfiguration`,
  `PaymentMethodConfiguration`) con las reglas de
  unicidad, collation `NOCASE`, y longitudes
  máximas. La migración `InitialSchema` ya creó
  las 2 tablas con los índices únicos.
- **F-003 de Fase 2** (completada): sembró las 9
  categorías y 5 métodos de pago del sistema.
- **F-006 de Phase 1** (completada): define
  `ICategoryRepository` y `IPaymentMethodRepository`
  con la firma de 5 métodos.
- **F-001 a F-008 de Phase 1** (todas): proveen el
  proyecto `Infrastructure` y el resto de la
  arquitectura.

### 13.2 Dependencias con otras features

| Feature | Relación |
| --- | --- |
| F-001 (Phase 2) | Provee `AppDbContext`, `IUnitOfWork`, y `AddPersistence` que F-004 extiende. |
| F-002 (Phase 2) | Las configuraciones y la migración `InitialSchema` ya están en su lugar. F-004 no necesita migraciones nuevas. |
| F-003 (Phase 2) | El seed con 14 entidades del sistema está en su lugar. F-004 puede consultarlas desde el primer momento. |
| F-005 (Phase 2) | Llamará a `AddPersistence` desde `Bootstrap.ConfigureServices`. Los 2 repos registrados por F-004 estarán disponibles para los use cases. |
| F-006 (Phase 2) | Testeará los 2 repos con SQLite in-memory. Los IDs deterministas del seed (de F-003) permiten tests predecibles. |
| F-007 (Phase 1) | Los tests R-1 a R-5 verifican que F-004 no rompe la arquitectura. |

### 13.3 Bloqueos

- **Ninguno.** F-004 puede implementarse ahora
  mismo. Los prerrequisitos (F-001, F-002, F-003)
  están cumplidos.

---

## 14. Fuera de alcance (detallado)

Repetición con énfasis. F-004 **NO** hace lo
siguiente:

- Implementar los otros 13 repositorios
  (`IExpenseRepository`, `IIncomeRepository`, etc.).
  Esas son features de Fase 3+ del roadmap.
- Modificar las interfaces
  (`ICategoryRepository`,
  `IPaymentMethodRepository`) definidas por F-006 de
  Phase 1. F-004 respeta la firma.
- Añadir tests de integración con SQLite in-memory.
  Es F-006 de Phase 2.
- Usar `IUnitOfWork` explícitamente desde los
  repos. Los repos usan `_db.SaveChangesAsync`
  directamente.
- Generar migraciones nuevas. F-004 usa el schema
  existente (de F-002) y los datos sembrados (de
  F-003).
- Convertir value objects (`Money`, `DateRange`)
  en EF Core. No aplica a `Category` y
  `PaymentMethod`.
- Wire-up de `AddPersistence` en
  `Bootstrap.ConfigureServices`. Es F-005.
- Implementar validaciones de unicidad vía
  `ExistsByNameAsync` (un método separado). El
  método se implementa inline en `AddAsync` con
  `AnyAsync`. Si en el futuro se necesita exponer
  el chequeo, se añadirá a la interfaz.
- Implementar paginación en `GetAllAsync`. Para v1
  con 9+5 entidades (más las custom del usuario,
  digamos < 100), la query completa es
  suficiente. La paginación se añadirá en una
  feature futura si la lista crece.
- Modificaciones a `Domain`, `Application`, `UI`,
  `Tests.Unit`, `Tests.Integration`. F-004 solo
  toca `Infrastructure`.
- Modificaciones al CI workflow.
- Modificaciones a `.editorconfig`,
  `stylecop.json`, `Directory.Build.props`.

---

## 15. Criterios de aceptación

La feature se considera completada cuando
**todos** los siguientes puntos se cumplen.

### AC-1 — `CategoryRepository` creado

- [ ] `src/ControlDeFinanzasIA.Infrastructure/Persistence/Repositories/CategoryRepository.cs`
      existe.
- [ ] `public sealed class CategoryRepository :
      ICategoryRepository`.
- [ ] Constructor público que recibe `AppDbContext`.
- [ ] Implementa los 5 métodos de la interfaz con
      las firmas exactas de F-006 de Phase 1.
- [ ] File-scoped namespace.
- [ ] `using` al tope.
- [ ] XML doc en cada método.

### AC-2 — `PaymentMethodRepository` creado

- [ ] `src/ControlDeFinanzasIA.Infrastructure/Persistence/Repositories/PaymentMethodRepository.cs`
      existe.
- [ ] `public sealed class PaymentMethodRepository :
      IPaymentMethodRepository`.
- [ ] Misma estructura que `CategoryRepository`.

### AC-3 — Lógica de validación implementada

- [ ] `AddAsync` valida unicidad de `Name` con
      `AnyAsync`. Si existe, devuelve
      `Result.Failure("DuplicateName", "...")`.
- [ ] `AddAsync` captura `DbUpdateException` (race
      condition) y la convierte a
      `Result.Failure("DuplicateName", "...")`.
- [ ] `UpdateAsync` valida que la entidad existe
      (devuelve `Result.Failure("NotFound", "...")`
      si no).
- [ ] `UpdateAsync` valida que
      `IsSystem=true && Name cambia` (devuelve
      `Result.Failure("SystemCategoryImmutable", "...")`
      o
      `Result.Failure("SystemPaymentMethodImmutable", "...")`).
- [ ] `RemoveAsync` valida que la entidad existe
      (devuelve `Result.Failure("NotFound", "...")`
      si no).
- [ ] `RemoveAsync` valida que `IsSystem=true` y
      rechaza (devuelve el código de inmutabilidad
      correspondiente).

### AC-4 — `AddPersistence` registra los 2 repos

- [ ] `ServiceCollectionExtensions.AddPersistence`
      registra
      `ICategoryRepository → CategoryRepository`
      como `Scoped`.
- [ ] Registra
      `IPaymentMethodRepository → PaymentMethodRepository`
      como `Scoped`.
- [ ] El orden en `AddPersistence` es:
      `AppDbContext` → `IUnitOfWork` → los 2
      repositorios.
- [ ] La línea
      `// Repositories registered in F-004 of Phase 2.`
      (de F-001) está borrada (ya no aplica).

### AC-5 — Compilación y tests

- [ ] `dotnet build -c Release` desde la raíz:
      0 warnings, 0 errors.
- [ ] `dotnet test` desde la raíz: 111 tests
      verdes (100 + 11 de F-007), 0 failures.
- [ ] Los 11 tests de NetArchTest de F-007
      pasan sin modificaciones.

### AC-6 — Convenciones del repo

- [ ] File-scoped namespace en los 2 archivos
      de repositorio.
- [ ] `using` directives al tope.
- [ ] XML doc en cada clase y método público.
- [ ] `sealed` en ambas clases.
- [ ] `dotnet format style --verify-no-changes`:
      exit 0.

### AC-7 — Sin scope creep

- [ ] NO se han implementado los otros 13
      repositorios.
- [ ] NO se ha modificado `ICategoryRepository`
      ni `IPaymentMethodRepository` (la firma
      queda como la de F-006 de Phase 1).
- [ ] NO se han añadido tests de integración
      (F-006 de Phase 2).
- [ ] NO se ha modificado `AppDbContext.cs` (las
      `DbSet<>` ya están desde F-001).
- [ ] NO se ha modificado `Domain`,
      `Application`, `UI`, `Tests.Unit`,
      `Tests.Integration`.
- [ ] NO se ha modificado `Bootstrap.ConfigureServices`
      (F-005 lo hace).
- [ ] NO se ha generado ninguna migración nueva.

### AC-8 — Documentación y trazabilidad

- [ ] `spec/phases/phase-02/features.md`
      actualizado: F-004 marcada como ✅
      COMPLETADA en la tabla resumen y en el
      header de la sección.
- [ ] La spec se renombra a
      `spec/features/phase-02/F-004-category-paymentmethod-repositories.done.md`.

---

## 16. Estructura de archivos esperada

```
src/
└── ControlDeFinanzasIA.Infrastructure/
    └── Persistence/                            (existente)
        ├── AppDbContext.cs                     (F-001, sin cambios)
        ├── EfUnitOfWork.cs                     (F-001, sin cambios)
        ├── ServiceCollectionExtensions.cs      ← F-004 (modificado: registra los 2 repos)
        ├── DesignTimeAppDbContextFactory.cs    (F-002, sin cambios)
        ├── Configurations/                     (F-002, sin cambios)
        │   ├── CategoryConfiguration.cs
        │   └── PaymentMethodConfiguration.cs
        ├── SeedData.cs                         (F-003, sin cambios)
        ├── Migrations/                         (F-002 + F-003, sin cambios)
        │   ├── YYYYMMDDhhmmss_InitialSchema.cs
        │   ├── YYYYMMDDhhmmss_InitialSchema.Designer.cs
        │   ├── YYYYMMDDhhmmss_SeedSystemData.cs
        │   ├── YYYYMMDDhhmmss_SeedSystemData.Designer.cs
        │   └── AppDbContextModelSnapshot.cs
        └── Repositories/                       ← NUEVO (F-004)
            ├── CategoryRepository.cs            ← F-004
            └── PaymentMethodRepository.cs       ← F-004
```

**Modificaciones a `.csproj`:** ninguna.

**Modificaciones al CI workflow:** ninguna.

---

## 17. Decisiones de diseño explícitas

### 17.1 — `public sealed class` (no `internal`)

**Decisión:** los 2 repos son `public sealed class`.

**Justificación:**
- El spec de F-001 los llama `public static
  class ServiceCollectionExtensions`. Los repos
  que esa clase registra deben ser públicos
  también (si fueran `internal`, no podrían
  inyectarse en el container de DI desde otros
  assemblies).
- `sealed` comunica que no se heredan (la
  implementación es específica de EF Core).

**Alternativa descartada:** `internal sealed
class`. Reduciría la superficie pública de
`Infrastructure`, pero rompería la convención de
DI.

### 17.2 — Usar EF Core Change Tracker (no `ExecuteUpdate`/`ExecuteDelete`)

**Decisión:** los repos usan `_db.Add` +
`SaveChangesAsync`, `_db.Entry(existing).CurrentValues.SetValues(category)`
+ `SaveChangesAsync`, y `_db.Remove` +
`SaveChangesAsync`.

**Justificación:**
- Change Tracker permite interceptar y
  validar antes de persistir.
- `ExecuteUpdate`/`ExecuteDelete` (EF Core 7+)
  son más eficientes pero no permiten
  interceptar la operación para validaciones
  de negocio.
- Para v1 con 14 entidades del sistema + unas
  pocas custom, la diferencia de performance es
  despreciable.
- Cuando haya volumen alto, se puede refactorizar
  a `ExecuteUpdate`/`ExecuteDelete` (preservando
  las validaciones en una fase previa).

### 17.3 — Validación antes de `SaveChangesAsync` (no después)

**Decisión:** las validaciones de unicidad y
`IsSystem` se hacen **antes** de
`_db.SaveChangesAsync`, no después.

**Justificación:**
- Si la validación falla, no se invoca
  `SaveChangesAsync` (ahorra un round-trip a
  la DB).
- Si pasara la validación pero la DB rechaza
  (race condition), el repo captura
  `DbUpdateException` y la convierte a
  `Result.Failure`.
- La validación previa no es infalible (race
  condition), pero la captura de
  `DbUpdateException` es la red de seguridad.

### 17.4 — `AsNoTracking()` en queries

**Decisión:** `GetAllAsync` usa
`_db.Categories.AsNoTracking()`.

**Justificación:**
- `GetAllAsync` es una query de solo lectura.
  `AsNoTracking` mejora el performance (EF Core
  no trackea cambios).
- `GetByIdAsync` usa `FindAsync` (que sí
  trackea, porque el caller puede querer
  modificar la entidad). En v1 no hay caller que
  modifique después de `GetById`, pero
  mantenemos `FindAsync` por flexibilidad.

### 17.5 — `OrderBy(c => c.Name)` en `GetAllAsync`

**Decisión:** `GetAllAsync` ordena por `Name`.

**Justificación:**
- La UI (Fase 3+) probablemente mostrará las
  categorías y los métodos en un dropdown o
  lista. El orden alfabético es predecible.
- El índice único en `Name` (de F-002) hace que
  esta ordenación sea eficiente.

### 17.6 — Códigos de error canónicos

**Decisión:** los códigos son strings en
PascalCase: `NotFound`, `DuplicateName`,
`SystemCategoryImmutable`,
`SystemPaymentMethodImmutable`.

**Justificación:**
- Coherente con la decisión de F-005 de Phase 1
  (`Result<T>.Error.Code` es `string`).
- Los nombres son auto-documentados.
- La UI (Fase 3+) puede usar un switch sobre
  el código para mostrar mensajes localizados.

### 17.7 — Sin uso de `IUnitOfWork` desde los repos

**Decisión:** los repos usan
`_db.SaveChangesAsync` directamente, no
`_unitOfWork.SaveChangesAsync`.

**Justificación:**
- Las operaciones de los 2 repos son
  single-entity (un Add, Update, o Remove por
  llamada). No hay coordinación entre repos.
- `IUnitOfWork` está disponible para use cases
  que necesiten coordinar varios repos en una
  sola transacción (e.g., "registrar un gasto
  con su categoría nueva"). F-004 no tiene
  ese caso.
- Si en el futuro un use case necesita
  coordinación, llamará a
  `_unitOfWork.SaveChangesAsync` directamente
  (el use case recibe `IUnitOfWork` por DI).

### 17.8 — Sin logging

**Decisión:** F-004 no loguea nada.

**Justificación:**
- F-005 introduce `ILogger<T>` en el composition
  root. F-004 es un eslabón de la cadena; logging
  se hace en F-005.
- Si en el futuro un use case necesita loguear
  ("categoría creada"), lo hace en su capa (no
  en el repo).

---

## 18. Riesgos y mitigaciones

| Riesgo | Probabilidad | Impacto | Mitigación |
| --- | --- | --- | --- |
| Race condition en unicidad (dos requests concurrentes con el mismo `Name`). | Baja | Alto | El repo consulta `AnyAsync` antes de `Add` y captura `DbUpdateException` después de `SaveChangesAsync`. La red de seguridad es el índice único de la DB (de F-002). |
| El developer edita las interfaces (`ICategoryRepository`, `IPaymentMethodRepository`) en lugar de implementar los repos. | Baja | Alto | F-006 ya está cerrado; las interfaces son parte de F-006. F-004 no debe modificarlas. El PR review debe verificar. |
| El developer implementa los 13 repositorios restantes "porque ya estoy aquí". | Media | Medio | F-004 está limitado a 2. El phase planning doc §F-004 lo especifica. AC-7 lo verifica. |
| `dotnet ef database update` falla por FK violation (categoría referenciada por un `Expense`). | Muy baja en F-004 (no hay FKs aún) | Bajo en F-004 | F-004 no captura `DbUpdateException` por FK violation. Se documenta en EC-4; la mejora queda para Fase 3+. |
| El developer usa `IUnitOfWork` en lugar de `_db.SaveChangesAsync`. | Baja | Bajo | No es un bug, pero añade una indirección innecesaria. AC-1 verifica las firmas. |
| R-1 de F-007 falla porque `Domain` se contaminó. | Muy baja (F-004 no toca `Domain`) | Alto | T-005 del tasks file verifica R-1. |
| `dotnet format style` propone cambios inesperados. | Baja | Bajo | Aplicar los cambios. No relajar reglas globales. |
| El developer añade `using static` o `using` redundantes. | Baja | Bajo | El spec §10.2 fija `using` al tope. AC-6 lo verifica. |
| El `Id` que llega a `UpdateAsync` es de un tipo que no existe (e.g., Guid de otra tabla). | Baja | Bajo | `FindAsync` devuelve `null`; el repo devuelve `Result.Failure("NotFound", "...")`. |
| `EF Core 8.0.11` tiene un bug con `FindAsync` o `AsNoTracking` que causa comportamiento inesperado. | Muy baja | Alto | T-005 del tasks file ejecuta los tests; cualquier bug se detecta. |

---

## 19. Referencias

- `docs/roadmap.md` — Fase 2 (Entregables esperados,
  Riesgos principales, Criterios de fase completada).
- `docs/architecture.md` — §3 (solución y proyectos),
  §4 (grafo de dependencias), §8 (capa de
  Infrastructure: persistencia, repositorios).
- `docs/vision.md` — §3 (es-DO como locale por defecto),
  §4.10 (Datos y portabilidad).
- `spec/constitution/tech-stack.md` — paquetes,
  convenciones de código, CPM.
- `spec/constitution/mission.md` — Clean
  Architecture, principios.
- `spec/phases/phase-02/features.md` — F-004 dentro
  del desglose de la fase 2 (6 features totales).
- `spec/features/phase-02/F-001-appdbcontext-addpersistence.done.md`
  — provee `AppDbContext`, `IUnitOfWork`, y
  `AddPersistence` que F-004 extiende. §RF-4 dice
  "**NO** registra los 15 repositorios. Eso es
  F-004."
- `spec/features/phase-02/F-002-entity-configurations-migration.done.md`
  — provee las configuraciones y la migración
  `InitialSchema`. El índice único en `Name`
  protege contra duplicados (mitigación de
  race condition en RN-1).
- `spec/features/phase-02/F-003-seed-data.done.md`
  — sembró las 9 categorías y 5 métodos de pago
  del sistema. F-004 los puede consultar desde el
  primer momento.
- `spec/features/phase-01/F-001-skeleton-solution.done.md`
  — convenciones de `.editorconfig`, `stylecop.json`,
  CPM, y `.gitignore`.
- `spec/features/phase-01/F-006-contrato-interfaces-repo.done.md`
  — define las 15 interfaces, incluyendo
  `ICategoryRepository` e `IPaymentMethodRepository`
  con la firma de 5 métodos que F-004 implementa.
- `spec/features/phase-01/F-007-validacion-arquitectonica.done.md`
  — los tests R-1 a R-5 que F-004 no debe romper.
- `spec/features/phase-01/F-008-composition-root.done.md`
  — el `Bootstrap.ConfigureServices` que F-005
  enchufa.
- `spec/README.md` — convención del directorio
  `spec/`.
- EF Core 8 documentation oficial (a consultar vía
  Context7 cuando se implemente).
