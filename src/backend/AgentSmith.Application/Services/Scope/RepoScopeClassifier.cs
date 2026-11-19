using System.Text;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Prompts;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Scope;

/// <summary>
/// p0331: the one cheap LLM call of the ScopeRepos step. 2026-11-19-6b0bb: it reads the ticket (body + comments)
/// beside the per-repo context inventory and answers what a run uses — the ticket's size and shape, and whether
/// it demands something that must not be done. Standard chat plumbing (factory + call scope + cost tracking).
/// An LLM/transport failure returns an error string and the run goes on unestimated. 2026-10-12-6bfae: the one
/// exception is a rejected model id — a configuration fault every later call repeats — which propagates and fails
/// the step with its explanation.
/// </summary>
public sealed class RepoScopeClassifier(
    IChatClientFactory chatClientFactory,
    IRunContextAccessor runContext,
    ILogger<RepoScopeClassifier> logger)
{
    public async Task<ScopeClassificationResult> ClassifyAsync(
        Ticket ticket, IReadOnlyList<TicketComment>? comments,
        IReadOnlyList<RepoConnection> repos,
        IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>> inventory,
        AgentConfig agentConfig, PipelineContext pipeline, CancellationToken cancellationToken)
    {
        string? text;
        try
        {
            var chat = chatClientFactory.Create(agentConfig, TaskType.Planning);
            var maxTokens = chatClientFactory.GetMaxOutputTokens(agentConfig, TaskType.Planning);
            using var _scope = runContext.BeginCallScope("repo-scope", SkillExecutionPhase.Plan.ToString());
            var response = await chat.GetResponseAsync(
                [
                    new(ChatRole.System, RepoScopeSystemPrompt.Text),
                    new(ChatRole.User, RepoScopePrompt.Build(ticket, comments, repos, inventory)),
                ],
                new ChatOptions { MaxOutputTokens = maxTokens }, cancellationToken);
            PipelineCostTracker.GetOrCreate(pipeline).Track(response);
            text = response.Text;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // operator/watchdog cancel is not a classification failure
        }
        catch (ModelIdRejectedException)
        {
            throw; // the configured id is refused; falling back would only fail the next step
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "The scope call failed — the run goes on unestimated");
            return new ScopeClassificationResult(
                ScopeEstimate.None, $"classifier call failed ({ex.GetType().Name}: {ex.Message})", CallFailed: true);
        }

        return Read(text);
    }

    // The estimate and the refusal are read each on its own, so a reply carrying one is not refused for the other.
    private static ScopeClassificationResult Read(string? text) =>
        new(ScopeEstimateParser.Parse(text), null, ScopeRefusalParser.Parse(text));
}
