# Tareas — F-005: Composition Root con Persistencia y Migraciones

> **Spec:** `spec/features/phase-02/F-005-composition-root-persistence.md`
> **Fuentes de verdad:** la spec, `docs/architecture.md` §3, §4,
> §8, §9, `docs/roadmap.md` §Fase 2, `docs/vision.md` §3
> (es-DO como locale por defecto), `spec/constitution/tech-stack.md`
> (paquetes, CPM), `spec/constitution/mission.md`
> (Clean Architecture),
> `spec/phases/phase-02/features.md` (F-005 dentro del
> desglose de la fase 2),
> `spec/features/phase-02/F-001-appdbcontext-addpersistence.done.md`
> (provee `AddPersistence` que F-005 enchufa),
> `spec/features/phase-02/F-002-entity-configurations-migration.done.md`
> (generó las 2 migraciones),
> `spec/features/phase-02/F-003-seed-data.done.md` (sembró
> las 14 entidades del sistema),
> `spec/features/phase-02/F-004-category-paymentmethod-repositories.done.md`
> (implementó los 2 repos),
> `spec/features/phase-01/F-007-validacion-arquitectonica.done.md`
> (el test R-4 con filtro `Bootstrap`),
> `spec/features/phase-01/F-008-composition-root.done.md`
> (provee `Bootstrap.ConfigureServices` y `App.axaml.cs`
> que F-005 extiende).
>
> Cada tarea es implementable en menos de 2 horas. El proyecto
> debe seguir compilando al finalizar cada una. La numeración
> (T-001...) es local a esta feature. F-005 modifica 3 archivos
> en `UI/`: `Bootstrap.cs`, `App.axaml.cs`, y `UI.csproj`. NO
> genera migraciones nuevas, NO modifica Infrastructure, NO
> añade tests.

---

## Resumen de tareas

| ID | Tarea | Estimación | Depende de |
| --- | --- | --- | --- |
| T-001 | Añadir `<ProjectReference>` a `Infrastructure` en `UI.csproj` | XS | — |
| T-002 | Modificar `Bootstrap.ConfigureServices` para llamar a `AddPersistence(pathResolver)` | S | T-001 |
| T-003 | Modificar `App.axaml.cs` para aplicar migraciones al arrancar dentro de un `IServiceScope` | S | T-002 |
| T-004 | Verificación de build, tests existentes, NetArchTest R-1 a R-5 (especialmente R-4), arranque end-to-end de la app, y conformidad arquitectónica | M | T-001 a T-003 |
| T-005 | Format check, actualización de `features.md`, rename de la spec a `.done.md` y build final | S | T-001 a T-004 |

**Total estimado:** menos de 1 jornada. T-001 es trivial;
T-002 y T-003 son la lógica principal; T-004 es la
verificación más pesada (incluye arranque end-to-end
de la app); T-005 es cierre.

---

## T-001 — Añadir `<ProjectReference>` a `Infrastructure` en `UI.csproj`

**Objetivo**
Introducir la única dependencia entre `UI` e
`Infrastructure` que faltaba. Esta es la dependencia
arquitectónica que R-4 de F-007 vigila con el
filtro `Bootstrap` (anotado con `[CompositionRoot]`).

**Descripción**

1. Abrir
   `src/ControlDeFinanzasIA.UI/ControlDeFinanzasIA.UI.csproj`.
2. Localizar el `<ItemGroup>` que contiene los
   `<ProjectReference>` actuales (a `Application`
   y `Domain`).
3. Añadir una nueva línea después de las
   existentes:
   ```xml
   <ProjectReference Include="..\ControlDeFinanzasIA.Infrastructure\ControlDeFinanzasIA.Infrastructure.csproj" />
   ```
4. **NO** añadir `Private="..."`, `IncludeAssets`,
   ni otros atributos. La referencia es directa.
5. **NO** modificar los `<PackageReference>`
   existentes (F-001 de Phase 1 los configuró
   correctamente).
6. **NO** añadir un `<ProjectReference>` a
   `Tests.Unit` ni a `Tests.Integration`. Eso
   violaría R-5.

**Dependencias**
- F-001 de Fase 2 (los `<PackageReference>` de
  Microsoft.Extensions ya están en
  `UI.csproj`).
- F-002 de Fase 2 (el proyecto
  `Infrastructure` existe y compila).

**Archivos / proyectos afectados**
- `src/ControlDeFinanzasIA.UI/ControlDeFinanzasIA.UI.csproj`
  (modificado: +1 línea).

**Criterios de finalización**
- `UI.csproj` tiene el nuevo
  `<ProjectReference>` a `Infrastructure`.
- `dotnet build "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --nologo`
  desde la raíz: 0 warnings, 0 errors.
- `dotnet list src/ControlDeFinanzasIA.UI reference`
  muestra: `Application`, `Domain`, **`Infrastructure`**.

---

## T-002 — Modificar `Bootstrap.ConfigureServices` para llamar a `AddPersistence(pathResolver)`

**Objetivo**
Extender el método `ConfigureServices` (de F-008
de Phase 1) para llamar a `AddPersistence` después
de registrar `IClock` e `IPathResolver`. Esto
enchufa la persistencia (F-001 a F-004 de Phase 2)
en el composition root.

**Descripción**

### T-002.1 — Leer `Bootstrap.ConfigureServices` actual

1. Abrir
   `src/ControlDeFinanzasIA.UI/Composition/Bootstrap.cs`.
2. Localizar el método `ConfigureServices(IServiceCollection
   services, IConfiguration configuration)`.
3. Confirmar que registra (en este orden):
   - `services.AddSingleton<IClock, SystemClock>();`
   - `services.AddSingleton<IPathResolver>(pathResolver);`
4. Si el orden es diferente, NO modificarlo (es
   el patrón de F-008 de Phase 1).

### T-002.2 — Añadir la llamada a `AddPersistence`

1. Después de `services.AddSingleton<IPathResolver>(pathResolver);`,
   añadir:
   ```text
   IPathResolver pathResolver = services.BuildServiceProvider().GetRequiredService<IPathResolver>();
   services.AddPersistence(pathResolver);
   ```
2. **Importante:** `BuildServiceProvider()` se usa
   para resolver `IPathResolver` ANTES de que el
   host esté construido. Esto es válido porque
   `IPathResolver` es un Singleton sin estado
   mutable. El `ServiceProvider` temporal creado
   aquí se debe disposer después de obtener el
   `IPathResolver` para evitar memory leaks.
3. **Manejo de disposición:** envolver la línea
   `BuildServiceProvider()` en un `using` o
   llamar a `Dispose()` explícitamente:
   ```text
   ServiceProvider tempProvider = services.BuildServiceProvider();
   try
   {
       IPathResolver pathResolver = tempProvider.GetRequiredService<IPathResolver>();
       services.AddPersistence(pathResolver);
   }
   finally
   {
       tempProvider.Dispose();
   }
   ```
4. **Alternativa descartada:** usar
   `services.AddSingleton<IPathResolver>(...)` y
   luego `services.AddPersistence(...)` directamente
   sin resolver `IPathResolver` (asumiendo que
   `AddPersistence` lo resuelve internamente). Esto
   cambiaría la firma de `AddPersistence`; la spec
   opta por mantenerla.

### T-002.3 — Actualizar el XML doc

1. El XML doc del método `ConfigureServices`
   actualmente dice: "registra los servicios
   runtime (Clock, PathResolver) en el contenedor
   de DI".
2. Actualizarlo para mencionar que también
   registra la persistencia (AppDbContext,
   IUnitOfWork, ICategoryRepository,
   IPaymentMethodRepository) vía `AddPersistence`.
3. Citar la spec F-001 (de Phase 2) §RF-4 y la
   spec F-005.

### T-002.4 — Verificar compilación

1. Ejecutar
   `dotnet build "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --nologo`.
2. Confirmar 0 warnings, 0 errors.
3. Si hay errores, revisar:
   - Que el `using` para `Infrastructure.Persistence`
     está presente (para `AddPersistence`).
   - Que el `using` para `IPathResolver` está
     presente (de F-008 de Phase 1).

**Dependencias**
- T-001 (`UI.csproj` referencia `Infrastructure`,
  necesario para que `AddPersistence` resuelva).
- F-001 de Fase 2 (provee `AddPersistence`).
- F-008 de Phase 1 (provee `Bootstrap.cs` base).

**Archivos / proyectos afectados**
- `src/ControlDeFinanzasIA.UI/Composition/Bootstrap.cs`
  (modificado: +1 bloque de código, XML doc
  actualizado).

**Criterios de finalización**
- `Bootstrap.ConfigureServices` llama a
  `AddPersistence(pathResolver)` después de
  registrar `IPathResolver`.
- El `ServiceProvider` temporal se dispone
  correctamente (con `using` o `try/finally`).
- XML doc actualizado.
- `dotnet build`: 0 warnings, 0 errors.

---

## T-003 — Modificar `App.axaml.cs` para aplicar migraciones al arrancar dentro de un `IServiceScope`

**Objetivo**
Extender `App.axaml.cs` (de F-008 de Phase 1) para
aplicar las 2 migraciones (`InitialSchema` y
`SeedSystemData`) al arrancar la app, dentro de un
`IServiceScope` que se dispone correctamente. Esto
es la pieza que **hace que la app persista datos
reales** end-to-end.

**Descripción**

### T-003.1 — Leer `App.axaml.cs` actual

1. Abrir `src/ControlDeFinanzasIA.UI/App.axaml.cs`.
2. Localizar el método
   `OnFrameworkInitializationCompleted()`.
3. Confirmar que el método actual (de F-008 de
   Phase 1) hace:
   - Resuelve `ILogger<App>`, `IClock`,
     `IPathResolver` desde `Services`.
   - Calcula `startTime = _clock.UtcNow;`
   - Emite "App starting".
   - Crea `mainWindow` con título
     "ControlDeFinanzasIA".
   - Wirea `mainWindow.Closing` con "App
     stopping".
   - Asigna `desktop.MainWindow = mainWindow`.
   - Llama a `base.OnFrameworkInitializationCompleted();`

### T-003.2 — Añadir la aplicación de migraciones

1. Añadir los `using` necesarios al tope:
   - `using ControlDeFinanzasIA.Infrastructure.Persistence;` (para
     `AppDbContext`).
   - `using Microsoft.Extensions.DependencyInjection;`
     (para `IServiceScope`).
   - `using Microsoft.Extensions.Logging;` (puede
     que ya esté de F-008).
2. **Decisión de orden:** aplicar migraciones
   DESPUÉS de emitir "App starting" y ANTES de
   crear la ventana. Esto permite que el log de
   "App starting" aparezca incluso si la migración
   falla (útil para debugging).
3. Insertar el siguiente bloque después del log
   "App starting" y antes de la creación de la
   ventana:
   ```text
   using (IServiceScope scope = Services.CreateScope())
   {
       AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
       db.Database.Migrate();
       _logger.LogInformation("Migrations applied");
   }
   ```
4. **Importante:** `using (IServiceScope scope = ...)`
   garantiza que el `AppDbContext` y sus
   dependencias se disposent correctamente,
   incluso si `Migrate()` lanza una excepción.
5. **Manejo de excepciones:** si `Migrate()` falla,
   la excepción se propaga. NO se captura. La app
   termina con un mensaje claro. El usuario ve
   el log de "App starting" + el error.

### T-003.3 — Verificar compilación

1. Ejecutar
   `dotnet build "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --nologo`.
2. Confirmar 0 warnings, 0 errors.
3. Si hay errores, revisar:
   - Que el `using` para
     `ControlDeFinanzasIA.Infrastructure.Persistence`
     está presente.
   - Que el `using` para
     `Microsoft.Extensions.DependencyInjection`
     está presente.

**Dependencias**
- T-002 (`Bootstrap.ConfigureServices` ya llama a
  `AddPersistence`, así que `AppDbContext` está
  registrado en DI).
- F-001 de Fase 2 (provee `AppDbContext`).
- F-002 de Fase 2 (las 2 migraciones existen).
- F-008 de Phase 1 (provee `App.axaml.cs` base).

**Archivos / proyectos afectados**
- `src/ControlDeFinanzasIA.UI/App.axaml.cs`
  (modificado: +1 bloque de código, +2 `using`).

**Criterios de finalización**
- `App.axaml.cs` tiene el bloque
  `using (IServiceScope scope = ...)` con
  `db.Database.Migrate()` después de emitir
  "App starting".
- `dotnet build`: 0 warnings, 0 errors.

---

## T-004 — Verificación de build, tests existentes, NetArchTest R-1 a R-5, arranque end-to-end de la app, y conformidad arquitectónica

**Objetivo**
Ejecutar la lista de comprobaciones exhaustivas que
validan que F-005 hace que la app funcione
end-to-end: build limpio, 111 tests verdes, R-4
sigue pasando (la pieza arquitectónica crítica), la
app arranca y aplica migraciones, y la DB se crea
y siembra correctamente.

**Descripción**

### T-004.1 — Build limpio

1. Ejecutar
   `dotnet build "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --nologo`.
2. Confirmar 0 warnings, 0 errors.
3. Si hay warnings o errors, diagnosticar y
   arreglar antes de continuar (probablemente un
   `using` mal ubicado o una referencia circular).

### T-004.2 — Tests existentes verdes

1. Ejecutar
   `dotnet test "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --nologo --no-build`.
2. Confirmar:
   - `Tests.Unit`: 111 passed, 0 failed.
   - `Tests.Integration`: 1 skipped (placeholder
     `F001PlaceholderTests`), 0 failed.
   - Total failures: 0.

### T-004.3 — NetArchTest R-1 a R-5 (especialmente R-4)

1. Ejecutar
   `dotnet test --filter "FullyQualifiedName~R1_Domain"`.
   Confirmar R-1 pasa.
2. Ejecutar
   `dotnet test --filter "FullyQualifiedName~R4_UI"`.
   Confirmar los 3 tests de R-4 pasan. Este es el
   chequeo **más importante** de F-005: verifica
   que la nueva dependencia `UI → Infrastructure`
   pasa por el filtro `Bootstrap` correctamente.
3. Ejecutar
   `dotnet test --filter "FullyQualifiedName~ArchitectureTests"`.
   Confirmar los 11 tests de NetArchTest pasan.
4. Si R-4 falla, investigar:
   - El filtro `.That().DoNotHaveName("Bootstrap")`
     debería excluir el tipo `Bootstrap`. Si no
     funciona, es un bug en el filtro (de F-007) o
     en el nombre de la clase.
   - Verificar que `Bootstrap` está anotada con
     `[CompositionRoot]` (de F-008 de Phase 1).
   - Verificar que el `using` en
     `ArchitectureTests.cs` es el correcto (de
     F-007).

### T-004.4 — Conformidad arquitectónica

1. Ejecutar
   `dotnet list src/ControlDeFinanzasIA.Domain package`.
   Debe estar **vacío**.
2. Ejecutar
   `dotnet list src/ControlDeFinanzasIA.Application package`.
   Debe seguir mostrando **solo `FluentValidation`**.
3. Ejecutar
   `dotnet list src/ControlDeFinanzasIA.UI reference`.
   Debe mostrar ahora: `Application`, `Domain`,
   **`Infrastructure`** (la nueva dependencia).
4. Ejecutar
   `dotnet list src/ControlDeFinanzasIA.Infrastructure reference`.
   Debe seguir mostrando `Application` (y, transitivamente,
   `Domain`).

### T-004.5 — Arranque end-to-end de la app

> **Nota:** este paso es el más importante de F-005.
> Verifica que la app realmente persiste datos.

1. **Borrar la DB existente** (si la hay) para
   simular una instalación limpia:
   ```text
   Remove-Item "$env:APPDATA\ControlDeFinanzasIA\data.db" -ErrorAction SilentlyContinue
   Remove-Item "$env:APPDATA\ControlDeFinanzasIA\data.db-wal" -ErrorAction SilentlyContinue
   Remove-Item "$env:APPDATA\ControlDeFinanzasIA\data.db-shm" -ErrorAction SilentlyContinue
   ```
2. Ejecutar
   `dotnet run --project src/ControlDeFinanzasIA.UI`
   desde la raíz.
3. Verificar:
   - La ventana aparece con título
     "ControlDeFinanzasIA".
   - El log incluye "App starting" y "Migrations
     applied".
4. **No** cerrar la ventana todavía; dejar la
   app corriendo y verificar la DB.
5. Verificar que `data.db` existe en
   `%APPDATA%\ControlDeFinanzasIA\data.db` (o
   `%LOCALAPPDATA%` según la versión de Windows).
6. **Cerrar la ventana** (X o Alt+F4).
7. Verificar que el log incluye "App stopping".
8. **Verificar la DB** (opcional pero recomendado):
   - Usar `dotnet ef dbcontext info` o
     `sqlite3` para inspeccionar la DB.
   - Confirmar que `SELECT COUNT(*) FROM Categories`
     devuelve 9.
   - Confirmar que `SELECT COUNT(*) FROM PaymentMethods`
     devuelve 5.
   - Confirmar que `SELECT * FROM __EFMigrationsHistory`
     tiene 2 filas: `InitialSchema` y `SeedSystemData`.
9. **Volver a arrancar** (`dotnet run`):
   - El log NO debería incluir "Migrations
     applied" (las 2 migraciones ya están
     aplicadas).
   - La ventana aparece.

### T-004.6 — Verificación de regresión

- Verificar que los pasos T-001 a T-003 no
  rompieron:
  - F-001 (AppDbContext + AddPersistence) sigue
    funcional.
  - F-002 (migraciones) se aplican
    correctamente.
  - F-003 (seed) siembra los 14 registros.
  - F-004 (repos) están registrados en DI.

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
- Los 11 tests de NetArchTest pasan, en particular
  R-4.
- `UI` referencia `Infrastructure`.
- `Domain` sin paquetes, `Application` solo
  `FluentValidation`.
- **Arranque end-to-end:** la app arranca, aplica
  migraciones, crea `data.db`, siembra 9
  categorías y 5 métodos de pago.

---

## T-005 — Format check, actualización de `features.md`, rename de la spec a `.done.md` y build final

**Objetivo**
Ejecutar la lista de comprobaciones finales de la
spec (§15 AC-5, AC-6 y AC-7), validar convenciones
de formato, dejar el repo en estado de "F-005
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
3. Los archivos modificados por F-005 (3
   archivos en `UI/`) deben cumplir con las
   convenciones del repo; `dotnet format` los
   normaliza.

### T-005.2 — Cierre de la feature

1. **Actualizar el desglose de fase:** en
   `spec/phases/phase-02/features.md`:
   - Cambiar la fila 005 de la tabla resumen
     de "Pendiente" a "✅ COMPLETADA".
   - Cambiar el header de la sección F-005 de
     `## F-005 — Composition root: registrar persistencia + aplicar migraciones al arranque`
     a
     `## F-005 — Composition root: registrar persistencia + aplicar migraciones al arranque — ✅ COMPLETADA`.
2. **Actualizar el campo `> **Estado:**`** en el
   header de la spec de `Pendiente de implementación`
   a `✅ COMPLETADA` (preventivamente, aprendiendo
   del sdd-verify de F-001).
3. **Renombrar la spec:** mover
   `spec/features/phase-02/F-005-composition-root-persistence.md`
   a
   `spec/features/phase-02/F-005-composition-root-persistence.done.md`.
4. **Build final** después del rename:
   `dotnet build -c Release` debe seguir 0/0.
5. **Tests finales**:
   `dotnet test -c Release --no-build` debe seguir
   111/1 (passed/skipped).
6. **Commit atómico** (cuando el developer
   commitee): las 3 modificaciones a
   `Bootstrap.cs`, `App.axaml.cs`, y `UI.csproj`
   + el rename de la spec + el `features.md`
   actualizado van en un solo commit, idealmente
   con un mensaje como
   `feat(persistence): wire AddPersistence and apply migrations on startup (F-005)`.

### Resumen de verificación de AC

| AC | Criterio | Tarea que lo verifica |
| --- | --- | --- |
| AC-1 | `Bootstrap.ConfigureServices` llama a `AddPersistence` | T-002 |
| AC-2 | `UI.csproj` añade `<ProjectReference>` a `Infrastructure` | T-001 |
| AC-3 | `App.axaml.cs` aplica migraciones al arrancar | T-003 |
| AC-4 | Arranque end-to-end (DB se crea, migraciones se aplican, seed se inserta) | T-004.5 |
| AC-5 | Build limpio, 111 tests, 11 NetArchTest (especialmente R-4) | T-004.1, T-004.2, T-004.3 |
| AC-6 | Sin scope creep | T-004.4 |
| AC-7 | Documentación y trazabilidad | T-005.2 |

**Dependencias**
- T-001 a T-004 (todas las anteriores).

**Archivos / proyectos afectados**
- `spec/phases/phase-02/features.md` (modificado:
  F-005 marcada como completada).
- `spec/features/phase-02/F-005-composition-root-persistence.md`
  (renombrado a `.done.md`).

**Criterios de finalización**
- `dotnet format style --verify-no-changes`: exit 0.
- `dotnet build -c Release`: 0 warnings, 0 errors.
- `dotnet test -c Release --no-build`: 111 passed,
  1 skipped, 0 failures.
- `spec/phases/phase-02/features.md` actualizado
  con F-005 marcada como completada (tabla
  resumen y header de sección).
- Spec renombrada a `.done.md`.

---

## Orden recomendado y trabajo en paralelo

**Orden secuencial estricto:** T-001 → T-002 → T-003 →
T-004 → T-005.

- **T-001 debe ir primero** porque T-002 referencia
  `Infrastructure` (necesita el `ProjectReference`
  de T-001).
- **T-002 depende de T-001** (necesita el
  `ProjectReference` para que `AddPersistence`
  resuelva).
- **T-003 depende de T-002** (necesita que
  `AddPersistence` haya registrado `AppDbContext`
  para que `Migrate()` funcione).
- **T-004 es verificación** de T-001 a T-003.
- **T-005 es cierre** de toda la feature.

No hay paralelización útil: cada tarea depende
de la anterior (T-001 establece la dependencia;
T-002 la enchufa; T-003 la ejercita; T-004 la
verifica).

## Trabajo en paralelo posible

Con 1 persona: orden secuencial estricto (la
feature es pequeña; el paralelismo no aporta
beneficio medible).

Con 2+ personas: las tareas son tan pequeñas
que la comunicación y el merge compensarían el
tiempo ganado. Recomiendo orden secuencial.

## Riesgos de la fase de tareas

| Riesgo | Probabilidad | Impacto | Mitigación |
| --- | --- | --- | --- |
| El test R-4 de F-007 falla porque la nueva dependencia `UI → Infrastructure` rompe la regla. | Baja | Alto | T-004.3 verifica R-4 explícitamente. Si falla, el filtro `Bootstrap` está mal configurado; revisar `ArchitectureTests.cs` y la firma del filtro. |
| `BuildServiceProvider()` en `ConfigureServices` causa memory leak. | Baja | Bajo | T-002.2 documenta explícitamente el manejo de disposición (con `using` o `try/finally`). |
| `Database.Migrate()` falla en una DB parcialmente inicializada. | Baja | Alto (app no arranca) | El usuario debe restaurar manualmente. F-019 introduce auto-backup. |
| El cambio de `--startup-project` a `UI` rompe futuras generaciones de migraciones. | Baja (futuro) | Medio | La spec §17.5 documenta el cambio. Las features futuras (probablemente Fase 3+) deben usar `UI` como startup project. |
| `dotnet run` no muestra la ventana porque la app falla al arrancar. | Baja | Alto | T-004.5 ejecuta `dotnet run` end-to-end. Si falla, el log muestra el error. |
| `dotnet format style` propone cambios en los archivos modificados. | Baja | Bajo | T-005.1 aplica los cambios. |
| El test R-1 de F-007 falla porque `AppDbContext` se importa en `Application`. | Muy baja (F-005 no toca `Application`) | Alto | T-004.3 verifica R-1. |
| `IServiceScope` no se dispone correctamente. | Baja (con `using` block) | Bajo (memory leak) | T-003.2 usa explícitamente `using (IServiceScope scope = ...)`. |

## Referencias

- `spec/features/phase-02/F-005-composition-root-persistence.md`
  — la spec completa de F-005. Este tasks file es su
  descomposición técnica.
- `docs/roadmap.md` — Fase 2 (Entregables esperados,
  Riesgos principales, Criterios de fase completada).
- `docs/architecture.md` — §3 (solución y proyectos),
  §4 (grafo de dependencias), §8 (capa de
  Infrastructure: persistencia), §9 (composition
  root).
- `docs/vision.md` — §3 (es-DO como locale por defecto).
- `spec/constitution/tech-stack.md` — paquetes,
  convenciones de código, CPM.
- `spec/constitution/mission.md` — Clean
  Architecture, principios.
- `spec/phases/phase-02/features.md` — F-005 dentro
  del desglose de la fase 2.
- `spec/features/phase-02/F-001-appdbcontext-addpersistence.done.md`
  — provee `AddPersistence` que F-005 enchufa.
- `spec/features/phase-02/F-002-entity-configurations-migration.done.md`
  — generó las 2 migraciones.
- `spec/features/phase-02/F-003-seed-data.done.md` —
  sembró las 14 entidades del sistema.
- `spec/features/phase-02/F-004-category-paymentmethod-repositories.done.md`
  — implementó los 2 repos.
- `spec/features/phase-01/F-007-validacion-arquitectonica.done.md`
  — el test R-4 con filtro `Bootstrap` que permite
  la dependencia `UI → Infrastructure`.
- `spec/features/phase-01/F-008-composition-root.done.md`
  — provee `Bootstrap.ConfigureServices` y
  `App.axaml.cs` que F-005 extiende.
- `spec/README.md` — convención del directorio
  `spec/`.
- EF Core 8 documentation oficial (a consultar vía
  Context7 cuando se implemente).
