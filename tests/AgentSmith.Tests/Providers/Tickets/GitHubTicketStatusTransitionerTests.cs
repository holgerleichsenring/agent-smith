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
        handler.Enqueue(IssueResponse("bug"));
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK));

        var sut = Create(handler);
        var result = await sut.TransitionAsync(
            new TicketId("42"),
            TicketLifecycleStatus.Pending,
            TicketLifecycleStatus.Enqueued,
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    // 2026-11-25-7f25b: GitHub answers a conditional header on a write with 400 ("Conditional
    // request headers are not allowed in unsafe requests unless supported by the endpoint"), so
    // an If-Match on the labels PATCH failed every claim. The handler answers exactly that way.
    [Fact]
    public async Task TransitionAsync_GitHubRefusesConditionalHeaders_TheWriteCarriesNoneAndSucceeds()
    {
        var handler = new RefusesConditionalHeadersHandler(IssueResponse("bug"));

        var result = await Create(handler).TransitionAsync(
            new TicketId("42"),
            TicketLifecycleStatus.Pending,
            TicketLifecycleStatus.Enqueued,
            CancellationToken.None);

        result.Outcome.Should().Be(TransitionOutcome.Succeeded);
        handler.Methods.Should().Equal(HttpMethod.Get, HttpMethod.Patch);
        handler.Refused.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadCurrentAsync_GitHubRefusesConditionalHeaders_TheReadCarriesNone()
    {
        var handler = new RefusesConditionalHeadersHandler(IssueResponse("agent-smith:enqueued"));

        var result = await Create(handler).ReadCurrentAsync(new TicketId("42"), CancellationToken.None);

        result.Should().Be(TicketLifecycleStatus.Enqueued);
        handler.Refused.Should().BeEmpty();
    }

    [Fact]
    public async Task TransitionAsync_IssueNotFound_SendsNoConditionalHeader()
    {
        var handler = new RefusesConditionalHeadersHandler(new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await Create(handler).TransitionAsync(
            new TicketId("42"),
            TicketLifecycleStatus.Pending,
            TicketLifecycleStatus.Enqueued,
            CancellationToken.None);

        result.Outcome.Should().Be(TransitionOutcome.NotFound);
        handler.Refused.Should().BeEmpty();
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
        handler.Enqueue(IssueResponse("bug"));

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
        handler.Enqueue(IssueResponseMulti(["agent-smith:init", "agent-smith:pending"]));
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

    private static HttpResponseMessage IssueResponse(string labelName)
        => IssueResponseMulti([labelName]);

    private static HttpResponseMessage IssueResponseMulti(string[] labelNames)
    {
        var labelsJson = string.Join(", ",
            labelNames.Select(n => $"{{ \"name\": \"{n}\" }}"));
        var json = $$"""
        {
            "number": 42,
            "labels": [ {{labelsJson}} ]
        }
        """;
        // GitHub sends an ETag on every issue read; a transitioner that echoes it back on the
        // write is exactly what the refusing handler below catches.
        var resp = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
        resp.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"v1\"");
        return resp;
    }

    /// <summary>Answers the first request with the given response and every later one with 200,
    /// except that any request carrying a conditional header gets GitHub's 400.</summary>
    private sealed class RefusesConditionalHeadersHandler(HttpResponseMessage first) : HttpMessageHandler
    {
        private static readonly string[] Conditional =
            ["If-Match", "If-None-Match", "If-Modified-Since", "If-Unmodified-Since"];

        public List<HttpMethod> Methods { get; } = [];
        public List<string> Refused { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Methods.Add(request.Method);
            var sent = Conditional.Where(request.Headers.Contains).ToList();
            if (sent.Count > 0)
            {
                Refused.Add($"{request.Method} {string.Join(",", sent)}");
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent(
                        "{\"message\":\"Bad Request\",\"errors\":[\"Conditional request headers are not allowed in unsafe requests unless supported by the endpoint\"],\"status\":\"400\"}")
                });
            }
            return Task.FromResult(Methods.Count == 1 ? first : new HttpResponseMessage(HttpStatusCode.OK));
        }
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
