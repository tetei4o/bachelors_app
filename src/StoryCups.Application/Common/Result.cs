namespace StoryCups.Application.Common;

/// <summary>
/// Грешка от use case: машинен код и съобщение за потребителя.
/// </summary>
public sealed record Error(string Code, string Message);

/// <summary>
/// Резултат от use case без изключения за очаквани откази (грешна парола, изтекъл токен).
/// </summary>
public sealed class Result<T>
{
    private Result(T? value, Error? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }

    public Error? Error { get; }

    public bool IsSuccess => Error is null;

    public static Result<T> Success(T value) => new(value, null);

    public static Result<T> Failure(string code, string message) => new(default, new Error(code, message));
}

/// <summary>
/// Резултат от use case, който не връща стойност.
/// </summary>
public sealed class Result
{
    private static readonly Result SuccessInstance = new(null);

    private Result(Error? error)
    {
        Error = error;
    }

    public Error? Error { get; }

    public bool IsSuccess => Error is null;

    public static Result Success() => SuccessInstance;

    public static Result Failure(string code, string message) => new(new Error(code, message));
}
