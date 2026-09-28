using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Adapters;
using AgentSmith.Server.Services.ChatRuns;

namespace AgentSmith.Server.Services.ChatLaunch;

/// <summary>
/// Tells the thread what came of the run it asked for: the run id it is queued under, with the
/// dashboard link when the deployment names its dashboard, or why it waits, or why it was
/// refused. The reply goes to the thread on the platform the request came from.
/// </summary>
public sealed class ChatLaunchAnnouncer(ChatThreadAdapters adapters, ChatRunLink runLink)
{
    public Task AnnounceAsync(
        ChatThread thread, string subject, ChatLaunchResult result, CancellationToken ct) =>
        adapters.PostAsync(thread, Compose(subject, result), ct);

    private string Compose(string subject, ChatLaunchResult result)
    {
        if (result.RunId is null)
            return $":x: Could not start {subject}: {result.Refusal}";

        var head = result.WaitReason is null
            ? $":rocket: Queued {subject} as run `{result.RunId}`. Its questions and its outcome will be posted here."
            : $":hourglass: Queued {subject} as run `{result.RunId}` — waiting: {result.WaitReason}";
        return runLink.For(result.RunId) is { } link ? $"{head}\nFollow it at {link}" : head;
    }
}
