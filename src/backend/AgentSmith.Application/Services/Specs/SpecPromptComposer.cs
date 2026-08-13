using System.Text;
using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: composes the derivation user prompt — the SEGMENTED ticket (the anchor
/// vocabulary), its conversation and attachment list, the AnalyzeCode-derived
/// per-repo code maps, and on re-entry the previous cut plus the cause of the
/// amendment. Pure mapping.
/// </summary>
internal static class SpecPromptComposer
{
    public static string Compose(
        Ticket ticket, IReadOnlyList<TicketSegment> segments, SpecSet? previous, string cause,
        PipelineContext pipeline)
    {
        var sb = new StringBuilder();
        // p0316: ticket fields are untrusted — delimited so an embedded injection reads
        // as data, exactly as the master prompts treat them.
        sb.AppendLine(TicketPromptDelimiters.Wrap($"""
            **Title:** {ticket.Title}
            **Acceptance Criteria:** {CriteriaOf(ticket)}

            ## The ticket, segment by segment
            Each block is prefixed with the id you address it by. Answer with ids only.

            {TicketSegmenter.Render(segments)}
            """));
        AppendConversation(sb, pipeline);
        AppendAttachments(sb, pipeline);
        AppendCodeMaps(sb, pipeline);
        AppendWorkShape(sb, pipeline);
        AppendPrevious(sb, previous, cause);
        return sb.ToString();
    }

    // p0413: the shape the scope classifier stated for this ticket — the input to
    // the master's cut-sizing rule. Absent (no classification ran, or the model
    // stated none) renders nothing, so the cut is the one it always was.
    private static void AppendWorkShape(StringBuilder sb, PipelineContext pipeline)
    {
        var shape = pipeline.TryGet<WorkShapeVerdict>(ContextKeys.WorkShape, out var s) ? s : null;
        var rendered = WorkShapePromptSection.Render(shape);
        if (rendered.Length > 0) sb.AppendLine(rendered);
    }

    // p0399: acceptance criteria arrive in the same transport encoding as the body —
    // the derivation reads them verbatim, so they get the same one-time conversion.
    private static string CriteriaOf(Ticket ticket) =>
        string.IsNullOrWhiteSpace(ticket.AcceptanceCriteria)
            ? "None specified"
            : TicketHtmlConverter.ToText(ticket.AcceptanceCriteria);

    private static void AppendConversation(StringBuilder sb, PipelineContext pipeline)
    {
        var comments = pipeline.TryGet<IReadOnlyList<TicketComment>>(
            ContextKeys.TicketComments, out var c) ? c : null;
        var rendered = TicketConversationPromptSection.Render(comments);
        if (rendered.Length > 0) sb.AppendLine().AppendLine(rendered);
    }

    // p0317: the attachment NAMES, not their bytes — the derivation call has no sandbox
    // to read them from. A named attachment tells the model a rule may be stated outside
    // the prose, so it says so rather than inventing one.
    private static void AppendAttachments(StringBuilder sb, PipelineContext pipeline)
    {
        var refs = pipeline.TryGet<IReadOnlyList<AttachmentRef>>(
            ContextKeys.TicketAttachmentRefs, out var r) ? r : null;
        if (refs is null || refs.Count == 0) return;
        sb.AppendLine();
        sb.AppendLine("## Ticket attachments");
        foreach (var attachment in refs) sb.AppendLine($"- {attachment.FileName}");
    }

    private static void AppendCodeMaps(StringBuilder sb, PipelineContext pipeline)
    {
        if (!pipeline.TryGet<IReadOnlyDictionary<string, string>>(
                ContextKeys.RepoCodeMaps, out var maps) || maps is null || maps.Count == 0)
            return;
        sb.AppendLine();
        sb.AppendLine("## Codebase analysis");
        sb.AppendLine(
            "This is what the repositories actually contain. A requirement that contradicts "
            + "it is a hand-back, not a phase.");
        foreach (var (repoName, codeMap) in maps)
        {
            if (string.IsNullOrWhiteSpace(codeMap)) continue;
            sb.AppendLine($"### Repository: {repoName}");
            sb.AppendLine(codeMap);
        }
    }

    private static void AppendPrevious(StringBuilder sb, SpecSet? previous, string cause)
    {
        if (previous is null) return;
        sb.AppendLine();
        sb.AppendLine("## The previous cut — AMEND it, do not re-derive from the prose");
        sb.AppendLine($"Cause of the revision you are writing now: {cause}");
        foreach (var phase in previous.Phases)
        {
            var executed = previous.Executed.Contains(phase.PhaseId, StringComparer.Ordinal);
            sb.AppendLine(
                $"- {phase.PhaseId} ({(executed ? "EXECUTED — keep exactly as it is" : "not started")}): "
                + phase.Draft.Goal);
        }
        sb.AppendLine();
        sb.AppendLine(
            "Return the WHOLE set again. Repeat every executed phase unchanged — same goal, "
            + "same done-list, same order. You may merge, split or reorder only the phases "
            + "that have not started.");
    }
}
