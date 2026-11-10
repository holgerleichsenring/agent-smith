using AgentSmith.Contracts.Memory;
using AgentSmith.Server.Extensions;
using AgentSmith.Tests.Architecture;
using AgentSmith.Tests.Memory;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AgentSmith.Tests.Server;

/// <summary>
/// 2026-11-10-fcd7d: a project's memories for a person — listed newest first with standing and provenance under
/// runs.read on the project, deleted under runs.control on it, 404 for a project with no config row. The
/// permissions are the route table's, which RoutePermissionGuardTests pins as a golden.
/// </summary>
public sealed class ProjectMemoryEndpointTests
{
    private static readonly string[] Routes = File.ReadAllLines(Path.Combine(
        ArchitectureSources.RepositoryRoot, "tests", "AgentSmith.Tests", "Server", "route-permission-baseline.tsv"));

    private static ProjectMemoryDesignTurnTests.ProjectRows Rows()
    {
        var rows = new ProjectMemoryDesignTurnTests.ProjectRows();
        rows.Rows.Add(new ProjectMemoryRecord("older", "project", "an older fact", "b", ProjectMemorySourceKinds.Run, "run-1", "s-1", null));
        rows.Rows.Insert(0, new ProjectMemoryRecord("newer", "feedback", "a newer rule", "b", ProjectMemorySourceKinds.Conversation, "conv-1", "4", "Holger"));
        return rows;
    }

    [Fact]
    public void ProjectMemoryEndpoints_Get_NeedsRunsReadOnTheProject() =>
        Routes.Should().Contain("GET\t/api/projects/{name}/memories\truns.read\troute:name");

    [Fact]
    public void ProjectMemoryEndpoints_Delete_NeedsRunsControlOnTheProject() =>
        Routes.Should().Contain("DELETE\t/api/projects/{name}/memories/{slug}\truns.control\troute:name");

    [Fact]
    public async Task ProjectMemoryEndpoints_List_NewestFirstWithStandingAndProvenance()
    {
        var result = await ProjectMemoryEndpoints.ListAsync("sample", Rows(), MemoryStandings.Reader(), default);

        var views = result.Should().BeOfType<Ok<List<ProjectMemoryView>>>().Subject.Value!;
        views.Select(v => v.Slug).Should().Equal("newer", "older");
        (views[0].Standing, views[0].SourceKind, views[0].SourceId, views[0].SourceRef, views[0].Author)
            .Should().Be(("unconfirmed", ProjectMemorySourceKinds.Conversation, "conv-1", "4", "Holger"));
    }

    [Fact]
    public async Task ProjectMemoryEndpoints_UnknownProject_IsNotFound()
    {
        (await ProjectMemoryEndpoints.ListAsync("other", Rows(), MemoryStandings.Reader(), default)).Should().BeOfType<NotFound>();
        (await ProjectMemoryEndpoints.DeleteAsync("other", "newer", Rows(), default)).Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task ProjectMemoryEndpoints_Delete_ExistingSlug_IsNoContent()
    {
        var deleted = new DeletingRows();

        (await ProjectMemoryEndpoints.DeleteAsync("sample", "newer", deleted, default)).Should().BeOfType<NoContent>();
        deleted.Deleted.Should().Equal("newer");
    }

    private sealed class DeletingRows : IProjectMemoryRepository
    {
        public List<string> Deleted { get; } = [];
        public bool IsAvailable => true;
        public Task<long?> ResolveProjectAsync(string project, CancellationToken ct) => Task.FromResult<long?>(project == "sample" ? 42 : null);
        public Task<IReadOnlyList<ProjectMemoryRecord>> ListAsync(long projectId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ProjectMemoryRecord>>([]);
        public Task UpsertAsync(long projectId, ProjectMemoryRecord record, CancellationToken ct) => Task.CompletedTask;
        public Task<bool> DeleteAsync(long projectId, string slug, CancellationToken ct)
        {
            Deleted.Add(slug);
            return Task.FromResult(true);
        }
    }
}
