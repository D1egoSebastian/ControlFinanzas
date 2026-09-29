using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.UseCases.Backup.CreateBackup;
using ControlDeFinanzasIA.Application.UseCases.Backup.InspectBackup;
using ControlDeFinanzasIA.Application.UseCases.Backup.RestoreBackup;
using ControlDeFinanzasIA.Infrastructure.Backup;
using ControlDeFinanzasIA.UI.Composition;
using ControlDeFinanzasIA.UI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Composition;

/// <summary>
/// Verifies the Phase 19 DI block of <see cref="Bootstrap"/> (F-019, S5, DD-10) with the real
/// composition root: scope validation on, the singleton backup service captures no scoped service.
/// </summary>
public sealed class BackupCompositionTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        Bootstrap.ConfigureServices(services, new ConfigurationBuilder().Build());
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = false });
    }

    [Fact]
    public void BackupServiceAndCatalog_ResolveFromRoot_AsTheRealImplementations()
    {
        using ServiceProvider provider = BuildProvider();

        IBackupService service = provider.GetRequiredService<IBackupService>();

        Assert.IsType<SqliteBackupService>(service);
        Assert.Same(service, provider.GetRequiredService<IBackupService>());
        Assert.IsType<EfMigrationCatalog>(provider.GetRequiredService<IMigrationCatalog>());
    }

    [Fact]
    public void FileOpenDialog_IsTheAvaloniaImplementation_AsASingleton()
    {
        using ServiceProvider provider = BuildProvider();

        IFileOpenDialog first = provider.GetRequiredService<IFileOpenDialog>();

        Assert.IsType<AvaloniaFileOpenDialog>(first);
        Assert.Same(first, provider.GetRequiredService<IFileOpenDialog>());
    }

    [Fact]
    public void AppRestartService_IsTheRealImplementation_AsASingleton()
    {
        using ServiceProvider provider = BuildProvider();

        IAppRestartService first = provider.GetRequiredService<IAppRestartService>();

        Assert.IsType<AppRestartService>(first);
        Assert.Same(first, provider.GetRequiredService<IAppRestartService>());
    }

    [Fact]
    public void BackupUseCases_ResolveInsideAScope_AsTransients()
    {
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        Assert.NotNull(services.GetRequiredService<CreateBackupUseCase>());
        Assert.NotNull(services.GetRequiredService<InspectBackupUseCase>());
        Assert.NotNull(services.GetRequiredService<RestoreBackupUseCase>());
        Assert.NotSame(
            services.GetRequiredService<RestoreBackupUseCase>(),
            services.GetRequiredService<RestoreBackupUseCase>());
    }

    [Fact]
    public void BackupServices_ResolveFromTheRoot_UnderScopeValidation()
    {
        using ServiceProvider provider = BuildProvider();

        // ValidateScopes = true: a captured scoped dependency would throw here.
        Assert.NotNull(provider.GetRequiredService<SqliteBackupService>());
        Assert.NotNull(provider.GetRequiredService<CreateBackupUseCase>());
    }

    [Fact]
    public void WholeContainer_BuildsWithScopeAndBuildValidation()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        Bootstrap.ConfigureServices(services, new ConfigurationBuilder().Build());

        using ServiceProvider provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        Assert.NotNull(provider.GetRequiredService<IBackupService>());
    }
}
