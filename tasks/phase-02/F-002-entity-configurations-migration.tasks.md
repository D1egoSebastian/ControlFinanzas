# Tareas — F-002: Configuraciones de Entidades y Migración Inicial

> **Spec:** `spec/features/phase-02/F-002-entity-configurations-migration.md`
> **Fuentes de verdad:** la spec, `docs/architecture.md` §3, §4,
> §8, `docs/roadmap.md` §Fase 2 (Entregables esperados),
> `spec/constitution/tech-stack.md` (paquetes, CPM),
> `spec/constitution/mission.md` (Clean Architecture),
> `spec/phases/phase-02/features.md` (F-002 dentro del
> desglose de la fase 2),
> `spec/features/phase-02/F-001-appdbcontext-addpersistence.done.md`
> (provee `AppDbContext` y los paquetes EF Core 8.0.11 que
> esta feature usa).
>
> Cada tarea es implementable en menos de 2 horas. El proyecto
> debe seguir compilando al finalizar cada una. La numeración
> (T-001...) es local a esta feature. F-002 solo añade
> archivos en `src/ControlDeFinanzasIA.Infrastructure/Persistence/Configurations/`
> y `Migrations/`, y crea `.config/dotnet-tools.json` en la
> raíz del repo. NO modifica `AppDbContext`, `Domain`,
> `Application`, `UI`, `Tests.Unit`, ni `Tests.Integration`.

---

## Resumen de tareas

| ID | Tarea | Estimación | Depende de |
| --- | --- | --- | --- |
| T-001 | Crear `.config/dotnet-tools.json` con `dotnet-ef` 8.0.x e instalar localmente | XS | — |
| T-002 | Crear `CategoryConfiguration.cs` en `Infrastructure/Persistence/Configurations/` | S | — |
| T-003 | Crear `PaymentMethodConfiguration.cs` en `Infrastructure/Persistence/Configurations/` | S | — |
| T-004 | Generar la migración `InitialSchema` con `dotnet ef migrations add` | S | T-001, T-002, T-003 |
| T-005 | Verificación de build, tests existentes, NetArchTest R-1 a R-5, y conformidad arquitectónica | S | T-001 a T-004 |
| T-006 | Format check, actualización de `features.md`, rename de la spec a `.done.md` y build final | S | T-001 a T-005 |

**Total estimado:** menos de 1 jornada. T-002 y T-003 son
las más largas (configuraciones con cuerpo sustancial);
T-001, T-004 y T-006 son cortos. T-005 es verificación.

---

## T-001 — Crear `.config/dotnet-tools.json` con `dotnet-ef` 8.0.x e instalar localmente

**Objetivo**
Introducir la herramienta `dotnet-ef` pinneada a 8.0.x
en el manifest local del repo (`.config/dotnet-tools.json`),
sin instalar globalmente. La herramienta se usa en T-004
para generar la migración `InitialSchema`.

**Descripción**

### T-001.1 — Verificar si existe `.config/dotnet-tools.json`

1. Comprobar si `.config/dotnet-tools.json` existe
   en la raíz del repo.
2. Si no existe, crearlo ejecutando
   `dotnet new tool-manifest` desde la raíz.
3. Si existe, verificar que el contenido sea válido
   (JSON bien formado, con `"version": 1` o superior).

### T-001.2 — Añadir la entrada de `dotnet-ef`

1. Añadir al `.config/dotnet-tools.json` una entrada
   para `dotnet-ef` con la versión alineada a EF Core
   8.0.11 (la misma versión que F-001 añadió a CPM).
   Default de referencia: `8.0.11`. Confirmar vía
   Context7 si hay una release más reciente.
2. El archivo debe quedar con este contenido
   (descripción, no implementación completa):
   ```text
   {
     "version": 1,
     "isRoot": true,
     "tools": {
       "dotnet-ef": {
         "version": "8.0.x",
         "commands": ["dotnet-ef"]
       }
     }
   }
   ```
3. **No** se añade ninguna otra herramienta. Solo
   `dotnet-ef`.

### T-001.3 — Restaurar e instalar la herramienta

1. Ejecutar `dotnet tool restore` desde la raíz del
   repo. Esto lee `.config/dotnet-tools.json` e
   instala la herramienta en la carpeta `.tools/`
   local del repo.
2. Verificar que la instalación es exitosa
   (exit code 0, sin warnings).
3. Ejecutar `dotnet ef --version` desde la raíz.
   Debe mostrar la versión 8.0.x instalada.

### T-001.4 — Verificar que no se instaló globalmente

1. Confirmar que `.config/dotnet-tools.json` está
   commiteable (no contiene rutas absolutas del
   developer, sino solo la versión).
2. Confirmar que el directorio `.tools/` está en
   `.gitignore` (ya lo está por la convención
   estándar de .NET: `.tools/` se ignora
   automáticamente; verificarlo en el `.gitignore`
   de F-001 de Phase 1).
3. Si `.tools/` no está ignorado, añadirlo a
   `.gitignore` (NO debería ser necesario, pero
   verificar).

**Dependencias**
- F-001 de Fase 2 (los paquetes EF Core 8.0.11
  están en CPM; la herramienta `dotnet-ef` los
  usa para generar migraciones).
- .NET 8 SDK instalado localmente (verificar con
  `dotnet --version` antes de empezar; debe ser
  8.0.x).

**Archivos / proyectos afectados**
- `.config/dotnet-tools.json` (nuevo o
  modificado).
- `.tools/` (creado por `dotnet tool restore`;
  ignorado por `.gitignore`).
- `.gitignore` (posiblemente modificado si `.tools/`
  no estaba ignorado; verificar antes de modificar).

**Criterios de finalización**
- `.config/dotnet-tools.json` existe con una entrada
  para `dotnet-ef` 8.0.x.
- `dotnet tool restore` pasa sin errores.
- `dotnet ef --version` muestra la versión 8.0.x.
- La herramienta **no** está instalada globalmente
  (verificación: `which dotnet-ef` o equivalente
  no debe resolver a una ruta fuera del repo).
- El archivo `.config/dotnet-tools.json` está
  commiteable (sin paths absolutos).

---

## T-002 — Crear `CategoryConfiguration.cs` en `Infrastructure/Persistence/Configurations/`

**Objetivo**
Definir el mapeo Fluent API de `Category` a la tabla
`Categories` con todas las reglas: PK, índice único
en `Name` con collation `NOCASE`, longitudes máximas,
columnas requeridas. Se descubre automáticamente vía
`ApplyConfigurationsFromAssembly` (que F-001 ya
configuró en `AppDbContext.OnModelCreating`).

**Descripción**

1. Crear el archivo
   `src/ControlDeFinanzasIA.Infrastructure/Persistence/Configurations/CategoryConfiguration.cs`.
2. Contenido (descripción de la estructura):
   - File-scoped namespace:
     `namespace ControlDeFinanzasIA.Infrastructure.Persistence.Configurations;`.
   - `using` directives al tope (NUNCA después del
     namespace — bug de Fase 0 ya corregido):
     - `using ControlDeFinanzasIA.Domain.Catalog;`
       (para `Category`).
     - `using Microsoft.EntityFrameworkCore;` (para
       `IEntityTypeConfiguration<T>`).
     - `using Microsoft.EntityFrameworkCore.Metadata.Builders;`
       (para `EntityTypeBuilder<T>`).
3. Clase:
   - `public sealed class CategoryConfiguration :
     IEntityTypeConfiguration<Category>`.
4. Método:
   - `public void Configure(EntityTypeBuilder<Category> b)`.
   - Cuerpo (DDL mapping):
     - `b.ToTable("Categories")` (PascalCase, per
       `docs/architecture.md` §8).
     - `b.HasKey(c => c.Id)`.
     - `b.Property(c => c.Id).ValueGeneratedNever()`
       (Guid desde Domain, no desde DB).
     - `b.Property(c => c.Name).IsRequired().HasMaxLength(100).UseCollation("NOCASE")`.
     - `b.HasIndex(c => c.Name).IsUnique()`.
     - `b.Property(c => c.IsSystem).IsRequired()`.
     - `b.Property(c => c.CreatedAt).IsRequired()`.
     - `b.Property(c => c.UpdatedAt).IsRequired()`.
     - `b.Property(c => c.Color).HasMaxLength(9)`
       (hex `#RRGGBB` o `#RRGGBBAA`).
     - `b.Property(c => c.Icon).HasMaxLength(50)`.
5. XML doc en la clase y en el método, citando
   `docs/architecture.md` §8 (mapeo Fluent API).

**Dependencias**
- T-001 (no bloqueante para compilar, pero la
  herramienta `dotnet-ef` se usa en T-004 para
  verificar la migración).
- F-001 de Fase 2 (los paquetes EF Core 8.0.11
  están en CPM; `EntityTypeBuilder` y
  `IEntityTypeConfiguration` resuelven).
- F-002 de Phase 1 (la entidad `Category` existe
  en `src/ControlDeFinanzasIA.Domain/Catalog/`).

**Archivos / proyectos afectados**
- `src/ControlDeFinanzasIA.Infrastructure/Persistence/Configurations/CategoryConfiguration.cs`
  (nuevo).

**Criterios de finalización**
- El archivo existe y compila.
- `public sealed class CategoryConfiguration :
  IEntityTypeConfiguration<Category>`.
- Método `Configure(EntityTypeBuilder<Category> b)`
  con todas las reglas del paso 4.
- File-scoped namespace.
- `using` al tope.
- XML doc en la clase y en el método.
- `dotnet build` desde la raíz: 0 warnings, 0
  errors.
- `AppDbContext` no se ha modificado (la
  configuración se descubre automáticamente vía
  `ApplyConfigurationsFromAssembly` que F-001 ya
  configuró).

---

## T-003 — Crear `PaymentMethodConfiguration.cs` en `Infrastructure/Persistence/Configurations/`

**Objetivo**
Definir el mapeo Fluent API de `PaymentMethod` a la
tabla `PaymentMethods` con las mismas reglas que
`CategoryConfiguration` (excepto `Color`/`Icon`).
Se descubre automáticamente vía
`ApplyConfigurationsFromAssembly`.

**Descripción**

1. Crear el archivo
   `src/ControlDeFinanzasIA.Infrastructure/Persistence/Configurations/PaymentMethodConfiguration.cs`.
2. Contenido (descripción de la estructura):
   - File-scoped namespace:
     `namespace ControlDeFinanzasIA.Infrastructure.Persistence.Configurations;`.
   - `using` directives al tope (iguales a
     `CategoryConfiguration` pero con `PaymentMethod`
     en vez de `Category`):
     - `using ControlDeFinanzasIA.Domain.Catalog;`
       (para `PaymentMethod`).
     - `using Microsoft.EntityFrameworkCore;`.
     - `using Microsoft.EntityFrameworkCore.Metadata.Builders;`.
3. Clase:
   - `public sealed class PaymentMethodConfiguration :
     IEntityTypeConfiguration<PaymentMethod>`.
4. Método:
   - `public void Configure(EntityTypeBuilder<PaymentMethod> b)`.
   - Cuerpo (DDL mapping, sin `Color`/`Icon`):
     - `b.ToTable("PaymentMethods")`.
     - `b.HasKey(p => p.Id)`.
     - `b.Property(p => p.Id).ValueGeneratedNever()`.
     - `b.Property(p => p.Name).IsRequired().HasMaxLength(100).UseCollation("NOCASE")`.
     - `b.HasIndex(p => p.Name).IsUnique()`.
     - `b.Property(p => p.IsSystem).IsRequired()`.
     - `b.Property(p => p.CreatedAt).IsRequired()`.
     - `b.Property(p => p.UpdatedAt).IsRequired()`.
     - **NO** se mapea `Color` ni `Icon` (campos
       exclusivos de `Category`).
5. XML doc en la clase y en el método, citando
   `docs/architecture.md` §8 y la nota de que
   `PaymentMethod` no tiene representación visual
   (por eso no tiene `Color`/`Icon`).

**Dependencias**
- T-001 (no bloqueante para compilar).
- F-001 de Fase 2 (paquetes EF Core).
- F-002 de Phase 1 (la entidad `PaymentMethod`
  existe).

**Archivos / proyectos afectados**
- `src/ControlDeFinanzasIA.Infrastructure/Persistence/Configurations/PaymentMethodConfiguration.cs`
  (nuevo).

**Criterios de finalización**
- El archivo existe y compila.
- `public sealed class PaymentMethodConfiguration :
  IEntityTypeConfiguration<PaymentMethod>`.
- Método `Configure(EntityTypeBuilder<PaymentMethod> b)`
  con todas las reglas del paso 4 (sin `Color`/`Icon`).
- File-scoped namespace.
- `using` al tope.
- XML doc en la clase y en el método.
- `dotnet build` desde la raíz: 0 warnings, 0
  errors.

---

## T-004 — Generar la migración `InitialSchema` con `dotnet ef migrations add`

**Objetivo**
Generar la primera migración EF Core (`InitialSchema`)
que materializa el schema definido por las dos
configuraciones. Los 3 archivos resultantes se
commitean en
`Infrastructure/Persistence/Migrations/`.

**Descripción**

### T-004.1 — Verificar prerrequisitos

1. Confirmar que la herramienta `dotnet-ef` está
   instalada (de T-001):
   `dotnet ef --version` debe mostrar 8.0.x.
2. Confirmar que las 2 configuraciones existen
   (de T-002 y T-003).
3. Confirmar que el `SeedData` (de F-001) es un
   stub vacío (sin `HasData`). Si por error
   alguien rellenó el stub, la migración incluiría
   `InsertData` automáticamente, lo que la spec
   prohíbe.

### T-004.2 — Ejecutar el comando de generación

1. Desde la raíz del repo, ejecutar:
   `dotnet ef migrations add InitialSchema --project src/ControlDeFinanzasIA.Infrastructure --startup-project src/ControlDeFinanzasIA.Infrastructure`.
2. El `--startup-project` es `Infrastructure` mismo
   en F-002 (no `UI`, porque `UI` aún no tiene
   `AddPersistence`; eso es F-005).
3. La herramienta debe completar sin errores.

### T-004.3 — Verificar los 3 archivos generados

1. Confirmar que existen 3 archivos en
   `src/ControlDeFinanzasIA.Infrastructure/Persistence/Migrations/`:
   - `YYYYMMDDhhmmss_InitialSchema.cs` (la
     migración; el timestamp lo asigna la
     herramienta).
   - `YYYYMMDDhhmmss_InitialSchema.Designer.cs`
     (metadata).
   - `AppDbContextModelSnapshot.cs` (snapshot
     del modelo).
2. El `git status` debe mostrar los 3 archivos
   como **untracked** (o como **staged** si el
   implementador los añade con `git add`).

### T-004.4 — Verificar el contenido de la migración

1. Abrir `YYYYMMDDhhmmss_InitialSchema.cs`.
2. Verificar que el método `Up`:
   - Crea la tabla `Categories` con todas las
     columnas (`Id`, `Name`, `Color`, `Icon`,
     `IsSystem`, `CreatedAt`, `UpdatedAt`).
   - Crea el índice único `IX_Categories_Name`
     sobre la columna `Name`.
   - Crea la tabla `PaymentMethods` con sus
     columnas (`Id`, `Name`, `IsSystem`,
     `CreatedAt`, `UpdatedAt`).
   - Crea el índice único `IX_PaymentMethods_Name`.
   - **NO** incluye `migrationBuilder.InsertData(...)`
     en ningún lugar (el seed se aplica vía
     `HasData` en runtime, no en la migración).
3. Verificar que el método `Down`:
   - Borra los 2 índices únicos.
   - Borra las 2 tablas.
4. Si la migración incluye `InsertData` por error
   (e.g., porque el snapshot tenía `HasData`),
   **eliminarlas manualmente** del `Up` y del
   `Down` antes de continuar. NO es un
   regenerar la migración; es una edición
   quirúrgica.
5. Verificar que el `Designer.cs` y el
   `ModelSnapshot.cs` no requieren edición
   manual (se regeneran automáticamente si es
   necesario).

### T-004.5 — Verificar que compila y los tests pasan

1. `dotnet build` desde la raíz: 0 warnings, 0
   errors.
2. `dotnet test`: 111 tests verdes (las
   configuraciones no afectan a los tests
   existentes; solo confirman que el modelo se
   construye sin errores).

**Dependencias**
- T-001 (herramienta `dotnet-ef` instalada).
- T-002 (`CategoryConfiguration` existe).
- T-003 (`PaymentMethodConfiguration` existe).
- F-001 de Fase 2 (`AppDbContext` y paquetes EF
  Core 8.0.11 disponibles).

**Archivos / proyectos afectados**
- `src/ControlDeFinanzasIA.Infrastructure/Persistence/Migrations/YYYYMMDDhhmmss_InitialSchema.cs`
  (generado por la herramienta).
- `src/ControlDeFinanzasIA.Infrastructure/Persistence/Migrations/YYYYMMDDhhmmss_InitialSchema.Designer.cs`
  (generado por la herramienta).
- `src/ControlDeFinanzasIA.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs`
  (generado por la herramienta).

**Criterios de finalización**
- Los 3 archivos existen en
  `Infrastructure/Persistence/Migrations/`.
- El `Up` de la migración crea las 2 tablas y los
  2 índices únicos.
- El `Up` **NO** incluye `InsertData`.
- El `Down` borra las 2 tablas y los 2 índices.
- `dotnet build` y `dotnet test` pasan.
- Los 3 archivos están listos para commit (todos
  commiteados juntos, no solo el `.cs`).

---

## T-005 — Verificación de build, tests existentes, NetArchTest R-1 a R-5, y conformidad arquitectónica

**Objetivo**
Ejecutar la lista de comprobaciones que validan que
F-002 no rompió nada: build limpio, 111 tests
verdes, los 11 tests de NetArchTest de F-007 (R-1
a R-5) siguen pasando, y la arquitectura sigue
intacta (`Domain` sin paquetes, `Application`
solo con `FluentValidation`, `UI` sin
referencia a `Infrastructure`).

**Descripción**

### T-005.1 — Build limpio

1. Ejecutar
   `dotnet build "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --nologo`
   desde la raíz.
2. Confirmar 0 warnings, 0 errors.
3. Si hay warnings o errors, diagnosticar y
   arreglar antes de continuar (probablemente un
   `using` mal ubicado, una firma de
   `Configure` que no coincide, o un
   `InsertData` olvidado en la migración).

### T-005.2 — Tests existentes verdes

1. Ejecutar
   `dotnet test "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --nologo --no-build`
   desde la raíz.
2. Confirmar:
   - `Tests.Unit`: 111 passed, 0 failed.
   - `Tests.Integration`: 1 skipped (placeholder
     `F001PlaceholderTests`), 0 failed.
   - Total failures: 0.

### T-005.3 — NetArchTest R-1 a R-5

1. Ejecutar
   `dotnet test --filter "FullyQualifiedName~R"`.
   Confirmar que los 11 tests de NetArchTest
   pasan (R-1_Domain × 2, R-2_Application × 2,
   R-3_Infrastructure × 2, R-4_UI × 3, R-5 × 2).
2. Si algún test R-N falla, investigar:
   - **R-1 falla:** `Domain` importó EF Core por
     error (no debería; F-002 solo toca
     `Infrastructure`). Verificar
     `Domain.csproj` sigue vacío.
   - **R-2 falla:** `Application` importó EF Core
     por error. Verificar `Application.csproj`
     solo tiene `FluentValidation`.
   - **R-3 falla:** porque `Infrastructure` ahora
     referencia `Domain` y `Application` (ya lo
     hacía). Si falla, es un cambio sutil en las
     firmas. R-3 usa `.csproj` parsing para
     positive dependency checks (decisión de
     F-007); verificar que la migración no
     rompe el parseo.
   - **R-4 falla:** porque se añadió la
     dependencia `UI → Infrastructure` por error
     (no debería; F-002 NO la añade). Verificar
     `dotnet list src/ControlDeFinanzasIA.UI reference`.
   - **R-5 falla:** `src/` importó `Tests.*` por
     error. Verificar `dotnet list reference` en
     cada proyecto de `src/`.

### T-005.4 — Conformidad arquitectónica

1. Ejecutar
   `dotnet list src/ControlDeFinanzasIA.Domain package`.
   Debe estar **vacío**.
2. Ejecutar
   `dotnet list src/ControlDeFinanzasIA.Application package`.
   Debe seguir mostrando **solo `FluentValidation`**
   y **NO** `Microsoft.EntityFrameworkCore.*`.
3. Ejecutar
   `dotnet list src/ControlDeFinanzasIA.UI reference`.
   Debe seguir mostrando solo `Application` y
   `Domain` (NO `Infrastructure`).
4. Ejecutar
   `dotnet list src/ControlDeFinanzasIA.Infrastructure reference`.
   Debe mostrar `Application` (ya lo hacía).
5. Ejecutar
   `dotnet list src/ControlDeFinanzasIA.Infrastructure package`.
   Debe seguir mostrando los 3 paquetes EF Core
   directos (Core, Sqlite, Design) y posiblemente
   `Relational` como transitivo de Sqlite.

### T-005.5 — Verificar la migración con `dotnet ef migrations script` (opcional pero recomendado)

1. Ejecutar
   `dotnet ef migrations script --project src/ControlDeFinanzasIA.Infrastructure --startup-project src/ControlDeFinanzasIA.Infrastructure`
   para generar el SQL de la migración.
2. Verificar que el SQL contiene solo `CREATE TABLE`
   y `CREATE INDEX` (no `INSERT INTO`).
3. Esta verificación es opcional pero detecta
   el caso de borde EC-1 (InsertData incluido
   por error).

**Dependencias**
- T-001 a T-004 (todas las anteriores).

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
- (Opcional) `dotnet ef migrations script` muestra
  solo DDL, no `INSERT INTO`.

---

## T-006 — Format check, actualización de `features.md`, rename de la spec a `.done.md` y build final

**Objetivo**
Ejecutar la lista de comprobaciones finales de la
spec (§15 AC-5, AC-7), validar convenciones de
formato, dejar el repo en estado de "F-002
terminada" y marcar la feature como completada en
el desglose de fase.

**Descripción**

### T-006.1 — Format check

1. Ejecutar
   `dotnet format style --verify-no-changes` desde
   la raíz.
2. Confirmar exit 0. Si propone cambios:
   - Aplicarlos con `dotnet format style` y volver
     a verificar.
   - **NO** relajar reglas globales en
     `.editorconfig` o `stylecop.json`.
3. Los archivos generados por `dotnet ef`
   (migración, Designer, ModelSnapshot) pueden
   tener `using` redundantes o formato que no
   coincide con el resto del repo. El comando
   `dotnet format style` los normaliza.

### T-006.2 — Verificar convenciones en los 5 archivos nuevos

1. Confirmar que los 5 archivos nuevos
   (2 configuraciones + 3 de migración) usan:
   - File-scoped namespace (los 2 de
     configuración; los de migración también).
   - `using` directives al tope (NO después del
     namespace).
   - XML doc en cada clase y método público
     (los 2 de configuración; los de migración
     son auto-generados).
   - `sealed` en las 2 configuraciones
     (`CategoryConfiguration`,
     `PaymentMethodConfiguration`).
2. Si algún archivo no cumple, arreglarlo antes
   de cerrar F-002. Los archivos de migración
   son auto-generados; si tienen formato
   inconsistente, se acepta (es estándar en
   proyectos EF Core).

### T-006.3 — Cierre de la feature

1. **Actualizar el desglose de fase:** en
   `spec/phases/phase-02/features.md`:
   - Cambiar la fila 002 de la tabla resumen
     de "Pendiente" a "✅ COMPLETADA".
   - Cambiar el header de la sección F-002 de
     `## F-002 — Configuraciones de entidades + migración inicial`
     a
     `## F-002 — Configuraciones de entidades + migración inicial — ✅ COMPLETADA`.
   - Actualizar también el campo `> **Estado:**`
     en el header de la spec (después del
     rename).
2. **Renombrar la spec:** mover
   `spec/features/phase-02/F-002-entity-configurations-migration.md`
   a
   `spec/features/phase-02/F-002-entity-configurations-migration.done.md`.
3. **Build final** después del rename:
   `dotnet build -c Release` debe seguir 0/0.
4. **Tests finales**:
   `dotnet test -c Release --no-build` debe seguir
   111/1 (passed/skipped).
5. **Commit atómico** (cuando el developer
   commitee): los 5 archivos nuevos (2
   configuraciones + 3 de migración) + el
   `.config/dotnet-tools.json` + el rename de
   la spec + el `features.md` actualizado van
   en un solo commit, idealmente con un mensaje
   como `feat(persistence): add entity configurations and initial migration (F-002)`.

### Resumen de verificación de AC

| AC | Criterio | Tarea que lo verifica |
| --- | --- | --- |
| AC-1 | 2 configuraciones creadas con todas las reglas | T-002, T-003 |
| AC-2 | Migración `InitialSchema` con 3 archivos, sin `InsertData` | T-004 |
| AC-3 | `.config/dotnet-tools.json` con `dotnet-ef` 8.0.x | T-001 |
| AC-4 | Build limpio, 111 tests verdes, 11 NetArchTest verdes | T-005 |
| AC-5 | Convenciones del repo (file-scoped, sealed, format) | T-006.1, T-006.2 |
| AC-6 | Sin scope creep (AppDbContext intacto, sin repos, sin UI→Infrastructure) | T-005.4 |
| AC-7 | Documentación y trazabilidad (features.md + rename) | T-006.3 |

**Dependencias**
- T-001 a T-005 (todas las anteriores).

**Archivos / proyectos afectados**
- `spec/phases/phase-02/features.md` (modificado:
  F-002 marcada como completada).
- `spec/features/phase-02/F-002-entity-configurations-migration.md`
  (renombrado a `.done.md`).

**Criterios de finalización**
- `dotnet format style --verify-no-changes`: exit 0.
- `dotnet build -c Release`: 0 warnings, 0 errors.
- `dotnet test -c Release --no-build`: 111 passed,
  1 skipped, 0 failures.
- Los 2 archivos de configuración siguen las
  convenciones del repo (file-scoped, `using` al
  tope, XML doc, sealed).
- `spec/phases/phase-02/features.md` actualizado
  con F-002 marcada como completada (tabla
  resumen y header de sección).
- Spec renombrada a `.done.md`.

---

## Orden recomendado y trabajo en paralelo

**Orden secuencial estricto:** T-001 → (T-002 ‖ T-003 en
paralelo posible) → T-004 → T-005 → T-006.

- **T-001 debe ir primero** porque T-004 lo
  necesita.
- **T-002 y T-003 son independientes entre sí**
  (archivos distintos, no comparten código). Si
  hay 2 personas, se pueden hacer en paralelo.
  T-001 no bloquea estas (la compilación no
  requiere `dotnet-ef`; solo lo necesita T-004).
- **T-004 depende de T-001, T-002 y T-003** (usa
  la herramienta, las configuraciones).
- **T-005 es verificación** de T-001 a T-004.
- **T-006 es cierre** de toda la feature.

## Trabajo en paralelo posible

Con 2 personas:
- Persona A: T-001 → T-002.
- Persona B: T-003 (paralela a T-002).
- Luego ambos: T-004 → T-005 → T-006.

Con 1 persona (el caso más probable, porque la
feature es pequeña): orden secuencial estricto.

## Riesgos de la fase de tareas

| Riesgo | Probabilidad | Impacto | Mitigación |
| --- | --- | --- | --- |
| Versión de `dotnet-ef` no coincide con EF Core 8.0.11. | Baja | Alto | T-001 pinnea la versión en `.config/dotnet-tools.json`. Verificar con `dotnet ef --version` antes de T-004. |
| La migración incluye `InsertData` por error (porque el snapshot tiene `HasData`). | Baja | Medio | T-004.4 verifica explícitamente que el `Up` no tiene `InsertData`. Si lo tiene, se elimina manualmente. EC-1 de la spec lo documenta. |
| Las configuraciones no son discoverable por `ApplyConfigurationsFromAssembly`. | Muy baja | Alto | El assembly es el mismo; las clases son `public` y `sealed`. Si no se descubren, es un problema de namespace o de visibilidad. T-002 y T-003 especifican el namespace exacto. |
| R-1 de F-007 falla porque `Domain` importó EF Core por error. | Muy baja | Alto | T-002 y T-003 NO tocan `Domain`. T-005.3 verifica R-1. |
| Los archivos de migración auto-generados rompen `dotnet format`. | Baja | Bajo | T-006.1 aplica los cambios de format. Si los archivos auto-generados no se pueden normalizar completamente, se acepta (es estándar en proyectos EF Core). |
| T-004 genera un `AppDbContextModelSnapshot.cs` que diverge de las configuraciones. | Baja | Alto | Si se ejecuta `dotnet ef migrations add` sin haber commiteado las configuraciones, el snapshot podría no reflejar los cambios. La spec exige commit atómico. |
| La migración NO se regenera si se hacen cambios futuros en las configuraciones. | Baja (responsabilidad del developer) | Bajo | Cualquier cambio futuro en una configuración debe ir acompañado de una nueva migración. Documentado en EC-4. |
| El `--startup-project` queda como `Infrastructure` cuando F-005 lo cambie a `UI`. | Baja | Bajo | F-005 lo actualiza. T-004 documenta explícitamente que F-002 usa `Infrastructure` como startup project. |
| `dotnet tool restore` falla porque el developer no tiene .NET 8 SDK. | Baja | Alto | Verificar con `dotnet --version` antes de T-001. Si no es 8.0.x, el developer debe instalar el SDK correcto. |

## Referencias

- `spec/features/phase-02/F-002-entity-configurations-migration.md`
  — la spec completa de F-002. Este tasks file es su
  descomposición técnica.
- `docs/roadmap.md` — Fase 2 (Entregables esperados,
  Riesgos principales, Criterios de fase completada).
- `docs/architecture.md` — §3 (solución y proyectos),
  §4 (grafo de dependencias), §8 (capa de
  Infrastructure: persistencia, repositorios).
- `docs/vision.md` — §4.10 (Datos y portabilidad).
- `spec/constitution/tech-stack.md` — paquetes,
  convenciones de código, CPM.
- `spec/constitution/mission.md` — Clean
  Architecture, principios.
- `spec/phases/phase-02/features.md` — F-002 dentro
  del desglose de la fase 2 (6 features totales).
- `spec/features/phase-02/F-001-appdbcontext-addpersistence.done.md`
  — provee `AppDbContext` y los paquetes EF Core
  8.0.11 que F-002 usa. Define `SeedData` como
  stub (F-002 NO lo rellena; F-003 lo hace).
- `spec/features/phase-01/F-001-skeleton-solution.done.md`
  — convenciones de `.editorconfig`, `stylecop.json`,
  CPM, y `.gitignore` (que excluye `.tools/`).
- `spec/features/phase-01/F-006-contrato-interfaces-repo.done.md`
  — provee `IUnitOfWork` que F-002 referencia (no
  implementa).
- `spec/features/phase-01/F-007-validacion-arquitectonica.done.md`
  — los tests R-1 a R-5 que F-002 no debe romper.
- `spec/README.md` — convención del directorio
  `spec/`.
- EF Core 8 documentation oficial (a consultar vía
  Context7 cuando se implemente).
- `dotnet ef` CLI documentation (a consultar vía
  Context7 cuando se genere la migración).
