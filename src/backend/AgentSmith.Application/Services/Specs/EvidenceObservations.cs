using System.Globalization;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-02-3f06b: an observation is real evidence a check cannot open — a log, a scan, a
/// tarball read by hand. The date makes it a record of WHEN, not a free pass, so it must carry
/// one, and every date-shaped part of it must be a real date.
/// </summary>
internal static class EvidenceObservations
{
    public static EvidenceProblem? DateProblem(string observation)
    {
        var dates = EvidenceGrammar.DateShaped().Matches(observation).Select(m => m.Value).ToList();
        if (dates.Count == 0) return new EvidenceProblem("an observation states no date (yyyy-MM-dd)");
        return dates.FirstOrDefault(d => !IsDate(d)) is { } invalid
            ? new EvidenceProblem($"'{invalid}' is not a date")
            : null;
    }

    private static bool IsDate(string text) =>
        DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
}
