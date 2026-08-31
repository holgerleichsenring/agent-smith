namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// p0356/2026-08-31-7097: renders the probe's result as the master context's
/// "Sandbox toolchain" section — what each sandbox has, and what its declared verify
/// stages asked for and did not find. Pure transformation; nothing to render means no
/// section at all, never a fabricated inventory.
/// </summary>
internal static class ToolchainSection
{
    public static string? Render(
        IReadOnlyList<(string Name, string Capability)> lines, IReadOnlyList<string> findings)
    {
        if (lines.Count == 0 && findings.Count == 0) return null;
        var body = lines.Count switch
        {
            0 => string.Empty,
            1 => $"This sandbox has: {lines[0].Capability}\n",
            _ => string.Join("\n", lines.Select(l => $"- `{l.Name}` has: {l.Capability}")) + "\n",
        };
        return $"## Sandbox toolchain\n{body}{Findings(findings)}";
    }

    private static string Findings(IReadOnlyList<string> findings) =>
        findings.Count == 0
            ? string.Empty
            : string.Join("\n", findings.Select(f => $"- {f}")) + "\n";
}
