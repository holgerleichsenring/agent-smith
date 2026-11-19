using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>2026-11-18-9c1cd: whether a surface's tool takes a parameter — a merged tool's folded job shows as one.</summary>
internal static class ToolSchema
{
    internal static bool Takes(this IEnumerable<AITool> tools, string tool, string parameter) =>
        tools.OfType<AIFunction>().SingleOrDefault(t => t.Name == tool) is { } function
        && function.JsonSchema.TryGetProperty("properties", out var properties)
        && properties.TryGetProperty(parameter, out _);
}
