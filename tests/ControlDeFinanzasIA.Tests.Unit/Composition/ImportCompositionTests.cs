using System.Reflection;
using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.UseCases.Import;
using ControlDeFinanzasIA.Application.UseCases.Import.CommitImport;
using ControlDeFinanzasIA.Application.UseCases.Import.PreviewImport;
using ControlDeFinanzasIA.Infrastructure.Import;
using ControlDeFinanzasIA.UI.Composition;
using ControlDeFinanzasIA.UI.Services;
using ControlDeFinanzasIA.UI.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Composition;

/// <summary>
/// Verifies the Phase 20 DI block of <see cref="Bootstrap"/> (F-020, S6b, DD-15) with the real composition
/// root and scope validation on: nothing singleton captures a scoped service, and no test opens the
/// user's real database (only objects that do not touch it are resolved).
/// </summary>
public sealed class ImportCompositionTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        Bootstrap.ConfigureServices(services, new ConfigurationBuilder().Build());
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = false });
    }

    [Fact]
    public void ImportPorts_ResolveFromTheRoot_AsTheRealImplementations()
    {
        using ServiceProvider provider = BuildProvider();

        Assert.IsType<SniffingTabularReader>(provider.GetRequiredService<ITabularReader>());
        Assert.IsType<EfImportLookup>(provider.GetRequiredService<IImportLookup>());
        Assert.IsType<EfImportWriter>(provider.GetRequiredService<IImportWriter>());
    }

    [Fact]
    public void ImportUseCasesAndPipeline_ResolveFromTheRoot_UnderScopeValidation_AsTransients()
    {
        using ServiceProvider provider = BuildProvider();

        Assert.NotNull(provider.GetRequiredService<ImportPipeline>());
        PreviewImportUseCase preview = provider.GetRequiredService<PreviewImportUseCase>();
        CommitImportUseCase commit = provider.GetRequiredService<CommitImportUseCase>();

        Assert.NotSame(preview, provider.GetRequiredService<PreviewImportUseCase>());
        Assert.NotSame(commit, provider.GetRequiredService<CommitImportUseCase>());
    }

    [Fact]
    public void ImportViewModel_IsTransient_SoNoRetainedFileOutlivesTheScreenThatOwnsIt()
    {
        using ServiceProvider provider = BuildProvider();

        ImportViewModel first = provider.GetRequiredService<ImportViewModel>();
        ImportViewModel second = provider.GetRequiredService<ImportViewModel>();

        Assert.NotSame(first, second);
        Assert.False(first.HasSource);
        Assert.Equal(ImportPanelState.Idle, first.State);
    }

    [Fact]
    public void ImportViewModel_UsesThePhase19OpenDialogSingleton()
    {
        using ServiceProvider provider = BuildProvider();

        Assert.IsType<AvaloniaFileOpenDialog>(provider.GetRequiredService<IFileOpenDialog>());
        Assert.Same(
            provider.GetRequiredService<IFileOpenDialog>(),
            provider.GetRequiredService<IFileOpenDialog>());
    }

    [Fact]
    public void ExpensesViewModel_CanReceiveTheImportViewModel_FromTheContainer()
    {
        using ServiceProvider provider = BuildProvider();
        IServiceProviderIsService isService = provider.GetRequiredService<IServiceProviderIsService>();

        ParameterInfo importParameter = Assert.Single(
            typeof(ExpensesViewModel).GetConstructors().Single().GetParameters(),
            p => p.ParameterType == typeof(ImportViewModel));

        // Resolving ExpensesViewModel itself would start loads against the real profile database.
        Assert.True(isService.IsService(importParameter.ParameterType));
        Assert.True(importParameter.HasDefaultValue);
    }

    [Fact]
    public void WholeContainer_BuildsWithScopeAndBuildValidation_IncludingTheImportGraph()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        Bootstrap.ConfigureServices(services, new ConfigurationBuilder().Build());

        using ServiceProvider provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        Assert.NotNull(provider.GetRequiredService<ImportViewModel>());
    }
}
