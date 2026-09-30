using ControlDeFinanzasIA.Infrastructure.Import;
using Microsoft.Extensions.DependencyInjection;

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
}
