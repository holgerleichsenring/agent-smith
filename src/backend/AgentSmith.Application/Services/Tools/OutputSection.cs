using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-10-07-6b9db: one section of a command's output as the server holds it — a head, a tail
/// (empty when the head holds everything held), and the counted <see cref="Total"/> the command
/// printed. <see cref="TailNotCaptured"/> marks a section whose end nobody kept, so its total is
/// a lower bound and no tail may be shown in its place (p0491: never a fake tail).
/// </summary>
internal readonly record struct OutputSection(string Head, string Tail, long Total, bool TailNotCaptured = false)
{
    public static OutputSection Of(StreamCapture stream) => new(stream.Head, stream.Tail, stream.Total);

    /// <summary>The section within <paramref name="budget"/>: unchanged when it fits, otherwise
    /// head and tail around a marker that names the true total. A section held in one piece is
    /// cut in its middle; one held in two is cut where the gap between them already is.</summary>
    public string Bound(int budget)
    {
        var bound = Tail.Length == 0 && !TailNotCaptured
            ? (string)ToolResultBound.Apply(Head, budget, Total)!
            : ToolResultBound.ApplyParts(Head, Tail, budget, Total);
        return bound.TrimEnd('\r', '\n');
    }

    public bool IsCut(int budget)
    {
        long held = Head.Length + Tail.Length;
        return TailNotCaptured || Total > held || held > budget;
    }

    /// <summary>The header line stating the section's total, e.g. <c>stdout_chars: 1234</c>.</summary>
    public string CountLine(string name) => TailNotCaptured
        ? $"{name}: at least {Total} (the tail was not captured)"
        : $"{name}: {Total}";
}
