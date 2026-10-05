using System.Reflection;
using NetArchTest.Rules;
using Shouldly;
using TacoTuesday.Modules.Candidates;
using TacoTuesday.Modules.Companies;
using TacoTuesday.Modules.Leads;

namespace TacoTuesday.ArchitectureTests;

public sealed class ModuleBoundaryTests
{
    private static readonly Assembly CandidatesAssembly = typeof(CandidatesModule).Assembly;
    private static readonly Assembly CompaniesAssembly  = typeof(CompaniesModule).Assembly;
    private static readonly Assembly LeadsAssembly      = typeof(LeadsModule).Assembly;

    private static readonly Assembly[] AllModules = [CandidatesAssembly, CompaniesAssembly, LeadsAssembly];

    private const string CandidatesRoot = "TacoTuesday.Modules.Candidates";
    private const string CompaniesRoot  = "TacoTuesday.Modules.Companies";
    private const string LeadsRoot      = "TacoTuesday.Modules.Leads";

    private static string[] InternalNamespacesOf(string moduleRoot) =>
    [
        $"{moduleRoot}.Domain",
        $"{moduleRoot}.Features",
        $"{moduleRoot}.Persistence"
    ];

    [Fact]
    public void Candidates_must_not_reach_into_Companies_internals()
    {
        var result = Types.InAssembly(CandidatesAssembly)
            .That().ResideInNamespaceStartingWith(CandidatesRoot)
            .ShouldNot().HaveDependencyOnAny(InternalNamespacesOf(CompaniesRoot))
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Explain(result));
    }

    [Fact]
    public void Companies_must_not_reach_into_Candidates_internals()
    {
        var result = Types.InAssembly(CompaniesAssembly)
            .That().ResideInNamespaceStartingWith(CompaniesRoot)
            .ShouldNot().HaveDependencyOnAny(InternalNamespacesOf(CandidatesRoot))
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Explain(result));
    }

    [Fact]
    public void Leads_must_not_reach_into_other_modules_internals()
    {
        var otherModulesInternals = InternalNamespacesOf(CandidatesRoot)
            .Concat(InternalNamespacesOf(CompaniesRoot))
            .ToArray();

        var result = Types.InAssembly(LeadsAssembly)
            .That().ResideInNamespaceStartingWith(LeadsRoot)
            .ShouldNot().HaveDependencyOnAny(otherModulesInternals)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Explain(result));
    }

    [Fact]
    public void Other_modules_must_not_reach_into_Leads_internals()
    {
        foreach (var assembly in new[] { CandidatesAssembly, CompaniesAssembly })
        {
            var result = Types.InAssembly(assembly)
                .ShouldNot().HaveDependencyOnAny(InternalNamespacesOf(LeadsRoot))
                .GetResult();

            result.IsSuccessful.ShouldBeTrue(Explain(result));
        }
    }

    [Fact]
    public void Modules_must_not_depend_on_the_Api_host()
    {
        foreach (var assembly in AllModules)
        {
            var result = Types.InAssembly(assembly)
                .ShouldNot().HaveDependencyOn("TacoTuesday.Api")
                .GetResult();

            result.IsSuccessful.ShouldBeTrue(Explain(result));
        }
    }

    [Fact]
    public void Handlers_must_not_depend_on_HttpContext()
    {
        foreach (var assembly in AllModules)
        {
            var result = Types.InAssembly(assembly)
                .That().HaveNameEndingWith("Handler")
                .ShouldNot().HaveDependencyOn("Microsoft.AspNetCore.Http")
                .GetResult();

            result.IsSuccessful.ShouldBeTrue(Explain(result));
        }
    }

    [Fact]
    public void Endpoints_must_be_sealed_and_named_Endpoint()
    {
        foreach (var assembly in AllModules)
        {
            var result = Types.InAssembly(assembly)
                .That().ImplementInterface(typeof(TacoTuesday.SharedKernel.IEndpoint))
                .Should().BeSealed().And().HaveNameEndingWith("Endpoint")
                .GetResult();

            result.IsSuccessful.ShouldBeTrue(Explain(result));
        }
    }

    [Fact]
    public void Modules_must_not_depend_on_a_database_provider()
    {
        string[] providers =
        [
            "Microsoft.EntityFrameworkCore.SqlServer",
            "Microsoft.EntityFrameworkCore.Sqlite",
            "Microsoft.EntityFrameworkCore.Cosmos",
            "Microsoft.EntityFrameworkCore.InMemory",
            "Npgsql.EntityFrameworkCore.PostgreSQL",
            "Microsoft.Data.SqlClient"
        ];

        foreach (var assembly in AllModules)
        {
            var result = Types.InAssembly(assembly)
                .ShouldNot().HaveDependencyOnAny(providers)
                .GetResult();

            result.IsSuccessful.ShouldBeTrue(Explain(result));
        }
    }

    private static string Explain(TestResult result) =>
        result.IsSuccessful
            ? string.Empty
            : "Offending types: " + string.Join(", ", result.FailingTypeNames ?? []);
}