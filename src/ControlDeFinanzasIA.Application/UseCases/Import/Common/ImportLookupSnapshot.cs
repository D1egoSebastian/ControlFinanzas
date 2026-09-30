using ControlDeFinanzasIA.Domain.Enums;

namespace ControlDeFinanzasIA.Application.UseCases.Import.Common;

/// <summary>A category or payment method as the importer needs to see it.</summary>
/// <param name="Id">Identifier.</param>
/// <param name="Name">Display name.</param>
/// <param name="IsSystem">Seeded by the application.</param>
public sealed record CatalogEntry(Guid Id, string Name, bool IsSystem);

/// <summary>The identity of an already-stored transaction, used for duplicate detection.</summary>
/// <param name="Date">Transaction date.</param>
/// <param name="Type">Expense or income.</param>
/// <param name="Amount">Stored amount.</param>
/// <param name="Currency">Stored currency.</param>
/// <param name="Description">Stored description.</param>
/// <param name="CategoryId">Stored category.</param>
public sealed record ExistingTransactionKey(
    DateOnly Date,
    TransactionType Type,
    decimal Amount,
    Currency Currency,
    string Description,
    Guid? CategoryId);

/// <summary>Everything the planner needs from the database (F-020, DD-1).</summary>
/// <param name="Categories">All categories.</param>
/// <param name="PaymentMethods">All payment methods.</param>
/// <param name="Existing">Stored transactions inside the file's date span.</param>
public sealed record ImportLookupSnapshot(
    IReadOnlyList<CatalogEntry> Categories,
    IReadOnlyList<CatalogEntry> PaymentMethods,
    IReadOnlyList<ExistingTransactionKey> Existing);
