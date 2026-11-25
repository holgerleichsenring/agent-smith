using System.Text.Json.Nodes;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Infrastructure.Migration.Services.ConfigDocuments;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>
/// 2026-11-25-9992i: every stored project moves off the four retired merge keys onto review ONCE — series_when manual is
/// each, else when manual or absent is end, else none — the keys go, the CI keys stay, a project naming none of them is not
/// re-saved, a second run changes nothing, and a project whose standalone tickets now behave otherwise is named in the log.
/// </summary>
public sealed class ReviewSettingMigrationTests : IDisposable
{
    private readonly DbConfigTestHarness _harness = new();
    private readonly CapturingLogger<ReviewSettingMigration> _log = new();

    public ReviewSettingMigrationTests()
    {
        _harness.Import(Yaml(["ci-series-manual", "manual", "absent", "ci-bypass", "gate", "ci-respect", "ci-only-keys", "series-manual", "own", "odd"]));
        Seed("ci-series-manual", """{"When":2,"SeriesWhen":0}""");
        Seed("manual", """{"When":0,"CiPollSeconds":60}""");
        Seed("ci-bypass", """{"When":2,"RespectBranchProtection":false}""");
        Seed("gate", """{"When":1,"RespectBranchProtection":true}""");
        Seed("ci-respect", """{"When":2,"SeriesWhen":2,"RespectBranchProtection":true,"RefreshOpenPullRequests":true,"CiPollSeconds":30}""");
        Seed("ci-only-keys", """{"CiPollSeconds":15}""");
        Seed("series-manual", """{"SeriesWhen":"manual"}""");
        Seed("own", """{"When":2}""", review: "end");
        Seed("odd", """{"When":2,"SeriesWhen":0}""", review: "sometimes");
    }

    [Theory]
    [InlineData("ci-series-manual", "each")]
    [InlineData("series-manual", "each")]
    [InlineData("manual", "end")]
    [InlineData("ci-bypass", "none")]
    [InlineData("gate", "none")]
    [InlineData("ci-respect", "none")]
    [InlineData("own", "end")]
    public void Move_MapsEachCombination_AndDropsTheKeys(string project, string review)
    {
        Migration().Move(_harness.DocStore);

        var doc = Doc(project);
        doc["Review"]!.GetValue<string>().Should().Be(review);
        doc["Merge"]!.AsObject().Select(p => p.Key).Should().NotContain(ReviewSettingMapping.RetiredKeys);
        Version(project).Should().Be(3, "seeded once, moved once");
    }

    [Fact]
    public void Move_KeepsTheCiKeys_AndTheMovedReviewReachesTheProject()
    {
        Migration().Move(_harness.DocStore);
        _harness.Store.Load();

        Doc("ci-respect")["Merge"]!["CiPollSeconds"]!.GetValue<int>().Should().Be(30);
        _harness.Store.GetProjects().Single(p => p.Id == "ci-respect").Review.Should().Be("none");
    }

    [Theory]
    [InlineData("absent", 1)]
    [InlineData("ci-only-keys", 2)]
    public void Move_AProjectNamingNoRetiredKey_IsNotReSaved(string project, int version)
    {
        Migration().Move(_harness.DocStore);

        Version(project).Should().Be(version);
        Doc(project)["Review"].Should().BeNull("no review was written");
    }

    [Fact]
    public void Move_RunTwice_ChangesNothingTheSecondTime()
    {
        Migration().Move(_harness.DocStore).Should().Be(8);

        Migration().Move(_harness.DocStore).Should().Be(0);
        Version("ci-series-manual").Should().Be(3);
    }

    [Fact]
    public void Move_NamesTheProjectsWhoseStandaloneTicketsChange()
    {
        Migration().Move(_harness.DocStore);

        _log.Warnings.Should().Contain(w => w.Contains("ci-series-manual") && w.Contains("now wait for a person"));
        _log.Warnings.Should().Contain(w => w.Contains("ci-bypass") && w.Contains("arms the host's auto-merge")
            && w.Contains("allow_auto_merge"), "a GitHub repository without auto-merge refuses the merge");
        _log.Warnings.Should().Contain(w => w.Contains("gate") && w.Contains("no longer at the end of their run"));
        _log.Warnings.Should().NotContain(w => w.Contains("Project manual ") || w.Contains("Project series-manual ")
            || w.Contains("ci-respect"), "their standalone tickets behave as before");
    }

    // A review already stored is the operator's: kept as written, readable or not; the old keys go and the project is named.
    [Theory]
    [InlineData("own", "end")]
    [InlineData("odd", "sometimes")]
    public void Move_NeverOverwritesAStoredReview_DropsTheKeys_AndNamesTheProject(string project, string stored)
    {
        Migration().Move(_harness.DocStore);

        var doc = Doc(project);
        doc["Review"]!.GetValue<string>().Should().Be(stored);
        doc["Merge"]!.AsObject().Select(p => p.Key).Should().NotContain(ReviewSettingMapping.RetiredKeys);
        _log.Warnings.Should().Contain(w => w.Contains($"Project {project} keeps its stored review '{stored}'"));
        _log.Warnings.Should().NotContain(w => w.Contains($"Project {project} now runs"));
    }

    [Fact]
    public void TryMove_IsAnIDataMigration_RecordedUnderItsId()
    {
        Migration().Id.Should().Be("2026-11-25-9992i-review-setting");
        Migration().TryMove(_harness.DocStore).Should().BeTrue();
    }

    private ReviewSettingMigration Migration() => new(_harness.Assembler, _log);

    // Writes the pre-9992i merge block (and a review) onto the stored project, as a new version.
    private void Seed(string project, string merge, string? review = null)
    {
        var doc = Doc(project);
        doc["Merge"] = JsonNode.Parse(merge);
        if (review is not null) doc["Review"] = review;
        var json = doc.ToJsonString();
        _harness.DocStore.Save(new ConfigDocWrite(ConfigDocTypes.Project, project, json, Version(project),
            _harness.Assembler.EdgesFor(ConfigDocTypes.Project, json), "test", "the pre-9992i merge block"));
    }

    private JsonObject Doc(string project) => JsonNode.Parse(Row(project).Doc)!.AsObject();

    private int Version(string project) => Row(project).Version;

    private ConfigDocRow Row(string project) =>
        _harness.DocStore.LoadAll().Single(r => r.Type == ConfigDocTypes.Project && r.Id == project);

    private static string Yaml(IEnumerable<string> projects) => """
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
          main:
            type: github
            auth: token
        projects:

        """ + string.Concat(projects.Select(p => $"  {p}:\n    agent: claude-default\n    tracker: main\n    repos: [test-repo]\n"));

    public void Dispose() => _harness.Dispose();
}
