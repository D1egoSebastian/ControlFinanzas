using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Packaging;

/// <summary>
/// The Windows executable must be a GUI-subsystem app (QA D4): a console-subsystem apphost opens a console window next
/// to the UI and closing that console kills the app. The built apphost's PE header is inspected directly.
/// </summary>
public sealed class WindowsSubsystemTests
{
    private const ushort _imageSubsystemWindowsGui = 2;

    [Fact]
    public void BuiltWindowsApphost_IsAGuiSubsystemExecutable()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string exe = Path.Combine(AppContext.BaseDirectory, "ControlDeFinanzasIA.UI.exe");
        Assert.True(File.Exists(exe), "The built UI apphost must be next to the test assembly: " + exe);

        Assert.Equal(_imageSubsystemWindowsGui, ReadSubsystem(exe));
    }

    private static ushort ReadSubsystem(string path)
    {
        using var reader = new BinaryReader(File.OpenRead(path));
        reader.BaseStream.Position = 0x3C;
        int peOffset = reader.ReadInt32();

        // PE signature (4) + COFF header (20) + optional header offset 68 holds the Subsystem field.
        reader.BaseStream.Position = peOffset + 4 + 20 + 68;
        return reader.ReadUInt16();
    }
}
