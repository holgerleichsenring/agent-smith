using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Builders;

public sealed class LoadSwaggerContextBuilder : IContextBuilder
{
    public ICommandContext Build(string commandName, ProjectConfig project, PipelineContext pipeline)
        => new LoadSwaggerContext(pipeline);
}

public sealed class SpawnNucleiContextBuilder : IContextBuilder
{
    public ICommandContext Build(string commandName, ProjectConfig project, PipelineContext pipeline)
        => new SpawnNucleiContext(pipeline);
}

public sealed class ApiSecurityTriageContextBuilder : IContextBuilder
{
    public ICommandContext Build(string commandName, ProjectConfig project, PipelineContext pipeline)
        => new ApiSecurityTriageContext(project.Agent, pipeline);
}

public sealed class ApiSecuritySkillRoundContextBuilder : IContextBuilder
{
    public ICommandContext Build(string commandName, ProjectConfig project, PipelineContext pipeline)
    {
        var parts = commandName.Split(':');
        var skillName = parts.Length > 1 ? parts[1] : string.Empty;
        var round = parts.Length > 2 && int.TryParse(parts[2], out var r) ? r : 1;
        return new ApiSecuritySkillRoundContext(skillName, round, project.Agent, pipeline);
    }
}
