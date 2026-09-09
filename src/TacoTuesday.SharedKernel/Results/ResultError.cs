namespace TacoTuesday.SharedKernel;

/// <summary>
/// A machine-readable failure. <paramref name="Code"/> is for the client to switch on;
/// <paramref name="Message"/> is for a human to read.
/// </summary>
public sealed record ResultError(string Code, string Message, IReadOnlyDictionary<string, string[]>? ValidationErrors = null)
{
    public static ResultError NotFound(string message = "The requested resource was not found.") => new("NOT_FOUND", message);
    public static ResultError Conflict(string code, string message) => new(code, message);
    public static ResultError Validation(IReadOnlyDictionary<string, string[]> errors) => new("VALIDATION_FAILED", "One or more fields are invalid.", errors);
    public static ResultError Forbidden(string message = "You are not allowed to do that.") => new("FORBIDDEN", message);
}