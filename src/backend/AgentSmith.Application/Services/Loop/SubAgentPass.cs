using AgentSmith.Application.Services.Turns;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// p0177: one pass of a child through the agentic loop — its first (no turn) or, 2026-11-18-c8e2a, a continuation over
/// the conversation it kept, with its master's message as the next turn. Publishes the lifecycle events, rolls the cost
/// up to the run's tracker and maps the loop's answer to a <see cref="SubAgentResult"/>.
/// </summary>
public sealed class SubAgentPass(
    IAgenticLoopRunner loopRunner,
    SubAgentRequestBuilder requests,
    SubAgentEvents events,
    SubAgentActivityFrame activityFrame,
    ILogger<SubAgentPass> logger)
{
    public async Task<SubAgentResult> RunAsync(
        SubAgentMember member, int index, SubAgentContext context, string? turn, CancellationToken ct)
    {
        using var frame = activityFrame.Open(member.Name);
        AgenticLoopRequest request;
        if (turn is null || member.Request is null)
        {
            await events.SpawnedAsync(member.Spec, member.Id, context, requests.HashInheritedContext(member.Spec.InheritedContext), ct);
            request = member.Request = requests.Build(member, context);
        }
        else request = member.Request with { UserPrompt = turn, PriorMessages = member.Conversation.Thread() };

        try
        {
            var loopResult = await loopRunner.RunAsync(request, ct);
            if (turn is null) member.Conversation.Opened(request, loopResult.Response);
            else member.Conversation.Continued(turn, loopResult.Response);
            return await AnsweredAsync(member, index, context, loopResult.Response, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Sub-agent {SubAgentId} ({Name}) failed inside the loop runner", member.Id, member.Name);
            await events.CompletedAsync(member.Id, context, SubAgentStatus.Failed, 0, 0, 0m, ct);
            return Failure(member, index, ex);
        }
    }

    /// <summary>A child its master's loop stopped: said on the event stream, and a result nobody is told of.</summary>
    public async Task<SubAgentResult> StoppedAsync(SubAgentMember member, int index, SubAgentContext context)
    {
        logger.LogInformation("Sub-agent {SubAgentId} ({Name}) was stopped with its master's loop", member.Id, member.Name);
        await events.CompletedAsync(member.Id, context, SubAgentStatus.Stopped, 0, 0, 0m, CancellationToken.None);
        return Result(member, index, SubAgentStatus.Stopped, "stopped with its master's loop");
    }

    // p0341e: a one-line, operator-readable failure cause from the exception chain (the
    // innermost message is usually the real reason — e.g. the provider error, not the wrapper).
    public SubAgentResult Failure(SubAgentMember member, int index, Exception ex)
    {
        var inner = ex;
        while (inner.InnerException is not null) inner = inner.InnerException;
        var message = string.IsNullOrWhiteSpace(inner.Message) ? ex.Message : inner.Message;
        return Result(member, index, SubAgentStatus.Failed, $"{inner.GetType().Name}: {message}");
    }

    private async Task<SubAgentResult> AnsweredAsync(
        SubAgentMember member, int index, SubAgentContext context, ChatResponse response, CancellationToken ct)
    {
        if (SubAgentAnswer.IsRunaway(response.Text)) // 2026-11-10-14a3a: the one cut there is, logged
            logger.LogWarning("Sub-agent {SubAgentId} ({Name}) ran past {Ceiling} characters; its answer is cut",
                member.Id, member.Name, SubAgentAnswer.Ceiling);
        var status = string.IsNullOrWhiteSpace(response.Text) ? SubAgentStatus.Empty : SubAgentStatus.Succeeded;
        // Each child's cost rolls up against the shared per-run tracker; its own cost is estimated from its
        // response usage — race-free under concurrent children, unlike a before/after delta of the tracker.
        context.CostTracker.Track(response);
        var costUsd = context.CostTracker.EstimateResponseCostUsd(response);
        var toolCalls = response.Messages?.Sum(m => m.Contents?.OfType<FunctionCallContent>().Count() ?? 0) ?? 0;
        var observations = response.Messages?.Count(m => m.Role == ChatRole.Assistant) ?? 0;
        await events.CompletedAsync(member.Id, context, status, observations, toolCalls, costUsd, ct);
        return new SubAgentResult(index, status, member.Id, member.Name, toolCalls, costUsd, DateTimeOffset.UtcNow,
            Answer: SubAgentAnswer.Whole(response.Text)); // 2026-11-09-0620e, 14a3a: whole
    }

    private static SubAgentResult Result(SubAgentMember member, int index, SubAgentStatus status, string reason) =>
        new(index, status, member.Id, member.Name, 0, 0m, DateTimeOffset.UtcNow, FailureReason: reason);
}
