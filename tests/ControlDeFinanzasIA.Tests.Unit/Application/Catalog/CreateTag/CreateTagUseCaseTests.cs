using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Catalog.CreateTag;
using ControlDeFinanzasIA.Domain.Catalog;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.Catalog.CreateTag;

/// <summary>
/// Unit tests for <see cref="CreateTagUseCase"/>.
/// </summary>
public sealed class CreateTagUseCaseTests
{
    private readonly FakeClock _clock;
    private readonly FakeTagRepository _repository;
    private readonly CreateTagValidator _validator;
    private readonly CreateTagUseCase _useCase;

    public CreateTagUseCaseTests()
    {
        _clock = new FakeClock
        {
            UtcNow = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc),
        };
        _repository = new FakeTagRepository();
        _validator = new CreateTagValidator();
        _useCase = new CreateTagUseCase(
            _repository,
            _clock,
            _validator,
            NullLogger<CreateTagUseCase>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidCommand_PersistsAndReturnsSuccess()
    {
        // Arrange
        CreateTagCommand command = new("Comida", "#FF8800");

        // Act
        Result result = await _useCase.ExecuteAsync(command);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Tag tag = Assert.IsType<Tag>(result.Value);
        Assert.Equal("Comida", tag.Name);
        Assert.Equal("#FF8800", tag.Color);
        Assert.Equal(_clock.UtcNow, tag.CreatedAt);

        Tag persisted = Assert.Single(_repository.AddedTags);
        Assert.Equal(tag.Id, persisted.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public async Task ExecuteAsync_WithEmptyName_ReturnsValidationFailure(string name)
    {
        // Arrange
        CreateTagCommand command = new(name, null);

        // Act
        Result result = await _useCase.ExecuteAsync(command);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("Validation", result.Error!.Code);
        Assert.NotNull(result.Error.Details);
        Assert.Contains("Name", result.Error.Details!.Keys);
        Assert.Empty(_repository.AddedTags);
    }

    [Fact]
    public async Task ExecuteAsync_WithNameOver50Chars_ReturnsValidationFailure()
    {
        // Arrange
        CreateTagCommand command = new(new string('x', 51), null);

        // Act
        Result result = await _useCase.ExecuteAsync(command);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("Validation", result.Error!.Code);
        Assert.NotNull(result.Error.Details);
        Assert.Contains("Name", result.Error.Details!.Keys);
        Assert.Empty(_repository.AddedTags);
    }

    [Fact]
    public async Task ExecuteAsync_WithColorOver9Chars_ReturnsValidationFailure()
    {
        // Arrange
        CreateTagCommand command = new("Valid", "#1234567890");

        // Act
        Result result = await _useCase.ExecuteAsync(command);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("Validation", result.Error!.Code);
        Assert.NotNull(result.Error.Details);
        Assert.Contains("Color", result.Error.Details!.Keys);
        Assert.Empty(_repository.AddedTags);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullColor_PersistsWithNullColor()
    {
        // Arrange
        CreateTagCommand command = new("Transporte", null);

        // Act
        Result result = await _useCase.ExecuteAsync(command);

        // Assert
        Assert.True(result.IsSuccess);
        Tag persisted = Assert.Single(_repository.AddedTags);
        Assert.Null(persisted.Color);
    }

    [Fact]
    public async Task ExecuteAsync_WithWhitespaceColor_NormalizesToNull()
    {
        // Arrange
        CreateTagCommand command = new("Transporte", "   ");

        // Act
        Result result = await _useCase.ExecuteAsync(command);

        // Assert
        Assert.True(result.IsSuccess);
        Tag tag = Assert.IsType<Tag>(result.Value);
        Assert.Null(tag.Color);
    }

    [Fact]
    public async Task ExecuteAsync_WithDuplicateName_ReturnsDuplicateName()
    {
        // Arrange
        _repository.AddResultOverride = Result.Failure("DuplicateName", "Ya existe un tag con ese nombre.");
        CreateTagCommand command = new("Comida", null);

        // Act
        Result result = await _useCase.ExecuteAsync(command);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("DuplicateName", result.Error!.Code);
    }

    [Fact]
    public async Task ExecuteAsync_TrimsNameAndColor()
    {
        // Arrange
        CreateTagCommand command = new("  Comida  ", "  #FF8800  ");

        // Act
        Result result = await _useCase.ExecuteAsync(command);

        // Assert
        Assert.True(result.IsSuccess);
        Tag tag = Assert.IsType<Tag>(result.Value);
        Assert.Equal("Comida", tag.Name);
        Assert.Equal("#FF8800", tag.Color);
    }
}
