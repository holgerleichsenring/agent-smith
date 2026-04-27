using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Triages the API security scan: filters the skill pool by mode + source,
/// then delegates to the deterministic triage builder.
/// </summary>
public sealed class ApiSecurityTriageHandler(
    ILlmClientFactory llmClientFactory,
    ISkillGraphBuilder skillGraphBuilder,
    ApiSecurityTriagePromptBuilder promptBuilder,
    ApiSecuritySkillFilter skillFilter,
    ILogger<ApiSecurityTriageHandler> logger)
    : TriageHandlerBase, ICommandHandler<ApiSecurityTriageContext>
{
    protected override ILogger Logger => logger;
    protected override string SkillRoundCommandName => "ApiSecuritySkillRoundCommand";
    protected override ISkillGraphBuilder? GraphBuilder => skillGraphBuilder;

    protected override string BuildUserPrompt(PipelineContext pipeline) =>
        promptBuilder.Build(pipeline);

    public async Task<CommandResult> ExecuteAsync(
        ApiSecurityTriageContext context, CancellationToken cancellationToken)
    {
        var pipeline = context.Pipeline;
        var activeMode = pipeline.TryGet<bool>(ContextKeys.ActiveMode, out var a) && a;
        var sourceAvailable = pipeline.TryGet<bool>(ContextKeys.ApiSourceAvailable, out var s) && s;

        if (pipeline.TryGet<bool>(ContextKeys.ZapFailed, out var zapFailed) && zapFailed)
            logger.LogInformation("ZAP failed — DAST findings unavailable");

        ApplySkillFilter(pipeline, activeMode, sourceAvailable);

        var llmClient = llmClientFactory.Create(context.AgentConfig);
        return await TriageAsync(pipeline, llmClient, cancellationToken);
    }

    private void ApplySkillFilter(PipelineContext pipeline, bool activeMode, bool sourceAvailable)
    {
        if (!pipeline.TryGet<IReadOnlyList<RoleSkillDefinition>>(
                ContextKeys.AvailableRoles, out var roles) || roles is null) return;

        var filtered = skillFilter.Filter(roles, activeMode, sourceAvailable);
        if (filtered.Count == roles.Count) return;

        var dropped = roles.Select(r => r.Name).Except(filtered.Select(r => r.Name));
        logger.LogInformation(
            "API skill pool: active={Active}, source={Source} — kept {Kept}, dropped [{Dropped}]",
            activeMode, sourceAvailable, filtered.Count, string.Join(", ", dropped));
        pipeline.Set(ContextKeys.AvailableRoles, filtered);
    }
}
