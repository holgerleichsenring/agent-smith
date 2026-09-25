using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using FluentAssertions;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>
/// 2026-09-25-f6c2: an operator may name the eight labels the framework writes. A name equal
/// to one of their OWN routing words is refused at the upsert that types it — from the tracker
/// door and from the project door, because a project changed later creates a collision a
/// tracker upsert already passed.
/// </summary>
public sealed class RoutingWordCollisionTests : IDisposable
{
    private readonly DbConfigTestHarness _h = new();
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
            resolution:
              tag: testproject
        """;

    /// <summary>
    /// PipelineResolver strips lifecycle words from the labels it matches on, so this key
    /// could never satisfy the operator's own map — it silently stops existing.
    /// </summary>
    [Fact]
    public void Collision_AVocabularyNameEqualToATrackersRoutingKey_IsRefused()
    {
        _h.Import(SampleYaml);
        var tracker = Tracker();

        var refuse = () => _h.Store.UpsertTracker(
            tracker with { PipelineFromLabel = new Dictionary<string, string> { ["agent-smith:pending"] = "fix-bug" } },
            Tester);

        refuse.Should().Throw<ConfigurationException>()
            .WithMessage("*agent-smith:pending*Pending*lifecycle label*");
    }

    /// <summary>
    /// The tracker door sees the projects bound to it: a rename lands on a routing value that
    /// was saved long before, and no per-entity validator can see the other side.
    /// </summary>
    [Fact]
    public void Collision_ATrackerRenamingALabelOntoAnExistingProjectsRoutingValue_IsRefused()
    {
        _h.Import(SampleYaml);

        var refuse = () => _h.Store.UpsertTracker(
            Tracker() with { LabelNames = new Dictionary<string, string> { ["done"] = "testproject" } }, Tester);

        refuse.Should().Throw<ConfigurationException>().WithMessage("*testproject*Done*");
    }

    /// <summary>
    /// A project resolving on a framework word matches EVERY framework-labelled ticket on that
    /// tracker — here the approved-set stamp, which also short-circuits routing to phase
    /// execution before the label map is consulted at all.
    /// </summary>
    [Fact]
    public void Collision_AVocabularyNameEqualToAProjectsResolutionValue_IsRefused()
    {
        _h.Import(SampleYaml);

        var refuse = () => _h.Store.UpsertProject(
            Project() with { Resolution = new ProjectResolution("tag", "phase-spec:approved") }, Tester);

        refuse.Should().Throw<ConfigurationException>()
            .WithMessage("*phase-spec:approved*approved-set stamp*");
    }

    /// <summary>
    /// The reason this rule fires from both doors: the tracker upsert was legitimately clean
    /// when it ran, and the project that collides with it is saved afterwards.
    /// </summary>
    [Fact]
    public void Collision_AProjectUpsertCreatingTheCollisionLater_IsRefused()
    {
        _h.Import(SampleYaml);
        _h.Store.UpsertTracker(
            Tracker() with { LabelNames = new Dictionary<string, string> { ["done"] = "ops:finished" } }, Tester);

        var refuse = () => _h.Store.UpsertProject(
            Project() with { Resolution = new ProjectResolution("tag", "ops:finished") }, Tester);

        refuse.Should().Throw<ConfigurationException>().WithMessage("*ops:finished*Done*");
    }

    [Fact]
    public void Collision_ADistinctVocabulary_PassesBothDoors()
    {
        _h.Import(SampleYaml);

        _h.Store.UpsertTracker(
            Tracker() with
            {
                LabelNames = new Dictionary<string, string> { ["done"] = "ops:finished" },
                PipelineFromLabel = new Dictionary<string, string> { ["ops:hotfix"] = "fix-bug" },
            },
            Tester);
        _h.Store.UpsertProject(Project() with { Resolution = new ProjectResolution("tag", "ops:owned") }, Tester);

        Tracker().PipelineFromLabel.Should().ContainKey("ops:hotfix");
        Project().Resolution!.Value.Should().Be("ops:owned");
    }

    /// <summary>
    /// An area path is never read against the board's labels, so a value equal to a framework
    /// word collides with nothing — refusing it would be a refusal with no defect behind it.
    /// </summary>
    [Fact]
    public void Collision_AFrameworkWordUnderANonLabelStrategy_IsAccepted()
    {
        _h.Import(SampleYaml);

        _h.Store.UpsertProject(
            Project() with { Resolution = new ProjectResolution("area_path", "agent-smith:done") }, Tester);

        Project().Resolution!.Strategy.Should().Be("area_path");
    }

    private TrackerEntity Tracker() => _h.Store.GetTrackers().Single(t => t.Id == "test-ado");
    private ProjectEntity Project() => _h.Store.GetProjects().Single(p => p.Id == "testproject");

    public void Dispose() => _h.Dispose();
}
