using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Creates the appropriate ICommandContext for a given pipeline command,
/// pulling data from project configuration and pipeline state.
/// </summary>
public interface ICommandContextFactory
{
    ICommandContext Create(
        PipelineCommand command,
        ProjectConfig project,
        PipelineContext pipeline);
}
