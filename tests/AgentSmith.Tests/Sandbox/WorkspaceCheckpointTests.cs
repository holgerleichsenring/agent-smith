using AgentSmith.Application.Services;
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

/// <summary>2026-11-25-9992b: before a restart every repository's work reaches its remote, or the restart is refused.</summary>
public sealed class WorkspaceCheckpointTests
{
    private const string Sha = "0123456789abcdef0123456789abcdef01234567";

    private static WorkspaceCheckpoint Checkpoint()
    {
        var targets = new SandboxTargets();
        var gitOps = TestTreeSync.GitOps();
        return new WorkspaceCheckpoint(
            new RepoWorkPusher(gitOps, new SecretPatternScanner(), targets, NullLogger<RepoWorkPusher>.Instance),
            gitOps, new GitBranchPusher(EventTestStubs.NoOp, EventTestStubs.RunContext), TestGitCredentials.Resolver,
            targets, NullLogger<WorkspaceCheckpoint>.Instance);
    }

    private static PipelineContext Pipeline(ISandbox sandbox)
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, [new RepoConnection { Name = "api", Url = "https://host/api.git" }]);
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox> { ["api"] = new RepoSandbox(sandbox, "api") });
        pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos, new Dictionary<string, string> { ["api"] = "api" });
        return pipeline;
    }

    private static ScriptedGitSandbox OnBranch() => new ScriptedGitSandbox()
        .Answer("git rev-parse --abbrev-ref HEAD", 0, "run/42\n")
        .Answer("git rev-parse HEAD", 0, Sha + "\n");

    [Fact]
    public async Task SaveAsync_CleanTreeWhoseHeadIsOnNoRemoteRef_PushesItAndNamesTheHead()
    {
        var sandbox = OnBranch();

        var save = await Checkpoint().SaveAsync(Pipeline(sandbox), CancellationToken.None);

        save.Refusal.Should().BeNull();
        save.Repos.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Branch = "run/42", Head = Sha });
        sandbox.Lines.Should().Contain(line => line.Contains("push --force-with-lease origin HEAD:run/42"));
    }

    [Fact]
    public async Task SaveAsync_HeadAlreadyOnTheRemote_PushesNothing()
    {
        var sandbox = OnBranch().Answer("git branch -r --contains HEAD", 0, "  origin/run/42\n");

        await Checkpoint().SaveAsync(Pipeline(sandbox), CancellationToken.None);

        sandbox.Lines.Should().NotContain(line => line.Contains(" push "));
    }

    [Fact]
    public async Task SaveAsync_TreeStaysDirty_RefusesNamingTheRepository()
    {
        // The status always answers a change, as when the secret scan declined the checkpoint's diff.
        var sandbox = OnBranch().Answer("git status --porcelain", 0, " M .env\n")
            .Answer("git diff --cached", 0, "+password=hunter2hunter2hunter2\n+AKIAABCDEFGHIJKLMNOP\n");

        var save = await Checkpoint().SaveAsync(Pipeline(sandbox), CancellationToken.None);

        save.Refusal.Should().StartWith("api:");
        save.Repos.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAsync_DetachedHead_Refuses()
    {
        var sandbox = new ScriptedGitSandbox().Answer("git rev-parse --abbrev-ref HEAD", 0, "HEAD\n");

        var save = await Checkpoint().SaveAsync(Pipeline(sandbox), CancellationToken.None);

        save.Refusal.Should().Contain("on no branch");
    }
}
