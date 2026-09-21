using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Infrastructure.Persistence.Repositories;
using ControlDeFinanzasIA.Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlDeFinanzasIA.Infrastructure.Persistence;

/// <summary>
/// Métodos de extensión para registrar los servicios de persistencia en
/// el contenedor de inyección de dependencias.
/// </summary>
/// <remarks>
/// <para>
/// Véase <c>docs/architecture.md</c> §8 y §9 para la separación entre la
/// capa de Infrastructure y el composition root.
/// </para>
    /// <para>
    /// Según la especificación de F-001 (RN-4), este método NO registra
    /// <see cref="IPathResolver"/>: esa responsabilidad corresponde a
    /// <c>Bootstrap.ConfigureServices</c> de F-008 de la Fase 1. El caller
    /// resuelve <see cref="IPathResolver"/> y lo pasa como parámetro.
    /// </para>
    /// <para>
    /// Según la especificación de F-004 de la Fase 2, este método también
    /// registra <see cref="ICategoryRepository"/> e <see cref="IPaymentMethodRepository"/>.
    /// </para>
    /// <para>
    /// Según la especificación de F-003 de la Fase 3, este método también
    /// registra <see cref="IExpenseRepository"/>.
    /// </para>
    /// <para>
    /// Según la especificación de F-002 de la Fase 4, este método también
    /// registra <see cref="IIncomeRepository"/>.
    /// </para>
    /// <para>
    /// Según la especificación de F-004 de la Fase 4, este método también
    /// registra <see cref="ITagRepository"/>.
    /// </para>
    /// <para>
    /// Según la especificación de F-006 de la Fase 4, este método también
    /// registra <see cref="ISavedViewRepository"/>.
    /// </para>
    /// </remarks>
    public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra <see cref="AppDbContext"/>, <see cref="IUnitOfWork"/>,
    /// <see cref="ICategoryRepository"/>, <see cref="IPaymentMethodRepository"/>,
    /// <see cref="IExpenseRepository"/>, <see cref="IIncomeRepository"/>,
    /// <see cref="ITagRepository"/> y <see cref="ISavedViewRepository"/> en el
    /// contenedor de DI usando SQLite como proveedor de base de datos.
    /// </summary>
    /// <param name="services">Colección de servicios de DI.</param>
    /// <param name="pathResolver">
    /// Resolvedor de rutas que provee la ubicación del archivo de base de
    /// datos. Debe provenir del composition root.
    /// </param>
    /// <returns>La misma <see cref="IServiceCollection"/> para encadenar.</returns>
    /// <exception cref="ArgumentNullException">
    /// Se lanza si <paramref name="services"/> o
    /// <paramref name="pathResolver"/> son <see langword="null"/>.
    /// </exception>
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IPathResolver pathResolver)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(pathResolver);

        services.AddDbContext<AppDbContext>(opts =>
            opts.UseSqlite($"Data Source={pathResolver.DatabaseFilePath}"));
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IPaymentMethodRepository, PaymentMethodRepository>();
        services.AddScoped<IExpenseRepository, ExpenseRepository>();
        services.AddScoped<IIncomeRepository, IncomeRepository>();
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<ISavedViewRepository, SavedViewRepository>();
        services.AddScoped<IRecurringRuleRepository, RecurringRuleRepository>();
        services.AddScoped<IDebtRepository, DebtRepository>();
        services.AddScoped<IDebtPaymentRepository, DebtPaymentRepository>();
        services.AddScoped<IBudgetRepository, BudgetRepository>();
        services.AddScoped<IGoalRepository, GoalRepository>();
        services.AddScoped<IGoalContributionRepository, GoalContributionRepository>();
        services.AddScoped<ISettingRepository, SettingRepository>();
        services.AddScoped<ISettingsStore, SettingsStore>();

        return services;
    }
}
