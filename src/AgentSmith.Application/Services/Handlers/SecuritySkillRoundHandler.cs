using AgentSmith.Application.Models;
using AgentSmith.Application.Services.SkillRounds;
using AgentSmith.Application.Services.SkillRounds.Strategies;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Security-scan skill round: provides code analysis as domain context. For
/// chain-analyst (executor), also provides all commodity tool findings. Used
/// by the security-scan pipeline.
/// </summary>
public sealed class SecuritySkillRoundHandler(
    IDiscussionRoundExecutor discussionExecutor,
    IStructuredRoundExecutor structuredExecutor,
    SecuritySkillPromptStrategy strategy,
    StructuredRoundToolPolicy toolPolicy,
    ILogger<SecuritySkillRoundHandler> logger)
    : SkillRoundHandlerBase(discussionExecutor, structuredExecutor),
      ICommandHandler<SecuritySkillRoundContext>
{
    protected override ILogger Logger => logger;
    protected override ISkillPromptStrategy Strategy => strategy;
    protected override ISkillRoundToolPolicy ToolPolicy => toolPolicy;

    public async Task<CommandResult> ExecuteAsync(
        SecuritySkillRoundContext context, CancellationToken cancellationToken)
    {
        context.Pipeline.Set(ContextKeys.AgentConfig, context.AgentConfig);
        return await ExecuteRoundAsync(
            context.SkillName, context.Round, context.Pipeline, cancellationToken);
    }
}
