namespace TacoTuesday.Modules.Candidates.Contracts;

public interface ICandidateQueries
{
    Task<bool> ExistsAsync(Guid candidateProfileId, CancellationToken ct);
}