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
    ILogger<SkillRoundHandler> logger)
    : SkillRoundHandlerBase, ICommandHandler<SkillRoundContext>
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

/// <summary>
/// A single entry in the multi-role plan discussion log.
/// </summary>
public sealed record DiscussionEntry(
    string RoleName,
    string DisplayName,
    string Emoji,
    int Round,
    string Content);
