using System.Text.Json.Nodes;
using AgentSmith.Contracts.Dialogue;

namespace AgentSmith.Server.Services.Adapters;

/// <summary>
/// Builds Adaptive Cards for typed dialogue questions (Confirmation, Choice, Approval, FreeText, Info).
/// </summary>
public sealed class TeamsQuestionCardBuilder
{
    public JsonObject Build(DialogQuestion question)
    {
        return question.Type switch
        {
            QuestionType.Confirmation => BuildConfirmation(question),
            QuestionType.Choice => BuildChoice(question),
            QuestionType.Approval => BuildApproval(question),
            QuestionType.FreeText => BuildFreeText(question),
            QuestionType.Info => BuildInfo(question),
            _ => BuildConfirmation(question),
        };
    }

    private static JsonObject BuildConfirmation(DialogQuestion q)
    {
        var body = new JsonArray { AdaptiveCardPrimitives.TextBlock($"\ud83d\udcad **{q.Text}**", "medium") };
        AdaptiveCardPrimitives.AddContext(body, q.Context);

        var actions = new JsonArray
        {
            AdaptiveCardPrimitives.ActionSubmit("Yes \u2705", QuestionData(q, "yes"), "positive"),
            AdaptiveCardPrimitives.ActionSubmit("No \u274c", QuestionData(q, "no"), "destructive"),
        };

        return AdaptiveCardPrimitives.WrapCard(body, actions);
    }

    private static JsonObject BuildChoice(DialogQuestion q)
    {
        var body = new JsonArray { AdaptiveCardPrimitives.TextBlock($"\ud83d\udcad **{q.Text}**", "medium") };
        AdaptiveCardPrimitives.AddContext(body, q.Context);

        var actions = new JsonArray();
        if (q.Choices is not null)
        {
            foreach (var choice in q.Choices)
                actions.Add(AdaptiveCardPrimitives.ActionSubmit(choice, QuestionData(q, choice)));
        }

        return AdaptiveCardPrimitives.WrapCard(body, actions);
    }

    private static JsonObject BuildApproval(DialogQuestion q)
    {
        var body = new JsonArray { AdaptiveCardPrimitives.TextBlock($"\ud83d\udccb **{q.Text}**", "medium") };
        AdaptiveCardPrimitives.AddContext(body, q.Context);

        body.Add(new JsonObject
        {
            ["type"] = "Input.Text",
            ["id"] = "comment",
            ["placeholder"] = "Optional comment...",
            ["isMultiline"] = true,
        });

        var actions = new JsonArray
        {
            AdaptiveCardPrimitives.ActionSubmit("Approve \u2705", QuestionData(q, "approve"), "positive"),
            AdaptiveCardPrimitives.ActionSubmit("Reject \u274c", QuestionData(q, "reject"), "destructive"),
        };

        return AdaptiveCardPrimitives.WrapCard(body, actions);
    }

    private static JsonObject BuildFreeText(DialogQuestion q)
    {
        var body = new JsonArray { AdaptiveCardPrimitives.TextBlock($"\u270f\ufe0f **{q.Text}**", "medium") };
        AdaptiveCardPrimitives.AddContext(body, q.Context);

        body.Add(new JsonObject
        {
            ["type"] = "Input.Text",
            ["id"] = "freetext",
            ["placeholder"] = "Type your answer...",
            ["isMultiline"] = true,
        });

        var actions = new JsonArray
        {
            AdaptiveCardPrimitives.ActionSubmit("Submit", QuestionData(q, "__freetext__")),
        };

        return AdaptiveCardPrimitives.WrapCard(body, actions);
    }

    private static JsonObject BuildInfo(DialogQuestion q)
    {
        var body = new JsonArray { AdaptiveCardPrimitives.TextBlock($"\u2139\ufe0f **{q.Text}**", "medium", "bolder") };
        if (!string.IsNullOrWhiteSpace(q.Context))
            body.Add(AdaptiveCardPrimitives.TextBlock(q.Context, "small"));

        var actions = new JsonArray
        {
            AdaptiveCardPrimitives.ActionSubmit("Acknowledge", QuestionData(q, "ack")),
        };

        return AdaptiveCardPrimitives.WrapCard(body, actions);
    }

    private static JsonObject QuestionData(DialogQuestion q, string answer) => new()
    {
        ["questionId"] = q.QuestionId,
        ["answer"] = answer,
    };
}
