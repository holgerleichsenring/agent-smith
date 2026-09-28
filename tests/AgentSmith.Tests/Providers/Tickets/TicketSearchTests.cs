using System.Net;
using System.Text.Json;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Octokit;
using Octokit.Internal;

namespace AgentSmith.Tests.Providers.Tickets;

/// <summary>
/// 2026-09-27-5c1ea: what each tracker's text search ASKS FOR, what it does with a cap, and how it
/// answers a query that never ran. The last one is the point of the port: the list helpers these
/// searches deliberately do not reuse collapse a failed query into an empty list, and an empty list
/// tells a person their ticket does not exist.
/// </summary>
public sealed class TicketSearchTests
{
    private const string Jql = "jql";

    [Fact]
    public async Task TicketSearch_Jira_AsksOverSummaryAndDescriptionOrderedByUpdated()
    {
        var handler = new RecordingHandler("""{"issues":[]}""");
        await Jira(handler, projectKey: "PROJ").SearchAsync("widget drops", 10, default);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.AbsolutePath.Should().Be("/rest/api/3/search/jql");
        var jql = Field(handler.LastBody!, Jql);
        jql.Should().Contain("summary ~ \"widget drops*\"")
            .And.Contain("description ~ \"widget drops*\"")
            .And.Contain("project = \"PROJ\"")
            .And.EndWith("ORDER BY updated DESC");
        // Over-asked by one, so "more existed" is measured rather than guessed.
        JsonDocument.Parse(handler.LastBody!).RootElement.GetProperty("maxResults").GetInt32()
            .Should().Be(11);
    }

    [Fact]
    public async Task TicketSearch_JiraWithNoProjectKey_SearchesTheSiteAsTheFetchAlreadyDoes()
    {
        var handler = new RecordingHandler("""{"issues":[]}""");
        await Jira(handler, projectKey: null).SearchAsync("widget", 10, default);

        Field(handler.LastBody!, Jql).Should().NotContain("project =");
    }

    [Fact]
    public void TicketSearch_AzureDevOps_MatchesATitleFragmentAndTheReproStepsOfABug()
    {
        var clause = AzureDevOpsTextMatchClause.For("auth")!;

        // A fragment: plain CONTAINS on the String-typed title, so "auth" reaches "authentication".
        clause.Should().Contain("[System.Title] CONTAINS 'auth'")
            .And.NotContain("[System.Title] CONTAINS WORDS");
        // A Bug's body is ReproSteps, and the long-text fields take only the full-text operator.
        clause.Should().Contain("[Microsoft.VSTS.TCM.ReproSteps] CONTAINS WORDS 'auth'")
            .And.Contain("[System.Description] CONTAINS WORDS 'auth'");
    }

    [Fact]
    public async Task TicketSearch_GitLab_AsksWithItsSearchParameterScopedToTheProject()
    {
        var handler = new RecordingHandler("[]");
        await GitLab(handler).SearchAsync("widget drops", 10, default);

        var uri = handler.LastRequest!.RequestUri!;
        uri.AbsolutePath.Should().Be("/api/v4/projects/group%2Fapp/issues");
        uri.Query.Should().Contain("search=widget%20drops")
            .And.Contain("in=title,description")
            .And.Contain("order_by=updated_at")
            .And.Contain("per_page=11");
    }

    [Fact]
    public async Task TicketSearch_GitHub_AsksItsSearchEndpointScopedToTheRepositoryOverTitleAndBody()
    {
        var (sut, asked) = GitHub(Found(totalCount: 1, "widget drops"));

        var result = await sut.SearchAsync("widget drops", 10, default);

        asked.Single().Term.Should().Be("widget drops");
        asked.Single().Repos.Should().ContainSingle().Which.Should().Be("owner/app");
        asked.Single().In.Should().BeEquivalentTo([IssueInQualifier.Title, IssueInQualifier.Body]);
        asked.Single().SortField.Should().Be(IssueSearchSort.Updated);
        // The search endpoint returns pull requests from the same index; a picker must not show them.
        asked.Single().Type.Should().Be(IssueTypeQualifier.Issue);
        result.Hits.Should().ContainSingle().Which.Title.Should().Be("widget drops");
    }

    [Fact]
    public async Task TicketSearch_MoreMatchesThanTheCap_SaysSomethingWasHeldBack()
    {
        // GitLab is over-asked by one, so the extra row IS the evidence.
        var handler = new RecordingHandler(Issues(3));
        var capped = await GitLab(handler).SearchAsync("widget", 2, default);

        capped.Hits.Should().HaveCount(2);
        capped.MoreHeldBack.Should().BeTrue();

        // GitHub reports the whole match count instead, so the cap is read off the tracker.
        var (github, _) = GitHub(Found(totalCount: 99, "widget"));
        var reported = await github.SearchAsync("widget", 10, default);
        reported.MoreHeldBack.Should().BeTrue();
        reported.Searched.Should().BeTrue();
    }

    [Fact]
    public async Task TicketSearch_ATrackerThatCannotRunTheQuery_IsDistinguishableFromNoMatches()
    {
        var broken = await GitLab(new RecordingHandler("boom", HttpStatusCode.InternalServerError))
            .SearchAsync("widget", 10, default);
        var empty = await GitLab(new RecordingHandler("[]")).SearchAsync("widget", 10, default);

        broken.Outcome.Should().Be(TicketSearchOutcome.Failed);
        broken.Reason.Should().NotBeNullOrWhiteSpace();
        empty.Outcome.Should().Be(TicketSearchOutcome.Searched);
        empty.Reason.Should().BeNull();
        // Both are empty; only the outcome tells a person which question was answered.
        broken.Hits.Should().BeEmpty();
        empty.Hits.Should().BeEmpty();
    }

    [Fact]
    public async Task TicketSearch_TextCarryingQuotesBackslashesAndWildcards_IsEscapedNotRefused()
    {
        const string hostile = "he said \"drop\" ~ * ? : \\ [x] AND";

        var handler = new RecordingHandler("""{"issues":[]}""");
        var jira = await Jira(handler, "PROJ").SearchAsync(hostile, 10, default);
        var jql = Field(handler.LastBody!, Jql);

        jira.Searched.Should().BeTrue("a typed operator must not become a refusal");
        // The operand is ONE balanced quoted term: every Lucene operator became whitespace.
        jql.Should().Contain("summary ~ \"he said drop x AND*\"");
        jql.Count(c => c == '"').Should().Be(6, "two quotes per operand, two around the project key");

        // WIQL escapes its delimiter by doubling it, so a typed apostrophe cannot end the literal.
        AzureDevOpsTextMatchClause.For("o'brien")!.Should().Contain("CONTAINS 'o''brien'");

        // A GitHub colon would turn the rest of the word into a qualifier.
        GitHubTicketSearch.Term("repo:other/secret").Should().Be("repo other/secret");

        // Text that is nothing BUT operators is not a query, and asks no tracker.
        JiraTicketSearch.Operand("~*?").Should().BeNull();
        var untouched = new RecordingHandler("""{"issues":[]}""");
        (await Jira(untouched, "PROJ").SearchAsync("~*?", 10, default)).Should().Be(TicketSearchResult.None);
        untouched.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task TicketSearch_EveryTracker_AsksForOpenTicketsOnly()
    {
        var jira = new RecordingHandler("""{"issues":[]}""");
        await Jira(jira, "PROJ").SearchAsync("widget", 10, default);
        Field(jira.LastBody!, Jql).Should().Contain("statusCategory != Done");

        var gitlab = new RecordingHandler("[]");
        await GitLab(gitlab).SearchAsync("widget", 10, default);
        gitlab.LastRequest!.RequestUri!.Query.Should().Contain("state=opened");

        var (github, asked) = GitHub(Found(0));
        await github.SearchAsync("widget", 10, default);
        asked.Single().State.Should().Be(ItemState.Open);

        // Azure DevOps scopes by the operator's open-state list, and by the team project with it.
        AzureDevOpsOpenScope.Where("Contoso", ["New", "Resolved"])
            .Should().Be("[System.TeamProject] = 'Contoso' AND [System.State] IN ('New', 'Resolved')");
        AzureDevOpsOpenScope.Where("Contoso", null)
            .Should().Contain("IN ('New', 'Active', 'Committed')");
    }


    [Fact]
    public async Task TicketNumberPrefix_Jira_EnumeratesTheKeysItStandsFor()
    {
        var handler = new RecordingHandler("""{"issues":[]}""");
        await Jira(handler, projectKey: "PROJ").SearchAsync("194", 10, default);

        var jql = Field(handler.LastBody!, Jql);
        // Jira has no prefix operator on a key and takes an enumerated list, which this tree
        // already sends elsewhere.
        jql.Should().Contain("key IN (").And.Contain("\"PROJ-194\"").And.Contain("\"PROJ-19400\"");
        // And the text match is still there: a number can appear in a title too.
        jql.Should().Contain("summary ~");
    }

    [Fact]
    public async Task TicketNumberPrefix_JiraWithNoProjectKey_AsksForNoKeys()
    {
        var handler = new RecordingHandler("""{"issues":[]}""");
        await Jira(handler, projectKey: null).SearchAsync("194", 10, default);

        // Without a configured project there is no prefix to put the number on.
        Field(handler.LastBody!, Jql).Should().NotContain("key IN");
    }

    [Fact]
    public async Task TicketNumberPrefix_GitLab_EnumeratesTheIidsItStandsFor()
    {
        var handler = new RecordingHandler("[]");
        await GitLab(handler).SearchAsync("194", 10, default);

        // The SECOND request is the numbered one; the text search is unchanged.
        handler.LastRequest!.RequestUri!.Query.Should().Contain("iids[]=194")
            .And.Contain("iids[]=19400").And.NotContain("search=");
    }

    [Fact]
    public async Task TicketNumberPrefix_TextThatIsNotAllDigits_AsksForNoPrefix()
    {
        var handler = new RecordingHandler("[]");
        await GitLab(handler).SearchAsync("widget", 10, default);

        handler.LastRequest!.RequestUri!.Query.Should().Contain("search=widget").And.NotContain("iids");
    }

    private static JiraTicketSearch Jira(RecordingHandler handler, string? projectKey) =>
        new(new JiraTicketConnection("https://jira.example.com", "a@b.c", "token", projectKey),
            new HttpClient(handler), new JiraFieldMapper(), NullLogger.Instance);

    private static GitLabTicketSearch GitLab(RecordingHandler handler) =>
        new(new GitLabTicketConnection("https://gitlab.example.com", "group%2Fapp", "token"),
            new HttpClient(handler), new GitLabFieldMapper(), NullLogger.Instance);

    private static (GitHubTicketSearch Sut, List<SearchIssuesRequest> Asked) GitHub(SearchIssuesResult answer)
    {
        var asked = new List<SearchIssuesRequest>();
        var search = new Mock<ISearchClient>();
        search.Setup(s => s.SearchIssues(It.IsAny<SearchIssuesRequest>()))
            .Callback<SearchIssuesRequest>(asked.Add)
            .ReturnsAsync(answer);
        var client = new Mock<IGitHubClient>();
        client.SetupGet(c => c.Search).Returns(search.Object);
        return (new GitHubTicketSearch(
            client.Object, new GitHubTicketConnection("https://github.com/owner/app", "token"),
            NullLogger.Instance), asked);
    }

    private static SearchIssuesResult Found(int totalCount, params string[] titles)
    {
        var items = string.Join(",", titles.Select((t, i) => $"{{\"number\":{i + 1},\"title\":\"{t}\"}}"));
        return new SimpleJsonSerializer().Deserialize<SearchIssuesResult>(
            $"{{\"total_count\":{totalCount},\"incomplete_results\":false,\"items\":[{items}]}}");
    }

    private static string Issues(int count) =>
        "[" + string.Join(",", Enumerable.Range(1, count)
            .Select(i => $"{{\"iid\":{i},\"title\":\"widget {i}\",\"description\":\"\",\"state\":\"opened\"}}")) + "]";

    private static string Field(string body, string name) =>
        JsonDocument.Parse(body).RootElement.GetProperty(name).GetString()!;

    private sealed class RecordingHandler(string body, HttpStatusCode status = HttpStatusCode.OK)
        : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(body) };
        }
    }
}
