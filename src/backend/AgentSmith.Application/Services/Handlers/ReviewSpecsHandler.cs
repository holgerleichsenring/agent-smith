using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-10-13-8762g: every unbuilt spec, reviewed in order through
/// <see cref="SpecReviewLoop"/> on settings read once, live. The share never stops the first reviewed spec; once
/// spent, the first spec not cleared is the <see cref="ContextKeys.SpecSequenceStop"/> — left, with every spec
/// after it, to the next attempt. 8762h: a REFUSE standing at the end is asked once (<see cref="ReviewExhaustion"/>).
/// 8762k: then ONE series round over the cleared prefix (<see cref="SeriesContractRound"/>), whose standing REFUSE
/// joins the spec objections in the same decision, so one question lists both.
/// 2026-11-10-8e59: a spec is spared only when a review ended clean on its exact text (<see cref="ReviewedText"/>);
/// approved but never reviewed, rewritten after its review, or interrupted, it is attacked before it is built.
/// </summary>
public sealed class ReviewSpecsHandler(
    ISpecReviewSettingsReader settings, ISeriesSettingsReader attempt, ApprovedSpecSetResolver approvals, SpecReviewLoop loop,
    ReviewExhaustion exhaustion, ReviewObjectionPark park, SeriesContractRound series, ILogger<ReviewSpecsHandler> logger) : ICommandHandler<ReviewSpecsContext>
{
    public async Task<CommandResult> ExecuteAsync(ReviewSpecsContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var (current, failure) = Read();
        if (current is null) return CommandResult.Fail(failure!);
        var set = context.Pipeline.TryGet<SpecSet>(ContextKeys.SpecSet, out var s) ? s : null;
        var approved = approvals.Published(context.Pipeline);
        // 2026-10-13-8762u: only what this attempt can build — never a spec its chunk defers.
        var tail = set?.UnexecutedTail.Take(attempt.Current().Series.SpecsPerAttempt).Where(spec => !ReviewedText.Covers(approved?.ReviewedClean, spec.SpecId, spec.Draft.Yaml)
            && !ReviewStepOutcomes.AnsweredBuild(context.Pipeline, spec)).ToList() ?? [];
        // 2026-11-10-8e59: the series contract is attacked too, unless a review ended clean on exactly this contract.
        var contractClean = set?.Contract is not { IsEmpty: false } contract || ReviewedText.Covers(approved?.ReviewedClean, contract);
        if (Skip(current, set, tail, contractClean) is { } skip) return Done($"spec review skipped: {skip}");
        var run = new SpecReviewRun(context.Pipeline, current, context.AgentConfig, SpecReviewWiderGoal.Of(context.Ticket, set!));
        return await WalkAsync(run, tail, contractClean, cancellationToken);
    }

    private (SpecReviewSettings? Settings, string? Failure) Read()
    {
        try
        {
            var current = settings.Current();
            return current.Review.Problems() is { Count: > 0 } problems
                ? (null, $"The spec_review settings are invalid: {string.Join("; ", problems)}")
                : (current, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (null, $"The spec_review settings could not be read: {ex.Message}");
        }
    }

    private static string? Skip(SpecReviewSettings current, SpecSet? set, IReadOnlyList<DerivedSpec> tail, bool contractClean) =>
        current.Review.Rounds == 0 ? "spec_review.rounds is 0"
        : set is null ? "the run has no spec set"
        : set.IsHandedBack ? "the derivation handed the ticket back"
        : set.UnexecutedTail.Count == 0 ? "every spec of the set is built"
        : tail.Count == 0 && contractClean ? "every unbuilt spec's exact text was reviewed clean or answered build by a person, and so was the contract"
        : null;

    private async Task<CommandResult> WalkAsync(SpecReviewRun run, IReadOnlyList<DerivedSpec> tail, bool contractClean, CancellationToken ct)
    {
        var lines = new List<string>();
        var reviewed = new List<string>();
        var objections = new List<StandingObjection>();
        foreach (var spec in tail)
        {
            var first = lines.Count == 0;
            var spent = first ? null : SpecReviewBudget.Spent(run.Pipeline, run.Settings);
            var outcome = spent is null ? await loop.RunAsync(run, spec, first, ct) : null;
            if (outcome?.Failure is { } failure) return CommandResult.Fail(failure);
            if (outcome is not null) reviewed.Add(spec.SpecId);
            if ((spent ?? ReviewStepOutcomes.Take(run.Pipeline, outcome!, lines, objections)) is not { } stop) continue;
            if (spent is not null) ReviewStepOutcomes.Stop(run.Pipeline, spec.SpecId, spent, lines);
            break;
        }
        var round = await series.RunAsync(run, reviewed, contractClean, ct);
        if (round.Failure is { } broke) return CommandResult.Fail(broke);
        lines.Add(round.Line);
        if (round.Objection is { } seriesObjection) objections.Add(seriesObjection);
        if (round.Uncleared is { } held) ReviewStepOutcomes.Stop(run.Pipeline, held.BeforeSpecId, held.Reason, lines);
        // 2026-11-10-8e59: a member the series round changed was attacked again; what that left is taken like any spec.
        foreach (var again in round.Reattacked)
        {
            if (again.Failure is { } failed) return CommandResult.Fail(failed);
            ReviewStepOutcomes.Take(run.Pipeline, again, lines, objections);
        }
        var ask = exhaustion.Decide(objections, run.Pipeline);
        lines.AddRange(objections.Except(ask).Select(o => $"{o.MarkerKey}: the author answered build as reviewed"));
        return await park.ApplyAsync(run.Pipeline, ask, Done(string.Join("; ", lines)), ct);
    }

    private CommandResult Done(string message)
    {
        logger.LogInformation("{Message}", message);
        return CommandResult.Ok(message);
    }
}
