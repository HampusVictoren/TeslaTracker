namespace TeslaTracker.Domain.SeedWork;

public readonly record struct Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);
}

public readonly struct Result<T>
{
    private readonly T? _value;

    private Result(T value)
    {
        _value = value;
        Error = Error.None;
        IsSuccess = true;
    }

    private Result(Error error)
    {
        _value = default;
        Error = error;
        IsSuccess = false;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot access Value of a failed Result ({Error.Code}: {Error.Message}).");

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(Error error) => new(error);
    public static Result<T> Failure(string code, string message) => new(new Error(code, message));

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error error) => Failure(error);
}
