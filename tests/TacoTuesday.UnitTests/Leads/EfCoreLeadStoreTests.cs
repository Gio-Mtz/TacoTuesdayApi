using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using Shouldly;

using TacoTuesday.Modules.Leads.Domain;
using TacoTuesday.Modules.Leads.Persistence;

namespace TacoTuesday.UnitTests.Leads;

/// <summary>
/// <see cref="EfCoreLeadStore"/> against a real relational database.
///
/// **On SQLite, and that is the point.** The EF Core in-memory provider does not enforce
/// unique indexes, so every test in this file would pass against a store with no constraint
/// behind it — which is precisely the bug they exist to catch. SQLite enforces the index,
/// raises a real <c>DbUpdateException</c>, and therefore exercises the same branch that Azure
/// SQL will.
///
/// **What is not tested here, deliberately:** fifty writers going at the address at the same
/// instant. Two connections to a SQLite <c>:memory:</c> database are two different databases,
/// and a shared one serializes and starts answering "database is locked" — a test that fails
/// at random is worse than a test that is not there. The guarantee under real concurrency is
/// not in this class anyway: it is the unique index, and what these tests do prove is the
/// branch that runs when the index fires. <c>InMemoryLeadStoreTests</c> keeps the fifty-way
/// version where it can run honestly.
/// </summary>
public sealed class EfCoreLeadStoreTests : IAsyncLifetime, IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private DbContextOptions<LeadsDbContext> _options = null!;

    public async Task InitializeAsync()
    {
        // The database lives for exactly as long as this connection is open. Close it and the
        // schema, the rows and the index all go — which is why it is held by the fixture and
        // every context below borrows it.
        await _connection.OpenAsync();

        _options = new DbContextOptionsBuilder<LeadsDbContext>()
            .UseSqlite(_connection)
            .Options;

        await using var db = new LeadsDbContext(_options);
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    /// <summary>
    /// Only here to satisfy CA1001: a type that holds a disposable field has to say so.
    /// xUnit disposes through <see cref="DisposeAsync"/>; SqliteConnection tolerates both.
    /// </summary>
    public void Dispose() => _connection.Dispose();

    /// <summary>
    /// A fresh context per call, like the scoped one a request gets. Sharing a single context
    /// across two registrations would let the change tracker answer the second one from
    /// memory and the database would never be asked.
    /// </summary>
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

        // The id the application generated, not one the database invented. `ValueGeneratedNever`
        // is what keeps this true; drop it and SQL Server starts handing out NEWID() values,
        // the UUID v7 ordering is gone and nobody notices until the index is fragmented.
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

        // A different person object, a different generated id, the same address typed
        // differently. This is the losing side of the race, and it is the whole contract:
        // 200 with `alreadyRegistered: true` and the id that is actually on the list.
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

        // Two columns on purpose: a confirmation mail should address somebody the way they
        // wrote their own name, and uniqueness should not care how they wrote it.
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

        // Read straight out of the column, past EF's conversion. `Kind = 1` in a query window
        // is a number somebody has to go look up; "Candidate" is not.
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
