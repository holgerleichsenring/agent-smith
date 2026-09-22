using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;

namespace AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

/// <summary>
/// p0392: what the server would say about an entity the operator has not saved yet.
///
/// p0391a made the server report what is missing once it is RUNNING. A configuration that
/// stops a unit is worth catching in the editor that produced it, before the save — the
/// 2026-07-31 outage was a trigger without needs_clarification_status, and the operator had
/// no way to know until a boot refused.
///
/// Every rule here is the server's own rule object, called on the same raw shapes the
/// loader builds: <see cref="RawConfigPatch"/> projects the draft, EffectiveTriggerBuilder
/// merges the tracker-owned workflow exactly as materialization does, and
/// <see cref="ProjectConfigNormalizer.Inspect"/> evaluates. Nothing is restated: a
/// second copy of "what a valid configuration is" would drift the first time either was
/// extended, which is the defect class this codebase keeps paying for.
/// </summary>
public sealed class ConfigDraftRules(
    EffectiveTriggerBuilder effectiveTriggers,
    ProjectConfigNormalizer normalizer)
{
    /// <summary>
    /// The findings this project draft would carry, judged against the rest of the stored
    /// catalog (its tracker owns half the workflow, so the draft alone cannot be judged).
    /// </summary>
    public IReadOnlyList<StartupFinding> ForProject(ProjectEntity draft, ConfigCatalog catalog)
    {
        var project = RawProjectPatch.Apply(draft, existing: null);
        var tracker = catalog.Trackers.FirstOrDefault(t => t.Id == draft.Tracker);
        var rawTracker = tracker is null ? null : RawConfigPatch.Tracker(tracker, existing: null);

        effectiveTriggers.Apply(draft.Id, project, rawTracker);
        // 2026-09-15-9b3e: the template rules ran only on the WRITE, so a broken binding
        // reached the operator as a 400 after Save rather than as a finding on the field
        // that caused it. Not the whole referential validator: it also judges agent,
        // tracker and repos and throws one aggregated string, so a blank new draft would
        // report "references unknown agent ''" on every keystroke.
        return
        [
            .. normalizer.Inspect(draft.Id, project),
            .. ProjectTemplateDraftCheck.MessagesFor(draft, catalog)
                .Select(m => ProjectFindings.Blocking(draft.Id, "templates", m)),
        ];
    }

    /// <summary>
    /// The findings this tracker draft would carry. The descriptor's requiredness is
    /// enforced on upsert by <see cref="ConfigStudioCapabilities.ValidateTracker"/>; here
    /// the same call reports instead of refusing, so the form can name the field first.
    /// </summary>
    public IReadOnlyList<StartupFinding> ForTracker(TrackerEntity draft)
    {
        List<StartupFinding> findings = [];
        try
        {
            ConfigStudioCapabilities.ValidateTracker(draft);
        }
        catch (ConfigurationException ex)
        {
            findings.Add(new StartupFinding(
                StartupSubsystems.Configuration, StartupFindingSeverity.Blocking,
                ex.Message, Field: "type"));
        }
        if (UndeclaredRouting(draft) is { } advisory) findings.Add(advisory);
        return findings;
    }

    /// <summary>
    /// 2026-09-16-a4d7: a tracker declaring neither a label map nor a default routes every
    /// ticket to the hardcoded fallback, and nothing said so. ADVISORY, not blocking — every
    /// tracker configured before that phase is in this state and must keep saving — and it
    /// names its own field, because the tracker findings above all say "type".
    /// </summary>
    private static StartupFinding? UndeclaredRouting(TrackerEntity draft)
    {
        if (draft.PipelineFromLabel is { Count: > 0 }) return null;
        if (!string.IsNullOrWhiteSpace(draft.DefaultPipeline)) return null;
        return new StartupFinding(
            StartupSubsystems.Configuration, StartupFindingSeverity.Advisory,
            $"Tracker '{draft.Id}' declares no pipeline_from_label and no default pipeline: "
            + $"every ticket it routes runs '{PipelinePresets.UndeclaredFallbackPipeline}'.",
            Field: "defaultPipeline");
    }
}
