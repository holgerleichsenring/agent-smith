using System.Diagnostics;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Source;
using AgentSmith.Tests.Architecture;
using FluentAssertions;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>
/// 2026-09-22-b6ad: putting files on a branch WITHOUT a checkout, driven against a REAL git
/// repository — the whole claim is about what the refs and the trees say afterwards, and about
/// the operator's working tree being untouched, neither of which a mocked process could show.
/// <para>
/// The local provider is the one with no remote, and it is the one that must still answer a
/// COMMIT SHA: the caller records that sha as the pointer at the spec path, and an absent pointer
/// is read by every later run as somebody else's edit.
/// </para>
/// </summary>
[Collection(ExternalProcessCollection.Name)]
public sealed class LocalSourceProviderBranchWriteTests : IDisposable
{
    private const string Ticket = "agent-smith/19106";
    private const string SpecPath = ".agentsmith/specs/azuredevops-19106/set.yaml";

    private readonly string _repoPath = Path.Combine(
        Path.GetTempPath(), $"agentsmith-b6ad-{Guid.NewGuid():N}");

    public LocalSourceProviderBranchWriteTests()
    {
        Directory.CreateDirectory(_repoPath);
        Git("init", "--initial-branch=main");
        Git("config", "user.email", "test@example.com");
        Git("config", "user.name", "Test");
        Commit("README.md", "base");
    }

    public void Dispose()
    {
        try { Directory.Delete(_repoPath, recursive: true); }
        catch (IOException) { /* a temp dir the OS still holds is not this test's problem */ }
    }

    [Fact]
    public async Task SourceProvider_WritingToABranchThatDoesNotExist_CreatesItAtTheDefaultBranchHead()
    {
        var mainHead = Rev("main");

        var result = await Write([new RepoFile(SpecPath, "key: azuredevops-19106\n")]);

        result.Written.Should().BeTrue(result.Error);
        Rev(Ticket).Should().Be(result.CommitSha);
        Git("rev-parse", $"{Ticket}^").Trim().Should().Be(mainHead,
            "the branch is cut at the default branch's head, not orphaned");
        Show($"{Ticket}:{SpecPath}").Should().Be("key: azuredevops-19106\n");
        Show($"{Ticket}:README.md").Should().Be("base",
            "the base tree survives — the write adds files, it does not replace the repository");
    }

    /// <summary>
    /// The operator's own checkout is a working tree somebody may be using. The commit is made
    /// through a scratch index, so neither their staged changes nor their unstaged edits are
    /// swept into it and nothing of ours appears in their tree.
    /// </summary>
    [Fact]
    public async Task SourceProvider_WritingToABranch_LeavesTheOperatorsWorkingTreeAlone()
    {
        File.WriteAllText(Path.Combine(_repoPath, "in-progress.txt"), "half-written");
        Git("add", "in-progress.txt");

        await Write([new RepoFile(SpecPath, "key: azuredevops-19106\n")]);

        Git("status", "--porcelain").Trim().Should().Be("A  in-progress.txt",
            "the operator's staged file is still staged and nothing else moved");
        File.Exists(Path.Combine(_repoPath, SpecPath)).Should().BeFalse(
            "the spec landed in the object store on another branch, not in their checkout");
        Git("rev-parse", "--abbrev-ref", "HEAD").Trim().Should().Be("main");
    }

    [Fact]
    public async Task SourceProvider_WritingToABranchThatExists_CommitsOnItsHeadWithoutForcing()
    {
        await Write([new RepoFile(SpecPath, "revision: 1\n")]);
        var first = Rev(Ticket);

        var second = await Write([new RepoFile(SpecPath, "revision: 2\n")]);

        second.Written.Should().BeTrue(second.Error);
        Git("rev-parse", $"{Ticket}^").Trim().Should().Be(first,
            "the existing branch is committed ONTO — never re-cut and never forced");
        Show($"{Ticket}:{SpecPath}").Should().Be("revision: 2\n");
    }

    /// <summary>
    /// A ref that moved under us is the case forcing would destroy. <c>update-ref</c> is given
    /// the value it expects the ref to hold, so the write is refused with the reason instead.
    /// </summary>
    [Fact]
    public async Task SourceProvider_AWriteThatTheRemoteRefuses_AnswersTheReasonRatherThanThrowingPast()
    {
        var missing = Path.Combine(_repoPath, "not-a-repository-at-all");

        var result = await new LocalSourceProvider(missing, "main").WriteFilesToBranchAsync(
            new BranchName(Ticket), [new RepoFile(SpecPath, "x")], "spec", CancellationToken.None);

        result.Written.Should().BeFalse();
        result.CommitSha.Should().BeNull("a write with no commit must not look like one");
        result.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task SourceProvider_WritingSeveralFiles_PutsThemAllInOneCommit()
    {
        var result = await Write(
        [
            new RepoFile(SpecPath, "key: azuredevops-19106\n"),
            new RepoFile(".agentsmith/specs/azuredevops-19106/p1-first.yaml", "phase: p1\n"),
            new RepoFile(".agentsmith/specs/azuredevops-19106/accounting.md", "# nothing\n"),
        ]);

        result.Written.Should().BeTrue(result.Error);
        Git("rev-list", "--count", $"main..{Ticket}").Trim().Should().Be("1",
            "three files are one revision, not three");
        Show($"{Ticket}:.agentsmith/specs/azuredevops-19106/p1-first.yaml").Should().Be("phase: p1\n");
        Show($"{Ticket}:.agentsmith/specs/azuredevops-19106/accounting.md").Should().Be("# nothing\n");
    }

    private Task<BranchWriteResult> Write(IReadOnlyList<RepoFile> files) =>
        new LocalSourceProvider(_repoPath, "main").WriteFilesToBranchAsync(
            new BranchName(Ticket), files, "spec: azuredevops-19106 revision 1",
            CancellationToken.None);

    private string Rev(string branch) => Git("rev-parse", branch).Trim();

    private string Show(string spec) => Git("show", spec);

    private void Commit(string relativePath, string content)
    {
        var full = Path.Combine(_repoPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        Git("add", "-A");
        Git("commit", "-m", $"add {relativePath}");
    }

    private string Git(params string[] arguments)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = _repoPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments) psi.ArgumentList.Add(argument);
        psi.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        process.ExitCode.Should().Be(0, $"git {string.Join(' ', arguments)} failed: {stderr}");
        return stdout;
    }
}
