using AgentSmith.Application.Services.Metrics;
using AgentSmith.Application.Services.Polling;
using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Triggers;

/// <summary>
/// p0391a: a blocking finding disables the narrowest unit that carries it. These are the
/// two readers that act on one — the webhook/poller claim path and the discovery query —
/// and both must refuse THAT trigger while everything else keeps running.
/// </summary>
[Collection(MeterCollection.Name)]
public sealed class BlockedTriggerGatingTests
{
    private readonly StartupFindings _findings = new();

    [Fact]
    public void Resolve_TriggerWithBlockingFinding_IsNotMatched()
    {
        Block("alpha", TriggerKinds.GitHub);
        var resolver = new ProjectResolver(new AgentSmithMetrics(), new AgentSmith.Application.Services.Polling.PipelineResolver(), NullLogger<ProjectResolver>.Instance, _findings);

        var matches = resolver.Resolve(TwoProjects(), Envelope("alpha"));

        matches.Should().BeEmpty();
    }

    [Fact]
    public void Resolve_OtherProjectKeepsMatching_WhenOneTriggerIsBlocked()
    {
        Block("alpha", TriggerKinds.GitHub);
        var resolver = new ProjectResolver(new AgentSmithMetrics(), new AgentSmith.Application.Services.Polling.PipelineResolver(), NullLogger<ProjectResolver>.Instance, _findings);

        var matches = resolver.Resolve(TwoProjects(), Envelope("beta"));

        matches.Should().ContainSingle(m => m.ProjectName == "beta");
    }

    [Fact]
    public void Resolve_AdvisoryFinding_StillMatches()
    {
        _findings.Record(new StartupFinding(
            StartupSubsystems.Configuration, StartupFindingSeverity.Advisory, "heads up",
            "alpha", TriggerKinds.GitHub, "trigger_statuses"));
        var resolver = new ProjectResolver(new AgentSmithMetrics(), new AgentSmith.Application.Services.Polling.PipelineResolver(), NullLogger<ProjectResolver>.Instance, _findings);

        var matches = resolver.Resolve(TwoProjects(), Envelope("alpha"));

        matches.Should().ContainSingle(m => m.ProjectName == "alpha");
    }

    [Fact]
    public void Build_ProjectWithBlockingFinding_IsExcludedFromDiscovery()
    {
        Block("alpha", TriggerKinds.GitHub);
        var builder = new TrackerDiscoveryQueryBuilder(
            NullLogger<TrackerDiscoveryQueryBuilder>.Instance, _findings);

        var query = builder.Build(TwoProjects(), Tracker);

        query.Branches.Should().ContainSingle();
        query.Branches[0].Criterion!.Value.Should().Be("beta");
    }

    [Fact]
    public void Build_NoFindings_KeepsEveryProject()
    {
        var builder = new TrackerDiscoveryQueryBuilder(
            NullLogger<TrackerDiscoveryQueryBuilder>.Instance, _findings);

        var query = builder.Build(TwoProjects(), Tracker);

        query.Branches.Should().HaveCount(2);
    }

    private void Block(string project, string trigger) =>
        _findings.Record(new StartupFinding(
            StartupSubsystems.Configuration, StartupFindingSeverity.Blocking,
            "no park status", project, trigger, "needs_clarification_status"));

    private static TrackerConnection Tracker => new() { Name = "gh", Type = TrackerType.GitHub };

    private static IncomingTicketEnvelope Envelope(string tag) => new() { Labels = [tag] };

    private static AgentSmithConfig TwoProjects() => new()
    {
        Projects = new Dictionary<string, ResolvedProject>
        {
            ["alpha"] = Project("alpha"),
            ["beta"] = Project("beta"),
        },
        PipelineTriggers = PipelineTriggerMap.Empty,
    };

    private static ResolvedProject Project(string name) => new()
    {
        Name = name,
        Tracker = Tracker,
        DefaultPipeline = "fix-bug",
        GithubTrigger = new WebhookTriggerConfig
        {
            DefaultPipeline = "fix-bug",
            TriggerStatuses = ["open"],
            NeedsClarificationStatus = "question",
            ProjectResolution = new ProjectResolutionConfig
            {
                Strategy = ResolutionStrategy.Tag,
                Value = name,
            },
        },
    };
}
