using AgentSmith.Application.Models;
using AgentSmith.Application.Services.SkillRounds;
using AgentSmith.Application.Services.SkillRounds.Strategies;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// pr-review skill round: provides the PR coordinates + structured diff as
/// domain context so the review skills (style / correctness / test-coverage /
/// security-overlap) emit observations anchored with file + line_range.
/// Used by the pr-review pipeline.
/// </summary>
public sealed class PrReviewSkillRoundHandler(
    IDiscussionRoundExecutor discussionExecutor,
    IStructuredRoundExecutor structuredExecutor,
    PrReviewSkillPromptStrategy strategy,
    DiscussionRoundToolPolicy toolPolicy,
    ILogger<PrReviewSkillRoundHandler> logger)
    : SkillRoundHandlerBase(discussionExecutor, structuredExecutor),
      ICommandHandler<PrReviewSkillRoundContext>
{
    protected override ILogger Logger => logger;
    protected override ISkillPromptStrategy Strategy => strategy;
    protected override ISkillRoundToolPolicy ToolPolicy => toolPolicy;

    public async Task<CommandResult> ExecuteAsync(
        PrReviewSkillRoundContext context, CancellationToken cancellationToken)
    {
        context.Pipeline.Set(ContextKeys.AgentConfig, context.AgentConfig);
        return await ExecuteRoundAsync(
            context.SkillName, context.Round, context.Pipeline, cancellationToken);
    }
}
