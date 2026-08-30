using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-08-30-3c12: binds the two requirement tools to the run they are answered for, for
/// the one master that states an entry map.
/// <para>
/// Answering a station's requirements presupposes knowing where the station lives, so the
/// question follows the map exactly: the master that states one is asked, and the two that
/// keep today's shape — an api scan that frequently holds no source, a pr review shown a
/// diff — are handed nothing and keep the read-only surface they have.
/// </para>
/// </summary>
public sealed class ScanRequirementToolFactory(
    IVerificationLens lens, RequirementAnswerRecorder recorder)
{
    public IEnumerable<AITool> For(string? masterSkillName, PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!ScanStationToolFactory.Maps(masterSkillName)) return [];
        var log = RequirementAnswerLog.GetOrCreate(pipeline);
        return
        [
            .. new RequirementCatalogueToolHost(lens, pipeline).GetTools(null, null),
            .. new RequirementAnswerToolHost(recorder, log, pipeline).GetTools(null, null)
        ];
    }
}
