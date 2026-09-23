using ControlDeFinanzasIA.Application.Abstractions.Llm;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Settings;
using ControlDeFinanzasIA.Application.UseCases.Expenses.CategorizeExpense;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Llm;

/// <summary>
/// Asserts the API key never leaks through <c>CategorizeExpenseUseCase</c>'s
/// logging or <see cref="Result"/> output on any failure path (F-013,
/// slice 5b, R-4, D-8, S-14). A sentinel key value is used so the assertion
/// is meaningful — it fails loudly if a future change starts formatting
/// the raw settings or exception into a log line.
/// </summary>
public sealed class ApiKeyIsNeverLoggedTests
{
    private const string _sentinelKey = "sk-SENTINEL-DO-NOT-LOG";

    private static readonly Category _category = new()
    {
        Id = Guid.NewGuid(),
        Name = "Alimentación",
        IsSystem = true,
    };

    private readonly FakeSettingsStore _settingsStore;
    private readonly FakeLlmClientFactory _factory;
    private readonly FakeLlmClient _client;
    private readonly StrictReadOnlyCategoryRepository _categories;
    private readonly FakePromptProvider _prompts;
    private readonly RecordingLogger<CategorizeExpenseUseCase> _logger;
    private readonly CategorizeExpenseUseCase _useCase;

    public ApiKeyIsNeverLoggedTests()
    {
        _settingsStore = new FakeSettingsStore
        {
            AppSettings = AppSettings.Defaults with { AiApiKey = _sentinelKey },
        };

        _client = new FakeLlmClient();

        _factory = new FakeLlmClientFactory
        {
            ResolveResult = LlmClientResolution.Configured(_client),
        };

        _categories = new StrictReadOnlyCategoryRepository { Categories = [_category] };
        _prompts = new FakePromptProvider();
        _logger = new RecordingLogger<CategorizeExpenseUseCase>();

        _useCase = new CategorizeExpenseUseCase(_settingsStore, _factory, _categories, _prompts, _logger);
    }

    private async Task<Result> RunAsync(Exception failure)
    {
        _client.ExceptionToThrow = failure;
        return await _useCase.ExecuteAsync(new CategorizeExpenseCommand("pizza 25"));
    }

    [Fact]
    public async Task ExecuteAsync_InvalidCredentialsFailure_SentinelKeyNeverAppearsInAnyLoggedMessage()
    {
        Result result = await RunAsync(new LlmException(LlmFailureKind.InvalidCredentials, "401 Unauthorized"));

        AssertSentinelNeverLeaked(result);
    }

    [Fact]
    public async Task ExecuteAsync_ServerErrorFailure_SentinelKeyNeverAppearsInAnyLoggedMessage()
    {
        Result result = await RunAsync(new LlmException(LlmFailureKind.ServerError, "500 Internal Server Error"));

        AssertSentinelNeverLeaked(result);
    }

    [Fact]
    public async Task ExecuteAsync_TimeoutFailure_SentinelKeyNeverAppearsInAnyLoggedMessage()
    {
        Result result = await RunAsync(new LlmException(LlmFailureKind.Timeout, "attempt timeout"));

        AssertSentinelNeverLeaked(result);
    }

    private void AssertSentinelNeverLeaked(Result result)
    {
        Assert.False(result.IsSuccess);
        Assert.DoesNotContain(_sentinelKey, result.Error!.Message, StringComparison.Ordinal);

        foreach (string message in _logger.Messages)
        {
            Assert.DoesNotContain(_sentinelKey, message, StringComparison.Ordinal);
        }

        foreach (KeyValuePair<string, object?> property in _logger.Properties)
        {
            string? value = property.Value?.ToString();
            if (value is not null)
            {
                Assert.DoesNotContain(_sentinelKey, value, StringComparison.Ordinal);
            }
        }
    }
}
