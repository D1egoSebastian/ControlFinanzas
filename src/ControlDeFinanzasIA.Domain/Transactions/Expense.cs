using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Domain.Transactions;

/// <summary>
/// Entidad de dominio que representa un gasto. Es una entidad con
/// identidad (<see cref="Id"/>), igualdad por referencia y
/// invariantes blindadas en <see cref="Create"/> y
/// <see cref="Update"/>.
/// </summary>
public sealed class Expense
{
    /// <summary>
    /// Identificador único del gasto. Generado en <see cref="Create"/>
    /// mediante <see cref="Guid.NewGuid"/>; inmutable post-construcción.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Identificador de la categoría a la que pertenece el gasto.
    /// </summary>
    public Guid CategoryId { get; private set; }

    /// <summary>
    /// Fecha en que ocurrió el gasto (día calendario, sin hora ni
    /// zona horaria).
    /// </summary>
    public DateOnly Date { get; private set; }

    /// <summary>
    /// Monto y moneda del gasto. La moneda se accede vía
    /// <see cref="Money.Currency"/>.
    /// </summary>
    public Money Amount { get; private set; } = null!;

    /// <summary>
    /// Descripción corta del gasto (obligatoria).
    /// </summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>
    /// Nota opcional de mayor longitud. Se normaliza a <c>null</c>
    /// cuando está vacía o contiene solo espacios en blanco.
    /// </summary>
    public string? Note { get; private set; }

    /// <summary>
    /// Fecha y hora de creación del registro. Inmutable post-construcción.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Fecha y hora de la última actualización. En la creación es igual
    /// a <see cref="CreatedAt"/>; en <see cref="Update"/> se refresca.
    /// </summary>
    public DateTime UpdatedAt { get; private set; }

    /// <summary>
    /// Colección de tags asociados al gasto. Se inicializa en ambos
    /// constructores para garantizar que nunca sea <c>null</c>, incluso
    /// al materializar instancias desde EF Core.
    /// </summary>
    public ICollection<Tag> Tags { get; private set; } = new List<Tag>();

    /// <summary>
    /// Constructor sin parámetros reservado para EF Core. No debe usarse
    /// desde código de aplicación.
    /// </summary>
    private Expense()
    {
        Tags = new List<Tag>();
    }

    /// <summary>
    /// Constructor de uso interno invocado por <see cref="Create"/> y
    /// <see cref="Update"/> después de validar las invariantes.
    /// No debe llamarse directamente desde código de aplicación.
    /// </summary>
    /// <param name="id">Identificador único del gasto.</param>
    /// <param name="categoryId">Identificador de la categoría.</param>
    /// <param name="date">Fecha del gasto (post-validación, INV-3).</param>
    /// <param name="amount">Monto y moneda (post-validación, INV-1, INV-2).</param>
    /// <param name="description">Descripción normalizada y trimada (INV-4).</param>
    /// <param name="note">Nota normalizada; <c>null</c> si vacía o whitespace (INV-5).</param>
    /// <param name="createdAt">Timestamp de creación UTC (INV-7).</param>
    /// <param name="updatedAt">Timestamp de actualización UTC (INV-7, INV-8).</param>
    private Expense(
        Guid id,
        Guid categoryId,
        DateOnly date,
        Money amount,
        string description,
        string? note,
        DateTime createdAt,
        DateTime updatedAt)
    {
        Id = id;
        CategoryId = categoryId;
        Date = date;
        Amount = amount;
        Description = description;
        Note = note;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        Tags = new List<Tag>();
    }

    /// <summary>
    /// Crea una nueva instancia de <see cref="Expense"/> validando
    /// las invariantes INV-1 a INV-7.
    /// </summary>
    /// <param name="categoryId">Identificador de la categoría.</param>
    /// <param name="date">Fecha del gasto.</param>
    /// <param name="amount">Monto y moneda del gasto.</param>
    /// <param name="description">Descripción corta.</param>
    /// <param name="note">Nota opcional; se normaliza a <c>null</c> si es vacía.</param>
    /// <param name="createdAt">Fecha y hora de creación.</param>
    /// <returns>Una instancia válida de <see cref="Expense"/>.</returns>
    /// <exception cref="ArgumentNullException">Si <paramref name="amount"/> es <c>null</c> (INV-1).</exception>
    /// <exception cref="ArgumentException">Si se viola alguna de las invariantes INV-2 a INV-6.</exception>
    public static Expense Create(
        Guid categoryId,
        DateOnly date,
        Money amount,
        string description,
        string? note,
        DateTime createdAt)
    {
        (string normalizedDescription, string? normalizedNote) = ValidateAndNormalize(
            amount,
            date,
            description,
            note,
            categoryId);

        return new Expense(
            Guid.NewGuid(),
            categoryId,
            date,
            amount,
            normalizedDescription,
            normalizedNote,
            createdAt,
            createdAt);
    }

    /// <summary>
    /// Reasigna las propiedades mutables del gasto y revalida las
    /// invariantes INV-1 a INV-6, además de INV-8
    /// (<paramref name="updatedAt"/> &gt;= <see cref="CreatedAt"/>).
    /// </summary>
    /// <param name="categoryId">Nuevo identificador de categoría.</param>
    /// <param name="date">Nueva fecha del gasto.</param>
    /// <param name="amount">Nuevo monto y moneda.</param>
    /// <param name="description">Nueva descripción.</param>
    /// <param name="note">Nueva nota; se normaliza a <c>null</c> si es vacía.</param>
    /// <param name="updatedAt">Nuevo timestamp de actualización.</param>
    /// <exception cref="ArgumentNullException">Si <paramref name="amount"/> es <c>null</c> (INV-1).</exception>
    /// <exception cref="ArgumentException">Si se viola alguna de las invariantes INV-2 a INV-6 o INV-8.</exception>
    public void Update(
        Guid categoryId,
        DateOnly date,
        Money amount,
        string description,
        string? note,
        DateTime updatedAt)
    {
        if (updatedAt < CreatedAt)
        {
            throw new ArgumentException(
                $"UpdatedAt must be on or after CreatedAt. CreatedAt: {CreatedAt:o}, received: {updatedAt:o}.",
                nameof(updatedAt));
        }

        (string normalizedDescription, string? normalizedNote) = ValidateAndNormalize(
            amount,
            date,
            description,
            note,
            categoryId);

        CategoryId = categoryId;
        Date = date;
        Amount = amount;
        Description = normalizedDescription;
        Note = normalizedNote;
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// Añade un <see cref="Tag"/> al gasto. Si el tag ya está presente
    /// (mismo <see cref="Tag.Id"/>), la operación es un no-op.
    /// </summary>
    /// <param name="tag">Tag a añadir.</param>
    /// <exception cref="ArgumentNullException">Si <paramref name="tag"/> es <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Si el nombre del tag está vacío.</exception>
    public void AddTag(Tag tag)
    {
        ArgumentNullException.ThrowIfNull(tag);

        if (string.IsNullOrWhiteSpace(tag.Name))
        {
            throw new ArgumentException("Tag name cannot be empty.", nameof(tag));
        }

        if (Tags.Any(t => t.Id == tag.Id))
        {
            return;
        }

        Tags.Add(tag);
    }

    /// <summary>
    /// Elimina un <see cref="Tag"/> del gasto por su identificador.
    /// Si el tag no está presente, la operación es un no-op.
    /// </summary>
    /// <param name="tag">Tag a eliminar.</param>
    /// <exception cref="ArgumentNullException">Si <paramref name="tag"/> es <c>null</c>.</exception>
    public void RemoveTag(Tag tag)
    {
        ArgumentNullException.ThrowIfNull(tag);

        Tag? existing = Tags.FirstOrDefault(t => t.Id == tag.Id);
        if (existing is not null)
        {
            Tags.Remove(existing);
        }
    }

    private static (string Description, string? Note) ValidateAndNormalize(
        Money amount,
        DateOnly date,
        string description,
        string? note,
        Guid categoryId)
    {
        if (amount is null)
        {
            throw new ArgumentNullException(nameof(amount));
        }

        if (amount.Amount <= 0)
        {
            throw new ArgumentException(
                $"Amount must be strictly positive. Received: {amount.Amount}.",
                nameof(amount));
        }

        if (date < new DateOnly(1900, 1, 1))
        {
            throw new ArgumentException(
                $"Date must be on or after 1900-01-01. Received: {date:o}.",
                nameof(date));
        }

        if (categoryId == Guid.Empty)
        {
            throw new ArgumentException(
                "CategoryId must be a non-empty Guid.",
                nameof(categoryId));
        }

        string normalizedDescription = description.Trim();
        if (string.IsNullOrWhiteSpace(normalizedDescription))
        {
            throw new ArgumentException(
                "Description is required.",
                nameof(description));
        }

        if (normalizedDescription.Length > 200)
        {
            throw new ArgumentException(
                $"Description must be at most 200 characters. Received: {normalizedDescription.Length}.",
                nameof(description));
        }

        string? normalizedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (normalizedNote?.Length > 500)
        {
            throw new ArgumentException(
                $"Note must be at most 500 characters. Received: {normalizedNote.Length}.",
                nameof(note));
        }

        return (normalizedDescription, normalizedNote);
    }
}
