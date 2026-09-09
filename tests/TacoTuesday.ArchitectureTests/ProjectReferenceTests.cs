using System.Xml.Linq;
using Shouldly;

namespace TacoTuesday.ArchitectureTests;

public sealed class ProjectReferenceTests
{
    [Fact]
    public void No_project_may_reference_the_Api_host()
    {
        var root = FindRepositoryRoot();

        var offenders = Directory
            .EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Where(p => !p.EndsWith("TacoTuesday.Api.csproj", StringComparison.Ordinal))
            .Where(p => !p.Contains("IntegrationTests", StringComparison.Ordinal))
            .Where(p => !p.Contains("ArchitectureTests", StringComparison.Ordinal))
            .Where(ReferencesTheApiHost)
            .Select(Path.GetFileName)
            .ToList();

        offenders.ShouldBeEmpty(
            $"these projects reference the host and must not: {string.Join(", ", offenders)}");
    }

    private static bool ReferencesTheApiHost(string csprojPath) =>
        XDocument.Load(csprojPath)
            .Descendants("ProjectReference")
            .Select(e => (string?)e.Attribute("Include") ?? string.Empty)
            .Any(include => include.Contains("TacoTuesday.Api.csproj", StringComparison.Ordinal));

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "TacoTuesdayAPI.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException(
            "Could not locate TacoTuesdayAPI.slnx above the test output directory.");
    }
}