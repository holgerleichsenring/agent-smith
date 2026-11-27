using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Series;
using AgentSmith.Server.Services.SpecDialog.Continuation;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Services.Series;

/// <summary>
/// 2026-11-27-3532d: the series master's retirement of a ticket's unstarted spec, recorded through the one edit of a worked
/// ticket's series (<see cref="SeriesEditAct"/>) under the series' conversation and approver — the plan's approval, as
/// add_ticket files under it. The replacement is an appended spec naming the retired one in <c>supersedes</c>; the edit
/// list places it and re-links what required the retired spec, the decision mints its id.
/// </summary>
public sealed class SeriesSpecRetirement(
    TicketProjects projects, SeriesEditAct edits, ISpecDraftValidator validator, SpecDraftReader reader, SpecDraftIdRewriter rewriter)
    : ISeriesSpecRetirement
{
    public async Task<SeriesCutAnswer> RetireAsync(
        SeriesRunRecord series, string ticketId, string specId, string yaml, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(series);
        if (projects.Of(series.Project) is not { } project) return new($"Project '{series.Project}' is not configured.", false);
        if (string.IsNullOrEmpty(series.Conversation))
            return new("This series has no conversation to record the edit under; nothing was recorded.", false);
        if (validator.ValidateYaml(yaml ?? string.Empty) is SpecDraftInvalid invalid)
            return new($"Nothing was recorded: the replacement is not a valid spec: {invalid.Error}.", false);
        var draft = reader.Read(yaml!);
        if (draft.Requires.Contains(specId, StringComparer.Ordinal))
            return new($"Nothing was recorded: the replacement takes {specId}'s place, so it cannot require it.", false);
        var placeholder = $"{specId}-replacement";
        var text = SpecSupersedesLine.Stamp(rewriter.Rewrite(draft.Yaml, new Dictionary<string, string> { [draft.SpecId] = placeholder }), specId);
        var answer = await edits.EditAsync(project, ticketId, series.Conversation, series.Principal,
            [new SeriesEdit(SeriesEditKinds.Append, Yaml: text)], cancellationToken);
        return new(answer, answer.StartsWith(SeriesEditAct.Recorded, StringComparison.Ordinal), ticketId);
    }
}
