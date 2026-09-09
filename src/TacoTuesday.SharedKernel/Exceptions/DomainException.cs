namespace TacoTuesday.SharedKernel;

/// <summary>
/// A business invariant was violated. This means a BUG — some code called a domain
/// method with state that should have been checked first. It is NOT for expected
/// failures like "email already registered": those return a Result.
/// </summary>

public sealed class DomaingException(string message) : Exception(message);