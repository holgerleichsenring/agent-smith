namespace AgentSmith.Contracts.Models.Workers;

/// <summary>
/// p0416: one message of the conversation as the worker sees it — the same role and the
/// same content parts the provider would have been sent, in the same order.
/// </summary>
public sealed record WorkerMessage(string Role, IReadOnlyList<WorkerContentPart> Content);
