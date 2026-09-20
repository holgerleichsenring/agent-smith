namespace AgentSmith.Contracts.Commands;

/// <summary>
/// Spec-dialog command names. Its own partial because
/// <c>CommandNames.Pipeline.cs</c> sits exactly at its file-length baseline and may
/// only get shorter; the coverage tests reflect over every partial, so a name is as
/// discoverable here as it is there.
/// </summary>
public static partial class CommandNames
{
    /// <summary>
    /// 2026-09-19-4c1f: reads the turn's scope through the source provider — no
    /// container, no clone — and publishes each scoped repository's context.yaml and
    /// principles.md under the keys the design-partner master already binds. The
    /// sandbox-bound loaders cannot serve this preset: two of them provision, which
    /// would replace the turn's lazy read-only scopes with empty pods. Reports what it
    /// could not read and succeeds — a provider outage leaves a conversation that can
    /// still answer from the code map.
    /// </summary>
    public const string GroundSpecDialog = "GroundSpecDialogCommand";
}
