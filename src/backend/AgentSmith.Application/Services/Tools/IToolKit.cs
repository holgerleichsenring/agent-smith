using AgentSmith.Application.Models;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// Composes per-pipeline, per-phase tool sets from caller-provided
/// <see cref="IToolHost"/> instances, gated by an
/// <see cref="AgentSmith.Contracts.Services.IPipelineToolPolicy"/>.
/// Hosts are passed at call time (not via constructor injection) because
/// each host carries per-pipeline-run state (sandbox handle, decision
/// logger, repo path, dialogue transport, job id) that lives in
/// <c>PipelineContext</c>, not in DI.
/// </summary>
public interface IToolKit
{
    /// <summary>
    /// Returns the AITool set the LLM may call for the given pipeline + phase.
    /// </summary>
    /// <param name="pipelineName">
    /// Pipeline preset name (e.g. "fix-bug", "security-scan"). Use
    /// <see cref="WildcardPipelineName"/> when no specific pipeline applies
    /// (legacy callers, ad-hoc test setups).
    /// </param>
    /// <param name="phase">Per-skill execution phase, or null for the legacy all-tools fallback.</param>
    /// <param name="investigatorMode">Investigator-mode discriminator (passed through to each host).</param>
    /// <param name="hosts">The candidate <see cref="IToolHost"/> instances for this pipeline run.</param>
    IList<AITool> GetToolsFor(
        string pipelineName,
        SkillExecutionPhase? phase,
        string? investigatorMode,
        IEnumerable<IToolHost> hosts);

    /// <summary>
    /// Sentinel for "no specific pipeline." Resolves to all-hosts-active under
    /// <see cref="AgentSmith.Contracts.Services.IPipelineToolPolicy"/> defaults.
    /// </summary>
    public const string WildcardPipelineName = "*";
}
