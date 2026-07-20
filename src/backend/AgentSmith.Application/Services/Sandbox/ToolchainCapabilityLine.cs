using System.Text.RegularExpressions;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// p0356: distills a toolchain probe's raw output ("git git version 2.43.0",
/// "bash GNU bash, version 5.2.21(1)-release ...") into the compact capability
/// line the master context carries ("git 2.43.0, bash 5.2.21"). Pure
/// transformation — no side effects.
/// </summary>
public static partial class ToolchainCapabilityLine
{
    private const int MaxTools = 12;

    [GeneratedRegex(@"\d+(\.\d+)+")]
    private static partial Regex VersionPattern();

    public static string? Distill(string? probeStdout)
    {
        if (string.IsNullOrWhiteSpace(probeStdout)) return null;
        var tools = probeStdout
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(DistillLine)
            .Where(t => t is not null)
            .Take(MaxTools)
            .ToList();
        return tools.Count == 0 ? null : string.Join(", ", tools);
    }

    /// <summary>Extracts the stdout section from a SandboxStepRunner labeled
    /// run_command result (headers, then "stdout:" and "stderr:" blocks).</summary>
    public static string? ExtractStdout(string? runCommandOutput)
    {
        if (string.IsNullOrEmpty(runCommandOutput)) return null;
        const string stdoutMarker = "stdout:\n";
        const string stderrMarker = "\nstderr:";
        var start = runCommandOutput.IndexOf(stdoutMarker, StringComparison.Ordinal);
        if (start < 0) return null;
        start += stdoutMarker.Length;
        var end = runCommandOutput.IndexOf(stderrMarker, start, StringComparison.Ordinal);
        return (end < 0 ? runCommandOutput[start..] : runCommandOutput[start..end]).Trim();
    }

    // "git git version 2.43.0" → "git 2.43.0"; a line with no parseable version
    // still reports the bare tool name (its presence IS the capability).
    private static string? DistillLine(string line)
    {
        var space = line.IndexOf(' ');
        var tool = space < 0 ? line : line[..space];
        if (tool.Length == 0) return null;
        var version = space < 0 ? null : VersionPattern().Match(line[space..]) is { Success: true } m ? m.Value : null;
        return version is null ? tool : $"{tool} {version}";
    }
}
