using System.Text.Json.Nodes;
using Anthropic.SDK.Common;

namespace AgentSmith.Infrastructure.Models;

/// <summary>
/// Defines the tools available to the AI agent during the agentic loop.
/// </summary>
public static class ToolDefinitions
{
    public static IList<Tool> All => new List<Tool>
    {
        ReadFile, WriteFile, ListFiles, Grep, RunCommand, LogDecision, AskHuman
    };

    public static IList<Tool> ScoutTools => new List<Tool>
    {
        ReadFile, ListFiles, Grep
    };

    public static Tool ReadFile => CreateTool(
        "read_file",
        "Read the contents of a file in the repository.",
        new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["path"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Relative path from repository root."
                }
            },
            ["required"] = new JsonArray("path")
        });

    public static Tool WriteFile => CreateTool(
        "write_file",
        "Write or overwrite a file in the repository.",
        new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["path"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Relative path from repository root."
                },
                ["content"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Complete file content to write."
                }
            },
            ["required"] = new JsonArray("path", "content")
        });

    public static Tool ListFiles => CreateTool(
        "list_files",
        "List files in a directory of the repository.",
        new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["path"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Relative directory path, empty string for root."
                }
            },
            ["required"] = new JsonArray("path")
        });

    public static Tool Grep => CreateTool(
        "grep",
        "Search files for a regex pattern. Returns up to 200 matching lines as JSON {matches:[{path,line,text}], truncated}.",
        new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["pattern"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Regex pattern to match against each line."
                },
                ["glob"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Optional glob to limit which files are searched (e.g. '**/*.cs', '**/*Tests*.csproj'). Defaults to '**/*'."
                }
            },
            ["required"] = new JsonArray("pattern")
        });

    public static Tool RunCommand => CreateTool(
        "run_command",
        "Run a shell command in the repository directory.",
        new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["command"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Shell command to execute."
                }
            },
            ["required"] = new JsonArray("command")
        });

    public static Tool LogDecision => CreateTool(
        "log_decision",
        "Log an architectural, tooling, or implementation decision with its reason. " +
        "Call this when deviating from the plan or making a non-trivial decision during execution.",
        new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
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
            ["required"] = new JsonArray("category", "decision")
        });

    public static Tool AskHuman => CreateTool(
        "ask_human",
        "Ask the human a question when clarification is needed. Use sparingly — only when genuinely ambiguous and the wrong choice would cause significant rework.",
        new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["question_type"] = new JsonObject
                {
                    ["type"] = "string",
                    ["enum"] = new JsonArray("confirmation", "choice", "free_text", "approval"),
                    ["description"] = "Type of question."
                },
                ["text"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "The question to ask."
                },
                ["context"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Why are you asking? Max 300 chars."
                },
                ["choices"] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = new JsonObject { ["type"] = "string" },
                    ["description"] = "Only for type=choice."
                },
                ["default_answer"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Used on timeout."
                }
            },
            ["required"] = new JsonArray("question_type", "text", "context", "default_answer")
        });

    private static Tool CreateTool(string name, string description, JsonObject parameters)
    {
        return new Tool(new Function(name, description, parameters));
    }
}
