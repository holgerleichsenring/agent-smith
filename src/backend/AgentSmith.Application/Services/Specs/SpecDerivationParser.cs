using System.Text.Json;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
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
    ISpecDraftValidator validator,
    PhaseDraftReader draftReader,
    DerivedPhaseYamlRenderer yamlRenderer,
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
    public Parsed Parse(
        string? reply, string key, string ticketId, IReadOnlyList<TicketSegment> segments,
        SpecSource source, IReadOnlyList<SpecPhase>? executedHead = null)
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
                return Build(root, key, ticketId, segments, source, executedHead ?? []);
            }
        }
        return new Parsed(null, "the reply contained no JSON object with a 'phases' array");
    }

    private Parsed Build(
        JsonElement root, string key, string ticketId,
        IReadOnlyList<TicketSegment> segments, SpecSource source,
        IReadOnlyList<SpecPhase> executedHead)
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
            var (phase, error) = BuildPhase(phaseElements[i], i, ticketId, segments, built);
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

    private (SpecPhase? Phase, string? Error) BuildPhase(
        JsonElement element, int index, string ticketId,
        IReadOnlyList<TicketSegment> segments, IReadOnlyList<SpecPhase> previous)
    {
        var goal = SpecJsonReader.ReadString(element, "goal");
        if (goal.Length == 0) return (null, $"phase {index + 1} has no goal");
        var done = SpecJsonReader.ReadStrings(element, "done");
        if (done.Count == 0)
            return (null,
                $"phase {index + 1} has no done-criteria — a phase that cannot end is not a phase");

        // p0400c: the deliverable is DECLARED, never defaulted. Live run aa2a showed
        // why: all five phases rendered ships_code: true — including a pure inventory
        // and a final report — because an absent field and a declared true are the
        // same value once a fallback has run. The obligation the prompt states is
        // enforced here, and the deriver's retry loop hands the rejection back to the
        // model, so it answers instead of the parser guessing on its behalf.
        if (!SpecJsonReader.TryReadBool(element, "ships_code", out var shipsCode))
            return (null,
                $"phase {index + 1} does not declare \"ships_code\". Every phase must state "
                + "its deliverable: true when it changes source, false when its done "
                + "criteria require no source diff (an inventory, a classification, a report).");

        var phaseId = PhaseIdFactory.For(ticketId, index);
        var slug = SpecJsonReader.ReadString(element, "slug") is { Length: > 0 } s
            ? PhaseIdFactory.Slug(s) : PhaseIdFactory.Slug(goal);
        var carried = SpecJsonReader.ReadInts(element, "carries")
            .Where(id => segments.Any(seg => seg.Id == id))
            .Distinct()
            .OrderBy(id => id)
            .ToList();
        var fileStem = $"{phaseId}-{slug}";

        var yaml = yamlRenderer.Render(
            phaseId, goal,
            // The sequence IS the requires-chain: each phase depends on the one before
            // it, because the repository it edits is the previous phase's output.
            previous.Count > 0 ? [previous[^1].PhaseId] : [],
            ReadSteps(element),
            done,
            $"{fileStem}.md",
            carried,
            ticketId,
            // p0400: the model may declare a knowledge phase (branch inventory,
            // classification) — carried into the ratified spec so keystone and
            // build gate judge it by its done criteria, not by a diff.
            shipsCode);

        if (validator.ValidateYaml(yaml) is SpecDraftInvalid invalid)
            return (null, $"phase {index + 1} ({phaseId}) is not a valid phase spec: {invalid.Error}");

        var markdown = SegmentExtractor.BuildMarkdown(phaseId, goal, carried, segments);
        return (new SpecPhase(draftReader.Read(yaml), slug, markdown, carried), null);
    }

    private static IReadOnlyList<(string Id, string Action)> ReadSteps(JsonElement element) =>
        [.. SpecJsonReader.ReadObjects(element, "steps")
            .Select(e => (
                Id: SpecJsonReader.ReadString(e, "id"),
                Action: SpecJsonReader.ReadString(e, "action")))
            .Where(s => s.Id.Length > 0 && s.Action.Length > 0)];

    private static SpecRevision Initial() =>
        new(1, SpecRevisionCause.Initial, DateTimeOffset.UtcNow);
}
