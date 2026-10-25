using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Runs;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Security;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using static AgentSmith.Tests.Tickets.TicketDefaultsFixtures;
using static AgentSmith.Tests.Tickets.TicketStoreHarness;

namespace AgentSmith.Tests.Tickets;

/// <summary>
/// 2026-10-13-8762p: GET /api/tickets/{project}/{ticketId}/defaults over the real registration and a migrated
/// store — the ticket's runs' defaults and its bound conversation's, ordered for a person to work through.
/// </summary>
[Collection(RelationalStoreCollection.Name)]
public sealed class TicketDefaultsReadTests : IDisposable
{
    private readonly TicketStoreHarness _store = new();

    public void Dispose() => _store.Dispose();

    [Fact]
    public async Task TicketDefaults_Read_PendingHoldingSeriesFirst_ThenPending_ThenDecided()
    {
        await SeedTwoAttemptsAsync(_store, Default(1, R1, 1), Default(2, R1, 2, verdict: DefaultDecisionVerdicts.Ratified),
            Default(3, R2, 3, holdsSeries: true), Default(4, R2, 4), Default(5, R1, 5, holdsSeries: true),
            Default(6, R2, 6, verdict: DefaultDecisionVerdicts.Overruled));

        var view = await ReadAsync(_store);

        view.Entries.Select(e => e.Id).Should().Equal(5, 3, 4, 1, 6, 2);
    }

    [Fact]
    public async Task TicketDefaults_Read_NumbersAttempts_AndCountsPending()
    {
        await SeedTwoAttemptsAsync(_store, Run(Scan, pipeline: "security-scan"), Default(1, R1, 1),
            Default(2, R2, 2, holdsSeries: true), Default(3, Scan, 3), Default(4, R2, 4, verdict: DefaultDecisionVerdicts.Ratified));

        var view = await ReadAsync(_store);

        view.Entries.ToDictionary(e => e.Id, e => e.Attempt).Should().Equal(
            new Dictionary<long, int?> { [1] = 1, [2] = 2, [3] = null, [4] = 2 });
        (view.Pending, view.PendingHoldingSeries).Should().Be((3, 1));
        var decided = view.Entries.Single(e => e.Id == 4);
        (decided.VerdictBy, decided.VerdictAt).Should().Be(("earlier", T.AddMinutes(5)));
        view.Entries.Single(e => e.Id == 1).Alternatives.Should().Equal("other");
    }

    [Fact]
    public async Task TicketDefaults_Read_UnknownTicket_Is404()
    {
        await _store.AddAsync(Default(1, R1, 1));

        (await TicketDefaultsEndpoints.DefaultsAsync("alpha", "42", Context(), _store.Defaults, CancellationToken.None))
            .Should().BeOfType<NotFound>("the row's run names no ticket and no conversation is bound");
        (await TicketDefaultsEndpoints.DefaultsAsync("nope", "42", Context(), _store.Defaults, CancellationToken.None))
            .Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task TicketDefaults_Read_BoundConversationDefault_IsListedWithoutAttempt_WithItsConversation()
    {
        await SeedTwoAttemptsAsync(_store, TicketConversationFixtures.Session(), Default(1, DesignRun, 0, conversation: Session),
            Default(2, DesignRun, 1, conversation: "other-session"), Default(3, R1, 2));

        var view = await ReadAsync(_store);

        view.Entries.Select(e => (e.Id, e.Attempt, e.Conversation)).Should().BeEquivalentTo(
            [(3L, (int?)1, (string?)null), (1L, (int?)null, (string?)Session)]);
    }

    [Fact]
    public async Task TicketDefaults_Read_ConversationRowText_OnlyWhenTheCallerMayReadTheTranscript()
    {
        await _store.AddAsync(TicketConversationFixtures.Session(), Default(1, DesignRun, 0, conversation: Session));

        var hidden = (await ReadAsync(_store, Context(enforce: true, permissions: [Permissions.RunsRead]))).Entries.Single();
        var shown = (await ReadAsync(_store, Context(enforce: true, permissions: [Permissions.RunsRead, Permissions.DialogWrite])))
            .Entries.Single();

        (hidden.Question, hidden.Chosen, hidden.Alternatives, hidden.Why).Should().Be(
            ((string?)null, (string?)null, (IReadOnlyList<string>?)null, (string?)null));
        hidden.Verdict.Should().Be(DefaultDecisionVerdicts.Pending, "runs.read still sees that it exists and where it stands");
        (shown.Question, shown.Chosen, shown.Why).Should().Be(("Question 1?", "choice 1", "why 1"));
    }

    [Fact]
    public async Task TicketDefaults_Read_MemoryProposals_AreNotServed()
    {
        await _store.AddAsync(Run(R1), Step(R1),
            Default(1, R1, 1, kind: DefaultDecisionKinds.MemoryProposal), Default(2, R1, 2));

        var view = await ReadAsync(_store);

        // 2026-10-25-9324e: no memory waits for a person; a proposal row of an older run is history, not a default.
        view.Entries.Select(e => e.Id).Should().Equal(2);
        view.Pending.Should().Be(1);
    }
}
