using AgentSmith.Application.Services.Configuration;
using AgentSmith.Application.Services.Memory;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Memory;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Memory;

/// <summary>
/// 2026-11-10-fcd7a: a run's memory sources — the project's own store first under '@project' when the project
/// has a config row, then each repository; a repository may not take a name the project store answers to; and
/// the DB-free store says why nothing was kept.
/// </summary>
public sealed class MemorySourcesTests
{
    private sealed class OneProject(string name, long id) : IProjectMemoryRepository
    {
        public List<ProjectMemoryRecord> Rows { get; } = [];
        public bool IsAvailable => true;
        public Task<long?> ResolveProjectAsync(string project, CancellationToken ct) =>
            Task.FromResult<long?>(project == name ? id : null);
        public Task<IReadOnlyList<ProjectMemoryRecord>> ListAsync(long projectId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ProjectMemoryRecord>>(Rows);
        public Task UpsertAsync(long projectId, ProjectMemoryRecord record, CancellationToken ct)
        {
            Rows.Add(record);
            return Task.CompletedTask;
        }
        public Task<bool> DeleteAsync(long projectId, string slug, CancellationToken ct) => Task.FromResult(false);
    }

    private static PipelineContext Pipeline(string project)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ProjectName, project);
        return pipeline;
    }

    [Fact]
    public async Task MemorySources_ProjectFirst_UnderAtProject()
    {
        var sources = new MemorySources(new OneProject("sample", 42), new MemoryRepositoryKey());

        var listed = await sources.OfAsync(
            Pipeline("sample"), TestRepositories.One(new InMemorySandboxFileReader(), "api"), "/work", null, default);

        listed.Select(s => (s.Label, s.LedgerKey)).Should().Equal(
            (ProjectMemorySource.Label, "project-memory:42"), ("api", "api"));
        listed[0].IsProject.Should().BeTrue();
    }

    [Fact]
    public async Task MemorySources_ProjectWithoutConfigRow_ListsOnlyRepositories()
    {
        var sources = new MemorySources(new OneProject("sample", 42), new MemoryRepositoryKey());

        var listed = await sources.OfAsync(
            Pipeline("other"), TestRepositories.One(new InMemorySandboxFileReader(), "api"), "/work", null, default);

        listed.Select(s => s.Label).Should().Equal("api");
    }

    [Fact]
    public async Task ProjectMemoryStore_Upsert_CarriesProvenanceAndListsAsEntries()
    {
        var repository = new OneProject("sample", 42);
        var store = new ProjectMemoryStore(repository, 42);

        await store.UpsertAsync(new MemoryEntry("no-shims", "Migrate callers", MemoryEntryType.Feedback, "body"), default,
            new MemoryProvenance(ProjectMemorySourceKinds.Conversation, "conv-1", "4", "Holger"));

        repository.Rows.Should().ContainSingle().Which.Should().Match<ProjectMemoryRecord>(r =>
            r.Type == "feedback" && r.SourceId == "conv-1" && r.SourceRef == "4" && r.Author == "Holger");
        (await store.ListAsync(default)).Should().ContainSingle().Which.Type.Should().Be(MemoryEntryType.Feedback);
        (await store.ReadIndexAsync(default)).Should().Contain("[no-shims]");
    }

    [Fact]
    public async Task NullProjectMemoryRepository_Write_SaysNoStore()
    {
        var store = new NullProjectMemoryRepository();

        store.IsAvailable.Should().BeFalse();
        var write = () => store.UpsertAsync(1, new ProjectMemoryRecord("x", "feedback", "d", "b", "run", "r", null, null), default);
        await write.Should().ThrowAsync<InvalidOperationException>().WithMessage("*no database*");
    }

    [Fact]
    public void ConfigValidation_RepositoryNameStartingWithAt_IsRefused()
    {
        var config = new AgentSmithConfig
        {
            Repos = new() { ["@project"] = new RepoConnection { Name = "@project" }, ["api"] = new RepoConnection { Name = "api" } },
        };

        RepositoryNameConfigValidator.Findings(config).Should().ContainSingle()
            .Which.Reason.Should().Contain("'@project'").And.Contain("may not start with '@'");
    }
}
