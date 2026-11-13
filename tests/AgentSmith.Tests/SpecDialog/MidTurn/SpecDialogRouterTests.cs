using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog.MidTurn;

/// <summary>2026-11-12-a1fa: the router while a turn holds the gate — a design turn takes the message, the ticket
/// steward's answers that a turn runs, and a message after the last tool call runs the next turn without a gap.</summary>
public sealed class SpecDialogRouterTests : IDisposable
{
    private readonly MidTurnRig _rig = new();

    [Fact]
    public async Task SpecDialogRouter_ComputingDesignTurn_NoInProgressReply()
    {
        await _rig.OpenAsync();
        _rig.Turns(async (_, _, messages) =>
        {
            messages.Lead(stewarded: false);
            await _rig.SendAsync("and the export too");
            await MidTurnRig.CallToolAsync(messages);
        });

        await _rig.SendAsync("why does the import fail?");

        _rig.BusyReplies().Should().Be(0);
        _rig.Posters.Should().HaveCount(1);
    }

    [Fact]
    public async Task SpecDialogRouter_Steward_KeepsInProgressReply()
    {
        await _rig.OpenAsync();
        _rig.Turns(async (_, _, messages) =>
        {
            messages.Lead(stewarded: true);
            await _rig.SendAsync("and the export too");
        });

        await _rig.SendAsync("what did the last run do?");

        _rig.BusyReplies().Should().Be(1, "the steward's turn keeps the turn-in-progress reply");
        _rig.Posters.Should().HaveCount(1, "the message waits for the operator's next one, as before");
    }

    [Fact]
    public async Task SpecDialogRouter_MessageAfterLastToolCall_StartsNextTurnBeforeGateFree()
    {
        var state = await _rig.OpenAsync();
        var heldDuringSecond = false;
        _rig.Turns(
            async (_, _, messages) =>
            {
                await MidTurnRig.CallToolAsync(messages);
                await _rig.SendAsync("one more thing");
            },
            (_, _, _) =>
            {
                heldDuringSecond = !_rig.Gate.TryEnter(state.JobId);
                return Task.CompletedTask;
            });

        await _rig.SendAsync("why does the import fail?");

        heldDuringSecond.Should().BeTrue("the gate is never free between the turn and the one its message starts");
        _rig.Posters.Should().HaveCount(2);
        _rig.Gate.TryEnter(state.JobId).Should().BeTrue("the gate is let go once nothing waits");
    }

    public void Dispose() => _rig.Dispose();
}
