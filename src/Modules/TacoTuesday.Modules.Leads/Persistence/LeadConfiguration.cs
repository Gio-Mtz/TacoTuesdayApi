using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using TacoTuesday.Modules.Leads.Domain;

namespace TacoTuesday.Modules.Leads.Persistence;

internal sealed class LeadConfiguration : IEntityTypeConfiguration<Lead>
{
    internal const int MaxShort = 80;

    internal const int MaxEmail = 160;

    internal const int MaxKind = 16;

    internal const string NormalizedEmailIndex = "UX_Leads_NormalizedEmail";

    public void Configure(EntityTypeBuilder<Lead> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Leads");

        builder.HasKey(lead => lead.Id);

        builder.Property(lead => lead.Id)
            .ValueGeneratedNever();

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

        builder.HasIndex(lead => lead.NormalizedEmail)
            .IsUnique()
            .HasDatabaseName(NormalizedEmailIndex);
    }
}
