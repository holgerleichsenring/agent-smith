using System.Text.Json.Nodes;
using AgentSmith.Contracts.Dialogue;

namespace AgentSmith.Server.Services.Adapters;

/// <summary>
/// Builds Adaptive Card JSON for all 5 QuestionTypes used in Microsoft Teams.
/// Cards follow the Adaptive Card 1.4 schema.
/// </summary>
public static class TeamsCardBuilder
{
    private const string Schema = "http://adaptivecards.io/schemas/adaptive-card.json";
    private const string CardVersion = "1.4";

    public static JsonObject BuildQuestionCard(DialogQuestion question)
    {
        return question.Type switch
        {
            QuestionType.Confirmation => BuildConfirmationCard(question),
            QuestionType.Choice => BuildChoiceCard(question),
            QuestionType.Approval => BuildApprovalCard(question),
            QuestionType.FreeText => BuildFreeTextCard(question),
            QuestionType.Info => BuildInfoCard(question),
            _ => BuildConfirmationCard(question),
        };
    }

    public static JsonObject BuildProgressCard(int step, int total, string commandName)
    {
        var bar = BuildProgressBar(step, total);
        var body = new JsonArray
        {
            TextBlock($"**[{step}/{total}]** {commandName}", "medium", "bolder"),
            TextBlock(bar, "small"),
        };

        return WrapCard(body);
    }

    public static JsonObject BuildDoneCard(string summary, string? prUrl)
    {
        var body = new JsonArray
        {
            TextBlock($"\u2705 **Done!** {summary}", "medium", "bolder"),
        };

        if (!string.IsNullOrWhiteSpace(prUrl))
        {
            body.Add(TextBlock($"[\ud83d\udd17 View Pull Request]({prUrl})", "small"));
        }

        return WrapCard(body);
    }

    public static JsonObject BuildErrorCard(string friendlyError, string? logUrl)
    {
        var body = new JsonArray
        {
            TextBlock($"\u274c **Error:** {friendlyError}", "medium", "bolder", "attention"),
        };

        if (!string.IsNullOrWhiteSpace(logUrl))
        {
            body.Add(TextBlock($"[\ud83d\udcdd View Logs]({logUrl})", "small"));
        }

        return WrapCard(body);
    }

    public static JsonObject BuildInfoCard(string title, string text)
    {
        var body = new JsonArray
        {
            TextBlock($"\u2139\ufe0f **{title}**", "medium", "bolder"),
            TextBlock(text, "small"),
        };

        return WrapCard(body);
    }

    public static JsonObject BuildClarificationCard(string suggestion)
    {
        var body = new JsonArray
        {
            TextBlock($"\ud83e\udd14 Did you mean: **{suggestion}**?", "medium"),
        };

        var actions = new JsonArray
        {
            ActionSubmit("Yes, do it", new JsonObject
            {
                ["questionId"] = "clarification",
                ["answer"] = "confirm",
            }, "positive"),
            ActionSubmit("Show help", new JsonObject
            {
                ["questionId"] = "clarification",
                ["answer"] = "help",
            }),
        };

        return WrapCard(body, actions);
    }

    public static JsonObject BuildAnsweredCard(string questionText, string answer)
    {
        var emoji = answer.Equals("yes", StringComparison.OrdinalIgnoreCase)
                    || answer.Equals("approve", StringComparison.OrdinalIgnoreCase)
            ? "\u2705" : "\u274c";

        var body = new JsonArray
        {
            TextBlock($"\ud83d\udcad **{questionText}**", "medium"),
            TextBlock($"{emoji} Answered: **{answer}**", "small"),
        };

        return WrapCard(body);
    }

    // --- Private card builders per QuestionType ---

    private static JsonObject BuildConfirmationCard(DialogQuestion question)
    {
        var body = new JsonArray
        {
            TextBlock($"\ud83d\udcad **{question.Text}**", "medium"),
        };

        AddContext(body, question.Context);

        var actions = new JsonArray
        {
            ActionSubmit("Yes \u2705", new JsonObject
            {
                ["questionId"] = question.QuestionId,
                ["answer"] = "yes",
            }, "positive"),
            ActionSubmit("No \u274c", new JsonObject
            {
                ["questionId"] = question.QuestionId,
                ["answer"] = "no",
            }, "destructive"),
        };

        return WrapCard(body, actions);
    }

    private static JsonObject BuildChoiceCard(DialogQuestion question)
    {
        var body = new JsonArray
        {
            TextBlock($"\ud83d\udcad **{question.Text}**", "medium"),
        };

        AddContext(body, question.Context);

        var actions = new JsonArray();
        if (question.Choices is not null)
        {
            foreach (var choice in question.Choices)
            {
                actions.Add(ActionSubmit(choice, new JsonObject
                {
                    ["questionId"] = question.QuestionId,
                    ["answer"] = choice,
                }));
            }
        }

        return WrapCard(body, actions);
    }

    private static JsonObject BuildApprovalCard(DialogQuestion question)
    {
        var body = new JsonArray
        {
            TextBlock($"\ud83d\udccb **{question.Text}**", "medium"),
        };

        AddContext(body, question.Context);

        // Add comment input field
        body.Add(new JsonObject
        {
            ["type"] = "Input.Text",
            ["id"] = "comment",
            ["placeholder"] = "Optional comment...",
            ["isMultiline"] = true,
        });

        var actions = new JsonArray
        {
            ActionSubmit("Approve \u2705", new JsonObject
            {
                ["questionId"] = question.QuestionId,
                ["answer"] = "approve",
            }, "positive"),
            ActionSubmit("Reject \u274c", new JsonObject
            {
                ["questionId"] = question.QuestionId,
                ["answer"] = "reject",
            }, "destructive"),
        };

        return WrapCard(body, actions);
    }

    private static JsonObject BuildFreeTextCard(DialogQuestion question)
    {
        var body = new JsonArray
        {
            TextBlock($"\u270f\ufe0f **{question.Text}**", "medium"),
        };

        AddContext(body, question.Context);

        body.Add(new JsonObject
        {
            ["type"] = "Input.Text",
            ["id"] = "freetext",
            ["placeholder"] = "Type your answer...",
            ["isMultiline"] = true,
        });

        var actions = new JsonArray
        {
            ActionSubmit("Submit", new JsonObject
            {
                ["questionId"] = question.QuestionId,
                ["answer"] = "__freetext__",
            }),
        };

        return WrapCard(body, actions);
    }

    private static JsonObject BuildInfoCard(DialogQuestion question)
    {
        var body = new JsonArray
        {
            TextBlock($"\u2139\ufe0f **{question.Text}**", "medium", "bolder"),
        };

        if (!string.IsNullOrWhiteSpace(question.Context))
            body.Add(TextBlock(question.Context, "small"));

        var actions = new JsonArray
        {
            ActionSubmit("Acknowledge", new JsonObject
            {
                ["questionId"] = question.QuestionId,
                ["answer"] = "ack",
            }),
        };

        return WrapCard(body, actions);
    }

    // --- Helpers ---

    internal static JsonObject WrapCard(JsonArray body, JsonArray? actions = null)
    {
        var card = new JsonObject
        {
            ["$schema"] = Schema,
            ["type"] = "AdaptiveCard",
            ["version"] = CardVersion,
            ["body"] = body,
        };

        if (actions is { Count: > 0 })
            card["actions"] = actions;

        return card;
    }

    private static JsonObject TextBlock(string text, string size,
        string? weight = null, string? color = null)
    {
        var block = new JsonObject
        {
            ["type"] = "TextBlock",
            ["text"] = text,
            ["size"] = size,
            ["wrap"] = true,
        };

        if (weight is not null) block["weight"] = weight;
        if (color is not null) block["color"] = color;

        return block;
    }

    private static JsonObject ActionSubmit(string title, JsonObject data, string? style = null)
    {
        var action = new JsonObject
        {
            ["type"] = "Action.Submit",
            ["title"] = title,
            ["data"] = data,
        };

        if (style is not null) action["style"] = style;

        return action;
    }

    private static void AddContext(JsonArray body, string? context)
    {
        if (string.IsNullOrWhiteSpace(context)) return;
        body.Add(TextBlock(context, "small"));
    }

    private static string BuildProgressBar(int step, int total)
    {
        const int barLength = 10;
        var filled = (int)Math.Round((double)step / total * barLength);
        var empty = barLength - filled;
        return $"[{new string('\u2588', filled)}{new string('\u2591', empty)}] {step}/{total}";
    }
}
