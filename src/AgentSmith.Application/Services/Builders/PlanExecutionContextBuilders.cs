using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

public sealed class AnalyzeCodeContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
    {
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        return new AnalyzeCodeContext(repo, pipeline);
    }
}

public sealed class GeneratePlanContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
    {
        var ticket = pipeline.Get<Ticket>(ContextKeys.Ticket);
        var projectMap = pipeline.Get<ProjectMap>(ContextKeys.ProjectMap);
        var principles = pipeline.Get<string>(ContextKeys.CodingPrinciples);
        pipeline.TryGet<string>(ContextKeys.CodeMap, out var codeMap);
        pipeline.TryGet<string>(ContextKeys.ProjectContext, out var projectContext);
        return new GeneratePlanContext(ticket, projectMap, principles, pipeline.Resolved().Agent, pipeline, codeMap, projectContext);
    }
}

public sealed class EmptyPlanCheckContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
        => new EmptyPlanCheckContext(pipeline);
}

public sealed class ApprovalContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
    {
        var plan = pipeline.Get<Plan>(ContextKeys.Plan);
        return new ApprovalContext(plan, pipeline);
    }
}

public sealed class AgenticExecuteContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
    {
        var plan = pipeline.Get<Plan>(ContextKeys.Plan);
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        var principles = pipeline.Get<string>(ContextKeys.CodingPrinciples);
        pipeline.TryGet<string>(ContextKeys.CodeMap, out var codeMap);
        pipeline.TryGet<string>(ContextKeys.ProjectContext, out var projectContext);
        return new AgenticExecuteContext(plan, repo, principles, pipeline.Resolved().Agent, pipeline, codeMap, projectContext);
    }
}

public sealed class TestContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
    {
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        var changes = pipeline.Get<IReadOnlyList<CodeChange>>(ContextKeys.CodeChanges);
        return new TestContext(repo, changes, pipeline);
    }
}

public sealed class WriteRunResultContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
    {
        // p0130c-followup: Plan and Ticket are optional. Init-project routes
        // through this handler (per p0130c) but has neither. Changes defaults
        // to empty when no implementer ran (init / mad-discussion / etc.).
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        pipeline.TryGet<Plan>(ContextKeys.Plan, out var plan);
        pipeline.TryGet<Ticket>(ContextKeys.Ticket, out var ticket);
        var changes = pipeline.TryGet<IReadOnlyList<CodeChange>>(ContextKeys.CodeChanges, out var c) && c is not null
            ? c
            : Array.Empty<CodeChange>();
        return new WriteRunResultContext(repo, plan, ticket, changes, pipeline);
    }
}

public sealed class CommitAndPRContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
    {
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        var repos = pipeline.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos);
        var changes = pipeline.Get<IReadOnlyList<CodeChange>>(ContextKeys.CodeChanges);
        var ticket = pipeline.Get<Ticket>(ContextKeys.Ticket);
        return new CommitAndPRContext(repo, changes, ticket, repos[0], project.Tracker, pipeline);
    }
}

public sealed class InitCommitContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
    {
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        var repos = pipeline.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos);
        return new InitCommitContext(repo, repos[0], project.Tracker, pipeline);
    }
}

public sealed class GenerateTestsContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
    {
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        var changes = pipeline.Get<IReadOnlyList<CodeChange>>(ContextKeys.CodeChanges);
        var principles = pipeline.Get<string>(ContextKeys.CodingPrinciples);
        pipeline.TryGet<string>(ContextKeys.CodeMap, out var codeMap);
        pipeline.TryGet<string>(ContextKeys.ProjectContext, out var projectContext);
        return new GenerateTestsContext(repo, changes, principles, pipeline.Resolved().Agent, pipeline, codeMap, projectContext);
    }
}

public sealed class GenerateDocsContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
    {
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        var changes = pipeline.Get<IReadOnlyList<CodeChange>>(ContextKeys.CodeChanges);
        var principles = pipeline.Get<string>(ContextKeys.CodingPrinciples);
        pipeline.TryGet<string>(ContextKeys.CodeMap, out var codeMap);
        pipeline.TryGet<string>(ContextKeys.ProjectContext, out var projectContext);
        return new GenerateDocsContext(repo, changes, principles, pipeline.Resolved().Agent, pipeline, codeMap, projectContext);
    }
}
