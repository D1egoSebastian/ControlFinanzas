# Tareas — F-001: Entidad `Expense` en Domain con invarianteshh
> **Spec:** `spec/features/phase-03/F-001-expense-domain-entity.spec.md`
> **Fuentes de verdad:** la spec, `docs/vision.md` §4.1, §4.2,
> `docs/architecture.md` §1, §3, §4, §6, §19 (ADR-007),
> `docs/roadmap.md` §Fase 3,
> `spec/constitution/tech-stack.md` (Clean Architecture, .NET 8),
> `spec/constitution/mission.md` (dominio puro, sin duplicación),
> `spec/phases/phase-03/features.md` (F-001 dentro del desglose de la fase 3),
> `spec/features/phase-01/F-002-money-value-object.done.md` (provee `Money` y `Currency`),
> `spec/features/phase-01/F-006-contrato-interfaces-repo.done.md` (provee `IExpenseRepository` e `IIncomeRepository`),
> `spec/features/phase-01/F-007-validacion-arquitectonica.done.md` (tests NetArchTest R-1 a R-5 que F-001 no debe romper).
>
> Cada tarea es implementable en menos de 2 horas. El proyecto debe
> seguir compilando al finalizar cada una. La numeración (T-001...)
> es local a esta feature. F-001 modifica `Domain/Transactions/`
> y `Tests.Unit/Domain/`. NO modifica `Application`, `Infrastructure`,
> `UI`, `Tests.Integration`, ni `Directory.Packages.props`.

---

## Resumen de tareas

| ID | Tarea | Descripción corta | Estimación | Depende de | Verificación |
| --- | --- | --- | --- | --- | --- |
| T-001 | Eliminar `Transaction.cs` y reescribir `Income.cs` como standalone vacío | Paso atómico para evitar romper el build | S | ninguna | `dotnet build` exit 0; `IExpenseRepository` e `IIncomeRepository` resuelven | [x]
| T-002 | Crear estructura de `Expense.cs` con propiedades y factory `Create` | Sin invariantes todavía; propiedades con `private set` y dos constructores privados | S | T-001 | `dotnet build` exit 0; entidad compila | [x]
| T-003 | Implementar invariantes en `Create` (INV-1..INV-7) | Incluye tests T-2..T-11; red → green | M | T-002 | `dotnet test --filter "FullyQualifiedName~ExpenseTests.Create"` 11 tests passed | [x]
| T-004 | Implementar `Update` con re-validación (INV-8 + INV-1..INV-6) | Reasigna propiedades; valida timestamp; tests T-12..T-14 | M | T-003 | `dotnet test --filter "FullyQualifiedName~ExpenseTests.Update"` 3 tests passed | [x]
| T-005 | Verificar igualdad por referencia e inmutabilidad | Tests T-15, T-16; `Expense` no sobrescribe `Equals`/`GetHashCode` | S | T-004 | `dotnet test --filter "FullyQualifiedName~ExpenseTests.Equality"` 2 tests passed | [x]
| T-006 | Verificación final, formato y cierre | Build, tests, NetArchTest, format, rename | M | T-001 a T-005 | `dotnet test` ≈ 145 verdes; `dotnet format style --verify-no-changes` exit 0 | [x]

**Total estimado:** ~1 jornada. T-003 y T-004 son las más densas (invariantes + tests);
T-006 es la verificación más pesada; T-001, T-002 y T-005 son cortas.

---

## T-001 — Eliminar `Transaction.cs` y reescribir `Income.cs` como standalone vacío

**Objetivo**
Eliminar el placeholder abstracto `Transaction.cs` y reescribir `Income.cs` como
`public sealed class Income` standalone vacío en un único paso atómico,
preservando la compilación de `Application` (que referencia `Income` y las
interfaces `IExpenseRepository` / `IIncomeRepository`).

**Pasos**

1. Leer el archivo actual `src/ControlDeFinanzasIA.Domain/Transactions/Transaction.cs`
   y confirmar que es un placeholder abstracto.
2. Leer el archivo actual `src/ControlDeFinanzasIA.Domain/Transactions/Income.cs`
   y confirmar que hereda de `Transaction`.
3. Eliminar `src/ControlDeFinanzasIA.Domain/Transactions/Transaction.cs`.
4. Reescribir `src/ControlDeFinanzasIA.Domain/Transactions/Income.cs` como:
   ```text
   namespace ControlDeFinanzasIA.Domain.Transactions;

   /// <summary>
   /// Placeholder para la entidad de ingreso. Phase 4 lo completará.
   /// </summary>
   public sealed class Income { }
   ```
5. Asegurar file-scoped namespace, `using` al tope, XML doc mínimo.
6. Ejecutar `dotnet build` desde la raíz.
7. Verificar que `IExpenseRepository` e `IIncomeRepository` en `Application`
   siguen resolviendo (no requieren cambios).

**Verificación**

- `dotnet build "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --nologo`:
  exit 0, 0 warnings, 0 errors.
- `dotnet test --filter "FullyQualifiedName~R1_Domain"` pasa
  (no se introdujo ninguna dependencia incorrecta).

**Dependencias**
- ninguna.

**Estimación**
- S (menos de 1 hora). Es un cambio mecánico de archivos; el riesgo está en
  la compilación cruzada.

**Trazabilidad**
- AC-1 (eliminar `Transaction.cs`, reescribir `Income.cs` como standalone vacío).
- §17.1, §17.2 (justificación de eliminación y reescritura).
- §18 riesgo 1 y 2 (mitigación: paso atómico).

---

## T-002 — Crear estructura de `Expense.cs` con propiedades y factory `Create`

**Objetivo**
Crear la estructura base de `Expense.cs` como `public sealed class Expense`
standalone con las 8 propiedades públicas con `private set`, dos constructores
privados y un factory `Create` que asigna los valores sin validar invariantes
todavía.

**Pasos**

1. Crear/reescribir `src/ControlDeFinanzasIA.Domain/Transactions/Expense.cs`.
2. Usar file-scoped namespace `ControlDeFinanzasIA.Domain.Transactions`.
3. Añadir `using ControlDeFinanzasIA.Domain.ValueObjects;` para `Money`.
4. Declarar `public sealed class Expense`.
5. Añadir las propiedades con `private set`:
   - `Id` (`Guid`)
   - `CategoryId` (`Guid`)
   - `Date` (`DateOnly`)
   - `Amount` (`Money`)
   - `Description` (`string`)
   - `Note` (`string?`)
   - `CreatedAt` (`DateTime`)
   - `UpdatedAt` (`DateTime`)
6. Añadir `private Expense() { }` (parameterless para EF Core).
7. Añadir `private Expense(Guid id, Guid categoryId, DateOnly date, Money amount,
   string description, string? note, DateTime createdAt, DateTime updatedAt)`
   que asigna todas las propiedades.
8. Implementar `public static Expense Create(...)` que genere `Id` con
   `Guid.NewGuid()`, normalice `Description` (trim) y `Note` (trim → null si
   vacío), setee `UpdatedAt = createdAt`, y llame al constructor privado
   parametrizado.
9. Añadir XML doc en clase, propiedades, factory y constructores.
10. Ejecutar `dotnet build`.

**Verificación**

- `dotnet build "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --nologo`:
  exit 0, 0 warnings, 0 errors.
- `dotnet test --filter "FullyQualifiedName~R1_Domain"` pasa.
- Inspección: `Expense` no hereda de nada, no es `record`, no tiene propiedades extra.

**Dependencias**
- T-001 (debe eliminarse `Transaction.cs` primero para evitar confusión con la
  herencia; aunque el namespace no cambia, el cleanup es previo).

**Estimación**
- S (1 hora). Es mecánico si se sigue la tabla de RF-2.

**Trazabilidad**
- AC-2 (API pública: propiedades, `Create`, constructores privados).
- RF-1, RF-2, RF-3 (parcial), RF-5, RF-8.
- INV-7 parcial (`CreatedAt` / `UpdatedAt` seteados).

---

## T-003 — Implementar invariantes en `Create` (INV-1..INV-7)

**Objetivo**
Blindar el factory `Create` con las invariantes INV-1 a INV-7 y cubrir cada una
con tests red → green. Al finalizar, todos los tests de `Create` pasan.

**Pasos**

1. Asegurar que `Expense.Create` lanza:
   - `ArgumentNullException` si `amount` es `null` (INV-1).
   - `ArgumentException` si `amount.Amount <= 0` (INV-2).
   - `ArgumentException` si `date < 1900-01-01` (INV-3).
   - `ArgumentException` si `description` es vacío, whitespace o > 200 caracteres
     (INV-4).
   - `ArgumentException` si `note` no es null y tras trim > 500 caracteres
     (INV-5).
   - `ArgumentException` si `categoryId == Guid.Empty` (INV-6).
   - `UpdatedAt = createdAt` (INV-7).
2. Los mensajes de excepción deben estar en inglés, descriptivos, identificando
   argumento y valor (INV-1 a INV-6).
3. Crear `tests/ControlDeFinanzasIA.Tests.Unit/Domain/ExpenseTests.cs`
   (si no existe).
4. Escribir los tests de `Create` (T-1 a T-11):
   - T-1: happy path.
   - T-2: `null` amount.
   - T-3: zero/negative amounts.
   - T-4: empty/whitespace description.
   - T-5: description > 200.
   - T-6: empty category id.
   - T-7: date before sanity floor.
   - T-8: future date succeeds.
   - T-9: note > 500.
   - T-10: null note normalizes to null.
   - T-11: whitespace note normalizes to null.
5. Ejecutar los tests. Inicialmente fallarán si las invariantes no están
   implementadas; implementarlas hasta que pasen.
6. Ejecutar `dotnet build` y `dotnet test --filter "FullyQualifiedName~ExpenseTests.Create"`.

**Verificación**

- `dotnet build "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --nologo`:
  exit 0.
- `dotnet test "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --no-build --filter "FullyQualifiedName~ExpenseTests.Create"`:
  11 passed, 0 failed.
- `dotnet test --filter "FullyQualifiedName~R1_Domain"` pasa.

**Dependencias**
- T-002 (estructura base de `Expense` y `Create`).

**Estimación**
- M (2–3 horas). Incluye 7 invariantes + 11 tests + iteraciones red → green.

**Trazabilidad**
- INV-1, INV-2, INV-3, INV-4, INV-5, INV-6, INV-7.
- AC-3, AC-7.
- T-1 a T-11 de §11.

---

## T-004 — Implementar `Update` con re-validación (INV-8 + INV-1..INV-6)

**Objetivo**
Añadir el método `Update` que reasigna las propiedades mutables, revalida las
invariantes INV-1 a INV-6 y añade la invariante INV-8
(`updatedAt >= CreatedAt`). Cubrir con tests T-12 a T-14.

**Pasos**

1. Implementar `public void Update(Guid categoryId, DateOnly date, Money amount,
   string description, string? note, DateTime updatedAt)` en `Expense.cs`.
2. Reasignar `CategoryId`, `Date`, `Amount`, `Description`, `Note`, `UpdatedAt`
   con las mismas normalizaciones de `Create`.
3. Revalidar INV-1 a INV-6 contra los nuevos valores.
4. Validar INV-8: `updatedAt >= this.CreatedAt`; lanzar `ArgumentException` con
   mensaje descriptivo en inglés si no.
5. No modificar `Id` ni `CreatedAt`.
6. Escribir tests:
   - T-12: `Update` con args válidos refresca `UpdatedAt` y preserva `Id` /
     `CreatedAt`.
   - T-13: `Update` con args inválidos lanza `ArgumentException` (monto cero,
     descripción vacía, `CategoryId` vacío, note > 500).
   - T-14: `Update` con `updatedAt` anterior a `CreatedAt` lanza
     `ArgumentException`.
7. Ejecutar `dotnet test --filter "FullyQualifiedName~ExpenseTests.Update"`.

**Verificación**

- `dotnet build "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --nologo`:
  exit 0.
- `dotnet test "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --no-build --filter "FullyQualifiedName~ExpenseTests.Update"`:
  3 passed, 0 failed.
- `dotnet test ... --filter "FullyQualifiedName~ExpenseTests.Create"`: sigue
  11 passed.
- `dotnet test --filter "FullyQualifiedName~R1_Domain"` pasa.

**Dependencias**
- T-003 (invariantes de `Create` y tests base).

**Estimación**
- M (2 horas). Similar a `Create` pero con re-validación y timestamp.

**Trazabilidad**
- INV-8.
- RF-4.
- AC-2, AC-7.
- T-12, T-13, T-14 de §11.

---

## T-005 — Implementar igualdad por referencia e inmutabilidad

**Objetivo**
Verificar que `Expense` no sobrescribe `Equals` ni `GetHashCode`, mantiene
igualdad por referencia de `object`, y que `Id` y `CreatedAt` son inmutables.
Cubrir con tests T-15 y T-16.

**Pasos**

1. Confirmar que `Expense` no tiene `override bool Equals(object?)`,
   `override bool Equals(Expense?)`, `override int GetHashCode()`, ni
   operadores `==` / `!=`.
2. Escribir tests:
   - T-15: dos instancias creadas con mismos argumentos NO son iguales por
     referencia (`!=`).
   - T-16: la misma instancia es igual a sí misma (`==`).
3. Ejecutar `dotnet test --filter "FullyQualifiedName~ExpenseTests.Equality"`.

**Verificación**

- `dotnet build "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --nologo`:
  exit 0.
- `dotnet test "C:\Users\diego\Desktop\ControlDeFinanzasIA\ControlDeFinanzasIA.sln" -c Release --no-build --filter "FullyQualifiedName~ExpenseTests.Equality"`:
  2 passed, 0 failed.
- `dotnet test --filter "FullyQualifiedName~R1_Domain"` pasa.

**Dependencias**
- T-004 (método `Update` y tests base).

**Estimación**
- S (1 hora). Es verificación de semántica más que código nuevo.

**Trazabilidad**
- RF-6.
- AC-2 (sin override de igualdad).
- T-15, T-16 de §11.
- RNF-4 (inmutabilidad de `Id` y `CreatedAt`).

---

## T-006 — Verificación final, formato y cierre

**Objetivo**
Ejecutar la batería de verificación final de la spec: build limpio, todos los
tests existentes + nuevos verdes, NetArchTest R-1 a R-5,
`dotnet format`, pureza de dominio, y cierre de la feature.

**Pasos**

1. Ejecutar `dotnet build -c Release` y `dotnet build -c Debug`:
   0 warnings, 0 errors.
2. Ejecutar `dotnet test` completo: confirmar 131 existentes + 14 nuevos ≈ 145
   passed, 0 failed.
3. Ejecutar `dotnet test --filter "FullyQualifiedName~ArchitectureTests"`
   (o el filtro que cubra los tests NetArchTest R-1 a R-5): todos passed.
4. Ejecutar `dotnet list src/ControlDeFinanzasIA.Domain package`:
   debe estar vacío.
5. Ejecutar `dotnet format style --verify-no-changes`: exit 0.
6. Actualizar `spec/phases/phase-03/features.md`: cambiar fila F-001 a
   "✅ COMPLETADA" y header de sección.
7. Renombrar `spec/features/phase-03/F-001-expense-domain-entity.spec.md` a
   `spec/features/phase-03/F-001-expense-domain-entity.done.md`.
8. Ejecutar `dotnet build -c Release` final.

**Verificación**

- `dotnet build -c Release`: exit 0.
- `dotnet test -c Release --no-build`: 145 passed, 0 failed
  (131 existentes + 14 nuevos).
- `dotnet test --filter "FullyQualifiedName~ArchitectureTests"`: todos passed.
- `dotnet format style --verify-no-changes`: exit 0.
- `dotnet list src/ControlDeFinanzasIA.Domain package`: vacío.
- Spec renombrada a `.done.md`.

**Dependencias**
- T-001 a T-005.

**Estimación**
- M (2 horas). Verificación exhaustiva + cierre administrativo.

**Trazabilidad**
- AC-4, AC-5, AC-6, AC-8, AC-9, AC-10.
- RNF-1, RNF-2, RNF-8.

---

## Orden de ejecución recomendado

**Orden secuencial estricto:** T-001 → T-002 → T-003 → T-004 → T-005 → T-006.

- **T-001 debe ir primero** porque elimina `Transaction.cs` y reescribe
  `Income.cs`; cualquier trabajo posterior en `Expense` asume que el namespace
  `Transactions` está limpio.
- **T-002 depende de T-001** (necesita el namespace sin `Transaction`).
- **T-003 depende de T-002** (necesita la estructura de `Create` y propiedades).
- **T-004 depende de T-003** (reusa las invariantes de `Create`).
- **T-005 depende de T-004** (verifica el estado final de la entidad).
- **T-006 es verificación y cierre** de toda la feature.

**No hay paralelización útil:** F-001 es pura capa de dominio con una sola
entidad; todas las tareas son secuenciales y pequeñas. Una persona sola puede
ejecutarlas en orden.

## Definition of Done (DoD)

- [ ] AC-1: `Expense.cs` e `Income.cs` existen como `sealed class` en
  `Transactions`; `Transaction.cs` eliminado. (T-001)
- [ ] AC-2: `Expense` expone 8 propiedades con `private set`; `Create` y
  `Update` con firmas correctas; constructores privados; sin
  `Currency` / `PaymentMethodId` / `TagIds` / `SourceRuleId`. (T-002, T-004)
- [ ] AC-3: `ExpenseTests.cs` existe con ≥14 tests cubriendo happy path,
  invariantes, `Update`, igualdad e inmutabilidad; todos pasan. (T-003, T-004,
  T-005)
- [ ] AC-4: `dotnet build -c Release` y `dotnet build -c Debug` 0 warnings /
  0 errors; `dotnet test` ≈ 145 verdes. (T-006)
- [ ] AC-5: NetArchTest R-1 a R-5 pasan; `Domain` sigue sin
  `PackageReference`s. (T-006)
- [ ] AC-6: `Expense.cs` solo importa BCL + `Money` / `Currency` de dominio; no
  toca `Application`, `Infrastructure`, `UI`, EF Core, Avalonia. (T-002, T-006)
- [ ] AC-7: Cada invariante INV-1..INV-8 se valida en `Create` / `Update` sin
  dependencias; cada una tiene al menos un test. (T-003, T-004)
- [ ] AC-8: File-scoped namespaces, `using` al tope, XML doc, `sealed`,
  `dotnet format style --verify-no-changes` exit 0. (T-002, T-006)
- [ ] AC-9: Trazabilidad de invariantes y ACs a fuentes de verdad documentada
  en el código (comentarios XML) y en la spec. (T-003, T-004, T-006)
- [ ] AC-10: `spec/phases/phase-03/features.md` actualizado y spec renombrada
  a `.done.md`. (T-006)

## Riesgos y mitigaciones

| Riesgo | Probabilidad | Impacto | Mitigación |
| --- | --- | --- | --- |
| Eliminar `Transaction.cs` rompe `Income.cs` (heredero) y el build. | Alta si se hacen en tareas separadas | Alto | T-001 los fusiona en un paso atómico: eliminar `Transaction.cs` y reescribir `Income.cs` como standalone vacío en la misma tarea. Verificar con `dotnet build` antes de continuar. |
| `IIncomeRepository` no compila tras eliminar `Income.cs`. | Alta si se elimina `Income.cs` | Alto | T-001 no elimina `Income.cs`; lo reescribe como `sealed class` standalone vacío. |
| `IExpenseRepository` no compila si cambia el namespace de `Expense`. | Media | Alto | F-001 mantiene `ControlDeFinanzasIA.Domain.Transactions`; T-001/T-002 verifican que `Application` no requiere cambios. |
| La forma `sealed class` con `private set` y constructor privado dificulta el mapeo EF Core en F-002. | Media | Medio | Surfaceado en §17.5 de la spec. F-001 fija la forma; F-002 decidirá `OwnsOne` vs `ValueConverter` y coordinará con F-001 si hace falta ajustar. |
| `Money` valida `>= 0` pero `Expense` exige `> 0`; confusión de dónde validar. | Baja | Bajo | Documentado en INV-2: `Money` cero es válido para agregaciones; `Expense` rechaza cero. El test T-3 cubre el caso. |
| `DateOnly` + `private set` + constructor privado no se hidratan bien en EF Core 8 SQLite. | Baja | Medio | F-002 se encarga del mapeo; EF Core 8 soporta `DateOnly` nativamente. Si surge, F-002 añade un `ValueConverter`. |
| `CreatedAt` con `DateTimeKind.Local` se acepta porque INV-7 no enforce runtime. | Baja | Bajo | El caller (F-004) debe usar `IClock.UtcNow`. T-006 verifica que la entidad no valida `Kind` (no introduce `DateTimeKind` check). |
| Igualdad por referencia confunde a quien espere igualdad por valor como `Money`. | Media | Bajo | Documentado en RF-6 y §17.9. T-005 cubre con T-15 y T-16. |
| Tests de `Expense` no cubren un edge case que aparece en F-003 (integración SQLite). | Baja | Bajo | F-003 añade tests de integración. F-001 cubre unit tests del dominio; T-006 verifica cobertura ≥95% de `Expense`. |
| `dotnet format style` propone cambios en archivos modificados. | Baja | Bajo | T-006 aplica `dotnet format style` si `--verify-no-changes` falla, y vuelve a verificar. No se relajan reglas de `.editorconfig`. |
| NetArchTest R-1 falla porque `Expense` importa EF Core o `Application`. | Baja | Alto | T-006 verifica `dotnet list src/ControlDeFinanzasIA.Domain package` vacío. `Expense` solo importa `System` + `Money` / `Currency`. |

## Referencias

- `spec/features/phase-03/F-001-expense-domain-entity.spec.md` — la spec
  completa de F-001. Este tasks file es su descomposición técnica.
- `spec/phases/phase-03/features.md` — F-001 dentro del desglose de la fase 3.
- `docs/vision.md` — §4.1 (Gastos del día a día), §4.2 (subconjunto mínimo de
  un movimiento).
- `docs/architecture.md` — §1 (dominio puro, determinismo testeable), §3
  (proyectos), §4 (grafo de dependencias R-1..R-5), §5 (convenciones de
  carpetas), §6 (entidades vs value objects), §19 (ADR-007 `IClock`).
- `docs/roadmap.md` — §Fase 3 (slice vertical registrar/listar gastos).
- `spec/constitution/tech-stack.md` — .NET 8, EF Core 8, Avalonia, Clean
  Architecture.
- `spec/constitution/mission.md` — dominio puro, sin duplicación de lógica.
- `spec/features/phase-01/F-002-money-value-object.done.md` — `Money` y
  `Currency` que `Expense` consume.
- `spec/features/phase-01/F-003-datarange-value-object.done.md` — patrón de
  value object con `DateOnly` e invariantes.
- `spec/features/phase-01/F-005-comuntypes-result-pagedresult.done.md` —
  `Result` y `PagedResult` que F-004 / F-005 consumirán.
- `spec/features/phase-01/F-006-contrato-interfaces-repo.done.md` —
  `IExpenseRepository` e `IIncomeRepository` que referencian `Expense` /
  `Income`.
- `spec/features/phase-01/F-007-validacion-arquitectonica.done.md` — tests
  NetArchTest R-1 a R-5 que F-001 no debe romper.
- `spec/features/phase-01/F-008-composition-root.done.md` — `Bootstrap` e
  `IClock` que F-004 usará.
- `tasks/phase-02/F-002-entity-configurations-migration.tasks.md` — formato de
  referencia para este tasks file.
- `tasks/phase-02/F-005-composition-root-persistence.tasks.md` — formato de
  referencia adicional.
