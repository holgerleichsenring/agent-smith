using System.Globalization;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Reviews;

namespace AgentSmith.Application.Services.Prompts;

/// <summary>
/// 2026-10-08-e8b9d: the review on the previous attempt's pull requests as a delimited prompt section
/// — untrusted data like the ticket conversation, each thread with its file and line and whether it is
/// still open, fitted newest thread first under the conversation's own budget.
/// </summary>
public static class PrReviewFeedbackPromptSection
{
    public static string Render(PipelineContext pipeline)
    {
        if (!pipeline.TryGet<IReadOnlyList<PrReviewFeedback>>(ContextKeys.PrReviewFeedback, out var feedback)
            || feedback is not { Count: > 0 }) return string.Empty;
        var blocks = feedback.SelectMany(f => f.Threads.Select(t => (Newest: t.Notes.Max(n => n.At), Text: Format(f, t))))
            .OrderBy(b => b.Newest).Select(b => b.Text).ToList();
        var fitted = NewestFirstFit.Of(blocks, TicketConversationPromptSection.MaxChars, mayOvershoot: true);
        var note = fitted.Dropped == 0 ? string.Empty : $"\n\n[{fitted.Dropped} older review thread(s) omitted.]";
        return TicketPromptDelimiters.WrapSection("## Pull request review", string.Join("\n\n", fitted.Kept) + note);
    }

    private static string Format(PrReviewFeedback feedback, PrReviewThread thread)
    {
        var where = thread.File is null ? feedback.PrUrl : $"{thread.File}{(thread.Line is { } l ? $":{l}" : "")}";
        var state = thread.Resolved switch { false => "open thread", true => "resolved thread", null => "comment" };
        var notes = thread.Notes.Select(n =>
            $"[{n.At.ToString("u", CultureInfo.InvariantCulture)}] {n.Author?.AuthorLogin ?? "(deleted account)"}:\n{n.Body}");
        return $"### {where} ({state}, {feedback.Repo})\n" + string.Join("\n\n", notes);
    }
}
