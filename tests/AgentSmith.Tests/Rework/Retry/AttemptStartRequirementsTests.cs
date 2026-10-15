using AgentSmith.Application.Services.Claim;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestSupport;
using AgentSmith.Tests.Tickets;
using FluentAssertions;

namespace AgentSmith.Tests.Rework.Retry;

/// <summary>
/// 2026-10-14-dbcei: asking for the next attempt of a ticket that waits for an unlanded ticket is answered in the
/// conversation with what it waits for — no approval recorded, no retry or nudge queued.
/// </summary>
public sealed class AttemptStartRequirementsTests : IDisposable
{
    private const string Key = "github-42";
    private readonly RetryHarness _h = new();

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task ConversationAttemptStart_TicketWaitsForAnUnlandedTicket_NamesItAndQueuesNothing()
    {
        await _h.AttemptAsync("r-1", RunStatuses.Failed);
        _h.Facts.Edges.Add(Requires("41"));

        var answer = await StartAsync();

        answer.Should().Be(RequiresUnmetGate.Sentence([Requires("41")]) + ConversationAttemptStart.NothingQueued);
        answer.Should().Contain("#41");
        (await _h.RequestsAsync()).Should().BeEmpty();
        (await _h.NudgesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task ConversationAttemptStart_PendingEditOnAWaitingTicket_RecordsNoApproval()
    {
        await _h.AttemptAsync("r-1", RunStatuses.Failed);
        await _h.Approvals.SaveAsync(Edit(TicketStoreHarness.T.AddHours(1)), default);
        _h.Facts.Edges.Add(Requires("41"));

        (await StartAsync()).Should().EndWith(ConversationAttemptStart.NothingQueued);

        (await _h.Approvals.GetAsync("gh", Key, default))!.Continuation!.StartsAttempt.Should().BeFalse();
        (await _h.NudgesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task ConversationAttemptStart_EveryRequirementLanded_RequestsAsBefore()
    {
        await _h.AttemptAsync("r-1", RunStatuses.Failed);
        _h.Facts.Edges.Add(Requires("41"));
        _h.Facts.Landed.Add(new TicketLandingFact("gh", "github-41", "41", "alpha", TicketLandingKinds.ByHand,
            TicketStoreHarness.T, "Ada", "shipped by hand", null));

        (await StartAsync()).Should().Be(ConversationAttemptStart.Requested);

        (await _h.RequestsAsync()).Should().ContainSingle();
    }

    private async Task<string> StartAsync()
    {
        var port = await _h.StartPortAsync(new TurnPoster("ops-2", MayStartRuns: true));
        return await port!.StartNextAsync("go on", default);
    }

    private static TicketRequirementEdge Requires(string required) =>
        new("gh", Key, "42", "alpha", $"github-{required}", required, true, default, "s-1");

    private static SpecApprovalRecord Edit(DateTimeOffset at) =>
        new(Key, ApprovedSets.Set(Key, [ApprovedSets.Spec("2026-10-14-aaaaa")], new SpecApproval(at, "s-0", "editor")),
            ["api"], "gh", TicketId: "42",
            Continuation: new SeriesContinuation([new("2026-10-14-aaaaa", SpecDispositionKinds.Recut)], "r-1"));
}
