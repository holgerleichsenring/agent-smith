using System.Text.Json;
using OpenAI.Chat;

namespace AgentSmith.Infrastructure.Models;

/// <summary>
/// Tool definitions for the OpenAI Chat Completions API.
/// Mirrors ToolDefinitions but uses OpenAI SDK ChatTool format.
/// </summary>
public static class OpenAiToolDefinitions
{
    public static IList<ChatTool> All => new List<ChatTool>
    {
        ReadFile, WriteFile, ListFiles, Grep, RunCommand, LogDecision
    };

    public static IList<ChatTool> ScoutTools => new List<ChatTool>
    {
        ReadFile, ListFiles, Grep
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

    public static ChatTool Grep => ChatTool.CreateFunctionTool(
        functionName: "grep",
        functionDescription: "Search files for a regex pattern. Returns up to 200 matching lines as JSON {matches:[{path,line,text}], truncated}.",
        functionParameters: BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "pattern": { "type": "string", "description": "Regex pattern to match against each line." },
                    "glob": { "type": "string", "description": "Optional glob to limit which files are searched (e.g. '**/*.cs'). Defaults to '**/*'." }
                },
                "required": ["pattern"]
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

    public static ChatTool LogDecision => ChatTool.CreateFunctionTool(
        functionName: "log_decision",
        functionDescription: "Log an architectural, tooling, or implementation decision with its reason. " +
            "Call this when deviating from the plan or making a non-trivial decision during execution.",
        functionParameters: BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "category": { "type": "string", "enum": ["Architecture", "Tooling", "Implementation", "TradeOff"], "description": "Decision category." },
                    "decision": { "type": "string", "description": "Format: '**DecisionName**: reason why, not what'" }
                },
                "required": ["category", "decision"]
            }
            """));
}
