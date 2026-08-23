using AgentSmith.Contracts.Services;
using AgentSmith.Server.Extensions;
using AgentSmith.Tests.Architecture;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>
/// p0510: the split of ConfigStudioEndpoints into per-surface files is behaviour-
/// preserving, so the routes it maps are the proof. This reads the mapped endpoints
/// straight off the route builder — no server, no store — and asserts the same
/// thirty-nine verb/path pairs the single file produced. The two file-length
/// assertions hold the other half of the phase: the split files stay under the limit
/// and none of them buys its way into the ratchet baseline.
/// </summary>
public sealed class ConfigStudioRouteSplitTests
{
    private const int MaxLines = 120;

    private static readonly string[] SplitFiles =
    [
        "AgentSmith.Server/Extensions/ConfigStudioEndpoints.cs",
        "AgentSmith.Server/Extensions/ConfigEntityRoutes.cs",
        "AgentSmith.Server/Extensions/ConfigCapabilityEndpoints.cs",
        "AgentSmith.Server/Extensions/ConfigTransferEndpoints.cs",
        "AgentSmith.Server/Extensions/ConfigChangeEndpoints.cs",
        "AgentSmith.Server/Extensions/ConfigSettingsEndpoints.cs",
        "AgentSmith.Server/Services/Config/ConfigStudioWriteGuard.cs",
    ];

    private static readonly string[] EntityRoutes =
        ["agents", "trackers", "repos", "projects", "mcp-servers", "secrets", "connections"];

    [Fact]
    public void ConfigStudio_MappedRoutes_AreTheSameThirtyNineAfterTheSplit() =>
        MappedRoutes().Should().HaveCount(39);

    [Fact]
    public void ConfigStudio_MappedRoutes_KeepTheirVerbsAndPaths() =>
        MappedRoutes().Should().BeEquivalentTo(ExpectedRoutes());

    [Fact]
    public void FileLengthRatchet_NoConfigStudioFile_IsInTheBaseline() =>
        SplitFiles.Where(Baseline().Contains).Should().BeEmpty(
            "a file at or under the limit must not sit in file-length-baseline.tsv");

    [Fact]
    public void FileLengthRatchet_EveryNewConfigStudioFile_IsUnderTheLimit() =>
        SplitFiles
            .Select(f => (File: f, Lines: LinesOf(f)))
            .Where(f => f.Lines > MaxLines)
            .Should().BeEmpty($"the config studio split exists to fit every file in {MaxLines} lines");

    private static IReadOnlyList<string> ExpectedRoutes() =>
        [
            .. EntityRoutes.SelectMany(r => new[]
            {
                $"GET /api/config/{r}",
                $"POST /api/config/{r}",
                $"PUT /api/config/{r}/{{id}}",
                $"DELETE /api/config/{r}/{{id}}",
            }),
            "GET /api/config/capabilities",
            "POST /api/config/projects/validate",
            "POST /api/config/trackers/validate",
            "GET /api/config/connections/{id}/repos",
            "GET /api/config/export.yml",
            "POST /api/config/import",
            "GET /api/config/changes",
            "POST /api/config/changes/{id}/revert",
            "GET /api/config/settings",
            "GET /api/config/settings/{type}",
            "PUT /api/config/settings/{type}",
        ];

    // Maps the real endpoints onto a bare host and reads them back. Route mapping
    // inspects every handler's parameters, so IConfigStore must be a KNOWN service
    // type; it never has to answer, because no request is made.
    private static IReadOnlyList<string> MappedRoutes()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory,
            Args = [],
        });
        builder.Services.AddSingleton<IConfigStore>(_ =>
            throw new NotSupportedException("This host maps the studio routes; it never serves them."));

        using var app = builder.Build();
        app.MapConfigStudioEndpoints();

        return [.. ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(e =>
                $"{e.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Single()} {e.RoutePattern.RawText}")];
    }

    private static IReadOnlySet<string> Baseline()
    {
        var path = Path.Combine(ArchitectureSources.TestSourceRoot, "Architecture", "file-length-baseline.tsv");
        return File.ReadAllLines(path)
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .Select(l => l.Split('\t')[1])
            .ToHashSet(StringComparer.Ordinal);
    }

    private static int LinesOf(string relativePath) =>
        File.ReadLines(Path.Combine(ArchitectureSources.BackendRoot, relativePath)).Count();
}
