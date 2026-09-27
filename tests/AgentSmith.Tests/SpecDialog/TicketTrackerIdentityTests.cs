using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-27-1bd9: a ticket read by its id may only name projects on the tracker that ANSWERED
/// for it.
/// <para>
/// The match keeps a project whose trigger kind equals the ticket's platform, and two Jira trackers
/// are both "jira". The binding re-fetches the ticket by id on the chosen project's tracker, so a
/// project matched on another one would open a different board's ticket of the same number —
/// silently, because both reads succeed.
/// </para>
/// </summary>
public sealed class TicketTrackerIdentityTests
{
    private const string First = "jira-one";
    private const string Second = "jira-two";

    [Fact]
    public async Task TicketProjectChoice_TwoTrackersOfOneKind_NamesOnlyTheAnsweringTrackersProjects()
    {
        // Both projects carry the same tag and both trackers are Jira, so the label match alone
        // names both. Only the first tracker has the ticket.
        var answer = await Sut(holder: First).ForAsync(Config(), "412", CancellationToken.None);

        answer!.Binding.Tracker.Should().Be(First);
        answer.Projects.Should().BeEquivalentTo(["alpha"]);
        answer.Elsewhere.Should().BeEquivalentTo(["beta"],
            "a project the labels DO name, on a tracker that cannot hold this ticket");
    }

    [Fact]
    public async Task TicketProjectChoice_LabelsNamingAnotherTrackersProject_IsAskedAboutRatherThanBound()
    {
        // The ticket lives on the second tracker; the only project the labels name on it is beta.
        var answer = await Sut(holder: Second).ForAsync(Config(), "412", CancellationToken.None);

        answer!.Projects.Should().BeEquivalentTo(["beta"]);
        answer.Elsewhere.Should().BeEquivalentTo(["alpha"]);
    }

    [Fact]
    public async Task TicketProjectChoice_ATicketOnATrackerWithNoProject_NamesNoneAndSaysWhere()
    {
        var config = Config();
        config.Trackers["jira-three"] = new TrackerConnection { Name = "jira-three", Type = TrackerType.Jira };

        var answer = await Sut(holder: "jira-three").ForAsync(config, "412", CancellationToken.None);

        answer!.Projects.Should().BeEmpty("no configured project is routed to that tracker");
        // The page needs the difference between "your labels match nothing" and "they match
        // somewhere this ticket cannot be worked from".
        answer.Elsewhere.Should().BeEquivalentTo(["alpha", "beta"]);
    }

    private static TicketProjectChoice Sut(string holder) =>
        new(new OneTrackerHolds(holder), NullLogger<TicketProjectChoice>.Instance);

    private static AgentSmithConfig Config() => new()
    {
        Trackers = new Dictionary<string, TrackerConnection>
        {
            [First] = new() { Name = First, Type = TrackerType.Jira },
            [Second] = new() { Name = Second, Type = TrackerType.Jira },
        },
        Projects = new Dictionary<string, ResolvedProject>
        {
            ["alpha"] = Project("alpha", First),
            ["beta"] = Project("beta", Second),
        },
    };

    private static ResolvedProject Project(string name, string tracker) => new()
    {
        Name = name,
        Tracker = new TrackerConnection { Name = tracker, Type = TrackerType.Jira },
        JiraTrigger = new JiraTriggerConfig
        {
            ProjectResolution = new ProjectResolutionConfig
            {
                Strategy = ResolutionStrategy.Tag,
                Value = "shared-tag",
            },
        },
    };

    /// <summary>Exactly one tracker has the number; the rest do not answer for it.</summary>
    private sealed class OneTrackerHolds(string holder) : ITicketProviderFactory
    {
        public ITicketProvider Create(TrackerConnection config) =>
            config.Name == holder
                ? new StubTicketProvider(id => new Ticket(
                    id, "Widget drops", string.Empty, null, "Open", "Jira", ["shared-tag"]))
                : new StubTicketProvider(id => throw new InvalidOperationException($"no ticket {id.Value}"));

        public ITicketRewriter CreateRewriter(TrackerConnection config) => new RecordingTicketRewriter();

        public ITicketSearch CreateSearch(TrackerConnection config) => new RecordingTicketSearch();
    }
}
