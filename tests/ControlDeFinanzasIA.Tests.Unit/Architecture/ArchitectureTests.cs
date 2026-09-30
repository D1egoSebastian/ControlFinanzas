using System.Reflection;
using System.Xml.Linq;
using NetArchTest.Rules;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Architecture;

/// <summary>
/// Tests arquitectónicos que validan el grafo de dependencias
/// entre las 4 capas del proyecto. Las reglas verificadas aquí
/// están definidas en <c>docs/architecture.md</c> §4 y
/// especificadas en
/// <c>spec/features/phase-01/F-007-validacion-arquitectonica.done.md</c>.
/// </summary>
/// <remarks>
/// Cualquier violación debe detectarse en este test antes de
/// llegar a code review o a producción. Si una regla no se
/// valida aquí, es un agujero en la red de seguridad del
/// proyecto.
/// <para>
/// Verificación con violaciones intencionales (T-008):
/// <list type="bullet">
/// <item>
/// Domain → Application: R-1 detectó el tipo
/// <c>ControlDeFinanzasIA.Domain.ValueObjects.Money</c> como violador.
/// </item>
/// <item>
/// Domain → EF Core: R-1 (paquetes prohibidos) detectó el tipo
/// <c>ControlDeFinanzasIA.Domain.ValueObjects.Money</c> como violador.
/// </item>
/// <item>
/// Application → Infrastructure: R-2 detectó el tipo
/// <c>ControlDeFinanzasIA.Application.ArchitectureProbe</c> como violador.
/// </item>
/// <item>
/// UI → Infrastructure: R-4 detectó el tipo
/// <c>ControlDeFinanzasIA.UI.App</c> como violador.
/// </item>
/// </list>
/// En todos los casos revertir la violación devolvió el test a verde.
/// </para>
/// </remarks>
public sealed class ArchitectureTests
{
    // Assembly names are obtained from the compiled projects.
    // Domain and Application are referenced directly by Tests.Unit.
    // Infrastructure, UI and Tests.Integration are loaded from the
    // build output so the rules can inspect them without adding
    // project references that would alter the architecture.
    private static readonly Assembly _domainAssembly = typeof(ControlDeFinanzasIA.Domain.ValueObjects.Money).Assembly;
    private static readonly Assembly _applicationAssembly = typeof(ControlDeFinanzasIA.Application.Abstractions.IUnitOfWork).Assembly;
    private static readonly Assembly _infrastructureAssembly = LoadProjectAssembly(@"src\ControlDeFinanzasIA.Infrastructure", "ControlDeFinanzasIA.Infrastructure");
    private static readonly Assembly _uiAssembly = LoadProjectAssembly(@"src\ControlDeFinanzasIA.UI", "ControlDeFinanzasIA.UI");
    private static readonly Assembly _testsUnitAssembly = typeof(ArchitectureTests).Assembly;
    private static readonly Assembly _testsIntegrationAssembly = LoadProjectAssembly(@"tests\ControlDeFinanzasIA.Tests.Integration", "ControlDeFinanzasIA.Tests.Integration");

    private static readonly string _domainName = _domainAssembly.GetName().Name!;
    private static readonly string _applicationName = _applicationAssembly.GetName().Name!;
    private static readonly string _infrastructureName = _infrastructureAssembly.GetName().Name!;
    private static readonly string _uiName = _uiAssembly.GetName().Name!;
    private static readonly string _testsUnitName = _testsUnitAssembly.GetName().Name!;
    private static readonly string _testsIntegrationName = _testsIntegrationAssembly.GetName().Name!;

    private static readonly string[] _forbiddenPackagesForDomainAndApplication =
    [
        "Microsoft.EntityFrameworkCore",
        "Avalonia.Controls",
        "System.Net.Http",

        // F-018 (slice S2, D-9): report libraries are Infrastructure-only.
        "QuestPDF",
        "ClosedXML",
        "DocumentFormat.OpenXml",

        // F-019 (slice S5, D-20): SQLite access is Infrastructure-only.
        "Microsoft.Data.Sqlite"
    ];

    private static readonly string[] _forbiddenPackagesForUi =
    [
        "Microsoft.EntityFrameworkCore",
        "System.Net.Http",

        // F-013 (slice 4, D-9): Bootstrap.cs may only call the
        // Infrastructure-owned AddLlmClients() extension, which returns
        // IServiceCollection. A stray AddHttpClient/IHttpClientBuilder
        // call in Bootstrap.cs (or any other UI type) would put a
        // Microsoft.Extensions.Http type in UI IL — this is exactly the
        // mistake R4_UI_No_Depende_De_Paquetes_Prohibidos_EfCore_HttpClient
        // exists to catch, extended to also catch it here.
        "Microsoft.Extensions.Http",

        // F-018 (slice S2, D-9): report libraries are Infrastructure-only.
        "QuestPDF",
        "ClosedXML",
        "DocumentFormat.OpenXml",

        // F-019 (slice S5, D-20): SQLite access is Infrastructure-only.
        "Microsoft.Data.Sqlite"
    ];

    /// <summary>
    /// R-1: <c>Domain</c> no referencia a ningún otro proyecto del repo
    /// (Application, Infrastructure, UI ni Tests.*).
    /// Fuentes: <c>docs/architecture.md</c> §4 — "Domain no referencia a nadie
    /// (excepto BCL puro)"; spec F-007 §5 R-1.
    /// </summary>
    [Fact]
    public void R1_Domain_No_Referencia_A_Application_Infra_UI_o_Tests()
    {
        TestResult result = Types
            .InAssembly(_domainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(_applicationName, _infrastructureName, _uiName, _testsUnitName, _testsIntegrationName)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailingTypes(result));
    }

    /// <summary>
    /// R-1: <c>Domain</c> no depende de los paquetes prohibidos
    /// (Entity Framework Core, Avalonia ni HttpClient).
    /// Fuentes: <c>docs/architecture.md</c> §4 — "Domain no referencia a nadie
    /// (excepto BCL puro)"; spec F-007 §5 R-1.
    /// </summary>
    [Fact]
    public void R1_Domain_No_Depende_De_Paquetes_Prohibidos_EfCore_Avalonia_HttpClient()
    {
        TestResult result = Types
            .InAssembly(_domainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(_forbiddenPackagesForDomainAndApplication)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailingTypes(result));
    }

    /// <summary>
    /// R-2: <c>Application</c> no referencia a Infrastructure, UI ni Tests.*.
    /// Fuentes: <c>docs/architecture.md</c> §4 — "Application solo referencia a
    /// Domain y a BCL/paquetes neutros (FluentValidation, etc.)";
    /// spec F-007 §5 R-2.
    /// </summary>
    [Fact]
    public void R2_Application_No_Referencia_A_Infra_UI_o_Tests()
    {
        TestResult result = Types
            .InAssembly(_applicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(_infrastructureName, _uiName, _testsUnitName, _testsIntegrationName)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailingTypes(result));
    }

    /// <summary>
    /// R-2: <c>Application</c> no depende de los paquetes prohibidos
    /// (Entity Framework Core, Avalonia ni HttpClient). FluentValidation es
    /// la única dependencia externa permitida en esta capa.
    /// Fuentes: <c>docs/architecture.md</c> §4 — "Application solo referencia a
    /// Domain y a BCL/paquetes neutros (FluentValidation, etc.)";
    /// spec F-007 §5 R-2.
    /// </summary>
    [Fact]
    public void R2_Application_No_Depende_De_Paquetes_Prohibidos_EfCore_Avalonia_HttpClient()
    {
        TestResult result = Types
            .InAssembly(_applicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(_forbiddenPackagesForDomainAndApplication)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailingTypes(result));
    }

    /// <summary>
    /// R-3: <c>Infrastructure</c> depende de <c>Application</c> y de
    /// <c>Domain</c>. Sin estas dependencias no podría implementar las
    /// interfaces definidas en Application.
    /// Fuentes: <c>docs/architecture.md</c> §4 — "Infrastructure referencia
    /// Application y Domain, más paquetes externos"; spec F-007 §5 R-3.
    /// </summary>
    [Fact]
    public void R3_Infrastructure_Depende_De_Application_Y_Domain()
    {
        AssertAssemblyDependsOn(_infrastructureAssembly, _applicationName);
        AssertAssemblyDependsOn(_infrastructureAssembly, _domainName, allowTransitive: true);
    }

    /// <summary>
    /// R-3: <c>Infrastructure</c> no referencia a <c>UI</c> ni a Tests.*.
    /// Fuentes: <c>docs/architecture.md</c> §4 — "Infrastructure referencia
    /// Application y Domain, más paquetes externos"; spec F-007 §5 R-3.
    /// </summary>
    [Fact]
    public void R3_Infrastructure_No_Referencia_A_UI_o_Tests()
    {
        TestResult result = Types
            .InAssembly(_infrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(_uiName, _testsUnitName, _testsIntegrationName)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailingTypes(result));
    }

    /// <summary>
    /// R-4: <c>UI</c> depende de <c>Application</c> y de <c>Domain</c>
    /// para los ViewModels y contratos.
    /// Fuentes: <c>docs/architecture.md</c> §4 — "UI referencia Application y
    /// Domain, no Infrastructure. El composition root ... es la única
    /// excepción documentada"; spec F-007 §5 R-4.
    /// </summary>
    [Fact]
    public void R4_UI_Depende_De_Application_Y_Domain()
    {
        AssertAssemblyDependsOn(_uiAssembly, _applicationName);
        AssertAssemblyDependsOn(_uiAssembly, _domainName);
    }

    /// <summary>
    /// R-4: <c>UI</c> no referencia a <c>Infrastructure</c> salvo desde el
    /// composition root. Como F-008 aún no existe, la exclusión por nombre
    /// <c>Bootstrap</c> es trivialmente cierta (no hay ningún tipo con ese
    /// nombre). Cuando F-008 implemente el composition root, el tipo
    /// <c>Bootstrap</c> será la única excepción permitida.
    /// Fuentes: <c>docs/architecture.md</c> §4 — "UI referencia Application y
    /// Domain, no Infrastructure. El composition root ... es la única
    /// excepción documentada"; spec F-007 §5 R-4 y §10.1.
    /// </summary>
    [Fact]
    public void R4_UI_No_Referencia_A_Infrastructure_Salvo_CompositionRoot()
    {
        TestResult result = Types
            .InAssembly(_uiAssembly)
            .That()
            .DoNotHaveName("Bootstrap")
            .ShouldNot()
            .HaveDependencyOn(_infrastructureName)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailingTypes(result));
    }

    /// <summary>
    /// R-4: <c>UI</c> no depende directamente de Entity Framework Core ni de
    /// HttpClient. El acceso a base de datos y red debe ir a través de las
    /// abstracciones definidas en <c>Application</c>. Avalonia no se incluye
    /// en la lista porque es el framework legítimo de la capa UI.
    /// Fuentes: <c>docs/architecture.md</c> §4; spec F-007 §5 R-4.
    /// </summary>
    [Fact]
    public void R4_UI_No_Depende_De_Paquetes_Prohibidos_EfCore_HttpClient()
    {
        TestResult result = Types
            .InAssembly(_uiAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(_forbiddenPackagesForUi)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailingTypes(result));
    }

    /// <summary>
    /// R-5: ningún proyecto de <c>src/</c> depende de los assemblies de tests.
    /// Fuentes: <c>docs/architecture.md</c> §4 — "Tests pueden referenciar
    /// cualquier proyecto, pero nadie los referencia a ellos";
    /// spec F-007 §5 R-5.
    /// </summary>
    [Fact]
    public void R5_Ningun_Proyecto_De_src_Depende_De_Tests()
    {
        AssertNoDependency(_domainAssembly, _testsUnitName);
        AssertNoDependency(_domainAssembly, _testsIntegrationName);
        AssertNoDependency(_applicationAssembly, _testsUnitName);
        AssertNoDependency(_applicationAssembly, _testsIntegrationName);
        AssertNoDependency(_infrastructureAssembly, _testsUnitName);
        AssertNoDependency(_infrastructureAssembly, _testsIntegrationName);
        AssertNoDependency(_uiAssembly, _testsUnitName);
        AssertNoDependency(_uiAssembly, _testsIntegrationName);
    }

    /// <summary>
    /// R-5: los proyectos de tests pueden depender de los proyectos de
    /// <c>src/</c>. Este test verifica las referencias actuales para detectar
    /// borrados accidentales de <c>ProjectReference</c>.
    /// Fuentes: <c>docs/architecture.md</c> §4 — "Tests pueden referenciar
    /// cualquier proyecto, pero nadie los referencia a ellos";
    /// spec F-007 §5 R-5.
    /// </summary>
    [Fact]
    public void R5_Tests_Pueden_Depender_De_Todos_Los_Proyectos_De_src()
    {
        AssertAssemblyDependsOn(_testsUnitAssembly, _domainName);
        AssertAssemblyDependsOn(_testsUnitAssembly, _applicationName);
        AssertAssemblyDependsOn(_testsIntegrationAssembly, _infrastructureName);
    }

    private static void AssertNoDependency(Assembly sourceAssembly, string dependencyName)
    {
        TestResult result = Types
            .InAssembly(sourceAssembly)
            .ShouldNot()
            .HaveDependencyOn(dependencyName)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailingTypes(result));
    }

    private static void AssertAssemblyDependsOn(Assembly sourceAssembly, string dependencyName, bool allowTransitive = false)
    {
        // Positive dependency assertions are verified at the project-file level
        // because the .NET compiler may omit unused assembly references from the
        // compiled output. The architecture rule is about the project reference
        // graph, not about whether a type happens to use a type from the target
        // assembly today.
        string csprojPath = GetProjectFilePath(sourceAssembly);
        bool hasReference = allowTransitive
            ? HasTransitiveProjectReference(csprojPath, dependencyName)
            : HasDirectProjectReference(csprojPath, dependencyName);

        string scope = allowTransitive ? "directly or transitively" : "directly";
        Assert.True(
            hasReference,
            $"Expected project {Path.GetFileName(csprojPath)} to reference {scope} {dependencyName}.");
    }

    private static bool HasDirectProjectReference(string csprojPath, string dependencyName)
    {
        var csproj = XDocument.Load(csprojPath);
        XNamespace ns = csproj.Root?.GetDefaultNamespace() ?? XNamespace.None;

        return csproj
            .Descendants(ns + "ProjectReference")
            .Select(e => (string?)e.Attribute("Include"))
            .OfType<string>()
            .Any(include => include.Contains(dependencyName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasTransitiveProjectReference(string csprojPath, string dependencyName)
    {
        if (HasDirectProjectReference(csprojPath, dependencyName))
        {
            return true;
        }

        string projectDir = Path.GetDirectoryName(csprojPath)!;
        var csproj = XDocument.Load(csprojPath);
        XNamespace ns = csproj.Root?.GetDefaultNamespace() ?? XNamespace.None;

        foreach (string include in csproj
            .Descendants(ns + "ProjectReference")
            .Select(e => (string?)e.Attribute("Include"))
            .OfType<string>())
        {
            string referencedCsproj = Path.GetFullPath(Path.Combine(projectDir, include));
            if (HasTransitiveProjectReference(referencedCsproj, dependencyName))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// F-018 (S2, R-11, S-14): the QuestPDF license is assigned in exactly one
    /// place (<c>QuestPdfConfiguration</c>, reached from <c>AddReportExporters()</c>).
    /// </summary>
    [Fact]
    public void F018_QuestPdf_License_Se_Asigna_En_Un_Unico_Lugar()
    {
        string testAssemblyLocation = typeof(ArchitectureTests).Assembly.Location;
        string repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testAssemblyLocation)!, "..", "..", "..", "..", ".."));

        string[] offenders = Directory
            .EnumerateFiles(Path.Combine(repoRoot, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => System.Text.RegularExpressions.Regex.IsMatch(File.ReadAllText(path), @"Settings\.License\s*="))
            .Select(path => Path.GetFileName(path))
            .ToArray();

        Assert.Equal(new[] { "QuestPdfConfiguration.cs" }, offenders);
    }

    /// <summary>
    /// F-020 (S5, D-26, DD-15): every import adapter that touches a library (CSV/XLSX parsing, EF Core) lives in
    /// <c>ControlDeFinanzasIA.Infrastructure.Import</c>.
    /// </summary>
    [Theory]
    [InlineData("CsvTableReader")]
    [InlineData("ClosedXmlTableReader")]
    [InlineData("SniffingTabularReader")]
    [InlineData("EfImportLookup")]
    [InlineData("EfImportWriter")]
    public void F020_Import_Adapters_Residen_En_Infrastructure_Import(string typeName)
    {
        IEnumerable<Type> found = Types
            .InAssembly(_infrastructureAssembly)
            .That()
            .HaveName(typeName)
            .GetTypes();

        Type type = Assert.Single(found);
        Assert.Equal("ControlDeFinanzasIA.Infrastructure.Import", type.Namespace);
    }

    /// <summary>
    /// F-020 (S5, D-26, DD-15): no Application type depends on the Infrastructure import namespace, and in the UI
    /// only the composition root may.
    /// </summary>
    [Fact]
    public void F020_Nadie_Depende_De_Infrastructure_Import_Salvo_CompositionRoot()
    {
        const string importNamespace = "ControlDeFinanzasIA.Infrastructure.Import";

        TestResult application = Types
            .InAssembly(_applicationAssembly)
            .ShouldNot()
            .HaveDependencyOn(importNamespace)
            .GetResult();
        TestResult ui = Types
            .InAssembly(_uiAssembly)
            .That()
            .DoNotHaveName("Bootstrap")
            .ShouldNot()
            .HaveDependencyOn(importNamespace)
            .GetResult();

        Assert.True(application.IsSuccessful, FormatFailingTypes(application));
        Assert.True(ui.IsSuccessful, FormatFailingTypes(ui));
    }

    private static string GetProjectFilePath(Assembly assembly)
    {
        string assemblyName = assembly.GetName().Name!;
        string testAssemblyLocation = typeof(ArchitectureTests).Assembly.Location;
        string repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testAssemblyLocation)!, "..", "..", "..", "..", ".."));

        string projectRelativePath = assemblyName switch
        {
            "ControlDeFinanzasIA.Domain" => @"src\ControlDeFinanzasIA.Domain",
            "ControlDeFinanzasIA.Application" => @"src\ControlDeFinanzasIA.Application",
            "ControlDeFinanzasIA.Infrastructure" => @"src\ControlDeFinanzasIA.Infrastructure",
            "ControlDeFinanzasIA.UI" => @"src\ControlDeFinanzasIA.UI",
            "ControlDeFinanzasIA.Tests.Unit" => @"tests\ControlDeFinanzasIA.Tests.Unit",
            "ControlDeFinanzasIA.Tests.Integration" => @"tests\ControlDeFinanzasIA.Tests.Integration",
            _ => throw new InvalidOperationException($"Unknown assembly: {assemblyName}")
        };

        return Path.Combine(repoRoot, projectRelativePath, $"{assemblyName}.csproj");
    }

    private static string FormatFailingTypes(TestResult result)
    {
        if (result.FailingTypeNames is null)
        {
            return "Architectural rule violation detected (no failing type names available).";
        }

        return $"Failing types: {string.Join(", ", result.FailingTypeNames)}";
    }

    private static Assembly LoadProjectAssembly(string projectRelativePath, string assemblyName)
    {
        string testAssemblyLocation = typeof(ArchitectureTests).Assembly.Location;
        string configuration = testAssemblyLocation.Contains($"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
            ? "Release"
            : "Debug";
        string repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testAssemblyLocation)!, "..", "..", "..", "..", ".."));
        string dllPath = Path.Combine(repoRoot, projectRelativePath, "bin", configuration, "net8.0", $"{assemblyName}.dll");

        if (!File.Exists(dllPath))
        {
            string fallbackConfiguration = configuration == "Release" ? "Debug" : "Release";
            dllPath = Path.Combine(repoRoot, projectRelativePath, "bin", fallbackConfiguration, "net8.0", $"{assemblyName}.dll");
        }

        return Assembly.LoadFrom(dllPath);
    }
}
