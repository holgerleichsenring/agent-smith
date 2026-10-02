using System.Text.Json;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Browser;

/// <summary>
/// 2026-10-01-283dh: the files of one directory tree in a repository's sandbox, relative to the
/// tree, held to <see cref="ReferenceSetLimits"/> before a byte is read — a tree that crosses a
/// bound is refused whole, naming the bound, never copied in part. Directories every search skips
/// (<see cref="GrepScope.ExcludedDirs"/>) and the uploaded websites a run carries are left out.
/// </summary>
public sealed class RepoTreeListing
{
    internal const int MaxDepth = 8;
    private const string WorkRoot = "/work";

    /// <summary>The tree's files relative to <paramref name="dir"/>, or why it is refused.</summary>
    public async Task<(IReadOnlyList<string>? Files, string? Refusal)> ListAsync(
        ISandbox sandbox, string dir, string page, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sandbox);
        var root = dir.Length == 0 ? WorkRoot : $"{WorkRoot}/{dir}";
        var result = await sandbox.RunStepAsync(new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.ListFiles,
            TimeoutSeconds: 30, Path: root, MaxDepth: MaxDepth), null, ct);
        if (result.ExitCode != 0 || result.OutputContent is null)
            return (null, $"the directory of '{page}' could not be listed: {result.ErrorMessage ?? "no listing"}");
        var (entries, cut) = Entries(result.OutputContent, root + "/");
        return Bounded(entries, cut, page);
    }

    private static (IReadOnlyList<string>?, string?) Bounded(
        IReadOnlyList<(string Path, long Size)> files, bool cut, string page)
    {
        if (cut || files.Count > ReferenceSetLimits.MaxFiles)
            return (null, $"the directory tree of '{page}' holds more than {ReferenceSetLimits.MaxFiles} files, the most one render copies");
        if (files.FirstOrDefault(f => f.Size > ReferenceSetLimits.MaxFileBytes) is { Path: not null } big)
            return (null, $"'{big.Path}' is {ReferenceSetLimits.Megabytes(big.Size)}, over the {ReferenceSetLimits.Megabytes(ReferenceSetLimits.MaxFileBytes)} one file may be");
        if (files.Sum(f => f.Size) is var total && total > ReferenceSetLimits.MaxSetBytes)
            return (null, $"the directory tree of '{page}' is {ReferenceSetLimits.Megabytes(total)}, over the {ReferenceSetLimits.Megabytes(ReferenceSetLimits.MaxSetBytes)} one render copies");
        if (files.FirstOrDefault(f => f.Path.Length > ReferenceSetLimits.MaxPathChars) is { Path: not null } deep)
            return (null, $"'{deep.Path}' is longer than {ReferenceSetLimits.MaxPathChars} characters");
        return ([.. files.Select(f => f.Path)], null);
    }

    // Files only, relative to the tree, and whether the listing was cut at its entry cap — which is
    // past the file bound by itself.
    private static (IReadOnlyList<(string Path, long Size)>, bool Cut) Entries(string json, string prefix)
    {
        using var doc = JsonDocument.Parse(json);
        var files = new List<(string, long)>();
        foreach (var entry in doc.RootElement.EnumerateArray())
        {
            var path = entry.TryGetProperty("path", out var p) ? p.GetString() ?? string.Empty : string.Empty;
            if (entry.TryGetProperty("is_directory", out var d) && d.GetBoolean()) continue;
            if (!path.StartsWith(prefix, StringComparison.Ordinal)) continue;
            var relative = path[prefix.Length..];
            if (relative.Split('/').SkipLast(1).Any(segment => GrepScope.ExcludedDirs.Contains(segment))
                || relative.StartsWith(ReferenceDirectory.Path + "/", StringComparison.Ordinal)) continue;
            files.Add((relative, entry.TryGetProperty("size_bytes", out var s) ? s.GetInt64() : 0));
        }
        return (files, doc.RootElement.GetArrayLength() >= SizeLimits.ListFilesMaxEntries);
    }
}
