using System.Text;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0474: the correction an account gets when it cited something that does not resolve.
/// <para>
/// SpecSetDeriver has had three attempts and a stated objection since p0422; the account
/// had one call and no way back, so every decision about the citation FORM was a single
/// bet settled by a forty minute pipeline. Three of those bets were lost in a row on the
/// same criterion, each time with the work finished and only the bookkeeping refused.
/// </para>
/// <para>
/// It carries the objection and the accepted form, never the verdict. A criterion the
/// account reported as NOT satisfied is never re-asked: that is an answer, not a
/// formatting failure, and asking again would be asking it to change its mind.
/// </para>
/// </summary>
internal static class AccountReAsk
{
    /// <summary>The rows worth asking about: claimed satisfied, cited something, and the
    /// citation resolved against nothing.</summary>
    public static IReadOnlyList<CriterionAccount> Unresolved(
        IReadOnlyList<CriterionAccount> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return [.. rows.Where(r => !r.Satisfied
            && r.Note?.StartsWith("claimed satisfied by", StringComparison.Ordinal) == true)];
    }

    public static string Message(IReadOnlyList<CriterionAccount> unresolved)
    {
        ArgumentNullException.ThrowIfNull(unresolved);
        var sb = new StringBuilder();
        sb.AppendLine("These criteria are claimed satisfied by a citation that resolves against nothing:");
        foreach (var row in unresolved) sb.AppendLine($"- {row.Criterion}: {row.Note}");
        sb.AppendLine();
        sb.AppendLine("A citation element is ONE whole thing: one path exactly as the FILE LIST");
        sb.AppendLine("prints it, or one command copied character for character from between the");
        sb.AppendLine("quotes on its line under COMMANDS. Two commands are two elements; never join");
        sb.AppendLine("them and never cut one apart.");
        sb.AppendLine();
        sb.AppendLine("Answer again with ONLY the JSON array, for these criteria alone. If you cannot");
        sb.AppendLine("name a file or a command that covers one, report it as not satisfied.");
        return sb.ToString();
    }
}
