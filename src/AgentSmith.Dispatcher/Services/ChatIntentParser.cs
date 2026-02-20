using System.Text.RegularExpressions;
using AgentSmith.Dispatcher.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Dispatcher.Services;

/// <summary>
/// Parses chat messages into typed ChatIntent instances using regex.
/// Zero AI cost. Project is optional — resolved later by IntentEngine.
/// </summary>
public sealed class ChatIntentParser(ILogger<ChatIntentParser> logger)
{
    private static readonly Regex HelpPattern = new(
        @"^(?:help|\?|what\s+can\s+you\s+do)\s*[?!.]?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex GreetingPattern = new(
        @"^(?:hi|hello|hey|hallo|howdy|good\s+(?:morning|afternoon|evening)|guten\s+tag)\s*[!.]?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex FixPattern = new(
        @"^(?:fix\s+)?(?:ticket\s+)?#(\d+)(?:\s+in\s+(\S+))?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ListPattern = new(
        @"^list(?:\s+tickets?)?(?:\s+(?:in|for)\s+(\S+))?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CreateWithDescPattern = new(
        @"^create\s+(?:ticket\s+)?[""'](.+?)[""']\s+in\s+(\S+)\s+[""'](.+?)[""']$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CreatePattern = new(
        @"^create\s+(?:ticket\s+)?[""'](.+?)[""']\s+in\s+(\S+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public ChatIntent Parse(string text, string userId, string channelId, string platform)
    {
        var trimmed = text.Trim();

        var intent = TryParseHelp(trimmed, userId, channelId, platform)
            ?? TryParseGreeting(trimmed, userId, channelId, platform)
            ?? TryParseCreateWithDesc(trimmed, userId, channelId, platform)
            ?? TryParseCreate(trimmed, userId, channelId, platform)
            ?? TryParseFix(trimmed, userId, channelId, platform)
            ?? TryParseList(trimmed, userId, channelId, platform)
            ?? (ChatIntent)UnknownIntent.From(trimmed, userId, channelId, platform);

        logger.LogInformation("Parsed intent {IntentType} from '{Text}'", intent.GetType().Name, trimmed);
        return intent;
    }

    private static HelpIntent? TryParseHelp(
        string text, string userId, string channelId, string platform)
    {
        return HelpPattern.IsMatch(text) ? HelpIntent.From(text, userId, channelId, platform) : null;
    }

    private static GreetingIntent? TryParseGreeting(
        string text, string userId, string channelId, string platform)
    {
        return GreetingPattern.IsMatch(text) ? GreetingIntent.From(text, userId, channelId, platform) : null;
    }

    private static FixTicketIntent? TryParseFix(
        string text, string userId, string channelId, string platform)
    {
        var match = FixPattern.Match(text);
        if (!match.Success) return null;

        return new FixTicketIntent
        {
            RawText = text, UserId = userId, ChannelId = channelId, Platform = platform,
            TicketId = int.Parse(match.Groups[1].Value),
            Project = match.Groups[2].Success ? match.Groups[2].Value : string.Empty
        };
    }

    private static ListTicketsIntent? TryParseList(
        string text, string userId, string channelId, string platform)
    {
        var match = ListPattern.Match(text);
        if (!match.Success) return null;

        return new ListTicketsIntent
        {
            RawText = text, UserId = userId, ChannelId = channelId, Platform = platform,
            Project = match.Groups[1].Success ? match.Groups[1].Value : string.Empty
        };
    }

    private static CreateTicketIntent? TryParseCreate(
        string text, string userId, string channelId, string platform)
    {
        var match = CreatePattern.Match(text);
        if (!match.Success) return null;

        return new CreateTicketIntent
        {
            RawText = text, UserId = userId, ChannelId = channelId, Platform = platform,
            Title = match.Groups[1].Value, Project = match.Groups[2].Value
        };
    }

    private static CreateTicketIntent? TryParseCreateWithDesc(
        string text, string userId, string channelId, string platform)
    {
        var match = CreateWithDescPattern.Match(text);
        if (!match.Success) return null;

        return new CreateTicketIntent
        {
            RawText = text, UserId = userId, ChannelId = channelId, Platform = platform,
            Title = match.Groups[1].Value, Project = match.Groups[2].Value,
            Description = match.Groups[3].Value
        };
    }
}
