using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

public sealed class TriageContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
        => new TriageContext(pipeline.Resolved().Agent, pipeline);
}

public sealed class SwitchSkillContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
    {
        var skillName = command.SkillName ?? string.Empty;
        return new SwitchSkillContext(skillName, pipeline);
    }
}

public sealed class SkillRoundContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
    {
        var skillName = command.SkillName ?? string.Empty;
        var round = command.Round ?? 1;
        var repoName = command.RepoName ?? string.Empty;
        var contextName = command.ContextName ?? string.Empty;
        var workdir = command.Workdir ?? ".";
        return new SkillRoundContext(
            skillName, round, pipeline.Resolved().Agent, pipeline, repoName, contextName, workdir);
    }
}

public sealed class SecuritySkillRoundContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
    {
        var skillName = command.SkillName ?? string.Empty;
        var round = command.Round ?? 1;
        return new SecuritySkillRoundContext(skillName, round, pipeline.Resolved().Agent, pipeline);
    }
}

public sealed class ConvergenceCheckContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
        => new ConvergenceCheckContext(pipeline.Resolved().Agent, pipeline);
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

public sealed class CompileDiscussionContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
    {
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        return new CompileDiscussionContext(repo, pipeline);
    }
}

/// <summary>
/// p0179b/d: builder for the AgenticMaster step. Master skill name resolution:
/// (1) PipelineCommand.SkillName when the caller named one explicitly,
/// (2) per-pipeline default from PipelineName (security-scan → security-master,
///     api-security-scan → api-security-master, legal-analysis →
///     legal-analyst-master, anything else → coding-agent-master).
/// </summary>
public sealed class AgenticMasterContextBuilder : IContextBuilder
{
    private const string CodingDefault = "coding-agent-master";

    private static readonly IReadOnlyDictionary<string, string> PerPipelineDefault =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["security-scan"] = "security-master",
            ["api-security-scan"] = "api-security-master",
            ["legal-analysis"] = "legal-analyst-master",
            ["mad-discussion"] = "mad-discussion-master",
            [PipelinePresets.SpecDialogName] = "design-partner-master",
        };

    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
    {
        var skillName = ResolveSkillName(command, pipeline);
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        var codingPrinciples = pipeline.TryGet<string>(ContextKeys.CodingPrinciples, out var cp)
            && cp is not null ? cp : string.Empty;
        var codeMap = pipeline.TryGet<string>(ContextKeys.CodeMap, out var cm) ? cm : null;
        var projectContext = pipeline.TryGet<string>(ContextKeys.ProjectContext, out var pc) ? pc : null;
        return new AgenticMasterContext(
            MasterSkillName: skillName,
            Repository: repo,
            CodingPrinciples: codingPrinciples,
            AgentConfig: pipeline.Resolved().Agent,
            Pipeline: pipeline,
            CodeMap: codeMap,
            ProjectContext: projectContext);
    }

    private static string ResolveSkillName(PipelineCommand command, PipelineContext pipeline)
    {
        if (!string.IsNullOrWhiteSpace(command.SkillName))
            return command.SkillName;
        if (pipeline.TryGet<string>(ContextKeys.PipelineName, out var pipelineName)
            && pipelineName is not null
            && PerPipelineDefault.TryGetValue(pipelineName, out var perPipeline))
            return perPipeline;
        return CodingDefault;
    }
}
