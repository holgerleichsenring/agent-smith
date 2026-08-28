using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0474: what a second answer is allowed to change.
/// <para>
/// A citation that resolves against nothing is a FORMAT failure far more often than a false
/// claim — three live runs died on it with the work finished and only the bookkeeping
/// refused. The deriver has had its objection and a second attempt since p0422; the account
/// gets one too. The second answer may only turn an unresolved row into a satisfied one:
/// the re-ask carries the objection, never the verdict, so a model that still cannot name
/// real evidence fails, and fails twice.
/// </para>
/// <para>
/// p0484: its own type because the resolver it judges against is now taken AFTER the
/// re-ask, which runs searches of its own — merging two answers and deciding what a citation
/// is worth are two reasons to change, and the accountant sits at the file-length ceiling.
/// </para>
/// </summary>
internal static class AccountSecondPass
{
    internal static IReadOnlyList<CriterionAccount> Merge(
        IReadOnlyList<CriterionAccount> first,
        IReadOnlyList<CriterionAccount> unresolved,
        IReadOnlyList<AccountRow>? second,
        string repoKey,
        AccountRowResolution reader,
        CitationResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(unresolved);
        ArgumentNullException.ThrowIfNull(reader);
        if (second is null) return first;

        var corrected = reader.Resolve(
            repoKey, [.. unresolved.Select(u => u.Criterion)], second, resolver);
        // 2026-08-25-9749: the correction may only RAISE a disposition, by the same ranking
        // the window merge uses. Reading it as "satisfied wins" would have made the second
        // answer unable to say what the first one could.
        return [.. first.Select(row =>
            corrected.FirstOrDefault(c => string.Equals(
                c.Criterion, row.Criterion, StringComparison.OrdinalIgnoreCase)) is { } fixedRow
            && fixedRow.Disposition > row.Disposition
            ? fixedRow
            : row)];
    }
}
