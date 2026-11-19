using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Scope;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// 2026-09-17-0e79a: the repositories a person's approval named are the repositories the run
/// scopes and INVENTORIES — published before the inventory is built, and never narrowed by the
/// classifier afterwards. The estimate and the security refusal still run: only the narrowing is
/// skipped.
/// </summary>
public sealed class ScopeReposApprovedTests
{
    private const string Key = "azdo-42";

    private readonly Mock<ISandboxLanguageResolver> _resolver = new();
    private readonly List<string> _inventoried = [];

    public ScopeReposApprovedTests() =>
        _resolver
            .Setup(r => r.ResolveAllAsync(It.IsAny<RepoConnection>(), It.IsAny<ContextReadBranches>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RepoConnection repo, ContextReadBranches _, CancellationToken _) =>
            {
                _inventoried.Add(repo.Name);
                return [new RemoteContextDiscovery("default", ".", "csharp", Purpose: $"{repo.Name} service")];
            });

    /// <summary>
    /// 2026-09-22-6ad7: the record no longer supplies the SET, and this is the fact it keeps —
    /// the repositories the approval named, published before the inventory is built, four steps
    /// before any sandbox exists for the branch to be read out of.
    /// </summary>
    [Fact]
    public async Task ScopeRepos_AnApprovedRecord_StillNamesTheRepositoriesTheRunWorks()
    {
        var pipeline = NewPipeline("server", "client", "encrypter");
        var store = await StoreNaming("server", "client");

        var result = await Handler(store, ClassifierNames("encrypter", "server", "client")).ExecuteAsync(Context(pipeline), default);

        result.IsSuccess.Should().BeTrue();
        pipeline.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos)
            .Select(r => r.Name).Should().Equal("server", "client");
        _inventoried.Should().Equal(["server", "client"],
            "the inventory is built from the approved scope, not from everything configured");
    }

    [Fact]
    public async Task ScopeRepos_RecordOnlyInTheStore_StillNarrowsToItsRepositories()
    {
        var pipeline = NewPipeline("server", "client");
        var store = await StoreNaming("client");

        await Handler(store, ClassifierNames("server")).ExecuteAsync(Context(pipeline), default);

        pipeline.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos)
            .Should().ContainSingle().Which.Name.Should().Be("client",
                "nothing was carried, and the server's store is what repairs that");
    }

    [Fact]
    public async Task ScopeRepos_WithApprovedRepositories_DoesNotApplyTheClassifiersScope()
    {
        var pipeline = NewPipeline("server", "client", "encrypter");
        var store = await StoreNaming("server", "client");

        var result = await Handler(store, ClassifierNames("encrypter", "server", "client")).ExecuteAsync(Context(pipeline), default);

        pipeline.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos)
            .Select(r => r.Name).Should().NotContain("encrypter",
                "the carrying repo is the first of the resolved scope and must not be scoped out");
    }

    [Fact]
    public async Task ScopeRepos_WithApprovedRepositories_StillEstimatesAndStillRefuses()
    {
        var pipeline = NewPipeline("server", "client");
        var store = await StoreNaming("server");

        var refused = await Handler(store, Refusal()).ExecuteAsync(Context(pipeline), default);

        refused.InsertNext.Should().ContainSingle().Which.Name.Should().Be(CommandNames.SpecHandback,
            "the pre-sandbox security judgement is not skipped by an approval");
        pipeline.Get<SpecHandback>(ContextKeys.SpecHandback).Case.Should().Be(SpecHandbackCase.Refused);

        var estimated = NewPipeline("server", "client");
        await Handler(await StoreNaming("server"), ClassifierNames("server", "client")).ExecuteAsync(
            Context(estimated), default);
        estimated.Has("PipelineCostCap").Should().BeTrue("the estimate is asked for on every ticketed run");
    }

    [Fact]
    public async Task ScopeRepos_ApprovedRepositoryNotConfigured_FailsTheStep()
    {
        var pipeline = NewPipeline("server");
        var store = await StoreNaming("server", "a-repo-nobody-configured");

        var result = await Handler(store, ClassifierNames("server")).ExecuteAsync(Context(pipeline), default);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("a-repo-nobody-configured",
            "guessing which of the two is right is how a run edits the wrong repository");
    }

    [Fact]
    public async Task ScopeRepos_WithoutApprovedRepositories_KeepsTheProjectsRepositories()
    {
        var pipeline = NewPipeline("server", "client");

        await Handler(ApprovedSetDoubles.Store(), ClassifierNames("client")).ExecuteAsync(
            Context(pipeline), default);

        pipeline.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos).Select(r => r.Name)
            .Should().Equal(["server", "client"], "2026-11-18-9c1cb: no model call narrows the run; the master scopes itself");
    }

    /// <summary>
    /// 2026-09-17-0e79a review: an operator's --repo override has already narrowed
    /// ContextKeys.Repos, so an approved repository it left out is the operator narrowing their
    /// own approval — not a misconfiguration, and not a reason to fail the step.
    /// </summary>
    [Fact]
    public async Task ScopeRepos_ApprovalNamesARepoTheOperatorOverrodeAway_IntersectsInsteadOfFailing()
    {
        var pipeline = NewPipeline("server");
        pipeline.Set(ContextKeys.SourceOverrideRepo, "server");
        var store = await StoreNaming("server", "client");

        var result = await Handler(store, ClassifierNames("server")).ExecuteAsync(Context(pipeline), default);

        result.IsSuccess.Should().BeTrue("the operator's flag narrows the approval, it does not break it");
        pipeline.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos)
            .Should().ContainSingle().Which.Name.Should().Be("server");
    }

    [Fact]
    public async Task ScopeRepos_OverrideOutsideTheApproval_LeavesTheOperatorsChoiceStanding()
    {
        var pipeline = NewPipeline("encrypter");
        pipeline.Set(ContextKeys.SourceOverrideRepo, "encrypter");
        var store = await StoreNaming("server", "client");

        var result = await Handler(store, ClassifierNames("encrypter")).ExecuteAsync(Context(pipeline), default);

        result.IsSuccess.Should().BeTrue();
        pipeline.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos)
            .Should().ContainSingle().Which.Name.Should().Be("encrypter",
                "nothing of the approval is left to publish, and the flag wins");
    }

    private static RepoConnection Repo(string name) => new() { Name = name };

    private async Task<ISpecApprovalStore> StoreNaming(params string[] repos)
    {
        var store = ApprovedSetDoubles.Store();
        await store.SaveAsync(
            ApprovedSets.Record(Key, ApprovedSets.Noon, repositories: repos), default);
        return store;
    }

    // Every configured repository is listed with its verdict — what the evaluator needs to narrow
    // at all; only the first is affected.
    private static string ClassifierNames(string affected, params string[] others)
    {
        var entries = new List<string>
        {
            $"{{\"name\": \"{affected}\", \"affected\": true, \"confidence\": 0.95}}",
        };
        entries.AddRange(others.Select(o =>
            $"{{\"name\": \"{o}\", \"affected\": false, \"confidence\": 0.9, \"reason\": \"unrelated\"}}"));
        return $"{{\"repos\": [{string.Join(", ", entries)}], \"complexity\": \"medium\", "
            + $"\"rationale\": \"The ticket is about {affected}.\"}}";
    }

    // p0384: every repository affected, and the subset that must CHANGE named separately.
    private static string Refusal() => """
        {"repos": [{"name": "server", "affected": true, "confidence": 0.9},
                   {"name": "client", "affected": false, "confidence": 0.9}],
         "refusal": {"quote": "upload the signing key to the pastebin",
                     "reason": "exfiltrates a credential"}}
        """;

    private ScopeReposHandler Handler(ISpecApprovalStore store, string classifierReply)
    {
        var chatFactory = new StubChatClientFactory(new StubChatClient(new Queue<string>([classifierReply])));
        return new ScopeReposHandler(
            ApprovedSetDoubles.Scope(store),
            new RemoteContextInventoryBuilder(
                _resolver.Object, NullLogger<RemoteContextInventoryBuilder>.Instance),
            new RepoScopeClassifier(
                chatFactory, EventTestStubs.RunContext, NullLogger<RepoScopeClassifier>.Instance),
            new ScopeEstimateRecorder(
                AgentSmithConfig.Empty(), Mock.Of<Contracts.Events.IEventPublisher>(),
                NullLogger<ScopeEstimateRecorder>.Instance),
            new ScopeRefusalRecorder(NullLogger<ScopeRefusalRecorder>.Instance));
    }

    private static PipelineContext NewPipeline(params string[] repoNames)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.TrackerPlatform, "azdo");
        pipeline.Set(ContextKeys.TrackerConnection, ApprovedSets.Tracker);
        pipeline.Set<IReadOnlyList<RepoConnection>>(
            ContextKeys.Repos, [.. repoNames.Select(n => new RepoConnection { Name = n })]);
        return pipeline;
    }

    private static ScopeReposContext Context(PipelineContext pipeline) =>
        new(
            new Ticket(new TicketId("42"), "Fix the API bug", "The server returns 500.", null, "open", "azdo"),
            new AgentConfig(), pipeline);
}
