using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

public sealed class TriageContextBuilder : IContextBuilder
{
    public ICommandContext Build(string commandName, ProjectConfig project, PipelineContext pipeline)
        => new TriageContext(project.Agent, pipeline);
}

public sealed class SecurityTriageContextBuilder : IContextBuilder
{
    public ICommandContext Build(string commandName, ProjectConfig project, PipelineContext pipeline)
        => new SecurityTriageContext(project.Agent, pipeline);
}

public sealed class SwitchSkillContextBuilder : IContextBuilder
{
    public ICommandContext Build(string commandName, ProjectConfig project, PipelineContext pipeline)
    {
        var parts = commandName.Split(':');
        var skillName = parts.Length > 1 ? parts[1] : string.Empty;
        return new SwitchSkillContext(skillName, pipeline);
    }
}

public sealed class SkillRoundContextBuilder : IContextBuilder
{
    public ICommandContext Build(string commandName, ProjectConfig project, PipelineContext pipeline)
    {
        var parts = commandName.Split(':');
        var skillName = parts.Length > 1 ? parts[1] : string.Empty;
        var round = parts.Length > 2 && int.TryParse(parts[2], out var r) ? r : 1;
        return new SkillRoundContext(skillName, round, project.Agent, pipeline);
    }
}

public sealed class SecuritySkillRoundContextBuilder : IContextBuilder
{
    public ICommandContext Build(string commandName, ProjectConfig project, PipelineContext pipeline)
    {
        var parts = commandName.Split(':');
        var skillName = parts.Length > 1 ? parts[1] : string.Empty;
        var round = parts.Length > 2 && int.TryParse(parts[2], out var r) ? r : 1;
        return new SecuritySkillRoundContext(skillName, round, project.Agent, pipeline);
    }
}

public sealed class ConvergenceCheckContextBuilder : IContextBuilder
{
    public ICommandContext Build(string commandName, ProjectConfig project, PipelineContext pipeline)
        => new ConvergenceCheckContext(project.Agent, pipeline);
}

public sealed class CompileDiscussionContextBuilder : IContextBuilder
{
    public ICommandContext Build(string commandName, ProjectConfig project, PipelineContext pipeline)
    {
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        return new CompileDiscussionContext(repo, pipeline);
    }
}
