using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.SavedViews.DeleteSavedView;
using ControlDeFinanzasIA.Domain.Enums;
using ControlDeFinanzasIA.Domain.Views;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.SavedViews.DeleteSavedView;

/// <summary>
/// Unit tests for <see cref="DeleteSavedViewUseCase"/>.
/// </summary>
public sealed class DeleteSavedViewUseCaseTests
{
    private readonly FakeSavedViewRepository _repository;
    private readonly DeleteSavedViewUseCase _useCase;

    public DeleteSavedViewUseCaseTests()
    {
        _repository = new FakeSavedViewRepository();
        _useCase = new DeleteSavedViewUseCase(
            _repository,
            NullLogger<DeleteSavedViewUseCase>.Instance);
    }

    private static SavedView CreateSavedView(string name)
        => SavedView.Create(
            name,
            TransactionType.Expense,
            "{}",
            new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc));

    [Fact]
    public async Task ExecuteAsync_WithValidId_DeletesAndReturnsSuccess()
    {
        // Arrange
        SavedView savedView = CreateSavedView("Vista 1");
        _ = await _repository.AddAsync(savedView);
        DeleteSavedViewCommand command = new(savedView.Id);

        // Act
        Result result = await _useCase.ExecuteAsync(command);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(_repository.AddedSavedViews);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyId_ReturnsValidationFailure()
    {
        // Arrange
        DeleteSavedViewCommand command = new(Guid.Empty);

        // Act
        Result result = await _useCase.ExecuteAsync(command);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("Validation", result.Error!.Code);
        Assert.Equal("Id cannot be empty.", result.Error.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WithNonExistentId_PropagatesNotFound()
    {
        // Arrange
        _repository.RemoveResultOverride = Result.Failure("NotFound", "La vista guardada no existe.");
        DeleteSavedViewCommand command = new(Guid.NewGuid());

        // Act
        Result result = await _useCase.ExecuteAsync(command);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("NotFound", result.Error!.Code);
    }

    [Fact]
    public async Task ExecuteAsync_WithRepositoryError_PropagatesError()
    {
        // Arrange
        _repository.RemoveResultOverride = Result.Failure("DeleteFailed", "No se pudo eliminar la vista.");
        DeleteSavedViewCommand command = new(Guid.NewGuid());

        // Act
        Result result = await _useCase.ExecuteAsync(command);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("DeleteFailed", result.Error!.Code);
    }
}
