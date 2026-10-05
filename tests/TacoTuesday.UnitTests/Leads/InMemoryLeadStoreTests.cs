using Shouldly;

using TacoTuesday.Modules.Leads.Domain;
using TacoTuesday.Modules.Leads.Persistence;

namespace TacoTuesday.UnitTests.Leads;

public sealed class InMemoryLeadStoreTests
{
    private static Lead NewLead(string email) => new(
        Guid.CreateVersion7(),
        LeadKind.Company,
        "Ana",
        email,
        email.ToLowerInvariant(),
        "Acme",
        null,
        DateTimeOffset.UnixEpoch);

    [Fact]
    public async Task A_new_address_is_added_and_reported_as_new()
    {
        var store = new InMemoryLeadStore();
        var lead = NewLead("ana@acme.com");

        var registration = await store.RegisterAsync(lead, CancellationToken.None);

        registration.AlreadyRegistered.ShouldBeFalse();
        registration.Id.ShouldBe(lead.Id);
    }

    [Fact]
    public async Task A_second_registration_keeps_the_first_id()
    {
        var store = new InMemoryLeadStore();
        var first = NewLead("ana@acme.com");
        var second = NewLead("ana@acme.com");

        await store.RegisterAsync(first, CancellationToken.None);
        var registration = await store.RegisterAsync(second, CancellationToken.None);

        registration.AlreadyRegistered.ShouldBeTrue();
        registration.Id.ShouldBe(first.Id);
        registration.Id.ShouldNotBe(second.Id);
    }

    [Fact]
    public async Task Fifty_simultaneous_registrations_of_one_address_produce_exactly_one_lead()
    {
        var store = new InMemoryLeadStore();

        var registrations = await Task.WhenAll(
            Enumerable.Range(0, 50).Select(_ =>
                Task.Run(() => store.RegisterAsync(NewLead("ana@acme.com"), CancellationToken.None))));

        registrations.Count(r => !r.AlreadyRegistered).ShouldBe(1, "exactly one caller should have written the row");
        registrations.Select(r => r.Id).Distinct().Count().ShouldBe(1, "everybody should end up pointing at the same lead");
    }
}
