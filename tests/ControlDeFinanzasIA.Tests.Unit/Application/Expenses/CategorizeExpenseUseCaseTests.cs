using ControlDeFinanzasIA.Application.Abstractions.Llm;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.Llm;
using ControlDeFinanzasIA.Application.Settings;
using ControlDeFinanzasIA.Application.UseCases.Expenses.CategorizeExpense;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.Expenses;

/// <summary>
/// Unit tests for <see cref="CategorizeExpenseUseCase"/> (F-013, slice 5b,
/// R-3, R-4, R-6, R-7, D-6, D-7, D-8). No mocking library — hand-rolled
/// fakes only.
/// </summary>
public sealed class CategorizeExpenseUseCaseTests
{
    private static readonly Category _alimentacion = new()
    {
        Id = new Guid("11111111-1111-1111-1111-000000000001"),
        Name = "Alimentación",
        IsSystem = true,
    };

    private static readonly Category _transporte = new()
    {
        Id = new Guid("11111111-1111-1111-1111-000000000002"),
        Name = "Transporte",
        IsSystem = true,
    };

    private readonly FakeSettingsStore _settingsStore;
    private readonly FakeLlmClientFactory _factory;
    private readonly FakeLlmClient _client;
    private readonly StrictReadOnlyCategoryRepository _categories;
    private readonly FakePromptProvider _prompts;
    private readonly RecordingLogger<CategorizeExpenseUseCase> _logger;
    private readonly CategorizeExpenseUseCase _useCase;

    public CategorizeExpenseUseCaseTests()
    {
        _settingsStore = new FakeSettingsStore
        {
            AppSettings = AppSettings.Defaults with
            {
                AiApiKey = "sk-configured",
                AiTemperature = 1.9d,
                AiMaxTokensPerRequest = 2_000,
            },
        };

        _client = new FakeLlmClient
        {
            ResponseToReturn = new LlmResponse("""{"category":"Alimentación"}""", 42, 120),
        };

        _factory = new FakeLlmClientFactory
        {
            ResolveResult = LlmClientResolution.Configured(_client),
        };

        _categories = new StrictReadOnlyCategoryRepository
        {
            Categories = [_alimentacion, _transporte],
        };

        _prompts = new FakePromptProvider();
        _logger = new RecordingLogger<CategorizeExpenseUseCase>();

        _useCase = new CategorizeExpenseUseCase(_settingsStore, _factory, _categories, _prompts, _logger);
    }

    private static CategorizeExpenseCommand Command(string description = "pizza 25") => new(description);

    [Fact]
    public async Task ExecuteAsync_MatchingAnswer_ReturnsSuccessWithMatchedDto()
    {
        Result result = await _useCase.ExecuteAsync(Command());

        Assert.True(result.IsSuccess);
        CategorySuggestionDto dto = Assert.IsType<CategorySuggestionDto>(result.Value);
        Assert.True(dto.Matched);
        Assert.Equal(_alimentacion.Id, dto.CategoryId);
        Assert.Equal(_alimentacion.Name, dto.CategoryName);
        Assert.Equal(1, _client.CompleteCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnmatchedAnswer_ReturnsSuccessNotFailure()
    {
        _client.ResponseToReturn = new LlmResponse("""{"category":"Mascotas"}""", 10, 100);

        Result result = await _useCase.ExecuteAsync(Command());

        Assert.True(result.IsSuccess);
        CategorySuggestionDto dto = Assert.IsType<CategorySuggestionDto>(result.Value);
        Assert.False(dto.Matched);
        Assert.Null(dto.CategoryId);
        Assert.Null(dto.CategoryName);
    }

    [Fact]
    public async Task ExecuteAsync_AmbiguousDuplicateNormalizingCategories_ReturnsUnmatched()
    {
        Category duplicate = _alimentacion with
        {
            Id = new Guid("11111111-1111-1111-1111-000000000003"),
            Name = "alimentación",
        };
        _categories.Categories = [_alimentacion, duplicate];

        Result result = await _useCase.ExecuteAsync(Command());

        Assert.True(result.IsSuccess);
        CategorySuggestionDto dto = Assert.IsType<CategorySuggestionDto>(result.Value);
        Assert.False(dto.Matched);
    }

    [Fact]
    public async Task ExecuteAsync_NoApiKeyConfigured_ReturnsAiNotConfiguredWithZeroClientCalls()
    {
        _settingsStore.AppSettings = AppSettings.Defaults with { AiApiKey = string.Empty };
        _factory.ResolveResult = LlmClientResolution.NotConfigured(LlmConfigurationStatus.MissingApiKey);

        Result result = await _useCase.ExecuteAsync(Command());

        Assert.False(result.IsSuccess);
        Assert.Equal("AiNotConfigured", result.Error!.Code);
        Assert.Equal(0, _client.CompleteCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_CustomProviderNoBaseUrl_ReturnsAiNotConfiguredWithZeroClientCalls()
    {
        _settingsStore.AppSettings = AppSettings.Defaults with
        {
            AiApiKey = "sk-configured",
            AiProvider = AiProvider.Custom,
            AiBaseUrl = null,
        };
        _factory.ResolveResult = LlmClientResolution.NotConfigured(LlmConfigurationStatus.MissingBaseUrl);

        Result result = await _useCase.ExecuteAsync(Command());

        Assert.False(result.IsSuccess);
        Assert.Equal("AiNotConfigured", result.Error!.Code);
        Assert.Equal(0, _client.CompleteCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_TooShortDescription_ReturnsAiNotConfiguredWithZeroClientCalls()
    {
        Result result = await _useCase.ExecuteAsync(Command("abc"));

        Assert.False(result.IsSuccess);
        Assert.Equal("AiNotConfigured", result.Error!.Code);
        Assert.Equal(0, _client.CompleteCallCount);
        Assert.Empty(_factory.ResolveCalls);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyCategoryList_ReturnsSuccessWithZeroClientCalls()
    {
        _categories.Categories = [];

        Result result = await _useCase.ExecuteAsync(Command());

        Assert.True(result.IsSuccess);
        CategorySuggestionDto dto = Assert.IsType<CategorySuggestionDto>(result.Value);
        Assert.False(dto.Matched);
        Assert.Equal(0, _client.CompleteCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_ThrowingClient_ReturnsLlmUnavailableAndLogsWarningWithCorrelationId()
    {
        _client.ExceptionToThrow = new LlmException(LlmFailureKind.ServerError, "boom");

        Result result = await _useCase.ExecuteAsync(Command());

        Assert.False(result.IsSuccess);
        Assert.Equal("LLMUnavailable", result.Error!.Code);
        Assert.True(_logger.HasWarning);
        Assert.Contains(
            _logger.Properties,
            p => string.Equals(p.Key, "CorrelationId", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_AlwaysSendsForcedZeroTemperature_EvenWithHighAiTemperature()
    {
        await _useCase.ExecuteAsync(Command());

        LlmRequest sent = Assert.Single(_client.CompleteRequests);
        Assert.Equal(0.0d, sent.Temperature);
    }

    [Fact]
    public async Task ExecuteAsync_CapsMaxTokensAt128_EvenWithHigherSetting()
    {
        _settingsStore.AppSettings = _settingsStore.AppSettings with { AiMaxTokensPerRequest = 2_000 };

        await _useCase.ExecuteAsync(Command());

        LlmRequest sent = Assert.Single(_client.CompleteRequests);
        Assert.Equal(128, sent.MaxTokens);
    }

    [Fact]
    public async Task ExecuteAsync_LongDescription_TruncatesTo200CharsInPrompt()
    {
        string longDescription = new('a', 250);

        await _useCase.ExecuteAsync(new CategorizeExpenseCommand(longDescription));

        (string Name, string Locale) userCall = _prompts.Calls.Single(c =>
            string.Equals(c.Name, PromptNames.CategorizeExpenseUser, StringComparison.Ordinal));
        Assert.Equal(SupportedLocales.Default, userCall.Locale);

        LlmRequest sent = Assert.Single(_client.CompleteRequests);
        LlmMessage userMessage = sent.Messages.Single(m => m.Role == LlmRole.User);
        Assert.DoesNotContain(new string('a', 250), userMessage.Content, StringComparison.Ordinal);
        Assert.Contains(new string('a', 200), userMessage.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_NeverCallsCategoryRepositoryWriteMembers()
    {
        // StrictReadOnlyCategoryRepository throws on any write; a passing
        // run across every scenario above is itself the assertion (S-11).
        // This test drives one more scenario (unmatched) to be explicit.
        _client.ResponseToReturn = new LlmResponse("""{"category":"Mascotas"}""", 10, 100);

        Result result = await _useCase.ExecuteAsync(Command());

        Assert.True(result.IsSuccess);
    }
}
