namespace TacoTuesday.SharedKernel;

public readonly record struct Result
{
    public readonly ResultStatus Status {get;}
    public readonly ResultError? Error {get;}
    private Result(ResultStatus status, ResultError? error)
    {
        Status = status;
        Error = error;
    }
    public bool IsSuccess => Status == ResultStatus.Success;
    public static Result Success() => new(ResultStatus.Success, null);
    public static Result NotFound(string message = "Not Found.") => new(ResultStatus.NotFound, ResultError.NotFound(message));
    public static Result Conflict(string code, string message) => new(ResultStatus.Conflict, ResultError.Conflict(code, message));
    public static Result Invalid(ResultError error) => new (ResultStatus.Invalid, error);
    public static Result Forbidden(string message = "forbidden.") => new(ResultStatus.Forbidden, ResultError.Forbidden(message));
}

[SuppressMessage("Design", "CA1000:Do not declare static members on generic types",
    Justification = "Static factory methods are the point of the Result pattern. The BCL does the same " +
                    "(ImmutableArray<T>.Empty, EqualityComparer<T>.Default). The implicit conversion " +
                    "from T covers the happy path, so callers rarely write the type argument.")]
public readonly record struct Result<T>
{
    public ResultStatus Status {get;}
    public ResultError? Error {get;}
    public T? Value {get;}
    public Result(ResultStatus status, T? value, ResultError? error)
    {
        Status = status;
        Error = error;
        Value = value;
    }
    public bool IsSuccess => Status == ResultStatus.Success;
    public static Result<T> Success(T value) => new (ResultStatus.Success, value, null);
    public static Result<T> NotFound(string message = "Not Found.") => new(ResultStatus.NotFound, default, ResultError.NotFound(message));
    public static Result<T> Conflict(string code, string message) => new(ResultStatus.Conflict, default, ResultError.Conflict(code, message));
    public static Result<T> Invalid(ResultError error) => new(ResultStatus.Invalid, default, error);
    public static Result<T> Forbidden(string message = "Forbidden.") => new(ResultStatus.Forbidden, default, ResultError.Forbidden(message));
    public static implicit operator Result<T>(T value) => Success(value);
}
