using System.Text.RegularExpressions;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Evaluates each skill candidate for fit and safety using an LLM.
/// Filters out candidates that do not meet minimum score thresholds.
/// </summary>
public sealed partial class EvaluateSkillsHandler(
    IChatClientFactory chatClientFactory,
    IPromptCatalog prompts,
    ILogger<EvaluateSkillsHandler> logger)
    : ICommandHandler<EvaluateSkillsContext>
{
    private const int MinFitScore = 5;
    private const int MinSafetyScore = 7;

    public async Task<CommandResult> ExecuteAsync(
        EvaluateSkillsContext context, CancellationToken cancellationToken)
    {
        if (context.Candidates.Count == 0)
        {
            logger.LogInformation("No candidates to evaluate");
            context.Pipeline.Set(ContextKeys.SkillEvaluations, (IReadOnlyList<SkillEvaluation>)[]);
            return CommandResult.Ok("No candidates to evaluate");
        }

        var installedList = string.Join(", ", context.InstalledSkillNames);
        var evaluations = new List<SkillEvaluation>();

        foreach (var candidate in context.Candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var userPrompt = $"""
                Skill name: {candidate.Name}
                Skill description: {candidate.Description}
                Already installed skills: {installedList}

                Skill content:
                {candidate.Content}
                """;

            try
            {
                var chat = chatClientFactory.Create(context.AgentConfig, TaskType.Scout);
                var maxTokens = chatClientFactory.GetMaxOutputTokens(context.AgentConfig, TaskType.Scout);
                var messages = new List<ChatMessage>
                {
                    new(ChatRole.System, prompts.Get("evaluate-skills-system")),
                    new(ChatRole.User, userPrompt),
                };
                var response = await chat.GetResponseAsync(messages,
                    new ChatOptions { MaxOutputTokens = maxTokens }, cancellationToken);
                PipelineCostTracker.GetOrCreate(context.Pipeline).Track(response);

                var evaluation = ParseEvaluation(candidate, response.Text ?? string.Empty);
                if (evaluation is not null)
                {
                    evaluations.Add(evaluation);
                    logger.LogInformation(
                        "Evaluated {Name}: fit={Fit}, safety={Safety}, recommendation={Rec}",
                        candidate.Name, evaluation.FitScore, evaluation.SafetyScore, evaluation.Recommendation);
                }
                else
                {
                    logger.LogWarning("Failed to parse evaluation for {Name}", candidate.Name);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error evaluating candidate {Name}", candidate.Name);
            }
        }

        // Filter by minimum scores
        var passing = evaluations
            .Where(e => e.FitScore >= MinFitScore && e.SafetyScore >= MinSafetyScore)
            .ToList();

        context.Pipeline.Set(ContextKeys.SkillEvaluations, (IReadOnlyList<SkillEvaluation>)passing.AsReadOnly());

        logger.LogInformation(
            "Evaluated {Total} candidates, {Passing} passed thresholds (fit>={MinFit}, safety>={MinSafety})",
            evaluations.Count, passing.Count, MinFitScore, MinSafetyScore);

        return CommandResult.Ok($"{passing.Count}/{evaluations.Count} candidates passed evaluation");
    }

    internal static SkillEvaluation? ParseEvaluation(SkillCandidate candidate, string response)
    {
        var fitScore = ExtractInt(response, @"FIT_SCORE:\s*(\d+)");
        var safetyScore = ExtractInt(response, @"SAFETY_SCORE:\s*(\d+)");
        var fitReasoning = ExtractLine(response, @"FIT_REASONING:\s*(.+)");
        var safetyReasoning = ExtractLine(response, @"SAFETY_REASONING:\s*(.+)");
        var recommendation = ExtractLine(response, @"RECOMMENDATION:\s*(.+)");
        var hasOverlap = ExtractLine(response, @"HAS_OVERLAP:\s*(.+)");
        var overlapWith = ExtractLine(response, @"OVERLAP_WITH:\s*(.*)");

        if (fitScore is null || safetyScore is null)
            return null;

        return new SkillEvaluation(
            Candidate: candidate,
            FitScore: fitScore.Value,
            SafetyScore: safetyScore.Value,
            FitReasoning: fitReasoning ?? "No reasoning provided",
            SafetyReasoning: safetyReasoning ?? "No reasoning provided",
            Recommendation: recommendation ?? "review",
            HasOverlap: hasOverlap?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false,
            OverlapWith: string.IsNullOrWhiteSpace(overlapWith) ? null : overlapWith);
    }

    private static int? ExtractInt(string text, string pattern)
    {
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        return match.Success && int.TryParse(match.Groups[1].Value.Trim(), out var value) ? value : null;
    }

    private static string? ExtractLine(string text, string pattern)
    {
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }
}
