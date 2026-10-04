namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-02-3f06b: what a probe learned about one cited path. NOT A FILE is one answer for
/// a missing path and a directory alike — both lead to the same correction. NOT CHECKED is
/// an honest "could not tell", which is never reported as a problem.
/// </summary>
public abstract record EvidenceProbeResult
{
    /// <summary>A file of <paramref name="LineCount"/> lines, counted by <see cref="EvidenceLineCount"/>.</summary>
    public sealed record File(int LineCount) : EvidenceProbeResult;

    public sealed record NotAFile : EvidenceProbeResult;

    public sealed record NotChecked : EvidenceProbeResult;
}
