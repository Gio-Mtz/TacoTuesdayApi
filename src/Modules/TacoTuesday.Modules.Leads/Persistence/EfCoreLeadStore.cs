using System.Data.Common;

using Microsoft.EntityFrameworkCore;

using TacoTuesday.Modules.Leads.Domain;

namespace TacoTuesday.Modules.Leads.Persistence;

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
            db.Entry(lead).State = EntityState.Detached;

            var existingId = await FindIdByNormalizedEmailOrNullAsync(lead.NormalizedEmail, ct);

            if (existingId is null)
            {
                throw;
            }

            return new LeadRegistration(existingId.Value, AlreadyRegistered: true);
        }
    }

    private async Task<Guid?> FindIdByNormalizedEmailOrNullAsync(string normalizedEmail, CancellationToken ct)
    {
        try
        {
            return await db.Leads
                .AsNoTracking()
                .Where(existing => existing.NormalizedEmail == normalizedEmail)
                .Select(existing => (Guid?)existing.Id)
                .FirstOrDefaultAsync(ct);
        }
        catch (DbException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
