using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-10-28-7a69b: whether an unmarked description takes the region below it. The framework's
/// own whole renderings stay refused — read off the ticket's labels and a recorded spec set, which
/// a failed first append cannot change — and every other ticket is a person's text.
/// </summary>
public sealed class TicketDescriptionRewriteTests
{
    [Fact]
    public async Task Placement_PersonsTicket_AppendsBelowTheirText()
    {
        var (sut, rewriter) = Sut([]);

        await sut.WriteAsync(Project(), new TicketId("41"), "region", previous: null, default);

        rewriter.Placements.Should().Equal(RegionPlacement.ReplaceOrAppend);
    }

    [Fact]
    public async Task Placement_TicketTextRecord_AppendsBelowTheTicketText()
    {
        var (sut, rewriter) = Sut([]);

        await sut.WriteAsync(Project(), new TicketId("41"), "region", Record(ticketText: true), default);

        rewriter.Placements.Should().Equal(RegionPlacement.ReplaceOrAppend);
    }

    [Theory]
    [InlineData(TicketLabels.ApprovedSetStamp)]
    [InlineData(TicketLabels.AcceptedApprovedSetStamp)]
    [InlineData(TicketLabels.AcceptedSpecEpicLabel)]
    public async Task Placement_FrameworkLabel_ReplacesOnly(string label)
    {
        var (sut, rewriter) = Sut([label]);

        await sut.WriteAsync(Project(), new TicketId("41"), "region", previous: null, default);

        rewriter.Placements.Should().Equal(RegionPlacement.ReplaceOnly);
    }

    [Fact]
    public async Task Placement_RecordedSpecSet_ReplacesOnly()
    {
        var (sut, rewriter) = Sut([]);

        await sut.WriteAsync(Project(), new TicketId("41"), "region", Record(ticketText: false), default);

        rewriter.Placements.Should().Equal(RegionPlacement.ReplaceOnly);
    }

    private static (TicketDescriptionRewrite, RecordingTicketRewriter) Sut(IReadOnlyList<string> labels)
    {
        var rewriter = new RecordingTicketRewriter();
        return (new TicketDescriptionRewrite(new Trackers(labels, rewriter), NullLogger<TicketDescriptionRewrite>.Instance), rewriter);
    }

    private static ResolvedProject Project() => new()
    {
        Name = "alpha", Tracker = new TrackerConnection { Name = "gh", Type = TrackerType.GitHub },
    };

    private static SpecApprovalRecord Record(bool ticketText) =>
        new("github:41", new SpecSet("github:41", [], SpecAccounting.Empty, [], SpecSource.Approved), ["api"], "gh", "", "41")
        { TicketText = ticketText };

    private sealed class Trackers(IReadOnlyList<string> labels, RecordingTicketRewriter rewriter) : ITicketProviderFactory
    {
        public ITicketProvider Create(TrackerConnection config) =>
            new StubTicketProvider(id => new Ticket(id, "Widget drops", "the widget drops", null, "Open", "GitHub", labels));

        public ITicketRewriter CreateRewriter(TrackerConnection config) => rewriter;

        public ITicketSearch CreateSearch(TrackerConnection config) => new RecordingTicketSearch();

        public ITicketLinkedWork CreateLinkedWork(TrackerConnection config) => new RecordingLinkedWork();
    }
}
