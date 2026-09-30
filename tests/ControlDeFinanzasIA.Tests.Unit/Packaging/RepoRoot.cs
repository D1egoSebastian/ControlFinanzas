namespace ControlDeFinanzasIA.Tests.Unit.Packaging;

/// <summary>Locates the repository root by walking up to the solution file.</summary>
internal static class RepoRoot
{
    private static readonly Lazy<string> _path = new(Find);

    public static string Path => _path.Value;

    public static string Combine(params string[] parts) =>
        System.IO.Path.Combine([Path, .. parts]);

    private static string Find()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(System.IO.Path.Combine(dir.FullName, "ControlDeFinanzasIA.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root (ControlDeFinanzasIA.sln) not found.");
    }
}
