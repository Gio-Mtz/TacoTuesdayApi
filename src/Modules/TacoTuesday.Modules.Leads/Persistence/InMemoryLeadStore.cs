using System.Collections.Concurrent;

using TacoTuesday.Modules.Leads.Domain;

namespace TacoTuesday.Modules.Leads.Persistence;

public sealed class InMemoryLeadStore : ILeadStore
{
    private readonly ConcurrentDictionary<string, Lead> _byNormalizedEmail = new(StringComparer.Ordinal);

    public Task<LeadRegistration> RegisterAsync(Lead lead, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(lead);
        ct.ThrowIfCancellationRequested();

        var stored = _byNormalizedEmail.GetOrAdd(lead.NormalizedEmail, lead);
        var alreadyRegistered = !ReferenceEquals(stored, lead);

        return Task.FromResult(new LeadRegistration(stored.Id, alreadyRegistered));
    }
}
