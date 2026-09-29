using System.Collections.Concurrent;

using TacoTuesday.Modules.Leads.Domain;

namespace TacoTuesday.Modules.Leads.Persistence;

/// <summary>
/// The waiting list, in RAM.
///
/// **No longer what production uses.** US-005 replaced the registration with
/// <see cref="EfCoreLeadStore"/>; this stays because the endpoint's integration tests
/// run against it on purpose. Those tests are about routing, binding, CORS and the rate
/// limiter, and pointing them at a database would make every one of them able to fail
/// for a reason that has nothing to do with what they are testing. The database path has
/// its own tests, on SQLite, in <c>EfCoreLeadStoreTests</c>.
///
/// What it cost while it WAS production, kept here because it is the reason US-005 exists:
/// the list did not survive a restart and each replica kept its own, and Container Apps
/// scales to zero — so an address could be accepted and then vanish. Do not put this back
/// behind the endpoint.
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
