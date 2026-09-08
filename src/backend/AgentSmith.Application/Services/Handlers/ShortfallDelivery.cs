using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0439: what a run that fell short of part of its contract delivers, and how it says so.
/// <para>
/// A run that stopped after a verified phase — a later phase's master died on the cost
/// budget, its verification was red, a safety mechanism cancelled it — has built something
/// sound. Today that ends as a failed run behind a draft nobody is pointed at. The keystone
/// stays: only phases verification recorded done are delivered, and each sandbox is brought
/// back to its verified head first; a sandbox that cannot be is a run that stays failed.
/// </para>
/// </summary>
public sealed class ShortfallDelivery(UnverifiedWorkReverter reverter, ILogger<ShortfallDelivery> logger)
{
    /// <summary>
    /// The shortfall this run delivers, with every sandbox's source restored to its verified
    /// head — or null: no verified phase, no open phase, or a sandbox whose unverified work
    /// could not be reverted.
    /// </summary>
    public async Task<RunShortfall?> PrepareAsync(
        PipelineContext pipeline, IReadOnlyList<(string Key, ISandbox Sandbox)> sandboxes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(sandboxes);
        var shortfall = RunShortfall.Of(pipeline);
        if (shortfall is null) return null;
        foreach (var (key, sandbox) in sandboxes)
        {
            if (await reverter.RestoreAsync(pipeline, key, sandbox, cancellationToken)) continue;
            logger.LogWarning(
                "{Key}: the tree cannot be brought back to its verified state — the run stays failed", key);
            return null;
        }
        logger.LogInformation("Delivering a shortfall: {Summary}", shortfall.Summary);
        return shortfall;
    }

    /// <summary>The banner a shortfall's pull request leads with — a delivery, and what it lacks.</summary>
    public string PullRequestBanner(RunShortfall shortfall)
    {
        ArgumentNullException.ThrowIfNull(shortfall);
        return $"> ✅ **Delivered with a shortfall** — {shortfall.Delivered.Count} of {shortfall.PhaseCount} "
            + "phase(s) built and verified; the rest is listed under \"Not delivered\" below.\n"
            + $"> The run stopped: {shortfall.Reason}\n\n";
    }

    /// <summary>The CommitAndPR step's own result on a shortfall run.</summary>
    public CommandResult StepResult(RunShortfall shortfall, IReadOnlyList<OpenedPullRequest> opened)
    {
        ArgumentNullException.ThrowIfNull(shortfall);
        ArgumentNullException.ThrowIfNull(opened);
        var urls = opened.Where(o => o.Status == OpenStatus.Opened && o.Url is not null).Select(o => o.Url!);
        return CommandResult.Ok($"{shortfall.Summary} — PR: {string.Join(", ", urls)}");
    }
}
