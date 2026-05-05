using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using LibGit2Sharp;

namespace AgentSmith.Tests.Providers;

/// <summary>
/// Regression guard for LocalSourceProvider.StageAllChanges. Original implementation
/// used Commands.Stage(repo, "*") which expanded the glob inside libgit2 and could
/// emit directory-shaped paths (e.g. "RHS.CICD/") that git_index_add_bypath then
/// rejected with "invalid path: ...". Replaced with per-file iteration via
/// RetrieveStatus. These tests run against a real on-disk LibGit2Sharp repo.
/// </summary>
public sealed class LocalSourceProviderStagingTests : IDisposable
{
    private readonly string _repoDir;

    public LocalSourceProviderStagingTests()
    {
        _repoDir = Path.Combine(Path.GetTempPath(), $"as-stage-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_repoDir);
        Repository.Init(_repoDir);
    }

    [Fact]
    public void StageAllChanges_NewFileInRoot_StagedSuccessfully()
    {
        File.WriteAllText(Path.Combine(_repoDir, "new.txt"), "hello");

        using var repo = new Repository(_repoDir);
        LocalSourceProvider.StageAllChanges(repo);

        repo.Index.Should().Contain(e => e.Path == "new.txt");
    }

    [Fact]
    public void StageAllChanges_NewFilesInUntrackedSubdirectory_AllStagedNoExceptions()
    {
        Directory.CreateDirectory(Path.Combine(_repoDir, "RHS.CICD"));
        File.WriteAllText(Path.Combine(_repoDir, "RHS.CICD", "config.yaml"), "k: v");
        File.WriteAllText(Path.Combine(_repoDir, "RHS.CICD", "deploy.yaml"), "k: v");

        using var repo = new Repository(_repoDir);
        var act = () => LocalSourceProvider.StageAllChanges(repo);

        act.Should().NotThrow();
        repo.Index.Should().Contain(e => e.Path == "RHS.CICD/config.yaml");
        repo.Index.Should().Contain(e => e.Path == "RHS.CICD/deploy.yaml");
    }

    [Fact]
    public void StageAllChanges_ModifiedTrackedFile_Staged()
    {
        var path = Path.Combine(_repoDir, "tracked.txt");
        File.WriteAllText(path, "v1");
        using (var repo = new Repository(_repoDir))
        {
            LibGit2Sharp.Commands.Stage(repo, "tracked.txt");
            var sig = new Signature("Test", "test@example.com", DateTimeOffset.UtcNow);
            repo.Commit("init", sig, sig);
        }
        File.WriteAllText(path, "v2");

        using var repo2 = new Repository(_repoDir);
        LocalSourceProvider.StageAllChanges(repo2);

        var staged = repo2.Diff.Compare<TreeChanges>(repo2.Head.Tip.Tree, DiffTargets.Index);
        staged.Modified.Should().Contain(c => c.Path == "tracked.txt");
    }

    [Fact]
    public void StageAllChanges_DeletedTrackedFile_Staged()
    {
        var path = Path.Combine(_repoDir, "doomed.txt");
        File.WriteAllText(path, "bye");
        using (var repo = new Repository(_repoDir))
        {
            LibGit2Sharp.Commands.Stage(repo, "doomed.txt");
            var sig = new Signature("Test", "test@example.com", DateTimeOffset.UtcNow);
            repo.Commit("init", sig, sig);
        }
        File.Delete(path);

        using var repo2 = new Repository(_repoDir);
        LocalSourceProvider.StageAllChanges(repo2);

        var staged = repo2.Diff.Compare<TreeChanges>(repo2.Head.Tip.Tree, DiffTargets.Index);
        staged.Deleted.Should().Contain(c => c.Path == "doomed.txt");
    }

    [Fact]
    public void StageAllChanges_NoChanges_DoesNotThrow()
    {
        using var repo = new Repository(_repoDir);
        var act = () => LocalSourceProvider.StageAllChanges(repo);

        act.Should().NotThrow();
    }

    public void Dispose()
    {
        if (!Directory.Exists(_repoDir)) return;
        try
        {
            foreach (var path in Directory.EnumerateFileSystemEntries(_repoDir, "*", SearchOption.AllDirectories))
            {
                var attrs = File.GetAttributes(path);
                if (attrs.HasFlag(FileAttributes.ReadOnly))
                    File.SetAttributes(path, attrs & ~FileAttributes.ReadOnly);
            }
            Directory.Delete(_repoDir, recursive: true);
        }
        catch (IOException) { /* leftover .git locks on some platforms — best-effort cleanup */ }
    }
}
