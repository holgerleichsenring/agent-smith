using AgentSmith.Application.Models;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// Centralizes the phase-keyed tool selection. Per phase the LLM sees a different
/// subset of <see cref="SandboxToolHost"/>'s seven methods. Read-only / escalation
/// tools (ReadFile, Grep, ListFiles, LogDecision, AskHuman) are the baseline;
/// WriteFile and RunCommand are gated by phase.
/// </summary>
public sealed class ToolKit : IToolKit
{
    private readonly SandboxToolHost _host;

    public ToolKit(SandboxToolHost host) => _host = host;

    public IList<AITool> GetToolsFor(SkillExecutionPhase? phase, string? investigatorMode)
    {
        _ = investigatorMode;

        if (phase is null)
            return AllTools();

        return phase switch
        {
            SkillExecutionPhase.Implementation => AllTools(),
            SkillExecutionPhase.Bootstrap => BootstrapTools(),
            SkillExecutionPhase.Verify or SkillExecutionPhase.Investigate => InvestigatorTools(),
            _ => ReadOnlyTools()
        };
    }

    private IList<AITool> ReadOnlyTools() => new List<AITool>
    {
        AIFunctionFactory.Create(_host.ReadFile),
        AIFunctionFactory.Create(_host.Grep),
        AIFunctionFactory.Create(_host.ListFiles),
        AIFunctionFactory.Create(_host.LogDecision),
        AIFunctionFactory.Create(_host.AskHuman)
    };

    private IList<AITool> InvestigatorTools()
    {
        var tools = ReadOnlyTools();
        tools.Add(AIFunctionFactory.Create(_host.RunCommand));
        return tools;
    }

    private IList<AITool> BootstrapTools()
    {
        var tools = ReadOnlyTools();
        tools.Insert(3, AIFunctionFactory.Create(_host.WriteFile));
        return tools;
    }

    private IList<AITool> AllTools() => new List<AITool>
    {
        AIFunctionFactory.Create(_host.ReadFile),
        AIFunctionFactory.Create(_host.WriteFile),
        AIFunctionFactory.Create(_host.ListFiles),
        AIFunctionFactory.Create(_host.Grep),
        AIFunctionFactory.Create(_host.RunCommand),
        AIFunctionFactory.Create(_host.LogDecision),
        AIFunctionFactory.Create(_host.AskHuman)
    };
}
