using AgentSmith.Application.Commands.Contexts;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.ValueObjects;

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
        return commandName switch
        {
            "FetchTicketCommand" => CreateFetchTicket(project, pipeline),
            "CheckoutSourceCommand" => CreateCheckoutSource(project, pipeline),
            "LoadCodingPrinciplesCommand" => CreateLoadCodingPrinciples(project, pipeline),
            "AnalyzeCodeCommand" => CreateAnalyzeCode(pipeline),
            "GeneratePlanCommand" => CreateGeneratePlan(project, pipeline),
            "ApprovalCommand" => CreateApproval(pipeline),
            "AgenticExecuteCommand" => CreateAgenticExecute(project, pipeline),
            "TestCommand" => CreateTest(pipeline),
            "CommitAndPRCommand" => CreateCommitAndPR(project, pipeline),
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
        var path = project.CodingPrinciplesPath ?? "config/coding-principles.md";
        return new LoadCodingPrinciplesContext(path, pipeline);
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
        return new GeneratePlanContext(ticket, analysis, principles, project.Agent, pipeline);
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
        return new AgenticExecuteContext(plan, repo, principles, project.Agent, pipeline);
    }

    private static TestContext CreateTest(PipelineContext pipeline)
    {
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        var changes = pipeline.Get<IReadOnlyList<CodeChange>>(ContextKeys.CodeChanges);
        return new TestContext(repo, changes, pipeline);
    }

    private static CommitAndPRContext CreateCommitAndPR(
        ProjectConfig project, PipelineContext pipeline)
    {
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        var changes = pipeline.Get<IReadOnlyList<CodeChange>>(ContextKeys.CodeChanges);
        var ticket = pipeline.Get<Ticket>(ContextKeys.Ticket);
        return new CommitAndPRContext(repo, changes, ticket, project.Source, pipeline);
    }
}
