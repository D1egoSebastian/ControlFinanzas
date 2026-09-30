namespace ControlDeFinanzasIA.UI.Diagnostics;

/// <summary>Shows a reported error to the user (implemented by the error overlay view-model).</summary>
public interface IErrorPresenter
{
    /// <summary>Presents the error identified by <paramref name="correlationId"/>.</summary>
    /// <param name="correlationId">The id written to the log.</param>
    /// <param name="occurredAtUtc">UTC time of the failure.</param>
    void Present(Guid correlationId, DateTime occurredAtUtc);
}
