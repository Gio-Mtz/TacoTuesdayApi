using Microsoft.EntityFrameworkCore;

using TacoTuesday.Modules.Leads.Domain;

namespace TacoTuesday.Modules.Leads.Persistence;

/// <summary>
/// The waiting list, in the database. Replaces <see cref="InMemoryLeadStore"/> as the
/// registration the host makes — the handler never notices, because it has only ever
/// depended on <see cref="ILeadStore"/>.
///
/// **How the duplicate is resolved, and why it is not a SELECT.**
/// The obvious shape is "look for the address; if it is not there, insert it". That is two
/// statements with a gap in between, and two visitors who submit the same address inside
/// that gap both find nothing and both insert. It works in every test and fails in
/// production, once, months later, and the evidence is a duplicate row nobody can explain.
///
/// So this inserts first and lets the unique index be the judge. Exactly one INSERT can
/// win; the loser comes back as a <see cref="DbUpdateException"/>, and only then — on the
/// losing branch, which is the rare one — does a SELECT run, to find out which id won.
/// The database decides, in one statement, and the race has nowhere to happen.
///
/// **Why the exception is not inspected.** Catching "SQL Server error 2601" would tie this
/// class to one provider and make the unit tests, which run on SQLite so the index is real,
/// test a different code path than production. Instead: if the address is on the list after
/// the failure, it was a duplicate; if it is not, the failure was something else and the
/// exception is rethrown untouched.
/// </summary>
public sealed class EfCoreLeadStore(LeadsDbContext db) : ILeadStore
{
    public async Task<LeadRegistration> RegisterAsync(Lead lead, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(lead);
        ArgumentNullException.ThrowIfNull(db);

        db.Leads.Add(lead);

        try
        {
            await db.SaveChangesAsync(ct);

            return new LeadRegistration(lead.Id, AlreadyRegistered: false);
        }
        catch (DbUpdateException)
        {
            // The failed INSERT is still sitting in the change tracker as Added. Left there,
            // the next SaveChanges on this same scoped context would try it again and fail
            // again — so it goes, and the context stays usable.
            db.Entry(lead).State = EntityState.Detached;

            var existingId = await db.Leads
                .AsNoTracking()
                .Where(existing => existing.NormalizedEmail == lead.NormalizedEmail)
                .Select(existing => (Guid?)existing.Id)
                .FirstOrDefaultAsync(ct);

            // Nothing there: the INSERT did not lose a race, it failed for some other reason
            // — a dropped connection, a truncated column, a table that is not there yet.
            // That is not "already registered", and swallowing it would tell a visitor they
            // are on a list they are not on.
            if (existingId is null)
            {
                throw;
            }

            return new LeadRegistration(existingId.Value, AlreadyRegistered: true);
        }
    }
}
