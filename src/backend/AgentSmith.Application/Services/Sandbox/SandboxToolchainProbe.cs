using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// p0356: one run_command per sandbox at master start reports what the
/// toolchain image actually provides; the distilled line enters the master
/// context as a capability statement ("This sandbox has: ..."). Probe failures
/// simply omit the section — never a fabricated inventory.
/// </summary>
public sealed class SandboxToolchainProbe(ILogger<SandboxToolchainProbe> logger) : ISandboxToolchainProbe
{
    private const int ProbeTimeoutSeconds = 45;

    // One POSIX-sh pass over the toolchains a coding master can mechanize with.
    // Each available tool reports its own first version line; absent tools stay
    // silent. `true` keeps the exit code green regardless of the last probe.
    internal const string ProbeCommand =
        "p() { command -v \"$1\" >/dev/null 2>&1 && echo \"$1 $($2 2>&1 | head -n 1)\"; }; "
        + "p bash 'bash --version'; p git 'git --version'; p dotnet 'dotnet --version'; "
        + "p node 'node --version'; p npm 'npm --version'; p python3 'python3 --version'; "
        + "p java 'java --version'; p go 'go version'; p cargo 'cargo --version'; "
        + "p make 'make --version'; true";

    public async Task<string?> ProbeAsync(
        IReadOnlyDictionary<string, ISandbox> sandboxes,
        IReadOnlyDictionary<string, string>? keyToRepo,
        CancellationToken cancellationToken)
    {
        var lines = new List<(string Name, string Capability)>();
        foreach (var (key, sandbox) in sandboxes)
        {
            var name = keyToRepo is not null && keyToRepo.TryGetValue(key, out var repo)
                && !string.IsNullOrEmpty(repo) ? repo : key;
            if (lines.Any(l => l.Name == name)) continue;
            var capability = await ProbeOneAsync(sandbox, cancellationToken);
            if (capability is not null) lines.Add((name, capability));
        }
        return Render(lines);
    }

    private async Task<string?> ProbeOneAsync(ISandbox sandbox, CancellationToken ct)
    {
        try
        {
            var output = await new SandboxStepRunner(sandbox).RunAsync(ProbeCommand, ProbeTimeoutSeconds, ct);
            return ToolchainCapabilityLine.Distill(ToolchainCapabilityLine.ExtractStdout(output));
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogDebug(ex, "Toolchain probe failed — omitting the sandbox-toolchain section");
            return null;
        }
    }

    private static string? Render(IReadOnlyList<(string Name, string Capability)> lines)
    {
        if (lines.Count == 0) return null;
        if (lines.Count == 1)
            return $"## Sandbox toolchain\nThis sandbox has: {lines[0].Capability}\n";
        var bullets = string.Join("\n", lines.Select(l => $"- `{l.Name}` has: {l.Capability}"));
        return $"## Sandbox toolchain\n{bullets}\n";
    }
}
