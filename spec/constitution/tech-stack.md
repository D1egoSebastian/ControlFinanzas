# Tech Stack & Conventions

> Decisiones técnicas que **no se cambian a la ligera**. Cualquier desviación
> debe documentarse y justificarse en `docs/architecture.md`.

## Core stack

- **Lenguaje:** C# sobre **.NET 8** (LTS).
- **UI:** **Avalonia** (cross-platform: Windows, macOS, Linux), patrón **MVVM**.
  - No usar WPF — la app debe correr fuera de Windows.
  - Estilos: Avalonia UI con un sistema de design tokens propio (colores,
    tipografía, espaciado) centralizado, no estilos inline.
- **Datos locales:** **SQLite** + **Entity Framework Core** (Code First,
  migraciones versionadas).
- **Inyección de dependencias:** `Microsoft.Extensions.DependencyInjection`.
- **Logging:** `Microsoft.Extensions.Logging` con sink configurable (consola
  en dev, archivo rotativo en producción).
- **Testing:** xUnit + FluentAssertions. Tests de EF Core contra SQLite
  en memoria. UI: Avalonia.Headless.

## IA — Bring Your Own (BYO)

- **No se embebe un modelo.** La app llama por HTTP a una API configurable.
- **Compatibilidad:** contratos estilo **OpenAI** y **Anthropic Messages**.
  La capa de cliente debe ser abstraída detrás de una interfaz
  `ILLMClient` con adapters por proveedor.
- **Proveedores esperados (v1):** Anthropic, OpenAI, OpenRouter (este último
  da acceso a modelos **gratuitos** como "Big Pickle"). La lista es
  extensible por configuración, no por código.
- **Configuración por el usuario:** API key, proveedor, modelo, parámetros
  opcionales (temperature, max tokens). Almacenada en el archivo de datos
  local, **nunca** hardcodeada.
- **Salida estructurada:** cuando aplique, la IA debe responder en JSON con
  un schema conocido (categorización, plan de ahorro, etc.).
- **Fallo silencioso:** si no hay key, no hay red o la API falla, la app
  sigue funcionando. La UI lo indica claramente.

## Almacenamiento y rutas

- **Archivo de datos:** SQLite, ruta resuelta por SO siguiendo convenciones
  XDG / plataforma:
  - Windows: `%APPDATA%\ControlDeFinanzasIA\`
  - macOS: `~/Library/Application Support/ControlDeFinanzasIA/`
  - Linux: `$XDG_CONFIG_HOME/ControlDeFinanzasIA/` o
    `~/.config/ControlDeFinanzasIA/`
- **Backups manuales** vía export/import. **No** hay backup automático en v1.
- **Configuración de IA** vive en el mismo archivo de datos (cifrado en
  reposo si es viable, ver `docs/architecture.md`).

## Internacionalización

- **Idiomas:** español (es-DO por defecto) e inglés (en).
- **Framework:** usar el sistema de i18n de .NET (`IStringLocalizer`) o
  similar — **no** hardcodear strings de UI en código.
- **Formatos locales:** fechas `dd/MM/yyyy`, separador de miles `.` y
  decimal `,` para es-DO. La app debe respetar el locale activo del SO.

## Convenciones de código

- **MVVM estricto en Avalonia:** sin code-behind con lógica. Views con
  bindings a ViewModels testeables.
- **Async/await de extremo a extremo** para todo I/O (DB, red).
- **Errores como valores** o excepciones tipadas, **nunca** silenciosos.
  La UI muestra mensajes claros.
- **Cero telemetría remota.**
- **Sin secrets en el repo.** API keys del usuario nunca se commitean.
- **Migraciones de EF Core** versionadas y commiteadas. Cambios destructivos
  de schema requieren migración explícita y, si aplica, backup automático
  previo.

## Documentación y ayuda

- **Context7 MCP** configurado en `opencode.json` para resolver dudas de
  frameworks, APIs y patrones actualizados. Usar cuando se introduce una
  librería o patrón nuevo.
- **Skill de diseño:** `.claude/skills/frontend-design/SKILL.md` aplica
  para cualquier cambio visual / UI de la app.

## Referencias

- Visión del producto: `docs/vision.md`.
- Arquitectura detallada: `docs/architecture.md`.
- Roadmap: `docs/roadmap.md`.
