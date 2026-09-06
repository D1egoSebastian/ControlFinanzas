# Mission

## Objetivo

Construir una aplicación de escritorio para Windows que ayude a los usuarios a administrar sus finanzas personales mediante el registro de ingresos y gastos, visualización de estadísticas y generación de presupuestos inteligentes utilizando un modelo de lenguaje (LLM).

---

# Objetivos del proyecto

- Mantener una arquitectura limpia y escalable.
- Priorizar código legible antes que optimizaciones prematuras.
- Cada módulo debe ser fácilmente testeable.
- Evitar duplicación de lógica.
- Seguir el patrón MVVM correctamente.
- Implementar funcionalidades de forma incremental.

---

# Tecnologías

UI
- WPF (.NET 8)
- MVVM

Persistencia
- SQLite
- Entity Framework Core (Code First)

Inyección de dependencias
- Microsoft.Extensions.DependencyInjection

IA
- API HTTP
- Claude u OpenAI
- Respuestas JSON estructuradas

Logging
- Microsoft.Extensions.Logging

---

# Principios

Siempre favorecer:

- Simplicidad
- Bajo acoplamiento
- Alta cohesión
- SOLID
- Clean Architecture cuando aplique

---

# Convenciones

## Código

- Clases pequeñas y con una única responsabilidad.
- Métodos cortos.
- Evitar lógica en el Code Behind.
- Toda la lógica debe vivir en ViewModels o Services.

## Nombres

ViewModels

CustomerViewModel

Views

CustomerView

Servicios

BudgetService

Repositorios

ExpenseRepository

Interfaces

IExpenseRepository

---

# Antes de implementar cualquier funcionalidad

El agente debe:

1. Comprender el problema.
2. Revisar la documentación disponible.
3. Consultar Context7 cuando existan dudas sobre frameworks o APIs.
4. No asumir requerimientos inexistentes.
5. Mantener consistencia con la arquitectura existente.

---

# Qué NO debe hacer

- No crear código innecesariamente complejo.
- No introducir nuevas dependencias sin justificación.
- No romper la arquitectura existente.
- No modificar funcionalidades no relacionadas con la tarea.

---

# Flujo de trabajo esperado

1. Analizar el requerimiento.
2. Diseñar la solución.
3. Dividir en tareas pequeñas.
4. Implementar.
5. Verificar.
6. Refactorizar si es necesario.