using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0167a: fetches the PR's per-file patches via IPrDiffProvider and parses
/// them into structured hunks with line numbers. Publishes ContextKeys.PrDiff
/// (PrDiffAnalysis) for the review skills, plus the platform's authoritative
/// PrHead / PrBase shas (overwriting the webhook's provisional values). Files
/// without a textual patch (binaries) become metadata-only entries.
/// </summary>
public sealed class AnalyzePrDiffHandler(
    IPrDiffProviderFactory prDiffProviderFactory,
    IUnifiedDiffParser diffParser,
    ILogger<AnalyzePrDiffHandler> logger) : ICommandHandler<AnalyzePrDiffContext>
{
    public async Task<CommandResult> ExecuteAsync(
        AnalyzePrDiffContext context, CancellationToken cancellationToken)
    {
        var provider = prDiffProviderFactory.Create(context.Repo);
        var diff = await provider.GetDiffAsync(context.PrNumber, cancellationToken);

        var files = diff.Files.Select(ParseFile).ToList();
        var analysis = new PrDiffAnalysis(diff.BaseSha, diff.HeadSha, files);

        context.Pipeline.Set(ContextKeys.PrDiff, analysis);
        context.Pipeline.Set(ContextKeys.PrHead, diff.HeadSha);
        context.Pipeline.Set(ContextKeys.PrBase, diff.BaseSha);

        var summary = Summarize(context.PrNumber, analysis);
        logger.LogInformation("{Summary}", summary);
        return CommandResult.Ok(summary);
    }

    private PrDiffFile ParseFile(ChangedFile file)
    {
        var isBinary = string.IsNullOrEmpty(file.Patch);
        return new PrDiffFile(
            file.Path,
            MapKind(file.Kind),
            isBinary,
            isBinary ? [] : diffParser.Parse(file.Patch));
    }

    private static PrFileChangeKind MapKind(ChangeKind kind) => kind switch
    {
        ChangeKind.Added => PrFileChangeKind.Added,
        ChangeKind.Deleted => PrFileChangeKind.Deleted,
        _ => PrFileChangeKind.Modified,
    };

    private static string Summarize(string prNumber, PrDiffAnalysis analysis)
    {
        var lines = analysis.Files.SelectMany(f => f.Hunks).SelectMany(h => h.Lines).ToList();
        var added = lines.Count(l => l.Kind == PrDiffLineKind.Added);
        var removed = lines.Count(l => l.Kind == PrDiffLineKind.Removed);
        var binary = analysis.Files.Count(f => f.IsBinary);
        return $"Analyzed PR #{prNumber} diff: {analysis.Files.Count} file(s), "
            + $"+{added}/-{removed} line(s), {binary} binary "
            + $"({ShortSha(analysis.BaseSha)}..{ShortSha(analysis.HeadSha)})";
    }

    private static string ShortSha(string sha) =>
        string.IsNullOrEmpty(sha) ? "unknown" : sha[..Math.Min(8, sha.Length)];
}
