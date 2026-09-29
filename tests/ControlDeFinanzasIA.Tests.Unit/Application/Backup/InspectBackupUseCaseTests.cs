using ControlDeFinanzasIA.Application.Backup;
using ControlDeFinanzasIA.Application.Common;
using ControlDeFinanzasIA.Application.UseCases.Backup.InspectBackup;
using ControlDeFinanzasIA.Tests.Unit.TestDoubles;
using Xunit;

namespace ControlDeFinanzasIA.Tests.Unit.Application.Backup;

/// <summary>Tests for <see cref="InspectBackupUseCase"/> (F-019, R-1, R-17, DD-2).</summary>
public sealed class InspectBackupUseCaseTests
{
    private readonly FakeBackupService _service = new();
    private readonly RecordingLogger<InspectBackupUseCase> _logger = new();

    private InspectBackupUseCase CreateSut() => new(_service, _logger);

    [Fact]
    public async Task ExecuteAsync_OnSuccess_PassesBackupInfoThroughUnchanged()
    {
        var info = new BackupInfo(new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc), "1.2.3", "20260926064030_AddNotifications", false, false, 4096);
        _service.InspectResult = Result.Success(info);
        using OpenTargets.RecordingOpenTarget recording = OpenTargets.Recording();

        Result result = await CreateSut().ExecuteAsync(recording.Target);

        Assert.True(result.IsSuccess);
        Assert.Same(info, result.Value);
        Assert.Equal(1, _service.InspectCalls);
        Assert.Equal(0, _service.RestoreCalls);
    }

    [Fact]
    public async Task ExecuteAsync_OnServiceFailure_PassesCodeAndDetailsThrough()
    {
        var details = new Dictionary<string, string[]> { [BackupErrorCodes.DetailBackupSchema] = ["x"] };
        _service.InspectResult = Result.Failure(BackupErrorCodes.NewerSchema, "newer", details);
        using OpenTargets.RecordingOpenTarget recording = OpenTargets.Recording();

        Result result = await CreateSut().ExecuteAsync(recording.Target);

        Assert.False(result.IsSuccess);
        Assert.Equal(BackupErrorCodes.NewerSchema, result.Error!.Code);
        Assert.Same(details, result.Error.Details);
    }

    [Fact]
    public async Task ExecuteAsync_WhenServiceThrows_ReturnsIoErrorAndLogs()
    {
        _service.InspectException = new IOException("secret path");
        using OpenTargets.RecordingOpenTarget recording = OpenTargets.Recording();

        Result result = await CreateSut().ExecuteAsync(recording.Target);

        Assert.Equal(BackupErrorCodes.IoError, result.Error!.Code);
        Assert.DoesNotContain("secret", result.Error.Message, StringComparison.Ordinal);
        Assert.True(_logger.HasError);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_PropagatesOperationCanceledException()
    {
        _service.InspectException = new OperationCanceledException();
        using OpenTargets.RecordingOpenTarget recording = OpenTargets.Recording();

        await Assert.ThrowsAsync<OperationCanceledException>(() => CreateSut().ExecuteAsync(recording.Target));
    }

    [Fact]
    public async Task ExecuteAsync_HandsTheServiceTheTargetOpener_AndNeverOpensItItself()
    {
        using OpenTargets.RecordingOpenTarget recording = OpenTargets.Recording();

        await CreateSut().ExecuteAsync(recording.Target);

        Assert.Equal(0, recording.OpenCount);
        Assert.NotNull(_service.LastOpenRead);

        using Stream stream = await _service.LastOpenRead!(CancellationToken.None);
        Assert.Equal(1, recording.OpenCount);
    }

    [Fact]
    public Task ExecuteAsync_WithNullTarget_ThrowsArgumentNullException() =>
        Assert.ThrowsAsync<ArgumentNullException>(() => CreateSut().ExecuteAsync(null!));
}
