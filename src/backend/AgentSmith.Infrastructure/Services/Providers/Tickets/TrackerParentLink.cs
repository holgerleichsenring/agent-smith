using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// The one shape every provider's link-to-parent call shares: the tracker's refusal is a Failed
/// result carrying its reason, never an exception, because the tickets it would link already
/// exist and a throw would read as a failed filing. A timeout is a refusal too: only the
/// caller's own cancellation ends the attempt.
/// </summary>
internal sealed class TrackerParentLink(string tracker, ILogger logger)
{
    public async Task<ParentLinkResult> AttemptAsync(Func<Task> link, CancellationToken cancellationToken)
    {
        try
        {
            await link();
            return ParentLinkResult.Linked;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "{Tracker} refused to link a child ticket to its parent", tracker);
            return ParentLinkResult.Failed($"{tracker} refused the link: {ex.Message}");
        }
    }
}
