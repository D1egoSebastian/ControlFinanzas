using ControlDeFinanzasIA.Application.Backup;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Backup.CreateBackup;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.Backup;

/// <summary>Tests for <see cref="CreateBackupUseCase"/> (F-019, R-1, R-17, DD-2).</summary>
public sealed class CreateBackupUseCaseTests
{
    private readonly FakeBackupService _service = new();
    private readonly RecordingLogger<CreateBackupUseCase> _logger = new();

    private CreateBackupUseCase CreateSut() => new(_service, _logger);

    [Fact]
    public async Task ExecuteAsync_OnSuccess_PassesBackupCreatedThroughUnchanged()
    {
        var created = new BackupCreated(1234, new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc), "20260926064030_AddNotifications");
        _service.CreateResult = Result.Success(created);
        using SaveTargets.RecordingSaveTarget recording = SaveTargets.Recording();

        Result result = await CreateSut().ExecuteAsync(recording.Target);

        Assert.True(result.IsSuccess);
        Assert.Same(created, result.Value);
        Assert.Equal(1, _service.CreateCalls);
    }

    [Fact]
    public async Task ExecuteAsync_OnServiceFailure_PassesCodeAndDetailsThrough()
    {
        var details = new Dictionary<string, string[]>
        {
            [BackupErrorCodes.DetailReason] = [BackupErrorCodes.ReasonInsufficientSpace],
        };
        _service.CreateResult = Result.Failure(BackupErrorCodes.IoError, "no space", details);
        using SaveTargets.RecordingSaveTarget recording = SaveTargets.Recording();

        Result result = await CreateSut().ExecuteAsync(recording.Target);

        Assert.False(result.IsSuccess);
        Assert.Equal(BackupErrorCodes.IoError, result.Error!.Code);
        Assert.Same(details, result.Error.Details);
    }

    [Fact]
    public async Task ExecuteAsync_WhenServiceThrowsIOException_ReturnsIoErrorAndLogs()
    {
        _service.CreateException = new IOException("disk gone");
        using SaveTargets.RecordingSaveTarget recording = SaveTargets.Recording();

        Result result = await CreateSut().ExecuteAsync(recording.Target);

        Assert.False(result.IsSuccess);
        Assert.Equal(BackupErrorCodes.IoError, result.Error!.Code);
        Assert.DoesNotContain("disk gone", result.Error.Message, StringComparison.Ordinal);
        Assert.True(_logger.HasError);
    }

    [Fact]
    public async Task ExecuteAsync_WhenServiceThrowsUnexpectedException_ReturnsIoError()
    {
        _service.CreateException = new InvalidOperationException("boom");
        using SaveTargets.RecordingSaveTarget recording = SaveTargets.Recording();

        Result result = await CreateSut().ExecuteAsync(recording.Target);

        Assert.Equal(BackupErrorCodes.IoError, result.Error!.Code);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_PropagatesOperationCanceledException()
    {
        _service.CreateException = new OperationCanceledException();
        using SaveTargets.RecordingSaveTarget recording = SaveTargets.Recording();

        await Assert.ThrowsAsync<OperationCanceledException>(() => CreateSut().ExecuteAsync(recording.Target));
    }

    [Fact]
    public async Task ExecuteAsync_HandsTheServiceTheTargetOpener_AndNeverOpensItItself()
    {
        using SaveTargets.RecordingSaveTarget recording = SaveTargets.Recording();

        await CreateSut().ExecuteAsync(recording.Target);

        Assert.Equal(0, recording.OpenCount);
        Assert.NotNull(_service.LastOpenDestination);

        await _service.LastOpenDestination!(CancellationToken.None);
        Assert.Equal(1, recording.OpenCount);
    }

    [Fact]
    public async Task ExecuteAsync_ForwardsCancellationTokenToTheService()
    {
        using var cts = new CancellationTokenSource();
        using SaveTargets.RecordingSaveTarget recording = SaveTargets.Recording();

        await CreateSut().ExecuteAsync(recording.Target, cts.Token);

        Assert.Equal(cts.Token, _service.LastCreateToken);
    }

    [Fact]
    public Task ExecuteAsync_WithNullTarget_ThrowsArgumentNullException() =>
        Assert.ThrowsAsync<ArgumentNullException>(() => CreateSut().ExecuteAsync(null!));
}
