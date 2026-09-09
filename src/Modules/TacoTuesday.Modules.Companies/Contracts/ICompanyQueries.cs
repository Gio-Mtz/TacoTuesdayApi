namespace TacoTuesday.Modules.Companies.Contracts;

public interface ICompanyQueries
{
    Task<bool> ExistsAsync(Guid companyId, CancellationToken ct);
}