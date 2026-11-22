namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-11-22-66e4a: what a call asks of its deployment's budget — its input estimate, its output cap and who to
/// tell when it has to wait. <paramref name="OnWait"/> is told once, with the expected wait and whether a provider
/// refusal pauses the deployment, before the call waits a second or more. 2026-11-22-ab45: <paramref name="OnShortfall"/> is
/// told, before any wait, when the call needs more than the deployment's whole minute.
/// </summary>
public sealed record LlmQuotaDemand(
    int EstimatedInputTokens,
    int MaxOutputTokens,
    Func<TimeSpan, bool, CancellationToken, Task>? OnWait = null,
    Func<QuotaShortfall, CancellationToken, Task>? OnShortfall = null);
