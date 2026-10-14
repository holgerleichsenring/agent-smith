using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Rework;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestSupport;
using AgentSmith.Tests.Tickets;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Moq;

namespace AgentSmith.Tests.Rework.Retry;

/// <summary>
/// 2026-10-14-da5ed: the person says "go on" in a failed ticket's conversation and the next attempt starts —
/// through start_next_attempt, the port a bound turn is seeded with, and the real rework worker. A pending series
/// edit starts as the conversation's approval; with none the latest attempt is retried unchanged.
/// </summary>
public sealed class AttemptStartTests : IDisposable
{
    private const string Poster = "ops-2";
    private const string Key = "github-42";

    private readonly RetryHarness _h = new();

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task AttemptStart_NoEdit_RetriesUnchanged_CauseIsRetry()
    {
        await _h.AttemptAsync("r-1", RunStatuses.Failed); // a preflight failure: no infrastructure end

        var answer = await SayGoOnAsync();

        answer.Should().Be(ConversationAttemptStart.Requested);
        (await _h.HandleNextAsync()).Should().Be(ReworkNudgeDisposition.Finish);
        _h.Launch.Verify(l => l.LaunchAsync("alpha", "42", "code", It.IsAny<CancellationToken>()), Times.Once);
        var cause = await _h.CauseOfNextAttemptAsync("r-2");
        cause.Channel.Should().Be(AttemptChannels.Retry);
        cause.Actor.Should().Be(Poster);
    }

    [Fact]
    public async Task AttemptStart_PendingEdit_RecordsTheApprovalWithStart_CauseIsTheConversation()
    {
        await _h.AttemptAsync("r-1", RunStatuses.Failed);
        await _h.Approvals.SaveAsync(Edit(TicketStoreHarness.T.AddHours(1)), default);

        var answer = await SayGoOnAsync();

        answer.Should().StartWith("Requested with the edited series");
        var record = await _h.Approvals.GetAsync("gh", Key, default);
        record!.Continuation!.StartsAttempt.Should().BeTrue();
        record.Approval!.Principal.Should().Be(Poster);
        record.Approval.Conversation.Should().Be("s-1");
        (await _h.RequestsAsync()).Should().BeEmpty("an edited series is the approval's act, not a retry");
        (await _h.HandleNextAsync()).Should().Be(ReworkNudgeDisposition.Finish);
        _h.Launch.Verify(l => l.LaunchAsync("alpha", "42", "code", It.IsAny<CancellationToken>()), Times.Once);
        var cause = await _h.CauseOfNextAttemptAsync("r-2");
        cause.Channel.Should().Be(AttemptChannels.Operator);
        cause.Actor.Should().Be(Poster);
        cause.Source!.Kind.Should().Be(AttemptSourceKinds.Conversation);
    }

    [Fact]
    public async Task AttemptStart_EditTheAttemptWorked_IsNotPending_Retries()
    {
        var approvedAt = TicketStoreHarness.T.AddHours(1);
        await _h.AttemptAsync("r-1", RunStatuses.Failed, null, new RunAttempt { RunId = "r-1", ApprovalAt = approvedAt });
        await _h.Approvals.SaveAsync(Edit(approvedAt), default);

        (await SayGoOnAsync()).Should().Be(ConversationAttemptStart.Requested);

        (await _h.RequestsAsync()).Should().ContainSingle().Which.RequestedBy.Should().Be(Poster);
        (await _h.Approvals.GetAsync("gh", Key, default))!.Continuation!.StartsAttempt.Should().BeFalse();
    }

    [Fact]
    public async Task AttemptStart_PendingEditWhileAQuestionWaits_RecordsNothing()
    {
        await _h.AttemptAsync("r-1", RunStatuses.WaitingForInput, null, TicketStoreHarness.Checkpoint("r-1"));
        await _h.Approvals.SaveAsync(Edit(TicketStoreHarness.T.AddHours(1)), default);

        var answer = await SayGoOnAsync();

        answer.Should().Contain("waiting for an answer");
        (await _h.Approvals.GetAsync("gh", Key, default))!.Continuation!.StartsAttempt.Should().BeFalse();
        (await _h.NudgesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task AttemptStart_WaitingHandbackWithoutEdit_AsksWhatShouldChange()
    {
        await _h.AttemptAsync("r-1", RunStatuses.WaitingForInput);

        var answer = await SayGoOnAsync();

        answer.Should().Contain("say what should change");
        (await _h.RequestsAsync()).Should().BeEmpty();
        (await _h.NudgesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task AttemptStart_PosterWithoutRunsControl_NoPort()
    {
        await _h.AttemptAsync("r-1", RunStatuses.Failed);

        (await _h.StartPortAsync(new TurnPoster(Poster, MayStartRuns: false))).Should().BeNull();
    }

    [Fact]
    public async Task AttemptStart_NeverWorkedTicket_NoPort()
    {
        (await _h.StartPortAsync(new TurnPoster(Poster, MayStartRuns: true))).Should().BeNull();
    }

    // The tool the master calls, over the port the bound turn is seeded with.
    private async Task<string> SayGoOnAsync()
    {
        var port = await _h.StartPortAsync(new TurnPoster(Poster, MayStartRuns: true));
        port.Should().NotBeNull();
        var tool = new StartNextAttemptToolHost(port!).GetTools(null, null).Single();
        var answer = await tool.InvokeAsync(new AIFunctionArguments { ["reason"] = "the registry is added, go on" });
        return answer!.ToString()!.Trim('"');
    }

    private static SpecApprovalRecord Edit(DateTimeOffset at) =>
        new(Key, ApprovedSets.Set(Key, [ApprovedSets.Spec("2026-10-14-aaaaa")], new SpecApproval(at, "s-0", "editor")),
            ["api"], "gh", TicketId: "42",
            Continuation: new SeriesContinuation([new("2026-10-14-aaaaa", SpecDispositionKinds.Recut)], "r-1"));
}
