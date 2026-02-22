using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Creates the appropriate ICommandContext for a given command name,
/// pulling data from project configuration and pipeline state.
/// </summary>
public interface ICommandContextFactory
{
    ICommandContext Create(
        string commandName,
        ProjectConfig project,
        PipelineContext pipeline);
}
