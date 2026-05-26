using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Handlers;

/// <summary>
/// Creates typed intent objects from Slack modal submission values.
/// </summary>
internal static class ModalIntentFactory
{
    public static FixTicketIntent CreateFixIntent(
        int ticketId, ModalCommandType command, string project,
        string userId, string channelId) => new()
    {
        TicketId = ticketId,
        Project = project,
        PipelineOverride = ResolvePipeline(command),
        RawText = $"/fix #{ticketId} in {project}",
        UserId = userId,
        ChannelId = channelId,
        Platform = DispatcherDefaults.PlatformSlack
    };

    public static FixTicketIntent CreatePipelineIntent(
        string pipeline, string project, string userId, string channelId) => new()
    {
        TicketId = 0,
        Project = project,
        PipelineOverride = pipeline,
        RawText = $"/{pipeline} in {project}",
        UserId = userId,
        ChannelId = channelId,
        Platform = DispatcherDefaults.PlatformSlack
    };

    public static ListTicketsIntent CreateListIntent(
        string project, string userId, string channelId) => new()
    {
        Project = project,
        RawText = $"/agentsmith list tickets in {project}",
        UserId = userId,
        ChannelId = channelId,
        Platform = DispatcherDefaults.PlatformSlack
    };

    public static CreateTicketIntent CreateCreateIntent(
        string project, string title, string? description,
        string userId, string channelId) => new()
    {
        Project = project,
        Title = title,
        Description = description,
        RawText = $"/agentsmith create ticket \"{title}\" in {project}",
        UserId = userId,
        ChannelId = channelId,
        Platform = DispatcherDefaults.PlatformSlack
    };

    public static InitProjectIntent CreateInitIntent(
        string project, string userId, string channelId) => new()
    {
        Project = project,
        RawText = $"/agentsmith init {project}",
        UserId = userId,
        ChannelId = channelId,
        Platform = DispatcherDefaults.PlatformSlack
    };

    private static string ResolvePipeline(ModalCommandType command) => command switch
    {
        ModalCommandType.FixBug => "fix-bug",
        ModalCommandType.FixBugNoTests => "fix-no-test",
        ModalCommandType.AddFeature => "add-feature",
        ModalCommandType.MadDiscussion => "mad-discussion",
        _ => "fix-bug"
    };
}
