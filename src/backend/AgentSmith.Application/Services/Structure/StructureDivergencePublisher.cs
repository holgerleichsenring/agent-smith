using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Structure;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// 2026-10-12-fbbda: judges the branch once more when the pull requests are composed and puts
/// every repository's divergences where the bodies read them. Not in the spec record: the gate
/// judges the whole branch, so a per-spec record would list earlier specs' work. A judgment that
/// fails names nothing and never stops a pull request.
/// </summary>
public sealed class StructureDivergencePublisher(StructureGate gate, ILogger<StructureDivergencePublisher> logger)
{
    public async Task PublishAsync(PipelineContext pipeline, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        try
        {
            var divergences = await gate.DivergencesAsync(pipeline, ct);
            pipeline.Set<IReadOnlyList<StructureFinding>>(ContextKeys.StructureDivergences, divergences);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Structure divergences not judged; the pull requests name none");
        }
    }
}
