using System.Text;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Scope;

/// <summary>
/// p0331: the one cheap LLM call of the ScopeRepos step — classifies
/// (ticket body + comments) against the per-repo remote context inventory
/// (name / workdir / language / purpose) into the affected-repo subset.
/// Standard chat plumbing (factory + call scope + cost tracking).
/// Never throws: an LLM/transport failure returns an error string
/// so the handler falls back to all repos, exactly like a parse failure.
/// <para>
/// p0413a: the one call answers TWO questions — which repositories the ticket
/// touches, and what the ticket is (size + shape). They are returned separately
/// because they fail separately: a reply that is no usable repo verdict can
/// still carry a usable estimate, and a run with nothing to scope asks the
/// second question alone.
/// </para>
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
                    new(ChatRole.User, BuildUserPrompt(ticket, comments, repos, inventory)),
                ],
                new ChatOptions { MaxOutputTokens = maxTokens }, cancellationToken);
            PipelineCostTracker.GetOrCreate(pipeline).Track(response);
            text = response.Text;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // operator/watchdog cancel is not a classification failure
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Repo scope classification call failed — keeping all repos");
            return new ScopeClassificationResult(
                null, ScopeEstimate.None, $"classifier call failed ({ex.GetType().Name}: {ex.Message})");
        }

        var classification = RepoScopeParser.TryParse(text);
        // p0413a: the estimate is read from the verdict when the reply is one, and
        // read on its own when it is not — the repos-array contract gates SCOPING,
        // and must not take the size and shape down with it.
        return classification is null
            ? new ScopeClassificationResult(
                null, ScopeEstimateParser.Parse(text),
                "classifier reply had no parseable {\"repos\": …} object")
            : new ScopeClassificationResult(
                classification, new ScopeEstimate(classification.Tier, classification.Shape), null);
    }

    private static string BuildUserPrompt(
        Ticket ticket, IReadOnlyList<TicketComment>? comments,
        IReadOnlyList<RepoConnection> repos,
        IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>> inventory)
    {
        var sb = new StringBuilder("## Repositories in this project\n");
        foreach (var repo in repos)
            sb.AppendLine(DescribeRepo(repo, inventory));
        sb.AppendLine();
        // p0316: ticket fields are untrusted — delimited so an embedded injection
        // reads as data, exactly like the master prompts treat them.
        sb.AppendLine(TicketPromptDelimiters.Wrap($"""
            **Title:** {ticket.Title}
            **Description:** {ticket.Description}
            **Acceptance Criteria:** {ticket.AcceptanceCriteria ?? "None specified"}
            """));
        var conversation = TicketConversationPromptSection.Render(comments);
        if (conversation.Length > 0) sb.AppendLine().AppendLine(conversation);
        return sb.ToString();
    }

    private static string DescribeRepo(
        RepoConnection repo, IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>> inventory)
    {
        var name = repo.Name ?? string.Empty;
        if (!inventory.TryGetValue(name, out var contexts) || contexts.Count == 0)
            return $"- {name}";
        var described = contexts.Select(c =>
        {
            var purpose = string.IsNullOrWhiteSpace(c.Purpose) ? string.Empty : $" — {c.Purpose}";
            return $"'{c.ContextName}' (workdir={c.Workdir}, lang={c.Language ?? "unknown"}){purpose}";
        });
        return $"- {name}: contexts {string.Join("; ", described)}";
    }
}
