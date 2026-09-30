using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Application.UseCases.Import;
using ControlDeFinanzasIA.Application.UseCases.Import.CommitImport;
using ControlDeFinanzasIA.Application.UseCases.Import.PreviewImport;
using ControlDeFinanzasIA.Infrastructure.Import;
using ControlDeFinanzasIA.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ControlDeFinanzasIA.Tests.Integration.Import;

public sealed class ImportServiceCollectionExtensionsTests
{
    [Fact]
    public void AddImportReaders_RegistersCsvTableReaderAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddImportReaders();

        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);

        Assert.Same(provider.GetRequiredService<CsvTableReader>(), provider.GetRequiredService<CsvTableReader>());
    }

    [Fact]
    public void AddImportReaders_CalledTwice_IsIdempotent()
    {
        var services = new ServiceCollection();
        services.AddImportReaders();
        int first = services.Count;
        services.AddImportReaders();

        Assert.Equal(first, services.Count);
    }

    [Fact]
    public void AddImportReaders_NullServices_Throws() =>
        Assert.Throws<ArgumentNullException>(() => ImportServiceCollectionExtensions.AddImportReaders(null!));

    [Fact]
    public void AddImport_ResolvesEveryPortAsASingletonUnderScopeValidation()
    {
        using ServiceProvider provider = BuildComposition();
        using IServiceScope scope = provider.CreateScope();

        foreach (Type port in new[] { typeof(ITabularReader), typeof(IImportLookup), typeof(IImportWriter), typeof(CsvTableReader), typeof(ClosedXmlTableReader) })
        {
            Assert.Same(provider.GetRequiredService(port), provider.GetRequiredService(port));
            Assert.Same(provider.GetRequiredService(port), scope.ServiceProvider.GetRequiredService(port));
        }

        Assert.IsType<SniffingTabularReader>(provider.GetRequiredService<ITabularReader>());
        Assert.IsType<EfImportLookup>(provider.GetRequiredService<IImportLookup>());
        Assert.IsType<EfImportWriter>(provider.GetRequiredService<IImportWriter>());
    }

    [Fact]
    public void AddImport_UseCasesResolveWithScopeValidation()
    {
        using ServiceProvider provider = BuildComposition();
        using IServiceScope scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<PreviewImportUseCase>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<CommitImportUseCase>());
        Assert.NotNull(provider.GetRequiredService<CommitImportUseCase>());
    }

    [Fact]
    public void AddImport_CalledTwice_IsIdempotent()
    {
        var services = new ServiceCollection();
        services.AddImport();
        int first = services.Count;
        services.AddImport();

        Assert.Equal(first, services.Count);
    }

    [Fact]
    public void AddImport_NullServices_Throws() =>
        Assert.Throws<ArgumentNullException>(() => ImportServiceCollectionExtensions.AddImport(null!));

    [Theory]
    [InlineData(typeof(EfImportLookup))]
    [InlineData(typeof(EfImportWriter))]
    public void ImportSingletons_HoldOnlyTheScopeFactoryAndALogger(Type implementation)
    {
        System.Reflection.ParameterInfo[] parameters = implementation.GetConstructors().Single().GetParameters();

        Assert.Contains(parameters, p => p.ParameterType == typeof(IServiceScopeFactory));
        Assert.All(parameters, p => Assert.True(
            p.ParameterType == typeof(IServiceScopeFactory) || (p.ParameterType.IsGenericType && p.ParameterType.GetGenericTypeDefinition() == typeof(ILogger<>)),
            $"{implementation.Name} captures {p.ParameterType.Name}"));
    }

    private static ServiceProvider BuildComposition()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPersistence(new UnusedPaths());
        services.AddSingleton<IClock>(new ImportTestClock(new DateOnly(2026, 9, 30)));
        services.AddImport();
        services.AddTransient<ImportPipeline>();
        services.AddTransient<PreviewImportUseCase>();
        services.AddTransient<CommitImportUseCase>();

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    private sealed class UnusedPaths : IPathResolver
    {
        public string DataDirectory => Path.Combine(Path.GetTempPath(), "import-composition-unused");

        public string DatabaseFilePath => Path.Combine(DataDirectory, "data.db");
    }
}
