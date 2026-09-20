using ControlDeFinanzasIA.Domain.Budgets;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.Debts;
using ControlDeFinanzasIA.Domain.Recurring;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.Views;
using Microsoft.EntityFrameworkCore;

namespace ControlDeFinanzasIA.Infrastructure.Persistence;

/// <summary>
/// Contexto principal de Entity Framework Core y única puerta de entrada
/// a la base de datos SQLite de la aplicación. Expone los conjuntos de
/// entidades <see cref="Category"/>, <see cref="PaymentMethod"/>,
/// <see cref="Expense"/>, <see cref="Income"/>, <see cref="Tag"/> y
/// <see cref="SavedView"/>.
/// </summary>
/// <remarks>
/// <para>
/// Véase <c>docs/architecture.md</c> §8 para la ubicación de la capa de
/// Infrastructure y la sección "Persistencia".
/// </para>
/// <para>
/// Según la especificación de F-001 (RN-1), este es el único
/// <see cref="DbContext"/> del proyecto en v1. Cualquier acceso a datos
/// pasa por este contexto, bien directamente o a través de
/// <see cref="IUnitOfWork"/>.
/// </para>
/// <para>
/// El contexto se registra como Scoped (RN-2) mediante
/// <see cref="ServiceCollectionExtensions.AddPersistence"/>.
/// </para>
/// </remarks>
public class AppDbContext : DbContext
{
    /// <summary>
    /// Inicializa una nueva instancia del <see cref="AppDbContext"/>.
    /// </summary>
    /// <param name="options">Opciones de configuración del contexto.</param>
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Conjunto de entidades <see cref="Category"/> mapeado a la tabla
    /// correspondiente en SQLite.
    /// </summary>
    public DbSet<Category> Categories => Set<Category>();

    /// <summary>
    /// Conjunto de entidades <see cref="PaymentMethod"/> mapeado a la
    /// tabla correspondiente en SQLite.
    /// </summary>
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();

    /// <summary>
    /// Conjunto de entidades <see cref="Expense" /> mapeado a la tabla
    /// <c>Expenses</c> en SQLite.
    /// </summary>
    public DbSet<Expense> Expenses => Set<Expense>();

    /// <summary>
    /// Conjunto de entidades <see cref="Income" /> mapeado a la tabla
    /// <c>Incomes</c> en SQLite.
    /// </summary>
    public DbSet<Income> Incomes => Set<Income>();

    /// <summary>
    /// Conjunto de entidades <see cref="Tag" /> mapeado a la tabla
    /// <c>Tags</c> en SQLite.
    /// </summary>
    public DbSet<Tag> Tags => Set<Tag>();

    /// <summary>
    /// Conjunto de entidades <see cref="SavedView" /> mapeado a la tabla
    /// <c>SavedViews</c> en SQLite.
    /// </summary>
    public DbSet<SavedView> SavedViews => Set<SavedView>();

    /// <summary>Conjunto de reglas recurrentes.</summary>
    public DbSet<RecurringRule> RecurringRules => Set<RecurringRule>();

    /// <summary>Conjunto de deudas con plan de cuotas.</summary>
    public DbSet<Debt> Debts => Set<Debt>();

    /// <summary>Conjunto de cuotas de deudas.</summary>
    public DbSet<DebtPayment> DebtPayments => Set<DebtPayment>();

    /// <summary>Conjunto de presupuestos mensuales por categoria.</summary>
    public DbSet<Budget> Budgets => Set<Budget>();

    /// <summary>
    /// Configura el modelo de EF Core aplicando las configuraciones de
    /// entidades del assembly actual y los datos iniciales del sistema.
    /// </summary>
    /// <param name="modelBuilder">Constructor del modelo de EF Core.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RecurringRule>();
        modelBuilder.Entity<Subscription>();
        modelBuilder.Entity<FixedExpense>();
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        SeedData.Apply(modelBuilder);
    }
}
