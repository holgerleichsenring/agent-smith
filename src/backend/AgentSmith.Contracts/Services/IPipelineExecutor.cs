using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Orchestrates a pipeline: builds ICommandContext records from command names,
/// dispatches them through the CommandExecutor, stops on first failure.
/// </summary>
public interface IPipelineExecutor
{
    Task<CommandResult> ExecuteAsync(
        IReadOnlyList<string> commandNames,
        ResolvedProject projectConfig,
        PipelineContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// p0327: re-enters a checkpointed run at its serialized step cursor. Takes
    /// full PipelineCommand records (dynamically spliced follow-ups carry
    /// SkillName/Round/RepoName/ContextName/Workdir that the name-list overload
    /// would lose) and continues step indices from the checkpointed execution
    /// count so the dashboard renders one continuous run.
    /// </summary>
    Task<CommandResult> ResumeAsync(
        IReadOnlyList<PipelineCommand> commands,
        ResolvedProject projectConfig,
        PipelineContext context,
        int startExecutionCount,
        CancellationToken cancellationToken);
}
