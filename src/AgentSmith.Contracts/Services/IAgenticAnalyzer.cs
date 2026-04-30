namespace AgentSmith.Contracts.Services;

/// <summary>
/// Provider-agnostic agentic-loop runner. Takes a system + user prompt and
/// a tool set; iterates until the model produces a non-tool-use response or
/// maxIterations is reached. Tool calls are dispatched through the supplied
/// IToolCallHandler so consumers control which side-effects are allowed.
/// </summary>
public interface IAgenticAnalyzer
{
    Task<AnalysisResult> AnalyzeAsync(
        string systemPrompt,
        string userPrompt,
        IReadOnlyList<ToolDefinition> tools,
        IToolCallHandler handler,
        int maxIterations,
        CancellationToken cancellationToken);
}
