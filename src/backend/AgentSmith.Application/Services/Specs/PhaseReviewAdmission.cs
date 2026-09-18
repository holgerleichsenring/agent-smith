using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-17-042eh: decides which of a phase reviewer's findings the framework keeps.
/// <para>
/// One rule, in four parts, and all four are the same rule: a finding rests on a look the
/// FRAMEWORK minted, never on the model's own assurance. It must cite an id from the
/// reviewer's own evidence; that look must be a READ that exited 0 — a search says a string is
/// somewhere, which is not the same as having seen the file, and a read that could not run
/// proves nothing; it must be a read of exactly the repository and the path the finding names,
/// because a citation of a different file is a plausible path attached to a real id; and the
/// line must lie inside what that read returned, because a line past the bound is a number
/// nobody can check. The path must also be in the diff this review was shown, or the finding
/// is about work this phase did not do.
/// </para>
/// </summary>
internal sealed class PhaseReviewAdmission(ILogger logger)
{
    public IReadOnlyList<PhaseFinding> Admit(
        IReadOnlyList<PhaseFinding> answer, IReadOnlyList<PhaseDiff> diffs, DerivationLook? look)
    {
        ArgumentNullException.ThrowIfNull(answer);
        ArgumentNullException.ThrowIfNull(diffs);
        var reads = (look?.Evidence.Looks ?? [])
            .Where(l => l.Ran && l.ExitCode == 0 && l.Kind == EvidenceRecord.Read)
            .ToDictionary(l => l.Id, l => l, StringComparer.Ordinal);
        return [.. answer.Where(finding => Admitted(finding, diffs, reads))];
    }

    private bool Admitted(
        PhaseFinding finding, IReadOnlyList<PhaseDiff> diffs, IReadOnlyDictionary<string, EvidenceLook> reads)
    {
        if (Read(finding, reads) is not { } read)
            return Discard(finding, $"no read of its own that ran minted {finding.Cites ?? "nothing"}");
        if (!InTheDiff(finding, diffs))
            return Discard(finding, "that path is not in the diff this review was shown");
        if (finding.Line < 1 || finding.Line > read.LinesReturned)
            return Discard(finding, $"line {finding.Line} is past the {read.LinesReturned} line(s) [{read.Id}] returned");
        return true;
    }

    private static EvidenceLook? Read(PhaseFinding finding, IReadOnlyDictionary<string, EvidenceLook> reads) =>
        DerivationEvidence.CitationsIn(finding.Cites)
            .Select(id => reads.TryGetValue(id, out var read) ? read : null)
            .FirstOrDefault(read => read is not null && Same(read, finding));

    private static bool Same(EvidenceLook read, PhaseFinding finding) =>
        string.Equals(read.Repository, finding.Repository, StringComparison.Ordinal)
        && string.Equals(Normalize(read.Path), Normalize(finding.Path), StringComparison.Ordinal);

    private static bool InTheDiff(PhaseFinding finding, IReadOnlyList<PhaseDiff> diffs) =>
        diffs.Any(d => string.Equals(d.SandboxKey, finding.Repository, StringComparison.Ordinal)
            && d.Paths.Any(p => string.Equals(Normalize(p), Normalize(finding.Path), StringComparison.Ordinal)));

    // The read tool contains a path to its "./x" form; a diff spells it "x". One spelling, or
    // an honest finding on the file it really read is discarded for a leading dot.
    private static string Normalize(string? path)
    {
        var text = (path ?? string.Empty).Trim().Replace('\\', '/');
        while (text.StartsWith("./", StringComparison.Ordinal)) text = text[2..];
        return text.TrimStart('/');
    }

    private bool Discard(PhaseFinding finding, string why)
    {
        logger.LogWarning(
            "Phase review reported {Repo}/{Path}:{Line} and {Why} — discarding the finding",
            finding.Repository, finding.Path, finding.Line, why);
        return false;
    }
}
