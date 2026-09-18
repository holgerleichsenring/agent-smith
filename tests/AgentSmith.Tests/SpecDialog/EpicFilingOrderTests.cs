using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

using AgentSmith.Tests.TestSupport;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-13-a72a: an epic is filed in DEPENDENCY order.
/// <para>
/// 2026-09-17-0e79d: the order is now the SET's — one run works the slices in it, phase by
/// phase — and the records are filed in the same order so the tracker reads as the run runs.
/// The label stamps that used to carry the order are gone with the N-children shape.
/// </para>
/// </summary>
public sealed class EpicFilingOrderTests
{
    [Fact]
    public async Task FileEpic_ForwardEdge_FilesInDependencyOrder()
    {
        var provider = new RecordingProvider();

        var report = await FileAsync(provider, Epic(
            Child("p9000a", requires: ["p9000b"]),
            Child("p9000b")));

        report.Error.Should().BeNull();
        provider.Created.Select(c => c.Title).Should().Equal(
            ["p9000: Widget platform", "p9000b: slice p9000b", "p9000a: slice p9000a"],
            "the cut listed the slices in the wrong order; the stored set and its records follow the edges");
    }

    [Fact]
    public async Task FileEpic_UnorderableEpic_IsRefused()
    {
        var provider = new RecordingProvider();

        var report = await FileAsync(provider, Epic(
            Child("p9000a", requires: ["p9000b"]),
            Child("p9000b", requires: ["p9000a"])));

        report.Error.Should().NotBeNull().And.Subject.ToString()
            .Should().Contain("cannot be put in an order");
        provider.Created.Should().BeEmpty(
            "an epic that cannot be ordered is refused at filing, not discovered at run time");
    }

    /// <summary>
    /// 2026-09-17-0e79d: the stamps are gone from the filing shape. A record carries the record
    /// label and nothing a machine reads; a stamp on the work ticket would cut its branch from
    /// another ticket's rung instead of from its own base.
    /// </summary>
    [Fact]
    public async Task FileEpic_NothingItFiles_CarriesAStamp()
    {
        var provider = new RecordingProvider();

        await FileAsync(provider, Epic(Child("p9000a"), Child("p9000b", requires: ["p9000a"])));

        provider.Created.Should().OnlyContain(
            c => FiledTicketLabels.ParentId(c.Labels) == null
                && FiledTicketLabels.PredecessorIds(c.Labels).Count == 0);
    }

    /// <summary>2026-09-17-042ea: the tracker shows each child under its parent.</summary>
    [Fact]
    public async Task EpicFiling_EachChild_IsLinkedToItsParent()
    {
        var provider = new RecordingProvider();

        var report = await FileAsync(provider, Epic(Child("p9000a"), Child("p9000b", requires: ["p9000a"])));

        provider.Links.Should().Equal(("2", "1"), ("3", "1"));
        report.Notes.Should().BeEmpty();
    }

    /// <summary>
    /// 2026-09-17-042ea: the tickets exist and the work ticket already carries the approved set.
    /// Were a refused link the filing's error, the notice would offer a retry — and the retry
    /// files a SECOND work ticket with a second stored set, which is two runs and two pull
    /// requests per repository. (2026-09-17-0e79d: the label stamps that used to order the
    /// children are gone; the order is the set's, inside one run.)
    /// </summary>
    [Fact]
    public async Task EpicFiling_ALinkThatFails_FilesTheRemainingChildrenAndNotesIt_WithoutAnError()
    {
        var provider = new RecordingProvider { RefuseLinkOf = "2" };

        var report = await FileAsync(provider, Epic(Child("p9000a"), Child("p9000b", requires: ["p9000a"])));

        report.Error.Should().BeNull("a link is for people; nothing was unfiled");
        provider.Created.Should().HaveCount(3, "the child after the refused link is still filed");
        report.Filed.Should().HaveCount(3);
        report.Notes.Should().ContainSingle().Which.Should()
            .Contain("https://tracker.test/2").And.Contain("https://tracker.test/1").And.Contain("link type is disabled");
    }

    /// <summary>
    /// An HttpClient timeout throws TaskCanceledException with nobody having cancelled. Escaping
    /// the filer, it stopped the sink after the confirmed outcome was stored, and asking again
    /// filed every ticket a second time.
    /// </summary>
    [Fact]
    public async Task EpicFiling_ALinkThatTimesOut_FilesTheRemainingChildrenAndNotesIt()
    {
        var provider = new RecordingProvider { ThrowOnLinkOf = ("2", new TaskCanceledException("the link timed out")) };

        var report = await FileAsync(provider, Epic(Child("p9000a"), Child("p9000b", requires: ["p9000a"])));

        report.Error.Should().BeNull();
        provider.Created.Should().HaveCount(3);
        report.Notes.Should().ContainSingle().Which.Should().Contain("the link timed out");
    }

    [Fact]
    public async Task EpicFiling_ALinkThatThrows_IsANoteNotAnError()
    {
        var provider = new RecordingProvider { ThrowOnLinkOf = ("2", new InvalidOperationException("tracker exploded")) };

        var report = await FileAsync(provider, Epic(Child("p9000a"), Child("p9000b", requires: ["p9000a"])));

        report.Error.Should().BeNull();
        report.Filed.Should().HaveCount(3);
        report.Notes.Should().ContainSingle().Which.Should().Contain("https://tracker.test/2").And.Contain("tracker exploded");
    }

    [Fact]
    public async Task OutcomeTicketFiler_ATrackerTimeoutOnCreate_IsReportedNotThrown()
    {
        var provider = new RecordingProvider { ThrowOnCreate = new TaskCanceledException("create timed out") };

        var report = await FileAsync(provider, Epic(Child("p9000a")));

        report.Error.Should().Be("create timed out", "nobody cancelled, so the report names the failure");
    }

    /// <summary>
    /// 2026-09-13-ed5a: the parent records what the analysis had open while it cut, because a
    /// reader who asks why the slices are shaped this way must find the answer on the ticket.
    /// </summary>
    [Fact]
    public async Task OutcomeTicketFiler_EpicWithTemplates_ParentBodyNamesRevisions()
    {
        var provider = new RecordingProvider();
        var epic = Epic(Child("p9000a")) with
        {
            Templates =
            [
                new TemplateProvenance("template:default", "reference-server", "a1b2c3d", Opened: true),
                new TemplateProvenance("template:web", "reference-web", "v2.1.0", Opened: false),
            ],
        };

        await FileAsync(provider, epic);

        var parent = provider.Created[0].Body;
        parent.Should().Contain("template:default").And.Contain("reference-server")
            .And.Contain("read at `a1b2c3d`", "the sha is what the cut was actually made against");
        parent.Should().Contain("declared at `v2.1.0`, unread",
            "a template that was open and unread is a different claim from one that was read");
    }

    /// <summary>
    /// 2026-09-13-b7ba made the parent a REQUIREMENT body, so the count that must hold is ZERO
    /// fenced blocks, not one: the phase-execution extractor takes the single yaml block out of
    /// a phase-labelled ticket, and the provenance is appended as plain lines for that reason.
    /// </summary>
    [Fact]
    public async Task OutcomeTicketFiler_EpicParentBody_StillOpensNoFence()
    {
        var provider = new RecordingProvider();
        var epic = Epic(Child("p9000a")) with
        {
            Templates = [new TemplateProvenance("template:default", "reference-server", "a1b2c3d", true)],
        };

        await FileAsync(provider, epic);

        provider.Created[0].Body.Should().NotContain("```");
    }

    private static async Task<FilingReport> FileAsync(RecordingProvider provider, EpicOutcome epic)
    {
        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(provider);
        var config = new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject>
            {
                ["proj"] = new() { Name = "proj", Tracker = new TrackerConnection() },
            },
        };
        var filer = new OutcomeTicketFiler(
            config, factory.Object, new PhaseTicketRenderer(), new BugTicketRenderer(),
            TestSupport.ApprovedSetDoubles.EpicFiler(),
            TestSupport.ApprovedSetDoubles.Recorder(),
            FiledWorkDoubles.Starter(), NullLogger<OutcomeTicketFiler>.Instance);
        return await filer.FileAsync(State(), epic, false, CancellationToken.None);
    }

    private static EpicOutcome Epic(params PhaseDraft[] children) =>
        new(new PhaseDraft("p9000", "Widget platform", "phase: p9000", []), children);

    private static PhaseDraft Child(string id, IReadOnlyList<string>? requires = null) =>
        new(id, $"slice {id}", $"phase: {id}", requires ?? []);

    private static ConversationState State() => new()
    {
        JobId = "job-1",
        ChannelId = "C1",
        UserId = "U1",
        Platform = "slack",
        Project = "proj",
        TicketId = string.Empty,
        StartedAt = DateTimeOffset.UtcNow,
    };

    private sealed class RecordingProvider : ITicketProvider
    {
        private readonly List<(string Title, string Body, IReadOnlyList<string> Labels)> _created = [];

        public IReadOnlyList<(string Title, string Body, IReadOnlyList<string> Labels)> Created => _created;

        public List<(string Child, string Parent)> Links { get; } = [];

        public string? RefuseLinkOf { get; init; }

        public (string Child, Exception Error)? ThrowOnLinkOf { get; init; }

        public Exception? ThrowOnCreate { get; init; }

        public string ProviderType => "recording";

        public Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken) =>
            Task.FromResult(ConnectionProbeResult.Reachable(0));

        public Task<Ticket> GetTicketAsync(TicketId ticketId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CreatedTicket> CreateAsync(
            string title, string description, IReadOnlyList<string> labels,
            CancellationToken cancellationToken)
        {
            if (ThrowOnCreate is not null) throw ThrowOnCreate;
            _created.Add((title, description, labels));
            return Task.FromResult(new CreatedTicket(
                new TicketId(_created.Count.ToString()), $"https://tracker.test/{_created.Count}"));
        }

        public Task<ParentLinkResult> LinkToParentAsync(
            CreatedTicket child, TicketId parent, CancellationToken cancellationToken)
        {
            if (ThrowOnLinkOf is { } thrown && child.Id.Value == thrown.Child) throw thrown.Error;
            if (child.Id.Value == RefuseLinkOf)
                return Task.FromResult(ParentLinkResult.Failed("the link type is disabled"));
            Links.Add((child.Id.Value, parent.Value));
            return Task.FromResult(ParentLinkResult.Linked);
        }

        public Task<TicketFinalizeResult> FinalizeAsync(
            TicketId ticketId, string comment, string? doneStatus, CancellationToken cancellationToken) =>
            Task.FromResult(TicketFinalizeResult.Moved());
    }
}
