namespace AgentSmith.Contracts.Commands;

public static partial class PipelinePresets
{
    /// <summary>
    /// The presets a TICKET can route to — what the configuration form offers on the value side
    /// of a label rule and as a default pipeline.
    /// <para>
    /// The ones left out are excluded by the PROPERTY that makes them unreachable rather than by
    /// name: the design conversation's run is launched with a transcript and a reply slot only a
    /// host supplies, which a label-routed run has no way to provide; 2026-10-26-bdd0a: the spec
    /// derivation moves no ticket status, so a ticket routed to it would stay where the poller
    /// claims it and be derived again on every poll. Project initialisation is NOT in that set: a
    /// label starts it on the ticket's branch like any other preset.
    /// </para>
    /// <para>
    /// It is assigned in the static constructor over in the main file, never here: a field
    /// initializer in a partial file runs in an order the C# spec does not fix, and this one
    /// reads <see cref="Names"/>.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> Routable { get; private set; } = [];

    /// <summary>True for a preset whose run is launched with context no ticket carries, so a
    /// label map or a default pipeline naming it routes a ticket to a run that cannot start.</summary>
    public static bool NeedsHostContext(string pipelineName) =>
        string.Equals(pipelineName, SpecDialogName, StringComparison.OrdinalIgnoreCase);

    /// <summary>Why a label cannot start this preset, in the words a configuration finding uses;
    /// null for a preset a ticket can be routed to.</summary>
    public static string? NotRoutableBecause(string pipelineName) =>
        NotRoutable.GetValueOrDefault(pipelineName);

    private static readonly Dictionary<string, string> NotRoutable = new(StringComparer.OrdinalIgnoreCase)
    {
        [SpecDialogName] = "its run needs a design conversation's transcript and reply slot, which only the chat "
            + "or the dashboard supplies, so a ticket routed to it fails when it starts",
        [SpecDeriveName] = "a spec derivation moves no ticket status, so the poller would derive the same ticket "
            + "again on every poll; start it from the ticket page or the ticket's conversation",
    };
}
