using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Progress;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Handlers;

// p0365: the open-loop stop policy — as little control as possible, as much as necessary.
// A pass is one fresh nudged mini-conversation whose boundary the MODEL controls; it is not a
// unit of progress (the model alternates edit passes and verify passes). The only reliable stop
// signal is an EMPTY pass — no tool call fired. These pin exactly that, and pin the regression
// that a verify-only pass (its one tool call a `dotnet test`) keeps driving — the 4c32 failure
// where a per-pass state-delta gate killed the run as it moved from build-fix to tests.
public sealed class ReengageProgressPolicyTests
{
    // ---- Decide: empty pass stops; any tool call continues ----

    [Fact]
    public void Decide_EmptyPassNoToolCall_StopsIdle() =>
        ReengageProgressPolicy.Decide(toolCallsInPass: 0, block: null, passEndedOnException: false)
            .Should().Be(ReengageOutcome.StopIdle);

    [Fact]
    public void Decide_PassWithToolCalls_Continues() =>
        ReengageProgressPolicy.Decide(toolCallsInPass: 5, block: null, passEndedOnException: false)
            .Should().Be(ReengageOutcome.Continue);

    [Fact]
    public void Decide_VerifyOnlyPassWithOneToolCall_Continues() =>
        // The exact 4c32 regression: a pass whose only act was running the tests (one tool call,
        // zero file changes). It is productive, not idle — must keep driving.
        ReengageProgressPolicy.Decide(toolCallsInPass: 1, block: null, passEndedOnException: false)
            .Should().Be(ReengageOutcome.Continue);

    [Fact]
    public void Decide_PassEndedOnException_ContinuesAsRecovery() =>
        ReengageProgressPolicy.Decide(toolCallsInPass: 0, block: null, passEndedOnException: true)
            .Should().Be(ReengageOutcome.Continue);

    // ---- Blocked claim: "can't" needs a concrete blocker, like "done" needs a diff ----

    [Fact]
    public void Decide_HonestBlockedConcreteBlocker_StopsBlocked() =>
        ReengageProgressPolicy.Decide(
            toolCallsInPass: 3,
            block: new MasterBlockedClaim(true, "broker connection string absent from config"),
            passEndedOnException: false)
            .Should().Be(ReengageOutcome.StopBlocked);

    [Fact]
    public void Decide_FakeImpossibleNoBlocker_ContinuesReDriven() =>
        // "too complex" with no concrete blocker is the can't-side of faking-green — re-driven.
        ReengageProgressPolicy.Decide(
            toolCallsInPass: 2, block: new MasterBlockedClaim(true, "   "), passEndedOnException: false)
            .Should().Be(ReengageOutcome.Continue);

    [Fact]
    public void ShouldRespectBlock_ConcreteBlocker_True() =>
        ReengageProgressPolicy.ShouldRespectBlock(new MasterBlockedClaim(true, "missing NuGet feed credentials"))
            .Should().BeTrue();

    [Fact]
    public void ShouldRespectBlock_EmptyBlocker_False() =>
        ReengageProgressPolicy.ShouldRespectBlock(new MasterBlockedClaim(true, null)).Should().BeFalse();

    // ---- CountToolCalls: reads FunctionCallContent across the returned conversation ----

    private static ChatResponse ResponseWith(params string[] toolNames)
    {
        var messages = new List<ChatMessage>();
        foreach (var name in toolNames)
            messages.Add(new ChatMessage(ChatRole.Assistant,
                [new FunctionCallContent($"call-{name}", name, null)]));
        messages.Add(new ChatMessage(ChatRole.Assistant, [new TextContent("done for now")]));
        return new ChatResponse(messages);
    }

    [Fact]
    public void CountToolCalls_MultipleToolCalls_CountsThem() =>
        ReengageProgressPolicy.CountToolCalls(ResponseWith("ReadFile", "Edit", "RunCommand")).Should().Be(3);

    [Fact]
    public void CountToolCalls_VerifyOnlyPass_CountsTheOneToolCall() =>
        // A pass whose only tool call is a build/test run still counts as active.
        ReengageProgressPolicy.CountToolCalls(ResponseWith("RunCommand")).Should().Be(1);

    [Fact]
    public void CountToolCalls_TextOnlyResponse_IsZero() =>
        ReengageProgressPolicy.CountToolCalls(
            new ChatResponse(new ChatMessage(ChatRole.Assistant, [new TextContent("I am done.")])))
            .Should().Be(0);

    // ---- Archetype trajectories: fold pass tool-call counts through Decide ----

    private static (int passes, ReengageOutcome stoppedOn) RunTrajectory(params int[] toolCallsPerPass)
    {
        for (var i = 0; i < toolCallsPerPass.Length; i++)
        {
            var outcome = ReengageProgressPolicy.Decide(toolCallsPerPass[i], block: null, passEndedOnException: false);
            if (outcome != ReengageOutcome.Continue) return (i + 1, outcome);
        }
        return (toolCallsPerPass.Length, ReengageOutcome.Continue);
    }

    [Fact]
    public void Reengage_BigStepAcrossActivePasses_DrivesOn() =>
        // A large migration: every pass fires tools (reads/edits/builds/tests). Drives on — no abort.
        RunTrajectory(8, 3, 12, 5, 1, 9).stoppedOn.Should().Be(ReengageOutcome.Continue);

    [Fact]
    public void Reengage_GreenfieldRamp_DrivesOn() =>
        // Greenfield UI: many active passes, no verdict yet. Drives on.
        RunTrajectory(6, 6, 6, 6, 6, 6, 6, 6).stoppedOn.Should().Be(ReengageOutcome.Continue);

    [Fact]
    public void Reengage_EditThenVerifyRhythm_DrivesOn() =>
        // Edit pass (many tools) then verify pass (one `dotnet test`) — the 4c32 shape. Both active.
        RunTrajectory(7, 1, 6, 1, 8, 1).stoppedOn.Should().Be(ReengageOutcome.Continue);

    [Fact]
    public void Reengage_IdlePass_StopsAtOnce()
    {
        var result = RunTrajectory(5, 4, 0);   // third pass fired no tool → idle
        result.stoppedOn.Should().Be(ReengageOutcome.StopIdle);
        result.passes.Should().Be(3);
    }

    // ---- Lazy-done stays a ShouldReengage concern: a done step whose target is absent from the diff ----

    [Fact]
    public void Reengage_LazyDoneWithoutDiff_KeepsDriving()
    {
        var ledger = new ProgressLedger(new[]
        {
            new ProgressLedgerEntry("1", "update server", ProgressStatus.Done, Target: "src/Server.cs"),
        });
        var noBackingDiff = System.Array.Empty<CodeChange>();
        var green = new MasterVerification(VerificationStatus.Green, true, true, true, true, "green");
        AgenticMasterHandler.ShouldReengage(
            "fix-bug", ledger, green, budgetExhausted: false, new[] { "Server updated" }, noBackingDiff)
            .Should().BeTrue();
    }

    // ---- Blocked-claim parser: ready to consume a { blocked, blocker } block ----

    [Fact]
    public void TryParseBlockedClaim_ConcreteBlocker_Parsed()
    {
        var claim = MasterVerificationParser.TryParseBlockedClaim(
            "Status:\n```json\n{\"blocked\": true, \"blocker\": \"missing NuGet feed credentials\"}\n```");
        claim.Should().NotBeNull();
        claim!.IsBlocked.Should().BeTrue();
        claim.Blocker.Should().Be("missing NuGet feed credentials");
    }

    [Fact]
    public void TryParseBlockedClaim_Absent_Null() =>
        MasterVerificationParser.TryParseBlockedClaim("All good, no blockers here.").Should().BeNull();
}
