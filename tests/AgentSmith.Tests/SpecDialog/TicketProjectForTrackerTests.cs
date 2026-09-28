using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-27-481bb: the projects a ticket names on the tracker it was FOUND on.
/// <para>
/// A search hit is an id and a title by contract, so the sweep can offer only the projects routed
/// to its tracker — which means the same ticket resolved one project when found by its number and
/// offered all of them when found by its title. This reads the ticket a person committed to, on the
/// tracker they found it on rather than one guessed by sweeping again.
/// </para>
/// </summary>
public sealed class TicketProjectForTrackerTests
{
    private const string Holder = "jira-one";
    private const string Other = "jira-two";

    [Fact]
    public async Task TicketResolve_ATicketOnANamedTracker_NamesTheProjectsItsLabelsNameThere()
    {
        var answer = await Sut().ForTrackerAsync(Config(), Holder, "412", CancellationToken.None);

        answer!.Projects.Should().BeEquivalentTo(["alpha"],
            "the labels name alpha, and alpha is routed to the tracker that has the ticket");
        answer.Elsewhere.Should().BeEmpty();
    }

    [Fact]
    public async Task TicketResolve_TheTrackerThatWasNamed_IsTheOnlyOneAsked()
    {
        // The other tracker holds the same number. Sweeping would answer for whichever came first;
        // naming the tracker is what stops a picked hit resolving a different board's ticket.
        var answer = await Sut(bothHold: true)
            .ForTrackerAsync(Config(), Other, "412", CancellationToken.None);

        answer!.Binding.Tracker.Should().Be(Other);
        answer.Projects.Should().BeEquivalentTo(["beta"]);
    }

    [Fact]
    public async Task TicketResolve_ATrackerThatIsNotConfigured_AnswersNothing()
    {
        var answer = await Sut().ForTrackerAsync(Config(), "jira-three", "412", CancellationToken.None);

        answer.Should().BeNull();
    }

    [Fact]
    public async Task TicketResolve_ATrackerThatDoesNotHaveTheTicket_AnswersNothing()
    {
        var answer = await Sut().ForTrackerAsync(Config(), Other, "412", CancellationToken.None);

        answer.Should().BeNull();
    }

    private static TicketProjectForTracker Sut(bool bothHold = false) =>
        new(new TicketProjectChoice(
            new Trackers(bothHold), NullLogger<TicketProjectChoice>.Instance));

    private static AgentSmithConfig Config() => new()
    {
        Trackers = new Dictionary<string, TrackerConnection>
        {
            [Holder] = new() { Name = Holder, Type = TrackerType.Jira },
            [Other] = new() { Name = Other, Type = TrackerType.Jira },
        },
        Projects = new Dictionary<string, ResolvedProject>
        {
            ["alpha"] = Project("alpha", Holder, "alpha-tag"),
            ["beta"] = Project("beta", Other, "beta-tag"),
        },
    };

    private static ResolvedProject Project(string name, string tracker, string tag) => new()
    {
        Name = name,
        Tracker = new TrackerConnection { Name = tracker, Type = TrackerType.Jira },
        JiraTrigger = new JiraTriggerConfig
        {
            ProjectResolution = new ProjectResolutionConfig
            {
                Strategy = ResolutionStrategy.Tag,
                Value = tag,
            },
        },
    };

    private sealed class Trackers(bool bothHold) : ITicketProviderFactory
    {
        public ITicketProvider Create(TrackerConnection config) =>
            config.Name == Holder || bothHold
                ? new StubTicketProvider(id => new Ticket(
                    id, "Widget drops", string.Empty, null, "Open", "Jira",
                    [config.Name == Holder ? "alpha-tag" : "beta-tag"]))
                : new StubTicketProvider(id => throw new TicketNotFoundException(id));

        public ITicketRewriter CreateRewriter(TrackerConnection config) => new RecordingTicketRewriter();

        public ITicketSearch CreateSearch(TrackerConnection config) => new RecordingTicketSearch();
    }
}
