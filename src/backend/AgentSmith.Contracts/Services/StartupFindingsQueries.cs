using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0391a: the questions boot and dispatch ask the findings list. A blocking finding
/// disables the narrowest unit that carries it, so the callers need "is THIS trigger
/// blocked", not "is anything wrong".
/// </summary>
public static class StartupFindingsQueries
{
    /// <summary>
    /// True when a blocking finding names this project trigger — or the project as a whole,
    /// which is the wider unit and therefore takes its triggers with it.
    /// </summary>
    public static bool IsTriggerBlocked(
        this IStartupFindings findings, string project, string triggerKind) =>
        findings.BlockingReason(project, triggerKind) is not null;

    /// <summary>The reason of the first blocking finding on this project trigger, if any.</summary>
    public static string? BlockingReason(
        this IStartupFindings findings, string project, string triggerKind) =>
        findings.All.FirstOrDefault(f => f.IsBlocking
            && string.Equals(f.Project, project, StringComparison.OrdinalIgnoreCase)
            && (f.Trigger is null
                || string.Equals(f.Trigger, triggerKind, StringComparison.OrdinalIgnoreCase)))?.Reason;

    public static IReadOnlyList<StartupFinding> Blocking(this IStartupFindings findings) =>
        findings.All.Where(f => f.IsBlocking).ToList();
}
