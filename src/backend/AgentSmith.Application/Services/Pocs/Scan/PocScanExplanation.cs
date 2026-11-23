using AgentSmith.Application.Models;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Pocs.Scan;

/// <summary>
/// 2026-11-23-933dc: one call with the poc- project's agent, for a BLOCKED verdict only, over the masked findings (the
/// prompt passes through the run's secret masker as well). The answer is untrusted text stored beside the verdict; no
/// rule reads it. A failed call leaves no explanation and names why; the verdict stands.
/// </summary>
public sealed class PocScanExplanation(
    IChatClientFactory chats, PocScanPrompt prompts, SecretMasker masker, IRunContextAccessor runContext,
    ILogger<PocScanExplanation> logger)
{
    public const string RoleName = "poc-scan-explainer";

    public async Task<(string? Text, string? Failure)> ExplainAsync(
        string pocName, PocScanVerdict verdict, AgentConfig agent, PipelineCostTracker costs, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(verdict);
        ArgumentNullException.ThrowIfNull(costs);
        if (!verdict.IsBlocked) return (null, null);
        try
        {
            var chat = chats.Create(agent, TaskType.Summarization);
            using var _ = costs.BeginCall(RoleName, RoleName, SkillExecutionPhase.Verify, RoleName);
            using var _scope = runContext.BeginCallScope(RoleName, SkillExecutionPhase.Verify.ToString(), RoleName);
            var response = await chat.GetResponseAsync(
                [new ChatMessage(ChatRole.User, masker.Apply(prompts.For(pocName, verdict), SecretMasker.ProviderPlaceholder))],
                new ChatOptions { MaxOutputTokens = chats.GetMaxOutputTokens(agent, TaskType.Summarization) }, ct);
            costs.Track(response);
            return (response.Text, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The PoC scan explanation failed; the verdict stands without one");
            return (null, ex.Message);
        }
    }
}
