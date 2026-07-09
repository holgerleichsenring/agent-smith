using System.Net;
using System.Text.Json;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Providers.Tickets;

/// <summary>
/// p0315f: CreateAsync across the four providers. GitLab/Jira are exercised at
/// the HTTP boundary (RecordingHandler, same convention as the lifecycle
/// tests); GitHub (Octokit) and AzDO (VssConnection) construct their SDK
/// clients internally, so their create payload/URL builders are pinned as the
/// testable pure surface.
/// </summary>
public sealed class TicketProviderCreateTests
{
    [Fact]
    public async Task GitLabCreateAsync_PostsIssueWithLabelsAndMapsIidAndWebUrl()
    {
        var handler = new RecordingHandler
        {
            Responder = _ => JsonResponse(
                """{ "iid": 33, "web_url": "https://gitlab.com/group/proj/-/issues/33" }""")
        };
        var sut = BuildGitLabSut(handler);

        var created = await sut.CreateAsync(
            "New widget", "Widget body", ["phase", "backlog"], CancellationToken.None);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.ToString()
            .Should().Be("https://gitlab.com/api/v4/projects/group%2Fproj/issues");
        using var body = JsonDocument.Parse(handler.LastRequestBody!);
        body.RootElement.GetProperty("title").GetString().Should().Be("New widget");
        body.RootElement.GetProperty("description").GetString().Should().Be("Widget body");
        body.RootElement.GetProperty("labels").GetString().Should().Be("phase,backlog");
        created.Id.Value.Should().Be("33");
        created.WebUrl.Should().Be("https://gitlab.com/group/proj/-/issues/33");
    }

    [Fact]
    public async Task JiraCreateAsync_PostsAdfIssueWithProjectKeyAndMapsBrowseUrl()
    {
        var handler = new RecordingHandler
        {
            Responder = _ => JsonResponse("""{ "id": "10001", "key": "PROJ-7" }""")
        };
        var sut = BuildJiraSut(handler, projectKey: "PROJ");

        var created = await sut.CreateAsync(
            "New widget", "line one\nline two", ["phase"], CancellationToken.None);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.AbsolutePath.Should().Be("/rest/api/3/issue");
        using var body = JsonDocument.Parse(handler.LastRequestBody!);
        var fields = body.RootElement.GetProperty("fields");
        fields.GetProperty("project").GetProperty("key").GetString().Should().Be("PROJ");
        fields.GetProperty("summary").GetString().Should().Be("New widget");
        fields.GetProperty("issuetype").GetProperty("name").GetString().Should().Be("Task");
        fields.GetProperty("labels").EnumerateArray().Select(l => l.GetString())
            .Should().Equal("phase");
        var description = fields.GetProperty("description");
        description.GetProperty("type").GetString().Should().Be("doc");
        JiraAdfParser.ExtractText(description).Should().Be("line one\nline two",
            "the ADF description must read back with its line structure intact");
        created.Id.Value.Should().Be("PROJ-7");
        created.WebUrl.Should().Be("https://jira.example.com/browse/PROJ-7");
    }

    [Fact]
    public async Task JiraCreateAsync_NoProjectKey_FailsLoud()
    {
        var handler = new RecordingHandler();
        var sut = BuildJiraSut(handler, projectKey: null);

        var act = () => sut.CreateAsync("t", "d", [], CancellationToken.None);

        await act.Should().ThrowAsync<ConfigurationException>()
            .WithMessage("*project key*");
        handler.LastRequest.Should().BeNull("nothing must be sent without a target project");
    }

    [Fact]
    public void GitHubCreateAsync_BuildsNewIssueWithLabels()
    {
        var issue = GitHubTicketProvider.BuildNewIssue("New widget", "Widget body", ["phase", "bug"]);

        issue.Title.Should().Be("New widget");
        issue.Body.Should().Be("Widget body");
        issue.Labels.Should().Equal("phase", "bug");
    }

    [Fact]
    public void AzureDevOpsCreateAsync_BuildsCreatePatchAndWebUrl()
    {
        var patch = AzureDevOpsTicketProvider.BuildCreatePatch(
            "New widget", "<p>Widget body</p>", ["phase", "backlog"]);

        patch.Select(op => (op.Path, (string)op.Value!)).Should().Equal(
            ("/fields/System.Title", "New widget"),
            ("/fields/System.Description", "<p>Widget body</p>"),
            ("/fields/System.Tags", "phase; backlog"));

        AzureDevOpsTicketProvider.WorkItemWebUrl("https://dev.azure.com/org/", "My Project", 77)
            .Should().Be("https://dev.azure.com/org/My%20Project/_workitems/edit/77");
    }

    [Fact]
    public void AzureDevOpsCreateAsync_EmptyDescriptionAndLabels_OmitsOptionalFields()
    {
        var patch = AzureDevOpsTicketProvider.BuildCreatePatch("Just a title", "", []);

        patch.Select(op => op.Path).Should().Equal("/fields/System.Title");
    }

    // ---- suts + plumbing (mirrors the ListByLifecycle test conventions) ----

    private static GitLabTicketProvider BuildGitLabSut(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var connection = new GitLabTicketConnection("https://gitlab.com", "group%2Fproj", "token");
        var loader = new GitLabAttachmentLoader(
            connection, httpClient, NullLogger<GitLabAttachmentLoader>.Instance);
        return new GitLabTicketProvider(
            connection, httpClient, loader,
            new GitLabFieldMapper(), NullLogger<GitLabTicketProvider>.Instance);
    }

    private static JiraTicketProvider BuildJiraSut(HttpMessageHandler handler, string? projectKey)
    {
        var httpClient = new HttpClient(handler);
        return new JiraTicketProvider(
            new JiraTicketConnection(
                "https://jira.example.com", "user@example.com", "token", projectKey),
            httpClient,
            new JiraFieldMapper(),
            NullLogger<JiraTicketProvider>.Instance);
    }

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
    };

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }
        public Func<HttpRequestMessage, HttpResponseMessage> Responder { get; set; }
            = _ => new HttpResponseMessage(HttpStatusCode.OK);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            // The provider http client disposes the request content after the
            // call; read the body now so assertions can run afterwards.
            if (request.Content is not null)
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            return Responder(request);
        }
    }
}
