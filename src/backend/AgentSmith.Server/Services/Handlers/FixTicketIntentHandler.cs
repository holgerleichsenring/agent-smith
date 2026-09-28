using AgentSmith.Contracts.Commands;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.ChatLaunch;

namespace AgentSmith.Server.Services.Handlers;

/// <summary>
/// Handles the FixTicketIntent: the named ticket's run goes through the server's spawn funnel
/// like every routed ticket, and the channel is told the run id it is queued under. A chat fix
/// that names no pipeline runs <c>code</c>, the one pipeline that ships code.
/// </summary>
public sealed class FixTicketIntentHandler(
    ChatTicketRunLauncher launcher,
    ChatLaunchAnnouncer announcer)
{
    public async Task HandleAsync(FixTicketIntent intent, CancellationToken cancellationToken)
    {
        var pipeline = string.IsNullOrWhiteSpace(intent.PipelineOverride)
            ? PipelinePresets.CodeName
            : intent.PipelineOverride;
        var result = await launcher.LaunchAsync(intent.Project, intent.TicketId, pipeline, cancellationToken);
        await announcer.AnnounceAsync(
            intent.ChannelId, $"{pipeline} for ticket *#{intent.TicketId}* in *{intent.Project}*",
            result, cancellationToken);
    }
}
