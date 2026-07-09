using System.Net;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Providers.Tickets;

/// <summary>
/// p0317: the ticket conversation reaches the agent — GitLab comment reads hit
/// the notes endpoint (the one UpdateStatusAsync posts to); system notes
/// ("changed label") are bookkeeping, not conversation, and are filtered out.
/// </summary>
public sealed class GitLabTicketProviderGetCommentsTests
{
    [Fact]
    public async Task GetCommentsAsync_MapsAuthorTimestampBody()
    {
        var handler = new RecordingCommentHandler
        {
            Responder = _ => JsonResponse("""
                [
                  {
                    "body": "changed the description",
                    "author": { "name": "Bot", "username": "bot" },
                    "created_at": "2026-07-01T09:00:00Z",
                    "system": true
                  },
                  {
                    "body": "use approach B, not A",
                    "author": { "name": "Jane Operator", "username": "jane" },
                    "created_at": "2026-07-01T10:15:00Z",
                    "system": false
                  }
                ]
                """)
        };
        var sut = BuildSut(handler);

        var comments = await sut.GetCommentsAsync(new TicketId("42"), CancellationToken.None);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.PathAndQuery
            .Should().Be("/api/v4/projects/group%2Fproj/issues/42/notes?per_page=100");
        comments.Should().HaveCount(1, "system notes are tracker bookkeeping, not conversation");
        comments[0].Author.Should().Be("Jane Operator");
        comments[0].CreatedAt.Should().Be(new DateTimeOffset(2026, 7, 1, 10, 15, 0, TimeSpan.Zero));
        comments[0].Body.Should().Be("use approach B, not A");
    }

    private static GitLabTicketProvider BuildSut(HttpMessageHandler handler)
    {
        var connection = new GitLabTicketConnection("https://gitlab.com", "group%2Fproj", "token");
        var httpClient = new HttpClient(handler);
        return new GitLabTicketProvider(
            connection, httpClient,
            new GitLabAttachmentLoader(connection, httpClient, NullLogger.Instance),
            new GitLabFieldMapper(),
            NullLogger<GitLabTicketProvider>.Instance);
    }

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
