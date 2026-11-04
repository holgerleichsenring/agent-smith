using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-02-2b26d: how an inline-ticket run ends on a hand-back it does not ask about — the reason, the question
/// and its readings printed, and the way on: run again with the answer in the ticket text. The answer has no
/// ticket to be written on, so the ticket text is the only place the next run reads it.
/// </summary>
internal static class InlineHandbackEnd
{
    public static CommandResult Fail(SpecHandback handback)
    {
        ArgumentNullException.ThrowIfNull(handback);
        var readings = handback.Readings.Count == 0 ? string.Empty
            : "\n" + string.Join("\n", handback.Readings.Select(
                (reading, i) => $"  {SpecHandbackComment.ReadingLabel(i)} {reading}"));
        return CommandResult.Fail(
            $"The derivation handed back ({handback.Case}): {handback.Reason}{readings}\n"
            + "Nothing was built. Run again with the answer in the ticket text.");
    }
}
