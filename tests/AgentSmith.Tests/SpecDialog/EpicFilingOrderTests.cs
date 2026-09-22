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
/// 2026-09-17-0e79d: the order is the SET's — one run works the slices in it, phase by phase.
/// The label stamps that used to carry the order are gone with the N-children shape.
/// </para>
/// <para>
/// 2026-09-22-b3d7: and the tickets that used to carry it are gone too. The order is now read on
/// the one ticket a cut files: its "## Slices" section, and the set stored under its spec key.
/// The ORDERING ITSELF is untouched — a cut whose edges cannot be ordered is still refused before
/// anything is created.
/// </para>
/// </summary>
public sealed class EpicFilingOrderTests
{
    [Fact]
    public async Task FileEpic_ForwardEdge_ListsTheSlicesInDependencyOrder()
    {
        var provider = new RecordingProvider();

        var report = await FileAsync(provider, Epic(
            Child("p9000a", requires: ["p9000b"]),
            Child("p9000b")));

        report.Error.Should().BeNull();
        provider.Created.Should().ContainSingle().Which.Title.Should().Be("p9000: Widget platform");
        var body = provider.Created[0].Body;
        body.IndexOf("p9000b", StringComparison.Ordinal).Should()
            .BeLessThan(body.IndexOf("p9000a", StringComparison.Ordinal),
                "the cut listed the slices in the wrong order; the stored set and the slice list follow the edges");
    }

    [Fact]
    public async Task Filing_ACutWhoseEdgesCannotBeOrdered_IsStillRefusedBeforeAnyTicketExists()
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
    /// 2026-09-17-0e79d: the position stamps are gone from the filing shape. A parent stamp on
    /// the work ticket would cut its branch from another ticket's rung instead of from its own
    /// base.
    /// </summary>
    [Fact]
    public async Task FileEpic_NothingItFiles_CarriesAStamp()
    {
        var provider = new RecordingProvider();

        await FileAsync(provider, Epic(Child("p9000a"), Child("p9000b", requires: ["p9000a"])));

        provider.Created.Should().OnlyContain(c => FiledTicketLabels.ParentId(c.Labels) == null);
    }

    /// <summary>
    /// 2026-09-22-b3d7: a cut files ONE ticket, so the parent link its records were tied to the
    /// work ticket with has no call site left, and the filing report has no note to carry.
    /// </summary>
    [Fact]
    public async Task EpicFiling_NothingIsLinkedToAnything_AndNoNoteIsRaised()
    {
        var provider = new RecordingProvider();

        var report = await FileAsync(provider, Epic(Child("p9000a"), Child("p9000b", requires: ["p9000a"])));

        provider.Links.Should().BeEmpty();
        report.Notes.Should().BeEmpty();
        report.Filed.Should().ContainSingle();
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
            new EpicChildOrderer(), TestSupport.ApprovedSetDoubles.SetFiler(),
            FiledWorkDoubles.Starter(), ApprovedSetDoubles.Kinds(), NullLogger<OutcomeTicketFiler>.Instance);
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

        public Exception? ThrowOnCreate { get; init; }

        public string ProviderType => "recording";

        public Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken) =>
            Task.FromResult(ConnectionProbeResult.Reachable(0));

        public Task<Ticket> GetTicketAsync(TicketId ticketId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CreatedTicket> CreateAsync(
            string title, string description, IReadOnlyList<string> labels, string? kind,
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
            Links.Add((child.Id.Value, parent.Value));
            return Task.FromResult(ParentLinkResult.Linked);
        }

        public Task<TicketFinalizeResult> FinalizeAsync(
            TicketId ticketId, string comment, string? doneStatus, CancellationToken cancellationToken) =>
            Task.FromResult(TicketFinalizeResult.Moved());
    }
}
