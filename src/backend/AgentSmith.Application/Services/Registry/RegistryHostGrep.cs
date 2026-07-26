using AgentSmith.Application.Models.Registry;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Registry;

/// <summary>
/// p0375 detection: a bounded, LLM-free, verbatim HOST-GREP over the checked-out
/// working tree. A configured registry host the fast-paths did not stage is
/// "uncovered" when it appears verbatim in some text file of the repo; the
/// matching file paths ride along as context for the LLM stager. Deliberately
/// NO manifest parsing and NO ecosystem inference — a per-manager detector
/// would reintroduce exactly the bespoke-parser problem this phase abolishes.
/// </summary>
public sealed class RegistryHostGrep(ILogger<RegistryHostGrep> logger)
{
    private const int MaxFilesScanned = 80;
    private const int MaxFileChars = 262_144;
    private const int MaxPathsPerHost = 5;

    private static readonly string[] FastPathSuffixes =
        ["/nuget.config", "/.npmrc"];

    private static readonly string[] BinaryExtensions =
        [".png", ".jpg", ".jpeg", ".gif", ".ico", ".pdf", ".zip", ".gz", ".tar",
         ".dll", ".exe", ".so", ".dylib", ".class", ".jar", ".nupkg", ".bin", ".woff", ".woff2"];

    public async Task<IReadOnlyList<UncoveredRegistry>> FindUncoveredAsync(
        IReadOnlyList<string> listing, ISet<string> coveredHosts,
        IReadOnlyList<RegistryConfig> registries, ISandboxFileReader reader,
        string repoKey, CancellationToken ct)
    {
        var candidates = registries
            .Where(r => !string.IsNullOrEmpty(r.Token) && !coveredHosts.Contains(r.Host))
            .ToList();
        if (candidates.Count == 0) return Array.Empty<UncoveredRegistry>();

        var matches = candidates.ToDictionary(
            r => r.Host, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);
        foreach (var path in ScannableFiles(listing))
        {
            var content = await reader.TryReadAsync(path, ct);
            if (string.IsNullOrEmpty(content) || content.Length > MaxFileChars) continue;
            CollectMatches(candidates, content, path, matches);
        }

        return BuildResult(candidates, matches, repoKey);
    }

    private IReadOnlyList<UncoveredRegistry> BuildResult(
        IReadOnlyList<RegistryConfig> candidates,
        Dictionary<string, List<string>> matches, string repoKey)
    {
        var uncovered = candidates
            .Where(r => matches[r.Host].Count > 0)
            .Select(r => new UncoveredRegistry(r, matches[r.Host]))
            .ToList();
        if (uncovered.Count > 0)
            logger.LogInformation(
                "{Repo}: {Count} registry host(s) referenced by the working tree but not staged: [{Hosts}].",
                repoKey, uncovered.Count, string.Join(", ", uncovered.Select(u => u.Registry.Host)));
        return uncovered;
    }

    private static void CollectMatches(
        IReadOnlyList<RegistryConfig> candidates, string content, string path,
        Dictionary<string, List<string>> matches)
    {
        foreach (var reg in candidates)
        {
            var paths = matches[reg.Host];
            if (paths.Count >= MaxPathsPerHost) continue;
            if (ReferencesHost(content, reg.Host)) paths.Add(path);
        }
    }

    // Host-boundary aware verbatim match: 'pkgs.dev.azure.com' must not match
    // inside 'evilpkgs.dev.azure.com' (preceding label char) nor be a prefix of
    // a longer host ('...azure.com.evil'). A leading dot IS allowed — that is a
    // legitimate subdomain, matching the fast-path's host-suffix semantics.
    private static bool ReferencesHost(string content, string host)
    {
        var idx = 0;
        while ((idx = content.IndexOf(host, idx, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            var before = idx == 0 ? '\0' : content[idx - 1];
            var afterIdx = idx + host.Length;
            var after = afterIdx >= content.Length ? '\0' : content[afterIdx];
            if (!IsLabelChar(before) && !IsContinuation(after)) return true;
            idx = afterIdx;
        }
        return false;
    }

    private static bool IsLabelChar(char c) => char.IsLetterOrDigit(c) || c == '-';
    private static bool IsContinuation(char c) => char.IsLetterOrDigit(c) || c == '-' || c == '.';

    private static IEnumerable<string> ScannableFiles(IReadOnlyList<string> listing) =>
        listing
            .Where(p => !IsFastPathFile(p) && !IsBinary(p))
            .Take(MaxFilesScanned);

    private static bool IsFastPathFile(string path) =>
        FastPathSuffixes.Any(s => path.EndsWith(s, StringComparison.OrdinalIgnoreCase));

    private static bool IsBinary(string path) =>
        BinaryExtensions.Any(e => path.EndsWith(e, StringComparison.OrdinalIgnoreCase));
}
