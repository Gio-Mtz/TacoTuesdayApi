using Shouldly;

using TacoTuesday.Modules.Leads.Domain;
using TacoTuesday.Modules.Leads.Features.CreateLead;
using TacoTuesday.Modules.Leads.Persistence;
using TacoTuesday.SharedKernel;

namespace TacoTuesday.UnitTests.Leads;

public sealed class CreateLeadHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 29, 18, 30, 0, TimeSpan.Zero);

    private static CreateLeadCommand ValidCompany(string email = "ana@acme.com") =>
        new("company", "Ana López", email, "Acme", null);

    private static CreateLeadCommand ValidCandidate(string email = "luis@correo.com") =>
        new("candidate", "Luis Pérez", email, null, "Backend");

    private static CreateLeadHandler HandlerOver(ILeadStore store) =>
        new(store, new FixedClock(Now));

    [Fact]
    public async Task A_valid_company_lead_is_accepted()
    {
        var handler = HandlerOver(new InMemoryLeadStore());

        var result = await handler.Handle(ValidCompany(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.AlreadyRegistered.ShouldBeFalse();
        result.Value!.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task A_candidate_does_not_need_a_company()
    {
        var handler = HandlerOver(new InMemoryLeadStore());

        var result = await handler.Handle(ValidCandidate(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task The_same_address_twice_is_a_success_not_a_conflict()
    {
        var handler = HandlerOver(new InMemoryLeadStore());

        var first = await handler.Handle(ValidCompany(), CancellationToken.None);
        var second = await handler.Handle(ValidCompany(), CancellationToken.None);

        second.IsSuccess.ShouldBeTrue();
        second.Status.ShouldNotBe(ResultStatus.Conflict);
        second.Value!.AlreadyRegistered.ShouldBeTrue();
        second.Value!.Id.ShouldBe(first.Value!.Id);
    }

    [Theory]
    [InlineData("ANA@ACME.COM")]
    [InlineData("Ana@Acme.Com")]
    [InlineData("  ana@acme.com  ")]
    public async Task Case_and_surrounding_spaces_do_not_make_a_second_lead(string second)
    {
        var handler = HandlerOver(new InMemoryLeadStore());

        var first = await handler.Handle(ValidCompany("ana@acme.com"), CancellationToken.None);
        var again = await handler.Handle(ValidCompany(second), CancellationToken.None);

        again.Value!.AlreadyRegistered.ShouldBeTrue();
        again.Value!.Id.ShouldBe(first.Value!.Id);
    }

    [Fact]
    public async Task Two_different_addresses_are_two_leads()
    {
        var handler = HandlerOver(new InMemoryLeadStore());

        var first = await handler.Handle(ValidCompany("ana@acme.com"), CancellationToken.None);
        var other = await handler.Handle(ValidCompany("beto@acme.com"), CancellationToken.None);

        other.Value!.AlreadyRegistered.ShouldBeFalse();
        other.Value!.Id.ShouldNotBe(first.Value!.Id);
    }

    [Fact]
    public async Task The_address_is_stored_as_typed_and_normalised_separately()
    {
        var store = new RecordingLeadStore();

        await HandlerOver(store).Handle(ValidCompany("  Ana@Acme.COM "), CancellationToken.None);

        store.Last!.Email.ShouldBe("Ana@Acme.COM");
        store.Last!.NormalizedEmail.ShouldBe("ana@acme.com");
    }

    [Fact]
    public async Task A_company_name_sent_by_a_candidate_is_dropped()
    {
        var store = new RecordingLeadStore();
        var command = new CreateLeadCommand("candidate", "Luis", "luis@correo.com", "Acme", "Backend");

        await HandlerOver(store).Handle(command, CancellationToken.None);

        store.Last!.Kind.ShouldBe(LeadKind.Candidate);
        store.Last!.Company.ShouldBeNull();
        store.Last!.Role.ShouldBe("Backend");
    }

    [Fact]
    public async Task A_role_sent_by_a_company_is_dropped()
    {
        var store = new RecordingLeadStore();
        var command = new CreateLeadCommand("company", "Ana", "ana@acme.com", "Acme", "Backend");

        await HandlerOver(store).Handle(command, CancellationToken.None);

        store.Last!.Kind.ShouldBe(LeadKind.Company);
        store.Last!.Company.ShouldBe("Acme");
        store.Last!.Role.ShouldBeNull();
    }

    [Fact]
    public async Task An_empty_role_is_stored_as_null_not_as_an_empty_string()
    {
        var store = new RecordingLeadStore();
        var command = new CreateLeadCommand("candidate", "Luis", "luis@correo.com", null, "   ");

        await HandlerOver(store).Handle(command, CancellationToken.None);

        store.Last!.Role.ShouldBeNull();
    }

    [Fact]
    public async Task The_creation_time_comes_from_the_clock_never_from_DateTimeOffset_UtcNow()
    {
        var store = new RecordingLeadStore();

        await HandlerOver(store).Handle(ValidCompany(), CancellationToken.None);

        store.Last!.CreatedAtUtc.ShouldBe(Now);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_missing_name_is_rejected(string? name)
    {
        var result = await HandlerOver(new InMemoryLeadStore())
            .Handle(new CreateLeadCommand("company", name, "ana@acme.com", "Acme", null), CancellationToken.None);

        ShouldFailOn(result, "name");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sin-arroba")]
    [InlineData("ana@")]
    [InlineData("@acme.com")]
    [InlineData("ana @acme.com")]
    public async Task A_bad_address_is_rejected(string? email)
    {
        var result = await HandlerOver(new InMemoryLeadStore())
            .Handle(new CreateLeadCommand("company", "Ana", email, "Acme", null), CancellationToken.None);

        ShouldFailOn(result, "email");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_company_without_a_company_name_is_rejected(string? company)
    {
        var result = await HandlerOver(new InMemoryLeadStore())
            .Handle(new CreateLeadCommand("company", "Ana", "ana@acme.com", company, null), CancellationToken.None);

        ShouldFailOn(result, "company");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("empresa")]
    [InlineData("Company")]
    [InlineData("recruiter")]
    public async Task An_unknown_kind_is_rejected(string? kind)
    {
        var result = await HandlerOver(new InMemoryLeadStore())
            .Handle(new CreateLeadCommand(kind, "Ana", "ana@acme.com", "Acme", null), CancellationToken.None);

        ShouldFailOn(result, "kind");
    }

    [Fact]
    public async Task A_name_over_eighty_characters_is_rejected()
    {
        var result = await HandlerOver(new InMemoryLeadStore())
            .Handle(new CreateLeadCommand("company", new string('a', 81), "ana@acme.com", "Acme", null), CancellationToken.None);

        ShouldFailOn(result, "name");
    }

    [Fact]
    public async Task A_name_of_exactly_eighty_characters_is_fine()
    {
        var result = await HandlerOver(new InMemoryLeadStore())
            .Handle(new CreateLeadCommand("company", new string('a', 80), "ana@acme.com", "Acme", null), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task An_address_over_one_hundred_and_sixty_characters_is_rejected()
    {
        var tooLong = new string('a', 160) + "@acme.com";

        var result = await HandlerOver(new InMemoryLeadStore())
            .Handle(new CreateLeadCommand("company", "Ana", tooLong, "Acme", null), CancellationToken.None);

        ShouldFailOn(result, "email");
    }

    [Fact]
    public async Task A_role_over_eighty_characters_is_rejected()
    {
        var result = await HandlerOver(new InMemoryLeadStore())
            .Handle(new CreateLeadCommand("candidate", "Luis", "luis@correo.com", null, new string('a', 81)), CancellationToken.None);

        ShouldFailOn(result, "role");
    }

    [Fact]
    public async Task Every_broken_field_is_reported_at_once_not_one_per_request()
    {
        var result = await HandlerOver(new InMemoryLeadStore())
            .Handle(new CreateLeadCommand("company", null, "nope", null, null), CancellationToken.None);

        var errors = result.Error!.ValidationErrors!;

        errors.Keys.ShouldContain("name");
        errors.Keys.ShouldContain("email");
        errors.Keys.ShouldContain("company");
        errors.Count.ShouldBe(3);
    }

    [Fact]
    public async Task A_rejected_command_is_never_written_to_the_store()
    {
        var store = new RecordingLeadStore();

        await HandlerOver(store).Handle(new CreateLeadCommand("company", null, "nope", null, null), CancellationToken.None);

        store.Written.ShouldBeEmpty();
    }

    private static void ShouldFailOn(Result<CreateLeadResponse> result, string field)
    {
        result.IsSuccess.ShouldBeFalse();
        result.Status.ShouldBe(ResultStatus.Invalid);
        result.Error!.Code.ShouldBe("VALIDATION_FAILED");
        result.Error.ValidationErrors!.Keys.ShouldContain(field);
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    private sealed class RecordingLeadStore : ILeadStore
    {
        private readonly Dictionary<string, Lead> _byNormalizedEmail = new(StringComparer.Ordinal);

        public List<Lead> Written { get; } = [];

        public Lead? Last => Written.Count == 0 ? null : Written[^1];

        public Task<LeadRegistration> RegisterAsync(Lead lead, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            if (_byNormalizedEmail.TryGetValue(lead.NormalizedEmail, out var existing))
            {
                return Task.FromResult(new LeadRegistration(existing.Id, true));
            }

            _byNormalizedEmail[lead.NormalizedEmail] = lead;
            Written.Add(lead);

            return Task.FromResult(new LeadRegistration(lead.Id, false));
        }
    }
}
