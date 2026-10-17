using System.Text.Json;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-07-b7e2: turns ONE spec element of the derivation reply into a spec — its
/// id by position, its slug, the schema-valid yaml, the byte-exact companion and, now,
/// its resolved facts. 2026-10-06-03c7c: the id is the series' base plus a letter, handed in. Split from <see cref="SpecDerivationParser"/>, which reads the
/// reply's envelope and the ordered set; one element's shape is its own question.
/// </summary>
public sealed class DerivedSpecBuilder(
    ISpecDraftValidator validator,
    SpecDraftReader draftReader,
    DerivedSpecYamlRenderer yamlRenderer,
    SpecDerivationEnvelope envelope,
    FactResolver factResolver)
{
    // p0400c's obligation, pointed at what the gate now measures (p0421): the gate asks
    // what the BRANCH satisfies, so the thing that may not be missing is the criteria
    // themselves — a spec whose completion cannot be stated is a spec nobody can
    // account for. The deriver's retry loop hands the rejection back to the model.
    private const string NoCriteria =
        "states no done-criteria. Every spec must state what is true when it is finished, "
        + "in terms someone can check against the repository — a spec that cannot end is not a spec.";

    /// <param name="specId">2026-10-12-f5f7d: the id the sequence chose — the position's letter, or
    /// the next free one when that id is taken (<see cref="SeriesIdFactory.NextFree"/>).</param>
    /// <param name="shared">2026-10-17-293ba: the reply's shared segments, carried beside this spec's own.</param>
    public (DerivedSpec? Phase, string? Error) Build(
        JsonElement element, int index, string specId, string ticketId,
        IReadOnlyList<TicketSegment> segments, IReadOnlyList<DerivedSpec> previous,
        IReadOnlyList<EvidenceLook>? evidence, IReadOnlyList<string>? repos = null, IReadOnlyList<int>? shared = null)
    {
        var goal = SpecJsonReader.ReadString(element, "goal");
        if (goal.Length == 0) return (null, $"spec {index + 1} has no goal");
        var done = SpecJsonReader.ReadStrings(element, "done");
        if (done.Count == 0) return (null, $"spec {index + 1} {NoCriteria}");

        var slug = SpecJsonReader.ReadString(element, "slug") is { Length: > 0 } s
            ? SpecIdFactory.Slug(s) : SpecIdFactory.Slug(goal);
        var carried = CarriedBy(element, segments, shared ?? []);
        var yaml = Render(element, specId, slug, goal, done, carried, ticketId, previous, evidence, repos ?? []);
        if (validator.ValidateYaml(yaml) is SpecDraftInvalid invalid)
            return (null, $"spec {index + 1} ({specId}) is not a valid spec: {invalid.Error}");

        // The companion stays the ticket's own bytes: facts live in the yaml, never here.
        var markdown = SegmentExtractor.BuildMarkdown(specId, goal, carried, segments);
        return (new DerivedSpec(draftReader.Read(yaml), slug, markdown, carried), null);
    }

    private string Render(
        JsonElement element, string specId, string slug, string goal, IReadOnlyList<string> done,
        IReadOnlyList<int> carried, string ticketId, IReadOnlyList<DerivedSpec> previous,
        IReadOnlyList<EvidenceLook>? evidence, IReadOnlyList<string> repos) =>
        yamlRenderer.Render(
            specId, goal,
            // The sequence IS the requires-chain: each spec depends on the one before
            // it, because the repository it edits is the previous spec's output.
            previous.Count > 0 ? [previous[^1].SpecId] : [],
            ReadSteps(element), done, $"{specId}-{slug}.md", carried, ticketId,
            factResolver.Resolve(envelope.Facts(element), evidence), envelope.Contexts(element), repos);

    // 2026-10-17-293ba: what every unit needs, then this spec's own unit — one list, so the yaml
    // scope line, the companion and the accounting cannot disagree about what the spec carries.
    private static List<int> CarriedBy(
        JsonElement element, IReadOnlyList<TicketSegment> segments, IReadOnlyList<int> shared) =>
        shared.Concat(SpecJsonReader.ReadInts(element, "carries"))
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
