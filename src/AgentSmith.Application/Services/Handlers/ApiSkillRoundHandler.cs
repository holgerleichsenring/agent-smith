using AgentSmith.Application.Models;
using AgentSmith.Application.Services.SkillRounds;
using AgentSmith.Application.Services.SkillRounds.Strategies;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// API-security skill round handler. Provides the Swagger specification +
/// per-skill API-scan findings as domain context. Used by the
/// api-security-scan pipeline.
/// </summary>
public sealed class ApiSkillRoundHandler(
    IDiscussionRoundExecutor discussionExecutor,
    IStructuredRoundExecutor structuredExecutor,
    ApiSkillPromptStrategy strategy,
    ILogger<ApiSkillRoundHandler> logger)
    : SkillRoundHandlerBase(discussionExecutor, structuredExecutor),
      ICommandHandler<ApiSecuritySkillRoundContext>
{
    protected override ILogger Logger => logger;
    protected override ISkillPromptStrategy Strategy => strategy;

    public async Task<CommandResult> ExecuteAsync(
        ApiSecuritySkillRoundContext context, CancellationToken cancellationToken)
    {
        context.Pipeline.Set(ContextKeys.AgentConfig, context.AgentConfig);
        return await ExecuteRoundAsync(
            context.SkillName, context.Round, context.Pipeline, cancellationToken);
    }
}
