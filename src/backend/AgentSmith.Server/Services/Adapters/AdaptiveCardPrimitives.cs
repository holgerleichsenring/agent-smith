using System.Text.Json.Nodes;

namespace AgentSmith.Server.Services.Adapters;

/// <summary>
/// Low-level Adaptive Card JSON builders (TextBlock, ActionSubmit, WrapCard).
/// Pure mapping — no state, no dependencies.
/// </summary>
internal static class AdaptiveCardPrimitives
{
    private const string Schema = "http://adaptivecards.io/schemas/adaptive-card.json";
    private const string CardVersion = "1.4";

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

    internal static JsonObject TextBlock(string text, string size,
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

    internal static JsonObject ActionSubmit(string title, JsonObject data, string? style = null)
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

    internal static void AddContext(JsonArray body, string? context)
    {
        if (string.IsNullOrWhiteSpace(context)) return;
        body.Add(TextBlock(context, "small"));
    }
}
