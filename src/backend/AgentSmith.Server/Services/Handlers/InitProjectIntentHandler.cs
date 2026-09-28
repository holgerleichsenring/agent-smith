using AgentSmith.Server.Models;
using AgentSmith.Server.Services.ChatLaunch;
using AgentSmith.Server.Services.Init;

namespace AgentSmith.Server.Services.Handlers;

/// <summary>
/// Handles the InitProjectIntent through the same launcher the dashboard's init button uses —
/// admitted, recorded and enqueued in the server — and binds the run to the thread that asked.
/// Pull requests the run opens stay open: auto-accept is consent given on the click that starts
/// the run, and a chat command gives none.
/// </summary>
public sealed class InitProjectIntentHandler(
    InitRunLauncher launcher,
    ChatRunStart start)
{
    public Task HandleAsync(InitProjectIntent intent, CancellationToken cancellationToken) =>
        start.StartAsync(
            ChatThread.Of(intent), $"initialization of *{intent.Project}*",
            ct => LaunchAsync(intent, ct), cancellationToken);

    private async Task<ChatLaunchResult> LaunchAsync(InitProjectIntent intent, CancellationToken ct)
    {
        var launch = await launcher.LaunchAsync(intent.Project, false, ct);
        return launch.Outcome == InitLaunchOutcome.Started
            ? ChatLaunchResult.Started(launch.RunId!)
            : ChatLaunchResult.Refused(launch.Reason ?? launch.Outcome.ToString());
    }
}
