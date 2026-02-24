using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services;

/// <summary>
/// Maps command names (from YAML config) to typed ICommandContext records,
/// pulling required data from project configuration and pipeline state.
/// </summary>
public sealed class CommandContextFactory : ICommandContextFactory
{
    public ICommandContext Create(
        string commandName, ProjectConfig project, PipelineContext pipeline)
    {
        var initMode = pipeline.TryGet<bool>(ContextKeys.InitMode, out var isInit) && isInit;

        return commandName switch
        {
            CommandNames.FetchTicket => CreateFetchTicket(project, pipeline),
            CommandNames.CheckoutSource when initMode => CreateInitCheckoutSource(project, pipeline),
            CommandNames.CheckoutSource => CreateCheckoutSource(project, pipeline),
            CommandNames.LoadCodingPrinciples => CreateLoadCodingPrinciples(project, pipeline),
            CommandNames.LoadContext => CreateLoadContext(pipeline),
            CommandNames.AnalyzeCode => CreateAnalyzeCode(pipeline),
            CommandNames.GeneratePlan => CreateGeneratePlan(project, pipeline),
            CommandNames.Approval => CreateApproval(pipeline),
            CommandNames.AgenticExecute => CreateAgenticExecute(project, pipeline),
            CommandNames.Test => CreateTest(pipeline),
            CommandNames.WriteRunResult => CreateWriteRunResult(pipeline),
            CommandNames.CommitAndPR => CreateCommitAndPR(project, pipeline),
            CommandNames.InitCommit => CreateInitCommit(project, pipeline),
            CommandNames.BootstrapProject => CreateBootstrapProject(project, pipeline),
            CommandNames.LoadCodeMap => CreateLoadCodeMap(pipeline),
            _ => throw new ConfigurationException(
                $"Unknown command: '{commandName}'")
        };
    }

    private static FetchTicketContext CreateFetchTicket(
        ProjectConfig project, PipelineContext pipeline)
    {
        var ticketId = pipeline.Get<TicketId>(ContextKeys.TicketId);
        return new FetchTicketContext(ticketId, project.Tickets, pipeline);
    }

    private static CheckoutSourceContext CreateCheckoutSource(
        ProjectConfig project, PipelineContext pipeline)
    {
        var ticketId = pipeline.Get<TicketId>(ContextKeys.TicketId);
        var branch = BranchName.FromTicket(ticketId);
        return new CheckoutSourceContext(project.Source, branch, pipeline);
    }

    private static LoadCodingPrinciplesContext CreateLoadCodingPrinciples(
        ProjectConfig project, PipelineContext pipeline)
    {
        var path = project.CodingPrinciplesPath ?? ".agentsmith/coding-principles.md";
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        return new LoadCodingPrinciplesContext(path, repo, pipeline);
    }

    private static LoadContextContext CreateLoadContext(PipelineContext pipeline)
    {
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        return new LoadContextContext(repo, pipeline);
    }

    private static AnalyzeCodeContext CreateAnalyzeCode(PipelineContext pipeline)
    {
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        return new AnalyzeCodeContext(repo, pipeline);
    }

    private static GeneratePlanContext CreateGeneratePlan(
        ProjectConfig project, PipelineContext pipeline)
    {
        var ticket = pipeline.Get<Ticket>(ContextKeys.Ticket);
        var analysis = pipeline.Get<CodeAnalysis>(ContextKeys.CodeAnalysis);
        var principles = pipeline.Get<string>(ContextKeys.CodingPrinciples);
        pipeline.TryGet<string>(ContextKeys.CodeMap, out var codeMap);
        pipeline.TryGet<string>(ContextKeys.ProjectContext, out var projectContext);
        return new GeneratePlanContext(ticket, analysis, principles, project.Agent, pipeline, codeMap, projectContext);
    }

    private static ApprovalContext CreateApproval(PipelineContext pipeline)
    {
        var plan = pipeline.Get<Plan>(ContextKeys.Plan);
        return new ApprovalContext(plan, pipeline);
    }

    private static AgenticExecuteContext CreateAgenticExecute(
        ProjectConfig project, PipelineContext pipeline)
    {
        var plan = pipeline.Get<Plan>(ContextKeys.Plan);
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        var principles = pipeline.Get<string>(ContextKeys.CodingPrinciples);
        pipeline.TryGet<string>(ContextKeys.CodeMap, out var codeMap);
        pipeline.TryGet<string>(ContextKeys.ProjectContext, out var projectContext);
        return new AgenticExecuteContext(plan, repo, principles, project.Agent, pipeline, codeMap, projectContext);
    }

    private static TestContext CreateTest(PipelineContext pipeline)
    {
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        var changes = pipeline.Get<IReadOnlyList<CodeChange>>(ContextKeys.CodeChanges);
        return new TestContext(repo, changes, pipeline);
    }

    private static WriteRunResultContext CreateWriteRunResult(PipelineContext pipeline)
    {
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        var plan = pipeline.Get<Plan>(ContextKeys.Plan);
        var ticket = pipeline.Get<Ticket>(ContextKeys.Ticket);
        var changes = pipeline.Get<IReadOnlyList<CodeChange>>(ContextKeys.CodeChanges);
        return new WriteRunResultContext(repo, plan, ticket, changes, pipeline);
    }

    private static CommitAndPRContext CreateCommitAndPR(
        ProjectConfig project, PipelineContext pipeline)
    {
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        var changes = pipeline.Get<IReadOnlyList<CodeChange>>(ContextKeys.CodeChanges);
        var ticket = pipeline.Get<Ticket>(ContextKeys.Ticket);
        return new CommitAndPRContext(repo, changes, ticket, project.Source, project.Tickets, pipeline);
    }

    private static BootstrapProjectContext CreateBootstrapProject(
        ProjectConfig project, PipelineContext pipeline)
    {
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        return new BootstrapProjectContext(repo, project.Agent, pipeline);
    }

    private static LoadCodeMapContext CreateLoadCodeMap(PipelineContext pipeline)
    {
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        return new LoadCodeMapContext(repo, pipeline);
    }

    private static CheckoutSourceContext CreateInitCheckoutSource(
        ProjectConfig project, PipelineContext pipeline)
    {
        var branch = new BranchName("agentsmith/init");
        return new CheckoutSourceContext(project.Source, branch, pipeline);
    }

    private static InitCommitContext CreateInitCommit(
        ProjectConfig project, PipelineContext pipeline)
    {
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        return new InitCommitContext(repo, project.Source, project.Tickets, pipeline);
    }
}
