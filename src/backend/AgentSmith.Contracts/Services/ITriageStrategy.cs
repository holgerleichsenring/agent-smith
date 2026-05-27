using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Strategy that produces the post-triage pipeline-command sequence for a single
/// pipeline run. p0131c: only <see cref="AgentSmith.Application.Services.Triage.StructuredTriageStrategy"/>
/// remains; the legacy LLM-discussion variant retired together with the
/// per-skill criteria bag in p0131a. The interface is kept as a DI seam so
/// future routing decisions (provider-specific variants, replay-from-trace,
/// etc.) plug in without re-wiring callers.
/// Implementations resolve IChatClient via IChatClientFactory + AgentConfig
/// from the pipeline context.
/// </summary>
public interface ITriageStrategy
{
    Task<CommandResult> ExecuteAsync(
        PipelineContext pipeline,
        CancellationToken cancellationToken);
}
