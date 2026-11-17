using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.Specs.Branch;

/// <summary>2026-11-17-a0b6a: one branch as a read-only file source through the host's API.</summary>
public sealed class BranchFilesTests
{
    [Fact]
    public async Task ListAsync_AnswersRepositoryRelativePaths()
    {
        var files = await FilesAsync(("dir/a.yaml", "a"), ("dir/b.yaml", "b"), ("dir/sub/c.yaml", "c"));

        (await files.ListAsync("dir", maxDepth: 1, default)).Should().BeEquivalentTo("dir/a.yaml", "dir/b.yaml");
    }

    [Fact]
    public async Task TryReadAsync_AMissingFile_IsNull()
    {
        var files = await FilesAsync(("dir/a.yaml", "a"));

        (await files.TryReadAsync("dir/a.yaml", default)).Should().Be("a");
        (await files.TryReadAsync("dir/none.yaml", default)).Should().BeNull();
        (await files.ExistsAsync("dir/none.yaml", default)).Should().BeFalse();
    }

    [Fact]
    public async Task WriteAsync_Throws()
    {
        var files = await FilesAsync();

        await files.Invoking(f => f.WriteAsync("dir/a.yaml", "a", default)).Should().ThrowAsync<NotSupportedException>();
    }

    private static async Task<BranchFiles> FilesAsync(params (string Path, string Content)[] content)
    {
        var sources = new RecordingBranchSources();
        var provider = sources.Create(new RepoConnection { Name = "repo" });
        await provider.WriteFilesToBranchAsync(new BranchName("b"),
            [.. content.Select(c => new AgentSmith.Contracts.Providers.RepoFile(c.Path, c.Content))], "m", default);
        return new BranchFiles(provider, new BranchName("b"));
    }
}
