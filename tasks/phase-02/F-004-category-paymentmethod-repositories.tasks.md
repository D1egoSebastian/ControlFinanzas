# Tareas — F-004: Repositorios Concretos de Category y PaymentMethod

> **Spec:** `spec/features/phase-02/F-004-category-paymentmethod-repositories.md`
> **Fuentes de verdad:** la spec, `docs/architecture.md` §3, §4,
> §8, `docs/roadmap.md` §Fase 2, `docs/vision.md` §3
> (es-DO como locale por defecto), `spec/constitution/tech-stack.md`
> (paquetes, CPM), `spec/constitution/mission.md`
> (Clean Architecture),
> `spec/phases/phase-02/features.md` (F-004 dentro del
> desglose de la fase 2),
> `spec/features/phase-02/F-001-appdbcontext-addpersistence.done.md`
> (provee `AppDbContext`, `IUnitOfWork`, y
> `AddPersistence` que F-004 extiende),
> `spec/features/phase-02/F-002-entity-configurations-migration.done.md`
> (provee las configuraciones, la migración
> `InitialSchema`, y el índice único en `Name`),
> `spec/features/phase-02/F-003-seed-data.done.md`
> (sembró las 9 categorías y 5 métodos de pago del
> sistema),
> `spec/features/phase-01/F-006-contrato-interfaces-repo.done.md`
> (define `ICategoryRepository` y
> `IPaymentMethodRepository` con la firma de 5
> métodos que F-004 implementa).
>
> Cada tarea es implementable en menos de 2 horas. El proyecto
> debe seguir compilando al finalizar cada una. La numeración
> (T-001...) es local a esta feature. F-004 crea 2 archivos
> nuevos y modifica 1 existente (`ServiceCollectionExtensions.cs`).
> NO implementa los otros 13 repositorios (queda para Fase 3+).
> NO añade tests de integración (F-006 de Phase 2).

---

## Resumen de tareas

| ID | Tarea | Estimación | Depende de |
| --- | --- | --- | --- |
| T-001 | Crear `CategoryRepository.cs` con los 5 métodos + validaciones de reglas de negocio | M | — |
| T-002 | Crear `PaymentMethodRepository.cs` con los 5 métodos + validaciones de reglas de negocio | M | — |
| T-003 | Modificar `AddPersistence` para registrar los 2 repos como `Scoped` | XS | T-001, T-002 |
| T-004 | Verificación de build, tests existentes, NetArchTest R-1 a R-5, y conformidad arquitectónica | S | T-001 a T-003 |
| T-005 | Format check, actualización de `features.md`, rename de la spec a `.done.md` y build final | S | T-001 a T-004 |

**Total estimado:** menos de 1 jornada. T-001 y T-002
son las más largas (lógica de validaciones + 5
métodos cada una); T-003 es trivial; T-004 y T-005
son verificación y cierre.

---

## T-001 — Crear `CategoryRepository.cs` con los 5 métodos + validaciones de reglas de negocio

**Objetivo**
Implementar la clase concreta `CategoryRepository` que
satisface `ICategoryRepository` (de F-006 de Phase 1)
con los 5 métodos estándar: `AddAsync`, `UpdateAsync`,
`RemoveAsync`, `GetByIdAsync`, `GetAllAsync`. Las
validaciones de reglas de negocio (unicidad de
`Name`, inmutabilidad de `IsSystem`) se implementan
dentro de los métodos, devolviendo `Result.Failure` con
códigos descriptivos.

**Descripción**

### T-001.1 — Verificar la interfaz

1. Abrir
   `src/ControlDeFinanzasIA.Application/Abstractions/ICategoryRepository.cs`
   y confirmar que la firma tiene exactamente los
   5 métodos de la spec §10.1:
   - `Task<Result> AddAsync(Category category, CancellationToken ct = default)`
   - `Task<Result> UpdateAsync(Category category, CancellationToken ct = default)`
   - `Task<Result> RemoveAsync(Guid id, CancellationToken ct = default)`
   - `Task<Category?> GetByIdAsync(Guid id, CancellationToken ct = default)`
   - `Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken ct = default)`
2. Si la firma ha cambiado (no debería; F-006 ya
   está cerrado), restaurar la firma de F-006 antes
   de continuar. F-004 NO modifica la interfaz.

### T-001.2 — Crear el archivo

1. Crear el archivo
   `src/ControlDeFinanzasIA.Infrastructure/Persistence/Repositories/CategoryRepository.cs`.
2. Contenido (descripción de la estructura):
   - File-scoped namespace:
     `namespace ControlDeFinanzasIA.Infrastructure.Persistence.Repositories;`.
   - `using` directives al tope (NUNCA después del
     namespace):
     - `using ControlDeFinanzasIA.Application.Abstractions;`
       (para `ICategoryRepository`).
     - `using ControlDeFinanzasIA.Application.Common;`
       (para `Result`).
     - `using ControlDeFinanzasIA.Domain.Catalog;`
       (para `Category`).
     - `using Microsoft.EntityFrameworkCore;` (para
       `AsNoTracking`, `FindAsync`).
3. Clase:
   - `public sealed class CategoryRepository :
     ICategoryRepository`.
4. Campo privado:
   - `private readonly AppDbContext _db;`.
5. Constructor:
   - `public CategoryRepository(AppDbContext db) => _db = db;`.

### T-001.3 — Implementar `GetByIdAsync` y `GetAllAsync` (queries)

1. `GetByIdAsync(Guid id, CancellationToken ct = default)`:
   - Cuerpo: `return await _db.Categories.FindAsync(new object?[] { id }, ct);`
   - Devuelve `Category?` (null si no existe).
2. `GetAllAsync(CancellationToken ct = default)`:
   - Cuerpo:
     `return await _db.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct);`
   - Devuelve `IReadOnlyList<Category>` ordenado
     alfabéticamente.
3. XML doc en cada método citando la regla de
   negocio y la spec.

### T-001.4 — Implementar `AddAsync` con validación de unicidad

1. `AddAsync(Category category, CancellationToken ct = default)`:
   - Validación defensiva:
     `ArgumentNullException.ThrowIfNull(category);`.
   - Verificar unicidad de `Name`:
     ```text
     if (await _db.Categories.AnyAsync(c => c.Name == category.Name, ct))
     {
         return Result.Failure("DuplicateName", $"Ya existe una categoría con el nombre '{category.Name}'.");
     }
     ```
   - Si no existe, `_db.Categories.Add(category);`
   - `await _db.SaveChangesAsync(ct);`
   - `return Result.Success();`
   - **Capturar `DbUpdateException`** (race condition
     con índice único de la DB): envolver el bloque
     `Add` + `SaveChangesAsync` en `try/catch` que
     captura `DbUpdateException` (y opcionalmente
     `DbUpdateConcurrencyException` por
     consistencia) y devuelve
     `Result.Failure("DuplicateName", "...")`.
2. XML doc en el método citando la regla de
   negocio RN-1 (unicidad) y la captura de
   `DbUpdateException`.

### T-001.5 — Implementar `UpdateAsync` con validaciones de existencia e inmutabilidad

1. `UpdateAsync(Category category, CancellationToken ct = default)`:
   - Validación defensiva:
     `ArgumentNullException.ThrowIfNull(category);`.
   - Buscar la entidad:
     ```text
     var existing = await _db.Categories.FindAsync(new object?[] { category.Id }, ct);
     if (existing is null)
     {
         return Result.Failure("NotFound", $"Categoría {category.Id} no encontrada.");
     }
     ```
   - Validar `IsSystem`:
     ```text
     if (existing.IsSystem && existing.Name != category.Name)
     {
         return Result.Failure("SystemCategoryImmutable", "No se puede renombrar una categoría del sistema.");
     }
     ```
   - Si todo OK:
     `_db.Entry(existing).CurrentValues.SetValues(category);`
     `await _db.SaveChangesAsync(ct);`
     `return Result.Success();`
2. XML doc citando la regla de negocio RN-2
   (inmutabilidad) y RN-3 (NotFound).

### T-001.6 — Implementar `RemoveAsync` con validaciones de existencia e inmutabilidad

1. `RemoveAsync(Guid id, CancellationToken ct = default)`:
   - Buscar la entidad:
     ```text
     var existing = await _db.Categories.FindAsync(new object?[] { id }, ct);
     if (existing is null)
     {
         return Result.Failure("NotFound", $"Categoría {id} no encontrada.");
     }
     ```
   - Validar `IsSystem`:
     ```text
     if (existing.IsSystem)
     {
         return Result.Failure("SystemCategoryImmutable", "No se puede eliminar una categoría del sistema.");
     }
     ```
   - Si todo OK:
     `_db.Categories.Remove(existing);`
     `await _db.SaveChangesAsync(ct);`
     `return Result.Success();`
2. XML doc citando la regla de negocio RN-2 y
   RN-3.

### T-001.7 — Verificar compilación

1. Ejecutar
   `dotnet build "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --nologo`
   desde la raíz.
2. Confirmar 0 warnings, 0 errors.
3. Si hay errores, revisar:
   - Firma de los métodos (debe coincidir
     carácter a carácter con la interfaz).
   - Tipos de retorno (`Task<Result>` para
     commands, `Task<T?>` para GetById,
     `Task<IReadOnlyList<T>>` para GetAll).
   - Que `using ControlDeFinanzasIA.Application.Common;`
     está presente (para `Result`).

**Dependencias**
- F-001 de Fase 2 (provee `AppDbContext` con
  `Categories` DbSet).
- F-002 de Fase 2 (la configuración de `Category`
  con el índice único en `Name` y collation
  `NOCASE`).
- F-006 de Phase 1 (define `ICategoryRepository`
  con la firma de 5 métodos).
- F-001 a F-008 de Phase 1 (la arquitectura en
  general).

**Archivos / proyectos afectados**
- `src/ControlDeFinanzasIA.Infrastructure/Persistence/Repositories/CategoryRepository.cs`
  (nuevo).

**Criterios de finalización**
- El archivo existe y compila.
- `public sealed class CategoryRepository :
  ICategoryRepository`.
- Constructor que recibe `AppDbContext`.
- Los 5 métodos públicos con las firmas exactas
  de `ICategoryRepository` (ver T-001.1).
- `AddAsync` valida unicidad de `Name` (con
  `AnyAsync` + captura de `DbUpdateException`).
- `UpdateAsync` valida existencia (NotFound) y
  `IsSystem` (SystemCategoryImmutable).
- `RemoveAsync` valida existencia (NotFound) y
  `IsSystem` (SystemCategoryImmutable).
- `GetByIdAsync` usa `FindAsync`.
- `GetAllAsync` usa `AsNoTracking().OrderBy(...).ToListAsync()`.
- File-scoped namespace.
- `using` al tope.
- XML doc en cada clase y método público.
- `dotnet build` desde la raíz: 0 warnings, 0
  errors.

---

## T-002 — Crear `PaymentMethodRepository.cs` con los 5 métodos + validaciones de reglas de negocio

**Objetivo**
Implementar la clase concreta `PaymentMethodRepository`
que satisface `IPaymentMethodRepository` (de F-006 de
Phase 1) con la misma estructura que
`CategoryRepository` (de T-001), pero sobre
`PaymentMethod`. Los códigos de error son
`SystemPaymentMethodImmutable` (en vez de
`SystemCategoryImmutable`).

**Descripción**

### T-002.1 — Verificar la interfaz

1. Abrir
   `src/ControlDeFinanzasIA.Application/Abstractions/IPaymentMethodRepository.cs`
   y confirmar que la firma tiene exactamente los
   5 métodos de la spec §10.2 (mismo patrón que
   `ICategoryRepository` pero con `PaymentMethod`).
2. Si la firma ha cambiado, restaurar la de F-006.

### T-002.2 — Crear el archivo

1. Crear el archivo
   `src/ControlDeFinanzasIA.Infrastructure/Persistence/Repositories/PaymentMethodRepository.cs`.
2. Contenido (descripción de la estructura):
   - File-scoped namespace:
     `namespace ControlDeFinanzasIA.Infrastructure.Persistence.Repositories;`.
   - `using` directives al tope (iguales a
     `CategoryRepository` pero con
     `IPaymentMethodRepository`):
     - `using ControlDeFinanzasIA.Application.Abstractions;`
       (para `IPaymentMethodRepository`).
     - `using ControlDeFinanzasIA.Application.Common;`
       (para `Result`).
     - `using ControlDeFinanzasIA.Domain.Catalog;`
       (para `PaymentMethod`).
     - `using Microsoft.EntityFrameworkCore;`.
3. Clase:
   - `public sealed class PaymentMethodRepository :
     IPaymentMethodRepository`.
4. Campo privado y constructor: idénticos a
   `CategoryRepository`.

### T-002.3 — Implementar los 5 métodos

1. **`GetByIdAsync(Guid id, CancellationToken ct = default)`**:
   - Cuerpo: `return await _db.PaymentMethods.FindAsync(new object?[] { id }, ct);`.
2. **`GetAllAsync(CancellationToken ct = default)`**:
   - Cuerpo: `return await _db.PaymentMethods.AsNoTracking().OrderBy(p => p.Name).ToListAsync(ct);`.
3. **`AddAsync(PaymentMethod paymentMethod, CancellationToken ct = default)`**:
   - Validar unicidad con
     `AnyAsync(p => p.Name == paymentMethod.Name, ct)`.
   - Si existe, devolver
     `Result.Failure("DuplicateName", $"Ya existe un método de pago con el nombre '{paymentMethod.Name}'.")`.
   - Si no, `Add` + `SaveChangesAsync` con captura
     de `DbUpdateException` →
     `Result.Failure("DuplicateName", "...")`.
4. **`UpdateAsync(PaymentMethod paymentMethod, CancellationToken ct = default)`**:
   - Buscar con `FindAsync`. Si null,
     `Result.Failure("NotFound", "...")`.
   - Si `existing.IsSystem && existing.Name != paymentMethod.Name`,
     `Result.Failure("SystemPaymentMethodImmutable", "...")`.
   - Si OK, `SetValues` + `SaveChangesAsync` →
     `Result.Success()`.
5. **`RemoveAsync(Guid id, CancellationToken ct = default)`**:
   - Buscar con `FindAsync`. Si null,
     `Result.Failure("NotFound", "...")`.
   - Si `existing.IsSystem`,
     `Result.Failure("SystemPaymentMethodImmutable", "...")`.
   - Si OK, `Remove` + `SaveChangesAsync` →
     `Result.Success()`.
6. XML doc en cada método citando la regla de
   negocio (RN-1, RN-2, RN-3 de la spec).

### T-002.4 — Verificar compilación

1. Ejecutar
   `dotnet build "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --nologo`
   desde la raíz.
2. Confirmar 0 warnings, 0 errors.
3. Si hay errores, revisar lo mismo que en T-001.7
   (firmas, tipos, usings).

**Dependencias**
- F-001 de Fase 2 (provee `AppDbContext` con
  `PaymentMethods` DbSet).
- F-002 de Fase 2 (la configuración de
  `PaymentMethod` con el índice único en `Name`).
- F-006 de Phase 1 (define
  `IPaymentMethodRepository`).
- T-001 (opcional; los 2 repos se pueden crear en
  paralelo, pero T-003 depende de ambos).

**Archivos / proyectos afectados**
- `src/ControlDeFinanzasIA.Infrastructure/Persistence/Repositories/PaymentMethodRepository.cs`
  (nuevo).

**Criterios de finalización**
- El archivo existe y compila.
- `public sealed class PaymentMethodRepository :
  IPaymentMethodRepository`.
- Constructor que recibe `AppDbContext`.
- Los 5 métodos con las firmas exactas de
  `IPaymentMethodRepository`.
- Mismas validaciones que `CategoryRepository` (con
  códigos de error ajustados:
  `SystemPaymentMethodImmutable` en vez de
  `SystemCategoryImmutable`).
- File-scoped namespace.
- `using` al tope.
- XML doc en cada clase y método público.
- `dotnet build` desde la raíz: 0 warnings, 0
  errors.

---

## T-003 — Modificar `AddPersistence` para registrar los 2 repos como `Scoped`

**Objetivo**
Extender el extension method `AddPersistence` (de
F-001) para registrar los 2 nuevos repos como
`Scoped`. F-001 dejó explícitamente esta tarea para
F-004 en §RF-4 y §17.4. Borrar la línea
`// Repositories registered in F-004 of Phase 2.`
que F-001 dejó como placeholder.

**Descripción**

1. Abrir
   `src/ControlDeFinanzasIA.Infrastructure/Persistence/ServiceCollectionExtensions.cs`
   (de F-001).
2. Localizar el método `AddPersistence` (de F-001).
3. Localizar la línea:
   ```text
   // Repositories registered in F-004 of Phase 2.
   ```
4. **Borrar** esa línea.
5. En su lugar, añadir:
   ```text
   services.AddScoped<ICategoryRepository, CategoryRepository>();
   services.AddScoped<IPaymentMethodRepository, PaymentMethodRepository>();
   ```
6. Verificar el orden en el método
   `AddPersistence`:
   - Primero `AppDbContext` (de F-001).
   - Luego `IUnitOfWork` (de F-001).
   - Luego los 2 repositorios (de F-004).
   - El método devuelve `services` al final
     (para chaining).
7. Añadir `using` al tope si no están:
   - `using ControlDeFinanzasIA.Application.Abstractions;`
     (para `ICategoryRepository` y
     `IPaymentMethodRepository`).
   - (probablemente ya está desde F-001, que
     registra `IPathResolver` y `IUnitOfWork`).
8. Actualizar el XML doc del método para reflejar
   que ahora también registra los 2 repos (citando
   la spec F-004).

**Dependencias**
- T-001 (`CategoryRepository` existe; de lo
  contrario, el registro no compila).
- T-002 (`PaymentMethodRepository` existe).
- F-001 de Fase 2 (el método `AddPersistence` base
  que F-004 extiende).

**Archivos / proyectos afectados**
- `src/ControlDeFinanzasIA.Infrastructure/Persistence/ServiceCollectionExtensions.cs`
  (modificado: registra los 2 repos; borrada la
  línea de placeholder de F-001).

**Criterios de finalización**
- `AddPersistence` registra `ICategoryRepository →
  CategoryRepository` como `Scoped`.
- `AddPersistence` registra
  `IPaymentMethodRepository → PaymentMethodRepository`
  como `Scoped`.
- El orden es: `AppDbContext` → `IUnitOfWork` → 2
  repositorios.
- La línea de placeholder
  `// Repositories registered in F-004 of Phase 2.`
  está borrada.
- XML doc del método actualizado.
- `dotnet build` desde la raíz: 0 warnings, 0
  errors.

---

## T-004 — Verificación de build, tests existentes, NetArchTest R-1 a R-5, y conformidad arquitectónica

**Objetivo**
Ejecutar la lista de comprobaciones que validan que
F-004 no rompió nada: build limpio, 111 tests
verdes, los 11 tests de NetArchTest de F-007 (R-1
a R-5) siguen pasando, y la arquitectura sigue
intacta.

**Descripción**

### T-004.1 — Build limpio

1. Ejecutar
   `dotnet build "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --nologo`
   desde la raíz.
2. Confirmar 0 warnings, 0 errors.
3. Si hay warnings o errors, diagnosticar y
   arreglar antes de continuar (probablemente un
   `using` mal ubicado o una firma que no coincide
   con la interfaz).

### T-004.2 — Tests existentes verdes

1. Ejecutar
   `dotnet test "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --nologo --no-build`
   desde la raíz.
2. Confirmar:
   - `Tests.Unit`: 111 passed, 0 failed.
   - `Tests.Integration`: 1 skipped (placeholder
     `F001PlaceholderTests`), 0 failed.
   - Total failures: 0.

### T-004.3 — NetArchTest R-1 a R-5

1. Ejecutar
   `dotnet test --filter "FullyQualifiedName~R"`.
   Confirmar que los 11 tests de NetArchTest
   pasan.
2. Si algún test R-N falla, investigar:
   - **R-1 falla:** `Domain` importó EF Core por
     error (no debería; F-004 no toca `Domain`).
   - **R-2 falla:** `Application` importó EF Core
     por error. F-004 implementa interfaces de
     `Application` pero no añade EF Core a
     `Application.csproj`.
   - **R-3 falla:** porque `Infrastructure` ahora
     referencia `Domain` y `Application` (ya lo
     hacía). Si falla, verificar que las firmas
     de los repos no rompen el parseo de `.csproj`
     (que es como R-3 verifica dependencias
     positivas).
   - **R-4 falla:** porque se añadió la
     dependencia `UI → Infrastructure` por error
     (no debería; F-004 NO la añade). Verificar
     `dotnet list src/ControlDeFinanzasIA.UI reference`.
   - **R-5 falla:** `src/` importó `Tests.*` por
     error.

### T-004.4 — Conformidad arquitectónica

1. Ejecutar
   `dotnet list src/ControlDeFinanzasIA.Domain package`.
   Debe estar **vacío**.
2. Ejecutar
   `dotnet list src/ControlDeFinanzasIA.Application package`.
   Debe seguir mostrando **solo `FluentValidation`**.
3. Ejecutar
   `dotnet list src/ControlDeFinanzasIA.UI reference`.
   Debe seguir mostrando solo `Application` y
   `Domain` (NO `Infrastructure`).

### T-004.5 — Verificación funcional (opcional, recomendada)

> **Nota:** este paso es opcional porque requiere
> ejecutar la app o crear una DB temporal. Si el
> developer no puede, se documenta en el commit
> / PR.

1. Si se tiene una DB de prueba, ejecutar
   `dotnet ef database update --project src/ControlDeFinanzasIA.Infrastructure --startup-project src/ControlDeFinanzasIA.Infrastructure`
   para aplicar las 2 migraciones.
2. Resolver `ICategoryRepository` y
   `IPaymentMethodRepository` desde el container
   (e.g., con un test manual o un programa
   pequeño).
3. Llamar a `GetAllAsync` y verificar que
   devuelve 9 categorías y 5 métodos (los del
   seed).
4. Llamar a `AddAsync` con una categoría nueva.
   Verificar que persiste. Llamar de nuevo con
   el mismo `Name`. Verificar que devuelve
   `Result.Failure("DuplicateName", "...")`.
5. Llamar a `UpdateAsync` sobre una categoría del
   sistema cambiando su `Name`. Verificar que
   devuelve `Result.Failure("SystemCategoryImmutable", "...")`.
6. Llamar a `RemoveAsync` sobre una categoría del
   sistema. Verificar que devuelve
   `Result.Failure("SystemCategoryImmutable", "...")`.
7. **No** es necesario implementar este paso
   para cerrar F-004; la verificación estática
   (T-004.1 a T-004.4) es suficiente para los
   criterios de aceptación. F-006 de Phase 2
   hará tests automatizados equivalentes.

**Dependencias**
- T-001 a T-003 (todas las anteriores).

**Archivos / proyectos afectados**
- Ninguno nuevo. Esta tarea es solo de
  verificación.

**Criterios de finalización**
- Build limpio: 0 warnings, 0 errors (Debug y
  Release).
- Tests.Unit: 111/111 passed.
- Tests.Integration: 1 skipped, 0 failed.
- 11 tests de NetArchTest: 11/11 passed.
- `Domain` no tiene paquetes NuGet.
- `Application` solo tiene `FluentValidation`.
- `UI` no tiene `<ProjectReference>` a
  `Infrastructure`.
- (Opcional) Verificación funcional con
  `dotnet ef database update` y resolución
  manual de los repos.

---

## T-005 — Format check, actualización de `features.md`, rename de la spec a `.done.md` y build final

**Objetivo**
Ejecutar la lista de comprobaciones finales de la
spec (§15 AC-6 y AC-8), validar convenciones de
formato, dejar el repo en estado de "F-004
terminada" y marcar la feature como completada en
el desglose de fase.

**Descripción**

### T-005.1 — Format check

1. Ejecutar
   `dotnet format style --verify-no-changes` desde
   la raíz.
2. Confirmar exit 0. Si propone cambios:
   - Aplicarlos con `dotnet format style` y volver
     a verificar.
   - **NO** relajar reglas globales en
     `.editorconfig` o `stylecop.json`.
3. Los archivos nuevos (los 2 repos) deben
   cumplir con las convenciones del repo;
   `dotnet format` los normaliza.

### T-005.2 — Cierre de la feature

1. **Actualizar el desglose de fase:** en
   `spec/phases/phase-02/features.md`:
   - Cambiar la fila 004 de la tabla resumen
     de "Pendiente" a "✅ COMPLETADA".
   - Cambiar el header de la sección F-004 de
     `## F-004 — Implementación de \`CategoryRepository\` y \`PaymentMethodRepository\``
     a
     `## F-004 — Implementación de \`CategoryRepository\` y \`PaymentMethodRepository\` — ✅ COMPLETADA`.
2. **Actualizar el campo `> **Estado:**`** en el
   header de la spec de `Pendiente de implementación`
   a `✅ COMPLETADA` (preventivamente, aprendiendo
   del sdd-verify de F-001).
3. **Renombrar la spec:** mover
   `spec/features/phase-02/F-004-category-paymentmethod-repositories.md`
   a
   `spec/features/phase-02/F-004-category-paymentmethod-repositories.done.md`.
4. **Build final** después del rename:
   `dotnet build -c Release` debe seguir 0/0.
5. **Tests finales**:
   `dotnet test -c Release --no-build` debe seguir
   111/1 (passed/skipped).
6. **Commit atómico** (cuando el developer
   commitee): los 2 archivos de repositorio + la
   modificación de `ServiceCollectionExtensions.cs`
   + el rename de la spec + el `features.md`
   actualizado van en un solo commit, idealmente
   con un mensaje como
   `feat(persistence): implement Category and PaymentMethod repositories (F-004)`.

### Resumen de verificación de AC

| AC | Criterio | Tarea que lo verifica |
| --- | --- | --- |
| AC-1 | `CategoryRepository` creado | T-001 |
| AC-2 | `PaymentMethodRepository` creado | T-002 |
| AC-3 | Validaciones de reglas de negocio | T-001.4 a T-001.6, T-002.3 |
| AC-4 | `AddPersistence` registra los 2 repos | T-003 |
| AC-5 | Build limpio, 111 tests, 11 NetArchTest | T-004.1, T-004.2, T-004.3 |
| AC-6 | Convenciones del repo | T-005.1 |
| AC-7 | Sin scope creep | T-004.4 |
| AC-8 | Documentación y trazabilidad | T-005.2 |

**Dependencias**
- T-001 a T-004 (todas las anteriores).

**Archivos / proyectos afectados**
- `spec/phases/phase-02/features.md` (modificado:
  F-004 marcada como completada).
- `spec/features/phase-02/F-004-category-paymentmethod-repositories.md`
  (renombrado a `.done.md`).

**Criterios de finalización**
- `dotnet format style --verify-no-changes`: exit 0.
- `dotnet build -c Release`: 0 warnings, 0 errors.
- `dotnet test -c Release --no-build`: 111 passed,
  1 skipped, 0 failures.
- Los 2 archivos de repositorio siguen las
  convenciones del repo.
- `spec/phases/phase-02/features.md` actualizado
  con F-004 marcada como completada (tabla
  resumen y header de sección).
- Spec renombrada a `.done.md`.

---

## Orden recomendado y trabajo en paralelo

**Orden secuencial estricto:** T-001 → (T-002 en paralelo
posible) → T-003 → T-004 → T-005.

- **T-001 debe ir primero** porque T-003 referencia
  `CategoryRepository` (si no existe, no compila).
- **T-002 puede ir en paralelo con T-001** (archivos
  distintos, no comparten código). Pero T-003
  depende de ambos.
- **T-003 depende de T-001 y T-002.**
- **T-004 es verificación** de T-001 a T-003.
- **T-005 es cierre** de toda la feature.

## Trabajo en paralelo posible

Con 2 personas:
- Persona A: T-001.
- Persona B: T-002 (paralela a T-001).
- Luego ambos: T-003 → T-004 → T-005.

Con 1 persona (el caso más probable, porque la
feature es pequeña): orden secuencial estricto.

## Riesgos de la fase de tareas

| Riesgo | Probabilidad | Impacto | Mitigación |
| --- | --- | --- | --- |
| El developer implementa los 13 repositorios restantes "porque ya estoy aquí". | Media | Medio | El phase planning doc §F-004 lo especifica: 2 repositorios solamente. AC-7 lo verifica. |
| El developer modifica la interfaz `ICategoryRepository` o `IPaymentMethodRepository` en lugar de implementar el repo. | Baja | Alto | F-006 está cerrado. T-001.1 verifica la firma antes de implementar. |
| Race condition en unicidad de `Name` (dos requests concurrentes con el mismo `Name`). | Baja | Alto | T-001.4 implementa la consulta `AnyAsync` Y captura `DbUpdateException` para la race condition. RN-1 de la spec lo documenta. |
| El developer usa `_unitOfWork.SaveChangesAsync` en lugar de `_db.SaveChangesAsync`. | Baja | Bajo | No es un bug; es un estilo. La spec §17.7 documenta que no se usa `IUnitOfWork` explícitamente. |
| El developer añade un campo `using static` que `dotnet format` no resuelve. | Baja | Bajo | T-005.1 ejecuta `dotnet format`. |
| R-1 de F-007 falla porque `Domain` se contaminó con EF Core. | Muy baja (F-004 no toca `Domain`) | Alto | T-004.3 verifica R-1. |
| El developer implementa `GetAllAsync` con paginación "porque es buena práctica". | Baja | Bajo | La spec §4.2 lo prohíbe explícitamente para F-004. La paginación se añadirá en una feature futura si la lista crece. |
| El developer añade validación de FK en `RemoveAsync` (categoría con gastos referenciándola). | Baja | Bajo | F-004 documenta en EC-4 que la captura de `DbUpdateException` por FK violation queda para Fase 3+. No es scope creep; es una mejora. |
| `dotnet ef database update` falla por FK violation en `RemoveAsync`. | Nula (F-004 no captura esta excepción) | Bajo en F-004 | F-004 NO captura esta excepción. La mejora queda para cuando las FKs existan (Fase 3+). |

## Referencias

- `spec/features/phase-02/F-004-category-paymentmethod-repositories.md`
  — la spec completa de F-004. Este tasks file es su
  descomposición técnica.
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
  — provee las configuraciones, la migración
  `InitialSchema`, y el índice único en `Name`
  (mitigación de race condition en RN-1).
- `spec/features/phase-02/F-003-seed-data.done.md`
  — sembró las 9 categorías y 5 métodos de pago del
  sistema. F-004 los puede consultar desde el
  primer momento.
- `spec/features/phase-01/F-001-skeleton-solution.done.md`
  — convenciones de `.editorconfig`, `stylecop.json`,
  CPM, y `.gitignore`.
- `spec/features/phase-01/F-006-contrato-interfaces-repo.done.md`
  — define `ICategoryRepository` y
  `IPaymentMethodRepository` con la firma de 5
  métodos que F-004 implementa.
- `spec/features/phase-01/F-007-validacion-arquitectonica.done.md`
  — los tests R-1 a R-5 que F-004 no debe romper.
- `spec/features/phase-01/F-008-composition-root.done.md`
  — el `Bootstrap.ConfigureServices` que F-005
  enchufa.
- `spec/README.md` — convención del directorio
  `spec/`.
- EF Core 8 documentation oficial (a consultar vía
  Context7 cuando se implemente).
