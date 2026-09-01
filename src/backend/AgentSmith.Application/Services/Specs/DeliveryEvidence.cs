using System.Text;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-08-25-0eae: what the account is given to judge — every repository's delivery diff,
/// and the ref each one was taken against.
/// <para>
/// ONE account for the phase, over every repository at once. Per-repo accounting asked each
/// repository to satisfy criteria written about ANOTHER — a two-repo ticket whose criteria
/// name the API service made the worker repository outstanding on every one of them, which
/// is a false negative by construction. The criteria belong to the PHASE; the repositories
/// are where its work lands.
/// </para>
/// <para>
/// The base ref travels WITH the diff because they have to agree: resolving it a second time
/// could hand the account's search a base the diff it is reading was never compared to.
/// Separated from <see cref="PhaseAccounting"/> because gathering evidence and deciding what
/// an account is worth are different jobs, and holding both put the type past the length the
/// architecture rule allows.
/// </para>
/// </summary>
internal static class DeliveryEvidence
{
    internal sealed record Gathered(
        string Diff,
        IReadOnlyDictionary<string, string?> BaseRefs,
        IReadOnlyList<string> Failures);

    public static async Task<Gathered> GatherAsync(
        DeliveryDiff deliveryDiff,
        IReadOnlyDictionary<string, ISandbox> sandboxes,
        string? runId,
        CancellationToken cancellationToken)
    {
        var combined = new StringBuilder();
        var failures = new List<string>();
        var baseRefs = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (var (key, sandbox) in sandboxes)
        {
            var diff = await deliveryDiff.ForBranchAsync(sandbox, runId, cancellationToken);
            if (diff.Failed)
            {
                failures.Add($"{key} ({diff.Basis})");
                continue;
            }
            baseRefs[key] = diff.BaseRef;
            combined.Append("# repository: ").Append(key).Append('\n')
                .Append(diff.Text).Append('\n');
        }

        return new Gathered(combined.ToString(), baseRefs, failures);
    }
}
