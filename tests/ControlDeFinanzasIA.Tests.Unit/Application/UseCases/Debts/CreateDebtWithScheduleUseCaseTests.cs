using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Debts.CreateDebtWithSchedule;
using ControlDeFinanzasIA.Domain.Debts;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.UseCases.Debts;

/// <summary>
/// Tests para <see cref="CreateDebtWithScheduleUseCase"/> (R-1, R-2,
/// R-3, S-1, S-2).
/// </summary>
public sealed class CreateDebtWithScheduleUseCaseTests
{
    private static CreateDebtWithScheduleUseCase CreateUseCase(FakeDebtRepository repository) =>
        new(repository, new CreateDebtWithScheduleValidator(), NullLogger<CreateDebtWithScheduleUseCase>.Instance);

    private static CreateDebtWithScheduleCommand ValidCommand() => new(
        "Prestamo carro",
        12000m,
        Currency.DOP,
        12,
        1000m,
        nameof(DebtFrequency.Monthly),
        new DateOnly(2026, 1, 15),
        "Banco Popular",
        null);

    [Fact]
    public async Task ExecuteAsync_ValidCommand_PersistsDebtAndFullSchedule()
    {
        var repository = new FakeDebtRepository();
        CreateDebtWithScheduleUseCase useCase = CreateUseCase(repository);

        Result result = await useCase.ExecuteAsync(ValidCommand());

        Assert.True(result.IsSuccess);
        CreateDebtWithScheduleResult value = Assert.IsType<CreateDebtWithScheduleResult>(result.Value);
        Assert.Equal(12, value.Payments.Count);
        Assert.True(repository.AddWithScheduleCalled);
        Assert.Single(repository.Debts);
        Assert.Equal(DebtStatus.Active, value.Debt.Status);
        Assert.All(value.Payments, p => Assert.Equal(DebtPaymentStatus.Pending, p.Status));
    }

    [Fact]
    public async Task ExecuteAsync_ScheduleCoversAllInstallmentsInOrder()
    {
        var repository = new FakeDebtRepository();
        CreateDebtWithScheduleUseCase useCase = CreateUseCase(repository);

        Result result = await useCase.ExecuteAsync(ValidCommand());

        CreateDebtWithScheduleResult value = Assert.IsType<CreateDebtWithScheduleResult>(result.Value);
        Assert.Equal(Enumerable.Range(1, 12), value.Payments.Select(p => p.InstallmentNumber));
        Assert.Equal(new DateOnly(2026, 1, 15), value.Payments[0].ScheduledDate);
        Assert.Equal(new DateOnly(2026, 12, 15), value.Payments[^1].ScheduledDate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public async Task ExecuteAsync_NonPositiveOriginalAmount_ReturnsValidationFailureWithoutWriting(decimal amount)
    {
        var repository = new FakeDebtRepository();
        CreateDebtWithScheduleUseCase useCase = CreateUseCase(repository);
        CreateDebtWithScheduleCommand command = ValidCommand() with { OriginalAmount = amount };

        Result result = await useCase.ExecuteAsync(command);

        Assert.False(result.IsSuccess);
        Assert.Equal("Validation", result.Error!.Code);
        Assert.Empty(repository.Debts);
        Assert.False(repository.AddWithScheduleCalled);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ExecuteAsync_NonPositiveTotalInstallments_ReturnsValidationFailureWithoutWriting(int installments)
    {
        var repository = new FakeDebtRepository();
        CreateDebtWithScheduleUseCase useCase = CreateUseCase(repository);
        CreateDebtWithScheduleCommand command = ValidCommand() with { TotalInstallments = installments };

        Result result = await useCase.ExecuteAsync(command);

        Assert.False(result.IsSuccess);
        Assert.Equal("Validation", result.Error!.Code);
        Assert.Empty(repository.Debts);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyDescription_ReturnsValidationFailureWithoutWriting()
    {
        var repository = new FakeDebtRepository();
        CreateDebtWithScheduleUseCase useCase = CreateUseCase(repository);
        CreateDebtWithScheduleCommand command = ValidCommand() with { Description = string.Empty };

        Result result = await useCase.ExecuteAsync(command);

        Assert.False(result.IsSuccess);
        Assert.Equal("Validation", result.Error!.Code);
        Assert.Empty(repository.Debts);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownFrequency_ReturnsValidationFailureWithoutWriting()
    {
        var repository = new FakeDebtRepository();
        CreateDebtWithScheduleUseCase useCase = CreateUseCase(repository);
        CreateDebtWithScheduleCommand command = ValidCommand() with { Frequency = "Weekly" };

        Result result = await useCase.ExecuteAsync(command);

        Assert.False(result.IsSuccess);
        Assert.Equal("Validation", result.Error!.Code);
        Assert.Empty(repository.Debts);
    }

    [Fact]
    public async Task ExecuteAsync_RepositoryFailure_PropagatesErrorAndDoesNotExposeSuccess()
    {
        var repository = new FakeDebtRepository
        {
            AddWithScheduleResult = Result.Failure("RepositoryError", "boom"),
        };
        CreateDebtWithScheduleUseCase useCase = CreateUseCase(repository);

        Result result = await useCase.ExecuteAsync(ValidCommand());

        Assert.False(result.IsSuccess);
        Assert.Equal("RepositoryError", result.Error!.Code);
        Assert.Empty(repository.Debts);
    }
}
