using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Scope;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// p0331: the ScopeRepos step — ticket→repo classification narrows
/// ContextKeys.Repos (the one seam checkout/sandboxes/commit re-read) BEFORE any
/// sandbox exists; every doubtful outcome keeps all repos; the decision is
/// always recorded as a run artifact.
/// </summary>
public sealed class ScopeReposHandlerTests
{
    private readonly Mock<ISandboxLanguageResolver> _resolverMock = new();

    public ScopeReposHandlerTests()
    {
        _resolverMock
            .Setup(r => r.ResolveAllAsync(It.IsAny<RepoConnection>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RepoConnection repo, CancellationToken _) =>
                new[] { new RemoteContextDiscovery("default", ".", "csharp", Purpose: $"{repo.Name} service") });
    }

    [Fact]
    public async Task ScopeRepos_TicketNamesOneRepo_NarrowsCheckoutAndSandboxes()
    {
        // A confident classification narrows ContextKeys.Repos — the single seam
        // CheckoutSource and PipelineSandboxCoordinator (and CommitAndPR) all
        // re-read, so this IS the checkout+sandbox narrowing.
        var pipeline = NewPipeline("server", "client", "encrypter");
        var handler = Handler(
            """{"repos": ["server"], "confidence": 0.95, "rationale": "The ticket describes a server-side API bug."}""");

        var result = await handler.ExecuteAsync(Context(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var repos = pipeline.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos);
        repos.Should().ContainSingle().Which.Name.Should().Be("server");
        // The pre-checkout inventory is cached for the coordinator (all repos,
        // pre-narrowing — a later escalation to 'client' must hit the cache too).
        pipeline.TryGet<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(
                ContextKeys.RemoteContextInventory, out var inventory)
            .Should().BeTrue();
        inventory!.Keys.Should().BeEquivalentTo("server", "client", "encrypter");
    }

    [Fact]
    public async Task ScopeRepos_LowConfidence_FallsBackToAllRepos()
    {
        var pipeline = NewPipeline("server", "client");
        var handler = Handler(
            """{"repos": ["server"], "confidence": 0.4, "rationale": "Might be server-only."}""");

        var result = await handler.ExecuteAsync(Context(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos)
            .Should().HaveCount(2, "low confidence must keep today's all-repos behavior");
        pipeline.Get<string>(ContextKeys.RepoScopeRationale)
            .Should().Contain("fallback").And.Contain("confidence");
    }

    [Theory]
    [InlineData("not json at all", "no parseable")]
    [InlineData("""{"repos": ["ghost-repo"], "confidence": 0.99}""", "unknown repo")]
    [InlineData("""{"repos": [], "confidence": 0.99}""", "empty repo list")]
    public async Task ScopeRepos_UnusableClassification_FallsBackToAllRepos(
        string reply, string expectedReason)
    {
        var pipeline = NewPipeline("server", "client");
        var handler = Handler(reply);

        await handler.ExecuteAsync(Context(pipeline), CancellationToken.None);

        pipeline.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos).Should().HaveCount(2);
        pipeline.Get<string>(ContextKeys.RepoScopeRationale).Should().Contain(expectedReason);
    }

    [Fact]
    public async Task ScopeRepos_RationaleRecordedAsRunArtifact()
    {
        var pipeline = NewPipeline("server", "client");
        var handler = Handler(
            """{"repos": ["server"], "confidence": 0.9, "rationale": "Only the server exposes this endpoint."}""");

        await handler.ExecuteAsync(Context(pipeline), CancellationToken.None);

        // Named context key for programmatic consumers…
        var rationale = pipeline.Get<string>(ContextKeys.RepoScopeRationale);
        rationale.Should().Contain("server").And.Contain("Only the server exposes this endpoint.");
        // …and a decision entry so result.md + the dashboard render the scope call.
        pipeline.TryGet<List<PlanDecision>>(ContextKeys.Decisions, out var decisions).Should().BeTrue();
        decisions!.Should().ContainSingle(d => d.Category == "scope" && d.Decision == rationale);
    }

    [Fact]
    public async Task ScopeRepos_SingleRepo_SkipsClassification_StillBuildsInventory()
    {
        // A CLI --repo override already narrowed Repos to one entry — the operator's
        // authority; the classifier must never run (and never override it).
        var pipeline = NewPipeline("server");
        var handler = Handler("""{"repos": [], "confidence": 1.0}""");

        var result = await handler.ExecuteAsync(Context(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("skipped");
        pipeline.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos).Should().HaveCount(1);
        pipeline.Has(ContextKeys.RemoteContextInventory).Should().BeTrue();
    }

    private ScopeReposHandler Handler(string classifierReply)
    {
        var chatFactory = new StubChatClientFactory(
            new StubChatClient(new Queue<string>([classifierReply])));
        var classifier = new RepoScopeClassifier(
            chatFactory, EventTestStubs.RunContext, NullLogger<RepoScopeClassifier>.Instance);
        return new ScopeReposHandler(
            _resolverMock.Object, classifier, NullLogger<ScopeReposHandler>.Instance);
    }

    private static PipelineContext NewPipeline(params string[] repoNames)
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos,
            repoNames.Select(n => new RepoConnection { Name = n }).ToList());
        return pipeline;
    }

    private static ScopeReposContext Context(PipelineContext pipeline) =>
        new(
            new Ticket(new TicketId("42"), "Fix the API bug", "The server returns 500.", null, "open", "test"),
            new AgentConfig(), pipeline);
}
