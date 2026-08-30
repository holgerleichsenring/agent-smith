using AgentSmith.Contracts.Commands;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-08-30-18e3: decides which master is asked for an entry map, and binds the tool to
/// the run it maps.
/// <para>
/// Three masters declare the observation schema, so the read-only Review surface they share
/// cannot be the place this is decided. An api scan runs its source checkout fail-soft and
/// frequently holds no source at all, and a pr review is shown a diff rather than a system;
/// a located station is a question neither can be asked. Both keep the surface they have.
/// </para>
/// </summary>
public sealed class ScanStationToolFactory
{
    public IEnumerable<AITool> For(string? masterSkillName, PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return Maps(masterSkillName)
            ? new ScanStationToolHost(StationClaimLog.GetOrCreate(pipeline))
                .GetTools(phase: null, investigatorMode: null)
            : [];
    }

    /// <summary>Whether this master states an entry map at all.</summary>
    public static bool Maps(string? masterSkillName) =>
        string.Equals(masterSkillName, PipelinePresets.SecurityMaster, StringComparison.OrdinalIgnoreCase);
}
