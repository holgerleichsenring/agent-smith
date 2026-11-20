using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services;

/// <summary>
/// 2026-11-20-24bd: gives a run's model place back on its way out of execution without letting a failed release
/// replace the run's own outcome — the housekeeping sweep, the terminal projection and the delete free a place left
/// behind. A composition without a place store releases nothing.
/// </summary>
public sealed class ModelPlaceRelease(ILogger<ModelPlaceRelease> logger, IModelRunSlots? slots = null)
{
    /// <summary>Releases <paramref name="runId"/>'s place; never throws.</summary>
    public async Task ReleaseAsync(string runId)
    {
        if (slots is null) return;
        try
        {
            await slots.ReleaseAsync(runId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Run {RunId}: its model place could not be released here; the sweep frees it", runId);
        }
    }
}
