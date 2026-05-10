using AgentSmith.Application.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// Captures the per-skill-call trace (LLM rounds + tool calls), truncates large
/// argument JSON, and emits a structured INFO log on completion. Owns the
/// 200-char ArgsSummary contract.
/// </summary>
public sealed class LoopTraceCollector
{
    private const int MaxArgsSummaryChars = 200;
    private readonly List<LoopTraceEntry> _entries = new();

    public void AppendLlmCall(string model, long inputTokens, long outputTokens, long durationMs)
        => _entries.Add(LoopTraceEntry.LlmCall(model, inputTokens, outputTokens, durationMs));

    public void AppendToolCall(string toolName, string argsJson, long durationMs, bool success, string? errorMessage)
        => _entries.Add(LoopTraceEntry.ToolCall(
            toolName, TruncateArgs(argsJson), durationMs, success, errorMessage));

    public IReadOnlyList<LoopTraceEntry> Build() => _entries.ToArray();

    public void EmitLog(ILogger logger, string skillName)
    {
        var llm = _entries.Count(e => e.Kind == LoopTraceEntryKind.LlmCall);
        var tool = _entries.Count(e => e.Kind == LoopTraceEntryKind.ToolCall);
        var totalDuration = _entries.Sum(e => e.DurationMs);

        logger.LogInformation(
            "skill_call_trace skill={SkillName} llm_calls={LlmCalls} tool_calls={ToolCalls} duration_ms={DurationMs} entries={Entries}",
            skillName, llm, tool, totalDuration, FormatEntries());
    }

    private string FormatEntries() => string.Join(" | ", _entries.Select(FormatEntry));

    private static string FormatEntry(LoopTraceEntry e) => e.Kind switch
    {
        LoopTraceEntryKind.LlmCall => $"llm[{e.ModelName}:{e.InputTokens}/{e.OutputTokens}t,{e.DurationMs}ms]",
        LoopTraceEntryKind.ToolCall => $"tool[{e.ToolName}:{(e.Success == true ? "ok" : "err")},{e.DurationMs}ms]",
        _ => "?"
    };

    private static string TruncateArgs(string argsJson)
    {
        if (string.IsNullOrEmpty(argsJson) || argsJson.Length <= MaxArgsSummaryChars)
            return argsJson;

        return PreservesLeadingBoundary(argsJson)
            ? argsJson[0] + argsJson.Substring(1, MaxArgsSummaryChars - 2) + "…"
            : argsJson.Substring(0, MaxArgsSummaryChars - 1) + "…";
    }

    private static bool PreservesLeadingBoundary(string s) => s[0] == '{' || s[0] == '[';
}
