using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Tickets;

namespace AgentSmith.Application.Services.Filing;

/// <summary>
/// 2026-11-17-5701b: the planned tickets of a lone spec and an epic, and the parts any ordered set of specs is filed
/// as. A set longer than one series may hold (series.max_specs) files as several tickets in order, each later part
/// requiring the one before — so it starts through its requires edge once the earlier part merges.
/// </summary>
public sealed class SeriesFilingPlan(SpecTicketRenderer renderer, EpicChildOrderer orderer, FiledSeriesFactory seriesFiling)
{
    public IReadOnlyList<PlannedTicket> Spec(FilingRequest request, ResolvedProject project, SpecOutcome spec)
    {
        ArgumentNullException.ThrowIfNull(request);
        // 2026-10-06-03c7c: the series is minted and re-id'd BEFORE anything renders.
        var series = seriesFiling.Mint([spec.Draft], spec);
        var (labels, note) = Stamp(project);
        return [new PlannedTicket("spec", TicketFilingRole.Spec,
            _ => DecidedQuestionLines.Appended(renderer.RenderSpec(series.Drafts[0], request.Conversation, note), spec.Questions),
            series, series.Drafts[0].Goal, [], labels)];
    }

    /// <summary>2026-09-17-0e79d: an approved cut is ONE piece of work — one work ticket per part, from the parent.</summary>
    public IReadOnlyList<PlannedTicket> Epic(FilingRequest request, ResolvedProject project, EpicOutcome epic)
    {
        ArgumentNullException.ThrowIfNull(request);
        var parts = Parts(epic.Children, epic, "The epic");
        var (labels, note) = Stamp(project);
        return [.. parts.Select((series, i) => new PlannedTicket(
            PartKey("epic", i, parts.Count), TicketFilingRole.Work,
            _ => DecidedQuestionLines.Appended(renderer.RenderEpicParent(
                epic.Parent, series.Drafts, epic.Templates, request.Conversation, note, series.Id), epic.Questions),
            series, PartGoal(epic.Parent.Goal, i, parts.Count),
            i == 0 ? [] : [PartKey("epic", i - 1, parts.Count)], labels))];
    }

    /// <summary>The set in the order the pane showed it, cut into series; an order that cannot be made refuses.</summary>
    public IReadOnlyList<FiledSeries> Parts(IReadOnlyList<SpecDraft> specs, OutcomeProposal source, string owner)
    {
        var order = orderer.Order(specs);
        if (order.Error is not null) throw new InvalidOperationException($"{owner} cannot be filed: {order.Error}.");
        return seriesFiling.MintParts(order.Children, source);
    }

    /// <summary>The approved-set stamp and the note on the ticket body that says what it binds.</summary>
    public static (IReadOnlyList<string> Labels, string? Note) Stamp(ResolvedProject project)
    {
        var labels = ApprovedSetTicketFiler.Labels(project);
        return (labels, TicketLabelNote.For(labels, labels[0]));
    }

    public static string PartKey(string key, int index, int count) => count == 1 ? key : $"{key} part {index + 1}";

    public static string PartGoal(string goal, int index, int count) =>
        count == 1 ? goal : $"{goal} (part {index + 1} of {count})";
}
