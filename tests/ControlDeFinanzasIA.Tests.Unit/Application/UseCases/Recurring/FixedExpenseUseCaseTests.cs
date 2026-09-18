using ControlDeFinanzasIA.Application.UseCases.Recurring.CreateFixedExpense;
using ControlDeFinanzasIA.Application.UseCases.Recurring.UpdateFixedExpense;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Recurring;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.UseCases.Recurring;

public sealed class FixedExpenseUseCaseTests
{
    [Fact]
    public async Task CreateAndUpdate_PersistsFixedExpense()
    {
        var repository = new FakeRecurringRuleRepository();
        var create = new CreateFixedExpenseUseCase(repository, new CreateFixedExpenseValidator(),
            NullLogger<CreateFixedExpenseUseCase>.Instance);
        ControlDeFinanzasIA.Application.Common.Result created = await create.ExecuteAsync(new CreateFixedExpenseCommand(
            "Gym", 40m, Currency.USD, "Monthly", new DateOnly(2026, 1, 15), null, Guid.NewGuid(), Guid.NewGuid()));
        FixedExpense rule = Assert.IsType<FixedExpense>(created.Value);
        var update = new UpdateFixedExpenseUseCase(repository, new UpdateFixedExpenseValidator(),
            NullLogger<UpdateFixedExpenseUseCase>.Instance);

        ControlDeFinanzasIA.Application.Common.Result result = await update.ExecuteAsync(new UpdateFixedExpenseCommand(
            rule.Id, "Gym Plus", 50m, Currency.USD, "Bimonthly", rule.StartDate, null,
            rule.CategoryId, rule.PaymentMethodId));

        Assert.True(result.IsSuccess);
        Assert.Equal("Gym Plus", rule.Name);
        Assert.Equal("Bimonthly", rule.Frequency);
    }
}
