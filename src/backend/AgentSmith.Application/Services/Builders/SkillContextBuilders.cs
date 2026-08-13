using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

public sealed class SwitchSkillContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
    {
        var skillName = command.SkillName ?? string.Empty;
        return new SwitchSkillContext(skillName, pipeline);
    }
}

public sealed class BootstrapRoundContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
    {
        var skillName = command.SkillName ?? string.Empty;
        var repoName = command.RepoName ?? string.Empty;
        var contextName = command.ContextName ?? string.Empty;
        var workdir = command.Workdir ?? ".";
        return new BootstrapRoundContext(
            skillName, repoName, pipeline.Resolved().Agent, pipeline, contextName, workdir);
    }
}

public sealed class BootstrapDiscoverContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
    {
        var repoName = command.RepoName ?? string.Empty;
        return new BootstrapDiscoverContext(repoName, pipeline.Resolved().Agent, pipeline);
    }
}

/// <summary>
/// p0179b/d: builder for the AgenticMaster step. Master skill name resolution:
/// (1) PipelineCommand.SkillName when the caller named one explicitly,
/// (2) the per-pipeline default from <see cref="PipelinePresets.MasterFor"/> —
///     p0408 moved that table next to the presets so the generated control-flow
///     diagram resolves the same master the run loads.
/// </summary>
public sealed class AgenticMasterContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
    {
        var skillName = ResolveSkillName(command, pipeline);
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        var codingPrinciples = pipeline.TryGet<string>(ContextKeys.CodingPrinciples, out var cp)
            && cp is not null ? cp : string.Empty;
        var repoCodeMaps = pipeline.TryGet<IReadOnlyDictionary<string, string>>(
            ContextKeys.RepoCodeMaps, out var cm) ? cm : null;
        var projectContext = pipeline.TryGet<string>(ContextKeys.ProjectContext, out var pc) ? pc : null;
        return new AgenticMasterContext(
            MasterSkillName: skillName,
            Repository: repo,
            CodingPrinciples: codingPrinciples,
            AgentConfig: pipeline.Resolved().Agent,
            Pipeline: pipeline,
            RepoCodeMaps: repoCodeMaps,
            ProjectContext: projectContext);
    }

    private static string ResolveSkillName(PipelineCommand command, PipelineContext pipeline)
    {
        if (!string.IsNullOrWhiteSpace(command.SkillName))
            return command.SkillName;
        return pipeline.TryGet<string>(ContextKeys.PipelineName, out var pipelineName)
            && pipelineName is not null
                ? PipelinePresets.MasterFor(pipelineName)
                : PipelinePresets.CodingMaster;
    }
}
