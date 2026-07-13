using System.Globalization;
using System.Text;
using AgentSmith.Contracts.Models.Preflight;

namespace AgentSmith.Cli.Services.Preflight;

/// <summary>
/// p0324: human-first doctor output — one status-tagged line per check plus the fix
/// hint directly under each failure. Pure formatting (Map-style static).
/// </summary>
internal static class DoctorTextRenderer
{
    public static string Render(PreflightReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("agent-smith doctor — active preflight checks");
        builder.AppendLine();
        foreach (var outcome in report.Outcomes)
            AppendOutcome(builder, outcome);
        builder.AppendLine();
        builder.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"{report.PassedCount} passed, {report.FailedCount} failed, {report.SkippedCount} skipped"));
        return builder.ToString().TrimEnd();
    }

    private static void AppendOutcome(StringBuilder builder, PreflightCheckOutcome outcome)
    {
        var tag = outcome.Result.Status switch
        {
            PreflightStatus.Pass => "[ OK ]",
            PreflightStatus.Fail => "[FAIL]",
            _ => "[SKIP]",
        };
        builder.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"{tag} {outcome.Name,-15} {outcome.Result.Message} ({outcome.DurationMs} ms)"));
        if (outcome.Result.Status == PreflightStatus.Fail && outcome.Result.FixHint is not null)
            builder.AppendLine($"       fix: {outcome.Result.FixHint}");
    }
}
