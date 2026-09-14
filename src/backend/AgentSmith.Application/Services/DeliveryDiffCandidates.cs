namespace AgentSmith.Application.Services;

/// <summary>
/// 2026-09-13-5cdf: the ordered comparisons a delivery diff tries, closest to "what this
/// branch delivers" first.
/// <para>
/// Extracted from <see cref="DeliveryDiff"/> rather than squeezed into it: that file sat
/// at 115 lines against an unbaselined ceiling of 120, and a file that has never had a
/// baseline row is not allowed to get one. Choosing WHAT to compare against and RUNNING
/// the comparison are different jobs anyway — this one is a pure function over two names
/// and needs no sandbox.
/// </para>
/// </summary>
internal static class DeliveryDiffCandidates
{
    public static IEnumerable<(string[] Args, string Description)> Ordered(
        string? baseBranch, string? runStart)
    {
        if (!string.IsNullOrWhiteSpace(baseBranch))
        {
            yield return ([$"origin/{baseBranch}"], $"against origin/{baseBranch}");
            yield return ([baseBranch], $"against {baseBranch}");
        }
        // Everything this run put on the branch, committed and uncommitted alike — the
        // answer when the branch and the base are the same ref.
        if (!string.IsNullOrWhiteSpace(runStart))
            yield return ([runStart], $"against {runStart} (where this run started)");
        // The branch's own first parent: everything committed on it plus the working tree.
        yield return (["HEAD"], "against HEAD (uncommitted work only)");
    }

    /// <summary>
    /// The comparison's ref, or null for the HEAD rung — HEAD is the branch, so a search of
    /// it would search the delivery under the name of the thing it is compared against.
    /// </summary>
    public static string? BaseRefOf(string[] against) =>
        against is ["HEAD"] ? null : against[0];
}
