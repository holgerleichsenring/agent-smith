using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Tickets.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using static AgentSmith.Tests.Tickets.TicketConversationFixtures;
using static AgentSmith.Tests.Tickets.TicketStoreHarness;

namespace AgentSmith.Tests.Tickets;

/// <summary>
/// The ticket itself on its page, over the real registration and a migrated store: the tracker's answer through a
/// minute's cache with our label note stripped, else the newest copy a code attempt kept, else a classified reason.
/// </summary>
public sealed class TicketTextReadTests : IDisposable
{
    private static readonly Ticket Live = new(new TicketId("42"), "Export it", "<h2>Goal</h2><p>A CSV.</p>", "<p>One file.</p>",
        "Active", "AzureDevOps", ["export"], new TicketPerson("Jane Operator", "jane-guid"), kind: "User Story",
        webUrl: "https://example.test/42", descriptionFormat: TicketTextFormat.Html, criteriaFormat: TicketTextFormat.Html);

    private readonly TicketStoreHarness _h = new();

    public void Dispose() => _h.Dispose();

    private TicketTextReader Reader => _h.Services.GetRequiredService<TicketTextReader>();

    private void Tracker(Ticket ticket) =>
        _h.Tracker.Setup(t => t.GetTicketAsync(It.IsAny<TicketId>(), It.IsAny<CancellationToken>())).ReturnsAsync(ticket);

    private void TrackerThrows(Exception ex) =>
        _h.Tracker.Setup(t => t.GetTicketAsync(It.IsAny<TicketId>(), It.IsAny<CancellationToken>())).ThrowsAsync(ex);

    private async Task<TicketTextView> ReadAsync() => (await Reader.ReadAsync("alpha", "42", default))!;

    [Fact]
    public async Task TicketText_Read_ServesTheTrackersTicketWithItsFormat()
    {
        await _h.AddAsync(Attempt("r-1", 0));
        Tracker(Live);

        var view = await ReadAsync();

        view.Should().BeEquivalentTo(new
        {
            Source = TicketTextSources.Tracker, Title = "Export it", Description = "<h2>Goal</h2><p>A CSV.</p>",
            DescriptionFormat = TicketTextFormat.Html, AcceptanceCriteria = "<p>One file.</p>", CriteriaFormat = TicketTextFormat.Html,
            WorkItemType = "User Story", State = "Active", Labels = new[] { "export" }, Assignee = "Jane Operator",
            WebUrl = "https://example.test/42", At = (DateTimeOffset?)T.AddHours(3), Reason = (string?)null,
        });
    }

    [Fact]
    public async Task TicketText_LabelNote_IsStripped()
    {
        await _h.AddAsync(Attempt("r-1", 0));
        Tracker(new Ticket(new TicketId("42"), "Export it", $"Export the report.\n\n{TicketLabelNote.Begin}\nbookkeeping\n{TicketLabelNote.End}",
            null, "Active", "GitHub", descriptionFormat: TicketTextFormat.Markdown));

        var view = await ReadAsync();

        (view.Description, view.DescriptionFormat).Should().Be(("Export the report.", TicketTextFormat.Markdown));
    }

    [Fact]
    public async Task TicketText_RefetchWithinAMinute_ReadsTheTrackerOnce()
    {
        await _h.AddAsync(Attempt("r-1", 0));
        Tracker(Live);

        await ReadAsync();
        await ReadAsync();

        _h.Tracker.Verify(t => t.GetTicketAsync(It.IsAny<TicketId>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TicketText_TrackerDown_ServesTheNewestCodeAttemptSnapshotWithItsAttempt()
    {
        TrackerThrows(new HttpRequestException("down at https://tracker.internal"));
        var review = Run("r-9", pipeline: "pr-review");
        await _h.AddAsync(Attempt("r-1", 0), Attempt("r-2", 60), review,
            Row("r-1", 1, RunConversationKinds.TicketText, 0, "Export it.", detailJson: "{\"acceptanceCriteria\":\"In a minute.\"}"),
            Row("r-2", 1, RunConversationKinds.TicketText, 60, "Export it now.", detailJson: "{\"acceptanceCriteria\":\"In a second.\"}"),
            Row("r-9", 1, RunConversationKinds.TicketText, 90, "Not an attempt."));

        var view = await ReadAsync();

        view.Should().BeEquivalentTo(new
        {
            Source = TicketTextSources.Snapshot, Title = "Ticket 42", Description = "Export it now.", AcceptanceCriteria = "In a second.",
            DescriptionFormat = TicketTextFormat.Text, RunId = "r-2", Attempt = (int?)2, At = (DateTimeOffset?)T.AddMinutes(60),
            WorkItemType = (string?)null, Labels = (IReadOnlyList<string>?)null, Assignee = (string?)null,
        });
    }

    [Fact]
    public async Task TicketText_RemovedProject_ServesSnapshot()
    {
        _h.Config.Projects.Remove("alpha");
        await _h.AddAsync(Attempt("r-1", 0), Row("r-1", 1, RunConversationKinds.TicketText, 0, "Export it."));

        var view = await ReadAsync();

        (view.Source, view.Description, view.Attempt).Should().Be((TicketTextSources.Snapshot, "Export it.", (int?)1));
        _h.Tracker.Verify(t => t.GetTicketAsync(It.IsAny<TicketId>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TicketText_TrackerDownNoSnapshot_IsUnavailableWithAClassifiedReason()
    {
        TrackerThrows(new TicketNotFoundException(new TicketId("42")));
        await _h.AddAsync(Attempt("r-1", 0));

        var view = await ReadAsync();

        (view.Source, view.Reason, view.Description).Should().Be((TicketTextSources.Unavailable, TicketTextReasons.NotFound, (string?)null));
    }

    [Fact]
    public async Task TicketText_RemovedProjectNoSnapshot_IsProjectNotConfigured()
    {
        _h.Config.Projects.Remove("alpha");
        await _h.AddAsync(Attempt("r-1", 0));

        (await ReadAsync()).Reason.Should().Be(TicketTextReasons.ProjectNotConfigured);
    }

    [Fact]
    public async Task TicketText_NoCodeRun_Is404()
    {
        await _h.AddAsync(Run("r-1", pipeline: "pr-review"));

        var result = await TicketTextEndpoints.TextAsync("alpha", "42", Reader, CancellationToken.None);

        result.Should().BeOfType<NotFound>();
    }
}
