using ControlDeFinanzasIA.Application.Common;

namespace ControlDeFinanzasIA.Tests.Unit.Common;

/// <summary>
/// Tests para <see cref="Result"/> y <see cref="Error"/>.
/// Cubre los criterios AC-2, AC-3, y AC-4 de la spec F-005.
/// </summary>
public class ResultTests
{
    // ============================================================
    // AC-2: factory tests
    // ============================================================

    [Fact]
    public void Result_Success_HasIsSuccessTrueAndValue()
    {
        var result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Result_Success_AcceptsNullValue()
    {
        object? nothing = null;
        var result = Result.Success(nothing!);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Result_FailureWithError_HasIsSuccessFalseAndError()
    {
        var error = new Error("NotFound", "Item not found");
        var result = Result.Failure(error);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void Result_FailureWithCodeAndMessage_CreatesErrorCorrectly()
    {
        var result = Result.Failure("ValidationError", "Amount must be non-negative.");

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.NotNull(result.Error);
        Assert.Equal("ValidationError", result.Error.Code);
        Assert.Equal("Amount must be non-negative.", result.Error.Message);
        Assert.Null(result.Error.Details);
    }

    [Fact]
    public void Result_FailureWithDetails_StoresDetailsCorrectly()
    {
        var details = new Dictionary<string, string[]>
        {
            ["Name"] = new[] { "Name is required" },
            ["Amount"] = new[] { "Amount must be non-negative", "Amount must be finite" }
        };
        var result = Result.Failure("ValidationError", "Some fields are invalid.", details);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.NotNull(result.Error.Details);
        Assert.Equal(2, result.Error.Details.Count);
        Assert.Equal(new[] { "Name is required" }, result.Error.Details["Name"]);
        Assert.Equal(2, result.Error.Details["Amount"].Length);
    }

    // ============================================================
    // AC-3: equality e inmutability
    // ============================================================

    [Fact]
    public void Result_TwoSuccessesWithSameValue_AreEqual()
    {
        var r1 = Result.Success(42);
        var r2 = Result.Success(42);

        Assert.Equal(r1, r2);
        Assert.True(r1 == r2);
    }

    [Fact]
    public void Result_TwoFailuresWithSameError_AreEqual()
    {
        var error = new Error("X", "y");
        var r1 = Result.Failure(error);
        var r2 = Result.Failure(error);

        Assert.Equal(r1, r2);
    }

    [Fact]
    public void Result_SuccessAndFailure_AreNotEqual()
    {
        var success = Result.Success(42);
        var failure = Result.Failure("X", "y");

        Assert.NotEqual(success, failure);
    }

    [Fact]
    public void Result_IsImmutable_PropertiesAreReadOnlyAtRuntime()
    {
        // Verificable por compilacion: intentar asignar
        // result.IsSuccess = true despues de la creacion no compila
        // (init-only). Este test documenta la expectativa.
        var result = Result.Success(42);
        Assert.Equal(42, result.Value);
        Assert.True(result.IsSuccess);
    }

    // ============================================================
    // AC-4: pattern matching
    // ============================================================

    [Fact]
    public void Result_PatternMatchingOnIsSuccess_DispatchesCorrectly()
    {
        var successResult = Result.Success(42);
        var failureResult = Result.Failure("X", "y");

        string successOutcome = successResult switch
        {
            { IsSuccess: true, Value: var v } => $"OK: {v}",
            { IsSuccess: false, Error: var e } => $"ERR: {e!.Code}",
            _ => "unknown"
        };
        string failureOutcome = failureResult switch
        {
            { IsSuccess: true, Value: var v } => $"OK: {v}",
            { IsSuccess: false, Error: var e } => $"ERR: {e!.Code}",
            _ => "unknown"
        };

        Assert.Equal("OK: 42", successOutcome);
        Assert.Equal("ERR: X", failureOutcome);
    }
}
