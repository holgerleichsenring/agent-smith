using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Exceptions;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-09-25-f6c2: refuses a configuration in which one word on a ticket means two things —
/// a framework label this tracker writes (2026-09-25-3c7ac made those eight names the
/// operator's to choose) spelled the same as one of the operator's OWN routing words.
/// <para>
/// THE COLLISION DOES NOT PRODUCE TWO ANSWERS; IT PRODUCES ONE WRONG ONE, THREE WAYS, EACH
/// SILENT. (1) A ticket that binds to phase execution short-circuits in ProjectResolver
/// BEFORE PipelineResolver.Resolve is reached, so a routing key equal to the approved-set
/// stamp never runs at all. (2) PipelineResolver STRIPS lifecycle words from the labels it
/// matches on, so a routing key equal to one of them cannot satisfy its own map — the key
/// simply stops existing. (3) A project resolving on a tag equal to a framework word matches
/// EVERY framework-labelled ticket on that tracker. None of the three logs anything an
/// operator would find: a key that stopped existing looks exactly like a ticket nobody
/// labelled. A configuration that cannot be diagnosed afterwards must not be accepted.
/// </para>
/// <para>
/// A WORD IS THE FRAMEWORK'S IF THIS TRACKER'S VOCABULARY RECOGNISES IT — the configured name
/// OR any name the framework has ever written. Reading is that union (3c7ac), and it is the
/// union that bites: PipelineResolver still strips by the historical constants, so both halves
/// are live for different mechanisms.
/// </para>
/// </summary>
public static class RoutingWordCollisionRule
{
    /// <summary>
    /// The tracker door. Judges this tracker's own routing keys AND the resolution value of
    /// every project already bound to it — renaming a label collides with projects saved long
    /// before, and no per-entity validator can see them.
    /// </summary>
    public static void ValidateTracker(TrackerEntity tracker, ConfigCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(tracker);
        ArgumentNullException.ThrowIfNull(catalog);
        var vocabulary = new TicketLabelVocabulary(tracker.LabelNames);
        foreach (var key in tracker.PipelineFromLabel?.Keys ?? [])
            Refuse(vocabulary, key, tracker.Id, $"Tracker '{tracker.Id}': pipeline_from_label key");
        foreach (var project in catalog.Projects.Where(p => ConfigNames.AreSame(p.Tracker, tracker.Id)))
            RefuseResolution(vocabulary, project, tracker.Id);
    }

    /// <summary>
    /// The project door. The same rule from the other side: a project changed later can create
    /// a collision the tracker upsert already passed.
    /// </summary>
    public static void ValidateProject(ProjectEntity project, ConfigCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(catalog);
        var tracker = catalog.Trackers.FirstOrDefault(t => ConfigNames.AreSame(t.Id, project.Tracker));
        // An unknown tracker reference is the referential validator's refusal, not this one's.
        if (tracker is null) return;
        RefuseResolution(new TicketLabelVocabulary(tracker.LabelNames), project, tracker.Id);
    }

    /// <summary>
    /// Only the TAG strategy matches a label. An area path, a repo URL and a to-address are not
    /// read against the board's labels at all, so a value equal to a framework word collides
    /// with nothing there.
    /// </summary>
    private static void RefuseResolution(
        TicketLabelVocabulary vocabulary, ProjectEntity project, string trackerId)
    {
        if (project.Resolution is not { } resolution) return;
        if (!ConfigNames.AreSame(resolution.Strategy, ConfigStudioCapabilities.WireName(ResolutionStrategy.Tag)))
            return;
        Refuse(vocabulary, resolution.Value, trackerId, $"Project '{project.Id}': resolution tag");
    }

    private static void Refuse(
        TicketLabelVocabulary vocabulary, string? word, string trackerId, string field)
    {
        if (FrameworkWord(vocabulary, word) is not { } framework) return;
        throw new ConfigurationException(
            $"{field} '{word}' is also {framework} on tracker '{trackerId}'. One word on a ticket "
            + "would mean two things, and routing answers with the framework's meaning without "
            + "saying so. Rename the routing word, or the tracker's label_names entry.");
    }

    /// <summary>What this tracker's vocabulary calls the word, or null when it is not ours.</summary>
    private static string? FrameworkWord(TicketLabelVocabulary vocabulary, string? word)
    {
        if (string.IsNullOrWhiteSpace(word)) return null;
        var trimmed = word.Trim();
        if (vocabulary.TryParse(trimmed, out var status))
            return $"the framework's '{status}' lifecycle label";
        return vocabulary.IsApprovedSetStamp(trimmed) ? "the framework's approved-set stamp" : null;
    }
}
