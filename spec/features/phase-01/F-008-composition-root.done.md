# Specification — F-008: Composition root con host de DI y logging

> **Fase:** 1 — Esqueleto de arquitectura y contratos base
> **Feature:** F-008
> **Estado:** Pendiente de implementación
> **Spec ID:** SPEC-F008
>
> Fuentes de verdad: `docs/vision.md`, `docs/architecture.md`,
> `docs/roadmap.md`, `spec/constitution/tech-stack.md`,
> `spec/constitution/mission.md`,
> `spec/phases/phase-01/features.md` (F-008 dentro del
> desglose de la fase 1).

---

## 1. Resumen

F-008 es la **última feature de la Fase 1**. Implementa el
**composition root** del proyecto: un único punto en el código
donde se conectan las cuatro capas de la Clean Architecture
vía un `Host` de .NET con `Microsoft.Extensions.DependencyInjection`
y `Microsoft.Extensions.Logging`.

Sin F-008, las interfaces de `Application` existen (F-004, F-005,
F-006) pero **nadie las instancia**. F-008 es lo que prueba que la
arquitectura está **eléctricamente conectada** y no solo declarada
en archivos y validada por NetArchTest (F-007).

Al final de la feature, la app arranca, registra todas las
abstracciones en el host, emite los mensajes de log
"App starting" / "App stopping", y los servicios
`IClock` / `IPathResolver` son resolubles desde `App.axaml.cs`
a través del `IServiceProvider`.

---

## 2. Contexto

El proyecto tiene 4 capas con el grafo de dependencias definido
en `docs/architecture.md` §4:

```
UI (Avalonia) → Infrastructure → Application → Domain
```

F-001 creó los 6 proyectos con las referencias correctas.
F-004 definió `IClock` e `IPathResolver` (los "seams" para
testear). F-005 añadió `Result<T>` y `PagedResult<T>`. F-006
creó las 15 interfaces de repositorio. F-007 añadió la red de
seguridad de NetArchTest.

Sin F-008, **ninguna abstracción se ha instanciado nunca**. La
arquitectura existe en el código y en los tests, pero el binario
no la ejerce. F-008 es la **primera ejecución real** del grafo:
el `Bootstrap` resuelve `IClock` desde el container, ejecuta un
log de "App starting", muestra la ventana, ejecuta un log de "App
stopping" al cerrar.

Además, F-008 cierra la mecánica de la **excepción del composition
root** que F-007 dejó documentada: el test R-4
(`R4_UI_No_Referencia_A_Infrastructure_Salvo_CompositionRoot`)
filtra tipos con nombre `Bootstrap` y asume que existirá una
clase `Bootstrap` con el atributo `[CompositionRoot]`. F-008
implementa ambos.

---

## 3. Objetivo

- Crear un `Host` de .NET en `UI/Program.cs` usando
  `Host.CreateApplicationBuilder` con `IServiceCollection`,
  `IConfiguration` e `ILoggerFactory`.
- Crear una clase `Bootstrap` en `UI/Composition/Bootstrap.cs`
  (anotada con `[CompositionRoot]`) que centralice toda la
  registración de servicios y la construcción del `IServiceProvider`.
- Crear el atributo marker `[CompositionRoot]` en
  `UI/Composition/CompositionRootAttribute.cs`.
- Crear las implementaciones triviales de runtime:
  - `SystemClock` (en `UI/System/`) implementando `IClock`.
  - `SystemPathResolver` (en `UI/System/`) implementando
    `IPathResolver`.
- Conectar el `IServiceProvider` al arranque de Avalonia para
  que `App.axaml.cs` pueda resolver servicios vía DI.
- Añadir log de "App starting" al arranque y "App stopping" al
  cierre, usando `ILogger<T>`.
- Mantener la **excepción arquitectónica** del composition root
  (UI → Infrastructure solo desde tipos anotados con
  `[CompositionRoot]`); el test R-4 de F-007 debe seguir pasando
  con la nueva dependencia.

---

## 4. Alcance

### 4.1 En alcance

- `UI/Program.cs` reescrito para arrancar Avalonia con un
  `IHost` configurado (no más `AppBuilder` aislado).
- `UI/Composition/Bootstrap.cs`: clase estática (o no estática,
  instanciable) que recibe un `IServiceCollection` y registra
  todos los servicios del proyecto. Anotada con
  `[CompositionRoot]`.
- `UI/Composition/CompositionRootAttribute.cs`: atributo marker
  (sealed, hereda de `Attribute`).
- `UI/System/SystemClock.cs`: implementación de `IClock` que
  devuelve la hora del sistema.
- `UI/System/SystemPathResolver.cs`: implementación de
  `IPathResolver` que devuelve la ruta esperada por SO según
  `docs/architecture.md` §12 y `spec/constitution/tech-stack.md`.
- `UI/App.axaml.cs`: usa el `IServiceProvider` del host para
  resolver `IClock`, `IPathResolver` y `ILogger<App>`, emite los
  logs de "App starting" / "App stopping".
- Registración en el host de:
  - `IClock` → `SystemClock` (singleton).
  - `IPathResolver` → `SystemPathResolver` (singleton).
  - `ILogger<T>` (ya provisto por `Microsoft.Extensions.Logging`).
  - `IConfiguration` (cargando `appsettings.json` si existe).
- Verificación de que la app arranca, los logs aparecen, y los
  servicios se resuelven correctamente desde `App.axaml.cs`.
- Verificación de que el test R-4 de F-007 sigue pasando
  (excepción del composition root funciona).

### 4.2 Fuera de alcance

- **No se registran las 15 interfaces de repositorio.** Las
  implementaciones concretas (con EF Core + SQLite) llegan en
  F-002 de Fase 2. F-008 solo registra `IClock`, `IPathResolver`
  e `ILogger`. Esto es explícito en los criterios de aceptación.
- **No se añade `MainWindow.axaml` ni navegación.** F-001
  dejó la ventana en código; F-008 mantiene ese patrón. La
  navegación entre vistas llega en fases posteriores (cuando
  se implementen los use cases de la Fase 3).
- **No se implementa un `IConfiguration` complejo.** F-008
  carga `appsettings.json` si existe, pero no define secciones
  de configuración todavía (esos llegan en F-012, "Configuración:
  tema, idioma, IA (sin uso)").
- **No se implementa un sink de log persistente (archivo).** El
  scope de F-008 es consola en `Debug`. El archivo rotativo
  llega en F-012 o en una fase de pulido.
- **No se cambia la convención de file-scoped namespace, el
  `.editorconfig`, el `stylecop.json`, ni el `Directory.Build.props`.**
  F-008 hereda las convenciones existentes.
- **No se añade ningún paquete NuGet nuevo.** Los paquetes
  necesarios (`Microsoft.Extensions.Hosting`,
  `Microsoft.Extensions.DependencyInjection`,
  `Microsoft.Extensions.Logging`,
  `Microsoft.Extensions.Logging.Console`) ya están en el
  `Directory.Packages.props` desde F-001.
- **No se modifica la estructura de carpetas de `src/`.** F-008
  añade archivos en `UI/Composition/` y `UI/System/`, pero no
  reorganiza lo existente.
- **No se modifica el `MainWindow` (ni se crea `MainWindow.axaml`).**
  F-008 sigue creando la ventana en código en `App.axaml.cs`
  (igual que en F-001), solo que ahora con servicios
  inyectados.
- **No se implementan `IExchangeRateProvider`, `ILLMClient`,
  ni otras abstracciones que se mencionan en `architecture.md`
  pero cuyas implementaciones concretas llegan en fases
  futuras.** F-008 es la base; las implementaciones se montan
  encima.
- **No se añade un `IExceptionHandler` global.** El manejo de
  excepciones global se cubre en F-012 o en una feature
  posterior. F-008 no captura excepciones inesperadas a nivel
  de host; las que ocurran se loguean con la configuración por
  defecto de `Microsoft.Extensions.Logging`.

---

## 5. Casos de uso

### CU-1 — App arranca y emite log de "App starting"

- **Actor:** usuario que hace doble click en el binario o
  ejecuta `dotnet run --project src/ControlDeFinanzasIA.UI`.
- **Trigger:** el binario se ejecuta.
- **Precondición:** la app está compilada.
- **Postcondición:** la ventana aparece, y en la consola (o
  en `Output` del IDE) aparece un log con
  `info: ControlDeFinanzasIA.UI.App[0] App starting` o
  similar (formato exacto configurable; lo importante es
  que el mensaje se emite y la app arranca).

### CU-2 — App se cierra y emite log de "App stopping"

- **Actor:** usuario que cierra la ventana con la X, o
  presiona Alt+F4, o mata el proceso.
- **Trigger:** evento de cierre de la ventana principal.
- **Precondición:** la app está corriendo.
- **Postcondición:** la app se cierra limpiamente, y en la
  consola aparece un log con
  `info: ControlDeFinanzasIA.UI.App[0] App stopping` o
  similar.

### CU-3 — `IClock` se resuelve desde `App.axaml.cs`

- **Actor:** código de `App.axaml.cs` (o cualquier otro punto
  que reciba `IServiceProvider`).
- **Trigger:** la app arranca; `App.axaml.cs` pide
  `serviceProvider.GetRequiredService<IClock>()`.
- **Precondición:** `Bootstrap` registró `IClock` con
  `SystemClock`.
- **Postcondición:** la instancia recibida implementa
  `IClock`; `.UtcNow` devuelve la hora actual del sistema;
  `.Today` devuelve la fecha actual como `DateOnly`.

### CU-4 — `IPathResolver` se resuelve desde `App.axaml.cs`

- **Actor:** código de `App.axaml.cs`.
- **Trigger:** la app arranca; `App.axaml.cs` pide
  `serviceProvider.GetRequiredService<IPathResolver>()`.
- **Precondición:** `Bootstrap` registró `IPathResolver` con
  `SystemPathResolver`.
- **Postcondición:** la instancia recibida implementa
  `IPathResolver`; `.DataDirectory` y `.DatabaseFilePath`
  devuelven la ruta esperada por SO según
  `spec/constitution/tech-stack.md`.

### CU-5 — Composition root referencia a Infrastructure (excepción arquitectónica)

- **Actor:** el sub-sistema de NetArchTest (F-007).
- **Trigger:** el test R-4
  (`R4_UI_No_Referencia_A_Infrastructure_Salvo_CompositionRoot`)
  corre como parte de `dotnet test`.
- **Precondición:** `Bootstrap` está anotado con
  `[CompositionRoot]`.
- **Postcondición:** el test pasa. La dependencia
  `UI → Infrastructure` existe en el `.csproj` (o en los
  assemblies), pero la inspección de tipos con NetArchTest
  excluye el tipo `Bootstrap` y no reporta la violación.

### CU-6 — `appsettings.json` se carga (opcional)

- **Actor:** el host al arrancar.
- **Trigger:** el host construye `IConfiguration`.
- **Precondición:** existe (o no) un archivo `appsettings.json`
  al lado del binario. Si no existe, el host usa solo
  defaults; no falla.
- **Postcondición:** `IConfiguration` está disponible en el
  container, y los servicios pueden pedir
  `IOptions<T>` cuando llegue F-012.

---

## 6. Requisitos funcionales

### RF-1 — Host de .NET con Generic Host

- `UI/Program.cs` usa `Host.CreateApplicationBuilder()` (o
  `Host.CreateDefaultBuilder()` si se prefiere la versión con
  defaults) para crear un `IHostBuilder`.
- El host expone:
  - `IServiceProvider` (vía `host.Services`).
  - `IConfiguration` (vía `host.Services.GetRequiredService<IConfiguration>()`).
  - `ILogger<T>` (vía `host.Services.GetRequiredService<ILogger<T>>()`).
- El host se compila con `host.Build()` antes de arrancar
  Avalonia.
- El host se dispone (`host.Dispose()` o `await using`) cuando
  Avalonia termina.

### RF-2 — Composition root (`Bootstrap`)

- Existe una clase `Bootstrap` en
  `UI/Composition/Bootstrap.cs` con un método estático
  `ConfigureServices(IServiceCollection services, IConfiguration
  configuration)` (o equivalente con `IHostBuilder`).
- `Bootstrap` está anotada con `[CompositionRoot]`.
- `Bootstrap` registra:
  - `IClock` → `SystemClock` (singleton).
  - `IPathResolver` → `SystemPathResolver` (singleton).
  - `ILoggerFactory` (ya viene del host; `Bootstrap` no lo
    registra explícitamente, lo deja como está).
  - `IConfiguration` (ya viene del host; ídem).
- `Bootstrap` **NO** registra las 15 interfaces de repositorio
  (queda para F-002 de Fase 2, cuando las implementaciones
  concretas existan).
- `Bootstrap` no tiene lógica condicional compleja. Si en el
  futuro hace falta registrar más servicios, se hace en este
  mismo método (no se fragmenta en múltiples clases).

### RF-3 — Atributo `[CompositionRoot]`

- Existe un atributo sealed `CompositionRootAttribute` en
  `UI/Composition/CompositionRootAttribute.cs`.
- Hereda de `Attribute`.
- Su target es `AttributeTargets.Class` (y opcionalmente
  `AttributeTargets.Struct`, por si en el futuro se quiere
  anotar structs).
- No tiene propiedades ni state; es un marker puro.
- El test R-4 de F-007 **no** inspecciona este atributo
  directamente (usa `.That().DoNotHaveName("Bootstrap")` como
  filtro de tipos). El atributo existe por **documentación
  y consistencia** y para que un futuro reader de código
  entienda la convención sin tener que abrir el test.

### RF-4 — Implementaciones triviales de runtime

- `UI/System/SystemClock.cs`:
  - Implementa `IClock`.
  - `UtcNow => DateTime.UtcNow`.
  - `Today => DateOnly.FromDateTime(DateTime.UtcNow)`.
  - Es `sealed` y `public` (o `internal`; ver §16 — decisión).
- `UI/System/SystemPathResolver.cs`:
  - Implementa `IPathResolver`.
  - `DataDirectory` devuelve la ruta del directorio de datos
    según el SO, siguiendo `spec/constitution/tech-stack.md`:
    - Windows: `%APPDATA%\ControlDeFinanzasIA\`
      (resuelto vía `Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)`).
    - macOS: `~/Library/Application Support/ControlDeFinanzasIA/`
      (resuelto vía `Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)`
      concatenado con `/Library/Application Support/ControlDeFinanzasIA`).
    - Linux: `$XDG_CONFIG_HOME/ControlDeFinanzasIA/` o
      `~/.config/ControlDeFinanzasIA/`. La spec dice
      `$XDG_CONFIG_HOME` con fallback a `~/.config/`.
  - `DatabaseFilePath` devuelve
    `Path.Combine(DataDirectory, "data.db")`.
  - La clase crea el directorio si no existe (usando
    `Directory.CreateDirectory` en el constructor o en un
    lazy initializer). Esto es una excepción a la regla "no
    I/O en constructores" porque el directorio tiene que
    existir para que la DB se pueda crear en F-002 de Fase 2.
    La spec documenta esto en §10.
  - Es `sealed` y `public` (o `internal`; ver §16 — decisión).

### RF-5 — `App.axaml.cs` consume el host

- `App.axaml.cs` recibe el `IServiceProvider` desde `Program.cs`
  (pasado como parámetro al método que crea la `Application`).
- En `OnFrameworkInitializationCompleted`:
  - Resuelve `ILogger<App>` desde el service provider.
  - Resuelve `IClock` desde el service provider.
  - Resuelve `IPathResolver` desde el service provider.
  - Emite el log `App starting` con la hora actual y la ruta
    del directorio de datos (concatenados).
  - Crea la `MainWindow` (sigue siendo un `Window` en código,
    no un `MainWindow.axaml`).
  - Asigna la ventana a `ApplicationLifetime.MainWindow`.
- Cuando la ventana se cierra, emite el log `App stopping`
  con la duración desde "App starting" (en ms o en formato
  legible, lo importante es que se emite).

### RF-6 — Logging configurado

- `Microsoft.Extensions.Logging` está configurado con
  `AddConsole` (sinks de consola).
- En `Debug`, los logs de nivel `Information` y superiores
  aparecen en la consola.
- En `Release`, el comportamiento por defecto es el de
  `Microsoft.Extensions.Logging` sin sinks adicionales
  (los logs van al depurador si está attached, o se
  descartan si no). Esto es aceptable para F-008; el
  sink persistente llega después.
- Los nombres de las categorías de log siguen el patrón
  estándar de `ILogger<T>` (e.g.,
  `ControlDeFinanzasIA.UI.App`).

### RF-7 — `IConfiguration` cargado

- El host carga `appsettings.json` si existe, con
  `hostBuilder.ConfigureAppConfiguration((context, config) =>
  { config.AddJsonFile("appsettings.json", optional: true); })`.
- Si el archivo no existe, el host sigue funcionando con
  defaults; no se lanza excepción.
- F-008 no usa ninguna setting de `appsettings.json` todavía;
  el archivo puede estar vacío o no existir. F-012 (y
  posiblemente antes, F-008+) lo consumirá.

### RF-8 — `Program.cs` reorganizado

- `Program.cs` no usa más el `AppBuilder` aislado de Avalonia.
  En su lugar, usa `Host.CreateApplicationBuilder()` para
  configurar el host, llama a `Bootstrap.ConfigureServices(...)`,
  construye el host, y luego arranca Avalonia con
  `AppBuilder` que recibe el `IServiceProvider`.
- El método `Main` se simplifica a:
  1. Crear el host.
  2. Llamar a `Bootstrap.ConfigureServices(host.Services, ...)`.
  3. Construir el host.
  4. Arrancar Avalonia pasando el `IServiceProvider` al `App`.
  5. Al cerrar Avalonia, disponer el host.

---

## 7. Requisitos no funcionales

### RNF-1 — Determinismo

- El composition root es determinista. No hay lógica condicional
  basada en tiempo o entorno (más allá de la lectura de
  variables de entorno en `SystemPathResolver`, que es
  documentada).
- Múltiples arranques producen el mismo grafo de servicios.

### RNF-2 — Velocidad de arranque

- El composition root añade menos de 100 ms al tiempo de
  arranque de la app (medible con un log de "App starting"
  y un log de "First window shown", si se quiere ser
  estricto). En la práctica, el overhead de `IHost` es
  despreciable para una app de escritorio.

### RNF-3 — Compatibilidad con la arquitectura

- Después de F-008, **el grafo de dependencias sigue
  cumpliendo** las reglas R-1 a R-5 de F-007. En particular:
  - R-1 (Domain sin dependencias): no se toca. ✅
  - R-2 (Application sin EF Core, etc.): no se toca. ✅
  - R-3 (Infrastructure → Application + Domain): no se toca. ✅
  - R-4 (UI no depende de Infrastructure salvo composition root):
    se introduce la dependencia, pero el filtro
    `.That().DoNotHaveName("Bootstrap")` la excluye. ✅
  - R-5 (src/ no depende de Tests): no se toca. ✅
- El test R-4 debe seguir pasando **sin modificar su
  implementación** (excepto si hay que ajustar el filtro
  por cambio de nombre; ver §16).

### RNF-4 — Sin paquetes nuevos

- F-008 no añade ningún paquete NuGet. Todo lo necesario
  (`Microsoft.Extensions.Hosting`,
  `Microsoft.Extensions.DependencyInjection`,
  `Microsoft.Extensions.Logging`,
  `Microsoft.Extensions.Logging.Console`) ya está en
  `Directory.Packages.props` desde F-001.

### RNF-5 — Sin nuevos archivos de configuración

- F-008 no crea ni modifica `appsettings.json`,
  `Directory.Build.props`, `Directory.Packages.props`,
  `.editorconfig`, `stylecop.json`, ni el workflow de CI.
  (El workflow de CI ya corre `dotnet test`, lo cual incluye
  los tests de F-007.)

### RNF-6 — Mensajes de log en inglés

- Los mensajes "App starting" y "App stopping" están en
  inglés (convención de la comunidad .NET; ver F-002 §RNF-5
  y F-005).
- Los logs de Microsoft.Extensions.Logging usan el formato
  por defecto del sink de consola (que es texto plano con
  timestamp, nivel, categoría y mensaje).

### RNF-7 — Cobertura de tests

- F-008 no añade tests nuevos al proyecto. La lógica del
  composition root (registrar servicios, construir el host)
  es trivial y se cubre con la verificación de que la app
  arranca y emite los logs (AC-1 a AC-3 de §14).
- Los tests existentes (100 de F-002, F-003, F-005, F-004
  + 11 de F-007) siguen pasando.

---

## 8. Reglas de negocio

### RN-1 — `Bootstrap` es el único composition root

- Solo existe **una** clase con `[CompositionRoot]` en todo
  el proyecto. Si en el futuro hace falta otro punto de
  composition (e.g., un bootstrap de tests de integración),
  se discute antes de crearlo. F-008 asume que no.
- El filtro de NetArchTest en R-4 usa
  `.That().DoNotHaveName("Bootstrap")`. Si hubiera un
  segundo `Bootstrap` en otro namespace, el filtro lo
  capturaría. Esto es aceptable: en ese caso, el
  implementador del segundo bootstrap debe seguir la misma
  convención o documentar la excepción.

### RN-2 — `SystemClock` y `SystemPathResolver` viven en `UI/System/`, no en `Application` ni en `Domain`

- **Razón:** "Application" y "Domain" son capas puras. La
  `SystemClock` lee la hora del sistema (acoplamiento a
  `DateTime.UtcNow`); la `SystemPathResolver` lee variables
  de entorno (acoplamiento a `Environment`). Ninguna de las
  dos encaja en `Application` (que define interfaces, no
  implementaciones) ni en `Domain` (que no tiene
  dependencias de runtime).
- **Decisión:** se crean en `UI/System/`. El composition
  root (también en `UI`) las referencia; el resto de la
  app las recibe vía `IClock` e `IPathResolver`.
- **Excepción:** `SystemPathResolver` hace I/O
  (`Directory.CreateDirectory`). Esto se documenta en §10.
- **Alternativas consideradas** (y por qué se descartan):
  - `Infrastructure/System/`: obligaría a `UI` a
    referenciar a `Infrastructure` para resolver
    `SystemClock` y `SystemPathResolver`, lo cual es la
    dependencia que el composition root ya tiene. Es
    redundante tenerla dos veces.
  - `Application/System/`: viola la regla "Application no
    implementa, solo define interfaces".

### RN-3 — `Bootstrap` no se anota con `[CompositionRoot]` por accidente, sino por convención

- La anotación es semántica: cualquier reader de código
  entiende que "esta clase es el composition root del
  proyecto". El test de F-007 no la usa directamente (filtra
  por nombre), pero queda para documentación.
- Si en el futuro F-007 actualiza el test R-4 para usar
  el atributo en vez del nombre, la convención ya está
  establecida.

### RN-4 — El I/O de `SystemPathResolver` es una excepción documentada

- `SystemPathResolver` llama a `Directory.CreateDirectory`
  para asegurar que el directorio de datos existe. Esto es
  I/O en un constructor (o lazy initializer), lo cual rompe
  la regla general "no I/O en constructores".
- **Razón de la excepción:** la spec de
  `docs/architecture.md` §12 dice que el archivo de datos
  SQLite se crea en el directorio resuelto por
  `IPathResolver`. Si el directorio no existe, F-002 de
  Fase 2 fallaría. La forma más simple de garantizar que
  existe es crearlo en el constructor de
  `SystemPathResolver`. La alternativa (crear el directorio
  en el constructor de cada repositorio concreto) duplica
  la lógica.
- **Trade-off:** esto hace que `SystemPathResolver` no
  sea 100% puro (hace I/O). Si el I/O falla, la
  construcción de la clase falla, lo cual es aceptable
  (la app no puede arrancar sin directorio de datos).
- **Mitigación:** la implementación captura
  `UnauthorizedAccessException` y `IOException` y los
  loguea, pero deja que la excepción se propague (la app
  falla al arrancar con un mensaje claro).

### RN-5 — `IConfiguration` se carga pero no se usa todavía

- F-008 carga `appsettings.json` para que cuando F-012
  (configuración de tema, idioma, IA) llegue, el host
  ya esté listo para servir las settings.
- F-008 no usa ninguna setting. El archivo
  `appsettings.json` puede estar vacío o no existir.

---

## 9. Modelo de dominio

El composition root no introduce nuevas entidades de dominio.
Lo que añade es un modelo de **infraestructura de wiring**:

```
UI/
├── Composition/                                 ← nuevo (F-008)
│   ├── Bootstrap.cs                             ← clase central
│   └── CompositionRootAttribute.cs              ← marker
├── System/                                      ← nuevo (F-008)
│   ├── SystemClock.cs                           ← implementa IClock
│   └── SystemPathResolver.cs                    ← implementa IPathResolver
├── App.axaml                                    (F-001, modificado)
├── App.axaml.cs                                 (F-001, modificado)
└── Program.cs                                   (F-001, modificado)
```

**`Bootstrap` (clase):**
- Anotada con `[CompositionRoot]`.
- Método estático `ConfigureServices(IServiceCollection
  services, IConfiguration configuration)`.
- Registra `IClock`, `IPathResolver` y los servicios que
  vayan llegando (sin incluir las 15 interfaces de
  repositorio, que se registran en F-002 de Fase 2).

**`CompositionRootAttribute` (atributo):**
- `sealed class CompositionRootAttribute : Attribute`.
- `[AttributeUsage(AttributeTargets.Class, AllowMultiple = false,
  Inherited = false)]`.
- Marker puro, sin propiedades.

**`SystemClock` (clase):**
- `sealed class SystemClock : IClock`.
- `DateTime UtcNow => DateTime.UtcNow;`
- `DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);`

**`SystemPathResolver` (clase):**
- `sealed class SystemPathResolver : IPathResolver`.
- Resuelve la ruta del directorio de datos según el SO.
- Crea el directorio en el constructor (ver RN-4).

**`App.axaml.cs` (modificado):**
- Recibe `IServiceProvider` desde `Program.cs`.
- Resuelve `ILogger<App>`, `IClock`, `IPathResolver`.
- Emite "App starting" y "App stopping".

**`Program.cs` (modificado):**
- Usa `Host.CreateApplicationBuilder()`.
- Llama a `Bootstrap.ConfigureServices`.
- Construye el host y arranca Avalonia.

---

## 10. Configuración técnica

### 10.1 — Paquetes (sin cambios)

- `Microsoft.Extensions.Hosting` (ya en CPM desde F-001).
- `Microsoft.Extensions.DependencyInjection` (ya en CPM).
- `Microsoft.Extensions.Logging` (ya en CPM).
- `Microsoft.Extensions.Logging.Console` (ya en CPM).
- `Microsoft.Extensions.Configuration.Json` (para
  `AddJsonFile`). **¿Está en CPM?** Si no, F-008 lo añade.
  Verificar en `Directory.Packages.props`.

### 10.2 — `appsettings.json`

- Opcional. Si existe, se carga con `AddJsonFile`. Si no,
  el host funciona con defaults.
- Estructura inicial (vacía o con sección vacía):
  ```json
  {
    "$schema": "https://json.schemastore.org/appsettings.json",
    "Logging": {
      "LogLevel": {
        "Default": "Information",
        "Microsoft": "Warning"
      }
    }
  }
  ```
  F-012 expandirá esto con secciones de tema, idioma, IA.

### 10.3 — Convenciones de código

- File-scoped namespace (proyecto rule).
- `using` al tope (proyecto rule; bug de Fase 0 ya
  corregido).
- XML doc en cada miembro público (proyecto rule).
- `Bootstrap` es `public` y `static` (el método
  `ConfigureServices` no requiere instancia).
- `SystemClock` y `SystemPathResolver` son `sealed` y
  `public`. **Decisión a confirmar en §16.**

### 10.4 — Logging format

- Sink por defecto: consola.
- Formato: el de `Microsoft.Extensions.Logging` con
  `SimpleConsoleFormatter` (incluye timestamp, level,
  category, message). No se personaliza el format para F-008.

### 10.5 — Manejo de excepciones en `App.axaml.cs`

- Si el servicio no se puede resolver (e.g.,
  `Bootstrap` olvidó registrar `IClock`), la app falla
  al arrancar con `InvalidOperationException`. Esto es
  aceptable: indica un error de developer.
- No se captura la excepción globalmente en F-008. El
  `IExceptionHandler` global llega en F-012 o después.

---

## 11. Casos borde

### EC-1 — `Bootstrap` no se llama

- Si `Program.cs` olvida llamar a `Bootstrap.ConfigureServices`,
  el host construye sin servicios. `App.axaml.cs` intenta
  resolver `IClock` y obtiene `InvalidOperationException`.
- **Mitigación:** AC-2 de §14 exige que los servicios se
  resuelvan correctamente. Si el test falla, F-008 no se
  cierra.

### EC-2 — `SystemPathResolver` no puede crear el directorio

- Si `Directory.CreateDirectory` falla (permisos, disco
  lleno), la excepción se propaga y la app no arranca.
- **Decisión:** aceptable. F-008 no captura excepciones de
  I/O (no es su scope).

### EC-3 — `appsettings.json` malformado

- Si el archivo existe pero tiene JSON inválido, el host
  falla al arrancar con `JsonReaderException`. Esto es
  aceptable: el archivo malformado es un error de developer.
- Si no existe, el host funciona con defaults.

### EC-4 — Múltiples instancias de `Bootstrap`

- Si alguien (en el futuro) crea un segundo `Bootstrap`, el
  filtro de NetArchTest en R-4 lo captura. F-008 asume
  que solo hay uno.

### EC-5 — El test R-4 falla porque el filtro no funciona

- Si el filtro `.That().DoNotHaveName("Bootstrap")` no
  excluye correctamente al tipo, el test R-4 falla al
  introducir la dependencia. Esto se detecta en AC-3 de §14.
- **Mitigación:** si el filtro falla, ajustar el nombre o
  la API de NetArchTest. La spec no exige un nombre
  específico más allá de "Bootstrap"; la convención puede
  cambiar si NetArchTest no soporta `.DoNotHaveName()` para
  la versión elegida.

### EC-6 — Avalonia se cierra antes de emitir "App stopping"

- Si el proceso se mata con SIGKILL, el log no se emite.
  Esto es aceptable: F-008 emite el log en el evento de
  cierre normal de la ventana, no en un handler de
  `Process.Exit`.

### EC-7 — `Bootstrap` referencia algo fuera de UI/Infrastructure

- Si `Bootstrap` importa un tipo de `Application` o `Domain`,
  la app falla al compilar (dependencia directa, no
  indirecta). Esto es aceptable: `Application` y `Domain`
  son dependencias legítimas de `UI` (no del composition
  root, sino de toda la capa UI).
- Si `Bootstrap` importa un tipo de `Tests.*`, el test
  R-5 de F-007 falla. Esto es detectable.

### EC-8 — El usuario ejecuta la app en Windows, macOS o Linux

- `SystemPathResolver` resuelve la ruta según el SO. La
  app debe compilar y arrancar en los 3. **Verificación:**
  AC-5 de §14 exige verificar el path en al menos Windows
  (donde se ejecuta el dev), y documentar en la PR que
  se verificó el path esperado en macOS y Linux
  (manualmente o por code review).

---

## 12. Dependencias

### 12.1 Prerrequisitos de implementación

- **F-001 completada:** la solución y los 6 proyectos
  existen. `UI/Program.cs` y `UI/App.axaml.cs` existen
  con la ventana inline.
- **F-004 completada:** `IClock` e `IPathResolver` existen
  en `Application/Abstractions/`.
- **F-005 completada:** `Result<T>` y `PagedResult<T>`
  existen (no se usan en F-008, pero la convención
  de errores como valores está fijada).
- **F-006 completada:** las 15 interfaces de repositorio
  existen en `Application/Abstractions/`. F-008 NO las
  registra (lo hará F-002 de Fase 2).
- **F-007 completada:** el test R-4 tiene el filtro
  `.That().DoNotHaveName("Bootstrap")` listo para
  aceptar el composition root.

### 12.2 Dependencias con otras features

| Feature | Relación |
| --- | --- |
| F-001 | Prove `UI/Program.cs`, `UI/App.axaml.cs`, y los paquetes de Microsoft.Extensions. |
| F-004 | Prove `IClock` e `IPathResolver` que `Bootstrap` registra. |
| F-005 | No se usa directamente, pero el patrón de `Result<T>` se respeta si `Bootstrap` reporta errores. |
| F-006 | Prove las 15 interfaces de repositorio. F-008 NO las registra (queda para F-002 de Fase 2). |
| F-007 | El test R-4 ya tiene el filtro `Bootstrap` listo. F-008 introduce la dependencia `UI → Infrastructure` que el filtro excluye. |
| F-002 de Fase 2 (futuro) | Registrará los 15 repositorios concretos. F-008 deja listo el punto de extensión (`Bootstrap.ConfigureServices`). |
| F-012 (futuro) | Usará `IConfiguration` para tema, idioma e IA. F-008 deja el `IConfiguration` listo. |

### 12.3 Bloqueos

- **Ninguno.** F-008 puede implementarse ahora mismo. Los
  prerrequisitos están cumplidos.

---

## 13. Fuera de alcance (detallado)

Repetición con énfasis para evitar ambigüedad. Estos puntos
**NO** se implementan en F-008:

- **Implementaciones de los 15 repositorios.** F-002 de
  Fase 2 con EF Core + SQLite. F-008 solo deja el
  `Bootstrap.ConfigureServices(IServiceCollection)` listo
  para que F-002 los añada.
- **`MainWindow.axaml` y navegación entre vistas.** Eso
  llega con las features de Fase 3 (registrar/listar
  gastos, ingresos, etc.). F-008 mantiene la ventana
  en código.
- **Implementación de `IExchangeRateProvider`, `ILLMClient`,
  `IBackupService`, `ICsvImporter`, `ISettingsStore`,**
  y demás abstracciones de `architecture.md` §7. F-008
  solo registra `IClock`, `IPathResolver` e
  `ILogger<T>`. El resto llega con sus features
  correspondientes.
- **Persistencia (EF Core, migraciones, DbContext).** Eso
  es F-002 de Fase 2.
- **i18n, theming, recursos, `IStringLocalizer`.** Eso es
  F-012.
- **Notificaciones (bandeja, configuración del usuario).**
  Eso es F-017.
- **Backup, import CSV, export.** Eso son F-018, F-019 y
  F-020.
- **CI workflow más complejo.** El workflow existente ya
  corre `dotnet build`, `dotnet format`, y `dotnet test`.
  F-008 no requiere cambios.
- **Manejo global de excepciones (`IExceptionHandler`).**
  Eso es F-012 o posterior.
- **Tests adicionales para `Bootstrap`, `SystemClock`,
  `SystemPathResolver`.** F-008 no añade tests; la
  verificación es por AC-1 a AC-3 (la app arranca y
  emite los logs).
- **Cambios en `Domain`, `Application`, `Infrastructure`.**
  F-008 solo toca `UI`.
- **Cambios en los `.csproj` (excepto posiblemente añadir
  una `<ProjectReference>` de `UI` a `Infrastructure`
  para que `Bootstrap` pueda usar las implementaciones
  concretas de los repositorios cuando lleguen en F-002).**
  Ver §16 — decisión.

---

## 14. Criterios de aceptación

La feature se considera completada cuando **todos** los
siguientes puntos se cumplen.

### AC-1 — App arranca y emite "App starting"

- [ ] `dotnet build -c Release` desde la raíz: 0 warnings,
      0 errors.
- [ ] `dotnet run --project src/ControlDeFinanzasIA.UI`
      arranca la app.
- [ ] La ventana aparece (con título "ControlDeFinanzasIA"
      o el que se haya definido).
- [ ] En la consola (o en el output del IDE), aparece
      un log con el mensaje "App starting" (o un mensaje
      que lo incluya; ver §16 para la convención exacta).

### AC-2 — App se cierra y emite "App stopping"

- [ ] Al cerrar la ventana con la X (o Alt+F4), la app
      termina limpiamente.
- [ ] En la consola aparece un log con el mensaje
      "App stopping" (o equivalente).
- [ ] No hay excepciones inesperadas en la salida.

### AC-3 — Servicios resolubles desde `App.axaml.cs`

- [ ] `App.axaml.cs` resuelve `ILogger<App>`, `IClock`
      e `IPathResolver` desde el `IServiceProvider` del
      host.
- [ ] `IClock.UtcNow` devuelve la hora actual del sistema
      (con margen de error razonable; lo importante es
      que NO devuelve la hora de compilación ni un valor
      hardcodeado).
- [ ] `IClock.Today` devuelve la fecha actual como
      `DateOnly`.
- [ ] `IPathResolver.DataDirectory` devuelve una ruta que
      termina en `ControlDeFinanzasIA` y sigue la
      convención de `spec/constitution/tech-stack.md`
      para el SO actual.
- [ ] `IPathResolver.DatabaseFilePath` devuelve
      `Path.Combine(DataDirectory, "data.db")`.
- [ ] El directorio apuntado por `DataDirectory` existe
      después del arranque (porque `SystemPathResolver`
      lo crea).

### AC-4 — Composition root respeta la arquitectura

- [ ] `Bootstrap` está anotada con `[CompositionRoot]`.
- [ ] El test R-4 de F-007
      (`R4_UI_No_Referencia_A_Infrastructure_Salvo_CompositionRoot`)
      **sigue pasando** después de F-008.
- [ ] Ningún `new ConcreteClass()` aparece en
      `App.axaml.cs`. Todo se resuelve desde el
      `IServiceProvider`.
- [ ] `dotnet test` desde la raíz: 111 tests pasan
      (100 + 11 de F-007), 0 failures.

### AC-5 — Compatibilidad multiplataforma

- [ ] En Windows: `SystemPathResolver.DataDirectory`
      devuelve `%APPDATA%\ControlDeFinanzasIA\` (o su
      equivalente vía `Environment.GetFolderPath`).
- [ ] En macOS: `SystemPathResolver.DataDirectory`
      devuelve `~/Library/Application Support/ControlDeFinanzasIA/`
      (verificable por code review o por test manual).
- [ ] En Linux: `SystemPathResolver.DataDirectory`
      devuelve `$XDG_CONFIG_HOME/ControlDeFinanzasIA/`
      o `~/.config/ControlDeFinanzasIA/` (idem).
- [ ] La app compila en los 3 SO objetivo (verificable
      en CI; en local, basta con el SO del developer +
      un check de los path strings en el código).

### AC-6 — Configuración y logging

- [ ] `IConfiguration` está disponible en el container
      (vía `host.Services.GetRequiredService<IConfiguration>()`).
- [ ] Si existe `appsettings.json` al lado del binario,
      se carga sin errores.
- [ ] Si no existe, el host funciona con defaults (no
      falla).
- [ ] Los logs de `ILogger<T>` aparecen en la consola
      en `Debug` con el formato estándar de
      `Microsoft.Extensions.Logging`.

### AC-7 — Convenciones del repo

- [ ] File-scoped namespace en todos los archivos
      nuevos.
- [ ] `using` al tope en todos los archivos nuevos.
- [ ] XML doc en cada miembro público nuevo.
- [ ] Sin placeholders ni nombres de prueba
      (`SystemClock1`, `TestBootstrap`, etc.).
- [ ] `dotnet format style --verify-no-changes`: exit 0.

### AC-8 — Sin scope creep

- [ ] No se registran las 15 interfaces de repositorio
      (eso es F-002 de Fase 2).
- [ ] No se crea `MainWindow.axaml` ni navegación.
- [ ] No se añade ningún paquete NuGet nuevo (verificar
      `Directory.Packages.props` antes y después).
- [ ] No se modifica `Domain`, `Application`,
      `Infrastructure` (excepto posiblemente el `.csproj`
      de `UI` para añadir `<ProjectReference>` a
      `Infrastructure` si la convención de §RN-2 lo
      requiere; ver §16).
- [ ] No se modifica el CI workflow.
- [ ] El test R-4 de F-007 no se modifica (debe pasar
      sin cambios).

### AC-9 — Documentación y trazabilidad

- [ ] `spec/phases/phase-01/features.md` actualizado:
      F-008 marcado como ✅ COMPLETADA en la tabla
      resumen y en el header de la sección.
- [ ] La spec se renombra a
      `spec/features/phase-01/F-008-composition-root.done.md`.

---

## 15. Estructura de archivos esperada

```
src/
└── ControlDeFinanzasIA.UI/
    ├── Composition/                             ← nuevo (F-008)
    │   ├── Bootstrap.cs                         ← clase central
    │   └── CompositionRootAttribute.cs          ← marker
    ├── System/                                  ← nuevo (F-008)
    │   ├── SystemClock.cs                       ← implementa IClock
    │   └── SystemPathResolver.cs                ← implementa IPathResolver
    ├── App.axaml                                (F-001, sin cambios)
    ├── App.axaml.cs                             (F-001, modificado: usa IServiceProvider, emite logs)
    ├── Program.cs                               (F-001, modificado: arranca Host, llama a Bootstrap)
    ├── app.manifest                             (F-001, sin cambios)
    └── ControlDeFinanzasIA.UI.csproj            (modificado solo si se añade ProjectReference a Infrastructure; ver §16)
```

Si la convención de §RN-2 obliga a añadir
`<ProjectReference>` a `Infrastructure` en `UI.csproj`, el
`.csproj` se modifica mínimamente. Si la convención es
dejar las implementaciones triviales solo en `UI/System/`
y no referenciar `Infrastructure` desde `UI` (porque
todavía no hace falta), el `.csproj` no se modifica.

---

## 16. Decisiones de diseño explícitas

### 16.1 — Ubicación de `SystemClock` y `SystemPathResolver`

**Decisión:** `UI/System/`. Ver RN-2.

**Justificación:** la implementación trivial lee
`DateTime.UtcNow` y variables de entorno. No es lógica de
dominio (no es de `Application`) ni de infraestructura
pesada (no es de `Infrastructure` con EF Core). Es
**infraestructura de runtime trivial**, y la convención
más limpia es tenerla junto al composition root que la
usa.

**Alternativas descartadas:**
- `Infrastructure/System/`: obligaría a `UI` a
  referenciar a `Infrastructure` solo para
  `SystemClock` y `SystemPathResolver`, lo cual es
  innecesario.
- `Application/System/`: viola la regla "Application
  no implementa, solo define interfaces".

### 16.2 — `Bootstrap` como clase estática vs instanciable

**Decisión:** clase con método estático
`ConfigureServices(IServiceCollection, IConfiguration)`.
No se inyecta a sí misma ni se instancia.

**Justificación:** el composition root no tiene estado.
Un método estático es la forma más simple y evita
acoplamiento accidental. Si en el futuro hace falta
estado (e.g., un configuration object), se refactoriza
a una clase instanciable; por ahora, estática es
suficiente.

### 16.3 — `SystemClock` y `SystemPathResolver` como `public` o `internal`

**Decisión:** `public sealed class` para ambas.

**Justificación:** el convention del proyecto es
`public` para tipos de aplicación. `internal` se reserva
para detalles de implementación que no salen del assembly.
Como `UI` es un executable (no un class library), no
importa la visibilidad a otros assemblies, pero `public`
es la convención.

### 16.4 — `ProjectReference` de `UI` a `Infrastructure`

**Decisión recomendada:** NO añadir todavía. Razón:
F-008 no usa ninguna implementación de `Infrastructure`
(las implementaciones concretas de los repositorios
llegan en F-002 de Fase 2). Añadir la referencia
ahora introduciría una dependencia que no se usa, lo
cual es deuda técnica.

**Si F-002 de Fase 2 llega pronto:** se añade la
`<ProjectReference>` en F-002, no en F-008. F-008 se
limita a `UI` puro.

**Si F-008 necesitase algo de `Infrastructure`:** se
discute en una ADR antes de añadir la referencia. Por
ahora, F-008 no lo necesita.

**Implicación para R-4:** la regla
`R4_UI_No_Referencia_A_Infrastructure_Salvo_CompositionRoot`
sigue pasando trivialmente (UI no tiene la referencia,
y el filtro no aplica porque no hay `Bootstrap` que
importe `Infrastructure`). Cuando F-002 añada la
referencia, el filtro de `Bootstrap` (que SÍ importa
`Infrastructure`) entrará en juego.

**Si esto cambia y F-008 sí necesita la referencia:**
se documenta en el commit / PR con la justificación.

### 16.5 — Convenciones de mensajes de log

**Decisión:** los mensajes siguen la convención
"App starting" y "App stopping" en inglés, en
`PascalCase` con espacio (formato oración). Ejemplos:

- `_logger.LogInformation("App starting at {Time}, data directory: {Directory}", _clock.UtcNow, _pathResolver.DataDirectory);`
- `_logger.LogInformation("App stopping after {DurationMs} ms", elapsed);`

**Justificación:** mensajes concisos, en inglés
(convención de la comunidad .NET), con placeholders
estructurados (no string interpolation) para que los
sinks de log puedan parsearlos.

### 16.6 — Filtro de NetArchTest en R-4

**Decisión:** mantener el filtro
`.That().DoNotHaveName("Bootstrap")` tal como está en
F-007. No modificar el test.

**Justificación:** la convención "Bootstrap" ya está
implementada. Si la versión de NetArchTest no soporta
`.DoNotHaveName()` correctamente, se ajusta en F-007,
no en F-008.

**Verificación:** AC-4 de §14 exige que el test R-4
siga pasando sin cambios. Si no pasa, F-008 tiene un
problema que requiere discussion.

### 16.7 — Manejo del `IConfiguration`

**Decisión:** cargar `appsettings.json` con
`AddJsonFile("appsettings.json", optional: true)`.
No usar variables de entorno ni otras fuentes todavía.

**Justificación:** F-008 es la base. F-012 añadirá
más fuentes (env vars, args, etc.). Por ahora, JSON es
suficiente.

### 16.8 — `MainWindow` vs `Window` en código

**Decisión:** mantener `Window` en código (igual que en
F-001). No crear `MainWindow.axaml` ni
`MainWindow.axaml.cs`.

**Justificación:** la navegación entre vistas llega
después (Fase 3 en adelante). Crear `MainWindow.axaml`
ahora sin navegación sería re-trabajo. La ventana
inline sigue siendo el patrón correcto hasta que haya
navegación.

---

## 17. Riesgos y mitigaciones

| Riesgo | Probabilidad | Impacto | Mitigación |
| --- | --- | --- | --- |
| El filtro `.DoNotHaveName("Bootstrap")` de NetArchTest no excluye correctamente y R-4 falla. | Baja | Alto | AC-4 de §14 verifica que R-4 pasa. Si no, ajustar el nombre (a "Bootstrap" exacto coincide) o usar la API alternativa de NetArchTest para excluir por nombre de tipo. |
| `SystemPathResolver` falla al crear el directorio por permisos. | Media | Medio | Decisión documentada: la app no arranca con un mensaje claro. Aceptable para F-008. F-012+ podría añadir retry o fallback a `%TEMP%`. |
| `Host.CreateApplicationBuilder` cambia la API entre versiones de `Microsoft.Extensions.Hosting`. | Baja | Bajo | Pinning exacto en CPM. Si la API cambia, se actualiza en una PR. |
| El logger no aparece en la consola en `Release` por configuración por defecto. | Media | Bajo | Verificar AC-1 y AC-2 en Debug y Release. Si Release no muestra los logs, añadir `AddConsole` explícitamente al `HostBuilder`. |
| `appsettings.json` malformado rompe el arranque. | Baja | Bajo | `AddJsonFile(..., optional: true)` no falla si el archivo no existe. Si está malformado, sí falla; pero es un error de developer, no un caso de usuario. |
| El `IServiceProvider` no se pasa correctamente a Avalonia. | Media | Alto | AC-3 verifica que los servicios se resuelven. Si fallan, ajustar el patrón de inyección (e.g., usar `IHost.AvaloniaLifetime`). |
| `Bootstrap.ConfigureServices` se vuelve muy grande con el tiempo. | Baja | Bajo | F-008 lo deja con 2-3 servicios. Si en F-002+ crece, se subdivide (e.g., `Bootstrap.AddRepositories(...)`, `Bootstrap.AddLLMClients(...)`). F-008 no lo hace preventivamente. |
| El atributo `[CompositionRoot]` se aplica a clases que no son composition root. | Baja | Bajo | La convención es que solo `Bootstrap` lo usa. Code review lo detecta. No hay test automatizado para el atributo (F-007 usa el nombre). |
| `Program.cs` se vuelve difícil de leer con el host setup. | Baja | Bajo | Si pasa, refactorizar en una clase `ProgramBootstrap` que encapsule el setup. F-008 lo deja simple. |

---

## 18. Referencias

- `docs/vision.md` — para el nombre del proyecto y el
  contexto.
- `docs/architecture.md` §3 (solución y proyectos) —
  `UI` es el composition root.
- `docs/architecture.md` §4 (grafo de dependencias) —
  la excepción del composition root está documentada
  aquí.
- `docs/architecture.md` §9 (capa de UI) — el
  composition root vive en `App.axaml.cs` /
  `Program.cs`.
- `docs/architecture.md` §12 (ruta del archivo de datos)
  — `IPathResolver` resuelve según el SO.
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
- `spec/phases/phase-01/features.md` — F-008 dentro del
  desglose de la fase 1.
- `spec/features/phase-01/F-001-skeleton-solution.done.md`
  — provee `UI/Program.cs`, `UI/App.axaml.cs`, y la
  ventana inline.
- `spec/features/phase-01/F-004-Abstracciones-clock-pathresolver.done.md`
  — provee `IClock` e `IPathResolver` que `Bootstrap`
  registra.
- `spec/features/phase-01/F-005-comuntypes-result-pagedresult.done.md`
  — patrón de `Result<T>` (no se usa en F-008, pero
  relevante si `Bootstrap` reporta errores).
- `spec/features/phase-01/F-006-contrato-interfaces-repo.done.md`
  — provee las 15 interfaces (no se registran en F-008,
  se registran en F-002 de Fase 2).
- `spec/features/phase-01/F-007-validacion-arquitectonica.done.md`
  — provee el test R-4 con el filtro `Bootstrap` listo
  para F-008.
- `spec/README.md` — convención del directorio `spec/`.
- [Microsoft.Extensions.Hosting — documentación
  oficial](https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host)
  (a consultar vía Context7 al implementar, para
  confirmar la API de la versión elegida).
- [Avalonia con Generic Host — guía oficial](https://docs.avaloniaui.net/docs/guides/dependency-injection/)
  (a consultar vía Context7 al implementar, para
  confirmar el patrón de integración Avalonia + .NET
  Host).
