using AgentSmith.Application.Services.Rework;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.Rework.Operator;

/// <summary>2026-10-13-9a59m: when a stored approval in a ticket's own conversation is an act on the ticket.</summary>
public sealed class OperatorActTests
{
    internal static readonly DateTimeOffset Start = new(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);

    internal static SpecApprovalRecord Record(DateTimeOffset at, bool starts = true) =>
        new("github-42", new SpecSet("github-42", [], SpecAccounting.Empty, [], SpecSource.Approved,
                Approval: new SpecApproval(at, "s-1", "owner")),
            ["api"], "gh", Continuation: new SeriesContinuation([], "r-1", starts));

    private static PreviousAttempt Attempt(DateTimeOffset? worked = null) =>
        new("r-1", RunStatuses.Failed, Start, true) { ActsReadAt = Start.AddMinutes(1), WorkedApprovalAt = worked };

    [Fact]
    public void OperatorAct_ApprovalNewerThanWorkedAndCutoff_IsAct()
    {
        var act = OperatorAct.Of(Record(Start.AddMinutes(30)), Attempt(worked: Start.AddMinutes(-10)));

        act.Should().Be(new ReworkAct("owner", Start.AddMinutes(30), ReworkChannel.Operator)
        {
            Source = new AttemptSource(AttemptSourceKinds.Conversation, Id: "s-1"),
        });
    }

    [Fact]
    public void OperatorAct_ApprovalTheAttemptWorked_IsNoAct()
    {
        var at = Start.AddMinutes(30);

        OperatorAct.Of(Record(at), Attempt(worked: at)).Should().BeNull("the attempt published this approval as worked");
    }

    [Fact]
    public void OperatorAct_AttemptDiedBeforePublishing_OlderApprovalIsNoAct()
    {
        OperatorAct.Of(Record(Start.AddMinutes(-5)), Attempt()).Should().BeNull("the attempt read after the approval");
        OperatorAct.Of(Record(Start.AddMinutes(5)), Attempt()).Should().NotBeNull("Precedes alone decides without a worked approval");
    }

    [Fact]
    public void OperatorAct_WithoutStartsAttempt_IsNoAct()
    {
        OperatorAct.Of(Record(Start.AddMinutes(30), starts: false), Attempt()).Should().BeNull();
        OperatorAct.Of(Record(Start.AddMinutes(30), starts: false), null).Should().BeNull();
        OperatorAct.Of(Record(Start.AddMinutes(30)) with { Continuation = null }, Attempt()).Should().BeNull("a filing's approval");
        OperatorAct.Of(Record(Start.AddMinutes(30)), null).Should().NotBeNull("a starting approval starts the first attempt");
    }
}
