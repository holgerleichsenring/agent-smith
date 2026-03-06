using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

public sealed class AnalyzeCodeContextBuilder : IContextBuilder
{
    public ICommandContext Build(string commandName, ProjectConfig project, PipelineContext pipeline)
    {
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        return new AnalyzeCodeContext(repo, pipeline);
    }
}

public sealed class GeneratePlanContextBuilder : IContextBuilder
{
    public ICommandContext Build(string commandName, ProjectConfig project, PipelineContext pipeline)
    {
        var ticket = pipeline.Get<Ticket>(ContextKeys.Ticket);
        var analysis = pipeline.Get<CodeAnalysis>(ContextKeys.CodeAnalysis);
        var principles = pipeline.Get<string>(ContextKeys.CodingPrinciples);
        pipeline.TryGet<string>(ContextKeys.CodeMap, out var codeMap);
        pipeline.TryGet<string>(ContextKeys.ProjectContext, out var projectContext);
        return new GeneratePlanContext(ticket, analysis, principles, project.Agent, pipeline, codeMap, projectContext);
    }
}

public sealed class ApprovalContextBuilder : IContextBuilder
{
    public ICommandContext Build(string commandName, ProjectConfig project, PipelineContext pipeline)
    {
        var plan = pipeline.Get<Plan>(ContextKeys.Plan);
        return new ApprovalContext(plan, pipeline);
    }
}

public sealed class AgenticExecuteContextBuilder : IContextBuilder
{
    public ICommandContext Build(string commandName, ProjectConfig project, PipelineContext pipeline)
    {
        var plan = pipeline.Get<Plan>(ContextKeys.Plan);
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        var principles = pipeline.Get<string>(ContextKeys.CodingPrinciples);
        pipeline.TryGet<string>(ContextKeys.CodeMap, out var codeMap);
        pipeline.TryGet<string>(ContextKeys.ProjectContext, out var projectContext);
        return new AgenticExecuteContext(plan, repo, principles, project.Agent, pipeline, codeMap, projectContext);
    }
}

public sealed class TestContextBuilder : IContextBuilder
{
    public ICommandContext Build(string commandName, ProjectConfig project, PipelineContext pipeline)
    {
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        var changes = pipeline.Get<IReadOnlyList<CodeChange>>(ContextKeys.CodeChanges);
        return new TestContext(repo, changes, pipeline);
    }
}

public sealed class WriteRunResultContextBuilder : IContextBuilder
{
    public ICommandContext Build(string commandName, ProjectConfig project, PipelineContext pipeline)
    {
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        var plan = pipeline.Get<Plan>(ContextKeys.Plan);
        var ticket = pipeline.Get<Ticket>(ContextKeys.Ticket);
        var changes = pipeline.Get<IReadOnlyList<CodeChange>>(ContextKeys.CodeChanges);
        return new WriteRunResultContext(repo, plan, ticket, changes, pipeline);
    }
}

public sealed class CommitAndPRContextBuilder : IContextBuilder
{
    public ICommandContext Build(string commandName, ProjectConfig project, PipelineContext pipeline)
    {
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        var changes = pipeline.Get<IReadOnlyList<CodeChange>>(ContextKeys.CodeChanges);
        var ticket = pipeline.Get<Ticket>(ContextKeys.Ticket);
        return new CommitAndPRContext(repo, changes, ticket, project.Source, project.Tickets, pipeline);
    }
}

public sealed class InitCommitContextBuilder : IContextBuilder
{
    public ICommandContext Build(string commandName, ProjectConfig project, PipelineContext pipeline)
    {
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        return new InitCommitContext(repo, project.Source, project.Tickets, pipeline);
    }
}

public sealed class GenerateTestsContextBuilder : IContextBuilder
{
    public ICommandContext Build(string commandName, ProjectConfig project, PipelineContext pipeline)
    {
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        var changes = pipeline.Get<IReadOnlyList<CodeChange>>(ContextKeys.CodeChanges);
        var principles = pipeline.Get<string>(ContextKeys.CodingPrinciples);
        pipeline.TryGet<string>(ContextKeys.CodeMap, out var codeMap);
        pipeline.TryGet<string>(ContextKeys.ProjectContext, out var projectContext);
        return new GenerateTestsContext(repo, changes, principles, project.Agent, pipeline, codeMap, projectContext);
    }
}

public sealed class GenerateDocsContextBuilder : IContextBuilder
{
    public ICommandContext Build(string commandName, ProjectConfig project, PipelineContext pipeline)
    {
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        var changes = pipeline.Get<IReadOnlyList<CodeChange>>(ContextKeys.CodeChanges);
        var principles = pipeline.Get<string>(ContextKeys.CodingPrinciples);
        pipeline.TryGet<string>(ContextKeys.CodeMap, out var codeMap);
        pipeline.TryGet<string>(ContextKeys.ProjectContext, out var projectContext);
        return new GenerateDocsContext(repo, changes, principles, project.Agent, pipeline, codeMap, projectContext);
    }
}
