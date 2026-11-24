using AgentSmith.Contracts.Commands;

namespace AgentSmith.Contracts.Pocs;

/// <summary>
/// 2026-11-23-933db: the ONE list of PoC presets — the only presets a server-provisioned poc- project runs. Each later
/// slice adds its constant here with its preset: poc-variables (933dg), poc-stop, poc-start, poc-rollback, poc-apply and
/// poc-delete (c81ed), poc-extend (c81ee). The installation presets (poc-target-install, poc-upload-purge) run on
/// '@agent-smith' and are not in it.
/// </summary>
public static class PocPresets
{
    public static IReadOnlyList<string> Names { get; } = [
        PipelinePresets.PocDeployName, PipelinePresets.PocVariablesName,
        PipelinePresets.PocStopName, PipelinePresets.PocStartName, PipelinePresets.PocRollbackName, // 2026-11-23-c81ed
        PipelinePresets.PocApplyName, PipelinePresets.PocDeleteName, PipelinePresets.PocExtendName, // 2026-11-23-c81ee
    ];

    public static bool Contains(string pipeline) => Names.Contains(pipeline, StringComparer.OrdinalIgnoreCase);
}
