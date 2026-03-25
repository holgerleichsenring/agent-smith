using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

public sealed class LoadSwaggerContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ProjectConfig project, PipelineContext pipeline)
        => new LoadSwaggerContext(pipeline);
}

public sealed class SpawnNucleiContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ProjectConfig project, PipelineContext pipeline)
        => new SpawnNucleiContext(pipeline);
}

public sealed class SpawnSpectralContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ProjectConfig project, PipelineContext pipeline)
        => new SpawnSpectralContext(pipeline);
}

public sealed class ApiSecurityTriageContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ProjectConfig project, PipelineContext pipeline)
        => new ApiSecurityTriageContext(project.Agent, pipeline);
}

public sealed class DeliverFindingsContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ProjectConfig project, PipelineContext pipeline)
    {
        pipeline.TryGet<string>(ContextKeys.OutputFormat, out var format);
        return new DeliverFindingsContext(format ?? "console", pipeline);
    }
}

public sealed class LoadSkillsContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ProjectConfig project, PipelineContext pipeline)
        => new LoadSkillsContext(project.SkillsPath, pipeline);
}

public sealed class CompileFindingsContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ProjectConfig project, PipelineContext pipeline)
        => new CompileFindingsContext(pipeline);
}

public sealed class ApiSecuritySkillRoundContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ProjectConfig project, PipelineContext pipeline)
    {
        var skillName = command.SkillName ?? string.Empty;
        var round = command.Round ?? 1;
        return new ApiSecuritySkillRoundContext(skillName, round, project.Agent, pipeline);
    }
}
