using System.Net;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Providers.Tickets;

/// <summary>
/// p0239b: ONE parametrized lifecycle suite over all four ticket platforms
/// (GitHub / GitLab / AzureDevOps / Jira). Proves the claim/lifecycle state
/// machine (Pending → Enqueued → InProgress → Done) and the precondition guard
/// behave UNIFORMLY across providers, despite each speaking a different HTTP
/// shape (GitHub ETag labels, GitLab label arrays, AzDO System.Tags + rev,
/// Jira label add/remove). Per-platform HTTP-quirk regressions (AzDO op:replace,
/// ETag/rev mismatch) stay in PlatformTransitionerTests; this suite is the
/// cross-provider flow contract — it must not be duplicated per platform.
/// </summary>
public sealed class CrossProviderLifecycleMatrixTests
{
    public static IEnumerable<object[]> Platforms() =>
        new[] { "GitHub", "GitLab", "AzureDevOps", "Jira" }.Select(p => new object[] { p });

    [Theory]
    [MemberData(nameof(Platforms))]
    public async Task Transitioner_FullLifecycle_EachStepSucceeds(string platform)
    {
        var f = Fixture.For(platform);
        var handler = new ScriptedHandler();
        var steps = new[]
        {
            (From: (TicketLifecycleStatus?)null, To: TicketLifecycleStatus.Enqueued),
            (From: TicketLifecycleStatus.Enqueued, To: TicketLifecycleStatus.InProgress),
            (From: TicketLifecycleStatus.InProgress, To: TicketLifecycleStatus.Done),
        };
        foreach (var (from, to) in steps)
        {
            handler.Enqueue(f.Read(from));
            foreach (var w in f.WriteOk(to)) handler.Enqueue(w);
        }

        var sut = f.Build(handler);
        foreach (var (from, to) in steps)
        {
            var result = await sut.TransitionAsync(
                f.Ticket, from ?? TicketLifecycleStatus.Pending, to, CancellationToken.None);
            result.IsSuccess.Should().BeTrue(
                $"{platform}: {from?.ToString() ?? "Pending"} → {to} must succeed ({result.Error})");
        }
    }

    [Theory]
    [MemberData(nameof(Platforms))]
    public async Task Transitioner_CurrentStatusMismatch_StillWrites_Unconditional(string platform)
    {
        // p0262: lifecycle tags are pure markers — the transition sets `to`
        // UNCONDITIONALLY, no `from` precondition. The ticket already carries
        // in-progress, yet the Pending → Enqueued claim write still LANDS for every
        // provider (the old precondition refusal is gone; concurrency is the lease's job).
        var f = Fixture.For(platform);
        var handler = new ScriptedHandler();
        handler.Enqueue(f.Read(TicketLifecycleStatus.InProgress));
        foreach (var w in f.WriteOk(TicketLifecycleStatus.Enqueued)) handler.Enqueue(w);

        var sut = f.Build(handler);
        var result = await sut.TransitionAsync(
            f.Ticket, TicketLifecycleStatus.Pending, TicketLifecycleStatus.Enqueued, CancellationToken.None);

        result.Outcome.Should().Be(TransitionOutcome.Succeeded,
            $"{platform} sets the marker unconditionally — no from-precondition");
    }

    [Theory]
    [MemberData(nameof(Platforms))]
    public async Task ReadCurrent_NoLifecycleLabel_ReturnsNull(string platform)
    {
        // A ticket with no lifecycle label reads as null (treated as Pending by
        // the claim service) — uniform across providers.
        var f = Fixture.For(platform);
        var handler = new ScriptedHandler();
        handler.Enqueue(f.Read(current: null));

        var sut = f.Build(handler);
        var current = await sut.ReadCurrentAsync(f.Ticket, CancellationToken.None);

        current.Should().BeNull($"{platform}: an unlabelled ticket has no lifecycle status");
    }

    // ── Per-platform HTTP fixtures ───────────────────────────────────────────
    private abstract class Fixture
    {
        public abstract TicketId Ticket { get; }
        public abstract ITicketStatusTransitioner Build(HttpMessageHandler handler);
        public abstract HttpResponseMessage Read(TicketLifecycleStatus? current);
        public abstract IEnumerable<HttpResponseMessage> WriteOk(TicketLifecycleStatus to);

        public static Fixture For(string platform) => platform switch
        {
            "GitHub" => new GitHubFixture(),
            "GitLab" => new GitLabFixture(),
            "AzureDevOps" => new AzureDevOpsFixture(),
            "Jira" => new JiraFixture(),
            _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, null),
        };

        protected static string? Label(TicketLifecycleStatus? s) =>
            s is null ? null : LifecycleLabels.For(s.Value);

        protected static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        };

        protected static HttpResponseMessage Status(HttpStatusCode code) => new(code);
    }

    private sealed class GitHubFixture : Fixture
    {
        public override TicketId Ticket => new("42");
        public override ITicketStatusTransitioner Build(HttpMessageHandler h) =>
            new GitHubTicketStatusTransitioner(
                new GitHubTicketConnection("https://github.com/org/repo", "token"),
                new HttpClient(h), NullLogger<GitHubTicketStatusTransitioner>.Instance);

        public override HttpResponseMessage Read(TicketLifecycleStatus? current)
        {
            var labels = Label(current) is { } l ? $"[{{\"name\":\"{l}\"}}]" : "[]";
            return Json($"{{\"labels\":{labels}}}");
        }

        public override IEnumerable<HttpResponseMessage> WriteOk(TicketLifecycleStatus to) =>
            new[] { Status(HttpStatusCode.OK) };
    }

    private sealed class GitLabFixture : Fixture
    {
        public override TicketId Ticket => new("42");
        public override ITicketStatusTransitioner Build(HttpMessageHandler h) =>
            new GitLabTicketStatusTransitioner(
                new GitLabTicketConnection("https://gitlab.com", "my-proj", "token"),
                new HttpClient(h), NullLogger<GitLabTicketStatusTransitioner>.Instance);

        public override HttpResponseMessage Read(TicketLifecycleStatus? current)
        {
            var labels = Label(current) is { } l ? $"[\"{l}\"]" : "[]";
            return Json($"{{\"labels\":{labels}}}");
        }

        public override IEnumerable<HttpResponseMessage> WriteOk(TicketLifecycleStatus to) =>
            new[] { Status(HttpStatusCode.OK) };
    }

    private sealed class AzureDevOpsFixture : Fixture
    {
        public override TicketId Ticket => new("42");
        public override ITicketStatusTransitioner Build(HttpMessageHandler h) =>
            new AzureDevOpsTicketStatusTransitioner(
                new AzureDevOpsTicketConnection("https://dev.azure.com/org", "proj", "pat"),
                new HttpClient(h), NullLogger<AzureDevOpsTicketStatusTransitioner>.Instance);

        public override HttpResponseMessage Read(TicketLifecycleStatus? current) =>
            Json($"{{\"fields\":{{\"System.Tags\":\"{Label(current) ?? ""}\",\"System.Rev\":5}}}}");

        public override IEnumerable<HttpResponseMessage> WriteOk(TicketLifecycleStatus to) =>
            new[]
            {
                Status(HttpStatusCode.OK),
                // p0133 follow-up: AzDO does a read-back diagnostic GET after PATCH.
                Json($"{{\"fields\":{{\"System.Tags\":\"{LifecycleLabels.For(to)}\",\"System.Rev\":6}}}}"),
            };
    }

    private sealed class JiraFixture : Fixture
    {
        public override TicketId Ticket => new("PROJ-1");
        public override ITicketStatusTransitioner Build(HttpMessageHandler h) =>
            new JiraTicketStatusTransitioner(
                new JiraTicketConnection("https://jira.com", "x@y", "tok", "PROJ"),
                new JiraWorkflowCatalog(NullLogger<JiraWorkflowCatalog>.Instance),
                new HttpClient(h), NullLogger<JiraTicketStatusTransitioner>.Instance);

        public override HttpResponseMessage Read(TicketLifecycleStatus? current)
        {
            var labels = Label(current) is { } l ? $"[\"{l}\"]" : "[]";
            return Json($"{{\"fields\":{{\"labels\":{labels}}}}}");
        }

        public override IEnumerable<HttpResponseMessage> WriteOk(TicketLifecycleStatus to) =>
            new[] { Status(HttpStatusCode.NoContent) };
    }

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();
        public void Enqueue(HttpResponseMessage response) => _responses.Enqueue(response);
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_responses.Count > 0
                ? _responses.Dequeue()
                : new HttpResponseMessage(HttpStatusCode.InternalServerError));
    }
}
