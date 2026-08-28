using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0474: turns an answer into resolved rows, one per criterion, and says so when a
/// citation resolved against nothing. Split from <see cref="SpecAccountant"/>, which now
/// also orchestrates a second attempt: reading one answer and deciding whether to ask
/// again are two reasons to change.
/// </summary>
internal sealed class AccountRowResolution(ILogger logger)
{
    public List<CriterionAccount> Resolve(
        string repoKey, IReadOnlyList<string> criteria,
        IReadOnlyList<AccountRow> answer, CitationResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentNullException.ThrowIfNull(answer);
        ArgumentNullException.ThrowIfNull(resolver);
        var rows = new List<CriterionAccount>();
        foreach (var criterion in criteria)
        {
            var row = answer.FirstOrDefault(r =>
                string.Equals(r.Criterion, criterion, StringComparison.OrdinalIgnoreCase))
                ?? new AccountRow(
                    criterion, AccountDisposition.NotSatisfied, null,
                    "the account did not address this criterion");
            rows.Add(Report(repoKey, resolver.Resolve(row)));
        }
        return rows;
    }

    /// <summary>
    /// What the log has to carry: a citation that resolved against nothing, and — since
    /// 2026-08-25-9749 — a criterion the account DECLINED to judge, with the antecedent it
    /// declared false. A disposition that never appears in the log is one nobody can audit
    /// after the run.
    /// </summary>
    private CriterionAccount Report(string repoKey, CriterionAccount resolved)
    {
        if (resolved.IsOutstanding && resolved.Note?.Contains("neither", StringComparison.Ordinal) == true)
            logger.LogWarning(
                "{Repo}: {Criterion} — {Note}", repoKey, Shorten(resolved.Criterion), resolved.Note);
        if (resolved.IsNotApplicable)
            logger.LogInformation(
                "{Repo}: {Criterion} — NOT APPLICABLE, the base does not contain {Antecedent} ({Citation})",
                repoKey, Shorten(resolved.Criterion), resolved.Antecedent, resolved.Citation);
        return resolved;
    }

    private static string Shorten(string text) =>
        text.Length <= 60 ? text : text[..60] + "…";
}
