using System.Net;
using System.Text.Json;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;

namespace AgentSmith.Tests.Providers.Tickets;

/// <summary>
/// 2026-09-17-042ea: each tracker links a filed child to its parent through its own relation.
/// Jira and GitLab are exercised at the HTTP boundary; Azure DevOps and GitHub construct their
/// SDK clients internally, so the patch and the request builder are the pinned surface.
/// </summary>
public sealed class TicketProviderParentLinkTests
{
    private static readonly CreatedTicket Child = new(new TicketId("PROJ-8"), null);

    [Fact]
    public void AzureDevOps_LinkToParent_PatchesAHierarchyRelationWithTheRestUrl()
    {
        var patch = AzureDevOpsTicketProvider.BuildParentLinkPatch("https://dev.azure.com/org/", 41);

        var op = patch.Should().ContainSingle().Subject;
        op.Path.Should().Be("/relations/-");
        var relation = op.Value.Should().BeOfType<WorkItemRelation>().Subject;
        relation.Rel.Should().Be("System.LinkTypes.Hierarchy-Reverse", "the child names its parent");
        relation.Url.Should().Be("https://dev.azure.com/org/_apis/wit/workItems/41",
            "a relation takes the REST url, not the web url a person follows");
    }

    [Fact]
    public void GitHub_SubIssueRequest_CarriesTheChildsDatabaseId()
    {
        var request = GitHubSubIssueRequest.For("owner", "repo", 7, 2_900_000_123L);

        request.Path.ToString().Should().Be("repos/owner/repo/issues/7/sub_issues");
        request.Body.Should().ContainSingle()
            .Which.Should().Be(new KeyValuePair<string, object>("sub_issue_id", 2_900_000_123L),
                "the sub-issue is named by its database id, never its issue number");
    }

    [Fact]
    public void GitHub_Created_CarriesTheDatabaseIdAsNativeId()
    {
        var issue = new Octokit.Internal.SimpleJsonSerializer().Deserialize<Octokit.Issue>(
            """{ "id": 2900000123, "number": 7, "html_url": "https://github.com/owner/repo/issues/7" }""");

        var created = GitHubTicketProvider.Created(issue);

        created.Id.Value.Should().Be("7");
        created.NativeId.Should().Be("2900000123", "the sub-issue link names the child by its database id");
    }

    [Fact]
    public async Task GitHub_LinkToParent_WithoutNativeId_IsAFailedLink()
    {
        var sut = new GitHubTicketProvider(
            new GitHubTicketConnection("https://github.com/owner/repo", "token"),
            new GitHubAttachmentLoader(new HttpClient(), NullLogger<GitHubAttachmentLoader>.Instance),
            new GitHubFieldMapper(), NullLogger<GitHubTicketProvider>.Instance);

        var result = await sut.LinkToParentAsync(
            new CreatedTicket(new TicketId("8"), null), new TicketId("7"), CancellationToken.None);

        result.Outcome.Should().Be(ParentLinkOutcome.Failed);
        result.Reason.Should().Contain("database id");
    }

    [Theory]
    [InlineData("41", "not-an-id")]
    [InlineData("not-an-id", "40")]
    public async Task AzureDevOps_LinkToParent_NotAWorkItemId_IsAFailedLink(string child, string parent)
    {
        var connection = new AzureDevOpsTicketConnection("https://dev.azure.com/org", "Project", "token");
        var sut = new AzureDevOpsTicketProvider(connection,
            new AzureDevOpsAttachmentLoader(connection, new HttpClient(), NullLogger.Instance),
            new AzureDevOpsFieldMapper(), NullLogger<AzureDevOpsTicketProvider>.Instance);

        var result = await sut.LinkToParentAsync(
            new CreatedTicket(new TicketId(child), null), new TicketId(parent), CancellationToken.None);

        result.Outcome.Should().Be(ParentLinkOutcome.Failed,
            "a patch that is never sent must not report the link as made");
        result.Reason.Should().Contain("not an Azure DevOps work item id");
    }

    [Fact]
    public async Task Jira_LinkToParent_TimedOut_IsAFailedLink()
    {
        var handler = new RecordingHandler { Responder = _ => throw new TaskCanceledException("timed out") };

        var result = await Jira(handler, linkType: null)
            .LinkToParentAsync(Child, new TicketId("PROJ-7"), CancellationToken.None);

        result.Outcome.Should().Be(ParentLinkOutcome.Failed,
            "an HttpClient timeout is not the caller cancelling, and must not abort the filing");
    }

    [Fact]
    public async Task Jira_LinkToParent_CallerCancelled_Throws()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var handler = new RecordingHandler { Responder = _ => throw new TaskCanceledException("cancelled") };

        var act = () => Jira(handler, linkType: null).LinkToParentAsync(Child, new TicketId("PROJ-7"), cancelled.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Jira_LinkToParent_CreatesAnIssueLinkOfTheConfiguredType()
    {
        var handler = new RecordingHandler();
        var sut = Jira(handler, linkType: "Parent of");

        var result = await sut.LinkToParentAsync(Child, new TicketId("PROJ-7"), CancellationToken.None);

        result.Should().Be(ParentLinkResult.Linked);
        handler.Requests.Single().Method.Should().Be(HttpMethod.Post);
        handler.Requests.Single().RequestUri!.AbsolutePath.Should().Be("/rest/api/3/issueLink");
        using var body = JsonDocument.Parse(handler.Bodies.Single()!);
        body.RootElement.GetProperty("type").GetProperty("name").GetString().Should().Be("Parent of");
        body.RootElement.GetProperty("inwardIssue").GetProperty("key").GetString().Should().Be("PROJ-7");
        body.RootElement.GetProperty("outwardIssue").GetProperty("key").GetString().Should().Be("PROJ-8");
    }

    [Fact]
    public async Task Jira_LinkToParent_MissingLinkType_IsAFailedLink()
    {
        var handler = new RecordingHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("""{"errorMessages":["No issue link type with name 'Relates' found."]}"""),
            },
        };

        var result = await Jira(handler, linkType: null)
            .LinkToParentAsync(Child, new TicketId("PROJ-7"), CancellationToken.None);

        result.Outcome.Should().Be(ParentLinkOutcome.Failed, "a site's refusal is a result, not a throw");
        result.Reason.Should().Contain("No issue link type with name 'Relates' found");
    }

    [Fact]
    public async Task GitLab_LinkToParent_CreatesARelatesToLink()
    {
        var handler = new RecordingHandler();
        var connection = new GitLabTicketConnection("https://gitlab.com", "group%2Fproj", "token");
        var httpClient = new HttpClient(handler);
        var sut = new GitLabTicketProvider(connection, httpClient,
            new GitLabAttachmentLoader(connection, httpClient, NullLogger<GitLabAttachmentLoader>.Instance),
            new GitLabFieldMapper(), NullLogger<GitLabTicketProvider>.Instance);

        var result = await sut.LinkToParentAsync(
            new CreatedTicket(new TicketId("34"), null), new TicketId("33"), CancellationToken.None);

        result.Should().Be(ParentLinkResult.Linked);
        handler.Requests.Single().RequestUri!.ToString()
            .Should().Be("https://gitlab.com/api/v4/projects/group%2Fproj/issues/34/links");
        using var body = JsonDocument.Parse(handler.Bodies.Single()!);
        body.RootElement.GetProperty("target_project_id").GetString().Should().Be("group/proj");
        body.RootElement.GetProperty("target_issue_iid").GetString().Should().Be("33");
        body.RootElement.GetProperty("link_type").GetString().Should().Be("relates_to");
    }

    [Fact]
    public async Task GitLab_LinkToParent_Refused_IsAFailedLink()
    {
        var handler = new RecordingHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent("""{"message":"403 Forbidden"}"""),
            },
        };
        var connection = new GitLabTicketConnection("https://gitlab.com", "group%2Fproj", "token");
        var httpClient = new HttpClient(handler);
        var sut = new GitLabTicketProvider(connection, httpClient,
            new GitLabAttachmentLoader(connection, httpClient, NullLogger<GitLabAttachmentLoader>.Instance),
            new GitLabFieldMapper(), NullLogger<GitLabTicketProvider>.Instance);

        var result = await sut.LinkToParentAsync(
            new CreatedTicket(new TicketId("34"), null), new TicketId("33"), CancellationToken.None);

        result.Outcome.Should().Be(ParentLinkOutcome.Failed);
        result.Reason.Should().Contain("403");
    }

    [Fact]
    public async Task Jira_GetTicket_RequestsAndMapsItsLabels()
    {
        var handler = new RecordingHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{ "key": "PROJ-8", "fields": { "summary": "s", "labels": ["phase", "phase-parent:PROJ-7"] } }"""),
            },
        };

        var ticket = await Jira(handler, linkType: null).GetTicketAsync(new TicketId("PROJ-8"), CancellationToken.None);

        handler.Requests.Single().RequestUri!.Query.Split('=')[1].Split(',').Should().Contain("labels",
            "every stamp read after the funnel is a label, and Jira returns only the fields it is asked for");
        ticket.Labels.Should().Equal("phase", "phase-parent:PROJ-7");
    }

    private static JiraTicketProvider Jira(HttpMessageHandler handler, string? linkType) =>
        new(new JiraTicketConnection("https://jira.example.com", "user@example.com", "token", "PROJ",
                ParentLinkType: linkType),
            new HttpClient(handler), new JiraFieldMapper(), NullLogger<JiraTicketProvider>.Instance);

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public List<string?> Bodies { get; } = [];
        public Func<HttpRequestMessage, HttpResponseMessage> Responder { get; init; }
            = _ => new HttpResponseMessage(HttpStatusCode.Created);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));
            return Responder(request);
        }
    }
}
