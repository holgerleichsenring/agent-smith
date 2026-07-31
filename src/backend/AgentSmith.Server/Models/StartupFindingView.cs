using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Server.Models;

/// <summary>
/// p0391a: one finding as the findings endpoint renders it — the unit, the field, the
/// severity as a word rather than an enum ordinal, and the operator-language reason.
/// </summary>
public sealed record StartupFindingView(
    string Subsystem,
    string Severity,
    string Reason,
    string? Project,
    string? Trigger,
    string? Field)
{
    public static StartupFindingView From(StartupFinding finding) => new(
        finding.Subsystem,
        finding.Severity.ToString().ToLowerInvariant(),
        finding.Reason,
        finding.Project,
        finding.Trigger,
        finding.Field);
}
