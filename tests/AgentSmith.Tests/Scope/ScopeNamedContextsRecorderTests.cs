using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Scope;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Scope;

/// <summary>
/// 2026-09-08-1830: what the scope call NAMED is recorded whether or not it narrowed —
/// run 8688 named both contexts of its one repository, dropped nothing, and the claim
/// existed nowhere the derivation could be held against.
/// </summary>
public sealed class ScopeNamedContextsRecorderTests
{
    private readonly ScopeNamedContextsRecorder _recorder = new(NullLogger<ScopeNamedContextsRecorder>.Instance);

    [Fact]
    public void Record_OneRepoNamingBothContexts_RecordsBareNamesAndTheRationale()
    {
        var pipeline = new PipelineContext();

        _recorder.Record(pipeline, Classification(("app", ["frontend", "backend"])), Repos("app"), Inventory(("app", ["frontend", "backend"])));

        var named = pipeline.Get<ScopeNamedContexts>(ContextKeys.ScopeNamedContexts);
        named.Contexts.Should().Equal("frontend", "backend");
        named.Rationale.Should().Be("both contexts need dependency auditing");
    }

    [Fact]
    public void Record_TwoReposInScope_QualifiesEveryNameWithItsRepo()
    {
        var pipeline = new PipelineContext();

        _recorder.Record(
            pipeline, Classification(("server", ["api", "worker"]), ("client", ["web", "admin"])),
            Repos("server", "client"),
            Inventory(("server", ["api", "worker"]), ("client", ["web", "admin"])));

        pipeline.Get<ScopeNamedContexts>(ContextKeys.ScopeNamedContexts).Contexts
            .Should().Equal("server/api", "server/worker", "client/web", "client/admin");
    }

    [Fact]
    public void Record_ASingleContextRepo_RecordsNothing()
    {
        // There is no cut across contexts to miss when the repository has one.
        var pipeline = new PipelineContext();

        _recorder.Record(pipeline, Classification(("app", ["default"])), Repos("app"), Inventory(("app", ["default"])));

        pipeline.Has(ContextKeys.ScopeNamedContexts).Should().BeFalse();
    }

    [Fact]
    public void Record_ANameTheInventoryDoesNotHold_IsDroppedAndTheRestKept()
    {
        var pipeline = new PipelineContext();

        _recorder.Record(pipeline, Classification(("app", ["backend", "ghost"])), Repos("app"), Inventory(("app", ["frontend", "backend"])));

        pipeline.Get<ScopeNamedContexts>(ContextKeys.ScopeNamedContexts).Contexts.Should().Equal("backend");
    }

    [Fact]
    public void Record_NoContextVerdict_RecordsNothing()
    {
        var pipeline = new PipelineContext();

        _recorder.Record(pipeline, new RepoScopeClassification([], "no verdict"), Repos("app"), Inventory(("app", ["frontend", "backend"])));
        _recorder.Record(pipeline, null, Repos("app"), Inventory(("app", ["frontend", "backend"])));

        pipeline.Has(ContextKeys.ScopeNamedContexts).Should().BeFalse();
    }

    private static RepoScopeClassification Classification(params (string Repo, string[] Contexts)[] named) =>
        new([], "both contexts need dependency auditing",
            named.ToDictionary(n => n.Repo, n => (IReadOnlyList<string>)n.Contexts, StringComparer.OrdinalIgnoreCase));

    private static IReadOnlyList<RepoConnection> Repos(params string[] names) =>
        [.. names.Select(n => new RepoConnection { Name = n })];

    private static IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>> Inventory(
        params (string Repo, string[] Contexts)[] repos) =>
        repos.ToDictionary(
            r => r.Repo,
            r => (IReadOnlyList<RemoteContextDiscovery>)[.. r.Contexts.Select(c => new RemoteContextDiscovery(c, c, "node"))],
            StringComparer.Ordinal);
}
