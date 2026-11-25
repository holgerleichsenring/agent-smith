using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Builds sandbox-side git Steps for the checkout flow. Every step but the clone names no working
/// directory: it runs at its sandbox's repository root (2026-11-25-9992c — /work/&lt;repo&gt; for a
/// run's repository, /work for a single-repo sandbox). The clone lands in the root, which
/// <see cref="BuildMakeRootStep"/> creates first. The credential the
/// remote-facing ones carry is <see cref="GitStepCredentials"/>'. 2026-10-02-5f89g: it is the
/// repo's own, resolved once by the caller and passed in — these builders stay pure.
/// </summary>
internal static class CheckoutStepFactory
{
    private const int CloneTimeoutSeconds = 300;
    private const int CheckoutTimeoutSeconds = 60;

    /// <summary>
    /// The RUN's clone: the whole history, because a run diffs against a base, reads its own
    /// log and can be handed a revision reachable from nothing.
    /// </summary>
    public static Step BuildCloneStep(RepoConnection config, GitCredential credential, string root) =>
        Clone(config, credential, [], root);

    /// <summary>
    /// 2026-09-22-b41d: the READ-ONLY source scope's clone — one branch at one commit. A
    /// scope serves four reads (a file, a directory, a tree and a search) and refuses every
    /// other step kind, so nothing that addresses one can ask for history; a design
    /// conversation paid a whole history transfer per turn for what it never read.
    /// </summary>
    public static Step BuildScopeCloneStep(RepoConnection config, GitCredential credential, string root) =>
        Clone(config, credential, ["--depth", "1", "--single-branch"], root);

    /// <summary>2026-11-23-933dc: a PoC folder write's clone — no checkout, sparse, no blobs until read, one branch.</summary>
    public static Step BuildSparseCloneStep(RepoConnection config, GitCredential credential, string? branch, string root) =>
        Clone(config, credential, ["--no-checkout", "--sparse", "--filter=blob:none", "--single-branch", .. branch is null ? [] : new[] { "--branch", branch }], root);

    private static Step Clone(RepoConnection config, GitCredential credential, string[] narrowing, string root) =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "git", Args: ["-c", GitStepCredentials.Helper, "clone", .. narrowing, config.Url!, "."],
            WorkingDirectory: root,
            Env: GitStepCredentials.TokenEnv(credential),
            TimeoutSeconds: CloneTimeoutSeconds);

    /// <summary>2026-11-25-9992c: makes a run repository's root, which its clone lands IN, so a backend serving the
    /// root from elsewhere (an in-process host mount) clones where later steps look. Null for the workspace root.
    /// The argument is relative: a backend translates a working directory, never an argument.</summary>
    public static Step? BuildMakeRootStep(string root)
    {
        var trimmed = root.TrimEnd('/');
        var slash = trimmed.LastIndexOf('/');
        if (trimmed == Repository.WorkspaceRoot || slash <= 0) return null;
        return new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "mkdir", Args: ["-p", trimmed[(slash + 1)..]],
            WorkingDirectory: trimmed[..slash], TimeoutSeconds: CheckoutTimeoutSeconds);
    }

    /// <summary>
    /// 2026-09-13-9802: asks the host for one revision by name, when a checkout has already failed. Against the run's full clone that is a sha reachable from no branch and no
    /// tag; against a scope's shallow clone (2026-09-22-b41d) it is the ordinary case, and
    /// the depth rung that follows runs when this one lands nothing. It carries the
    /// credential the plain checkout does not, because it talks to the remote.
    /// </summary>
    public static Step BuildFetchRevisionStep(GitCredential credential, string revision) =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "git", Args: new[] { "-c", GitStepCredentials.Helper, "fetch", "origin", revision },
            Env: GitStepCredentials.TokenEnv(credential),
            TimeoutSeconds: CloneTimeoutSeconds);

    /// <summary>
    /// 2026-09-22-b41d: asks the host for one revision WITH depth, for the shallow clone a
    /// read-only scope materialises with. A plain fetch into a shallow tree asks for that
    /// revision's whole history, and a host may refuse an unadvertised object outright — so
    /// this is the rung that runs when the fetch by name could not land the revision, and
    /// the tree lands on FETCH_HEAD because a single-branch clone tracks no other remote ref.
    /// </summary>
    public static Step BuildFetchRevisionAtDepthStep(GitCredential credential, string revision) =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "git", Args: ["-c", GitStepCredentials.Helper, "fetch", "--depth", "1", "origin", revision],
            Env: GitStepCredentials.TokenEnv(credential),
            TimeoutSeconds: CloneTimeoutSeconds);

    /// <summary>2026-11-17-d5cc: whether /work already is a checkout's root — a local repo's bind mount — or
    /// still empty. The prefix is empty only at a work tree's root, so a work dir that merely sits inside
    /// some other repository is not mistaken for the checkout.</summary>
    public static Step BuildIsWorkTreeStep() =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "git", Args: new[] { "rev-parse", "--show-prefix" },
            TimeoutSeconds: CheckoutTimeoutSeconds);

    /// <summary>2026-09-13-9802: what the tree is actually on, asked of the clone.</summary>
    public static Step BuildResolveHeadStep() =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "git", Args: new[] { "rev-parse", "HEAD" },
            TimeoutSeconds: CheckoutTimeoutSeconds);

    /// <summary>
    /// 2026-09-13-35a4: creates a branch on the remote at a given ref — NO force and NO
    /// lease, so a ref that already exists is refused rather than overwritten. It carries
    /// the credential for the same reason the clone does: it talks to the remote.
    /// </summary>
    public static Step BuildCreateRemoteBranchStep(GitCredential credential, string atRef, string branch) =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "git", Args: new[] { "-c", GitStepCredentials.Helper, "push", "origin", $"{atRef}:refs/heads/{branch}" },
            Env: GitStepCredentials.TokenEnv(credential),
            TimeoutSeconds: CloneTimeoutSeconds);

    public static Step BuildCheckoutStep(string branch) =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "git", Args: new[] { "checkout", branch },
            TimeoutSeconds: CheckoutTimeoutSeconds);

    public static Step BuildCreateBranchStep(string branch) =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "git", Args: new[] { "checkout", "-b", branch },
            TimeoutSeconds: CheckoutTimeoutSeconds);
}
