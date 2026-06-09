using System.Net;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
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

    // p0262: the lifecycle-from precondition was removed — tags are pure markers set
    // unconditionally. The cross-provider matrix proves the unconditional write for all
    // four platforms (Transitioner_CurrentStatusMismatch_StillWrites_Unconditional).

    [Fact]
    public async Task ReadCurrentAsync_NoLifecycleLabel_ReturnsNull()
    {
        var handler = new SequentialHandler();
        handler.Enqueue(IssueResponse("bug", etag: "\"v1\""));

        var sut = Create(handler);
        var result = await sut.ReadCurrentAsync(new TicketId("42"), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task TransitionAsync_OperatorAgentSmithPrefixedLabel_IsPreservedThroughTransition()
    {
        // p0133 follow-up: operator-defined labels that share the agent-smith:
        // prefix (e.g. agent-smith:init triggering init-project) must survive a
        // status transition. Before tightening, BuildLabels stripped them via
        // a prefix-match and replaced the trigger label with the new status —
        // which is why init-project runs were silently re-routed to fix-bug
        // (default_pipeline) on the next poll cycle.
        var handler = new SequentialHandler();
        handler.Enqueue(IssueResponseMulti(["agent-smith:init", "agent-smith:pending"], etag: "\"v1\""));
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK));

        var sut = Create(handler);
        var result = await sut.TransitionAsync(
            new TicketId("42"),
            TicketLifecycleStatus.Pending,
            TicketLifecycleStatus.Enqueued,
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var patchBody = handler.LastPatchBody;
        patchBody.Should().Contain("agent-smith:init",
            because: "operator trigger labels must survive transition");
        patchBody.Should().Contain("agent-smith:enqueued",
            because: "new lifecycle status must be added");
        patchBody.Should().NotContain("agent-smith:pending",
            because: "the previous lifecycle status must be stripped");
    }

    private static GitHubTicketStatusTransitioner Create(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler);
        return new GitHubTicketStatusTransitioner(
            new GitHubTicketConnection("https://github.com/org/repo", "token"),
            client, NullLogger<GitHubTicketStatusTransitioner>.Instance);
    }

    private static HttpResponseMessage IssueResponse(string labelName, string etag)
        => IssueResponseMulti([labelName], etag);

    private static HttpResponseMessage IssueResponseMulti(string[] labelNames, string etag)
    {
        var labelsJson = string.Join(", ",
            labelNames.Select(n => $"{{ \"name\": \"{n}\" }}"));
        var json = $$"""
        {
            "number": 42,
            "labels": [ {{labelsJson}} ]
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
        public string? LastPatchBody { get; private set; }

        public void Enqueue(HttpResponseMessage response) => _responses.Enqueue(response);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Patch && request.Content is not null)
                LastPatchBody = await request.Content.ReadAsStringAsync(cancellationToken);
            return _responses.Dequeue();
        }
    }
}
