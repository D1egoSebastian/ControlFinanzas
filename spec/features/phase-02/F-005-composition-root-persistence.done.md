# Specification — F-005: Composition Root con Persistencia y Migraciones

> **Fase:** 2 — Persistencia con EF Core + SQLite
> **Feature:** F-005 (local a esta fase)
> **Estado:** ✅ COMPLETADA
> **Spec ID:** SPEC-F005P2
>
> **Nota de numeración:** este `F-005` es el de la **Fase 2**
> (`spec/phases/phase-02/features.md`). No debe confundirse con
> el `F-005` de Fase 1 (Tipos comunes Result y PagedResult
> en `spec/phases/phase-01/features.md`). Los IDs son locales
> a cada fase; el folder los desambigua. Ver `spec/README.md`
> para la convención.
>
> Fuentes de verdad: `docs/vision.md`, `docs/architecture.md`,
> `docs/roadmap.md` (Fase 2), `spec/constitution/tech-stack.md`,
> `spec/constitution/mission.md`,
> `spec/phases/phase-02/features.md` (F-005 dentro del
> desglose de la fase 2),
> `spec/features/phase-02/F-001-appdbcontext-addpersistence.done.md`
> (provee `AddPersistence` que F-005 enchufa),
> `spec/features/phase-02/F-002-entity-configurations-migration.done.md`
> (generó `InitialSchema` y `SeedSystemData`),
> `spec/features/phase-01/F-007-validacion-arquitectonica.done.md`
> (el test R-4 con filtro `Bootstrap` que permite la
> dependencia `UI → Infrastructure`),
> `spec/features/phase-01/F-008-composition-root.done.md`
> (provee `Bootstrap.ConfigureServices` y `App.axaml.cs`
> que F-005 extiende).

---

## 1. Resumen

F-005 **enchufa la persistencia** que F-001 a F-004
de Phase 2 construyeron en el composition root que
F-008 de Phase 1 dejó listo. Concretamente:
1. `Bootstrap.ConfigureServices` llama a
   `AddPersistence` (registrando `AppDbContext`,
   `IUnitOfWork`, y los 2 repositorios).
2. `UI.csproj` añade `<ProjectReference>` a
   `Infrastructure` (la única dependencia entre
   `UI` e `Infrastructure` que faltaba).
3. `App.axaml.cs` aplica las 2 migraciones
   (`InitialSchema` + `SeedSystemData`) al arrancar
   la app, dentro de un `IServiceScope`.

Al final de la feature:
- La app arranca, aplica migraciones, siembra las
  14 entidades del sistema, y muestra la ventana.
- `dotnet run` y `dotnet ef database update`
  producen el mismo resultado (la DB se crea y
  siembra).
- El archivo `data.db` se crea automáticamente en
  el primer arranque (si no existe).
- El test R-4 de F-007 sigue pasando (la
  dependencia `UI → Infrastructure` pasa por el
  filtro `.That().DoNotHaveName("Bootstrap")`).

---

## 2. Contexto

F-001 de Phase 2 creó `AddPersistence` con un
placeholder explícito: "**NO** registra los 15
repositorios. Eso es F-004." F-004 añadió los 2
repositorios (`CategoryRepository` y
`PaymentMethodRepository`). F-005 los enchufa
todos al composition root.

F-008 de Phase 1 dejó `Bootstrap.ConfigureServices`
con solo `IClock` e `IPathResolver`. F-005 lo
extiende para llamar a `AddPersistence`.

F-002 y F-003 generaron 2 migraciones
(`InitialSchema` y `SeedSystemData`) que materializan
el schema y el seed. F-005 las aplica en runtime.

**Por qué F-005 es la pieza que hace que la app
funcione end-to-end:**

Hasta F-004, la app **podía** construir el host
con DI, pero `Bootstrap` no llamaba a
`AddPersistence`. Los repositorios existían pero
no estaban registrados. Sin F-005, los 14 registros
del sistema (de F-003) están en el snapshot del
modelo pero nunca se aplican a una DB real. La app
arranca, muestra una ventana vacía, y no persiste
nada. F-005 cierra ese ciclo: la app arranca, crea
`data.db`, aplica las 2 migraciones, y queda lista
para que los use cases de Fase 3+ la usen.

**Por qué el filtro `Bootstrap` de R-4 importa:**

F-007 de Phase 1 implementó el test R-4
(`R4_UI_No_Referencia_A_Infrastructure_Salvo_CompositionRoot`)
con el filtro
`.Types.InAssembly(uiAssembly).That().DoNotHaveName("Bootstrap").ShouldNot().HaveDependencyOn(infrastructureAssembly)`.
F-005 introduce la dependencia `UI → Infrastructure`
que R-4 vigila. El filtro excluye el tipo `Bootstrap`
(anotado con `[CompositionRoot]` de F-008 de
Phase 1), por lo que el test sigue pasando. Sin ese
filtro, R-4 fallaría porque la dependencia
existe.

---

## 3. Objetivo

1. **Modificar `UI/Composition/Bootstrap.cs`** para
   llamar a `AddPersistence` después de registrar
   `IClock` e `IPathResolver`.
2. **Modificar `UI/ControlDeFinanzasIA.UI.csproj`**
   para añadir `<ProjectReference>` a
   `Infrastructure`.
3. **Modificar `UI/App.axaml.cs`** para aplicar
   las migraciones al arrancar, antes de crear la
   ventana principal.
4. **NO** añadir auto-backup antes de migraciones
   (queda para F-019).
5. **NO** añadir logging complejo (solo los logs
   que ya están en F-008 de Phase 1).

---

## 4. Alcance

### 4.1 En alcance

- Modificación de
  `src/ControlDeFinanzasIA.UI/Composition/Bootstrap.cs`
  (de F-008 de Phase 1) para llamar a
  `AddPersistence` con el `IPathResolver` resuelto
  desde la colección.
- Modificación de
  `src/ControlDeFinanzasIA.UI/ControlDeFinanzasIA.UI.csproj`
  para añadir `<ProjectReference>` a
  `Infrastructure`.
- Modificación de
  `src/ControlDeFinanzasIA.UI/App.axaml.cs`
  (de F-008 de Phase 1) para aplicar las
  migraciones al arrancar dentro de un
  `IServiceScope`.
- Verificación de que el build y los 111 tests
  existentes siguen pasando.
- Verificación de que el test R-4 de F-007
  (`UI_No_Referencia_A_Infrastructure_Salvo_CompositionRoot`)
  sigue pasando.

### 4.2 Fuera de alcance

- **Auto-backup antes de migraciones destructivas.**
  Queda para F-019 según `docs/roadmap.md`.
- **Migración desde una versión anterior del schema**
  (con auto-backup). F-019.
- **Generación de migraciones en runtime.** Las
  migraciones están commiteadas (de F-002 y F-003);
  F-005 las aplica, no las genera.
- **Logging de queries SQL.** F-005 NO configura
  `LogTo` para EF Core. El logging fino es trabajo
  de F-012+.
- **Tests de integración.** Es F-006 de Phase 2.
  F-005 implementa el wiring; F-006 lo testea.
- **Cambios en `Domain`, `Application`,
  `Infrastructure`, `Tests.Unit`, `Tests.Integration`.**
  F-005 solo toca `UI`.
- **Modificaciones a `Bootstrap.ConfigureServices`
  para registrar use cases o ViewModels.** Esos
  llegan con las features de Fase 3+ (cuando
  existan use cases). F-005 solo añade
  `AddPersistence`.
- **Modificaciones a `App.axaml.cs` para crear
  `MainWindow` desde DI.** Eso llega con la
  feature que introduzca navegación (Fase 3+).
  F-005 mantiene la ventana en código (igual que
  en F-001 y F-008 de Phase 1).
- **Cambio de `DesignTimeAppDbContextFactory`
  (de F-002) a UI como startup project.** Cuando
  F-005 mueva el startup project de
  `Infrastructure` a `UI`, la factory quedará
  obsoleta pero no rompe nada. La doc de F-005
  documenta el cambio de startup project en
  futuras migraciones (los comandos `dotnet ef`
  futuros usarán `--startup-project
  src/ControlDeFinanzasIA.UI`).

---

## 5. Casos de uso

### CU-1 — Primer arranque en una DB limpia

- **Actor:** el usuario (o CI, o un script de
  instalación).
- **Trigger:** ejecuta `dotnet run --project
  src/ControlDeFinanzasIA.UI` (o hace doble click
  en el binario).
- **Precondición:** la DB no existe en
  `%APPDATA%\ControlDeFinanzasIA\data.db`
  (primera instalación).
- **Postcondición:**
  - El directorio `%APPDATA%\ControlDeFinanzasIA\`
    se crea (por `SystemPathResolver`).
  - El archivo `data.db` se crea.
  - La migración `InitialSchema` se aplica (crea
    las 2 tablas y los 2 índices).
  - La migración `SeedSystemData` se aplica
    (inserta los 14 registros del sistema).
  - El log incluye "Migrations applied".
  - La ventana aparece.

### CU-2 — Arranques subsiguientes (DB ya existente)

- **Actor:** el usuario.
- **Trigger:** ejecuta la app después de CU-1.
- **Precondición:** la DB existe con las 2
  migraciones ya aplicadas.
- **Postcondición:**
  - `Database.Migrate()` consulta
    `__EFMigrationsHistory`, ve que las 2
    migraciones ya están aplicadas, y no hace nada.
  - El log NO incluye "Migrations applied"
    (porque no se aplicó ninguna migración nueva).
    Alternativamente, EF Core puede loguear "No
    migrations were applied" (depende de la
    versión de EF Core).
  - La ventana aparece.

### CU-3 — DB borrada manualmente

- **Actor:** el usuario (con acceso a
  `%APPDATA%\ControlDeFinanzasIA\`).
- **Trigger:** borra `data.db` (simula una
  instalación corrupta).
- **Precondición:** el directorio
  `%APPDATA%\ControlDeFinanzasIA\` existe (con
  posiblemente archivos `-wal` y `-shm`).
- **Postcondición:**
  - Al siguiente arranque, `Database.Migrate()`
    detecta que la DB no existe (o no tiene
    `__EFMigrationsHistory`).
  - EF Core crea la DB desde cero y aplica
    `InitialSchema` + `SeedSystemData`.
  - El archivo `-wal` y `-shm` se regeneran.
  - La ventana aparece con los 14 registros del
    sistema.

### CU-4 — Migración falla (DB corrupta)

- **Actor:** el sistema (no el usuario; la app
  falla).
- **Trigger:** el archivo `data.db` está corrupto
  (e.g., el usuario lo editó a mano, o se cerró
  el sistema durante una escritura).
- **Postcondición:**
  - `Database.Migrate()` lanza una excepción
    (e.g., `SqliteException`).
  - El log incluye el error con un stack trace.
  - **La ventana NO se muestra.** La app termina
    con un mensaje claro.
- **Decisión documentada:** datos corruptos son
  peor que app inusable. El usuario debe restaurar
  manualmente (e.g., desde un backup, o borrando
  `data.db`).

### CU-5 — Test R-4 sigue pasando

- **Actor:** `dotnet test`.
- **Trigger:** ejecuta el test
  `R4_UI_No_Referencia_A_Infrastructure_Salvo_CompositionRoot`
  (de F-007).
- **Postcondición:** el test pasa. La
  dependencia `UI → Infrastructure` existe en el
  `.csproj` de UI, pero el filtro
  `.That().DoNotHaveName("Bootstrap")` excluye
  el tipo `Bootstrap` (anotado con
  `[CompositionRoot]`), por lo que NetArchTest no
  reporta violación.

---

## 6. Requisitos funcionales

### RF-1 — `Bootstrap.ConfigureServices` llama a `AddPersistence`

- Modificar
  `src/ControlDeFinanzasIA.UI/Composition/Bootstrap.cs`
  (de F-008 de Phase 1).
- Localizar el método `ConfigureServices`.
- Después de `services.AddSingleton<IPathResolver>(...)`,
  añadir:
  ```text
  IPathResolver pathResolver = services.BuildServiceProvider().GetRequiredService<IPathResolver>();
  services.AddPersistence(pathResolver);
  ```
- **Importante:** `BuildServiceProvider()` se usa
  aquí solo para resolver `IPathResolver` (que ya
  está registrado como Singleton). Es un patrón
  válido en composition root: el host se construye
  fuera de `ConfigureServices` (en `Program.Main`),
  pero aquí necesitamos el `IPathResolver` antes de
  que el host esté listo.
- **Alternativa descartada:** crear un método de
  extensión específico que reciba `IPathResolver`
  como parámetro. Esto cambiaría la firma de
  `ConfigureServices` (que en F-008 de Phase 1
  recibe solo `IServiceCollection, IConfiguration`).
  Para mantener compatibilidad con el patrón de
  F-008, la spec opta por `BuildServiceProvider`
  aquí.
- Actualizar el XML doc del método para mencionar
  que también registra la persistencia.

### RF-2 — `UI.csproj` añade `<ProjectReference>` a `Infrastructure`

- Modificar
  `src/ControlDeFinanzasIA.UI/ControlDeFinanzasIA.UI.csproj`
  (de F-001 de Phase 1).
- Añadir dentro del `<ItemGroup>` apropiado:
  ```xml
  <ProjectReference Include="..\ControlDeFinanzasIA.Infrastructure\ControlDeFinanzasIA.Infrastructure.csproj" />
  ```
- **Importante:** esta es la primera (y única)
  dependencia de `UI` a `Infrastructure`. El test
  R-4 de F-007 lo valida (con la exclusión de
  `Bootstrap`).

### RF-3 — `App.axaml.cs` aplica migraciones al arrancar

- Modificar
  `src/ControlDeFinanzasIA.UI/App.axaml.cs`
  (de F-008 de Phase 1).
- En `OnFrameworkInitializationCompleted`, después
  de resolver `ILogger<App>`, `IClock`, y
  `IPathResolver`, y antes de emitir "App starting"
  (o después, según convenga), añadir:
  ```text
  using (IServiceScope scope = Services.CreateScope())
  {
      AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      db.Database.Migrate();
      _logger.LogInformation("Migrations applied");
  }
  ```
- **Decisión de orden:** el spec recomienda
  aplicar migraciones **después** de emitir "App
  starting" y **antes** de crear la ventana. Esto
  permite que el log de "App starting" aparezca
  incluso si la migración falla (útil para
  debugging).
- **Manejo de excepciones:** si `Database.Migrate()`
  lanza una excepción, NO se captura en F-005.
  La excepción se propaga y la app termina con
  un mensaje claro. F-005 NO muestra un diálogo
  de error (eso requiere un `IExceptionHandler`
  global, que llega en F-012+).
- **Scope disposal:** `using (IServiceScope scope =
  ...)` garantiza que el `AppDbContext` y sus
  dependencias se disposent correctamente.

### RF-4 — `BuildAvaloniaApp` (de F-008) no se modifica

- El método `BuildAvaloniaApp` (de F-008 de
  Phase 1) sigue siendo el entry point de Avalonia.
  F-005 NO lo modifica.
- El wiring de la persistencia ocurre en
  `Bootstrap.ConfigureServices` y en
  `App.axaml.cs`, no en `BuildAvaloniaApp`.

### RF-5 — Compilación y tests

- `dotnet build` desde la raíz: 0 warnings, 0
  errors (Debug y Release).
- `dotnet test`: 111 tests verdes (100 + 11 de
  F-007), 0 failures.
- 11 tests de NetArchTest de F-007 pasan, en
  particular R-4
  (`R4_UI_No_Referencia_A_Infrastructure_Salvo_CompositionRoot`).
- `dotnet format style --verify-no-changes`: exit
  0.

---

## 7. Requisitos no funcionales

### RNF-1 — Compatibilidad con la arquitectura

- Después de F-005, el grafo de dependencias
  incluye una nueva arista: `UI → Infrastructure`
  (a través de `Bootstrap` y `App.axaml.cs`).
- R-4 de F-007 valida esta arista con el filtro
  `.That().DoNotHaveName("Bootstrap")`. El test
  pasa sin modificaciones.
- Las demás reglas (R-1, R-2, R-3, R-5) siguen
  pasando.

### RNF-2 — Determinismo

- Las migraciones son deterministas (EF Core
  genera el mismo SQL para el mismo schema).
- La idempotencia de las migraciones se basa en
  `__EFMigrationsHistory` (de F-002 y F-003).

### RNF-3 — Manejo de errores

- Si una migración falla, la app termina con
  un mensaje claro. F-005 NO captura la
  excepción globalmente; deja que la app
  termine.
- Esto es coherente con la decisión documentada
  en `docs/roadmap.md` y la spec de F-002: "datos
  corruptos son peor que app inusable".

### RNF-4 — Sin scope creep

- F-005 NO añade use cases, ViewModels, o
  navegación.
- F-005 NO añade un `IExceptionHandler` global.
- F-005 NO añade auto-backup (queda para F-019).
- F-005 NO añade `DesignTimeAppDbContextFactory`
  (ya existe de F-002).

---

## 8. Reglas de negocio

### RN-1 — Migraciones se aplican al arrancar, no al instalar

- La app aplica las migraciones en
  `OnFrameworkInitializationCompleted` (de F-005).
- Esto significa que **no** hay un instalador que
  ejecute las migraciones. La app se auto-instala
  en el primer arranque.
- **Razón:** simplifica el instalador (no es un
  paso separado) y garantiza que la DB siempre
  esté sincronizada con la versión de la app.
- **Limitación:** si la app se inicia con una DB
  corrupta, falla con un error (CU-4). El usuario
  debe restaurar manualmente.

### RN-2 — Migraciones destructivas sin auto-backup

- Si una migración nueva de Phase 3+ (o
  posterior) rompe la DB existente, F-005 no
  hace backup automático. F-019 introduce
  auto-backup antes de migraciones destructivas.
- **Razón:** scope discipline. F-005 es la
  primera incursión de runtime de la app; el
  auto-backup es trabajo de una feature
  posterior con su propia spec.
- **Riesgo aceptado:** la primera migración
  destructiva (probablemente en Fase 3 o
  posterior) requerirá un backup manual del
  usuario. Se documenta en el commit / PR de
  esa feature.

### RN-3 — El filtro `Bootstrap` de R-4 es la base de la arquitectura

- La decisión arquitectónica clave de F-008
  de Phase 1 fue: "la única dependencia entre
  `UI` e `Infrastructure` pasa por el composition
  root, y el test R-4 la filtra por nombre".
- F-005 es la **primera feature** que ejerce
  esta decisión: añade la dependencia que R-4
  vigila. Si el filtro funciona (como diseñó
  F-007), F-005 pasa sin romper nada.
- Si el filtro NO funciona, R-4 falla. F-005
  tiene que arreglar esto sin modificar R-4 (que
  es un test de Phase 1 cerrado). La forma
  correcta es ajustar el nombre o el patrón
  en `Bootstrap` y en `App.axaml.cs` (no en el
  test).

---

## 9. Modelo de dominio

F-005 **NO** introduce nuevas entidades. Solo
modifica la composición de las existentes:

```
UI/
├── Composition/
│   ├── Bootstrap.cs                              ← F-005 (modificado: llama a AddPersistence)
│   └── CompositionRootAttribute.cs                ← F-008 de Phase 1 (sin cambios)
├── App.axaml.cs                                  ← F-005 (modificado: aplica migraciones)
├── Program.cs                                    ← F-008 de Phase 1 (sin cambios)
└── ControlDeFinanzasIA.UI.csproj                  ← F-005 (modificado: ProjectReference a Infrastructure)
```

**`Bootstrap.cs` (modificado):**
- Mismo método estático con `[CompositionRoot]`.
- `ConfigureServices(IServiceCollection services,
  IConfiguration configuration)` añade la llamada
  a `AddPersistence(pathResolver)` después de
  registrar `IPathResolver`.

**`App.axaml.cs` (modificado):**
- `OnFrameworkInitializationCompleted` añade
  el bloque `using (IServiceScope scope = ...)`
  con `db.Database.Migrate()` antes de crear la
  ventana.

**`UI.csproj` (modificado):**
- Un `<ItemGroup>` adicional con
  `<ProjectReference Include="..\ControlDeFinanzasIA.Infrastructure\..." />`.

---

## 10. Configuración técnica

### 10.1 — `Bootstrap.ConfigureServices`

- Después de `services.AddSingleton<IPathResolver>(pathResolver)`,
  añadir (en orden):
  ```text
  services.AddPersistence(pathResolver);
  ```
- `pathResolver` se resuelve con
  `services.BuildServiceProvider().GetRequiredService<IPathResolver>()`.
- **Importante:** `BuildServiceProvider()` se usa
  ANTES de que el host esté construido. Esto es
  válido porque `IPathResolver` es un Singleton
  (sin estado mutable) y solo expone rutas.

### 10.2 — `App.axaml.cs` y migraciones

- En `OnFrameworkInitializationCompleted`, después
  de resolver los servicios actuales, añadir:
  ```text
  using (IServiceScope scope = Services.CreateScope())
  {
      AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      db.Database.Migrate();
      _logger.LogInformation("Migrations applied");
  }
  ```
- **Decisión de orden:** aplicar migraciones
  DESPUÉS de emitir "App starting" (para que el
  log aparezca incluso si la migración falla).
  Esto es útil para debugging.
- **Manejo de excepciones:** si `Database.Migrate()`
  falla, la excepción se propaga. La app termina
  con un mensaje claro.

### 10.3 — `UI.csproj`

- Añadir el `<ProjectReference>` a `Infrastructure`
  en el `<ItemGroup>` apropiado (después de los
  ProjectReference a `Application` y `Domain`).
- Sin `Private` ni `IncludeAssets` (default).
- La versión no se especifica (la dependencia
  resuelve automáticamente).

### 10.4 — Convenciones de código

- File-scoped namespace (proyecto rule).
- `using` directives al tope.
- XML doc actualizado en los métodos
  modificados.
- Sin nuevos `using` redundantes.

---

## 11. Validaciones

| # | Validación | Cuándo se aplica | Comportamiento esperado |
| --- | --- | --- | --- |
| V-1 | `IPathResolver` se resuelve correctamente antes de `AddPersistence`. | Al llamar a `AddPersistence` en `ConfigureServices`. | Sin excepciones; `pathResolver` tiene valores válidos. |
| V-2 | `AppDbContext` se resuelve desde el scope. | Al ejecutar `scope.ServiceProvider.GetRequiredService<AppDbContext>()`. | Sin excepciones; el contexto se construye correctamente. |
| V-3 | `Database.Migrate()` aplica las 2 migraciones. | Al ejecutar `Migrate()`. | Sin excepciones en DB limpia; las tablas y datos se crean. |
| V-4 | El test R-4 sigue pasando después de añadir el ProjectReference. | Después de `dotnet build`. | El test pasa porque el filtro `Bootstrap` excluye correctamente. |
| V-5 | `dotnet build` y `dotnet test` pasan. | Después de la implementación. | 0 warnings, 0 errors; 111 tests verdes. |

---

## 12. Casos borde

### EC-1 — DB parcialmente inicializada (solo `__EFMigrationsHistory`)

- Si la DB tiene solo la tabla
  `__EFMigrationsHistory` (sin las 2 migraciones
  aplicadas), `Database.Migrate()` aplica las
  migraciones pendientes.
- **Mitigación:** EF Core maneja este caso
  automáticamente.

### EC-2 — DB con una migración parcial

- Si una migración anterior se aplicó
  parcialmente (e.g., se creó la tabla
  `Categories` pero no la tabla `PaymentMethods`
  porque el proceso fue interrumpido), la siguiente
  ejecución de `Database.Migrate()` continúa
  desde donde se quedó.
- **Limitación:** si el estado parcial es
  inconsistente (e.g., la tabla `Categories` existe
  pero con columnas faltantes), la siguiente
  migración puede fallar. F-019 (auto-backup)
  mitiga esto.
- **F-005 no aborda este caso**; es scope de
  F-019.

### EC-3 — Permisos del directorio de datos

- `SystemPathResolver` (de F-008 de Phase 1)
  crea el directorio en su constructor y captura
  `UnauthorizedAccessException`. Si no puede
  crearlo, la app no arranca (capturado en F-008).
- **F-005 no cambia este comportamiento.** Si
  el directorio no existe cuando F-005 intenta
  acceder a `data.db`, `SystemPathResolver` ya
  falló antes.

### EC-4 — Migración aplicada, pero `data.db` está corrupto

- Si el archivo `data.db` está corrupto (header
  inválido), `Database.Migrate()` lanza
  `SqliteException`.
- F-005 NO captura la excepción. La app termina.
- **Mitigación:** el usuario debe restaurar
  manualmente (borrar `data.db` o restaurar un
  backup, cuando F-019 exista).

### EC-5 — Cambios futuros en migraciones (después de F-005)

- Cuando se añadan migraciones en Phase 3+, el
  comando `dotnet ef migrations add` cambiará de
  `--startup-project src/ControlDeFinanzasIA.Infrastructure`
  (el actual, de F-002) a
  `--startup-project src/ControlDeFinanzasIA.UI`.
- **Razón:** con F-005, el composition root
  está en UI. EF Core usa el startup project para
  inicializar el host (que ahora vive en UI).
- F-005 documenta este cambio para features
  futuras. La primera feature que añada una
  migración (probablemente en Fase 3) debe usar
  `UI` como startup project.

---

## 13. Dependencias

### 13.1 Prerrequisitos de implementación

- **F-001 de Fase 2** (completada): provee
  `AddPersistence` con la firma
  `AddPersistence(IServiceCollection,
  IPathResolver)`. F-005 la llama.
- **F-002 de Fase 2** (completada): generó
  `InitialSchema` y `SeedSystemData` con sus 3
  archivos cada una.
- **F-003 de Fase 2** (completada): rellenó el
  `SeedData` con las 14 `HasData` calls. Las
  migraciones están listas para aplicar.
- **F-004 de Fase 2** (completada): implementó
  `CategoryRepository` y `PaymentMethodRepository`,
  y los registró en `AddPersistence`.
- **F-008 de Phase 1** (completada): provee
  `Bootstrap.ConfigureServices` y `App.axaml.cs`
  que F-005 extiende. El filtro R-4 (de F-007)
  permite la dependencia `UI → Infrastructure` a
  través de `Bootstrap`.

### 13.2 Dependencias con otras features

| Feature | Relación |
| --- | --- |
| F-001 (Phase 2) | Provee `AddPersistence`. F-005 la llama. |
| F-002 (Phase 2) | Generó las 2 migraciones. F-005 las aplica. |
| F-003 (Phase 2) | Sembró las 14 entidades. F-005 hace que se inserten en runtime. |
| F-004 (Phase 2) | Implementó los 2 repos. F-005 los registra en DI. |
| F-006 (Phase 2) | Testeará el flujo end-to-end que F-005 habilita. |
| F-007 (Phase 1) | El test R-4 verifica que F-005 no rompe la arquitectura. |

### 13.3 Bloqueos

- **Ninguno.** F-005 puede implementarse ahora
  mismo. Los prerrequisitos (F-001 a F-004) están
  cumplidos.

---

## 14. Fuera de alcance (detallado)

- **Auto-backup antes de migraciones destructivas**
  (F-019).
- **Generación de migraciones en runtime** (no
  aplica; las migraciones están commiteadas).
- **Migración desde una versión anterior con
  auto-backup** (F-019).
- **Logging de queries SQL** (F-012+).
- **`IExceptionHandler` global** (F-012+).
- **Cambios en `Domain`, `Application`,
  `Infrastructure`, `Tests.Unit`, `Tests.Integration`**
  (F-005 solo toca `UI`).
- **El CI workflow.** El workflow existente ya
  corre `dotnet build` + `dotnet test`; F-005 no
  requiere cambios.

---

## 15. Criterios de aceptación

La feature se considera completada cuando
**todos** los siguientes puntos se cumplen.

### AC-1 — `Bootstrap.ConfigureServices` llama a `AddPersistence`

- [ ] `src/ControlDeFinanzasIA.UI/Composition/Bootstrap.cs`
      modificado.
- [ ] Después de `services.AddSingleton<IPathResolver>(...)`,
      hay una llamada a
      `services.AddPersistence(pathResolver)`.
- [ ] `pathResolver` se resuelve con
      `services.BuildServiceProvider().GetRequiredService<IPathResolver>()`.
- [ ] XML doc del método actualizado.

### AC-2 — `UI.csproj` añade `<ProjectReference>` a `Infrastructure`

- [ ] `src/ControlDeFinanzasIA.UI/ControlDeFinanzasIA.UI.csproj`
      modificado.
- [ ] Tiene un
      `<ProjectReference Include="..\ControlDeFinanzasIA.Infrastructure\ControlDeFinanzasIA.Infrastructure.csproj" />`.
- [ ] El archivo compila con el ProjectReference.

### AC-3 — `App.axaml.cs` aplica migraciones al arrancar

- [ ] `src/ControlDeFinanzasIA.UI/App.axaml.cs` modificado.
- [ ] `OnFrameworkInitializationCompleted` tiene
      un bloque `using (IServiceScope scope = ...)`
      que resuelve `AppDbContext` y llama a
      `db.Database.Migrate()`.
- [ ] Después de la migración, se emite el log
      "Migrations applied".
- [ ] Si la migración falla, la app termina con
      un mensaje claro (no se muestra la ventana).

### AC-4 — Arranque end-to-end

- [ ] `dotnet build -c Release` desde la raíz: 0
      warnings, 0 errors.
- [ ] `dotnet run --project
      src/ControlDeFinanzasIA.UI` desde la raíz:
      - La ventana aparece.
      - El log incluye "App starting", "Migrations
        applied", "App stopping".
- [ ] En una DB limpia (sin `data.db`):
      - El archivo `data.db` se crea en
        `%APPDATA%\ControlDeFinanzasIA\`.
      - Las 2 migraciones se aplican.
      - Las 9 categorías y 5 métodos de pago del
        sistema están presentes.

### AC-5 — Compilación y tests

- [ ] `dotnet test` desde la raíz: 111 tests
      verdes (100 + 11 de F-007), 0 failures.
- [ ] El test R-4 de F-007
      (`R4_UI_No_Referencia_A_Infrastructure_Salvo_CompositionRoot`)
      pasa **sin modificaciones**.
- [ ] `dotnet format style --verify-no-changes`: exit
      0.

### AC-6 — Sin scope creep

- [ ] NO se ha añadido auto-backup (F-019).
- [ ] NO se ha añadido `IExceptionHandler` global
      (F-012+).
- [ ] NO se ha modificado `Domain`, `Application`,
      `Infrastructure`, `Tests.Unit`,
      `Tests.Integration`.
- [ ] NO se ha añadido `DesignTimeAppDbContextFactory`
      nuevo (la de F-002 sigue siendo válida).
- [ ] NO se ha generado ninguna migración nueva.
- [ ] NO se ha añadido `BuildAvaloniaApp` (de F-008).

### AC-7 — Documentación y trazabilidad

- [ ] `spec/phases/phase-02/features.md`
      actualizado: F-005 marcada como ✅
      COMPLETADA en la tabla resumen y en el
      header de la sección.
- [ ] La spec se renombra a
      `spec/features/phase-02/F-005-composition-root-persistence.done.md`.

---

## 16. Estructura de archivos esperada

```
src/
└── ControlDeFinanzasIA.UI/
    ├── Composition/                             (existente de F-008 de Phase 1)
    │   ├── Bootstrap.cs                         ← F-005 (modificado: llama AddPersistence)
    │   └── CompositionRootAttribute.cs          (F-008, sin cambios)
    ├── System/                                  (F-008 de Phase 1, sin cambios)
    │   ├── SystemClock.cs
    │   └── SystemPathResolver.cs
    ├── App.axaml                                (F-008, sin cambios)
    ├── App.axaml.cs                             ← F-005 (modificado: aplica migraciones)
    ├── Program.cs                                (F-008, sin cambios)
    ├── app.manifest                             (F-001, sin cambios)
    └── ControlDeFinanzasIA.UI.csproj            ← F-005 (modificado: ProjectReference a Infrastructure)
```

**Modificaciones al CI workflow:** ninguna.

---

## 17. Decisiones de diseño explícitas

### 17.1 — `BuildServiceProvider()` en `Bootstrap.ConfigureServices`

**Decisión:** usar
`services.BuildServiceProvider().GetRequiredService<IPathResolver>()`
para resolver el `IPathResolver` y pasarlo a
`AddPersistence`.

**Justificación:**
- `IPathResolver` ya está registrado como
  Singleton por F-008 de Phase 1. Resolverlo
  en `ConfigureServices` (antes de que el host
  esté construido) es un patrón válido en
  composition root.
- El `IPathResolver` es un Singleton sin
  estado mutable, así que es seguro construir un
  ServiceProvider temporal aquí.

**Alternativas descartadas:**
- **Modificar la firma de `ConfigureServices`**
  para que reciba `IPathResolver` como
  parámetro. Esto cambiaría el patrón de F-008
  de Phase 1. La spec opta por mantener la
  firma actual.
- **Pasar `IPathResolver` como parámetro del
  método estático `ConfigureServices`** vía un
  builder pattern. Demasiado complejo para
  v1.

### 17.2 — Orden de migraciones (después de "App starting")

**Decisión:** aplicar migraciones DESPUÉS de
emitir "App starting" y ANTES de crear la
ventana.

**Justificación:**
- El log de "App starting" aparece incluso si
  la migración falla. Esto es útil para
  debugging (el usuario ve que la app
  arrancó, pero la migración falló).
- Si se aplicaran migraciones ANTES de "App
  starting", un fallo de migración impediría
  ver el log.

### 17.3 — NO captura de excepciones en `Migrate()`

**Decisión:** si `Database.Migrate()` falla, la
excepción se propaga. La app termina con un
mensaje claro (de EF Core o del sistema).

**Justificación:**
- "Datos corruptos son peor que app inusable"
  (de la spec de F-002 y del `docs/roadmap.md`).
- Capturar la excepción globalmente requeriría
  un `IExceptionHandler` (F-012+).
- El log de la excepción es lo suficientemente
  claro para que el usuario diagnostique.

### 17.4 — `using (IServiceScope scope = ...)` para el `AppDbContext`

**Decisión:** usar un `IServiceScope` para
resolver y disposer el `AppDbContext`.

**Justificación:**
- `AppDbContext` es Scoped (registrado por
  F-001). Un scope nuevo garantiza que se
  cree y se dispose correctamente.
- El `using` block garantiza la disposición
  incluso si `Migrate()` lanza una excepción.
- El scope se crea y se destruye dentro del
  `OnFrameworkInitializationCompleted`, sin
  afectar el `AppDbContext` que el `App`
  pudiera usar después (no hay tal uso en F-005).

### 17.5 — Cambio de `--startup-project` para migraciones futuras

**Decisión:** el startup project de
`dotnet ef` cambia de
`src/ControlDeFinanzasIA.Infrastructure` (F-002) a
`src/ControlDeFinanzasIA.UI` (a partir de F-005).

**Justificación:**
- Con F-005, el composition root está en
  `UI/Program.cs`. EF Core usa el startup
  project para inicializar el host que carga
  `AppDbContext` con DI.
- `Infrastructure` ya no tiene `Program.cs`
  (es una class library). El `DesignTimeAppDbContextFactory`
  de F-002 sigue siendo válido para generar
  migraciones desde `Infrastructure` como
  startup project (es el patrón estándar de
  EF Core). Pero cuando se use `dotnet ef
  migrations add` en una feature futura, el
  comando será:
  `dotnet ef migrations add ... --project src/ControlDeFinanzasIA.Infrastructure --startup-project src/ControlDeFinanzasIA.UI`.
- La spec documenta este cambio para features
  futuras (probablemente en Fase 3+).

---

## 18. Riesgos y mitigaciones

| Riesgo | Probabilidad | Impacto | Mitigación |
| --- | --- | --- | --- |
| El test R-4 de F-007 falla porque la dependencia `UI → Infrastructure` rompe la regla. | Baja | Alto | AC-5 verifica R-4 explícitamente. Si falla, el filtro `Bootstrap` está mal configurado; revisar `ArchitectureTests.cs` y la firma del filtro. |
| `Database.Migrate()` falla en una DB existente con schema anterior. | Baja | Alto (app no arranca) | El usuario debe restaurar manualmente (borrar `data.db` o restaurar un backup). F-019 introduce auto-backup. |
| El cambio de `--startup-project` a `UI` rompe una futura generación de migraciones. | Baja (futuro) | Medio | La spec §17.5 documenta el cambio. Las features futuras deben usar `UI` como startup project. |
| `Bootstrap.ConfigureServices` se llama múltiples veces (e.g., en tests de integración). | Media | Bajo | `services.BuildServiceProvider()` se llama en cada invocación. Esto crea un `ServiceProvider` temporal que se debe disposer (con `using` o explícitamente). Si no se dispone, hay un memory leak. **Mitigación:** en F-005, `Bootstrap.ConfigureServices` se llama una vez al arrancar la app; no hay memory leak en producción. En F-006 (tests de integración), el patrón es diferente (se usa un `ServiceProvider` dedicado). |
| `IServiceScope` no se dispone correctamente. | Baja | Bajo (memory leak) | El `using` block garantiza la disposición. |
| El test R-1 de F-007 falla porque el `AppDbContext` se importa en `Application`. | Muy baja (F-005 no toca `Application`) | Alto | T-005 del tasks file verifica R-1. |
| `dotnet format style` propone cambios en los archivos modificados. | Baja | Bajo | Aplicar los cambios. |
| El usuario ejecuta `dotnet ef database update` mientras la app está corriendo. | Baja | Bajo (concurrencia SQLite) | SQLite en modo archivo no soporta concurrencia. Si el usuario lo hace, una de las dos operaciones falla. Se documenta en EC-3 de F-002 (single-proceso). |

---

## 19. Referencias

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
  — `Bootstrap.ConfigureServices` y `App.axaml.cs`
  que F-005 extiende.
- `spec/README.md` — convención del directorio
  `spec/`.
- EF Core 8 documentation oficial (a consultar vía
  Context7 cuando se implemente).
