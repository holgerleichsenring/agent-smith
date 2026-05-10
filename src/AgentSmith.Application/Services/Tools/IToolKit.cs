using AgentSmith.Application.Models;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// Returns the per-phase AITool list. Plan/Review/Discuss/Filter/Synthesize get
/// the read-only set; Investigate/Verify add RunCommand; Implementation gets all
/// seven; Bootstrap gets WriteFile (no RunCommand). Null phase is the legacy
/// fallback — all seven tools.
/// </summary>
public interface IToolKit
{
    IList<AITool> GetToolsFor(SkillExecutionPhase? phase, string? investigatorMode);
}
