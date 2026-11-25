using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
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
/// <para>
/// 2026-10-12-f5f7f: <see cref="PushPlainAsync"/> forces nothing and never retries — a member of a
/// multi-repository spec write, and the revert that undoes it, must be refused when somebody else
/// pushed in between, as <c>CreateOnlyBranchPush</c> is.
/// </para>
/// <para>
/// 2026-11-25-098f: inside a run, the head about to be pushed is published first (<see cref="BranchPushedEvent"/>), so the run
/// records it before any read of the host can see it; a pull request's moved head is then known as agent-smith's own.
/// </para>
/// </summary>
public sealed class GitBranchPusher(IEventPublisher events, IRunContextAccessor runContext)
{
    private const int GitTimeoutSeconds = 120;

    public async Task PushAsync(
        ISandbox sandbox, string branch, string credentialHelper,
        GitCredential credential, CancellationToken ct)
    {
        // 2026-10-02-5f89g: the repo's own credential, resolved by the caller that holds the repo.
        var env = credential.Env(); // 2026-11-25-d0e2: a bearer credential carries its header too
        await RecordHeadAsync(sandbox, branch, ct);

        var result = await PushOnceAsync(sandbox, branch, credential.Args(credentialHelper), env, ct);
        if (result.ExitCode != 0 && Mentions(result, "stale info"))
        {
            await Run(sandbox, [.. credential.Args(credentialHelper), "fetch", "origin", branch], env, ct);
            result = await PushOnceAsync(sandbox, branch, credential.Args(credentialHelper), env, ct);
        }

        if (result.ExitCode != 0)
            throw new InvalidOperationException(
                $"git push failed (exit {result.ExitCode}): {result.ErrorMessage}");
    }

    /// <summary>A fast-forward push of HEAD; a moved remote refuses it and nothing is overwritten.</summary>
    public async Task PushPlainAsync(
        ISandbox sandbox, string branch, string credentialHelper, GitCredential credential, CancellationToken ct)
    {
        await RecordHeadAsync(sandbox, branch, ct);
        var result = await Run(sandbox, [.. credential.Args(credentialHelper), "push", "origin", $"HEAD:{branch}"], credential.Env(), ct);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"git push failed (exit {result.ExitCode}): {result.ErrorMessage}");
    }

    // Outside a run nothing records the head; a HEAD git cannot name has nothing to push either.
    private async Task RecordHeadAsync(ISandbox sandbox, string branch, CancellationToken ct)
    {
        if (runContext.CurrentRunId is not { Length: > 0 } runId) return;
        var head = await Run(sandbox, ["rev-parse", "HEAD"], null, ct);
        if (head.ExitCode != 0 || (head.OutputContent ?? string.Empty).Trim() is not { Length: > 0 } sha) return;
        await events.PublishAsync(new BranchPushedEvent(runId, DateTimeOffset.UtcNow, branch, sha), ct);
    }

    private static Task<StepResult> PushOnceAsync(
        ISandbox sandbox, string branch, string[] credentialArgs,
        IReadOnlyDictionary<string, string>? env, CancellationToken ct) =>
        Run(sandbox, [.. credentialArgs, "push", "--force-with-lease", "origin", $"HEAD:{branch}"], env, ct);

    private static Task<StepResult> Run(
        ISandbox sandbox, string[] args, IReadOnlyDictionary<string, string>? env, CancellationToken ct) =>
        sandbox.RunStepAsync(
            new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
                Command: "git", Args: args, Env: env, TimeoutSeconds: GitTimeoutSeconds),
            progress: null, ct);

    private static bool Mentions(StepResult result, string phrase) =>
        (result.ErrorMessage ?? string.Empty).Contains(phrase, StringComparison.OrdinalIgnoreCase)
        || (result.OutputContent ?? string.Empty).Contains(phrase, StringComparison.OrdinalIgnoreCase);
}
