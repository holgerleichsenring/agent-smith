using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

public sealed class TriageContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ProjectConfig project, PipelineContext pipeline)
        => new TriageContext(pipeline.Resolved().Agent, pipeline);
}

public sealed class SecurityTriageContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ProjectConfig project, PipelineContext pipeline)
        => new SecurityTriageContext(pipeline.Resolved().Agent, pipeline);
}

public sealed class SwitchSkillContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ProjectConfig project, PipelineContext pipeline)
    {
        var skillName = command.SkillName ?? string.Empty;
        return new SwitchSkillContext(skillName, pipeline);
    }
}

public sealed class SkillRoundContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ProjectConfig project, PipelineContext pipeline)
    {
        var skillName = command.SkillName ?? string.Empty;
        var round = command.Round ?? 1;
        return new SkillRoundContext(skillName, round, pipeline.Resolved().Agent, pipeline);
    }
}

public sealed class SecuritySkillRoundContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ProjectConfig project, PipelineContext pipeline)
    {
        var skillName = command.SkillName ?? string.Empty;
        var round = command.Round ?? 1;
        return new SecuritySkillRoundContext(skillName, round, pipeline.Resolved().Agent, pipeline);
    }
}

public sealed class ConvergenceCheckContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ProjectConfig project, PipelineContext pipeline)
        => new ConvergenceCheckContext(pipeline.Resolved().Agent, pipeline);
}

public sealed class CompileDiscussionContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ProjectConfig project, PipelineContext pipeline)
    {
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        return new CompileDiscussionContext(repo, pipeline);
    }
}
