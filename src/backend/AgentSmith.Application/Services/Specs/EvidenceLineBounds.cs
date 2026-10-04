namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-02-3f06b: whether every cited range lies inside a file of a known length. Lines count
/// from one, a range runs forward, and the last line is the file's own.
/// </summary>
internal static class EvidenceLineBounds
{
    public static EvidenceProblem? Problem(EvidenceReference reference, int lineCount)
    {
        foreach (var range in reference.Lines ?? [])
        {
            var cited = $"{reference.Path}:{range.From}" + (range.To == range.From ? string.Empty : $"-{range.To}");
            if (range.From < 1) return new EvidenceProblem("lines count from 1", cited);
            if (range.To < range.From) return new EvidenceProblem("the range runs backwards", cited);
            if (range.To > lineCount)
                return new EvidenceProblem($"past the end of a {lineCount}-line file", cited);
        }
        return null;
    }
}
