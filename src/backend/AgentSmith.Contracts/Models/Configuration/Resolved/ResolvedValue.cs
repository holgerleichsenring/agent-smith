namespace AgentSmith.Contracts.Models.Configuration.Resolved;

/// <summary>
/// p0270a: where an effective config value came from. Travels WITH the value so
/// the dashboard renders exactly what the runtime resolved — provenance is not a
/// separate computation.
/// </summary>
public enum ResolutionSource
{
    /// <summary>The process-wide default applied (no per-project override).</summary>
    GlobalDefault,

    /// <summary>A per-project block overrode the global default.</summary>
    ProjectOverride,

    /// <summary>
    /// Not knowable at config time — resolved per run from repo/run inputs
    /// (e.g. the toolchain image chosen from the repo's context.yaml). The
    /// <see cref="ResolvedValue{T}.Value"/> is null for these.
    /// </summary>
    RunResolved,
}

/// <summary>
/// p0270a: an effective config value plus its provenance. One record, used for
/// every resolved field so the run path and the dashboard read the same shape.
/// </summary>
public sealed record ResolvedValue<T>(T Value, ResolutionSource Source)
{
    public static ResolvedValue<T> Global(T value) => new(value, ResolutionSource.GlobalDefault);
    public static ResolvedValue<T> Override(T value) => new(value, ResolutionSource.ProjectOverride);

    /// <summary>Run-resolved: no config-time value (Value is default — null for a
    /// reference type, serialized as null for the dashboard).</summary>
    public static ResolvedValue<T> PerRun() => new(default!, ResolutionSource.RunResolved);

    /// <summary>From a boolean: true → project-override, false → global-default.</summary>
    public static ResolvedValue<T> From(T value, bool isOverride) =>
        new(value, isOverride ? ResolutionSource.ProjectOverride : ResolutionSource.GlobalDefault);
}
