using CommunityToolkit.Mvvm.ComponentModel;

namespace ControlDeFinanzasIA.UI.ViewModels;

/// <summary>
/// Observable wrapper for a tag shown in a multi-select picker.
/// The immutable identity data is set on creation; <see cref="IsSelected"/>
/// is observable so the view-model can read the current selection without
/// code-behind synchronization.
/// </summary>
public sealed partial class SelectableTagItem : ObservableObject
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SelectableTagItem"/> class.
    /// </summary>
    /// <param name="id">Tag identifier.</param>
    /// <param name="name">Display name.</param>
    /// <param name="color">Optional color code.</param>
    public SelectableTagItem(Guid id, string name, string? color)
    {
        Id = id;
        Name = name;
        Color = color;
    }

    /// <summary>
    /// Gets the tag identifier.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets the tag display name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the optional color code.
    /// </summary>
    public string? Color { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the tag is currently selected.
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;
}
