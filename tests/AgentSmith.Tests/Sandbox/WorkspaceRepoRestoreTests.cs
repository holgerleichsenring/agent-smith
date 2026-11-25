using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.TestHelpers;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>2026-11-25-9992b: a restored repository lands exactly on its saved branch and commit, or the run ends.</summary>
public sealed class WorkspaceRepoRestoreTests
{
    private const string Sha = "0123456789abcdef0123456789abcdef01234567";
    private static readonly RepoConnection Api = new() { Name = "api", Url = "https://host/api.git" };

    private static WorkspaceRepoRestore Restore() => new(
        TestGitCredentials.Resolver, TestGit.Identity, new SandboxTargets(),
        new MidRunRegistryAuth(null!, new AgentSmithConfig()), new PrerequisiteInstall(NullLogger<PrerequisiteInstall>.Instance),
        NullLogger<WorkspaceRepoRestore>.Instance);

    private static PipelineContext Pipeline(ISandbox sandbox, string? prerequisites = null)
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, [Api]);
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox> { ["api"] = new RepoSandbox(sandbox, "api") });
        pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos, new Dictionary<string, string> { ["api"] = "api" });
        pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(ContextKeys.SandboxDiscoveries,
            new Dictionary<string, RemoteContextDiscovery> { ["api"] = new("default", ".", "csharp", Prerequisites: prerequisites) });
        return pipeline;
    }

    [Fact]
    public async Task RestoreAsync_MakesTheRootClonesAndChecksTheSavedCommitOut_InThatOrder()
    {
        var sandbox = new ScriptedGitSandbox().Answer("git rev-parse HEAD", 0, Sha + "\n");

        var notes = await Restore().RestoreAsync(Pipeline(sandbox, "npm ci"), new WorkspaceRepoState(Api, "run/42", Sha), CancellationToken.None);

        notes.Should().BeEmpty();
        sandbox.Lines.Should().ContainInOrder(
            "mkdir -p api", "git clone https://host/api.git .", $"git checkout -B run/42 {Sha}", "git rev-parse HEAD", "npm ci");
        sandbox.Steps.Single(s => s.Command == "mkdir").WorkingDirectory.Should().Be("/work");
        sandbox.Steps.Where(s => s.Command == "git").Should().OnlyContain(s => s.WorkingDirectory == "/work/api");
    }

    [Fact]
    public async Task RestoreAsync_CloneStandsElsewhere_EndsTheRunNamingTheRepository()
    {
        var sandbox = new ScriptedGitSandbox().Answer("git rev-parse HEAD", 0, "ffffffffffffffffffffffffffffffffffffffff\n");

        var act = () => Restore().RestoreAsync(Pipeline(sandbox), new WorkspaceRepoState(Api, "run/42", Sha), CancellationToken.None);

        (await act.Should().ThrowAsync<WorkspaceRestoreException>()).WithMessage("*api could not be restored at run/42*");
    }

    [Fact]
    public async Task RestoreAsync_SavedCommitNotOnTheRemote_EndsTheRun()
    {
        var sandbox = new ScriptedGitSandbox().Answer("git checkout -B", 128, "fatal: reference is not a tree");

        var act = () => Restore().RestoreAsync(Pipeline(sandbox), new WorkspaceRepoState(Api, "run/42", Sha), CancellationToken.None);

        (await act.Should().ThrowAsync<WorkspaceRestoreException>()).WithMessage($"*{Sha} is not on its remote*");
    }

    [Fact]
    public async Task RestoreAsync_PrerequisiteFails_SaysSoAndKeepsGoing()
    {
        var sandbox = new ScriptedGitSandbox().Answer("git rev-parse HEAD", 0, Sha + "\n").Answer("npm ci", 1, "ERR");

        var notes = await Restore().RestoreAsync(Pipeline(sandbox, "npm ci"), new WorkspaceRepoState(Api, "run/42", Sha), CancellationToken.None);

        notes.Should().ContainSingle().Which.Should().Contain("prerequisites exited 1");
    }
}
