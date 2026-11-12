using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using AgentSmith.Application.Services.Json;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-10-14-dbcek: a fresh reasoning instance reviews a proposed list of tickets as a plan, with a look over the
/// turn's repositories AND its uploaded material, where the operator's requirements live too. Admission: a missing
/// requirement is a finding about the whole list; any other finding naming no ticket of the list is dropped.
/// 2026-11-12-cd4ci: findings only — nothing is cut. Null when the call failed or the answer could not be read.
/// </summary>
public sealed class TicketPlanReviewer(
    IChatClientFactory chats, IRunContextAccessor runContext, DerivationLookFactory looks, ILogger<TicketPlanReviewer> logger)
{
    private const string RoleName = "ticket-plan-reviewer";

    public async Task<IReadOnlyList<ProposalFinding>?> ReviewAsync(ProposalTurn turn, TicketGraphOutcome graph, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(turn);
        ArgumentNullException.ThrowIfNull(graph);
        var (look, uploads) = Look(turn.Pipeline, turn.Agent);
        turn.Pipeline.TryGet<IReadOnlyList<SpecDialogTurn>>(ContextKeys.SpecDialogTranscript, out var transcript);
        var prompt = TicketPlanReviewPrompt.For(graph, transcript ?? [], look, uploads, ProposalOpenQuestions.Of(turn.Pipeline));
        var key = turn.Pipeline.TryGet<string>(ContextKeys.DialogueJobId, out var jobId) && jobId is not null ? jobId : "spec-dialog";
        try
        {
            // The look owns none of the turn's sandboxes — the runner disposes them — so nothing is disposed.
            var chat = chats.Create(turn.Agent, TaskType.Reasoning, look is null ? null : look.Terms.Allowance + 2); // cd4cl
            using var _ = turn.CostTracker.BeginCall(RoleName, RoleName, SkillExecutionPhase.Plan, key);
            using var _scope = runContext.BeginCallScope(RoleName, SkillExecutionPhase.Plan.ToString(), key);
            var response = await chat.GetResponseAsync(
                [new ChatMessage(ChatRole.User, prompt)], new ChatOptions { Tools = DerivationTools.For(look) }, ct);
            turn.CostTracker.Track(response);
            var text = response.Messages.LastOrDefault(m => !string.IsNullOrWhiteSpace(m.Text))?.Text ?? response.Text;
            var answer = JsonAnswerReader.ReadArray<TicketPlanFindingWire, TicketPlanFindingWire>(text, w => w);
            if (answer is not null) return Admit(graph, answer);
            logger.LogWarning("The plan review of the proposed list returned nothing readable");
        }
        // On the RUN's token: an LLM NetworkTimeout arrives as a TaskCanceledException on an uncancelled token.
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "The plan review of the proposed list could not be taken");
        }
        return null;
    }

    /// <summary>The turn's repositories and uploads, no template; the uploads by their reference: address.</summary>
    private (DerivationLook? Look, IReadOnlyList<string> Uploads) Look(PipelineContext pipeline, AgentConfig agent)
    {
        if (!pipeline.TryGet<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, out var map) || map is null)
            return (null, []);
        var readable = map.Where(e => !e.Key.StartsWith(TemplateScopeName.Prefix, StringComparison.Ordinal))
            .ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        if (readable.Count == 0) return (null, []);
        return (looks.ForReview(readable, agent),
            [.. readable.Keys.Where(k => k.StartsWith(ReferenceScopeName.Prefix, StringComparison.Ordinal))]);
    }

    private List<ProposalFinding> Admit(TicketGraphOutcome graph, IReadOnlyList<TicketPlanFindingWire> answer)
    {
        var findings = new List<ProposalFinding>();
        foreach (var item in answer)
        {
            var (problem, why) = (item.Problem?.Trim().ToLowerInvariant() ?? "other", item.Why?.Trim() ?? string.Empty);
            if (problem == TicketPlanReviewPrompt.Missing)
            {
                findings.Add(new ProposalFinding(TicketPlanReviewPrompt.ListKey, problem, why));
                continue;
            }
            var ticket = graph.Tickets.FirstOrDefault(t => string.Equals(t.Key, item.Ticket?.Trim(), StringComparison.Ordinal));
            if (ticket is null)
                logger.LogWarning("The plan review named no ticket of the list ({Ticket}) — discarding: {Why}", item.Ticket, why);
            else findings.Add(new ProposalFinding(ticket.Key, problem, why));
        }
        return findings;
    }
}
