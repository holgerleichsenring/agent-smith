using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Expectations;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Expectations;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0328: negotiates the WHAT before planning. Drafts the schema-capped Soll
/// block (grounded in ticket + analysis), posts it to the ticket + dialogue
/// transports, and waits for ratification through the p0327 durable ask —
/// checkpoint/park past the hot window, resume at this very step with the
/// answer. The draft rides the pipeline context so a resumed run restores it
/// instead of re-drafting (LLM-free resume); the ratified expectation becomes
/// the run's acceptance contract. Headless runs auto-ratify with a visible
/// 'unratified' stamp — degradation, never a silent skip.
/// </summary>
public sealed class NegotiateExpectationHandler(
    IExpectationDrafter drafter,
    ExpectationQuestionBuilder questionBuilder,
    ExpectationRatifier ratifier,
    IExpectationTrackerCommenter commenter,
    ExpectationOutcomeRecorder recorder,
    IDialogueAskGate askGate,
    ILogger<NegotiateExpectationHandler> logger)
    : ICommandHandler<NegotiateExpectationContext>
{
    public async Task<CommandResult> ExecuteAsync(
        NegotiateExpectationContext context, CancellationToken cancellationToken)
    {
        if (context.Ticket is null)
            return CommandResult.Ok("Expectation negotiation skipped: run has no ticket");

        var draft = await ResolveDraftAsync(context, cancellationToken);
        if (draft is null)
            return CommandResult.Fail(
                "Expectation drafting failed: the model produced no schema-conforming draft "
                + "within the bounded retries");

        if (context.Pipeline.TryGet<bool>(ContextKeys.Headless, out var headless) && headless)
            return await AutoRatifyAsync(context, draft, cancellationToken);

        await PostCommentOnceAsync(context, draft, cancellationToken);
        var outcome = await askGate.AskAsync(
            context.Pipeline, questionBuilder.Build(draft, context.Pipeline), cancellationToken);
        if (outcome.Checkpointed)
            return CommandResult.Ok("Expectation ratification parked: waiting for the operator (checkpointed)");
        return await ApplyAnswerAsync(context, draft, outcome.Answer!, cancellationToken);
    }

    // The draft is published to the context BEFORE the ask: a checkpointed run
    // restores it on resume and must not call the model again.
    private async Task<ExpectationDraft?> ResolveDraftAsync(
        NegotiateExpectationContext context, CancellationToken cancellationToken)
    {
        if (context.Pipeline.TryGet<ExpectationDraft>(ContextKeys.ExpectationDraft, out var restored)
            && restored is not null)
            return restored;
        var (draft, error) = await drafter.DraftAsync(
            context.Ticket!, context.AgentConfig, context.Pipeline, cancellationToken);
        if (draft is null)
        {
            logger.LogError("Expectation drafting exhausted its retries: {Error}", error);
            return null;
        }
        context.Pipeline.Set(ContextKeys.ExpectationDraft, draft);
        return draft;
    }

    private async Task<CommandResult> AutoRatifyAsync(
        NegotiateExpectationContext context, ExpectationDraft draft, CancellationToken cancellationToken)
    {
        var unratified = new RatifiedExpectation(
            draft, ExpectationOutcomes.Unratified, "system", DateTimeOffset.UtcNow, EditDistance: 0);
        context.Pipeline.Set(ContextKeys.RunExpectation, unratified);
        await recorder.RecordAsync(context.Pipeline, draft, unratified, cancellationToken);
        logger.LogInformation("Headless run: expectation auto-ratified and stamped 'unratified'");
        return CommandResult.Ok("Expectation auto-ratified (headless) — stamped 'unratified'");
    }

    private async Task PostCommentOnceAsync(
        NegotiateExpectationContext context, ExpectationDraft draft, CancellationToken cancellationToken)
    {
        if (context.Tracker is null
            || (context.Pipeline.TryGet<bool>(ContextKeys.ExpectationCommentPosted, out var posted) && posted))
            return;
        context.Pipeline.Set(ContextKeys.ExpectationCommentPosted, true);
        await commenter.PostAsync(context.Tracker, context.Ticket!.Id, draft, cancellationToken);
    }

    private async Task<CommandResult> ApplyAnswerAsync(
        NegotiateExpectationContext context, ExpectationDraft draft,
        Contracts.Dialogue.DialogAnswer answer, CancellationToken cancellationToken)
    {
        var result = ratifier.Ratify(draft, answer);
        if (result.Error is not null)
            return CommandResult.Fail($"Expectation ratification failed: {result.Error}");
        await recorder.RecordAsync(context.Pipeline, draft, result.Expectation!, cancellationToken);
        if (result.IsRejected)
            return CommandResult.Fail(
                $"Expectation rejected by {answer.AnsweredBy}"
                + (string.IsNullOrWhiteSpace(result.RejectComment) ? "" : $": {result.RejectComment}"));
        context.Pipeline.Set(ContextKeys.RunExpectation, result.Expectation!);
        return CommandResult.Ok(
            $"Expectation ratified ({result.Expectation!.Outcome}) by {answer.AnsweredBy}");
    }
}
