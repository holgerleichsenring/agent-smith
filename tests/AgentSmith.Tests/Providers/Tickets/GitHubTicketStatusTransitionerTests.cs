using System.Net;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;

namespace AgentSmith.Tests.Providers.Tickets;

public sealed class GitHubTicketStatusTransitionerTests
{
    [Fact]
    public async Task TransitionAsync_HappyPath_ReturnsSucceeded()
    {
        var handler = new SequentialHandler();
        handler.Enqueue(IssueResponse("bug", etag: "\"v1\""));
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK));

        var sut = Create(handler);
        var result = await sut.TransitionAsync(
            new TicketId("42"),
            TicketLifecycleStatus.Pending,
            TicketLifecycleStatus.Enqueued,
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task TransitionAsync_IfMatchConflict_ReturnsPreconditionFailed()
    {
        var handler = new SequentialHandler();
        handler.Enqueue(IssueResponse("bug", etag: "\"v1\""));
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.PreconditionFailed));

        var sut = Create(handler);
        var result = await sut.TransitionAsync(
            new TicketId("42"),
            TicketLifecycleStatus.Pending,
            TicketLifecycleStatus.Enqueued,
            CancellationToken.None);

        result.Outcome.Should().Be(TransitionOutcome.PreconditionFailed);
    }

    [Fact]
    public async Task TransitionAsync_IssueNotFound_ReturnsNotFound()
    {
        var handler = new SequentialHandler();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.NotFound));

        var sut = Create(handler);
        var result = await sut.TransitionAsync(
            new TicketId("42"),
            TicketLifecycleStatus.Pending,
            TicketLifecycleStatus.Enqueued,
            CancellationToken.None);

        result.Outcome.Should().Be(TransitionOutcome.NotFound);
    }

    [Fact]
    public async Task TransitionAsync_CurrentStatusDoesNotMatchFrom_ReturnsPreconditionFailed()
    {
        // Issue has agent-smith:in-progress, but caller expects Pending
        var handler = new SequentialHandler();
        handler.Enqueue(IssueResponse("agent-smith:in-progress", etag: "\"v1\""));

        var sut = Create(handler);
        var result = await sut.TransitionAsync(
            new TicketId("42"),
            TicketLifecycleStatus.Pending,
            TicketLifecycleStatus.Enqueued,
            CancellationToken.None);

        result.Outcome.Should().Be(TransitionOutcome.PreconditionFailed);
    }

    [Fact]
    public async Task ReadCurrentAsync_NoLifecycleLabel_ReturnsNull()
    {
        var handler = new SequentialHandler();
        handler.Enqueue(IssueResponse("bug", etag: "\"v1\""));

        var sut = Create(handler);
        var result = await sut.ReadCurrentAsync(new TicketId("42"), CancellationToken.None);

        result.Should().BeNull();
    }

    private static GitHubTicketStatusTransitioner Create(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler);
        return new GitHubTicketStatusTransitioner(
            "https://github.com/org/repo", "token",
            client, NullLogger<GitHubTicketStatusTransitioner>.Instance);
    }

    private static HttpResponseMessage IssueResponse(string labelName, string etag)
    {
        var json = $$"""
        {
            "number": 42,
            "labels": [
                { "name": "{{labelName}}" }
            ]
        }
        """;
        var resp = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
        resp.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue(etag);
        return resp;
    }

    private sealed class SequentialHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();

        public void Enqueue(HttpResponseMessage response) => _responses.Enqueue(response);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_responses.Dequeue());
    }
}
