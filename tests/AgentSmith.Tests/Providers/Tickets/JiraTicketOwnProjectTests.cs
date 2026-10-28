using System.Net;
using System.Text;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Providers.Tickets;

/// <summary>
/// 2026-10-28-7a69a: a Jira tracker with a project key holds that project's issues only. Jira's
/// <c>/issue/{key}</c> answers for the whole site, so without this two trackers on one site each
/// return the other's issues and a ticket link binds to whichever tracker is asked first.
/// </summary>
public sealed class JiraTicketOwnProjectTests
{
    private const string Site = "https://site.example.test";

    [Fact]
    public async Task JiraGetTicket_IssueOfAnotherProject_IsNotFound()
    {
        var sut = Provider("ALP", Issue("BET-12"));

        var act = () => sut.GetTicketAsync(new TicketId("BET-12"), CancellationToken.None);

        await act.Should().ThrowAsync<TicketNotFoundException>();
    }

    [Fact]
    public async Task JiraGetTicket_MovedIssueIntoThisProject_IsFound()
    {
        // Asked by its old key; Jira answers with the key it has now, in this tracker's project.
        var sut = Provider("BET", Issue("BET-9"));

        var ticket = await sut.GetTicketAsync(new TicketId("ALP-5"), CancellationToken.None);

        ticket.Title.Should().Be("Widget drops");
    }

    [Fact]
    public async Task JiraGetTicket_NumericIdOfThisProject_IsFoundByTheKeyJiraReturns()
    {
        var sut = Provider("BET", Issue("BET-12"));

        var ticket = await sut.GetTicketAsync(new TicketId("10012"), CancellationToken.None);

        ticket.Title.Should().Be("Widget drops");
    }

    [Fact]
    public async Task TicketProjectChoice_TwoJiraTrackersOnOneSite_BindsTheTrackerOfTheKey()
    {
        var choice = new TicketProjectChoice(new SiteTrackers(), NullLogger<TicketProjectChoice>.Instance);

        var answer = await choice.ForAsync(Config(), "BET-12", CancellationToken.None);

        answer!.Binding.Tracker.Should().Be("bet", "the alpha tracker is asked first, but BET-12 is not its issue");
        answer.Projects.Should().BeEquivalentTo(["beta-team"]);
    }

    private static JiraTicketProvider Provider(string? projectKey, string json) => new(
        new JiraTicketConnection(Site, "bot@site.example.test", "token", projectKey),
        new HttpClient(new FixedHandler(json)), new JiraFieldMapper(), NullLogger<JiraTicketProvider>.Instance);

    private static string Issue(string key) => "{\"key\":\"" + key + "\"," + """
        "fields":{"summary":"Widget drops","status":{"name":"To Do"},"labels":["bet-tag"],"issuetype":{"name":"Task"}}}
        """;

    private static AgentSmithConfig Config() => new()
    {
        Trackers = new Dictionary<string, TrackerConnection>
        {
            ["alp"] = Tracker("alp", "ALP"),
            ["bet"] = Tracker("bet", "BET"),
        },
        Projects = new Dictionary<string, ResolvedProject>
        {
            ["alpha-team"] = Project("alpha-team", Tracker("alp", "ALP"), "alp-tag"),
            ["beta-team"] = Project("beta-team", Tracker("bet", "BET"), "bet-tag"),
        },
    };

    private static TrackerConnection Tracker(string name, string project) =>
        new() { Name = name, Type = TrackerType.Jira, Url = Site, Project = project };

    private static ResolvedProject Project(string name, TrackerConnection tracker, string tag) => new()
    {
        Name = name,
        Tracker = tracker,
        JiraTrigger = new JiraTriggerConfig
        {
            ProjectResolution = new ProjectResolutionConfig { Strategy = ResolutionStrategy.Tag, Value = tag },
        },
    };

    // One site: every tracker's HTTP reaches the same issue, as Jira's does.
    private sealed class SiteTrackers : ITicketProviderFactory
    {
        public ITicketProvider Create(TrackerConnection config) => Provider(config.Project, Issue("BET-12"));

        public ITicketRewriter CreateRewriter(TrackerConnection config) => new RecordingTicketRewriter();

        public ITicketSearch CreateSearch(TrackerConnection config) => new RecordingTicketSearch();

        public ITicketLinkedWork CreateLinkedWork(TrackerConnection config) => new RecordingLinkedWork();
    }

    private sealed class FixedHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
    }
}
