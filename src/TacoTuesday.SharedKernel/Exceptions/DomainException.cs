namespace TacoTuesday.SharedKernel;

public sealed class DomaingException(string message) : Exception(message);