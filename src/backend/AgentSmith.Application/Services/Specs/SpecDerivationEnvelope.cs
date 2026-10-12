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
/// a question anyone can answer (<see cref="QuestionHandbackShape"/>) — it is refused, and
/// <see cref="HandbackRejection"/> says why so the retry names it.
/// </para>
/// <para>2026-10-12-d142: <see cref="SpecHandbackCase.ScopeUnassigned"/> is raised by the code only;
/// a reply naming it is refused the same way.</para>
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

    /// <summary>2026-09-07-b7e2: a phase's fact lines as the model wrote them — claim and
    /// the id it cites. Which of them ARE facts is <see cref="FactResolver"/>'s call.</summary>
    public IReadOnlyList<FactLine> Facts(JsonElement phase) =>
        [.. SpecJsonReader.ReadObjects(phase, "facts")
            .Select(e => new FactLine(
                SpecJsonReader.ReadString(e, "claim"),
                SpecJsonReader.ReadString(e, "cites")))
            .Where(f => f.Claim.Length > 0)];

    /// <summary>2026-09-08-1830: the contexts a phase declares it changes, as written.</summary>
    public IReadOnlyList<string> Contexts(JsonElement phase) =>
        SpecJsonReader.ReadStrings(phase, "contexts");

    /// <summary>2026-10-12-3a36e: the repositories a phase states it changes, as written.
    /// 2026-10-12-f5f7a: a string names one — one repository per spec is the rule, so the
    /// shape the rule asks for is not read as nothing stated.</summary>
    public IReadOnlyList<string> Repos(JsonElement phase)
    {
        var single = SpecJsonReader.ReadString(phase, "repos");
        return single.Length > 0 ? [single] : SpecJsonReader.ReadStrings(phase, "repos");
    }

    /// <summary>2026-09-08-1830: the named contexts the model deliberately leaves out, each
    /// with its reason; one without a reason is not accounted for.</summary>
    public IReadOnlyList<DiscardedContext> DiscardedContexts(JsonElement root) =>
        [.. SpecJsonReader.ReadObjects(root, "discardedcontexts")
            .Select(e => new DiscardedContext(
                SpecJsonReader.ReadString(e, "context"),
                SpecJsonReader.ReadString(e, "reason")))
            .Where(d => d.Context.Length > 0 && d.Reason.Length > 0)];

    public SpecHandback? Handback(JsonElement root)
    {
        if (!TryReadCase(root, out var el, out var parsed) || parsed == SpecHandbackCase.ScopeUnassigned) return null;
        if (parsed != SpecHandbackCase.Question)
            return new SpecHandback(parsed, SpecJsonReader.ReadString(el, "reason"));
        var readings = SpecJsonReader.ReadStrings(el, "readings");
        var taken = SpecJsonReader.ReadInt(el, "taken");
        if (QuestionHandbackShape.Rejection(readings, taken) is not null) return null;
        return new SpecHandback(
            parsed, SpecJsonReader.ReadString(el, "reason"), Readings: readings, Taken: taken);
    }

    /// <summary>2026-10-12-3a36b: the evidence ids a hand-back cites, as written — a string
    /// (<c>"L2, L5"</c>) or a list; each folded to the id as minted. Empty when it cites nothing.</summary>
    public IReadOnlyList<string> HandbackCites(JsonElement root)
    {
        if (!TryReadCase(root, out var el, out _) || !SpecJsonReader.TryGet(el, "cites", out var cites))
            return [];
        return cites.ValueKind switch
        {
            JsonValueKind.String => DerivationEvidence.CitationsIn(cites.GetString()),
            JsonValueKind.Array => [.. cites.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .SelectMany(e => DerivationEvidence.CitationsIn(e.GetString()))],
            _ => [],
        };
    }

    /// <summary>Why a present hand-back object was refused; null when it was usable or absent.</summary>
    public string? HandbackRejection(JsonElement root) =>
        !TryReadCase(root, out var el, out var parsed) ? null : parsed switch
        {
            SpecHandbackCase.Question => QuestionHandbackShape.Rejection(
                SpecJsonReader.ReadStrings(el, "readings"), SpecJsonReader.ReadInt(el, "taken")),
            SpecHandbackCase.ScopeUnassigned =>
                "the scope_unassigned hand-back is raised by the framework, not by a reply — cut the ticket or name another case",
            _ => null,
        };

    private static bool TryReadCase(JsonElement root, out JsonElement el, out SpecHandbackCase parsed)
    {
        parsed = SpecHandbackCase.None;
        if (!SpecJsonReader.TryGet(root, "handback", out el) || el.ValueKind != JsonValueKind.Object)
            return false;
        var raw = SpecJsonReader.ReadString(el, "case").Replace("_", string.Empty);
        return Enum.TryParse(raw, ignoreCase: true, out parsed) && parsed != SpecHandbackCase.None;
    }
}
