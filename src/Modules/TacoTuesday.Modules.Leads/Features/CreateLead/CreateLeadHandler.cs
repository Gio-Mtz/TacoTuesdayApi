using System.Text.RegularExpressions;

using TacoTuesday.Modules.Leads.Domain;
using TacoTuesday.Modules.Leads.Persistence;
using TacoTuesday.SharedKernel;

namespace TacoTuesday.Modules.Leads.Features.CreateLead;

/// <summary>
/// Puts one address on the waiting list.
///
/// Two behaviours in here are contract, not implementation, and both are argued in
/// ADR 0004:
///
/// 1. **Posting the same address twice succeeds.** It answers 200 with
///    <c>alreadyRegistered: true</c>, never 409. The visitor did nothing wrong, and the
///    UI is already written to say so.
/// 2. **Validation mirrors the form field by field.** The browser check is a courtesy;
///    this one is the actual rule. Anything can POST straight at the endpoint.
/// </summary>
public sealed partial class CreateLeadHandler(ILeadStore store, IClock clock)
{
    /// <summary>Longest value accepted in a single-line field. Mirrors MAX_SHORT in waitlist.ts.</summary>
    private const int MaxShort = 80;

    /// <summary>Longest address accepted. Mirrors MAX_EMAIL in waitlist.ts. 254 is the RFC ceiling.</summary>
    private const int MaxEmail = 160;

    public async Task<Result<CreateLeadResponse>> Handle(CreateLeadCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var kind = ParseKind(command.Kind);
        var name = command.Name?.Trim() ?? string.Empty;
        var email = command.Email?.Trim() ?? string.Empty;
        var company = command.Company?.Trim();
        var role = command.Role?.Trim();

        var errors = Validate(kind, name, email, company, role);

        if (errors.Count > 0)
        {
            return Result<CreateLeadResponse>.Invalid(ResultError.Validation(errors));
        }

        // `kind` is non-null here: a null kind is the first thing Validate rejects.
        var isCompany = kind == LeadKind.Company;

        var lead = new Lead(
            Id: Guid.CreateVersion7(),
            Kind: kind!.Value,
            Name: name,
            Email: email,
            NormalizedEmail: Normalize(email),
            // The side that is on screen is the only side we keep. Without this, filling in
            // "Acme", switching to candidato and submitting would file "Acme" as a job title.
            Company: isCompany ? company : null,
            Role: isCompany ? null : NullIfEmpty(role),
            CreatedAtUtc: clock.UtcNow);

        var registration = await store.RegisterAsync(lead, ct);

        return Result<CreateLeadResponse>.Success(
            new CreateLeadResponse(registration.Id, registration.AlreadyRegistered));
    }

    /// <summary>
    /// The lowercase form of an address, and the only thing uniqueness is decided on.
    ///
    /// Invariant, not current-culture: with a Turkish locale on the server,
    /// <c>"ISABEL@x.com".ToLower()</c> produces a dotless ı and the same person becomes two
    /// leads depending on which machine took the request.
    /// </summary>
    public static string Normalize(string email)
    {
        ArgumentNullException.ThrowIfNull(email);
        return email.Trim().ToLowerInvariant();
    }

    private static LeadKind? ParseKind(string? kind) => kind switch
    {
        "company" => LeadKind.Company,
        "candidate" => LeadKind.Candidate,
        _ => null
    };

    private static Dictionary<string, string[]> Validate(
        LeadKind? kind,
        string name,
        string email,
        string? company,
        string? role)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (kind is null)
        {
            errors["kind"] = ["kind must be either 'company' or 'candidate'."];
        }

        if (name.Length == 0)
        {
            errors["name"] = ["name is required."];
        }
        else if (name.Length > MaxShort)
        {
            errors["name"] = [$"name must be {MaxShort} characters or fewer."];
        }

        if (email.Length == 0)
        {
            errors["email"] = ["email is required."];
        }
        else if (email.Length > MaxEmail)
        {
            errors["email"] = [$"email must be {MaxEmail} characters or fewer."];
        }
        else if (!EmailPattern().IsMatch(email))
        {
            errors["email"] = ["email is not a valid address."];
        }

        // `company` is only a field when the visitor is a company. Sending one as a
        // candidate is not an error — it is ignored, exactly as the form ignores it.
        if (kind == LeadKind.Company)
        {
            if (string.IsNullOrEmpty(company))
            {
                errors["company"] = ["company is required when kind is 'company'."];
            }
            else if (company.Length > MaxShort)
            {
                errors["company"] = [$"company must be {MaxShort} characters or fewer."];
            }
        }
        else if (role is { Length: > MaxShort })
        {
            errors["role"] = [$"role must be {MaxShort} characters or fewer."];
        }

        return errors;
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    /// <summary>
    /// Byte-for-byte the regex Angular's <c>Validators.email</c> uses.
    ///
    /// Copied rather than improved on purpose. If the server were stricter than the form,
    /// a visitor would fill in an address the page accepts and get a 400 with no field
    /// highlighted — the worst failure a form can have. If it were looser, the rule would
    /// live in the browser, where anyone can turn it off.
    ///
    /// Source-generated: the pattern is compiled at build time, so there is no regex
    /// parsing on the first request and no ReDoS surface from an interpreted backtracker.
    /// </summary>
    [GeneratedRegex(@"^(?=.{1,254}$)(?=.{1,64}@)[a-zA-Z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-zA-Z0-9!#$%&'*+/=?^_`{|}~-]+)*@[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*$")]
    private static partial Regex EmailPattern();
}
