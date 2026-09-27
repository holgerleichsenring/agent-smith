using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-27-5c1eb: the sweep behind the dialog's ticket field — every configured tracker asked,
/// bounded, and saying which of them could not answer.
/// <para>
/// The load-bearing case is the last one. A tracker that could not run the query and a board with
/// no such ticket are one answer in every list helper this tree already had, and the second reading
/// tells a person their ticket does not exist.
/// </para>
/// </summary>
public sealed class TicketSearchAcrossTrackersTests
{
    private const string Jira = "jira-main";
    private const string GitLab = "gitlab-main";
    private const string Ado = "ado-main";

    [Fact]
    public async Task TicketSearch_AcrossTrackers_CapsTheWholeAnswerAndNamesWhoCouldNotAnswer()
    {
        var tracker = new FakeTrackers();
        tracker.Searching(Jira).Answer = Hits(15);
        tracker.Searching(GitLab).Answer = Hits(10);
        // Not "no matches": a secret that is not configured, a tracker that is down, a 400 from a
        // query it would not take.
        tracker.Searching(Ado).Answer = TicketSearchResult.Failed("the organisation is unreachable");

        var answer = await Sut(tracker).ForAsync(Config(), "widget", CancellationToken.None);

        answer.Found.Should().HaveCount(TicketSearchAcrossTrackers.Cap);
        answer.MoreHeldBack.Should().BeTrue("twenty-five matched and twenty are shown");
        answer.Unsearchable.Should().BeEquivalentTo([Ado]);
        // Every tracker was asked for the same screenful, so the cap is the newest of each.
        tracker.Searching(Jira).Asked.Single().Should()
            .Be(("widget", TicketSearchAcrossTrackers.Cap));
    }

    [Fact]
    public async Task TicketSearch_ANumberOnATracker_IsFoundBesideTheTextMatchesAndCarriesItsTracker()
    {
        // A Jira key is in no title and no body, so a text search alone would never find it.
        var tracker = new FakeTrackers { Holding = (Jira, "DPG-1239", "Cannot log in") };

        var answer = await Sut(tracker).ForAsync(Config(), "DPG-1239", CancellationToken.None);

        var hit = answer.Found.Should().ContainSingle().Subject;
        hit.TicketId.Should().Be("DPG-1239");
        hit.Title.Should().Be("Cannot log in");
        // The tracker is on the hit because one number is different work on two boards.
        hit.Tracker.Should().Be(Jira);
        hit.Exact.Should().BeTrue("this is the number that was typed, not a ticket mentioning it");
        answer.Found.Should().StartWith(hit, "the exact hit is added before the text sweep");
    }

    [Fact]
    public async Task TicketSearch_ANumberLookupThatFailed_IsListedApartFromAnUnsearchableTracker()
    {
        // GitLab cannot be ASKED for a number; Azure DevOps cannot run the TEXT query. Two
        // different failures: one sentence cannot say both, and neither is an empty board.
        var tracker = new FakeTrackers { Unreachable = GitLab };
        tracker.Searching(Jira).Answer = Hits(1);
        tracker.Searching(GitLab).Answer = Hits(0);
        tracker.Searching(Ado).Answer = TicketSearchResult.Failed("the organisation is unreachable");

        var answer = await Sut(tracker).ForAsync(Config(), "412", CancellationToken.None);

        answer.Unreachable.Should().BeEquivalentTo([GitLab]);
        answer.Unsearchable.Should().BeEquivalentTo([Ado]);
    }

    [Fact]
    public async Task TicketSearch_ATrackerWithoutTheNumber_IsNotCalledUnreachable()
    {
        // The commonest case by far: the board simply has no such ticket. It must not be reported
        // as a board nothing is known about.
        var answer = await Sut(new FakeTrackers()).ForAsync(Config(), "412", CancellationToken.None);

        answer.Unreachable.Should().BeEmpty();
    }

    [Fact]
    public async Task TicketSearch_EveryHit_ReportsTheProjectsRoutedToItsTracker()
    {
        var tracker = new FakeTrackers();
        tracker.Searching(Jira).Answer = Hits(1);
        tracker.Searching(GitLab).Answer = Hits(1);

        var answer = await Sut(tracker).ForAsync(Config(), "widget", CancellationToken.None);

        // The wire that opens a conversation carries a project and NO tracker: the binding is
        // re-fetched by id on the project's tracker, so a hit sent with a project routed elsewhere
        // would bind a different board's ticket of the same number, silently.
        answer.Found.Single(f => f.Tracker == Jira).Projects.Should().BeEquivalentTo(["alpha", "beta"]);
        answer.Found.Single(f => f.Tracker == GitLab).Projects.Should().BeEquivalentTo(["gamma"]);
    }

    [Fact]
    public async Task TicketSearchRoute_TwoCharacters_AsksNoTracker()
    {
        // The loader THROWS: below the minimum the route must not even read the configuration, let
        // alone sweep every tracker. Two characters match most of a board.
        var trackers = new FakeTrackers();

        var answered = await TicketSearchEndpoints.SearchTicketsAsync(
            "ab", new ThrowingLoader(), new ServerContext("unused"), Sut(trackers),
            CancellationToken.None);

        answered.Should().BeAssignableTo<IStatusCodeHttpResult>().Which.StatusCode.Should().Be(200);
        trackers.Searching(Jira).Asked.Should().BeEmpty();
    }

    private static TicketSearchAcrossTrackers Sut(FakeTrackers trackers) =>
        new(trackers,
            new TicketProjectChoice(trackers, NullLogger<TicketProjectChoice>.Instance),
            NullLogger<TicketSearchAcrossTrackers>.Instance);

    private static TicketSearchResult Hits(int count) =>
        TicketSearchResult.Of(
            Enumerable.Range(1, count).Select(i => new TicketSearchHit(new TicketId($"{i}"), $"widget {i}")),
            TicketSearchAcrossTrackers.Cap);

    private static AgentSmithConfig Config() => new()
    {
        Trackers = new Dictionary<string, TrackerConnection>
        {
            [Jira] = new() { Name = Jira, Type = TrackerType.Jira },
            [GitLab] = new() { Name = GitLab, Type = TrackerType.GitLab },
            [Ado] = new() { Name = Ado, Type = TrackerType.AzureDevOps },
        },
        Projects = new Dictionary<string, ResolvedProject>
        {
            ["alpha"] = Project("alpha", Jira, TrackerType.Jira),
            ["beta"] = Project("beta", Jira, TrackerType.Jira),
            ["gamma"] = Project("gamma", GitLab, TrackerType.GitLab),
        },
    };

    private static ResolvedProject Project(string name, string tracker, TrackerType type) =>
        new() { Name = name, Tracker = new TrackerConnection { Name = tracker, Type = type } };

    /// <summary>The configured trackers as doubles: one recording search each, and at most one of
    /// them holding a ticket by its id.</summary>
    private sealed class FakeTrackers : ITicketProviderFactory
    {
        private readonly Dictionary<string, RecordingTicketSearch> _searches = new(StringComparer.Ordinal);

        public (string Tracker, string TicketId, string Title)? Holding { get; init; }

        /// <summary>2026-09-27-481bb: a tracker that cannot be ASKED, which is not a tracker
        /// without the ticket — the distinction the number lookup swallowed until now.</summary>
        public string? Unreachable { get; init; }

        public RecordingTicketSearch Searching(string tracker)
        {
            if (!_searches.TryGetValue(tracker, out var search))
                _searches[tracker] = search = new RecordingTicketSearch();
            return search;
        }

        public ITicketSearch CreateSearch(TrackerConnection config) => Searching(config.Name);

        public ITicketProvider Create(TrackerConnection config) =>
            config.Name == Unreachable
                ? new StubTicketProvider(_ => throw new HttpRequestException("the organisation is unreachable"))
                : Holding is { } held && held.Tracker == config.Name
                    ? new StubTicketProvider(id => id.Value == held.TicketId
                        ? new Ticket(id, held.Title, string.Empty, null, "Open", "Jira")
                        : throw new TicketNotFoundException(id))
                    // A tracker that does not have the number answers for it, with nothing.
                    : new StubTicketProvider(id => throw new TicketNotFoundException(id));

        public ITicketRewriter CreateRewriter(TrackerConnection config) => new RecordingTicketRewriter();
    }

    private sealed class ThrowingLoader : IConfigurationLoader
    {
        public AgentSmithConfig LoadConfig(string configPath) =>
            throw new InvalidOperationException("the configuration must not be read below the minimum");

        public ConfigFileReadFact? LastRead => null;
    }
}
