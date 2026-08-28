using AgentSmith.Application.Services.Specs;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// 2026-08-28-c310: refuses to score an account that was offered fewer search tools than a
/// run offers it.
/// <para>
/// A missing tool fails nothing by itself — it lowers a score, and a lowered score is
/// indistinguishable from a worse account. The harness lost <c>search_base</c> exactly that
/// way while the corpus carried a fixture that turns on the base, so the number it would
/// have produced described a component that never runs.
/// </para>
/// <para>
/// The offered list is read from production's own <see cref="AccountTools"/>, so this
/// compares the account under test against the account rather than against a second copy of
/// its rules.
/// </para>
/// </summary>
internal static class AccountToolParity
{
    private const string SearchBranch = "search_branch";
    private const string SearchBase = "search_base";

    /// <summary>Throws when the tools built for <paramref name="search"/> are not the tools a
    /// run builds for the same gathered evidence.</summary>
    public static void Verify(IReadOnlyDictionary<string, string?> baseRefs, BranchSearch search)
    {
        ArgumentNullException.ThrowIfNull(baseRefs);
        ArgumentNullException.ThrowIfNull(search);

        var expected = Expected(baseRefs);
        var offered = AccountTools.For(search)?.Select(tool => tool.Name).ToList() ?? [];
        var missing = expected.Except(offered, StringComparer.Ordinal).ToList();
        if (missing.Count == 0) return;

        throw new InvalidOperationException(
            $"The account under test was offered [{string.Join(", ", offered)}] where a run "
            + $"offers it [{string.Join(", ", expected)}] — missing "
            + $"[{string.Join(", ", missing)}]. Scoring here would measure a crippled account "
            + "and report the number as the account's.");
    }

    /// <summary>What a run offers for this evidence: the branch always, the base wherever the
    /// delivery diff resolved a real ref.</summary>
    private static IReadOnlyList<string> Expected(IReadOnlyDictionary<string, string?> baseRefs) =>
        baseRefs.Any(reference => reference.Value is not null)
            ? [SearchBranch, SearchBase]
            : [SearchBranch];
}
