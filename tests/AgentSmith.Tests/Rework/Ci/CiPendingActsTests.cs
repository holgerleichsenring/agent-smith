using AgentSmith.Application.Services.Rework;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Contracts.Reviews;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Rework.Ci;

/// <summary>
/// 2026-11-17-3435c: the rework worker weighs a red-CI act beside a ticket's other acts — the newer wins — and never
/// reads it as the answer to a hand-back, so a red CI cannot release a ticket a person is asked about.
/// </summary>
public sealed class CiPendingActsTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 17, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ACiAct_IsTheNewest_WhenNothingNewerStands()
    {
        var (acts, project, attempt) = Sut(Start.AddMinutes(30));

        (await acts.NewestAsync(project, "42", attempt, default))!.Act.Channel.Should().Be(ReworkChannel.Ci);
        (await acts.NewestHandbackAnswerAsync(project, "42", attempt, default)).Should().BeNull();
    }

    [Fact]
    public async Task ANewerRetry_Wins()
    {
        var (acts, project, attempt) = Sut(Start.AddMinutes(30));
        attempt = attempt with { RetryRequestedBy = "alice", RetryRequestedAt = Start.AddMinutes(40) };

        (await acts.NewestAsync(project, "42", attempt, default))!.Act.Channel.Should().Be(ReworkChannel.Retry);
    }

    private static (ReworkPendingActs, ResolvedProject, PreviousAttempt) Sut(DateTimeOffset ciAt)
    {
        var ci = new Mock<ICiReworkAct>();
        ci.Setup(c => c.NewestAsync(It.IsAny<ResolvedProject>(), "42", It.IsAny<PreviousAttempt>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PendingReworkAct(new ReworkAct(CiAct.Actor, ciAt, ReworkChannel.Ci), Text: "evidence"));
        var acts = new ReworkPendingActs(Mock.Of<ITicketProviderFactory>(), Mock.Of<ISourceProviderFactory>(),
            Mock.Of<IPrReviewAuthorTrust>(), new InMemorySpecApprovalStore(), ci.Object);
        return (acts, new ResolvedProject { Name = "p" }, new PreviousAttempt("run-1", RunStatuses.Success, Start, true));
    }
}
