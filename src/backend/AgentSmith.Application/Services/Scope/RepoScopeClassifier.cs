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
/// Mirrors GeneratePlanHandler's chat plumbing (factory + call scope + cost
/// tracking). Never throws: an LLM/transport failure returns an error string
/// so the handler falls back to all repos, exactly like a parse failure.
/// </summary>
public sealed class RepoScopeClassifier(
    IChatClientFactory chatClientFactory,
    IRunContextAccessor runContext,
    ILogger<RepoScopeClassifier> logger)
{
    public async Task<(RepoScopeClassification? Classification, string? Error)> ClassifyAsync(
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
                [new(ChatRole.System, SystemPrompt), new(ChatRole.User, BuildUserPrompt(ticket, comments, repos, inventory))],
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
            return (null, $"classifier call failed ({ex.GetType().Name}: {ex.Message})");
        }

        var classification = RepoScopeParser.TryParse(text);
        return classification is null
            ? (null, "classifier reply had no parseable {\"repos\": …} object")
            : (classification, null);
    }

    private const string SystemPrompt =
        "You are a repository scope classifier for a multi-repository software project. "
        + "Decide which of the project's repositories — and which CONTEXTS within them — "
        + "must be checked out and provisioned to implement the ticket.\n\n"
        + "Reply with ONLY one JSON object, no prose:\n"
        + "{\"repos\": [\"<repo name>\", ...], "
        + "\"contexts\": {\"<repo name>\": [\"<context name>\", ...]}, "
        + "\"complexity\": \"trivial|small|medium|large\", "
        + "\"confidence\": <0.0-1.0>, \"rationale\": \"<1-2 sentences>\"}\n\n"
        + "Rules:\n"
        + "- repos must be a subset of the listed repository names, spelled exactly.\n"
        + "- Include every repository whose code must change or must be inspected to make the change.\n"
        + "- contexts is OPTIONAL and finer-grained: for a kept repo, list ONLY the contexts "
        + "(spelled exactly as listed) that must change or be inspected. OMIT a repo from "
        + "contexts (or omit contexts entirely) to keep ALL of its contexts. Never list a "
        + "context for a repo not in repos.\n"
        // p0341c: a coarse effort bucket that sizes the run's cost ceiling (not its
        // correctness). Estimate the SCALE of the change, not your confidence:
        + "- complexity is a coarse estimate of the CHANGE SIZE this ticket implies: "
        + "'trivial' = a one-line / config tweak; 'small' = a localised bug fix in one repo; "
        + "'medium' = a feature touching several files; 'large' = a cross-repo migration or "
        + "sweeping refactor. When unsure, estimate HIGHER — it only sizes the budget ceiling.\n"
        + "- confidence is your certainty that the OMITTED repositories AND contexts are NOT affected.\n"
        + "- When unsure whether a repository or context is affected, include it and lower confidence.";

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
