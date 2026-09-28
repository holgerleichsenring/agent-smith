namespace AgentSmith.Contracts.Commands;

public static partial class PipelinePresets
{
    /// <summary>
    /// The presets a TICKET can route to — what the configuration form offers on the value side
    /// of a label rule and as a default pipeline.
    /// <para>
    /// The one left out is excluded by the PROPERTY that makes it unreachable rather than by
    /// name: the design conversation's run is launched with a transcript and a reply slot only a
    /// host supplies, which a label-routed run has no way to provide. Project initialisation is
    /// NOT in that set: a label starts it on the ticket's branch like any other preset. Derived,
    /// so the next preset of the excluded kind cannot quietly become offerable.
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
        NeedsHostSuppliedContext.Contains(pipelineName);

    /// <summary>The presets whose run is launched with context no ticket carries.</summary>
    private static readonly HashSet<string> NeedsHostSuppliedContext =
        new(StringComparer.OrdinalIgnoreCase) { SpecDialogName };
}
