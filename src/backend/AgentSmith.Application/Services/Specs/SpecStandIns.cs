using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-11-19-6b0ba: what DeriveSpec writes when a ticket has no spec to build — the executed head kept, and per
/// repository in scope one spec carrying the WHOLE ticket and naming that repository (goal = the title, done = the
/// ticket's own criteria). No model cuts anything here: the coding master reads the ticket and the code and writes
/// the real spec as the first part of its work (revise_spec), and the set records which specs are still stand-ins.
/// It replaces the deriver's cut and the p0393a single-spec fallback with one shape.
/// </summary>
public sealed class SpecStandIns(
    ISpecDraftValidator validator,
    SpecDraftReader draftReader,
    DerivedSpecYamlRenderer yamlRenderer)
{
    /// <param name="previous">The set the branch carries; its executed head is kept as it ran.</param>
    /// <param name="taken">Ids (or spec file names) a new spec must not take.</param>
    public SpecSet Build(
        string key, string series, Ticket ticket, IReadOnlyList<TicketSegment> segments, SpecSet? previous,
        SpecSource source, IReadOnlyList<string> repos, IReadOnlyCollection<string> taken)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        ArgumentNullException.ThrowIfNull(repos);
        var head = previous?.ExecutedHead ?? [];
        IReadOnlyCollection<string> skip = [.. taken, .. head.Select(p => p.SpecId)];
        var specs = new List<DerivedSpec>(head);
        var letter = head.Count;
        foreach (var repo in repos.Count == 0 ? [string.Empty] : repos)
        {
            var (specId, at) = SeriesIdFactory.NextFree(series, letter, skip);
            letter = at + 1;
            specs.Add(WholeTicket(key, specId, ticket, segments, repo.Length == 0 ? [] : [repo]));
        }
        return new SpecSet(
            key, specs, SpecAccounting.Empty,
            [new SpecRevision(1, SpecRevisionCause.Initial, DateTimeOffset.UtcNow)],
            source, Handback: null, TicketPinnedWhole: true,
            ExecutedSpecIds: [.. head.Select(p => p.SpecId)], Series: series)
        {
            StandIns = [.. specs.Skip(head.Count).Select(p => p.SpecId)],
        };
    }

    private DerivedSpec WholeTicket(
        string key, string specId, Ticket ticket, IReadOnlyList<TicketSegment> segments, IReadOnlyList<string> repos)
    {
        var goal = string.IsNullOrWhiteSpace(ticket.Title) ? $"Ticket {ticket.Id.Value}" : ticket.Title;
        var slug = SpecIdFactory.Slug(goal);
        var yaml = yamlRenderer.Render(
            specId, goal, [], [], TicketCriteria(ticket), $"{specId}-{slug}.md",
            [.. segments.Select(s => s.Id)], ticket.Id.Value, repos: repos);
        // A yaml this method composes and cannot validate is a composition bug, not a ticket shape.
        if (validator.ValidateYaml(yaml) is SpecDraftInvalid invalid)
            throw new InvalidOperationException($"The stand-in spec for {key} is not a valid spec: {invalid.Error}");
        var markdown = SegmentExtractor.BuildWholeTicketMarkdown(specId, goal, segments);
        return new DerivedSpec(draftReader.Read(yaml) with { Repos = repos }, slug, markdown, [.. segments.Select(s => s.Id)]);
    }

    /// <summary>
    /// The ticket's own acceptance criteria, one per line. Nothing is invented here: a ticket that states none leaves
    /// the stand-in with none, and its master writes them. 2026-09-17-042eb: with no tracker field, the body's own
    /// section is the ticket's statement.
    /// </summary>
    private static IReadOnlyList<string> TicketCriteria(Ticket ticket) =>
        string.IsNullOrWhiteSpace(ticket.AcceptanceCriteria)
            ? AcceptanceCriteriaSection.Read(ticket.Description)
            : [.. TicketHtmlConverter.ToText(ticket.AcceptanceCriteria)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(CriterionLine.StripMarker)
                .Where(line => !CriterionLine.IsPlaceholder(line))];
}
