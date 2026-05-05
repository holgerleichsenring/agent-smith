using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// Builds AIFunction tool sets from a SandboxToolHost. Replaces the 4 provider-specific
/// ToolDefinitions files. AIFunctionFactory.Create reads each method's [Description] +
/// parameters via reflection to generate the LLM-facing schema.
/// </summary>
public static class SandboxToolHostExtensions
{
    /// <summary>All 7 tools (read/write/list/grep/run/log_decision/ask_human).</summary>
    public static IList<AITool> GetAllTools(this SandboxToolHost host) => new List<AITool>
    {
        AIFunctionFactory.Create(host.ReadFile),
        AIFunctionFactory.Create(host.WriteFile),
        AIFunctionFactory.Create(host.ListFiles),
        AIFunctionFactory.Create(host.Grep),
        AIFunctionFactory.Create(host.RunCommand),
        AIFunctionFactory.Create(host.LogDecision),
        AIFunctionFactory.Create(host.AskHuman)
    };

    /// <summary>Read-only subset for the Scout agent (read/list/grep).</summary>
    public static IList<AITool> GetScoutTools(this SandboxToolHost host) => new List<AITool>
    {
        AIFunctionFactory.Create(host.ReadFile),
        AIFunctionFactory.Create(host.ListFiles),
        AIFunctionFactory.Create(host.Grep)
    };
}
