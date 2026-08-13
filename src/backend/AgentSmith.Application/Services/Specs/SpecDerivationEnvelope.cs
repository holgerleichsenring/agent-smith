using System.Text.Json;
using AgentSmith.Domain.Models;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0400c: the cut's ENVELOPE — what the model discarded and why, which ticket
/// instructions it refused to obey, and whether it handed the ticket back instead
/// of cutting it. Read beside the phases, not inside them: these three answer for
/// the ticket as a whole, while a phase answers for its own slice.
/// </summary>
public sealed class SpecDerivationEnvelope
{
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
        if (!SpecJsonReader.TryGet(root, "handback", out var el)
            || el.ValueKind != JsonValueKind.Object)
            return null;
        var raw = SpecJsonReader.ReadString(el, "case").Replace("_", string.Empty);
        if (!Enum.TryParse<SpecHandbackCase>(raw, ignoreCase: true, out var parsed)
            || parsed == SpecHandbackCase.None)
            return null;
        return new SpecHandback(parsed, SpecJsonReader.ReadString(el, "reason"));
    }
}
