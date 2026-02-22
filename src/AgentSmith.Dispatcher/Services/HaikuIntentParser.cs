using AgentSmith.Dispatcher.Contracts;
using System.Text.Json;
using AgentSmith.Dispatcher.Models;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Dispatcher.Services;

/// <summary>
/// Uses Claude Haiku to classify free-form user input into a typed intent.
/// Only called when the regex stage fails to match. Near-zero cost per call.
/// </summary>
public sealed class HaikuIntentParser(
    AnthropicClient? client,
    ILogger<HaikuIntentParser> logger) : IHaikuIntentParser
{
    private const string HaikuModel = "claude-haiku-4-5-20251001";
    private const int MaxResponseTokens = 256;

    private const string SystemPrompt = """
        You are an intent classifier for a coding agent bot called Agent Smith.
        Extract the user's intent from their message and return ONLY a JSON object.

        Valid commandTypes: fix, list, create, help, unknown

        Rules:
        - "fix", "implement", "solve", "work on", "handle" -> commandType: "fix"
        - "list", "show", "what tickets", "open issues" -> commandType: "list"
        - "create", "add", "new ticket", "open a ticket" -> commandType: "create"
        - Greetings, capability questions -> commandType: "help"
        - Anything else -> commandType: "unknown"
        - Extract ticket numbers from #N or "ticket N" or "issue N"
        - Extract project names after "in" or "for" or "on"
        - If uncertain, set confidence: "low"

        Always respond with valid JSON only. No explanation. No markdown.
        Schema: { "commandType": string, "ticketNumber": string, "project": string, "title": string, "confidence": string }
        """;

    public async Task<ChatIntent?> ParseAsync(
        string text, string userId, string channelId, string platform,
        CancellationToken cancellationToken = default)
    {
        if (client is null) return null;

        try
        {
            var json = await CallHaikuAsync(text, cancellationToken);
            return ParseJsonResponse(json, text, userId, channelId, platform);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Haiku intent parsing failed for '{Text}'", text);
            return null;
        }
    }

    private async Task<string> CallHaikuAsync(string text, CancellationToken ct)
    {
        var response = await client!.Messages.GetClaudeMessageAsync(
            new MessageParameters
            {
                Model = HaikuModel,
                MaxTokens = MaxResponseTokens,
                System = [new SystemMessage(SystemPrompt)],
                Messages =
                [
                    new Message
                    {
                        Role = RoleType.User,
                        Content = [new TextContent { Text = text }]
                    }
                ],
                Stream = false
            }, ct);

        return response.Content.OfType<TextContent>().First().Text;
    }

    private ChatIntent? ParseJsonResponse(
        string json, string text, string userId, string channelId, string platform)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var commandType = GetStringProp(root, "commandType");
        var ticketNumber = GetStringProp(root, "ticketNumber");
        var project = GetStringProp(root, "project");
        var title = GetStringProp(root, "title");
        var confidence = GetStringProp(root, "confidence");

        var intent = BuildIntent(
            commandType, ticketNumber, project, title, text, userId, channelId, platform);

        if (confidence == "low" && intent is not null and not UnknownIntent)
        {
            var suggestion = FormatSuggestion(commandType, ticketNumber, project);
            return ClarificationNeeded.From(suggestion, text, userId, channelId, platform);
        }

        return intent;
    }

    private static ChatIntent? BuildIntent(
        string type, string ticket, string project, string title,
        string raw, string userId, string channelId, string platform)
    {
        return type switch
        {
            "fix" when int.TryParse(ticket, out var id) => new FixTicketIntent
            {
                RawText = raw, UserId = userId, ChannelId = channelId,
                Platform = platform, TicketId = id, Project = project
            },
            "list" => new ListTicketsIntent
            {
                RawText = raw, UserId = userId, ChannelId = channelId,
                Platform = platform, Project = project
            },
            "create" when !string.IsNullOrEmpty(title) => new CreateTicketIntent
            {
                RawText = raw, UserId = userId, ChannelId = channelId,
                Platform = platform, Title = title, Project = project
            },
            "help" => HelpIntent.From(raw, userId, channelId, platform),
            _ => null
        };
    }

    private static string FormatSuggestion(string type, string ticket, string project)
    {
        var suffix = string.IsNullOrEmpty(project) ? "" : $" in {project}";
        return type switch
        {
            "fix" => $"fix #{ticket}{suffix}",
            "list" => $"list tickets{suffix}",
            "create" => $"create a ticket{suffix}",
            _ => type
        };
    }

    private static string GetStringProp(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var prop) ? prop.GetString() ?? "" : "";
    }
}
