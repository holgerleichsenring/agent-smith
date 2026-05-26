namespace AgentSmith.Server.Services.Adapters;

/// <summary>
/// Builds Slack Block Kit payloads for standard messages
/// (clarification, info, question-answered).
/// Registered as Transient in DI.
/// </summary>
public sealed class SlackMessageBlockBuilder
{
    internal sealed record BlockPayload(string FallbackText, object[] Blocks);

    internal BlockPayload BuildClarification(string suggestion)
    {
        var text = $":thinking_face: Did you mean: *{suggestion}*?";
        var blocks = new object[]
        {
            new
            {
                type = "section",
                text = new { type = "mrkdwn", text }
            },
            new
            {
                type = "actions",
                block_id = "clarification",
                elements = new object[]
                {
                    new
                    {
                        type = "button",
                        text = new { type = "plain_text", text = "Yes, do it" },
                        style = "primary",
                        value = "confirm",
                        action_id = "clarification:confirm"
                    },
                    new
                    {
                        type = "button",
                        text = new { type = "plain_text", text = "Show help" },
                        value = "help",
                        action_id = "clarification:help"
                    }
                }
            }
        };
        return new BlockPayload(text, blocks);
    }

    internal BlockPayload BuildInfo(string title, string body)
    {
        var fallback = $":information_source: {title}: {body}";
        var blocks = new object[]
        {
            new
            {
                type = "section",
                text = new { type = "mrkdwn", text = $":information_source: *{title}*" }
            },
            new
            {
                type = "section",
                text = new { type = "mrkdwn", text = body }
            }
        };
        return new BlockPayload(fallback, blocks);
    }

    internal BlockPayload BuildQuestionAnswered(
        string questionText, string answer)
    {
        var emoji = answer.Equals("yes", StringComparison.OrdinalIgnoreCase)
            ? ":white_check_mark:"
            : ":x:";

        var mrkdwn = $":thought_balloon: *{questionText}*\n{emoji} Answered: *{answer}*";
        var blocks = new object[]
        {
            new
            {
                type = "section",
                text = new { type = "mrkdwn", text = mrkdwn }
            }
        };
        return new BlockPayload(mrkdwn, blocks);
    }
}
