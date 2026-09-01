using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// The passes a scan master's run is made of, in order, and the one answer they deliver:
/// the unanchored first look, the coverage re-drive when the review was shallow, and the
/// reconciliation against the scanners' output. Every pass ADDS to the answer.
/// </summary>
public sealed class ScanMasterPasses(
    ScanCoverageRedrive coverage,
    ScanReconciliationDrive reconciliation,
    MasterAnswerUnion union,
    ITolerantJsonParser tolerantParser,
    ILogger<ScanMasterPasses> logger)
{
    public const string Unanchored = "unanchored";
    public const string Coverage = "coverage";
    public const string Reconciliation = "reconciliation";

    public async Task<ScanPassesOutcome> DriveAsync(
        PipelineContext pipeline, AgenticLoopRequest request, string userPrompt,
        AgenticLoopResult first, MasterConversation conversation,
        int readCount, int readFloor, Action<ChatResponse> trackUsage,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(first);
        var passes = new List<MasterPassAnswer> { new(Unanchored, Text(first)) };
        var last = first;

        var deeper = await coverage.DriveAsync(
            request, userPrompt, conversation, trackUsage, readCount, readFloor, cancellationToken);
        if (deeper is not null) { passes.Add(new MasterPassAnswer(Coverage, Text(deeper))); last = deeper; }

        var reconciled = await reconciliation.DriveAsync(
            pipeline, request, conversation, trackUsage, cancellationToken);
        if (reconciled is not null)
        {
            passes.Add(new MasterPassAnswer(Reconciliation, Text(reconciled)));
            last = reconciled;
        }
        return new ScanPassesOutcome(last, Deliver(pipeline, passes));
    }

    /// <summary>
    /// The union of every pass. A single-pass run publishes its own text byte for byte, so
    /// a scan that needed no follow-up reads exactly as it did before.
    /// </summary>
    private string Deliver(PipelineContext pipeline, IReadOnlyList<MasterPassAnswer> passes)
    {
        var combined = union.Combine(passes);
        if (combined.Origins.Count > 0)
            pipeline.Set(ContextKeys.ScanFindingOrigins, combined.Origins);
        if (passes.Count == 1 || combined.Answer is null) return passes[^1].Answer;

        // 2026-09-01-6c32 keeps its mark across the union: a pass cut off mid-array
        // contributed salvaged literals, and the repaired union must not hide that.
        if (!passes.All(pass => tolerantParser.IsJsonArray(pass.Answer)))
            pipeline.Set(ContextKeys.ScanTriageRecovered,
                "a scan pass was cut off mid-array — its complete findings were recovered "
                + "into the union of the passes");
        logger.LogInformation(
            "Scan delivered the union of {Passes} pass(es): {Breakdown}",
            passes.Count, Breakdown(combined.Origins));
        return combined.Answer;
    }

    private static string Breakdown(IReadOnlyDictionary<string, string> origins) =>
        string.Join(", ", origins
            .GroupBy(entry => entry.Value, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"{group.Key}={group.Count()}"));

    private static string Text(AgenticLoopResult result) => result.Response.Text ?? string.Empty;
}
