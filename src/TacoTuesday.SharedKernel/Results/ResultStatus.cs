namespace TacoTuesday.SharedKernel;

public enum ResultStatus
{
    Success = 0,
    NotFound,
    Conflict,
    Invalid,
    Forbidden,
    Unauthorized
}