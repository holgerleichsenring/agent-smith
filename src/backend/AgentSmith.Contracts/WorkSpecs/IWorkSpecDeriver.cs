using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Contracts.WorkSpecs;

/// <summary>
/// p0390: the one LLM call of the DeriveSpecification step. Given the ticket
/// (description, acceptance criteria, conversation, materialized attachments)
/// plus the analysis — and, on re-entry, the PREVIOUS artifact — it produces the
/// next revision. Revision, not regeneration.
/// </summary>
public interface IWorkSpecDeriver
{
    Task<(WorkSpecDraft? Draft, string? Error)> DeriveAsync(
        Ticket ticket,
        WorkSpecArtifact? previous,
        string cause,
        AgentConfig agentConfig,
        PipelineContext pipeline,
        CancellationToken cancellationToken);
}
