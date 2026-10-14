using System.Net;
using System.Text.Json;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Octokit;
using Octokit.Internal;

namespace AgentSmith.Tests.Providers.Tickets;

/// <summary>
/// Each tracker's field mapper names the format its description and criteria are written in, and the one copy of a
/// ticket (Ticket.With) carries it through the providers that rewrite a mapped ticket.
/// </summary>
public sealed class TicketFieldFormatTests
{
    internal const string AdfDescription = """
        { "type": "doc", "version": 1, "content": [
          { "type": "heading", "attrs": { "level": 2 }, "content": [{ "type": "text", "text": "Goal" }] },
          { "type": "paragraph", "content": [{ "type": "text", "text": "Export ", "marks": [{ "type": "strong" }] },
            { "type": "text", "text": "the report." }] } ] }
        """;

    [Fact]
    public void AzureDevOpsFieldMapper_DescriptionAndCriteria_AreHtml()
    {
        var ticket = new AzureDevOpsFieldMapper().Map(new TicketId("17"), AzureFields());

        (ticket.DescriptionFormat, ticket.CriteriaFormat).Should().Be((TicketTextFormat.Html, TicketTextFormat.Html));
    }

    [Fact]
    public void AzureDevOpsFieldMapper_UntaggedDescriptionAndCriteria_AreMarkdown()
    {
        var fields = AzureFields();
        fields["System.Description"] = "Goal\n====\n\n*   **Export** the report.";
        fields["Microsoft.VSTS.Common.AcceptanceCriteria"] = "- A CSV file.";

        var ticket = new AzureDevOpsFieldMapper().Map(new TicketId("17"), fields);

        (ticket.DescriptionFormat, ticket.CriteriaFormat).Should().Be((TicketTextFormat.Markdown, TicketTextFormat.Markdown));
    }

    [Fact]
    public void GitHubAndGitLabFieldMappers_Description_IsMarkdown()
    {
        var github = new GitHubFieldMapper().Map(new TicketId("7"),
            new SimpleJsonSerializer().Deserialize<Issue>("""{ "number": 7, "title": "T", "body": "**D**", "state": "open" }"""));
        var gitlab = new GitLabFieldMapper().Map(new TicketId("7"),
            JsonDocument.Parse("""{ "iid": 7, "title": "T", "description": "**D**", "state": "opened" }""").RootElement);

        github.DescriptionFormat.Should().Be(TicketTextFormat.Markdown);
        gitlab.DescriptionFormat.Should().Be(TicketTextFormat.Markdown);
    }

    [Fact]
    public void JiraFieldMapper_Description_IsUnchangedTextMarkedText()
    {
        var ticket = new JiraFieldMapper().Map(new TicketId("AS-1"), JiraIssue());

        ticket.Description.Should().Be(JiraAdfParser.ExtractText(JsonDocument.Parse(AdfDescription).RootElement));
        ticket.DescriptionFormat.Should().Be(TicketTextFormat.Text);
    }

    [Fact]
    public void AzureDevOpsTicketProvider_GetTicket_KeepsHtmlFormat()
    {
        // The provider rewrites the mapped ticket through With(webUrl, isClosed) before it returns it.
        var ticket = new AzureDevOpsFieldMapper().Map(new TicketId("17"), AzureFields())
            .With(webUrl: "https://dev.azure.com/org/Project/_workitems/edit/17", isClosed: true);

        (ticket.DescriptionFormat, ticket.CriteriaFormat).Should().Be((TicketTextFormat.Html, TicketTextFormat.Html));
    }

    [Fact]
    public async Task JiraTicketProvider_GetTicket_KeepsTextFormat()
    {
        var handler = new IssueHandler($$"""{ "key": "AS-1", "fields": { "summary": "T", "description": {{AdfDescription}}, "status": { "name": "Open" } } }""");
        var sut = new JiraTicketProvider(new JiraTicketConnection("https://jira.example.com", "u@example.com", "token", "AS"),
            new HttpClient(handler), new JiraFieldMapper(), NullLogger<JiraTicketProvider>.Instance);

        var ticket = await sut.GetTicketAsync(new TicketId("AS-1"), default);

        (ticket.WebUrl, ticket.DescriptionFormat).Should().Be(("https://jira.example.com/browse/AS-1", TicketTextFormat.Text));
    }

    internal static Dictionary<string, object> AzureFields() => new()
    {
        ["System.Title"] = "Export the report",
        ["System.Description"] = "<h2>Goal</h2><ul><li>Export</li></ul>",
        ["Microsoft.VSTS.Common.AcceptanceCriteria"] = "<p>A <b>CSV</b> file.</p>",
        ["System.State"] = "Active",
    };

    internal static JsonElement JiraIssue() =>
        JsonDocument.Parse($$"""{ "key": "AS-1", "fields": { "summary": "T", "description": {{AdfDescription}}, "status": { "name": "Open" } } }""").RootElement;

    private sealed class IssueHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
            });
    }
}
