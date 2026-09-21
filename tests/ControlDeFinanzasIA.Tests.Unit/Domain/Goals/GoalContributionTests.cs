using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Goals;
using ControlDeFinanzasIA.Domain.ValueObjects;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Domain.Goals;

/// <summary>
/// Tests para las invariantes de <see cref="GoalContribution.Create"/>
/// (R-3, D-2). A diferencia de <c>DebtPayment</c>, este factory es
/// PUBLICO: el mero hecho de que este test (en otro assembly) compile y
/// llame a <see cref="GoalContribution.Create"/> directamente prueba que
/// es accesible, no <c>internal</c>.
/// </summary>
public sealed class GoalContributionTests
{
    [Fact]
    public void Create_ValidContribution_ReturnsContributionWithProvidedValues()
    {
        var goalId = Guid.NewGuid();
        var date = new DateOnly(2026, 9, 20);

        var contribution = GoalContribution.Create(
            goalId, Money.Create(15000m, Currency.DOP), date);

        Assert.NotEqual(Guid.Empty, contribution.Id);
        Assert.Equal(goalId, contribution.GoalId);
        Assert.Equal(15000m, contribution.Amount.Amount);
        Assert.Equal(Currency.DOP, contribution.Amount.Currency);
        Assert.Equal(date, contribution.Date);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_AmountNotPositive_ThrowsArgumentException(decimal amount)
    {
        // Amount == 0 is reachable through Money.Create (which only rejects negatives),
        // so GoalContribution.Create must reject it explicitly. Amount < 0 is unreachable
        // through Money.Create at all, but the guard must still hold for 0.
        if (amount < 0)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Money.Create(amount, Currency.DOP));
            return;
        }

        Assert.Throws<ArgumentException>(() => GoalContribution.Create(
            Guid.NewGuid(), Money.Create(amount, Currency.DOP), new DateOnly(2026, 9, 20)));
    }

    [Fact]
    public void Create_EmptyGoalId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => GoalContribution.Create(
            Guid.Empty, Money.Create(15000m, Currency.DOP), new DateOnly(2026, 9, 20)));
    }

    [Fact]
    public void Create_NullAmount_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => GoalContribution.Create(
            Guid.NewGuid(), null!, new DateOnly(2026, 9, 20)));
    }
}
