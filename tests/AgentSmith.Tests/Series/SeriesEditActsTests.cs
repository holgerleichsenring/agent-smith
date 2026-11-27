using AgentSmith.Application.Services.Filing;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Server.Services.Series;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Server.Services.Tickets;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-25-9992f: the series master's cut changes. add_ticket files one unstarted ticket through the 'file' run
/// seeded with the series id, then stores its edges, each copying its dependent's StartsWhenMet; link_tickets adds and
/// removes edges; a self-edge or a cycle is refused with the ticket named and nothing filed or written.</summary>
public sealed class SeriesEditActsTests
{
    private readonly InMemoryTicketFacts _facts = new();
    private readonly Mock<IFilingRun> _filing = new();
    private readonly List<(FilingRequest Request, string? Series)> _filed = [];

    private SeriesEditActs Acts(ITicketRequirementStore? store = null)
    {
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject>
            {
                ["sample"] = new() { Name = "sample", Tracker = new TrackerConnection { Name = TicketRequirementEdges.Tracker, Type = TrackerType.Jira } },
            },
        });
        _filing.Setup(f => f.RunRequestAsync("sample", It.IsAny<FilingRequest>(), It.IsAny<string?>()))
            .Callback<string, FilingRequest, string?>((_, r, s) => _filed.Add((r, s)))
            .ReturnsAsync(new FilingReport([new FiledTicket("https://jira/SCRUM-46", "Prerequisite") { TicketId = "46", Key = "SCRUM-46" }], null));
        return new SeriesEditActs(new TicketProjects(loader.Object, new ServerContext("c.yml")), _filing.Object,
            new SeriesRequirementLinks(store ?? _facts, TimeProvider.System),
            new SeriesAddedSpec(new AgentSmith.Application.Services.SpecDialog.SpecDraftValidator(
                new AgentSmith.Application.Services.Validation.SpecSchemaProvider()), new AgentSmith.Application.Services.SpecDialog.SpecDraftReader()));
    }

    // 2026-11-27-3532a: add_ticket files a spec; its own requires name specs, never tickets, and are not read.
    private const string Spec = "spec: 2026-11-27-ab12\ngoal: \"The index exists before the API reads it\"\nrequires: [2026-11-01-0001]\nrepos: [api]\n";

    private static SeriesRunRecord Series => SeriesRecords.Running();

    private void Edge(string ticket, string required, bool startsWhenMet) =>
        _facts.Edges.Add(new(TicketRequirementEdges.Tracker, $"jira-{ticket}", ticket, "sample", $"jira-{required}", required, startsWhenMet, default, "conv-1"));

    private IEnumerable<(string, string, bool)> Stored => _facts.Edges.Select(e => (e.TicketId, e.RequiredTicketId, e.StartsWhenMet));

    [Fact]
    public async Task AddTicket_FilesOneUnstartedTicketIntoTheSeries_ThenStoresItsEdges()
    {
        Edge("44", "43", startsWhenMet: false);

        var added = await Acts().AddTicketAsync(Series, Spec, ["43"], ["44"], default);

        added.Should().Be(new SeriesCutAnswer(
            "Filed SCRUM-46 into the series, unstarted; it requires SCRUM-43; SCRUM-44 requires it; it starts once what it requires has landed, or by start_ticket.", true, "46", "SCRUM-46"));
        _filed.Should().ContainSingle().Which.Should().Match<(FilingRequest Request, string? Series)>(f => f.Series == "sr-1"
            && !f.Request.MayStartRuns && f.Request.IsSeriesMasterAddition && f.Request.FollowUp == null
            && f.Request.ScopeRepos.SequenceEqual(new[] { "api" }) && f.Request.Conversation == "conv-1" && f.Request.Principal == "operator");
        var draft = _filed[0].Request.Proposal.Should().BeOfType<AgentSmith.Contracts.Models.SpecOutcome>().Which.Draft;
        (draft.SpecId, draft.Requires.Count).Should().Be(("2026-11-27-ab12", 0), "the spec's own requires are not read");
        Stored.Should().BeEquivalentTo([("44", "43", false), ("46", "43", true), ("44", "46", false)],
            "a new edge copies its dependent's StartsWhenMet, true when it has none");
    }

    [Fact]
    public async Task AddTicket_EdgesThatCannotBeStored_SaysTheTicketWasFiledWithoutThem()
    {
        var store = new Mock<ITicketRequirementStore>();
        store.Setup(s => s.ByProjectAsync("sample", It.IsAny<CancellationToken>())).ReturnsAsync([]);
        store.Setup(s => s.RequiredOfAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        store.Setup(s => s.AddAsync(It.IsAny<IReadOnlyList<TicketRequirementEdge>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("the store is down"));

        var added = await Acts(store.Object).AddTicketAsync(Series, Spec, ["43"], [], default);

        added.Changed.Should().BeTrue("the ticket was filed and joined the series");
        added.Answer.Should().StartWith("Filed SCRUM-46 into the series, unstarted, but its requirement edges were not stored")
            .And.EndWith("link_tickets stores them.");
    }

    [Fact]
    public async Task AddTicket_BetweenTicketsThatWouldCloseACycle_IsRefusedAndFilesNothing()
    {
        Edge("43", "44", startsWhenMet: true);

        var added = await Acts().AddTicketAsync(Series, Spec, ["43"], ["44"], default);

        added.Changed.Should().BeFalse();
        added.Answer.Should().Contain("SCRUM-43 already waits for SCRUM-44").And.Contain("nothing was filed");
        _filed.Should().BeEmpty();
    }

    [Fact]
    public async Task AddTicket_ASpecTheSchemaRefuses_FilesNothingAndSaysWhy()
    {
        var added = await Acts().AddTicketAsync(Series, "goal: \"no spec id\"\n", [], [], default);

        added.Changed.Should().BeFalse();
        added.Answer.Should().StartWith("Nothing was filed: the spec is not valid:").And.Contain("spec");
        _filed.Should().BeEmpty();
    }

    [Fact]
    public async Task Link_AddsAndRemovesEdges()
    {
        Edge("45", "43", startsWhenMet: true);

        var linked = await Acts().LinkAsync(Series, "45", ["44"], ["43"], default);

        linked.Should().Be(new SeriesCutAnswer("SCRUM-45 no longer requires SCRUM-43. SCRUM-45 requires SCRUM-44 (1 new).", true));
        Stored.Should().Equal(("45", "44", true));
    }

    [Fact]
    public async Task Link_ACycle_IsRefusedWithTheTicketNamed_AndNothingChanges()
    {
        Edge("44", "43", startsWhenMet: true);
        Edge("45", "44", startsWhenMet: true);

        var linked = await Acts().LinkAsync(Series, "43", ["45"], [], default);

        linked.Changed.Should().BeFalse();
        linked.Answer.Should().Be("Refused: SCRUM-45 already waits for SCRUM-43, so SCRUM-43 requiring it closes a cycle; nothing was changed.");
        Stored.Should().HaveCount(2);
    }

    [Fact]
    public async Task Link_ASelfEdge_IsRefused()
    {
        var linked = await Acts().LinkAsync(Series, "43", ["43"], [], default);

        linked.Should().Be(new SeriesCutAnswer("Refused: SCRUM-43 cannot require itself; nothing was changed.", false));
        _facts.Edges.Should().BeEmpty();
    }

    [Fact]
    public async Task Link_RemovingTheEdgeThatClosedTheCycle_LetsTheNewEdgeStand()
    {
        Edge("44", "43", startsWhenMet: true);

        var linked = await Acts().LinkAsync(Series, "43", ["44"], [], default);
        var relinked = await Acts().LinkAsync(Series, "44", [], ["43"], default);
        var reversed = await Acts().LinkAsync(Series, "43", ["44"], [], default);

        (linked.Changed, relinked.Changed, reversed.Changed).Should().Be((false, true, true));
        Stored.Should().Equal(("43", "44", true));
    }
}
