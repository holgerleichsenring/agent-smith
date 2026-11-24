using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Pocs.Rollout;

/// <summary>
/// 2026-11-23-c81ec: what a run's follow waits for, read from the run — the Application's absence when an act set
/// ContextKeys.PocRolloutAbsent (poc-delete), else the desired state the GitOps commit recorded on the row (image digest,
/// replicas) under the target's registry, and the variables checksum of the run's seal.
/// </summary>
public sealed class PocRolloutExpectation
{
    public (PocRolloutExpected? Expected, string? Refusal) Of(PipelineContext pipeline, PocGitOpsSubject poc, PocTargetConfig target)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(poc);
        ArgumentNullException.ThrowIfNull(target);
        var application = PocNamespace.For(poc.Name);
        if (pipeline.TryGet<bool>(ContextKeys.PocRolloutAbsent, out var absent) && absent) return (PocRolloutExpected.Gone(application), null);
        if (poc.ImageDigest is not { } digest || poc.Revision is null)
            return (null, $"'{poc.Name}' has no desired state on its row; the GitOps commit records it before the follow");
        if (!pipeline.TryGet<PocSealResult>(ContextKeys.PocSeal, out var seal) || seal is null)
            return (null, $"the rollout of '{poc.Name}' has no variables checksum to wait for: the seal step did not run");
        if (target.Registry is not { } registry)
            return (null, $"{PocRegistryConfig.Key} of target '{target.Name}' is missing; the image to wait for cannot be named");
        return (new PocRolloutExpected(application, application, false, registry.LoginServer, $"{registry.Repository}/{poc.Name}",
            digest, poc.Replicas, seal.VariablesChecksum), null);
    }
}
