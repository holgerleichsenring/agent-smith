using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Progress;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

// p0359: the in-pass reminder is a system-reminder the model may DISMISS — gentle,
// states that restructuring is allowed (the plan may deviate), and never pretends
// the recorded state is authoritative over what the model is actually doing.
public sealed class InPassReminderTextTests
{
    [Fact]
    public void Reminder_EmptyLedger_NudgesSeedingAndIsIgnorable()
    {
        var text = MasterNudges.BuildInPassReminder(new ProgressLedger([]));

        text.Should().Contain("<system-reminder>").And.Contain("</system-reminder>");
        text.Should().Contain("seed the checklist");
        text.Should().Contain("ignore this reminder");
    }

    [Fact]
    public void Reminder_DrainedLedger_PointsAtVerdictOrRestructure()
    {
        var text = MasterNudges.BuildInPassReminder(new ProgressLedger(
        [
            new ProgressLedgerEntry("1", "a", ProgressStatus.Done),
        ]));

        text.Should().Contain("emit your verdict");
        // p0391: the invitation to add steps is QUALIFIED. Unqualified it sat next to a
        // mechanism that turns any added item into another loop pass, so a model appending
        // "verify X once more" re-drove itself until the money or the operator stopped it.
        text.Should().Contain("Add a step only for");
        text.Should().Contain("work you have NOT done yet");
        text.Should().Contain("re-read or re-confirm evidence you already recorded");
        text.Should().Contain("you are done");
    }

    [Fact]
    public void Reminder_StaleLedger_AllowsRestructureAndCarriesCurrentState()
    {
        var text = MasterNudges.BuildInPassReminder(new ProgressLedger(
        [
            new ProgressLedgerEntry("1", "inventory", ProgressStatus.Done),
            new ProgressLedgerEntry("2", "migrate handlers", ProgressStatus.InProgress),
        ]));

        text.Should().Contain("restructure the checklist");
        text.Should().Contain("migrate handlers", "the current recorded ledger rides along");
        text.Should().Contain("ignore this reminder");
    }
}
