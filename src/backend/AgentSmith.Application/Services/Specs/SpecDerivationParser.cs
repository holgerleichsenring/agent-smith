using System.Text.Json;
using AgentSmith.Contracts.Json;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: turns the derivation model's reply into the ordered set. Everything the
/// model may decide is a JUDGEMENT — where the boundaries fall, which segments are
/// load-bearing, what to discard and why. Everything else is computed here: spec
/// ids, the chained requires-edges, the schema-valid yaml and the byte-exact
/// markdown companions.
/// </summary>
public sealed class SpecDerivationParser(
    DerivedSpecSequence specs,
    SpecDerivationEnvelope envelope)
{
    /// <param name="Uncited">2026-10-12-3a36b: why a contradiction hand-back cites no look this derivation
    /// took; it is still parsed, and whether it is asked again or parks is the deriver's call.</param>
    public sealed record Parsed(SpecDerivation? Derivation, string? Error, string? Uncited = null);

    /// <param name="series">2026-10-06-03c7c: the series' base id; spec ids are it plus a letter.</param>
    /// <param name="executedHead">
    /// Specs that already ran, re-used VERBATIM at the head of the set; the model's entries
    /// for those positions are discarded, because an executed spec is append-only. Spec ids
    /// are assigned by position, so the head keeps its identity by construction.
    /// </param>
    /// <param name="evidence">
    /// 2026-09-07-b7e2: the evidence lines the framework minted for the looks this
    /// derivation took. A fact line citing one of their ids is a fact; any other is an
    /// assumption. Null or empty — a reply from a catalog that never looked — resolves
    /// every fact line to an assumption and nothing else changes.
    /// </param>
    /// <param name="scope">2026-10-12-3a36e: the scope's repository names; see <see cref="SpecReposRule"/>.</param>
    /// <param name="taken">2026-10-12-f5f7d: ids a new spec must not take; see <see cref="SeriesTakenIds"/>.</param>
    public Parsed Parse(
        string? reply, string key, string series, string ticketId, IReadOnlyList<TicketSegment> segments,
        SpecSource source, IReadOnlyList<DerivedSpec>? executedHead = null,
        IReadOnlyList<string>? evidence = null, IReadOnlyList<string>? scope = null,
        IReadOnlyList<string>? taken = null)
    {
        if (string.IsNullOrWhiteSpace(reply))
            return new Parsed(null, "the reply was empty");

        foreach (var json in JsonObjectSpans.Balanced(reply))
        {
            JsonDocument doc;
            try { doc = JsonDocument.Parse(json); }
            catch (JsonException) { continue; }
            using (doc)
            {
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) continue;
                if (!SpecJsonReader.TryGet(root, SpecJsonReader.SpecsKey(root), out _)
                    && !SpecJsonReader.TryGet(root, "handback", out _))
                    continue;
                return Build(root, new SeriesOf(key, series), ticketId, segments, source, executedHead ?? [],
                    new SpecInputs(evidence, scope ?? [], taken ?? []));
            }
        }
        return new Parsed(null, "the reply contained no JSON object with a 'specs' array");
    }

    private Parsed Build(
        JsonElement root, SeriesOf owner, string ticketId,
        IReadOnlyList<TicketSegment> segments, SpecSource source,
        IReadOnlyList<DerivedSpec> executedHead, SpecInputs inputs)
    {
        var handback = envelope.Handback(root);
        if (handback is not null)
        {
            var cited = ContradictionCitations.Resolve(handback, envelope.HandbackCites(root), inputs.Evidence);
            return new Parsed(
                new SpecDerivation(
                    new SpecSet(owner.Key, [], SpecAccounting.Empty, [Initial()], source,
                        handback with { Cites = cited.Lines }, Series: owner.Series),
                    envelope.IgnoredInstructions(root)),
                null, cited.Problem);
        }

        var specElements = SpecJsonReader.ReadObjects(root, SpecJsonReader.SpecsKey(root)).ToList();
        if (specElements.Count == 0)
            return new Parsed(null,
                envelope.HandbackRejection(root) ?? "the reply carried neither a spec nor a hand-back");
        if (specElements.Count > SpecSet.MaxSpecs)
            return new Parsed(null,
                $"{specElements.Count} specs exceed the maximum of {SpecSet.MaxSpecs} — merge the smaller ones");

        // 2026-10-08-f114: the head's positions are discarded, but a reply that does not even reach
        // past them dropped the set it was asked to repeat — refused, so the deriver asks again.
        if (specElements.Count < executedHead.Count)
            return new Parsed(null,
                $"the reply has {specElements.Count} spec(s) but {executedHead.Count} already ran — repeat them first");
        var (built, error) = specs.Build(
            specElements, executedHead, owner.Series, ticketId, segments, inputs.Evidence, inputs.Scope, inputs.Taken);
        if (built is null) return new Parsed(null, error);

        var accounting = SpecAccountingBuilder.Build(
            built, envelope.Discarded(root), segments, envelope.DiscardedContexts(root));
        return new Parsed(
            new SpecDerivation(
                new SpecSet(
                    owner.Key, built, accounting, [Initial()], source,
                    ExecutedSpecIds: [.. executedHead.Select(p => p.SpecId)],
                    Series: owner.Series),
                envelope.IgnoredInstructions(root)),
            null);
    }

    /// <summary>The ticket key and the series' base id the parsed set belongs to.</summary>
    private sealed record SeriesOf(string Key, string Series);

    private sealed record SpecInputs(
        IReadOnlyList<string>? Evidence, IReadOnlyList<string> Scope, IReadOnlyList<string> Taken);

    private static SpecRevision Initial() =>
        new(1, SpecRevisionCause.Initial, DateTimeOffset.UtcNow);
}
