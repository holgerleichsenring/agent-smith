using AgentSmith.Application.Services.Dialogue;
using AgentSmith.Server.Contracts;

namespace AgentSmith.Server.Services.ChatLaunch;

/// <summary>
/// Tells the channel what came of the run it asked for: the run id it is queued under, with
/// the dashboard link when the deployment names its dashboard, or why it waits, or why it was
/// refused. Following the run afterwards is not this class's business.
/// </summary>
public sealed class ChatLaunchAnnouncer(IPlatformAdapter adapter, RunAnswerLink runLink)
{
    public Task AnnounceAsync(
        string channelId, string subject, ChatLaunchResult result, CancellationToken ct) =>
        adapter.SendMessageAsync(channelId, Compose(subject, result), ct);

    private string Compose(string subject, ChatLaunchResult result)
    {
        if (result.RunId is null)
            return $":x: Could not start {subject}: {result.Refusal}";

        var head = result.WaitReason is null
            ? $":rocket: Queued {subject} as run `{result.RunId}`."
            : $":hourglass: Queued {subject} as run `{result.RunId}` — waiting: {result.WaitReason}";
        return runLink.For(result.RunId) is { } link ? $"{head}\nFollow it at {link}" : head;
    }
}
