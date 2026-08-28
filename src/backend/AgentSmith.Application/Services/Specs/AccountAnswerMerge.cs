using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0474: what a LATER answer is allowed to change about the rows already taken.
/// <para>
/// A citation that resolves against nothing is a FORMAT failure far more often than a false
/// claim — three live runs died on it with the work finished and only the bookkeeping
/// refused. The deriver has had its objection and a second attempt since p0422; the account
/// gets one too. The later answer carries the objection, never the verdict, so a model that
/// still cannot name real evidence fails, and fails twice.
/// </para>
/// <para>
/// p0484: its own type because the resolver it judges against is taken AFTER the re-ask,
/// which runs searches of its own — merging two answers and deciding what a citation is worth
/// are two reasons to change, and the accountant sits at the file-length ceiling.
/// </para>
/// <para>
/// 2026-08-25-6f12: named for what it does rather than for which pass calls it, now that two
/// do. The correction and the full-reach pass merge by ONE rule, or the account has two
/// answers to "what may a later answer say" and no way to tell which one it gave.
/// </para>
/// </summary>
internal static class AccountAnswerMerge
{
    internal static IReadOnlyList<CriterionAccount> Of(
        IReadOnlyList<CriterionAccount> taken,
        IReadOnlyList<CriterionAccount> asked,
        IReadOnlyList<AccountRow>? answer,
        string repoKey,
        AccountRowResolution reader,
        CitationResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(taken);
        ArgumentNullException.ThrowIfNull(asked);
        ArgumentNullException.ThrowIfNull(reader);
        if (answer is null) return taken;

        var corrected = reader.Resolve(
            repoKey, [.. asked.Select(u => u.Criterion)], answer, resolver);
        // 2026-08-25-9749: a later answer may only RAISE a disposition, by the same ranking
        // the window merge uses. Reading it as "satisfied wins" would have made the second
        // answer unable to say what the first one could.
        return [.. taken.Select(row =>
            corrected.FirstOrDefault(c => string.Equals(
                c.Criterion, row.Criterion, StringComparison.OrdinalIgnoreCase)) is { } fixedRow
            && fixedRow.Disposition > row.Disposition
            ? fixedRow
            : row)];
    }
}
