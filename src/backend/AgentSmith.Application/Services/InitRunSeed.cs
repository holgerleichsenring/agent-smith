using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services;

/// <summary>
/// 2026-11-17-713ea: what makes a run an init, decided from the pipeline it runs and nothing its
/// door passed. Until now only a caller that set <c>IsInit</c> got init mode and the init branch,
/// so an init a tracker label started ran on its ticket's branch, outside init mode. Every door —
/// the dashboard, chat, the CLI, a label — now reaches this one answer.
/// </summary>
public static class InitRunSeed
{
    /// <summary>Marks an init-project run as an init and hands it the init branch; any other run is
    /// left alone.</summary>
    public static void Apply(PipelineContext pipeline, string pipelineName)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!PipelinePresets.IsInit(pipelineName)) return;
        pipeline.Set(ContextKeys.InitMode, true);
        pipeline.Set(ContextKeys.CheckoutBranch, TicketBranchNamer.InitBranch);
    }

    /// <summary>True when the run was seeded as an init.</summary>
    public static bool IsInitRun(PipelineContext pipeline) =>
        pipeline.TryGet<bool>(ContextKeys.InitMode, out var init) && init;
}
