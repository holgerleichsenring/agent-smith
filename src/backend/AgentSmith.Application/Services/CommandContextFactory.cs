using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services;

/// <summary>
/// Maps pipeline commands to typed ICommandContext records via keyed IContextBuilder lookup.
/// Adding a new command requires only a new IContextBuilder + DI registration.
/// 2026-10-12-967ab: a command is looked up by its declared name, so an operator's preset that
/// still names a spec-block command by its old name resolves to the same builder.
/// </summary>
public sealed class CommandContextFactory(
    IEnumerable<KeyedContextBuilder> builders) : ICommandContextFactory
{
    private readonly Dictionary<string, IContextBuilder> _builders =
        builders.ToDictionary(b => b.CommandName, b => b.Builder);

    public ICommandContext Create(
        PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
    {
        if (!_builders.TryGetValue(CommandNames.Declared(command.Name), out var builder))
        {
            if (CommandNames.TryGetRetirementMessage(command.Name, out var retirement))
                throw new ConfigurationException(retirement);
            throw new ConfigurationException($"Unknown command: '{command.DisplayName}'");
        }

        return builder.Build(command, project, pipeline);
    }
}

/// <summary>
/// Wrapper to associate a command name with its IContextBuilder for DI registration.
/// </summary>
public sealed record KeyedContextBuilder(string CommandName, IContextBuilder Builder);
