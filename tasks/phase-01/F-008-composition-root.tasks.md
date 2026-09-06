# Tareas — F-008: Composition root con host de DI y logging

> **Spec:** `spec/features/phase-01/F-008-composition-root.md`
> **Fuentes de verdad:** la spec, `docs/architecture.md` §3, §4,
> §9, §12, §13, §14, `docs/roadmap.md` §Fase 1, F-008 dentro del
> desglose, `spec/constitution/tech-stack.md` (paquetes
> Microsoft.Extensions, ruta por SO),
> `spec/constitution/mission.md` (Clean Architecture),
> `spec/phases/phase-01/features.md` (F-008 dentro del desglose
> de la fase 1),
> `spec/features/phase-01/F-001-skeleton-solution.done.md`
> (provee `UI/Program.cs`, `UI/App.axaml.cs`, y los paquetes
> Microsoft.Extensions pre-instalados),
> `spec/features/phase-01/F-004-Abstracciones-clock-pathresolver.done.md`
> (provee `IClock` e `IPathResolver`),
> `spec/features/phase-01/F-007-validacion-arquitectonica.done.md`
> (provee el test R-4 con el filtro `Bootstrap` listo para
> F-008).
>
> Cada tarea es implementable en menos de 2 horas. El proyecto
> debe seguir compilando al finalizar cada una. La numeración
> (T-001...) es local a esta feature. No se escribe código de
> `Domain`, `Application` ni `Infrastructure` en esta feature;
> solo se añaden archivos en `UI/Composition/`, `UI/System/`,
> y se modifican `UI/Program.cs` y `UI/App.axaml.cs`.

---

## Resumen de tareas

| ID | Tarea | Estimación | Depende de |
| --- | --- | --- | --- |
| T-001 | Verificar / añadir `Microsoft.Extensions.Configuration.Json` en CPM y `UI.csproj` | XS | — |
| T-002 | Crear `CompositionRootAttribute` en `UI/Composition/` | XS | — |
| T-003 | Crear `SystemClock` (implementación de `IClock`) en `UI/System/` | XS | — |
| T-004 | Crear `SystemPathResolver` (implementación de `IPathResolver`) en `UI/System/` | S | T-001 |
| T-005 | Crear `Bootstrap` (composition root) en `UI/Composition/` | S | T-002, T-003, T-004 |
| T-006 | Reescribir `UI/Program.cs` para arrancar con `IHost` + `Bootstrap` | M | T-005, T-001 |
| T-007 | Modificar `UI/App.axaml.cs` para consumir `IServiceProvider` y emitir logs | M | T-006 |
| T-008 | Verificar que el test R-4 de F-007 sigue pasando y que los servicios se resuelven | S | T-007 |
| T-009 | Verificación con arranque real de la app, format check y cierre de la feature | M | T-001 a T-008 |

**Total estimado:** 1–2 jornadas. T-007 y T-009 son las más
largas porque T-007 toca la lógica de arranque y T-009 incluye
verificación manual de la app corriendo. T-001 a T-005 son
triviales (archivos pequeños, lógica mecánica). T-006 conecta
todo.

---

## T-001 — Verificar / añadir `Microsoft.Extensions.Configuration.Json` en CPM y `UI.csproj`

**Objetivo**
Confirmar que el paquete `Microsoft.Extensions.Configuration.Json`
está disponible para `UI` (necesario para `AddJsonFile` en
T-006) y, si no, añadirlo correctamente vía CPM (Central
Package Management) para que `Program.cs` pueda cargar
`appsettings.json`.

**Descripción**
La spec de F-001 §5 lista varios paquetes de
`Microsoft.Extensions.*` pre-instalados en `UI`, pero **no
menciona explícitamente** `Microsoft.Extensions.Configuration.Json`.
Verificar:

1. Abrir `Directory.Packages.props` y comprobar si hay un
   `<PackageVersion Include="Microsoft.Extensions.Configuration.Json" Version="..." />`.
2. Si no está, añadir la versión (última estable compatible
   con .NET 8; consultar documentación oficial vía Context7).
3. Abrir
   `src/ControlDeFinanzasIA.UI/ControlDeFinanzasIA.UI.csproj`
   y comprobar si hay un
   `<PackageReference Include="Microsoft.Extensions.Configuration.Json" />`.
4. Si no está, añadirlo (sin `Version`, vía CPM).
5. Ejecutar `dotnet restore` desde la raíz y verificar que
   el paquete se restaura sin errores.

**Dependencias**
- F-001 completada (la solución, los proyectos y la
  configuración de CPM ya están listos).

**Archivos / proyectos afectados**
- `Directory.Packages.props` (modificado solo si falta el
  `PackageVersion`).
- `src/ControlDeFinanzasIA.UI/ControlDeFinanzasIA.UI.csproj`
  (modificado solo si falta el `PackageReference`).
- `src/ControlDeFinanzasIA.UI/obj/project.assets.json`
  (regenerado por `dotnet restore`).

**Criterios de finalización**
- `Directory.Packages.props` contiene un
  `<PackageVersion Include="Microsoft.Extensions.Configuration.Json" Version="X.Y.Z" />`
  con versión exacta (sin rangos).
- `UI.csproj` contiene un
  `<PackageReference Include="Microsoft.Extensions.Configuration.Json" />`
  sin atributo `Version`.
- `dotnet restore` desde la raíz pasa sin errores.
- `dotnet list src/ControlDeFinanzasIA.UI package --include-transitive`
  muestra `Microsoft.Extensions.Configuration.Json` con la
  versión esperada.
- Si ya estaba todo, no se hace ningún cambio (no-op).

---

## T-002 — Crear `CompositionRootAttribute` en `UI/Composition/`

**Objetivo**
Implementar el atributo marker `[CompositionRoot]` documentado
en F-007 §10.1 y referenciado en la spec F-008 §RF-3 y §RN-3.
Es un marker puro, sin state, que sirve para documentar
convención y para futuros tests o herramientas que quieran
inspeccionar composition roots por atributo en vez de por
nombre de tipo.

**Descripción**
Crear el archivo
`src/ControlDeFinanzasIA.UI/Composition/CompositionRootAttribute.cs`
con:

- File-scoped namespace:
  `namespace ControlDeFinanzasIA.UI.Composition;`.
- `using System;` para `Attribute`.
- `[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]`.
- `public sealed class CompositionRootAttribute : Attribute`.
- XML doc en la clase explicando:
  - Qué marca (composition root del proyecto).
  - Por qué existe (convención, no es funcionalmente
    necesario para F-008; el test R-4 de F-007 usa el
    nombre `Bootstrap` directamente, no el atributo).
  - Quién lo usa (la clase `Bootstrap` creada en T-005).
- Constructor público sin parámetros (puede ser explícito o
  implícito).

**Dependencias**
- Ninguna (el archivo no usa nada externo más allá de
  `System`).

**Archivos / proyectos afectados**
- `src/ControlDeFinanzasIA.UI/Composition/CompositionRootAttribute.cs`
  (nuevo).

**Criterios de finalización**
- El archivo existe y compila.
- `public sealed class CompositionRootAttribute : Attribute`.
- `[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]`
  está presente.
- File-scoped namespace.
- `using` al tope del archivo.
- XML doc en la clase cita el propósito del atributo.
- `dotnet build` desde la raíz: 0 warnings, 0 errors.

---

## T-003 — Crear `SystemClock` (implementación de `IClock`) en `UI/System/`

**Objetivo**
Implementar la versión de runtime de `IClock` que devuelve
la hora actual del sistema. Esta es la implementación
trivial que `Bootstrap` registra en T-005.

**Descripción**
Crear el archivo
`src/ControlDeFinanzasIA.UI/System/SystemClock.cs` con:

- File-scoped namespace:
  `namespace ControlDeFinanzasIA.UI.System;`.
- `using System;` para `DateTime` y `DateOnly`.
- `using ControlDeFinanzasIA.Application.Abstractions;` para
  `IClock`.
- `public sealed class SystemClock : IClock`.
- `public DateTime UtcNow => DateTime.UtcNow;`.
- `public DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);`.
- XML doc en la clase y en cada propiedad explicando:
  - Que es la implementación de runtime de `IClock`.
  - Por qué vive en `UI/System/` (RN-2 de la spec: es
    infraestructura de runtime trivial, no de `Application`
    ni de `Infrastructure`).
  - Que `Today` se calcula desde `UtcNow` (no usa
    `DateTime.Now` para evitar inconsistencias de zona
    horaria en futuras features).

**Dependencias**
- F-004 completada (`IClock` existe en
  `Application/Abstractions/`).

**Archivos / proyectos afectados**
- `src/ControlDeFinanzasIA.UI/System/SystemClock.cs` (nuevo).

**Criterios de finalización**
- El archivo existe y compila.
- `public sealed class SystemClock : IClock`.
- Las dos propiedades están implementadas como
  expression-bodied members.
- File-scoped namespace.
- `using` al tope.
- XML doc en la clase y en cada propiedad.
- `dotnet build`: 0 warnings, 0 errors.

---

## T-004 — Crear `SystemPathResolver` (implementación de `IPathResolver`) en `UI/System/`

**Objetivo**
Implementar la versión de runtime de `IPathResolver` que
resuelve la ruta del directorio de datos según el SO,
siguiendo `spec/constitution/tech-stack.md` y `docs/architecture.md` §12.
Esta es la implementación trivial que `Bootstrap` registra
en T-005. El constructor crea el directorio si no existe
(excepción documentada en RN-4 de la spec).

**Descripción**
Crear el archivo
`src/ControlDeFinanzasIA.UI/System/SystemPathResolver.cs` con:

- File-scoped namespace:
  `namespace ControlDeFinanzasIA.UI.System;`.
- `using` directives necesarias al tope:
  - `using System;`
  - `using System.IO;` para `Path`, `Directory`, etc.
  - `using ControlDeFinanzasIA.Application.Abstractions;`
    para `IPathResolver`.
- `public sealed class SystemPathResolver : IPathResolver`.
- **Detección de SO y resolución de `DataDirectory`:**
  - Si `RuntimeInformation.IsOSPlatform(OSPlatform.Windows)`:
    - `DataDirectory` =
      `Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ControlDeFinanzasIA")`.
  - Si `RuntimeInformation.IsOSPlatform(OSPlatform.OSX)`:
    - `DataDirectory` =
      `Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "ControlDeFinanzasIA")`.
  - Si no (Linux u otros):
    - `DataDirectory` = si `XDG_CONFIG_HOME` está
      definida y no vacía: `Path.Combine(XDG_CONFIG_HOME,
      "ControlDeFinanzasIA")`. Si no:
      `Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "ControlDeFinanzasIA")`.
  - (El bloque de OS detection puede usar un
    `switch` con `RuntimeInformation.IsOSPlatform(...)` o
    un `if/else` con `OperatingSystem.IsWindows()` /
    `OperatingSystem.IsMacOS()` / `OperatingSystem.IsLinux()`.
    La spec deja libertad de implementación; cualquier
    enfoque que cubra los 3 casos es aceptable.)
- `DatabaseFilePath => Path.Combine(DataDirectory, "data.db");`.
- **Creación del directorio en el constructor:**
  - En el constructor (público sin parámetros), llamar a
    `Directory.CreateDirectory(_dataDirectory);` para
    garantizar que el directorio existe antes de que se
    intente crear el archivo SQLite en F-002 de Fase 2.
  - Capturar `UnauthorizedAccessException` e `IOException`
    y loguear (con `Console.Error.WriteLine` o equivalente)
    antes de propagar la excepción. Esto es para que el
    mensaje sea visible en la consola si la app falla al
    arrancar.
  - La creación del directorio se hace en un bloque
    `try/catch` que **loguea pero no suprime** la
    excepción. La app no puede arrancar sin directorio
    de datos.
- XML doc en la clase, en cada propiedad, y en el
  constructor, explicando:
  - Que es la implementación de runtime de `IPathResolver`.
  - Por qué vive en `UI/System/` (RN-2 de la spec).
  - La convención de ruta por SO
    (`spec/constitution/tech-stack.md`).
  - La excepción documentada: el constructor hace I/O
    (crea el directorio) y por qué (RN-4 de la spec).

**Dependencias**
- F-004 completada (`IPathResolver` existe en
  `Application/Abstractions/`).
- T-001 (`Microsoft.Extensions.Configuration.Json` no es
  estrictamente necesario para `SystemPathResolver`, pero
  se lista aquí porque T-001 lo verifica; el resto de
  T-001 aplica al proyecto en general).

**Archivos / proyectos afectados**
- `src/ControlDeFinanzasIA.UI/System/SystemPathResolver.cs`
  (nuevo).

**Criterios de finalización**
- El archivo existe y compila.
- `public sealed class SystemPathResolver : IPathResolver`.
- Constructor público sin parámetros que crea el directorio.
- `DataDirectory` resuelve según el SO (Windows / macOS /
  Linux).
- `DatabaseFilePath` =
  `Path.Combine(DataDirectory, "data.db")`.
- File-scoped namespace.
- `using` al tope.
- XML doc en la clase, en cada propiedad y en el
  constructor, citando RN-2 y RN-4 de la spec.
- `dotnet build`: 0 warnings, 0 errors.

---

## T-005 — Crear `Bootstrap` (composition root) en `UI/Composition/`

**Objetivo**
Implementar la clase `Bootstrap` que centraliza la
registración de servicios en el `IServiceCollection`.
Esta es la pieza que hace que la arquitectura pase de
"declarada" a "ejercitada en runtime". Va anotada con
`[CompositionRoot]` (T-002) y es la única excepción a
la regla R-4 de F-007 (UI no debe referenciar a
Infrastructure).

**Descripción**
Crear el archivo
`src/ControlDeFinanzasIA.UI/Composition/Bootstrap.cs` con:

- File-scoped namespace:
  `namespace ControlDeFinanzasIA.UI.Composition;`.
- `using` directives necesarias al tope:
  - `using ControlDeFinanzasIA.Application.Abstractions;`
    para `IClock` e `IPathResolver`.
  - `using ControlDeFinanzasIA.UI.System;` para
    `SystemClock` y `SystemPathResolver`.
  - `using Microsoft.Extensions.Configuration;` para
    `IConfiguration`.
  - `using Microsoft.Extensions.DependencyInjection;`
    para `IServiceCollection`.
- `[CompositionRoot]` aplicada a la clase.
- `public static class Bootstrap`.
- Método estático público:
  `public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)`.
- Cuerpo del método:
  - `services.AddSingleton<IClock, SystemClock>();`
  - `services.AddSingleton<IPathResolver, SystemPathResolver>();`
  - (No se registran las 15 interfaces de repositorio.
    Eso es F-002 de Fase 2. F-008 solo registra
    `IClock`, `IPathResolver` e `ILogger<T>`. `ILogger<T>`
    y `IConfiguration` ya vienen del host; no se
    registran explícitamente.)
  - (El parámetro `configuration` se acepta pero no se usa
    en F-008. Se mantiene en la firma para que F-002 y
    F-012 no tengan que cambiar la firma. Se documenta
    con un `_ = configuration;` o un comentario XML
    que explique que se reserva para uso futuro.)
- XML doc en la clase y en el método explicando:
  - Que es el composition root del proyecto.
  - Por qué está anotada con `[CompositionRoot]`.
  - Que no registra las 15 interfaces de repositorio
    (es F-002 de Fase 2).
  - Que el parámetro `configuration` está reservado
    para uso futuro (F-002, F-012).

**Dependencias**
- T-002 (existe `CompositionRootAttribute`).
- T-003 (existe `SystemClock`).
- T-004 (existe `SystemPathResolver`).
- F-004 (existen `IClock` e `IPathResolver`).

**Archivos / proyectos afectados**
- `src/ControlDeFinanzasIA.UI/Composition/Bootstrap.cs`
  (nuevo).

**Criterios de finalización**
- El archivo existe y compila.
- `public static class Bootstrap` con
  `[CompositionRoot]` aplicada.
- `public static void ConfigureServices(IServiceCollection, IConfiguration)`.
- Registra `IClock` y `IPathResolver` como singletons
  con sus implementaciones de runtime.
- **NO** registra las 15 interfaces de repositorio
  (eso es F-002 de Fase 2).
- File-scoped namespace.
- `using` al tope.
- XML doc en la clase y en el método.
- `dotnet build`: 0 warnings, 0 errors.

---

## T-006 — Reescribir `UI/Program.cs` para arrancar con `IHost` + `Bootstrap`

**Objetivo**
Sustituir el `AppBuilder` aislado de F-001 por un `IHost`
configurado. El host carga `appsettings.json`, configura
logging de consola, llama a `Bootstrap.ConfigureServices`,
construye el host, y arranca Avalonia pasando el
`IServiceProvider`.

**Descripción**
Reemplazar el contenido de
`src/ControlDeFinanzasIA.UI/Program.cs` con la siguiente
lógica (descripción de alto nivel, la implementación
exacta se hace en la fase de aplicación):

1. **Crear el host builder** con
   `Host.CreateApplicationBuilder()` (de
   `Microsoft.Extensions.Hosting`).
2. **Configurar `IConfiguration`** con
   `builder.Configuration.AddJsonFile("appsettings.json",
   optional: true, reloadOnChange: true)`.
3. **Configurar logging** con
   `builder.Logging.AddConsole()` (de
   `Microsoft.Extensions.Logging.Console`).
4. **Llamar a `Bootstrap.ConfigureServices`**
   pasando `builder.Services` y `builder.Configuration`.
5. **Construir el host** con `builder.Build()`.
6. **Arrancar Avalonia** con un `AppBuilder` configurado
   para usar el `IServiceProvider` del host:
   - `var services = host.Services;`
   - Pasar `services` al constructor de `App` (o vía un
     mecanismo equivalente, según el patrón de
     integración Avalonia + IHost documentado en la guía
     oficial de Avalonia).
   - Llamar a `AvaloniaBuilder.SetupWithoutStarting()`
     si la integración con el host lo requiere (verificar
     en la documentación oficial de Avalonia la versión
     usada).
7. **Cerrar el host al cerrar Avalonia**:
   - Cuando la app termina (evento de cierre de la
     ventana principal), llamar a `host.Dispose()` o
     `await host.StopAsync()`.

Consideraciones:
- El método `Main` debe seguir siendo el entry point.
- Si Avalonia requiere un patrón específico para
  recibir un `IServiceProvider` externo, seguir la guía
  oficial de Avalonia para "Dependency Injection" (ver
  referencias de la spec).
- No se añade lógica de manejo global de excepciones
  (eso es F-012+).
- No se añade un `appsettings.json` al directorio del
  proyecto (el spec dice que es opcional; si no existe,
  el host funciona con defaults).

**Dependencias**
- T-001 (paquete `Microsoft.Extensions.Configuration.Json`
  disponible).
- T-005 (existe `Bootstrap.ConfigureServices`).
- F-001 (existe `UI/Program.cs` con la versión actual).

**Archivos / proyectos afectados**
- `src/ControlDeFinanzasIA.UI/Program.cs` (modificado:
  reescrito para usar `IHost`).

**Criterios de finalización**
- `Program.cs` reescrito con la lógica de host.
- `using` al tope (incluyendo
  `Microsoft.Extensions.Hosting`,
  `Microsoft.Extensions.DependencyInjection`,
  `Microsoft.Extensions.Configuration`,
  `Microsoft.Extensions.Configuration.Json`,
  `Microsoft.Extensions.Logging`,
  `Microsoft.Extensions.Logging.Console`,
  `ControlDeFinanzasIA.UI.Composition`).
- `using` de Avalonia preservados (e.g.,
  `using Avalonia;`, `using Avalonia.Controls;`,
  `using Avalonia.Headless;` si aplica).
- File-scoped namespace.
- XML doc en `Main` (si es público) o en los métodos
  helper (si los hay).
- `dotnet build`: 0 warnings, 0 errors.
- `dotnet test` desde la raíz: 111 tests pasan
  (verificación rápida de que la app sigue siendo
  ejecutable por los tests).

---

## T-007 — Modificar `UI/App.axaml.cs` para consumir `IServiceProvider` y emitir logs

**Objetivo**
Hacer que `App.axaml.cs` use el `IServiceProvider` del host
para resolver `ILogger<App>`, `IClock` e `IPathResolver`,
y emitir los logs de "App starting" / "App stopping".

**Descripción**
Modificar `src/ControlDeFinanzasIA.UI/App.axaml.cs` con la
siguiente lógica (descripción de alto nivel):

1. **Recibir `IServiceProvider`** en el constructor de
   `App` (añadir un parámetro al constructor existente, o
   un campo `public IServiceProvider Services { get; }`).
2. **En `OnFrameworkInitializationCompleted`**:
   - Resolver `ILogger<App>` con
     `_services.GetRequiredService<ILogger<App>>()`.
   - Resolver `IClock` con
     `_services.GetRequiredService<IClock>()`.
   - Resolver `IPathResolver` con
     `_services.GetRequiredService<IPathResolver>()`.
   - Guardar la hora de inicio:
     `var startTime = _clock.UtcNow;`.
   - Emitir log:
     `_logger.LogInformation("App starting at {Time},
     data directory: {Directory}", startTime,
     _pathResolver.DataDirectory);`
   - Crear la `MainWindow` (sigue siendo un `Window` en
     código, **no** se crea `MainWindow.axaml`).
   - Wire up el evento de cierre de la ventana para
     emitir "App stopping":
     - `mainWindow.Closing += (sender, e) => { ... };`
     - En el handler, calcular
       `var elapsed = _clock.UtcNow - startTime;`.
     - Emitir
       `_logger.LogInformation("App stopping after
       {ElapsedMs} ms", elapsed.TotalMilliseconds);`.
   - Asignar la ventana a
     `ApplicationLifetime.MainWindow`.
3. **Si el `IServiceProvider` no se puede construir** (e.g.,
   el host falló), `App` debe manejar el caso con un
   fallback: usar `Console.WriteLine` o un logger
   mínimo hardcodeado para reportar el error. Esto es
   un edge case (EC-1) y solo se maneja si AC-1 lo
   requiere.
4. **No** se añade un `MainWindow.axaml`. La ventana
   sigue siendo un `Window` creado en código (igual
   que en F-001).
5. **No** se añade manejo global de excepciones
   (`IExceptionHandler`). Eso es F-012+.

Consideraciones:
- Si el patrón de integración Avalonia + IHost usa un
  mecanismo distinto para pasar el `IServiceProvider`
  (e.g., `IAvaloniaApplication` con un campo público
  `Services`), seguir la guía oficial de Avalonia y
  adaptar la descripción anterior.
- Los placeholders `{Time}`, `{Directory}`, `{ElapsedMs}`
  son los nombres exactos; se pueden ajustar a
  convenciones locales si el equipo lo prefiere, pero
  deben ser placeholders estructurados (no string
  interpolation).
- Los logs van en inglés (convención de la comunidad
  .NET, ver F-002 §RNF-5 y F-005).

**Dependencias**
- T-006 (`Program.cs` reescrito, el `IServiceProvider`
  del host está disponible).
- T-003, T-004, T-005 (los servicios están registrados).

**Archivos / proyectos afectados**
- `src/ControlDeFinanzasIA.UI/App.axaml.cs` (modificado:
  consume `IServiceProvider` y emite logs).

**Criterios de finalización**
- `App.axaml.cs` modificado.
- Resuelve `ILogger<App>`, `IClock`, `IPathResolver`
  desde el `IServiceProvider`.
- Emite "App starting" al arrancar.
- Emite "App stopping" al cerrar.
- Crea la `MainWindow` (sigue siendo un `Window` en
  código).
- `using` al tope (incluyendo
  `Microsoft.Extensions.Logging`,
  `ControlDeFinanzasIA.Application.Abstractions`).
- File-scoped namespace preservado.
- XML doc en los métodos relevantes.
- `dotnet build`: 0 warnings, 0 errors.
- `dotnet test`: 111 tests pasan.

---

## T-008 — Verificar que el test R-4 de F-007 sigue pasando y que los servicios se resuelven

**Objetivo**
Confirmar que la introducción del composition root no rompe
la arquitectura (R-4 de F-007 sigue pasando) y que los
servicios se resuelven correctamente. Si el test R-4 falla,
investigar y ajustar (sin modificar el test de F-007; solo
ajustar nombres o implementaciones de F-008).

**Descripción**
Esta tarea es verificación, no implementación. Se ejecuta
después de T-007.

### T-008.1 — Confirmar build limpio

1. Ejecutar `dotnet build -c Release` desde la raíz.
2. Confirmar 0 warnings, 0 errors.

### T-008.2 — Confirmar tests existentes

1. Ejecutar `dotnet test` desde la raíz.
2. Resultado esperado: 111 tests pasan (100 + 11 de F-007),
   0 failures, 1 skipped (placeholder de F-001 en
   Tests.Integration).

### T-008.3 — Confirmar R-4 específicamente

1. Ejecutar:
   `dotnet test --filter "FullyQualifiedName~R4_UI_No_Referencia_A_Infrastructure_Salvo_CompositionRoot"`.
2. Confirmar que el test pasa.
3. Si falla, investigar:
   - Si el filtro `.DoNotHaveName("Bootstrap")` no
     excluye correctamente (raro; verificar la API de
     NetArchTest para la versión usada).
   - Si el atributo `[CompositionRoot]` no se aplica
     (verificar T-002 y T-005).
   - Si la dependencia `UI → Infrastructure` se añadió
     por error (verificar que T-006 no introdujo
     `<ProjectReference>` a Infrastructure; ver
     §16.4 de la spec).
4. **No modificar el test R-4 de F-007.** Si hay que
   ajustar algo, ajustar la convención de F-008 (e.g.,
   cambiar el nombre de `Bootstrap` o la API de
   NetArchTest usada). Documentar el cambio en el commit
   / PR.

### T-008.4 — Verificar que los servicios se resuelven (via code review)

1. Code review de `App.axaml.cs`:
   - Confirma que `_services.GetRequiredService<IClock>()`
     es correcto (no `GetService`, que puede devolver
     `null`).
   - Confirma que `_services.GetRequiredService<IPathResolver>()`
     es correcto.
   - Confirma que `_services.GetRequiredService<ILogger<App>>()`
     es correcto.
2. Code review de `Bootstrap.ConfigureServices`:
   - Confirma que `services.AddSingleton<IClock, SystemClock>()`
     está presente.
   - Confirma que
     `services.AddSingleton<IPathResolver, SystemPathResolver>()`
     está presente.
3. Code review de `Program.cs`:
   - Confirma que `Bootstrap.ConfigureServices(host.Services, host.Configuration)` (o equivalente) se llama.
   - Confirma que el `IServiceProvider` se pasa al `App`.

**Dependencias**
- T-007 (`App.axaml.cs` modificado y compilable).

**Archivos / proyectos afectados**
- Ninguno nuevo. Esta tarea solo verifica.

**Criterios de finalización**
- Build limpio (0 warnings, 0 errors).
- 111 tests pasan.
- R-4 específicamente pasa.
- Code review confirma que los servicios están bien
  registrados y resueltos.
- Si R-4 falla, el problema se diagnostica y se arregla
  (sin modificar el test de F-007).

---

## T-009 — Verificación con arranque real de la app, format check y cierre de la feature

**Objetivo**
Ejecutar la app manualmente y verificar que arranca, emite
los logs, y los servicios se comportan correctamente. Luego
ejecutar las acciones de cierre (format check,
actualización de `features.md`, rename de la spec).

**Descripción**

### T-009.1 — Arranque manual de la app

1. Ejecutar
   `dotnet run --project src/ControlDeFinanzasIA.UI`
   desde la raíz.
2. Verificar:
   - La ventana aparece con título "ControlDeFinanzasIA".
   - En la consola (o en el output del IDE) aparece el
     log de "App starting" con la hora actual y la ruta
     del directorio de datos.
3. Cerrar la ventana (X, Alt+F4, etc.).
4. Verificar:
   - La app termina limpiamente.
   - En la consola aparece el log de "App stopping"
     con la duración.
5. **Si no se puede ejecutar la GUI** (e.g., entorno
   headless, sin display):
   - Documentar la limitación.
   - Saltar T-009.1 y proceder con T-009.2 a T-009.4.
   - Mencionar la limitación en la PR / commit para que
     otro developer con entorno gráfico la verifique.

### T-009.2 — Verificación de la ruta del directorio de datos

- En Windows: ejecutar
  `dotnet run --project src/ControlDeFinanzasIA.UI`,
  verificar que el log de "App starting" muestra una ruta
  que termina en `ControlDeFinanzasIA` bajo
  `%APPDATA%`. Verificar que el directorio existe en
  el filesystem.
- En macOS y Linux: el developer que pruebe en esos
  entornos verifica la ruta correspondiente. Si F-008
  se implementa solo en Windows (lo más probable por el
  entorno del developer), se documenta en la PR que la
  verificación cross-OS se difiere a una fase posterior o
  a otro developer con macOS/Linux.

### T-009.3 — Format check

1. Ejecutar
   `dotnet format style --verify-no-changes` desde la
   raíz.
2. Confirmar exit 0. Si propone cambios, aplicarlos
   con `dotnet format style` y volver a verificar.

### T-009.4 — Cierre de la feature

1. **Verificar T-008 revert state** (los cambios de
   T-008 no deberían haber tocado nada; pero confirmar):
   - `dotnet test` desde la raíz: 111 pasan.
   - `git status` (o equivalente) muestra solo los
     archivos legítimos de T-001 a T-007.

2. **Actualizar el desglose de fase:** en
   `spec/phases/phase-01/features.md`:
   - Cambiar la fila 008 de la tabla resumen de
     "Pendiente" a "✅ COMPLETADA" (mismo formato que
     F-001 a F-007).
   - Cambiar el header de la sección F-008 de
     `## F-008 — Composition root con host de DI y logging`
     a
     `## F-008 — Composition root con host de DI y logging — ✅ COMPLETADA`.

3. **Renombrar la spec:** mover
   `spec/features/phase-01/F-008-composition-root.md`
   a
   `spec/features/phase-01/F-008-composition-root.done.md`
   (convención del proyecto, ver F-001 a F-007).

4. **Build final** después del rename:
   `dotnet build -c Release` debe seguir 0/0.

5. **Verificar Fase 1 cerrada**:
   - Las 8 features de Fase 1 están marcadas ✅
     COMPLETADA en `spec/phases/phase-01/features.md`.
   - Las 8 specs están renombradas a `.done.md`.
   - El `dotnet test` final pasa los tests acumulados
     (esperado: 100 originales + 11 de F-007 + 0 de
     F-008 = 111).

### Resumen de verificación de AC

| AC | Criterio | Estado |
| --- | --- | --- |
| AC-1 | App arranca y emite "App starting" | T-009.1 |
| AC-2 | App se cierra y emite "App stopping" | T-009.1 |
| AC-3 | Servicios resolubles desde `App.axaml.cs` | T-008.4 + T-009.1 |
| AC-4 | Composition root respeta la arquitectura (R-4 pasa) | T-008.3 |
| AC-5 | Compatibilidad multiplataforma | T-009.2 |
| AC-6 | Configuración y logging | T-006 + T-007 + T-009.1 |
| AC-7 | Convenciones del repo | T-002 a T-007 + T-009.3 |
| AC-8 | Sin scope creep | T-001 a T-007 (verificación continua) |
| AC-9 | Documentación y trazabilidad | T-009.4 |

**Dependencias**
- T-001 a T-008 (todas las anteriores).

**Archivos / proyectos afectados**
- `spec/phases/phase-01/features.md` (modificado: F-008
  marcada como completada).
- `spec/features/phase-01/F-008-composition-root.md`
  (renombrado a `.done.md`).

**Criterios de finalización**
- App arranca manualmente y emite "App starting" /
  "App stopping" (o, si no se puede, limitación
  documentada).
- `dotnet format style --verify-no-changes`: exit 0.
- `dotnet build -c Release`: 0 warnings, 0 errors.
- `dotnet test` desde la raíz: 111 pasan, 0 failures.
- `spec/phases/phase-01/features.md` actualizado con
  F-008 marcada como completada (tabla resumen y
  header de sección).
- Spec renombrada a `.done.md`.
- Las 8 features de Fase 1 están ahora marcadas ✅
  COMPLETADA.

---

## Orden recomendado y trabajo en paralelo

**Orden secuencial estricto:** T-001 → (T-002 ‖ T-003 ‖ T-004 en
paralelo posible) → T-005 → T-006 → T-007 → T-008 → T-009.

**T-002, T-003, T-004 son independientes entre sí** (archivos
distintos, no comparten código). Si hay 2-3 personas, se
pueden hacer en paralelo. T-001 no bloquea estas (el paquete
Configuration.Json es solo para T-006).

**T-005 depende de T-002, T-003, T-004** (usa los tres
artefactos).

**T-006 depende de T-005 y T-001** (usa el método de Bootstrap
y el paquete Configuration).

**T-007 depende de T-006** (consume el `IServiceProvider` que
T-006 expone).

**T-008 es verificación** de T-007.

**T-009 es cierre** de toda la feature.

## Riesgos de la fase de tareas

| Riesgo | Probabilidad | Impacto | Mitigación |
| --- | --- | --- | --- |
| El patrón de integración Avalonia + IHost no es el esperado o requiere código específico no documentado. | Media | Alto | Consultar la guía oficial de Avalonia (referencias de la spec) y adaptar la descripción de T-006 / T-007. Si la API cambia, documentar en el commit / PR. |
| El filtro `.DoNotHaveName("Bootstrap")` de NetArchTest no funciona como se espera y R-4 falla. | Baja | Alto | T-008.3 verifica R-4. Si falla, ajustar la convención (cambiar el nombre o usar otra API de NetArchTest). NO modificar el test de F-007. |
| `SystemPathResolver` falla al crear el directorio por permisos o disco lleno. | Media | Medio | Decisión documentada en la spec: la app no arranca. AC-3 no requiere captura de excepciones. F-012+ podría añadir retry. |
| El `IServiceProvider` no se pasa correctamente a Avalonia y los servicios no se resuelven. | Media | Alto | T-008.4 hace code review. T-009.1 ejecuta la app. Si falla, ajustar el patrón de inyección. |
| `appsettings.json` se crea accidentalmente con formato incorrecto. | Baja | Bajo | F-008 no crea `appsettings.json` (es opcional). Si se crea, debe ser JSON válido. T-006 usa `optional: true` para que la ausencia no falle. |
| La verificación manual (T-009.1) no se puede ejecutar en el entorno actual. | Media | Bajo | T-009.1 documenta la limitación y se difiere a un developer con entorno gráfico. T-008.4 (code review) y T-008.2 (tests) cubren la verificación estática. |
| El atributo `[CompositionRoot]` se aplica accidentalmente a otra clase. | Baja | Bajo | Code review. La convención es que solo `Bootstrap` lo usa. No hay test automatizado para el atributo (F-007 usa el nombre). |
| `SystemPathResolver` no maneja correctamente el caso Linux sin `XDG_CONFIG_HOME` definido. | Media | Bajo | Implementación cuidadosa con check explícito. AC-5 verifica que la ruta Linux es correcta. |
| Tarea T-006 (reescribir `Program.cs`) se vuelve difícil de revisar en un solo commit. | Media | Bajo | Si el diff es grande, considerar dividirlo en dos commits: "agregar IHost" y "integrar Avalonia con IHost". F-008 no obliga a commits separados. |

## Referencias

- `spec/features/phase-01/F-008-composition-root.md` —
  la spec completa de F-008. Este tasks file es su
  descomposición técnica.
- `docs/architecture.md` §3 (solución y proyectos) —
  `UI` es el composition root.
- `docs/architecture.md` §4 (grafo de dependencias) —
  la excepción del composition root está documentada
  aquí.
- `docs/architecture.md` §9 (capa de UI) — el
  composition root vive en `App.axaml.cs` /
  `Program.cs`.
- `docs/architecture.md` §12 (ruta del archivo de
  datos) — `IPathResolver` resuelve según el SO.
- `docs/architecture.md` §13 (configuración) — capas
  de configuración.
- `docs/architecture.md` §14 (logging) — stack de
  logging, qué se loguea, qué no.
- `docs/roadmap.md` §Fase 1, F-008 dentro del
  desglose.
- `docs/roadmap.md` §5 (MVP y límites) — F-008 cierra
  la fase 1.
- `spec/constitution/tech-stack.md` — paquetes de
  Microsoft.Extensions, ruta por SO.
- `spec/constitution/mission.md` — Clean Architecture,
  determinismo testeable.
- `spec/phases/phase-01/features.md` — F-008 dentro
  del desglose de la fase 1.
- `spec/features/phase-01/F-001-skeleton-solution.done.md`
  — provee `UI/Program.cs`, `UI/App.axaml.cs`, y los
  paquetes Microsoft.Extensions pre-instalados.
- `spec/features/phase-01/F-004-Abstracciones-clock-pathresolver.done.md`
  — provee `IClock` e `IPathResolver`.
- `spec/features/phase-01/F-005-comuntypes-result-pagedresult.done.md`
  — patrón de `Result<T>` (no se usa en F-008, pero
  relevante si `Bootstrap` reporta errores).
- `spec/features/phase-01/F-006-contrato-interfaces-repo.done.md`
  — provee las 15 interfaces (no se registran en
  F-008; F-002 de Fase 2 lo hará).
- `spec/features/phase-01/F-007-validacion-arquitectonica.done.md`
  — provee el test R-4 con el filtro `Bootstrap`
  listo para F-008.
- `spec/README.md` — convención del directorio
  `spec/`.
- [Microsoft.Extensions.Hosting — documentación
  oficial](https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host)
  (a consultar vía Context7 al implementar, para
  confirmar la API de la versión elegida).
- [Avalonia con Generic Host — guía
  oficial](https://docs.avaloniaui.net/docs/guides/dependency-injection/)
  (a consultar vía Context7 al implementar, para
  confirmar el patrón de integración Avalonia + .NET
  Host).
