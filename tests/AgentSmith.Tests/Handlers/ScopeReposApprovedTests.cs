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
            .Setup(r => r.ResolveAllAsync(It.IsAny<RepoConnection>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RepoConnection repo, CancellationToken _) =>
            {
                _inventoried.Add(repo.Name);
                return [new RemoteContextDiscovery("default", ".", "csharp", Purpose: $"{repo.Name} service")];
            });

    [Fact]
    public async Task ScopeRepos_WithApprovedRepositories_PublishesThemBeforeTheInventoryIsBuilt()
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

        result.Message.Should().Contain("approved specification named the repositories");
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
    public async Task ScopeRepos_WithoutApprovedRepositories_ClassifiesAsBefore()
    {
        var pipeline = NewPipeline("server", "client");

        await Handler(ApprovedSetDoubles.Store(), ClassifierNames("client", "server")).ExecuteAsync(
            Context(pipeline), default);

        pipeline.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos)
            .Should().ContainSingle().Which.Name.Should().Be("client",
                "a ticket nobody approved behaves exactly as it does today");
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

    /// <summary>
    /// 2026-09-17-0e79a review: an approved scope skips only the NARROWING. The keystone's
    /// per-repo delivery gate (p0384) and the context scope (p0336b) still run — losing them was
    /// a silent regression of the two things that make a multi-repo run verifiable.
    /// </summary>
    [Fact]
    public async Task ScopeRepos_WithApprovedRepositories_StillPublishesTheExpectedChangeRepos()
    {
        var pipeline = NewPipeline("server", "client", "encrypter");
        var store = await StoreNaming("server", "client");

        await Handler(store, MustChange("server", "client")).ExecuteAsync(Context(pipeline), default);

        pipeline.TryGet<IReadOnlyList<string>>(ContextKeys.ExpectedChangeRepos, out var expected)
            .Should().BeTrue("the keystone gates delivery per repository on this key");
        expected.Should().Contain("server");
        pipeline.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos)
            .Select(r => r.Name).Should().Equal("server", "client");
    }

    [Fact]
    public void ApprovedRepoScope_NarrowOverAnApprovedScope_ChangesNothing()
    {
        var pipeline = NewPipeline("server", "client");
        var approved = new ApprovedRepoScope.Scope([new RepoConnection { Name = "server" }], FromApproval: true);

        ApprovedSetDoubles.Scope().Narrow(pipeline, approved, [new RepoConnection { Name = "client" }]);

        pipeline.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos)
            .Select(r => r.Name).Should().Equal(["server", "client"],
                "the handler's own publish is skipped, so the approved scope stands");
    }

    [Fact]
    public void ApprovedRepoScope_Kept_IsTheApprovalWhenThereIsOneAndTheNarrowingOtherwise()
    {
        var approved = new ApprovedRepoScope.Scope([Repo("server"), Repo("client")], FromApproval: true);
        var classified = new ApprovedRepoScope.Scope([Repo("server"), Repo("client")], FromApproval: false);

        ApprovedRepoScope.Kept(approved, [Repo("server")]).Select(r => r.Name)
            .Should().Equal(["server", "client"], "an approved scope is never narrowed further");
        ApprovedRepoScope.Kept(classified, [Repo("server")]).Select(r => r.Name)
            .Should().Equal(["server"]);
        ApprovedRepoScope.Kept(classified, null).Select(r => r.Name).Should().Equal(["server", "client"]);
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
    private static string MustChange(params string[] repos)
    {
        var entries = repos.Select(r =>
            $"{{\"name\": \"{r}\", \"affected\": true, \"confidence\": 0.95}}");
        var expected = repos.Select(r => $"\"{r}\"");
        return $"{{\"repos\": [{string.Join(", ", entries)}], \"complexity\": \"medium\", "
            + $"\"expected_changes\": [{string.Join(", ", expected)}], "
            + "\"rationale\": \"both are affected.\"}}";
    }

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
            new ScopeRefusalRecorder(NullLogger<ScopeRefusalRecorder>.Instance),
            new ScopeNamedContextsRecorder(NullLogger<ScopeNamedContextsRecorder>.Instance),
            NullLogger<ScopeReposHandler>.Instance);
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
