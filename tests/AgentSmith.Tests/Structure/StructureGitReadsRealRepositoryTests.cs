using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Structure;
using AgentSmith.Infrastructure.Services.Sandbox;
using AgentSmith.Tests.Architecture;
using AgentSmith.Tests.Integration;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Structure;

/// <summary>
/// 2026-10-13-2d0c: the structure readers against a REAL git repository, so the stub in
/// <see cref="RepoSandbox"/> answers what git answers — -z listings, '--' before paths, untracked
/// files, deletions left out, and a base that moved after the cut resolved to the merge-base.
/// </summary>
[Trait("TestProcess", "serial")]
[Collection(ExternalProcessCollection.Name)]
public sealed class StructureGitReadsRealRepositoryTests : IAsyncLifetime
{
    private const string Principles = ".agentsmith/contexts/api/principles.md";
    private const string PrinciplesText = "## Limits\n\n```yaml\nfunction_lines: 4\n```\n";
    private InProcessSandbox? _sandbox;

    public async Task InitializeAsync()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        var dir = Path.Combine(Path.GetTempPath(), "as-2d0c-git-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        _sandbox = new InProcessSandbox("test", dir, ownsWorkDir: true, NullLogger.Instance);
        await GitAsync("init", "-q", "-b", "main");
        await GitAsync("config", "user.email", "test@example.test");
        await GitAsync("config", "user.name", "test");
        await GitAsync("config", "commit.gpgsign", "false");
        Write(Principles, PrinciplesText);
        Write("keep.cs", "class Keep { }\n");
        Write("gone.cs", "class Gone { }\n");
        Write("shared.cs", "class Shared { }\n");
        await CommitAllAsync("base");
        await CutBranchAsync();
        await MoveMainAsync();
        Write("new.cs", "class New { }\n");
    }

    public async Task DisposeAsync()
    {
        if (_sandbox is not null) await _sandbox.DisposeAsync();
    }

    [Fact]
    public async Task StructureGitReads_RealRepository_ListsLikeTheStub()
    {
        if (_sandbox is null) return;
        var reads = StructureBaselineStubs.GitReads();

        var resolved = await StructureGateFixture.MergeBase().ResolveAsync(_sandbox, new DeliveryBasis(null, null), default);

        resolved.Should().NotBeNull();
        resolved!.Value.Rung.Should().Be("origin/main");
        resolved.Value.Commit.Should().Be((await GitAsync("rev-parse", "base")).Trim());
        (await reads.ChangedPathsAsync(_sandbox, resolved.Value.Commit, default)).Should().BeEquivalentTo(
            ["keep.cs", "-dash.cs", "src/With Space.cs", "new.cs"], "deletions are left out, untracked files are in");
        (await reads.ChangedPathsAsync(_sandbox, "origin/main", default)).Should().Contain(
            "shared.cs", "against the moved rung, a file only main changed would be judged — the stub's MovedOnBase");
    }

    [Fact]
    public async Task StructureGitReads_RealRepository_ReadsPresenceAbsenceAndFailure()
    {
        if (_sandbox is null) return;
        var reads = StructureBaselineStubs.GitReads();
        var commit = (await GitAsync("rev-parse", "base")).Trim();

        (await reads.ReadAtAsync(_sandbox, commit, Principles, default)).Should().Be(new StructureBaseRead(PrinciplesText));
        (await reads.ReadAtAsync(_sandbox, commit, ".agentsmith/contexts/api/structure-baseline.tsv", default))
            .Should().Be(StructureBaseRead.Absent);
        (await reads.ReadAtAsync(_sandbox, "no-such-revision", Principles, default)).Failure.Should().Contain("ls-tree");
        (await StructureBaselineStubs.Listing().ListAsync(_sandbox, "src", [".cs"], default)).Paths
            .Should().Equal("src/With Space.cs");
    }

    // The branch: keep.cs edited, gone.cs deleted, two added — one with a space, one dash-leading.
    private async Task CutBranchAsync()
    {
        await GitAsync("tag", "base");
        await GitAsync("checkout", "-q", "-b", "work");
        Write("keep.cs", "class Keep { void Run() { } }\n");
        await GitAsync("rm", "-q", "--", "gone.cs");
        Write("src/With Space.cs", "class Spaced { }\n");
        Write("-dash.cs", "class Dash { }\n");
        await CommitAllAsync("branch");
    }

    // origin/main moves after the cut: shared.cs changes there and nowhere on the branch.
    private async Task MoveMainAsync()
    {
        await GitAsync("checkout", "-q", "main");
        Write("shared.cs", "class Shared { void Changed() { } }\n");
        await CommitAllAsync("main moves");
        await GitAsync("update-ref", "refs/remotes/origin/main", "HEAD");
        await GitAsync("symbolic-ref", "refs/remotes/origin/HEAD", "refs/remotes/origin/main");
        await GitAsync("checkout", "-q", "work");
    }

    private async Task CommitAllAsync(string message)
    {
        await GitAsync("add", "-A");
        await GitAsync("commit", "-q", "-m", message);
    }

    private void Write(string path, string content)
    {
        var full = Path.Combine(_sandbox!.WorkDir, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    private async Task<string> GitAsync(params string[] args)
    {
        var result = await new StructureGitRunner().RunAsync(_sandbox!, args, default);
        result.ExitCode.Should().Be(0, $"git {string.Join(' ', args)}: {result.ErrorMessage}");
        return result.OutputContent ?? string.Empty;
    }
}
