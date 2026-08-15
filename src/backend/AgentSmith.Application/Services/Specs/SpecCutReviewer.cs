using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0422: asks a fresh instance which phases CANNOT BE DELIVERED as written, before the
/// first master token is spent on them.
/// <para>
/// The same shape as the delivery account and for the same reason: a fresh instance with
/// no stake in the cut, asked adversarially, and every finding checked against something
/// the framework can verify — here, that the quoted criterion really is in that phase.
/// Ticket 19106 lost two hours to a phase whose criteria contradicted each other; this
/// costs one call before any of them are spent.
/// </para>
/// </summary>
public sealed class SpecCutReviewer(
    IChatClientFactory chatClientFactory,
    IRunContextAccessor runContext,
    ILogger<SpecCutReviewer> logger) : ISpecCutReviewer
{
    private const string RoleName = "spec-cut-reviewer";

    public async Task<SpecCutReview> ReviewAsync(
        SpecSet set, string ticketText, AgentConfig agent,
        PipelineCostTracker costTracker, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(set);
        if (set.Phases.Count == 0) return SpecCutReview.Clean;

        var answer = await AskAsync(set, ticketText, agent, costTracker, cancellationToken);
        if (answer is null)
            return new SpecCutReview([], "the cut review returned nothing readable");

        var kept = answer.Where(finding => Quoted(set, finding)).ToList();
        foreach (var finding in kept)
            logger.LogWarning("Cut review — {Phase} cannot be delivered: {Problem} — {Why}",
                finding.PhaseId, finding.Problem, finding.Why);
        return new SpecCutReview(kept);
    }

    /// <summary>
    /// A finding must quote a criterion the phase really states. Anything else is the
    /// reviewer inventing a fault, which would block a cut nobody can find the flaw in.
    /// </summary>
    private bool Quoted(SpecSet set, CutFinding finding)
    {
        var phase = set.Phases.FirstOrDefault(p =>
            string.Equals(p.Draft.PhaseId, finding.PhaseId, StringComparison.OrdinalIgnoreCase));
        if (phase is not null && phase.Draft.Done.Any(d => Matches(d, finding.Criterion))) return true;

        logger.LogWarning(
            "Cut review quoted a criterion that is not in {Phase} — discarding the finding: {Criterion}",
            finding.PhaseId, Shorten(finding.Criterion));
        return false;
    }

    private static bool Matches(string stated, string quoted) =>
        stated.Contains(quoted.Trim(), StringComparison.OrdinalIgnoreCase)
        || quoted.Contains(stated.Trim(), StringComparison.OrdinalIgnoreCase);

    private async Task<IReadOnlyList<CutFinding>?> AskAsync(
        SpecSet set, string ticketText, AgentConfig agent,
        PipelineCostTracker costTracker, CancellationToken ct)
    {
        try
        {
            var chat = chatClientFactory.Create(agent, TaskType.Reasoning);
            using var _ = costTracker.BeginCall(RoleName, RoleName, SkillExecutionPhase.Plan, set.Key);
            using var _scope = runContext.BeginCallScope(
                RoleName, SkillExecutionPhase.Plan.ToString(), set.Key);
            var response = await chat.GetResponseAsync(
                [new ChatMessage(ChatRole.User, SpecCutReviewPrompt.For(set, ticketText))],
                new ChatOptions(), ct);
            costTracker.Track(response);
            return Read(response.Text);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The cut review call failed");
            return null;
        }
    }

    private static IReadOnlyList<CutFinding>? Read(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var start = text.IndexOf('[');
        var end = text.LastIndexOf(']');
        if (start < 0 || end <= start) return null;
        try
        {
            return JsonSerializer.Deserialize<List<CutFinding>>(
                text[start..(end + 1)],
                // snake_case is what the prompt asks for, and case-insensitivity alone does
                // not bridge an underscore — the same trap ships_code fell into.
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Shorten(string text) => text.Length <= 80 ? text : text[..80] + "…";
}
