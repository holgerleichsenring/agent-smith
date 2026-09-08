using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: the one LLM call of DeriveSpec, and the only place JUDGEMENT lives —
/// where the phase boundaries fall, which ticket segments are load-bearing, and
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
/// </summary>
public sealed class SpecSetDeriver(
    ISpecCutReviewer reviewer,
    SpecDerivationCall call,
    DerivationLookFactory looks,
    IPromptCatalog prompts,
    SpecDerivationParser parser,
    ScopedContextCoverage coverage,
    ILogger<SpecSetDeriver> logger) : ISpecSetDeriver
{
    /// <summary>Name of the pinned master skill carrying the judgement prompt.</summary>
    public const string SkillName = "spec-derivation-master";

    private const int MaxAttempts = 3;

    public async Task<(SpecDerivation? Derivation, string? Error)> DeriveAsync(
        Ticket ticket, IReadOnlyList<TicketSegment> segments, SpecSet? previous, string cause,
        AgentConfig agentConfig, PipelineContext pipeline, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        var key = previous?.Key ?? SpecSetKeyFactory.For(ticket, pipeline).Value;
        var look = looks.Create(pipeline);
        var named = pipeline.TryGet<ScopeNamedContexts>(ContextKeys.ScopeNamedContexts, out var n) ? n : null;
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, RenderSystemPrompt()),
            new(ChatRole.User,
                SpecPromptComposer.Compose(ticket, segments, previous, cause, pipeline)
                + DerivationLookPromptSection.Render(look)),
        };

        string? lastError = null;
        var kept = new LeastObjectedCut();
        var pinned = false;
        for (var attempt = 1; attempt <= MaxAttempts + (pinned ? 1 : 0); attempt++)
        {
            var response = await call.AskAsync(
                agentConfig, pipeline, messages, DerivationTools.For(look), cancellationToken);
            // The whole exchange is kept, not its text: the looks travel as tool calls.
            messages.AddRange(response.Messages);
            var parsed = parser.Parse(
                response.Text, key, ticket.Id.Value, segments,
                previous is null ? SpecSource.Derived : SpecSource.BranchArtifact,
                previous?.ExecutedHead, look?.Evidence.Lines);
            if (parsed.Derivation is null)
            {
                lastError = parsed.Error;
                logger.LogWarning("Spec derivation attempt {Attempt} rejected: {Error}", attempt, parsed.Error);
                messages.Add(Again("Your cut was rejected", parsed.Error));
                continue;
            }
            // p0422: the parser checks the SHAPE; a fresh instance checks whether the cut can
            // be DELIVERED. Rejected here, the deriver answers instead of a run finding out.
            var review = await reviewer.ReviewAsync(
                parsed.Derivation.Set, ticket.Description ?? string.Empty,
                agentConfig, PipelineCostTracker.GetOrCreate(pipeline), cancellationToken);
            if (!review.Deliverable)
            {
                kept.Offer(parsed.Derivation, review);
                lastError = SpecCutRejection.For(review);
                logger.LogWarning("Spec cut attempt {Attempt} is not deliverable: {Error}", attempt, lastError);
                messages.Add(Again("Your cut cannot be delivered", lastError));
                continue;
            }
            var gap = coverage.Gap(named, parsed.Derivation.Set);
            if (gap.Count == 0) return (parsed.Derivation, null);
            if (pinned) return (ScopedContextQuestion.For(parsed.Derivation, gap, named!), null);
            pinned = true;
            logger.LogWarning("Spec cut leaves named context(s) {Gap} uncovered — pinned once", string.Join(", ", gap));
            messages.Add(new ChatMessage(ChatRole.User,
                ScopedContextGapPromptSection.Render(gap, ScopedContextCoverage.Carried(parsed.Derivation.Set))));
        }
        return (kept.Best, lastError);
    }

    private static ChatMessage Again(string verdict, string? error) =>
        new(ChatRole.User, $"{verdict}:\n{error}\nRespond again with ONLY the corrected JSON object.");

    private string RenderSystemPrompt() => prompts.Render(SkillName, new Dictionary<string, string>
    {
        ["MaxPhases"] = SpecSet.MaxPhases.ToString(),
    });
}
