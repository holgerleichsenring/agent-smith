using AgentSmith.Application.Services.Polling;
using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Tickets;

namespace AgentSmith.Application.Services.Filing;

/// <summary>
/// 2026-11-26-5f6e: the label that triggers a filed ticket's code run when nothing else does. A ticket a list or a spec filed
/// carries the approved-set stamp, which routes it to the code pipeline with its stored set. A ticket filed from a text (the
/// series master's add_ticket, a check's follow-up) has no stored set, so the stamp would stop its run at the filed-ticket
/// gate. Under a pipeline_from_label map such a ticket resolved to no pipeline and was never claimed. It gets the one key that
/// map routes to the code pipeline, read from the project's trigger: the stamp's alternative, never both. A map with no
/// such key is left to the start's own reason. Only such a ticket: a bug or a person's ticket is left to the project's rules.
/// </summary>
public static class FiledWorkTriggerLabel
{
    /// <summary>The key to add, or null when the ticket carries the stamp or a key the map resolves, or the map names none.
    /// 2026-11-26-a12eb: the map is the one routing reads (<see cref="PipelineResolver.MapOf"/>), the installation's
    /// pipeline_triggers included when the trigger has none.</summary>
    public static string? For(ResolvedProject project, IReadOnlyList<string> labels, PipelineTriggerMap? globalTriggers = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(labels);
        var trigger = TriggerSelectionHelper.ByKind(project, project.Tracker.Type.ToString().ToLowerInvariant());
        if (PipelineResolver.MapOf(trigger, globalTriggers) is not { Count: > 0 } map
            || labels.Contains(TicketLabelVocabulary.For(project.Tracker).ApprovedSetStamp, StringComparer.OrdinalIgnoreCase)
            || map.Keys.Any(k => labels.Contains(k, StringComparer.OrdinalIgnoreCase))) return null;
        return map.FirstOrDefault(p => p.Value == PipelinePresets.CodeName).Key;
    }
}
