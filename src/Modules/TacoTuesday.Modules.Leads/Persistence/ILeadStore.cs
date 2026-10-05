using TacoTuesday.Modules.Leads.Domain;

namespace TacoTuesday.Modules.Leads.Persistence;

public interface ILeadStore
{
    Task<LeadRegistration> RegisterAsync(Lead lead, CancellationToken ct);
}

public readonly record struct LeadRegistration(Guid Id, bool AlreadyRegistered);
