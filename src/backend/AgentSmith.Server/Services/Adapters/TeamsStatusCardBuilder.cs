using System.Text.Json.Nodes;

namespace AgentSmith.Server.Services.Adapters;

/// <summary>
/// Builds Adaptive Cards for status messages (info, clarification, answered).
/// </summary>
public sealed class TeamsStatusCardBuilder
{
    public JsonObject BuildInfo(string title, string text)
    {
        var body = new JsonArray
        {
            AdaptiveCardPrimitives.TextBlock($"\u2139\ufe0f **{title}**", "medium", "bolder"),
            AdaptiveCardPrimitives.TextBlock(text, "small"),
        };
        return AdaptiveCardPrimitives.WrapCard(body);
    }

    public JsonObject BuildClarification(string suggestion)
    {
        var body = new JsonArray
        {
            AdaptiveCardPrimitives.TextBlock($"\ud83e\udd14 Did you mean: **{suggestion}**?", "medium"),
        };
        var actions = new JsonArray
        {
            AdaptiveCardPrimitives.ActionSubmit("Yes, do it", new JsonObject
            {
                ["questionId"] = "clarification",
                ["answer"] = "confirm",
            }, "positive"),
            AdaptiveCardPrimitives.ActionSubmit("Show help", new JsonObject
            {
                ["questionId"] = "clarification",
                ["answer"] = "help",
            }),
        };
        return AdaptiveCardPrimitives.WrapCard(body, actions);
    }

    public JsonObject BuildAnswered(string questionText, string answer)
    {
        var emoji = answer.Equals("yes", StringComparison.OrdinalIgnoreCase)
                    || answer.Equals("approve", StringComparison.OrdinalIgnoreCase)
            ? "\u2705" : "\u274c";

        var body = new JsonArray
        {
            AdaptiveCardPrimitives.TextBlock($"\ud83d\udcad **{questionText}**", "medium"),
            AdaptiveCardPrimitives.TextBlock($"{emoji} Answered: **{answer}**", "small"),
        };
        return AdaptiveCardPrimitives.WrapCard(body);
    }
}
