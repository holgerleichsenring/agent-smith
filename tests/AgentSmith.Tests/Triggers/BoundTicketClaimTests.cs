using AgentSmith.Application.Services.Claim;
using AgentSmith.Application.Services.Metrics;
using AgentSmith.Application.Services.Polling;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Triggers;

/// <summary>
/// 2026-09-25-e5b1: the bind and the claim have to agree, and they agree on a NAME. ProjectResolver
/// hard-binds a phase ticket to a pipeline before any label map is read, and ClaimPreChecker then
/// exempts that same pipeline from the label-triggered check — because the bind is the reason the
/// name is in no label map. Moving the bind from `phase-execution` to `code` without moving the
/// exemption would have left the two disagreeing: a resolved pipeline the claim refuses, on a
/// project whose trigger declares any other default. This is the pair, end to end.
/// </summary>
public sealed class BoundTicketClaimTests
{
    [Fact]
    public void Routing_ABoundTicket_ResolvesToTheCodePresetAndIsClaimable()
    {
        var config = Config();
        var envelope = new IncomingTicketEnvelope
        {
            Platform = "github",
            TicketId = "42",
            Labels = [FiledTicketLabels.ApprovedSetStamp, "sample"],
        };

        var matches = new ProjectResolver(new AgentSmithMetrics(), new PipelineResolver())
            .Resolve(config, envelope);

        var match = matches.Should().ContainSingle().Which;
        match.PipelineName.Should().Be(PipelinePresets.CodeName);
        PipelinePresets.TryResolve(match.PipelineName).Should().NotBeNull(
            "PipelineRunner throws on a name it cannot resolve, and the bind chose this one");
        ClaimPreChecker.Check(
                new ClaimRequest("github", "sample", new TicketId("42"), match.PipelineName),
                config)
            .Should().BeNull(
                "the trigger below declares security-scan, so only the exemption can pass this");
    }

    private static AgentSmithConfig Config() => new()
    {
        Projects = new Dictionary<string, ResolvedProject>
        {
            ["sample"] = new()
            {
                Name = "sample",
                GithubTrigger = new WebhookTriggerConfig
                {
                    DefaultPipeline = "security-scan",
                    ProjectResolution = new ProjectResolutionConfig
                    {
                        Strategy = ResolutionStrategy.Tag,
                        Value = "sample",
                    },
                },
            },
        },
    };
}
