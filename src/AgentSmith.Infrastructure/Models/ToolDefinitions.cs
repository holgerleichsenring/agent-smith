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
        ReadFile, WriteFile, ListFiles, RunCommand
    };

    public static IList<Tool> ScoutTools => new List<Tool>
    {
        ReadFile, ListFiles
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

    private static Tool CreateTool(string name, string description, JsonObject parameters)
    {
        return new Tool(new Function(name, description, parameters));
    }
}
