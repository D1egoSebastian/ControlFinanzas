namespace ControlDeFinanzasIA.Domain.Catalog;

/// <summary>
/// Etiqueta de clasificación libre asociada a gastos. Los tags son
/// entidades con identidad (<see cref="Id"/>), igualdad por referencia e
/// invariantes blindadas en <see cref="Create"/> y <see cref="Update"/>.
/// </summary>
public sealed class Tag
{
    /// <summary>
    /// Identificador único del tag. Generado en <see cref="Create"/>
    /// mediante <see cref="Guid.NewGuid"/>; inmutable post-construcción.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Nombre descriptivo del tag. Obligatorio, único en la tabla
    /// <c>Tags</c> y con longitud máxima de 50 caracteres.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Color opcional asociado al tag (por ejemplo, <c>#FF8800</c>).
    /// Se normaliza a <c>null</c> cuando está vacío o contiene solo
    /// espacios en blanco; si se informa, su longitud máxima es 9.
    /// </summary>
    public string? Color { get; private set; }

    /// <summary>
    /// Fecha y hora de creación del tag. Inmutable post-construcción.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Constructor sin parámetros reservado para EF Core. No debe usarse
    /// desde código de aplicación.
    /// </summary>
    private Tag()
    {
    }

    /// <summary>
    /// Constructor de uso interno invocado por <see cref="Create"/>.
    /// No debe llamarse directamente desde código de aplicación.
    /// </summary>
    /// <param name="id">Identificador único del tag.</param>
    /// <param name="name">Nombre normalizado y trimado.</param>
    /// <param name="color">Color normalizado; <c>null</c> si está vacío.</param>
    /// <param name="createdAt">Timestamp de creación UTC.</param>
    private Tag(Guid id, string name, string? color, DateTime createdAt)
    {
        Id = id;
        Name = name;
        Color = color;
        CreatedAt = createdAt;
    }

    /// <summary>
    /// Crea una nueva instancia de <see cref="Tag"/> validando sus
    /// invariantes.
    /// </summary>
    /// <param name="name">Nombre del tag.</param>
    /// <param name="color">Color opcional; se normaliza a <c>null</c> si es vacío.</param>
    /// <param name="createdAt">Fecha y hora de creación.</param>
    /// <returns>Una instancia válida de <see cref="Tag"/>.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="name"/> es <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Si se viola alguna invariante de <paramref name="name"/>, <paramref name="color"/> o <paramref name="createdAt"/>.</exception>
    public static Tag Create(string name, string? color, DateTime createdAt)
    {
        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        (string normalizedName, string? normalizedColor) = ValidateAndNormalize(name, color);

        if (createdAt == default)
        {
            throw new ArgumentException(
                "CreatedAt must be a non-default DateTime.",
                nameof(createdAt));
        }

        return new Tag(Guid.NewGuid(), normalizedName, normalizedColor, createdAt);
    }

    /// <summary>
    /// Reasigna las propiedades mutables del tag y revalida sus invariantes.
    /// No modifica <see cref="Id"/> ni <see cref="CreatedAt"/>.
    /// </summary>
    /// <param name="name">Nuevo nombre del tag.</param>
    /// <param name="color">Nuevo color opcional.</param>
    /// <exception cref="ArgumentNullException">Si <paramref name="name"/> es <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Si se viola alguna invariante de <paramref name="name"/> o <paramref name="color"/>.</exception>
    public void Update(string name, string? color)
    {
        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        (string normalizedName, string? normalizedColor) = ValidateAndNormalize(name, color);

        Name = normalizedName;
        Color = normalizedColor;
    }

    /// <summary>
    /// Valida y normaliza el nombre y el color de un tag.
    /// </summary>
    /// <param name="name">Nombre a validar.</param>
    /// <param name="color">Color a validar; puede ser <c>null</c>.</param>
    /// <returns>Tupla con el nombre trimado y el color normalizado.</returns>
    /// <exception cref="ArgumentException">Si se viola alguna invariante.</exception>
    private static (string Name, string? Color) ValidateAndNormalize(string name, string? color)
    {
        string normalizedName = name.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new ArgumentException(
                "Name is required.",
                nameof(name));
        }

        if (normalizedName.Length > 50)
        {
            throw new ArgumentException(
                $"Name must be at most 50 characters. Received: {normalizedName.Length}.",
                nameof(name));
        }

        string? normalizedColor = string.IsNullOrWhiteSpace(color) ? null : color.Trim();
        if (normalizedColor?.Length > 9)
        {
            throw new ArgumentException(
                $"Color must be at most 9 characters. Received: {normalizedColor.Length}.",
                nameof(color));
        }

        return (normalizedName, normalizedColor);
    }
}
