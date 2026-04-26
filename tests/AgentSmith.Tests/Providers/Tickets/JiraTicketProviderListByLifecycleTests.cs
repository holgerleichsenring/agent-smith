using System.Net;
using System.Text.Json;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Providers.Tickets;

public sealed class JiraTicketProviderListByLifecycleTests
{
    [Fact]
    public async Task ListByLifecycleStatusAsync_PostsToSearchEndpointWithJqlBody()
    {
        var handler = new RecordingHandler
        {
            Responder = _ => JsonResponse("""{"issues":[]}""")
        };
        var sut = BuildSut(handler, projectKey: "PROJ");

        await sut.ListByLifecycleStatusAsync(TicketLifecycleStatus.Pending, CancellationToken.None);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.AbsolutePath.Should().Be("/rest/api/3/search");

        var bodyJson = await handler.LastRequest.Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(bodyJson);
        var jql = doc.RootElement.GetProperty("jql").GetString();
        jql.Should().Contain("project = \"PROJ\"");
        jql.Should().Contain("labels = \"agent-smith:pending\"");
    }

    [Fact]
    public async Task ListByLifecycleStatusAsync_NoProjectKey_OmitsProjectClause()
    {
        var handler = new RecordingHandler
        {
            Responder = _ => JsonResponse("""{"issues":[]}""")
        };
        var sut = BuildSut(handler, projectKey: null);

        await sut.ListByLifecycleStatusAsync(TicketLifecycleStatus.Pending, CancellationToken.None);

        var body = await handler.LastRequest!.Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var jql = doc.RootElement.GetProperty("jql").GetString();
        jql.Should().NotContain("project =");
        jql.Should().Be("labels = \"agent-smith:pending\"");
    }

    [Fact]
    public async Task ListByLifecycleStatusAsync_TwoIssues_MapsKeyAsIdAndParsesAdfDescription()
    {
        var adf = """
            {
              "type": "doc",
              "version": 1,
              "content": [
                { "type": "paragraph", "content": [
                    { "type": "text", "text": "hello world" } ] }
              ]
            }
            """;
        var handler = new RecordingHandler
        {
            Responder = _ => JsonResponse($$"""
                {
                  "issues": [
                    {
                      "key": "PROJ-42",
                      "fields": {
                        "summary": "first",
                        "description": {{adf}},
                        "status": { "name": "To Do" },
                        "labels": ["agent-smith:pending", "security-review"]
                      }
                    },
                    {
                      "key": "PROJ-43",
                      "fields": {
                        "summary": "second",
                        "description": null,
                        "status": { "name": "To Do" },
                        "labels": []
                      }
                    }
                  ]
                }
                """)
        };
        var sut = BuildSut(handler, projectKey: "PROJ");

        var tickets = await sut.ListByLifecycleStatusAsync(
            TicketLifecycleStatus.Pending, CancellationToken.None);

        tickets.Should().HaveCount(2);
        tickets[0].Id.Value.Should().Be("PROJ-42");
        tickets[0].Title.Should().Be("first");
        tickets[0].Description.Should().Contain("hello world");
        tickets[0].Status.Should().Be("To Do");
        tickets[0].Labels.Should().BeEquivalentTo(new[] { "agent-smith:pending", "security-review" });
        tickets[1].Id.Value.Should().Be("PROJ-43");
        tickets[1].Description.Should().Be(""); // null description maps to empty
        tickets[1].Labels.Should().BeEmpty();
    }

    [Fact]
    public async Task ListByLifecycleStatusAsync_HttpError_ReturnsEmptyList()
    {
        var handler = new RecordingHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        };
        var sut = BuildSut(handler, projectKey: "PROJ");

        var tickets = await sut.ListByLifecycleStatusAsync(
            TicketLifecycleStatus.Pending, CancellationToken.None);

        tickets.Should().BeEmpty();
    }

    private static JiraTicketProvider BuildSut(HttpMessageHandler handler, string? projectKey)
    {
        var httpClient = new HttpClient(handler);
        return new JiraTicketProvider(
            "https://jira.example.com",
            "user@example.com",
            "token",
            httpClient,
            NullLogger<JiraTicketProvider>.Instance,
            projectKey: projectKey);
    }

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
    };

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public Func<HttpRequestMessage, HttpResponseMessage> Responder { get; set; }
            = _ => new HttpResponseMessage(HttpStatusCode.OK);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(Responder(request));
        }
    }
}
