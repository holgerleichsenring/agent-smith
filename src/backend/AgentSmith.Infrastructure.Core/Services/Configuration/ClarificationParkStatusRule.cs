using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Exceptions;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// p0391: a preset that can PARK a run on an operator question must have somewhere to park it.
/// Until now an unset needs_clarification_status degraded silently — the gate posted its
/// question, logged "(not parked)" and ended the run Ok while the ticket kept a trigger status,
/// so discovery re-claimed it and the same run repeated. Safety belongs in the type system, not
/// the log: this is a load-time configuration error.
///
/// It fires ONLY where a park can actually happen: a project that has a tracker trigger AND
/// declares a pipeline whose command list carries a clarification step. A trackerless or
/// scan-only project is untouched. Pure rule with no dependencies — a sibling of
/// <see cref="PipelinePresets"/> and the loop policies, not a service, so the config loader
/// keeps its constructor and every existing caller keeps working unchanged.
/// </summary>
public static class ClarificationParkStatusRule
{
    public static void FailIfParkingPresetHasNoStatus(
        string projectName, string triggerKind, RawProjectEntry project, WebhookTriggerConfig trigger)
    {
        if (!string.IsNullOrWhiteSpace(trigger.NeedsClarificationStatus)) return;
        var parking = DeclaredPipelines(project, trigger)
            .FirstOrDefault(PipelinePresets.ParksOpenQuestions);
        if (parking is null) return;

        throw new ConfigurationException(
            $"Project '{projectName}' {triggerKind}: pipeline '{parking}' can park a run on an "
            + "operator question, but needs_clarification_status is not set (neither on the "
            + "tracker nor on this trigger). A parked run posts its question and ends while the "
            + "ticket keeps a trigger status, so discovery re-claims it and the run repeats. "
            + "Set needs_clarification_status to a status OUTSIDE trigger_statuses.");
    }

    // Every pipeline this trigger can reach: the declared list (the legacy single `pipeline:`
    // is folded into it before validation), the project default, and every label route.
    private static IEnumerable<string> DeclaredPipelines(
        RawProjectEntry project, WebhookTriggerConfig trigger)
    {
        foreach (var entry in project.Pipelines)
            if (!string.IsNullOrWhiteSpace(entry.Name)) yield return entry.Name;
        if (!string.IsNullOrWhiteSpace(project.DefaultPipeline)) yield return project.DefaultPipeline;
        if (trigger.PipelineFromLabel is null) yield break;
        foreach (var pipeline in trigger.PipelineFromLabel.Values)
            if (!string.IsNullOrWhiteSpace(pipeline)) yield return pipeline;
    }
}
