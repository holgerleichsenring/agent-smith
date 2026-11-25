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

    // 2026-11-25-9992e: the project and ticket id the series question needs, and a judged request's protection over the policy's.
    [Fact]
    public void Build_AMergeGreenRequestWithAProtection_MergesUnderIt_AndCarriesProjectAndTicket()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.TicketId, new TicketId("42"));
        pipeline.Set(ContextKeys.CiGreenMerge, new AgentSmith.Contracts.Runs.CiGreenMergeRequest("42", "a-1", "agent-smith/42", [])
        {
            Protection = BranchProtection.Respect,
        });

        var context = Build(pipeline);

        (context.Project, context.TicketId, context.Protection).Should().Be(("p", "42", BranchProtection.Respect));
    }

    // 2026-11-25-9992i: the protection a merge asks for — the judged request's, else past protection into a series branch, else
    // under the host's branch protection.
    [Fact]
    public void Protection_JudgedOverSeriesBranchOverTheHostsRules()
    {
        var judged = new AgentSmith.Contracts.Runs.CiGreenMergeRequest("42", "a-1", "agent-smith/42", []) { Protection = BranchProtection.Respect };

        CompletePullRequestsContextBuilder.Protection(judged, "agent-smith/series/sr-1").Should().Be(BranchProtection.Respect);
        CompletePullRequestsContextBuilder.Protection(null, "agent-smith/series/sr-1").Should().Be(BranchProtection.Bypass);
        CompletePullRequestsContextBuilder.Protection(null, null).Should().Be(BranchProtection.Respect);
        CompletePullRequestsContextBuilder.Style("agent-smith/series/sr-1").Should().Be(AgentSmith.Contracts.Providers.MergeStyle.Squash);
        CompletePullRequestsContextBuilder.Style(null).Should().Be(AgentSmith.Contracts.Providers.MergeStyle.HostDefault);
    }

    private static CompletePullRequestsContext Build(PipelineContext pipeline) =>
        (CompletePullRequestsContext)new CompletePullRequestsContextBuilder().Build(
            PipelineCommand.Simple(CommandNames.CompletePullRequests), Project, pipeline);
}
