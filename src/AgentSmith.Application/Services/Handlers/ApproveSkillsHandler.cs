using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Asks for human approval for each evaluated skill candidate.
/// Uses IDialogueTransport when running in job mode, IProgressReporter otherwise.
/// Conservative default: "no" (reject).
/// </summary>
public sealed class ApproveSkillsHandler(
    IDialogueTransport dialogueTransport,
    IDialogueTrail dialogueTrail,
    IProgressReporter progressReporter,
    ILogger<ApproveSkillsHandler> logger)
    : ICommandHandler<ApproveSkillsContext>
{
    private static readonly string[] ApprovalAnswers = ["yes", "y", "approve", "approved"];

    public async Task<CommandResult> ExecuteAsync(
        ApproveSkillsContext context, CancellationToken cancellationToken)
    {
        if (context.Evaluations.Count == 0)
        {
            logger.LogInformation("No skills to approve");
            context.Pipeline.Set(ContextKeys.ApprovedSkills, (IReadOnlyList<SkillEvaluation>)[]);
            return CommandResult.Ok("No skills to approve");
        }

        var approved = new List<SkillEvaluation>();
        var jobId = progressReporter.JobId;

        foreach (var evaluation in context.Evaluations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var questionText = FormatApprovalQuestion(evaluation);
            var questionId = $"approve-skill-{evaluation.Candidate.Name}-{Guid.NewGuid():N}";

            bool isApproved;

            if (jobId is not null)
            {
                var question = new DialogQuestion(
                    QuestionId: questionId,
                    Type: QuestionType.Approval,
                    Text: questionText,
                    Context: evaluation.Candidate.Content,
                    Choices: null,
                    DefaultAnswer: "no",
                    Timeout: TimeSpan.FromMinutes(10));

                await dialogueTransport.PublishQuestionAsync(jobId, question, cancellationToken);
                var answer = await dialogueTransport.WaitForAnswerAsync(
                    jobId, questionId, question.Timeout, cancellationToken);

                if (answer is not null)
                    await dialogueTrail.RecordAsync(question, answer);

                isApproved = answer is not null &&
                    ApprovalAnswers.Contains(answer.Answer.Trim().ToLowerInvariant());
            }
            else
            {
                isApproved = await progressReporter.AskYesNoAsync(
                    questionId, questionText, defaultAnswer: false, cancellationToken);
            }

            if (isApproved)
            {
                approved.Add(evaluation);
                logger.LogInformation("Skill {Name} approved", evaluation.Candidate.Name);
            }
            else
            {
                logger.LogInformation("Skill {Name} rejected", evaluation.Candidate.Name);
            }
        }

        context.Pipeline.Set(ContextKeys.ApprovedSkills, (IReadOnlyList<SkillEvaluation>)approved.AsReadOnly());

        logger.LogInformation("Approved {Approved}/{Total} skills",
            approved.Count, context.Evaluations.Count);

        return approved.Count > 0
            ? CommandResult.Ok($"{approved.Count}/{context.Evaluations.Count} skills approved")
            : CommandResult.Ok("No skills approved");
    }

    internal static string FormatApprovalQuestion(SkillEvaluation evaluation)
    {
        return $"""
            Install skill "{evaluation.Candidate.Name}"?

            Fit: {evaluation.FitScore}/10 - {evaluation.FitReasoning}
            Safety: {evaluation.SafetyScore}/10 - {evaluation.SafetyReasoning}
            Recommendation: {evaluation.Recommendation}
            Overlap: {(evaluation.HasOverlap ? $"Yes, with {evaluation.OverlapWith}" : "No")}

            Source: {evaluation.Candidate.SourceUrl}
            """;
    }
}
