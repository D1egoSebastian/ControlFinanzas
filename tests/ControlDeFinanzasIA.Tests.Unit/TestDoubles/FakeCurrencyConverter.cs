using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Services;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.ValueObjects;

namespace ControlDeFinanzasIA.Tests.Unit.TestDoubles;

/// <summary>
/// Hand-rolled fake for <see cref="ICurrencyConverter"/> used by the
/// Dashboard query tests (F-008, slice 2). Unlike
/// <see cref="ExpensesViewModelTests"/>, which wires the real
/// <c>CurrencyConverter</c> against a <c>FakeExchangeRateProvider</c>, the
/// dashboard queries need per-call control over which currencies succeed or
/// fail (S-6: FX failure must exclude the bucket without throwing), so a
/// direct fake of <see cref="ICurrencyConverter"/> is simpler than composing
/// the real converter + cache + provider stack for every test.
/// </summary>
public sealed class FakeCurrencyConverter : ICurrencyConverter
{
    private readonly Func<Money, Currency, Result>? _onConvert;

    /// <summary>
    /// Creates a fake that resolves conversions via <paramref name="onConvert"/>.
    /// When <c>null</c>, <see cref="ConvertAsync"/> defaults to a 1:1 rate
    /// (same numeric <see cref="Money.Amount"/>, target currency).
    /// </summary>
    /// <param name="onConvert">
    /// Callback invoked for cross-currency conversions (same-currency calls
    /// short-circuit before reaching it, matching the real converter's
    /// contract). Return <see cref="Result.Failure(string, string)"/> with
    /// code <c>"ExchangeRateUnavailable"</c> to simulate a provider failure.
    /// </param>
    public FakeCurrencyConverter(Func<Money, Currency, Result>? onConvert = null)
    {
        _onConvert = onConvert;
    }

    /// <summary>
    /// Number of times <see cref="ConvertAsync"/> was invoked, including
    /// same-currency short-circuits.
    /// </summary>
    public int ConvertCallCount { get; private set; }

    /// <inheritdoc />
    public Task<Result> ConvertAsync(Money source, Currency target, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ConvertCallCount++;

        if (source.Currency == target)
        {
            return Task.FromResult(Result.Success(source));
        }

        if (_onConvert is not null)
        {
            return Task.FromResult(_onConvert(source, target));
        }

        return Task.FromResult(Result.Success(Money.Create(source.Amount, target)));
    }

    /// <inheritdoc />
    public Task<MoneyDisplay> DisplayAsync(Money source, CancellationToken ct = default)
        => throw new NotSupportedException(
            "DisplayAsync is not used by the Dashboard queries (they normalize via ConvertAsync, see design.md).");
}
