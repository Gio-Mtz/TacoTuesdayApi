namespace TacoTuesday.Modules.Leads.Domain;

/// <summary>
/// Which side of the marketplace the lead is on.
///
/// The wire format is the lowercase string the UI sends (`"company"` / `"candidate"`,
/// see `src/app/api/Leads/ILead.ts` in TacoTuesdayUI). Parsing lives in the handler
/// so that an unknown value comes back as a validation error the visitor can be told
/// about, instead of a 400 produced by the JSON deserializer with no field name in it.
/// </summary>
public enum LeadKind
{
    Company = 0,
    Candidate = 1
}
