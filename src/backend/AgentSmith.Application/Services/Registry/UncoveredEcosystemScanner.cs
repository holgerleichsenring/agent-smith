using AgentSmith.Application.Models.Registry;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Registry;

/// <summary>
/// After the NuGet/npm fast-paths run, finds configured registries whose host is
/// referenced by SOME other manifest/config file in the repo yet was not staged.
/// Ecosystem-agnostic on purpose: it looks for the host string in candidate text
/// files rather than parsing any manager's format — the LLM fallback owns the
/// actual per-ecosystem staging. The file scan is a cost bound (skip the LLM when
/// nothing references an unstaged host), never authoritative staging logic.
/// </summary>
public sealed class UncoveredEcosystemScanner(ILogger<UncoveredEcosystemScanner> logger)
{
    private const int MaxFilesScanned = 80;
    private const int MaxFileChars = 262_144;

    private static readonly string[] FastPathSuffixes =
        ["/nuget.config", "/.npmrc"];

    private static readonly string[] BinaryExtensions =
        [".png", ".jpg", ".jpeg", ".gif", ".ico", ".pdf", ".zip", ".gz", ".tar",
         ".dll", ".exe", ".so", ".dylib", ".class", ".jar", ".nupkg", ".bin", ".woff", ".woff2"];

    public async Task<IReadOnlyList<UncoveredRegistry>> ScanAsync(
        IReadOnlyList<string> listing, ISet<string> coveredHosts,
        IReadOnlyList<RegistryConfig> registries, ISandboxFileReader reader,
        string repoKey, CancellationToken ct)
    {
        var candidates = registries
            .Where(r => !string.IsNullOrEmpty(r.Token) && !coveredHosts.Contains(r.Host))
            .ToList();
        if (candidates.Count == 0) return Array.Empty<UncoveredRegistry>();

        var found = new Dictionary<string, UncoveredRegistry>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in ScannableFiles(listing))
        {
            var content = await reader.TryReadAsync(path, ct);
            if (string.IsNullOrEmpty(content) || content.Length > MaxFileChars) continue;
            MatchHosts(candidates, content, path, found);
            if (found.Count == candidates.Count) break;
        }

        if (found.Count > 0)
            logger.LogInformation(
                "{Repo}: {Count} registry host(s) referenced by an unstaged ecosystem: [{Hosts}] — handing to the LLM fallback.",
                repoKey, found.Count, string.Join(", ", found.Keys));
        return found.Values.ToList();
    }

    private static void MatchHosts(
        IReadOnlyList<RegistryConfig> candidates, string content, string path,
        Dictionary<string, UncoveredRegistry> found)
    {
        foreach (var reg in candidates)
        {
            if (found.ContainsKey(reg.Host)) continue;
            if (ReferencesHost(content, reg.Host))
                found[reg.Host] = new UncoveredRegistry(reg, path);
        }
    }

    // Host-boundary aware: 'pkgs.dev.azure.com' must not match inside
    // 'evilpkgs.dev.azure.com' (preceding label char) nor be a prefix of a
    // longer host ('...azure.com.evil'). A leading dot IS allowed — that is a
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
