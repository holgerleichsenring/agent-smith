using System.Text.Json;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using Kinds = AgentSmith.Server.Models.TicketConversationKinds;
using Roles = AgentSmith.Server.Models.TicketConversationRoles;
using Sources = AgentSmith.Server.Models.TicketConversationSources;

namespace AgentSmith.Server.Services.Tickets.Conversation;

/// <summary>
/// 2026-10-13-9a59h: the entries a ticket's attempts recorded, each under its run. A question with its
/// answer from the inbox (a deadline answer is the system's default); a hand-back and every cut review; and
/// an edit only where consecutive attempts fetched different fingerprints — the changed fields of title,
/// description and criteria, timed at the later fetch and with no author, which no tracker read gives.
/// Questions, hand-backs and cut reviews are markdown; an answer is plain text unless it was read off a ticket
/// comment, whose format it keeps.
/// </summary>
public static class ConversationRunEntries
{
    private const string DeadlineComment = "timeout";
    private const string DeadlineAuthor = "system";
    private static readonly TimeSpan SameMoment = TimeSpan.FromSeconds(1);

    public static IEnumerable<TicketConversationEntryView> Of(
        IReadOnlyList<Run> attempts, ConversationRunRows rows, IReadOnlyList<TicketComment> thread)
    {
        foreach (var row in rows.Entries.OrderBy(e => e.At))
        {
            switch (row.Kind)
            {
                case RunConversationKinds.Question when row.QuestionId is { } questionId:
                    yield return Entry($"{Sources.Question}:{row.RunId}:{questionId}", questionId, Sources.Question,
                        row.Kind, Roles.Agent, row, row.Text);
                    if (rows.Answers.FirstOrDefault(a => a.DialogueJobId == row.RunId && a.QuestionId == questionId) is { } answer)
                        yield return Answer(row.RunId, answer, thread);
                    break;
                case RunConversationKinds.HandBack:
                case RunConversationKinds.CutReview:
                    yield return Entry($"{Sources.SpecCut}:{row.RunId}:{row.EventSeq ?? row.Id}", null, Sources.SpecCut,
                        row.Kind, Roles.Agent, row, row.Text, Detail(row.DetailJson)) with { IsOurs = true };
                    break;
            }
        }
        foreach (var edit in Edits(attempts, rows)) yield return edit;
    }

    private static TicketConversationEntryView Answer(string runId, DialogueAnswerEntry answer, IReadOnlyList<TicketComment> thread)
    {
        var deadline = answer.Comment == DeadlineComment && answer.AnsweredBy == DeadlineAuthor;
        return new TicketConversationEntryView($"{Sources.Question}:{runId}:{answer.QuestionId}:answer", answer.QuestionId,
            null, Sources.Question, deadline ? Kinds.DefaultAnswer : Kinds.Answer, deadline ? Roles.System : Roles.Person,
            deadline ? null : answer.AnsweredBy, false, answer.AnsweredAt, runId, null, answer.Answer,
            Detail: new { answer.Comment, IsDefault = deadline }, Format: AnswerFormat(answer, thread));
    }

    // The thread comment the answer was read from: same author and time. A comment no longer in the thread
    // was written on Azure DevOps, the only tracker that is polled, whose discussion editor writes HTML.
    private static string AnswerFormat(DialogueAnswerEntry answer, IReadOnlyList<TicketComment> thread) =>
        answer.Comment != DialogAnswer.FromTicketComment ? TicketTextFormat.Text
        : thread.FirstOrDefault(c => string.Equals(c.Author, answer.AnsweredBy, StringComparison.Ordinal)
            && (c.CreatedAt - answer.AnsweredAt).Duration() < SameMoment)?.Format ?? TicketTextFormat.Html;

    private static IEnumerable<TicketConversationEntryView> Edits(IReadOnlyList<Run> attempts, ConversationRunRows rows)
    {
        var fetched = attempts.Select(run => (Run: run,
                Fingerprint: rows.Attempts.FirstOrDefault(a => a.RunId == run.Id)?.TicketFingerprint,
                Text: rows.Entries.Where(e => e.RunId == run.Id && e.Kind == RunConversationKinds.TicketText)
                    .OrderBy(e => e.EventSeq).ThenBy(e => e.Id).FirstOrDefault()))
            .Where(f => f.Fingerprint is not null).ToList();
        for (var i = 1; i < fetched.Count; i++)
        {
            var (before, after) = (fetched[i - 1], fetched[i]);
            if (before.Fingerprint == after.Fingerprint) continue;
            yield return new TicketConversationEntryView(
                $"{Sources.Ticket}:{after.Run.Id}:{after.Text?.EventSeq?.ToString() ?? "fetch"}", null, null, Sources.Ticket,
                Kinds.Edited, Roles.Person, null, false, after.Text?.At ?? after.Run.ActsReadAt ?? after.Run.StartedAt,
                after.Run.Id, null, null, Detail: Diff(before.Run, before.Text, after.Run, after.Text));
        }
    }

    private static Dictionary<string, object> Diff(Run beforeRun, RunConversationEntry? before, Run afterRun, RunConversationEntry? after)
    {
        var diff = new Dictionary<string, object>(StringComparer.Ordinal);
        Changed(diff, "title", beforeRun.TicketTitle, afterRun.TicketTitle);
        Changed(diff, "description", before?.Text, after?.Text);
        Changed(diff, "criteria", Criteria(before), Criteria(after));
        return diff;
    }

    private static void Changed(Dictionary<string, object> diff, string field, string? before, string? after)
    {
        if (!string.Equals(before ?? string.Empty, after ?? string.Empty, StringComparison.Ordinal))
            diff[field] = new { Before = before, After = after };
    }

    private static string? Criteria(RunConversationEntry? row) =>
        Detail(row?.DetailJson) is { } detail && detail.TryGetProperty("acceptanceCriteria", out var criteria)
            && criteria.ValueKind == JsonValueKind.String ? criteria.GetString() : null;

    private static JsonElement? Detail(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            return JsonDocument.Parse(json).RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static TicketConversationEntryView Entry(string id, string? sourceId, string source, string kind, string role,
        RunConversationEntry row, string? text, object? detail = null) =>
        new(id, sourceId, null, source, kind, role, null, false, row.At, row.RunId, null, text, Detail: detail,
            Format: TicketTextFormat.Markdown);
}
