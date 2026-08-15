using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services;

/// <summary>
/// p0422: pushes the run's branch, and refreshes a lease that can no longer be satisfied.
/// <para>
/// <c>--force-with-lease</c> refuses when our remote-tracking ref no longer describes the
/// remote — "stale info". That is right when someone else has pushed, and pointless when
/// the branch was deleted or rewritten since this working copy last fetched: the lease
/// then protects a state that does not exist, and the commit is simply lost. Run 16 lost
/// its spec that way. One fetch, one retry; a genuinely contested branch still fails.
/// </para>
/// </summary>
public sealed class GitBranchPusher
{
    private const int GitTimeoutSeconds = 120;

    public async Task PushAsync(
        ISandbox sandbox, string branch, string credentialHelper,
        RepoType repoType, CancellationToken ct)
    {
        var token = GitTokenResolver.Resolve(repoType);
        var env = token is null
            ? null
            : (IReadOnlyDictionary<string, string>)new Dictionary<string, string> { ["GIT_TOKEN"] = token };

        var result = await PushOnceAsync(sandbox, branch, credentialHelper, env, ct);
        if (result.ExitCode != 0 && Mentions(result, "stale info"))
        {
            await Run(sandbox, ["-c", credentialHelper, "fetch", "origin", branch], env, ct);
            result = await PushOnceAsync(sandbox, branch, credentialHelper, env, ct);
        }

        if (result.ExitCode != 0)
            throw new InvalidOperationException(
                $"git push failed (exit {result.ExitCode}): {result.ErrorMessage}");
    }

    private static Task<StepResult> PushOnceAsync(
        ISandbox sandbox, string branch, string credentialHelper,
        IReadOnlyDictionary<string, string>? env, CancellationToken ct) =>
        Run(sandbox, ["-c", credentialHelper, "push", "--force-with-lease", "origin", $"HEAD:{branch}"], env, ct);

    private static Task<StepResult> Run(
        ISandbox sandbox, string[] args, IReadOnlyDictionary<string, string>? env, CancellationToken ct) =>
        sandbox.RunStepAsync(
            new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
                Command: "git", Args: args, WorkingDirectory: Repository.SandboxWorkPath,
                Env: env, TimeoutSeconds: GitTimeoutSeconds),
            progress: null, ct);

    private static bool Mentions(StepResult result, string phrase) =>
        (result.ErrorMessage ?? string.Empty).Contains(phrase, StringComparison.OrdinalIgnoreCase)
        || (result.OutputContent ?? string.Empty).Contains(phrase, StringComparison.OrdinalIgnoreCase);
}
