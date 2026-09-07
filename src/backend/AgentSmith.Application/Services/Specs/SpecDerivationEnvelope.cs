using System.Text.Json;
using AgentSmith.Domain.Models;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0400c: the cut's ENVELOPE — what the model discarded and why, which ticket
/// instructions it refused to obey, and whether it handed the ticket back instead
/// of cutting it. Read beside the phases, not inside them: these three answer for
/// the ticket as a whole, while a phase answers for its own slice.
/// <para>
/// A QUESTION hand-back carries its readings as a typed list and the taken one as
/// an index. One with fewer than two readings, or a taken index outside them, is not
/// a question anyone can answer — it is refused, and <see cref="HandbackRejection"/>
/// says why so the retry names it.
/// </para>
/// </summary>
public sealed class SpecDerivationEnvelope
{
    private const int MinReadings = 2;

    public IReadOnlyList<DiscardedSegment> Discarded(JsonElement root) =>
        [.. SpecJsonReader.ReadObjects(root, "discarded")
            .Select(e => new DiscardedSegment(
                SpecJsonReader.ReadInt(e, "segment"),
                SpecJsonReader.ReadString(e, "reason")))
            .Where(d => d.SegmentId > 0)];

    public IReadOnlyList<IgnoredInstruction> IgnoredInstructions(JsonElement root) =>
        [.. SpecJsonReader.ReadObjects(root, "ignoredinstructions")
            .Select(e => new IgnoredInstruction(
                SpecJsonReader.ReadString(e, "quote"),
                SpecJsonReader.ReadString(e, "reason")))
            .Where(i => i.Quote.Length > 0)];

    public SpecHandback? Handback(JsonElement root)
    {
        if (!TryReadCase(root, out var el, out var parsed)) return null;
        if (parsed != SpecHandbackCase.Question)
            return new SpecHandback(parsed, SpecJsonReader.ReadString(el, "reason"));
        var readings = SpecJsonReader.ReadStrings(el, "readings");
        var taken = SpecJsonReader.ReadInt(el, "taken");
        if (QuestionRejection(readings, taken) is not null) return null;
        return new SpecHandback(
            parsed, SpecJsonReader.ReadString(el, "reason"), Readings: readings, Taken: taken);
    }

    /// <summary>Why a present hand-back object was refused; null when it was usable or absent.</summary>
    public string? HandbackRejection(JsonElement root) =>
        TryReadCase(root, out var el, out var parsed) && parsed == SpecHandbackCase.Question
            ? QuestionRejection(
                SpecJsonReader.ReadStrings(el, "readings"), SpecJsonReader.ReadInt(el, "taken"))
            : null;

    private static bool TryReadCase(JsonElement root, out JsonElement el, out SpecHandbackCase parsed)
    {
        parsed = SpecHandbackCase.None;
        if (!SpecJsonReader.TryGet(root, "handback", out el) || el.ValueKind != JsonValueKind.Object)
            return false;
        var raw = SpecJsonReader.ReadString(el, "case").Replace("_", string.Empty);
        return Enum.TryParse(raw, ignoreCase: true, out parsed) && parsed != SpecHandbackCase.None;
    }

    private static string? QuestionRejection(IReadOnlyList<string> readings, int taken)
    {
        if (readings.Count < MinReadings)
            return $"a question hand-back needs at least {MinReadings} readings — "
                + $"{readings.Count} given; if there is only one reading, cut phases under it";
        if (taken < 0 || taken >= readings.Count)
            return $"the taken index {taken} names none of the {readings.Count} readings";
        return null;
    }
}
