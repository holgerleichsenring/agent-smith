using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services;

/// <summary>
/// Maps command names to typed ICommandContext records via keyed IContextBuilder lookup.
/// Adding a new command requires only a new IContextBuilder + DI registration.
/// </summary>
public sealed class CommandContextFactory(
    IEnumerable<KeyedContextBuilder> builders) : ICommandContextFactory
{
    private readonly Dictionary<string, IContextBuilder> _builders =
        builders.ToDictionary(b => b.CommandName, b => b.Builder);

    public ICommandContext Create(
        string commandName, ProjectConfig project, PipelineContext pipeline)
    {
        var baseCommand = commandName.Contains(':')
            ? commandName[..commandName.IndexOf(':')]
            : commandName;

        if (!_builders.TryGetValue(baseCommand, out var builder))
            throw new ConfigurationException($"Unknown command: '{commandName}'");

        return builder.Build(commandName, project, pipeline);
    }
}

/// <summary>
/// Wrapper to associate a command name with its IContextBuilder for DI registration.
/// </summary>
public sealed record KeyedContextBuilder(string CommandName, IContextBuilder Builder);
