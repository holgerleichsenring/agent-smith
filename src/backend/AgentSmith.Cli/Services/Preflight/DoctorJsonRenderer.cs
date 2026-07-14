using System.Text.Json;
using AgentSmith.Contracts.Models.Preflight;

namespace AgentSmith.Cli.Services.Preflight;

/// <summary>
/// p0324: machine-readable doctor output for CI gating (`agentsmith doctor --json`).
/// Snake_case keys, stable shape: overall status + exit_code + one entry per check.
/// Pure formatting (Map-style static).
/// </summary>
internal static class DoctorJsonRenderer
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string Render(PreflightReport report)
    {
        var document = new
        {
            status = report.HasFailures ? "fail" : "pass",
            exit_code = report.ExitCode,
            passed = report.PassedCount,
            failed = report.FailedCount,
            skipped = report.SkippedCount,
            checks = report.Outcomes.Select(o => new
            {
                name = o.Name,
                category = o.Category,
                status = o.Result.Status.ToString().ToLowerInvariant(),
                message = o.Result.Message,
                fix_hint = o.Result.FixHint,
                duration_ms = o.DurationMs,
            }).ToArray(),
        };
        return JsonSerializer.Serialize(document, Options);
    }
}
