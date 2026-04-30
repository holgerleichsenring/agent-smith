using System.Net;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Providers.Tickets;

public sealed class PlatformTransitionerTests
{
    [Fact]
    public async Task GitLab_HappyPath_ReturnsSucceeded()
    {
        var handler = new SequentialHandler();
        handler.Enqueue(JsonResponse("{\"labels\":[\"bug\"]}"));
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK));

        var sut = new GitLabTicketStatusTransitioner(
            "https://gitlab.com", "my-proj", "token",
            new HttpClient(handler),
            NullLogger<GitLabTicketStatusTransitioner>.Instance);

        var result = await sut.TransitionAsync(new TicketId("42"),
            TicketLifecycleStatus.Pending, TicketLifecycleStatus.Enqueued, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GitLab_CurrentLabelMismatch_ReturnsPreconditionFailed()
    {
        var handler = new SequentialHandler();
        handler.Enqueue(JsonResponse("{\"labels\":[\"agent-smith:in-progress\"]}"));

        var sut = new GitLabTicketStatusTransitioner(
            "https://gitlab.com", "my-proj", "token",
            new HttpClient(handler),
            NullLogger<GitLabTicketStatusTransitioner>.Instance);

        var result = await sut.TransitionAsync(new TicketId("42"),
            TicketLifecycleStatus.Pending, TicketLifecycleStatus.Enqueued, CancellationToken.None);

        result.Outcome.Should().Be(TransitionOutcome.PreconditionFailed);
    }

    [Fact]
    public async Task AzureDevOps_RevMismatch_ReturnsPreconditionFailed()
    {
        var handler = new SequentialHandler();
        handler.Enqueue(JsonResponse("{\"fields\":{\"System.Tags\":\"\",\"System.Rev\":5}}"));
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.PreconditionFailed));

        var sut = new AzureDevOpsTicketStatusTransitioner(
            "https://dev.azure.com/org", "proj", "pat",
            new HttpClient(handler),
            NullLogger<AzureDevOpsTicketStatusTransitioner>.Instance);

        var result = await sut.TransitionAsync(new TicketId("42"),
            TicketLifecycleStatus.Pending, TicketLifecycleStatus.Enqueued, CancellationToken.None);

        result.Outcome.Should().Be(TransitionOutcome.PreconditionFailed);
    }

    [Fact]
    public async Task AzureDevOps_HappyPath_ReturnsSucceeded()
    {
        var handler = new SequentialHandler();
        handler.Enqueue(JsonResponse("{\"fields\":{\"System.Tags\":\"\",\"System.Rev\":5}}"));
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK));

        var sut = new AzureDevOpsTicketStatusTransitioner(
            "https://dev.azure.com/org", "proj", "pat",
            new HttpClient(handler),
            NullLogger<AzureDevOpsTicketStatusTransitioner>.Instance);

        var result = await sut.TransitionAsync(new TicketId("42"),
            TicketLifecycleStatus.Pending, TicketLifecycleStatus.Enqueued, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task AzureDevOps_PatchUsesOpReplace_NotOpAdd_SoTagsAreReplacedNotMerged()
    {
        // Regression: AzDO treats op:add on /fields/System.Tags as merge-into-existing-list,
        // which left the previous lifecycle tag (e.g. agent-smith:enqueued) on the ticket
        // alongside the new one (agent-smith:in-progress). op:replace is the deterministic fix.
        var handler = new RecordingSequentialHandler();
        handler.Enqueue(JsonResponse("{\"fields\":{\"System.Tags\":\"agent-smith:enqueued; bug\",\"System.Rev\":5}}"));
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK));

        var sut = new AzureDevOpsTicketStatusTransitioner(
            "https://dev.azure.com/org", "proj", "pat",
            new HttpClient(handler),
            NullLogger<AzureDevOpsTicketStatusTransitioner>.Instance);

        var result = await sut.TransitionAsync(new TicketId("42"),
            TicketLifecycleStatus.Enqueued, TicketLifecycleStatus.InProgress, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var patchBody = handler.SentBodies.LastOrDefault();
        patchBody.Should().NotBeNull();
        patchBody.Should().Contain("\"op\":\"replace\"", "tags must be REPLACED, not merged");
        patchBody.Should().NotContain("\"op\":\"add\"", "op:add on System.Tags merges in AzDO");
        patchBody.Should().Contain("agent-smith:in-progress");
        patchBody.Should().NotContain("agent-smith:enqueued",
            "the previous lifecycle tag must be filtered out before sending");
        patchBody.Should().Contain("bug", "non-lifecycle tags must be preserved");
    }

    [Fact]
    public async Task Jira_LabelMode_AcquiresLabelLockAndPutsLabels()
    {
        var handler = new SequentialHandler();
        handler.Enqueue(JsonResponse("{\"fields\":{\"labels\":[]}}"));
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.NoContent));

        var claimLock = new Mock<IRedisClaimLock>();
        claimLock.Setup(l => l.TryAcquireAsync(
            It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("tok");
        claimLock.Setup(l => l.ReleaseAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var catalog = new JiraWorkflowCatalog(NullLogger<JiraWorkflowCatalog>.Instance);
        var sut = new JiraTicketStatusTransitioner(
            "https://jira.com", "x@y", "tok", "PROJ",
            catalog, claimLock.Object,
            new HttpClient(handler),
            NullLogger<JiraTicketStatusTransitioner>.Instance);

        var result = await sut.TransitionAsync(new TicketId("PROJ-1"),
            TicketLifecycleStatus.Pending, TicketLifecycleStatus.Enqueued, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        claimLock.Verify(l => l.TryAcquireAsync(
            It.Is<string>(k => k.StartsWith("agentsmith:jira-label-lock:")),
            It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
        claimLock.Verify(l => l.ReleaseAsync(
            It.IsAny<string>(), "tok", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Jira_LabelLockHeld_ReturnsPreconditionFailed()
    {
        var claimLock = new Mock<IRedisClaimLock>();
        claimLock.Setup(l => l.TryAcquireAsync(
            It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var catalog = new JiraWorkflowCatalog(NullLogger<JiraWorkflowCatalog>.Instance);
        var sut = new JiraTicketStatusTransitioner(
            "https://jira.com", "x@y", "tok", "PROJ",
            catalog, claimLock.Object,
            new HttpClient(new SequentialHandler()),
            NullLogger<JiraTicketStatusTransitioner>.Instance);

        var result = await sut.TransitionAsync(new TicketId("PROJ-1"),
            TicketLifecycleStatus.Pending, TicketLifecycleStatus.Enqueued, CancellationToken.None);

        result.Outcome.Should().Be(TransitionOutcome.PreconditionFailed);
    }

    [Fact]
    public void JiraWorkflowCatalog_DefaultsToLabelMode()
    {
        var catalog = new JiraWorkflowCatalog(NullLogger<JiraWorkflowCatalog>.Instance);
        catalog.GetModeForProject("PROJ").Should().Be(JiraLifecycleMode.Label);
    }

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
    };

    private sealed class SequentialHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();
        public void Enqueue(HttpResponseMessage response) => _responses.Enqueue(response);
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_responses.Count > 0
                ? _responses.Dequeue()
                : new HttpResponseMessage(HttpStatusCode.InternalServerError));
    }

    private sealed class RecordingSequentialHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();
        public List<string> SentBodies { get; } = new();
        public void Enqueue(HttpResponseMessage response) => _responses.Enqueue(response);
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            SentBodies.Add(body);
            return _responses.Count > 0
                ? _responses.Dequeue()
                : new HttpResponseMessage(HttpStatusCode.InternalServerError);
        }
    }
}
