using System.Text.RegularExpressions;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services;

/// <summary>
/// 2026-10-03-cf20c: which catalog framework overlays one component's manifests declare.
/// <para>
/// The workdir is the model's — discovery chose the directory — but the match is not: whether a
/// file there names Spark is READ, against the signals the catalog declares, never judged. A
/// signal is a literal file read at the component root, or a glob over its direct children.
/// </para>
/// <para>
/// Any failure applies nothing for that overlay and is logged. A missing overlay costs rules;
/// a wrongly applied one is a wrong mandate the operator is asked to ratify.
/// </para>
/// </summary>
public sealed class FrameworkOverlayDetector(
    ISandboxFileReaderFactory readerFactory,
    ILogger<FrameworkOverlayDetector> logger)
{
    /// <summary>The slugs of the overlays whose signals match, in catalog order.</summary>
    public async Task<IReadOnlyList<string>> DetectAsync(
        ISandbox sandbox, string workdir, IReadOnlyList<FrameworkOverlay> overlays,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(overlays);
        if (overlays.Count == 0) return [];
        if (Escapes(workdir))
        {
            logger.LogWarning(
                "Component workdir '{Workdir}' is rooted or leaves the repository — no framework overlay is detected",
                workdir);
            return [];
        }

        var trimmed = (workdir ?? string.Empty).Trim().TrimEnd('/');
        var root = trimmed is "" or "." ? string.Empty : trimmed + "/";
        var reader = readerFactory.Create(sandbox);
        var applied = new List<string>();
        foreach (var overlay in overlays)
        {
            try
            {
                if (await AnyMatchesAsync(reader, root, overlay.Signals, cancellationToken))
                    applied.Add(overlay.Slug);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex,
                    "Framework overlay '{Slug}': reading the signals under '{Workdir}' failed — not applied",
                    overlay.Slug, workdir);
            }
        }
        return applied;
    }

    private static async Task<bool> AnyMatchesAsync(
        ISandboxFileReader reader, string root, IReadOnlyList<FrameworkOverlaySignal> signals,
        CancellationToken cancellationToken)
    {
        foreach (var signal in signals)
        {
            if (Escapes(signal.File)) continue;
            foreach (var path in await CandidatesAsync(reader, root + signal.File, cancellationToken))
            {
                var content = await reader.TryReadAsync(path, cancellationToken);
                if (content is not null
                    && (signal.Contains is null || content.Contains(signal.Contains, StringComparison.Ordinal)))
                    return true;
            }
        }
        return false;
    }

    // A literal is its own only candidate. A glob lists its directory (direct children only)
    // and keeps the names it matches; the listing's paths are absolute, so names are compared.
    private static async Task<IReadOnlyList<string>> CandidatesAsync(
        ISandboxFileReader reader, string path, CancellationToken cancellationToken)
    {
        var name = Path.GetFileName(path);
        if (name.IndexOfAny(['*', '?']) < 0) return [path];
        var directory = path[..^name.Length].TrimEnd('/');
        var pattern = new Regex(
            "^" + Regex.Escape(name).Replace(@"\*", "[^/]*").Replace(@"\?", "[^/]") + "$",
            RegexOptions.CultureInvariant);
        var entries = await reader.ListAsync(directory.Length == 0 ? "." : directory, maxDepth: 1, cancellationToken);
        return entries
            .Where(e => pattern.IsMatch(Path.GetFileName(e)))
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    private static bool Escapes(string? path) =>
        path is not null
        && (path.StartsWith('/') || path.StartsWith('\\') || Path.IsPathRooted(path)
            || path.Split('/', '\\').Contains(".."));
}
