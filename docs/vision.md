# Visión - ControlDeFinanzasIA

> Documento fundacional del producto. Define **qué** se construye y **qué no**.
> Las decisiones de **cómo** viven en `docs/architecture.md` y en `spec/constitution/`.

---

## 1. Propósito

`ControlDeFinanzasIA` es una aplicación de **escritorio cross-platform** (Windows, macOS, Linux) pensada para que una persona lleve el **control personal de sus finanzas** — gastos, ingresos, deudas, metas y presupuestos — con la ayuda de un modelo de lenguaje (IA) que se integra como asistente: categoriza, predice, alerta y responde preguntas sobre los datos del usuario.

Todo funciona **100% en local**. Los datos nunca abandonan el equipo del usuario, salvo cuando este explícitamente configura una API de IA y la invoca.

---

## 2. Usuario objetivo

- **Primario:** el dueño del proyecto, en **República Dominicana**, con ingresos/gastos en **DOP** y **USD**.
- **Secundario:** un círculo muy reducido de personas de confianza (ej. un amigo) que reciban el binario y lo usen **cada uno con su propio archivo de datos**, sin compartición de información entre instalaciones.

No es un producto multi-tenant. **Cada instalación es un único usuario, con un único archivo de datos local.**

---

## 3. Propuesta de valor

- **Privacidad por defecto:** los datos financieros viven en un archivo en el disco del usuario. No hay servidor, no hay cuenta, no hay telemetría.
- **Multi-moneda con DOP y USD:** el usuario registra en la moneda original; los reportes muestran conversión a la otra moneda usando una tasa de cambio en vivo.
- **IA como asistente, no como sustituto:** la IA sugiere, categoriza, predice y conversa, pero el usuario mantiene el control total sobre sus datos.
- **Bring Your Own AI:** el usuario elige el proveedor y coloca su propia API key. Soporta modelos gratuitos (estilo OpenRouter) y de pago (Anthropic, OpenAI, etc.).
- **Bilingüe ES / EN**, con ES por defecto, orientado a un usuario dominicano.
- **Espectro completo:** la app cubre gastos del día a día, ingresos, deudas con cuotas, suscripciones, gastos fijos, metas de ahorro con fecha objetivo, presupuestos mensuales y predicciones.

---

## 4. Alcance funcional

### 4.1 Transacciones

| Tipo | Descripción |
| --- | --- |
| **Gastos del día a día** | Cualquier compra o pago puntual. |
| **Ingresos** | Sueldo, freelance, transferencias recibidas, etc. Siempre **manuales**. |
| **Deudas / préstamos** | Con plan de **cuotas automáticas** (cantidad, frecuencia, monto por cuota, fecha de inicio). El usuario registra cada pago. |
| **Suscripciones** | Netflix, Spotify, etc. Gastos fijos mensuales, con su propia lista editable. |
| **Gastos fijos no-suscripción** | Alquiler, internet, colegio, gimnasio, etc. Recurrentes editables. |
| **Pagos programados de deudas** | Generados automáticamente a partir del plan de cuotas de cada deuda. |

**No** hay ingresos recurrentes automáticos: cada ingreso se registra a mano.

### 4.2 Organización de un movimiento

Cada movimiento lleva, como mínimo:

- Fecha.
- Monto y **moneda original** (DOP o USD).
- **Categoría** (plana, configurable por el usuario).
- **Tags libres** opcionales (uno o varios).
- **Método de pago** (etiqueta: efectivo, débito, crédito, transferencia, otro). Es solo una etiqueta, **no** se llevan balances por método.
- Nota opcional.

### 4.3 Búsqueda y vistas

- **Filtros** por categoría, tags y rango de fecha.
- **Vistas guardadas** con un nombre (ej. "Suscripciones del mes", "Gastos de vacaciones").
- **Sin búsqueda por texto libre** y sin filtros por monto en v1.

### 4.4 Metas de ahorro

- Meta con **monto objetivo** y **fecha objetivo** (ej. "20,000 DOP para diciembre 2026 para un viaje").
- **Aportes manuales** registrados por el usuario.
- **Aportes automáticos** derivados del cálculo "ingreso no gastado" en un periodo.
- **Sugerencia de IA:** "según tus movimientos, para llegar a tu meta en X meses deberías ahorrar Y al mes".

### 4.5 Presupuestos

- **Presupuesto mensual por categoría** (ej. "5,000 DOP en comida este mes").
- Alerta cuando el gasto real se acerca o supera el límite.
- ALerta cuando un presupuesto supone el no poder aportar en el aporte de la meta de ahorro (ej. Presupuesto de este monitor es de 30,000 DOP, excede tus limites para un ahorro mensual estable.)

### 4.6 Visualizaciones y reportes

- Resumen del mes: gastos vs ingresos, % de cambio vs mes anterior.
- Gráficos por categoría (torta / barras).
- Evolución temporal (línea) por semana, mes o año.
- Barras de progreso de metas y presupuestos.
- Vista de **calendario** de gastos.
- **Exportar** reportes a PDF y a Excel/CSV.

### 4.7 Vistas por periodo

- **Semana actual.**
- **Mes actual** con navegación hacia meses anteriores y siguientes.
- **Año completo.**
- **Comparar dos periodos** lado a lado (ej. este mes vs mes pasado).

No hay rangos personalizados arbitrarios en v1.

### 4.8 Notificaciones (in-app y/o sistema)

- **Recordatorios de pagos:** cuota de deuda próxima, suscripción a cobrarse, gasto fijo del mes.
- **Alertas de presupuesto:** cerca o por encima del límite.
- **Resumen diario / semanal:** agregado automático del periodo.
- **Alertas de anomalías:** gasto inusualmente alto respecto al patrón del usuario.

### 4.9 Inteligencia artificial (BYO)

Todas las funciones de IA pasan por una API configurable por el usuario. La app **no incluye** un modelo propio.

| Función | Descripción |
| --- | --- |
| Categorización automática | A partir del texto del movimiento ("compré pizza 25"), la IA infiere categoría. |
| Chat en lenguaje natural | El usuario pregunta sobre sus propios datos y la IA responde. |
| Insights y recomendaciones | Resúmenes y sugerencias personalizadas. |
| Predicción de fin de mes | Proyección de gastos hasta cierre de mes según el histórico. |
| Detección de anomalías | Gastos fuera del patrón. |
| Plan de ahorro para metas | Sugerencia de aporte mensual para llegar a la meta. |

**Configuración:** el usuario introduce su **API key** y elige **proveedor + modelo**. La app habla HTTP con APIs compatibles (Anthropic, OpenAI, OpenRouter y similares, sean gratuitas o de pago). **El usuario es responsable de su key y de los costos.**

### 4.10 Datos y portabilidad

- **Almacenamiento:** un único archivo local (SQLite) por instalación.
- **Ubicación por SO:**
  - Windows: `%APPDATA%\ControlDeFinanzasIA\`
  - macOS: `~/Library/Application Support/ControlDeFinanzasIA/`
  - Linux: `$XDG_CONFIG_HOME/ControlDeFinanzasIA/` o `~/.config/ControlDeFinanzasIA/`
- **Backup:** **manual**, mediante un botón "Exportar todo a archivo" y su contraparte "Importar desde archivo". El usuario decide dónde guarda el respaldo.
- **Importación inicial:** CSV/Excel genérico con mapeo de columnas configurable.

---

## 5. Fuera de alcance (no-goals explícitos)

Para evitar scope creep, lo siguiente **no se construye en v1** y no debería asumirse como parte del producto:

- Multi-cuenta con balances (efectivo, tarjeta, banco, cripto...).
- Inversiones: acciones, fondos, cripto, valoración de cartera.
- OCR de tickets o facturas.
- Gastos compartidos / IOUs / liquidaciones entre varias personas.
- Ingresos recurrentes automáticos.
- Sincronización en la nube o entre dispositivos.
- App móvil nativa o versión web.
- Telemetría, analítica remota, cuentas de usuario online.
- Pause/resume de una serie recurrente completa (sí se puede saltar un mes puntual).

---

## 6. Restricciones técnicas

- **Plataforma:** escritorio cross-platform (Windows, macOS, Linux). Sin servidor.
- **Lenguaje y framework UI:** **.NET 8 + Avalonia** con patrón MVVM.
- **Persistencia:** **SQLite** con **Entity Framework Core** (Code First).
- **Inyección de dependencias:** `Microsoft.Extensions.DependencyInjection`.
- **IA:** HTTP a APIs externas, contrato compatible con OpenAI / Anthropic / OpenRouter y similares. Configurada por el usuario, nunca embebida.
- **Internacionalización:** ES y EN, con ES por defecto. Formatos de fecha y moneda siguen el locale del usuario.

Detalles de implementación, módulos y estructura interna: ver `docs/architecture.md`.

---

## 7. Cualidades no funcionales

- **Privacidad y aislamiento:** los datos del usuario **no salen del equipo** sin su acción explícita (configurar y llamar a una API de IA). El binario no envía telemetría.
- **Integridad de datos:** las escrituras locales deben ser **atómicas** — un crash a mitad de un guardado no debe corromper el archivo de base de datos.
- **Rendimiento percibido:** registrar un movimiento debe ser **inmediato** (<200 ms en hardware típico). Las llamadas a IA son asíncronas y no bloquean la UI.
- **Resiliencia ante IA caída:** si la API de IA no responde o el usuario no ha configurado key, la app sigue siendo **100% funcional** sin IA. Las funciones de IA fallan silenciosamente con un mensaje claro.
- **Internacionalización:** mensajes, formatos de fecha y separadores de moneda siguen el locale activo (es-DO por defecto).
- **Compatibilidad:** Windows 10/11, macOS 12+, distribuciones Linux actuales (x64).
- **Instalación:** un único binario instalable por SO. No requiere runtime externo más allá de lo que la app traiga consigo (self-contained).

---

## 8. Casos de borde clave

Decisiones que un agente o desarrollador podría asumir mal. **Fijadas aquí:**

- **Conversión de moneda** se aplica **en tiempo de display**, no al guardar. El movimiento se almacena en su moneda original; los reportes convierten usando una tasa **actualizada periódicamente** desde una API.
- **Recurrentes generados** se pueden **saltar un mes**, **editar el monto** y **cambiar la fecha** puntualmente. **No** hay pause/resume de toda una serie.
- **Método de pago** es una **etiqueta**, no un balance. No se descuenta saldo por método.
- **Ingresos siempre manuales.** No hay ingresos recurrentes.
- **Categorías planas**, no jerárquicas. La granularidad adicional se resuelve con **tags libres**.
- **Sin gasto compartido / IOU** en v1, aunque varios usuarios puedan tener la app.
- **Conversión automática de IA:** si el usuario habla en español, la IA entiende español; si habla en inglés, inglés. El idioma de la UI y el de la consulta a la IA no están atados.
- **Cada instalación = un usuario = un archivo.** Compartir el binario no comparte datos.

---

## 9. Glosario mínimo

- **Movimiento:** cualquier registro de gasto o ingreso en la app.
- **Recurrente:** regla que genera movimientos periódicos (suscripción, gasto fijo, cuota de deuda).
- **Categoría:** etiqueta estructurada, plana, configurable. Obligatoria en cada movimiento.
- **Tag:** etiqueta libre, opcional, una o varias por movimiento. No estructurada.
- **Meta:** objetivo de ahorro con monto y fecha objetivo.
- **Presupuesto:** límite de gasto mensual por categoría.
- **BYO (Bring Your Own):** el usuario trae su propia API key / modelo para usar las funciones de IA.
- **Anomalía:** gasto que la IA detecta como fuera del patrón histórico del usuario.

---

## 10. Referencias internas

- `docs/roadmap.md` — fases, hitos y entregables.
- `docs/architecture.md` — módulos, capas, decisiones técnicas.
- `spec/constitution/mission.md` — resumen ejecutivo del propósito.
- `spec/constitution/tech-stack.md` — stack y convenciones.
- `spec/constitution/features/` — features específicas (criterios de aceptación).
