using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Builds a typed ICommandContext from a pipeline command, project config, and pipeline state.
/// Registered as keyed service by command name for dictionary-style dispatch.
/// </summary>
public interface IContextBuilder
{
    ICommandContext Build(PipelineCommand command, ProjectConfig project, PipelineContext pipeline);
}
