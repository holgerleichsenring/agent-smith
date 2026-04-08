using System.Globalization;
using System.Text.RegularExpressions;
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
public sealed partial class SpawnFixHandler(
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

        var severities = GetIncludedSeverities(config.SeverityThreshold);

        var fixable = findings
            .Where(f => severities.Contains(f.Severity.ToUpperInvariant()))
            .Where(f => !string.IsNullOrWhiteSpace(f.File))
            .Where(f => !IsExcluded(f.File, config.ExcludedPatterns))
            .ToList();

        if (fixable.Count == 0)
        {
            logger.LogInformation("No Critical/High findings with file paths to fix");
            return CommandResult.Ok("No fixable findings above severity threshold");
        }

        var groups = fixable
            .GroupBy(f => (File: f.File, Category: ExtractCategory(f.Title)))
            .Take(config.MaxConcurrent)
            .ToList();

        var requests = groups
            .Select(g => new SecurityFixRequest(
                FilePath: g.Key.File,
                Category: g.Key.Category,
                SuggestedBranch: GenerateBranchName(g.First()),
                Items: g.Select(f => new SecurityFixItem(
                    Severity: f.Severity.ToUpperInvariant(),
                    Title: f.Title,
                    Description: f.Description,
                    CweId: ExtractCweId(f.Description),
                    Line: f.StartLine)).ToList().AsReadOnly()))
            .ToList();

        if (config.ConfirmBeforeFix)
        {
            var distinctFiles = fixable.Select(f => f.File).Distinct().Count();
            var categories = string.Join(", ", fixable
                .GroupBy(f => ExtractCategory(f.Title))
                .OrderByDescending(g => g.Count())
                .Take(3)
                .Select(g => g.Key));

            var questionText = $"Found {fixable.Count} fixable findings in {distinctFiles} files.\n" +
                               $"Top categories: {categories}\n" +
                               $"Auto-fix will create {groups.Count} fix branches.\n" +
                               "Proceed?";

            var questionId = $"confirm-fix-{Guid.NewGuid():N}";
            var jobId = progressReporter.JobId;

            if (jobId is not null)
            {
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
                    return CommandResult.Ok("Auto-fix cancelled by human");
                }
            }
            else
            {
                var approved = await progressReporter.AskYesNoAsync(
                    questionId, questionText, defaultAnswer: true, cancellationToken);

                if (!approved)
                {
                    logger.LogInformation("Auto-fix cancelled by human via CLI");
                    return CommandResult.Ok("Auto-fix cancelled by human");
                }
            }
        }

        var fixesDir = Path.Combine(repo.LocalPath, FixesDir);
        Directory.CreateDirectory(fixesDir);

        foreach (var request in requests)
        {
            var fileName = SanitizeFileName(request.SuggestedBranch) + ".yaml";
            var filePath = Path.Combine(fixesDir, fileName);
            var yaml = SerializeFixRequest(request);
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

    internal static HashSet<string> GetIncludedSeverities(string threshold) =>
        threshold.ToUpperInvariant() switch
        {
            "CRITICAL" => ["CRITICAL"],
            _ => ["CRITICAL", "HIGH"],
        };

    internal static string ExtractCategory(string title)
    {
        // Use the first word of the title as a rough category
        var firstSpace = title.IndexOf(' ');
        return firstSpace > 0 ? title[..firstSpace] : title;
    }

    internal static string? ExtractCweId(string description)
    {
        var match = CwePattern().Match(description);
        return match.Success ? match.Groups[1].Value : null;
    }

    internal static string GenerateBranchName(Finding finding)
    {
        var cweId = ExtractCweId(finding.Description);
        var slug = SlugPattern().Replace(finding.Title.ToLowerInvariant(), "-");
        slug = slug.Trim('-');
        if (slug.Length > 40) slug = slug[..40].TrimEnd('-');

        return cweId is not null
            ? $"security-fix/cwe-{cweId}-{slug}"
            : $"security-fix/{slug}";
    }

    internal static string SanitizeFileName(string branchName) =>
        branchName.Replace('/', '-');

    internal static bool IsExcluded(string filePath, List<string> excludedPatterns) =>
        excludedPatterns.Any(p => filePath.Contains(p, StringComparison.OrdinalIgnoreCase));

    internal static string SerializeFixRequest(SecurityFixRequest request)
    {
        var lines = new List<string>
        {
            $"file_path: {request.FilePath}",
            $"category: {request.Category}",
            $"suggested_branch: {request.SuggestedBranch}",
            "items:"
        };

        foreach (var item in request.Items)
        {
            lines.Add($"  - severity: {item.Severity}");
            lines.Add($"    title: \"{EscapeYaml(item.Title)}\"");
            lines.Add($"    description: \"{EscapeYaml(item.Description)}\"");
            lines.Add(item.CweId is not null
                ? $"    cwe_id: {item.CweId}"
                : "    cwe_id:");
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"    line: {item.Line}"));
        }

        return string.Join('\n', lines) + '\n';
    }

    private static string EscapeYaml(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    [GeneratedRegex(@"CWE-(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex CwePattern();

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex SlugPattern();
}
