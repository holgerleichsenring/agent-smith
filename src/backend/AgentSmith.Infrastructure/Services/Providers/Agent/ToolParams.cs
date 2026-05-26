using System.Text.Json.Nodes;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// Shared parameter extraction and validation for tool handlers.
/// </summary>
internal static class ToolParams
{
    public static string GetString(JsonNode? input, string name)
    {
        var value = input?[name]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"Missing required parameter: {name}");
        return value;
    }

    public static void ValidatePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path must not be empty.");
        if (Path.IsPathRooted(path))
            throw new ArgumentException("Absolute paths are not allowed.");
        if (path.Contains("..", StringComparison.Ordinal))
            throw new ArgumentException("Path traversal (..) is not allowed.");
    }
}
