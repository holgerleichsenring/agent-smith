using System.Net;
using System.Text.Json;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;

namespace AgentSmith.Tests.Providers.Tickets;

/// <summary>
/// 2026-10-14-dbcem: the requires link on Azure DevOps, Jira and GitLab — the request each sends, and that a refusal
/// is a class with no host, organisation or response body in it.
/// </summary>
public sealed class TicketRequiresLinkTests
{
    private const string HostBody = """{"errorMessages":["No issue link type 'Blocks' on https://sample-org.atlassian.net for https://dev.azure.com/sample-org"]}""";

    [Fact]
    public async Task AzureDevOpsRequiresLink_Link_PatchesDependentWithDependencyReverseToRequiredRestUrl()
    {
        var sent = new List<(JsonPatchDocument Patch, int Id)>();
        var sut = AzureDevOps((patch, id) => { sent.Add((patch, id)); return Task.CompletedTask; });

        var result = await sut.LinkAsync(new TicketId("42"), new TicketId("41"), CancellationToken.None);

        result.Should().Be(RequiresLinkResult.Done);
        var (document, target) = sent.Should().ContainSingle().Subject;
        target.Should().Be(42, "the dependent carries the relation");
        var op = document.Should().ContainSingle().Subject;
        op.Path.Should().Be("/relations/-");
        var relation = op.Value.Should().BeOfType<WorkItemRelation>().Subject;
        relation.Rel.Should().Be("System.LinkTypes.Dependency-Reverse", "the reverse end names its target as the predecessor");
        relation.Url.Should().Be("https://dev.azure.com/sample-org/_apis/wit/workItems/41");
    }

    [Theory]
    [InlineData("42", "PROJ-1")]
    [InlineData("x", "41")]
    public async Task AzureDevOpsRequiresLink_NonNumericId_RefusedWithoutACall(string dependent, string required)
    {
        var calls = 0;
        var result = await AzureDevOps((_, _) => { calls++; return Task.CompletedTask; })
            .LinkAsync(new TicketId(dependent), new TicketId(required), CancellationToken.None);

        result.Linked.Should().BeFalse();
        result.Unreachable.Should().BeFalse();
        calls.Should().Be(0);
    }

    [Fact]
    public async Task JiraRequiresLink_Link_PostsBlocksIssueLinkWithRequiredInwardAndDependentOutward()
    {
        var handler = new LinkHandler(HttpStatusCode.Created);

        var result = await Jira(handler).LinkAsync(new TicketId("PROJ-8"), new TicketId("PROJ-7"), CancellationToken.None);

        result.Should().Be(RequiresLinkResult.Done, "Jira answers an issue link with an empty 201");
        handler.Request!.Method.Should().Be(HttpMethod.Post);
        handler.Request.RequestUri!.ToString().Should().Be("https://sample.atlassian.net/rest/api/3/issueLink");
        using var body = JsonDocument.Parse(handler.Body!);
        body.RootElement.GetProperty("type").GetProperty("name").GetString().Should().Be("Blocks");
        body.RootElement.GetProperty("inwardIssue").GetProperty("key").GetString().Should().Be("PROJ-7");
        body.RootElement.GetProperty("outwardIssue").GetProperty("key").GetString().Should().Be("PROJ-8");
    }

    [Fact]
    public async Task JiraRequiresLink_DataCenterIssuePath_PostsToApiTwoIssueLink()
    {
        var handler = new LinkHandler(HttpStatusCode.Created);

        await Jira(handler, new JiraEndpoints { Create = "/rest/api/2/issue" })
            .LinkAsync(new TicketId("PROJ-8"), new TicketId("PROJ-7"), CancellationToken.None);

        handler.Request!.RequestUri!.AbsolutePath.Should().Be("/rest/api/2/issueLink");
    }

    [Fact]
    public async Task JiraRequiresLink_CreatePathWithTrailingSlash_PostsToIssueLink()
    {
        var handler = new LinkHandler(HttpStatusCode.Created);

        await Jira(handler, new JiraEndpoints { Create = "/rest/api/3/issue/" })
            .LinkAsync(new TicketId("PROJ-8"), new TicketId("PROJ-7"), CancellationToken.None);

        handler.Request!.RequestUri!.AbsolutePath.Should().Be("/rest/api/3/issueLink");
    }

    [Fact]
    public async Task JiraRequiresLink_LinkTypeMissing_RefusedWithTrackerMessage()
    {
        // The tracker's message stays in the log; the person reads its class.
        var result = await Jira(new LinkHandler(HttpStatusCode.NotFound, HostBody))
            .LinkAsync(new TicketId("PROJ-8"), new TicketId("PROJ-7"), CancellationToken.None);

        result.Should().Be(RequiresLinkResult.Refused("the tracker refused the link (HTTP 404)"));
    }

    [Fact]
    public async Task GitLabRequiresLink_Link_PostsRelatesToWithDecodedProjectPathInBody()
    {
        var handler = new LinkHandler(HttpStatusCode.Created, "{}");
        var sut = new GitLabRequiresLink(
            new GitLabTicketConnection("https://gitlab.example", Uri.EscapeDataString("group/app"), "token"),
            new HttpClient(handler), NullLogger.Instance);

        var result = await sut.LinkAsync(new TicketId("8"), new TicketId("7"), CancellationToken.None);

        result.Should().Be(RequiresLinkResult.Done);
        handler.Request!.RequestUri!.OriginalString.Should().Be("https://gitlab.example/api/v4/projects/group%2Fapp/issues/8/links");
        using var body = JsonDocument.Parse(handler.Body!);
        body.RootElement.GetProperty("target_project_id").GetString().Should().Be("group/app");
        body.RootElement.GetProperty("target_issue_iid").GetString().Should().Be("7");
        body.RootElement.GetProperty("link_type").GetString().Should().Be("relates_to");
    }

    [Theory]
    [InlineData("azure-devops")]
    [InlineData("jira")]
    [InlineData("gitlab")]
    public async Task RequiresLink_TrackerUnreachable_UnreachedNeverThrows(string tracker)
    {
        var socket = new HttpRequestException("No such host is known. (sample-org.atlassian.net:443)");
        var sut = Of(tracker, LinkHandler.Throwing(socket), new VssServiceException("TF400893: https://dev.azure.com/sample-org is offline"));

        var result = await sut.LinkAsync(new TicketId("8"), new TicketId("7"), CancellationToken.None);

        result.Should().Be(RequiresLinkResult.Unreached("the tracker did not answer"));
    }

    [Theory]
    [InlineData("azure-devops", "the tracker refused the link (HTTP 400)")]
    [InlineData("jira", "the tracker refused the link (HTTP 400)")]
    [InlineData("gitlab", "not authorized")]
    public async Task RequiresLink_Refused_ReasonCarriesNoHostOrSdkText(string tracker, string reason)
    {
        var status = tracker == "gitlab" ? HttpStatusCode.Forbidden : HttpStatusCode.BadRequest;
        var sut = Of(tracker, new LinkHandler(status, HostBody),
            new VssServiceResponseException(HttpStatusCode.BadRequest, HostBody, null!));

        var result = await sut.LinkAsync(new TicketId("8"), new TicketId("7"), CancellationToken.None);

        result.Should().Be(RequiresLinkResult.Refused(reason));
        result.Reason.Should().NotContain("sample-org").And.NotContain("http");
    }

    [Fact]
    public async Task RequiresLink_CallerCancelled_Throws()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var act = () => Jira(LinkHandler.Throwing(new TaskCanceledException()))
            .LinkAsync(new TicketId("PROJ-8"), new TicketId("PROJ-7"), cancelled.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task TicketProviderFactory_DefaultCreateRequiresLink_RefusesNamingTheFactoryType()
    {
        ITicketProviderFactory factory = new AgentSmith.Tests.TestHelpers.StubTicketProviderFactory();

        var result = await factory.CreateRequiresLink(new TrackerConnection { Type = TrackerType.Jira })
            .LinkAsync(new TicketId("PROJ-8"), new TicketId("PROJ-7"), CancellationToken.None);

        result.Should().Be(RequiresLinkResult.Refused("StubTicketProviderFactory builds no tracker link"));
    }

    private static ITicketRequiresLink Of(string tracker, LinkHandler handler, Exception azureFailure) => tracker switch
    {
        "azure-devops" => AzureDevOps((_, _) => throw azureFailure),
        "jira" => Jira(handler),
        _ => new GitLabRequiresLink(new GitLabTicketConnection("https://gitlab.example", "group%2Fapp", "token"),
            new HttpClient(handler), NullLogger.Instance),
    };

    private static AzureDevOpsRequiresLink AzureDevOps(Func<JsonPatchDocument, int, Task> patch) =>
        new("https://dev.azure.com/sample-org/", (document, id, _) => patch(document, id), NullLogger.Instance);

    private static JiraRequiresLink Jira(LinkHandler handler, JiraEndpoints? endpoints = null) =>
        new(new JiraTicketConnection("https://sample.atlassian.net/", "bot@example.com", "token", Endpoints: endpoints),
            new HttpClient(handler), NullLogger.Instance);

    private sealed class LinkHandler(HttpStatusCode status, string body = "", Exception? failure = null) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        public static LinkHandler Throwing(Exception failure) => new(HttpStatusCode.OK, "", failure);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            if (failure is not null) throw failure;
            return new HttpResponseMessage(status) { Content = new StringContent(body) };
        }
    }
}
