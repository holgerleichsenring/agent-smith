using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: the shape a run falls back to when the split cannot be trusted — ONE
/// phase carrying the WHOLE ticket.
/// <para>
/// Fail safe means falling back to a shape that is known to work, not partially
/// applying one that is not. Degrading to too much context is cheap; degrading to a
/// missing manual nobody noticed is the failure the accounting exists to prevent.
/// </para>
/// </summary>
public sealed class SpecFallback(
    ISpecDraftValidator validator,
    PhaseDraftReader draftReader,
    DerivedPhaseYamlRenderer yamlRenderer)
{
    public SpecSet Build(
        string key, Ticket ticket, IReadOnlyList<TicketSegment> segments,
        IReadOnlyList<string> doneCriteria, SpecSource source)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        var phaseId = PhaseIdFactory.For(ticket.Id.Value, 0);
        var goal = string.IsNullOrWhiteSpace(ticket.Title) ? $"Ticket {ticket.Id.Value}" : ticket.Title;
        var slug = PhaseIdFactory.Slug(goal);
        var fileStem = $"{phaseId}-{slug}";
        var done = doneCriteria.Count > 0 ? doneCriteria : TicketCriteria(ticket);

        var yaml = yamlRenderer.Render(
            phaseId, goal, [], [], done, $"{fileStem}.md",
            [.. segments.Select(s => s.Id)], ticket.Id.Value);
        // A yaml this method composes and cannot validate is a composition bug, not a
        // model failure — the fallback has no second chance to fall back to.
        if (validator.ValidateYaml(yaml) is SpecDraftInvalid invalid)
            throw new InvalidOperationException(
                $"The single-phase fallback for {key} is not a valid phase spec: {invalid.Error}");

        var markdown = SegmentExtractor.BuildWholeTicketMarkdown(phaseId, goal, segments);
        return new SpecSet(
            key,
            [new SpecPhase(draftReader.Read(yaml), slug, markdown, [.. segments.Select(s => s.Id)])],
            SpecAccounting.Empty,
            [new SpecRevision(1, SpecRevisionCause.Initial, DateTimeOffset.UtcNow)],
            source,
            Handback: null,
            TicketPinnedWhole: true);
    }

    /// <summary>
    /// The ticket's own acceptance criteria, one per line. Nothing is invented here: a
    /// ticket that states none leaves the phase with none, and the run says so rather
    /// than scoring itself against a criterion no human wrote.
    /// </summary>
    private static IReadOnlyList<string> TicketCriteria(Ticket ticket) =>
        string.IsNullOrWhiteSpace(ticket.AcceptanceCriteria)
            ? []
            : [.. TicketHtmlConverter.ToText(ticket.AcceptanceCriteria)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(line => line.TrimStart('-', '*', ' ').Trim())
                .Where(line => line.Length > 0)];
}
