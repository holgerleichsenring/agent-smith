using System.Globalization;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Parses security snapshot YAML files into SecurityRunSnapshot records.
/// </summary>
internal static class SnapshotYamlParser
{
    internal static async Task<List<SecurityRunSnapshot>> LoadSnapshotsAsync(
        ISandboxFileReader reader, string securityDir, CancellationToken cancellationToken)
    {
        var snapshots = new List<SecurityRunSnapshot>();
        var entries = await reader.ListAsync(securityDir, maxDepth: 1, cancellationToken);

        foreach (var file in entries.Where(e => e.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                var yaml = await reader.TryReadAsync(file, cancellationToken);
                if (yaml is null) continue;
                var snapshot = ParseSnapshotYaml(yaml);
                if (snapshot is not null)
                    snapshots.Add(snapshot);
            }
            catch
            {
                // Skip malformed snapshot files
            }
        }

        return snapshots;
    }

    internal static SecurityRunSnapshot? ParseSnapshotYaml(string yaml)
    {
        var lines = yaml.Split('\n');
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var scanTypes = new List<string>();
        var topCategories = new List<string>();
        string? currentList = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');

            if (line.StartsWith("  - ") && currentList is not null)
            {
                var item = line[4..].Trim();
                if (currentList == "scan_types") scanTypes.Add(item);
                else if (currentList == "top_categories") topCategories.Add(item);
                continue;
            }

            currentList = null;
            var colonIdx = line.IndexOf(':');
            if (colonIdx <= 0) continue;

            var key = line[..colonIdx].Trim();
            var value = line[(colonIdx + 1)..].Trim();

            if (string.IsNullOrEmpty(value))
            {
                if (key is "scan_types" or "top_categories")
                    currentList = key;
                continue;
            }

            values[key] = value;
        }

        if (!values.ContainsKey("date"))
            return null;

        return new SecurityRunSnapshot(
            Date: DateTimeOffset.TryParse(values.GetValueOrDefault("date", ""), CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                ? date : DateTimeOffset.MinValue,
            Branch: values.GetValueOrDefault("branch", "unknown"),
            FindingsCritical: ParseInt(values, "findings_critical"),
            FindingsHigh: ParseInt(values, "findings_high"),
            FindingsMedium: ParseInt(values, "findings_medium"),
            FindingsRetained: ParseInt(values, "findings_retained"),
            FindingsAutoFixed: ParseInt(values, "findings_auto_fixed"),
            ScanTypes: scanTypes,
            NewSinceLast: ParseInt(values, "new_since_last"),
            ResolvedSinceLast: ParseInt(values, "resolved_since_last"),
            TopCategories: topCategories,
            CostUsd: ParseDecimal(values, "cost_usd"));
    }

    internal static int ParseInt(Dictionary<string, string> values, string key)
        => values.TryGetValue(key, out var v) && int.TryParse(v, out var n) ? n : 0;

    internal static decimal ParseDecimal(Dictionary<string, string> values, string key)
        => values.TryGetValue(key, out var v) && decimal.TryParse(v, CultureInfo.InvariantCulture, out var n) ? n : 0m;
}
