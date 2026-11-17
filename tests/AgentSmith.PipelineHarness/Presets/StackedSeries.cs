using AgentSmith.Application.Services.Claim;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Rework;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>2026-11-17-9a39b/c: a filed api/ui pair, its code runs and the source host, for StackedStartCompositionTests.</summary>
internal static class StackedSeries
{
    public const string Project = "fixture-file";
    public const string PrerequisitePr = "https://git.test/o/r/pull/1";
    public const string DependentPr = "https://git.test/o/r/pull/2";
    private const string Repo = "filing-fixture";
    private static readonly DateTimeOffset Noon = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

    // A finished code run of the ticket that opened a pull request, as the run-event projection leaves it.
    internal static async Task RunAsync(IServiceProvider services, string ticket, string url)
    {
        var uow = services.GetRequiredService<IUnitOfWork>();
        uow.Set<Run>().Add(new Run
        {
            Id = $"20261117-120000-code-{ticket}", Project = Project, Pipeline = PipelinePresets.CodeName, TicketId = ticket,
            Status = RunStatuses.Success, StartedAt = Noon, FinishedAt = Noon.AddMinutes(30),
            PullRequestsJson = RunStoryJson.Serialize(new List<RunPullRequestView> { new(Repo, "opened", url, null, Noon) }),
        });
        await uow.SaveChangesAsync(default);
    }

    // The prerequisite's pull request landed on main; the dependent's sits on the prerequisite's branch.
    internal static Mock<ISourceProvider> Host()
    {
        var provider = new Mock<ISourceProvider>();
        provider.Setup(p => p.ReadPullRequestBaseAsync(PrerequisitePr, It.IsAny<CancellationToken>())).ReturnsAsync("main");
        provider.Setup(p => p.ReadPullRequestBaseAsync(DependentPr, It.IsAny<CancellationToken>())).ReturnsAsync("agent-smith/1");
        provider.Setup(p => p.RetargetPullRequestAsync(DependentPr, new BranchName("main"), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return provider;
    }

    internal static IReworkNudges Recording(List<ReworkNudgeRequest> into)
    {
        var nudges = new Mock<IReworkNudges>();
        nudges.Setup(n => n.EnqueueAsync(It.IsAny<ReworkNudgeRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ReworkNudgeRequest, CancellationToken>((r, _) => into.Add(r)).Returns(Task.CompletedTask);
        return nudges.Object;
    }

    internal static ClaimedReworkNudge Nudge(string ticket, ReworkNudgeOrigin origin) => new(Project, ticket, origin, null, null, 1, "claim", 0);
    internal static TicketGraphOutcome List() => new([Ticket("api", []), Ticket("ui", ["api"])]);

    internal static GraphTicket Ticket(string key, IReadOnlyList<string> requires) =>
        new(key, $"Title {key}", $"Requirement of {key}.", [$"{key} works"], requires, [])
        { Specs = [new SpecDraft($"{key}-spec", $"Goal of {key}", $"spec: {key}-spec\ngoal: \"Goal of {key}\"\n", [])] };

    internal static ConversationState State() => new()
    {
        JobId = "s-stack", ChannelId = "d-stack", ThreadId = "d-stack", UserId = "sample.user", Platform = "dashboard",
        Project = Project, TicketId = string.Empty, StartedAt = Noon,
        Transcript = [new TranscriptTurn(TranscriptRole.User, "Split it into api and ui.", Noon, Author: "sample.user"),
            new TranscriptTurn(TranscriptRole.User, "approve", Noon.AddMinutes(1), Decision: SpecDialogDecision.Approved, DecisionProposalId: "prop-1")],
    };
}
