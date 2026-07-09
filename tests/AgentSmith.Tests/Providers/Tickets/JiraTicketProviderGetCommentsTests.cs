using System.Net;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Providers.Tickets;

/// <summary>
/// p0317: the ticket conversation reaches the agent — Jira comment reads go to the
/// same /comment endpoint the framework posts to, and ADF bodies flatten to text.
/// </summary>
public sealed class JiraTicketProviderGetCommentsTests
{
    [Fact]
    public async Task GetCommentsAsync_MapsAuthorTimestampBody()
    {
        var handler = new RecordingCommentHandler
        {
            Responder = _ => JsonResponse("""
                {
                  "comments": [
                    {
                      "author": { "displayName": "Jane Operator" },
                      "created": "2026-07-01T10:15:00.000+0000",
                      "body": {
                        "type": "doc", "version": 1,
                        "content": [ { "type": "paragraph", "content": [
                          { "type": "text", "text": "use approach B, not A" } ] } ]
                      }
                    }
                  ]
                }
                """)
        };
        var sut = BuildSut(handler);

        var comments = await sut.GetCommentsAsync(new TicketId("PROJ-42"), CancellationToken.None);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.AbsolutePath.Should().Be("/rest/api/3/issue/PROJ-42/comment");
        comments.Should().HaveCount(1);
        comments[0].Author.Should().Be("Jane Operator");
        comments[0].CreatedAt.Should().Be(new DateTimeOffset(2026, 7, 1, 10, 15, 0, TimeSpan.Zero));
        comments[0].Body.Should().Be("use approach B, not A");
    }

    [Fact]
    public async Task GetCommentsAsync_NoComments_ReturnsEmpty()
    {
        var handler = new RecordingCommentHandler
        {
            Responder = _ => JsonResponse("""{"comments":[]}""")
        };
        var sut = BuildSut(handler);

        var comments = await sut.GetCommentsAsync(new TicketId("PROJ-42"), CancellationToken.None);

        comments.Should().BeEmpty();
    }

    private static JiraTicketProvider BuildSut(HttpMessageHandler handler) =>
        new(
            new JiraTicketConnection("https://jira.example.com", "user@example.com", "token", "PROJ"),
            new HttpClient(handler),
            new JiraFieldMapper(),
            NullLogger<JiraTicketProvider>.Instance);

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
    };

    private sealed class RecordingCommentHandler : HttpMessageHandler
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
