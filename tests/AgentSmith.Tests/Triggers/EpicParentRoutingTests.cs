using AgentSmith.Application.Services.Metrics;
using AgentSmith.Application.Services.Polling;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using FluentAssertions;

namespace AgentSmith.Tests.Triggers;

/// <summary>
/// 2026-09-13-a3f1: an epic parent is the record of a cut and starts no run — including on
/// the configuration where every other rule has a fallback, which is the one that made
/// removing the phase label worse than leaving it.
/// </summary>
public sealed class EpicParentRoutingTests
{
    [Fact]
    public void Resolve_EpicParent_NoPipelineFromLabel_IsNotRouted()
    {
        var matches = Resolve(Config(pipelineFromLabel: null), Envelope(PhaseTicketRenderer.EpicLabel));

        matches.Should().BeEmpty(
            "without a label filter PipelineResolver falls back to DefaultPipeline, which would "
            + "start a coding run on the summary of a cut");
    }

    [Fact]
    public void Resolve_EpicParent_WithPipelineFromLabel_IsNotRouted()
    {
        var config = Config(pipelineFromLabel: new Dictionary<string, string> { ["bug"] = "code" });

        Resolve(config, Envelope(PhaseTicketRenderer.EpicLabel)).Should().BeEmpty();
    }

    [Fact]
    public void Resolve_PhaseTicket_StillRoutesToPhaseExecution()
    {
        var matches = Resolve(Config(pipelineFromLabel: null), Envelope(PhaseTicketRenderer.PhaseLabel));

        matches.Should().ContainSingle().Which.PipelineName
            .Should().Be(PipelinePresets.PhaseExecutionName);
    }

    [Fact]
    public void Resolve_OrdinaryTicket_RoutingUnchanged()
    {
        var matches = Resolve(Config(pipelineFromLabel: null), Envelope("bug"));

        matches.Should().ContainSingle().Which.PipelineName.Should().Be("code");
    }

    private static IReadOnlyList<ProjectMatch> Resolve(
        AgentSmithConfig config, IncomingTicketEnvelope envelope) =>
        new ProjectResolver(new AgentSmithMetrics(), new PipelineResolver())
            .Resolve(config, envelope);

    private static IncomingTicketEnvelope Envelope(params string[] labels) => new()
    {
        TicketId = "1", Platform = "github", Labels = [.. labels, "bug"],
    };

    private static AgentSmithConfig Config(Dictionary<string, string>? pipelineFromLabel) => new()
    {
        Projects = new Dictionary<string, ResolvedProject>(StringComparer.Ordinal)
        {
            ["app"] = new()
            {
                Name = "app",
                DefaultPipeline = "code",
                GithubTrigger = new WebhookTriggerConfig
                {
                    DefaultPipeline = "code",
                    TriggerStatuses = ["open"],
                    NeedsClarificationStatus = "question",
                    PipelineFromLabel = pipelineFromLabel,
                    ProjectResolution = new ProjectResolutionConfig
                    {
                        Strategy = ResolutionStrategy.Tag,
                        Value = "bug",
                    },
                },
            },
        },
        PipelineTriggers = PipelineTriggerMap.Empty,
    };
}
