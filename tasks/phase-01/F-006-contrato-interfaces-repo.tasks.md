# Tareas — F-006: Contratos de interfaces de repositorio

> **Spec:** `spec/features/phase-01/F-006-contrato-interfaces-repo.md`
> **Fuentes de verdad:** la spec, `docs/architecture.md` (que
> define las entidades y los nombres de las interfaces),
> `docs/vision.md`, `docs/roadmap.md`, `spec/constitution/*`,
> `spec/features/phase-01/F-001-skeleton-solution.done.md` (F-001
> completada, provee `Application/Abstractions/` vacío),
> `spec/features/phase-01/F-002-money-value-object.done.md` y
> `F-003-datarange-value-object.done.md` (patrón de value
> objects),
> `spec/features/phase-01/F-004-Abstracciones-clock-pathresolver.done.md`
> (convención de ubicación),
> `spec/features/phase-01/F-005-comuntypes-result-pagedresult.done.md`
> (patrón de `Result` non-generic).
>
> Cada tarea es implementable en menos de 2 horas. El proyecto
> debe seguir compilando al finalizar cada una. La numeración
> (T-001...) es local a esta feature.

---

## Resumen de tareas

| ID | Tarea | Estimación | Depende de |
| --- | --- | --- | --- |
| T-001 | Crear `IUnitOfWork` (interface con `SaveChangesAsync`) | XS | — |
| T-002 | Crear las 14 interfaces de repositorio (mismo patrón, 14 archivos) | M | T-001 |
| T-003 | Verificación final: build, format, arquitectónica, cierre | S | T-002 |

**Total estimado:** 1 jornada o menos. T-002 es la más larga
(14 interfaces con la misma estructura, cada una corta). T-001
es trivial (1 interface, 1 método). T-003 es solo verificación.

---

## T-001 — Crear `IUnitOfWork` (interface con `SaveChangesAsync`)

**Objetivo**
Establecer el contrato de unidad de trabajo que coordina las
operaciones de múltiples repositorios en una sola transacción
atómica.

**Descripción**
Crear el archivo
`src/ControlDeFinanzasIA.Application/Abstractions/IUnitOfWork.cs`
con:

```csharp
using ControlDeFinanzasIA.Application.Common;

namespace ControlDeFinanzasIA.Application.Abstractions;

/// <summary>
/// Unidad de trabajo que coordina operaciones de múltiples
/// repositorios en una sola transacción atómica.
/// </summary>
/// <remarks>
/// La implementación (en F-002 de Fase 2) envuelve
/// <c>SaveChangesAsync</c> de EF Core. Es trivial: un wrapper
/// que llama a <c>DbContext.SaveChangesAsync</c>. El rol real de
/// esta interface es: (1) permitir a los use cases coordinar
/// varios repos en una sola transacción, y (2) abstraer la
/// persistencia de la lógica de negocio.
/// </remarks>
public interface IUnitOfWork
{
    /// <summary>
    /// Persiste los cambios pendientes en el store atómicamente.
    /// </summary>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// Número de filas afectadas (útil para verificar que la
    /// operación tuvo efecto, o 0 si no había cambios pendientes).
    /// </returns>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
```

- File-scoped namespace: `namespace
  ControlDeFinanzasIA.Application.Abstractions;`.
- XML doc en la interface y el método.
- Usar el patrón de las interfaces existentes de F-004
  (`IClock`, `IPathResolver`): un archivo por interface, file-scoped
  namespace, XML doc en cada miembro público.

**Dependencias**
- F-001 (proyecto `Application` existe).

**Archivos / proyectos afectados**
- `src/ControlDeFinanzasIA.Application/Abstractions/IUnitOfWork.cs`
  (nuevo).

**Criterios de finalización**
- `IUnitOfWork.cs` existe en
  `src/.../Application/Abstractions/`.
- Interface es `public interface IUnitOfWork`.
- Un método `SaveChangesAsync` con la signatura
  `Task<int> SaveChangesAsync(CancellationToken ct = default)`.
- File-scoped namespace.
- XML doc en la interface y el método.
- `dotnet build`: 0 warnings, 0 errors.
- `Application` sigue sin `PackageReference`s.

---

## T-002 — Crear las 14 interfaces de repositorio

**Objetivo**
Definir los 14 contratos de repositorio de la fase 1 en
`Application/Abstractions/`, cada uno con el mismo patrón:
`AddAsync`, `UpdateAsync`, `RemoveAsync`, `GetByIdAsync`,
`GetAllAsync`. Sin implementación, solo contratos.

**Descripción**
Crear 14 archivos, uno por interface, todos en
`src/ControlDeFinanzasIA.Application/Abstractions/`. Cada archivo
sigue este patrón:

```csharp
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Domain.<NAMESPACE>;  // o donde esté la entidad

namespace ControlDeFinanzasIA.Application.Abstractions;

/// <summary>
/// Repositorio para gestionar agregados <see cref="<ENTITY>"/>.
/// </summary>
public interface I<NOMBRE>Repository
{
    /// <summary>
    /// Añade un nuevo <see cref="<ENTITY>"/> al store.
    /// </summary>
    /// <param name="<entity>">Entidad a añadir.</param>
    /// <param name="ct">Token de cancelación opcional.</param>
    /// <returns>
    /// <c>Result</c> con <c>IsSuccess = true</c> si la operación
    /// tuvo éxito, o con <c>Error</c> si falló (e.g., la entidad ya
    /// existe, constraint físico violado).
    /// </returns>
    Task<Result> AddAsync(<ENTITY> <entity>, CancellationToken ct = default);

    /// <summary>
    /// Actualiza un <see cref="<ENTITY>"/> existente.
    /// </summary>
    Task<Result> UpdateAsync(<ENTITY> <entity>, CancellationToken ct = default);

    /// <summary>
    /// Elimina un <see cref="<ENTITY>"/> por su ID.
    /// </summary>
    /// <returns>
    /// <c>Result</c> con <c>IsSuccess = true</c> si la operación
    /// tuvo éxito, o con <c>Error</c> si el ID no existe.
    /// </returns>
    Task<Result> RemoveAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Obtiene un <see cref="<ENTITY>"/> por su ID, o <c>null</c> si
    /// no existe.
    /// </summary>
    Task<<ENTITY>?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Obtiene todos los <see cref="<ENTITY>"/>. La colección
    /// retornada es de solo lectura.
    /// </summary>
    Task<IReadOnlyList<<ENTITY>>> GetAllAsync(CancellationToken ct = default);
}
```

Los 14 archivos a crear son:

| Archivo | Entity | Namespace de la entity |
| --- | --- | --- |
| `IExpenseRepository.cs` | `Expense` | `ControlDeFinanzasIA.Domain.Transactions` (o donde esté) |
| `IIncomeRepository.cs` | `Income` | `ControlDeFinanzasIA.Domain.Transactions` |
| `IDebtRepository.cs` | `Debt` | `ControlDeFinanzasIA.Domain.Debts` |
| `IDebtPaymentRepository.cs` | `DebtPayment` | `ControlDeFinanzasIA.Domain.Debts` |
| `IRecurringRuleRepository.cs` | `RecurringRule` | `ControlDeFinanzasIA.Domain.Recurring` |
| `IGoalRepository.cs` | `Goal` | `ControlDeFinanzasIA.Domain.Goals` |
| `IGoalContributionRepository.cs` | `GoalContribution` | `ControlDeFinanzasIA.Domain.Goals` |
| `IBudgetRepository.cs` | `Budget` | `ControlDeFinanzasIA.Domain.Budgets` |
| `ICategoryRepository.cs` | `Category` | `ControlDeFinanzasIA.Domain.Catalog` |
| `ITagRepository.cs` | `Tag` | `ControlDeFinanzasIA.Domain.Catalog` |
| `IPaymentMethodRepository.cs` | `PaymentMethod` | `ControlDeFinanzasIA.Domain.Catalog` |
| `ISavedViewRepository.cs` | `SavedView` | `ControlDeFinanzasIA.Domain.Views` |
| `IAnomalyAlertRepository.cs` | `AnomalyAlert` | `ControlDeFinanzasIA.Domain.Alerts` |
| `ISettingRepository.cs` | `Setting` | `ControlDeFinanzasIA.Domain.Settings` |

> **Nota sobre namespaces de entidades:** los namespaces exactos
> (`ControlDeFinanzasIA.Domain.Transactions`, etc.) deben
> coincidir con donde están definidas las entidades en `Domain`.
> Si los namespaces no coinciden, el `using` debe ajustarse.

**Dependencias**
- T-001.
- Las entidades (`Expense`, `Income`, etc.) deben existir como
  tipos de C# en `Domain`. Si no existen, hay dos opciones:
  - **Opción A (recomendada):** crear placeholders de las
    entidades en `Domain` con la estructura mínima (Id: Guid,
    sin otras propiedades). Esto permite que F-006 compile.
    Las features futuras refinan las entidades.
  - **Opción B:** esperar a que las entidades existan. Esto
    retrasa F-006 innecesariamente.

**Archivos / proyectos afectados**
- 14 archivos nuevos en
  `src/ControlDeFinanzasIA.Application/Abstractions/`.

**Criterios de finalización**
- 14 archivos `.cs` existen en
  `src/.../Application/Abstractions/` (uno por interface).
- Cada interface es `public interface I<NOMBRE>Repository` (o
  `IUnitOfWork`).
- Cada interface tiene los 5 métodos con las signaturas correctas.
- Cada archivo usa file-scoped namespace.
- Cada interface tiene XML doc en la interface y los métodos.
- `dotnet build`: 0 warnings, 0 errors.
- `Application` sigue sin `PackageReference`s.
- `Application` no tiene `ProjectReference` a `Infrastructure`.

---

## T-003 — Verificación final, cobertura y cierre

**Objetivo**
Validar que F-006 cumple los criterios de aceptación de la spec
(sección 14), verificar la compatibilidad arquitectónica, y dejar
el repo en estado de "F-006 terminada".

**Descripción**
Esta tarea **no introduce código nuevo**. Consiste en ejecutar
los pasos de verificación y ejecutar las acciones de cierre.

### Pasos de verificación

1. **Compilación:** `dotnet build -c Debug` y `dotnet build -c
   Release` — ambos con 0 warnings, 0 errors.
2. **Format check:** `dotnet format style --verify-no-changes` —
   exit 0.
3. **Compatibilidad arquitectónica:**
   - `grep -r "using ControlDeFinanzasIA.Application.Abstractions"
     src/ControlDeFinanzasIA.Domain` debe retornar 0 líneas (las
     entidades NO importan los repositorios).
   - `grep -r "using ControlDeFinanzasIA.Infrastructure"
     src/ControlDeFinanzasIA.Application` debe retornar 0 líneas
     (`Application` no debe referenciar a `Infrastructure`).
   - `dotnet list src/.../Application package` debe seguir
     mostrando "No packages were found".
4. **Layout:** los 15 archivos (1 de T-001 + 14 de T-002) están en
   `src/.../Application/Abstractions/`.

### Pasos de limpieza

5. **Verificar que no hay placeholders que limpiar.** F-005 y
   F-006 no tienen tests propios que limpiar.
6. **Actualizar el desglose de fase:** en
   `spec/phases/phase-01/features.md`, cambiar el estado de F-006
   de "Pendiente" a "✅ COMPLETADA" (mismo formato que F-001,
   F-002, F-003, F-004, F-005).
7. **Renombrar la spec:** mover
   `spec/features/phase-01/F-006-contrato-interfaces-repo.md`
   a
   `spec/features/phase-01/F-006-contrato-interfaces-repo.done.md`
   (convención del proyecto, ver F-001, F-002, F-003, F-004,
   F-005).

### Resumen de verificación de AC

| AC | Criterio | Estado |
| --- | --- | --- |
| AC-1 | 15 interfaces existen y compilan | 1 archivo por interface; build 0 errors |
| AC-2 | Convenciones del repo | File-scoped namespace, XML doc, naming consistente |
| AC-3 | Compatibilidad arquitectónica | `Domain` no referencia `Application/Abstractions/`; `Application` sin `PackageReference`s ni `ProjectReference` a `Infrastructure` |
| AC-4 | Cobertura de tests | N/A (F-006 no tiene tests; ver spec §4) |
| AC-5 | Verificación final | Build clean, format clean, layout OK, features.md actualizado, spec renombrada |

### Resumen de cobertura manual (N/A para F-006)

F-006 no tiene tests propios (la spec lo dice explícitamente). La
cobertura de los repositorios reales (con EF Core + SQLite)
entra en F-002 de Fase 2.

**Dependencias**
- T-001, T-002.

**Archivos / proyectos afectados**
- `spec/phases/phase-01/features.md` (modificado: marcar F-006
  como completada).
- `spec/features/phase-01/F-006-contrato-interfaces-repo.md`
  (renombrado a `.done.md`).

**Criterios de finalización**
- `dotnet build -c Debug && -c Release`: 0 warnings, 0 errors.
- `dotnet format style --verify-no-changes`: exit 0.
- 15 archivos `.cs` en
  `src/.../Application/Abstractions/` (1 de T-001 + 14 de T-002).
- `Application` sin `PackageReference`s.
- `Application` sin `ProjectReference` a `Infrastructure`.
- `Domain` no referencia `Application/Abstractions/`.
- `spec/phases/phase-01/features.md` actualizado con F-006
  marcada como completada.
- Spec renombrada a `.done.md`.

---

## Orden recomendado y trabajo en paralelo

**Orden secuencial estricto:** T-001 → T-002 → T-003.

**T-001 y T-002 son secuenciales** porque T-002 es la "carga útil" y
T-001 es trivial (1 interface, 1 método). Hacerlos en orden.

**T-002 es la tarea más larga** porque requiere crear 14 interfaces.
Si hay 2 personas, pueden repartirse las 14 interfaces por
dominio (e.g., uno hace las de transacciones, otro las de reference
data). Pero la tarea es mecánica (mismo patrón), no requiere
mucho diseño.

**T-003 es solo verificación y cierre.** No introduce código.

---

## Riesgos de la fase de tareas

| Riesgo | Probabilidad | Impacto | Mitigación |
| --- | --- | --- | --- |
| Las entidades no existen y F-006 no compila | Media | Alto | Crear placeholders en `Domain` (recomendado en spec §16.7). Si se prefiere esperar, F-006 se retrasa. |
| El test project no compila por los `Guid` o el uso de `Result` (no-genérico) | Baja | Alto | F-005 pasó con la misma config; replicar la fix (non-generic, `object?`) si el issue reaparece. |
| El formato del archivo (file-scoped namespace, XML doc) falla el format check | Baja | Medio | Seguir exactamente el formato de las interfaces de F-004. |
| 14 interfaces es mucho para una sola tarea | Baja | Bajo | T-002 es mecánica (mismo patrón). Si se quiere paralelizar, se puede separar en 2-3 sub-tareas, pero el bottleneck es el `grep` final, no la implementación. |
| Acoplamiento accidental a `Infrastructure` | Baja | Alto | F-006 no importa `Infrastructure`. Si lo hace accidentalmente, F-007 lo detecta. |
| Los namespaces de las entidades no coinciden con el plan | Baja | Medio | Verificar primero dónde están las entidades en `Domain` y ajustar los `using` accordingly. |

---

## Referencias

- `spec/features/phase-01/F-006-contrato-interfaces-repo.md` —
  la spec completa de F-006. Este tasks file es su
  descomposición técnica.
- `spec/features/phase-01/F-001-skeleton-solution.done.md` —
  F-001 completada, provee la base: `Application/Abstractions/`
  existe (vacío).
- `spec/features/phase-01/F-002-money-value-object.done.md` —
  patrón de value object.
- `spec/features/phase-01/F-003-datarange-value-object.done.md` —
  patrón de value object.
- `spec/features/phase-01/F-004-Abstraciones-clock-pathresolver.done.md` —
  convención de ubicación de interfaces.
- `spec/features/phase-01/F-005-comuntypes-result-pagedresult.done.md` —
  patrón de `Result` non-generic.
- `docs/architecture.md` — define los nombres de las entidades
  (Expense, Income, etc.) y de las interfaces, y la convención
  de no usar `IRepository<T, TId>` genérico.
- `docs/roadmap.md` § Fase 1, F-006 dentro del desglose.
- `spec/constitution/tech-stack.md` — convenciones de código.
- `spec/phases/phase-01/features.md` — F-006 dentro del
  desglose de features de la fase 1.
- `spec/README.md` — convención completa del directorio `spec/`.
