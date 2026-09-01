using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0279 / 2026-09-01-7df4: drives a scan master that barely read the source one more time
/// for coverage, records the drive in the conversation it belongs to, and delivers the
/// UNION of both passes instead of the deeper pass alone.
/// <para>
/// The re-drive used to replace the first pass's result and never appear in the
/// conversation, so a first pass that found something the deeper pass did not mention lost
/// it. A failed re-drive still keeps the first pass: a shallow answer beats none.
/// </para>
/// </summary>
public sealed class ScanCoverageRedrive(
    IAgenticLoopRunner loopRunner,
    IScanMasterPromptFactory promptFactory,
    MasterAnswerUnion union,
    ITolerantJsonParser tolerantParser,
    ILogger<ScanCoverageRedrive> logger)
{
    public async Task<ScanRedriveOutcome> DriveAsync(
        PipelineContext pipeline, AgenticLoopRequest request, string userPrompt,
        AgenticLoopResult first, MasterConversation conversation,
        Action<ChatResponse> trackUsage, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(conversation);
        ArgumentNullException.ThrowIfNull(trackUsage);
        var nudge = promptFactory.BuildCoverageNudge(userPrompt);
        try
        {
            var deeper = await loopRunner.RunAsync(
                request with { UserPrompt = nudge }, cancellationToken);
            conversation.Continued(nudge, deeper.Response);
            trackUsage(deeper.Response);
            return new ScanRedriveOutcome(deeper, Union(pipeline, first, deeper));
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Coverage re-drive failed — keeping the first pass's findings");
            return new ScanRedriveOutcome(first, first.Response.Text ?? string.Empty);
        }
    }

    private string Union(PipelineContext pipeline, AgenticLoopResult first, AgenticLoopResult deeper)
    {
        string[] answers = [first.Response.Text ?? string.Empty, deeper.Response.Text ?? string.Empty];
        var combined = union.Combine(answers);
        if (combined is null) return answers[^1];
        // 2026-09-01-6c32 keeps its mark across the union: a pass whose array was cut off
        // mid-write contributed salvaged literals, and the repaired union must not hide it.
        if (!answers.All(tolerantParser.IsJsonArray))
            pipeline.Set(ContextKeys.ScanTriageRecovered,
                "a scan pass was cut off mid-array — its complete findings were recovered "
                + "into the union of the passes");
        logger.LogInformation(
            "Coverage re-drive delivered the union of both passes ({Chars} characters)",
            combined.Length);
        return combined;
    }
}
