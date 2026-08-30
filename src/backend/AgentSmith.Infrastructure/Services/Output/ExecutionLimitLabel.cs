using AgentSmith.Contracts.Models;

namespace AgentSmith.Infrastructure.Services.Output;

/// <summary>
/// The operator-facing name of an execution-limit category, for the stdout strategies that
/// list limit hits beside the findings. Extracted verbatim from ConsoleOutputStrategy and
/// SummaryOutputStrategy, which carried the same switch twice.
/// </summary>
public static class ExecutionLimitLabel
{
    public static string For(string? category) => category switch
    {
        ExecutionLimitCategories.ExecutionLimitToolCalls => "tool-call limit",
        ExecutionLimitCategories.ExecutionLimitTokens => "token limit",
        ExecutionLimitCategories.ExecutionLimitWallClock => "wall-clock limit",
        ExecutionLimitCategories.ExecutionError => "runtime error",
        ExecutionLimitCategories.CostCapExhausted => "cost cap",
        ExecutionLimitCategories.ExecutionParseFailure => "parse failure",
        _ => "execution limit"
    };
}
