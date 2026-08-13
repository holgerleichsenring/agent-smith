using System.Text.Json;

namespace AgentSmith.Contracts.Models.Workers;

/// <summary>
/// p0416: one tool as the provider would have been offered it — name, description and
/// the JSON schema of its arguments, verbatim from the <c>AIFunction</c>. The schema is
/// what makes the worker answer with arguments the framework can execute; without it the
/// worker is guessing, and a run driven by guesses proves nothing.
/// </summary>
public sealed record WorkerToolDefinition(string Name, string? Description, JsonElement? InputSchema);
