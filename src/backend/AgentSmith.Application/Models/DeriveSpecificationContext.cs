using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Models;

/// <summary>
/// p0390: input for the DeriveSpecification step. The repo list is the RESOLVED
/// scope in order — the spec lives in exactly ONE repo, the first of them, and
/// the DB pointer records which so a later run with a different scope still
/// finds it.
/// </summary>
public sealed record DeriveSpecificationContext(
    Ticket? Ticket,
    IReadOnlyList<RepoConnection> Repos,
    AgentConfig AgentConfig,
    PipelineContext Pipeline) : ICommandContext;
