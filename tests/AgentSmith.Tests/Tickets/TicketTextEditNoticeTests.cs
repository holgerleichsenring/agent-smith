using AgentSmith.Contracts.Series;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Services.Acts;
using AgentSmith.Server.Services.Tickets;
using AgentSmith.Server.Services.Tickets.Conversation;
using AgentSmith.Server.Services.Tickets.Text;
using AgentSmith.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using static AgentSmith.Tests.Tickets.TicketConversationFixtures;
using static AgentSmith.Tests.Tickets.TicketSeriesFixtures;

namespace AgentSmith.Tests.Tickets;

/// <summary>
/// 2026-11-22-a720b: ticket 42's approved set filed onto its branch the way filing writes it, the tracker answering
/// through the real readers over a migrated store. A text that differs from the one agent-smith wrote is put to the
/// ticket's conversation once per edit; the text agent-smith wrote, or a set nobody approved, says nothing.
/// </summary>
public sealed class TicketTextEditNoticeTests : IDisposable
{
    private static readonly Ticket Written = new(new TicketId("42"), "t", "b", null, "open", "github", []);
    private static readonly Ticket Edited = new(new TicketId("42"), "t", "b, and also export a PDF", null, "open", "github", []);

    private readonly TicketStoreHarness _h = new();
    private readonly Mock<ISeriesConversation> _conversation = new();

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task CheckAsync_PersonEditedTheText_PostsOnceIntoTheOpenConversation()
    {
        await _h.AddAsync(OnBranch(BranchSpec("2026-11-22-297aa", "It works")), Session());
        Tracker(Edited);

        await CheckAsync();
        await CheckAsync();

        _conversation.Verify(c => c.SayAsync("s-1", It.Is<string>(t => t.Contains("The ticket text was changed")
            && t.Contains("Should this go into the specs?") && t.Contains("also export a PDF")), It.IsAny<CancellationToken>()),
            Times.Once, "one edit is asked about once, however often the page reads it");
    }

    [Fact]
    public async Task CheckAsync_TheTextAgentSmithWrote_PostsNothing()
    {
        await _h.AddAsync(OnBranch(BranchSpec("2026-11-22-297aa", "It works")), Session());
        Tracker(Written);

        await CheckAsync();

        _conversation.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CheckAsync_NoOpenConversation_PostsAgentSmithsCommentOnTheTicket()
    {
        await _h.AddAsync(OnBranch(BranchSpec("2026-11-22-297aa", "It works")));
        Tracker(Edited);

        await CheckAsync();

        _h.Tracker.Verify(t => t.UpdateStatusAsync(new TicketId("42"),
            It.Is<string>(s => s.StartsWith(TicketTextEditPost.Heading) && s.Contains("Should this go into the specs?")),
            It.IsAny<CancellationToken>()), Times.Once);
        _conversation.VerifyNoOtherCalls();
    }

    [Fact]
    public void Message_NamesTheGoalsTheTextNoLongerStates()
    {
        var set = new Contracts.Specs.SpecSet("github-42", [BranchSpec("2026-11-22-297aa", "It works")],
            Contracts.Specs.SpecAccounting.Empty, [], Contracts.Specs.SpecSource.Approved);

        var message = TicketTextEditNotice.Message(Edited, set);

        message.Should().Contain("It no longer states 1 of its 1 spec goal(s):").And.Contain("- Goal of 2026-11-22-297aa");
    }

    private void Tracker(Ticket ticket)
    {
        _h.Tracker.Setup(t => t.GetTicketAsync(It.IsAny<TicketId>(), It.IsAny<CancellationToken>())).ReturnsAsync(ticket);
        _h.Trackers.Setup(f => f.Create(It.IsAny<Contracts.Models.Configuration.TrackerConnection>())).Returns(_h.Tracker.Object);
    }

    private async Task CheckAsync()
    {
        using var scope = _h.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var post = new TicketTextEditPost(sp.GetRequiredService<TicketTranscriptReader>(), sp.GetRequiredService<IActRun>(),
            NullLogger<TicketTextEditPost>.Instance, _conversation.Object);
        await new TicketTextEditNotice(sp.GetRequiredService<TicketProjects>(), sp.GetRequiredService<TicketTrackerTextReader>(),
                sp.GetRequiredService<Application.Services.Specs.TicketBranchSeries>(), sp.GetRequiredService<ReworkLedgerRepository>(), post,
                sp.GetRequiredService<AgentSmith.Server.Services.SpecDialog.FiledWorkTrackerProjects>(), sp.GetRequiredService<AgentSmith.Contracts.Services.IActiveRunLease>())
            .CheckAsync("alpha", "42", CancellationToken.None);
    }
}
