using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
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
/// the shape that is known to work.
/// </para>
/// </summary>
public sealed class SpecSetDeriver(
    ISpecCutReviewer reviewer,
    IChatClientFactory chatClientFactory,
    IPromptCatalog prompts,
    SpecDerivationParser parser,
    IRunContextAccessor runContext,
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
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, RenderSystemPrompt()),
            new(ChatRole.User, SpecPromptComposer.Compose(ticket, segments, previous, cause, pipeline)),
        };

        string? lastError = null;
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var text = await CallModelAsync(agentConfig, pipeline, messages, cancellationToken);
            var parsed = parser.Parse(
                text, key, ticket.Id.Value, segments,
                previous is null ? SpecSource.Derived : SpecSource.BranchArtifact,
                ExecutedHeadOf(previous));
            if (parsed.Derivation is not null)
            {
                // p0422: the parser checks the SHAPE; a fresh instance checks whether the cut
                // can be DELIVERED. Ticket 19106 passed every shape rule and still carried a
                // phase demanding both "no production source is modified" and "the old
                // library appears nowhere" — the master spent two hours before noticing.
                // Rejected here, the deriver answers instead of a run finding out.
                var review = await reviewer.ReviewAsync(
                    parsed.Derivation.Set, ticket.Description ?? string.Empty,
                    agentConfig, PipelineCostTracker.GetOrCreate(pipeline), cancellationToken);
                if (review.Deliverable) return (parsed.Derivation, null);
                lastError = SpecCutRejection.For(review);
                logger.LogWarning(
                    "Spec cut attempt {Attempt}/{Max} is not deliverable: {Error}",
                    attempt, MaxAttempts, lastError);
                messages.Add(new ChatMessage(ChatRole.Assistant, text));
                messages.Add(new ChatMessage(ChatRole.User,
                    $"Your cut cannot be delivered:\n{lastError}\nRespond again with ONLY the corrected JSON object."));
                continue;
            }
            lastError = parsed.Error;
            logger.LogWarning(
                "Spec derivation attempt {Attempt}/{Max} rejected: {Error}",
                attempt, MaxAttempts, parsed.Error);
            messages.Add(new ChatMessage(ChatRole.Assistant, text));
            messages.Add(new ChatMessage(ChatRole.User,
                $"Your cut was rejected:\n{parsed.Error}\nRespond again with ONLY the corrected JSON object."));
        }
        return (null, lastError);
    }

    // The executed phases in their original order. Only a CONTIGUOUS head can be
    // preserved by position, which is what the append-only rule already implies: the
    // sequence executes in order, so anything executed is a prefix of it.
    private static IReadOnlyList<SpecPhase> ExecutedHeadOf(SpecSet? previous) =>
        previous is null
            ? []
            : [.. previous.Phases.TakeWhile(
                p => previous.Executed.Contains(p.PhaseId, StringComparer.Ordinal))];

    private string RenderSystemPrompt() => prompts.Render(SkillName, new Dictionary<string, string>
    {
        ["MaxPhases"] = SpecSet.MaxPhases.ToString(),
    });

    private async Task<string> CallModelAsync(
        AgentConfig agentConfig, PipelineContext pipeline,
        List<ChatMessage> messages, CancellationToken cancellationToken)
    {
        var chat = chatClientFactory.Create(agentConfig, TaskType.Planning);
        var maxTokens = chatClientFactory.GetMaxOutputTokens(agentConfig, TaskType.Planning);
        using var _scope = runContext.BeginCallScope(
            "spec-derivation", SkillExecutionPhase.Plan.ToString());
        var response = await chat.GetResponseAsync(
            messages, new ChatOptions { MaxOutputTokens = maxTokens }, cancellationToken);
        PipelineCostTracker.GetOrCreate(pipeline).Track(response);
        return response.Text ?? string.Empty;
    }
}
