using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Pipeline;

/// <summary>
/// p0405: composes the two labels a pipeline command wears — the progress LABEL
/// (present-continuous, "Executing …") and the operator-facing DISPLAY NAME
/// (noun-phrase). Split out of PipelineStepRunner because the runner is no longer
/// the only caller: the planned-steps announcement labels commands that have not
/// run yet, and both must read identically or the rail would rename a step the
/// moment it starts. Pure functions over a command — no collaborators, no DI.
/// </summary>
public static class StepLabelComposer
{
    // p0176c: appends a (repo, component) suffix when the command carries
    // RepoName / ContextName so multi-repo BootstrapRound dispatches render as one
    // operator-readable row per (repo, component) pair instead of N identical
    // "Producing bootstrap files" rows.
    public static string Label(PipelineCommand cmd) =>
        WithScope(PhaseQualified(CommandNames.GetLabel(cmd.Name), cmd), cmd);

    // p0203: draws the base label from CommandDisplayNames (noun-phrase) instead
    // of CommandNames.GetLabel (present-continuous).
    public static string DisplayName(PipelineCommand cmd) =>
        WithScope(PhaseQualified(CommandDisplayNames.Get(cmd.Name), cmd), cmd);

    // p0405: the same name WITHOUT the phase prefix, for surfaces that carry the
    // phase as its own field. The prefix exists because the persisted step row has
    // nowhere else to put the phase; a payload with a phase field does not need it,
    // and the read path would only split it off again.
    public static string PlainDisplayName(PipelineCommand cmd) =>
        WithScope(CommandDisplayNames.Get(cmd.Name), cmd);

    private static string WithScope(string label, PipelineCommand cmd)
    {
        var hasRepo = !string.IsNullOrEmpty(cmd.RepoName);
        var hasContext = !string.IsNullOrEmpty(cmd.ContextName);
        if (hasRepo && hasContext) return $"{label} ({cmd.RepoName}, {cmd.ContextName})";
        if (hasRepo) return $"{label} ({cmd.RepoName})";
        if (hasContext) return $"{label} ({cmd.ContextName})";
        return label;
    }

    // p0393a: a sequence runs the same steps once per derived phase. Without the
    // phase id on the row the trail reads as the run repeating itself; with it,
    // progress is readable per phase, which is the point of splitting the ticket.
    private static string PhaseQualified(string label, PipelineCommand cmd) =>
        string.IsNullOrEmpty(cmd.PhaseId) ? label : $"{cmd.PhaseId}: {label}";
}
