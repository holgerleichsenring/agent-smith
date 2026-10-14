using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-17-042eh: decides which of a spec reviewer's findings the framework keeps.
/// <para>
/// One rule, in four parts, and all four are the same rule: a finding rests on a look the
/// FRAMEWORK minted, never on the model's own assurance. It must cite an id from the
/// reviewer's own evidence; that look must be a READ that exited 0 — a search says a string is
/// somewhere, which is not the same as having seen the file, and a read that could not run
/// proves nothing; it must be a read of exactly the repository and the path the finding names,
/// because a citation of a different file is a plausible path attached to a real id; and the
/// line must lie inside what that read returned, because a line past the bound is a number
/// nobody can check. The path must also be in the diff this review was shown, or the finding
/// is about work this spec did not do. 2026-10-13-8762e: the read matching is <see cref="OwnReadRule"/>.
/// </para>
/// </summary>
internal sealed class SpecReviewAdmission(ILogger logger)
{
    public IReadOnlyList<SpecFinding> Admit(
        IReadOnlyList<SpecFinding> answer, IReadOnlyList<SpecDiff> diffs, DerivationLook? look)
    {
        ArgumentNullException.ThrowIfNull(answer);
        ArgumentNullException.ThrowIfNull(diffs);
        var reads = OwnReadRule.Reads(look);
        return [.. answer.Where(finding => Admitted(finding, diffs, reads))];
    }

    private bool Admitted(
        SpecFinding finding, IReadOnlyList<SpecDiff> diffs, IReadOnlyDictionary<string, EvidenceLook> reads)
    {
        if (OwnReadRule.Read(finding.Cites, finding.Repository, finding.Path, reads) is not { } read)
            return Discard(finding, $"no read of its own that ran minted {finding.Cites ?? "nothing"}");
        if (!InTheDiff(finding, diffs))
            return Discard(finding, "that path is not in the diff this review was shown");
        if (!OwnReadRule.Within(finding.Line, read))
            return Discard(finding, $"line {finding.Line} is past the {read.LinesReturned} line(s) [{read.Id}] returned");
        return true;
    }

    private static bool InTheDiff(SpecFinding finding, IReadOnlyList<SpecDiff> diffs) =>
        diffs.Any(d => string.Equals(d.SandboxKey, finding.Repository, StringComparison.Ordinal)
            && d.Paths.Any(p => string.Equals(
                OwnReadRule.Normalize(p), OwnReadRule.Normalize(finding.Path), StringComparison.Ordinal)));

    private bool Discard(SpecFinding finding, string why)
    {
        logger.LogWarning(
            "Spec review reported {Repo}/{Path}:{Line} and {Why} — discarding the finding",
            finding.Repository, finding.Path, finding.Line, why);
        return false;
    }
}
