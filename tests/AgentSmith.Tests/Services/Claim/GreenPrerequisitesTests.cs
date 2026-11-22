using AgentSmith.Application.Services.Claim;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Moq;
using static AgentSmith.Tests.TestSupport.TicketRequirementEdges;

namespace AgentSmith.Tests.Services.Claim;

/// <summary>
/// 2026-11-17-9a39a: a prerequisite is green when its newest code attempt delivered every spec and opened a pull
/// request; a dependent stacks on the tip of a chain of green prerequisites, and waits on two independent ones.
/// </summary>
public sealed class GreenPrerequisitesTests
{
    private readonly InMemoryTicketFacts _facts = new();
    private readonly Mock<IPreviousAttemptReader> _attempts = new() { CallBase = true }; // 9719d: the set reads loop LatestAsync

    public static TheoryData<string, bool, bool, bool> Attempts => new()
    {
        { RunStatuses.Success, false, true, true },
        { RunStatuses.Shortfall, false, true, false },
        { RunStatuses.Failed, false, true, false },
        { RunStatuses.Success, true, true, false },
        { RunStatuses.Success, false, false, false },
    };

    [Theory]
    [MemberData(nameof(Attempts))]
    public void IsGreen_TheAttemptsEnd_DecidesIt(string status, bool boundary, bool opened, bool green) =>
        GreenPrerequisites.IsGreen(Attempt(status, boundary, opened)).Should().Be(green);

    [Fact]
    public void IsGreen_AnUnfinishedAttempt_IsNot() =>
        GreenPrerequisites.IsGreen(new PreviousAttempt("r", RunStatuses.Success, default, Finished: false) { OpenedPullRequest = true })
            .Should().BeFalse();

    [Fact]
    public async Task TipAsync_OneGreenPrerequisite_IsTheTip()
    {
        Green("41");

        var tip = await Green().TipAsync(Project, [Edge("42", "41")], requireGreen: true, default);

        tip!.RequiredTicketId.Should().Be("41");
    }

    [Fact]
    public async Task TipAsync_AChain_StacksOnItsLastLink()
    {
        _facts.Edges.Add(Edge("41", "40"));
        Green("40");
        Green("41");

        var tip = await Green().TipAsync(Project, [Edge("42", "40"), Edge("42", "41")], requireGreen: true, default);

        tip!.RequiredTicketId.Should().Be("41", "41's branch was cut from 40's and carries it");
    }

    [Fact]
    public async Task TipAsync_TwoIndependentPrerequisites_HasNoTip()
    {
        Green("40");
        Green("41");

        (await Green().TipAsync(Project, [Edge("42", "40"), Edge("42", "41")], requireGreen: true, default)).Should().BeNull();
    }

    [Fact]
    public async Task TipAsync_APrerequisiteNotGreen_HasNoTipUnlessGreenIsNotRequired()
    {
        _attempts.Setup(a => a.LatestAsync(Project, "41", null, default)).ReturnsAsync(Attempt(RunStatuses.Failed));

        (await Green().TipAsync(Project, [Edge("42", "41")], requireGreen: true, default)).Should().BeNull();
        (await Green().TipAsync(Project, [Edge("42", "41")], requireGreen: false, default))!.RequiredTicketId.Should().Be("41");
    }

    [Fact]
    public async Task TipAsync_AClosedPrerequisite_IsNotGreen()
    {
        Green("41");
        _facts.Closed.Add(new TicketClosedMark(Tracker, "jira-41", "41", Project, DateTimeOffset.UnixEpoch, "sample.user", null, [], false));

        (await Green().TipAsync(Project, [Edge("42", "41")], requireGreen: true, default)).Should().BeNull();
    }

    private GreenPrerequisites Green() => new(_facts, _attempts.Object, _facts);

    private void Green(string ticket) =>
        _attempts.Setup(a => a.LatestAsync(Project, ticket.ToUpperInvariant(), null, default)).ReturnsAsync(Attempt(RunStatuses.Success));

    private static PreviousAttempt Attempt(string status, bool boundary = false, bool opened = true) =>
        new("run-1", status, DateTimeOffset.UnixEpoch, Finished: true) { StoppedAtBoundary = boundary, OpenedPullRequest = opened };
}
