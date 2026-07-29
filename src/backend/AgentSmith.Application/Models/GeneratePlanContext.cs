using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for generating an execution plan via AI agent.
/// p0384: carries the per-repo analysis dictionaries (keyed by sandbox key /
/// repo name) so the plan prompt enumerates EVERY scoped repo; single-repo
/// runs are a dictionary of one flowing through the same path.
/// </summary>
public sealed record GeneratePlanContext(
    Ticket Ticket,
    IReadOnlyDictionary<string, ProjectMap> RepoProjectMaps,
    string CodingPrinciples,
    AgentConfig AgentConfig,
    PipelineContext Pipeline,
    IReadOnlyDictionary<string, string>? RepoCodeMaps = null,
    string? ProjectContext = null) : ICommandContext;
