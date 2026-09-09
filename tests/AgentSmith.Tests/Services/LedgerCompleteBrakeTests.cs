using AgentSmith.Infrastructure.Services.Providers.Agent;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

// 2026-09-08-805f: the per-pass counter behind the ledger-complete brake. Each Observe is one
// governor iteration, BEFORE the model call; an iteration observed while the ledger was
// already complete on the previous one is a tool turn the model spent with a complete checklist.
public sealed class LedgerCompleteBrakeTests
{
    [Fact]
    public void Observe_LedgerIncomplete_NeverActs()
    {
        var brake = new LedgerCompleteBrake(allowance: 3);

        for (var i = 0; i < 20; i++)
            brake.Observe(ledgerComplete: false).Should().Be(LedgerBrakeAction.None);
    }

    [Fact]
    public void Observe_CompleteForNIterations_DemandsOnce()
    {
        var brake = new LedgerCompleteBrake(allowance: 3);

        brake.Observe(true).Should().Be(LedgerBrakeAction.None, "the turn that completed the ledger is free");
        brake.Observe(true).Should().Be(LedgerBrakeAction.None, "turn 1 of the allowance");
        brake.Observe(true).Should().Be(LedgerBrakeAction.None, "turn 2 of the allowance");
        brake.Observe(true).Should().Be(LedgerBrakeAction.Demand, "turn 3 spends it — the verdict is demanded");
        brake.Observe(true).Should().Be(LedgerBrakeAction.None, "a second demand is never made");
    }

    [Fact]
    public void Observe_ItemReopened_ResetsTheCount()
    {
        var brake = new LedgerCompleteBrake(allowance: 2);

        brake.Observe(true);
        brake.Observe(true).Should().Be(LedgerBrakeAction.None, "one turn short of the allowance");
        brake.Observe(false).Should().Be(LedgerBrakeAction.None, "an item was reopened — the ledger is open again");
        brake.Observe(true).Should().Be(LedgerBrakeAction.None, "complete again: the count starts over");
        brake.Observe(true).Should().Be(LedgerBrakeAction.None);
        brake.Observe(true).Should().Be(LedgerBrakeAction.Demand);
    }

    [Fact]
    public void Observe_NIterationsAfterTheDemand_Stops()
    {
        var brake = new LedgerCompleteBrake(allowance: 2);

        brake.Observe(true);
        brake.Observe(true);
        brake.Observe(true).Should().Be(LedgerBrakeAction.Demand);
        brake.Observe(true).Should().Be(LedgerBrakeAction.None, "the demand buys the same allowance — build and test is a tool call");
        brake.Observe(true).Should().Be(LedgerBrakeAction.Stop, "the allowance after the demand is spent on tools");
        brake.TurnsSinceComplete.Should().Be(4, "two turns before the demand and two after it");
    }

    [Fact]
    public void Observe_AllowanceZero_IsOff()
    {
        var brake = new LedgerCompleteBrake(allowance: 0);

        for (var i = 0; i < 10; i++)
            brake.Observe(true).Should().Be(LedgerBrakeAction.None);
    }

    [Fact]
    public void EndOfPass_IsALabelledToolLessAssistantTurn()
    {
        var response = LedgerCompleteBrake.EndOfPass();

        response.Messages.Should().ContainSingle()
            .Which.Role.Should().Be(Microsoft.Extensions.AI.ChatRole.Assistant);
        response.Text.Should().Contain("No verdict was emitted");
    }
}
