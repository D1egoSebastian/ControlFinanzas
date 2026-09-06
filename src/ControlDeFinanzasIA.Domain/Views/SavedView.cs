using ControlDeFinanzasIA.Domain.Enums;

namespace ControlDeFinanzasIA.Domain.Views;

/// <summary>
/// Vista guardada: un filtro de transacciones serializado como JSON
/// versionado que el usuario puede nombrar y recuperar. Entidad con
/// identidad (<see cref="Id"/>), igualdad por referencia e invariantes
/// blindadas en <see cref="Create"/> y <see cref="Update"/>.
/// </summary>
public sealed class SavedView
{
    /// <summary>
    /// Identificador único de la vista. Generado en <see cref="Create"/>
    /// mediante <see cref="Guid.NewGuid"/>; inmutable post-construcción.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Nombre descriptivo de la vista. Obligatorio, único en la tabla
    /// <c>SavedViews</c> y con longitud máxima de 100 caracteres.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Tipo de entidad al que aplica la vista (<c>Expense</c> o
    /// <c>Income</c>). Inmutable post-construcción.
    /// </summary>
    public TransactionType EntityType { get; private set; }

    /// <summary>
    /// Filtro serializado como JSON versionado en envelope
    /// <c>{ schemaVersion, type, data }</c>. Obligatorio y no vacío.
    /// </summary>
    public string FilterJson { get; private set; } = string.Empty;

    /// <summary>
    /// Versión del esquema del filtro almacenado. Se estampa internamente
    /// en <see cref="Create"/> con valor 1.
    /// </summary>
    public int SchemaVersion { get; private set; }

    /// <summary>
    /// Fecha y hora de creación de la vista. Inmutable post-construcción.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Fecha y hora de la última actualización de la vista.
    /// </summary>
    public DateTime UpdatedAt { get; private set; }

    /// <summary>
    /// Constructor sin parámetros reservado para EF Core. No debe usarse
    /// desde código de aplicación.
    /// </summary>
    private SavedView()
    {
    }

    /// <summary>
    /// Constructor de uso interno invocado por <see cref="Create"/>.
    /// No debe llamarse directamente desde código de aplicación.
    /// </summary>
    /// <param name="id">Identificador único de la vista.</param>
    /// <param name="name">Nombre normalizado y trimado.</param>
    /// <param name="entityType">Tipo de entidad (gastos o ingresos).</param>
    /// <param name="filterJson">Filtro serializado como JSON.</param>
    /// <param name="schemaVersion">Versión del esquema del filtro.</param>
    /// <param name="createdAt">Timestamp de creación UTC.</param>
    /// <param name="updatedAt">Timestamp de última actualización UTC.</param>
    private SavedView(
        Guid id,
        string name,
        TransactionType entityType,
        string filterJson,
        int schemaVersion,
        DateTime createdAt,
        DateTime updatedAt)
    {
        Id = id;
        Name = name;
        EntityType = entityType;
        FilterJson = filterJson;
        SchemaVersion = schemaVersion;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// Crea una nueva instancia de <see cref="SavedView"/> validando sus
    /// invariantes.
    /// </summary>
    /// <param name="name">Nombre de la vista.</param>
    /// <param name="entityType">Tipo de entidad al que aplica la vista.</param>
    /// <param name="filterJson">Filtro serializado como JSON.</param>
    /// <param name="createdAt">Fecha y hora de creación.</param>
    /// <returns>Una instancia válida de <see cref="SavedView"/>.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="name"/> o <paramref name="filterJson"/> son <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Si se viola alguna invariante de <paramref name="name"/>, <paramref name="entityType"/>, <paramref name="filterJson"/> o <paramref name="createdAt"/>.</exception>
    public static SavedView Create(
        string name,
        TransactionType entityType,
        string filterJson,
        DateTime createdAt)
    {
        (string normalizedName, TransactionType validatedType, string normalizedFilter) =
            ValidateAndNormalize(name, entityType, filterJson, createdAt);

        return new SavedView(
            Guid.NewGuid(),
            normalizedName,
            validatedType,
            normalizedFilter,
            1,
            createdAt,
            createdAt);
    }

    /// <summary>
    /// Reasigna las propiedades mutables de la vista y revalida sus
    /// invariantes. No modifica <see cref="Id"/>, <see cref="EntityType"/>,
    /// <see cref="SchemaVersion"/> ni <see cref="CreatedAt"/>.
    /// </summary>
    /// <param name="name">Nuevo nombre de la vista.</param>
    /// <param name="filterJson">Nuevo filtro serializado como JSON.</param>
    /// <param name="updatedAt">Timestamp de actualización.</param>
    /// <exception cref="ArgumentNullException">Si <paramref name="name"/> o <paramref name="filterJson"/> son <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Si se viola alguna invariante de <paramref name="name"/>, <paramref name="filterJson"/> o <paramref name="updatedAt"/>.</exception>
    public void Update(string name, string filterJson, DateTime updatedAt)
    {
        (string normalizedName, _, string normalizedFilter) =
            ValidateAndNormalize(name, EntityType, filterJson, updatedAt);

        Name = normalizedName;
        FilterJson = normalizedFilter;
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// Valida y normaliza el nombre, tipo de entidad, filtro JSON y
    /// timestamp de una vista guardada.
    /// </summary>
    /// <param name="name">Nombre a validar.</param>
    /// <param name="entityType">Tipo de entidad a validar.</param>
    /// <param name="filterJson">Filtro JSON a validar.</param>
    /// <param name="timestamp">Timestamp a validar.</param>
    /// <returns>Tupla con el nombre trimado, el tipo de entidad y el filtro JSON.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="name"/> o <paramref name="filterJson"/> son <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Si se viola alguna invariante.</exception>
    private static (string Name, TransactionType EntityType, string FilterJson) ValidateAndNormalize(
        string name,
        TransactionType entityType,
        string filterJson,
        DateTime timestamp)
    {
        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        if (filterJson is null)
        {
            throw new ArgumentNullException(nameof(filterJson));
        }

        string normalizedName = name.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new ArgumentException(
                "Name is required.",
                nameof(name));
        }

        if (normalizedName.Length > 100)
        {
            throw new ArgumentException(
                $"Name must be at most 100 characters. Received: {normalizedName.Length}.",
                nameof(name));
        }

        if (!Enum.IsDefined(typeof(TransactionType), entityType))
        {
            throw new ArgumentException(
                "EntityType is not a valid TransactionType value.",
                nameof(entityType));
        }

        string normalizedFilter = filterJson.Trim();
        if (string.IsNullOrWhiteSpace(normalizedFilter))
        {
            throw new ArgumentException(
                "FilterJson is required.",
                nameof(filterJson));
        }

        if (timestamp == default)
        {
            throw new ArgumentException(
                "Timestamp must be a non-default DateTime.",
                nameof(timestamp));
        }

        return (normalizedName, entityType, normalizedFilter);
    }
}
