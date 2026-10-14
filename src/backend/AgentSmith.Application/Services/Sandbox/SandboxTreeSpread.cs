using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// The last leg of the sandbox tree sync: after the primary committed the union, every
/// secondary receives the changes it lacks, must then write exactly the primary's tree —
/// the only mechanical statement that two clones hold the same files — and commits it
/// locally. It is never pushed; the primary carries the branch.
/// </summary>
public sealed class SandboxTreeSpread(SandboxTreeGit git)
{
    public const string CommitMessage = "agent-smith tree sync";

    /// <summary>Levels every secondary of <paramref name="plan"/>; throws naming the sandbox that is not.</summary>
    public async Task SpreadAsync(Plan plan, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var union = await git.HeadTreeAsync(plan.Primary.Key, plan.Primary.Value, ct);
        foreach (var (key, sandbox) in plan.Sandboxes.Skip(1))
        {
            foreach (var patch in plan.Spread.Where(p => p.Receiver == key))
                if (await git.ApplyAsync(sandbox, patch.Encoded, checkOnly: false, ct) is { } failed)
                    throw new InvalidOperationException($"{key}: the change from {patch.Sender} did not apply: {failed}");
            var tree = await git.StageAndWriteTreeAsync(key, sandbox, ct);
            if (tree != union)
                throw new InvalidOperationException(
                    $"{key}: after the spread it holds tree {tree}, not the union tree {union} of {plan.Primary.Key}");
            await git.SyncCommitAsync(key, sandbox, CommitMessage, ct);
        }
    }

    /// <summary>One repository's sync, read before anything was applied.</summary>
    public sealed record Plan(
        string Repository, IReadOnlyList<KeyValuePair<string, ISandbox>> Sandboxes, bool NeedsPush,
        IReadOnlyList<Patch> Fold, IReadOnlyList<Patch> Spread)
    {
        public KeyValuePair<string, ISandbox> Primary => Sandboxes[0];
    }

    /// <summary>One sender's change for one receiver, base64-encoded.</summary>
    public sealed record Patch(string Sender, string Receiver, string Encoded);
}
