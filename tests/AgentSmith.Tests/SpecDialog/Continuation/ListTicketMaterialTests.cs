using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Specs;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.SpecDialog.Continuation;

/// <summary>
/// 2026-10-14-dbcel: a ticket filed from a list keeps the material of the conversation that filed it when its approval
/// is re-recorded in its own conversation — settled there before it ran, or edited by the ticket steward after.
/// </summary>
public sealed class ListTicketMaterialTests : IDisposable
{
    private const string ListConversation = "list-conv";
    private const string Series = "2026-10-13-ab12";
    private static readonly string A = Series + "a", B = Series + "b";

    private readonly ContinuationHarness _h = new();

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task TicketAmendment_ListTicketSettledInItsOwnConversation_KeepsTheListUploads()
    {
        await FiledFromAListAsync();
        _h.References.Set(ContinuationHarness.SessionId, "set-own");
        var proposal = new SpecOutcome(Draft("p9000a", "The widget is reliable"));
        await _h.BindAsync(proposal);

        await _h.AcceptAsync(proposal);

        var record = (await _h.RecordAsync())!;
        record.Approval!.Conversation.Should().Be(ContinuationHarness.SessionId);
        record.Set.Specs.Should().ContainSingle("the ticket's specs are settled now");
        record.CitedSets.Should().BeEquivalentTo(["set-own", "set-a"]);
        record.CitedImages.Should().Equal("img-1");
        record.ConversationOf("set-a").Should().Be(ListConversation);
        record.ConversationOf("img-1").Should().Be(ListConversation);
        record.ConversationOf("set-own").Should().Be(ContinuationHarness.SessionId);
    }

    [Fact]
    public async Task SeriesEditAct_StewardEditOfAListTicket_KeepsTheFilingConversationsMaterial()
    {
        await FiledFromAListAsync();
        await _h.WorkedAsync("r-1", "alpha", Series, [A, B], executed: [A], status: RunStatuses.Failed);
        await _h.BindAsync(null);

        var ports = await _h.InScopeAsync(sp => sp.GetRequiredService<BoundSeriesPorts>().ForAsync(_h.Config.Projects["alpha"],
            ContinuationHarness.TicketId, ContinuationHarness.SessionId, new TurnPoster("ops-2", true), default));
        var answer = await ports!.Edit!.EditAsync([new SeriesEdit(SeriesEditKinds.Recut, B, SeriesEditTests.Spec(B, "B, re-cut"))], default);

        answer.Should().Contain("nothing was started");
        var record = (await _h.RecordAsync())!;
        record.Approval!.Conversation.Should().Be(ContinuationHarness.SessionId);
        record.CitedSets.Should().Equal("set-a");
        record.ConversationOf("set-a").Should().Be(ListConversation);
    }

    // The ticket-text record a list filing stored: approved in the list's conversation, citing what it held.
    private async Task FiledFromAListAsync()
    {
        _h.References.Set(ListConversation, "set-a").Image(ListConversation, "img-1");
        await _h.Approvals.SaveAsync(new SpecApprovalRecord(ContinuationHarness.Key,
            new SpecSet(ContinuationHarness.Key, [], SpecAccounting.Empty, [], SpecSource.Approved,
                Approval: new SpecApproval(DateTimeOffset.UnixEpoch, ListConversation, "owner")) { Goal = "Widget drops" },
            ["api"], "gh", "api", ContinuationHarness.TicketId, ["set-a"], ["img-1"]) { Project = "alpha", TicketText = true }, default);
    }

    private static SpecDraft Draft(string id, string goal) =>
        new(id, goal, $"spec: {id}\ngoal: \"{goal}\"\ndone:\n  - \"{goal} works.\"\n", []) { Repos = ["api"], Done = [$"{goal} works."] };
}
