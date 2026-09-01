using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-09-01-0e80: puts the scanners' raw output to the master AFTER it has said what it
/// found on its own, and asks it to reconcile — which facts it already covered, which it
/// now judges real, which it dismisses and why.
/// <para>
/// It runs on the SAME conversation, so the reads of the first pass are still in view and
/// the turn is a reconciliation rather than a second search. Null from the prompt factory
/// means there is nothing to reconcile separately: an api scan's master inputs ARE the
/// scanner reports, and a repository scan whose scanners found nothing has no list.
/// </para>
/// </summary>
public sealed class ScanReconciliationDrive(
    IAgenticLoopRunner loopRunner,
    IScanMasterPromptFactory promptFactory,
    ILogger<ScanReconciliationDrive> logger)
{
    /// <summary>The reconciliation pass, or null when there was nothing to reconcile.</summary>
    public async Task<AgenticLoopResult?> DriveAsync(
        PipelineContext pipeline, AgenticLoopRequest request, MasterConversation conversation,
        Action<ChatResponse> trackUsage, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        ArgumentNullException.ThrowIfNull(trackUsage);
        var prompt = promptFactory.BuildReconciliation(pipeline);
        if (prompt is null) return null;

        try
        {
            var reconciled = await loopRunner.RunAsync(
                request with { UserPrompt = prompt, PriorMessages = conversation.Thread() },
                cancellationToken);
            conversation.Continued(prompt, reconciled.Response);
            trackUsage(reconciled.Response);
            logger.LogInformation("Scan master reconciled its review against the scanner output");
            return reconciled;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Reconciliation turn failed — keeping the unanchored findings");
            return null;
        }
    }
}
