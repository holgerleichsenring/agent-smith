using System.Text.Json;
using System.Text.Json.Nodes;
using AgentSmith.Server.Services;

namespace AgentSmith.Server.Extensions;

internal static class SlackPayloadExtractor
{
    internal static (string text, string userId, string channelId) ExtractEventFields(JsonNode json)
    {
        var eventNode = json["event"];
        var eventType = eventNode?["type"]?.GetValue<string>();

        if (eventType != "message" && eventType != "app_mention")
            return (string.Empty, string.Empty, string.Empty);

        if (!string.IsNullOrWhiteSpace(eventNode?["bot_id"]?.GetValue<string>()))
            return (string.Empty, string.Empty, string.Empty);

        var rawText = eventNode?["text"]?.GetValue<string>() ?? string.Empty;
        var text = StripMention(rawText);
        var userId = eventNode?["user"]?.GetValue<string>() ?? string.Empty;
        var channelId = eventNode?["channel"]?.GetValue<string>() ?? string.Empty;

        return (text, userId, channelId);
    }

    internal static (string channelId, string? questionId, string answer) ExtractInteractionFields(JsonNode json)
    {
        var channelId = json["channel"]?["id"]?.GetValue<string>() ?? string.Empty;
        var actionId = json["actions"]?[0]?["action_id"]?.GetValue<string>() ?? string.Empty;

        var separatorIndex = actionId.LastIndexOf(':');
        if (separatorIndex < 0) return (channelId, null, string.Empty);

        var questionId = actionId[..separatorIndex];
        var answer = actionId[(separatorIndex + 1)..];

        return (channelId, questionId, answer);
    }

    internal static string? ExtractSelectedProjectFromViewState(JsonNode json)
    {
        return json["view"]?["state"]?["values"]
            ?[DispatcherDefaults.SlackBlockProject]
            ?[DispatcherDefaults.SlackActionProject]
            ?["selected_option"]?["value"]?.GetValue<string>();
    }

    internal static string? ExtractSelectedProjectFromOptionsPayload(JsonNode json)
    {
        var metadata = GetMetadata(json);
        return metadata.SelectedProject;
    }

    internal static ModalMetadata GetMetadata(JsonNode json)
    {
        var raw = json["view"]?["private_metadata"]?.GetValue<string>() ?? "{}";
        return JsonSerializer.Deserialize<ModalMetadata>(raw) ?? new ModalMetadata();
    }

    internal static string SerializeMetadata(ModalMetadata metadata) =>
        JsonSerializer.Serialize(metadata);

    private static string StripMention(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith("<@", StringComparison.Ordinal)) return trimmed;

        var end = trimmed.IndexOf('>', 2);
        return end >= 0 ? trimmed[(end + 1)..].TrimStart() : trimmed;
    }
}
