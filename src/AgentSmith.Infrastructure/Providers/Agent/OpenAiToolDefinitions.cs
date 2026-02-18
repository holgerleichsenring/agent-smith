using System.Text.Json;
using OpenAI.Chat;

namespace AgentSmith.Infrastructure.Providers.Agent;

/// <summary>
/// Tool definitions for the OpenAI Chat Completions API.
/// Mirrors ToolDefinitions but uses OpenAI SDK ChatTool format.
/// </summary>
public static class OpenAiToolDefinitions
{
    public static IList<ChatTool> All => new List<ChatTool>
    {
        ReadFile, WriteFile, ListFiles, RunCommand
    };

    public static IList<ChatTool> ScoutTools => new List<ChatTool>
    {
        ReadFile, ListFiles
    };

    public static ChatTool ReadFile => ChatTool.CreateFunctionTool(
        functionName: "read_file",
        functionDescription: "Read the contents of a file in the repository.",
        functionParameters: BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "path": { "type": "string", "description": "Relative path from repository root." }
                },
                "required": ["path"]
            }
            """));

    public static ChatTool WriteFile => ChatTool.CreateFunctionTool(
        functionName: "write_file",
        functionDescription: "Write or overwrite a file in the repository.",
        functionParameters: BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "path": { "type": "string", "description": "Relative path from repository root." },
                    "content": { "type": "string", "description": "Complete file content to write." }
                },
                "required": ["path", "content"]
            }
            """));

    public static ChatTool ListFiles => ChatTool.CreateFunctionTool(
        functionName: "list_files",
        functionDescription: "List files in a directory of the repository.",
        functionParameters: BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "path": { "type": "string", "description": "Relative directory path, empty string for root." }
                },
                "required": ["path"]
            }
            """));

    public static ChatTool RunCommand => ChatTool.CreateFunctionTool(
        functionName: "run_command",
        functionDescription: "Run a shell command in the repository directory.",
        functionParameters: BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "command": { "type": "string", "description": "Shell command to execute." }
                },
                "required": ["command"]
            }
            """));
}
