using System.Text.Json;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-07-b7e2: turns ONE phase element of the derivation reply into a phase — its
/// id by position, its slug, the schema-valid yaml, the byte-exact companion and, now,
/// its resolved facts. Split from <see cref="SpecDerivationParser"/>, which reads the
/// reply's envelope and the ordered set; one element's shape is its own question.
/// </summary>
public sealed class DerivedPhaseBuilder(
    ISpecDraftValidator validator,
    PhaseDraftReader draftReader,
    DerivedPhaseYamlRenderer yamlRenderer,
    SpecDerivationEnvelope envelope,
    FactResolver factResolver)
{
    // p0400c's obligation, pointed at what the gate now measures (p0421): the gate asks
    // what the BRANCH satisfies, so the thing that may not be missing is the criteria
    // themselves — a phase whose completion cannot be stated is a phase nobody can
    // account for. The deriver's retry loop hands the rejection back to the model.
    private const string NoCriteria =
        "states no done-criteria. Every phase must state what is true when it is finished, "
        + "in terms someone can check against the repository — a phase that cannot end is not a phase.";

    public (SpecPhase? Phase, string? Error) Build(
        JsonElement element, int index, string ticketId,
        IReadOnlyList<TicketSegment> segments, IReadOnlyList<SpecPhase> previous,
        IReadOnlyList<string>? evidence)
    {
        var goal = SpecJsonReader.ReadString(element, "goal");
        if (goal.Length == 0) return (null, $"phase {index + 1} has no goal");
        var done = SpecJsonReader.ReadStrings(element, "done");
        if (done.Count == 0) return (null, $"phase {index + 1} {NoCriteria}");

        var phaseId = PhaseIdFactory.For(ticketId, index);
        var slug = SpecJsonReader.ReadString(element, "slug") is { Length: > 0 } s
            ? PhaseIdFactory.Slug(s) : PhaseIdFactory.Slug(goal);
        var carried = CarriedBy(element, segments);
        var yaml = Render(element, phaseId, slug, goal, done, carried, ticketId, previous, evidence);
        if (validator.ValidateYaml(yaml) is SpecDraftInvalid invalid)
            return (null, $"phase {index + 1} ({phaseId}) is not a valid phase spec: {invalid.Error}");

        // The companion stays the ticket's own bytes: facts live in the yaml, never here.
        var markdown = SegmentExtractor.BuildMarkdown(phaseId, goal, carried, segments);
        return (new SpecPhase(draftReader.Read(yaml), slug, markdown, carried), null);
    }

    private string Render(
        JsonElement element, string phaseId, string slug, string goal, IReadOnlyList<string> done,
        IReadOnlyList<int> carried, string ticketId, IReadOnlyList<SpecPhase> previous,
        IReadOnlyList<string>? evidence) =>
        yamlRenderer.Render(
            phaseId, goal,
            // The sequence IS the requires-chain: each phase depends on the one before
            // it, because the repository it edits is the previous phase's output.
            previous.Count > 0 ? [previous[^1].PhaseId] : [],
            ReadSteps(element), done, $"{phaseId}-{slug}.md", carried, ticketId,
            factResolver.Resolve(envelope.Facts(element), evidence));

    private static List<int> CarriedBy(JsonElement element, IReadOnlyList<TicketSegment> segments) =>
        SpecJsonReader.ReadInts(element, "carries")
            .Where(id => segments.Any(seg => seg.Id == id))
            .Distinct()
            .OrderBy(id => id)
            .ToList();

    private static IReadOnlyList<(string Id, string Action)> ReadSteps(JsonElement element) =>
        [.. SpecJsonReader.ReadObjects(element, "steps")
            .Select(e => (
                Id: SpecJsonReader.ReadString(e, "id"),
                Action: SpecJsonReader.ReadString(e, "action")))
            .Where(s => s.Id.Length > 0 && s.Action.Length > 0)];
}
