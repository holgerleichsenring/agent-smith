using AgentSmith.Contracts.Models.Configuration;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// p0177: SemaphoreSlim(MaxConcurrentSubAgents) + Task.WhenAll over the
/// SubAgentSpec list, each child one <see cref="SubAgentPass"/>.
/// Returns results in deterministic spec order regardless of completion
/// order — the merge index is the slot in the input list, not the time
/// of arrival. NO fail-fast: a failed child returns Status=Failed and the
/// siblings keep running.
/// <para>2026-11-18-c8e2a: every child joins the context's team; <see cref="Start"/> and <see cref="Continue"/> run
/// passes in the background, and each that ends — unless its master's loop stopped it — posts its notice to the team
/// and hands its result to the caller's <c>finished</c>.</para>
/// </summary>
public sealed class SubAgentRunner(
    SubAgentPass pass,
    LoopLimitsConfig loopLimits,
    ILogger<SubAgentRunner> logger) : ISubAgentRunner
{
    public async Task<IReadOnlyList<SubAgentResult>> RunAsync(
        IReadOnlyList<SubAgentSpec> specs,
        SubAgentContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(specs);
        ArgumentNullException.ThrowIfNull(context);
        if (specs.Count == 0) return Array.Empty<SubAgentResult>();

        using var throttle = new SemaphoreSlim(Math.Max(1, loopLimits.MaxConcurrentSubAgents));
        var slots = new SubAgentResult?[specs.Count];
        await Task.WhenAll(specs.Select((spec, index) => RunOneAsync(
            Enlist(spec, context), index, context, throttle, slots, cancellationToken)));

        var all = SubAgentAnswer.WithinCall(slots.Select(s => s!).ToArray(), out var cut); // 2026-11-10-85e2
        if (cut) logger.LogWarning("A spawn call's answers passed {Ceiling} characters together; the rest is cut", SubAgentAnswer.CallCeiling);
        return all;
    }

    public IReadOnlyList<SubAgentMember> Start(
        IReadOnlyList<SubAgentSpec> specs, SubAgentContext context,
        Func<SubAgentMember, SubAgentResult, Task> finished, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(specs);
        ArgumentNullException.ThrowIfNull(context);
        var throttle = new SemaphoreSlim(Math.Max(1, loopLimits.MaxConcurrentSubAgents));
        var members = specs.Select(spec => Enlist(spec, context)).ToList();
        foreach (var (member, index) in members.Select((m, i) => (m, i)))
        {
            member.Begin();
            member.Pass = Task.Run(() => BackgroundAsync(member, index, context, throttle, null, finished, cancellationToken));
        }
        return members;
    }

    /// <summary>The master's message is the child's next turn, after any word it ended before reading.</summary>
    public void Continue(
        SubAgentMember member, string message, SubAgentContext context,
        Func<SubAgentMember, SubAgentResult, Task> finished, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(member);
        if (!member.Begin()) throw new InvalidOperationException($"Sub-agent {member.Id} is still running.");
        var turn = string.Join("\n\n", [.. member.Inbox.Drain().Select(m => m.Text), message]);
        member.Pass = Task.Run(() => BackgroundAsync(member, 0, context, null, turn, finished, cancellationToken));
    }

    private static SubAgentMember Enlist(SubAgentSpec spec, SubAgentContext context) =>
        context.Team?.Enlist(spec) ?? SubAgentMember.For(spec);

    private async Task RunOneAsync(
        SubAgentMember member, int index, SubAgentContext context,
        SemaphoreSlim throttle, SubAgentResult?[] slots, CancellationToken ct)
    {
        await throttle.WaitAsync(ct);
        member.Begin();
        try
        {
            slots[index] = await pass.RunAsync(member, index, context, null, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Sub-agent slot {Index} threw outside the loop runner — recording as failed", index);
            slots[index] = pass.Failure(member, index, ex);
        }
        finally
        {
            member.End();
            throttle.Release();
        }
    }

    private async Task BackgroundAsync(
        SubAgentMember member, int index, SubAgentContext context, SemaphoreSlim? throttle, string? turn,
        Func<SubAgentMember, SubAgentResult, Task> finished, CancellationToken ct)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct, context.Team?.Stopping ?? CancellationToken.None);
        var result = await BackgroundPassAsync(member, index, context, throttle, turn, stop.Token, ct);
        member.End();
        if (result is null || result.Status == SubAgentStatus.Stopped) return;
        context.Team?.Notices.Post(SubAgentNotice.Of(member, result), SubAgentTeam.NoticeHeading);
        try
        {
            await finished(member, result);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Recording sub-agent {SubAgentId}'s end failed", member.Id);
        }
    }

    // Null when the whole run was cancelled: nobody is left to tell.
    private async Task<SubAgentResult?> BackgroundPassAsync(
        SubAgentMember member, int index, SubAgentContext context, SemaphoreSlim? throttle, string? turn,
        CancellationToken stop, CancellationToken ct)
    {
        var acquired = false;
        try
        {
            if (throttle is not null) { await throttle.WaitAsync(stop); acquired = true; }
            return await pass.RunAsync(member, index, context, turn, stop);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return await pass.StoppedAsync(member, index, context);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Sub-agent {SubAgentId} threw outside the loop runner — recording as failed", member.Id);
            return pass.Failure(member, index, ex);
        }
        finally
        {
            if (acquired) throttle!.Release();
        }
    }
}
