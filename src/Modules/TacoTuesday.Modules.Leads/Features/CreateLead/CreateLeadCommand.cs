namespace TacoTuesday.Modules.Leads.Features.CreateLead;

public sealed record CreateLeadCommand(
    string? Kind,
    string? Name,
    string? Email,
    string? Company,
    string? Role);

public sealed record CreateLeadResponse(Guid Id, bool AlreadyRegistered);
