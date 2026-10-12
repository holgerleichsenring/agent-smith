using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.SpecExecution;

/// <summary>
/// 2026-10-06-03c7e: writes an executed spec into <c>specs/done/</c> under its own stem — the
/// spec with its <c>outcome:</c> block, its markdown companion and its design mocks — and names
/// the planned files it replaces. It writes and never moves: <c>git mv</c> fails on a missing
/// target directory and on an untracked source.
/// </summary>
public sealed class SpecDoneFiles
{
    public async Task<SpecDoneMove> WriteAsync(
        ISandboxFileReader files, SpecPhase spec, string body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(spec);
        var written = new List<string>
        {
            await WriteAsync(files, SeriesPaths.Spec(SeriesPaths.Done, spec.FileStem), body, cancellationToken),
        };
        // 2026-10-08-e8b9f: an approved spec has no companion; its planned path is still removed.
        if (!string.IsNullOrWhiteSpace(spec.Markdown))
            written.Add(await WriteAsync(files, SeriesPaths.Companion(SeriesPaths.Done, spec.FileStem),
                spec.Markdown, cancellationToken));
        foreach (var mock in PlannedMocks(spec))
            if (await files.TryReadAsync(mock, cancellationToken) is { } html)
                written.Add(await WriteAsync(
                    files, $"{SeriesPaths.Done}/{SeriesPaths.FileName(mock)}", html, cancellationToken));
        return new SpecDoneMove(written, Planned(spec));
    }

    private static IReadOnlyList<string> Planned(SpecPhase spec) =>
        [SeriesPaths.Spec(SeriesPaths.Planned, spec.FileStem),
            SeriesPaths.Companion(SeriesPaths.Planned, spec.FileStem), .. PlannedMocks(spec)];

    private static IEnumerable<string> PlannedMocks(SpecPhase spec) =>
        (spec.MockPaths ?? []).Where(p => p.StartsWith(SeriesPaths.Planned + "/", StringComparison.Ordinal));

    private static async Task<string> WriteAsync(
        ISandboxFileReader files, string path, string content, CancellationToken ct)
    {
        await files.WriteAsync(path, content, ct);
        return path;
    }
}
