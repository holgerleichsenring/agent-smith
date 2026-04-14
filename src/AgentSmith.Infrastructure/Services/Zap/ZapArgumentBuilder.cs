using System.Text;
using System.Text.Json;

namespace AgentSmith.Infrastructure.Services.Zap;

/// <summary>
/// Builds command-line arguments for ZAP scan types.
/// </summary>
internal static class ZapArgumentBuilder
{
    internal static List<string> BuildArguments(
        string scanType, string dockerTarget, string? swaggerPath,
        Dictionary<string, string> inputFiles)
    {
        return scanType.ToLowerInvariant() switch
        {
            "full-scan" => BuildFullScanArgs(dockerTarget),
            "api-scan" => BuildApiScanArgs(dockerTarget, swaggerPath, inputFiles),
            _ => BuildBaselineArgs(dockerTarget),
        };
    }

    private static List<string> BuildBaselineArgs(string target)
    {
        return ["zap-baseline.py", "-t", target, "-J", "{work}/zap-report.json", "-l", "WARN"];
    }

    private static List<string> BuildFullScanArgs(string target)
    {
        return ["zap-full-scan.py", "-t", target, "-J", "{work}/zap-report.json", "-l", "WARN"];
    }

    private static List<string> BuildApiScanArgs(
        string target, string? swaggerPath, Dictionary<string, string> inputFiles)
    {
        if (!string.IsNullOrWhiteSpace(swaggerPath) && File.Exists(swaggerPath))
        {
            var specContent = InjectServerUrl(File.ReadAllText(swaggerPath), target);
            inputFiles["swagger.json"] = specContent;
            return ["zap-api-scan.py", "-t", "{work}/swagger.json", "-f", "openapi", "-J", "{work}/zap-report.json", "-l", "WARN"];
        }

        // Fallback: use target URL directly for api-scan
        return ["zap-api-scan.py", "-t", target, "-f", "openapi", "-J", "{work}/zap-report.json", "-l", "WARN"];
    }

    /// <summary>
    /// Ensures the OpenAPI spec has a servers entry with the target URL.
    /// ZAP fails with "Unable to obtain any server URL" if servers is missing or relative ("/").
    /// </summary>
    internal static string InjectServerUrl(string specJson, string targetUrl)
    {
        try
        {
            using var doc = JsonDocument.Parse(specJson);
            var root = doc.RootElement;

            // Check if servers array exists and has a valid absolute URL
            if (root.TryGetProperty("servers", out var servers) && servers.ValueKind == JsonValueKind.Array)
            {
                foreach (var server in servers.EnumerateArray())
                {
                    if (server.TryGetProperty("url", out var url)
                        && url.GetString() is { } urlStr
                        && urlStr.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        return specJson; // Already has a valid absolute server URL
                    }
                }
            }

            // Inject the target URL as the server
            using var ms = new MemoryStream();
            using (var writer = new Utf8JsonWriter(ms))
            {
                writer.WriteStartObject();

                // Write servers first
                writer.WritePropertyName("servers");
                writer.WriteStartArray();
                writer.WriteStartObject();
                writer.WriteString("url", targetUrl);
                writer.WriteEndObject();
                writer.WriteEndArray();

                // Copy all existing properties
                foreach (var prop in root.EnumerateObject())
                {
                    if (prop.Name == "servers") continue; // Skip original servers
                    prop.WriteTo(writer);
                }

                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(ms.ToArray());
        }
        catch
        {
            return specJson; // If anything goes wrong, return original
        }
    }
}
