using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-04-9e8c: a derivation's question on an inline-ticket run is asked in the terminal, and the answer is used
/// the only way a hand-back without specs can use it — by deriving again. The question and the answer join the
/// ticket thread as ours and the author's, which is where the derivation reads a conversation from, and the
/// derivation and its hand-back step run once more. A second derivation hand-back in the same run is not asked
/// again: it ends printing its question, as does a contradiction nobody answered.
/// </summary>
public sealed class InlineDerivationAsk(IDialogueAskGate askGate, ILogger<InlineDerivationAsk> logger)
{
    private const string AskedKey = "InlineDerivationAsked";
    private static readonly TimeSpan AskTimeout = TimeSpan.FromMinutes(10);

    public async Task<CommandResult> AskAsync(
        PipelineContext pipeline, SpecHandback handback, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(handback);
        if (pipeline.Flag(AskedKey)) return InlineHandbackEnd.Fail(handback);
        pipeline.Set(AskedKey, true);
        var outcome = await askGate.AskAsync(pipeline, Question(handback), cancellationToken);
        if (outcome.Checkpointed || outcome.Answer is null)
            return CommandResult.Fail("The derivation's question parked, and an inline run cannot be resumed");
        var answer = string.IsNullOrWhiteSpace(outcome.Answer.Answer)
            ? handback.TakenReading
            : outcome.Answer.Answer.Trim();
        if (string.IsNullOrWhiteSpace(answer)) return InlineHandbackEnd.Fail(handback);
        logger.LogInformation("The derivation's question was answered in the terminal: {Answer}", answer);
        Converse(pipeline, handback, answer, outcome.Answer.AnsweredBy);
        pipeline.Remove(ContextKeys.SpecHandback);
        return CommandResult.OkAndContinueWith($"Answered in the terminal: {answer} — deriving again",
            PipelineCommand.Simple(CommandNames.DeriveSpec), PipelineCommand.Simple(CommandNames.SpecHandback));
    }

    // The console prints the context first: the derivation's reason, then what to type.
    internal static DialogQuestion Question(SpecHandback handback) => handback.Readings.Count == 0
        ? new(Guid.NewGuid().ToString("N"), QuestionType.FreeText,
            "The ticket contradicts the repository. What should be built?", handback.Reason, null, null, AskTimeout)
        : new(Guid.NewGuid().ToString("N"), QuestionType.Choice, "Pick a reading, or type your own answer.",
            handback.Reason, [.. handback.Readings.Select(r => new DialogChoice(r))], handback.TakenReading,
            AskTimeout, handback.Taken);

    // The question as our own comment, then the answer as the author's, strictly later: the thread the next
    // derivation renders keeps an answered question of ours beside the answer that follows it.
    private static void Converse(PipelineContext pipeline, SpecHandback handback, string answer, string author)
    {
        var thread = pipeline.TryGet<IReadOnlyList<TicketComment>>(ContextKeys.TicketComments, out var c) && c is not null
            ? c : [];
        var asked = DateTimeOffset.UtcNow;
        pipeline.Set<IReadOnlyList<TicketComment>>(ContextKeys.TicketComments,
        [
            .. thread,
            new TicketComment("agent-smith", asked, SpecHandbackComment.Build(handback, null, string.Empty)),
            new TicketComment(author, asked.AddSeconds(1), answer),
        ]);
    }
}
