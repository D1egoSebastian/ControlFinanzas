# Tasks — F-001 Reglas recurrentes

## Revisión de entrega

Estimated changed lines: 700–1100

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: pending
400-line budget risk: High

## Foundation

- [x] 1.1 Crear `RecurringRuleConfiguration`, DbSets y migración EF Core.
- [x] 1.2 Implementar `RecurringRuleRepository` y registrarlo en DI.
- [x] 1.3 Añadir tests de schema, round-trip y provenance del movimiento generado.
  (`ExpenseSchemaTests`, `RecurringRuleRepositoryTests.GeneratedExpense_RoundTripsProvenanceAndPeriod`,
  migración `20260912150000_AddRecurringExpenseProvenance`.)

## Use cases

- [x] 2.1 Completar create/update de `Subscription`; delete queda cubierto por el repositorio.
- [x] 2.2 Implementar create/update/delete de `FixedExpense`.
  (`FixedExpenseUseCaseTests`, `RecurringLifecycleUseCaseTests`.)
- [x] 2.3 Implementar skip y edición de movimientos generados.
  (`SkipNextOccurrenceUseCase` + `GeneratedExpenseTests.Update_ChangesMovementWithoutMutatingProvenance`
  confirma que editar el `Expense` generado no muta la `RecurringRule` origen.)

## Generación

- [x] 3.1 Crear preview de ocurrencias usando `IClock`.
- [x] 3.2 Crear confirmación idempotente por regla y periodo.
  (`GenerateDueRecurringUseCaseTests.PreviewAndConfirm_CreateProvenanceLinkedExpenseOnce`
  confirma que una segunda confirmación no duplica.)
- [x] 3.3 Cubrir mensual, bimestral, anual, fin de mes, leap year, inactive,
  ended y duplicate. (16 casos en `RecurringOccurrenceCalculatorTests`, ≥ el mínimo de 10 del spec;
  inactive/ended cubiertos en `GenerateDueRecurringUseCaseTests.Preview_ExcludesInactiveAndEndedRules`.)

## UI y cierre

- [x] 4.1 Crear lista y formularios de reglas recurrentes.
  (`RecurringRulesView`/`RecurringRulesViewModel` soporta Subscription y FixedExpense con
  crear/editar/desactivar/eliminar/saltar; `RecurringRulesViewModelTests` cubre el selector de tipo.)
- [x] 4.2 Crear pantalla "Generar del mes" con preview y confirmación.
  (Sección integrada en `RecurringRulesView`: `PreviewGenerationCommand` + `ConfirmGenerationCommand`.)
- [x] 4.3 Añadir localización, estados vacíos y errores como valores.
  (Estado vacío de la lista de reglas; `Result.Failure` en todos los use cases; strings en español
  siguiendo el mismo patrón hardcoded que `ExpensesView`/`IncomeView` — la migración a `IStringLocalizer`
  es deuda pre-existente de toda la UI, no específica de esta feature, diferida a Fase 12.)
- [x] 4.4 Ejecutar build, tests, format, NetArchTest y smoke test.
  (`dotnet build` 0 warnings/errores; `dotnet test` 614/614 verdes (492 unit + 122 integration);
  `dotnet format style --verify-no-changes` limpio; NetArchTest 11/11.)
- [x] 4.5 Actualizar plan de Phase 6 y archivar el ciclo SDD.
