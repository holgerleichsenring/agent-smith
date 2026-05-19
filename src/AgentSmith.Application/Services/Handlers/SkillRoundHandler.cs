using AgentSmith.Application.Models;
using AgentSmith.Application.Services.SkillRounds;
using AgentSmith.Application.Services.SkillRounds.Strategies;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Skill round used by ticket-driven pipelines (fix-bug, add-feature, MAD)
/// AND by the bootstrap path (init-project, where no ticket exists). Provides
/// ticket title + description as domain context when a ticket is in scope;
/// otherwise renders a synthetic "no ticket" block so bootstrap-style skills
/// can run without a ticket.
/// </summary>
public sealed class SkillRoundHandler(
    IDiscussionRoundExecutor discussionExecutor,
    IStructuredRoundExecutor structuredExecutor,
    DefaultSkillPromptStrategy strategy,
    ILogger<SkillRoundHandler> logger)
    : SkillRoundHandlerBase(discussionExecutor, structuredExecutor),
      ICommandHandler<SkillRoundContext>
{
    protected override ILogger Logger => logger;
    protected override ISkillPromptStrategy Strategy => strategy;

    public async Task<CommandResult> ExecuteAsync(
        SkillRoundContext context, CancellationToken cancellationToken)
    {
        context.Pipeline.Set(ContextKeys.AgentConfig, context.AgentConfig);
        return await ExecuteRoundAsync(
            context.SkillName, context.Round, context.Pipeline, cancellationToken);
    }
}
