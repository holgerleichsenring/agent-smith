using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0400: finds the one thing `dotnet build` can be pointed at in a repository
/// that declared no build command — a single *.sln up to depth 2, else a single
/// *.csproj at the context workdir. An ambiguous or absent entry point is a named
/// RESOLUTION failure that says what was searched and where; a filename is never
/// invented. Extracted from VerifyPhaseHandler (p0504).
/// </summary>
public sealed class DotnetEntryPointDiscovery(
    ISandboxFileReaderFactory readerFactory,
    ILogger<DotnetEntryPointDiscovery> logger)
{
    private const int SearchDepth = 2;

    public async Task<IReadOnlyList<VerifyStage>> DiscoverAsync(
        string key, ISandbox sandbox, string workdir,
        List<string> resolutionFindings, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(resolutionFindings);
        var entries = await readerFactory.Create(sandbox).ListAsync(workdir, SearchDepth, ct);
        var relative = entries.Select(e => Relative(e, workdir)).Where(p => p.Length > 0).ToList();
        var solutions = relative
            .Where(p => p.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)).ToList();
        if (solutions.Count == 1) return [Build(key, solutions[0], workdir)];

        var projects = relative
            .Where(p => !p.Contains('/') && p.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (solutions.Count == 0 && projects.Count == 1) return [Build(key, projects[0], workdir)];

        resolutionFindings.Add(
            $"{(string.IsNullOrEmpty(key) ? "(default)" : key)}: searched for a single *.sln up to "
            + $"depth {SearchDepth} under {workdir} and a single *.csproj at {workdir} — found "
            + $"{solutions.Count} solution(s) and {projects.Count} top-level project(s). No command "
            + "was executed; declare ci.build_command / ci.test_command to make this repo verifiable.");
        return [];
    }

    private VerifyStage Build(string key, string entryPoint, string workdir)
    {
        logger.LogInformation("{Key}: discovered build entry point {EntryPoint}", key, entryPoint);
        return new VerifyStage("build", $"dotnet build \"{entryPoint}\"", workdir);
    }

    // Entries may come back absolute or relative to the listed root; normalize to
    // workdir-relative so the command runs against the path it was discovered at.
    private static string Relative(string entry, string workdir)
    {
        var normalized = entry.Replace('\\', '/');
        if (normalized.StartsWith(workdir + "/", StringComparison.Ordinal))
            normalized = normalized[(workdir.Length + 1)..];
        return normalized.TrimStart('/').Trim();
    }
}
