using Xunit;

namespace ControlDeFinanzasIA.Tests.Integration.Packaging;

/// <summary>
/// A <see cref="FactAttribute"/> that is skipped, with an explicit message, when <c>artifacts/</c> does not exist and
/// <c>CONTROLDEFINANZASIA_REQUIRE_PACKAGING</c> is not <c>1</c>. xunit 2.9 has no runtime skip, so the decision is taken here.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class PackagingFactAttribute : FactAttribute
{
    public PackagingFactAttribute()
    {
        Skip = PackagingSupport.SkipReason(
            Directory.Exists(PackagingSupport.Artifacts),
            Environment.GetEnvironmentVariable(PackagingSupport.RequireVariable));
    }
}

/// <summary>The <see cref="TheoryAttribute"/> twin of <see cref="PackagingFactAttribute"/>.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class PackagingTheoryAttribute : TheoryAttribute
{
    public PackagingTheoryAttribute()
    {
        Skip = PackagingSupport.SkipReason(
            Directory.Exists(PackagingSupport.Artifacts),
            Environment.GetEnvironmentVariable(PackagingSupport.RequireVariable));
    }
}
