using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0279: drives a scan master that barely read the source ONE more time, pushing a full
/// surface inventory and a per-area review.
/// <para>
/// 2026-09-01-7df4: the drive records itself in the conversation it drove, and what it
/// returns is the pass — not a replacement for the first one. The caller unions them, so a
/// finding the first pass made and the deeper pass did not repeat is no longer discarded.
/// A failed re-drive returns nothing at all: a shallow answer beats none.
/// </para>
/// </summary>
public sealed class ScanCoverageRedrive(
    IAgenticLoopRunner loopRunner,
    IScanMasterPromptFactory promptFactory,
    ILogger<ScanCoverageRedrive> logger)
{
    /// <summary>The deeper pass, or null when the read floor was met or the drive failed.</summary>
    public async Task<AgenticLoopResult?> DriveAsync(
        AgenticLoopRequest request, string userPrompt, MasterConversation conversation,
        Action<ChatResponse> trackUsage, int readCount, int readFloor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        ArgumentNullException.ThrowIfNull(trackUsage);
        if (readCount >= readFloor) return null;

        logger.LogWarning(
            "Scan master read only {Count} source file(s) (< floor {Floor}) — re-prompting once for deeper coverage",
            readCount, readFloor);
        var nudge = promptFactory.BuildCoverageNudge(userPrompt);
        try
        {
            var deeper = await loopRunner.RunAsync(
                request with { UserPrompt = nudge }, cancellationToken);
            conversation.Continued(nudge, deeper.Response);
            trackUsage(deeper.Response);
            return deeper;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Coverage re-drive failed — keeping the first pass's findings");
            return null;
        }
    }
}
