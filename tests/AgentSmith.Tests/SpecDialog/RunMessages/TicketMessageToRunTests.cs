using AgentSmith.Contracts.Models;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.SpecDialog.RunMessages;

/// <summary>
/// 2026-11-17-4860b: a message in a ticket's conversation while a code attempt works the ticket goes to that attempt —
/// no turn runs, the stored message carries the run's id, the conversation says where it went, and the run reads it
/// through the server's port once. Without a running attempt, or from a poster who may not start runs, a turn runs.
/// </summary>
public sealed class TicketMessageToRunTests : IDisposable
{
    private readonly TicketMessageRig _rig = new();

    [Fact]
    public async Task Router_TicketHeldByItsCodeAttempt_AddressesTheMessageToTheRunAndRunsNoTurn()
    {
        await _rig.OpenBoundAsync();
        _rig.Running();

        var routed = await _rig.SendAsync("also keep the CSV header");

        routed.Should().Be(SpecDialogRouted.Handled);
        _rig.Runner.Verify(r => r.RunTurnAsync(It.IsAny<ConversationState>(), It.IsAny<TurnPoster>(),
            It.IsAny<CancellationToken>()), Times.Never, "the running attempt takes it, not a conversation turn");
        var transcript = await _rig.TranscriptAsync();
        transcript.Single(t => t.Role == TranscriptRole.User).RunId.Should().Be(TicketMessageRig.Run);
        transcript.Last().Kind.Should().Be(SpecDialogTurnKind.Notice);
        transcript.Last().Text.Should().Contain(TicketMessageRig.Run).And.Contain("next tool result");
    }

    [Fact]
    public async Task Port_AddressedMessages_AreReadOnceByIndex()
    {
        await _rig.OpenBoundAsync();
        _rig.Running();
        await _rig.SendAsync("also keep the CSV header");

        (await _rig.Port.LastActivityAsync(TicketMessageRig.Project, TicketMessageRig.TicketId, default)).Should().NotBeNull();
        var first = await _rig.Port.AddressedToAsync(TicketMessageRig.Project, TicketMessageRig.TicketId, TicketMessageRig.Run, -1, default);
        first.Should().ContainSingle().Which.Text.Should().Be("also keep the CSV header");
        (await _rig.Port.AddressedToAsync(TicketMessageRig.Project, TicketMessageRig.TicketId, TicketMessageRig.Run, first[0].Index, default))
            .Should().BeEmpty();
        (await _rig.Port.AddressedToAsync(TicketMessageRig.Project, TicketMessageRig.TicketId, "another-run", -1, default))
            .Should().BeEmpty("a message is for the run it was addressed to");
    }

    [Fact]
    public async Task Router_NoAttemptRunning_RunsATurnAsBefore()
    {
        await _rig.OpenBoundAsync();

        await _rig.SendAsync("what did the last run do?");

        _rig.Runner.Verify(r => r.RunTurnAsync(It.IsAny<ConversationState>(), It.IsAny<TurnPoster>(),
            It.IsAny<CancellationToken>()), Times.Once);
        (await _rig.TranscriptAsync()).Should().NotContain(t => t.RunId == TicketMessageRig.Run);
    }

    [Fact]
    public async Task Router_PosterWithoutRunsControl_DoesNotSteerTheRun()
    {
        await _rig.OpenBoundAsync();
        _rig.Running();

        await _rig.SendAsync("stop doing the export", mayStartRuns: false);

        _rig.Runner.Verify(r => r.RunTurnAsync(It.IsAny<ConversationState>(), It.IsAny<TurnPoster>(),
            It.IsAny<CancellationToken>()), Times.Once);
        (await _rig.Port.AddressedToAsync(TicketMessageRig.Project, TicketMessageRig.TicketId, TicketMessageRig.Run, -1, default))
            .Should().BeEmpty();
    }

    public void Dispose() => _rig.Dispose();
}
