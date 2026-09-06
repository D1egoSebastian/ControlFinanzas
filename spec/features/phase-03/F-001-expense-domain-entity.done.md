# Specification — F-001: Entidad `Expense` en Domain con invariantes

> **Fase:** 3 — Slice vertical: registrar y listar gastos
> **Feature:** F-001 (local a esta fase)
> **Estado:** IMPLEMENTADA, VERIFICADA Y ARCHIVADA (cycle cerrado el 2026-08-04)
> **Spec ID:** SPEC-F001P3
>
> **Nota de numeración:** este `F-001` es el de la **Fase 3**
> (`spec/phases/phase-03/features.md`). No debe confundirse con el
> `F-001` de Fase 1 (esqueleto de solución) ni con el `F-001` de
> Fase 2 (`AppDbContext` y `AddPersistence`). Los IDs son locales a
> cada fase; el folder los desambigua. Ver `spec/README.md` para la
> convención.
>
> Fuentes de verdad: `docs/vision.md`, `docs/architecture.md`,
> `docs/roadmap.md` (§Fase 3), `spec/constitution/tech-stack.md`,
> `spec/constitution/mission.md`,
> `spec/phases/phase-03/features.md` (F-001 dentro del desglose de
> la fase 3).

---

## 1. Resumen

F-001 introduce la **primera entidad transaccional real** del
modelo de dominio: la entidad `Expense` (gasto) en el proyecto
`ControlDeFinanzasIA.Domain`, con su API pública orientada a
factory, sus invariantes blindados en el constructor, un método
de reasignación `Update`, y la batería de unit tests que la
protegen. Sin persistencia, sin use cases, sin UI: pura capa de
dominio.

Al final de la feature:

- `src/ControlDeFinanzasIA.Domain/Transactions/Expense.cs`
  existe como `public sealed class Expense` con factory
  estático `Create`, método `Update`, dos constructores privados
  (parameterless para EF Core y parametrizado para el factory),
  propiedades con `private set` y todas las invariantes
  validadas en construcción y en actualización.
- `src/ControlDeFinanzasIA.Domain/Transactions/Transaction.cs`
  (placeholder abstracto de F-006 Fase 1) **se elimina** por
  contradecir las decisiones fijadas para esta fase (ver §2 y
  §17.1).
- `src/ControlDeFinanzasIA.Domain/Transactions/Income.cs`
  (placeholder vacío que heredaba de `Transaction`) se
  reescribe como `public sealed class Income` standalone vacío,
  para que `IIncomeRepository` (Fase 1 F-006) siga compilando
  hasta que Phase 4 lo complete.
- `tests/ControlDeFinanzasIA.Tests.Unit/Domain/ExpenseTests.cs`
  existe con 14 tests cubriendo happy path, cada invariante,
  igualdad e inmutabilidad; todos pasan en `Tests.Unit`.
- `dotnet build` desde la raíz: 0 warnings, 0 errors.
- `dotnet test`: 131 tests existentes verdes + 14 nuevos de
  `Expense` ≈ 145 verdes.
- NetArchTest R-1 a R-5 (F-007 Fase 1) siguen pasando: `Domain`
  sigue sin `PackageReference`s y sin referenciar a
  `Application`, `Infrastructure`, `UI` ni `Tests.*`.

---

## 2. Contexto

La visión (`docs/vision.md` §4.1) fija como capacidad nuclear del
producto **registrar un gasto del día a día**: una compra o pago
puntual, con fecha, monto y moneda original, categoría obligatoria,
descripción y nota opcional. El roadmap (`docs/roadmap.md` §Fase
3, "Entregables esperados") materializa esta capacidad como un
slice vertical end-to-end que va de la entidad de dominio al
formulario de Avalonia, descompuesto en las 7 features F-001 a
F-007 de la fase.

F-001 es el **nodo inicial** de ese slice. Sin la entidad
`Expense` definida y blindada, no hay contrato que F-002
(persistencia), F-003 (repositorio), F-004 (use case de
escritura), F-005 (query de lectura) y F-007 (vista + ViewModel)
puedan consumir. La entidad fija la forma del dato, las reglas
que ningún caller puede violar, y la semántica de igualdad que
los repos y ViewModels asumirán. Definirla primero, con
invariantes blindados dentro de la entidad y no en los use cases
o repositorios, evita que reglas de negocio se filtren hacia
afuera del dominio (principio "Dominio puro, sin framework" de
`docs/architecture.md` §1.2 y `spec/constitution/mission.md`).

**Brecha arquitectónica que cierra esta feature.** La Fase 1
(F-006) introdujo `Transaction.cs` como `abstract record`
placeholder con `Date: DateTime`, propiedades separadas
`Amount: Money` y `Currency: Currency`, `PaymentMethodId`,
`TagIds` y `SourceRuleId?`; y `Expense.cs` / `Income.cs` como
subtipos vacíos que heredaban de ese placeholder. Esa estructura
**especula** la jerarquía `Transaction` abstracta y los campos de
fases posteriores (tags → Fase 4, método de pago → Fase 5, regla
recurrente origen → Fase 6). Las decisiones fijadas para Phase 3
resuelven explícitamente NO especular: `Expense` se introduce como
`sealed class` standalone sin base abstracta, con el subconjunto
mínimo de campos de `vision.md` §4.2 (fecha, monto+moneda
embebida, categoría, descripción, nota opcional), y `Transaction`
abstracta se difiere a un refactor trivial de Phase 4 cuando
llegue `Income`. Esta feature **reescribe** `Expense.cs` y
`Income.cs` y **elimina** `Transaction.cs` para alinear el
codebase con esa decisión (ver §17.1 y §17.2).

**Contrato que esta feature fija para F-002 a F-007.** Toda
feature posterior de la fase consume la entidad por su nombre y
su firma pública:

- F-002 mapea `Expense` a SQLite; la decisión `OwnsOne` vs
  `ValueConverter` para `Money` es de F-002 (input #4 del
  orchestrator), pero la forma de la entidad la fija F-001.
- F-003 implementa `ExpenseRepository` que persiste y recupera
  instancias de `Expense` válidas.
- F-004 construye `Expense` vía `Expense.Create(...)` desde el
  `RegisterExpenseUseCase`, pasando `createdAt` sourced desde
  `IClock.UtcNow`.
- F-005 mapea `Expense` a `ExpenseDto` para listar.
- F-007 bindea `ExpenseDto` (no la entidad) a la vista; la
  entidad nunca se filtra a la UI.

Si F-001 se equivoca en la forma de la entidad o en una
invariante, el costo se propaga a seis features. Por eso esta
spec es prescriptiva sobre la firma pública, las invariantes y
los tests.

---

## 3. Objetivo

1. **Reescribir `Expense.cs`** como `public sealed class Expense`
   standalone (sin heredar de `Transaction` ni de ninguna base),
   con factory estático `Create`, método `Update`, propiedades
   con `private set`, y todas las invariantes validadas en
   construcción y actualización.
2. **Eliminar `Transaction.cs`** (placeholder abstracto de F-006
   Fase 1) que contradice las decisiones fijadas para Phase 3.
3. **Reescribir `Income.cs`** placeholder como `public sealed
   class Income` standalone vacío, para que
   `IIncomeRepository` (Fase 1 F-006) siga compilando hasta
   Phase 4.
4. **Consumir el value object `Money`** (Fase 1 F-002) como tipo
   de la propiedad `Amount`, sin exponer una propiedad `Currency`
   separada (la moneda se accede vía `expense.Amount.Currency`).
5. **Blindar las invariantes** listadas en §8 dentro del factory
   `Create` y del método `Update`, lanzando
   `ArgumentException` / `ArgumentNullException` con mensajes
   descriptivos en inglés (convención de F-002 Fase 1 §RNF-5).
6. **No introducir persistencia, use cases, validadores de
   Application, DTOs, ni UI.** F-001 es pura capa de dominio.
7. **Cubrir la entidad con 14 unit tests** en
   `tests/ControlDeFinanzasIA.Tests.Unit/Domain/ExpenseTests.cs`
   que validen happy path, cada invariante, igualdad e
   inmutabilidad.

---

## 4. Alcance

### 4.1 En alcance

- Reescritura de
  `src/ControlDeFinanzasIA.Domain/Transactions/Expense.cs`.
- Eliminación de
  `src/ControlDeFinanzasIA.Domain/Transactions/Transaction.cs`.
- Reescritura de
  `src/ControlDeFinanzasIA.Domain/Transactions/Income.cs`
  como `sealed class` standalone vacío (placeholder mínimo,
  sin invariantes; Phase 4 lo completa).
- Creación de
  `tests/ControlDeFinanzasIA.Tests.Unit/Domain/ExpenseTests.cs`
  con 14 tests.
- Verificación de que `dotnet build` y los 131 tests existentes
  siguen pasando, más los 14 nuevos.
- Verificación de que NetArchTest R-1 a R-5 (F-007 Fase 1)
  siguen pasando: `Domain` no adquiere `PackageReference`s ni
  referencias a `Application` / `Infrastructure` / `UI` /
  `Tests.*`.

### 4.2 Fuera de alcance

- **Persistencia de `Expense`** (configuración EF Core, migración
  `AddExpenseTable`, `OwnsOne` vs `ValueConverter` para
  `Money`). Es F-002 de Fase 3.
- **Repositorio concreto `ExpenseRepository`** y extensión de
  `IExpenseRepository` con `GetByFilterAsync`. Es F-003.
- **Use cases de escritura y lectura** (`RegisterExpenseUseCase`,
  `RegisterExpenseValidator`, `GetTransactionsQuery`, DTOs).
  Son F-004 y F-005.
- **UI** (`ExpensesViewModel`, `ExpensesView`, navegación,
  DataTemplates). Son F-006 y F-007.
- **Campos `PaymentMethodId`, `TagIds`, `SourceRuleId?`** y
  propiedad separada `Currency`. Llegan en Fases 4, 5 y 6;
  F-001 no los introduce (ver §14 y §17.4).
- **Base abstracta `Transaction`**. Diferida a un refactor trivial
  de Phase 4 cuando llegue `Income` real (decisión #2 del
  orchestrator; ver §17.1).
- **Validación de que `CategoryId` referencie una `Category`
  existente.** Eso es una regla de Application (F-004) e
  Infrastructure (F-003 valida la FK contra `Categories` antes
  de `SaveChanges`). El dominio sólo valida `CategoryId !=
  Guid.Empty` (sintaxis); la semántica "existe en el catálogo"
  es cross-aggregate y no corresponde a la entidad `Expense`
  (no hay reference entre agregados; ver §17.6).
- **Soft-delete** (`IsDeleted`, query filters). Rechazado para
  v1 (ver §17.7).
- **Audit fields** (`CreatedBy`, `UpdatedBy`, `Version` /
  `RowVersion`). Rechazados para v1 (ver §17.8).
- **Conversión de moneda** (DOP ↔ USD). No es dominio
  (`Money` F-002 Fase 1 §RN-4); ocurre en presentación con un
  proveedor de tasas (Fase 5).
- **Localización de mensajes de excepción.** Los mensajes van en
  inglés en código (F-002 Fase 1 §RNF-5); la UI los traduce
  (Fase 12).

---

## 5. Casos de uso

### CU-1 — Registrar un gasto válido

- **Actor:** use case `RegisterExpenseUseCase` (F-004, futuro).
- **Trigger:** el use case recibe un comando válido, obtiene
  `createdAt` de `IClock.UtcNow`, llama
  `Expense.Create(categoryId, date, amount, description, note,
  createdAt)`.
- **Precondición:** los argumentos satisfacen las invariantes
  INV-1 a INV-7 de §8.
- **Postcondición:** existe una instancia de `Expense` con `Id`
  generado (`Guid.NewGuid()`), `UpdatedAt == CreatedAt`,
  todas las propiedades con los valores recibidos (descripción
  trimada, nota normalizada a `null` si era vacía/espacios), y
  la entidad está lista para ser pasada a
  `IExpenseRepository.AddAsync` (F-003).

### CU-2 — Rechazar un gasto con monto cero o negativo

- **Actor:** use case o test.
- **Trigger:** `Expense.Create(..., Money.Create(0m, DOP), ...)`
  o `Money.Create(-1m, DOP)`.
- **Precondición:** argumento `Amount` con `Amount.Amount <= 0`
  (o `Amount` mismo es `null`).
- **Postcondición:** el factory lanza `ArgumentException` con
  mensaje que identifica el valor inválido. No se crea ninguna
  instancia. La pila de llamadas no continúa al repositorio.

### CU-3 — Rechazar un gasto con descripción vacía o sólo espacios

- **Actor:** use case o test.
- **Trigger:** `Expense.Create(..., "", ...)` o
  `Expense.Create(..., "   ", ...)`.
- **Postcondición:** el factory lanza `ArgumentException`. La
  descripción es un campo obligatorio de un movimiento
  (`vision.md` §4.2: "nota opcional" — la descripción NO es
  opcional).

### CU-4 — Aceptar un gasto con fecha futura (gasto planeado)

- **Actor:** use case o test.
- **Trigger:** `Expense.Create(..., DateOnly(2099, 12, 31),
  ...)`.
- **Precondición:** `Date >= 1900-01-01` (sanity floor,
  decisión #1 del orchestrator).
- **Postcondición:** el factory acepta la fecha. `vision.md` §4
  no prohíbe gastos planificados; la invariante sólo rechaza
  fechas anteriores al sanity floor (ver §17.3).

### CU-5 — Actualizar un gasto existente

- **Actor:** use case `UpdateExpenseUseCase` (Phase 4, futuro)
  o test.
- **Trigger:** sobre una instancia `expense` válida, se llama
  `expense.Update(categoryId, date, amount, description, note,
  updatedAt)` con `updatedAt >= expense.CreatedAt`.
- **Postcondición:** las propiedades `CategoryId`, `Date`,
  `Amount`, `Description`, `Note` y `UpdatedAt` se reasignan;
  `Id` y `CreatedAt` son inmutables y no cambian; las
  invariantes INV-1 a INV-6 se revalidan contra los nuevos
  valores.

### CU-6 — Rechazar actualización con timestamp inconsistente

- **Actor:** use case o test.
- **Trigger:** `expense.Update(..., updatedAt: timestamp_anterior_a_CreatedAt)`.
- **Postcondición:** el método lanza `ArgumentException`. La
  auditoría no puede ir hacia atrás (invariante INV-8).

### CU-7 — Igualdad por referencia en colecciones

- **Actor:** repositorio (F-003), ViewModel (F-007), test.
- **Trigger:** comparar dos instancias de `Expense` distintas
  con los mismos valores de propiedades.
- **Postcondición:** `expenseA == expenseB` es `false` (igualdad
  por referencia, NO por valor). `Expense` es una entidad con
  identidad (`Id`), no un value object; su igualdad es la
  referencia, no el contenido. Ver §9.6 y §17.9.

---

## 6. Requisitos funcionales

### RF-1 — Tipo y ubicación

- `public sealed class Expense` en
  `src/ControlDeFinanzasIA.Domain/Transactions/Expense.cs`.
- Namespace `ControlDeFinanzasIA.Domain.Transactions` (mismo
  namespace del placeholder que reemplaza; consistente con
  `Category.cs` en `Catalog/`, `Budget.cs` en `Budgets/`, etc.
  — convención por agregado, verificada en el codebase existente
  vía `glob`).
- **NO** es un `record` (ver §17.9). **NO** hereda de ninguna
  base (ver §17.1). **NO** implementa interfaces de dominio (ni
  `IDomainEvent` ni nada similar; F-001 no introduce eventos).

### RF-2 — Propiedades públicas

La entidad expone ocho propiedades públicas con `private set`
(sólo mutables dentro de la propia clase, vía factory o `Update`):

| Propiedad | Tipo | Nulabilidad | Origen del valor | Invariante asociada |
| --- | --- | --- | --- | --- |
| `Id` | `Guid` | no nullable | generado en `Create` vía `Guid.NewGuid()` | INV-0 (no `Guid.Empty` post-construcción) |
| `CategoryId` | `Guid` | no nullable | parámetro de `Create` / `Update` | INV-6 |
| `Date` | `DateOnly` | no nullable | parámetro de `Create` / `Update` | INV-3 |
| `Amount` | `Money` | no nullable (valor de referencia; defensive guard en `Create`) | parámetro de `Create` / `Update` | INV-1, INV-2 |
| `Description` | `string` | no nullable | parámetro de `Create` / `Update`; trimado antes de almacenar | INV-4 |
| `Note` | `string?` | nullable | parámetro de `Create` / `Update`; normalizado a `null` si vacío o sólo espacios | INV-5 |
| `CreatedAt` | `DateTime` | no nullable | parámetro de `Create`; inmutable después | INV-7 |
| `UpdatedAt` | `DateTime` | no nullable | `= createdAt` en `Create`; parámetro en `Update` | INV-7, INV-8 |

**No hay propiedad `Currency` separada.** La moneda se accede
vía `expense.Amount.Currency`. Decisión #4 del orchestrator: el
mapeo de `Money` ( OwnsOne vs ValueConverter ) es decisión de
F-002; F-001 sólo fija la forma del dominio (ver §17.5).

**No hay propiedades `PaymentMethodId`, `TagIds`, `SourceRuleId`.**
Llegan en Fases 5, 4 y 6 respectivamente (ver §14).

### RF-3 — Factory estático `Create`

- Firma: `public static Expense Create(
    Guid categoryId,
    DateOnly date,
    Money amount,
    string description,
    string? note,
    DateTime createdAt)`.
- **Genera `Id` internamente** vía `Guid.NewGuid()` (decisión
  de F-002 Fase 3: `b.Property(e => e.Id).ValueGeneratedNever()`
  — el `Guid` lo provee el dominio, no la base de datos). Ver
  §17.10 para la justificación de testabilidad.
- **Valida todas las invariantes INV-1 a INV-7** (ver §8) antes
  de construir la instancia. Si alguna falla, lanza
  `ArgumentException` o `ArgumentNullException` con mensaje
  descriptivo en inglés (F-002 Fase 1 §RNF-5) que identifique el
  argumento inválido y su valor.
- **Normaliza `Description`**: trim leading/trailing whitespace
  antes de validar longitud y antes de almacenar. `"  pizza  "`
  se almacena como `"pizza"`. Si tras el trim el resultado es
  vacío, se rechaza por INV-4.
- **Normaliza `Note`**: si `note` es `null`, se almacena `null`.
  Si `note` es no-`null`, se trim y si el resultado es vacío se
  almacena `null` (canonización: "no hay nota" = `null`, no
  `""`). Si el resultado no es vacío, se almacena el valor
  trimado y validado contra INV-5.
- **Establece `UpdatedAt = createdAt`** (la creación fija el
  primer `UpdatedAt` igual al `CreatedAt`; no se puede crear un
  `Expense` con `UpdatedAt != CreatedAt`).
- **No realiza I/O, no accede al reloj, no consulta la base de
  datos.** El `createdAt` llega por parámetro; el caller (use
  case F-004) lo obtiene de `IClock.UtcNow`. Razón arquitectónica:
  `IClock` vive en `Application/Abstractions/` (Fase 1 F-004) y
  `Domain` no puede referenciar a `Application` (R-1 de F-007
  Fase 1). La entidad es pura y no puede resolver la hora por sí
  misma (ver §17.11).

### RF-4 — Método `Update`

- Firma: `public void Update(
    Guid categoryId,
    DateOnly date,
    Money amount,
    string description,
    string? note,
    DateTime updatedAt)`.
- Reasigna `CategoryId`, `Date`, `Amount`, `Description`, `Note`
  y `UpdatedAt` con los valores recibidos, aplicando las mismas
  normalizaciones que `Create` (trim de `Description`,
  normalización de `Note`).
- **Revalida las invariantes INV-1 a INV-6** contra los nuevos
  valores (un `Update` con monto cero, descripción vacía o
  `CategoryId` vacío lanza `ArgumentException` igual que
  `Create`).
- **Valida INV-8**: `updatedAt >= this.CreatedAt`. Un `Update`
  con `updatedAt` anterior a `CreatedAt` lanza
  `ArgumentException`. Esto protege la auditoría: no se puede
  "viajar al pasado".
- **No modifica `Id` ni `CreatedAt`** (son inmutables; no hay
  setter público ni privado que los exponga para mutación).
- **No realiza I/O.** El caller pasa `updatedAt` sourced desde
  `IClock.UtcNow` (F-004 / Phase 4 use cases).

### RF-5 — Constructores privados

- `private Expense() { }` — constructor sin parámetros, uso
  exclusivo de EF Core para materializar instancias desde la
  base de datos (F-002 Fase 3). El cuerpo es trivial; EF Core
  setea las propiedades vía reflection usando los `private set`.
  Necesario porque las propiedades tienen `private set` (no
  `init`): sin este constructor, EF Core no podría hidratar la
  entidad. Replicar el patrón que F-002 Fase 3 valide en la
  configuración.
- `private Expense(Guid id, Guid categoryId, DateOnly date,
  Money amount, string description, string? note, DateTime
  createdAt, DateTime updatedAt) { ... }` — constructor
  parametrizado privado, usado por el factory `Create` después
  de validar todas las invariantes. Setea todas las propiedades
  (incluido `Id` generado por el factory).

**No hay constructor público.** El único camino público para
construir un `Expense` es `Expense.Create(...)`. Esto garantiza
que ninguna instancia existe sin pasar por la validación de
invariantes (mismo principio que `Money.Create` en F-002 Fase 1
§RF-2 y `DateRange.Create` en F-003 Fase 1 §RF-2).

### RF-6 — Igualdad e identidad

- `Expense` es una `sealed class` (no `record`); hereda la
  igualdad por referencia de `object` (no se sobrescriben
  `Equals` ni `GetHashCode`).
- Dos instancias de `Expense` son iguales **si y sólo si** son
  la misma referencia. Dos instancias distintas con los mismos
  valores de propiedades NO son iguales.
- `Expense` es una **entidad** (con identidad `Id`), no un value
  object. Su igualdad es por referencia, no por valor. Esto
  diverge intencionalmente de `Money` y `DateRange` (value
  objects con igualdad por valor vía `record`).
- F-001 no introduce igualdad por `Id` (i.e., override
  `Equals(Expense other) => Id == other.Id`). Razón: para v1,
  los repositorios (F-003) devuelven la instancia trackeada y
  los tests verifican propiedades, no igualdad. Si Phase 4
  necesita colecciones con `Contains` basado en `Id`, se
  introduce en ese momento (ver §17.9).

### RF-7 — Tests unitarios

- Archivo:
  `tests/ControlDeFinanzasIA.Tests.Unit/Domain/ExpenseTests.cs`.
- Namespace: `ControlDeFinanzasIA.Tests.Unit.Domain`.
- Framework: xUnit + FluentAssertions (convención de Fase 1
  §15). `[Fact]` para casos únicos, `[Theory]` + `[InlineData]`
  para casos parametrizados (montos, descripciones, fechas).
- 14 tests (ver §11 para el listado completo y la batería).

### RF-8 — Convenciones de código

- File-scoped namespace (Fase 1 §5, convención global).
- `using` directives al tope (nunca después del namespace).
- XML doc exhaustivo en la clase, cada propiedad pública, el
  factory `Create`, el método `Update` y cada constructor
  privado (consistente con `Money.cs` F-002 Fase 1 y
  `DateRange.cs` F-003 Fase 1).
- PascalCase para tipos y miembros públicos; `camelCase` para
  parámetros y locales; `_camelCase` para campos privados (si
  los hay — la entidad no necesita campos privados porque las
  propiedades con `private set` son el almacenamiento).
- `sealed` en la clase (no se espera herencia; ver §17.1).
- Sin `#region`, sin `#if DEBUG`, sin pragmas de warnings.

---

## 7. Requisitos no funcionales

### RNF-1 — Pureza de Dominio (regla R-1 de F-007 Fase 1)

- `Expense.cs` no depende de EF Core, Avalonia, ni de ningún
  paquete externo. Sólo BCL (`System`, `System.Globalization` si
  se necesita para normalización) y del propio dominio
  (`Money` de `ValueObjects/`, `Currency` de `Enums/`).
- **Verificable:** tras F-001, `dotnet list
  src/ControlDeFinanzasIA.Domain package` debe seguir vacío
  (cero `PackageReference`s).
- **Verificable:** el test R-1 de F-007 Fase 1
  (`R1_Domain_No_Depende_De_Application_Infra_UI_o_Tests`) debe
  seguir pasando. La nueva entidad no introduce `using
  ControlDeFinanzasIA.Application` ni nada similar.

### RNF-2 — NetArchTest R-2 a R-5 siguen pasando

- R-2 (`Application` sólo depende de `Domain` +
  `FluentValidation`): F-001 no toca `Application`; sigue
  pasando.
- R-3 (`Infrastructure` depende de `Application` + `Domain`):
  F-001 no toca `Infrastructure`; sigue pasando.
- R-4 (`UI` no depende de `Infrastructure` salvo composition
  root): F-001 no toca `UI`; sigue pasando.
- R-5 (ningún proyecto de `src/` depende de `Tests.*`): F-001 no
  toca referencias de proyecto; sigue pasando.

### RNF-3 — Determinismo

- `Expense.Create` es determinista salvo por `Guid.NewGuid()`
  (la única fuente de no-determinismo es la generación del
  `Id`; el resto de las propiedades se setean exactamente con
  los valores recibidos, tras normalización).
- Los tests no asumen un `Id` específico; verifican
  `expense.Id != Guid.Empty` y, cuando necesitan un `Id`
  conocido para búsquedas, lo leen de la instancia creada
  (`var id = expense.Id;`) y lo usan en la siguiente operación.
- `CreatedAt` y `UpdatedAt` provienen del caller (que en tests
  es un `DateTime` fijo, p. ej. `new DateTime(2026, 1, 1,
  DateTimeKind.Utc)`). `Expense.Create` no llama a
  `DateTime.UtcNow` (violación ADR-007; `IClock` es la
  abstracción y vive en Application).

### RNF-4 — Inmutabilidad de identidad y auditoría de creación

- `Id` y `CreatedAt` son inmutables post-construcción: no hay
  setter público ni método que los reasigne. `Update` no los
  toca.
- Las propiedades `CategoryId`, `Date`, `Amount`, `Description`,
  `Note`, `UpdatedAt` son mutables sólo vía `Update` (no hay
  setter público; el `private set` sólo es accesible dentro de
  la clase, y el único método público que muta es `Update`).
- No se exponen métodos `With...` (estilo `record`); la entidad
  se muta in-place vía `Update`. Razón: la entidad tiene
  identidad; una "copia modificada" rompería la identidad. Si
  Phase 4 necesita "copia modificada", se introduce en ese
  momento (ver §17.9).

### RNF-5 — Cobertura de tests >= 95% en `Expense`

- Cobertura medida con `coverlet.collector` (Fase 1 §15;
  configuración heredada).
- Cubre: factory happy path, cada invariante con caso fallido,
  normalización de `Description` y `Note`, `Update` happy path
  y re-validación, igualdad por referencia, inmutabilidad de
  `Id` y `CreatedAt`.

### RNF-6 — Sin estado estático

- `Expense` no tiene campos `static`, no tiene cache, no tiene
- singletons. Cada instancia es independiente. `Guid.NewGuid()`
  es la única fuente de "estado global" (el generador de GUIDs
  del runtime), y es thread-safe.

### RNF-7 — Mensajes de excepción en inglés

- Consistente con F-002 Fase 1 §RNF-5: los mensajes de
  `ArgumentException` / `ArgumentNullException` están en inglés
  en el código (convención de la comunidad .NET y de las
  librerías de Microsoft). La UI los traduce al español si lo
  necesita (Fase 12, i18n).

### RNF-8 — Cero warnings

- `dotnet build -c Release` desde la raíz: 0 warnings, 0
  errors.
- `dotnet build -c Debug` desde la raíz: 0 warnings, 0
  errors.
- `dotnet format style --verify-no-changes`: exit 0.

---

## 8. Reglas de negocio e invariantes

Cada invariante es una regla positiva, trazable a la fuente de
verdad, enforceable en `Create` y `Update` sin dependencias
externas, y testable en un unit test sin DB. La numeración
INV-1..INV-8 se reutiliza en §11 (tests) y §19 (trazabilidad).

### INV-1 — `Amount` no es `null`

- **Regla:** el argumento `amount` pasado a `Create` y `Update`
  no es `null`.
- **Enforcement:** `ArgumentNullException(nameof(amount))` si
  `amount is null`.
- **Trazabilidad:** `Money` es un `record` (reference type);
  sin este guard, un `null` provocaría `NullReferenceException`
  al acceder a `amount.Amount` (INV-2), con mensaje opaco. El
  guard explícito produce un error claro y tipado.

### INV-2 — `Amount.Amount` es estrictamente positivo

- **Regla:** `amount.Amount > 0` (estrictamente mayor que cero;
  ni cero ni negativo).
- **Enforcement:** `ArgumentException` con mensaje `"Amount
  must be strictly positive. Received: {amount.Amount}."`.
- **Trazabilidad:** `vision.md` §4.1 (gasto del día a día — un
  gasto de monto cero no es gasto); decisión #3 del
  orchestrator (`Amount > 0` se enforza en la entidad
  `Expense`, no en el value object `Money`; `Money` F-002 Fase
  1 §RF-3 sólo valida `>= 0` para soportar agregaciones
  `Money.Zero`).
- **Nota:** `Money.Create(0m, DOP)` es válido (cero es un Money
  válido para agregaciones). La invariante `> 0` es específica
  de la entidad `Expense`, no del value object. Un `Income`
  (Phase 4) podrá decidir su propia regla (probablemente
  también `> 0`).

### INV-3 — `Date >= 1900-01-01` (sanity floor); fechas futuras permitidas

- **Regla:** `date >= new DateOnly(1900, 1, 1)`. No hay techo
  superior: las fechas futuras son válidas (gastos planeados).
- **Enforcement:** `ArgumentException` con mensaje `"Date must
  be on or after 1900-01-01. Received: {date:o}."`.
- **Trazabilidad:** decisión #1 del orchestrator (`Expense.Date`
  admite fechas futuras; la invariante sólo rechaza `Date <
  1900-01-01` como sanity floor); `vision.md` §4.1 no prohíbe
  gastos planificados. El floor `1900-01-01` se eligió porque
  cualquier fecha real de un movimiento personal cae en la era
  moderna; fechas pre-1900 son errores de entrada. El usuario
  puede refinar este floor en §17.3 antes del apply.

### INV-4 — `Description` no vacía, no sólo espacios, longitud ≤ 200

- **Regla:** tras trimar `description`, el resultado no es
  vacío ni `null`, y su longitud es ≤ 200 caracteres.
- **Enforcement:** `ArgumentException` con mensajes
  diferenciados: `"Description is required."` (vacío o sólo
  espacios) o `"Description must be at most 200 characters.
  Received: {length}."` (longitud excedida).
- **Trazabilidad:** `vision.md` §4.2 (descripción es parte del
  subconjunto mínimo de un movimiento; la nota es la opcional,
  no la descripción); phase plan F-001 (`Description` no vacío
  ni sólo espacios; `HasMaxLength(200)`).
- **Normalización:** `description` se trim antes de validar y
  antes de almacenar. `"  pizza  "` → `"pizza"`. Si tras trim
  es vacío, se rechaza.

### INV-5 — `Note` si no es `null`, longitud ≤ 500

- **Regla:** si `note` es `null`, se almacena `null`. Si
  `note` es no-`null`, se trim y si el resultado es vacío se
  almacena `null`; si el resultado no es vacío, su longitud
  debe ser ≤ 500 caracteres.
- **Enforcement:** `ArgumentException` con mensaje `"Note must
  be at most 500 characters. Received: {length}."`.
- **Trazabilidad:** `vision.md` §4.2 ("Nota opcional"); phase
  plan F-001 (`Note`, si presente, `HasMaxLength(500)`).
- **Normalización:** canonización "no hay nota" = `null` (no
  `""`). Evita la ambigüedad `null` vs `""` en persistencia
  (F-002) y en DTOs (F-004).

### INV-6 — `CategoryId != Guid.Empty`

- **Regla:** `categoryId != Guid.Empty`.
- **Enforcement:** `ArgumentException` con mensaje
  `"CategoryId must be a non-empty Guid."`.
- **Trazabilidad:** `vision.md` §4.2 ("Categoría" — obligatoria
  en cada movimiento); phase plan F-001
  (`CategoryId != Guid.Empty`).
- **Nota:** la validación de que `CategoryId` referencie una
  `Category` existente en el catálogo es cross-aggregate y
  corresponde a `ExpenseRepository.AddAsync` (F-003) y al
  `RegisterExpenseValidator` (F-004), no a la entidad
  (ver §17.6). El dominio valida la sintaxis (`!= Guid.Empty`);
  la semántica (existe en `Categories`) es de Application /
  Infrastructure.

### INV-7 — `CreatedAt` y `UpdatedAt` con `DateTimeKind.Utc`

- **Regla:** el caller pasa `createdAt` con
  `DateTimeKind.Utc`. `Expense.Create` no fuerza el `Kind`
  (defensive: si el caller pasa `Local` o `Unspecified`, se
  acepta pero se documenta que el caller es responsable).
- **Enforcement:** F-001 no valida el `Kind` en runtime (no
  lanza si `createdAt.Kind != Utc`). Razón: el contrato lo
  enforce el use case (F-004) que obtiene `createdAt` de
  `IClock.UtcNow`, cuya implementación real `SystemClock`
  garantiza `DateTimeKind.Utc` (Fase 1 F-004 spec §RNF-1). Un
  test del use case (F-004) verifica que `IClock.UtcNow` se
  usa, no `DateTime.UtcNow` directo.
- **Trazabilidad:** ADR-007 (`docs/architecture.md` §19, tabla
  ADRs: "`IClock` y `IPathResolver` inyectados en todo lo
  dependiente de tiempo/ruta").
- **`UpdatedAt` en `Create`:** se setea igual a `createdAt`
  (la creación fija el primer `UpdatedAt`). No se puede crear
  un `Expense` con `UpdatedAt != CreatedAt`.

### INV-8 — En `Update`, `updatedAt >= CreatedAt`

- **Regla:** el argumento `updatedAt` de `Update` debe ser `>=
  this.CreatedAt`.
- **Enforcement:** `ArgumentException` con mensaje
  `"UpdatedAt must be on or after CreatedAt. CreatedAt:
  {this.CreatedAt:o}, received: {updatedAt:o}."`.
- **Trazabilidad:** invariante de auditoría; derivada del
  principio de que la auditoría no viaja al pasado
  (`docs/architecture.md` §1.7, "Determinismo testeable" +
  sentido común de auditoría).
- **Nota:** `UpdatedAt == CreatedAt` es válido en `Update`
  (una actualización "instantánea" a la creación). `UpdatedAt
  < CreatedAt` es lo único rechazado.

---

## 9. Modelo de dominio — API surface

Esta sección describe la API pública de `Expense` en forma
narrativa. No incluye bloques de código (restricción del
orchestrator: zero `csharp` code blocks); los símbolos se
referencian inline.

### 9.1 Identidad

La identidad de un `Expense` es su propiedad `Id: Guid`. El
`Guid` se genera **dentro** del factory `Create` vía
`Guid.NewGuid()` (no lo provee la base de datos, no lo pasa el
caller). Esto alinea con la decisión de F-002 Fase 3
(`b.Property(e => e.Id).ValueGeneratedNever()`) y evita que el
caller tenga que generar el `Guid` (reducción de superficie de
error). El `Id` es inmutable post-construcción: no hay setter
público ni método que lo reasigne, y `Update` no lo toca.

Determinismo en tests: los tests no asumen un `Id` específico.
Verifican `expense.Id != Guid.Empty`. Cuando un test necesita
el `Id` para una operación subsiguiente (p. ej. buscar la
entidad por `Id` en un repositorio mockeado), lo lee de la
instancia creada (`var id = expense.Id;`). No se introduce un
overload `CreateWithId(Guid id, ...)` porque no hay necesidad
real y abriría la puerta a `Id`s duplicados (ver §17.10).

### 9.2 Manejo de `Money` y `Currency`

La propiedad `Amount: Money` (value object de Fase 1 F-002)
almacena el monto y la moneda embebidos. `Expense` **no expone
una propiedad `Currency` separada**; la moneda se accede vía
`expense.Amount.Currency`. Razones:

- `Money` ya encapsula `Amount` + `Currency` como un value
  object inmutable con validación (`Money.Create`).
- Duplicar `Currency` como propiedad plana de `Expense`
  rompería la invariante "la moneda del gasto es la moneda del
  monto" (podrían desincronizarse).
- El mapeo EF Core de `Money` ( OwnsOne vs ValueConverter ) es
  decisión de F-002 (input #4 del orchestrator); F-001 sólo
  fija que `Amount: Money` es la propiedad, y F-002 decide cómo
  se descompone en columnas.

Persistencia: en Phase 3, el `Currency` efectivamente
persistido será `Currency.DOP` por defecto (phase plan F-001:
"en Phase 3 sólo se persistirá `Currency.DOP` por defecto; ver
F-004"). Esto es una restricción del use case / validador
(F-004), no de la entidad. La entidad acepta `Money` con
cualquier `Currency` válida (DOP o USD); F-004 decide cuáles
acepta en Phase 3 y cuáles difiere a Fase 5.

### 9.3 Manejo de `Date`

La propiedad `Date: DateOnly` (no `DateTime`). Justificación
en una frase: un gasto ocurre en un día calendario, sin
componente de hora ni zona horaria, y `DateOnly` (BCL .NET 6+)
es el tipo canónico para esa semántica — además alinea con
`DateRange` de Fase 1 F-003 que también usa `DateOnly`, y EF
Core 8 mapea `DateOnly` nativamente a `TEXT` ISO-8601 en SQLite
sin `ValueConverter`.

`Date` admite fechas futuras (gasto planeado, INV-3); sólo
rechaza fechas anteriores a `1900-01-01` (sanity floor).

### 9.4 Referencias FK

La propiedad `CategoryId: Guid` es la única FK expuesta por
`Expense` en F-001. Es una referencia por `Guid` (no una
propiedad navegable `Category Category`): la entidad `Expense`
no conoce la entidad `Category` ni su agregado; sólo conoce el
identificador. Esto sigue el principio "no references between
aggregates" de DDD tactical: `Expense` y `Category` son
agregados separados, y la referencia es por `Id`, no por
objeto. La validación de que `CategoryId` exista en `Categories`
es cross-aggregate y corresponde a `ExpenseRepository.AddAsync`
(F-003) y al `RegisterExpenseValidator` (F-004), no a la
entidad (ver §17.6).

`PaymentMethodId` **no se incluye** en F-001 (llega en Fase 5).
`TagIds` **no se incluye** (llega en Fase 4). `SourceRuleId?`
**no se incluye** (llega en Fase 6 con la generación de
recurrentes).

### 9.5 Texto libre

`Description: string` (no nullable, obligatorio, longitud ≤
200, trimado). Representa la descripción corta del gasto
("pizza", "gasolina", "cena con amigos").

`Note: string?` (nullable, opcional, longitud ≤ 500 si
no-`null`, normalizado a `null` si vacío o sólo espacios).
Representa una nota más larga o contexto adicional ("cumpleaños
de María", "gasto compartido con Pedro — le debo la mitad").

La distinción `Description` obligatoria / `Note` opcional viene
de `vision.md` §4.2: la nota es explícitamente opcional, la
descripción no se marca como opcional (es parte del subconjunto
mínimo de un movimiento).

### 9.6 Metadata de sistema (`CreatedAt`, `UpdatedAt`)

`CreatedAt: DateTime` y `UpdatedAt: DateTime` son los campos
de auditoría mínimos. Se setean en `Create` con el valor de
`createdAt` recibido por parámetro (`UpdatedAt = createdAt` en
la creación). `Update` reasigna `UpdatedAt` con el valor
recibido; `CreatedAt` es inmutable.

**Set by whom:** el caller (use case F-004 para escritura, use
case Phase 4 para edición) pasa `createdAt` / `updatedAt`
sourced desde `IClock.UtcNow`. `Expense` no llama a
`DateTime.UtcNow` ni a `IClock` (no puede: `IClock` vive en
`Application` y `Domain` no referencia `Application`, R-1 de
F-007 Fase 1). La entidad es pura y recibe el timestamp como
dato. ADR-007 (`docs/architecture.md` §19) fija el principio:
"`IClock` y `IPathResolver` inyectados en todo lo dependiente
de tiempo/ruta". La entidad es "dependiente de tiempo" sólo
en el sentido de que registra timestamps; la dependencia se
resuelve en el use case, no en la entidad.

**Soft-delete:** NO para v1. No se introduce `IsDeleted`,
query filters, ni comportamiento de "undo delete". Razones
(ver §17.7): la app es single-user local sin requisito de
recuperación; `RemoveAsync` (F-003) hace hard delete; añadir
soft-delete ahora es especular un requisito que la visión no
menciona. Phase 4+ puede revisitar si el usuario pide
"papelera".

**Audit fields:** NO para v1 más allá de `CreatedAt` /
`UpdatedAt`. No se introducen `CreatedBy` / `UpdatedBy` (no
hay identidad de usuario: single-user, `vision.md` §2 "Cada
instalación es un único usuario"). No se introduce `Version` /
`RowVersion` (no hay concurrencia: single-process,
`docs/architecture.md` §12 "Concurrencia: Single-user, un solo
proceso a la vez").

### 9.7 Igualdad e identidad

`Expense` es una `sealed class` (no `record`). Su igualdad es
la de `object`: por referencia. Dos instancias de `Expense`
son iguales si y sólo si son la misma referencia. Dos
instancias distintas con los mismos valores de propiedades
(incluyendo el mismo `Id`) NO son iguales por `==` /
`Equals(object)`.

Esto diverge intencionalmente de `Money` y `DateRange` (value
objects `record` con igualdad por valor). `Expense` es una
entidad con identidad (`Id`); su igualdad natural es la
referencia. No se sobrescribe `Equals` para comparar por `Id`
en F-001 (ver §17.9): para v1, los repositorios devuelven la
instancia trackeada y los tests verifican propiedades, no
igualdad. Si Phase 4 necesita `Contains` basado en `Id` en una
colección, se introduce en ese momento como override
explícito.

---

## 10. Configuración técnica

### 10.1 — Ubicación de archivos

| Archivo | Acción | Justificación |
| --- | --- | --- |
| `src/ControlDeFinanzasIA.Domain/Transactions/Expense.cs` | Reescribir | Placeholder vacío de F-006 Fase 1; se reemplaza por `sealed class` standalone con factory, invariantes y `Update`. |
| `src/ControlDeFinanzasIA.Domain/Transactions/Transaction.cs` | Eliminar | Placeholder abstracto que contradice la decisión #2 del orchestrator (no especular base `Transaction` en F-001). |
| `src/ControlDeFinanzasIA.Domain/Transactions/Income.cs` | Reescribir | Placeholder vacío que heredaba de `Transaction`; se reescribe como `sealed class` standalone vacío para que `IIncomeRepository` (Fase 1 F-006) siga compilando. Phase 4 lo completa. |
| `tests/ControlDeFinanzasIA.Tests.Unit/Domain/ExpenseTests.cs` | Crear | Nueva carpeta `Domain/` bajo `Tests.Unit` (no existe; F-001 la introduce para agrupar tests de entidades — los de value objects están en `ValueObjects/`, los de tipos comunes en `Common/`). |

**Nota sobre la convención de carpetas.** El phase plan dice
`src/ControlDeFinanzasIA.Domain/Entities/Expense.cs`, pero el
codebase existente (verificado vía `glob`) usa la convención
por agregado: `Transactions/`, `Catalog/`, `Debts/`, `Budgets/`,
`Goals/`, `Recurring/`, `Alerts/`, `Settings/`, `Views/`,
`Enums/`, `ValueObjects/`. No hay carpeta `Entities/`. Esta
spec sigue la convención del codebase (`Transactions/`) por
consistencia con el resto del dominio y para minimizar el churn
(ver §17.12). `architecture.md` §5 menciona `Entities/` pero es
aspiracional; el código real usa agregados.

### 10.2 — Sin paquetes NuGet nuevos

F-001 no añade ningún `PackageReference` ni `PackageVersion` a
`Directory.Packages.props`. `Domain` sigue sin paquetes (regla
R-1 de F-007 Fase 1). Los tests usan xUnit + FluentAssertions
ya presentes (Fase 1 §15).

### 10.3 — Sin migraciones EF Core

F-001 no genera migraciones ni toca `AppDbContext`. La
persistencia de `Expense` es F-002 Fase 3.

### 10.4 — Sin modificaciones a `Application`, `Infrastructure`, `UI`

F-001 sólo toca `Domain` y `Tests.Unit`. `Application`
(`IExpenseRepository` ya existe de Fase 1 F-006 y referencia el
tipo `Expense` del namespace `Domain.Transactions`; el
namespace no cambia, así que `Application` sigue compilando
sin modificaciones), `Infrastructure` (sin `AppDbContext`
cambios), `UI` (sin cambios) quedan intactos.

### 10.5 — Convenciones de código

- File-scoped namespace (Fase 1 §5, regla global).
- `using` directives al tope (nunca después del namespace).
- XML doc exhaustivo en la clase, cada propiedad pública, el
  factory `Create`, el método `Update` y cada constructor
  privado (consistente con `Money.cs` F-002 Fase 1).
- `sealed` en la clase `Expense` y en la clase `Income`
  (placeholder reescrito).
- Sin `#region`, sin `#if DEBUG`, sin pragmas.

---

## 11. Validaciones — batería de tests

`tests/ControlDeFinanzasIA.Tests.Unit/Domain/ExpenseTests.cs`
con 14 tests. Cubre happy path, cada invariante (INV-1 a INV-8),
normalización, `Update`, igualdad e inmutabilidad. Usa xUnit
`[Fact]` para casos únicos y `[Theory]` + `[InlineData]` para
casos parametrizados (montos, descripciones, fechas).

| # | Test | Tipo | Invariante cubierta |
| --- | --- | --- | --- |
| T-1 | `Create_WithValidArgs_ReturnsExpenseWithExpectedState` | `[Fact]` | Happy path; verifica `Id != Guid.Empty`, todas las propiedades con valores recibidos, `Description` trimada, `Note` normalizada, `UpdatedAt == CreatedAt`. |
| T-2 | `Create_WithNullAmount_ThrowsArgumentNullException` | `[Fact]` | INV-1 |
| T-3 | `Create_WithZeroOrNegativeAmount_ThrowsArgumentException` | `[Theory]` `[InlineData(0)]` `[InlineData(-1)]` `[InlineData(-0.01)]` | INV-2 |
| T-4 | `Create_WithEmptyOrWhitespaceDescription_ThrowsArgumentException` | `[Theory]` `[InlineData("")]` `[InlineData("   ")]` `[InlineData("\t\n")]` | INV-4 (vacío / sólo espacios) |
| T-5 | `Create_WithDescriptionOverMaxLength_ThrowsArgumentException` | `[Fact]` (descripción de 201 caracteres) | INV-4 (longitud) |
| T-6 | `Create_WithEmptyCategoryId_ThrowsArgumentException` | `[Fact]` (`Guid.Empty`) | INV-6 |
| T-7 | `Create_WithDateBeforeSanityFloor_ThrowsArgumentException` | `[Theory]` `[InlineData(1899, 12, 31)]` `[InlineData(1900, 1, 0)]` (si el compilador lo permite; si no, `1899-12-31`) | INV-3 (floor) |
| T-8 | `Create_WithFutureDate_Succeeds` | `[Fact]` (`DateOnly(2099, 12, 31)`) | INV-3 (futuro permitido — decision #1) |
| T-9 | `Create_WithNoteOverMaxLength_ThrowsArgumentException` | `[Fact]` (nota de 501 caracteres) | INV-5 (longitud) |
| T-10 | `Create_WithNullNote_NormalizesToNull` | `[Fact]` | INV-5 (normalización `null` → `null`) |
| T-11 | `Create_WithWhitespaceNote_NormalizesToNull` | `[Fact]` (`"   "` → `null`) | INV-5 (normalización whitespace → `null`) |
| T-12 | `Update_WithValidArgs_RefreshesUpdatedAt_PreservesIdAndCreatedAt` | `[Fact]` | Update happy path + inmutabilidad de `Id` / `CreatedAt` |
| T-13 | `Update_WithInvalidArgs_ThrowsArgumentException` | `[Theory]` cubriendo monto cero, descripción vacía, `CategoryId` vacío, nota sobre límite | Re-validación INV-1 a INV-6 en `Update` |
| T-14 | `Update_WithUpdatedAtBeforeCreatedAt_ThrowsArgumentException` | `[Fact]` | INV-8 |
| T-15 | `Equality_ByReference_TwoInstancesNotEqual` | `[Fact]` (dos `Create` con mismos argumentos; `!=`) | Igualdad por referencia (RF-6) |
| T-16 | `Equality_SameInstance_IsEqual` | `[Fact]` (`var e = Create(...); e == e`) | Sanity de igualdad por referencia |

> **Nota sobre el conteo:** se listan 16 tests, no 14 como en el
> resumen. T-10 y T-11 pueden colapsarse en un `[Theory]`
> (`Create_WithNullOrWhitespaceNote_NormalizesToNull`) para
> llegar a 15; T-15 y T-16 pueden colapsarse en un `[Theory]`
> para llegar a 14. El implementador decide la granularity;
> esta spec garantiza cobertura de las 8 invariantes + happy
> path `Create` + happy path `Update` + igualdad + inmutabilidad.
> Mínimo exigido por AC-3: 14 tests cubriendo todos los casos
> listados.

---

## 12. Casos borde

### EC-1 — `Amount = decimal.MaxValue`

- Válido: `Money.Create(decimal.MaxValue, DOP)` acepta, y
  `Expense.Create(..., thatMoney, ...)` acepta. La aritmética
  futura (sumas en reportes) puede causar `OverflowException`;
  documentado en F-002 Fase 1 §EC-1. `Expense` no valida techo
  superior.

### EC-2 — `Description` exactamente de 200 caracteres

- Válido (longitud ≤ 200 es la invariante; 200 incluido). Test
  implícito en T-1 (que usa una descripción corta); el
  implementador puede añadir un `[Theory]` con 200 y 201 para
  cubrir el boundary explícito.

### EC-3 — `Note` exactamente de 500 caracteres

- Válido (longitud ≤ 500 si no-`null`). Misma consideración que
  EC-2 para el boundary.

### EC-4 — `Date = 1900-01-01` (exactamente el floor)

- Válido (invariante es `>= 1900-01-01`, inclusivo). Test
  implícito en T-8 (que usa una fecha futura); el
  implementador puede añadir un caso `[InlineData(1900, 1, 1)]`
  en T-7/T-8 para cubrir el boundary.

### EC-5 — `Date = 1899-12-31` (un día antes del floor)

- Inválido. T-7 lo cubre. Mensaje esperado: `"Date must be on
  or after 1900-01-01. Received: 1899-12-31."`.

### EC-6 — `Date = DateOnly.MaxValue` (`9999-12-31`)

- Válido (no hay techo superior). Prueba que las fechas
  futuras "extremas" se aceptan. El implementador puede añadir
  un caso en T-8 si quiere cubrir el extremo.

### EC-7 — `Note = ""` vs `Note = null` vs `Note = "   "`

- Los tres se normalizan a `null` (canonización). T-10 (`null`)
  y T-11 (`"   "`) cubren los casos; `""` se cubre con un
  `[InlineData]` extra en el mismo `[Theory]` si se colapsan.
  Post-construcción, `expense.Note` es `null` para los tres
  casos. `expense.Note == null` (no `expense.Note == ""`).

### EC-8 — `Description = "  pizza  "` (con espacios)

- Se trim a `"pizza"` antes de validar y almacenar.
  `expense.Description == "pizza"`. La longitud validada es la
  del trim (`5`), no la del input original (`9`). T-1 cubre
  implícitamente; el implementador puede añadir un caso
  explícito.

### EC-9 — `CreatedAt` con `DateTimeKind.Local`

- `Expense.Create` no fuerza el `Kind` (no lanza). Se acepta.
  El caller es responsable de pasar UTC (INV-7 contrato, no
  enforcement runtime). Si el caller pasa `Local`, la entidad
  lo almacena tal cual; la auditoría quedará inconsistente. La
  defensa es el use case (F-004) que obtiene `createdAt` de
  `IClock.UtcNow` (que garantiza UTC). Un test del use case
  (F-004) verifica que se usa `IClock.UtcNow`.

### EC-10 — `Update` con `UpdatedAt == CreatedAt`

- Válido (invariante es `>= CreatedAt`, inclusivo). Representa
  una actualización "instantánea" a la creación. T-12 cubre
  implícitamente; el implementador puede añadir un caso
  explícito.

### EC-11 — `Expense` después de `Update`: `Id` no cambia

- `Update` no reasigna `Id`. Test T-12 verifica
  `expense.Id == idBeforeUpdate`. La identidad es inmutable.

### EC-12 — Dos `Create` con los mismos argumentos: `Id`s distintos

- `Guid.NewGuid()` genera `Guid`s únicos. Dos `Create` con los
  mismos argumentos producen dos instancias con `Id`s
  distintos. Test implícito en T-15 (dos instancias no son
  iguales por referencia, y sus `Id`s también difieren).

---

## 13. Dependencias

### 13.1 Prerrequisitos de implementación

- **Fase 1 F-002 completada:** el value object `Money` y el
  enum `Currency` existen en
  `src/ControlDeFinanzasIA.Domain/ValueObjects/Money.cs` y
  `src/ControlDeFinanzasIA.Domain/Enums/Currency.cs`. `Money`
  expone `Create(decimal, Currency)`, `Zero(Currency)`,
  `Add`, `Subtract`, `Multiply`, `ToString`. `Expense`
  consume `Money` como tipo de `Amount`.
- **Fase 1 F-006 completada:** las 15 interfaces de
  repositorio, incluyendo `IExpenseRepository` que referencia
  `Expense` del namespace `ControlDeFinanzasIA.Domain.
  Transactions`. F-001 no cambia el namespace, así que
  `IExpenseRepository` sigue compilando sin modificaciones.
- **Fase 1 F-007 completada:** los tests NetArchTest R-1 a R-5
  están en `tests/.../Architecture/`. F-001 no debe romperlos.
- **Fase 1 F-008 completada:** `IClock` y `SystemClock`
  implementados y registrados en `Bootstrap`. `Expense` no
  referencia `IClock` directamente (R-1: `Domain` no referencia
  `Application`), pero el use case F-004 lo usará para pasar
  `createdAt` a `Expense.Create`.
- **Fase 2 completada:** `AppDbContext`, migración
  `InitialSchema`, `SeedData`, repositorios de `Category` y
  `PaymentMethod`. F-001 no los toca, pero el código existente
  (`Category.cs`, `PaymentMethod.cs`, `Money.cs`,
  `DateRange.cs`) establece las convenciones que `Expense`
  sigue.

### 13.2 Dependencias con otras features (Fase 3)

| Feature | Relación | Notas |
| --- | --- | --- |
| F-002 (Fase 3) | Consumidora | Mapea `Expense` a SQLite; decide `OwnsOne` vs `ValueConverter` para `Money` (input #4 del orchestrator). Si la forma de `Expense` (propiedades con `private set`, constructor privado parameterless) dificulta el mapeo, F-002 lo surfacea (ver §17.5). |
| F-003 (Fase 3) | Consumidora | Implementa `ExpenseRepository` que persiste y recupera `Expense` vía `IExpenseRepository` (Fase 1 F-006). Recupera instancias vía el constructor parameterless privado + reflection. |
| F-004 (Fase 3) | Consumidora | `RegisterExpenseUseCase` construye `Expense` vía `Create(...)`, pasando `createdAt` desde `IClock.UtcNow`. `RegisterExpenseValidator` valida el comando antes de construir; las invariantes de `Expense` son la red de seguridad tras el validador. |
| F-005 (Fase 3) | Consumidora | `GetTransactionsQuery` mapea `Expense` a `ExpenseDto` (en F-004) para listar. Lee `expense.Amount.Amount`, `expense.Amount.Currency`, `expense.Date`, etc. |
| F-006 (Fase 3) | Independiente | UI shell no toca `Expense`. |
| F-007 (Fase 3) | Consumidora (vía DTO) | `ExpensesViewModel` consume `ExpenseDto` (F-004), no la entidad `Expense` directamente. La entidad no se filtra a la UI. |

### 13.3 Bloqueos

- **Ninguno.** F-001 puede implementarse inmediatamente. Todos
  los prerrequisitos (Fase 1 + Fase 2) están cumplidos.

---

## 14. Fuera de alcance (detallado)

Repetición con énfasis para evitar ambigüedad. Estos puntos **NO**
se implementan en F-001:

- **Persistencia de `Expense`** (configuración EF Core, migración
  `AddExpenseTable`, `OwnsOne` vs `ValueConverter` para
  `Money`). Es F-002 Fase 3.
- **Repositorio concreto `ExpenseRepository`** y extensión de
  `IExpenseRepository` con `GetByFilterAsync`. Es F-003 Fase 3.
- **Use cases** (`RegisterExpenseUseCase`,
  `RegisterExpenseValidator`, `GetTransactionsQuery`, DTOs,
  `ExpenseExtensions.ToDto`). Son F-004 y F-005 Fase 3.
- **UI** (`ExpensesViewModel`, `ExpensesView`, navegación,
  DataTemplates, `DatePicker`, `ComboBox`). Son F-006 y F-007
  Fase 3.
- **Base abstracta `Transaction`.** Diferida a Phase 4 (decisión
  #2 del orchestrator; ver §17.1). F-001 elimina el placeholder
  existente.
- **Propiedad `Currency` separada.** La moneda se accede vía
  `expense.Amount.Currency` (decisión #4 del orchestrator).
- **Propiedad `PaymentMethodId`.** Llega en Fase 5 (multi-moneda
  + método de pago).
- **Propiedad `TagIds`.** Llega en Fase 4 (tags).
- **Propiedad `SourceRuleId?`.** Llega en Fase 6 (recurrentes).
- **Soft-delete** (`IsDeleted`). Rechazado para v1 (ver §17.7).
- **Audit fields** (`CreatedBy`, `UpdatedBy`, `Version`). No
  aplican a single-user single-process (ver §17.8).
- **Validación de que `CategoryId` exista en `Categories`.** Es
  cross-aggregate; corresponde a `ExpenseRepository.AddAsync`
  (F-003) y al `RegisterExpenseValidator` (F-004). La entidad
  valida sólo `!= Guid.Empty` (ver §17.6).
- **Conversión de moneda** (DOP ↔ USD). No es dominio; ocurre
  en presentación (Fase 5).
- **Localización de mensajes de excepción.** En inglés en
  código (F-002 Fase 1 §RNF-5); i18n en Fase 12.
- **Igualdad por `Id`** (override `Equals(Expense other)`).
  Diferida a Phase 4 si surge necesidad (ver §17.9).
- **Overload `CreateWithId(Guid id, ...)` para tests
  deterministas.** No necesario (ver §17.10).
- **Eventos de dominio** (`IDomainEvent` despachados tras
  `Create` / `Update`). Sin event sourcing en v1
  (`docs/architecture.md` §6 "Interfaces de dominio": "En v1
  los use cases leen la entidad recargada tras
  `SaveChangesAsync`; no se requiere event sourcing").

---

## 15. Criterios de aceptación

La feature se considera completada cuando **todos** los
siguientes puntos se cumplen, verificables mediante tests
automatizados y revisión de código.

### AC-1 — Archivos de dominio

- [ ] `src/ControlDeFinanzasIA.Domain/Transactions/Expense.cs`
      existe como `public sealed class Expense` (no `record`,
      no hereda de `Transaction` ni de ninguna base).
- [ ] `src/ControlDeFinanzasIA.Domain/Transactions/Transaction.cs`
      **no existe** (eliminado; ver §17.1).
- [ ] `src/ControlDeFinanzasIA.Domain/Transactions/Income.cs`
      existe como `public sealed class Income` standalone vacío
      (sin herencia; placeholder mínimo para que
      `IIncomeRepository` compile).
- [ ] El namespace de `Expense.cs` y `Income.cs` es
      `ControlDeFinanzasIA.Domain.Transactions` (sin cambios
      vs. el placeholder; `IExpenseRepository` / `IIncomeRepository`
      de Fase 1 F-006 siguen compilando).

### AC-2 — API pública de `Expense`

- [ ] `Expense` expone ocho propiedades públicas con `private
      set`: `Id` (`Guid`), `CategoryId` (`Guid`), `Date`
      (`DateOnly`), `Amount` (`Money`), `Description`
      (`string`), `Note` (`string?`), `CreatedAt` (`DateTime`),
      `UpdatedAt` (`DateTime`).
- [ ] **No** hay propiedad `Currency` separada; la moneda se
      accede vía `expense.Amount.Currency`.
- [ ] **No** hay propiedades `PaymentMethodId`, `TagIds`,
      `SourceRuleId`.
- [ ] `public static Expense Create(Guid categoryId, DateOnly
      date, Money amount, string description, string? note,
      DateTime createdAt)` existe y es el único camino público
      para construir un `Expense`.
- [ ] `public void Update(Guid categoryId, DateOnly date, Money
      amount, string description, string? note, DateTime
      updatedAt)` existe y reasigna las propiedades listadas
      (no `Id`, no `CreatedAt`).
- [ ] `private Expense()` y `private Expense(Guid, Guid,
      DateOnly, Money, string, string?, DateTime, DateTime)`
      existen; no hay constructor público.

### AC-3 — Tests unitarios

- [ ] `tests/ControlDeFinanzasIA.Tests.Unit/Domain/ExpenseTests.cs`
      existe con al menos 14 tests cubriendo: happy path
      `Create` (T-1), cada invariante (T-2..T-11), `Update`
      happy path + inmutabilidad de `Id` / `CreatedAt` (T-12),
      re-validación en `Update` (T-13), INV-8 (T-14), igualdad
      por referencia (T-15, T-16).
- [ ] Todos los tests pasan en `Tests.Unit`.
- [ ] Los tests usan xUnit + FluentAssertions (convención de
      Fase 1 §15); `[Fact]` y `[Theory]` + `[InlineData]` según
      corresponda.

### AC-4 — Compilación y tests existentes

- [ ] `dotnet build -c Release` desde la raíz: 0 warnings, 0
      errors.
- [ ] `dotnet build -c Debug` desde la raíz: 0 warnings, 0
      errors.
- [ ] `dotnet test` desde la raíz: 131 tests existentes
      (heredados de Fase 1 + Fase 2) + 14 nuevos de `Expense`
      ≈ 145 verdes, 0 failures.

### AC-5 — NetArchTest R-1 a R-5 siguen pasando

- [ ] El test R-1 de F-007 Fase 1
      (`R1_Domain_No_Depende_De_Application_Infra_UI_o_Tests`)
      sigue pasando: `Domain` no referencia a `Application`,
      `Infrastructure`, `UI`, ni `Tests.*`, y no depende de
      `Microsoft.EntityFrameworkCore.*`, `Avalonia.*`, ni
      `System.Net.Http`.
- [ ] `dotnet list src/ControlDeFinanzasIA.Domain package`
      sigue vacío (cero `PackageReference`s).
- [ ] Los tests R-2, R-3, R-4, R-5 siguen pasando sin
      modificaciones (F-001 no toca `Application`,
      `Infrastructure`, ni `UI`).

### AC-6 — Pureza de dominio

- [ ] `Expense.cs` sólo importa `System` (y `System.Globalization`
      si se necesita para trim / normalización) y del propio
      dominio (`Money` de `ValueObjects/`, `Currency` de
      `Enums/`). No importa `Application`, `Infrastructure`,
      `UI`, ni EF Core / Avalonia / HttpClient.
- [ ] Verificable por inspección del archivo y por NetArchTest
      R-1.

### AC-7 — Invariantes enforceable sin dependencias

- [ ] Cada una de las 8 invariantes (INV-1 a INV-8) se valida
      dentro de `Create` o `Update` sin acceder a I/O, reloj,
      base de datos, ni servicios externos.
- [ ] Cada invariante tiene al menos un test que la ejercita
      con un input inválido y verifica que se lanza la
      excepción correcta (`ArgumentNullException` para INV-1,
      `ArgumentException` para las demás).

### AC-8 — Convenciones del repo

- [ ] File-scoped namespace en `Expense.cs`, `Income.cs`, y
      `ExpenseTests.cs`.
- [ ] `using` directives al tope (nunca después del namespace).
- [ ] XML doc en la clase `Expense`, cada propiedad pública,
      el factory `Create`, el método `Update`, y los
      constructores privados.
- [ ] `sealed` en `Expense` y `Income`.
- [ ] `dotnet format style --verify-no-changes`: exit 0.

### AC-9 — Trazabilidad

- [ ] Cada invariante INV-1..INV-8 de §8 referencia
      explícitamente la fuente de verdad (visión §X,
      arquitectura §X, decisión #N del orchestrator) en su
      campo "Trazabilidad".
- [ ] La tabla de trazabilidad de §19 mapea cada invariante y
      cada AC a su sección fuente.

### AC-10 — Documentación y renombrado

- [ ] `spec/phases/phase-03/features.md` actualizado: F-001
      marcada como COMPLETADA en la tabla resumen y en el
      header de la sección F-001 (al cerrar la fase, no antes
      del apply + verify).
- [ ] La spec se renombra a
      `spec/features/phase-03/F-001-expense-domain-entity.done.md`
      tras apply + verify.

---

## 16. Estructura de archivos esperada

| Archivo | Acción | Contenido |
| --- | --- | --- |
| `src/ControlDeFinanzasIA.Domain/Transactions/Expense.cs` | Reescribir | `public sealed class Expense` con factory `Create`, `Update`, dos constructores privados, propiedades con `private set`, XML doc exhaustivo. |
| `src/ControlDeFinanzasIA.Domain/Transactions/Transaction.cs` | Eliminar | N/A (placeholder abstracto eliminado). |
| `src/ControlDeFinanzasIA.Domain/Transactions/Income.cs` | Reescribir | `public sealed class Income` standalone vacío (placeholder para Phase 4). |
| `tests/ControlDeFinanzasIA.Tests.Unit/Domain/ExpenseTests.cs` | Crear | 14 tests xUnit + FluentAssertions cubriendo happy path, invariantes, `Update`, igualdad, inmutabilidad. |

**Modificaciones a otros proyectos:** ninguna. `Application`,
`Infrastructure`, `UI`, `Tests.Integration`,
`Directory.Packages.props` no se modifican.

---

## 17. Decisiones de diseño explícitas y decisiones pendientes

### 17.1 — `Expense` como `sealed class` sin base `Transaction` (decisión #2 del orchestrator)

**Decisión:** `public sealed class Expense` standalone, sin
heredar de `Transaction` ni de ninguna base abstracta.

**Justificación:** decisión #2 del orchestrator: "Expense se
introduce como clase sellada (sealed class) sin base
Transaction abstracta. Phase 4 (cuando llegue Income) hará un
refactor trivial para extraer la base. NO especular con la base
ahora." Especular la base ahora (con un solo subtipo `Expense`)
viola YAGNI y fuerza a diseñar la jerarquía sin conocer las
necesidades reales de `Income` (Phase 4).

**Consecuencia sobre el codebase existente:** la Fase 1 F-006
introdujo `Transaction.cs` como `abstract record` placeholder
con `Date: DateTime`, propiedades separadas `Amount: Money` y
`Currency: Currency`, `PaymentMethodId`, `TagIds`,
`SourceRuleId?`; y `Expense.cs` / `Income.cs` como subtipos
vacíos. Esa estructura especula la jerarquía Y los campos de
fases posteriores. F-001 la deshace:

1. **Elimina `Transaction.cs`.**
2. **Reescribe `Expense.cs`** como `sealed class` standalone con
   los campos de Fase 3 (`Date: DateOnly`, `Amount: Money` sin
   `Currency` separada, sin `PaymentMethodId` / `TagIds` /
   `SourceRuleId`).
3. **Reescribe `Income.cs`** como `sealed class` standalone
   vacío (placeholder mínimo; Phase 4 lo completa).

**Alternativa descartada:** mantener `Transaction.cs` como
abstract muerto (sin herederos tras reescribir `Expense` y
`Income` como standalone). Feo, dead code, confunde lectores.
Mejor eliminar y dejar que Phase 4 lo reintroduzca con
conocimiento real de ambos subtipos.

### 17.2 — `Income.cs` reescrito como standalone vacío

**Decisión:** `public sealed class Income { }` (o con un
comentario XML doc que diga "Placeholder para Phase 4. Phase 4
lo completa con invariantes análogas a `Expense` y extrae la
base `Transaction`.").

**Justificación:** `IIncomeRepository` (Fase 1 F-006) referencia
el tipo `Income` del namespace `ControlDeFinanzasIA.Domain.
Transactions`. Si se elimina `Income.cs`, `Application` no
compila. Si se deja heredando de `Transaction` (eliminado),
tampoco compila. La opción limpia es un `sealed class` vacío
standalone que sólo existe para que `IIncomeRepository`
resuelva el tipo. Phase 4 lo completa.

### 17.3 — Sanity floor `1900-01-01` para `Date` (decisión #1 del orchestrator)

**Decisión:** `Date >= 1900-01-01` (invariante INV-3). Fechas
futuras permitidas (gasto planeado).

**Justificación:** decisión #1 del orchestrator: "Expense.Date
admite fechas futuras (gasto planeado está permitido). La
invariante sólo rechaza Date < 1900-01-01 como sanity floor."
`vision.md` §4.1 no prohíbe gastos planificados; el floor
existe para rechazar errores obvios de entrada (año 1, año
1899).

**Decisión pendiente (DP-1):** el usuario puede refinar el
floor antes del apply. Default propuesto: `1900-01-01`.
Alternativas: `2000-01-01` (más estricto, asume que el usuario
no tiene gastos pre-2000), `DateOnly.MinValue` (sin floor,
cualquier fecha válida aceptada — menos defensivo). Si el
usuario no responde, se aplica el default `1900-01-01`.

### 17.4 — Sin `PaymentMethodId`, `TagIds`, `SourceRuleId`, sin `Currency` separada

**Decisión:** F-001 no introduce estas propiedades. La moneda
se accede vía `expense.Amount.Currency`.

**Justificación:**

- `PaymentMethodId`: fase 5 (método de pago + multi-moneda).
- `TagIds`: fase 4 (tags).
- `SourceRuleId?`: fase 6 (recurrentes).
- `Currency` separada: decisión #4 del orchestrator (el mapeo
  de `Money` es de F-002; F-001 fija la forma del dominio).
  Duplicar `Currency` rompería la invariante "la moneda del
  gasto es la moneda del monto".

### 17.5 — Forma de la entidad y mapeo EF Core (input #4 del orchestrator)

**Decisión:** `Expense` expone `Amount: Money` como propiedad
de tipo value object. Las propiedades tienen `private set`; hay
un constructor parameterless privado para EF Core.

**Riesgo para F-002 (surfaceado, no resuelto):** el mapeo
EF Core de `Money` ( OwnsOne vs ValueConverter ) es decisión de
F-002 (input #4 del orchestrator). F-001 sólo fija la forma. Si
F-002 elige `OwnsOne(e => e.Amount, money => { ... })`, EF
Core necesita acceder a las propiedades de `Money` (`Amount`,
`Currency`) — que en `Money.cs` son `{ get; }` (readonly, no
`private set`). EF Core 8 puede hidratar propiedades readonly
vía el constructor parametrizado de `Money` (si es `private`,
EF Core puede usarlo si está configurado), pero esto es
concern de F-002. Si F-002 encuentra que la forma de `Expense`
(con `private set` y constructor privado) o de `Money` (con
`{ get; }` y constructor privado) dificulta el mapeo, F-002 lo
surfacea y F-001 + F-002 coordinan el ajuste (p. ej., cambiar
`private set` a `init`, o exponer el constructor de `Money`
como `internal` para que EF Core lo use).

**Decisión pendiente (DP-2):** si el reviewer prefiere `init`
sobre `private set` para las propiedades de `Expense` (que
permite `with` expressions pero rompe la inmutabilidad
post-`Update`), se debate antes del apply. Default: `private
set` (consistente con la semántica de entidad mutable sólo vía
`Update`).

### 17.6 — Validación cross-aggregate de `CategoryId` diferida a F-003 / F-004

**Decisión:** `Expense` valida sólo `CategoryId != Guid.Empty`
(sintaxis, INV-6). La validación de que `CategoryId` referencie
una `Category` existente es de `ExpenseRepository.AddAsync`
(F-003) y del `RegisterExpenseValidator` (F-004).

**Justificación:** `Expense` y `Category` son agregados
separados. La entidad `Expense` no conoce la entidad `Category`
ni su repositorio; sólo conoce el `Guid`. Validar la existencia
del `Category` requeriría inyectar `ICategoryRepository` en la
entidad, violando pureza de dominio (R-1) y el principio "no
references between aggregates". La validación cross-aggregate
es responsabilidad del use case (con el validador
FluentValidation) y del repositorio (con `FindAsync` sobre
`Categories` antes de `SaveChanges`). El phase plan F-003 lo
confirma: "AddAsync: chequea que CategoryId exista en
Categories vía _db.Categories.FindAsync; si no, devuelve
Result.Failure("CategoryNotFound", "...")."

### 17.7 — Sin soft-delete para v1

**Decisión:** F-001 no introduce `IsDeleted`, query filters, ni
comportamiento de "undo delete". `RemoveAsync` (F-003) hace
hard delete.

**Justificación:** `vision.md` no menciona recuperación de
gastos borrados ni papelera. La app es single-user local; el
backup manual (Fase 19) es la red de seguridad para datos
perdidos. Añadir soft-delete ahora especula un requisito que
no existe y complica queries (cada `GetAllAsync` necesitaría
`IsDeleted == false`). Phase 4+ puede revisitar si el usuario
pide "deshacer borrado".

### 17.8 — Sin audit fields más allá de `CreatedAt` / `UpdatedAt`

**Decisión:** F-001 no introduce `CreatedBy`, `UpdatedBy`,
`Version` / `RowVersion`.

**Justificación:**

- `CreatedBy` / `UpdatedBy`: no hay identidad de usuario
  (`vision.md` §2: "Cada instalación es un único usuario").
- `Version` / `RowVersion`: no hay concurrencia
  (`docs/architecture.md` §12: "Single-user, un solo proceso a
  la vez"). ADR-013 ("Multi-proceso / lock file") está
  pendiente pero no necesario en v1.

### 17.9 — Igualdad por referencia, no por valor ni por `Id`

**Decisión:** `Expense` es `sealed class` con igualdad por
referencia (default de `object`). No se sobrescriben `Equals`
ni `GetHashCode`.

**Justificación:** `Expense` es una entidad con identidad
(`Id`); su igualdad natural es la referencia. Diferente de
`Money` / `DateRange` (value objects con igualdad por valor vía
`record`). Igualdad por `Id` (override `Equals(Expense other)
=> Id == other.Id`) se difiere a Phase 4 si una colección
necesita `Contains` basado en `Id`; para v1, los repositorios
devuelven la instancia trackeada y los tests verifican
propiedades, no igualdad.

**Decisión pendiente (DP-3):** si el reviewer prefiere
igualdad por `Id` desde F-001 (para preparar `Contains` en
colecciones de F-007), se debate antes del apply. Default:
igualdad por referencia (mínimo superficie, sin override).

### 17.10 — `Id` generado internamente en `Create`

**Decisión:** `Create` genera `Id = Guid.NewGuid()` internamente.
No hay overload `CreateWithId(Guid id, ...)`.

**Justificación:** la decisión de F-002 Fase 3
(`b.Property(e => e.Id).ValueGeneratedNever()`) fija que el
`Guid` lo provee el dominio, no la base de datos. Generarlo en
`Create` reduce la superficie de error (el caller no puede
olvidar generar el `Guid` ni generar duplicados). Para tests
deterministas, no se necesita un `Id` conocido: los tests
verifican `expense.Id != Guid.Empty` y, cuando necesitan el
`Id` para una operación subsiguiente, lo leen de la instancia
creada.

**Alternativa descartada:** `Create(Guid id, ...)` con `Id`
por parámetro. Permite tests deterministas pero abre la puerta
a `Id`s duplicados (dos `Create` con el mismo `Guid`). No hay
necesidad real; la lectura del `Id` post-`Create` cubre todos
los casos de uso de test.

### 17.11 — `CreatedAt` / `UpdatedAt` por parámetro, no desde `IClock`

**Decisión:** `Create` y `Update` reciben `createdAt` /
`updatedAt` como parámetros. La entidad no referencia `IClock`.

**Justificación:** `IClock` vive en
`Application/Abstractions/` (Fase 1 F-004). `Domain` no puede
referenciar `Application` (R-1 de F-007 Fase 1). La entidad es
pura y no puede resolver la hora por sí misma. El caller (use
case F-004 / Phase 4) obtiene el timestamp de `IClock.UtcNow` y
lo pasa. ADR-007 (`docs/architecture.md` §19) fija el
principio; la entidad es "dependiente de tiempo" sólo en el
sentido de que registra timestamps, y la dependencia se
resuelve en el use case.

### 17.12 — Ubicación en `Transactions/`, no en `Entities/`

**Decisión:** `src/ControlDeFinanzasIA.Domain/Transactions/Expense.cs`
(no `Entities/Expense.cs` como dice el phase plan).

**Justificación:** el codebase existente (verificado vía `glob`)
usa la convención por agregado: `Transactions/`, `Catalog/`,
`Debts/`, `Budgets/`, `Goals/`, `Recurring/`, `Alerts/`,
`Settings/`, `Views/`, `Enums/`, `ValueObjects/`. No hay
carpeta `Entities/`. `architecture.md` §5 menciona `Entities/`
pero es aspiracional; el código real usa agregados. Seguir la
convención del codebase minimiza el churn y mantiene
consistencia. Mover todas las entidades a `Entities/` sería
una refactor separada (fuera de scope de F-001).

### 17.13 — Longitudes máximas `Description` 200, `Note` 500

**Decisión:** INV-4 fija `Description` ≤ 200; INV-5 fija
`Note` ≤ 500.

**Justificación:** el phase plan F-001 sugiere estos valores
("Description no vacío ni sólo espacios; hasMaxLength(200)";
"Note, si presente, hasMaxLength(500)"). La decisión de
persistencia (F-002 `HasMaxLength(200)` / `HasMaxLength(500)`)
es un eco de la invariante de dominio, no una decisión
independiente. F-001 adopta las longitudes como invariantes de
dominio para que la entidad sea testeable sin DB.

**Decisión pendiente (DP-4):** el usuario puede ajustar las
longitudes antes del apply. Defaults: 200 / 500. Alternativas:
280 / 1000 (más holgado), 140 / 280 (estilo Twitter). Si el
usuario no responde, se aplican los defaults.

### 17.14 — Normalización de `Description` (trim) y `Note` (trim + null canonization)

**Decisión:** `Create` y `Update` trim `Description` antes de
validar y almacenar. `Note` se trim y si vacío/whitespace se
normaliza a `null`.

**Justificación:** canonización de almacenamiento. Evita la
ambigüedad `null` vs `""` vs `"   "` en persistencia (F-002) y
en DTOs (F-004). Dos `Expense` con `Note = null` y `Note = ""`
son semánticamente idénticos ("no hay nota") y deben
almacenarse igual.

---

## 18. Riesgos y mitigaciones

| Riesgo | Probabilidad | Impacto | Mitigación |
| --- | --- | --- | --- |
| Eliminar `Transaction.cs` rompe compilación de `Income.cs` (heredero). | Alta (sin mitigación) | Alto | F-001 reescribe `Income.cs` como `sealed class` standalone vacío antes / junto con eliminar `Transaction.cs`. AC-1 verifica. |
| `IIncomeRepository` (Fase 1 F-006) referencia `Income`; si se elimina `Income.cs`, `Application` no compila. | Alta (sin mitigación) | Alto | F-001 no elimina `Income.cs`; lo reescribe como standalone vacío. AC-1 verifica. |
| `IExpenseRepository` (Fase 1 F-006) referencia `Expense` del namespace `Domain.Transactions`; si F-001 cambia el namespace, `Application` no compila. | Media | Alto | F-001 mantiene el namespace `ControlDeFinanzasIA.Domain.Transactions` (sin cambios). AC-1 verifica. |
| La forma `sealed class` con `private set` y constructor privado dificulta el mapeo EF Core en F-002 (`OwnsOne` de `Money`). | Media | Medio | Surfaceado en §17.5. F-001 fija la forma; F-002 decide el mapeo y coordina con F-001 si hace falta ajustar (`private set` → `init`, o exponer constructor de `Money` como `internal`). |
| `Money` sólo valida `>= 0`; `Expense` añade `> 0` (INV-2). Un caller que construye `Money.Create(0, DOP)` y lo pasa a `Expense.Create` obtiene `ArgumentException`, no `Money` inválido. Confusión. | Baja | Bajo | Documentado en INV-2: `Money` cero es válido para agregaciones; `Expense` rechaza cero porque un gasto de monto cero no es gasto (decisión #3 del orchestrator). El test T-3 cubre el caso. |
| `Date` con `DateOnly` no se mapea nativamente en EF Core 8 a SQLite. | Baja | Medio | EF Core 8 mapea `DateOnly` nativamente a `TEXT` ISO-8601 (verificar vía Context7 en F-002). Si surgen edge cases, F-002 introduce un `ValueConverter<DateOnly, string>`. F-001 no se preocupa por persistencia. |
| `CreatedAt` con `DateTimeKind.Local` se acepta (INV-7 no enforce runtime). | Baja | Bajo | El use case F-004 obtiene `createdAt` de `IClock.UtcNow` (garantía UTC). El test F-004 verifica que se usa `IClock.UtcNow`, no `DateTime.UtcNow`. La entidad es pura y no valida el `Kind` (defensive del caller). |
| `Expense` como `sealed class` (no `record`) no soporta `with` expressions. Phase 4 podría querer "copia modificada". | Baja | Bajo | `Update` muta in-place. Si Phase 4 necesita "copia modificada", se introduce un método `With...` o se cambia a `record` en ese momento. F-001 no especula. |
| Igualdad por referencia confunde a quien espere igualdad por valor (estilo `Money`). | Media | Bajo | Documentado en §9.6 y §17.9. `Expense` es entidad, no value object. El test T-15 / T-16 cubre la semántica. |
| Los 14 tests no cubren un edge case que aparece en F-003 (integración con SQLite). | Baja | Bajo | F-003 añade tests de integración. F-001 cubre unit tests del dominio; los de integración son responsabilidad de F-003. |
| `Description` trim pierde información intencional (p. ej., "  pizza  " podría ser intencional). | Baja | Bajo | El trim es coherente con la canonización de `Note` y evita "  pizza  " != "pizza" en búsquedas. Si el usuario quiere preservar espacios internos, el trim sólo afecta leading/trailing, no internos. |

---

## 19. Trazabilidad

Tabla que mapea cada invariante y AC a la sección de la fuente
de verdad que la justifica.

| Ítem | Tipo | Fuente de verdad | Sección / referencia |
| --- | --- | --- | --- |
| INV-1 | Invariante (`Amount` no null) | Defensiva de diseño | F-002 Fase 1 §RF-2 (patrón factory + guards) |
| INV-2 | Invariante (`Amount.Amount > 0`) | Decisión #3 orchestrator + visión | `vision.md` §4.1; decisión #3 orchestrator |
| INV-3 | Invariante (`Date >= 1900-01-01`) | Decisión #1 orchestrator | Decisión #1 orchestrator; `vision.md` §4.1 (gastos planificados no prohibidos) |
| INV-4 | Invariante (`Description` ≤ 200, no vacío) | Visión + phase plan | `vision.md` §4.2; phase plan F-001 |
| INV-5 | Invariante (`Note` ≤ 500 si no null) | Visión + phase plan | `vision.md` §4.2 ("Nota opcional"); phase plan F-001 |
| INV-6 | Invariante (`CategoryId != Guid.Empty`) | Visión + phase plan | `vision.md` §4.2 ("Categoría" obligatoria); phase plan F-001 |
| INV-7 | Invariante (`CreatedAt` / `UpdatedAt` UTC) | ADR-007 | `docs/architecture.md` §19 (ADR-007); Fase 1 F-004 spec §RNF-1 |
| INV-8 | Invariante (`UpdatedAt >= CreatedAt` en `Update`) | Auditoría | `docs/architecture.md` §1.7 (determinismo testeable) + sentido común de auditoría |
| RF-1 | Tipo `sealed class Expense` sin base | Decisión #2 orchestrator | Decisión #2 orchestrator |
| RF-2 | Sin `Currency` separada | Decisión #4 orchestrator | Decisión #4 orchestrator |
| RF-2 | Sin `PaymentMethodId` / `TagIds` / `SourceRuleId` | Phase plan + roadmap | Phase plan F-001; `docs/roadmap.md` §Fase 4 (tags), §Fase 5 (método de pago), §Fase 6 (recurrentes) |
| RF-3 | `Id` generado en `Create` | F-002 Fase 3 | Phase plan F-002 Fase 3 (`b.Property(e => e.Id).ValueGeneratedNever()`) |
| RF-3 | `createdAt` por parámetro (no `IClock` en entidad) | R-1 + ADR-007 | `docs/architecture.md` §4 (R-1), §19 (ADR-007); Fase 1 F-004 (`IClock` en `Application/Abstractions/`) |
| RF-6 | Igualdad por referencia (no valor) | Convención entidad vs value object | `docs/architecture.md` §6 (entidades vs value objects); F-002 Fase 1 (Money value object, igualdad por valor); F-003 Fase 1 (DateRange value object, igualdad por valor) |
| RNF-1 | Pureza de dominio (R-1) | F-007 Fase 1 | `docs/architecture.md` §4; `spec/features/phase-01/F-007-validacion-arquitectonica.done.md` §5 R-1 |
| RNF-2 | NetArchTest R-2 a R-5 | F-007 Fase 1 | `spec/features/phase-01/F-007-validacion-arquitectonica.done.md` §5 R-2..R-5 |
| AC-1 | Eliminar `Transaction.cs`, reescribir `Expense.cs` / `Income.cs` | Decisión #2 orchestrator | Decisión #2 orchestrator |
| AC-7 | Invariantes enforceable sin dependencias | `docs/architecture.md` §1.2 (dominio puro) | `docs/architecture.md` §1.2; `spec/constitution/mission.md` ("Evitar lógica en code behind / mantener lógica en capas adecuadas") |
| AC-10 | Renombrado a `.done.md` tras apply + verify | Convención del repo | `spec/README.md` (convención `.spec.md` → `.done.md`); F-001 Fase 2 §AC-10 (patrón) |
| §17.5 | Surface de riesgo para F-002 (mapeo `Money`) | Decisión #4 orchestrator | Decisión #4 orchestrator (F-001 fija la forma, F-002 el mapeo) |
| §17.12 | Ubicación `Transactions/` no `Entities/` | Convención del codebase | Glob de `src/ControlDeFinanzasIA.Domain/**/*.cs` confirma estructura por agregado; `architecture.md` §5 menciona `Entities/` pero es aspiracional |

---

## 20. Referencias

- `docs/vision.md` §4.1 (Gastos del día a día), §4.2
  (Organización de un movimiento — subconjunto mínimo: fecha,
  monto+moneda, categoría, nota opcional), §2 (single-user).
- `docs/architecture.md` §1 (Principios arquitectónicos:
  dominio puro, determinismo testeable), §3 (solución y
  proyectos), §4 (grafo de dependencias, reglas R-1 a R-5),
  §5 (Convenciones de código y carpetas), §6 (Capa de Dominio:
  entidades, value objects, `Transaction` abstracta —
  referenciada como futuro refactor de Phase 4), §19 (ADRs,
  ADR-007 `IClock`).
- `docs/roadmap.md` §Fase 3 (Entregables esperados: `Expense`
  entidad en `Domain` con `CategoryId`, `Date`, `Amount`,
  `Description`, `Note?`), §Fase 4 (tags), §Fase 5 (método de
  pago + multi-moneda), §Fase 6 (recurrentes).
- `spec/constitution/mission.md` — Clean Architecture, "Evitar
  duplicación de lógica", "Clases pequeñas y con una única
  responsabilidad".
- `spec/constitution/tech-stack.md` — Convenciones de código
  (MVVM estricto, async/await extremo a extremo, errores como
  valores, cero telemetría, sin secrets).
- `spec/phases/phase-03/features.md` — F-001 dentro del
  desglose de la fase 3 (objetivo, descripción breve,
  invariantes, justificación, dependencias, criterios de
  cierre).
- `spec/features/phase-01/F-001-skeleton-solution.done.md` —
  Convenciones de `.editorconfig`, file-scoped namespaces,
  CPM, estructura de proyectos.
- `spec/features/phase-01/F-002-money-value-object.done.md` —
  Value object `Money` que `Expense` consume como `Amount`;
  patrón factory + private constructor + readonly properties;
  convención de mensajes de excepción en inglés (RNF-5).
- `spec/features/phase-01/F-003-datarange-value-object.done.md`
  — Value object `DateRange` con `DateOnly`; patrón de
  invariante validada en `Create` y lanzamiento de
  `ArgumentException`.
- `spec/features/phase-01/F-005-comuntypes-result-pagedresult.done.md`
  — `Result` y `PagedResult<T>` que el use case F-004 y la
  query F-005 consumirán (no F-001 directamente).
- `spec/features/phase-01/F-006-contrato-interfaces-repo.done.md`
  — `IExpenseRepository` y `IIncomeRepository` que referencian
  los tipos `Expense` / `Income` del namespace
  `Domain.Transactions`; F-001 mantiene el namespace para no
  romper las interfaces.
- `spec/features/phase-01/F-007-validacion-arquitectonica.done.md`
  — Tests NetArchTest R-1 a R-5 que F-001 no debe romper.
- `spec/features/phase-01/F-008-composition-root.done.md` —
  `Bootstrap.ConfigureServices` y `SystemClock` (implementación
  de `IClock`) que el use case F-004 usará para pasar
  `createdAt` a `Expense.Create`.
- `spec/features/phase-02/F-001-appdbcontext-addpersistence.done.md`
  — Formato de spec de referencia (estructura de secciones,
  rigor de ACs, uso de tablas de trazabilidad, convenciones de
  prosa) que esta spec espeja.
- `spec/README.md` — Convención del directorio `spec`
  (numeración local por fase, sufijos `.spec.md` /
  `.done.md`).
