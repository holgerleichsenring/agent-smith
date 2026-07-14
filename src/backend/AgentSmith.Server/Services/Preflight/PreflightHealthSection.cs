using AgentSmith.Contracts.Models.Preflight;

namespace AgentSmith.Server.Services.Preflight;

/// <summary>
/// p0324: shapes the startup preflight report into the /health JSON body — status
/// "pending" until the warn-only startup run finished, then pass/fail with the
/// failed checks (message + fix hint) inlined so an operator curling /health sees
/// what to do without reading logs. Pure mapping.
/// </summary>
internal static class PreflightHealthSection
{
    public static object From(PreflightReportStore? store)
    {
        var report = store?.Current;
        if (report is null)
            return new { status = "pending" };

        return new
        {
            status = report.HasFailures ? "fail" : "pass",
            completed_at_utc = store!.CompletedAtUtc?.ToString("O"),
            passed = report.PassedCount,
            failed = report.FailedCount,
            skipped = report.SkippedCount,
            failures = report.Outcomes
                .Where(o => o.Result.Status == PreflightStatus.Fail)
                .Select(o => new
                {
                    name = o.Name,
                    message = o.Result.Message,
                    fix_hint = o.Result.FixHint,
                })
                .ToArray(),
        };
    }
}
