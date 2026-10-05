using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using Shouldly;

using TacoTuesday.Modules.Leads.Domain;
using TacoTuesday.Modules.Leads.Persistence;

namespace TacoTuesday.UnitTests.Leads;

public sealed class EfCoreLeadStoreTests : IAsyncLifetime, IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private DbContextOptions<LeadsDbContext> _options = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();

        _options = new DbContextOptionsBuilder<LeadsDbContext>()
            .UseSqlite(_connection)
            .Options;

        await using var db = new LeadsDbContext(_options);
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    public void Dispose() => _connection.Dispose();

    private LeadsDbContext NewContext() => new(_options);

    private static Lead NewLead(string email, string? name = null) => new(
        Guid.CreateVersion7(),
        LeadKind.Company,
        name ?? "Ana",
        email,
        email.Trim().ToLowerInvariant(),
        "Acme",
        null,
        DateTimeOffset.UnixEpoch);

    [Fact]
    public async Task A_new_address_is_written_and_reported_as_new()
    {
        var lead = NewLead("ana@acme.com");

        await using var context = NewContext();
        var registration = await new EfCoreLeadStore(context).RegisterAsync(lead, CancellationToken.None);

        registration.AlreadyRegistered.ShouldBeFalse();

        registration.Id.ShouldBe(lead.Id);

        await using var reader = NewContext();
        var stored = await reader.Leads.SingleAsync(CancellationToken.None);
        stored.Id.ShouldBe(lead.Id);
        stored.Email.ShouldBe("ana@acme.com");
    }

    [Fact]
    public async Task The_same_address_in_another_case_comes_back_as_the_first_id()
    {
        var first = NewLead("Ana@Acme.com", "Ana López");

        await using (var context = NewContext())
        {
            await new EfCoreLeadStore(context).RegisterAsync(first, CancellationToken.None);
        }

        var second = NewLead("ANA@ACME.COM", "Ana");

        await using var other = NewContext();
        var registration = await new EfCoreLeadStore(other).RegisterAsync(second, CancellationToken.None);

        registration.AlreadyRegistered.ShouldBeTrue();
        registration.Id.ShouldBe(first.Id);
        registration.Id.ShouldNotBe(second.Id);

        await using var reader = NewContext();
        var rows = await reader.Leads.CountAsync(CancellationToken.None);
        rows.ShouldBe(1, "the unique index should have refused the second row, not stored it");
    }

    [Fact]
    public async Task The_address_is_kept_as_it_was_typed_and_matched_in_lowercase()
    {
        var lead = NewLead("Ana.Lopez@Acme.COM");

        await using (var context = NewContext())
        {
            await new EfCoreLeadStore(context).RegisterAsync(lead, CancellationToken.None);
        }

        await using var reader = NewContext();
        var stored = await reader.Leads.SingleAsync(CancellationToken.None);

        stored.Email.ShouldBe("Ana.Lopez@Acme.COM");
        stored.NormalizedEmail.ShouldBe("ana.lopez@acme.com");
    }

    [Fact]
    public async Task Two_different_addresses_both_go_on_the_list()
    {
        await using (var one = NewContext())
        {
            await new EfCoreLeadStore(one).RegisterAsync(NewLead("ana@acme.com"), CancellationToken.None);
        }

        await using (var two = NewContext())
        {
            var registration = await new EfCoreLeadStore(two)
                .RegisterAsync(NewLead("beto@acme.com"), CancellationToken.None);

            registration.AlreadyRegistered.ShouldBeFalse();
        }

        await using var reader = NewContext();
        (await reader.Leads.CountAsync(CancellationToken.None)).ShouldBe(2);
    }

    [Fact]
    public async Task The_kind_is_stored_as_text_so_the_table_can_be_read_by_a_human()
    {
        await using (var context = NewContext())
        {
            var candidate = new Lead(
                Guid.CreateVersion7(),
                LeadKind.Candidate,
                "Beto",
                "beto@acme.com",
                "beto@acme.com",
                null,
                "Backend",
                DateTimeOffset.UnixEpoch);

            await new EfCoreLeadStore(context).RegisterAsync(candidate, CancellationToken.None);
        }

        await using var raw = _connection.CreateCommand();
        raw.CommandText = "SELECT Kind FROM Leads";
        var value = (string?)await raw.ExecuteScalarAsync(CancellationToken.None);

        value.ShouldBe("Candidate");
    }

    [Fact]
    public async Task The_round_trip_keeps_every_field()
    {
        var lead = new Lead(
            Guid.CreateVersion7(),
            LeadKind.Candidate,
            "Beto Ramírez",
            "Beto@Acme.com",
            "beto@acme.com",
            null,
            "Backend .NET",
            new DateTimeOffset(2026, 9, 29, 19, 30, 0, TimeSpan.Zero));

        await using (var context = NewContext())
        {
            await new EfCoreLeadStore(context).RegisterAsync(lead, CancellationToken.None);
        }

        await using var reader = NewContext();
        var stored = await reader.Leads.SingleAsync(CancellationToken.None);

        stored.ShouldBe(lead);
    }
}
