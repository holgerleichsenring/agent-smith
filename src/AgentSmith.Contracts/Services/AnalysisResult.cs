namespace AgentSmith.Contracts.Services;

/// <summary>
/// Outcome of an agentic-analyzer run. FinalText is the model's terminal
/// non-tool-use response — typically the structured JSON the consumer parses.
/// </summary>
public sealed record AnalysisResult(
    string FinalText,
    int Iterations,
    int ToolCallCount,
    AnalyzerTokenUsage Tokens);

public sealed record AnalyzerTokenUsage(
    int Input,
    int Output,
    int CacheRead = 0,
    int CacheCreate = 0);
