using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for a Filter-role skill round. Filter takes the current observations
/// list as input and either reduces it (output_type[Filter] = List) or synthesizes
/// an artifact (output_type[Filter] = Artifact).
/// </summary>
public sealed record FilterRoundContext(
    string SkillName,
    int Round,
    AgentConfig AgentConfig,
    PipelineContext Pipeline) : ICommandContext;
