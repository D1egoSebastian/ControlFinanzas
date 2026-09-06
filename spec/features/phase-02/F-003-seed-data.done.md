# Specification — F-003: Seed Data

> **Fase:** 2 — Persistencia con EF Core + SQLite
> **Feature:** F-003 (local a esta fase)
> **Estado:** ✅ COMPLETADA
> **Spec ID:** SPEC-F003P2
>
> **Nota de numeración:** este `F-003` es el de la **Fase 2**
> (`spec/phases/phase-02/features.md`). No debe confundirse con
> el `F-003` de Fase 1 (DateRange value object en
> `spec/phases/phase-01/features.md`). Los IDs son locales a cada
> fase; el folder los desambigua. Ver `spec/README.md` para la
> convención.
>
> Fuentes de verdad: `docs/vision.md`, `docs/architecture.md`,
> `docs/roadmap.md` (Fase 2), `spec/constitution/tech-stack.md`,
> `spec/constitution/mission.md`,
> `spec/phases/phase-02/features.md` (F-003 dentro del
> desglose de la fase 2),
> `spec/features/phase-02/F-001-appdbcontext-addpersistence.done.md`
> (introduce `SeedData` como stub vacío),
> `spec/features/phase-02/F-002-entity-configurations-migration.done.md`
> (generó `InitialSchema` sin seed).

---

## 1. Resumen

F-003 completa el **sembrado de datos del sistema** que
F-001 introdujo como stub vacío y F-002 omitió de la
primera migración. La feature:

1. Rellena el cuerpo de `SeedData.Apply(ModelBuilder)`
   con las 9 categorías y los 5 métodos de pago del
   sistema (todos con `IsSystem=true`, IDs
   deterministas, y `CreatedAt`/`UpdatedAt` en un
   timestamp UTC fijo).
2. Genera una **nueva migración** `SeedSystemData` que
   captura el sembrado en `InsertData` calls, de modo
   que cuando F-005 aplique las migraciones al
   arrancar la app, los datos del sistema se inserten
   en la DB.
3. Verifica la idempotencia: re-aplicar las
   migraciones no duplica los datos.

Al final de la feature:
- `SeedData.Apply` (en `Infrastructure/Persistence/SeedData.cs`)
  está implementado con `HasData` para las 9
  categorías y los 5 métodos de pago.
- La migración `SeedSystemData` (en
  `Infrastructure/Persistence/Migrations/`) está
  generada con `InsertData` para los 14 registros.
- `dotnet build` y `dotnet test` (los 111 tests
  existentes) siguen pasando en limpio.
- Los 11 tests de NetArchTest de F-007 (R-1 a R-5)
  siguen pasando.

---

## 2. Contexto

F-001 de Fase 2 introdujo `SeedData` como **stub
vacío** (cuerpo con un comentario; ver F-001 §AC-8
y §17.5) para que `AppDbContext.OnModelCreating`
pudiera compilar sin un símbolo faltante. La
intención era que F-003 rellenara el cuerpo.

F-002 generó la primera migración `InitialSchema`
que crea las tablas y los índices, pero **sin**
incluir `InsertData` para el seed. La razón
documentada en F-002 §17.5 y §RN-5: el stub de
`SeedData` estaba vacío en ese momento, por lo
que el snapshot del modelo no contenía `HasData`,
y EF Core no generó `InsertData` automáticamente.

F-003 cierra ese ciclo. Rellena el stub y, como
**consecuencia natural** del cambio en el modelo
(`HasData` ahora presente), genera una nueva
migración que captura el sembrado.

**Por qué `HasData` + nueva migración, no solo una
de las dos:**

- **`HasData` en `OnModelCreating`**: garantiza la
  idempotencia y mantiene el modelo "vivo" para
  regeneraciones futuras de migraciones. Si se
  añade una nueva categoría en F-003+, EF Core
  detecta la diferencia y genera la
  correspondiente migración.
- **Nueva migración `SeedSystemData`**: garantiza
  que el sembrado se aplique **en la primera
  ejecución** de la app en un equipo limpio. Sin
  la migración explícita, el `HasData` queda
  "informacional" en el snapshot pero nunca se
  inserta en la DB.
- Ambas juntas: el modelo es la fuente de verdad
  (regenerable), y la migración es la materialización
  inicial (commiteada).

**Por qué no editar la migración `InitialSchema`
existente:**

- Rompería la convención de EF Core: cada migración
  es inmutable. Editar la `Up` requeriría
  regenerar también el `Designer` y el `ModelSnapshot`,
  perdiendo el historial.
- Dificulta el desarrollo paralelo: si alguien
  clona el repo con la `InitialSchema` ya aplicada
  a su DB, y luego F-003 edita la migración, el
  developer tendría que borrar la DB manualmente.
- Con una **nueva migración** `SeedSystemData`:
  los developers con DB limpia aplican ambas
  (InitialSchema + SeedSystemData); los developers
  con DB ya inicializada (después de F-002) solo
  aplican SeedSystemData. Sin conflicto.

**Por qué IDs deterministas (no `Guid.NewGuid()`):**

- Los tests pueden predefinir los IDs esperados y
  referenciar las entidades del sistema sin
  parsear la DB.
- El seed es reproducible: ejecutar la migración
  en diferentes máquinas produce los mismos IDs.
- Los `Guid.NewGuid()` que use un usuario para
  crear entidades custom nunca colisionan con
  los IDs del sistema (los del sistema empiezan
  con `11111111-...` y `22222222-...`, prefix
  reconocible).

---

## 3. Objetivo

1. **Rellenar el cuerpo de `SeedData.Apply`** en
   `src/ControlDeFinanzasIA.Infrastructure/Persistence/SeedData.cs`
   con `HasData` para las 9 categorías y los 5
   métodos de pago del sistema.
2. **Generar la migración `SeedSystemData`** con
   `dotnet ef migrations add SeedSystemData --project src/ControlDeFinanzasIA.Infrastructure --startup-project src/ControlDeFinanzasIA.Infrastructure`,
   que captura el sembrado en `InsertData` calls.
3. **NO modificar** la migración `InitialSchema`
   existente (de F-002). Queda intacta.
4. **NO añadir** nuevos `PackageReference`,
   `<ProjectReference>`, ni dependencias de
   proyecto.
5. **NO modificar** `AppDbContext`, las
   configuraciones, `Domain`, `Application`, `UI`,
   `Tests.Unit`, ni `Tests.Integration`.

---

## 4. Alcance

### 4.1 En alcance

- Modificación de
  `src/ControlDeFinanzasIA.Infrastructure/Persistence/SeedData.cs`
  (introducido por F-001 como stub): rellenar el
  cuerpo del método `Apply` con `HasData` calls.
- Adición de 3 archivos en
  `src/ControlDeFinanzasIA.Infrastructure/Persistence/Migrations/`:
  - `YYYYMMDDhhmmss_SeedSystemData.cs`.
  - `YYYYMMDDhhmmss_SeedSystemData.Designer.cs`.
  - `AppDbContextModelSnapshot.cs` (actualizado por
    la herramienta; este archivo se sobreescribe).
- Verificación de que el build y los 111 tests
  existentes siguen pasando.
- Verificación de que el test R-1 de NetArchTest (F-007)
  sigue pasando (`Domain` sigue sin paquetes
  externos).

### 4.2 Fuera de alcance

- **Modificar la migración `InitialSchema`.** Queda
  intacta. La nueva migración es
  `SeedSystemData`, una migración aparte.
- **Modificar `AppDbContext.cs`.** Las llamadas
  `ApplyConfigurationsFromAssembly` y
  `SeedData.Apply` ya están en
  `OnModelCreating` desde F-001. F-003 solo
  rellena el cuerpo del stub.
- **Modificar las configuraciones** (`CategoryConfiguration`,
  `PaymentMethodConfiguration`). F-002 las dejó
  completas. F-003 no las toca.
- **Modificar el Domain** (entidades, propiedades).
  F-002 (con la corrección del orchestrator) ya
  dejó `Category` y `PaymentMethod` con
  `CreatedAt` y `UpdatedAt`. F-003 solo usa esas
  propiedades.
- **Crear los 15 repositorios concretos.** Es F-004.
- **Wiring del `AddPersistence` en
  `Bootstrap.ConfigureServices`.** Es F-005.
- **Aplicar migraciones al arranque.** Es F-005.
- **`<ProjectReference>` de `UI` a `Infrastructure`.**
  Es F-005.
- **Tests de integración con SQLite in-memory.** Es
  F-006.
- **Migraciones adicionales** más allá de
  `SeedSystemData`. Si en el futuro se añaden más
  categorías o métodos, se generan nuevas
  migraciones.
- **Modificaciones a `Domain`, `Application`, `UI`,
  `Tests.Unit`, `Tests.Integration`.** F-003 solo
  toca `Infrastructure`.
- **El CI workflow.** El workflow existente (`dotnet
  build` + `dotnet test`) sigue siendo suficiente.
- **Configuración de logging** para el seed. F-003
  no loguea nada al aplicar el seed; eso es
  F-005.

---

## 5. Casos de uso

### CU-1 — Sembrado inicial en una DB limpia

- **Actor:** `Database.Migrate()` llamado por F-005
  (composition root al arrancar la app) o por
  `dotnet ef database update` (manual).
- **Precondición:** la DB no existe (primera
  instalación en un equipo limpio).
- **Postcondición:** las 2 tablas y los 2 índices
  únicos se crean (vía `InitialSchema`), y los
  14 registros del sistema (9 categorías + 5
  métodos) se insertan (vía `SeedSystemData`).
- **Verificación:** `SELECT COUNT(*) FROM Categories`
  devuelve 9; `SELECT COUNT(*) FROM PaymentMethods`
  devuelve 5. Todos con `IsSystem=1`.

### CU-2 — Sembrado idempotente

- **Actor:** `Database.Migrate()` llamado múltiples
  veces (e.g., la app arranca varias veces).
- **Precondición:** el sembrado ya se aplicó en una
  ejecución anterior.
- **Postcondición:** los 14 registros siguen
  existiendo, sin duplicados. `__EFMigrationsHistory`
  tiene `InitialSchema` y `SeedSystemData` marcados
  como aplicados; las llamadas subsiguientes a
  `Migrate()` son no-op.
- **Verificación:** `SELECT COUNT(*) FROM Categories`
  sigue siendo 9 (no 18, no 27).

### CU-3 — Aplicar el seed en una DB con esquema pre-existente

- **Actor:** un developer que actualiza el repo
  después de F-002 (con `InitialSchema` ya aplicada
  en su DB local) y luego corre F-003.
- **Precondición:** la DB tiene las 2 tablas
  creadas (vía `InitialSchema` en una ejecución
  anterior).
- **Postcondición:** las 2 tablas siguen
  existiendo; los 14 registros del sistema se
  insertan (vía `SeedSystemData`).
- **Verificación:** la migración `InitialSchema` es
  no-op (ya aplicada); `SeedSystemData` aplica el
  seed.

### CU-4 — Test referencia entidades del sistema por Id

- **Actor:** un test de integración (F-006) o un
  use case (futuro, Fase 3).
- **Precondición:** el seed ya se aplicó.
- **Postcondición:** el test puede hacer
  `await ctx.Categories.FindAsync(new Guid("11111111-1111-1111-1111-000000000001"))`
  y obtener la categoría "Comida" sin parsear la
  DB.
- **Verificación:** la API de EF Core devuelve la
  entidad esperada.

### CU-5 — El usuario añade una categoría custom con nombre "Comida"

- **Actor:** un use case (futuro, Fase 3) que crea
  una categoría custom.
- **Precondición:** el seed ya está aplicado;
  existe la categoría "Comida" del sistema.
- **Postcondición:** la categoría custom
  "Comida" (con un Guid.NewGuid() diferente) NO
  colisiona con la categoría del sistema. El
  índice único en `Name` con collation `NOCASE`
  detecta el duplicado de nombre y rechaza la
  inserción (vía el repositorio, que devuelve
  `Result.Failure("DuplicateName", "...")`).
- **Verificación:** el repositorio rechaza el
  duplicado; la app no rompe.

---

## 6. Requisitos funcionales

### RF-1 — `SeedData.Apply` con 9 categorías

- `SeedData.Apply(ModelBuilder modelBuilder)` (en
  `src/ControlDeFinanzasIA.Infrastructure/Persistence/SeedData.cs`,
  que ya existe como stub desde F-001) llama a
  `modelBuilder.Entity<Category>().HasData(...)` con
  las 9 categorías del sistema. Cada categoría es
  un objeto `Category` con:
  - `Id`: Guid determinista (ver §10.1).
  - `Name`: uno de los 9 valores de §10.2.
  - `Color`: hex string de §10.4 (nullable; "Otros"
    tiene `null`).
  - `Icon`: emoji de §10.4 (nullable; "Otros" tiene
    `null`).
  - `IsSystem`: `true` para todas.
  - `CreatedAt`: `new DateTime(2026, 1, 1, 0, 0, 0,
    DateTimeKind.Utc)` para todas.
  - `UpdatedAt`: el mismo timestamp que `CreatedAt`
    para todas.

### RF-2 — `SeedData.Apply` con 5 métodos de pago

- La misma llamada `Apply` añade
  `modelBuilder.Entity<PaymentMethod>().HasData(...)`
  con los 5 métodos del sistema. Cada método es
  un objeto `PaymentMethod` con:
  - `Id`: Guid determinista (ver §10.1).
  - `Name`: uno de los 5 valores de §10.3.
  - `IsSystem`: `true` para todos.
  - `CreatedAt` y `UpdatedAt`: el mismo timestamp
    UTC fijo.
  - **NO** `Color` ni `Icon` (campos que
    `PaymentMethod` no tiene; ver F-002).

### RF-3 — Migración `SeedSystemData` generada

- Después de modificar `SeedData.Apply` y verificar
  que el build compila, ejecutar desde la raíz
  del repo:
  `dotnet ef migrations add SeedSystemData --project src/ControlDeFinanzasIA.Infrastructure --startup-project src/ControlDeFinanzasIA.Infrastructure`.
- El `--startup-project` es `Infrastructure` mismo
  (mismo que F-002; F-005 lo cambiará a `UI`).
- La herramienta detecta el diff entre el snapshot
  actual (con `HasData`) y el último snapshot
  (sin `HasData`) y genera la migración con
  `InsertData`.
- Se crean 3 archivos en
  `src/ControlDeFinanzasIA.Infrastructure/Persistence/Migrations/`:
  - `YYYYMMDDhhmmss_SeedSystemData.cs` (la
    migración; el timestamp lo asigna la
    herramienta).
  - `YYYYMMDDhhmmss_SeedSystemData.Designer.cs`
    (metadata).
  - `AppDbContextModelSnapshot.cs` (snapshot
    actualizado; sobreescrito por la herramienta).
- Los 3 archivos se commitean en el mismo commit
  atómico que el cambio a `SeedData.Apply`.

### RF-4 — Contenido de la migración

- El método `Up` de
  `YYYYMMDDhmss_SeedSystemData.cs`:
  - `migrationBuilder.InsertData(table: "Categories",
    columns: new[] { "Id", "Name", "Color", "Icon",
    "IsSystem", "CreatedAt", "UpdatedAt" },
    values: new object[] { ... })` para cada una de
    las 9 categorías (9 calls).
  - `migrationBuilder.InsertData(table: "PaymentMethods",
    columns: new[] { "Id", "Name", "IsSystem",
    "CreatedAt", "UpdatedAt" },
    values: new object[] { ... })` para cada uno de
    los 5 métodos (5 calls).
  - Total: 14 `InsertData` calls en el `Up`.
- El método `Down`:
  - Borra los 14 registros por Id:
    `migrationBuilder.DeleteData(...)` para cada
    categoría y cada método.
  - **No** borra las tablas (eso es responsabilidad
    de `InitialSchema.Down`).
- `BuildTargetModel` y demás metadata en el
  `Designer.cs` (generados por la herramienta, no
  editados a mano).

### RF-5 — `InitialSchema` intacta

- La migración `InitialSchema` (de F-002) **no se
  modifica**. Su `Up` sigue creando las 2 tablas y
  los 2 índices; su `Down` sigue borrando las 2
  tablas.
- El `AppDbContextModelSnapshot.cs` se
  sobreescribe durante la generación de
  `SeedSystemData` (la herramienta lo actualiza para
  reflejar el nuevo estado del modelo). Este
  cambio es automático y esperado.
- Los `Designer.cs` de `InitialSchema` y de
  `SeedSystemData` son inmutables una vez
  commiteados.

### RF-6 — Idempotencia

- El sembrado es idempotente:
  - Re-aplicar la migración `SeedSystemData` (en un
    equipo donde ya se aplicó) **no** duplica
    registros, porque EF Core consulta
    `__EFMigrationsHistory` y solo aplica
    migraciones pendientes.
  - Si un usuario futuro añade una categoría con
    un nombre que ya existe (vía la UI), el
    repositorio (F-004) rechaza el duplicado vía
    `Result.Failure("DuplicateName", "...")`.
- La migración **no** añade lógica de "upsert" en
  su `Up`. La idempotencia viene del framework
  (`__EFMigrationsHistory`), no del código de la
  migración.

### RF-7 — Compilación y tests

- `dotnet build` desde la raíz: 0 warnings, 0
  errors (Debug y Release).
- `dotnet test`: 111 tests verdes (100 + 11 de
  F-007), 0 failures.
- 11 tests de NetArchTest de F-007 pasan.
- `dotnet format style --verify-no-changes`: exit
  0.

---

## 7. Requisitos no funcionales

### RNF-1 — Compatibilidad con la arquitectura

- Después de F-003, el grafo de dependencias sigue
  cumpliendo las reglas R-1 a R-5 de F-007:
  - R-1 (Domain sin EF Core): `Domain` no importa
    `Microsoft.EntityFrameworkCore.*`. F-003 no
    toca `Domain`.
  - R-2 (Application sin EF Core): F-003 no toca
    `Application`.
  - R-3 (Infrastructure → Application + Domain):
    sin cambios.
  - R-4 (UI no → Infrastructure salvo composition
    root): F-003 no toca `UI`.
  - R-5 (src/ no → Tests): sin cambios.
- Los 11 tests de NetArchTest pasan sin
  modificaciones.

### RNF-2 — Determinismo del seed

- Las 14 entradas del seed tienen IDs
  deterministas (no `Guid.NewGuid()`). El seed es
  reproducible: ejecutar la migración en diferentes
  máquinas produce los mismos IDs.
- El `CreatedAt` y `UpdatedAt` de las 14
  entradas son un timestamp UTC fijo
  (`2026-01-01 00:00:00 UTC`).

### RNF-3 — Idempotencia

- El seed es idempotente: re-aplicar la migración
  no duplica registros. La idempotencia viene de
  `__EFMigrationsHistory` (EF Core solo aplica
  migraciones pendientes).
- Los IDs deterministas garantizan que, si en el
  futuro se regenera la migración (e.g., por un
  cambio en otra parte del modelo), las nuevas
  versiones producen los mismos IDs para el seed.

### RNF-4 — Sin scope creep

- F-003 NO modifica la migración `InitialSchema`.
- F-003 NO crea los 15 repositorios (F-004).
- F-003 NO enchufa `AddPersistence` en
  `Bootstrap.ConfigureServices` (F-005).
- F-003 NO aplica migraciones al arranque (F-005).
- F-003 NO añade `<ProjectReference>` de UI a
  Infrastructure (F-005).
- F-003 NO añade tests de integración (F-006).
- F-003 NO modifica `AppDbContext`, las
  configuraciones, `Domain`, `Application`, `UI`,
  `Tests.Unit`, ni `Tests.Integration`.

### RNF-5 — Mensajes y convenciones

- Nombres en español en el seed (es-DO per
  `docs/vision.md` §3). Las categorías y los
  métodos de pago se siembran en español; la UI
  puede traducirlos en runtime si i18n lo
  requiere (F-012).
- XML doc en los métodos públicos del
  `SeedData.Apply` actualizado (cita los IDs
  deterministas y la fecha fija).
- File-scoped namespace (proyecto rule).
- `using` directives al tope del archivo.

---

## 8. Reglas de negocio

### RN-1 — IDs deterministas para el sistema

- Las 9 categorías tienen IDs del rango
  `11111111-1111-1111-1111-00000000000X` (X = 1..9).
- Los 5 métodos de pago tienen IDs del rango
  `22222222-2222-2222-2222-00000000000X` (X = 1..5).
- Esta convención hace que los IDs del sistema
  sean fácilmente reconocibles en logs, en
  la DB, y en los tests.
- Los usuarios NUNCA crean entidades con estos
  prefijos (sus `Guid.NewGuid()` son aleatorios).
  La colisión es prácticamente imposible.

### RN-2 — Timestamp fijo para `CreatedAt`/`UpdatedAt`

- Las 14 entradas usan
  `new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)`.
- Esto evita que el seed "viaje en el tiempo" si
  alguien regenera la migración en otra zona
  horaria.
- Si en el futuro se necesita un timestamp más
  realista (e.g., la fecha de release del
  producto), se cambia en una nueva migración
  que actualice los registros. No se modifica
  esta migración.

### RN-3 — `IsSystem=true` en todas las entradas

- Las 14 entidades del sistema tienen
  `IsSystem=true` para que la UI (futura, Fase
  3+) pueda impedir su borrado y, opcionalmente,
  su modificación (e.g., no permitir renombrar
  "Comida").
- El repositorio (F-004) **NO** valida
  `IsSystem` (eso es responsabilidad de la UI o
  del use case). El seed solo marca los registros
  para que las capas superiores apliquen la regla
  de negocio.
- Si en el futuro el usuario quiere renombrar
  "Comida" a "Alimentación", debe pasar por la
  UI (que rechaza el cambio) o por SQL directo
  (que no debería ocurrir).

### RN-4 — `SeedSystemData` es una migración nueva, no una edición de `InitialSchema`

- `InitialSchema` (F-002) crea el schema. No
  contiene datos.
- `SeedSystemData` (F-003) añade los datos. No
  modifica el schema.
- Esta separación sigue la convención de EF Core
  y permite que los developers con DB pre-F-002
  apliquen solo el seed sin tener que
  re-aplicar el schema.

### RN-5 — El seed está en español (es-DO)

- `docs/vision.md` §3 fija que la app está
  orientada a un usuario dominicano y los
  mensajes por defecto son en español.
- Las categorías y los métodos de pago se
  siembran en español. La UI puede traducir
  (F-012), pero el seed base es español.
- Si en el futuro la app soporta múltiples
  idiomas, el seed NO se duplica para cada
  idioma. Se mantiene un solo seed (español) y la
  UI traduce en runtime.

---

## 9. Modelo de dominio

F-003 **NO** introduce nuevas entidades. Solo
rellena las entidades existentes con datos
iniciales. El "modelo" que añade F-003 es el
siguiente conjunto de datos:

**9 Categorías** (en `Categories`):

| Name | IsSystem | Color | Icon |
| --- | --- | --- | --- |
| Comida | true | `#E67E22` | 🍔 |
| Transporte | true | `#3498DB` | 🚗 |
| Hogar | true | `#9B59B6` | 🏠 |
| Salud | true | `#1ABC9C` | 💊 |
| Ocio | true | `#E74C3C` | 🎮 |
| Educación | true | `#F39C12` | 📚 |
| Ropa | true | `#16A085` | 👕 |
| Servicios | true | `#34495E` | 💡 |
| Otros | true | `#95A5A6` | (null) |

**5 Métodos de pago** (en `PaymentMethods`):

| Name | IsSystem |
| --- | --- |
| Efectivo | true |
| Tarjeta de débito | true |
| Tarjeta de crédito | true |
| Transferencia | true |
| Otro | true |

Todos con `CreatedAt` y `UpdatedAt` en
`2026-01-01 00:00:00 UTC`.

---

## 10. Configuración técnica

### 10.1 — IDs deterministas

**9 Categorías:**

| Name | Id |
| --- | --- |
| Comida | `11111111-1111-1111-1111-000000000001` |
| Transporte | `11111111-1111-1111-1111-000000000002` |
| Hogar | `11111111-1111-1111-1111-000000000003` |
| Salud | `11111111-1111-1111-1111-000000000004` |
| Ocio | `11111111-1111-1111-1111-000000000005` |
| Educación | `11111111-1111-1111-1111-000000000006` |
| Ropa | `11111111-1111-1111-1111-000000000007` |
| Servicios | `11111111-1111-1111-1111-000000000008` |
| Otros | `11111111-1111-1111-1111-000000000009` |

**5 Métodos de pago:**

| Name | Id |
| --- | --- |
| Efectivo | `22222222-2222-2222-2222-000000000001` |
| Tarjeta de débito | `22222222-2222-2222-2222-000000000002` |
| Tarjeta de crédito | `22222222-2222-2222-2222-000000000003` |
| Transferencia | `22222222-2222-2222-2222-000000000004` |
| Otro | `22222222-2222-2222-2222-000000000005` |

### 10.2 — Timestamp fijo

- `new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)`
- Aplicado a `CreatedAt` y `UpdatedAt` de las 14
  entradas.

### 10.3 — Colores hex para categorías

- Comida: `#E67E22` (naranja)
- Transporte: `#3498DB` (azul)
- Hogar: `#9B59B6` (púrpura)
- Salud: `#1ABC9C` (turquesa)
- Ocio: `#E74C3C` (rojo)
- Educación: `#F39C12` (amarillo)
- Ropa: `#16A085` (verde azulado)
- Servicios: `#34495E` (gris oscuro)
- Otros: `#95A5A6` (gris claro)

### 10.4 — Iconos emoji para categorías

- Comida: 🍔
- Transporte: 🚗
- Hogar: 🏠
- Salud: 💊
- Ocio: 🎮
- Educación: 📚
- Ropa: 👕
- Servicios: 💡
- Otros: (null — sin icono)

### 10.5 — Convenciones de código

- File-scoped namespace:
  `namespace ControlDeFinanzasIA.Infrastructure.Persistence;`.
- `using` directives al tope.
- XML doc en `SeedData.Apply` actualizado: cita
  las 14 entradas, los IDs deterministas, y la
  fecha fija.
- Nombres en español (es-DO per `docs/vision.md`).
- La clase `SeedData` sigue siendo `public static`
  (heredado de F-001; `static` implica
  inmutabilidad y no requiere instanciación).

---

## 11. Validaciones

| # | Validación | Cuándo se aplica | Comportamiento esperado |
| --- | --- | --- | --- |
| V-1 | El comando `dotnet ef migrations add SeedSystemData` se ejecuta sin errores. | Después de modificar `SeedData.Apply` y compilar. | Si falla, el implementador diagnostica (versión de `dotnet-ef` incorrecta, paquete faltante, etc.) antes de continuar. |
| V-2 | La migración `SeedSystemData` tiene 14 `InsertData` calls (9 categorías + 5 métodos). | Inspección del `Up` de la migración. | Si falta alguna, ajustar el `HasData` y regenerar. |
| V-3 | La migración `SeedSystemData` tiene 14 `DeleteData` calls en el `Down`. | Inspección del `Down`. | Si falta alguna, ajustar y regenerar. |
| V-4 | `InitialSchema` no se ha modificado. | `git diff` sobre `20260715152603_InitialSchema.cs` (o el timestamp que tenga). | Si se modificó, revertir el cambio (la nueva migración es la responsable del seed). |
| V-5 | `dotnet build` desde la raíz: 0 warnings, 0 errors. | Después de la generación. | Si hay warnings, ajustar. |
| V-6 | `dotnet test`: 111 tests verdes. | Después del build. | Si falla, investigar (no debería). |
| V-7 | Re-aplicar la migración no duplica los 14 registros. | Verificación manual o con `dotnet ef database update` dos veces. | `SELECT COUNT(*) FROM Categories` devuelve 9 (no 18). |

---

## 12. Casos borde

### EC-1 — DB con seed parcial

- Un developer actualiza desde F-002 sin ejecutar
  F-003. Tiene las tablas pero no los registros
  del sistema.
- Al ejecutar F-005 (composition root +
  `Migrate()`), se aplica `SeedSystemData`, que
  inserta los 14 registros.
- **Postcondición:** la DB tiene las 2 tablas y
  los 14 registros. La UI funciona.

### EC-2 — DB con seed antiguo (versión vieja)

- Si en el futuro se cambia el seed (e.g., se
  añade una categoría "Salud mental" con Id
  `00000000-...-010`), se genera una nueva
  migración (e.g., `AddHealthCategory`) que añade
  solo el nuevo registro. Las migraciones
  anteriores ya están aplicadas; la nueva se
  aplica sola.

### EC-3 — Usuario crea una categoría con nombre duplicado

- El usuario (vía UI) intenta crear "Comida" con
  un `Guid.NewGuid()` aleatorio.
- El repositorio (F-004) consulta
  `ExistsByNameAsync("Comida")` y devuelve
  `true`.
- El repositorio devuelve
  `Result.Failure("DuplicateName", "...")`.
- La UI muestra el error. No hay colisión de IDs
  (el Guid del usuario es aleatorio, el del
  sistema es `11111111-...-001`).

### EC-4 — El usuario edita el `SeedData.Apply` localmente

- Un developer modifica
  `SeedData.Apply` (e.g., añade un log).
- **No debe** regenerar la migración (eso
  requeriría un nuevo `InsertData` en
  `SeedSystemData`).
- El `HasData` en el modelo cambia, pero la
  migración commiteada sigue siendo la misma
  (los 14 `InsertData` originales).
- Si la modificación es funcional (cambia los
  datos del seed), debe ir acompañada de una
  nueva migración.

### EC-5 — SQLite `NOCASE` collation y nombres duplicados

- El índice único en `Name` con `NOCASE`
  (configurado en F-002) hace que "Comida" y
  "comida" sean treated as iguales.
- El seed inserta "Comida" (con C mayúscula).
- Si un usuario intenta insertar "comida" (con
  c minúscula), la inserción falla por el índice
  único. El repositorio captura esto y devuelve
  `Result.Failure`.

### EC-6 — Migration tools no disponibles

- Si el developer no ha ejecutado
  `dotnet tool restore` desde F-002, el comando
  `dotnet ef migrations add` falla con "tool not
  found".
- **Mitigación:** documentar en el commit / PR
  que el primer paso es `dotnet tool restore`.
  F-001 §T-001.4 ya documenta esto.

---

## 13. Dependencias

### 13.1 Prerrequisitos de implementación

- **F-001 de Fase 2** (completada): provee
  `AppDbContext` con `OnModelCreating` que llama
  a `SeedData.Apply(modelBuilder)`, y el archivo
  `SeedData.cs` con la firma completa (stub
  vacío).
- **F-002 de Fase 2** (completada): provee las
  configuraciones de las entidades, la migración
  `InitialSchema`, y la herramienta `dotnet-ef`
  8.0.11 instalada localmente.
- **F-001 a F-008 de Phase 1** (todas
  completadas): proveen el proyecto
  `Infrastructure` y la disciplina de CPM.

### 13.2 Dependencias con otras features

| Feature | Relación |
| --- | --- |
| F-001 (Phase 2) | Provee el stub `SeedData.cs` y el `AppDbContext` que lo invoca. F-003 rellena el stub. |
| F-002 (Phase 2) | Generó `InitialSchema` con las 2 tablas vacías. F-003 genera la migración que las puebla. |
| F-004 (Phase 2) | Implementa los repositorios. El seed de F-003 es el set de datos que el repositorio `GetAll` devuelve por defecto. |
| F-005 (Phase 2) | Aplica `Migrate()` al arrancar la app. Las 2 migraciones (`InitialSchema` + `SeedSystemData`) se aplican en orden. |
| F-006 (Phase 2) | Testa el seed con SQLite in-memory. Los IDs deterministas permiten que los tests predefinan los registros esperados. |
| F-007 (Phase 1) | Los tests R-1 a R-5 verifican que F-003 no rompe la arquitectura. |

### 13.3 Bloqueos

- **Ninguno.** F-003 puede implementarse ahora
  mismo. Los prerrequisitos (F-001 y F-002) están
  cumplidos.

---

## 14. Fuera de alcance (detallado)

Repetición con énfasis. F-003 **NO** hace lo
siguiente:

- **Modificar la migración `InitialSchema` de
  F-002.** Queda intacta. La nueva migración
  `SeedSystemData` es la responsable del seed.
- **Editar el `Designer.cs` o el `ModelSnapshot.cs`
  de `InitialSchema` manualmente.** Solo se
  actualizan automáticamente cuando la herramienta
  genera una nueva migración.
- **Implementar los 15 repositorios concretos.**
  Es F-004.
- **Wiring del `AddPersistence` en
  `Bootstrap.ConfigureServices`.** Es F-005.
- **Aplicar migraciones al arranque.** Es F-005.
- **`<ProjectReference>` de `UI` a `Infrastructure`.**
  Es F-005.
- **Tests de integración con SQLite in-memory.** Es
  F-006. F-006 puede aprovechar los IDs
  deterministas de F-003 para testear que el seed
  se aplica correctamente.
- **Conversión de value objects en EF Core.** No
  aplica (las entidades de seed no tienen
  `Money` o `DateRange`).
- **Migraciones adicionales a `SeedSystemData`.**
  Solo se genera esta segunda migración. Las
  siguientes son trabajo de features futuras
  (e.g., si se añade una categoría "Salud
  mental", se genera `AddHealthCategory`).
- **Modificaciones a `AppDbContext`, las
  configuraciones, `Domain`, `Application`, `UI`,
  `Tests.Unit`, `Tests.Integration`.** F-003 solo
  toca `SeedData.cs` y los 3 archivos de
  migración.
- **Modificaciones al CI workflow.** El workflow
  existente (`dotnet build` + `dotnet test`) sigue
  siendo suficiente.
- **Loguear el seed.** F-003 no añade
  `ILogger`. Si en el futuro se quiere loguear
  cuántos registros se insertaron, se hace en
  F-005 (composition root).

---

## 15. Criterios de aceptación

La feature se considera completada cuando
**todos** los siguientes puntos se cumplen.

### AC-1 — `SeedData.Apply` implementado

- [ ] `src/ControlDeFinanzasIA.Infrastructure/Persistence/SeedData.cs`
      existe (introducido por F-001 como stub).
- [ ] El cuerpo de `Apply` ya no es trivial:
      contiene `modelBuilder.Entity<Category>().HasData(...)`
      con las 9 categorías.
- [ ] Contiene `modelBuilder.Entity<PaymentMethod>().HasData(...)`
      con los 5 métodos.
- [ ] Cada entrada tiene:
      - `Id` determinista (per §10.1).
      - `IsSystem = true`.
      - `CreatedAt` y `UpdatedAt` en
        `new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)`.
- [ ] Las 9 categorías tienen `Name`, `Color` (excepto
      "Otros" que tiene `null`), e `Icon` (excepto
      "Otros" que tiene `null`) per §10.3 y §10.4.
- [ ] Los 5 métodos tienen `Name` per §10.
- [ ] File-scoped namespace.
- [ ] `using` al tope.
- [ ] XML doc actualizado en `Apply` cita los IDs
      deterministas y la fecha fija.

### AC-2 — Migración `SeedSystemData` generada

- [ ] El comando
      `dotnet ef migrations add SeedSystemData --project src/ControlDeFinanzasIA.Infrastructure --startup-project src/ControlDeFinanzasIA.Infrastructure`
      se ejecutó exitosamente.
- [ ] Los 3 archivos existen en
      `Infrastructure/Persistence/Migrations/`:
      - `YYYYMMDDhhmmss_SeedSystemData.cs`.
      - `YYYYMMDDhhmmss_SeedSystemData.Designer.cs`.
      - `AppDbContextModelSnapshot.cs` (sobreescrito).
- [ ] El `Up` de la migración tiene 14
      `InsertData` calls: 9 para `Categories` y 5
      para `PaymentMethods`.
- [ ] El `Down` de la migración tiene 14
      `DeleteData` calls.
- [ ] Los IDs en los `InsertData` coinciden con
      los de §10.1.
- [ ] Los timestamps en los `InsertData` son
      `2026-01-01 00:00:00 UTC`.

### AC-3 — `InitialSchema` intacta

- [ ] `git diff` sobre
      `YYYYMMDDhhmmss_InitialSchema.cs` (el timestamp
      de F-002) no muestra cambios desde el commit
      de F-002.
- [ ] El `Up` de `InitialSchema` sigue teniendo solo
      `CreateTable` y `CreateIndex` (sin
      `InsertData`).
- [ ] El `Designer.cs` de `InitialSchema` no se ha
      modificado.

### AC-4 — Compilación y tests

- [ ] `dotnet build -c Release` desde la raíz:
      0 warnings, 0 errors.
- [ ] `dotnet test` desde la raíz: 111 tests
      verdes (100 + 11 de F-007), 0 failures.
- [ ] Los 11 tests de NetArchTest de F-007 pasan
      sin modificaciones.

### AC-5 — Idempotencia (verificación manual)

- [ ] Ejecutar
      `dotnet ef database update --project src/ControlDeFinanzasIA.Infrastructure --startup-project src/ControlDeFinanzasIA.Infrastructure`
      en una DB limpia (o reiniciar la DB).
- [ ] Verificar que `Categories` tiene 9 filas y
      `PaymentMethods` tiene 5.
- [ ] Ejecutar el mismo comando una segunda vez.
- [ ] Verificar que `Categories` sigue teniendo 9
      filas y `PaymentMethods` sigue teniendo 5
      (idempotencia).
- [ ] Verificar que `__EFMigrationsHistory` tiene
      tanto `InitialSchema` como `SeedSystemData`
      como aplicadas.

### AC-6 — Convenciones del repo

- [ ] File-scoped namespace en
      `SeedData.cs` (sin cambios respecto a F-001).
- [ ] `using` al tope.
- [ ] XML doc en `Apply` actualizado.
- [ ] `dotnet format style --verify-no-changes`:
      exit 0.

### AC-7 — Sin scope creep

- [ ] NO se ha modificado la migración
      `InitialSchema` (ni su `.cs`, ni su
      `Designer.cs`).
- [ ] NO se han modificado las configuraciones
      (`CategoryConfiguration`,
      `PaymentMethodConfiguration`).
- [ ] NO se ha modificado `AppDbContext.cs`.
- [ ] NO se han modificado `Domain`,
      `Application`, `UI`, `Tests.Unit`,
      `Tests.Integration`.
- [ ] NO se ha añadido lógica de repositorios
      (F-004).
- [ ] NO se ha añadido wiring de `AddPersistence`
      en `Bootstrap.ConfigureServices` (F-005).
- [ ] NO se ha añadido `<ProjectReference>` de
      UI a Infrastructure (F-005).
- [ ] NO se han añadido tests de integración
      (F-006).

### AC-8 — Documentación y trazabilidad

- [ ] `spec/phases/phase-02/features.md`
      actualizado: F-003 marcada como ✅
      COMPLETADA en la tabla resumen y en el
      header de la sección.
- [ ] La spec se renombra a
      `spec/features/phase-02/F-003-seed-data.done.md`.

---

## 16. Estructura de archivos esperada

```
src/
└── ControlDeFinanzasIA.Infrastructure/
    └── Persistence/                          (existente)
        ├── AppDbContext.cs                   (F-001, sin cambios)
        ├── EfUnitOfWork.cs                   (F-001, sin cambios)
        ├── ServiceCollectionExtensions.cs    (F-001, sin cambios)
        ├── DesignTimeAppDbContextFactory.cs  (F-002, sin cambios)
        ├── Configurations/                   (F-002, sin cambios)
        │   ├── CategoryConfiguration.cs
        │   └── PaymentMethodConfiguration.cs
        ├── SeedData.cs                       ← F-003 (modificado: cuerpo lleno)
        └── Migrations/                       (F-002 + F-003)
            ├── YYYYMMDDhhmmss_InitialSchema.cs          (F-002, sin cambios)
            ├── YYYYMMDDhhmmss_InitialSchema.Designer.cs (F-002, sin cambios)
            ├── YYYYMMDDhhmmss_SeedSystemData.cs          ← F-003 (nuevo)
            ├── YYYYMMDDhhmmss_SeedSystemData.Designer.cs ← F-003 (nuevo)
            └── AppDbContextModelSnapshot.cs             (F-002/F-003, sobreescrito)
```

**Modificaciones a `.csproj`:** ninguna. F-003 no
añade ni quita paquetes ni referencias.

**Modificaciones al CI workflow:** ninguna.

---

## 17. Decisiones de diseño explícitas

### 17.1 — `HasData` en `OnModelCreating` + nueva migración `SeedSystemData`

**Decisión:** ambas.

**Justificación:**
- `HasData` en `OnModelCreating` mantiene el
  modelo "vivo" para regeneraciones futuras
  (si se añade una categoría, EF Core detecta
  la diferencia y genera una migración).
- La nueva migración `SeedSystemData` aplica
  el seed **en la primera ejecución** de la
  app en un equipo limpio. Sin la migración
  explícita, el `HasData` queda "informacional"
  en el snapshot pero nunca se inserta en la
  DB.
- Las dos juntas: el modelo es la fuente de
  verdad (regenerable), y la migración es la
  materialización inicial (commiteada).

**Alternativas descartadas:**
- **Solo `HasData` sin nueva migración:** el
  seed nunca se aplica en producción. No
  funciona.
- **Solo `InsertData` en una nueva migración
  sin `HasData`:** el seed queda "congelado" en
  la migración. Si en el futuro se quiere
  modificar, hay que regenerar la migración.
  Pierde la ventaja del `HasData`.
- **Editar `InitialSchema` para añadir
  `InsertData`:** rompe la convención de EF
  Core (migraciones inmutables) y dificulta el
  desarrollo paralelo. La nueva migración es
  más limpia.

### 17.2 — IDs deterministas (no `Guid.NewGuid()`)

**Decisión:** IDs del rango `11111111-...` para
categorías y `22222222-...` para métodos.

**Justificación:**
- Los tests pueden predefinir los IDs esperados
  y referenciar las entidades del sistema sin
  parsear la DB.
- El seed es reproducible.
- Los IDs son fácilmente reconocibles en logs
  y en la DB.
- La colisión con `Guid.NewGuid()` de usuarios
  es prácticamente imposible (los del sistema
  tienen un patrón reconocible).

### 17.3 — Timestamp fijo (`2026-01-01 00:00:00 UTC`)

**Decisión:** `new DateTime(2026, 1, 1, 0, 0, 0,
DateTimeKind.Utc)` para todas las 14 entradas.

**Justificación:**
- Evita que el seed "viaje en el tiempo" si
  alguien regenera la migración en otra zona
  horaria.
- Es un valor único, fácil de recordar y de
  buscar.
- Si en el futuro se quiere un timestamp más
  realista, se actualiza en una nueva migración.

### 17.4 — `IsSystem=true` en todas las entradas

**Decisión:** las 14 entidades del sistema tienen
`IsSystem=true`.

**Justificación:**
- La UI (Fase 3+) puede impedir su borrado
  (regla de negocio: el usuario no puede
  eliminar "Comida").
- El nombre "system" es explícito: son las
  entidades predefinidas por la app, no
  creadas por el usuario.

### 17.5 — Idempotencia via `__EFMigrationsHistory`

**Decisión:** la idempotencia viene del framework
(`__EFMigrationsHistory`), no del código de la
migración. La migración NO incluye lógica de
upsert en su `Up`.

**Justificación:**
- EF Core ya gestiona la idempotencia vía la
  tabla de historial. Re-aplicar `Migrate()`
  es no-op si las migraciones ya están
  aplicadas.
- Añadir lógica de upsert en el `Up` sería
  redundante y complicaría el código.
- La "doble protección" contra duplicados (a
  nivel de constraint UNIQUE en `Name`) se
  delega al repositorio (F-004), no al seed.

### 17.6 — Categorías y métodos en español

**Decisión:** todas las entradas se siembran en
español (es-DO per `docs/vision.md` §3).

**Justificación:**
- `docs/vision.md` fija la app como orientada a
  un usuario dominicano, con español por
  defecto.
- El seed es la base. La UI puede traducir
  (F-012) si añade i18n, pero el seed base es
  español.

### 17.7 — No se modifica `InitialSchema`

**Decisión:** `InitialSchema` (de F-002) queda
intacta. La nueva migración es
`SeedSystemData`.

**Justificación:**
- Las migraciones de EF Core son inmutables.
  Editarlas rompe la convención.
- Los developers con DB pre-F-003 pueden
  aplicar solo `SeedSystemData` sin tener que
  re-aplicar `InitialSchema`.

---

## 18. Riesgos y mitigaciones

| Riesgo | Probabilidad | Impacto | Mitigación |
| --- | --- | --- | --- |
| El developer edita la migración `InitialSchema` por error. | Baja | Alto | El tasks file de F-003 documenta explícitamente "NO modificar `InitialSchema`". El PR review debe verificar con `git diff`. |
| El developer regenera la migración `SeedSystemData` y sobrescribe los archivos. | Baja | Medio | Es comportamiento esperado si hay cambios en el modelo. Pero no debe regenerarse por cambios cosméticos en `SeedData.Apply`. |
| Los `InsertData` en la migración tienen valores incorrectos (e.g., Guid mal formateado). | Baja | Alto | La herramienta EF Core genera los `InsertData` automáticamente desde el modelo. Verificación visual del `Up` confirma los valores. |
| `dotnet ef` no está disponible porque el developer no ejecutó `dotnet tool restore`. | Baja | Medio | Documentar en el commit / PR. F-001 §T-001.4 ya documenta esto. |
| R-1 de F-007 falla porque `Domain` se contaminó con EF Core. | Muy baja (F-003 no toca `Domain`) | Alto | T-005 del tasks file verifica R-1. |
| El sembrado se duplica por un bug en EF Core. | Muy baja | Alto | AC-5 de §15 verifica la idempotencia ejecutando la migración dos veces. |
| El usuario crea una categoría con el mismo Guid que el sistema (por colisión con `Guid.NewGuid()`). | Prácticamente imposible | Bajo | Los IDs del sistema tienen un patrón reconocible (`11111111-...`); los `Guid.NewGuid()` son aleatorios. Probabilidad de colisión: ~0. |
| El `AppDbContextModelSnapshot.cs` se corrompe porque la herramienta tuvo un bug. | Muy baja | Alto | El snapshot es generado por la herramienta, no editado a mano. Si falla, se borra y regenera. |
| La migración `SeedSystemData` no se aplica porque F-005 no llama a `Migrate()`. | Baja (F-005 está en scope) | Alto | F-005 es responsable de aplicar las migraciones. F-003 solo genera la migración. |
| El `Up` de `SeedSystemData` tiene `InsertData` duplicados (por error de la herramienta). | Muy baja | Alto | Verificación visual del `Up` antes de commit. |

---

## 19. Referencias

- `docs/roadmap.md` — Fase 2 (Entregables esperados,
  Riesgos principales, Criterios de fase completada).
- `docs/architecture.md` — §3 (solución y proyectos),
  §4 (grafo de dependencias), §8 (capa de
  Infrastructure: persistencia, repositorios).
- `docs/vision.md` — §3 (es-DO como locale por defecto),
  §4.10 (Datos y portabilidad).
- `spec/constitution/tech-stack.md` — paquetes,
  convenciones de código, CPM.
- `spec/constitution/mission.md` — Clean
  Architecture, principios.
- `spec/phases/phase-02/features.md` — F-003 dentro
  del desglose de la fase 2 (6 features totales).
- `spec/features/phase-02/F-001-appdbcontext-addpersistence.done.md`
  — provee `AppDbContext` y el stub de `SeedData`
  que F-003 rellena. §AC-8 y §17.5 documentan el
  stub.
- `spec/features/phase-02/F-002-entity-configurations-migration.done.md`
  — provee las configuraciones y la primera
  migración (`InitialSchema`) sin seed. F-003
  genera la segunda.
- `spec/features/phase-01/F-001-skeleton-solution.done.md`
  — convenciones de `.editorconfig`, `stylecop.json`,
  CPM, y `.gitignore`.
- `spec/features/phase-01/F-004-Abstracciones-clock-pathresolver.done.md`
  — `IPathResolver` que F-005 enchufa.
- `spec/features/phase-01/F-006-contrato-interfaces-repo.done.md`
  — `IUnitOfWork` y las 15 interfaces que F-004
  implementa (los repos).
- `spec/features/phase-01/F-007-validacion-arquitectonica.done.md`
  — los tests R-1 a R-5 que F-003 no debe romper.
- `spec/features/phase-01/F-008-composition-root.done.md`
  — el `Bootstrap.ConfigureServices` que F-005
  enchufa.
- `spec/README.md` — convención del directorio
  `spec/`.
- EF Core 8 documentation oficial (a consultar vía
  Context7 cuando se implemente).
- `dotnet ef` CLI documentation (a consultar vía
  Context7 cuando se genere la migración).
