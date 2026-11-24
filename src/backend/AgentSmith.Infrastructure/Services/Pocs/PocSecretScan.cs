using System.Security.Cryptography;
using System.Text;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Services.Security;

namespace AgentSmith.Infrastructure.Services.Pocs;

/// <summary>
/// 2026-11-23-933dc: the in-process pattern scan of a PoC revision. Every text file is read in full, line by line, with
/// every catalog pattern and every match on a line; no ignore rule of the upload applies. A 'secrets' pattern that times
/// out fails the scan; another catalog's timeout is a note, because its findings can only ever be notes. Binary files
/// are listed in one note.
/// </summary>
public sealed class PocSecretScan(
    PocPatternSet patternSet, PatternLineMatcher lineMatcher, PocLineMasker masker, PocTextFiles text) : IPocSecretScan
{
    public PocSecretScanResult Scan(IReadOnlyList<ReferenceSetFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var (patterns, failure) = patternSet.Load();
        if (failure is not null) return PocSecretScanResult.Failed(failure);
        var run = new PocScanRun();
        var binary = new List<string>();
        foreach (var file in files)
        {
            if (text.TextOf(file) is { } content) ScanFile(run, file.Path, content, patterns);
            else binary.Add(file.Path);
            if (run.Failure is not null) return PocSecretScanResult.Failed(run.Failure);
        }
        if (binary.Count > 0) run.Notes.Add(BinaryNote(binary));
        return new PocSecretScanResult(run.Hits, run.Notes);
    }

    private void ScanFile(PocScanRun run, string path, string content, IReadOnlyList<CompiledPattern> patterns)
    {
        var lines = content.Split('\n');
        for (var index = 0; index < lines.Length && run.Failure is null; index++)
        {
            var line = lines[index].TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line)) continue;
            var matches = lineMatcher.Match(line, patterns, timedOut => run.TimedOut(timedOut.Definition, path));
            foreach (var match in matches)
                run.Hits.Add(Hit(match, path, index + 1, masker.Snippet(line, matches, match, path, index + 1)));
        }
    }

    private static PocPatternHit Hit(PatternLineMatch match, string path, int line, string snippet)
    {
        var d = match.Pattern.Definition;
        return new PocPatternHit(d.Id, d.Category, d.Severity, d.Confidence, path, line, d.Title, d.Description, snippet,
            d.RevocationUrl);
    }

    private static PocScanFinding BinaryNote(IReadOnlyList<string> paths) =>
        new("note:" + Hash("binary|" + string.Join('|', paths)), PocScanFindingKinds.Note, "binary-not-scanned",
            paths[0], null, $"{paths.Count} binary file(s) were not pattern-scanned",
            "Not pattern-scanned because they are not text: " + string.Join(", ", paths), null, null, []);

    internal static string Hash(string text) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];
}
