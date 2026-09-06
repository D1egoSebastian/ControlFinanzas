namespace ControlDeFinanzasIA.Application.Common;

/// <summary>
/// Representa un error de negocio que un use case o query puede
/// reportar a través de <see cref="Result"/>. Es un dato
/// estructurado, no una excepcion.
/// </summary>
public record Error(
    string Code,
    string Message,
    IReadOnlyDictionary<string, string[]>? Details = null);

/// <summary>
/// Representa el resultado de un use case o query: exito (con un
/// valor) o fallo (con un <see cref="Error"/>). Es el patron
/// estandar de retorno en Application.
/// </summary>
/// <remarks>
/// Version non-generic con <see cref="Value"/> de tipo
/// <see cref="object"/>?. Es una simplificacion que evita
/// problemas de inferencia de tipos en el compiler con genericos.
/// El caller debe hacer cast del <see cref="Value"/> al tipo
/// esperado: <c>(MiTipo)result.Value</c>.
/// </remarks>
public sealed record Result
{
    public bool IsSuccess { get; init; }
    public object? Value { get; init; }
    public Error? Error { get; init; }

    private Result(bool isSuccess, object? value, Error? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public static Result Success(object value) => new(true, value, null);
    public static Result Failure(Error error) => new(false, null, error);
    public static Result Failure(string code, string message) =>
        Failure(new Error(code, message));
    public static Result Failure(
        string code,
        string message,
        IReadOnlyDictionary<string, string[]> details) =>
        Failure(new Error(code, message, details));
}
