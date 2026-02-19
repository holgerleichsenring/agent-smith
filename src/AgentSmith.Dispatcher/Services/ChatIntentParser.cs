using System.Text.RegularExpressions;
using AgentSmith.Dispatcher.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Dispatcher.Services;

/// <summary>
/// Parses natural language chat messages into strongly-typed ChatIntent instances.
/// Uses regex matching - no LLM call required.
/// Supported patterns:
///   fix #65 in todo-list
///   list tickets in todo-list
///   create ticket "Add logging" in todo-list
/// </summary>
public sealed class ChatIntentParser(ILogger<ChatIntentParser> logger)
{
    private static readonly Regex FixPattern = new(
        @"^fix\s+#(\d+)\s+in\s+(\S+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ListPattern = new(
        @"^list\s+tickets?\s+(?:in|for)\s+(\S+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CreatePattern = new(
        @"^create\s+ticket\s+[""'](.+?)[""']\s+in\s+(\S+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CreateWithDescPattern = new(
        @"^create\s+ticket\s+[""'](.+?)[""']\s+in\s+(\S+)\s+[""'](.+?)[""']$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public ChatIntent Parse(string text, string userId, string channelId, string platform)
    {
        var trimmed = text.Trim();

        var intent = TryParseCreateWithDesc(trimmed, userId, channelId, platform)
            ?? TryParseCreate(trimmed, userId, channelId, platform)
            ?? TryParseFix(trimmed, userId, channelId, platform)
            ?? TryParseList(trimmed, userId, channelId, platform)
            ?? (ChatIntent)UnknownIntent.From(trimmed, userId, channelId, platform);

        logger.LogInformation(
            "Parsed intent {IntentType} from '{Text}' (user={UserId}, channel={ChannelId}, platform={Platform})",
            intent.GetType().Name, trimmed, userId, channelId, platform);

        return intent;
    }

    private static FixTicketIntent? TryParseFix(
        string text, string userId, string channelId, string platform)
    {
        var match = FixPattern.Match(text);
        if (!match.Success) return null;

        return new FixTicketIntent
        {
            RawText = text,
            UserId = userId,
            ChannelId = channelId,
            Platform = platform,
            TicketId = int.Parse(match.Groups[1].Value),
            Project = match.Groups[2].Value
        };
    }

    private static ListTicketsIntent? TryParseList(
        string text, string userId, string channelId, string platform)
    {
        var match = ListPattern.Match(text);
        if (!match.Success) return null;

        return new ListTicketsIntent
        {
            RawText = text,
            UserId = userId,
            ChannelId = channelId,
            Platform = platform,
            Project = match.Groups[1].Value
        };
    }

    private static CreateTicketIntent? TryParseCreate(
        string text, string userId, string channelId, string platform)
    {
        var match = CreatePattern.Match(text);
        if (!match.Success) return null;

        return new CreateTicketIntent
        {
            RawText = text,
            UserId = userId,
            ChannelId = channelId,
            Platform = platform,
            Title = match.Groups[1].Value,
            Project = match.Groups[2].Value
        };
    }

    private static CreateTicketIntent? TryParseCreateWithDesc(
        string text, string userId, string channelId, string platform)
    {
        var match = CreateWithDescPattern.Match(text);
        if (!match.Success) return null;

        return new CreateTicketIntent
        {
            RawText = text,
            UserId = userId,
            ChannelId = channelId,
            Platform = platform,
            Title = match.Groups[1].Value,
            Project = match.Groups[2].Value,
            Description = match.Groups[3].Value
        };
    }
}
