using System.Text.RegularExpressions;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services;

/// <summary>
/// Unified-diff format parser. Splits a patch into hunks at each
/// <c>@@ -a,b +c,d @@</c> header and tracks old/new line counters per line:
/// '+' advances the new counter, '-' the old, context lines both.
/// Anything before the first hunk header (git's <c>---</c>/<c>+++</c>
/// preamble) and <c>\ No newline at end of file</c> markers are skipped.
/// </summary>
public sealed class UnifiedDiffParser : IUnifiedDiffParser
{
    private static readonly Regex HunkHeader = new(
        @"^@@ -(?<oldStart>\d+)(?:,(?<oldCount>\d+))? \+(?<newStart>\d+)(?:,(?<newCount>\d+))? @@",
        RegexOptions.Compiled);

    public IReadOnlyList<PrHunk> Parse(string patch)
    {
        if (string.IsNullOrEmpty(patch)) return [];

        var hunks = new List<PrHunk>();
        HunkAccumulator? current = null;
        foreach (var line in patch.Split('\n'))
        {
            var header = HunkHeader.Match(line);
            if (header.Success)
            {
                AppendCompleted(hunks, current);
                current = new HunkAccumulator(header);
            }
            else
            {
                current?.Add(TrimTrailingCarriageReturn(line));
            }
        }
        AppendCompleted(hunks, current);
        return hunks;
    }

    private static void AppendCompleted(List<PrHunk> hunks, HunkAccumulator? current)
    {
        if (current is not null) hunks.Add(current.Build());
    }

    private static string TrimTrailingCarriageReturn(string line) =>
        line.EndsWith('\r') ? line[..^1] : line;

    private sealed class HunkAccumulator
    {
        private readonly int _oldStart;
        private readonly int _oldCount;
        private readonly int _newStart;
        private readonly int _newCount;
        private readonly List<PrDiffLine> _lines = [];
        private int _oldLine;
        private int _newLine;

        public HunkAccumulator(Match header)
        {
            _oldStart = int.Parse(header.Groups["oldStart"].Value);
            _oldCount = header.Groups["oldCount"].Success ? int.Parse(header.Groups["oldCount"].Value) : 1;
            _newStart = int.Parse(header.Groups["newStart"].Value);
            _newCount = header.Groups["newCount"].Success ? int.Parse(header.Groups["newCount"].Value) : 1;
            _oldLine = _oldStart;
            _newLine = _newStart;
        }

        public void Add(string line)
        {
            if (line.StartsWith('\\')) return; // "\ No newline at end of file"
            if (line.Length == 0 && IsExhausted) return; // trailing newline artifact
            switch (line.Length > 0 ? line[0] : ' ')
            {
                case '+':
                    _lines.Add(new PrDiffLine(PrDiffLineKind.Added, null, _newLine++, Content(line)));
                    break;
                case '-':
                    _lines.Add(new PrDiffLine(PrDiffLineKind.Removed, _oldLine++, null, Content(line)));
                    break;
                default:
                    // ' '-prefixed context; an empty string is a context line whose
                    // trailing space the platform API trimmed away.
                    _lines.Add(new PrDiffLine(PrDiffLineKind.Context, _oldLine++, _newLine++, Content(line)));
                    break;
            }
        }

        public PrHunk Build() => new(_oldStart, _oldCount, _newStart, _newCount, _lines);

        private bool IsExhausted =>
            _oldLine >= _oldStart + _oldCount && _newLine >= _newStart + _newCount;

        private static string Content(string line) => line.Length > 0 ? line[1..] : "";
    }
}
