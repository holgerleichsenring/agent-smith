using AgentSmith.PipelineHarness.Llm;
using FluentAssertions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0199: read-side helpers for tool-call SHAPE assertions. Tests assert
/// on the (tool name, args) tuples the scripted client emitted — not on
/// the master's prose, not on the assistant message body. Pinning prose
/// makes tests change-detectors that break on every prompt iteration and
/// get silenced; SHAPE is the behavioural contract.
/// </summary>
internal static class ToolCallShapeAssertions
{
    public static void ShouldHaveCalledInOrder(
        this IReadOnlyList<ScriptedToolCall> calls, params string[] expectedNames)
    {
        calls.Select(c => c.Name).Should().Equal(
            expectedNames,
            "the master must drive its tool surface in the documented order");
    }

    public static ScriptedToolCall First(
        this IReadOnlyList<ScriptedToolCall> calls, string toolName) =>
        calls.FirstOrDefault(c => string.Equals(c.Name, toolName, StringComparison.Ordinal))
        ?? throw new InvalidOperationException(
            $"No tool call named '{toolName}' was emitted. Emitted: " +
            $"[{string.Join(", ", calls.Select(c => c.Name))}].");

    public static string StringArg(this ScriptedToolCall call, string argName)
    {
        if (!call.Arguments.TryGetValue(argName, out var raw) || raw is null)
            throw new InvalidOperationException(
                $"Tool call '{call.Name}' missing string argument '{argName}'.");
        return raw.ToString() ?? string.Empty;
    }
}
