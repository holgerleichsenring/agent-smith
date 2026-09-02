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
        var readers = Sandboxes(pipeline).Select(readerFactory.Create).ToList();
        var source = readers.Count == 0 ? null : new ScanSourceReader(readers);
        return new ScanEvidence(source, CitedEndpointIndex.FromSpec(Spec(pipeline)), Exchanges(pipeline));
    }

    /// <summary>
    /// 2026-09-01-85b2: EVERY sandbox the run holds. The scan master addresses every
    /// repository, so reading only the default one made a second repository's findings
    /// unresolvable — which used to mean deleted.
    /// </summary>
    private static IReadOnlyList<ISandbox> Sandboxes(PipelineContext pipeline)
    {
        if (pipeline.TryGet<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, out var all)
            && all is { Count: > 0 })
            return [.. all.Values];
        return pipeline.TryGet<ISandbox>(ContextKeys.Sandbox, out var one) && one is not null ? [one] : [];
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
