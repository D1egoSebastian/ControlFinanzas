using ControlDeFinanzasIA.Application.Abstractions.Llm;
using ControlDeFinanzasIA.Application.UseCases.Goals.ContributeToGoal;
using ControlDeFinanzasIA.Application.UseCases.Goals.CreateGoal;
using ControlDeFinanzasIA.Application.UseCases.Goals.DeleteGoal;
using ControlDeFinanzasIA.Application.UseCases.Goals.GenerateSavingsPlan;
using ControlDeFinanzasIA.Application.UseCases.Goals.GetGoals;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Goals;
using ControlDeFinanzasIA.Domain.ValueObjects;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using ControlDeFinanzasIA.UI.Resources;
using ControlDeFinanzasIA.UI.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.UI;

/// <summary>
/// Unit tests for <see cref="GoalsViewModel"/> (F-011, slice 3): load
/// populates goal rows with precomputed progress/plan (R-1, R-5), create
/// dispatches the use case with the current form values and resets the
/// form on success, a failed create/delete surfaces into
/// <see cref="GoalsViewModel.FormError"/> without clearing the form,
/// selecting a row exposes the pinned contribution currency, a
/// contribution always uses the selected goal's currency (never a
/// user-picked one), and a <c>CurrencyMismatch</c> failure is substituted
/// with the localized <see cref="Strings.GoalsCurrencyMismatchMessage"/>
/// rather than the use case's internal message (spec R-4 reconciliation
/// note).
/// </summary>
public sealed class GoalsViewModelTests
{
    private static readonly DateOnly _today = new(2026, 9, 20);

    [Fact]
    public async Task Constructor_LoadsGoals_PopulatesRowsWithProgressAndPlan()
    {
        var goals = new FakeGoalRepository();
        var contributions = new FakeGoalContributionRepository();
        var goal = Goal.Create("Fondo de emergencia", Money.Create(200000m, Currency.DOP), new DateOnly(2027, 6, 30));
        goals.Seed(goal);
        contributions.Seed(GoalContribution.Create(goal.Id, Money.Create(15000m, Currency.DOP), _today));

        GoalsViewModel viewModel = CreateViewModel(goals: goals, contributions: contributions);
        await Task.Delay(1); // allow the constructor's fire-and-forget LoadAsync to complete

        GoalRow row = Assert.Single(viewModel.Goals);
        Assert.Equal("Fondo de emergencia", row.Name);
        Assert.Equal(15000m, row.ContributedAmount);
        Assert.Equal(7.5m, row.PercentComplete);
        Assert.False(row.AlreadyMet);
        Assert.True(row.ShowPlan);
    }

    [Fact]
    public async Task Constructor_NoGoals_LeavesGoalsEmpty()
    {
        GoalsViewModel viewModel = CreateViewModel();
        await Task.Delay(1);

        Assert.Empty(viewModel.Goals);
    }

    [Fact]
    public async Task CreateCommand_ValidForm_CallsUseCaseAndResetsForm()
    {
        GoalsViewModel viewModel = CreateViewModel();
        await Task.Delay(1);
        viewModel.Name = "Vacaciones";
        viewModel.TargetAmount = 50000m;
        viewModel.Currency = Currency.DOP;
        viewModel.TargetDate = new DateOnly(2027, 1, 1);

        await viewModel.CreateCommand.ExecuteAsync(null);

        Assert.Single(viewModel.Goals);
        Assert.Equal(string.Empty, viewModel.FormError);
        Assert.NotEqual(string.Empty, viewModel.StatusMessage);

        // The form is reset after a successful create.
        Assert.Equal(string.Empty, viewModel.Name);
        Assert.Equal(0m, viewModel.TargetAmount);
    }

    [Fact]
    public async Task CreateCommand_InvalidForm_SetsFormErrorAndDoesNotResetForm()
    {
        GoalsViewModel viewModel = CreateViewModel();
        await Task.Delay(1);
        viewModel.Name = string.Empty; // invalid: name required
        viewModel.TargetAmount = 50000m;

        await viewModel.CreateCommand.ExecuteAsync(null);

        Assert.Empty(viewModel.Goals);
        Assert.NotEqual(string.Empty, viewModel.FormError);

        // The form is NOT cleared on failure.
        Assert.Equal(50000m, viewModel.TargetAmount);
    }

    [Fact]
    public async Task SelectCommand_SelectingRow_ExposesContributionCurrencyFromGoal()
    {
        var goals = new FakeGoalRepository();
        var goal = Goal.Create("Fondo", Money.Create(100000m, Currency.USD), new DateOnly(2027, 1, 1));
        goals.Seed(goal);
        GoalsViewModel viewModel = CreateViewModel(goals: goals);
        await Task.Delay(1);
        GoalRow row = viewModel.Goals[0];

        viewModel.SelectCommand.Execute(row);

        Assert.Equal(row, viewModel.SelectedGoal);
        Assert.Equal(Currency.USD, viewModel.ContributionCurrency);
    }

    [Fact]
    public async Task ContributeCommand_MatchingCurrency_AddsContributionAndReloads()
    {
        var goals = new FakeGoalRepository();
        var contributions = new FakeGoalContributionRepository();
        var goal = Goal.Create("Fondo", Money.Create(100000m, Currency.DOP), new DateOnly(2027, 1, 1));
        goals.Seed(goal);
        GoalsViewModel viewModel = CreateViewModel(goals: goals, contributions: contributions);
        await Task.Delay(1);
        viewModel.SelectCommand.Execute(viewModel.Goals[0]);
        viewModel.ContributionAmount = 5000m;

        await viewModel.ContributeCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, viewModel.FormError);
        Assert.NotEqual(string.Empty, viewModel.StatusMessage);
        Assert.Equal(5000m, viewModel.Goals[0].ContributedAmount);
    }

    [Fact]
    public async Task ContributeCommand_UsesSelectedGoalCurrency_NeverAUserPickedOne()
    {
        // The contribution form has no currency picker (D-10): ContributeAsync
        // must always build the command with SelectedGoal.Currency.
        var goals = new FakeGoalRepository();
        var goal = Goal.Create("Fondo", Money.Create(100000m, Currency.DOP), new DateOnly(2027, 1, 1));
        goals.Seed(goal);
        var contributions = new FakeGoalContributionRepository();
        GoalsViewModel viewModel = CreateViewModel(goals: goals, contributions: contributions);
        await Task.Delay(1);
        viewModel.SelectCommand.Execute(viewModel.Goals[0]);
        viewModel.ContributionAmount = 1000m;

        await viewModel.ContributeCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, viewModel.FormError);
        GoalContribution contribution = Assert.Single(contributions.Contributions);
        Assert.Equal(Currency.DOP, contribution.Amount.Currency);
    }

    [Fact]
    public async Task ContributeCommand_CurrencyMismatch_SubstitutesLocalizedMessage()
    {
        var goalsForList = new FakeGoalRepository();
        var goalInDop = Goal.Create("Fondo", Money.Create(100000m, Currency.DOP), new DateOnly(2027, 1, 1));
        goalsForList.Seed(goalInDop);

        // A separate backing repository for the use case, forced (via
        // GetByIdOverride) to resolve to a goal in a different currency than
        // the one shown in the row -- simulating a stale/racing read. This is
        // the only way to exercise the CurrencyMismatch path through the
        // ViewModel: ContributeAsync always sends SelectedGoal.Currency, so a
        // mismatch is otherwise unreachable from this screen (D-10), and is
        // otherwise covered directly at the use-case layer.
        var goalInUsd = Goal.Create("Fondo", Money.Create(100000m, Currency.USD), new DateOnly(2027, 1, 1));
        var goalsForContribute = new FakeGoalRepository { GetByIdOverride = goalInUsd };

        var contributions = new FakeGoalContributionRepository();
        var contributeUseCase = new ContributeToGoalUseCase(
            goalsForContribute, contributions, new ContributeToGoalValidator(), NullLogger<ContributeToGoalUseCase>.Instance);

        GoalsViewModel viewModel = CreateViewModel(
            goals: goalsForList, contributions: contributions, contributeUseCase: contributeUseCase);
        await Task.Delay(1);
        viewModel.SelectCommand.Execute(viewModel.Goals[0]);
        viewModel.ContributionAmount = 1000m;

        await viewModel.ContributeCommand.ExecuteAsync(null);

        Assert.Equal(Strings.GoalsCurrencyMismatchMessage, viewModel.FormError);
        Assert.Empty(contributions.Contributions);
    }

    [Fact]
    public async Task DeleteCommand_RemovesGoalFromList()
    {
        var goals = new FakeGoalRepository();
        var goal = Goal.Create("Fondo", Money.Create(100000m, Currency.DOP), new DateOnly(2027, 1, 1));
        goals.Seed(goal);
        GoalsViewModel viewModel = CreateViewModel(goals: goals);
        await Task.Delay(1);
        GoalRow row = viewModel.Goals[0];

        await viewModel.DeleteCommand.ExecuteAsync(row);

        Assert.Empty(viewModel.Goals);
        Assert.NotEqual(string.Empty, viewModel.StatusMessage);
    }

    [Fact]
    public async Task RequestPlanCommand_Generated_SetsNarrationAndClearsStatusText()
    {
        var goals = new FakeGoalRepository();
        var goal = Goal.Create("Fondo", Money.Create(100000m, Currency.DOP), new DateOnly(2027, 1, 1));
        goals.Seed(goal);
        var llmClient = new FakeLlmClient
        {
            ResponseToReturn = new LlmResponse("Vas por buen camino, sigue asi.", 10, 5),
        };
        GenerateSavingsPlanUseCase savingsPlanUseCase = CreateSavingsPlanUseCase(goals, llmClient: llmClient, configured: true);
        GoalsViewModel viewModel = CreateViewModel(goals: goals, savingsPlanUseCase: savingsPlanUseCase);
        await Task.Delay(1);
        GoalRow row = viewModel.Goals[0];
        decimal figureBefore = row.RemainingAmount;

        await viewModel.RequestPlanCommand.ExecuteAsync(row);

        Assert.Equal("Vas por buen camino, sigue asi.", row.PlanNarration);
        Assert.Equal(string.Empty, row.PlanNarrationStatusText);
        Assert.False(row.IsGeneratingPlan);
        Assert.Equal(figureBefore, row.RemainingAmount);
    }

    [Fact]
    public async Task RequestPlanCommand_NotConfigured_ShowsLocalizedCaptionAndKeepsFigureUnchanged()
    {
        // Default CreateViewModel wires a savings-plan use case over FakeLlmClientFactory's
        // default "not configured" resolution, so no override is needed here.
        var goals = new FakeGoalRepository();
        var goal = Goal.Create("Fondo", Money.Create(100000m, Currency.DOP), new DateOnly(2027, 1, 1));
        goals.Seed(goal);
        GoalsViewModel viewModel = CreateViewModel(goals: goals);
        await Task.Delay(1);
        GoalRow row = viewModel.Goals[0];
        decimal figureBefore = row.RemainingAmount;

        await viewModel.RequestPlanCommand.ExecuteAsync(row);

        Assert.Equal(Strings.GoalsAiNotConfigured, row.PlanNarrationStatusText);
        Assert.Equal(string.Empty, row.PlanNarration);
        Assert.False(row.IsGeneratingPlan);
        Assert.Equal(figureBefore, row.RemainingAmount);
    }

    [Fact]
    public async Task RequestPlanCommand_LlmFailure_ShowsLocalizedCaptionAndKeepsFigureUnchanged()
    {
        var goals = new FakeGoalRepository();
        var goal = Goal.Create("Fondo", Money.Create(100000m, Currency.DOP), new DateOnly(2027, 1, 1));
        goals.Seed(goal);
        var llmClient = new FakeLlmClient
        {
            ExceptionToThrow = new LlmException(LlmFailureKind.Transport, "boom"),
        };
        GenerateSavingsPlanUseCase savingsPlanUseCase = CreateSavingsPlanUseCase(goals, llmClient: llmClient, configured: true);
        GoalsViewModel viewModel = CreateViewModel(goals: goals, savingsPlanUseCase: savingsPlanUseCase);
        await Task.Delay(1);
        GoalRow row = viewModel.Goals[0];
        decimal figureBefore = row.RemainingAmount;

        await viewModel.RequestPlanCommand.ExecuteAsync(row);

        Assert.Equal(Strings.GoalsAiUnavailable, row.PlanNarrationStatusText);
        Assert.Equal(string.Empty, row.PlanNarration);
        Assert.False(row.IsGeneratingPlan);
        Assert.Equal(figureBefore, row.RemainingAmount);
    }

    [Fact]
    public async Task RequestPlanCommand_ConcurrentRows_RowBPendingNeverBlocksRowA_AndDoubleClickOnRowBIsNoOp()
    {
        // S-8: row A and row B both idle. Row B's request starts first and is held open by the
        // gate below; row A must complete normally while row B is still pending, and a second
        // click on row B while it is pending must not invoke the client again.
        var goals = new FakeGoalRepository();
        var goalA = Goal.Create("Meta A", Money.Create(100000m, Currency.DOP), new DateOnly(2027, 1, 1));
        var goalB = Goal.Create("Meta B", Money.Create(200000m, Currency.DOP), new DateOnly(2027, 1, 1));
        goals.Seed(goalA);
        goals.Seed(goalB);

        var gate = new TaskCompletionSource<bool>();
        var firstCallStarted = new TaskCompletionSource<bool>();
        int callOrder = 0;
        var llmClient = new FakeLlmClient
        {
            OnCompleteAsync = async (_, _) =>
            {
                if (Interlocked.Increment(ref callOrder) == 1)
                {
                    firstCallStarted.TrySetResult(true);
                    await gate.Task;
                }

                return new LlmResponse("Vas bien, continua asi.", 10, 5);
            },
        };
        GenerateSavingsPlanUseCase savingsPlanUseCase = CreateSavingsPlanUseCase(goals, llmClient: llmClient, configured: true);
        GoalsViewModel viewModel = CreateViewModel(goals: goals, savingsPlanUseCase: savingsPlanUseCase);
        await Task.Delay(1);
        GoalRow rowA = viewModel.Goals.Single(g => g.Name == "Meta A");
        GoalRow rowB = viewModel.Goals.Single(g => g.Name == "Meta B");

        Task rowBTask = viewModel.RequestPlanCommand.ExecuteAsync(rowB);
        await firstCallStarted.Task;
        Assert.True(rowB.IsGeneratingPlan);

        Assert.True(viewModel.RequestPlanCommand.CanExecute(rowA));
        await viewModel.RequestPlanCommand.ExecuteAsync(rowA);

        Assert.False(rowA.IsGeneratingPlan);
        Assert.Equal("Vas bien, continua asi.", rowA.PlanNarration);
        Assert.True(rowB.IsGeneratingPlan);

        int callCountBeforeSecondClick = llmClient.CompleteCallCount;
        await viewModel.RequestPlanCommand.ExecuteAsync(rowB);
        Assert.Equal(callCountBeforeSecondClick, llmClient.CompleteCallCount);

        gate.SetResult(true);
        await rowBTask;
        Assert.False(rowB.IsGeneratingPlan);
        Assert.Equal("Vas bien, continua asi.", rowB.PlanNarration);
    }

    [Fact]
    public async Task LoadGoalsAsync_AfterContribute_ClearsNarrationAndIsGeneratingPlan_NoCarryOverByGoalId()
    {
        var goals = new FakeGoalRepository();
        var contributions = new FakeGoalContributionRepository();
        var goal = Goal.Create("Fondo", Money.Create(100000m, Currency.DOP), new DateOnly(2027, 1, 1));
        goals.Seed(goal);
        var llmClient = new FakeLlmClient
        {
            ResponseToReturn = new LlmResponse("Vas por buen camino, sigue asi.", 10, 5),
        };
        GenerateSavingsPlanUseCase savingsPlanUseCase = CreateSavingsPlanUseCase(goals, contributions, llmClient: llmClient, configured: true);
        GoalsViewModel viewModel = CreateViewModel(goals: goals, contributions: contributions, savingsPlanUseCase: savingsPlanUseCase);
        await Task.Delay(1);
        GoalRow row = viewModel.Goals[0];
        await viewModel.RequestPlanCommand.ExecuteAsync(row);
        Assert.NotEqual(string.Empty, row.PlanNarration);

        viewModel.SelectCommand.Execute(row);
        viewModel.ContributionAmount = 1000m;
        await viewModel.ContributeCommand.ExecuteAsync(null);

        GoalRow reloadedRow = Assert.Single(viewModel.Goals);
        Assert.Equal(goal.Id, reloadedRow.Id);
        Assert.NotSame(row, reloadedRow);
        Assert.Equal(string.Empty, reloadedRow.PlanNarration);
        Assert.Equal(string.Empty, reloadedRow.PlanNarrationStatusText);
        Assert.False(reloadedRow.IsGeneratingPlan);
    }

    [Fact]
    public async Task RequestPlanCommand_LateCompletionAfterReload_LeavesNewRowInstanceUntouched()
    {
        // D-9: a result arriving after a reload writes to the detached old row instance and is
        // effectively discarded — the rebuilt row instance is never touched by the stale write.
        var goals = new FakeGoalRepository();
        var contributions = new FakeGoalContributionRepository();
        var goal = Goal.Create("Fondo", Money.Create(100000m, Currency.DOP), new DateOnly(2027, 1, 1));
        goals.Seed(goal);
        var gate = new TaskCompletionSource<bool>();
        var llmClient = new FakeLlmClient
        {
            OnCompleteAsync = async (_, _) =>
            {
                await gate.Task;
                return new LlmResponse("Narracion tardia sin numeros.", 10, 5);
            },
        };
        GenerateSavingsPlanUseCase savingsPlanUseCase = CreateSavingsPlanUseCase(goals, contributions, llmClient: llmClient, configured: true);
        GoalsViewModel viewModel = CreateViewModel(goals: goals, contributions: contributions, savingsPlanUseCase: savingsPlanUseCase);
        await Task.Delay(1);
        GoalRow staleRow = viewModel.Goals[0];

        Task staleRequestTask = viewModel.RequestPlanCommand.ExecuteAsync(staleRow);

        viewModel.SelectCommand.Execute(staleRow);
        viewModel.ContributionAmount = 1000m;
        await viewModel.ContributeCommand.ExecuteAsync(null);
        GoalRow freshRow = viewModel.Goals[0];
        Assert.NotSame(staleRow, freshRow);

        gate.SetResult(true);
        await staleRequestTask;

        Assert.Equal("Narracion tardia sin numeros.", staleRow.PlanNarration);
        Assert.Equal(string.Empty, freshRow.PlanNarration);
        Assert.False(freshRow.IsGeneratingPlan);
    }

    [Fact]
    public void GoalRow_ProgressValue_ClampsAt100_WhilePercentCompleteStaysAboveIt()
    {
        var row = new GoalRow(
            Guid.NewGuid(),
            "Fondo",
            Currency.DOP,
            TargetAmount: 100m,
            ContributedAmount: 150m,
            RemainingAmount: 0m,
            PercentComplete: 150m,
            TargetDate: new DateOnly(2027, 1, 1),
            Status: GoalStatus.Completed,
            MonthsRemaining: 0,
            MonthlyContributionRequired: 0m,
            AlreadyMet: true,
            TargetDateInPast: false);

        Assert.Equal(100d, row.ProgressValue);
        Assert.Equal(150m, row.PercentComplete);
    }

    [Fact]
    public void GoalRow_ShowFlags_AreMutuallyExclusive_EvenWhenBothUnderlyingFlagsAreTrue()
    {
        var row = new GoalRow(
            Guid.NewGuid(),
            "Fondo",
            Currency.DOP,
            TargetAmount: 100m,
            ContributedAmount: 100m,
            RemainingAmount: 0m,
            PercentComplete: 100m,
            TargetDate: new DateOnly(2020, 1, 1),
            Status: GoalStatus.Completed,
            MonthsRemaining: 0,
            MonthlyContributionRequired: 0m,
            AlreadyMet: true,
            TargetDateInPast: true);

        Assert.True(row.ShowAlreadyMetMessage);
        Assert.False(row.ShowPastDateWarning);
        Assert.False(row.ShowPlan);
    }

    private static GoalsViewModel CreateViewModel(
        FakeGoalRepository? goals = null,
        FakeGoalContributionRepository? contributions = null,
        ContributeToGoalUseCase? contributeUseCase = null,
        FakeClock? clock = null,
        GenerateSavingsPlanUseCase? savingsPlanUseCase = null)
    {
        goals ??= new FakeGoalRepository();
        contributions ??= new FakeGoalContributionRepository();
        clock ??= new FakeClock { Today = _today };

        var getGoalsQuery = new GetGoalsQuery(goals, contributions, clock);
        var createUseCase = new CreateGoalUseCase(goals, new CreateGoalValidator(), NullLogger<CreateGoalUseCase>.Instance);
        contributeUseCase ??= new ContributeToGoalUseCase(
            goals, contributions, new ContributeToGoalValidator(), NullLogger<ContributeToGoalUseCase>.Instance);
        var deleteUseCase = new DeleteGoalUseCase(goals);

        // Defaults to FakeLlmClientFactory's "not configured" resolution, so existing tests that
        // do not pass savingsPlanUseCase keep making zero LLM calls.
        savingsPlanUseCase ??= CreateSavingsPlanUseCase(goals, contributions, configured: false);

        return new GoalsViewModel(
            getGoalsQuery,
            createUseCase,
            contributeUseCase,
            deleteUseCase,
            savingsPlanUseCase,
            clock,
            NullLogger<GoalsViewModel>.Instance);
    }

    private static GenerateSavingsPlanUseCase CreateSavingsPlanUseCase(
        FakeGoalRepository goals,
        FakeGoalContributionRepository? contributions = null,
        FakeLlmClient? llmClient = null,
        bool configured = false)
    {
        contributions ??= new FakeGoalContributionRepository();
        var factory = new FakeLlmClientFactory();
        if (configured)
        {
            factory.ResolveResult = LlmClientResolution.Configured(llmClient ?? new FakeLlmClient());
        }

        return new GenerateSavingsPlanUseCase(
            goals,
            contributions,
            new FakeSettingsStore(),
            factory,
            new DictionaryPromptProvider(),
            new FakeClock { Today = _today },
            NullLogger<GenerateSavingsPlanUseCase>.Instance);
    }
}
