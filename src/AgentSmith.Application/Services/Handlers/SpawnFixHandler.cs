using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Reads extracted security findings, filters to Critical/High severity,
/// groups by file + category, and writes fix request YAML files to
/// .agentsmith/security/fixes/ for pickup by a follow-up command.
/// </summary>
public sealed class SpawnFixHandler(
    ILogger<SpawnFixHandler> logger,
    IDialogueTransport dialogueTransport,
    IDialogueTrail dialogueTrail,
    IProgressReporter progressReporter)
    : ICommandHandler<SpawnFixContext>
{
    private const string FixesDir = ".agentsmith/security/fixes";

    public async Task<CommandResult> ExecuteAsync(
        SpawnFixContext context, CancellationToken cancellationToken)
    {
        var config = context.Config;

        if (!config.Enabled)
        {
            logger.LogInformation("Auto-fix is disabled, skipping fix generation");
            return CommandResult.Ok("Auto-fix disabled, skipping");
        }

        if (!context.Pipeline.TryGet<Repository>(ContextKeys.Repository, out var repo)
            || repo is null)
        {
            logger.LogInformation("No repository available, skipping fix generation");
            return CommandResult.Ok("No repository, skipping fix generation");
        }

        context.Pipeline.TryGet<IReadOnlyList<Finding>>(ContextKeys.ExtractedFindings, out var findings);
        findings ??= [];

        if (findings.Count == 0)
        {
            logger.LogInformation("No findings to fix");
            return CommandResult.Ok("No findings to fix");
        }

        var severities = SecurityFixRequestBuilder.GetIncludedSeverities(config.SeverityThreshold);

        var fixable = findings
            .Where(f => severities.Contains(f.Severity.ToUpperInvariant()))
            .Where(f => !string.IsNullOrWhiteSpace(f.File))
            .Where(f => !SecurityFixRequestBuilder.IsExcluded(f.File, config.ExcludedPatterns))
            .ToList();

        if (fixable.Count == 0)
        {
            logger.LogInformation("No Critical/High findings with file paths to fix");
            return CommandResult.Ok("No fixable findings above severity threshold");
        }

        var groups = fixable
            .GroupBy(f => (File: f.File, Category: SecurityFixRequestBuilder.ExtractCategory(f.Title)))
            .Take(config.MaxConcurrent)
            .ToList();

        var requests = groups
            .Select(g => new SecurityFixRequest(
                FilePath: g.Key.File,
                Category: g.Key.Category,
                SuggestedBranch: SecurityFixRequestBuilder.GenerateBranchName(g.First()),
                Items: g.Select(f => new SecurityFixItem(
                    Severity: f.Severity.ToUpperInvariant(),
                    Title: f.Title,
                    Description: f.Description,
                    CweId: SecurityFixRequestBuilder.ExtractCweId(f.Description),
                    Line: f.StartLine)).ToList().AsReadOnly()))
            .ToList();

        if (config.ConfirmBeforeFix)
        {
            var approved = await ConfirmWithHumanAsync(fixable, groups.Count, cancellationToken);
            if (!approved)
                return CommandResult.Ok("Auto-fix cancelled by human");
        }

        var fixesDir = Path.Combine(repo.LocalPath, FixesDir);
        Directory.CreateDirectory(fixesDir);

        foreach (var request in requests)
        {
            var fileName = SecurityFixRequestBuilder.SanitizeFileName(request.SuggestedBranch) + ".yaml";
            var filePath = Path.Combine(fixesDir, fileName);
            var yaml = SecurityFixRequestBuilder.SerializeFixRequest(request);
            File.WriteAllText(filePath, yaml);
        }

        context.Pipeline.Set(ContextKeys.SecurityFixRequests,
            (IReadOnlyList<SecurityFixRequest>)requests.AsReadOnly());

        var totalItems = requests.Sum(r => r.Items.Count);
        logger.LogInformation(
            "Written {RequestCount} fix requests for {FindingCount} findings to {Dir}",
            requests.Count, totalItems, fixesDir);

        return CommandResult.Ok(
            $"{requests.Count} fix requests written for {totalItems} findings");
    }

    private async Task<bool> ConfirmWithHumanAsync(
        List<Finding> fixable, int groupCount, CancellationToken cancellationToken)
    {
        var distinctFiles = fixable.Select(f => f.File).Distinct().Count();
        var categories = string.Join(", ", fixable
            .GroupBy(f => SecurityFixRequestBuilder.ExtractCategory(f.Title))
            .OrderByDescending(g => g.Count())
            .Take(3)
            .Select(g => g.Key));

        var questionText = $"Found {fixable.Count} fixable findings in {distinctFiles} files.\n" +
                           $"Top categories: {categories}\n" +
                           $"Auto-fix will create {groupCount} fix branches.\n" +
                           "Proceed?";

        var questionId = $"confirm-fix-{Guid.NewGuid():N}";
        var jobId = progressReporter.JobId;

        if (jobId is null)
        {
            var approved = await progressReporter.AskYesNoAsync(
                questionId, questionText, defaultAnswer: true, cancellationToken);

            if (!approved)
                logger.LogInformation("Auto-fix cancelled by human via CLI");

            return approved;
        }

        var question = new DialogQuestion(
            QuestionId: questionId,
            Type: QuestionType.Approval,
            Text: questionText,
            Context: null,
            Choices: null,
            DefaultAnswer: "yes",
            Timeout: TimeSpan.FromMinutes(10));

        await dialogueTransport.PublishQuestionAsync(jobId, question, cancellationToken);
        var answer = await dialogueTransport.WaitForAnswerAsync(
            jobId, questionId, question.Timeout, cancellationToken);

        if (answer is not null)
            await dialogueTrail.RecordAsync(question, answer);

        if (answer is not null && !answer.Answer.Equals("yes", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("Auto-fix cancelled by human via dialogue");
            return false;
        }

        return true;
    }
}
