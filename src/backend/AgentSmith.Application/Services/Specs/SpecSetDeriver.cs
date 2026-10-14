using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: the one LLM call of DeriveSpec, and the only place JUDGEMENT lives —
/// where the spec boundaries fall, which ticket segments are load-bearing, and
/// what may be discarded. The prompt is an EXTERNAL pinned skill so that judgement
/// can be tuned without a release; everything the reply is turned into is computed
/// in code, because a model that retypes a code template produces a plausible copy.
/// <para>
/// A cut that fails validation is rejected BACK to the model with the reason —
/// bounded retries, then the step reports the failure and the caller falls back to
/// the shape that is known to work. 2026-09-07-b7e2: before it writes, the derivation
/// may LOOK through the named read-only tools one host offers across every attempt.
/// </para>
/// <para>
/// 2026-09-08-1830: a deliverable cut that covers fewer contexts than the scope call
/// named is pinned back once — one extra attempt with the first cut and the looks in
/// view — and a gap that survives the pin becomes a question for the author.
/// </para>
/// <para>
/// 2026-10-12-3a36b: a contradiction hand-back passes <see cref="ContradictionHandbackGate"/>
/// between parse and review; a refutation is a second extra attempt beside the pin's.
/// </para>
/// </summary>
public sealed class SpecSetDeriver(
    CutReviewStep cutReview,
    SpecDerivationCall call,
    DerivationLookFactory looks,
    IPromptCatalog prompts,
    SpecDerivationParser parser,
    ScopedContextCoverage coverage,
    ContradictionHandbackGate handbacks,
    ILogger<SpecSetDeriver> logger) : ISpecSetDeriver
{
    /// <summary>Name of the pinned master skill carrying the judgement prompt.</summary>
    public const string SkillName = "spec-derivation-master";

    private const int MaxAttempts = 3;

    public async Task<(SpecDerivation? Derivation, string? Error)> DeriveAsync(
        Ticket ticket, IReadOnlyList<TicketSegment> segments, SpecSet? previous, string series,
        string cause, IReadOnlyList<string> repositories, AgentConfig agentConfig, PipelineContext pipeline,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        var key = previous?.Key ?? TicketKeyFactory.For(ticket, pipeline).Value;
        var look = looks.Create(pipeline);
        await using var owned = look; // 2026-09-13-84c0: it owns any template scope it made

        var named = pipeline.TryGet<ScopeNamedContexts>(ContextKeys.ScopeNamedContexts, out var n) ? n : null;
        var messages = SpecDerivationMessages.Opening(
            RenderSystemPrompt(), ticket, segments, previous, cause, pipeline, look, repositories);

        var attempts = new DerivationAttempts(MaxAttempts);
        for (var attempt = 1; attempt <= attempts.Bound; attempt++)
        {
            var response = await call.AskAsync(
                agentConfig, pipeline, messages, look, cancellationToken);
            // The whole exchange is kept, not its text: the looks travel as tool calls.
            messages.AddRange(response.Messages);
            // 2026-09-13-84c0: a template look refused for budget must not become a cut made
            // against the target's habits while the declaration says a template governs.
            if (look?.TemplateRefusal is { } refused)
                return (null, $"The declared template could not be read: {refused}");
            var parsed = parser.Parse(
                response.Text, key, series, ticket.Id.Value, segments,
                previous is null ? SpecSource.Derived : SpecSource.BranchArtifact,
                previous?.ExecutedHead, look?.Evidence.Lines, repositories,
                pipeline.TryGet<IReadOnlyList<string>>(ContextKeys.SpecTakenIds, out var taken) ? taken : null);
            if (parsed.Derivation is null)
            {
                logger.LogWarning("Spec derivation attempt {Attempt} rejected: {Error}", attempt, parsed.Error);
                messages.Add(attempts.Reject("Your cut was rejected", parsed.Error));
                continue;
            }
            if (DerivedContractRule.Problem(parsed, previous) is { } contract) // 2026-10-13-8762j: yields to the cut on the last attempt
            {
                if (!attempts.IsLast(attempt)) { messages.Add(attempts.Reject("Your series contract was rejected", contract)); continue; }
                logger.LogWarning("Spec derivation attempt {Attempt}: the series contract is dropped, the cut kept: {Problem}", attempt, contract);
            }
            var gated = await handbacks.JudgeAsync(parsed.Derivation, parsed.Uncited, look,
                attempts.IsLast(attempt), attempts.Refuted, agentConfig, pipeline, cancellationToken);
            if (gated.Step == HandbackGateStep.Park) return (parsed.Derivation, null);
            if (gated.Step != HandbackGateStep.Continue) { messages.Add(attempts.Gated(gated, parsed.Derivation)); continue; }
            // p0422: the parser checks the SHAPE; a fresh instance checks whether the cut can be DELIVERED.
            var rejection = await cutReview.ReviewAsync(
                parsed.Derivation, ticket, attempt, attempts.Kept, looks, agentConfig, pipeline, cancellationToken);
            if (rejection is not null) { messages.Add(attempts.Reject("Your cut cannot be delivered", rejection)); continue; }
            var gap = coverage.Gap(named, parsed.Derivation.Set);
            if (gap.Count == 0) return (parsed.Derivation, null);
            if (attempts.Pinned) return (ScopedContextQuestion.For(parsed.Derivation, gap, named!), null);
            logger.LogWarning("Spec cut leaves named context(s) {Gap} uncovered — pinned once", string.Join(", ", gap));
            messages.Add(attempts.Pin(gap, parsed.Derivation.Set));
        }
        return attempts.Exhausted;
    }

    private string RenderSystemPrompt() => prompts.Render(SkillName, new Dictionary<string, string>
    {
        ["MaxSpecs"] = SpecSet.MaxSpecs.ToString(),
    });
}
