using System.Collections.Concurrent;

using TacoTuesday.Modules.Leads.Domain;

namespace TacoTuesday.Modules.Leads.Persistence;

/// <summary>
/// The waiting list, in RAM. Registered as a singleton by <c>AddLeadsModule</c>.
///
/// This is deliberate scope control, not an oversight: US-004 is the endpoint and its
/// contract, US-005 is the table. Shipping them together would mean writing an EF Core
/// mapping and a migration that nobody can run until the Azure SQL connection string
/// exists, and reviewing all of it blind in one pull request.
///
/// What it costs, written down so nobody is surprised: **the list does not survive a
/// restart, and each replica keeps its own.** Container Apps scales to zero, so in
/// production today an address can be accepted and then vanish. That is why the endpoint
/// is not announced to anybody until US-005 lands — see the ADR.
/// </summary>
public sealed class InMemoryLeadStore : ILeadStore
{
    // Ordinal: the key arrives already lowercased by the handler, and a culture-aware
    // comparer would make "is this the same address" depend on the server's locale.
    private readonly ConcurrentDictionary<string, Lead> _byNormalizedEmail = new(StringComparer.Ordinal);

    public Task<LeadRegistration> RegisterAsync(Lead lead, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(lead);
        ct.ThrowIfCancellationRequested();

        // GetOrAdd is what makes this atomic. Two requests with the same address landing
        // together both get the same stored instance back, and exactly one of them is the
        // one that wrote it.
        var stored = _byNormalizedEmail.GetOrAdd(lead.NormalizedEmail, lead);
        var alreadyRegistered = !ReferenceEquals(stored, lead);

        return Task.FromResult(new LeadRegistration(stored.Id, alreadyRegistered));
    }
}
