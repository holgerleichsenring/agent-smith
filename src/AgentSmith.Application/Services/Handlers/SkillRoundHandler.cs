using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Skill round used by ticket-driven pipelines (fix-bug, add-feature, MAD)
/// AND by the bootstrap path (init-project, where no ticket exists).
/// Provides ticket title + description as domain context when a ticket is
/// in scope; otherwise renders a synthetic "no ticket" block so
/// bootstrap-style skills (csharp-bootstrap, ...) can run without a ticket.
/// </summary>
public sealed class SkillRoundHandler(
    IChatClientFactory chatClientFactory,
    ISkillPromptBuilder promptBuilder,
    IGateRetryCoordinator gateRetryCoordinator,
    IUpstreamContextBuilder upstreamContextBuilder,
    StructuredOutputInstructionBuilder instructionBuilder,
    ILogger<SkillRoundHandler> logger)
    : SkillRoundHandlerBase(promptBuilder, gateRetryCoordinator, upstreamContextBuilder, instructionBuilder, chatClientFactory),
      ICommandHandler<SkillRoundContext>
{
    protected override ILogger Logger => logger;

    protected override string BuildDomainSection(PipelineContext pipeline)
    {
        // p0125c-followup: init-project routes through SkillRound (per p0130c)
        // but has no ticket — the ticket-driven presets tell operators "run
        // init-project first" so init-project MUST work without a ticket.
        // Fall back to a synthetic "no ticket" block, mirroring what
        // TriageOutputProducer.ResolveTicketOrSyntheticInput already does
        // for the same class of bootstrap / scan runs.
        if (!pipeline.TryGet<Ticket>(ContextKeys.Ticket, out var ticket) || ticket is null)
            return """
                ## Ticket
                (no ticket — bootstrap or ticketless scan run; rely on the
                ProjectMap and Repository context to ground the response)
                """;
        return $"""
            ## Ticket
            {ticket.Title}
            {ticket.Description}
            """;
    }

    public async Task<CommandResult> ExecuteAsync(
        SkillRoundContext context, CancellationToken cancellationToken)
    {
        context.Pipeline.Set(ContextKeys.AgentConfig, context.AgentConfig);
        return await ExecuteRoundAsync(
            context.SkillName, context.Round, context.Pipeline, cancellationToken);
    }
}
