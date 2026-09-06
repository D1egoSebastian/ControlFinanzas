using System;

namespace ControlDeFinanzasIA.UI.Composition;

/// <summary>
/// Marks the composition root of the <c>ControlDeFinanzasIA.UI</c> project.
/// </summary>
/// <remarks>
/// This attribute is a pure marker with no behavior. It documents the
/// convention that the annotated class is the single place where the
/// application's service graph is wired. In F-008 it is applied to
/// <see cref="Bootstrap"/>. The architecture test R-4 from F-007 filters
/// by the type name <c>Bootstrap</c> rather than by this attribute, but
/// the attribute is kept for readability and future tooling.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class CompositionRootAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CompositionRootAttribute"/> class.
    /// </summary>
    public CompositionRootAttribute()
    {
    }
}
