using AgentSmith.Server.Models;
using AgentSmith.Server.Services.ChatLaunch;
using AgentSmith.Server.Services.Init;

namespace AgentSmith.Server.Services.Handlers;

/// <summary>
/// Handles the InitProjectIntent through the same launcher the dashboard's init button uses —
/// admitted, recorded and enqueued in the server — and tells the channel the run id. Pull
/// requests the run opens stay open: auto-accept is consent given on the click that starts the
/// run, and a chat command gives none.
/// </summary>
public sealed class InitProjectIntentHandler(
    InitRunLauncher launcher,
    ChatLaunchAnnouncer announcer)
{
    public async Task HandleAsync(InitProjectIntent intent, CancellationToken cancellationToken)
    {
        var launch = await launcher.LaunchAsync(intent.Project, false, cancellationToken);
        var result = launch.Outcome == InitLaunchOutcome.Started
            ? ChatLaunchResult.Started(launch.RunId!)
            : ChatLaunchResult.Refused(launch.Reason ?? launch.Outcome.ToString());
        await announcer.AnnounceAsync(
            intent.ChannelId, $"initialization of *{intent.Project}*", result, cancellationToken);
    }
}
