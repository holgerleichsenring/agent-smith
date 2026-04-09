using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Converts raw security scan findings (StaticScanResult, GitHistoryScanResult,
/// DependencyAuditResult) into unified Finding records for SARIF/markdown output.
/// Bridges the security-scan discussion pipeline with DeliverFindingsHandler.
/// </summary>
public sealed class ExtractFindingsHandler(
    ILogger<ExtractFindingsHandler> logger)
    : ICommandHandler<ExtractFindingsContext>
{
    public Task<CommandResult> ExecuteAsync(
        ExtractFindingsContext context, CancellationToken cancellationToken)
    {
        var pipeline = context.Pipeline;
        var findings = new List<Finding>();

        if (pipeline.TryGet<StaticScanResult>(ContextKeys.StaticScanResult, out var staticResult))
        {
            foreach (var f in staticResult.Findings)
            {
                findings.Add(new Finding(
                    Severity: f.Severity.ToUpperInvariant(),
                    File: f.File,
                    StartLine: f.Line,
                    EndLine: null,
                    Title: f.Title,
                    Description: f.Description,
                    Confidence: f.Confidence));
            }
        }

        if (pipeline.TryGet<GitHistoryScanResult>(ContextKeys.GitHistoryScanResult, out var historyResult))
        {
            foreach (var f in historyResult.Findings)
            {
                findings.Add(new Finding(
                    Severity: f.StillInWorkingTree ? "HIGH" : "CRITICAL",
                    File: f.File,
                    StartLine: f.Line,
                    EndLine: null,
                    Title: f.Title,
                    Description: f.Description + " (commit: " + f.CommitHash[..7] + ")",
                    Confidence: 9));
            }
        }

        if (pipeline.TryGet<DependencyAuditResult>(ContextKeys.DependencyAuditResult, out var depResult))
        {
            var manifestFile = MapEcosystemToManifest(depResult.Ecosystem);
            foreach (var f in depResult.Findings)
            {
                findings.Add(new Finding(
                    Severity: f.Severity.ToUpperInvariant(),
                    File: manifestFile,
                    StartLine: 0,
                    EndLine: null,
                    Title: f.Package + " " + f.Version + " — " + f.Title,
                    Description: f.Description + (f.Cve != null ? " [" + f.Cve + "]" : ""),
                    Confidence: 8));
            }
        }

        var totalRaw = findings.Count;

        // Apply LLM assessments: mark findings as confirmed/false_positive
        if (pipeline.TryGet<List<FindingAssessment>>(
                ContextKeys.FindingAssessments, out var assessments) && assessments.Count > 0)
        {
            findings = ApplyAssessments(findings, assessments);

            var falsePositives = findings.Count(f => f.ReviewStatus == "false_positive");
            findings = findings.Where(f => f.ReviewStatus != "false_positive").ToList();

            logger.LogInformation(
                "Applied {AssessmentCount} assessments: {FP} false positives removed, {Remaining} findings remain",
                assessments.Count, falsePositives, findings.Count);
        }

        pipeline.Set(ContextKeys.ExtractedFindings, findings.AsReadOnly());

        logger.LogInformation(
            "Extracted {Count} findings from {Raw} raw (static={Static}, history={History}, deps={Deps})",
            findings.Count, totalRaw,
            staticResult?.Findings.Count ?? 0,
            historyResult?.Findings.Count ?? 0,
            depResult?.Findings.Count ?? 0);

        return Task.FromResult(CommandResult.Ok(
            $"Extracted {findings.Count} findings from {totalRaw} raw for output"));
    }

    private static List<Finding> ApplyAssessments(
        List<Finding> findings, List<FindingAssessment> assessments)
    {
        return findings.Select(f =>
        {
            var match = assessments.FirstOrDefault(a =>
                a.File.Equals(f.File, StringComparison.OrdinalIgnoreCase)
                && (a.Line == f.StartLine
                    || a.Title.Equals(f.Title, StringComparison.OrdinalIgnoreCase)));

            return match is not null
                ? f with { ReviewStatus = match.Status }
                : f;
        }).ToList();
    }

    private static string MapEcosystemToManifest(string ecosystem) =>
        ecosystem.ToUpperInvariant() switch
        {
            "NPM" => "package.json",
            "NUGET" => "packages.config",
            "DOTNET" => "Directory.Packages.props",
            "PIP" or "PYTHON" => "requirements.txt",
            "MAVEN" => "pom.xml",
            "GRADLE" => "build.gradle",
            "GO" => "go.mod",
            "RUBY" or "GEMS" => "Gemfile",
            "CARGO" or "RUST" => "Cargo.toml",
            _ => "package.json",
        };
}
