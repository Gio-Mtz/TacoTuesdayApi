using TacoTuesday.Modules.Leads.Domain;

namespace TacoTuesday.Modules.Leads.Persistence;

/// <summary>
/// The one thing the waiting list has to do: take an address, and be honest about
/// whether it was already there.
/// </summary>
public interface ILeadStore
{
    /// <summary>
    /// Stores <paramref name="lead"/>, unless its <see cref="Lead.NormalizedEmail"/> is
    /// already on the list — in which case nothing is written and the existing row wins.
    /// </summary>
    /// <returns>
    /// The id that is now on the list for that address, and whether it was already there.
    /// Never a failure: signing up twice is not an error, it is the same outcome reached
    /// twice. See ADR 0004.
    /// </returns>
    Task<LeadRegistration> RegisterAsync(Lead lead, CancellationToken ct);
}

/// <summary>Outcome of putting an address on the list.</summary>
/// <param name="Id">The id of the lead that holds that address — the new one or the old one.</param>
/// <param name="AlreadyRegistered">True when the address was already on the list.</param>
public readonly record struct LeadRegistration(Guid Id, bool AlreadyRegistered);
