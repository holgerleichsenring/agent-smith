using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Drafts SKILL.md, agentsmith.md, and source.md files for each evaluated skill
/// into a temporary directory for review before installation.
/// </summary>
public sealed class DraftSkillFilesHandler(
    ILogger<DraftSkillFilesHandler> logger)
    : ICommandHandler<DraftSkillFilesContext>
{
    public Task<CommandResult> ExecuteAsync(
        DraftSkillFilesContext context, CancellationToken cancellationToken)
    {
        if (context.Evaluations.Count == 0)
        {
            logger.LogInformation("No evaluations to draft");
            return Task.FromResult(CommandResult.Ok("No evaluations to draft"));
        }

        var draftRoot = Path.Combine(Path.GetTempPath(), "agentsmith-skill-drafts-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(draftRoot);

        foreach (var evaluation in context.Evaluations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var skillDir = Path.Combine(draftRoot, evaluation.Candidate.Name);
            Directory.CreateDirectory(skillDir);

            // Write SKILL.md (the original skill content)
            File.WriteAllText(
                Path.Combine(skillDir, "SKILL.md"),
                evaluation.Candidate.Content);

            // Write agentsmith.md (agent metadata with convergence criteria)
            var agentsmith = GenerateAgentsmithMd(evaluation);
            File.WriteAllText(
                Path.Combine(skillDir, "agentsmith.md"),
                agentsmith);

            // Write source.md (provenance tracking)
            var source = GenerateSourceMd(evaluation);
            File.WriteAllText(
                Path.Combine(skillDir, "source.md"),
                source);

            logger.LogDebug("Drafted skill files for {Name} in {Dir}", evaluation.Candidate.Name, skillDir);
        }

        context.Pipeline.Set(ContextKeys.SkillInstallPath, draftRoot);

        logger.LogInformation("Drafted {Count} skill packages to {Dir}", context.Evaluations.Count, draftRoot);
        return Task.FromResult(CommandResult.Ok($"{context.Evaluations.Count} skill packages drafted to {draftRoot}"));
    }

    internal static string GenerateAgentsmithMd(SkillEvaluation evaluation)
    {
        return $"""
            ---
            name: {evaluation.Candidate.Name}
            fit_score: {evaluation.FitScore}
            safety_score: {evaluation.SafetyScore}
            recommendation: {evaluation.Recommendation}
            ---

            # {evaluation.Candidate.Name}

            {evaluation.Candidate.Description}

            ## Convergence Criteria

            - Skill content is reviewed and approved by a human operator
            - Fit score meets minimum threshold ({evaluation.FitScore}/10)
            - Safety score meets minimum threshold ({evaluation.SafetyScore}/10)

            ## Evaluation

            **Fit**: {evaluation.FitReasoning}
            **Safety**: {evaluation.SafetyReasoning}
            """;
    }

    internal static string GenerateSourceMd(SkillEvaluation evaluation)
    {
        var candidate = evaluation.Candidate;
        return $"""
            ---
            origin: {candidate.SourceUrl}
            version: {candidate.Version ?? "unknown"}
            commit: {candidate.Commit ?? "unknown"}
            reviewed: {DateOnly.FromDateTime(DateTime.UtcNow):yyyy-MM-dd}
            reviewed_by: skill-manager
            ---
            """;
    }
}
