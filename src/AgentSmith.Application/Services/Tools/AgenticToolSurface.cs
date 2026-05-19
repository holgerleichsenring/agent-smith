using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// Composes AITool lists from the p0145 hosts (FilesystemToolHost +
/// LogDecisionToolHost + HumanToolHost). Replaces the pre-p0145
/// SandboxToolHost facade — same tool surfaces, no facade layer.
/// </summary>
public static class AgenticToolSurface
{
    /// <summary>Full agentic surface: 5 fs tools + log_decision + ask_human.</summary>
    public static IList<AITool> ReadWriteWithHuman(
        FilesystemToolHost fs, LogDecisionToolHost log, HumanToolHost human) =>
        fs.GetTools(phase: null, investigatorMode: null)
            .Concat(log.GetTools(phase: null, investigatorMode: null))
            .Concat(human.GetTools(phase: null, investigatorMode: null))
            .Cast<AITool>()
            .ToList();

    /// <summary>Scout surface: read-only fs (ReadFile + Grep + ListFiles).</summary>
    public static IList<AITool> Scout(FilesystemToolHost fs) =>
        fs.GetTools(Models.SkillExecutionPhase.Plan, investigatorMode: null)
            .Cast<AITool>()
            .ToList();

    /// <summary>Bootstrap surface: fs read/write/list/grep + log_decision (no run, no human).</summary>
    public static IList<AITool> Bootstrap(FilesystemToolHost fs, LogDecisionToolHost log) =>
        fs.GetTools(Models.SkillExecutionPhase.Bootstrap, investigatorMode: null)
            .Concat(log.GetTools(phase: null, investigatorMode: null))
            .Cast<AITool>()
            .ToList();
}
