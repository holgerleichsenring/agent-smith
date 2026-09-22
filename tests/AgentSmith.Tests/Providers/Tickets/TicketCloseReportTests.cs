using System.Net;
using System.Reflection;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Providers.Tickets;

/// <summary>
/// 2026-09-22-9519: the close stops being a call whose success says nothing. It used to return a
/// bare task, and three of the four providers already held the answer internally and dropped it —
/// one wrapped a call whose own comment says it answers whether the issue transitioned, one
/// returned quietly for an id that will not parse, one threw. Routing the question through
/// finalize would not have answered it either: with no target status, two of the four report the
/// ticket moved unconditionally.
/// </summary>
public sealed class TicketCloseReportTests
{
    private static readonly TicketId Ticket = new("42");

    /// <summary>
    /// The two providers whose id is a number they must parse. Neither may answer "closed" for an
    /// id it never sent anywhere: GitHub reports false and Azure DevOps reports by throwing, and
    /// both are answers — what neither does any more is return as though it had closed something.
    /// </summary>
    [Fact]
    public async Task Close_AnUnparseableTicketId_IsReportedNotSwallowed()
    {
        var github = new GitHubTicketProvider(
            new GitHubTicketConnection("https://github.com/owner/repo", "token"),
            new GitHubAttachmentLoader(new HttpClient(), NullLogger<GitHubAttachmentLoader>.Instance),
            new GitHubFieldMapper(), NullLogger<GitHubTicketProvider>.Instance);

        var closed = await github.CloseTicketAsync(new TicketId("not-an-id"), "withdrawn", default);

        closed.Should().BeFalse("nothing was sent, so nothing was closed");

        var connection = new AzureDevOpsTicketConnection("https://dev.azure.com/org", "Project", "token");
        var azure = new AzureDevOpsTicketProvider(
            connection,
            new AzureDevOpsAttachmentLoader(connection, new HttpClient(), NullLogger.Instance),
            new AzureDevOpsFieldMapper(), NullLogger<AzureDevOpsTicketProvider>.Instance);

        var act = () => azure.CloseTicketAsync(new TicketId("not-an-id"), "withdrawn", default);

        await act.Should().ThrowAsync<TicketNotFoundException>();
    }

    /// <summary>
    /// Jira moves an issue only along a workflow transition, and a workflow that offers none to the
    /// done status leaves the issue exactly as open. The comment still lands — it is the one word a
    /// human reads — and it is the ANSWER that is new.
    /// </summary>
    [Fact]
    public async Task Close_AWorkflowWithNoTransition_IsReportedAsNotClosed()
    {
        var handler = new RecordingHandler
        {
            Responder = request => request.Method == HttpMethod.Get
                ? Json("""{ "transitions": [] }""")
                : new HttpResponseMessage(HttpStatusCode.OK),
        };

        var closed = await Jira(handler).CloseTicketAsync(Ticket, "withdrawn", default);

        closed.Should().BeFalse("the issue is still in the status it was in");
        handler.Posted.Should().Contain(url => url.Contains("/comment"),
            "the resolution comment lands whether or not the workflow can move the issue");
    }

    /// <summary>
    /// EVERY provider answers, and none of them inherits the port's silent default. The behaviour
    /// of two is driven here; the other two are driven by the test above. The declaration check is
    /// what a FIFTH tracker runs into: a provider that simply never overrides the member would
    /// otherwise report "not closed" for every close it happily performed.
    /// </summary>
    [Fact]
    public async Task Close_EachProvider_AnswersWhetherItClosed()
    {
        var gitlab = new RecordingHandler();
        (await GitLab(gitlab).CloseTicketAsync(Ticket, "withdrawn", default))
            .Should().BeTrue("GitLab fails the request when it refuses the state event");

        var jira = new RecordingHandler
        {
            Responder = request => request.Method == HttpMethod.Get
                ? Json("""{ "transitions": [ { "id": "31", "name": "Done" } ] }""")
                : new HttpResponseMessage(HttpStatusCode.OK),
        };
        (await Jira(jira).CloseTicketAsync(Ticket, "withdrawn", default))
            .Should().BeTrue("the workflow offered the transition and it was posted");

        Type[] providers =
        [
            typeof(JiraTicketProvider), typeof(GitHubTicketProvider),
            typeof(GitLabTicketProvider), typeof(AzureDevOpsTicketProvider),
        ];
        foreach (var provider in providers)
            provider.GetMethod(
                nameof(ITicketProvider.CloseTicketAsync),
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Should().NotBeNull($"{provider.Name} must answer whether it closed the ticket itself");
    }

    private static JiraTicketProvider Jira(HttpMessageHandler handler) => new(
        new JiraTicketConnection("https://jira.example.com", "user@example.com", "token", "PROJ"),
        new HttpClient(handler), new JiraFieldMapper(), NullLogger<JiraTicketProvider>.Instance);

    private static GitLabTicketProvider GitLab(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var connection = new GitLabTicketConnection("https://gitlab.com", "group%2Fproj", "token");
        return new GitLabTicketProvider(
            connection, httpClient,
            new GitLabAttachmentLoader(connection, httpClient, NullLogger<GitLabAttachmentLoader>.Instance),
            new GitLabFieldMapper(), NullLogger<GitLabTicketProvider>.Instance);
    }

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
    };

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<string> Posted { get; } = [];
        public Func<HttpRequestMessage, HttpResponseMessage> Responder { get; set; }
            = _ => new HttpResponseMessage(HttpStatusCode.OK);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method != HttpMethod.Get)
                Posted.Add(request.RequestUri!.ToString());
            return Task.FromResult(Responder(request));
        }
    }
}
