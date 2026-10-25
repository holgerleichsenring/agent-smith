using System.Diagnostics;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Services.Sandbox;
using AgentSmith.Tests.Architecture;
using AgentSmith.Tests.Specs;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Integration;

/// <summary>
/// 2026-10-12-3a36b against real git: what the checkout merged from the base and what we committed
/// ourselves is never foreign; a commit by somebody else on the ticket branch is. Asked of git,
/// because whether a commit came from the base is a fact about the history, not the argument list.
/// </summary>
[Trait("TestProcess", "serial")]
[Collection(ExternalProcessCollection.Name)]
public sealed class ForeignCommitProbeTests : IAsyncDisposable
{
    private const string Branch = "agent-smith/1001";
    private const string Repo = "Server";
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"agentsmith-probe-{Guid.NewGuid():N}");

    private string Remote => Path.Combine(_root, "remote");
    private string Work => Path.Combine(_root, "work");

    [Fact]
    public async Task ForeignCommitProbe_BaseMergeAndOurCommits_AreNotForeign()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        var stored = Arrange(branchHasOwnWork: true);
        Commit(Remote, "main", "base.txt", "a colleague on main", "colleague@example.com");
        Git(Work, "fetch", "origin");
        Git(Work, "merge", "--no-edit", "--no-ff", "origin/main");
        Commit(Work, null, "ours.txt", "our next revision", SandboxGitIdentity.Email);

        (await ProbeAsync(stored)).Should().BeFalse("a base merge and our own commit are not foreign");
    }

    [Fact]
    public async Task ForeignCommitProbe_FastForwardedBase_IsNotForeign()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        var stored = Arrange(branchHasOwnWork: false);
        Commit(Remote, "main", "base.txt", "a colleague on main", "colleague@example.com");
        Git(Work, "fetch", "origin");
        Git(Work, "merge", "--ff-only", "origin/main");

        (await ProbeAsync(stored)).Should().BeFalse("base commits arriving by fast-forward are the base's");
    }

    [Fact]
    public async Task ForeignCommitProbe_ReviewerCommitOnTheBranch_IsForeign()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        var stored = Arrange(branchHasOwnWork: true);
        Commit(Remote, Branch, "fix.txt", "the reviewer's hand edit", "reviewer@example.com");
        Git(Work, "fetch", "origin");
        Git(Work, "merge", "--ff-only", $"origin/{Branch}");

        (await ProbeAsync(stored)).Should().BeTrue();
    }

    [Fact]
    public async Task ForeignCommitProbe_RepoWithoutStoredHeadOrUnknownSha_IsSkipped()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        Arrange(branchHasOwnWork: true);
        Commit(Work, null, "fix.txt", "somebody else", "reviewer@example.com");

        (await ProbeAsync(stored: null)).Should().BeFalse("no head was stored for the repository");
        (await ProbeAsync(new string('d', 40))).Should().BeFalse("a sha this clone does not know is skipped");
    }

    [Fact]
    public async Task ForeignCommitProbe_AnotherSandboxIdentity_IsSkipped()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return;
        var stored = Arrange(branchHasOwnWork: true);
        Git(Work, "config", "user.email", "operator@example.com");
        Commit(Work, null, "fix.txt", "somebody else", "reviewer@example.com");

        (await ProbeAsync(stored)).Should().BeFalse("under another identity ours cannot be told apart");
    }

    [Fact]
    public async Task ForeignCommitProbe_FilingAfterHandback_NotProbedWhenSetNotHandedBack()
    {
        var sandbox = new DerivationTestLooks.CountingSandbox(0, "reviewer@example.com");
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes, new Dictionary<string, ISandbox> { [Repo] = sandbox });
        var filed = new SpecSet("azdo-1", [], SpecAccounting.Empty, [], SpecSource.BranchArtifact);
        var pointer = new SpecSetPointer("azdo-1", Repo, "sha", 1,
            HandbackHeads: new Dictionary<string, string> { [Repo] = "sha" });

        var foreign = await HandbackTestDoubles.Probe().ProbeAsync(
            pipeline, [new RepoConnection { Name = Repo }], filed, pointer, CancellationToken.None);

        foreign.Should().BeFalse();
        sandbox.Ran.Should().BeEmpty("a set that is not handed back is never probed");
        pipeline.TryGet<bool>(ContextKeys.ForeignCommitSinceHandback, out var recorded).Should().BeTrue();
        recorded.Should().BeFalse();
    }

    private async Task<bool> ProbeAsync(string? stored)
    {
        await using var sandbox = new InProcessSandbox("probe", Work, ownsWorkDir: false, InProcessStepEnvironment.Inherit,
            NullLogger<InProcessSandbox>.Instance);
        return await HandbackTestDoubles.Probe().ForeignSinceAsync(sandbox, Repo, stored, null, CancellationToken.None);
    }

    /// <summary>A remote with main and the ticket branch, cloned with our identity; returns the
    /// branch's remote head — what the hand-back stored at parking.</summary>
    private string Arrange(bool branchHasOwnWork)
    {
        Directory.CreateDirectory(Remote);
        Git(Remote, "init", "-b", "main");
        Commit(Remote, null, "shared.txt", "base v1", "colleague@example.com");
        Git(Remote, "branch", Branch);
        if (branchHasOwnWork) Commit(Remote, Branch, "spec.txt", "our hand-back revision", SandboxGitIdentity.Email);
        Git(Remote, "checkout", "main");
        Git(_root, "clone", Remote, Work);
        Git(Work, "checkout", Branch);
        Git(Work, "config", "user.email", SandboxGitIdentity.Email);
        Git(Work, "config", "user.name", SandboxGitIdentity.Name);
        return Git(Work, "rev-parse", $"origin/{Branch}").Trim();
    }

    private static void Commit(string repo, string? branch, string file, string message, string email)
    {
        if (branch is not null) Git(repo, "checkout", branch);
        File.WriteAllText(Path.Combine(repo, file), message + "\n");
        Git(repo, "add", file);
        Git(repo, "-c", $"user.email={email}", "-c", "user.name=someone", "commit", "-m", message);
    }

    private static string Git(string workingDirectory, params string[] args)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        using var process = Process.Start(psi)!;
        var error = process.StandardError.ReadToEnd();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"git {string.Join(' ', args)} failed: {output}{error}");
        return output;
    }

    public ValueTask DisposeAsync()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        return ValueTask.CompletedTask;
    }
}
