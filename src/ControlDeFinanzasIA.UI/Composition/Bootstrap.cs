using System.Reflection;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.UseCases.Budgets.CreateOrUpdateBudget;
using ControlDeFinanzasIA.Application.UseCases.Budgets.DeleteBudget;
using ControlDeFinanzasIA.Application.UseCases.Budgets.GetBudgetsForMonth;
using ControlDeFinanzasIA.Application.UseCases.Budgets.GetBudgetVsActual;
using ControlDeFinanzasIA.Application.UseCases.Calendar.GetCalendarMonth;
using ControlDeFinanzasIA.Application.UseCases.Catalog.CreateTag;
using ControlDeFinanzasIA.Application.UseCases.Catalog.GetPaymentMethods;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetCategoryBreakdown;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthlyEvolution;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetMonthSummary;
using ControlDeFinanzasIA.Application.UseCases.Dashboard.GetUpcomingPayments;
using ControlDeFinanzasIA.Application.UseCases.Debts.CreateDebtWithSchedule;
using ControlDeFinanzasIA.Application.UseCases.Debts.GetDebts;
using ControlDeFinanzasIA.Application.UseCases.Debts.GetDebtSchedule;
using ControlDeFinanzasIA.Application.UseCases.Debts.RegisterDebtPayment;
using ControlDeFinanzasIA.Application.UseCases.Debts.SkipDebtPayment;
using ControlDeFinanzasIA.Application.UseCases.Expenses.GetTransactions;
using ControlDeFinanzasIA.Application.UseCases.Expenses.RegisterExpense;
using ControlDeFinanzasIA.Application.UseCases.Expenses.UpdateExpense;
using ControlDeFinanzasIA.Application.UseCases.Incomes.GetIncomes;
using ControlDeFinanzasIA.Application.UseCases.Incomes.RegisterIncome;
using ControlDeFinanzasIA.Application.UseCases.Incomes.UpdateIncome;
using ControlDeFinanzasIA.Application.UseCases.Recurring.CreateFixedExpense;
using ControlDeFinanzasIA.Application.UseCases.Recurring.CreateSubscription;
using ControlDeFinanzasIA.Application.UseCases.Recurring.DeactivateRecurringRule;
using ControlDeFinanzasIA.Application.UseCases.Recurring.DeleteRecurringRule;
using ControlDeFinanzasIA.Application.UseCases.Recurring.GenerateDueRecurring;
using ControlDeFinanzasIA.Application.UseCases.Recurring.SkipNextOccurrence;
using ControlDeFinanzasIA.Application.UseCases.Recurring.UpdateFixedExpense;
using ControlDeFinanzasIA.Application.UseCases.Recurring.UpdateSubscription;
using ControlDeFinanzasIA.Application.UseCases.SavedViews.ApplySavedView;
using ControlDeFinanzasIA.Application.UseCases.SavedViews.DeleteSavedView;
using ControlDeFinanzasIA.Application.UseCases.SavedViews.GetSavedViews;
using ControlDeFinanzasIA.Application.UseCases.SavedViews.SaveCurrentFilter;
using ControlDeFinanzasIA.Application.UseCases.Transactions.DeleteTransaction;
using ControlDeFinanzasIA.Infrastructure.ExchangeRates;
using ControlDeFinanzasIA.Infrastructure.Persistence;
using ControlDeFinanzasIA.UI.Navigation;
using ControlDeFinanzasIA.UI.System;
using ControlDeFinanzasIA.UI.ViewModels;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.UI.Composition;

/// <summary>
/// Composition root of the application.
/// </summary>
/// <remarks>
/// Annotated with <see cref="CompositionRootAttribute"/> to document that
/// this is the only place where the UI layer wires concrete implementations
/// to the abstractions defined in <c>Application</c>. The 15 repository
/// interfaces are intentionally <strong>not</strong> registered here; their
/// concrete implementations with EF Core + SQLite will be registered in
/// F-002 of Phase 2. The <paramref name="configuration"/> parameter is
/// accepted for forward compatibility with F-002 and F-012.
/// </remarks>
[CompositionRoot]
public static class Bootstrap
{
    /// <summary>
    /// Registers the application's services in the given
    /// <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <param name="configuration">
    /// Application configuration. Reserved for future use in F-002 and F-012.
    /// </param>
    /// <remarks>
    /// Also registers the expense and income registration and update use cases,
    /// the transaction deletion use case, the income query (<see cref="GetIncomesQuery"/>),
    /// the tag creation use case (<see cref="CreateTagUseCase"/>) and its validator
    /// (<see cref="CreateTagValidator"/>), the saved-view use cases
    /// (<see cref="SaveCurrentFilterUseCase"/>, <see cref="ApplySavedViewUseCase"/>,
    /// <see cref="GetSavedViewsUseCase"/>, <see cref="DeleteSavedViewUseCase"/>) and their validator
    /// (<see cref="SaveCurrentFilterValidator"/>), plus the validators for register
    /// and update of expenses and incomes, plus persistence (<see cref="AppDbContext"/>,
    /// <see cref="IUnitOfWork"/>, <see cref="ICategoryRepository"/>,
    /// <see cref="IPaymentMethodRepository"/>, <see cref="ISavedViewRepository"/>) via
    /// <see cref="ServiceCollectionExtensions.AddPersistence(IServiceCollection, IPathResolver)"/>.
    /// </remarks>
    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        _ = configuration;

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPathResolver, SystemPathResolver>();

        services.AddLocalization();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<ShellViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<ExpensesViewModel>();
        services.AddTransient<IncomeViewModel>();
        services.AddTransient<RecurringRulesViewModel>();
        services.AddTransient<DebtsViewModel>();
        services.AddTransient<CalendarViewModel>();

        services.AddTransient<RegisterExpenseUseCase>();
        services.AddTransient<UpdateExpenseUseCase>();
        services.AddTransient<RegisterIncomeUseCase>();
        services.AddTransient<UpdateIncomeUseCase>();
        services.AddTransient<CreateSubscriptionUseCase>();
        services.AddTransient<UpdateSubscriptionUseCase>();
        services.AddTransient<CreateFixedExpenseUseCase>();
        services.AddTransient<UpdateFixedExpenseUseCase>();
        services.AddTransient<GenerateDueRecurringUseCase>();
        services.AddTransient<SkipNextOccurrenceUseCase>();
        services.AddTransient<DeactivateRecurringRuleUseCase>();
        services.AddTransient<DeleteRecurringRuleUseCase>();
        services.AddTransient<IValidator<CreateFixedExpenseCommand>, CreateFixedExpenseValidator>();
        services.AddTransient<IValidator<UpdateFixedExpenseCommand>, UpdateFixedExpenseValidator>();
        services.AddTransient<IValidator<CreateSubscriptionCommand>, CreateSubscriptionValidator>();
        services.AddTransient<IValidator<UpdateSubscriptionCommand>, UpdateSubscriptionValidator>();
        services.AddTransient<CreateDebtWithScheduleUseCase>();
        services.AddTransient<RegisterDebtPaymentUseCase>();
        services.AddTransient<SkipDebtPaymentUseCase>();
        services.AddTransient<GetDebtsQuery>();
        services.AddTransient<GetDebtScheduleQuery>();
        services.AddTransient<IValidator<CreateDebtWithScheduleCommand>, CreateDebtWithScheduleValidator>();
        services.AddTransient<DeleteTransactionUseCase>();
        services.AddTransient<GetTransactionsQuery>();
        services.AddTransient<IValidator<RegisterExpenseCommand>, RegisterExpenseValidator>();
        services.AddTransient<IValidator<UpdateExpenseCommand>, UpdateExpenseValidator>();
        services.AddTransient<IValidator<RegisterIncomeCommand>, RegisterIncomeValidator>();
        services.AddTransient<IValidator<UpdateIncomeCommand>, UpdateIncomeValidator>();

        services.AddTransient<SaveCurrentFilterUseCase>();
        services.AddTransient<ApplySavedViewUseCase>();
        services.AddTransient<GetSavedViewsUseCase>();
        services.AddTransient<DeleteSavedViewUseCase>();
        services.AddTransient<IValidator<SaveCurrentFilterCommand>, SaveCurrentFilterValidator>();

        services.AddTransient<GetIncomesQuery>();
        services.AddTransient<CreateTagUseCase>();
        services.AddTransient<IValidator<CreateTagCommand>, CreateTagValidator>();
        services.AddTransient<GetPaymentMethodsQuery>();

        // Dashboard (F-008 Phase 8): the 4 dashboard queries.
        services.AddTransient<GetMonthSummaryQuery>();
        services.AddTransient<GetCategoryBreakdownQuery>();
        services.AddTransient<GetMonthlyEvolutionQuery>();
        services.AddTransient<GetUpcomingPaymentsQuery>();

        // Calendario de gastos (F-009 Phase 9): grilla mensual + drill-down.
        services.AddTransient<GetCalendarMonthQuery>();

        // Presupuestos mensuales por categoría (F-010 Phase 10, slice 2):
        // los 4 use cases/queries de Application. BudgetsViewModel (slice 3)
        // se registra por separado cuando esa slice aterrice.
        services.AddTransient<CreateOrUpdateBudgetUseCase>();
        services.AddTransient<DeleteBudgetUseCase>();
        services.AddTransient<GetBudgetsForMonthQuery>();
        services.AddTransient<GetBudgetVsActualQuery>();
        services.AddTransient<IValidator<CreateOrUpdateBudgetCommand>, CreateOrUpdateBudgetValidator>();

        // Multi-moneda F-501/F-502/F-511: provider stub + cache + converter.
        services.AddExchangeRates(configuration);

        ServiceProvider tempProvider = services.BuildServiceProvider();
        try
        {
            IPathResolver pathResolver = tempProvider.GetRequiredService<IPathResolver>();
            services.AddPersistence(pathResolver);
        }
        finally
        {
            tempProvider.Dispose();
        }
    }

    /// <summary>
    /// Applies pending EF Core migrations using a new service scope.
    /// </summary>
    /// <param name="services">The application's root service provider.</param>
    /// <remarks>
    /// This method is intentionally part of the composition root so that
    /// the UI layer does not directly reference Entity Framework Core types
    /// outside of <see cref="Bootstrap"/>, preserving the R-4 NetArchTest
    /// boundary. The actual migration call is performed via reflection to
    /// avoid a compile-time dependency on the EF Core package by the UI
    /// assembly.
    /// </remarks>
    public static void ApplyMigrations(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        using IServiceScope scope = services.CreateScope();

        Type appDbContextType = Type.GetType(
            "ControlDeFinanzasIA.Infrastructure.Persistence.AppDbContext, ControlDeFinanzasIA.Infrastructure",
            throwOnError: true)!;

        object db = scope.ServiceProvider.GetRequiredService(appDbContextType);

        object database = appDbContextType
            .GetProperty("Database", BindingFlags.Public | BindingFlags.Instance)!
            .GetValue(db)!;

        Type extensionsType = Type.GetType(
            "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions, Microsoft.EntityFrameworkCore.Relational",
            throwOnError: true)!;

        MethodInfo migrateMethod = extensionsType
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == "Migrate" && m.GetParameters().Length == 1)
            .Single();

        migrateMethod.Invoke(null, new[] { database });

        ILoggerFactory loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        ILogger logger = loggerFactory.CreateLogger("Bootstrap");
        logger.LogInformation("Migrations applied");
    }
}
