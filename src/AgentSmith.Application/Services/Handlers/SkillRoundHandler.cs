using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Ticket-based skill round: provides ticket title + description as domain context.
/// Used by fix-bug, add-feature, MAD pipelines.
/// </summary>
public sealed class SkillRoundHandler(
    ILlmClientFactory llmClientFactory,
    ISkillPromptBuilder promptBuilder,
    IGateRetryCoordinator gateRetryCoordinator,
    IUpstreamContextBuilder upstreamContextBuilder,
    ILogger<SkillRoundHandler> logger)
    : SkillRoundHandlerBase(promptBuilder, gateRetryCoordinator, upstreamContextBuilder),
      ICommandHandler<SkillRoundContext>
{
    protected override ILogger Logger => logger;

    protected override string BuildDomainSection(PipelineContext pipeline)
    {
        var ticket = pipeline.Get<Ticket>(ContextKeys.Ticket);
        return $"""
            ## Ticket
            {ticket.Title}
            {ticket.Description}
            """;
    }

    public async Task<CommandResult> ExecuteAsync(
        SkillRoundContext context, CancellationToken cancellationToken)
    {
        var llmClient = llmClientFactory.Create(context.AgentConfig);
        return await ExecuteRoundAsync(
            context.SkillName, context.Round, context.Pipeline, llmClient, cancellationToken);
    }
}
