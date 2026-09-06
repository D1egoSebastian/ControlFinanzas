# ControlDeFinanzasIA

App de escritorio cross-platform (Windows, macOS, Linux) para el control
personal de finanzas, potenciada por IA. Datos 100% locales.

> **Documentación principal:** [docs/vision.md](docs/vision.md) ·
> [docs/architecture.md](docs/architecture.md) ·
> [docs/roadmap.md](docs/roadmap.md)

## Requisitos

- .NET 8 SDK. Si no está instalado:
  ```bash
  winget install Microsoft.DotNet.SDK.8
  ```

## Compilar

```bash
dotnet build
```

## Ejecutar la app

```bash
dotnet run --project src/ControlDeFinanzasIA.UI
```

## Ejecutar los tests

```bash
dotnet test
```

## Estado

El proyecto está activo y en desarrollo. Las **fases 1 y 2** están
completas y archivadas. La **fase 3** (slice vertical de registro y
listado de gastos) está en curso, con **2/7 features** completadas
(`F-001` entidad `Expense`, `F-002` configuración EF Core + migración
`AddExpenseTable`). Ver [docs/roadmap.md](docs/roadmap.md) para el
plan completo y [spec/phases/phase-03/features.md](spec/phases/phase-03/features.md)
para el detalle de la fase actual.

### Stack

- C# sobre .NET 8 (LTS) · Avalonia UI (MVVM) · SQLite + EF Core
- xUnit + FluentAssertions · NetArchTest para validar la arquitectura
- Convenciones y reglas: ver [AGENTS.md](AGENTS.md)
