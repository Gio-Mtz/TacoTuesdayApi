using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using TacoTuesday.Modules.Leads.Domain;

namespace TacoTuesday.Modules.Leads.Persistence;

/// <summary>
/// How a <see cref="Lead"/> becomes a row.
///
/// The lengths here are not decoration: they are the same numbers the form and
/// <c>CreateLeadHandler</c> enforce, so that a value the handler accepts can never be one
/// the database truncates. If one of the three changes, all three change the same day.
/// </summary>
internal sealed class LeadConfiguration : IEntityTypeConfiguration<Lead>
{
    /// <summary>Mirrors <c>CreateLeadHandler.MaxShort</c> and <c>MAX_SHORT</c> in waitlist.ts.</summary>
    internal const int MaxShort = 80;

    /// <summary>Mirrors <c>CreateLeadHandler.MaxEmail</c> and <c>MAX_EMAIL</c> in waitlist.ts.</summary>
    internal const int MaxEmail = 160;

    /// <summary>Room for the longest <see cref="LeadKind"/> name, with slack for a third one.</summary>
    internal const int MaxKind = 16;

    /// <summary>
    /// The name of the unique index. Named on purpose rather than left to EF's convention:
    /// this is the one constraint whose violation is a normal, expected outcome, and a
    /// human reading a deadlock or a failed deploy should not have to guess what
    /// <c>IX_Leads_NormalizedEmail1</c> was for.
    /// </summary>
    internal const string NormalizedEmailIndex = "UX_Leads_NormalizedEmail";

    public void Configure(EntityTypeBuilder<Lead> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Leads");

        builder.HasKey(lead => lead.Id);

        // The application assigns the id, and it assigns a UUID v7 — time-ordered, so new
        // rows land at the end of the clustered index instead of being inserted in the
        // middle of it. Letting the database default it to NEWID() would throw that away.
        builder.Property(lead => lead.Id)
            .ValueGeneratedNever();

        // Stored as text, not as the enum's number. A waiting list is a table somebody will
        // eyeball in a query window long before there is an admin screen, and `Kind = 1` in
        // a result grid means nothing without the source open next to it.
        builder.Property(lead => lead.Kind)
            .HasConversion<string>()
            .HasMaxLength(MaxKind)
            .IsRequired();

        builder.Property(lead => lead.Name)
            .HasMaxLength(MaxShort)
            .IsRequired();

        builder.Property(lead => lead.Email)
            .HasMaxLength(MaxEmail)
            .IsRequired();

        builder.Property(lead => lead.NormalizedEmail)
            .HasMaxLength(MaxEmail)
            .IsRequired();

        builder.Property(lead => lead.Company)
            .HasMaxLength(MaxShort);

        builder.Property(lead => lead.Role)
            .HasMaxLength(MaxShort);

        builder.Property(lead => lead.CreatedAtUtc)
            .IsRequired();

        // THE line of this file. Uniqueness is decided here, by the database, over the
        // lowercase column — never over `Email`, which keeps whatever the person typed.
        //
        // It is a constraint and not a check the code performs because a check performed by
        // the code is two statements with a gap between them, and the gap is where the
        // duplicate gets in. `EfCoreLeadStore` inserts and lets this index be the judge.
        builder.HasIndex(lead => lead.NormalizedEmail)
            .IsUnique()
            .HasDatabaseName(NormalizedEmailIndex);
    }
}
