using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// p0261/p0318/p0391a: the per-trigger configuration rules, expressed as findings.
/// The trigger decision rests on the NATIVE status (trigger_statuses), so two
/// misconfigurations re-claim a just-processed ticket forever: a terminal status that is
/// ALSO a trigger_status (blocking — that trigger loops), and empty trigger_statuses
/// (advisory — every native status is accepted, which is the base-config default).
/// Each finding names the project, the trigger and the field, so the blast radius of a
/// broken trigger is that trigger.
/// </summary>
public static class ProjectTriggerRules
{
    public static IEnumerable<StartupFinding> Evaluate(string projectName, RawProjectEntry project)
    {
        foreach (var (kind, trigger) in EnumerateTriggers(project))
        {
            if (trigger is null) continue;
            var park = ClarificationParkStatusRule.FindingForParkingPresetWithoutStatus(
                projectName, kind, project, trigger);
            if (park is not null) yield return park;

            if (trigger.TriggerStatuses.Count == 0)
            {
                yield return EmptyTriggerStatuses(projectName, kind);
                continue;
            }
            foreach (var finding in TerminalStatusFindings(projectName, kind, trigger))
                yield return finding;
        }
    }

    private static IEnumerable<(string Kind, WebhookTriggerConfig? Trigger)> EnumerateTriggers(
        RawProjectEntry p)
    {
        yield return (TriggerKinds.Jira, p.JiraTrigger);
        yield return (TriggerKinds.GitHub, p.GithubTrigger);
        yield return (TriggerKinds.GitLab, p.GitlabTrigger);
        yield return (TriggerKinds.AzureDevOps, p.AzuredevopsTrigger);
    }

    private static IEnumerable<StartupFinding> TerminalStatusFindings(
        string projectName, string kind, WebhookTriggerConfig trigger)
    {
        (string Field, string? Status)[] terminals =
        [
            ("done_status", trigger.DoneStatus),
            ("failed_status", trigger.FailedStatus),
            (ClarificationParkStatusRule.Field, trigger.NeedsClarificationStatus),
            // p0390: a verdict park inside trigger_statuses would be re-claimed on the next
            // poll — the exact unbounded loop the clarification park was fixed for.
            ("not_implementable_status", trigger.NotImplementableStatus),
        ];
        foreach (var (field, status) in terminals)
        {
            if (string.IsNullOrEmpty(status)) continue;
            if (!trigger.TriggerStatuses.Contains(status, StringComparer.OrdinalIgnoreCase)) continue;
            yield return TerminalIsTrigger(projectName, kind, field, status);
        }
    }

    private static StartupFinding TerminalIsTrigger(
        string projectName, string kind, string field, string status) =>
        new(StartupSubsystems.Configuration,
            StartupFindingSeverity.Blocking,
            $"Project '{projectName}' {kind}: {field} '{status}' is also a trigger_status. "
            + "A processed ticket would land back in a trigger status and be re-claimed "
            + $"immediately (loop). This trigger is disabled until {field} names a status "
            + "OUTSIDE trigger_statuses.",
            projectName, kind, field);

    private static StartupFinding EmptyTriggerStatuses(string projectName, string kind) =>
        new(StartupSubsystems.Configuration,
            StartupFindingSeverity.Advisory,
            $"Project '{projectName}' {kind}: trigger_statuses is empty — every native status is "
            + "accepted, so a ticket terminalized to done_status/failed_status can be re-claimed "
            + "immediately (p0261). Set trigger_statuses to the open states only.",
            projectName, kind, "trigger_statuses");
}
