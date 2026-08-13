using System.Text.Json;

namespace AgentSmith.Contracts.Models.Workers;

/// <summary>
/// p0416: one tool call in a worker's reply. <c>CallId</c> is optional — the bridge
/// assigns one when the worker omits it, because correlating calls with results is the
/// framework's bookkeeping, not the worker's.
/// </summary>
public sealed record WorkerToolCall(string Name, JsonElement Arguments, string? CallId = null);
