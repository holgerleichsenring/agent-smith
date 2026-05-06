using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Skills;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Builds a TriageInput from the pipeline context, calls the LLM via IChatClientFactory,
/// parses + validates the single-line JSON response, and returns a TriageOutput.
/// Implementation lives in Application — split out so StructuredTriageStrategy can stay
/// focused on command expansion and phase dispatch.
/// </summary>
public interface ITriageOutputProducer
{
    Task<TriageOutput> ProduceAsync(
        PipelineContext pipeline,
        CancellationToken cancellationToken);
}
