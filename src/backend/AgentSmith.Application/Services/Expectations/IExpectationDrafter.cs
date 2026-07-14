using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Expectations;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Expectations;

/// <summary>
/// p0328: drafts the schema-capped Soll block from ticket + analysis via the
/// drafting model. Bounded validation retries happen inside; a null result
/// means the model could not produce a schema-conforming draft and the step
/// must fail loudly (Error carries the last validation/parse message).
/// </summary>
public interface IExpectationDrafter
{
    Task<(ExpectationDraft? Draft, string? Error)> DraftAsync(
        Ticket ticket, AgentConfig agentConfig, PipelineContext pipeline,
        CancellationToken cancellationToken);
}
