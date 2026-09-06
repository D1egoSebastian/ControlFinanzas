using ControlDeFinanzasIA.Application.Abstractions;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>
/// Tests para <see cref="FakePathResolver"/>. Verifican que el fake
/// permite controlar las rutas explícitamente, y que retorna
/// exactamente lo que se le setea.
/// </summary>
public class FakePathResolverTests
{
    [Fact]
    public void FakePathResolver_DefaultConstruction_ReturnsEmptyStrings()
    {
        var fake = new FakePathResolver();

        Assert.Equal(string.Empty, fake.DataDirectory);
        Assert.Equal(string.Empty, fake.DatabaseFilePath);
    }

    [Fact]
    public void FakePathResolver_SetDataDirectory_ReturnsSetValue()
    {
        var fake = new FakePathResolver();
        string expected = "/home/user/.config/app";

        fake.DataDirectory = expected;

        Assert.Equal(expected, fake.DataDirectory);
    }

    [Fact]
    public void FakePathResolver_SetDatabaseFilePath_ReturnsSetValue()
    {
        var fake = new FakePathResolver();
        string expected = "/home/user/.config/app/data.db";

        fake.DatabaseFilePath = expected;

        Assert.Equal(expected, fake.DatabaseFilePath);
    }

    [Fact]
    public void FakePathResolver_SetEmptyStrings_AcceptsEmptyValues()
    {
        var fake = new FakePathResolver
        {
            DataDirectory = string.Empty,
            DatabaseFilePath = string.Empty
        };

        Assert.Equal(string.Empty, fake.DataDirectory);
        Assert.Equal(string.Empty, fake.DatabaseFilePath);
    }

    [Fact]
    public void FakePathResolver_SetPathWithSpaces_PreservesValue()
    {
        var fake = new FakePathResolver();
        string expected = "/path with spaces/app data";

        fake.DataDirectory = expected;

        Assert.Equal(expected, fake.DataDirectory);
    }

    [Fact]
    public void FakePathResolver_SetPathWithSpecialCharacters_PreservesValue()
    {
        var fake = new FakePathResolver();
        string expected = "/path/with/../special~chars";

        fake.DataDirectory = expected;

        Assert.Equal(expected, fake.DataDirectory);
    }
}
