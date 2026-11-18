using System.Text;
using AgentSmith.Application.Services.Filing;
using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-17-0207d: the failures of a series check not followed up before, as the one follow-up the run's
/// filing steps file into the series — a work ticket naming each failed criterion and what showed it.</summary>
public static class SeriesFollowUp
{
    public static IReadOnlyList<SeriesCriterionResult> New(SeriesRunRecord record, SeriesVerdict verdict) =>
        [.. verdict.Failed.Where(f => !record.FollowedUp.Contains(f.Criterion, StringComparer.Ordinal))];

    public static FilingRequest Request(SeriesRunRecord record, IReadOnlyList<SeriesCriterionResult> failed)
    {
        ArgumentNullException.ThrowIfNull(record);
        var body = new StringBuilder($"The series check on the default branch found {failed.Count} done criteria of series ")
            .Append(record.Id).Append(" that do not hold. Make each hold, with a test that shows it.\n");
        foreach (var f in failed) body.Append("\n- ").Append(f.Criterion).Append("\n  Found: ").Append(f.Evidence);
        var title = failed.Count == 1 ? $"Series check: {Short(failed[0].Criterion)}" : $"Series check: {failed.Count} done criteria fail on main";
        return new FilingRequest(record.Conversation, record.Principal, [], [], null, record.MayStartRuns)
        {
            FollowUp = new FilingFollowUp(title, body.ToString()),
        };
    }

    private static string Short(string criterion) => criterion[(criterion.IndexOf(": ", StringComparison.Ordinal) + 2)..];
}
