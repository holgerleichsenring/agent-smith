using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.SpecDialog.Writes;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-11-19-3d7e: the mechanical findings of one stored draft file, for the answer of the write that stored it —
/// a spec's cited evidence (<see cref="ProposalEvidenceReview"/>), a ticket's <c>ticket:&lt;id&gt;</c> requires
/// (<see cref="TicketExistenceFindings"/>). Advisory: the file stays stored. The same checks still run on the whole
/// draft after the turn (<see cref="ProposalMechanicalChecks"/>).
/// </summary>
public sealed class DraftWriteFindings(
    ProposalEvidenceReview evidence, TicketExistenceFindings existence, ILogger<DraftWriteFindings> logger)
{
    public const string Heading = "Findings to fix:";

    public const string Unchecked = "Its evidence and required tickets could not be checked.";

    /// <summary>The block to append to the answer for the file at <paramref name="bare"/> in <paramref name="draft"/>:
    /// empty when the file is neither a spec nor a ticket, or has no finding.</summary>
    public async Task<string> OfAsync(PipelineContext pipeline, ProposalDraft draft, string bare, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(draft);
        try
        {
            var findings = await FindingsAsync(pipeline, draft, bare, ct);
            return findings.Count == 0 ? string.Empty
                : $"\n{Heading}\n{string.Join("\n", findings.Select(Line))}";
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "The findings of the stored draft file {Path} could not be checked", bare);
            return $"\n{Unchecked}";
        }
    }

    private async Task<IReadOnlyList<ProposalFinding>> FindingsAsync(
        PipelineContext pipeline, ProposalDraft draft, string bare, CancellationToken ct)
    {
        if (SpecAt(draft, bare) is { } spec)
            return await evidence.FindingsAsync(pipeline, new SpecOutcome(spec), ct);
        var list = new TicketGraphOutcome(draft.Tickets); // normalizes ticket:<sibling> over the whole draft
        if (list.Tickets.FirstOrDefault(t => DraftPath.OfTicket(t.Key) == bare) is { } ticket)
            return await existence.FindingsAsync(pipeline, new TicketGraphOutcome([ticket]), ct);
        return [];
    }

    private static SpecDraft? SpecAt(ProposalDraft draft, string bare) =>
        draft.Specs.FirstOrDefault(s => DraftPath.OfSpec(s.Draft.SpecId, s.Parent) == bare)?.Draft
        ?? draft.Tickets.SelectMany(t => t.Specs.Where(s => DraftPath.OfTicketSpec(t.Key, s.SpecId) == bare)).FirstOrDefault();

    private static string Line(ProposalFinding finding) =>
        $"- {finding.Problem}: {finding.Why}" + (finding.Quote is { Length: > 0 } quote ? $" (claim: \"{quote}\")" : string.Empty);
}
