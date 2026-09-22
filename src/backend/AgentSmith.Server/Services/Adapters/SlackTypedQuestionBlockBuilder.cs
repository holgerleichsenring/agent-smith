using AgentSmith.Contracts.Dialogue;

namespace AgentSmith.Server.Services.Adapters;

/// <summary>
/// Builds Slack Block Kit payloads for typed dialogue questions.
/// Registered as Transient in DI.
/// <para>
/// 2026-09-22-355b: an approval's action block carries the approve/reject pair and, beside it,
/// one button per offered shape. The label travels in the ACTION ID, because the tail of the
/// action id after the LAST colon is the only part of a clicked button this platform sends
/// back — the value is never read — which is why a shape label may never contain a colon.
/// The four block shapes were folded onto one section/context/actions/button vocabulary in the
/// same phase, so a file already over the length limit came back under it.
/// </para>
/// </summary>
public sealed class SlackTypedQuestionBlockBuilder
{
    internal object[] Build(DialogQuestion question) => question.Type switch
    {
        QuestionType.Choice => ChoiceBlocks(question),
        QuestionType.Approval => ApprovalBlocks(question),
        QuestionType.FreeText => FreeTextBlocks(question),
        _ => ConfirmationBlocks(question),
    };

    private static object[] ConfirmationBlocks(DialogQuestion question)
    {
        var blocks = Headed(question, ":thought_balloon:");
        blocks.Add(ActionsBlock(question,
        [
            Button(question, "Yes \u2705", "yes", "primary"),
            Button(question, "No \u274c", "no", "danger"),
        ]));
        return [.. blocks];
    }

    private static object[] ChoiceBlocks(DialogQuestion question)
    {
        var blocks = Headed(question, ":thought_balloon:");
        var buttons = new List<object>();
        for (var i = 0; i < (question.Choices?.Count ?? 0); i++)
        {
            // The choice branch answers by INDEX, not by label; 2026-09-22-355b touched only
            // the approval branch and left this exactly as it was.
            buttons.Add(new
            {
                type = "button",
                text = new { type = "plain_text", text = question.Choices![i] },
                value = question.Choices[i],
                action_id = $"{question.QuestionId}:{i}"
            });
        }

        blocks.Add(ActionsBlock(question, buttons));
        return [.. blocks];
    }

    private static object[] ApprovalBlocks(DialogQuestion question)
    {
        var blocks = Headed(question, ":clipboard:");
        var elements = new List<object>
        {
            Button(question, "Approve \u2705", "approve", "primary"),
            Button(question, "Reject \u274c", "reject", "danger"),
        };
        foreach (var shape in question.Choices ?? [])
            elements.Add(Button(question, shape.Label, shape.Label));

        blocks.Add(ActionsBlock(question, elements));
        // A button that quietly buys a whole design turn has to say so beside the one that files.
        blocks.Add(ContextBlock((question.Choices?.Count ?? 0) == 0
            ? "_You can reply with an optional comment as the next message._"
            : "_A shape above starts a new turn and files nothing. "
                + "You can reply with an optional comment as the next message._"));
        return [.. blocks];
    }

    private static object[] FreeTextBlocks(DialogQuestion question)
    {
        var blocks = Headed(question, ":pencil:");
        blocks.Add(ContextBlock("_Please type your answer as the next message in this channel._"));
        return [.. blocks];
    }

    /// <summary>The section stating the question, followed by its context block when it has one.</summary>
    private static List<object> Headed(DialogQuestion question, string icon)
    {
        var blocks = new List<object>
        {
            new { type = "section", text = new { type = "mrkdwn", text = $"{icon} *{question.Text}*" } },
        };
        if (!string.IsNullOrWhiteSpace(question.Context)) blocks.Add(ContextBlock(question.Context));
        return blocks;
    }

    private static object ContextBlock(string text) =>
        new { type = "context", elements = new object[] { new { type = "mrkdwn", text } } };

    private static object ActionsBlock(DialogQuestion question, IReadOnlyList<object> elements) =>
        new { type = "actions", block_id = question.QuestionId, elements = elements.ToArray() };

    /// <summary>One button. The ANSWER is what the action id carries, because that is what comes back.</summary>
    private static object Button(DialogQuestion question, string label, string answer, string? style = null)
    {
        var text = new { type = "plain_text", text = label };
        var actionId = $"{question.QuestionId}:{answer}";
        return style is null
            ? new { type = "button", text, value = answer, action_id = actionId }
            : new { type = "button", text, style, value = answer, action_id = actionId };
    }
}
