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

public sealed class SessionSetupContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ProjectConfig project, PipelineContext pipeline)
        => new SessionSetupContext(pipeline);
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

public sealed class SpawnZapContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ProjectConfig project, PipelineContext pipeline)
        => new SpawnZapContext(pipeline);
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
        pipeline.TryGet<string>(ContextKeys.OutputDir, out var outputDir);

        var formats = (format ?? "console")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        return new DeliverFindingsContext(formats, outputDir, pipeline);
    }
}

public sealed class LoadSkillsContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ProjectConfig project, PipelineContext pipeline)
    {
        // Allow pipeline-specific override (e.g. security-scan sets skills/security)
        var skillsPath = pipeline.TryGet<string>(ContextKeys.SkillsPathOverride, out var overridePath)
            ? overridePath!
            : project.SkillsPath;
        return new LoadSkillsContext(skillsPath, pipeline);
    }
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

public sealed class ApiCodeContextContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ProjectConfig project, PipelineContext pipeline)
        => new ApiCodeContextCommandContext(pipeline);
}
