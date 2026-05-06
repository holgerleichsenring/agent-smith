using System.Text.Json;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services;

/// <summary>
/// Uses an LLM (via IChatClientFactory with the Reasoning task type) to classify
/// free-form user input into a typed intent. Only called when the regex stage fails to match.
/// </summary>
public sealed class LlmIntentParser(
    IChatClientFactory chatClientFactory,
    AgentConfig agent,
    ILogger<LlmIntentParser> logger) : ILlmIntentParser
{
    private const string SystemPrompt = """
        You are an intent classifier for an AI orchestration platform called Agent Smith.
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
        CancellationToken cancellationToken)
    {
        try
        {
            var chat = chatClientFactory.Create(agent, TaskType.Reasoning);
            var maxTokens = chatClientFactory.GetMaxOutputTokens(agent, TaskType.Reasoning);
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, SystemPrompt),
                new(ChatRole.User, text),
            };
            var response = await chat.GetResponseAsync(messages,
                new ChatOptions { MaxOutputTokens = maxTokens }, cancellationToken);
            return ParseJsonResponse(response.Text ?? string.Empty, text, userId, channelId, platform);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "LLM intent parsing failed for '{Text}'", text);
            return null;
        }
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
