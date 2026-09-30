namespace ControlDeFinanzasIA.UI.Input;

/// <summary>
/// One request to move keyboard focus (F-021, DD-5). A view-model raises a request by assigning a NEW instance to a
/// property bound to <c>FocusOnRequest.Token</c>: reference inequality guarantees a property-changed notification
/// even when the previous request targeted the same control.
/// </summary>
public sealed class FocusRequestToken
{
}
