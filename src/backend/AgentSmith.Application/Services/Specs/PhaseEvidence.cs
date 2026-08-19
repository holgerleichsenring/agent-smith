using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Application.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0452: what the delivery account is shown as having actually run.
/// <para>
/// Two sources, and until now only the first: the stages VERIFICATION ran, and the commands
/// THE AGENT ran. Runs 459d, 587c and 929f were each refused for a search the agent had
/// performed and the account was never shown — in 929f, nineteen of them across two
/// repositories, two printing a labelled legacy-reference report.
/// </para>
/// </summary>
public static class PhaseEvidence
{
    public static IReadOnlyList<string> From(
        IEnumerable<VerifyOutcome> outcomes, PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(outcomes);
        ArgumentNullException.ThrowIfNull(pipeline);
        var evidence = outcomes
            .Where(o => !o.Skipped)
            .Select(o => $"{o.Key}: {o.Stage} '{o.Command}' exited {o.ExitCode}")
            .ToList();
        if (pipeline.TryGet<PhaseCommandLog>(ContextKeys.PhaseCommands, out var agentRan)
            && agentRan is not null)
            evidence.AddRange(agentRan.Evidence());
        return evidence;
    }
}
