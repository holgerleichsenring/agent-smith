namespace AgentSmith.Contracts.Models.Workers;

/// <summary>
/// p0416: the sampling options of the call. <see cref="NotRendered"/> is the honesty
/// field: every <c>ChatOptions</c> property that was SET by the caller but has no place
/// in this envelope is named here, so a fidelity gap between what the provider would
/// receive and what the worker receives is visible in the payload itself instead of
/// living in a comment nobody reads.
/// </summary>
public sealed record WorkerRequestOptions(
    int? MaxOutputTokens = null,
    float? Temperature = null,
    float? TopP = null,
    IReadOnlyList<string>? StopSequences = null,
    string? ToolMode = null,
    bool? AllowMultipleToolCalls = null,
    string? ResponseFormat = null,
    IReadOnlyList<string>? NotRendered = null);
