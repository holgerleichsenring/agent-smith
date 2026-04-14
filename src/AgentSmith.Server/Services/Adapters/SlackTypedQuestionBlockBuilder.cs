using AgentSmith.Contracts.Dialogue;

namespace AgentSmith.Server.Services.Adapters;

/// <summary>
/// Builds Slack Block Kit payloads for typed dialogue questions.
/// Pure static builder — no state, no dependencies.
/// </summary>
internal static class SlackTypedQuestionBlockBuilder
{
    internal static object[] Build(DialogQuestion question)
    {
        return question.Type switch
        {
            QuestionType.Confirmation => BuildConfirmationBlocks(question),
            QuestionType.Choice => BuildChoiceBlocks(question),
            QuestionType.Approval => BuildApprovalBlocks(question),
            QuestionType.FreeText => BuildFreeTextBlocks(question),
            _ => BuildConfirmationBlocks(question)
        };
    }

    private static object[] BuildConfirmationBlocks(DialogQuestion question)
    {
        var blocks = new List<object>
        {
            new
            {
                type = "section",
                text = new { type = "mrkdwn", text = $":thought_balloon: *{question.Text}*" }
            }
        };

        AddContextBlock(blocks, question.Context);

        blocks.Add(new
        {
            type = "actions",
            block_id = question.QuestionId,
            elements = new object[]
            {
                new
                {
                    type = "button",
                    text = new { type = "plain_text", text = "Yes \u2705" },
                    style = "primary",
                    value = "yes",
                    action_id = $"{question.QuestionId}:yes"
                },
                new
                {
                    type = "button",
                    text = new { type = "plain_text", text = "No \u274c" },
                    style = "danger",
                    value = "no",
                    action_id = $"{question.QuestionId}:no"
                }
            }
        });

        return blocks.ToArray();
    }

    private static object[] BuildChoiceBlocks(DialogQuestion question)
    {
        var blocks = new List<object>
        {
            new
            {
                type = "section",
                text = new { type = "mrkdwn", text = $":thought_balloon: *{question.Text}*" }
            }
        };

        AddContextBlock(blocks, question.Context);

        var buttons = new List<object>();
        if (question.Choices is not null)
        {
            for (var i = 0; i < question.Choices.Count; i++)
            {
                buttons.Add(new
                {
                    type = "button",
                    text = new { type = "plain_text", text = question.Choices[i] },
                    value = question.Choices[i],
                    action_id = $"{question.QuestionId}:{i}"
                });
            }
        }

        blocks.Add(new
        {
            type = "actions",
            block_id = question.QuestionId,
            elements = buttons.ToArray()
        });

        return blocks.ToArray();
    }

    private static object[] BuildApprovalBlocks(DialogQuestion question)
    {
        var blocks = new List<object>
        {
            new
            {
                type = "section",
                text = new { type = "mrkdwn", text = $":clipboard: *{question.Text}*" }
            }
        };

        AddContextBlock(blocks, question.Context);

        blocks.Add(new
        {
            type = "actions",
            block_id = question.QuestionId,
            elements = new object[]
            {
                new
                {
                    type = "button",
                    text = new { type = "plain_text", text = "Approve \u2705" },
                    style = "primary",
                    value = "approve",
                    action_id = $"{question.QuestionId}:approve"
                },
                new
                {
                    type = "button",
                    text = new { type = "plain_text", text = "Reject \u274c" },
                    style = "danger",
                    value = "reject",
                    action_id = $"{question.QuestionId}:reject"
                }
            }
        });

        blocks.Add(new
        {
            type = "context",
            elements = new object[]
            {
                new { type = "mrkdwn", text = "_You can reply with an optional comment as the next message._" }
            }
        });

        return blocks.ToArray();
    }

    private static object[] BuildFreeTextBlocks(DialogQuestion question)
    {
        var blocks = new List<object>
        {
            new
            {
                type = "section",
                text = new { type = "mrkdwn", text = $":pencil: *{question.Text}*" }
            }
        };

        AddContextBlock(blocks, question.Context);

        blocks.Add(new
        {
            type = "context",
            elements = new object[]
            {
                new { type = "mrkdwn", text = "_Please type your answer as the next message in this channel._" }
            }
        });

        return blocks.ToArray();
    }

    private static void AddContextBlock(List<object> blocks, string? context)
    {
        if (string.IsNullOrWhiteSpace(context)) return;

        blocks.Add(new
        {
            type = "context",
            elements = new object[]
            {
                new { type = "mrkdwn", text = context }
            }
        });
    }
}
