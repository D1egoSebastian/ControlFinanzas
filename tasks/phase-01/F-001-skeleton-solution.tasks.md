# Tareas — F-001: Esqueleto de solución y proyectos por capa

> **Spec:** `spec/features/phase-01/F-001-skeleton-solution.md`
> **Fuentes de verdad:** la spec, `docs/architecture.md`, `docs/vision.md`.
>
> Cada tarea es implementable en menos de 2 horas. El proyecto debe
> seguir compilando al finalizar cada una. No se escribe código de
> aplicación en esta fase; solo configuración, archivos de proyecto y
> una `MainWindow` mínima.

---

## Resumen de tareas

| ID | Tarea | Estimación | Depende de |
| --- | --- | --- | --- |
| T-001 | Crear archivo de solución y estructura de carpetas | S | — |
| T-002 | Crear `Directory.Build.props` con settings comunes | S | T-001 |
| T-003 | Crear `Directory.Packages.props` con Central Package Management | S | T-001 |
| T-004 | Crear `.gitignore` estándar .NET + extras del proyecto | XS | T-001 |
| T-005 | Crear `.editorconfig` con reglas de formato y naming | M | T-001 |
| T-006 | Crear `stylecop.json` y `README.md` mínimo | XS | T-001 |
| T-007 | Crear proyecto `Domain` y añadirlo a la solución | S | T-002, T-003 |
| T-008 | Crear proyecto `Application` con referencia a `Domain` | S | T-007 |
| T-009 | Crear proyecto `Infrastructure` con referencia a `Application` | S | T-008 |
| T-010 | Crear proyecto `UI` con plantilla Avalonia y referencias | M | T-008 |
| T-010a | Añadir verificación de estilo al pipeline de CI | XS | T-010 |
| T-011 | Crear proyecto `Tests.Unit` con referencias a `Domain` y `Application` | S | T-008 |
| T-012 | Crear proyecto `Tests.Integration` con referencia a `Infrastructure` | S | T-009 |
| T-013 | Añadir tests placeholder `Skip`-eados en ambos proyectos de tests | XS | T-011, T-012 |
| T-014 | Verificación final de compilación, tests y arranque | M | todas |

**Total estimado:** 1–2 jornadas. T-005 y T-010 son las más largas;
el resto cabe en 30–60 minutos.

---

## T-001 — Crear archivo de solución y estructura de carpetas

**Objetivo**
Tener la solución vacía y el layout de carpetas `src/` y `tests/`
listo para empezar a poblar con proyectos.

**Descripción**
Desde la raíz del repo, ejecutar `dotnet new sln` para crear
`ControlDeFinanzasIA.sln`. Crear manualmente las carpetas `src/`
y `tests/`. No se añade ningún proyecto todavía; eso entra en las
tareas T-007 a T-012.

Verificar que el archivo `.sln` se generó correctamente y que
contiene el header estándar de una solución de .NET 8.

**Dependencias**
- Ninguna. Es la primera tarea.

**Archivos / proyectos afectados**
- `ControlDeFinanzasIA.sln` (nuevo).

**Criterios de finalización**
- El archivo `ControlDeFinanzasIA.sln` existe en la raíz.
- `dotnet sln list` ejecutado desde la raíz no lista proyectos
  (solución vacía, esperado en este punto).
- Las carpetas `src/` y `tests/` existen.
- `dotnet build` desde la raíz pasa (soluciones vacías compilan).

---

## T-002 — Crear `Directory.Build.props` con settings comunes

**Objetivo**
Centralizar las propiedades MSBuild comunes a todos los proyectos
para que no haya que repetirlas en cada `.csproj`.

**Descripción**
Crear `Directory.Build.props` en la raíz con un `PropertyGroup` que
contenga, como mínimo, las propiedades listadas en la sección 7.1
de la spec: `TargetFramework=net8.0`, `Nullable=enable`,
`ImplicitUsings=enable`, `LangVersion=latest`,
`TreatWarningsAsErrors=true`, `EnforceCodeStyleInBuild=true`,
`AnalysisLevel=latest`, y los metadatos del paquete
(`Authors`, `Product`, `Company`, `Copyright`).

Dejar `NoWarn` vacío en esta tarea. Si una tarea futura necesita
suprimir un warning, lo hará localmente en su `.csproj` con un
comentario que lo justifique.

**Dependencias**
- T-001 (la solución debe existir).

**Archivos / proyectos afectados**
- `Directory.Build.props` (nuevo).

**Criterios de finalización**
- El archivo existe y es XML válido.
- `dotnet build` desde la raíz sigue pasando (no hay proyectos
  todavía, pero la compilación implícita del directorio no debe
  fallar).
- Inspección manual confirma que las propiedades listadas están
  presentes y escritas correctamente.

---

## T-003 — Crear `Directory.Packages.props` con Central Package Management

**Objetivo**
Fijar las versiones de todos los paquetes NuGet que el proyecto va
a usar, gestionados de forma centralizada, para evitar drift entre
proyectos.

**Descripción**
Crear `Directory.Packages.props` en la raíz con
`ManagePackageVersionsCentrally=true` y los `<PackageVersion
Include="..." Version="..." />` para los paquetes listados en la
sección 5 de la spec:

- Avalonia y derivados (Avalonia, Avalonia.Desktop,
  Avalonia.Themes.Fluent).
- Microsoft.Extensions.Hosting,
  Microsoft.Extensions.DependencyInjection,
  Microsoft.Extensions.Logging,
  Microsoft.Extensions.Logging.Console.
- StyleCop.Analyzers.
- xUnit y familia (xunit, xunit.runner.visualstudio,
  Microsoft.NET.Test.Sdk).
- NetArchTest.Rules.
- FluentValidation.

Fijar versiones exactas, sin rangos. Consultar la documentación
oficial de cada paquete (vía Context7) para confirmar la última
estable compatible con .NET 8 al momento de implementar.

**Dependencias**
- T-001.

**Archivos / proyectos afectados**
- `Directory.Packages.props` (nuevo).

**Criterios de finalización**
- El archivo existe y es XML válido.
- `dotnet restore` desde la raíz pasa (aunque no haya proyectos
  todavía, el restore valida el archivo).
- Inspección manual confirma que cada paquete de la tabla de la
  sección 5 de la spec está presente con versión exacta.

---

## T-004 — Crear `.gitignore` estándar .NET + extras del proyecto

**Objetivo**
Asegurar que los archivos generados, dependencias locales y
archivos de base de datos que aparecerán en fases futuras no se
commitean por accidente.

**Descripción**
Generar `.gitignore` con el contenido del comando `dotnet new
gitignore`, complementado con las exclusiones listadas en la
sección 7.5 de la spec: `**/bin/`, `**/obj/`, `*.user`, `*.suo`,
`.vs/`, `.idea/`, `.vscode/`, `*.db`, `*.db-wal`, `*.db-shm` y
`app-*.log`.

**Dependencias**
- T-001.

**Archivos / proyectos afectados**
- `.gitignore` (nuevo).

**Criterios de finalización**
- El archivo existe.
- `git status` desde la raíz no muestra archivos de build
  (`bin/`, `obj/`) incluso después de ejecutar `dotnet build`.

---

## T-005 — Crear `.editorconfig` con reglas de formato y naming

**Objetivo**
Fijar las convenciones de formato, naming y severidad de análisis
para que el equipo (y el build) las apliquen de forma consistente.

**Descripción**
Crear `.editorconfig` en la raíz con todas las secciones listadas
en la sección 7.3 de la spec: configuración general (`[*]`), reglas
específicas para archivos MSBuild, JSON/YAML/XML, `*.cs` y `*.axaml`.

Las reglas clave de C# a incluir son: file-scoped namespaces,
directivas `using` fuera del namespace,
`dotnet_style_namespace_match_folder`, naming (PascalCase público,
camelCase parámetros/locales, `_camelCase` privados, prefijo `I` en
interfaces), llaves K&R (apertura en la misma línea).

Esta es la tarea más larga de las de configuración. Reservar tiempo
para validar que las reglas no produzcan warnings espurios sobre
archivos generados por la plantilla de Avalonia (en cuyo caso se
usan directivas `#pragma` o exclusiones por archivo, **nunca**
relajar la regla global).

**Dependencias**
- T-001.

**Archivos / proyectos afectados**
- `.editorconfig` (nuevo).

**Criterios de finalización**
- El archivo existe y es parseable.
- `dotnet format --verify-no-changes` no propone cambios sobre el
  propio `.editorconfig` ni sobre los archivos que se crearán en
  tareas siguientes (verificable al final de T-014).

---

## T-006 — Crear `stylecop.json` y `README.md` mínimo

**Objetivo**
Dejar listos los archivos finales de la raíz: configuración de
StyleCop y un README que sirva de entrada al repositorio.

**Descripción**
- Crear `stylecop.json` en la raíz con la configuración por defecto
  (objeto `{"settings": {}}`). Las reglas que se deshabiliten se
  documentarán en el propio archivo con un comentario que explique
  el porqué.
- Crear `README.md` mínimo con: nombre del proyecto, comando para
  compilar (`dotnet build`), comando para ejecutar
  (`dotnet run --project src/ControlDeFinanzasIA.UI`), y una
  referencia a `docs/vision.md` para entender el producto.

**Dependencias**
- T-001.

**Archivos / proyectos afectados**
- `stylecop.json` (nuevo).
- `README.md` (nuevo).

**Criterios de finalización**
- Ambos archivos existen.
- `stylecop.json` es JSON válido.
- `README.md` se renderiza correctamente (verificable abriendo el
  archivo en un visor Markdown).

---

## T-007 — Crear proyecto `Domain` y añadirlo a la solución

**Objetivo**
Tener el proyecto `ControlDeFinanzasIA.Domain` creado, vacío y
añadido a la solución.

**Descripción**
Desde la raíz, ejecutar `dotnet new classlib -n
ControlDeFinanzasIA.Domain -o src/ControlDeFinanzasIA.Domain -f
net8.0` para crear el proyecto. Eliminar el archivo `Class1.cs`
generado por la plantilla. Añadir el proyecto a la solución con
`dotnet sln add src/ControlDeFinanzasIA.Domain/ControlDeFinanzasIA.Domain.csproj`.

**No se añaden project references ni paquetes NuGet** en esta tarea
(es una tarea explícita de la spec: `Domain` no puede tener
ninguno).

**Dependencias**
- T-002 (las propiedades comunes de `Directory.Build.props`
  aplican al proyecto recién creado).
- T-003 (necesario para que cualquier `<PackageReference>` futuro
  funcione vía CPM; en esta tarea no se añade ninguno, pero el
  archivo ya está disponible).

**Archivos / proyectos afectados**
- `src/ControlDeFinanzasIA.Domain/` (carpeta nueva).
- `src/ControlDeFinanzasIA.Domain/ControlDeFinanzasIA.Domain.csproj`
  (nuevo).
- `ControlDeFinanzasIA.sln` (modificado: nuevo proyecto listado).

**Criterios de finalización**
- `dotnet sln list` muestra el proyecto `Domain`.
- `dotnet build` desde la raíz pasa con código `0` y sin warnings.
- El proyecto `Domain` **no** tiene `ProjectReference` ni
  `PackageReference` (verificable con `dotnet list reference` y
  `dotnet list package`).
- El archivo `Class1.cs` no existe en `src/ControlDeFinanzasIA.Domain/`.

---

## T-008 — Crear proyecto `Application` con referencia a `Domain`

**Objetivo**
Tener el proyecto `ControlDeFinanzasIA.Application` creado, con
referencia a `Domain` y el paquete `FluentValidation` disponible.

**Descripción**
Ejecutar `dotnet new classlib -n ControlDeFinanzasIA.Application -o
src/ControlDeFinanzasIA.Application -f net8.0`. Eliminar `Class1.cs`.
Añadir el proyecto a la solución. Añadir `<ProjectReference
Include="..\ControlDeFinanzasIA.Domain\ControlDeFinanzasIA.Domain.csproj" />`
al `.csproj`. Añadir `<PackageReference Include="FluentValidation" />`
(sin `Version`, tomado de `Directory.Packages.props`).

**Dependencias**
- T-007 (existe `Domain`).

**Archivos / proyectos afectados**
- `src/ControlDeFinanzasIA.Application/` (carpeta nueva).
- `src/ControlDeFinanzasIA.Application/ControlDeFinanzasIA.Application.csproj`
  (nuevo).
- `ControlDeFinanzasIA.sln` (modificado).

**Criterios de finalización**
- `dotnet sln list` muestra el proyecto `Application`.
- `dotnet build` desde la raíz pasa sin warnings.
- `dotnet list src/ControlDeFinanzasIA.Application reference` muestra
  solo `Domain`.
- `dotnet list src/ControlDeFinanzasIA.Application package` muestra
  `FluentValidation` con la versión esperada.
- `Class1.cs` no existe.

---

## T-009 — Crear proyecto `Infrastructure` con referencia a `Application`

**Objetivo**
Tener el proyecto `ControlDeFinanzasIA.Infrastructure` creado, con
referencia a `Application` (que arrastra `Domain`).

**Descripción**
Ejecutar `dotnet new classlib -n ControlDeFinanzasIA.Infrastructure
-o src/ControlDeFinanzasIA.Infrastructure -f net8.0`. Eliminar
`Class1.cs`. Añadir a la solución. Añadir `<ProjectReference
Include="..\ControlDeFinanzasIA.Application\ControlDeFinanzasIA.Application.csproj" />`.

**No se añaden paquetes NuGet en esta tarea.** EF Core y SQLite se
añaden en la feature F-002, que es la primera que los usa. Esta
disciplina evita instalar paquetes que no se usan.

**Dependencias**
- T-008 (existe `Application`).

**Archivos / proyectos afectados**
- `src/ControlDeFinanzasIA.Infrastructure/` (carpeta nueva).
- `src/ControlDeFinanzasIA.Infrastructure/ControlDeFinanzasIA.Infrastructure.csproj`
  (nuevo).
- `ControlDeFinanzasIA.sln` (modificado).

**Criterios de finalización**
- `dotnet sln list` muestra el proyecto `Infrastructure`.
- `dotnet build` desde la raíz pasa sin warnings.
- `dotnet list src/ControlDeFinanzasIA.Infrastructure reference`
  muestra `Application` (y, transitivamente, `Domain`).
- `dotnet list src/ControlDeFinanzasIA.Infrastructure package` no
  lista paquetes (puede listar los implícitos de .NET 8 si los
  hubiera, pero no debe haber NuGets añadidos manualmente).
- `Class1.cs` no existe.

---

## T-010 — Crear proyecto `UI` con plantilla Avalonia y referencias

**Objetivo**
Tener la app Avalonia arrancando con una `MainWindow` vacía, con
referencias a `Application` y `Domain`.

**Descripción**
Ejecutar la plantilla de Avalonia para crear el proyecto:
`dotnet new install Avalonia.Templates` (si no está ya instalada)
seguido de `dotnet new avalonia.app -n ControlDeFinanzasIA.UI -o
src/ControlDeFinanzasIA.UI`. Esto crea `Program.cs`, `App.axaml`,
`App.axaml.cs` y el `.csproj` ya configurado con los paquetes de
Avalonia.

Revisar el `.csproj` generado y:
- Confirmar que `<UseAvaloniaApp>true</UseAvaloniaApp>` está.
- Confirmar que `OutputType=Exe` está.
- Añadir los paquetes `Microsoft.Extensions.Hosting`,
  `Microsoft.Extensions.DependencyInjection`,
  `Microsoft.Extensions.Logging` y
  `Microsoft.Extensions.Logging.Console` (vía CPM, sin `Version`).
- Añadir `<ProjectReference>` a `Application` y `Domain`.
- Quitar cualquier `PackageReference` con `Version` hardcodeado
  (todo debe pasar por CPM).

Personalizar `App.axaml.cs` para que en
`OnFrameworkInitializationCompleted` se cree una `Window` con título
"ControlDeFinanzasIA" y se asigne a
`ApplicationLifetime.MainWindow`. **No** crear `MainWindow.axaml`;
la ventana es íntegramente en código en esta feature.

**Dependencias**
- T-008 (existe `Application`, que es la dependencia que
  `Infrastructure`-todavía-no-referenciada necesita para que la
  app compile; `UI` no referencia a `Infrastructure` aún).
- T-003 (los paquetes de Avalonia y Microsoft.Extensions están en
  CPM).

**Archivos / proyectos afectados**
- `src/ControlDeFinanzasIA.UI/` (carpeta nueva).
- `src/ControlDeFinanzasIA.UI/ControlDeFinanzasIA.UI.csproj`
  (nuevo o modificado por la plantilla).
- `src/ControlDeFinanzasIA.UI/Program.cs` (nuevo).
- `src/ControlDeFinanzasIA.UI/App.axaml` (nuevo).
- `src/ControlDeFinanzasIA.UI/App.axaml.cs` (nuevo o modificado).
- `ControlDeFinanzasIA.sln` (modificado).

**Criterios de finalización**
- `dotnet sln list` muestra el proyecto `UI`.
- `dotnet build` desde la raíz pasa sin warnings.
- `dotnet list src/ControlDeFinanzasIA.UI reference` muestra
  `Application` y `Domain`, **no** muestra `Infrastructure`.
- `dotnet list src/ControlDeFinanzasIA.UI package` lista los
  paquetes de Avalonia y Microsoft.Extensions, todos con la versión
  fijada en `Directory.Packages.props` (verificable con
  `dotnet list package --include-transitive`).
- `dotnet run --project src/ControlDeFinanzasIA.UI` arranca una
  ventana con título "ControlDeFinanzasIA" y la app se cierra sin
  excepciones al cerrarla.

---

## T-010a — Añadir verificación de estilo al pipeline de CI

**Objetivo**
Cerrar el hueco detectado en la re-revisión de T-010: las
violaciones de `.editorconfig` no se detectan en `dotnet build` (el
build no enforce reglas `IDE*` aunque `EnforceCodeStyleInBuild=true`
esté activo), pero **sí** se detectan con
`dotnet format style --verify-no-changes`. Sin CI, el estilo se
degrada silenciosamente. Esta tarea añade ese check al pipeline.

**Descripción**
Crear un workflow de CI mínimo (GitHub Actions) en
`.github/workflows/build.yml` que ejecute, en orden, sobre la raíz
del repo:

1. `dotnet restore`.
2. `dotnet build -c Release --no-restore` (build "duro" para
   detectar regresiones del compilador y de los analizadores del
   compilador).
3. `dotnet format style --verify-no-changes` (check de estilo
   contra `.editorconfig`).
4. `dotnet test -c Release --no-build` cuando existan tests (a
   partir de T-013). En F-001 todavía no hay tests reales, pero
   el step se puede añadir ya y simplemente no ejecuta nada.

**Notas sobre elección de CI:**
- Se usa GitHub Actions como default porque es lo más común en
  proyectos .NET OSS. Si el repo vive en otro proveedor (Azure
  DevOps, GitLab, etc.), el contenido del workflow es
  trasladable 1:1 — los `run: dotnet ...` son los mismos.
- En F-001 el CI corre solo en `ubuntu-latest`. La validación
  cross-OS (windows-latest, macos-latest) se difiere a una fase
  posterior, cuando haya que validar cosas específicas de
  plataforma (instaladores, dependencias nativas).

**Dependencias**
- T-010 (necesita que la app compile end-to-end para que el
  pipeline tenga sentido).

**Archivos / proyectos afectados**
- `.github/workflows/build.yml` (nuevo).

**Criterios de finalización**
- El archivo `.github/workflows/build.yml` existe y es YAML
  válido (parseable).
- El workflow referencia explícitamente los 4 comandos
  listados arriba.
- `dotnet format style --verify-no-changes` se ejecuta localmente
  y pasa (exit 0), confirmando que el check tendría éxito en CI
  sobre el estado actual del repo.
- `dotnet build` desde la raíz sigue pasando sin warnings (el
  nuevo archivo no afecta al build local).

---

## T-011 — Crear proyecto `Tests.Unit` con referencias a `Domain` y `Application`

**Objetivo**
Tener el proyecto de tests unitarios creado y compilando, listo
para que F-007 añada las reglas de NetArchTest.

**Descripción**
Ejecutar `dotnet new xunit -n ControlDeFinanzasIA.Tests.Unit -o
tests/ControlDeFinanzasIA.Tests.Unit -f net8.0`. Eliminar el
`UnitTest1.cs` que genera la plantilla. Añadir el proyecto a la
solución. Añadir `<ProjectReference>` a `Domain` y `Application`.
Añadir los paquetes `xunit`, `xunit.runner.visualstudio`,
`Microsoft.NET.Test.Sdk` y `NetArchTest.Rules` (todos vía CPM, sin
`Version`).

Asegurarse de que el `.csproj` tiene `<IsTestProject>true</IsTestProject>`.

**Dependencias**
- T-008 (existen `Domain` y `Application`).

**Archivos / proyectos afectados**
- `tests/ControlDeFinanzasIA.Tests.Unit/` (carpeta nueva).
- `tests/ControlDeFinanzasIA.Tests.Unit/ControlDeFinanzasIA.Tests.Unit.csproj`
  (nuevo).
- `ControlDeFinanzasIA.sln` (modificado).

**Criterios de finalización**
- `dotnet sln list` muestra el proyecto `Tests.Unit`.
- `dotnet build` desde la raíz pasa sin warnings.
- `dotnet list tests/ControlDeFinanzasIA.Tests.Unit reference`
  muestra `Domain` y `Application`.
- `dotnet test` desde la raíz corre (puede no encontrar tests, pero
  no debe fallar con error de compilación o de proyecto).
- `UnitTest1.cs` no existe.

---

## T-012 — Crear proyecto `Tests.Integration` con referencia a `Infrastructure`

**Objetivo**
Tener el proyecto de tests de integración creado, referenciando
`Infrastructure` (lo que arrastra a `Application` y `Domain`).

**Descripción**
Ejecutar `dotnet new xunit -n ControlDeFinanzasIA.Tests.Integration
-o tests/ControlDeFinanzasIA.Tests.Integration -f net8.0`. Eliminar
el `UnitTest1.cs`. Añadir a la solución. Añadir
`<ProjectReference>` a `Infrastructure` (vía CPM para los paquetes
xUnit, igual que en T-011).

**No se añade `Microsoft.EntityFrameworkCore.Sqlite` en esta
tarea.** EF Core se añade en F-002 cuando los repositorios se
creen. La spec lo indica explícitamente.

Asegurarse de que el `.csproj` tiene
`<IsTestProject>true</IsTestProject>`.

**Dependencias**
- T-009 (existe `Infrastructure`).

**Archivos / proyectos afectados**
- `tests/ControlDeFinanzasIA.Tests.Integration/` (carpeta nueva).
- `tests/ControlDeFinanzasIA.Tests.Integration/ControlDeFinanzasIA.Tests.Integration.csproj`
  (nuevo).
- `ControlDeFinanzasIA.sln` (modificado).

**Criterios de finalización**
- `dotnet sln list` muestra el proyecto `Tests.Integration`.
- `dotnet build` desde la raíz pasa sin warnings.
- `dotnet list tests/ControlDeFinanzasIA.Tests.Integration reference`
  muestra `Infrastructure` (y, transitivamente, `Application` y
  `Domain`).
- `dotnet test` desde la raíz corre el runner sobre este proyecto
  aunque no haya tests reales todavía.
- `UnitTest1.cs` no existe.

---

## T-013 — Añadir tests placeholder `Skip`-eados

**Objetivo**
Que `dotnet test` ejecute al menos un test (aunque esté skipped) en
cada proyecto de tests, para validar que el pipeline de testing
funciona end-to-end desde el día uno.

**Descripción**
Crear un único archivo de tests en cada proyecto de tests con un
único método marcado con el atributo `Skip` de xUnit y un mensaje
claro que indique que es un placeholder de F-001 y que se eliminará
en F-002.

Ejemplo de patrón a seguir (no se escribe el código, solo se
describe):

- Clase pública `F001PlaceholderTests` en el namespace raíz del
  proyecto de tests correspondiente.
- Un único método `[Fact(Skip = "F-001 placeholder — se elimina en
  F-002")]` llamado, por ejemplo, `Placeholder_ShouldBeSkipped`.
- Sin lógica dentro: el cuerpo es trivial o vacío.

**Dependencias**
- T-011, T-012 (ambos proyectos de tests deben existir).

**Archivos / proyectos afectados**
- `tests/ControlDeFinanzasIA.Tests.Unit/F001PlaceholderTests.cs`
  (nuevo).
- `tests/ControlDeFinanzasIA.Tests.Integration/F001PlaceholderTests.cs`
  (nuevo).

**Criterios de finalización**
- `dotnet test` desde la raíz corre ambos proyectos de tests.
- En la salida aparece, por cada proyecto, un test con estado
  `Skipped` y el mensaje del `Skip`.
- `dotnet test` retorna código de salida `0` (los skipped no
  cuentan como fallo).
- `dotnet build` desde la raíz sigue pasando sin warnings.

---

## T-014 — Verificación final de compilación, tests y arranque

**Objetivo**
Ejecutar la lista de comprobaciones de la sección 10 de la spec
para confirmar que F-001 cumple todos los criterios de aceptación
antes de marcarla como completada.

**Descripción**
Esta tarea no introduce archivos ni cambios. Consiste en
ejecutar, en orden, los comandos listados abajo y volcar la salida
a la PR para revisión.

Pasos:

1. `dotnet build -c Debug` — debe pasar sin warnings.
2. `dotnet build -c Release` — debe pasar sin warnings.
3. `dotnet test` — debe ejecutar ambos proyectos de tests; los
   tests placeholder deben aparecer como `Skipped`.
4. `dotnet sln list` — debe listar los 6 proyectos en el orden
   del layout.
5. `dotnet list src/ControlDeFinanzasIA.Domain reference` — vacío.
6. `dotnet list src/ControlDeFinanzasIA.Application reference` —
   solo `Domain`.
7. `dotnet list src/ControlDeFinanzasIA.Infrastructure reference` —
   solo `Application`.
8. `dotnet list src/ControlDeFinanzasIA.UI reference` — `Application`
   y `Domain`, **no** `Infrastructure`.
9. `dotnet list src/ControlDeFinanzasIA.UI package` — paquetes
   Avalonia + Microsoft.Extensions, todos con la versión de CPM.
10. `dotnet format --verify-no-changes` — sin cambios propuestos.
11. `dotnet run --project src/ControlDeFinanzasIA.UI` — la app
    arranca y muestra una ventana con título
    "ControlDeFinanzasIA". Comprobar al menos en el SO del
    implementador; documentar en la PR la verificación en los
    otros dos SO.
12. Verificación de `.gitignore`: `git status --ignored` no muestra
    `bin/`, `obj/`, `*.db`, `app-*.log`.

**Dependencias**
- Todas las tareas anteriores.

**Archivos / proyectos afectados**
- Ninguno nuevo. Esta tarea es solo de verificación.

**Criterios de finalización**
- Todos los pasos anteriores ejecutados y resultados consistentes
  con lo esperado.
- Captura de la ventana arrancando (o descripción detallada en la
  PR si el entorno es headless).
- Sección 10 de la spec (criterios de aceptación) revisada punto
  por punto en la descripción de la PR, con cada check marcado.
- F-001 marcada como completada en
  `spec/features/phase-01-features.md` (cambiar el estado de
  "Pendiente" a "Completada") y esta spec movida o renombrada a
  `F-001.done.md` siguiendo la convención del proyecto.

---

## Orden recomendado y trabajo en paralelo

**Orden secuencial estricto:** T-001 → T-002 → T-003 → T-004 → T-005
→ T-006 → T-007 → T-008 → (T-009 y T-011 en paralelo posible) →
T-010 → **T-010a** → T-012 → T-013 → T-014.

T-009 y T-011 se pueden hacer en paralelo porque no comparten
archivos: T-009 trabaja en `src/` e `Infrastructure`; T-011 trabaja
en `tests/` y `Tests.Unit`. Requieren que T-008 esté terminada.

**No se recomienda paralelizar** T-002 con T-003/T-004/T-005/T-006
porque todos escriben en la raíz y un editor que no refresca
puede perder cambios.

**T-010 (UI / Avalonia) es la más delicada.** Si la plantilla de
Avalonia genera una estructura distinta a la esperada, ajustar la
tarea antes de seguir. No continuar con T-013 si T-010 no compila
y arranca.

---

## Riesgos de la fase de tareas

- **`dotnet new` con plantillas desactualizadas.** Si la plantilla
  de Avalonia cambia entre versiones, los archivos generados pueden
  no coincidir con la descripción de T-010. Mitigación: fijar
  versión exacta de la plantilla al instalarla.
- **Conflictos en `.editorconfig` con plantillas.** Algunas
  plantillas generan archivos con estilo distinto al esperado;
  aplicar `dotnet format` tras crearlos y revisar las diferencias
  antes de commit.
- **`TreatWarningsAsErrors=true` rompe con warnings de paquetes
  externos.** Si pasa, ajustar localmente en el `.csproj` con
  `NoWarn` y comentario. No relajar la regla global.
- **Avalonia requiere dependencias nativas en Linux** (X11/Wayland,
  libgtk-3, etc.). Si la verificación en Linux falla por esto,
  documentar en la PR y abrir issue de seguimiento, pero no
  bloquear el merge de F-001.

---

## Referencias

- `spec/features/phase-01/F-001-skeleton-solution.md` — spec
  completa de F-001.
- `docs/architecture.md` §3 (solución y proyectos), §4 (grafo de
  dependencias).
- `docs/vision.md` — para el nombre y propósito del proyecto.
- `spec/constitution/tech-stack.md` — reglas de paquetes y
  convenciones.
- `spec/features/phase-01-features.md` — F-001 dentro del
  desglose de la fase.
