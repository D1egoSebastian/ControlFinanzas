using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Goals;
using ControlDeFinanzasIA.Domain.ValueObjects;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Domain.Goals;

/// <summary>
/// Tests para las invariantes de <see cref="Goal.Create"/> (R-2, S-3,
/// S-4) y para el calculo de progreso (R-5) y el auto-completado (R-8).
/// </summary>
public sealed class GoalTests
{
    private static Goal CreateValidGoal(
        string name = "Fondo de emergencia",
        decimal targetAmount = 200000m,
        Currency currency = Currency.DOP,
        DateOnly? targetDate = null,
        string? note = null) =>
        Goal.Create(
            name,
            Money.Create(targetAmount, currency),
            targetDate ?? new DateOnly(2027, 6, 30),
            note);

    private static GoalContribution CreateContribution(Guid goalId, decimal amount, Currency currency = Currency.DOP, DateOnly? date = null) =>
        GoalContribution.Create(goalId, Money.Create(amount, currency), date ?? new DateOnly(2026, 9, 20));

    // --- R-2 / S-3 / S-4: Create invariants ---

    [Fact]
    public void Create_ValidGoal_ReturnsActiveGoalWithProvidedValues()
    {
        Goal goal = CreateValidGoal();

        Assert.NotEqual(Guid.Empty, goal.Id);
        Assert.Equal("Fondo de emergencia", goal.Name);
        Assert.Equal(200000m, goal.TargetAmount.Amount);
        Assert.Equal(Currency.DOP, goal.Currency);
        Assert.Equal(new DateOnly(2027, 6, 30), goal.TargetDate);
        Assert.Equal(GoalStatus.Active, goal.Status);
    }

    [Fact]
    public void Create_CurrencyIsDerivedFromTargetAmountCurrency()
    {
        var goal = Goal.Create(
            "Meta en dolares",
            Money.Create(5000m, Currency.USD),
            new DateOnly(2027, 1, 1));

        Assert.Equal(Currency.USD, goal.Currency);
        Assert.Equal(Currency.USD, goal.TargetAmount.Currency);
    }

    [Fact]
    public void Create_TrimsNameAndNote()
    {
        var goal = Goal.Create(
            "  Fondo de emergencia  ",
            Money.Create(200000m, Currency.DOP),
            new DateOnly(2027, 6, 30),
            "  nota  ");

        Assert.Equal("Fondo de emergencia", goal.Name);
        Assert.Equal("nota", goal.Note);
    }

    [Fact]
    public void Create_NoteOmitted_IsNull()
    {
        Goal goal = CreateValidGoal();

        Assert.Null(goal.Note);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_EmptyName_ThrowsArgumentException(string? name)
    {
        Assert.Throws<ArgumentException>(() => Goal.Create(
            name!,
            Money.Create(200000m, Currency.DOP),
            new DateOnly(2027, 6, 30)));
    }

    [Fact]
    public void Create_NameTooLong_ThrowsArgumentException()
    {
        string tooLong = new('a', Goal.MaxNameLength + 1);

        Assert.Throws<ArgumentException>(() => Goal.Create(
            tooLong,
            Money.Create(200000m, Currency.DOP),
            new DateOnly(2027, 6, 30)));
    }

    [Fact]
    public void Create_TargetAmountZero_ThrowsArgumentException()
    {
        // Money.Create only rejects negative amounts; Goal.Create must add its own
        // explicit "> 0" check on top (D-1).
        Assert.Throws<ArgumentException>(() => Goal.Create(
            "Meta",
            Money.Create(0m, Currency.DOP),
            new DateOnly(2027, 6, 30)));
    }

    [Fact]
    public void Create_NegativeTargetAmount_ThrowsAtMoneyCreate() =>
        // A negative Money can never be constructed at all (Money.Create's own
        // invariant), so a negative TargetAmount is unreachable at Goal.Create.
        Assert.Throws<ArgumentOutOfRangeException>(() => Money.Create(-1m, Currency.DOP));

    [Fact]
    public void Create_NoteTooLong_ThrowsArgumentException()
    {
        string tooLong = new('b', Goal.MaxNoteLength + 1);

        Assert.Throws<ArgumentException>(() => Goal.Create(
            "Meta",
            Money.Create(200000m, Currency.DOP),
            new DateOnly(2027, 6, 30),
            tooLong));
    }

    [Fact]
    public void Create_TargetDateInThePast_DoesNotThrow()
    {
        var goal = Goal.Create(
            "Meta vencida",
            Money.Create(200000m, Currency.DOP),
            new DateOnly(2020, 1, 1));

        Assert.Equal(new DateOnly(2020, 1, 1), goal.TargetDate);
    }

    // --- R-5: CalculateProgress ---

    [Fact]
    public void CalculateProgress_NoContributions_ReturnsZeroContributedAndFullRemaining()
    {
        Goal goal = CreateValidGoal(targetAmount: 200000m);

        GoalProgress progress = goal.CalculateProgress([]);

        Assert.Equal(0m, progress.Contributed.Amount);
        Assert.Equal(200000m, progress.Remaining.Amount);
        Assert.Equal(0m, progress.PercentComplete);
    }

    [Fact]
    public void CalculateProgress_PartialContribution_ReturnsSummedProgress()
    {
        Goal goal = CreateValidGoal(targetAmount: 200000m);
        GoalContribution contribution = CreateContribution(goal.Id, 15000m);

        GoalProgress progress = goal.CalculateProgress([contribution]);

        Assert.Equal(15000m, progress.Contributed.Amount);
        Assert.Equal(185000m, progress.Remaining.Amount);
        Assert.Equal(7.5m, progress.PercentComplete);
    }

    [Fact]
    public void CalculateProgress_ExactlyMetTarget_ReturnsZeroRemainingAndHundredPercent()
    {
        Goal goal = CreateValidGoal(targetAmount: 200000m);
        GoalContribution contribution = CreateContribution(goal.Id, 200000m);

        GoalProgress progress = goal.CalculateProgress([contribution]);

        Assert.Equal(200000m, progress.Contributed.Amount);
        Assert.Equal(0m, progress.Remaining.Amount);
        Assert.Equal(100m, progress.PercentComplete);
    }

    [Fact]
    public void CalculateProgress_OverContributed_ClampsRemainingAtZeroWithoutThrowing()
    {
        // Money.Subtract throws on a negative result; over-contribution is normal for a
        // goal, so Remaining must be computed via an explicit clamp, not an unguarded Subtract.
        Goal goal = CreateValidGoal(targetAmount: 200000m);
        GoalContribution contribution = CreateContribution(goal.Id, 215000m);

        GoalProgress progress = goal.CalculateProgress([contribution]);

        Assert.Equal(215000m, progress.Contributed.Amount);
        Assert.Equal(0m, progress.Remaining.Amount);
        Assert.Equal(107.5m, progress.PercentComplete);
    }

    [Fact]
    public void CalculateProgress_MismatchedCurrencyContribution_ThrowsArgumentExceptionNamingContribution()
    {
        Goal goal = CreateValidGoal(currency: Currency.DOP, targetAmount: 200000m);
        GoalContribution mismatched = CreateContribution(goal.Id, 100m, currency: Currency.USD);

        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => goal.CalculateProgress([mismatched]));
        Assert.Contains(mismatched.Id.ToString(), exception.Message);
    }

    [Fact]
    public void CalculateProgress_NullContributions_ThrowsArgumentNullException()
    {
        Goal goal = CreateValidGoal();

        Assert.Throws<ArgumentNullException>(() => goal.CalculateProgress(null!));
    }

    // --- R-8: TryAutoComplete ---

    [Fact]
    public void TryAutoComplete_BelowTarget_ReturnsFalseAndStaysActive()
    {
        Goal goal = CreateValidGoal(targetAmount: 200000m);
        GoalContribution contribution = CreateContribution(goal.Id, 100000m);

        bool completed = goal.TryAutoComplete([contribution]);

        Assert.False(completed);
        Assert.Equal(GoalStatus.Active, goal.Status);
    }

    [Fact]
    public void TryAutoComplete_ExactlyAtTarget_TransitionsToCompleted()
    {
        Goal goal = CreateValidGoal(targetAmount: 200000m);
        GoalContribution contribution = CreateContribution(goal.Id, 200000m);

        bool completed = goal.TryAutoComplete([contribution]);

        Assert.True(completed);
        Assert.Equal(GoalStatus.Completed, goal.Status);
    }

    [Fact]
    public void TryAutoComplete_AboveTarget_TransitionsToCompleted()
    {
        Goal goal = CreateValidGoal(targetAmount: 200000m);
        GoalContribution contribution = CreateContribution(goal.Id, 215000m);

        bool completed = goal.TryAutoComplete([contribution]);

        Assert.True(completed);
        Assert.Equal(GoalStatus.Completed, goal.Status);
    }

    [Fact]
    public void TryAutoComplete_AlreadyCompleted_IsIdempotentAndReturnsFalse()
    {
        Goal goal = CreateValidGoal(targetAmount: 200000m);
        GoalContribution contribution = CreateContribution(goal.Id, 200000m);
        Assert.True(goal.TryAutoComplete([contribution]));

        bool completedAgain = goal.TryAutoComplete([contribution]);

        Assert.False(completedAgain);
        Assert.Equal(GoalStatus.Completed, goal.Status);
    }

    [Fact]
    public void TryAutoComplete_EmptyContributions_ReturnsFalse()
    {
        Goal goal = CreateValidGoal(targetAmount: 200000m);

        bool completed = goal.TryAutoComplete([]);

        Assert.False(completed);
        Assert.Equal(GoalStatus.Active, goal.Status);
    }
}
