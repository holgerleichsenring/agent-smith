using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: the shape a run falls back to when the split cannot be trusted — ONE
/// spec carrying the WHOLE ticket.
/// <para>
/// Fail safe means falling back to a shape that is known to work, not partially
/// applying one that is not. Degrading to too much context is cheap; degrading to a
/// missing manual nobody noticed is the failure the accounting exists to prevent.
/// </para>
/// <para>
/// 2026-10-12-f5f7a: a spec names exactly one repository, so the whole-ticket shape exists only
/// for a scope of one (or none); over several repositories the fallback is a hand-back naming them
/// (<see cref="SpecScopeHandback"/>), because which one would carry the whole ticket is a guess.
/// </para>
/// </summary>
public sealed class SpecFallback(
    ISpecDraftValidator validator,
    SpecDraftReader draftReader,
    DerivedSpecYamlRenderer yamlRenderer)
{
    public SpecSet Build(
        string key, string series, Ticket ticket, IReadOnlyList<TicketSegment> segments,
        IReadOnlyList<string> doneCriteria, SpecSource source, IReadOnlyList<string> repos)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        ArgumentNullException.ThrowIfNull(repos);
        return repos.Count > 1
            ? SpecScopeHandback.For(key, series, source, repos)
            : WholeTicket(key, series, ticket, segments, doneCriteria, source, repos);
    }

    private SpecSet WholeTicket(
        string key, string series, Ticket ticket, IReadOnlyList<TicketSegment> segments,
        IReadOnlyList<string> doneCriteria, SpecSource source, IReadOnlyList<string> repos)
    {
        var specId = SeriesIdFactory.Member(series, 0);
        var goal = string.IsNullOrWhiteSpace(ticket.Title) ? $"Ticket {ticket.Id.Value}" : ticket.Title;
        var slug = SpecIdFactory.Slug(goal);
        var fileStem = $"{specId}-{slug}";
        var done = doneCriteria.Count > 0 ? doneCriteria : TicketCriteria(ticket);

        var yaml = yamlRenderer.Render(
            specId, goal, [], [], done, $"{fileStem}.md",
            [.. segments.Select(s => s.Id)], ticket.Id.Value, repos: repos);
        // A yaml this method composes and cannot validate is a composition bug, not a
        // model failure — the fallback has no second chance to fall back to.
        if (validator.ValidateYaml(yaml) is SpecDraftInvalid invalid)
            throw new InvalidOperationException(
                $"The single-phase fallback for {key} is not a valid phase spec: {invalid.Error}");

        var markdown = SegmentExtractor.BuildWholeTicketMarkdown(specId, goal, segments);
        return new SpecSet(
            key,
            [new SpecPhase(draftReader.Read(yaml), slug, markdown, [.. segments.Select(s => s.Id)])],
            SpecAccounting.Empty,
            [new SpecRevision(1, SpecRevisionCause.Initial, DateTimeOffset.UtcNow)],
            source,
            Handback: null,
            TicketPinnedWhole: true,
            Series: series);
    }

    /// <summary>
    /// The ticket's own acceptance criteria, one per line. Nothing is invented here: a
    /// ticket that states none leaves the spec with none, and the run says so rather
    /// than scoring itself against a criterion no human wrote. 2026-09-17-042eb: with no
    /// tracker field, the body's own section is the ticket's statement.
    /// </summary>
    private static IReadOnlyList<string> TicketCriteria(Ticket ticket) =>
        string.IsNullOrWhiteSpace(ticket.AcceptanceCriteria)
            ? AcceptanceCriteriaSection.Read(ticket.Description)
            : [.. TicketHtmlConverter.ToText(ticket.AcceptanceCriteria)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(CriterionLine.StripMarker)
                .Where(line => !CriterionLine.IsPlaceholder(line))];
}
