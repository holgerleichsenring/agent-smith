using System.Text.Json.Nodes;

namespace AgentSmith.Server.Services.Adapters;

/// <summary>
/// Builds Adaptive Cards for status messages (progress, done, error, info, clarification, answered).
/// </summary>
public sealed class TeamsStatusCardBuilder
{
    public JsonObject BuildProgress(int step, int total, string commandName)
    {
        var bar = BuildProgressBar(step, total);
        var body = new JsonArray
        {
            AdaptiveCardPrimitives.TextBlock($"**[{step}/{total}]** {commandName}", "medium", "bolder"),
            AdaptiveCardPrimitives.TextBlock(bar, "small"),
        };
        return AdaptiveCardPrimitives.WrapCard(body);
    }

    public JsonObject BuildDone(string summary, string? prUrl)
    {
        var body = new JsonArray
        {
            AdaptiveCardPrimitives.TextBlock($"\u2705 **Done!** {summary}", "medium", "bolder"),
        };
        if (!string.IsNullOrWhiteSpace(prUrl))
            body.Add(AdaptiveCardPrimitives.TextBlock($"[\ud83d\udd17 View Pull Request]({prUrl})", "small"));
        return AdaptiveCardPrimitives.WrapCard(body);
    }

    public JsonObject BuildError(string friendlyError, string? logUrl)
    {
        var body = new JsonArray
        {
            AdaptiveCardPrimitives.TextBlock($"\u274c **Error:** {friendlyError}", "medium", "bolder", "attention"),
        };
        if (!string.IsNullOrWhiteSpace(logUrl))
            body.Add(AdaptiveCardPrimitives.TextBlock($"[\ud83d\udcdd View Logs]({logUrl})", "small"));
        return AdaptiveCardPrimitives.WrapCard(body);
    }

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

    private static string BuildProgressBar(int step, int total)
    {
        const int barLength = 10;
        var filled = (int)Math.Round((double)step / total * barLength);
        var empty = barLength - filled;
        return $"[{new string('\u2588', filled)}{new string('\u2591', empty)}] {step}/{total}";
    }
}
