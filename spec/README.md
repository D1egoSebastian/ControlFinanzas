# `spec/` — convención de estructura

Este directorio contiene los documentos de especificación del proyecto,
organizados en tres áreas:

```
spec/
├── README.md                                  ← este archivo
├── constitution/                             ← documentos estables / fundacionales
│   ├── mission.md                            ← propósito ejecutivo del producto
│   └── tech-stack.md                         ← stack y convenciones técnicas
├── phases/                                   ← planificación a nivel de fase
│   └── phase-NN/
│       └── features.md                       ← desglose de features de la fase
└── features/                                 ← specs de features individuales
    └── phase-NN/
        └── F-XXX-name.done.md                ← spec de un feature (terminada)
        └── F-XXX-name.md                     ← spec de un feature (en curso)
```

## Reglas de la estructura

### `constitution/`

- **Estables.** Cambian solo cuando cambia la constitución del proyecto
  (misión, stack, convenciones).
- **Fuente de verdad** para todas las specs y tasks.

### `phases/phase-NN/`

- **Planificación** de la fase: lista de features que la componen, con
  dependencias, estimaciones y criterios de fase.
- **Un archivo por fase.** El archivo se llama `features.md` (no
  `phase-NN-features.md`) porque ya está dentro de la carpeta
  `phase-NN`.
- **No** contiene specs individuales; eso vive en `features/`.

### `features/phase-NN/`

- **Specs individuales** de cada feature de la fase.
- **Un archivo por feature.** El nombre sigue el patrón
  `F-XXX-short-name.md` (en curso) o `F-XXX-short-name.done.md`
  (terminada; convención del proyecto, ver spec de F-001).
- La spec referencia explícitamente su `F-XXX` dentro de
  `phases/phase-NN/features.md`.

## Convención de IDs de features

Los IDs de feature (`F-001`, `F-002`, ...) se numeran **dentro de cada
fase, empezando en 1**. Es decir, `F-002` aparece tanto en Fase 1
(Money value object) como en Fase 2 (Persistencia con EF Core +
SQLite).

Esto es intencional:

- **Los IDs son locales a la fase**, no globales. Cada fase tiene su
  propio `F-001`, `F-002`, etc.
- **El folder los desambigua:** `spec/features/phase-01/F-002-...md`
  vs `spec/features/phase-02/F-002-...md`.
- **Las specs referencian explícitamente su fase** en la cabecera
  (`**Fase:** N — ...`) y mencionan la posible colisión en la nota
  inicial cuando aplica.

## Cómo añadir una nueva fase

1. Crear `spec/phases/phase-NN/` (la nueva fase) y
   `spec/features/phase-NN/`.
2. Crear `spec/phases/phase-NN/features.md` con el desglose (mismo
   formato que las fases existentes: tabla resumen, features
   detalladas con las 9 secciones estándar).
3. Para cada feature del desglose, crear
   `spec/features/phase-NN/F-XXX-name.md` con la spec completa
   (las 14 secciones del template).
4. Cuando una feature se completa, renombrar a `.done.md`.

## Cómo añadir un nuevo documento

- Documentos **fundacionales** (mission, tech-stack): van en
  `spec/constitution/`.
- Documentos de **planificación de fase** (desglose de features): van
  en `spec/phases/phase-NN/features.md`.
- **Specs de feature** (especificación técnica de una capacidad): van
  en `spec/features/phase-NN/F-XXX-name.md`.

Si un documento no encaja en ninguna de estas tres categorías, es
señal de que hay que reconsiderar la estructura antes de añadirlo.
