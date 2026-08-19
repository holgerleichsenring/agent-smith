using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Pipeline;

/// <summary>
/// The two edits a finished step may make to the steps still ahead of it: splice work in,
/// or retire work its own phase no longer needs.
/// <para>
/// p0460 extracted this from <see cref="PipelineStepRunner"/>, which dispatches steps —
/// deciding what happens to the remaining list is a different job, and it is the job that
/// grows.
/// </para>
/// </summary>
public sealed class PipelineCommandList(ILogger<PipelineCommandList> logger)
{
    /// <summary>Splices <see cref="CommandResult.InsertNext"/> directly after the step
    /// that produced it, in order.</summary>
    public void Insert(
        LinkedListNode<PipelineCommand> after, LinkedList<PipelineCommand> commands,
        CommandResult result)
    {
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(result);
        if (result.InsertNext is not { Count: > 0 } follow) return;

        var insertAfter = after;
        foreach (var next in follow)
        {
            commands.AddAfter(insertAfter, next);
            insertAfter = insertAfter.Next!;
        }
        logger.LogInformation("{Command} inserted {Count} follow-up commands: {Commands}",
            after.Value.DisplayName, follow.Count, string.Join(", ", follow));
    }

    /// <summary>
    /// p0460: removes the named steps of the CURRENT step's phase from what is still
    /// ahead. The walk stops at the first step belonging to another phase — a step may
    /// retire its own phase's remaining work and nothing beyond it — and a step outside
    /// any phase has no phase to retire.
    /// </summary>
    public void DropAhead(
        LinkedListNode<PipelineCommand> after, LinkedList<PipelineCommand> commands,
        CommandResult result)
    {
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(result);
        if (result.DropAhead is not { Count: > 0 } drop) return;
        if (after.Value.PhaseId is not { Length: > 0 } phaseId) return;

        var dropped = new List<string>();
        var node = after.Next;
        while (node is not null && string.Equals(node.Value.PhaseId, phaseId, StringComparison.Ordinal))
        {
            var next = node.Next;
            if (drop.Contains(node.Value.Name, StringComparer.Ordinal))
            {
                dropped.Add(node.Value.DisplayName);
                commands.Remove(node);
            }
            node = next;
        }
        if (dropped.Count == 0) return;

        logger.LogInformation(
            "{Command} retired {Count} step(s) of phase {PhaseId}: {Commands}",
            after.Value.DisplayName, dropped.Count, phaseId, string.Join(", ", dropped));
    }
}
