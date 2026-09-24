using Microsoft.Extensions.AI;

namespace AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders.Copilot;

/// <summary>2026-09-07-d5f2: a turn's consumption in the shape the cost surfaces already read.</summary>
internal static class CopilotUsageMapping
{
    /// <summary>
/// Fills the shape LlmCallCostCalculator already reads — including
/// <see cref="UsageDetails.CachedInputTokenCount"/>, which is where the calculator looks for
/// cache reads, so a Copilot call's cached share is priced rather than silently counted twice.
/// The provider's own meter travels beside it as additional counts: premium requests and
/// nano-AI units are what a seat is actually billed in, and they belong in the trace even
/// though the domain prices tokens.
/// </summary>
internal static UsageDetails ToUsageDetails(CopilotUsage usage)
{
    var details = new UsageDetails
    {
        InputTokenCount = usage.InputTokens,
        OutputTokenCount = usage.OutputTokens,
        TotalTokenCount = usage.InputTokens + usage.OutputTokens,
        CachedInputTokenCount = usage.CacheReadTokens,
    };
    details.AdditionalCounts = new AdditionalPropertiesDictionary<long>
    {
        ["CopilotPremiumRequestCostMilli"] = (long)Math.Round(usage.PremiumRequestCost * 1000),
        ["CopilotNanoAiu"] = (long)Math.Round(usage.NanoAiu ?? 0),
    };
    return details;
}
}
