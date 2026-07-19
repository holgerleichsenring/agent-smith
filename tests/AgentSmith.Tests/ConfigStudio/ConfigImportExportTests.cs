using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Domain.Exceptions;
using FluentAssertions;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>
/// p0349: the guarded, operator-invoked import/export — the DR + cutover path (NOT
/// auto-seed). Import is deliberate and guarded; export round-trips the DB back to
/// a YAML the real loader accepts.
/// </summary>
public sealed class ConfigImportExportTests : IDisposable
{
    private readonly DbConfigTestHarness _h = new();
    private readonly List<string> _tempFiles = new();
    private static readonly ChangeAttribution Tester = new("tester");

    private const string SampleYaml = """
        agents:
          claude-default:
            type: claude
            model: sonnet-4
        repos:
          test-repo:
            type: github
            url: https://github.com/test/repo
            auth: token
        trackers:
          test-ado:
            type: azure_devops
            organization: testorg
            project: TestProject
            auth: token
        projects:
          testproject:
            agent: claude-default
            tracker: test-ado
            repos: [test-repo]
            pipeline: fix-bug
        secrets:
          github_token: ${AGENTSMITH_TEST_GH_TOKEN}
        """;

    [Fact]
    public void ConfigImport_IntoEmptyDb_RestoresFromExportedYaml_RoundTrip()
    {
        _h.Import(SampleYaml);

        var exported = _h.Store.ExportYaml();
        var reloaded = DbConfigTestHarness.RealLoader().LoadConfig(WriteTemp(exported));

        reloaded.Agents.Should().ContainKey("claude-default");
        reloaded.Projects["testproject"].Repos.Should().ContainSingle(r => r.Url == "https://github.com/test/repo");
    }

    [Fact]
    public void ConfigImport_IntoNonEmptyDb_RejectedUnlessForce()
    {
        _h.Import(SampleYaml);

        _h.Invoking(h => h.Import(SampleYaml)).Should().Throw<ConfigurationException>()
            .WithMessage("*not empty*--force*");
        _h.Invoking(h => h.Import(SampleYaml, force: true)).Should().NotThrow();
    }

    [Fact]
    public void ConfigImport_InsertsAllEntitiesBeforeEdges_RestrictSatisfied()
    {
        // With foreign keys enforced (the harness sets PRAGMA foreign_keys=ON), an
        // edge inserted before its target would fail. A clean import proves the
        // ordering; the now-active RESTRICT proves the edges actually landed.
        _h.Import(SampleYaml);

        _h.Store.GetProjects().Should().ContainSingle(p => p.Id == "testproject");
        _h.Store.Invoking(s => s.DeleteAgent("claude-default", Tester))
            .Should().Throw<ConfigurationException>().WithMessage("*referenced by*");
    }

    private string WriteTemp(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"agentsmith-io-{Guid.NewGuid():N}.yml");
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        _h.Dispose();
        foreach (var f in _tempFiles) if (File.Exists(f)) File.Delete(f);
    }
}
