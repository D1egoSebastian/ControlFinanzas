namespace ControlDeFinanzasIA.UI.Services;

/// <summary>
/// Subscribes an object to <see cref="IThemeService.Changed"/> WITHOUT keeping it alive. The theme service is a singleton
/// and the view-models that react to a theme switch (the dashboard charts) are transient, so a plain event subscription
/// would keep every dashboard the user ever opened in memory; this one lets the target be collected and unsubscribes
/// itself the first time it finds the target gone.
/// </summary>
internal static class WeakThemeSubscription
{
    /// <summary>Calls <paramref name="onChanged"/> with <paramref name="target"/> every time the theme changes.</summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <param name="themeService">The theme service to listen to.</param>
    /// <param name="target">The object that reacts; held weakly.</param>
    /// <param name="onChanged">The reaction. It must be static so it cannot capture the target.</param>
    public static void Create<T>(IThemeService themeService, T target, Action<T> onChanged)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(themeService);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(onChanged);

        var weak = new WeakReference<T>(target);

        void Handler(object? sender, EventArgs e)
        {
            if (weak.TryGetTarget(out T? alive))
            {
                onChanged(alive);
            }
            else
            {
                themeService.Changed -= Handler;
            }
        }

        themeService.Changed += Handler;
    }
}
