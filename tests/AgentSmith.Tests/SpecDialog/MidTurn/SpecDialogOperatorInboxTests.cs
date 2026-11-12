using AgentSmith.Contracts.Providers;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.SpecDialog.MidTurn;

/// <summary>2026-11-12-a1fa: the inbox beside the gate — it queues only while a router design turn holds the gate, and
/// the gate is let go only when nothing waits.</summary>
public sealed class SpecDialogOperatorInboxTests
{
    private const string Session = "s-inbox";
    private static readonly TurnPoster Owner = new("U1", true);
    private static readonly TurnPoster Colleague = new("U2", false);

    private readonly SpecDialogTurnGate _gate = new(TimeProvider.System);
    private readonly SpecDialogOperatorInbox _inbox;

    public SpecDialogOperatorInboxTests() => _inbox = new SpecDialogOperatorInbox(_gate);

    [Fact]
    public void TryQueue_OnlyWhileADesignTurnHoldsTheGate_Queues()
    {
        _inbox.TryQueue(Session, Message(1, Owner)).Should().BeFalse("no turn holds the gate");
        _gate.TryEnter(Session).Should().BeTrue();
        _inbox.TryQueue(Session, Message(1, Owner)).Should().BeFalse("a resume or a deletion is no design turn");
        _gate.Exit(Session);
        _gate.TryEnterTurn(Session).Should().BeTrue();
        _gate.Lead(Session, stewarded: true);
        _inbox.TryQueue(Session, Message(1, Owner)).Should().BeFalse("the steward's turn keeps the busy reply");
        _gate.Lead(Session, stewarded: false);

        _inbox.TryQueue(Session, Message(1, Owner)).Should().BeTrue();
        _gate.HoldsDesignTurn(Session).Should().BeTrue();
    }

    [Fact]
    public void Drain_TakesThePostersMessagesOnce_LeavesOthers()
    {
        _gate.TryEnterTurn(Session);
        _inbox.TryQueue(Session, Message(1, Owner));
        _inbox.TryQueue(Session, Message(2, Colleague));

        _inbox.Drain(Session, Owner).Select(m => m.Index).Should().Equal(1);
        _inbox.Drain(Session, Owner).Should().BeEmpty();
        _inbox.TakeBefore(Session, 3).Select(m => m.Index).Should().Equal(2);
    }

    [Fact]
    public void TakeBefore_LeavesWhatTheReadStateDoesNotHold()
    {
        _gate.TryEnterTurn(Session);
        _inbox.TryQueue(Session, Message(1, Owner));
        _inbox.TryQueue(Session, Message(4, Owner));

        _inbox.TakeBefore(Session, 3).Select(m => m.Index).Should().Equal(1);
        _inbox.TryExit(Session).Should().BeFalse("index 4 still waits");
    }

    [Fact]
    public void TryExit_HoldsTheGateWhileAMessageWaits_ReleasesItOnceNothingDoes()
    {
        _gate.TryEnterTurn(Session);
        _inbox.TryQueue(Session, Message(1, Owner));

        _inbox.TryExit(Session).Should().BeFalse();
        _gate.TryEnter(Session).Should().BeFalse("still held");
        _inbox.Drain(Session, Owner);
        _inbox.TryExit(Session).Should().BeTrue();
        _gate.TryEnter(Session).Should().BeTrue();
    }

    [Fact]
    public void Abandon_ReleasesTheGateAndDropsWhatWaited()
    {
        _gate.TryEnterTurn(Session);
        _inbox.TryQueue(Session, Message(1, Owner));

        _inbox.Abandon(Session);

        _gate.TryEnterTurn(Session).Should().BeTrue();
        _inbox.TryExit(Session).Should().BeTrue("nothing waits after an abandon");
    }

    [Fact]
    public void Drop_ForgetsWhatWaited_KeepsTheGate()
    {
        _gate.TryEnterTurn(Session);
        _inbox.TryQueue(Session, Message(1, Owner));

        _inbox.Drop(Session);

        _inbox.TakeBefore(Session, int.MaxValue).Should().BeEmpty();
        _gate.HoldsDesignTurn(Session).Should().BeTrue();
    }

    [Fact]
    public void For_TheTurnsView_LeadsTheGateAndDrainsIntoTheCapture()
    {
        _gate.TryEnterTurn(Session);
        _inbox.TryQueue(Session, Message(3, Owner));
        var capture = new ForwardedThreadCapture();
        var turn = _inbox.For(Session, Owner, capture);

        turn.Drain().Select(m => m.Text).Should().Equal("message 3");
        capture.Record([new ChatMessage(ChatRole.User, "p")], new ChatResponse(new ChatMessage(ChatRole.Assistant, "a")));
        turn.Lead(stewarded: true);

        capture.Delivered.Should().Equal(3);
        _gate.HoldsDesignTurn(Session).Should().BeFalse("the steward leads now");
    }

    [Fact]
    public void Lead_AHoldThatIsNoTurn_StaysAsItIs()
    {
        _gate.TryEnter(Session);

        _gate.Lead(Session, stewarded: false);

        _gate.HoldsDesignTurn(Session).Should().BeFalse();
    }

    private static QueuedOperatorMessage Message(int index, TurnPoster poster) =>
        new(index, $"message {index}", DateTimeOffset.UnixEpoch, poster);
}
