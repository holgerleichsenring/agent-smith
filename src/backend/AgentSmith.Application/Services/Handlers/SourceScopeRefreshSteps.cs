using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-09-22-2d11b: the three steps a HELD source scope's work path takes instead of a
/// clone — who this tree is a clone of, what the remote calls HEAD now, and the tree made
/// identical to it.
/// <para>
/// The ref is the remote's own HEAD and not a configured default branch: the scope's clone
/// (<see cref="CheckoutStepFactory.BuildScopeCloneStep"/>) names no branch, so the tree sits
/// on whatever HEAD the remote advertised, while a repository's configured default branch is
/// nullable and read elsewhere with a fallback to "main" — resetting to that name could move
/// the tree to a branch the clone never had.
/// </para>
/// </summary>
internal static class SourceScopeRefreshSteps
{
    private const int FetchTimeoutSeconds = 300;
    private const int LocalTimeoutSeconds = 60;

    /// <summary>
    /// What remote this work path is a clone OF. A non-zero exit is an empty work path or no
    /// repository at all — the ordinary answer on a sandbox that has just spawned.
    /// </summary>
    public static Step BuildRemoteUrlStep() =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "git",
            Args: ["config", "--get", "remote.origin.url"],
            WorkingDirectory: Repository.SandboxWorkPath,
            TimeoutSeconds: LocalTimeoutSeconds);

    /// <summary>
    /// The remote's own HEAD, one commit deep. It carries the credential for the reason the
    /// clone does: it talks to the remote.
    /// </summary>
    public static Step BuildFetchHeadAtDepthStep(RepoConnection config) =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "git",
            Args: ["-c", GitStepCredentials.Helper, "fetch", "--depth", "1", "origin", "HEAD"],
            WorkingDirectory: Repository.SandboxWorkPath,
            Env: GitStepCredentials.TokenEnv(config),
            TimeoutSeconds: FetchTimeoutSeconds);

    /// <summary>
    /// The tree made IDENTICAL to what came back. It is a reset and not a pull, because a
    /// pull merges and a merge can conflict and leave a tree a read tool would report as
    /// source. The tree is disposable; the honest act is never to reconcile.
    /// </summary>
    public static Step BuildResetHardStep(string reference) =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "git",
            Args: ["reset", "--hard", reference],
            WorkingDirectory: Repository.SandboxWorkPath,
            TimeoutSeconds: LocalTimeoutSeconds);
}
