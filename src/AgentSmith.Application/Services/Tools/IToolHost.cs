using AgentSmith.Application.Models;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// A domain-scoped contributor of LLM-callable tools. ToolKit composes one or
/// more hosts (per IPipelineToolPolicy) into the full tool surface for a
/// given pipeline + phase. Per-phase filtering lives on each host — the host
/// knows which of its tools belong in which phase.
/// </summary>
public interface IToolHost
{
    /// <summary>Concrete host type, used by IPipelineToolPolicy for allow-listing.</summary>
    Type HostType => GetType();

    IEnumerable<AIFunction> GetTools(SkillExecutionPhase? phase, string? investigatorMode);
}
