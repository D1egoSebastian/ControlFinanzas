using ControlDeFinanzasIA.Application.UseCases.Import.Common;
using ControlDeFinanzasIA.Application.UseCases.Import.Parsing;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Transactions;
using ControlDeFinanzasIA.Domain.ValueObjects;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Import;

/// <summary>
/// Proves the validator and the domain factories agree (F-020, R-10, DD-6, D-22): every accepted row builds
/// with the REAL <see cref="Expense"/> / <see cref="Income"/> factories, and every row rejected for amount,
/// date or description would have thrown there.
/// </summary>
public sealed class ImportRowValidatorDomainConsistencyTests
{
    private static readonly DateOnly _today = new(2026, 9, 30);
    private static readonly ColumnMapping _map = new(0, 1, 2, null, 3, 4, null);
    private static readonly DateTime _now = new(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);

    private static ParsedRow Parse(string date, string type, string description, string amount, string currency) =>
        ImportRowValidator.Parse(
            new RawRow(2, new[] { date, type, description, amount, currency }.Select(c => new RawCell(c, RawCellKind.Text)).ToList()),
            _map,
            5,
            new ImportOptions(null, null, DecimalStyle.Point, ImportDateFormat.DayMonthYear),
            DecimalStyle.Point,
            _today);

    private static void BuildWithDomain(ParsedRow row)
    {
        var money = Money.Create(row.Amount!.Value, row.Currency!.Value);

        if (row.Type == TransactionType.Expense)
        {
            var expense = Expense.Create(Guid.NewGuid(), row.Date!.Value, money, row.Description!, null, _now);
            Assert.Equal(row.Description, expense.Description);
            Assert.Equal(row.Amount, expense.Amount.Amount);
        }
        else
        {
            var income = Income.Create(null, row.Date!.Value, money, row.Description!, null, null, _now);
            Assert.Equal(row.Description, income.Description);
            Assert.Equal(row.Amount, income.Amount.Amount);
        }
    }

    public static IEnumerable<object[]> AcceptedBoundaries()
    {
        string max = new('x', 200);

        foreach (string type in new[] { "Gasto", "Ingreso" })
        {
            foreach (string currency in new[] { "DOP", "USD" })
            {
                yield return new object[] { "2026-03-04", type, "x", "0.0001", currency };
                yield return new object[] { "1900-01-01", type, max, "99999999999999.9999", currency };
                yield return new object[] { "2026-09-30", type, " " + max + " ", "1", currency };
                yield return new object[] { "2099-12-31", type, "Future", "12.5", currency };
            }
        }
    }

    [Theory]
    [MemberData(nameof(AcceptedBoundaries))]
    public void AcceptedRows_BuildWithTheRealDomainFactories(string date, string type, string description, string amount, string currency)
    {
        ParsedRow row = Parse(date, type, description, amount, currency);

        Assert.False(row.IsRejected);
        BuildWithDomain(row);
    }

    public static IEnumerable<object[]> RejectedBoundaries()
    {
        yield return new object[] { "2026-03-04", "Gasto", "x", "0", "DOP", ImportReason.ZeroAmount };
        yield return new object[] { "2026-03-04", "Gasto", "x", "-0.0001", "DOP", ImportReason.NegativeAmount };
        yield return new object[] { "1899-12-31", "Gasto", "x", "1", "DOP", ImportReason.DateBefore1900 };
        yield return new object[] { "2026-03-04", "Gasto", string.Empty, "1", "DOP", ImportReason.MissingDescription };
        yield return new object[] { "2026-03-04", "Gasto", "   ", "1", "DOP", ImportReason.MissingDescription };
        yield return new object[] { "2026-03-04", "Ingreso", new string('x', 201), "1", "USD", ImportReason.DescriptionTooLong };
    }

    [Theory]
    [MemberData(nameof(RejectedBoundaries))]
    public void RejectedRows_WouldHaveThrownInTheRealDomainFactories(
        string date, string type, string description, string amount, string currency, ImportReason expected)
    {
        ParsedRow row = Parse(date, type, description, amount, currency);

        Assert.Contains(expected, row.Reasons);
        Assert.True(row.IsRejected);

        DateOnly domainDate = expected == ImportReason.DateBefore1900 ? new DateOnly(1899, 12, 31) : new DateOnly(2026, 3, 4);
        decimal domainAmount = expected switch
        {
            ImportReason.ZeroAmount => 0m,
            ImportReason.NegativeAmount => -0.0001m,
            _ => 1m,
        };
        string domainDescription = expected is ImportReason.MissingDescription or ImportReason.DescriptionTooLong ? description : "x";

        Exception? thrown = Record.Exception(() =>
        {
            var money = Money.Create(Math.Abs(domainAmount), Currency.DOP);
            money = expected == ImportReason.NegativeAmount ? Money.Create(domainAmount, Currency.DOP) : money;

            if (type == "Gasto")
            {
                Expense.Create(Guid.NewGuid(), domainDate, money, domainDescription, null, _now);
            }
            else
            {
                Income.Create(null, domainDate, money, domainDescription, null, null, _now);
            }
        });

        Assert.NotNull(thrown);
        Assert.IsAssignableFrom<ArgumentException>(thrown);
    }
}
