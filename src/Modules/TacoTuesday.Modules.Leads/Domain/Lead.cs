namespace TacoTuesday.Modules.Leads.Domain;

/// <summary>
/// One address on the waiting list.
/// </summary>
/// <param name="Id">
/// A UUID v7. Version 7 is time-ordered, so rows land at the end of a clustered index
/// instead of scattering across it. That matters in US-005, where this becomes a SQL
/// table: a random v4 primary key on Azure SQL fragments the index from the first
/// thousand rows.
/// </param>
/// <param name="Email">
/// As the person typed it, only trimmed. Case is preserved because a confirmation mail
/// should be addressed the way they wrote their own name.
/// </param>
/// <param name="NormalizedEmail">
/// The lowercase form, and the only thing uniqueness is ever decided on. US-005 puts the
/// unique index on this column, not on <see cref="Email"/>.
/// </param>
public sealed record Lead(
    Guid Id,
    LeadKind Kind,
    string Name,
    string Email,
    string NormalizedEmail,
    string? Company,
    string? Role,
    DateTimeOffset CreatedAtUtc);
