using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-02-2b26d: what a hand-back comes to on an inline-ticket run, which has nobody's ticket to park on. A
/// standing review objection is asked in the terminal: its default reading builds the reviewed specs, another
/// reading ends the run naming it. A refusal, an undivided scope and a series out of step end as on a run without a
/// tracker. A derivation's question or contradiction goes to <see cref="InlineDerivationAsk"/> (2026-11-04-9e8c);
/// every other hand-back ends printing its question, because an answer it cannot use would report an empty run as done.
/// </summary>
public sealed class InlineHandbackAsk(
    IDialogueAskGate askGate, InlineDerivationAsk derivation, ILogger<InlineHandbackAsk> logger)
{
    private static readonly TimeSpan AskTimeout = TimeSpan.FromMinutes(10);

    public async Task<CommandResult> HandleAsync(
        PipelineContext pipeline, SpecHandback handback, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(handback);
        if (handback.Case is SpecHandbackCase.Refused or SpecHandbackCase.ScopeUnassigned
            or SpecHandbackCase.SeriesOutOfStep)
            return SpecHandbackWithoutTracker.Result(handback);
        if (handback.TakenReading is not null && handback.QuestionKind == SpecQuestionKind.ReviewObjection)
            return await AskObjectionAsync(pipeline, handback, cancellationToken);
        // 2026-11-04-9e8c: a derivation's question is asked and derived again with the answer.
        if (handback.Case is SpecHandbackCase.Question or SpecHandbackCase.RequirementsContradictRepository)
            return await derivation.AskAsync(pipeline, handback, cancellationToken);
        return InlineHandbackEnd.Fail(handback);
    }

    private async Task<CommandResult> AskObjectionAsync(
        PipelineContext pipeline, SpecHandback handback, CancellationToken cancellationToken)
    {
        var outcome = await askGate.AskAsync(pipeline, Question(handback), cancellationToken);
        if (outcome.Checkpointed || outcome.Answer is null)
            return CommandResult.Fail("The review objection's question parked, and an inline run cannot be resumed");
        var chosen = handback.Readings.FirstOrDefault(r =>
            string.Equals(r, outcome.Answer.Answer.Trim(), StringComparison.OrdinalIgnoreCase));
        if (chosen is not null && chosen != handback.TakenReading)
            return CommandResult.Fail($"The author chose \"{chosen}\" — put the change in the ticket text and run again");
        // 2026-11-10-8e59: a timeout answers "system" — nobody was there, and silence builds nothing unreviewed. A person
        // pressing Enter on the shown default (the recommended reading) has chosen it.
        var timedOut = outcome.Answer.AnsweredBy == "system";
        if (!timedOut && string.IsNullOrWhiteSpace(outcome.Answer.Answer)) chosen = handback.TakenReading;
        if (chosen is null || timedOut)
            return CommandResult.Fail("Nobody chose a reading for the review objection, so the spec is not built "
                + "unreviewed; run again to review it from fresh rounds, or answer the objection");
        logger.LogInformation("Review objection on an inline run: the author chose {Reading}", handback.TakenReading);
        pipeline.Remove(ContextKeys.SpecHandback);
        return CommandResult.Ok($"Review objection answered (the author chose it): {handback.TakenReading}");
    }

    internal static DialogQuestion Question(SpecHandback handback) => new(
        QuestionId: Guid.NewGuid().ToString("N"),
        Type: QuestionType.Choice,
        Text: "A spec reviewer still objects. Which reading should the run take?",
        Context: handback.Reason,
        Choices: [.. handback.Readings.Select(r => new DialogChoice(r))],
        DefaultAnswer: handback.TakenReading,
        Timeout: AskTimeout,
        RecommendedIndex: handback.Taken);
}
