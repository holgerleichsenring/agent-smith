using AgentSmith.Dispatcher.Contracts;
using AgentSmith.Contracts.Services;
using AgentSmith.Dispatcher.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Dispatcher.Services;

/// <summary>
/// Three-stage intent engine: Regex (free) -> Haiku (cheap) -> Project Resolution (deterministic).
/// Replaces the simple ChatIntentParser for the Dispatcher's chat flow.
/// </summary>
public sealed class IntentEngine(
    ChatIntentParser regexParser,
    IHaikuIntentParser haikuParser,
    IProjectResolver projectResolver,
    IConfigurationLoader configLoader,
    ILogger<IntentEngine> logger)
{
    public async Task<ChatIntent> ParseAsync(
        string text, string userId, string channelId, string platform,
        CancellationToken cancellationToken = default)
    {
        var intent = regexParser.Parse(text, userId, channelId, platform);

        if (intent is not UnknownIntent)
            return await ResolveProjectIfNeededAsync(intent, cancellationToken);

        return await TryHaikuFallbackAsync(intent, text, userId, channelId, platform, cancellationToken);
    }

    private async Task<ChatIntent> TryHaikuFallbackAsync(
        ChatIntent fallback, string text, string userId, string channelId,
        string platform, CancellationToken ct)
    {
        var haikuIntent = await haikuParser.ParseAsync(text, userId, channelId, platform, ct);

        if (haikuIntent is not null and not UnknownIntent)
        {
            logger.LogInformation("Haiku classified '{Text}' as {IntentType}", text, haikuIntent.GetType().Name);
            return await ResolveProjectIfNeededAsync(haikuIntent, ct);
        }

        return fallback;
    }

    private async Task<ChatIntent> ResolveProjectIfNeededAsync(
        ChatIntent intent, CancellationToken ct)
    {
        return intent switch
        {
            FixTicketIntent { Project: "" } fix => await ResolveFixProjectAsync(fix, ct),
            ListTicketsIntent { Project: "" } list => ResolveListProject(list),
            _ => intent
        };
    }

    private async Task<ChatIntent> ResolveFixProjectAsync(FixTicketIntent fix, CancellationToken ct)
    {
        var result = await projectResolver.ResolveAsync(fix.TicketId.ToString(), ct);

        return result switch
        {
            ProjectResolved r => fix with { Project = r.ProjectName },
            ProjectAmbiguous a => BuildAmbiguousMessage(fix, a),
            _ => ErrorIntent.From(
                $"Ticket #{fix.TicketId} was not found in any configured project.",
                fix.RawText, fix.UserId, fix.ChannelId, fix.Platform)
        };
    }

    private ChatIntent ResolveListProject(ListTicketsIntent list)
    {
        var config = configLoader.LoadConfig(DispatcherDefaults.ConfigPath);

        if (config.Projects.Count == 1)
            return list with { Project = config.Projects.Keys.First() };

        return ErrorIntent.From(
            "Multiple projects configured. Please specify: `list tickets in <project>`",
            list.RawText, list.UserId, list.ChannelId, list.Platform);
    }

    private static ClarificationNeeded BuildAmbiguousMessage(
        FixTicketIntent fix, ProjectAmbiguous ambiguous)
    {
        var projects = string.Join(", ", ambiguous.Projects.Select(p => $"*{p}*"));
        var suggestion = $"Ticket #{fix.TicketId} found in {projects}. " +
                         $"Please specify: `fix #{fix.TicketId} in <project>`";

        return ClarificationNeeded.From(
            suggestion, fix.RawText, fix.UserId, fix.ChannelId, fix.Platform);
    }
}
