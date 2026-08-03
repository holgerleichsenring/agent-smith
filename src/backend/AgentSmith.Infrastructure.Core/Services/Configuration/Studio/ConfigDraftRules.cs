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
        var project = RawConfigPatch.Project(draft, existing: null);
        var tracker = catalog.Trackers.FirstOrDefault(t => t.Id == draft.Tracker);
        var rawTracker = tracker is null ? null : RawConfigPatch.Tracker(tracker, existing: null);

        effectiveTriggers.Apply(draft.Id, project, rawTracker);
        return normalizer.Inspect(draft.Id, project);
    }

    /// <summary>
    /// The findings this tracker draft would carry. The descriptor's requiredness is
    /// enforced on upsert by <see cref="ConfigStudioCapabilities.ValidateTracker"/>; here
    /// the same call reports instead of refusing, so the form can name the field first.
    /// </summary>
    public IReadOnlyList<StartupFinding> ForTracker(TrackerEntity draft)
    {
        try
        {
            ConfigStudioCapabilities.ValidateTracker(draft);
            return [];
        }
        catch (ConfigurationException ex)
        {
            return
            [
                new StartupFinding(
                    StartupSubsystems.Configuration, StartupFindingSeverity.Blocking,
                    ex.Message, Field: "type"),
            ];
        }
    }
}
