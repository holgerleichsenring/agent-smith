using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Commands;

/// <summary>
/// 2026-11-23-97c2a: the merge step carries the run's ticket key as the requirement rows spell it, so the merge reads
/// what the ticket requires; a run without a ticket carries none.
/// </summary>
public sealed class CompletePullRequestsContextBuilderTests
{
    private static readonly ResolvedProject Project = new()
    {
        Name = "p", Tracker = new TrackerConnection { Name = "gh-tracker", Type = TrackerType.GitHub },
    };

    [Fact]
    public void Build_RunWithATicket_CarriesTheTrackerAndTheTicketKey()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.TicketId, new TicketId("42"));

        var context = Build(pipeline);

        context.Tracker.Should().Be("gh-tracker");
        context.TicketKey.Should().Be("github-42");
    }

    [Fact]
    public void Build_RunWithoutATicket_CarriesNoTicketKey() =>
        Build(new PipelineContext()).TicketKey.Should().BeNull();

    private static CompletePullRequestsContext Build(PipelineContext pipeline) =>
        (CompletePullRequestsContext)new CompletePullRequestsContextBuilder().Build(
            PipelineCommand.Simple(CommandNames.CompletePullRequests), Project, pipeline);
}
