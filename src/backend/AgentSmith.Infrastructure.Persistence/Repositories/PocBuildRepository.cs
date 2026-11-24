using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-11-23-933dd: what a poc-deploy run's build step writes on a revision — the pipeline run it follows, an image-stage
/// block, the confirmed digest. A PoC moves by the rule the code scan uses: an image block blocks one that never reached
/// ready (importing or blocked), a built revision sets a blocked PoC back to importing, a ready PoC keeps serving.
/// </summary>
public sealed class PocBuildRepository(IUnitOfWork unitOfWork)
{
    public Task RecordPipelineRunAsync(long pocId, int revision, string? runId, string? url, CancellationToken ct) =>
        Revision(pocId, revision).ExecuteUpdateAsync(set => set
            .SetProperty(r => r.PipelineRunId, runId).SetProperty(r => r.PipelineUrl, url), ct);

    public async Task RecordImageBlockAsync(long pocId, int revision, string verdictJson, CancellationToken ct)
    {
        await Revision(pocId, revision).ExecuteUpdateAsync(set => set.SetProperty(r => r.State, PocRevisionStates.Blocked)
            .SetProperty(r => r.ImageVerdictJson, verdictJson).SetProperty(r => r.BlockedStage, PocScanStages.Image), ct);
        await MovePocAsync(pocId, [PocStates.Importing, PocStates.Blocked], PocStates.Blocked, ct);
    }

    public async Task RecordBuiltAsync(long pocId, int revision, string digest, string verdictJson, CancellationToken ct)
    {
        await Revision(pocId, revision).ExecuteUpdateAsync(set => set.SetProperty(r => r.State, PocRevisionStates.Built)
            .SetProperty(r => r.Digest, digest).SetProperty(r => r.ImageVerdictJson, verdictJson)
            .SetProperty(r => r.BlockedStage, (string?)null), ct);
        await MovePocAsync(pocId, [PocStates.Blocked], PocStates.Importing, ct);
    }

    private IQueryable<PocRevision> Revision(long pocId, int revision) =>
        unitOfWork.Set<PocRevision>().Where(r => r.PocId == pocId && r.Number == revision);

    private Task MovePocAsync(long pocId, string[] from, string to, CancellationToken ct) =>
        unitOfWork.Set<Poc>().Where(p => p.Id == pocId && from.Contains(p.State))
            .ExecuteUpdateAsync(set => set.SetProperty(p => p.State, to), ct);
}
