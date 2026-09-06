# Specification — F-006: Tests de Integración con SQLite In-Memory

> **Fase:** 2 — Persistencia con EF Core + SQLite
> **Feature:** F-006 (local a esta fase)
> **Estado:** ✅ COMPLETADA
> **Spec ID:** SPEC-F006P2
>
> **Nota de numeración:** este `F-006` es el de la **Fase 2**
> (`spec/phases/phase-02/features.md`). No debe confundirse con
> el `F-006` de Fase 1 (Contratos de interfaces de repositorio
> en `spec/phases/phase-01/features.md`). Los IDs son locales
> a cada fase; el folder los desambigua. Ver `spec/README.md`
> para la convención.
>
> Fuentes de verdad: `docs/vision.md`, `docs/architecture.md`,
> `docs/roadmap.md` (Fase 2), `spec/constitution/tech-stack.md`,
> `spec/constitution/mission.md`,
> `spec/phases/phase-02/features.md` (F-006 dentro del
> desglose de la fase 2),
> `spec/features/phase-02/F-001-appdbcontext-addpersistence.done.md`
> (provee `AppDbContext` y `AddPersistence` que F-006
> testea),
> `spec/features/phase-02/F-002-entity-configurations-migration.done.md`
> (las migraciones que F-006 aplica en memoria),
> `spec/features/phase-02/F-003-seed-data.done.md` (el
> seed con IDs deterministas que F-006 verifica),
> `spec/features/phase-02/F-004-category-paymentmethod-repositories.done.md`
> (los repos que F-006 testea con SQLite in-memory),
> `spec/features/phase-01/F-007-validacion-arquitectonica.done.md`
> (los 11 tests de NetArchTest; F-006 no debe
> romperlos).

---

## 1. Resumen

F-006 añade **tests de integración** con **SQLite
in-memory** para validar la capa de persistencia
de Phase 2 completa. Crea un `TestAppDbContextFactory`
que abre una `SqliteConnection` en memoria y la
comparte entre los `AppDbContext` del test. Cubre:
- Las 2 migraciones se aplican correctamente.
- El seed inserta las 9 categorías y los 5
  métodos de pago con sus IDs deterministas.
- El seed es idempotente.
- Los 2 repositorios concretos (`CategoryRepository`
  y `PaymentMethodRepository`) cumplen las 5
  firmas y las validaciones de reglas de negocio.
- El placeholder `F001PlaceholderTests` se elimina.

Al final de la feature:
- `Tests.Integration` tiene 4 clases de test
  (factory + 3 test classes con ~30 tests).
- `Tests.Unit` sigue con 111 tests pasando.
- Los 11 tests de NetArchTest de F-007 siguen
  pasando.
- F-006 cierra la Fase 2 (última feature
  pendiente).

---

## 2. Contexto

F-005 dejó la app funcionando end-to-end: arranca,
aplica las 2 migraciones, siembra los 14 registros
del sistema, y la ventana aparece. Sin tests
automatizados, **cada cambio futuro en la capa de
persistencia requiere verificación manual**
(arrancar la app, abrir `data.db`, contar
registros, ejecutar CRUD, etc.).

F-006 añade tests automatizados con SQLite
in-memory, que son:
- **Rápidos** (~ms por test, sin I/O de disco).
- **Deterministas** (la DB se crea y se destruye
  en cada test).
- **Aislados** (cada test tiene su propia
  `SqliteConnection` y su propio `AppDbContext`).
- **Cercanos al production** (usan el mismo
  `AppDbContext` y las mismas migraciones que
  producción).

**Por qué SQLite in-memory y no en memoria
"real"** (e.g., `InMemoryDatabase`):

EF Core tiene un provider `InMemoryDatabase` que
es 100% en memoria. **NO se usa** porque:
- No soporta migraciones EF Core (las 2
  migraciones de F-002 y F-003 no se aplican).
- No soporta muchas features de SQLite (e.g.,
  collation `NOCASE`).
- No refleja el comportamiento real de la DB.

SQLite in-memory (`DataSource=:memory:` + conexión
compartida) **sí** soporta migraciones y todas
las features de SQLite. Es el patrón estándar de
testing con EF Core + SQLite.

**Por qué F-006 es la última feature de Phase 2:**

Después de F-006, la Fase 2 está completa. Las
siguientes fases (Fase 3+) usan la app como base
y añaden use cases, navegación, multi-moneda, etc.
Los tests de F-006 garantizan que la base
(persistencia) sigue funcionando mientras se
construyen features encima.

---

## 3. Objetivo

1. **Crear `TestAppDbContextFactory`** en
   `Tests.Integration/Persistence/` que abre una
   `SqliteConnection` en memoria y la comparte
   entre los `AppDbContext` del test.
2. **Crear `AppDbContextTests`** que valida que
   las 2 migraciones se aplican correctamente y que
   el seed funciona.
3. **Crear `CategoryRepositoryTests`** que valida
   los 5 métodos de `CategoryRepository` y las
   validaciones de reglas de negocio.
4. **Crear `PaymentMethodRepositoryTests`** que
   valida los 5 métodos de `PaymentMethodRepository`
   y las validaciones de reglas de negocio.
5. **Eliminar `F001PlaceholderTests.cs`** (ya no
   es un placeholder; F-006 lo reemplaza con tests
   reales).
6. **NO** añadir tests de UI (eso es scope de
   Fase 3+ con la navegación).

---

## 4. Alcance

### 4.1 En alcance

- Creación de
  `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/TestAppDbContextFactory.cs`.
- Creación de
  `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/AppDbContextTests.cs`.
- Creación de
  `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/CategoryRepositoryTests.cs`.
- Creación de
  `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/PaymentMethodRepositoryTests.cs`.
- Eliminación de
  `tests/ControlDeFinanzasIA.Tests.Integration/F001PlaceholderTests.cs`.
- Verificación de que el build y los 111 tests
  existentes (de `Tests.Unit`) siguen pasando.
- Verificación de que los 11 tests de NetArchTest
  de F-007 siguen pasando.

### 4.2 Fuera de alcance

- **Tests de UI / ViewModels / navegación.** Es
  scope de Fase 3+ (cuando existan esos
  componentes).
- **Tests de las 13 interfaces de repositorio que
  no se implementan en Phase 2** (Expense, Income,
  Debt, etc.). F-006 solo testea las 2 que F-004
  implementó.
- **Tests de auto-backup o de migración
  destructiva.** F-019.
- **Tests de logging.** F-012+ introduce
  `ILogger<T>`; F-006 no testea logs.
- **Tests de los use cases** (futuros, Fase 3+).
- **Tests de navegación** (futuros).
- **Modificaciones a los repositorios** (F-004) o
  al `AppDbContext` (F-001). F-006 testea lo que
  ya existe; no modifica la lógica de aplicación.
- **Tests de las migraciones** (más allá de
  verificar que se aplican). Las migraciones
  mismas son de F-002 y F-003.
- **Tests con BD de archivo real.** F-006 usa
  SQLite in-memory exclusivamente. Tests con
  archivo real (para validar concurrencia, locks,
  etc.) son scope de F-019.

---

## 5. Casos de uso

### CU-1 — El test crea un contexto fresco

- **Actor:** `TestAppDbContextFactory` (usado por
  todos los tests de F-006).
- **Trigger:** un test llama a `factory.CreateContext()`.
- **Precondición:** la factoría se creó
  (probablemente en el constructor del test o
  en una fixture xUnit).
- **Postcondición:** se devuelve un `AppDbContext`
  configurado para usar una `SqliteConnection`
  compartida en memoria. La primera vez que el
  test llama a `db.Database.Migrate()` o
  `factory.MigrateAsync()`, las 2 migraciones se
  aplican.

### CU-2 — Test verifica que el schema se crea

- **Actor:** `AppDbContextTests.Migrate_CreatesSchema`.
- **Trigger:** el test crea un contexto con la
  factoría, llama a `MigrateAsync`, y luego
  verifica que las 2 tablas existen.
- **Postcondición:** el test pasa si
  `SELECT name FROM sqlite_master WHERE type='table'`
  incluye `Categories` y `PaymentMethods`.

### CU-3 — Test verifica que el seed se aplica

- **Actor:** `AppDbContextTests.SeedData_InsertsSystemCategories`
  y `SeedData_InsertsSystemPaymentMethods`.
- **Trigger:** el test crea un contexto, llama a
  `MigrateAsync`, y verifica que las filas
  esperadas están presentes.
- **Postcondición:** los tests pasan si hay
  exactamente 9 categorías con `IsSystem=true` y
  5 métodos con `IsSystem=true`.

### CU-4 — Test verifica la idempotencia del seed

- **Actor:**
  `AppDbContextTests.SeedData_IsIdempotent`.
- **Trigger:** el test crea un contexto, llama a
  `MigrateAsync`, luego crea un segundo contexto
  sobre la misma conexión, llama a `MigrateAsync`
  otra vez, y verifica que no hay duplicados.
- **Postcondición:** el test pasa si los conteos
  siguen siendo 9 y 5.

### CU-5 — Test verifica `GetAllAsync` del repo

- **Actor:**
  `CategoryRepositoryTests.GetAllAsync_ReturnsAllCategoriesOrderedByName`.
- **Trigger:** el test crea un contexto, aplica
  migraciones, resuelve `CategoryRepository` (vía
  `new CategoryRepository(context)`), llama a
  `GetAllAsync()`.
- **Postcondición:** el test pasa si devuelve
  exactamente 9 categorías, ordenadas
  alfabéticamente.

### CU-6 — Test verifica `AddAsync` con duplicado

- **Actor:**
  `CategoryRepositoryTests.AddAsync_WithDuplicateName_ReturnsFailureWithDuplicateName`.
- **Trigger:** el test crea una categoría custom
  con `Name = "Comida"`. Llama a `AddAsync`.
- **Postcondición:** el test pasa si devuelve
  `Result.Failure("DuplicateName", "...")`. El
  índice único de la DB (de F-002) detecta el
  conflicto.

### CU-7 — Test verifica `UpdateAsync` en sistema

- **Actor:**
  `CategoryRepositoryTests.UpdateAsync_WithSystemCategoryRenamed_ReturnsFailureWithSystemCategoryImmutable`.
- **Trigger:** el test obtiene "Comida" (del
  sistema), crea una nueva instancia con `Name =
  "Alimentación"`, llama a `UpdateAsync`.
- **Postcondición:** el test pasa si devuelve
  `Result.Failure("SystemCategoryImmutable", "...")`.

### CU-8 — Test verifica `RemoveAsync` en sistema

- **Actor:**
  `CategoryRepositoryTests.RemoveAsync_WithSystemCategory_ReturnsFailureWithSystemCategoryImmutable`.
- **Trigger:** el test obtiene el Id de "Comida"
  (del sistema), llama a `RemoveAsync` con ese Id.
- **Postcondición:** el test pasa si devuelve
  `Result.Failure("SystemCategoryImmutable", "...")`.

### CU-9 — Test verifica race condition en unicidad

- **Actor:**
  `CategoryRepositoryTests.AddAsync_RaceCondition_ReturnsFailureWithDuplicateName`
  (opcional, marcado como `[Skip]` si es difícil
  de reproducir de forma fiable).
- **Trigger:** el test crea 2 categorías con el
  mismo `Name` en tasks concurrentes. Ambas
  llaman a `AddAsync` casi simultáneamente.
- **Postcondición:** solo una de las 2 inserciones
  tiene éxito; la otra devuelve
  `Result.Failure("DuplicateName", "...")` (porque
  el índice único de la DB detecta el conflicto).

---

## 6. Requisitos funcionales

### RF-1 — `TestAppDbContextFactory`

- `public sealed class TestAppDbContextFactory :
  IDisposable` en
  `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/TestAppDbContextFactory.cs`.
- **Patrón estándar de EF Core testing**:
  ```text
  public sealed class TestAppDbContextFactory : IDisposable
  {
      private readonly SqliteConnection _connection;

      public TestAppDbContextFactory()
      {
          _connection = new SqliteConnection("DataSource=:memory:");
          _connection.Open();
      }

      public AppDbContext CreateContext()
      {
          var options = new DbContextOptionsBuilder<AppDbContext>()
              .UseSqlite(_connection)
              .Options;
          return new AppDbContext(options);
      }

      public void Dispose() => _connection.Dispose();
  }
  ```
- `SqliteConnection` con `DataSource=:memory:` +
  `Open()` (la conexión debe estar abierta para que
  múltiples `DbContext` la compartan).
- `CreateContext()` devuelve un `AppDbContext`
  configurado con `UseSqlite(_connection)`.
- `Dispose()` cierra la conexión.

### RF-2 — `AppDbContextTests`

- `public class AppDbContextTests` en
  `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/AppDbContextTests.cs`.
- 4 tests:
  - `Migrate_CreatesSchema`: aplica las 2
    migraciones, verifica que las tablas
    `Categories` y `PaymentMethods` existen.
  - `SeedData_InsertsSystemCategories`: aplica
    migraciones, verifica que hay exactamente 9
    filas en `Categories` con `IsSystem=true`.
  - `SeedData_InsertsSystemPaymentMethods`:
    análogo al anterior para `PaymentMethods`.
  - `SeedData_IsIdempotent`: aplica migraciones 2
    veces, verifica que los conteos siguen siendo
    9 y 5.
- Cada test usa `using (var factory = new
  TestAppDbContextFactory())` para garantizar
  aislamiento.
- `IDisposable` no es necesario (xUnit crea una
  instancia de la clase por test, y el `using`
  interno maneja la disposición).

### RF-3 — `CategoryRepositoryTests`

- `public class CategoryRepositoryTests` en
  `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/CategoryRepositoryTests.cs`.
- ~11 tests (uno por escenario de uso de la
  spec §5):
  - `GetByIdAsync_WithExistingId_ReturnsCategory`.
  - `GetByIdAsync_WithMissingId_ReturnsNull`.
  - `GetByIdAsync_WithEmptyGuid_ReturnsNull`
    (defensivo).
  - `GetAllAsync_ReturnsAllCategoriesOrderedByName`.
  - `AddAsync_WithUniqueName_PersistsCategory`.
  - `AddAsync_WithDuplicateName_ReturnsFailureWithDuplicateName`.
  - `AddAsync_WithDbUpdateException_ReturnsFailureWithDuplicateName`
    (opcional, hard to reproduce reliably).
  - `UpdateAsync_WithExistingId_UpdatesCategory`.
  - `UpdateAsync_WithMissingId_ReturnsFailureWithNotFound`.
  - `UpdateAsync_WithSystemCategoryRenamed_ReturnsFailureWithSystemCategoryImmutable`.
  - `RemoveAsync_WithExistingId_DeletesCategory`.
  - `RemoveAsync_WithMissingId_ReturnsFailureWithNotFound`.
  - `RemoveAsync_WithSystemCategory_ReturnsFailureWithSystemCategoryImmutable`.

### RF-4 — `PaymentMethodRepositoryTests`

- Misma estructura que
  `CategoryRepositoryTests` pero sobre
  `PaymentMethod`. ~11 tests análogos.

### RF-5 — Eliminar `F001PlaceholderTests.cs`

- Borrar
  `tests/ControlDeFinanzasIA.Tests.Integration/F001PlaceholderTests.cs`.
- El archivo ya no es necesario: F-006 lo reemplaza
  con tests reales.

### RF-6 — Compilación y tests

- `dotnet build` desde la raíz: 0 warnings, 0
  errors (Debug y Release).
- `dotnet test`: `Tests.Unit` 111 passing +
  `Tests.Integration` ~30 nuevos (los de F-006),
  0 failures.
- 11 tests de NetArchTest de F-007 siguen pasando.
- `dotnet format style --verify-no-changes`: exit 0.

---

## 7. Requisitos no funcionales

### RNF-1 — Compatibilidad con la arquitectura

- F-006 NO toca `Domain`, `Application`,
  `Infrastructure`, ni `UI`. F-006 solo añade
  archivos en `Tests.Integration/Persistence/`.
- Los 11 tests de NetArchTest pasan sin
  modificaciones.

### RNF-2 — Velocidad

- Los tests de F-006 corren en < 5 segundos en
  total. SQLite in-memory es rápido.
- Cada test individual corre en < 100 ms.

### RNF-3 — Determinismo

- Los tests son deterministas: la DB se crea
  desde cero en cada test (con `TestAppDbContextFactory`
  en el `using`).
- Los IDs del sistema son deterministas
  (11111111-..., 22222222-...) de F-003; los
  tests pueden hacer `FindAsync` con esos IDs.

### RNF-4 — Aislamiento

- Cada test tiene su propia `SqliteConnection`
  y su propio `AppDbContext`. No hay estado
  compartido entre tests.

### RNF-5 — Sin scope creep

- F-006 NO implementa lógica de aplicación
  (solo testea lo que ya existe).
- F-006 NO modifica los repositorios ni
  `AppDbContext`.
- F-006 NO añade tests de UI o navegación.
- F-006 NO añade tests de las 13 interfaces que
  no se implementan en Phase 2.

---

## 8. Reglas de negocio

### RN-1 — Los tests reflejan las reglas de F-004

- Cada regla de negocio de F-004
  (`DuplicateName`, `NotFound`,
  `SystemCategoryImmutable`,
  `SystemPaymentMethodImmutable`) tiene al menos
  un test que verifica el comportamiento.
- Si una regla cambia en una feature futura, el
  test correspondiente debe actualizarse.

### RN-2 — Los tests usan los IDs deterministas del seed

- Los tests pueden usar los IDs
  `11111111-1111-1111-1111-00000000000X` y
  `22222222-2222-2222-2222-00000000000X` para
  referenciar entidades del sistema.
- Esto es más robusto que buscar por `Name`
  (que es case-insensitive y puede tener
  variaciones Unicode).

### RN-3 — Sin dependencias entre tests

- Cada test es independiente. No hay
  `[CollectionFixture]` ni bases de datos
  compartidas entre tests.
- La factoría (`TestAppDbContextFactory`) es
  instanciada en el `using` de cada test.

### RN-4 — Tests de idempotencia

- `SeedData_IsIdempotent` verifica que aplicar
  las migraciones 2 veces no duplica los
  registros. Esto es importante porque en
  producción (F-005) la app puede arrancar
  múltiples veces sin que la DB se corrompa.

---

## 9. Modelo de dominio

F-006 **NO** introduce nuevas entidades. Solo
añade el modelo de **testing**:

```
tests/
└── ControlDeFinanzasIA.Tests.Integration/
    └── Persistence/                                ← NUEVO
        ├── TestAppDbContextFactory.cs              ← F-006
        ├── AppDbContextTests.cs                     ← F-006
        ├── CategoryRepositoryTests.cs               ← F-006
        └── PaymentMethodRepositoryTests.cs          ← F-006

    └── F001PlaceholderTests.cs                    ← F-006 (eliminado)
```

**`TestAppDbContextFactory`:**
- `public sealed class TestAppDbContextFactory :
  IDisposable`.
- Campo privado `SqliteConnection _connection`.
- Constructor que abre la conexión.
- `CreateContext()` que devuelve un `AppDbContext`
  configurado.
- `Dispose()` que cierra la conexión.

**`AppDbContextTests`:**
- 4 `[Fact]` tests que validan schema y seed.

**`CategoryRepositoryTests`:**
- ~13 `[Fact]` tests (uno por escenario).

**`PaymentMethodRepositoryTests`:**
- ~13 `[Fact]` tests análogos.

---

## 10. Configuración técnica

### 10.1 — `TestAppDbContextFactory`

- `SqliteConnection` con
  `new SqliteConnection("DataSource=:memory:")`.
- `Open()` para abrir la conexión (debe estar
  abierta para que múltiples `DbContext` la
  compartan).
- `UseSqlite(_connection)` para que cada
  `AppDbContext` use la conexión compartida.
- `IDisposable` para cerrar la conexión al
  finalizar el test.

### 10.2 — `Microsoft.Data.Sqlite`

- El paquete `Microsoft.Data.Sqlite` ya está
  en `Tests.Integration.csproj` (de F-002). F-006
  NO añade paquetes nuevos.

### 10.3 — Patrón de cada test

- Cada test sigue este patrón:
  ```text
  [Fact]
  public async Task TestName()
  {
      // Arrange
      using var factory = new TestAppDbContextFactory();
      using var context = factory.CreateContext();
      await context.Database.MigrateAsync();

      // Act
      var repo = new CategoryRepository(context);
      var result = await repo.AddAsync(category);

      // Assert
      Assert.True(result.IsSuccess);
  }
  ```
- `using var factory` garantiza la disposición
  de la conexión.
- `using var context` garantiza la disposición
  del `DbContext` (aunque el repo lo libere
  también).

### 10.4 — Convenciones de código

- File-scoped namespace (proyecto rule).
- `using` al tope.
- XML doc en cada clase y método público.
- Nombres en inglés (convención de la comunidad
  .NET).
- `[Fact]` (no `[Theory]`) — un test = un
  escenario.

---

## 11. Validaciones

| # | Validación | Cuándo se aplica | Comportamiento esperado |
| --- | --- | --- | --- |
| V-1 | `TestAppDbContextFactory.CreateContext` devuelve un `AppDbContext` válido. | Al llamar a `CreateContext`. | Sin excepciones; el contexto se construye con la conexión compartida. |
| V-2 | `Migrate_CreatesSchema` verifica que las 2 tablas existen. | Después de `MigrateAsync`. | Las tablas `Categories` y `PaymentMethods` están en `sqlite_master`. |
| V-3 | `SeedData_InsertsSystemCategories` verifica el conteo. | Después de `MigrateAsync`. | Exactamente 9 filas con `IsSystem=true`. |
| V-4 | `AddAsync` con nombre duplicado devuelve `Result.Failure("DuplicateName", "...")`. | Después de intentar insertar. | El test pasa. |
| V-5 | `UpdateAsync` con `IsSystem=true && Name cambia` devuelve `Result.Failure("SystemCategoryImmutable", "...")`. | Después de intentar actualizar. | El test pasa. |
| V-6 | `RemoveAsync` con `IsSystem=true` devuelve `Result.Failure("SystemCategoryImmutable", "...")`. | Después de intentar eliminar. | El test pasa. |
| V-7 | `dotnet test` ejecuta los nuevos tests. | Al ejecutar los tests. | Pasan todos; 0 failures. |

---

## 12. Casos borde

### EC-1 — Race condition en `AddAsync` (difícil de reproducir)

- Dos requests concurrentes con el mismo
  `Name`. Ambos pasan el `AnyAsync` antes de
  insertar. El segundo `SaveChangesAsync` lanza
  `DbUpdateException`.
- **Mitigación:** el test de race condition
  (`AddAsync_RaceCondition_ReturnsFailureWithDuplicateName`)
  es **opcional** y difícil de reproducir de
  forma fiable en SQLite. Si se incluye, marcar
  como `[Skip]` o usar `Parallel.For` con
  parámetros cuidadosos.

### EC-2 — `GetByIdAsync` con `Guid.Empty`

- `id == Guid.Empty` debería devolver `null`
  (defensivo, sin tocar la DB).
- El test `GetByIdAsync_WithEmptyGuid_ReturnsNull`
  lo verifica.

### EC-3 — `MigrateAsync` ya aplicado

- Si el test llama a `MigrateAsync` dos veces
  seguidas, la segunda llamada es un no-op
  (EF Core verifica `__EFMigrationsHistory`).
- Esto es correcto y se verifica en
  `SeedData_IsIdempotent`.

### EC-4 — `AddAsync` con `category` null

- `ArgumentNullException.ThrowIfNull(category)` en
  el repo (de F-004). El test no necesita
  verificarlo (es defensivo), pero podría
  agregarse un test que confirme la excepción.

### EC-5 — Tests que necesitan IDs deterministas

- Los tests que validan `GetByIdAsync` con
  entidades del sistema pueden usar los IDs
  hardcodeados (e.g.,
  `new Guid("11111111-1111-1111-1111-000000000001")`).
- Esto es robusto y no depende del `Name` (que
  es case-insensitive).

---

## 13. Dependencias

### 13.1 Prerrequisitos de implementación

- **F-001 de Fase 2** (completada): provee
  `AppDbContext`.
- **F-002 de Fase 2** (completada): provee las
  configuraciones y las 2 migraciones; añadió
  `Microsoft.Data.Sqlite` a `Tests.Integration.csproj`.
- **F-003 de Fase 2** (completada): provee el seed
  con IDs deterministas.
- **F-004 de Fase 2** (completada): provee los 2
  repositorios que F-006 testea.
- **F-005 de Fase 2** (completada): enchufa
  `AddPersistence` en el composition root y
  aplica migraciones al arrancar.

### 13.2 Dependencias con otras features

| Feature | Relación |
| --- | --- |
| F-001 (Phase 2) | Provee `AppDbContext` que F-006 testea. |
| F-002 (Phase 2) | Provee las migraciones y el paquete `Microsoft.Data.Sqlite` que F-006 usa. |
| F-003 (Phase 2) | Provee el seed con IDs deterministas que F-006 verifica. |
| F-004 (Phase 2) | Provee los 2 repositorios que F-006 testea con SQLite in-memory. |
| F-005 (Phase 2) | Ya enchufó `AddPersistence` y aplica migraciones en producción. F-006 testea el mismo flujo pero en memoria. |

### 13.3 Bloqueos

- **Ninguno.** F-006 puede implementarse ahora
  mismo.

---

## 14. Fuera de alcance (detallado)

- **Tests de UI / ViewModels / navegación.** F-006
  testea solo la capa de persistencia. Los tests
  de UI llegan con las features de Fase 3+ que
  introducen navegación.
- **Tests de las 13 interfaces que F-006 no
  implementa.** Esas llegan cuando se implementen
  los repos correspondientes (Fase 3+).
- **Tests con BD de archivo real.** SQLite in-memory
  es suficiente para v1. Tests con archivo real
  son scope de F-019 (validación de concurrencia,
  locks, etc.).
- **Tests de auto-backup o migración destructiva.**
  F-019.
- **Tests de logging.** F-012+ introduce
  `ILogger<T>`; los tests de logs son scope de
  features futuras.
- **Tests de los use cases** (futuros, Fase 3+).
- **Tests de `IExchangeRateProvider`, `ILLMClient`,
  `IBackupService`, etc.** Esas abstracciones
  llegan con sus features respectivas.
- **Modificaciones a la lógica de aplicación** (F-001
  a F-004). F-006 testea lo que ya existe.

---

## 15. Criterios de aceptación

La feature se considera completada cuando
**todos** los siguientes puntos se cumplen.

### AC-1 — `TestAppDbContextFactory` creado

- [ ] `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/TestAppDbContextFactory.cs`
      existe.
- [ ] `public sealed class TestAppDbContextFactory :
      IDisposable`.
- [ ] Constructor que abre `SqliteConnection` con
      `DataSource=:memory:`.
- [ ] `CreateContext()` devuelve un `AppDbContext`
      configurado con `UseSqlite(_connection)`.
- [ ] `Dispose()` cierra la conexión.

### AC-2 — `AppDbContextTests` creado

- [ ] `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/AppDbContextTests.cs`
      existe.
- [ ] `Migrate_CreatesSchema` pasa.
- [ ] `SeedData_InsertsSystemCategories` pasa
      (9 categorías).
- [ ] `SeedData_InsertsSystemPaymentMethods` pasa
      (5 métodos).
- [ ] `SeedData_IsIdempotent` pasa.

### AC-3 — `CategoryRepositoryTests` creado

- [ ] `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/CategoryRepositoryTests.cs`
      existe.
- [ ] Los 13 tests listados en RF-3 pasan (los
      opcionales pueden marcarse como `[Skip]`).

### AC-4 — `PaymentMethodRepositoryTests` creado

- [ ] `tests/ControlDeFinanzasIA.Tests.Integration/Persistence/PaymentMethodRepositoryTests.cs`
      existe.
- [ ] Los ~13 tests análogos a `CategoryRepositoryTests`
      pasan.

### AC-5 — `F001PlaceholderTests.cs` eliminado

- [ ] El archivo ya no existe.

### AC-6 — Compilación y tests

- [ ] `dotnet build -c Release` desde la raíz: 0
      warnings, 0 errors.
- [ ] `dotnet test` desde la raíz:
      - `Tests.Unit`: 111 passed, 0 failed.
      - `Tests.Integration`: ~30 nuevos passed, 0
        failed, 0 skipped.
- [ ] Los 11 tests de NetArchTest de F-007 pasan
      sin modificaciones.
- [ ] `dotnet format style --verify-no-changes`: exit
      0.

### AC-7 — Sin scope creep

- [ ] NO se ha modificado `Domain`, `Application`,
      `Infrastructure`, ni `UI`.
- [ ] NO se ha modificado ningún repositorio
      (F-004) ni `AppDbContext` (F-001).
- [ ] NO se han añadido tests de UI o navegación.
- [ ] NO se han añadido tests de las 13 interfaces
      que F-006 no implementa.
- [ ] NO se ha modificado el CI workflow.

### AC-8 — Documentación y trazabilidad

- [ ] `spec/phases/phase-02/features.md`
      actualizado: F-006 marcada como ✅
      COMPLETADA en la tabla resumen y en el
      header de la sección.
- [ ] La spec se renombra a
      `spec/features/phase-02/F-006-integration-tests.done.md`.
- [ ] **Fase 2 cerrada:** las 6 features de Fase 2
      están marcadas ✅ COMPLETADA en
      `spec/phases/phase-02/features.md`.

---

## 16. Estructura de archivos esperada

```
tests/
└── ControlDeFinanzasIA.Tests.Integration/
    ├── F001PlaceholderTests.cs                    ← F-006 (eliminado)
    └── Persistence/                                ← NUEVO
        ├── TestAppDbContextFactory.cs              ← F-006
        ├── AppDbContextTests.cs                     ← F-006
        ├── CategoryRepositoryTests.cs               ← F-006
        └── PaymentMethodRepositoryTests.cs          ← F-006
```

**Modificaciones al CI workflow:** ninguna.

---

## 17. Decisiones de diseño explícitas

### 17.1 — `SqliteConnection` con `DataSource=:memory:`

**Decisión:** usar `new SqliteConnection("DataSource=:memory:")`.

**Justificación:**
- SQLite in-memory es el patrón estándar de
  testing con EF Core + SQLite.
- La conexión se mantiene **abierta**
  (`_connection.Open()`) durante toda la vida
  del test, para que múltiples `DbContext` la
  compartan.
- La DB se destruye cuando la conexión se cierra
  (`_connection.Dispose()` al final del test).

**Alternativa descartada:** `InMemoryDatabase`
provider de EF Core. No soporta migraciones
EF Core, ni collation `NOCASE`, ni muchas
features de SQLite.

### 17.2 — `await context.Database.MigrateAsync()` en cada test

**Decisión:** cada test que necesita la DB
sembrada llama a `MigrateAsync()` en el `Arrange`.

**Justificación:**
- Aislamiento: cada test tiene su propia DB
  desde cero.
- Rapidez: `MigrateAsync` con SQLite in-memory
  es ~ms.
- Determinismo: la DB siempre está en el mismo
  estado inicial.

**Alternativa descartada:** compartir una
factoría estática. Reduce la velocidad de
setup pero rompe el aislamiento.

### 17.3 — `using var factory` y `using var context`

**Decisión:** cada test usa `using var factory
= new TestAppDbContextFactory()` y `using var
context = factory.CreateContext()`.

**Justificación:**
- Garantiza la disposición de la conexión y
  del `DbContext` al final del test, incluso si
  una aserción falla.
- Es el patrón estándar de xUnit + IDisposable.

### 17.4 — Tests como `[Fact]` (no `[Theory]`)

**Decisión:** cada escenario de test es un
`[Fact]` separado, no un `[Theory]` con
`[InlineData]`.

**Justificación:**
- Cada escenario tiene un `Arrange` único
  (diferente `Guid`, diferente `Name`, etc.).
  `[Theory]` con `[InlineData]` requeriría
  construir el contexto dentro del parámetro,
  lo cual es más complejo.
- Los nombres de los tests son más
  descriptivos (`AddAsync_WithDuplicateName_ReturnsFailureWithDuplicateName`)
  que un `[Theory]` genérico.

### 17.5 — Tests de `F001PlaceholderTests.cs` reemplazados, no complementados

**Decisión:** el placeholder se **elimina** en
F-006.

**Justificación:**
- F-006 lo reemplaza con tests reales. Mantener
  el placeholder sin sentido es ruido.
- Si en el futuro se añade un test en
  `Tests.Integration` que no encaje en las 4
  clases de F-006, se crea un nuevo archivo.

---

## 18. Riesgos y mitigaciones

| Riesgo | Probabilidad | Impacto | Mitigación |
| --- | --- | --- | --- |
| Race condition en `AddAsync` (difícil de reproducir). | Media | Bajo | EC-1: el test es opcional; si se incluye, marcar como `[Skip]` o usar `Parallel.For` cuidadoso. |
| `SqliteConnection` no se cierra correctamente (memory leak). | Baja | Bajo | T-002.1 documenta el `Dispose()`. xUnit + `using var` garantiza la disposición. |
| Test depende del orden de las filas en `GetAllAsync` (no determinista). | Baja | Medio | `GetAllAsync` está ordenado por `Name`. Los tests usan IDs deterministas o `OrderBy` antes de comparar. |
| `dotnet ef` no se puede ejecutar para regenerar migraciones en CI. | Baja | Bajo | Los tests de F-006 NO ejecutan `dotnet ef`. Solo usan las migraciones commiteadas. |
| `dotnet format style` propone cambios en los nuevos archivos. | Baja | Bajo | T-006.1 (en el tasks file) aplica los cambios. |
| El test R-1 de F-007 falla porque `Tests.Integration` se considera "src/". | Muy baja | Alto | R-5 (F-007) verifica que `src/` no depende de `Tests.*`. `Tests.Integration` NO es parte de `src/`. AC-6 lo verifica. |
| `Microsoft.Data.Sqlite` no está en `Tests.Integration.csproj`. | Baja | Alto | F-002 lo añadió. Si por error se quita, T-001 del tasks file lo verifica. |
| `AppDbContext` cambia (F-001) y los tests de F-006 se rompen. | Baja | Medio | Los tests están acoplados al `AppDbContext` actual. Si cambia, los tests se actualizan en la misma feature. |
| `SeedData` cambia (F-003) y los tests de F-006 se rompen. | Baja | Medio | Los tests están acoplados al seed actual (9 categorías + 5 métodos). Si cambia, los tests se actualizan en la misma feature. |

---

## 19. Referencias

- `docs/roadmap.md` — Fase 2 (Entregables esperados,
  Riesgos principales, Criterios de fase completada).
- `docs/architecture.md` — §3 (solución y proyectos),
  §4 (grafo de dependencias), §8 (capa de
  Infrastructure: persistencia), §15 (testing).
- `docs/vision.md` — §3 (es-DO como locale por defecto).
- `spec/constitution/tech-stack.md` — paquetes,
  convenciones de código, CPM, testing (xUnit +
  FluentAssertions).
- `spec/constitution/mission.md` — Clean
  Architecture, principios, testability.
- `spec/phases/phase-02/features.md` — F-006 dentro
  del desglose de la fase 2 (última feature
  pendiente).
- `spec/features/phase-02/F-001-appdbcontext-addpersistence.done.md`
  — provee `AppDbContext` que F-006 testea.
- `spec/features/phase-02/F-002-entity-configurations-migration.done.md`
  — generó las 2 migraciones; F-002 añadió
  `Microsoft.Data.Sqlite` a `Tests.Integration.csproj`.
- `spec/features/phase-02/F-003-seed-data.done.md` —
  sembró las 14 entidades del sistema con IDs
  deterministas.
- `spec/features/phase-02/F-004-category-paymentmethod-repositories.done.md`
  — implementó los 2 repositorios que F-006
  testea.
- `spec/features/phase-02/F-005-composition-root-persistence.done.md`
  — enchufa `AddPersistence` y aplica migraciones
  en producción. F-006 replica el mismo flujo en
  memoria.
- `spec/features/phase-01/F-007-validacion-arquitectonica.done.md`
  — los 11 tests de NetArchTest; F-006 no debe
  romperlos.
- `spec/README.md` — convención del directorio
  `spec/`.
- EF Core 8 documentation oficial (a consultar vía
  Context7 cuando se implemente).
- `Microsoft.Data.Sqlite` documentation (a consultar
  vía Context7 cuando se implemente).
