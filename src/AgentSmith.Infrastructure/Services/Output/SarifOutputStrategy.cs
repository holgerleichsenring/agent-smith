// SARIF is the only boundary conversion in this codebase. SkillObservation → SARIF tuple.
// See p0123 decisions: pipeline carries SkillObservations universally; output strategies
// consume them directly except when an external spec (here: SARIF 2.1.0) demands a stricter
// shape, in which case the conversion lives at the strategy boundary.

using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
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
        var sarif = BuildSarifDocument(context.Observations);
        var json = sarif.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

        Directory.CreateDirectory(context.OutputDir);
        var outputPath = Path.Combine(context.OutputDir, "findings.sarif");
        await File.WriteAllTextAsync(outputPath, json, cancellationToken);
        logger.LogInformation("SARIF report written to {Path}", outputPath);

        LogSummary(context.Observations);
    }

    internal static JsonObject BuildSarifDocument(IReadOnlyList<SkillObservation> observations)
    {
        var rules = new JsonArray();
        var results = new JsonArray();
        var ruleIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var obs in observations)
        {
            var ruleKey = obs.Category ?? ExtractTitle(obs.Description);
            var ruleId = GetOrCreateRuleId(ruleKey, ruleIndex, rules);
            results.Add(BuildResult(obs, ruleId));
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
        string key, Dictionary<string, int> ruleIndex, JsonArray rules)
    {
        if (ruleIndex.TryGetValue(key, out var existing))
            return $"AS{existing:D3}";

        var index = ruleIndex.Count + 1;
        ruleIndex[key] = index;
        var ruleId = $"AS{index:D3}";

        rules.Add(new JsonObject
        {
            ["id"] = ruleId,
            ["shortDescription"] = new JsonObject { ["text"] = key }
        });

        return ruleId;
    }

    private static JsonObject BuildResult(SkillObservation obs, string ruleId)
    {
        var region = new JsonObject { ["startLine"] = Math.Max(1, obs.StartLine) };
        if (obs.EndLine.HasValue)
            region["endLine"] = obs.EndLine.Value;

        var location = new JsonObject
        {
            ["physicalLocation"] = new JsonObject
            {
                ["artifactLocation"] = new JsonObject { ["uri"] = obs.File ?? "" },
                ["region"] = region
            }
        };

        if (obs.ApiPath is not null || obs.SchemaName is not null)
        {
            var logicalLocations = new JsonArray();
            if (obs.ApiPath is not null)
                logicalLocations.Add(new JsonObject { ["name"] = obs.ApiPath, ["kind"] = "endpoint" });
            if (obs.SchemaName is not null)
                logicalLocations.Add(new JsonObject { ["name"] = obs.SchemaName, ["kind"] = "type" });
            location["logicalLocations"] = logicalLocations;
        }

        var result = new JsonObject
        {
            ["ruleId"] = ruleId,
            ["level"] = MapSeverity(obs.Severity),
            ["message"] = new JsonObject { ["text"] = obs.Description },
            ["locations"] = new JsonArray { location },
            ["properties"] = new JsonObject
            {
                ["evidence_mode"] = MapEvidence(obs.EvidenceMode),
                ["confidence"] = obs.Confidence,
                ["concern"] = obs.Concern.ToString()
            }
        };

        if (obs.ReviewStatus == "false_positive")
            result["suppressions"] = new JsonArray
            {
                new JsonObject { ["kind"] = "external", ["status"] = "accepted" }
            };

        return result;
    }

    private static string ExtractTitle(string description)
    {
        var firstLine = description.Split('\n')[0].Trim();
        return firstLine.Length > 80 ? firstLine[..80] : firstLine;
    }

    private static string MapEvidence(EvidenceMode mode) => mode switch
    {
        EvidenceMode.Confirmed => "confirmed",
        EvidenceMode.AnalyzedFromSource => "analyzed_from_source",
        _ => "potential"
    };

    internal static string MapSeverity(ObservationSeverity severity) => severity switch
    {
        ObservationSeverity.High => "error",
        ObservationSeverity.Medium => "warning",
        ObservationSeverity.Low => "note",
        _ => "none"
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

    private void LogSummary(IReadOnlyList<SkillObservation> observations)
    {
        if (observations.Count == 0)
        {
            logger.LogInformation("No security findings.");
            return;
        }

        var s = ObservationSummary.From(observations);
        logger.LogInformation("Found {Total} issues ({High} HIGH, {Medium} MEDIUM, {Low} LOW, {Info} INFO)",
            s.Total, s.High, s.Medium, s.Low, s.Info);
    }
}
