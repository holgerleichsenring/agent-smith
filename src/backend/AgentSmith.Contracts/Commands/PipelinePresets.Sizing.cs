namespace AgentSmith.Contracts.Commands;

public static partial class PipelinePresets
{
    // 2026-11-24-6811c: the presets whose sandboxes build and test — every code run, and a refresh-base run, which builds
    // and tests each base merge before it pushes. Only sandbox sizing reads it; ExpectsCodeChanges keeps the master's
    // re-engagement to the code preset.
    private static readonly HashSet<string> BuildingPresets = new(StringComparer.OrdinalIgnoreCase)
    {
        CodeName, RefreshBaseName,
    };

    /// <summary>2026-11-24-6811c: true when the preset's sandbox builds and tests, so it is sized for building.</summary>
    public static bool BuildsInSandbox(string pipelineName) => BuildingPresets.Contains(pipelineName);
}
