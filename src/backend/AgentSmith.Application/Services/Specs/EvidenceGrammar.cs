using System.Text.RegularExpressions;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-02-3f06b: the patterns of the evidence grammar, in one place so the reader and the
/// check cannot spell a line list or a date two ways.
/// </summary>
internal static partial class EvidenceGrammar
{
    /// <summary>Lines after a path's ':' — '12', '12-14', '3,7-9'.</summary>
    [GeneratedRegex(@"^\d+(-\d+)?(,\d+(-\d+)?)*$")]
    internal static partial Regex LineList();

    /// <summary>A repository qualifier in front of a path: 'agent-smith-skills:skills/…'.</summary>
    [GeneratedRegex(@"^(?<qualifier>[a-z][a-z0-9-]*):(?<rest>.*/.*)$")]
    internal static partial Regex Qualified();

    /// <summary>A root file is a path only with lines: 'CLAUDE.md:12'.</summary>
    [GeneratedRegex(@"^(?<path>[A-Za-z0-9_.-]*[A-Za-z0-9_-]\.[A-Za-z0-9]+):(?<lines>\d+(-\d+)?(,\d+(-\d+)?)*)$")]
    internal static partial Regex RootFileWithLines();

    [GeneratedRegex(@"^[a-z0-9-]+(\.[a-z0-9-]+)+$")]
    internal static partial Regex HostSegment();

    [GeneratedRegex(@"^\[L\d+\]")]
    internal static partial Regex Minted();

    [GeneratedRegex(@"\b\d{4}-\d{2}-\d{2}\b")]
    internal static partial Regex DateShaped();
}
