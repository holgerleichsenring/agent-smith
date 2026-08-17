using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Scans;

/// <summary>
/// p0429a: gathers the run's evidence off the pipeline — the source the scan can read, the
/// API surface it loaded, and the exchanges its scanners really made.
/// </summary>
public sealed class ScanEvidenceFactory(ISandboxFileReaderFactory readerFactory)
{
    public ScanEvidence For(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var source = pipeline.TryGet<ISandbox>(ContextKeys.Sandbox, out var sandbox) && sandbox is not null
            ? readerFactory.Create(sandbox)
            : null;
        return new ScanEvidence(source, CitedEndpointIndex.FromSpec(Spec(pipeline)), Exchanges(pipeline));
    }

    /// <summary>
    /// The verbatim document when it is there, the compressed one otherwise — compression
    /// rewrites the raw JSON and keeps the endpoint list, so either answers "does this
    /// path exist"; the verbatim one is preferred because it never dropped anything.
    /// </summary>
    private static SwaggerSpec? Spec(PipelineContext pipeline) =>
        pipeline.TryGet<SwaggerSpec>(ContextKeys.SwaggerSpecFull, out var full) && full is not null
            ? full
            : pipeline.TryGet<SwaggerSpec>(ContextKeys.SwaggerSpec, out var compressed) ? compressed : null;

    private static ScanExchanges Exchanges(PipelineContext pipeline)
    {
        var captured = new List<HttpExchange?>();
        if (pipeline.TryGet<ZapResult>(ContextKeys.ZapResult, out var zap) && zap is not null)
            captured.AddRange(zap.Findings.Select(f => f.Exchange));
        if (pipeline.TryGet<NucleiResult>(ContextKeys.NucleiResult, out var nuclei) && nuclei is not null)
            captured.AddRange(nuclei.Findings.Select(f => f.Exchange));
        return ScanExchanges.From(captured);
    }
}
