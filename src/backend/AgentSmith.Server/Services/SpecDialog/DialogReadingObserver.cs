using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// The observer one dashboard design turn sets: every report goes to the dialog it runs on.
/// </summary>
public sealed class DialogReadingObserver(DashboardReadingChannel channel, string dialogId)
    : ISourceScopeObserver
{
    public Task ReportAsync(
        string repoName, SourceScopeProgress progress, CancellationToken cancellationToken) =>
        channel.PushAsync(dialogId, repoName, progress, cancellationToken);
}
