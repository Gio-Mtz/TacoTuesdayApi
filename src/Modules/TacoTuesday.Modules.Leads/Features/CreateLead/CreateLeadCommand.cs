namespace TacoTuesday.Modules.Leads.Features.CreateLead;

/// <summary>
/// Body of <c>POST /api/leads</c>, exactly as the UI sends it
/// (<c>ILeadRequest</c> in <c>src/app/api/Leads/ILead.ts</c> of TacoTuesdayUI).
///
/// Every field is nullable on purpose. This is untrusted input off the wire: a
/// non-nullable <c>string</c> here would be a lie the deserializer is happy to tell,
/// and the first `name.Trim()` would throw a NullReferenceException that surfaces as a
/// 500 instead of the 400 the visitor deserves.
/// </summary>
/// <param name="Kind">`"company"` or `"candidate"`. Anything else is a validation error.</param>
/// <param name="Company">Required when <paramref name="Kind"/> is `"company"`, ignored otherwise.</param>
/// <param name="Role">Always optional. Ignored when <paramref name="Kind"/> is `"company"`.</param>
public sealed record CreateLeadCommand(
    string? Kind,
    string? Name,
    string? Email,
    string? Company,
    string? Role);

/// <summary>
/// Body of a successful <c>POST /api/leads</c> — 201 the first time, 200 after that.
/// </summary>
/// <param name="Id">The id now holding that address. The UI does not use it; support will.</param>
/// <param name="AlreadyRegistered">
/// True when the address was already on the list. This is the field that makes the
/// idempotency *visible* rather than merely correct: the form says "ya estabas en la
/// lista" instead of congratulating somebody twice.
/// </param>
public sealed record CreateLeadResponse(Guid Id, bool AlreadyRegistered);
