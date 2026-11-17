using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog.Writes;

/// <summary>
/// 2026-11-17-5701a: one spec of a ticket written into a draft, as <c>tickets/&lt;key&gt;/&lt;label&gt;.yaml</c>. The
/// ticket must be in the draft; the label is placed and the text checked against the spec schema as a lone spec's is;
/// its repositories are decided against the ticket's when it names any, else the turn's scope. A label is the spec's
/// handle across the whole list, so one held by another ticket's spec, a ticket's key or the list's review id is
/// refused. The ticket is the specs' parent: their requires name specs of the same ticket and leave an order to build
/// them in. A refusal is the exact reason, and the draft stays as it was.
/// </summary>
public sealed class TicketSpecDraftWrite(
    SpecDraftIds ids, ISpecDraftValidator validator, DialogDraftRepos repos, SpecDraftReader reader, EpicChildOrderer orderer)
{
    public DraftWrite Write(ProposalDraft draft, string key, string label, string yaml, IReadOnlyList<string> scope)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(scope);
        var at = draft.Tickets.Select(t => t.Key).ToList().IndexOf(key);
        if (at < 0)
            return DraftWrite.Refused($"{Shown(key, label)} is a spec of ticket '{key}', which this draft does not hold — "
                + $"write {DraftPath.Shown(DraftPath.OfTicket(key))} first");
        if (Clash(draft, key, label) is { } clash) return DraftWrite.Refused(clash);
        var ticket = draft.Tickets[at];
        var (spec, error) = Read(label, yaml, ticket.Repos.Count > 0 ? ticket.Repos : scope);
        return spec is null ? DraftWrite.Refused(error!) : Place(draft, at, spec);
    }

    private (SpecDraft? Spec, string? Error) Read(string label, string yaml, IReadOnlyList<string> repoScope)
    {
        var (placed, error) = ids.Place(label, yaml ?? string.Empty);
        if (placed is null) return (null, error);
        var validation = validator.ValidateYaml(ids.ForSchema(label, placed));
        if (validation is SpecDraftInvalid invalid) return (null, $"spec '{label}': {invalid.Error}");
        if (validation is not SpecDraftValid) return (null, $"spec '{label}' is empty");
        var (decided, reposError) = repos.ForSpec(placed.Trim(), repoScope);
        return decided is null ? (null, $"spec '{label}': {reposError}") : (reader.Read(decided), null);
    }

    private DraftWrite Place(ProposalDraft draft, int at, SpecDraft spec)
    {
        var ticket = draft.Tickets[at];
        List<SpecDraft> specs = [.. ticket.Specs];
        var index = specs.FindIndex(s => s.SpecId == spec.SpecId);
        if (index >= 0) specs[index] = spec;
        else specs.Add(spec);
        if ((Unwritten(ticket.Key, specs) ?? orderer.Order(specs).Error) is { } refusal)
            return DraftWrite.Refused($"ticket '{ticket.Key}': {refusal}");
        List<GraphTicket> tickets = [.. draft.Tickets];
        tickets[at] = ticket with { Specs = specs };
        return DraftWrite.To(draft with { Tickets = tickets });
    }

    private static string? Clash(ProposalDraft draft, string key, string label)
    {
        if (label == ProposalReviewTexts.ListId)
            return $"'{label}' names the list's own review — give the spec another label";
        if (draft.Tickets.Any(t => t.Key == label))
            return $"'{label}' is a ticket's key — give the spec a label no ticket of the draft is named by";
        return draft.Tickets.FirstOrDefault(t => t.Key != key && t.Specs.Any(s => s.SpecId == label)) is { } other
            ? $"{Shown(other.Key, label)} holds '{label}' already — a label names one spec of the whole list; give this spec another"
            : null;
    }

    // A requirement naming a spec — by label or id — must be a spec of the same ticket; a sentence is an outside
    // precondition.
    private static string? Unwritten(string key, IReadOnlyList<SpecDraft> specs)
    {
        var labels = specs.Select(s => s.SpecId).ToHashSet(StringComparer.Ordinal);
        foreach (var spec in specs)
            foreach (var id in spec.Requires.Where(r => !labels.Contains(r) && Names(r)))
                return $"'{spec.SpecId}' requires '{id}', which no spec of this ticket is — write {Shown(key, id)} first, "
                    + "or state an outside precondition as a sentence";
        return null;
    }

    private static bool Names(string requirement) =>
        SpecDraftIds.IsLabel(requirement) || RequiresEdgeChecker.SpecIdRegex().IsMatch(requirement);

    private static string Shown(string key, string label) => DraftPath.Shown(DraftPath.OfTicketSpec(key, label));
}
