using ControlDeFinanzasIA.Application.Settings;
using ControlDeFinanzasIA.UI.Services;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>Hand-rolled <see cref="IThemeService"/> that records every applied theme.</summary>
public sealed class FakeThemeService : IThemeService
{
    private readonly List<AppTheme> _applied = [];

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <summary>Gets the themes passed to <see cref="Apply"/>, in call order.</summary>
    public IReadOnlyList<AppTheme> Applied => _applied;

    /// <inheritdoc />
    public AppTheme Current { get; private set; } = AppSettings.DefaultTheme;

    /// <inheritdoc />
    public void Apply(AppTheme theme)
    {
        _applied.Add(theme);
        bool changed = Current != theme;
        Current = theme;
        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
