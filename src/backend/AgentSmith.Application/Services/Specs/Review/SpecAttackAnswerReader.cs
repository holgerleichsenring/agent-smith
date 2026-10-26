using AgentSmith.Application.Services.Json;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762e: reads the attack reviewer's JSON object out of its answer, wherever in the prose it sits. The
/// reviewer cites ids written <c>[A3]</c> and quotes spec text that may hold braces; the outermost object that reads
/// as an answer with a verdict is the answer, null when none does. 2026-10-26-cb25d: the scan is
/// <see cref="JsonAnswerReader"/>'s.
/// </summary>
internal static class SpecAttackAnswerReader
{
    public static SpecAttackAnswer? Read(string? text) =>
        JsonAnswerReader.ReadObject<SpecAttackWire, SpecAttackAnswer>(text, wire => wire.ToAnswer());
}
