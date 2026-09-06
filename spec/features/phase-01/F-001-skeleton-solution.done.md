# Specification — F-001: Esqueleto de solución y proyectos por capa

> **Fase:** 1 — Esqueleto de arquitectura y contratos base
> **Feature:** F-001
> **Estado:** Pendiente de implementación
> **Spec ID:** SPEC-F001
>
> Fuentes de verdad: `docs/vision.md`, `docs/architecture.md`,
> `docs/roadmap.md`, `spec/constitution/tech-stack.md`,
> `spec/constitution/mission.md`,
> `spec/features/phase-01-features.md` (F-001).

---

## 1. Resumen

Crear la solución .NET 8 con los seis proyectos definidos en
`architecture.md` §3, las referencias entre proyectos según
`architecture.md` §4, la configuración global de compilación
centralizada, las reglas de formato y estilo, y la app Avalonia
arrancando con una ventana vacía en los tres sistemas operativos
objetivo.

Al final de la feature, `dotnet build` debe pasar sin warnings,
`dotnet test` debe ejecutarse (aunque no haya tests aún) y la
aplicación de UI debe arrancar y mostrar una `MainWindow` vacía.

---

## 2. Contexto

F-001 es la primera feature del proyecto. Sin este esqueleto:

- Ninguna otra feature de la fase 1 (F-002 a F-008) puede
  implementarse.
- El grafo de dependencias entre proyectos queda implícito y se
  rompe sin que nada lo detecte hasta que el daño ya está hecho.
- Las decisiones de versionado de paquetes y de configuración de
  build quedan dispersas y son imposibles de cambiar de forma
  consistente.

Construir el esqueleto con la configuración correcta desde el día
uno es lo que hace que las features siguientes sean incrementales
(añadir tipos, añadir referencias internas, añadir tests) en vez de
reorganizaciones (mover archivos, dividir proyectos, romper
referencias).

---

## 3. Objetivos

- Crear la solución `ControlDeFinanzasIA.sln`.
- Crear los seis proyectos C# en el layout definido (ver §6), cada
  uno con su `.csproj` correctamente configurado.
- Configurar las referencias entre proyectos según
  `architecture.md` §4.
- Centralizar la configuración común de compilación en
  `Directory.Build.props`.
- Centralizar la versión de paquetes NuGet en
  `Directory.Packages.props` (Central Package Management).
- Activar reglas de formato y estilo via `.editorconfig` y
  StyleCop.Analyzers.
- Configurar la app Avalonia para que arranque con una `MainWindow`
  vacía.
- Verificar que todo compila sin warnings, que `dotnet test` corre y
  que la app arranca en Windows, macOS y Linux.

---

## 4. No-objetivos (fuera de alcance)

Lo siguiente **no** se hace en esta feature; queda diferido a
features posteriores:

- **Código de dominio, aplicación o infraestructura.** Los proyectos
  `Domain`, `Application` e `Infrastructure` quedan vacíos (sin tipos
  propios, salvo los que impongan las plantillas, ej. `Class1.cs`
  debe eliminarse).
- **Composición de DI y logging.** Eso es F-008. F-001 deja la
  app arrancando con la plantilla por defecto de Avalonia.
- **Tests.** Ningún test en esta feature. F-007 introduce
  NetArchTest.
- **Internacionalización, theming, recursos.** F-012 introduce
  `IStringLocalizer` y los temas. F-001 usa los valores por defecto
  de Avalonia.
- **Publicación self-contained e instaladores.** F-001 solo verifica
  que la app arranca en modo `dotnet run` / IDE.
- **Empaquetado para distribución, firma, code signing.** Diferido a
  la fase 21 del roadmap.
- **CI más allá de build y test básicos.** El pipeline mínimo (que
  ya existe según la fase 0) se mantiene; ningún job nuevo se añade
  en esta feature.

---

## 5. Stack y versiones

Versiones objetivo para F-001. Quedan fijadas en
`Directory.Packages.props` (Central Package Management):

| Paquete | Versión objetivo | Notas |
| --- | --- | --- |
| `Avalonia` | última estable 11.x | Incluye base. |
| `Avalonia.Desktop` | última estable 11.x | Habilita el target desktop. |
| `Avalonia.Themes.Fluent` | última estable 11.x | Tema por defecto. |
| `Avalonia.Fonts.Inter` (opcional) | última estable | Solo si se decide usar. |
| `Microsoft.Extensions.Hosting` | 8.x | Para F-008; instalado ya en F-001 para evitar doble cambio. |
| `Microsoft.Extensions.DependencyInjection` | 8.x | Idem. |
| `Microsoft.Extensions.Logging` | 8.x | Idem. |
| `Microsoft.Extensions.Logging.Console` | 8.x | Idem. |
| `StyleCop.Analyzers` | última estable | Análisis de estilo. |
| `xUnit` y familia (`xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`) | últimas estables | Para F-007, pero preinstalado. |
| `NetArchTest.Rules` | última estable | Idem. |

**Decisiones de versionado:**

- **Central Package Management (CPM) activado.** Todos los proyectos
  consumen versiones de `Directory.Packages.props`. No se permite
  `<PackageReference Version="...">` en `.csproj` salvo para
  dependencias internas al repo (project references), que no usan
  CPM.
- **Pinning exacto de versiones** (no `^` ni rangos flotantes). Las
  actualizaciones son intencionales y se hacen en commits separados
  con su propia revisión.
- **.NET 8 LTS** como `TargetFramework` en todos los proyectos
  (`net8.0`).

**Decisión abierta a confirmar durante la implementación (ver §11):**
¿Se prefiere Avalonia 11.x o Avalonia 12.x si está disponible al
momento de implementar? El spec asume 11.x por compatibilidad y
documentación; cambiar a 12.x es factible si la migración es
documentada en la PR.

---

## 6. Layout de archivos esperado

```
ControlDeFinanzasIA/                                  (raíz del repo)
├── ControlDeFinanzasIA.sln
├── Directory.Build.props                             (config MSBuild común)
├── Directory.Packages.props                          (CPM)
├── .editorconfig                                     (formato, naming, severidad)
├── .gitignore                                        (estándar .NET + binarios Avalonia)
├── README.md                                         (mínimo: nombre + cómo compilar)
├── src/
│   ├── ControlDeFinanzasIA.Domain/
│   │   └── ControlDeFinanzasIA.Domain.csproj
│   ├── ControlDeFinanzasIA.Application/
│   │   └── ControlDeFinanzasIA.Application.csproj
│   ├── ControlDeFinanzasIA.Infrastructure/
│   │   └── ControlDeFinanzasIA.Infrastructure.csproj
│   └── ControlDeFinanzasIA.UI/
│       ├── ControlDeFinanzasIA.UI.csproj
│       ├── Program.cs
│       ├── App.axaml
│       └── App.axaml.cs
└── tests/
    ├── ControlDeFinanzasIA.Tests.Unit/
    │   └── ControlDeFinanzasIA.Tests.Unit.csproj
    └── ControlDeFinanzasIA.Tests.Integration/
        └── ControlDeFinanzasIA.Tests.Integration.csproj
```

**Decisiones de layout:**

- **`src/` y `tests/` separan código de tests.** Convención común en
  proyectos .NET; permite a CI excluir `tests/` de builds de
  release.
- **No se crean carpetas internas dentro de los proyectos** (salvo
  las que imponga la propia plantilla, p. ej. `Properties/`). Las
  carpetas internas (Entities/, ValueObjects/, etc.) llegan con las
  features F-002 en adelante.
- **No se incluye `global.json`** en esta feature. Si en el futuro
  hace falta fijar la versión del SDK, se añade como cambio
  independiente.

---

## 7. Configuración de compilación centralizada

### 7.1 `Directory.Build.props`

Debe contener, como mínimo, los siguientes `PropertyGroup` y
`ItemGroup`:

**`PropertyGroup` común a todos los proyectos:**

- `TargetFramework` = `net8.0`.
- `LangVersion` = `latest` (resuelve a C# 12 con .NET 8).
- `Nullable` = `enable`.
- `ImplicitUsings` = `enable`.
- `TreatWarningsAsErrors` = `true`.
- `EnforceCodeStyleInBuild` = `true` (para que las reglas de
  `.editorconfig` se apliquen en build).
- `AnalysisLevel` = `latest` (reglas del compilador al día).
- `NoWarn` con un set mínimo y justificado. **Por defecto vacío.**
  Si hace falta suprimir alguna regla, debe documentarse con un
  comentario en el `.csproj` que la usa.
- `Authors`, `Product`, `Company`, `Copyright` (placeholders
  razonables; pueden ajustarse en otra feature).

**Sobrescrituras por proyecto (en el `.csproj` correspondiente):**

- `UI`: `OutputType` = `Exe`, `UseAvaloniaApp` = `true`,
  `ApplicationManifest` apuntando al manifest estándar.
- `Tests.Unit` y `Tests.Integration`: `IsPackable` = `false`,
  `IsTestProject` = `true` (reconocido por Microsoft.NET.Test.Sdk).

### 7.2 `Directory.Packages.props`

- `ManagePackageVersionsCentrally` = `true`.
- `<PackageVersion Include="..." Version="..." />` para cada paquete
  listado en §5.
- Los `.csproj` referencian paquetes con `<PackageReference
  Include="..." />` sin atributo `Version`.

### 7.3 `.editorconfig`

Debe incluir al menos:

- `root = true` en la primera línea.
- Sección `[*]` con: charset utf-8, end_of_line lf, insert_final_newline
  true, trim_trailing_whitespace true, indent_style space,
  indent_size 4.
- Sección `[*.{csproj,props,targets}]` con indent_size 2 (estándar de
  archivos MSBuild).
- Sección `[*.{json,yml,yaml,xml}]` con indent_size 2.
- Sección `[*.cs]` con las convenciones C# de .NET:
  - `csharp_style_namespace_declarations = file_scoped:warning`.
  - `csharp_using_directive_placement = outside_namespace:warning`.
  - `dotnet_style_namespace_match_folder = true:warning`.
  - Reglas de naming:
    - Tipos y miembros públicos: PascalCase.
    - Parámetros y locales: camelCase.
    - Campos privados: `_camelCase` con prefijo guión bajo.
    - Constantes: PascalCase.
    - Interfaces: prefijo `I`.
  - Reglas de formato:
    - Llaves en línea nueva (Allman) o en la misma línea (K&R):
      **K&R** (llave de apertura en la misma línea).
    - `csharp_new_line_before_open_brace` = all (excepto para
      accessors, object/collection initializers, lambdas).
- Sección `[*.axaml]` con indent_size 2 (estándar XAML).

### 7.4 StyleCop

- `StyleCop.Analyzers` referenciado en cada proyecto de código
  (no en los de tests, donde puede ser ruidoso y se prefiere
  concentrar en reglas de `.editorconfig`).
- `stylecop.json` en la raíz con la configuración por defecto
  (`"settings": {}` está bien para empezar).
- `GenerateDocumentationFile` se deja **off** en F-001. Se activará
  cuando haya API pública que documentar (probablemente en F-005 o
  F-006).

### 7.5 `.gitignore`

El estándar de .NET (`dotnet new gitignore`) más:

- `**/bin/`, `**/obj/`.
- `*.user`, `*.suo`.
- `.vs/`, `.idea/`, `.vscode/` (con whitelist de `.vscode/launch.json`
  y `.vscode/tasks.json` si se decide versionar).
- `*.db`, `*.db-wal`, `*.db-shm` (archivos SQLite locales que
  aparecerán desde la fase 2).
- `app-*.log` (logs de la app que aparecerán desde la fase 12).

---

## 8. Proyectos y referencias

### 8.1 `ControlDeFinanzasIA.Domain`

- **Tipo:** Class library, `net8.0`.
- **Dependencias NuGet:** ninguna. Solo BCL.
- **Project references:** ninguna.
- **Contenido:** carpetas vacías (sin archivos de código). Las
  carpetas `Entities/`, `ValueObjects/`, etc. se crean con sus
  primeras features (F-002, F-003).
- **Regla arquitectónica:** no puede referenciar a nadie más que
  BCL. Lo validará NetArchTest en F-007.

### 8.2 `ControlDeFinanzasIA.Application`

- **Tipo:** Class library, `net8.0`.
- **Dependencias NuGet:** `FluentValidation` (anunciado en
  `architecture.md`; instalado en F-001 para no tener que tocar el
  `.csproj` más adelante, aunque aún no se use).
- **Project references:** `ControlDeFinanzasIA.Domain`.
- **Contenido:** vacío.
- **Regla:** no puede referenciar a `Infrastructure` ni a `UI`. Lo
  valida NetArchTest.

### 8.3 `ControlDeFinanzasIA.Infrastructure`

- **Tipo:** Class library, `net8.0`.
- **Dependencias NuGet:** ninguna en F-001 (EF Core y SQLite se
  añaden en F-002, que es la primera que los usa). Esto evita
  instalar paquetes no usados y mantenerlos actualizados sin
  necesidad.
- **Project references:** `ControlDeFinanzasIA.Application` (que
  arrastra transitivamente a `Domain`).
- **Contenido:** vacío.
- **Regla:** puede referenciar a `Application` y `Domain`, y a
  paquetes externos (BCL, EF Core, etc.).

### 8.4 `ControlDeFinanzasIA.UI`

- **Tipo:** Executable, `net8.0`, target Avalonia Desktop.
- **Dependencias NuGet:**
  - `Avalonia`.
  - `Avalonia.Desktop`.
  - `Avalonia.Themes.Fluent`.
  - `Microsoft.Extensions.Hosting`.
  - `Microsoft.Extensions.DependencyInjection`.
  - `Microsoft.Extensions.Logging`.
  - `Microsoft.Extensions.Logging.Console`.
- **Project references:** `ControlDeFinanzasIA.Application`,
  `ControlDeFinanzasIA.Domain`. **No** referencia a `Infrastructure`
  directamente en F-001 (F-008 introduce esa referencia en el
  composition root como excepción explícita).
- **Contenido:**
  - `Program.cs`: entry point. Crea un `AppBuilder` de Avalonia
    usando `App.BuildAvaloniaApp()` (estilo plantilla oficial de
    Avalonia 11) y arranca con `StartWithClassicDesktopLifetime`.
  - `App.axaml`: declara `Application` raíz y carga el tema Fluent.
  - `App.axaml.cs`: clase parcial de `App`, sin lógica (un override
    vacío de `OnFrameworkInitializationCompleted` que crea una
    `MainWindow` y la asigna a `ApplicationLifetime.MainWindow`).
  - **No** se añade `MainWindow.axaml` ni `MainWindow.axaml.cs` en
    F-001. Una ventana vacía creada en código en
    `App.axaml.cs` es suficiente para verificar el arranque y se
    elimina en features siguientes cuando la app tenga navegación.
- **Regla:** puede referenciar a `Application` y `Domain`. La
  referencia a `Infrastructure` (cuando llegue) debe limitarse al
  composition root y documentarse en `architecture.md`.

### 8.5 `ControlDeFinanzasIA.Tests.Unit`

- **Tipo:** xUnit test project, `net8.0`, `IsTestProject=true`.
- **Dependencias NuGet:**
  - `Microsoft.NET.Test.Sdk`.
  - `xunit`.
  - `xunit.runner.visualstudio` (paquete de runners, no de
    aserciones).
  - `NetArchTest.Rules` (instalado aunque F-007 sea la feature que
    los use; así no hay que tocar el `.csproj` después).
- **Project references:** `ControlDeFinanzasIA.Domain`,
  `ControlDeFinanzasIA.Application`.
- **Contenido:** un único test placeholder (`Skip = "F-001
  placeholder"` para que se cuente como verde pero no falle nada).
  Se elimina en F-002 cuando se añada el primer test real.

### 8.6 `ControlDeFinanzasIA.Tests.Integration`

- **Tipo:** xUnit test project, `net8.0`, `IsTestProject=true`.
- **Dependencias NuGet:** mismas que `Tests.Unit`, más
  `Microsoft.EntityFrameworkCore.Sqlite` (se añade en F-002; en
  F-001 todavía no).
- **Project references:** `ControlDeFinanzasIA.Infrastructure`
  (arrastra transitivamente a `Application` y `Domain`).
- **Contenido:** un único test placeholder `Skip`-eado, igual que
  `Tests.Unit`.

### 8.7 Referencia a la solución

`ControlDeFinanzasIA.sln` debe:

- Listar los 6 proyectos en el orden del layout (§6).
- Configurar `Debug | Any CPU` y `Release | Any CPU` como
  configuraciones por defecto.
- **No** debe incluir `x86`, `x64`, `ARM`, etc. como
  configuraciones manuales: las plataformas se gestionan vía
  RIDs/TargetFramework al publicar (feature 21), no en la solución.
- Carpeta virtual `src/` y carpeta virtual `tests/` para agrupar
  visualmente los proyectos en el IDE.

---

## 9. App Avalonia — estructura mínima

`App.axaml` declara el `Application` con el tema Fluent de Avalonia.
`App.axaml.cs` construye el `AppBuilder` (vía el método estático
estándar `BuildAvaloniaApp`) y, en
`OnFrameworkInitializationCompleted`, instancia una ventana vacía
(`Window { Title = "ControlDeFinanzasIA"; }`) y la asigna a
`ApplicationLifetime.MainWindow`. Sin lógica de navegación, sin
DI, sin logging propio: eso es F-008.

No se añade `MainWindow.axaml` porque no hay XAML que la justifique
en F-001; todo se hace en código. Cuando llegue la navegación
(generalmente con la feature 3), `MainWindow.axaml` aparecerá con su
layout de shell.

---

## 10. Criterios de aceptación

La feature se considera completada **solo si todos** los siguientes
puntos se cumplen. Cada punto es verificable de forma mecánica o
visual.

### 10.1 Compilación

- [ ] `dotnet build` desde la raíz pasa con código de salida `0`.
- [ ] `dotnet build` no emite **ningún warning** en los 6 proyectos
      con la configuración por defecto.
- [ ] `dotnet build -c Release` también pasa sin warnings.
- [ ] Todos los proyectos compilan con `Nullable=enable` (verificable
      inspeccionando los `.csproj` o el `Directory.Build.props`).

### 10.2 Tests

- [ ] `dotnet test` ejecuta los proyectos `Tests.Unit` y
      `Tests.Integration` sin errores.
- [ ] Los tests placeholder están marcados como `Skip` (no fallan,
      no pasan: skipped), y se documenta que se eliminarán en F-002.
- [ ] El runner xUnit aparece en la salida (`xUnit.net` o similar).

### 10.3 Arranque de la app

- [ ] `dotnet run --project src/ControlDeFinanzasIA.UI` lanza la
      app y muestra una ventana con el título "ControlDeFinanzasIA".
- [ ] La ventana se puede cerrar y el proceso termina limpiamente.
- [ ] El log de stdout no contiene excepciones ni stack traces
      inesperados.
- [ ] El comportamiento es equivalente en **Windows**, **macOS** y
      **Linux** (un developer de cada OS verifica; en CI solo
      build).

### 10.4 Grafo de referencias

- [ ] Inspección visual de los `.csproj`: las referencias coinciden
      con la tabla de §8.
- [ ] `Domain` no tiene project references.
- [ ] `Application` solo tiene `Domain`.
- [ ] `UI` tiene `Application` y `Domain`, **no** tiene
      `Infrastructure` (todavía).
- [ ] Las referencias se verifican también con `dotnet list
      reference` y su output se incluye en la PR.

### 10.5 Convenciones de estilo

- [ ] `.editorconfig` está en la raíz y todas las secciones listadas
      en §7.3 están presentes.
- [ ] `stylecop.json` está en la raíz y es referenciado por los
      `.csproj` que corresponda (en F-001: los 4 proyectos de `src/`).
- [ ] Una violación intencional (p. ej. una variable pública en
      camelCase) **produce un warning de build** (porque
      `TreatWarningsAsErrors=true` la convierte en error).
- [ ] `dotnet format --verify-no-changes` (o equivalente) no
      propone cambios sobre los archivos creados.

### 10.6 Layout

- [ ] La estructura de §6 coincide exactamente con `Get-ChildItem
      -Recurse` (excluyendo `bin/`, `obj/` y `.git/`).
- [ ] No hay archivos `Class1.cs` ni similares heredados de la
      plantilla de Class Library.
- [ ] `README.md` contiene al menos: nombre del proyecto, comando
      para compilar (`dotnet build`) y comando para ejecutar
      (`dotnet run --project src/ControlDeFinanzasIA.UI`).

### 10.7 CI

- [ ] El pipeline de CI (introducido en la fase 0) sigue pasando
      con la nueva solución.
- [ ] El pipeline corre tanto `dotnet build` como `dotnet test`.
- [ ] El job de build no aumenta su tiempo de ejecución de forma
      significativa (< 2x respecto a la fase 0).

---

## 11. Decisiones a confirmar durante la implementación

Puntos que están definidos con un default pero admiten cambio
justificado durante la implementación. Cada uno se documenta en la
PR correspondiente si se cambia.

1. **Versión exacta de Avalonia.** Default: última 11.x estable.
   ¿Se prefiere 12.x si está disponible?
2. **`UseAvaloniaApp` vs configuración manual del SDK.** Default:
   `<UseAvaloniaApp>true</UseAvaloniaApp>` en `UI.csproj`. Verifica
   que es la sintaxis correcta en la versión de Avalonia elegida.
3. **Versión exacta de StyleCop y severidad.** Default: configuración
   por defecto del paquete. Si genera demasiados falsos positivos en
   código Avalonia, se puede relajar solo para `UI`.
4. **`README.md` mínimo o detallado.** Default: mínimo (nombre +
   comandos). Un README detallado se hará en la fase 21 (cierre).
5. **Versión del SDK de .NET.** ¿Se fija con `global.json`? Default:
   no se fija en F-001. Si el developer tiene varios SDKs y
   compila con uno incorrecto, se añadirá `global.json` como fix
   posterior.
6. **`.vscode/` y `.idea/` en el repo.** Default: ignorados. Se
   pueden añadir `launch.json` y `tasks.json` versionados si el
   equipo lo decide; no es parte de F-001.

---

## 12. Riesgos y mitigaciones

| Riesgo | Probabilidad | Impacto | Mitigación |
| --- | --- | --- | --- |
| Plantilla de Avalonia cambia entre versiones y el `Program.cs` esperado no compila. | Media | Bajo | Fijar versión exacta en `Directory.Packages.props` y verificar contra la documentación de la versión elegida. |
| `TreatWarningsAsErrors=true` rompe por warnings de paquetes externos (ej. analizadores con bugs). | Media | Medio | Permitir `NoWarn` solo en `.csproj` afectados y con comentario que justifique. No relajar la regla global. |
| El `Directory.Packages.props` no se aplica a un `.csproj` (error de novato). | Baja | Bajo | Validar con `dotnet restore` y `dotnet list package` que las versiones se resuelven centralizadamente. |
| La plantilla de `dotnet new` agrega un `Class1.cs` y un `Usings.cs` que contaminan el repo. | Alta | Bajo | Eliminar manualmente después de crear cada proyecto. Listado en criterios de aceptación. |
| `dotnet run` falla en Linux por falta de dependencias nativas (X11/Wayland). | Media | Bajo | Documentar dependencias en el README; no se valida en CI, sí manualmente. |
| El developer usa una ruta con espacios o tildes y el build falla por algún analizador. | Baja | Bajo | Usar `git` con paths cortos; documentar en el README que la ruta recomendada no tiene espacios. |
| `StyleCop.Analyzers` entra en conflicto con `.editorconfig` y se duplican reglas. | Baja | Medio | Revisar la primera ejecución de build; documentar cualquier deshabilitación de regla. |

---

## 13. Verificación final (checklist de cierre)

Antes de marcar F-001 como completada:

- [ ] Todos los puntos de §10 verificados.
- [ ] PR abierta con descripción que referencia esta spec
      (`SPEC-F001`).
- [ ] Capturas o nota textual confirmando el arranque de la app en
      los 3 SO objetivo.
- [ ] Actualizar `spec/features/phase-01-features.md` si alguna
      decisión de F-001 cambia el alcance (no debería).
- [ ] Mover esta spec a `spec/features/phase-01/F-001.done.md` o
      marcar su estado como `Completada` en la cabecera.

---

## 14. Referencias

- `docs/vision.md` — qué se construye.
- `docs/architecture.md` — §3 (solución y proyectos), §4 (grafo de
  dependencias).
- `docs/roadmap.md` — Fase 1, criterios de cierre.
- `spec/constitution/mission.md` — principios.
- `spec/constitution/tech-stack.md` — versiones y reglas de
  paquetes.
- `spec/features/phase-01-features.md` — F-001 dentro del
  desglose de la fase.
- Documentación oficial de Avalonia 11.x (a consultar vía Context7
  cuando se implemente).
- Documentación oficial de Central Package Management en .NET 8
  (a consultar vía Context7 cuando se implemente).
