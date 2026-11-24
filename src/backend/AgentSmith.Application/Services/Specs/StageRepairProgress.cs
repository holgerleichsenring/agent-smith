using AgentSmith.Application.Services.Handlers;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-11-24-6811c: the one rule whether a red stage may go back to a master once more, read mechanically — the repair
/// pass wrote to the tree and the failure changed (2026-11-18-9c1cc). Extracted from <see cref="StageRepair"/> so the
/// refresh-base run's repair (BaseMergeVerify) stops where a spec's repair stops.
/// </summary>
public static class StageRepairProgress
{
    /// <summary>What identifies one red stage: where, which, how it exited and the end of its output.</summary>
    public static string Signature(VerifyOutcome red)
    {
        ArgumentNullException.ThrowIfNull(red);
        return $"{red.Key}|{red.Stage}|{red.ExitCode}|{VerifyCommandRunner.Tail(red.Output ?? string.Empty, VerifyCommandRunner.OutputTailChars)}";
    }

    /// <summary>The last repair wrote something and the failure it left is not the one it was given.</summary>
    public static bool MadeProgress(string lastRed, string red, IReadOnlyList<CodeChange>? wrote) =>
        !string.Equals(lastRed, red, StringComparison.Ordinal) && ReengageProgressPolicy.ProducedNewWork([], wrote ?? []);
}
