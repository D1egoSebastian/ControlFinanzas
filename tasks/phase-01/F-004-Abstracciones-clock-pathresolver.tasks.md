# Tareas — F-004: Abstracciones IClock e IPathResolver

> **Spec:** `spec/features/phase-01/F-004-Abstracciones-clock-pathresolver.md`
> **Fuentes de verdad:** la spec, `docs/architecture.md`,
> `docs/vision.md`, `docs/roadmap.md`, `spec/constitution/*`,
> `spec/features/phase-01/F-001-skeleton-solution.done.md` (F-001
> completada, provee la base: proyecto `Application` existe),
> `spec/features/phase-01/F-002-money-value-object.done.md` y
> `F-003-datarange-value-object.done.md` (features de value object
> que siguen el mismo patrón de F-004).
>
> Cada tarea es implementable en menos de 2 horas. El proyecto
> debe seguir compilando al finalizar cada una. La numeración
> (T-001...) es local a esta feature.

---

## Resumen de tareas

| ID | Tarea | Estimación | Depende de |
| --- | --- | --- | --- |
| T-001 | Crear la interface `IClock` | XS | — |
| T-002 | Crear la interface `IPathResolver` | XS | — |
| T-003 | Crear `FakeClock` (implementación de prueba) + tests | S | T-001 |
| T-004 | Crear `FakePathResolver` (implementación de prueba) + tests | S | T-002 |
| T-005 | Verificación final: tests, format check, build, cobertura, cierre | S | todas |

**Total estimado:** 1 jornada o menos. T-001 y T-002 son triviales
(2 interfaces pequeñas). T-003 y T-004 son las substantive (fakes
+ tests). T-005 es solo verificación.

---

## T-001 — Crear la interface `IClock`

**Objetivo**
Establecer el contrato `IClock` como la única forma en que
`Application` (y todo el código que la use) accede a la hora
actual.

**Descripción**
Crear el archivo
`src/ControlDeFinanzasIA.Application/Abstractions/IClock.cs` con:

- `public interface IClock` con dos miembros:
  - `DateTime UtcNow { get; }` — retorna la hora actual en UTC.
  - `DateOnly Today { get; }` — retorna la fecha actual.
- File-scoped namespace: `namespace
  ControlDeFinanzasIA.Application.Abstractions;`
- XML doc en la interface y en cada miembro, explicando:
  - Que `UtcNow` debe estar en UTC (`Kind == DateTimeKind.Utc`).
  - Que `Today` es "fecha actual" sin especificar zona (decisión
    de la implementación).

> **No es necesario añadir tests** a `IClock` directamente: las
> interfaces son contratos sin lógica. Los tests se centran en las
> implementaciones (`FakeClock` en T-003 y la implementación real
> `SystemClock` en F-008).

**Dependencias**
- F-001 (proyecto `Application` existe).

**Archivos / proyectos afectados**
- `src/ControlDeFinanzasIA.Application/Abstractions/IClock.cs`
  (nuevo).

**Criterios de finalización**
- El archivo existe en `src/ControlDeFinanzasIA.Application/Abstractions/`.
- `IClock` es `public interface`.
- Tiene exactamente los miembros `UtcNow` y `Today` con los
  tipos correctos (`DateTime` y `DateOnly` respectivamente).
- File-scoped namespace.
- XML doc en la interface y los miembros.
- `dotnet build`: 0 warnings, 0 errors.
- `Application` sigue sin `PackageReference`s.

---

## T-002 — Crear la interface `IPathResolver`

**Objetivo**
Establecer el contrato `IPathResolver` como la única forma en que
`Application` accede a las rutas de archivos de datos.

**Descripción**
Crear el archivo
`src/ControlDeFinanzasIA.Application/Abstractions/IPathResolver.cs`
con:

- `public interface IPathResolver` con dos miembros:
  - `string DataDirectory { get; }` — ruta del directorio de datos
    (no incluye el nombre del archivo).
  - `string DatabaseFilePath { get; }` — ruta completa al archivo
    de base de datos (incluye el nombre del archivo).
- File-scoped namespace: `namespace
  ControlDeFinanzasIA.Application.Abstractions;`
- XML doc en la interface y los miembros, explicando:
  - `DataDirectory` es la ruta al directorio (no al archivo).
  - `DatabaseFilePath` es derivable de `DataDirectory` + nombre
    del archivo, pero se expone para que los callers no tengan
    que conocer la convención.

> **No es necesario añadir tests** a `IPathResolver` directamente:
> las interfaces son contratos sin lógica. Los tests se centran en
> las implementaciones (`FakePathResolver` en T-004 y
> `SystemPathResolver` en F-008).

**Dependencias**
- F-001.

**Archivos / proyectos afectados**
- `src/ControlDeFinanzasIA.Application/Abstractions/IPathResolver.cs`
  (nuevo).

**Criterios de finalización**
- El archivo existe en `src/ControlDeFinanzasIA.Application/Abstractions/`.
- `IPathResolver` es `public interface`.
- Tiene exactamente los miembros `DataDirectory` y
  `DatabaseFilePath` con tipo `string`.
- File-scoped namespace.
- XML doc en la interface y los miembros.
- `dotnet build`: 0 warnings, 0 errors.
- `Application` sigue sin `PackageReference`s.

---

## T-003 — Crear `FakeClock` (implementación de prueba) + tests

**Objetivo**
Implementar `FakeClock` que satisfaga `IClock` y permita a los
tests controlar `UtcNow` y `Today` explícitamente. Validar con
tests que el fake funciona correctamente.

**Descripción**
Crear dos archivos:

### Archivo 1: `tests/ControlDeFinanzasIA.Tests.Unit/TestDoubles/FakeClock.cs`

- `public class FakeClock : IClock` con dos propiedades
  auto-implementadas con `get; set;`:
  - `public DateTime UtcNow { get; set; }`
  - `public DateOnly Today { get; set; }`
- File-scoped namespace: `namespace
  ControlDeFinanzasIA.Tests.Unit.TestDoubles;`
- `using` para `IClock` desde
  `ControlDeFinanzasIA.Application.Abstractions`.
- Sin lógica adicional: el fake almacena y retorna lo que se le
  setea.

### Archivo 2: `tests/ControlDeFinanzasIA.Tests.Unit/TestDoubles/FakeClockTests.cs`

- `public class FakeClockTests` con los siguientes tests (basados
  en la sección AC-3 de la spec):
  - `FakeClock_DefaultConstruction_ReturnsDefaultValues` — al
    construir sin parámetros, `UtcNow` es `default(DateTime)` y
    `Today` es `default(DateOnly)`.
  - `FakeClock_SetUtcNow_ReturnsSetValue` — setear un valor
    arbitrario, verificar que `UtcNow` lo retorna.
  - `FakeClock_SetToday_ReturnsSetValue` — setear un valor,
    verificar.
  - `FakeClock_SetDateTimeMinValue_ReturnsMinValue` — boundary
    con `DateTime.MinValue`.
  - `FakeClock_SetDateTimeMaxValue_ReturnsMaxValue` — boundary
    con `DateTime.MaxValue`.
  - `FakeClock_SetDateOnlyMinAndMax_ReturnsCorrectValues` —
    boundary con `DateOnly.MinValue` y `DateOnly.MaxValue`.
  - `FakeClock_SetKindToUtc_PreservesUtcKind` — al setear un
    `DateTime` con `Kind = Utc`, `UtcNow` retorna ese `Kind`.

**Dependencias**
- T-001 (`IClock` debe existir).

**Archivos / proyectos afectados**
- `tests/ControlDeFinanzasIA.Tests.Unit/TestDoubles/FakeClock.cs`
  (nuevo).
- `tests/ControlDeFinanzasIA.Tests.Unit/TestDoubles/FakeClockTests.cs`
  (nuevo).

**Criterios de finalización**
- `FakeClock.cs` existe en `TestDoubles/`.
- `FakeClock` es `public class`, implementa `IClock`, tiene
  `UtcNow` y `Today` con `get; set;`.
- `FakeClockTests.cs` existe con los 7 tests listados.
- Todos los 7 tests pasan.
- `dotnet build`: 0 warnings, 0 errors.
- `dotnet test`: los nuevos tests pasan + los anteriores.
- `dotnet format style --verify-no-changes`: pasa.

---

## T-004 — Crear `FakePathResolver` (implementación de prueba) + tests

**Objetivo**
Implementar `FakePathResolver` que satisfaga `IPathResolver` y
permita a los tests controlar las rutas explícitamente. Validar
con tests.

**Descripción**
Crear dos archivos:

### Archivo 1: `tests/ControlDeFinanzasIA.Tests.Unit/TestDoubles/FakePathResolver.cs`

- `public class FakePathResolver : IPathResolver` con dos
  propiedades auto-implementadas con `get; set;`:
  - `public string DataDirectory { get; set; } = string.Empty;`
  - `public string DatabaseFilePath { get; set; } = string.Empty;`
- File-scoped namespace: `namespace
  ControlDeFinanzasIA.Tests.Unit.TestDoubles;`
- `using` para `IPathResolver` desde
  `ControlDeFinanzasIA.Application.Abstractions`.
- Sin lógica adicional: el fake almacena y retorna lo que se le
  setea.

### Archivo 2: `tests/ControlDeFinanzasIA.Tests.Unit/TestDoubles/FakePathResolverTests.cs`

- `public class FakePathResolverTests` con los siguientes tests
  (basados en la sección AC-3 de la spec):
  - `FakePathResolver_DefaultConstruction_ReturnsEmptyStrings` —
    al construir sin parámetros, ambas propiedades son
    `string.Empty`.
  - `FakePathResolver_SetDataDirectory_ReturnsSetValue` — setear
    una ruta, verificar.
  - `FakePathResolver_SetDatabaseFilePath_ReturnsSetValue` — setear
    una ruta completa, verificar.
  - `FakePathResolver_SetEmptyStrings_AcceptsEmptyValues` — el
    fake acepta strings vacíos (no valida).
  - `FakePathResolver_SetPathWithSpaces_PreservesValue` — rutas con
    espacios.
  - `FakePathResolver_SetPathWithSpecialCharacters_PreservesValue`
    — rutas con caracteres especiales (`/`, `\`, `..`).

**Dependencias**
- T-002 (`IPathResolver` debe existir).

**Archivos / proyectos afectados**
- `tests/ControlDeFinanzasIA.Tests.Unit/TestDoubles/FakePathResolver.cs`
  (nuevo).
- `tests/ControlDeFinanzasIA.Tests.Unit/TestDoubles/FakePathResolverTests.cs`
  (nuevo).

**Criterios de finalización**
- `FakePathResolver.cs` existe en `TestDoubles/`.
- `FakePathResolver` es `public class`, implementa `IPathResolver`,
  tiene `DataDirectory` y `DatabaseFilePath` con `get; set;` y
  valores por defecto `string.Empty`.
- `FakePathResolverTests.cs` existe con los 6 tests listados.
- Todos los 6 tests pasan.
- `dotnet build`: 0 warnings, 0 errors.
- `dotnet test`: los nuevos tests pasan + los anteriores.
- `dotnet format style --verify-no-changes`: pasa.

---

## T-005 — Verificación final, cobertura y cierre

**Objetivo**
Validar que F-004 cumple los criterios de aceptación de la spec
(sección 14), y dejar el repo en estado de "F-004 terminada".

**Descripción**
Esta tarea **no introduce código nuevo**. Consiste en ejecutar
los pasos de verificación y ejecutar las acciones de cierre.

### Pasos de verificación

1. **Compilación:** `dotnet build -c Debug` y `dotnet build -c
   Release` — ambos con 0 warnings, 0 errors.
2. **Tests:** `dotnet test` — todos los tests pasan (los nuevos
   de F-004 + los anteriores de F-002 y F-003).
3. **Format check:** `dotnet format style --verify-no-changes` —
   exit 0.
4. **Cobertura:** verificar que `FakeClock.cs` y
   `FakePathResolver.cs` tienen cobertura 100%. Sin
   `coverlet.collector` configurado, **verificación manual**:
   cada setter probado con al menos un test (ver tabla abajo).
5. **Aislamiento de Domain:** `dotnet list src/.../Domain package`
   debe seguir mostrando "No packages were found for this
   framework." (preparación para F-007).
6. **Layout:** `IClock.cs` y `IPathResolver.cs` en
   `src/ControlDeFinanzasIA.Application/Abstractions/`. `FakeClock*`
   y `FakePathResolver*` en
   `tests/.../TestDoubles/`.

### Pasos de limpieza

7. **Verificar que no hay placeholders que limpiar.** F-002 ya
   eliminó el `F001PlaceholderTests.cs` de `Tests.Unit` en su
   T-007. No hay nada que limpiar para F-004.
8. **Actualizar el desglose de fase:** en
   `spec/phases/phase-01/features.md`, cambiar el estado de F-004
   de "Pendiente" a "✅ COMPLETADA" (mismo formato que F-001,
   F-002, F-003).
9. **Renombrar la spec:** mover
   `spec/features/phase-01/F-004-Abstracciones-clock-pathresolver.md`
   a
   `spec/features/phase-01/F-004-Abstracciones-clock-pathresolver.done.md`
   (convención del proyecto, ver F-001, F-002, F-003).

### Resumen de cobertura manual (criterio AC-5)

Verificar que cada línea pública de los fakes está cubierta por
al menos un test:

| Miembro público | Test |
| --- | --- |
| `FakeClock()` constructor | `FakeClock_DefaultConstruction_ReturnsDefaultValues` |
| `FakeClock.UtcNow` getter y setter | T-003 (todos los tests usan el setter; el getter se verifica en cada uno) |
| `FakeClock.Today` getter y setter | T-003 (todos los tests usan el setter; el getter se verifica en cada uno) |
| `FakePathResolver()` constructor | `FakePathResolver_DefaultConstruction_ReturnsEmptyStrings` |
| `FakePathResolver.DataDirectory` getter y setter | T-004 (todos los tests usan el setter; el getter se verifica en cada uno) |
| `FakePathResolver.DatabaseFilePath` getter y setter | T-004 (todos los tests usan el setter; el getter se verifica en cada uno) |

**Cobertura esperada: 100% en `FakeClock` y `FakePathResolver`.**
Las interfaces (`IClock`, `IPathResolver`) no tienen lógica y se
verifican implícitamente al usarlas en los fakes y en futuras
features.

**Dependencias**
- T-001 a T-004.

**Archivos / proyectos afectados**
- `spec/phases/phase-01/features.md` (modificado: marcar F-004
  como completada).
- `spec/features/phase-01/F-004-Abstracciones-clock-pathresolver.md`
  (renombrado a `.done.md`).

**Criterios de finalización**
- `dotnet build` (Debug y Release): 0 warnings, 0 errors.
- `dotnet test`: todos los tests pasan.
- `dotnet format style --verify-no-changes`: exit 0.
- `IClock.cs` y `IPathResolver.cs` en
  `src/ControlDeFinanzasIA.Application/Abstractions/`.
- `FakeClock.cs`, `FakeClockTests.cs`, `FakePathResolver.cs`,
  `FakePathResolverTests.cs` en `TestDoubles/`.
- `Application` sigue sin `PackageReference`s.
- `Domain` sigue sin `PackageReference`s.
- `Domain` no referencia `Application/Abstractions/` (verificable
  por `grep` o por NetArchTest en F-007).
- `spec/phases/phase-01/features.md` actualizado con F-004
  marcada como completada.
- Spec renombrada a `.done.md`.
- **Confirmación punto por punto de los 6 grupos de criterios de
  aceptación de la spec (AC-1 a AC-6)**, presentada al cerrar
  la feature.

---

## Orden recomendado y trabajo en paralelo

**Orden secuencial estricto:** T-001 → T-002 → (T-003, T-004 en
cualquier orden) → T-005.

**T-001 y T-002 se pueden hacer en paralelo** (no comparten
archivos). Con 1 persona, en orden.

**T-003 y T-004 también se pueden hacer en paralelo** (archivos
distintos). Con 1 persona, en orden.

**No se recomienda paralelizar** T-001/T-002 entre sí si la
persona que las hace no tiene experiencia con las convenciones del
proyecto, porque las interfaces deben ser consistentes entre sí.

**T-001 y T-002 son las tareas más delicadas.** Definen los
contratos que toda la feature usa. Si se hacen con prisa (p. ej.
usando `DateTime` en vez de `DateTime` para `UtcNow` sin
documentar el `Kind`), hay que corregirlo antes de seguir.

---

## Riesgos de la fase de tareas

| Riesgo | Probabilidad | Impacto | Mitigación |
| --- | --- | --- | --- |
| `Domain` referencia `IClock` o `IPathResolver` accidentalmente | Baja | Alto | La ubicación en `Application/Abstractions/` (no en `Domain`) hace que un `using` accidental falle por compilación. NetArchTest en F-007 lo verifica automáticamente. |
| Los fakes divergen de las interfaces | Baja | Alto | Las interfaces son explícitas (no se generan). Si cambia la interfaz, el compilador obliga a actualizar el fake. |
| Confusión entre `DataDirectory` y `DatabaseFilePath` | Baja | Bajo | Nombres distintos, XML doc explícito, tests diferenciados. |
| Los fakes no son thread-safe y un test intenta usarlos en paralelo | Baja | Bajo | Los tests son single-thread por diseño. Si en el futuro se quiere paralelizar, los fakes se pueden sincronizar. |
| `Kind = Utc` no se verifica en el fake | Baja | Bajo | El fake verifica el contrato del fake (almacena y retorna), no la semántica de UTC (que es de la implementación real en F-008). Documentado en RN-1. |
| El `DateTime.UtcNow` de la implementación real no tiene `Kind = Utc` | Baja | Medio | El spec documenta que la implementación real debe garantizarlo. Test específico en F-008 cuando se implemente. |

---

## Referencias

- `spec/features/phase-01/F-004-Abstracciones-clock-pathresolver.md`
  — la spec completa de F-004. Este tasks file es su
  descomposición técnica.
- `spec/features/phase-01/F-001-skeleton-solution.done.md` —
  F-001 completada, provee la base: proyecto `Application`
  existe, configuración global aplicada.
- `spec/features/phase-01/F-002-money-value-object.done.md` —
  provee el patrón de value object con tests organizados por AC.
- `spec/features/phase-01/F-003-datarange-value-object.done.md` —
  sigue el mismo patrón; T-001..T-006 son un buen modelo para
  T-001..T-005 de F-004.
- `docs/architecture.md` §7 (Composition root) — define la
  convención de las interfaces de "seam" en
  `Application/Abstractions/`.
- `docs/roadmap.md` § Fase 1, F-004 dentro del desglose.
- `spec/constitution/tech-stack.md` — convenciones de código y
  reglas de paquetes.
- `spec/phases/phase-01/features.md` — F-004 dentro del
  desglose de features de la fase 1.
- `spec/README.md` — convención completa del directorio `spec/`.
- **Documentación de `DateTime.UtcNow`:**
  [learn.microsoft.com/dotnet/api/system.datetime.utcnow](https://learn.microsoft.com/dotnet/api/system.datetime.utcnow)
- **Documentación de `DateOnly`:**
  [learn.microsoft.com/dotnet/api/system.dateonly](https://learn.microsoft.com/dotnet/api/system.dateonly)
- **Documentación de `Environment.SpecialFolder`:**
  [learn.microsoft.com/dotnet/api/system.environment.specialfolder](https://learn.microsoft.com/dotnet/api/system.environment.specialfolder)
