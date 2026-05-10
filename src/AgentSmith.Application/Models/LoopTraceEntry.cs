namespace AgentSmith.Application.Models;

/// <summary>
/// Single entry in the per-skill-call loop trace. Discriminated via <see cref="Kind"/>;
/// LlmCall variant fills ModelName/InputTokens/OutputTokens; ToolCall variant fills
/// ToolName/ArgsSummary/Success/ErrorMessage. Flat shape to keep JSON serialization
/// trivial (no polymorphic deserialization).
/// </summary>
public sealed record LoopTraceEntry
{
    public required LoopTraceEntryKind Kind { get; init; }
    public long DurationMs { get; init; }

    public string? ModelName { get; init; }
    public long? InputTokens { get; init; }
    public long? OutputTokens { get; init; }

    public string? ToolName { get; init; }
    public string? ArgsSummary { get; init; }
    public bool? Success { get; init; }
    public string? ErrorMessage { get; init; }

    public static LoopTraceEntry LlmCall(string modelName, long inputTokens, long outputTokens, long durationMs)
        => new()
        {
            Kind = LoopTraceEntryKind.LlmCall,
            ModelName = modelName,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            DurationMs = durationMs
        };

    public static LoopTraceEntry ToolCall(string toolName, string argsSummary, long durationMs, bool success, string? errorMessage)
        => new()
        {
            Kind = LoopTraceEntryKind.ToolCall,
            ToolName = toolName,
            ArgsSummary = argsSummary,
            Success = success,
            ErrorMessage = errorMessage,
            DurationMs = durationMs
        };
}
