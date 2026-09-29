namespace ControlDeFinanzasIA.Application.Abstractions;

/// <summary>
/// Describes the file type a picker dialog offers (F-019, DD-1).
/// </summary>
/// <param name="DisplayName">Localized name shown in the dialog.</param>
/// <param name="Extension">Extension without the leading dot (for example <c>zip</c>).</param>
/// <param name="MimeType">MIME type of the files.</param>
public sealed record FileTypeFilter(string DisplayName, string Extension, string MimeType);
