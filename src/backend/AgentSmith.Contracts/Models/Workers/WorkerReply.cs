namespace AgentSmith.Contracts.Models.Workers;

/// <summary>
/// p0416: the worker's answer to one <see cref="WorkerRequest"/> — assistant text, tool
/// calls, or both, exactly as a provider would have answered. <see cref="Error"/> lets a
/// worker refuse a call explicitly instead of returning something the loop would then
/// reason about as if it were a model answer.
/// </summary>
public sealed record WorkerReply(
    string? Text = null,
    IReadOnlyList<WorkerToolCall>? ToolCalls = null,
    string? Error = null);
