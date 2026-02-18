using GenerativeAI.Types;

namespace AgentSmith.Infrastructure.Providers.Agent;

/// <summary>
/// Tool definitions for the Google Gemini API.
/// Mirrors ToolDefinitions but uses Gemini SDK types.
/// </summary>
public static class GeminiToolDefinitions
{
    private const string TypeObject = "OBJECT";
    private const string TypeString = "STRING";

    public static Tool AllTools => new()
    {
        FunctionDeclarations = new List<FunctionDeclaration>
        {
            ReadFile, WriteFile, ListFiles, RunCommand
        }
    };

    public static Tool ScoutOnlyTools => new()
    {
        FunctionDeclarations = new List<FunctionDeclaration>
        {
            ReadFile, ListFiles
        }
    };

    private static FunctionDeclaration ReadFile => new()
    {
        Name = "read_file",
        Description = "Read the contents of a file in the repository.",
        Parameters = new Schema
        {
            Type = TypeObject,
            Properties = new Dictionary<string, Schema>
            {
                ["path"] = new() { Type = TypeString, Description = "Relative path from repository root." }
            },
            Required = new List<string> { "path" }
        }
    };

    private static FunctionDeclaration WriteFile => new()
    {
        Name = "write_file",
        Description = "Write or overwrite a file in the repository.",
        Parameters = new Schema
        {
            Type = TypeObject,
            Properties = new Dictionary<string, Schema>
            {
                ["path"] = new() { Type = TypeString, Description = "Relative path from repository root." },
                ["content"] = new() { Type = TypeString, Description = "Complete file content to write." }
            },
            Required = new List<string> { "path", "content" }
        }
    };

    private static FunctionDeclaration ListFiles => new()
    {
        Name = "list_files",
        Description = "List files in a directory of the repository.",
        Parameters = new Schema
        {
            Type = TypeObject,
            Properties = new Dictionary<string, Schema>
            {
                ["path"] = new() { Type = TypeString, Description = "Relative directory path, empty string for root." }
            },
            Required = new List<string> { "path" }
        }
    };

    private static FunctionDeclaration RunCommand => new()
    {
        Name = "run_command",
        Description = "Run a shell command in the repository directory.",
        Parameters = new Schema
        {
            Type = TypeObject,
            Properties = new Dictionary<string, Schema>
            {
                ["command"] = new() { Type = TypeString, Description = "Shell command to execute." }
            },
            Required = new List<string> { "command" }
        }
    };
}
