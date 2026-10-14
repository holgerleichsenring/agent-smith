using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>The sandbox tree sync with its collaborators, wired the way the container wires
/// them; the secret scan can be swapped for one that declines.</summary>
internal static class TestTreeSync
{
    public static SandboxGitOperations GitOps() =>
        new(new GitBranchPusher(), AgentSmith.Tests.TestSupport.TestGitCredentials.Resolver,
            NullLogger<SandboxGitOperations>.Instance, new SandboxFileReaderFactory(), TestGit.Identity);

    public static SandboxTreeGit TreeGit() => new(new SandboxBinaryFileWriter(), TestGit.Identity);

    public static RunWorkCheckpointer Checkpointer(SandboxTargets targets, ISecretPatternScanner? scanner = null) =>
        new(new RepoWorkPusher(GitOps(), scanner ?? new SecretPatternScanner(), targets,
                NullLogger<RepoWorkPusher>.Instance),
            NullLogger<RunWorkCheckpointer>.Instance);

    public static StageRelevance Relevance() => new(new SandboxTargets(), NullLogger<StageRelevance>.Instance);

    /// <summary>run_gate_stage over the real sync, resolver and runner.</summary>
    public static AgentSmith.Application.Services.Tools.GateStageToolHost GateStage(
        SandboxTargets? targets = null, ISecretPatternScanner? scanner = null)
    {
        var resolved = targets ?? new SandboxTargets();
        return new(Create(resolved, scanner), resolved, new ContextVerifyStagesResolver(),
            new VerifyStageResolver(new DeclaredStagePresence(new SandboxFileReaderFactory(),
                NullLogger<DeclaredStagePresence>.Instance), NullLogger<VerifyStageResolver>.Instance),
            new VerifyCommandRunner(NullLogger<VerifyCommandRunner>.Instance));
    }

    public static SandboxTreeSync Create(SandboxTargets? targets = null, ISecretPatternScanner? scanner = null)
    {
        var resolved = targets ?? new SandboxTargets();
        var tree = TreeGit();
        return new SandboxTreeSync(resolved, tree, new SandboxTreeSpread(tree), GitOps(),
            Checkpointer(resolved, scanner), NullLogger<SandboxTreeSync>.Instance);
    }
}
