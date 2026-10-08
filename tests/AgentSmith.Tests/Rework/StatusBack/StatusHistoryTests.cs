using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using FluentAssertions;
using Moq;
using Octokit;

namespace AgentSmith.Tests.Rework.StatusBack;

/// <summary>2026-10-08-2123: each tracker's history read — newest person's move into a trigger status.</summary>
public sealed class StatusHistoryTests
{
    private static readonly string[] ToDo = ["To Do"];

    private static JiraStatusHistory Jira(Func<string, (HttpStatusCode, string)> answer, JiraEndpoints? endpoints = null) =>
        new(TicketProviderHttpClient.WithBasicAuth(new HttpClient(new Routed(answer)), "e", "t"), "https://jira.example", endpoints ?? new JiraEndpoints());

    private const string History = """
        { "id": "1", "created": "2026-10-08T12:00:00.000+0000", "author": { "accountId": "alice-id", "displayName": "Alice" },
          "items": [ { "field": "status", "fromString": "Done", "toString": "To Do" } ] }
        """;

    [Fact]
    public async Task JiraHistory_CloudAndDataCenterShapes_Read()
    {
        var cloud = await Jira(url => (HttpStatusCode.OK, $$"""{ "total": 1, "values": [ {{History}} ] }"""))
            .NewestPersonMoveIntoAsync(new TicketId("A-1"), ToDo, null, CancellationToken.None);
        var dataCenter = await Jira(url => url.Contains("/changelog") ? (HttpStatusCode.NotFound, "{}")
                : (HttpStatusCode.OK, $$"""{ "changelog": { "histories": [ {{History}} ] } }"""))
            .NewestPersonMoveIntoAsync(new TicketId("A-1"), ToDo, null, CancellationToken.None);

        cloud!.At.Should().Be(new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero));
        dataCenter!.Actor.Id.Should().Be("alice-id");
    }

    [Fact]
    public async Task JiraDataCenter_OwnMove_Ignored()
    {
        var endpoints = new JiraEndpoints { Changelog = "/rest/api/2/issue/{id}?expand=changelog" };
        var own = History.Replace("\"accountId\": \"alice-id\"", "\"key\": \"agent\", \"name\": \"agent\"");

        var move = await Jira(_ => (HttpStatusCode.OK, $$"""{ "changelog": { "histories": [ {{own}} ] } }"""), endpoints)
            .NewestPersonMoveIntoAsync(new TicketId("A-1"), ToDo, new TrackerActor("agent", "agent"), CancellationToken.None);

        move.Should().BeNull();
    }

    [Fact]
    public async Task GitLabHistory_Reopened_IsMove()
    {
        var history = new GitLabStatusHistory(TicketProviderHttpClient.WithPrivateToken(new HttpClient(new Routed(_ => (HttpStatusCode.OK, """
            [ { "id": 1, "state": "closed", "created_at": "2026-10-08T11:00:00Z", "user": { "id": 7, "username": "bob" } },
              { "id": 2, "state": "reopened", "created_at": "2026-10-08T12:00:00Z", "user": { "id": 7, "username": "bob" } } ]
            """))), "t"), "https://gitlab.example", "o%2Fr");

        var move = await history.NewestPersonMoveIntoAsync(new TicketId("3"), ["opened"], null, CancellationToken.None);

        move!.Actor.Login.Should().Be("bob");
        move.At.Should().Be(new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task GitHubHistory_AppReopen_Ignored()
    {
        var client = new Mock<IGitHubClient>();
        client.Setup(c => c.Issue.Events.GetAllForIssue("o", "r", 4))
            .ReturnsAsync([Reopen("renovate[bot]", AccountType.Bot, 12), Reopen("alice", AccountType.User, 11)]);

        var move = await new GitHubStatusHistory(client.Object, "o", "r")
            .NewestPersonMoveIntoAsync(new TicketId("4"), ["open"], null, CancellationToken.None);

        move!.Actor.Login.Should().Be("alice", "the bot's later reopen is no person's move");
    }

    private static IssueEvent Reopen(string login, AccountType type, int hour)
    {
        var actor = (User)RuntimeHelpers.GetUninitializedObject(typeof(User));
        Set(actor, nameof(User.Login), login);
        Set(actor, nameof(User.Type), (AccountType?)type);
        var e = (IssueEvent)RuntimeHelpers.GetUninitializedObject(typeof(IssueEvent));
        Set(e, nameof(IssueEvent.Actor), actor);
        Set(e, nameof(IssueEvent.Event), new StringEnum<EventInfoState>(EventInfoState.Reopened));
        Set(e, nameof(IssueEvent.CreatedAt), new DateTimeOffset(2026, 10, 8, hour, 0, 0, TimeSpan.Zero));
        return e;
    }

    private static void Set(object target, string property, object? value) =>
        target.GetType().GetProperty(property)!.SetValue(target, value);

    private sealed class Routed(Func<string, (HttpStatusCode Code, string Body)> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var (code, body) = answer(request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
