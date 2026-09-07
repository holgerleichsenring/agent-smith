using System.Text.Json;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: turns the derivation model's reply into the ordered set. Everything the
/// model may decide is a JUDGEMENT — where the boundaries fall, which segments are
/// load-bearing, what to discard and why. Everything else is computed here: phase
/// ids, the chained requires-edges, the schema-valid yaml and the byte-exact
/// markdown companions.
/// </summary>
public sealed class SpecDerivationParser(
    DerivedPhaseBuilder phaseBuilder,
    SpecDerivationEnvelope envelope)
{
    public sealed record Parsed(SpecDerivation? Derivation, string? Error);

    /// <param name="executedHead">
    /// Phases that already ran, in order. They are re-used VERBATIM at the head of the
    /// set and the model's entries for those positions are discarded: an executed phase
    /// is append-only, and letting a re-cut rewrite one would rewrite the record of work
    /// that already sits in the branch history. Phase ids stay stable because they are
    /// assigned by position, so the head keeps its identity by construction.
    /// </param>
    /// <param name="evidence">
    /// 2026-09-07-b7e2: the evidence lines the framework minted for the looks this
    /// derivation took. A fact line citing one of their ids is a fact; any other is an
    /// assumption. Null or empty — a reply from a catalog that never looked — resolves
    /// every fact line to an assumption and nothing else changes.
    /// </param>
    public Parsed Parse(
        string? reply, string key, string ticketId, IReadOnlyList<TicketSegment> segments,
        SpecSource source, IReadOnlyList<SpecPhase>? executedHead = null,
        IReadOnlyList<string>? evidence = null)
    {
        if (string.IsNullOrWhiteSpace(reply))
            return new Parsed(null, "the reply was empty");

        foreach (var json in SpecJsonReader.BalancedObjects(reply))
        {
            JsonDocument doc;
            try { doc = JsonDocument.Parse(json); }
            catch (JsonException) { continue; }
            using (doc)
            {
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) continue;
                if (!SpecJsonReader.TryGet(root, "phases", out _)
                    && !SpecJsonReader.TryGet(root, "handback", out _))
                    continue;
                return Build(root, key, ticketId, segments, source, executedHead ?? [], evidence);
            }
        }
        return new Parsed(null, "the reply contained no JSON object with a 'phases' array");
    }

    private Parsed Build(
        JsonElement root, string key, string ticketId,
        IReadOnlyList<TicketSegment> segments, SpecSource source,
        IReadOnlyList<SpecPhase> executedHead, IReadOnlyList<string>? evidence)
    {
        var handback = envelope.Handback(root);
        if (handback is not null)
            return new Parsed(
                new SpecDerivation(
                    new SpecSet(key, [], SpecAccounting.Empty, [Initial()], source, handback),
                    envelope.IgnoredInstructions(root)),
                null);

        var phaseElements = SpecJsonReader.ReadObjects(root, "phases").ToList();
        if (phaseElements.Count == 0)
            return new Parsed(null, "the reply carried neither a phase nor a hand-back");
        if (phaseElements.Count > SpecSet.MaxPhases)
            return new Parsed(null,
                $"{phaseElements.Count} phases exceed the maximum of {SpecSet.MaxPhases} — "
                + "merge the smaller ones");

        var built = new List<SpecPhase>(executedHead);
        for (var i = executedHead.Count; i < phaseElements.Count; i++)
        {
            var (phase, error) = phaseBuilder.Build(
                phaseElements[i], i, ticketId, segments, built, evidence);
            if (phase is null) return new Parsed(null, error);
            built.Add(phase);
        }

        var accounting = SpecAccountingBuilder.Build(built, envelope.Discarded(root), segments);
        return new Parsed(
            new SpecDerivation(
                new SpecSet(
                    key, built, accounting, [Initial()], source,
                    ExecutedPhaseIds: [.. executedHead.Select(p => p.PhaseId)]),
                envelope.IgnoredInstructions(root)),
            null);
    }

    private static SpecRevision Initial() =>
        new(1, SpecRevisionCause.Initial, DateTimeOffset.UtcNow);
}
