using System.Text;
using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.WorkSpecs;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.WorkSpecs;

/// <summary>
/// p0390: composes the derivation user prompt — the delimited (untrusted) ticket
/// with its conversation and attachment list, the AnalyzeCode-derived per-repo
/// code maps, and on re-entry the PREVIOUS revision plus the cause of this one.
/// Pure mapping.
/// </summary>
internal static class WorkSpecPromptComposer
{
    public static string Compose(
        Ticket ticket, WorkSpecArtifact? previous, string cause,
        PipelineContext pipeline, IWorkSpecSerializer serializer)
    {
        var sb = new StringBuilder();
        // p0316: ticket fields are untrusted — delimited so an embedded injection
        // reads as data, exactly as the master prompts treat them.
        sb.AppendLine(TicketPromptDelimiters.Wrap($"""
            **Title:** {ticket.Title}
            **Description:** {ticket.Description}
            **Acceptance Criteria:** {ticket.AcceptanceCriteria ?? "None specified"}
            """));
        AppendConversation(sb, pipeline);
        AppendAttachments(sb, pipeline);
        AppendCodeMaps(sb, pipeline);
        AppendPrevious(sb, previous, cause, serializer);
        return sb.ToString();
    }

    private static void AppendConversation(StringBuilder sb, PipelineContext pipeline)
    {
        var comments = pipeline.TryGet<IReadOnlyList<TicketComment>>(
            ContextKeys.TicketComments, out var c) ? c : null;
        var rendered = TicketConversationPromptSection.Render(comments);
        if (rendered.Length > 0) sb.AppendLine().AppendLine(rendered);
    }

    // p0317: the attachment NAMES, not their bytes — the derivation call has no
    // sandbox to read them from. A named attachment tells the model a rule may be
    // stated outside the prose, so it records an assumption rather than inventing one.
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
        foreach (var (repoName, codeMap) in maps)
        {
            if (string.IsNullOrWhiteSpace(codeMap)) continue;
            sb.AppendLine($"### Repository: {repoName}");
            sb.AppendLine(codeMap);
        }
    }

    private static void AppendPrevious(
        StringBuilder sb, WorkSpecArtifact? previous, string cause, IWorkSpecSerializer serializer)
    {
        if (previous is null) return;
        sb.AppendLine();
        sb.AppendLine("## Previous revision — AMEND this, do not re-derive it");
        sb.AppendLine($"Cause of the revision you are writing now: {cause}");
        sb.AppendLine("```yaml");
        sb.AppendLine(serializer.Serialize(previous.Spec));
        sb.AppendLine("```");
        if (string.IsNullOrWhiteSpace(previous.SamplesMarkdown)) return;
        sb.AppendLine("### Its spec.md (samples)");
        sb.AppendLine("```markdown");
        sb.AppendLine(previous.SamplesMarkdown);
        sb.AppendLine("```");
    }
}
