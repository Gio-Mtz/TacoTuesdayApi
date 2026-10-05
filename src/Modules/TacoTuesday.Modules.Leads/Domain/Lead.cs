namespace TacoTuesday.Modules.Leads.Domain;

public sealed record Lead(
    Guid Id,
    LeadKind Kind,
    string Name,
    string Email,
    string NormalizedEmail,
    string? Company,
    string? Role,
    DateTimeOffset CreatedAtUtc);
