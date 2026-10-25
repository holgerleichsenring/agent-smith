using AgentSmith.Application.Services.Memory;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Memory;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Memory.Standing;

/// <summary>2026-10-25-9324a: a run's repository is keyed by its configured URL, the way the PR sweep keys it.</summary>
public sealed class MemoryRepositoryKeyTests
{
    private readonly MemoryRepositoryKey _key = new();

    [Fact]
    public void MemoryRepositoryKey_ProjectRepository_IsTheKeyOfItsUrl()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ProjectConfig, new ResolvedProject
        {
            Repos = [new RepoConnection { Name = "api", Url = "https://github.com/O/Api.git" }],
        });

        _key.Of(pipeline, "api").Should().Be("github.com/o/api");
    }

    [Fact]
    public void MemoryRepositoryKey_OnlyInTheRunsRepositories_IsTheKeyOfThatUrl()
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, [new RepoConnection { Name = "web", Url = "https://github.com/o/web/" }]);

        _key.Of(pipeline, "web").Should().Be("github.com/o/web");
    }

    [Fact]
    public void MemoryRepositoryKey_NoUrl_IsTheName() =>
        _key.Of(new PipelineContext(), "Local").Should().Be("local");

    [Fact]
    public async Task NullMemoryEvidenceStore_KeepsNothing()
    {
        var store = new NullMemoryEvidenceStore();
        var fact = new MemoryEvidenceFact("k", "x", MemoryEvidenceKinds.Relied, 0, "p", "t", "r", "s", null, DateTimeOffset.UnixEpoch);

        await store.AppendAsync(fact, default);
        await store.UpsertOutcomeAsync(fact, default);
        await store.ReplaceAsync("r", "s", MemoryEvidenceKinds.Relied, [fact], default);

        (await store.OfRepositoryAsync("k", default)).Should().BeEmpty();
    }
}
