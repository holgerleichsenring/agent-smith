namespace AgentSmith.Application.Services.Scans;

/// <summary>
/// p0429: the lines around a cited one, numbered, so a refuter is shown the code rather
/// than the headline.
/// <para>
/// Numbered because the refutation has to quote a line back and the framework checks the
/// quote against what was shown; a window without numbers makes "which line" a matter of
/// counting. Whole files are not sent: a refutation is about one line's neighbourhood,
/// and forty findings' worth of whole files is a context nobody can pay for.
/// </para>
/// <para>
/// 2026-09-01-85b2: a line the file does not have yields NOTHING. It used to yield a
/// sentence saying the line does not exist, which is a non-null candidate — the refuter
/// was shown that sentence, refuted on it, and satisfied the quote check by quoting the
/// sentence verbatim. A true finding downgraded with a clean audit trail.
/// </para>
/// </summary>
public sealed class CitedCodeWindow
{
    private const int LinesEitherSide = 12;

    /// <summary>The numbered neighbourhood of <paramref name="line"/>, or null when the
    /// file does not have that line and there is no evidence to show.</summary>
    public string? Around(string? content, int line)
    {
        if (string.IsNullOrEmpty(content) || line < 1) return null;
        var lines = content.Replace("\r\n", "\n").Split('\n');
        if (line > lines.Length) return null;
        var first = Math.Max(1, line - LinesEitherSide);
        var last = Math.Min(lines.Length, line + LinesEitherSide);
        return string.Join("\n", Enumerable.Range(first, last - first + 1)
            .Select(n => $"{n,6}: {lines[n - 1]}"));
    }
}
