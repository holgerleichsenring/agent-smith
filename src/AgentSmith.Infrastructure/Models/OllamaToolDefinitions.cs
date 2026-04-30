using System.Text.Json.Nodes;

namespace AgentSmith.Infrastructure.Models;

/// <summary>
/// Tool definitions in OpenAI-compatible JSON format for Ollama.
/// Same tools as OpenAiToolDefinitions but as raw JSON (no OpenAI SDK dependency).
/// </summary>
public static class OllamaToolDefinitions
{
    public static JsonArray All => new(
        ReadFile(), WriteFile(), ListFiles(), Grep(), RunCommand(), LogDecision());

    private static JsonObject ReadFile() => Fn("read_file",
        "Read the contents of a file in the repository.",
        Props(("path", "string", "Relative path to the file")),
        ["path"]);

    private static JsonObject WriteFile() => Fn("write_file",
        "Write or overwrite a file in the repository.",
        Props(("path", "string", "Relative path"), ("content", "string", "File content")),
        ["path", "content"]);

    private static JsonObject ListFiles() => Fn("list_files",
        "List files and directories at the given path.",
        Props(("path", "string", "Relative path (default: root)")),
        []);

    private static JsonObject Grep() => Fn("grep",
        "Search files for a regex pattern. Returns up to 200 matching lines as JSON.",
        Props(
            ("pattern", "string", "Regex pattern to match"),
            ("glob", "string", "Optional glob to limit files (default '**/*')")),
        ["pattern"]);

    private static JsonObject RunCommand() => Fn("run_command",
        "Run a shell command in the repository directory.",
        Props(("command", "string", "Shell command to execute")),
        ["command"]);

    private static JsonObject LogDecision() => Fn("log_decision",
        "Log an architectural, tooling, or implementation decision.",
        new JsonObject
        {
            ["category"] = new JsonObject
            {
                ["type"] = "string",
                ["enum"] = new JsonArray("Architecture", "Tooling", "Implementation", "TradeOff"),
                ["description"] = "Decision category."
            },
            ["decision"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Format: '**DecisionName**: reason why, not what'"
            }
        },
        ["category", "decision"]);

    private static JsonObject Fn(string name, string desc, JsonObject props, string[] required)
    {
        return new JsonObject
        {
            ["type"] = "function",
            ["function"] = new JsonObject
            {
                ["name"] = name,
                ["description"] = desc,
                ["parameters"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = props,
                    ["required"] = new JsonArray(required.Select(r => (JsonNode)r).ToArray())
                }
            }
        };
    }

    private static JsonObject Props(params (string name, string type, string desc)[] fields)
    {
        var obj = new JsonObject();
        foreach (var (name, type, desc) in fields)
            obj[name] = new JsonObject { ["type"] = type, ["description"] = desc };
        return obj;
    }
}
