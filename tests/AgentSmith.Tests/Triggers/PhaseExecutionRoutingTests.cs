using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using FluentAssertions;

namespace AgentSmith.Tests.Triggers;

/// <summary>
/// p0315d: trigger routing by ticket kind. A `phase`-labelled ticket (the
/// p0315c filing artifact) routes hard-bound to the phase-execution preset in
/// ProjectResolver — before pipeline_from_label, which would otherwise drop
/// it. Every other ticket keeps today's label routing (bug -> fix-bug).
/// </summary>
public sealed class PhaseExecutionRoutingTests
{
    private readonly ProjectResolver _sut = new();

    [Fact]
    public void Routing_PhaseTicket_SelectsPhaseExecution()
    {
        var config = ConfigWithLabelRouting();

        var matches = _sut.Resolve(config, new IncomingTicketEnvelope { Labels = ["proj", "phase"] });

        matches.Should().ContainSingle(
            m => m.PipelineName == PipelinePresets.PhaseExecutionName,
            "a phase-labelled ticket must route to the phase-execution preset even though "
            + "no pipeline_from_label entry maps the framework-owned label");
    }

    [Fact]
    public void Routing_BugTicket_StillSelectsFixBug()
    {
        var config = ConfigWithLabelRouting();

        var matches = _sut.Resolve(config, new IncomingTicketEnvelope { Labels = ["proj", "bug"] });

        matches.Should().ContainSingle(
            m => m.PipelineName == "fix-bug",
            "a bug ticket keeps today's pipeline_from_label routing untouched");
    }

    // One project, tag-resolved, with the typical bug -> fix-bug label map. The
    // map is a strict filter: without the p0315d branch a phase ticket would be
    // dropped here, never routed.
    private static AgentSmithConfig ConfigWithLabelRouting() => new()
    {
        Projects = new Dictionary<string, ResolvedProject>
        {
            ["alpha"] = new ResolvedProject
            {
                Name = "alpha",
                Tracker = new TrackerConnection { Name = "gh", Type = TrackerType.GitHub },
                DefaultPipeline = "fix-bug",
                GithubTrigger = new WebhookTriggerConfig
                {
                    ProjectResolution = new ProjectResolutionConfig
                    {
                        Strategy = ResolutionStrategy.Tag,
                        Value = "proj",
                    },
                    DefaultPipeline = "fix-bug",
                    PipelineFromLabel = new Dictionary<string, string> { ["bug"] = "fix-bug" },
                },
            },
        },
        PipelineTriggers = PipelineTriggerMap.Empty,
    };
}
