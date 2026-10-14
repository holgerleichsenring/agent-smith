namespace AgentSmith.Application.Services.Loop.Sanity;

/// <summary>
/// A search that found nothing in a path the current spec cites: grep_in_file, grep_in_tree or
/// find_files returned an empty text and its path or root is a cited path or lies inside a cited
/// directory. A whole-tree or ancestor search that finds nothing is often the right answer and is
/// not flagged. The line stays neutral — a zero-hit search is also how a free name is confirmed.
/// </summary>
public sealed class UncitedZeroHitRule : IToolResultSanityRule
{
    public string? Observe(ToolResultSanityCall call, ToolResultSanityScope scope)
    {
        var argument = call.Tool switch
        {
            "grep_in_file" => "path",
            "grep_in_tree" or "find_files" => "root",
            _ => null,
        };
        if (argument is null || scope.CitedPaths.Count == 0 || !string.IsNullOrWhiteSpace(call.Result))
            return null;
        var searched = scope.Normalise(call.Argument(argument) ?? ".");
        var cited = scope.CitedPaths.FirstOrDefault(searched.LiesIn);
        return cited is null
            ? null
            : $"{call.Tool} found nothing in {Display(searched)}, a path this spec cites ({Display(cited)}) — "
              + "verify the pattern and the path before concluding absence.";
    }

    private static string Display(ScopedPath path) =>
        path.Repository is null ? path.Path : $"{path.Repository}/{path.Path}";
}
