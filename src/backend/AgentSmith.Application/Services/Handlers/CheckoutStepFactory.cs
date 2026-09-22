using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Builds sandbox-side git Steps for the checkout flow. Each step runs inside
/// the per-repo sandbox where /work is the repo root (p0158e), so the workdir
/// is always /work — no per-call target directory parameter.
/// </summary>
internal static class CheckoutStepFactory
{
    private const int CloneTimeoutSeconds = 300;
    private const int CheckoutTimeoutSeconds = 60;

    private const string CredHelper =
        "credential.helper=!f() { echo \"username=x-access-token\"; echo \"password=$GIT_TOKEN\"; }; f";

    private static IReadOnlyDictionary<string, string>? TokenEnv(RepoConnection config)
    {
        var token = GitTokenResolver.Resolve(config.Type);
        return token is null
            ? null
            : new Dictionary<string, string> { ["GIT_TOKEN"] = token };
    }

    /// <summary>
    /// The RUN's clone: the whole history, because a run diffs against a base, reads its own
    /// log and can be handed a revision reachable from nothing.
    /// </summary>
    public static Step BuildCloneStep(RepoConnection config) => Clone(config, []);

    /// <summary>
    /// 2026-09-22-b41d: the READ-ONLY source scope's clone — one branch at one commit. A
    /// scope serves four reads (a file, a directory, a tree and a search) and refuses every
    /// other step kind, so nothing that addresses one can ask for history; a design
    /// conversation paid a whole history transfer per turn for what it never read.
    /// </summary>
    public static Step BuildScopeCloneStep(RepoConnection config) =>
        Clone(config, ["--depth", "1", "--single-branch"]);

    private static Step Clone(RepoConnection config, string[] narrowing) =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "git",
            Args: ["-c", CredHelper, "clone", .. narrowing, config.Url!, "."],
            WorkingDirectory: Repository.SandboxWorkPath,
            Env: TokenEnv(config),
            TimeoutSeconds: CloneTimeoutSeconds);

    /// <summary>
    /// 2026-09-13-9802: asks the host for one revision by name, when a checkout has already
    /// failed. Against the run's full clone that is a sha reachable from no branch and no
    /// tag; against a scope's shallow clone (2026-09-22-b41d) it is the ordinary case, and
    /// the depth rung that follows runs when this one lands nothing. It carries the
    /// credential the plain checkout does not, because it talks to the remote.
    /// </summary>
    public static Step BuildFetchRevisionStep(RepoConnection config, string revision) =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "git",
            Args: new[] { "-c", CredHelper, "fetch", "origin", revision },
            WorkingDirectory: Repository.SandboxWorkPath,
            Env: TokenEnv(config),
            TimeoutSeconds: CloneTimeoutSeconds);

    /// <summary>
    /// 2026-09-22-b41d: asks the host for one revision WITH depth, for the shallow clone a
    /// read-only scope materialises with. A plain fetch into a shallow tree asks for that
    /// revision's whole history, and a host may refuse an unadvertised object outright — so
    /// this is the rung that runs when the fetch by name could not land the revision, and
    /// the tree lands on FETCH_HEAD because a single-branch clone tracks no other remote ref.
    /// </summary>
    public static Step BuildFetchRevisionAtDepthStep(RepoConnection config, string revision) =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "git",
            Args: ["-c", CredHelper, "fetch", "--depth", "1", "origin", revision],
            WorkingDirectory: Repository.SandboxWorkPath,
            Env: TokenEnv(config),
            TimeoutSeconds: CloneTimeoutSeconds);

    /// <summary>2026-09-13-9802: what the tree is actually on, asked of the clone.</summary>
    public static Step BuildResolveHeadStep() =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "git",
            Args: new[] { "rev-parse", "HEAD" },
            WorkingDirectory: Repository.SandboxWorkPath,
            TimeoutSeconds: CheckoutTimeoutSeconds);

    /// <summary>
    /// 2026-09-13-35a4: creates a branch on the remote at a given ref — NO force and NO
    /// lease, so a ref that already exists is refused rather than overwritten. It carries
    /// the credential for the same reason the clone does: it talks to the remote.
    /// </summary>
    public static Step BuildCreateRemoteBranchStep(RepoConnection config, string atRef, string branch) =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "git",
            Args: new[] { "-c", CredHelper, "push", "origin", $"{atRef}:refs/heads/{branch}" },
            WorkingDirectory: Repository.SandboxWorkPath,
            Env: TokenEnv(config),
            TimeoutSeconds: CloneTimeoutSeconds);

    public static Step BuildCheckoutStep(string branch) =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "git",
            Args: new[] { "checkout", branch },
            WorkingDirectory: Repository.SandboxWorkPath,
            TimeoutSeconds: CheckoutTimeoutSeconds);

    public static Step BuildCreateBranchStep(string branch) =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "git",
            Args: new[] { "checkout", "-b", branch },
            WorkingDirectory: Repository.SandboxWorkPath,
            TimeoutSeconds: CheckoutTimeoutSeconds);
}
