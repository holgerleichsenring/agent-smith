namespace AgentSmith.Domain.Models;

/// <summary>
/// 2026-09-17-042eh: one thing a fresh instance found wrong in the phase's own diff, named
/// where it can be opened.
/// <para>
/// A finding is kept only when it rests on a READ the reviewer itself took of exactly this
/// repository and this path, with the line inside what that read returned — the rule
/// <see cref="CutFinding"/> states for a false premise, applied to code. A reviewer asked for
/// a path it never opened produces a plausible one, which is the fabrication the citation
/// exists to stop.
/// </para>
/// </summary>
/// <param name="Repository">The sandbox key the path belongs to, as the diff lists it.</param>
/// <param name="Path">The path inside that repository, as the phase diff spells it.</param>
/// <param name="Line">The line the finding is about, one-based, within the read's numbering.</param>
/// <param name="Rule">The principle or the spec passage the diff breaks, quoted.</param>
/// <param name="Why">One sentence: what the code does that the rule does not allow.</param>
/// <param name="Cites">The evidence id of the reviewer's own read of that file.</param>
/// <param name="Reverted">Set when the fix pass that was meant to close this was reverted;
/// the finding stands, and the record and the pull request say why.</param>
public sealed record PhaseFinding(
    string Repository,
    string Path,
    int Line,
    string Rule,
    string Why,
    string? Cites = null,
    string? Reverted = null);
