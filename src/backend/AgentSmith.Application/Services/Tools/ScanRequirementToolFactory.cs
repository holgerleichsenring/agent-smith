using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-08-30-3c12: binds the two requirement tools to the run they are cited for, for the
/// one master that states an entry map.
/// <para>
/// Citing a station's requirements presupposes knowing where the station lives, so the
/// question follows the map exactly: the master that states one is given the tools, and the
/// two that keep today's shape — an api scan that frequently holds no source, a pr review
/// shown a diff — are handed nothing and keep the read-only surface they have.
/// </para>
/// </summary>
public sealed class ScanRequirementToolFactory(
    IVerificationLens lens, CitedFindingRecorder recorder)
{
    public IEnumerable<AITool> For(string? masterSkillName, PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!ScanStationToolFactory.Maps(masterSkillName)) return [];
        var log = CitedFindingLog.GetOrCreate(pipeline);
        return
        [
            .. new RequirementLookupToolHost(lens, pipeline).GetTools(null, null),
            .. new CitedFindingToolHost(recorder, log, pipeline).GetTools(null, null)
        ];
    }
}
