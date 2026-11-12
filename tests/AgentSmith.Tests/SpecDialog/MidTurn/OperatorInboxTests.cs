using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Server.Services.SpecDialog;
using Moq;
using AgentSmith.Server.Models;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog.MidTurn;

/// <summary>2026-11-12-a1fa: a message sent while a design turn runs reaches it with the next tool result, once; what
/// it did not take runs the next turn as its poster before the gate is let go.</summary>
public sealed class OperatorInboxTests : IDisposable
{
    private readonly MidTurnRig _rig = new();

    [Fact]
    public async Task OperatorInbox_AdmittedWhileComputing_DeliveredWithNextToolResult_Once()
    {
        await _rig.OpenAsync();
        var results = new List<string>();
        _rig.Turns(async (_, _, messages) =>
        {
            (await _rig.SendAsync("also check the retry path")).Should().BeTrue();
            results.Add(await MidTurnRig.CallToolAsync(messages));
            results.Add(await MidTurnRig.CallToolAsync(messages));
        });

        await _rig.SendAsync("why does the import fail?");

        results[0].Should().StartWith("file text\n\nThe operator wrote while you were working (")
            .And.EndWith("):\nalso check the retry path");
        results[1].Should().Be("file text", "a message is handed on once");
        _rig.Posters.Should().HaveCount(1, "the running turn took it, so no turn follows");
        _rig.BusyReplies().Should().Be(0);
    }

    [Fact]
    public async Task OperatorInbox_Answer_And_DecisionWithCardAwaiting_NotQueued()
    {
        var state = await _rig.OpenAsync();
        _rig.Runner.Setup(r => r.RunTurnAsync(It.IsAny<ConversationState>(), It.IsAny<TurnPoster>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SpecDialogTurnResult.On(MidTurnRig.Platform, "draft reply",
                new SpecOutcome(new SpecDraft("p9999", "widget goal", "spec: p9999\ngoal: \"widget goal\"", []))));
        await _rig.SendAsync("draft the spec");
        _rig.Gate.TryEnterTurn(state.JobId).Should().BeTrue();
        _rig.Pending.Set(state.JobId, new DialogQuestion("q-1", QuestionType.FreeText, "which tracker?", null, null, "",
            TimeSpan.FromMinutes(15)), expiresAt: null);

        await _rig.SendAsync("the second one");
        await _rig.SendAsync("approve");

        _rig.Inbox.TryExit(state.JobId).Should().BeTrue("neither the answer nor the decision on the awaiting card was queued");
        _rig.BusyReplies().Should().Be(1, "the decision keeps the turn-in-progress reply");
    }

    [Fact]
    public async Task OperatorInbox_DecisionWordWithoutCard_Queued()
    {
        var state = await _rig.OpenAsync();
        _rig.Gate.TryEnterTurn(state.JobId).Should().BeTrue();

        await _rig.SendAsync("approve");

        _rig.Inbox.Drain(state.JobId, new TurnPoster("U1", false)).Select(m => m.Text).Should().Equal("approve");
        _rig.BusyReplies().Should().Be(0);
        var stored = (await _rig.Harness.Sessions.GetOpenByThreadAsync(MidTurnRig.Platform, MidTurnRig.Thread, default))!;
        stored.Transcript[^1].DecisionApplied.Should().NotBe(true, "a decision word with no card decided nothing");
    }

    [Fact]
    public async Task OperatorInbox_OtherPoster_NotDeliveredMidTurn_RunsNextTurnAsThatPoster()
    {
        await _rig.OpenAsync();
        var handedOn = "";
        _rig.Turns(async (_, _, messages) =>
        {
            await _rig.SendAsync("from a colleague", user: "U2");
            handedOn = await MidTurnRig.CallToolAsync(messages);
        });

        await _rig.SendAsync("why does the import fail?");

        handedOn.Should().Be("file text", "another poster's message is not this turn's to read");
        _rig.Posters.Select(p => p.Id).Should().Equal("U1", "U2");
    }

    [Fact]
    public async Task OperatorInbox_QueuedAfterComputingBeforeExit_RunsNextTurn_DeliveredOnce()
    {
        await _rig.OpenAsync();
        var secondPrompt = new List<string>();
        var secondHandedOn = "";
        _rig.Turns(
            async (_, _, _) => await _rig.SendAsync("one more thing"),
            async (state, _, messages) =>
            {
                secondPrompt.AddRange(state.Transcript.Select(t => t.Text));
                secondHandedOn = await MidTurnRig.CallToolAsync(messages);
            });

        await _rig.SendAsync("why does the import fail?");

        secondPrompt.Should().Contain("one more thing", "the next turn's prompt holds it");
        secondHandedOn.Should().Be("file text", "taken with the prompt, so never handed on again");
        _rig.Posters.Should().HaveCount(2);
    }

    public void Dispose() => _rig.Dispose();
}
