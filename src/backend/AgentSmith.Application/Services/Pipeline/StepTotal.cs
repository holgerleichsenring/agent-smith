using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Pipeline;

/// <summary>
/// 2026-11-26-5201g: the "of N" beside a step's number — the steps already run plus the commands still after it in the
/// live list. The list's own length was used before, and a resumed run re-enters with only its reprovision steps and
/// what remained while its step number goes on from the checkpoint, so a run showed "step 32 of 30". A run that never
/// resumes gets the list length as before: its step number is its position in the list.
/// </summary>
public static class StepTotal
{
    /// <summary>The total for step <paramref name="stepNumber"/>, which runs <paramref name="at"/>.</summary>
    public static int Of(int stepNumber, LinkedListNode<PipelineCommand> at)
    {
        ArgumentNullException.ThrowIfNull(at);
        var ahead = 0;
        for (var node = at.Next; node is not null; node = node.Next) ahead++;
        return stepNumber + ahead;
    }
}
