# Tareas — F-003: Seed Data

> **Spec:** `spec/features/phase-02/F-003-seed-data.md`
> **Fuentes de verdad:** la spec, `docs/architecture.md` §3, §4,
> §8, `docs/roadmap.md` §Fase 2, `docs/vision.md` §3
> (es-DO como locale por defecto), `spec/constitution/tech-stack.md`
> (paquetes, CPM), `spec/constitution/mission.md`
> (Clean Architecture),
> `spec/phases/phase-02/features.md` (F-003 dentro del
> desglose de la fase 2),
> `spec/features/phase-02/F-001-appdbcontext-addpersistence.done.md`
> (introduce el stub de `SeedData` que esta feature
> rellena),
> `spec/features/phase-02/F-002-entity-configurations-migration.done.md`
> (generó la primera migración `InitialSchema` sin seed;
> F-003 añade `SeedSystemData` aparte).
>
> Cada tarea es implementable en menos de 2 horas. El proyecto
> debe seguir compilando al finalizar cada una. La numeración
> (T-001...) es local a esta feature. F-003 solo modifica
> `SeedData.cs` y añade 3 archivos de migración. NO modifica
> `InitialSchema`, `AppDbContext`, las configuraciones,
> `Domain`, `Application`, `UI`, `Tests.Unit`, ni
> `Tests.Integration`.

---

## Resumen de tareas

| ID | Tarea | Estimación | Depende de |
| --- | --- | --- | --- |
| T-001 | Rellenar `SeedData.Apply` con las 14 `HasData` calls (9 categorías + 5 métodos) | M | — |
| T-002 | Generar la migración `SeedSystemData` con `dotnet ef migrations add` y verificar su contenido | S | T-001 |
| T-003 | Verificar que `InitialSchema` queda intacta y la idempotencia del seed | XS | T-002 |
| T-004 | Verificación de build, tests existentes, NetArchTest R-1 a R-5, y conformidad arquitectónica | S | T-001 a T-003 |
| T-005 | Format check, actualización de `features.md`, rename de la spec a `.done.md` y build final | S | T-001 a T-004 |

**Total estimado:** menos de 1 jornada. T-001 es la más
larga (rellenar 14 entradas con datos específicos);
T-002 es mecánica (un comando y verificación de 3
archivos); T-003, T-004 y T-005 son verificación y
cierre.

---

## T-001 — Rellenar `SeedData.Apply` con las 14 `HasData` calls

**Objetivo**
Rellenar el cuerpo del método `SeedData.Apply` (que
F-001 introdujo como stub vacío) con las 9 categorías
y los 5 métodos de pago del sistema. Cada entrada
tiene ID determinista, `IsSystem=true`, y
`CreatedAt`/`UpdatedAt` en un timestamp UTC fijo.
Las llamadas `HasData` son el reflejo en el modelo
de los datos que la migración `SeedSystemData`
insertará.

**Descripción**

### T-001.1 — Verificar el stub actual

1. Abrir
   `src/ControlDeFinanzasIA.Infrastructure/Persistence/SeedData.cs`.
2. Confirmar que el archivo tiene la firma:
   - `public static class SeedData`.
   - `public static void Apply(ModelBuilder modelBuilder)`.
   - Cuerpo con un comentario (trivial).
3. Si la firma ha cambiado (no debería), restaurar
   la firma de F-001 antes de continuar.

### T-001.2 — Añadir las 9 categorías (HasData)

1. Dentro de `Apply`, antes de la línea final
   (`}`), añadir:
   ```text
   modelBuilder.Entity<Category>().HasData(
       new Category { /* Comida */ },
       new Category { /* Transporte */ },
       // ... 7 más
   );
   ```
2. Cada `Category` se construye con los valores de
   §10.1, §10.2, §10.3, §10.4 de la spec:
   - **9 categorías** con los nombres: Comida,
     Transporte, Hogar, Salud, Ocio, Educación,
     Ropa, Servicios, Otros.
   - **IDs** del rango
     `11111111-1111-1111-1111-00000000000X`
     (X = 1..9, en ese orden).
   - **`IsSystem = true`** en todas.
   - **`Color`** per §10.3: `#E67E22`, `#3498DB`,
     `#9B59B6`, `#1ABC9C`, `#E74C3C`, `#F39C12`,
     `#16A085`, `#34495E`, `#95A5A6`. La categoría
     "Otros" tiene `Color = null`.
   - **`Icon`** per §10.4: 🍔, 🚗, 🏠, 💊, 🎮, 📚,
     👕, 💡. La categoría "Otros" tiene `Icon = null`.
   - **`CreatedAt` y `UpdatedAt`** =
     `new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)`
     en todas.

### T-001.3 — Añadir los 5 métodos de pago (HasData)

1. Después de la llamada de categorías, añadir:
   ```text
   modelBuilder.Entity<PaymentMethod>().HasData(
       new PaymentMethod { /* Efectivo */ },
       new PaymentMethod { /* Tarjeta de débito */ },
       // ... 3 más
   );
   ```
2. Cada `PaymentMethod` se construye con:
   - **5 métodos**: Efectivo, Tarjeta de débito,
     Tarjeta de crédito, Transferencia, Otro.
   - **IDs** del rango
     `22222222-2222-2222-2222-00000000000X`
     (X = 1..5, en ese orden).
   - **`IsSystem = true`** en todos.
   - **`CreatedAt` y `UpdatedAt`** =
     `new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)`
     en todos.
   - **NO** `Color` ni `Icon` (campos que
     `PaymentMethod` no tiene).

### T-001.4 — Actualizar el XML doc

1. Actualizar el comentario XML doc del método
   `Apply` para que cite:
   - Las 14 entradas (9 categorías + 5 métodos).
   - Los IDs deterministas (los rangos `11111111-...`
     y `22222222-...`).
   - El timestamp fijo `2026-01-01 00:00:00 UTC`.
   - Que el seed se materializa en la migración
     `SeedSystemData` (generada en T-002).
2. Si F-001 dejó un XML doc que decía "F-003
   implementa el seed", reemplazarlo por el doc
   completo.

### T-001.5 — Verificar compilación

1. Ejecutar
   `dotnet build "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --nologo`
   desde la raíz.
2. Confirmar 0 warnings, 0 errors.
3. Si hay errores, revisar los nombres de
   propiedades, los tipos, y que las entidades
   `Category` y `PaymentMethod` tienen todas las
   propiedades referenciadas (en particular
   `CreatedAt` y `UpdatedAt`, que F-002 con la
   corrección del orchestrator añadió).

**Dependencias**
- F-001 de Fase 2 (provee el stub `SeedData`).
- F-002 de Fase 2 (las entidades `Category` y
  `PaymentMethod` con `CreatedAt` y `UpdatedAt`,
  gracias a la corrección del orchestrator).

**Archivos / proyectos afectados**
- `src/ControlDeFinanzasIA.Infrastructure/Persistence/SeedData.cs`
  (modificado: cuerpo del método `Apply`).

**Criterios de finalización**
- `SeedData.cs` existe y compila.
- `public static class SeedData` (heredado de
  F-001).
- `public static void Apply(ModelBuilder modelBuilder)`.
- El cuerpo del método llama a
  `modelBuilder.Entity<Category>().HasData(...)` con
  9 entradas.
- El cuerpo del método llama a
  `modelBuilder.Entity<PaymentMethod>().HasData(...)`
  con 5 entradas.
- Los 14 IDs son deterministas per §10.1 de la
  spec.
- Los 14 timestamps son
  `new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)`.
- Todas las 14 entradas tienen `IsSystem = true`.
- Las 9 categorías tienen `Name`, `Color` (null
  solo en "Otros"), `Icon` (null solo en "Otros").
- Los 5 métodos tienen `Name` (sin `Color`/`Icon`).
- File-scoped namespace.
- `using` al tope.
- XML doc actualizado.
- `dotnet build` desde la raíz: 0 warnings, 0
  errors.

---

## T-002 — Generar la migración `SeedSystemData` con `dotnet ef migrations add` y verificar su contenido

**Objetivo**
Generar la migración EF Core que captura el
sembrado en `InsertData` calls, de modo que cuando
F-005 aplique las migraciones al arrancar la app,
los 14 registros del sistema se inserten en la DB.
Verificar que la migración tiene el contenido
esperado (14 `InsertData` + 14 `DeleteData`).

**Descripción**

### T-002.1 — Verificar prerrequisitos

1. Confirmar que la herramienta `dotnet-ef` está
   instalada (de F-002 §T-001):
   `dotnet ef --version` debe mostrar 8.0.x.
2. Confirmar que `SeedData.Apply` ya tiene las 14
   `HasData` calls (de T-001).
3. Confirmar que `dotnet build` pasa en limpio
   (de T-001.5).

### T-002.2 — Ejecutar el comando de generación

1. Desde la raíz del repo, ejecutar:
   `dotnet ef migrations add SeedSystemData --project src/ControlDeFinanzasIA.Infrastructure --startup-project src/ControlDeFinanzasIA.Infrastructure`.
2. El `--startup-project` es `Infrastructure` mismo
   (mismo que F-002; F-005 lo cambiará a `UI`).
3. La herramienta debe completar sin errores.
4. Si falla, las causas típicas son:
   - `dotnet-ef` no instalado: ejecutar
     `dotnet tool restore`.
   - `SeedData.Apply` tiene un error de compilación:
     revisar T-001.
   - `DesignTimeAppDbContextFactory` (de F-002)
     tiene un error: revisar F-002.

### T-002.3 — Verificar los 3 archivos generados

1. Confirmar que existen 3 archivos en
   `src/ControlDeFinanzasIA.Infrastructure/Persistence/Migrations/`:
   - `YYYYMMDDhhmmss_SeedSystemData.cs` (la
     migración; el timestamp lo asigna la
     herramienta).
   - `YYYYMMDDhhmmss_SeedSystemData.Designer.cs`
     (metadata).
   - `AppDbContextModelSnapshot.cs` (snapshot
     sobreescrito; el snapshot anterior tenía sin
     `HasData`; el nuevo tiene las 14 entradas).
2. El `git status` debe mostrar los 3 archivos
   como modificados o untracked.

### T-002.4 — Verificar el contenido de la migración

1. Abrir `YYYYMMDDhhmmss_SeedSystemData.cs`.
2. Verificar que el método `Up`:
   - Tiene exactamente **9 `migrationBuilder.InsertData(...)` calls
     para la tabla `Categories`** (uno por cada
     categoría).
   - Tiene exactamente **5 `migrationBuilder.InsertData(...)` calls
     para la tabla `PaymentMethods`** (uno por cada
     método).
   - Los **IDs en los `InsertData` coinciden** con
     los de §10.1 de la spec (los rangos
     `11111111-...` para categorías y
     `22222222-...` para métodos).
   - Los **timestamps en los `InsertData` son
     `2026-01-01 00:00:00 UTC`**.
3. Verificar que el método `Down`:
   - Tiene exactamente **9 `migrationBuilder.DeleteData(...)` calls
     para la tabla `Categories`** (uno por cada
     categoría).
   - Tiene exactamente **5 `migrationBuilder.DeleteData(...)` calls
     para la tabla `PaymentMethods`** (uno por cada
     método).
   - Los `DeleteData` usan los mismos IDs que los
     `InsertData` (son inversos).
4. **No** debe haber `CreateTable` ni `CreateIndex`
   en `SeedSystemData` (esas operaciones son de
   `InitialSchema`, no de `SeedSystemData`).

### T-002.5 — Verificar que compila y los tests pasan

1. `dotnet build` desde la raíz: 0 warnings, 0
   errors.
2. `dotnet test`: 111 tests verdes (las 14
   entradas del seed no afectan a los tests
   existentes; solo confirman que el modelo se
   construye sin errores).

**Dependencias**
- T-001 (`SeedData.Apply` rellenado con 14
  `HasData` calls).
- F-002 de Fase 2 (herramienta `dotnet-ef` 8.0.x
  instalada; migración `InitialSchema` como base).

**Archivos / proyectos afectados**
- `src/ControlDeFinanzasIA.Infrastructure/Persistence/Migrations/YYYYMMDDhhmmss_SeedSystemData.cs`
  (generado por la herramienta).
- `src/ControlDeFinanzasIA.Infrastructure/Persistence/Migrations/YYYYMMDDhhmmss_SeedSystemData.Designer.cs`
  (generado por la herramienta).
- `src/ControlDeFinanzasIA.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs`
  (sobreescrito por la herramienta).

**Criterios de finalización**
- Los 3 archivos existen en
  `Migrations/`.
- El `Up` de la migración tiene 14 `InsertData`
  calls (9 + 5).
- El `Down` de la migración tiene 14 `DeleteData`
  calls (9 + 5).
- Los IDs en los `InsertData`/`DeleteData`
  coinciden con §10.1 de la spec.
- Los timestamps son `2026-01-01 00:00:00 UTC`.
- `dotnet build` y `dotnet test` pasan.
- Los 3 archivos están listos para commit (todos
  commiteados juntos con T-001 en un commit
  atómico).

---

## T-003 — Verificar que `InitialSchema` queda intacta y la idempotencia del seed

**Objetivo**
Confirmar que la nueva migración `SeedSystemData` no
ha modificado la `InitialSchema` existente (las
migraciones de EF Core son inmutables), y que el
seed es idempotente: re-aplicar las migraciones no
duplica los 14 registros.

**Descripción**

### T-003.1 — Verificar que `InitialSchema` no se ha modificado

1. Ejecutar
   `git diff src/ControlDeFinanzasIA.Infrastructure/Persistence/Migrations/YYYYMMDDhhmmss_InitialSchema.cs`
   (donde el timestamp es el de F-002).
2. El comando no debe mostrar ningún cambio. Si
   muestra cambios, **revertir** la modificación
   (la nueva migración es la responsable del seed,
   no `InitialSchema`).
3. Ejecutar
   `git diff src/ControlDeFinanzasIA.Infrastructure/Persistence/Migrations/YYYYMMDDhhmmss_InitialSchema.Designer.cs`.
4. El comando tampoco debe mostrar cambios (el
   Designer de InitialSchema es inmutable).
5. Si el `AppDbContextModelSnapshot.cs` se ha
   modificado, **es esperado** (la herramienta lo
   sobreescribe para reflejar el nuevo modelo).
   Pero solo debe haber cambios relacionados con
   los 14 `HasData` calls; el resto del snapshot
   debe ser idéntico.

### T-003.2 — Verificar la idempotencia (verificación estructural)

1. Confirmar que el `Up` de `SeedSystemData` NO
   incluye lógica de upsert (solo `InsertData`).
2. Confirmar que el `Up` de `SeedSystemData` no usa
   `migrationBuilder.Sql(...)` para lógica
   condicional.
3. La idempotencia viene del framework
   (`__EFMigrationsHistory`); no está en el código
   de la migración. Esto es correcto.

### T-003.3 — Verificar la idempotencia (verificación manual, opcional)

> **Nota:** este paso es opcional pero recomendado.
> Si el developer no puede ejecutarlo (e.g.,
> entorno headless), se documenta en el commit
> / PR.

1. Desde la raíz, ejecutar:
   `dotnet ef database update --project src/ControlDeFinanzasIA.Infrastructure --startup-project src/ControlDeFinanzasIA.Infrastructure`.
2. Esto aplica `InitialSchema` (si no estaba
   aplicada) y `SeedSystemData` (la nueva).
3. Verificar las filas:
   - `SELECT COUNT(*) FROM Categories` debe
     devolver 9.
   - `SELECT COUNT(*) FROM PaymentMethods` debe
     devolver 5.
   - Todas con `IsSystem = 1`.
4. Ejecutar el mismo comando una segunda vez.
5. Verificar que las filas siguen siendo 9 y 5
   (idempotencia).
6. Si la DB no existe o no se quiere crear, se
   puede omitir este paso; la verificación
   estructural (T-003.2) es suficiente.

### T-003.4 — Documentar la verificación

1. En el commit message del PR que cierra F-003,
   incluir una nota como:
   > "InitialSchema intacta (verificado con git
   > diff). SeedSystemData genera 14 InsertData
   > calls (9 categorías + 5 métodos). Idempotencia
   > garantizada por __EFMigrationsHistory."

**Dependencias**
- T-002 (migración `SeedSystemData` generada).

**Archivos / proyectos afectados**
- Ninguno nuevo. Esta tarea es solo de
  verificación.

**Criterios de finalización**
- `git diff` sobre `InitialSchema.cs` y su
  `Designer.cs` no muestra cambios.
- `git diff` sobre `AppDbContextModelSnapshot.cs`
  muestra SOLO cambios relacionados con los 14
  `HasData` calls; el resto del snapshot es
  idéntico.
- El `Up` de `SeedSystemData` NO tiene lógica de
  upsert (solo `InsertData`).
- (Opcional) `dotnet ef database update` aplicado
  dos veces seguidas produce el mismo número de
  filas (9 + 5).
- La verificación se documenta en el commit / PR.

---

## T-004 — Verificación de build, tests existentes, NetArchTest R-1 a R-5, y conformidad arquitectónica

**Objetivo**
Ejecutar la lista de comprobaciones que validan que
F-003 no rompió nada: build limpio, 111 tests
verdes, los 11 tests de NetArchTest de F-007 (R-1
a R-5) siguen pasando, y la arquitectura sigue
intacta (`Domain` sin paquetes, `Application`
solo con `FluentValidation`, `UI` sin
referencia a `Infrastructure`).

**Descripción**

### T-004.1 — Build limpio

1. Ejecutar
   `dotnet build "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --nologo`
   desde la raíz.
2. Confirmar 0 warnings, 0 errors.
3. Si hay warnings o errors, diagnosticar y
   arreglar antes de continuar (probablemente
   algún `using` mal ubicado o un error de
   tipeo en los IDs).

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
   pasan (R-1_Domain × 2, R-2_Application × 2,
   R-3_Infrastructure × 2, R-4_UI × 3, R-5 × 2).
2. Si algún test R-N falla, investigar:
   - **R-1 falla:** `Domain` importó EF Core por
     error (no debería; F-003 no toca `Domain`).
     Verificar `Domain.csproj` sigue vacío.
   - **R-2 falla:** `Application` importó EF Core
     por error. Verificar `Application.csproj`
     solo tiene `FluentValidation`.
   - **R-3 falla:** porque `Infrastructure` ahora
     referencia `Domain` y `Application` (ya lo
     hacía). Si falla, es un cambio sutil en las
     firmas. R-3 usa `.csproj` parsing para
     positive dependency checks (decisión de
     F-007); verificar que la nueva migración no
     rompe el parseo.
   - **R-4 falla:** porque se añadió la
     dependencia `UI → Infrastructure` por error
     (no debería; F-003 NO la añade). Verificar
     `dotnet list src/ControlDeFinanzasIA.UI reference`.
   - **R-5 falla:** `src/` importó `Tests.*` por
     error. Verificar `dotnet list reference` en
     cada proyecto de `src/`.

### T-004.4 — Conformidad arquitectónica

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

---

## T-005 — Format check, actualización de `features.md`, rename de la spec a `.done.md` y build final

**Objetivo**
Ejecutar la lista de comprobaciones finales de la
spec (§15 AC-6 y AC-8), validar convenciones de
formato, dejar el repo en estado de "F-003
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
3. Los archivos generados por `dotnet ef`
   (migración, Designer, ModelSnapshot) pueden
   tener `using` redundantes o formato que no
   coincide con el resto del repo. El comando
   `dotnet format style` los normaliza.

### T-005.2 — Cierre de la feature

1. **Actualizar el desglose de fase:** en
   `spec/phases/phase-02/features.md`:
   - Cambiar la fila 003 de la tabla resumen
     de "Pendiente" a "✅ COMPLETADA".
   - Cambiar el header de la sección F-003 de
     `## F-003 — Seed data: 9 categorías y 5 métodos de pago del sistema`
     a
     `## F-003 — Seed data: 9 categorías y 5 métodos de pago del sistema — ✅ COMPLETADA`.
2. **Actualizar el campo `> **Estado:**`** en el
   header de la spec de `Pendiente de implementación`
   a `✅ COMPLETADA` (después del rename, o antes
   de él; el archivo en disco se renombra en el
   siguiente paso).
3. **Renombrar la spec:** mover
   `spec/features/phase-02/F-003-seed-data.md`
   a
   `spec/features/phase-02/F-003-seed-data.done.md`.
4. **Build final** después del rename:
   `dotnet build -c Release` debe seguir 0/0.
5. **Tests finales**:
   `dotnet test -c Release --no-build` debe seguir
   111/1 (passed/skipped).
6. **Commit atómico** (cuando el developer
   commitee): el cambio a `SeedData.cs` + los 3
   archivos de migración + el rename de la spec
   + el `features.md` actualizado van en un solo
   commit, idealmente con un mensaje como
   `feat(persistence): seed 9 system categories and 5 payment methods (F-003)`.

### Resumen de verificación de AC

| AC | Criterio | Tarea que lo verifica |
| --- | --- | --- |
| AC-1 | `SeedData.Apply` con 14 `HasData` calls | T-001 |
| AC-2 | Migración `SeedSystemData` con 14 `InsertData` + 14 `DeleteData` | T-002 |
| AC-3 | `InitialSchema` intacta | T-003.1 |
| AC-4 | Build limpio, 111 tests, 11 NetArchTest | T-004 |
| AC-5 | Idempotencia (ejecutar la migración 2 veces) | T-003.3 |
| AC-6 | Convenciones del repo | T-005.1 |
| AC-7 | Sin scope creep | T-004.4 |
| AC-8 | Documentación y trazabilidad | T-005.2 |

**Dependencias**
- T-001 a T-004 (todas las anteriores).

**Archivos / proyectos afectados**
- `spec/phases/phase-02/features.md` (modificado:
  F-003 marcada como completada).
- `spec/features/phase-02/F-003-seed-data.md`
  (renombrado a `.done.md`).

**Criterios de finalización**
- `dotnet format style --verify-no-changes`: exit 0.
- `dotnet build -c Release`: 0 warnings, 0 errors.
- `dotnet test -c Release --no-build`: 111 passed,
  1 skipped, 0 failures.
- `spec/phases/phase-02/features.md` actualizado
  con F-003 marcada como completada (tabla
  resumen y header de sección).
- Spec renombrada a `.done.md`.

---

## Orden recomendado y trabajo en paralelo

**Orden secuencial estricto:** T-001 → T-002 → T-003 →
T-004 → T-005.

- **T-001 debe ir primero** porque T-002 genera la
  migración basándose en el modelo modificado por
  T-001.
- **T-002 depende de T-001.**
- **T-003 verifica la inmutabilidad de
  `InitialSchema` y la idempotencia**, que solo
  se puede comprobar después de T-002.
- **T-004 es verificación** de T-001 a T-003.
- **T-005 es cierre** de toda la feature.

No hay paralelización útil: el trabajo es
secuencial porque cada tarea depende de la
anterior. Con 1 persona, orden secuencial estricto.

## Trabajo en paralelo posible

Con 1 persona: orden secuencial estricto (la feature
es pequeña; el paralelismo no aporta beneficio
medible).

Con 2+ personas: las tareas son tan pequeñas que
la comunicación y el merge compensarían el tiempo
ganado. Recomiendo orden secuencial.

## Riesgos de la fase de tareas

| Riesgo | Probabilidad | Impacto | Mitigación |
| --- | --- | --- | --- |
| `dotnet ef` no está disponible (developer olvidó `dotnet tool restore`). | Baja | Medio | T-002.1 verifica `dotnet ef --version` antes de la generación. |
| El developer edita `InitialSchema` por error. | Baja | Alto | T-003.1 verifica con `git diff` que `InitialSchema` y su `Designer.cs` no han cambiado. Si cambiaron, revertir. |
| Los `InsertData` en la migración tienen IDs mal copiados. | Baja | Alto | T-002.4 verifica los IDs contra §10.1 de la spec. La herramienta genera los `InsertData` automáticamente desde el modelo, pero un typo en `SeedData.Apply` se propaga. |
| `CreatedAt` o `UpdatedAt` se pasan con `DateTimeKind.Local` en vez de `Utc`. | Baja | Bajo | El spec fija explícitamente `DateTimeKind.Utc`. T-001.2 y T-001.3 lo verifican. |
| El `AppDbContextModelSnapshot.cs` se corrompe porque la herramienta tuvo un bug. | Muy baja | Alto | T-002.5 verifica el build. Si falla, regenerar la migración borrando los 3 archivos de `SeedSystemData` y volviendo a ejecutar `dotnet ef migrations add`. |
| R-1 de F-007 falla porque `Domain` se contaminó con EF Core. | Muy baja (F-003 no toca `Domain`) | Alto | T-004.3 verifica R-1. |
| `dotnet format style` propone cambios en los archivos auto-generados de la migración. | Baja | Bajo | T-005.1 aplica los cambios. Si los archivos auto-generados no se pueden normalizar completamente, se acepta. |
| La idempotencia falla porque EF Core genera `InsertData` duplicados. | Muy baja | Alto | T-002.4 cuenta los `InsertData` (debe ser exactamente 14, no 15 o más). |
| El `HasData` no se aplica en runtime porque la app no llama a `Migrate()`. | Baja (F-005 lo hace) | Bajo | F-003 no aplica el seed; F-005 lo hace. F-003 es responsable solo de la generación. |
| F-005 cambia el `--startup-project` a `UI` y la nueva migración se rompe. | Baja (F-005 es futuro) | Bajo | F-005 documenta el cambio. F-003 no anticipa esto; usa `Infrastructure` como startup project actual. |

## Referencias

- `spec/features/phase-02/F-003-seed-data.md` —
  la spec completa de F-003. Este tasks file es su
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
- `spec/phases/phase-02/features.md` — F-003 dentro
  del desglose de la fase 2 (6 features totales).
- `spec/features/phase-02/F-001-appdbcontext-addpersistence.done.md`
  — provee `AppDbContext` y el stub de `SeedData`
  que esta feature rellena. §AC-8 y §17.5
  documentan el stub.
- `spec/features/phase-02/F-002-entity-configurations-migration.done.md`
  — provee las configuraciones, la migración
  `InitialSchema`, la herramienta `dotnet-ef` 8.0.11
  instalada, y el `DesignTimeAppDbContextFactory`.
  F-003 genera `SeedSystemData` como segunda
  migración; no toca `InitialSchema`.
- `spec/features/phase-01/F-001-skeleton-solution.done.md`
  — convenciones de `.editorconfig`, `stylecop.json`,
  CPM, y `.gitignore`.
- `spec/features/phase-01/F-006-contrato-interfaces-repo.done.md`
  — `IUnitOfWork` y las 15 interfaces que F-004
  implementa.
- `spec/features/phase-01/F-007-validacion-arquitectonica.done.md`
  — los tests R-1 a R-5 que F-003 no debe romper.
- `spec/features/phase-01/F-008-composition-root.done.md`
  — el `Bootstrap.ConfigureServices` que F-005
  enchufa (y donde F-005 llama a `Migrate()`).
- `spec/README.md` — convención del directorio
  `spec/`.
- EF Core 8 documentation oficial (a consultar vía
  Context7 cuando se implemente).
- `dotnet ef` CLI documentation (a consultar vía
  Context7 cuando se genere la migración).
