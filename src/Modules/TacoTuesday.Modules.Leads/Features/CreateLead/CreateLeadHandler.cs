using System.Text.RegularExpressions;

using TacoTuesday.Modules.Leads.Domain;
using TacoTuesday.Modules.Leads.Persistence;
using TacoTuesday.SharedKernel;

namespace TacoTuesday.Modules.Leads.Features.CreateLead;

public sealed partial class CreateLeadHandler(ILeadStore store, IClock clock)
{
    private const int MaxShort = 80;

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

        var isCompany = kind == LeadKind.Company;

        var lead = new Lead(
            Id: Guid.CreateVersion7(),
            Kind: kind!.Value,
            Name: name,
            Email: email,
            NormalizedEmail: Normalize(email),

            Company: isCompany ? company : null,
            Role: isCompany ? null : NullIfEmpty(role),
            CreatedAtUtc: clock.UtcNow);

        var registration = await store.RegisterAsync(lead, ct);

        return Result<CreateLeadResponse>.Success(
            new CreateLeadResponse(registration.Id, registration.AlreadyRegistered));
    }

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

    [GeneratedRegex(@"^(?=.{1,254}$)(?=.{1,64}@)[a-zA-Z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-zA-Z0-9!#$%&'*+/=?^_`{|}~-]+)*@[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*$")]
    private static partial Regex EmailPattern();
}
