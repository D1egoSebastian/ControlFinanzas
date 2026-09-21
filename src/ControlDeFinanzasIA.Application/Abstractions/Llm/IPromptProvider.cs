namespace ControlDeFinanzasIA.Application.Abstractions.Llm;

/// <summary>
/// Puerto hacia las plantillas de prompt usadas por el pipeline de IA
/// (F-013, D-4). Las plantillas concretas viven como recursos embebidos
/// en Infrastructure.
/// </summary>
public interface IPromptProvider
{
    /// <summary>
    /// Devuelve la plantilla de prompt <paramref name="name"/> para
    /// <paramref name="locale"/>, con fallback al locale por defecto si
    /// no existe una variante localizada. Nunca devuelve <c>null</c>;
    /// nunca lanza para un nombre conocido.
    /// </summary>
    /// <param name="name">
    /// Nombre de la plantilla (ver
    /// <see cref="ControlDeFinanzasIA.Application.Llm.PromptNames"/>).
    /// </param>
    /// <param name="locale">Locale solicitado.</param>
    string GetTemplate(string name, string locale);
}
