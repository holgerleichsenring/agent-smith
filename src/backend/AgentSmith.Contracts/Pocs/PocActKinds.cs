namespace AgentSmith.Contracts.Pocs;

/// <summary>
/// 2026-11-23-c81ed: the lifecycle acts an owner starts from a PoC's detail page — each a run of its own preset that
/// changes only the desired state and commits it — and the audit act each one records (<see cref="PocActs"/>).
/// </summary>
public static class PocActKinds
{
    public const string Stop = "stop";
    public const string Start = "start";
    public const string Rollback = "rollback";
    public const string Apply = "apply";
    public const string Delete = "delete";

    /// <summary>Every act, in the order the detail page offers them.</summary>
    public static IReadOnlyList<string> All { get; } = [Stop, Start, Rollback, Apply, Delete];

    public static bool IsAct(string? act) => act is not null && All.Contains(act, StringComparer.Ordinal);

    /// <summary>The preset that carries <paramref name="act"/> out.</summary>
    public static string PresetOf(string act) => act switch
    {
        Stop => Commands.PipelinePresets.PocStopName,
        Start => Commands.PipelinePresets.PocStartName,
        Rollback => Commands.PipelinePresets.PocRollbackName,
        Apply => Commands.PipelinePresets.PocApplyName,
        Delete => Commands.PipelinePresets.PocDeleteName,
        _ => throw new ArgumentOutOfRangeException(nameof(act), act, "not a PoC act"),
    };

    /// <summary>The act a preset carries out, or null for any other preset.</summary>
    public static string? OfPreset(string? preset) =>
        All.FirstOrDefault(act => string.Equals(PresetOf(act), preset, StringComparison.OrdinalIgnoreCase));

    /// <summary>The audit act an act's commit records; apply records 'deployed' with the reason 'applied'.</summary>
    public static string AuditActOf(string act) => act switch
    {
        Stop => PocActs.Stopped,
        Start => PocActs.Started,
        Rollback => PocActs.RolledBack,
        Delete => PocActs.Deleted,
        Apply => PocActs.Deployed,
        _ => throw new ArgumentOutOfRangeException(nameof(act), act, "not a PoC act"),
    };
}
