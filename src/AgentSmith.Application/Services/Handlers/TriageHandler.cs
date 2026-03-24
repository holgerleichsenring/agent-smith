using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Triages based on ticket description to determine which specialist roles
/// should participate in planning. Used by fix-bug, add-feature, MAD pipelines.
/// </summary>
public sealed class TriageHandler(
    ILlmClientFactory llmClientFactory,
    ILogger<TriageHandler> logger)
    : TriageHandlerBase, ICommandHandler<TriageContext>
{
    protected override ILogger Logger => logger;

    protected override string BuildUserPrompt(PipelineContext pipeline)
    {
        var ticket = pipeline.Get<Ticket>(ContextKeys.Ticket);
        pipeline.TryGet<string>(ContextKeys.ProjectContext, out var projectContext);

        return $"""
            ## Ticket
            {ticket.Title}
            {ticket.Description}

            ## Project Context
            {projectContext ?? "Not available"}
            """;
    }

    public async Task<CommandResult> ExecuteAsync(
        TriageContext context, CancellationToken cancellationToken)
    {
        var llmClient = llmClientFactory.Create(context.AgentConfig);
        return await TriageAsync(context.Pipeline, llmClient, cancellationToken);
    }
}
