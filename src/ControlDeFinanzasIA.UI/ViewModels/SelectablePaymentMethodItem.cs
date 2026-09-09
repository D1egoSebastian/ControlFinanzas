using CommunityToolkit.Mvvm.ComponentModel;

namespace ControlDeFinanzasIA.UI.ViewModels;

/// <summary>
/// Observable wrapper for a payment method shown in the multi-select
/// filter picker. The immutable identity data is set on creation;
/// <see cref="IsSelected"/> is observable so the view-model can read
/// the current selection without code-behind synchronization.
/// </summary>
public sealed partial class SelectablePaymentMethodItem : ObservableObject
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SelectablePaymentMethodItem"/> class.
    /// </summary>
    /// <param name="id">Payment method identifier.</param>
    /// <param name="name">Display name.</param>
    /// <param name="isSystem">Whether the method is a system seed (not deletable).</param>
    public SelectablePaymentMethodItem(Guid id, string name, bool isSystem)
    {
        Id = id;
        Name = name;
        IsSystem = isSystem;
    }

    /// <summary>
    /// Gets the payment method identifier.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets the payment method display name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets a value indicating whether this is a system payment method.
    /// </summary>
    public bool IsSystem { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the payment method is currently
    /// selected for filtering.
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;
}
