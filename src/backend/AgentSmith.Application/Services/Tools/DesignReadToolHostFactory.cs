using AgentSmith.Application.Services.Design;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-10-01-7f7ab: builds design_read for a master whose project has a figma source — a run
/// reads the sources off its <see cref="ResolvedProject"/>, a design turn off the seed its
/// runner set (<see cref="PipelineDesignSources"/>, 2026-10-01-7f7ad). No source, no host: the
/// tool is never offered where it could only fail.
/// <para>2026-10-01-7f7ae: with a run id on the pipeline the host records each answered read
/// on that run; without one nothing is published — no run id is invented.</para>
/// <para>2026-10-01-7f7ac: every host renders the node it read through the shared image deposit.</para>
/// </summary>
public sealed class DesignReadToolHostFactory(
    IFigmaClient figma, IToolImageDeposit images, IEventPublisher events, ILogger<DesignReadToolHostFactory> logger)
{
    public DesignReadToolHost? Create(PipelineContext pipeline)
    {
        var figmaSources = PipelineDesignSources.Figma(pipeline);
        if (figmaSources.Count == 0) return null;
        var recorder = pipeline.TryGet<string>(ContextKeys.RunId, out var runId) && !string.IsNullOrEmpty(runId)
            ? new DesignReadRecorder(events, runId, logger)
            : null;
        return new DesignReadToolHost(figma, figmaSources, recorder, new DesignNodeRender(figma, images));
    }
}
