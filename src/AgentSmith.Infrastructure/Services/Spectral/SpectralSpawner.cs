using System.Reflection;
using System.Text.Json;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Spectral;

/// <summary>
/// Runs a Spectral lint scan via IToolRunner.
/// Mounts swagger.json and .spectral.yaml, parses JSON output.
/// </summary>
public sealed class SpectralSpawner(
    IToolRunner toolRunner,
    ILogger<SpectralSpawner> logger) : ISpectralScanner
{
    public async Task<SpectralResult> LintAsync(
        string swaggerPath, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting Spectral lint: {SwaggerPath}", swaggerPath);

        var rulesetSource = GetRulesetPath();

        var inputFiles = new Dictionary<string, string>
        {
            ["swagger.json"] = File.ReadAllText(swaggerPath),
            [".spectral.yaml"] = File.ReadAllText(rulesetSource),
        };

        var arguments = new List<string>
        {
            "lint",
            "/input/swagger.json",
            "--ruleset", "/input/.spectral.yaml",
            "--format", "json",
            "--output", "/input/results.json",
        };

        var request = new ToolRunRequest(
            "spectral", arguments, inputFiles,
            OutputFileName: "results.json",
            TimeoutSeconds: 120);

        var result = await toolRunner.RunAsync(request, cancellationToken);

        var output = result.OutputFileContent ?? result.Stdout;
        var findings = ParseJsonOutput(output);

        var errorCount = findings.Count(f => f.Severity is "error");
        var warnCount = findings.Count(f => f.Severity is "warn");

        logger.LogInformation(
            "Spectral lint completed: {Count} findings ({Errors} errors, {Warnings} warnings) in {Duration}s",
            findings.Count, errorCount, warnCount, result.DurationSeconds);

        if (!string.IsNullOrWhiteSpace(result.Stderr) && result.ExitCode != 0)
            logger.LogWarning("Spectral stderr: {Stderr}", result.Stderr[..Math.Min(500, result.Stderr.Length)]);

        return new SpectralResult(findings, errorCount, warnCount, result.DurationSeconds);
    }

    internal static List<SpectralFinding> ParseJsonOutput(string output)
    {
        var findings = new List<SpectralFinding>();

        if (string.IsNullOrWhiteSpace(output))
            return findings;

        try
        {
            using var doc = JsonDocument.Parse(output);

            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return findings;

            foreach (var element in doc.RootElement.EnumerateArray())
            {
                var code = element.TryGetProperty("code", out var c)
                    ? c.GetString() ?? "" : "";
                var message = element.TryGetProperty("message", out var m)
                    ? m.GetString() ?? "" : "";
                var path = element.TryGetProperty("path", out var p) && p.ValueKind == JsonValueKind.Array
                    ? string.Join(".", p.EnumerateArray().Select(x => x.GetString()))
                    : "";
                var severity = MapSeverity(element.TryGetProperty("severity", out var s)
                    ? s.GetInt32() : -1);
                var line = element.TryGetProperty("range", out var range)
                    && range.TryGetProperty("start", out var start)
                    && start.TryGetProperty("line", out var ln)
                    ? ln.GetInt32() : 0;

                findings.Add(new SpectralFinding(code, message, path, severity, line));
            }
        }
        catch (JsonException)
        {
            // If output is not valid JSON, return empty list
        }

        return findings;
    }

    internal static string MapSeverity(int level) => level switch
    {
        0 => "error",
        1 => "warn",
        2 => "info",
        3 => "hint",
        _ => "unknown",
    };

    private static string GetRulesetPath()
    {
        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var rulesetPath = Path.Combine(assemblyDir, "config", "spectral.yaml");

        if (File.Exists(rulesetPath))
            return rulesetPath;

        var fallback = Path.Combine("config", "spectral.yaml");
        if (File.Exists(fallback))
            return fallback;

        throw new FileNotFoundException("Spectral ruleset not found. Expected at: " + rulesetPath);
    }
}
