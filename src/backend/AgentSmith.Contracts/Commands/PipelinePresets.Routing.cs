namespace AgentSmith.Contracts.Commands;

public static partial class PipelinePresets
{
    /// <summary>
    /// 2026-09-25-3c7ad: the presets a TICKET can route to — what the configuration form offers
    /// on the value side of a label rule and as a default pipeline.
    /// <para>
    /// The two that are left out are excluded by the PROPERTY that makes them unreachable rather
    /// than by name: their run is launched with context only a host supplies — a transcript and a
    /// reply slot for the design conversation, an init context for project initialisation — which
    /// a label-routed run has no way to provide. Derived, so the next preset of that kind cannot
    /// quietly become offerable, and a hand-written minus-one list cannot drift from the reason.
    /// </para>
    /// <para>
    /// It is assigned in the static constructor over in the main file, never here: a field
    /// initializer in a partial file runs in an order the C# spec does not fix, and this one
    /// reads <see cref="Names"/>.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> Routable { get; private set; } = [];

    /// <summary>The presets whose run is launched with context no ticket carries.</summary>
    private static readonly HashSet<string> NeedsHostSuppliedContext =
        new(StringComparer.OrdinalIgnoreCase) { SpecDialogName, "init-project" };
}
