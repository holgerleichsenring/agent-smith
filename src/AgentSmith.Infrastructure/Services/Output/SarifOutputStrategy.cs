using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Output;

/// <summary>
/// Produces valid SARIF 2.1.0 output. Writes findings.sarif to current directory
/// and logs a human-readable summary.
/// </summary>
public sealed class SarifOutputStrategy(
    ILogger<SarifOutputStrategy> logger) : IOutputStrategy
{
    public string ProviderType => "sarif";

    public async Task DeliverAsync(OutputContext context, CancellationToken cancellationToken = default)
    {
        var sarif = BuildSarifDocument(context.Findings);
        var json = sarif.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

        var outputDir = ResolveOutputDir(context);
        Directory.CreateDirectory(outputDir);

        var outputPath = Path.Combine(outputDir, "findings.sarif");
        await File.WriteAllTextAsync(outputPath, json, cancellationToken);
        logger.LogInformation("SARIF report written to {Path}", outputPath);

        LogSummary(context.Findings);
    }

    private static string ResolveOutputDir(OutputContext context)
    {
        context.Pipeline.TryGet<string>(ContextKeys.OutputDir, out var dir);
        if (!string.IsNullOrWhiteSpace(dir) && IsWritable(dir))
            return dir;

        if (IsWritable("/output"))
            return "/output";

        return "./agentsmith-output";
    }

    private static bool IsWritable(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            var testFile = Path.Combine(path, ".write-test");
            File.WriteAllText(testFile, "");
            File.Delete(testFile);
            return true;
        }
        catch { return false; }
    }

    internal static JsonObject BuildSarifDocument(IReadOnlyList<Finding> findings)
    {
        var rules = new JsonArray();
        var results = new JsonArray();
        var ruleIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var finding in findings)
        {
            var ruleId = GetOrCreateRuleId(finding.Title, ruleIndex, rules);
            results.Add(BuildResult(finding, ruleId));
        }

        return new JsonObject
        {
            ["$schema"] = "https://json.schemastore.org/sarif-2.1.0.json",
            ["version"] = "2.1.0",
            ["runs"] = new JsonArray
            {
                new JsonObject
                {
                    ["tool"] = new JsonObject
                    {
                        ["driver"] = new JsonObject
                        {
                            ["name"] = "Agent Smith Security",
                            ["version"] = "1.0.0",
                            ["rules"] = rules
                        }
                    },
                    ["results"] = results
                }
            }
        };
    }

    private static string GetOrCreateRuleId(
        string title, Dictionary<string, int> ruleIndex, JsonArray rules)
    {
        if (ruleIndex.TryGetValue(title, out var existing))
            return $"AS{existing:D3}";

        var index = ruleIndex.Count + 1;
        ruleIndex[title] = index;
        var ruleId = $"AS{index:D3}";

        rules.Add(new JsonObject
        {
            ["id"] = ruleId,
            ["shortDescription"] = new JsonObject { ["text"] = title }
        });

        return ruleId;
    }

    private static JsonObject BuildResult(Finding finding, string ruleId)
    {
        var region = new JsonObject { ["startLine"] = finding.StartLine };
        if (finding.EndLine.HasValue)
            region["endLine"] = finding.EndLine.Value;

        return new JsonObject
        {
            ["ruleId"] = ruleId,
            ["level"] = MapSeverity(finding.Severity),
            ["message"] = new JsonObject { ["text"] = finding.Description },
            ["locations"] = new JsonArray
            {
                new JsonObject
                {
                    ["physicalLocation"] = new JsonObject
                    {
                        ["artifactLocation"] = new JsonObject { ["uri"] = finding.File },
                        ["region"] = region
                    }
                }
            }
        };
    }

    internal static string MapSeverity(string severity) => severity.ToUpperInvariant() switch
    {
        "HIGH" => "error",
        "MEDIUM" => "warning",
        "LOW" => "note",
        _ => "note"
    };

    /// <summary>
    /// Compresses SARIF JSON to base64-gzip for GitHub Code Scanning API upload.
    /// </summary>
    internal static string CompressToBase64Gzip(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        using var ms = new MemoryStream();
        using (var gzip = new GZipStream(ms, CompressionLevel.Optimal))
            gzip.Write(bytes, 0, bytes.Length);
        return Convert.ToBase64String(ms.ToArray());
    }

    private void LogSummary(IReadOnlyList<Finding> findings)
    {
        if (findings.Count == 0)
        {
            logger.LogInformation("No security findings.");
            return;
        }

        var s = FindingSummary.From(findings);
        logger.LogInformation("Found {Total} issues ({High} HIGH, {Medium} MEDIUM, {Low} LOW)",
            s.Total, s.High, s.Medium, s.Low);
    }
}
