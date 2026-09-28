using AgentSmith.Contracts.Commands;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.ChatLaunch;

namespace AgentSmith.Server.Services.Handlers;

/// <summary>
/// Handles the FixTicketIntent: the named ticket's run goes through the server's spawn funnel
/// like every routed ticket, and the run is bound to the thread that asked. A chat fix that
/// names no pipeline runs <c>code</c>, the one pipeline that ships code.
/// </summary>
public sealed class FixTicketIntentHandler(
    ChatTicketRunLauncher launcher,
    ChatRunStart start)
{
    public Task HandleAsync(FixTicketIntent intent, CancellationToken cancellationToken)
    {
        var pipeline = string.IsNullOrWhiteSpace(intent.PipelineOverride)
            ? PipelinePresets.CodeName
            : intent.PipelineOverride;
        return start.StartAsync(
            ChatThread.Of(intent), $"{pipeline} for ticket *#{intent.TicketId}* in *{intent.Project}*",
            ct => launcher.LaunchAsync(intent.Project, intent.TicketId, pipeline, ct), cancellationToken);
    }
}
